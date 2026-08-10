using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Candidates;

public static partial class CandidatePipeline
{
    private static CandidateDependencyEdgeContract Edge(
        string fromKind, OpaqueId fromId, string toKind, OpaqueId toId, string edgeKind) => new(
            CandidateAnalysisIdentity.StableId("candidate-edge", fromKind, fromId.Value, toKind, toId.Value, edgeKind),
            fromKind, fromId, toKind, toId, edgeKind);

    private static void ValidateRequest(CandidatePipelineRequest request)
    {
        if (request.Context.OriginatingRunId is { } contextRun
            && contextRun != request.OriginatingRunId)
        {
            throw new InvalidOperationException("Candidate population context run identity differs from the request run.");
        }
        if (request.ExecutionInput is { } executionInput)
        {
            AnalysisExecutionContractInvariants.Validate(executionInput);
            if (executionInput.RunId != request.OriginatingRunId
                || request.Context.SourceSnapshotId != executionInput.InstallationSnapshot.ArtifactId
                || request.Context.ConfigurationId != executionInput.EffectiveConfiguration.ArtifactId)
            {
                throw new InvalidOperationException("Candidate execution input differs from the admitted run, snapshot, or configuration.");
            }
            Dictionary<OpaqueId, ArtifactReferenceContract> analyzerReferences = executionInput.AnalyzerDeclarations
                .ToDictionary(item => item.ArtifactId);
            if (analyzerReferences.Count != request.Sources.Count
                || request.Sources.Any(source =>
                {
                    Sha256Fingerprint fingerprint = CandidateAnalysisIdentity.StructuralHash(
                        [JsonSerializer.Serialize(source.Declaration)]);
                    return !analyzerReferences.TryGetValue(source.AnalyzerId, out ArtifactReferenceContract? reference)
                        || reference.ArtifactVersion != source.Declaration.AnalyzerVersion
                        || reference.Fingerprint != fingerprint;
                }))
            {
                throw new InvalidOperationException("Candidate sources differ from the admitted analyzer declaration set.");
            }
            if (request.Context.DeliveredInput is not null && request.Context.DeliveredExpansion is not null)
            {
                throw new InvalidOperationException("Candidate execution must admit exactly one delivered input or expansion artifact.");
            }
            if (request.Context.DeliveredInput is null
                && request.Context.DeliveredExpansion is null
                && request.Context.AdmittedDeliveredInputId is not null)
            {
                throw new InvalidOperationException(
                    "A delivered-input root cannot be admitted without delivered input or expansion bytes.");
            }
            if (request.Context.DeliveredInput is { } delivered)
            {
                CandidateDeliveredContractInvariants.Validate(delivered);
                Sha256Fingerprint actualFingerprint = ContractJsonSerializer.Fingerprint(delivered);
                ArtifactReferenceContract? deliveredReference = executionInput.SourceInputs.SingleOrDefault(item =>
                    item.ArtifactId == delivered.PayloadId);
                if (request.Context.AdmittedDeliveredInputId != delivered.PayloadId
                    || request.Context.DeliveredInputByteFingerprint is null
                    || deliveredReference is null
                    || deliveredReference.ArtifactVersion != CandidateDeliveredInputIdentity.Version
                    || request.Context.DeliveredInputByteFingerprint != actualFingerprint
                    || deliveredReference.Fingerprint != request.Context.DeliveredInputByteFingerprint
                    || !StringComparer.Ordinal.Equals(deliveredReference.Availability, "retained"))
                {
                    throw new InvalidOperationException("Candidate delivered input bytes differ from the admitted source artifact reference.");
                }
            }
            if (request.Context.DeliveredExpansion is { } expansion)
            {
                CandidateDeliveredContractInvariants.Validate(expansion);
                Sha256Fingerprint actualFingerprint = ContractJsonSerializer.Fingerprint(expansion);
                ArtifactReferenceContract? expansionReference = executionInput.SourceInputs.SingleOrDefault(item =>
                    item.ArtifactId == expansion.ExpansionId);
                OpaqueId[] resolvedRoots = request.Sources
                    .OfType<ICandidateDeliveredRootResolver>()
                    .Select(item => item.ResolveDeliveredInputId(request.Context))
                    .Distinct().ToArray();
                if (request.Context.AdmittedDeliveredInputId is null
                    || resolvedRoots.Length != 1
                    || resolvedRoots[0] != request.Context.AdmittedDeliveredInputId
                    || request.Context.DeliveredExpansionByteFingerprint is null
                    || expansionReference is null
                    || expansionReference.ArtifactVersion != CandidateDeliveredInputIdentity.Version
                    || request.Context.DeliveredExpansionByteFingerprint != actualFingerprint
                    || expansionReference.Fingerprint != request.Context.DeliveredExpansionByteFingerprint
                    || !StringComparer.Ordinal.Equals(expansionReference.Availability, "retained"))
                {
                    throw new InvalidOperationException("Candidate delivered expansion bytes differ from the admitted source artifact reference.");
                }
            }
        }
        else if (request.Context.DeliveredInput is not null
            || request.Context.DeliveredExpansion is not null
            || request.Context.AdmittedDeliveredInputId is not null)
        {
            throw new InvalidOperationException("A delivered candidate input or expansion requires an admitted analysis execution input.");
        }
        foreach (ICandidatePopulationSource source in request.Sources)
        {
            DomainContractInvariants.Validate(source.Declaration);
            if (!StringComparer.Ordinal.Equals(source.Declaration.AnalyzerId, source.AnalyzerId.Value)
                || source.Declaration.OperationRequirements.Mode != ExecutionRequirement.LocalOnly
                || source.Declaration.ExpectedScaleAndCost.Billable)
            {
                throw new InvalidOperationException("Candidate sources require matching local, non-billable analyzer declarations.");
            }
        }
        if (request.Sources.Count == 0
            || request.Limits.MaximumPopulationWork < 0
            || request.Limits.MaximumPopulationWork > 1_000_000
            || request.Limits.MaximumOptionalCandidates < 0
            || request.Limits.MaximumOptionalCandidates > 1_000_000
            || request.Sources.Select(item => item.AnalyzerId).Distinct().Count() != request.Sources.Count)
        {
            throw new InvalidOperationException("Candidate execution requires unique analyzers and non-negative closed limits.");
        }
    }

    private static void ValidateMember(CausalJoinPopulationMember member)
    {
        bool invalid = member.InputState == CausalJoinInputState.InvalidInput;
        bool failed = member.InputState == CausalJoinInputState.Failed;
        if (member.Lane == CandidateLane.Unspecified
            || StringComparer.Ordinal.Equals(member.SourceFactId.Value, "source-fact-unspecified")
            || member.Participants.Count > 16
            || (!invalid && !failed && member.Participants.Count < 2)
            || member.Participants.Any(item => string.IsNullOrWhiteSpace(item.Role)
                || item.Role.Length > 128
                || !IsAsciiToken(item.Role))
            || member.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != member.Participants.Count
            || member.Path.Count > 64
            || (!invalid && !failed && member.Path.Count == 0)
            || (!invalid && !failed && member.Participants.Any(item => !member.Path.Contains(item.ParticipantId)))
            || member.DependencyIds.Count > 128
            || (!invalid && !failed && member.DependencyIds.Count == 0)
            || member.DependencyIds.Distinct().Count() != member.DependencyIds.Count
            || member.SupportingEvidenceIds.Count > 128
            || member.ContradictingEvidenceIds.Count > 128
            || member.ContradictingEvidenceIds.Distinct().Count() != member.ContradictingEvidenceIds.Count
            || member.MissingInformation.Count > 32
            || member.MissingInformation.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1024)
            || (!invalid && !failed && member.SupportingEvidenceIds.Count == 0)
            || member.SupportingEvidenceIds.Distinct().Count() != member.SupportingEvidenceIds.Count
            || string.IsNullOrWhiteSpace(member.JoinKind)
            || member.JoinKind.Length > 128
            || !IsAsciiToken(member.JoinKind)
            || string.IsNullOrWhiteSpace(member.Rationale)
            || member.Rationale.Length > 4096
            || !IsStrictUtf8(member.Rationale)
            || string.IsNullOrWhiteSpace(member.PredictedImpact)
            || member.PredictedImpact.Length > 4096
            || !IsStrictUtf8(member.PredictedImpact)
            || member.MissingInformation.Any(item => !IsStrictUtf8(item))
            || (failed && (string.IsNullOrWhiteSpace(member.FailureCode)
                || member.FailureCode.Length > 128
                || !IsAsciiToken(member.FailureCode)
                || string.IsNullOrWhiteSpace(member.FailureMessage)
                || member.FailureMessage.Length > 512
                || !IsStrictUtf8(member.FailureMessage)))
            || (!failed && (member.FailureCode is not null || member.FailureMessage is not null))
            || (member.InputState == CausalJoinInputState.Ambiguous
                && member.ContradictingEvidenceIds.Count == 0
                && member.MissingInformation.Count == 0)
            || (member.EmitGap
                && member.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous
                && member.MissingInformation.Count == 0)
            || (member.Lane == CandidateLane.OptionalRanked && member.OptionalRank is null or <= 0)
            || (member.Lane != CandidateLane.OptionalRanked && member.OptionalRank is not null))
        {
            throw new InvalidDataException("A causal population member is not a closed bounded join.");
        }
    }

    private static bool IsAsciiToken(string value) => value.Length != 0
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '/' or '-');

    private static bool IsStrictUtf8(string value)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static CausalJoinPopulationMember DeclarationFailureMember(
        ICandidatePopulationSource source,
        string message)
    {
        OpaqueId memberId = CandidateAnalysisIdentity.StableId(
            "candidate-population-declaration-failure",
            source.AnalyzerId.Value,
            source.Declaration.AnalyzerVersion.ToString(),
            source.Declaration.RulesetVersion.ToString());
        return new CausalJoinPopulationMember(
            memberId,
            source.AnalyzerId,
            CandidateLane.DeterministicRequired,
            [],
            "analyzer-population-declaration",
            [],
            [],
            [],
            [],
            ["declared eligible population"],
            CausalJoinInputState.Failed,
            "The analyzer failed before it could declare a bounded eligible population.",
            "No causal impact can be assessed until the analyzer population can be declared.",
            FailureCode: "analyzer-declaration-failed",
            FailureMessage: "The analyzer could not declare its bounded population.",
            EmitGap: true)
        { SourceFactId = memberId };
    }

    private static CausalJoinPopulationMember FailureMember(
        OpaqueId analyzerId,
        CausalJoinPopulationMember member,
        string failureCode,
        string message)
    {
        try
        {
            ValidateMember(member);
            return member with
            {
                AnalyzerId = analyzerId,
                MissingInformation = ["completed analyzer execution"],
                InputState = CausalJoinInputState.Failed,
                Rationale = "The analyzer failed while constructing this declared population member.",
                PredictedImpact = "No causal impact can be assessed until analyzer execution completes.",
                FailureCode = failureCode,
                FailureMessage = "The analyzer did not complete bounded candidate construction.",
                EmitGap = true,
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return new CausalJoinPopulationMember(
                member.PopulationMemberId,
                analyzerId,
                CandidateLane.DeterministicRequired,
                [],
                "failed-causal-join",
                [], [], [], [],
                ["valid analyzer output"],
                CausalJoinInputState.Failed,
                "The analyzer returned malformed output and did not complete candidate construction.",
                "No causal impact can be assessed from malformed analyzer output.",
                FailureCode: "analyzer-output-invalid",
                FailureMessage: "The analyzer output failed bounded validation.",
                EmitGap: true)
            { SourceFactId = member.PopulationMemberId };
        }
    }

    private static CausalJoinPopulationMember ApplyDeclaredScope(
        ICandidatePopulationSource source,
        CausalJoinPopulationMember member)
    {
        if (source.Declaration.Scope.SupportedRecordFieldAssetShapes.Contains(member.JoinKind, StringComparer.Ordinal))
        {
            return member;
        }
        ReasonedAnalyzerScopeContract? excluded = source.Declaration.Scope.ExcludedRecordFieldAssetShapes
            .FirstOrDefault(item => StringComparer.Ordinal.Equals(item.ScopeId, member.JoinKind));
        return excluded is null
            ? InvalidMember(source.AnalyzerId, member, $"relationship kind '{member.JoinKind}' is not declared")
            : member with
            {
                InputState = CausalJoinInputState.Unsupported,
                MissingInformation = member.MissingInformation
                    .Append(excluded.Reason)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
            };
    }

    private static CausalJoinPopulationMember InvalidMember(
        OpaqueId analyzerId,
        CausalJoinPopulationMember member,
        string reason) => member with
        {
            AnalyzerId = analyzerId,
            SourceFactId = member.SourceFactId.Value == "source-fact-unspecified"
            ? member.PopulationMemberId
            : member.SourceFactId,
            Lane = CandidateLane.DeterministicRequired,
            Participants = [],
            JoinKind = "invalid-causal-join",
            Path = [],
            DependencyIds = [],
            SupportingEvidenceIds = [],
            ContradictingEvidenceIds = [],
            MissingInformation = ["valid bounded causal-join input"],
            InputState = CausalJoinInputState.InvalidInput,
            Rationale = "The declared population member failed bounded causal-join validation.",
            PredictedImpact = "Invalid input prevents a bounded downstream causal assessment.",
            OptionalRank = null,
            FailureCode = null,
            FailureMessage = null,
            EmitGap = false,
        };

    private static int LaneOrder(CandidateLane lane) => lane switch
    {
        CandidateLane.DeterministicRequired => 0,
        CandidateLane.MandatoryEvidence => 1,
        CandidateLane.OptionalRanked => 2,
        _ => throw new InvalidOperationException("Candidate lane is not closed."),
    };

    private static string Bound(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
}
