namespace Infinium.Domain.Contracts;

public static partial class DomainContractInvariants
{
    public static void Validate(RunOutputAggregateContract output)
    {
        ArgumentNullException.ThrowIfNull(output);
        RequireNonEmpty(output.AnalyzerDeclarations, nameof(output.AnalyzerDeclarations));
        RequireUnique(
            output.AnalyzerDeclarations.Select(value => value.AnalyzerId),
            nameof(output.AnalyzerDeclarations));
        foreach (AnalyzerDeclarationContract declaration in output.AnalyzerDeclarations)
        {
            Validate(declaration);
        }
        RequireUnique(
            output.TaxonomyAssignments.Select(value => value.AssignmentId.Value),
            nameof(output.TaxonomyAssignments));
        foreach (TaxonomyAssignmentContract assignment in output.TaxonomyAssignments)
        {
            Validate(assignment);
        }
        RequireUnique(output.Coverage.Select(value => value.CoverageId.Value), nameof(output.Coverage));
        RequireUnique(
            output.Coverage.Select(value => $"{value.AnalyzerId.Value}/{value.PopulationId}"),
            "analyzer coverage populations");

        RequireUnique(output.Findings.Select(value => value.OccurrenceId.Value), nameof(output.Findings));
        RequireUnique(output.SupportedCases.Select(value => value.OccurrenceId.Value), nameof(output.SupportedCases));
        RequireUnique(output.LeadOnlyCases.Select(value => value.OccurrenceId.Value), nameof(output.LeadOnlyCases));
        RequireUnique(
            output.SupportedCases.Concat(output.LeadOnlyCases).Select(value => value.OccurrenceId.Value),
            "all case occurrences");
        foreach (CaseOccurrenceContract supportedCase in output.SupportedCases)
        {
            Validate(supportedCase);
            if (supportedCase.Kind != CaseOccurrenceKind.Supported
                || supportedCase.OriginatingRunId != output.RunId)
            {
                throw new InvalidOperationException(
                    "Supported-case output must be supported and owned by the current run.");
            }
        }
        foreach (CaseOccurrenceContract leadOnlyCase in output.LeadOnlyCases)
        {
            Validate(leadOnlyCase);
            if (leadOnlyCase.Kind != CaseOccurrenceKind.LeadOnly
                || leadOnlyCase.OriginatingRunId != output.RunId)
            {
                throw new InvalidOperationException(
                    "Lead-only output must be lead-only and owned by the current run.");
            }
        }
        if (output.Findings.Any(value => value.OriginatingRunId != output.RunId))
        {
            throw new InvalidOperationException(
                "Finding occurrences must retain the current producing run.");
        }

        HashSet<string> findingIds = output.Findings
            .Select(value => value.OccurrenceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.SupportedCases.Concat(output.LeadOnlyCases)
            .SelectMany(value => value.FindingOccurrenceIds)
            .Any(value => !findingIds.Contains(value.Value)))
        {
            throw new InvalidOperationException("Case output references a finding outside the run output.");
        }
        HashSet<string> hypothesisIds = output.Hypotheses
            .Select(value => value.HypothesisId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.SupportedCases.Concat(output.LeadOnlyCases)
            .SelectMany(value => value.HypothesisIds)
            .Any(value => !hypothesisIds.Contains(value.Value)))
        {
            throw new InvalidOperationException("Case output references a hypothesis outside the run output.");
        }

        HashSet<string> externalClaimIds = output.ExternalClaims
            .Select(value => value.ClaimId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.DiscoveryLeads.Any(value => value.AdmittedExternalClaimId is not null
                && !externalClaimIds.Contains(value.AdmittedExternalClaimId.Value))
            || output.ExternalClaimApplicationLinks.Any(value =>
                !externalClaimIds.Contains(value.ExternalClaimId.Value)
                || value.ConsumingAnalysisRunId != output.RunId
                || value.SemanticAnalysisContextId != output.AnalysisContextId)
            || output.ExternalClaims.Any(value =>
                value.AcquisitionRunId != value.Provenance.OriginatingRunId))
        {
            throw new InvalidOperationException(
                "External claims, discovery admissions, and application links must retain their producing acquisition run and consuming run context.");
        }
        foreach (ExternalClaimContract claim in output.ExternalClaims)
        {
            HashSet<string> declaredLinks = claim.ApplicationLinkIds
                .Select(value => value.Value)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> retainedLinks = output.ExternalClaimApplicationLinks
                .Where(value => value.ExternalClaimId == claim.ClaimId)
                .Select(value => value.ApplicationLinkId.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (!declaredLinks.SetEquals(retainedLinks))
            {
                throw new InvalidOperationException(
                    "External claims and consuming-run application links must be bidirectionally complete.");
            }
        }
        if (output.ExternalClaims.Any(value => value.Authority is
                EvidenceAuthority.Unspecified
                or EvidenceAuthority.SnapshotBoundLocal
                or EvidenceAuthority.DeterministicDerived
                or EvidenceAuthority.HeuristicOrLlmInference)
            || output.Observations.Any(value => value.Authority != EvidenceAuthority.SnapshotBoundLocal)
            || output.DeterministicResults.Any(value =>
                value.Authority != EvidenceAuthority.DeterministicDerived))
        {
            throw new InvalidOperationException(
                "Observation, deterministic-result, and external-claim authority must remain claim-type-specific.");
        }

        string[] allArtifactIds =
        [
            .. output.Observations.Select(value => value.ObservationId.Value),
            .. output.DeterministicResults.Select(value => value.ResultId.Value),
            .. output.ExternalClaims.Select(value => value.ClaimId.Value),
            .. output.ExternalClaimApplicationLinks.Select(value => value.ApplicationLinkId.Value),
            .. output.DiscoveryLeads.Select(value => value.LeadId.Value),
            .. output.ModelProposals.Select(value => value.ProposalId.Value),
            .. output.ProposalAdmissions.Select(value => value.AdmissionId.Value),
            .. output.Candidates.Select(value => value.CandidateId.Value),
            .. output.Hypotheses.Select(value => value.HypothesisId.Value),
            .. output.Findings.Select(value => value.OccurrenceId.Value),
            .. output.Recommendations.Select(value => value.RecommendationId.Value),
            .. output.SupportedCases.Select(value => value.OccurrenceId.Value),
            .. output.LeadOnlyCases.Select(value => value.OccurrenceId.Value),
            .. output.Abstentions.Select(value => value.AbstentionId.Value),
            .. output.InvalidInputs.Select(value => value.InvalidInputId.Value),
            .. output.CoverageGaps.Select(value => value.GapId.Value),
            .. output.Failures.Select(value => value.FailureId.Value),
        ];
        RequireUnique(allArtifactIds, "all run-output artifact IDs");

        if (output.Observations.Any(value =>
                value.Provenance.LlmInvolvement.State != LlmInvolvementState.None)
            || output.DeterministicResults.Any(value =>
                value.Provenance.LlmInvolvement.State != LlmInvolvementState.None))
        {
            throw new InvalidOperationException(
                "LLM output cannot become a local observation or deterministic result.");
        }

        IEnumerable<ArtifactProvenanceContract> provenances = output.Observations.Select(value => value.Provenance)
            .Concat(output.DeterministicResults.Select(value => value.Provenance))
            .Concat(output.ExternalClaims.Select(value => value.Provenance))
            .Concat(output.ExternalClaimApplicationLinks.Select(value => value.Provenance))
            .Concat(output.DiscoveryLeads.Select(value => value.Provenance))
            .Concat(output.ModelProposals.Select(value => value.Provenance))
            .Concat(output.Candidates.Select(value => value.Provenance))
            .Concat(output.Hypotheses.Select(value => value.Provenance))
            .Concat(output.Findings.Select(value => value.Conclusion.Provenance))
            .Concat(output.Recommendations.Select(value => value.Provenance))
            .Concat(output.Abstentions.Select(value => value.Provenance))
            .Concat(output.InvalidInputs.Select(value => value.Provenance))
            .Concat(output.CoverageGaps.Select(value => value.Provenance))
            .Concat(output.Failures.Select(value => value.Provenance));
        foreach (ArtifactProvenanceContract provenance in provenances)
        {
            Validate(provenance.LlmInvolvement);
            if (provenance.SupersedesRevisionId == provenance.RevisionId)
            {
                throw new InvalidOperationException("Artifact provenance cannot supersede itself.");
            }
            RequireUnique(
                provenance.SupportingEvidenceIds.Select(value => value.Value),
                "supporting evidence IDs");
            RequireUnique(
                provenance.ContradictingEvidenceIds.Select(value => value.Value),
                "contradicting evidence IDs");
            if (provenance.SupportingEvidenceIds
                .Select(value => value.Value)
                .Intersect(
                    provenance.ContradictingEvidenceIds.Select(value => value.Value),
                    StringComparer.Ordinal)
                .Any())
            {
                throw new InvalidOperationException(
                    "The same evidence cannot simultaneously support and contradict one artifact.");
            }
        }
        RequireUnique(provenances.Select(value => value.RevisionId.Value), "artifact provenance revision IDs");

        IEnumerable<ArtifactProvenanceContract> directlyProducedProvenances =
            output.Observations.Select(value => value.Provenance)
                .Concat(output.DeterministicResults.Select(value => value.Provenance))
                .Concat(output.ExternalClaimApplicationLinks.Select(value => value.Provenance))
                .Concat(output.DiscoveryLeads.Select(value => value.Provenance))
                .Concat(output.ModelProposals.Select(value => value.Provenance))
                .Concat(output.Candidates.Select(value => value.Provenance))
                .Concat(output.Hypotheses.Select(value => value.Provenance))
                .Concat(output.Findings.Select(value => value.Conclusion.Provenance))
                .Concat(output.Recommendations.Select(value => value.Provenance))
                .Concat(output.Abstentions.Select(value => value.Provenance))
                .Concat(output.InvalidInputs.Select(value => value.Provenance))
                .Concat(output.CoverageGaps.Select(value => value.Provenance))
                .Concat(output.Failures.Select(value => value.Provenance));
        if (directlyProducedProvenances.Any(value => value.OriginatingRunId != output.RunId))
        {
            throw new InvalidOperationException(
                "Directly produced run output must retain the current run as its provenance owner.");
        }
        HashSet<string> appliedForeignClaimIds = output.ExternalClaimApplicationLinks
            .Select(value => value.ExternalClaimId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.ExternalClaims.Any(value =>
                value.Provenance.OriginatingRunId != output.RunId
                && !appliedForeignClaimIds.Contains(value.ClaimId.Value)))
        {
            throw new InvalidOperationException(
                "A reusable foreign-run external claim requires an explicit application link in the consuming run.");
        }

        HashSet<string> proposalIds = output.ModelProposals
            .Select(value => value.ProposalId.Value)
            .ToHashSet(StringComparer.Ordinal);
        RequireUnique(output.ModelProposals.Select(value => value.ProposalId.Value), nameof(output.ModelProposals));
        RequireUnique(output.ProposalAdmissions.Select(value => value.AdmissionId.Value), nameof(output.ProposalAdmissions));
        RequireUnique(output.ProposalAdmissions.Select(value => value.ProposalId.Value), "admitted proposal IDs");
        RequireUnique(
            output.ProposalAdmissions.Select(value => value.AdmittedArtifactId.Value),
            "proposal-admitted artifact IDs");
        if (output.ModelProposals.Any(value =>
            value.ValidationState == ProposalValidationState.Unspecified
                || value.Operation is LlmOperation.Unspecified or LlmOperation.None))
        {
            throw new InvalidOperationException(
                "Model proposals require an explicit operation and validation state.");
        }

        Dictionary<string, ModelProposalContract> proposalsById = output.ModelProposals
            .ToDictionary(value => value.ProposalId.Value, StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> admissibleArtifacts = new(StringComparer.Ordinal)
        {
            ["external-claim"] = output.ExternalClaims
                .Select(value => value.ClaimId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["candidate"] = output.Candidates
                .Select(value => value.CandidateId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["hypothesis"] = output.Hypotheses
                .Select(value => value.HypothesisId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["finding"] = output.Findings
                .Select(value => value.OccurrenceId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["recommendation"] = output.Recommendations
                .Select(value => value.RecommendationId.Value)
                .ToHashSet(StringComparer.Ordinal),
            ["abstention"] = output.Abstentions
                .Select(value => value.AbstentionId.Value)
                .ToHashSet(StringComparer.Ordinal),
        };
        Dictionary<string, IReadOnlyDictionary<string, ArtifactProvenanceContract>> admissibleProvenance =
            new(StringComparer.Ordinal)
            {
                ["external-claim"] = output.ExternalClaims.ToDictionary(
                    value => value.ClaimId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["candidate"] = output.Candidates.ToDictionary(
                    value => value.CandidateId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["hypothesis"] = output.Hypotheses.ToDictionary(
                    value => value.HypothesisId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["finding"] = output.Findings.ToDictionary(
                    value => value.OccurrenceId.Value,
                    value => value.Conclusion.Provenance,
                    StringComparer.Ordinal),
                ["recommendation"] = output.Recommendations.ToDictionary(
                    value => value.RecommendationId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
                ["abstention"] = output.Abstentions.ToDictionary(
                    value => value.AbstentionId.Value,
                    value => value.Provenance,
                    StringComparer.Ordinal),
            };
        if (output.ModelProposals.Any(value =>
                !admissibleArtifacts.ContainsKey(value.ProposedArtifactType)
                || value.Provenance.OriginatingRunId != output.RunId
                || value.Provenance.LlmInvolvement.Operation != value.Operation
                || value.Provenance.LlmInvolvement.State
                    != (value.ValidationState == ProposalValidationState.Rejected
                        ? LlmInvolvementState.ProposalRejected
                        : LlmInvolvementState.ProposalRetained)))
        {
            throw new InvalidOperationException(
                "Model proposal provenance must retain its run, operation, validation disposition, "
                + "and an allowed proposed artifact type.");
        }

        if (output.ProposalAdmissions.Any(value =>
                !proposalIds.Contains(value.ProposalId.Value)
                || proposalsById[value.ProposalId.Value].ValidationState != ProposalValidationState.Validated
                || !StringComparer.Ordinal.Equals(
                    proposalsById[value.ProposalId.Value].ProposedArtifactType,
                    value.AdmittedArtifactType)
                || value.OriginatingRunId != output.RunId
                || !admissibleArtifacts.TryGetValue(value.AdmittedArtifactType, out HashSet<string>? artifacts)
                || !artifacts.Contains(value.AdmittedArtifactId.Value)
                || !admissibleProvenance.TryGetValue(
                    value.AdmittedArtifactType,
                    out IReadOnlyDictionary<string, ArtifactProvenanceContract>? provenanceById)
                || !provenanceById.TryGetValue(
                    value.AdmittedArtifactId.Value,
                    out ArtifactProvenanceContract? admittedProvenance)
                || admittedProvenance.LlmInvolvement.State != LlmInvolvementState.ProposalAdmitted
                || admittedProvenance.LlmInvolvement.Operation
                    != proposalsById[value.ProposalId.Value].Operation
                || admittedProvenance.LlmInvolvement.InvocationId
                    != proposalsById[value.ProposalId.Value].Provenance.LlmInvolvement.InvocationId))
        {
            throw new InvalidOperationException(
                "Every admitted model proposal must be validated and point to an existing allowed typed artifact "
                + "whose type, invocation, and proposal-admitted operation match the retained proposal.");
        }

        HashSet<string> admittedArtifactIds = admissibleProvenance
            .SelectMany(value => value.Value)
            .Where(value => value.Value.LlmInvolvement.State == LlmInvolvementState.ProposalAdmitted)
            .Select(value => value.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (!admittedArtifactIds.SetEquals(
                output.ProposalAdmissions.Select(value => value.AdmittedArtifactId.Value)))
        {
            throw new InvalidOperationException(
                "Proposal admissions and proposal-admitted artifacts must form a complete bidirectional record.");
        }

        HashSet<string> taxonomyAssignmentIds = output.TaxonomyAssignments
            .Select(value => value.AssignmentId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.Findings.Any(value =>
                value.TaxonomyAssignmentIds
                    .Concat(value.Conclusion.TaxonomyAssignmentIds)
                    .Any(id => !taxonomyAssignmentIds.Contains(id.Value))))
        {
            throw new InvalidOperationException(
                "Finding taxonomy references must resolve to retained assignments.");
        }
        HashSet<string> recommendationIds = output.Recommendations
            .Select(value => value.RecommendationId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (output.Findings.Any(value =>
                value.Conclusion.RecommendationId is not null
                && !recommendationIds.Contains(value.Conclusion.RecommendationId.Value)))
        {
            throw new InvalidOperationException(
                "Finding recommendation references must resolve within the run output.");
        }

        HashSet<string> analyzerIds = output.AnalyzerDeclarations
            .Select(value => value.AnalyzerId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> coverageGapIds = output.CoverageGaps
            .Select(value => value.GapId.Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> failureIds = output.Failures
            .Select(value => value.FailureId.Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CoverageContract coverage in output.Coverage)
        {
            Validate(coverage);
            if (!analyzerIds.Contains(coverage.AnalyzerId.Value)
                || coverage.OriginatingRunId != output.RunId
                || coverage.TaxonomyAssignmentIds.Any(
                    value => !taxonomyAssignmentIds.Contains(value.Value))
                || coverage.GapIds.Any(value => !coverageGapIds.Contains(value.Value))
                || coverage.FailureIds.Any(value => !failureIds.Contains(value.Value)))
            {
                throw new InvalidOperationException(
                    "Coverage must be run-bound and resolve its analyzer, taxonomy, gap, and failure references.");
            }
        }

        RequireUnique(output.CollectionStates.Select(value => value.CollectionName), nameof(output.CollectionStates));
        if (!RequiredRunOutputCollectionNames.SetEquals(
                output.CollectionStates.Select(value => value.CollectionName)))
        {
            throw new InvalidOperationException("Run output must state production status for every typed collection.");
        }
        if (output.CollectionStates.Any(value => value.State == CollectionProductionState.Unspecified))
        {
            throw new InvalidOperationException("Run output collection states must be explicit.");
        }
        Dictionary<string, int> collectionCounts = new(StringComparer.Ordinal)
        {
            ["observations"] = output.Observations.Count,
            ["deterministic_results"] = output.DeterministicResults.Count,
            ["external_claims"] = output.ExternalClaims.Count,
            ["application_links"] = output.ExternalClaimApplicationLinks.Count,
            ["discovery_leads"] = output.DiscoveryLeads.Count,
            ["model_proposals"] = output.ModelProposals.Count,
            ["proposal_admissions"] = output.ProposalAdmissions.Count,
            ["candidates"] = output.Candidates.Count,
            ["hypotheses"] = output.Hypotheses.Count,
            ["findings"] = output.Findings.Count,
            ["recommendations"] = output.Recommendations.Count,
            ["supported_cases"] = output.SupportedCases.Count,
            ["lead_only_cases"] = output.LeadOnlyCases.Count,
            ["abstentions"] = output.Abstentions.Count,
            ["invalid_inputs"] = output.InvalidInputs.Count,
            ["coverage_gaps"] = output.CoverageGaps.Count,
            ["failures"] = output.Failures.Count,
        };
        if (output.CollectionStates.Any(value =>
                string.IsNullOrWhiteSpace(value.Reason)
                || (collectionCounts[value.CollectionName] > 0
                    && value.State != CollectionProductionState.Populated)
                || (collectionCounts[value.CollectionName] == 0
                    && value.State == CollectionProductionState.Populated)))
        {
            throw new InvalidOperationException(
                "Typed collection state and reason must agree with the retained collection contents.");
        }
        if (output.Readiness.RunId != output.RunId
            || output.Readiness.Scope == ReadinessScope.Unspecified
            || output.Replayability.ReplayClass == ReplayClass.Unspecified
            || output.Auditability.State == AuditabilityState.Unspecified)
        {
            throw new InvalidOperationException(
                "Readiness, replayability, and auditability state must be explicit and run-bound.");
        }
        bool readinessAbsent = output.Readiness.Scope == ReadinessScope.None;
        if ((readinessAbsent
                && (output.Readiness.ReadinessPolicyId is not null
                    || output.Readiness.DispositionIds.Count != 0))
            || (!readinessAbsent && output.Readiness.ReadinessPolicyId is null))
        {
            throw new InvalidOperationException(
                "Readiness absence cannot carry dispositions; evaluated readiness requires an explicit policy.");
        }
        RequireUnique(
            output.Readiness.DispositionIds.Select(value => value.Value),
            "readiness disposition IDs");
        RequireUnique(
            output.Replayability.DependencyIds.Select(value => value.Value),
            "replay dependency IDs");
        RequireUnique(output.Replayability.MissingDependencies, "missing replay dependencies");
        RequireUnique(output.UnsupportedCapabilities, nameof(output.UnsupportedCapabilities));
        if (output.Replayability.MissingDependencies.Any(string.IsNullOrWhiteSpace)
            || output.UnsupportedCapabilities.Any(string.IsNullOrWhiteSpace)
            || output.Auditability.Gaps.Any(string.IsNullOrWhiteSpace)
            || (output.Replayability.ReplayClass == ReplayClass.CompleteClean
                && output.Replayability.MissingDependencies.Count != 0)
            || (output.Auditability.State == AuditabilityState.Complete
                && output.Auditability.Gaps.Count != 0)
            || (output.Auditability.State != AuditabilityState.Complete
                && output.Auditability.Gaps.Count == 0))
        {
            throw new InvalidOperationException(
                "Replay, audit, and unsupported-capability declarations must retain coherent explicit gaps.");
        }
    }

}
