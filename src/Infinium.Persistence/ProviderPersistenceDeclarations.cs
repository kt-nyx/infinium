namespace Infinium.Persistence;

public static class ProviderPersistenceDeclarations
{
    public const int SchemaVersion = 6;
    public const string StorageContractVersion = "1.5.0";
    public const string MigrationId = "M1-S6-0006";
    public const int SourceSchemaVersion = 5;
    public const string SourceStorageContractVersion = "1.4.0";
    public const string SourceSchemaFingerprint = "e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d";
    public const string SchemaFingerprint = "9cc35e3709a9a7fb4bdc0470e4ee488441648cb9b43055d0319f22af878464f4";
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
        "provider_capability_snapshots",
        "provider_price_snapshots",
        "provider_price_rules",
        "provider_generations",
        "provider_credential_intents",
        "provider_operation_attempts",
        "provider_reservations",
        "provider_reservation_scope_items",
        "provider_transport_events",
        "provider_responses",
        "provider_usage_entries",
        "provider_settlements",
        "provider_settlement_adjustments",
        "provider_semantic_proposals",
        "provider_semantic_admissions",
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
