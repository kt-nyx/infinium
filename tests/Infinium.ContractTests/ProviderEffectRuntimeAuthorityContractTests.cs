using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class ProviderEffectRuntimeAuthorityContractTests
{
    private const string Schema = "provider-effect-runtime-authority.v1.schema.json";
    private const string SuccessorSchema = "provider-effect-runtime-authority.v2.schema.json";

    [TestMethod]
    public void CredentialEvidenceRecoveryIsSchemaClosedEffectFreeAndPredecessorBound()
    {
        JsonObject authority = ExactRecoveryAuthority();
        Validate(authority);

        JsonObject external = authority.DeepClone().AsObject();
        external["scope"] = "external-effect";
        Assert.ThrowsExactly<InvalidDataException>(() => Validate(external));

        JsonObject noPredecessor = authority.DeepClone().AsObject();
        noPredecessor["predecessor"]!["ledger_event_hash"] = "none";
        Assert.ThrowsExactly<InvalidDataException>(() => Validate(noPredecessor));

        JsonObject nativeCall = authority.DeepClone().AsObject();
        nativeCall["limits"]!["credential_native_calls"] = 1;
        Assert.ThrowsExactly<InvalidDataException>(() => Validate(nativeCall));
    }

    [TestMethod]
    public void SuccessorAuthorityPreservesV1SecurityBindingsAndAddsFreshAttemptIdentity()
    {
        JsonObject authority = ExactSuccessorAuthority();
        Validate(authority, SuccessorSchema);
        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
        {
            value => value["scope"] = "effect-free-rehearsal",
            value => value["attempt"]!["request_id"] = null,
            value => value["candidate_binding"]!["implementation_commit"] = new string('0', 39),
            value => value["execution"]!["ledger_path_relative"] = "../terminal/ledger.jsonl",
            value => value["limits"]!["provider_starts"] = 2,
            value => value["owner_amendment"]!["sha256"] = new string('g', 64),
            value => value["unknown"] = true,
        })
        {
            JsonObject changed = authority.DeepClone().AsObject();
            mutation(changed);
            Assert.ThrowsExactly<InvalidDataException>(() => Validate(changed, SuccessorSchema));
        }
    }

    private static JsonObject ExactRecoveryAuthority() => JsonSerializer.SerializeToNode(new
    {
        schema_identity = "infinium.provider.effect-runtime-authority/v1",
        authority_id = "recovery-runtime/contract",
        scope = "effect-free-rehearsal",
        kind = "credential-evidence-recovery",
        status = "reviewed-and-owner-accepted",
        subject_manifest = new { id = "credential/contract", sha256 = new string('1', 64) },
        campaign = new { id = "campaign/contract", sha256 = new string('2', 64) },
        predecessor = new
        {
            ledger_event_hash = new string('3', 64),
            evidence_id = "terminal-failure/contract",
            evidence_sha256 = new string('4', 64),
        },
        candidate_binding = new
        {
            implementation_commit = new string('5', 40),
            coordinator_sha256 = new string('6', 64),
            helper_sha256 = new string('7', 64),
        },
        review = new { evidence_id = "success-evidence/contract", evidence_sha256 = new string('8', 64) },
        owner_decision = new { decision_id = "owner/contract", decision_sha256 = new string('9', 64) },
        not_before_utc = "2026-08-19T20:00:00.0000000Z",
        expires_at_utc = "2026-08-19T21:00:00.0000000Z",
        execution = new
        {
            output_root_relative = "artifacts/recovery",
            ledger_path_relative = "artifacts/recovery/ledger.jsonl",
            product_state_root_relative = "artifacts/product-state",
            coordinator_path_relative = "bin/Infinium.Coordinator.exe",
            helper_path_relative = "bin/Infinium.CredentialHelper.exe",
        },
        limits = new
        {
            helper_launches = 0,
            credential_native_calls = 0,
            provider_starts = 0,
            dns_resolutions = 0,
            billable_operations = 0,
            literal_loopback_starts = 0,
            automatic_retry = false,
            fourth_call_permitted = false,
        },
    })!.AsObject();

    private static void Validate(JsonObject value)
    {
        using JsonDocument document = JsonDocument.Parse(value.ToJsonString());
        ActiveJsonSchemaValidator.Validate(document.RootElement, Schema);
    }

    private static JsonObject ExactSuccessorAuthority() => JsonSerializer.SerializeToNode(new
    {
        schema_identity = "infinium.provider.effect-runtime-authority/v2",
        authority_id = "successor-runtime/contract",
        scope = "external-effect",
        kind = "transport-qualification",
        status = "reviewed-and-owner-accepted",
        subject_manifest = new { id = "stage/contract", sha256 = new string('1', 64) },
        campaign = new { id = "campaign/contract", sha256 = new string('2', 64) },
        predecessor = new { ledger_event_hash = new string('3', 64), evidence_id = "predecessor/contract", evidence_sha256 = new string('4', 64) },
        attempt = new { attempt_id = "attempt/2", attempt_ordinal = 2, request_id = "request/2", reservation_id = "reservation/2", dispatch_fence_id = "fence/2" },
        credential_access = new { id = "credential-access/contract", sha256 = new string('5', 64) },
        candidate_binding = new { candidate_id = "runtime-candidate/contract", candidate_path = "artifacts/runtime-candidate.json", candidate_sha256 = new string('c', 64), implementation_commit = new string('6', 40), coordinator_sha256 = new string('7', 64), helper_sha256 = new string('8', 64) },
        review = new { evidence_id = "review/contract", evidence_path = "artifacts/review.json", evidence_sha256 = new string('9', 64) },
        owner_decision = new { decision_id = "owner/contract", decision_path = "artifacts/owner.json", decision_sha256 = new string('a', 64) },
        owner_amendment = new { id = "amendment/contract", sha256 = new string('b', 64) },
        not_before_utc = "2026-08-20T20:00:00.0000000Z",
        expires_at_utc = "2026-08-20T21:00:00.0000000Z",
        execution = new { output_root_relative = "artifacts/successor", ledger_path_relative = "artifacts/successor/ledger.jsonl", evidence_path_relative = "artifacts/successor/evidence.json", product_state_root_absolute = "C:/retained/product-state", product_state_snapshot_origin_sha256 = new string('d', 64), product_state_checkpoint_sha256 = new string('e', 64), coordinator_path_relative = "bin/Infinium.Coordinator.exe", helper_path_relative = "bin/Infinium.CredentialHelper.exe" },
        limits = new { helper_launches = 1, credential_native_calls = 2, provider_starts = 1, dns_resolutions = 1, billable_operations = 1, automatic_retry = false },
    })!.AsObject();

    private static void Validate(JsonObject value, string schema)
    {
        using JsonDocument document = JsonDocument.Parse(value.ToJsonString());
        ActiveJsonSchemaValidator.Validate(document.RootElement, schema);
    }
}
