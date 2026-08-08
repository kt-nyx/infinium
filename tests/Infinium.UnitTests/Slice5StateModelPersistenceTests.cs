using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Slice5StateModelPersistenceTests
{
    private static readonly string[] Slice5Tables =
    [
        "analysis_candidates",
        "analysis_coverage",
        "analysis_dependency_edges",
        "analysis_gaps",
        "analysis_hypotheses",
        "analysis_recommendations",
        "analysis_replay_manifests",
        "analysis_run_outputs",
        "candidate_decisions",
        "case_memberships",
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
        "lineage_details",
        "payload_backup_pins",
        "reconciliation_details",
        "taxonomy_assignments",
    ];

    private static readonly string[] Slice5Indexes =
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
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelSchema4HasExactMigrationContractAndObjects()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        Assert.AreEqual(4, store.GetSchemaVersion());

        using SqliteConnection connection = temporary.OpenRaw();
        Assert.AreEqual("4", ScalarText(connection, "PRAGMA user_version;"));
        Assert.AreEqual(
            "4",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'schema_version';"));
        Assert.AreEqual(
            "1.3.0",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'storage_contract_version';"));
        Assert.AreEqual(
            "195fc92064e9f204157f5b355bac141516f00e496e5ed6962dd34280cbd3532d",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'schema_fingerprint';"));
        Assert.AreEqual(
            "3|4",
            ScalarText(
                connection,
                """
                SELECT CAST(from_version AS TEXT) || '|' || CAST(to_version AS TEXT)
                FROM migration_history
                WHERE migration_id = 'M1-S5-0004';
                """));

        CollectionAssert.AreEquivalent(
            Slice5Tables,
            SchemaNames(connection, "table", Slice5Tables));
        CollectionAssert.AreEquivalent(
            Slice5Indexes,
            SchemaNames(connection, "index", Slice5Indexes));

        foreach (string table in Slice5Tables)
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
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelMigratesSchema3ForwardAndRefusesNewerSchema()
    {
        using TemporaryStore migration = new();
        using (AuthoritativeStore store = migration.Open())
        {
            Assert.AreEqual(4, store.GetSchemaVersion());
        }

        using (SqliteConnection connection = migration.OpenRaw())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                string.Join(
                    Environment.NewLine,
                    Slice5Tables.Reverse().Select(table => $"DROP TABLE {table};"))
                +
                """

                DELETE FROM migration_history WHERE migration_id = 'M1-S5-0004';
                UPDATE store_metadata SET value = '3' WHERE key = 'schema_version';
                UPDATE store_metadata SET value = '1.2.0'
                    WHERE key = 'storage_contract_version';
                UPDATE store_metadata SET value =
                    '02fed67fa5dac6c28ec2a9f477733edc9f12eaa03a08f9d7dec05b502e45d6cf'
                    WHERE key = 'schema_fingerprint';
                PRAGMA user_version = 3;
                """;
            command.ExecuteNonQuery();
        }

        using (AuthoritativeStore migrated = migration.Open())
        {
            Assert.AreEqual(4, migrated.GetSchemaVersion());
        }

        using (SqliteConnection connection = migration.OpenRaw())
        {
            Assert.AreEqual(
                1L,
                ScalarInt64(
                    connection,
                    "SELECT COUNT(*) FROM migration_history WHERE migration_id = 'M1-S5-0004';"));
        }

        using TemporaryStore newer = new();
        using (AuthoritativeStore store = newer.Open())
        {
            Assert.AreEqual(4, store.GetSchemaVersion());
        }

        using (SqliteConnection connection = newer.OpenRaw())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version = 5;";
            command.ExecuteNonQuery();
        }

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(newer.Open);
        Assert.IsNotNull(exception.InnerException);
        StringAssert.Contains(
            exception.InnerException.Message,
            "Database schema 5 is newer than supported schema 4.");
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRejectsOpenStatesAndMutation()
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
            $"infinium-slice5-storage-{Guid.NewGuid():N}");

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
