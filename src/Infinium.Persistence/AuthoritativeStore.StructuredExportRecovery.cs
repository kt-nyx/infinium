using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed partial class AuthoritativeStore
{
    private void RecoverStructuredExportArtifacts(DateTimeOffset now)
    {
        lock (gate)
        {
            List<(string ExportId, string RequestSha, string EventId)> pending = [];
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT projection.export_id,event.request_sha256,event.event_id
                    FROM structured_export_projection projection
                    JOIN structured_export_events event ON event.event_id=projection.last_event_id
                    WHERE projection.state='deletion-pending'
                    ORDER BY projection.export_id;
                    """;
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    pending.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                }
            }
            foreach ((string exportId, string requestSha, string eventId) in pending)
            {
                CompleteStructuredExportDeletion(exportId, requestSha, "recovery-" + eventId, now);
            }

            HashSet<string> retainedPaths = [];
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT artifact_relative_path FROM structured_exports;";
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    retainedPaths.Add(reader.GetString(0));
                }
            }
            foreach (string fullPath in Directory.EnumerateFiles(
                         Paths.Exports,
                         "structured-export-*.json",
                         SearchOption.TopDirectoryOnly))
            {
                string relativePath = Path.GetFileName(fullPath);
                if (!retainedPaths.Contains(relativePath))
                {
                    Paths.DeleteFile(ProductWriteClass.Export, relativePath, missingIsSuccess: true);
                    using SqliteTransaction transaction = BeginTransaction();
                    InsertAuditEvent(
                        "structured-export-orphan-recovered",
                        "structured-export-artifact",
                        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(relativePath))),
                        now,
                        transaction);
                    transaction.Commit();
                }
            }
        }
    }
}
