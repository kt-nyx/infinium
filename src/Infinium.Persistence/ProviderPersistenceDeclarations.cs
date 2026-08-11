namespace Infinium.Persistence;

public static class ProviderPersistenceDeclarations
{
    public const int SchemaVersion = 6;
    public const string StorageContractVersion = "1.5.0";
    public const string MigrationId = "M1-S6-0006";
    public const int SourceSchemaVersion = 5;
    public const string SourceStorageContractVersion = "1.4.0";
    public const string SourceSchemaFingerprint = "e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d";
    public const string SchemaFingerprint = "bc209224a7c1810ea23006005850f1bcfaca221995fd6b058fafea8ff1f1d6c4";
    public const int ProjectionContractVersion = 1;

    public static IReadOnlyList<string> RebuildableProjections { get; } =
    [
        "provider_operation_projection",
        "provider_budget_projection",
        "provider_profile_projection",
    ];

    public static IReadOnlyList<string> ProjectionSources { get; } =
    [
        "provider_operation_authorizations",
        "provider_operation_blocks",
        "provider_effective_scan_configurations_v2",
        "provider_command_bindings",
        "evidence_acquisition_job_nodes",
        "evidence_acquisition_attempts",
        "evidence_acquisition_commands",
        "provider_access_profiles",
        "provider_capability_snapshots",
        "provider_price_snapshots",
        "provider_price_rules",
        "provider_generations",
        "provider_credential_intents",
        "provider_credential_intent_events",
        "provider_operation_attempts",
        "provider_requests",
        "provider_reservations",
        "provider_reservation_scope_items",
        "provider_transport_events",
        "provider_dispatch_fences",
        "provider_responses",
        "provider_response_finalizations",
        "provider_usage_entries",
        "provider_rate_limit_facts",
        "provider_settlements",
        "provider_settlement_adjustments",
        "provider_semantic_proposals",
        "provider_semantic_admissions",
        "provider_semantic_validations",
        "provider_run_output_v2_bindings",
        "provider_replay_edges",
    ];

    public static IReadOnlyList<string> BackupIncludedClasses { get; } =
    [
        "authoritative-database",
        "retained-payloads",
    ];

    public static IReadOnlyList<string> StructurallyExcludedClasses { get; } =
    [
        "provider-secret-bytes",
        "credential-manager-targets",
        "authorization-headers",
        "helper-private-handles",
    ];

    public static IReadOnlyList<string> DeletionHistorySources { get; } =
    [
        "provider_credential_intents",
        "provider_generations",
        "provider_transport_events",
        "provider_semantic_admissions",
        "evidence_acquisition_application_links",
    ];
}
