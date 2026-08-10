namespace Infinium.Domain.Contracts;

public static partial class DomainContractInvariants
{
    public static void Validate(EffectiveScanConfigurationContract configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!StringComparer.Ordinal.Equals(
                configuration.SchemaId,
                ContractConstants.EffectiveScanConfigurationSchemaId)
            || configuration.SchemaVersion.Major != 1)
        {
            throw new InvalidOperationException("Effective configuration uses an unsupported schema contract.");
        }

        RequireNonEmpty(configuration.Analyzers, nameof(configuration.Analyzers));
        RequireUnique(
            configuration.Analyzers.Select(value => value.AnalyzerId.Value),
            nameof(configuration.Analyzers));
        RequireUnique(
            configuration.Sources.Select(value => value.SourceId.Value),
            nameof(configuration.Sources));
        RequireUnique(
            configuration.Thresholds.Select(value => $"{value.AnalyzerId.Value}/{value.ThresholdId.Value}"),
            nameof(configuration.Thresholds));
        RequireUnique(
            configuration.SemanticContextOverrides.Select(value => value.OverrideId.Value),
            nameof(configuration.SemanticContextOverrides));
        if (configuration.Analyzers.Any(value => value.Origin == SettingOrigin.Unspecified)
            || configuration.Sources.Any(value =>
                value.Origin == SettingOrigin.Unspecified || value.Mode == SourceMode.Unspecified)
            || configuration.Budgets.Origin == SettingOrigin.Unspecified
            || configuration.CachePolicy.Origin == SettingOrigin.Unspecified
            || configuration.Tracing.Origin == SettingOrigin.Unspecified
            || configuration.Tracing.SensitivityLabel
                != DiagnosticSensitivityLabel.SensitiveDevelopmentDiagnostic
            || configuration.CandidateBreadth.Origin == SettingOrigin.Unspecified
            || configuration.Provider.Origin == SettingOrigin.Unspecified
            || configuration.Provider.Mode == ProviderMode.Unspecified
            || configuration.Resources.Origin == SettingOrigin.Unspecified
            || configuration.Thresholds.Any(value => value.Origin == SettingOrigin.Unspecified)
            || configuration.SemanticContextOverrides.Any(
                value => value.Origin != SettingOrigin.SemanticContextOverride))
        {
            throw new InvalidOperationException("Every effective control must retain an explicit valid origin.");
        }

        if (configuration.Budgets.MaximumDispatchCount < 0
            || configuration.Budgets.MaximumInputTokens < 0
            || configuration.Budgets.MaximumOutputTokens < 0
            || configuration.Budgets.MaximumHostedSearchCalls != 0
            || configuration.Budgets.MaximumNanoUsd < 0
            || configuration.CandidateBreadth.MaximumCandidates < 1
            || configuration.CandidateBreadth.AllPairsLlmComparison
            || configuration.Resources.MaximumGeneralWorkers < 1
            || configuration.Resources.MaximumMemoryBytes < 1
            || configuration.Resources.MaximumOutputBytes < 1)
        {
            throw new InvalidOperationException("Effective controls must retain finite analysis bounds.");
        }
        if (configuration.CachePolicy.AnalyticalMode is not ("reuse-valid" or "force-clean-recomputation")
            || configuration.CachePolicy.SourceMode is not ("reuse-resolved-source" or "force-clean-extraction")
            || !StringComparer.Ordinal.Equals(configuration.CachePolicy.ProviderCacheMode, "disabled")
            || configuration.Tracing.Level is not ("off" or "errors" or "operations" or "development")
            || configuration.CandidateBreadth.Mode is not (
                "declared-mandatory-and-causal-lanes" or "expanded-deterministic-lanes"))
        {
            throw new InvalidOperationException("Effective cache and candidate controls use the closed analysis vocabulary.");
        }
        string[] requiredPayloadSchemas =
        [
            ContractConstants.DocumentationEvidenceSchemaId,
            ContractConstants.CandidateAnalysisSchemaId,
            ContractConstants.FindingCaseSchemaId,
            ContractConstants.AnalysisReplaySchemaId,
            ContractConstants.AnalysisExecutionInputSchemaId,
        ];
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(
            configuration.NotUsedBoundaries,
            requireNotUsed: true);
        if (!requiredPayloadSchemas.All(schemaId => configuration.PayloadContracts.Any(
                item => item.Required && StringComparer.Ordinal.Equals(item.SchemaId, schemaId))))
        {
            throw new InvalidOperationException(
                "Effective analysis pipeline configuration must bind execution input, required payloads, and not-used boundaries.");
        }

        bool providerDisabled = configuration.Provider.Mode == ProviderMode.Disabled;
        bool hasProviderDetails = configuration.Provider.Model is not null
            || configuration.Provider.ReasoningEffort is not null
            || configuration.Provider.Store is not null
            || configuration.Provider.ServiceTier is not null
            || configuration.Provider.Streaming is not null
            || configuration.Provider.MaximumConcurrentLiveDispatches is not null;
        bool hasAllProviderDetails = configuration.Provider.Model is not null
            && configuration.Provider.ReasoningEffort is not null
            && configuration.Provider.Store is not null
            && configuration.Provider.ServiceTier is not null
            && configuration.Provider.Streaming is not null
            && configuration.Provider.MaximumConcurrentLiveDispatches is not null;
        if ((providerDisabled && hasProviderDetails)
            || (!providerDisabled && (!hasAllProviderDetails
                || !StringComparer.Ordinal.Equals(configuration.Provider.Model, "gpt-5.6-sol")
                || !StringComparer.Ordinal.Equals(configuration.Provider.ReasoningEffort, "medium")
                || configuration.Provider.Store != false
                || !StringComparer.Ordinal.Equals(configuration.Provider.ServiceTier, "default")
                || configuration.Provider.Streaming != false
                || configuration.Provider.MaximumConcurrentLiveDispatches != 1)))
        {
            throw new InvalidOperationException(
                "Disabled provider controls omit live details; enabled controls retain every accepted detail.");
        }
    }

    public static void Validate(LifecycleTransitionContract transition)
    {
        OperationalContractInvariants.Validate(transition);
    }

}
