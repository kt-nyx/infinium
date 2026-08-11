using System.Security.Cryptography;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderContractTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderFiniteLimitsBindAllSevenDimensionsPerOperationKindAndUnprovedInputAdmissionFailsClosed()
    {
        ProviderFiniteLimitsContract qualification = new(
            16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
        ProviderFiniteLimitsContract semantic = new(
            65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);

        ProviderOperationContractInvariants.Validate(ProviderOperationKind.TransportQualification, qualification);
        ProviderOperationContractInvariants.Validate(ProviderOperationKind.SourceClaimExtraction, semantic);
        ProviderOperationContractInvariants.Validate(ProviderOperationKind.CandidateInvestigation, semantic);
        foreach (ProviderFiniteLimitsContract invalid in new[]
        {
            qualification with { MaximumRequestBytes = 0 },
            qualification with { MaximumRequestBytes = 16_385 },
            qualification with { MaximumInputTokens = 0 },
            qualification with { MaximumInputTokens = 20_481 },
            qualification with { MaximumOutputTokens = 0 },
            qualification with { MaximumOutputTokens = 257 },
            qualification with { MaximumRawResponseBytes = 0 },
            qualification with { MaximumRawResponseBytes = 262_145 },
            qualification with { MaximumDispatchCount = 0 },
            qualification with { MaximumDispatchCount = 2 },
            qualification with { MaximumCalculatedNanoUsd = 0 },
            qualification with { MaximumCalculatedNanoUsd = 140_000_001 },
            qualification with { DeadlineMilliseconds = 0 },
            qualification with { DeadlineMilliseconds = 60_001 },
        })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProviderOperationContractInvariants.Validate(ProviderOperationKind.TransportQualification, invalid));
        }
        foreach (ProviderFiniteLimitsContract invalid in new[]
        {
            semantic with { MaximumRequestBytes = 65_537 },
            semantic with { MaximumInputTokens = 73_729 },
            semantic with { MaximumOutputTokens = 4_097 },
            semantic with { MaximumRawResponseBytes = 1_048_577 },
            semantic with { MaximumDispatchCount = 2 },
            semantic with { MaximumCalculatedNanoUsd = 600_000_001 },
            semantic with { DeadlineMilliseconds = 120_001 },
        })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProviderOperationContractInvariants.Validate(ProviderOperationKind.SourceClaimExtraction, invalid));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ProviderOperationContractInvariants.Validate(ProviderOperationKind.CandidateInvestigation, invalid));
        }
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderOperationContractInvariants.RequireLocalInputBoundProof(1));
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderOperationContractInvariants.RequireLocalInputBoundProof(65_536));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ProviderOperationContractInvariants.RequireLocalInputBoundProof(65_537));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderContractPriceRuleUsesCheckedComponentWiseUpwardRounding()
    {
        ProviderPriceRuleContract rule = new(
            Id("price-rule-1"), "openai", "gpt-5.6-sol", "default",
            "standard-under-272k", "ordinary-input", "input", "none",
            "global", "USD", 3, 2, "2026-08-10");

        Assert.AreEqual(2L, ProviderOperationContractInvariants.CalculateComponentNanoUsd(1, rule));
        Assert.AreEqual(3L, ProviderOperationContractInvariants.CalculateComponentNanoUsd(2, rule));
        Assert.ThrowsExactly<OverflowException>(() =>
            ProviderOperationContractInvariants.CalculateComponentNanoUsd(long.MaxValue, rule));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.CalculateComponentNanoUsd(1, rule with { Region = "implicit" }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderContractApplicationQueriesAreFiniteAndReplayIsOffline()
    {
        ProviderApplicationContractInvariants.Validate(new ProviderBudgetQuery(Id("scope-1"), "global", 100));
        ProviderApplicationContractInvariants.Validate(new ProviderReplayQuery(Id("op-1"), Id("response-1"), false));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(new ProviderBudgetQuery(Id("scope-1"), "global", 101)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(new ProviderReplayQuery(Id("op-1"), Id("response-1"), true)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderContractFactoriesBindFrozenV1BytesWithoutReinterpretingThem()
    {
        byte[] localConfigurationV1 = [1, 2, 3];
        byte[] localRunOutputV1 = [4, 5, 6];
        byte[] localCliSummaryV1 = [7, 8, 9];
        ProviderFiniteLimitsContract limits = new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);
        EffectiveScanConfigurationV2Document configuration = ProviderContractFactories.CreateEffectiveConfigurationV2(
            Id("config-2"), Id("config-1"), localConfigurationV1, Id("profile-1"), Id("generation-1"), limits);
        RunOutputV2Document output = ProviderContractFactories.CreateRunOutputV2Supplement(
            Id("run-1"), Id("run-output-1"), localRunOutputV1, configuration.ConfigurationId, [], [], [], [], []);
        ProviderOperationSummaryProjection projection = new(
            Id("operation-1"), ProviderOperationState.Settled, "openai", "gpt-5.6-sol", 100, 42, false,
            "retained-response", []);
        CliSummaryV2Document summary = ProviderContractFactories.CreateCliSummaryV2Supplement(
            Id("run-1"), localCliSummaryV1, projection, 1, 32, 16, 4, []);
        CliSummaryV2Document notUsed = ProviderContractFactories.CreateProviderNotUsedCliSummaryV2Supplement(
            Id("run-1"), localCliSummaryV1, unavailable: false, []);

        Assert.AreEqual(Hash(localConfigurationV1), configuration.LocalConfigurationV1Fingerprint.Value);
        Assert.AreEqual(Hash(localRunOutputV1), output.LocalRunOutputV1.Fingerprint.Value);
        Assert.AreEqual(Hash(localCliSummaryV1), summary.LocalCliSummaryV1Fingerprint.Value);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, localConfigurationV1);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, localRunOutputV1);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, localCliSummaryV1);
        Assert.AreEqual("not-used", notUsed.ProviderState);
        Assert.IsNull(notUsed.DispatchCount.Value);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderOperationStateTotalityCoversEveryStateAndEveryTransitionPair()
    {
        ProviderOperationState[] states = [ProviderOperationState.Proposed, ProviderOperationState.Confirmed,
            ProviderOperationState.Reserved, ProviderOperationState.Assigned, ProviderOperationState.InputBoundBlocked];
        foreach (ProviderOperationState state in states)
        {
            ProviderOperationContractInvariants.Validate(Operation(state));
        }

        HashSet<(ProviderOperationState, ProviderOperationState)> legal =
        [
            (ProviderOperationState.Proposed, ProviderOperationState.Confirmed),
            (ProviderOperationState.Confirmed, ProviderOperationState.Reserved),
            (ProviderOperationState.Reserved, ProviderOperationState.Assigned),
            (ProviderOperationState.Assigned, ProviderOperationState.InputBoundBlocked),
        ];
        foreach (ProviderOperationState from in states)
        {
            foreach (ProviderOperationState to in states)
            {
                if (legal.Contains((from, to)))
                {
                    ProviderOperationContractInvariants.ValidateTransition(from, to);
                }
                else
                {
                    Assert.ThrowsExactly<InvalidOperationException>(() =>
                        ProviderOperationContractInvariants.ValidateTransition(from, to), $"{from}->{to}");
                }
            }
        }

        ProviderOperationDocument proposed = Operation(ProviderOperationState.Proposed);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            proposed with { TransportState = "completed", ReceiptState = "validated", SettlementState = "settled", ReplayState = "retained-response" }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderOperationStateShapeMatrixExhaustivelyRejectsIllegalIdentitiesAndProjections()
    {
        ProviderOperationState[] states = Enum.GetValues<ProviderOperationState>()
            .Where(x => x != ProviderOperationState.Unspecified).ToArray();
        string[] transports = ["not-started", "may-have-started", "started", "completed", "failed-known", "ambiguous"];
        string[] receipts = ["not-available", "staged", "validated", "rejected", "unresolved"];
        string[] settlements = ["not-started", "settled", "unresolved-hold", "failed-known", "overrun"];
        string[] replays = ["not-available", "retained-response", "audit-only"];

        foreach (ProviderOperationState state in states)
        {
            ProviderOperationDocument valid = Operation(state);
            ProviderOperationDocument[] identityContradictions =
            [
                valid with { AttemptId = Toggle(valid.AttemptId, "attempt-contradiction") },
                valid with { RequestId = Toggle(valid.RequestId, "request-contradiction") },
                valid with { SettingsFingerprint = Toggle(valid.SettingsFingerprint, 'd') },
                valid with { OutputSchemaFingerprint = Toggle(valid.OutputSchemaFingerprint, 'e') },
                valid with { RequestFingerprint = Toggle(valid.RequestFingerprint, 'f') },
                valid with { AuthorizationId = Toggle(valid.AuthorizationId, "authorization-contradiction") },
                valid with { ReservationId = Toggle(valid.ReservationId, "reservation-contradiction") },
                valid with { DispatchFenceId = Toggle(valid.DispatchFenceId, "fence-contradiction") },
            ];
            foreach (ProviderOperationDocument contradiction in identityContradictions)
            {
                Assert.ThrowsExactly<InvalidOperationException>(() =>
                    ProviderOperationContractInvariants.Validate(contradiction), state.ToString());
            }

            foreach (string transport in transports)
            {
                foreach (string receipt in receipts)
                {
                    foreach (string settlement in settlements)
                    {
                        foreach (string replay in replays)
                        {
                            ProviderOperationDocument candidate = valid with
                            {
                                TransportState = transport,
                                ReceiptState = receipt,
                                SettlementState = settlement,
                                ReplayState = replay,
                            };
                            bool expected = LegalProjection(state, transport, receipt, settlement, replay);
                            try
                            {
                                ProviderOperationContractInvariants.ValidateOperationStateShape(candidate);
                                Assert.IsTrue(expected, $"Unexpected legal projection: {state}/{transport}/{receipt}/{settlement}/{replay}");
                            }
                            catch (InvalidOperationException)
                            {
                                Assert.IsFalse(expected, $"Unexpected rejected projection: {state}/{transport}/{receipt}/{settlement}/{replay}");
                            }
                        }
                    }
                }
            }
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            Operation(ProviderOperationState.Proposed) with { State = (ProviderOperationState)999 }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderSemanticAndOutputCrossStateSubstitutionsFailClosed()
    {
        SourceClaimExtractionDocument source = new(
            ContractConstants.SourceClaimExtractionSchemaId, "1", Id("acq"), Id("op"), Id("source"), [Id("passage")],
            "synthetic", [new(Id("proposal"), Id("foreign"), "claim", [], ProposalAdmissionState.Proposed, "reason")],
            [], [], [], [], []);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(source));

        CandidateInvestigationDocument candidate = new(
            ContractConstants.CandidateInvestigationSchemaId, "1", Id("op"), Id("candidate"), [Id("participant")], ["role"], [],
            Id("closure"), [Id("evidence")], [new(Id("proposal"), Id("foreign"), "hypothesis", [Id("evidence")],
                [Id("evidence")], [], ProposalAdmissionState.Proposed, "reason")], [], [], [], []);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(candidate));

        ProviderQuantityContract absent = new(ProviderAvailabilityState.NotUsed, null);
        CliSummaryV2Document cli = new(ContractConstants.CliSummaryV2SchemaId, "1", Id("run"),
            new Sha256Fingerprint(new string('a', 64)), "not-used", absent, absent, absent, absent, absent, absent,
            absent, absent, false, "not-available", [], false, false);
        ProviderOperationContractInvariants.Validate(cli);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
            cli with { DispatchCount = new(ProviderAvailabilityState.Available, 1) }));
        ProviderPublicationReferenceContract invalid = new(null, null, null, null, null, null, null, null, null, "not-used", true);
        RunOutputV2Document output = new(ContractConstants.RunOutputV2SchemaId, "1", Id("run"),
            new(Id("v1"), new(new string('a', 64))), Id("config"), [invalid], [], [], [], [], false, false);
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(output));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderProfileLifecycleMatrixRequiresOnlyTruthfullyReachableIdentities()
    {
        foreach (ProviderProfileState state in Enum.GetValues<ProviderProfileState>().Where(x => x != ProviderProfileState.Unspecified))
        {
            bool active = state is ProviderProfileState.ActiveUnverified or ProviderProfileState.ActiveVerified or ProviderProfileState.Replacing;
            ProviderAccessProfileDocument profile = new(
                ContractConstants.ProviderAccessProfileSchemaId, "1", Id("profile"), Id("generation"), 1, 0,
                "openai", "responses", "Synthetic", state,
                state == ProviderProfileState.PendingEnrollment ? ProviderAvailabilityState.NotApplicable
                    : state is ProviderProfileState.ActiveVerified or ProviderProfileState.Replacing
                        ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable,
                active ? Id("account") : null, active ? Id("billing") : null, active ? Id("capability") : null,
                state == ProviderProfileState.Deleted ? null : Id("intent"),
                state == ProviderProfileState.RecoveryRequired ? "required"
                    : state == ProviderProfileState.SecureStoreUnavailable ? "unavailable" : "not-required",
                state == ProviderProfileState.DeletePending ? "pending"
                    : state == ProviderProfileState.Deleted ? "confirmed" : "not-requested",
                UtcTimestamp.Parse("2026-08-10T00:00:00.0000000+00:00"));
            ProviderOperationContractInvariants.Validate(profile);
            Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
                profile with { VerificationState = ProviderAvailabilityState.Unsupported }), state.ToString());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderResponseMatrixRetainsBoundedEvidenceAndSemanticDispositionForEveryState()
    {
        ProviderUsageContract usage = new(Available(1), Available(1), Available(0), Available(0), Available(0),
            Available(0), Available(0), Available(1), ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable);
        foreach (ProviderResponseState state in Enum.GetValues<ProviderResponseState>().Where(x => x != ProviderResponseState.Unspecified))
        {
            bool cancelled = state == ProviderResponseState.Cancelled;
            bool completed = state == ProviderResponseState.Completed;
            ProviderResponseDocument response = new(
                ContractConstants.ProviderResponseSchemaId, "1", Id("response"), Id("operation"), Id("request"),
                cancelled ? null : new(Id("payload"), new(new string('a', 64))), cancelled ? null : 10,
                cancelled ? null : 200, completed ? "provider-response" : null, state,
                state == ProviderResponseState.Refusal ? "refusal" : null,
                state == ProviderResponseState.Incomplete ? "incomplete" : null,
                !cancelled && state is not (ProviderResponseState.Completed or ProviderResponseState.Refusal or ProviderResponseState.Incomplete) ? "error" : null,
                "gpt-5.6-sol", completed ? "gpt-5.6-sol" : null, "default", completed ? "default" : null,
                "current_turn", "standard", "explicit", usage,
                completed ? ProposalAdmissionState.Admitted : cancelled ? ProposalAdmissionState.Unavailable : ProposalAdmissionState.Rejected,
                completed ? ProposalAdmissionState.Admitted : cancelled ? ProposalAdmissionState.Unavailable : ProposalAdmissionState.Rejected,
                UtcTimestamp.Parse("2026-08-10T00:00:00.0000000+00:00"));
            ProviderOperationContractInvariants.Validate(response);
            Assert.ThrowsExactly<InvalidOperationException>(() => ProviderOperationContractInvariants.Validate(
                response with { AdmissionState = ProposalAdmissionState.Deleted }), state.ToString());
        }
    }

    private static ProviderOperationDocument Operation(ProviderOperationState state)
    {
        int stage = state switch
        {
            ProviderOperationState.Proposed => 0,
            ProviderOperationState.Confirmed => 1,
            ProviderOperationState.Reserved => 2,
            ProviderOperationState.Assigned => 3,
            ProviderOperationState.InputBoundBlocked => 3,
            ProviderOperationState.FinalGateAuthorized or ProviderOperationState.TransportNotStarted => 4,
            _ => 5,
        };
        string transport = state == ProviderOperationState.Settled ? "completed"
            : state is ProviderOperationState.TransportMayHaveStarted or ProviderOperationState.UnresolvedHold ? "may-have-started"
            : state is ProviderOperationState.ResponseStaged or ProviderOperationState.Admitted or ProviderOperationState.Rejected ? "completed"
            : "not-started";
        string receipt = state == ProviderOperationState.Settled ? "validated"
            : state == ProviderOperationState.ResponseStaged ? "staged"
            : state == ProviderOperationState.Admitted ? "validated"
            : state == ProviderOperationState.Rejected ? "rejected"
            : state == ProviderOperationState.UnresolvedHold ? "unresolved" : "not-available";
        string settlement = state == ProviderOperationState.Settled ? "settled"
            : state == ProviderOperationState.UnresolvedHold ? "unresolved-hold" : "not-started";
        ProviderUsageContract usage = new(
            Available(stage >= 5 ? 1 : 0), Available(stage >= 5 ? 1 : 0), Available(0), Available(0),
            Available(0), Available(0), Available(0), Available(0), ProviderAvailabilityState.Available,
            ProviderAvailabilityState.Available, ProviderAvailabilityState.Unavailable);
        return new(ContractConstants.ProviderOperationSchemaId, "1", Id("operation"), Id("run"), "analysis-run",
            ProviderOperationKind.SourceClaimExtraction, Id("job"), stage >= 3 ? Id("attempt") : null,
            stage >= 1 ? Id("request") : null, Id("profile"), Id("generation"), 0, Capability(), Price(),
            stage >= 1 ? new(new string('a', 64)) : null, stage >= 1 ? new(new string('b', 64)) : null,
            stage >= 1 ? new(new string('c', 64)) : null,
            new(ProviderOperationContractInvariants.LocalInputBoundPolicyId,
                ProviderOperationContractInvariants.LocalInputBoundPolicyVersion,
                ProviderInputBoundProofState.AuthorityRequired, null, null),
            new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000),
            stage >= 1 ? Id("authorization") : null, stage >= 2 ? Id("reservation") : null,
            stage >= 4 ? Id("fence") : null, state, transport, receipt, usage, settlement,
            state is ProviderOperationState.Settled or ProviderOperationState.Admitted ? "retained-response"
                : state == ProviderOperationState.Rejected ? "audit-only" : "not-available",
            UtcTimestamp.Parse("2026-08-10T00:00:00.0000000+00:00"));
    }

    private static ProviderQuantityContract Available(long value) => new(ProviderAvailabilityState.Available, value);
    private static OpaqueId? Toggle(OpaqueId? value, string replacement) => value is null ? Id(replacement) : null;
    private static Sha256Fingerprint? Toggle(Sha256Fingerprint? value, char replacement) =>
        value is null ? new(new string(replacement, 64)) : null;

    private static bool LegalProjection(
        ProviderOperationState state,
        string transport,
        string receipt,
        string settlement,
        string replay) => state switch
        {
            ProviderOperationState.Proposed or ProviderOperationState.Confirmed or ProviderOperationState.Reserved
                or ProviderOperationState.Assigned or ProviderOperationState.InputBoundBlocked or ProviderOperationState.FinalGateAuthorized
                or ProviderOperationState.TransportNotStarted => transport == "not-started"
                    && receipt == "not-available" && settlement == "not-started" && replay == "not-available",
            ProviderOperationState.TransportMayHaveStarted => transport == "may-have-started"
                && receipt is "not-available" or "unresolved" && settlement == "not-started" && replay == "not-available",
            ProviderOperationState.ResponseStaged => transport == "completed" && receipt == "staged"
                && settlement == "not-started" && replay == "not-available",
            ProviderOperationState.Admitted => transport == "completed" && receipt == "validated"
                && settlement == "not-started" && replay == "retained-response",
            ProviderOperationState.Rejected => transport is "completed" or "failed-known" && receipt == "rejected"
                && settlement == "not-started" && replay is "retained-response" or "audit-only",
            ProviderOperationState.Settled => transport is "completed" or "failed-known" && receipt is "validated" or "rejected"
                && settlement == "settled" && replay is "retained-response" or "audit-only",
            ProviderOperationState.UnresolvedHold => transport is "may-have-started" or "ambiguous" && receipt == "unresolved"
                && settlement == "unresolved-hold" && replay is "not-available" or "audit-only",
            _ => false,
        };
    private static ProviderCapabilitySnapshotContract Capability() => new(Id("capability"), new(new string('a', 64)),
        "openai", "gpt-5.6-sol", "default", "medium", "current_turn", "standard", false, false, false,
        "none", 0, "disabled", "explicit", false, false, 272_000, "synthetic-v1");
    private static ProviderPriceSnapshotContract Price() => new(Id("price"), new(new string('a', 64)), "openai",
        "gpt-5.6-sol", "default", "USD", "synthetic-v1", [new(Id("rule"), "openai", "gpt-5.6-sol",
            "default", "standard-under-272k", "ordinary-input", "input", "none", "global", "USD", 1, 1, "synthetic-v1")]);

    private static OpaqueId Id(string value) => new(value);
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
}
