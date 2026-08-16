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
    private readonly string accountIdentityId;
    private readonly string billingScopeIdentityId;
    private readonly Dictionary<string, ProviderBudgetVectorContract> reservations = new(StringComparer.Ordinal);

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
        System.Text.Json.JsonElement intent = root.GetProperty("provider_intent");
        profileId = profile.GetProperty("access_profile_id").GetString()!;
        generationId = profile.GetProperty("generation_id").GetString()!;
        accountIdentityId = intent.GetProperty("account_identity_id").GetString()!;
        billingScopeIdentityId = intent.GetProperty("billing_scope_identity_id").GetString()!;
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
        string operationId = prefix + "-operation";
        string attemptId = prefix + "-attempt-1";
        string requestId = prefix + "-request";
        string reservationId = prefix + "-reservation-1";
        string authorizationId = prefix + "-authorization";
        string dispatchFenceId = prefix + "-dispatch-1";
        string ownerKind = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? "evidence-acquisition-run" : "analysis-run";
        string ownerId = authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? prefix + "-acquisition" : prefix + "-run";
        // Coordinator leases are process-clock authority, while the retained stage clock is
        // an independently validated campaign timestamp. Never project a rehearsed/event time
        // into the live fencing lease comparison performed by AuthoritativeStore.
        CoordinatorAuthority coordinator = store.AcquireCoordinatorAuthorityAfterProcessExclusion(
            "m1-s6-finite-campaign", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        DateTimeOffset deadline = now.AddMilliseconds(authority.Limits.DeadlineMilliseconds);
        M1Slice6CampaignSemanticAdmission.PreparePrerequisites(store, authority, now);
        SeedOperationGraph(authority, prefix, ownerKind, ownerId, operationId, attemptId,
            requestId, authorizationId, coordinator.FencingEpoch, deadline, now);
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
            SourceClaimExecutionInput sourceInput = System.Text.Json.JsonSerializer.Deserialize<SourceClaimExecutionInput>(
                M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(authority.CanonicalRequest),
                SourceClaimContextMinimizer.JsonOptions)
                ?? throw new InvalidDataException("WP10 canonical request has no exact parent analysis run.");
            ids.Add(sourceInput.ParentAnalysisRunId);
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

    public void RecordPossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now)
    {
        if (!reservations.ContainsKey(admission.ReservationId) || now >= admission.DeadlineUtc)
        {
            throw new InvalidDataException("Authoritative transport-start persistence is expired or unreserved.");
        }
        store.RecordProviderTransportStart(admission.OperationId, admission.AttemptId,
            admission.RequestId, admission.DispatchFenceId, ambiguous: false, now);
    }

    public void ReleaseBeforePossibleStart(M1Slice6CampaignAccountingAdmission admission, DateTimeOffset now)
    {
        if (!reservations.Remove(admission.ReservationId, out ProviderBudgetVectorContract? reserved))
        {
            throw new InvalidDataException("Authoritative prestart release lacks its exact reservation.");
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
        string authorizationId, long fencingEpoch, DateTimeOffset deadline, DateTimeOffset now)
    {
        string parentRunId = prefix + "-run";
        string applicationScopeId = prefix + "-application";
        string costAttributionScopeId = prefix + "-cost";
        if (authority.Stage == M1Slice6CampaignStage.SourceClaimExtraction)
        {
            SourceClaimExecutionInput sourceInput = System.Text.Json.JsonSerializer.Deserialize<SourceClaimExecutionInput>(
                M1Slice6CampaignSemanticAdmission.ExtractUntrustedInput(authority.CanonicalRequest),
                SourceClaimContextMinimizer.JsonOptions)
                ?? throw new InvalidDataException("WP10 canonical request has no exact source-claim input.");
            if (sourceInput.AcquisitionRunId != ownerId || sourceInput.OperationId != operationId
                || sourceInput.HostAuthorizationId != authorizationId)
            {
                throw new InvalidDataException("WP10 source-claim input differs from the authoritative stage identities.");
            }
            parentRunId = sourceInput.ParentAnalysisRunId;
            applicationScopeId = sourceInput.ApplicationScopeId;
            costAttributionScopeId = sourceInput.CostAttributionScopeId;
        }
        string requestHash = authority.CanonicalRequestSha256;
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
        using (FileStream payload = new(payloadPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough))
        {
            payload.Write(authority.CanonicalRequest);
            payload.Flush(flushToDisk: true);
        }
        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.Parameters.AddWithValue("$prefix", prefix);
        command.Parameters.AddWithValue("$run", prefix + "-run");
        command.Parameters.AddWithValue("$parentRun", parentRunId);
        command.Parameters.AddWithValue("$parentPrefix", parentRunId.EndsWith("-run", StringComparison.Ordinal)
            ? parentRunId[..^4] : parentRunId);
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
            INSERT OR IGNORE INTO runs VALUES($run,$prefix || '-install',$prefix || '-context',$prefix || '-config',$prefix || '-manifest','Running',0,1,1,$now,$now);
            INSERT OR IGNORE INTO runs VALUES($parentRun,$parentPrefix || '-install',$parentPrefix || '-context',$parentPrefix || '-config',$parentPrefix || '-manifest','Running',0,1,1,$now,$now);
            INSERT INTO job_nodes VALUES($prefix || '-job',$run,NULL,'provider','Running',0,$now,$now);
            INSERT INTO durable_commands VALUES($prefix || '-command','provider',$run,0,'recorded','running',NULL,$now,NULL,NULL);
            INSERT INTO evidence_acquisition_runs
              SELECT $owner,$prefix || '-install',$prefix || '-context',$prefix || '-config',$prefix || '-manifest',
                $parentRun,$applicationScope,$costScope,'running',$now
              WHERE $ownerKind='evidence-acquisition-run';
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
              $prefix || '-effective',$prefix || '-config','abababababababababababababababababababababababababababababababab',
              'asserted-retained-v1-identity',$profile,$generation,'gpt-5.6-sol','medium','current_turn','standard',0,
              'default',0,0,'none',0,'disabled','explicit',0,0,$maxRequest,$maxInput,$maxOutput,$maxRaw,1,$maxCost,
              $deadlineMs,'["hosted-search","nexus","loot"]',$now);
            INSERT INTO payloads VALUES($prefix || '-payload',$requestHash,$requestBytes,'application/json','retained',
              'payloads/' || substr($requestHash,1,2) || '/' || substr($requestHash,3,2) || '/' || $requestHash,$now);
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
              $prefix || '-install',$prefix || '-context',$prefix || '-effective',$prefix || '-manifest',$profile,$generation,0,
              $operationKind,$capability,$price,$promptId,$promptFingerprint,$prefix || '-schema',$schemaHash,
              $requestHash,$prefix || '-payload',$requestHash,$requestBytes,$settingsHash,
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
            VALUES($request,$request,$operation,$attempt,$requestHash,$requestHash,$settingsHash,$schemaHash,
              'openai-responses-o200k-byte-envelope','v2','proved',$prefix || '-payload',$requestHash,$requestBytes,$now);
            """;
        _ = command.ExecuteNonQuery();
    }

    private static long Required(ProviderQuantityContract quantity, string name) =>
        quantity.Availability == ProviderAvailabilityState.Available && quantity.Value is >= 0
            ? quantity.Value.Value : throw new InvalidDataException("Exact " + name + " are unavailable.");
}
