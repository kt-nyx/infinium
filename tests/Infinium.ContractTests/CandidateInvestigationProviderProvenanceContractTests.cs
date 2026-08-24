using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderProvenanceContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void CurrentAnswerFreeInputRoundTripsBothEvidenceRootKinds()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(input, SourceClaimContextMinimizer.JsonOptions);
        using JsonDocument json = JsonDocument.Parse(bytes);
        ActiveJsonSchemaValidator.Validate(json.RootElement,
            "candidate-investigation-execution-input.v1.schema.json");
        CandidateInvestigationExecutionInput reopened = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
            bytes, SourceClaimContextMinimizer.JsonOptions)!;
        CandidateInvestigationContextMinimizer.ValidateInput(reopened);
        Assert.AreEqual("persisted-source-claim-application", reopened.Contexts[0].Evidence[0].RootKind);
        Assert.AreEqual("source-application-decision", reopened.Contexts[0].Evidence[0].SourceApplicationDecisionId);
        Assert.AreEqual("frozen-host-evidence", reopened.Contexts[1].Evidence[0].RootKind);
        Assert.AreEqual("host-evidence-root", reopened.Contexts[1].Evidence[0].EvidenceRootId);
        Assert.AreEqual("host-applicability-record", reopened.Contexts[1].Evidence[0].ApplicabilityRecordId);
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ProviderProvenanceAndTransparencyRetainBothExactRootKinds()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationResult result = CandidateInvestigationEngine.Execute(input,
            [CandidateInvestigationDeveloperExample.Positive(), CandidateInvestigationDeveloperExample.Unsupported()]);
        CandidateInvestigationScenarioResult source = result.Scenarios[0];
        CandidateInvestigationScenarioResult host = result.Scenarios[1];
        Assert.AreEqual("source-admission", source.SourceAcquisitionLinks.Single().SourceAdmissionId);
        Assert.AreEqual("source-application-decision",
            source.SourceAcquisitionLinks.Single().SourceApplicationDecisionId);
        Assert.AreEqual("persisted-source-claim-application",
            source.EvidenceProvenanceLinks.Single().RootKind);
        Assert.AreEqual("host-evidence-root", host.EvidenceProvenanceLinks.Single().EvidenceRootId);
        Assert.AreEqual("host-applicability-record", host.EvidenceProvenanceLinks.Single().ApplicabilityRecordId);

        string json = Encoding.UTF8.GetString(CandidateInvestigationTransparencyRenderer.RenderJson(result));
        StringAssert.Contains(json, "source_application_decision_id\":\"source-application-decision");
        StringAssert.Contains(json, "root_kind\":\"frozen-host-evidence");
        StringAssert.Contains(json, "evidence_root_id\":\"host-evidence-root");
        StringAssert.Contains(json, "audit_reasons");
        string human = CandidateInvestigationTransparencyRenderer.RenderHuman(result);
        StringAssert.Contains(human, "exact evidence provenance roots");
        StringAssert.Contains(human, "host-evidence-root");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void OutputCodecRoundTripsCurrentTerminalStates()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        foreach (CandidateInvestigationRetainedTranscript transcript in new[]
                 {
                     CandidateInvestigationDeveloperExample.Positive(),
                     CandidateInvestigationDeveloperExample.Unsupported(),
                     CandidateInvestigationDeveloperExample.NoModel(),
                     CandidateInvestigationDeveloperExample.Drift(),
                 })
        {
            CandidateInvestigationDocument document = CandidateInvestigationEngine.Execute(input, [transcript])
                .Scenarios.Single().Investigation;
            byte[] first = ProviderContractJsonCodecs.Serialize(document);
            byte[] second = ProviderContractJsonCodecs.Serialize(
                ProviderContractJsonCodecs.DeserializeCandidateInvestigation(first));
            CollectionAssert.AreEqual(first, second);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void InputRejectsConflatedDecisionIdsAndInvalidDigests()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationContextInput context = input.Contexts[0];
        CandidateEvidenceInput evidence = context.Evidence.Single();
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationContextMinimizer.ValidateInput(
            input with { Contexts = [context with { Evidence = [evidence with
                { SourceApplicationDecisionId = evidence.SourceAdmissionId }] }, input.Contexts[1]] }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationContextMinimizer.ValidateInput(
            input with { Contexts = [context with { Evidence = [evidence with
                { ContentSha256 = "A" + evidence.ContentSha256[1..] }] }, input.Contexts[1]] }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationContextMinimizer.ValidateInput(
            input with { Contexts = [context with { ContextId = "invalid context" }, input.Contexts[1]] }));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ExecutionInputParticipantLimitMatchesTheTypedThirtyTwoParticipantLimit()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationContextInput context = input.Contexts[0];
        string[] participants = Enumerable.Range(1, 33).Select(index => $"participant-{index}").ToArray();
        string[] roles = Enumerable.Range(1, 33).Select(index => $"role-{index}").ToArray();
        CandidateInvestigationExecutionInput mutation = input with
        {
            Contexts = [context with { ParticipantIds = participants, ParticipantRoles = roles }, input.Contexts[1]],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationContextMinimizer.ValidateInput(mutation));
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(
            mutation, SourceClaimContextMinimizer.JsonOptions));
        Assert.ThrowsExactly<InvalidDataException>(() => ActiveJsonSchemaValidator.Validate(
            json.RootElement, "candidate-investigation-execution-input.v1.schema.json"));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateOutputTextAndIdentityBoundsMatchItsJsonContract()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationDocument document = CandidateInvestigationEngine.Execute(input,
            [CandidateInvestigationDeveloperExample.Positive()]).Scenarios.Single().Investigation;
        CandidateInvestigationDocument bounded = document with { Abstentions = [new string('x', 4096)] };
        byte[] canonical = ProviderContractJsonCodecs.Serialize(bounded);
        using (JsonDocument json = JsonDocument.Parse(canonical))
        {
            ActiveJsonSchemaValidator.Validate(json.RootElement, "candidate-investigation.v1.schema.json");
        }
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            document with { Abstentions = [new string('x', 4097)] }));
        Assert.ThrowsExactly<ArgumentException>(() => new OpaqueId("invalid candidate"));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            document with { ParticipantRoles = [new string('r', 129)] }));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateAnswerIsolationRejectsAuthorityAndDispositionCues()
    {
        foreach (string json in new[]
                 {
                     "{\"outer\":[{\"ground-truth\":\"x\"}]}",
                     "{\"context_id\":\"matched-negative\"}",
                     "{\"candidate_id\":\"positive_control\"}",
                     "{\"evidence_ids\":[\"no model\"]}",
                 })
        {
            using JsonDocument mutation = JsonDocument.Parse(json);
            Assert.ThrowsExactly<InvalidDataException>(
                () => CandidateInvestigationFixtureReader.AssertAnswerFreeProductInput(mutation.RootElement));
        }
    }
}
