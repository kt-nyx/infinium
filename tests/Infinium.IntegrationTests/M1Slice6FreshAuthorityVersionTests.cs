using System.Text.Json;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class M1Slice6FreshAuthorityVersionTests
{
    private const string FreshCampaign =
        "infinium.m1-s6.finite-live-campaign/aef3cd3f-9321-4cdc-86a2-2d61510c2e28";
    private const string FreshCredential =
        "infinium.m1-s6.wp9.production-profile-authorization/52b2cfdb-ccd4-49c0-8f6a-ace8c426012e";

    [TestMethod]
    public void ExternalEffectRequiresFreshV3CampaignAndCredentialAuthority()
    {
        ProviderEffectRuntimeAuthority external = Authority(ProviderEffectAuthorityScope.ExternalEffect);

        M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
            M1Slice6AuthorityContractVersion.FreshC2V3,
            M1Slice6AuthorityContractVersion.FreshC2V3,
            FreshCampaign, FreshCredential);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.RetiredV2,
                M1Slice6AuthorityContractVersion.RetiredV2,
                FreshCampaign, FreshCredential));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.FreshC2V3,
                M1Slice6AuthorityContractVersion.FreshC2V3,
                M1Slice6AuthorityContracts.RetiredCampaignId, FreshCredential));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6AuthorityContracts.RequireFreshExternalEffect(external,
                M1Slice6AuthorityContractVersion.FreshC2V3,
                M1Slice6AuthorityContractVersion.FreshC2V3,
                FreshCampaign, M1Slice6AuthorityContracts.RetiredCredentialManifestId));
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
            M1Slice6AuthorityContracts.StageEvidenceSchema(M1Slice6AuthorityContractVersion.FreshC2V3));
        Assert.AreEqual("infinium.m1-s6.campaign-composed-evidence/v3",
            M1Slice6AuthorityContracts.ComposedEvidenceSchema(M1Slice6AuthorityContractVersion.FreshC2V3));
        Assert.AreEqual("infinium.m1-s6.campaign-stage-evidence/v2",
            M1Slice6AuthorityContracts.StageEvidenceSchema(M1Slice6AuthorityContractVersion.RetiredV2));
    }

    [TestMethod]
    public void FreshV3SchemasBindOnlyTheNewCampaignCredentialProfileAndStages()
    {
        string root = RepositoryRoot();
        string repositoryContracts = Path.Combine(root, "contracts", "repository");
        string[] files =
        [
            "wp9-production-profile-authorization.v3.schema.json",
            "m1-slice6-finite-campaign-authorization.v3.schema.json",
            "m1-slice6-campaign-stage-request.v3.schema.json",
            "m1-slice6-campaign-stage-evidence.v3.schema.json",
            "m1-slice6-campaign-composed-evidence.v3.schema.json",
        ];
        string combined = string.Join('\n', files.Select(file =>
        {
            string text = File.ReadAllText(Path.Combine(repositoryContracts, file));
            using JsonDocument _ = JsonDocument.Parse(text);
            return text;
        }));

        Assert.AreEqual(1, Count(combined, M1Slice6AuthorityContracts.RetiredCampaignId));
        Assert.AreEqual(1, Count(combined, M1Slice6AuthorityContracts.RetiredCredentialManifestId));
        StringAssert.Contains(combined, "prohibited-reserved-history-only");
        StringAssert.Contains(combined, FreshCampaign);
        StringAssert.Contains(combined, FreshCredential);
        StringAssert.Contains(combined,
            "infinium.m1-s6.campaign-stage/Qualification/033d4d98-871e-44e3-947d-295d8429514e");
        StringAssert.Contains(combined,
            "infinium.m1-s6.campaign-stage/SourceClaimExtraction/01c76dba-f052-48dc-9b9b-b63414128233");
        StringAssert.Contains(combined,
            "infinium.m1-s6.campaign-stage/CandidateInvestigation/4b3378e2-e29c-4e62-843a-62a882f16144");
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

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
