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
        JsonObject acceptance = matrix["acceptance_binding"]!.AsObject();
        string acceptanceState = acceptance["state"]!.GetValue<string>();
        Assert.IsTrue(acceptanceState is "correction-verification-pending" or "accepted-closeout");
        StringAssert.Matches(acceptance["verification_candidate_commit"]!.GetValue<string>(),
            new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));
        Assert.AreEqual(matrix["candidate_binding"]!["wp8_verification_candidate_commit"]!.GetValue<string>(),
            acceptance["verification_candidate_commit"]!.GetValue<string>());
        string[] closeoutFields =
        [
            "post_run_evidence_candidate_commit", "non_live_all_receipt_sha256",
            "pre_live_receipt_sha256", "direct_layer6_receipt_sha256",
        ];
        if (acceptanceState == "correction-verification-pending")
        {
            foreach (string field in closeoutFields)
            {
                Assert.AreEqual("pending-until-post-run-evidence-freeze", acceptance[field]!.GetValue<string>());
            }
        }
        else
        {
            StringAssert.Matches(acceptance[closeoutFields[0]]!.GetValue<string>(),
                new System.Text.RegularExpressions.Regex("^[0-9a-f]{40}$"));
            foreach (string field in closeoutFields.Skip(1))
            {
                StringAssert.Matches(acceptance[field]!.GetValue<string>(),
                    new System.Text.RegularExpressions.Regex("^[0-9a-f]{64}$"));
            }
        }
        string acceptanceBinding = acceptance.ToJsonString();

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
        if (acceptanceState == "correction-verification-pending")
        {
            StringAssert.Contains(normalizedReadme, "WP8 closeout correction and complete non-live reverification are active.");
            StringAssert.Contains(normalizedReadme, "WP9 is not eligible");
            StringAssert.Contains(normalizedReadme, "The earlier WP8 acceptance identities and receipts are retained only as superseded historical evidence and do not certify the corrected candidate.");
            Assert.IsFalse(normalizedReadme.Contains("The next eligible action is only the owner's decision whether to begin WP9", StringComparison.Ordinal));
        }
        else
        {
            StringAssert.Contains(normalizedReadme, "Corrected WP8 is independently accepted.");
            StringAssert.Contains(normalizedReadme, acceptance["verification_candidate_commit"]!.GetValue<string>());
            StringAssert.Contains(normalizedReadme, acceptance["post_run_evidence_candidate_commit"]!.GetValue<string>());
            StringAssert.Contains(normalizedReadme, acceptance["non_live_all_receipt_sha256"]!.GetValue<string>());
            StringAssert.Contains(normalizedReadme, acceptance["pre_live_receipt_sha256"]!.GetValue<string>());
            StringAssert.Contains(normalizedReadme, acceptance["direct_layer6_receipt_sha256"]!.GetValue<string>());
            Assert.IsTrue(
                normalizedReadme.Contains("The next eligible action is only the owner's decision whether to begin WP9", StringComparison.Ordinal)
                || normalizedReadme.Contains("WP9 non-effectful production-profile preparation is active.", StringComparison.Ordinal)
                || normalizedReadme.Contains("WP9 non-effectful production-profile preparation is complete at close-ready", StringComparison.Ordinal)
                || normalizedReadme.Contains("WP9 non-effectful production-profile preparation is in bounded correction and reverification.", StringComparison.Ordinal)
                || normalizedReadme.Contains("WP9 non-effectful production-profile preparation is frozen at corrected close-ready implementation", StringComparison.Ordinal),
                "Accepted WP8 must retain either its exact closeout handoff or the exact later no-effect WP9 preparation handoff.");
        }
        StringAssert.Contains(normalizedReadme, "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority.");
        StringAssert.Contains(normalizedReadme, "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.");

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
            ("acceptance-cross-document", docs =>
            {
                string state = docs["matrix"]["acceptance_binding"]!["state"]!.GetValue<string>();
                docs["profile"]["acceptance_binding"]!["state"] =
                    state == "accepted-closeout" ? "correction-verification-pending" : "accepted-closeout";
            }),
            ("acceptance-arbitrary-state", docs =>
            {
                foreach (JsonObject document in docs.Values)
                {
                    document["acceptance_binding"]!["state"] = "arbitrary";
                }
            }),
            ("acceptance-mixed-accepted-pending-fields", docs =>
            {
                foreach (JsonObject document in docs.Values)
                {
                    JsonNode binding = document["acceptance_binding"]!;
                    if (binding["state"]!.GetValue<string>() == "accepted-closeout")
                    {
                        binding["post_run_evidence_candidate_commit"] = "pending-until-post-run-evidence-freeze";
                        binding["non_live_all_receipt_sha256"] = "pending-until-post-run-evidence-freeze";
                        binding["pre_live_receipt_sha256"] = "pending-until-post-run-evidence-freeze";
                        binding["direct_layer6_receipt_sha256"] = "pending-until-post-run-evidence-freeze";
                    }
                    else
                    {
                        binding["state"] = "accepted-closeout";
                    }
                }
            }),
            ("acceptance-mixed-pending-exact-fields", docs =>
            {
                foreach (JsonObject document in docs.Values)
                {
                    JsonNode binding = document["acceptance_binding"]!;
                    binding["post_run_evidence_candidate_commit"] = new string('1', 40);
                    binding["non_live_all_receipt_sha256"] = new string('2', 64);
                    binding["pre_live_receipt_sha256"] = new string('3', 64);
                    binding["direct_layer6_receipt_sha256"] = new string('4', 64);
                }
            }),
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
        string root = RepositoryRoot();
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        StringAssert.Contains(script, "'NonLiveAll'");
        StringAssert.Contains(script, "function Invoke-NonLiveAllGate");
        StringAssert.Contains(script, "function Get-Wp8Layer6CurrentStateDisposition");
        StringAssert.Contains(script, "$isWp8StructuredCurrentState");
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
        Assert.AreNotEqual(0, RunLayer6Mode(root, null),
            "Ordinary Layer6 unexpectedly unprotected the WP8 current-state path.");
        Assert.AreNotEqual(0, RunLayer6Mode(root, "wp8"),
            "Explicit WP8 pre-live closeout mode incorrectly accepted the later WP9 state.");
        string currentState = TestRepository.Read("docs", "current-state.md");
        int ownerStop = RunLayer6Mode(root, "wp9");
        int reviewCloseout = RunLayer6Mode(root, "wp9-review");
        if (currentState.Contains("non-effectful production-profile preparation verification and independent review only", StringComparison.Ordinal))
        {
            Assert.AreEqual(0, ownerStop, "Exact WP9 pre-review owner-stop state was rejected.");
            Assert.AreNotEqual(0, reviewCloseout, "Pre-review owner-stop state was admitted as reviewed closeout.");
        }
        else if (currentState.Contains("remains pending exact owner acceptance", StringComparison.Ordinal))
        {
            Assert.AreNotEqual(0, ownerStop, "Reviewed state was admitted as pre-review owner-stop.");
            Assert.AreEqual(0, reviewCloseout, "Exact reviewed-pending-owner closeout was rejected.");
        }
        else
        {
            StringAssert.Contains(currentState, "review-closeout correction and reverification only");
            Assert.AreNotEqual(0, ownerStop, "Correction state was admitted as pre-review owner-stop.");
            Assert.AreNotEqual(0, reviewCloseout, "Correction state was admitted as reviewed closeout.");
        }
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
            "Get-Wp8Wp9OwnerStopPaths",
            "Test-Wp8CorrectionCurrentState",
            "Test-Wp8CorrectionReadme",
            "Test-Wp8AcceptedHandoffCurrentState",
            "Test-Wp8AcceptedHandoffReadme",
            "Test-Wp9OwnerStopCurrentState",
            "Test-Wp9OwnerStopReadme",
            "Test-Wp9ReviewCloseoutCorrectionCurrentState",
            "Test-Wp9ReviewCloseoutCorrectionReadme",
            "Test-Wp9ReviewedOwnerPendingState",
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
        string currentState = """
            | Current authorized work | `M1/S6/WP8` closeout correction and complete non-live reverification only; WP9 is not eligible. |
            | Next eligible action | Freeze the corrected WP8 verification candidate, bind its non-executable templates, then run the complete non-live floor and fresh independent review; do not begin WP9 |
            | Later work | WP9 remains ineligible until the corrected WP8 evidence is independently accepted and an exact no-effect closeout is committed. No prior WP8 acceptance or template grants inherited authority |
            The former evidence was later invalidated as current handoff authority by current-HEAD review and remains historical evidence only.
            Only WP8 closeout correction and complete non-live reverification are eligible; WP9 remains ineligible.
            No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority.
            No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.
            """;
        string readme = """
            WP8 closeout correction and complete non-live reverification are active. WP9 is not eligible.
            The earlier WP8 acceptance identities and receipts are retained only as superseded historical evidence and do not certify the corrected candidate.
            No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority.
            No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.
            """;
        string record = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6", "record.md"));
        const string noEffect = "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.";
        const string noInheritance = "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority";
        string[] noEffectFacts =
        [
            "API-key use", "live-manifest execution", "native Credential Manager operation", "DNS operation",
            "public-network operation", "provider request", "billable operation", "production-profile materialization/use",
        ];
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        string ownerContract = Path.Combine(root, "eng", "wp9-owner-documentation-contract.ps1")
            .Replace("'", "''", StringComparison.Ordinal);
        string command = $$"""
            . '{{ownerContract}}'
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
            $wp9Current = @"
            | Current authorized work | ``M1/S6/WP9`` non-effectful production-profile preparation verification and independent review only. Corrected close-ready implementation ``ffffffffffffffffffffffffffffffffffffffff`` is bound by manifest ``infinium.m1-s6.wp9.production-profile-authorization/ded946a6-e1b8-4c8e-95eb-5ef59619804f``, but no exact replacement independent-review or owner-acceptance record exists yet. No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. |
            Accepted corrected ``M1/S6/WP8`` candidate $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit) $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256)
            | Next eligible action | Run the complete non-live floor and fresh independent security/semantic/diff review against the exact corrected manifest binding. Only an accepted exact reviewed candidate may then reach the owner accept-or-decline stop. The transport-qualification request manifest remains unmaterialized and blocked pending separate ``safety_identifier`` authority resolution plus successful profile enrollment. |
            {{noInheritance}}
            "@
            $wp9Readme = "Corrected WP8 is independently accepted. $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit) $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256) WP9 non-effectful production-profile preparation is frozen at corrected close-ready implementation ``ffffffffffffffffffffffffffffffffffffffff``. The canonical non-incremental Release build pins both informational-version and SourceLink revision identities to that exact commit. Two consecutive clean builds reproduced the coordinator, helper, and complete 126-file execution closure exactly. No corrected independent-review or owner-acceptance record exists, and WP9 execution remains ineligible. The transport-qualification request manifest is not materialized. {{noInheritance}}. {{noEffect}}"
            if (-not (Test-Wp9OwnerStopCurrentState $wp9Current $accepted) -or -not (Test-Wp9OwnerStopReadme $wp9Readme $accepted)) { exit 21 }
            $wp9Paths = @(Get-Wp8Wp9OwnerStopPaths)
            if ((Get-Wp8PostVerificationDisposition $wp9Paths $wp9Current $wp9Readme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'exact-wp9-owner-stop-no-effect-state') { exit 21 }
            foreach ($requiredPath in @('Directory.Build.targets','eng/wp9-owner-documentation-contract.ps1')) {
                $missingPath = @($wp9Paths | Where-Object { $_ -cne $requiredPath })
                if ((Get-Wp8PostVerificationDisposition $missingPath $wp9Current $wp9Readme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 22 }
            }
            if ((Get-Wp8PostVerificationDisposition @($wp9Paths + 'src/Infinium.Application/Unauthorized.cs') $wp9Current $wp9Readme $baseRecord ($baseRecord + $acceptanceRecord) $accepted) -ne 'invalid') { exit 22 }
            foreach ($weakened in @(
                $wp9Current.Replace('UI launch, ',''),
                $wp9Current.Replace('but no exact replacement independent-review or owner-acceptance record exists yet.','is executable.'),
                $wp9Current.Replace('{{noInheritance}}',''),
                $wp9Current.Replace($accepted.non_live_all_receipt_sha256,('0' * 64)))) {
                if (Test-Wp9OwnerStopCurrentState $weakened $accepted) { exit 22 }
            }
            foreach ($weakened in @(
                $wp9Readme.Replace('frozen at corrected close-ready implementation','is executable at'),
                $wp9Readme.Replace('No corrected independent-review or owner-acceptance record exists','Execution authority exists'),
                $wp9Readme.Replace('Two consecutive clean builds reproduced the coordinator, helper, and complete 126-file execution closure exactly.',''),
                $wp9Readme.Replace('{{noEffect}}',''),
                $wp9Readme.Replace('{{noInheritance}}',''))) {
                if (Test-Wp9OwnerStopReadme $weakened $accepted) { exit 23 }
            }
            $reviewCandidate=('f'*40); $manifestSha=('1'*64); $manifestId='infinium.m1-s6.wp9.production-profile-authorization/test'
            $reviewRequirements=Get-Wp9ReviewedOwnerPendingDocumentationRequirements -ManifestId $manifestId -ManifestSha256 $manifestSha -CloseReadyCommit ('9'*40) -ReviewedCandidate $reviewCandidate
            $reviewCurrent=[string]::Join("`n",@($reviewRequirements.current_state))
            $reviewReadme=[string]::Join("`n",@($reviewRequirements.readme))
            $reviewedRecord=$baseRecord+$acceptanceRecord
            $marker=Get-Wp9ReviewAcceptanceMarker -ManifestId $manifestId -ManifestSha256 $manifestSha -ReviewedCandidate $reviewCandidate
            $reviewHeadRecord=$reviewedRecord+"`n`n"+$marker
            $reviewBinding=[pscustomobject]@{
              manifest_id=$manifestId; manifest_sha256=$manifestSha; close_ready_commit=('9'*40)
              reviewed_candidate_commit=$reviewCandidate; reviewed_record_text=$reviewedRecord
              closeout_paths=@('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')
            }
            if((Get-Wp8PostVerificationDisposition $wp9Paths $reviewCurrent $reviewReadme $baseRecord $reviewHeadRecord $accepted $reviewBinding) -ne 'exact-wp9-reviewed-owner-pending-no-effect-state'){exit 24}
            foreach($mutatedBinding in @(
              [pscustomobject]@{manifest_id='wrong';manifest_sha256=$manifestSha;close_ready_commit=('9'*40);reviewed_candidate_commit=$reviewCandidate;reviewed_record_text=$reviewedRecord;closeout_paths=$reviewBinding.closeout_paths},
              [pscustomobject]@{manifest_id=$manifestId;manifest_sha256=('2'*64);close_ready_commit=('9'*40);reviewed_candidate_commit=$reviewCandidate;reviewed_record_text=$reviewedRecord;closeout_paths=$reviewBinding.closeout_paths},
              [pscustomobject]@{manifest_id=$manifestId;manifest_sha256=$manifestSha;close_ready_commit=('8'*40);reviewed_candidate_commit=$reviewCandidate;reviewed_record_text=$reviewedRecord;closeout_paths=$reviewBinding.closeout_paths},
              [pscustomobject]@{manifest_id=$manifestId;manifest_sha256=$manifestSha;close_ready_commit=('9'*40);reviewed_candidate_commit=('7'*40);reviewed_record_text=$reviewedRecord;closeout_paths=$reviewBinding.closeout_paths},
              [pscustomobject]@{manifest_id=$manifestId;manifest_sha256=$manifestSha;close_ready_commit=('9'*40);reviewed_candidate_commit=$reviewCandidate;reviewed_record_text=$reviewedRecord;closeout_paths=@($reviewBinding.closeout_paths+'src/unauthorized.cs')})) {
              if((Get-Wp8PostVerificationDisposition $wp9Paths $reviewCurrent $reviewReadme $baseRecord $reviewHeadRecord $accepted $mutatedBinding) -ne 'invalid'){exit 25}
            }
            foreach($mutated in @(
              $reviewCurrent.Replace('No execution or effect is authorized.',''),
              $reviewCurrent.Replace('No packet, review, or prior owner statement grants inherited authority.',''),
              $reviewReadme.Replace('No authority is inherited.',''),
              $reviewHeadRecord.Replace('security,semantics,diff','security,diff'),
              ($reviewHeadRecord+"`nWP9_PROFILE_OWNER_ACCEPTANCE invalid"))) {
              $c=$reviewCurrent; $r=$reviewReadme; $rec=$reviewHeadRecord
              if($mutated -like '*Current authorized work*'){$c=$mutated}elseif($mutated -like '*WP9 production-profile manifest*'){$r=$mutated}else{$rec=$mutated}
              if((Get-Wp8PostVerificationDisposition $wp9Paths $c $r $baseRecord $rec $accepted $reviewBinding) -ne 'invalid'){exit 26}
            }
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
        StringAssert.Contains(script, "one exact state-specific closeout commit with no extra paths");
        StringAssert.Contains(script, "validate-m1-slice6-wp9-profile-authorization.ps1");
        StringAssert.Contains(script, "profileManifest.candidate_binding.close_ready_implementation_commit");
        string currentState = """
            | Current authorized work | `M1/S6/WP8` closeout correction and complete non-live reverification only; WP9 is not eligible. |
            | Next eligible action | Freeze the corrected WP8 verification candidate, bind its non-executable templates, then run the complete non-live floor and fresh independent review; do not begin WP9 |
            | Later work | WP9 remains ineligible until the corrected WP8 evidence is independently accepted and an exact no-effect closeout is committed. No prior WP8 acceptance or template grants inherited authority |
            The former evidence was later invalidated as current handoff authority by current-HEAD review and remains historical evidence only.
            Only WP8 closeout correction and complete non-live reverification are eligible; WP9 remains ineligible.
            No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority.
            No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.
            """;
        const string noEffect = "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.";
        const string noInheritance = "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority";
        static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        string ownerContract = Path.Combine(root, "eng", "wp9-owner-documentation-contract.ps1")
            .Replace("'", "''", StringComparison.Ordinal);
        string command = $$"""
            . '{{ownerContract}}'
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
            $wp9 = @"
            | Current authorized work | ``M1/S6/WP9`` non-effectful production-profile enrollment packet preparation and verification only. Exact new-only manifest ``infinium.m1-s6.wp9.production-profile-authorization/ded946a6-e1b8-4c8e-95eb-5ef59619804f`` remains binding-pending and is not executable. {{noEffect}} |
            | Next eligible action | Complete and independently review the exact WP9 production-profile close-ready binding, then stop for an exact owner decision. |
            no WP9 transport-qualification request manifest may be materialized until that request-field authority is resolved.
            {{noInheritance}}. {{noEffect}}
            "@
            if ((Get-Wp8NonLiveCurrentStateDisposition $wp9 $accepted) -ne 'exact-wp9-profile-preparation-no-effect-state') { exit 17 }
            foreach ($weakened in @(
                $wp9.Replace('binding-pending and is not executable.','ready'),
                $wp9.Replace('no WP9 transport-qualification request manifest may be materialized until that request-field authority is resolved.',''),
                $wp9.Replace('non-effectful production-profile enrollment packet preparation and verification only.','production profile execution.'),
                $wp9.Replace('{{noInheritance}}',''),
                $wp9.Replace('{{noEffect}}',''))) {
                if ((Get-Wp8NonLiveCurrentStateDisposition $weakened $accepted) -ne 'invalid') { exit 18 }
            }
            $ownerStop = @"
            | Current authorized work | ``M1/S6/WP9`` non-effectful production-profile preparation verification and independent review only. Corrected close-ready implementation ``ffffffffffffffffffffffffffffffffffffffff`` is bound by manifest ``infinium.m1-s6.wp9.production-profile-authorization/ded946a6-e1b8-4c8e-95eb-5ef59619804f``, but no exact replacement independent-review or owner-acceptance record exists yet. The prior binding at ``1c3b64a651361c147cba018b8054cb2f0ac4f036`` is historical and non-executable. No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. |
            Accepted corrected ``M1/S6/WP8`` candidate $($accepted.verification_candidate_commit) $($accepted.post_run_evidence_candidate_commit) $($accepted.non_live_all_receipt_sha256) $($accepted.pre_live_receipt_sha256) $($accepted.direct_layer6_receipt_sha256)
            | Next eligible action | Run the complete non-live floor and fresh independent security/semantic/diff review against the exact corrected manifest binding. Only an accepted exact reviewed candidate may then reach the owner accept-or-decline stop. The transport-qualification request manifest remains unmaterialized and blocked pending separate ``safety_identifier`` authority resolution plus successful profile enrollment. |
            {{noInheritance}}.
            "@
            if ((Get-Wp8NonLiveCurrentStateDisposition $ownerStop $accepted) -ne 'exact-wp9-profile-owner-stop-no-effect-state') { exit 19 }
            foreach ($weakened in @(
                $ownerStop.Replace('non-effectful production-profile preparation verification and independent review only.','production profile execution.'),
                $ownerStop.Replace('but no exact replacement independent-review or owner-acceptance record exists yet.','is executable.'),
                $ownerStop.Replace('Run the complete non-live floor and fresh independent security/semantic/diff review against the exact corrected manifest binding.','Execute WP9.'),
                $ownerStop.Replace('The transport-qualification request manifest remains unmaterialized and blocked','The transport-qualification request manifest is ready'),
                $ownerStop.Replace('UI launch, ',''),
                $ownerStop.Replace($accepted.non_live_all_receipt_sha256,('0' * 64)),
                $ownerStop.Replace('{{noInheritance}}',''),
                $ownerStop.Replace('No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',''))) {
                if ((Get-Wp8NonLiveCurrentStateDisposition $weakened $accepted) -ne 'invalid') { exit 20 }
            }
            $ownerStopCorrection = @"
            | Current authorized work | ``M1/S6/WP9`` bounded non-effectful owner-stop correction and reverification only. Terminal review invalidated binding ``4dffe0ba2ad799ba68a67a6dd091a0a4c728d5b0``: build drift. The manifest is binding-pending; no independent-review or owner-acceptance record exists for corrected bytes. No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. |
            | Next eligible action | Freeze and bind the corrected commit-stable Release closure, action-time readiness evidence, and exact post-review/owner authority-document transitions; then rerun the complete non-live floor and fresh independent security/semantic/diff review. WP9 execution remains ineligible. |
            {{noInheritance}}.
            "@
            if ((Get-Wp8NonLiveCurrentStateDisposition $ownerStopCorrection $accepted) -ne 'exact-wp9-owner-stop-correction-no-effect-state') { exit 21 }
            foreach ($weakened in @(
                $ownerStopCorrection.Replace('bounded non-effectful owner-stop correction and reverification only.','production execution.'),
                $ownerStopCorrection.Replace('Terminal review invalidated binding','Review retained binding'),
                $ownerStopCorrection.Replace('The manifest is binding-pending; no independent-review or owner-acceptance record exists for corrected bytes.',''),
                $ownerStopCorrection.Replace('WP9 execution remains ineligible.','WP9 execution is eligible.'),
                $ownerStopCorrection.Replace('{{noInheritance}}',''),
                $ownerStopCorrection.Replace('No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',''))) {
                if ((Get-Wp8NonLiveCurrentStateDisposition $weakened $accepted) -ne 'invalid') { exit 22 }
            }
            $repeatCorrection = @"
            | Current authorized work | ``M1/S6/WP9`` bounded non-effectful owner-stop correction and reverification only. Post-binding reproduction invalidated binding ``38fcc90d45459d9ecc2e3dc6f56b187eb68bfc05``: inventory drift. The manifest is binding-pending; no independent-review or owner-acceptance record exists for corrected bytes. No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. |
            | Next eligible action | Freeze and bind an exact non-incremental SourceRevisionId-pinned Release closure, prove repeated-build identity, then rerun the complete non-live floor and fresh independent security/semantic/diff review. WP9 execution remains ineligible. |
            {{noInheritance}}.
            "@
            if ((Get-Wp8NonLiveCurrentStateDisposition $repeatCorrection $accepted) -ne 'exact-wp9-owner-stop-correction-no-effect-state') { exit 23 }
            foreach ($weakened in @(
                $repeatCorrection.Replace('Post-binding reproduction invalidated binding','Reproduction accepted binding'),
                $repeatCorrection.Replace('non-incremental SourceRevisionId-pinned Release closure','ordinary build'),
                $repeatCorrection.Replace('The manifest is binding-pending; no independent-review or owner-acceptance record exists for corrected bytes.',''),
                $repeatCorrection.Replace('{{noInheritance}}',''),
                $repeatCorrection.Replace('No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',''))) {
                if ((Get-Wp8NonLiveCurrentStateDisposition $weakened $accepted) -ne 'invalid') { exit 24 }
            }
            $sourceLinkCorrection = @"
            | Current authorized work | ``M1/S6/WP9`` bounded non-effectful owner-stop correction and reverification only. Post-binding reproduction invalidated binding ``76c7827609364abe7bf852c01cd95156ac98f62c``: SourceLink drift. The manifest is binding-pending; no independent-review or owner-acceptance record exists for corrected bytes. No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized. |
            | Next eligible action | Pin SourceRoot revision metadata together with SourceRevisionId, prove repeated non-incremental full-closure identity across a later binding HEAD, then rerun the complete non-live floor and fresh independent review. WP9 execution remains ineligible. |
            {{noInheritance}}.
            "@
            if ((Get-Wp8NonLiveCurrentStateDisposition $sourceLinkCorrection $accepted) -ne 'exact-wp9-owner-stop-correction-no-effect-state') { exit 25 }
            foreach ($weakened in @(
                $sourceLinkCorrection.Replace('Post-binding reproduction invalidated binding','Reproduction accepted binding'),
                $sourceLinkCorrection.Replace('Pin SourceRoot revision metadata together with SourceRevisionId','Use ordinary build metadata'),
                $sourceLinkCorrection.Replace('The manifest is binding-pending; no independent-review or owner-acceptance record exists for corrected bytes.',''),
                $sourceLinkCorrection.Replace('{{noInheritance}}',''),
                $sourceLinkCorrection.Replace('No API-key use, UI launch, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.',''))) {
                if ((Get-Wp8NonLiveCurrentStateDisposition $weakened $accepted) -ne 'invalid') { exit 26 }
            }
            $reviewCandidate=('f'*40); $manifestSha=('1'*64); $manifestId='infinium.m1-s6.wp9.production-profile-authorization/test'; $closeReady=('9'*40)
            $requirements=Get-Wp9ReviewedOwnerPendingDocumentationRequirements -ManifestId $manifestId -ManifestSha256 $manifestSha -CloseReadyCommit $closeReady -ReviewedCandidate $reviewCandidate
            $reviewCurrent=[string]::Join("`n",@($requirements.current_state)); $reviewReadme=[string]::Join("`n",@($requirements.readme))
            $reviewedRecord='reviewed record'; $marker=Get-Wp9ReviewAcceptanceMarker -ManifestId $manifestId -ManifestSha256 $manifestSha -ReviewedCandidate $reviewCandidate
            $reviewRecord=$reviewedRecord+"`n`n"+$marker
            $reviewBinding=[pscustomobject]@{manifest_id=$manifestId;manifest_sha256=$manifestSha;close_ready_commit=$closeReady;reviewed_candidate_commit=$reviewCandidate;reviewed_record_text=$reviewedRecord;closeout_paths=@('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')}
            if((Get-Wp8NonLiveCurrentStateDisposition $reviewCurrent $accepted $reviewReadme $reviewRecord $reviewBinding) -ne 'exact-wp9-reviewed-owner-pending-no-effect-state'){exit 27}
            foreach($mutation in @(
              @($reviewCurrent.Replace('No execution or effect is authorized.',''),$reviewReadme,$reviewRecord,$reviewBinding),
              @($reviewCurrent,$reviewReadme.Replace('No authority is inherited.',''),$reviewRecord,$reviewBinding),
              @($reviewCurrent,$reviewReadme,$reviewRecord.Replace('security,semantics,diff','security,diff'),$reviewBinding),
              @($reviewCurrent,$reviewReadme,($reviewRecord+"`nWP9_PROFILE_OWNER_ACCEPTANCE invalid"),$reviewBinding),
              @($reviewCurrent,$reviewReadme,$reviewRecord,[pscustomobject]@{manifest_id=$manifestId;manifest_sha256=('2'*64);close_ready_commit=$closeReady;reviewed_candidate_commit=$reviewCandidate;reviewed_record_text=$reviewedRecord;closeout_paths=$reviewBinding.closeout_paths}))) {
              if((Get-Wp8NonLiveCurrentStateDisposition $mutation[0] $accepted $mutation[1] $mutation[2] $mutation[3]) -ne 'invalid'){exit 28}
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

    private static int RunLayer6Mode(string root, string? mode)
    {
        string output = Path.Combine(Path.GetTempPath(), "infinium-wp8-layer6-" + Guid.NewGuid().ToString("N"));
        try
        {
            string baseline = "63e4584f8926227c2a1e12ef31c71a3a88798c7f";
            if (mode == "wp9-review")
            {
                string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
                    "wp9-production-profile-authorization.v1.json");
                using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                string manifestId = manifest.RootElement.GetProperty("manifest_id").GetString()!;
                string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant();
                string record = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6", "record.md"));
                System.Text.RegularExpressions.Match review = System.Text.RegularExpressions.Regex.Match(record,
                    "(?m)^WP9_PROFILE_REVIEW_ACCEPTANCE candidate_commit=([0-9a-f]{40}) manifest_id=" +
                    System.Text.RegularExpressions.Regex.Escape(manifestId) + " sha256=" + sha +
                    " verdicts=security,semantics,diff$");
                if (review.Success) { baseline = review.Groups[1].Value; }
            }
            ProcessStartInfo start = new("pwsh.exe")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in new[]
            {
                "-NoProfile", "-File", Path.Combine(root, "eng", "verify-m1-slice6.ps1"),
                "-Gate", "Layer6Review", "-BaselineCommit", baseline,
                "-CandidateCommit", "HEAD", "-OutputRoot", output,
            })
            {
                start.ArgumentList.Add(argument);
            }
            if (mode == "wp8")
            {
                start.ArgumentList.Add("-Wp8PreLiveCloseout");
            }
            else if (mode == "wp9")
            {
                start.ArgumentList.Add("-Wp9OwnerStopReview");
            }
            else if (mode == "wp9-review")
            {
                start.ArgumentList.Add("-Wp9ReviewCloseout");
            }
            using Process process = Process.Start(start)!;
            _ = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            Assert.IsTrue(process.WaitForExit(30_000), "Layer6 mode contract process timed out.");
            return process.ExitCode;
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    private static string RepositoryRoot() => Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
}
