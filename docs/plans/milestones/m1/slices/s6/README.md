# M1 Slice 6

Status: Accepted
Disposition: Active slice navigation; live authority remains in current state

Last reviewed: 2026-08-21

Live authorization is stated only in
[current project state](../../../../../current-state.md). This entry routes to
current Slice 6 authority and evidence without duplicating implementation
chronology.

## Current authority and navigation

- [Accepted Slice 6 plan](plan.md)
- [Accepted Slice 6 remainder plan](remainder-plan.md), governing historical
  R1-R2 execution and preserving the original remaining obligations
- [Accepted lean continuation plan](continuation-plan.md), superseding post-R2
  orchestration only after activation
- [Accepted M1 process-continuation amendment](../../amendments/process-continuation.md)
- [M1 continuation verification profile](../../../../../evaluation/m1-continuation-verification-profile.md)
- [Replacement C2 inert authority-package report](c2-replacement-authority-package-report.md),
  whose exact package commit and digest are now owner-accepted
- [Replacement C2 typed package](m1-slice6-c2-replacement-authority-package.v2.json),
  which grants no effect authority by itself
- [Replacement C2 owner acceptance record](c2-replacement-owner-acceptance.v2.json),
  opening only the package-defined C2A handoff and predecessor-gated C2B-C2D envelope
- [C2A post-success recovery authority report](c2a-post-success-recovery-authority-report.md)
  and [typed package](m1-slice6-c2a-post-success-recovery-authority-package.v1.json),
  plus its [owner acceptance record](c2a-post-success-recovery-owner-acceptance.v1.json),
  which opens only one independently reviewed all-zero ledger recovery
- [Accepted C2A recovery evidence](c2a-post-success-recovery-evidence-acceptance.v1.json)
  and [evidence report](c2a-post-success-recovery-evidence-report.md), which
  supply the accepted predecessor for the newly opened sequential C2B-C2D handoff
- [Terminal predecessor C2 report](c2-authority-package-report.md) and
  [acceptance record](c2-owner-acceptance.v1.json), retained only as terminal
  chronology and never reusable authority
- [Append-only implementation record](record.md), for exact chronology and
  retained evidence
- [Owner-authorized hard-budget continuation amendment](m1-slice6-development-campaign-amendment.v2.json),
  which supersedes the obsolete five-start, repeated-defect, and cumulative-reservation
  restrictions without changing the USD 10 aggregate, one-start, sequential,
  first-valid, credential, or isolation boundaries
- [Active generation-2 successor campaign v7](m1-slice6-successor-campaign-authorization.v7.json),
  [credential-access v3](m1-slice6-successor-credential-access.v3.json), and
  [production profile v5](wp9-production-profile-authorization.v5.json), which
  admit only the verified replacement generation and the single ledger-v4 path
- [Accepted campaign-v7 review](m1-slice6-successor-campaign-v7-independent-review.v3.json)
- [Historical campaign v6](m1-slice6-successor-campaign-authorization.v6.json)
  and [credential-access v2](m1-slice6-successor-credential-access.v2.json),
  superseded for new effects but retained as immutable ledger-v3 lineage
- [Superseded R1-R7 orchestrator handoff](orchestrator-handoff.md), retained as
  historical execution guidance only

Task-specific fixture, campaign, research, and authority documents remain
linked from the accepted plans and implementation record. Load them only when
the current package names them as exact inputs.

## Current handoff

Credential replacement is complete. Independently accepted evidence proves
generation 2 active-verified/available, the predecessor absent and ineligible,
exact masked UI and native-call closure, and zero DNS/network/provider/billable
effect during replacement. Campaign v7 imports immutable ledger-v3 sequence 39
into the only active ledger-v4 path at sequence 40. Committed Slice 6 exposure
remains USD 0.91056 with no outstanding reservation and USD 9.08944 remaining.
The next eligible action is fresh WP9 ordinal 9; WP10, WP11, C3, and Slice 7
remain gated exactly as stated in current state.

R1 is accepted at candidate
`fcd17cd6db98019fc9e5253d5167d2487862671c`. Its final NonLiveAll receipt
SHA-256 is
`38d7dc84a7d4c05945ee937aaa3559a01a46deaa8fde3d523a4657805272ab7c`,
and its ordinary Layer 6 receipt SHA-256 is
`9bcdadf4e57cc453306570474260783a49b9ba4cf76a3d1a9dabbe6c79a38071`.
R1 froze the accepted answer-isolated live-semantic v2 packages, closed
repository schemas, additive registry v2, v2-only resealing, and frozen-v1
preservation without any external effect.

R2 implementation `67ca34d6de162ad64f05fbe88972105745d3e831` and handoff
`8c25ca7274c394e41953a0b076010c26f6ffa97e` are accepted. R2 completes the
effect-free WP9-to-WP11 product, persistence, replay, provenance, recovery,
semantic-review, evidence, lifecycle/expiry, and offline-rehearsal path. Its
final `CampaignV2NonLive` receipt SHA-256 is
`bb9455c293b3049c1561cf9a15322c0b6b79502365097b51e2c92e4014e5019d`.
All credential, native, network, provider, and billable counts were zero.

The owner accepted the process amendment and lean continuation proposal at
commit `2c82365fd853cb2021f1772d6c572ee9fa006d01` and accepted documentation
activation commit `0b015753a926b1e498f59ffc3fbef1d07597b94a`. C1 effect-free
readiness closure is accepted in the commit containing this handoff. It adds
typed runtime authority and durable campaign-ledger binding, closes the safety
and invalid-state surfaces, preserves the exact R1/R2 public authority, and
rehearses WP9 through WP11 with fake credential storage and literal loopback.
No external effect was materialized. The owner-authorized C1.1 correction is
implemented at `9aea07380a3d3cc2a6f70be6d32907a96e7720da`; it adds fresh v3
authority paths and rejects retired v2 identities for every external effect.
The owner accepted exact C2 package commit
`926d6a49a37b6c465cb706cdebbfbf8b98b32c61` and package SHA-256
`10b1704591f36a85a6e680b13f28744ad5e81b786efad9aca276ce791b169b9c`
on 2026-08-19. That decision opens C2A runtime-authority preparation and the
package's sequential predecessor-gated C2 path; it does not itself create a
runtime authority, stage request, durable admission, profile, safety latch, or
effect evidence.

The resulting C2A attempt reached exactly one helper launch, then stopped at
helper-side `manifest-validation` before the credential dialog, secret entry,
containment descendant, or native store creation. Typed evidence proves zero
credential-manager, DNS, network, provider, and billable operations. The
campaign ledger is terminal, and every v3 campaign, credential, profile,
generation, target, stage, runtime-authority, package, and decision identity
from that attempt is reserved historical state that cannot be retried or
reused. C2B-C2D did not open.

The owner-authorized offline C1.2 correction is committed at
`356ce66c18bb67d5b1de8815970e04bf88195a86`. It introduces the v4
helper/coordinator authority seam, explicit v2/v3 retirement rejection,
compiled-helper coverage before UI/native execution, and truthful known-zero
failure summaries. Its exact clean-commit readiness floor and reproducible
126-file Release closure passed. The exact replacement package at commit
`ad5277fa3c5861f4f6115fe26215e55b61e30728`, SHA-256
`c19248fcc843808588860968a04828cfab5105e5cf08ca98228fbc98419be2bf`,
is now owner-accepted. That decision opens C2A runtime-authority preparation
and one masked credential enrollment, but creates no effect automatically.

C1 effect-free readiness closure is accepted. The owner later accepted keeping
it implementation-active through C2, with final C1 binding required before C3.

That enrollment submitted successfully and retained success evidence proves
the credential write/read-back, active verified profile, exact four-call native
trace, UI cleanup, and zero network/provider/billable operations. The
coordinator then rejected the helper's canonical
`wp9-production-profile-enrollment` scenario because one downstream validator
still expected `wp9-production-profile/enroll-and-verify`. The conservative
failure path terminalized the ledger. The credential operation must not be
retried and Credential Manager must not be inspected or modified.

The owner accepted keeping C1 implementation-active until C2 completes, with
final C1 binding required before C3. Corrected implementation
`c8cc455c8320f50bc87a160e3523f34eceb2ad13` now aligns the scenario contract,
validates the retained success and read-only durable product projection, and
provides a closed append-only recovery path. Its complete non-live floor and
independent affected-surface review passed. The inert authority package is the
owner-accepted recovery boundary and its consolidated package review passed
with no remaining must-fix. The zero-effect recovery is now complete and
independently accepted; it changed only the append-only campaign ledger.

## Lean continuation

The remaining accepted R3-R7 obligations are preserved through three outcome
packages:

1. `M1/S6/C1` - one coherent effect-free readiness candidate, focused checks,
   consolidated review, corrections on the same candidate, and one final floor
   and binding when review-ready.
2. `M1/S6/C2` - one fresh, separately owner-authorized bounded live campaign:
   masked credential enrollment, WP9 transport qualification, WP10
   source-claim extraction, and WP11 candidate investigation, each with exact
   admission, expiry, ceiling, persistence, evidence, and no-retry gates.
3. `M1/S6/C3` - retained-evidence replay, composed provenance, accumulated
   verification, contract-maturity review, documentation, and the final owner
   Slice 6 acceptance handoff without another effect.

C1-C3 preserve WP9, WP10, and WP11 identities, semantic outcomes, exact
profile and ceiling requirements, persistence/replay, invalid-state handling,
product/evaluator separation, answer isolation, provenance, and final owner
acceptance. The change is execution cadence and package decomposition, not
scope reduction.

## Effect and campaign boundary

### 2026-08-21 hard-budget successor amendment

The v4 C2B campaign is now terminal historical evidence. Its final ledger event
is `282c97151dbdcd354288b67f96c4b01d7f7ef43b1bbfb9f247cbd9b510506de9`.
Nothing in the successor work reopens that ledger or reuses its campaign,
stage, attempt, or runtime identity. The terminal WP9 start and USD 0.14
reservation remain conservatively consumed.

The owner-authorized
[hard-budget amendment](m1-slice6-development-campaign-amendment.v2.json)
and [v6 successor campaign](m1-slice6-successor-campaign-authorization.v6.json)
supersede the obsolete v5 execution cadence. Each independently reviewed and
durably admitted fresh attempt permits at most one possible provider start and
one masked-helper `CredReadW -> CredFree` sequence.
Automatic retry, parallel calls, credential enumeration/write/delete/
replacement, and semantic-output selection remain prohibited.

There is no per-stage start count, attempt count, or per-attempt policy-cost
ceiling. A failed or ambiguous attempt may have a fresh successor after its
sanitized evidence is independently accepted and exact offline diagnosis and
correction are durably bound. The first structurally valid WP10 or WP11
response is permanent stage authority and stops further calls for that stage
before semantic comparison. Historical committed exposure plus settled,
unresolved, and outstanding successor-v6 exposure may not exceed USD 10.00;
released pre-start reservations are reusable and cumulative reservations are
telemetry only.

The historical successor-v5 persistence candidate added migration
`M1-S6-SUCCESSOR-0007` (schema 6/storage `1.5.0` to schema 7/storage `1.6.0`).
The active hard-budget continuation adds the clean-break
`M1-S6-SUCCESSOR-V6-0008` migration to schema 8/storage `1.7.0`, preserving
the schema-7 rows while giving v6 authorization, reservations, responses,
settlement/replay, and semantic bindings successor-only tables with their
adjustable technical envelope and aggregate USD 10 accounting. It scopes
repeated frozen requests to fresh transport-operation and attempt identities.
Each reviewed runtime candidate also binds an exact logical
product-state checkpoint: a consistent read-only SQLite backup digest plus
every retained non-database file. Runtime admission recomputes it before any
possible start.

The successor live wrapper is `eng/run-m1-slice6-successor.ps1`. It accepts
only exact digest-bound campaign/stage/runtime/helper inputs and performs one
coordinator invocation. It contains no loop, retry, fallback, credential
inspection, authority derivation, or semantic-output selection.

Successor WP9 attempt 2 crossed one possible-start latch and stopped without a
retained response, HTTP status, provider identifier, or usage receipt. Its full
USD 0.11008 reservation remains unresolved; together with terminal WP9 the
conservative lineage total is USD 0.25008, leaving USD 9.74992 under the owner
cap and three WP9 starts under the lineage ceiling. Immutable evidence
`c642571f81670346e56e61902306df982a235d591bd0da50ccb2082e6d20690e`
supports only the possible-start and accounting claims. Its recorded adapter
send count is not independently verified, the exact containment predicate is
unavailable, and the credential read/free trace was not independently
retained. The versioned supplement repairs four empty optional accounting
values without rewriting that evidence, records those weakened claims, and
requires full identity/artifact/accounting validation plus independent review.
Every fresh attempt must use v2 evidence retaining the closed helper outcome,
adapter transport/send/DNS facts, TCP snapshot, containment predicates, and
validated trace/canary sidecars when safe.

No repository document supplies an API key. The consumed C2A authority permits
no retry. The recovery package permits no helper/UI launch, native credential
call, credential-store inspection, DNS/public network, provider contact,
billing, private-fixture access, archive work, destructive action, push, or
later-slice implementation. Package acceptance authorizes only one append-only
ledger event under a separately derived and reviewed all-zero runtime authority.

The old dormant campaign and credential authorization IDs named in the lean
continuation plan are retired without execution and cannot be inherited or
reused. The terminal C2 package bound its exact IDs, C1.1 binaries, paths,
expiries, ceilings, official-profile/capability/price evidence, sequencing, and
inert derivation rules. Its one helper launch is now terminal historical
evidence. The helper cannot run again. Before the ledger recovery can run, the
fresh zero-effect recovery runtime authority must be derived from the accepted
package and decision, independently reviewed, digest-bound, durably admitted,
and immediately revalidated.

That recovery is now complete and independently accepted. It appended exactly
one `credential-post-success-validator-defect-evidence-accepted` event while
preserving the prior ledger prefix, success/failure evidence, and durable
product state. All recovery-effect counters except the one ledger append are
zero. The owner then supplied the fresh v5 direction to execute C2B, C2C, and
C2D sequentially under that campaign, automatically continue only after
independent evidence acceptance, then establish the final C1 binding and
complete effect-free C3. At that historical v5 boundary, every then-existing
per-stage admission, one-start, ceiling, no-retry, settlement, and stop
condition remained exact. Those restrictions are superseded for the active
2026-08-21 hard-budget continuation above and remain historical validation
inputs only.

The exact C2B two-file candidate
`572a3342ac6537d42a1041e9f8a5878cfcba1958` passed independent security,
semantic, budget, answer-isolation, and diff review. This review creates no
runtime authority or durable admission; the exact admission transition and a
separately derived and reviewed typed runtime manifest remain mandatory before
the one-shot request can become eligible.

The exact stage transition is now admitted through
`2026-08-31T23:59:00.0000000Z`. This does not replace the still-absent typed
runtime authority or the immediate durable-ledger and effect checks.

Runtime effect authority comes from a closed typed manifest and durable
coordinator state, never Git history, current HEAD, commit subjects, log order,
pickaxe, line attribution, or historical marker discovery.

## Historical evidence

WP1-WP8 acceptance identities, qualification and recovery history, rejected
candidate diagnostics, command receipts, and the full R1-R2 chronology remain
in the [implementation record](record.md). The accepted remainder plan and
record are not rewritten. Do not accumulate that chronology in this entry or
create a new archive inside the active repository. Any later sibling-archive
transfer requires separate exact owner authorization.
