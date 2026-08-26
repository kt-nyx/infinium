using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public static class ApplicationSetupPersistenceDeclarations
{
    public const int SchemaVersion = 12;
    public const string StorageContractVersion = "1.11.0";
    public const string MigrationId = "application-setup-contract-0012";
    public const string SourceSchemaFingerprint = ScopeReversionV2PersistenceDeclarations.SchemaFingerprint;
    public const string SchemaFingerprint = "e3dcd08192656fcc24b8374198bb1fbf66d9dd75fc6cf160b2558be16059b3ce";
}

public sealed record SetupObjectRecord(
    string ObjectKind,
    string ObjectId,
    long Revision,
    string LifecycleState,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SetupMutationRequest(
    string RequestId,
    string OperationKind,
    string ObjectKind,
    string ObjectId,
    long ExpectedRevision,
    string LifecycleState,
    string PayloadJson,
    DateTimeOffset RequestedAt);

public sealed record SetupMutationReceipt(
    string RequestId,
    string RequestFingerprint,
    string ObjectKind,
    string ObjectId,
    long AcceptedRevision,
    DateTimeOffset RecordedAt,
    bool Replayed);

public sealed record PreparedRunRecord(
    string PreparationId,
    string RequestId,
    long Revision,
    string ConfirmedProfileId,
    long ProfileRevision,
    string SavedConfigurationId,
    long SavedConfigurationRevision,
    string EffectiveConfigurationId,
    string EffectiveConfigurationJson,
    RunBinding Binding,
    string EstimateJson,
    DateTimeOffset PreparedAt);

public sealed partial class AuthoritativeStore
{
    public IReadOnlyList<SetupObjectRecord> ListSetupObjects(string objectKind, int maximumCount = 100)
    {
        ValidateSetupToken(objectKind, nameof(objectKind));
        if (maximumCount is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT object_id,revision,lifecycle_state,payload_json,created_at,updated_at "
                + "FROM application_setup_objects WHERE object_kind=$kind ORDER BY object_id LIMIT $limit;";
            command.Parameters.AddWithValue("$kind", objectKind);
            command.Parameters.AddWithValue("$limit", maximumCount);
            using SqliteDataReader reader = command.ExecuteReader();
            List<SetupObjectRecord> result = [];
            while (reader.Read())
            {
                result.Add(ReadSetupObject(objectKind, reader));
            }

            return result;
        }
    }

    public SetupObjectRecord? FindSetupObject(string objectKind, string objectId)
    {
        ValidateSetupToken(objectKind, nameof(objectKind));
        ValidateSetupIdentity(objectId, nameof(objectId));
        lock (gate)
        {
            return FindSetupObjectCore(objectKind, objectId, transaction: null);
        }
    }

    public SetupMutationReceipt ApplySetupMutation(SetupMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSetupIdentity(request.RequestId, nameof(request.RequestId));
        ValidateSetupToken(request.OperationKind, nameof(request.OperationKind));
        ValidateSetupToken(request.ObjectKind, nameof(request.ObjectKind));
        ValidateSetupIdentity(request.ObjectId, nameof(request.ObjectId));
        if (request.ExpectedRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
        if (request.LifecycleState is not ("active" or "deleted"))
        {
            throw new ArgumentException("The setup lifecycle state is unsupported.", nameof(request));
        }
        ValidateBoundedJson(request.PayloadJson, nameof(request.PayloadJson));
        string fingerprint = SetupMutationFingerprint(request);

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            using (SqliteCommand replay = connection.CreateCommand())
            {
                replay.Transaction = transaction;
                replay.CommandText =
                    "SELECT request_fingerprint,object_kind,object_id,accepted_revision,recorded_at "
                    + "FROM application_setup_receipts WHERE request_id=$request;";
                replay.Parameters.AddWithValue("$request", request.RequestId);
                using SqliteDataReader reader = replay.ExecuteReader();
                if (reader.Read())
                {
                    SetupMutationReceipt receipt = new(
                        request.RequestId,
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt64(3),
                        DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
                        Replayed: true);
                    if (!StringComparer.Ordinal.Equals(receipt.RequestFingerprint, fingerprint))
                    {
                        throw new InvalidOperationException(
                            "A setup request identity cannot be rebound to different inputs.");
                    }

                    transaction.Commit();
                    return receipt;
                }
            }

            SetupObjectRecord? current = FindSetupObjectCore(
                request.ObjectKind,
                request.ObjectId,
                transaction);
            long actualRevision = current?.Revision ?? 0;
            if (actualRevision != request.ExpectedRevision)
            {
                throw new SetupRevisionConflictException(request.ExpectedRevision, actualRevision);
            }
            if (current is null && request.LifecycleState == "deleted")
            {
                throw new InvalidOperationException("A missing setup object cannot be deleted.");
            }

            long acceptedRevision = checked(actualRevision + 1);
            if (current is null)
            {
                Execute(
                    "INSERT INTO application_setup_objects(object_kind,object_id,revision,lifecycle_state,payload_json,created_at,updated_at) "
                    + "VALUES($kind,$id,$revision,$state,$payload,$now,$now);",
                    transaction,
                    ("$kind", request.ObjectKind),
                    ("$id", request.ObjectId),
                    ("$revision", acceptedRevision),
                    ("$state", request.LifecycleState),
                    ("$payload", request.PayloadJson),
                    ("$now", ToText(request.RequestedAt)));
            }
            else
            {
                Execute(
                    "UPDATE application_setup_objects SET revision=$revision,lifecycle_state=$state,payload_json=$payload,updated_at=$now "
                    + "WHERE object_kind=$kind AND object_id=$id AND revision=$expected;",
                    transaction,
                    ("$revision", acceptedRevision),
                    ("$state", request.LifecycleState),
                    ("$payload", request.PayloadJson),
                    ("$now", ToText(request.RequestedAt)),
                    ("$kind", request.ObjectKind),
                    ("$id", request.ObjectId),
                    ("$expected", actualRevision));
            }

            Execute(
                "INSERT INTO application_setup_receipts(request_id,request_fingerprint,operation_kind,object_kind,object_id,accepted_revision,recorded_at) "
                + "VALUES($request,$fingerprint,$operation,$kind,$id,$revision,$now);",
                transaction,
                ("$request", request.RequestId),
                ("$fingerprint", fingerprint),
                ("$operation", request.OperationKind),
                ("$kind", request.ObjectKind),
                ("$id", request.ObjectId),
                ("$revision", acceptedRevision),
                ("$now", ToText(request.RequestedAt)));
            InsertAuditEvent(
                "application-setup-mutated",
                request.ObjectKind,
                request.ObjectId,
                request.RequestedAt,
                transaction);
            transaction.Commit();
            return new(
                request.RequestId,
                fingerprint,
                request.ObjectKind,
                request.ObjectId,
                acceptedRevision,
                request.RequestedAt,
                Replayed: false);
        }
    }

    public PreparedRunRecord CreatePreparedRun(PreparedRunRecord request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSetupIdentity(request.PreparationId, nameof(request.PreparationId));
        ValidateSetupIdentity(request.RequestId, nameof(request.RequestId));
        ValidateSetupIdentity(request.ConfirmedProfileId, nameof(request.ConfirmedProfileId));
        ValidateSetupIdentity(request.SavedConfigurationId, nameof(request.SavedConfigurationId));
        ValidateSetupIdentity(request.EffectiveConfigurationId, nameof(request.EffectiveConfigurationId));
        if (request.Revision != 1 || request.ProfileRevision <= 0 || request.SavedConfigurationRevision <= 0)
        {
            throw new ArgumentException("Prepared-run revisions are invalid.", nameof(request));
        }
        ValidateBinding(request.Binding);
        ValidateBoundedJson(request.EffectiveConfigurationJson, nameof(request.EffectiveConfigurationJson));
        ValidateBoundedJson(request.EstimateJson, nameof(request.EstimateJson));

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            PreparedRunRecord? existing = FindPreparedRunByRequestCore(request.RequestId, transaction);
            if (existing is not null)
            {
                if (existing with { PreparedAt = request.PreparedAt } != request)
                {
                    throw new InvalidOperationException(
                        "A run-preparation request identity cannot be rebound to different inputs.");
                }

                transaction.Commit();
                return existing;
            }

            Execute(
                "INSERT INTO prepared_manual_runs(preparation_id,request_id,revision,confirmed_profile_id,profile_revision,saved_configuration_id,saved_configuration_revision,effective_configuration_id,effective_configuration_json,installation_snapshot_id,analysis_context_id,resolved_input_manifest_id,estimate_json,prepared_at) "
                + "VALUES($id,$request,$revision,$profile,$profile_revision,$saved,$saved_revision,$effective,$configuration,$snapshot,$context,$manifest,$estimate,$now);",
                transaction,
                ("$id", request.PreparationId),
                ("$request", request.RequestId),
                ("$revision", request.Revision),
                ("$profile", request.ConfirmedProfileId),
                ("$profile_revision", request.ProfileRevision),
                ("$saved", request.SavedConfigurationId),
                ("$saved_revision", request.SavedConfigurationRevision),
                ("$effective", request.EffectiveConfigurationId),
                ("$configuration", request.EffectiveConfigurationJson),
                ("$snapshot", request.Binding.InstallationSnapshotId),
                ("$context", request.Binding.AnalysisContextId),
                ("$manifest", request.Binding.ResolvedInputManifestId),
                ("$estimate", request.EstimateJson),
                ("$now", ToText(request.PreparedAt)));
            InsertAuditEvent(
                "manual-run-prepared",
                "prepared-manual-run",
                request.PreparationId,
                request.PreparedAt,
                transaction);
            transaction.Commit();
            return request;
        }
    }

    public PreparedRunRecord GetPreparedRun(string preparationId)
    {
        ValidateSetupIdentity(preparationId, nameof(preparationId));
        lock (gate)
        {
            using SqliteCommand command = PreparedRunCommand(
                "WHERE preparation_id=$identity",
                transaction: null);
            command.Parameters.AddWithValue("$identity", preparationId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? ReadPreparedRun(reader)
                : throw new KeyNotFoundException(
                    $"Prepared manual run '{preparationId}' does not exist.");
        }
    }

    private PreparedRunRecord? FindPreparedRunByRequestCore(
        string requestId,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = PreparedRunCommand(
            "WHERE request_id=$identity",
            transaction);
        command.Parameters.AddWithValue("$identity", requestId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadPreparedRun(reader) : null;
    }

    private SqliteCommand PreparedRunCommand(string predicate, SqliteTransaction? transaction)
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT preparation_id,request_id,revision,confirmed_profile_id,profile_revision,saved_configuration_id,saved_configuration_revision,effective_configuration_id,effective_configuration_json,installation_snapshot_id,analysis_context_id,resolved_input_manifest_id,estimate_json,prepared_at "
            + "FROM prepared_manual_runs " + predicate + ";";
        return command;
    }

    private static PreparedRunRecord ReadPreparedRun(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetInt64(2),
        reader.GetString(3),
        reader.GetInt64(4),
        reader.GetString(5),
        reader.GetInt64(6),
        reader.GetString(7),
        reader.GetString(8),
        new RunBinding(reader.GetString(9), reader.GetString(10), reader.GetString(7), reader.GetString(11)),
        reader.GetString(12),
        DateTimeOffset.Parse(reader.GetString(13), System.Globalization.CultureInfo.InvariantCulture));

    private SetupObjectRecord? FindSetupObjectCore(
        string objectKind,
        string objectId,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT revision,lifecycle_state,payload_json,created_at,updated_at "
            + "FROM application_setup_objects WHERE object_kind=$kind AND object_id=$id;";
        command.Parameters.AddWithValue("$kind", objectKind);
        command.Parameters.AddWithValue("$id", objectId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read()
            ? new(
                objectKind,
                objectId,
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture))
            : null;
    }

    private static SetupObjectRecord ReadSetupObject(string objectKind, SqliteDataReader reader) => new(
        objectKind,
        reader.GetString(0),
        reader.GetInt64(1),
        reader.GetString(2),
        reader.GetString(3),
        DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture));

    private void ApplyApplicationSetupMigration()
    {
        string sourceFingerprint = ComputeSchemaFingerprint(connection);
        if (sourceFingerprint != ApplicationSetupPersistenceDeclarations.SourceSchemaFingerprint)
        {
            throw new InvalidOperationException(
                "The application setup migration source is not the exact accepted schema-11 state.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(
            """
            CREATE TABLE application_setup_objects(
              object_kind TEXT NOT NULL CHECK(length(object_kind) BETWEEN 1 AND 64),
              object_id TEXT NOT NULL CHECK(length(object_id) BETWEEN 1 AND 128),
              revision INTEGER NOT NULL CHECK(revision > 0),
              lifecycle_state TEXT NOT NULL CHECK(lifecycle_state IN ('active','deleted')),
              payload_json TEXT NOT NULL CHECK(length(payload_json) BETWEEN 2 AND 65536),
              created_at TEXT NOT NULL,
              updated_at TEXT NOT NULL,
              PRIMARY KEY(object_kind,object_id),
              CHECK(updated_at >= created_at)
            ) STRICT;
            CREATE TABLE application_setup_receipts(
              request_id TEXT PRIMARY KEY CHECK(length(request_id) BETWEEN 1 AND 128),
              request_fingerprint TEXT NOT NULL CHECK(length(request_fingerprint)=64),
              operation_kind TEXT NOT NULL CHECK(length(operation_kind) BETWEEN 1 AND 64),
              object_kind TEXT NOT NULL,
              object_id TEXT NOT NULL,
              accepted_revision INTEGER NOT NULL CHECK(accepted_revision > 0),
              recorded_at TEXT NOT NULL,
              FOREIGN KEY(object_kind,object_id) REFERENCES application_setup_objects(object_kind,object_id)
            ) STRICT;
            CREATE TABLE prepared_manual_runs(
              preparation_id TEXT PRIMARY KEY CHECK(length(preparation_id) BETWEEN 1 AND 128),
              request_id TEXT NOT NULL UNIQUE CHECK(length(request_id) BETWEEN 1 AND 128),
              revision INTEGER NOT NULL CHECK(revision=1),
              confirmed_profile_id TEXT NOT NULL CHECK(length(confirmed_profile_id) BETWEEN 1 AND 128),
              profile_revision INTEGER NOT NULL CHECK(profile_revision > 0),
              saved_configuration_id TEXT NOT NULL CHECK(length(saved_configuration_id) BETWEEN 1 AND 128),
              saved_configuration_revision INTEGER NOT NULL CHECK(saved_configuration_revision > 0),
              effective_configuration_id TEXT NOT NULL CHECK(length(effective_configuration_id) BETWEEN 1 AND 128),
              effective_configuration_json TEXT NOT NULL CHECK(length(effective_configuration_json) BETWEEN 2 AND 65536),
              installation_snapshot_id TEXT NOT NULL,
              analysis_context_id TEXT NOT NULL,
              resolved_input_manifest_id TEXT NOT NULL,
              estimate_json TEXT NOT NULL CHECK(length(estimate_json) BETWEEN 2 AND 65536),
              prepared_at TEXT NOT NULL
            ) STRICT;
            CREATE TABLE prepared_run_submissions(
              command_id TEXT PRIMARY KEY REFERENCES durable_commands(command_id) ON DELETE RESTRICT,
              preparation_id TEXT NOT NULL REFERENCES prepared_manual_runs(preparation_id) ON DELETE RESTRICT,
              user_gesture_id TEXT NOT NULL CHECK(length(user_gesture_id) BETWEEN 16 AND 128),
              submitted_at TEXT NOT NULL,
              UNIQUE(preparation_id,user_gesture_id)
            ) STRICT;
            """,
            transaction);
        CreateAppendOnlyTriggers(
            ["application_setup_receipts", "prepared_manual_runs", "prepared_run_submissions"],
            transaction);
        string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (schemaFingerprint != ApplicationSetupPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"The application setup migration produced unexpected schema fingerprint '{schemaFingerprint}'.");
        }
        Execute(
            """
            UPDATE store_metadata SET value='12' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.11.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO migration_history(migration_id,from_version,to_version,applied_at,sqlite_source_id)
            VALUES('application-setup-contract-0012',11,12,$now,$source);
            PRAGMA user_version=12;
            """,
            transaction,
            ("$fingerprint", schemaFingerprint),
            ("$now", ToText(DateTimeOffset.UtcNow)),
            ("$source", BindingIdentity.SourceId));
        transaction.Commit();
    }

    private void ValidateApplicationSetupMigration()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('application_setup_objects','application_setup_receipts','prepared_manual_runs','prepared_run_submissions');";
        string actual = ComputeSchemaFingerprint(connection);
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 4
            || actual != ApplicationSetupPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"Schema 12 lacks the exact application setup migration: {actual}");
        }
    }

    private static string SetupMutationFingerprint(SetupMutationRequest request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            request.OperationKind,
            request.ObjectKind,
            request.ObjectId,
            request.ExpectedRevision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.LifecycleState,
            request.PayloadJson))));

    private static void ValidateSetupToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 64 || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("The setup token is invalid.", parameterName);
        }
    }

    private static void ValidateSetupIdentity(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 128 || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("The setup identity is invalid.", parameterName);
        }
    }
}

public sealed class SetupRevisionConflictException(long expectedRevision, long currentRevision)
    : InvalidOperationException("The setup object revision is stale.")
{
    public long ExpectedRevision { get; } = expectedRevision;
    public long CurrentRevision { get; } = currentRevision;
}
