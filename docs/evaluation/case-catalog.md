# Evaluation case catalog

Status: Accepted
Disposition: actively maintained
Last reviewed: 2026-08-25
ADR-0035 defers independent semantic-oracle qualification throughout M1 and
M2. Case tests remain part of product conformance, but no historical semantic
package or oracle `PASS` gates those milestones.
Executable product-conformance fixtures now cover the accepted bounded M1
backend; many broader catalog cases remain planned for later milestones. This
catalog records both executed status and planned breadth. A row that remains
"pending" outside M1's accepted required-case set does not make the completed
M1 scope incomplete. The Requirements column provides baseline traceability; a
case specification may refine those links but shall not silently drop a
requirement it is used to validate.

ADR-0008 through ADR-0011 accept the Wave B qualification obligations relevant
to EVAL-0046 and EVAL-0051 through EVAL-0054. “ADR gate accepted; case
specification/execution pending” means the required boundary is decided, but
the case remains outside the accepted M1 specification set and no fixture has
passed. It is not evidence of implementation conformance or supported product
coverage.

ADR-0012 through ADR-0014 likewise accept the Wave D Nexus, OpenAI, and LOOT
managed-data design obligations reflected in EVAL-0010 through EVAL-0012,
EVAL-0033, EVAL-0053, EVAL-0064, EVAL-0067, EVAL-0068, EVAL-0076,
EVAL-0081, and EVAL-0083. Their **Planned** or
**specification/execution pending** states remain unchanged: accepting the
design boundary does not pass provider, source, adapter, security, or
evaluation conformance.

“Qualified candidate; specification/execution pending” means research has
pinned the inputs, independent ground truth, controls, and claim boundary, but
the final case specification remains outside the accepted M1 set and no
implementation has passed it.

Wave F produced the accepted
[archived M1 evaluation baseline](evaluator-history.md), its two accepted
[case-specification sets](specifications/), and their
[closed-world public fixture authority](repository-evaluation-authority.v1.json).
“Wave F specification accepted; execution
pending” means the detailed obligation is authoritative but no fixture or
implementation has passed the case.

The `Domain` values below are historical case-catalog descriptors for planning
and navigation. They are not codes from the accepted
[Skyrim SE mod-impact taxonomy](../product/mod-impact-taxonomy.md). Case
specifications shall add versioned taxonomy assignments without rewriting what
these historical planning labels meant.

Current Slice 4/4.5 partition state:

```text
Public Slice 4 evidence: passed
Evaluator-v1 held-out attempts: blocked / no product verdict
Historical evaluator-v2 /2 Stage C: immutable FAIL; product verdict invalidated
Final public protocol /4: qualified/frozen historically; retired and archived by ADR-0033
Owner semantic disposition: accepted under ADR-0028
Public realignment/requalification: complete; candidate frozen at a98d648
Private B2 oracle/corpus: terminal public-authority stop; no oracle/product verdict
Public /5 successor: retired unqualified after WP1V hard stop; no implementation or verdict
Held-out C2 scoring and Stage D: deferred / not authorized / no product verdict
Slice 4.5: closed by accepted owner disposition with explicit residual risk
Overall M1 gate: completed under the accepted product-conformance profile; live handoff is stated only in docs/current-state.md
```

The sanitized evaluator chronology and successor dispositions are in
[Evaluator history](evaluator-history.md).
The exact public successor handoff is
[machine-readable](evaluator-history.md).

| ID | Type | Historical planning label | Expected purpose | Requirements | Status |
|---|---|---|---|---|---|
| EVAL-0001 | Synthetic positive | NPC/cross-layer | Appearance mod unintentionally reverts behavioral edits | ANALYSIS-004, ANALYSIS-005, FIND-003 | Wave F specification accepted; execution pending |
| EVAL-0002 | Synthetic negative | NPC/cross-layer | Structurally similar reversion is documented/intentional | ANALYSIS-003, ANALYSIS-004, EVID-006, INTENT-003 | Wave F specification accepted; execution pending |
| EVAL-0003 | Synthetic positive | Patch | Correct patch preserves appearance and behavior | ANALYSIS-007 | Planned |
| EVAL-0004 | Synthetic positive | Patch | Patch exists but is later overwritten | ANALYSIS-007 | Planned |
| EVAL-0005 | Synthetic positive | Patch/version | Patch targets an older upstream version | ANALYSIS-007, ANALYSIS-008 | Planned |
| EVAL-0006 | Synthetic positive | Cell/quest | Visual cell edit reverts a quest-relevant reference | ANALYSIS-004, ANALYSIS-005, FIND-003 | Planned after M1; requires separate narrow `QUST` qualification |
| EVAL-0007 | Synthetic negative | Cell | Intentional cell override with no lost required behavior | ANALYSIS-003, EVID-006 | Planned after M1; not part of the M1 required case set |
| EVAL-0008 | Synthetic positive | Item/crafting | Visual item change loses keywords or recipe behavior | ANALYSIS-004, ANALYSIS-005 | Planned |
| EVAL-0009 | Synthetic negative | Asset | Ordinary texture/mesh overwrite is not a finding | ANALYSIS-006, UX-004 | Planned |
| EVAL-0010 | Synthetic positive | Documentation | A schema-bound claim proposal resolves only to host-supplied exact spans and the acquired source revision, then matches applicable local versions/options | DOC-003, EVID-002, ANALYSIS-001 | Planned |
| EVAL-0011 | Synthetic negative | Documentation | An authoritative-looking search result/model summary cannot satisfy source authority or local applicability without acquired exact evidence, and a conditional acquired claim still does not apply to mismatched local versions/options | DOC-005, EVID-006 | Planned |
| EVAL-0012 | Synthetic boundary | Intent/documentation | Missing landing acquisition, exact passage, version, or material intent creates a lead/gap/needs-input/abstention rather than an external claim or finding | INTENT-001, INTENT-002, DOC-008, EVID-006 | Planned |
| EVAL-0013 | Synthetic positive | Snapshot | Relevant change invalidates dependent result | SNAP-002, SNAP-004 | Planned |
| EVAL-0014 | Synthetic negative/boundary | Snapshot | A proven unrelated change permits explained carryover, while ambiguous impact requires recomputation, explicit skipping, or dependency-evaluable typed input and cannot become validated through bare “reuse anyway” confirmation | SNAP-004, SCAN-007 | Planned |
| EVAL-0015 | Synthetic failure | Coverage | Analyzer failure is isolated and visible | SCAN-006, COVER-001, COVER-003 | Planned |
| EVAL-0016 | Real mods | Actor/AI/FaceGen cross-layer | First pinned real scope-incongruent reversion candidate (`REAL-NPC-0001`) | ANALYSIS-004, ANALYSIS-005 | Passed for the accepted M1 developer-owned controlled-real actor scope with its matched control; no independent semantic verdict or archive/NIF breadth is claimed |
| EVAL-0017 | Real mods | Placed-reference/link/placement cross-layer | Materially different generic-mechanism candidate (`REAL-REFR-0001`) | ANALYSIS-004, ANALYSIS-005 | Passed for the accepted M1 developer-owned controlled-real placed-reference scope with its matched control; no independent semantic verdict or broader record-family claim is made |
| EVAL-0018 | Scale | Operational | High-end profile progress, resume, and cost behavior | SCAN-004, SCAN-005, AI-004, OPS-004 | Planned for M3 |
| EVAL-0019 | Synthetic positive | Analysis context | Relevant intent, identity, or local claim-applicability change invalidates dependent results, while confirmed assumptions remain scoped and dependency-validated rather than becoming universal rules | SNAP-002, SNAP-004, INTENT-004, INTENT-005, DOC-003, DOC-004 | Planned |
| EVAL-0020 | Synthetic negative | Review state | Finding disposition or note does not alter analyzer output/context | FIND-005, FIND-006 | Planned |
| EVAL-0021 | Synthetic boundary | Reproducibility | Changed live source creates a distinct run while a complete retained replay remains stable | SNAP-001, SNAP-005, SNAP-006, AI-006 | Planned |
| EVAL-0022 | Synthetic boundary | Runtime evidence | Historical or weakly matched log is not auto-applied | VALID-003 | Planned |
| EVAL-0023 | Synthetic negative | Scan configuration | Tracing/cache/concurrency change does not create a semantic context or by itself invalidate prior equivalent artifacts | SNAP-001, SNAP-004, SCAN-009 | Planned |
| EVAL-0024 | Synthetic positive | Carryover/history | Reused finding/case revision preserves original provenance and records a reuse edge | SNAP-004, FIND-006 | Planned |
| EVAL-0025 | Synthetic failure | Retention | Missing original input reports replay and audit gaps without rewriting surviving history | SNAP-006, DOC-008, OPS-002 | Planned |
| EVAL-0026 | Synthetic boundary | Run immutability | Mid-run context/config edit creates a new version and never mutates the active run | SNAP-002, SCAN-009, INTENT-004 | Wave F specification accepted; execution pending |
| EVAL-0027 | Synthetic boundary | Readiness | Targeted run never borrows/overwrites prior coverage; only full-policy carryover replaces readiness, while other targeted results show explicit scope-limited/no-readiness status and newer applicable evidence can stale the old result | SNAP-003, FIND-007, FIND-008, FIND-010, FIND-012, COVER-003, UX-001 | Planned |
| EVAL-0028 | Synthetic boundary | Claim adjudication | Source extraction correction is reusable while local non-applicability remains context-scoped | DOC-004 | Planned |
| EVAL-0029 | Synthetic positive | Runtime evidence | Test evidence alone mutates nothing; manual reanalysis creates linked finding/case revisions | VALID-001, VALID-002, VALID-003, VALID-005 | Planned |
| EVAL-0030 | Synthetic boundary | Severity/confidence/maturity | Plausible catastrophic impact retains severity but does not auto-block below evidence/maturity policy; maturity-based presentation never relabels a finding as a lead or a lead as a finding, and review confirmation alone cannot promote an under-threshold lead without additional qualifying typed evidence | FIND-001, FIND-009 | Planned |
| EVAL-0031 | Synthetic boundary | Freshness | Refresh-vs-reuse choice changes execution configuration; resolved source revisions change run inputs; stale-evidence acceptance changes semantic context | DOC-009, SNAP-001 | Planned |
| EVAL-0032 | Synthetic scale | Candidate selection | Snapshot-bound typed indexes and causal joins retain planted interactions without naïve all-pairs LLM calls; canonical participants, join/rationale provenance, matched negatives, unsupported/gap populations, and scope dependencies are preserved; score perturbation cannot remove deterministic or mandatory-lane work | EVID-005, ANALYSIS-017, OPS-004 | Catalog specification accepted 2026-07-25; Wave F fixture/specification expansion accepted; execution pending |
| EVAL-0033 | Synthetic adversarial | Untrusted content | Prompt/HTML/search/tool-output instructions cannot grant source, local-state, or operation authority; cannot obtain internal tools or secrets; and cannot alter analysis/source policy | SEC-001 | Wave F specification accepted; execution pending |
| EVAL-0034 | Synthetic adversarial | Credentials/context minimization | Secrets and unnecessary usernames, absolute paths, and unrelated values are absent from prompts, logs, traces, and exports while required non-secret context remains usable; confirmed credential deletion prevents queued/new/retry or reserved-but-undispatched use, releases unused budget reservation, and leaves uncancellable in-flight work disclosed | SEC-002, SEC-004, AI-003, AI-004 | Wave F specification accepted; execution pending |
| EVAL-0035 | Synthetic adversarial | Privileged boundary | Out-of-scope path, URL, command, and tool arguments are rejected | AUTH-002, SEC-003 | Wave F specification accepted; execution pending |
| EVAL-0036 | Synthetic boundary | Case presentation | Lead-only investigation is counted separately, shows hypothesis status, and cannot affect readiness | FIND-002, FIND-011, UX-001 | Planned |
| EVAL-0037 | Synthetic boundary | Clean/freshness policy | Clean analysis or extraction bypasses only its selected derived layer without implicitly refreshing source bytes; explicit refresh is separately visible | SCAN-007, DOC-011 | Wave F specification accepted; execution pending |
| EVAL-0038 | Synthetic boundary | Job lifecycle | Pause resumes the same run and stops all new attached-child work by default; cancellation is terminal; terminal retry or changed-config continuation uses a new user-initiated run; node-scoped limit exhaustion leaves unrelated parent work active; child continuation/detachment is explicit and preserves provenance | SCAN-005, SCAN-006, AI-004 | Wave F specification accepted; execution pending |
| EVAL-0039 | Synthetic boundary | Documentation acquisition | Independent acquisition retains source-run provenance/coverage; profile application is explicit and local documents retain snapshot provenance | DOC-002, DOC-011 | Wave F specification accepted; execution pending |
| EVAL-0040 | Synthetic boundary | Run output/export | M1 emits human-readable CLI and versioned JSON as run-owned output; later user-created exports remain distinct and record exact readiness-evaluation/source selection, sharing class, configuration, privacy/source-policy decisions, and omissions; restricted material is excluded from externally shareable output; creation/deletion does not mutate source results | SEC-004, OPS-003 | The bounded M1 CLI, versioned run output, and finding-report projection passed; user-created export workflow and presentation remain later milestones |
| EVAL-0041 | Synthetic failure | Retention/job state | Deletion preview exposes active/paused resumability, reuse impact, and independently retained exports, run-owned outputs, or traces containing selected material; explicitly scoped direct or confirmed-cascade deletion creates honest gaps without corrupting or silently deleting surviving work | SCAN-005, SNAP-006, OPS-002 | Planned |
| EVAL-0042 | Synthetic boundary | Symptom report | Initial diagnostic submission explicitly starts one bounded run; later report/clarification revisions remain user-statement evidence and create no run unless analytical follow-up is explicitly initiated; consuming runs/revisions never rewrite earlier history | SCOPE-004, SNAP-001, VALID-005 | Planned |
| EVAL-0043 | Synthetic boundary | Assumption lifecycle | Inferred/user-provided origin remains distinct from confirmation; create/edit/delete makes new context state without rewriting history | INTENT-004, INTENT-005 | Planned |
| EVAL-0044 | Synthetic boundary | Cost accounting | Attached child acquisition is included once in every applicable parent/child limit and rollup without duplicated ownership; reused historical cost remains separate; detachment freezes the parent contribution and later acquisition spend is separately authorized/attributed | SCAN-004, SCAN-005, AI-004 | Planned |
| EVAL-0045 | Synthetic boundary | Manual initiation | Profile/source changes do not trigger analysis or paid/network work; configured children run only under a user-initiated parent | SCOPE-004 | Passed for M1 Slice 3 on 2026-07-30: explicit durable submission, idempotency, bounded worker dispatch, staging, fencing, restart handling, coordinator validation, and publication authority passed without implicit work |
| EVAL-0046 | Integration safety | External tools | Every allowed application/library operation is exercised against disposable protected roots; it leaves MO2, mods, game, profile, configuration, and generated output unchanged, records every allowed product/tool cache or temp effect, and proves that no forbidden write/apply API or command path was reachable | AUTH-001, AUTH-003 | Passed for the delivered M1 Slice 3 exact headless capture operation on 2026-07-29; conditional repeat remains required for every future external operation/version |
| EVAL-0047 | Synthetic boundary | Suppression lifecycle | Equivalent finding carries suppression with provenance; materially changed revision remains visible and retains old suppression only as history | FIND-005, FIND-006 | Planned |
| EVAL-0048 | Synthetic boundary | Advisory readiness | Unreviewed advisory stays visible/countable without changing readiness; explicit action-required disposition does change it | FIND-013 | Planned |
| EVAL-0049 | Synthetic boundary | Remediation/validation | A supported remediation states risks, reversibility, and meaningful verification; an unsupported resolution instead produces a snapshot/case-scoped, bounded validation or missing-evidence plan with save/test risks and inconclusive outcomes, and neither makes a global-safety claim | PROD-004, FIND-004, VALID-001 | Planned |
| EVAL-0050 | Synthetic boundary | Resolution state | User-resolved disposition is visibly unverified until new evidence validates it; verification creates new evidence/review history and only the analytical finding/case revisions actually produced by reanalysis, without rewriting the decision or prior finding | FIND-005, FIND-006 | Planned |
| EVAL-0051 | Integration ground truth | MO2 effective state | For pinned MO2 `2.5.2` disposable instances, explicit profile selection, enabled state/order, plugin state/order, qualified loose providers, hidden/deleted/unmanaged state, mapper effects, and physical-local/source identity separation agree with authoritative MO2 behavior; archive-provider behavior is tested only when separately qualified and otherwise becomes a gap, the saved selection is only a startup suggestion, drift fails closed, and the private reference profile is not an oracle | SCOPE-003, SCOPE-005, SNAP-001 | Passed 2026-07-30 for the exact admitted MO2/Skyrim target and accepted empty additional-mapper inventory; broader MO2 versions, mappers, and archive members remain unsupported |
| EVAL-0052 | Integration ground truth | Bethesda records | For positively allowlisted `Mutagen.Bethesda.Skyrim` `0.54.2` shapes, supported plugin order, records, override chains, winners, FormKeys, links, states, and field values agree with independently specified hand-audited binary/semantic fixture expectations; the Mutagen path under test is not the sole source of expected results, and unsupported archive/string/record semantics become explicit gaps | SCOPE-005, ANALYSIS-003, ANALYSIS-019, COVER-001 | Public Slice 4 conformance passed for exact candidate `a98d648` and scope. Protocols `/4` and `/5` are retired; `/4` is archived and has no current evidence role. Private held-out evaluation is deferred with no valid current verdict. Later M1 use is public development/validation evidence under the continuation profile. |
| EVAL-0053 | Integration fidelity | LOOT | When LOOT coverage is claimed, the pinned libloot `0.29.6` read-only adapter reproduces the exact selected immutable masterlist/prelude pair, private userlist, configuration, and allowlisted diagnostics; refresh tests cover 200/304, corrupt/partial/pair-invalid updates, atomic activation/rollback, offline/stale state, unsupported compatibility lines, run-binding races, and historical replay; authorities remain distinct and no set/write/apply operation is reachable | AUTH-003, EVID-003, ANALYSIS-002 | ADR-0011 and ADR-0014 gates accepted; specification/execution pending |
| EVAL-0054 | Synthetic boundary | Supported target | Only the exact initial Steam Windows x64 Skyrim SE `1.6.1170.0` executable identity accepted by ADR-0009 enters semantic analysis; unknown hashes, other channels/runtimes, unsupported managers/platforms, malformed inputs, and mid-capture changes fail clearly without best-effort conclusions or fabricated coverage | SCOPE-001, SCOPE-002, SCOPE-006 | Passed 2026-07-29 against the evaluator-private exact target and complete preregistered negative matrix |
| EVAL-0055 | Synthetic coverage | Documentation | Full enabled-mod mode accounts for every eligible identifiable mod and reports exclusions, limits, failures, and unresolved identities | COVER-001, COVER-002, DOC-001 | Planned |
| EVAL-0056 | Workflow evaluation | Frontend | Experienced mod users can complete the finding-centric preflight workflow; summary-first review, progressive disclosure, the user-facing scan-configuration controls delivered for M2, and large-result navigation remain understandable and responsive | PROD-001, PROD-002, PROD-003, SCAN-009, UX-001, UX-002, UX-003, UX-004, UX-005, UX-006, OPS-005 | Planned for M2 |
| EVAL-0057 | Integration ground truth | Root/native state | Runtime, loaders, native components, unmanaged root files, and installed-version relationships are reconstructed without inferring unsupported compatibility | ANALYSIS-008, ANALYSIS-009 | Research required |
| EVAL-0058 | Synthetic positive | Generated output | A supported generator's stale or mismatched output is detected from declared inputs/outputs, while an unsupported generator produces bounded observations and explicit gaps | ANALYSIS-010, COVER-001 | Planned |
| EVAL-0059 | Synthetic positive | Referenced assets | A format-specific reference to a missing effective asset is detected with the expected provider/reference provenance | ANALYSIS-013 | Planned |
| EVAL-0060 | Synthetic positive | Performance/stability | A concrete documented or locally measured instability mechanism is reported with bounded impact and evidence | ANALYSIS-014 | Planned |
| EVAL-0061 | Synthetic negative | Performance/stability | Generic texture/script heaviness or hardware speculation produces no performance finding | ANALYSIS-014, EVID-006 | Planned |
| EVAL-0062 | Synthetic positive | Playthrough lifecycle | Applicable new-game, install, upgrade, removal, or regeneration instructions are extracted and matched to the installed state | ANALYSIS-015, DOC-005 | Planned |
| EVAL-0063 | Synthetic positive | Requirements/masters | A missing, disabled, or incompatible master/requirement is reported only when its applicability is established | ANALYSIS-001, EVID-003 | Planned |
| EVAL-0064 | Contract/integration | Offline/provider boundary | A local-only run requires no provider credentials; unavailable network/LLM capabilities are explicit; OpenAI may be the sole initial provider; later adapters declare their own capabilities without altering provider-independent domain/evidence contracts or being required to emulate OpenAI search | AI-001, AI-002, OPS-001 | Passed for the bounded M1 offline-by-default local path and explicit OpenAI development-provider boundary; additional providers and shipped-product enrollment remain later work |
| EVAL-0065 | Contract/boundary | Analyzer modularity | One analyzer can be configured and run independently with declared scope, dependencies, evidence threshold, coverage, cost/scale, maturity, and evaluation links intact | SCAN-001, ANALYSIS-016 | Passed for the independently configured M1 scope-reversion analyzer and its declared coverage/dependencies; future analyzers must qualify the same contract separately |
| EVAL-0066 | Calibrated operational | Estimate/presets | Pre-run time/cost/coverage estimates and user presets are derived from retained measurements, expose uncertainty and overrides, and respect configured limits | SCAN-003, SCAN-010 | Planned for M3 |
| EVAL-0067 | Synthetic contract | Evidence/LLM transparency | Typed observations, claims, candidates, hypotheses, findings, recommendations, gaps, OpenAI Response/search items, discovery leads, and admitted outputs remain distinct; LLM involvement and raw development intermediates are retained and visible as required | EVID-001, EVID-004, EVID-007, OPS-002 | The bounded M1 separated-axis contracts, semantic admission, persistence, replay, and historical-migration regressions passed. Historical packages remain non-authorizing, and ADR-0035 still defers independent semantic qualification through M2. |
| EVAL-0068 | Synthetic boundary | Source policy/history | Nexus latest-capable v3/v2/v1 routing, schema drift/fallback, unsupported API surfaces, and no-page fallback are explicit; web search, landing acquisition, extraction, and local application are separately provenanced; permitted material remains available through required work and later deletion/minimization produces honest gaps | DOC-006, DOC-007, DOC-008 | Planned |
| EVAL-0069 | Synthetic boundary | Disposition/readiness | Per-finding decisions remain canonical; accepted risk or readiness-policy change creates a new time/policy/disposition-bound evaluation without rewriting the run, semantic context, or prior evaluations; case-level bulk action records each member change | FIND-007, FIND-008, FIND-009, FIND-010, FIND-012 | Planned |
| EVAL-0070 | Operational | Resource defaults | Conservative defaults permit background use, basic user limits are honored, and limiting one resource exposes resulting delay or coverage loss | SCAN-008 | Planned if delivered |
| EVAL-0071 | Synthetic positive/negative | Configuration | Supported syntax, winner, schema, and documentation rules detect planted defects while arbitrary unsupported semantics produce gaps rather than guesses | ANALYSIS-011 | Planned if delivered |
| EVAL-0072 | Synthetic boundary | Installer choices | Installed files and retained archives support a bounded likely choice or explicit ambiguity without claiming exact FOMOD history | ANALYSIS-012 | Planned if delivered |
| EVAL-0073 | Synthetic adversarial | Broader web search | Opt-in broader search obeys the source registry and preserves community authority instead of promoting model-selected results | DOC-010 | Planned if delivered |
| EVAL-0074 | Synthetic boundary | Manual log import | User-initiated import records snapshot/session provenance and preserves weak or unknown association without automatic application | VALID-003, VALID-004 | Planned if delivered |
| EVAL-0075 | Integration boundary | Tracked test session | A manually delimited session correlates evidence without launching the game, automating success, or changing setup state | AUTH-001, VALID-006 | Planned if delivered |
| EVAL-0076 | Contract/boundary | Provider capabilities | Response tokens, hosted-search calls, rate-window headroom, configured spend limits, historical usage/cost, local run budgets, finite hard-limit bounds, and billing latency remain distinct; unavailable credit balance or other capabilities are explicit and never invented | SCAN-003, AI-005 | The bounded synchronous M1 capability, local limit, usage, and unavailable-state distinctions passed; hosted search, background/Batch/cache, concurrent billing, and production account visibility remain disabled or later work |
| EVAL-0077 | Contract/adversarial | Provider billing authority | No authenticated or billable model call occurs without current user-supplied authorization for the selected account; usage/cost is attributed correctly and no project/shared credential fallback exists | AI-004, AI-007 | The explicit budgeted development-provider route and strict separation from shipped-product credentials passed under ADR-0036; ordinary product-user enrollment and authenticated production qualification remain later work |
| EVAL-0078 | Synthetic positive/negative | Change impact | Relevant cross-snapshot/context changes are explained and invalidate only dependent work; unrelated changes carry over, and a user-designated reference is never treated as correctness proof | SNAP-004, UX-005, ANALYSIS-018 | Planned if delivered |
| EVAL-0079 | Synthetic positive/negative | Finding/case lineage | Equivalent or explicitly superseding outputs reconcile to stable logical finding/case lineage with valid disposition carryover only where applicable, while similar-but-distinct causes or changed applicability remain separate; reviewed merges/splits create explicit lineage | FIND-006, FIND-014 | The bounded M1 identity/reconciliation, persistence, and replay portion passed; interactive ambiguity and reviewed merge/split workflow remain M2 extensions |
| EVAL-0080 | Synthetic/integration safety | Product writes | Every exercised settings, credential, cache/temp, history/checkpoint, trace, export, deletion, and update-staging path stays within its approved product-controlled, OS-backed, or explicitly selected non-protected authority; direct and aliased/reparse-point paths into protected setup roots are rejected and remain unchanged | AUTH-001, AUTH-002, SEC-003 | Passed for every write path exercised by the bounded M1 backend; each future settings/export/update path requires its own qualification |
| EVAL-0081 | Synthetic/adversarial | Budget enforcement | Concurrent parent/child billable work atomically reserves one declared worst-case amount against every applicable limit, handles sync abort/background cancel/Batch cancel-expiry and partial/delayed usage without releasing budget incorrectly, reconciles one owned actual ledger entry, and cannot oversubscribe or continue after unresolved variance | SCAN-004, SCAN-005, AI-004 | The bounded synchronous M1 reservation, fencing, settlement, abort, variance, and replay substrate passed; concurrent/background/Batch/cache-specific extensions remain disabled pending separate qualification |
| EVAL-0082 | Contract/boundary | Development controls | The M1 CLI/config contract independently controls analyzers, sources, budgets, cache policy, and tracing; the effective values are retained, and every enabled analyzer contributes its raw typed output without preset or maturity-based suppression | SCAN-002, SCAN-009, EVID-007 | Passed for the bounded M1 CLI/configuration and retained effective inputs; frontend controls and future analyzer/source options remain later work |
| EVAL-0083 | Synthetic/integration provenance | End-to-end provenance | A material conclusion retains local/tool/source versions, Nexus interface/spec/schema/query/fingerprint routing, OpenAI capability/model/request/search actions/sources, landing acquisition/passages, validation/admission, supporting/contradicting evidence, and application links; deleted/unavailable inputs create honest gaps | EVID-002, SNAP-006, AI-006 | Passed for the local, Bethesda, retained-source, and synchronous OpenAI boundaries exercised by M1; Nexus/live-search and each future boundary require separate extension evidence |
| EVAL-0084 | Synthetic positive/negative | Causal case grouping | Multiple findings with one supported likely cause group into one case, while findings sharing only a mod or record family but having distinct causes remain separate; lead-only investigations remain visibly distinct from supported cases | FIND-002 | Passed for the bounded M1 finding/case and scope-reversion populations; future analyzers must re-exercise cross-analyzer grouping |
| EVAL-0085 | Synthetic boundary | Coverage/readiness presentation | Different coverage populations retain labeled denominators and states rather than one combined analyzed/safety percentage; no-findings output remains qualified by gaps and uncertainty and never claims the playthrough is safe | PROD-004, COVER-001, COVER-002, COVER-003 | The bounded M1 CLI/report projection passed with separate coverage, failure, abstention, limited, and gap states and no safety claim; graphical M2 presentation remains later work |
| EVAL-0086 | Synthetic/controlled real | Taxonomy classification | Representative single-area, cross-cutting, multi-surface, and assigned/unknown/unsupported/unmapped/not-applicable interactions use the accepted taxonomy and declared/observed/predicted/established roles without conflating declared purpose, hosting-site category, technical modification surface, affected game area, consequence, severity, confidence, authority, or effect extent; historical outputs retain their taxonomy version | FIND-001, COVER-002, ANALYSIS-016, ANALYSIS-019 | The accepted M1 two-domain generic proof and EVAL-0016/EVAL-0017 controlled-real classification obligations passed as developer-owned conformance. Broader taxonomy population coverage and an independent verdict remain later work. |
| EVAL-0087 | Synthetic/integration failure | Persistence integrity and recovery | Authoritative relational state and content-addressed payloads preserve atomic publication, immutable history, typed dependencies, and explicit gaps across crash points, WAL/checkpoint pressure, migration failure, backup/restore, corruption/quarantine, shared-payload deletion, and projection rebuild; recovery never manufactures or silently rebinds evidence | SNAP-004, SNAP-006, OPS-002, OPS-004 | The bounded M1 persistence, migration, backup/restore, replay, corruption, and fault obligations passed; longer-duration and high-end-scale evidence remain later work |
| EVAL-0088 | Synthetic/integration boundary | Process, IPC, and query contract | Coordinator-start races select one fenced authority; client/worker/helper roles, protocol versions, nonces, message and page limits, cursors, streams, cancellation semantics, reconnect/resync, worker staging, and coordinator-only publication fail closed and remain correct across malformed input, slow clients, and process crashes | SCAN-004, SCAN-005, AUTH-002, SEC-003 | The bounded M1 coordinator, worker/helper protocol, fencing, malformed input, reconnect, staging, and crash-recovery obligations passed; frontend and expanded operational modes remain later work |
| EVAL-0089 | Synthetic/integration failure | Credential lifecycle and recovery | Exact-target OS-backed enrollment, verification, replacement, disable, deletion, restart recovery, unavailable-store handling, size limits, metadata/secret half-commit recovery, backup/restore reauthentication, and dispatch races preserve generation/revocation authority without secret leakage, fallback, or continued undispatched use | SEC-002, SEC-004, AI-004, AI-007 | The bounded M1 credential enrollment/verification/rotation/disable/deletion, recovery, unavailable-store, size-limit, race, and secret-canary obligations passed; shipped-product enrollment UX and production qualification remain later work |
| EVAL-0090 | Contract/integration | Frontend application bootstrap and live contract | Generated clients negotiate versions; obtain bounded health/capability/configuration/recent-work bootstrap state; use typed query/command/error/conflict/cancellation states; and recover through authoritative snapshot/resync after stale cursor, event loss, renderer/shell restart, or coordinator restart without UI-owned durable truth | SEC-003, SCAN-004, SCAN-005, OPS-005 | Planned for `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION` Phases A/B/D/E |
| EVAL-0091 | Contract/integration safety | Setup, profile, configuration, estimate, and enrollment | Exact MO2/applicable LOOT states and capability gaps are visible; tool override is typed and validated; saved MO2 selection remains suggestion-only; one profile is explicitly confirmed; saved scan configurations preserve revisions and immutable effective run values; pre-run estimates expose authority gaps; and provider enrollment returns only non-secret outcomes | SCOPE-003, SCOPE-004, SCAN-001, SCAN-002, SCAN-003, SCAN-009, TOOL-001, TOOL-002, TOOL-003, SEC-002 | Planned for `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION` Phases B/D/E |
| EVAL-0092 | Contract/integration/scale | Frontend result exploration | Summary/readiness or scope-limited status, supported-case and lead-only queues, finding/case/report details, evidence/provenance expansion, and focused-mod views preserve canonical truth, uncertainty, coverage, and gaps through bounded server-side filters/sorts/search/keyset pages; 100,000 synthetic summaries are virtualized without full-population transfer | PROD-003, PROD-004, UX-001, UX-002, UX-003, UX-004, OPS-005 | Planned for `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION` Phases C/D/E |
| EVAL-0093 | Contract/integration failure | Review state, assumptions, targeted verification, and export | Revision-bound append-only dispositions, suppression, annotations, and assumptions survive concurrency/restart without rewriting analysis; carryover obeys causal/revision proof; targeted verification creates separately initiated scope-linked work without borrowing unrelated readiness; and local-private structured export records exact selection/provenance/privacy/source-policy decisions without mutating source objects | INTENT-004, INTENT-005, FIND-005, FIND-006, FIND-007, FIND-014, UX-005, OPS-002, OPS-003 | Planned for `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION` Phases C/D/E |
| EVAL-0094 | Integration/adversarial/accessibility/performance | Desktop host and renderer bridge qualification | Packaged React runs under the controlled WebView2 origin; the generated closed bridge rejects wrong-origin/version/session/sequence/gesture, malformed, oversized, replayed, out-of-order, arbitrary path/command/URL/provider/credential, navigation/download/permission/new-window, DevTools, and remote-debug attempts; real paginated query/progress/cancellation/reconnect paths work; missing/outdated runtime and renderer failure recover; representative keyboard/focus/naming/landmark/contrast/zoom/reduced-motion/screen-reader checks pass; and startup/memory/query/bridge/package/runtime/license measurements are retained | SCOPE-006, SEC-001, SEC-002, SEC-003, SEC-004, UX-006, OPS-005 | Planned for `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION` Phases D/E |

## First proof acceptance

The initial semantic proof is not complete unless it:

- detects EVAL-0001;
- does not misclassify EVAL-0002;
- passes EVAL-0051 and EVAL-0052, or approved successor ground-truth cases, for
  every MO2/file/archive/record surface exercised by the proof;
- passes EVAL-0065 and EVAL-0067, or approved successors, for analyzer-contract
  and typed-evidence/LLM-transparency behavior exercised by the proof;
- passes EVAL-0082 or an approved successor for M1 development-control
  behavior;
- passes EVAL-0083 through EVAL-0085, or approved successors, for end-to-end
  provenance, causal grouping, and honest coverage/no-safety-claim behavior;
- passes the M1 identity/reconciliation portion of EVAL-0079 or an approved
  successor without implicit disposition or suppression carryover;
- passes EVAL-0087 and EVAL-0088, or approved successors, for every exercised
  persistence and process/query boundary;
- uses citations correctly;
- produces a coherent case;
- explains uncertainty;
- suggests a plausible resolution or validation;
- replays from a fixture run whose declared dependencies are completely
  retained;
- passes EVAL-0026 or an approved successor run-immutability case;
- passes EVAL-0032 or an approved successor candidate-selection case;
- passes EVAL-0037 for every analytical/extraction reuse layer exercised;
- passes EVAL-0039 or an approved successor acquisition/application-provenance
  case;
- passes EVAL-0033 through EVAL-0035, or approved successor cases, for every
  security boundary exercised by M1;
- passes EVAL-0046 or an approved successor non-mutation case for every external
  tool operation exercised by M1;
- passes EVAL-0080 or an approved successor write-isolation case for every
  product write surface exercised by M1;
- passes EVAL-0089 or an approved successor before any authenticated provider
  integration is exercised;
- passes EVAL-0016 or an approved successor pinned real-mod case;
- passes EVAL-0017 or an approved successor pinned real case from a materially
  different accepted taxonomy region.

## Real-mod candidate selection criteria

Prefer:

- documented intent;
- exact obtainable versions;
- small dependency surface;
- inspectable plugin/assets;
- author or community patch that helps establish intended resolution;
- a meaningful but bounded symptom;
- materially different accepted taxonomy facets, including cross-cutting or
  currently under-evaluated areas where practical;
- legal/policy-compliant acquisition.

Candidate research may use the creator's installed profile and online primary
sources. RESEARCH-0035 selected `REAL-NPC-0001` and `REAL-REFR-0001` as the
current EVAL-0016/EVAL-0017 candidates; their exact private manifests and
independent claim boundaries remain controlled by that investigation. Wave F
must accept the final case specifications before fixture execution.

## EVAL-0052 and EVAL-0086 held-out clarification

The accepted [final scope amendment](evaluator-history.md)
narrows only the held-out proof surfaces of EVAL-0052 and applicable
EVAL-0086 assertions. Exact failure vocabulary, typed AIDT subfields, and
product taxonomy/provenance IDs remain public-conformance assertions. The
historical protocol `/4` oracle partition used result publication, AIDT
presence, and semantic taxonomy tuples instead.
The accepted
[semantic-authority owner disposition](evaluator-history.md)
historically supplied the bounded rules without changing protocol `/4` or
projection `3.0.0`.

ADR-0032 defers the current private held-out partition with no valid product
verdict and retires protocol `/5` unqualified. ADR-0033 retires and archives
protocol `/4` with no current review role. Public EVAL-0052 and applicable
EVAL-0086 conformance for Slice 4 remains valid for exact candidate `a98d648`
and scope.
For later M1 slices, these and all remaining cases are executed as public
development/validation evidence under the
[product-conformance verification profile](product-conformance-verification-profile.md).
Slice 7 must prove the generic mechanism across actor/AI/FaceGen and
REFR/link/placement with matched negatives. Slice 8 must run controlled-real
EVAL-0016 and EVAL-0017. Neither creates a private held-out verdict or M3
reliability/readiness claim.
