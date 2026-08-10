namespace Infinium.Domain.Contracts;

public static partial class DomainContractInvariants
{
    public static void Validate(CoverageContract coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        if (coverage.Denominator < 0
            || coverage.CompletedCount < 0
            || coverage.CompletedCount > coverage.Denominator
            || coverage.State == CoverageState.Unspecified
            || !StringComparer.Ordinal.Equals(coverage.TaxonomyId, ContractConstants.TaxonomyId)
            || coverage.TaxonomyVersion != ContractVersion.Parse(ContractConstants.TaxonomyVersion)
            || string.IsNullOrWhiteSpace(coverage.PopulationId)
            || string.IsNullOrWhiteSpace(coverage.DenominatorLabel))
        {
            throw new InvalidOperationException(
                "Coverage requires bounded counts, explicit state and the accepted taxonomy contract.");
        }
        RequireUnique(
            coverage.TaxonomyAssignmentIds.Select(value => value.Value),
            "coverage taxonomy assignment IDs");
        RequireUnique(coverage.GapIds.Select(value => value.Value), "coverage gap IDs");
        RequireUnique(coverage.FailureIds.Select(value => value.Value), "coverage failure IDs");
        if (coverage.Exclusions.Any(item => string.IsNullOrWhiteSpace(item.MemberId.Value)
                || string.IsNullOrWhiteSpace(item.Reason)
                || item.State is not (CoverageMemberState.SkippedByConfiguration or CoverageMemberState.SkippedByLimit))
            || coverage.Exclusions.Select(item => item.MemberId).Distinct().Count() != coverage.Exclusions.Count
            || (coverage.State == CoverageState.Completed
                && (coverage.CompletedCount != coverage.Denominator
                    || coverage.GapIds.Count != 0
                    || coverage.FailureIds.Count != 0))
            || (coverage.State == CoverageState.CompletedWithGaps
                && coverage.GapIds.Count == 0
                && coverage.FailureIds.Count == 0
                && coverage.Exclusions.Count == 0)
            || (coverage.State == CoverageState.Failed && coverage.FailureIds.Count == 0)
            || (coverage.State is CoverageState.SkippedByConfiguration
                    or CoverageState.SkippedByLimit
                    or CoverageState.Unsupported
                && coverage.CompletedCount != 0))
        {
            throw new InvalidOperationException(
                "Coverage status must agree with completed work and explicit gaps, failures, or exclusions.");
        }
    }

    public static void Validate(LlmInvolvementContract involvement)
    {
        ArgumentNullException.ThrowIfNull(involvement);
        if (involvement.State == LlmInvolvementState.Unspecified
            || involvement.Operation == LlmOperation.Unspecified)
        {
            throw new InvalidOperationException("LLM involvement state and operation must be explicit.");
        }

        bool isNone = involvement.State == LlmInvolvementState.None;
        if ((isNone
                && (involvement.Operation != LlmOperation.None || involvement.InvocationId is not null))
            || (!isNone
                && (involvement.Operation == LlmOperation.None || involvement.InvocationId is null)))
        {
            throw new InvalidOperationException(
                "LLM absence has no invocation; rejected or admitted proposals retain operation and invocation.");
        }
    }

    public static void Validate(DiagnosticTraceContract trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (!StringComparer.Ordinal.Equals(trace.SchemaId, ContractConstants.DiagnosticTraceSchemaId)
            || trace.SchemaVersion.Major != 1
            || trace.SensitivityLabel != DiagnosticSensitivityLabel.SensitiveDevelopmentDiagnostic
            || trace.SharingClass != DiagnosticSharingClass.PrivateDiagnostic
            || trace.CredentialMaterialPresent
            || trace.RedactionState == DiagnosticRedactionState.Unspecified)
        {
            throw new InvalidOperationException(
                "Diagnostic traces must remain sensitive PrivateDiagnostic artifacts verified free of credentials.");
        }

        if (trace.Events.Any(value =>
                value.Sequence < 0
                || value.Severity == DiagnosticSeverity.Unspecified
                || value.Fields.Any(field =>
                    field.DataClass == DiagnosticDataClass.Unspecified
                    || field.Redaction == DiagnosticFieldRedaction.Unspecified))
            || trace.Events.Select(value => value.Sequence).Distinct().Count() != trace.Events.Count)
        {
            throw new InvalidOperationException("Diagnostic trace events require explicit unique sequences and labels.");
        }
    }

    public static void Validate(CaseOccurrenceContract occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.Kind == CaseOccurrenceKind.Unspecified)
        {
            throw new InvalidOperationException("Case occurrence kind must be explicit.");
        }

        if (occurrence.RevisionNumber < 1)
        {
            throw new InvalidOperationException("Case occurrence revision must be positive.");
        }

        if (occurrence.Kind == CaseOccurrenceKind.Supported
            && occurrence.FindingOccurrenceIds.Count == 0)
        {
            throw new InvalidOperationException("A supported case requires at least one finding.");
        }

        if (occurrence.Kind == CaseOccurrenceKind.LeadOnly
            && occurrence.FindingOccurrenceIds.Count != 0)
        {
            throw new InvalidOperationException(
                "A lead-only case cannot contain a finding or affect readiness.");
        }
    }

}
