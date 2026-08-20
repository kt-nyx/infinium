# M1 Slice 6

Status: Accepted
Disposition: Active slice navigation; live authority remains in current state

Last reviewed: 2026-08-19

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
- [Terminal predecessor C2 report](c2-authority-package-report.md) and
  [acceptance record](c2-owner-acceptance.v1.json), retained only as terminal
  chronology and never reusable authority
- [Append-only implementation record](record.md), for exact chronology and
  retained evidence
- [Superseded R1-R7 orchestrator handoff](orchestrator-handoff.md), retained as
  historical execution guidance only

Task-specific fixture, campaign, research, and authority documents remain
linked from the accepted plans and implementation record. Load them only when
the current package names them as exact inputs.

## Current handoff

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

C1 effect-free readiness closure is accepted. That retained readiness evidence
does not itself decide the proposed implementation-active-through-C2 amendment.

That enrollment submitted successfully and retained success evidence proves
the credential write/read-back, active verified profile, exact four-call native
trace, UI cleanup, and zero network/provider/billable operations. The
coordinator then rejected the helper's canonical
`wp9-production-profile-enrollment` scenario because one downstream validator
still expected `wp9-production-profile/enroll-and-verify`. The conservative
failure path terminalized the ledger. The credential operation must not be
retried and Credential Manager must not be inspected or modified.

The owner asked the project to consider keeping C1 implementation-active until
C2 completes. The recovery package includes that proposal for exact acceptance;
it is not current process authority yet. Current work is the offline
scenario-contract correction and a closed append-only recovery path. Before
that path changes durable campaign state, its corrected executable, proposed
process amendment, and zero-effect runtime authority must be independently
reviewed, exactly bound, and separately owner-accepted.

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

No repository document supplies an API key. The consumed C2A authority permits
no retry. Current recovery work permits no helper/UI launch, native credential
call, credential-store inspection, DNS/public network, provider contact,
billing, private-fixture access, archive work, destructive action, push, or
later-slice implementation.

The old dormant campaign and credential authorization IDs named in the lean
continuation plan are retired without execution and cannot be inherited or
reused. The terminal C2 package bound its exact IDs, C1.1 binaries, paths,
expiries, ceilings, official-profile/capability/price evidence, sequencing, and
inert derivation rules. Its one helper launch is now terminal historical
evidence. Replacement v4 identities are sealed candidate inputs only and do
not grant authority. Before the C2A helper can run, the fresh credential
runtime authority must be derived from the accepted package and decision,
independently reviewed, digest-bound, durably admitted, and immediately
revalidated.

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
