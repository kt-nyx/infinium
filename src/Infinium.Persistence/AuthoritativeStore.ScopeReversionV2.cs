using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public static class ScopeReversionV2PersistenceDeclarations
{
    public const int SchemaVersion = 11;
    public const string StorageContractVersion = "1.10.0";
    public const string MigrationId = "M1-S8-WP4-0011";
    public const string SourceSchemaFingerprint = ScopeReversionPersistenceDeclarations.SchemaFingerprint;
    public const string SchemaFingerprint = "73f58a86ef5ff4b046e7d2b45b4612047eeda17515f31d75524a37d7a48d8bba";
}

public sealed record ScopeReversionV2RetainedArtifact(
    string ArtifactId,
    string Kind,
    ReadOnlyMemory<byte> Bytes);

public sealed record ScopeReversionV2PublicationRequest(
    ScopeReversionV2AnalysisContract Analysis,
    ReadOnlyMemory<byte> AnalysisBytes,
    IReadOnlyList<ScopeReversionV2RetainedArtifact> RetainedArtifacts,
    DateTimeOffset CreatedAt);

public sealed record ScopeReversionV2PersistenceReceipt(
    string PayloadId,
    string RunId,
    string SnapshotId,
    string ContextId,
    string ConfigurationId,
    string ExecutionInputId,
    string AssignmentId,
    string InputHandoffId,
    string InputManifestSha256,
    IReadOnlyList<ScopeReversionV2PublicManifestReferenceContract> PublicManifests,
    IReadOnlyList<ScopeReversionV2ControlledInputReferenceContract> ControlledInputs,
    string PayloadSha256,
    long PayloadByteLength,
    string SemanticFingerprint,
    IReadOnlyList<string> ArtifactIds);

public sealed record ScopeReversionV2InvalidationRecord(
    string PayloadId,
    string DependencyId,
    string Reason,
    DateTimeOffset InvalidatedAt);

public sealed partial class AuthoritativeStore
{
    public string GetCurrentSchemaFingerprint()
    {
        lock (gate)
        {
            return ComputeSchemaFingerprint(connection);
        }
    }

    public ScopeReversionV2PersistenceReceipt PublishScopeReversionV2Analysis(
        ScopeReversionV2PublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ScopeReversionV2Contract.Validate(request.Analysis);
        if (request.AnalysisBytes.IsEmpty || request.AnalysisBytes.Length > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("The scope-reversion v2 canonical payload is empty or exceeds its bound.");
        }
        string payloadId = request.Analysis.PayloadId.Value;
        byte[] expectedCanonical = JsonSerializer.SerializeToUtf8Bytes(
            request.Analysis,
            ContractJsonSerializer.Options);
        if (!expectedCanonical.AsSpan().SequenceEqual(request.AnalysisBytes.Span))
        {
            throw new InvalidDataException("The scope-reversion v2 publication bytes are not the canonical bytes of the validated analysis.");
        }
        string payloadSha = Hash(request.AnalysisBytes.Span);
        string semanticFingerprint = ContractJsonSerializer.Fingerprint(new
        {
            request.Analysis.Subjects,
            request.Analysis.Members,
            request.Analysis.PublicManifests,
            request.Analysis.ControlledInputs,
            request.Analysis.SourceDecisions,
            request.Analysis.Taxonomy,
            request.Analysis.Decisions,
            request.Analysis.Hypotheses,
            request.Analysis.Findings,
            request.Analysis.Cases,
            request.Analysis.Coverage,
            request.Analysis.Gaps,
            request.Analysis.PartitionTransitions,
        }).Value;
        if (request.RetainedArtifacts.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count()
                != request.RetainedArtifacts.Count)
        {
            throw new InvalidDataException("Scope-reversion v2 retained artifact identities must be unique.");
        }
        Dictionary<string, ScopeReversionV2RetainedArtifact> artifacts = request.RetainedArtifacts
            .ToDictionary(item => item.ArtifactId, StringComparer.Ordinal);
        if (artifacts.Values.Any(item => string.IsNullOrWhiteSpace(item.ArtifactId)
                || string.IsNullOrWhiteSpace(item.Kind) || item.Kind.Length > 64
                || item.Bytes.IsEmpty || item.Bytes.Length > 16 * 1024 * 1024))
        {
            throw new InvalidDataException("Scope-reversion v2 retained artifacts are duplicate or outside finite bounds.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? existingSha = ScalarStringOrNull(
                "SELECT payload_sha256 FROM scope_reversion_v2_analyses WHERE payload_id=$id;",
                transaction, ("$id", payloadId));
            if (existingSha is not null)
            {
                if (!StringComparer.Ordinal.Equals(existingSha, payloadSha)
                    || !ReadScopeReversionV2BytesCore(payloadId, transaction).AsSpan()
                        .SequenceEqual(request.AnalysisBytes.Span))
                {
                    throw new InvalidDataException("A duplicate scope-reversion v2 identity resolves to different bytes.");
                }
                foreach (ScopeReversionV2RetainedArtifact artifact in artifacts.Values)
                {
                    ValidateExistingScopeReversionV2Artifact(payloadId, artifact, transaction);
                }
                ScopeReversionV2PersistenceReceipt existing = ScopeReversionV2Receipt(payloadId, transaction);
                string[] suppliedIds = artifacts.Keys.Append(payloadId).Order(StringComparer.Ordinal).ToArray();
                if (!existing.ArtifactIds.SequenceEqual(suppliedIds, StringComparer.Ordinal))
                {
                    throw new InvalidDataException("A duplicate scope-reversion v2 publication supplies a different retained artifact set.");
                }
                transaction.Rollback();
                return existing;
            }

            Execute(
                """
                INSERT INTO scope_reversion_v2_analyses(
                  payload_id,run_id,snapshot_id,context_id,configuration_id,execution_input_id,
                  assignment_id,input_manifest_sha256,analyzer_declaration_sha256,partition_role,
                  payload_sha256,payload_byte_length,semantic_fingerprint,payload_bytes,created_at)
                VALUES($payload,$run,$snapshot,$context,$configuration,$execution,$assignment,$manifest,
                  $declaration,$partition,$sha,$length,$semantic,$bytes,$created);
                """,
                transaction,
                ("$payload", payloadId), ("$run", request.Analysis.OriginatingRunId.Value),
                ("$snapshot", request.Analysis.SnapshotId.Value), ("$context", request.Analysis.ContextId.Value),
                ("$configuration", request.Analysis.ConfigurationId.Value),
                ("$execution", request.Analysis.ExecutionInputId.Value),
                ("$assignment", request.Analysis.AssignmentId.Value),
                ("$manifest", request.Analysis.InputManifestFingerprint.Value),
                ("$declaration", request.Analysis.Analyzer.DeclarationFingerprint.Value),
                ("$partition", request.Analysis.PartitionRole.ToString()),
                ("$sha", payloadSha), ("$length", request.AnalysisBytes.Length),
                ("$semantic", semanticFingerprint), ("$bytes", request.AnalysisBytes.ToArray()),
                ("$created", ToText(request.CreatedAt)));

            InsertScopeReversionV2Items(payloadId, request.Analysis, transaction);
            InsertScopeReversionV2Artifact(payloadId, payloadId, "scope-reversion-v2-analysis",
                request.AnalysisBytes.Span, transaction);
            foreach (ScopeReversionV2RetainedArtifact artifact in artifacts.Values.OrderBy(item => item.ArtifactId, StringComparer.Ordinal))
            {
                InsertScopeReversionV2Artifact(artifact.ArtifactId, payloadId, artifact.Kind, artifact.Bytes.Span, transaction);
                Execute(
                    "INSERT INTO scope_reversion_v2_dependencies(payload_id,dependency_id,artifact_id,edge_kind) VALUES($payload,$dependency,$artifact,'retained-input');",
                    transaction, ("$payload", payloadId), ("$dependency", artifact.ArtifactId),
                    ("$artifact", artifact.ArtifactId));
            }
            Execute(
                "INSERT INTO scope_reversion_v2_publications(payload_id,publication_id,payload_sha256,published_at) VALUES($payload,$publication,$sha,$created);",
                transaction, ("$payload", payloadId), ("$publication", "publication-" + payloadId),
                ("$sha", payloadSha), ("$created", ToText(request.CreatedAt)));
            InsertAuditEvent("scope-reversion-v2-published", "scope-reversion-v2-analysis", payloadId,
                request.CreatedAt, transaction);
            ScopeReversionV2PersistenceReceipt receipt = ScopeReversionV2Receipt(payloadId, transaction);
            transaction.Commit();
            return receipt;
        }
    }

    public byte[] ReadScopeReversionV2AnalysisBytes(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            return ReadScopeReversionV2BytesCore(payloadId, null);
        }
    }

    public ScopeReversionV2PersistenceReceipt ReadScopeReversionV2Receipt(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            ScopeReversionV2PersistenceReceipt receipt = ScopeReversionV2Receipt(payloadId, transaction);
            transaction.Rollback();
            return receipt;
        }
    }

    public byte[] GetScopeReversionV2Artifact(string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT kind,content_sha256,byte_length,artifact_bytes FROM scope_reversion_v2_artifacts WHERE artifact_id=$id ORDER BY payload_id;";
            command.Parameters.AddWithValue("$id", artifactId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Scope-reversion v2 artifact '{artifactId}' does not exist.");
            }
            string kind = reader.GetString(0);
            string sha = reader.GetString(1);
            long length = reader.GetInt64(2);
            byte[] bytes = (byte[])reader[3];
            if (bytes.LongLength != length || Hash(bytes) != sha)
            {
                throw new InvalidDataException("Scope-reversion v2 artifact failed retained-byte validation.");
            }
            while (reader.Read())
            {
                if (reader.GetString(0) != kind || reader.GetString(1) != sha || reader.GetInt64(2) != length
                    || !((byte[])reader[3]).AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException("A scope-reversion v2 artifact identity resolves to different bytes.");
                }
            }
            return bytes;
        }
    }

    public IReadOnlyList<ScopeReversionV2InvalidationRecord> InvalidateScopeReversionV2Dependency(
        string dependencyId,
        string reason,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 512)
        {
            throw new ArgumentException("Invalidation reason exceeds its finite bound.", nameof(reason));
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            List<string> payloadIds = [];
            using (SqliteCommand query = connection.CreateCommand())
            {
                query.Transaction = transaction;
                query.CommandText = "SELECT DISTINCT payload_id FROM scope_reversion_v2_dependencies WHERE dependency_id=$id ORDER BY payload_id;";
                query.Parameters.AddWithValue("$id", dependencyId);
                using SqliteDataReader reader = query.ExecuteReader();
                while (reader.Read())
                {
                    payloadIds.Add(reader.GetString(0));
                }
            }
            foreach (string payloadId in payloadIds)
            {
                Execute(
                    "INSERT INTO scope_reversion_v2_invalidations(payload_id,dependency_id,reason,invalidated_at) VALUES($payload,$dependency,$reason,$now);",
                    transaction, ("$payload", payloadId), ("$dependency", dependencyId),
                    ("$reason", reason), ("$now", ToText(now)));
            }
            transaction.Commit();
            return payloadIds.Select(payloadId => new ScopeReversionV2InvalidationRecord(
                payloadId, dependencyId, reason, now)).ToArray();
        }
    }

    public IReadOnlyList<ScopeReversionV2InvalidationRecord> ReadScopeReversionV2Invalidations(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT dependency_id,reason,invalidated_at FROM scope_reversion_v2_invalidations WHERE payload_id=$id ORDER BY invalidation_id;";
            command.Parameters.AddWithValue("$id", payloadId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ScopeReversionV2InvalidationRecord> rows = [];
            while (reader.Read())
            {
                rows.Add(new(payloadId, reader.GetString(0), reader.GetString(1),
                    DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture)));
            }
            return rows;
        }
    }

    private void ApplyScopeReversionV2Migration()
    {
        string sourceFingerprint = ComputeSchemaFingerprint(connection);
        if (sourceFingerprint != ScopeReversionV2PersistenceDeclarations.SourceSchemaFingerprint)
        {
            throw new InvalidOperationException("The Slice 8 persistence migration source is not the exact accepted schema-10 state.");
        }
        using SqliteTransaction transaction = BeginTransaction();
        Execute(
            """
            CREATE TABLE scope_reversion_v2_analyses(
              payload_id TEXT PRIMARY KEY, run_id TEXT NOT NULL, snapshot_id TEXT NOT NULL,
              context_id TEXT NOT NULL, configuration_id TEXT NOT NULL, execution_input_id TEXT NOT NULL,
              assignment_id TEXT NOT NULL, input_manifest_sha256 TEXT NOT NULL CHECK(length(input_manifest_sha256)=64),
              analyzer_declaration_sha256 TEXT NOT NULL CHECK(length(analyzer_declaration_sha256)=64),
              partition_role TEXT NOT NULL CHECK(partition_role IN ('ControlledRealValidation','ControlledRealDevelopment')),
              payload_sha256 TEXT NOT NULL CHECK(length(payload_sha256)=64),
              payload_byte_length INTEGER NOT NULL CHECK(payload_byte_length BETWEEN 1 AND 67108864),
              semantic_fingerprint TEXT NOT NULL CHECK(length(semantic_fingerprint)=64),
              payload_bytes BLOB NOT NULL, created_at TEXT NOT NULL,
              UNIQUE(run_id,snapshot_id,context_id,configuration_id,execution_input_id,assignment_id)
            ) STRICT;
            CREATE TABLE scope_reversion_v2_items(
              payload_id TEXT NOT NULL REFERENCES scope_reversion_v2_analyses(payload_id),
              item_kind TEXT NOT NULL CHECK(item_kind IN ('public-manifest','controlled-input','subject','member','source-decision','taxonomy','decision','hypothesis','finding','case','coverage','gap','partition-transition')),
              item_id TEXT NOT NULL, ordinal INTEGER NOT NULL CHECK(ordinal>=0),
              item_sha256 TEXT NOT NULL CHECK(length(item_sha256)=64), item_json BLOB NOT NULL,
              PRIMARY KEY(payload_id,item_kind,item_id), UNIQUE(payload_id,item_kind,ordinal)
            ) STRICT;
            CREATE TABLE scope_reversion_v2_artifacts(
              artifact_id TEXT NOT NULL, payload_id TEXT NOT NULL REFERENCES scope_reversion_v2_analyses(payload_id),
              kind TEXT NOT NULL CHECK(length(kind) BETWEEN 1 AND 64),
              content_sha256 TEXT NOT NULL CHECK(length(content_sha256)=64),
              byte_length INTEGER NOT NULL CHECK(byte_length BETWEEN 1 AND 16777216 OR kind='scope-reversion-v2-analysis'),
              artifact_bytes BLOB NOT NULL, PRIMARY KEY(payload_id,artifact_id)
            ) STRICT;
            CREATE TABLE scope_reversion_v2_dependencies(
              payload_id TEXT NOT NULL REFERENCES scope_reversion_v2_analyses(payload_id),
              dependency_id TEXT NOT NULL, artifact_id TEXT NOT NULL, edge_kind TEXT NOT NULL,
              PRIMARY KEY(payload_id,dependency_id),
              FOREIGN KEY(payload_id,artifact_id) REFERENCES scope_reversion_v2_artifacts(payload_id,artifact_id)
            ) STRICT;
            CREATE INDEX scope_reversion_v2_dependencies_identity_idx ON scope_reversion_v2_dependencies(dependency_id,payload_id);
            CREATE INDEX scope_reversion_v2_artifacts_identity_idx ON scope_reversion_v2_artifacts(artifact_id,payload_id);
            CREATE TABLE scope_reversion_v2_invalidations(
              invalidation_id INTEGER PRIMARY KEY AUTOINCREMENT,
              payload_id TEXT NOT NULL REFERENCES scope_reversion_v2_analyses(payload_id),
              dependency_id TEXT NOT NULL, reason TEXT NOT NULL CHECK(length(reason) BETWEEN 1 AND 512),
              invalidated_at TEXT NOT NULL
            ) STRICT;
            CREATE TABLE scope_reversion_v2_publications(
              payload_id TEXT NOT NULL REFERENCES scope_reversion_v2_analyses(payload_id),
              publication_id TEXT NOT NULL UNIQUE, payload_sha256 TEXT NOT NULL CHECK(length(payload_sha256)=64),
              published_at TEXT NOT NULL, PRIMARY KEY(payload_id,publication_id)
            ) STRICT;
            """,
            transaction);
        CreateAppendOnlyTriggers(
            ["scope_reversion_v2_analyses", "scope_reversion_v2_items", "scope_reversion_v2_artifacts",
                "scope_reversion_v2_dependencies", "scope_reversion_v2_invalidations", "scope_reversion_v2_publications"],
            transaction);
        string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (schemaFingerprint != ScopeReversionV2PersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException($"The Slice 8 persistence migration produced unexpected schema fingerprint '{schemaFingerprint}'.");
        }
        Execute(
            """
            UPDATE store_metadata SET value='11' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.10.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO migration_history(migration_id,from_version,to_version,applied_at,sqlite_source_id)
            VALUES('M1-S8-WP4-0011',10,11,$now,$source);
            PRAGMA user_version=11;
            """,
            transaction, ("$fingerprint", schemaFingerprint), ("$now", ToText(DateTimeOffset.UtcNow)),
            ("$source", BindingIdentity.SourceId));
        transaction.Commit();
    }

    private void ValidateScopeReversionV2Migration()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name LIKE 'scope_reversion_v2_%';";
        string actual = ComputeSchemaFingerprint(connection);
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 6
            || actual != ScopeReversionV2PersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException($"Schema 11 lacks the exact Slice 8 scope-reversion v2 migration: {actual}");
        }
    }

    private void InsertScopeReversionV2Items(string payloadId, ScopeReversionV2AnalysisContract analysis, SqliteTransaction transaction)
    {
        InsertItems(payloadId, "public-manifest", analysis.PublicManifests.Select(item => (item.RepositoryPath, (object)item)), transaction);
        InsertItems(payloadId, "controlled-input", analysis.ControlledInputs.Select(item => (item.RelativePath, (object)item)), transaction);
        InsertItems(payloadId, "subject", analysis.Subjects.Select(item => (item.SubjectId.Value, (object)item)), transaction);
        InsertItems(payloadId, "member", analysis.Members.Select(item => (item.MemberId.Value, (object)item)), transaction);
        InsertItems(payloadId, "source-decision", analysis.SourceDecisions.Select(item => (item.DecisionId.Value, (object)item)), transaction);
        InsertItems(payloadId, "taxonomy", analysis.Taxonomy.Select(item => (item.AssignmentId.Value, (object)item)), transaction);
        InsertItems(payloadId, "decision", analysis.Decisions.Select(item => (item.DecisionId.Value, (object)item)), transaction);
        InsertItems(payloadId, "hypothesis", analysis.Hypotheses.Select(item => (item.HypothesisId.Value, (object)item)), transaction);
        InsertItems(payloadId, "finding", analysis.Findings.Select(item => (item.FindingId.Value, (object)item)), transaction);
        InsertItems(payloadId, "case", analysis.Cases.Select(item => (item.CaseId.Value, (object)item)), transaction);
        InsertItems(payloadId, "coverage", analysis.Coverage.Select(item => (item.PopulationId, (object)item)), transaction);
        InsertItems(payloadId, "gap", analysis.Gaps.Select(item => (item.GapId.Value, (object)item)), transaction);
        InsertItems(payloadId, "partition-transition", analysis.PartitionTransitions.Select(item => (item.TransitionId.Value, (object)item)), transaction);
    }

    private void InsertItems(string payloadId, string kind, IEnumerable<(string Id, object Value)> values, SqliteTransaction transaction)
    {
        int ordinal = 0;
        foreach ((string id, object value) in values)
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, ContractJsonSerializer.Options);
            Execute(
                "INSERT INTO scope_reversion_v2_items(payload_id,item_kind,item_id,ordinal,item_sha256,item_json) VALUES($payload,$kind,$id,$ordinal,$sha,$json);",
                transaction, ("$payload", payloadId), ("$kind", kind), ("$id", id), ("$ordinal", ordinal++),
                ("$sha", Hash(json)), ("$json", json));
        }
    }

    private void InsertScopeReversionV2Artifact(string artifactId, string payloadId, string kind,
        ReadOnlySpan<byte> bytes, SqliteTransaction transaction)
    {
        using (SqliteCommand existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText = "SELECT kind,content_sha256,byte_length,artifact_bytes FROM scope_reversion_v2_artifacts WHERE artifact_id=$artifact;";
            existing.Parameters.AddWithValue("$artifact", artifactId);
            using SqliteDataReader reader = existing.ExecuteReader();
            string sha = Hash(bytes);
            while (reader.Read())
            {
                if (reader.GetString(0) != kind || reader.GetString(1) != sha
                    || reader.GetInt64(2) != bytes.Length || !((byte[])reader[3]).AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException("A scope-reversion v2 artifact identity already resolves to different retained bytes.");
                }
            }
        }
        Execute(
            "INSERT INTO scope_reversion_v2_artifacts(artifact_id,payload_id,kind,content_sha256,byte_length,artifact_bytes) VALUES($artifact,$payload,$kind,$sha,$length,$bytes);",
            transaction, ("$artifact", artifactId), ("$payload", payloadId), ("$kind", kind),
            ("$sha", Hash(bytes)), ("$length", bytes.Length), ("$bytes", bytes.ToArray()));
    }

    private void ValidateExistingScopeReversionV2Artifact(
        string payloadId,
        ScopeReversionV2RetainedArtifact artifact,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT kind,content_sha256,byte_length,artifact_bytes FROM scope_reversion_v2_artifacts WHERE payload_id=$payload AND artifact_id=$artifact;";
        command.Parameters.AddWithValue("$payload", payloadId);
        command.Parameters.AddWithValue("$artifact", artifact.ArtifactId);
        using SqliteDataReader reader = command.ExecuteReader();
        string sha = Hash(artifact.Bytes.Span);
        if (!reader.Read() || reader.GetString(0) != artifact.Kind || reader.GetString(1) != sha
            || reader.GetInt64(2) != artifact.Bytes.Length
            || !((byte[])reader[3]).AsSpan().SequenceEqual(artifact.Bytes.Span) || reader.Read())
        {
            throw new InvalidDataException("A duplicate scope-reversion v2 publication supplies drifted retained dependency bytes.");
        }
    }

    private byte[] ReadScopeReversionV2BytesCore(string payloadId, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT payload_sha256,payload_byte_length,payload_bytes FROM scope_reversion_v2_analyses WHERE payload_id=$id;";
        command.Parameters.AddWithValue("$id", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Scope-reversion v2 payload '{payloadId}' does not exist.");
        }
        string sha = reader.GetString(0);
        long length = reader.GetInt64(1);
        byte[] bytes = (byte[])reader[2];
        if (bytes.LongLength != length || Hash(bytes) != sha)
        {
            throw new InvalidDataException("Scope-reversion v2 analysis failed retained-byte validation.");
        }
        return bytes;
    }

    private ScopeReversionV2PersistenceReceipt ScopeReversionV2Receipt(string payloadId, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT run_id,snapshot_id,context_id,configuration_id,execution_input_id,assignment_id,input_manifest_sha256,payload_sha256,payload_byte_length,semantic_fingerprint FROM scope_reversion_v2_analyses WHERE payload_id=$id;";
        command.Parameters.AddWithValue("$id", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Scope-reversion v2 payload '{payloadId}' does not exist.");
        }
        string[] fields = Enumerable.Range(0, 8).Select(reader.GetString).ToArray();
        long length = reader.GetInt64(8);
        string semantic = reader.GetString(9);
        reader.Close();
        ScopeReversionV2AnalysisContract analysis = JsonSerializer.Deserialize<ScopeReversionV2AnalysisContract>(
            ReadScopeReversionV2BytesCore(payloadId, transaction), ContractJsonSerializer.Options)
            ?? throw new InvalidDataException("Scope-reversion v2 retained provenance is unreadable.");
        ScopeReversionV2Contract.Validate(analysis);
        using SqliteCommand artifacts = connection.CreateCommand();
        artifacts.Transaction = transaction;
        artifacts.CommandText = "SELECT artifact_id FROM scope_reversion_v2_artifacts WHERE payload_id=$id ORDER BY artifact_id;";
        artifacts.Parameters.AddWithValue("$id", payloadId);
        using SqliteDataReader artifactReader = artifacts.ExecuteReader();
        List<string> ids = [];
        while (artifactReader.Read())
        {
            ids.Add(artifactReader.GetString(0));
        }
        return new(payloadId, fields[0], fields[1], fields[2], fields[3], fields[4], fields[5],
            analysis.InputHandoffId, fields[6], analysis.PublicManifests, analysis.ControlledInputs,
            fields[7], length, semantic, ids);
    }
}
