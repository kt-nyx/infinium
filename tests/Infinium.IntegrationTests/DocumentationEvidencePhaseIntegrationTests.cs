using System.Security.Cryptography;
using System.Text;
using Infinium.Application.Documentation;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DocumentationEvidencePhaseIntegrationTests
{
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void PhasePublishesReadbackAndRetainedDeletionReuseAtomically()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-documentation-{Guid.NewGuid():N}");
        AuthoritativeStore? store = null;
        StoragePaths? paths = null;
        try
        {
            paths = new(root);
            store = new(paths);
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "documentation-integration", Now, TimeSpan.FromMinutes(10));
            CreateRun(store, authority, "run-clean", "command-clean");
            CreateRun(store, authority, "run-reuse", "command-reuse");
            CreateRun(store, authority, "run-drift", "command-drift");
            CreateRun(store, authority, "run-invalid-context", "command-invalid-context");
            CreateRun(store, authority, "run-reextract", "command-reextract");

            DocumentationImportRequestContract cleanRequest = Request("run-clean");
            DocumentationApplicationInputContract firstApplication = cleanRequest.Manifest.Applications.Single();
            cleanRequest = cleanRequest with
            {
                Manifest = cleanRequest.Manifest with
                {
                    Applications =
                    [
                        firstApplication,
                        firstApplication with { SubjectId = Id("entity-2") },
                    ],
                },
                AcceptedApplicationTargets =
                [
                    .. cleanRequest.AcceptedApplicationTargets,
                    cleanRequest.AcceptedApplicationTargets.Single() with { SubjectId = Id("entity-2") },
                ],
            };
            DocumentationEvidencePhaseResult clean = DocumentationEvidencePhase.Execute(store, cleanRequest);
            CollectionAssert.AreEqual(
                clean.SerializedPayload,
                store.ReadDocumentationEvidencePayload(clean.Receipt.PayloadId));
            DocumentationEvidenceContract cleanReadback = DocumentationEvidenceJsonCodec.Deserialize(
                store.ReadDocumentationEvidencePayload(clean.Receipt.PayloadId));
            Assert.AreEqual(clean.Evidence.PayloadId, cleanReadback.PayloadId);
            Assert.HasCount(2, clean.Evidence.Applications);
            Assert.AreEqual(2L, Count(paths.Database, "documentation_application_bindings"));
            string[] deletableObjectPaths = ReadDocumentationObjectPaths(paths.Database);
            Assert.IsTrue(deletableObjectPaths.All(path => File.Exists(Path.Combine(root, path))));

            DocumentationImportRequestContract invalidContextRequest = Request("run-invalid-context");
            DocumentationApplicationInputContract invalidContextApplication =
                invalidContextRequest.Manifest.Applications.Single();
            invalidContextRequest = invalidContextRequest with
            {
                Manifest = invalidContextRequest.Manifest with
                {
                    SourceRevision = "fixture-r2",
                    Applications =
                    [
                        invalidContextApplication with { AnalysisContextId = Id("unbound-context") },
                    ],
                },
                AcceptedApplicationTargets =
                [
                    invalidContextRequest.AcceptedApplicationTargets.Single() with
                    {
                        AnalysisContextId = Id("unbound-context"),
                    },
                ],
            };
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocumentationEvidencePhase.Execute(store, invalidContextRequest));
            Assert.AreEqual(1L, Count(paths.Database, "documentation_imports"));

            DocumentationEvidencePhaseResult reextracted = DocumentationEvidencePhase.Execute(
                store,
                Request("run-reextract"));
            Assert.AreEqual(
                clean.Evidence.Revisions.Single().RevisionId,
                reextracted.Evidence.Revisions.Single().RevisionId);
            CollectionAssert.AreNotEquivalent(
                clean.Evidence.Claims.Select(item => item.ClaimId).ToArray(),
                reextracted.Evidence.Claims.Select(item => item.ClaimId).ToArray());
            Assert.IsTrue(reextracted.Evidence.Claims.All(item =>
                item.ProducingImportId == reextracted.Evidence.Imports.Single().ImportId));

            DocumentationEvidenceContract identityDrift = clean.Evidence with
            {
                Revisions = [clean.Evidence.Revisions.Single() with { SourceRevision = "fixture-r1-drift" }],
            };
            identityDrift = identityDrift with
            {
                PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(identityDrift),
            };
            DocumentationImportRequestContract driftRequest = Request("run-drift") with
            {
                Mode = DocumentationImportMode.RetainedReuse,
                SourceBytes = null,
                RetainedEvidence = identityDrift,
                Manifest = cleanRequest.Manifest with
                {
                    SourceRevision = "fixture-r1-drift",
                    Claims = [],
                    Applications = [],
                },
                AcceptedApplicationTargets = [],
            };
            Assert.ThrowsExactly<InvalidDataException>(() =>
                DocumentationEvidencePhase.Execute(store, driftRequest));
            Assert.AreEqual(2L, Count(paths.Database, "documentation_imports"));
            Assert.IsTrue(store.ReconcilePayloadStore().Any(issue => issue.Kind == "orphan-payload"));

            DocumentationImportRequestContract reuseRequest = Request("run-reuse") with
            {
                Mode = DocumentationImportMode.RetainedReuse,
                SourceBytes = null,
                RetainedEvidence = clean.Evidence,
                Manifest = cleanRequest.Manifest with
                {
                    Availability = DocumentationSourceAvailability.Deleted,
                    Claims = [],
                    Applications = [],
                },
                AcceptedApplicationTargets = [],
            };
            DocumentationEvidencePhaseResult reuse = DocumentationEvidencePhase.Execute(store, reuseRequest);
            DocumentationEvidenceContract reuseReadback = DocumentationEvidenceJsonCodec.Deserialize(
                store.ReadDocumentationEvidencePayload(reuse.Receipt.PayloadId));

            Assert.AreEqual(reuse.Evidence.PayloadId, reuseReadback.PayloadId);
            Assert.IsTrue(reuseReadback.Gaps.Any(item => item.Kind == DocumentationGapKind.Deletion));
            Assert.HasCount(1, reuseReadback.DeletionReceipts);
            Assert.IsTrue(deletableObjectPaths.All(path => !File.Exists(Path.Combine(root, path))));
            Assert.AreEqual(
                deletableObjectPaths.Length,
                CountWhere(paths.Database, "payloads", "retention_state = 'deleted'"));
            Assert.AreEqual(1L, Count(paths.Database, "documentation_revisions"));
            Assert.AreEqual(3L, Count(paths.Database, "documentation_imports"));
            Assert.AreEqual(2L, Count(paths.Database, "documentation_passages"));
            Assert.AreEqual(4L, Count(paths.Database, "evidence_revisions"));
            Assert.AreEqual(3L, Count(paths.Database, "evidence_application_links"));
            Assert.AreEqual(3L, Count(paths.Database, "taxonomy_assignments"));
            Assert.AreEqual(1L, Count(paths.Database, "documentation_deletion_receipts"));
            Assert.AreEqual(2L, Count(paths.Database, "analysis_gaps"));
            Assert.IsTrue(Count(paths.Database, "analysis_dependency_edges") >= 15L);
        }
        finally
        {
            store?.Dispose();
            paths?.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void DeletionReceiptPreservesContentAddressedBytesOwnedByAnotherRevision()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-documentation-shared-{Guid.NewGuid():N}");
        AuthoritativeStore? store = null;
        StoragePaths? paths = null;
        try
        {
            paths = new(root);
            store = new(paths);
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "documentation-shared-integration", Now, TimeSpan.FromMinutes(10));
            CreateRun(store, authority, "run-first", "command-first");
            CreateRun(store, authority, "run-second", "command-second");
            CreateRun(store, authority, "run-delete-first", "command-delete-first");

            DocumentationImportRequestContract firstRequest = Request("run-first");
            DocumentationEvidencePhaseResult first = DocumentationEvidencePhase.Execute(store, firstRequest);
            DocumentationImportRequestContract secondRequest = Request("run-second");
            secondRequest = secondRequest with
            {
                Manifest = secondRequest.Manifest with
                {
                    SourceId = Id("source-2"),
                    SourceRevision = "fixture-r2",
                },
            };
            _ = DocumentationEvidencePhase.Execute(store, secondRequest);
            string[] sharedObjectPaths = ReadDocumentationObjectPaths(paths.Database);

            DocumentationImportRequestContract deleteFirst = Request("run-delete-first") with
            {
                Mode = DocumentationImportMode.RetainedReuse,
                SourceBytes = null,
                RetainedEvidence = first.Evidence,
                Manifest = firstRequest.Manifest with
                {
                    Availability = DocumentationSourceAvailability.Deleted,
                    Claims = [],
                    Applications = [],
                },
                AcceptedApplicationTargets = [],
            };
            DocumentationEvidencePhaseResult deleted = DocumentationEvidencePhase.Execute(store, deleteFirst);

            Assert.IsNotEmpty(deleted.Evidence.DeletionReceipts.Single().IndependentlyRetainedPayloadIds);
            Assert.IsTrue(sharedObjectPaths.All(path => File.Exists(Path.Combine(root, path))));
            Assert.AreEqual(0L, CountWhere(paths.Database, "payloads", "retention_state = 'deleted'"));
            Assert.AreEqual(1L, Count(paths.Database, "documentation_deletion_receipts"));
        }
        finally
        {
            store?.Dispose();
            paths?.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void DeletionReceiptRecordsBackupPinsWhileRemovingMainPayloadCopies()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-documentation-backup-{Guid.NewGuid():N}");
        AuthoritativeStore? store = null;
        StoragePaths? paths = null;
        try
        {
            paths = new(root);
            store = new(paths);
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "documentation-backup-integration", Now, TimeSpan.FromMinutes(10));
            CreateRun(store, authority, "run-clean", "command-clean");
            CreateRun(store, authority, "run-delete", "command-delete");
            DocumentationImportRequestContract cleanRequest = Request("run-clean");
            DocumentationEvidencePhaseResult clean = DocumentationEvidencePhase.Execute(store, cleanRequest);
            string[] mainObjectPaths = ReadDocumentationObjectPaths(paths.Database);

            BackupArtifact backup = store.CreateBackup("documentation", Now);
            string backupPayloadRoot = backup.DatabasePath + ".payloads";
            Assert.IsTrue(Directory.Exists(backupPayloadRoot));
            Assert.IsNotEmpty(Directory.EnumerateFiles(backupPayloadRoot, "*", SearchOption.AllDirectories));
            Assert.IsTrue(Count(paths.Database, "payload_backup_pins") >= mainObjectPaths.Length);

            DocumentationImportRequestContract deletion = Request("run-delete") with
            {
                Mode = DocumentationImportMode.RetainedReuse,
                SourceBytes = null,
                RetainedEvidence = clean.Evidence,
                Manifest = cleanRequest.Manifest with
                {
                    Availability = DocumentationSourceAvailability.Deleted,
                    Claims = [],
                    Applications = [],
                },
                AcceptedApplicationTargets = [],
            };
            DocumentationEvidencePhaseResult deleted = DocumentationEvidencePhase.Execute(store, deletion);

            Assert.IsNotEmpty(deleted.Evidence.DeletionReceipts.Single().IndependentlyRetainedPayloadIds);
            Assert.IsTrue(mainObjectPaths.All(path => !File.Exists(Path.Combine(root, path))));
            Assert.IsNotEmpty(Directory.EnumerateFiles(backupPayloadRoot, "*", SearchOption.AllDirectories));
        }
        finally
        {
            store?.Dispose();
            paths?.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void CreateRun(
        AuthoritativeStore store,
        CoordinatorAuthority authority,
        string runId,
        string commandId) =>
        _ = store.CreateRun(
            commandId,
            runId,
            new RunBinding("snapshot-1", "context-1", "config-1", "manifest-1"),
            authority.FencingEpoch,
            Now);

    private static DocumentationImportRequestContract Request(string runId)
    {
        const string source = "Purpose: Adds a feature.\nRequirement: Component A.\n";
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        const string purposeText = "Purpose: Adds a feature.";
        const string requirementText = "Requirement: Component A.";
        long requirementStart = Encoding.UTF8.GetByteCount(purposeText + "\n");
        DocumentationClaimInputContract purpose = new(
            Id("purpose-key"), 0, Encoding.UTF8.GetByteCount(purposeText), purposeText,
            ClaimKind.DeclaredPurpose, [], EvidenceAuthority.AuthoritativeExternal,
            ClaimApplicabilityState.Applicable, ClassificationRole.Declared, []);
        DocumentationClaimInputContract requirement = new(
            Id("requirement-key"), requirementStart,
            requirementStart + Encoding.UTF8.GetByteCount(requirementText), requirementText,
            ClaimKind.Requirement, [], EvidenceAuthority.AuthoritativeExternal,
            ClaimApplicabilityState.Applicable, ClassificationRole.Declared, []);
        DocumentationApplicationInputContract application = new(
            purpose.ClaimKey, Id(runId), Id("context-1"), Id("entity-1"), "installed-entity",
            Id("closure-1"), ClaimApplicabilityState.Applicable, [requirement.ClaimKey],
            new("purpose.add-expand", [requirement.ClaimKey], Id("documentation-importer"), "exact declared purpose"));
        DocumentationClaimImportManifestContract manifest = new(
            ContractConstants.DocumentationClaimImportSchemaId,
            new ContractVersion(1, 0, 0),
            Id("source-1"),
            DocumentationSourceKind.Fixture,
            "fixture-r1",
            DocumentationSourceAvailability.Present,
            new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(bytes))),
            bytes.Length,
            Id("snapshot-1"),
            [purpose, requirement],
            [application]);
        return new(
            Id(runId), Id(runId), DocumentationImportMode.CleanImport,
            Id("closure-1"), Id("extractor-1"), new UtcTimestamp(Now), manifest, bytes, null,
            [new(
                application.ConsumingRunId,
                Id("snapshot-1"),
                application.AnalysisContextId,
                Id("manifest-1"),
                application.SubjectId,
                application.SubjectType,
                application.DependencyClosureId)]);
    }

    private static long Count(string database, string table)
    {
        using SqliteConnection connection = new($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long CountWhere(string database, string table, string predicate)
    {
        using SqliteConnection connection = new($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {predicate};";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string[] ReadDocumentationObjectPaths(string database)
    {
        using SqliteConnection connection = new($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT DISTINCT payloads.object_relative_path
            FROM payloads
            JOIN payload_owners ON payload_owners.payload_id = payloads.payload_id
            WHERE payload_owners.owner_kind IN ('documentation-revision','documentation-passage')
            ORDER BY payloads.object_relative_path;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> paths = [];
        while (reader.Read())
        {
            paths.Add(reader.GetString(0).Replace('/', Path.DirectorySeparatorChar));
        }
        return paths.ToArray();
    }

    private static OpaqueId Id(string value) => new(value);
}
