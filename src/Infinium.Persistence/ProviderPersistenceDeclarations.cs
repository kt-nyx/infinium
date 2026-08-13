namespace Infinium.Persistence;

public static class ProviderPersistenceDeclarations
{
    public const int SchemaVersion = 6;
    public const string StorageContractVersion = "1.5.0";
    public const string MigrationId = "M1-S6-0006";
    public const string Wp2ExtensionMigrationId = "M1-S6-WP2-0006A";
    public const string Wp2ExtensionSourceSchemaFingerprint = "56dc6efd92fff75fe21f344abafa3b88b99a8e92d2d1b2517f706d63af4599a3";
    public const string Wp3ExtensionMigrationId = "M1-S6-WP3-0006B";
    public const string Wp3ExtensionSourceSchemaFingerprint = "240a06fe2a9fa3d79db63985fbda329c8e83822534b93cbfb539062a109cad9e";
    public const string Wp3CorrectionMigrationId = "M1-S6-WP3-0006C";
    public const string Wp3CorrectionSourceSchemaFingerprint = "554129523ac64ce52ee4d24e90644dbaa167c0d98602f1c2d0f25ad271ec0581";
    public const string Wp5ExtensionMigrationId = "M1-S6-WP5-0006D";
    public const string Wp5ExtensionSourceSchemaFingerprint = "85c0ed0d1ee466c9a62d33c2a5ce6da8f28b2fc788603deffaa364683d5966fd";
    public const string Wp5CorrectionMigrationId = "M1-S6-WP5-0006E";
    public const string Wp5CorrectionSourceSchemaFingerprint = "a312f695cc1ed6f77c89c2471a6c7dc6949035000d3c0db18261237bf1c6e107";
    public const string Wp6CorrectionMigrationId = "M1-S6-WP6-0006F";
    public const string Wp6CorrectionSourceSchemaFingerprint = "4a9591b76c17bdac790010c9cef292875d59fcad0aa81054b91d69a699c7372e";
    public const string Wp6ActiveContractCorrectionMigrationId = "M1-S6-WP6-0006G";
    public const string Wp6ActiveContractCorrectionSourceSchemaFingerprint = "a9c58c7e3f374b77a623b751547353a356b2132f24f353ca2356a4268f13b51d";
    public const string Wp7ExtensionMigrationId = "M1-S6-WP7-0006H";
    public const string Wp7ExtensionSourceSchemaFingerprint = "0c831ead2dc177f3d4367b8fef12b0bbad2d17aa7d83203b6e2caf6c8b978ef5";
    public const int SourceSchemaVersion = 5;
    public const string SourceStorageContractVersion = "1.4.0";
    public const string SourceSchemaFingerprint = "e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d";
    public const string SchemaFingerprint = "8195fc34887e202b823bd1a7c6757bde6dd78f2df6648e589d64f46a3effbcbf";
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
        "provider_credential_terminal_root_consumptions",
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
        "provider_budget_limits",
        "provider_budget_events",
        "provider_usage_rollup_references",
        "provider_budget_settlement_receipts",
        "provider_semantic_proposals",
        "provider_semantic_admissions",
        "provider_semantic_validations",
        "candidate_investigation_outcomes",
        "analysis_candidates",
        "analysis_hypotheses",
        "evidence_application_links",
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
