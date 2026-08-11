using Infinium.Application.Provider;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderContractTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderFiniteBoundAuthorityIsExplicitAndDispatchCannotBeAdmitted()
    {
        ProviderInputBoundProofContract blocked = BlockedProof();
        ProviderOperationContractInvariants.ValidateBlockedInputBoundProof(blocked);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ProviderOperationContractInvariants.RequireLocalInputBoundProof(1));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ProviderOperationContractInvariants.RequireDispatchableInputBoundProof(blocked));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.ValidateBlockedInputBoundProof(blocked with { PolicyVersion = "proved" }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderFiniteLimitsEnforceEveryOperationSpecificBoundary()
    {
        ProviderFiniteLimitsContract qualification = new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
        ProviderOperationContractInvariants.Validate(ProviderOperationKind.TransportQualification, qualification);
        foreach (ProviderFiniteLimitsContract above in new[]
        {
            qualification with { MaximumRequestBytes = 16_385 }, qualification with { MaximumInputTokens = 20_481 },
            qualification with { MaximumOutputTokens = 257 }, qualification with { MaximumRawResponseBytes = 262_145 },
            qualification with { MaximumDispatchCount = 2 }, qualification with { MaximumCalculatedNanoUsd = 140_000_001 },
            qualification with { DeadlineMilliseconds = 60_001 },
        })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProviderOperationContractInvariants.Validate(ProviderOperationKind.TransportQualification, above));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderAccessProfileRequiresCompleteLifecycleIdentityGroups()
    {
        ProviderAccessProfileDocument active = new(
            ContractConstants.ProviderAccessProfileSchemaId, "1", Id("profile-1"), Id("generation-1"), 1, 0,
            "openai", "responses", "profile", ProviderProfileState.ActiveVerified,
            ProviderAvailabilityState.Available, Id("account-1"), Id("billing-1"), Id("capability-1"), Id("intent-1"),
            "not-required", "not-requested", Now);
        ProviderOperationContractInvariants.Validate(active);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(active with { CapabilitySnapshotId = null }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(active with { IntentId = null }));
        ProviderOperationContractInvariants.Validate(active with
        {
            LifecycleState = ProviderProfileState.Replacing,
            VerificationState = ProviderAvailabilityState.Unavailable,
        });
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(active with { LifecycleState = ProviderProfileState.Replacing }));
        ProviderOperationContractInvariants.Validate(active with
        {
            LifecycleState = ProviderProfileState.DeletePending,
            VerificationState = ProviderAvailabilityState.Unavailable,
            CleanupDisposition = "failed",
        });

        ProviderAccessProfileDocument deleted = active with
        {
            LifecycleState = ProviderProfileState.Deleted,
            VerificationState = ProviderAvailabilityState.Unavailable,
            AccountIdentityId = null,
            BillingScopeIdentityId = null,
            CapabilitySnapshotId = null,
            IntentId = null,
            CleanupDisposition = "confirmed",
        };
        ProviderOperationContractInvariants.Validate(deleted);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(deleted with { AccountIdentityId = Id("account-1") }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderOperationIsTotalOnlyForTruthfulPreProofBlockedState()
    {
        ProviderOperationDocument blocked = BlockedOperation();
        ProviderOperationContractInvariants.Validate(blocked);
        ProviderOperationContractInvariants.ValidateTransition(ProviderOperationState.Proposed, ProviderOperationState.InputBoundBlocked);
        foreach (ProviderOperationState state in Enum.GetValues<ProviderOperationState>().Where(x =>
                     x is not ProviderOperationState.Unspecified and not ProviderOperationState.InputBoundBlocked))
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProviderOperationContractInvariants.Validate(blocked with { State = state }));
        }
        foreach (ProviderOperationState from in Enum.GetValues<ProviderOperationState>().Where(x => x != ProviderOperationState.Unspecified))
        {
            foreach (ProviderOperationState to in Enum.GetValues<ProviderOperationState>().Where(x => x != ProviderOperationState.Unspecified))
            {
                if (from == ProviderOperationState.Proposed && to == ProviderOperationState.InputBoundBlocked)
                {
                    continue;
                }
                Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.ValidateTransition(from, to));
            }
        }
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(blocked with { OwnerKind = "analysis-run" }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(blocked with { Usage = BlockedUsage() with { InputTokens = Q(1) } }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderResponseIsUnavailableUntilProofQualifiedAuthorizationExists()
    {
        ProviderUsageContract completedUsage = new(Q(1), Q(2), Q(3), Q(5), Q(1), Q(0), Q(0), Q(0), Q(42),
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable);
        ProviderResponseDocument unavailable = Response();
        ProviderOperationContractInvariants.Validate(unavailable);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(unavailable with { State = ProviderResponseState.Completed, Usage = completedUsage }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(unavailable with { RequestId = Id("request-1") }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderOutputSurfacesExposeOnlyNotUsedUnavailableOrBlocked()
    {
        ProviderPublicationReferenceContract blocked = new(Id("operation-1"), ProviderOperationKind.SourceClaimExtraction,
            Id("acquisition-1"), null, null, null, null, null, null, "blocked", false);
        RunOutputV2Document output = new(ContractConstants.RunOutputV2SchemaId, "1", Id("run-1"), Ref("local-1"),
            Id("config-1"), [blocked], [Id("acquisition-1")], [], [], ["input-bound-authority-required"], false, false);
        ProviderOperationContractInvariants.Validate(output);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(output with
            {
                ProviderOperations = [blocked with { AuthorizationId = Id("authorization-1") }],
            }));
        OpaqueId authorizationId = Id("authorization-1");
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ProviderOperationContractInvariants.Validate(output with
            {
                ProviderOperations = [blocked with
                {
                    Availability = "live",
                    Live = true,
                    AuthorizationId = authorizationId,
                    LiveAuthorizationId = authorizationId,
                    AcceptedInputBoundPolicyId = "future-accepted-policy",
                    AcceptedInputBoundPolicyVersion = "future-accepted-version",
                }],
            }));

        ProviderQuantityContract absent = U();
        CliSummaryV2Document cli = new(ContractConstants.CliSummaryV2SchemaId, "1", Id("run-1"), Fingerprint,
            "blocked", absent, absent, absent, absent, absent, absent, absent, absent, false, "not-available",
            ["input-bound-authority-required"], false, false);
        ProviderOperationContractInvariants.Validate(cli);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(cli with { DispatchCount = Q(0) }));
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderOperationContractInvariants.Validate(cli with
        {
            ProviderState = "live",
            AcceptedInputBoundPolicyId = "future-accepted-policy",
            AcceptedInputBoundPolicyVersion = "future-accepted-version",
            LiveAuthorizationId = authorizationId,
        }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderApplicationConfirmationRetainsEveryExactBindingButCannotConfirm()
    {
        byte[] canonicalRequest = [1, 2, 3];
        Sha256Fingerprint requestFingerprint = new(Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(canonicalRequest)));
        SelectAndConfirmProviderOperationCommand command = new(
            Id("command-1"), Id("operation-1"), ProviderOperationKind.SourceClaimExtraction,
            "evidence-acquisition-run", Id("acquisition-1"), Id("job-1"), Id("install-1"), Id("context-1"), Id("config-1"),
            Id("manifest-1"), Id("profile-1"), Id("generation-1"), 0, Id("capability-1"), requestFingerprint,
            requestFingerprint, canonicalRequest, canonicalRequest.Length, Fingerprint, Id("price-1"), Fingerprint, Fingerprint, Id("prompt-1"),
            Fingerprint, Id("schema-1"), Fingerprint, BlockedProof(),
            new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000),
            UtcTimestamp.Parse("2026-08-10T00:02:00.0000000+00:00"), 1, Now, Now);
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderApplicationContractInvariants.Validate(command));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(command with { CanonicalRequestBytes = 65_537 }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(command with { CoordinatorFencingEpoch = 0 }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(command with { RequestFingerprint = Fingerprint }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(command with
            {
                DispatchDeadline = UtcTimestamp.Parse("2026-08-10T00:02:00.0010000+00:00"),
            }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderSemanticCandidatesRequireExactOwnersAndAdmissionEvidence()
    {
        HypothesisProposalContract admitted = new(
            Id("proposal-1"), Id("candidate-1"), "synthetic hypothesis", [Id("evidence-1")], [], [],
            ProposalAdmissionState.Admitted, "synthetic admission");
        CandidateInvestigationDocument candidate = new(
            ContractConstants.CandidateInvestigationSchemaId, "1", Id("operation-1"), "analysis-run", Id("run-1"),
            Id("run-1"), Id("candidate-1"), [Id("participant-1")], ["subject"], [Id("path-1")],
            Id("closure-1"), [Id("evidence-1")], [admitted], [], [], [Id("validation-1")], [Id("admission-1")]);
        ProviderOperationContractInvariants.Validate(candidate);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(candidate with { OwnerId = Id("run-other") }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(candidate with { ValidationIds = [] }));
        Assert.ThrowsExactly<ArgumentException>(() => Id(" "));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderPriceUsesCheckedComponentWiseUpwardRounding()
    {
        ProviderPriceRuleContract rule = Price().Rules[0];
        Assert.AreEqual(1L, ProviderOperationContractInvariants.CalculateComponentNanoUsd(1, rule with
        {
            NumeratorNanoUsd = 1,
            DenominatorTokens = 3,
        }));
        Assert.ThrowsExactly<OverflowException>(() => ProviderOperationContractInvariants.CalculateComponentNanoUsd(
            long.MaxValue, rule with { NumeratorNanoUsd = 2 }));
    }

    private static ProviderOperationDocument BlockedOperation() => new(
        ContractConstants.ProviderOperationSchemaId, "1", Id("operation-1"), Id("acquisition-1"), "evidence-acquisition-run",
        ProviderOperationKind.SourceClaimExtraction, Id("job-1"), Id("command-1"), Id("install-1"), Id("context-1"),
        Id("config-1"), Id("manifest-1"), Id("profile-1"), Id("generation-1"), 0,
        Capability(), Price(), Id("prompt-1"), Fingerprint, Id("schema-1"), Fingerprint, Fingerprint,
        Ref("request-payload-1"), 1024, Fingerprint, BlockedProof(),
        new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000),
        ProviderOperationState.InputBoundBlocked, "not-started", "not-available", BlockedUsage(), "not-started",
        "not-available", Now, Now, UtcTimestamp.Parse("2026-08-10T00:02:00.0000000+00:00"), 1, Now);

    private static ProviderResponseDocument Response() => new(
        ContractConstants.ProviderResponseSchemaId, "1", Id("response-1"), Id("operation-1"), null, null,
        null, BlockedProof(), ProviderAvailabilityState.Unavailable, null, null, 1_048_576, null, null,
        ProviderAvailabilityState.Unavailable, null, null, null, null, ProviderAvailabilityState.Unavailable,
        ProviderResponseState.Unknown, null, null, null, "gpt-5.6-sol", null, "default", null,
        "current_turn", "standard", "explicit", BlockedUsage(), [], null, ProposalAdmissionState.Unavailable,
        ProposalAdmissionState.Unavailable, Now);

    private static ProviderUsageContract BlockedUsage() => new(Q(0), U(), U(), U(), U(), U(), U(), U(), U(),
        ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable);
    private static ProviderQuantityContract Q(long value) => new(ProviderAvailabilityState.Available, value);
    private static ProviderQuantityContract U() => new(ProviderAvailabilityState.Unavailable, null);
    private static ProviderInputBoundProofContract BlockedProof() => new(
        ProviderOperationContractInvariants.LocalInputBoundPolicyId,
        ProviderOperationContractInvariants.LocalInputBoundPolicyVersion,
        ProviderInputBoundProofState.AuthorityRequired);
    private static ProviderCapabilitySnapshotContract Capability() => new(Id("capability-1"), Fingerprint, "openai",
        "gpt-5.6-sol", "default", "medium", "current_turn", "standard", false, false, false, "none", 0,
        "disabled", "explicit", false, false, 272_000, "synthetic-v1");
    private static ProviderPriceSnapshotContract Price() => new(Id("price-1"), Fingerprint, "openai", "gpt-5.6-sol",
        "default", "USD", "synthetic-v1", [new(Id("rule-1"), "openai", "gpt-5.6-sol", "default",
            "standard-under-272k", "ordinary-input", "input", "none", "global", "USD", 1, 1, "synthetic-v1")]);
    private static ProviderIdentityReferenceContract Ref(string value) => new(Id(value), Fingerprint);
    private static OpaqueId Id(string value) => new(value);
    private static readonly Sha256Fingerprint Fingerprint = new(new string('a', 64));
    private static readonly UtcTimestamp Now = UtcTimestamp.Parse("2026-08-10T00:00:00.0000000+00:00");
}
