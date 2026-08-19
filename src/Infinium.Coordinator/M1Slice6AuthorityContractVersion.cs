using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.Coordinator;

public enum M1Slice6AuthorityContractVersion
{
    RetiredV2,
    FreshC2V3,
}

internal enum M1Slice6AuthorityDocumentKind
{
    CredentialProfile,
    Campaign,
    StageRequest,
}

internal static class M1Slice6AuthorityContracts
{
    internal const string RetiredCampaignId =
        "infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66";
    internal const string RetiredCredentialManifestId =
        "infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5";

    internal const string CredentialV2 =
        "infinium.repository.wp9-production-profile-authorization/2.0.0";
    internal const string CredentialV3 =
        "infinium.repository.wp9-production-profile-authorization/3.0.0";
    internal const string CampaignV2 =
        "infinium.repository.m1-slice6-finite-campaign-authorization/2.0.0";
    internal const string CampaignV3 =
        "infinium.repository.m1-slice6-finite-campaign-authorization/3.0.0";
    internal const string StageV2 =
        "infinium.repository.m1-slice6-campaign-stage-request/2.0.0";
    internal const string StageV3 =
        "infinium.repository.m1-slice6-campaign-stage-request/3.0.0";

    internal static M1Slice6AuthorityContractVersion Validate(
        string documentPath,
        byte[] documentBytes,
        M1Slice6AuthorityDocumentKind kind)
    {
        using JsonDocument document = JsonDocument.Parse(documentBytes);
        string identity = document.RootElement.GetProperty("schema_identity").GetString()
            ?? throw new InvalidDataException("The authority document has no schema identity.");
        (M1Slice6AuthorityContractVersion version, string schemaFile) = (kind, identity) switch
        {
            (M1Slice6AuthorityDocumentKind.CredentialProfile, CredentialV2) =>
                (M1Slice6AuthorityContractVersion.RetiredV2,
                    "wp9-production-profile-authorization.v2.schema.json"),
            (M1Slice6AuthorityDocumentKind.CredentialProfile, CredentialV3) =>
                (M1Slice6AuthorityContractVersion.FreshC2V3,
                    "wp9-production-profile-authorization.v3.schema.json"),
            (M1Slice6AuthorityDocumentKind.Campaign, CampaignV2) =>
                (M1Slice6AuthorityContractVersion.RetiredV2,
                    "m1-slice6-finite-campaign-authorization.v2.schema.json"),
            (M1Slice6AuthorityDocumentKind.Campaign, CampaignV3) =>
                (M1Slice6AuthorityContractVersion.FreshC2V3,
                    "m1-slice6-finite-campaign-authorization.v3.schema.json"),
            (M1Slice6AuthorityDocumentKind.StageRequest, StageV2) =>
                (M1Slice6AuthorityContractVersion.RetiredV2,
                    "m1-slice6-campaign-stage-request.v2.schema.json"),
            (M1Slice6AuthorityDocumentKind.StageRequest, StageV3) =>
                (M1Slice6AuthorityContractVersion.FreshC2V3,
                    "m1-slice6-campaign-stage-request.v3.schema.json"),
            _ => throw new InvalidDataException("The authority schema identity is not an active supported version."),
        };

        string repository = M1Slice6CampaignStageManifestValidator.FindRepositoryRoot(documentPath);
        string schemaPath = Path.Combine(repository, "contracts", "repository", schemaFile);
        ActiveRepositoryJsonSchemaValidator.Validate(documentBytes, File.ReadAllBytes(schemaPath), schemaFile);
        return version;
    }

    internal static void RequireFreshExternalEffect(
        ProviderEffectRuntimeAuthority authority,
        M1Slice6AuthorityContractVersion campaignVersion,
        M1Slice6AuthorityContractVersion subjectVersion,
        string campaignId,
        string credentialManifestId)
    {
        if (authority.Scope != ProviderEffectAuthorityScope.ExternalEffect)
        {
            return;
        }
        if (campaignVersion != M1Slice6AuthorityContractVersion.FreshC2V3
            || subjectVersion != M1Slice6AuthorityContractVersion.FreshC2V3
            || campaignId == RetiredCampaignId
            || credentialManifestId == RetiredCredentialManifestId)
        {
            throw new InvalidDataException(
                "External C2 effects require fresh v3 authority and explicitly reject retired campaign or credential identities.");
        }
    }

    internal static string StageEvidenceSchema(M1Slice6AuthorityContractVersion version) => version switch
    {
        M1Slice6AuthorityContractVersion.RetiredV2 => "infinium.m1-s6.campaign-stage-evidence/v2",
        M1Slice6AuthorityContractVersion.FreshC2V3 => "infinium.m1-s6.campaign-stage-evidence/v3",
        _ => throw new InvalidDataException("The stage evidence authority version is unknown."),
    };

    internal static string ComposedEvidenceSchema(M1Slice6AuthorityContractVersion version) => version switch
    {
        M1Slice6AuthorityContractVersion.RetiredV2 => "infinium.m1-s6.campaign-composed-evidence/v2",
        M1Slice6AuthorityContractVersion.FreshC2V3 => "infinium.m1-s6.campaign-composed-evidence/v3",
        _ => throw new InvalidDataException("The composed evidence authority version is unknown."),
    };
}
