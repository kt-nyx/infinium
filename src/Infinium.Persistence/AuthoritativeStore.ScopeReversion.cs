using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public static class ScopeReversionPersistenceDeclarations
{
    public const int SchemaVersion = 10;
    public const string StorageContractVersion = "1.9.0";
    public const string MigrationId = "M1-S7-WP5-0010";
    public const string SourceSchemaFingerprint =
        ProviderPersistenceDeclarations.SemanticAdmissionSeparationSchemaFingerprint;
    public const string SchemaFingerprint = "d1a3348454d53f3fe4e24c668fbed7fea1443f6ce9947a111c8b29269851efeb";
}

public sealed record ScopeReversionRetainedArtifact(
    string ArtifactId,
    string Kind,
    ReadOnlyMemory<byte> Bytes);

public sealed record ScopeReversionPublicationRequest(
    ScopeReversionAnalysisContract Analysis,
    ReadOnlyMemory<byte> AnalysisBytes,
    IReadOnlyList<ScopeReversionRetainedArtifact> RetainedArtifacts,
    DateTimeOffset CreatedAt);

public sealed record ScopeReversionPersistenceReceipt(
    string PayloadId,
    string RunId,
    string AssignmentId,
    string PayloadSha256,
    long PayloadByteLength,
    string SemanticFingerprint,
    IReadOnlyList<string> ArtifactIds);

public sealed record ScopeReversionArtifactRecord(
    string ArtifactId,
    string PayloadId,
    string Kind,
    string ContentSha256,
    long ByteLength);

public sealed record ScopeReversionInvalidationRecord(
    string PayloadId,
    string DependencyId,
    string Reason,
    DateTimeOffset InvalidatedAt);

public sealed partial class AuthoritativeStore
{
    public ScopeReversionPersistenceReceipt PublishScopeReversionAnalysis(
        ScopeReversionPublicationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ScopeReversionContractInvariants.Validate(request.Analysis);
        if (request.AnalysisBytes.Length is < 1 or > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Scope-reversion analysis payload exceeds its finite bound.");
        }
        byte[] exactAnalysisBytes = JsonSerializer.SerializeToUtf8Bytes(
            request.Analysis, ContractJsonSerializer.Options);
        if (!exactAnalysisBytes.AsSpan().SequenceEqual(request.AnalysisBytes.Span))
        {
            throw new InvalidDataException("Scope-reversion typed analysis and retained canonical bytes differ.");
        }
        string payloadSha = Hash(request.AnalysisBytes.Span);
        string semanticFingerprint = ContractJsonSerializer.Fingerprint(new
        {
            request.Analysis.Decisions,
            request.Analysis.Candidates,
            request.Analysis.Hypotheses,
            request.Analysis.Contradictions,
            request.Analysis.Abstentions,
            request.Analysis.Gaps,
            request.Analysis.Failures,
            request.Analysis.Findings,
            request.Analysis.Cases,
            request.Analysis.Recommendations,
            request.Analysis.Taxonomy,
            request.Analysis.Coverage,
            request.Analysis.DependencyEdges,
            request.Analysis.PublicationClaimBoundary,
        }).Value;
        Dictionary<string, ScopeReversionRetainedArtifact> artifacts = request.RetainedArtifacts
            .ToDictionary(item => item.ArtifactId, StringComparer.Ordinal);
        HashSet<string> required = request.Analysis.DependencyEdges
            .Where(item => item.ToKind is "dependency" or "evidence")
            .Select(item => item.ToId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (artifacts.Count != request.RetainedArtifacts.Count
            || artifacts.Count != required.Count
            || required.Any(item => !artifacts.ContainsKey(item))
            || artifacts.Values.Any(item => item.ArtifactId.Length is < 1 or > 128
                || item.Kind.Length is < 1 or > 64
                || item.Bytes.Length is < 1 or > 16 * 1024 * 1024))
        {
            throw new InvalidDataException("Scope-reversion publication has duplicate, missing, or unbounded retained dependency artifacts.");
        }

        string payloadId = request.Analysis.PayloadId.Value;
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? existingSha = ScalarStringOrNull(
                "SELECT payload_sha256 FROM scope_reversion_analyses WHERE payload_id=$id;",
                transaction, ("$id", payloadId));
            if (existingSha is not null)
            {
                if (!StringComparer.Ordinal.Equals(existingSha, payloadSha)
                    || !ReadScopeReversionBytesCore(payloadId, transaction).AsSpan().SequenceEqual(request.AnalysisBytes.Span))
                {
                    throw new InvalidOperationException("A duplicate scope-reversion identity resolves to different retained bytes.");
                }
                foreach (ScopeReversionRetainedArtifact artifact in artifacts.Values)
                {
                    ValidateExistingArtifact(payloadId, artifact, transaction);
                }
                ScopeReversionPersistenceReceipt existing = Receipt(payloadId, transaction);
                transaction.Rollback();
                return existing;
            }

            Execute(
                """
                INSERT INTO scope_reversion_analyses(
                    payload_id,run_id,assignment_id,input_fingerprint,analyzer_declaration_sha256,
                    payload_sha256,payload_byte_length,semantic_fingerprint,payload_bytes,created_at)
                VALUES($payload,$run,$assignment,$input,$declaration,$sha,$length,$semantic,$bytes,$created);
                """,
                transaction,
                ("$payload", payloadId),
                ("$run", request.Analysis.OriginatingRunId.Value),
                ("$assignment", request.Analysis.AssignmentId.Value),
                ("$input", request.Analysis.InputFingerprint.Value),
                ("$declaration", request.Analysis.Analyzer.DeclarationFingerprint.Value),
                ("$sha", payloadSha),
                ("$length", request.AnalysisBytes.Length),
                ("$semantic", semanticFingerprint),
                ("$bytes", request.AnalysisBytes.ToArray()),
                ("$created", ToText(request.CreatedAt)));
            InsertArtifact(payloadId, payloadId, "scope-reversion-analysis", request.AnalysisBytes.Span, transaction);
            foreach (ScopeReversionRetainedArtifact artifact in artifacts.Values.OrderBy(item => item.ArtifactId, StringComparer.Ordinal))
            {
                InsertArtifact(artifact.ArtifactId, payloadId, artifact.Kind, artifact.Bytes.Span, transaction);
            }
            foreach (ScopeReversionDependencyEdgeContract edge in request.Analysis.DependencyEdges
                .Where(item => item.ToKind is "dependency" or "evidence")
                .OrderBy(item => item.EdgeId.Value, StringComparer.Ordinal))
            {
                Execute(
                    "INSERT INTO scope_reversion_dependencies(payload_id,edge_id,artifact_id,edge_kind) VALUES($payload,$edge,$artifact,$kind);",
                    transaction,
                    ("$payload", payloadId),
                    ("$edge", edge.EdgeId.Value),
                    ("$artifact", edge.ToId.Value),
                    ("$kind", edge.EdgeKind));
            }
            InsertAuditEvent("scope-reversion-analysis-published", "scope-reversion-analysis", payloadId,
                request.CreatedAt, transaction);
            ScopeReversionPersistenceReceipt receipt = Receipt(payloadId, transaction);
            transaction.Commit();
            return receipt;
        }
    }

    public byte[] ReadScopeReversionAnalysisBytes(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            return ReadScopeReversionBytesCore(payloadId, null);
        }
    }

    public byte[] GetScopeReversionArtifact(string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT kind,content_sha256,byte_length,artifact_bytes FROM scope_reversion_artifacts WHERE artifact_id=$id ORDER BY payload_id;";
            command.Parameters.AddWithValue("$id", artifactId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Scope-reversion artifact '{artifactId}' does not exist.");
            }
            string kind = reader.GetString(0);
            string sha = reader.GetString(1);
            long length = reader.GetInt64(2);
            byte[] bytes = (byte[])reader[3];
            if (bytes.LongLength != length || Hash(bytes) != sha)
            {
                throw new InvalidDataException("Scope-reversion artifact failed retained-byte validation.");
            }
            while (reader.Read())
            {
                byte[] duplicate = (byte[])reader[3];
                if (reader.GetString(0) != kind || reader.GetString(1) != sha || reader.GetInt64(2) != length
                    || !duplicate.AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException("A scope-reversion artifact identity resolves to different retained bytes.");
                }
            }
            return bytes;
        }
    }

    public IReadOnlyList<ScopeReversionArtifactRecord> ListScopeReversionArtifacts(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT artifact_id,payload_id,kind,content_sha256,byte_length FROM scope_reversion_artifacts WHERE payload_id=$id ORDER BY artifact_id;";
            command.Parameters.AddWithValue("$id", payloadId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ScopeReversionArtifactRecord> records = [];
            while (reader.Read())
            {
                records.Add(new ScopeReversionArtifactRecord(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetInt64(4)));
            }
            return records;
        }
    }

    public IReadOnlyList<ScopeReversionInvalidationRecord> InvalidateScopeReversionDependency(
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
                query.CommandText =
                    "SELECT DISTINCT payload_id FROM scope_reversion_dependencies WHERE artifact_id=$id ORDER BY payload_id;";
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
                    "INSERT INTO scope_reversion_invalidations(payload_id,dependency_id,reason,invalidated_at) VALUES($payload,$dependency,$reason,$now);",
                    transaction,
                    ("$payload", payloadId), ("$dependency", dependencyId),
                    ("$reason", reason), ("$now", ToText(now)));
            }
            transaction.Commit();
            return payloadIds.Select(payloadId => new ScopeReversionInvalidationRecord(
                payloadId, dependencyId, reason, now)).ToArray();
        }
    }

    public IReadOnlyList<ScopeReversionInvalidationRecord> ReadScopeReversionInvalidations(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT dependency_id,reason,invalidated_at FROM scope_reversion_invalidations WHERE payload_id=$id ORDER BY invalidation_id;";
            command.Parameters.AddWithValue("$id", payloadId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ScopeReversionInvalidationRecord> records = [];
            while (reader.Read())
            {
                records.Add(new ScopeReversionInvalidationRecord(
                    payloadId,
                    reader.GetString(0),
                    reader.GetString(1),
                    DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture)));
            }
            return records;
        }
    }

    private void ApplyScopeReversionMigration()
    {
        string sourceFingerprint = ComputeSchemaFingerprint(connection);
        if (sourceFingerprint != ScopeReversionPersistenceDeclarations.SourceSchemaFingerprint)
        {
            throw new InvalidOperationException(
                "The Slice 7 persistence migration source is not the exact accepted schema-9 state.");
        }
        using SqliteTransaction transaction = BeginTransaction();
        Execute(
            """
            CREATE TABLE scope_reversion_analyses(
              payload_id TEXT PRIMARY KEY,
              run_id TEXT NOT NULL,
              assignment_id TEXT NOT NULL,
              input_fingerprint TEXT NOT NULL CHECK(length(input_fingerprint)=64),
              analyzer_declaration_sha256 TEXT NOT NULL CHECK(length(analyzer_declaration_sha256)=64),
              payload_sha256 TEXT NOT NULL CHECK(length(payload_sha256)=64),
              payload_byte_length INTEGER NOT NULL CHECK(payload_byte_length BETWEEN 1 AND 67108864),
              semantic_fingerprint TEXT NOT NULL CHECK(length(semantic_fingerprint)=64),
              payload_bytes BLOB NOT NULL,
              created_at TEXT NOT NULL,
              UNIQUE(run_id,assignment_id)
            ) STRICT;
            CREATE TABLE scope_reversion_artifacts(
              artifact_id TEXT NOT NULL,
              payload_id TEXT NOT NULL REFERENCES scope_reversion_analyses(payload_id),
              kind TEXT NOT NULL CHECK(length(kind) BETWEEN 1 AND 64),
              content_sha256 TEXT NOT NULL CHECK(length(content_sha256)=64),
              byte_length INTEGER NOT NULL CHECK(byte_length BETWEEN 1 AND 16777216 OR kind='scope-reversion-analysis'),
              artifact_bytes BLOB NOT NULL,
              PRIMARY KEY(payload_id,artifact_id)
            ) STRICT;
            CREATE TABLE scope_reversion_dependencies(
              payload_id TEXT NOT NULL REFERENCES scope_reversion_analyses(payload_id),
              edge_id TEXT NOT NULL,
              artifact_id TEXT NOT NULL,
              edge_kind TEXT NOT NULL,
              PRIMARY KEY(payload_id,edge_id),
              FOREIGN KEY(payload_id,artifact_id)
                REFERENCES scope_reversion_artifacts(payload_id,artifact_id)
            ) STRICT;
            CREATE INDEX scope_reversion_artifacts_identity_idx
              ON scope_reversion_artifacts(artifact_id,payload_id);
            CREATE INDEX scope_reversion_dependencies_artifact_idx
              ON scope_reversion_dependencies(artifact_id,payload_id);
            CREATE TABLE scope_reversion_invalidations(
              invalidation_id INTEGER PRIMARY KEY AUTOINCREMENT,
              payload_id TEXT NOT NULL REFERENCES scope_reversion_analyses(payload_id),
              dependency_id TEXT NOT NULL,
              reason TEXT NOT NULL CHECK(length(reason) BETWEEN 1 AND 512),
              invalidated_at TEXT NOT NULL
            ) STRICT;
            """,
            transaction);
        CreateAppendOnlyTriggers(
            ["scope_reversion_analyses", "scope_reversion_artifacts", "scope_reversion_dependencies", "scope_reversion_invalidations"],
            transaction);
        string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (schemaFingerprint != ScopeReversionPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException(
                $"The Slice 7 persistence migration produced unexpected schema fingerprint '{schemaFingerprint}'.");
        }
        Execute(
            """
            UPDATE store_metadata SET value='10' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.9.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO migration_history(migration_id,from_version,to_version,applied_at,sqlite_source_id)
            VALUES('M1-S7-WP5-0010',9,10,$now,$source);
            PRAGMA user_version=10;
            """,
            transaction,
            ("$fingerprint", schemaFingerprint),
            ("$now", ToText(DateTimeOffset.UtcNow)),
            ("$source", BindingIdentity.SourceId));
        transaction.Commit();
    }

    private void ValidateScopeReversionMigration()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('scope_reversion_analyses','scope_reversion_artifacts','scope_reversion_dependencies','scope_reversion_invalidations');";
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 4
            || ComputeSchemaFingerprint(connection) != ScopeReversionPersistenceDeclarations.SchemaFingerprint)
        {
            throw new InvalidOperationException("Schema 10 lacks the exact Slice 7 scope-reversion persistence migration.");
        }
    }

    private void InsertArtifact(
        string artifactId,
        string payloadId,
        string kind,
        ReadOnlySpan<byte> bytes,
        SqliteTransaction transaction)
    {
        using (SqliteCommand existing = connection.CreateCommand())
        {
            existing.Transaction = transaction;
            existing.CommandText =
                "SELECT kind,content_sha256,byte_length,artifact_bytes FROM scope_reversion_artifacts WHERE artifact_id=$artifact;";
            existing.Parameters.AddWithValue("$artifact", artifactId);
            using SqliteDataReader reader = existing.ExecuteReader();
            string sha = Hash(bytes);
            while (reader.Read())
            {
                if (reader.GetString(0) != kind
                    || reader.GetString(1) != sha
                    || reader.GetInt64(2) != bytes.Length
                    || !((byte[])reader[3]).AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException(
                        "A scope-reversion artifact identity already resolves to different retained bytes.");
                }
            }
        }
        Execute(
            "INSERT INTO scope_reversion_artifacts(artifact_id,payload_id,kind,content_sha256,byte_length,artifact_bytes) VALUES($artifact,$payload,$kind,$sha,$length,$bytes);",
            transaction,
            ("$artifact", artifactId), ("$payload", payloadId), ("$kind", kind),
            ("$sha", Hash(bytes)), ("$length", bytes.Length), ("$bytes", bytes.ToArray()));
    }

    private void ValidateExistingArtifact(
        string payloadId,
        ScopeReversionRetainedArtifact artifact,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT kind,content_sha256,byte_length,artifact_bytes FROM scope_reversion_artifacts WHERE payload_id=$payload AND artifact_id=$artifact;";
        command.Parameters.AddWithValue("$payload", payloadId);
        command.Parameters.AddWithValue("$artifact", artifact.ArtifactId);
        using SqliteDataReader reader = command.ExecuteReader();
        string sha = Hash(artifact.Bytes.Span);
        if (!reader.Read()
            || reader.GetString(0) != artifact.Kind
            || reader.GetString(1) != sha
            || reader.GetInt64(2) != artifact.Bytes.Length
            || !((byte[])reader[3]).AsSpan().SequenceEqual(artifact.Bytes.Span)
            || reader.Read())
        {
            throw new InvalidDataException(
                "A duplicate scope-reversion publication supplies different retained dependency bytes.");
        }
    }

    private byte[] ReadScopeReversionBytesCore(string payloadId, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT payload_sha256,payload_byte_length,payload_bytes FROM scope_reversion_analyses WHERE payload_id=$id;";
        command.Parameters.AddWithValue("$id", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Scope-reversion payload '{payloadId}' does not exist.");
        }
        string sha = reader.GetString(0);
        long length = reader.GetInt64(1);
        byte[] bytes = (byte[])reader[2];
        if (bytes.LongLength != length || Hash(bytes) != sha)
        {
            throw new InvalidDataException("Scope-reversion analysis failed retained-byte validation.");
        }
        return bytes;
    }

    private ScopeReversionPersistenceReceipt Receipt(string payloadId, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT run_id,assignment_id,payload_sha256,payload_byte_length,semantic_fingerprint FROM scope_reversion_analyses WHERE payload_id=$id;";
        command.Parameters.AddWithValue("$id", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Scope-reversion payload '{payloadId}' does not exist.");
        }
        string runId = reader.GetString(0);
        string assignmentId = reader.GetString(1);
        string sha = reader.GetString(2);
        long length = reader.GetInt64(3);
        string semantic = reader.GetString(4);
        reader.Close();
        string[] artifacts;
        using (SqliteCommand list = connection.CreateCommand())
        {
            list.Transaction = transaction;
            list.CommandText = "SELECT artifact_id FROM scope_reversion_artifacts WHERE payload_id=$id ORDER BY artifact_id;";
            list.Parameters.AddWithValue("$id", payloadId);
            using SqliteDataReader items = list.ExecuteReader();
            List<string> ids = [];
            while (items.Read())
            {
                ids.Add(items.GetString(0));
            }
            artifacts = ids.ToArray();
        }
        return new ScopeReversionPersistenceReceipt(
            payloadId, runId, assignmentId, sha, length, semantic, artifacts);
    }
}
