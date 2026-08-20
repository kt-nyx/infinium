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
}
