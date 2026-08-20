using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal static class Wp9ProductionProfileEnrollmentRunner
{
    private const string ProductionEnrollmentScenario = "wp9-production-profile-enrollment";
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    internal static void ValidateCampaignAdmissionOnly(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string? runtimeAuthorityManifestPath = null,
        string? runtimeAuthorityManifestSha256 = null)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        byte[] campaignBytes = File.ReadAllBytes(Path.GetFullPath(campaignManifestPath));
        M1Slice6AuthorityContractVersion credentialVersion = M1Slice6AuthorityContracts.Validate(
            credentialManifestPath, credentialBytes, M1Slice6AuthorityDocumentKind.CredentialProfile);
        M1Slice6AuthorityContractVersion campaignVersion = M1Slice6AuthorityContracts.Validate(
            campaignManifestPath, campaignBytes, M1Slice6AuthorityDocumentKind.Campaign);
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
        if (string.IsNullOrEmpty(runtimeAuthorityManifestPath)
            || string.IsNullOrEmpty(runtimeAuthorityManifestSha256))
        {
            ValidateCommittedCampaignAuthority(Path.GetFullPath(campaignManifestPath), campaignManifestSha256,
                reviewedCandidate);
            return;
        }
        ProviderEffectRuntimeAuthority runtimeAuthority = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
            runtimeAuthorityManifestPath, runtimeAuthorityManifestSha256, DateTimeOffset.UtcNow);
        ProviderEffectRuntimeAuthorityLoader.RequireExternalEffect(runtimeAuthority,
            ProviderEffectAuthorityKind.CredentialEnrollment);
        M1Slice6AuthorityContracts.RequireFreshExternalEffect(runtimeAuthority, campaignVersion,
            credentialVersion, campaign.RootElement.GetProperty("campaign_id").GetString()!, manifestId);
        if (runtimeAuthority.SubjectManifestId != manifestId
            || runtimeAuthority.SubjectManifestSha256 != credentialManifestSha256
            || runtimeAuthority.CampaignId != campaign.RootElement.GetProperty("campaign_id").GetString()
            || runtimeAuthority.CampaignManifestSha256 != campaignManifestSha256
            || runtimeAuthority.ImplementationCommit != reviewedCandidate)
        {
            throw new InvalidDataException("The credential runtime authority does not bind the exact campaign candidate.");
        }
    }

    internal static void AdmitCampaignCredentialExecutionHandoff(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, string runtimeAuthorityManifestPath,
        string runtimeAuthorityManifestSha256, DateTimeOffset now)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        _ = M1Slice6AuthorityContracts.Validate(credentialManifestPath, credentialBytes,
            M1Slice6AuthorityDocumentKind.CredentialProfile);
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
            reviewedCandidate, ledgerPath, runtimeAuthorityManifestPath, runtimeAuthorityManifestSha256);
        M1Slice6FiniteCampaignLedger ledger = OpenCampaignCredentialHandoff(campaign, root,
            root.GetProperty("manifest_id").GetString()!, credentialManifestSha256,
            profile.GetProperty("access_profile_id").GetString()!,
            profile.GetProperty("generation_id").GetString()!,
            profile.GetProperty("target_fingerprint_sha256").GetString()!, now, expiry,
            M1Slice6CampaignState.Ready);
        ProviderEffectRuntimeAuthority runtimeAuthority = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
            runtimeAuthorityManifestPath, runtimeAuthorityManifestSha256, now);
        ledger.RecordIndependentReview(runtimeAuthority.AuthorityId, runtimeAuthority.ManifestSha256,
            now.AddTicks(1));
        ledger.AdmitCampaign(now.AddTicks(2));
        ledger.BeginCredentialExecutionHandoff(now.AddTicks(3));
    }

    internal static void AcceptCampaignCredentialEvidence(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, string evidencePath, string repositoryRecordPath,
        DateTimeOffset now)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        _ = M1Slice6AuthorityContracts.Validate(
            credentialManifestPath, credentialBytes, M1Slice6AuthorityDocumentKind.CredentialProfile);
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
        // Profile-enrollment evidence has its own contract-version axis. The accepted v2
        // evidence shape carries manifest IDs as data and is valid for either authority
        // generation; only campaign/profile/stage authority needed the fresh v4 break.
        const string evidenceSchema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v2";
        const string evidenceId = "wp9-production-profile-enrollment-evidence-v2";
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
        ValidateAcceptedCampaignCredentialArtifacts(root, profile.GetProperty("target_fingerprint_sha256").GetString()!);
        DateTimeOffset completedAt = DateTimeOffset.Parse(root.GetProperty("completed_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        if (!root.EnumerateObject().Select(property => property.Name).SequenceEqual(exactProperties,
                StringComparer.Ordinal)
            || !containment.EnumerateObject().Select(property => property.Name).SequenceEqual(
                ["probe_executed", "excluded_handle_accessible", "process_tree_terminated",
                    "process_tree_survivor_count", "total_contained_process_count"], StringComparer.Ordinal)
            || root.GetProperty("schema").GetString() != evidenceSchema
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
            || root.GetProperty("listener_count").GetInt32() != 0
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
            || completedAt.Offset != TimeSpan.Zero || completedAt > now || completedAt > expiry
            || ledger.Current.EvidenceSha256 != evidenceSha)
        {
            throw new InvalidDataException("Credential evidence is not the exact independently reviewable success handoff.");
        }
        using JsonDocument campaignDocument = JsonDocument.Parse(
            File.ReadAllBytes(Path.GetFullPath(campaignManifestPath)));
        JsonElement campaignRoot = campaignDocument.RootElement;
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

    internal static void RecoverCampaignCredentialEvidence(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, string evidencePath, string failurePath,
        string productRoot, string helperBinary, string runtimeAuthorityManifestPath,
        string runtimeAuthorityManifestSha256, DateTimeOffset now)
        => RecoverCampaignCredentialEvidenceCore(credentialManifestPath, credentialManifestSha256,
            campaignManifestPath, campaignManifestSha256, reviewedCandidate, ledgerPath, evidencePath,
            failurePath, productRoot, helperBinary, runtimeAuthorityManifestPath,
            runtimeAuthorityManifestSha256, now, null);

    internal static void RecoverCampaignCredentialEvidenceForTesting(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, string evidencePath, string failurePath,
        string productRoot, string helperBinary, string runtimeAuthorityManifestPath,
        string runtimeAuthorityManifestSha256, DateTimeOffset now, string executingCoordinatorBinary)
        => RecoverCampaignCredentialEvidenceCore(credentialManifestPath, credentialManifestSha256,
            campaignManifestPath, campaignManifestSha256, reviewedCandidate, ledgerPath, evidencePath,
            failurePath, productRoot, helperBinary, runtimeAuthorityManifestPath,
            runtimeAuthorityManifestSha256, now, executingCoordinatorBinary);

    private static void RecoverCampaignCredentialEvidenceCore(string credentialManifestPath,
        string credentialManifestSha256, string campaignManifestPath, string campaignManifestSha256,
        string reviewedCandidate, string ledgerPath, string evidencePath, string failurePath,
        string productRoot, string helperBinary, string runtimeAuthorityManifestPath,
        string runtimeAuthorityManifestSha256, DateTimeOffset now, string? executingCoordinatorBinary)
    {
        byte[] credentialBytes = File.ReadAllBytes(Path.GetFullPath(credentialManifestPath));
        byte[] campaignBytes = File.ReadAllBytes(Path.GetFullPath(campaignManifestPath));
        M1Slice6AuthorityContractVersion credentialVersion = M1Slice6AuthorityContracts.Validate(
            credentialManifestPath, credentialBytes, M1Slice6AuthorityDocumentKind.CredentialProfile);
        M1Slice6AuthorityContractVersion campaignVersion = M1Slice6AuthorityContracts.Validate(
            campaignManifestPath, campaignBytes, M1Slice6AuthorityDocumentKind.Campaign);
        if (credentialVersion != M1Slice6AuthorityContractVersion.FreshC2V4
            || campaignVersion != M1Slice6AuthorityContractVersion.FreshC2V4
            || Convert.ToHexStringLower(SHA256.HashData(credentialBytes)) != credentialManifestSha256
            || Convert.ToHexStringLower(SHA256.HashData(campaignBytes)) != campaignManifestSha256)
        {
            throw new InvalidDataException("Credential evidence recovery requires the exact terminal v4 authority bytes.");
        }

        using JsonDocument credential = JsonDocument.Parse(credentialBytes);
        using JsonDocument campaign = JsonDocument.Parse(campaignBytes);
        JsonElement credentialRoot = credential.RootElement;
        JsonElement profile = credentialRoot.GetProperty("profile");
        JsonElement providerIntent = credentialRoot.GetProperty("provider_intent");
        JsonElement campaignRoot = campaign.RootElement;
        JsonElement envelope = campaignRoot.GetProperty("credential_envelope");
        JsonElement attachment = campaignRoot.GetProperty("authority_source");
        if (campaignRoot.GetProperty("candidate_binding").GetProperty("close_ready_implementation_commit")
                .GetString() != reviewedCandidate
            || credentialRoot.GetProperty("candidate_binding").GetProperty("close_ready_implementation_commit")
                .GetString() != reviewedCandidate
            || envelope.GetProperty("source_manifest_sha256").GetString() != credentialManifestSha256)
        {
            throw new InvalidDataException("Credential evidence recovery changed the terminal campaign candidate.");
        }
        DateTimeOffset campaignExpiry = DateTimeOffset.Parse(campaignRoot.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset credentialExpiry = DateTimeOffset.Parse(credentialRoot.GetProperty("expires_at_utc").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        M1Slice6CampaignIdentity identity = new(campaignRoot.GetProperty("campaign_id").GetString()!,
            campaignManifestSha256, attachment.GetProperty("attachment_sha256").GetString()!, reviewedCandidate,
            credentialRoot.GetProperty("manifest_id").GetString()!, credentialManifestSha256,
            profile.GetProperty("access_profile_id").GetString()!, profile.GetProperty("generation_id").GetString()!,
            profile.GetProperty("target_fingerprint_sha256").GetString()!);
        M1Slice6FiniteCampaignLedger ledger = new(Path.GetFullPath(ledgerPath), identity,
            campaignExpiry, credentialExpiry, now);
        if (ledger.Current.State != M1Slice6CampaignState.Stopped
            || ledger.Current.Event != "credential-helper-evidence-ambiguity-terminal-stop")
        {
            throw new InvalidOperationException("Credential evidence recovery requires its exact terminal predecessor.");
        }

        byte[] successBytes = File.ReadAllBytes(Path.GetFullPath(evidencePath));
        byte[] failureBytes = File.ReadAllBytes(Path.GetFullPath(failurePath));
        string successSha = Convert.ToHexStringLower(SHA256.HashData(successBytes));
        string failureSha = Convert.ToHexStringLower(SHA256.HashData(failureBytes));
        using JsonDocument success = JsonDocument.Parse(successBytes);
        using JsonDocument failure = JsonDocument.Parse(failureBytes);
        ValidateAcceptedCampaignCredentialArtifacts(success.RootElement,
            profile.GetProperty("target_fingerprint_sha256").GetString()!);
        DateTimeOffset completedAt = ParseEvidenceCompletedAtUtc(success.RootElement);
        if (success.RootElement.GetProperty("status").GetString() != "passed-active-verified"
            || success.RootElement.GetProperty("manifest_id").GetString() != identity.CredentialManifestId
            || success.RootElement.GetProperty("manifest_sha256").GetString() != credentialManifestSha256
            || success.RootElement.GetProperty("profile_id").GetString() != identity.CredentialProfileId
            || success.RootElement.GetProperty("generation_id").GetString() != identity.CredentialGenerationId
            || success.RootElement.GetProperty("campaign_credential_handoff_event_hash").GetString()
                != ledger.Entries[^2].EventHash
            || failure.RootElement.GetProperty("status").GetString() != "stopped-ambiguous-effect"
            || failure.RootElement.GetProperty("manifest_id").GetString() != identity.CredentialManifestId
            || failure.RootElement.GetProperty("manifest_sha256").GetString() != credentialManifestSha256
            || failure.RootElement.GetProperty("provider_operation_count").GetInt32() != 0
            || failure.RootElement.GetProperty("billable_operation_count").GetInt32() != 0
            || failure.RootElement.GetProperty("retry_permitted").GetBoolean()
            || completedAt > now || completedAt > credentialExpiry || completedAt > campaignExpiry
            || ledger.Current.EvidenceId != "wp9-production-profile-enrollment-failure"
            || ledger.Current.EvidenceSha256 != failureSha)
        {
            throw new InvalidDataException("Credential evidence recovery artifacts differ from the exact retained terminal facts.");
        }

        string helperPath = Path.GetFullPath(helperBinary);
        string helperSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helperPath)));
        if (credentialRoot.GetProperty("release_build").GetProperty("helper_sha256").GetString() != helperSha)
        {
            throw new InvalidDataException("Credential evidence recovery changed the accepted helper bytes.");
        }
        ProviderEffectRuntimeAuthority authority = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
            runtimeAuthorityManifestPath, runtimeAuthorityManifestSha256, now);
        ProviderEffectRuntimeAuthorityLoader.RequireEffectFreeRehearsal(authority);
        if (authority.Kind != ProviderEffectAuthorityKind.CredentialEvidenceRecovery
            || authority.SubjectManifestId != identity.CredentialManifestId
            || authority.SubjectManifestSha256 != credentialManifestSha256
            || authority.CampaignId != identity.CampaignId
            || authority.CampaignManifestSha256 != campaignManifestSha256
            || authority.PredecessorLedgerEventHash != ledger.Current.EventHash
            || authority.PredecessorEvidenceId != ledger.Current.EvidenceId
            || authority.PredecessorEvidenceSha256 != failureSha
            || authority.ReviewEvidenceId != "wp9-production-profile-enrollment-evidence-v2"
            || authority.ReviewEvidenceSha256 != successSha)
        {
            throw new InvalidDataException("Credential evidence recovery authority does not bind the exact terminal and success evidence.");
        }
        string coordinatorBinary = executingCoordinatorBinary ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("The executing coordinator binary path is unavailable.");
        string coordinatorSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(coordinatorBinary)));
        ProviderEffectRuntimeAuthorityLoader.ValidateExecutableBinding(authority,
            typeof(Wp9ProductionProfileEnrollmentRunner).Assembly, coordinatorSha, helperSha);
        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(campaignManifestPath);
        ProviderEffectRuntimeAuthorityLoader.ValidateExecutionBinding(authority, repository,
            Path.GetDirectoryName(Path.GetFullPath(evidencePath))!, ledgerPath, productRoot,
            coordinatorBinary, helperPath);
        string productStateBefore = CaptureReadOnlyProductStateIdentity(productRoot);
        CredentialProfileProjection durable = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
            Path.GetFullPath(productRoot), identity.CredentialProfileId);
        string productStateAfter = CaptureReadOnlyProductStateIdentity(productRoot);
        if (durable.GenerationId != identity.CredentialGenerationId
            || durable.ProfileId != identity.CredentialProfileId
            || durable.GenerationOrdinal != profile.GetProperty("generation_ordinal").GetInt64()
            || durable.RevocationEpoch != profile.GetProperty("revocation_epoch").GetInt64()
            || durable.LifecycleState != "active-verified" || durable.VerificationState != "available"
            || durable.CapabilitySnapshotId != M1ProviderCatalog.Capability.Identity.Value
            || durable.AccountIdentityId != providerIntent.GetProperty("account_identity_id").GetString()
            || durable.BillingScopeIdentityId != providerIntent.GetProperty("billing_scope_identity_id").GetString()
            || durable.RecoveryDisposition != "not-required"
            || durable.CleanupDisposition != "not-requested"
            || durable.ProjectionVersion != 3 || string.IsNullOrWhiteSpace(durable.IntentId)
            || durable.UpdatedAt > completedAt
            || productStateBefore != productStateAfter)
        {
            throw new InvalidDataException(
                "Credential evidence recovery lacks an immutable exact durable active profile projection.");
        }
        ledger.RecoverPostSuccessCredentialEvidence(ledger.Current.EventHash, ledger.Current.EvidenceId,
            failureSha, authority.ReviewEvidenceId, successSha, new(1, 2, 0, 1, 4),
            authority.AuthorityId, authority.ManifestSha256, now);
    }

    internal static void ValidateAcceptedCampaignCredentialArtifacts(JsonElement root, string targetFingerprint)
    {
        string[] exactProperties = ["schema", "status", "manifest_id", "manifest_sha256",
            "campaign_credential_handoff_event_hash", "profile_id", "generation_id",
            "target_fingerprint_sha256", "lifecycle_state", "verification_state",
            "native_credential_operation_count", "native_call_trace", "entry_evidence", "canaries",
            "network_operation_count", "listener_count", "provider_operation_count",
            "billable_operation_count", "retry_attempted", "containment", "namespace_reuse_blocked",
            "namespace_reuse_block_reason", "retention", "completed_at_utc"];
        JsonElement containment = root.GetProperty("containment");
        if (!root.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(exactProperties, StringComparer.Ordinal)
            || root.GetProperty("schema").GetString()
                != "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v2"
            || root.GetProperty("status").GetString() != "passed-active-verified"
            || root.GetProperty("target_fingerprint_sha256").GetString() != targetFingerprint
            || root.GetProperty("lifecycle_state").GetString() != "active-verified"
            || root.GetProperty("verification_state").GetString() != "available"
            || root.GetProperty("native_credential_operation_count").GetInt32() != 4
            || root.GetProperty("network_operation_count").GetInt32() != 0
            || root.GetProperty("listener_count").GetInt32() != 0
            || root.GetProperty("provider_operation_count").GetInt32() != 0
            || root.GetProperty("billable_operation_count").GetInt32() != 0
            || root.GetProperty("retry_attempted").GetBoolean()
            || !containment.EnumerateObject().Select(property => property.Name).SequenceEqual(
                ["probe_executed", "excluded_handle_accessible", "process_tree_terminated",
                    "process_tree_survivor_count", "total_contained_process_count"], StringComparer.Ordinal)
            || !containment.GetProperty("probe_executed").GetBoolean()
            || containment.GetProperty("excluded_handle_accessible").GetBoolean()
            || !containment.GetProperty("process_tree_terminated").GetBoolean()
            || containment.GetProperty("process_tree_survivor_count").GetInt32() != 0
            || containment.GetProperty("total_contained_process_count").GetInt32() != 2
            || root.GetProperty("namespace_reuse_blocked").GetBoolean()
            || root.GetProperty("namespace_reuse_block_reason").ValueKind != JsonValueKind.Null
            || root.GetProperty("retention").GetString() != "exact-generation-retained-no-delete-authority")
        {
            throw new InvalidDataException(
                "Accepted credential evidence changed its exact success, zero-effect, containment, or retention facts.");
        }

        JsonElement traceValue = root.GetProperty("native_call_trace");
        if (traceValue.ValueKind != JsonValueKind.Array || traceValue.GetArrayLength() != 4)
        {
            throw new InvalidDataException("Accepted credential evidence omitted the exact four-call native trace.");
        }
        JsonElement[] trace = traceValue.EnumerateArray().ToArray();
        string[] operations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree"];
        string[] results = ["ERROR_NOT_FOUND", "success", "success", "released"];
        for (int index = 0; index < trace.Length; index++)
        {
            string[] exactNames = ["Sequence", "Operation", "TargetFingerprintSha256", "Scenario",
                "Result", "AllocationId", "PairedAllocationId"];
            if (!trace[index].EnumerateObject().Select(property => property.Name)
                    .SequenceEqual(exactNames, StringComparer.Ordinal)
                || trace[index].GetProperty("Sequence").GetInt32() != index + 1
                || trace[index].GetProperty("Operation").GetString() != operations[index]
                || trace[index].GetProperty("Result").GetString() != results[index]
                || trace[index].GetProperty("TargetFingerprintSha256").GetString() != targetFingerprint
                || trace[index].GetProperty("Scenario").GetString() != ProductionEnrollmentScenario)
            {
                throw new InvalidDataException("Accepted credential evidence changed its native operation, result, target, order, or scenario.");
            }
        }
        ValidateExactFreePairing(trace, targetFingerprint);

        JsonElement entry = root.GetProperty("entry_evidence");
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Accepted credential evidence omitted its entry readiness and cleanup evidence.");
        }
        CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(
            entry.GetRawText(), "submitted");
        ValidateEntryElement(entry, "submitted");
        ValidateCanaryElement(root.GetProperty("canaries"));
        _ = ParseEvidenceCompletedAtUtc(root);
    }

    private static DateTimeOffset ParseEvidenceCompletedAtUtc(JsonElement root)
    {
        if (!DateTimeOffset.TryParseExact(root.GetProperty("completed_at_utc").GetString(), "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTimeOffset completedAt)
            || completedAt.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException("Accepted credential evidence completion time is not exact UTC.");
        }
        return completedAt;
    }

    private static string CaptureReadOnlyProductStateIdentity(string productRoot)
    {
        string root = Path.GetFullPath(productRoot);
        if (!Directory.Exists(root))
        {
            throw new InvalidDataException("The durable product-state root is absent.");
        }
        using IncrementalHash inventory = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                         StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            byte[] identity = Encoding.UTF8.GetBytes(relative + "\0" + new FileInfo(file).Length + "\0");
            inventory.AppendData(identity);
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                inventory.AppendData(buffer, 0, read);
            }
        }
        return Convert.ToHexStringLower(inventory.GetHashAndReset());
    }

    internal static string ProduceV2SuccessEvidence(string evidencePath,
        string manifestId, string manifestSha256, string campaignCredentialHandoffEventHash,
        string profileId, string generationId, string targetFingerprint,
        JsonElement nativeCallTrace, JsonElement entryEvidence, JsonElement canaries,
        DateTimeOffset completedAtUtc)
    {
        object evidence = new
        {
            schema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v2",
            status = "passed-active-verified",
            manifest_id = manifestId,
            manifest_sha256 = manifestSha256,
            campaign_credential_handoff_event_hash = campaignCredentialHandoffEventHash,
            profile_id = profileId,
            generation_id = generationId,
            target_fingerprint_sha256 = targetFingerprint,
            lifecycle_state = "active-verified",
            verification_state = "available",
            native_credential_operation_count = 4,
            native_call_trace = nativeCallTrace,
            entry_evidence = entryEvidence,
            canaries,
            network_operation_count = 0,
            listener_count = 0,
            provider_operation_count = 0,
            billable_operation_count = 0,
            retry_attempted = false,
            containment = new
            {
                probe_executed = true,
                excluded_handle_accessible = false,
                process_tree_terminated = true,
                process_tree_survivor_count = 0,
                total_contained_process_count = 2,
            },
            namespace_reuse_blocked = false,
            namespace_reuse_block_reason = (string?)null,
            retention = "exact-generation-retained-no-delete-authority",
            completed_at_utc = completedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",
                System.Globalization.CultureInfo.InvariantCulture),
        };
        string fullPath = Path.GetFullPath(evidencePath);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(evidence, IndentedJson) + "\n",
            new UTF8Encoding(false));
        using JsonDocument retained = JsonDocument.Parse(File.ReadAllBytes(fullPath));
        ValidateAcceptedCampaignCredentialArtifacts(retained.RootElement, targetFingerprint);
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fullPath)));
    }

    private static void ValidateCanaryElement(JsonElement canary)
    {
        string[] rootNames = ["SecretMatches", "RawTargetMatches", "RawTargetEncodings", "ScannedSurfaces"];
        if (canary.ValueKind != JsonValueKind.Object
            || !canary.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(rootNames, StringComparer.Ordinal)
            || canary.GetProperty("SecretMatches").GetInt32() != 0
            || canary.GetProperty("RawTargetMatches").GetInt32() != 0
            || !canary.GetProperty("RawTargetEncodings").EnumerateArray().Select(item => item.GetString())
                .SequenceEqual(["utf-8", "utf-16le"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("Accepted credential evidence retained a secret/raw-target canary or malformed encoding inventory.");
        }
        JsonElement[] surfaces = canary.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        string[] expectedNames = ["private protocol request", "private protocol response", "native call trace",
            "process command line", "process environment names"];
        string[] expectedKinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes",
            "captured-text", "captured-text"];
        if (surfaces.Length != expectedNames.Length)
        {
            throw new InvalidDataException("Accepted credential evidence has an incomplete canary surface inventory.");
        }
        for (int index = 0; index < surfaces.Length; index++)
        {
            string[] names = ["Name", "Kind", "ByteCount", "SecretMatches", "RawTargetMatches"];
            if (!surfaces[index].EnumerateObject().Select(property => property.Name)
                    .SequenceEqual(names, StringComparer.Ordinal)
                || surfaces[index].GetProperty("Name").GetString() != expectedNames[index]
                || surfaces[index].GetProperty("Kind").GetString() != expectedKinds[index]
                || surfaces[index].GetProperty("ByteCount").GetInt64() <= 0
                || surfaces[index].GetProperty("SecretMatches").GetInt32() != 0
                || surfaces[index].GetProperty("RawTargetMatches").GetInt32() != 0)
            {
                throw new InvalidDataException("Accepted credential canary evidence is stale, vacuous, duplicated, or nonzero.");
            }
        }
    }

    private static void ValidateEntryElement(JsonElement value, string terminalState)
    {
        if (value.GetProperty("Surface").GetString() != "wp9-distinct-helper-owned-native-masked-paste-surface"
            || value.GetProperty("TerminalState").GetString() != terminalState
            || !value.GetProperty("Masked").GetBoolean() || !value.GetProperty("PastePermitted").GetBoolean()
            || !value.GetProperty("HelperOwned").GetBoolean() || value.GetProperty("RendererReceivedSecret").GetBoolean()
            || !value.GetProperty("InitiallyBlank").GetBoolean() || !value.GetProperty("Ready").GetBoolean()
            || !value.GetProperty("HelperProcessOwned").GetBoolean() || !value.GetProperty("SameSession").GetBoolean()
            || !value.GetProperty("InputDesktopAvailable").GetBoolean() || !value.GetProperty("NotCloaked").GetBoolean()
            || !value.GetProperty("OnMonitor").GetBoolean() || !value.GetProperty("Enabled").GetBoolean()
            || !value.GetProperty("Focused").GetBoolean() || !value.GetProperty("Foreground").GetBoolean()
            || !value.GetProperty("Active").GetBoolean() || value.GetProperty("ReadinessChecks").GetInt32() < 1
            || value.GetProperty("MessagePumpIterations").GetInt32() < 1
            || !value.GetProperty("WindowDestroyed").GetBoolean() || !value.GetProperty("BufferCleared").GetBoolean()
            || !value.GetProperty("NativeEditEmptyVerified").GetBoolean()
            || !value.GetProperty("ThreadJoined").GetBoolean())
        {
            throw new InvalidDataException("Accepted credential entry evidence omitted exact readiness, ownership, masking, action, or cleanup facts.");
        }
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
        M1Slice6AuthorityContractVersion credentialVersion = M1Slice6AuthorityContracts.Validate(
            manifestPath, manifestBytes, M1Slice6AuthorityDocumentKind.CredentialProfile);
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(manifestBytes)), manifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("WP9 production enrollment manifest bytes changed after authorization.");
        }
        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;
        JsonElement profile = root.GetProperty("profile");
        JsonElement providerIntent = root.GetProperty("provider_intent");
        if (root.GetProperty("status").GetString() != "ready-for-owner-acceptance"
            || profile.GetProperty("mode").GetString() != "new-only"
            || credentialVersion is not (M1Slice6AuthorityContractVersion.RetiredV2
                or M1Slice6AuthorityContractVersion.FreshC2V4))
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
        if (campaign is not null && campaign.RuntimeAuthorityManifestPath.Length != 0)
        {
            ProviderEffectRuntimeAuthority runtimeAuthority = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                campaign.RuntimeAuthorityManifestPath, campaign.RuntimeAuthorityManifestSha256, now);
            byte[] campaignBytes = File.ReadAllBytes(Path.GetFullPath(campaign.ManifestPath));
            M1Slice6AuthorityContractVersion campaignVersion = M1Slice6AuthorityContracts.Validate(
                campaign.ManifestPath, campaignBytes, M1Slice6AuthorityDocumentKind.Campaign);
            using JsonDocument campaignDocument = JsonDocument.Parse(campaignBytes);
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(runtimeAuthority, campaignVersion,
                credentialVersion, campaignDocument.RootElement.GetProperty("campaign_id").GetString()!,
                manifestId);
            string coordinatorBinary = Environment.ProcessPath
                ?? throw new InvalidOperationException("The executing coordinator binary path is unavailable.");
            string coordinatorSha256 = Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(Path.GetFullPath(coordinatorBinary))));
            ProviderEffectRuntimeAuthorityLoader.ValidateExecutableBinding(runtimeAuthority,
                typeof(Wp9ProductionProfileEnrollmentRunner).Assembly, coordinatorSha256, helperSha256);
            string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(campaign.ManifestPath);
            ProviderEffectRuntimeAuthorityLoader.ValidateExecutionBinding(runtimeAuthority, repository,
                outputRoot, campaign.LedgerPath, productRoot, coordinatorBinary, helperBinary);
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
                ProductionEnrollmentScenario, bootstrap, assignment, now.AddTicks(2)).ConfigureAwait(false);
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
            schema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v2",
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
        if (disposition == "passed-active-verified")
        {
            JsonElement trace = ParseOptional(helper.Process.NativeCallTraceBytes)
                ?? throw new InvalidDataException("Successful enrollment omitted its retained native trace.");
            JsonElement entry = ParseOptional(helper.Process.NativeEntryCleanupBytes)
                ?? throw new InvalidDataException("Successful enrollment omitted its entry cleanup evidence.");
            JsonElement canary = ParseOptional(helper.Process.NativeCanaryEvidenceBytes)
                ?? throw new InvalidDataException("Successful enrollment omitted its canary evidence.");
            _ = ProduceV2SuccessEvidence(evidencePath, manifestId, manifestSha256,
                campaignLedger?.Current.EventHash ?? string.Empty, profileId, generationId,
                targetFingerprint, trace, entry, canary, DateTimeOffset.UtcNow);
        }
        else
        {
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, IndentedJson) + "\n",
                new UTF8Encoding(false));
        }
        if (disposition != "passed-active-verified")
        {
            object failure = new
            {
                schema = "infinium.m1-s6.wp9.production-profile-enrollment-failure/v2",
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
                    "wp9-production-profile-enrollment-evidence-v2", evidenceSha256,
                    new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), DateTimeOffset.UtcNow);
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
                    "wp9-production-profile-enrollment-evidence-v2", evidenceSha256, DateTimeOffset.UtcNow);
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
        M1Slice6AuthorityContractVersion campaignVersion = M1Slice6AuthorityContracts.Validate(
            campaignPath, bytes, M1Slice6AuthorityDocumentKind.Campaign);
        M1Slice6AuthorityContractVersion credentialVersion = credentialManifest.GetProperty("schema_identity").GetString() switch
        {
            M1Slice6AuthorityContracts.CredentialV2 => M1Slice6AuthorityContractVersion.RetiredV2,
            M1Slice6AuthorityContracts.CredentialV3 => M1Slice6AuthorityContractVersion.RetiredC2V3,
            M1Slice6AuthorityContracts.CredentialV4 => M1Slice6AuthorityContractVersion.FreshC2V4,
            _ => throw new InvalidDataException("Campaign credential authority version is unsupported."),
        };
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
        if (root.GetProperty("status").GetString() != "ready-for-campaign-review"
            || now >= campaignExpiry || credentialExpiry != envelopeExpiry
            || envelope.GetProperty("profile_id").GetString() != profileId
            || envelope.GetProperty("generation_id").GetString() != generationId
            || envelope.GetProperty("target_fingerprint_sha256").GetString() != targetFingerprint
            || credentialManifest.GetProperty("manifest_id").GetString() != credentialManifestId)
        {
            throw new InvalidDataException("Campaign-derived credential handoff does not preserve the exact credential envelope.");
        }
        M1Slice6CampaignIdentity identity = new(
            root.GetProperty("campaign_id").GetString()!, campaign.ManifestSha256,
            authority.GetProperty("attachment_sha256").GetString()!, campaign.ReviewedCandidateCommit,
            credentialManifestId, credentialManifestSha256, profileId, generationId, targetFingerprint);
        M1Slice6FiniteCampaignLedger ledger = new(ledgerPath, identity, campaignExpiry, credentialExpiry, now);
        if (campaign.RuntimeAuthorityManifestPath.Length == 0
            || campaign.RuntimeAuthorityManifestSha256.Length == 0)
        {
            ValidateCommittedCampaignAuthority(campaignPath, campaign.ManifestSha256,
                campaign.ReviewedCandidateCommit);
        }
        else
        {
            ProviderEffectRuntimeAuthority runtimeAuthority = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                campaign.RuntimeAuthorityManifestPath, campaign.RuntimeAuthorityManifestSha256, now);
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(runtimeAuthority, campaignVersion,
                credentialVersion, root.GetProperty("campaign_id").GetString()!, credentialManifestId);
            if (ledger.Current.State == M1Slice6CampaignState.Ready)
            {
                ProviderEffectRuntimeAuthorityLoader.ValidateDurableBinding(runtimeAuthority, identity,
                    ledger.Current, ProviderEffectAuthorityKind.CredentialEnrollment,
                    credentialManifestId, credentialManifestSha256, requireExternalEffect: true);
            }
            else if (ledger.Current.RuntimeAuthorityId != runtimeAuthority.AuthorityId
                || ledger.Current.RuntimeAuthoritySha256 != runtimeAuthority.ManifestSha256)
            {
                throw new InvalidDataException("The credential runtime authority differs from the durable admission.");
            }
        }
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
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(campaignManifestPath));
        if (manifest.RootElement.GetProperty("schema_identity").GetString()
            == "infinium.repository.m1-slice6-finite-campaign-authorization/2.0.0")
        {
            string relative = Path.GetRelativePath(repositoryRoot, campaignManifestPath).Replace('\\', '/');
            const string campaignRelative =
                "docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v2.json";
            const string profileRelative =
                "docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json";
            const string recordRelative = "docs/plans/milestones/m1/slices/s6/record.md";
            if (relative != campaignRelative
                || expectedReviewedCandidate.Length != 40
                || expectedReviewedCandidate.Any(character => !char.IsAsciiHexDigit(character)))
            {
                throw new InvalidDataException("R2 campaign authority is outside its exact reviewed Git candidate.");
            }
            byte[] committed = ReadGitBlob(repositoryRoot, expectedReviewedCandidate, relative);
            string committedSha = Convert.ToHexStringLower(SHA256.HashData(committed));
            JsonElement root = manifest.RootElement;
            JsonElement authority = root.GetProperty("authority_source");
            JsonElement candidateBinding = root.GetProperty("candidate_binding");
            JsonElement envelope = root.GetProperty("credential_envelope");
            byte[] profileBytes = ReadGitBlob(repositoryRoot, expectedReviewedCandidate, profileRelative);
            string profileSha = Convert.ToHexStringLower(SHA256.HashData(profileBytes));
            using JsonDocument profile = JsonDocument.Parse(profileBytes);
            JsonElement profileRoot = profile.RootElement;
            if (committedSha != expectedManifestSha256
                || manifest.RootElement.GetProperty("campaign_id").GetString()
                    != "infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66"
                || manifest.RootElement.GetProperty("status").GetString() != "ready-for-campaign-review"
                || manifest.RootElement.GetProperty("expires_at_utc").GetString()
                    != "2026-08-31T23:59:00.0000000Z"
                || envelope.GetProperty("source_manifest_id").GetString()
                    != profileRoot.GetProperty("manifest_id").GetString()
                || envelope.GetProperty("source_manifest_sha256").GetString() != profileSha)
            {
                throw new InvalidDataException("R2 campaign authority differs from its exact reviewed Git blob.");
            }
            string campaignId = root.GetProperty("campaign_id").GetString()!;
            string closeReady = candidateBinding.GetProperty(
                "close_ready_implementation_commit").GetString()!;
            string review = $"M1_S6_CAMPAIGN_REVIEW_ACCEPTANCE candidate_commit={expectedReviewedCandidate}" +
                $" campaign_id={campaignId} sha256={committedSha} verdicts=security,semantics,diff";
            string admission = $"M1_S6_CAMPAIGN_ADMISSION candidate_commit={expectedReviewedCandidate}" +
                $" authority_sha256={authority.GetProperty("attachment_sha256").GetString()}" +
                $" campaign_id={campaignId} sha256={committedSha} close_ready_commit={closeReady}" +
                $" expires_at_utc={root.GetProperty("expires_at_utc").GetString()}";
            string rollover = $"WP9_PROFILE_CAMPAIGN_ROLLOVER_ADMISSION campaign_candidate_commit={expectedReviewedCandidate}" +
                $" authority_sha256={authority.GetProperty("attachment_sha256").GetString()}" +
                $" campaign_id={campaignId} campaign_sha256={committedSha}" +
                $" manifest_id={profileRoot.GetProperty("manifest_id").GetString()} sha256={profileSha}" +
                $" close_ready_commit={profileRoot.GetProperty("candidate_binding")
                    .GetProperty("close_ready_implementation_commit").GetString()}" +
                $" credential_expires_at_utc={profileRoot.GetProperty("expires_at_utc").GetString()}";
            string reviewCommit = M1Slice6CampaignStageManifestValidator.UniqueMarkerCommit(
                repositoryRoot, review, expectedReviewedCandidate, recordRelative);
            string admissionCommit = M1Slice6CampaignStageManifestValidator.UniqueMarkerCommit(
                repositoryRoot, admission, reviewCommit, recordRelative);
            _ = M1Slice6CampaignStageManifestValidator.UniqueMarkerCommit(
                repositoryRoot, rollover, admissionCommit, recordRelative);
            return;
        }
        throw new InvalidDataException(
            "Campaign-derived credential execution accepts only the clean-break v2 campaign authority.");
    }

    private static byte[] ReadGitBlob(string repositoryRoot, string commit, string relativePath)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("show");
        start.ArgumentList.Add(commit + ":" + relativePath);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("R2 campaign Git-blob reader did not start.");
        using MemoryStream output = new();
        Task outputDrain = process.StandardOutput.BaseStream.CopyToAsync(output);
        Task<string> errorDrain = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("R2 campaign Git-blob reader exceeded its bound.");
        }
        Task.WaitAll(outputDrain, errorDrain);
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException("R2 campaign Git-blob binding is unavailable: "
                + errorDrain.Result.GetType().Name);
        }
        return output.ToArray();
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
        ValidateCanaryElement(canary.RootElement);
        byte[] entryBytes = receipt.NativeEntryCleanupBytes
            ?? throw new InvalidDataException("WP9 production enrollment omitted entry cleanup evidence.");
        string entryJson = System.Text.Encoding.UTF8.GetString(entryBytes);
        CredentialNativeQualificationSupervisor.ValidateWp9ProductionEntryEvidence(entryJson, terminalState);
        using JsonDocument entry = JsonDocument.Parse(entryBytes);
        ValidateEntryElement(entry.RootElement, terminalState);
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
            bool knownZeroEffectPreUi = retained is
            {
                Stage: "manifest-validation",
                ManualUiAttempted: false,
                CallCountsKnown: true,
                Total: 0,
                NetworkFactsKnown: true,
                ListenerCount: 0,
                NetworkOperationCount: 0,
                ExternalEffectFactsKnown: true,
                DnsOperationCount: 0,
                ProviderOperationCount: 0,
                BillableOperationCount: 0,
                ContainmentDescendantStarted: false,
            }
                && containment is { ProcessTreeTerminated: true, ProcessTreeSurvivorCount: 0 };
            string retainedStatus = knownZeroEffectPreUi
                ? "stopped-known-zero-effect-pre-ui"
                : "stopped-ambiguous-effect";
            object? retainedTrace = retained?.NativeCallTraceJson is null
                ? null : JsonSerializer.Deserialize<object>(retained.NativeCallTraceJson);
            object? retainedCanaries = retained?.CanaryEvidenceJson is null
                ? null : JsonSerializer.Deserialize<object>(retained.CanaryEvidenceJson);
            object? retainedEntry = retained?.EntryCleanupJson is null
                ? null : JsonSerializer.Deserialize<object>(retained.EntryCleanupJson);
            object failure = new
            {
                schema = "infinium.m1-s6.wp9.production-profile-enrollment-failure/v2",
                status = retainedStatus,
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
                schema = "infinium.m1-s6.wp9.production-profile-enrollment-evidence/v2",
                status = retainedStatus,
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
                    $"WP9 production profile enrollment\nstatus={retainedStatus}\n"
                    + $"profile_id={profileId}\ngeneration_id={generationId}\n"
                    + $"target_fingerprint_sha256={fingerprint}\n"
                    + $"lifecycle_state={durableState}\nverification_state=unavailable\n"
                    + $"native_calls={(retained?.CallCountsKnown == true ? retained.Total.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown")}\n"
                    + $"network_operations={(retained?.NetworkFactsKnown == true ? retained.NetworkOperationCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : "unknown")}\n"
                    + "provider_operations=0\n"
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
    string LedgerPath,
    string RuntimeAuthorityManifestPath = "",
    string RuntimeAuthorityManifestSha256 = "");
