using System.Diagnostics;
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

    internal static void ValidateCampaignAdmissionOnly(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        byte[] campaignBytes = File.ReadAllBytes(Path.GetFullPath(campaignManifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(credentialBytes)) != credentialManifestSha256
            || Convert.ToHexStringLower(SHA256.HashData(campaignBytes)) != campaignManifestSha256)
        {
            throw new InvalidDataException("Campaign credential admission probe bytes are stale.");
        }
        using JsonDocument credential = JsonDocument.Parse(credentialBytes);
        using JsonDocument campaign = JsonDocument.Parse(campaignBytes);
        JsonElement profile = credential.RootElement.GetProperty("profile");
        JsonElement envelope = campaign.RootElement.GetProperty("credential_envelope");
        if (credential.RootElement.GetProperty("manifest_id").GetString() is not string manifestId
            || envelope.GetProperty("profile_id").GetString() != profile.GetProperty("access_profile_id").GetString()
            || envelope.GetProperty("generation_id").GetString() != profile.GetProperty("generation_id").GetString()
            || envelope.GetProperty("target_fingerprint_sha256").GetString()
                != profile.GetProperty("target_fingerprint_sha256").GetString()
            || manifestId.Length == 0)
        {
            throw new InvalidDataException("Campaign credential admission probe envelope is stale.");
        }
        ValidateCommittedCampaignAuthority(Path.GetFullPath(campaignManifestPath), campaignManifestSha256,
            reviewedCandidate);
    }

    internal static void AdmitCampaignCredentialExecutionHandoff(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, DateTimeOffset now)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(credentialBytes)) != credentialManifestSha256)
        {
            throw new InvalidDataException("Campaign credential handoff admission has stale manifest bytes.");
        }
        using JsonDocument credential = JsonDocument.Parse(credentialBytes);
        JsonElement root = credential.RootElement;
        JsonElement profile = root.GetProperty("profile");
        DateTimeOffset expiry = DateTimeOffset.Parse(root.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        Wp9CampaignCredentialExecution campaign = new(campaignManifestPath, campaignManifestSha256,
            reviewedCandidate, ledgerPath);
        M1Slice6FiniteCampaignLedger ledger = OpenCampaignCredentialHandoff(campaign, root,
            root.GetProperty("manifest_id").GetString()!, credentialManifestSha256,
            profile.GetProperty("access_profile_id").GetString()!,
            profile.GetProperty("generation_id").GetString()!,
            profile.GetProperty("target_fingerprint_sha256").GetString()!, now, expiry,
            M1Slice6CampaignState.Ready);
        ledger.RecordIndependentReview(now.AddTicks(1));
        ledger.AdmitCampaign(now.AddTicks(2));
        ledger.BeginCredentialExecutionHandoff(now.AddTicks(3));
    }

    internal static void AcceptCampaignCredentialEvidence(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, string evidencePath, string repositoryRecordPath,
        DateTimeOffset now)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(credentialBytes)) != credentialManifestSha256)
        {
            throw new InvalidDataException("Credential evidence acceptance has stale manifest bytes.");
        }
        using JsonDocument credential = JsonDocument.Parse(credentialBytes);
        JsonElement profile = credential.RootElement.GetProperty("profile");
        DateTimeOffset expiry = DateTimeOffset.Parse(
            credential.RootElement.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        Wp9CampaignCredentialExecution campaign = new(campaignManifestPath, campaignManifestSha256,
            reviewedCandidate, ledgerPath);
        M1Slice6FiniteCampaignLedger ledger = OpenCampaignCredentialHandoff(campaign,
            credential.RootElement, credential.RootElement.GetProperty("manifest_id").GetString()!,
            credentialManifestSha256, profile.GetProperty("access_profile_id").GetString()!,
            profile.GetProperty("generation_id").GetString()!,
            profile.GetProperty("target_fingerprint_sha256").GetString()!, now,
            expiry, M1Slice6CampaignState.CredentialEvidenceHandoff);
        byte[] evidenceBytes = File.ReadAllBytes(Path.GetFullPath(evidencePath));
        string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(evidenceBytes));
        using JsonDocument evidence = JsonDocument.Parse(evidenceBytes);
        JsonElement root = evidence.RootElement;
        string[] exactProperties = ["schema", "status", "manifest_id", "manifest_sha256",
            "campaign_credential_handoff_event_hash", "profile_id", "generation_id",
            "target_fingerprint_sha256", "lifecycle_state", "verification_state",
            "native_credential_operation_count", "native_call_trace", "entry_evidence", "canaries",
            "network_operation_count", "listener_count", "provider_operation_count",
            "billable_operation_count", "retry_attempted", "containment", "namespace_reuse_blocked",
            "namespace_reuse_block_reason", "retention", "completed_at_utc"];
        M1Slice6CampaignLedgerEntry handoffPredecessor = ledger.Entries.Count >= 2
            ? ledger.Entries[^2]
            : throw new InvalidDataException("Credential evidence has no exact execution handoff predecessor.");
        JsonElement containment = root.GetProperty("containment");
        if (!root.EnumerateObject().Select(property => property.Name).SequenceEqual(exactProperties,
                StringComparer.Ordinal)
            || !containment.EnumerateObject().Select(property => property.Name).SequenceEqual(
                ["probe_executed", "excluded_handle_accessible", "process_tree_terminated",
                    "process_tree_survivor_count", "total_contained_process_count"], StringComparer.Ordinal)
            || root.GetProperty("schema").GetString()
                != "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v1"
            || root.GetProperty("status").GetString() != "passed-active-verified"
            || root.GetProperty("manifest_id").GetString()
                != credential.RootElement.GetProperty("manifest_id").GetString()
            || root.GetProperty("manifest_sha256").GetString() != credentialManifestSha256
            || root.GetProperty("profile_id").GetString() != profile.GetProperty("access_profile_id").GetString()
            || root.GetProperty("generation_id").GetString() != profile.GetProperty("generation_id").GetString()
            || root.GetProperty("target_fingerprint_sha256").GetString()
                != profile.GetProperty("target_fingerprint_sha256").GetString()
            || root.GetProperty("campaign_credential_handoff_event_hash").GetString()
                != handoffPredecessor.EventHash
            || root.GetProperty("lifecycle_state").GetString() != "active-verified"
            || root.GetProperty("verification_state").GetString() != "available"
            || root.GetProperty("native_credential_operation_count").GetInt32() != 4
            || root.GetProperty("network_operation_count").GetInt32() != 0
            || root.GetProperty("provider_operation_count").GetInt32() != 0
            || root.GetProperty("billable_operation_count").GetInt32() != 0
            || root.GetProperty("retry_attempted").GetBoolean()
            || !containment.GetProperty("probe_executed").GetBoolean()
            || containment.GetProperty("excluded_handle_accessible").GetBoolean()
            || !containment.GetProperty("process_tree_terminated").GetBoolean()
            || containment.GetProperty("process_tree_survivor_count").GetInt32() != 0
            || containment.GetProperty("total_contained_process_count").GetInt32() < 2
            || root.GetProperty("namespace_reuse_blocked").GetBoolean()
            || root.GetProperty("namespace_reuse_block_reason").ValueKind != JsonValueKind.Null
            || root.GetProperty("retention").GetString() != "exact-generation-retained-no-delete-authority"
            || ledger.Current.EvidenceSha256 != evidenceSha)
        {
            throw new InvalidDataException("Credential evidence is not the exact independently reviewable success handoff.");
        }
        using JsonDocument campaignDocument = JsonDocument.Parse(
            File.ReadAllBytes(Path.GetFullPath(campaignManifestPath)));
        JsonElement campaignRoot = campaignDocument.RootElement;
        string evidenceId = "wp9-production-profile-enrollment-evidence";
        string marker = "M1_S6_CAMPAIGN_CREDENTIAL_EVIDENCE_ACCEPTANCE campaign_id="
            + ledger.Current.Identity.CampaignId + " campaign_sha256=" + campaignManifestSha256
            + " manifest_id=" + ledger.Current.Identity.CredentialManifestId
            + " manifest_sha256=" + credentialManifestSha256
            + " evidence_id=" + evidenceId + " sha256=" + evidenceSha
            + " verdicts=credential,security,semantics,diff";
        string rolloverMarker = "WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION campaign_candidate_commit="
            + reviewedCandidate + " authority_sha256="
            + campaignRoot.GetProperty("authority_source").GetProperty("attachment_sha256").GetString()
            + " campaign_id=" + ledger.Current.Identity.CampaignId + " campaign_sha256=" + campaignManifestSha256
            + " manifest_id=" + ledger.Current.Identity.CredentialManifestId + " sha256=" + credentialManifestSha256
            + " close_ready_commit=" + credential.RootElement.GetProperty("candidate_binding")
                .GetProperty("close_ready_implementation_commit").GetString()
            + " credential_expires_at_utc=" + credential.RootElement.GetProperty("expires_at_utc").GetString();
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(repositoryRecordPath);
        const string recordRelative = "docs/plans/milestones/m1/slices/s6/record.md";
        string rolloverCommit = M1Slice6CampaignStageManifestValidator.FindUniqueMarkerCommit(
            repository, rolloverMarker, recordRelative);
        _ = M1Slice6CampaignStageManifestValidator.UniqueMarkerCommit(repository, marker,
            rolloverCommit, recordRelative);
        ledger.AcceptCredentialEvidence(evidenceId, evidenceSha,
            now);
    }

    internal static async Task<int> RunAsync(
        string manifestPath,
        string manifestSha256,
        string outputRoot,
        string productRoot,
        Wp9CampaignCredentialExecution? campaign = null)
    {
        try
        {
            return await RunCoreAsync(manifestPath, manifestSha256, outputRoot, productRoot, campaign).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            string durableState = TryMarkRecoveryBlocked(productRoot, manifestPath);
            RetainAmbiguousFailure(outputRoot, manifestPath, manifestSha256, exception, durableState);
            if (campaign is not null)
            {
                TryStopCampaignCredentialHandoff(campaign, manifestPath, manifestSha256, outputRoot,
                    "helper-evidence-ambiguity", DateTimeOffset.UtcNow);
            }
            throw;
        }
    }

    private static async Task<int> RunCoreAsync(
        string manifestPath,
        string manifestSha256,
        string outputRoot,
        string productRoot,
        Wp9CampaignCredentialExecution? campaign)
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

        M1Slice6FiniteCampaignLedger? campaignLedger = campaign is null ? null :
            OpenCampaignCredentialHandoff(campaign, root, manifestId, manifestSha256, profileId,
                generationId, targetFingerprint, now, expires);

        string helperBinary = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        string helperSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helperBinary)));
        string reviewedHelperSha256 = root.GetProperty("release_build").GetProperty("helper_sha256").GetString()!;
        if (!string.Equals(helperSha256, reviewedHelperSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("WP9 production enrollment helper differs from the exact reviewed Release binding.");
        }
        OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateWp9ProductionEnrollment(
            helperBinary, reviewedHelperSha256, manifestPath, manifestSha256, manifestId);
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
        string disposition = ValidateEffectReceipt(helper.Process, projection, targetFingerprint);
        if (disposition == "stopped-ambiguous-effect"
            && projection.LifecycleState != "recovery-required")
        {
            string intentKind = projection.LifecycleState == "pending-enrollment" ? "enroll" : "recover";
            projection = store.ApplyCredentialTransition(new(
                "wp9-production-profile-enrollment-recovery-block",
                profileId,
                generationId,
                intentKind,
                projection.LifecycleState,
                "recovery-required",
                "recovery-required",
                projection.CapabilitySnapshotId,
                projection.AccountIdentityId,
                projection.BillingScopeIdentityId,
                now.AddTicks(7),
                now.AddTicks(8),
                SecureStoreUnavailable: true));
        }
        object evidence = new
        {
            schema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v1",
            status = disposition,
            manifest_id = manifestId,
            manifest_sha256 = manifestSha256,
            campaign_credential_handoff_event_hash = campaignLedger?.Current.EventHash,
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
            containment = new
            {
                probe_executed = helper.Process.ContainmentProbeExecuted,
                excluded_handle_accessible = helper.Process.ExcludedHandleAccessible,
                process_tree_terminated = helper.Process.ProcessTreeTerminated,
                process_tree_survivor_count = helper.Process.ProcessTreeSurvivorCount,
                total_contained_process_count = helper.Process.TotalContainedProcessCount,
            },
            namespace_reuse_blocked = helper.Process.NativeNamespaceReuseBlocked,
            namespace_reuse_block_reason = helper.Process.NativeNamespaceReuseBlockReason,
            retention = "exact-generation-retained-no-delete-authority",
            completed_at_utc = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",
                System.Globalization.CultureInfo.InvariantCulture),
        };
        string evidencePath = Path.Combine(outputRoot, "profile-enrollment-evidence.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, IndentedJson) + "\n", new UTF8Encoding(false));
        if (disposition != "passed-active-verified")
        {
            object failure = new
            {
                schema = "infinium.m1-s6.wp9.production-profile-enrollment-failure/v1",
                status = disposition,
                failure_kind = helper.Process.Receipt.Outcome.ToString(),
                manifest_id = manifestId,
                manifest_sha256 = manifestSha256,
                profile_id = profileId,
                generation_id = generationId,
                target_fingerprint_sha256 = targetFingerprint,
                native_call_count_status = "known",
                native_credential_operation_count = helper.Process.NativeCredentialOperationCount,
                native_call_trace = ParseOptional(helper.Process.NativeCallTraceBytes) ?? EmptyArray(),
                allocation_free_pairing = FreePairing(helper.Process.NativeCallTraceBytes),
                canary_evidence = ParseOptional(helper.Process.NativeCanaryEvidenceBytes),
                ui_cleanup_evidence = ParseOptional(helper.Process.NativeEntryCleanupBytes),
                durable_lifecycle_state = projection.LifecycleState,
                durable_verification_state = projection.VerificationState,
                recovery_required = disposition is "stopped-native-failure" or "stopped-ambiguous-effect",
                provider_requests_blocked = true,
                retry_permitted = false,
                network_operation_count = helper.Process.NetworkOperationCount,
                provider_operation_count = 0,
                billable_operation_count = 0,
                containment_probe_executed = helper.Process.ContainmentProbeExecuted,
                process_tree_terminated = helper.Process.ProcessTreeTerminated,
                process_tree_survivor_count = helper.Process.ProcessTreeSurvivorCount,
                excluded_handle_accessible = helper.Process.ExcludedHandleAccessible,
            };
            File.WriteAllText(Path.Combine(outputRoot, "profile-enrollment-failure.json"),
                JsonSerializer.Serialize(failure, IndentedJson) + "\n", new UTF8Encoding(false));
        }
        string summary = string.Join('\n',
            "WP9 production profile enrollment",
            $"status={disposition}",
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
        if (campaignLedger is not null)
        {
            string evidenceSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidencePath)));
            if (disposition == "passed-active-verified")
            {
                campaignLedger.RecordCredentialEvidenceHandoff(
                    "wp9-production-profile-enrollment-evidence", evidenceSha256, DateTimeOffset.UtcNow);
            }
            else
            {
                string reason = disposition switch
                {
                    "stopped-owner-cancelled" => "owner-cancelled",
                    "stopped-existing-target-collision" => "preflight-collision",
                    "stopped-native-failure" => "native-failure",
                    _ => "cleanup-ambiguity",
                };
                campaignLedger.StopCredentialHandoff(reason,
                    "wp9-production-profile-enrollment-evidence", evidenceSha256, DateTimeOffset.UtcNow);
            }
        }
        return disposition == "passed-active-verified" ? 0 : 73;
    }

    private static M1Slice6FiniteCampaignLedger OpenCampaignCredentialHandoff(
        Wp9CampaignCredentialExecution campaign,
        JsonElement credentialManifest,
        string credentialManifestId,
        string credentialManifestSha256,
        string profileId,
        string generationId,
        string targetFingerprint,
        DateTimeOffset now,
        DateTimeOffset credentialExpiry,
        M1Slice6CampaignState expectedState = M1Slice6CampaignState.CredentialExecutionHandoff)
    {
        string campaignPath = Path.GetFullPath(campaign.ManifestPath);
        string ledgerPath = Path.GetFullPath(campaign.LedgerPath);
        byte[] bytes = File.ReadAllBytes(campaignPath);
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), campaign.ManifestSha256,
                StringComparison.Ordinal)
            || campaign.ReviewedCandidateCommit.Length != 40
            || campaign.ReviewedCandidateCommit.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsAsciiLetterUpper(character)))
        {
            throw new InvalidDataException("Campaign-derived credential handoff identity is stale.");
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement envelope = root.GetProperty("credential_envelope");
        JsonElement authority = root.GetProperty("authority_source");
        DateTimeOffset campaignExpiry = DateTimeOffset.Parse(root.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset envelopeExpiry = DateTimeOffset.Parse(
            envelope.GetProperty("credential_expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        if (root.GetProperty("schema_identity").GetString()
                != "infinium.repository.m1-slice6-finite-campaign-authorization/1.0.0"
            || root.GetProperty("status").GetString() != "ready-for-campaign-review"
            || now >= campaignExpiry || credentialExpiry != envelopeExpiry
            || envelope.GetProperty("profile_id").GetString() != profileId
            || envelope.GetProperty("generation_id").GetString() != generationId
            || envelope.GetProperty("target_fingerprint_sha256").GetString() != targetFingerprint
            || credentialManifest.GetProperty("manifest_id").GetString() != credentialManifestId)
        {
            throw new InvalidDataException("Campaign-derived credential handoff does not preserve the exact credential envelope.");
        }
        ValidateCommittedCampaignAuthority(campaignPath, campaign.ManifestSha256,
            campaign.ReviewedCandidateCommit);
        M1Slice6CampaignIdentity identity = new(
            root.GetProperty("campaign_id").GetString()!, campaign.ManifestSha256,
            authority.GetProperty("attachment_sha256").GetString()!, campaign.ReviewedCandidateCommit,
            credentialManifestId, credentialManifestSha256, profileId, generationId, targetFingerprint);
        M1Slice6FiniteCampaignLedger ledger = new(ledgerPath, identity, campaignExpiry, credentialExpiry, now);
        if (ledger.Current.State != expectedState)
        {
            throw new InvalidOperationException("Campaign credential operation requires its exact durable predecessor.");
        }
        return ledger;
    }

    private static void TryStopCampaignCredentialHandoff(Wp9CampaignCredentialExecution campaign,
        string credentialManifestPath, string credentialManifestSha256, string outputRoot,
        string reason, DateTimeOffset now)
    {
        try
        {
            using JsonDocument credential = JsonDocument.Parse(File.ReadAllBytes(credentialManifestPath));
            JsonElement profile = credential.RootElement.GetProperty("profile");
            DateTimeOffset expiry = DateTimeOffset.Parse(
                credential.RootElement.GetProperty("expires_at_utc").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
            M1Slice6FiniteCampaignLedger ledger = OpenCampaignCredentialHandoff(campaign,
                credential.RootElement, credential.RootElement.GetProperty("manifest_id").GetString()!,
                credentialManifestSha256, profile.GetProperty("access_profile_id").GetString()!,
                profile.GetProperty("generation_id").GetString()!,
                profile.GetProperty("target_fingerprint_sha256").GetString()!, now, expiry);
            string failurePath = Path.Combine(Path.GetFullPath(outputRoot), "profile-enrollment-failure.json");
            if (!File.Exists(failurePath)) { return; }
            string failureSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(failurePath)));
            ledger.StopCredentialHandoff(reason, "wp9-production-profile-enrollment-failure", failureSha, now);
        }
        catch
        {
            // The original retained failure remains authoritative. Never replace it with a secondary ledger error.
        }
    }

    private static void ValidateCommittedCampaignAuthority(
        string campaignManifestPath,
        string expectedManifestSha256,
        string expectedReviewedCandidate)
    {
        DirectoryInfo? cursor = new(Path.GetDirectoryName(campaignManifestPath)!);
        while (cursor is not null && !Directory.Exists(Path.Combine(cursor.FullName, ".git")))
        {
            cursor = cursor.Parent;
        }
        string repositoryRoot = cursor?.FullName
            ?? throw new InvalidDataException("Campaign-derived execution requires its exact Git worktree.");
        string validator = Path.Combine(repositoryRoot, "eng", "validate-m1-slice6-campaign.ps1");
        ProcessStartInfo start = new("pwsh")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
        {
            "-NoProfile", "-File", validator, "-AuthorizationManifest", campaignManifestPath,
            "-RequireState", "RolloverAdmitted",
        })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Campaign authority validator did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Campaign authority validator exceeded its pre-effect bound.");
        }
        string json = output.GetAwaiter().GetResult();
        string diagnostic = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException("Campaign authority validator rejected the committed rollover: "
                + diagnostic.GetType().Name);
        }
        using JsonDocument receipt = JsonDocument.Parse(json);
        if (receipt.RootElement.GetProperty("disposition").GetString() != "rollover-admitted"
            || receipt.RootElement.GetProperty("manifest_sha256").GetString() != expectedManifestSha256
            || receipt.RootElement.GetProperty("reviewed_candidate_commit").GetString() != expectedReviewedCandidate)
        {
            throw new InvalidDataException("Campaign authority receipt identity is stale.");
        }
    }

    internal static string ValidateEffectReceipt(
        HelperProcessReceipt receipt,
        CredentialProfileProjection projection,
        string targetFingerprint)
    {
        if (receipt.NetworkOperationCount != 0 || receipt.ListenerCount != 0
            || receipt.RetryAttempted || receipt.StagedResponseBytes.Length != 0
            || !receipt.ContainmentProbeExecuted || receipt.ExcludedHandleAccessible
            || !receipt.ProcessTreeTerminated || receipt.ProcessTreeSurvivorCount != 0
            || receipt.TotalContainedProcessCount < 2)
        {
            throw new InvalidDataException("WP9 production profile enrollment observed a forbidden transport or retry effect.");
        }
        if (receipt.Receipt.Outcome == HelperOutcomeV2.Completed)
        {
            if (receipt.NativeNamespaceReuseBlocked) { throw new InvalidDataException("A completed WP9 enrollment cannot block namespace reuse."); }
            using JsonDocument traceDocument = JsonDocument.Parse(receipt.NativeCallTraceBytes
                ?? throw new InvalidDataException("WP9 production enrollment omitted its native call trace."));
            JsonElement[] trace = traceDocument.RootElement.EnumerateArray().ToArray();
            string[] operations = trace.Select(item => item.GetProperty("Operation").GetString()!).ToArray();
            string[] results = trace.Select(item => item.GetProperty("Result").GetString()!).ToArray();
            if (!operations.SequenceEqual(["CredReadW", "CredWriteW", "CredReadW", "CredFree"])
                || !results.SequenceEqual(["ERROR_NOT_FOUND", "success", "success", "released"])
                || trace.Select((item, index) => item.GetProperty("Sequence").GetInt32() != index + 1).Any(value => value)
                || trace.Any(item => item.GetProperty("TargetFingerprintSha256").GetString() != targetFingerprint)
                || receipt.NativeCredentialOperationCount != 4
                || projection.LifecycleState != "active-verified"
                || projection.VerificationState != "available")
            {
                throw new InvalidDataException("WP9 production enrollment did not preserve the exact finite native and durable success grammar.");
            }
            ValidateExactFreePairing(trace, targetFingerprint);
            ValidateCanaryAndEntry(receipt, "submitted");
            return "passed-active-verified";
        }
        if (receipt.Receipt.Outcome == HelperOutcomeV2.Cancelled
            && receipt.NativeCredentialOperationCount == 0
            && projection.LifecycleState == "pending-enrollment"
            && !receipt.NativeNamespaceReuseBlocked)
        {
            ValidateCanaryAndEntry(receipt, "cancelled");
            return "stopped-owner-cancelled";
        }
        if (receipt.Receipt.Outcome == HelperOutcomeV2.FailedKnown
            && receipt.NativeNamespaceReuseBlocked
            && receipt.NativeNamespaceReuseBlockReason == "preflight-collision"
            && projection.LifecycleState == "pending-enrollment")
        {
            using JsonDocument collisionDocument = JsonDocument.Parse(receipt.NativeCallTraceBytes
                ?? throw new InvalidDataException("WP9 collision omitted its exact native trace."));
            JsonElement[] collision = collisionDocument.RootElement.EnumerateArray().ToArray();
            if (receipt.NativeCredentialOperationCount != 2 || collision.Length != 2
                || collision[0].GetProperty("Operation").GetString() != "CredReadW"
                || collision[0].GetProperty("Result").GetString() != "success"
                || collision[0].GetProperty("Sequence").GetInt32() != 1
                || collision[0].GetProperty("TargetFingerprintSha256").GetString() != targetFingerprint
                || collision[1].GetProperty("Operation").GetString() != "CredFree"
                || collision[1].GetProperty("Result").GetString() != "released"
                || collision[1].GetProperty("Sequence").GetInt32() != 2
                || collision[1].GetProperty("TargetFingerprintSha256").GetString() != targetFingerprint)
            {
                throw new InvalidDataException("WP9 collision did not preserve the exact R-success/F-released grammar.");
            }
            ValidateExactFreePairing(collision, targetFingerprint);
            ValidateCanaryAndEntry(receipt, "submitted");
            return "stopped-existing-target-collision";
        }
        using JsonDocument stoppedDocument = JsonDocument.Parse(receipt.NativeCallTraceBytes
            ?? throw new InvalidDataException("A stopped WP9 enrollment omitted its native trace."));
        JsonElement[] stoppedTrace = stoppedDocument.RootElement.EnumerateArray().ToArray();
        string[] expectedOperations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree"];
        string[] expectedResults = ["ERROR_NOT_FOUND", "success", "success", "released"];
        bool exactOperationPrefix = stoppedTrace.Select((item, index) =>
            item.GetProperty("Sequence").GetInt32() == index + 1
            && item.GetProperty("TargetFingerprintSha256").GetString() == targetFingerprint
            && item.GetProperty("Operation").GetString() == expectedOperations[index]).All(valid => valid);
        bool exactResultPrefix = stoppedTrace.Select((item, index) =>
            item.GetProperty("Result").GetString() == expectedResults[index]
            || index == stoppedTrace.Length - 1
                && item.GetProperty("Result").GetString()!.StartsWith("win32-error:", StringComparison.Ordinal))
            .All(valid => valid);
        if (receipt.Receipt.Outcome is not (HelperOutcomeV2.FailedKnown or HelperOutcomeV2.Unavailable)
            || receipt.NativeNamespaceReuseBlocked
            || receipt.NativeCredentialOperationCount is < 0 or > 4
            || receipt.NativeCredentialOperationCount != stoppedTrace.Length
            || projection.LifecycleState == "active-verified"
            || !exactOperationPrefix || !exactResultPrefix)
        {
            throw new InvalidDataException("A stopped WP9 production enrollment did not retain an exact safe success-prefix trace.");
        }
        ValidateCanaryAndEntry(receipt, "submitted");
        string pairing = FreePairing(receipt.NativeCallTraceBytes);
        bool measuredNativeFailure = stoppedTrace.Length > 0
            && stoppedTrace[^1].GetProperty("Result").GetString()!
                .StartsWith("win32-error:", StringComparison.Ordinal);
        return (measuredNativeFailure || receipt.Receipt.Outcome == HelperOutcomeV2.Unavailable
                && stoppedTrace.Length == 0) && pairing == "exactly-paired"
            ? "stopped-native-failure" : "stopped-ambiguous-effect";
    }

    private static void ValidateCanaryAndEntry(HelperProcessReceipt receipt, string terminalState)
    {
        using JsonDocument canary = JsonDocument.Parse(receipt.NativeCanaryEvidenceBytes
            ?? throw new InvalidDataException("WP9 production enrollment omitted canary evidence."));
        if (canary.RootElement.GetProperty("SecretMatches").GetInt32() != 0
            || canary.RootElement.GetProperty("RawTargetMatches").GetInt32() != 0
            || string.Join('|', canary.RootElement.GetProperty("RawTargetEncodings").EnumerateArray()
                .Select(item => item.GetString())) != "utf-8|utf-16le")
        {
            throw new InvalidDataException("WP9 production enrollment retained a secret or raw target canary.");
        }
        JsonElement[] surfaces = canary.RootElement.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        string[] expectedSurfaces =
        [
            "private protocol request|private-pipe-bytes",
            "private protocol response|private-pipe-bytes",
            "native call trace|canonical-trace-bytes",
            "process command line|captured-text",
            "process environment names|captured-text",
        ];
        string[] actualSurfaces = surfaces.Select(item =>
            $"{item.GetProperty("Name").GetString()}|{item.GetProperty("Kind").GetString()}").ToArray();
        if (!actualSurfaces.SequenceEqual(expectedSurfaces)
            || surfaces.Any(item => item.GetProperty("ByteCount").GetInt64() <= 0
                || item.GetProperty("SecretMatches").GetInt32() != 0
                || item.GetProperty("RawTargetMatches").GetInt32() != 0))
        {
            throw new InvalidDataException("WP9 production enrollment canary surface inventory is incomplete or nonzero.");
        }
        byte[] entryBytes = receipt.NativeEntryCleanupBytes
            ?? throw new InvalidDataException("WP9 production enrollment omitted entry cleanup evidence.");
        string entryJson = System.Text.Encoding.UTF8.GetString(entryBytes);
        CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(entryJson, terminalState);
        using JsonDocument entry = JsonDocument.Parse(entryBytes);
        JsonElement value = entry.RootElement;
        if (value.GetProperty("Surface").GetString() != "wp9-distinct-helper-owned-native-masked-paste-surface"
            || value.GetProperty("TerminalState").GetString() != terminalState
            || !value.GetProperty("Masked").GetBoolean() || !value.GetProperty("PastePermitted").GetBoolean()
            || !value.GetProperty("HelperOwned").GetBoolean() || value.GetProperty("RendererReceivedSecret").GetBoolean()
            || !value.GetProperty("InitiallyBlank").GetBoolean() || !value.GetProperty("Ready").GetBoolean()
            || !value.GetProperty("HelperProcessOwned").GetBoolean()
            || !value.GetProperty("SameSession").GetBoolean()
            || !value.GetProperty("InputDesktopAvailable").GetBoolean()
            || !value.GetProperty("NotCloaked").GetBoolean()
            || !value.GetProperty("OnMonitor").GetBoolean()
            || !value.GetProperty("Enabled").GetBoolean()
            || !value.GetProperty("Focused").GetBoolean()
            || !value.GetProperty("Foreground").GetBoolean()
            || !value.GetProperty("Active").GetBoolean()
            || value.GetProperty("ReadinessChecks").GetInt32() < 1
            || value.GetProperty("MessagePumpIterations").GetInt32() < 1
            || !value.GetProperty("WindowDestroyed").GetBoolean() || !value.GetProperty("BufferCleared").GetBoolean()
            || !value.GetProperty("NativeEditEmptyVerified").GetBoolean()
            || !value.GetProperty("ThreadJoined").GetBoolean())
        {
            throw new InvalidDataException("WP9 production entry readiness, ownership, masking, or cleanup evidence is incomplete.");
        }
    }

    private static string FreePairing(byte[]? traceBytes)
    {
        if (traceBytes is null) { return "unknown-no-trace"; }
        using JsonDocument trace = JsonDocument.Parse(traceBytes);
        JsonElement[] items = trace.RootElement.EnumerateArray().ToArray();
        try
        {
            ValidateExactFreePairing(items, null);
            return "exactly-paired";
        }
        catch (InvalidDataException) { return "ambiguous-recovery-required"; }
    }

    private static void ValidateExactFreePairing(JsonElement[] items, string? exactTarget)
    {
        JsonElement[] reads = items.Where(item => item.GetProperty("Operation").GetString() == "CredReadW"
            && item.GetProperty("Result").GetString() == "success").ToArray();
        JsonElement[] frees = items.Where(item => item.GetProperty("Operation").GetString() == "CredFree").ToArray();
        if (reads.Length != frees.Length) { throw new InvalidDataException("Successful native reads are not exactly released."); }
        foreach (JsonElement read in reads)
        {
            long allocation = read.GetProperty("AllocationId").GetInt64();
            JsonElement[] pairs = frees.Where(free =>
                free.GetProperty("Result").GetString() == "released"
                && free.GetProperty("PairedAllocationId").GetInt64() == allocation
                && free.GetProperty("Sequence").GetInt32() > read.GetProperty("Sequence").GetInt32()
                && free.GetProperty("TargetFingerprintSha256").GetString()
                    == read.GetProperty("TargetFingerprintSha256").GetString()
                && free.GetProperty("Scenario").GetString() == read.GetProperty("Scenario").GetString()).ToArray();
            if (pairs.Length != 1 || exactTarget is not null
                && read.GetProperty("TargetFingerprintSha256").GetString() != exactTarget)
            {
                throw new InvalidDataException("A native allocation lacks one exact later CredFree pairing.");
            }
        }
    }

    internal static void RetainAmbiguousFailure(
        string outputRoot,
        string manifestPath,
        string manifestSha256,
        Exception exception,
        string durableState)
    {
        try
        {
            outputRoot = Path.GetFullPath(outputRoot);
            if (!Directory.Exists(outputRoot)) { return; }
            string path = Path.Combine(outputRoot, "profile-enrollment-failure.json");
            string? manifestId = null;
            string? profileId = null;
            string? generationId = null;
            string? fingerprint = null;
            using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath)))
            {
                manifestId = manifest.RootElement.GetProperty("manifest_id").GetString();
                JsonElement profile = manifest.RootElement.GetProperty("profile");
                profileId = profile.GetProperty("access_profile_id").GetString();
                generationId = profile.GetProperty("generation_id").GetString();
                fingerprint = profile.GetProperty("target_fingerprint_sha256").GetString();
            }
            CredentialNativeHelperFailureException? typedFailure = exception as CredentialNativeHelperFailureException;
            NativeHelperFailureEnvelope? retained = typedFailure?.Evidence;
            NativeHelperFailureContainmentEvidence? containment = typedFailure?.Containment
                ?? (exception as CredentialNativeHelperEvidenceAmbiguityException)?.Containment;
            object? retainedTrace = retained?.NativeCallTraceJson is null
                ? null : JsonSerializer.Deserialize<object>(retained.NativeCallTraceJson);
            object? retainedCanaries = retained?.CanaryEvidenceJson is null
                ? null : JsonSerializer.Deserialize<object>(retained.CanaryEvidenceJson);
            object? retainedEntry = retained?.EntryCleanupJson is null
                ? null : JsonSerializer.Deserialize<object>(retained.EntryCleanupJson);
            object failure = new
            {
                schema = "infinium.m1-s6.wp9.production-profile-enrollment-failure/v1",
                status = "stopped-ambiguous-effect",
                failure_kind = exception.GetType().Name,
                manifest_id = manifestId,
                manifest_sha256 = manifestSha256,
                profile_id = profileId,
                generation_id = generationId,
                target_fingerprint_sha256 = fingerprint,
                native_call_count_status = retained?.CallCountsKnown == true ? "known" : "unknown-helper-or-evidence-failure",
                native_credential_operation_count = retained?.CallCountsKnown == true ? retained.Total : (int?)null,
                native_call_trace = retainedTrace,
                allocation_free_pairing = retained?.CallCountsKnown == true
                    ? FreePairing(retained.NativeCallTraceJson is null ? null : Encoding.UTF8.GetBytes(retained.NativeCallTraceJson))
                    : "unknown-recovery-required",
                canary_evidence = retainedCanaries ?? "unknown-recovery-required",
                ui_cleanup_evidence = retainedEntry ?? "unknown-recovery-required",
                durable_lifecycle_state = durableState,
                durable_verification_state = "unavailable",
                recovery_required = true,
                provider_requests_blocked = true,
                retry_permitted = false,
                network_operation_count = retained?.NetworkFactsKnown == true
                    ? retained.NetworkOperationCount : (int?)null,
                provider_operation_count = 0,
                billable_operation_count = 0,
                containment_probe_executed = retained?.ContainmentDescendantStarted ?? false,
                process_tree_terminated = containment?.ProcessTreeTerminated ?? false,
                process_tree_survivor_count = containment?.ProcessTreeSurvivorCount,
                excluded_handle_accessible = (bool?)null,
                typed_failure_details = exception switch
                {
                    CredentialNativeHelperFailureException typedDetail => (object)new
                    {
                        kind = "typed-helper-failure",
                        assignment_id = typedDetail.AssignmentId,
                        evidence = (object?)typedDetail.Evidence,
                        containment = typedDetail.Containment,
                    },
                    CredentialNativeHelperEvidenceAmbiguityException ambiguity => (object)new
                    {
                        kind = "helper-evidence-ambiguity",
                        assignment_id = ambiguity.AssignmentId,
                        validation_stage = ambiguity.ValidationStage,
                        containment = ambiguity.Containment,
                        evidence = (object?)null,
                        unvalidated_envelope_summary = ambiguity.EnvelopeSummary,
                    },
                    _ => (object)new
                    {
                        kind = "coordinator-failure",
                        assignment_id = (string?)null,
                        validation_stage = (string?)null,
                        containment = (object?)null,
                        evidence = (object?)null,
                    },
                },
            };
            if (!File.Exists(path))
            {
                using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                JsonSerializer.Serialize(stream, failure, IndentedJson);
                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
            }
            object main = new
            {
                schema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v1",
                status = "stopped-ambiguous-effect",
                manifest_id = manifestId,
                manifest_sha256 = manifestSha256,
                profile_id = profileId,
                generation_id = generationId,
                target_fingerprint_sha256 = fingerprint,
                lifecycle_state = durableState,
                verification_state = "unavailable",
                native_credential_operation_count = retained?.CallCountsKnown == true ? retained.Total : (int?)null,
                native_call_trace = retainedTrace,
                entry_evidence = retainedEntry ?? "unknown-recovery-required",
                canaries = retainedCanaries ?? "unknown-recovery-required",
                network_operation_count = retained?.NetworkFactsKnown == true
                    ? retained.NetworkOperationCount : (int?)null,
                listener_count = retained?.NetworkFactsKnown == true ? retained.ListenerCount : (int?)null,
                provider_operation_count = 0,
                billable_operation_count = 0,
                retry_attempted = false,
                recovery_required = true,
                qualification_request_authority = "none",
            };
            string mainPath = Path.Combine(outputRoot, "profile-enrollment-evidence.json");
            if (!File.Exists(mainPath))
            {
                using FileStream mainStream = new(mainPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                JsonSerializer.Serialize(mainStream, main, IndentedJson);
                mainStream.WriteByte((byte)'\n');
                mainStream.Flush(flushToDisk: true);
            }
            string summaryPath = Path.Combine(outputRoot, "profile-enrollment-summary.txt");
            if (!File.Exists(summaryPath))
            {
                File.WriteAllText(summaryPath,
                    "WP9 production profile enrollment\nstatus=stopped-ambiguous-effect\n"
                    + $"profile_id={profileId}\ngeneration_id={generationId}\n"
                    + $"target_fingerprint_sha256={fingerprint}\n"
                    + $"lifecycle_state={durableState}\nverification_state=unavailable\n"
                    + "native_calls=unknown\nnetwork_operations=unknown\nprovider_operations=0\n"
                    + "billable_operations=0\nretry_attempted=false\nrecovery_required=true\n"
                    + "qualification_request_authority=none\n", new UTF8Encoding(false));
            }
        }
        catch
        {
            // The original typed failure remains primary; the PowerShell
            // runner writes a final fallback receipt if this path cannot.
        }
    }

    private static string TryMarkRecoveryBlocked(string productRoot, string manifestPath)
    {
        try
        {
            productRoot = Path.GetFullPath(productRoot);
            if (!Directory.Exists(productRoot)) { return "not-materialized"; }
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            JsonElement profile = manifest.RootElement.GetProperty("profile");
            string profileId = profile.GetProperty("access_profile_id").GetString()!;
            string generationId = profile.GetProperty("generation_id").GetString()!;
            using AuthoritativeStore store = new(new StoragePaths(productRoot));
            CredentialProfileProjection current = store.GetCredentialProfile(profileId);
            if (current.LifecycleState == "recovery-required") { return current.LifecycleState; }
            DateTimeOffset now = DateTimeOffset.UtcNow;
            CredentialProfileProjection blocked = store.ApplyCredentialTransition(new(
                "wp9-production-profile-retained-ambiguity-block",
                profileId,
                generationId,
                current.LifecycleState == "pending-enrollment" ? "enroll" : "recover",
                current.LifecycleState,
                "recovery-required",
                "recovery-required",
                current.CapabilitySnapshotId,
                current.AccountIdentityId,
                current.BillingScopeIdentityId,
                now,
                now.AddTicks(1),
                Failed: true));
            return blocked.LifecycleState;
        }
        catch
        {
            // Retained ambiguity evidence remains authoritative and blocks all
            // provider requests even if the durable marker cannot be advanced.
            return "unknown-transition-failed-inspection-required";
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

internal sealed record Wp9CampaignCredentialExecution(
    string ManifestPath,
    string ManifestSha256,
    string ReviewedCandidateCommit,
    string LedgerPath);
