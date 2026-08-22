using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Evaluation;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal static class M1Slice6SuccessorCredentialReplacementRunner
{
    internal const string AuthoritySchema =
        "infinium.repository.m1-slice6-successor-credential-replacement-authorization/1.0.0";
    internal const string AuthoritySchemaV2 =
        "infinium.repository.m1-slice6-successor-credential-replacement-authorization/2.0.0";
    internal const string ReviewSchema =
        "infinium.repository.m1-slice6-successor-credential-replacement-review/1.0.0";
    internal const string EvidenceSchema =
        "infinium.m1-s6.successor-credential-replacement-evidence/v1";
    internal const string EvidenceSchemaV2 =
        "infinium.m1-s6.successor-credential-replacement-evidence/v2";
    internal const string EvidenceSchemaV3 =
        "infinium.m1-s6.successor-credential-replacement-evidence/v3";
    internal const string FailureEvidenceSchema =
        "infinium.m1-s6.successor-credential-replacement-failure-evidence/v1";
    internal const string BoundarySchema =
        "infinium.m1-s6.successor-credential-replacement-helper-boundary/v2";
    internal const string RecoveryAmendmentSchema =
        "infinium.repository.m1-slice6-development-campaign-amendment/4.0.0";
    internal const string CleanupRecoveryAmendmentSchema =
        "infinium.repository.m1-slice6-development-campaign-amendment/5.0.0";
    internal const string TypedFailureRecoveryAmendmentSchema =
        "infinium.repository.m1-slice6-development-campaign-amendment/6.0.0";
    internal const string ForegroundRecoveryAmendmentSchema =
        "infinium.repository.m1-slice6-development-campaign-amendment/7.0.0";
    internal const string Generation3ReplacementAmendmentSchema =
        "infinium.repository.m1-slice6-development-campaign-amendment/8.0.0";
    internal const string Generation3CorrectionCommit = "41c0f1918910a4f230cccf045331f87e57cf6d03";

    private enum ReplacementMode
    {
        Initial,
        ReplacingRecovery,
        DeletePendingRecovery,
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };
    private static readonly JsonSerializerOptions JsonLf = new(Json) { NewLine = "\n" };

    internal static async Task<int> RunDevelopmentEnrollmentAsync(CancellationToken cancellationToken)
    {
        string coordinatorPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The coordinator executable path is unavailable.");
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(coordinatorPath);
        string documents = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
        string authorityPath = Path.Combine(documents,
            "m1-slice6-successor-credential-replacement-generation-3-authorization.v2.json");
        string reviewPath = Path.Combine(documents,
            "m1-slice6-successor-credential-replacement-generation-3-review.v1.json");
        string productStateRoot = Path.Combine(repository, "artifacts", "m1-slice6", "successor-product-state");
        string ledgerPath = Path.Combine(repository, "artifacts", "m1-slice6", "successor-campaign", "ledger.v4.jsonl");
        string helperPath = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Release", "net10.0",
            "Infinium.CredentialHelper.exe");
        using FileStream enrollmentLock = AcquireDevelopmentEnrollmentLock(repository);
        string commit = AssemblyCommit().Split('+')[^1];
        string attemptId = DevelopmentAttemptIdentity(repository, commit);
        string evidencePath = Path.Combine(repository, "artifacts", "m1-slice6",
            "development-credential-continuation", commit + "-" + attemptId, "replacement-evidence.v3.json");
        return await RunAsync(authorityPath, M1Slice6SuccessorAuthorityLoader.HashFile(authorityPath),
            reviewPath, M1Slice6SuccessorAuthorityLoader.HashFile(reviewPath), productStateRoot,
            ledgerPath, helperPath, M1Slice6SuccessorAuthorityLoader.HashFile(helperPath),
            evidencePath, cancellationToken, developmentContinuation: true,
            developmentAttemptId: attemptId).ConfigureAwait(false);
    }

    internal static async Task<int> RunAsync(
        string authorityPath,
        string authoritySha256,
        string reviewPath,
        string reviewSha256,
        string productStateRoot,
        string ledgerPath,
        string helperPath,
        string helperSha256,
        string evidencePath,
        CancellationToken cancellationToken,
        ReplacementRunnerTestHooks? testHooks = null,
        bool developmentContinuation = false,
        string? developmentAttemptId = null)
    {
        ReplacementFailureContext? failureContext = null;
        try
        {
            return await RunCoreAsync(authorityPath, authoritySha256, reviewPath, reviewSha256,
                productStateRoot, ledgerPath, helperPath, helperSha256, evidencePath,
                context => failureContext = context, testHooks, developmentContinuation,
                developmentAttemptId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (failureContext is not null)
            {
                try
                {
                    RetainAmbiguousFailure(failureContext, exception);
                }
                catch (Exception retentionException)
                {
                    throw new InvalidOperationException(
                        "A possibly effected credential replacement could not retain trustworthy failure evidence.",
                        new AggregateException(exception, retentionException));
                }
            }
            throw;
        }
    }

    private static async Task<int> RunCoreAsync(
        string authorityPath,
        string authoritySha256,
        string reviewPath,
        string reviewSha256,
        string productStateRoot,
        string ledgerPath,
        string helperPath,
        string helperSha256,
        string evidencePath,
        Action<ReplacementFailureContext> admitFailureRetention,
        ReplacementRunnerTestHooks? testHooks,
        bool developmentContinuation,
        string? developmentAttemptId,
        CancellationToken cancellationToken)
    {
        authorityPath = Path.GetFullPath(authorityPath);
        reviewPath = Path.GetFullPath(reviewPath);
        productStateRoot = Path.GetFullPath(productStateRoot);
        ledgerPath = Path.GetFullPath(ledgerPath);
        helperPath = Path.GetFullPath(helperPath);
        evidencePath = Path.GetFullPath(evidencePath);
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(authorityPath);
        byte[] authorityBytes = ExactBytes(authorityPath, authoritySha256);
        string authoritySchema;
        using (JsonDocument identityDocument = JsonDocument.Parse(authorityBytes))
        {
            authoritySchema = identityDocument.RootElement.GetProperty("schema_identity").GetString()!;
        }
        bool generation3Replacement = authoritySchema == AuthoritySchemaV2;
        ActiveRepositoryJsonSchemaValidator.Validate(authorityBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                generation3Replacement
                    ? "m1-slice6-successor-credential-replacement-authorization.v2.schema.json"
                    : "m1-slice6-successor-credential-replacement-authorization.v1.schema.json")),
            generation3Replacement ? AuthoritySchemaV2 : AuthoritySchema);
        byte[] reviewBytes = ExactBytes(reviewPath, reviewSha256);
        ActiveRepositoryJsonSchemaValidator.Validate(reviewBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-review.v1.schema.json")),
            ReviewSchema);
        using JsonDocument authorityDocument = JsonDocument.Parse(authorityBytes);
        using JsonDocument reviewDocument = JsonDocument.Parse(reviewBytes);
        JsonElement authority = authorityDocument.RootElement;
        JsonElement review = reviewDocument.RootElement;
        JsonElement profile = authority.GetProperty("profile");
        JsonElement predecessor = authority.GetProperty("predecessor_ledger");
        JsonElement state = authority.GetProperty("product_state");
        JsonElement release = authority.GetProperty("release_build");
        JsonElement effect = authority.GetProperty("effect_boundary");
        JsonElement owner = authority.GetProperty("owner_authority");
        DateTimeOffset now = testHooks?.UtcNow ?? DateTimeOffset.UtcNow;
        DateTimeOffset prepared = UtcZ(authority.GetProperty("prepared_at_utc").GetString()!);
        DateTimeOffset notBefore = UtcZ(authority.GetProperty("not_before_utc").GetString()!);
        DateTimeOffset expires = UtcZ(authority.GetProperty("expires_at_utc").GetString()!);
        DateTimeOffset reviewedAt = UtcZ(review.GetProperty("reviewed_at_utc").GetString()!);
        string authorityId = authority.GetProperty("authority_id").GetString()!;
        if (developmentContinuation
            && (developmentAttemptId is null || developmentAttemptId.Length != 32
                || !Guid.TryParseExact(developmentAttemptId, "N", out _)))
        {
            throw new InvalidDataException("The development credential attempt identity is not a fresh canonical UUID.");
        }
        string evidenceId = developmentContinuation
            ? "infinium.m1-s6.development-credential-evidence/" + developmentAttemptId
            : authority.GetProperty("evidence_id").GetString()!;
        string profileId = profile.GetProperty("access_profile_id").GetString()!;
        string predecessorGeneration = profile.GetProperty("predecessor_generation_id").GetString()!;
        string successorGeneration = profile.GetProperty("successor_generation_id").GetString()!;
        long successorOrdinal = profile.GetProperty("successor_generation_ordinal").GetInt64();
        long predecessorOrdinal = successorOrdinal - 1;
        string predecessorFingerprint = profile.GetProperty("predecessor_target_fingerprint_sha256").GetString()!;
        string successorFingerprint = profile.GetProperty("successor_target_fingerprint_sha256").GetString()!;
        string operationId = developmentContinuation
            ? "m1s6-development-credential-" + developmentAttemptId
            : "m1s6-credential-replacement-" + authoritySha256[..32];
        if (authority.GetProperty("schema_identity").GetString() != authoritySchema
            || authoritySchema is not (AuthoritySchema or AuthoritySchemaV2)
            || authority.GetProperty("status").GetString() != "independently-reviewed-ready-for-owner-effect"
            || prepared > notBefore
            || reviewedAt < prepared || (!developmentContinuation && reviewedAt > now) || reviewedAt >= expires
            || review.GetProperty("schema_identity").GetString() != ReviewSchema
            || review.GetProperty("verdict").GetString() != "accept"
            || !review.GetProperty("independent").GetBoolean()
            || review.GetProperty("provider_effect_used").GetBoolean()
            || review.GetProperty("subject").GetProperty("id").GetString() != authorityId
            || review.GetProperty("subject").GetProperty("sha256").GetString() != authoritySha256
            || review.GetProperty("findings").GetArrayLength() != 0)
        {
            throw new InvalidDataException("The credential replacement authority or independent review is not effect-eligible.");
        }
        string ownerPath = Path.GetFullPath(Path.Combine(repository,
            owner.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        byte[] ownerBytes = ExactBytes(ownerPath, owner.GetProperty("sha256").GetString()!);
        ReplacementMode mode;
        using (JsonDocument ownerDocument = JsonDocument.Parse(ownerBytes))
        {
            string ownerSchema = ownerDocument.RootElement.GetProperty("schema_identity").GetString()!;
            mode = ownerSchema switch
            {
                RecoveryAmendmentSchema => ReplacementMode.ReplacingRecovery,
                CleanupRecoveryAmendmentSchema => ReplacementMode.DeletePendingRecovery,
                TypedFailureRecoveryAmendmentSchema => ReplacementMode.DeletePendingRecovery,
                ForegroundRecoveryAmendmentSchema => ReplacementMode.DeletePendingRecovery,
                Generation3ReplacementAmendmentSchema => ReplacementMode.Initial,
                _ => ReplacementMode.Initial,
            };
            if (developmentContinuation)
            {
                if (!generation3Replacement || ownerSchema != Generation3ReplacementAmendmentSchema)
                {
                    throw new InvalidDataException(
                        "The development credential continuation requires the retained generation-3 package.");
                }
                mode = ReplacementMode.DeletePendingRecovery;
            }
            string ownerSchemaFile = ownerSchema switch
            {
                RecoveryAmendmentSchema => "m1-slice6-development-campaign-amendment.v4.schema.json",
                CleanupRecoveryAmendmentSchema => "m1-slice6-development-campaign-amendment.v5.schema.json",
                TypedFailureRecoveryAmendmentSchema => "m1-slice6-development-campaign-amendment.v6.schema.json",
                ForegroundRecoveryAmendmentSchema => "m1-slice6-development-campaign-amendment.v7.schema.json",
                Generation3ReplacementAmendmentSchema =>
                    "m1-slice6-development-campaign-amendment.v8.schema.json",
                _ => "m1-slice6-development-campaign-amendment.v3.schema.json",
            };
            ActiveRepositoryJsonSchemaValidator.Validate(ownerBytes,
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository", ownerSchemaFile)),
                ownerSchema);
            if (ownerDocument.RootElement.GetProperty("amendment_id").GetString()
                    != owner.GetProperty("id").GetString()
                || ownerDocument.RootElement.GetProperty("status").GetString()
                    != (developmentContinuation
                        ? "owner-authorized-credential-replacement-generation-3"
                        : mode switch
                        {
                            ReplacementMode.ReplacingRecovery => "owner-authorized-credential-replacement-recovery",
                            ReplacementMode.DeletePendingRecovery =>
                                "owner-authorized-credential-replacement-cleanup-recovery",
                            _ => generation3Replacement
                                ? "owner-authorized-credential-replacement-generation-3"
                                : "owner-authorized-credential-replacement",
                        }))
            {
                throw new InvalidDataException("The exact owner credential-replacement amendment is stale.");
            }
            if (developmentContinuation)
            {
                ValidateGeneration3ReplacementOwner(
                    repository, ownerDocument.RootElement, testHooks?.Generation3OwnerLedgerPath);
            }
            else if (mode == ReplacementMode.ReplacingRecovery)
            {
                JsonElement priorOwner = ownerDocument.RootElement.GetProperty("prior_owner_authority");
                JsonElement failure = ownerDocument.RootElement.GetProperty("retained_failure");
                JsonElement recoveryNode = ownerDocument.RootElement.GetProperty("recovery");
                string priorOwnerPath = Path.GetFullPath(Path.Combine(repository,
                    priorOwner.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
                string exactPriorOwnerPath = Path.GetFullPath(Path.Combine(repository, "docs", "plans", "milestones",
                    "m1", "slices", "s6", "m1-slice6-development-campaign-amendment.v3.json"));
                byte[] priorOwnerBytes = ExactBytes(priorOwnerPath, priorOwner.GetProperty("sha256").GetString()!);
                ActiveRepositoryJsonSchemaValidator.Validate(priorOwnerBytes,
                    File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                        "m1-slice6-development-campaign-amendment.v3.schema.json")),
                    "infinium.repository.m1-slice6-development-campaign-amendment/3.0.0");
                string failurePath = Path.GetFullPath(Path.Combine(repository,
                    failure.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
                string exactFailurePath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
                    "successor-credential-replacement", "c2cf7e8c-dd55-4791-9eb1-f1e557f80124",
                    "replacement-evidence.v1.json"));
                byte[] failureBytes = ExactBytes(failurePath, failure.GetProperty("sha256").GetString()!);
                ActiveRepositoryJsonSchemaValidator.Validate(failureBytes,
                    File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                        "m1-slice6-successor-credential-replacement-failure-evidence.v1.schema.json")),
                    FailureEvidenceSchema);
                using JsonDocument failureDocument = JsonDocument.Parse(failureBytes);
                JsonElement retainedAuthority = failureDocument.RootElement.GetProperty("authority");
                JsonElement retainedReview = failureDocument.RootElement.GetProperty("independent_review");
                JsonElement retainedState = failureDocument.RootElement.GetProperty("product_state");
                if (priorOwnerPath != exactPriorOwnerPath
                    || failurePath != exactFailurePath
                    || priorOwner.GetProperty("id").GetString()
                        != "infinium.m1-s6.credential-replacement/20260821-owner-fresh-key"
                    || priorOwner.GetProperty("sha256").GetString()
                        != "088b5f2ae8198aa6bbec775fc25e1e1705b65d3f445e66b1e5d1427dd1660b47"
                    || recoveryNode.GetProperty("successor_generation_id").GetString() != successorGeneration
                    || failureDocument.RootElement.GetProperty("operation_id").GetString()
                        != "m1s6-credential-replacement-89e103f783b0f67f2bf5ee9e273c796c"
                    || failureDocument.RootElement.GetProperty("evidence_id").GetString()
                        != failure.GetProperty("id").GetString()
                    || retainedAuthority.GetProperty("id").GetString()
                        != "infinium.m1-s6.successor-credential-replacement/c2cf7e8c-dd55-4791-9eb1-f1e557f80124"
                    || retainedAuthority.GetProperty("sha256").GetString()
                        != "89e103f783b0f67f2bf5ee9e273c796c26474c4e2ed48dc1a73139da5b6bdad8"
                    || retainedReview.GetProperty("id").GetString()
                        != "infinium.m1-s6.successor-credential-replacement-review/8aad193f-c124-40ef-9f72-77e7f84c9788"
                    || retainedReview.GetProperty("sha256").GetString()
                        != "0e02ca9e9dc12f88035b51f0f0db8dfbe46ed63f9d81a978740c6b72bd6da0ba"
                    || retainedState.GetProperty("checkpoint_sha256").GetString()
                        != "7509d3d36d0512a7cbfdd704791cffcf625ce67e239388c47583daf8a2a5fecb"
                    || retainedState.GetProperty("profile_id").GetString()
                        != "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e"
                    || retainedState.GetProperty("generation_id").GetString()
                        != "g-ff6d82e7a7d244f6b8a9d0164991be37"
                    || retainedState.GetProperty("generation_ordinal").GetInt64() != 1
                    || retainedState.GetProperty("lifecycle_state").GetString() != "replacing"
                    || failureDocument.RootElement.GetProperty("typed_failure").GetString()
                        != "KeyNotFoundException")
                {
                    throw new InvalidDataException("The exact retained pre-native replacement failure is stale.");
                }
            }
            else if (mode == ReplacementMode.DeletePendingRecovery)
            {
                if (ownerSchema == ForegroundRecoveryAmendmentSchema)
                {
                    ValidateForegroundRecoveryOwner(
                        repository, ownerDocument.RootElement, successorGeneration);
                }
                else if (ownerSchema == TypedFailureRecoveryAmendmentSchema)
                {
                    ValidateTypedFailureRecoveryOwner(
                        repository, ownerDocument.RootElement, successorGeneration);
                }
                else
                {
                    ValidateDeletePendingRecoveryOwner(
                        repository, ownerDocument.RootElement, successorGeneration);
                }
            }
            else if (generation3Replacement)
            {
                ValidateGeneration3ReplacementOwner(
                    repository, ownerDocument.RootElement, testHooks?.Generation3OwnerLedgerPath);
            }
        }
        string expectedProductRoot = Path.GetFullPath(state.GetProperty("root_absolute").GetString()!);
        string closedProductRoot = testHooks?.ClosedProductRoot is null
            ? Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6", "successor-product-state"))
            : Path.GetFullPath(testHooks.ClosedProductRoot);
        string expectedLedger = Path.GetFullPath(Path.Combine(repository,
            predecessor.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string expectedHelper = developmentContinuation && testHooks is not null
            ? helperPath
            : developmentContinuation
                ? Path.GetFullPath(Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Release",
                    "net10.0", "Infinium.CredentialHelper.exe"))
                : Path.GetFullPath(Path.Combine(repository,
                    release.GetProperty("helper_path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string coordinatorPath = testHooks?.CoordinatorPath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("The coordinator executable path is unavailable.");
        string expectedCoordinator = developmentContinuation && testHooks is not null
            ? Path.GetFullPath(coordinatorPath)
            : developmentContinuation
            ? Path.GetFullPath(Path.Combine(repository, "src", "Infinium.Coordinator", "bin", "Release", "net10.0",
                "Infinium.Coordinator.exe"))
            : Path.GetFullPath(Path.Combine(repository,
                release.GetProperty("coordinator_path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string expectedEvidence = developmentContinuation && testHooks is not null
            ? evidencePath
            : developmentContinuation
            ? Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
                "development-credential-continuation", AssemblyCommit().Split('+')[^1] + "-" + developmentAttemptId,
                "replacement-evidence.v3.json"))
            : Path.GetFullPath(Path.Combine(repository,
                effect.GetProperty("evidence_path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string evidenceDirectory = Path.GetDirectoryName(evidencePath)!;
        bool exactRootCandidate = productStateRoot == expectedProductRoot && productStateRoot == closedProductRoot;
        bool replayBoundaryAlreadyDurable = exactRootCandidate && File.Exists(Path.Combine(
            productStateRoot, "staging", operationId, AuthoritativeStore.CredentialReplacementBoundaryFileName));
        bool evidenceDirectoryExists = Directory.Exists(evidenceDirectory);
        bool evidenceDirectoryIsEmpty = !evidenceDirectoryExists
            || !Directory.EnumerateFileSystemEntries(evidenceDirectory).Any();
        if (productStateRoot != expectedProductRoot || productStateRoot != closedProductRoot || ledgerPath != expectedLedger
            || helperPath != expectedHelper || Path.GetFullPath(coordinatorPath) != expectedCoordinator
            || evidencePath != expectedEvidence || !Directory.Exists(productStateRoot)
            || !File.Exists(ledgerPath) || !File.Exists(helperPath) || File.Exists(evidencePath)
            || evidenceDirectoryExists && (!evidenceDirectoryIsEmpty
                || !developmentContinuation && !replayBoundaryAlreadyDurable))
        {
            throw new InvalidDataException("The credential replacement effect roots differ from exact reviewed authority.");
        }
        string coordinatorSha = M1Slice6SuccessorAuthorityLoader.HashFile(coordinatorPath);
        bool exactDevelopmentBuild = !developmentContinuation || testHooks is not null
            || (ExecutableProductCommit(coordinatorPath) == AssemblyCommit().Split('+')[^1]
                && ExecutableProductCommit(helperPath) == AssemblyCommit().Split('+')[^1]);
        if (M1Slice6SuccessorAuthorityLoader.HashFile(ledgerPath) != predecessor.GetProperty("sha256").GetString()
            || M1Slice6SuccessorAuthorityLoader.HashFile(helperPath) != helperSha256
            || !exactDevelopmentBuild
            || (!developmentContinuation
                && (helperSha256 != release.GetProperty("helper_sha256").GetString()
                    || coordinatorSha != release.GetProperty("coordinator_sha256").GetString()
                    || !AssemblyCommit().EndsWith(release.GetProperty("implementation_commit").GetString()!,
                        StringComparison.Ordinal))))
        {
            throw new InvalidDataException("The reviewed replacement ledger or executable binding is stale.");
        }
        using (JsonDocument tailDocument = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last()))
        {
            JsonElement tail = tailDocument.RootElement;
            if (tail.GetProperty("sequence").GetInt64() != predecessor.GetProperty("sequence").GetInt64()
                || tail.GetProperty("event_hash").GetString() != predecessor.GetProperty("event_hash").GetString()
                || generation3Replacement && (tail.GetProperty("sequence").GetInt64() != 44
                    || tail.GetProperty("wp9_possible_starts").GetInt64() != 9
                    || tail.GetProperty("wp10_possible_starts").GetInt64() != 0
                    || tail.GetProperty("wp11_possible_starts").GetInt64() != 0
                    || tail.GetProperty("wp9_authoritative").GetBoolean()
                    || tail.GetProperty("wp10_authoritative").GetBoolean()
                    || tail.GetProperty("wp11_authoritative").GetBoolean()
                    || tail.GetProperty("successor_outstanding_reserved_nano_usd").GetInt64() != 0
                    || tail.GetProperty("successor_unresolved_nano_usd").GetInt64() != 880_640_000
                    || tail.GetProperty("prior_conservative_nano_usd").GetInt64()
                        + tail.GetProperty("successor_unresolved_nano_usd").GetInt64()
                        + tail.GetProperty("successor_settled_nano_usd").GetInt64() != 1_020_640_000))
            {
                throw new InvalidDataException("The replacement predecessor is not the exact retained ledger tip.");
            }
        }
        if (!generation3Replacement)
        {
            string campaignPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
                "m1-slice6-successor-campaign-authorization.v6.json");
            string amendmentPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
                "m1-slice6-development-campaign-amendment.v2.json");
            M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, M1Slice6SuccessorAuthorityLoader.HashFile(campaignPath));
            M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
                amendmentPath, M1Slice6SuccessorAuthorityLoader.HashFile(amendmentPath), campaign);
            M1Slice6SuccessorCampaignLedgerV3 validatedLedger = M1Slice6SuccessorCampaignRunner.OpenHardBudgetLedger(
                campaign, hardBudget, ledgerPath, now, requireExisting: true, amendmentReview: null);
            if (validatedLedger.Current.Sequence != 39
                || validatedLedger.Current.EventHash
                    != "b5292326a65791731614a6ab75a0b838de121ea721d01d5489f4c2897f88dcc0"
                || validatedLedger.Current.Wp9PossibleStarts != 8
                || validatedLedger.Current.Wp10PossibleStarts != 0
                || validatedLedger.Current.Wp11PossibleStarts != 0
                || validatedLedger.Current.Wp9Authoritative || validatedLedger.Current.Wp10Authoritative
                || validatedLedger.Current.Wp11Authoritative
                || validatedLedger.Current.SuccessorOutstandingReservedNanoUsd != 0
                || validatedLedger.Current.SuccessorUnresolvedNanoUsd != 770_560_000
                || validatedLedger.CommittedNanoUsd != 910_560_000)
            {
                throw new InvalidDataException(
                    "The replacement ledger does not preserve its exact accounting predecessor.");
            }
        }
        string beforeCheckpoint = developmentContinuation
            ? M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productStateRoot)
            : state.GetProperty("checkpoint_sha256").GetString()!;
        string stagedReceiptPath = Path.Combine(productStateRoot, "staging", operationId, "helper-receipt.v2.pb");
        string stagedBoundaryPath = Path.Combine(productStateRoot, "staging", operationId,
            AuthoritativeStore.CredentialReplacementBoundaryFileName);
        bool receiptExists = File.Exists(stagedReceiptPath);
        bool boundaryExists = File.Exists(stagedBoundaryPath);
        if (receiptExists && !boundaryExists)
        {
            throw new InvalidDataException(
                "A replacement receipt without its validated helper boundary cannot authorize replay.");
        }
        bool stagedReplay = boundaryExists;
        DateTimeOffset effectNotBefore = developmentContinuation ? now.AddSeconds(-1) : notBefore;
        DateTimeOffset effectExpires = developmentContinuation ? now.AddMinutes(11) : expires;
        if (!stagedReplay && (now < effectNotBefore || now >= effectExpires))
        {
            throw new InvalidDataException("The credential replacement effect window is not live.");
        }
        if (!stagedReplay
            && M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productStateRoot)
                != beforeCheckpoint)
        {
            throw new InvalidDataException("The credential replacement product-state checkpoint is stale.");
        }
        CoordinatedHelperReceipt helper;
        CredentialProfileProjection projection;
        using (AuthoritativeStore store = new(new StoragePaths(productStateRoot)))
        {
            CredentialProfileProjection current = store.GetCredentialProfile(profileId);
            bool exactInitial = mode == ReplacementMode.Initial
                && current.GenerationId == predecessorGeneration && current.GenerationOrdinal == predecessorOrdinal
                && current.LifecycleState == "active-verified" && current.VerificationState == "available";
            bool exactReplacingRecovery = mode == ReplacementMode.ReplacingRecovery
                && current.GenerationId == predecessorGeneration && current.GenerationOrdinal == 1
                && current.LifecycleState == "replacing" && current.VerificationState == "unavailable";
            bool exactDeletePendingRecovery = mode == ReplacementMode.DeletePendingRecovery
                && current.GenerationId == predecessorGeneration && current.GenerationOrdinal == predecessorOrdinal
                && current.LifecycleState == "delete-pending" && current.VerificationState == "unavailable"
                && current.CleanupDisposition == "failed"
                && store.IsCredentialReplacementCleanupRecovery(
                    profileId, predecessorGeneration, successorGeneration);
            bool exactPublishedReplay = stagedReplay && mode == ReplacementMode.DeletePendingRecovery
                && current.GenerationId == successorGeneration && current.GenerationOrdinal == successorOrdinal
                && current.LifecycleState == "active-verified" && current.VerificationState == "available"
                && store.HasExactCompletedCredentialTransition(
                    operationId + "-replacement-cleanup-recovered", profileId, successorGeneration,
                    "recover", "delete-pending", "active-unverified", "active-unverified", false)
                && store.HasExactCompletedCredentialTransition(
                    operationId + "-verified-generation", profileId, successorGeneration,
                    "verify", "active-unverified", "active-verified", "active-verified", true);
            bool exactUnverifiedReplay = stagedReplay && mode == ReplacementMode.DeletePendingRecovery
                && current.GenerationId == successorGeneration && current.GenerationOrdinal == successorOrdinal
                && current.LifecycleState == "active-unverified" && current.VerificationState == "unavailable"
                && current.CleanupDisposition == "not-requested"
                && store.HasExactCompletedCredentialTransition(
                    operationId + "-replacement-cleanup-recovered", profileId, successorGeneration,
                    "recover", "delete-pending", "active-unverified", "active-unverified", true);
            bool exactGeneration3ReplacingReplay = stagedReplay && generation3Replacement
                && current.GenerationId == predecessorGeneration && current.GenerationOrdinal == predecessorOrdinal
                && current.LifecycleState == "replacing" && current.VerificationState == "unavailable";
            bool exactGeneration3StoppedReplay = stagedReplay && generation3Replacement
                && current.GenerationId == predecessorGeneration && current.GenerationOrdinal == predecessorOrdinal
                && current.LifecycleState == "delete-pending" && current.VerificationState == "unavailable"
                && current.CleanupDisposition is "failed" or "pending";
            bool exactGeneration3PublishedReplay = stagedReplay && generation3Replacement
                && current.GenerationId == successorGeneration && current.GenerationOrdinal == successorOrdinal
                && current.LifecycleState == "active-verified" && current.VerificationState == "available"
                && store.HasExactCompletedCredentialTransition(
                    operationId + "-credential-transition", profileId, successorGeneration,
                    "replace", "replacing", "active-unverified", "active-unverified", false)
                && store.HasExactCompletedCredentialTransition(
                    operationId + "-verified-generation", profileId, successorGeneration,
                    "verify", "active-unverified", "active-verified", "active-verified", true);
            bool exactGeneration3UnverifiedReplay = stagedReplay && generation3Replacement
                && current.GenerationId == successorGeneration && current.GenerationOrdinal == successorOrdinal
                && current.LifecycleState == "active-unverified" && current.VerificationState == "unavailable"
                && current.CleanupDisposition == "not-requested"
                && store.HasExactCompletedCredentialTransition(
                    operationId + "-credential-transition", profileId, successorGeneration,
                    "replace", "replacing", "active-unverified", "active-unverified", true);
            if (!exactInitial && !exactReplacingRecovery && !exactDeletePendingRecovery
                && !exactPublishedReplay && !exactUnverifiedReplay
                && !exactGeneration3ReplacingReplay && !exactGeneration3StoppedReplay
                && !exactGeneration3PublishedReplay && !exactGeneration3UnverifiedReplay)
            {
                throw new InvalidDataException("The replacement predecessor is not the exact active verified generation.");
            }
            string expectedPredecessorGeneration = generation3Replacement
                ? "g-e6b6a3f21ad74108ba65955850349f83"
                : "g-ff6d82e7a7d244f6b8a9d0164991be37";
            string expectedPredecessorFingerprint = generation3Replacement
                ? "e7be7fd8ea2adef986806215f4a431174bab96b50789e855a2a18f96254c93ca"
                : "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
            if (profileId != "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e"
                || predecessorGeneration != expectedPredecessorGeneration
                || predecessorFingerprint != expectedPredecessorFingerprint
                || store.CredentialGenerationExists(profileId, successorGeneration)
                    != (mode != ReplacementMode.Initial || stagedReplay && generation3Replacement)
                || (mode != ReplacementMode.Initial || stagedReplay && generation3Replacement)
                    && store.CredentialGenerationOrdinal(profileId, successorGeneration) != successorOrdinal)
            {
                throw new InvalidDataException("The credential replacement identity is stale or the successor is not fresh.");
            }
            admitFailureRetention(new ReplacementFailureContext(
                repository, authorityId, authoritySha256, evidenceId, operationId,
                review.GetProperty("review_id").GetString()!, reviewSha256,
                profileId, productStateRoot, evidencePath));
            CredentialProfileProjection replacing = mode == ReplacementMode.Initial && !stagedReplay
                ? store.BeginCredentialReplacement(
                    operationId + "-begin", profileId, predecessorGeneration, successorGeneration,
                    successorOrdinal, now.AddTicks(1))
                : current;
            string admittedLifecycle = mode == ReplacementMode.DeletePendingRecovery ? "delete-pending" : "replacing";
            if (!exactPublishedReplay && !exactUnverifiedReplay
                && !exactGeneration3ReplacingReplay && !exactGeneration3StoppedReplay
                && !exactGeneration3PublishedReplay && !exactGeneration3UnverifiedReplay
                && (replacing.GenerationId != predecessorGeneration || replacing.LifecycleState != admittedLifecycle
                    || replacing.VerificationState != "unavailable"))
            {
                throw new InvalidOperationException("The durable replacement intent did not make the predecessor ineligible.");
            }
            bool cleanupRecovery = mode == ReplacementMode.DeletePendingRecovery;
            HelperPrivateFrameV2 assignment = Assignment(
                profileId, successorGeneration, authorityId,
                HelperAssignmentKindV2.Replace, checked((ulong)successorOrdinal));
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            if (stagedReplay)
            {
                CredentialHelperCoordinator coordinator = new(store);
                (helper, projection) = cleanupRecovery
                    ? coordinator.RecoverVerifiedReplacementCleanup(
                        repository, operationId, assignment,
                        predecessorFingerprint, successorFingerprint, helperSha256, now)
                    : coordinator.RecoverVerifiedSuccessorReplacement(
                        repository, operationId, assignment, predecessorGeneration,
                        predecessorFingerprint, successorFingerprint, helperSha256, now);
            }
            else
            {
                DateTimeOffset helperExpiry = effectExpires;
                HelperPrivateFrameV2 bootstrap = Bootstrap(
                    profileId, predecessorGeneration, now, helperExpiry, authorityId);
                using CancellationTokenSource effectDeadline =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                TimeSpan remaining = helperExpiry - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException("Replacement authority expired before helper launch.");
                }
                effectDeadline.CancelAfter(remaining);
                if (testHooks?.EffectExecutor is not null)
                {
                    (helper, projection) = await testHooks.EffectExecutor(
                        store, operationId, bootstrap, assignment, now, effectDeadline.Token).ConfigureAwait(false);
                }
                else
                {
                    OneShotCredentialHelperLauncher launcher =
                        OneShotCredentialHelperLauncher.CreateSuccessorCredentialReplacement(
                            helperPath, helperSha256, authorityPath, authoritySha256, authorityId,
                            developmentCredentialContinuation: developmentContinuation);
                    CredentialHelperCoordinator coordinator = new(store, launcher);
                    (helper, projection) = cleanupRecovery
                        ? await coordinator.ExecuteVerifiedReplacementCleanupAsync(
                            operationId, bootstrap, assignment, now,
                            cancellationToken: effectDeadline.Token).ConfigureAwait(false)
                        : generation3Replacement
                        ? await coordinator.ExecuteVerifiedSuccessorReplacementAsync(
                            repository, operationId, bootstrap, assignment, now,
                            cancellationToken: effectDeadline.Token).ConfigureAwait(false)
                        : await coordinator.ExecuteVerifiedReplacementAsync(
                            operationId, bootstrap, assignment, now, effectDeadline.Token).ConfigureAwait(false);
                }
            }
        }
        CredentialProfileProjection reopened = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
            productStateRoot, profileId);
        if (reopened != projection)
        {
            throw new InvalidDataException("The replacement projection changed across durable reopen.");
        }
        bool exactSuccess = helper.Process.Receipt.Outcome == HelperOutcomeV2.Completed
            && projection.GenerationId == successorGeneration
            && projection.GenerationOrdinal == successorOrdinal
            && projection.LifecycleState == "active-verified"
            && projection.VerificationState == "available";
        bool exactStopped = helper.Process.Receipt.Outcome is HelperOutcomeV2.Cancelled
                or HelperOutcomeV2.FailedKnown or HelperOutcomeV2.Unavailable
            && projection.GenerationId == predecessorGeneration
            && projection.GenerationOrdinal == predecessorOrdinal
            && projection.LifecycleState == "delete-pending"
            && projection.VerificationState == "unavailable"
            && (mode != ReplacementMode.DeletePendingRecovery
                || projection.CleanupDisposition == "failed");
        if (!exactSuccess && !exactStopped)
        {
            throw new InvalidDataException("The replacement helper outcome and final durable projection disagree.");
        }
        string status = exactSuccess
            ? "passed-active-verified-predecessor-absent"
            : "stopped-non-dispatchable-recovery-required";
        JsonElement trace = ParseOptional(helper.Process.NativeCallTraceBytes)
            ?? throw new InvalidDataException("Credential replacement omitted its exact native trace.");
        JsonElement canaries = ParseOptional(helper.Process.NativeCanaryEvidenceBytes)
            ?? throw new InvalidDataException("Credential replacement omitted its canary evidence.");
        JsonElement? entry = ParseOptional(helper.Process.NativeEntryCleanupBytes);
        ValidateReplacementHelperBoundary(
            helper.Process, predecessorFingerprint, successorFingerprint, exactSuccess);
        string evidenceSchema = generation3Replacement
            ? EvidenceSchemaV3
            : mode == ReplacementMode.DeletePendingRecovery ? EvidenceSchemaV2 : EvidenceSchema;
        object profileEvidence = mode == ReplacementMode.DeletePendingRecovery || generation3Replacement
            ? new
            {
                access_profile_id = profileId,
                predecessor_generation_id = predecessorGeneration,
                predecessor_target_fingerprint_sha256 = predecessorFingerprint,
                successor_generation_id = successorGeneration,
                successor_target_fingerprint_sha256 = successorFingerprint,
                final_generation_id = projection.GenerationId,
                final_generation_ordinal = projection.GenerationOrdinal,
                final_lifecycle_state = projection.LifecycleState,
                final_verification_state = projection.VerificationState,
                final_cleanup_disposition = projection.CleanupDisposition,
            }
            : new
            {
                access_profile_id = profileId,
                predecessor_generation_id = predecessorGeneration,
                predecessor_target_fingerprint_sha256 = predecessorFingerprint,
                successor_generation_id = successorGeneration,
                successor_target_fingerprint_sha256 = successorFingerprint,
                final_generation_id = projection.GenerationId,
                final_generation_ordinal = projection.GenerationOrdinal,
                final_lifecycle_state = projection.LifecycleState,
                final_verification_state = projection.VerificationState,
            };
        NativeHelperFailureEnvelope? validatedFailure = helper.ValidatedNativeFailureEnvelopeBytes is null
            ? null
            : NativeHelperFailureProtocol.DecodeCanonical(helper.ValidatedNativeFailureEnvelopeBytes);
        object effectEvidence = mode == ReplacementMode.DeletePendingRecovery || generation3Replacement
            ? new
            {
                helper_launch_count = 1,
                native_credential_operation_count = helper.Process.NativeCredentialOperationCount,
                native_call_trace = trace,
                entry_evidence = entry,
                canaries,
                staged_response_byte_length = helper.Process.StagedResponseBytes.Length,
                network_operation_count = helper.Process.NetworkOperationCount,
                listener_count = helper.Process.ListenerCount,
                provider_operation_count = 0,
                billable_operation_count = 0,
                retry_attempted = helper.Process.RetryAttempted,
                helper_outcome = helper.Process.Receipt.Outcome.ToString(),
                terminal_origin = validatedFailure is null
                    ? "helper-terminal-receipt"
                    : "validated-native-failure-envelope",
                validated_failure_envelope_sha256 = helper.ValidatedNativeFailureEnvelopeBytes is null
                    ? null
                    : Convert.ToHexStringLower(SHA256.HashData(
                        helper.ValidatedNativeFailureEnvelopeBytes)),
                failure_stage = validatedFailure?.Stage,
                failure_reason = validatedFailure?.Reason,
                namespace_reuse_blocked = helper.Process.NativeNamespaceReuseBlocked,
                namespace_reuse_block_reason = helper.Process.NativeNamespaceReuseBlockReason,
                containment = new
                {
                    probe_executed = helper.Process.ContainmentProbeExecuted,
                    process_tree_terminated = helper.Process.ProcessTreeTerminated,
                    process_tree_survivor_count = helper.Process.ProcessTreeSurvivorCount,
                    active_process_count_before_job_close = helper.Process.ActiveProcessCountBeforeJobClose,
                    total_contained_process_count = helper.Process.TotalContainedProcessCount,
                    excluded_handle_accessible = helper.Process.ExcludedHandleAccessible,
                },
            }
            : new
            {
                helper_launch_count = 1,
                native_credential_operation_count = helper.Process.NativeCredentialOperationCount,
                native_call_trace = trace,
                entry_evidence = entry,
                canaries,
                staged_response_byte_length = helper.Process.StagedResponseBytes.Length,
                network_operation_count = helper.Process.NetworkOperationCount,
                listener_count = helper.Process.ListenerCount,
                provider_operation_count = 0,
                billable_operation_count = 0,
                retry_attempted = helper.Process.RetryAttempted,
                helper_outcome = helper.Process.Receipt.Outcome.ToString(),
                namespace_reuse_blocked = helper.Process.NativeNamespaceReuseBlocked,
                namespace_reuse_block_reason = helper.Process.NativeNamespaceReuseBlockReason,
                containment = new
                {
                    probe_executed = helper.Process.ContainmentProbeExecuted,
                    process_tree_terminated = helper.Process.ProcessTreeTerminated,
                    process_tree_survivor_count = helper.Process.ProcessTreeSurvivorCount,
                    active_process_count_before_job_close = helper.Process.ActiveProcessCountBeforeJobClose,
                    total_contained_process_count = helper.Process.TotalContainedProcessCount,
                    excluded_handle_accessible = helper.Process.ExcludedHandleAccessible,
                },
            };
        object evidence = new
        {
            schema_identity = evidenceSchema,
            evidence_id = evidenceId,
            status,
            authority = new { id = authorityId, sha256 = authoritySha256 },
            independent_review = new
            {
                id = review.GetProperty("review_id").GetString(),
                sha256 = reviewSha256,
            },
            profile = profileEvidence,
            product_state = new
            {
                root_projection_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(productStateRoot))),
                checkpoint_before_sha256 = beforeCheckpoint,
                checkpoint_after_sha256 = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productStateRoot),
            },
            effect = effectEvidence,
            completed_at_utc = mode == ReplacementMode.DeletePendingRecovery || generation3Replacement
                ? CanonicalZ(DateTimeOffset.UtcNow)
                : DateTimeOffset.UtcNow.ToString("O"),
        };
        byte[] evidenceBytes = JsonSerializer.SerializeToUtf8Bytes(
            evidence, generation3Replacement ? JsonLf : Json);
        ActiveRepositoryJsonSchemaValidator.Validate(evidenceBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                generation3Replacement
                    ? "m1-slice6-successor-credential-replacement-evidence.v3.schema.json"
                    : mode == ReplacementMode.DeletePendingRecovery
                    ? "m1-slice6-successor-credential-replacement-evidence.v2.schema.json"
                    : "m1-slice6-successor-credential-replacement-evidence.v1.schema.json")),
            evidenceSchema);
        PublishEvidenceAtomically(evidencePath, evidenceBytes);
        return status == "passed-active-verified-predecessor-absent" ? 0 : 2;
    }

    internal static void ValidateGeneration3ReplacementOwner(
        string repository,
        JsonElement owner,
        string? expectedLedgerPathOverride = null)
    {
        const string attemptEvidenceSha =
            "b8f66af50409db241ab85920dc2686380c9ede4314f4c2161cc644f0441a8a46";
        const string attemptReviewSha =
            "abbf319aeeae271a02e45ff0c1afa95a4ccb48beb45df42fac6e2a7f0ebae081";
        const string replacementEvidenceSha =
            "4778cb8e9275c34a5eab70d32635261f5ebf9eda75247960e7389e01fe448feb";
        const string replacementReviewSha =
            "85d09936320fd393abacb337031b6519843e580fcb93fb10d93eb82aee9db160";
        const string ledgerSha =
            "232d0bc9d7f9a61f1cd87c9346cb2268aee6ea28e4be96a0e072df04ecbb4e27";
        const string ledgerEvent =
            "5dc8b3f2797620b305e3616d950c62e7b2f59d5b7c1ff6ce0a84f87b09e55e16";
        const string attemptEvidenceId =
            "successor-attempt-evidence-m1-s6-successor-v6-wp9-attempt-9/3637b14e-0d22-4a0c-88b2-c86bc57d871b";
        const string attemptReviewId =
            "infinium.m1-s6.successor-attempt-9-evidence-review-v3/8a34c582-888d-4be5-ba51-765222e084f1";
        const string replacementEvidenceId =
            "infinium.m1-s6.successor-credential-replacement-evidence/0dd95374-f9e1-400a-888d-ffd56f680214";
        const string replacementReviewId =
            "infinium.m1-s6.successor-credential-replacement-evidence-review-v3/18cabac3-02ce-4f54-b19d-778ee7f94367";

        JsonElement retainedWp9 = owner.GetProperty("retained_wp9_failure");
        JsonElement attemptBinding = retainedWp9.GetProperty("evidence");
        JsonElement attemptReviewBinding = retainedWp9.GetProperty("accepted_review");
        JsonElement replacementBinding = owner.GetProperty("retained_replacement_evidence");
        JsonElement replacementReviewBinding = owner.GetProperty("retained_replacement_review");
        JsonElement correction = owner.GetProperty("correction");
        JsonElement ledger = owner.GetProperty("ledger_predecessor");
        JsonElement replacement = owner.GetProperty("replacement");

        string exactAttemptPath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-campaign", "wp9-attempt-9-9e47065a5bd548edaf9ea6ef6ec2aa92",
            "attempt-evidence.v3.json"));
        string exactAttemptReviewPath = Path.Combine(Path.GetDirectoryName(exactAttemptPath)!,
            "attempt-evidence-review.v3.json");
        string exactReplacementPath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-credential-replacement-foreground-recovery",
            "68898542-71bb-464e-a381-7e3829d65a37", "replacement-evidence.v2.json"));
        string exactReplacementReviewPath = Path.GetFullPath(Path.Combine(repository, "docs", "plans",
            "milestones", "m1", "slices", "s6",
            "m1-slice6-successor-credential-replacement-foreground-recovery-evidence-review.v3.json"));
        string exactLedgerPath = expectedLedgerPathOverride is null
            ? Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
                "successor-campaign", "ledger.v4.jsonl"))
            : Path.GetFullPath(expectedLedgerPathOverride);

        static string Resolve(string root, JsonElement binding) => Path.GetFullPath(Path.Combine(root,
            binding.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string attemptPath = Resolve(repository, attemptBinding);
        string attemptReviewPath = Resolve(repository, attemptReviewBinding);
        string replacementPath = Resolve(repository, replacementBinding);
        string replacementReviewPath = Resolve(repository, replacementReviewBinding);
        string ledgerPath = Resolve(repository, ledger);
        if (attemptPath != exactAttemptPath || attemptReviewPath != exactAttemptReviewPath
            || replacementPath != exactReplacementPath || replacementReviewPath != exactReplacementReviewPath
            || ledgerPath != exactLedgerPath
            || attemptBinding.GetProperty("id").GetString() != attemptEvidenceId
            || attemptBinding.GetProperty("sha256").GetString() != attemptEvidenceSha
            || attemptReviewBinding.GetProperty("id").GetString() != attemptReviewId
            || attemptReviewBinding.GetProperty("sha256").GetString() != attemptReviewSha
            || replacementBinding.GetProperty("id").GetString() != replacementEvidenceId
            || replacementBinding.GetProperty("sha256").GetString() != replacementEvidenceSha
            || replacementReviewBinding.GetProperty("id").GetString() != replacementReviewId
            || replacementReviewBinding.GetProperty("sha256").GetString() != replacementReviewSha
            || ledger.GetProperty("sha256").GetString() != ledgerSha
            || ledger.GetProperty("sequence").GetInt64() != 44
            || ledger.GetProperty("event_hash").GetString() != ledgerEvent
            || ledger.GetProperty("committed_nano_usd").GetInt64() != 1_020_640_000
            || ledger.GetProperty("outstanding_nano_usd").GetInt64() != 0
            || retainedWp9.GetProperty("observed_submitted_character_length").GetInt64() != 91
            || retainedWp9.GetProperty("owner_confirmed_expected_character_length").GetInt64() != 164
            || correction.GetProperty("implementation_commit").GetString() != Generation3CorrectionCommit
            || correction.GetProperty("exact_character_length").GetInt64() != 164
            || correction.GetProperty("exact_utf8_byte_length").GetInt64() != 164
            || replacement.GetProperty("predecessor_generation_ordinal").GetInt64() != 2
            || replacement.GetProperty("successor_generation_ordinal").GetInt64() != 3
            || replacement.GetProperty("helper_launches").GetInt64() != 1
            || replacement.GetProperty("assignment_kind").GetString() != "Replace"
            || replacement.GetProperty("automatic_retry").GetBoolean()
            || replacement.GetProperty("dns_resolutions").GetInt64() != 0
            || replacement.GetProperty("network_operations").GetInt64() != 0
            || replacement.GetProperty("provider_operations").GetInt64() != 0
            || replacement.GetProperty("billable_operations").GetInt64() != 0)
        {
            throw new InvalidDataException("The generation-3 replacement owner lineage is not exact.");
        }

        byte[] attemptBytes = ExactBytes(attemptPath, attemptEvidenceSha);
        byte[] attemptReviewBytes = ExactBytes(attemptReviewPath, attemptReviewSha);
        byte[] replacementBytes = ExactBytes(replacementPath, replacementEvidenceSha);
        byte[] replacementReviewBytes = ExactBytes(replacementReviewPath, replacementReviewSha);
        _ = ExactBytes(ledgerPath, ledgerSha);
        ActiveRepositoryJsonSchemaValidator.Validate(attemptBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-attempt-evidence.v3.schema.json")),
            M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV3);
        ActiveRepositoryJsonSchemaValidator.Validate(attemptReviewBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-independent-review.v3.schema.json")),
            M1Slice6SuccessorAuthorityLoader.IndependentReviewSchemaV3);
        ActiveRepositoryJsonSchemaValidator.Validate(replacementBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-evidence.v2.schema.json")),
            EvidenceSchemaV2);
        ActiveRepositoryJsonSchemaValidator.Validate(replacementReviewBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-independent-review.v3.schema.json")),
            M1Slice6SuccessorAuthorityLoader.IndependentReviewSchemaV3);

        using JsonDocument attemptDocument = JsonDocument.Parse(attemptBytes);
        using JsonDocument attemptReviewDocument = JsonDocument.Parse(attemptReviewBytes);
        using JsonDocument replacementDocument = JsonDocument.Parse(replacementBytes);
        using JsonDocument replacementReviewDocument = JsonDocument.Parse(replacementReviewBytes);
        using JsonDocument ledgerTailDocument = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last());
        JsonElement attempt = attemptDocument.RootElement;
        JsonElement attemptReview = attemptReviewDocument.RootElement;
        JsonElement replacementEvidence = replacementDocument.RootElement;
        JsonElement replacementReview = replacementReviewDocument.RootElement;
        JsonElement replacementProfile = replacementEvidence.GetProperty("profile");
        JsonElement action = replacementEvidence.GetProperty("effect").GetProperty("entry_evidence")
            .GetProperty("ActionSnapshot");
        JsonElement tail = ledgerTailDocument.RootElement;
        if (attempt.GetProperty("status").GetString() != "failure-review-pending"
            || attempt.GetProperty("stage").GetString() != "Qualification"
            || attempt.GetProperty("work_package").GetString() != "WP9"
            || attempt.GetProperty("attempt_ordinal").GetInt64() != 9
            || attempt.GetProperty("attempt_id").GetString()
                != "m1-s6-successor-v6-wp9-attempt-9/3637b14e-0d22-4a0c-88b2-c86bc57d871b"
            || attempt.GetProperty("failure_disposition").GetString() != "provider-failed"
            || attempt.GetProperty("http_status").GetInt64() != 401
            || attempt.GetProperty("provider_error_code").GetString() != "invalid_api_key"
            || attempt.GetProperty("provider_send_count").GetInt64() != 1
            || attempt.GetProperty("dns_resolution_count").GetInt64() != 1
            || attempt.GetProperty("retry_permitted").GetBoolean()
            || attempt.GetProperty("unresolved_hold_nano_usd").GetInt64() != 110_080_000
            || !AcceptedReview(attemptReview, attemptReviewId, attemptEvidenceId, attemptEvidenceSha)
            || replacementEvidence.GetProperty("evidence_id").GetString() != replacementEvidenceId
            || replacementEvidence.GetProperty("status").GetString()
                != "passed-active-verified-predecessor-absent"
            || replacementProfile.GetProperty("successor_generation_id").GetString()
                != "g-e6b6a3f21ad74108ba65955850349f83"
            || replacementProfile.GetProperty("final_generation_id").GetString()
                != "g-e6b6a3f21ad74108ba65955850349f83"
            || replacementProfile.GetProperty("final_generation_ordinal").GetInt64() != 2
            || replacementProfile.GetProperty("final_lifecycle_state").GetString() != "active-verified"
            || replacementProfile.GetProperty("final_verification_state").GetString() != "available"
            || action.GetProperty("Action").GetString() != "submit"
            || action.GetProperty("CurrentCharacterLength").GetInt64() != 91
            || !action.GetProperty("Admitted").GetBoolean()
            || !AcceptedReview(replacementReview, replacementReviewId, replacementEvidenceId,
                replacementEvidenceSha)
            || tail.GetProperty("sequence").GetInt64() != 44
            || tail.GetProperty("event_hash").GetString() != ledgerEvent
            || tail.GetProperty("wp9_possible_starts").GetInt64() != 9
            || tail.GetProperty("successor_unresolved_nano_usd").GetInt64() != 880_640_000
            || tail.GetProperty("successor_outstanding_reserved_nano_usd").GetInt64() != 0)
        {
            throw new InvalidDataException("The generation-3 replacement retained evidence is not exact.");
        }

        static bool AcceptedReview(JsonElement review, string reviewId, string subjectId, string subjectSha)
        {
            JsonElement correctionNode = review.GetProperty("correction");
            return review.GetProperty("review_id").GetString() == reviewId
                && review.GetProperty("review_kind").GetString() == "attempt-evidence"
                && review.GetProperty("verdict").GetString() == "accept"
                && review.GetProperty("reviewer_id").GetString() == "/root/successor-design-review"
                && review.GetProperty("independent").GetBoolean()
                && !review.GetProperty("provider_effect_used").GetBoolean()
                && review.GetProperty("subject").GetProperty("id").GetString() == subjectId
                && review.GetProperty("subject").GetProperty("sha256").GetString() == subjectSha
                && !correctionNode.GetProperty("required").GetBoolean()
                && correctionNode.GetProperty("defect_id").ValueKind == JsonValueKind.Null
                && review.GetProperty("findings").GetArrayLength() == 0;
        }
    }

    private static void ValidateSuccessfulTrace(JsonElement trace, string predecessor, string successor, int count)
    {
        string[] operations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree",
            "CredReadW", "CredFree", "CredReadW", "CredFree", "CredDeleteW", "CredReadW", "CredReadW"];
        string[] fingerprints = [successor, successor, successor, successor,
            predecessor, predecessor, predecessor, predecessor, predecessor, predecessor, predecessor];
        string[] results = ["ERROR_NOT_FOUND", "success", "success", "released",
            "success", "released", "success", "released", "success", "ERROR_NOT_FOUND", "ERROR_NOT_FOUND"];
        JsonElement[] calls = trace.EnumerateArray().ToArray();
        bool fullDelete = calls.Length == operations.Length;
        bool predecessorAlreadyAbsent = calls.Length == 5;
        if (count != calls.Length || !fullDelete && !predecessorAlreadyAbsent)
        {
            throw new InvalidDataException("The successful replacement native call count is not exact.");
        }
        for (int index = 0; index < calls.Length; index++)
        {
            string[] names = ["Sequence", "Operation", "TargetFingerprintSha256", "Scenario", "Result",
                "AllocationId", "PairedAllocationId"];
            long? expectedAllocation = index switch { 2 => 1, 4 when fullDelete => 2, 6 => 3, _ => null };
            long? expectedPair = index switch { 3 => 1, 5 => 2, 7 => 3, _ => null };
            string expectedResult = predecessorAlreadyAbsent && index == 4 ? "ERROR_NOT_FOUND" : results[index];
            if (!calls[index].EnumerateObject().Select(property => property.Name)
                    .SequenceEqual(names, StringComparer.Ordinal)
                || calls[index].GetProperty("Sequence").GetInt64() != index + 1
                || calls[index].GetProperty("Operation").GetString() != operations[index]
                || calls[index].GetProperty("TargetFingerprintSha256").GetString() != fingerprints[index]
                || calls[index].GetProperty("Scenario").GetString()
                    != "m1-slice6-successor-credential-replacement"
                || calls[index].GetProperty("Result").GetString() != expectedResult
                || NullableInt64(calls[index], "AllocationId") != expectedAllocation
                || NullableInt64(calls[index], "PairedAllocationId") != expectedPair)
            {
                throw new InvalidDataException("The successful replacement native trace is not exact.");
            }
        }
    }

    private static void ValidateStoppedTrace(
        JsonElement trace,
        string predecessor,
        string successor,
        int count,
        HelperOutcomeV2 outcome)
    {
        string[] operations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree",
            "CredReadW", "CredFree", "CredReadW", "CredFree", "CredDeleteW", "CredReadW", "CredReadW"];
        string[] fingerprints = [successor, successor, successor, successor,
            predecessor, predecessor, predecessor, predecessor, predecessor, predecessor, predecessor];
        string[] results = ["ERROR_NOT_FOUND", "success", "success", "released",
            "success", "released", "success", "released", "success", "ERROR_NOT_FOUND", "ERROR_NOT_FOUND"];
        JsonElement[] calls = trace.EnumerateArray().ToArray();
        bool collision = calls.Length == 2
            && outcome == HelperOutcomeV2.FailedKnown;
        if (collision)
        {
            ValidateCollisionTrace(calls, successor, count);
            return;
        }
        if (count != calls.Length || calls.Length > operations.Length
            || calls.Length == 0 && outcome is not (HelperOutcomeV2.Cancelled
                or HelperOutcomeV2.FailedKnown or HelperOutcomeV2.Unavailable))
        {
            throw new InvalidDataException("The stopped replacement native trace count or empty outcome is invalid.");
        }
        HashSet<long> allocations = [];
        HashSet<long> released = [];
        for (int index = 0; index < calls.Length; index++)
        {
            JsonElement call = calls[index];
            string[] names = ["Sequence", "Operation", "TargetFingerprintSha256", "Scenario", "Result",
                "AllocationId", "PairedAllocationId"];
            string result = call.GetProperty("Result").GetString() ?? "";
            long? allocation = NullableInt64(call, "AllocationId");
            long? pair = NullableInt64(call, "PairedAllocationId");
            long? expectedAllocation = result == "success"
                ? index switch { 2 => 1, 4 => 2, 6 => 3, _ => null }
                : null;
            if (!call.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal)
                || call.GetProperty("Sequence").GetInt64() != index + 1
                || call.GetProperty("Scenario").GetString() != "m1-slice6-successor-credential-replacement"
                || call.GetProperty("Operation").GetString() != operations[index]
                || call.GetProperty("TargetFingerprintSha256").GetString() != fingerprints[index]
                || result != results[index]
                    && !(index == calls.Length - 1
                        && IsCanonicalWin32Error(result))
                || allocation != expectedAllocation
                || pair != (index switch { 3 => 1, 5 => 2, 7 => 3, _ => null }))
            {
                throw new InvalidDataException("The stopped replacement native trace is not an exact authorized prefix.");
            }
            if (allocation is not null && !allocations.Add(allocation.Value)
                || pair is not null && (!allocations.Contains(pair.Value) || !released.Add(pair.Value))
                || operations[index] == "CredFree" != (pair is not null)
                || operations[index] != "CredFree" && pair is not null)
            {
                throw new InvalidDataException("The stopped replacement allocation/free pairing is invalid.");
            }
        }
        if (!allocations.SetEquals(released))
        {
            throw new InvalidDataException("The stopped replacement retained an unpaired credential allocation.");
        }
    }

    private static bool IsCanonicalWin32Error(string value)
    {
        const string prefix = "win32-error:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        string suffix = value[prefix.Length..];
        return int.TryParse(
                suffix,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out int code)
            && code > 0
            && suffix == code.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static void ValidateReplacementHelperBoundary(
        HelperProcessReceipt process,
        string predecessorFingerprint,
        string successorFingerprint,
        bool requireCompleted)
    {
        if (process.ExitCode is not (0 or 72) || process.InheritedPrivateHandleCount != 2
            || process.StandardProtocolHandleCount != 0)
        {
            throw new InvalidDataException("The replacement helper launcher facts are not exact.");
        }
        JsonElement trace = ParseOptional(process.NativeCallTraceBytes)
            ?? throw new InvalidDataException("Credential replacement omitted its exact native trace.");
        JsonElement canaries = ParseOptional(process.NativeCanaryEvidenceBytes)
            ?? throw new InvalidDataException("Credential replacement omitted its canary evidence.");
        JsonElement? entry = ParseOptional(process.NativeEntryCleanupBytes);
        if (entry is null)
        {
            if (requireCompleted || process.Receipt.Outcome != HelperOutcomeV2.FailedKnown
                || process.NativeCredentialOperationCount != 0 || trace.GetArrayLength() != 0
                || process.NativeNamespaceReuseBlocked || process.NativeNamespaceReuseBlockReason is not null)
            {
                throw new InvalidDataException("Only an exact zero-native pre-entry failure may omit entry evidence.");
            }
            ValidatePreEntryStoppedBoundary(process, canaries);
            return;
        }
        if (requireCompleted)
        {
            ValidateSuccessfulBoundary(process, entry.Value, canaries);
            ValidateSuccessfulTrace(trace, predecessorFingerprint, successorFingerprint,
                process.NativeCredentialOperationCount);
            return;
        }
        ValidateStoppedBoundary(process, entry.Value, canaries);
        ValidateStoppedTrace(trace, predecessorFingerprint, successorFingerprint,
            process.NativeCredentialOperationCount, process.Receipt.Outcome);
        bool collision = trace.GetArrayLength() == 2
            && trace[0].GetProperty("Operation").GetString() == "CredReadW"
            && trace[0].GetProperty("Result").GetString() == "success";
        if (process.NativeNamespaceReuseBlocked != collision
            || collision && process.NativeNamespaceReuseBlockReason != "preflight-collision"
            || !collision && process.NativeNamespaceReuseBlockReason is not null)
        {
            throw new InvalidDataException("The stopped replacement collision and namespace-reuse facts disagree.");
        }
    }

    internal static void ValidateReplacementFailureEnvelope(
        NativeHelperFailureEnvelope evidence,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        JsonElement manifest)
    {
        JsonElement profile = manifest.GetProperty("profile");
        JsonElement maxima = manifest.GetProperty("native_boundary").GetProperty("maximum_calls");
        string authorityId = manifest.GetProperty("authority_id").GetString()!;
        string profileId = profile.GetProperty("access_profile_id").GetString()!;
        string predecessorGeneration = profile.GetProperty("predecessor_generation_id").GetString()!;
        string successorGeneration = profile.GetProperty("successor_generation_id").GetString()!;
        string predecessorFingerprint = profile.GetProperty(
            "predecessor_target_fingerprint_sha256").GetString()!;
        string successorFingerprint = profile.GetProperty(
            "successor_target_fingerprint_sha256").GetString()!;
        if (assignment.AssignmentKind != HelperAssignmentKindV2.Replace
            || assignment.AssignmentId != authorityId + "/replace"
            || assignment.CommandId != authorityId + "/command"
            || bootstrap.CommandId != authorityId + "/command"
            || bootstrap.Credential?.AccessProfileId?.Value != profileId
            || bootstrap.Credential?.GenerationId?.Value != predecessorGeneration
            || assignment.AccessProfileId?.Value != profileId
            || assignment.GenerationId?.Value != successorGeneration
            || assignment.GenerationOrdinal
                != profile.GetProperty("successor_generation_ordinal").GetUInt64()
            || assignment.Credential?.AccessProfileId?.Value != profileId
            || assignment.Credential?.GenerationId?.Value != successorGeneration
            || predecessorGeneration == successorGeneration
            || evidence.ContainmentProbeExecuted != true || evidence.ExcludedHandleAccessible != false
            || !evidence.ContainmentDescendantStarted || evidence.ContainmentDescendantProcessId <= 0
            || evidence.CredWriteW > maxima.GetProperty("CredWriteW").GetInt32()
            || evidence.CredReadW > maxima.GetProperty("CredReadW").GetInt32()
            || evidence.CredDeleteW > maxima.GetProperty("CredDeleteW").GetInt32()
            || evidence.CredFree > maxima.GetProperty("CredFree").GetInt32()
            || evidence.Total > maxima.GetProperty("total").GetInt32())
        {
            throw new InvalidDataException(
                "The replacement helper failure exceeds its exact authority, identity, or native bounds.");
        }
        JsonElement trace = ParseOptional(Encoding.UTF8.GetBytes(evidence.NativeCallTraceJson!))
            ?? throw new InvalidDataException("The replacement failure native trace is absent.");
        JsonElement canaries = ParseOptional(Encoding.UTF8.GetBytes(evidence.CanaryEvidenceJson!))
            ?? throw new InvalidDataException("The replacement failure canary evidence is absent.");
        JsonElement? entry = evidence.EntryCleanupJson is null
            ? null
            : ParseOptional(Encoding.UTF8.GetBytes(evidence.EntryCleanupJson));
        ValidateStoppedTrace(
            trace, predecessorFingerprint, successorFingerprint,
            evidence.Total, HelperOutcomeV2.FailedKnown);
        bool collision = trace.GetArrayLength() == 2
            && trace[0].GetProperty("Operation").GetString() == "CredReadW"
            && trace[0].GetProperty("Result").GetString() == "success";
        if (evidence.NamespaceReuseBlocked != collision
            || collision && evidence.NamespaceReuseBlockReason != "preflight-collision"
            || !collision && evidence.NamespaceReuseBlockReason is not null)
        {
            throw new InvalidDataException(
                "The replacement helper failure collision facts are not exact.");
        }
        if (!evidence.ManualUiAttempted)
        {
            if (entry is not null || evidence.CredWriteW != 0 || trace.GetArrayLength() != 0)
            {
                throw new InvalidDataException(
                    "A pre-entry replacement failure contains impossible UI or native mutation evidence.");
            }
        }
        else
        {
            JsonElement exactEntry = entry
                ?? throw new InvalidDataException("A replacement UI failure lacks exact cleanup evidence.");
            CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(
                evidence.EntryCleanupJson!);
            ValidateEntryProperties(exactEntry);
        }
        string responseSurface = evidence.Stage == "metrics-write"
            ? "private protocol response"
            : "private protocol partial response";
        ValidateCanaries(canaries, responseSurface, allowEmptyResponse: true);
    }

    private static void ValidatePreEntryStoppedBoundary(HelperProcessReceipt process, JsonElement canaries)
    {
        if (process.Receipt.Outcome != HelperOutcomeV2.FailedKnown
            || process.NetworkOperationCount != 0 || process.ListenerCount != 0 || process.RetryAttempted
            || process.StagedResponseBytes.Length != 0 || !process.ContainmentProbeExecuted
            || !process.ProcessTreeTerminated || process.ProcessTreeSurvivorCount != 0
            || process.TotalContainedProcessCount < 2 || process.ActiveProcessCountBeforeJobClose < 1
            || process.ExcludedHandleAccessible)
        {
            throw new InvalidDataException("The pre-entry replacement failure boundary is not exact or contained.");
        }
        ValidateProcessCanaries(process, canaries);
    }

    internal static byte[] CreateValidatedReplacementBoundary(
        string repository,
        string attemptId,
        CoordinatedHelperReceipt helper,
        string predecessorFingerprint,
        string successorFingerprint,
        byte[]? terminalReceiptBytes = null)
    {
        bool completed = helper.Process.Receipt.Outcome == HelperOutcomeV2.Completed;
        ValidateReplacementHelperBoundary(
            helper.Process, predecessorFingerprint, successorFingerprint, completed);
        ContentDigest expectedReceiptDigest = CredentialReceiptDigest(
            helper.Process.Receipt.AssignmentId,
            helper.Process.Receipt.CommandId,
            helper.Process.Receipt.Outcome);
        ContentDigest? actualReceiptDigest = helper.Process.Receipt.NonSecretReceipt;
        if (actualReceiptDigest is null
            || actualReceiptDigest.Algorithm != expectedReceiptDigest.Algorithm
            || actualReceiptDigest.SizeBytes != expectedReceiptDigest.SizeBytes
            || !actualReceiptDigest.Value.Span.SequenceEqual(expectedReceiptDigest.Value.Span))
        {
            throw new InvalidDataException(
                "The replacement helper receipt non-secret digest is not derived from its exact terminal identity.");
        }
        byte[] canonicalReceipt = terminalReceiptBytes ?? HelperPrivateProtocolV2.Encode(new()
        {
            Sequence = 3,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(
                Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = helper.Process.Receipt.Clone(),
        });
        HelperPrivateFrameV2 decodedReceipt = HelperPrivateProtocolV2.Decode(canonicalReceipt, 3);
        if (!decodedReceipt.Receipt.Equals(helper.Process.Receipt))
        {
            throw new InvalidDataException(
                "The replacement helper terminal frame does not contain the validated process receipt.");
        }
        string canonicalSha = Convert.ToHexStringLower(SHA256.HashData(canonicalReceipt));
        byte[]? failureEnvelopeBytes = helper.ValidatedNativeFailureEnvelopeBytes;
        NativeHelperFailureEnvelope? failureEnvelope = failureEnvelopeBytes is null
            ? null : NativeHelperFailureProtocol.DecodeCanonical(failureEnvelopeBytes);
        bool typedFailure = failureEnvelope is not null;
        if (!typedFailure && helper.Process.ExitCode != 0
            || typedFailure && (helper.Process.ExitCode != 72
                || helper.Process.Receipt.Outcome != HelperOutcomeV2.FailedKnown
                || failureEnvelope!.ContainmentProbeExecuted != true
                || failureEnvelope.ExcludedHandleAccessible != false))
        {
            throw new InvalidDataException(
                "The replacement terminal origin and helper exit facts disagree.");
        }
        if (failureEnvelope is not null)
        {
            ValidateFailureEnvelopeProcessLinkage(failureEnvelope, helper.Process);
        }
        string exactReceiptRelative = Path.Combine(attemptId, "helper-receipt.v2.pb");
        if (helper.Staging.AttemptId != attemptId
            || helper.Staging.RelativePath != exactReceiptRelative
            || canonicalReceipt.Length != helper.Staging.ByteLength || canonicalSha != helper.Staging.Sha256
            || helper.Staging.ResponseRelativePath is not null || helper.Staging.ResponseByteLength != 0
            || helper.Staging.ResponseSha256 is not null
            || !helper.Staging.StagedBeforeAdmission || !helper.Staging.CoordinatorOnlyAdmission)
        {
            throw new InvalidDataException("The replacement helper boundary receipt bytes are not exact.");
        }
        object boundary = new
        {
            schema_identity = BoundarySchema,
            attempt_id = attemptId,
            assignment_id = helper.Process.Receipt.AssignmentId,
            terminal_origin = typedFailure
                ? "validated-native-failure-envelope"
                : "helper-terminal-receipt",
            validated_failure_envelope = failureEnvelopeBytes is null ? null : new
            {
                sha256 = Convert.ToHexStringLower(SHA256.HashData(failureEnvelopeBytes)),
                base64 = Convert.ToBase64String(failureEnvelopeBytes),
                stage = failureEnvelope!.Stage,
                reason = failureEnvelope.Reason,
                network_facts_known = failureEnvelope.NetworkFactsKnown,
                external_effect_facts_known = failureEnvelope.ExternalEffectFactsKnown,
                dns_operation_count = failureEnvelope.DnsOperationCount,
                provider_operation_count = failureEnvelope.ProviderOperationCount,
                billable_operation_count = failureEnvelope.BillableOperationCount,
            },
            terminal_receipt = new
            {
                relative_path = helper.Staging.RelativePath.Replace('\\', '/'),
                sha256 = helper.Staging.Sha256,
                base64 = Convert.ToBase64String(canonicalReceipt),
                sequence = 3,
                outcome = helper.Process.Receipt.Outcome.ToString(),
            },
            process = new
            {
                process_id = helper.Process.ProcessId,
                exit_code = helper.Process.ExitCode,
                binary_sha256 = helper.Process.BinarySha256,
                staged_response_base64 = Convert.ToBase64String(helper.Process.StagedResponseBytes),
                inherited_private_handle_count = helper.Process.InheritedPrivateHandleCount,
                standard_protocol_handle_count = helper.Process.StandardProtocolHandleCount,
                listener_count = helper.Process.ListenerCount,
                network_operation_count = helper.Process.NetworkOperationCount,
                native_credential_operation_count = helper.Process.NativeCredentialOperationCount,
                process_tree_survivor_count = helper.Process.ProcessTreeSurvivorCount,
                process_tree_terminated = helper.Process.ProcessTreeTerminated,
                retry_attempted = helper.Process.RetryAttempted,
                native_call_trace_base64 = Convert.ToBase64String(helper.Process.NativeCallTraceBytes!),
                native_entry_cleanup_base64 = helper.Process.NativeEntryCleanupBytes is null
                    ? null : Convert.ToBase64String(helper.Process.NativeEntryCleanupBytes),
                native_canary_evidence_base64 = Convert.ToBase64String(helper.Process.NativeCanaryEvidenceBytes!),
                containment_probe_executed = helper.Process.ContainmentProbeExecuted,
                excluded_handle_accessible = helper.Process.ExcludedHandleAccessible,
                active_process_count_before_job_close = helper.Process.ActiveProcessCountBeforeJobClose,
                total_contained_process_count = helper.Process.TotalContainedProcessCount,
                namespace_reuse_blocked = helper.Process.NativeNamespaceReuseBlocked,
                namespace_reuse_block_reason = helper.Process.NativeNamespaceReuseBlockReason,
            },
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(boundary, Json);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-helper-boundary.v2.schema.json")),
            BoundarySchema);
        return bytes;
    }

    internal static CoordinatedHelperReceipt ReadValidatedReplacementBoundary(
        string repository,
        AuthoritativeStore store,
        string attemptId,
        HelperPrivateFrameV2 assignment,
        string predecessorFingerprint,
        string successorFingerprint,
        string expectedHelperSha256,
        DateTimeOffset now)
    {
        HelperAssignmentV2 expected = assignment.Assignment;
        byte[] boundaryBytes = store.ReadCredentialReplacementBoundary(attemptId);
        ActiveRepositoryJsonSchemaValidator.Validate(boundaryBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-helper-boundary.v2.schema.json")),
            BoundarySchema);
        using JsonDocument document = JsonDocument.Parse(boundaryBytes);
        JsonElement root = document.RootElement;
        JsonElement terminalNode = root.GetProperty("terminal_receipt");
        JsonElement processNode = root.GetProperty("process");
        string terminalOrigin = root.GetProperty("terminal_origin").GetString()!;
        JsonElement failureNode = root.GetProperty("validated_failure_envelope");
        byte[]? failureEnvelopeBytes = failureNode.ValueKind == JsonValueKind.Null
            ? null : Convert.FromBase64String(failureNode.GetProperty("base64").GetString()!);
        NativeHelperFailureEnvelope? failureEnvelope = failureEnvelopeBytes is null
            ? null : NativeHelperFailureProtocol.DecodeCanonical(failureEnvelopeBytes);
        if (failureEnvelopeBytes is null && terminalOrigin != "helper-terminal-receipt"
            || failureEnvelopeBytes is not null
                && (terminalOrigin != "validated-native-failure-envelope"
                    || Convert.ToHexStringLower(SHA256.HashData(failureEnvelopeBytes))
                        != failureNode.GetProperty("sha256").GetString()
                    || failureEnvelope!.Stage != failureNode.GetProperty("stage").GetString()
                    || failureEnvelope.Reason != failureNode.GetProperty("reason").GetString()
                    || failureEnvelope.NetworkFactsKnown
                        != failureNode.GetProperty("network_facts_known").GetBoolean()
                    || failureEnvelope.ExternalEffectFactsKnown
                        != failureNode.GetProperty("external_effect_facts_known").GetBoolean()
                    || failureEnvelope.DnsOperationCount
                        != failureNode.GetProperty("dns_operation_count").GetInt32()
                    || failureEnvelope.ProviderOperationCount
                        != failureNode.GetProperty("provider_operation_count").GetInt32()
                    || failureEnvelope.BillableOperationCount
                        != failureNode.GetProperty("billable_operation_count").GetInt32()))
        {
            throw new InvalidDataException("The replacement boundary failure-envelope provenance is stale.");
        }
        string receiptRelative = terminalNode.GetProperty("relative_path").GetString()!.Replace('/', Path.DirectorySeparatorChar);
        string exactReceiptRelative = Path.Combine(attemptId, "helper-receipt.v2.pb");
        byte[] receiptBytes = Convert.FromBase64String(terminalNode.GetProperty("base64").GetString()!);
        if (root.GetProperty("attempt_id").GetString() != attemptId
            || root.GetProperty("assignment_id").GetString() != expected.AssignmentId
            || receiptRelative != exactReceiptRelative
            || Convert.ToHexStringLower(SHA256.HashData(receiptBytes))
                != terminalNode.GetProperty("sha256").GetString())
        {
            throw new InvalidDataException("The staged replacement boundary identity or receipt hash is stale.");
        }
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(receiptBytes, 3);
        if (terminalNode.GetProperty("outcome").GetString() != terminal.Receipt.Outcome.ToString())
        {
            throw new InvalidDataException("The staged replacement boundary outcome is stale.");
        }
        _ = HelperProtocolV2Codec.Decode(
            terminal.ToByteArray(), DateTimeOffset.UtcNow,
            expectedAssignmentId: expected.AssignmentId,
            expectedCommandId: expected.CommandId,
            expectedProfileId: expected.AccessProfileId.Value,
            expectedGenerationId: expected.GenerationId.Value,
            expectedGenerationOrdinal: expected.GenerationOrdinal,
            expectedNonSecretReceipt: CredentialReceiptDigest(
                expected.AssignmentId, expected.CommandId, terminal.Receipt.Outcome),
            expectedPayloadCase: HelperPrivateFrameV2.PayloadOneofCase.Receipt,
            expectedSequence: 3,
            expectedAssignmentKind: HelperAssignmentKindV2.Replace);
        byte[] trace = Convert.FromBase64String(processNode.GetProperty("native_call_trace_base64").GetString()!);
        byte[]? entry = processNode.GetProperty("native_entry_cleanup_base64").ValueKind == JsonValueKind.Null
            ? null : Convert.FromBase64String(processNode.GetProperty("native_entry_cleanup_base64").GetString()!);
        byte[] canaries = Convert.FromBase64String(processNode.GetProperty("native_canary_evidence_base64").GetString()!);
        HelperProcessReceipt process = new(
            processNode.GetProperty("process_id").GetInt32(),
            processNode.GetProperty("exit_code").GetInt32(),
            processNode.GetProperty("binary_sha256").GetString()!,
            terminal.Receipt.Clone(),
            Convert.FromBase64String(processNode.GetProperty("staged_response_base64").GetString()!),
            processNode.GetProperty("inherited_private_handle_count").GetInt32(),
            processNode.GetProperty("standard_protocol_handle_count").GetInt32(),
            processNode.GetProperty("listener_count").GetInt32(),
            processNode.GetProperty("network_operation_count").GetInt32(),
            processNode.GetProperty("native_credential_operation_count").GetInt32(),
            processNode.GetProperty("process_tree_survivor_count").GetInt32(),
            processNode.GetProperty("process_tree_terminated").GetBoolean(),
            processNode.GetProperty("retry_attempted").GetBoolean(),
            trace, entry, canaries,
            processNode.GetProperty("containment_probe_executed").GetBoolean(),
            processNode.GetProperty("excluded_handle_accessible").GetBoolean(),
            processNode.GetProperty("active_process_count_before_job_close").GetInt32(),
            processNode.GetProperty("total_contained_process_count").GetInt32(),
            processNode.GetProperty("namespace_reuse_blocked").GetBoolean(),
            processNode.GetProperty("namespace_reuse_block_reason").ValueKind == JsonValueKind.Null
                ? null : processNode.GetProperty("namespace_reuse_block_reason").GetString());
        if (expectedHelperSha256.Length != 64
            || !expectedHelperSha256.All(char.IsAsciiHexDigit)
            || process.BinarySha256 != expectedHelperSha256)
        {
            throw new InvalidDataException("The staged replacement boundary helper binary is not the reviewed build.");
        }
        if (failureEnvelope is not null)
        {
            ValidateFailureEnvelopeProcessLinkage(failureEnvelope, process);
        }
        ValidateReplacementHelperBoundary(
            process, predecessorFingerprint, successorFingerprint,
            requireCompleted: process.Receipt.Outcome == HelperOutcomeV2.Completed);
        string receiptPath = Path.Combine(store.Paths.Staging, exactReceiptRelative);
        HelperStagingReceipt staging;
        if (File.Exists(receiptPath))
        {
            staging = store.AdmitExistingHelperReceipt(attemptId, receiptBytes, now);
        }
        else
        {
            staging = store.StageAndAdmitHelperReceipt(
                attemptId, receiptBytes, now);
        }
        return new(process, staging, failureEnvelopeBytes);
    }

    private static void ValidateFailureEnvelopeProcessLinkage(
        NativeHelperFailureEnvelope envelope,
        HelperProcessReceipt process)
    {
        byte[] traceBytes = Encoding.UTF8.GetBytes(envelope.NativeCallTraceJson
            ?? throw new InvalidDataException(
                "The typed replacement failure envelope omits its native trace."));
        byte[] canaryBytes = Encoding.UTF8.GetBytes(envelope.CanaryEvidenceJson
            ?? throw new InvalidDataException(
                "The typed replacement failure envelope omits its canary evidence."));
        byte[]? entryBytes = envelope.EntryCleanupJson is null
            ? null
            : Encoding.UTF8.GetBytes(envelope.EntryCleanupJson);
        JsonElement trace = ParseOptional(traceBytes)
            ?? throw new InvalidDataException(
                "The typed replacement failure envelope native trace is absent.");
        JsonElement[] calls = trace.EnumerateArray().ToArray();
        int credWriteW = calls.Count(call =>
            call.GetProperty("Operation").GetString() == "CredWriteW");
        int credReadW = calls.Count(call =>
            call.GetProperty("Operation").GetString() == "CredReadW");
        int credDeleteW = calls.Count(call =>
            call.GetProperty("Operation").GetString() == "CredDeleteW");
        int credFree = calls.Count(call =>
            call.GetProperty("Operation").GetString() == "CredFree");
        bool entryMatches = entryBytes is null
            ? process.NativeEntryCleanupBytes is null
            : process.NativeEntryCleanupBytes is not null
                && entryBytes.AsSpan().SequenceEqual(process.NativeEntryCleanupBytes);
        if (!envelope.CallCountsKnown
            || !envelope.NetworkFactsKnown
            || !envelope.ExternalEffectFactsKnown
            || envelope.ContainmentProbeExecuted != process.ContainmentProbeExecuted
            || envelope.ExcludedHandleAccessible != process.ExcludedHandleAccessible
            || !envelope.ContainmentDescendantStarted
            || envelope.ContainmentDescendantProcessId <= 0
            || envelope.ListenerCount != process.ListenerCount
            || envelope.NetworkOperationCount != process.NetworkOperationCount
            || envelope.DnsOperationCount != 0
            || envelope.ProviderOperationCount != 0
            || envelope.BillableOperationCount != 0
            || envelope.Total != process.NativeCredentialOperationCount
            || envelope.Total != calls.Length
            || envelope.CredWriteW != credWriteW
            || envelope.CredReadW != credReadW
            || envelope.CredDeleteW != credDeleteW
            || envelope.CredFree != credFree
            || !traceBytes.AsSpan().SequenceEqual(process.NativeCallTraceBytes)
            || !entryMatches
            || !canaryBytes.AsSpan().SequenceEqual(process.NativeCanaryEvidenceBytes)
            || envelope.ManualUiAttempted != (process.NativeEntryCleanupBytes is not null)
            || envelope.NamespaceReuseBlocked != process.NativeNamespaceReuseBlocked
            || envelope.NamespaceReuseBlockReason != process.NativeNamespaceReuseBlockReason)
        {
            throw new InvalidDataException(
                "The typed replacement failure envelope and retained process facts disagree.");
        }
    }

    private static void ValidateCollisionTrace(JsonElement[] calls, string successor, int count)
    {
        string[] operations = ["CredReadW", "CredFree"];
        string[] results = ["success", "released"];
        if (count != 2) { throw new InvalidDataException("The replacement collision count is not exact."); }
        for (int index = 0; index < 2; index++)
        {
            string[] names = ["Sequence", "Operation", "TargetFingerprintSha256", "Scenario", "Result",
                "AllocationId", "PairedAllocationId"];
            if (!calls[index].EnumerateObject().Select(property => property.Name)
                    .SequenceEqual(names, StringComparer.Ordinal)
                || calls[index].GetProperty("Sequence").GetInt32() != index + 1
                || calls[index].GetProperty("Operation").GetString() != operations[index]
                || calls[index].GetProperty("TargetFingerprintSha256").GetString() != successor
                || calls[index].GetProperty("Scenario").GetString()
                    != "m1-slice6-successor-credential-replacement"
                || calls[index].GetProperty("Result").GetString() != results[index]
                || NullableInt64(calls[index], "AllocationId") != (index == 0 ? 1 : null)
                || NullableInt64(calls[index], "PairedAllocationId") != (index == 1 ? 1 : null))
            {
                throw new InvalidDataException("The replacement collision trace is not exact.");
            }
        }
    }

    private static void ValidateStoppedBoundary(HelperProcessReceipt process, JsonElement entry, JsonElement canaries)
    {
        if (process.Receipt.Outcome is not (HelperOutcomeV2.Cancelled or HelperOutcomeV2.FailedKnown
                or HelperOutcomeV2.Unavailable)
            || process.NetworkOperationCount != 0 || process.ListenerCount != 0 || process.RetryAttempted
            || process.StagedResponseBytes.Length != 0 || !process.ContainmentProbeExecuted
            || !process.ProcessTreeTerminated || process.ProcessTreeSurvivorCount != 0
            || process.TotalContainedProcessCount < 2 || process.ActiveProcessCountBeforeJobClose < 1
            || process.ExcludedHandleAccessible)
        {
            throw new InvalidDataException("The stopped replacement boundary is not exact or safely contained.");
        }
        ValidateProcessCanaries(process, canaries);
        string terminal = entry.GetProperty("TerminalState").GetString() ?? "";
        if (process.Receipt.Outcome == HelperOutcomeV2.Cancelled && terminal != "cancelled"
            || process.Receipt.Outcome != HelperOutcomeV2.Cancelled
                && terminal is not ("submitted" or "failed" or "timed-out"))
        {
            throw new InvalidDataException("The stopped helper outcome and entry terminal state disagree.");
        }
        CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(
            Encoding.UTF8.GetString(process.NativeEntryCleanupBytes!), terminal);
        ValidateEntryProperties(entry);
    }

    private static void ValidateSuccessfulBoundary(HelperProcessReceipt process, JsonElement entry, JsonElement canaries)
    {
        if (process.Receipt.Outcome != HelperOutcomeV2.Completed
            || process.NetworkOperationCount != 0 || process.ListenerCount != 0
            || process.RetryAttempted || process.StagedResponseBytes.Length != 0
            || !process.ContainmentProbeExecuted || !process.ProcessTreeTerminated
            || process.ProcessTreeSurvivorCount != 0 || process.TotalContainedProcessCount < 2
            || process.ActiveProcessCountBeforeJobClose < 1
            || process.ExcludedHandleAccessible || process.NativeNamespaceReuseBlocked)
        {
            throw new InvalidDataException("The successful replacement helper containment or zero-network boundary failed.");
        }
        ValidateProcessCanaries(process, canaries);
        string entryJson = Encoding.UTF8.GetString(process.NativeEntryCleanupBytes!);
        CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(entryJson, "submitted");
        ValidateEntryProperties(entry);
        if (entry.GetProperty("Surface").GetString() != "wp9-distinct-helper-owned-native-masked-paste-surface"
            || entry.GetProperty("TerminalState").GetString() != "submitted"
            || !entry.GetProperty("Masked").GetBoolean() || !entry.GetProperty("PastePermitted").GetBoolean()
            || !entry.GetProperty("HelperOwned").GetBoolean() || entry.GetProperty("RendererReceivedSecret").GetBoolean()
            || !entry.GetProperty("InitiallyBlank").GetBoolean() || !entry.GetProperty("Ready").GetBoolean()
            || !entry.GetProperty("HelperProcessOwned").GetBoolean() || !entry.GetProperty("SameSession").GetBoolean()
            || !entry.GetProperty("InputDesktopAvailable").GetBoolean() || !entry.GetProperty("NotCloaked").GetBoolean()
            || !entry.GetProperty("OnMonitor").GetBoolean() || !entry.GetProperty("Enabled").GetBoolean()
            || !entry.GetProperty("Focused").GetBoolean() || !entry.GetProperty("Foreground").GetBoolean()
            || !entry.GetProperty("Active").GetBoolean() || entry.GetProperty("ReadinessChecks").GetInt32() < 1
            || entry.GetProperty("MessagePumpIterations").GetInt32() < 1
            || !entry.GetProperty("WindowDestroyed").GetBoolean() || !entry.GetProperty("BufferCleared").GetBoolean()
            || !entry.GetProperty("NativeEditEmptyVerified").GetBoolean()
            || !entry.GetProperty("ThreadJoined").GetBoolean())
        {
            throw new InvalidDataException("The successful replacement entry cleanup evidence is incomplete.");
        }
    }

    private static void ValidateCanaries(
        JsonElement canaries,
        string responseSurface = "private protocol response",
        bool allowEmptyResponse = false)
    {
        string[] names = ["SecretMatches", "RawTargetMatches", "RawTargetEncodings", "ScannedSurfaces"];
        if (!canaries.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal)
            || canaries.GetProperty("SecretMatches").GetInt32() != 0
            || canaries.GetProperty("RawTargetMatches").GetInt32() != 0
            || !canaries.GetProperty("RawTargetEncodings").EnumerateArray().Select(item => item.GetString())
                .SequenceEqual(["utf-8", "utf-16le"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("The replacement canary root is malformed or nonzero.");
        }
        JsonElement[] surfaces = canaries.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        string[] expectedNames = ["private protocol request", responseSurface, "native call trace",
            "process command line", "process environment names"];
        string[] expectedKinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes",
            "captured-text", "captured-text"];
        if (surfaces.Length != expectedNames.Length) { throw new InvalidDataException("The replacement canary inventory is incomplete."); }
        for (int index = 0; index < surfaces.Length; index++)
        {
            string[] surfaceNames = ["Name", "Kind", "ByteCount", "SecretMatches", "RawTargetMatches"];
            if (!surfaces[index].EnumerateObject().Select(property => property.Name)
                    .SequenceEqual(surfaceNames, StringComparer.Ordinal)
                || surfaces[index].GetProperty("Name").GetString() != expectedNames[index]
                || surfaces[index].GetProperty("Kind").GetString() != expectedKinds[index]
                || surfaces[index].GetProperty("ByteCount").GetInt64() < 0
                || surfaces[index].GetProperty("ByteCount").GetInt64() == 0
                    && !(allowEmptyResponse && index == 1)
                || surfaces[index].GetProperty("SecretMatches").GetInt32() != 0
                || surfaces[index].GetProperty("RawTargetMatches").GetInt32() != 0)
            {
                throw new InvalidDataException("A replacement canary surface is vacuous or nonzero.");
            }
        }
    }

    private static void ValidateProcessCanaries(
        HelperProcessReceipt process,
        JsonElement canaries)
    {
        if (process.ExitCode == 72)
        {
            JsonElement[] surfaces = canaries.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
            string responseSurface = surfaces.Length > 1
                ? surfaces[1].GetProperty("Name").GetString() ?? ""
                : "";
            if (responseSurface is not ("private protocol partial response" or "private protocol response"))
            {
                throw new InvalidDataException(
                    "The typed replacement failure response canary surface is invalid.");
            }
            ValidateCanaries(canaries, responseSurface, allowEmptyResponse: true);
            return;
        }
        ValidateCanaries(canaries);
    }

    private static void ValidateEntryProperties(JsonElement entry)
    {
        string[] names = ["Surface", "Masked", "PastePermitted", "HelperOwned", "RendererReceivedSecret",
            "InitiallyBlank", "Ready", "HelperProcessOwned", "SameSession", "InputDesktopAvailable",
            "NotCloaked", "OnMonitor", "Enabled", "Focused", "Foreground", "Active", "ReadinessChecks",
            "PreReadinessIgnoredActions", "MessagePumpIterations", "ActionSnapshot", "TerminalState",
            "WindowDestroyed", "BufferCleared", "NativeEditEmptyVerified", "ThreadJoined"];
        if (!entry.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The replacement entry evidence contains an unreviewed property.");
        }
    }

    private static long? NullableInt64(JsonElement node, string name) =>
        node.GetProperty(name).ValueKind == JsonValueKind.Null ? null : node.GetProperty(name).GetInt64();

    private static HelperPrivateFrameV2 Bootstrap(
        string profileId, string generationId, DateTimeOffset now, DateTimeOffset expires, string authorityId) => new()
        {
            Sequence = 1,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Bootstrap = new()
            {
                CoordinatorFencingEpoch = 1,
                ExpiresAt = Instant(expires),
                OneUseNonceFingerprintSha256 = ByteString.CopyFrom(RandomNumberGenerator.GetBytes(32)),
                CommandId = authorityId + "/command",
                Credential = new() { AccessProfileId = new() { Value = profileId }, GenerationId = new() { Value = generationId } },
            },
        };

    internal static void ValidateTypedFailureRecoveryOwner(
        string repository,
        JsonElement owner,
        string successorGeneration)
    {
        JsonElement priorOwner = owner.GetProperty("prior_owner_authority");
        JsonElement failure = owner.GetProperty("retained_failure");
        JsonElement correction = owner.GetProperty("correction");
        JsonElement recovery = owner.GetProperty("recovery");
        string priorOwnerPath = Path.GetFullPath(Path.Combine(repository,
            priorOwner.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string exactPriorOwnerPath = Path.GetFullPath(Path.Combine(repository, "docs", "plans", "milestones",
            "m1", "slices", "s6", "m1-slice6-development-campaign-amendment.v5.json"));
        byte[] priorOwnerBytes = ExactBytes(priorOwnerPath, priorOwner.GetProperty("sha256").GetString()!);
        ActiveRepositoryJsonSchemaValidator.Validate(priorOwnerBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-development-campaign-amendment.v5.schema.json")),
            CleanupRecoveryAmendmentSchema);
        string failurePath = Path.GetFullPath(Path.Combine(repository,
            failure.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string exactFailurePath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-credential-replacement-cleanup-recovery", "ca6a6e5f-966f-467f-be55-c2320896a092",
            "replacement-evidence.v2.json"));
        byte[] failureBytes = ExactBytes(failurePath, failure.GetProperty("sha256").GetString()!);
        ActiveRepositoryJsonSchemaValidator.Validate(failureBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-failure-evidence.v1.schema.json")),
            FailureEvidenceSchema);
        using JsonDocument priorOwnerDocument = JsonDocument.Parse(priorOwnerBytes);
        using JsonDocument failureDocument = JsonDocument.Parse(failureBytes);
        JsonElement retainedAuthority = failureDocument.RootElement.GetProperty("authority");
        JsonElement retainedReview = failureDocument.RootElement.GetProperty("independent_review");
        JsonElement retainedState = failureDocument.RootElement.GetProperty("product_state");
        const string retainedOperation =
            "m1s6-credential-replacement-5bba03b2f399640c13c0432cab823a05";
        string productRoot = Path.Combine(repository, "artifacts", "m1-slice6", "successor-product-state");
        CredentialReplacementRecoveryAudit audit =
            AuthoritativeStore.ReadCredentialReplacementRecoveryAuditReadOnly(
                productRoot,
                "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e",
                successorGeneration,
                retainedOperation);
        CredentialProfileProjection current = audit.Projection;
        string retainedStaging = Path.Combine(productRoot, "staging", retainedOperation);
        if (priorOwnerPath != exactPriorOwnerPath
            || failurePath != exactFailurePath
            || priorOwner.GetProperty("id").GetString()
                != "infinium.m1-s6.credential-replacement-cleanup-recovery/20260821-pre-entry-assignment-prefix"
            || priorOwner.GetProperty("sha256").GetString()
                != "1970893a8a7f31ce15dec7bfa71b17a83512098ab3b6f0bda5f7c0424899ddd1"
            || priorOwnerDocument.RootElement.GetProperty("amendment_id").GetString()
                != priorOwner.GetProperty("id").GetString()
            || failure.GetProperty("id").GetString()
                != "infinium.m1-s6.successor-credential-replacement-evidence/725cedc6-c2ee-4707-a6a0-066caf9c47f9"
            || failure.GetProperty("sha256").GetString()
                != "4ae72405d2611e53ec94e1e0015704c1444b2915b7b730bacf7f6568eda02d23"
            || failureDocument.RootElement.GetProperty("evidence_id").GetString()
                != failure.GetProperty("id").GetString()
            || failureDocument.RootElement.GetProperty("operation_id").GetString() != retainedOperation
            || failureDocument.RootElement.GetProperty("typed_failure").GetString()
                != "CredentialNativeHelperEvidenceAmbiguityException"
            || failureDocument.RootElement.GetProperty("observed_effect_facts").GetString()
                != "unknown-conservatively-blocked"
            || retainedAuthority.GetProperty("id").GetString()
                != "infinium.m1-s6.successor-credential-replacement-cleanup-recovery/ca6a6e5f-966f-467f-be55-c2320896a092"
            || retainedAuthority.GetProperty("sha256").GetString()
                != "5bba03b2f399640c13c0432cab823a052d3dc000a05524c9826fb2ce9f3d242e"
            || retainedReview.GetProperty("id").GetString()
                != "infinium.m1-s6.successor-credential-replacement-cleanup-recovery-review/a6589a9f-c1a3-45be-84f4-628dee94a16b"
            || retainedReview.GetProperty("sha256").GetString()
                != "135694b199e72deef3c5feb9e4c75c45f8a6cedb3fb1dc66de1eb85b3c2d6cad"
            || retainedState.GetProperty("checkpoint_sha256").GetString()
                != "8baa3e0f2be9fc0b30323c394e1e14cd2505395514d4fa2dfd8a9fda3b50ea79"
            || retainedState.GetProperty("profile_id").GetString()
                != "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e"
            || retainedState.GetProperty("generation_id").GetString()
                != "g-ff6d82e7a7d244f6b8a9d0164991be37"
            || retainedState.GetProperty("generation_ordinal").GetInt64() != 1
            || retainedState.GetProperty("lifecycle_state").GetString() != "delete-pending"
            || correction.GetProperty("implementation_commit").GetString()
                != "65f68f13ede2745e63026d31073740c5e9327c90"
            || correction.GetProperty("retained_operation_id").GetString() != retainedOperation
            || correction.GetProperty("consumed_helper_launches").GetInt64() != 1
            || correction.GetProperty("validated_boundary").GetString() != "absent"
            || correction.GetProperty("terminal_receipt").GetString() != "absent"
            || recovery.GetProperty("successor_generation_id").GetString() != successorGeneration
            || recovery.GetProperty("assignment_kind").GetString() != "Replace"
            || audit.PriorHelperLaunchAdmissionCount != 1
            || Directory.Exists(retainedStaging)
            || current.GenerationId != "g-ff6d82e7a7d244f6b8a9d0164991be37"
            || current.GenerationOrdinal != 1
            || current.LifecycleState != "delete-pending"
            || current.VerificationState != "unavailable"
            || current.CleanupDisposition != "failed"
            || audit.SuccessorGenerationOrdinal != 2)
        {
            throw new InvalidDataException(
                "The exact retained typed-failure cleanup-recovery lineage is stale.");
        }
    }

    internal static void ValidateForegroundRecoveryOwner(
        string repository,
        JsonElement owner,
        string successorGeneration)
    {
        JsonElement priorOwner = owner.GetProperty("prior_owner_authority");
        JsonElement retainedEvidence = owner.GetProperty("retained_stopped_evidence");
        JsonElement retainedBoundary = owner.GetProperty("retained_boundary");
        JsonElement retainedReceipt = owner.GetProperty("retained_receipt");
        JsonElement correction = owner.GetProperty("correction");
        JsonElement review = correction.GetProperty("independent_review");
        JsonElement recovery = owner.GetProperty("recovery");
        const string retainedOperation =
            "m1s6-credential-replacement-d7ae77b7d68c577196bbe5ac26325638";
        const string retainedAuthorityId =
            "infinium.m1-s6.successor-credential-replacement-typed-failure-recovery/93a82db5-f427-4616-a24d-2be75ac06d5f";
        const string profileId = "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e";
        const string predecessorGeneration = "g-ff6d82e7a7d244f6b8a9d0164991be37";

        string priorOwnerPath = RepositoryBindingPath(repository, priorOwner);
        string evidencePath = RepositoryBindingPath(repository, retainedEvidence);
        string boundaryPath = RepositoryBindingPath(repository, retainedBoundary);
        string receiptPath = RepositoryBindingPath(repository, retainedReceipt);
        string reviewPath = RepositoryBindingPath(repository, review);
        string exactPriorOwnerPath = Path.GetFullPath(Path.Combine(repository, "docs", "plans", "milestones",
            "m1", "slices", "s6", "m1-slice6-development-campaign-amendment.v6.json"));
        string exactEvidencePath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-credential-replacement-typed-failure-recovery",
            "93a82db5-f427-4616-a24d-2be75ac06d5f", "replacement-evidence.v2.json"));
        string exactStagingRoot = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-product-state", "staging", retainedOperation));
        string exactBoundaryPath = Path.Combine(
            exactStagingRoot, AuthoritativeStore.CredentialReplacementBoundaryFileName);
        string exactReceiptPath = Path.Combine(exactStagingRoot, "helper-receipt.v2.pb");
        string exactReviewPath = Path.GetFullPath(Path.Combine(repository, "docs", "plans", "milestones",
            "m1", "slices", "s6",
            "m1-slice6-successor-credential-replacement-foreground-readiness-offline-correction-review.v3.json"));

        byte[] priorOwnerBytes = ExactBytes(priorOwnerPath, priorOwner.GetProperty("sha256").GetString()!);
        byte[] evidenceBytes = ExactBytes(evidencePath, retainedEvidence.GetProperty("sha256").GetString()!);
        byte[] boundaryBytes = ExactBytes(boundaryPath, retainedBoundary.GetProperty("sha256").GetString()!);
        byte[] receiptBytes = ExactBytes(receiptPath, retainedReceipt.GetProperty("sha256").GetString()!);
        byte[] reviewBytes = ExactBytes(reviewPath, review.GetProperty("sha256").GetString()!);
        ActiveRepositoryJsonSchemaValidator.Validate(priorOwnerBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-development-campaign-amendment.v6.schema.json")),
            TypedFailureRecoveryAmendmentSchema);
        ActiveRepositoryJsonSchemaValidator.Validate(evidenceBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-evidence.v2.schema.json")),
            EvidenceSchemaV2);
        ActiveRepositoryJsonSchemaValidator.Validate(boundaryBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-helper-boundary.v2.schema.json")),
            BoundarySchema);
        ActiveRepositoryJsonSchemaValidator.Validate(reviewBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-independent-review.v3.schema.json")),
            "infinium.repository.m1-slice6-successor-independent-review/3.0.0");

        using JsonDocument priorOwnerDocument = JsonDocument.Parse(priorOwnerBytes);
        using JsonDocument evidenceDocument = JsonDocument.Parse(evidenceBytes);
        using JsonDocument boundaryDocument = JsonDocument.Parse(boundaryBytes);
        using JsonDocument reviewDocument = JsonDocument.Parse(reviewBytes);
        JsonElement evidence = evidenceDocument.RootElement;
        JsonElement effect = evidence.GetProperty("effect");
        JsonElement entry = effect.GetProperty("entry_evidence");
        JsonElement canaries = effect.GetProperty("canaries");
        JsonElement boundary = boundaryDocument.RootElement;
        JsonElement terminal = boundary.GetProperty("terminal_receipt");
        JsonElement acceptedReview = reviewDocument.RootElement;
        byte[] boundaryReceipt = Convert.FromBase64String(terminal.GetProperty("base64").GetString()!);
        HelperPrivateFrameV2 receipt = HelperPrivateProtocolV2.Decode(receiptBytes, 3);
        _ = HelperProtocolV2Codec.Decode(
            receipt.ToByteArray(), DateTimeOffset.UtcNow,
            expectedAssignmentId: retainedAuthorityId + "/replace",
            expectedCommandId: retainedAuthorityId + "/command",
            expectedProfileId: profileId,
            expectedGenerationId: successorGeneration,
            expectedGenerationOrdinal: 2,
            expectedNonSecretReceipt: CredentialReceiptDigest(
                retainedAuthorityId + "/replace", retainedAuthorityId + "/command",
                HelperOutcomeV2.FailedKnown),
            expectedPayloadCase: HelperPrivateFrameV2.PayloadOneofCase.Receipt,
            expectedSequence: 3,
            expectedAssignmentKind: HelperAssignmentKindV2.Replace);

        string productRoot = Path.Combine(repository, "artifacts", "m1-slice6", "successor-product-state");
        CredentialReplacementRecoveryAudit audit =
            AuthoritativeStore.ReadCredentialReplacementRecoveryAuditReadOnly(
                productRoot, profileId, successorGeneration, retainedOperation);
        CredentialProfileProjection current = audit.Projection;
        if (priorOwnerPath != exactPriorOwnerPath || evidencePath != exactEvidencePath
            || boundaryPath != exactBoundaryPath || receiptPath != exactReceiptPath
            || reviewPath != exactReviewPath
            || priorOwner.GetProperty("id").GetString()
                != "infinium.m1-s6.credential-replacement-cleanup-recovery/20260821-typed-native-failure-settlement"
            || priorOwner.GetProperty("sha256").GetString()
                != "948f9ea190c76ae6e9b0935b604109b35ab986c4b2fdf199199e973b8b3dc468"
            || priorOwnerDocument.RootElement.GetProperty("amendment_id").GetString()
                != priorOwner.GetProperty("id").GetString()
            || retainedEvidence.GetProperty("id").GetString()
                != "infinium.m1-s6.successor-credential-replacement-evidence/97c7c188-edf5-4223-8fe4-6532046c5fbe"
            || retainedEvidence.GetProperty("sha256").GetString()
                != "a63d681239f907151168aca6a031bc39c0d3b3d13d133650dae23f4f4e38a1b7"
            || retainedBoundary.GetProperty("sha256").GetString()
                != "9e5e370a2afa4cb1bb39c5aa08bc2131388ef8a49f45158531802886f421b3d7"
            || retainedReceipt.GetProperty("sha256").GetString()
                != "37fb81c213658d173fa67cfec43d7e05773c7e346becd12034c2934333311b23"
            || review.GetProperty("sha256").GetString()
                != "e33153081f1d2efb69b2d54cc9eeed2aa08b851627dd767e54a94a6691bcd53c"
            || evidence.GetProperty("evidence_id").GetString() != retainedEvidence.GetProperty("id").GetString()
            || evidence.GetProperty("status").GetString() != "stopped-non-dispatchable-recovery-required"
            || evidence.GetProperty("authority").GetProperty("id").GetString() != retainedAuthorityId
            || evidence.GetProperty("authority").GetProperty("sha256").GetString()
                != "d7ae77b7d68c577196bbe5ac263256382d62e1e2f032e6a4cc3aa08927c0678d"
            || evidence.GetProperty("product_state").GetProperty("checkpoint_after_sha256").GetString()
                != "34d4fb9e56d38b534ba2ec3e5e874106c66a2038d1895576cbdfd5c964fb98de"
            || effect.GetProperty("helper_launch_count").GetInt64() != 1
            || effect.GetProperty("native_credential_operation_count").GetInt64() != 0
            || effect.GetProperty("network_operation_count").GetInt64() != 0
            || effect.GetProperty("provider_operation_count").GetInt64() != 0
            || effect.GetProperty("billable_operation_count").GetInt64() != 0
            || effect.GetProperty("retry_attempted").GetBoolean()
            || effect.GetProperty("helper_outcome").GetString() != "FailedKnown"
            || effect.GetProperty("terminal_origin").GetString() != "validated-native-failure-envelope"
            || effect.GetProperty("failure_stage").GetString() != "engine-execution"
            || effect.GetProperty("failure_reason").GetString() != "controlled-failure"
            || entry.GetProperty("Ready").GetBoolean() || entry.GetProperty("Foreground").GetBoolean()
            || !entry.GetProperty("Focused").GetBoolean() || !entry.GetProperty("Active").GetBoolean()
            || entry.GetProperty("ActionSnapshot").ValueKind != JsonValueKind.Null
            || canaries.GetProperty("SecretMatches").GetInt64() != 0
            || canaries.GetProperty("RawTargetMatches").GetInt64() != 0
            || boundary.GetProperty("attempt_id").GetString() != retainedOperation
            || boundary.GetProperty("assignment_id").GetString() != retainedAuthorityId + "/replace"
            || boundary.GetProperty("terminal_origin").GetString() != "validated-native-failure-envelope"
            || terminal.GetProperty("sha256").GetString() != retainedReceipt.GetProperty("sha256").GetString()
            || !CryptographicOperations.FixedTimeEquals(boundaryReceipt, receiptBytes)
            || receipt.Receipt.Outcome != HelperOutcomeV2.FailedKnown
            || acceptedReview.GetProperty("review_id").GetString() != review.GetProperty("id").GetString()
            || acceptedReview.GetProperty("verdict").GetString() != "accept"
            || acceptedReview.GetProperty("reviewer_id").GetString() != "/root/successor-design-review"
            || acceptedReview.GetProperty("subject").GetProperty("id").GetString()
                != retainedEvidence.GetProperty("id").GetString()
            || acceptedReview.GetProperty("subject").GetProperty("sha256").GetString()
                != retainedEvidence.GetProperty("sha256").GetString()
            || acceptedReview.GetProperty("correction").GetProperty("candidate_commit").GetString()
                != "5d7f3d889b74254b2ca58cca0bc08a2c1418b0e2"
            || correction.GetProperty("implementation_commit").GetString()
                != "5d7f3d889b74254b2ca58cca0bc08a2c1418b0e2"
            || correction.GetProperty("retained_operation_id").GetString() != retainedOperation
            || correction.GetProperty("consumed_helper_launches").GetInt64() != 1
            || correction.GetProperty("native_credential_operations").GetInt64() != 0
            || recovery.GetProperty("successor_generation_id").GetString() != successorGeneration
            || recovery.GetProperty("assignment_kind").GetString() != "Replace"
            || audit.PriorHelperLaunchAdmissionCount != 1
            || current.GenerationId != predecessorGeneration || current.GenerationOrdinal != 1
            || current.LifecycleState != "delete-pending" || current.VerificationState != "unavailable"
            || current.CleanupDisposition != "failed" || audit.SuccessorGenerationOrdinal != 2)
        {
            throw new InvalidDataException(
                "The exact retained foreground-readiness cleanup-recovery lineage is stale.");
        }
    }

    private static string RepositoryBindingPath(string repository, JsonElement binding) =>
        Path.GetFullPath(Path.Combine(repository,
            binding.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));

    private static void ValidateDeletePendingRecoveryOwner(
        string repository,
        JsonElement owner,
        string successorGeneration)
    {
        JsonElement priorOwner = owner.GetProperty("prior_owner_authority");
        JsonElement failure = owner.GetProperty("retained_failure");
        JsonElement receipt = owner.GetProperty("retained_receipt");
        JsonElement recovery = owner.GetProperty("recovery");
        string priorOwnerPath = Path.GetFullPath(Path.Combine(repository,
            priorOwner.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string exactPriorOwnerPath = Path.GetFullPath(Path.Combine(repository, "docs", "plans", "milestones",
            "m1", "slices", "s6", "m1-slice6-development-campaign-amendment.v4.json"));
        byte[] priorOwnerBytes = ExactBytes(priorOwnerPath, priorOwner.GetProperty("sha256").GetString()!);
        ActiveRepositoryJsonSchemaValidator.Validate(priorOwnerBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-development-campaign-amendment.v4.schema.json")),
            RecoveryAmendmentSchema);
        string failurePath = Path.GetFullPath(Path.Combine(repository,
            failure.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string exactFailurePath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-credential-replacement-recovery", "a6755587-1f60-4854-91b9-771e37a36ac9",
            "replacement-evidence.v1.json"));
        byte[] failureBytes = ExactBytes(failurePath, failure.GetProperty("sha256").GetString()!);
        ActiveRepositoryJsonSchemaValidator.Validate(failureBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-failure-evidence.v1.schema.json")),
            FailureEvidenceSchema);
        string receiptPath = Path.GetFullPath(Path.Combine(repository,
            receipt.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string exactReceiptPath = Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6",
            "successor-product-state", "staging",
            "m1s6-credential-replacement-56e83ae29d7792337aa0dbc797bb294b", "helper-receipt.v2.pb"));
        byte[] receiptBytes = ExactBytes(receiptPath, receipt.GetProperty("sha256").GetString()!);
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(receiptBytes, 3);
        using JsonDocument failureDocument = JsonDocument.Parse(failureBytes);
        JsonElement retainedAuthority = failureDocument.RootElement.GetProperty("authority");
        JsonElement retainedReview = failureDocument.RootElement.GetProperty("independent_review");
        JsonElement retainedState = failureDocument.RootElement.GetProperty("product_state");
        const string retainedAuthorityId =
            "infinium.m1-s6.successor-credential-replacement-recovery/a6755587-1f60-4854-91b9-771e37a36ac9";
        _ = HelperProtocolV2Codec.Decode(
            terminal.ToByteArray(), DateTimeOffset.UtcNow,
            expectedAssignmentId: retainedAuthorityId + "/replace",
            expectedCommandId: retainedAuthorityId + "/command",
            expectedProfileId: "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e",
            expectedGenerationId: successorGeneration,
            expectedGenerationOrdinal: 2,
            expectedNonSecretReceipt: CredentialReceiptDigest(
                retainedAuthorityId + "/replace", retainedAuthorityId + "/command",
                HelperOutcomeV2.FailedKnown),
            expectedPayloadCase: HelperPrivateFrameV2.PayloadOneofCase.Receipt,
            expectedSequence: 3,
            expectedAssignmentKind: HelperAssignmentKindV2.Replace);
        if (priorOwnerPath != exactPriorOwnerPath || failurePath != exactFailurePath || receiptPath != exactReceiptPath
            || priorOwner.GetProperty("id").GetString()
                != "infinium.m1-s6.credential-replacement-recovery/20260821-pre-native-launcher-factory"
            || priorOwner.GetProperty("sha256").GetString()
                != "b84a5d158cce4ebb61fa1ddc7ce1dc899fbc1a5cae090fa1f26285d4786cf078"
            || recovery.GetProperty("successor_generation_id").GetString() != successorGeneration
            || recovery.GetProperty("assignment_kind").GetString() != "Replace"
            || failureDocument.RootElement.GetProperty("operation_id").GetString()
                != "m1s6-credential-replacement-56e83ae29d7792337aa0dbc797bb294b"
            || failureDocument.RootElement.GetProperty("evidence_id").GetString()
                != failure.GetProperty("id").GetString()
            || retainedAuthority.GetProperty("id").GetString() != retainedAuthorityId
            || retainedAuthority.GetProperty("sha256").GetString()
                != "56e83ae29d7792337aa0dbc797bb294b0f2d729601465a5f0f4200452e6576b2"
            || retainedReview.GetProperty("id").GetString()
                != "infinium.m1-s6.successor-credential-replacement-review/4bb8c9ef-36a8-42fc-ac07-4f105fc9a76c"
            || retainedReview.GetProperty("sha256").GetString()
                != "c6b884a774fcd04fe60175c6b92227ac08f875f9a757e56ea418596646a1a0fd"
            || retainedState.GetProperty("checkpoint_sha256").GetString()
                != "0d0dae5feb20c28980c2f30e253e5204350a5547f783189c6bdd6432c9792d37"
            || retainedState.GetProperty("profile_id").GetString()
                != "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e"
            || retainedState.GetProperty("generation_id").GetString()
                != "g-ff6d82e7a7d244f6b8a9d0164991be37"
            || retainedState.GetProperty("generation_ordinal").GetInt64() != 1
            || retainedState.GetProperty("lifecycle_state").GetString() != "delete-pending"
            || failureDocument.RootElement.GetProperty("typed_failure").GetString() != "InvalidDataException"
            || terminal.Receipt.Outcome != HelperOutcomeV2.FailedKnown
            || terminal.Receipt.TransportMayHaveStarted || terminal.Receipt.OutcomeHasResponse
            || terminal.Receipt.RawResponse is not null
            || terminal.Receipt.UsageReceiptState != UsageReceiptStateV2.NotDispatched
            || terminal.Receipt.AssignmentId != retainedAuthorityId + "/replace"
            || receipt.GetProperty("sequence").GetInt64() != 3
            || receipt.GetProperty("outcome").GetString() != "FailedKnown"
            || receipt.GetProperty("assignment_id").GetString() != terminal.Receipt.AssignmentId)
        {
            throw new InvalidDataException("The exact retained pre-entry cleanup-recovery failure is stale.");
        }
    }

    private static HelperPrivateFrameV2 Assignment(
        string profileId,
        string generationId,
        string authorityId,
        HelperAssignmentKindV2 kind,
        ulong generationOrdinal = 2) => new()
        {
            Sequence = 2,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Assignment = new()
            {
                AssignmentId = authorityId + (kind == HelperAssignmentKindV2.Recover ? "/recover" : "/replace"),
                CommandId = authorityId + "/command",
                AssignmentKind = kind,
                AccessProfileId = new() { Value = profileId },
                GenerationId = new() { Value = generationId },
                GenerationOrdinal = generationOrdinal,
                Credential = new() { AccessProfileId = new() { Value = profileId }, GenerationId = new() { Value = generationId } },
            },
        };

    private static Instant Instant(DateTimeOffset value) => new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };

    private static ContentDigest CredentialReceiptDigest(
        string assignmentId,
        string commandId,
        HelperOutcomeV2 outcome)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{assignmentId}/{commandId}/{outcome}");
        return new()
        {
            Algorithm = DigestAlgorithm.Sha256,
            Value = ByteString.CopyFrom(SHA256.HashData(bytes)),
            SizeBytes = checked((ulong)bytes.Length),
        };
    }

    private static string CanonicalZ(DateTimeOffset value) => value.ToUniversalTime().ToString(
        "yyyy-MM-ddTHH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static byte[] ExactBytes(string path, string expectedSha)
    {
        byte[] bytes = File.ReadAllBytes(path);
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return actual == expectedSha ? bytes : throw new InvalidDataException("An exact replacement artifact hash is stale.");
    }

    private static DateTimeOffset UtcZ(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, "yyyy-MM-ddTHH:mm:ss.fffffff'Z'",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException("A replacement timestamp is not canonical Z UTC.");
        }
        return parsed;
    }

    private static string AssemblyCommit() => typeof(M1Slice6SuccessorCredentialReplacementRunner).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException("The coordinator informational version is unavailable.");

    private static string ExecutableProductCommit(string path)
    {
        string productVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).ProductVersion
            ?? throw new InvalidDataException("A development credential executable has no product version.");
        string commit = productVersion.Split('+')[^1];
        if (commit.Length != 40 || !commit.All(char.IsAsciiHexDigit))
        {
            throw new InvalidDataException("A development credential executable is not bound to a Git commit.");
        }
        return commit.ToLowerInvariant();
    }

    private static string DevelopmentAttemptIdentity(string repository, string commit)
    {
        string root = Path.Combine(repository, "artifacts", "m1-slice6", "development-credential-continuation");
        Directory.CreateDirectory(root);
        string marker = Path.Combine(root, "active-attempt.v1.txt");
        if (File.Exists(marker))
        {
            string[] retained = File.ReadAllText(marker, Encoding.ASCII).Trim().Split(':');
            if (retained is [string retainedCommit, string retainedAttempt]
                && retainedCommit == commit && Guid.TryParseExact(retainedAttempt, "N", out _))
            {
                string retainedEvidence = Path.Combine(root, retainedCommit + "-" + retainedAttempt,
                    "replacement-evidence.v3.json");
                if (!File.Exists(retainedEvidence))
                {
                    return retainedAttempt;
                }
            }
        }
        string attempt = Guid.NewGuid().ToString("N");
        string temporary = Path.Combine(root, ".active-attempt-" + attempt + ".tmp");
        using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   bufferSize: 4096, FileOptions.WriteThrough))
        {
            byte[] bytes = Encoding.ASCII.GetBytes(commit + ":" + attempt + "\n");
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, marker, overwrite: true);
        return attempt;
    }

    internal static FileStream AcquireDevelopmentEnrollmentLock(string repository)
    {
        string root = Path.Combine(Path.GetFullPath(repository), "artifacts", "m1-slice6",
            "development-credential-continuation");
        Directory.CreateDirectory(root);
        return new FileStream(Path.Combine(root, "enrollment.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None, bufferSize: 1, FileOptions.WriteThrough);
    }

    private static JsonElement? ParseOptional(byte[]? bytes)
    {
        if (bytes is null) { return null; }
        using JsonDocument document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    private static void RetainAmbiguousFailure(ReplacementFailureContext context, Exception exception)
    {
        if (File.Exists(context.EvidencePath))
        {
            throw new IOException("The reviewed replacement evidence path already exists and cannot be trusted as this failure record.");
        }
        string lifecycle = "unknown-non-dispatchable-inspection-required";
        string generation = "unknown-generation";
        long ordinal = 0;
        try
        {
            CredentialProfileProjection projection =
                AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(context.ProductStateRoot, context.ProfileId);
            lifecycle = projection.LifecycleState;
            generation = projection.GenerationId;
            ordinal = projection.GenerationOrdinal;
        }
        catch
        {
            // Unknown is the conservative retained fact when durable inspection cannot complete.
        }
        string? checkpoint = null;
        try { checkpoint = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(context.ProductStateRoot); }
        catch { }
        object retained = new
        {
            schema_identity = FailureEvidenceSchema,
            evidence_id = context.EvidenceId,
            operation_id = context.OperationId,
            status = "stopped-ambiguous-effect-recovery-required",
            authority = new { id = context.AuthorityId, sha256 = context.AuthoritySha256 },
            independent_review = new { id = context.ReviewId, sha256 = context.ReviewSha256 },
            product_state = new
            {
                checkpoint_sha256 = checkpoint,
                profile_id = context.ProfileId,
                generation_id = generation,
                generation_ordinal = ordinal,
                lifecycle_state = lifecycle,
            },
            observed_effect_facts = "unknown-conservatively-blocked",
            typed_failure = exception.GetType().Name,
            isolation_observation = "fallback-fields-are-secret-free-effect-isolation-unverified-stop-condition",
            completed_at_utc = DateTimeOffset.UtcNow.ToString("O"),
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(retained, Json);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes,
            File.ReadAllBytes(Path.Combine(context.Repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-failure-evidence.v1.schema.json")),
            FailureEvidenceSchema);
        PublishEvidenceAtomically(context.EvidencePath, bytes);
    }

    private static void PublishEvidenceAtomically(string path, byte[] bytes)
    {
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, ".replacement-evidence-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }

    private sealed record ReplacementFailureContext(
        string Repository,
        string AuthorityId,
        string AuthoritySha256,
        string EvidenceId,
        string OperationId,
        string ReviewId,
        string ReviewSha256,
        string ProfileId,
        string ProductStateRoot,
        string EvidencePath);

    internal sealed record ReplacementRunnerTestHooks(
        DateTimeOffset UtcNow,
        string ClosedProductRoot,
        string CoordinatorPath,
        Func<AuthoritativeStore, string, HelperPrivateFrameV2, HelperPrivateFrameV2, DateTimeOffset,
            CancellationToken, Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>>
            EffectExecutor)
    {
        internal string? Generation3OwnerLedgerPath { get; init; }
    }
}
