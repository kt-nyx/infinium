# RESEARCH-0044: Wave E architecture and security integration

Status: Completed  
Date: 2026-07-28  
Last reviewed: 2026-07-28  
Researcher: Codex integration agent  
Research wave: M0 Wave E  
Primary questions: RQ-013, RQ-015 through RQ-018, and RQ-032 through RQ-034  
Decision enabled: Wave E ADR set and Gate E disposition  
Acceptance: Integrated recommendation accepted by the project owner via
ADR-0015 through ADR-0023 on 2026-07-28

## Subsequent dispositions

This document is the dated integration of the eight original Wave E
investigations. Subsequent owner dispositions establish its current use:

- [RESEARCH-0045](RESEARCH-0045-openai-user-access-modes.md) investigated a
  second Codex/ChatGPT-plan surface, but the owner rejected that recommendation
  and ADR-0024. Direct Responses/API-key access under ADR-0013 remains the
  initial LLM execution surface.
- The owner reopened RQ-015, then closed
  [RESEARCH-0046](RESEARCH-0046-dapr-workflow-desktop-lifecycle-qualification.md)
  without a Dapr prototype and accepted the application-owned SQLite
  lifecycle in ADR-0016.
- The owner accepted the standalone coordinator, bounded-worker, and one-shot
  helper process roles in ADR-0018. The concrete application stack and
  IPC/query mechanism remained separate decisions until the final Wave E
  acceptance.
- The owner accepted ADR-0015, ADR-0017, and ADR-0019 through ADR-0023,
  completing the required Wave E architecture set.
- RESEARCH-0049 subsequently completed Wave F, accepted the detailed M1
  specifications and plan, and closed M0. The Wave E evaluation mappings below
  now feed those accepted specifications; their executions remain unpassed.

Accordingly, ADR-0015 through ADR-0023 are accepted and Gate E is met at the
M0 architecture/design layer. Implementation, qualification, and evaluation
remain later work.

## Executive result

The eight original Wave E investigations were mutually compatible when
integrated. RESEARCH-0045/0046 add explicit rejected-alternative and owner
disposition records. The accepted recommendations and subsequent owner
dispositions converge on one coherent local Windows architecture:

- a UI-independent .NET engine and human-readable CLI;
- a React/TypeScript presentation application in a minimal, non-elevated
  WPF/WebView2 Evergreen host;
- a standalone per-user .NET coordinator as the only database, durable-job,
  query, authorization, budget, and publication authority;
- gRPC over explicitly ACL-restricted Windows named pipes for bounded
  application and worker contracts;
- isolated, coordinator-launched workers that stage output but cannot publish
  authoritative state;
- SQLite as the authoritative relational store, paired with a
  coordinator-owned content-addressed payload store and rebuildable
  projections;
- an Infinium-owned transactional run/job lifecycle rather than a generic
  workflow server;
- Windows Credential Manager generic credentials reached only through a
  dedicated one-shot credential/provider helper;
- deny-by-default renderer, filesystem, subprocess, diagnostic, and export
  controls;
- opaque finding/case logical identities with evidence-bearing reconciliation;
  and
- a coordinator-owned, atomic, multi-scope budget-reservation and usage ledger.

No report relies on the legacy implementation, mocked production authority,
guessed effective state, plaintext credential persistence, broad renderer
authority, or direct client/worker database access.

The reports contain no material architectural contradiction. Three boundary
details must be made unambiguous in the ADRs:

1. **The coordinator launches and authorizes the credential/provider helper.**
   The WPF host may request and present/parent an enrollment flow after a user
   gesture, but it does not launch an independently authorized helper or
   receive the secret.
2. **The helper is the entire secret-bearing provider boundary.** It should
   stage a bounded provider response and return non-secret status/usage. Any
   response parsing inside that helper must be minimal, exact-version
   qualified, and unable to expand its filesystem, credential, or endpoint
   authority. General application or worker IPC never carries a secret or
   Credential Manager target.
3. **A Job Object is containment, not a sandbox.** An M1 parser/tool operation
   whose accepted threat model requires compromise containment must first pass
   an AppContainer/LPAC or equivalent isolation decision and prototype, or stay
   outside M1. Ordinary same-user workers must never be described as
   sandboxed.

These are ADR wording and M1 capability-selection obligations, not reasons to
repeat Wave E research.

**Gate E is met at the M0 architecture/design layer.** ADR-0015 through
ADR-0023 are accepted and the documentation is reconciled. This does not imply
that an implementation exists or that any evaluation case has passed.

## Inputs reviewed

This integration reviewed:

- the accepted product baseline and M0 research plan;
- accepted ADR-0001 through ADR-0014;
- [RESEARCH-0036](RESEARCH-0036-evidence-persistence-and-versioning.md);
- [RESEARCH-0037](RESEARCH-0037-job-checkpoint-and-run-lifecycle.md);
- [RESEARCH-0038](RESEARCH-0038-desktop-application-stack-comparison.md);
- [RESEARCH-0039](RESEARCH-0039-process-and-data-query-boundary.md);
- [RESEARCH-0040](RESEARCH-0040-credential-entry-and-storage.md);
- [RESEARCH-0041](RESEARCH-0041-security-boundary-controls.md);
- [RESEARCH-0042](RESEARCH-0042-finding-case-continuity-and-reconciliation.md);
- [RESEARCH-0043](RESEARCH-0043-cost-ledger-and-budget-enforcement.md);
- the current architecture responsibility documents; and
- the evaluation strategy, case catalog, fixture guidelines, and accepted
  anti-overfitting rules.

The individual reports remain research recommendations until their decisions
are accepted through ADRs.

## Independent coherence audit

### Authority and process topology

The recommended authority graph is consistent:

```text
React presentation
  -> narrow WebView2 messages
WPF host / CLI
  -> bounded application contract
standalone coordinator
  -> authoritative SQLite/CAS/job/query/security/budget state
  -> bounded worker assignments
isolated workers
  -> staged candidate output only

coordinator-authorized one-shot helper
  -> exact Credential Manager target
  -> one exact provider enrollment or dispatch
  -> bounded non-secret receipt and staged response
```

The renderer is not a privileged client, the desktop host is not a durable
engine, the CLI is not an alternate direct-database mode, and workers do not
publish evidence, lifecycle, or accounting facts. This preserves ADR-0001
through ADR-0004 and does not expand setup authority.

RESEARCH-0039 contains one phrase that could otherwise be misread as the WPF
host “initiating” the helper. RESEARCH-0041 resolves the intended mechanism:
the coordinator records the exact intent and launches the exact helper;
the host supplies a validated user gesture and presentation relationship only.
The process and credential ADRs must use the latter wording.

### Persistence, payloads, and publication

RESEARCH-0036, RESEARCH-0037, RESEARCH-0039, and RESEARCH-0041 agree that:

- SQLite is the sole authoritative relational system of record;
- one coordinator process owns all database connections and serializes writes;
- large immutable bodies, traces, checkpoints, and outputs live in a
  content-addressed product payload store;
- the database owns payload identity, provenance, retention, and availability;
- workers write only to per-attempt staging;
- the coordinator independently verifies and adopts staged bytes; and
- rebuildable search, summary, current-review, and UI projections are not
  historical truth.

The database and filesystem cannot be committed as one atomic transaction.
The selected protocol must therefore be explicit: stage and hash bytes,
atomically adopt immutable payload content, commit logical references, and use
a reconciler to remove unreferenced temporary/orphaned payloads or report
missing/corrupt payloads. A run or stage becomes complete only after its
authoritative relational references, provenance, coverage, and accounting
commit. This creates harmless reclaimable orphans on some crashes, not durable
rows that silently point to unverified output.

The storage ADR must require a patched SQLite line. It should select an exact
supported version or minimum fixed line through the implementation plan rather
than embedding RESEARCH-0036's probe binding. The probe used SQLite 3.50.4,
which the report correctly identifies as unsuitable for production because of
the documented WAL-reset affected range.

### Runs, jobs, pause/cancel, and deletion

The lifecycle model is internally consistent:

- analysis runs, acquisition runs, and accepted managed-data maintenance
  operations are distinct owner types;
- every node, attempt, checkpoint, output, and actual usage entry has one
  owner;
- requested control and observed execution state remain separate;
- same-run pause/resume preserves immutable bindings;
- cancellation, limit, failure, and invalidation are terminal at their
  applicable node/run scope;
- continuation from a terminal run creates a new manually initiated run with
  validated reuse;
- a fenced coordinator rejects stale worker publication;
- parent/child attachment changes control and attribution, not acquisition
  ownership; and
- deletion is an explicit graph plan, not a queue purge or recursive path
  action.

Paused work is durable and does not require a coordinator process to remain
alive. A renderer or shell restart never changes lifecycle state. An active
coordinator may continue after the shell exits only under the accepted
active-work/keep-running policy; it is not a service and does not survive
sign-out.

### Query and responsiveness boundary

The stack and process reports agree that UI virtualization is insufficient by
itself. High-scale access uses coordinator-side filters, aggregation, bounded
detail expansion, keyset pagination, stable opaque IDs, and short database
reads. Events are coalescible hints with durable sequence/projection versions;
overflow or reconnect forces an authoritative resync.

The accepted initial page and message ceilings are security defaults, not
performance claims. Exact tuning belongs in the M1/M2 prototypes and plans.
Direct SQLite access, caller SQL, arbitrary object/path access, and renderer
connection to the coordinator are all rejected.

### Credential and provider boundary

RESEARCH-0040 and RESEARCH-0041 select a coherent secret lifecycle:

- `CRED_TYPE_GENERIC`;
- opaque exact targets;
- `CRED_PERSIST_LOCAL_MACHINE`;
- fail closed above the 2,560-byte generic-credential limit;
- no enumeration, reveal, secret fingerprint, fallback, roaming, or portable
  secret backup;
- recoverable non-secret enrollment/deletion intents;
- immutable profile/generation/revocation references;
- final dispatch-gate revalidation; and
- separate local disable, local delete, and user-performed provider
  revocation.

The general coordinator is intentionally not credential-bearing. The
one-shot helper alone collects or reads the secret and sends the authorized
provider request. This enlarges that helper's trusted surface beyond a simple
vault broker, so its endpoint, redirect, request shape, response limit, SDK,
logging, and staging behavior must be positively allowlisted and version
qualified. It must not parse/render active provider content, accept an
arbitrary URL, retain a client configured with the key, or send secret-bearing
state back over application/worker IPC.

For CLI development use, “non-echoing helper/CLI path” must mean that the same
trusted helper obtains the secret through its own native or console-bound
entry surface. The CLI and coordinator must not receive the key over ordinary
stdin, arguments, environment variables, gRPC, settings, or logs.

### Untrusted content and privileged operations

The security report closes the important renderer and write-authority gaps:

- packaged local React assets under one controlled WebView2 origin;
- inert text or typed raw-HTML-disabled Markdown, never acquired active HTML;
- strict CSP/Trusted Types and no host objects;
- denied remote navigation, downloads, permissions, frames, or embedded
  remote resources;
- validated external HTTPS opening outside WebView2;
- closed, role-specific message/operation schemas;
- fixed write classes and product roots;
- immutable protected-root registry;
- handle-resolved, handle-relative filesystem operations;
- rejection of reparses, unexpected hard links, device/ADS syntax, and
  caller-selected recursive deletion;
- exact direct subprocess launch without a command shell;
- explicit environment, arguments, working directory, inherited handles, and
  Job Object containment; and
- structured diagnostics plus explicit sharing classes.

The renderer-debug boundary is appropriately strict: release DevTools and
remote debugging are disabled/fail closed, while a development-debug renderer
cannot exercise credentials, paid/provider work, external tools, or protected
evidence through the bridge.

The remaining AppContainer/LPAC question is conditional, not silently solved.
The security ADR must state that Job Objects do not constrain a compromised
worker's ambient same-user file/network rights. Any operation that needs that
stronger threat boundary is excluded until a separately reviewed prototype
and decision accepts it.

### Finding/case continuity

RESEARCH-0042 fits ADR-0002, ADR-0010, FIND-006, and FIND-014:

- immutable run occurrences are distinct from opaque logical identities;
- canonical signatures are indexed candidate keys, not identity authority;
- causal, applicability, dependency, and producer-compatibility gates are
  independently visible;
- only unique, fully proven one-to-one matches reconcile automatically;
- related follow-up, ambiguity, unknown, not-observed, and not-evaluated remain
  explicit;
- merge and split create append-only successor lineage;
- taxonomy, titles, mod names, and symptoms do not establish causal identity;
  and
- disposition, suppression, and annotation carryover is a separate
  provenance-bearing decision over exact finding revisions.

Promotion of a lead-only case should be expressed as a new immutable case
occurrence and explicit `promotes-lead` lineage. The continuity ADR should say
whether this creates a successor logical case or a revision under one logical
case, but it must preserve the accepted product rule: the earlier lead-only
case is never relabeled, and the supported occurrence gains a real finding.
Either non-destructive representation can satisfy the product contract if its
logical meaning and queries are unambiguous.

### Cost, limits, and dispatch

RESEARCH-0037 and RESEARCH-0043 use the same accounting ownership:

- one attempt/operation owns one reservation group and one actual usage entry;
- a reservation is checked against several scopes without duplicating usage;
- attached-parent rollups reference acquisition-owned entries;
- detachment attribution uses dispatch sequence, not receipt time;
- reused historical work creates no new spend; and
- unresolved potentially billable work retains a conservative hold.

The reservation transaction, final dispatch transaction, and credential gate
are separate checks in one authorization sequence. None grants the others'
authority. Exact provider usage, catalog-calculated cost, provider aggregate
billing, rate headroom, spend limit, and prepaid credit remain separate facts.

M1 can hard-enforce only dimensions with a qualified finite bound. For OpenAI,
the accepted first subset uses synchronous stateless Responses, explicit
output/tool bounds, a qualified exact-request input count or conservative
bound, one billable dispatch in flight, and versioned rational price rules.
Exact provider-billed dollars remain unsupported. Background, Batch, explicit
cache behavior, and concurrent live billable dispatch stay disabled until
their separate capability and evaluation gates pass.

If the input-token-count request itself has unqualified billing or rate
behavior, it cannot be treated as a free preflight oracle. The provider
conformance plan must either qualify and reserve that operation or use a
qualified conservative bound that does not create recursive unauthorised work.

## Wave E ADR set

Nine ADRs are the smallest clear durable set. Combining them would make
replaceable transports, product semantics, and security authorities harder to
review independently.

| ADR | Owns | Must not own |
|---|---|---|
| **ADR-0015: Authoritative evidence persistence and payload storage** | SQLite system of record; single process ownership assumption; patched version policy; normalized immutable revisions; CAS payload boundary; typed dependency/provenance/causal indexes; projections; migrations; backup/restore; graph-aware deletion mechanism | Job state semantics, desktop shell, IPC, credential or budget policy |
| **ADR-0016: Application-owned durable run and job lifecycle** | Accepted application-owned SQLite lifecycle; run/acquisition/maintenance ownership; state machine; attempts; checkpoints; pause/resume; terminality; retries; cross-run reuse; child control/attribution; progress; deletion safety | Exact IPC, renderer, credential, or price algorithm |
| **ADR-0017: Windows desktop application stack** | .NET 10 LTS family; UI-independent engine/CLI; React/TypeScript; minimal non-elevated WPF/WebView2 Evergreen host; packaged renderer; shell replaceability; compared/rejected stack options | Coordinator topology, transport, credential store, detailed security controls, M4 packaging/updater |
| **ADR-0018: Process and authority topology** | Accepted standalone per-user coordinator; sole Infinium database/scheduler/query/authorization/publication authority; desktop/CLI clients; worker staging; coordinator-launched one-shot helper; no service/remote client; process lifetime | Concrete presentation stack, local transport, or presentation DTOs |
| **ADR-0019: Local IPC and application-query contract** | gRPC/HTTP2 over explicit current-user/elevation restricted named pipes; separate application/worker endpoints; role/nonce/epoch/version checks; bounded queries, keyset cursors, progress/resync, durable-command idempotency, finite messages; WebView relay is separate and narrow | Domain truth, secret transport, direct renderer connection, TCP/remote API |
| **ADR-0020: Credential storage and provider dispatch** | Credential Manager generic credentials; exact-target one-shot helper; native/helper entry; non-secret profile/generation metadata; dispatch-time resolution; disable/delete/replacement; no fallback; no secret backup/export; same-user limitation | General renderer/path/process controls or budget arithmetic |
| **ADR-0021: Desktop and local-operation security boundary** | Renderer origin/content/navigation controls; role-separated authority; helper launch constraints; protected roots; handle-bound writes; typed subprocesses; Job Objects and non-sandbox statement; staging/publication; diagnostics/sharing classes; M1 export deferral; conditional stronger isolation | Credential lifecycle semantics already owned by ADR-0020; M4 installer/update mechanism |
| **ADR-0022: Finding and case continuity and reconciliation** | Occurrence/logical identity split; versioned signatures; equivalence gates; reconciliation outcomes; lineage; review-state carryover; M1 subset | Analyzer harm semantics, physical schema, UI design |
| **ADR-0023: Atomic cost ledger and hard-budget enforcement** | Multi-scope reservation; deadlines; final dispatch fence; one-owned usage; rational pricing; unresolved holds; detachment/reuse attribution; exhaustion; conservative M1 provider subset | Credential storage, source authority, or provider billing facts not exposed by the provider |

### ADR dependency and review order

The clean review order is:

1. ADR-0015 through ADR-0017;
2. ADR-0016 and ADR-0018;
3. ADR-0019;
4. ADR-0020 and ADR-0021 as a paired authority review;
5. ADR-0022; and
6. ADR-0023.

ADR-0016 appears in both early groups because its product lifecycle can be
reviewed independently, while its concrete coordinator realization depends on
ADR-0015/ADR-0018. The final accepted set must cross-reference rather than
duplicate ownership.

The persistence ADR should explicitly satisfy the accepted RQ-035
typed-index/causal-join representation. This avoids a tenth ADR solely for
physical candidate-index storage. Analyzer-specific candidate lanes,
selection rules, and evaluation remain analyzer/plan responsibilities under
the accepted RQ-035 disposition.

## M1 durable-mechanism coverage

| M1 mechanism | Governing decision after acceptance | M1 boundary |
|---|---|---|
| Immutable snapshots and dependency validity | ADR-0002, ADR-0010, ADR-0015 | Exact schema and transactional capture remain plan work |
| Evidence/acquisition/source/application persistence | ADR-0015 | Full logical split; source retention still follows accepted RQ-031 policy |
| Typed causal/provenance indexes | ADR-0001, accepted RQ-035, ADR-0015 | Only exercised typed edges and bounded joins |
| Run/job/checkpoint lifecycle | ADR-0016 | Accepted thin application-owned SQLite lifecycle over a finite local DAG; no external workflow authority |
| Coordinator and worker authority | ADR-0018, ADR-0021 | One Infinium coordinator; at least one isolated worker; no sandbox claim |
| Local IPC and data access | ADR-0019 | CLI first; graphical shell may remain spike-level in M1 |
| Desktop stack | ADR-0017 | UI-independent M1 remains runnable without WebView2 |
| Credentials and provider dispatch | ADR-0020, ADR-0021 | Required before any authenticated provider operation |
| Filesystem/process/content security | ADR-0021 | Exact supported Windows/filesystem and operation allowlists only |
| Finding/case continuity | ADR-0022 | Conservative one-to-one M1 reconciliation; interactive merge/split deferred |
| Cost/budget enforcement | ADR-0023 | Single live billable dispatch; background/Batch/cache/concurrency disabled |
| Run-owned CLI/JSON output | ADR-0015, ADR-0018, ADR-0019, ADR-0021 | Fixed product output root; not an externally shareable export |
| Deletion and retention effects | ADR-0015, ADR-0016, ADR-0021 | Preview by durable IDs; active-dependency deletion blocked |

No selected M1 mechanism depends on the abandoned implementation.

## Evaluation mapping

No evaluation status should change from `Planned` or
`specification/execution pending` merely because the ADRs are accepted.
Wave F subsequently refined the M1 cases using these ownership assignments:

| Evaluation | Governing Wave E mechanisms |
|---|---|
| **EVAL-0026** | ADR-0015/0016/0019: a context/config edit creates a new version; reconnect, cursor, checkpoint, or UI state cannot mutate the active run |
| **EVAL-0033** | ADR-0017/0019/0021: hostile source/model/tool/renderer/IPC content gains no source, local-state, secret, or operation authority |
| **EVAL-0034** | ADR-0019/0020/0021/0023: secret and unnecessary local context are absent from every ordinary channel; deletion closes dispatch and releases only proven unused reservation |
| **EVAL-0035** | ADR-0018/0019/0021: wrong role/origin/nonce, arbitrary path/URL/SQL/process/tool arguments, aliases, and oversized/malformed messages fail closed |
| **EVAL-0038** | ADR-0016/0019/0023: legal lifecycle transitions, same-run pause, terminal cancel, retries, child control, limits, restart, and durable-command idempotency |
| **EVAL-0039** | ADR-0015/0016/0019: acquisition owns its work/evidence/cost; local application is an explicit immutable link |
| **EVAL-0040** | ADR-0015/0019/0021: M1 CLI/JSON output is run-owned and sensitive; later exports are distinct immutable selections with sharing policy |
| **EVAL-0041** | ADR-0015/0016/0021/0023: deletion preview covers resumability, outstanding holds, reuse, replay, audit, and independent copies without silent cascade |
| **EVAL-0044** | ADR-0016/0023: one owned entry rolls up once; detachment uses dispatch cutoff; historical reuse incurs no new debit |
| **EVAL-0045** | ADR-0016/0018/0019/0023: startup, reconnect, profile/source changes, and UI events never initiate analysis or paid/network work |
| **EVAL-0046** | ADR-0018/0021 plus ADR-0003/0008/0011: exact qualified adapters, no shell/mutating path, declared temp effects, protected roots unchanged |
| **EVAL-0079** | ADR-0015/0022: exact continuation, false-merge/split controls, ambiguity, successor lineage, and review-state carryover |
| **EVAL-0080** | ADR-0015/0018/0020/0021: every product write, credential target, stage, checkpoint, trace, output, deletion, and later export destination stays in its write class under alias/reparse/hard-link adversaries |
| **EVAL-0081** | ADR-0016/0020/0023: reservation and dispatch races, credential revocation, crash/ambiguity, exact arithmetic, settlement, and no oversubscription |
| **EVAL-0082** | ADR-0015/0018/0019: CLI/config independently controls and retains analyzer, source, budget, cache, and trace values without UI/preset suppression |
| **EVAL-0083** | All nine ADRs plus ADR-0001/0002/0010/0012/0013: end-to-end source, local, tool, provider, run, identity, security, ledger, admission, deletion-gap, and application provenance |
| **EVAL-0087** | ADR-0015/0016/0021: authoritative database/payload publication, migration, backup/restore, deletion, and crash reconciliation preserve integrity or expose an explicit recoverable gap |
| **EVAL-0088** | ADR-0018/0019/0021: coordinator authority, worker staging, role-separated IPC, bounded query/cursor behavior, reconnect/resync, and hostile-message handling remain fail-closed |
| **EVAL-0089** | ADR-0018/0020/0021/0023: credential entry, replacement, deletion, helper dispatch, crash recovery, and reservation release never disclose or reuse stale authorization |

EVAL-0081 may use concurrent synthetic reservation transactions while live M1
provider execution remains single-dispatch. Concurrent live billable execution
cannot be claimed until the full provider-specific interleaving suite passes.

## Documentation reconciliation after ADR creation

The root documentation pass should update these exact locations without
changing evaluation cases to “passed”:

1. **`docs/research/investigations/README.md`**
   - index RESEARCH-0036 through RESEARCH-0046;
   - state that RQ-015 is resolved and the remaining research is
     decision-ready;
   - state that the recommendations were accepted through the ADR set.
2. **`docs/research/open-questions.md`**
   - record RQ-013, RQ-015 through RQ-018, and RQ-032 through RQ-034 as
     `Resolved for M0` with their exact accepted ADRs and remaining
     implementation/evaluation gates.
3. **`docs/plans/milestones/M0-research-foundation.md`**
   - add a Wave E research-complete disposition;
   - record Gate E as met only at the M0 architecture/design layer and
     preserve all M1 conformance gates.
4. **`docs/architecture/decisions/README.md`**
   - index ADR-0015 through ADR-0023 with their actual statuses;
   - replace the statement that no stack/database/process/IPC is accepted only
     after the corresponding ADRs are accepted;
   - remove those subjects from the unresolved future-ADR list while retaining
     M4 packaging/update and exact implementation pins as future work.
5. **`docs/README.md`**
   - summarize Wave E as accepted at the architecture/design layer;
   - preserve the distinction between architecture selection and implemented
     conformance.
6. **`docs/architecture/overview.md`**
   - replace the “current leading candidate” section with the accepted stack
     and authority topology only after acceptance;
   - keep responsibilities and shell replaceability explicit.
7. **`docs/architecture/jobs-caching-and-snapshots.md`**
   - replace “storage technology and scheduling implementation remain
     undecided” with the accepted storage/lifecycle mechanisms;
   - retain exact schema, tuning, and implementation-plan details as pending.
8. **`docs/architecture/integrations.md`**
   - update the exact worker/binding/process statements for Mutagen/libloot and
     provider/source adapters;
   - keep every supported surface behind its existing conformance gate.
9. **`docs/architecture/security-and-privacy.md`**
   - replace the Wave E open-security list with the accepted renderer,
     credential/helper, IPC, path, subprocess, staging, and diagnostics
     boundaries;
   - retain provider-retention, M4 packaging/update, and conditional stronger
     worker isolation as explicit follow-up.
10. **`docs/architecture/data-and-trust-model.md`**
    - link the accepted persistence, continuity, and budget mechanisms where
      they implement existing trust concepts without changing evidence
      authority.
11. **`docs/evaluation/evaluation-strategy.md` and
    `docs/evaluation/case-catalog.md`**
    - add ADR traceability and the refined Wave E obligations;
    - keep every named case unpassed until an implementation executes its
      accepted specification.
12. **Accepted product documents**
    - require only a semantic consistency review; these mechanisms implement
      current requirements and should not be duplicated as new product
      behavior.

The eight individual reports are `Completed` and identify their accepted ADR
dispositions. This status does not claim implementation or evaluation
conformance.

## Gate E assessment

| Gate E condition | Current assessment |
|---|---|
| Every durable/cross-cutting M1 mechanism has an accepted ADR | **Met.** ADR-0015 through ADR-0023 are accepted. |
| Selected design satisfies ADR-0001 through ADR-0014 | **Met at the design layer.** No contradiction found; implementation conformance remains pending. |
| No mocked state, guessed authority, plaintext credentials, broad UI privilege, or legacy inertia | **Met by the accepted design.** Runtime conformance remains pending. |
| Every unresolved mechanism is outside M1 or an explicit M1 blocker | **Met at the design layer.** Remaining implementation and qualification gaps stay explicit below. |

The following do not block ADR acceptance but must remain explicit M1
qualification gates or exclusions:

- an affected SQLite native line or unqualified binding cannot ship;
- the database/CAS crash-publication and reconciliation protocol must pass
  fault injection;
- handle-relative write authorization must pass the exact supported Windows
  and filesystem prototype;
- no worker may be called sandboxed; a parser/tool needing compromise
  containment stays excluded until stronger isolation is accepted;
- authenticated work cannot start before the one-shot credential/provider
  helper and revocation/dispatch tests pass;
- a provider operation with an unbounded configured consumptive dimension
  cannot start;
- background Responses, Batch, explicit caching, and concurrent live billable
  dispatch remain outside the first M1 provider subset;
- user-selected/shareable exports remain outside bounded M1; and
- M1 continuity auto-matching remains unique, one-to-one, and fully proven.

The nine required ADRs are accepted, and Gate E is met at the M0
architecture/design layer.

## Recommended disposition

1. Mark RESEARCH-0036 through RESEARCH-0043 `Completed` after review of this
   integration, while keeping their recommendations non-authoritative until
   ADR acceptance.
2. Create ADR-0015 through ADR-0023 with the ownership boundaries above.
3. Review the credential and security ADRs together, and the lifecycle and
   budget ADRs together, to prevent authority or accounting gaps.
4. Accept or reject each ADR explicitly.
5. Run the listed documentation reconciliation.
6. Mark RQ-013, RQ-015 through RQ-018, and RQ-032 through RQ-034 resolved for
   M0 and Gate E met at the architecture/design layer.
7. Proceed to Wave F for reviewed evaluation specifications, deferred-question
   ledger, and the accepted M1 milestone plan.

No production implementation, architecture prototype, conformance result, or
evaluation pass is claimed by this report.
