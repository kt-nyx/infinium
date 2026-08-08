# Open research questions

Status: Active register

Last reviewed: 2026-08-08

The product baseline was accepted on 2026-07-25. The
[M0 research-foundation plan](../plans/milestones/M0-research-foundation.md) is
Accepted and sequences these investigations. Status is updated only from a
bounded investigation and the applicable reviewed or accepted disposition; no
item is answered merely because the legacy code chose an approach.

RQ-026's resolution concludes licensing and the high-level integration posture.
RQ-001 through RQ-007 and RQ-014 now have completed investigations and a
reviewed, accepted Wave B disposition in
[RESEARCH-0013](investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md).
ADR-0007 through ADR-0011 resolve them for M0. Their named conformance,
supported-surface, and implementation gates remain open without reopening the
accepted product-licence or user-installed-application boundary.

Wave C's ten investigations and recommendations were accepted by the project
owner on 2026-07-25. The remaining RQ-023 and RQ-025 qualification work was
completed and accepted through RESEARCH-0034/0035 on 2026-07-28, closing Gate
C. This still distinguishes a qualified decision boundary or controlled-real
candidate from an executed support claim: EVAL-0016/EVAL-0017 and the relevant
analyzers have not passed implementation evaluation.

Wave D's original research is retained in RESEARCH-0025 through
RESEARCH-0029. The 2026-07-28 revision in RESEARCH-0030 through RESEARCH-0033
completed authenticated Nexus qualification, current-compatible LOOT
freshness research, and the OpenAI-first capability reassessment. The former
authenticated-Nexus and separate GraphQL-policy blockers are removed by
accepted ADR-0012. The owner accepted the integrated Wave D disposition and
ADR-0013/ADR-0014 on 2026-07-28. Gate D is met at the M0 research/design
layer. Production credential, adapter, budget, and evaluation conformance
remain later gates.

Wave E's eight original bounded investigations and independent integration are
complete in RESEARCH-0036 through RESEARCH-0044. RESEARCH-0045 investigated a
Codex/ChatGPT-plan access amendment; the owner rejected that recommendation
and ADR-0024 records the disposition. RESEARCH-0046 records the owner's
decision to reject Dapr without a comparison prototype and accept the
application-owned SQLite lifecycle in ADR-0016. ADR-0018 accepts the process
and authority topology. The owner subsequently accepted every remaining
required Wave E decision in ADR-0015, ADR-0017, and ADR-0019 through ADR-0023.
Gate E is met at the M0 architecture/design layer.
Implementation and
EVAL-0079/EVAL-0087 through EVAL-0089 conformance remain later gates.

Wave F specification work is integrated, independently reviewed, and accepted.
RESEARCH-0047 provides RQ-028's empirical calibration plan without inventing
thresholds; RQ-029 is scheduled before automatic runtime-log application and
RQ-030 for M4 packaging/update planning. RESEARCH-0048 closes RQ-038 through
accepted ADR-0025.

| ID | Question | Decision enabled | Status |
|---|---|---|---|
| RQ-001 | How can Infinium obtain authoritative MO2 profile and effective VFS state by deterministic reconstruction or bounded execution through the user's MO2, and is direct USVFS operation ever necessary? | MO2 integration ADR | Resolved for M0 by ADR-0008; Slice 3 implementation and EVAL-0051 passed for the exact admitted target, while broader MO2 versions/mappers/archive members remain unsupported |
| RQ-002 | Does MO2 expose a current/last-selected profile reliably? | Profile selection behavior | Resolved for M0 by ADR-0008; saved selection is suggestion-only |
| RQ-003 | Which exact Skyrim SE runtime version is initially pinned, how is it detected, and how is support deliberately advanced? | Runtime support contract | Resolved for M0 by ADR-0009; Slice 3 implementation and EVAL-0054 passed for the exact admitted target, while public-release breadth remains pending |
| RQ-004 | Which exact Mutagen.Bethesda package and authority boundary should Infinium accept, and which Skyrim SE plugin, archive, string, record, and field surfaces must be positively qualified or excluded? | Bethesda semantic layer ADR | Resolved for bounded M1 by ADR-0009, the Option A RESEARCH-0053 fixture correction, and ADR-0028/ADR-0029. Slice 4 public conformance passed for exact candidate `a98d648` and scope; archive/string and non-allowlisted breadth remain explicit gaps, and no private held-out verdict exists. |
| RQ-005 | Can supported invocation of the user-installed LOOT application provide the required structured, deterministic, non-mutating evidence; if not, which needs justify a pinned bundled libloot dependency, and how are LOOT data inputs managed? | LOOT integration ADR | Resolved for M0 by ADR-0011; LOOT delivery remains milestone-conditional and qualification-gated |
| RQ-006 | Which functions of user-installed xEdit provide unique automated or ground-truth value, and which detection, version, invocation, cache/temp, and failure contract is supported? | External-tool scope | Resolved by ADR-0007: xEdit is excluded from every Infinium boundary; its investigated oracle role was rejected |
| RQ-007 | What metadata does MO2 retain about Nexus identity, source archives, FOMOD choices, hidden files, and manual changes? | Identity/FOMOD design | Resolved for M0 by ADR-0008; bounded FOMOD reconstruction remains later work |
| RQ-008 | Which current Nexus-provided read APIs/interfaces provide descriptions, requirements, articles, changelogs, files, posts, and revision identity? | Nexus adapter/source coverage | Resolved for M0 by ADR-0012 and authenticated RESEARCH-0030: latest-capable v3, then v2 GraphQL, then v1 per-content routing; unsupported articles/mod posts/stickies/mod bug reports remain explicit gaps |
| RQ-009 | Which Nexus policies constrain scraping, caching, redistribution, and public application registration? | Source and distribution policy | Answered for M0 by ADR-0005; Nexus confirmation pending |
| RQ-010 | Which non-Nexus sources should be approved, and how can they be searched legally and reliably? | Source registry | Resolved for M0 by ADR-0013/ADR-0014 and the accepted Wave D source-registry dispositions: local documentation and LOOT managed data form the minimal core, GitHub-hosted mod docs are optional/later, and governed OpenAI web search is discovery only; exact landing-source adapters remain later |
| RQ-011 | What is the smallest safe LLM semantic/capability boundary without letting provider transport become domain truth? | LLM provider ADR | Resolved for M0 by ADR-0013: preserve the two schema-bound semantic operations and host admission while allowing governed OpenAI-specific search and execution modes outside provider-independent domain truth |
| RQ-012 | Which initial-provider authentication modes and APIs support user-owned usage, useful LLM/search capabilities, structured output, execution modes, cost, quota, rate limits, and model identity? | Provider capability and user-owned-usage design | Resolved for M0 by ADR-0013: OpenAI Responses, Structured Outputs, hosted search, and separately qualified background/Batch/cache capabilities form the initial boundary; exact credential/model/account/cost/cancellation/retention conformance remains later |
| RQ-013 | How should reusable documentation/LLM evidence, acquisition runs, source revisions, and profile-application links be stored, cached, and versioned? | Evidence store/cache ADR | Resolved for M0 by accepted ADR-0015: SQLite authority plus coordinator-owned content-addressed payload storage |
| RQ-014 | Which fingerprint/dependency strategy proves installation-snapshot and cache validity without prohibitive IO? | Snapshot/cache ADR | Resolved for M0 by ADR-0010; exact schema and implementation conformance pending |
| RQ-015 | Which job store/process model supports linked analysis/acquisition runs, same-run pause/resume, terminal cancellation, checkpoint reuse into a new run, retries, single-owner cost rollups, deletion safety, and UI restarts? | Worker/job architecture ADR | Resolved for M0 by accepted ADR-0016: application-owned transactional SQLite lifecycle and bounded scheduler; implementation/fault conformance pending |
| RQ-016 | Which desktop/application stack best satisfies UI, security, deployment, and analysis-isolation requirements: Electron, Avalonia, Tauri/WebView2, or another design? | Application stack ADR | Resolved for M0 by accepted ADR-0017: .NET 10 engine/CLI, React/TypeScript, and a minimal WPF/WebView2 host |
| RQ-017 | Which process and data-query boundary, including whether IPC is needed, keeps the UI responsive at high-end scale? | Process/data-access ADR | Resolved for M0 by accepted ADR-0018/ADR-0019: standalone coordinator/process authority and bounded role-separated named-pipe gRPC contracts |
| RQ-018 | Which secure credential-entry and storage mechanisms fit the selected desktop architecture? | Security ADR | Resolved for M0 by accepted ADR-0020: Credential Manager generic credentials plus an exact-target one-shot helper |
| RQ-019 | Which root-level Skyrim components can be identified and version-checked deterministically? | Native/root analyzer catalog | Researched; bounded static inventory and layered-identity recommendation accepted; named-analyzer qualification remains conditional |
| RQ-020 | Which generated-output tools expose usable manifests or stable formats? | Generator analyzer roadmap | Researched; generic inspection plus version-pinned adapter roadmap accepted; named generator qualification remains later |
| RQ-021 | Which configuration ecosystems merit named schemas first? | Configuration roadmap | Researched; generic layer followed by MCM Helper, SPID/KID/BOS, and OAR roadmap accepted |
| RQ-022 | How far can compiled Papyrus be analyzed structurally and semantically without unreliable claims? | Script analyzer scope | Researched; bounded static PEX/VMAD structural contract accepted; generic behavior and performance claims excluded |
| RQ-023 | Which asset formats can be checked for referenced-file completeness efficiently? | Asset analyzer scope | Resolved for M0; NIF-first typed-reference scope selected and RESEARCH-0034 qualified the loose-only FaceGen decision boundary for pre-resolved record/provider inputs; archive-positive support and production adapter conformance remain later work |
| RQ-024 | Which semantic record families and field relationships should follow the first proof? | Analyzer roadmap | Resolved for M0 by an accepted generic substrate → bounded first-category proof → materially different category proof roadmap; current candidates use actor/AI/FaceGen then REFR/placement/link semantics, while exact implementation-shape conformance remains later work |
| RQ-025 | Which real mod combinations provide stable, redistributable or locally reproducible evaluation cases? | Evaluation corpus | Resolved for M0; RESEARCH-0035 pins independently grounded, locally reconstructible EVAL-0016 and materially different EVAL-0017 candidates with matched controls; accepted Wave F specifications define them and M1 owns fixture construction/execution |
| RQ-026 | What licensing/distribution obligations apply to bundled helpers and external tools? | Packaging/licensing ADR | Resolved for M0 by ADR-0006 |
| RQ-027 | What high-end time, memory, disk, and cost baselines are realistic on the creator's profile? | Performance budgets/presets | Answered for M0 at method and rough-feasibility level; exact budgets deferred until authoritative adapters and an architecture prototype exist |
| RQ-028 | What evidence, analyzer-maturity, false-positive, coverage, targeted-run carryover, and stale-result thresholds govern M3/M4 readiness and user-facing filtering? | Readiness and maturity acceptance policy | Calibration protocol accepted from RESEARCH-0047; numerical thresholds remain later empirical evidence collected during and after M1 |
| RQ-029 | Which fingerprints and capture workflows can classify imported or tracked logs as exact, matched, likely, unknown, or historical? | Runtime-evidence provenance design | Scheduled before automatic runtime-log application, no later than the M3 delivery plan |
| RQ-030 | Which packaging, signing, update, and distribution mechanisms fit M4 without weakening the local security boundary? | Packaging/update ADR | Scheduled for M4 packaging/update planning after application architecture qualification |
| RQ-031 | Which source bytes, tool/model boundary outputs, and tool/model/executable versions may be retained legally and practically; which retained content may appear in each export sharing class; and what replay and redistribution guarantees follow? | Retention/replayability/export policy | Answered for M0 with source-specific conditions and measured-storage follow-up |
| RQ-032 | Which sanitization, navigation, protected-root/write-destination authorization, subprocess, and export-redaction controls satisfy AUTH-002, SEC-001, SEC-003, and SEC-004 in the selected architecture? | Security-boundary ADR | Resolved for M0 by accepted ADR-0021's layered desktop and local-operation security boundary |
| RQ-033 | Which causal/dependency continuity keys and reconciliation workflow can link or explicitly supersede logical findings and cases across runs without false merges, false splits, or disposition leakage? | Finding/case identity and lineage design | Resolved for M0 by accepted ADR-0022's evidence-bearing, append-only continuity and reconciliation model |
| RQ-034 | Which deadline-check, atomic reservation, and reconciliation model can enforce concurrent operation/acquisition/analysis hard limits across providers, including elapsed-time deadlines, maximum-call bounds, batching, cancellation, rounding, delayed billing, and adapter capability gaps? | Cost-ledger and budget-enforcement design | Resolved for M0 by accepted ADR-0023's atomic multi-scope reservation and usage ledger; rejected ADR-0024 adds no plan-mode path |
| RQ-035 | Which local indexes, interaction-graph representation, candidate-generation rules, and staged ranking strategy can retain meaningful interactions at high-end scale without naïve all-pairs model comparison? | M1 candidate-selection design and performance plan | Resolved for M0 by accepted typed-index, causal-join, canonical-participant, mandatory-lane design; independent production evaluation remains pending |
| RQ-036 | What purposes/intended feature areas do Skyrim SE mods declare; through which technical surfaces can they alter effective state; which game systems/content areas, consequence types, and effect extents can they affect; and which distinct empirically grounded taxonomies should Infinium use without conflating them? | M1 product taxonomy specification plus analyzer, coverage, navigation, and evaluation coverage map | Resolved for M0 by accepted `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`; unevaluated regions remain explicit |
| RQ-037 | Which OpenAI user-owned access modes can Infinium support—direct Platform API usage, ChatGPT/Codex subscription access, or both—and how must authentication, execution, billing, usage visibility, security, provenance, and hard-limit capabilities differ? | OpenAI access-mode ADR and credential/cost amendments | Closed by owner disposition: use user-supplied, usage-priced Platform API keys with direct Responses; reject Codex/ChatGPT-plan core integration in ADR-0024 |
| RQ-038 | Which exact OpenAI model identity and synchronous Responses profile should M1 qualify for its two semantic operations, and how must capability drift be handled when no immutable snapshot exists? | Exact M1 provider-profile ADR | Resolved for M1 by accepted ADR-0025 from RESEARCH-0048; implementation/evaluation conformance pending |
| RQ-039 | Does the exact admitted Skyrim SE game plugin provide a secondary Data root or Data-contributing mapper, and how should EVAL-0051 treat an empty exact-target inventory? | Slice 3 mapper inventory and EVAL-0051 plan/specification disposition | Resolved by accepted RESEARCH-0051: the exact additional-mapper inventory is empty, the production allowlist remains empty, and a positive real additional-mapper case is conditional on future deliberate qualification |
| RQ-040 | How should evaluator-private validation and held-out fixtures remain versioned and autonomously maintainable without exposing answers to ordinary implementation agents? | Evaluator-private repository and delegated agent-access ADR | Resolved by accepted RESEARCH-0052 and ADR-0026: separate private sibling Git history, sanitized public bindings, purpose-bound fresh-context delegates, access records, and explicit contamination/replacement transitions |

## RQ-036 accepted result and change discipline

[RESEARCH-0021](investigations/RESEARCH-0021-skyrim-mod-impact-taxonomy.md)
produced the accepted
[Skyrim SE mod-impact taxonomy](../product/mod-impact-taxonomy.md). It keeps
declared purpose and intended target, observed technical surface, affected
area, consequence, and faceted extent distinct; supports multi-label,
cross-cutting, unknown, unsupported, unmapped, and not-applicable assignments;
and preserves severity, confidence, symptoms, evidence authority, and
readiness as separate concepts.

RQ-004 and RQ-019 through RQ-024 remain specialized technical-surface,
format, or analyzer-family questions. Their future extensions must map to the
accepted versioned taxonomy rather than defining competing mod-type or
game-area taxonomies. Taxonomy revisions follow the product specification's
versioning and review rules.

## Investigation requirements

Each completed question should produce a document under
[`investigations/`](investigations/) containing:

- question and product requirements;
- current date and exact versions;
- primary sources;
- local experiments and artifacts;
- alternatives;
- findings and uncertainty;
- recommendation;
- ADR or follow-up questions enabled.

Research conclusions remain proposals until they receive the applicable
accepted disposition—for example an ADR, product-specification amendment,
milestone-plan amendment, or explicitly accepted owner policy disposition.
