using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Wp8PreLiveReadinessContractTests
{
    private static readonly string[] RelativeTemplates =
    [
        "docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json",
        "docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json",
        "docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json",
        "docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json",
    ];

    [TestMethod]
    [TestCategory("Contract")]
    public void MatrixAndFourTemplatesAreClosedFiniteAndNonExecutable()
    {
        string root = RepositoryRoot();
        JsonObject matrix = ReadNode(root, "docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json");
        Assert.AreEqual("infinium.m1-s6.wp8.case-requirement-matrix/v1", matrix["matrix_id"]!.GetValue<string>());
        Assert.AreEqual(23, matrix["cases"]!.AsArray().Count);
        Assert.AreEqual(6, matrix["evidence_groups"]!.AsArray().Count);
        Assert.AreEqual(0, matrix["external_effects"]!["credential_manager_operations"]!.GetValue<int>());
        Assert.AreEqual(0, matrix["external_effects"]!["provider_requests"]!.GetValue<int>());
        Assert.AreEqual("pending-fresh-independent-review", matrix["review"]!["judgment"]!.GetValue<string>());
        Assert.AreEqual("correction-verification-pending", matrix["acceptance_binding"]!["state"]!.GetValue<string>());
        Assert.AreEqual("pending-until-post-run-evidence-freeze",
            matrix["acceptance_binding"]!["post_run_evidence_candidate_commit"]!.GetValue<string>());
        string acceptanceBinding = matrix["acceptance_binding"]!.ToJsonString();

        HashSet<string> packetIds = new(StringComparer.Ordinal);
        HashSet<string> packetKinds = new(StringComparer.Ordinal);
        foreach (string relative in RelativeTemplates)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement template = document.RootElement;
            Assert.AreEqual("non-executable-template", template.GetProperty("status").GetString());
            Assert.AreEqual("none", template.GetProperty("effect_authority").GetString());
            Assert.IsFalse(template.GetProperty("execution").GetProperty("permitted").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, template.GetProperty("execution").GetProperty("command").ValueKind);
            Assert.AreEqual(acceptanceBinding,
                JsonNode.Parse(template.GetProperty("acceptance_binding").GetRawText())!.ToJsonString());
            Assert.IsTrue(packetIds.Add(template.GetProperty("packet_id").GetString()!));
            Assert.IsTrue(packetKinds.Add(template.GetProperty("packet_kind").GetString()!));
            string text = Encoding.UTF8.GetString(bytes);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,
                @"(?i)bearer\s+[A-Za-z0-9._-]+|sk-(?:proj-)?[A-Za-z0-9_-]{8,}|Infinium:[^\""\s]+"));
        }
        Assert.AreEqual(4, packetIds.Count);
        Assert.AreEqual(4, packetKinds.Count);

        string readme = File.ReadAllText(Path.Combine(root,
            "docs/plans/milestones/m1/slices/s6/README.md".Replace('/', Path.DirectorySeparatorChar)));
        string normalizedReadme = System.Text.RegularExpressions.Regex.Replace(readme, @"\s+", " ");
        StringAssert.Contains(normalizedReadme, "WP8 closeout correction and complete non-live reverification are active.");
        StringAssert.Contains(normalizedReadme, "WP9 is not eligible");
        StringAssert.Contains(normalizedReadme, "The earlier WP8 acceptance identities and receipts are retained only as superseded historical evidence and do not certify the corrected candidate.");
        StringAssert.Contains(normalizedReadme, "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority.");
        StringAssert.Contains(normalizedReadme, "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.");
        Assert.IsFalse(normalizedReadme.Contains("The next eligible action is only the owner's decision whether to begin WP9", StringComparison.Ordinal));

        foreach (string relative in new[]
        {
            "contracts/repository/wp8-case-requirement-matrix.v1.schema.json",
            "contracts/repository/wp8-production-profile-authorization-template.v1.schema.json",
            "contracts/repository/wp8-provider-request-authorization-template.v1.schema.json",
        })
        {
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar))));
            Assert.IsFalse(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
            StringAssert.StartsWith(schema.RootElement.GetProperty("$id").GetString()!,
                "https://schemas.infinium.dev/repository/wp8-");
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void SemanticValidatorAcceptsExactCandidateAndRejectsPacketMatrixMutations()
    {
        Assert.AreEqual(0, RunValidator(null), "Exact WP8 candidate failed semantic validation.");

        (string Name, Action<Dictionary<string, JsonObject>> Mutation)[] mutations =
        [
            ("missing-case", docs => docs["matrix"]["cases"]!.AsArray().RemoveAt(0)),
            ("unknown-property", docs => docs["profile"]["unexpected"] = true),
            ("matrix-nested-unknown", docs => docs["matrix"]["cases"]![0]!["unexpected"] = true),
            ("profile-nested-unknown", docs => docs["profile"]["persistence_delete"]!["unexpected"] = true),
            ("request-nested-unknown", docs => docs["qualification"]["limits"]!["unexpected"] = 1),
            ("acceptance-nested-unknown", docs => docs["matrix"]["acceptance_binding"]!["unexpected"] = true),
            ("case-classification", docs => docs["matrix"]["cases"]![0]!["classification"] = "primary"),
            ("case-disposition", docs => docs["matrix"]["cases"]![4]!["disposition"] = "covered-non-live"),
            ("case-requirement", docs => docs["matrix"]["cases"]![0]!["requirements"]![0] = "SNAP-001"),
            ("case-gate", docs => docs["matrix"]["cases"]![0]!["evidence_gates"]![0] = "Budget"),
            ("case-assertion", docs => docs["matrix"]["cases"]![0]!["covered_assertions"]![0] = "weakened"),
            ("case-na-tuple", docs => docs["matrix"]["cases"]![4]!["n_a_assertions"]![0]!["later_authority"] = "WP10"),
            ("supplemental-mapping", docs => docs["matrix"]["supplemental_requirement_mappings"]![0]!["evidence_gates"]![0] = "Budget"),
            ("operation-swap", docs => docs["qualification"]["request_binding"]!["operation"] = "source-claim-extraction"),
            ("packet-id", docs => docs["source"]["packet_id"] = "infinium.m1-s6.wp8.pre-live-qualification-authorization-template/v1"),
            ("limit", docs => docs["candidate"]["limits"]!["maximum_dispatch_count"] = 2),
            ("unbounded-extra-limit", docs => docs["candidate"]["limits"]!["maximum_total_tokens"] = 999999999),
            ("retry", docs => docs["source"]["transport_boundary"]!["automatic_retry"] = true),
            ("predecessor", docs => docs["source"]["candidate_binding"]!["required_predecessor_live_acceptance"] = "accepted-WP10-source-claim-operation-receipt-pending"),
            ("official-doc", docs => docs["candidate"]["capability_price_binding"]!["official_doc_snapshot_sha256"] = new string('0', 64)),
            ("refresh-authority", docs => docs["source"]["capability_price_binding"]!["official_doc_refresh_authority"] = "inherited-from-WP10"),
            ("owner-inheritance", docs => docs["qualification"]["owner_authorization"]!["inheritance"] = "allowed"),
            ("common-binding", docs => docs["profile"]["candidate_binding"]!["accepted_wp4_execution_commit"] = new string('0', 40)),
            ("product-template-identity", docs => docs["matrix"]["candidate_binding"]!["wp8_product_template_commit"] = new string('0', 40)),
            ("verification-identity", docs => docs["matrix"]["candidate_binding"]!["wp8_verification_candidate_commit"] = new string('0', 40)),
            ("acceptance-verification-identity", docs => docs["matrix"]["acceptance_binding"]!["verification_candidate_commit"] = new string('0', 40)),
            ("acceptance-old-verification-identity", docs => docs["matrix"]["acceptance_binding"]!["verification_candidate_commit"] = "fbdb1f03e006a85723b0533d44b2ed06e02cc724"),
            ("acceptance-cross-document", docs => docs["profile"]["acceptance_binding"]!["state"] = "accepted-closeout"),
            ("acceptance-premature-evidence", docs => docs["matrix"]["acceptance_binding"]!["post_run_evidence_candidate_commit"] = new string('1', 40)),
            ("acceptance-premature-nonlive", docs => docs["matrix"]["acceptance_binding"]!["non_live_all_receipt_sha256"] = new string('1', 64)),
            ("acceptance-premature-prelive", docs => docs["matrix"]["acceptance_binding"]!["pre_live_receipt_sha256"] = new string('1', 64)),
            ("acceptance-premature-layer6", docs => docs["matrix"]["acceptance_binding"]!["direct_layer6_receipt_sha256"] = new string('1', 64)),
            ("delete-enabled", docs => docs["profile"]["persistence_delete"]!["deletion_permitted"] = true),
            ("delete-call-authorized", docs => docs["profile"]["native_boundary"]!["forbidden_calls"]!.AsArray().RemoveAt(0)),
            ("secret", docs => docs["qualification"]["request_binding"]!["canonical_request"] = "Bearer sk-proj-forbidden-canary"),
            ("raw-target", docs => docs["profile"]["canaries"]!["target_canary"] = "Infinium:raw:target"),
            ("stale-identity", docs => docs["matrix"]["candidate_binding"]!["accepted_wp7_product_commit"] = new string('0', 40)),
            ("executable", docs => docs["candidate"]["status"] = "ready-for-owner-acceptance"),
        ];
        foreach ((string name, Action<Dictionary<string, JsonObject>> mutation) in mutations)
        {
            Assert.AreNotEqual(0, RunValidator(mutation), $"WP8 validator accepted mutation '{name}'.");
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void NonLiveAllIsClosedAndDoesNotInvokeNativeOrLiveGates()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        StringAssert.Contains(script, "'NonLiveAll'");
        StringAssert.Contains(script, "function Invoke-NonLiveAllGate");
        StringAssert.Contains(script, "NonLiveAll refuses every authorization manifest.");
        StringAssert.Contains(script, "Invoke-Wp8PreLiveValidationGate");
        StringAssert.Contains(script, "content-bound evidence");
        StringAssert.Contains(script, "credential_manager_operations = 0");
        StringAssert.Contains(script, "provider_requests = 0");
        StringAssert.Contains(script, "live_manifest_execution = $false");

        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(script,
            @"(?ms)^function Invoke-NonLiveAllGate \{.*?^\}");
        Assert.IsTrue(function.Success, "NonLiveAll implementation was not found.");
        Assert.IsFalse(function.Value.Contains("Invoke-CredentialNativeGate", StringComparison.Ordinal));
        Assert.IsFalse(function.Value.Contains("Invoke-CredentialNativeRecoveryGate", StringComparison.Ordinal));
        Assert.IsFalse(function.Value.Contains("run-m1-slice6-live", StringComparison.Ordinal));
        Assert.IsFalse(function.Value.Contains("run-m1-slice6-credential", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void FreezeModelAcceptsOnlyCorrectionBindingOrStructuredAppendOnlyAcceptedHandoff()
    {
        string root = RepositoryRoot();
        string script = TestRepository.Read("eng", "validate-m1-slice6-wp8-prelive.ps1");
        string[] functionNames =
        [
            "Test-Wp8ExactPathSet",
            "Test-Wp8CorrectionCurrentState",
            "Test-Wp8CorrectionReadme",
            "Test-Wp8AcceptedHandoffCurrentState",
            "Test-Wp8AcceptedHandoffReadme",
            "Test-Wp8RetainedAcceptanceRecord",
            "Get-Wp8PostVerificationDisposition",
        ];
        string functions = string.Join(Environment.NewLine, functionNames.Select(name =>
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
                script, $@"(?ms)^function {System.Text.RegularExpressions.Regex.Escape(name)}\(.*?^\}}");
            Assert.IsTrue(match.Success, $"Freeze function '{name}' was not found.");
            return match.Value;
        }));
        string currentState = File.ReadAllText(Path.Combine(root, "docs", "current-state.md"));
        string readme = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6", "README.md"));
        string record = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6", "record.md"));
        const string noEffect = "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.";
        const string noInheritance = "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority";
        string[] noEffectFacts =
        [
            "API-key use", "live-manifest execution", "native Credential Manager operation", "DNS operation",
            "public-network operation", "provider request", "billable operation", "production-profile materialization/use",
        ];
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        string command = $$"""
            {{functions}}
            $current = [regex]::Replace([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{Encode(currentState)}}')), '\s+', ' ')
            $readme = [regex]::Replace([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{Encode(readme)}}')), '\s+', ' ')
            $record = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{Encode(record)}}'))
            $baseRecord = "verification-record`n"
            $pending = [pscustomobject]@{
              state='correction-verification-pending'; verification_candidate_commit=('a' * 40)
              post_run_evidence_candidate_commit='pending-until-post-run-evidence-freeze'
              non_live_all_receipt_sha256='pending-until-post-run-evidence-freeze'
              pre_live_receipt_sha256='pending-until-post-run-evidence-freeze'
              direct_layer6_receipt_sha256='pending-until-post-run-evidence-freeze'
            }
            $accepted = [pscustomobject]@{
              state='accepted-closeout'; verification_candidate_commit=('a' * 40)
              post_run_evidence_candidate_commit=('b' * 40)
              non_live_all_receipt_sha256=('c' * 64); pre_live_receipt_sha256=('d' * 64)
              direct_layer6_receipt_sha256=('e' * 64)
            }
            $binding = @(
              'docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json',
              'docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json',
              'docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json',
              'docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json',
              'docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json')
            $closeout = @($binding) + @('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')
            if ((Get-Wp8PostVerificationDisposition @() $current $readme $record $record $pending) -ne 'exact-correction-verification-state') { exit 10 }
            if ((Get-Wp8PostVerificationDisposition $binding $current $readme $record $record $pending) -ne 'exact-correction-verification-state') { exit 11 }
            $acceptedCurrent = @"
            | Current authorized work | ``M1/S6/WP9`` owner decision and exact authorization-packet materialization planning only; corrected WP8 is accepted. {{noEffect}} |
            Accepted corrected ``M1/S6/WP8`` candidate $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit) $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256)
            | Next eligible action | Owner decision whether to begin ``M1/S6/WP9`` materialization planning under accepted plan section 20; only fresh exact production-profile and WP9 request authorizations may be prepared, and neither may be executed without separate exact owner acceptance |
            {{noInheritance}}. {{noEffect}}
            "@
            $acceptedReadme = "Corrected WP8 is independently accepted. $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit) $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256) The next eligible action is only the owner's decision whether to begin WP9 fresh exact authorization-packet materialization planning. {{noInheritance}}. {{noEffect}}"
            $acceptanceRecord = @"
            Corrected WP8 independent acceptance and handoff
            | contract-persistence | ``ACCEPT`` |
            | budget-settlement-faults | ``ACCEPT`` |
            | credential-helper-security | ``ACCEPT`` |
            | provider-adapter-offline-safety | ``ACCEPT`` |
            | source-candidate-semantics-provenance | ``ACCEPT`` |
            | overall-matrix-claims-diff | ``ACCEPT`` |
            $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit)
            $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256)
            No separate reviewer-judgment artifact or hash was created or required.
            "@
            if ((Get-Wp8PostVerificationDisposition $closeout $acceptedCurrent $acceptedReadme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'exact-accepted-append-only-handoff') { exit 12 }
            $badSets = @(
              ,(@($binding) + 'src/Infinium.Application/Unauthorized.cs'),
              ,(@($binding) + 'docs/plans/milestones/m1/slices/s6/README.md'),
              ,(@($binding) + 'docs/current-state.md'),
              ,(@($binding) + 'docs/plans/milestones/m1/slices/s6/record.md'))
            foreach ($bad in $badSets) {
                if ((Get-Wp8PostVerificationDisposition $bad $current $readme $record $record $pending) -ne 'invalid') { exit 13 }
            }
            if ((Get-Wp8PostVerificationDisposition $closeout $acceptedCurrent $acceptedReadme 'different-prefix' $acceptanceRecord $accepted) -ne 'invalid') { exit 14 }
            $old = '| Current authorized work | `M1/S6/WP9` owner decision and exact authorization-packet materialization planning only; WP8 is accepted. | Accepted `M1/S6/WP8` candidate fbdb1f03e006a85723b0533d44b2ed06e02cc724 36b980d226e9f9a0e91281a530fc959a211fb696 {{noInheritance}} {{noEffect}}'
            if ((Get-Wp8PostVerificationDisposition $binding $old $readme $record $record $pending) -ne 'invalid') { exit 15 }
            $wp9Pending = $current.Replace('`M1/S6/WP8` closeout correction and complete non-live reverification only; WP9 is not eligible.', '`M1/S6/WP9` planning is eligible.')
            if ((Get-Wp8PostVerificationDisposition $binding $wp9Pending $readme $record $record $pending) -ne 'invalid') { exit 16 }
            if ((Get-Wp8PostVerificationDisposition $binding ($current.Replace('was later invalidated as current handoff authority by current-HEAD review and remains historical evidence only.','')) $readme $record $record $pending) -ne 'invalid') { exit 16 }
            if ((Get-Wp8PostVerificationDisposition $binding $current ($readme.Replace('WP9 is not eligible','WP9 is eligible')) $record $record $pending) -ne 'invalid') { exit 16 }
            foreach ($stale in @('WP8 is independently accepted at exact','The only next eligible action is an owner decision and exact packet-materialization planning for WP9')) {
                if ((Get-Wp8PostVerificationDisposition $binding ($current + ' ' + $stale) $readme $record $record $pending) -ne 'invalid') { exit 16 }
                if ((Get-Wp8PostVerificationDisposition $binding $current ($readme + ' ' + $stale) $record $record $pending) -ne 'invalid') { exit 16 }
            }
            foreach ($fact in @('API-key use','live-manifest execution','native Credential Manager operation','DNS operation','public-network operation','provider request','billable operation','production-profile materialization/use')) {
                $mutated = $acceptedCurrent.Replace('{{noEffect}}', '{{noEffect}}'.Replace($fact, ''))
                if ((Get-Wp8PostVerificationDisposition $closeout $mutated $acceptedReadme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 17 }
            }
            if ((Get-Wp8PostVerificationDisposition $closeout ($acceptedCurrent.Replace('{{noInheritance}}','')) $acceptedReadme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 18 }
            foreach ($value in @($accepted.verification_candidate_commit,$accepted.post_run_evidence_candidate_commit,$accepted.non_live_all_receipt_sha256,$accepted.pre_live_receipt_sha256,$accepted.direct_layer6_receipt_sha256)) {
                $mutated = $acceptedCurrent.Replace($value, ('0' * $value.Length))
                if ((Get-Wp8PostVerificationDisposition $closeout $mutated $acceptedReadme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 18 }
            }
            if ((Get-Wp8PostVerificationDisposition $closeout $acceptedCurrent ($acceptedReadme.Replace('Corrected WP8 is independently accepted.','WP8 accepted.')) $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 19 }
            $missingReadme = @($closeout | Where-Object { $_ -ne 'docs/plans/milestones/m1/slices/s6/README.md' })
            if ((Get-Wp8PostVerificationDisposition $missingReadme $acceptedCurrent $acceptedReadme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 20 }
            exit 0
            """;
        Assert.AreEqual(0, RunPowerShellScript(command),
            "Freeze model accepted unauthorized drift or rejected an exact state.");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void NonLiveAllAuthorityPredicateAcceptsCorrectionAndRejectsOldOrWeakenedHandoffs()
    {
        string root = RepositoryRoot();
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(
            script, @"(?ms)^function Get-Wp8NonLiveCurrentStateDisposition\(.*?^\}");
        Assert.IsTrue(function.Success, "NonLiveAll current-state predicate was not found.");
        string currentState = File.ReadAllText(Path.Combine(root, "docs", "current-state.md"));
        const string noEffect = "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.";
        const string noInheritance = "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority";
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        string command = $$"""
            {{function.Value}}
            $valid = [regex]::Replace([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{Encode(currentState)}}')), '\s+', ' ')
            $pending = [pscustomobject]@{
              state='correction-verification-pending'; verification_candidate_commit=('a' * 40)
              post_run_evidence_candidate_commit='pending-until-post-run-evidence-freeze'
              non_live_all_receipt_sha256='pending-until-post-run-evidence-freeze'
              pre_live_receipt_sha256='pending-until-post-run-evidence-freeze'
              direct_layer6_receipt_sha256='pending-until-post-run-evidence-freeze'
            }
            if ((Get-Wp8NonLiveCurrentStateDisposition $valid $pending) -ne 'exact-wp8-correction-reverification-state') { exit 10 }
            $old = '| Current authorized work | `M1/S6/WP9` owner decision and exact authorization-packet materialization planning only; WP8 is accepted. | Accepted `M1/S6/WP8` candidate fbdb1f03e006a85723b0533d44b2ed06e02cc724 36b980d226e9f9a0e91281a530fc959a211fb696 {{noInheritance}} {{noEffect}}'
            if ((Get-Wp8NonLiveCurrentStateDisposition $old $pending) -ne 'invalid') { exit 11 }
            if ((Get-Wp8NonLiveCurrentStateDisposition ($valid.Replace('WP9 is not eligible','WP9 is eligible')) $pending) -ne 'invalid') { exit 12 }
            if ((Get-Wp8NonLiveCurrentStateDisposition ($valid.Replace('was later invalidated as current handoff authority by current-HEAD review and remains historical evidence only.','')) $pending) -ne 'invalid') { exit 12 }
            foreach ($stale in @('WP8 is independently accepted at exact','The only next eligible action is an owner decision and exact packet-materialization planning for WP9')) {
                if ((Get-Wp8NonLiveCurrentStateDisposition ($valid + ' ' + $stale) $pending) -ne 'invalid') { exit 12 }
            }
            foreach ($fact in @('API-key use','live-manifest execution','native Credential Manager operation','DNS operation','public-network operation','provider request','billable operation','production-profile materialization/use')) {
                $mutated = $valid.Replace('{{noEffect}}', '{{noEffect}}'.Replace($fact, ''))
                if ((Get-Wp8NonLiveCurrentStateDisposition $mutated $pending) -ne 'invalid') { exit 13 }
            }
            if ((Get-Wp8NonLiveCurrentStateDisposition ($valid.Replace('{{noInheritance}}','')) $pending) -ne 'invalid') { exit 14 }
            $accepted = [pscustomobject]@{
              state='accepted-closeout'; verification_candidate_commit=('a' * 40)
              post_run_evidence_candidate_commit=('b' * 40)
              non_live_all_receipt_sha256=('c' * 64); pre_live_receipt_sha256=('d' * 64)
              direct_layer6_receipt_sha256=('e' * 64)
            }
            $acceptedText = @"
            | Current authorized work | ``M1/S6/WP9`` owner decision and exact authorization-packet materialization planning only; corrected WP8 is accepted. {{noEffect}} |
            Accepted corrected ``M1/S6/WP8`` candidate $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit) $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256)
            | Next eligible action | Owner decision whether to begin ``M1/S6/WP9`` materialization planning under accepted plan section 20; only fresh exact production-profile and WP9 request authorizations may be prepared, and neither may be executed without separate exact owner acceptance |
            {{noInheritance}}. {{noEffect}}
            "@
            if ((Get-Wp8NonLiveCurrentStateDisposition $acceptedText $accepted) -ne 'exact-corrected-wp8-accepted-handoff') { exit 15 }
            foreach ($value in @($accepted.verification_candidate_commit,$accepted.post_run_evidence_candidate_commit,$accepted.non_live_all_receipt_sha256,$accepted.pre_live_receipt_sha256,$accepted.direct_layer6_receipt_sha256)) {
                if ((Get-Wp8NonLiveCurrentStateDisposition ($acceptedText.Replace($value,('0' * $value.Length))) $accepted) -ne 'invalid') { exit 16 }
            }
            exit 0
            """;
        Assert.AreEqual(0, RunPowerShellScript(command),
            "NonLiveAll accepted generic or weakened WP9 authority.");
    }

    private static int RunValidator(Action<Dictionary<string, JsonObject>>? mutation)
    {
        string root = RepositoryRoot();
        string temp = Path.Combine(Path.GetTempPath(), "infinium-wp8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Dictionary<string, JsonObject> docs = new(StringComparer.Ordinal)
            {
                ["matrix"] = ReadNode(root, "docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json"),
                ["profile"] = ReadNode(root, RelativeTemplates[0]),
                ["qualification"] = ReadNode(root, RelativeTemplates[1]),
                ["source"] = ReadNode(root, RelativeTemplates[2]),
                ["candidate"] = ReadNode(root, RelativeTemplates[3]),
            };
            mutation?.Invoke(docs);
            foreach ((string name, JsonObject document) in docs)
            {
                File.WriteAllText(Path.Combine(temp, name + ".json"), document.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                }), new UTF8Encoding(false));
            }

            ProcessStartInfo start = new("pwsh.exe")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(Path.Combine(root, "eng", "validate-m1-slice6-wp8-prelive.ps1"));
            start.ArgumentList.Add("-MatrixPath");
            start.ArgumentList.Add(Path.Combine(temp, "matrix.json"));
            start.ArgumentList.Add("-ProfileTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "profile.json"));
            start.ArgumentList.Add("-QualificationTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "qualification.json"));
            start.ArgumentList.Add("-SourceClaimTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "source.json"));
            start.ArgumentList.Add("-CandidateTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "candidate.json"));
            using Process process = Process.Start(start)!;
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            Assert.IsTrue(process.WaitForExit(30_000), "WP8 validator mutation process timed out.");
            if (process.ExitCode != 0 && mutation is null)
            {
                Console.WriteLine(standardOutput);
                Console.WriteLine(standardError);
            }
            return process.ExitCode;
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static JsonObject ReadNode(string root, string relative) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))!.AsObject();

    private static int RunPowerShellScript(string command)
    {
        string path = Path.Combine(Path.GetTempPath(), "infinium-wp8-script-" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            File.WriteAllText(path, command, new UTF8Encoding(false));
            ProcessStartInfo start = new("pwsh.exe")
            {
                WorkingDirectory = RepositoryRoot(),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(path);
            using Process process = Process.Start(start)!;
            Assert.IsTrue(process.WaitForExit(30_000), "PowerShell mutation script timed out.");
            return process.ExitCode;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string RepositoryRoot() => Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
}
