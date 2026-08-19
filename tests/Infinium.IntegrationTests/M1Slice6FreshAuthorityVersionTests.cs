using System.Text.Json;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class M1Slice6FreshAuthorityVersionTests
{
    private const string FreshCampaign =
        "infinium.m1-s6.finite-live-campaign/ff2d542a-04f0-448a-bcb8-a0ecbedde5b9";
    private const string FreshCredential =
        "infinium.m1-s6.wp9.production-profile-authorization/234b5227-0ad4-4f5e-acf5-5ac6b89fca2b";

    [TestMethod]
    public void ExternalEffectRequiresFreshV4CampaignAndCredentialAuthority()
    {
        ProviderEffectRuntimeAuthority external = Authority(ProviderEffectAuthorityScope.ExternalEffect);

        M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
            M1Slice6AuthorityContractVersion.FreshC2V4,
            M1Slice6AuthorityContractVersion.FreshC2V4,
            FreshCampaign, FreshCredential);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.RetiredV2,
                M1Slice6AuthorityContractVersion.RetiredV2,
                FreshCampaign, FreshCredential));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.RetiredC2V3,
                M1Slice6AuthorityContractVersion.RetiredC2V3,
                M1Slice6AuthorityContracts.RetiredC2CampaignId,
                M1Slice6AuthorityContracts.RetiredC2CredentialManifestId));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                M1Slice6AuthorityContracts.RetiredCampaignId, FreshCredential));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                FreshCampaign, M1Slice6AuthorityContracts.RetiredCredentialManifestId));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                M1Slice6AuthorityContracts.RetiredC2CampaignId, FreshCredential));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                M1Slice6AuthorityContractVersion.FreshC2V4,
                FreshCampaign, M1Slice6AuthorityContracts.RetiredC2CredentialManifestId));
    }

    [TestMethod]
    public void EffectFreeRehearsalRetainsHistoricalV2EvidenceCompatibility()
    {
        M1Slice6AuthorityContracts.RequireFreshExternalEffect(
            Authority(ProviderEffectAuthorityScope.EffectFreeRehearsal),
            M1Slice6AuthorityContractVersion.RetiredV2,
            M1Slice6AuthorityContractVersion.RetiredV2,
            M1Slice6AuthorityContracts.RetiredCampaignId,
            M1Slice6AuthorityContracts.RetiredCredentialManifestId);
    }

    [TestMethod]
    public void FreshEvidenceSchemasAreVersionCoupled()
    {
        Assert.AreEqual("infinium.m1-s6.campaign-stage-evidence/v3",
            M1Slice6AuthorityContracts.StageEvidenceSchema(M1Slice6AuthorityContractVersion.RetiredC2V3));
        Assert.AreEqual("infinium.m1-s6.campaign-stage-evidence/v4",
            M1Slice6AuthorityContracts.StageEvidenceSchema(M1Slice6AuthorityContractVersion.FreshC2V4));
        Assert.AreEqual("infinium.m1-s6.campaign-composed-evidence/v3",
            M1Slice6AuthorityContracts.ComposedEvidenceSchema(M1Slice6AuthorityContractVersion.RetiredC2V3));
        Assert.AreEqual("infinium.m1-s6.campaign-composed-evidence/v4",
            M1Slice6AuthorityContracts.ComposedEvidenceSchema(M1Slice6AuthorityContractVersion.FreshC2V4));
        Assert.AreEqual("infinium.m1-s6.campaign-stage-evidence/v2",
            M1Slice6AuthorityContracts.StageEvidenceSchema(M1Slice6AuthorityContractVersion.RetiredV2));
    }

    [TestMethod]
    public void FreshV4SchemasBindOnlyTheNewCampaignCredentialProfileAndStages()
    {
        string root = RepositoryRoot();
        string repositoryContracts = Path.Combine(root, "contracts", "repository");
        string[] files =
        [
            "wp9-production-profile-authorization.v4.schema.json",
            "m1-slice6-finite-campaign-authorization.v4.schema.json",
            "m1-slice6-campaign-stage-request.v4.schema.json",
            "m1-slice6-campaign-stage-evidence.v4.schema.json",
            "m1-slice6-campaign-composed-evidence.v4.schema.json",
        ];
        string combined = string.Join('\n', files.Select(file =>
        {
            string text = File.ReadAllText(Path.Combine(repositoryContracts, file));
            using JsonDocument _ = JsonDocument.Parse(text);
            return text;
        }));

        StringAssert.Contains(combined, M1Slice6AuthorityContracts.RetiredCampaignId);
        StringAssert.Contains(combined, M1Slice6AuthorityContracts.RetiredCredentialManifestId);
        StringAssert.Contains(combined, M1Slice6AuthorityContracts.RetiredC2CampaignId);
        StringAssert.Contains(combined, M1Slice6AuthorityContracts.RetiredC2CredentialManifestId);
        StringAssert.Contains(combined, "prohibited-reserved-or-terminal-history-only");
        StringAssert.Contains(combined, FreshCampaign);
        StringAssert.Contains(combined, FreshCredential);
        StringAssert.Contains(combined,
            "infinium.m1-s6.campaign-stage/Qualification/cf3ba7b9-e2cb-427d-b5cb-ae9f679c19c1");
        StringAssert.Contains(combined,
            "infinium.m1-s6.campaign-stage/SourceClaimExtraction/5eae2494-e3fe-41a7-8b3d-ed11148744e8");
        StringAssert.Contains(combined,
            "infinium.m1-s6.campaign-stage/CandidateInvestigation/a9d1346f-ef84-44dc-a482-266b8e089dce");
    }

    private static ProviderEffectRuntimeAuthority Authority(ProviderEffectAuthorityScope scope) => new(
        "authority", scope, ProviderEffectAuthorityKind.CredentialEnrollment,
        "subject", new('a', 64), FreshCampaign, new('b', 64),
        "none", "none", "none", new('c', 40), new('d', 64), new('e', 64),
        "review", new('f', 64), "owner", new('1', 64),
        DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1),
        new("output", "ledger", "state", "coordinator", "helper"),
        new(1, 4, 0, 0, 0, 0, false, false), new('2', 64));

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
