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
    public bool HasRecoverablePublication(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT p.content_sha256, p.byte_length, p.object_relative_path
                FROM publication_receipts receipt
                JOIN attempts attempt ON attempt.attempt_id = receipt.attempt_id
                JOIN publication_receipt_payloads published
                  ON published.receipt_id = receipt.receipt_id
                JOIN payloads p ON p.payload_id = published.payload_id
                WHERE receipt.run_id = $run
                  AND attempt.outcome = 'completed-staged'
                ORDER BY receipt.published_at, p.payload_id;
                """;
            command.Parameters.AddWithValue("$run", runId);
            using var reader = command.ExecuteReader();
            bool found = false;
            while (reader.Read())
            {
                found = true;
                string expectedSha256 = reader.GetString(0);
                long expectedByteLength = reader.GetInt64(1);
                string relativePath = reader.GetString(2);
                string objectPath = Paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    relativePath["payloads/".Length..]
                        .Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(objectPath))
                {
                    return false;
                }

                var fileInfo = new FileInfo(objectPath);
                if (fileInfo.Length != expectedByteLength
                    || !string.Equals(
                        HashFile(objectPath),
                        expectedSha256,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return found;
        }
    }

    public IReadOnlyList<ReconciliationIssue> ReconcilePayloadStore()
    {
        lock (gate)
        {
            var issues = new List<ReconciliationIssue>();
            var known = new Dictionary<string, (string Sha, long Length, string Retention)>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT object_relative_path, content_sha256, byte_length, retention_state FROM payloads;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    known[reader.GetString(0).Replace('/', Path.DirectorySeparatorChar)] =
                        (reader.GetString(1), reader.GetInt64(2), reader.GetString(3));
                }
            }

            foreach (var entry in known)
            {
                var fullPath = Paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    entry.Key["payloads".Length..]
                        .TrimStart(Path.DirectorySeparatorChar));
                bool exists = File.Exists(fullPath);
                if (StringComparer.Ordinal.Equals(entry.Value.Retention, "deleted"))
                {
                    if (exists)
                    {
                        issues.Add(new ReconciliationIssue(
                            "deleted-payload-present",
                            entry.Key,
                            "Deleted payload bytes remain physically present."));
                    }
                    continue;
                }
                if (!exists)
                {
                    issues.Add(new ReconciliationIssue("missing-payload", entry.Key, "Registered payload is absent."));
                    continue;
                }

                var info = new FileInfo(fullPath);
                if (info.Length != entry.Value.Length
                    || !string.Equals(HashFile(fullPath), entry.Value.Sha, StringComparison.Ordinal))
                {
                    issues.Add(new ReconciliationIssue("corrupt-payload", entry.Key, "Size or digest mismatch."));
                }
            }

            if (Directory.Exists(Paths.Payloads))
            {
                foreach (var file in Directory.EnumerateFiles(Paths.Payloads, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(Paths.ProductRoot, file);
                    if (!known.ContainsKey(relative))
                    {
                        issues.Add(new ReconciliationIssue("orphan-payload", relative, "No authoritative owner."));
                    }
                }
            }

            foreach (var file in Directory.EnumerateFiles(Paths.Staging, "*", SearchOption.AllDirectories))
            {
                issues.Add(new ReconciliationIssue(
                    "orphan-staging",
                    Path.GetRelativePath(Paths.ProductRoot, file),
                    "Staging data is never authoritative."));
            }

            return issues;
        }
    }

    public void RebuildProjections(DateTimeOffset now)
    {
        lock (gate)
        {
            using var transaction = BeginTransaction();
            Execute("DELETE FROM run_projection;", transaction);
            Execute(
                """
                INSERT INTO run_projection(
                    run_id, lifecycle_state, lifecycle_generation, durable_sequence,
                    projection_version, updated_at)
                SELECT run_id, lifecycle_state, lifecycle_generation, durable_sequence,
                       1, $now
                FROM runs;
                """,
                transaction,
                ("$now", ToText(now)));
            Execute("DELETE FROM review_projection; DELETE FROM assumption_projection;", transaction);
            Execute(
                """
                INSERT INTO review_projection(
                    subject_occurrence_id,run_id,subject_kind,revision,disposition,suppressed,
                    annotation,last_event_id,updated_at)
                SELECT event.subject_occurrence_id,event.run_id,event.subject_kind,event.revision,
                       event.disposition,event.suppressed,event.annotation,event.event_id,event.created_at
                FROM review_events event
                WHERE event.revision=(SELECT MAX(latest.revision) FROM review_events latest
                    WHERE latest.subject_occurrence_id=event.subject_occurrence_id);

                INSERT INTO assumption_projection(
                    assumption_id,profile_id,revision,origin,confirmation,subject,value,scope,
                    dependency_ids_json,effective,analysis_context_id,last_event_id,updated_at)
                SELECT event.assumption_id,event.profile_id,event.revision,event.origin,event.confirmation,
                       event.subject,event.value,event.scope,event.dependency_ids_json,event.effective,
                       event.analysis_context_id,event.event_id,event.created_at
                FROM assumption_events event
                WHERE event.revision=(SELECT MAX(latest.revision) FROM assumption_events latest
                    WHERE latest.assumption_id=event.assumption_id);
                """,
                transaction);
            transaction.Commit();
        }
    }

}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
