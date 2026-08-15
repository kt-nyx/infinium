using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderLayer6VerifierContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void Wp9OwnerStopLayer6ModeIsExactAndDoesNotWeakenGenericModes()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        StringAssert.Contains(script, "[switch] $Wp9OwnerStopReview");
        StringAssert.Contains(script, "Wp9OwnerStopReview requires the exact current ready manifest, no-effect state, baseline, candidate, and one four-document binding commit.");
        StringAssert.Contains(script, "Wp9OwnerStopReview requires the exact finite 39-path WP8-to-WP9 candidate set.");
        StringAssert.Contains(script, "Invoke-Layer6ReviewGate $baseline $candidate $false $true");
        StringAssert.Contains(script, "wp9_owner_stop_review = [bool]$Wp9OwnerStopMode");
        StringAssert.Contains(script, "-not $isWp9OwnerStopPath");
        StringAssert.Contains(script, "[switch] $Wp9ReviewCloseout");
        StringAssert.Contains(script, "Wp9ReviewCloseout requires one exact three-document reviewed-pending-owner transition from the exact reviewed candidate.");
        StringAssert.Contains(script, "Wp9ReviewCloseout requires exactly current-state, Slice 6 README, and append-only record in the exact reviewed-pending-owner state.");
        StringAssert.Contains(script, "wp9_review_closeout = [bool]$Wp9ReviewCloseoutMode");
        StringAssert.Contains(script, "-not $isWp9ReviewCloseoutPath");
        StringAssert.Contains(script, "$isWp9ReviewCloseoutPath -or");
        StringAssert.Contains(script, "[switch] $Wp9OwnerAcceptanceCloseout");
        StringAssert.Contains(script, "Wp9OwnerAcceptanceCloseout requires one exact three-document owner-acceptance transition from the exact reviewed-pending-owner baseline.");
        StringAssert.Contains(script, "wp9_owner_acceptance_closeout = [bool]$Wp9OwnerAcceptanceCloseoutMode");
        StringAssert.Contains(script, "-not $isWp9OwnerAcceptanceCloseoutPath");
        StringAssert.Contains(script, "$isWp9OwnerAcceptanceCloseoutPath -or");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void Wp9OwnerAcceptanceCloseoutPredicateRejectsEveryStateCountAndPathMutation()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(
            script, @"(?ms)^function Test-Wp9OwnerAcceptanceCloseoutLayer6Transition\(.*?^\}");
        Assert.IsTrue(function.Success, "Pure WP9 owner-acceptance closeout predicate was not found.");
        string command = $$$"""
            {{{function.Value}}}
            $a=('a'*40); $b=('b'*40); $binding=[pscustomobject]@{reviewed_candidate_commit=('c'*40)}
            $paths=@('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')
            $reviewed='exact-wp9-reviewed-owner-pending-no-effect-state'; $accepted='exact-wp9-owner-accepted-bounded-effect-state'
            if(-not (Test-Wp9OwnerAcceptanceCloseoutLayer6Transition $a $b $b $binding $reviewed $accepted '1' $paths)){exit 10}
            $mutations=@(
              ,@($a,$b,('d'*40),$binding,$reviewed,$accepted,'1',$paths),
              ,@($a,$b,$b,$null,$reviewed,$accepted,'1',$paths),
              ,@($a,$b,$b,$binding,'invalid',$accepted,'1',$paths),
              ,@($a,$b,$b,$binding,$reviewed,'invalid','1',$paths),
              ,@($a,$b,$b,$binding,$reviewed,$accepted,'2',$paths),
              ,@($a,$b,$b,$binding,$reviewed,$accepted,'1',@($paths[0],$paths[1])),
              ,@($a,$b,$b,$binding,$reviewed,$accepted,'1',@($paths+'src/unauthorized.cs')),
              ,@($a,$b,$b,$binding,$reviewed,$accepted,'1',@($paths+$paths[0]))
            )
            foreach($m in $mutations){if(Test-Wp9OwnerAcceptanceCloseoutLayer6Transition @m){exit 11}}
            exit 0
            """;
        System.Diagnostics.ProcessStartInfo start = new("pwsh.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Assert.IsTrue(process.WaitForExit(30_000), "WP9 owner-acceptance closeout mutation test timed out.");
        Assert.AreEqual(0, process.ExitCode, $"WP9 owner-acceptance closeout predicate admitted a mutation. output={output} error={error}");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void Wp9ReviewCloseoutPredicateRejectsEveryIdentityCountAndPathMutation()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(
            script, @"(?ms)^function Test-Wp9ReviewCloseoutLayer6Transition\(.*?^\}");
        Assert.IsTrue(function.Success, "Pure WP9 review-closeout predicate was not found.");
        string command = $$$"""
            {{{function.Value}}}
            $a=('a'*40); $b=('b'*40)
            $binding=[pscustomobject]@{reviewed_candidate_commit=$a}
            $paths=@('docs/current-state.md','docs/plans/milestones/m1/slices/s6/README.md','docs/plans/milestones/m1/slices/s6/record.md')
            if(-not (Test-Wp9ReviewCloseoutLayer6Transition $a $b $b $binding 'exact-wp9-reviewed-owner-pending-no-effect-state' '1' $paths)){exit 10}
            $mutations=@(
              ,@($a,$b,$b,[pscustomobject]@{reviewed_candidate_commit=('c'*40)},'exact-wp9-reviewed-owner-pending-no-effect-state','1',$paths),
              ,@(('c'*40),$b,$b,$binding,'exact-wp9-reviewed-owner-pending-no-effect-state','1',$paths),
              ,@($a,$b,('c'*40),$binding,'exact-wp9-reviewed-owner-pending-no-effect-state','1',$paths),
              ,@($a,$b,$b,$binding,'exact-wp9-profile-owner-stop-no-effect-state','1',$paths),
              ,@($a,$b,$b,$binding,'exact-wp9-reviewed-owner-pending-no-effect-state','2',$paths),
              ,@($a,$b,$b,$binding,'exact-wp9-reviewed-owner-pending-no-effect-state','1',@($paths[0],$paths[1])),
              ,@($a,$b,$b,$binding,'exact-wp9-reviewed-owner-pending-no-effect-state','1',@($paths+'src/unauthorized.cs')),
              ,@($a,$b,$b,$binding,'exact-wp9-reviewed-owner-pending-no-effect-state','1',@($paths+$paths[0]))
            )
            foreach($m in $mutations){if(Test-Wp9ReviewCloseoutLayer6Transition @m){exit 11}}
            exit 0
            """;
        System.Diagnostics.ProcessStartInfo start = new("pwsh.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        Assert.IsTrue(process.WaitForExit(30_000), "WP9 review-closeout mutation test timed out.");
        Assert.AreEqual(0, process.ExitCode, $"WP9 review-closeout predicate admitted a mutation. output={output} error={error}");
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void Layer6ReviewHasCandidateBoundInterfaceAndRetainedReports()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");

        string[] requiredInterfaceAndEvidence =
        [
            "'Layer6Review'",
            "[string] $BaselineCommit",
            "[string] $CandidateCommit",
            "[switch] $HandoffCloseout",
            "[switch] $Wp4OwnerReviewHandoff",
            "[switch] $Wp8PreLiveCloseout",
            "[switch] $OwnerTestProcessCleanup",
            "merge-base --is-ancestor",
            "layer6-changed-paths.json",
            "docs/evaluation/specifications/semantic-fixture-catalog.md",
            "fixtures/public/provider/source-claims/",
            "layer6-relative-links.json",
            "layer6-changed-json.json",
            "layer6-status-claims.json",
            "layer6-gap-inventory.json",
            "layer6-private-archive-absence.json",
            "candidate_bound = $true",
            "wp8_pre_live_closeout = [bool]$Wp8PreLiveCloseoutMode",
            "network_permitted = $false",
            "credential_access_permitted = $false",
        ];

        foreach (string required in requiredInterfaceAndEvidence)
        {
            StringAssert.Contains(script, required);
        }

        StringAssert.Contains(script, "Test-Wp1AllowedPath");
        StringAssert.Contains(script, "Test-Wp1ProtectedPath");
        StringAssert.Contains(script, "isHandoffCurrentState");
        StringAssert.Contains(script, "HandoffCloseout current state must record accepted WP1");
        StringAssert.Contains(script, "or record accepted WP4 and authorize non-live M1/S6/WP8 only");
        StringAssert.Contains(script, "or record accepted WP8 and authorize WP9 owner decision/materialization planning only");
        StringAssert.Contains(script, "`M1/S6/WP8` accumulated non-live verification and pre-live review only");
        StringAssert.Contains(script, "Accepted `M1/S6/WP4` qualification");
        StringAssert.Contains(script, "1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b");
        StringAssert.Contains(script, "3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390");
        StringAssert.Contains(script, "no further Credential Manager operation is authorized");
        StringAssert.Contains(script, "no provider request is authorized");
        StringAssert.Contains(script, "Wp4OwnerReviewHandoff requires exactly one changed candidate docs/current-state.md.");
        StringAssert.Contains(script, "fresh qualification-manifest consumer binding and owner-review preparation only");
        StringAssert.Contains(script, "03ae6929bad069c7c9e351b2ed5bd361e31b89e7");
        StringAssert.Contains(script, "c6e9226e-3d95-496c-bda6-c9142bb6b980");
        StringAssert.Contains(script, "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.");
        StringAssert.Contains(script, "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority");
        StringAssert.Contains(script, "Do not append an owner marker or execute `CredentialNative` during preparation");
        StringAssert.Contains(script, "OwnerTestProcessCleanup requires exactly one changed candidate docs/execution-policy.md");
        StringAssert.Contains(script, "Never terminate by process name alone");
        StringAssert.Contains(script, "JsonDocumentOptions");
        StringAssert.Contains(script, "Assert-NoDuplicateJsonProperties");
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void HandoffCloseoutRequiresEveryWp4ToWp8AuthorityFact()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(
            script,
            @"(?ms)^function Test-HandoffCloseoutCurrentState\(.*?^\}");
        Assert.IsTrue(function.Success, "Pure handoff predicate was not found.");

        string[] required =
        [
            "`M1/S6/WP8` accumulated non-live verification and pre-live review only",
            "Accepted `M1/S6/WP4` qualification",
            "1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b",
            "3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390",
            "no further Credential Manager operation is authorized",
            "no provider request is authorized",
        ];
        string valid = string.Join(Environment.NewLine, required);
        static string Encode(string value) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        string[] encoded = new string[required.Length];
        for (int index = 0; index < required.Length; index++)
        {
            encoded[index] = $"'{Encode(required[index])}'";
        }

        string command = $$"""
            {{function.Value}}
            $valid = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{Encode(valid)}}'))
            if (-not (Test-HandoffCloseoutCurrentState $valid)) { exit 10 }
            foreach ($encoded in @({{string.Join(",", encoded)}})) {
                $token = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))
                if (Test-HandoffCloseoutCurrentState $valid.Replace($token, '')) { exit 11 }
            }
            exit 0
            """;
        System.Diagnostics.ProcessStartInfo start = new("pwsh.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        Assert.IsTrue(process.WaitForExit(30_000), "Handoff predicate mutation test timed out.");
        Assert.AreEqual(0, process.ExitCode, "WP4-to-WP8 handoff accepted a missing authority fact.");
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void HandoffCloseoutRequiresEveryWp8ToWp9AuthorityFact()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(
            script,
            @"(?ms)^function Test-HandoffCloseoutCurrentState\(.*?^\}");
        Assert.IsTrue(function.Success, "Pure handoff predicate was not found.");

        string[] required =
        [
            "`M1/S6/WP9` owner decision and exact authorization-packet materialization planning only",
            "Accepted `M1/S6/WP8` candidate",
            "260a09ecfafea103227f113faf7625a5bf0ce759",
            "fbdb1f03e006a85723b0533d44b2ed06e02cc724",
            "36b980d226e9f9a0e91281a530fc959a211fb696",
            "95919bcfbb6ea79f6ee5f6a8422d23da743c4b4da4f6ba6f9039ac4e69534e78",
            "b8645da64eba4c12bbbc72953753e9e7debbc93ef576ef07cdd96b418399e498",
            "4fe96ddf83e4472ba2bc66f6c046253d3055a69bf32716d934ea222b53072b0c",
            "only fresh exact production-profile and WP9 request authorizations may be prepared",
            "neither may be executed without separate exact owner acceptance",
            "No WP8 template, prior owner statement, packet identity, expiry, profile identity, predecessor acceptance, official-doc result, or request fingerprint grants inherited authority",
            "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.",
            "no provider request is authorized now",
        ];
        string valid = string.Join(Environment.NewLine, required);
        static string Encode(string value) =>
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        string[] encoded = new string[required.Length];
        for (int index = 0; index < required.Length; index++)
        {
            encoded[index] = $"'{Encode(required[index])}'";
        }
        const string noEffectClause = "No API-key use, live-manifest execution, native Credential Manager operation, DNS operation, public-network operation, provider request, billable operation, or production-profile materialization/use is authorized.";
        string[] requiredNoEffectFacts =
        [
            "API-key use",
            "live-manifest execution",
            "native Credential Manager operation",
            "DNS operation",
            "public-network operation",
            "provider request",
            "billable operation",
            "production-profile materialization/use",
        ];
        string[] encodedEffectMutations = new string[requiredNoEffectFacts.Length];
        for (int index = 0; index < requiredNoEffectFacts.Length; index++)
        {
            string mutatedClause = noEffectClause.Replace(requiredNoEffectFacts[index], string.Empty, StringComparison.Ordinal);
            string mutated = valid.Replace(noEffectClause, mutatedClause, StringComparison.Ordinal);
            encodedEffectMutations[index] = $"'{Encode(mutated)}'";
        }

        string command = $$"""
            {{function.Value}}
            $valid = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{Encode(valid)}}'))
            if (-not (Test-HandoffCloseoutCurrentState $valid)) { exit 10 }
            foreach ($encoded in @({{string.Join(",", encoded)}})) {
                $token = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))
                if (Test-HandoffCloseoutCurrentState $valid.Replace($token, '')) { exit 11 }
            }
            foreach ($encoded in @({{string.Join(",", encodedEffectMutations)}})) {
                $mutated = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))
                if (Test-HandoffCloseoutCurrentState $mutated) { exit 12 }
            }
            exit 0
            """;
        System.Diagnostics.ProcessStartInfo start = new("pwsh.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add(command);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)!;
        Assert.IsTrue(process.WaitForExit(30_000), "Handoff predicate mutation test timed out.");
        Assert.AreEqual(0, process.ExitCode, "WP8-to-WP9 handoff accepted a missing authority fact.");
    }
}
