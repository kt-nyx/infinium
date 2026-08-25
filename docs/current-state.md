# Current project state

Status: Accepted
Disposition: M1 Slice 7 remains owner-accepted and frozen; Slice 8 WP1 through WP7 are implementation-complete on exact clean product candidate `c79661cd8eb016e483fa8b7396e7d4997b85d590`, with one documentation-only handoff pending final owner accept/reject/amend decision

Last reviewed: 2026-08-24
Owner: Project owner

This is the only document that states the live milestone, slice, work package,
and evaluator handoff. Product documents and accepted ADRs define product
meaning; accepted plans define scope; implementation records preserve detailed
chronology and evidence.

## Active handoff

| Field | Current value |
|---|---|
| Milestone | `M1` - active |
| Active slice | `M1/S8` is review-ready. WP1 through WP7 are complete; implementation acceptance remains a distinct owner decision. |
| Accepted implementation baseline | R1 candidate `fcd17cd6db98019fc9e5253d5167d2487862671c`; R2 implementation `67ca34d6de162ad64f05fbe88972105745d3e831`; R2 handoff `8c25ca7274c394e41953a0b076010c26f6ffa97e` |
| Accepted R2 evidence | Final `CampaignV2NonLive` receipt SHA-256 `bb9455c293b3049c1561cf9a15322c0b6b79502365097b51e2c92e4014e5019d`; all credential, native, network, provider, and billable counts were zero |
| Accepted process proposal | Commit `2c82365fd853cb2021f1772d6c572ee9fa006d01`; process-amendment SHA-256 `5d9aff4226f93ff73025573e056080530d23f66e5cb9cc92efddfd78655acc9f`; lean-plan SHA-256 `57d9a3b25201bf55281cad02c9b8a3e458639ec10d1e465cdbad85f532c464af` |
| Accepted C1 outcome | C1 effect-free readiness closure is accepted. The owner also accepted the recovery-package amendment keeping C1 implementation-active through C2, with final C1 binding required before C3. Every correction remains offline until exact review and fresh typed authority; no effect gate is weakened. |
| C1.1 implementation | Commit `9aea07380a3d3cc2a6f70be6d32907a96e7720da` added fresh v3 coordinator consumers and schemas while retaining effect-free v2 rehearsal compatibility, but the compiled credential helper still hard-coded the obsolete v2 production-enrollment contract. The terminal C2A attempt exposed that incomplete vertical closure before UI or native access. |
| Terminal C2A evidence | Under owner-accepted package commit `926d6a49a37b6c465cb706cdebbfbf8b98b32c61`, exactly one helper process launched and rejected the v3 manifest at `manifest-validation`. `ManualUiAttempted=false`; `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFree`, DNS, network, provider, and billable counts were all zero. Ledger event 5 terminalized the campaign with final event hash `bb23edb886b68a90b2bb74f74e9b77cb021232e2d5b2633b33f62c82e3a576d8`; retry and reuse are prohibited. |
| C1.2 implementation | Commit `356ce66c18bb67d5b1de8815970e04bf88195a86` closes the v4 helper/coordinator authority seam, rejects every dormant v2 and terminal v3 identity before UI/native execution, preserves known-zero pre-UI facts, passes the exact effect-free readiness floor, and binds a reproducible 126-file Release closure. |
| Terminal replacement C2A evidence | Under activation commit `bce84c12f7550ec679f82b2a970c5f13dcbb39b0`, the owner submitted the masked dialog. Sanitized success evidence SHA-256 `0fe89804afc3aaaa04d59961e711099adbe656466fd033e54c55ad709cb3042a` proves exact `CredReadW(ERROR_NOT_FOUND) -> CredWriteW(success) -> CredReadW(success) -> CredFree(released)`, active/verified durable state, cleared UI/buffer, zero canary matches, and zero DNS/network/provider/billable operations. A coordinator-only scenario-name mismatch then produced conservative failure SHA-256 `1c83f83842a7a67a22aa658fb61140cf93eb01b23a8b8064167a3e79319c16cb` and terminal ledger event hash `a1369f547801fa282334585a17f31ebf52f7028ad836b3026738f340ce50b2f9`. No retry or credential-store inspection is permitted. |
| Corrected recovery implementation | Commit `c8cc455c8320f50bc87a160e3523f34eceb2ad13` aligns the canonical scenario, adds a closed effect-free recovery authority and append-only transition, independently validates the retained success semantics and read-only durable product projection, and permits no helper/native/network/provider effect. Its complete readiness receipt SHA-256 is `c2524db81fe448e75478bb108bf53231a1f086f299ba9676580e5e47094b1646`; independent affected-surface review passed. |
| Accepted C2A recovery authority | The owner accepted commit `dde661235533eb1b90a7330aed44e53e66b906d8`, package SHA-256 `1a53d48959ba03c26d99068263c274062b4f17ac9902effb4b15afcb9dd1b345`, and the included process amendment. Decision `infinium.m1-s6.c2a.recovery-owner-acceptance/805329e9-65e0-4e9d-9b38-84df7fc90b44` authorizes only runtime-byte derivation/review and one zero-effect ledger append. |
| Accepted C2A recovery evidence | Runtime SHA-256 `7003d3dcc061c94a7c8b3bd398ad67b2313ed93bf62a918e1bc40ff7abd38b2f` appended exactly one event. Post-ledger SHA-256 is `add1f5f7f3e5b8c010a988de2130647172dd3efdd1cd8ad9b8c67dbeae20e0ff`; event hash is `a56e6accea6bb34fd983492791dc3b02cd1df4f05c1d128edec7782898433e1a`. Independent review recomputed the event hash and proved the prior ledger prefix and every retained evidence/product identity unchanged, with all recovery effects except the append equal to zero. |
| Terminal C2B evidence | The accepted v4 campaign crossed one WP9 possible-start latch, recorded one provider start and one DNS resolution, then stopped before trustworthy response/usage settlement. The retained evidence does not establish an HTTP status, provider rejection, or whether response bytes existed. Its final event hash is `282c97151dbdcd354288b67f96c4b01d7f7ef43b1bbfb9f247cbd9b510506de9`; its USD 0.14 reservation is conservatively consumed. The v4 campaign, ledger, stage, attempt, and runtime identities are terminal and may not be retried, erased, reinterpreted, or reused. |
| Completed credential work | Generation 3 ordinal 3 is `active-verified`/`available`. Sanitized enrollment evidence SHA-256 `4016db9308160991b43a49beb3682abed332df0039dcf9bde3916d2469533ccf` proves exact 164-character protected submission and read-back equality, cleared transient buffers, and zero provider/network effect without retaining the credential or a credential-derived digest. |
| Completed live stages | WP9 ordinal 11, WP10 ordinal 2, and WP11 ordinal 1 are the permanent first-structurally-valid results. WP10 effect-free recovery admitted three host-valid source claims from the complete nine-proposal matrix. WP11 effect-free recovery retained both provider proposals, admitted the supported candidate, and retained the explicit unsupported negative. No semantic answer was selected or replaced. |
| Completed C3 | Composed evidence SHA-256 `901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d` binds the frozen sequence-8 predecessor, all inherited evidence, every ledger-v4 attempt handoff, the three first-valid stage results, and exact SQLite provenance. Ledger v4 is completed at sequence 72/event `bb2094d71515b0f16edc45a5411d8689b743ff9fe5bb811beef3511c76340445`, whole-file SHA-256 `4cc47bba72ee4c6881cbe77834ac5ab79bd0e0f487145fe0942738d34c507a17`. |
| Accepted Slice 6 implementation | The owner accepts exact product candidate `a17ff8f05ca916b4a6db2b4b3e78ba99e1313442` and handoff commit `1479da5511603596860117e732affadddcecfe5a`. The product candidate descends from governance/history commit `ba9de748d3e0c2fb9b406db5c81cfbd5dc013a9f` and product commit `1f8c4706585a201263e667f40cfe7b0be830d617`, plus focused lock, persistence, concurrency, formatting, source-transparency, and WP8 historical-binding corrections. It matches the exact 193-path authority. Fresh product, governance, and safety reviews report no remaining must-fix, and the complete accepted floor passed on the exact clean committed product bytes. All Slice-6-owned identities covered by the nine verified contract families are now `Slice-frozen`. |
| Slice 7 implementation authority | The owner activates the complete accepted Slice 7 plan on exact implementation base `29c421d38336295e5638be0e78728d98e5c11919`. One orchestrator may implement WP1 through WP7 in predecessor order and automatically continue when each internal exit gate is met. Ordinary implementation defects, failed tests, review findings, schema/codec mismatch, stale documentation, and correctable fixtures return to same-candidate correction and re-review; they do not require owner intervention. Owner escalation is limited to the accepted plan's genuine authority, scope-expansion, owner-controlled dependency, safety, private-material, destructive, or external-effect boundaries. |
| Accepted Slice 7 implementation | The owner accepts product commit `8209e93901cbc7865adad390ca913b62fe7a1650` and documentation-only handoff `88c78c135fc68a4f100b67a881ca631622b7f53b`. The complete floor passed on the exact product bytes. Combined receipt `all.json` is 764 bytes with SHA-256 `6cd835446bd34ec0bd4496a421d351fbba55fe667ea8097cd18b737789771c56`; all provider, network, credential, billable, private-fixture, archive, semantic-oracle, push, and publication effects were zero. Slice 7-owned contracts are now `Slice-frozen`. |
| Accepted Slice 8 plan | The owner accepts exact plan candidate `ab3f7ed2cf0d44067c96a7d88a44be4074486412`, [M1 Slice 8: Controlled-real generalization](plans/milestones/m1/slices/s8/README.md). It preserves every frozen Slice 7 contract and uses an additive v2 only for the richer cohort/taxonomy/provenance shape. |
| Slice 8 implementation authority | The owner activates the complete accepted WP1-through-WP7 plan on exact implementation base `ab3f7ed2cf0d44067c96a7d88a44be4074486412`. One orchestrator may implement, test, review, correct, re-review, run the final accepted floor, and prepare the owner-acceptance handoff without further approval for ordinary in-scope corrections. Read-only use of the untracked answer-free local handoff `m1-slice8-research0035-local-v1`, manifest SHA-256 `8972ef0e160b9de04da281d48639b66d8bffcc153504c1d699f654f1eff6ecf5`, is owner-authorized after the plan's containment and identity gate accepted all 26 allowlisted inputs; its absolute root remains untracked. No additional owner decision is needed for this conforming handoff. Private fixtures, evaluator-private repositories, legacy/evaluator archives, semantic oracles, credentials, providers, network, external effects, publication, push, and Slice 9 remain unauthorized. |
| Review-ready Slice 8 implementation | Product commit `c79661cd8eb016e483fa8b7396e7d4997b85d590`, tree `fd706b21b51e4009cf02e338ef52fbc2fe3eb937`, is clean and complete. The exact Section 12 floor passed: Unit 300/4 skipped, Contract 181/0, Integration 193/1, Evaluation 91/8, Security 180/6, Fault 118/3, plus the mandatory Slice 8 harness 37/37 with zero skips and `Gate All`. Harness receipt SHA-256 is `571507a1622a4bd598573466da79c40782ace16ac0a9b30707f65e841e72700f`; pipeline `all.json` SHA-256 is `fa877dd90dc4aab7ee32aa922a93bb1918c6f9fc9ccbde6f77484d7ba11f0f1b`. The v2 product/storage contracts are `Producer-consumer-validated`, not frozen. No private/independent semantic verdict exists. |
| Resolved post-closeout finding | The live WP10 and WP11 requests had recorded detailed `PromptV1` identities while transmitting a shorter instruction. The effect-free product candidate now rejects that mismatch and separates source support, local applicability, and host admission. Historical semantic packages remain immutable non-authorizing evidence and were not used to choose or tune the corrected behavior. |
| Effect-free semantic correction | The working candidate based on `dde21f4f055ec7a950b3fa86676da5ed0680c41a` requires exact declared prompt text/digest and separates proposal or faithful extraction, support, applicability, and host decision through schema 9/storage `1.8.0`. Source acquisition can retain source support but cannot evaluate local applicability or admit; a distinct analysis-run-owned source-application decision binds every evaluated result to exact neutral fact bytes. Applicable-but-abstained decisions retain those facts without an admitted artifact, while only supported plus applicable decisions enter current consumers, and candidate replay reopens every fact and its bundle. Realistic populated schema-8 migration and current-only consumers preserve historical bytes without semantic authority. Public semantic packages v1-v13 and their dependent frozen-v2 live wrappers are development history; their original evidence and accounting bytes remain unchanged and grant no current product verdict. |
| Final accounting | WP9 starts: 11; WP10 starts: 2; WP11 starts: 1. Successor settled exposure is USD 0.11332, unresolved conservative exposure is USD 1.37216, outstanding reservation is zero, and total committed exposure is USD 1.62548. USD 8.37452 of the aggregate hard limit remains unused. |
| Independent semantic evaluation | Deferred throughout M1 and M2 by ADR-0035. Packages v1-v13 are historical non-authorizing development evidence; no current package grants a product verdict. V14 was an unaccepted draft, is not a current candidate, and has been removed without review, registration, comparison, or archival. The missing independent semantic verdict is an explicit deferred risk. |
| Next gate | The owner must accept, reject, or amend the exact Slice 8 product candidate and documentation-only handoff. Passing verification does not accept the slice. Slice 9 remains unplanned and unauthorized; the independent semantic verdict remains explicitly deferred. |
| External-effect authority | Consumed. Slice 8 is local and effect-free. Read-only controlled-real input use is approved only through the exact pre-WP3 manifest/root gate. Credential exposure or enumeration, provider/network/billable calls, semantic-oracle work, private fixtures, evaluator-private repositories, archives, destructive work, external publication, and push remain prohibited. |

## Current authority

- [Consumed practical Slice 6 development continuation](plans/milestones/m1/slices/s6/development-continuation.md)
- [Development execution policy](execution-policy.md)
- [M1 milestone plan](plans/milestones/m1/plan.md)
- [Accepted M1 process-continuation amendment](plans/milestones/m1/amendments/process-continuation.md)
- [Accepted semantic-oracle deferral ADR](architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md)
- [Accepted M1 semantic-oracle deferral amendment](plans/milestones/m1/amendments/semantic-oracle-deferral.md)
- [M1/M2 product-conformance verification profile](evaluation/m1-continuation-verification-profile.md)
- [Slice 6 entry](plans/milestones/m1/slices/s6/README.md)
- [Accepted Slice 6 plan](plans/milestones/m1/slices/s6/plan.md)
- [Accepted Slice 6 lean continuation plan](plans/milestones/m1/slices/s6/continuation-plan.md)
- [Accepted Slice 6 remainder plan through R2](plans/milestones/m1/slices/s6/remainder-plan.md)
- [Slice 6 implementation record](plans/milestones/m1/slices/s6/record.md), only when exact chronology or retained evidence is needed
- [Accepted and activated Slice 7 plan](plans/milestones/m1/slices/s7/README.md)
- [Slice 7 implementation record](plans/milestones/m1/slices/s7/record.md)
- [Accepted and activated Slice 8 plan](plans/milestones/m1/slices/s8/README.md)
- [Slice 8 implementation record](plans/milestones/m1/slices/s8/record.md)

The accepted process amendment governed Slice 7 and continues to govern future
activated M1 work. Narrower
historical-integrity, answer-isolation,
credential, destructive, and external-effect rules override it only at their
exact immutable or irreversible boundary.

## Accepted Slice 6 baseline

WP1-WP8, the R1 answer-isolated live-semantic v2 package closure, and the R2
effect-free WP9-to-WP11 product/persistence/replay path are accepted. The
Slice 6 contract identities covered by the nine verified contract families are
`Slice-frozen`; Slice 5 v1 contracts remain `Slice-frozen`.

R1 accepted five answer-isolated v2 packages, 23 closed repository schemas,
the additive registry v2, v2-only resealing, frozen-v1 preservation, and
read-only semantic-authority verification. R2 accepted the v2 campaign/profile
consumers, full WP10 state and exact-one artifact/application persistence,
WP10-to-WP11 positive reuse, independent negative provenance, atomic SQLite
writes, reopen/replay and corruption rejection, semantic review, lifecycle and
expiry validation, and an effect-free end-to-end rehearsal.

Exact WP1-WP8 identities, consumed qualification/recovery namespaces,
historical candidate corrections, command receipts, and detailed evidence stay
in the [Slice 6 implementation record](plans/milestones/m1/slices/s6/record.md).
They are not competing live handoffs.

## Accepted Slice 7 baseline

Slice 7 added one deterministic category-neutral scope-reversion analyzer and
proved it on matched synthetic actor/AI/FaceGen and REFR/link/placement
examples. It distinguishes supported reversions from intentional or harmless
changes and abstains when evidence is ambiguous, while retaining provenance,
coverage, persistence, replay, and bounded JSON/CLI output. The owner accepted
the exact product and handoff commits named above; Slice 7 contracts are
`Slice-frozen`. This is public synthetic product-conformance evidence, not a
private held-out semantic verdict or a broad safety/readiness claim.

## Process and successor-campaign state

The owner-authorized practical continuation completed the sequential live
campaign and effect-free C3 candidate at
`c9c06aad0185db19b9d8e41cc01eca54aa453977`. Every credential and provider
effect is closed, the ledger has no outstanding reservation, and all earlier
campaign and attempt identities remain immutable historical evidence.

Post-closeout review found that the live request bytes did not carry the full
detailed prompt whose identity and fingerprint the evidence recorded. The
effect-free correction now rejects that mismatch before dispatch and binds the
exact serialized instruction bytes to the declared prompt identity and SHA-256.
It also retains four independent semantic facts: what was proposed or
faithfully extracted, whether bounded evidence supports it, whether it applies
to the exact local context, and whether the host admits or abstains from a
conclusion. Unsupported means insufficient support, contradicted requires
direct opposing evidence, and abstained means the host publishes no conclusion.

Public semantic-admission packages v1-v13 are retained as historical
non-authorizing development evidence. Their package bytes and registry
bindings remain integrity-visible, but they are not compared with current
product output and grant no verdict. No successor may be authored until an
accepted M3 evaluation plan reactivates that work after M2 acceptance. Schema
9/storage `1.8.0` still carries ADR-0034's product axes through persistence,
backup/restore, and replay. No live response, ledger event, accounting fact,
retained evidence, credential, or provider effect changed.

Git may bind reviewed bytes, but runtime effect authority comes only from a
closed typed manifest plus durable coordinator-owned admission, use,
settlement, and terminal state. Branch/HEAD state, commit subjects, Git log
order, pickaxe, line attribution, and historical marker discovery never grant
runtime authority.

## Evaluator status

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and is not resumable.
- Protocol `/4` is retired under ADR-0033 and archived outside this repository;
  it has no current execution, testing, review, or authority role.
- Ordinary product work must not access the evaluator-private fixture
  repository or create, repair, retry, or replace private evaluator work.
- The compact chronology and exact Git-backed recovery map are in
  [Evaluator history](evaluation/evaluator-history.md).

## Maintenance rule

Update this file only when the live handoff, accepted inputs, meaningful gaps,
or next gate changes. Do not copy rejected-candidate chronology here. Historical
plans, incidents, attestations, command receipts, and implementation records
preserve their own evidence and must not be scanned to infer current authority.
