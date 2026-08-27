# Current project state

Status: Accepted
Disposition: Frontend Application Foundation Checkpoint C under correction; Phase D blocked and not started, and M2 remains inactive

Last reviewed: 2026-08-27
Owner: Project owner

This is the single live handoff for Infinium. Product documents define what the
product means, plans define bounded work, and implementation records preserve
the evidence. Historical milestone and slice records are not current
instructions.

## Plain-language state

Infinium is now a tested backend and command-line proof of the product's core
analysis idea. It can accept bounded Skyrim mod-analysis data, identify one
specific family of suspicious conflict outcomes, retain the evidence and
uncertainty behind each result, reproduce stored results, and project those
internal results into a stable report format for a future interface.

Phases A and B of the accepted frontend foundation are complete. Phase C is
under correction. FindingReport query/readback, application validation,
export deletion/recovery, provenance, paging, and populated migration gaps now
have focused evidence. Targeted verification now has accepted executable
architecture in RESEARCH-0058, ADR-0038, and the accepted WP6 addendum: capture
a new snapshot, acquire semantic evidence under a separate durable owner,
prepare an inspectable dependency-closed and correlation-complete scope, and
start an ordinary `managed-analysis-v1` successor with exact lineage. The
architecture now has a complete implementation candidate. It prepares an
inspectable recheck, captures a fresh saved-profile snapshot, acquires current
semantic evidence, preserves every required member in the coverage denominator,
and atomically starts an ordinary `managed-analysis-v1` successor with exact
source-to-successor lineage. The corrected typed prepared-run
path validates its authoritative retained inputs and
atomically binds the supported durable analysis operation before scheduling. The repository
now has an exact application-authority inventory, a bounded display-safe
bootstrap, common typed error/revision/receipt/cancellation vocabulary, and a
closed renderer envelope/operation registry with a fail-closed reference
consumer. The application contract also exposes typed tool/profile/setup state,
versioned saved configurations, honest pre-run estimates, non-secret provider
status, and prepared manual-run initiation with immutable durable bindings.
The independent Phase C correction surface now has a real FindingReport
publication/queue/detail consumer, recursive closed request validation,
append-only structured-export deletion with crash recovery, and corrected
provenance and cursor binding. Checkpoint C remains suspended because the new
targeted-verification candidate still requires corrected architecture-steward
Checkpoint C review; implementation does not accept its own checkpoint.
The correction now proves the complete native named-pipe path without manually
publishing a ready plan: the actual snapshot and semantic producers feed the
production planner, qualified known-member processing failures remain visible
as limited plans, all authority-bearing gestures are globally one-shot, and
start rejects retained-source, target, plan, acquisition, proof, input, or
fencing drift before creating any successor authority.
The contract correction now projects the complete accepted preparation evidence
through generated C# clients with independent member, dependency, target-analyzer,
lifecycle, artifact-decision, and terminal-gap pagination. One store-locked
readback returns a single preparation/acquisition generation and validates all
retained capture, lifecycle, publication, provenance, and application-link
evidence before projecting it. The immutable preparation now retains the exact
resolved input manifest, delivered input, coverage input, and preallocated
successor identity that start must consume without replacement. Application
`1.13.0`, domain `1.6.0`, storage `1.16.0`/schema `17`, and persisted
targeted-plan `1.2.0` bind the corrected bytes. Terminal-gap continuation is a
bounded opaque token tied to the exact preparation/acquisition generation and
ordered gap set. Lifecycle continuation uses an append-only sealed cross-owner
sequence, so equal timestamps and later events cannot renumber previously
returned history. Schema 17 validates every projected lifecycle field against
both its retained owner event and the sealed unified ordering before readback.
Valid plans that cannot start because correlation is ambiguous or required
proof is missing are retained as inspectable `Invalidated` preparations,
including their complete denominator, reasons, gaps, and lifecycle evidence;
they cannot create successor authority. Schema-16 migration validates the
closed JSON shape and retained command, attempt, publication, and cancellation
bindings for every acquisition event kind before schema 17 seals the history,
and rejects tampering atomically while the accepted source schema and triggers
remain intact.
It binds finding and case roots to their retained canonical identity envelopes,
including analyzer, analyzer-version, semantic-contract, and identity-contract
versions. Raw plugin names, form keys, contribution strings, and normalized
paths cannot grant identity, and a changed contribution or asset slot remains
non-startable without retained typed continuity proof. Canonical analysis truth
remains the authority, WP5/WP6 are not accepted, and Phase D remains blocked.
The renderer boundary still permits exactly five Phase A operation/message combinations,
keeps gesture proof request-only, represents accepted and explicit non-success
outcomes separately, and commits replay state only after complete validation.

It is not yet a complete end-user application. There is no graphical interface,
ordinary mod-manager workflow, broad analyzer catalog, or claim that an entire
modlist is safe. The accepted M1-to-M2 Foundation — Frontend Application Foundation now
defines the missing backend/application layer and diagnostic desktop proof that
must be implemented before M2. M2 remains separately planned and authorized
work.

## Live handoff

| Field | Current value |
|---|---|
| Completed milestone | M1 - bounded backend semantic proof. |
| Completed transition | Post-M1 cleanup, including archive consolidation, modularization, report projection, functional renaming, governance, and local hygiene. |
| Accepted cleanup implementation | Base cleanup commit `58e0401b9510ab287ee44a83a547eefee82c79ae`; final naming/documentation/governance correction `c7b365eefb30aa6c066a7ab8e5d537c983415ca9`; complete final floor: 674 passed, 10 expected skips, 0 failed. |
| Accepted next plan | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`, with five orchestration phases and nine work packages. Planning base `32dbb2c48754666336d2da571e554ad8897ed71c`. |
| Active implementation | Frontend Application Foundation Phases A-B (WP1-WP4) remain accepted at corrected Checkpoint B. The ADR-0038 targeted-verification vertical is implemented as a corrected Phase C/WP6 candidate with five native-only RPCs, complete bounded preparation readback, canonical identity-envelope authority, typed correlation, inspectable non-startable plans, schema-17 sealed lifecycle durability, closed-shape schema-16 acquisition-event migration, stable opaque pagination, real fresh acquisition, production limited-plan correlation, globally one-shot gestures, pre-mutation start revalidation, atomic `managed-analysis-v1` successor admission, and exact lineage. WP5/WP6 remain under correction and the earlier Checkpoint C receipt stays suspended. |
| Next authorized action and gate | Perform corrected Checkpoint C architecture-steward review of the Phase C candidate. Phase D and `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP7` remain blocked. This does not activate M2. |
| External provider authority | Development-only OpenAI use is available through the explicit, budgeted, offline-by-default path accepted by ADR-0036. No live call is implicit, and the shipped product cannot silently use the development credential. |
| Independent semantic evaluation | Deferred through M2 by ADR-0035. Current fixtures prove contract and bounded behavior, not independent real-world accuracy or production readiness. |

## What the backend can detect now

The implemented scope-reversion analyzer detects a narrow conflict pattern: a
later winning record appears to undo a compatible change made earlier in the
load order because it copied an older or narrower version of the record.

Current tested examples cover:

- actor records where AI or FaceGen-related changes are unintentionally lost;
- placed-reference records where link or placement-related changes are
  unintentionally lost; and
- matched negative controls, where a difference is intentional, harmless,
  unsupported, or too ambiguous to call.

For each analyzed subject, the backend can produce a supported finding, a
resolved negative, an abstention, a failure, a limited result, or an explicit
coverage gap. Reports retain affected subjects, evidence, provenance,
severity/confidence under the analyzer's provisional policy, recommendations,
coverage totals, failures, and gaps. The `scope-reports` CLI command emits the
implementation-active finding-report contract as JSON or a bounded human-readable
view.

This scope is deliberately narrow. It does not yet detect every kind of plugin,
asset, script, configuration, dependency, performance, or runtime problem.

## Cleanup result

- One-time provider-campaign machinery, completed M0/M1 planning chronology,
  historical fixtures, retained run evidence, and other development-only
  material were removed from the active repository.
- Recoverable development history is committed in
  `../infinium-development-history-archive/` at
  `6f8976db6c560456201a9166caf4f36506be5477` (850 files, 42,587,985 bytes,
  with zero manifest hash failures).
- The abandoned implementation archive was cleaned and bound at
  `../infinium-legacy-archive/` commit
  `0fe8562007eeaa6ac3e4c8f883c9b8287db956e5`.
- The retired protocol `/4` repository remains immutable and separate. The
  private evaluator fixture repository was not read or modified.
- The active public evaluation registry now contains only current conformance
  packages. Historical package discovery can no longer silently influence an
  ordinary product run.
- Provider credentials, explicit development invocation, usage accounting,
  semantic admission, persistence, and replay are separated by responsibility.
- Functional naming is enforced automatically across ordinary source,
  scripts, projects, protobuf, structured contract names, and repository
  paths. The reviewed allowlist contains 190 exact compatibility,
  governance, retained-evidence, or genuine domain-term exceptions, with zero
  cleanup-debt and zero unexplained findings.
- Verification closeout now requires zero repository-owned .NET/test-host
  survivors. If an SDK build server demonstrably holds generated repository
  data open after all builds finish, the policy requires graceful
  `dotnet build-server shutdown`, never a broad process-name kill.
- The report projection is implementation-active so M2 can consume it, but its
  presentation and user workflow remain M2 work.

## Known limitations

- Severity and confidence are honest analyzer-local defaults, not broadly
  calibrated promises across all future analyzers.
- Public fixtures are developer-owned conformance evidence. They are not an
  independent answer key and do not establish population-wide accuracy.
- The finding-report contract is exercised end to end but remains
  implementation-active until M2 proves its real producer and interface
  consumers together.
- Coverage outside the implemented record families remains explicit rather
  than being treated as safe or compatible.
- There is no graphical interface, installer, ordinary user workflow, or
  production-readiness claim yet.
- Native credential entry remains unavailable in Phase B; provider status is
  non-secret and local-only setup/run evidence remains available.
- Phase C remains under correction. FindingReport query/readback, recursive
  Phase C request validation, export deletion/recovery, and corrected
  provenance/paging have focused offline producer-consumer evidence. Populated
  pre-publication stores now expose a typed report-projection-unavailable gap
  instead of an ambiguous empty page. Targeted verification now has an
  implementation candidate, but that does not restore or accept Checkpoint C
  before architecture-steward review. It does not build the desktop, expand analyzer scope, qualify
  an independent semantic oracle, or activate M2.

## Current authority

- [Product definition](product/product-definition.md)
- [Product scope and milestones](product/scope-and-milestones.md)
- [Development execution policy](execution-policy.md)
- [Product-conformance verification profile](evaluation/product-conformance-verification-profile.md)
- [Development-provider access ADR](architecture/decisions/ADR-0036-development-provider-access-and-product-credential-separation.md)
- [Independent semantic-oracle deferral ADR](architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md)
- [Functional implementation naming governance](governance/functional-implementation-naming.md)
- [Frontend application contract and desktop bridge ADR](architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [Targeted-verification preparation and execution ADR](architecture/decisions/ADR-0038-targeted-verification-preparation-and-execution.md)
- [M1-to-M2 Foundation — Frontend Application Foundation](plans/transitions/m1-to-m2/frontend-application-foundation/README.md)
- [Accepted Phase C/WP6 targeted-verification addendum](plans/transitions/m1-to-m2/frontend-application-foundation/wp6-targeted-verification-addendum.md)
- [Post-M1 cleanup closeout](plans/transitions/post-m1-cleanup/README.md)
- [Post-M1 cleanup implementation record](plans/transitions/post-m1-cleanup/implementation-record.md)

## Historical recovery

Detailed M0/M1 plans, slice records, historical provider/evaluator development
material, and their exact transfer inventory are recoverable from the
development-history archive commit named above. They are intentionally absent
from active navigation and do not grant current product or runtime authority.

Update this file only when the live handoff, accepted inputs, meaningful gaps,
or next gate changes.
