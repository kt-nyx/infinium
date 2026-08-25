# M1 platform fixture manifests

Status: Accepted

Accepted: 2026-07-28

Accepted by: Project owner

Last reviewed: 2026-08-10
## Purpose

This document defines the pre-registered fixture families for
[the archived M1 platform and operational case specifications](../evaluator-history.md).
It creates no executable fixture, contains no production implementation, and
does not mark any EVAL execution passed or any described fixture
execution-ready.

The 2026-08-08 Slice 5 recovery removed six prematurely instantiated platform
packages. Their behavioral descriptions remain accepted obligations, but the
affected sections below are now staged WP5 slots, not current or reserved
fixture identities. WP5 must assign fresh package identities when it authors
and reviews those slots. Do not reconstruct the removed packages from
historical records.

All initial fixtures are synthetic and use invented identities. No real mod
name, Nexus identity, private profile, or answer-bearing filename may enter
production logic. Fixture answer manifests and canary values are supplied only
to the evaluation harness, never to the coordinator, worker, helper, adapter,
renderer, model, retrieval path, or analyzer under test.

## Instantiated WP5 package identities

The product-blind WP5 authoring pass on 2026-08-09 assigned these fresh public
package identities without reconstructing the removed packages:

| Package identity | Version | Partition | Status |
|---|---:|---|---|
| `infinium.m1s5.wp5.publication-replay-query-output-recovery-safety.lantern-a` | `1.0.2` | development | independently reviewed; typed-policy comparison complete; explicit native capability gaps |
| `infinium.m1s5.wp5.publication-replay-query-output-recovery-safety.compass-b` | `1.0.2` | validation | independently reviewed; typed-policy comparison complete; explicit native capability gaps |

Their shared registry, answer-free factual inputs, and separately isolated
expected results are under
[`m1-slice5-wp5-operational-cases-v1/`](../../../fixtures/public/operations/analysis-lifecycle/README.md).
This registration records their bounded typed-policy comparison as complete:
12/12 frozen bindings passed with no expected-truth edits and with actual
pre-dispatch schema/answer-isolation receipts. It does not establish
unconditional execution-ready native filesystem coverage. Native symbolic-link
creation was unavailable, and native 8.3 alias, UNC, device,
alternate-data-stream, and cross-volume qualification remain explicit gaps or
stand-ins. The result does not broaden the registered EVAL claims or establish
the explicitly excluded full external-adapter, lifecycle, persistence, or IPC
matrices. A validation result that drives product change must follow the
partition transition and replacement rules below.

Version `1.0.2` replaces the review-rejected `1.0.1` authoring bytes while
preserving its accepted projection isolation and symmetric family structure.
It gives both safety counterparts neutral, explicit physical object graphs and
complete race transitions; permutes the validation command order; and derives
independent accept/reject vectors solely from frozen final-object authority
facts. The rejected identities are not executable or authoritative inputs.

## Common manifest schema

Every instantiated fixture manifest shall contain:

```text
fixture_id
fixture_version
partition = development | validation | held-out
partition_history
purpose
positive_negative_boundary_class
eval_case_ids
requirement_ids
adr_ids
taxonomy_version
taxonomy_assignments_or_not_applicable
application_and_schema_versions
protocol_and_native_dependency_versions
installation_snapshot_identity
analysis_context_identity
effective_scan_configuration_identity
resolved_input_manifest_identity
owner_run_job_attempt_identities
expected_observations
expected_candidates_hypotheses_findings_cases
expected_abstentions_gaps_coverage
expected_lifecycle_and_cost_events
expected_filesystem_process_network_effects
expected_replayability_and_auditability
fault_schedule
security_and_non_mutation_canaries
ground_truth_method
answer_isolation_review
replay_dependency_manifest
licensing_redistribution_and_privacy
known_limits
reviewer_and_review_time
```

For platform-only cases with no semantic candidate/finding/case, those fields
shall explicitly contain `not-applicable`; they must not be omitted. The
taxonomy version is retained even where classification is `not-applicable`
because these fixtures test historical contract/version preservation.

## Shared fixture root: M1-PLAT-BASE-v1

Create one disposable root outside every real modding and product-data
location:

```text
fixture-root/
  protected/
    mo2-instance/
      ModOrganizer.ini
      profiles/Profile-A/
      mods/Fixture Alpha/
      mods/Fixture Beta/
      overwrite/
    game/
      SkyrimSE.exe.fixture
      Data/
    generated/
    configuration/
  product/
    data/
    payloads/
    cache/
    attempts/
    diagnostics/
    run-output/
    update-staging/
  external-tool-private/
  export-outside-protected/
  acquisition-origin/
  oracle/
```

`protected/` is immutable to the product and every invoked operation.
`product/` is writable only through the matching fixed write class.
`external-tool-private/` is writable only by the pre-registered exact tool
operation. The fixture root is never the user's actual MO2, game, or Infinium
root.

The protected-root oracle records:

- relative directory entries;
- bytes and cryptographic hashes;
- volume/file identity;
- creation and last-write time;
- attributes and ACL;
- reparse type/target;
- alternate data streams; and
- relevant configuration values.

Access time is informational only where reads can update it. Each test repeats
the comparison after success, rejection, injected crash, and recovery.

## Shared canary pack: M1-PLAT-CANARY-v1

Generate fresh per-run random values for:

- `SECRET_CANARY`;
- `CREDENTIAL_TARGET_CANARY`;
- `PRIVATE_USERNAME_CANARY`;
- `ABSOLUTE_PATH_CANARY`;
- `UNTRUSTED_INSTRUCTION_CANARY`;
- `STALE_PUBLICATION_CANARY`;
- `FORBIDDEN_NETWORK_CANARY`;
- `FORBIDDEN_COMMAND_CANARY`; and
- `ANSWER_LABEL_CANARY`.

The oracle searches exact bytes plus UTF-8, UTF-16, JSON-escaped, URL-encoded,
base64, case-folded where applicable, and structured serialization variants.
The secret and credential-target canaries are forbidden from:

- renderer/WPF state and messages;
- application and worker gRPC;
- command lines and environments;
- coordinator/general-worker memory snapshots when the supported harness can
  inspect them without itself exposing the secret;
- SQLite, WAL, shared-memory, payloads, staging, checkpoints, settings, and
  backups;
- prompts, requests excluding the provider authorization header held only by
  the helper, provider response bodies, and model context;
- logs, traces, stdout/stderr, errors, crash artifacts, CLI output, run-owned
  JSON, diagnostics, and exports.

The credential test oracle and exact one-shot helper/OS credential operation
are the only permitted secret/target surfaces.

## Fault schedule: M1-PLAT-FAULTS-v1

Each applicable test is parameterized to terminate, suspend, reject, corrupt,
or race at these named boundaries:

1. before and after durable intent;
2. before and after lease/fence acquisition;
3. before and after job claim;
4. before and after reservation commit;
5. before and after final dispatch authorization;
6. before and after possible transport start;
7. before and after checkpoint staging and commit;
8. before and after output/payload staging;
9. before and after coordinator admission/publication;
10. before and after usage receipt/settlement;
11. before and after terminal transition;
12. before and after payload placement/registration;
13. before and after migration/backup/restore/deletion steps; and
14. during IPC handshake, page, stream, resync, and client/worker/helper crash.

The harness records the exact injected point and verifies that it actually
occurred. An untriggered fault variant is not a pass.

## Fixture families

### M1-PLAT-IMMUTABILITY-v1

**Cases:** EVAL-0026.

**Inputs:** Base snapshot `S1`; context `C1` with one inferred and one
user-provided assumption; saved configuration source `GS1`; effective
configuration `G1`; one relevant and one irrelevant file dependency; staged
job partitions with deterministic gates.

**Mutations:** Create `GS2/G2`, `C2`, and `S2` while the active run crosses
each applicable fault point. Also rename an unrelated display value.

**Oracle:** A table enumerating every run/artifact and its only legal
`S1/C1/G1/M1` binding, the expected affected dependency set, terminal/gap
outcome, and allowed new-version identities. No semantic finding is expected;
candidate/hypothesis/finding/case state is `not-applicable`.

**Matched controls:** Editing the saved configuration before run resolution
binds the new version normally; editing only an unrelated display label during
the run neither rebinds nor invalidates it; and a no-edit run reaches the same
typed analytical outcome under `S1/C1/G1/M1`.

**Replay dependencies:** Original fixture bytes, configuration/context versions,
dependency graph, transition policy, job definitions, application/schema
versions, and event trace.

### WP5 untrusted-content platform slot

**Cases:** EVAL-0033 and EVAL-0035.

**Inputs:** Equivalent hostile instructions embedded in sanitized-source HTML,
Markdown, plain text, log/tool/model output, metadata, filename/record string,
malformed binary envelope, IPC fields, and renderer messages when applicable.

**Adversarial corpus:**

- requests to reveal canaries or hidden prompts;
- fake system/tool/result roles;
- requests to change source authority, budget, provider, scan scope, or
  readiness;
- shell/PowerShell/cmd fragments and executable paths;
- SQL, arbitrary paths, URLs, UNC/device/ADS syntax, and traversal;
- encoded/nested/Unicode-confusable operations;
- oversized/deeply nested content and malformed lengths; and
- requests to publish staged data or treat the document as authorization.

**Matched negative:** Benign documentation containing code examples and words
such as “install,” “delete,” or “run” that must remain usable as evidence while
remaining inert.

**Oracle:** Zero privileged side effects; exact typed rejection/abstention/gap
classes; retained inert content provenance; no canary disclosure.

**Replay dependencies:** Corpus bytes, sanitizer/renderer/parser versions,
prompt/schema and bridge/protocol versions, operation registry, source policy,
and result trace.

### M1-PLAT-MINIMIZATION-v1

**Cases:** EVAL-0034.

**Inputs:** A synthetic evidence package containing three facts required by the
task, three irrelevant records, user and machine names, real-looking absolute
paths, opaque replacements, and secret-shaped canaries.

**Positive oracle:** The structured result must cite all three required facts
and use opaque evidence identities. It must omit every unrelated/sensitive
value and remain schema-valid.

**Negative controls:** Removing one required fact must cause a typed
missing-information result; marking one previously irrelevant non-secret fact
as required must admit only that fact, not its neighboring record.

**Credential extension:** Reuse M1-PLAT-CREDENTIAL-v1 to test
queued/retry/reserved/in-flight deletion states.

**Replay dependencies:** Exact minimized/unminimized packages, task schema,
redaction policy/version, expected fact set, helper/adapter versions where
enabled, and full canary scan.

### WP5 clean-layer and reuse platform slot

**Cases:** EVAL-0037 and EVAL-0039.

**Inputs:** Controlled acquisition endpoint with immutable `R1` and later
`R2`; retained `R1` body; extraction `X1`; analysis `A1`; one independent
acquisition, one child acquisition, one local file, and one in-archive
document; profile snapshots `S1` and `S2` with different applicability.

**Variants:** ordinary reuse; clean analysis; clean extraction; explicit
refresh; explicit refresh plus clean extraction/analysis; application of one
source-bound claim to both profiles; offline reuse of `R1`.

**Oracle:** Exact network-call counts, source/acquisition/extraction/application
identities, ownership and rollup edges, expected `R1`/`R2` bindings, zero
finding/readiness effect from acquisition alone, and semantic-equivalence
assertions for same-byte recomputation.

**Matched negative:** A fresh source revision that changes bytes but not the
extracted claim; it still creates a distinct source/acquisition revision and
must not be collapsed because the claim is equivalent.

**Replay dependencies:** Endpoint transcript, retained source bodies and
fingerprints, source policy, extractor/analyzer versions, local supplying
snapshot, configurations, application evidence, and cost/event ledger.

### M1-PLAT-LIFECYCLE-v1

**Cases:** EVAL-0038, EVAL-0045, and EVAL-0082.

**Inputs:** A finite DAG containing local analyzer A, local analyzer B,
attached acquisition C, independent node D, retry-safe node E, indivisible
node F, and limit-bounded node G. Work uses deterministic gates and
checkpoints.

**Variants:** every legal/illegal state transition; pause at safe and
indivisible points; resume; cancellation; failure; limit exhaustion;
invalidation; same-run retry; attempted terminal retry; worker/coordinator/CLI
crash; idle file/profile/config/credential changes; explicit duplicate
idempotency command; independent configuration toggles.

**Oracle:** Versioned transition table, expected current/terminal states,
attempt/checkpoint ownership, dispatch count, parent/child control, progress
population/denominator, gaps, reservation state, and exact effective controls.
No idle mutation may create a run or request.

**Matched negatives:** Unrelated node failure does not stop node D; a
tracing-only change does not create semantic context; a semantic assumption
change does; a duplicate client request creates no second run.

**Replay dependencies:** DAG and transition-policy versions, checkpoints,
configuration/context versions, fault schedule, coordinator epochs, command
identities, and event/usage traces.

### M1-PLAT-OUTPUT-v1

**Cases:** EVAL-0040.

**Inputs:** One completed, failed, cancelled, and limit-reached run with
findings, lead-only state, gaps, omitted restricted source material, review
state, and secret-shaped canaries.

**Oracle:** Human-readable snapshot expectations plus JSON Schema and exact
identity/provenance/sensitivity fields. A later-export simulator has a
different artifact identity and complete selection/sharing/redaction manifest.
Deletion-preview expectations enumerate every independent retained copy.

**Matched negatives:** Copying run-owned JSON does not change its sharing
class; creating a new export does not mutate the run; deleting the simulated
export does not delete its sources.

**Replay dependencies:** Run/evidence/review revisions, output/export
generator/schema versions, source policy, sharing policy, selection manifest,
and canary report.

### M1-PLAT-EXTERNAL-v1

**Cases:** EVAL-0046.

**Conditional inputs:** One manifest per exact external operation/version. Each
manifest declares executable/library hash, operation enum, closed arguments,
working directory, environment, inherited handles, expected output, allowed
cache/temp root and side effects, protected roots, process descendants, time
and resource limits, and forbidden operations.

**Baseline stand-in:** A test executable may prove the generic launcher and
canary harness, but it cannot qualify MO2, LOOT/libloot, Mutagen, or another
real adapter operation.

**Oracle:** Protected-root equivalence, exact process/operation trace,
allowlisted external-tool-private effects, staged output, coordinator
admission, and typed unsupported behavior.

**Adversarial variants:** write/apply/set/sort/save operation IDs; shell
metacharacters; substituted executable; DLL/search-path manipulation;
unexpected child; reparse/cache path into protected root; timeout/crash; and
malformed output manifest.

**Replay dependencies:** Exact binary/library and hash or reproducible
acquisition record, license/redistribution disposition, operation/adapter
version, disposable input root, before/after manifests, logs, and staged
outputs.

### M1-PLAT-OFFLINE-v1

**Cases:** EVAL-0064.

**Inputs:** Local analyzer with complete inputs; local analyzer with one
unsupported input; provider-only analyzer; fresh and stale cached source
revisions; no credential profile; blocked DNS/network; OpenAI-only capability
registry.

**Oracle:** Local completion, explicit unsupported/unavailable/stale/gap states,
zero provider/network/credential activity, exact cached revision and policy
decision, and provider-independent stored domain outputs.

**Matched negative:** Enabling a provider-dependent analyzer while offline
changes only its explicit availability/coverage state; it does not prevent the
same local analyzer result.

**Replay dependencies:** Local bytes, cached source revisions, freshness
policy, capability registry, network spy transcript, configurations, and
expected coverage populations.

### M1-PLAT-PROVIDER-CAPABILITY-v1

**Cases:** EVAL-0076.

**M1 profile:** Direct synchronous `/v1/responses`; explicit
`gpt-5.6-sol`; `reasoning.effort: medium`; strict Structured Outputs through
`text.format`; `store: false`; explicit `service_tier: "default"`;
non-streaming; no
tools, background, Batch, conversation state, persisted reasoning, Pro mode,
model alias, alternate provider, or fallback. This profile follows accepted
ADR-0025 and must be reconciled if that ADR is not accepted as written.

**Deterministic inputs:** Provider-simulator capability snapshots and Response
receipts covering:

- input/output/reasoning tokens;
- provider rate-window limit, remaining headroom, and reset;
- absent and present provider-side spend limits;
- absent, stale, delayed, and differently scoped administrative
  usage/cost history;
- local operation/run/profile budgets and reservations;
- exact rational price catalog, calculated nano-USD, and later billing
  adjustment;
- absent credit/prepaid balance; and
- hosted-search capability explicitly disabled/not exercised.

**Adversarial capability matrix:** Missing usage, missing rate headers, 429
with/without reset, malformed/negative/overflowing numeric fields, stale or
different-account aggregates, unknown currency/price/tier/context/cache class,
returned-model mismatch, alias drift, delayed adjustment, and UI/JSON attempts
to synthesize unavailable credit, amount charged, or guaranteed completion.

**Oracle:** Each authority has a separate typed field, source/scope/time,
availability state, and provenance. Missing balance/spend/history/rate fields
are `unavailable`, never `0`, unlimited, or inferred. Provider receipt usage
settles one owned actual entry; catalog cost remains a local calculation;
rate-window headroom remains non-reserved provider state; local hard limits
and reservations remain authoritative only for Infinium dispatch.

**Live qualification manifest:** One separately authorized request with:

- exact selected access profile/generation and safely available
  organization/project/account or billing-scope metadata;
- immutable capability/price snapshot and exact rendered request;
- strict trivial response schema with a deliberately tiny input and finite
  `max_output_tokens`;
- all applicable finite token/call/nano-USD/deadline limits and committed
  reservation;
- `gpt-5.6-sol`, medium effort, `store: false`, no tools, no stream, and no
  fallback;
- pre-run display/confirmation record;
- raw Response, requested/returned model, request ID where available, usage,
  service tier, rate metadata, settlement, and full canary scan; and
- explicit retained-result replay status plus the statement that a repeated
  request would be a new live execution.

The qualification request is the only authenticated/billable action permitted
while this case is incomplete. General provider dispatch stays closed until
the result passes. An unresolved dispatch or settlement keeps it closed.

**Matched negatives:** Identical local budget with unavailable provider
balance; identical receipt under different account-scope metadata; sufficient
rate headroom with insufficient local budget; and sufficient local budget with
unknown finite price/input bound. Only the properly scoped, fully bounded
combination may dispatch.

**Replay dependencies:** Exact request/response, capability and price snapshots,
rate headers, receipt and administrative fixtures, provider/model/profile
identities, local budget ledger, settlement/adjustments, output-display
artifacts, live authorization, and canary report. Secret bytes are excluded.

### M1-PLAT-PROVIDER-AUTHORITY-v1

**Cases:** EVAL-0077.

**Inputs:** Two invented OpenAI Platform access profiles, each with distinct
opaque metadata and test credential generations; one disabled old generation;
one forbidden shared/project credential sentinel; simulated ChatGPT/Codex
state that must remain unreachable; exact `gpt-5.6-sol` request profile; tiny
local limits; and deterministic success/failure/ambiguous provider responses.

**Authorization sequence:** Explicit profile selection -> usage-priced billing
disclosure -> exact maximum-bound display -> user confirmation -> atomic
reservation -> final coordinator/helper revalidation -> possible transport
start -> Response admission -> actual-usage settlement. The oracle records a
monotonic event sequence and allows no transport before the final gate.

**Adversarial matrix:** Missing confirmation; wrong profile/purpose/account
metadata; stale/disabled/deleted generation; revocation after reservation;
expired deadline; exhausted scope; unknown price class; duplicate idempotency
identity; helper/coordinator crash before/after each gate; auth/quota/rate/
network/model/scope/billing/refusal/incomplete/schema failure; returned-model
mismatch; known-undispatched cancel; ambiguous transport start; late receipt
or adjustment; and fallback attempts to the second key, ChatGPT/Codex, a model
alias, alternate provider, or forbidden shared/project key.

**Oracle:**

- zero provider transport without current explicit authorization, reservation,
  and final gate;
- exact binding to one direct Responses access profile/generation/revocation
  epoch, provider/purpose, safely available account/billing scope, request,
  model/capability/price snapshot, deadline, and limits;
- one request/attempt/reservation/Response/usage/settlement owner with
  nonduplicating rollups;
- no fallback on any failure;
- proven-undispatched cancellation releases unused reservation, while an
  ambiguous start retains a full unresolved hold and forbids automatic retry;
- dispatched/uncancellable and late usage remains visible and attributed to
  the terminal original attempt; and
- no secret or Credential Manager target in any ordinary surface.

**Live qualification manifest:** Reuse the single request defined by
M1-PLAT-PROVIDER-CAPABILITY-v1 and M1-PLAT-CREDENTIAL-v1 rather than sending an
additional call. The one request must traverse the production credential
helper, SQLite reservation/final-gate, direct adapter, admission, and settlement
paths and satisfy all three fixture-family oracles. The general provider gate
opens only after terminal settlement or another explicitly accepted
non-ambiguous outcome.

**Matched negatives:** A valid second key cannot replace the selected failed
key; ChatGPT subscription state cannot fund or authenticate the request;
sufficient provider rate headroom cannot override exhausted local budget; and
a valid reservation cannot override revoked credentials or expired deadline.

**Replay dependencies:** Non-secret access-profile/generation metadata, user
selection/confirmation, immutable request and capability/price identities,
authorization/reservation/final-gate events, provider transcript, Response and
usage receipt, settlement/hold/adjustment records, process/IPC trace, and
canary scan. The key is never retained as a replay dependency.

### WP5 lineage platform slot

**Cases:** EVAL-0079.

**Sequences:**

1. exact causal continuation with display rename;
2. unrelated snapshot change;
3. same names/participants with distinct causal conditions;
4. changed applicability;
5. changed dependency closure;
6. new contradiction with same causal locus;
7. declared-compatible and incompatible analyzer upgrades;
8. taxonomy-only reclassification;
9. stable case cause with one added/removed symptom finding;
10. same member overlap with distinct shared causes;
11. missing/deleted identity proof;
12. supported promotion of a lead-only case;
13. candidate-order permutation; and
14. reviewed disposition/suppression with valid and invalid carryover
    conditions.

**Oracle:** Preassigned occurrence identities, permitted opaque logical
continuities, per-gate evidence, explicit reconciliation outcomes, lineage
events, carryover/no-carryover events, audit gaps, and false-merge/split
metrics. The oracle IDs and expected labels remain isolated.

**Matched negatives:** Names/signatures match but cause differs; participants
change but shared cause remains; taxonomy changes while cause does not; exact
cause continues but a new contradiction invalidates review-state carryover.

**Replay dependencies:** All run occurrences, identity envelopes, analyzer
compatibility declarations, dependency closures, evidence/contradictions,
taxonomy versions, review events, policy versions, deletion receipt, and
candidate permutations.

### WP5 write-boundary platform slot

**Cases:** EVAL-0035 and EVAL-0080.

**Inputs:** One valid destination for each delivered write class plus protected
MO2/game/generated/configuration roots and outside export root.

**Adversarial path matrix:** direct descendant; `..`; absolute and relative
alias; symlink; junction; mount point; hard link where applicable; short name;
case variant; UNC/device path; alternate stream; cross-volume target; ancestor
replacement; final-target replacement; check/use race; stale capability; and
recursive deletion request.

**Oracle:** Exact accepted/rejected matrix by supported filesystem capability,
opened-handle/final-object evidence, authorized write log, deletion plan and
receipt, and unchanged protected-root manifest.

**Replay dependencies:** Filesystem/OS/version and feature inventory, root
registry, path/object identities, operation schema, race schedule, write/deletion
events, and before/after manifests.

### M1-PLAT-BUDGET-v1

**Cases:** EVAL-0081.

**Inputs:** Deterministic non-network provider simulator; exact request
fingerprints; rational price catalog; capability snapshot; operation,
acquisition, analysis, and provider/account limits; parent/child ownership;
single global live-dispatch setting.

**Reservation vectors:** dispatch count, input tokens, output/reasoning tokens,
bounded hosted-search count, and nano-USD. Include below-limit, exact-limit,
one-unit-over, maximum safe integer, overflow, unsupported price class,
unknown context band, cache-hit-unknown, and unbounded-tool variants.

**Concurrency/fault matrix:** two or more simultaneous reservation
transactions; parent/child competition; pause/cancel/delete/revocation at each
gate; coordinator epoch change; duplicate request; known abort; ambiguous
transport start; receipt below/equal/above reserve; delayed positive/negative
adjustment; clock rollback; deadline crossing; detachment cutoff; projection
rebuild.

**Oracle:** Exact integer arithmetic, one winning/rejected atomic outcome,
scope projections, reservation/actual/adjustment ownership, rollups,
unresolved holds, terminal/skipped states, and zero current debit for reuse.

**Mandatory mode:** This fixture calls the same synchronous reservation and
final-gate implementation intended for real dispatch. A mock that bypasses
SQLite admission is invalid. It performs no billable request.

**Disabled-mode manifests:** Background, Batch, explicit-cache,
cache-dependent, and concurrent-live-provider variants record
`capability-disabled; extension-not-passed`, not success.

**Replay dependencies:** SQLite transaction/event history, request/capability/
price/configuration identities, simulator receipts, deadlines/clock events,
credential-generation metadata where applicable, fault schedule, and expected
ledger/projection tables.

### WP5 persistence platform slot

**Cases:** EVAL-0087.

**Inputs:** At least one instance of every M1 authoritative object, immutable
revision, typed forward/reverse edge, shared payload owner, projection,
checkpoint, reconciliation/lineage event, review event, cost entry, output,
deletion preview, gap, and receipt. Include high-fanout dependency and shared
payload graphs within bounded M1 scale.

**Fault/corruption matrix:** staging orphan; payload orphan; missing payload;
hash/size mismatch; crash at publication; long reader/WAL pressure;
checkpoint/reset; unsupported native SQLite substitution; foreign-key
disablement; migration failure at each step; unsupported newer schema;
incomplete/corrupt backup; restore with missing/wrong payload; deletion with
live owner/backup pin; crash before/after logical deletion and physical remove;
projection corruption/rebuild.

**Oracle:** Authoritative table/edge/payload inventory, loaded native SQLite
version/hash/features, transaction boundaries, integrity and foreign-key
results, reconciliation action, backup/restore result, deletion effects,
projection checksum, replay/audit classification, and query/performance
measurements.

**Replay dependencies:** Original database snapshot, WAL state where relevant,
payload manifest/bytes, application/schema/migration/native SQLite versions,
fault schedule, backup pins, deletion plan, and expected rebuilt projections.

### WP5 IPC platform slot

**Cases:** EVAL-0088.

**Inputs:** Two concurrent coordinator candidates; generated compatible,
additive-minor, incompatible-major, unknown-enum, malformed, oversized, slow,
replayed, out-of-order, and wrong-role clients; one CLI; one general worker;
optional WPF host; deterministic result population exceeding one page.

**Oracle:** One winning fencing epoch; endpoint names/DACL/remote-rejection
inspection; handshake matrix; closed method matrix; finite message/page/chunk/
queue limits; stable keyset page sequence; resync behavior; idempotent command
records; worker bootstrap/assignment/staging/admission; process-tree cleanup;
and authoritative state after every crash/reconnect.

**Matched negatives:** Application client cannot call worker methods; worker
cannot query application data; transport cancel does not cancel run; valid
minor-version client works only within negotiated capabilities; reordered
population with the same stable keys yields the same traversal.

**Replay dependencies:** Generated protocol definitions, endpoint security
descriptor, runtime descriptor, compatibility matrix, coordinator epochs,
nonces, query data/order, cursors, event log, process/handle trace, staged
outputs, and crash schedule.

### M1-PLAT-CREDENTIAL-v1

**Cases:** EVAL-0034, EVAL-0077, and EVAL-0089.

**M1 live-provider inputs:** Unique opaque credential profile and generation IDs;
Credential Manager generic credential targets in a test-only namespace;
provider/purpose/account metadata; secret canary below the size limit;
over-limit value; deterministic provider simulator; exact endpoint/request and
budget reservation. At least one enrollment uses the native helper entry path;
test-only private-handle injection may automate the remaining fault matrix but
must not be compiled into or exposed by the production entry contract.

**Lifecycle matrix:** enroll success/cancel/fail; write-success/metadata-fail;
metadata-success/write-missing; verify fail; replacement before/after
generation activation; disable; deletion success/fail/store unavailable;
helper/coordinator restart at every intent step; restored metadata without
credential; wrong provider/purpose/profile/generation/account; queued/retry/
paused/reserved-undispatched/in-flight dispatch; auth/quota/network/scope/
billing error.

**Oracle:** Exact allowed Credential Manager calls/targets; non-secret metadata
states; generation and revocation epochs; final-gate decision; simulator
transport count; reservation release/hold; staged/admitted response; backup
contents; and full secret/target canary scan.

**Cleanup:** Close dispatch, delete every known exact test target, verify
absence, and retain a non-secret cleanup receipt. A failed cleanup is visible
and blocks reuse of that fixture namespace.

**Live extension:** Reuse the one explicitly authorized qualification request
from M1-PLAT-PROVIDER-CAPABILITY-v1 and M1-PLAT-PROVIDER-AUTHORITY-v1. It uses
deliberately tiny hard limits and the exact direct Responses
`gpt-5.6-sol`/medium/`store: false` adapter, request, capability, and price
identity. It is never part of an implicit/default synthetic run and must not
create a second live request merely to satisfy this family.

**Replay dependencies:** Non-secret intent/event metadata, helper and native
wrapper versions, target derivation algorithm, simulator transcript, budget
ledger, fault schedule, backup/restore artifacts, canary report, and cleanup
receipt. Secret bytes are deliberately not a replay dependency; restored work
requires re-entry/new generation.

## Partitioning and anti-overfitting

The first executable revision should divide each family across development and
validation variants before implementation tuning. At least one materially
independent held-out permutation is required for:

- untrusted-content encoding and authority target;
- path/reparse/race shape;
- lifecycle crash boundary;
- lineage false-merge/false-split shape;
- budget competition and receipt outcome;
- provider capability/scope availability and no-invented-balance behavior;
- provider access-profile/fallback and dispatch/settlement ambiguity;
- persistence corruption boundary; and
- IPC role/version/malformed-input shape.

If any validation or held-out result changes implementation, prompts, rules,
schemas, or thresholds, that fixture becomes development data and a materially
independent replacement is registered. Generic production behavior must be
keyed to typed structure, authority, provenance, and dependencies—not fixture
IDs, paths, canaries, names, ordering, or expected labels.

## Redistribution and retention

All base fixture content is project-created synthetic material intended for
repository redistribution under the project's accepted license posture.
Generated secrets are ephemeral and never committed. Credential targets,
machine/user names, absolute paths, dumps, crash artifacts, and raw local
diagnostics remain private test artifacts and are minimized after the required
canary/audit work. Exact third-party binaries or libraries used by a
conditional external-operation manifest follow their own accepted dependency
and redistribution decision; where redistribution is not allowed, retain a
hash, exact version, acquisition instructions, and private execution record.

Deletion of any retained dependency must update the fixture run's replayability
and audit-gap record. It must not rewrite the pre-registered oracle or mark an
unexecutable case passed.

## What these manifests do not establish

These manifests do not:

- implement the evaluation harness;
- select physical schemas, libraries, test frameworks, or numeric performance
  thresholds left to the M1 plan;
- qualify a real external tool merely because a generic stand-in passes;
- authorize authenticated or billable provider calls outside the separately
  approved, tightly bounded M1 live sequence;
- prove semantic analyzer correctness or taxonomy breadth; or
- permit fixture-specific production exceptions.
