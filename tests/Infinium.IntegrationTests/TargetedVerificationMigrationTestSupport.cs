namespace Infinium.Tests;

internal static class TargetedVerificationMigrationTestSupport
{
    public const string DropSchema16Sql =
        """
        DROP TABLE targeted_result_links;
        DROP TABLE targeted_initiation_lineage;
        DROP TABLE targeted_operation_inputs;
        DROP TABLE targeted_start_admissions;
        DROP TABLE targeted_verification_plans;
        DROP TABLE targeted_reuse_decisions;
        DROP TABLE targeted_correlation_rows;
        DROP TABLE targeted_scope_dependencies;
        DROP TABLE targeted_scope_members;
        DROP TABLE targeted_scope_roots;
        DROP TABLE semantic_acquisition_application_links;
        DROP TABLE semantic_acquisition_publications;
        DROP TABLE semantic_acquisition_progress;
        DROP TABLE semantic_acquisition_checkpoints;
        DROP TABLE semantic_acquisition_attempts;
        DROP TABLE semantic_acquisition_projection;
        DROP TABLE semantic_acquisition_events;
        DROP TABLE semantic_acquisition_commands;
        DROP TABLE semantic_acquisition_jobs;
        DROP TABLE semantic_acquisition_runs;
        DROP TABLE targeted_snapshot_links;
        DROP TABLE targeted_preparation_projection;
        DROP TABLE targeted_preparation_commands;
        DROP TABLE targeted_preparation_events;
        DROP TABLE targeted_preparation_requests;
        DELETE FROM migration_history
          WHERE migration_id='targeted-verification-preparation-0016';
        """;
}
