# M1 platform and operational evaluation specifications

Status: Accepted

Accepted: 2026-07-28

Accepted by: Project owner

Last reviewed: 2026-08-10
## Purpose and authority

This document specifies the platform and operational half of the Wave F M1
evaluation baseline. It refines the following planned cases without executing
them or claiming implementation conformance:

- EVAL-0026;
- EVAL-0033 through EVAL-0035;
- EVAL-0037 through EVAL-0040;
- EVAL-0045 and EVAL-0046;
- EVAL-0064;
- EVAL-0076 and EVAL-0077;
- EVAL-0079;
- EVAL-0080 through EVAL-0082; and
- EVAL-0087 through EVAL-0089.

The [case catalog](../case-catalog.md) remains the identifier inventory.
Accepted product requirements and ADRs remain authoritative if this accepted
specification conflicts with them. Acceptance of this document would accept
test obligations and expected behavior, not pass any case, qualify an
implementation, or authorize production implementation before an accepted M1
plan.

The companion
[fixture manifests](platform-fixture-catalog.md) define the
pre-registered inputs, canaries, fault points, replay dependencies, and expected
artifacts used below.

## Shared M1 boundary

### Exercised process and authority roles

The baseline exercises:

1. one non-elevated per-user coordinator as the only database, lifecycle,
   operation-authorization, query, cost-admission, worker/helper-launch,
   result-admission, and publication authority;
2. one human-readable CLI using the versioned application contract;
3. at least one coordinator-launched general worker using the worker-only
   contract and a per-attempt staging root;
4. the one-shot credential/provider helper for the bounded live direct OpenAI
   Responses proof and EVAL-0089; and
5. the WPF/WebView2 shell only when the separately scoped M1 shell/security
   spike is exercised.

The renderer, WPF host, CLI, general worker, helper, tool, acquired content,
provider response, and staged manifest never become storage, lifecycle,
authorization, budget, or publication authorities.

### Mandatory and conditional gates

The following cases are mandatory for every M1 implementation surface named in
their scope: EVAL-0026, EVAL-0033 through EVAL-0035, EVAL-0037 through
EVAL-0040, EVAL-0045, EVAL-0064, EVAL-0076, EVAL-0077, EVAL-0079,
EVAL-0080 through EVAL-0082, and EVAL-0087 through EVAL-0089. EVAL-0076,
EVAL-0077, and EVAL-0089 are mandatory because M1 includes a bounded live
direct OpenAI Responses sequence using a reusable user-supplied Platform API
key: one transport qualification followed, only after it passes, by the two
semantic operations specified in EVAL-0067/EVAL-0083.

EVAL-0046 becomes an M1 gate for each external application, library operation,
or subprocess adapter exercised by M1. It is not satisfied by testing a generic
stand-in instead of the exact selected operation. If M1 invokes no such
operation, the case is retained as not applicable to the delivered M1 surface,
not passed.

EVAL-0081's synchronous reservation path is mandatory for the M1
budget/dispatch substrate before the bounded live proof. It is tested first
with deterministic provider simulators and concurrent reservation
transactions. Live billable concurrency, background Responses, Batch, and
cache-dependent budgeting remain disabled. Their extensions must pass before
the corresponding capability is enabled.

The only authenticated/billable action permitted before EVAL-0076,
EVAL-0077, EVAL-0089, and the applicable live extension of EVAL-0081 complete
is the deliberately tiny, explicitly approved qualification request that those
cases evaluate. General product dispatch remains closed before and during that
request and opens only after the retained request, response, usage,
cancellation/settlement state, canary results, and assertions pass. Failure or
ambiguity leaves the gate closed.

### Shared assertions

Every case records:

- fixture ID/version and development, validation, or held-out partition;
- application, schema, protocol, SQLite, analyzer, adapter, tool, provider,
  capability, price-catalog, prompt, and taxonomy versions that apply;
- exact installation snapshot, analysis context, effective scan configuration,
  resolved input manifest, owner run, job node, and attempt identities;
- pre-registered expected observations, outputs, abstentions, gaps, lifecycle
  states, coverage states, and replayability;
- deterministic event order or a retained partial-order trace where races are
  intentional;
- all injected faults and whether the fault was observed;
- protected-root and secret-canary results;
- expected and actual authoritative rows, payloads, output manifests, logs,
  diagnostics, process launches, network requests, and filesystem effects; and
- a machine-readable assertion report plus a human-readable explanation.

Expected answers and canary values remain outside the path under test.
Development runs retain raw typed outputs without maturity or preset
suppression.

### Shared safety canaries

The following canaries apply wherever their surface is reachable:

- a byte-, metadata-, identity-, ACL-, and reparse-aware protected-root
  manifest for disposable MO2, selected profile, mods, game, configuration,
  and generated-output roots;
- unique secret values and Credential Manager target-name canaries that must
  not appear in ordinary database/payload rows, IPC, command lines,
  environments, prompts, traces, stdout/stderr, run-owned JSON, diagnostics,
  exports, crash artifacts, or worker staging;
- untrusted instruction tokens that must remain inert and must not alter
  source, analysis, operation, or authorization policy;
- forbidden path, URL, command, argument, endpoint, method, cursor, and
  operation identifiers;
- a publication canary staged by a stale or unauthorized worker that must
  never become authoritative; and
- network and process spies that fail the case if non-allowlisted activity
  occurs.

Access-time-only filesystem changes are excluded from the protected-root
comparison where the supported filesystem can update them on read. Directory
membership, bytes, file identity, creation/write time, attributes, ACLs,
reparse targets, alternate streams, and relevant MO2/game configuration must
remain unchanged.

## Case specifications

### EVAL-0026 — Run immutability

**Purpose and traceability:** Prove SNAP-002, SCAN-009, and INTENT-004 under
ADR-0015, ADR-0016, ADR-0018, and ADR-0019: an active run owns immutable
resolved bindings even when mutable source objects are edited.

**Scope:** A queued/running run bound to installation snapshot `S1`, analysis
context `C1`, effective scan configuration `G1`, and resolved manifest `M1`.
While a fenced job is running, independently edit the saved configuration to
`G2`, edit an assumption/mapping input to context `C2`, and mutate one relevant
fixture file to create snapshot candidate `S2`.

**Expected states and outputs:**

- the active run continues to identify only `S1/C1/G1/M1`;
- saved configuration and assumption edits create new immutable versions;
- the detected relevant physical change invalidates only affected active work,
  producing a typed `invalidated-by-changed-input` terminal state or an
  affected-stage gap according to the pre-registered stage policy;
- no active-run row, dependency edge, checkpoint, candidate, finding, or
  output is rebound to `S2`, `C2`, or `G2`;
- unaffected staged work may finish only under the original bindings, while
  any continuation against new inputs requires a new manually initiated run
  and explicit validated reuse edges; and
- run-owned CLI/JSON output reports the edit, old and new identities, affected
  dependencies, terminal/gap consequence, and replay state.

**Adversarial variants:** edit immediately before and after job claim,
checkpoint commit, staged-output admission, and terminal publication; resend an
old edit command/idempotency key; and attempt direct database mutation from the
CLI/worker.

**Pass/fail:** Pass only if every authoritative artifact remains immutably
bound and each race has one legal, inspectable outcome. Any in-place identity
change, mixed-state evidence, unrecorded invalidation, or implicit continuation
fails.

**Does not prove:** general snapshot reconstruction correctness, arbitrary
cross-run carryover, or readiness validity.

### EVAL-0033 — Untrusted-content isolation

**Purpose and traceability:** Prove SEC-001 under ADR-0013 and ADR-0017 through
ADR-0021.

**Scope:** Feed the same hostile instruction corpus as retrieved HTML converted
to the supported inert representation, local documentation, mod metadata,
tool output, model output, log text, worker diagnostics, IPC text fields, and
binary/static-parser inputs. Exercise the WebView2 path only when its M1 spike
is present.

**Expected states and outputs:**

- content is retained as typed untrusted data with origin and fingerprint;
- embedded instructions cannot change prompts outside the bounded task
  template, source registry, scan configuration, authority classification,
  operation allowlist, provider/model selection, budget, or admission policy;
- no hidden tool, shell, SQL, filesystem, navigation, credential, provider,
  arbitrary-network, or publication capability becomes reachable;
- active HTML/script, DLL loading, PEX/SWF execution, remote frames/resources,
  downloads, permissions, new windows, and unexpected navigation remain
  disabled or rejected on exercised surfaces;
- attempts become typed security rejections or inert evidence, not findings
  that claim the instruction executed; and
- no secret or protected-root canary is disclosed or modified.

**Adversarial variants:** nested/encoded instructions, oversized and malformed
markup, Unicode/confusable operation names, tool-result role spoofing,
instruction text in filenames/record strings, prompt-boundary imitation,
search-result instructions, and content requesting its own authority.

**Pass/fail:** Any authority, policy, secret, network, navigation, process, or
write effect attributable to content fails. A parser crash is also a failure
unless it is safely isolated and reported as a bounded gap without authority
escape or unrelated-run loss.

**Does not prove:** that all future parsers are memory-safe, that same-user
malware is contained, or that the semantic model is correct.

### EVAL-0034 — Credentials and context minimization

**Purpose and traceability:** Prove SEC-002, SEC-004, AI-003, and AI-004 under
ADR-0018 through ADR-0021 and ADR-0023.

**Scope:** Build a task whose useful answer requires selected non-secret local
facts but not usernames, original absolute paths, unrelated records, secret
bytes, or Credential Manager target names. For the no-auth baseline, inject
secret-shaped canaries only into test storage/transport boundaries. When the
credential helper is implemented, use the EVAL-0089 exact-target fixture and
exercise disable/deletion across queued, retrying, reserved-undispatched, and
in-flight states.

**Expected states and outputs:**

- the bounded task context contains all pre-registered necessary non-secret
  facts and omits every unnecessary field;
- redacted/tokenized path references remain resolvable to evidence through
  host-side opaque identities without exposing the original path;
- canary values and target names occur only in the authorized helper/native
  credential boundary and test oracle;
- confirmed disable/deletion prevents every new, queued, retry, paused, and
  reserved-undispatched transport start;
- an unused reservation is released only after undispatched status is proven;
- a request already past the transport-start boundary remains disclosed as
  in-flight/uncancellable or follows the adapter's qualified cancellation
  behavior, with its hold and eventual usage preserved; and
- logs, traces, outputs, diagnostics, crash artifacts, and exports contain
  typed redaction/omission markers where useful, never the canary.

**Pass/fail:** A missing required fact, unnecessary sensitive context, secret
leak, stale-generation dispatch, premature reservation release, hidden
in-flight work, or account/provider fallback fails.

**M1 live-provider gate:** The context-minimization and secret-free
ordinary-surface variants run first. Credential lifecycle and live-dispatch
variants then run with EVAL-0089 and are mandatory for M1's bounded direct
OpenAI Responses proof. They do not authorize any broader provider operation.

**Does not prove:** resistance to an administrator/debugger/same-user malware,
provider-side revocation, or perfect erasure of immutable managed strings.

### EVAL-0035 — Privileged-operation boundary

**Purpose and traceability:** Prove AUTH-002 and SEC-003 under ADR-0018,
ADR-0019, and ADR-0021.

**Scope:** Attempt every supported operation with a valid request and with
out-of-scope paths, URLs, executables, commands, arguments, methods, roles,
nonces, cursors, message sizes, environments, inherited handles, and
destination classes.

**Expected states and outputs:**

- only closed-schema, allowlisted operations over opaque or typed relative
  identities are accepted;
- arbitrary SQL, path, URL, shell, generic object lookup, credential target,
  provider request, tool invocation, or publication request is unreachable;
- final path/object authorization rejects traversal, symlink, junction, mount,
  hard-link, short-name, case, device, alternate-stream, replacement, and
  check/use race variants into protected roots;
- subprocess launch uses an exact absolute executable, no shell, closed
  arguments/environment, explicit working directory/handles, and bounded
  behavior; and
- every rejection is typed and audit-visible without revealing secrets or
  mutating protected state.

**Pass/fail:** Any forbidden operation reaching an OS/provider/tool boundary,
any caller-selected primitive bypass, fail-open parse/version behavior, or
protected-root change fails.

**Does not prove:** that Job Objects are a sandbox or that an unqualified tool
is safe.

### EVAL-0037 — Clean analysis versus source freshness

**Purpose and traceability:** Prove SCAN-007 and DOC-011 under ADR-0010,
ADR-0015, ADR-0016, and ADR-0023.

**Scope:** Use one retained source revision `R1`, extracted claims `X1`, and
derived analysis `A1`, while a controlled live endpoint exposes changed
revision `R2`. Run four configurations: ordinary reuse, clean analysis only,
clean extraction only, and explicit refresh plus clean extraction/analysis.

**Expected states and outputs:**

- ordinary reuse consumes dependency-valid `X1/A1` with reuse provenance;
- clean analysis bypasses `A1` but consumes `X1/R1` and performs no source
  request;
- clean extraction bypasses `X1` but re-extracts from the exact retained
  `R1` bytes and performs no source request;
- only explicit refresh acquires and fingerprints `R2`, creates a new
  acquisition/source revision, and permits downstream work to bind to it;
- each run retains its effective cache/freshness configuration, producing
  identities, reuse edges, network-call count, cost attribution, and
  replayability; and
- semantically equivalent clean/incremental results over the same resolved
  bytes compare equal at the typed assertion level without requiring byte-
  identical LLM prose.

**Pass/fail:** Any implicit network refresh, reuse of the selected clean layer,
source-byte substitution, false cache-equivalence failure caused by `R2`, or
missing provenance fails.

**Does not prove:** that `R2` is authoritative, that all caches invalidate
correctly, or that an LLM rerun is byte-deterministic.

### EVAL-0038 — Durable job lifecycle

**Purpose and traceability:** Prove SCAN-006 and AI-004 and validate the
M1-bounded backend lifecycle/checkpoint subset toward SCAN-005 under ADR-0016,
ADR-0018, ADR-0019, and ADR-0023. This is not a complete SCAN-005 delivery
claim.

**Scope:** Exercise the complete M1 state/transition table, safe checkpoints,
pause/resume, terminal cancel/fail/limit/invalidation, retry attempts,
coordinator and worker restart, attached child control, and node-scoped
failure. User-facing child detachment may remain disabled, but its identity and
attribution fields must not be collapsed.

**Expected states and outputs:**

- requested and observed transitions are append-only and current state is
  reconstructible;
- pause stops new dispatch, propagates to attached children by default, and
  reaches `paused` only at declared safe boundaries;
- same-run resume keeps identical run inputs/configuration and uses only valid
  checkpoints;
- cancellation and all other terminal states never reopen;
- retry inside an active run creates a new attempt; work from a terminal run
  continues only in a new user-initiated run with validated reuse;
- child/node failure or limit exhaustion leaves unrelated allowed parent work
  active and reports explicit skipped/gap populations;
- stale epochs/attempts cannot dispatch or publish; and
- progress denominators, retries, reused units, gaps, in-flight ambiguity,
  reservations, and terminal reasons remain accurate after recovery.

**Fault points:** before/after transition request, lease/fence commit, dispatch,
checkpoint stage/commit, output stage/admission, reservation/final gate,
transport-start marker, receipt, and terminal publication.

**Pass/fail:** Any illegal transition, reopened terminal state, mutated run
binding, double dispatch/publication, hidden continuing child, lost reservation,
retry-inflated denominator, or unrelated-work cancellation fails.

**Does not prove:** multi-host orchestration, arbitrary durable code replay,
reboot/upgrade recovery unless explicitly added, or calibrated ETA.

### EVAL-0039 — Acquisition and application provenance

**Purpose and traceability:** Prove DOC-002 and DOC-011 under ADR-0001,
ADR-0002, ADR-0015, and ADR-0016.

**Scope:** Run one independent external acquisition, one scan-configured child
acquisition, and one local/in-archive document extraction. Reuse admitted
source-bound claims in two analysis runs with different profile snapshots and
applicability.

**Expected states and outputs:**

- each acquisition retains one immutable acquisition-run owner, request,
  initiation/parent link, configuration, resolved source/entity/version,
  adapter/extractor/provider/model calls and outputs, coverage, cost, audit,
  and replay state;
- local documents additionally retain the supplying installation snapshot;
- consuming analyses add application links and local applicability evidence
  without rebinding or copying acquisition ownership;
- acquisition alone creates no profile finding or readiness effect;
- attached acquisition cost rolls up by reference once, while independent
  reuse has zero current acquisition debit and exposes original cost; and
- differences between source/extraction correctness and local applicability
  remain typed.

**Pass/fail:** Profile-owned source claims, missing acquisition identity,
duplicated ownership/cost, findings created by acquisition alone, rewritten
source provenance, or lost local-snapshot provenance fails.

**Does not prove:** source authority, citation correctness, or universal claim
applicability.

### EVAL-0040 — Run-owned output and later export separation

**Purpose and traceability:** Prove SEC-004 and OPS-003 under ADR-0015,
ADR-0018, and ADR-0021.

**Scope:** Complete, fail, cancel, and limit one run each. Emit M1 human-readable
CLI output and versioned JSON into fixed product-controlled storage. Use a
fixture-only later-export simulator to verify that an explicitly created export
has a distinct identity and selection manifest; this does not deliver M2 export
UX.

**Expected states and outputs:**

- each run-owned output identifies its run, snapshot, context, effective
  configuration, resolved inputs, schema/generator version, creation time,
  coverage, gaps, replay state, and potentially-sensitive classification;
- run-owned output is not marked externally shareable;
- a simulated later export records exact selected revisions, filters, sharing
  class, configuration, omissions, source-policy decisions, and redactions;
- restricted material is omitted or replaced by a permitted
  citation/fingerprint/omission marker in externally-shareable simulation;
- creating, deleting, or regenerating output/export artifacts does not mutate
  source run, finding, case, readiness, evidence, or review state; and
- independently retained copies are visible to deletion preview.

**Pass/fail:** Missing provenance/sensitivity labeling, secret disclosure,
restricted content in an externally-shareable artifact, conflated output/export
identity, caller-selected protected destination, or source mutation fails.

**Does not prove:** M2/M3 export UX, completeness of public redaction policy, or
M4 diagnostic-bundle redistribution approval.

### EVAL-0045 — Manual initiation

**Purpose and traceability:** Prove SCOPE-004 under ADR-0014, ADR-0016,
ADR-0018, ADR-0019, and ADR-0023.

**Scope:** Change watched fixture files, profile selection, source availability,
saved configuration, assumptions, credentials, and application lifecycle while
the coordinator is idle. Then issue one explicit user-initiated parent command
whose immutable configuration includes approved child stages.

**Expected states and outputs:**

- idle changes may update passive status or accepted LOOT managed-data
  maintenance only; they create no analysis/acquisition run, provider
  reservation, general documentation/Nexus/search request, LLM request, or
  finding;
- shell/CLI connect, reload, disconnect, restart, and coordinator recovery do
  not start, pause, cancel, or resume work;
- the explicit command creates exactly one parent operation under its
  idempotency key;
- only configured children start, each retaining initiation/parent provenance;
  and
- an indeterminate client response is reconciled by durable command identity,
  not blind resubmission.

**Pass/fail:** Any unsolicited analysis-related network, paid, analysis,
acquisition, or finding work; a duplicate run; or an unconfigured child fails.

**Does not prove:** future continuous monitoring, calendar scheduling, or
product-initiated game/MO2 launch.

### EVAL-0046 — External-tool non-mutation

**Purpose and traceability:** Prove AUTH-001 and AUTH-003 under ADR-0003,
ADR-0011 where LOOT is exercised, ADR-0018, and ADR-0021.

**Scope:** For each exact selected external application/library operation,
execute its pinned version and closed adapter request against disposable
protected MO2, profile, mods, game, configuration, and generated-output roots.
Capture process tree, arguments, environment, handles, tool cache/temp effects,
and before/after protected-root manifests. Negative variants attempt every
known write/apply/set/sort/save path and malformed argument.

**Expected states and outputs:**

- only the pre-registered read-only operation is reachable;
- every executable/library/version/input/configuration and output is recorded;
- protected setup roots remain byte-, identity-, metadata-, ACL-, and
  reparse-equivalent under the shared canary contract;
- approved tool-owned cache/temp effects occur only in the declared isolated
  location and are fully inventoried;
- unsupported or mutation-requiring behavior becomes a typed capability gap;
  and
- worker staging remains non-authoritative until coordinator validation.

**Pass/fail:** Any protected-root mutation, reachable forbidden API/command,
undeclared side effect, shell/path fallback, or fabricated safe result fails
that exact operation and prevents its use.

**Conditional gate:** Repeat the full case for every external operation and
version actually delivered. User installation prevalence or upstream
reputation cannot substitute for execution.

**Does not prove:** semantic correctness of tool results, safety of untested
versions/operations, or hostile-code containment.

### EVAL-0064 — Offline and provider boundary

**Purpose and traceability:** Prove AI-001, AI-002, and OPS-001 under ADR-0013,
ADR-0017, ADR-0018, and ADR-0020.

**Scope:** Run the configured local-only proof with network disabled, no
credential profile, an unavailable OpenAI adapter, stale cached external
evidence, and a mixed configuration containing explicitly unavailable
provider-dependent analyzers.

**Expected states and outputs:**

- every local component whose inputs are available completes without a provider
  credential or network request;
- provider/network/source-dependent components become explicit unavailable,
  unsupported, stale, skipped-by-configuration, or completed-with-gap states
  as applicable;
- cached evidence reports exact revision/freshness and is used only when the
  semantic context permits it;
- provider-specific capability data remains adapter provenance and does not
  enter provider-independent evidence/finding/case/readiness truth;
- OpenAI may be the only configured LLM adapter, and absent later-provider
  parity does not fail the run; and
- no credential enrollment prompt, fallback provider/account, or project key
  appears.

**Pass/fail:** Local work blocked solely by absent provider state, hidden
coverage loss, network/credential use, invented capability, or provider-specific
domain truth fails.

**Does not prove:** offline availability of uncached external sources or
semantic equivalence between different providers.

### EVAL-0076 — Provider capabilities and cost authorities

**Purpose and traceability:** Prove SCAN-003 and AI-005 under ADR-0013,
ADR-0020, and ADR-0023 while respecting ADR-0024's rejection. The accepted M1
execution profile follows ADR-0025 unless it is superseded through a reviewed
decision and plan amendment: direct synchronous `/v1/responses`, explicit
`gpt-5.6-sol`, `reasoning.effort: medium`, strict `text.format`,
`store: false`, explicit `service_tier: "default"`, non-streaming, and no tools,
background, Batch, conversation state, alias, alternate provider, or fallback.

**Scope:** Qualify the exact selected OpenAI access profile and capability/
price snapshot through deterministic adapter fixtures, then one explicitly
approved tiny live request. Capture what the exact credential and endpoint
actually expose before, during, and after the request. Hosted search remains
disabled for this M1 profile; its call count and price class are represented as
disabled/not exercised, not inferred from absence.

**Required separation:** The retained capability projection and user-facing
CLI/JSON must keep distinct:

- requested and returned model identity;
- input, output, and reasoning token usage from the Response receipt;
- model-dispatch and enabled priced-tool call counts;
- provider-reported rate-window limit, remaining headroom, and reset metadata;
- provider-side configured spend limit, if the selected credential exposes it;
- provider historical usage/cost aggregates and their scope/time/billing
  latency, if separately exposed;
- local request/run/provider-profile hard limits and remaining reservations;
- the finite worst-case token/tool/catalog-money bound used for dispatch;
- versioned catalog-calculated cost and its price/model/tier/context identity;
- provider billing observations or later adjustments; and
- prepaid credit/balance.

Unavailable fields remain typed `unavailable` with a capability reason.
Infinium shall not broaden credential purpose merely to obtain administrative
usage data, derive a credit balance from rate headroom or historical cost,
label catalog calculation as amount charged, or imply that current provider
headroom/spend state guarantees completion.

**Expected states and outputs:**

- pre-run output names the direct usage-priced Platform API access mode,
  selected access profile/generation, explicit `gpt-5.6-sol` profile,
  configured finite request/run limits, worst-case reservation, locally
  calculated estimate, and uncertainty;
- request and response bind the exact capability/price snapshot, requested and
  returned model, `store: false`, request ID where available, service tier,
  usage receipt, rate metadata, and billing-reconciliation limitation;
- the provider Response usage receipt settles one owned ledger entry without
  being rewritten as exact provider billing;
- missing provider spend/history/balance fields remain unavailable rather than
  zero, unlimited, inferred, or copied from another scope;
- rate-limit or administrative data from a different account, project,
  organization, time window, or credential purpose cannot be attached to this
  operation;
- the retained result is replayable as a retained boundary result; repeating
  the request is a new live execution because the selected Sol identity has no
  documented date pin; and
- local-only operation remains available if any provider capability is
  unavailable.

**Adversarial variants:** Missing/unknown usage fields, absent rate headers,
429 with and without reset metadata, stale administrative aggregates,
incompatible currency or price class, returned model mismatch, alias drift,
unsupported service tier/context band, malformed numeric fields, negative or
overflowing values, and an attempt to present unavailable credit as zero or
remaining spend.

**Pass/fail:** Conflating any authority/scope, inventing a balance or billing
fact, using stale/different-scope data as current authority, omitting estimate
uncertainty, dispatching with an unqualified finite-bound gap, or silently
changing model/mode fails. A provider capability may be unsupported without
failing local analysis, but its absence must remain explicit and may block the
affected provider operation.

**Live qualification gate:** The sole pre-pass live request is the explicitly
approved qualification action described by the shared gate. It uses a
deliberately tiny budget and cannot enable general dispatch until its retained
capability, usage, rate, price, settlement, and redaction assertions pass.

**Does not prove:** exact provider-billed dollar cost, credit-balance
availability, future rate-window headroom, hosted-search accounting, a
date-pinned model, or behavior of another model/tier/account/provider.

### EVAL-0077 — User-owned provider billing authority

**Purpose and traceability:** Prove AI-004 and AI-007 under ADR-0013,
ADR-0020, and ADR-0023, with ADR-0024's Codex/ChatGPT-plan proposal remaining
rejected. The accepted exact M1 request profile is the ADR-0025 profile
described in EVAL-0076.

**Scope:** Exercise enrollment, explicit profile selection, pre-run
confirmation, reservation, final helper dispatch, response admission,
cancellation/ambiguity handling, and settlement for one user-supplied OpenAI
Platform API-key generation. Test all failure/fallback variants with the
deterministic provider simulator before the one deliberately tiny live
qualification request.

**Exact binding and authorization:**

- the user explicitly selects and confirms one opaque OpenAI Platform access
  profile and active credential generation for usage-priced API billing;
- the immutable operation binds provider, direct Responses execution surface,
  profile/generation/revocation epoch, purpose, safely available
  organization/project/account or billing-scope metadata, explicit
  `gpt-5.6-sol` capability/price snapshot, exact request/schema/settings,
  `store: false`, deadline, and every applicable hard limit;
- the helper alone resolves the exact Credential Manager target and places the
  key on the exact qualified OpenAI provider request;
- the coordinator revalidates current user authorization, generation,
  revocation, deadline, request identity, and atomic reservation immediately
  before transport; and
- absent provider account/billing identifiers are recorded as unavailable.
  The selected local access profile remains exact, but Infinium shall not claim
  a provider-confirmed billing identity without provider evidence.

**Expected states and outputs:**

- no authenticated or billable transport starts before the explicit
  confirmation, committed reservation, and final gate;
- the live qualification request is direct, synchronous, non-streaming,
  `store: false`, strict-schema `gpt-5.6-sol` at medium effort, with no tool,
  background, Batch, conversation, alias, provider, account, credential, or
  model fallback;
- ChatGPT subscription/Codex state and credentials are never requested, reused,
  or represented as funding the Platform API call;
- one provider request maps to one attempt, reservation group, Response/request
  provenance record, actual-usage entry, and settlement, with non-owning
  rollups to the applicable operation/run/profile scopes;
- auth, quota, rate, network, model, scope, billing, refusal, incomplete, and
  schema failure remain attributed to the exact selected profile and never
  authorize another key/account/provider/model or a project/shared credential;
- if transport is proven undispatched, cancellation/revocation releases the
  unused reservation; if transport may have started, cancellation/timeout/
  disconnect retains an unresolved full hold until a qualified receipt or
  explicit later reconciliation and forbids automatic retry;
- dispatched but uncancellable work remains visible after pause, cancellation,
  deadline, or credential deletion, and eventual usage/overrun/adjustment
  settles against the original attempt without reopening the run; and
- ordinary state, IPC, logs, prompts, traces, outputs, and exports contain no
  key or Credential Manager target canary.

**Adversarial variants:** No confirmation, stale or disabled generation,
revocation after reservation, wrong purpose/profile/account metadata, expired
deadline, exhausted local scope, unknown price class, ambiguous transport
start, duplicate idempotency identity, helper/coordinator crash at every gate,
auth/quota/rate/network failure, returned-model mismatch, and attempted fallback
to another key, ChatGPT/Codex, model alias, provider, or shared/project key.

**Pass/fail:** Any call without current explicit user authorization, any
cross-profile/account/provider/model fallback, use of a project/shared key,
incorrect ownership/attribution, secret leakage, retry after ambiguous
dispatch, premature hold release, or hidden in-flight/late cost fails and keeps
general provider dispatch closed.

**Live qualification gate:** The qualification harness may authorize exactly
one tiny billable request as the action under test. It must show the selected
usage-priced mode and maximum local bound before confirmation, use the same
production helper/reservation/final-gate path, and retain the terminal or
explicitly unresolved settlement state. No general scan/analyzer call is
enabled until this case and EVAL-0076, EVAL-0081's applicable live extension,
and EVAL-0089 pass.

**Does not prove:** provider-side key revocation, exact provider-billed dollar
cost, general affordability, ChatGPT-plan access, another account/provider,
concurrent/background/Batch/cache-dependent dispatch, or permanent suitability
of Sol as a production default.

### EVAL-0079 — Finding and case lineage

**Purpose and traceability:** Prove FIND-006 and FIND-014 under ADR-0015,
ADR-0016, and ADR-0022.

**Scope:** Pre-register generic positive, matched-negative, boundary, deletion,
and metamorphic sequences covering exact continuation, display rename,
unrelated change, same names with distinct causes, changed applicability,
changed dependencies, new contradiction, compatible/incompatible analyzer
version, taxonomy reclassification, stable shared cause with changed case
membership, missing proof, lead promotion, and candidate-order permutation.

**Expected states and outputs:**

- every run emits immutable finding/case occurrence IDs and versioned identity
  envelopes; opaque logical IDs are distinct from signatures;
- only a unique fully proven one-to-one causal, applicability, dependency, and
  producer-contract match auto-reconciles;
- outcomes use the accepted explicit vocabulary, including
  `exact-continuation`, `analytical-revision`, `related-follow-up`,
  `new-distinct`, `ambiguous`, `unknown`, `not-observed`, and
  `not-evaluated`;
- display prose, names, taxonomy, severity, confidence, symptoms, or participant
  overlap alone never grant identity;
- case continuity follows member-finding and independent shared-cause proof;
- lead promotion creates a successor supported-case occurrence and retained
  `promotes-lead` lineage without relabeling history;
- no disposition/suppression carries implicitly; any exercised carryover is a
  separate event satisfying the stricter proof and provenance contract; and
- deletion of proof preserves historical decisions, creates an audit gap, and
  prevents new reconciliation/carryover from the missing proof.

**Pass/fail:** Any false merge, destructive history rewrite, name/hash-only
identity, silent false split, implicit suppression/disposition carryover,
lead relabeling, or candidate-order-dependent outcome fails. Report
auto-match precision/coverage, false-merge/split counts, ambiguity causes, and
carryover precision separately.

**M1 limit:** Interactive ambiguity review and reviewed merge/split/correction
workflows remain M2 extensions; M1 must persist append-only schema support and
must not simulate automatic review.

**Does not prove:** broad cross-analyzer identity beyond declared compatible
contracts or the usability of the later lineage UI.

### EVAL-0080 — Product-write isolation

**Purpose and traceability:** Prove AUTH-001, AUTH-002, and SEC-003 under
ADR-0015, ADR-0018, and ADR-0021.

**Scope:** Exercise every delivered settings, OS-credential, database,
payload/cache, attempt-staging, history/checkpoint, trace, run-output,
deletion, and update-staging write class. User-selected exports and production
update staging are tested only if delivered. Target direct and aliased paths
into every protected setup root.

**Expected states and outputs:**

- each write has one fixed class, coordinator authorization, typed object or
  relative artifact identity, approved root, and audit record;
- handle-bound final-object checks reject all shared adversarial path variants
  and stale capabilities;
- no caller supplies an arbitrary recursive deletion/copy/move path;
- direct product writes remain within product-controlled or OS-backed storage;
- explicit user-selected destinations, if delivered, reject protected roots;
- deletion touches only the version-bound selected graph and records a receipt;
  and
- protected-root canaries remain unchanged through success, rejection, crash,
  and race variants.

**Pass/fail:** Any unclassified write, path-string-only authorization,
protected-root effect, arbitrary destination, stale-capability success,
unplanned cascade, or missing audit record fails.

**Does not prove:** future installer/update correctness, administrator
resistance, or write safety on unsupported filesystems.

### EVAL-0081 — Atomic budget enforcement

**Purpose and traceability:** Prove AI-004 and validate the M1-bounded exact
usage/cost-accounting and cancellation/reservation subset toward SCAN-004 and
SCAN-005 under ADR-0016, ADR-0020, and ADR-0023. It does not prove the full
hierarchical progress/ETA or user-control contract.

**Mandatory M1 synchronous path:** M1 must exercise the real coordinator-owned
transactional reservation and final-dispatch-gate code path with deterministic
non-billable provider simulators. It shall not replace the transaction with a
single-threaded shortcut merely because live M1 billable dispatch is limited
to one globally in-flight request.

**Scope:** Concurrently submit competing parent/child reservation requests
against operation, acquisition-run, analysis-run, and local
provider-profile/account limits. Use exact immutable requests, capability and
price snapshots, finite dispatch/input/output/tool/nano-USD bounds, and
wall-elapsed deadlines. Exercise reservation, final authorization, settlement,
and projection rebuild without sending a billable request.

**Expected states and outputs:**

- one short transaction atomically checks and reserves the full worst-case
  vector against every applicable scope or rejects it without partial debit;
- the final gate separately revalidates fences, run/node eligibility,
  deadline, pause/cancel/delete state, selected credential generation when
  applicable, and prior/ambiguous transport start;
- a single usage event has one attempt owner and appears once in each applicable
  rollup by reference;
- child exhaustion stops only affected work; unaffected parent/local work may
  continue under its own immutable limits;
- historical reuse creates no request, reservation, or current debit;
- cancellation or expiry releases only proven-undispatched reservations;
  ambiguous transport start retains the full unresolved hold and forbids
  automatic retry;
- receipts below, equal to, and above reservation settle visibly; overrun
  exhausts scope without creating authority;
- exact rational pricing, component-wise upward rounding, signed 64-bit
  nano-USD checked arithmetic, price/model/tier/context/cache-class gaps, clock
  rollback, deadline crossing, and delayed adjustments fail conservatively;
  and
- rebuilding projections from the immutable ledger produces the same available
  headroom and unresolved holds.

**Fault/adversarial points:** every transaction boundary; competing commits;
coordinator crash after reserve, before/after final gate, after possible
transport start, before receipt, and during settlement; stale epoch; duplicate
idempotency identity; overflow; negative/late adjustment; revocation after
reservation; and detachment-sequence cutoff.

**Live-mode exclusions:** Background Responses, Batch, explicit/cache-dependent
budgeting, concurrent live billable attempts, provider-admin reconciliation,
automatic period resets, non-USD limits, and unbounded tools remain disabled.
Their capability-specific extensions must pass before enablement. Before any
live authenticated or paid transport, the synthetic/pre-dispatch portions of
EVAL-0076, EVAL-0077, EVAL-0089, and this case must pass. The single explicitly
approved qualification request then completes their live
request/usage/price/cancellation assertions. No subsequent provider call may
dispatch unless those retained live assertions pass.

**Pass/fail:** Any oversubscription, partial reservation, double ownership,
premature release, dispatch after an invalid gate, hidden unresolved variance,
floating/overflow arithmetic error, current debit for reuse, or projection
drift fails.

**Does not prove:** provider-billed dollar accuracy, provider-side spend-limit
authority, or safety of disabled modes.

### EVAL-0082 — Independent M1 development controls

**Purpose and traceability:** Prove SCAN-002, SCAN-009, and EVID-007 under
ADR-0015 through ADR-0019 and ADR-0023.

**Scope:** Through CLI and versioned configuration artifacts, vary one control
at a time and in combinations across analyzers, sources, budgets, cache/source
policy, tracing, candidate breadth, thresholds, provider/model where enabled,
concurrency/resources, and effective semantic-context overrides.

**Expected states and outputs:**

- analyzer, source, budget, cache, and tracing controls remain independently
  settable rather than being hidden behind one preset;
- startup resolves a versioned saved artifact into distinct immutable effective
  scan configuration and semantic analysis context;
- every effective value and its source/default/override is retained with the
  run;
- enabled analyzers emit raw typed candidates, evidence, failures, abstentions,
  findings/leads, and gaps without maturity/preset suppression;
- disabling one analyzer/source reports skipped-by-configuration coverage and
  does not disable unrelated work;
- tracing/cache/concurrency-only change does not manufacture a new semantic
  context; an actual semantic override does; and
- CLI human-readable and JSON output agree on effective controls and effects.

**Pass/fail:** Coupled controls, unretained defaults, hidden enabled-analyzer
output, preset/maturity filtering, semantic/operational context conflation, or
unreported skipped scope fails.

**Does not prove:** user-friendly M2 preset design, calibrated defaults, or
release maturity thresholds.

### EVAL-0087 — Persistence integrity and recovery

**Purpose and traceability:** Prove SNAP-004, SNAP-006, OPS-002, and OPS-004
under ADR-0010, ADR-0015, ADR-0016, ADR-0022, and ADR-0023.

**Scope:** Exercise every M1 authoritative object and typed edge, content-
addressed payload ownership, append-only history, projection, checkpoint,
reconciliation/lineage record, budget record, and deletion/audit gap. Inject
crashes and corruption around SQLite and payload-store boundaries.

**Expected states and outputs:**

- authoritative publication is atomic with all required outputs, provenance,
  dependencies, coverage, gaps, payload references, and accounting ownership;
- payload staging verifies hash/size and uses atomic same-volume placement
  before transactional registration;
- recovery classifies orphan staging, orphan object, missing object, and
  hash/size mismatch without manufacturing or rebinding evidence;
- one patched, pinned, asserted native SQLite build owns authoritative storage;
  foreign keys and required features remain enabled;
- immutable history survives WAL/checkpoint pressure, process crash, migration
  failure, backup/restore, graph-aware deletion, and projection rebuild;
- unsupported-newer schema refuses to open; failed migration restores or
  leaves the original consistent store;
- backup manifest and restore verify database integrity, foreign keys,
  referenced payloads, hashes, and compatible versions;
- shared payload deletion preserves live owners and backup pins; logical
  deletion and receipt precede permitted physical removal; and
- replayability/audit gaps distinguish complete, partial, audit-only, and
  unavailable outcomes after loss.

**Fault points:** before/after payload stage, rename, relational registration,
stage completion, WAL checkpoint/reset, each migration step and swap,
backup manifest/snapshot, restore verification, deletion preview/receipt/file
removal, reconciliation, and projection rebuild.

**Pass/fail:** Published partial state, lost append-only fact, silently rebound
payload, unpinned/affected SQLite use, unverified restore, live shared-payload
removal, fabricated replayability, or non-rebuildable projection fails.
Representative M1 query plans, latency, WAL growth, disk use, backup, and
restore are measured and reported; thresholds belong to the M1 plan.

**Does not prove:** M3 high-end scale, all future domain tables, automatic
retention expiry, or multi-user/multi-authority storage.

### EVAL-0088 — Process, IPC, and query contract

**Purpose and traceability:** Prove AUTH-002 and SEC-003 and validate the
M1-bounded lifecycle/progress-query substrate toward SCAN-004 and SCAN-005
under ADR-0016, ADR-0018, ADR-0019, and ADR-0021. It does not prove the
complete user-facing progress/ETA/control contract.

**Scope:** Race coordinator startup; exercise application and worker named-pipe
endpoints, protocol compatibility, instance nonces, role/launch binding,
bounded unary/streaming queries, keyset cursors, idempotent commands, event
resynchronization, worker staging, child containment, crashes, and
coordinator-only publication.

**Expected states and outputs:**

- exactly one coordinator obtains the durable fenced authority; losers connect
  to it or fail closed;
- application and worker endpoints have restrictive current-user/elevation
  DACLs, reject remote clients, use unpredictable instance-qualified names,
  and expose no TCP/reflection/browser fallback;
- handshake binds compatible protocol/domain/storage versions, instance/epoch,
  capabilities, nonce, endpoint role, and launch relationship;
- unknown major versions/privileged methods and unknown success enums fail
  closed;
- server-side allowlisted filters/sorts, deterministic tie-breakers, bounded
  pages/chunks/messages/queues, and opaque query-bound keyset cursors produce
  stable results;
- slow/overflowed/disconnected clients cannot block durable work and receive
  `resync-required` after a gap/restart/expired window;
- transport cancellation affects only its query/stream; durable commands use
  explicit methods/idempotency and are reconciled after indeterminate replies;
- worker bootstrap travels through an inherited private handle, not
  command-line/environment/application IPC; one worker receives only its exact
  assignment and staging authority;
- worker/helper crash or stale fence cannot publish; only coordinator
  validation/admission creates authoritative artifacts; and
- CLI/shell restart preserves durable run state.

**Pass/fail:** Multiple authorities, direct database access, endpoint role
confusion, nonce replay, unbounded message/query, unstable/misbound cursor,
slow-client scheduler blockage, transport cancel mutating a run, worker
publication, secret/target on ordinary IPC, orphan process tree, or
fail-open incompatibility fails.

**Does not prove:** remote/cross-platform transport, same-user malicious-code
exclusion, persistent worker pools, or WebView2 bridge conformance unless the
shell spike is explicitly included.

### EVAL-0089 — Credential lifecycle and recovery

**Purpose and traceability:** Prove SEC-002, SEC-004, AI-004, and AI-007 under
ADR-0018 through ADR-0021 and ADR-0023.

**M1 live-provider scope:** Mandatory for the bounded direct OpenAI Responses
proof and before any other authenticated reusable-secret provider operation.
Use a unique test profile/generation namespace and exact Credential Manager
generic-credential targets. The helper performs at least one native-entry
enrollment flow. A test-only private-handle injection may automate the
remaining fault variants without creating a production entry path. The case
exercises exact-target write/read/delete and deterministic provider-simulator
dispatch before the deliberately tiny live qualification request.

**Expected states and outputs:**

- enrollment follows `pending_enrollment` -> helper write/verify -> atomic
  activation; half-commit states recover by known exact identity without vault
  enumeration;
- replacement creates a new generation, makes the old generation ineligible
  before exact-target deletion, and retains visible `delete_pending` until
  confirmed;
- disable closes dispatch but retains the item; deletion increments revocation,
  closes every undispatched path, deletes every known exact target, and reports
  typed unavailable-store/failure state without restoring eligibility;
- targets over the 2,560-byte generic-credential limit fail closed without
  truncation, splitting, fallback storage, or activation;
- restart recovery never enumerates arbitrary credentials and cannot confuse
  provider, purpose, profile, generation, account/billing scope, or target;
- backup/restore contains no secret/target and requires re-entry/new generation;
- final helper dispatch revalidates exact operation/run, provider/purpose,
  profile/generation/revocation epoch, account/scope, endpoint/request,
  deadline, reservation, response bounds, and staging identity immediately
  before transport;
- auth/quota/network/scope/billing failure never falls back to another
  credential, account, provider, shared, or project-funded key;
- in-flight work and unknown usage remain visible after local deletion, while
  queued/new/retry/reserved-undispatched work cannot start; and
- secret and target canaries appear only in the authorized OS/helper oracle
  surfaces and are absent everywhere named by EVAL-0034.

**Fault points:** before/after intent, write, verify, activate, generation
switch, revocation increment, exact-target delete, final dispatch gate,
credential read, possible transport start, cancellation, staged response,
coordinator admission, and helper/coordinator restart.

**Pass/fail:** Secret/target leakage, enumeration, wrong-target access,
half-committed activation, in-place replacement, stale-generation dispatch,
fallback, post-revocation undispatched use, premature budget release,
portable backup secret, or silent recovery failure fails.

**Live extension:** As the explicitly approved qualification action, repeat
the applicable dispatch/redaction/request-bound variants against the exact
qualified direct OpenAI Responses adapter and selected access profile with a
deliberately tiny bound. It uses the EVAL-0076/EVAL-0077 profile and remains
gated from general product dispatch. It must never run implicitly as part of
the synthetic baseline.

**Does not prove:** provider-side key revocation, delegated OAuth/device login,
credential portability, protection from same-user malware/administrator, or
M4 uninstall/repair behavior.

## Acceptance interpretation

Passing all mandatory cases proves only that the exercised M1 platform
substrate conforms to these bounded contracts. It does not prove:

- that M1's semantic analyzers are correct or useful;
- that unexercised tools, providers, parsers, record families, filesystems,
  process modes, or export classes are supported;
- M2 graphical workflow quality or accessibility;
- M3 creator-profile/upper-bound scale, exhaustive preflight coverage, or
  personal-playthrough trust;
- M4 packaging, updates, public diagnostics, or supportability;
- safety against a malicious administrator or arbitrary same-user malware; or
- that “no findings” means a playthrough is safe.

Any external-tool operation or future provider mode that is skipped because a
conditional capability is absent remains not applicable to the delivered
scope and unpassed for future enablement. M1's bounded direct OpenAI proof
cannot skip EVAL-0076, EVAL-0077, EVAL-0089, or the applicable live
EVAL-0081 path. Every failed assertion blocks the affected capability; the
implementation may narrow later optional scope and report a gap, but may not
relabel failure as success.
