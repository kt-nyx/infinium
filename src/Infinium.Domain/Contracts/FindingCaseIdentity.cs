namespace Infinium.Domain.Contracts;

public static class FindingCaseIdentity
{
    public static OpaqueId SharedCauseDependencyClosureId(IEnumerable<OpaqueId> dependencyIds) =>
        CandidateAnalysisIdentity.StableId(
            "shared-cause-dependency-closure",
            dependencyIds.Select(item => item.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());

    public static Sha256Fingerprint ComputeIdentitySignature(IdentityEnvelopeContract value) =>
        CandidateAnalysisIdentity.StructuralHash(
        [
            value.AnalyzerFamily,
            value.AnalyzerVersion.ToString(),
            value.SemanticContractVersion.ToString(),
            value.IdentityContractVersion.ToString(),
            CandidateAnalysisIdentity.FramedSequence(
                "participants",
                value.ParticipantsAndRoles.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => $"{item.Key}:{item.Value}")),
            value.CausalCondition,
            value.AffectedLocus,
            CandidateAnalysisIdentity.FramedSequence(
                "applicability",
                value.ApplicabilityPredicates.Order(StringComparer.Ordinal)),
            value.DependencyClosureId.Value,
        ]);

    public static OpaqueId EnvelopeId(IdentityEnvelopeContract value) =>
        CandidateAnalysisIdentity.StableId("identity-envelope", ComputeIdentitySignature(value).Value);

    private static Sha256Fingerprint ComputeSemanticIdentitySignature(IdentityEnvelopeContract value) =>
        CandidateAnalysisIdentity.StructuralHash(
        [
            CandidateAnalysisIdentity.FramedSequence(
                "participants",
                value.ParticipantsAndRoles.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => $"{item.Key}:{item.Value}")),
            value.CausalCondition,
            value.AffectedLocus,
            CandidateAnalysisIdentity.FramedSequence(
                "applicability",
                value.ApplicabilityPredicates.Order(StringComparer.Ordinal)),
            value.DependencyClosureId.Value,
        ]);

    public static Sha256Fingerprint FindingSemanticFingerprint(
        string conclusion,
        FindingSeverity severity,
        AnalysisConfidence confidence,
        IdentityEnvelopeContract identity,
        IEnumerable<string> taxonomySemantics) =>
        CandidateAnalysisIdentity.StructuralHash(
        [
            conclusion,
            severity.ToString(),
            confidence.ToString(),
            ComputeSemanticIdentitySignature(identity).Value,
            CandidateAnalysisIdentity.FramedSequence(
                "taxonomy",
                taxonomySemantics.Order(StringComparer.Ordinal)),
        ]);

    public static string TaxonomySemanticDescriptor(TaxonomyAssignmentContract value) =>
        string.Join("|", value.TaxonomyId, value.TaxonomyVersion, value.Axis, value.Facet,
            value.Code ?? "<open>", value.Applicability, value.Role?.ToString() ?? "<none>");

    public static Sha256Fingerprint CaseSemanticFingerprint(
        CaseOccurrenceKind kind,
        IdentityEnvelopeContract identity,
        IEnumerable<OpaqueId> logicalFindingIds) =>
        CandidateAnalysisIdentity.StructuralHash(
        [
            kind.ToString(),
            ComputeSemanticIdentitySignature(identity).Value,
            CandidateAnalysisIdentity.FramedSequence(
                "findings",
                logicalFindingIds.Select(item => item.Value).Order(StringComparer.Ordinal)),
        ]);

    public static OpaqueId ComputeInputId(FindingCaseInputContract value) =>
        CandidateAnalysisIdentity.StableId(
            "finding-case-input",
            value.OriginatingRunId.Value,
            value.PromotionPolicyId.Value,
            value.PromotionPolicyVersion.ToString(),
            value.ReconciliationPolicyId.Value,
            value.ReconciliationPolicyVersion.ToString(),
            value.ReconciliationActorId.Value,
            value.AssessmentTime.ToString(),
            value.CandidateAnalysis.PayloadId.Value,
            ContractJsonSerializer.Fingerprint(value.FindingEvidenceFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.FindingRecommendationFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.SharedCauseProofs.OrderBy(item => item.ProofId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.TaxonomyClassificationFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.TaxonomyProjectionInputs.OrderBy(item => item.SourceClassificationFactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.CoveragePopulationFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.CoverageMemberFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.CoverageFailureFacts.OrderBy(item => item.FailureId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.PriorFindings.OrderBy(item => item.FindingOccurrenceId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.PriorCases.OrderBy(item => item.CaseOccurrenceId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.ProducerCompatibilities.OrderBy(item => item.CompatibilityId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.RelatedFindingFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.ReconciliationCandidateFacts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal)).Value,
            ContractJsonSerializer.Fingerprint(value.Boundaries.OrderBy(item => item.BoundaryId, StringComparer.Ordinal)).Value);

    public static OpaqueId ComputePayloadId(FindingCaseContract value) =>
        CandidateAnalysisIdentity.StableId(
            "finding-case",
            value.OriginatingRunId.Value,
            value.InputId.Value,
            value.PromotionPolicyId.Value,
            value.PromotionPolicyVersion.ToString(),
            value.ReconciliationPolicyId.Value,
            value.ReconciliationPolicyVersion.ToString(),
            ContractJsonSerializer.Fingerprint(value.PromotionAssessments).Value,
            ContractJsonSerializer.Fingerprint(value.Abstentions).Value,
            ContractJsonSerializer.Fingerprint(value.Findings).Value,
            ContractJsonSerializer.Fingerprint(value.Recommendations).Value,
            ContractJsonSerializer.Fingerprint(value.Cases).Value,
            ContractJsonSerializer.Fingerprint(value.ReconciliationAssessments).Value,
            ContractJsonSerializer.Fingerprint(value.LineageEvents).Value,
            ContractJsonSerializer.Fingerprint(value.TaxonomyAssignments).Value,
            ContractJsonSerializer.Fingerprint(value.TaxonomyProjections).Value,
            ContractJsonSerializer.Fingerprint(value.Coverage).Value,
            ContractJsonSerializer.Fingerprint(value.CoverageFailures).Value,
            ContractJsonSerializer.Fingerprint(value.Gaps).Value,
            ContractJsonSerializer.Fingerprint(value.Boundaries).Value,
            value.PublicationClaimBoundary);
}

public static class FindingCaseContractInvariants
{
    public static CoverageState ExpectedCoverageState(IReadOnlyList<CoverageMemberState> states)
    {
        long completed = states.Count(item => item == CoverageMemberState.Completed);
        return states.Count == 0 || completed == states.Count ? CoverageState.Completed
            : states.All(item => item == CoverageMemberState.SkippedByConfiguration) ? CoverageState.SkippedByConfiguration
            : states.All(item => item == CoverageMemberState.SkippedByLimit) ? CoverageState.SkippedByLimit
            : states.All(item => item == CoverageMemberState.Unsupported) ? CoverageState.Unsupported
            : states.All(item => item == CoverageMemberState.Failed) ? CoverageState.Failed
            : CoverageState.CompletedWithGaps;
    }

    public static (FindingGapState State, GapReplayEffect Replay, GapConclusionEffect Conclusion)
        ExpectedCoverageGapShape(IReadOnlyList<CoverageMemberState> states)
    {
        CoverageMemberState state = states.Any(item => item == CoverageMemberState.Failed)
            ? CoverageMemberState.Failed
            : states.Any(item => item == CoverageMemberState.Unsupported)
                ? CoverageMemberState.Unsupported
                : states.Any(item => item == CoverageMemberState.SkippedByLimit)
                    ? CoverageMemberState.SkippedByLimit
                    : CoverageMemberState.CompletedWithGaps;
        return state switch
        {
            CoverageMemberState.Failed => (FindingGapState.Failed, GapReplayEffect.Partial, GapConclusionEffect.Unavailable),
            CoverageMemberState.Unsupported => (FindingGapState.Unsupported, GapReplayEffect.Unavailable, GapConclusionEffect.Abstain),
            CoverageMemberState.SkippedByLimit => (FindingGapState.Limited, GapReplayEffect.Partial, GapConclusionEffect.Bounded),
            _ => (FindingGapState.MissingInformation, GapReplayEffect.Partial, GapConclusionEffect.Bounded),
        };
    }

    public static FindingPromotionOutcome ExpectedPromotionOutcome(
        bool statePresent,
        bool confidenceAtLeastPlausible,
        bool hasSupportingEvidence,
        bool hasNoDefeatingContradictions,
        bool hasNoMissingInformation,
        bool severityClosed,
        bool identityClosed,
        bool conclusionAvailable,
        bool leadEligibleState)
    {
        bool supported = statePresent && confidenceAtLeastPlausible && hasSupportingEvidence
            && hasNoDefeatingContradictions && hasNoMissingInformation && severityClosed && identityClosed
            && conclusionAvailable && leadEligibleState;
        return supported ? FindingPromotionOutcome.SupportedFinding
            : conclusionAvailable && hasSupportingEvidence && leadEligibleState
                ? FindingPromotionOutcome.LeadOnly
                : FindingPromotionOutcome.Abstained;
    }

    public static bool HasUnknownGate(ReconciliationGatesContract value) =>
        value.Causal == ReconciliationGateState.Unknown
        || value.Applicability == ReconciliationGateState.Unknown
        || value.Dependency == ReconciliationGateState.Unknown
        || value.Producer == ReconciliationGateState.Unknown;

    public static bool HasDifferentGate(ReconciliationGatesContract value) =>
        value.Causal == ReconciliationGateState.ProvenDifferent
        || value.Applicability == ReconciliationGateState.ProvenDifferent
        || value.Dependency == ReconciliationGateState.ProvenDifferent
        || value.Producer == ReconciliationGateState.ProvenDifferent;

    public static void Validate(FindingCaseInputContract value)
    {
        if (!StringComparer.Ordinal.Equals(value.SchemaId, ContractConstants.FindingCaseInputSchemaId)
            || value.SchemaVersion != new ContractVersion(1, 0, 0))
        {
            throw new InvalidOperationException("Finding/case inputs require the current exact schema identity.");
        }
        Slice5ContractInvariants.Validate(value.CandidateAnalysis);
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(value.Boundaries, requireNotUsed: true);
        if (value.OriginatingRunId != value.CandidateAnalysis.OriginatingRunId
            || value.PromotionPolicyVersion != new ContractVersion(1, 0, 0)
            || value.ReconciliationPolicyVersion.Major < 1
            || value.InputId != FindingCaseIdentity.ComputeInputId(value))
        {
            throw new InvalidOperationException("Finding/case input identity must bind the exact run, policy, and upstream aggregate.");
        }
        Dictionary<OpaqueId, CandidateHypothesisContract> hypotheses = value.CandidateAnalysis.Hypotheses
            .ToDictionary(item => item.HypothesisId);
        Dictionary<OpaqueId, CandidateAnalysisEntryContract> candidates = value.CandidateAnalysis.Candidates
            .ToDictionary(item => item.CandidateId);
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = value.CandidateAnalysis.Decisions
            .ToDictionary(item => item.DecisionId);
        Dictionary<OpaqueId, CandidateAnalyzerBindingContract> analyzerBindings = value.CandidateAnalysis.AnalyzerBindings
            .ToDictionary(item => item.AnalyzerId);
        RequireUnique(value.FindingEvidenceFacts.Select(item => item.FactId), "finding evidence facts");
        RequireUnique(value.FindingEvidenceFacts.Select(item => item.HypothesisId), "finding evidence hypotheses");
        foreach (FindingEvidenceFactContract fact in value.FindingEvidenceFacts)
        {
            if (!hypotheses.TryGetValue(fact.HypothesisId, out CandidateHypothesisContract? hypothesis)
                || (fact.WorstCredibleConsequence == WorstCredibleConsequence.Unspecified
                    && hypothesis.State == Slice5ResultState.Present)
                || string.IsNullOrWhiteSpace(fact.AffectedLocus)
                || string.IsNullOrWhiteSpace(fact.CausalCondition)
                || fact.ApplicabilityPredicates.Count == 0
                || fact.EvidenceIds.Count == 0
                || fact.EvidenceIds.Any(id => !hypothesis.SupportingEvidenceIds.Contains(id))
                || !hypothesis.ContradictingEvidenceIds.ToHashSet().SetEquals(
                    fact.DefeatingContradictionIds.Concat(fact.RetainedNonDefeatingContradictionIds))
                || fact.DefeatingContradictionIds.Intersect(fact.RetainedNonDefeatingContradictionIds).Any())
            {
                throw new InvalidOperationException("Finding evidence facts must be answer-free, hypothesis-bound, consequence-grounded, and evidence-closed.");
            }
        }
        RequireUnique(value.FindingRecommendationFacts.Select(item => item.FactId), "finding recommendation facts");
        RequireUnique(value.FindingRecommendationFacts.Select(item => item.HypothesisId), "finding recommendation hypotheses");
        if (!value.FindingRecommendationFacts.Select(item => item.HypothesisId).ToHashSet()
                .SetEquals(value.FindingEvidenceFacts.Select(item => item.HypothesisId))
            || value.FindingRecommendationFacts.Any(fact =>
                !hypotheses.TryGetValue(fact.HypothesisId, out CandidateHypothesisContract? hypothesis)
                || fact.Kind == RecommendationKind.Unspecified
                || string.IsNullOrWhiteSpace(fact.Action)
                || string.IsNullOrWhiteSpace(fact.Uncertainty)
                || string.IsNullOrWhiteSpace(fact.Reversibility)
                || string.IsNullOrWhiteSpace(fact.Verification)
                || fact.Risks.Count == 0 || fact.Risks.Any(string.IsNullOrWhiteSpace)
                || fact.EvidenceIds.Count == 0
                || fact.EvidenceIds.Any(id => !hypothesis.SupportingEvidenceIds.Contains(id)
                    && !hypothesis.ContradictingEvidenceIds.Contains(id))))
        {
            throw new InvalidOperationException("Recommendation source facts must be one-to-one, typed, evidence-bound, and include verification.");
        }
        RequireUnique(value.SharedCauseProofs.Select(item => item.ProofId), "shared cause proofs");
        if (value.SharedCauseProofs.SelectMany(item => item.HypothesisIds)
            .GroupBy(id => id).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Each hypothesis may participate in at most one admitted shared-cause proof.");
        }
        foreach (SharedCauseProofContract proof in value.SharedCauseProofs)
        {
            CandidateDecisionContract[] memberDecisions = proof.HypothesisIds.Select(id =>
                decisions[candidates[hypotheses[id].CandidateId].DecisionId]).ToArray();
            CandidateAnalyzerBindingContract[] memberBindings = memberDecisions.Select(decision =>
                analyzerBindings[decision.AnalyzerId]).ToArray();
            HashSet<string> proofLoci = proof.AffectedLocus.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> expectedLoci = proof.HypothesisIds.Select(id => value.FindingEvidenceFacts
                .Single(fact => fact.HypothesisId == id).AffectedLocus).ToHashSet(StringComparer.Ordinal);
            HashSet<string> expectedApplicability = proof.HypothesisIds.SelectMany(id => value.FindingEvidenceFacts
                .Single(fact => fact.HypothesisId == id).ApplicabilityPredicates).ToHashSet(StringComparer.Ordinal);
            Dictionary<string, string> memberParticipants = memberDecisions.SelectMany(item => item.Participants)
                .GroupBy(item => item.ParticipantId.Value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Role).Distinct(StringComparer.Ordinal).Single(),
                    StringComparer.Ordinal);
            if (proof.HypothesisIds.Count == 0 || proof.HypothesisIds.Distinct().Count() != proof.HypothesisIds.Count
                || proof.HypothesisIds.Any(id => !hypotheses.ContainsKey(id)) || proof.EvidenceIds.Count == 0
                || proof.EvidenceIds.Any(id => !proof.HypothesisIds.SelectMany(hypothesisId =>
                    hypotheses[hypothesisId].SupportingEvidenceIds).Contains(id))
                || memberBindings.Any(binding => !StringComparer.Ordinal.Equals(binding.AnalyzerFamily, proof.AnalyzerFamily))
                || memberDecisions.Any(decision => !decision.Participants.Any(member => proof.ParticipantsAndRoles.TryGetValue(
                        member.ParticipantId.Value, out string? role) && StringComparer.Ordinal.Equals(member.Role, role)))
                || proof.ParticipantsAndRoles.Any(participant => !memberParticipants.TryGetValue(
                    participant.Key, out string? role) || !StringComparer.Ordinal.Equals(participant.Value, role))
                || proof.ParticipantsAndRoles.Count != memberParticipants.Count
                || proof.HypothesisIds.Any(id => !StringComparer.Ordinal.Equals(proof.CausalCondition,
                    value.FindingEvidenceFacts.Single(fact => fact.HypothesisId == id).CausalCondition))
                || memberBindings.Any(binding => binding.AnalyzerVersion != proof.AnalyzerVersion
                    || binding.SemanticContractVersion != proof.SemanticContractVersion
                    || binding.IdentityContractVersion != proof.IdentityContractVersion)
                || proof.DependencyClosureId != FindingCaseIdentity.SharedCauseDependencyClosureId(
                    memberDecisions.SelectMany(item => item.DependencyIds))
                || !proofLoci.SetEquals(expectedLoci)
                || !proof.ApplicabilityPredicates.ToHashSet(StringComparer.Ordinal).SetEquals(expectedApplicability))
            {
                throw new InvalidOperationException("Shared-cause proof must bind exact member decisions, producer versions, loci, dependency closure, and independent evidence.");
            }
            IdentityEnvelopeContract proofIdentity = new(
                proof.AnalyzerFamily, proof.SemanticContractVersion, proof.IdentityContractVersion,
                proof.ParticipantsAndRoles, proof.CausalCondition, proof.AffectedLocus,
                proof.ApplicabilityPredicates, proof.DependencyClosureId, new Sha256Fingerprint(new string('0', 64)))
            {
                AnalyzerVersion = proof.AnalyzerVersion,
            };
            proofIdentity = proofIdentity with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(proofIdentity) };
            ValidateIdentity(proofIdentity, allowOpen: false);
        }
        RequireUnique(value.TaxonomyClassificationFacts.Select(item => item.FactId), "taxonomy classification facts");
        foreach (TaxonomyClassificationFactContract fact in value.TaxonomyClassificationFacts)
        {
            if (!hypotheses.ContainsKey(fact.HypothesisId) || string.IsNullOrWhiteSpace(fact.TaxonomyId)
                || string.IsNullOrWhiteSpace(fact.Axis) || string.IsNullOrWhiteSpace(fact.Facet)
                || fact.Applicability == TaxonomyApplicability.Unspecified
                || (fact.Applicability == TaxonomyApplicability.Assigned) != (fact.Code is not null)
                || (fact.Applicability == TaxonomyApplicability.Assigned && fact.Role is null)
                || (fact.EvidenceIds.Count == 0 && fact.Applicability != TaxonomyApplicability.Unknown)
                || string.IsNullOrWhiteSpace(fact.Reason)
                || !IsCanonicalProductTaxonomy(fact.TaxonomyId, fact.TaxonomyVersion,
                    fact.Axis, fact.Facet, fact.Code))
            {
                throw new InvalidOperationException("Taxonomy facts require closed subject, state, role when assigned, and provenance.");
            }
        }
        OpaqueId[] retainedAssignmentIds = value.TaxonomyClassificationFacts
            .Where(item => item.SourceAssignmentId is not null).Select(item => item.SourceAssignmentId!).ToArray();
        RequireUnique(retainedAssignmentIds, "retained taxonomy assignment identities");
        if (value.TaxonomyClassificationFacts.Any(fact => fact.SupersedesAssignmentId is not null
            && !value.TaxonomyClassificationFacts.Any(source => source.SourceAssignmentId == fact.SupersedesAssignmentId
                && source.HypothesisId == fact.HypothesisId
                && source.SourceAssignmentId != fact.SourceAssignmentId)))
        {
            throw new InvalidOperationException("Direct taxonomy supersession must resolve a distinct retained assignment for the same subject.");
        }
        RequireUnique(value.TaxonomyProjectionInputs.Select(item => CandidateAnalysisIdentity.StableId(
            "taxonomy-projection-input", item.SourceClassificationFactId.Value, item.TargetTaxonomyId,
            item.TargetTaxonomyVersion.ToString(), item.TargetAxis, item.TargetFacet, item.TargetCode ?? "<open>")),
            "taxonomy projection inputs");
        foreach (TaxonomyProjectionInputContract projection in value.TaxonomyProjectionInputs)
        {
            TaxonomyClassificationFactContract? source = value.TaxonomyClassificationFacts.SingleOrDefault(
                item => item.FactId == projection.SourceClassificationFactId);
            ClassificationRole? effectiveRole = projection.TargetRole ?? source?.Role;
            if (source is null || string.IsNullOrWhiteSpace(projection.TargetTaxonomyId)
                || string.IsNullOrWhiteSpace(projection.TargetAxis) || string.IsNullOrWhiteSpace(projection.TargetFacet)
                || projection.TargetApplicability == TaxonomyApplicability.Unspecified
                || (projection.TargetApplicability == TaxonomyApplicability.Assigned) != (projection.TargetCode is not null)
                || (projection.TargetApplicability == TaxonomyApplicability.Assigned && effectiveRole is null)
                || projection.EvidenceIds.Count == 0 || string.IsNullOrWhiteSpace(projection.Reason)
                || !IsCanonicalProductTaxonomy(projection.TargetTaxonomyId, projection.TargetTaxonomyVersion,
                    projection.TargetAxis, projection.TargetFacet, projection.TargetCode))
            {
                throw new InvalidOperationException("Taxonomy projection inputs require a resolved source and closed target semantics and mapping provenance.");
            }
        }
        RequireUnique(value.CoveragePopulationFacts.Select(item => item.FactId), "coverage population facts");
        if (value.CoveragePopulationFacts.GroupBy(item => item.PopulationId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || value.CoveragePopulationFacts.Any(item => string.IsNullOrWhiteSpace(item.PopulationId)
                || string.IsNullOrWhiteSpace(item.DenominatorLabel)))
        {
            throw new InvalidOperationException("Coverage population facts require one exact definition per population.");
        }
        HashSet<string> coveragePopulations = value.CoveragePopulationFacts.Select(item => item.PopulationId).ToHashSet(StringComparer.Ordinal);
        Dictionary<string, CoveragePopulationFactContract> coverageDefinitions = value.CoveragePopulationFacts
            .ToDictionary(item => item.PopulationId, StringComparer.Ordinal);
        RequireUnique(value.CoverageMemberFacts.Select(item => item.FactId), "coverage member facts");
        if (value.CoverageMemberFacts.GroupBy(item => (item.PopulationId, item.MemberId)).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Coverage ledgers cannot count the same member twice in one population.");
        }
        RequireUnique(value.CoverageFailureFacts.Select(item => item.FailureId), "coverage failures");
        HashSet<OpaqueId> coverageFailures = value.CoverageFailureFacts.Select(item => item.FailureId).ToHashSet();
        if (value.CoverageMemberFacts.Where(item => item.GapId is not null).GroupBy(item => item.GapId!)
                .Any(group => group.Select(item => item.PopulationId).Distinct(StringComparer.Ordinal).Count() != 1)
            || value.CoverageFailureFacts.Any(failure =>
                !value.CoverageMemberFacts.Any(member => member.FailureId == failure.FailureId)
                || string.IsNullOrWhiteSpace(failure.FailureCode) || string.IsNullOrWhiteSpace(failure.Message)))
        {
            throw new InvalidOperationException("Coverage gap and failure facts must be population-scoped and referenced by the exact member ledger.");
        }
        foreach (CoverageMemberFactContract fact in value.CoverageMemberFacts)
        {
            if (fact.State == CoverageMemberState.Unspecified || !coveragePopulations.Contains(fact.PopulationId)
                || string.IsNullOrWhiteSpace(fact.DenominatorLabel)
                || (coverageDefinitions.TryGetValue(fact.PopulationId, out CoveragePopulationFactContract? definition)
                    && (definition.AnalyzerId != fact.AnalyzerId
                        || !StringComparer.Ordinal.Equals(definition.DenominatorLabel, fact.DenominatorLabel)))
                || (fact.State == CoverageMemberState.Failed) != (fact.FailureId is not null)
                || ((fact.State is CoverageMemberState.CompletedWithGaps or CoverageMemberState.Unsupported)
                    && fact.GapId is null)
                || (fact.State is CoverageMemberState.Completed or CoverageMemberState.SkippedByConfiguration
                    && fact.GapId is not null)
                || (fact.FailureId is not null && !coverageFailures.Contains(fact.FailureId))
                || (fact.FailureId is not null && value.CoverageFailureFacts.Single(item =>
                    item.FailureId == fact.FailureId).AnalyzerId != fact.AnalyzerId)
                || fact.TaxonomyClassificationFactIds.Any(id =>
                    !value.TaxonomyClassificationFacts.Any(taxonomy => taxonomy.FactId == id)))
            {
                throw new InvalidOperationException("Coverage member facts require exact member state and closed failure references.");
            }
        }
        RequireUnique(value.PriorFindings.Select(item => item.FindingOccurrenceId), "prior findings");
        RequireUnique(value.PriorFindings.Select(item => item.LogicalFindingId), "prior logical findings");
        RequireUnique(value.PriorCases.Select(item => item.CaseOccurrenceId), "prior cases");
        RequireUnique(value.PriorCases.Select(item => item.LogicalCaseId), "prior logical cases");
        foreach (PriorFindingContract prior in value.PriorFindings)
        {
            ValidateIdentity(prior.IdentityEnvelope, allowOpen: !prior.ProofAvailable);
        }
        foreach (PriorCaseContract prior in value.PriorCases)
        {
            ValidateIdentity(prior.IdentityEnvelope, allowOpen: !prior.ProofAvailable);
            bool supported = prior.Kind == CaseOccurrenceKind.Supported;
            PriorFindingContract[] members = prior.FindingOccurrenceIds.Select(id =>
                value.PriorFindings.SingleOrDefault(item => item.FindingOccurrenceId == id)!).ToArray();
            if (prior.Kind == CaseOccurrenceKind.Unspecified
                || prior.FindingOccurrenceIds.Distinct().Count() != prior.FindingOccurrenceIds.Count
                || prior.HypothesisIds.Count == 0
                || prior.HypothesisIds.Distinct().Count() != prior.HypothesisIds.Count
                || (supported && (prior.FindingOccurrenceIds.Count == 0 || members.Any(item => item is null)
                    || !prior.HypothesisIds.ToHashSet().SetEquals(members.Select(item => item.HypothesisId))))
                || (!supported && prior.FindingOccurrenceIds.Count != 0))
            {
                throw new InvalidOperationException("Prior cases require kind-consistent closed finding and hypothesis membership.");
            }
        }
        RequireUnique(value.ProducerCompatibilities.Select(item => item.CompatibilityId), "producer compatibility declarations");
        if (value.ProducerCompatibilities.Any(item => item.EvidenceIds.Count == 0)
            || value.ProducerCompatibilities.GroupBy(ProducerCompatibilityKey).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Producer compatibility declarations require one proof-bearing row per exact producer tuple.");
        }
        RequireUnique(value.RelatedFindingFacts.Select(item => item.FactId), "related finding facts");
        if (value.RelatedFindingFacts.GroupBy(item => (item.CurrentHypothesisId, item.PriorOccurrenceId))
                .Any(group => group.Count() != 1)
            || value.RelatedFindingFacts.Any(item => !hypotheses.ContainsKey(item.CurrentHypothesisId)
                || !value.PriorFindings.Any(prior => prior.FindingOccurrenceId == item.PriorOccurrenceId)
                || item.EvidenceIds.Count == 0 || string.IsNullOrWhiteSpace(item.Reason)
                || !value.ReconciliationCandidateFacts.Any(scope => scope.CurrentHypothesisId == item.CurrentHypothesisId
                    && scope.PriorOccurrenceIds.Contains(item.PriorOccurrenceId))
                || item.EvidenceIds.Any(id => !hypotheses[item.CurrentHypothesisId].SupportingEvidenceIds.Contains(id)
                    && !hypotheses[item.CurrentHypothesisId].ContradictingEvidenceIds.Contains(id))))
        {
            throw new InvalidOperationException("Related-finding facts require one explicit evidence-bound current/prior relation.");
        }
        RequireUnique(value.ReconciliationCandidateFacts.Select(item => item.FactId), "reconciliation candidate facts");
        if ((value.PriorFindings.Count > 0 && hypotheses.Keys.Any(id =>
                value.ReconciliationCandidateFacts.Count(item => item.CurrentHypothesisId == id) != 1))
            || (value.PriorFindings.Count == 0 && value.ReconciliationCandidateFacts.Count > 0)
            || value.ReconciliationCandidateFacts.GroupBy(item => item.CurrentHypothesisId).Any(group => group.Count() != 1)
            || value.ReconciliationCandidateFacts.Any(item => !hypotheses.ContainsKey(item.CurrentHypothesisId)
                || item.PriorOccurrenceIds.Distinct().Count() != item.PriorOccurrenceIds.Count
                || item.PriorOccurrenceIds.Any(id => !value.PriorFindings.Any(prior => prior.FindingOccurrenceId == id))))
        {
            throw new InvalidOperationException("Reconciliation candidate facts must close each declared current/prior comparison scope.");
        }
    }

    public static void ValidateIdentity(IdentityEnvelopeContract value, bool allowOpen)
    {
        bool open = string.IsNullOrWhiteSpace(value.AnalyzerFamily)
            || value.AnalyzerVersion.Major < 1
            || value.SemanticContractVersion.Major < 1
            || value.IdentityContractVersion.Major < 1
            || value.ParticipantsAndRoles.Count == 0
            || value.ParticipantsAndRoles.Any(item => string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
            || string.IsNullOrWhiteSpace(value.CausalCondition)
            || string.IsNullOrWhiteSpace(value.AffectedLocus)
            || value.ApplicabilityPredicates.Count == 0
            || value.ApplicabilityPredicates.Any(string.IsNullOrWhiteSpace)
            || value.DependencyClosureId.Value.EndsWith("unspecified", StringComparison.Ordinal);
        if (!allowOpen && open)
        {
            throw new InvalidOperationException("Identity envelopes require closed producer, causal, locus, applicability, participant, and dependency identity.");
        }
        if (!open && value.CanonicalSignature != FindingCaseIdentity.ComputeIdentitySignature(value))
        {
            throw new InvalidOperationException("Identity signatures are retrieval aids and must exactly cover the typed envelope.");
        }
    }

    public static void ValidateTaxonomy(IReadOnlyList<TaxonomyAssignmentContract> assignments)
    {
        RequireUnique(assignments.Select(item => item.AssignmentId), "taxonomy assignments");
        foreach (TaxonomyAssignmentContract assignment in assignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.TaxonomyId)
                || string.IsNullOrWhiteSpace(assignment.Axis)
                || string.IsNullOrWhiteSpace(assignment.Facet)
                || assignment.Role == ClassificationRole.Unspecified
                || assignment.Applicability == TaxonomyApplicability.Unspecified
                || (assignment.Applicability == TaxonomyApplicability.Assigned) != (assignment.Code is not null)
                || (assignment.Applicability == TaxonomyApplicability.Assigned && assignment.Role is null)
                || (assignment.EvidenceIds.Count == 0 && assignment.Applicability != TaxonomyApplicability.Unknown)
                || string.IsNullOrWhiteSpace(assignment.Reason)
                || !IsCanonicalProductTaxonomy(assignment.TaxonomyId, assignment.TaxonomyVersion,
                    assignment.Axis, assignment.Facet, assignment.Code))
            {
                throw new InvalidOperationException("Taxonomy assignments must keep taxonomy identity, axis, role, and applicability state separate and closed.");
            }
        }
    }

    public static void ValidateCoverage(
        IReadOnlyList<CoverageContract> coverage,
        IReadOnlyList<FindingCaseGapContract> gaps,
        IReadOnlyList<TaxonomyAssignmentContract> assignments)
    {
        RequireUnique(coverage.Select(item => item.CoverageId), "coverage ledgers");
        RequireUnique(gaps.Select(item => item.GapId), "finding/case gaps");
        HashSet<OpaqueId> gapIds = gaps.Select(item => item.GapId).ToHashSet();
        HashSet<OpaqueId> assignmentIds = assignments.Select(item => item.AssignmentId).ToHashSet();
        foreach (CoverageContract item in coverage)
        {
            RequireUnique(item.MemberResults.Select(member => member.MemberId), $"coverage members for {item.PopulationId}");
            OpaqueId[] memberGapIds = item.MemberResults.Where(member => member.GapId is not null)
                .Select(member => member.GapId!).Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            OpaqueId[] memberFailureIds = item.MemberResults.Where(member => member.FailureId is not null)
                .Select(member => member.FailureId!).Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            OpaqueId[] memberTaxonomyIds = item.MemberResults.SelectMany(member => member.TaxonomyAssignmentIds)
                .Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            CoverageExclusionContract[] expectedExclusions = item.MemberResults
                .Where(member => member.State is CoverageMemberState.SkippedByConfiguration or CoverageMemberState.SkippedByLimit)
                .Select(member => new CoverageExclusionContract(member.MemberId, member.Reason, member.State))
                .OrderBy(member => member.MemberId.Value, StringComparer.Ordinal).ToArray();
            if (item.State == CoverageState.Unspecified
                || item.Denominator < 0
                || item.CompletedCount < 0
                || item.CompletedCount > item.Denominator
                || item.Denominator != item.MemberResults.Count
                || item.CompletedCount != item.MemberResults.Count(member => member.State == CoverageMemberState.Completed)
                || item.GapIds.Any(id => !gapIds.Contains(id))
                || item.TaxonomyAssignmentIds.Any(id => !assignmentIds.Contains(id))
                || !item.GapIds.OrderBy(id => id.Value, StringComparer.Ordinal).SequenceEqual(memberGapIds)
                || !item.FailureIds.OrderBy(id => id.Value, StringComparer.Ordinal).SequenceEqual(memberFailureIds)
                || !item.TaxonomyAssignmentIds.OrderBy(id => id.Value, StringComparer.Ordinal).SequenceEqual(memberTaxonomyIds)
                || !item.Exclusions.OrderBy(member => member.MemberId.Value, StringComparer.Ordinal).SequenceEqual(expectedExclusions)
                || item.MemberResults.Any(member => member.State == CoverageMemberState.Unspecified
                    || string.IsNullOrWhiteSpace(member.Reason)
                    || string.IsNullOrWhiteSpace(member.MissingCapabilityOrInformation)
                    || (member.State == CoverageMemberState.Failed) != (member.FailureId is not null)
                    || (member.State is CoverageMemberState.Completed or CoverageMemberState.SkippedByConfiguration
                        && member.GapId is not null)
                    || member.TaxonomyAssignmentIds.Any(id => !assignmentIds.Contains(id))))
            {
                throw new InvalidOperationException("Coverage ledgers require an exact typed member ledger with derived counts and closed references.");
            }
            if (item.State != ExpectedCoverageState(item.MemberResults.Select(member => member.State).ToArray()))
            {
                throw new InvalidOperationException($"Coverage state {item.State} is inconsistent with its exact counts and gaps.");
            }
        }
        foreach (FindingCaseGapContract gap in gaps)
        {
            CoverageContract owner = coverage.SingleOrDefault(item => item.PopulationId == gap.PopulationId)
                ?? throw new InvalidOperationException("Every retained gap must resolve to one coverage population.");
            CoverageMemberResultContract[] members = owner.MemberResults.Where(item => item.GapId == gap.GapId).ToArray();
            (FindingGapState expectedState, GapReplayEffect expectedReplay, GapConclusionEffect expectedConclusion) =
                ExpectedCoverageGapShape(members.Select(item => item.State).ToArray());
            if (gap.State == FindingGapState.Unspecified
                || gap.ReplayEffect == GapReplayEffect.Unspecified
                || gap.ConclusionEffect == GapConclusionEffect.Unspecified
                || string.IsNullOrWhiteSpace(gap.PopulationId)
                || string.IsNullOrWhiteSpace(gap.StageId)
                || string.IsNullOrWhiteSpace(gap.Reason)
                || string.IsNullOrWhiteSpace(gap.MissingCapabilityOrInformation)
                || members.Length == 0
                || gap.State != expectedState
                || gap.ReplayEffect != expectedReplay
                || gap.ConclusionEffect != expectedConclusion
                || !gap.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal)
                    .SequenceEqual(members.Select(item => item.MemberId).OrderBy(item => item.Value, StringComparer.Ordinal)))
            {
                throw new InvalidOperationException("Gaps require typed population, stage, replay, and conclusion effects.");
            }
        }
    }

    private static void RequireUnique(IEnumerable<OpaqueId> ids, string label)
    {
        OpaqueId[] values = ids.ToArray();
        if (values.Distinct().Count() != values.Length)
        {
            throw new InvalidOperationException($"{label} require unique identities.");
        }
    }

    public static bool IsCanonicalProductTaxonomy(
        string taxonomyId, ContractVersion version, string axis, string facet, string? code)
    {
        if (!StringComparer.Ordinal.Equals(taxonomyId, ContractConstants.TaxonomyId))
        {
            return true;
        }
        if (version != ContractVersion.Parse(ContractConstants.TaxonomyVersion))
        {
            return false;
        }
        HashSet<string>? acceptedCodes = (axis, facet) switch
        {
            ("declared-purpose-and-intended-feature-area", "purpose-kind") => PurposeCodes,
            ("technical-modification-surface", "semantic-mechanism") => SurfaceCodes,
            ("technical-modification-surface", "realization-and-delivery") => DeliveryCodes,
            ("affected-game-system-or-content-area", "affected-area") => AreaCodes,
            ("consequence-type", "consequence-type") => ConsequenceCodes,
            ("effect-extent", "direct-subject-breadth") => SubjectExtentCodes,
            ("effect-extent", "spatial-breadth") => SpatialExtentCodes,
            ("effect-extent", "persistence-and-lifecycle-breadth") => PersistenceExtentCodes,
            ("effect-extent", "causal-propagation-or-blast-radius") => PropagationExtentCodes,
            _ => null,
        };
        return acceptedCodes is not null && (code is null || acceptedCodes.Contains(code));
    }

    private static (string, ContractVersion, ContractVersion, ContractVersion, string, ContractVersion, ContractVersion, ContractVersion)
        ProducerCompatibilityKey(ProducerCompatibilityContract item) =>
        (item.PriorAnalyzerFamily, item.PriorAnalyzerVersion, item.PriorSemanticContractVersion, item.PriorIdentityContractVersion,
            item.CurrentAnalyzerFamily, item.CurrentAnalyzerVersion, item.CurrentSemanticContractVersion, item.CurrentIdentityContractVersion);

    private static readonly HashSet<string> PurposeCodes =
    [
        "purpose.add-expand", "purpose.replace-overhaul", "purpose.modify-tune", "purpose.fix-restore",
        "purpose.integrate-patch", "purpose.configure-expose-choice", "purpose.generate-precompute",
        "purpose.provide-runtime-framework", "purpose.provide-tool-workflow", "purpose.remove-disable",
    ];
    private static readonly HashSet<string> SurfaceCodes =
    [
        "surface.plugin-data", "surface.asset", "surface.asset.model-geometry", "surface.asset.texture-material",
        "surface.asset.animation-behavior-morph", "surface.asset.audio-voice", "surface.asset.interface",
        "surface.asset.localization-text", "surface.logic", "surface.logic.compiled-papyrus",
        "surface.logic.native-runtime", "surface.configuration", "surface.configuration.game-profile",
        "surface.configuration.component", "surface.configuration.runtime-rule-dsl", "surface.runtime-support-data",
        "surface.generated", "surface.generated.plugin", "surface.generated.behavior-animation",
        "surface.generated.mesh-morph", "surface.generated.lod-terrain-object-visual",
        "surface.generated.grass-cell-cache", "surface.generated.runtime-consumed-sidecar",
    ];
    private static readonly HashSet<string> DeliveryCodes =
    [
        "delivery.plugin-container", "delivery.loose-data-file", "delivery.archive-member",
        "delivery.game-root-component", "delivery.profile-or-external-config", "delivery.mapped-or-secondary-root",
    ];
    private static readonly HashSet<string> AreaCodes =
    [
        "area.runtime-session", "area.runtime-session.bootstrap-loading", "area.runtime-session.save-persistence-lifecycle",
        "area.runtime-session.mod-framework-services", "area.player-progression", "area.actors",
        "area.actors.appearance-identity", "area.actors.ai-packages", "area.actors.factions-relationships",
        "area.quests", "area.quests.progression-objectives-aliases", "area.quests.dialogue-scenes-voice",
        "area.quests.radiant-story-events", "area.world", "area.world.cells-worldspaces-locations",
        "area.world.placed-objects-activation", "area.world.navigation-encounters",
        "area.world.landscape-water-weather-lighting-lod", "area.gameplay", "area.gameplay.combat-action",
        "area.gameplay.magic-effects", "area.gameplay.stealth-crime", "area.gameplay.items-inventory-economy",
        "area.gameplay.crafting", "area.interface-controls", "area.presentation", "area.presentation.visual",
        "area.presentation.animation", "area.presentation.audio-voice", "area.presentation.text-localization",
    ];
    private static readonly HashSet<string> ConsequenceCodes =
    [
        "consequence.execution-unavailable", "consequence.content-feature-unavailable",
        "consequence.incorrect-functional-behavior", "consequence.progression-access-blocked",
        "consequence.state-persistence-integrity", "consequence.stability-failure",
        "consequence.performance-resource-degradation", "consequence.presentation-incoherence",
        "consequence.usability-control-degradation", "consequence.reproducibility-maintenance-risk",
    ];
    private static readonly HashSet<string> SubjectExtentCodes =
    [
        "extent.subject.single-instance", "extent.subject.bounded-set", "extent.subject.type-or-category",
        "extent.subject.system-wide", "extent.subject.runtime-or-installation-wide",
    ];
    private static readonly HashSet<string> SpatialExtentCodes =
    [
        "extent.spatial.single-reference-or-point", "extent.spatial.cell-or-location",
        "extent.spatial.region-or-worldspace", "extent.spatial.world-global", "extent.spatial.nonspatial",
    ];
    private static readonly HashSet<string> PersistenceExtentCodes =
    [
        "extent.persistence.event-only", "extent.persistence.while-condition-holds",
        "extent.persistence.current-session", "extent.persistence.save-persistent",
        "extent.persistence.installation-persistent",
    ];
    private static readonly HashSet<string> PropagationExtentCodes =
    [
        "extent.propagation.isolated-output", "extent.propagation.bounded-dependents",
        "extent.propagation.feature-wide", "extent.propagation.cross-feature",
        "extent.propagation.cross-system", "extent.propagation.runtime-or-installation-wide",
    ];
}
