# Current project state

Status: Accepted
Disposition: Current execution authority

Last reviewed: 2026-08-15
Owner: Project owner

This is the only document that states the live milestone, slice, work-package,
and evaluator handoff. Product documents and accepted ADRs define product
meaning; accepted plans define scope; records preserve implementation and
review history.

## Active handoff

| Field | Current value |
|---|---|
| Milestone | `M1` - active |
| Active slice | `M1/S6` - owner-accepted; implementation-active |
| Current authorized work | `M1/S6` finite-campaign amendment implementation, non-live verification, and correction/reverification only. Bound candidate `8b6c1df19cacd6b4fa92c58904bda426773adf4f` is excluded because locked restore found the IntegrationTests dependency lock stale after the exact PublicFixtures project reference was added. The replacement campaign is verification-pending until a corrected close-ready source and exact clean four-document binding successor are committed and fully reverified. Neither the correction nor any historical candidate is executable. Immutable owner authority source SHA-256 is `c9541bb5563304335e8f7af4d176eba3e507c719c4e135c542b8ac1bc4bc12be`. Campaign `infinium.m1-s6.finite-live-campaign/da6ba996-29b9-4aa7-a938-b6675047ebee` remains non-executable. No credential or provider effect is admitted. |
| Accepted Slice 5 candidate | Final cleanup implementation `5514919b8f742d00e59752fa7125da487a390926`, following public-fixture consolidation and protocol `/4` retirement |
| Accepted Slice 6 WP1 candidate | `61b90314d8273749849f590b303814008fa2fdfa`; nine Slice 6 contracts are `Implementation-active` and the accepted local input-bound policy is `openai-responses-o200k-byte-envelope/v1` |
| Accepted `M1/S6/WP2` candidate | `ed27ed04897103d93a60e6200971ca12d04f2e11`; capability, price, atomic reservation/final-gate, settlement, projection, replay, simulator, and public fixture/oracle evidence are independently accepted |
| Accepted `M1/S6/WP3` candidate | `b32939e8b7491a5c47453f912d25dd98c090f103`; one-shot helper process isolation, strict protocol, synthetic credential lifecycle, recovery, staging/admission, exact SDK `10.0.303`, and the integration synchronization barrier are independently accepted |
| Accepted `M1/S6/WP4` qualification | Owner-authorized execution `1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b`, retained evidence SHA-256 `3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390`, and bounded post-effect audit correction `be55eda59752f884fe6e113f40927295da45f2cd`; 12/12 targets are absent and the consumed namespace may never be reused |
| Accepted `M1/S6/WP5` candidate | `fd3c80d91dd247e65b5130309a9b5bb19dd1381f`, with append-only evidence `11e60445b6d5f1d3efc5b607f080dd986afb4ed4`; exact Responses serialization/codec/transport, deterministic loopback/offline replay, bounded secret-safe receipts, persistence/output/replay, and public WP5 evidence are independently accepted |
| Accepted `M1/S6/WP6` candidate | Product `ee0b6d31f1c1826c2af7634766155397e916c3e1`, append-only evidence `2b277338390f7dac37b5a5436bbe2cd81dedc871`, and answer-isolated oracle `37aa2b4e2fc084307ba5211f21bbeeb7a93efab0`; source-claim acquisition, deterministic admission, retained semantic provenance/replay, and later admitted-artifact consumption are independently accepted |
| Accepted `M1/S6/WP7` candidate | Product `59367a7479a7395b173b974bf720543aab2404d4`, append-only acceptance evidence `51251c0e0eb98d67dbc9b295b9ff084ebca33890`, and answer-isolated VAL-v3 oracle freeze `e9b032366552aa67649636655ed07a3bb50bb3b1`; deterministic candidate investigation, complete provenance, durable retention/readback, and database-owned replay are independently accepted |
| Superseded `M1/S6/WP8` acceptance evidence | Product/template `260a09ecfafea103227f113faf7625a5bf0ce759`, verification `fbdb1f03e006a85723b0533d44b2ed06e02cc724`, evidence/review HEAD `36b980d226e9f9a0e91281a530fc959a211fb696`, and their receipt hashes remain historical evidence only; they do not certify the corrected closeout candidate or make WP9 eligible |
| Accepted corrected `M1/S6/WP8` candidate | Verification `cc14bf60f78c80280cb6eafe60fddaf2bc764d06`, post-run evidence candidate `baef115cdd43fa38d0a352c15f8ba44cbfa35312`, NonLiveAll SHA-256 `52aa77325a2226505c35b1fba6d9d0fe2b6354022a6c85f6652211d609c529ad`, pre-live validation SHA-256 `e5f0ca42e44e6a4ea4f98bf4aab0c1bf6769c436c51aecc32a01e22a3db5f567`, and direct Layer 6 SHA-256 `4b0b661575b14c681e59d2097abca5a04cfa9baecea3837b7acef7d07e0227b5`; accepted without provider qualification, dispatch authority, or production-profile authority |
| Accepted Slice 6 plan | Explicit stateless/cache-off ADR-0025 conformance closure plus the exact finite-campaign amendment in Section 19A; one campaign may replace the original separate WP9-WP11 owner acceptances only after exact campaign review/admission and predecessor evidence |
| Next eligible action | Freeze the corrected dependency-lock source, materialize its exact clean four-document binding successor, then restart the complete non-live floor and fresh independent security, semantics, and diff review. Credential execution remains ineligible. |
| Campaign effect boundary | No API-key use, UI/helper/readiness launch, authority lock, live-manifest execution, native Credential Manager operation, profile materialization, DNS/public-network operation, provider request, or billable operation is authorized. All such observed counts are exactly zero. |
| Later work | Provider-stage manifests remain unmaterialized. Credential success must be independently accepted before WP9 request materialization; WP9 live evidence before WP10; WP10 live evidence before WP11. No historical marker grants inherited authority. |
| Execution policy | [Repository execution policy](execution-policy.md) |
| Milestone plan | [M1 backend semantic proof](plans/milestones/m1/plan.md) |
| Slice entry | [Slice 6 current summary and navigation](plans/milestones/m1/slices/s6/README.md) |
| Public verification profile | [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md) |

The B20 WP9 production-profile manifest and E20 review remain append-only
predecessor evidence. Section 19A now authorizes only a pre-effect,
field-by-field non-broadening semantic rollover into one finite campaign. It
does not make either historical owner marker current and does not pre-accept
unknown replacement bytes. Current authority is bounded correction and
non-live reverification only. A later fresh review is required after the
replacement close-ready source and exact four-document bound review candidate
are frozen; neither candidate exists during this correction state.
Binding `cfb11302d2011a0edc18aed6891ec1efff681713` is excluded by terminal
review because authoritative provider persistence, retained semantic evidence,
and offline evidence gates were incomplete; it is not review-ready or
executable.
Binding `f7257e63fb5269ff5f44e28097c7ce00cd71475b` is excluded by
post-bind Layer 6 verification because the exact finite-campaign review path
equation omitted current provider evidence surfaces; it is not review-ready or
executable.
Binding `aa1c1f582309f39daf57a17922128e068e134d40` is excluded by
NonLiveAll contract validation because registry and input-bound-v2 consumer
contracts were stale; it is not review-ready or executable.
Source `dd4d52c98ab74558c9e9f7006d466a73c3aafcc5` is excluded by focused
retained-WP8 validation because the exact finite-campaign path equation omitted
the public fixture registry schema; it is not review-ready or executable.
Binding `0da49dab6146ec34b71338df894cc36090677d3a` is excluded by NonLiveAll
because one unit contract fixture retained the superseded pre-v2 canonical
request shape; it is not review-ready or executable.
Binding `26edecadcd13c0201740f60729673c6ae90ecb53` is excluded by NonLiveAll
because two persistence fixtures retained the pre-campaign schema fingerprint
and input-bound policy version; it is not review-ready or executable.
Source `3a078804c70677b016f90a7214966693ea3d90a5` is excluded by focused
retained-WP8 validation because the exact finite-campaign path equation omitted
the changed persistence-state contract test; it is not review-ready or
executable.
Binding `ff5c18024bf1265ced69bb36f45ecf1d0843ab50` is excluded by focused
retained-WP8 validation because README formatting split the exact
`finite-campaign` review-state token; it is not review-ready or executable.
Binding `c3166811e57c23c84575689a29f31f9f4e3de1b8` is excluded by fresh
terminal review because production stage execution could fabricate its WP9
credential projection, WP10/WP11 deterministic validation packages were not
executed over the exact bound product inputs, SQLite and campaign-ledger
terminality could diverge after a known settlement, and the offline evidence
gates did not independently reopen the exact database, artifacts, markers, and
semantic provenance. The replacement correction closes those seams and
remains non-executable pending refreeze and complete reverification.
Binding `8b6c1df19cacd6b4fa92c58904bda426773adf4f` is excluded because the
complete common floor's locked restore found the IntegrationTests dependency
lock stale after the exact PublicFixtures project reference was added. Its
NonLiveAll receipt remains diagnostic only; the partially continued Unit and
Contract tranche is excluded because the preceding restore/build failed.
Every helper, readiness, authority-lock, native, profile, DNS/network,
provider, billable, and API-key observation remains zero; production output
roots remain absent. WP9 credential execution, WP9 request materialization,
WP10, and WP11 are ineligible until the exact campaign bootstrap closes.

The retained 2026-08-15 official-document review confirms the accepted
`gpt-5.6-sol` Responses/structured-output profile and unchanged conservative
short-context catalog. It also identifies current guidance recommending a
stable privacy-preserving `safety_identifier` for applications serving
individual end users. Section 19A resolves that request field as a stable local
random product-user seed with only a domain-separated SHA-256 projection
transmitted. The closed credential profile remains unchanged. No WP9
transport-qualification request manifest may be materialized before credential
success and independent evidence acceptance.

## Accepted Slice 6 authority

On 2026-08-10 the project owner accepted the independently reviewed Slice 6
plan and accepted explicit `reasoning.context: "current_turn"`, standard
reasoning mode, and explicit prompt-cache mode with no cache breakpoint/key as
ADR-0025 conformance closure. No separate ADR is required.

WP1 is accepted at exact candidate
`61b90314d8273749849f590b303814008fa2fdfa`, WP2 is independently accepted
at exact candidate `ed27ed04897103d93a60e6200971ca12d04f2e11`, and WP3 is
independently accepted at exact candidate
`b32939e8b7491a5c47453f912d25dd98c090f103`. WP5 is independently accepted
at exact candidate `fd3c80d91dd247e65b5130309a9b5bb19dd1381f`, with append-only
evidence `11e60445b6d5f1d3efc5b607f080dd986afb4ed4`. WP6 is independently
accepted at exact product candidate
`ee0b6d31f1c1826c2af7634766155397e916c3e1`, with append-only evidence
`2b277338390f7dac37b5a5436bbe2cd81dedc871` and answer-isolated oracle
`37aa2b4e2fc084307ba5211f21bbeeb7a93efab0`. WP7 is independently accepted
at exact product candidate `59367a7479a7395b173b974bf720543aab2404d4`,
with append-only acceptance evidence
`51251c0e0eb98d67dbc9b295b9ff084ebca33890` and answer-isolated VAL-v3
oracle freeze `e9b032366552aa67649636655ed07a3bb50bb3b1`. The nine Slice 6
contracts remain `Implementation-active`, while Slice 5 v1 remains
`Slice-frozen`. Qualification manifest `e3f76cd6` was executed exactly once
from `f0ee9814f8bd0100692dfa7b7cab83ed9181457f` and is now terminally consumed.
Its Submit and Cancel interactions completed, but a retained `SqliteException`
stopped the run before the third dialog. Cleanup recovery
`8b7fc811-7cd2-4c2a-abe1-506bd7b06bf5` then proved `backup-new` and
`fake-dispatch` absent with W0/R2/D0/F0/T2, combining with the immutable prior
ten-target proof for 12/12 terminal namespace absence. The qualification and
recovery identities, targets, locks, namespaces, and output roots are consumed
and may never be reused. The bounded non-native restore-clock correction is
accepted at `2f95692687b60d97db2710835e9d0966f131c164`. Fresh qualification
`e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe` was then executed exactly once from
`fdd9d49301e72f6a421e4597ea405bb2ca69da2f`. The owner completed Submit,
Cancel, and restored-generation Submit, but the run terminated during
`backup-restore-reauthentication/cleanup-successor` with cleanup ambiguity.
Recovery `6232bae5-f735-4db7-a74f-7ede9f67b752` then processed all 12 targets
in exact manifest order with W0/R13/D1/F1/T15, deleted the remaining
`backup-new` credential, and proved all 12 targets absent. The qualification
and recovery namespaces, authorities, locks, targets, and output roots are
consumed forever. The bounded non-native ambiguity-evidence correction is
independently accepted at `2dce8acc27eece01b0232dd531a2deb27ef752af`:
future terminal ambiguity retains separate secret-safe typed primary and
cleanup causes, validated and rejected native traces/counts/free pairing,
canaries, helper results and staging, conservative external-effect facts, and
known or explicitly unknown process containment. Fresh manifest
`4936dcef-a0f4-4302-9899-0afd99b19799` used a new disjoint disposable
namespace and superseded only the consumed e6 authority plus its terminal
12/12 recovery proof. It was executed once from
`8f49943d0af53c495b8f288048cbd8d8bd1fe775`. The owner completed disposable
Submit, blank Cancel, and different-disposable restored-generation Submit,
but no admissible third phase receipt was retained. The coordinator then
terminated at `backup-restore-reauthentication/cleanup-successor` with a
schema-v3 cleanup ambiguity: validated calls are W7/R60/D6/F19/T92, zero
later native calls, whole-namespace absence false, and the namespace blocked
forever. The qualification identity, authority, target namespace, output,
and lock are consumed and may never be retried or reused. Current authority
is limited to exact post-effect audit and preparation plus fresh independent
review of a conservative cleanup-only recovery. It does not authorize a
further Credential Manager operation or any provider operation before exact
review. Independent post-effect audit accepted an 11-target prior absence
inventory and identified only `backup-new` as unresolved. Draft cleanup-only
recovery `dd412ecc-3b2c-4628-8865-bc8574a357c7` is bound to that one target,
with W0/R3/D1/F1/T5 and no UI, enumeration, fallback, provider, DNS, or
network path. Fresh pre-effect review accepted the exact bytes, and the
recovery then completed once with W0/R2/D1/F1/T4: it freed and deleted the
remaining `backup-new` credential and ended with exact `ERROR_NOT_FOUND`.
Combined prior 11 plus recovery one proves all 12 targets absent; cleanup
ambiguity is false and namespace reuse remains blocked. The bounded non-native
root-cause correction is independently accepted at
`3456fe02594fd365b1d2627dd08fad44fe0aee92`. The third helper's valid
recovery metrics could exceed the coordinator's accidental 4 KiB reader cap;
writer and reader now share a closed 64 KiB metrics bound, ambiguity retains
its exact assignment and closed redacted validation stage, and helper
containment/no-reuse is proven before any later cleanup call. Only the exact
unadmitted restored-generation successor may use absence-only cleanup, without
changing the authoritative projection. Qualification
`076b981a-9d32-4e6a-af35-1e7017e0f833` was owner-authorized and executed once
from `31643235c014a93f71096d5c80d2a911758e328f`. The owner completed all three
dummy-only dialogs. The runner completed all scenarios and internal cleanup,
then failed with a typed `IOException` during post-success evidence
finalization before it could retain the required final evidence JSON. Source
ordering strongly implicates the recursive artifact scan over retained SQLite
files, but the durable error does not distinguish that scan from the final
evidence write, so the exact I/O substage is unproven. The identity is consumed
and may not be retried. Cleanup-only recovery
`040817c8-0a87-480a-915c-71dc2fe54da3` subsequently proved all 12 exact
targets absent with W0/R12/D0/F0/T12 and was independently accepted. The
bounded evidence-finalization correction is independently accepted at
`03ae6929bad069c7c9e351b2ed5bd361e31b89e7`: every store is disposed before
artifact scanning, summary bytes receive an in-memory raw-target canary scan,
failure stages retain a closed redacted evidence envelope, and final evidence
is atomically sealed before the human-readable summary. Fresh qualification
`c6e9226e-3d95-496c-bda6-c9142bb6b980` was then owner-authorized and executed
exactly once from `1fe62bbad155b4e9b8fc2d1056fee14a15dbc11b`. The owner
completed disposable dummy Submit, blank Cancel, and a different-disposable
restored-generation Submit. All nine scenarios and 41 phases passed. Retained
evidence SHA-256
`3f148b76fef94c077293d863a06447bb22b395997db2b09dea291193c1598390`
proves the exact W9/R78/D9/F28/T124 call trace, 28 read/free pairs, 12/12
post-cleanup absences, zero secret/raw-target canary matches, complete process
containment, and zero DNS, network, provider, billable, or retry operations.
The outer gate's post-effect parser defect was corrected without another
native run; exact audit-only receipt SHA-256
`87565206a33be6f2128254d2dfa9ba6006c57472a3038f69b407eb63253f98c9`
binds the retained evidence and consumed authority lock. Fresh independent
Windows credential/security review accepted the qualification. WP4 is
accepted, and its identity, authority, namespace, targets, lock, and output
root are consumed forever. The earlier WP8 acceptance at evidence/review HEAD
`36b980d226e9f9a0e91281a530fc959a211fb696`, product/template identity
`260a09ecfafea103227f113faf7625a5bf0ce759`, and verification identity
`fbdb1f03e006a85723b0533d44b2ed06e02cc724` was later invalidated as current
handoff authority by current-HEAD review and remains historical evidence only.
The corrected WP8 verification candidate
`cc14bf60f78c80280cb6eafe60fddaf2bc764d06` and post-run evidence candidate
`baef115cdd43fa38d0a352c15f8ba44cbfa35312` are independently accepted with
the exact three receipt hashes retained above. B20/E20 and every later WP9
owner marker remain append-only historical predecessor evidence only. The
first finite-campaign candidate B11 was rejected by terminal review because
its marker transitions, schema enforcement, production rollover/ledger route,
and post-use safety-identifier state were incomplete. Its bound successor B9
was also rejected because close-ready/review-candidate identity, separate
credential-evidence acceptance, production stage orchestration, successor
equations, and mutation proof remained incomplete. Binding
`876130e6a030b3228c151e030a6914cc2f156c04` was then excluded because its
committed rehearsal inherited ready bindings into the synthetic source A.
Current authority is correction and non-live reverification of the
finite-campaign implementation only; fresh review follows a new exact bound
candidate. The separately committed zero-effect
credential handoff and review/admission transitions for all three exact stage
manifests exist only inside the disposable committed temp-clone rehearsal.
They are synthetic proof of the production validators and coordinator state
machine, not active-repository authority or live evidence. No provider-stage
manifest, stage review marker, stage admission marker, stage evidence marker,
or composed-evidence marker is materialized in the active repository. No WP8
template, prior authorization, campaign marker, or owner marker is executable
or inheritable.
The corrected production path requires the exact accepted WP9 product-state
database and active-verified profile/generation/account/billing projection; it
cannot enroll, verify, or synthesize that predecessor. Exact WP10 and WP11
product-input, predecessor-manifest, oracle, and canonical request-template
bytes are validated by typed deterministic product oracles. The independent
offline gates reopen the campaign ledger and authoritative SQLite operation,
reservation, raw response, headers, usage, settlement, replay, and persisted
WP10-to-WP11 semantic links. These statements describe implemented non-live
validation surfaces only; the active repository still has no provider-stage
manifest or live evidence.
No API-key use, UI launch, live-manifest execution, native Credential Manager
operation, DNS operation, public-network operation, provider request, billable
operation, or production-profile materialization/use is authorized.

## Completed Slice 5 boundary

The accepted normalization amendment and owner-approved cleanup follow-up
completed functional renaming, fixture/tool relocation, documentation
consolidation, production and test-file decomposition, shared test support,
an exact public-fixture registry, and removal of proven temporary material.
Every affected producer, consumer, persistence seam, schema, fixture, test,
verification script, and current document was updated together. Public
semantic truth and claim boundaries did not change. On 2026-08-10 the project
owner accepted the revised closeout candidate, marked `M1/S5` complete, and
advanced its contracts from `Implementation-active` to `Slice-frozen`.

The compact [Slice 5 entry](plans/milestones/m1/slices/s5/current.md) routes
scope changes to the full plan and chronology/evidence questions to the full
implementation record. Do not copy that chronology back into current
navigation documents.

## Evaluator status

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and is not resumable.
- Protocol `/4` is retired under ADR-0033 and archived outside this repository.
  It has no current execution, testing, review, or authority role.
- Ordinary product work must not access the evaluator-private fixture
  repository or create, repair, retry, or replace private evaluator work.
- The compact chronology and exact Git-backed recovery map are in
  [Evaluator history](evaluation/evaluator-history.md).

## Maintenance rule

Update this file when the live handoff changes. Historical plans, ADR
chronology, incidents, attestations, occurrence ledgers, and implementation
records must never be scanned or amended to infer current status.
