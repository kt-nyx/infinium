using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal static class Wp9ProductionProfileEnrollmentRunner
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    internal static async Task<int> RunAsync(
        string manifestPath,
        string manifestSha256,
        string outputRoot,
        string productRoot)
    {
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("WP9 production enrollment requires Windows."); }
        manifestPath = Path.GetFullPath(manifestPath);
        outputRoot = Path.GetFullPath(outputRoot);
        productRoot = Path.GetFullPath(productRoot);
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(manifestBytes)), manifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("WP9 production enrollment manifest bytes changed after authorization.");
        }
        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;
        JsonElement profile = root.GetProperty("profile");
        JsonElement providerIntent = root.GetProperty("provider_intent");
        if (root.GetProperty("schema_identity").GetString() != "infinium.repository.wp9-production-profile-authorization/1.0.0"
            || root.GetProperty("status").GetString() != "ready-for-owner-acceptance"
            || profile.GetProperty("mode").GetString() != "new-only")
        {
            throw new InvalidDataException("WP9 production enrollment requires the exact new-only accepted packet.");
        }
        string manifestId = root.GetProperty("manifest_id").GetString()!;
        string profileId = profile.GetProperty("access_profile_id").GetString()!;
        string generationId = profile.GetProperty("generation_id").GetString()!;
        string targetFingerprint = profile.GetProperty("target_fingerprint_sha256").GetString()!;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset expires = DateTimeOffset.Parse(root.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        if (now >= expires) { throw new InvalidDataException("WP9 production enrollment authority expired before coordinator admission."); }
        if (!Directory.Exists(outputRoot) || Directory.Exists(productRoot) || File.Exists(productRoot))
        {
            throw new InvalidOperationException("WP9 production enrollment requires its prepared output root and a fresh absent product root.");
        }

        string helperBinary = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        string helperSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helperBinary)));
        OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateWp9ProductionEnrollment(
            helperBinary, helperSha256, manifestPath, manifestSha256, manifestId);
        Directory.CreateDirectory(Path.GetDirectoryName(productRoot)!);
        using AuthoritativeStore store = new(new StoragePaths(productRoot));
        store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, now);
        CredentialProfileProjection pending = store.BeginCredentialEnrollment(
            profileId, generationId, profile.GetProperty("display_label").GetString()!, now.AddTicks(1),
            providerIntent.GetProperty("account_identity_id").GetString(),
            providerIntent.GetProperty("billing_scope_identity_id").GetString());
        if (pending.LifecycleState != "pending-enrollment" || pending.GenerationOrdinal != 1 || pending.RevocationEpoch != 0)
        {
            throw new InvalidOperationException("WP9 production profile did not begin at its exact new-generation state.");
        }
        CredentialHelperCoordinator coordinator = new(store, launcher);
        HelperPrivateFrameV2 bootstrap = Bootstrap(profileId, generationId, now);
        HelperPrivateFrameV2 assignment = Assignment(profileId, generationId);
        (CoordinatedHelperReceipt helper, CredentialProfileProjection projection) =
            await coordinator.ExecuteVerifiedEnrollmentAsync(
                "wp9-production-profile-enrollment", bootstrap, assignment, now.AddTicks(2)).ConfigureAwait(false);
        ValidateEffectReceipt(helper.Process, projection, targetFingerprint);
        object evidence = new
        {
            schema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v1",
            status = helper.Process.Receipt.Outcome == HelperOutcomeV2.Completed ? "passed" : "stopped",
            manifest_id = manifestId,
            manifest_sha256 = manifestSha256,
            profile_id = profileId,
            generation_id = generationId,
            target_fingerprint_sha256 = targetFingerprint,
            lifecycle_state = projection.LifecycleState,
            verification_state = projection.VerificationState,
            native_credential_operation_count = helper.Process.NativeCredentialOperationCount,
            native_call_trace = ParseOptional(helper.Process.NativeCallTraceBytes) ?? EmptyArray(),
            entry_evidence = ParseOptional(helper.Process.NativeEntryCleanupBytes),
            canaries = ParseOptional(helper.Process.NativeCanaryEvidenceBytes),
            network_operation_count = helper.Process.NetworkOperationCount,
            listener_count = helper.Process.ListenerCount,
            provider_operation_count = 0,
            billable_operation_count = 0,
            retry_attempted = helper.Process.RetryAttempted,
            retention = "exact-generation-retained-no-delete-authority",
            completed_at_utc = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",
                System.Globalization.CultureInfo.InvariantCulture),
        };
        string evidencePath = Path.Combine(outputRoot, "profile-enrollment-evidence.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, IndentedJson) + "\n", new UTF8Encoding(false));
        string summary = string.Join('\n',
            "WP9 production profile enrollment",
            $"status={(projection.LifecycleState == "active-verified" ? "passed" : "stopped")}",
            $"profile_id={profileId}",
            $"generation_id={generationId}",
            $"target_fingerprint_sha256={targetFingerprint}",
            $"lifecycle_state={projection.LifecycleState}",
            $"verification_state={projection.VerificationState}",
            $"native_calls={helper.Process.NativeCredentialOperationCount}",
            "network_operations=0",
            "provider_operations=0",
            "billable_operations=0",
            "retry_attempted=false",
            "qualification_request_authority=none") + "\n";
        File.WriteAllText(Path.Combine(outputRoot, "profile-enrollment-summary.txt"), summary, new UTF8Encoding(false));
        return projection.LifecycleState == "active-verified" ? 0 : 73;
    }

    private static void ValidateEffectReceipt(
        HelperProcessReceipt receipt,
        CredentialProfileProjection projection,
        string targetFingerprint)
    {
        if (receipt.NetworkOperationCount != 0 || receipt.ListenerCount != 0
            || receipt.RetryAttempted || receipt.StagedResponseBytes.Length != 0)
        {
            throw new InvalidDataException("WP9 production profile enrollment observed a forbidden transport or retry effect.");
        }
        if (receipt.Receipt.Outcome == HelperOutcomeV2.Completed)
        {
            using JsonDocument traceDocument = JsonDocument.Parse(receipt.NativeCallTraceBytes
                ?? throw new InvalidDataException("WP9 production enrollment omitted its native call trace."));
            JsonElement[] trace = traceDocument.RootElement.EnumerateArray().ToArray();
            string[] operations = trace.Select(item => item.GetProperty("Operation").GetString()!).ToArray();
            string[] results = trace.Select(item => item.GetProperty("Result").GetString()!).ToArray();
            if (!operations.SequenceEqual(["CredReadW", "CredWriteW", "CredReadW", "CredFree"])
                || !results.SequenceEqual(["ERROR_NOT_FOUND", "success", "success", "success"])
                || trace.Any(item => item.GetProperty("TargetFingerprintSha256").GetString() != targetFingerprint)
                || receipt.NativeCredentialOperationCount != 4
                || projection.LifecycleState != "active-verified"
                || projection.VerificationState != "available")
            {
                throw new InvalidDataException("WP9 production enrollment did not preserve the exact finite native and durable success grammar.");
            }
        }
        else if (receipt.NativeCredentialOperationCount != 0 || projection.LifecycleState != "pending-enrollment")
        {
            throw new InvalidDataException("A stopped WP9 production enrollment changed native or active profile state.");
        }
    }

    private static JsonElement? ParseOptional(byte[]? bytes)
    {
        if (bytes is null) { return null; }
        using JsonDocument document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    private static JsonElement EmptyArray()
    {
        using JsonDocument document = JsonDocument.Parse("[]"u8.ToArray());
        return document.RootElement.Clone();
    }

    private static HelperPrivateFrameV2 Bootstrap(string profileId, string generationId, DateTimeOffset now) => new()
    {
        Sequence = 1,
        ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
        Bootstrap = new()
        {
            CoordinatorFencingEpoch = 1,
            ExpiresAt = Instant(now.AddMinutes(11)),
            OneUseNonceFingerprintSha256 = ByteString.CopyFrom(RandomNumberGenerator.GetBytes(32)),
            CommandId = "wp9-production-profile-command",
            Credential = new() { AccessProfileId = new() { Value = profileId }, GenerationId = new() { Value = generationId } },
        },
    };

    private static HelperPrivateFrameV2 Assignment(string profileId, string generationId) => new()
    {
        Sequence = 2,
        ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
        Assignment = new()
        {
            AssignmentId = "wp9-production-profile/enroll-and-verify",
            CommandId = "wp9-production-profile-command",
            AssignmentKind = HelperAssignmentKindV2.Enroll,
            AccessProfileId = new() { Value = profileId },
            GenerationId = new() { Value = generationId },
            GenerationOrdinal = 1,
            Credential = new() { AccessProfileId = new() { Value = profileId }, GenerationId = new() { Value = generationId } },
        },
    };

    private static Instant Instant(DateTimeOffset value) => new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };
}
