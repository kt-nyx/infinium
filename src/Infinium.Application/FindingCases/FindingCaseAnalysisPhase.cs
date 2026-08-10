using Infinium.Analysis.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.FindingCases;

public static class FindingCaseAnalysisPhase
{
    public const string PhaseId = "finding-case-analysis";
    public const string PhaseVersion = "1.0.0";

    public static FindingCaseAnalysisPhaseResult Execute(
        AuthoritativeStore store,
        FindingCaseInputBuildRequest request,
        AttemptRecord attempt,
        RunBinding binding,
        DateTimeOffset now) => Execute(store, FindingCaseInputProducer.Create(request), attempt, binding, now);

    public static FindingCaseAnalysisPhaseResult Execute(
        AuthoritativeStore store,
        FindingCaseInputContract input,
        AttemptRecord attempt,
        RunBinding binding,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(store);
        FindingCaseContractInvariants.Validate(input);
        if (!StringComparer.Ordinal.Equals(input.OriginatingRunId.Value, attempt.RunId)
            || input.CandidateAnalysis.OriginatingRunId != input.OriginatingRunId)
        {
            throw new InvalidOperationException("Finding/case input and current attempt must bind the same immutable run.");
        }
        FindingCaseContract result = FindingCasePipeline.Execute(input);
        byte[] payload = FindingCaseJsonCodec.Serialize(result);
        FindingCaseContract roundTrip = FindingCaseJsonCodec.Deserialize(payload);
        if (!payload.AsSpan().SequenceEqual(FindingCaseJsonCodec.Serialize(roundTrip))
            || roundTrip.PayloadId != result.PayloadId)
        {
            throw new InvalidDataException("Finding/case publication must round-trip to the exact aggregate semantics.");
        }
        FindingCasePersistenceReceipt receipt = store.PublishFindingCase(result, payload, attempt, binding, now);
        return new FindingCaseAnalysisPhaseResult(result, receipt, payload);
    }
}

public sealed record FindingCaseAnalysisPhaseResult(
    FindingCaseContract Analysis,
    FindingCasePersistenceReceipt Receipt,
    byte[] SerializedPayload);
