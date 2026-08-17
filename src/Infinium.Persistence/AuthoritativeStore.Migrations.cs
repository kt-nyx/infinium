using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

#pragma warning disable IDE0008 // SQL transaction code is clearer with local type inference.
#pragma warning disable CA1512 // Guard clauses use parameter-specific messages.
#pragma warning disable CA1869 // The backup serializer is not a hot path.

namespace Infinium.Persistence;


public sealed partial class AuthoritativeStore
{
    private void ApplyMigrations()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var current = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (current > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema {current} is newer than supported schema {CurrentSchemaVersion}.");
            }

            if (current == 0)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV1, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    INSERT INTO store_metadata(key, value) VALUES ('schema_version', '1');
                    INSERT INTO store_metadata(key, value) VALUES ('schema_fingerprint', $schema_fingerprint);
                    INSERT INTO store_metadata(key, value) VALUES ('storage_contract_version', '1.0.0');
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_version', $sqlite_version);
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_source_id', $sqlite_source);
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S2-0001', 0, 1, $now, $sqlite_source);
                    PRAGMA user_version = 1;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_version", BindingIdentity.Version),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 1)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV2, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '2' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.1.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S3-0002', 1, 2, $now, $sqlite_source);
                    PRAGMA user_version = 2;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 2)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV3, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '3' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.2.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S4-0003', 2, 3, $now, $sqlite_source);
                    PRAGMA user_version = 3;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 3)
            {
                ValidateSchema3MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV4, transaction);
                CreateSchemaV4AppendOnlyTriggers(transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '4' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.3.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S5-0004', 3, 4, $now, $sqlite_source);
                    PRAGMA user_version = 4;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 4)
            {
                ValidateSchema4MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV5, transaction);
                CreateAppendOnlyTriggers(SchemaV5AppendOnlyTables, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '5' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.4.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S5-WP4-0005', 4, 5, $now, $sqlite_source);
                    PRAGMA user_version = 5;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 5)
            {
                ValidateSchema5MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV6, transaction);
                CreateAppendOnlyTriggers(SchemaV6AppendOnlyTables, transaction);
                CreateSchemaV6CanonicalTimestampTriggers(transaction);
                Execute(Wp2AuthorizationModeExtension, transaction);
                Execute(Wp2Schema6Extension, transaction);
                CreateAppendOnlyTriggers(Wp2Schema6ExtensionAppendOnlyTables, transaction);
                CreateCanonicalTimestampTriggers(Wp2Schema6ExtensionCanonicalTimestampColumns, transaction, replaceExisting: true);
                Execute(R2LiveSemanticSchema6Extension, transaction);
                CreateAppendOnlyTriggers(R2LiveSemanticSchema6ExtensionAppendOnlyTables, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '6' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.5.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp2_schema_extension_id','M1-S6-WP2-0006A');
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp3_schema_extension_id','M1-S6-WP3-0006B')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp3_schema_correction_id','M1-S6-WP3-0006C')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp5_schema_extension_id','M1-S6-WP5-0006D')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp5_schema_correction_id','M1-S6-WP5-0006E')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp6_schema_correction_id','M1-S6-WP6-0006F')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp6_active_contract_correction_id','M1-S6-WP6-0006G')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp7_schema_extension_id','M1-S6-WP7-0006H')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('wp9_campaign_input_bound_correction_id','M1-S6-WP9-0006I')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO store_metadata(key,value)
                    VALUES ('r2_live_semantic_extension_id','M1-S6-R2-0006J')
                    ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S6-0006', 5, 6, $now, $sqlite_source);
                    PRAGMA user_version = 6;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current == 6)
            {
                ApplyWp2Schema6ExtensionIfRequired();
                ApplyWp3Schema6ExtensionIfRequired();
                ApplyWp5Schema6ExtensionIfRequired();
                ApplyWp5Schema6CorrectionIfRequired();
                ApplyWp6Schema6CorrectionIfRequired();
                ApplyWp6ActiveContractCorrectionIfRequired();
                ApplyWp7Schema6ExtensionIfRequired();
                ApplyWp9CampaignInputBoundCorrectionIfRequired();
                ApplyR2LiveSemanticSchema6ExtensionIfRequired();
            }
        }
    }

    private void ApplyR2LiveSemanticSchema6ExtensionIfRequired()
    {
        using SqliteCommand declared = connection.CreateCommand();
        declared.CommandText =
            "SELECT COUNT(*) FROM store_metadata WHERE key='r2_live_semantic_extension_id' AND value='M1-S6-R2-0006J';";
        if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1)
        {
            return;
        }
        using SqliteTransaction transaction = BeginTransaction();
        Execute(R2LiveSemanticSchema6Extension, transaction);
        CreateAppendOnlyTriggers(R2LiveSemanticSchema6ExtensionAppendOnlyTables, transaction);
        string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
        Execute(
            "UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint'; "
            + "INSERT INTO store_metadata(key,value) VALUES('r2_live_semantic_extension_id','M1-S6-R2-0006J');",
            transaction,
            ("$fingerprint", upgradedFingerprint));
        transaction.Commit();
    }

    private void ApplyWp2Schema6ExtensionIfRequired()
    {
        const string acceptedWp1Fingerprint =
            "56dc6efd92fff75fe21f344abafa3b88b99a8e92d2d1b2517f706d63af4599a3";
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint != acceptedWp1Fingerprint)
        {
            return;
        }

        using (SqliteCommand state = connection.CreateCommand())
        {
            state.CommandText =
                """
                SELECT
                  (SELECT COUNT(*) FROM provider_operation_authorizations)
                  + (SELECT COUNT(*) FROM provider_reservations)
                  + (SELECT COUNT(*) FROM provider_budget_projection);
                """;
            if (Convert.ToInt64(state.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0)
            {
                throw new InvalidOperationException(
                    "The accepted WP1 schema-6 store contains provider execution state and cannot receive the bounded WP2 same-version extension automatically.");
            }
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(Wp2AuthorizationModeExtension, transaction);
        Execute(Wp2Schema6Extension, transaction);
        CreateAppendOnlyTriggers(Wp2Schema6ExtensionAppendOnlyTables, transaction);
        CreateCanonicalTimestampTriggers(Wp2Schema6ExtensionCanonicalTimestampColumns, transaction, replaceExisting: true);
        string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
        Execute(
            """
            UPDATE store_metadata SET value = $schema_fingerprint
            WHERE key = 'schema_fingerprint';
            INSERT INTO store_metadata(key,value)
            VALUES ('wp2_schema_extension_id','M1-S6-WP2-0006A');
            """,
            transaction,
            ("$schema_fingerprint", schemaFingerprint),
            ("$sqlite_source", BindingIdentity.SourceId),
            ("$now", ToText(DateTimeOffset.UtcNow)));
        transaction.Commit();
    }

    private void ApplyWp3Schema6ExtensionIfRequired()
    {
        const string acceptedWp2Fingerprint =
            "240a06fe2a9fa3d79db63985fbda329c8e83822534b93cbfb539062a109cad9e";
        const string rejectedWp3Fingerprint =
            "554129523ac64ce52ee4d24e90644dbaa167c0d98602f1c2d0f25ad271ec0581";
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.SchemaFingerprint
            or ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp7ExtensionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6ActiveContractCorrectionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6CorrectionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp5ExtensionSourceSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                """
                SELECT COUNT(*) FROM store_metadata
                WHERE (key='wp3_schema_extension_id' AND value='M1-S6-WP3-0006B')
                   OR (key='wp3_schema_correction_id' AND value='M1-S6-WP3-0006C');
                """;
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 2)
            {
                throw new InvalidOperationException(
                    "The current WP3 schema fingerprint is missing its exact same-version extension declaration.");
            }
            return;
        }
        if (actualFingerprint is not (acceptedWp2Fingerprint or rejectedWp3Fingerprint))
        {
            throw new InvalidOperationException(
                $"Schema 6 does not match the exact accepted WP2 storage contract or current WP3 same-version extension ({actualFingerprint}).");
        }

        using (SqliteCommand metadata = connection.CreateCommand())
        {
            metadata.CommandText =
                """
                SELECT COUNT(*) FROM store_metadata
                WHERE (key='schema_version' AND value='6')
                   OR (key='storage_contract_version' AND value='1.5.0')
                   OR (key='schema_fingerprint' AND value=$fingerprint)
                   OR (key='wp2_schema_extension_id' AND value='M1-S6-WP2-0006A')
                   OR (key='wp3_schema_extension_id' AND value='M1-S6-WP3-0006B');
                """;
            metadata.Parameters.AddWithValue("$fingerprint", actualFingerprint);
            long expectedMetadataCount = actualFingerprint == acceptedWp2Fingerprint ? 4 : 5;
            if (Convert.ToInt64(metadata.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != expectedMetadataCount)
            {
                throw new InvalidOperationException(
                    "The accepted WP2 schema lacks its exact schema-6/storage-1.5.0 extension provenance.");
            }
        }

        List<(string Name, string Sql)> intentTriggers = [];
        using (SqliteCommand triggers = connection.CreateCommand())
        {
            triggers.CommandText =
                "SELECT name,sql FROM sqlite_schema WHERE type='trigger' AND tbl_name='provider_credential_intents' ORDER BY name;";
            using SqliteDataReader reader = triggers.ExecuteReader();
            while (reader.Read())
            {
                intentTriggers.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        if (intentTriggers.Count == 0)
        {
            throw new InvalidOperationException("The accepted WP2 credential-intent trigger set is absent.");
        }

        Execute("PRAGMA foreign_keys=OFF; PRAGMA legacy_alter_table=ON;", null);
        try
        {
            using SqliteTransaction transaction = BeginTransaction();
            Execute("ALTER TABLE provider_credential_intents RENAME TO provider_credential_intents_wp2;", transaction);
            Execute(ExtractSchemaStatement(SchemaV6, "CREATE TABLE " + "provider_credential_intents("), transaction);
            Execute(
                """
                INSERT INTO provider_credential_intents
                SELECT * FROM provider_credential_intents_wp2;
                DROP TABLE provider_credential_intents_wp2;
                """,
                transaction);
            foreach ((string name, string sql) in intentTriggers)
            {
                Execute(name is "provider_profile_transition_order_guard" or "provider_delete_pending_never_reactivates_guard"
                    ? ExtractSchemaStatement(SchemaV6, $"CREATE TRIGGER {name}")
                    : sql,
                    transaction);
            }
            foreach (string triggerName in new[]
                     {
                         "provider_profile_projection_exact_root_insert_guard",
                         "provider_profile_projection_monotonic_update_guard",
                         "provider_block_eligibility_guard",
                     })
            {
                Execute($"DROP TRIGGER {triggerName};", transaction);
                Execute(ExtractSchemaStatement(SchemaV6, $"CREATE TRIGGER {triggerName}"), transaction);
            }
            string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
            if (upgradedFingerprint != ProviderPersistenceDeclarations.Wp5ExtensionSourceSchemaFingerprint)
            {
                throw new InvalidOperationException(
                    $"The bounded WP3 same-version extension did not converge on the declared fingerprint ({upgradedFingerprint}).");
            }
            Execute(
                """
                UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
                INSERT INTO store_metadata(key,value) VALUES('wp3_schema_extension_id','M1-S6-WP3-0006B')
                  ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                INSERT INTO store_metadata(key,value) VALUES('wp3_schema_correction_id','M1-S6-WP3-0006C')
                  ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """,
                transaction,
                ("$fingerprint", upgradedFingerprint));
            transaction.Commit();
        }
        finally
        {
            Execute("PRAGMA legacy_alter_table=OFF; PRAGMA foreign_keys=ON;", null);
        }
    }

    private void ApplyWp5Schema6ExtensionIfRequired()
    {
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.SchemaFingerprint
            or ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp7ExtensionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6ActiveContractCorrectionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6CorrectionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp5CorrectionSourceSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                "SELECT COUNT(*) FROM store_metadata WHERE key='wp5_schema_extension_id' AND value='M1-S6-WP5-0006D';";
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The current WP5 schema fingerprint is missing its exact same-version extension declaration.");
            }
            return;
        }

        if (actualFingerprint != ProviderPersistenceDeclarations.Wp5ExtensionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException(
                "Schema 6 does not match the exact accepted WP3 storage contract or current WP5 same-version extension.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute("DROP TRIGGER provider_usage_response_totality_guard;", transaction);
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TRIGGER provider_usage_response_totality_guard"), transaction);
        string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (upgradedFingerprint != ProviderPersistenceDeclarations.Wp5CorrectionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"The bounded WP5 same-version extension did not converge on the declared fingerprint ({upgradedFingerprint}).");
        }
        Execute(
            """
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO store_metadata(key,value) VALUES('wp5_schema_extension_id','M1-S6-WP5-0006D')
              ON CONFLICT(key) DO UPDATE SET value=excluded.value;
            """,
            transaction,
            ("$fingerprint", upgradedFingerprint));
        transaction.Commit();
    }

    private void ApplyWp5Schema6CorrectionIfRequired()
    {
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.SchemaFingerprint
            or ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp7ExtensionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6ActiveContractCorrectionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6CorrectionSourceSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                "SELECT COUNT(*) FROM store_metadata WHERE key='wp5_schema_correction_id' AND value='M1-S6-WP5-0006E';";
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException("The current WP5 correction fingerprint lacks its exact provenance.");
            }
            return;
        }
        if (actualFingerprint != ProviderPersistenceDeclarations.Wp5CorrectionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException("Schema 6 does not match the exact WP5 correction source.");
        }

        List<string> triggerNames = [];
        using (SqliteCommand triggers = connection.CreateCommand())
        {
            triggers.CommandText = "SELECT name FROM sqlite_schema WHERE type='trigger' AND tbl_name='provider_responses' ORDER BY name;";
            using SqliteDataReader reader = triggers.ExecuteReader();
            while (reader.Read()) { triggerNames.Add(reader.GetString(0)); }
        }
        Execute("PRAGMA foreign_keys=OFF; PRAGMA legacy_alter_table=ON;", null);
        try
        {
            using SqliteTransaction transaction = BeginTransaction();
            Execute("ALTER TABLE provider_responses RENAME TO provider_responses_wp5;", transaction);
            Execute(ExtractSchemaStatement(SchemaV6, "CREATE TABLE " + "provider_responses("), transaction);
            Execute("INSERT INTO provider_responses SELECT * FROM provider_responses_wp5; DROP TABLE provider_responses_wp5;", transaction);
            foreach (string triggerName in triggerNames)
            {
                Execute(ExtractSchemaStatement(SchemaV6, $"CREATE TRIGGER {triggerName}"), transaction);
            }
            string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
            if (upgradedFingerprint != ProviderPersistenceDeclarations.SchemaFingerprint)
            {
                throw new InvalidOperationException($"The bounded WP5 correction did not converge on its declared fingerprint ({upgradedFingerprint}).");
            }
            Execute(
                """
                UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
                INSERT INTO store_metadata(key,value) VALUES('wp5_schema_correction_id','M1-S6-WP5-0006E')
                  ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """,
                transaction,
                ("$fingerprint", upgradedFingerprint));
            transaction.Commit();
        }
        finally
        {
            Execute("PRAGMA legacy_alter_table=OFF; PRAGMA foreign_keys=ON;", null);
        }
    }

    private void ApplyWp6Schema6CorrectionIfRequired()
    {
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.SchemaFingerprint
            or ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp7ExtensionSourceSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp6ActiveContractCorrectionSourceSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                "SELECT COUNT(*) FROM store_metadata WHERE key='wp6_schema_correction_id' AND value='M1-S6-WP6-0006F';";
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException("The current WP6 correction fingerprint lacks its exact provenance.");
            }
            return;
        }
        if (actualFingerprint != ProviderPersistenceDeclarations.Wp6CorrectionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException("Schema 6 does not match the exact WP6 correction source.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(
            """
            DROP TRIGGER provider_semantic_proposal_root_guard;
            DROP TRIGGER provider_semantic_admission_application_guard;
            """,
            transaction);
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TRIGGER provider_semantic_proposal_root_guard"), transaction);
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TRIGGER provider_semantic_admission_application_guard"), transaction);
        Execute(ExtractSchemaStatement(SchemaV6,
            "CREATE TRIGGER evidence_acquisition_application_admitted_artifact_guard"), transaction);
        string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (upgradedFingerprint != ProviderPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"The bounded WP6 correction did not converge on its declared fingerprint ({upgradedFingerprint}).");
        }
        Execute(
            """
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO store_metadata(key,value) VALUES('wp6_schema_correction_id','M1-S6-WP6-0006F')
              ON CONFLICT(key) DO UPDATE SET value=excluded.value;
            """,
            transaction,
            ("$fingerprint", upgradedFingerprint));
        transaction.Commit();
    }

    private void ApplyWp6ActiveContractCorrectionIfRequired()
    {
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.SchemaFingerprint
            or ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint
            or ProviderPersistenceDeclarations.Wp7ExtensionSourceSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                "SELECT COUNT(*) FROM store_metadata WHERE key='wp6_active_contract_correction_id' AND value='M1-S6-WP6-0006G';";
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException("The current WP6 active contract fingerprint lacks its exact provenance.");
            }
            return;
        }
        if (actualFingerprint != ProviderPersistenceDeclarations.Wp6ActiveContractCorrectionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException("Schema 6 does not match the exact WP6 active contract correction source.");
        }

        using SqliteCommand ambiguity = connection.CreateCommand();
        ambiguity.CommandText =
            """
            SELECT COUNT(*) FROM evidence_acquisition_application_links link
            WHERE (SELECT COUNT(*) FROM provider_semantic_admissions admission
              WHERE admission.owner_kind='evidence-acquisition-run'
                AND admission.owner_id=link.acquisition_run_id
                AND admission.state='admitted'
                AND admission.admitted_artifact_id=link.admitted_artifact_id) <> 1;
            """;
        if (Convert.ToInt64(ambiguity.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0)
        {
            throw new InvalidOperationException(
                "Existing source-claim applications do not map to one exact admitted artifact identity.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(
            """
            DROP TRIGGER evidence_acquisition_application_links_append_only_update;
            DROP TRIGGER evidence_acquisition_application_links_append_only_delete;
            DROP TRIGGER evidence_acquisition_application_links_created_at_canonical_utc_insert;
            DROP TRIGGER evidence_acquisition_application_admitted_artifact_guard;
            DROP TRIGGER provider_semantic_admission_application_guard;
            ALTER TABLE provider_semantic_proposals RENAME COLUMN application_link_id TO semantic_link_id;
            ALTER TABLE provider_semantic_admissions RENAME COLUMN application_link_id TO semantic_link_id;
            CREATE UNIQUE INDEX idx_provider_semantic_admission_artifact_owner
              ON provider_semantic_admissions(admission_id,owner_id,admitted_artifact_id);
            ALTER TABLE evidence_acquisition_application_links RENAME TO evidence_acquisition_application_links_old;
            """,
            transaction);
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TABLE evidence_acquisition_application_links"), transaction);
        Execute(
            """
            INSERT INTO evidence_acquisition_application_links(
              application_link_id,acquisition_run_id,admission_id,analysis_run_id,application_scope_id,
              cost_attribution_scope_id,admitted_artifact_id,created_at)
            SELECT link.application_link_id,link.acquisition_run_id,
              (SELECT admission.admission_id FROM provider_semantic_admissions admission
               WHERE admission.owner_kind='evidence-acquisition-run'
                 AND admission.owner_id=link.acquisition_run_id
                 AND admission.state='admitted'
                 AND admission.admitted_artifact_id=link.admitted_artifact_id),
              link.analysis_run_id,link.application_scope_id,link.cost_attribution_scope_id,
              link.admitted_artifact_id,link.created_at
            FROM evidence_acquisition_application_links_old link;
            DROP TABLE evidence_acquisition_application_links_old;
            """,
            transaction);
        Execute(ExtractSchemaStatement(SchemaV6,
            "CREATE TRIGGER evidence_acquisition_application_admitted_artifact_guard"), transaction);
        Execute(ExtractSchemaStatement(SchemaV6,
            "CREATE TRIGGER provider_semantic_admission_application_guard"), transaction);
        CreateAppendOnlyTriggers(["evidence_acquisition_application_links"], transaction);
        CreateCanonicalTimestampTriggers(
            [("evidence_acquisition_application_links", "created_at", false)], transaction);
        string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (upgradedFingerprint != ProviderPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"The bounded WP6 active contract correction did not converge on its declared fingerprint ({upgradedFingerprint}).");
        }
        Execute(
            """
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO store_metadata(key,value) VALUES('wp6_active_contract_correction_id','M1-S6-WP6-0006G')
              ON CONFLICT(key) DO UPDATE SET value=excluded.value;
            """,
            transaction,
            ("$fingerprint", upgradedFingerprint));
        transaction.Commit();
    }

    private void ApplyWp7Schema6ExtensionIfRequired()
    {
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.SchemaFingerprint
            or ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                "SELECT COUNT(*) FROM store_metadata WHERE key='wp7_schema_extension_id' AND value='M1-S6-WP7-0006H';";
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException("The current WP7 schema fingerprint lacks its exact provenance.");
            }
            return;
        }
        if (actualFingerprint != ProviderPersistenceDeclarations.Wp7ExtensionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException("Schema 6 does not match the exact WP7 extension source.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TABLE candidate_investigation_outcomes"), transaction);
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TRIGGER candidate_investigation_outcome_candidate_guard"), transaction);
        Execute(ExtractSchemaStatement(SchemaV6, "CREATE TRIGGER candidate_investigation_outcome_response_guard"), transaction);
        CreateAppendOnlyTriggers(["candidate_investigation_outcomes"], transaction);
        CreateCanonicalTimestampTriggers([("candidate_investigation_outcomes", "created_at", false)], transaction);
        string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (upgradedFingerprint != ProviderPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"The bounded WP7 same-version extension did not converge on its declared fingerprint ({upgradedFingerprint}).");
        }
        Execute(
            """
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO store_metadata(key,value) VALUES('wp7_schema_extension_id','M1-S6-WP7-0006H')
              ON CONFLICT(key) DO UPDATE SET value=excluded.value;
            """,
            transaction,
            ("$fingerprint", upgradedFingerprint));
        transaction.Commit();
    }

    private void ApplyWp9CampaignInputBoundCorrectionIfRequired()
    {
        string actualFingerprint = ComputeSchemaFingerprint(connection);
        if (actualFingerprint is ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint
            or ProviderPersistenceDeclarations.R2LiveSemanticSchemaFingerprint)
        {
            using SqliteCommand declared = connection.CreateCommand();
            declared.CommandText =
                "SELECT COUNT(*) FROM store_metadata WHERE key='wp9_campaign_input_bound_correction_id' AND value='M1-S6-WP9-0006I';";
            if (Convert.ToInt64(declared.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The current WP9 campaign input-bound correction fingerprint lacks its exact provenance.");
            }
            return;
        }
        if (actualFingerprint != ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSourceSchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"Schema 6 does not match the exact WP9 campaign input-bound correction source ({actualFingerprint}).");
        }

        using (SqliteCommand state = connection.CreateCommand())
        {
            state.CommandText =
                "SELECT (SELECT COUNT(*) FROM provider_operation_authorizations) + (SELECT COUNT(*) FROM provider_requests);";
            if (Convert.ToInt64(state.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0)
            {
                throw new InvalidOperationException(
                    "The accepted schema-6 store contains provider execution state and cannot receive the clean-break WP9 input-bound correction automatically.");
            }
        }

        List<(string Name, string Sql)> triggers = [];
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT name,sql FROM sqlite_schema WHERE type='trigger' AND tbl_name IN ('provider_operation_authorizations','provider_requests') ORDER BY name;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                triggers.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        static string UpgradePolicy(string sql) => sql.Replace(
            "input_bound_policy_version = 'v1'",
            "input_bound_policy_version = 'v2'",
            StringComparison.Ordinal).Replace(
            "input_bound_policy_version <> 'v1'",
            "input_bound_policy_version <> 'v2'",
            StringComparison.Ordinal);

        Execute("PRAGMA foreign_keys=OFF; PRAGMA legacy_alter_table=ON;", null);
        try
        {
            using SqliteTransaction transaction = BeginTransaction();
            Execute(
                "ALTER TABLE provider_requests RENAME TO provider_requests_wp9; "
                + "ALTER TABLE provider_operation_authorizations RENAME TO provider_operation_authorizations_wp9;",
                transaction);
            Execute(UpgradePolicy(ExtractSchemaStatement(
                SchemaV6, "CREATE TABLE " + "provider_operation_authorizations(")), transaction);
            Execute(UpgradePolicy(ExtractSchemaStatement(
                SchemaV6, "CREATE TABLE " + "provider_requests(")), transaction);
            Execute(
                "DROP TABLE provider_requests_wp9; DROP TABLE provider_operation_authorizations_wp9; "
                + "CREATE UNIQUE INDEX idx_provider_request_fingerprint ON provider_requests(request_fingerprint);",
                transaction);
            foreach ((string _, string sql) in triggers)
            {
                Execute(UpgradePolicy(sql), transaction);
            }

            string upgradedFingerprint = ComputeSchemaFingerprint(connection, transaction);
            if (upgradedFingerprint != ProviderPersistenceDeclarations.Wp9CampaignInputBoundCorrectionSchemaFingerprint)
            {
                throw new InvalidOperationException(
                    $"The bounded WP9 campaign input-bound correction did not converge on its declared fingerprint ({upgradedFingerprint}).");
            }
            Execute(
                """
                UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
                INSERT INTO store_metadata(key,value) VALUES('wp9_campaign_input_bound_correction_id','M1-S6-WP9-0006I')
                  ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """,
                transaction,
                ("$fingerprint", upgradedFingerprint));
            transaction.Commit();
        }
        finally
        {
            Execute("PRAGMA legacy_alter_table=OFF; PRAGMA foreign_keys=ON;", null);
        }
    }

    private static string ExtractSchemaStatement(string schema, string marker)
    {
        int start = schema.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException($"Schema statement marker '{marker}' is absent.");
        }
        int end = schema.IndexOf("\nEND;", start, StringComparison.Ordinal);
        int strictEnd = schema.IndexOf(") STRICT;", start, StringComparison.Ordinal);
        if (marker.StartsWith("CREATE TABLE", StringComparison.Ordinal)
            && strictEnd >= 0 && (end < 0 || strictEnd < end))
        {
            return schema[start..(strictEnd + ") STRICT;".Length)];
        }
        if (end < 0)
        {
            throw new InvalidOperationException($"Schema trigger marker '{marker}' has no terminal END.");
        }
        return schema[start..(end + "\nEND;".Length)];
    }

    private void ValidateSchema5MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        const string schema5Fingerprint = "e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d";
        if (metadata.Count != 5
            || metadata["schema_version"] != "5"
            || metadata["storage_contract_version"] != "1.4.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != schema5Fingerprint
            || ComputeSchemaFingerprint(connection) != schema5Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 5 does not match the exact accepted Slice 5 storage contract required for M1-S6-0006.");
        }

        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S5-WP4-0005' AND from_version = 4 AND to_version = 5
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(migration.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("Schema 5 migration provenance is invalid.");
        }
    }

    private void ValidateSchema4MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }
        const string schema4Fingerprint = "0e4fbeb821fdd83d86737d60979fa35d9a1300a4d971450c516f66d07ef2231e";
        if (metadata.Count != 5
            || metadata["schema_version"] != "4"
            || metadata["storage_contract_version"] != "1.3.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != schema4Fingerprint
            || ComputeSchemaFingerprint(connection) != schema4Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 4 does not match the exact accepted analysis contract required for finding and case storage migration.");
        }
        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S5-0004' AND from_version = 3 AND to_version = 4
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(migration.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("Schema 4 migration provenance is invalid.");
        }
    }

    private void ValidateSchema3MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        if (metadata.Count != 5
            || metadata["schema_version"] != "3"
            || metadata["storage_contract_version"] != "1.2.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != SchemaV3Fingerprint
            || ComputeSchemaFingerprint(connection) != SchemaV3Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 3 does not match the exact accepted storage contract required for migration.");
        }

        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S4-0003'
              AND from_version = 2
              AND to_version = 3
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(
                migration.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException(
                "Schema 3 migration provenance is invalid.");
        }
    }

    private void CreateSchemaV4AppendOnlyTriggers(SqliteTransaction transaction)
        => CreateAppendOnlyTriggers(SchemaV4AppendOnlyTables, transaction);

    private void CreateAppendOnlyTriggers(IEnumerable<string> tables, SqliteTransaction transaction)
    {
        foreach (string table in tables)
        {
            Execute(
                $"""
                CREATE TRIGGER {table}_append_only_update
                BEFORE UPDATE ON {table}
                BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                CREATE TRIGGER {table}_append_only_delete
                BEFORE DELETE ON {table}
                BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                """,
                transaction);
        }
    }

    private void CreateSchemaV6CanonicalTimestampTriggers(SqliteTransaction transaction)
    {
        CreateCanonicalTimestampTriggers(SchemaV6CanonicalTimestampColumns, transaction);
    }

    private void CreateCanonicalTimestampTriggers(
        IReadOnlyList<(string Table, string Column, bool Optional)> columns,
        SqliteTransaction transaction,
        bool replaceExisting = false)
    {
        foreach ((string table, string column, bool optional) in columns)
        {
            if (replaceExisting)
            {
                Execute(
                    $"DROP TRIGGER IF EXISTS {table}_{column}_canonical_utc_insert; DROP TRIGGER IF EXISTS {table}_{column}_canonical_utc_update;",
                    transaction);
            }
            string prefix = optional ? $"NEW.{column} IS NOT NULL AND " : string.Empty;
            string valid = $"""
                  length(NEW.{column}) = 33
                  AND NEW.{column} GLOB '????-??-??T??:??:??.???????+00:00'
                  AND substr(NEW.{column},1,4) NOT GLOB '*[^0-9]*'
                  AND substr(NEW.{column},6,2) NOT GLOB '*[^0-9]*'
                  AND substr(NEW.{column},9,2) NOT GLOB '*[^0-9]*'
                  AND substr(NEW.{column},12,2) NOT GLOB '*[^0-9]*'
                  AND substr(NEW.{column},15,2) NOT GLOB '*[^0-9]*'
                  AND substr(NEW.{column},18,2) NOT GLOB '*[^0-9]*'
                  AND substr(NEW.{column},21,7) NOT GLOB '*[^0-9]*'
                  AND CAST(substr(NEW.{column},1,4) AS INTEGER) BETWEEN 1 AND 9999
                  AND CAST(substr(NEW.{column},6,2) AS INTEGER) BETWEEN 1 AND 12
                  AND CAST(substr(NEW.{column},9,2) AS INTEGER) BETWEEN 1 AND CASE
                    WHEN CAST(substr(NEW.{column},6,2) AS INTEGER) IN (1,3,5,7,8,10,12) THEN 31
                    WHEN CAST(substr(NEW.{column},6,2) AS INTEGER) IN (4,6,9,11) THEN 30
                    WHEN (CAST(substr(NEW.{column},1,4) AS INTEGER) % 400 = 0)
                      OR (CAST(substr(NEW.{column},1,4) AS INTEGER) % 4 = 0
                        AND CAST(substr(NEW.{column},1,4) AS INTEGER) % 100 <> 0) THEN 29
                    ELSE 28 END
                  AND CAST(substr(NEW.{column},12,2) AS INTEGER) BETWEEN 0 AND 23
                  AND CAST(substr(NEW.{column},15,2) AS INTEGER) BETWEEN 0 AND 59
                  AND CAST(substr(NEW.{column},18,2) AS INTEGER) BETWEEN 0 AND 59
                """;
            Execute(
                $"""
                CREATE TRIGGER {table}_{column}_canonical_utc_insert
                BEFORE INSERT ON {table}
                WHEN {prefix}({valid}) IS NOT TRUE
                BEGIN SELECT RAISE(ABORT, 'non-canonical UTC authority timestamp'); END;
                """,
                transaction);
            if (SchemaV6MutableProjectionTables.Contains(table))
            {
                Execute(
                    $"""
                    CREATE TRIGGER {table}_{column}_canonical_utc_update
                    BEFORE UPDATE OF {column} ON {table}
                    WHEN {prefix}({valid}) IS NOT TRUE
                    BEGIN SELECT RAISE(ABORT, 'non-canonical UTC authority timestamp'); END;
                    """,
                    transaction);
            }
        }
    }

    private static readonly HashSet<string> SchemaV6MutableProjectionTables =
    [
        "provider_operation_projection",
        "provider_profile_projection",
        "provider_budget_projection",
    ];

    private static readonly (string Table, string Column, bool Optional)[] SchemaV6CanonicalTimestampColumns =
    [
        ("provider_access_profiles", "created_at", false),
        ("provider_generations", "created_at", false),
        ("provider_credential_intents", "created_at", false),
        ("provider_credential_intent_events", "created_at", false),
        ("provider_credential_terminal_root_consumptions", "created_at", false),
        ("provider_capability_snapshots", "created_at", false),
        ("provider_price_snapshots", "created_at", false),
        ("provider_effective_scan_configurations_v2", "created_at", false),
        ("evidence_acquisition_runs", "created_at", false),
        ("evidence_acquisition_job_nodes", "created_at", false),
        ("evidence_acquisition_attempts", "created_at", false),
        ("evidence_acquisition_commands", "requested_at", false),
        ("provider_command_bindings", "requested_at", false),
        ("evidence_acquisition_parent_links", "created_at", false),
        ("evidence_acquisition_application_links", "created_at", false),
        ("provider_operation_blocks", "requested_at", false),
        ("provider_operation_blocks", "confirmed_at", false),
        ("provider_operation_blocks", "dispatch_deadline_utc", false),
        ("provider_operation_blocks", "recorded_at", false),
        ("provider_operation_authorizations", "requested_at", true),
        ("provider_operation_authorizations", "confirmed_at", true),
        ("provider_operation_authorizations", "dispatch_deadline_utc", true),
        ("provider_operation_attempts", "created_at", false),
        ("provider_requests", "created_at", false),
        ("provider_reservations", "expires_at", false),
        ("provider_reservations", "created_at", false),
        ("provider_dispatch_fences", "evaluated_at", false),
        ("provider_transport_events", "occurred_at", false),
        ("provider_responses", "created_at", false),
        ("provider_response_finalizations", "finalized_at", false),
        ("provider_usage_entries", "created_at", false),
        ("provider_rate_limit_facts", "observed_at", false),
        ("provider_rate_limit_facts", "resets_at", true),
        ("provider_settlements", "created_at", false),
        ("provider_settlement_adjustments", "created_at", false),
        ("provider_semantic_proposals", "created_at", false),
        ("provider_semantic_validations", "created_at", false),
        ("provider_semantic_admissions", "created_at", false),
        ("candidate_investigation_outcomes", "created_at", false),
        ("provider_replay_edges", "created_at", false),
        ("provider_run_output_v2_bindings", "created_at", false),
        ("provider_operation_projection", "updated_at", false),
        ("provider_profile_projection", "updated_at", false),
        ("provider_budget_projection", "updated_at", false),
    ];

    private static readonly string[] Wp2Schema6ExtensionAppendOnlyTables =
    [
        "provider_reservation_scope_items",
        "provider_budget_limits",
        "provider_budget_events",
        "provider_usage_rollup_references",
        "provider_budget_settlement_receipts",
    ];

    private static readonly string[] R2LiveSemanticSchema6ExtensionAppendOnlyTables =
    [
        "source_claim_admitted_artifacts",
        "source_claim_applicability_facts",
        "candidate_evidence_authority",
    ];

    private static readonly (string Table, string Column, bool Optional)[] Wp2Schema6ExtensionCanonicalTimestampColumns =
    [
        ("provider_budget_limits", "created_at", false),
        ("provider_budget_events", "occurred_at", false),
        ("provider_usage_rollup_references", "created_at", false),
        ("provider_budget_settlement_receipts", "created_at", false),
        ("provider_budget_projection", "updated_at", false),
    ];

    private static readonly HashSet<string> RequiredSchemaObjects =
    [
        .. R2LiveSemanticSchema6ExtensionAppendOnlyTables.Select(table => $"table:{table}"),
        .. R2LiveSemanticSchema6ExtensionAppendOnlyTables.SelectMany(table => new[]
        {
            $"trigger:{table}_append_only_update",
            $"trigger:{table}_append_only_delete",
        }),
        .. SchemaV6CanonicalTimestampColumns.Select(item =>
            $"trigger:{item.Table}_{item.Column}_canonical_utc_insert"),
        .. Wp2Schema6ExtensionCanonicalTimestampColumns.Select(item =>
            $"trigger:{item.Table}_{item.Column}_canonical_utc_insert"),
        .. SchemaV6CanonicalTimestampColumns
            .Where(item => SchemaV6MutableProjectionTables.Contains(item.Table))
            .Select(item => $"trigger:{item.Table}_{item.Column}_canonical_utc_update"),
        "trigger:provider_usage_response_totality_guard",
        "index:idx_attempts_run",
        "index:idx_attempts_one_live_per_run",
        "index:idx_candidate_decisions_run_population",
        "index:idx_candidates_run_lane",
        "index:idx_case_memberships_member",
        "index:idx_coverage_run_population",
        "index:idx_dependency_edges_from",
        "index:idx_dependency_edges_to",
        "index:idx_documentation_passages_revision",
        "index:idx_documentation_imports_revision",
        "index:idx_documentation_application_bindings_run",
        "index:idx_documentation_deletion_receipts_revision",
        "index:idx_effect_receipts_run",
        "index:idx_evidence_applications_run",
        "index:idx_evidence_revisions_passage",
        "index:idx_events_run_sequence",
        "index:idx_findings_signature",
        "index:idx_gaps_run_population",
        "index:idx_hypotheses_candidate",
        "index:idx_lineage_successor",
        "index:idx_recommendations_finding",
        "index:idx_reconciliation_successor",
        "index:idx_replay_manifests_run",
        "index:idx_run_outputs_run",
        "index:idx_runs_created",
        "index:idx_runs_dispatch",
        "index:idx_snapshot_capture_dispatch",
        "index:idx_snapshot_capture_one_live_attempt",
        "index:idx_taxonomy_subject",
        "index:idx_provider_active_generation",
        "index:idx_payload_identity_size",
        "index:idx_provider_request_fingerprint",
        "index:idx_provider_reservation_scope",
        "index:idx_provider_budget_events_scope",
        "index:idx_provider_semantic_admission_artifact_owner",
        "table:analysis_candidates",
        "table:analysis_coverage",
        "table:analysis_coverage_failure_links",
        "table:analysis_coverage_gap_links",
        "table:analysis_coverage_taxonomy_links",
        "table:analysis_dependency_edges",
        "table:analysis_gaps",
        "table:analysis_hypotheses",
        "table:analysis_recommendations",
        "table:analysis_replay_manifests",
        "table:analysis_run_outputs",
        "table:attempts",
        "table:audit_events",
        "table:candidate_decisions",
        "table:case_memberships",
        "table:case_hypothesis_memberships",
        "table:case_occurrence_details",
        "table:case_occurrences",
        "table:checkpoints",
        "table:coordinator_leases",
        "table:documentation_passages",
        "table:documentation_imports",
        "table:documentation_revisions",
        "table:documentation_application_bindings",
        "table:documentation_deletion_receipts",
        "table:documentation_purpose_assignment_details",
        "table:documentation_gap_details",
        "table:durable_commands",
        "table:effect_receipts",
        "table:evidence_application_links",
        "table:evidence_revisions",
        "table:finding_occurrence_details",
        "table:finding_case_abstentions",
        "table:finding_case_case_details",
        "table:finding_case_finding_details",
        "table:finding_case_gap_details",
        "table:finding_case_publications",
        "table:finding_case_recommendations",
        "table:finding_case_taxonomy_assignments",
        "table:finding_promotion_assessments",
        "table:finding_occurrences",
        "table:job_nodes",
        "table:lifecycle_events",
        "table:lineage_details",
        "table:lineage_event_edges",
        "table:lineage_events",
        "table:logical_cases",
        "table:logical_findings",
        "table:migration_history",
        "table:payload_owners",
        "table:payload_backup_pins",
        "table:payloads",
        "table:publication_receipt_payloads",
        "table:publication_receipts",
        "table:reconciliation_assessments",
        "table:reconciliation_details",
        "table:reconciliation_metadata",
        "table:reconciliation_proof_links",
        "table:run_projection",
        "table:run_operations",
        "table:runs",
        "table:store_metadata",
        "table:snapshot_capture_attempts",
        "table:snapshot_capture_operations",
        "table:snapshot_capture_publications",
        "table:taxonomy_assignments",
        "table:taxonomy_projection_edges",
        "table:evidence_acquisition_application_links",
        "table:evidence_acquisition_attempts",
        "table:evidence_acquisition_commands",
        "table:evidence_acquisition_job_nodes",
        "table:evidence_acquisition_parent_links",
        "table:evidence_acquisition_runs",
        "table:provider_access_profiles",
        "table:provider_budget_projection",
        "table:provider_budget_limits",
        "table:provider_budget_events",
        "table:provider_usage_rollup_references",
        "table:provider_budget_settlement_receipts",
        "table:provider_capability_snapshots",
        "table:provider_credential_intents",
        "table:provider_credential_intent_events",
        "table:provider_credential_terminal_root_consumptions",
        "table:provider_command_bindings",
        "table:provider_dispatch_fences",
        "table:provider_generations",
        "table:provider_effective_scan_configurations_v2",
        "table:provider_operation_attempts",
        "table:provider_operation_authorizations",
        "table:provider_operation_blocks",
        "table:provider_operation_projection",
        "table:provider_profile_projection",
        "table:provider_price_rules",
        "table:provider_price_snapshots",
        "table:provider_rate_limit_facts",
        "table:provider_replay_edges",
        "table:provider_run_output_v2_bindings",
        "table:provider_requests",
        "table:provider_reservation_scope_items",
        "table:provider_reservations",
        "table:provider_responses",
        "table:provider_response_finalizations",
        "table:provider_semantic_admissions",
        "table:provider_semantic_proposals",
        "table:provider_semantic_validations",
        "table:candidate_investigation_outcomes",
        "table:provider_settlement_adjustments",
        "table:provider_settlements",
        "table:provider_transport_events",
        "table:provider_usage_entries",
        "view:provider_settlement_vector_partitions",
        "trigger:analysis_candidates_append_only_delete",
        "trigger:analysis_candidates_append_only_update",
        "trigger:analysis_coverage_append_only_delete",
        "trigger:analysis_coverage_append_only_update",
        "trigger:analysis_coverage_failure_links_append_only_delete",
        "trigger:analysis_coverage_failure_links_append_only_update",
        "trigger:analysis_coverage_gap_links_append_only_delete",
        "trigger:analysis_coverage_gap_links_append_only_update",
        "trigger:analysis_coverage_taxonomy_links_append_only_delete",
        "trigger:analysis_coverage_taxonomy_links_append_only_update",
        "trigger:analysis_dependency_edges_append_only_delete",
        "trigger:analysis_dependency_edges_append_only_update",
        "trigger:analysis_gaps_append_only_delete",
        "trigger:analysis_gaps_append_only_update",
        "trigger:analysis_hypotheses_append_only_delete",
        "trigger:analysis_hypotheses_append_only_update",
        "trigger:analysis_recommendations_append_only_delete",
        "trigger:analysis_recommendations_append_only_update",
        "trigger:analysis_replay_manifests_append_only_delete",
        "trigger:analysis_replay_manifests_append_only_update",
        "trigger:analysis_run_outputs_append_only_delete",
        "trigger:analysis_run_outputs_append_only_update",
        "trigger:candidate_decisions_append_only_delete",
        "trigger:candidate_decisions_append_only_update",
        "trigger:case_memberships_append_only_delete",
        "trigger:case_memberships_append_only_update",
        "trigger:case_hypothesis_memberships_append_only_delete",
        "trigger:case_hypothesis_memberships_append_only_update",
        "trigger:case_occurrence_details_append_only_delete",
        "trigger:case_occurrence_details_append_only_update",
        "trigger:documentation_passages_append_only_delete",
        "trigger:documentation_passages_append_only_update",
        "trigger:documentation_imports_append_only_delete",
        "trigger:documentation_imports_append_only_update",
        "trigger:documentation_revisions_append_only_delete",
        "trigger:documentation_revisions_append_only_update",
        "trigger:documentation_application_bindings_append_only_delete",
        "trigger:documentation_application_bindings_append_only_update",
        "trigger:documentation_deletion_receipts_append_only_delete",
        "trigger:documentation_deletion_receipts_append_only_update",
        "trigger:documentation_purpose_assignment_details_append_only_delete",
        "trigger:documentation_purpose_assignment_details_append_only_update",
        "trigger:documentation_gap_details_append_only_delete",
        "trigger:documentation_gap_details_append_only_update",
        "trigger:payload_backup_pins_append_only_delete",
        "trigger:payload_backup_pins_append_only_update",
        "trigger:effect_receipts_append_only_delete",
        "trigger:effect_receipts_append_only_update",
        "trigger:evidence_application_links_append_only_delete",
        "trigger:evidence_application_links_append_only_update",
        "trigger:evidence_revisions_append_only_delete",
        "trigger:evidence_revisions_append_only_update",
        "trigger:finding_occurrence_details_append_only_delete",
        "trigger:finding_occurrence_details_append_only_update",
        "trigger:finding_case_abstentions_append_only_delete",
        "trigger:finding_case_abstentions_append_only_update",
        "trigger:finding_case_case_details_append_only_delete",
        "trigger:finding_case_case_details_append_only_update",
        "trigger:finding_case_finding_details_append_only_delete",
        "trigger:finding_case_finding_details_append_only_update",
        "trigger:finding_case_gap_details_append_only_delete",
        "trigger:finding_case_gap_details_append_only_update",
        "trigger:finding_case_publications_append_only_delete",
        "trigger:finding_case_publications_append_only_update",
        "trigger:finding_case_recommendations_append_only_delete",
        "trigger:finding_case_recommendations_append_only_update",
        "trigger:finding_case_taxonomy_assignments_append_only_delete",
        "trigger:finding_case_taxonomy_assignments_append_only_update",
        "trigger:finding_promotion_assessments_append_only_delete",
        "trigger:finding_promotion_assessments_append_only_update",
        "trigger:lineage_details_append_only_delete",
        "trigger:lineage_details_append_only_update",
        "trigger:lineage_event_edges_append_only_delete",
        "trigger:lineage_event_edges_append_only_update",
        "trigger:reconciliation_details_append_only_delete",
        "trigger:reconciliation_details_append_only_update",
        "trigger:reconciliation_metadata_append_only_delete",
        "trigger:reconciliation_metadata_append_only_update",
        "trigger:reconciliation_proof_links_append_only_delete",
        "trigger:reconciliation_proof_links_append_only_update",
        "trigger:taxonomy_assignments_append_only_delete",
        "trigger:taxonomy_assignments_append_only_update",
        "trigger:taxonomy_projection_edges_append_only_delete",
        "trigger:taxonomy_projection_edges_append_only_update",
        "trigger:lifecycle_events_append_only_delete",
        "trigger:lifecycle_events_append_only_update",
        "trigger:audit_events_append_only_delete",
        "trigger:audit_events_append_only_update",
        "trigger:case_occurrences_append_only_delete",
        "trigger:case_occurrences_append_only_update",
        "trigger:checkpoints_append_only_delete",
        "trigger:checkpoints_append_only_update",
        "trigger:durable_commands_append_only_delete",
        "trigger:durable_commands_append_only_update",
        "trigger:finding_occurrences_append_only_delete",
        "trigger:finding_occurrences_append_only_update",
        "trigger:lineage_append_only_delete",
        "trigger:lineage_append_only_update",
        "trigger:publication_receipts_append_only_delete",
        "trigger:publication_receipts_append_only_update",
        "trigger:reconciliation_append_only_delete",
        "trigger:reconciliation_append_only_update",
        "trigger:runs_immutable_binding",
        "trigger:run_operations_immutable",
        "trigger:snapshot_capture_request_immutable",
        "trigger:snapshot_capture_publications_append_only_delete",
        "trigger:snapshot_capture_publications_append_only_update",
        "trigger:evidence_acquisition_application_links_append_only_delete",
        "trigger:evidence_acquisition_application_links_append_only_update",
        "trigger:evidence_acquisition_attempts_append_only_delete",
        "trigger:evidence_acquisition_attempts_append_only_update",
        "trigger:evidence_acquisition_commands_append_only_delete",
        "trigger:evidence_acquisition_commands_append_only_update",
        "trigger:evidence_acquisition_job_nodes_append_only_delete",
        "trigger:evidence_acquisition_job_nodes_append_only_update",
        "trigger:evidence_acquisition_parent_links_append_only_delete",
        "trigger:evidence_acquisition_parent_links_append_only_update",
        "trigger:evidence_acquisition_runs_append_only_delete",
        "trigger:evidence_acquisition_runs_append_only_update",
        "trigger:evidence_acquisition_application_admitted_artifact_guard",
        "trigger:provider_command_binding_owner_guard",
        "trigger:authorization_owner_job_guard",
        "trigger:provider_authority_release_required",
        "trigger:provider_block_eligibility_guard",
        "trigger:provider_block_owner_job_guard",
        "trigger:provider_budget_projection_monotonic_update_guard",
        "trigger:provider_credential_intent_time_order_guard",
        "trigger:provider_credential_intent_event_chain_guard",
        "trigger:provider_credential_terminal_requires_pending_root",
        "trigger:provider_credential_terminal_root_consume",
        "trigger:provider_dispatch_deadline_guard",
        "trigger:provider_profile_projection_exact_root_insert_guard",
        "trigger:provider_profile_projection_monotonic_update_guard",
        "trigger:provider_operation_projection_monotonic_update_guard",
        "trigger:provider_profile_transition_order_guard",
        "trigger:provider_cancelled_response_operation_root_guard",
        "trigger:provider_cancelled_response_blocks_authorization_guard",
        "trigger:provider_cancelled_response_blocks_attempt_guard",
        "trigger:provider_cancelled_response_blocks_request_guard",
        "trigger:provider_cancelled_response_blocks_reservation_guard",
        "trigger:provider_cancelled_response_blocks_fence_guard",
        "trigger:provider_cancelled_response_blocks_transport_guard",
        "trigger:provider_request_authorization_ceiling_guard",
        "trigger:provider_reservation_authorization_vector_guard",
        "trigger:provider_reservation_scope_vector_guard",
        "trigger:provider_response_transport_binding_guard",
        "trigger:provider_response_finalization_totality_guard",
        "trigger:provider_rate_limit_fact_totality_guard",
        "trigger:provider_settlement_usage_classification_guard",
        "trigger:provider_settlement_reservation_amount_guard",
        "trigger:provider_replay_configuration_guard",
        "trigger:provider_run_output_configuration_guard",
        "trigger:provider_semantic_admission_application_guard",
        "trigger:provider_semantic_admission_chronology_guard",
        "trigger:provider_semantic_proposal_chronology_guard",
        "trigger:provider_semantic_validation_chronology_guard",
        "trigger:provider_semantic_proposal_root_guard",
        "trigger:candidate_investigation_outcome_candidate_guard",
        "trigger:candidate_investigation_outcome_response_guard",
        "trigger:provider_transport_event_order_guard",
        "trigger:provider_delete_pending_never_reactivates_guard",
        "trigger:provider_access_profiles_append_only_delete",
        "trigger:provider_access_profiles_append_only_update",
        "trigger:provider_capability_snapshots_append_only_delete",
        "trigger:provider_capability_snapshots_append_only_update",
        "trigger:provider_command_bindings_append_only_delete",
        "trigger:provider_command_bindings_append_only_update",
        "trigger:provider_credential_intents_append_only_delete",
        "trigger:provider_credential_intents_append_only_update",
        "trigger:provider_credential_intent_events_append_only_delete",
        "trigger:provider_credential_intent_events_append_only_update",
        "trigger:provider_credential_terminal_root_consumptions_append_only_delete",
        "trigger:provider_credential_terminal_root_consumptions_append_only_update",
        "trigger:provider_dispatch_fences_append_only_delete",
        "trigger:provider_dispatch_fences_append_only_update",
        "trigger:provider_generations_append_only_delete",
        "trigger:provider_generations_append_only_update",
        "trigger:provider_effective_scan_configurations_v2_append_only_delete",
        "trigger:provider_effective_scan_configurations_v2_append_only_update",
        "trigger:provider_operation_attempts_append_only_delete",
        "trigger:provider_operation_attempts_append_only_update",
        "trigger:provider_operation_authorizations_append_only_delete",
        "trigger:provider_operation_authorizations_append_only_update",
        "trigger:provider_operation_blocks_append_only_delete",
        "trigger:provider_operation_blocks_append_only_update",
        "trigger:provider_price_rules_append_only_delete",
        "trigger:provider_price_rules_append_only_update",
        "trigger:provider_price_snapshots_append_only_delete",
        "trigger:provider_price_snapshots_append_only_update",
        "trigger:provider_rate_limit_facts_append_only_delete",
        "trigger:provider_rate_limit_facts_append_only_update",
        "trigger:provider_replay_edges_append_only_delete",
        "trigger:provider_replay_edges_append_only_update",
        "trigger:provider_run_output_v2_bindings_append_only_delete",
        "trigger:provider_run_output_v2_bindings_append_only_update",
        "trigger:provider_requests_append_only_delete",
        "trigger:provider_requests_append_only_update",
        "trigger:provider_reservation_scope_items_append_only_delete",
        "trigger:provider_reservation_scope_items_append_only_update",
        "trigger:provider_reservations_append_only_delete",
        "trigger:provider_reservations_append_only_update",
        "trigger:provider_responses_append_only_delete",
        "trigger:provider_responses_append_only_update",
        "trigger:provider_response_finalizations_append_only_delete",
        "trigger:provider_response_finalizations_append_only_update",
        "trigger:provider_semantic_admissions_append_only_delete",
        "trigger:provider_semantic_admissions_append_only_update",
        "trigger:provider_semantic_proposals_append_only_delete",
        "trigger:provider_semantic_proposals_append_only_update",
        "trigger:provider_semantic_validations_append_only_delete",
        "trigger:provider_semantic_validations_append_only_update",
        "trigger:candidate_investigation_outcomes_append_only_delete",
        "trigger:candidate_investigation_outcomes_append_only_update",
        "trigger:provider_settlement_adjustments_append_only_delete",
        "trigger:provider_settlement_adjustments_append_only_update",
        "trigger:provider_settlements_append_only_delete",
        "trigger:provider_settlements_append_only_update",
        "trigger:provider_transport_events_append_only_delete",
        "trigger:provider_transport_events_append_only_update",
        "trigger:provider_usage_entries_append_only_delete",
        "trigger:provider_usage_entries_append_only_update",
        "trigger:provider_budget_limits_append_only_delete",
        "trigger:provider_budget_limits_append_only_update",
        "trigger:provider_budget_events_append_only_delete",
        "trigger:provider_budget_events_append_only_update",
        "trigger:provider_usage_rollup_references_append_only_delete",
        "trigger:provider_usage_rollup_references_append_only_update",
        "trigger:provider_budget_settlement_receipts_append_only_delete",
        "trigger:provider_budget_settlement_receipts_append_only_update",
    ];

    private static readonly string[] SchemaV4AppendOnlyTables =
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
        "payload_backup_pins",
        "effect_receipts",
        "evidence_application_links",
        "evidence_revisions",
        "finding_occurrence_details",
        "lineage_details",
        "reconciliation_details",
        "taxonomy_assignments",
    ];

    private static readonly string[] SchemaV5AppendOnlyTables =
    [
        "analysis_coverage_failure_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_taxonomy_links",
        "case_hypothesis_memberships",
        "finding_case_abstentions",
        "finding_case_case_details",
        "finding_case_finding_details",
        "finding_case_gap_details",
        "finding_case_publications",
        "finding_case_recommendations",
        "finding_case_taxonomy_assignments",
        "finding_promotion_assessments",
        "lineage_event_edges",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "taxonomy_projection_edges",
    ];

    private static readonly string[] SchemaV6AppendOnlyTables =
    [
        "evidence_acquisition_application_links",
        "evidence_acquisition_attempts",
        "evidence_acquisition_commands",
        "evidence_acquisition_job_nodes",
        "evidence_acquisition_parent_links",
        "evidence_acquisition_runs",
        "provider_access_profiles",
        "provider_capability_snapshots",
        "provider_command_bindings",
        "provider_credential_intents",
        "provider_credential_intent_events",
        "provider_credential_terminal_root_consumptions",
        "provider_dispatch_fences",
        "provider_generations",
        "provider_effective_scan_configurations_v2",
        "provider_operation_attempts",
        "provider_operation_authorizations",
        "provider_operation_blocks",
        "provider_price_snapshots",
        "provider_price_rules",
        "provider_rate_limit_facts",
        "provider_replay_edges",
        "provider_run_output_v2_bindings",
        "provider_requests",
        "provider_reservation_scope_items",
        "provider_reservations",
        "provider_responses",
        "provider_response_finalizations",
        "provider_semantic_admissions",
        "candidate_investigation_outcomes",
        "provider_semantic_proposals",
        "provider_semantic_validations",
        "provider_settlement_adjustments",
        "provider_settlements",
        "provider_transport_events",
        "provider_usage_entries",
    ];

    private const string SchemaV5 =
        """
        ALTER TABLE lineage_events ADD COLUMN predecessor_occurrence_id TEXT;
        ALTER TABLE lineage_events ADD COLUMN successor_occurrence_id TEXT;
        ALTER TABLE finding_occurrences ADD COLUMN analyzer_version TEXT NOT NULL DEFAULT 'legacy-unspecified';
        DROP INDEX idx_findings_signature;
        CREATE INDEX idx_findings_signature ON finding_occurrences(
            analyzer_family, analyzer_version, identity_contract_version, canonical_signature);
        ALTER TABLE analysis_coverage ADD COLUMN analyzer_id TEXT NOT NULL DEFAULT 'legacy-unspecified';
        ALTER TABLE analysis_coverage ADD COLUMN denominator_label TEXT NOT NULL DEFAULT 'legacy coverage';
        ALTER TABLE analysis_coverage ADD COLUMN exclusions_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(exclusions_json));
        ALTER TABLE analysis_coverage ADD COLUMN member_results_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(member_results_json));
        CREATE TABLE finding_case_publications(
            finding_case_payload_id TEXT PRIMARY KEY REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            input_id TEXT NOT NULL,
            promotion_policy_id TEXT NOT NULL,
            promotion_policy_version TEXT NOT NULL,
            reconciliation_policy_id TEXT NOT NULL,
            reconciliation_policy_version TEXT NOT NULL,
            boundaries_json TEXT NOT NULL CHECK(json_valid(boundaries_json)),
            publication_claim_boundary TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_promotion_assessments(
            promotion_assessment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            state_present INTEGER NOT NULL CHECK(state_present IN (0,1)),
            confidence_at_least_plausible INTEGER NOT NULL CHECK(confidence_at_least_plausible IN (0,1)),
            has_supporting_evidence INTEGER NOT NULL CHECK(has_supporting_evidence IN (0,1)),
            has_no_defeating_contradictions INTEGER NOT NULL CHECK(has_no_defeating_contradictions IN (0,1)),
            has_no_missing_information INTEGER NOT NULL CHECK(has_no_missing_information IN (0,1)),
            severity_closed INTEGER NOT NULL CHECK(severity_closed IN (0,1)),
            identity_closed INTEGER NOT NULL CHECK(identity_closed IN (0,1)),
            conclusion_available INTEGER NOT NULL CHECK(conclusion_available IN (0,1)),
            lead_eligible_state INTEGER NOT NULL CHECK(lead_eligible_state IN (0,1)),
            promotion_outcome TEXT NOT NULL CHECK(promotion_outcome IN ('supported-finding','lead-only','abstained')),
            reasons_json TEXT NOT NULL CHECK(json_valid(reasons_json)),
            assessment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_abstentions(
            abstention_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            required_information_json TEXT NOT NULL CHECK(json_valid(required_information_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            abstention_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_finding_details(
            finding_occurrence_id TEXT PRIMARY KEY REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            conclusion TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            case_identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            taxonomy_assignment_ids_json TEXT NOT NULL CHECK(json_valid(taxonomy_assignment_ids_json)),
            semantic_fingerprint TEXT NOT NULL,
            supersedes_occurrence_id TEXT,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_recommendations(
            recommendation_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            finding_occurrence_id TEXT REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            abstention_id TEXT REFERENCES finding_case_abstentions(abstention_id) ON DELETE RESTRICT,
            lead_hypothesis_id TEXT REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            recommendation_kind TEXT NOT NULL CHECK(recommendation_kind IN (
                'remediation','alternative-remediation','validation','further-investigation','abstention')),
            action TEXT NOT NULL,
            uncertainty TEXT NOT NULL,
            reversibility TEXT NOT NULL,
            verification TEXT NOT NULL,
            risks_json TEXT NOT NULL CHECK(json_valid(risks_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            recommendation_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((finding_occurrence_id IS NOT NULL) + (abstention_id IS NOT NULL) + (lead_hypothesis_id IS NOT NULL) = 1)
        ) STRICT;
        CREATE TABLE case_hypothesis_memberships(
            case_hypothesis_membership_id TEXT PRIMARY KEY,
            case_occurrence_id TEXT NOT NULL REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            membership_role TEXT NOT NULL CHECK(membership_role IN ('cause','lead')),
            cause_proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(case_occurrence_id,hypothesis_id)
        ) STRICT;
        CREATE TABLE finding_case_case_details(
            case_occurrence_id TEXT PRIMARY KEY REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            shared_cause TEXT NOT NULL,
            cause_proof_evidence_ids_json TEXT NOT NULL CHECK(json_valid(cause_proof_evidence_ids_json)),
            semantic_fingerprint TEXT NOT NULL,
            supersedes_occurrence_id TEXT,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_taxonomy_assignments(
            taxonomy_assignment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('hypothesis','finding-occurrence','case-occurrence')),
            subject_id TEXT NOT NULL,
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            axis TEXT NOT NULL,
            facet TEXT NOT NULL,
            taxonomy_code TEXT,
            applicability_state TEXT NOT NULL CHECK(applicability_state IN ('assigned','unknown','unsupported','unmapped','not-applicable')),
            classification_role TEXT CHECK(classification_role IN ('declared','observed','predicted','established')),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            applicability_condition_ids_json TEXT NOT NULL CHECK(json_valid(applicability_condition_ids_json)),
            confidence_assessment_id TEXT,
            analyzer_or_adjudicator_id TEXT NOT NULL,
            reason TEXT NOT NULL,
            supersedes_assignment_ids_json TEXT NOT NULL CHECK(json_valid(supersedes_assignment_ids_json)),
            assignment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((applicability_state = 'assigned') = (taxonomy_code IS NOT NULL)),
            CHECK(applicability_state <> 'assigned' OR classification_role IS NOT NULL)
        ) STRICT;
        CREATE TABLE taxonomy_projection_edges(
            taxonomy_projection_id TEXT PRIMARY KEY,
            source_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            projected_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            mapping_authority_id TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            reason TEXT NOT NULL,
            projection_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_gap_details(
            gap_id TEXT PRIMARY KEY REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            missing_capability_or_information TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_coverage_taxonomy_links(
            coverage_taxonomy_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            taxonomy_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,taxonomy_assignment_id)
        ) STRICT;
        CREATE TABLE analysis_coverage_gap_links(
            coverage_gap_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            gap_id TEXT NOT NULL REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,gap_id)
        ) STRICT;
        CREATE TABLE analysis_coverage_failure_links(
            coverage_failure_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            failure_id TEXT NOT NULL,
            failure_code TEXT NOT NULL,
            message TEXT NOT NULL,
            retryable INTEGER NOT NULL CHECK(retryable IN (0,1)),
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,failure_id)
        ) STRICT;
        CREATE TABLE reconciliation_metadata(
            reconciliation_assessment_id TEXT PRIMARY KEY REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            actor_id TEXT NOT NULL,
            policy_id TEXT NOT NULL,
            policy_version TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            visible_by_default INTEGER NOT NULL CHECK(visible_by_default IN (0,1)),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE reconciliation_proof_links(
            reconciliation_proof_link_id TEXT PRIMARY KEY,
            reconciliation_assessment_id TEXT NOT NULL REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            evidence_id TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(reconciliation_assessment_id,evidence_id)
        ) STRICT;
        CREATE TABLE lineage_event_edges(
            lineage_event_edge_id TEXT PRIMARY KEY,
            lineage_event_id TEXT NOT NULL REFERENCES lineage_events(lineage_event_id) ON DELETE RESTRICT,
            edge_side TEXT NOT NULL CHECK(edge_side IN ('predecessor','successor')),
            occurrence_id TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(lineage_event_id,edge_side,occurrence_id)
        ) STRICT;
        """;

    private const string SchemaV4 =
        """
        CREATE TABLE payload_backup_pins(
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            backup_identity TEXT NOT NULL,
            content_sha256 TEXT NOT NULL CHECK(
                length(content_sha256) = 64
                AND content_sha256 NOT GLOB '*[^0-9a-f]*'),
            created_at TEXT NOT NULL,
            PRIMARY KEY(payload_id, backup_identity)
        ) STRICT;
        CREATE TABLE documentation_revisions(
            documentation_revision_id TEXT PRIMARY KEY,
            source_id TEXT NOT NULL,
            source_kind TEXT NOT NULL CHECK(source_kind IN (
                'project-authored-local','fixture')),
            source_revision TEXT NOT NULL,
            supplying_snapshot_id TEXT,
            body_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            content_sha256 TEXT NOT NULL CHECK(
                length(content_sha256) = 64
                AND content_sha256 NOT GLOB '*[^0-9a-f]*'),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            availability_state TEXT NOT NULL CHECK(availability_state IN (
                'present','partial','unavailable')),
            retention_state TEXT NOT NULL CHECK(retention_state IN (
                'present','partial','unavailable')),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
                'complete-clean','partial','audit-only','unavailable','failed-identity-drift')),
            created_at TEXT NOT NULL,
            UNIQUE(source_id, source_revision, content_sha256),
            CHECK((availability_state = 'present') = (body_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_imports(
            documentation_import_id TEXT PRIMARY KEY,
            import_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            import_mode TEXT NOT NULL CHECK(import_mode IN (
                'clean-import','retained-reuse')),
            reused_import_id TEXT REFERENCES documentation_imports(documentation_import_id) ON DELETE RESTRICT,
            dependency_closure_id TEXT NOT NULL,
            extractor_id TEXT NOT NULL,
            llm_involvement TEXT NOT NULL CHECK(llm_involvement = 'none'),
            llm_operation TEXT NOT NULL CHECK(llm_operation = 'none'),
            boundaries_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            import_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(import_run_id, documentation_revision_id, import_mode),
            CHECK((import_mode = 'retained-reuse') = (reused_import_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_passages(
            documentation_passage_id TEXT PRIMARY KEY,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            utf8_byte_start INTEGER NOT NULL CHECK(utf8_byte_start >= 0),
            utf8_byte_end INTEGER NOT NULL CHECK(utf8_byte_end > utf8_byte_start),
            passage_sha256 TEXT NOT NULL CHECK(
                length(passage_sha256) = 64
                AND passage_sha256 NOT GLOB '*[^0-9a-f]*'),
            passage_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            availability_state TEXT NOT NULL CHECK(availability_state IN (
                'present','partial','unavailable')),
            created_at TEXT NOT NULL,
            UNIQUE(documentation_revision_id, utf8_byte_start, utf8_byte_end),
            CHECK((availability_state = 'present') = (passage_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE evidence_revisions(
            evidence_revision_id TEXT PRIMARY KEY,
            documentation_passage_id TEXT
                REFERENCES documentation_passages(documentation_passage_id) ON DELETE RESTRICT,
            import_id TEXT NOT NULL
                REFERENCES documentation_imports(documentation_import_id) ON DELETE RESTRICT,
            payload_schema_id TEXT NOT NULL,
            payload_schema_version TEXT NOT NULL,
            evidence_kind TEXT NOT NULL CHECK(evidence_kind IN (
                'local-observation','deterministic-derived','documentation-claim')),
            claim_kind TEXT CHECK(claim_kind IN (
                'declared-purpose','requirement','incompatibility','installation-instruction',
                'priority-instruction','lifecycle-instruction','configuration-instruction',
                'patch-instruction','known-issue')),
            authority_kind TEXT NOT NULL CHECK(authority_kind IN (
                'snapshot-bound-local','deterministic-derived','authoritative-external',
                'corroborated-community','uncorroborated-report','user-statement',
                'test-result','heuristic-or-llm-inference')),
            applicability_state TEXT NOT NULL CHECK(applicability_state IN (
                'applicable','not-applicable','unknown','unsupported','contradicted')),
            classification_role TEXT CHECK(classification_role IN (
                'declared','observed','predicted','established')),
            evidence_state TEXT NOT NULL CHECK(evidence_state IN (
                'admitted','invalid-input','unsupported','unavailable','deleted')),
            evidence_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            contradiction_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK(
                (evidence_kind = 'documentation-claim')
                = (documentation_passage_id IS NOT NULL
                   AND claim_kind IS NOT NULL
                   AND classification_role IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_application_bindings(
            documentation_application_binding_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            subject_type TEXT NOT NULL CHECK(subject_type = 'installed-entity'),
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, analysis_context_id, subject_type, subject_id, dependency_closure_id)
        ) STRICT;
        CREATE TABLE evidence_application_links(
            evidence_application_link_id TEXT PRIMARY KEY,
            evidence_revision_id TEXT NOT NULL
                REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            application_binding_id TEXT NOT NULL
                REFERENCES documentation_application_bindings(documentation_application_binding_id) ON DELETE RESTRICT,
            analysis_context_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            subject_type TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            application_state TEXT NOT NULL CHECK(application_state IN (
                'applicable','not-applicable','unknown','unsupported','contradicted')),
            application_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(evidence_revision_id, run_id, analysis_context_id, subject_type, subject_id)
        ) STRICT;
        CREATE TABLE documentation_deletion_receipts(
            documentation_deletion_receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            deleted_body_sha256 TEXT NOT NULL CHECK(
                length(deleted_body_sha256) = 64
                AND deleted_body_sha256 NOT GLOB '*[^0-9a-f]*'),
            deleted_passage_ids_json TEXT NOT NULL CHECK(json_valid(deleted_passage_ids_json)),
            independently_retained_payload_ids_json TEXT NOT NULL
                CHECK(json_valid(independently_retained_payload_ids_json)),
            replay_effect TEXT NOT NULL CHECK(replay_effect IN ('audit-only','unavailable')),
            receipt_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            deleted_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE candidate_decisions(
            candidate_decision_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            relationship_id TEXT NOT NULL,
            disposition TEXT NOT NULL CHECK(disposition IN (
                'candidate-admitted','resolved-negative','unsupported','ambiguous',
                'invalid-input','limited','deferred','unprocessed','failed')),
            lane TEXT NOT NULL CHECK(lane IN (
                'deterministic-required','mandatory-evidence','optional-ranked')),
            rule_version TEXT NOT NULL,
            decision_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_candidates(
            candidate_id TEXT PRIMARY KEY,
            candidate_decision_id TEXT NOT NULL UNIQUE
                REFERENCES candidate_decisions(candidate_decision_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            lane TEXT NOT NULL CHECK(lane IN (
                'deterministic-required','mandatory-evidence','optional-ranked')),
            candidate_state TEXT NOT NULL CHECK(candidate_state IN (
                'present','ambiguous','abstained')),
            dependency_closure_id TEXT NOT NULL,
            candidate_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_hypotheses(
            hypothesis_id TEXT PRIMARY KEY,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_state TEXT NOT NULL CHECK(hypothesis_state IN (
                'present','ambiguous','partial')),
            confidence TEXT NOT NULL CHECK(confidence IN (
                'speculative-lead','plausible','strongly-supported','confirmed')),
            threshold_id TEXT NOT NULL,
            hypothesis_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_recommendations(
            recommendation_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            finding_occurrence_id TEXT
                REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            abstention_id TEXT,
            recommendation_kind TEXT NOT NULL CHECK(recommendation_kind IN (
                'remediation','alternative-remediation','validation',
                'further-investigation','abstention')),
            recommendation_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((finding_occurrence_id IS NOT NULL) <> (abstention_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE taxonomy_assignments(
            taxonomy_assignment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN (
                'documentation-revision','evidence-revision','installed-entity','candidate','hypothesis',
                'finding-occurrence','case-occurrence')),
            subject_id TEXT NOT NULL,
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            axis TEXT NOT NULL,
            facet TEXT NOT NULL,
            taxonomy_code TEXT,
            applicability_state TEXT NOT NULL CHECK(applicability_state IN (
                'assigned','unknown','unsupported','unmapped','not-applicable')),
            classification_role TEXT NOT NULL CHECK(classification_role IN (
                'declared','observed','predicted','established')),
            assignment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((applicability_state = 'assigned') = (taxonomy_code IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_purpose_assignment_details(
            taxonomy_assignment_id TEXT PRIMARY KEY
                REFERENCES taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            evidence_revision_id TEXT NOT NULL
                REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            evidence_application_link_id TEXT NOT NULL
                REFERENCES evidence_application_links(evidence_application_link_id) ON DELETE RESTRICT,
            analyzer_or_adjudicator_id TEXT NOT NULL,
            applicability_condition_ids_json TEXT NOT NULL
                CHECK(json_valid(applicability_condition_ids_json)),
            reason TEXT NOT NULL,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE analysis_coverage(
            coverage_result_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            coverage_state TEXT NOT NULL CHECK(coverage_state IN (
                'completed','completed-with-gaps','failed','skipped-by-configuration',
                'skipped-by-limit','unsupported')),
            denominator INTEGER NOT NULL CHECK(denominator >= 0),
            completed INTEGER NOT NULL CHECK(completed >= 0 AND completed <= denominator),
            excluded INTEGER NOT NULL CHECK(excluded >= 0),
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            coverage_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, population_id)
        ) STRICT;
        CREATE TABLE analysis_gaps(
            gap_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            stage_id TEXT NOT NULL,
            gap_state TEXT NOT NULL CHECK(gap_state IN (
                'missing-information','missing-capability','missing-dependency','unsupported',
                'failed','limited','unavailable','deleted','audit-gap')),
            replay_effect TEXT NOT NULL CHECK(replay_effect IN (
                'none','partial','audit-only','unavailable')),
            conclusion_effect TEXT NOT NULL CHECK(conclusion_effect IN (
                'none','bounded','abstain','unavailable')),
            gap_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE documentation_gap_details(
            gap_id TEXT PRIMARY KEY REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            evidence_revision_id TEXT REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            evidence_application_link_id TEXT
                REFERENCES evidence_application_links(evidence_application_link_id) ON DELETE RESTRICT,
            gap_kind TEXT NOT NULL CHECK(gap_kind IN (
                'contradiction','deletion','unavailable-source','replay')),
            reason TEXT NOT NULL,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE analysis_dependency_edges(
            dependency_edge_id TEXT NOT NULL,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            from_kind TEXT NOT NULL CHECK(from_kind IN (
                'documentation-import','documentation-revision','passage','evidence-revision',
                'claim-application','taxonomy-assignment','documentation-gap','installed-entity','candidate-analysis-root','candidate-decision','dependency-closure',
                'candidate','hypothesis','abstention','failure','finding-occurrence','recommendation','case-occurrence',
                'coverage','gap','replay-manifest','run-output')),
            from_id TEXT NOT NULL,
            to_kind TEXT NOT NULL CHECK(to_kind IN (
                'snapshot','analysis-context','scan-configuration','resolved-input-manifest',
                'documentation-import','documentation-revision','passage','evidence-revision','claim-application',
                'installed-entity','documentation-evidence','evidence','dependency-closure','dependency','candidate-decision',
                'candidate','hypothesis','finding-occurrence','recommendation','case-occurrence',
                'coverage','gap','source-fact','payload','execution-input-binding','policy-binding','threshold-binding','limit-binding','analyzer-declaration-binding')),
            to_id TEXT NOT NULL,
            edge_kind TEXT NOT NULL CHECK(edge_kind IN (
                'derived-from','supports','supported-by','contradicts','applies','applies-to',
                'depends-on','consumes','conditioned-by','classifies','limits','reuses',
                'member-of','supersedes','produced-by','uses')),
            edge_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            PRIMARY KEY(dependency_edge_id, run_id),
            UNIQUE(run_id, from_kind, from_id, to_kind, to_id, edge_kind)
        ) STRICT;
        CREATE TABLE case_memberships(
            case_membership_id TEXT PRIMARY KEY,
            case_occurrence_id TEXT NOT NULL
                REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            member_kind TEXT NOT NULL CHECK(member_kind IN (
                'finding-occurrence','candidate')),
            member_id TEXT NOT NULL,
            membership_role TEXT NOT NULL CHECK(membership_role IN (
                'cause','effect','support','contradiction','lead')),
            cause_proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(case_occurrence_id, member_kind, member_id)
        ) STRICT;
        CREATE TABLE analysis_replay_manifests(
            replay_manifest_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            replay_mode TEXT NOT NULL CHECK(replay_mode IN (
                'clean','incremental','retained-downstream-replay')),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
                'complete-clean','partial','audit-only','unavailable','failed-identity-drift')),
            auditability_state TEXT NOT NULL CHECK(auditability_state IN (
                'complete','partial','unavailable')),
            semantic_equivalence INTEGER NOT NULL CHECK(semantic_equivalence IN (0,1)),
            compared_run_id TEXT REFERENCES runs(run_id) ON DELETE RESTRICT,
            manifest_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            manifest_sha256 TEXT NOT NULL CHECK(
                length(manifest_sha256) = 64
                AND manifest_sha256 NOT GLOB '*[^0-9a-f]*'),
            created_at TEXT NOT NULL,
            UNIQUE(run_id, replay_manifest_id)
        ) STRICT;
        CREATE TABLE analysis_run_outputs(
            run_output_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            payload_schema_id TEXT NOT NULL,
            payload_schema_version TEXT NOT NULL,
            revision INTEGER NOT NULL CHECK(revision > 0),
            output_state TEXT NOT NULL CHECK(output_state IN (
                'present','partial','unavailable','failed')),
            output_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            output_sha256 TEXT NOT NULL CHECK(
                length(output_sha256) = 64
                AND output_sha256 NOT GLOB '*[^0-9a-f]*'),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            provenance_id TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            replay_manifest_id TEXT
                REFERENCES analysis_replay_manifests(replay_manifest_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, revision),
            CHECK((output_state = 'present') = (output_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE effect_receipts(
            effect_receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            effect_class TEXT NOT NULL CHECK(effect_class IN (
                'database','payload-store','staging','trace','run-output')),
            effect_state TEXT NOT NULL CHECK(effect_state IN (
                'admitted','reconciled','missing','orphaned','invalid','not-used')),
            object_id TEXT NOT NULL,
            receipt_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_occurrence_details(
            finding_occurrence_id TEXT PRIMARY KEY
                REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            confidence TEXT NOT NULL CHECK(confidence IN (
                'confirmed','strongly-supported','plausible')),
            severity TEXT NOT NULL CHECK(severity IN (
                'advisory','minor','moderate','major','blocker')),
            finding_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE case_occurrence_details(
            case_occurrence_id TEXT PRIMARY KEY
                REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            case_kind TEXT NOT NULL CHECK(case_kind IN ('supported','lead-only')),
            affects_readiness INTEGER NOT NULL CHECK(affects_readiness IN (0,1)),
            case_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((case_kind = 'supported') OR affects_readiness = 0)
        ) STRICT;
        CREATE TABLE reconciliation_details(
            reconciliation_assessment_id TEXT PRIMARY KEY
                REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            mechanism TEXT NOT NULL CHECK(mechanism IN ('automatic','reviewed','not-evaluated')),
            causal_gate TEXT NOT NULL CHECK(causal_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            applicability_gate TEXT NOT NULL CHECK(applicability_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            dependency_gate TEXT NOT NULL CHECK(dependency_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            producer_gate TEXT NOT NULL CHECK(producer_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            gap_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            considered_occurrences_payload_id TEXT NOT NULL
                REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lineage_details(
            lineage_event_id TEXT PRIMARY KEY
                REFERENCES lineage_events(lineage_event_id) ON DELETE RESTRICT,
            lineage_kind TEXT NOT NULL CHECK(lineage_kind IN (
                'supersedes','analytical-revision','related-follow-up','promotes-lead',
                'merge-successor','split-successor','correction-successor')),
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE INDEX idx_documentation_passages_revision
            ON documentation_passages(documentation_revision_id, utf8_byte_start);
        CREATE INDEX idx_documentation_imports_revision
            ON documentation_imports(documentation_revision_id, created_at);
        CREATE INDEX idx_documentation_application_bindings_run
            ON documentation_application_bindings(run_id, subject_type, subject_id);
        CREATE INDEX idx_documentation_deletion_receipts_revision
            ON documentation_deletion_receipts(documentation_revision_id, deleted_at);
        CREATE INDEX idx_evidence_revisions_passage
            ON evidence_revisions(documentation_passage_id, evidence_revision_id);
        CREATE INDEX idx_evidence_applications_run
            ON evidence_application_links(run_id, evidence_revision_id);
        CREATE INDEX idx_candidate_decisions_run_population
            ON candidate_decisions(run_id, population_id, disposition);
        CREATE INDEX idx_candidates_run_lane ON analysis_candidates(run_id, lane, candidate_id);
        CREATE INDEX idx_hypotheses_candidate ON analysis_hypotheses(candidate_id, hypothesis_id);
        CREATE INDEX idx_recommendations_finding
            ON analysis_recommendations(finding_occurrence_id, recommendation_id);
        CREATE INDEX idx_taxonomy_subject
            ON taxonomy_assignments(subject_kind, subject_id, taxonomy_version, axis);
        CREATE INDEX idx_coverage_run_population
            ON analysis_coverage(run_id, population_id, coverage_state);
        CREATE INDEX idx_gaps_run_population ON analysis_gaps(run_id, population_id, gap_state);
        CREATE INDEX idx_dependency_edges_from
            ON analysis_dependency_edges(from_kind, from_id, edge_kind);
        CREATE INDEX idx_dependency_edges_to
            ON analysis_dependency_edges(to_kind, to_id, edge_kind);
        CREATE INDEX idx_case_memberships_member
            ON case_memberships(member_kind, member_id, case_occurrence_id);
        CREATE INDEX idx_replay_manifests_run
            ON analysis_replay_manifests(run_id, replay_state);
        CREATE INDEX idx_run_outputs_run ON analysis_run_outputs(run_id, revision);
        CREATE INDEX idx_effect_receipts_run ON effect_receipts(run_id, effect_class, effect_state);
        """;

    private const string SchemaV3 =
        """
        CREATE TABLE run_operations(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE RESTRICT,
            operation_kind TEXT NOT NULL,
            request_json TEXT NOT NULL,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256) = 64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER run_operations_immutable
        BEFORE UPDATE ON run_operations
        BEGIN SELECT RAISE(ABORT, 'run operations are immutable'); END;
        """;

    private const string SchemaV2 =
        """
        CREATE TABLE snapshot_capture_operations(
            operation_id TEXT PRIMARY KEY,
            durable_command_id TEXT NOT NULL UNIQUE,
            request_json TEXT NOT NULL,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256) = 64),
            initiation_kind TEXT NOT NULL,
            dispatch_deadline TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL
                CHECK(lifecycle_state IN ('Queued','Running','Completed','Failed')),
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            installation_snapshot_id TEXT,
            payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            CHECK (
                (lifecycle_state = 'Completed'
                 AND installation_snapshot_id IS NOT NULL
                 AND payload_id IS NOT NULL)
                OR
                (lifecycle_state <> 'Completed'
                 AND installation_snapshot_id IS NULL
                 AND payload_id IS NULL)
            )
        ) STRICT;
        CREATE TRIGGER snapshot_capture_request_immutable
        BEFORE UPDATE OF durable_command_id, request_json, request_sha256,
                         initiation_kind, dispatch_deadline
        ON snapshot_capture_operations
        BEGIN SELECT RAISE(ABORT, 'snapshot capture requests are immutable'); END;
        CREATE INDEX idx_snapshot_capture_dispatch
            ON snapshot_capture_operations(lifecycle_state, created_at, operation_id);
        CREATE TABLE snapshot_capture_attempts(
            attempt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL
                REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            lease_acquired_at TEXT NOT NULL,
            lease_expires_at TEXT NOT NULL,
            outcome TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(operation_id, attempt_generation),
            UNIQUE(operation_id, attempt_fencing_token),
            CHECK(lease_expires_at > lease_acquired_at)
        ) STRICT;
        CREATE UNIQUE INDEX idx_snapshot_capture_one_live_attempt
            ON snapshot_capture_attempts(operation_id)
            WHERE outcome = 'running';
        CREATE TABLE snapshot_capture_publications(
            receipt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL UNIQUE
                REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL UNIQUE
                REFERENCES snapshot_capture_attempts(attempt_id) ON DELETE RESTRICT,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256) = 64),
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL UNIQUE,
            published_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER snapshot_capture_publications_append_only_update
        BEFORE UPDATE ON snapshot_capture_publications
        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER snapshot_capture_publications_append_only_delete
        BEFORE DELETE ON snapshot_capture_publications
        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        """;

    private const string SchemaV1 =
        """
        CREATE TABLE store_metadata(
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        ) STRICT;
        CREATE TABLE migration_history(
            migration_id TEXT PRIMARY KEY,
            from_version INTEGER NOT NULL,
            to_version INTEGER NOT NULL UNIQUE,
            applied_at TEXT NOT NULL,
            sqlite_source_id TEXT NOT NULL
        ) STRICT;
        CREATE TABLE coordinator_leases(
            coordinator_instance_id TEXT NOT NULL,
            fencing_epoch INTEGER PRIMARY KEY CHECK(fencing_epoch > 0),
            acquired_at TEXT NOT NULL,
            expires_at TEXT NOT NULL,
            CHECK(expires_at > acquired_at)
        ) STRICT;
        CREATE TABLE runs(
            run_id TEXT PRIMARY KEY,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_scan_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER runs_immutable_binding
        BEFORE UPDATE OF installation_snapshot_id, analysis_context_id,
                         effective_scan_configuration_id, resolved_input_manifest_id
        ON runs
        BEGIN SELECT RAISE(ABORT, 'run bindings are immutable'); END;
        CREATE TABLE job_nodes(
            job_node_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            parent_job_node_id TEXT REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            node_kind TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lifecycle_events(
            transition_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            record_kind TEXT NOT NULL CHECK(record_kind IN ('requested','observed')),
            policy_version TEXT NOT NULL,
            from_state TEXT NOT NULL,
            to_state TEXT NOT NULL,
            expected_generation INTEGER NOT NULL CHECK(expected_generation >= 0),
            new_generation INTEGER NOT NULL CHECK(new_generation = expected_generation + 1),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            reason TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            UNIQUE(run_id, durable_sequence)
        ) STRICT;
        CREATE TABLE durable_commands(
            command_id TEXT PRIMARY KEY,
            command_kind TEXT NOT NULL,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            expected_generation INTEGER NOT NULL CHECK(expected_generation >= 0),
            disposition TEXT NOT NULL,
            resulting_state TEXT NOT NULL,
            transition_id TEXT REFERENCES lifecycle_events(transition_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            start_initiation_kind TEXT,
            start_dispatch_deadline TEXT,
            CHECK (
                (command_kind = 'start'
                 AND ((start_initiation_kind IS NULL AND start_dispatch_deadline IS NULL)
                      OR (start_initiation_kind IS NOT NULL
                          AND start_dispatch_deadline IS NOT NULL)))
                OR
                (command_kind <> 'start'
                 AND start_initiation_kind IS NULL
                 AND start_dispatch_deadline IS NULL)
            )
        ) STRICT;
        CREATE TABLE attempts(
            attempt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            lease_acquired_at TEXT NOT NULL,
            lease_expires_at TEXT NOT NULL,
            dispatch_identity TEXT NOT NULL UNIQUE,
            idempotency_identity TEXT NOT NULL UNIQUE,
            retry_safety TEXT NOT NULL,
            outcome TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, attempt_generation),
            UNIQUE(run_id, attempt_fencing_token),
            CHECK(lease_expires_at > lease_acquired_at)
        ) STRICT;
        CREATE TABLE checkpoints(
            checkpoint_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL REFERENCES attempts(attempt_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_scan_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            content_sha256 TEXT NOT NULL CHECK(length(content_sha256) = 64),
            completed_partitions_json TEXT NOT NULL,
            pending_and_gaps_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE payloads(
            payload_id TEXT PRIMARY KEY,
            content_sha256 TEXT NOT NULL UNIQUE CHECK(length(content_sha256) = 64),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            codec TEXT NOT NULL,
            retention_state TEXT NOT NULL,
            object_relative_path TEXT NOT NULL UNIQUE,
            admitted_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE payload_owners(
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            owner_kind TEXT NOT NULL,
            owner_id TEXT NOT NULL,
            PRIMARY KEY(payload_id, owner_kind, owner_id)
        ) STRICT;
        CREATE TABLE publication_receipts(
            receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL UNIQUE REFERENCES attempts(attempt_id) ON DELETE RESTRICT,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256) = 64),
            published_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE publication_receipt_payloads(
            receipt_id TEXT NOT NULL REFERENCES publication_receipts(receipt_id) ON DELETE RESTRICT,
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            PRIMARY KEY(receipt_id, payload_id)
        ) STRICT;
        CREATE TABLE logical_findings(
            logical_finding_id TEXT PRIMARY KEY,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_occurrences(
            finding_occurrence_id TEXT PRIMARY KEY,
            logical_finding_id TEXT NOT NULL REFERENCES logical_findings(logical_finding_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            analyzer_family TEXT NOT NULL,
            semantic_contract_version TEXT NOT NULL,
            identity_contract_version TEXT NOT NULL,
            identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            canonical_signature TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, finding_occurrence_id)
        ) STRICT;
        CREATE TABLE logical_cases(
            logical_case_id TEXT PRIMARY KEY,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE case_occurrences(
            case_occurrence_id TEXT PRIMARY KEY,
            logical_case_id TEXT NOT NULL REFERENCES logical_cases(logical_case_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            shared_cause_signature TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE reconciliation_assessments(
            reconciliation_assessment_id TEXT PRIMARY KEY,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            predecessor_occurrence_id TEXT,
            successor_occurrence_id TEXT,
            causal_gate TEXT NOT NULL,
            applicability_gate TEXT NOT NULL,
            dependency_gate TEXT NOT NULL,
            producer_compatibility_gate TEXT NOT NULL,
            outcome TEXT NOT NULL CHECK(outcome IN (
                'exact-continuation','analytical-revision','related-follow-up','new-distinct',
                'ambiguous','unknown','not-observed','not-evaluated')),
            proof_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            policy_version TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lineage_events(
            lineage_event_id TEXT PRIMARY KEY,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            event_kind TEXT NOT NULL CHECK(event_kind IN (
                'continuation','revision','follow-up','merge','split','supersession',
                'promotion','correction')),
            predecessor_logical_id TEXT,
            successor_logical_id TEXT NOT NULL,
            reconciliation_assessment_id TEXT REFERENCES reconciliation_assessments(reconciliation_assessment_id)
                ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE audit_events(
            audit_event_id TEXT PRIMARY KEY,
            event_kind TEXT NOT NULL,
            object_kind TEXT NOT NULL,
            object_id TEXT NOT NULL,
            detail_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            occurred_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE run_projection(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE CASCADE,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER lifecycle_events_append_only_update
        BEFORE UPDATE ON lifecycle_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lifecycle_events_append_only_delete
        BEFORE DELETE ON lifecycle_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER reconciliation_append_only_update
        BEFORE UPDATE ON reconciliation_assessments BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER reconciliation_append_only_delete
        BEFORE DELETE ON reconciliation_assessments BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lineage_append_only_update
        BEFORE UPDATE ON lineage_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lineage_append_only_delete
        BEFORE DELETE ON lineage_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER audit_events_append_only_update
        BEFORE UPDATE ON audit_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER audit_events_append_only_delete
        BEFORE DELETE ON audit_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER durable_commands_append_only_update
        BEFORE UPDATE ON durable_commands BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER durable_commands_append_only_delete
        BEFORE DELETE ON durable_commands BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER checkpoints_append_only_update
        BEFORE UPDATE ON checkpoints BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER checkpoints_append_only_delete
        BEFORE DELETE ON checkpoints BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER publication_receipts_append_only_update
        BEFORE UPDATE ON publication_receipts BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER publication_receipts_append_only_delete
        BEFORE DELETE ON publication_receipts BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER finding_occurrences_append_only_update
        BEFORE UPDATE ON finding_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER finding_occurrences_append_only_delete
        BEFORE DELETE ON finding_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER case_occurrences_append_only_update
        BEFORE UPDATE ON case_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER case_occurrences_append_only_delete
        BEFORE DELETE ON case_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE INDEX idx_runs_created ON runs(created_at, run_id);
        CREATE INDEX idx_runs_dispatch ON runs(lifecycle_state, created_at, run_id);
        CREATE INDEX idx_events_run_sequence ON lifecycle_events(run_id, durable_sequence);
        CREATE INDEX idx_attempts_run ON attempts(run_id, attempt_generation);
        CREATE UNIQUE INDEX idx_attempts_one_live_per_run
        ON attempts(run_id) WHERE outcome = 'running';
        CREATE INDEX idx_findings_signature ON finding_occurrences(
            analyzer_family, identity_contract_version, canonical_signature);
        CREATE INDEX idx_reconciliation_successor ON reconciliation_assessments(
            subject_kind, successor_occurrence_id);
        CREATE INDEX idx_lineage_successor ON lineage_events(subject_kind, successor_logical_id);
        """;

    private const string Wp2AuthorizationModeExtension =
        """
        ALTER TABLE provider_operation_authorizations ADD COLUMN execution_mode TEXT NOT NULL
          DEFAULT 'simulated-nonnetwork' CHECK(execution_mode IN ('simulated-nonnetwork','provider-live'));
        """;

    private const string R2LiveSemanticSchema6Extension =
        """
        CREATE TABLE source_claim_admitted_artifacts(
          admitted_artifact_id TEXT PRIMARY KEY,
          acquisition_run_id TEXT NOT NULL REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
          proposal_id TEXT NOT NULL REFERENCES provider_semantic_proposals(proposal_id) ON DELETE RESTRICT,
          admission_id TEXT NOT NULL UNIQUE REFERENCES provider_semantic_admissions(admission_id) ON DELETE RESTRICT,
          payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
          source_revision_id TEXT NOT NULL REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
          passage_id TEXT NOT NULL,
          content_sha256 TEXT NOT NULL CHECK(length(content_sha256)=64),
          byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
          start_byte INTEGER NOT NULL CHECK(start_byte >= 0),
          end_byte INTEGER NOT NULL CHECK(end_byte >= start_byte AND end_byte <= byte_length),
          created_at TEXT NOT NULL,
          UNIQUE(acquisition_run_id,proposal_id),
          UNIQUE(payload_id,source_revision_id,passage_id,start_byte,end_byte)
        ) STRICT;
        CREATE TABLE source_claim_applicability_facts(
          applicability_fact_id TEXT PRIMARY KEY,
          acquisition_run_id TEXT NOT NULL REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
          proposal_id TEXT NOT NULL REFERENCES provider_semantic_proposals(proposal_id) ON DELETE RESTRICT,
          source_revision_id TEXT NOT NULL REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
          statement_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
          statement_sha256 TEXT NOT NULL CHECK(length(statement_sha256)=64),
          created_at TEXT NOT NULL,
          UNIQUE(acquisition_run_id,proposal_id,applicability_fact_id)
        ) STRICT;
        CREATE TABLE candidate_evidence_authority(
          outcome_id TEXT NOT NULL REFERENCES candidate_investigation_outcomes(outcome_id) ON DELETE RESTRICT,
          evidence_id TEXT NOT NULL,
          evidence_application_link_id TEXT NOT NULL,
          root_kind TEXT NOT NULL CHECK(root_kind IN ('persisted-source-claim-application','frozen-host-evidence')),
          evidence_root_id TEXT,
          applicability_record_id TEXT,
          source_acquisition_id TEXT,
          source_proposal_id TEXT,
          source_admission_id TEXT,
          admitted_artifact_id TEXT,
          source_application_link_id TEXT,
          source_revision_id TEXT NOT NULL,
          passage_id TEXT NOT NULL,
          content_sha256 TEXT NOT NULL CHECK(length(content_sha256)=64),
          local_observation_id TEXT NOT NULL,
          local_observation_sha256 TEXT NOT NULL CHECK(length(local_observation_sha256)=64),
          input_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
          created_at TEXT NOT NULL,
          PRIMARY KEY(outcome_id,evidence_id),
          UNIQUE(outcome_id,evidence_application_link_id),
          CHECK((root_kind='persisted-source-claim-application'
              AND evidence_root_id IS NULL AND applicability_record_id IS NULL
              AND source_acquisition_id IS NOT NULL AND source_proposal_id IS NOT NULL
              AND source_admission_id IS NOT NULL AND admitted_artifact_id IS NOT NULL
              AND source_application_link_id IS NOT NULL)
            OR (root_kind='frozen-host-evidence'
              AND evidence_root_id IS NOT NULL AND applicability_record_id IS NOT NULL
              AND source_acquisition_id IS NULL AND source_proposal_id IS NULL
              AND source_admission_id IS NULL AND admitted_artifact_id IS NULL
              AND source_application_link_id IS NULL))
        ) STRICT;
        """;

    private const string Wp2Schema6Extension =
        """
        DROP TRIGGER provider_authority_release_required;
        CREATE TRIGGER provider_authority_release_required
        BEFORE INSERT ON provider_operation_authorizations
        WHEN NEW.input_bound_policy_id <> 'openai-responses-o200k-byte-envelope'
          OR NEW.input_bound_policy_version <> 'v2' OR NEW.input_bound_proof_status <> 'proved'
        BEGIN SELECT RAISE(ABORT, 'provider dispatch requires the exact accepted repository-local input-bound proof'); END;

        DROP TRIGGER provider_reservation_scope_items_append_only_update;
        DROP TRIGGER provider_reservation_scope_items_append_only_delete;
        DROP TRIGGER provider_reservation_scope_vector_guard;
        DROP INDEX idx_provider_reservation_scope;
        DROP TABLE provider_reservation_scope_items;
        CREATE TABLE provider_reservation_scope_items(
            reservation_scope_item_id TEXT PRIMARY KEY,
            reservation_id TEXT NOT NULL REFERENCES provider_reservations(reservation_id) ON DELETE RESTRICT,
            scope_kind TEXT NOT NULL CHECK(scope_kind IN ('request','operation','evidence-acquisition-run','analysis-run','provider-profile','provider-account','billing-scope','global')),
            scope_id TEXT NOT NULL,
            usage_json TEXT NOT NULL CHECK(json_valid(usage_json)),
            nano_usd INTEGER NOT NULL CHECK(nano_usd >= 0),
            UNIQUE(reservation_id,scope_kind,scope_id)
        ) STRICT;
        CREATE INDEX idx_provider_reservation_scope ON provider_reservation_scope_items(scope_kind,scope_id);
        CREATE TRIGGER provider_reservation_scope_vector_guard
        BEFORE INSERT ON provider_reservation_scope_items
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_reservations reservation
            WHERE reservation.reservation_id = NEW.reservation_id
              AND reservation.usage_json = NEW.usage_json
              AND reservation.maximum_nano_usd = NEW.nano_usd)
            THEN RAISE(ABORT, 'provider reservation scope must retain the exact operation reservation vector') END;
        END;

        DROP TRIGGER provider_budget_projection_authority_guard;
        DROP TRIGGER provider_budget_projection_monotonic_update_guard;
        DROP TRIGGER provider_budget_projection_updated_at_canonical_utc_update;
        DROP TRIGGER provider_budget_projection_updated_at_canonical_utc_insert;
        DROP TABLE provider_budget_projection;

        CREATE TABLE provider_budget_limits(
            scope_kind TEXT NOT NULL CHECK(scope_kind IN ('request','operation','evidence-acquisition-run','analysis-run','provider-profile','provider-account','billing-scope','global')),
            scope_id TEXT NOT NULL CHECK(length(trim(scope_id)) > 0),
            dispatch_count INTEGER NOT NULL CHECK(dispatch_count >= 0), input_tokens INTEGER NOT NULL CHECK(input_tokens >= 0),
            output_tokens INTEGER NOT NULL CHECK(output_tokens >= 0), total_tokens INTEGER NOT NULL CHECK(total_tokens = input_tokens + output_tokens),
            reasoning_tokens INTEGER NOT NULL CHECK(reasoning_tokens BETWEEN 0 AND output_tokens), cache_read_tokens INTEGER NOT NULL CHECK(cache_read_tokens >= 0),
            cache_write_tokens INTEGER NOT NULL CHECK(cache_write_tokens >= 0), priced_tool_calls INTEGER NOT NULL CHECK(priced_tool_calls >= 0),
            nano_usd INTEGER NOT NULL CHECK(nano_usd >= 0), authority_kind TEXT NOT NULL CHECK(authority_kind = 'local-hard-limit'), created_at TEXT NOT NULL,
            PRIMARY KEY(scope_kind,scope_id)
        ) STRICT;
        CREATE TABLE provider_budget_events(
            budget_event_id TEXT PRIMARY KEY CHECK(length(trim(budget_event_id)) > 0),
            reservation_id TEXT NOT NULL REFERENCES provider_reservations(reservation_id) ON DELETE RESTRICT,
            usage_entry_id TEXT REFERENCES provider_usage_entries(usage_entry_id) ON DELETE RESTRICT,
            scope_kind TEXT NOT NULL, scope_id TEXT NOT NULL,
            event_kind TEXT NOT NULL CHECK(event_kind IN ('reserved','released-undispatched','settled-complete','settled-failed-known','retained-ambiguous','retained-partial','retained-unavailable','settled-overrun','adjustment')),
            dispatch_count INTEGER NOT NULL CHECK(dispatch_count >= 0), input_tokens INTEGER NOT NULL CHECK(input_tokens >= 0),
            output_tokens INTEGER NOT NULL CHECK(output_tokens >= 0), total_tokens INTEGER NOT NULL CHECK(total_tokens = input_tokens + output_tokens),
            reasoning_tokens INTEGER NOT NULL CHECK(reasoning_tokens BETWEEN 0 AND output_tokens), cache_read_tokens INTEGER NOT NULL CHECK(cache_read_tokens >= 0),
            cache_write_tokens INTEGER NOT NULL CHECK(cache_write_tokens >= 0), priced_tool_calls INTEGER NOT NULL CHECK(priced_tool_calls >= 0),
            nano_usd INTEGER NOT NULL CHECK(nano_usd >= 0), sequence INTEGER NOT NULL CHECK(sequence > 0), occurred_at TEXT NOT NULL,
            UNIQUE(reservation_id,scope_kind,scope_id,sequence),
            FOREIGN KEY(reservation_id,scope_kind,scope_id) REFERENCES provider_reservation_scope_items(reservation_id,scope_kind,scope_id) ON DELETE RESTRICT,
            CHECK((event_kind IN ('settled-complete','settled-failed-known','settled-overrun') AND usage_entry_id IS NOT NULL)
              OR (event_kind IN ('reserved','released-undispatched','retained-ambiguous') AND usage_entry_id IS NULL)
              OR event_kind IN ('retained-partial','retained-unavailable','adjustment'))
        ) STRICT;
        CREATE INDEX idx_provider_budget_events_scope ON provider_budget_events(scope_kind,scope_id,sequence);
        CREATE TABLE provider_usage_rollup_references(
            usage_entry_id TEXT NOT NULL REFERENCES provider_usage_entries(usage_entry_id) ON DELETE RESTRICT,
            scope_kind TEXT NOT NULL CHECK(scope_kind IN ('request','operation','evidence-acquisition-run','analysis-run','provider-profile','provider-account','billing-scope','global')),
            scope_id TEXT NOT NULL CHECK(length(trim(scope_id)) > 0),
            attribution_kind TEXT NOT NULL CHECK(attribution_kind IN ('owner','attached-pre-cutoff','non-owning-rollup')),
            dispatch_sequence_cutoff INTEGER CHECK(dispatch_sequence_cutoff > 0), created_at TEXT NOT NULL,
            PRIMARY KEY(usage_entry_id,scope_kind,scope_id),
            CHECK((attribution_kind = 'attached-pre-cutoff') = (dispatch_sequence_cutoff IS NOT NULL))
        ) STRICT;
        CREATE TABLE provider_budget_settlement_receipts(
            settlement_id TEXT PRIMARY KEY CHECK(length(trim(settlement_id)) > 0),
            reservation_id TEXT NOT NULL UNIQUE REFERENCES provider_reservations(reservation_id) ON DELETE RESTRICT,
            event_kind TEXT NOT NULL CHECK(event_kind IN ('released-undispatched','settled-complete','settled-failed-known','retained-ambiguous','retained-partial','retained-unavailable','settled-overrun')),
            usage_entry_id TEXT REFERENCES provider_usage_entries(usage_entry_id) ON DELETE RESTRICT,
            retry_permitted INTEGER NOT NULL CHECK(retry_permitted = 0),
            created_at TEXT NOT NULL,
            CHECK((event_kind IN ('settled-complete','settled-failed-known','settled-overrun') AND usage_entry_id IS NOT NULL)
              OR event_kind IN ('released-undispatched','retained-ambiguous','retained-partial','retained-unavailable'))
        ) STRICT;
        CREATE TABLE provider_budget_projection(
            scope_kind TEXT NOT NULL,
            scope_id TEXT NOT NULL,
            reserved_dispatch_count INTEGER NOT NULL CHECK(reserved_dispatch_count >= 0), reserved_input_tokens INTEGER NOT NULL CHECK(reserved_input_tokens >= 0),
            reserved_output_tokens INTEGER NOT NULL CHECK(reserved_output_tokens >= 0), reserved_total_tokens INTEGER NOT NULL CHECK(reserved_total_tokens = reserved_input_tokens + reserved_output_tokens),
            reserved_reasoning_tokens INTEGER NOT NULL CHECK(reserved_reasoning_tokens BETWEEN 0 AND reserved_output_tokens), reserved_cache_read_tokens INTEGER NOT NULL CHECK(reserved_cache_read_tokens >= 0),
            reserved_cache_write_tokens INTEGER NOT NULL CHECK(reserved_cache_write_tokens >= 0), reserved_priced_tool_calls INTEGER NOT NULL CHECK(reserved_priced_tool_calls >= 0),
            reserved_nano_usd INTEGER NOT NULL CHECK(reserved_nano_usd >= 0),
            settled_dispatch_count INTEGER NOT NULL CHECK(settled_dispatch_count >= 0), settled_input_tokens INTEGER NOT NULL CHECK(settled_input_tokens >= 0),
            settled_output_tokens INTEGER NOT NULL CHECK(settled_output_tokens >= 0), settled_total_tokens INTEGER NOT NULL CHECK(settled_total_tokens = settled_input_tokens + settled_output_tokens),
            settled_reasoning_tokens INTEGER NOT NULL CHECK(settled_reasoning_tokens BETWEEN 0 AND settled_output_tokens), settled_cache_read_tokens INTEGER NOT NULL CHECK(settled_cache_read_tokens >= 0),
            settled_cache_write_tokens INTEGER NOT NULL CHECK(settled_cache_write_tokens >= 0), settled_priced_tool_calls INTEGER NOT NULL CHECK(settled_priced_tool_calls >= 0),
            settled_nano_usd INTEGER NOT NULL CHECK(settled_nano_usd >= 0),
            unresolved_dispatch_count INTEGER NOT NULL CHECK(unresolved_dispatch_count >= 0), unresolved_input_tokens INTEGER NOT NULL CHECK(unresolved_input_tokens >= 0),
            unresolved_output_tokens INTEGER NOT NULL CHECK(unresolved_output_tokens >= 0), unresolved_total_tokens INTEGER NOT NULL CHECK(unresolved_total_tokens = unresolved_input_tokens + unresolved_output_tokens),
            unresolved_reasoning_tokens INTEGER NOT NULL CHECK(unresolved_reasoning_tokens BETWEEN 0 AND unresolved_output_tokens), unresolved_cache_read_tokens INTEGER NOT NULL CHECK(unresolved_cache_read_tokens >= 0),
            unresolved_cache_write_tokens INTEGER NOT NULL CHECK(unresolved_cache_write_tokens >= 0), unresolved_priced_tool_calls INTEGER NOT NULL CHECK(unresolved_priced_tool_calls >= 0),
            unresolved_nano_usd INTEGER NOT NULL CHECK(unresolved_nano_usd >= 0),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL,
            PRIMARY KEY(scope_kind,scope_id),
            FOREIGN KEY(scope_kind,scope_id) REFERENCES provider_budget_limits(scope_kind,scope_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TRIGGER provider_budget_projection_monotonic_update_guard
        BEFORE UPDATE ON provider_budget_projection
        WHEN NEW.scope_kind <> OLD.scope_kind OR NEW.scope_id <> OLD.scope_id
          OR NEW.projection_version <= OLD.projection_version
          OR NEW.updated_at <= OLD.updated_at
        BEGIN SELECT RAISE(ABORT, 'provider budget projection must advance monotonically on one exact root'); END;
        """;

    private const string SchemaV6 =
        """
        CREATE UNIQUE INDEX idx_payload_identity_size ON payloads(payload_id,content_sha256,byte_length);
        CREATE TABLE provider_access_profiles(
            profile_id TEXT PRIMARY KEY CHECK(length(trim(profile_id)) > 0),
            provider TEXT NOT NULL CHECK(provider = 'openai'),
            purpose TEXT NOT NULL CHECK(purpose = 'responses'),
            display_label TEXT NOT NULL CHECK(length(trim(display_label)) > 0),
            account_identity_id TEXT,
            billing_scope_identity_id TEXT,
            created_at TEXT NOT NULL,
            CHECK((account_identity_id IS NULL AND billing_scope_identity_id IS NULL)
              OR (account_identity_id IS NOT NULL AND billing_scope_identity_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE provider_generations(
            generation_id TEXT PRIMARY KEY CHECK(length(trim(generation_id)) > 0),
            profile_id TEXT NOT NULL REFERENCES provider_access_profiles(profile_id) ON DELETE RESTRICT,
            generation_ordinal INTEGER NOT NULL CHECK(generation_ordinal > 0),
            revocation_epoch INTEGER NOT NULL CHECK(revocation_epoch >= 0),
            created_at TEXT NOT NULL,
            UNIQUE(profile_id,generation_ordinal),
            UNIQUE(profile_id,generation_id)
        ) STRICT;
        CREATE TABLE provider_credential_intents(
            intent_id TEXT PRIMARY KEY CHECK(length(trim(intent_id)) > 0),
            profile_id TEXT NOT NULL REFERENCES provider_access_profiles(profile_id) ON DELETE RESTRICT,
            generation_id TEXT NOT NULL,
            intent_kind TEXT NOT NULL CHECK(intent_kind IN ('enroll','replace','verify','disable','delete','recover')),
            intent_state TEXT NOT NULL CHECK(intent_state IN ('pending','completed','failed','cancelled','unavailable')),
            from_lifecycle_state TEXT NOT NULL CHECK(from_lifecycle_state IN (
              'none','pending-enrollment','active-unverified','active-verified','replacing','disabled',
              'delete-pending','deleted','secure-store-unavailable','recovery-required')),
            to_lifecycle_state TEXT NOT NULL CHECK(to_lifecycle_state IN (
              'pending-enrollment','active-unverified','active-verified','replacing','disabled',
              'delete-pending','deleted','secure-store-unavailable','recovery-required')),
            outcome_lifecycle_state TEXT NOT NULL CHECK(outcome_lifecycle_state IN (
              'none','pending-enrollment','active-unverified','active-verified','replacing','disabled',
              'delete-pending','deleted','secure-store-unavailable','recovery-required')),
            verification_state TEXT NOT NULL CHECK(verification_state IN (
              'available','unavailable','unsupported','not-applicable','not-used')),
            account_identity_id TEXT,
            billing_scope_identity_id TEXT,
            capability_snapshot_id TEXT,
            recovery_disposition TEXT NOT NULL CHECK(recovery_disposition IN ('not-required','required','unavailable')),
            cleanup_disposition TEXT NOT NULL CHECK(cleanup_disposition IN ('not-requested','pending','confirmed','failed')),
            created_at TEXT NOT NULL,
            UNIQUE(intent_id,profile_id,generation_id,account_identity_id,billing_scope_identity_id,
              capability_snapshot_id,to_lifecycle_state,verification_state,recovery_disposition,cleanup_disposition),
            FOREIGN KEY(profile_id,generation_id)
              REFERENCES provider_generations(profile_id,generation_id) ON DELETE RESTRICT,
            FOREIGN KEY(capability_snapshot_id)
              REFERENCES provider_capability_snapshots(capability_snapshot_id) ON DELETE RESTRICT,
            CHECK((intent_kind = 'enroll' AND ((from_lifecycle_state = 'none' AND to_lifecycle_state = 'pending-enrollment')
                OR (from_lifecycle_state = 'pending-enrollment' AND to_lifecycle_state IN ('active-unverified','secure-store-unavailable','recovery-required'))))
              OR (intent_kind = 'verify' AND from_lifecycle_state = 'active-unverified'
                AND to_lifecycle_state IN ('active-verified','secure-store-unavailable','recovery-required'))
              OR (intent_kind = 'replace' AND ((from_lifecycle_state IN ('active-unverified','active-verified')
                  AND to_lifecycle_state IN ('replacing','active-unverified','active-verified'))
                OR (from_lifecycle_state = 'replacing' AND to_lifecycle_state IN ('active-unverified','active-verified','secure-store-unavailable','recovery-required'))))
              OR (intent_kind = 'disable' AND from_lifecycle_state IN ('active-unverified','active-verified','replacing') AND to_lifecycle_state = 'disabled')
              OR (intent_kind = 'delete' AND ((from_lifecycle_state IN ('active-unverified','active-verified','disabled','replacing') AND to_lifecycle_state = 'delete-pending')
                OR (from_lifecycle_state = 'delete-pending' AND to_lifecycle_state IN ('delete-pending','deleted'))))
              OR (intent_kind = 'recover' AND ((from_lifecycle_state IN (
                    'pending-enrollment','active-unverified','active-verified','replacing','disabled','secure-store-unavailable','recovery-required')
                  AND to_lifecycle_state = 'recovery-required')
                OR (from_lifecycle_state IN ('secure-store-unavailable','recovery-required')
                  AND to_lifecycle_state IN ('active-unverified','active-verified','disabled','delete-pending','recovery-required','secure-store-unavailable'))
                OR (from_lifecycle_state = 'delete-pending' AND to_lifecycle_state = 'active-unverified')))),
            CHECK(((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) IN ('none','pending-enrollment','deleted')
                AND account_identity_id IS NULL AND billing_scope_identity_id IS NULL AND capability_snapshot_id IS NULL)
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) IN ('active-unverified','active-verified','replacing','disabled','delete-pending')
                AND account_identity_id IS NOT NULL AND billing_scope_identity_id IS NOT NULL AND capability_snapshot_id IS NOT NULL)
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) IN ('secure-store-unavailable','recovery-required')
                AND ((account_identity_id IS NULL AND billing_scope_identity_id IS NULL AND capability_snapshot_id IS NULL)
                  OR (account_identity_id IS NOT NULL AND billing_scope_identity_id IS NOT NULL AND capability_snapshot_id IS NOT NULL)))),
            CHECK(((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) IN ('none','pending-enrollment') AND verification_state = 'not-applicable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'not-requested')
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) = 'active-verified' AND verification_state = 'available'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'not-requested')
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) IN ('active-unverified','replacing','disabled') AND verification_state = 'unavailable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'not-requested')
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) = 'delete-pending' AND verification_state = 'unavailable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition IN ('pending','failed'))
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) = 'deleted' AND verification_state = 'unavailable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'confirmed')
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) = 'secure-store-unavailable' AND verification_state = 'unavailable'
                AND recovery_disposition = 'unavailable' AND cleanup_disposition IN ('not-requested','failed'))
              OR ((CASE WHEN intent_state IN ('pending','completed') THEN to_lifecycle_state ELSE outcome_lifecycle_state END) = 'recovery-required' AND verification_state = 'unavailable'
                AND recovery_disposition = 'required' AND cleanup_disposition IN ('not-requested','failed'))),
            CHECK(intent_kind <> 'delete' OR intent_state <> 'failed'
              OR (from_lifecycle_state = 'delete-pending' AND to_lifecycle_state = 'delete-pending'
                AND cleanup_disposition = 'failed')),
            CHECK((intent_state = 'pending' AND outcome_lifecycle_state = from_lifecycle_state)
              OR (intent_state = 'completed' AND outcome_lifecycle_state = to_lifecycle_state)
              OR (intent_state = 'failed' AND outcome_lifecycle_state = CASE
                    WHEN intent_kind = 'delete' THEN 'delete-pending' ELSE from_lifecycle_state END)
              OR (intent_state = 'unavailable' AND ((intent_kind = 'delete' AND outcome_lifecycle_state = 'delete-pending')
                    OR (intent_kind <> 'delete' AND outcome_lifecycle_state IN ('secure-store-unavailable','recovery-required'))))
              OR (intent_state = 'cancelled' AND outcome_lifecycle_state = from_lifecycle_state)),
            CHECK(intent_state <> 'cancelled' OR (
              intent_kind IN ('enroll','replace','verify','disable','delete','recover')
              AND to_lifecycle_state <> outcome_lifecycle_state))
        ) STRICT;
        CREATE TABLE provider_credential_intent_events(
            intent_event_id TEXT PRIMARY KEY CHECK(length(trim(intent_event_id)) > 0),
            intent_root_id TEXT NOT NULL CHECK(length(trim(intent_root_id)) > 0),
            intent_id TEXT NOT NULL UNIQUE REFERENCES provider_credential_intents(intent_id) ON DELETE RESTRICT,
            event_version INTEGER NOT NULL CHECK(event_version > 0),
            prior_intent_event_id TEXT REFERENCES provider_credential_intent_events(intent_event_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(intent_root_id,event_version),
            CHECK((event_version = 1 AND prior_intent_event_id IS NULL)
              OR (event_version > 1 AND prior_intent_event_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE provider_credential_terminal_root_consumptions(
            pending_intent_id TEXT PRIMARY KEY
              REFERENCES provider_credential_intents(intent_id) ON DELETE RESTRICT,
            terminal_intent_id TEXT NOT NULL UNIQUE
              REFERENCES provider_credential_intents(intent_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER provider_credential_terminal_requires_pending_root
        BEFORE INSERT ON provider_credential_intents
        WHEN NEW.intent_state IN ('completed','failed','cancelled','unavailable')
        BEGIN
          SELECT CASE WHEN (SELECT count(*)
            FROM provider_credential_intents pending
            JOIN provider_credential_intent_events pending_event
              ON pending_event.intent_id = pending.intent_id
            WHERE pending.profile_id = NEW.profile_id
              AND pending.generation_id = NEW.generation_id
              AND pending.intent_kind = NEW.intent_kind
              AND pending.from_lifecycle_state = NEW.from_lifecycle_state
              AND pending.to_lifecycle_state = NEW.to_lifecycle_state
              AND pending.intent_state = 'pending'
              AND pending_event.event_version = 1
              AND NOT EXISTS(
                SELECT 1 FROM provider_credential_intent_events terminal_event
                WHERE terminal_event.intent_root_id = pending_event.intent_root_id
                  AND terminal_event.event_version > pending_event.event_version)) <> 1
            THEN RAISE(ABORT, 'provider credential terminal intent requires one exact open pending v1 root') END;
        END;
        CREATE TRIGGER provider_credential_terminal_root_consume
        AFTER INSERT ON provider_credential_intents
        WHEN NEW.intent_state IN ('completed','failed','cancelled','unavailable')
        BEGIN
          INSERT INTO provider_credential_terminal_root_consumptions(
            pending_intent_id,terminal_intent_id,created_at)
          SELECT pending.intent_id,NEW.intent_id,NEW.created_at
          FROM provider_credential_intents pending
          JOIN provider_credential_intent_events pending_event
            ON pending_event.intent_id = pending.intent_id
          WHERE pending.profile_id = NEW.profile_id
            AND pending.generation_id = NEW.generation_id
            AND pending.intent_kind = NEW.intent_kind
            AND pending.from_lifecycle_state = NEW.from_lifecycle_state
            AND pending.to_lifecycle_state = NEW.to_lifecycle_state
            AND pending.intent_state = 'pending'
            AND pending_event.event_version = 1
            AND NOT EXISTS(
              SELECT 1 FROM provider_credential_intent_events terminal_event
              WHERE terminal_event.intent_root_id = pending_event.intent_root_id
                AND terminal_event.event_version > pending_event.event_version);
          SELECT CASE WHEN changes() <> 1
            THEN RAISE(ABORT, 'provider credential terminal intent must atomically consume one pending root') END;
        END;
        CREATE TRIGGER provider_credential_intent_event_chain_guard
        BEFORE INSERT ON provider_credential_intent_events
        BEGIN
           SELECT CASE WHEN (NEW.event_version = 1 AND NOT EXISTS(
                SELECT 1 FROM provider_credential_intents current
                WHERE current.intent_id = NEW.intent_id AND current.intent_state = 'pending'
                  AND current.created_at <= NEW.created_at))
              OR (NEW.event_version > 1 AND NOT EXISTS(
                SELECT 1 FROM provider_credential_intent_events prior_event
                JOIN provider_credential_intents prior ON prior.intent_id = prior_event.intent_id
                JOIN provider_credential_intents current ON current.intent_id = NEW.intent_id
                WHERE prior_event.intent_event_id = NEW.prior_intent_event_id
                  AND prior_event.intent_root_id = NEW.intent_root_id
                  AND prior_event.event_version = NEW.event_version - 1
                  AND prior.intent_state = 'pending'
                  AND current.intent_state IN ('completed','failed','cancelled','unavailable')
                  AND current.profile_id = prior.profile_id AND current.generation_id = prior.generation_id
                  AND current.intent_kind = prior.intent_kind
                  AND current.from_lifecycle_state = prior.from_lifecycle_state
                  AND current.to_lifecycle_state = prior.to_lifecycle_state
                  AND prior.created_at <= prior_event.created_at
                  AND prior_event.created_at < current.created_at
                  AND current.created_at <= NEW.created_at
                  AND EXISTS(SELECT 1 FROM provider_credential_terminal_root_consumptions consumption
                    WHERE consumption.pending_intent_id = prior.intent_id
                      AND consumption.terminal_intent_id = current.intent_id
                      AND consumption.created_at = current.created_at)))
            THEN RAISE(ABORT, 'provider credential terminal event must append to its exact pending intent root') END;
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_credential_intents current
            WHERE current.intent_id = NEW.intent_id
              AND current.created_at = NEW.created_at)
            THEN RAISE(ABORT, 'provider credential event time must equal its exact durable intent time') END;
          SELECT CASE WHEN EXISTS(
            SELECT 1 FROM provider_credential_intent_events existing_event
            JOIN provider_credential_intents existing_intent
              ON existing_intent.intent_id = existing_event.intent_id
            JOIN provider_credential_intents current_intent
              ON current_intent.intent_id = NEW.intent_id
            WHERE existing_intent.profile_id = current_intent.profile_id
              AND existing_event.created_at >= NEW.created_at)
            THEN RAISE(ABORT, 'provider credential events must advance the profile-wide durable sequence') END;
        END;
        CREATE TABLE provider_capability_snapshots(
            capability_snapshot_id TEXT PRIMARY KEY,
            provider TEXT NOT NULL CHECK(provider = 'openai'),
            model TEXT NOT NULL CHECK(model = 'gpt-5.6-sol'),
            service_tier TEXT NOT NULL CHECK(service_tier = 'default'),
            reasoning_effort TEXT NOT NULL CHECK(reasoning_effort = 'medium'),
            reasoning_context TEXT NOT NULL CHECK(reasoning_context = 'current_turn'),
            reasoning_mode TEXT NOT NULL CHECK(reasoning_mode = 'standard'),
            store INTEGER NOT NULL CHECK(store = 0),
            background INTEGER NOT NULL CHECK(background = 0),
            stream INTEGER NOT NULL CHECK(stream = 0),
            tool_choice TEXT NOT NULL CHECK(tool_choice = 'none'),
            tool_count INTEGER NOT NULL CHECK(tool_count = 0),
            truncation TEXT NOT NULL CHECK(truncation = 'disabled'),
            prompt_cache_mode TEXT NOT NULL CHECK(prompt_cache_mode = 'explicit'),
            has_prompt_cache_key INTEGER NOT NULL CHECK(has_prompt_cache_key = 0),
            has_prompt_cache_breakpoint INTEGER NOT NULL CHECK(has_prompt_cache_breakpoint = 0),
            maximum_context_tokens INTEGER NOT NULL CHECK(maximum_context_tokens > 0),
            revision TEXT NOT NULL CHECK(length(trim(revision)) > 0),
            fingerprint TEXT NOT NULL UNIQUE,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE provider_price_snapshots(
            price_snapshot_id TEXT PRIMARY KEY,
            provider TEXT NOT NULL CHECK(provider = 'openai'),
            model TEXT NOT NULL CHECK(model = 'gpt-5.6-sol'),
            currency TEXT NOT NULL CHECK(currency = 'USD'),
            service_tier TEXT NOT NULL CHECK(service_tier = 'default'),
            revision TEXT NOT NULL CHECK(length(trim(revision)) > 0),
            fingerprint TEXT NOT NULL UNIQUE,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE provider_price_rules(
            price_snapshot_id TEXT NOT NULL REFERENCES provider_price_snapshots(price_snapshot_id) ON DELETE RESTRICT,
            rule_id TEXT NOT NULL,
            context_band TEXT NOT NULL CHECK(context_band = 'standard-under-272k'),
            cache_class TEXT NOT NULL CHECK(cache_class IN ('ordinary-input','cache-write','cache-read','none')),
            token_class TEXT NOT NULL CHECK(token_class IN ('input','output','reasoning')),
            tool_class TEXT NOT NULL CHECK(tool_class = 'none'),
            region TEXT NOT NULL CHECK(region = 'global'),
            numerator_nano_usd INTEGER NOT NULL CHECK(numerator_nano_usd >= 0),
            denominator_tokens INTEGER NOT NULL CHECK(denominator_tokens > 0),
            revision TEXT NOT NULL CHECK(length(trim(revision)) > 0),
            PRIMARY KEY(price_snapshot_id,rule_id)
        ) STRICT;
        CREATE TABLE provider_effective_scan_configurations_v2(
            configuration_id TEXT PRIMARY KEY CHECK(length(trim(configuration_id)) > 0),
            local_configuration_v1_id TEXT NOT NULL CHECK(length(trim(local_configuration_v1_id)) > 0),
            local_configuration_v1_fingerprint TEXT NOT NULL CHECK(length(local_configuration_v1_fingerprint) = 64),
            local_configuration_v1_provenance TEXT NOT NULL CHECK(local_configuration_v1_provenance = 'asserted-retained-v1-identity'),
            profile_id TEXT NOT NULL,
            generation_id TEXT NOT NULL,
            model TEXT NOT NULL CHECK(model = 'gpt-5.6-sol'),
            reasoning_effort TEXT NOT NULL CHECK(reasoning_effort = 'medium'),
            reasoning_context TEXT NOT NULL CHECK(reasoning_context = 'current_turn'),
            reasoning_mode TEXT NOT NULL CHECK(reasoning_mode = 'standard'),
            store INTEGER NOT NULL CHECK(store = 0),
            service_tier TEXT NOT NULL CHECK(service_tier = 'default'),
            background INTEGER NOT NULL CHECK(background = 0),
            stream INTEGER NOT NULL CHECK(stream = 0),
            tool_choice TEXT NOT NULL CHECK(tool_choice = 'none'),
            tool_count INTEGER NOT NULL CHECK(tool_count = 0),
            truncation TEXT NOT NULL CHECK(truncation = 'disabled'),
            prompt_cache_mode TEXT NOT NULL CHECK(prompt_cache_mode = 'explicit'),
            has_prompt_cache_key INTEGER NOT NULL CHECK(has_prompt_cache_key = 0),
            has_prompt_cache_breakpoint INTEGER NOT NULL CHECK(has_prompt_cache_breakpoint = 0),
            maximum_request_bytes INTEGER NOT NULL CHECK(maximum_request_bytes BETWEEN 1 AND 65536),
            maximum_input_tokens INTEGER NOT NULL CHECK(maximum_input_tokens BETWEEN 1 AND 73728),
            maximum_output_tokens INTEGER NOT NULL CHECK(maximum_output_tokens BETWEEN 1 AND 4096),
            maximum_raw_response_bytes INTEGER NOT NULL CHECK(maximum_raw_response_bytes BETWEEN 1 AND 1048576),
            maximum_dispatch_count INTEGER NOT NULL CHECK(maximum_dispatch_count = 1),
            maximum_calculated_nano_usd INTEGER NOT NULL CHECK(maximum_calculated_nano_usd BETWEEN 1 AND 600000000),
            deadline_milliseconds INTEGER NOT NULL CHECK(deadline_milliseconds BETWEEN 1 AND 120000),
            not_used_boundaries_json TEXT NOT NULL CHECK(json(not_used_boundaries_json) = json('["hosted-search","nexus","loot"]')),
            created_at TEXT NOT NULL,
            UNIQUE(configuration_id,profile_id,generation_id),
            UNIQUE(configuration_id,local_configuration_v1_id,local_configuration_v1_fingerprint),
            FOREIGN KEY(profile_id,generation_id)
              REFERENCES provider_generations(profile_id,generation_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE evidence_acquisition_runs(
            acquisition_run_id TEXT PRIMARY KEY CHECK(length(trim(acquisition_run_id)) > 0),
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            parent_analysis_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            application_scope_id TEXT NOT NULL,
            cost_attribution_scope_id TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(acquisition_run_id,parent_analysis_run_id),
            UNIQUE(acquisition_run_id,parent_analysis_run_id,application_scope_id,cost_attribution_scope_id),
            UNIQUE(acquisition_run_id,parent_analysis_run_id,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,application_scope_id,cost_attribution_scope_id)
        ) STRICT;
        CREATE TABLE evidence_acquisition_job_nodes(
            acquisition_job_node_id TEXT PRIMARY KEY CHECK(length(trim(acquisition_job_node_id)) > 0),
            acquisition_run_id TEXT NOT NULL REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
            node_kind TEXT NOT NULL CHECK(length(trim(node_kind)) > 0),
            lifecycle_state TEXT NOT NULL CHECK(length(trim(lifecycle_state)) > 0),
            created_at TEXT NOT NULL,
            UNIQUE(acquisition_run_id,acquisition_job_node_id)
        ) STRICT;
        CREATE TABLE evidence_acquisition_attempts(
            acquisition_attempt_id TEXT PRIMARY KEY CHECK(length(trim(acquisition_attempt_id)) > 0),
            acquisition_run_id TEXT NOT NULL,
            acquisition_job_node_id TEXT NOT NULL,
            attempt_ordinal INTEGER NOT NULL CHECK(attempt_ordinal > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            created_at TEXT NOT NULL,
            UNIQUE(acquisition_run_id,attempt_ordinal),
            UNIQUE(acquisition_run_id,acquisition_attempt_id),
            FOREIGN KEY(acquisition_run_id,acquisition_job_node_id)
              REFERENCES evidence_acquisition_job_nodes(acquisition_run_id,acquisition_job_node_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE evidence_acquisition_commands(
            command_id TEXT PRIMARY KEY CHECK(length(trim(command_id)) > 0),
            acquisition_run_id TEXT NOT NULL REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
            command_kind TEXT NOT NULL CHECK(command_kind = 'provider-operation'),
            requested_at TEXT NOT NULL,
            disposition TEXT NOT NULL CHECK(length(trim(disposition)) > 0),
            UNIQUE(command_id,acquisition_run_id,requested_at)
        ) STRICT;
        CREATE TABLE provider_command_bindings(
            command_id TEXT PRIMARY KEY CHECK(length(trim(command_id)) > 0),
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL CHECK(length(trim(owner_id)) > 0),
            requested_at TEXT NOT NULL,
            UNIQUE(command_id,owner_kind,owner_id,requested_at)
        ) STRICT;
        CREATE TRIGGER provider_command_binding_owner_guard
        BEFORE INSERT ON provider_command_bindings
        BEGIN
          SELECT CASE
            WHEN NEW.owner_kind = 'analysis-run' AND NOT EXISTS(
              SELECT 1 FROM durable_commands c
              WHERE c.command_id = NEW.command_id AND c.run_id = NEW.owner_id AND c.created_at = NEW.requested_at)
              THEN RAISE(ABORT, 'provider command must bind exact analysis-run durable command and request time')
            WHEN NEW.owner_kind = 'evidence-acquisition-run' AND NOT EXISTS(
              SELECT 1 FROM evidence_acquisition_commands c
              WHERE c.command_id = NEW.command_id AND c.acquisition_run_id = NEW.owner_id AND c.requested_at = NEW.requested_at)
              THEN RAISE(ABORT, 'provider command must bind exact evidence-acquisition durable command and request time')
          END;
        END;
        CREATE TABLE evidence_acquisition_parent_links(
            parent_link_id TEXT PRIMARY KEY,
            acquisition_run_id TEXT NOT NULL REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
            analysis_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            relation TEXT NOT NULL CHECK(relation IN ('initiated-by','attached','detached')),
            dispatch_sequence_cutoff INTEGER,
            created_at TEXT NOT NULL,
            FOREIGN KEY(acquisition_run_id,analysis_run_id)
              REFERENCES evidence_acquisition_runs(acquisition_run_id,parent_analysis_run_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE evidence_acquisition_application_links(
            application_link_id TEXT PRIMARY KEY,
            acquisition_run_id TEXT NOT NULL REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
            admission_id TEXT NOT NULL,
            analysis_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            application_scope_id TEXT NOT NULL,
            cost_attribution_scope_id TEXT NOT NULL,
            admitted_artifact_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            FOREIGN KEY(acquisition_run_id,analysis_run_id,application_scope_id,cost_attribution_scope_id)
              REFERENCES evidence_acquisition_runs(acquisition_run_id,parent_analysis_run_id,application_scope_id,cost_attribution_scope_id) ON DELETE RESTRICT,
            FOREIGN KEY(admission_id,acquisition_run_id,admitted_artifact_id)
              REFERENCES provider_semantic_admissions(admission_id,owner_id,admitted_artifact_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TRIGGER evidence_acquisition_application_admitted_artifact_guard
        BEFORE INSERT ON evidence_acquisition_application_links
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_semantic_admissions admission
            WHERE admission.owner_kind='evidence-acquisition-run'
              AND admission.owner_id=NEW.acquisition_run_id
              AND admission.admission_id=NEW.admission_id
              AND admission.state='admitted'
              AND admission.admitted_artifact_id=NEW.admitted_artifact_id
              AND admission.created_at <= NEW.created_at)
            THEN RAISE(ABORT, 'source-claim application requires an exact admitted acquisition artifact') END;
        END;
        CREATE TABLE provider_operation_blocks(
            operation_id TEXT PRIMARY KEY,
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL,
            job_node_id TEXT NOT NULL,
            command_id TEXT NOT NULL UNIQUE,
            requested_at TEXT NOT NULL,
            confirmed_at TEXT NOT NULL,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            profile_id TEXT NOT NULL,
            generation_id TEXT NOT NULL,
            revocation_epoch INTEGER NOT NULL CHECK(revocation_epoch >= 0),
            operation_kind TEXT NOT NULL CHECK(operation_kind IN ('transport-qualification','source-claim-extraction','candidate-investigation')),
            capability_snapshot_id TEXT NOT NULL REFERENCES provider_capability_snapshots(capability_snapshot_id) ON DELETE RESTRICT,
            price_snapshot_id TEXT NOT NULL REFERENCES provider_price_snapshots(price_snapshot_id) ON DELETE RESTRICT,
            prompt_id TEXT NOT NULL,
            prompt_fingerprint TEXT NOT NULL CHECK(length(prompt_fingerprint) = 64),
            output_schema_id TEXT NOT NULL,
            output_schema_fingerprint TEXT NOT NULL CHECK(length(output_schema_fingerprint) = 64),
            request_fingerprint TEXT NOT NULL CHECK(length(request_fingerprint) = 64),
            canonical_request_payload_id TEXT NOT NULL,
            canonical_request_fingerprint TEXT NOT NULL CHECK(length(canonical_request_fingerprint) = 64),
            canonical_request_bytes INTEGER NOT NULL CHECK(canonical_request_bytes > 0),
            settings_fingerprint TEXT NOT NULL CHECK(length(settings_fingerprint) = 64),
            input_bound_policy_id TEXT NOT NULL CHECK(input_bound_policy_id = 'unresolved-openai-responses-framing'),
            input_bound_policy_version TEXT NOT NULL CHECK(input_bound_policy_version = 'authority-required'),
            input_bound_proof_status TEXT NOT NULL CHECK(input_bound_proof_status = 'authority-required'),
            maximum_request_bytes INTEGER NOT NULL CHECK(maximum_request_bytes > 0),
            maximum_input_tokens INTEGER NOT NULL CHECK(maximum_input_tokens > 0),
            maximum_output_tokens INTEGER NOT NULL CHECK(maximum_output_tokens > 0),
            maximum_raw_response_bytes INTEGER NOT NULL CHECK(maximum_raw_response_bytes > 0),
            maximum_dispatch_count INTEGER NOT NULL CHECK(maximum_dispatch_count = 1),
            maximum_calculated_nano_usd INTEGER NOT NULL CHECK(maximum_calculated_nano_usd > 0),
            deadline_milliseconds INTEGER NOT NULL CHECK(deadline_milliseconds > 0),
            dispatch_deadline_utc TEXT NOT NULL,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            state TEXT NOT NULL CHECK(state = 'input-bound-blocked'),
            recorded_at TEXT NOT NULL,
            UNIQUE(operation_id,profile_id,generation_id),
            FOREIGN KEY(profile_id,generation_id)
              REFERENCES provider_generations(profile_id,generation_id) ON DELETE RESTRICT,
            FOREIGN KEY(command_id,owner_kind,owner_id,requested_at)
              REFERENCES provider_command_bindings(command_id,owner_kind,owner_id,requested_at) ON DELETE RESTRICT,
            FOREIGN KEY(effective_configuration_id,profile_id,generation_id)
              REFERENCES provider_effective_scan_configurations_v2(configuration_id,profile_id,generation_id) ON DELETE RESTRICT,
            FOREIGN KEY(canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes)
              REFERENCES payloads(payload_id,content_sha256,byte_length) ON DELETE RESTRICT,
            CHECK(length(requested_at) = 33 AND requested_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(requested_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(requested_at,1,10),'+0 days') = substr(requested_at,1,10)
              AND strftime('%H:%M:%S',substr(requested_at,1,19)) = substr(requested_at,12,8)),
            CHECK(length(confirmed_at) = 33 AND confirmed_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(confirmed_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(confirmed_at,1,10),'+0 days') = substr(confirmed_at,1,10)
              AND strftime('%H:%M:%S',substr(confirmed_at,1,19)) = substr(confirmed_at,12,8)),
            CHECK(length(dispatch_deadline_utc) = 33 AND dispatch_deadline_utc GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(dispatch_deadline_utc,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(dispatch_deadline_utc,1,10),'+0 days') = substr(dispatch_deadline_utc,1,10)
              AND strftime('%H:%M:%S',substr(dispatch_deadline_utc,1,19)) = substr(dispatch_deadline_utc,12,8)),
            CHECK(requested_at <= confirmed_at AND confirmed_at < dispatch_deadline_utc),
            CHECK(((CAST(strftime('%s', substr(dispatch_deadline_utc,1,19) || 'Z') AS INTEGER)
                     - CAST(strftime('%s', substr(confirmed_at,1,19) || 'Z') AS INTEGER)) * 10000000
                    + CAST(substr(dispatch_deadline_utc,21,7) AS INTEGER)
                    - CAST(substr(confirmed_at,21,7) AS INTEGER)) <= deadline_milliseconds * 10000),
            CHECK(((CAST(strftime('%s', substr(dispatch_deadline_utc,1,19) || 'Z') AS INTEGER)
                     - CAST(strftime('%s', substr(requested_at,1,19) || 'Z') AS INTEGER)) * 10000000
                    + CAST(substr(dispatch_deadline_utc,21,7) AS INTEGER)
                    - CAST(substr(requested_at,21,7) AS INTEGER)) <= deadline_milliseconds * 10000),
            CHECK(request_fingerprint = canonical_request_fingerprint),
            CHECK(canonical_request_bytes <= maximum_request_bytes),
            CHECK((operation_kind = 'source-claim-extraction' AND owner_kind = 'evidence-acquisition-run')
              OR (operation_kind IN ('transport-qualification','candidate-investigation') AND owner_kind = 'analysis-run')),
            CHECK((operation_kind = 'transport-qualification'
                AND maximum_request_bytes <= 16384 AND maximum_input_tokens <= 20480
                AND maximum_output_tokens <= 256 AND maximum_raw_response_bytes <= 262144
                AND maximum_calculated_nano_usd <= 140000000 AND deadline_milliseconds <= 60000)
              OR (operation_kind IN ('source-claim-extraction','candidate-investigation')
                AND maximum_request_bytes <= 65536 AND maximum_input_tokens <= 73728
                AND maximum_output_tokens <= 4096 AND maximum_raw_response_bytes <= 1048576
                AND maximum_calculated_nano_usd <= 600000000 AND deadline_milliseconds <= 120000))
        ) STRICT;
        CREATE TABLE provider_operation_authorizations(
            authorization_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL UNIQUE,
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL,
            analysis_run_id TEXT REFERENCES runs(run_id) ON DELETE RESTRICT,
            evidence_acquisition_run_id TEXT REFERENCES evidence_acquisition_runs(acquisition_run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL,
            command_id TEXT NOT NULL,
            requested_at TEXT NOT NULL,
            profile_id TEXT NOT NULL REFERENCES provider_access_profiles(profile_id) ON DELETE RESTRICT,
            generation_id TEXT NOT NULL,
            revocation_epoch INTEGER NOT NULL CHECK(revocation_epoch >= 0),
            operation_kind TEXT NOT NULL CHECK(operation_kind IN ('transport-qualification','source-claim-extraction','candidate-investigation')),
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            prompt_id TEXT NOT NULL,
            prompt_fingerprint TEXT NOT NULL,
            output_schema_id TEXT NOT NULL,
            output_schema_fingerprint TEXT NOT NULL,
            request_fingerprint TEXT NOT NULL,
            canonical_request_fingerprint TEXT NOT NULL,
            capability_snapshot_id TEXT NOT NULL REFERENCES provider_capability_snapshots(capability_snapshot_id) ON DELETE RESTRICT,
            price_snapshot_id TEXT NOT NULL REFERENCES provider_price_snapshots(price_snapshot_id) ON DELETE RESTRICT,
            settings_fingerprint TEXT NOT NULL,
            input_bound_policy_id TEXT NOT NULL CHECK(input_bound_policy_id = 'openai-responses-o200k-byte-envelope'),
            input_bound_policy_version TEXT NOT NULL CHECK(input_bound_policy_version = 'v2'),
            input_bound_proof_status TEXT NOT NULL CHECK(input_bound_proof_status = 'proved'),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            maximum_request_bytes INTEGER NOT NULL CHECK(maximum_request_bytes > 0),
            maximum_input_tokens INTEGER NOT NULL CHECK(maximum_input_tokens > 0),
            maximum_output_tokens INTEGER NOT NULL CHECK(maximum_output_tokens > 0),
            maximum_raw_response_bytes INTEGER NOT NULL CHECK(maximum_raw_response_bytes > 0),
            maximum_dispatch_count INTEGER NOT NULL CHECK(maximum_dispatch_count = 1),
            maximum_calculated_nano_usd INTEGER NOT NULL CHECK(maximum_calculated_nano_usd > 0),
            deadline_milliseconds INTEGER NOT NULL CHECK(deadline_milliseconds > 0),
            dispatch_deadline_utc TEXT NOT NULL,
            confirmed_at TEXT NOT NULL,
            UNIQUE(operation_id,profile_id,generation_id),
            UNIQUE(operation_id,request_fingerprint,canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status),
            UNIQUE(operation_id,coordinator_fencing_epoch),
            UNIQUE(operation_id,maximum_raw_response_bytes),
            UNIQUE(authorization_id,operation_id),
            UNIQUE(authorization_id,operation_id,owner_kind,owner_id),
            UNIQUE(operation_id,operation_kind,maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_calculated_nano_usd),
            UNIQUE(authorization_id,operation_id,profile_id,generation_id,revocation_epoch),
            FOREIGN KEY(profile_id,generation_id)
              REFERENCES provider_generations(profile_id,generation_id) ON DELETE RESTRICT,
            FOREIGN KEY(command_id,owner_kind,owner_id,requested_at)
              REFERENCES provider_command_bindings(command_id,owner_kind,owner_id,requested_at) ON DELETE RESTRICT,
            FOREIGN KEY(effective_configuration_id,profile_id,generation_id)
              REFERENCES provider_effective_scan_configurations_v2(configuration_id,profile_id,generation_id) ON DELETE RESTRICT,
            CHECK(request_fingerprint = canonical_request_fingerprint),
            CHECK(length(requested_at) = 33 AND requested_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(requested_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(requested_at,1,10),'+0 days') = substr(requested_at,1,10)
              AND strftime('%H:%M:%S',substr(requested_at,1,19)) = substr(requested_at,12,8)),
            CHECK(length(confirmed_at) = 33 AND confirmed_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(confirmed_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(confirmed_at,1,10),'+0 days') = substr(confirmed_at,1,10)
              AND strftime('%H:%M:%S',substr(confirmed_at,1,19)) = substr(confirmed_at,12,8)),
            CHECK(length(dispatch_deadline_utc) = 33 AND dispatch_deadline_utc GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(dispatch_deadline_utc,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(dispatch_deadline_utc,1,10),'+0 days') = substr(dispatch_deadline_utc,1,10)
              AND strftime('%H:%M:%S',substr(dispatch_deadline_utc,1,19)) = substr(dispatch_deadline_utc,12,8)),
            CHECK(requested_at <= confirmed_at AND confirmed_at < dispatch_deadline_utc),
            CHECK(((CAST(strftime('%s', substr(dispatch_deadline_utc,1,19) || 'Z') AS INTEGER)
                     - CAST(strftime('%s', substr(confirmed_at,1,19) || 'Z') AS INTEGER)) * 10000000
                    + CAST(substr(dispatch_deadline_utc,21,7) AS INTEGER)
                    - CAST(substr(confirmed_at,21,7) AS INTEGER)) <= deadline_milliseconds * 10000),
            CHECK(((CAST(strftime('%s', substr(dispatch_deadline_utc,1,19) || 'Z') AS INTEGER)
                     - CAST(strftime('%s', substr(requested_at,1,19) || 'Z') AS INTEGER)) * 10000000
                    + CAST(substr(dispatch_deadline_utc,21,7) AS INTEGER)
                    - CAST(substr(requested_at,21,7) AS INTEGER)) <= deadline_milliseconds * 10000),
            CHECK((owner_kind = 'analysis-run' AND owner_id = analysis_run_id
                AND analysis_run_id IS NOT NULL AND evidence_acquisition_run_id IS NULL)
              OR (owner_kind = 'evidence-acquisition-run' AND owner_id = evidence_acquisition_run_id
                AND evidence_acquisition_run_id IS NOT NULL AND analysis_run_id IS NULL)),
            CHECK((operation_kind = 'transport-qualification'
                AND maximum_request_bytes <= 16384 AND maximum_input_tokens <= 20480
                AND maximum_output_tokens <= 256 AND maximum_raw_response_bytes <= 262144
                AND maximum_calculated_nano_usd <= 140000000 AND deadline_milliseconds <= 60000)
              OR (operation_kind IN ('source-claim-extraction','candidate-investigation')
                AND maximum_request_bytes <= 65536 AND maximum_input_tokens <= 73728
                AND maximum_output_tokens <= 4096 AND maximum_raw_response_bytes <= 1048576
                AND maximum_calculated_nano_usd <= 600000000 AND deadline_milliseconds <= 120000))
        ) STRICT;
        CREATE TRIGGER provider_block_owner_job_guard
        BEFORE INSERT ON provider_operation_blocks
        BEGIN
          SELECT CASE
            WHEN NEW.owner_kind = 'analysis-run' AND NOT EXISTS(
              SELECT 1 FROM runs r JOIN job_nodes j ON j.run_id = r.run_id
              JOIN provider_effective_scan_configurations_v2 configuration
                ON configuration.configuration_id = NEW.effective_configuration_id
               AND configuration.local_configuration_v1_id = r.effective_scan_configuration_id
              WHERE r.run_id = NEW.owner_id AND j.job_node_id = NEW.job_node_id
                AND r.installation_snapshot_id = NEW.installation_snapshot_id
                AND r.analysis_context_id = NEW.analysis_context_id
                AND r.resolved_input_manifest_id = NEW.resolved_input_manifest_id)
              THEN RAISE(ABORT, 'analysis-run provider block job node owner mismatch')
            WHEN NEW.owner_kind = 'evidence-acquisition-run' AND NOT EXISTS(
              SELECT 1 FROM evidence_acquisition_runs a
              JOIN evidence_acquisition_job_nodes j ON j.acquisition_run_id = a.acquisition_run_id
              JOIN provider_effective_scan_configurations_v2 configuration
                ON configuration.configuration_id = NEW.effective_configuration_id
               AND configuration.local_configuration_v1_id = a.effective_configuration_id
              WHERE a.acquisition_run_id = NEW.owner_id AND j.acquisition_job_node_id = NEW.job_node_id
                AND a.installation_snapshot_id = NEW.installation_snapshot_id
                AND a.analysis_context_id = NEW.analysis_context_id
                AND a.resolved_input_manifest_id = NEW.resolved_input_manifest_id)
              THEN RAISE(ABORT, 'evidence-acquisition provider block job node owner mismatch')
          END;
        END;
        CREATE TRIGGER provider_block_eligibility_guard
        BEFORE INSERT ON provider_operation_blocks
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_profile_projection p
            JOIN provider_access_profiles a ON a.profile_id = p.profile_id
            JOIN provider_generations g ON g.profile_id = p.profile_id AND g.generation_id = p.generation_id
            JOIN provider_credential_intents i ON i.intent_id = p.intent_id
            WHERE p.profile_id = NEW.profile_id AND p.generation_id = NEW.generation_id
              AND p.revocation_epoch = NEW.revocation_epoch
              AND p.capability_snapshot_id = NEW.capability_snapshot_id
              AND p.lifecycle_state = 'active-verified' AND p.verification_state = 'available'
              AND p.account_identity_id IS NOT NULL AND p.billing_scope_identity_id IS NOT NULL
              AND a.account_identity_id = p.account_identity_id
              AND a.billing_scope_identity_id = p.billing_scope_identity_id
              AND g.revocation_epoch <= p.revocation_epoch
              AND i.profile_id = p.profile_id AND i.generation_id = p.generation_id
              AND i.capability_snapshot_id = p.capability_snapshot_id
              AND i.account_identity_id = p.account_identity_id
              AND i.billing_scope_identity_id = p.billing_scope_identity_id
              AND i.to_lifecycle_state = p.lifecycle_state
              AND i.verification_state = p.verification_state
              AND i.intent_state = 'completed'
              AND NOT EXISTS(
                SELECT 1 FROM provider_credential_intents replacement
                JOIN provider_generations replacement_generation
                  ON replacement_generation.profile_id = replacement.profile_id
                 AND replacement_generation.generation_id = replacement.generation_id
                JOIN provider_generations current_generation
                  ON current_generation.profile_id = p.profile_id
                 AND current_generation.generation_id = p.generation_id
                WHERE replacement.profile_id = p.profile_id
                  AND replacement.intent_kind = 'replace' AND replacement.intent_state = 'pending'
                  AND replacement_generation.generation_ordinal > current_generation.generation_ordinal))
            THEN RAISE(ABORT, 'provider block requires exact eligible verified profile generation') END;
        END;
        CREATE TRIGGER provider_authority_release_required
        BEFORE INSERT ON provider_operation_authorizations
        BEGIN
          SELECT RAISE(ABORT, 'provider authorization unavailable: accepted local input-bound policy required');
        END;
        CREATE TRIGGER authorization_owner_job_guard
        BEFORE INSERT ON provider_operation_authorizations
        BEGIN
          SELECT CASE
            WHEN NEW.owner_kind = 'analysis-run' AND NOT EXISTS(
              SELECT 1 FROM runs r JOIN job_nodes j ON j.run_id = r.run_id
              JOIN provider_effective_scan_configurations_v2 configuration
                ON configuration.configuration_id = NEW.effective_configuration_id
               AND configuration.local_configuration_v1_id = r.effective_scan_configuration_id
              WHERE r.run_id = NEW.analysis_run_id AND j.job_node_id = NEW.job_node_id
                AND r.installation_snapshot_id = NEW.installation_snapshot_id
                AND r.analysis_context_id = NEW.analysis_context_id
                AND r.resolved_input_manifest_id = NEW.resolved_input_manifest_id)
              THEN RAISE(ABORT, 'analysis-run authorization job node owner mismatch')
            WHEN NEW.owner_kind = 'evidence-acquisition-run' AND NOT EXISTS(
              SELECT 1 FROM evidence_acquisition_runs a
              JOIN evidence_acquisition_job_nodes j ON j.acquisition_run_id = a.acquisition_run_id
              JOIN provider_effective_scan_configurations_v2 configuration
                ON configuration.configuration_id = NEW.effective_configuration_id
               AND configuration.local_configuration_v1_id = a.effective_configuration_id
              WHERE a.acquisition_run_id = NEW.evidence_acquisition_run_id AND j.acquisition_job_node_id = NEW.job_node_id
                AND a.installation_snapshot_id = NEW.installation_snapshot_id
                AND a.analysis_context_id = NEW.analysis_context_id
                AND a.resolved_input_manifest_id = NEW.resolved_input_manifest_id)
              THEN RAISE(ABORT, 'evidence-acquisition authorization job node owner mismatch')
          END;
        END;
        CREATE TABLE provider_operation_attempts(
            provider_attempt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL REFERENCES provider_operation_authorizations(operation_id) ON DELETE RESTRICT,
            attempt_ordinal INTEGER NOT NULL CHECK(attempt_ordinal > 0),
            initial_state TEXT NOT NULL CHECK(initial_state = 'proposed'),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            created_at TEXT NOT NULL,
            UNIQUE(operation_id,attempt_ordinal),
            UNIQUE(operation_id,provider_attempt_id),
            FOREIGN KEY(operation_id,coordinator_fencing_epoch)
              REFERENCES provider_operation_authorizations(operation_id,coordinator_fencing_epoch) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE provider_requests(
            request_id TEXT PRIMARY KEY,
            client_request_id TEXT NOT NULL UNIQUE,
            operation_id TEXT NOT NULL REFERENCES provider_operation_authorizations(operation_id) ON DELETE RESTRICT,
            provider_attempt_id TEXT NOT NULL,
            request_fingerprint TEXT NOT NULL,
            canonical_request_fingerprint TEXT NOT NULL,
            settings_fingerprint TEXT NOT NULL,
            output_schema_fingerprint TEXT NOT NULL,
            input_bound_policy_id TEXT NOT NULL CHECK(input_bound_policy_id = 'openai-responses-o200k-byte-envelope'),
            input_bound_policy_version TEXT NOT NULL CHECK(input_bound_policy_version = 'v2'),
            input_bound_proof_status TEXT NOT NULL CHECK(input_bound_proof_status = 'proved'),
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            payload_fingerprint TEXT NOT NULL CHECK(length(payload_fingerprint) = 64),
            payload_bytes INTEGER NOT NULL CHECK(payload_bytes > 0),
            created_at TEXT NOT NULL,
            UNIQUE(operation_id,request_fingerprint),
            UNIQUE(request_id,client_request_id),
            UNIQUE(operation_id,provider_attempt_id),
            UNIQUE(operation_id,provider_attempt_id,request_id),
            FOREIGN KEY(operation_id,provider_attempt_id)
              REFERENCES provider_operation_attempts(operation_id,provider_attempt_id) ON DELETE RESTRICT,
            FOREIGN KEY(payload_id,payload_fingerprint,payload_bytes)
              REFERENCES payloads(payload_id,content_sha256,byte_length) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,request_fingerprint,canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status)
              REFERENCES provider_operation_authorizations(operation_id,request_fingerprint,canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,
                input_bound_policy_id,input_bound_policy_version,input_bound_proof_status) ON DELETE RESTRICT,
            CHECK(request_fingerprint = canonical_request_fingerprint),
            CHECK(payload_fingerprint = canonical_request_fingerprint)
        ) STRICT;
        CREATE UNIQUE INDEX idx_provider_request_fingerprint ON provider_requests(request_fingerprint);
        CREATE TRIGGER provider_request_authorization_ceiling_guard
        BEFORE INSERT ON provider_requests
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_operation_authorizations a
            WHERE a.operation_id = NEW.operation_id
              AND NEW.payload_bytes <= a.maximum_request_bytes)
            THEN RAISE(ABORT, 'provider request exceeds exact pre-dispatch authorization byte ceiling') END;
        END;
        CREATE TABLE provider_reservations(
            reservation_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL REFERENCES provider_operation_authorizations(operation_id) ON DELETE RESTRICT,
            provider_attempt_id TEXT NOT NULL,
            request_id TEXT NOT NULL,
            usage_json TEXT NOT NULL CHECK(json_valid(usage_json)),
            reserved_dispatch_count INTEGER NOT NULL CHECK(reserved_dispatch_count = 1),
            reserved_input_tokens INTEGER NOT NULL CHECK(reserved_input_tokens > 0 AND reserved_input_tokens <= 73728),
            reserved_output_tokens INTEGER NOT NULL CHECK(reserved_output_tokens > 0 AND reserved_output_tokens <= 4096),
            reserved_reasoning_tokens INTEGER NOT NULL CHECK(reserved_reasoning_tokens >= 0 AND reserved_reasoning_tokens <= reserved_output_tokens),
            reserved_cache_read_tokens INTEGER NOT NULL CHECK(reserved_cache_read_tokens = 0),
            reserved_cache_write_tokens INTEGER NOT NULL CHECK(reserved_cache_write_tokens = 0),
            reserved_priced_tool_calls INTEGER NOT NULL CHECK(reserved_priced_tool_calls = 0),
            maximum_nano_usd INTEGER NOT NULL CHECK(maximum_nano_usd > 0),
            expires_at TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(operation_id,provider_attempt_id),
            UNIQUE(operation_id,provider_attempt_id,request_id,reservation_id),
            FOREIGN KEY(operation_id,provider_attempt_id,request_id)
              REFERENCES provider_requests(operation_id,provider_attempt_id,request_id) ON DELETE RESTRICT,
            CHECK(usage_json = json_object(
              'dispatch_count',reserved_dispatch_count,
              'input_tokens',reserved_input_tokens,
              'output_tokens',reserved_output_tokens,
              'total_tokens',reserved_input_tokens + reserved_output_tokens,
              'reasoning_tokens',reserved_reasoning_tokens,
              'cache_read_tokens',reserved_cache_read_tokens,
              'cache_write_tokens',reserved_cache_write_tokens,
              'priced_tool_calls',reserved_priced_tool_calls,
              'calculated_nano_usd',maximum_nano_usd))
        ) STRICT;
        CREATE TRIGGER provider_reservation_authorization_vector_guard
        BEFORE INSERT ON provider_reservations
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_operation_authorizations a
            WHERE a.operation_id = NEW.operation_id
              AND NEW.reserved_dispatch_count <= a.maximum_dispatch_count
              AND NEW.reserved_input_tokens <= a.maximum_input_tokens
              AND NEW.reserved_output_tokens <= a.maximum_output_tokens
              AND NEW.maximum_nano_usd <= a.maximum_calculated_nano_usd)
            THEN RAISE(ABORT, 'provider reservation vector exceeds exact pre-dispatch authorization ceiling') END;
        END;
        CREATE TABLE provider_reservation_scope_items(
            reservation_scope_item_id TEXT PRIMARY KEY,
            reservation_id TEXT NOT NULL REFERENCES provider_reservations(reservation_id) ON DELETE RESTRICT,
            scope_kind TEXT NOT NULL CHECK(scope_kind IN ('operation','evidence-acquisition-run','analysis-run','provider-profile','provider-account','global')),
            scope_id TEXT NOT NULL,
            usage_json TEXT NOT NULL CHECK(json_valid(usage_json)),
            nano_usd INTEGER NOT NULL CHECK(nano_usd >= 0),
            UNIQUE(reservation_id,scope_kind,scope_id)
        ) STRICT;
        CREATE INDEX idx_provider_reservation_scope ON provider_reservation_scope_items(scope_kind,scope_id);
        CREATE TRIGGER provider_reservation_scope_vector_guard
        BEFORE INSERT ON provider_reservation_scope_items
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_reservations reservation
            WHERE reservation.reservation_id = NEW.reservation_id
              AND reservation.usage_json = NEW.usage_json
              AND reservation.maximum_nano_usd = NEW.nano_usd)
            THEN RAISE(ABORT, 'provider reservation scope must retain the exact operation reservation vector') END;
        END;
        CREATE TABLE provider_dispatch_fences(
            dispatch_fence_id TEXT PRIMARY KEY,
            authorization_id TEXT NOT NULL,
            operation_id TEXT NOT NULL,
            reservation_id TEXT NOT NULL,
            request_id TEXT NOT NULL,
            provider_attempt_id TEXT NOT NULL,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            profile_id TEXT NOT NULL,
            generation_id TEXT NOT NULL,
            revocation_epoch INTEGER NOT NULL CHECK(revocation_epoch >= 0),
            authorized INTEGER NOT NULL CHECK(authorized = 1),
            decision_reason TEXT NOT NULL,
            evaluated_at TEXT NOT NULL,
            UNIQUE(operation_id,provider_attempt_id,request_id,dispatch_fence_id),
            UNIQUE(operation_id),
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,reservation_id)
              REFERENCES provider_reservations(operation_id,provider_attempt_id,request_id,reservation_id) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,profile_id,generation_id)
              REFERENCES provider_operation_authorizations(operation_id,profile_id,generation_id) ON DELETE RESTRICT,
            FOREIGN KEY(authorization_id,operation_id,profile_id,generation_id,revocation_epoch)
              REFERENCES provider_operation_authorizations(authorization_id,operation_id,profile_id,generation_id,revocation_epoch) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,coordinator_fencing_epoch)
              REFERENCES provider_operation_authorizations(operation_id,coordinator_fencing_epoch) ON DELETE RESTRICT,
            CHECK(length(evaluated_at) = 33 AND evaluated_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(evaluated_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(evaluated_at,1,10),'+0 days') = substr(evaluated_at,1,10)
              AND strftime('%H:%M:%S',substr(evaluated_at,1,19)) = substr(evaluated_at,12,8))
        ) STRICT;
        CREATE TRIGGER provider_dispatch_deadline_guard
        BEFORE INSERT ON provider_dispatch_fences
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_operation_authorizations a
            WHERE a.operation_id = NEW.operation_id
              AND NEW.evaluated_at >= a.confirmed_at
              AND NEW.evaluated_at < a.dispatch_deadline_utc
              AND ((CAST(strftime('%s', substr(a.dispatch_deadline_utc,1,19) || 'Z') AS INTEGER)
                     - CAST(strftime('%s', substr(NEW.evaluated_at,1,19) || 'Z') AS INTEGER)) * 10000000
                    + CAST(substr(a.dispatch_deadline_utc,21,7) AS INTEGER)
                    - CAST(substr(NEW.evaluated_at,21,7) AS INTEGER)) <= a.deadline_milliseconds * 10000)
            THEN RAISE(ABORT, 'provider dispatch fence deadline mismatch') END;
        END;
        CREATE TABLE provider_transport_events(
            transport_event_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL,
            provider_attempt_id TEXT NOT NULL,
            request_id TEXT NOT NULL,
            dispatch_fence_id TEXT NOT NULL,
            event_kind TEXT NOT NULL CHECK(event_kind IN ('not-started','may-have-started','started','response-staged','failed-known','ambiguous')),
            sequence INTEGER NOT NULL CHECK(sequence > 0),
            occurred_at TEXT NOT NULL,
            UNIQUE(provider_attempt_id,sequence),
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,dispatch_fence_id)
              REFERENCES provider_dispatch_fences(operation_id,provider_attempt_id,request_id,dispatch_fence_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TRIGGER provider_transport_event_order_guard
        BEFORE INSERT ON provider_transport_events
        BEGIN
          SELECT CASE
            WHEN NEW.sequence = 1 AND NEW.event_kind <> 'not-started'
              THEN RAISE(ABORT, 'provider transport must begin at not-started')
            WHEN NEW.sequence <> 1 AND NEW.event_kind = 'not-started'
              THEN RAISE(ABORT, 'provider transport not-started may occur only once')
            WHEN NEW.sequence > 1 AND NOT EXISTS(
              SELECT 1 FROM provider_transport_events e
              WHERE e.provider_attempt_id = NEW.provider_attempt_id AND e.sequence = NEW.sequence - 1)
              THEN RAISE(ABORT, 'provider transport sequence gap')
            WHEN NEW.sequence = 1 AND NEW.occurred_at < (
              SELECT evaluated_at FROM provider_dispatch_fences f
              WHERE f.operation_id = NEW.operation_id AND f.provider_attempt_id = NEW.provider_attempt_id
                AND f.request_id = NEW.request_id AND f.dispatch_fence_id = NEW.dispatch_fence_id)
              THEN RAISE(ABORT, 'provider transport precedes dispatch fence')
            WHEN NEW.sequence > 1 AND NEW.occurred_at < (
              SELECT e.occurred_at FROM provider_transport_events e
              WHERE e.provider_attempt_id = NEW.provider_attempt_id AND e.sequence = NEW.sequence - 1)
              THEN RAISE(ABORT, 'provider transport time ordering violation')
            WHEN NEW.event_kind IN ('started','may-have-started') AND NOT EXISTS(
              SELECT 1 FROM provider_transport_events e WHERE e.provider_attempt_id = NEW.provider_attempt_id
                AND e.sequence = NEW.sequence - 1 AND e.event_kind = 'not-started')
              THEN RAISE(ABORT, 'provider transport start ordering violation')
            WHEN NEW.event_kind IN ('response-staged','failed-known','ambiguous') AND NOT EXISTS(
              SELECT 1 FROM provider_transport_events e WHERE e.provider_attempt_id = NEW.provider_attempt_id
                AND e.sequence = NEW.sequence - 1 AND e.event_kind IN ('started','may-have-started'))
              THEN RAISE(ABORT, 'provider transport terminal ordering violation')
          END;
        END;
        CREATE TABLE provider_responses(
            response_record_id TEXT PRIMARY KEY,
            availability TEXT NOT NULL CHECK(availability IN ('available','unavailable')),
            usage_availability TEXT NOT NULL CHECK(usage_availability IN ('available','unavailable')),
            authorization_id TEXT,
            operation_id TEXT NOT NULL,
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL CHECK(length(trim(owner_id)) > 0),
            request_id TEXT,
            provider_attempt_id TEXT,
            reservation_id TEXT,
            dispatch_fence_id TEXT,
            operation_kind TEXT NOT NULL CHECK(operation_kind IN ('transport-qualification','source-claim-extraction','candidate-investigation')),
            maximum_input_tokens INTEGER NOT NULL CHECK(maximum_input_tokens BETWEEN 1 AND 73728),
            maximum_output_tokens INTEGER NOT NULL CHECK(maximum_output_tokens BETWEEN 1 AND 4096),
            maximum_calculated_nano_usd INTEGER NOT NULL CHECK(maximum_calculated_nano_usd BETWEEN 1 AND 600000000),
            raw_response_availability TEXT NOT NULL CHECK(raw_response_availability IN ('available','unavailable','unsupported','not-applicable')),
            raw_response_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            raw_response_fingerprint TEXT CHECK(raw_response_fingerprint IS NULL OR length(raw_response_fingerprint) = 64),
            raw_response_bytes INTEGER CHECK(raw_response_bytes > 0 AND raw_response_bytes <= 1048576),
            maximum_raw_response_bytes INTEGER NOT NULL CHECK(maximum_raw_response_bytes > 0 AND maximum_raw_response_bytes <= 1048576),
            overflow_observed_excess_bytes INTEGER CHECK(overflow_observed_excess_bytes = 1),
            response_headers_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            response_headers_fingerprint TEXT,
            response_headers_bytes INTEGER CHECK(response_headers_bytes > 0 AND response_headers_bytes <= 65536),
            response_headers_availability TEXT NOT NULL CHECK(response_headers_availability IN ('available','unavailable','unsupported','not-applicable')),
            http_status_availability TEXT NOT NULL CHECK(http_status_availability IN ('available','unavailable','unsupported','not-applicable')),
            http_status INTEGER CHECK(http_status BETWEEN 100 AND 599),
            provider_response_id_availability TEXT NOT NULL CHECK(provider_response_id_availability IN ('available','unavailable','unsupported','not-applicable')),
            provider_response_id TEXT,
            client_request_id_availability TEXT NOT NULL CHECK(client_request_id_availability IN ('available','unavailable','unsupported','not-applicable')),
            client_request_id TEXT,
            provider_request_id TEXT,
            billing_evidence_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            billing_evidence_fingerprint TEXT,
            billing_evidence_bytes INTEGER CHECK(billing_evidence_bytes > 0 AND billing_evidence_bytes <= 65536),
            billing_evidence_availability TEXT NOT NULL CHECK(billing_evidence_availability IN ('available','unavailable','unsupported','not-applicable')),
            provider_request_id_availability TEXT NOT NULL CHECK(provider_request_id_availability IN ('available','unavailable','unsupported','not-applicable')),
            response_state TEXT NOT NULL CHECK(response_state IN ('completed','refusal','incomplete','failed','queued','in-progress','malformed','oversized','mismatched','unknown','cancelled')),
            refusal_availability TEXT NOT NULL CHECK(refusal_availability IN ('available','unavailable','unsupported','not-applicable')),
            refusal_code TEXT,
            incomplete_availability TEXT NOT NULL CHECK(incomplete_availability IN ('available','unavailable','unsupported','not-applicable')),
            incomplete_reason TEXT,
            error_availability TEXT NOT NULL CHECK(error_availability IN ('available','unavailable','unsupported','not-applicable')),
            error_code TEXT,
            requested_model TEXT NOT NULL CHECK(requested_model = 'gpt-5.6-sol'),
            returned_model_availability TEXT NOT NULL CHECK(returned_model_availability IN ('available','unavailable','unsupported','not-applicable')),
            returned_model TEXT CHECK(returned_model IS NULL OR length(trim(returned_model)) > 0),
            requested_service_tier TEXT NOT NULL CHECK(requested_service_tier = 'default'),
            returned_service_tier_availability TEXT NOT NULL CHECK(returned_service_tier_availability IN ('available','unavailable','unsupported','not-applicable')),
            returned_service_tier TEXT CHECK(returned_service_tier IS NULL OR length(trim(returned_service_tier)) > 0),
            reasoning_context TEXT NOT NULL CHECK(reasoning_context = 'current_turn'),
            reasoning_mode TEXT NOT NULL CHECK(reasoning_mode = 'standard'),
            prompt_cache_mode TEXT NOT NULL CHECK(prompt_cache_mode = 'explicit'),
            billing_availability TEXT NOT NULL CHECK(billing_availability IN ('available','unavailable','unsupported','not-applicable')),
            rate_availability TEXT NOT NULL CHECK(rate_availability IN ('available','unavailable','unsupported','not-applicable')),
            expected_rate_limit_fact_count INTEGER NOT NULL CHECK(expected_rate_limit_fact_count BETWEEN 0 AND 64),
            credit_availability TEXT NOT NULL CHECK(credit_availability IN ('available','unavailable','unsupported','not-applicable')),
            validation_state TEXT NOT NULL CHECK(validation_state IN ('proposed','admitted','rejected','abstained','unavailable','unsupported','deleted')),
            admission_state TEXT NOT NULL CHECK(admission_state IN ('proposed','admitted','rejected','abstained','unavailable','unsupported','deleted')),
            created_at TEXT NOT NULL,
            UNIQUE(operation_id),
            UNIQUE(request_id),
            UNIQUE(operation_id,provider_attempt_id,request_id,response_record_id),
            UNIQUE(operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id),
            FOREIGN KEY(operation_id,provider_attempt_id,request_id)
              REFERENCES provider_requests(operation_id,provider_attempt_id,request_id) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,reservation_id)
              REFERENCES provider_reservations(operation_id,provider_attempt_id,request_id,reservation_id) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,dispatch_fence_id)
              REFERENCES provider_dispatch_fences(operation_id,provider_attempt_id,request_id,dispatch_fence_id) ON DELETE RESTRICT,
            FOREIGN KEY(authorization_id,operation_id)
              REFERENCES provider_operation_authorizations(authorization_id,operation_id) ON DELETE RESTRICT,
            FOREIGN KEY(authorization_id,operation_id,owner_kind,owner_id)
              REFERENCES provider_operation_authorizations(authorization_id,operation_id,owner_kind,owner_id) ON DELETE RESTRICT,
            FOREIGN KEY(raw_response_payload_id,raw_response_fingerprint,raw_response_bytes)
              REFERENCES payloads(payload_id,content_sha256,byte_length) ON DELETE RESTRICT,
            FOREIGN KEY(response_headers_payload_id,response_headers_fingerprint,response_headers_bytes)
              REFERENCES payloads(payload_id,content_sha256,byte_length) ON DELETE RESTRICT,
            FOREIGN KEY(billing_evidence_payload_id,billing_evidence_fingerprint,billing_evidence_bytes)
              REFERENCES payloads(payload_id,content_sha256,byte_length) ON DELETE RESTRICT,
            FOREIGN KEY(request_id,client_request_id)
              REFERENCES provider_requests(request_id,client_request_id) ON DELETE RESTRICT,
            CHECK(raw_response_bytes IS NULL OR raw_response_bytes <= maximum_raw_response_bytes),
            CHECK((response_state = 'oversized' AND raw_response_availability = 'unavailable'
                AND raw_response_payload_id IS NULL AND raw_response_fingerprint IS NULL AND raw_response_bytes IS NULL
                AND overflow_observed_excess_bytes = 1)
              OR (response_state <> 'oversized' AND overflow_observed_excess_bytes IS NULL)),
            CHECK((raw_response_availability = 'available' AND raw_response_payload_id IS NOT NULL
                AND raw_response_fingerprint IS NOT NULL AND raw_response_bytes IS NOT NULL)
              OR (raw_response_availability <> 'available' AND raw_response_payload_id IS NULL
                AND raw_response_fingerprint IS NULL AND raw_response_bytes IS NULL)),
            CHECK((response_headers_availability <> 'available' AND response_headers_payload_id IS NULL
                AND response_headers_fingerprint IS NULL AND response_headers_bytes IS NULL)
              OR (response_headers_availability = 'available' AND response_headers_payload_id IS NOT NULL
                AND response_headers_fingerprint IS NOT NULL AND length(response_headers_fingerprint) = 64
                AND response_headers_bytes IS NOT NULL)),
            CHECK((provider_request_id_availability = 'available') = (provider_request_id IS NOT NULL)),
            CHECK((billing_evidence_availability = 'available' AND billing_evidence_payload_id IS NOT NULL
                AND billing_evidence_fingerprint IS NOT NULL AND length(billing_evidence_fingerprint) = 64
                AND billing_evidence_bytes IS NOT NULL)
              OR (billing_evidence_availability <> 'available' AND billing_evidence_payload_id IS NULL
                AND billing_evidence_fingerprint IS NULL AND billing_evidence_bytes IS NULL)),
            CHECK((billing_availability = 'available') = (billing_evidence_availability = 'available')),
            CHECK(availability = usage_availability),
            CHECK((rate_availability = 'available' AND expected_rate_limit_fact_count > 0)
              OR (rate_availability <> 'available' AND expected_rate_limit_fact_count = 0)),
            CHECK(credit_availability <> 'available'),
            CHECK((http_status_availability = 'available') = (http_status IS NOT NULL)),
            CHECK((provider_response_id_availability = 'available') = (provider_response_id IS NOT NULL)),
            CHECK((client_request_id_availability = 'available') = (client_request_id IS NOT NULL)),
            CHECK((refusal_availability = 'available') = (refusal_code IS NOT NULL)),
            CHECK((incomplete_availability = 'available') = (incomplete_reason IS NOT NULL)),
            CHECK((error_availability = 'available') = (error_code IS NOT NULL)),
            CHECK((returned_model_availability = 'available') = (returned_model IS NOT NULL)),
            CHECK((returned_service_tier_availability = 'available') = (returned_service_tier IS NOT NULL)),
            CHECK(length(created_at) = 33 AND created_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(created_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(created_at,1,10),'+0 days') = substr(created_at,1,10)
              AND strftime('%H:%M:%S',substr(created_at,1,19)) = substr(created_at,12,8)),
            CHECK((response_state = 'completed' AND availability = 'available' AND usage_availability = 'available'
                AND authorization_id IS NOT NULL AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND refusal_code IS NULL AND incomplete_reason IS NULL AND error_code IS NULL
                AND raw_response_payload_id IS NOT NULL AND http_status IS NOT NULL
                AND returned_model = 'gpt-5.6-sol' AND returned_service_tier = 'default'
                AND validation_state = 'proposed' AND admission_state = 'proposed')
              OR (response_state = 'refusal' AND availability = 'available' AND usage_availability = 'available' AND authorization_id IS NOT NULL
                AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND raw_response_payload_id IS NOT NULL AND http_status IS NOT NULL
                AND refusal_code IS NOT NULL AND incomplete_reason IS NULL AND error_code IS NULL
                AND validation_state IN ('rejected','abstained','unavailable','unsupported') AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              OR (response_state = 'incomplete' AND availability = 'available' AND usage_availability = 'available' AND authorization_id IS NOT NULL
                AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND raw_response_payload_id IS NOT NULL AND http_status IS NOT NULL
                AND refusal_code IS NULL AND incomplete_reason IS NOT NULL AND error_code IS NULL
                AND validation_state IN ('rejected','abstained','unavailable','unsupported') AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              OR (response_state = 'failed' AND availability = 'available' AND usage_availability = 'available' AND authorization_id IS NOT NULL
                AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND raw_response_payload_id IS NOT NULL AND http_status IS NOT NULL
                AND refusal_code IS NULL AND incomplete_reason IS NULL AND error_code IS NOT NULL
                AND validation_state IN ('rejected','abstained','unavailable','unsupported') AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              OR (response_state = 'mismatched' AND availability = 'available' AND usage_availability = 'available' AND authorization_id IS NOT NULL
                AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND raw_response_payload_id IS NOT NULL AND http_status IS NOT NULL
                AND returned_model IS NOT NULL AND returned_service_tier IS NOT NULL
                AND (returned_model <> 'gpt-5.6-sol' OR returned_service_tier <> 'default')
                AND validation_state IN ('rejected','abstained','unavailable','unsupported') AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              OR (response_state = 'cancelled' AND availability = 'unavailable' AND usage_availability = 'unavailable'
                AND authorization_id IS NOT NULL AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NULL
                AND raw_response_availability = 'unavailable' AND raw_response_payload_id IS NULL AND http_status_availability = 'unavailable' AND http_status IS NULL
                AND response_headers_availability = 'unavailable' AND response_headers_payload_id IS NULL
                AND provider_response_id_availability = 'unavailable' AND provider_response_id IS NULL
                AND client_request_id_availability = 'unavailable' AND client_request_id IS NULL
                AND provider_request_id_availability = 'unavailable' AND provider_request_id IS NULL
                AND billing_evidence_availability = 'unavailable' AND billing_evidence_payload_id IS NULL
                AND returned_model_availability = 'unavailable' AND returned_model IS NULL
                AND returned_service_tier_availability = 'unavailable' AND returned_service_tier IS NULL
                AND refusal_availability = 'unavailable' AND incomplete_availability = 'unavailable' AND error_availability = 'unavailable'
                AND billing_availability = 'unavailable' AND rate_availability = 'unavailable' AND credit_availability = 'unavailable'
                AND refusal_code IS NULL AND incomplete_reason IS NULL AND error_code IS NULL
                AND validation_state IN ('rejected','abstained','unavailable','unsupported') AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              OR (response_state = 'cancelled' AND availability = 'available' AND usage_availability = 'available'
                AND authorization_id IS NOT NULL AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL
                AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND raw_response_payload_id IS NOT NULL AND http_status IS NOT NULL
                AND refusal_code IS NULL AND incomplete_reason IS NULL AND error_code IS NULL
                AND validation_state IN ('rejected','abstained','unavailable','unsupported')
                AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              OR (response_state IN ('queued','in-progress','malformed','oversized','unknown')
                AND availability = 'available' AND usage_availability = 'available' AND authorization_id IS NOT NULL
                AND request_id IS NOT NULL AND provider_attempt_id IS NOT NULL AND reservation_id IS NOT NULL AND dispatch_fence_id IS NOT NULL
                AND ((response_state = 'oversized' AND raw_response_payload_id IS NULL)
                  OR (response_state <> 'oversized' AND raw_response_payload_id IS NOT NULL)) AND http_status IS NOT NULL
                AND refusal_code IS NULL AND incomplete_reason IS NULL AND error_code IS NULL
                AND validation_state IN ('rejected','abstained','unavailable','unsupported') AND admission_state IN ('rejected','abstained','unavailable','unsupported'))
              )
        ) STRICT;
        CREATE TRIGGER provider_response_transport_binding_guard
        BEFORE INSERT ON provider_responses
        WHEN NEW.response_state <> 'cancelled' OR NEW.availability = 'available'
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_operation_authorizations a
            WHERE a.authorization_id = NEW.authorization_id AND a.operation_id = NEW.operation_id
              AND a.owner_kind = NEW.owner_kind AND a.owner_id = NEW.owner_id
              AND a.operation_kind = NEW.operation_kind
              AND a.maximum_input_tokens = NEW.maximum_input_tokens
              AND a.maximum_output_tokens = NEW.maximum_output_tokens
              AND a.maximum_raw_response_bytes = NEW.maximum_raw_response_bytes
              AND a.maximum_calculated_nano_usd = NEW.maximum_calculated_nano_usd)
            THEN RAISE(ABORT, 'provider response requires exact authorization owner and finite limits') END;
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_transport_events e
            WHERE e.operation_id = NEW.operation_id AND e.provider_attempt_id = NEW.provider_attempt_id
              AND e.request_id = NEW.request_id AND e.dispatch_fence_id = NEW.dispatch_fence_id
               AND e.event_kind = 'response-staged'
              AND e.occurred_at <= NEW.created_at)
            THEN RAISE(ABORT, 'provider response requires exact staged transport event') END;
        END;
        CREATE TRIGGER provider_cancelled_response_operation_root_guard
        BEFORE INSERT ON provider_responses
        WHEN NEW.response_state = 'cancelled' AND NEW.availability = 'unavailable'
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_operation_authorizations a
            JOIN provider_operation_attempts attempt
              ON attempt.operation_id = a.operation_id AND attempt.provider_attempt_id = NEW.provider_attempt_id
            JOIN provider_requests request
              ON request.operation_id = a.operation_id AND request.provider_attempt_id = attempt.provider_attempt_id
             AND request.request_id = NEW.request_id
            JOIN provider_reservations reservation
              ON reservation.operation_id = a.operation_id AND reservation.provider_attempt_id = attempt.provider_attempt_id
             AND reservation.request_id = request.request_id AND reservation.reservation_id = NEW.reservation_id
            WHERE a.operation_id = NEW.operation_id AND a.authorization_id = NEW.authorization_id
              AND a.owner_kind = NEW.owner_kind AND a.owner_id = NEW.owner_id
              AND a.operation_kind = NEW.operation_kind
              AND a.maximum_input_tokens = NEW.maximum_input_tokens
              AND a.maximum_output_tokens = NEW.maximum_output_tokens
              AND a.maximum_raw_response_bytes = NEW.maximum_raw_response_bytes
              AND a.maximum_calculated_nano_usd = NEW.maximum_calculated_nano_usd
              AND a.confirmed_at <= NEW.created_at AND reservation.created_at <= NEW.created_at)
            THEN RAISE(ABORT, 'cancelled provider response requires exact reserved undispatched operation root') END;
          SELECT CASE WHEN EXISTS(
            SELECT 1 FROM provider_transport_events e
            WHERE e.operation_id = NEW.operation_id)
            THEN RAISE(ABORT, 'cancelled provider response requires an undispatched operation with no transport event') END;
        END;
        CREATE TRIGGER provider_cancelled_response_blocks_authorization_guard
        BEFORE INSERT ON provider_operation_authorizations
        WHEN EXISTS(SELECT 1 FROM provider_responses r
          WHERE r.operation_id = NEW.operation_id AND r.response_state = 'cancelled' AND r.availability = 'unavailable')
        BEGIN SELECT RAISE(ABORT, 'cancelled provider operation is terminal before authorization'); END;
        CREATE TRIGGER provider_cancelled_response_blocks_attempt_guard
        BEFORE INSERT ON provider_operation_attempts
        WHEN EXISTS(SELECT 1 FROM provider_responses r
          WHERE r.operation_id = NEW.operation_id AND r.response_state = 'cancelled' AND r.availability = 'unavailable')
        BEGIN SELECT RAISE(ABORT, 'cancelled provider operation is terminal before attempt'); END;
        CREATE TRIGGER provider_cancelled_response_blocks_request_guard
        BEFORE INSERT ON provider_requests
        WHEN EXISTS(SELECT 1 FROM provider_responses r
          WHERE r.operation_id = NEW.operation_id AND r.response_state = 'cancelled' AND r.availability = 'unavailable')
        BEGIN SELECT RAISE(ABORT, 'cancelled provider operation is terminal before request'); END;
        CREATE TRIGGER provider_cancelled_response_blocks_reservation_guard
        BEFORE INSERT ON provider_reservations
        WHEN EXISTS(SELECT 1 FROM provider_responses r
          WHERE r.operation_id = NEW.operation_id AND r.response_state = 'cancelled' AND r.availability = 'unavailable')
        BEGIN SELECT RAISE(ABORT, 'cancelled provider operation is terminal before reservation'); END;
        CREATE TRIGGER provider_cancelled_response_blocks_fence_guard
        BEFORE INSERT ON provider_dispatch_fences
        WHEN EXISTS(SELECT 1 FROM provider_responses r
          WHERE r.operation_id = NEW.operation_id AND r.response_state = 'cancelled' AND r.availability = 'unavailable')
        BEGIN SELECT RAISE(ABORT, 'cancelled provider operation is terminal before dispatch fence'); END;
        CREATE TRIGGER provider_cancelled_response_blocks_transport_guard
        BEFORE INSERT ON provider_transport_events
        WHEN EXISTS(SELECT 1 FROM provider_responses r
          WHERE r.operation_id = NEW.operation_id AND r.response_state = 'cancelled' AND r.availability = 'unavailable')
        BEGIN SELECT RAISE(ABORT, 'cancelled provider operation is terminal before transport'); END;
        CREATE TABLE provider_usage_entries(
            usage_entry_id TEXT PRIMARY KEY,
            receipt_id TEXT NOT NULL UNIQUE,
            availability TEXT NOT NULL CHECK(availability IN ('available','unavailable')),
            operation_id TEXT NOT NULL,
            provider_attempt_id TEXT,
            request_id TEXT,
            dispatch_fence_id TEXT,
            response_record_id TEXT NOT NULL,
            dispatch_count_availability TEXT NOT NULL CHECK(dispatch_count_availability IN ('available','unavailable','unsupported','not-applicable')),
            dispatch_count INTEGER CHECK(dispatch_count BETWEEN 0 AND 2),
            input_tokens_availability TEXT NOT NULL CHECK(input_tokens_availability IN ('available','unavailable','unsupported','not-applicable')),
            input_tokens INTEGER CHECK(input_tokens BETWEEN 0 AND 147456),
            output_tokens_availability TEXT NOT NULL CHECK(output_tokens_availability IN ('available','unavailable','unsupported','not-applicable')),
            output_tokens INTEGER CHECK(output_tokens BETWEEN 0 AND 8192),
            total_tokens_availability TEXT NOT NULL CHECK(total_tokens_availability IN ('available','unavailable','unsupported','not-applicable')),
            total_tokens INTEGER CHECK(total_tokens BETWEEN 0 AND 155648),
            reasoning_tokens_availability TEXT NOT NULL CHECK(reasoning_tokens_availability IN ('available','unavailable','unsupported','not-applicable')),
            reasoning_tokens INTEGER CHECK(reasoning_tokens BETWEEN 0 AND 8192),
            cache_read_tokens_availability TEXT NOT NULL CHECK(cache_read_tokens_availability IN ('available','unavailable','unsupported','not-applicable')),
            cache_read_tokens INTEGER CHECK(cache_read_tokens BETWEEN 0 AND 147456),
            cache_write_tokens_availability TEXT NOT NULL CHECK(cache_write_tokens_availability IN ('available','unavailable','unsupported','not-applicable')),
            cache_write_tokens INTEGER CHECK(cache_write_tokens BETWEEN 0 AND 147456),
            priced_tool_calls_availability TEXT NOT NULL CHECK(priced_tool_calls_availability IN ('available','unavailable','unsupported','not-applicable')),
            priced_tool_calls INTEGER CHECK(priced_tool_calls BETWEEN 0 AND 64),
            calculated_nano_usd_availability TEXT NOT NULL CHECK(calculated_nano_usd_availability IN ('available','unavailable','unsupported','not-applicable')),
            calculated_nano_usd INTEGER CHECK(calculated_nano_usd BETWEEN 0 AND 1200000000),
            billing_availability TEXT NOT NULL CHECK(billing_availability IN ('available','unavailable','unsupported','not-applicable')),
            rate_availability TEXT NOT NULL CHECK(rate_availability IN ('available','unavailable','unsupported','not-applicable')),
            credit_availability TEXT NOT NULL CHECK(credit_availability IN ('available','unavailable','unsupported','not-applicable')),
            receipt_state TEXT NOT NULL CHECK(receipt_state IN (
              'not-dispatched','complete','partial','failed-known','ambiguous','unavailable')),
            created_at TEXT NOT NULL,
            UNIQUE(provider_attempt_id),
            UNIQUE(response_record_id),
            UNIQUE(operation_id,provider_attempt_id,request_id,usage_entry_id),
            UNIQUE(response_record_id,usage_entry_id),
            FOREIGN KEY(response_record_id) REFERENCES provider_responses(response_record_id) ON DELETE RESTRICT,
            CHECK((dispatch_count_availability = 'available') = (dispatch_count IS NOT NULL)),
            CHECK((input_tokens_availability = 'available') = (input_tokens IS NOT NULL)),
            CHECK((output_tokens_availability = 'available') = (output_tokens IS NOT NULL)),
            CHECK((total_tokens_availability = 'available') = (total_tokens IS NOT NULL)),
            CHECK((reasoning_tokens_availability = 'available') = (reasoning_tokens IS NOT NULL)),
            CHECK((cache_read_tokens_availability = 'available') = (cache_read_tokens IS NOT NULL)),
            CHECK((cache_write_tokens_availability = 'available') = (cache_write_tokens IS NOT NULL)),
            CHECK((priced_tool_calls_availability = 'available') = (priced_tool_calls IS NOT NULL)),
            CHECK((calculated_nano_usd_availability = 'available') = (calculated_nano_usd IS NOT NULL)),
            CHECK(total_tokens IS NULL OR (input_tokens IS NOT NULL AND output_tokens IS NOT NULL
              AND total_tokens = input_tokens + output_tokens)),
            CHECK(reasoning_tokens IS NULL OR (output_tokens IS NOT NULL AND reasoning_tokens <= output_tokens))
        ) STRICT;
        CREATE TRIGGER provider_usage_response_totality_guard
        BEFORE INSERT ON provider_usage_entries
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_responses r
            WHERE r.response_record_id = NEW.response_record_id
              AND r.operation_id = NEW.operation_id AND r.provider_attempt_id IS NEW.provider_attempt_id
              AND r.request_id IS NEW.request_id AND r.dispatch_fence_id IS NEW.dispatch_fence_id
              AND r.created_at <= NEW.created_at
              AND r.availability = NEW.availability AND r.usage_availability = NEW.availability
              AND r.billing_availability = NEW.billing_availability
              AND r.rate_availability = NEW.rate_availability
              AND r.credit_availability = NEW.credit_availability
              AND ((r.response_state = 'cancelled' AND r.availability = 'unavailable' AND NEW.availability = 'unavailable'
                  AND NEW.provider_attempt_id IS NOT NULL AND NEW.request_id IS NOT NULL AND NEW.dispatch_fence_id IS NULL
                  AND NEW.dispatch_count_availability = 'available' AND NEW.dispatch_count = 0
                  AND NEW.input_tokens_availability = 'unavailable' AND NEW.output_tokens_availability = 'unavailable'
                  AND NEW.total_tokens_availability = 'unavailable' AND NEW.reasoning_tokens_availability = 'unavailable'
                  AND NEW.cache_read_tokens_availability = 'unavailable' AND NEW.cache_write_tokens_availability = 'unavailable'
                  AND NEW.priced_tool_calls_availability = 'unavailable'
                  AND NEW.calculated_nano_usd_availability = 'unavailable')
                OR ((r.response_state <> 'cancelled' OR r.availability = 'available') AND NEW.availability = 'available'
                  AND NEW.provider_attempt_id IS NOT NULL AND NEW.request_id IS NOT NULL AND NEW.dispatch_fence_id IS NOT NULL
                  AND NEW.dispatch_count_availability = 'available' AND NEW.dispatch_count >= 1))
              AND (r.response_state <> 'completed' OR (
                NEW.availability = 'available' AND NEW.dispatch_count_availability = 'available' AND NEW.dispatch_count >= 1
                AND NEW.input_tokens_availability = 'available' AND NEW.output_tokens_availability = 'available'
                AND NEW.total_tokens_availability = 'available' AND NEW.reasoning_tokens_availability = 'available'
                AND NEW.cache_read_tokens_availability = 'available'
                AND NEW.cache_write_tokens_availability = 'available'
                AND NEW.priced_tool_calls_availability = 'available'
                AND NEW.calculated_nano_usd_availability = 'available'))
              AND ((r.response_state IN ('completed','refusal','mismatched') AND NEW.receipt_state = 'complete')
                OR (r.response_state IN ('incomplete','queued','in-progress') AND NEW.receipt_state = 'partial')
                OR (r.response_state = 'malformed' AND NEW.receipt_state IN ('complete','partial'))
                OR (r.response_state = 'failed' AND NEW.receipt_state = 'failed-known')
                OR (r.response_state = 'unknown' AND NEW.receipt_state = 'ambiguous')
                OR (r.response_state = 'oversized' AND NEW.receipt_state IN ('complete','partial'))
                OR (r.response_state = 'cancelled' AND r.availability = 'unavailable' AND NEW.receipt_state = 'not-dispatched')
                OR (r.response_state = 'cancelled' AND r.availability = 'available'
                  AND NEW.receipt_state IN ('complete','partial','failed-known'))))
            THEN RAISE(ABORT, 'provider usage must exactly match response availability and completed-state matrix') END;
        END;
        CREATE TABLE provider_rate_limit_facts(
            rate_limit_fact_id TEXT PRIMARY KEY,
            usage_entry_id TEXT NOT NULL REFERENCES provider_usage_entries(usage_entry_id) ON DELETE RESTRICT,
            scope TEXT NOT NULL CHECK(scope IN ('request','project','organization','model')),
            dimension TEXT NOT NULL CHECK(dimension IN ('requests','input-tokens','output-tokens','total-tokens')),
            availability TEXT NOT NULL CHECK(availability IN ('available','unavailable','unsupported')),
            limit_value INTEGER CHECK(limit_value >= 0),
            remaining_value INTEGER CHECK(remaining_value >= 0),
            observed_at TEXT NOT NULL,
            resets_at TEXT,
            UNIQUE(usage_entry_id,scope,dimension),
            CHECK(length(observed_at) = 33 AND observed_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(observed_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(observed_at,1,10),'+0 days') = substr(observed_at,1,10)
              AND strftime('%H:%M:%S',substr(observed_at,1,19)) = substr(observed_at,12,8)),
            CHECK(resets_at IS NULL OR (length(resets_at) = 33 AND resets_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(resets_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(resets_at,1,10),'+0 days') = substr(resets_at,1,10)
              AND strftime('%H:%M:%S',substr(resets_at,1,19)) = substr(resets_at,12,8)
              AND resets_at >= observed_at)),
            CHECK((availability = 'available' AND limit_value IS NOT NULL AND remaining_value IS NOT NULL
                AND remaining_value <= limit_value)
              OR (availability <> 'available' AND limit_value IS NULL AND remaining_value IS NULL AND resets_at IS NULL))
        ) STRICT;
        CREATE TRIGGER provider_rate_limit_fact_totality_guard
        BEFORE INSERT ON provider_rate_limit_facts
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_usage_entries u
            JOIN provider_responses r ON r.response_record_id = u.response_record_id
            WHERE u.usage_entry_id = NEW.usage_entry_id AND u.rate_availability = 'available'
              AND r.created_at <= NEW.observed_at AND u.created_at <= NEW.observed_at)
            THEN RAISE(ABORT, 'provider rate fact requires available rate evidence') END;
          SELECT CASE WHEN EXISTS(
            SELECT 1 FROM provider_response_finalizations f
            WHERE f.usage_entry_id = NEW.usage_entry_id)
            THEN RAISE(ABORT, 'provider rate facts cannot change after response finalization') END;
        END;
        CREATE TABLE provider_response_finalizations(
            finalization_id TEXT PRIMARY KEY CHECK(length(trim(finalization_id)) > 0),
            response_record_id TEXT NOT NULL UNIQUE REFERENCES provider_responses(response_record_id) ON DELETE RESTRICT,
            usage_entry_id TEXT NOT NULL UNIQUE,
            validation_state TEXT NOT NULL CHECK(validation_state IN ('admitted','rejected','abstained','unavailable','unsupported')),
            admission_state TEXT NOT NULL CHECK(admission_state IN ('admitted','rejected','abstained','unavailable','unsupported')),
            finalized_at TEXT NOT NULL,
            FOREIGN KEY(response_record_id,usage_entry_id)
              REFERENCES provider_usage_entries(response_record_id,usage_entry_id) ON DELETE RESTRICT,
            CHECK(length(finalized_at) = 33 AND finalized_at GLOB '????-??-??T??:??:??.???????+00:00'
              AND substr(finalized_at,21,7) NOT GLOB '*[^0-9]*'
              AND date(substr(finalized_at,1,10),'+0 days') = substr(finalized_at,1,10)
              AND strftime('%H:%M:%S',substr(finalized_at,1,19)) = substr(finalized_at,12,8))
        ) STRICT;
        CREATE TRIGGER provider_response_finalization_totality_guard
        BEFORE INSERT ON provider_response_finalizations
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_responses r
            JOIN provider_usage_entries u ON u.response_record_id = r.response_record_id
            WHERE r.response_record_id = NEW.response_record_id AND u.usage_entry_id = NEW.usage_entry_id
              AND r.created_at <= NEW.finalized_at AND u.created_at <= NEW.finalized_at
              AND ((u.rate_availability = 'available' AND r.expected_rate_limit_fact_count = (
                    SELECT COUNT(*) FROM provider_rate_limit_facts fact
                    WHERE fact.usage_entry_id = u.usage_entry_id AND fact.observed_at <= NEW.finalized_at))
                OR (u.rate_availability <> 'available' AND NOT EXISTS(
                    SELECT 1 FROM provider_rate_limit_facts fact WHERE fact.usage_entry_id = u.usage_entry_id)))
              AND NOT EXISTS(SELECT 1 FROM provider_rate_limit_facts fact
                WHERE fact.usage_entry_id = u.usage_entry_id AND fact.observed_at > NEW.finalized_at)
              AND ((r.response_state = 'completed' AND (
                    (u.dispatch_count = 1
                      AND u.input_tokens <= r.maximum_input_tokens
                      AND u.output_tokens <= r.maximum_output_tokens
                      AND u.cache_read_tokens = 0 AND u.cache_write_tokens = 0
                      AND u.priced_tool_calls = 0
                      AND u.calculated_nano_usd <= r.maximum_calculated_nano_usd
                      AND NEW.validation_state = 'admitted' AND NEW.admission_state = 'admitted')
                    OR ((u.dispatch_count > 1
                      OR u.input_tokens > r.maximum_input_tokens
                      OR u.output_tokens > r.maximum_output_tokens
                      OR u.cache_read_tokens > 0 OR u.cache_write_tokens > 0
                      OR u.priced_tool_calls > 0
                      OR u.calculated_nano_usd > r.maximum_calculated_nano_usd)
                      AND NEW.validation_state IN ('rejected','abstained','unavailable','unsupported')
                      AND NEW.admission_state IN ('rejected','abstained','unavailable','unsupported'))))
                OR (r.response_state <> 'completed' AND NEW.validation_state = r.validation_state
                    AND NEW.admission_state = r.admission_state
                    AND NEW.validation_state IN ('rejected','abstained','unavailable','unsupported')
                    AND NEW.admission_state IN ('rejected','abstained','unavailable','unsupported'))))
            THEN RAISE(ABORT, 'provider response finalization requires exactly one total usage row and exact rate/admission state') END;
        END;
        CREATE TABLE provider_settlements(
            settlement_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL,
            provider_attempt_id TEXT NOT NULL,
            request_id TEXT NOT NULL,
            reservation_id TEXT NOT NULL,
            usage_entry_id TEXT,
            dispatch_fence_id TEXT,
            state TEXT NOT NULL CHECK(state IN ('settled','failed-known','unresolved-hold','overrun')),
            released_nano_usd INTEGER NOT NULL CHECK(released_nano_usd >= 0),
            retained_hold_nano_usd INTEGER NOT NULL CHECK(retained_hold_nano_usd >= 0),
            created_at TEXT NOT NULL,
            UNIQUE(reservation_id),
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,reservation_id)
              REFERENCES provider_reservations(operation_id,provider_attempt_id,request_id,reservation_id) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,usage_entry_id)
              REFERENCES provider_usage_entries(operation_id,provider_attempt_id,request_id,usage_entry_id) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,dispatch_fence_id)
              REFERENCES provider_dispatch_fences(operation_id,provider_attempt_id,request_id,dispatch_fence_id) ON DELETE RESTRICT,
            CHECK((state = 'unresolved-hold' AND released_nano_usd = 0 AND retained_hold_nano_usd > 0
                AND dispatch_fence_id IS NOT NULL)
              OR (state <> 'unresolved-hold' AND usage_entry_id IS NOT NULL))
        ) STRICT;
        CREATE TRIGGER provider_settlement_usage_classification_guard
        BEFORE INSERT ON provider_settlements
        WHEN NEW.usage_entry_id IS NOT NULL
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_reservations reservation
            JOIN provider_usage_entries usage
              ON usage.operation_id = NEW.operation_id
             AND usage.provider_attempt_id = NEW.provider_attempt_id
             AND usage.request_id = NEW.request_id
             AND usage.usage_entry_id = NEW.usage_entry_id
            WHERE reservation.reservation_id = NEW.reservation_id
              AND reservation.operation_id = NEW.operation_id
              AND reservation.provider_attempt_id = NEW.provider_attempt_id
              AND reservation.request_id = NEW.request_id
              AND ((EXISTS(SELECT 1 FROM provider_responses response
                      WHERE response.response_record_id = usage.response_record_id
                        AND response.response_state = 'cancelled'
                        AND response.reservation_id = reservation.reservation_id
                        AND response.dispatch_fence_id IS NULL)
                    AND usage.availability = 'unavailable'
                    AND usage.receipt_state = 'not-dispatched'
                    AND usage.dispatch_count = 0
                    AND NEW.state = 'settled' AND NEW.dispatch_fence_id IS NULL)
                  OR ((usage.calculated_nano_usd_availability = 'available'
                    AND usage.dispatch_count_availability = 'available'
                    AND usage.input_tokens_availability = 'available'
                    AND usage.output_tokens_availability = 'available'
                    AND usage.total_tokens_availability = 'available'
                    AND usage.reasoning_tokens_availability = 'available'
                    AND usage.cache_read_tokens_availability = 'available'
                    AND usage.cache_write_tokens_availability = 'available'
                    AND usage.priced_tool_calls_availability = 'available'
                    AND ((usage.dispatch_count > reservation.reserved_dispatch_count
                    OR usage.input_tokens > reservation.reserved_input_tokens
                    OR usage.output_tokens > reservation.reserved_output_tokens
                    OR usage.total_tokens > reservation.reserved_input_tokens + reservation.reserved_output_tokens
                    OR usage.reasoning_tokens > reservation.reserved_reasoning_tokens
                    OR usage.cache_read_tokens > reservation.reserved_cache_read_tokens
                    OR usage.cache_write_tokens > reservation.reserved_cache_write_tokens
                    OR usage.priced_tool_calls > reservation.reserved_priced_tool_calls
                    OR usage.calculated_nano_usd > reservation.maximum_nano_usd)
                  AND NEW.state = 'overrun')
                OR ((usage.dispatch_count <= reservation.reserved_dispatch_count
                    AND usage.input_tokens <= reservation.reserved_input_tokens
                    AND usage.output_tokens <= reservation.reserved_output_tokens
                    AND usage.total_tokens <= reservation.reserved_input_tokens + reservation.reserved_output_tokens
                    AND usage.reasoning_tokens <= reservation.reserved_reasoning_tokens
                    AND usage.cache_read_tokens <= reservation.reserved_cache_read_tokens
                    AND usage.cache_write_tokens <= reservation.reserved_cache_write_tokens
                    AND usage.priced_tool_calls <= reservation.reserved_priced_tool_calls
                    AND usage.calculated_nano_usd <= reservation.maximum_nano_usd)
                   AND NEW.state <> 'overrun')) AND NEW.dispatch_fence_id IS NOT NULL)))
            THEN RAISE(ABORT, 'provider settlement must classify observed usage against the exact reservation') END;
        END;
        CREATE TRIGGER provider_settlement_reservation_amount_guard
        BEFORE INSERT ON provider_settlements
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_reservations reservation
            WHERE reservation.reservation_id = NEW.reservation_id
              AND reservation.operation_id = NEW.operation_id
              AND reservation.provider_attempt_id = NEW.provider_attempt_id
              AND reservation.request_id = NEW.request_id
              AND ((NEW.state = 'unresolved-hold'
                    AND NEW.released_nano_usd = 0
                    AND NEW.retained_hold_nano_usd = reservation.maximum_nano_usd)
                OR (NEW.state <> 'unresolved-hold'
                    AND NEW.released_nano_usd = reservation.maximum_nano_usd
                    AND NEW.retained_hold_nano_usd = 0)))
            THEN RAISE(ABORT, 'provider settlement release and hold must exactly partition the reservation') END;
        END;
        CREATE VIEW provider_settlement_vector_partitions AS
        SELECT s.settlement_id,s.reservation_id,s.state,
          r.reserved_dispatch_count,r.reserved_input_tokens,r.reserved_output_tokens,
          r.reserved_input_tokens + r.reserved_output_tokens AS reserved_total_tokens,
          r.reserved_reasoning_tokens,r.reserved_cache_read_tokens,r.reserved_cache_write_tokens,
          r.reserved_priced_tool_calls,r.maximum_nano_usd AS reserved_nano_usd,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_dispatch_count END AS released_dispatch_count,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_input_tokens END AS released_input_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_output_tokens END AS released_output_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_input_tokens + r.reserved_output_tokens END AS released_total_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_reasoning_tokens END AS released_reasoning_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_cache_read_tokens END AS released_cache_read_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_cache_write_tokens END AS released_cache_write_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN 0 ELSE r.reserved_priced_tool_calls END AS released_priced_tool_calls,
          s.released_nano_usd,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_dispatch_count ELSE 0 END AS retained_dispatch_count,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_input_tokens ELSE 0 END AS retained_input_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_output_tokens ELSE 0 END AS retained_output_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_input_tokens + r.reserved_output_tokens ELSE 0 END AS retained_total_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_reasoning_tokens ELSE 0 END AS retained_reasoning_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_cache_read_tokens ELSE 0 END AS retained_cache_read_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_cache_write_tokens ELSE 0 END AS retained_cache_write_tokens,
          CASE WHEN s.state = 'unresolved-hold' THEN r.reserved_priced_tool_calls ELSE 0 END AS retained_priced_tool_calls,
          s.retained_hold_nano_usd
        FROM provider_settlements s
        JOIN provider_reservations r ON r.reservation_id = s.reservation_id;
        CREATE TABLE provider_settlement_adjustments(
            adjustment_id TEXT PRIMARY KEY,
            settlement_id TEXT NOT NULL REFERENCES provider_settlements(settlement_id) ON DELETE RESTRICT,
            delta_nano_usd INTEGER NOT NULL,
            authority_kind TEXT NOT NULL,
            reason TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE provider_semantic_proposals(
            proposal_id TEXT PRIMARY KEY,
            authorization_id TEXT NOT NULL,
            operation_id TEXT NOT NULL,
            provider_attempt_id TEXT NOT NULL,
            request_id TEXT NOT NULL,
            response_record_id TEXT NOT NULL,
            dispatch_fence_id TEXT NOT NULL,
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL CHECK(length(trim(owner_id)) > 0),
            root_subject_id TEXT NOT NULL CHECK(length(trim(root_subject_id)) > 0),
            semantic_link_id TEXT NOT NULL CHECK(length(trim(semantic_link_id)) > 0),
            proposal_kind TEXT NOT NULL CHECK(proposal_kind IN ('source-claim','candidate-hypothesis','abstention','gap')),
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id),
            FOREIGN KEY(authorization_id,operation_id)
              REFERENCES provider_operation_authorizations(authorization_id,operation_id) ON DELETE RESTRICT,
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id)
              REFERENCES provider_responses(operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE provider_semantic_validations(
            validation_id TEXT PRIMARY KEY CHECK(length(trim(validation_id)) > 0),
            proposal_id TEXT NOT NULL,
            operation_id TEXT NOT NULL,
            response_record_id TEXT NOT NULL,
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL,
            root_subject_id TEXT NOT NULL,
            state TEXT NOT NULL CHECK(state IN ('admitted','rejected','abstained','unavailable','unsupported','deleted')),
            host_policy_id TEXT NOT NULL CHECK(length(trim(host_policy_id)) > 0),
            reason TEXT NOT NULL CHECK(length(trim(reason)) > 0),
            created_at TEXT NOT NULL,
            UNIQUE(validation_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,state),
            FOREIGN KEY(proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id)
              REFERENCES provider_semantic_proposals(proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TRIGGER provider_semantic_validation_chronology_guard
        BEFORE INSERT ON provider_semantic_validations
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_semantic_proposals proposal
            WHERE proposal.proposal_id = NEW.proposal_id
              AND proposal.operation_id = NEW.operation_id
              AND proposal.response_record_id = NEW.response_record_id
              AND proposal.owner_kind = NEW.owner_kind AND proposal.owner_id = NEW.owner_id
              AND proposal.root_subject_id = NEW.root_subject_id
              AND proposal.created_at <= NEW.created_at)
            THEN RAISE(ABORT, 'semantic validation cannot predate its exact proposal root') END;
        END;
        CREATE TRIGGER provider_semantic_proposal_root_guard
        BEFORE INSERT ON provider_semantic_proposals
        BEGIN
          SELECT CASE
            WHEN NOT EXISTS(
              SELECT 1 FROM provider_operation_authorizations a
              JOIN provider_responses r ON r.authorization_id = a.authorization_id AND r.operation_id = a.operation_id
                AND r.response_record_id = NEW.response_record_id AND r.response_state = 'completed'
              JOIN provider_usage_entries u ON u.response_record_id = r.response_record_id
                AND u.operation_id = r.operation_id AND u.availability = 'available'
              JOIN provider_response_finalizations f ON f.response_record_id = r.response_record_id
                AND f.usage_entry_id = u.usage_entry_id
                AND f.validation_state = 'admitted' AND f.admission_state = 'admitted'
                AND f.finalized_at <= NEW.created_at
              WHERE a.authorization_id = NEW.authorization_id AND a.operation_id = NEW.operation_id
                AND a.owner_kind = NEW.owner_kind AND a.owner_id = NEW.owner_id)
              THEN RAISE(ABORT, 'semantic proposal requires exact completed validated admitted response authority and usage')
            WHEN NEW.owner_kind = 'evidence-acquisition-run' AND (
              NEW.proposal_kind NOT IN ('source-claim','abstention','gap')
              OR NOT EXISTS(SELECT 1 FROM provider_operation_authorizations a
                WHERE a.authorization_id = NEW.authorization_id AND a.operation_id = NEW.operation_id
                  AND a.operation_kind = 'source-claim-extraction'
                  AND a.evidence_acquisition_run_id = NEW.owner_id)
              OR NOT EXISTS(SELECT 1 FROM documentation_revisions d
                WHERE d.documentation_revision_id = NEW.root_subject_id AND d.created_at <= NEW.created_at)
              OR NOT EXISTS(SELECT 1 FROM evidence_acquisition_runs acquisition
                WHERE acquisition.acquisition_run_id = NEW.owner_id AND acquisition.created_at <= NEW.created_at))
              THEN RAISE(ABORT, 'source-claim, abstention, and gap proposals must bind exact acquisition, source revision, and application roots')
            WHEN NEW.owner_kind = 'analysis-run' AND (
              NEW.proposal_kind NOT IN ('candidate-hypothesis','abstention','gap')
              OR NOT EXISTS(SELECT 1 FROM provider_operation_authorizations a
                WHERE a.authorization_id = NEW.authorization_id AND a.operation_id = NEW.operation_id
                  AND a.operation_kind = 'candidate-investigation' AND a.analysis_run_id = NEW.owner_id)
              OR NOT EXISTS(SELECT 1 FROM analysis_candidates c
                WHERE c.candidate_id = NEW.root_subject_id AND c.run_id = NEW.owner_id
                  AND c.created_at <= NEW.created_at)
              OR NOT EXISTS(SELECT 1 FROM evidence_application_links link
                WHERE link.evidence_application_link_id = NEW.semantic_link_id AND link.run_id = NEW.owner_id
                  AND link.created_at <= NEW.created_at))
              THEN RAISE(ABORT, 'candidate, abstention, and gap proposals must bind exact analysis, candidate, and application roots')
          END;
        END;
        CREATE TRIGGER provider_semantic_proposal_chronology_guard
        BEFORE INSERT ON provider_semantic_proposals
        BEGIN
          SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM provider_response_finalizations finalization
            WHERE finalization.response_record_id = NEW.response_record_id
              AND finalization.finalized_at <= NEW.created_at)
            THEN RAISE(ABORT, 'semantic proposal cannot predate its exact response finalization') END;
        END;
        CREATE TABLE provider_semantic_admissions(
            admission_id TEXT PRIMARY KEY,
            proposal_id TEXT NOT NULL,
            operation_id TEXT NOT NULL,
            response_record_id TEXT NOT NULL,
            owner_kind TEXT NOT NULL CHECK(owner_kind IN ('analysis-run','evidence-acquisition-run')),
            owner_id TEXT NOT NULL,
            root_subject_id TEXT NOT NULL,
            validation_id TEXT NOT NULL,
            semantic_link_id TEXT NOT NULL,
            state TEXT NOT NULL CHECK(state IN ('admitted','rejected','abstained','unavailable','unsupported','deleted')),
            host_policy_id TEXT NOT NULL,
            reason TEXT NOT NULL,
            admitted_artifact_id TEXT,
            created_at TEXT NOT NULL,
            UNIQUE(admission_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,validation_id,semantic_link_id),
            FOREIGN KEY(validation_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,state)
              REFERENCES provider_semantic_validations(validation_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,state) ON DELETE RESTRICT
        ) STRICT;
        CREATE UNIQUE INDEX idx_provider_semantic_admission_artifact_owner
          ON provider_semantic_admissions(admission_id,owner_id,admitted_artifact_id);
        CREATE TRIGGER provider_semantic_admission_application_guard
        BEFORE INSERT ON provider_semantic_admissions
        BEGIN
          SELECT CASE
            WHEN NOT EXISTS(SELECT 1 FROM provider_semantic_proposals proposal
              WHERE proposal.proposal_id = NEW.proposal_id
                AND proposal.semantic_link_id = NEW.semantic_link_id)
              THEN RAISE(ABORT, 'semantic admission must retain the proposal correlation edge')
            WHEN NEW.owner_kind = 'analysis-run' AND (NOT EXISTS(
              SELECT 1 FROM analysis_candidates candidate
              WHERE candidate.candidate_id = NEW.root_subject_id AND candidate.run_id = NEW.owner_id)
              OR NOT EXISTS(
                SELECT 1 FROM evidence_application_links link
                WHERE link.evidence_application_link_id = NEW.semantic_link_id AND link.run_id = NEW.owner_id))
              THEN RAISE(ABORT, 'candidate admission must bind the exact candidate root and application edge')
          END;
        END;
        CREATE TRIGGER provider_semantic_admission_chronology_guard
        BEFORE INSERT ON provider_semantic_admissions
        BEGIN
          SELECT CASE WHEN NOT EXISTS(SELECT 1 FROM provider_semantic_validations validation
            WHERE validation.validation_id = NEW.validation_id
              AND validation.created_at <= NEW.created_at)
            THEN RAISE(ABORT, 'semantic admission cannot predate its exact validation root') END;
        END;
        CREATE TABLE candidate_investigation_outcomes(
            outcome_id TEXT PRIMARY KEY CHECK(length(trim(outcome_id)) > 0),
            authorization_id TEXT NOT NULL,
            operation_id TEXT NOT NULL,
            owner_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            context_id TEXT NOT NULL CHECK(length(trim(context_id)) > 0),
            transcript_id TEXT NOT NULL UNIQUE CHECK(length(trim(transcript_id)) > 0),
            response_record_id TEXT REFERENCES provider_responses(response_record_id) ON DELETE RESTRICT,
            response_fingerprint TEXT NOT NULL CHECK(length(response_fingerprint) = 64),
            transcript_state TEXT NOT NULL CHECK(transcript_state IN (
              'completed','malformed','refusal','incomplete','drift','not-used','unavailable')),
            disposition TEXT NOT NULL CHECK(length(trim(disposition)) > 0),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
              'retained-response','audit-only','failed-identity-drift','not-applicable','unavailable')),
            input_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            transcript_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            result_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(operation_id,context_id),
            FOREIGN KEY(authorization_id,operation_id)
              REFERENCES provider_operation_authorizations(authorization_id,operation_id) ON DELETE RESTRICT,
            CHECK((transcript_state IN ('not-used','unavailable') AND response_record_id IS NULL)
              OR (transcript_state NOT IN ('not-used','unavailable') AND response_record_id IS NOT NULL))
        ) STRICT;
        CREATE TRIGGER candidate_investigation_outcome_candidate_guard
        BEFORE INSERT ON candidate_investigation_outcomes
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM analysis_candidates candidate
            JOIN analysis_hypotheses hypothesis
              ON hypothesis.hypothesis_id=NEW.hypothesis_id
             AND hypothesis.candidate_id=candidate.candidate_id
             AND hypothesis.run_id=candidate.run_id
            JOIN provider_operation_authorizations authorization
              ON authorization.authorization_id=NEW.authorization_id
             AND authorization.operation_id=NEW.operation_id
             AND authorization.owner_kind='analysis-run' AND authorization.owner_id=NEW.owner_id
             AND authorization.operation_kind='candidate-investigation'
            WHERE candidate.candidate_id=NEW.candidate_id AND candidate.run_id=NEW.owner_id)
            THEN RAISE(ABORT, 'candidate outcome must bind its exact analysis candidate and hypothesis roots') END;
        END;
        CREATE TRIGGER candidate_investigation_outcome_response_guard
        BEFORE INSERT ON candidate_investigation_outcomes
        WHEN NEW.response_record_id IS NOT NULL
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_responses response
            WHERE response.response_record_id=NEW.response_record_id
              AND response.authorization_id=NEW.authorization_id
              AND response.operation_id=NEW.operation_id
              AND response.owner_kind='analysis-run' AND response.owner_id=NEW.owner_id
              AND response.operation_kind='candidate-investigation'
              AND response.raw_response_fingerprint=NEW.response_fingerprint)
            THEN RAISE(ABORT, 'candidate outcome must bind the exact retained provider response bytes') END;
        END;
        CREATE TABLE provider_replay_edges(
            replay_edge_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL REFERENCES provider_operation_authorizations(operation_id) ON DELETE RESTRICT,
            provider_attempt_id TEXT,
            request_id TEXT,
            response_record_id TEXT,
            dispatch_fence_id TEXT,
            replay_state TEXT NOT NULL CHECK(replay_state IN ('retained-response','audit-only','unavailable')),
            dependency_manifest_id TEXT,
            effective_configuration_id TEXT,
            created_at TEXT NOT NULL,
            FOREIGN KEY(operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id)
              REFERENCES provider_responses(operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id) ON DELETE RESTRICT,
            CHECK((replay_state = 'unavailable' AND provider_attempt_id IS NULL AND request_id IS NULL
                AND response_record_id IS NULL AND dispatch_fence_id IS NULL AND dependency_manifest_id IS NULL
                AND effective_configuration_id IS NULL)
              OR (replay_state IN ('retained-response','audit-only') AND provider_attempt_id IS NOT NULL
                AND request_id IS NOT NULL AND response_record_id IS NOT NULL
                AND dispatch_fence_id IS NOT NULL AND dependency_manifest_id IS NOT NULL
                AND effective_configuration_id IS NOT NULL))
        ) STRICT;
        CREATE TRIGGER provider_replay_configuration_guard
        BEFORE INSERT ON provider_replay_edges
        WHEN NEW.replay_state IN ('retained-response','audit-only')
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_operation_authorizations a
            JOIN provider_effective_scan_configurations_v2 configuration
              ON configuration.configuration_id = a.effective_configuration_id
             AND configuration.profile_id = a.profile_id AND configuration.generation_id = a.generation_id
            WHERE a.operation_id = NEW.operation_id
              AND a.effective_configuration_id = NEW.effective_configuration_id)
            THEN RAISE(ABORT, 'provider replay must bind the exact persisted effective configuration v2 row') END;
        END;
        CREATE TABLE provider_run_output_v2_bindings(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE RESTRICT,
            effective_configuration_v2_id TEXT NOT NULL REFERENCES provider_effective_scan_configurations_v2(configuration_id) ON DELETE RESTRICT,
            local_run_output_v1_payload_id TEXT NOT NULL,
            local_run_output_v1_fingerprint TEXT NOT NULL CHECK(length(local_run_output_v1_fingerprint) = 64),
            local_run_output_v1_bytes INTEGER NOT NULL CHECK(local_run_output_v1_bytes > 0),
            created_at TEXT NOT NULL,
            UNIQUE(run_id,effective_configuration_v2_id),
            FOREIGN KEY(local_run_output_v1_payload_id,local_run_output_v1_fingerprint,local_run_output_v1_bytes)
              REFERENCES payloads(payload_id,content_sha256,byte_length) ON DELETE RESTRICT
        ) STRICT;
        CREATE TRIGGER provider_run_output_configuration_guard
        BEFORE INSERT ON provider_run_output_v2_bindings
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM runs run
            JOIN provider_effective_scan_configurations_v2 configuration
              ON configuration.configuration_id = NEW.effective_configuration_v2_id
             AND configuration.local_configuration_v1_id = run.effective_scan_configuration_id
            WHERE run.run_id = NEW.run_id)
            THEN RAISE(ABORT, 'run-output v2 must bind the exact persisted v1-to-v2 configuration successor') END;
        END;
        CREATE TABLE provider_operation_projection(
            operation_id TEXT PRIMARY KEY REFERENCES provider_operation_blocks(operation_id) ON DELETE CASCADE,
            state TEXT NOT NULL CHECK(state = 'input-bound-blocked'),
            reserved_nano_usd INTEGER NOT NULL CHECK(reserved_nano_usd = 0),
            calculated_nano_usd INTEGER NOT NULL CHECK(calculated_nano_usd = 0),
            unresolved_hold INTEGER NOT NULL CHECK(unresolved_hold = 0),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER provider_operation_projection_monotonic_update_guard
        BEFORE UPDATE ON provider_operation_projection
        WHEN NEW.operation_id <> OLD.operation_id
          OR NEW.projection_version <= OLD.projection_version
          OR NEW.updated_at <= OLD.updated_at
        BEGIN SELECT RAISE(ABORT, 'provider operation projection must advance monotonically on one exact root'); END;
        CREATE TABLE provider_profile_projection(
            profile_id TEXT PRIMARY KEY CHECK(length(trim(profile_id)) > 0) REFERENCES provider_access_profiles(profile_id) ON DELETE CASCADE,
            generation_id TEXT NOT NULL CHECK(length(trim(generation_id)) > 0),
            revocation_epoch INTEGER NOT NULL CHECK(revocation_epoch >= 0),
            lifecycle_state TEXT NOT NULL CHECK(lifecycle_state IN (
              'pending-enrollment','active-unverified','active-verified','replacing','disabled',
              'delete-pending','deleted','secure-store-unavailable','recovery-required')),
            verification_state TEXT NOT NULL CHECK(verification_state IN (
              'available','unavailable','unsupported','not-applicable','not-used')),
            capability_snapshot_id TEXT,
            account_identity_id TEXT,
            billing_scope_identity_id TEXT,
            intent_id TEXT,
            recovery_disposition TEXT NOT NULL CHECK(recovery_disposition IN ('not-required','required','unavailable')),
            cleanup_disposition TEXT NOT NULL CHECK(cleanup_disposition IN ('not-requested','pending','confirmed','failed')),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL,
            FOREIGN KEY(profile_id,generation_id)
              REFERENCES provider_generations(profile_id,generation_id) ON DELETE RESTRICT,
            FOREIGN KEY(capability_snapshot_id)
              REFERENCES provider_capability_snapshots(capability_snapshot_id) ON DELETE RESTRICT,
            FOREIGN KEY(intent_id)
              REFERENCES provider_credential_intents(intent_id) ON DELETE RESTRICT,
            CHECK((lifecycle_state IN ('active-unverified','active-verified','replacing','disabled','delete-pending')
                AND account_identity_id IS NOT NULL AND billing_scope_identity_id IS NOT NULL
                AND capability_snapshot_id IS NOT NULL AND intent_id IS NOT NULL)
              OR (lifecycle_state = 'deleted' AND account_identity_id IS NULL AND billing_scope_identity_id IS NULL
                AND capability_snapshot_id IS NULL AND intent_id IS NULL)
              OR (lifecycle_state = 'pending-enrollment' AND account_identity_id IS NULL
                AND billing_scope_identity_id IS NULL AND capability_snapshot_id IS NULL AND intent_id IS NOT NULL)
              OR (lifecycle_state IN ('secure-store-unavailable','recovery-required') AND intent_id IS NOT NULL
                AND ((account_identity_id IS NULL AND billing_scope_identity_id IS NULL AND capability_snapshot_id IS NULL)
                  OR (account_identity_id IS NOT NULL AND billing_scope_identity_id IS NOT NULL
                    AND capability_snapshot_id IS NOT NULL)))),
            CHECK((lifecycle_state = 'pending-enrollment' AND verification_state = 'not-applicable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'not-requested')
              OR (lifecycle_state = 'active-verified' AND verification_state = 'available'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'not-requested')
              OR (lifecycle_state IN ('active-unverified','replacing','disabled') AND verification_state = 'unavailable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'not-requested')
              OR (lifecycle_state = 'delete-pending' AND verification_state = 'unavailable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition IN ('pending','failed'))
              OR (lifecycle_state = 'deleted' AND verification_state = 'unavailable'
                AND recovery_disposition = 'not-required' AND cleanup_disposition = 'confirmed')
              OR (lifecycle_state = 'secure-store-unavailable' AND verification_state = 'unavailable'
                AND recovery_disposition = 'unavailable' AND cleanup_disposition IN ('not-requested','failed'))
              OR (lifecycle_state = 'recovery-required' AND verification_state = 'unavailable'
                AND recovery_disposition = 'required' AND cleanup_disposition IN ('not-requested','failed')))
        ) STRICT;
        CREATE TRIGGER provider_profile_projection_exact_root_insert_guard
        BEFORE INSERT ON provider_profile_projection
        BEGIN
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_generations g
            WHERE g.profile_id = NEW.profile_id AND g.generation_id = NEW.generation_id
              AND g.revocation_epoch <= NEW.revocation_epoch)
            THEN RAISE(ABORT, 'provider profile projection generation root mismatch') END;
          SELECT CASE WHEN NEW.intent_id IS NOT NULL AND NOT EXISTS(
            SELECT 1 FROM provider_credential_intents i
            WHERE i.intent_id = NEW.intent_id AND i.profile_id = NEW.profile_id
              AND i.generation_id = NEW.generation_id
               AND ((i.intent_state IN ('completed','failed','cancelled','unavailable') AND i.outcome_lifecycle_state = NEW.lifecycle_state)
                OR (i.intent_state = 'pending' AND i.to_lifecycle_state = NEW.lifecycle_state
                  AND NEW.lifecycle_state IN ('pending-enrollment','delete-pending')))
              AND i.verification_state = NEW.verification_state
              AND i.recovery_disposition = NEW.recovery_disposition
              AND i.cleanup_disposition = NEW.cleanup_disposition
              AND i.account_identity_id IS NEW.account_identity_id
              AND i.billing_scope_identity_id IS NEW.billing_scope_identity_id
              AND i.capability_snapshot_id IS NEW.capability_snapshot_id
              AND EXISTS(SELECT 1 FROM provider_credential_intent_events current_event
                WHERE current_event.intent_id = i.intent_id
                  AND current_event.created_at <= NEW.updated_at
                  AND ((i.intent_state = 'pending' AND current_event.event_version = 1
                    AND current_event.prior_intent_event_id IS NULL)
                    OR (i.intent_state IN ('completed','failed','cancelled','unavailable')
                      AND current_event.event_version = 2
                      AND EXISTS(SELECT 1 FROM provider_credential_intent_events prior_event
                        WHERE prior_event.intent_event_id = current_event.prior_intent_event_id
                          AND prior_event.intent_root_id = current_event.intent_root_id
                          AND prior_event.event_version = 1)))
                  AND NOT EXISTS(SELECT 1 FROM provider_credential_intent_events later_event
                    WHERE later_event.intent_root_id = current_event.intent_root_id
                      AND later_event.event_version > current_event.event_version)
                  AND NOT EXISTS(
                    SELECT 1 FROM provider_credential_intent_events newer_event
                    JOIN provider_credential_intents newer_intent ON newer_intent.intent_id = newer_event.intent_id
                    WHERE newer_intent.profile_id = i.profile_id
                      AND newer_event.created_at <= NEW.updated_at
                      AND newer_event.created_at > current_event.created_at)))
            THEN RAISE(ABORT, 'provider profile projection intent root mismatch') END;
          SELECT CASE WHEN NEW.lifecycle_state = 'deleted' AND NOT EXISTS(
            SELECT 1 FROM provider_credential_intents terminal_intent
            JOIN provider_credential_intent_events terminal_event
              ON terminal_event.intent_id = terminal_intent.intent_id
             AND terminal_event.event_version = 2
            JOIN provider_credential_intent_events pending_event
              ON pending_event.intent_event_id = terminal_event.prior_intent_event_id
             AND pending_event.intent_root_id = terminal_event.intent_root_id
             AND pending_event.event_version = 1
            JOIN provider_credential_intents pending_intent
              ON pending_intent.intent_id = pending_event.intent_id
            WHERE terminal_intent.profile_id = NEW.profile_id
              AND terminal_intent.generation_id = NEW.generation_id
              AND terminal_intent.intent_kind = 'delete'
              AND terminal_intent.intent_state = 'completed'
              AND terminal_intent.from_lifecycle_state = 'delete-pending'
              AND terminal_intent.to_lifecycle_state = 'deleted'
              AND terminal_intent.outcome_lifecycle_state = 'deleted'
              AND terminal_intent.verification_state = NEW.verification_state
              AND terminal_intent.recovery_disposition = NEW.recovery_disposition
              AND terminal_intent.cleanup_disposition = NEW.cleanup_disposition
              AND terminal_intent.account_identity_id IS NULL
              AND terminal_intent.billing_scope_identity_id IS NULL
              AND terminal_intent.capability_snapshot_id IS NULL
              AND pending_intent.profile_id = terminal_intent.profile_id
              AND pending_intent.generation_id = terminal_intent.generation_id
              AND pending_intent.intent_kind = terminal_intent.intent_kind
              AND terminal_event.created_at <= NEW.updated_at
              AND NOT EXISTS(SELECT 1 FROM provider_credential_intent_events later_event
                WHERE later_event.intent_root_id = terminal_event.intent_root_id
                  AND later_event.event_version > terminal_event.event_version)
              AND NOT EXISTS(
                SELECT 1 FROM provider_credential_intent_events newer_event
                JOIN provider_credential_intents newer_intent ON newer_intent.intent_id = newer_event.intent_id
                WHERE newer_intent.profile_id = terminal_intent.profile_id
                  AND newer_event.created_at <= NEW.updated_at
                  AND newer_event.created_at > terminal_event.created_at))
            THEN RAISE(ABORT, 'deleted provider profile projection requires exact completed delete event chain') END;
          SELECT CASE WHEN NEW.account_identity_id IS NOT NULL AND NOT EXISTS(
            SELECT 1 FROM provider_access_profiles a WHERE a.profile_id = NEW.profile_id
              AND a.account_identity_id = NEW.account_identity_id
              AND a.billing_scope_identity_id = NEW.billing_scope_identity_id)
            THEN RAISE(ABORT, 'provider profile projection account root mismatch') END;
        END;
        CREATE TRIGGER provider_profile_projection_monotonic_update_guard
        BEFORE UPDATE ON provider_profile_projection
        BEGIN
          SELECT CASE WHEN NEW.profile_id <> OLD.profile_id
              OR NEW.projection_version <= OLD.projection_version
              OR NEW.updated_at <= OLD.updated_at
              OR (OLD.lifecycle_state = 'delete-pending' AND NEW.lifecycle_state NOT IN ('delete-pending','deleted')
                AND NOT (NEW.lifecycle_state='active-unverified'
                  AND EXISTS(
                    SELECT 1 FROM provider_credential_intents recovery
                    JOIN provider_generations predecessor
                      ON predecessor.profile_id=OLD.profile_id AND predecessor.generation_id=OLD.generation_id
                    JOIN provider_generations successor
                      ON successor.profile_id=NEW.profile_id AND successor.generation_id=NEW.generation_id
                    WHERE recovery.intent_id=NEW.intent_id
                      AND recovery.profile_id=NEW.profile_id
                      AND recovery.generation_id=NEW.generation_id
                      AND recovery.intent_kind='recover'
                      AND recovery.intent_state='completed'
                      AND recovery.from_lifecycle_state='delete-pending'
                      AND recovery.to_lifecycle_state='active-unverified'
                      AND recovery.outcome_lifecycle_state='active-unverified'
                      AND successor.generation_ordinal=predecessor.generation_ordinal+1)))
              OR (OLD.lifecycle_state = 'deleted' AND NEW.lifecycle_state <> 'deleted')
            THEN RAISE(ABORT, 'provider profile projection must advance monotonically on one exact root') END;
          SELECT CASE WHEN NOT EXISTS(
            SELECT 1 FROM provider_generations g
            WHERE g.profile_id = NEW.profile_id AND g.generation_id = NEW.generation_id
              AND g.revocation_epoch <= NEW.revocation_epoch)
            THEN RAISE(ABORT, 'provider profile projection generation root mismatch') END;
          SELECT CASE WHEN NEW.intent_id IS NOT NULL AND NOT EXISTS(
            SELECT 1 FROM provider_credential_intents i
            WHERE i.intent_id = NEW.intent_id AND i.profile_id = NEW.profile_id
              AND i.generation_id = NEW.generation_id
               AND ((i.intent_state IN ('completed','failed','cancelled','unavailable') AND i.outcome_lifecycle_state = NEW.lifecycle_state)
                OR (i.intent_state = 'pending' AND i.to_lifecycle_state = NEW.lifecycle_state
                  AND NEW.lifecycle_state IN ('pending-enrollment','delete-pending')))
              AND i.verification_state = NEW.verification_state
              AND i.recovery_disposition = NEW.recovery_disposition
              AND i.cleanup_disposition = NEW.cleanup_disposition
              AND i.account_identity_id IS NEW.account_identity_id
              AND i.billing_scope_identity_id IS NEW.billing_scope_identity_id
              AND i.capability_snapshot_id IS NEW.capability_snapshot_id
              AND EXISTS(SELECT 1 FROM provider_credential_intent_events current_event
                WHERE current_event.intent_id = i.intent_id
                  AND current_event.created_at <= NEW.updated_at
                  AND ((i.intent_state = 'pending' AND current_event.event_version = 1
                    AND current_event.prior_intent_event_id IS NULL)
                    OR (i.intent_state IN ('completed','failed','cancelled','unavailable')
                      AND current_event.event_version = 2
                      AND EXISTS(SELECT 1 FROM provider_credential_intent_events prior_event
                        WHERE prior_event.intent_event_id = current_event.prior_intent_event_id
                          AND prior_event.intent_root_id = current_event.intent_root_id
                          AND prior_event.event_version = 1)))
                  AND NOT EXISTS(SELECT 1 FROM provider_credential_intent_events later_event
                    WHERE later_event.intent_root_id = current_event.intent_root_id
                      AND later_event.event_version > current_event.event_version)
                  AND NOT EXISTS(
                    SELECT 1 FROM provider_credential_intent_events newer_event
                    JOIN provider_credential_intents newer_intent ON newer_intent.intent_id = newer_event.intent_id
                    WHERE newer_intent.profile_id = i.profile_id
                      AND newer_event.created_at <= NEW.updated_at
                      AND newer_event.created_at > current_event.created_at)))
            THEN RAISE(ABORT, 'provider profile projection intent root mismatch') END;
          SELECT CASE WHEN NEW.lifecycle_state = 'deleted' AND NOT EXISTS(
            SELECT 1 FROM provider_credential_intents terminal_intent
            JOIN provider_credential_intent_events terminal_event
              ON terminal_event.intent_id = terminal_intent.intent_id
             AND terminal_event.event_version = 2
            JOIN provider_credential_intent_events pending_event
              ON pending_event.intent_event_id = terminal_event.prior_intent_event_id
             AND pending_event.intent_root_id = terminal_event.intent_root_id
             AND pending_event.event_version = 1
            JOIN provider_credential_intents pending_intent
              ON pending_intent.intent_id = pending_event.intent_id
            WHERE terminal_intent.profile_id = NEW.profile_id
              AND terminal_intent.generation_id = NEW.generation_id
              AND terminal_intent.intent_kind = 'delete'
              AND terminal_intent.intent_state = 'completed'
              AND terminal_intent.from_lifecycle_state = 'delete-pending'
              AND terminal_intent.to_lifecycle_state = 'deleted'
              AND terminal_intent.outcome_lifecycle_state = 'deleted'
              AND terminal_intent.verification_state = NEW.verification_state
              AND terminal_intent.recovery_disposition = NEW.recovery_disposition
              AND terminal_intent.cleanup_disposition = NEW.cleanup_disposition
              AND terminal_intent.account_identity_id IS NULL
              AND terminal_intent.billing_scope_identity_id IS NULL
              AND terminal_intent.capability_snapshot_id IS NULL
              AND pending_intent.profile_id = terminal_intent.profile_id
              AND pending_intent.generation_id = terminal_intent.generation_id
              AND pending_intent.intent_kind = terminal_intent.intent_kind
              AND terminal_event.created_at <= NEW.updated_at
              AND NOT EXISTS(SELECT 1 FROM provider_credential_intent_events later_event
                WHERE later_event.intent_root_id = terminal_event.intent_root_id
                  AND later_event.event_version > terminal_event.event_version)
              AND NOT EXISTS(
                SELECT 1 FROM provider_credential_intent_events newer_event
                JOIN provider_credential_intents newer_intent ON newer_intent.intent_id = newer_event.intent_id
                WHERE newer_intent.profile_id = terminal_intent.profile_id
                  AND newer_event.created_at <= NEW.updated_at
                  AND newer_event.created_at > terminal_event.created_at))
            THEN RAISE(ABORT, 'deleted provider profile projection requires exact completed delete event chain') END;
          SELECT CASE WHEN NEW.account_identity_id IS NOT NULL AND NOT EXISTS(
            SELECT 1 FROM provider_access_profiles a WHERE a.profile_id = NEW.profile_id
              AND a.account_identity_id = NEW.account_identity_id
              AND a.billing_scope_identity_id = NEW.billing_scope_identity_id)
            THEN RAISE(ABORT, 'provider profile projection account root mismatch') END;
        END;
        CREATE UNIQUE INDEX idx_provider_active_generation ON provider_profile_projection(generation_id)
          WHERE lifecycle_state IN ('active-unverified','active-verified','replacing');
        CREATE TRIGGER provider_profile_transition_order_guard
        BEFORE INSERT ON provider_credential_intents
        WHEN NEW.from_lifecycle_state <> 'none'
        BEGIN
          SELECT CASE WHEN NEW.intent_kind = 'recover' AND NEW.from_lifecycle_state = 'recovery-required'
              AND NEW.to_lifecycle_state <> 'recovery-required'
              AND EXISTS(SELECT 1 FROM provider_profile_projection p
                WHERE p.profile_id = NEW.profile_id AND p.generation_id = NEW.generation_id
                  AND p.intent_id LIKE 'restore-recovery-%')
            THEN RAISE(ABORT, 'restored credential recovery cannot reactivate the restored generation') END;
          SELECT CASE WHEN NEW.intent_kind = 'replace' AND NEW.from_lifecycle_state IN ('active-unverified','active-verified')
            AND NOT EXISTS(
              SELECT 1 FROM provider_profile_projection p
              JOIN provider_generations predecessor ON predecessor.profile_id = p.profile_id AND predecessor.generation_id = p.generation_id
              JOIN provider_generations successor ON successor.profile_id = NEW.profile_id
                AND successor.generation_ordinal = predecessor.generation_ordinal + 1
              WHERE p.profile_id = NEW.profile_id AND p.lifecycle_state = NEW.from_lifecycle_state
                AND ((NEW.to_lifecycle_state = 'replacing' AND NEW.generation_id = predecessor.generation_id)
                  OR (NEW.to_lifecycle_state <> 'replacing' AND NEW.generation_id = successor.generation_id)))
            THEN RAISE(ABORT, 'provider replacement must bind a fresh successor generation to the exact predecessor root') END;
          SELECT CASE WHEN NEW.intent_kind = 'replace' AND NEW.from_lifecycle_state = 'replacing'
              AND NOT EXISTS(
                SELECT 1 FROM provider_profile_projection p
                JOIN provider_generations predecessor ON predecessor.profile_id=p.profile_id AND predecessor.generation_id=p.generation_id
                JOIN provider_generations successor ON successor.profile_id=NEW.profile_id AND successor.generation_id=NEW.generation_id
                WHERE p.profile_id=NEW.profile_id AND p.lifecycle_state='replacing'
                  AND successor.generation_ordinal=predecessor.generation_ordinal+1)
            THEN RAISE(ABORT, 'provider replacement completion must bind the exact successor to its ineligible predecessor') END;
          SELECT CASE WHEN NEW.intent_kind = 'recover' AND NEW.from_lifecycle_state IN ('secure-store-unavailable','recovery-required')
              AND NEW.generation_id <> (SELECT p.generation_id FROM provider_profile_projection p WHERE p.profile_id = NEW.profile_id)
              AND NOT EXISTS(
                SELECT 1 FROM provider_profile_projection p
                JOIN provider_generations predecessor ON predecessor.profile_id = p.profile_id AND predecessor.generation_id = p.generation_id
                JOIN provider_generations successor ON successor.profile_id = NEW.profile_id AND successor.generation_id = NEW.generation_id
                WHERE p.profile_id = NEW.profile_id AND p.lifecycle_state = NEW.from_lifecycle_state
                  AND successor.generation_ordinal = predecessor.generation_ordinal + 1)
            THEN RAISE(ABORT, 'provider recovery re-entry must bind a fresh successor generation to the exact restored root') END;
          SELECT CASE WHEN NEW.intent_kind = 'recover' AND NEW.from_lifecycle_state = 'delete-pending'
              AND NOT EXISTS(
                SELECT 1 FROM provider_profile_projection p
                JOIN provider_generations predecessor ON predecessor.profile_id=p.profile_id AND predecessor.generation_id=p.generation_id
                JOIN provider_generations successor ON successor.profile_id=NEW.profile_id AND successor.generation_id=NEW.generation_id
                WHERE p.profile_id=NEW.profile_id AND p.lifecycle_state='delete-pending'
                  AND p.cleanup_disposition IN ('pending','failed')
                  AND successor.generation_ordinal=predecessor.generation_ordinal+1
                  AND EXISTS(
                    SELECT 1 FROM provider_credential_intents replacement
                    WHERE replacement.profile_id=p.profile_id AND replacement.generation_id=p.generation_id
                      AND replacement.intent_kind='replace' AND replacement.intent_state='completed'
                      AND replacement.to_lifecycle_state='replacing'
                      AND replacement.outcome_lifecycle_state='replacing'))
            THEN RAISE(ABORT, 'provider replacement cleanup recovery must bind the exact successor to its delete-pending predecessor') END;
          SELECT CASE WHEN NOT (NEW.intent_kind = 'replace' AND NEW.from_lifecycle_state IN ('active-unverified','active-verified'))
              AND NOT (NEW.intent_kind = 'replace' AND NEW.from_lifecycle_state = 'replacing')
              AND NOT (NEW.intent_kind = 'recover' AND NEW.from_lifecycle_state IN ('secure-store-unavailable','recovery-required')
                AND NEW.generation_id <> (SELECT p.generation_id FROM provider_profile_projection p WHERE p.profile_id = NEW.profile_id))
              AND NOT (NEW.intent_kind = 'recover' AND NEW.from_lifecycle_state = 'delete-pending')
              AND NOT EXISTS(
            SELECT 1 FROM provider_profile_projection p
            WHERE p.profile_id = NEW.profile_id AND p.generation_id = NEW.generation_id
              AND p.lifecycle_state = NEW.from_lifecycle_state)
            THEN RAISE(ABORT, 'provider profile transition predecessor mismatch') END;
        END;
        CREATE TRIGGER provider_credential_intent_time_order_guard
        BEFORE INSERT ON provider_credential_intents
        WHEN EXISTS(SELECT 1 FROM provider_credential_intents i WHERE i.profile_id = NEW.profile_id)
        BEGIN
          SELECT CASE WHEN NEW.created_at <= (
            SELECT max(authority_time) FROM (
              SELECT i.created_at AS authority_time
              FROM provider_credential_intents i WHERE i.profile_id = NEW.profile_id
              UNION ALL
              SELECT e.created_at
              FROM provider_credential_intent_events e
              JOIN provider_credential_intents i ON i.intent_id = e.intent_id
              WHERE i.profile_id = NEW.profile_id
              UNION ALL
              SELECT p.updated_at
              FROM provider_profile_projection p WHERE p.profile_id = NEW.profile_id))
            THEN RAISE(ABORT, 'provider credential lifecycle time regression') END;
          SELECT CASE WHEN NEW.from_lifecycle_state <> 'none'
            AND NOT EXISTS(
              SELECT 1 FROM provider_profile_projection p
              JOIN provider_credential_intents projected_intent ON projected_intent.intent_id = p.intent_id
              JOIN provider_credential_intent_events projected_event ON projected_event.intent_id = projected_intent.intent_id
              WHERE p.profile_id = NEW.profile_id
                AND projected_event.created_at <= p.updated_at
                AND NOT EXISTS(
                  SELECT 1 FROM provider_credential_intent_events later_event
                  JOIN provider_credential_intents later_intent ON later_intent.intent_id = later_event.intent_id
                  WHERE later_intent.profile_id = NEW.profile_id
                    AND later_event.created_at > projected_event.created_at))
            AND NOT (NEW.intent_state IN ('completed','failed','cancelled','unavailable') AND EXISTS(
              SELECT 1 FROM provider_credential_intent_events pending_event
              JOIN provider_credential_intents pending_intent ON pending_intent.intent_id = pending_event.intent_id
              WHERE pending_intent.profile_id = NEW.profile_id
                AND pending_intent.generation_id = NEW.generation_id
                AND pending_intent.intent_kind = NEW.intent_kind
                AND pending_intent.intent_state = 'pending'
                AND pending_intent.from_lifecycle_state = NEW.from_lifecycle_state
                AND pending_intent.to_lifecycle_state = NEW.to_lifecycle_state
                AND NOT EXISTS(
                  SELECT 1 FROM provider_credential_intent_events later_event
                  JOIN provider_credential_intents later_intent ON later_intent.intent_id = later_event.intent_id
                  WHERE later_intent.profile_id = NEW.profile_id
                    AND later_event.created_at > pending_event.created_at)))
            AND NOT EXISTS(
              SELECT 1 FROM provider_credential_intent_events terminal_event
              JOIN provider_credential_intents terminal_intent ON terminal_intent.intent_id = terminal_event.intent_id
              WHERE terminal_intent.profile_id = NEW.profile_id
                AND terminal_intent.intent_state IN ('completed','failed','cancelled','unavailable')
                AND NOT EXISTS(
                  SELECT 1 FROM provider_credential_intent_events later_event
                  JOIN provider_credential_intents later_intent ON later_intent.intent_id = later_event.intent_id
                  WHERE later_intent.profile_id = NEW.profile_id
                    AND later_event.created_at > terminal_event.created_at))
            THEN RAISE(ABORT, 'provider credential successor requires projection of the exact latest durable event') END;
        END;
        CREATE TRIGGER provider_delete_pending_never_reactivates_guard
        BEFORE INSERT ON provider_credential_intents
        WHEN EXISTS(
          SELECT 1 FROM provider_credential_intents prior
          WHERE prior.profile_id = NEW.profile_id AND prior.intent_kind = 'delete'
            AND ((prior.intent_state = 'pending' AND prior.to_lifecycle_state = 'delete-pending'
              AND EXISTS(
                SELECT 1 FROM provider_credential_intent_events pending_event
                WHERE pending_event.intent_id = prior.intent_id
                  AND NOT EXISTS(
                    SELECT 1 FROM provider_credential_intent_events terminal_event
                    JOIN provider_credential_intents terminal_intent
                      ON terminal_intent.intent_id = terminal_event.intent_id
                    WHERE terminal_event.intent_root_id = pending_event.intent_root_id
                      AND terminal_event.event_version > pending_event.event_version
                      AND terminal_intent.intent_state = 'cancelled')))
              OR (prior.intent_state IN ('completed','failed','unavailable')
                AND prior.outcome_lifecycle_state = 'delete-pending'))
            AND NOT EXISTS(
              SELECT 1 FROM provider_credential_intents cleanup_recovery
              JOIN provider_credential_intent_events cleanup_event
                ON cleanup_event.intent_id=cleanup_recovery.intent_id
              JOIN provider_generations predecessor
                ON predecessor.profile_id=prior.profile_id AND predecessor.generation_id=prior.generation_id
              JOIN provider_generations successor
                ON successor.profile_id=cleanup_recovery.profile_id AND successor.generation_id=cleanup_recovery.generation_id
              WHERE cleanup_recovery.profile_id=prior.profile_id
                AND cleanup_recovery.intent_kind='recover'
                AND cleanup_recovery.intent_state='completed'
                AND cleanup_recovery.from_lifecycle_state='delete-pending'
                AND cleanup_recovery.to_lifecycle_state='active-unverified'
                AND cleanup_recovery.outcome_lifecycle_state='active-unverified'
                AND successor.generation_ordinal=predecessor.generation_ordinal+1
                AND cleanup_event.created_at > prior.created_at))
        BEGIN
          SELECT CASE WHEN NOT (
              (NEW.intent_kind = 'delete'
                AND NEW.from_lifecycle_state = 'delete-pending'
                AND NEW.to_lifecycle_state IN ('delete-pending','deleted'))
              OR (NEW.intent_kind = 'recover'
                AND NEW.from_lifecycle_state = 'delete-pending'
                AND NEW.to_lifecycle_state = 'active-unverified'
                AND EXISTS(
                  SELECT 1 FROM provider_profile_projection projection
                  JOIN provider_generations predecessor
                    ON predecessor.profile_id=projection.profile_id AND predecessor.generation_id=projection.generation_id
                  JOIN provider_generations successor
                    ON successor.profile_id=NEW.profile_id AND successor.generation_id=NEW.generation_id
                  WHERE projection.profile_id=NEW.profile_id
                    AND projection.lifecycle_state='delete-pending'
                    AND projection.cleanup_disposition IN ('pending','failed')
                    AND successor.generation_ordinal=predecessor.generation_ordinal+1
                    AND EXISTS(
                      SELECT 1 FROM provider_credential_intents replacement
                      WHERE replacement.profile_id=projection.profile_id
                        AND replacement.generation_id=projection.generation_id
                        AND replacement.intent_kind='replace'
                        AND replacement.intent_state='completed'
                        AND replacement.to_lifecycle_state='replacing'
                        AND replacement.outcome_lifecycle_state='replacing')))
              OR (NEW.intent_kind = 'delete' AND NEW.intent_state = 'cancelled' AND EXISTS(
                SELECT 1 FROM provider_credential_intents pending
                WHERE pending.profile_id = NEW.profile_id
                  AND pending.generation_id = NEW.generation_id
                  AND pending.intent_kind = 'delete'
                  AND pending.intent_state = 'pending'
                  AND pending.from_lifecycle_state = NEW.from_lifecycle_state
                  AND pending.to_lifecycle_state = NEW.to_lifecycle_state)))
            THEN RAISE(ABORT, 'delete-pending provider profile cannot reactivate') END;
        END;
        CREATE TABLE provider_budget_projection(
            scope_kind TEXT NOT NULL,
            scope_id TEXT NOT NULL,
            reserved_nano_usd INTEGER NOT NULL CHECK(reserved_nano_usd >= 0),
            settled_nano_usd INTEGER NOT NULL CHECK(settled_nano_usd >= 0),
            unresolved_nano_usd INTEGER NOT NULL CHECK(unresolved_nano_usd >= 0),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL,
            PRIMARY KEY(scope_kind,scope_id),
            CHECK(reserved_nano_usd = 0 AND settled_nano_usd = 0 AND unresolved_nano_usd = 0)
        ) STRICT;
        CREATE TRIGGER provider_budget_projection_monotonic_update_guard
        BEFORE UPDATE ON provider_budget_projection
        WHEN NEW.scope_kind <> OLD.scope_kind OR NEW.scope_id <> OLD.scope_id
          OR NEW.projection_version <= OLD.projection_version
          OR NEW.updated_at <= OLD.updated_at
        BEGIN SELECT RAISE(ABORT, 'provider budget projection must advance monotonically on one exact root'); END;
        CREATE TRIGGER provider_budget_projection_authority_guard
        BEFORE INSERT ON provider_budget_projection
        BEGIN
          SELECT RAISE(ABORT, 'provider budget projection unavailable before accepted provider dispatch authority');
        END;
        """;
}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
