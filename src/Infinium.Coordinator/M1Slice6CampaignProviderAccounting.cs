using System.Security.Cryptography;
using System.Text;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Coordinator;

/// <summary>
/// Binds every finite campaign stage to the accepted authoritative SQLite provider graph.
/// The append-only campaign JSONL remains effect/admission authority; it is not a substitute
/// for durable operation, reservation, response, usage, settlement, and replay persistence.
/// </summary>
public sealed class M1Slice6CampaignSqliteProviderAccounting : IM1Slice6CampaignProviderAccounting,
    IM1Slice6CampaignRecoveryAccounting, IDisposable
{
    private readonly AuthoritativeStore store;
    private readonly ProviderAccountingCoordinator accounting;
    private readonly string profileId;
    private readonly string generationId;
    private readonly string credentialTargetFingerprintSha256;
    private readonly string accountIdentityId;
    private readonly string billingScopeIdentityId;
    private readonly Dictionary<string, ProviderBudgetVectorContract> reservations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> successorCampaignIdsByAttempt = new(StringComparer.Ordinal);

    public M1Slice6CampaignSqliteProviderAccounting(string stateRoot, string credentialManifestPath,
        string credentialManifestSha256, DateTimeOffset now)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(credentialBytes)) != credentialManifestSha256)
        {
            throw new InvalidDataException("Authoritative provider accounting has stale credential authority bytes.");
        }
        using System.Text.Json.JsonDocument credential = System.Text.Json.JsonDocument.Parse(credentialBytes);
        System.Text.Json.JsonElement root = credential.RootElement;
        System.Text.Json.JsonElement profile = root.GetProperty("profile");
        bool developmentGeneration3 = root.GetProperty("schema_identity").GetString()
            == "infinium.repository.m1-slice6-successor-credential-replacement-authorization/2.0.0";
        System.Text.Json.JsonElement intent;
        System.Text.Json.JsonDocument? providerDocument = null;
        if (developmentGeneration3)
        {
            string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(credentialManifestPath);
            string providerPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
                "wp9-production-profile-authorization.v5.json");
            if (M1Slice6SuccessorAuthorityLoader.HashFile(providerPath)
                != "49b71673b144dc5c5118f4dbfec52d22ca9f8f380ebe4cb7f9d7959746d93939"
                || root.GetProperty("status").GetString() != "independently-reviewed-ready-for-owner-effect"
                || profile.GetProperty("successor_generation_ordinal").GetInt32() != 3)
            { throw new InvalidDataException("Development provider accounting authority is stale."); }
            providerDocument = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(providerPath));
            intent = providerDocument.RootElement.GetProperty("provider_intent");
        }
        else
        {
            intent = root.GetProperty("provider_intent");
        }
        profileId = profile.GetProperty("access_profile_id").GetString()!;
        generationId = profile.GetProperty(developmentGeneration3
            ? "successor_generation_id" : "generation_id").GetString()!;
        credentialTargetFingerprintSha256 = profile.GetProperty(developmentGeneration3
            ? "successor_target_fingerprint_sha256" : "target_fingerprint_sha256").GetString()!;
        accountIdentityId = intent.GetProperty("account_identity_id").GetString()!;
        billingScopeIdentityId = intent.GetProperty("billing_scope_identity_id").GetString()!;
        providerDocument?.Dispose();
        if (string.IsNullOrWhiteSpace(accountIdentityId)
            || string.IsNullOrWhiteSpace(billingScopeIdentityId)
            || accountIdentityId == "unavailable" || billingScopeIdentityId == "unavailable")
        {
            throw new InvalidDataException("Authoritative provider accounting requires an exact credential/account/billing binding.");
        }
        store = new(new StoragePaths(Path.GetFullPath(stateRoot)));
        accounting = new(store);
        try
        {
            accounting.PublishExactCatalog(now);
            RequireVerifiedCredential();
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    public M1Slice6CampaignAccountingAdmission Prepare(M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignIdentity campaignIdentity, DateTimeOffset now)
    {
        if (campaignIdentity.CredentialProfileId != profileId
            || campaignIdentity.CredentialGenerationId != generationId)
        {
            throw new InvalidDataException("Provider accounting rejected a cross-profile stage.");
        }
        string prefix = "m1s6-campaign-stage-" + (int)authority.Stage;
        string canonicalInput = M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(
            authority.CanonicalRequest);
        SourceClaimExecutionInput? sourceInput = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? M1Slice6CampaignV2InputAdapter.ReadSourceClaim(canonicalInput) : null;
        CandidateInvestigationExecutionInput? candidateInput =
            authority.Stage == M1Slice6CampaignStage.CandidateInvestigation
                ? M1Slice6CampaignV2InputAdapter.ReadCandidate(canonicalInput).ProductInput : null;
        string operationId = sourceInput?.OperationId ?? candidateInput?.OperationId ?? prefix + "-operation";
        string attemptId = prefix + "-attempt-1";
        string requestId = prefix + "-request";
        string reservationId = prefix + "-reservation-1";
        string authorizationId = sourceInput?.HostAuthorizationId
            ?? candidateInput?.HostAuthorizationId ?? prefix + "-authorization";
        string dispatchFenceId = prefix + "-dispatch-1";
        string ownerKind = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? "evidence-acquisition-run" : "analysis-run";
        string ownerId = sourceInput?.AcquisitionRunId ?? candidateInput?.AnalysisRunId
            ?? (authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
                ? prefix + "-acquisition" : prefix + "-run");
        // Coordinator leases are process-clock authority, while the retained stage clock is
        // an independently validated campaign timestamp. Never project a rehearsed/event time
        // into the live fencing lease comparison performed by AuthoritativeStore.
        CoordinatorAuthority coordinator = store.AcquireCoordinatorAuthorityAfterProcessExclusion(
            "m1-s6-finite-campaign", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        DateTimeOffset deadline = now.AddMilliseconds(authority.Limits.DeadlineMilliseconds);
        SeedOperationGraph(authority, prefix, ownerKind, ownerId, operationId, attemptId,
            requestId, authorizationId, coordinator.FencingEpoch, deadline, now);
        M1Slice6CampaignSemanticAdmission.PreparePrerequisites(store, authority, now);
        ProviderFiniteLimitsContract finiteLimits = new(
            authority.Limits.MaximumRequestBytes,
            authority.Limits.MaximumInputTokens,
            authority.Limits.MaximumOutputTokens,
            authority.Limits.MaximumRawResponseBytes,
            1,
            authority.Limits.MaximumNanoUsd,
            authority.Limits.DeadlineMilliseconds);
        long catalogWorstCaseNanoUsd = M1ProviderCatalog.CalculateWorstCaseNanoUsd(
            authority.Operation, finiteLimits);
        ProviderBudgetVectorContract vector = new(1, authority.Limits.MaximumInputTokens,
            authority.Limits.MaximumOutputTokens,
            checked(authority.Limits.MaximumInputTokens + authority.Limits.MaximumOutputTokens),
            authority.Limits.MaximumOutputTokens, 0, 0, 0, catalogWorstCaseNanoUsd);
        List<string> kinds = ["request", "operation", ownerKind];
        List<string> ids = [requestId, operationId, ownerId];
        if (ownerKind == "evidence-acquisition-run")
        {
            kinds.Add("analysis-run");
            ids.Add(sourceInput?.ParentAnalysisRunId
                ?? throw new InvalidDataException("WP10 canonical request has no exact parent analysis run."));
        }
        kinds.AddRange(["provider-profile", "provider-account", "billing-scope", "global"]);
        ids.AddRange([profileId, accountIdentityId, billingScopeIdentityId, "provider-global"]);
        ProviderBudgetVectorContract campaignLimit = new(
            3, 167_936, 8_448, 176_384, 8_448, 0, 0, 0, 1_340_000_000);
        ProviderBudgetScopeContract[] scopes = kinds.Zip(ids,
            (kind, id) => new ProviderBudgetScopeContract(kind, new OpaqueId(id),
                kind is "analysis-run" or "provider-profile" or "provider-account" or "billing-scope" or "global"
                    ? campaignLimit : vector)).ToArray();
        accounting.ConfigureLimits(coordinator.FencingEpoch, scopes, now);
        bool reserved = false;
        ProviderDispatchGateReceipt gate;
        try
        {
            _ = accounting.Reserve(coordinator.FencingEpoch, new(reservationId, operationId, attemptId,
                requestId, vector, scopes, deadline, now.AddTicks(1)));
            reserved = true;
            gate = accounting.FinalGate(new(dispatchFenceId, authorizationId,
                operationId, reservationId, attemptId, requestId, profileId, generationId, 0,
                coordinator.FencingEpoch, now.AddTicks(2)));
            if (!gate.Authorized || gate.DecisionReason != "exact-final-gate-authorized"
                || gate.ReservationId != reservationId || gate.CoordinatorFencingEpoch != coordinator.FencingEpoch
                || gate.Deadline != deadline)
            {
                throw new InvalidDataException("Authoritative SQLite final gate did not admit the exact one-shot stage.");
            }
        }
        catch
        {
            if (reserved)
            {
                ProviderBudgetSettlementReceipt released = accounting.Settle(new(
                    "m1s6-campaign-prepare-release-" + reservationId, reservationId,
                    ProviderBudgetEventKind.ReleasedUndispatched, null, null, now.AddTicks(3)));
                if (released.Released != vector || released.Settled != ProviderBudgetVectorContract.Zero
                    || released.Unresolved != ProviderBudgetVectorContract.Zero || released.RetryPermitted)
                {
                    throw new InvalidDataException(
                        "Authoritative prepare failure did not converge to released-undispatched.");
                }
            }
            throw;
        }
        reservations.Add(reservationId, vector);
        return new(authorizationId, operationId, attemptId, requestId, reservationId,
            dispatchFenceId, coordinator.FencingEpoch, gate.EffectiveGateTime, gate.Deadline,
            accountIdentityId, billingScopeIdentityId);
    }

    public M1Slice6CampaignAccountingAdmission PrepareSuccessor(
        M1Slice6CampaignStageAuthority authority, M1Slice6CampaignIdentity campaignIdentity,
        M1Slice6SuccessorAttemptIdentity attempt, DateTimeOffset now)
    {
        if (campaignIdentity.CredentialProfileId != profileId
            || campaignIdentity.CredentialGenerationId != generationId
            || attempt.Stage != authority.Stage || attempt.AttemptOrdinal <= 0)
        {
            throw new InvalidDataException("Successor accounting rejected a cross-profile or cross-stage attempt.");
        }
        string canonicalInput = M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(
            authority.CanonicalRequest);
        SourceClaimExecutionInput? sourceInput = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? M1Slice6CampaignV2InputAdapter.ReadSourceClaim(canonicalInput) : null;
        CandidateInvestigationExecutionInput? candidateInput =
            authority.Stage == M1Slice6CampaignStage.CandidateInvestigation
                ? M1Slice6CampaignV2InputAdapter.ReadCandidate(canonicalInput).ProductInput : null;
        string semanticOperationId = sourceInput?.OperationId ?? candidateInput?.OperationId
            ?? "m1s6-campaign-stage-1-operation";
        string semanticAuthorizationId = sourceInput?.HostAuthorizationId
            ?? candidateInput?.HostAuthorizationId ?? "m1s6-campaign-stage-1-authorization";
        string prefix = "m1s6-successor-" + attempt.AttemptId;
        string operationId = prefix + "-transport-operation";
        string authorizationId = prefix + "-transport-authorization";
        string ownerKind = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? "evidence-acquisition-run" : "analysis-run";
        string ownerId = sourceInput?.AcquisitionRunId ?? candidateInput?.AnalysisRunId
            ?? prefix + "-run";
        CoordinatorAuthority coordinator = store.AcquireCoordinatorAuthorityAfterProcessExclusion(
            "m1-s6-successor-campaign", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        DateTimeOffset deadline = now.AddMilliseconds(authority.Limits.DeadlineMilliseconds);
        string requestFingerprint = authority.CanonicalRequestSha256;
        SeedOperationGraph(authority, prefix, ownerKind, ownerId, operationId, attempt.AttemptId,
            attempt.RequestId, authorizationId, coordinator.FencingEpoch, deadline, now,
            semanticOperationId, semanticAuthorizationId, requestFingerprint);
        M1Slice6CampaignSemanticAdmission.PreparePrerequisites(store, authority, now);
        ProviderFiniteLimitsContract finiteLimits = new(
            authority.Limits.MaximumRequestBytes, authority.Limits.MaximumInputTokens,
            authority.Limits.MaximumOutputTokens, authority.Limits.MaximumRawResponseBytes,
            1, authority.Limits.MaximumNanoUsd, authority.Limits.DeadlineMilliseconds);
        long catalogWorstCaseNanoUsd = M1ProviderCatalog.CalculateWorstCaseNanoUsd(
            authority.Operation, finiteLimits);
        ProviderBudgetVectorContract vector = new(1, authority.Limits.MaximumInputTokens,
            authority.Limits.MaximumOutputTokens,
            checked(authority.Limits.MaximumInputTokens + authority.Limits.MaximumOutputTokens),
            authority.Limits.MaximumOutputTokens, 0, 0, 0, catalogWorstCaseNanoUsd);
        ProviderBudgetVectorContract successorLimit = new(
            14, 1_000_000, 100_000, 1_100_000, 100_000, 0, 0, 0,
            M1Slice6SuccessorCampaignLedger.SuccessorMaximumNanoUsd);
        List<ProviderBudgetScopeContract> scopeList =
        [
            new("request", new OpaqueId(attempt.RequestId), vector),
            new("operation", new OpaqueId(operationId), vector),
        ];
        if (ownerKind == "evidence-acquisition-run")
        { scopeList.Add(new(ownerKind, new OpaqueId(ownerId), successorLimit)); }
        scopeList.AddRange(
        [
            new("analysis-run", new OpaqueId("m1s6-successor-campaign-budget"), successorLimit),
            new("provider-profile", new OpaqueId("m1s6-successor-profile-budget"), successorLimit),
            new("provider-account", new OpaqueId("m1s6-successor-account-budget"), successorLimit),
            new("billing-scope", new OpaqueId("m1s6-successor-billing-budget"), successorLimit),
            new("global", new OpaqueId("m1s6-successor-global"), successorLimit),
        ]);
        ProviderBudgetScopeContract[] scopes = [.. scopeList];
        accounting.ConfigureLimits(coordinator.FencingEpoch, scopes, now);
        bool reserved = false;
        try
        {
            _ = accounting.Reserve(coordinator.FencingEpoch, new(attempt.ReservationId,
                operationId, attempt.AttemptId, attempt.RequestId, vector, scopes,
                deadline, now.AddTicks(1)));
            reserved = true;
            ProviderDispatchGateReceipt gate = accounting.FinalGate(new(attempt.DispatchFenceId,
                authorizationId, operationId, attempt.ReservationId, attempt.AttemptId,
                attempt.RequestId, profileId, generationId, 0, coordinator.FencingEpoch,
                now.AddTicks(2)));
            if (!gate.Authorized || gate.DecisionReason != "exact-final-gate-authorized"
                || gate.ReservationId != attempt.ReservationId
                || gate.CoordinatorFencingEpoch != coordinator.FencingEpoch
                || gate.Deadline != deadline)
            {
                throw new InvalidDataException("Successor SQLite final gate did not admit the exact attempt.");
            }
            reservations.Add(attempt.ReservationId, vector);
            successorCampaignIdsByAttempt.Add(attempt.AttemptId, campaignIdentity.CampaignId);
            return new(authorizationId, operationId, attempt.AttemptId, attempt.RequestId,
                    attempt.ReservationId, attempt.DispatchFenceId, coordinator.FencingEpoch,
                    gate.EffectiveGateTime, gate.Deadline, accountIdentityId, billingScopeIdentityId,
                    semanticOperationId, semanticAuthorizationId, vector.NanoUsd);
        }
        catch
        {
            if (reserved)
            {
                _ = accounting.Settle(new("m1s6-successor-prepare-release-" + attempt.ReservationId,
                    attempt.ReservationId, ProviderBudgetEventKind.ReleasedUndispatched,
                    null, null, now.AddTicks(3)));
            }
            throw;
        }
    }

    public M1Slice6CampaignAccountingAdmission PrepareSuccessorV6(
        M1Slice6CampaignStageAuthority authority, M1Slice6CampaignIdentity campaignIdentity,
        M1Slice6SuccessorAttemptIdentity attempt, DateTimeOffset now)
    {
        if (campaignIdentity.CredentialProfileId != profileId
            || campaignIdentity.CredentialGenerationId != generationId
            || attempt.Stage != authority.Stage || attempt.AttemptOrdinal <= 0)
        {
            throw new InvalidDataException("Successor accounting rejected a cross-profile or cross-stage attempt.");
        }
        string canonicalInput = M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(
            authority.CanonicalRequest);
        SourceClaimExecutionInput? sourceInput = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? M1Slice6CampaignV2InputAdapter.ReadSourceClaim(canonicalInput) : null;
        CandidateInvestigationExecutionInput? candidateInput =
            authority.Stage == M1Slice6CampaignStage.CandidateInvestigation
                ? M1Slice6CampaignV2InputAdapter.ReadCandidate(canonicalInput).ProductInput : null;
        string semanticOperationId = sourceInput?.OperationId ?? candidateInput?.OperationId
            ?? "m1s6-campaign-stage-1-operation";
        string semanticAuthorizationId = sourceInput?.HostAuthorizationId
            ?? candidateInput?.HostAuthorizationId ?? "m1s6-campaign-stage-1-authorization";
        string prefix = "m1s6-successor-v6-" + attempt.AttemptId;
        string operationId = prefix + "-transport-operation";
        string authorizationId = prefix + "-transport-authorization";
        string ownerKind = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? "evidence-acquisition-run" : "analysis-run";
        string ownerId = sourceInput?.AcquisitionRunId ?? candidateInput?.AnalysisRunId
            ?? prefix + "-run";
        CoordinatorAuthority coordinator = store.AcquireCoordinatorAuthorityAfterProcessExclusion(
            "m1-s6-successor-campaign", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        DateTimeOffset deadline = now.AddMilliseconds(authority.Limits.DeadlineMilliseconds);
        M1Slice6CampaignSemanticAdmission.PreparePrerequisites(store, authority, now);
        ProviderFiniteLimitsContract finiteLimits = new(
            authority.Limits.MaximumRequestBytes, authority.Limits.MaximumInputTokens,
            authority.Limits.MaximumOutputTokens, authority.Limits.MaximumRawResponseBytes,
            1, authority.Limits.MaximumNanoUsd, authority.Limits.DeadlineMilliseconds);
        long catalogWorstCaseNanoUsd = M1Slice6SuccessorPricing.Calculate(finiteLimits);
        ProviderBudgetVectorContract vector = new(1, authority.Limits.MaximumInputTokens,
            authority.Limits.MaximumOutputTokens,
            checked(authority.Limits.MaximumInputTokens + authority.Limits.MaximumOutputTokens),
            authority.Limits.MaximumOutputTokens, 0, 0, 0, catalogWorstCaseNanoUsd);
        string stage = authority.Stage switch
        {
            M1Slice6CampaignStage.Qualification => "qualification",
            M1Slice6CampaignStage.SourceClaimExtraction => "source-claim-extraction",
            _ => "candidate-investigation",
        };
        string operationKind = authority.Operation switch
        {
            ProviderOperationKind.TransportQualification => "transport-qualification",
            ProviderOperationKind.SourceClaimExtraction => "source-claim-extraction",
            _ => "candidate-investigation",
        };
        M1Slice6SuccessorV6AdmissionReceipt gate = store.AdmitM1Slice6SuccessorV6(new(
            authorizationId, operationId, campaignIdentity.CampaignId, stage, operationKind,
            attempt.AttemptId, attempt.RequestId, attempt.ReservationId, attempt.DispatchFenceId,
            ownerKind, ownerId, semanticAuthorizationId, semanticOperationId,
            authority.CanonicalRequestSha256, authority.Limits.MaximumRequestBytes,
            authority.Limits.MaximumInputTokens, authority.Limits.MaximumOutputTokens,
            authority.Limits.MaximumRawResponseBytes, authority.Limits.DeadlineMilliseconds,
            vector.NanoUsd, coordinator.FencingEpoch, deadline, now));
        reservations.Add(attempt.ReservationId, vector);
        successorCampaignIdsByAttempt.Add(attempt.AttemptId, campaignIdentity.CampaignId);
        return new(authorizationId, operationId, attempt.AttemptId, attempt.RequestId,
                attempt.ReservationId, attempt.DispatchFenceId, coordinator.FencingEpoch,
                gate.EffectiveGateTime, gate.DeadlineUtc, accountIdentityId, billingScopeIdentityId,
                semanticOperationId, semanticAuthorizationId, vector.NanoUsd);
    }

    public void RecordPossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now)
    {
        if (!reservations.ContainsKey(admission.ReservationId) || now >= admission.DeadlineUtc)
        {
            throw new InvalidDataException("Authoritative transport-start persistence is expired or unreserved.");
        }
        if (admission.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
        {
            store.RecordM1Slice6SuccessorV6PossibleStart(admission.OperationId, admission.AttemptId,
                admission.RequestId, admission.ReservationId, admission.DispatchFenceId, now);
        }
        else
        {
            store.RecordProviderTransportStart(admission.OperationId, admission.AttemptId,
                admission.RequestId, admission.DispatchFenceId, ambiguous: false, now);
        }
    }

    public void ReleaseBeforePossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now)
    {
        if (!reservations.Remove(admission.ReservationId, out ProviderBudgetVectorContract? reserved))
        {
            throw new InvalidDataException("Authoritative prestart release lacks its exact reservation.");
        }
        if (admission.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
        {
            store.ReleaseM1Slice6SuccessorV6BeforeStart(admission.OperationId, admission.ReservationId, now);
            return;
        }
        ProviderBudgetSettlementReceipt released = accounting.Settle(new(
            "m1s6-campaign-prestart-release-" + admission.ReservationId,
            admission.ReservationId, ProviderBudgetEventKind.ReleasedUndispatched, null, null, now));
        if (released.Released != reserved || released.Settled != ProviderBudgetVectorContract.Zero
            || released.Unresolved != ProviderBudgetVectorContract.Zero || released.RetryPermitted)
        {
            throw new InvalidDataException("Authoritative prestart reservation was not exactly released-undispatched.");
        }
    }

    public M1Slice6CampaignAccountingSettlement PersistSettleAndReplay(
        M1Slice6CampaignAccountingAdmission admission, M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignStageBoundaryResult result)
    {
        if (!reservations.TryGetValue(admission.ReservationId, out ProviderBudgetVectorContract? reserved))
        {
            throw new InvalidDataException("Authoritative provider settlement lacks its exact reservation.");
        }
        OpenAiResponsesResult response = result.Response;
        ProviderRateLimitFactContract[] rates = response.RateHeaders.Select((fact, index) => new ProviderRateLimitFactContract(
            "provider-response-header", fact.Name, ProviderAvailabilityState.Available,
            fact.Value, fact.Value, new UtcTimestamp(result.CompletedAtUtc), null)).ToArray();
        string identity = "m1s6-campaign-stage-" + (int)authority.Stage;
        string responseId = identity + "-response";
        string usageId = identity + "-usage";
        string settlementId = identity + "-settlement";
        ProviderSimulationPersistenceReceipt persisted = store.PersistProviderSimulation(new(
            responseId, usageId, identity + "-receipt", identity + "-finalization",
            admission.AuthorizationId, admission.OperationId, admission.ReservationId,
            admission.AttemptId, admission.RequestId, admission.DispatchFenceId,
            response.State, response.HttpStatus ?? 0, response.ReturnedModel,
            response.ReturnedServiceTier, response.ErrorCode, response.RefusalCode,
            response.IncompleteReason, response.Usage, rates, response.RawResponseBytes,
            result.CompletedAtUtc, result.ResponseHeadersBytes, response.ProviderResponseId,
            response.ProviderRequestId, response.Admitted));
        ProviderBudgetSettlementReceipt settled = accounting.Settle(new(settlementId,
            admission.ReservationId, persisted.SettlementKind, persisted.UsageEntryId,
            persisted.Actual, result.CompletedAtUtc.AddTicks(1)));
        OpenAiResponsesResult replay = accounting.Replay(new(new OpaqueId(admission.OperationId),
            new OpaqueId(responseId), NetworkPermitted: false));
        ProviderOperationReadModel operation = store.ReadProviderOperation(admission.OperationId);
        long exactCost = Required(response.Usage.CalculatedNanoUsd, "calculated cost");
        if (replay.State != response.State || replay.ProviderResponseId != response.ProviderResponseId
            || replay.ProviderRequestId != response.ProviderRequestId
            || replay.ReturnedModel != response.ReturnedModel
            || replay.ReturnedServiceTier != response.ReturnedServiceTier
            || replay.RawResponseBytes is null || response.RawResponseBytes is null
            || !replay.RawResponseBytes.AsSpan().SequenceEqual(response.RawResponseBytes)
            || operation.ResponseId != responseId || operation.UsageEntryId != usageId
            || operation.SettlementId != settlementId || operation.ReplayState != "retained-response"
            || operation.RawResponseBytes is null || !operation.RawResponseBytes.AsSpan().SequenceEqual(response.RawResponseBytes)
            || operation.ResponseHeadersBytes is null
            || !operation.ResponseHeadersBytes.AsSpan().SequenceEqual(result.ResponseHeadersBytes)
            || settled.Settled.NanoUsd != exactCost || settled.Unresolved != ProviderBudgetVectorContract.Zero
            || settled.RetryPermitted || persisted.Actual.NanoUsd != exactCost
            || !ProviderBudgetVectorContract.FitsWithin(ProviderBudgetVectorContract.Zero, persisted.Actual, reserved))
        {
            throw new InvalidDataException("Authoritative SQLite replay or settlement differs from exact retained stage evidence.");
        }
        M1Slice6CampaignSemanticAdmissionReceipt semantic;
        try
        {
            semantic = M1Slice6CampaignSemanticAdmission.Admit(
                store, authority, admission, response, result.CompletedAtUtc.AddTicks(2));
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
        {
            throw new M1Slice6CampaignKnownSettlementException(
                "Authoritative provider response is settled with no retry, but semantic evidence is unreviewable.",
                new M1Slice6CampaignRecoveredSettlement(
                    Required(response.Usage.InputTokens, "settled input tokens"),
                    Required(response.Usage.OutputTokens, "settled output tokens"),
                    response.RawResponseBytes?.LongLength
                        ?? throw new InvalidDataException("Settled response bytes are absent."),
                    exactCost),
                exception);
        }
        return new(responseId, usageId, settlementId, operation.ReplayEdgeId,
            Convert.ToHexStringLower(SHA256.HashData(response.RawResponseBytes)),
            Convert.ToHexStringLower(SHA256.HashData(result.ResponseHeadersBytes)),
            exactCost, operation.UnresolvedHold, settled.RetryPermitted,
            semantic.ValidationId, semantic.Disposition, semantic.ProposalCount,
            semantic.AdmissionCount, semantic.ResultSha256, semantic.Provenance);
    }

    internal M1Slice6SuccessorAccountingPersistence PersistSuccessorAttempt(
        M1Slice6CampaignAccountingAdmission admission, M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignStageBoundaryResult result, bool structurallyValid,
        Action? beforeSuccessorSemanticBinding = null)
    {
        if (!reservations.TryGetValue(admission.ReservationId,
                out ProviderBudgetVectorContract? reserved))
        {
            throw new InvalidDataException("Successor settlement lacks its exact SQLite reservation.");
        }
        OpenAiResponsesResult response = result.Response;
        string identity = "m1s6-successor-" + admission.AttemptId;
        string settlementId = identity + "-settlement";
        if (response.State == ProviderResponseState.Unknown && response.RawResponseBytes is null)
        {
            if (admission.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
            {
                M1Slice6SuccessorV6PersistenceReceipt v6Ambiguous =
                    store.RetainM1Slice6SuccessorV6Ambiguous(admission.OperationId,
                        admission.ReservationId, settlementId, result.CompletedAtUtc);
                reservations.Remove(admission.ReservationId);
                return new("", "", v6Ambiguous.SettlementId, "", 0,
                    v6Ambiguous.UnresolvedNanoUsd, false, false, null, "");
            }
            ProviderBudgetSettlementReceipt ambiguous = accounting.Settle(new(settlementId,
                admission.ReservationId, ProviderBudgetEventKind.RetainedAmbiguous,
                null, null, result.CompletedAtUtc));
            reservations.Remove(admission.ReservationId);
            return new("", "", settlementId, "", ambiguous.Settled.NanoUsd,
                ambiguous.Unresolved.NanoUsd, ambiguous.RetryPermitted, false, null, "");
        }
        ProviderRateLimitFactContract[] rates = response.RateHeaders.Select(item =>
            new ProviderRateLimitFactContract("provider-response-header", item.Name,
                ProviderAvailabilityState.Available, item.Value, item.Value,
                new UtcTimestamp(result.CompletedAtUtc), null)).ToArray();
        string responseId = identity + "-response";
        string usageId = identity + "-usage";
        if (admission.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
        {
            M1Slice6SuccessorV6PersistenceReceipt v6Persisted = store.PersistM1Slice6SuccessorV6Response(new(
                responseId, usageId, identity + "-receipt", identity + "-finalization",
                admission.AuthorizationId, admission.OperationId, admission.ReservationId,
                admission.AttemptId, admission.RequestId, admission.DispatchFenceId,
                response.State, response.HttpStatus ?? 0, response.ReturnedModel,
                response.ReturnedServiceTier, response.ErrorCode, response.RefusalCode,
                response.IncompleteReason, response.Usage, rates, response.RawResponseBytes,
                result.CompletedAtUtc, result.ResponseHeadersBytes, response.ProviderResponseId,
                response.ProviderRequestId, response.Admitted));
            reservations.Remove(admission.ReservationId);
            M1Slice6CampaignSemanticAdmissionReceipt? v6Semantic = null;
            if (structurallyValid)
            {
                ProviderOperationReadModel operation = store.ReadProviderOperation(admission.OperationId);
                if (operation.RawResponseBytes is null || response.RawResponseBytes is null
                    || !operation.RawResponseBytes.AsSpan().SequenceEqual(response.RawResponseBytes)
                    || operation.ResponseId != responseId || operation.UsageEntryId != usageId
                    || operation.SettlementId != v6Persisted.SettlementId || operation.UnresolvedHold)
                { throw new InvalidDataException("Successor-v6 SQLite replay differs from the authoritative response."); }
                try
                {
                    beforeSuccessorSemanticBinding?.Invoke();
                    if (!successorCampaignIdsByAttempt.TryGetValue(admission.AttemptId, out string? campaignId))
                    { throw new InvalidDataException("Successor semantic binding lacks its exact campaign attempt."); }
                    EnsureSuccessorSemanticBinding(authority, admission, responseId,
                        campaignId, result.CompletedAtUtc.AddTicks(2));
                    v6Semantic = M1Slice6CampaignSemanticAdmission.Admit(store, authority, admission,
                        response, result.CompletedAtUtc.AddTicks(3), successorV6: true);
                }
                catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
                {
                    return new(responseId, usageId, v6Persisted.SettlementId, v6Persisted.ReplayEdgeId,
                        v6Persisted.SettledNanoUsd, v6Persisted.UnresolvedNanoUsd, false,
                        true, null, "semantic-admission-failure");
                }
            }
            return new(responseId, usageId, v6Persisted.SettlementId, v6Persisted.ReplayEdgeId,
                v6Persisted.SettledNanoUsd, v6Persisted.UnresolvedNanoUsd, false,
                v6Persisted.ResponsePersisted, v6Semantic, "");
        }
        ProviderSimulationPersistenceReceipt persisted = store.PersistProviderSimulation(new(
            responseId, usageId, identity + "-receipt", identity + "-finalization",
            admission.AuthorizationId, admission.OperationId, admission.ReservationId,
            admission.AttemptId, admission.RequestId, admission.DispatchFenceId,
            response.State, response.HttpStatus ?? 0, response.ReturnedModel,
            response.ReturnedServiceTier, response.ErrorCode, response.RefusalCode,
            response.IncompleteReason, response.Usage, rates, response.RawResponseBytes,
            result.CompletedAtUtc, result.ResponseHeadersBytes, response.ProviderResponseId,
            response.ProviderRequestId, response.Admitted));
        ProviderBudgetSettlementReceipt settled = accounting.Settle(new(settlementId,
            admission.ReservationId, persisted.SettlementKind,
            persisted.SettlementKind is ProviderBudgetEventKind.RetainedPartial
                or ProviderBudgetEventKind.RetainedUnavailable ? null : persisted.UsageEntryId,
            persisted.SettlementKind is ProviderBudgetEventKind.RetainedPartial
                or ProviderBudgetEventKind.RetainedUnavailable ? null : persisted.Actual,
            result.CompletedAtUtc.AddTicks(1)));
        reservations.Remove(admission.ReservationId);
        string replayEdge = "";
        M1Slice6CampaignSemanticAdmissionReceipt? semantic = null;
        if (structurallyValid)
        {
            OpenAiResponsesResult replay = accounting.Replay(new(new OpaqueId(admission.OperationId),
                new OpaqueId(responseId), NetworkPermitted: false));
            ProviderOperationReadModel operation = store.ReadProviderOperation(admission.OperationId);
            if (replay.RawResponseBytes is null || response.RawResponseBytes is null
                || !replay.RawResponseBytes.AsSpan().SequenceEqual(response.RawResponseBytes)
                || operation.ResponseId != responseId || operation.UsageEntryId != usageId
                || operation.SettlementId != settlementId || operation.UnresolvedHold
                || settled.Unresolved != ProviderBudgetVectorContract.Zero
                || settled.RetryPermitted)
            {
                throw new InvalidDataException("Successor SQLite replay or settlement differs from the authoritative response.");
            }
            replayEdge = operation.ReplayEdgeId;
            try
            {
                beforeSuccessorSemanticBinding?.Invoke();
                if (!successorCampaignIdsByAttempt.TryGetValue(admission.AttemptId, out string? campaignId))
                { throw new InvalidDataException("Successor semantic binding lacks its exact campaign attempt."); }
                EnsureSuccessorSemanticBinding(authority, admission, responseId,
                    campaignId, result.CompletedAtUtc.AddTicks(2));
                semantic = M1Slice6CampaignSemanticAdmission.Admit(store, authority, admission,
                    response, result.CompletedAtUtc.AddTicks(3));
            }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException)
            {
                return new(responseId, usageId, settlementId, replayEdge,
                    settled.Settled.NanoUsd, settled.Unresolved.NanoUsd,
                    settled.RetryPermitted, true, null, "semantic-admission-failure");
            }
        }
        if (settled.Settled.NanoUsd > reserved.NanoUsd || settled.RetryPermitted)
        {
            throw new InvalidDataException("Successor settlement exceeded its reservation or enabled retry.");
        }
        return new(responseId, usageId, settlementId, replayEdge, settled.Settled.NanoUsd,
            settled.Unresolved.NanoUsd, settled.RetryPermitted, true, semantic, "");
    }

    private void EnsureSuccessorSemanticBinding(M1Slice6CampaignStageAuthority authority,
        M1Slice6CampaignAccountingAdmission admission, string responseId, string campaignId,
        DateTimeOffset now)
    {
        if (authority.Stage == M1Slice6CampaignStage.Qualification) { return; }
        if (string.IsNullOrWhiteSpace(campaignId))
        { throw new InvalidDataException("Successor semantic binding lacks its exact campaign identity."); }
        string input = M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(authority.CanonicalRequest);
        string ownerKind;
        string ownerId;
        string semanticOperation;
        string semanticAuthorization;
        string stage;
        if (authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction)
        {
            SourceClaimExecutionInput source = M1Slice6CampaignV2InputAdapter.ReadSourceClaim(input);
            ownerKind = "evidence-acquisition-run";
            ownerId = source.AcquisitionRunId;
            semanticOperation = source.OperationId;
            semanticAuthorization = source.HostAuthorizationId;
            stage = "source-claim-extraction";
        }
        else
        {
            CandidateInvestigationExecutionInput candidate =
                M1Slice6CampaignV2InputAdapter.ReadCandidate(input).ProductInput;
            ownerKind = "analysis-run";
            ownerId = candidate.AnalysisRunId;
            semanticOperation = candidate.OperationId;
            semanticAuthorization = candidate.HostAuthorizationId;
            stage = "candidate-investigation";
        }
        bool successorV6 = admission.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal);
        string bindingTable = successorV6
            ? "m1_slice6_successor_v6_semantic_response_bindings"
            : "m1_slice6_successor_semantic_response_bindings";
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            INSERT INTO {bindingTable}(
              binding_id,campaign_id,stage,semantic_authorization_id,semantic_operation_id,
              transport_authorization_id,transport_operation_id,provider_attempt_id,request_id,
              dispatch_fence_id,semantic_response_record_id,transport_response_record_id,
              owner_kind,owner_id,created_at)
            VALUES($binding,$campaign,$stage,$semantic_authorization,$semantic_operation,
              $transport_authorization,$transport_operation,$attempt,$request,$fence,$semantic_response,$response,
              $owner_kind,$owner,$now)
            ON CONFLICT(binding_id) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$binding", "m1s6-successor-semantic-binding-" + admission.AttemptId);
        command.Parameters.AddWithValue("$campaign", campaignId);
        command.Parameters.AddWithValue("$stage", stage);
        command.Parameters.AddWithValue("$semantic_authorization", semanticAuthorization);
        command.Parameters.AddWithValue("$semantic_operation", semanticOperation);
        command.Parameters.AddWithValue("$transport_authorization", admission.AuthorizationId);
        command.Parameters.AddWithValue("$transport_operation", admission.OperationId);
        command.Parameters.AddWithValue("$attempt", admission.AttemptId);
        command.Parameters.AddWithValue("$request", admission.RequestId);
        command.Parameters.AddWithValue("$fence", admission.DispatchFenceId);
        command.Parameters.AddWithValue("$semantic_response",
            authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
                ? "m1s6-campaign-stage-2-response" : "m1s6-campaign-stage-3-response");
        command.Parameters.AddWithValue("$response", responseId);
        command.Parameters.AddWithValue("$owner_kind", ownerKind);
        command.Parameters.AddWithValue("$owner", ownerId);
        command.Parameters.AddWithValue("$now", now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        _ = command.ExecuteNonQuery();
        command.Parameters.Clear();
        command.CommandText =
            "SELECT campaign_id,stage,semantic_authorization_id,semantic_operation_id,transport_authorization_id,"
            + "transport_operation_id,provider_attempt_id,request_id,dispatch_fence_id,semantic_response_record_id,"
            + $"transport_response_record_id,owner_kind,owner_id FROM {bindingTable} "
            + "WHERE binding_id=$binding;";
        command.Parameters.AddWithValue("$binding", "m1s6-successor-semantic-binding-" + admission.AttemptId);
        using SqliteDataReader reader = command.ExecuteReader();
        string semanticResponse = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? "m1s6-campaign-stage-2-response" : "m1s6-campaign-stage-3-response";
        string[] exact = [campaignId, stage, semanticAuthorization, semanticOperation,
            admission.AuthorizationId, admission.OperationId, admission.AttemptId, admission.RequestId,
            admission.DispatchFenceId, semanticResponse, responseId, ownerKind, ownerId];
        if (!reader.Read() || Enumerable.Range(0, exact.Length).Any(index => reader.GetString(index) != exact[index])
            || reader.Read())
        { throw new InvalidDataException("The successor semantic response binding is absent or stale."); }
    }

    internal M1Slice6SuccessorAccountingPersistence RetainSuccessorAmbiguousStart(
        M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now)
    {
        if (!reservations.Remove(admission.ReservationId,
                out ProviderBudgetVectorContract? reserved))
        { throw new InvalidDataException("Ambiguous successor start lacks its exact reservation."); }
        string settlementId = "m1s6-successor-" + admission.AttemptId + "-settlement";
        if (admission.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
        {
            M1Slice6SuccessorV6PersistenceReceipt v6Retained = store.RetainM1Slice6SuccessorV6Ambiguous(
                admission.OperationId, admission.ReservationId, settlementId, now);
            return new("", "", v6Retained.SettlementId, "", 0, v6Retained.UnresolvedNanoUsd,
                false, false, null, "");
        }
        ProviderBudgetSettlementReceipt retained = accounting.Settle(new(settlementId,
            admission.ReservationId, ProviderBudgetEventKind.RetainedAmbiguous,
            null, null, now));
        if (retained.Unresolved != reserved || retained.Settled != ProviderBudgetVectorContract.Zero
            || retained.RetryPermitted)
        { throw new InvalidDataException("Ambiguous successor start was not retained as a full no-retry hold."); }
        return new("", "", settlementId, "", 0, retained.Unresolved.NanoUsd,
            false, false, null, "");
    }

    internal M1Slice6SuccessorAccountingPersistence RecoverSuccessorV6AmbiguousStart(
        string operationId, string authorizationId, string attemptId, string requestId,
        string reservationId, string dispatchFenceId, DateTimeOffset now)
    {
        if (!operationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
        { throw new InvalidDataException("Started-failure recovery requires a successor-v6 operation."); }
        M1Slice6SuccessorV6ReservationReadModel reservation =
            store.ReadM1Slice6SuccessorV6Reservation(operationId, authorizationId, attemptId,
                requestId, reservationId, dispatchFenceId);
        string settlementId = "m1s6-successor-" + attemptId + "-settlement";
        M1Slice6SuccessorV6PersistenceReceipt retained =
            store.RetainM1Slice6SuccessorV6Ambiguous(operationId, reservationId, settlementId, now);
        if (retained.SettledNanoUsd != 0
            || retained.UnresolvedNanoUsd != reservation.ReservedNanoUsd
            || retained.ResponsePersisted)
        {
            throw new InvalidDataException(
                "Started-failure recovery did not retain the exact successor-v6 reservation.");
        }
        return new("", "", retained.SettlementId, "", 0, retained.UnresolvedNanoUsd,
            false, false, null, "post-effect-settlement-recovered");
    }

    internal M1Slice6SuccessorAccountingPersistence ConvergeSuccessorPersistenceFailure(
        M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now)
    {
        try
        {
            ProviderOperationReadModel operation = store.ReadProviderOperation(admission.OperationId);
            reservations.Remove(admission.ReservationId);
            if (operation.State == ProviderOperationState.Settled && !operation.UnresolvedHold
                && operation.ResponseId.Length > 0 && operation.UsageEntryId is not null
                && operation.SettlementId is not null)
            {
                return new(operation.ResponseId, operation.UsageEntryId, operation.SettlementId,
                    operation.ReplayEdgeId, operation.CalculatedNanoUsd, 0, false, true, null,
                    "persistence-recovery-required");
            }
            return new(operation.ResponseId, operation.UsageEntryId ?? "",
                operation.SettlementId ?? "", operation.ReplayEdgeId, 0,
                admission.ReservedNanoUsd, false, operation.RawResponseBytes is not null, null,
                "persistence-recovery-required");
        }
        catch (KeyNotFoundException)
        {
            M1Slice6SuccessorAccountingPersistence held = RetainSuccessorAmbiguousStart(admission, now);
            return held with { SemanticFailureCode = "persistence-recovery-required" };
        }
    }

    internal M1Slice6SuccessorAccountingPersistence RecoverSuccessorSemantic(
        M1Slice6CampaignStageAuthority authority, string transportOperationId,
        string transportAuthorizationId, string attemptId, string requestId,
        string reservationId, string dispatchFenceId, string responseId, string campaignId,
        byte[] rawResponseBytes, byte[] responseHeadersBytes, DateTimeOffset now)
    {
        bool successorV6 = transportOperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal);
        OpenAiResponsesResult retainedReplay = successorV6
            ? OpenAiStagedResponseEnvelope.ReplaySuccessorV6(rawResponseBytes, responseHeadersBytes, requestId)
            : OpenAiStagedResponseEnvelope.Replay(rawResponseBytes, responseHeadersBytes, requestId);
        M1Slice6CampaignAccountingAdmission admission = RecoveryAdmission(transportOperationId,
            transportAuthorizationId, attemptId, requestId, reservationId, dispatchFenceId, now,
            out ProviderBudgetVectorContract reserved);
        ProviderOperationReadModel operation;
        try { operation = store.ReadProviderOperation(transportOperationId); }
        catch (KeyNotFoundException)
        {
            using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = successorV6
                ? "SELECT COUNT(*) FROM m1_slice6_successor_v6_responses WHERE operation_id=$operation;"
                : "SELECT COUNT(*) FROM provider_responses WHERE operation_id=$operation;";
            command.Parameters.AddWithValue("$operation", transportOperationId);
            bool responseExists = Convert.ToInt64(command.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture) == 1;
            if (!responseExists)
            {
                reservations[reservationId] = reserved;
                M1Slice6CampaignStageBoundaryResult boundary = new(retainedReplay,
                    new(profileId, generationId, credentialTargetFingerprintSha256, 1, 1, 0, 0, "success", "released"),
                    authority.CanonicalRequestSha256, authority.SafetyIdentifierProjection,
                    retainedReplay.DnsResolutionCount, responseHeadersBytes, [], [], now);
                M1Slice6SuccessorAccountingPersistence persisted = PersistSuccessorAttempt(
                    admission, authority, boundary, structurallyValid: true);
                if (persisted.Semantic is not null) { return persisted; }
            }
            else if (!successorV6)
            {
                command.Parameters.Clear();
                command.CommandText =
                    "SELECT usage_entry_id,dispatch_count,input_tokens,output_tokens,total_tokens,reasoning_tokens,"
                    + "cache_read_tokens,cache_write_tokens,priced_tool_calls,calculated_nano_usd "
                    + "FROM provider_usage_entries WHERE operation_id=$operation AND availability='available';";
                command.Parameters.AddWithValue("$operation", transportOperationId);
                using SqliteDataReader usage = command.ExecuteReader();
                if (!usage.Read()) { throw new InvalidDataException("Recovery found a response without exact available usage."); }
                string usageId = usage.GetString(0);
                ProviderBudgetVectorContract actual = new(usage.GetInt64(1), usage.GetInt64(2), usage.GetInt64(3),
                    usage.GetInt64(4), usage.GetInt64(5), usage.GetInt64(6), usage.GetInt64(7),
                    usage.GetInt64(8), usage.GetInt64(9));
                if (usage.Read()) { throw new InvalidDataException("Recovery found ambiguous usage rows."); }
                usage.Close();
                _ = accounting.Settle(new("m1s6-successor-" + attemptId + "-settlement",
                    reservationId, ProviderBudgetEventKind.SettledComplete, usageId, actual, now));
                reservations.Remove(reservationId);
            }
            else
            {
                throw new InvalidDataException(
                    "Successor-v6 recovery found response bytes without atomic terminal accounting.");
            }
            operation = store.ReadProviderOperation(transportOperationId);
        }
        if (operation.AuthorizationId != transportAuthorizationId || operation.ResponseId != responseId
            || operation.State != ProviderOperationState.Settled || operation.UnresolvedHold
            || operation.RawResponseBytes is null || operation.ResponseHeadersBytes is null
            || operation.UsageEntryId is null || operation.SettlementId is null)
        { throw new InvalidDataException("Semantic recovery lacks an exact settled retained transport response."); }
        OpenAiResponsesResult replay = successorV6
            ? OpenAiStagedResponseEnvelope.ReplaySuccessorV6(operation.RawResponseBytes,
                operation.ResponseHeadersBytes, requestId)
            : accounting.Replay(new(new OpaqueId(transportOperationId),
                new OpaqueId(responseId), NetworkPermitted: false));
        if (authority.Stage != M1Slice6CampaignStage.Qualification)
        {
            string input = M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(authority.CanonicalRequest);
            string semanticOperation;
            string semanticAuthorization;
            if (authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction)
            {
                SourceClaimExecutionInput source = M1Slice6CampaignV2InputAdapter.ReadSourceClaim(input);
                semanticOperation = source.OperationId;
                semanticAuthorization = source.HostAuthorizationId;
            }
            else
            {
                CandidateInvestigationExecutionInput candidate =
                    M1Slice6CampaignV2InputAdapter.ReadCandidate(input).ProductInput;
                semanticOperation = candidate.OperationId;
                semanticAuthorization = candidate.HostAuthorizationId;
            }
            admission = admission with
            { SemanticOperationId = semanticOperation, SemanticAuthorizationId = semanticAuthorization };
        }
        M1Slice6CampaignSemanticAdmission.PreparePrerequisites(store, authority, now);
        EnsureSuccessorSemanticBinding(authority, admission, responseId, campaignId, now.AddTicks(1));
        M1Slice6CampaignSemanticAdmissionReceipt semantic = M1Slice6CampaignSemanticAdmission.Admit(
            store, authority, admission, replay, now.AddTicks(2), successorV6);
        return new(responseId, operation.UsageEntryId, operation.SettlementId,
            operation.ReplayEdgeId, Required(replay.Usage.CalculatedNanoUsd, "recovered calculated cost"),
            0, false, true, semantic, "");
    }

    private M1Slice6CampaignAccountingAdmission RecoveryAdmission(string operationId,
        string authorizationId, string attemptId, string requestId, string reservationId,
        string dispatchFenceId, DateTimeOffset now, out ProviderBudgetVectorContract reserved)
    {
        if (operationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal))
        {
            M1Slice6SuccessorV6ReservationReadModel v6 = store.ReadM1Slice6SuccessorV6Reservation(
                operationId, authorizationId, attemptId, requestId, reservationId, dispatchFenceId);
            reserved = new(1, v6.MaximumInputTokens, v6.MaximumOutputTokens,
                checked(v6.MaximumInputTokens + v6.MaximumOutputTokens), v6.MaximumOutputTokens,
                0, 0, 0, v6.ReservedNanoUsd);
            return new(authorizationId, operationId, attemptId, requestId, reservationId,
                dispatchFenceId, v6.CoordinatorFencingEpoch, now, v6.DeadlineUtc,
                accountIdentityId, billingScopeIdentityId, ReservedNanoUsd: reserved.NanoUsd);
        }
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT r.reserved_dispatch_count,r.reserved_input_tokens,r.reserved_output_tokens,"
            + "r.reserved_reasoning_tokens,r.reserved_cache_read_tokens,r.reserved_cache_write_tokens,"
            + "r.reserved_priced_tool_calls,r.maximum_nano_usd,a.coordinator_fencing_epoch,a.dispatch_deadline_utc "
            + "FROM provider_reservations r JOIN provider_operation_authorizations a ON a.operation_id=r.operation_id "
            + "WHERE r.reservation_id=$reservation AND r.operation_id=$operation AND r.provider_attempt_id=$attempt "
            + "AND r.request_id=$request AND a.authorization_id=$authorization;";
        command.Parameters.AddWithValue("$reservation", reservationId);
        command.Parameters.AddWithValue("$operation", operationId);
        command.Parameters.AddWithValue("$attempt", attemptId);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$authorization", authorizationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) { throw new InvalidDataException("Recovery reservation identity is absent."); }
        reserved = new(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2),
            checked(reader.GetInt64(1) + reader.GetInt64(2)), reader.GetInt64(3), reader.GetInt64(4),
            reader.GetInt64(5), reader.GetInt64(6), reader.GetInt64(7));
        long epoch = reader.GetInt64(8);
        DateTimeOffset deadline = DateTimeOffset.Parse(reader.GetString(9),
            System.Globalization.CultureInfo.InvariantCulture);
        if (reader.Read()) { throw new InvalidDataException("Recovery reservation identity is ambiguous."); }
        return new(authorizationId, operationId, attemptId, requestId, reservationId,
            dispatchFenceId, epoch, now, deadline, accountIdentityId, billingScopeIdentityId,
            ReservedNanoUsd: reserved.NanoUsd);
    }

    internal void ValidateSuccessorC3Attempt(M1Slice6CampaignStage stage,
        string operationId, string authorizationId, string responseId, string usageEntryId,
        string settlementId, string replayEdgeId, string rawResponseSha256,
        M1Slice6CampaignSemanticProvenance provenance)
    {
        ProviderOperationReadModel operation = store.ReadProviderOperation(operationId);
        if (operation.State != ProviderOperationState.Settled || operation.UnresolvedHold
            || operation.AuthorizationId != authorizationId || operation.ResponseId != responseId
            || operation.UsageEntryId != usageEntryId || operation.SettlementId != settlementId
            || operation.ReplayEdgeId != replayEdgeId || operation.RawResponseBytes is null
            || Convert.ToHexStringLower(SHA256.HashData(operation.RawResponseBytes)) != rawResponseSha256)
        { throw new InvalidDataException("C3 provider accounting does not bind one exact settled retained response."); }
        OpenAiResponsesResult replay = operationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal)
            ? OpenAiStagedResponseEnvelope.ReplaySuccessorV6(operation.RawResponseBytes,
                operation.ResponseHeadersBytes ?? [], operation.ClientRequestId)
            : accounting.Replay(new(new OpaqueId(operationId),
                new OpaqueId(responseId), NetworkPermitted: false));
        if (replay.RawResponseBytes is null
            || Convert.ToHexStringLower(SHA256.HashData(replay.RawResponseBytes)) != rawResponseSha256)
        { throw new InvalidDataException("C3 effect-free retained response replay changed bytes."); }
        if (stage == M1Slice6CampaignStage.SourceClaimExtraction)
        {
            SourceClaimApplicationReadModel application = store
                .ReadSourceClaimApplicationLinks(provenance.SourceAcquisitionId)
                .Single(link => link.ApplicationLinkId == provenance.SourceApplicationLinkId);
            if (application.AdmissionId != provenance.SourceAdmissionId
                || application.AdmittedArtifactId != provenance.AdmittedArtifactId)
            { throw new InvalidDataException("C3 WP10 application-chain provenance changed."); }
            using SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM m1_slice6_successor_all_semantic_response_bindings b "
                + "JOIN provider_semantic_admissions a ON a.operation_id=b.semantic_operation_id "
                + "AND a.response_record_id=b.semantic_response_record_id "
                + "JOIN evidence_acquisition_application_links l ON l.admission_id=a.admission_id "
                + "AND l.acquisition_run_id=a.owner_id AND l.admitted_artifact_id=a.admitted_artifact_id "
                + "WHERE b.transport_operation_id=$operation AND b.transport_response_record_id=$response "
                + "AND a.state='admitted' AND a.admission_id=$admission AND a.admitted_artifact_id=$artifact "
                + "AND l.application_link_id=$application;";
            command.Parameters.AddWithValue("$operation", operationId);
            command.Parameters.AddWithValue("$response", responseId);
            command.Parameters.AddWithValue("$admission", provenance.SourceAdmissionId);
            command.Parameters.AddWithValue("$artifact", provenance.AdmittedArtifactId);
            command.Parameters.AddWithValue("$application", provenance.SourceApplicationLinkId);
            if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            { throw new InvalidDataException("C3 WP10 transport-to-semantic-to-application chain changed."); }
        }
        else if (stage == M1Slice6CampaignStage.CandidateInvestigation)
        {
            using SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT semantic_operation_id,owner_id FROM m1_slice6_successor_all_semantic_response_bindings "
                + "WHERE transport_operation_id=$operation AND transport_response_record_id=$response;";
            command.Parameters.AddWithValue("$operation", operationId);
            command.Parameters.AddWithValue("$response", responseId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read()) { throw new InvalidDataException("C3 WP11 semantic bridge is absent."); }
            string semanticOperation = reader.GetString(0);
            string ownerId = reader.GetString(1);
            if (reader.Read()) { throw new InvalidDataException("C3 WP11 semantic bridge is ambiguous."); }
            reader.Close();
            IReadOnlyList<CandidateInvestigationOutcomeIdentityReadModel> outcomes =
                store.ReadCandidateInvestigationOutcomesForOperation(ownerId, semanticOperation);
            if (outcomes.Count != 2
                || outcomes.Count(item => item.Disposition is "accepted" or "accepted-conditional") != 1
                || outcomes.Count(item => item.Disposition == "empty-abstained") != 1
                || outcomes.Any(item => item.ReplayState != "retained-response"))
            { throw new InvalidDataException("C3 WP11 positive/negative retained consumption changed."); }
            command.Parameters.Clear();
            command.CommandText =
                "SELECT COUNT(*) FROM candidate_investigation_outcomes o "
                + "JOIN candidate_evidence_authority e ON e.outcome_id=o.outcome_id "
                + "WHERE o.owner_id=$owner AND o.operation_id=$semantic AND o.candidate_id=$candidate "
                + "AND o.hypothesis_id=$hypothesis AND o.disposition IN ('accepted','accepted-conditional') "
                + "AND e.root_kind='persisted-source-claim-application' "
                + "AND e.source_acquisition_id=$acquisition AND e.source_admission_id=$admission "
                + "AND e.admitted_artifact_id=$artifact AND e.source_application_link_id=$application;";
            command.Parameters.AddWithValue("$owner", ownerId);
            command.Parameters.AddWithValue("$semantic", semanticOperation);
            command.Parameters.AddWithValue("$candidate", provenance.CandidateId);
            command.Parameters.AddWithValue("$hypothesis", provenance.HypothesisId);
            command.Parameters.AddWithValue("$acquisition", provenance.SourceAcquisitionId);
            command.Parameters.AddWithValue("$admission", provenance.SourceAdmissionId);
            command.Parameters.AddWithValue("$artifact", provenance.AdmittedArtifactId);
            command.Parameters.AddWithValue("$application", provenance.SourceApplicationLinkId);
            if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            { throw new InvalidDataException("C3 WP11 positive outcome no longer consumes exact WP10 provenance."); }
            command.Parameters.Clear();
            command.CommandText =
                "SELECT COUNT(*) FROM candidate_investigation_outcomes o "
                + "JOIN candidate_evidence_authority e ON e.outcome_id=o.outcome_id "
                + "WHERE o.owner_id=$owner AND o.operation_id=$semantic AND o.disposition='empty-abstained' "
                + "AND e.root_kind='frozen-host-evidence' AND e.evidence_root_id IS NOT NULL "
                + "AND e.applicability_record_id IS NOT NULL AND e.source_acquisition_id IS NULL "
                + "AND e.source_admission_id IS NULL AND e.admitted_artifact_id IS NULL "
                + "AND e.source_application_link_id IS NULL;";
            command.Parameters.AddWithValue("$owner", ownerId);
            command.Parameters.AddWithValue("$semantic", semanticOperation);
            if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            { throw new InvalidDataException("C3 WP11 matched negative no longer uses one independent non-source root."); }
        }
    }

    public void Dispose() => store.Dispose();

    public M1Slice6CampaignRecoveredSettlement? TryRecoverKnownSettlement(
        M1Slice6CampaignStage stage, string canonicalRequestSha256)
    {
        string operationId = "m1s6-campaign-stage-" + (int)stage + "-operation";
        ProviderOperationReadModel operation;
        try
        {
            operation = store.ReadProviderOperation(operationId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        if (operation.State != ProviderOperationState.Settled || operation.UnresolvedHold
            || operation.SettlementId is null || operation.RawResponseBytes is null
            || operation.ResponseHeadersBytes is null
            || operation.AuthorizationId != "m1s6-campaign-stage-" + (int)stage + "-authorization"
            || operation.OperationKind != stage switch
            {
                M1Slice6CampaignStage.Qualification => "transport-qualification",
                M1Slice6CampaignStage.SourceClaimExtraction => "source-claim-extraction",
                M1Slice6CampaignStage.CandidateInvestigation => "candidate-investigation",
                _ => string.Empty,
            })
        {
            return null;
        }
        using (SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False"))
        {
            connection.Open();
            using SqliteCommand request = connection.CreateCommand();
            request.CommandText =
                "SELECT canonical_request_fingerprint FROM provider_requests WHERE operation_id=$operation;";
            request.Parameters.AddWithValue("$operation", operationId);
            if (request.ExecuteScalar() is not string retainedRequest
                || retainedRequest != canonicalRequestSha256)
            {
                return null;
            }
        }
        OpenAiResponsesResult replay = accounting.Replay(new(new OpaqueId(operationId),
            new OpaqueId(operation.ResponseId), NetworkPermitted: false));
        return new(
            Required(replay.Usage.InputTokens, "recovered input tokens"),
            Required(replay.Usage.OutputTokens, "recovered output tokens"),
            replay.RawResponseBytes?.LongLength
                ?? throw new InvalidDataException("Recovered settlement omitted raw response bytes."),
            Required(replay.Usage.CalculatedNanoUsd, "recovered calculated cost"));
    }

    private void RequireVerifiedCredential()
    {
        try
        {
            CredentialProfileProjection existing = store.GetCredentialProfile(profileId);
            if (existing.GenerationId != generationId || existing.LifecycleState != "active-verified"
                || existing.VerificationState != "available" || existing.AccountIdentityId != accountIdentityId
                || existing.BillingScopeIdentityId != billingScopeIdentityId)
            {
                throw new InvalidDataException("Authoritative SQLite credential projection is stale.");
            }
            return;
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidDataException(
                "The exact WP9 product-state database has no accepted credential profile; campaign execution cannot fabricate enrollment or verification.",
                exception);
        }
    }

    private void SeedOperationGraph(M1Slice6CampaignStageAuthority authority, string prefix,
        string ownerKind, string ownerId, string operationId, string attemptId, string requestId,
        string authorizationId, long fencingEpoch, DateTimeOffset deadline, DateTimeOffset now,
        string? semanticOperationId = null, string? semanticAuthorizationId = null,
        string? transportRequestFingerprint = null)
    {
        string parentRunId = prefix + "-run";
        string applicationScopeId = prefix + "-application";
        string costAttributionScopeId = prefix + "-cost";
        if (authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction)
        {
            SourceClaimExecutionInput sourceInput = M1Slice6CampaignV2InputAdapter.ReadSourceClaim(
                M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(authority.CanonicalRequest));
            if (sourceInput.AcquisitionRunId != ownerId
                || sourceInput.OperationId != (semanticOperationId ?? operationId)
                || sourceInput.HostAuthorizationId != (semanticAuthorizationId ?? authorizationId))
            {
                throw new InvalidDataException("WP10 source-claim input differs from the authoritative stage identities.");
            }
            parentRunId = sourceInput.ParentAnalysisRunId;
            applicationScopeId = sourceInput.ApplicationScopeId;
            costAttributionScopeId = sourceInput.CostAttributionScopeId;
        }
        else if (authority.Stage == M1Slice6CampaignStage.CandidateInvestigation)
        {
            CandidateInvestigationExecutionInput candidateInput = M1Slice6CampaignV2InputAdapter.ReadCandidate(
                M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(authority.CanonicalRequest)).ProductInput;
            if (candidateInput.AnalysisRunId != ownerId
                || candidateInput.OperationId != (semanticOperationId ?? operationId)
                || candidateInput.HostAuthorizationId != (semanticAuthorizationId ?? authorizationId))
            {
                throw new InvalidDataException("WP11 candidate input differs from the authoritative stage identities.");
            }
            parentRunId = candidateInput.AnalysisRunId;
            applicationScopeId = candidateInput.ApplicationScopeId;
            costAttributionScopeId = candidateInput.CostAttributionScopeId;
        }
        string requestHash = authority.CanonicalRequestSha256;
        string requestFingerprint = transportRequestFingerprint ?? requestHash;
        byte[] settingsBytes = Encoding.UTF8.GetBytes("m1-s6-campaign-settings/" + (int)authority.Stage);
        string settingsHash = Convert.ToHexStringLower(SHA256.HashData(settingsBytes));
        byte[] schemaBytes;
        using (System.Text.Json.JsonDocument request = System.Text.Json.JsonDocument.Parse(authority.CanonicalRequest))
        {
            schemaBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(request.RootElement
                .GetProperty("text").GetProperty("format").GetProperty("schema"));
        }
        string schemaHash = Convert.ToHexStringLower(SHA256.HashData(schemaBytes));
        string operationKind = authority.Operation switch
        {
            ProviderOperationKind.TransportQualification => "transport-qualification",
            ProviderOperationKind.SourceClaimExtraction => "source-claim-extraction",
            ProviderOperationKind.CandidateInvestigation => "candidate-investigation",
            _ => throw new InvalidDataException("Campaign accounting operation kind is not closed."),
        };
        string payloadDirectory = Path.Combine(store.Paths.Payloads, requestHash[..2], requestHash[2..4]);
        Directory.CreateDirectory(payloadDirectory);
        string payloadPath = Path.Combine(payloadDirectory, requestHash);
        if (!File.Exists(payloadPath))
        {
            using FileStream payload = new(payloadPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough);
            payload.Write(authority.CanonicalRequest);
            payload.Flush(flushToDisk: true);
        }
        else if (Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(payloadPath))) != requestHash)
        {
            throw new InvalidDataException("Retained canonical request payload bytes differ from their digest.");
        }
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        string payloadId;
        using (SqliteCommand existingPayload = connection.CreateCommand())
        {
            existingPayload.CommandText = "SELECT payload_id FROM payloads WHERE content_sha256=$sha;";
            existingPayload.Parameters.AddWithValue("$sha", requestHash);
            payloadId = existingPayload.ExecuteScalar() as string ?? prefix + "-payload";
        }
        using SqliteCommand command = connection.CreateCommand();
        string commandRunId = ownerKind == "analysis-run" ? ownerId : prefix + "-run";
        string commandRunPrefix = commandRunId == ownerId
            ? ownerId.EndsWith("-run", StringComparison.Ordinal) ? ownerId[..^4] : ownerId
            : prefix;
        string ownerPrefix = ownerId.EndsWith("-run", StringComparison.Ordinal)
            ? ownerId[..^4] : ownerId;
        string commandParentPrefix = commandRunId == parentRunId
            ? commandRunPrefix
            : parentRunId.EndsWith("-run", StringComparison.Ordinal) ? parentRunId[..^4] : parentRunId;
        command.Parameters.AddWithValue("$prefix", prefix);
        command.Parameters.AddWithValue("$runPrefix", commandRunPrefix);
        command.Parameters.AddWithValue("$ownerPrefix", ownerPrefix);
        command.Parameters.AddWithValue("$run", commandRunId);
        command.Parameters.AddWithValue("$parentRun", parentRunId);
        command.Parameters.AddWithValue("$parentPrefix", commandParentPrefix);
        command.Parameters.AddWithValue("$applicationScope", applicationScopeId);
        command.Parameters.AddWithValue("$costScope", costAttributionScopeId);
        command.Parameters.AddWithValue("$ownerKind", ownerKind);
        command.Parameters.AddWithValue("$owner", ownerId);
        command.Parameters.AddWithValue("$operation", operationId);
        command.Parameters.AddWithValue("$attempt", attemptId);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$authorization", authorizationId);
        command.Parameters.AddWithValue("$profile", profileId);
        command.Parameters.AddWithValue("$generation", generationId);
        command.Parameters.AddWithValue("$account", accountIdentityId);
        command.Parameters.AddWithValue("$billing", billingScopeIdentityId);
        command.Parameters.AddWithValue("$capability", M1ProviderCatalog.Capability.Identity.Value);
        command.Parameters.AddWithValue("$price", M1ProviderCatalog.Price.Identity.Value);
        command.Parameters.AddWithValue("$requestHash", requestHash);
        command.Parameters.AddWithValue("$requestFingerprint", requestFingerprint);
        command.Parameters.AddWithValue("$payloadId", payloadId);
        command.Parameters.AddWithValue("$settingsHash", settingsHash);
        command.Parameters.AddWithValue("$schemaHash", schemaHash);
        command.Parameters.AddWithValue("$promptId", authority.Stage == M1Slice6CampaignStage.CandidateInvestigation
            ? CandidateInvestigationPromptV1.Id
            : authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
                ? SourceClaimPromptV1.Id : prefix + "-prompt");
        command.Parameters.AddWithValue("$promptFingerprint", authority.Stage == M1Slice6CampaignStage.CandidateInvestigation
            ? CandidateInvestigationPromptV1.Fingerprint
            : authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
                ? SourceClaimPromptV1.Fingerprint : requestHash);
        command.Parameters.AddWithValue("$requestBytes", authority.CanonicalRequest.LongLength);
        command.Parameters.AddWithValue("$operationKind", operationKind);
        command.Parameters.AddWithValue("$maxRequest", authority.Limits.MaximumRequestBytes);
        command.Parameters.AddWithValue("$maxInput", authority.Limits.MaximumInputTokens);
        command.Parameters.AddWithValue("$maxOutput", authority.Limits.MaximumOutputTokens);
        command.Parameters.AddWithValue("$maxRaw", authority.Limits.MaximumRawResponseBytes);
        command.Parameters.AddWithValue("$maxCost", authority.Limits.MaximumNanoUsd);
        command.Parameters.AddWithValue("$deadlineMs", authority.Limits.DeadlineMilliseconds);
        command.Parameters.AddWithValue("$deadline", deadline.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$epoch", fencingEpoch);
        command.Parameters.AddWithValue("$now", now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            INSERT INTO runs VALUES($run,$runPrefix || '-install',$runPrefix || '-context',$runPrefix || '-config',$runPrefix || '-manifest','Running',0,1,1,$now,$now)
              ON CONFLICT(run_id) DO NOTHING;
            INSERT INTO runs
            SELECT $parentRun,$parentPrefix || '-install',$parentPrefix || '-context',$parentPrefix || '-config',$parentPrefix || '-manifest','Running',0,1,1,$now,$now
              WHERE $parentRun <> $run
              ON CONFLICT(run_id) DO NOTHING;
            INSERT INTO job_nodes VALUES($prefix || '-job',$run,NULL,'provider','Running',0,$now,$now);
            INSERT INTO durable_commands VALUES($prefix || '-command','provider',$run,0,'recorded','running',NULL,$now,NULL,NULL);
            INSERT INTO evidence_acquisition_runs
              SELECT $owner,$ownerPrefix || '-install',$ownerPrefix || '-context',$ownerPrefix || '-config',$ownerPrefix || '-manifest',
                $parentRun,$applicationScope,$costScope,'running',$now
              WHERE $ownerKind='evidence-acquisition-run'
              ON CONFLICT(acquisition_run_id) DO NOTHING;
            INSERT INTO evidence_acquisition_job_nodes
              SELECT $prefix || '-acquisition-job',$owner,'provider','running',$now
              WHERE $ownerKind='evidence-acquisition-run';
            INSERT INTO evidence_acquisition_commands
              SELECT $prefix || '-command',$owner,'provider-operation',$now,'recorded'
              WHERE $ownerKind='evidence-acquisition-run';
            INSERT INTO evidence_acquisition_parent_links
              SELECT $prefix || '-parent',$owner,$parentRun,'initiated-by',NULL,$now
              WHERE $ownerKind='evidence-acquisition-run';
            INSERT INTO provider_command_bindings VALUES($prefix || '-command',$ownerKind,$owner,$now);
            INSERT INTO provider_effective_scan_configurations_v2 VALUES(
              $prefix || '-effective',$ownerPrefix || '-config','abababababababababababababababababababababababababababababababab',
              'asserted-retained-v1-identity',$profile,$generation,'gpt-5.6-sol','medium','current_turn','standard',0,
              'default',0,0,'none',0,'disabled','explicit',0,0,$maxRequest,$maxInput,$maxOutput,$maxRaw,1,$maxCost,
              $deadlineMs,'["hosted-search","nexus","loot"]',$now);
            INSERT INTO payloads VALUES($payloadId,$requestHash,$requestBytes,'application/json','retained',
              'payloads/' || substr($requestHash,1,2) || '/' || substr($requestHash,3,2) || '/' || $requestHash,$now)
              ON CONFLICT(content_sha256) DO NOTHING;
            INSERT INTO provider_operation_blocks(
              operation_id,owner_kind,owner_id,job_node_id,command_id,requested_at,confirmed_at,
              installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
              profile_id,generation_id,revocation_epoch,operation_kind,capability_snapshot_id,price_snapshot_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,
              canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,coordinator_fencing_epoch,state,recorded_at)
            VALUES($operation,$ownerKind,$owner,
              CASE WHEN $ownerKind='evidence-acquisition-run' THEN $prefix || '-acquisition-job' ELSE $prefix || '-job' END,
              $prefix || '-command',$now,$now,
              $ownerPrefix || '-install',$ownerPrefix || '-context',$prefix || '-effective',$ownerPrefix || '-manifest',$profile,$generation,0,
              $operationKind,$capability,$price,$promptId,$promptFingerprint,$prefix || '-schema',$schemaHash,
              $requestFingerprint,$payloadId,$requestHash,$requestBytes,$settingsHash,
              'unresolved-openai-responses-framing','authority-required','authority-required',$maxRequest,$maxInput,$maxOutput,$maxRaw,1,$maxCost,
              $deadlineMs,$deadline,$epoch,'input-bound-blocked',$now);
            INSERT INTO provider_operation_projection VALUES($operation,'input-bound-blocked',0,0,0,1,$now);
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,evidence_acquisition_run_id,job_node_id,
              command_id,requested_at,profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,
              analysis_context_id,effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,
              output_schema_id,output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,
              capability_snapshot_id,price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,
              input_bound_proof_status,coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,
              maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,
              deadline_milliseconds,dispatch_deadline_utc,confirmed_at)
            SELECT $authorization,operation_id,owner_kind,owner_id,
              CASE WHEN $ownerKind='analysis-run' THEN $owner ELSE NULL END,
              CASE WHEN $ownerKind='evidence-acquisition-run' THEN $owner ELSE NULL END,
              job_node_id,command_id,requested_at,profile_id,generation_id,revocation_epoch,operation_kind,
              installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,
              canonical_request_fingerprint,capability_snapshot_id,price_snapshot_id,settings_fingerprint,
              'openai-responses-o200k-byte-envelope','v2','proved',coordinator_fencing_epoch,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,confirmed_at
            FROM provider_operation_blocks WHERE operation_id=$operation;
            INSERT INTO provider_operation_attempts VALUES($attempt,$operation,1,'proposed',$epoch,$now);
            INSERT INTO provider_requests(
              request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
              canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
              input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
            VALUES($request,$request,$operation,$attempt,$requestFingerprint,$requestHash,$settingsHash,$schemaHash,
              'openai-responses-o200k-byte-envelope','v2','proved',$payloadId,$requestHash,$requestBytes,$now);
            """;
        _ = command.ExecuteNonQuery();
        using SqliteCommand validateRuns = connection.CreateCommand();
        validateRuns.CommandText =
            """
            SELECT COUNT(*) FROM runs
            WHERE run_id=$run AND installation_snapshot_id=$runPrefix || '-install'
              AND analysis_context_id=$runPrefix || '-context'
              AND effective_scan_configuration_id=$runPrefix || '-config'
              AND resolved_input_manifest_id=$runPrefix || '-manifest'
              AND lifecycle_state='Running' AND lifecycle_generation=0
              AND coordinator_fencing_epoch=1 AND durable_sequence=1;
            SELECT COUNT(*) FROM runs
            WHERE run_id=$parentRun AND installation_snapshot_id=$parentPrefix || '-install'
              AND analysis_context_id=$parentPrefix || '-context'
              AND effective_scan_configuration_id=$parentPrefix || '-config'
              AND resolved_input_manifest_id=$parentPrefix || '-manifest'
              AND lifecycle_state='Running' AND lifecycle_generation=0
              AND coordinator_fencing_epoch=1 AND durable_sequence=1;
            """;
        validateRuns.Parameters.AddWithValue("$run", commandRunId);
        validateRuns.Parameters.AddWithValue("$prefix", prefix);
        validateRuns.Parameters.AddWithValue("$runPrefix", commandRunPrefix);
        validateRuns.Parameters.AddWithValue("$parentRun", parentRunId);
        validateRuns.Parameters.AddWithValue("$parentPrefix", commandParentPrefix);
        using SqliteDataReader runReader = validateRuns.ExecuteReader();
        long runCount = runReader.Read() ? runReader.GetInt64(0) : 0;
        _ = runReader.NextResult();
        long parentRunCount = runReader.Read() ? runReader.GetInt64(0) : 0;
        if (runCount != 1 || parentRunCount != 1)
        {
            throw new InvalidDataException("Campaign accounting found a conflicting preexisting run identity.");
        }
    }

    private static long Required(ProviderQuantityContract quantity, string name) =>
        quantity.Availability == ProviderAvailabilityState.Available && quantity.Value is >= 0
            ? quantity.Value.Value : throw new InvalidDataException("Exact " + name + " are unavailable.");
}
