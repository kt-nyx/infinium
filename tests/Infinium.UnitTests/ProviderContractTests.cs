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
        ProviderUsageContract completedUsage = new(ProviderAvailabilityState.Available, Q(1), Q(2), Q(3), Q(5), Q(1), Q(0), Q(0), Q(0), Q(42),
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
    public void ProofQualifiedFutureResponseStateMatrixIsTotalBeforeMaturityRejection()
    {
        ProviderResponseState[] states =
        [
            ProviderResponseState.Completed, ProviderResponseState.Refusal, ProviderResponseState.Incomplete,
            ProviderResponseState.Failed, ProviderResponseState.Queued, ProviderResponseState.InProgress,
            ProviderResponseState.Malformed, ProviderResponseState.Oversized, ProviderResponseState.Mismatched,
            ProviderResponseState.Unknown, ProviderResponseState.Cancelled,
        ];
        foreach (ProviderResponseState state in states)
        {
            ProviderResponseDocument future = FutureResponse(state);
            Assert.ThrowsExactly<NotSupportedException>(() => ProviderOperationContractInvariants.Validate(future), state.ToString());
            ProviderResponseDocument contradictory = future with
            {
                ValidationState = state == ProviderResponseState.Completed
                    ? ProposalAdmissionState.Rejected : ProposalAdmissionState.Admitted,
                AdmissionState = state == ProviderResponseState.Completed
                    ? ProposalAdmissionState.Rejected : ProposalAdmissionState.Admitted,
            };
            Assert.ThrowsExactly<InvalidOperationException>(
                () => ProviderOperationContractInvariants.Validate(contradictory), state.ToString());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderFutureResponseRejectsAvailabilityUsageAndLimitContradictionsBeforeMaturityGate()
    {
        ProviderResponseDocument completed = FutureResponse(ProviderResponseState.Completed);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            completed with { Usage = completed.Usage with { Availability = ProviderAvailabilityState.Unavailable } }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            completed with { Usage = completed.Usage with { TotalTokens = Q(49) } }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            completed with { Usage = completed.Usage with { ReasoningTokens = Q(17) } }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            completed with { MaximumRawResponseBytes = completed.Limits.MaximumRawResponseBytes - 1 }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            FutureResponse(ProviderResponseState.Refusal) with
            {
                Usage = FutureResponse(ProviderResponseState.Refusal).Usage with
                {
                    Availability = ProviderAvailabilityState.Unavailable,
                },
            }));
        ProviderResponseDocument malformed = FutureResponse(ProviderResponseState.Malformed);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            malformed with
            {
                ErrorAvailability = ProviderAvailabilityState.Available,
                ErrorCode = "unexpected-error-fact",
            }));
        ProviderResponseDocument cancelled = FutureResponse(ProviderResponseState.Cancelled);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            cancelled with { ResponseHeadersAvailability = ProviderAvailabilityState.Unsupported }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            cancelled with
            {
                ProviderResponseIdAvailability = ProviderAvailabilityState.Available,
                ProviderResponseId = "fabricated-provider-response",
            }));
        ProviderResponseDocument oversized = FutureResponse(ProviderResponseState.Oversized);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            oversized with { OverflowObservedExcessBytes = 2 }));
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            oversized with
            {
                RawResponseAvailability = ProviderAvailabilityState.Available,
                RawResponsePayload = Ref("over-limit-body"),
                RawResponseBytes = 128,
            }));
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
            Id("closure-1"), [Id("evidence-1")], [admitted], [], [], [Id("validation-1")], [Id("admission-1")],
            [new(Id("admission-1"), Id("proposal-1"), Id("authorization-1"), Id("operation-1"), Id("response-1"), "analysis-run",
                Id("run-1"), Id("candidate-1"), Id("validation-1"), Id("application-1"), ProposalAdmissionState.Admitted)]);
        ProviderOperationContractInvariants.Validate(candidate);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            candidate with { AdmissionLinkIds = [Id("application-1")] }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(candidate with { OwnerId = Id("run-other") }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(candidate with { ValidationIds = [] }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(candidate with
            {
                HypothesisProposals = [admitted with { State = ProposalAdmissionState.Rejected }],
            }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(candidate with
            {
                AdmissionLinks = [candidate.AdmissionLinks[0] with { State = ProposalAdmissionState.Rejected }],
            }));
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
        SchemaId: ContractConstants.ProviderResponseSchemaId, SchemaVersion: "1", ResponseRecordId: Id("response-1"),
        OperationId: Id("operation-1"), OwnerKind: "evidence-acquisition-run", OwnerId: Id("acquisition-1"),
        AuthorizationId: null, RequestId: null, DispatchFenceId: null,
        OperationKind: ProviderOperationKind.SourceClaimExtraction,
        Limits: new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000), InputBoundProof: BlockedProof(),
        Availability: ProviderAvailabilityState.Unavailable, RawResponseAvailability: ProviderAvailabilityState.Unavailable,
        RawResponsePayload: null, RawResponseBytes: null, MaximumRawResponseBytes: 1_048_576,
        OverflowObservedExcessBytes: null,
        ResponseHeadersPayload: null, ResponseHeadersBytes: null, ResponseHeadersAvailability: ProviderAvailabilityState.Unavailable,
        HttpStatus: null, HttpStatusAvailability: ProviderAvailabilityState.Unavailable,
        ProviderResponseId: null, ProviderResponseIdAvailability: ProviderAvailabilityState.Unavailable,
        ClientRequestId: null, ClientRequestIdAvailability: ProviderAvailabilityState.Unavailable,
        ProviderRequestId: null, ProviderRequestIdAvailability: ProviderAvailabilityState.Unavailable,
        State: ProviderResponseState.Unknown, RefusalCode: null, RefusalAvailability: ProviderAvailabilityState.Unavailable,
        IncompleteReason: null, IncompleteAvailability: ProviderAvailabilityState.Unavailable,
        ErrorCode: null, ErrorAvailability: ProviderAvailabilityState.Unavailable,
        RequestedModel: "gpt-5.6-sol", ReturnedModel: null, ReturnedModelAvailability: ProviderAvailabilityState.Unavailable,
        RequestedServiceTier: "default", ReturnedServiceTier: null, ReturnedServiceTierAvailability: ProviderAvailabilityState.Unavailable,
        ReasoningContext: "current_turn", ReasoningMode: "standard", PromptCacheMode: "explicit", Usage: BlockedUsage(),
        RateLimitFacts: [], BillingEvidencePayload: null, BillingEvidenceAvailability: ProviderAvailabilityState.Unavailable,
        ValidationState: ProposalAdmissionState.Unavailable, AdmissionState: ProposalAdmissionState.Unavailable, RecordedAt: Now);

    private static ProviderResponseDocument FutureResponse(ProviderResponseState state)
    {
        bool completed = state == ProviderResponseState.Completed;
        bool oversized = state == ProviderResponseState.Oversized;
        bool raw = state != ProviderResponseState.Cancelled && !oversized;
        bool http = state != ProviderResponseState.Cancelled;
        bool refusal = state == ProviderResponseState.Refusal;
        bool incomplete = state == ProviderResponseState.Incomplete;
        bool error = state == ProviderResponseState.Failed;
        bool mismatched = state == ProviderResponseState.Mismatched;
        bool cancelled = state == ProviderResponseState.Cancelled;
        return Response() with
        {
            AuthorizationId = cancelled ? null : Id("authorization-1"),
            RequestId = cancelled ? null : Id("request-1"),
            DispatchFenceId = cancelled ? null : Id("fence-1"),
            InputBoundProof = new ProviderInputBoundProofContract("accepted-policy", "1", ProviderInputBoundProofState.Proved),
            Availability = cancelled ? ProviderAvailabilityState.Unavailable : ProviderAvailabilityState.Available,
            State = state,
            RawResponseAvailability = raw ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            RawResponsePayload = raw ? Ref("raw-response-1") : null,
            RawResponseBytes = raw ? 128 : null,
            OverflowObservedExcessBytes = oversized ? 1 : null,
            HttpStatusAvailability = http ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            HttpStatus = http ? 200 : null,
            RefusalAvailability = refusal ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            RefusalCode = refusal ? "policy-refusal" : null,
            IncompleteAvailability = incomplete ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            IncompleteReason = incomplete ? "maximum-output" : null,
            ErrorAvailability = error ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            ErrorCode = error ? "provider-error" : null,
            ReturnedModelAvailability = completed || mismatched
                ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            ReturnedModel = completed ? "gpt-5.6-sol" : mismatched ? "mismatched-model" : null,
            ReturnedServiceTierAvailability = completed || mismatched
                ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
            ReturnedServiceTier = completed || mismatched ? "default" : null,
            Usage = completed
                ? new ProviderUsageContract(ProviderAvailabilityState.Available, Q(1), Q(2), Q(3), Q(5), Q(1), Q(0), Q(0), Q(0), Q(42),
                    ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable)
                : cancelled ? BlockedUsage()
                : new ProviderUsageContract(ProviderAvailabilityState.Available, Q(1), U(), U(), U(), U(), U(), U(), U(), U(),
                    ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable),
            ValidationState = completed ? ProposalAdmissionState.Admitted : ProposalAdmissionState.Rejected,
            AdmissionState = completed ? ProposalAdmissionState.Admitted : ProposalAdmissionState.Rejected,
        };
    }

    private static ProviderUsageContract BlockedUsage() => new(ProviderAvailabilityState.Unavailable, Q(0), U(), U(), U(), U(), U(), U(), U(), U(),
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
