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
    internal const string ReviewSchema =
        "infinium.repository.m1-slice6-successor-credential-replacement-review/1.0.0";
    internal const string EvidenceSchema =
        "infinium.m1-s6.successor-credential-replacement-evidence/v1";
    internal const string FailureEvidenceSchema =
        "infinium.m1-s6.successor-credential-replacement-failure-evidence/v1";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

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
        ReplacementRunnerTestHooks? testHooks = null)
    {
        ReplacementFailureContext? failureContext = null;
        try
        {
            return await RunCoreAsync(authorityPath, authoritySha256, reviewPath, reviewSha256,
                productStateRoot, ledgerPath, helperPath, helperSha256, evidencePath,
                context => failureContext = context, testHooks, cancellationToken)
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
        ActiveRepositoryJsonSchemaValidator.Validate(authorityBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-authorization.v1.schema.json")),
            AuthoritySchema);
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
        string evidenceId = authority.GetProperty("evidence_id").GetString()!;
        string profileId = profile.GetProperty("access_profile_id").GetString()!;
        string predecessorGeneration = profile.GetProperty("predecessor_generation_id").GetString()!;
        string successorGeneration = profile.GetProperty("successor_generation_id").GetString()!;
        string predecessorFingerprint = profile.GetProperty("predecessor_target_fingerprint_sha256").GetString()!;
        string successorFingerprint = profile.GetProperty("successor_target_fingerprint_sha256").GetString()!;
        string operationId = "m1s6-credential-replacement-" + authoritySha256[..32];
        if (authority.GetProperty("schema_identity").GetString() != AuthoritySchema
            || authority.GetProperty("status").GetString() != "independently-reviewed-ready-for-owner-effect"
            || prepared > notBefore || now < notBefore || now >= expires
            || reviewedAt < prepared || reviewedAt > now || reviewedAt >= expires
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
        ActiveRepositoryJsonSchemaValidator.Validate(ownerBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-development-campaign-amendment.v3.schema.json")),
            "infinium.repository.m1-slice6-development-campaign-amendment/3.0.0");
        using (JsonDocument ownerDocument = JsonDocument.Parse(ownerBytes))
        {
            if (ownerDocument.RootElement.GetProperty("amendment_id").GetString()
                    != owner.GetProperty("id").GetString()
                || ownerDocument.RootElement.GetProperty("status").GetString()
                    != "owner-authorized-credential-replacement")
            {
                throw new InvalidDataException("The exact owner credential-replacement amendment is stale.");
            }
        }
        string expectedProductRoot = Path.GetFullPath(state.GetProperty("root_absolute").GetString()!);
        string closedProductRoot = testHooks?.ClosedProductRoot is null
            ? Path.GetFullPath(Path.Combine(repository, "artifacts", "m1-slice6", "successor-product-state"))
            : Path.GetFullPath(testHooks.ClosedProductRoot);
        string expectedLedger = Path.GetFullPath(Path.Combine(repository,
            predecessor.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string expectedHelper = Path.GetFullPath(Path.Combine(repository,
            release.GetProperty("helper_path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string coordinatorPath = testHooks?.CoordinatorPath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("The coordinator executable path is unavailable.");
        string expectedCoordinator = Path.GetFullPath(Path.Combine(repository,
            release.GetProperty("coordinator_path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        string expectedEvidence = Path.GetFullPath(Path.Combine(repository,
            effect.GetProperty("evidence_path").GetString()!.Replace('/', Path.DirectorySeparatorChar)));
        if (productStateRoot != expectedProductRoot || productStateRoot != closedProductRoot || ledgerPath != expectedLedger
            || helperPath != expectedHelper || Path.GetFullPath(coordinatorPath) != expectedCoordinator
            || evidencePath != expectedEvidence || !Directory.Exists(productStateRoot)
            || !File.Exists(ledgerPath) || !File.Exists(helperPath) || File.Exists(evidencePath)
            || Directory.Exists(Path.GetDirectoryName(evidencePath)!))
        {
            throw new InvalidDataException("The credential replacement effect roots differ from exact reviewed authority.");
        }
        string coordinatorSha = M1Slice6SuccessorAuthorityLoader.HashFile(coordinatorPath);
        if (M1Slice6SuccessorAuthorityLoader.HashFile(ledgerPath) != predecessor.GetProperty("sha256").GetString()
            || M1Slice6SuccessorAuthorityLoader.HashFile(helperPath) != helperSha256
            || helperSha256 != release.GetProperty("helper_sha256").GetString()
            || coordinatorSha != release.GetProperty("coordinator_sha256").GetString()
            || !AssemblyCommit().EndsWith(release.GetProperty("implementation_commit").GetString()!,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The reviewed replacement ledger or executable binding is stale.");
        }
        using (JsonDocument tailDocument = JsonDocument.Parse(File.ReadLines(ledgerPath, Encoding.UTF8).Last()))
        {
            JsonElement tail = tailDocument.RootElement;
            if (tail.GetProperty("sequence").GetInt64() != predecessor.GetProperty("sequence").GetInt64()
                || tail.GetProperty("event_hash").GetString() != predecessor.GetProperty("event_hash").GetString())
            {
                throw new InvalidDataException("The replacement predecessor is not the exact retained ledger tip.");
            }
        }
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
            || validatedLedger.Current.EventHash != "b5292326a65791731614a6ab75a0b838de121ea721d01d5489f4c2897f88dcc0"
            || validatedLedger.Current.Wp9PossibleStarts != 8
            || validatedLedger.Current.Wp10PossibleStarts != 0
            || validatedLedger.Current.Wp11PossibleStarts != 0
            || validatedLedger.Current.Wp9Authoritative || validatedLedger.Current.Wp10Authoritative
            || validatedLedger.Current.Wp11Authoritative
            || validatedLedger.Current.SuccessorOutstandingReservedNanoUsd != 0
            || validatedLedger.Current.SuccessorUnresolvedNanoUsd != 770_560_000
            || validatedLedger.CommittedNanoUsd != 910_560_000)
        {
            throw new InvalidDataException("The replacement ledger does not preserve the exact seq39 accounting state.");
        }
        string beforeCheckpoint = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productStateRoot);
        if (beforeCheckpoint != state.GetProperty("checkpoint_sha256").GetString())
        {
            throw new InvalidDataException("The credential replacement product-state checkpoint is stale.");
        }
        CoordinatedHelperReceipt helper;
        CredentialProfileProjection projection;
        using (AuthoritativeStore store = new(new StoragePaths(productStateRoot)))
        {
            CredentialProfileProjection current = store.GetCredentialProfile(profileId);
            if (current.GenerationId != predecessorGeneration || current.GenerationOrdinal != 1
                || current.LifecycleState != "active-verified" || current.VerificationState != "available")
            {
                throw new InvalidDataException("The replacement predecessor is not the exact active verified generation.");
            }
            if (profileId != "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e"
                || predecessorGeneration != "g-ff6d82e7a7d244f6b8a9d0164991be37"
                || predecessorFingerprint != "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0"
                || store.CredentialGenerationExists(profileId, successorGeneration))
            {
                throw new InvalidDataException("The credential replacement identity is stale or the successor is not fresh.");
            }
            admitFailureRetention(new ReplacementFailureContext(
                repository, authorityId, authoritySha256, evidenceId, operationId,
                review.GetProperty("review_id").GetString()!, reviewSha256,
                profileId, productStateRoot, evidencePath));
            CredentialProfileProjection replacing = store.BeginCredentialReplacement(
                operationId + "-begin", profileId, predecessorGeneration, successorGeneration, 2, now.AddTicks(1));
            if (replacing.GenerationId != predecessorGeneration || replacing.LifecycleState != "replacing"
                || replacing.VerificationState != "unavailable")
            {
                throw new InvalidOperationException("The durable replacement intent did not make the predecessor ineligible.");
            }
            DateTimeOffset helperExpiry = now.AddMinutes(11) < expires ? now.AddMinutes(11) : expires;
            HelperPrivateFrameV2 bootstrap = Bootstrap(
                profileId, predecessorGeneration, now, helperExpiry, authorityId);
            HelperPrivateFrameV2 assignment = Assignment(profileId, successorGeneration, authorityId);
            Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
            using CancellationTokenSource effectDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            TimeSpan remaining = helperExpiry - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) { throw new TimeoutException("Replacement authority expired before helper launch."); }
            effectDeadline.CancelAfter(remaining);
            if (testHooks?.EffectExecutor is not null)
            {
                (helper, projection) = await testHooks.EffectExecutor(
                    store, operationId, bootstrap, assignment, now, effectDeadline.Token).ConfigureAwait(false);
            }
            else
            {
                OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateWp9ProductionEnrollment(
                    helperPath, helperSha256, authorityPath, authoritySha256, authorityId);
                CredentialHelperCoordinator coordinator = new(store, launcher);
                (helper, projection) = await coordinator.ExecuteVerifiedReplacementAsync(
                    operationId, bootstrap, assignment, now, effectDeadline.Token).ConfigureAwait(false);
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
            && projection.GenerationOrdinal == 2
            && projection.LifecycleState == "active-verified"
            && projection.VerificationState == "available";
        bool exactStopped = helper.Process.Receipt.Outcome is HelperOutcomeV2.Cancelled
                or HelperOutcomeV2.FailedKnown or HelperOutcomeV2.Unavailable
            && projection.GenerationId == predecessorGeneration
            && projection.GenerationOrdinal == 1
            && projection.LifecycleState == "delete-pending"
            && projection.VerificationState == "unavailable";
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
        JsonElement entry = ParseOptional(helper.Process.NativeEntryCleanupBytes)
            ?? throw new InvalidDataException("Credential replacement omitted its entry cleanup evidence.");
        if (status == "passed-active-verified-predecessor-absent")
        {
            ValidateSuccessfulBoundary(helper.Process, entry, canaries);
            ValidateSuccessfulTrace(trace, predecessorFingerprint, successorFingerprint,
                helper.Process.NativeCredentialOperationCount);
        }
        else
        {
            ValidateStoppedBoundary(helper.Process, entry, canaries);
            ValidateStoppedTrace(trace, predecessorFingerprint, successorFingerprint,
                helper.Process.NativeCredentialOperationCount, helper.Process.Receipt.Outcome);
            bool collision = trace.GetArrayLength() == 2
                && trace[0].GetProperty("Operation").GetString() == "CredReadW"
                && trace[0].GetProperty("Result").GetString() == "success";
            if (helper.Process.NativeNamespaceReuseBlocked != collision
                || collision && helper.Process.NativeNamespaceReuseBlockReason != "preflight-collision"
                || !collision && helper.Process.NativeNamespaceReuseBlockReason is not null)
            {
                throw new InvalidDataException("The stopped replacement collision and namespace-reuse facts disagree.");
            }
        }
        object evidence = new
        {
            schema_identity = EvidenceSchema,
            evidence_id = evidenceId,
            status,
            authority = new { id = authorityId, sha256 = authoritySha256 },
            independent_review = new
            {
                id = review.GetProperty("review_id").GetString(),
                sha256 = reviewSha256,
            },
            profile = new
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
            },
            product_state = new
            {
                root_projection_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(productStateRoot))),
                checkpoint_before_sha256 = beforeCheckpoint,
                checkpoint_after_sha256 = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productStateRoot),
            },
            effect = new
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
            },
            completed_at_utc = DateTimeOffset.UtcNow.ToString("O"),
        };
        byte[] evidenceBytes = JsonSerializer.SerializeToUtf8Bytes(evidence, Json);
        ActiveRepositoryJsonSchemaValidator.Validate(evidenceBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-credential-replacement-evidence.v1.schema.json")),
            EvidenceSchema);
        PublishEvidenceAtomically(evidencePath, evidenceBytes);
        return status == "passed-active-verified-predecessor-absent" ? 0 : 2;
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
            || calls.Length == 0 && outcome is not (HelperOutcomeV2.Cancelled or HelperOutcomeV2.Unavailable))
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
            if (!call.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal)
                || call.GetProperty("Sequence").GetInt64() != index + 1
                || call.GetProperty("Scenario").GetString() != "m1-slice6-successor-credential-replacement"
                || call.GetProperty("Operation").GetString() != operations[index]
                || call.GetProperty("TargetFingerprintSha256").GetString() != fingerprints[index]
                || result != results[index]
                    && !(index == calls.Length - 1
                        && result.StartsWith("win32-error:", StringComparison.Ordinal))
                || allocation != (index switch { 2 => 1, 4 => 2, 6 => 3, _ => null })
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
        ValidateCanaries(canaries);
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
        ValidateCanaries(canaries);
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

    private static void ValidateCanaries(JsonElement canaries)
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
        string[] expectedNames = ["private protocol request", "private protocol response", "native call trace",
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
                || surfaces[index].GetProperty("ByteCount").GetInt64() <= 0
                || surfaces[index].GetProperty("SecretMatches").GetInt32() != 0
                || surfaces[index].GetProperty("RawTargetMatches").GetInt32() != 0)
            {
                throw new InvalidDataException("A replacement canary surface is vacuous or nonzero.");
            }
        }
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

    private static HelperPrivateFrameV2 Assignment(string profileId, string generationId, string authorityId) => new()
    {
        Sequence = 2,
        ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
        Assignment = new()
        {
            AssignmentId = authorityId + "/replace",
            CommandId = authorityId + "/command",
            AssignmentKind = HelperAssignmentKindV2.Replace,
            AccessProfileId = new() { Value = profileId },
            GenerationId = new() { Value = generationId },
            GenerationOrdinal = 2,
            Credential = new() { AccessProfileId = new() { Value = profileId }, GenerationId = new() { Value = generationId } },
        },
    };

    private static Instant Instant(DateTimeOffset value) => new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };

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
            EffectExecutor);
}
