using Infinium.Analysis.Candidates;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Candidates;

public sealed class DeliveredIndexCandidatePopulationSource : ICandidatePopulationSource, ICandidateDeliveredRootResolver
{
    public static readonly OpaqueId Id = new("candidate-source-delivered-indexes-v1");

    public OpaqueId AnalyzerId => Id;

    public AnalyzerDeclarationContract Declaration { get; } = CandidateAnalyzerDeclarations.Create(
        Id,
        supportedInput: "candidate-delivered-input/v1 factual Bethesda and documentation evidence projection",
        supportedShapes:
        [
            "record-link-winner-comparison",
            "record-facegen-provider",
            "delivered-coverage-gap",
            "documentation-application",
        ],
        inputPopulations:
        [
            new("candidate-delivered-substrate", "exactly one retained delivered input or deterministic expansion", true),
            new("delivered-record-links", "snapshot-bound prior and winner link facts", false),
            new("delivered-facegen", "snapshot-bound FaceGen applicability and provider facts", false),
            new("delivered-coverage-gaps", "explicit unsupported delivered populations", false),
            new("documentation-applications", "run/snapshot/context-bound documentation application facts", false),
        ],
        dependencies:
        [
            new(ContractConstants.CandidateDeliveredInputSchemaId, CandidateDeliveredInputIdentity.Version, false, CoverageState.Completed),
            new(ContractConstants.CandidateDeliveredExpansionSchemaId, CandidateDeliveredInputIdentity.Version, false, CoverageState.Completed),
            new("bethesda-semantic-snapshot", BethesdaSemanticContract.SchemaVersion, false, CoverageState.Unsupported),
            new(ContractConstants.DocumentationEvidenceSchemaId, new ContractVersion(1, 0, 0), false, CoverageState.Unsupported),
        ]);

    public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
        CandidatePopulationContext context,
        CancellationToken cancellationToken = default) => Build(context, cancellationToken);

    public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
        CandidatePopulationContext context,
        CancellationToken cancellationToken = default) => Build(context, cancellationToken);

    private static CausalJoinPopulationMember[] Build(CandidatePopulationContext context, CancellationToken cancellationToken)
    {
        if (context.DeliveredInput is not null && context.DeliveredExpansion is not null)
        {
            throw new InvalidDataException("Delivered candidate construction accepts one input form, not both.");
        }
        CandidateDeliveredInputContract? input = context.DeliveredInput;
        CandidateDeliveredExpansionContract? expansion = context.DeliveredExpansion;
        if (input is null && expansion is not null)
        {
            CandidateDeliveredContractInvariants.Validate(expansion);
            if (context.OriginatingRunId is not null && expansion.OriginatingRunId != context.OriginatingRunId
                || context.SourceSnapshotId is not null && expansion.SourceSnapshotId != context.SourceSnapshotId
                || context.AnalysisContextId is not null && expansion.AnalysisContextId != context.AnalysisContextId
                || context.ConfigurationId is not null && expansion.ConfigurationId != context.ConfigurationId)
            {
                throw new InvalidDataException("The delivered candidate expansion is not bound to the candidate population context.");
            }
            input = CandidateDeliveredInputExpander.Expand(expansion);
        }
        if (input is null)
        {
            throw new InvalidDataException("The required candidate delivered-input artifact is unavailable.");
        }
        CandidateDeliveredContractInvariants.Validate(input);
        if (context.OriginatingRunId is not null && input.OriginatingRunId != context.OriginatingRunId
            || context.SourceSnapshotId is not null && input.SourceSnapshotId != context.SourceSnapshotId
            || context.AnalysisContextId is not null && input.AnalysisContextId != context.AnalysisContextId
            || context.ConfigurationId is not null && input.ConfigurationId != context.ConfigurationId)
        {
            throw new InvalidDataException("The delivered candidate input is not bound to the candidate population context.");
        }

        return BuildLinkJoins(input, cancellationToken).Concat(BuildFaceGenJoins(input, cancellationToken))
            .Concat(BuildGapJoins(input, cancellationToken)).Concat(BuildDocumentationJoins(input, cancellationToken))
            .OrderBy(item => item.PopulationMemberId.Value, StringComparer.Ordinal).ToArray();
    }

    public OpaqueId ResolveDeliveredInputId(CandidatePopulationContext context)
    {
        if (context.DeliveredInput is not null && context.DeliveredExpansion is not null)
        {
            throw new InvalidOperationException(
                "Delivered root resolution requires exactly one input or expansion artifact.");
        }
        if (context.DeliveredInput is { } input)
        {
            CandidateDeliveredContractInvariants.Validate(input);
            return input.PayloadId;
        }
        if (context.DeliveredExpansion is { } expansion)
        {
            return CandidateDeliveredInputExpander.Expand(expansion).PayloadId;
        }
        throw new InvalidOperationException(
            "Delivered root resolution requires input or expansion bytes.");
    }

    private static IEnumerable<CausalJoinPopulationMember> BuildLinkJoins(
        CandidateDeliveredInputContract input,
        CancellationToken cancellationToken)
    {
        foreach (CandidateDeliveredLinkFactContract fact in input.LinkFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool comparable = fact.PriorState != CandidateDeliveredLinkState.Unresolved
                && fact.WinningState != CandidateDeliveredLinkState.Unresolved;
            bool equal = comparable && fact.PriorState == fact.WinningState
                && fact.PriorTargetParticipantId == fact.WinningTargetParticipantId;
            List<CandidateParticipantContract> participants =
            [
                new(fact.RecordParticipantId, "record"),
                new(fact.PriorContributionId, "prior-contribution"),
                new(fact.WinningContributionId, "winning-contribution"),
            ];
            if (fact.PriorTargetParticipantId is { } priorTarget)
            {
                participants.Add(new(priorTarget, "prior-target"));
            }
            if (fact.WinningTargetParticipantId is { } winningTarget)
            {
                participants.Add(new(winningTarget, "winning-target"));
            }
            List<string> missing = comparable ? [] : ["resolved canonical link target"];
            yield return new(
                CandidateAnalysisIdentity.StableId("candidate-population", "record-link", fact.FactId.Value),
                Id, CandidateLane.DeterministicRequired, participants, "record-link-winner-comparison",
                participants.Select(item => item.ParticipantId).Concat(fact.EvidenceIds).ToArray(),
                fact.DependencyIds.Prepend(input.PayloadId).Distinct().ToArray(), fact.EvidenceIds, [], missing,
                equal ? CausalJoinInputState.ResolvedNegative
                    : comparable ? CausalJoinInputState.Complete : CausalJoinInputState.Ambiguous,
                equal ? "The prior and winning canonical relationship agree."
                    : "A prior canonical relationship differs from, is absent from, or cannot yet be resolved against the winning contribution.",
                "A changed winning relationship may alter which retained target relationship downstream analysis must evaluate.",
                EmitGap: missing.Count != 0)
            { SourceFactId = fact.FactId };
        }
    }

    private static IEnumerable<CausalJoinPopulationMember> BuildFaceGenJoins(
        CandidateDeliveredInputContract input,
        CancellationToken cancellationToken)
    {
        foreach (CandidateDeliveredFaceGenFactContract fact in input.FaceGenFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool notApplicable = fact.Applicability == CandidateDeliveredFaceGenApplicability.NotApplicable;
            bool unknown = fact.Applicability == CandidateDeliveredFaceGenApplicability.Unknown
                || (!notApplicable && (fact.MeshAvailability == CandidateDeliveredAssetAvailability.Unknown
                    || fact.TintAvailability == CandidateDeliveredAssetAvailability.Unknown));
            List<string> missing = unknown ? ["resolved FaceGen applicability and exact asset availability"] : [];
            List<CandidateParticipantContract> participants =
            [new(fact.NpcParticipantId, "record"), new(fact.MeshAssetId, "mesh-asset"), new(fact.TintAssetId, "tint-asset")];
            if (fact.MeshProviderParticipantId is { } meshProvider)
            {
                participants.Add(new(meshProvider, "mesh-provider"));
            }
            if (fact.TintProviderParticipantId is { } tintProvider)
            {
                participants.Add(new(tintProvider, "tint-provider"));
            }
            long rank = checked(1L + (100L - fact.Locality) * 101L + (100L - fact.Specificity));
            yield return new(
                CandidateAnalysisIdentity.StableId("candidate-population", "facegen", fact.FactId.Value),
                Id, CandidateLane.OptionalRanked, participants, "record-facegen-provider",
                participants.Select(item => item.ParticipantId).Concat(fact.EvidenceIds).ToArray(),
                fact.DependencyIds.Prepend(input.PayloadId).Distinct().ToArray(), fact.EvidenceIds, [], missing,
                notApplicable ? CausalJoinInputState.ResolvedNegative : unknown ? CausalJoinInputState.Ambiguous : CausalJoinInputState.Complete,
                "A snapshot-bound record-to-asset-provider relationship deserves bounded cross-layer analysis.",
                "A mismatch between an applicable NPC record and its retained mesh or tint assets may affect downstream appearance analysis.",
                rank, EmitGap: missing.Count != 0)
            { SourceFactId = fact.FactId };
        }
    }

    private static IEnumerable<CausalJoinPopulationMember> BuildGapJoins(
        CandidateDeliveredInputContract input,
        CancellationToken cancellationToken)
    {
        foreach (CandidateDeliveredCoverageGapFactContract fact in input.CoverageGapFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new(
                CandidateAnalysisIdentity.StableId("candidate-population", "delivered-gap", fact.FactId.Value),
                Id, CandidateLane.MandatoryEvidence,
                [new(input.SourceSnapshotId, "snapshot"), new(fact.PopulationId, "unsupported-population")],
                "delivered-coverage-gap", [input.SourceSnapshotId, fact.PopulationId, .. fact.EvidenceIds],
                fact.DependencyIds.Prepend(input.PayloadId).Distinct().ToArray(), fact.EvidenceIds, [], [fact.MissingCapability],
                CausalJoinInputState.Unsupported, fact.Reason,
                "The missing capability may prevent complete downstream evaluation of the declared population.")
            { SourceFactId = fact.FactId };
        }
    }

    private static IEnumerable<CausalJoinPopulationMember> BuildDocumentationJoins(
        CandidateDeliveredInputContract input,
        CancellationToken cancellationToken)
    {
        foreach (CandidateDeliveredDocumentationFactContract fact in input.DocumentationFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool bound = fact.ConsumingRunId == input.OriginatingRunId
                && fact.AnalysisContextId == input.AnalysisContextId
                && (fact.SupplyingSnapshotId is null || fact.SupplyingSnapshotId == input.SourceSnapshotId);
            CausalJoinInputState state = !bound ? CausalJoinInputState.Unsupported : fact.Applicability switch
            {
                ClaimApplicabilityState.Applicable => CausalJoinInputState.Complete,
                ClaimApplicabilityState.NotApplicable => CausalJoinInputState.ResolvedNegative,
                ClaimApplicabilityState.Unsupported => CausalJoinInputState.Unsupported,
                ClaimApplicabilityState.Unknown or ClaimApplicabilityState.Contradicted => CausalJoinInputState.Ambiguous,
                _ => CausalJoinInputState.InvalidInput,
            };
            List<string> missing = !bound ? ["matching run, snapshot, and analysis-context provenance"]
                : state == CausalJoinInputState.Ambiguous ? ["resolved claim applicability"] : [];
            yield return new(
                CandidateAnalysisIdentity.StableId("candidate-population", "documentation", fact.FactId.Value),
                Id, CandidateLane.MandatoryEvidence,
                [new(fact.SubjectId, "local-subject"), new(fact.ClaimId, "external-claim")],
                "documentation-application", [fact.SubjectId, fact.ApplicationId, fact.ClaimId, fact.PassageId],
                fact.DependencyIds.Prepend(input.PayloadId).Distinct().ToArray(), fact.SupportingEvidenceIds,
                fact.ContradictingEvidenceIds, missing, state,
                "An admitted claim application is causally joined to its exact local subject and applicability context.",
                "An applicable external requirement may change how the exact local subject is evaluated downstream.",
                EmitGap: missing.Count != 0)
            { SourceFactId = fact.FactId };
        }
    }
}

public static class CandidateDeliveredInputAdapter
{
    public static CandidateDeliveredInputContract Create(
        OpaqueId originatingRunId,
        OpaqueId sourceSnapshotId,
        OpaqueId analysisContextId,
        OpaqueId configurationId,
        BethesdaSemanticSnapshot? bethesdaSnapshot,
        DocumentationEvidenceContract? documentationEvidence,
        OpaqueId? retainedDocumentationSourceRunId = null,
        CancellationToken cancellationToken = default)
    {
        if (bethesdaSnapshot is not null && bethesdaSnapshot.SourceSnapshotId != sourceSnapshotId)
        {
            throw new InvalidDataException("The Bethesda semantic snapshot does not match the delivered-input snapshot binding.");
        }
        if (documentationEvidence is not null)
        {
            DocumentationEvidenceContractInvariants.Validate(documentationEvidence);
            if (documentationEvidence.OriginatingRunId != originatingRunId
                && documentationEvidence.OriginatingRunId != retainedDocumentationSourceRunId)
            {
                throw new InvalidDataException("The documentation evidence does not match the delivered-input run binding.");
            }
        }
        CandidateDeliveredInputContract result = new(
            ContractConstants.CandidateDeliveredInputSchemaId, CandidateDeliveredInputIdentity.Version,
            new OpaqueId("candidate-delivered-input-pending"), originatingRunId, sourceSnapshotId,
            analysisContextId, configurationId,
            bethesdaSnapshot is null ? [] : LinkFacts(bethesdaSnapshot, cancellationToken).ToArray(),
            bethesdaSnapshot is null ? [] : FaceGenFacts(bethesdaSnapshot, cancellationToken).ToArray(),
            bethesdaSnapshot is null ? [] : GapFacts(bethesdaSnapshot, cancellationToken).ToArray(),
            documentationEvidence is null ? [] : DocumentationFacts(documentationEvidence, cancellationToken).ToArray());
        result = result with { PayloadId = CandidateDeliveredInputIdentity.ComputePayloadId(result) };
        CandidateDeliveredContractInvariants.Validate(result);
        return result;
    }

    private static IEnumerable<CandidateDeliveredLinkFactContract> LinkFacts(
        BethesdaSemanticSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        Dictionary<string, BethesdaLinkFact[]> links = snapshot.Links.GroupBy(item => item.SourceContributionId, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.ToArray(), StringComparer.Ordinal);
        foreach (BethesdaOverrideChain chain in snapshot.OverrideChains.Values.OrderBy(item => item.Identity.ParticipantId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            links.TryGetValue(chain.Winner.ContributionId, out BethesdaLinkFact[]? winners);
            Dictionary<string, BethesdaLinkFact> winnerSlots = (winners ?? []).ToDictionary(LinkKey, StringComparer.Ordinal);
            foreach (BethesdaRecordContribution prior in chain.Contributions.Where(item => item.ContributionId != chain.Winner.ContributionId)
                .OrderBy(item => item.ContributionId, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                links.TryGetValue(prior.ContributionId, out BethesdaLinkFact[]? priors);
                Dictionary<string, BethesdaLinkFact> priorSlots = (priors ?? []).ToDictionary(LinkKey, StringComparer.Ordinal);
                foreach (string slot in priorSlots.Keys.Concat(winnerSlots.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    priorSlots.TryGetValue(slot, out BethesdaLinkFact? priorLink);
                    winnerSlots.TryGetValue(slot, out BethesdaLinkFact? winnerLink);
                    BethesdaLinkFact shape = priorLink ?? winnerLink!;
                    OpaqueId factId = CandidateAnalysisIdentity.StableId("candidate-delivered-link-fact", chain.Identity.ParticipantId,
                        prior.ContributionId, chain.Winner.ContributionId, slot);
                    yield return new(
                        factId, SourceId("record", chain.Identity.ParticipantId), SourceId("contribution", prior.ContributionId),
                        SourceId("contribution", chain.Winner.ContributionId), prior.ContributionId, chain.Winner.ContributionId,
                        Token(shape.Field), shape.Component is null ? null : Token(shape.Component), shape.Ordinal,
                        LinkState(priorLink), Target(priorLink), LinkState(winnerLink), Target(winnerLink),
                        [snapshot.SourceSnapshotId, SourceId("contribution", prior.ContributionId), SourceId("contribution", chain.Winner.ContributionId)],
                        [CandidateAnalysisIdentity.StableId("candidate-delivered-link-evidence", factId.Value, "prior"),
                            CandidateAnalysisIdentity.StableId("candidate-delivered-link-evidence", factId.Value, "winner")]);
                }
            }
        }
    }

    private static IEnumerable<CandidateDeliveredFaceGenFactContract> FaceGenFacts(
        BethesdaSemanticSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        foreach (BethesdaFaceGenFact fact in snapshot.FaceGen.OrderBy(item => item.NpcParticipantId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpaqueId factId = CandidateAnalysisIdentity.StableId("candidate-delivered-facegen-fact", fact.NpcParticipantId);
            yield return new(
                factId, SourceId("record", fact.NpcParticipantId), Applicability(fact.Applicability),
                SourceId("asset", fact.Mesh.NormalizedRelativePath), Availability(fact.Mesh.Availability),
                fact.Mesh.WinnerParticipantId is null ? null : SourceId("provider", fact.Mesh.WinnerParticipantId),
                SourceId("asset", fact.Tint.NormalizedRelativePath), Availability(fact.Tint.Availability),
                fact.Tint.WinnerParticipantId is null ? null : SourceId("provider", fact.Tint.WinnerParticipantId),
                fact.Mesh.WinnerParticipantId is null && fact.Tint.WinnerParticipantId is null ? 0 : 1,
                (fact.Mesh.Availability == BethesdaAssetAvailability.Unknown ? 0 : 1)
                    + (fact.Tint.Availability == BethesdaAssetAvailability.Unknown ? 0 : 1),
                [snapshot.SourceSnapshotId, factId],
                [CandidateAnalysisIdentity.StableId("candidate-delivered-facegen-evidence", factId.Value)]);
        }
    }

    private static IEnumerable<CandidateDeliveredCoverageGapFactContract> GapFacts(
        BethesdaSemanticSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        foreach (BethesdaCoverageGap gap in snapshot.Gaps.OrderBy(item => item.GapId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpaqueId factId = CandidateAnalysisIdentity.StableId("candidate-delivered-gap-fact", gap.GapId);
            yield return new(factId, SourceId("population", gap.Population), gap.Denominator, gap.MissingCapability, gap.Reason,
                [snapshot.SourceSnapshotId, factId], [CandidateAnalysisIdentity.StableId("candidate-delivered-gap-evidence", factId.Value)]);
        }
    }

    private static IEnumerable<CandidateDeliveredDocumentationFactContract> DocumentationFacts(
        DocumentationEvidenceContract evidence,
        CancellationToken cancellationToken)
    {
        Dictionary<OpaqueId, DocumentationClaimContract> claims = evidence.Claims.ToDictionary(item => item.ClaimId);
        Dictionary<OpaqueId, DocumentationPassageContract> passages = evidence.Passages.ToDictionary(item => item.PassageId);
        Dictionary<OpaqueId, DocumentationRevisionContract> revisions = evidence.Revisions.ToDictionary(item => item.RevisionId);
        foreach (ClaimApplicationContract application in evidence.Applications.OrderBy(item => item.ApplicationId.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentationClaimContract claim = claims[application.ClaimId];
            DocumentationPassageContract passage = passages[claim.PassageId];
            DocumentationRevisionContract revision = revisions[passage.RevisionId];
            yield return new(application.ApplicationId, application.ApplicationId, claim.ClaimId, claim.PassageId,
                revision.RevisionId, application.SubjectId, application.ConsumingRunId, revision.SupplyingSnapshotId,
                application.AnalysisContextId, application.Applicability,
                [evidence.PayloadId, application.DependencyClosureId, claim.PassageId, revision.RevisionId],
                application.EvidenceIds.Count == 0 ? [claim.ClaimId] : application.EvidenceIds,
                claim.ContradictingEvidenceIds);
        }
    }

    private static CandidateDeliveredLinkState LinkState(BethesdaLinkFact? fact) => fact?.State switch
    {
        null => CandidateDeliveredLinkState.Absent,
        BethesdaLinkState.Null => CandidateDeliveredLinkState.Null,
        BethesdaLinkState.Resolved => CandidateDeliveredLinkState.Resolved,
        BethesdaLinkState.Unresolved => CandidateDeliveredLinkState.Unresolved,
        _ => throw new InvalidDataException("A Bethesda link fact has an unspecified state."),
    };

    private static OpaqueId? Target(BethesdaLinkFact? fact) => fact?.State == BethesdaLinkState.Resolved
        ? SourceId("target", fact.TargetParticipantId ?? fact.TargetFormKey
            ?? throw new InvalidDataException("A resolved Bethesda link lacks a target identity."))
        : null;

    private static CandidateDeliveredFaceGenApplicability Applicability(BethesdaFaceGenApplicability value) => value switch
    {
        BethesdaFaceGenApplicability.Applicable => CandidateDeliveredFaceGenApplicability.Applicable,
        BethesdaFaceGenApplicability.NotApplicableDeletedWinner or BethesdaFaceGenApplicability.NotApplicableTemplateTraits
            or BethesdaFaceGenApplicability.NotApplicableRaceWithoutFaceGenHead => CandidateDeliveredFaceGenApplicability.NotApplicable,
        BethesdaFaceGenApplicability.UnknownRace or BethesdaFaceGenApplicability.UnknownTemplateTraitsDecision => CandidateDeliveredFaceGenApplicability.Unknown,
        _ => throw new InvalidDataException("A FaceGen fact has an unspecified applicability state."),
    };

    private static CandidateDeliveredAssetAvailability Availability(BethesdaAssetAvailability value) => value switch
    {
        BethesdaAssetAvailability.Present => CandidateDeliveredAssetAvailability.Present,
        BethesdaAssetAvailability.Absent => CandidateDeliveredAssetAvailability.Absent,
        BethesdaAssetAvailability.Unknown => CandidateDeliveredAssetAvailability.Unknown,
        _ => throw new InvalidDataException("A FaceGen asset fact has an unspecified availability state."),
    };

    private static string LinkKey(BethesdaLinkFact value) => $"{value.Field}|{value.Component ?? "none"}|{value.Ordinal}";
    private static OpaqueId SourceId(string kind, string value) => CandidateAnalysisIdentity.StableId("candidate-delivered-source", kind, value);
    private static string Token(string value) => new(value.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray());
}
