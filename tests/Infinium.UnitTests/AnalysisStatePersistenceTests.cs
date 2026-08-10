using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class AnalysisStatePersistenceTests
{
    private static readonly string[] AnalysisFindingCaseTables =
    [
        "finding_case_publications",
        "finding_promotion_assessments",
        "finding_case_abstentions",
        "finding_case_finding_details",
        "finding_case_recommendations",
        "case_hypothesis_memberships",
        "finding_case_case_details",
        "finding_case_taxonomy_assignments",
        "taxonomy_projection_edges",
        "finding_case_gap_details",
        "analysis_coverage_taxonomy_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_failure_links",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "lineage_event_edges",
    ];

    private static readonly string[] AnalysisTables =
    [
        "analysis_candidates",
        "analysis_coverage",
        "analysis_coverage_failure_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_taxonomy_links",
        "analysis_dependency_edges",
        "analysis_gaps",
        "analysis_hypotheses",
        "analysis_recommendations",
        "analysis_replay_manifests",
        "analysis_run_outputs",
        "candidate_decisions",
        "case_memberships",
        "case_hypothesis_memberships",
        "case_occurrence_details",
        "documentation_passages",
        "documentation_imports",
        "documentation_revisions",
        "documentation_application_bindings",
        "documentation_deletion_receipts",
        "documentation_purpose_assignment_details",
        "documentation_gap_details",
        "effect_receipts",
        "evidence_application_links",
        "evidence_revisions",
        "finding_occurrence_details",
        "finding_case_abstentions",
        "finding_case_case_details",
        "finding_case_finding_details",
        "finding_case_gap_details",
        "finding_case_publications",
        "finding_case_recommendations",
        "finding_case_taxonomy_assignments",
        "finding_promotion_assessments",
        "lineage_details",
        "lineage_event_edges",
        "payload_backup_pins",
        "reconciliation_details",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "taxonomy_assignments",
        "taxonomy_projection_edges",
    ];

    private static readonly string[] AnalysisIndexes =
    [
        "idx_candidate_decisions_run_population",
        "idx_candidates_run_lane",
        "idx_case_memberships_member",
        "idx_coverage_run_population",
        "idx_dependency_edges_from",
        "idx_dependency_edges_to",
        "idx_documentation_passages_revision",
        "idx_documentation_imports_revision",
        "idx_documentation_application_bindings_run",
        "idx_documentation_deletion_receipts_revision",
        "idx_effect_receipts_run",
        "idx_evidence_applications_run",
        "idx_evidence_revisions_passage",
        "idx_gaps_run_population",
        "idx_hypotheses_candidate",
        "idx_recommendations_finding",
        "idx_replay_manifests_run",
        "idx_run_outputs_run",
        "idx_taxonomy_subject",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Cases")]
    public void AnalysisStateModelSchema5HasExactMigrationContractAndObjects()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        Assert.AreEqual(5, store.GetSchemaVersion());

        using SqliteConnection connection = temporary.OpenRaw();
        Assert.AreEqual("5", ScalarText(connection, "PRAGMA user_version;"));
        Assert.AreEqual(
            "5",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'schema_version';"));
        Assert.AreEqual(
            "1.4.0",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'storage_contract_version';"));
        Assert.AreEqual(
            "e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'schema_fingerprint';"));
        Assert.AreEqual(
            "4|5",
            ScalarText(
                connection,
                """
                SELECT CAST(from_version AS TEXT) || '|' || CAST(to_version AS TEXT)
                FROM migration_history
                WHERE migration_id = 'M1-S5-WP4-0005';
                """));

        CollectionAssert.AreEquivalent(
            AnalysisTables,
            SchemaNames(connection, "table", AnalysisTables));
        CollectionAssert.AreEquivalent(
            AnalysisIndexes,
            SchemaNames(connection, "index", AnalysisIndexes));

        foreach (string table in AnalysisTables)
        {
            Assert.AreEqual(
                1L,
                ScalarInt64(
                    connection,
                    "SELECT strict FROM pragma_table_list WHERE schema = 'main' AND name = $name;",
                    ("$name", table)),
                $"{table} must be STRICT.");
            Assert.AreEqual(
                2L,
                ScalarInt64(
                    connection,
                    """
                    SELECT COUNT(*) FROM sqlite_schema
                    WHERE type = 'trigger' AND tbl_name = $name
                      AND name IN ($update_name, $delete_name);
                    """,
                    ("$name", table),
                    ("$update_name", table + "_append_only_update"),
                    ("$delete_name", table + "_append_only_delete")),
                $"{table} must be append-only.");
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Cases")]
    public void AnalysisStateModelMigratesAcceptedSchema4ForwardAndRefusesNewerSchema()
    {
        using TemporaryStore migration = new();
        using (AuthoritativeStore store = migration.Open())
        {
            Assert.AreEqual(5, store.GetSchemaVersion());
        }

        using (SqliteConnection connection = migration.OpenRaw())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                string.Join(
                    Environment.NewLine,
                    AnalysisFindingCaseTables.Reverse().Select(table => $"DROP TABLE {table};"))
                +
                """

                ALTER TABLE lineage_events DROP COLUMN predecessor_occurrence_id;
                ALTER TABLE lineage_events DROP COLUMN successor_occurrence_id;
                DROP INDEX idx_findings_signature;
                ALTER TABLE finding_occurrences DROP COLUMN analyzer_version;
                CREATE INDEX idx_findings_signature ON finding_occurrences(
                    analyzer_family, identity_contract_version, canonical_signature);
                ALTER TABLE analysis_coverage DROP COLUMN analyzer_id;
                ALTER TABLE analysis_coverage DROP COLUMN denominator_label;
                ALTER TABLE analysis_coverage DROP COLUMN exclusions_json;
                ALTER TABLE analysis_coverage DROP COLUMN member_results_json;

                DELETE FROM migration_history WHERE migration_id = 'M1-S5-WP4-0005';
                UPDATE store_metadata SET value = '4' WHERE key = 'schema_version';
                UPDATE store_metadata SET value = '1.3.0'
                    WHERE key = 'storage_contract_version';
                UPDATE store_metadata SET value =
                    '0e4fbeb821fdd83d86737d60979fa35d9a1300a4d971450c516f66d07ef2231e'
                    WHERE key = 'schema_fingerprint';
                PRAGMA user_version = 4;
                """;
            command.ExecuteNonQuery();
        }

        using (AuthoritativeStore migrated = migration.Open())
        {
            Assert.AreEqual(5, migrated.GetSchemaVersion());
        }

        using (SqliteConnection connection = migration.OpenRaw())
        {
            Assert.AreEqual(
                1L,
                ScalarInt64(
                    connection,
                    "SELECT COUNT(*) FROM migration_history WHERE migration_id = 'M1-S5-WP4-0005';"));
        }

        using TemporaryStore newer = new();
        using (AuthoritativeStore store = newer.Open())
        {
            Assert.AreEqual(5, store.GetSchemaVersion());
        }

        using (SqliteConnection connection = newer.OpenRaw())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version = 6;";
            command.ExecuteNonQuery();
        }

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(newer.Open);
        Assert.IsNotNull(exception.InnerException);
        StringAssert.Contains(
            exception.InnerException.Message,
            "Database schema 6 is newer than supported schema 5.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void AnalysisStateModelRejectsOpenStatesAndMutation()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        using SqliteConnection connection = temporary.OpenRaw();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO documentation_revisions(
                documentation_revision_id, source_id, source_kind, source_revision,
                content_sha256, byte_length, availability_state, retention_state,
                replay_state, created_at)
            VALUES (
                'revision-invalid', 'source-a', 'invented-source', '1',
                $sha, 0, 'unavailable', 'partial', 'audit-only', $now);
            """;
        command.Parameters.AddWithValue("$sha", new string('a', 64));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO documentation_revisions(
                documentation_revision_id, source_id, source_kind, source_revision,
                content_sha256, byte_length, availability_state, retention_state,
                replay_state, created_at)
            VALUES (
                'revision-valid', 'source-a', 'fixture', '1',
                $sha, 0, 'unavailable', 'partial', 'audit-only', $now);
            """;
        command.ExecuteNonQuery();
        command.CommandText =
            "UPDATE documentation_revisions SET source_revision = '2' WHERE documentation_revision_id = 'revision-valid';";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            "DELETE FROM documentation_revisions WHERE documentation_revision_id = 'revision-valid';";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    private static string[] SchemaNames(
        SqliteConnection connection,
        string type,
        IReadOnlyCollection<string> expectedNames)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_schema WHERE type = $type ORDER BY name;";
        command.Parameters.AddWithValue("$type", type);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> names = [];
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.Where(expectedNames.Contains).ToArray();
    }

    private static string ScalarText(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static long ScalarInt64(
        SqliteConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class TemporaryStore : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-analysis-storage-{Guid.NewGuid():N}");

        public AuthoritativeStore Open() => new(new StoragePaths(root));

        public SqliteConnection OpenRaw()
        {
            SqliteRuntimeIdentity.InitializeNativeProvider();
            using StoragePaths paths = new(root);
            SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = paths.Database,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            }.ToString());
            connection.Open();
            using SqliteCommand foreignKeys = connection.CreateCommand();
            foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
            foreignKeys.ExecuteNonQuery();
            return connection;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
