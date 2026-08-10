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


public sealed partial class AuthoritativeStore : IDisposable
{
    public const int CurrentSchemaVersion = 5;
    public const string CurrentStorageContractVersion = "1.4.0";
    private const string SchemaV3Fingerprint =
        "02fed67fa5dac6c28ec2a9f477733edc9f12eaa03a08f9d7dec05b502e45d6cf";
    private const int MaximumBackupManifestBytes = 16 * 1024 * 1024;
    private const int MaximumCheckpointJsonBytes = 64 * 1024;
    private static readonly JsonSerializerOptions DocumentationPayloadJsonOptions =
        CreateDocumentationPayloadJsonOptions();

    private readonly Lock gate = new();
    private readonly SqliteConnection connection;
    private readonly WindowsGuardedSqliteVfs sqliteVfs;
    private bool disposed;

    public AuthoritativeStore(StoragePaths paths)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        try
        {
            Paths.Create();
            _ = Paths.ResolveProductPath(ProductWriteClass.Data, "infinium.sqlite3");
            SqliteRuntimeIdentity.InitializeNativeProvider();
            sqliteVfs = new WindowsGuardedSqliteVfs(
                Paths,
                ProductWriteClass.Data,
                "infinium.sqlite3");
        }
        catch
        {
            Paths.Dispose();
            throw;
        }

        try
        {
            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = Paths.Database,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                Vfs = sqliteVfs.Name,
            }.ToString());
            connection.Open();
            BindingIdentity = SqliteRuntimeIdentity.VerifyExactPatchedBinding(connection);
            ConfigureConnection(connection);
            WindowsGuardedSqliteVfs.EnablePersistentWal(connection);
            ApplyMigrations();
            ValidateDatabaseIdentityAndIntegrity(connection, BindingIdentity);
            sqliteVfs.VerifyAllGuards();
            RecordWriteClassAuthorityBindings(DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            connection?.Dispose();
            Exception? callbackError = sqliteVfs.LastCallbackError;
            string? callbackDetail = sqliteVfs.LastCallbackDetail;
            sqliteVfs.Dispose();
            Paths.Dispose();
            if (callbackError is not null)
            {
                throw new InvalidOperationException(
                    "The guarded SQLite VFS rejected a database operation.",
                    callbackError);
            }

            if (callbackDetail is not null)
            {
                throw new InvalidOperationException(
                    $"The guarded SQLite VFS failed after '{callbackDetail}'.",
                    exception);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    public StoragePaths Paths { get; }
    public SqliteBindingIdentity BindingIdentity { get; }

    public void RecordAuditEvent(
        string eventKind,
        string objectKind,
        string objectId,
        DateTimeOffset now)
    {
        ValidateAuditToken(eventKind, nameof(eventKind));
        ValidateAuditToken(objectKind, nameof(objectKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        if (Encoding.UTF8.GetByteCount(objectId) > 512)
        {
            throw new ArgumentException("The audit object identity exceeds its bound.", nameof(objectId));
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            InsertAuditEvent(eventKind, objectKind, objectId, now, transaction);
            transaction.Commit();
        }
    }

    public CoordinatorAuthority AcquireCoordinatorAuthority(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        return AcquireCoordinatorAuthorityCore(
            instanceId,
            now,
            leaseDuration,
            allowUnexpiredTakeoverAfterProcessExclusion: false);
    }

    public CoordinatorAuthority AcquireCoordinatorAuthorityAfterProcessExclusion(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        return AcquireCoordinatorAuthorityCore(
            instanceId,
            now,
            leaseDuration,
            allowUnexpiredTakeoverAfterProcessExclusion: true);
    }

    private CoordinatorAuthority AcquireCoordinatorAuthorityCore(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        bool allowUnexpiredTakeoverAfterProcessExclusion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            long unexpiredActiveLease = ScalarLong(
                """
                SELECT COUNT(*)
                FROM coordinator_leases lease
                JOIN store_metadata metadata
                  ON metadata.key = 'active_coordinator_epoch'
                 AND CAST(metadata.value AS INTEGER) = lease.fencing_epoch
                WHERE lease.expires_at > $now;
                """,
                transaction,
                ("$now", ToText(now)));
            if (unexpiredActiveLease != 0
                && !allowUnexpiredTakeoverAfterProcessExclusion)
            {
                throw new InvalidOperationException(
                    "An unexpired coordinator authority already owns the store.");
            }

            var epoch = ScalarLong(
                "SELECT COALESCE(MAX(fencing_epoch), 0) + 1 FROM coordinator_leases;",
                transaction);
            var expires = now.Add(leaseDuration);
            Execute(
                """
                INSERT INTO coordinator_leases(
                    coordinator_instance_id, fencing_epoch, acquired_at, expires_at)
                VALUES ($instance, $epoch, $acquired, $expires);
                """,
                transaction,
                ("$instance", instanceId),
                ("$epoch", epoch),
                ("$acquired", ToText(now)),
                ("$expires", ToText(expires)));
            Execute(
                """
                INSERT INTO store_metadata(key, value) VALUES ('active_coordinator_epoch', $epoch)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """,
                transaction,
                ("$epoch", epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            transaction.Commit();
            return new CoordinatorAuthority(instanceId, epoch, expires);
        }
    }

    public CoordinatorAuthority RenewCoordinatorAuthority(
        long currentFencingEpoch,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        RequirePositive(currentFencingEpoch, nameof(currentFencingEpoch));
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        DateTimeOffset requestedExpiry = now.Add(leaseDuration);
        lock (gate)
        {
            using var transaction = BeginTransaction();
            int updated = Execute(
                """
                UPDATE coordinator_leases
                SET expires_at = CASE
                    WHEN expires_at > $expires THEN expires_at
                    ELSE $expires
                END
                WHERE fencing_epoch = $epoch
                  AND expires_at > $now
                  AND EXISTS (
                      SELECT 1
                      FROM store_metadata
                      WHERE key = 'active_coordinator_epoch'
                        AND CAST(value AS INTEGER) = $epoch
                  );
                """,
                transaction,
                ("$epoch", currentFencingEpoch),
                ("$now", ToText(now)),
                ("$expires", ToText(requestedExpiry)));
            if (updated != 1)
            {
                throw new InvalidOperationException(
                    "Only the current unexpired coordinator authority may renew its lease.");
            }

            string instanceId = ScalarString(
                """
                SELECT coordinator_instance_id
                FROM coordinator_leases
                WHERE fencing_epoch = $epoch;
                """,
                transaction,
                ("$epoch", currentFencingEpoch));
            DateTimeOffset expiresAt = DateTimeOffset.Parse(
                ScalarString(
                    """
                    SELECT expires_at
                    FROM coordinator_leases
                    WHERE fencing_epoch = $epoch;
                    """,
                    transaction,
                    ("$epoch", currentFencingEpoch)),
                System.Globalization.CultureInfo.InvariantCulture);
            transaction.Commit();
            return new CoordinatorAuthority(instanceId, currentFencingEpoch, expiresAt);
        }
    }

    public int GetSchemaVersion()
    {
        lock (gate)
        {
            return checked((int)ScalarLong(
                "SELECT CAST(value AS INTEGER) FROM store_metadata WHERE key = 'schema_version';",
                transaction: null));
        }
    }

    public IReadOnlyList<string> GetTableNames()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var reader = command.ExecuteReader();
            var result = new List<string>();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            connection.Dispose();
            sqliteVfs.Dispose();
            Paths.Dispose();
            disposed = true;
        }
    }

    private void EnsureCurrentCoordinatorEpoch(
        long coordinatorFencingEpoch,
        SqliteTransaction transaction)
    {
        long current = ScalarLong(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM coordinator_leases lease
                JOIN store_metadata metadata
                  ON metadata.key = 'active_coordinator_epoch'
                 AND CAST(metadata.value AS INTEGER) = lease.fencing_epoch
                WHERE lease.fencing_epoch = $epoch
                  AND lease.expires_at >= $now
            ) THEN 1 ELSE 0 END;
            """,
            transaction,
            ("$epoch", coordinatorFencingEpoch),
            ("$now", ToText(DateTimeOffset.UtcNow)));
        if (current != 1)
        {
            throw new InvalidOperationException(
                "The coordinator fencing epoch is stale or its lease has expired.");
        }
    }

    private static void ConfigureConnection(SqliteConnection target)
    {
        using var command = target.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            PRAGMA temp_store = MEMORY;
            PRAGMA trusted_schema = OFF;
            PRAGMA busy_timeout = 5000;
            """;
        command.ExecuteNonQuery();
        using var verify = target.CreateCommand();
        verify.CommandText = "SELECT foreign_keys FROM pragma_foreign_keys;";
        if (Convert.ToInt32(verify.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("SQLite foreign-key enforcement could not be enabled.");
        }
    }

    private int Execute(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return command.ExecuteNonQuery();
    }

    private SqliteTransaction BeginTransaction()
    {
        sqliteVfs.VerifyAllGuards();
        return connection.BeginTransaction();
    }

    private long ScalarLong(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private string ScalarString(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters) =>
        ScalarStringOrNull(sql, transaction, parameters)
        ?? throw new InvalidOperationException("A required database value was missing.");

    private string? ScalarStringOrNull(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void RecordWriteClassAuthorityBindings(DateTimeOffset now)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            foreach (ProductWriteClass writeClass in Enum.GetValues<ProductWriteClass>())
            {
                InsertAuditEvent(
                    "write-class-authority-bound",
                    "write-class",
                    writeClass.ToString(),
                    now,
                    transaction);
            }

            transaction.Commit();
        }
    }

    private void InsertAuditEvent(
        string eventKind,
        string objectKind,
        string objectId,
        DateTimeOffset now,
        SqliteTransaction transaction,
        string? detailPayloadId = null)
    {
        Execute(
            """
            INSERT INTO audit_events(
                audit_event_id, event_kind, object_kind, object_id,
                detail_payload_id, occurred_at)
            VALUES ($id, $event, $kind, $object, $payload, $now);
            """,
            transaction,
            ("$id", Guid.NewGuid().ToString("N")),
            ("$event", eventKind),
            ("$kind", objectKind),
            ("$object", objectId),
            ("$payload", detailPayloadId),
            ("$now", ToText(now)));
    }

    private static void ValidateAuditToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 64
            || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Audit tokens must contain 1-64 ASCII letters, digits, hyphens, or underscores.",
                parameterName);
        }
    }

    private static void ValidateBinding(RunBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.InstallationSnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.AnalysisContextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.EffectiveScanConfigurationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ResolvedInputManifestId);
    }

    private static void ValidateBoundedJson(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (Encoding.UTF8.GetByteCount(value) > MaximumCheckpointJsonBytes)
        {
            throw new ArgumentException(
                "Checkpoint JSON exceeds its finite byte bound.",
                parameterName);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                value,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            _ = document.RootElement.ValueKind;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Checkpoint JSON must be a finite valid JSON value.",
                parameterName,
                exception);
        }
    }

    private static void ValidateSha256(string value)
    {
        if (value.Length != 64 || value.Any(ch => !Uri.IsHexDigit(ch)))
        {
            throw new ArgumentException("A lowercase 64-character SHA-256 value is required.", nameof(value));
        }

        if (!string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new ArgumentException("SHA-256 values must use lowercase canonical encoding.", nameof(value));
        }
    }

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static string ToText(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
