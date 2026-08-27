using System.Security.Cryptography;
using Infinium.Application.Analysis;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal sealed record TargetedCanonicalSourceIdentity(
    OpaqueId OccurrenceId,
    OpaqueId LogicalId,
    IdentityEnvelopeContract IdentityEnvelope);

internal static class TargetedVerificationSourceIdentity
{
    public static byte[] ReadCanonicalPayload(
        AuthoritativeStore store,
        string runId,
        ResultItemPersistenceRecord projection)
    {
        ArgumentNullException.ThrowIfNull(store);
        AnalysisPhaseCheckpointRecord checkpoint = store.ReadLatestAnalysisPhaseCheckpoint(
            runId, FindingCaseAnalysisPhase.PhaseId)
            ?? throw new AnalysisIdentityDriftException(
                "The targeted source run has no retained canonical finding/case checkpoint.");
        byte[] bytes = store.ReadFindingCasePayload(checkpoint.PayloadId);
        string fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        _ = FindingCaseJsonCodec.Deserialize(bytes);
        if (checkpoint.PayloadSha256 != fingerprint)
        {
            throw new AnalysisIdentityDriftException(
                "The retained canonical finding/case checkpoint fingerprint differs from its payload bytes.");
        }
        if (projection.SourcePayloadId != checkpoint.PayloadId)
        {
            throw new AnalysisIdentityDriftException(
                "The targeted source result index selects a different payload than the retained canonical finding/case checkpoint.");
        }
        if (projection.SourcePayloadSha256 != checkpoint.PayloadSha256)
        {
            throw new AnalysisIdentityDriftException(
                "The targeted source result index reports a different fingerprint than the retained canonical finding/case checkpoint.");
        }
        return bytes;
    }

    public static bool ProjectionKindMatches(string occurrenceKind, string projectionKind) =>
        occurrenceKind switch
        {
            "finding" => projectionKind == "finding",
            "case" => projectionKind is "supported-case" or "lead-only-case",
            _ => false,
        };

    public static TargetedCanonicalSourceIdentity Resolve(
        FindingCaseContract payload,
        string occurrenceKind,
        string occurrenceId)
    {
        ArgumentNullException.ThrowIfNull(payload);
        FindingCaseContractInvariants.Validate(payload);
        return occurrenceKind switch
        {
            "finding" => ResolveFinding(payload, occurrenceId),
            "case" => ResolveCase(payload, occurrenceId),
            _ => throw new InvalidDataException("The targeted source occurrence kind is unsupported."),
        };
    }

    private static TargetedCanonicalSourceIdentity ResolveFinding(
        FindingCaseContract payload,
        string occurrenceId)
    {
        FindingContract[] matches = payload.Findings
            .Where(item => item.FindingOccurrenceId.Value == occurrenceId)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new AnalysisIdentityDriftException(
                "The canonical targeted finding occurrence does not resolve exactly once.");
        }
        FindingContract finding = matches[0];
        return new(finding.FindingOccurrenceId, finding.LogicalFindingId, finding.IdentityEnvelope);
    }

    private static TargetedCanonicalSourceIdentity ResolveCase(
        FindingCaseContract payload,
        string occurrenceId)
    {
        AnalysisCaseContract[] matches = payload.Cases
            .Where(item => item.CaseOccurrenceId.Value == occurrenceId)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new AnalysisIdentityDriftException(
                "The canonical targeted case occurrence does not resolve exactly once.");
        }
        AnalysisCaseContract analysisCase = matches[0];
        return new(analysisCase.CaseOccurrenceId, analysisCase.LogicalCaseId, analysisCase.IdentityEnvelope);
    }
}
