namespace Infinium.Persistence;

public static class ProviderPersistenceDeclarations
{
    public const int SchemaVersion = 9;
    public const string StorageContractVersion = "1.8.0";
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
    public const string Wp9CampaignInputBoundCorrectionMigrationId = "M1-S6-WP9-0006I";
    public const string Wp9CampaignInputBoundCorrectionSourceSchemaFingerprint = SchemaFingerprint;
    public const string Wp9CampaignInputBoundCorrectionSchemaFingerprint = "f138afbdd4737400370473f6caa9ae44324f3f8eb04a5bac150f1ab2a01d08b7";
    public const string R2LiveSemanticSchemaFingerprint = "b70a79ef993b0db45a661ab0a1f701dd2484223ba105e1e82fd77ec9debc8e32";
    public const string SuccessorAttemptExtensionMigrationId = "M1-S6-SUCCESSOR-0007";
    public const string SuccessorAttemptSchemaFingerprint = "bc281cecc1025f1fa687c735c536967f338b639259b426545a1b60f33b2c846b";
    public const string SuccessorV6PersistenceMigrationId = "M1-S6-SUCCESSOR-V6-0008";
    public const string SuccessorV6PersistenceOriginalSchemaFingerprint = "dcf0653bbf1c337e77f0f58aad0ba63fb3d775ccb3c6a7e4e560e971ff309893";
    public const string SuccessorV6PersistenceSchemaFingerprint = "67dab8043a37d7095720016c75ab0199116e0a4f14029234a17fa6ced3c36b2a";
    public const string SuccessorV6SemanticTriggerCorrectionId = "M1-S6-SUCCESSOR-V6-0008A";
    public const string SemanticAdmissionSeparationMigrationId = "M1-S6-C2-SEMANTIC-0009";
    public const string SemanticAdmissionSeparationSchemaFingerprint = "c40cfd33517c3578f5247d7bf4196fd02016b9160c6fb1a743ca58d17c1673f0";
    public const int SourceSchemaVersion = 5;
    public const string SourceStorageContractVersion = "1.4.0";
    public const string SourceSchemaFingerprint = "e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d";
    public const string SchemaFingerprint = "938bd18d7af76470bc70058cf5c31aa5257e220c075991aa1797f99a6fba94d7";
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
        "m1_slice6_successor_semantic_response_bindings",
        "m1_slice6_successor_v6_operations",
        "m1_slice6_successor_v6_budget_events",
        "m1_slice6_successor_v6_responses",
        "m1_slice6_successor_v6_semantic_response_bindings",
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
