# Evaluation case catalog

Status: Draft  
Last reviewed: 2026-07-25

No executable evaluation fixtures have been created for the rewrite. This
catalog records planned cases; each requires its own reviewed specification
before implementation. The Requirements column provides baseline traceability;
a case specification may refine those links but shall not silently drop a
requirement it is used to validate.

The `Domain` values below are provisional case-catalog descriptors for planning
and navigation. They are not an accepted taxonomy of Skyrim game areas,
technical modification surfaces, or consequences. Case specifications shall
adopt the accepted versioned taxonomy resulting from RQ-036 without rewriting
what these historical planning labels meant.

| ID | Type | Provisional domain label | Expected purpose | Requirements | Status |
|---|---|---|---|---|---|
| EVAL-0001 | Synthetic positive | NPC/cross-layer | Appearance mod unintentionally reverts behavioral edits | ANALYSIS-004, ANALYSIS-005, FIND-003 | Planned |
| EVAL-0002 | Synthetic negative | NPC/cross-layer | Structurally similar reversion is documented/intentional | ANALYSIS-003, ANALYSIS-004, EVID-006, INTENT-003 | Planned |
| EVAL-0003 | Synthetic positive | Patch | Correct patch preserves appearance and behavior | ANALYSIS-007 | Planned |
| EVAL-0004 | Synthetic positive | Patch | Patch exists but is later overwritten | ANALYSIS-007 | Planned |
| EVAL-0005 | Synthetic positive | Patch/version | Patch targets an older upstream version | ANALYSIS-007, ANALYSIS-008 | Planned |
| EVAL-0006 | Synthetic positive | Cell/quest | Visual cell edit reverts a quest-relevant reference | ANALYSIS-004, ANALYSIS-005, FIND-003 | Planned |
| EVAL-0007 | Synthetic negative | Cell | Intentional cell override with no lost required behavior | ANALYSIS-003, EVID-006 | Planned |
| EVAL-0008 | Synthetic positive | Item/crafting | Visual item change loses keywords or recipe behavior | ANALYSIS-004, ANALYSIS-005 | Planned |
| EVAL-0009 | Synthetic negative | Asset | Ordinary texture/mesh overwrite is not a finding | ANALYSIS-006, UX-004 | Planned |
| EVAL-0010 | Synthetic positive | Documentation | Applicable incompatibility/requirement is extracted and matched | DOC-003, EVID-002, ANALYSIS-001 | Planned |
| EVAL-0011 | Synthetic negative | Documentation | Conditional claim does not apply to installed versions/options | DOC-005, EVID-006 | Planned |
| EVAL-0012 | Synthetic boundary | Intent | Ambiguous purpose creates needs-input/abstention | INTENT-001, INTENT-002, EVID-006 | Planned |
| EVAL-0013 | Synthetic positive | Snapshot | Relevant change invalidates dependent result | SNAP-002, SNAP-004 | Planned |
| EVAL-0014 | Synthetic negative/boundary | Snapshot | A proven unrelated change permits explained carryover, while ambiguous impact requires recomputation, explicit skipping, or dependency-evaluable typed input and cannot become validated through bare “reuse anyway” confirmation | SNAP-004, SCAN-007 | Planned |
| EVAL-0015 | Synthetic failure | Coverage | Analyzer failure is isolated and visible | SCAN-006, COVER-001, COVER-003 | Planned |
| EVAL-0016 | Real mods | NPC/cross-layer | First pinned real scope-incongruent reversion case | ANALYSIS-004, ANALYSIS-005 | Research required |
| EVAL-0017 | Real mods | Non-NPC | First pinned real generalization case | ANALYSIS-004, ANALYSIS-005 | Research required |
| EVAL-0018 | Scale | Operational | High-end profile progress, resume, and cost behavior | SCAN-004, SCAN-005, AI-004, OPS-004 | Planned for M3 |
| EVAL-0019 | Synthetic positive | Analysis context | Relevant intent, identity, or local claim-applicability change invalidates dependent results, while confirmed assumptions remain scoped and dependency-validated rather than becoming universal rules | SNAP-002, SNAP-004, INTENT-004, INTENT-005, DOC-003, DOC-004 | Planned |
| EVAL-0020 | Synthetic negative | Review state | Finding disposition or note does not alter analyzer output/context | FIND-005, FIND-006 | Planned |
| EVAL-0021 | Synthetic boundary | Reproducibility | Changed live source creates a distinct run while a complete retained replay remains stable | SNAP-001, SNAP-005, SNAP-006, AI-006 | Planned |
| EVAL-0022 | Synthetic boundary | Runtime evidence | Historical or weakly matched log is not auto-applied | VALID-003 | Planned |
| EVAL-0023 | Synthetic negative | Scan configuration | Tracing/cache/concurrency change does not create a semantic context or by itself invalidate prior equivalent artifacts | SNAP-001, SNAP-004, SCAN-009 | Planned |
| EVAL-0024 | Synthetic positive | Carryover/history | Reused finding/case revision preserves original provenance and records a reuse edge | SNAP-004, FIND-006 | Planned |
| EVAL-0025 | Synthetic failure | Retention | Missing original input reports replay and audit gaps without rewriting surviving history | SNAP-006, DOC-008, OPS-002 | Planned |
| EVAL-0026 | Synthetic boundary | Run immutability | Mid-run context/config edit creates a new version and never mutates the active run | SNAP-002, SCAN-009, INTENT-004 | Planned |
| EVAL-0027 | Synthetic boundary | Readiness | Targeted run never borrows/overwrites prior coverage; only full-policy carryover replaces readiness, while other targeted results show explicit scope-limited/no-readiness status and newer applicable evidence can stale the old result | SNAP-003, FIND-007, FIND-008, FIND-010, FIND-012, COVER-003, UX-001 | Planned |
| EVAL-0028 | Synthetic boundary | Claim adjudication | Source extraction correction is reusable while local non-applicability remains context-scoped | DOC-004 | Planned |
| EVAL-0029 | Synthetic positive | Runtime evidence | Test evidence alone mutates nothing; manual reanalysis creates linked finding/case revisions | VALID-001, VALID-002, VALID-003, VALID-005 | Planned |
| EVAL-0030 | Synthetic boundary | Severity/confidence/maturity | Plausible catastrophic impact retains severity but does not auto-block below evidence/maturity policy; maturity-based presentation never relabels a finding as a lead or a lead as a finding, and review confirmation alone cannot promote an under-threshold lead without additional qualifying typed evidence | FIND-001, FIND-009 | Planned |
| EVAL-0031 | Synthetic boundary | Freshness | Refresh-vs-reuse choice changes execution configuration; resolved source revisions change run inputs; stale-evidence acceptance changes semantic context | DOC-009, SNAP-001 | Planned |
| EVAL-0032 | Synthetic scale | Candidate selection | Indexed selection retains planted interactions without naïve all-pairs LLM calls, and every candidate retains its originating analysis run/analyzer, rationale, supporting evidence, scoped population, and validity dependencies | EVID-005, ANALYSIS-017, OPS-004 | Planned |
| EVAL-0033 | Synthetic adversarial | Untrusted content | Prompt/HTML/tool-output instructions cannot grant authority or alter analysis policy | SEC-001 | Planned |
| EVAL-0034 | Synthetic adversarial | Credentials/context minimization | Secrets and unnecessary usernames, absolute paths, and unrelated values are absent from prompts, logs, traces, and exports while required non-secret context remains usable; confirmed credential deletion prevents queued/new/retry or reserved-but-undispatched use, releases unused budget reservation, and leaves uncancellable in-flight work disclosed | SEC-002, SEC-004, AI-003, AI-004 | Planned |
| EVAL-0035 | Synthetic adversarial | Privileged boundary | Out-of-scope path, URL, command, and tool arguments are rejected | AUTH-002, SEC-003 | Planned |
| EVAL-0036 | Synthetic boundary | Case presentation | Lead-only investigation is counted separately, shows hypothesis status, and cannot affect readiness | FIND-002, FIND-011, UX-001 | Planned |
| EVAL-0037 | Synthetic boundary | Clean/freshness policy | Clean analysis or extraction bypasses only its selected derived layer without implicitly refreshing source bytes; explicit refresh is separately visible | SCAN-007, DOC-011 | Planned |
| EVAL-0038 | Synthetic boundary | Job lifecycle | Pause resumes the same run and stops all new attached-child work by default; cancellation is terminal; terminal retry or changed-config continuation uses a new user-initiated run; node-scoped limit exhaustion leaves unrelated parent work active; child continuation/detachment is explicit and preserves provenance | SCAN-005, SCAN-006, AI-004 | Planned |
| EVAL-0039 | Synthetic boundary | Documentation acquisition | Independent acquisition retains source-run provenance/coverage; profile application is explicit and local documents retain snapshot provenance | DOC-002, DOC-011 | Planned |
| EVAL-0040 | Synthetic boundary | Run output/export | M1 emits human-readable CLI and versioned JSON as run-owned output; later user-created exports remain distinct and record exact readiness-evaluation/source selection, sharing class, configuration, privacy/source-policy decisions, and omissions; restricted material is excluded from externally shareable output; creation/deletion does not mutate source results | SEC-004, OPS-003 | Planned from M1, extended at each export milestone |
| EVAL-0041 | Synthetic failure | Retention/job state | Deletion preview exposes active/paused resumability, reuse impact, and independently retained exports, run-owned outputs, or traces containing selected material; explicitly scoped direct or confirmed-cascade deletion creates honest gaps without corrupting or silently deleting surviving work | SCAN-005, SNAP-006, OPS-002 | Planned |
| EVAL-0042 | Synthetic boundary | Symptom report | Initial diagnostic submission explicitly starts one bounded run; later report/clarification revisions remain user-statement evidence and create no run unless analytical follow-up is explicitly initiated; consuming runs/revisions never rewrite earlier history | SCOPE-004, SNAP-001, VALID-005 | Planned |
| EVAL-0043 | Synthetic boundary | Assumption lifecycle | Inferred/user-provided origin remains distinct from confirmation; create/edit/delete makes new context state without rewriting history | INTENT-004, INTENT-005 | Planned |
| EVAL-0044 | Synthetic boundary | Cost accounting | Attached child acquisition is included once in every applicable parent/child limit and rollup without duplicated ownership; reused historical cost remains separate; detachment freezes the parent contribution and later acquisition spend is separately authorized/attributed | SCAN-004, SCAN-005, AI-004 | Planned |
| EVAL-0045 | Synthetic boundary | Manual initiation | Profile/source changes do not trigger analysis or paid/network work; configured children run only under a user-initiated parent | SCOPE-004 | Planned |
| EVAL-0046 | Integration safety | External tools | Approved operation leaves MO2/mod/game/profile state unchanged and records every allowed cache/temp side effect | AUTH-001, AUTH-003 | Research required |
| EVAL-0047 | Synthetic boundary | Suppression lifecycle | Equivalent finding carries suppression with provenance; materially changed revision remains visible and retains old suppression only as history | FIND-005, FIND-006 | Planned |
| EVAL-0048 | Synthetic boundary | Advisory readiness | Unreviewed advisory stays visible/countable without changing readiness; explicit action-required disposition does change it | FIND-013 | Planned |
| EVAL-0049 | Synthetic boundary | Remediation/validation | A supported remediation states risks, reversibility, and meaningful verification; an unsupported resolution instead produces a snapshot/case-scoped, bounded validation or missing-evidence plan with save/test risks and inconclusive outcomes, and neither makes a global-safety claim | PROD-004, FIND-004, VALID-001 | Planned |
| EVAL-0050 | Synthetic boundary | Resolution state | User-resolved disposition is visibly unverified until new evidence validates it; verification creates new evidence/review history and only the analytical finding/case revisions actually produced by reanalysis, without rewriting the decision or prior finding | FIND-005, FIND-006 | Planned |
| EVAL-0051 | Integration ground truth | MO2 effective state | Selected profile, enabled state/order, loose/archive provider chains, and hidden/deleted/unmanaged state agree with authoritative MO2 behavior | SCOPE-003, SCOPE-005, SNAP-001 | Research required |
| EVAL-0052 | Integration ground truth | Bethesda records | Supported override chains, links, and winners agree with xEdit ground truth; unsupported semantics become explicit gaps | SCOPE-005, ANALYSIS-003, COVER-001 | Research required |
| EVAL-0053 | Integration fidelity | LOOT | The exact invoked masterlist, prelude, userlist, configuration, and diagnostics are reproduced with curated and user-supplied authority kept distinct | AUTH-003, EVID-003, ANALYSIS-002 | Research required |
| EVAL-0054 | Synthetic boundary | Supported target | Unsupported manager/runtime/platform inputs fail clearly without best-effort semantic conclusions or fabricated coverage | SCOPE-001, SCOPE-002, SCOPE-006 | Planned |
| EVAL-0055 | Synthetic coverage | Documentation | Full enabled-mod mode accounts for every eligible identifiable mod and reports exclusions, limits, failures, and unresolved identities | COVER-001, COVER-002, DOC-001 | Planned |
| EVAL-0056 | Workflow evaluation | Frontend | Experienced mod users can complete the finding-centric preflight workflow; summary-first review, progressive disclosure, the user-facing scan-configuration controls delivered for M2, and large-result navigation remain understandable and responsive | PROD-001, PROD-002, PROD-003, SCAN-009, UX-001, UX-002, UX-003, UX-004, UX-005, UX-006, OPS-005 | Planned for M2 |
| EVAL-0057 | Integration ground truth | Root/native state | Runtime, loaders, native components, unmanaged root files, and installed-version relationships are reconstructed without inferring unsupported compatibility | ANALYSIS-008, ANALYSIS-009 | Research required |
| EVAL-0058 | Synthetic positive | Generated output | A supported generator's stale or mismatched output is detected from declared inputs/outputs, while an unsupported generator produces bounded observations and explicit gaps | ANALYSIS-010, COVER-001 | Planned |
| EVAL-0059 | Synthetic positive | Referenced assets | A format-specific reference to a missing effective asset is detected with the expected provider/reference provenance | ANALYSIS-013 | Planned |
| EVAL-0060 | Synthetic positive | Performance/stability | A concrete documented or locally measured instability mechanism is reported with bounded impact and evidence | ANALYSIS-014 | Planned |
| EVAL-0061 | Synthetic negative | Performance/stability | Generic texture/script heaviness or hardware speculation produces no performance finding | ANALYSIS-014, EVID-006 | Planned |
| EVAL-0062 | Synthetic positive | Playthrough lifecycle | Applicable new-game, install, upgrade, removal, or regeneration instructions are extracted and matched to the installed state | ANALYSIS-015, DOC-005 | Planned |
| EVAL-0063 | Synthetic positive | Requirements/masters | A missing, disabled, or incompatible master/requirement is reported only when its applicability is established | ANALYSIS-001, EVID-003 | Planned |
| EVAL-0064 | Contract/integration | Offline/provider boundary | A local-only run requires no provider credentials; unavailable network/LLM capabilities are explicit; provider selection and equivalent provider adapters do not alter core evidence contracts | AI-001, AI-002, OPS-001 | Planned |
| EVAL-0065 | Contract/boundary | Analyzer modularity | One analyzer can be configured and run independently with declared scope, dependencies, evidence threshold, coverage, cost/scale, maturity, and evaluation links intact | SCAN-001, ANALYSIS-016 | Planned for M1 |
| EVAL-0066 | Calibrated operational | Estimate/presets | Pre-run time/cost/coverage estimates and user presets are derived from retained measurements, expose uncertainty and overrides, and respect configured limits | SCAN-003, SCAN-010 | Planned for M3 |
| EVAL-0067 | Synthetic contract | Evidence/LLM transparency | Typed observations, claims, candidates, hypotheses, findings, recommendations, and gaps remain distinct; LLM involvement and raw development intermediates are retained and visible as required | EVID-001, EVID-004, EVID-007, OPS-002 | Planned for M1 |
| EVAL-0068 | Synthetic boundary | Source policy/history | Prohibited access is not attempted; supported local documentation is processed; retained/deleted passages and revision metadata produce honest history, audit, and replay disclosures | DOC-006, DOC-007, DOC-008 | Planned |
| EVAL-0069 | Synthetic boundary | Disposition/readiness | Per-finding decisions remain canonical; accepted risk or readiness-policy change creates a new time/policy/disposition-bound evaluation without rewriting the run, semantic context, or prior evaluations; case-level bulk action records each member change | FIND-007, FIND-008, FIND-009, FIND-010, FIND-012 | Planned |
| EVAL-0070 | Operational | Resource defaults | Conservative defaults permit background use, basic user limits are honored, and limiting one resource exposes resulting delay or coverage loss | SCAN-008 | Planned if delivered |
| EVAL-0071 | Synthetic positive/negative | Configuration | Supported syntax, winner, schema, and documentation rules detect planted defects while arbitrary unsupported semantics produce gaps rather than guesses | ANALYSIS-011 | Planned if delivered |
| EVAL-0072 | Synthetic boundary | Installer choices | Installed files and retained archives support a bounded likely choice or explicit ambiguity without claiming exact FOMOD history | ANALYSIS-012 | Planned if delivered |
| EVAL-0073 | Synthetic adversarial | Broader web search | Opt-in broader search obeys the source registry and preserves community authority instead of promoting model-selected results | DOC-010 | Planned if delivered |
| EVAL-0074 | Synthetic boundary | Manual log import | User-initiated import records snapshot/session provenance and preserves weak or unknown association without automatic application | VALID-003, VALID-004 | Planned if delivered |
| EVAL-0075 | Integration boundary | Tracked test session | A manually delimited session correlates evidence without launching the game, automating success, or changing setup state | AUTH-001, VALID-006 | Planned if delivered |
| EVAL-0076 | Contract/boundary | Provider capabilities | Supported quota/usage, finite consumptive hard-limit bounds, and billing-reconciliation latency are shown accurately near configuration and pre-run review while unavailable or unreliable capabilities are explicit | SCAN-003, AI-005 | Planned before first billable provider integration |
| EVAL-0077 | Contract/adversarial | Provider billing authority | No authenticated or billable model call occurs without current user-supplied authorization for the selected account; usage/cost is attributed correctly and no project/shared credential fallback exists | AI-004, AI-007 | Planned for first authenticated provider integration |
| EVAL-0078 | Synthetic positive/negative | Change impact | Relevant cross-snapshot/context changes are explained and invalidate only dependent work; unrelated changes carry over, and a user-designated reference is never treated as correctness proof | SNAP-004, UX-005, ANALYSIS-018 | Planned if delivered |
| EVAL-0079 | Synthetic positive/negative | Finding/case lineage | Equivalent or explicitly superseding outputs reconcile to stable logical finding/case lineage with valid disposition carryover only where applicable, while similar-but-distinct causes or changed applicability remain separate; reviewed merges/splits create explicit lineage | FIND-006, FIND-014 | Planned for M2 |
| EVAL-0080 | Synthetic/integration safety | Product writes | Every exercised settings, credential, cache/temp, history/checkpoint, trace, export, deletion, and update-staging path stays within its approved product-controlled, OS-backed, or explicitly selected non-protected authority; direct and aliased/reparse-point paths into protected setup roots are rejected and remain unchanged | AUTH-001, AUTH-002, SEC-003 | Planned for M1 |
| EVAL-0081 | Synthetic/adversarial | Budget enforcement | Concurrent parent/child billable work atomically reserves one declared worst-case amount against every applicable consumptive hard limit, passes hard-deadline checks before dispatch, reconciles to one owned actual ledger entry, cannot oversubscribe shared budget, rejects adapters without the required finite bound, and reports in-flight deadline or provider-side variance without starting further work | SCAN-004, SCAN-005, AI-004 | Planned before concurrent billable execution |
| EVAL-0082 | Contract/boundary | Development controls | The M1 CLI/config contract independently controls analyzers, sources, budgets, cache policy, and tracing; the effective values are retained, and every enabled analyzer contributes its raw typed output without preset or maturity-based suppression | SCAN-002, SCAN-009, EVID-007 | Planned for M1 |
| EVAL-0083 | Synthetic/integration provenance | End-to-end provenance | A material conclusion retains applicable versions/times, identities or fingerprints, supporting and contradicting evidence, and an inspectable processing chain across every exercised local, deterministic-tool, external-claim, and model boundary; deleted or unavailable inputs create honest audit/replay gaps rather than fabricated provenance | EVID-002, SNAP-006, AI-006 | Planned for M1 scope and extended with each new boundary |
| EVAL-0084 | Synthetic positive/negative | Causal case grouping | Multiple findings with one supported likely cause group into one case, while findings sharing only a mod or record family but having distinct causes remain separate; lead-only investigations remain visibly distinct from supported cases | FIND-002 | Planned for M1 |
| EVAL-0085 | Synthetic boundary | Coverage/readiness presentation | Different coverage populations retain labeled denominators and states rather than one combined analyzed/safety percentage; no-findings output remains qualified by gaps and uncertainty and never claims the playthrough is safe | PROD-004, COVER-001, COVER-002, COVER-003 | Planned for M1 output and M2 presentation |
| EVAL-0086 | Synthetic/controlled real | Taxonomy classification | Representative single-area, cross-cutting, multi-surface, and unknown/unsupported interactions use the accepted taxonomy without conflating declared purpose, hosting-site category, technical modification surface, affected game area, consequence, severity, or effect extent; historical outputs retain their taxonomy version | FIND-001, COVER-002, ANALYSIS-016 | Planned after RQ-036 |

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
- passes EVAL-0016 or an approved successor pinned real-mod case;
- passes EVAL-0017 or an approved successor pinned real non-NPC case.

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
sources. The product baseline was accepted on 2026-07-25; selection may proceed
when scheduled by an accepted M0 research plan.
