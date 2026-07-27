# M0 research foundation plan

Status: Accepted  
Owner: Project owner  
Created: 2026-07-25  
Last reviewed: 2026-07-26
Accepted: 2026-07-25  
Target milestone: M0 — Documentation and research foundation

Accepted amendments:

- 2026-07-25 —
  [RESEARCH-0024](../../research/investigations/RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md)
  accepts all Wave C research recommendations and
  `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`. RQ-024, RQ-027, RQ-035, and
  RQ-036 are resolved for M0; RQ-019 through RQ-022 retain accepted bounded
  conditional roadmaps. Gate C remains pending only the RQ-023 loose-only
  FaceGen qualification and RQ-025 exact EVAL-0016/EVAL-0017 real-mod cases.
  EVAL-0032 and EVAL-0086 specifications are accepted, but neither case has
  passed execution.
- 2026-07-25 —
  [RESEARCH-0013](../../research/investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
  and ADR-0008 through ADR-0011 accept Wave B and Gate B as met with
  documented non-blocking gaps. RQ-001 through RQ-007 and RQ-014 are resolved
  for M0. The selected architecture boundaries are authoritative, while their
  named conformance tests, exact supported surfaces, and implementation plans
  remain unpassed/unaccepted. ADR-0007's complete xEdit exclusion controls
  every Wave B interpretation.
- 2026-07-25 —
  [ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
  resolves RQ-006 by excluding xEdit from Infinium's product, development,
  dependency, integration, and evaluation boundaries. Mutagen-focused
  record-semantic qualification now uses independently specified first-party
  fixture truth. The earlier xEdit investigation remains historical decision
  provenance, not a pending integration path.
- 2026-07-25 —
  [RESEARCH-0003](../../research/investigations/RESEARCH-0003-retention-replay-export-policy.md)
  answers RQ-031 for M0 with source-specific conditions and later measured
  storage work. Permitted private source material must remain available long
  enough for useful extraction, analysis, case/finding synthesis, prose,
  provenance, and audit. Supported-API Nexus material follows ADR-0005's
  accepted development-risk decision unless a reversal trigger occurs.
- 2026-07-25 —
  [ADR-0006](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
  resolves RQ-026 with GPLv3-family licensing; user-installed MO2 and LOOT;
  the then-leading bundled Mutagen, conditional libloot, and disfavored USVFS
  candidate postures; and managed versioned LOOT data. Exact mechanisms
  remained with RQ-001/RQ-002/RQ-004/RQ-005 until ADR-0008 through ADR-0011.
  ADR-0007 subsequently excludes xEdit and resolves RQ-006.
- 2026-07-25 — [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md)
  accepts bounded, user-initiated supported Nexus API retrieval and diagnostic
  transformation as a non-blocking project-risk decision. Gate A is met with
  documented non-blocking gaps; Wave B and later dependency-ordered research
  may proceed within that ADR.

## Purpose and authority

This plan sequences the research needed to select Infinium's implementation
architecture and write an acceptable M1 backend semantic proof plan. It consumes
the accepted product baseline and ADR-0001 through ADR-0011; it does not change
their requirements or accept any currently unresolved technical mechanism.

Research results are evidence and recommendations. A recommendation becomes an
implementation constraint only through the applicable accepted ADR, accepted
product specification, or accepted milestone plan.

No production rewrite work is authorized by this plan. Research may
use bounded, disposable probes, benchmarks, fixtures, and read-only integration
experiments when the plan is accepted and the applicable source, security, and
authority preconditions below are satisfied.

## Objective

Complete enough reviewed research to:

1. prove that authoritative MO2/effective-installation and Bethesda semantic
   state can be reconstructed for the pinned target;
2. establish lawful and technically viable evidence-source and LLM boundaries;
3. produce, validate, and obtain acceptance for the versioned product taxonomy
   required by RQ-036;
4. select the storage, snapshot, job, process, application-stack, security,
   provider, and candidate-selection mechanisms needed by M1 without
   over-generalizing for later games or managers;
5. define reproducible synthetic and real-mod evaluation inputs for the first
   proof;
6. compare realistic system architectures and accept the ADRs that govern M1;
7. produce and accept a bounded M1 backend semantic proof plan.

## M0 completion outcome

M0 is complete only when:

- the accepted product baseline and foundational ADRs remain internally
  consistent with the research outcomes;
- every M0 exit-blocking research question has a reviewable investigation and
  a recorded disposition;
- every selected durable technical mechanism that meets the repository's ADR
  criteria has an accepted ADR; RQ-036 has an accepted versioned product
  taxonomy; and other recommendations have a reviewed source, evaluation, or
  planning disposition appropriate to their authority;
- research-dependent M1 evaluation cases have reviewed specifications and
  obtainable or reproducible ground truth;
- the M1-scoped evaluation strategy, fixture/anti-overfitting rules, and case
  specifications are explicitly accepted as the evaluation baseline;
- the selected architecture has been compared against realistic alternatives,
  including documented rejection reasons and unresolved limitations;
- an M1 plan links its scope to accepted requirements, ADRs, evaluation cases,
  and exact verification gates;
- the M1 plan is accepted before production implementation begins.

M0 does not require every question in the long-term research backlog to be
closed. Questions whose answers depend on measurements from M1–M3 or concern
M4 packaging/runtime-diagnosis features remain explicitly scheduled rather than
being answered speculatively.

## Accepted inputs

### Product baseline

- [Product definition](../../product/product-definition.md)
- [Requirements](../../product/requirements.md)
- [Workflows](../../product/workflows.md)
- [Domain model](../../product/domain-model.md)
- [Severity, confidence, maturity, coverage, and readiness](../../product/severity-confidence-and-coverage.md)
- [Analysis catalog](../../product/analysis-catalog.md)
- [Scope and milestones](../../product/scope-and-milestones.md)

### Foundational architecture constraints

- [ADR-0001 — Evidence authority boundary](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md)
- [ADR-0002 — Installation-snapshot and analysis-context binding](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
- [ADR-0003 — Exclude setup-mutation capabilities through M4](../../architecture/decisions/ADR-0003-read-only-authority.md)
- [ADR-0004 — Avoid premature manager/runtime abstraction](../../architecture/decisions/ADR-0004-initial-target-scope.md)
- [ADR-0005 — Proceed with supported Nexus API analysis](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md)
- [ADR-0006 — GPL product and tool-dependency boundary](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
- [ADR-0007 — Exclude xEdit from Infinium](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
- [ADR-0008 — MO2 profile, effective-state, and local-identity acquisition](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md)
- [ADR-0009 — Skyrim runtime and Bethesda semantic support](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
- [ADR-0010 — Snapshot fingerprint and dependency invalidation](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0011 — LOOT semantic and managed-data boundary](../../architecture/decisions/ADR-0011-loot-semantic-and-managed-data-boundary.md)

### Research and evaluation inputs

- [Open research questions](../../research/open-questions.md)
- [Source registry](../../research/source-registry.md)
- [Taxonomy research dependency map](../../research/taxonomy-dependency-map.md)
- [Investigation procedure](../../research/investigations/README.md)
- [Evaluation strategy](../../evaluation/evaluation-strategy.md)
- [Evaluation case catalog](../../evaluation/case-catalog.md)
- [Fixture guidelines](../../evaluation/fixture-guidelines.md)
- [Anti-overfitting rules](../../evaluation/anti-overfitting-rules.md)

### Legacy boundary

The archived implementation under [`../../../legacy/`](../../../legacy/) is
non-authoritative archaeological context. A research investigation may inspect
it only after establishing the current requirement and independent ground truth.
Legacy behavior, tests, dependencies, or chosen technologies are never evidence
that a rewrite mechanism is correct.

## Requirements served

The plan establishes prerequisites rather than delivering product capability.
Its research work is organized around these accepted requirement groups:

| Research area | Principal requirements served |
|---|---|
| Target and effective-state truth | SCOPE-001 through SCOPE-006; SNAP-001 through SNAP-006; ANALYSIS-001, ANALYSIS-003 through ANALYSIS-009 |
| Authority, integration, and security | AUTH-001 through AUTH-003; SEC-001 through SEC-004; EVID-002, EVID-003 |
| Typed evidence and semantic investigation | EVID-001 through EVID-007; FIND-001 through FIND-004; ANALYSIS-004, ANALYSIS-005, ANALYSIS-016, ANALYSIS-017 |
| Analyzer feasibility and roadmap | ANALYSIS-002, ANALYSIS-006 through ANALYSIS-015, ANALYSIS-018; VALID-001 through VALID-005 |
| Documentation and identity | DOC-001 through DOC-011; INTENT-001 through INTENT-005 |
| Jobs, reuse, history, and replay | SCAN-001 through SCAN-009; SNAP-001 through SNAP-006; OPS-002 through OPS-004 |
| Provider, privacy, and cost | AI-001 through AI-007; OPS-001; SCAN-003 through SCAN-006 |
| Taxonomy, coverage, and readiness foundations | FIND-001, FIND-003, FIND-007 through FIND-014; COVER-001 through COVER-003; PROD-004 |
| M1 outputs and development controls | SCAN-002, SCAN-009; EVID-007; OPS-003; ANALYSIS-016, ANALYSIS-017 |

These links identify why research is needed. They do not claim that M0
implements or validates the complete product requirement.

## Scope

### In scope

- current authoritative documentation and source/policy research;
- read-only inspection of a user-confirmed MO2 instance, profile, Skyrim
  installation, and relevant tool configuration;
- exact-version experiments against controlled synthetic inputs and carefully
  selected real inputs;
- disposable parsers, adapters, probes, benchmarks, and schema experiments;
- source-policy, retention, licensing, privacy, and security analysis before
  the affected data or operation is used;
- taxonomy research, acceptance, and versioned evolution using primary
  technical sources and a representative real/synthetic mod corpus;
- evaluation-fixture discovery and ground-truth design;
- architecture comparison and proposed ADRs;
- updating research, evaluation, architecture, and planning documents when an
  investigation produces a reviewed proposal;
- drafting and accepting the M1 implementation plan as the final M0 gate.

### Out of scope

- production application or analyzer implementation;
- changing MO2, the selected profile, load order, mod files, game files,
  configuration, generated output, or the game installation;
- treating a research probe as a supported product path;
- final M3 analyzer breadth, reliability thresholds, or calibrated user
  presets before measurement data exists;
- polished frontend implementation;
- public packaging, signing, update, and support operations;
- runtime-log diagnosis and test-session implementation;
- other managers, Skyrim runtimes/editions, games, or write-capable features;
- hard-coding the first NPC proof, a real mod, or a provisional taxonomy label
  into a general mechanism.

## Preflight

### Already satisfied

- The seven product documents are accepted.
- ADR-0001 through ADR-0011 are accepted.
- The legacy implementation is isolated and documented.
- The open-question registry and evaluation catalog exist.

### Required before the first investigation

1. This plan is reviewed and accepted.
2. The project owner confirms the local reference MO2 instance/profile and
   Skyrim installation to use for experiments.
3. Each investigation states whether it will access local private data, the
   network, authenticated APIs, paid LLMs, or external tools.
4. No investigation uses an authenticated or billable provider until its
   credential, authorization, context-minimization, retention, and cost
   controls are defined for the experiment.
5. No source acquisition proceeds through a method whose current policy is
   unknown or prohibits the intended operation.
6. No external-tool experiment proceeds until its candidate operation is
   identified as read-only and its product/tool-owned cache or temporary side
   effects are understood.
7. Sensitive or non-redistributable local artifacts receive a private
   retention location and a committed manifest/fingerprint instead of being
   added to the repository blindly.

### Per-investigation preflight

Before work starts, record:

- primary RQ and linked secondary RQs;
- exact target versions and retrieval/experiment date;
- accepted requirements and ADR constraints;
- source classes and access permissions;
- local paths or data classes required, without placing secrets in the
  document;
- expected experiments and negative/boundary controls;
- artifact retention and redistribution treatment;
- stopping conditions;
- decision, ADR, taxonomy, evaluation, or follow-up enabled.

## Research-question disposition

The classes below control the M0 exit:

- **Exit-blocking:** must have a reviewed investigation and reviewed downstream
  disposition before the M1 plan can be accepted; any technical mechanism
  selected for M1 must be accepted through an ADR.
- **Conditional:** scheduled in M0, but blocks the M1 plan only if M1 exercises
  the affected capability or if another exit-blocking investigation cannot
  reach a sound conclusion without it.
- **Later evidence:** intentionally remains open until the required
  implementation/evaluation evidence exists.
- **Resolved:** an accepted disposition satisfies the question for M0. Named
  residual risks or later measurement/mechanism work remain follow-up rather
  than an M0 blocker unless a documented reversal trigger reopens the question.

An accepted amendment to this plan may promote a Conditional or Later-evidence
question. It may not silently remove an Exit-blocking question.

| RQ | M0 class | Planned wave | M0 disposition |
|---|---|---|---|
| RQ-001 | Resolved | B | ADR-0008 selects version-pinned quiescent MO2 reconstruction; EVAL-0051 remains a support gate |
| RQ-002 | Resolved | B | ADR-0008 treats MO2 saved selection as suggestion-only and requires explicit target binding |
| RQ-003 | Resolved | B | ADR-0009 pins the initial exact Steam `1.6.1170.0` runtime manifest |
| RQ-004 | Resolved | B | ADR-0009 selects Mutagen `0.54.2` with positive supported-shape qualification and explicit archive/string gaps |
| RQ-005 | Resolved | B | ADR-0011 selects the conditional libloot/data delivery boundary and rejects current LOOT application automation |
| RQ-006 | Resolved | B | ADR-0007 excludes xEdit from all Infinium boundaries and replaces its proposed oracle role with parser-independent first-party fixture truth |
| RQ-007 | Resolved | B | ADR-0008 separates physical installed identity, source mapping, and unavailable installer/manual-change history |
| RQ-008 | Exit-blocking | D | Identify currently supported Nexus interfaces and content/revision coverage |
| RQ-009 | Resolved | A | ADR-0005 accepts bounded, user-initiated analysis through documented supported Nexus APIs under an explicit owner risk decision |
| RQ-010 | Conditional | D | Approve only sources needed by M1; broader registry expansion may follow |
| RQ-011 | Exit-blocking | D | Define the minimum provider-neutral claim/investigation contract used by M1 |
| RQ-012 | Exit-blocking | D | Verify the reference provider and comparison capabilities needed by the M1 contract |
| RQ-013 | Exit-blocking | E | Select evidence/acquisition persistence and revision relationships needed by M1 |
| RQ-014 | Resolved | B | ADR-0010 selects canonical structural manifests, scoped SHA-256, dependency closures, and conservative invalidation |
| RQ-015 | Exit-blocking | E | Select the durable run/job/checkpoint model and bounded M1 subset |
| RQ-016 | Exit-blocking | E | Compare desktop/application stacks and select the application/engine direction |
| RQ-017 | Exit-blocking | E | Select the UI/analysis/process/data-query boundary needed for scale and isolation |
| RQ-018 | Exit-blocking | E | Select secure credential entry/storage for the chosen architecture before authenticated integration |
| RQ-019 | Conditional | C | Accepted bounded static inventory and layered-identity roadmap; named analyzer work blocks only if selected into M1 |
| RQ-020 | Conditional | C | Accepted generic inspection plus version-pinned adapter roadmap; named generator delivery remains later |
| RQ-021 | Conditional | C | Accepted generic/configuration-schema roadmap; named schemas do not block the first proof |
| RQ-022 | Conditional | C | Accepted bounded static PEX/VMAD contract; compiled-Papyrus analysis blocks only if selected |
| RQ-023 | Exit-blocking | C | NIF-first scope accepted; exact loose-only FaceGen identity/provider qualification remains pending |
| RQ-024 | Resolved | C | Accepted generic substrate → NPC proof → REFR/placement/enable-link roadmap and later-family ordering |
| RQ-025 | Exit-blocking | C | Two-layer corpus strategy accepted; exact EVAL-0016 remains incomplete and EVAL-0017 remains unselected |
| RQ-026 | Resolved | A | ADR-0006 establishes GPLv3-family licensing and the accepted external-application/bundled-library/data posture |
| RQ-027 | Resolved | C | Accepted benchmark/cost method and rough feasibility evidence; exact architecture/production budgets require later remeasurement |
| RQ-028 | Later evidence | F | Define the calibration/evidence-collection plan now; set M3/M4 thresholds only after analyzer data exists |
| RQ-029 | Later evidence | F | Schedule before automatic runtime-log application, no later than its M3 delivery plan |
| RQ-030 | Later evidence | F | Schedule for M4 packaging/update planning after the application architecture stabilizes |
| RQ-031 | Resolved | A | Accepted metadata-first durable minimization, useful-analysis private retention, independent permission/export classes, replay disclosure, and deletion semantics; measured storage remains follow-up work |
| RQ-032 | Exit-blocking | E | Select concrete content, path, subprocess, navigation, and export controls for M1 surfaces |
| RQ-033 | Exit-blocking | E | Select finding/case continuity and reconciliation keys without name-based false identity |
| RQ-034 | Exit-blocking | E | Select enforceable reservation/deadline/reconciliation behavior before concurrent or billable M1 work |
| RQ-035 | Resolved | C | Accepted typed-index, causal-join, canonical-participant, and mandatory-lane design; independent production execution remains later |
| RQ-036 | Resolved | C | Accepted `infinium.skyrim-se.mod-impact-taxonomy/0.1.0` and integrated its consumer documents |

## Dependency-ordered research waves

Wave letters express dependency order, not calendar promises. Work inside a wave
may proceed concurrently only when its inputs and artifacts are independent.
Every investigation remains separately reviewable.

### Wave A — Policy and evidence-handling guardrails

Status: Completed on 2026-07-25. Gate A is met through ADR-0005, ADR-0006,
and the accepted RQ-031 owner disposition. This authorizes the next research
wave, not any still-unresearched integration operation or implementation
architecture.

Questions:

- RQ-009 — Nexus access and policy;
- RQ-026 — helper/tool licensing and distribution obligations;
- RQ-031 — retention, replayability, and export/redistribution boundaries.

Required outputs:

- dated policy/source findings based on current primary sources;
- permitted/prohibited acquisition-operation matrix;
- private-retention versus redistribution matrix by evidence class;
- accepted helper/tool licensing, external-application, bundled-library, and
  managed-data constraints;
- updates to the source registry;
- explicit constraints consumed by later experiments and ADRs.

Gate A:

- no planned Wave D source access relies on a prohibited or unknown method;
- no planned tracked artifact assumes that private retention permits
  redistribution;
- every external helper considered for M1 has a known experiment and
  distribution posture, or is excluded.

### Wave B — Authoritative local state and deterministic ground truth

Status: Completed and accepted on 2026-07-25. Gate B is met with documented
non-blocking gaps. ADR-0007 excludes xEdit, and ADR-0008 through ADR-0011
record the accepted Wave B architecture boundaries. Named conformance cases
remain unexecuted and gate implementation/support claims.

Questions:

- RQ-001 through RQ-007;
- RQ-014.

Completed investigation order:

1. inventory the user-confirmed reference environment and exact versions;
2. establish MO2/profile state and authoritative comparison methods;
3. pin runtime detection and unsupported-target behavior;
4. verify plugin, archive, linking, override-chain, and winner semantics;
5. determine identity/source/FOMOD metadata actually retained by MO2;
6. compare LOOT integration options and record the researched rejection of
   xEdit as a project dependency or oracle;
7. benchmark candidate fingerprint/dependency strategies against realistic
   file/archive populations.

Required outputs:

- reproducible read-only environment/experiment manifest;
- synthetic and controlled-real MO2 profiles or private manifests;
- agreement/disagreement matrix against authoritative MO2 behavior;
- parser-independent Bethesda record ground-truth specification;
- capability/gap matrices for MO2, the selected Mutagen boundary, and LOOT, plus
  historical rejection evidence for xEdit;
- snapshot/fingerprint benchmark results and invalidation examples;
- accepted integration, semantic-layer, snapshot, and conditional LOOT ADRs;
- reviewed specifications or research prerequisites for EVAL-0051,
  EVAL-0052, EVAL-0053 where applicable, EVAL-0054, and EVAL-0046.

Gate B:

- Infinium has a defensible route to exact effective state for every local
  surface exercised by M1;
- unsupported or unobservable state has explicit gap semantics;
- no chosen external-tool operation is known to mutate protected setup state;
- snapshot validity is based on declared dependencies and measured behavior,
  not modification time or guessed ownership alone.

Accepted result: **Met with documented non-blocking gaps.**

If authoritative MO2 effective state or applicable record ground truth cannot
be reconstructed, M0 stops before architecture acceptance and records the
blocking evidence instead of compensating with heuristics or LLM inference.

### Wave C — Analysis surfaces, taxonomy, corpus, and candidate scale

Status: Investigations completed and recommendations accepted on 2026-07-25.
The taxonomy and EVAL-0032/EVAL-0086 specifications are accepted. Gate C
remains pending only the exact loose-only FaceGen qualification required by
RQ-023 and pinned, independently grounded EVAL-0016/EVAL-0017 real-mod
candidates required by RQ-025. These are qualification/corpus closeout tasks,
not invitations to redesign the accepted Wave C roadmaps.

Questions:

- RQ-019 through RQ-025;
- RQ-027;
- RQ-035;
- RQ-036.

Internal order:

1. conduct bounded technical-surface surveys for root/native components,
   generated output, configuration, scripts, assets, and record families;
2. inventory a deliberately varied synthetic and real-mod corpus;
3. select exact positive, matched-negative, boundary, unsupported, and
   cross-cutting candidates for M1 evaluation;
4. propose one versioned product taxonomy with distinct declared-purpose,
   technical-surface, affected-area, consequence, and effect-extent axes;
5. test multi-label, cross-cutting, unknown, and unsupported classification;
6. build candidate/index/ranking experiments over the observed structures;
7. benchmark candidate recall, volume, latency, IO, memory, and estimated LLM
   escalation cost at increasing profile scales;
8. revise and review the taxonomy and candidate design against failures.

RQ-036 consumed bounded survey evidence from Conditional RQ-019 through RQ-022
without pretending that their later named-analyzer roadmaps are complete.
Their bounded M0 recommendations are accepted; named analyzer depth,
implementation, and qualification remain later work. Future extensions may not
create incompatible mod-type or game-area taxonomies.

Required outputs:

- accepted versioned product taxonomy specification;
- completed update to the taxonomy dependency map and every affected
  pre-acceptance inventory identified by it;
- classification examples and counterexamples across materially different
  surfaces and game areas;
- M1 positive/negative/boundary fixture designs;
- pinned real-mod candidate manifests for EVAL-0016 and EVAL-0017, with
  acquisition/licensing/privacy treatment;
- selected first semantic record/relationship scope and materially different
  generalization scope;
- selected candidate-index/ranking design with benchmark evidence;
- initial performance/cost measurement method and architecture budgets;
- reviewed EVAL-0032 and EVAL-0086 specifications or approved successors.

Gate C conditions already satisfied:

- the product owner accepted the RQ-036 taxonomy as a product specification,
  not an ADR;
- every affected accepted product document was revised and re-reviewed through
  its change discipline when the taxonomy materially changed its normative
  language; the new taxonomy specification does not silently override it;
- the first proof remains generic scope-incongruent reversion rather than an
  NPC-specific rule;
- candidate experiments retain planted interactions without defaulting to
  naïve all-pairs LLM comparison;
- unevaluated taxonomy regions and unsupported semantics are explicit.

Remaining Gate C conditions:

- RQ-023 must complete the exact loose-only FaceGen identity/provider closure
  used by the first real cross-layer case; and
- RQ-025 must supply pinned, independently grounded EVAL-0016 and EVAL-0017
  candidates, including matched controls and at least one materially different
  non-NPC surface or affected game area under the accepted taxonomy.

### Wave D — Documentation acquisition and provider-neutral LLM boundary

Questions:

- RQ-008;
- RQ-010 through RQ-012.

Dependencies:

- Wave A source-policy and retention findings;
- RQ-009 access-policy conclusions from Wave A;
- Wave B installed-mod identity findings;
- accepted Wave C taxonomy version `0.1.0` for declared-purpose and
  claimed-area fields;
- accepted ADR-0001 evidence authority.

Internal order:

1. enumerate supported Nexus content, revision identity, authentication, and
   access limits;
2. run bounded authenticated experiments only against documented, supported
   Nexus operations under ADR-0005; record unsupported content surfaces as
   coverage gaps rather than using page fallbacks;
3. register only necessary M1 sources and access methods;
4. test claim extraction on retained, permitted source samples;
5. define the smallest provider-neutral extraction/investigation schemas;
6. compare reference-provider authentication, structured-output, batching,
   model-version, token/cost, rate-limit, quota, and cancellation behavior;
7. exercise citation, applicability, contradiction, abstention, and hostile
   embedded-instruction cases.

Required outputs:

- updated source registry with verified dates and capability gaps;
- source/entity/version acquisition contract;
- provider-neutral claim-extraction and investigation contract proposal;
- GPT reference-adapter capability matrix plus a contract-level portability
  review against at least one materially different provider's published
  capabilities; live multi-provider testing is not required for M0 unless the
  paper comparison exposes a material uncertainty;
- prompt/context minimization and untrusted-content experiment results;
- provider capability gaps that affect estimates, hard limits, replay, or UX;
- research inputs for EVAL-0010 through EVAL-0012, EVAL-0033, EVAL-0034,
  EVAL-0064, EVAL-0067, EVAL-0068, EVAL-0076, EVAL-0077, and EVAL-0083.

Gate D:

- every extracted claim resolves to permitted source evidence and applicable
  versions/conditions or abstains;
- model output cannot become local-state authority or grant operation
  authority;
- the contract works without provider-specific concepts in the core domain;
- authenticated or billable experimentation has explicit user authorization,
  credential handling, context, cost, and retention boundaries.

### Wave E — Architecture and security synthesis

Questions:

- RQ-013 and RQ-015 through RQ-018;
- RQ-032 through RQ-034;
- architecture conclusions enabled by RQ-001, RQ-004, RQ-005, RQ-014,
  RQ-026, RQ-031, RQ-035, and accepted ADR-0007 through ADR-0011.

Required comparison:

- the current C#/.NET worker + React/TypeScript + hardened Electron candidate;
- an Avalonia-centered alternative;
- a Tauri/WebView2 or comparably realistic web-frontend desktop alternative;
- any materially better design discovered by research.

Each candidate must be compared on:

- authoritative MO2/Bethesda/tool integration feasibility;
- long-running and crash-prone work isolation;
- UI responsiveness and progressive/paginated data access;
- snapshot, evidence, history, lineage, and dependency-query behavior;
- checkpoint, cancellation, restart, and cost-ledger correctness;
- credential and privileged-operation boundaries;
- untrusted HTML/text/model-output handling;
- local-first/offline behavior;
- Windows packaging implications without selecting M4 distribution details;
- implementation complexity and language/tooling burden;
- testability and replacement of the UI shell without rewriting the engine;
- high-end time, memory, disk, and IO behavior;
- failure and coverage-gap behavior.

Required outputs:

- compared architecture report with explicit assumptions and rejected options;
- proposed ADRs for every durable mechanism selected for M1;
- storage/schema-evolution direction for immutable runs/evidence and mutable
  review projections;
- persistence mechanisms that implement ADR-0010's accepted
  dependency-aware snapshot/cache strategy;
- job/checkpoint and process/data-query topology;
- application stack and UI/worker boundary;
- secure credential and allowlisted privileged-operation design;
- candidate-index/interaction representation where sufficiently cross-cutting;
- provider cost reservation/deadline/reconciliation design;
- finding/case continuity and reconciliation design;
- evaluation feasibility mapping for EVAL-0026, EVAL-0033 through EVAL-0035,
  EVAL-0038 through EVAL-0041, EVAL-0044 through EVAL-0046, EVAL-0079 through
  EVAL-0083.

Gate E:

- every durable or cross-cutting M1 mechanism that meets the repository's ADR
  criteria is governed by an accepted ADR, while local implementation details
  remain bounded by the accepted M1 plan;
- the selected design satisfies ADR-0001 through ADR-0011 without hidden
  authority expansion;
- no decision relies on mocked production data, guessed effective state,
  plaintext credentials, broad privileged UI access, or legacy architecture
  inertia;
- any unresolved mechanism is either outside M1 or blocks the M1 plan
  explicitly.

### Wave F — Evaluation specifications, deferred-question ledger, and M1 plan

Questions:

- record the evidence-acquisition plan and later gate for RQ-028;
- schedule RQ-029 before automatic runtime-log use;
- schedule RQ-030 for M4 architecture/distribution work;
- reconcile every other RQ status with completed investigations and accepted
  decisions.

Required outputs:

- reviewed and accepted M1 evaluation baseline and case specifications;
- updated evaluation catalog and fixture manifests;
- updated open-question statuses and follow-up questions;
- ADR index containing the accepted M1 architecture decisions;
- explicit residual-risk and unsupported-capability register;
- accepted M1 backend semantic proof plan.

Gate F:

- all M0 exit criteria pass;
- the evaluation strategy and M1-applicable fixture, anti-overfitting, and case
  specifications are accepted;
- every M1 requirement claim has at least one reviewed evaluation case;
- the M1 plan names exact scopes, artifacts, contracts, verification commands,
  and completion evidence;
- no production implementation starts before M1 plan acceptance.

## Investigation deliverable contract

Each investigation addresses one primary research question in one bounded,
dated document under
[`../../research/investigations/`](../../research/investigations/) using the
repository investigation outline. Every Exit-blocking question must have at
least one owning investigation. Conditional and Later-evidence questions
receive an investigation when their scheduled work actually starts; they do
not require empty reports merely to complete M0. A single experiment may
support several questions, but one document must own each conclusion and other
investigations must cite it rather than duplicating or subtly changing it.

Every completed investigation must include:

1. the primary question, linked requirements, ADR constraints, and downstream
   decision;
2. explicit scope and non-scope;
3. current primary sources with exact versions/revisions and retrieval dates;
4. access, authentication, retention, citation, licensing, and redistribution
   constraints where applicable;
5. reproducible experiment procedure and retained artifact manifest;
6. positive, negative, boundary, malformed, and unsupported observations where
   applicable;
7. measured results rather than qualitative performance claims where the
   decision concerns scale or cost;
8. alternatives considered on the same requirements and evidence;
9. uncertainty, unsupported behavior, and contrary evidence;
10. recommendation with the exact ADR, product specification, evaluation case,
    or follow-up it enables.

An investigation is not complete merely because it finds documentation that
supports the leading candidate. It must test material claims locally where
possible and state what remains unverified.

## Source and experiment rules

- Prefer current official specifications, source repositories, tool
  documentation, and author-maintained material.
- Record retrieval date and exact tool/library/API/runtime version.
- Use community sources only as investigative evidence unless corroborated.
- Follow the source registry; do not scrape prohibited content or bypass
  authentication/access controls.
- Public policy, API, licensing, and technical documentation may be consulted
  to establish Wave A guardrails.
- Under ADR-0005, bounded user-initiated acquisition and diagnostic analysis of
  content exposed through documented supported Nexus APIs is permitted for
  research. Unsupported content surfaces remain coverage gaps; HTML scraping,
  browser automation, undocumented endpoints, bulk/rehost behavior, model
  training, and raw public source redistribution remain prohibited.
- Keep local/deterministic state, source claims, user statements, and model
  interpretations separately attributable.
- Treat every retrieved document, archive, log, tool output, and model output
  as untrusted data.
- Do not put credentials, account tokens, unnecessary usernames, or unrelated
  absolute paths into probes, prompts, tracked artifacts, logs, or reports.
- Do not commit copyrighted mod/source bytes or private profile data merely for
  convenience. Retain permitted fixtures or use manifests, fingerprints, and
  reproducible acquisition instructions.
- Do not treat a source's current unavailability as permission to invent
  content or silently remove coverage.
- Do not use an LLM to decide deterministic winners, fill unavailable binary
  state, or replace a missing authoritative integration.

## Probe and artifact rules

Research code is disposable until an accepted implementation plan explicitly
selects it. Every probe must:

- be clearly labeled as research-only;
- default to read-only behavior;
- declare all filesystem, process, network, credential, and paid-operation
  effects;
- reject protected setup write targets;
- emit machine-readable evidence where useful and a human-readable summary;
- record its version/commit and exact inputs;
- avoid hidden fallback or fabricated success data;
- preserve failed, unsupported, and ambiguous outcomes;
- avoid real-mod-name or fixture-specific production logic.

Reuse of a probe in production requires a later implementation task, contract
review, independent tests, security review, and acceptance against the
applicable ADRs. Research success alone does not approve transplantation.

## Evaluation work required before M1

M0 does not execute production acceptance tests, but it must make the first
proof's gates precise and reproducible. The M1 plan must have reviewed
specifications for the following cases or approved successors:

- semantic positive/negative and real generalization: EVAL-0001, EVAL-0002,
  EVAL-0016, EVAL-0017;
- run immutability, candidate selection, and clean/source separation:
  EVAL-0026, EVAL-0032, EVAL-0037;
- security and non-mutation: EVAL-0033 through EVAL-0035, EVAL-0046,
  EVAL-0080;
- manual initiation, acquisition/application provenance, and run output:
  EVAL-0039, EVAL-0040, EVAL-0045;
- local ground truth and supported-target rejection: EVAL-0051, EVAL-0052,
  EVAL-0054;
- modular analyzer, typed evidence, and development controls: EVAL-0065,
  EVAL-0067, EVAL-0082;
- offline/provider-boundary behavior: EVAL-0064;
- end-to-end provenance, causal grouping, and honest coverage:
  EVAL-0083 through EVAL-0085;
- taxonomy separation and historical versioning: EVAL-0086.

If M1 invokes LOOT, a billable/authenticated provider, concurrent billable
work, or another optional external boundary, its applicable EVAL-0053,
EVAL-0076, EVAL-0077, EVAL-0081, and related cases become M1 gates before that
boundary is implemented.

Fixture specifications must include exact expected observations, candidates,
hypotheses/findings, supported-versus-lead-only state, abstentions, gaps,
ground truth, replay dependencies, taxonomy version, and redistribution
treatment. Every positive requires a meaningful matched negative.

## Review and acceptance workflow

For each investigation:

1. Draft the investigation and collect evidence.
2. Check primary-source freshness and exact-version applicability.
3. Reproduce or independently inspect material experiments.
4. Review semantic consistency against accepted requirements and ADRs.
5. Review negative evidence, uncertainty, security, and source-policy effects.
6. Mark the recommendation ready for decision without treating it as accepted
   architecture.
7. Create or update the proposed ADR/product-taxonomy/evaluation artifact.
8. Obtain explicit acceptance, rejection, or deferral of that downstream
   artifact.
9. Update the open-question registry and cross-references.

Use the
[research-investigation agent handoff template](../research-investigation-agent-handoff-template.md)
when assigning one of these bounded investigations to a fresh agent.

At the end of each wave:

- conduct a full cross-document semantic review;
- check for contradictions with earlier findings and accepted decisions;
- update dependency and residual-risk registers;
- re-sequence later work where evidence changed dependencies;
- record plan amendments rather than silently changing the original rationale.

## Stopping and escalation rules

Stop the affected path and request a decision when:

- no authoritative way to reconstruct an M1-required effective-state surface
  is found;
- a candidate tool operation cannot be shown to preserve read-only authority;
- a required source cannot be accessed through a supported, permitted method;
- a stack option cannot provide the required credential, privileged-operation,
  or untrusted-content boundary;
- a provider cannot expose a finite reservation bound required by the selected
  hard-limit configuration;
- an M1 fixture cannot be obtained, reproduced, or privately retained with
  adequate ground truth;
- the RQ-036 corpus is too narrow to justify a closed taxonomy;
- benchmark evidence makes the selected candidate or architecture impractical
  at the required scale;
- research would require expanding product authority or scope beyond the
  accepted baseline.

The acceptable outcome may be a documented coverage gap, a narrower M1 scope,
an alternative architecture, or a blocked milestone. It is never fabricated
certainty or silent best-effort behavior.

## Documentation and traceability updates

During M0:

- investigation evidence goes under `docs/research/investigations/`;
- RQ status and follow-up questions go in
  [`../../research/open-questions.md`](../../research/open-questions.md);
- source capabilities/policies update
  [`../../research/source-registry.md`](../../research/source-registry.md);
- taxonomy research and later revisions update
  [`../../research/taxonomy-dependency-map.md`](../../research/taxonomy-dependency-map.md)
  and every affected inventory it identifies, and produce a separately
  reviewable versioned product-taxonomy specification;
- durable technical selections produce proposed ADRs under
  `docs/architecture/decisions/`;
- evaluation discoveries update the case catalog and reviewed fixture
  specifications without changing expected results to fit a probe;
- the final implementation work is described only in the accepted M1 plan.

Every material conclusion must be traceable in both directions:

```text
accepted requirement
  -> research question
  -> investigation evidence
  -> accepted ADR or product specification
  -> evaluation case
  -> M1 plan slice
```

## Verification before M0 completion

Run and record:

- Markdown link validation across `README.md` and `docs/`;
- duplicate/unknown requirement, RQ, EVAL, and ADR identifier checks;
- ADR metadata/index consistency;
- investigation-to-RQ and decision traceability review;
- evaluation-case-to-requirement traceability review;
- source/version/retrieval-date audit for every exit-blocking investigation;
- artifact/licensing/privacy manifest review;
- `git diff --check`;
- a final semantic review of all changed product, research, architecture,
  evaluation, and planning documents.

The M0 completion record must name the exact accepted ADRs and taxonomy version,
research documents, evaluation specifications, verification commands/results,
known gaps, and accepted M1 plan revision.

## Rollback and amendment

This milestone changes documentation, research artifacts, and decisions, not
the user's modding setup.

- Incorrect research is corrected through a dated revision that preserves the
  earlier evidence and explains the change.
- An accepted ADR is superseded rather than silently rewritten.
- An accepted taxonomy is versioned; historical classification meaning remains
  identifiable.
- This plan may be amended after review when research changes dependencies or
  scope. The amendment must identify affected RQs, gates, and M1 consequences.
- Production implementation remains unauthorized until the resulting M1 plan
  is accepted, even if a probe appears usable.

## Deferred follow-up

Unless promoted by accepted evidence:

- RQ-028 maturity/readiness thresholds remain for empirical calibration during
  and after M1 analyzer evaluation;
- RQ-029 runtime-evidence fingerprinting remains before automatic log
  application, no later than its M3 plan;
- RQ-030 packaging/signing/update selection remains for M4 planning;
- full named coverage for generators, configuration ecosystems, compiled
  scripts, root/native components, and later semantic families remains in the
  appropriate M3 analyzer plans where its Conditional M0 research did not
  become an M1 dependency;
- public supportability, redistribution-reviewed diagnostic bundles, and
  release operations remain M4 concerns.

## Completion record

Completion status: In progress — Waves A through C research accepted; Gate C
qualification/corpus closeout plus Waves D through F remain

To be filled when M0 completes:

- accepted plan revision:
- completed investigation documents:
- accepted product-taxonomy version:
- accepted/superseded ADRs:
- reviewed M1 evaluation specifications:
- accepted M1 plan:
- verification commands and results:
- unresolved gaps and deferred questions:
- completion date:
