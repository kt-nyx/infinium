using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class M1Slice6SuccessorAuthorityTests
{
    [TestMethod]
    public void CheckedInSuccessorAuthorityIsSchemaValidAndBindsTheReviewedSnapshot()
    {
        string repository = RepositoryRoot();
        string path = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-successor-campaign-authorization.v5.json");
        string sha = M1Slice6SuccessorAuthorityLoader.HashFile(path);
        string schema = Path.Combine(repository, "contracts", "repository",
            "m1-slice6-successor-campaign-authorization.v5.schema.json");
        ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(path), File.ReadAllBytes(schema),
            "infinium.repository.m1-slice6-successor-campaign-authorization/5.0.0");
        M1Slice6SuccessorCampaignAuthority authority =
            M1Slice6SuccessorAuthorityLoader.Campaign(path, sha);
        Assert.AreEqual("infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1",
            authority.CampaignId);
        Assert.AreEqual("e6788d546308a8ec8f7c3374c52cf8700a7a2245f52d213587e6a84d1d779b0d",
            authority.CredentialAccessAuthoritySha256);
        string reviewPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-successor-campaign-independent-review.v1.json");
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "campaign-authority", authority.CampaignId, sha, false);
        Assert.AreEqual("/root/successor-authority-review/campaign-final-20260820", review.ReviewId);
    }

    [TestMethod]
    public void IndependentReviewMustBeClosedAcceptedAndBindExactSubject()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".successor-review-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "review.json");
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_identity = M1Slice6SuccessorAuthorityLoader.IndependentReviewSchema,
                review_id = "/root/review/independent-review-test",
                review_kind = "campaign-authority",
                verdict = "accept",
                reviewer_id = "/root/successor-authority-review",
                independent = true,
                provider_effect_used = false,
                subject = new { id = "campaign-test", sha256 = new string('a', 64) },
                correction = new
                {
                    required = false,
                    defect_id = (string?)null,
                    diagnosis_disposition = (string?)null,
                    failure_evidence_id = (string?)null,
                    failure_evidence_sha256 = (string?)null,
                    candidate_commit = (string?)null
                },
                findings = Array.Empty<string>(),
                reviewed_at_utc = "2026-08-20T18:00:00.0000000+00:00",
            });
            File.WriteAllBytes(path, bytes);
            M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
                path, "campaign-authority", "campaign-test", new string('a', 64), false);
            Assert.AreEqual("/root/review/independent-review-test", review.ReviewId);
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Review(
                path, "campaign-authority", "different-campaign", new string('a', 64), false));
            File.WriteAllText(Path.Combine(directory, "prose.md"), "accepted");
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Review(
                Path.Combine(directory, "prose.md"), "campaign-authority", "campaign-test",
                new string('a', 64), false));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ReviewedTemporaryAuthorityVerifiesEverySnapshotOriginByteWithoutAnyProviderEffect()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".successor-snapshot-authority-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
            JsonObject access = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-credential-access.v1.json")))!.AsObject();
            access["status"] = "reviewed-and-admitted";
            string accessPath = Path.Combine(directory, "access.json");
            File.WriteAllText(accessPath, access.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string accessSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(accessPath)));

            JsonObject campaign = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-campaign-authorization.v5.json")))!.AsObject();
            campaign["status"] = "owner-authorized-reviewed-and-admitted";
            string accessRelative = Path.GetRelativePath(repository, accessPath).Replace('\\', '/');
            campaign["credential_inheritance"]!["access_authority_path"] = accessRelative;
            campaign["credential_inheritance"]!["access_authority_sha256"] = accessSha;
            string campaignPath = Path.Combine(directory, "campaign.json");
            File.WriteAllText(campaignPath, campaign.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string campaignSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath)));

            M1Slice6SuccessorCampaignAuthority loaded = M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha);
            Assert.AreEqual(accessSha, loaded.CredentialAccessAuthoritySha256);
            Assert.AreEqual("e3d23f0a11d66c243fd857e66e741d957abb8d470b45d653ae53f17d74fe4945",
                loaded.ProductStateSnapshotOriginSha256);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void InitialSnapshotAllowsOnlyTransientSqliteSharedMemoryDrift()
    {
        string repository = RepositoryRoot();
        string directory = Path.Combine(repository, ".successor-transient-snapshot-test-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "source");
        string destination = Path.Combine(directory, "destination");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);
        try
        {
            string slice = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6");
            JsonObject checkedInAccess = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-credential-access.v1.json")))!.AsObject();
            string checkedInSource = checkedInAccess["retained_product_state"]!["source_root_absolute"]!.GetValue<string>();
            JsonObject checkedInOrigin = JsonNode.Parse(File.ReadAllText(Path.Combine(
                checkedInAccess["retained_product_state"]!["successor_root_absolute"]!.GetValue<string>(),
                "successor-snapshot-origin.v1.json")))!.AsObject();
            foreach (JsonNode? item in checkedInOrigin["files"]!.AsArray())
            {
                string relative = item!["path"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar);
                string sourceFile = Path.Combine(checkedInSource, relative);
                string copiedSource = Path.Combine(source, relative);
                string copiedDestination = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copiedSource)!);
                Directory.CreateDirectory(Path.GetDirectoryName(copiedDestination)!);
                File.Copy(sourceFile, copiedSource);
                File.Copy(sourceFile, copiedDestination);
            }
            checkedInOrigin["source_root"] = source;
            checkedInOrigin["destination_root"] = destination;
            string originPath = Path.Combine(destination, "successor-snapshot-origin.v1.json");
            File.WriteAllText(originPath, checkedInOrigin.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            string sharedMemory = Path.Combine(destination, "data", "infinium.sqlite3-shm");
            byte[] sharedMemoryBytes = File.ReadAllBytes(sharedMemory);
            sharedMemoryBytes[0] ^= 0xff;
            File.WriteAllBytes(sharedMemory, sharedMemoryBytes);

            JsonObject access = checkedInAccess.DeepClone().AsObject();
            access["retained_product_state"]!["source_root_absolute"] = source;
            access["retained_product_state"]!["successor_root_absolute"] = destination;
            access["retained_product_state"]!["snapshot_origin_sha256"] =
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(originPath)));
            string accessPath = Path.Combine(directory, "access.json");
            File.WriteAllText(accessPath, access.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            string accessSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(accessPath)));

            JsonObject campaign = JsonNode.Parse(File.ReadAllText(Path.Combine(slice,
                "m1-slice6-successor-campaign-authorization.v5.json")))!.AsObject();
            campaign["credential_inheritance"]!["access_authority_path"] =
                Path.GetRelativePath(repository, accessPath).Replace('\\', '/');
            campaign["credential_inheritance"]!["access_authority_sha256"] = accessSha;
            string campaignPath = Path.Combine(directory, "campaign.json");
            File.WriteAllText(campaignPath, campaign.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            _ = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath))));

            string retainedPayload = Directory.GetFiles(Path.Combine(destination, "payloads"), "*", SearchOption.AllDirectories).Single();
            File.AppendAllText(retainedPayload, "changed");
            Assert.ThrowsExactly<InvalidDataException>(() => M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath,
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(campaignPath)))));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ProductStateCheckpointIsDeterministicAndDetectsDatabaseOrRetainedFileMutation()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-successor-state-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "data"));
        Directory.CreateDirectory(Path.Combine(root, "payloads"));
        try
        {
            string database = Path.Combine(root, "data", "infinium.sqlite3");
            using (SqliteConnection connection = new($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE exact_state(id INTEGER PRIMARY KEY,value TEXT NOT NULL); INSERT INTO exact_state VALUES(1,'one');";
                command.ExecuteNonQuery();
            }
            string retained = Path.Combine(root, "payloads", "retained");
            File.WriteAllText(retained, "first");
            string first = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root);
            Assert.AreEqual(first, M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root));
            using (SqliteConnection connection = new($"Data Source={database};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO exact_state VALUES(2,'two');";
                command.ExecuteNonQuery();
            }
            string databaseChanged = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root);
            Assert.AreNotEqual(first, databaseChanged);
            File.WriteAllText(retained, "second");
            Assert.AreNotEqual(databaseChanged,
                M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string RepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "Infinium.sln"))) { return current; }
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
