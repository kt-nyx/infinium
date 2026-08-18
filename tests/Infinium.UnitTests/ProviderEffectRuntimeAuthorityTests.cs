using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ProviderEffectRuntimeAuthorityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-08-15T16:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly M1Slice6CampaignIdentity Identity = new(
        "campaign/c1-rehearsal", new string('1', 64), new string('2', 64), new string('3', 40),
        "credential/c1-rehearsal", new string('4', 64), "profile/c1-rehearsal",
        "generation/c1-rehearsal", new string('5', 64));

    [TestMethod]
    public void EffectFreeAuthorityIsClosedTypedAndCannotAuthorizeAnExternalEffect()
    {
        string root = TempRoot();
        try
        {
            (string path, string sha) = WriteAuthority(root, "credential-authority",
                "credential-enrollment", Identity.CredentialManifestId, Identity.CredentialManifestSha256,
                "none", "none", "none", credential: true);
            ProviderEffectRuntimeAuthority authority = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                path, sha, Now);

            ProviderEffectRuntimeAuthorityLoader.RequireEffectFreeRehearsal(authority);
            Assert.AreEqual(ProviderEffectAuthorityKind.CredentialEnrollment, authority.Kind);
            Assert.AreEqual(0, authority.Limits.HelperLaunches);
            Assert.AreEqual(0, authority.Limits.CredentialNativeCalls);
            Assert.AreEqual(0, authority.Limits.ProviderStarts);
            Assert.AreEqual(0, authority.Limits.DnsResolutions);
            Assert.AreEqual(0, authority.Limits.BillableOperations);
            Assert.IsFalse(authority.Limits.AutomaticRetry);
            Assert.IsFalse(authority.Limits.FourthCallPermitted);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProviderEffectRuntimeAuthorityLoader.RequireExternalEffect(authority,
                    ProviderEffectAuthorityKind.CredentialEnrollment));

            string stale = new string('0', 64);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(path, stale, Now));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(path, sha, Now.AddDays(2)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void DurableLedgerBindsCredentialAndStageAuthoritiesAcrossReopenAndRejectsTamper()
    {
        string root = TempRoot();
        string ledgerPath = Path.Combine(root, "ledger.jsonl");
        try
        {
            M1Slice6FiniteCampaignLedger ledger = new(ledgerPath, Identity, Now.AddDays(3),
                Now.AddDays(2), Now);
            (string credentialPath, string credentialSha) = WriteAuthority(root, "credential-authority",
                "credential-enrollment", Identity.CredentialManifestId, Identity.CredentialManifestSha256,
                "none", "none", "none", credential: true);
            ProviderEffectRuntimeAuthority credential = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                credentialPath, credentialSha, Now.AddMinutes(1));
            ProviderEffectRuntimeAuthorityLoader.ValidateDurableBinding(credential, Identity, ledger.Current,
                ProviderEffectAuthorityKind.CredentialEnrollment, Identity.CredentialManifestId,
                Identity.CredentialManifestSha256, requireExternalEffect: false);
            ledger.RecordIndependentReview(credential.AuthorityId, credential.ManifestSha256, Now.AddMinutes(1));
            ledger.AdmitCampaign(Now.AddMinutes(2));
            ledger.BeginCredentialExecutionHandoff(Now.AddMinutes(3));
            ledger.RecordCredentialEvidenceHandoff("credential-evidence", new string('6', 64),
                new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), Now.AddMinutes(4));
            ledger.AcceptCredentialEvidence("credential-evidence", new string('6', 64), Now.AddMinutes(4).AddTicks(1));

            (string stagePath, string stageSha) = WriteAuthority(root, "qualification-authority",
                "transport-qualification", "qualification-manifest", new string('7', 64),
                ledger.Current.EventHash, ledger.Current.EvidenceId, ledger.Current.EvidenceSha256,
                credential: false);
            ProviderEffectRuntimeAuthority stage = ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                stagePath, stageSha, Now.AddMinutes(5));
            ProviderEffectRuntimeAuthorityLoader.ValidateDurableBinding(stage, Identity, ledger.Current,
                ProviderEffectAuthorityKind.TransportQualification, "qualification-manifest",
                new string('7', 64), requireExternalEffect: false);
            M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(
                M1Slice6CampaignStage.Qualification);
            ledger.ReserveStage(M1Slice6CampaignStage.Qualification,
                new("qualification-manifest", new string('7', 64), 1024, 1024,
                    limits.MaximumOutputTokens, limits.MaximumRawResponseBytes, limits.MaximumNanoUsd),
                stage.AuthorityId, stage.ManifestSha256, Now.AddMinutes(5));

            M1Slice6FiniteCampaignLedger reopened = new(ledgerPath, Identity, Now.AddDays(3),
                Now.AddDays(2), Now.AddMinutes(6));
            Assert.AreEqual(stage.AuthorityId, reopened.Current.RuntimeAuthorityId);
            Assert.AreEqual(stage.ManifestSha256, reopened.Current.RuntimeAuthoritySha256);

            string tampered = File.ReadAllText(ledgerPath).Replace(
                stage.AuthorityId, "qualification-authority-tampered", StringComparison.Ordinal);
            File.WriteAllText(ledgerPath, tampered, new UTF8Encoding(false));
            Assert.ThrowsExactly<InvalidDataException>(() => new M1Slice6FiniteCampaignLedger(
                ledgerPath, Identity, Now.AddDays(3), Now.AddDays(2), Now.AddMinutes(7)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void ForbiddenLimitsUnknownFieldsAndEscapingPathsFailClosed()
    {
        string root = TempRoot();
        try
        {
            (string path, string sha) = WriteAuthority(root, "qualification-authority",
                "transport-qualification", "qualification-manifest", new string('7', 64),
                new string('8', 64), "predecessor-evidence", new string('9', 64), credential: false);
            string valid = File.ReadAllText(path);

            string broadened = valid.Replace("\"provider_starts\":0", "\"provider_starts\":1",
                StringComparison.Ordinal);
            File.WriteAllText(path, broadened, new UTF8Encoding(false));
            Assert.ThrowsExactly<InvalidDataException>(() => ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                path, Sha(path), Now));

            File.WriteAllText(path, valid.Replace("\"output_root_relative\":\"artifacts/c1/output\"",
                "\"output_root_relative\":\"../outside\"", StringComparison.Ordinal), new UTF8Encoding(false));
            Assert.ThrowsExactly<InvalidDataException>(() => ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                path, Sha(path), Now));

            File.WriteAllText(path, valid.Replace("\"limits\":{", "\"unknown\":0,\"limits\":{",
                StringComparison.Ordinal), new UTF8Encoding(false));
            Assert.ThrowsExactly<InvalidDataException>(() => ProviderEffectRuntimeAuthorityLoader.LoadAndValidate(
                path, Sha(path), Now));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void ExecutableBindingRequiresExactCompiledRevisionAndBinaryDigests()
    {
        string version = typeof(ProviderEffectRuntimeAuthorityTests).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        string revision = Regex.Match(version, @"\+(?<sha>[0-9a-f]{40})$").Groups["sha"].Value;
        Assert.AreEqual(40, revision.Length);
        ProviderEffectRuntimeAuthority authority = new("authority", ProviderEffectAuthorityScope.EffectFreeRehearsal,
            ProviderEffectAuthorityKind.CredentialEnrollment, "subject", new string('1', 64),
            "campaign", new string('2', 64), "none", "none", "none", revision,
            new string('a', 64), new string('b', 64), "review", new string('c', 64),
            "owner", new string('d', 64), Now.AddHours(-1), Now.AddHours(1),
            new("out", "out/ledger", "state", "bin/coordinator", "bin/helper"),
            new(0, 0, 0, 0, 0, 0, false, false), new string('e', 64));
        ProviderEffectRuntimeAuthorityLoader.ValidateExecutableBinding(authority,
            typeof(ProviderEffectRuntimeAuthorityTests).Assembly, new string('a', 64), new string('b', 64));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ProviderEffectRuntimeAuthorityLoader.ValidateExecutableBinding(authority with
            {
                ImplementationCommit = new string('f', 40),
            }, typeof(ProviderEffectRuntimeAuthorityTests).Assembly, new string('a', 64), new string('b', 64)));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ProviderEffectRuntimeAuthorityLoader.ValidateExecutableBinding(authority,
                typeof(ProviderEffectRuntimeAuthorityTests).Assembly, new string('0', 64), new string('b', 64)));
    }

    private static (string Path, string Sha) WriteAuthority(string root, string authorityId,
        string kind, string subjectId, string subjectSha, string predecessorHash,
        string predecessorEvidenceId, string predecessorEvidenceSha, bool credential)
    {
        string json = JsonSerializer.Serialize(new
        {
            schema_identity = ProviderEffectRuntimeAuthorityLoader.SchemaIdentity,
            authority_id = authorityId,
            scope = "effect-free-rehearsal",
            kind,
            status = "reviewed-and-owner-accepted",
            subject_manifest = new { id = subjectId, sha256 = subjectSha },
            campaign = new { id = Identity.CampaignId, sha256 = Identity.CampaignManifestSha256 },
            predecessor = new
            {
                ledger_event_hash = predecessorHash,
                evidence_id = predecessorEvidenceId,
                evidence_sha256 = predecessorEvidenceSha,
            },
            candidate_binding = new
            {
                implementation_commit = Identity.VerificationCandidateCommit,
                coordinator_sha256 = new string('a', 64),
                helper_sha256 = new string('b', 64),
            },
            review = new { evidence_id = "c1-review", evidence_sha256 = new string('c', 64) },
            owner_decision = new { decision_id = "c1-owner", decision_sha256 = new string('d', 64) },
            not_before_utc = "2026-08-15T15:00:00.0000000Z",
            expires_at_utc = "2026-08-16T15:00:00.0000000Z",
            execution = new
            {
                output_root_relative = "artifacts/c1/output",
                ledger_path_relative = "artifacts/c1/output/ledger.jsonl",
                product_state_root_relative = "artifacts/c1/product-state",
                coordinator_path_relative = "artifacts/c1/bin/Infinium.Coordinator.exe",
                helper_path_relative = "artifacts/c1/bin/Infinium.CredentialHelper.exe",
            },
            limits = new
            {
                helper_launches = 0,
                credential_native_calls = 0,
                provider_starts = 0,
                dns_resolutions = 0,
                billable_operations = 0,
                literal_loopback_starts = credential ? 0 : 1,
                automatic_retry = false,
                fourth_call_permitted = false,
            },
        });
        string path = Path.Combine(root, authorityId + ".json");
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return (path, Sha(path));
    }

    private static string Sha(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private static string TempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-c1-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
