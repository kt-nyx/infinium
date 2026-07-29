using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed partial class ProtoAuthorityContractTests
{
    private static readonly string[] PrivilegedMessagesWithReservedAuthorityBand =
    [
        "infinium/helper/v1/helper.proto:HelperPrivateFrame",
        "infinium/helper/v1/helper.proto:CredentialLifecycleAssignment",
        "infinium/helper/v1/helper.proto:ProviderDispatchAssignment",
        "infinium/helper/v1/helper.proto:DispatchRevalidationRequest",
        "infinium/helper/v1/helper.proto:HelperStatus",
        "infinium/helper/v1/helper.proto:HelperStagedOutputManifest",
        "infinium/worker/v1/worker.proto:WorkerAssignment",
        "infinium/worker/v1/worker.proto:StagedOutputManifest",
        "infinium/protocol/v1/protocol.proto:ApplicationHandshakeRequest",
        "infinium/protocol/v1/protocol.proto:WorkerPrivateBootstrap",
        "infinium/protocol/v1/protocol.proto:WorkerHandshakeRequest",
        "infinium/protocol/v1/protocol.proto:HelperPrivateBootstrap",
    ];

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void OperationOwnerAndProviderDispatchUseClosedTypedAuthorityIdentities()
    {
        string identities = ReadProto("infinium/domain/v1/identities.proto");
        string owner = ExtractMessageBody(identities, "OperationOwner");
        StringAssert.Contains(owner, "oneof owner");
        StringAssert.Contains(owner, "RunId analysis_run_id = 1;");
        StringAssert.Contains(owner, "EvidenceAcquisitionRunId evidence_acquisition_run_id = 2;");
        StringAssert.Contains(owner, "MaintenanceOperationId maintenance_operation_id = 3;");

        string application = ReadProto("infinium/application/v1/application.proto");
        string lifecycleChanged = ExtractMessageBody(application, "LifecycleChanged");
        StringAssert.Contains(
            lifecycleChanged,
            "LifecycleTransitionRecordKind transition_record_kind = 5;");
        StringAssert.Contains(
            lifecycleChanged,
            "SemanticVersion lifecycle_policy_version = 6;");

        string helper = ReadProto("infinium/helper/v1/helper.proto");
        string assignment = ExtractMessageBody(helper, "ProviderDispatchAssignment");
        AssertTypedProviderBinding(assignment);
        StringAssert.Contains(assignment, "ProviderRequestPayload request = 10;");
        StringAssert.Contains(assignment, "CapabilitySnapshotId capability_snapshot_id = 11;");
        StringAssert.Contains(assignment, "PriceSnapshotId price_snapshot_id = 12;");
        StringAssert.Contains(assignment, "ReservationGroupId reservation_group_id = 13;");

        string revalidation = ExtractMessageBody(helper, "DispatchRevalidationRequest");
        AssertTypedProviderBinding(revalidation);
        StringAssert.Contains(revalidation, "ContentDigest exact_request = 9;");
        StringAssert.Contains(revalidation, "CapabilitySnapshotId capability_snapshot_id = 13;");
        StringAssert.Contains(revalidation, "PriceSnapshotId price_snapshot_id = 14;");

        Assert.IsFalse(assignment.Contains("string provider", StringComparison.Ordinal));
        Assert.IsFalse(assignment.Contains("string purpose", StringComparison.Ordinal));
        Assert.IsFalse(assignment.Contains("string endpoint", StringComparison.Ordinal));
        Assert.IsFalse(revalidation.Contains("string provider", StringComparison.Ordinal));
        Assert.IsFalse(revalidation.Contains("string purpose", StringComparison.Ordinal));
        Assert.IsFalse(revalidation.Contains("string endpoint", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void PrivilegedMessagesReserveAuthorityBandAndForbiddenNames()
    {
        foreach (string specification in PrivilegedMessagesWithReservedAuthorityBand)
        {
            string[] parts = specification.Split(':', 2);
            string body = ExtractMessageBody(ReadProto(parts[0]), parts[1]);
            StringAssert.Matches(
                body,
                ReservedAuthorityBandRegex(),
                $"{specification} must reserve fields 90 through 99.");
            StringAssert.Matches(
                body,
                ForbiddenReservedNameRegex(),
                $"{specification} must reserve at least one authority-bearing forbidden name.");
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void WorkerAndHelperStringFieldsRemainOnExplicitInertAllowlist()
    {
        AssertStringFieldAllowlist(
            "infinium/helper/v1/helper.proto",
            ["idempotency_identity", "typed_relative_name"]);
        AssertStringFieldAllowlist(
            "infinium/worker/v1/worker.proto",
            [
                "bootstrap_id",
                "adapter_or_analyzer_id",
                "logical_name",
                "typed_relative_name",
                "inert_status_text",
                "staging_receipt_id",
            ]);
        AssertStringFieldAllowlist(
            "infinium/protocol/v1/protocol.proto",
            ["bootstrap_id"]);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void HelperUnknownFieldsAreExplicitlyRejectedRatherThanForwarded()
    {
        string readme = TestRepository.Read("contracts", "protobuf", "README.md");
        string helper = ReadProto("infinium/helper/v1/helper.proto");

        StringAssert.Contains(
            readme,
            "A helper private frame, or any nested credential/provider-helper message,");
        StringAssert.Contains(
            readme,
            "must reject the frame and must not retain, forward, echo, stage, or log the");
        StringAssert.Contains(
            helper,
            "Decoders MUST reject the entire frame if this envelope or any nested");
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void ProtoMessagesDoNotReuseFieldNumbersWithinAMessage()
    {
        string protobufRoot = TestRepository.PathFromRoot("contracts", "protobuf");
        foreach (string path in Directory.EnumerateFiles(protobufRoot, "*.proto", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach ((string Name, string Body) message in EnumerateMessages(text))
            {
                int[] numbers = FieldNumberRegex()
                    .Matches(message.Body)
                    .Select(match => int.Parse(
                        match.Groups["number"].Value,
                        System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray();
                Assert.AreEqual(
                    numbers.Length,
                    numbers.Distinct().Count(),
                    $"{Path.GetRelativePath(protobufRoot, path)}:{message.Name}");
            }
        }
    }

    private static void AssertTypedProviderBinding(string body)
    {
        StringAssert.Contains(body, "ProviderAccessProfileId access_profile_id");
        StringAssert.Contains(body, "CredentialGenerationId generation_id");
        StringAssert.Contains(body, "uint64 revocation_epoch");
        StringAssert.Contains(body, "ProviderAccountIdentityId provider_account_identity_id");
        StringAssert.Contains(body, "BillingScopeIdentityId billing_scope_identity_id");
        StringAssert.Contains(body, "ScanConfigurationId effective_scan_configuration_id");
        StringAssert.Contains(body, "ProviderKind provider");
        StringAssert.Contains(body, "CredentialPurpose purpose");
        StringAssert.Contains(body, "ProviderEndpoint endpoint");
    }

    private static void AssertStringFieldAllowlist(
        string relativePath,
        IReadOnlyCollection<string> allowedNames)
    {
        string text = ReadProto(relativePath);
        string[] names = StringFieldRegex()
            .Matches(text)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected = allowedNames
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(expected, names, relativePath);
    }

    private static string ReadProto(string relativePath)
    {
        string[] parts = ["contracts", "protobuf", .. relativePath.Split('/')];
        return TestRepository.Read(parts);
    }

    private static string ExtractMessageBody(string text, string messageName)
    {
        return EnumerateMessages(text)
            .Single(message => StringComparer.Ordinal.Equals(message.Name, messageName))
            .Body;
    }

    private static IEnumerable<(string Name, string Body)> EnumerateMessages(string text)
    {
        foreach (Match match in MessageStartRegex().Matches(text))
        {
            int openBrace = text.IndexOf('{', match.Index);
            int depth = 1;
            int index = openBrace + 1;
            while (index < text.Length && depth > 0)
            {
                if (text[index] == '{')
                {
                    depth++;
                }
                else if (text[index] == '}')
                {
                    depth--;
                }
                index++;
            }

            Assert.AreEqual(0, depth, $"Unterminated protobuf message {match.Groups["name"].Value}.");
            yield return (
                match.Groups["name"].Value,
                text[(openBrace + 1)..(index - 1)]);
        }
    }

    [GeneratedRegex(
        @"\bmessage\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{",
        RegexOptions.CultureInvariant)]
    private static partial Regex MessageStartRegex();

    [GeneratedRegex(
        @"=\s*(?<number>[1-9][0-9]*)\s*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex FieldNumberRegex();

    [GeneratedRegex(
        @"\bstring\s+(?<name>[a-z][a-z0-9_]*)\s*=",
        RegexOptions.CultureInvariant)]
    private static partial Regex StringFieldRegex();

    [GeneratedRegex(
        @"\breserved\s+90\s+to\s+99\s*;",
        RegexOptions.CultureInvariant)]
    private static partial Regex ReservedAuthorityBandRegex();

    [GeneratedRegex(
        @"\breserved\s+[^;]*""(?:credential_target|provider_secret|secret_bytes|database_path|publish|sql|path|url|command_line|arbitrary_url)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex ForbiddenReservedNameRegex();
}
