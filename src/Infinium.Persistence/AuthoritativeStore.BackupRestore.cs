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
    public BackupArtifact CreateBackup(string label, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        string safeLabel = string.Concat(label.Where(char.IsLetterOrDigit));
        if (safeLabel.Length is 0 or > 48)
        {
            throw new ArgumentException("The backup label must contain 1-48 letters or digits.", nameof(label));
        }

        lock (gate)
        {
            string stamp = now.UtcDateTime.ToString(
                "yyyyMMddTHHmmssfffZ",
                System.Globalization.CultureInfo.InvariantCulture);
            string backupDatabaseName = $"{stamp}-{safeLabel}.sqlite3";
            string databasePath = Paths.ResolveProductPath(
                ProductWriteClass.Backup,
                backupDatabaseName);
            bool reservationCreated = false;
            try
            {
                using (FileStream reservation = Paths.CreateNewFile(
                           ProductWriteClass.Backup,
                           backupDatabaseName))
                {
                    reservationCreated = true;
                    reservation.Flush(flushToDisk: true);
                }

                using (WindowsGuardedSqliteVfs backupVfs = new(
                           Paths,
                           ProductWriteClass.Backup,
                           backupDatabaseName))
                {
                    using SqliteConnection destination = new(
                        new SqliteConnectionStringBuilder
                        {
                            DataSource = databasePath,
                            Mode = SqliteOpenMode.ReadWrite,
                            Pooling = false,
                            Vfs = backupVfs.Name,
                        }.ToString());
                    destination.Open();
                    sqliteVfs.VerifyAllGuards();
                    connection.BackupDatabase(destination);
                    backupVfs.VerifyAllGuards();
                }

                string databaseSha;
                using (FileStream database = Paths.OpenReadFile(
                           ProductWriteClass.Backup,
                           backupDatabaseName))
                {
                    databaseSha = HashStream(database);
                }

                List<BackupPayloadManifest> payloads = [];
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        """
                        SELECT content_sha256, byte_length, object_relative_path
                        FROM payloads
                        WHERE retention_state = 'retained'
                        ORDER BY content_sha256;
                        """;
                    using SqliteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string sha256 = reader.GetString(0);
                        long byteLength = reader.GetInt64(1);
                        string relativePath = reader.GetString(2);
                        string payloadRelative = relativePath["payloads/".Length..]
                            .Replace('/', Path.DirectorySeparatorChar);
                        Paths.CopyFile(
                            ProductWriteClass.Payload,
                            payloadRelative,
                            ProductWriteClass.Backup,
                            Path.Combine(
                                backupDatabaseName + ".payloads",
                                payloadRelative),
                            byteLength,
                            sha256);
                        payloads.Add(new BackupPayloadManifest(
                            sha256,
                            byteLength,
                            relativePath));
                    }
                }

                string manifestPath = Paths.ResolveProductPath(
                    ProductWriteClass.Backup,
                    backupDatabaseName + ".manifest.json");
                byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(
                    new BackupManifest(
                        CurrentSchemaVersion,
                        BindingIdentity,
                        databaseSha,
                        payloads,
                        now),
                    new JsonSerializerOptions { WriteIndented = true });
                if (manifest.Length is 0 or > MaximumBackupManifestBytes)
                {
                    throw new InvalidOperationException(
                        "The backup manifest exceeds its finite bound.");
                }

                using (FileStream manifestStream = Paths.CreateNewFile(
                           ProductWriteClass.Backup,
                           backupDatabaseName + ".manifest.json"))
                {
                    manifestStream.Write(manifest);
                    manifestStream.Flush(flushToDisk: true);
                }

                BackupArtifact artifact =
                    new(databasePath, manifestPath, databaseSha);
                _ = ValidateBackup(artifact);
                using (SqliteTransaction transaction = BeginTransaction())
                {
                    Execute(
                        """
                        INSERT INTO payload_backup_pins(
                            payload_id, backup_identity, content_sha256, created_at)
                        SELECT payload_id, $backup, content_sha256, $now
                        FROM payloads
                        WHERE retention_state = 'retained';
                        """,
                        transaction,
                        ("$backup", backupDatabaseName),
                        ("$now", ToText(now)));
                    InsertAuditEvent(
                        "backup-created",
                        "backup",
                        Path.GetFileName(databasePath),
                        now,
                        transaction);
                    transaction.Commit();
                }

                return artifact;
            }
            catch (Exception backupException) when (reservationCreated)
            {
                try
                {
                    CleanupFailedBackup(backupDatabaseName);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "Backup creation failed and its partial bundle could not be removed.",
                        backupException,
                        cleanupException);
                }

                throw;
            }
        }
    }

    private void CleanupFailedBackup(string backupDatabaseName)
    {
        Paths.DeleteFile(
            ProductWriteClass.Backup,
            backupDatabaseName + ".manifest.json",
            missingIsSuccess: true);
        Paths.DeleteDirectoryTree(
            ProductWriteClass.Backup,
            backupDatabaseName + ".payloads",
            missingIsSuccess: true);
        foreach (string suffix in new[] { "-journal", "-shm", "-wal", string.Empty })
        {
            Paths.DeleteFile(
                ProductWriteClass.Backup,
                backupDatabaseName + suffix,
                missingIsSuccess: true);
        }
    }

    public static void RestoreBackup(BackupArtifact backup, StoragePaths target)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(target);
        if (Directory.Exists(target.ProductRoot) || File.Exists(target.ProductRoot))
        {
            throw new InvalidOperationException("Restore requires an absent target product root.");
        }

        ValidatedBackup validated = ValidateBackup(backup);
        string? targetParent = Directory.GetParent(target.ProductRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(targetParent))
        {
            throw new InvalidOperationException("The restore target must have a parent directory.");
        }
        if (!Directory.Exists(targetParent))
        {
            throw new InvalidOperationException(
                "The restore target parent must already exist and be selected explicitly.");
        }
        StoragePaths staging = target.CreateRestoreStagingPaths();
        bool published = false;
        try
        {
            staging.Create();
            staging.CopyExternalFileIntoProduct(
                ProductWriteClass.Data,
                "infinium.sqlite3",
                backup.DatabasePath,
                validated.DatabaseByteLength,
                validated.Manifest.DatabaseSha256);
            string backupPayloadRoot = backup.DatabasePath + ".payloads";
            foreach (BackupPayloadManifest payload in validated.Manifest.Payloads)
            {
                string source = PayloadPath(backupPayloadRoot, payload.Sha256);
                staging.CopyExternalFileIntoProduct(
                    ProductWriteClass.Payload,
                    payload.RelativePath["payloads/".Length..]
                        .Replace('/', Path.DirectorySeparatorChar),
                    source,
                    payload.ByteLength,
                    payload.Sha256);
            }

            using (FileStream stagedDatabase = staging.OpenReadFile(
                       ProductWriteClass.Data,
                       "infinium.sqlite3"))
            {
                if (!string.Equals(
                        HashStream(stagedDatabase),
                        validated.Manifest.DatabaseSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The staged restore database fingerprint is invalid.");
                }
            }

            List<PublicationFileExpectation> expectedFiles;
            using (FileStream stagedDatabase = AppendRestoreAudit(
                       staging,
                       target.AuthorityIdentity,
                       DateTimeOffset.UtcNow))
            {
                long stagedDatabaseLength = stagedDatabase.Length;
                string stagedDatabaseSha256 = HashStream(stagedDatabase);
                IReadOnlyList<BackupPayloadManifest> stagedPayloads =
                    ValidateDatabaseFile(
                        staging.Database,
                        validated.Manifest.Sqlite);
                ValidateManifestPayloadSet(
                    validated.Manifest.Payloads,
                    stagedPayloads);
                ValidatePayloadFiles(
                    staging.Payloads,
                    validated.Manifest.Payloads);

                expectedFiles =
                [
                    new(
                        Path.Combine("data", "infinium.sqlite3"),
                        stagedDatabaseLength,
                        stagedDatabaseSha256),
                ];
                expectedFiles.AddRange(validated.Manifest.Payloads.Select(payload =>
                    new PublicationFileExpectation(
                        payload.RelativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar),
                        payload.ByteLength,
                        payload.Sha256)));
            }

            target.PublishFrom(staging, expectedFiles);
            published = true;
            using AuthoritativeStore restored = new(new StoragePaths(target.ProductRoot));
            _ = restored.MarkRestoredCredentialsRecoveryRequired(DateTimeOffset.UtcNow);
        }
        finally
        {
            if (!published && staging.HasBoundProductRoot)
            {
                staging.DeleteProductTree();
            }

            staging.Dispose();
        }
    }

    private static ValidatedBackup ValidateBackup(BackupArtifact backup)
    {
        if (!File.Exists(backup.DatabasePath) || !File.Exists(backup.ManifestPath))
        {
            throw new InvalidOperationException("The backup database or manifest is missing.");
        }

        BackupManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(
                ReadBoundedFile(
                    backup.ManifestPath,
                    MaximumBackupManifestBytes,
                    "backup manifest"),
                new JsonSerializerOptions { MaxDepth = 32 })
                ?? throw new InvalidOperationException("The backup manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The backup manifest is malformed.", exception);
        }

        if (manifest.SchemaVersion != CurrentSchemaVersion
            || manifest.Sqlite is null
            || manifest.Sqlite.CompileOptions is null
            || manifest.Payloads is null)
        {
            throw new InvalidOperationException(
                "The backup manifest schema or SQLite identity is incompatible.");
        }

        ValidateManifestBinding(manifest.Sqlite);
        if (!IsCanonicalSha256(manifest.DatabaseSha256)
            || !IsCanonicalSha256(backup.Sha256))
        {
            throw new InvalidOperationException(
                "The backup database fingerprint is not canonically encoded.");
        }

        string actualDatabaseSha;
        long databaseByteLength;
        using (FileStream database = new(
                   backup.DatabasePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            databaseByteLength = database.Length;
            actualDatabaseSha = HashStream(database);
        }

        if (!string.Equals(actualDatabaseSha, backup.Sha256, StringComparison.Ordinal)
            || !string.Equals(
                actualDatabaseSha,
                manifest.DatabaseSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup database fingerprint is invalid.");
        }

        IReadOnlyList<BackupPayloadManifest> databasePayloads =
            ValidateDatabaseFile(backup.DatabasePath, manifest.Sqlite);
        ValidateManifestPayloadSet(manifest.Payloads, databasePayloads);
        ValidatePayloadFiles(backup.DatabasePath + ".payloads", manifest.Payloads);
        return new ValidatedBackup(manifest, databaseByteLength);
    }

    private static List<BackupPayloadManifest> ValidateDatabaseFile(
        string databasePath,
        SqliteBindingIdentity expectedBinding)
    {
        SqliteRuntimeIdentity.InitializeNativeProvider();
        using var database = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());
        try
        {
            database.Open();
            SqliteBindingIdentity actualBinding =
                SqliteRuntimeIdentity.VerifyExactPatchedBinding(database);
            if (!BindingEquals(actualBinding, expectedBinding))
            {
                throw new InvalidOperationException(
                    "The backup SQLite binding identity does not match the current runtime.");
            }

            ValidateDatabaseIdentityAndIntegrity(database, actualBinding);
            return ReadDatabasePayloads(database);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                "The backup database failed SQLite validation.",
                exception);
        }
    }

    private static void ValidateDatabaseIdentityAndIntegrity(
        SqliteConnection database,
        SqliteBindingIdentity binding)
    {
        using (var integrity = database.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            using SqliteDataReader reader = integrity.ExecuteReader();
            if (!reader.Read()
                || !string.Equals(reader.GetString(0), "ok", StringComparison.Ordinal)
                || reader.Read())
            {
                throw new InvalidOperationException("The database failed SQLite integrity validation.");
            }
        }

        using (var foreignKeys = database.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_key_check;";
            using SqliteDataReader reader = foreignKeys.ExecuteReader();
            if (reader.Read())
            {
                throw new InvalidOperationException("The database failed foreign-key validation.");
            }
        }

        using (var version = database.CreateCommand())
        {
            version.CommandText = "PRAGMA user_version;";
            if (Convert.ToInt32(
                    version.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture)
                != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The database user-version does not match the supported schema.");
            }
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (var command = database.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value
                FROM store_metadata
                WHERE key IN (
                    'schema_version',
                    'schema_fingerprint',
                    'storage_contract_version',
                    'sqlite_version',
                    'sqlite_source_id'
                );
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        if (metadata.Count != 5
            || metadata["schema_version"]
                != CurrentSchemaVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            || metadata["storage_contract_version"] != CurrentStorageContractVersion
            || metadata["sqlite_version"] != binding.Version
            || metadata["sqlite_source_id"] != binding.SourceId
            || metadata["schema_fingerprint"] != ComputeSchemaFingerprint(database))
        {
            throw new InvalidOperationException(
                "The database storage contract or SQLite identity metadata is invalid.");
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S2-0001'
                  AND from_version = 0
                  AND to_version = 1
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The database migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                "SELECT COUNT(*) FROM migration_history "
                + "WHERE migration_id='M1-S6-SUCCESSOR-0007' AND from_version=6 AND to_version=7 "
                + "AND sqlite_source_id=$source;";
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The provider attempt-identity storage migration is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                "SELECT COUNT(*) FROM migration_history "
                + "WHERE migration_id='M1-S7-WP5-0010' AND from_version=9 AND to_version=10 "
                + "AND sqlite_source_id=$source;";
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The scope-reversion persistence migration is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                "SELECT COUNT(*) FROM migration_history "
                + "WHERE migration_id='M1-S6-SUCCESSOR-V6-0008' AND from_version=7 AND to_version=8 "
                + "AND sqlite_source_id=$source;";
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The extended provider-operation persistence migration is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S3-0002'
                  AND from_version = 1
                  AND to_version = 2
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The snapshot-capture storage migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S4-0003'
                  AND from_version = 2
                  AND to_version = 3
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The durable run-operation storage migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S5-0004'
                  AND from_version = 3
                  AND to_version = 4
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The analysis pipeline analytical storage migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S5-WP4-0005'
                  AND from_version = 4
                  AND to_version = 5
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The finding/case completion migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S6-0006'
                  AND from_version = 5
                  AND to_version = 6
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The provider-operation storage migration identity is invalid.");
            }
        }

        HashSet<string> actualObjects = new(StringComparer.Ordinal);
        using (var schema = database.CreateCommand())
        {
            schema.CommandText =
                """
                SELECT type || ':' || name
                FROM sqlite_schema
                WHERE name NOT LIKE 'sqlite_%'
                ORDER BY type, name;
                """;
            using SqliteDataReader reader = schema.ExecuteReader();
            while (reader.Read())
            {
                actualObjects.Add(reader.GetString(0));
            }
        }

        if (!actualObjects.SetEquals(RequiredSchemaObjects))
        {
            string missing = string.Join(",", RequiredSchemaObjects.Except(actualObjects).Order(StringComparer.Ordinal));
            string unexpected = string.Join(",", actualObjects.Except(RequiredSchemaObjects).Order(StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"The database schema objects do not match the supported storage contract. Missing=[{missing}] Unexpected=[{unexpected}]");
        }
    }

    private static string ComputeSchemaFingerprint(
        SqliteConnection database,
        SqliteTransaction? transaction = null)
    {
        using SqliteCommand schema = database.CreateCommand();
        schema.Transaction = transaction;
        schema.CommandText =
            """
            SELECT type, name, tbl_name, COALESCE(sql, '')
            FROM sqlite_schema
            WHERE name NOT LIKE 'sqlite_%'
            ORDER BY type, name;
            """;
        var canonical = new StringBuilder();
        using SqliteDataReader reader = schema.ExecuteReader();
        while (reader.Read())
        {
            canonical
                .Append(reader.GetString(0)).Append('\u001f')
                .Append(reader.GetString(1)).Append('\u001f')
                .Append(reader.GetString(2)).Append('\u001f')
                .Append(reader.GetString(3)).Append('\n');
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static List<BackupPayloadManifest> ReadDatabasePayloads(
        SqliteConnection database)
    {
        List<BackupPayloadManifest> payloads = [];
        using var command = database.CreateCommand();
        command.CommandText =
            """
            SELECT content_sha256, byte_length, object_relative_path
            FROM payloads
            WHERE retention_state = 'retained'
            ORDER BY content_sha256;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            payloads.Add(new BackupPayloadManifest(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetString(2)));
        }

        return payloads;
    }

    private static void ValidateManifestPayloadSet(
        IReadOnlyList<BackupPayloadManifest> manifestPayloads,
        IReadOnlyList<BackupPayloadManifest> databasePayloads)
    {
        Dictionary<string, BackupPayloadManifest> manifestBySha =
            new(StringComparer.Ordinal);
        foreach (BackupPayloadManifest payload in manifestPayloads)
        {
            if (!IsCanonicalSha256(payload.Sha256)
                || payload.ByteLength < 0
                || !string.Equals(
                    payload.RelativePath,
                    CanonicalPayloadRelativePath(payload.Sha256),
                    StringComparison.Ordinal)
                || !manifestBySha.TryAdd(payload.Sha256, payload))
            {
                throw new InvalidOperationException(
                    "The backup payload manifest contains an invalid or duplicate entry.");
            }
        }

        if (manifestBySha.Count != databasePayloads.Count)
        {
            throw new InvalidOperationException(
                "The backup payload manifest is incomplete or contains extra entries.");
        }

        foreach (BackupPayloadManifest databasePayload in databasePayloads)
        {
            if (!IsCanonicalSha256(databasePayload.Sha256)
                || databasePayload.ByteLength < 0
                || !string.Equals(
                    databasePayload.RelativePath,
                    CanonicalPayloadRelativePath(databasePayload.Sha256),
                    StringComparison.Ordinal)
                || !manifestBySha.TryGetValue(
                    databasePayload.Sha256,
                    out BackupPayloadManifest? manifestPayload)
                || manifestPayload.ByteLength != databasePayload.ByteLength
                || !string.Equals(
                    manifestPayload.RelativePath,
                    databasePayload.RelativePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The backup payload manifest does not match the database payload registry.");
            }
        }
    }

    private static void ValidatePayloadFiles(
        string payloadRoot,
        IReadOnlyList<BackupPayloadManifest> payloads)
    {
        foreach (BackupPayloadManifest payload in payloads)
        {
            string path = PayloadPath(payloadRoot, payload.Sha256);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("A referenced backup payload is missing.");
            }

            using FileStream file = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            if (file.Length != payload.ByteLength
                || !string.Equals(HashStream(file), payload.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A referenced backup payload has an invalid length or fingerprint.");
            }
        }
    }

    private static string PayloadPath(string payloadRoot, string sha256) =>
        Path.Combine(payloadRoot, sha256[..2], sha256[2..4], sha256);

    private static string CanonicalPayloadRelativePath(string sha256) =>
        $"payloads/{sha256[..2]}/{sha256[2..4]}/{sha256}";

    private static void ValidateManifestBinding(SqliteBindingIdentity binding)
    {
        SqliteBindingIdentity required = new(
            SqliteRuntimeIdentity.RequiredVersion,
            SqliteRuntimeIdentity.RequiredSourceId,
            SqliteRuntimeIdentity.RequiredWinX64NativeSha256,
            binding.CompileOptions);
        if (!BindingEquals(binding, required)
            || !binding.CompileOptions.Contains("THREADSAFE=1", StringComparer.Ordinal)
            || !binding.CompileOptions.Contains(
                "DEFAULT_WAL_SYNCHRONOUS=2",
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The backup manifest SQLite identity is incompatible.");
        }
    }

    private static bool BindingEquals(
        SqliteBindingIdentity first,
        SqliteBindingIdentity second) =>
        string.Equals(first.Version, second.Version, StringComparison.Ordinal)
        && string.Equals(first.SourceId, second.SourceId, StringComparison.Ordinal)
        && string.Equals(first.NativeSha256, second.NativeSha256, StringComparison.Ordinal)
        && first.CompileOptions.SequenceEqual(second.CompileOptions, StringComparer.Ordinal);

    private static bool IsCanonicalSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static FileStream AppendRestoreAudit(
        StoragePaths paths,
        string authorityIdentity,
        DateTimeOffset now)
    {
        WindowsGuardedSqliteVfs restoredVfs = new(
            paths,
            ProductWriteClass.Data,
            "infinium.sqlite3");
        SqliteConnection restored = new(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            Vfs = restoredVfs.Name,
        }.ToString());
        FileStream? pinnedDatabase = null;
        try
        {
            restored.Open();
            ConfigureConnection(restored);
            using (SqliteTransaction transaction = restored.BeginTransaction())
            using (SqliteCommand command = restored.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO audit_events(
                        audit_event_id, event_kind, object_kind, object_id,
                        detail_payload_id, occurred_at)
                    VALUES ($id, 'restore-completed', 'product-root', $object, NULL, $now);
                    """;
                command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
                command.Parameters.AddWithValue("$object", authorityIdentity);
                command.Parameters.AddWithValue("$now", ToText(now));
                command.ExecuteNonQuery();
                transaction.Commit();
            }

            using (SqliteCommand checkpoint = restored.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                using SqliteDataReader result = checkpoint.ExecuteReader();
                if (!result.Read() || result.GetInt32(0) != 0)
                {
                    throw new InvalidOperationException(
                        "The restored database WAL could not be checkpointed.");
                }
            }

            using (SqliteCommand journalMode = restored.CreateCommand())
            {
                journalMode.CommandText = "PRAGMA journal_mode = DELETE;";
                if (!string.Equals(
                        Convert.ToString(
                            journalMode.ExecuteScalar(),
                            System.Globalization.CultureInfo.InvariantCulture),
                        "delete",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The restored database could not be normalized to one main file.");
                }
            }

            restoredVfs.VerifyAllGuards();
            restored.Dispose();
            pinnedDatabase = paths.OpenReadFile(
                ProductWriteClass.Data,
                "infinium.sqlite3");
            restoredVfs.VerifyAllGuards();
            restoredVfs.Dispose();
            foreach (string suffix in new[] { "-journal", "-shm", "-wal" })
            {
                paths.DeleteFile(
                    ProductWriteClass.Data,
                    "infinium.sqlite3" + suffix,
                    missingIsSuccess: true);
            }

            FileStream resultStream = pinnedDatabase;
            pinnedDatabase = null;
            return resultStream;
        }
        finally
        {
            pinnedDatabase?.Dispose();
            restored.Dispose();
            restoredVfs.Dispose();
        }
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashStream(Stream stream) =>
        Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

    private static byte[] ReadBoundedFile(
        string path,
        int maximumBytes,
        string description)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximumBytes)
        {
            throw new InvalidOperationException(
                $"The {description} exceeds its finite bound.");
        }

        byte[] buffer = new byte[maximumBytes + 1];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total is 0 || total > maximumBytes)
        {
            throw new InvalidOperationException(
                $"The {description} exceeds its finite bound.");
        }

        return buffer[..total];
    }

    private sealed record BackupManifest(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("sqlite")] SqliteBindingIdentity Sqlite,
        [property: JsonPropertyName("databaseSha256")] string DatabaseSha256,
        [property: JsonPropertyName("payloads")] IReadOnlyList<BackupPayloadManifest> Payloads,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

    private sealed record BackupPayloadManifest(
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("byteLength")] long ByteLength,
        [property: JsonPropertyName("relativePath")] string RelativePath);

    private sealed record ValidatedBackup(
        BackupManifest Manifest,
        long DatabaseByteLength);

}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
