# M1 backend semantic proof plan

Status: Accepted  
Owner: Project owner  
Prepared: 2026-07-28  
Accepted: 2026-07-28  
Last reviewed: 2026-07-28  
Target milestone: M1 — Backend semantic proof

## Authority and start condition

The project owner accepted this plan on 2026-07-28. It is the active
implementation authorization for the bounded M1 scope below. It does not
authorize excluded capabilities or imply that any implementation or evaluation
already passes.

The plan consumes:

- the accepted product baseline;
- taxonomy `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`;
- accepted ADR-0001 through ADR-0023 and ADR-0025, with ADR-0024 rejected;
- the accepted M0 investigations;
- the accepted [M1 evaluation baseline](../../evaluation/m1-evaluation-baseline.md);
- the accepted M1 case specifications and fixture manifests; and
- the accepted
  [deferred-question and residual-risk register](../../research/deferred-question-and-residual-risk-register.md).

All linked Wave F evaluation documents were reviewed and accepted, every M1
claim below retains a reviewed evaluation case, and Gate F was recorded as met
on 2026-07-28. Implementation still begins with the plan's repository,
dependency, fixture, and security preflight rather than assuming those runtime
conditions from document acceptance.

## Objective

Prove, through a human-readable CLI and versioned JSON artifact, that Infinium
can:

1. admit exactly the supported Skyrim/MO2 target and capture one immutable
   effective-installation snapshot;
2. derive positively qualified Bethesda record/provider truth without using
   xEdit or circular fixture expectations;
3. generate inspectable candidates from typed indexes and causal joins rather
   than all-pairs model comparison;
4. combine local observations, source-bound purpose claims, and a bounded
   schema-constrained LLM operation without allowing the model to invent local
   state or authority;
5. detect a scope-incongruent stale relation, abstain on its meaningful matched
   negative, and produce an evidence-bearing finding/case with consequence,
   uncertainty, remediation or validation, coverage, and provenance;
6. demonstrate the same generic mechanism in a materially different accepted
   taxonomy region; and
7. persist, replay, inspect, and safely fail the proof under the accepted
   lifecycle, IPC, security, credential, and budget boundaries.

## Exact M1 scope

### Included

- Windows x64 and exact Steam Skyrim SE runtime `1.6.1170.0`.
- Explicitly selected, quiescent MO2 `2.5.2` disposable or user profile input.
- Deterministic MO2 profile/plugin/qualified loose-provider reconstruction.
- Exact structural snapshots and scoped content SHA-256 dependencies.
- Mutagen.Bethesda `0.54.2` over an explicit allowlist sufficient for:
  - TES4/master mapping;
  - NPC record identity, selected package relations, and selected appearance
    fields;
  - loose FaceGen origin/provider presence for the qualified boundary;
  - REFR placement, enable-parent/linked-reference relations;
  - the narrow forced-reference alias relation required by the second proof;
  - override chains, winners, FormKeys, links, record states, and selected
    field values used by accepted fixtures.
- Local/fixture documentation acquisition and claim extraction.
- Direct synchronous OpenAI Responses API calls using a user-supplied Platform
  API key, explicit `gpt-5.6-sol`, explicit `reasoning.effort: medium`, strict
  Structured Outputs, `store: false`, explicit `service_tier: "default"`,
  non-streaming execution, exact retained invocation provenance, local finite
  reservation, and no alternate model, tool, execution, or access mode. M1
  executes both accepted semantic contracts: source-claim extraction and
  evidence-bound candidate investigation.
- SQLite authoritative state, coordinator-owned content-addressed payloads,
  application-owned lifecycle, standalone coordinator, bounded managed
  workers, role-separated named-pipe gRPC, and a one-shot credential/provider
  helper.
- CLI configuration of analyzers, sources, budgets, cache/recompute behavior,
  and tracing.
- Synthetic positive/matched-negative semantic cases and controlled-real
  EVAL-0016/EVAL-0017.
- Human-readable CLI report, versioned JSON run output, and developer traces.

### Excluded

- WPF/WebView2/React product UI;
- LOOT application or libloot integration;
- LOOT automatic managed-data maintenance implementation;
- Nexus acquisition, hosted web search, and community-source acquisition;
- OpenAI background mode, Batch, explicit provider caching, concurrent live
  calls, provider tools, conversation state, persisted reasoning, Pro mode,
  model aliases/routing, ChatGPT/Codex-plan access, or another provider;
- archive-positive FaceGen, general BSA semantic parity, localized-string
  support, or a production NIF parser;
- PEX/VMAD, root/native, generated-output, named configuration, performance,
  playthrough-lifecycle, and runtime-log analyzers;
- M3 maturity/readiness thresholds, calibrated presets, high-end scale, or
  creator-profile correctness claims;
- user-created export workflows, installers, signing, updates, or public
  packaging; and
- any modlist, plugin, file, configuration, or generated-output mutation.

Excluded capabilities must appear in machine-readable and human-readable
coverage/capability output where relevant. They cannot be silently absent.

## Required repository structure

M1 shall create this clean-break structure rather than reviving `legacy/`:

```text
Infinium.sln
Directory.Build.props
Directory.Packages.props
global.json
src/
  Infinium.Domain/
  Infinium.Application/
  Infinium.Persistence/
  Infinium.Mo2/
  Infinium.Bethesda/
  Infinium.Analysis/
  Infinium.OpenAI/
  Infinium.Coordinator/
  Infinium.Worker/
  Infinium.CredentialHelper/
  Infinium.Cli/
contracts/
  protobuf/
  json-schema/
tests/
  Infinium.UnitTests/
  Infinium.ContractTests/
  Infinium.IntegrationTests/
  Infinium.EvaluationTests/
test-data/
  synthetic/
  manifests/
tools/
  evaluation/
```

Controlled-real bytes remain outside the tracked repository. `test-data`
contains only permitted synthetic bytes, public identities, hashes, expected
structural values, and private-acquisition manifests.

## Required contracts

All contracts are versioned before the first producer/consumer implementation.

### Domain contracts

- installation snapshot and assurance;
- semantic analysis context;
- effective scan configuration;
- analysis and evidence-acquisition runs;
- resolved input manifest;
- typed observation, external claim, candidate, hypothesis, finding,
  recommendation, and coverage gap;
- taxonomy assignment;
- logical finding/case identity, immutable occurrence/revision, lineage, and
  reconciliation assessment;
- dependency closure and reuse/application edge;
- readiness evaluation placeholder sufficient to state no/full/scope-limited
  M1 readiness without inventing M3 policy;
- auditability and replayability assessment.

### Operational contracts

- lifecycle state machine, transition, lease/fence, attempt, checkpoint, and
  publication receipt;
- content-addressed payload manifest;
- provider access-profile and non-secret credential-generation metadata;
- immutable provider request assignment;
- reservation, dispatch fence, usage ledger entry, and settlement state;
- process-role bootstrap, protocol negotiation, nonce, request/response,
  paginated query, event cursor, worker assignment, and staged-output manifest.

### Analyzer contracts

Every analyzer declares:

- supported taxonomy version, scope, fields/shapes, and exclusions;
- exact input/dependency populations;
- evidence and abstention thresholds;
- possible typed outputs;
- coverage denominators and gap states;
- offline/network/provider requirements;
- expected scale/cost;
- maturity fixed to Experimental during M1; and
- linked evaluation cases.

### Output contracts

- `infinium.run-output/v1` JSON schema;
- stable CLI exit-code and summary contract;
- diagnostic trace schema and sensitivity label;
- evaluation result/manifest schema.

Display prose is not a stable machine contract.

## Implementation slices

Each slice is implemented completely, reviewed semantically, and verified
before the next dependent slice begins. Passing slice tests does not waive the
milestone-wide cases.

### Slice 0 — Toolchain, licensing posture, and dependency lock

Deliver:

- .NET 10 SDK pin and deterministic build settings;
- central package management and exact lock identities;
- GPLv3-family notices in project metadata without choosing an operative
  selector unless a license file is introduced;
- dependency licence/provenance manifest;
- solution/project skeleton and analyzer/style configuration;
- no reference from production projects to `legacy/`.

Verification:

- clean restore/build on the supported Windows environment;
- dependency graph and licence review;
- repository search proving no production legacy reference.

### Slice 1 — Versioned domain, wire, output, and evaluation contracts

Deliver:

- domain value objects and invariants;
- protobuf and JSON schemas;
- fixture-manifest and assertion-result readers;
- evaluation partition/answer-isolation enforcement;
- schema compatibility tests.

Gates:

- EVAL-0065, EVAL-0067, EVAL-0082 contract portions;
- fixture loader refuses missing fingerprints, partition, ground truth,
  taxonomy version, or expected gap declarations.

### Slice 2 — Persistence, lifecycle, coordinator, worker, and CLI substrate

Deliver:

- exact patched SQLite binding assertion;
- migrations, STRICT/foreign-key schema, CAS admission, projection rebuild, and
  backup/restore primitives;
- application-owned lifecycle ledger, lease/fence, attempts, checkpoints, and
  cancellation/pause semantics needed by M1;
- standalone coordinator single-authority startup;
- bounded named-pipe gRPC client/worker contracts;
- managed general worker with staged-output publication;
- CLI start/status/wait/cancel/inspect commands.

Gates:

- EVAL-0026, EVAL-0038 where exercised, EVAL-0079 substrate, EVAL-0080,
  EVAL-0087, and EVAL-0088.

### Slice 3 — Supported-target and MO2 snapshot reconstruction

Deliver:

- explicit MO2 instance/profile selection;
- MO2 `2.5.2` identity/configuration validation;
- quiescence checks and double structural capture;
- enabled mod/plugin order and qualified loose provider/hidden/deleted/
  unmanaged reconstruction;
- physical installed entity separated from source identity;
- exact game-runtime manifest admission;
- snapshot assurance and mid-capture change failure;
- no MO2 process launch and no USVFS operation.

Gates:

- EVAL-0045, EVAL-0046 for every read/library operation, EVAL-0051, and
  EVAL-0054.

### Slice 4 — Positively qualified Bethesda semantics and typed indexes

Deliver:

- ordered-plugin input supplied only from the accepted snapshot;
- explicit Mutagen `0.54.2` shape/field allowlist;
- master/FormKey translation, override chains, winner and link resolution;
- selected NPC/package/appearance, loose FaceGen presence, REFR relation and
  placement, and forced-reference alias facts;
- explicit unsupported/gap results outside the allowlist;
- canonical participant identities and typed indexes used by M1 analyzers.

Gates:

- EVAL-0052 and applicable EVAL-0086 assertions.

### Slice 5 — Evidence, documentation, candidates, cases, and replay

Deliver:

- retained local/fixture documentation source revision and exact passage;
- schema-bound deterministic claim import/extraction path;
- claim applicability and declared-purpose assignments;
- causal joins and deterministic/mandatory candidate lanes;
- score-independent candidate admission;
- typed hypothesis/finding threshold and abstention;
- evidence-bearing case grouping;
- immutable lineage/reconciliation;
- complete retained downstream replay for synthetic fixtures;
- human-readable and JSON coverage/gap reporting.

No live LLM work is authorized in this slice.

Gates:

- EVAL-0032, EVAL-0037, EVAL-0039, EVAL-0067, EVAL-0079, and
  EVAL-0083 through EVAL-0086 applicable local paths.

### Slice 6 — Direct OpenAI credential, budget, and semantic operations

Deliver:

- Credential Manager exact-target wrapper and native one-shot helper;
- recoverable enrollment/replacement/deletion intents and generation epochs;
- direct synchronous Responses adapter with exact host allowlist and structured
  schema;
- the exact accepted M1 model/profile, with no alias or fallback;
- one request assignment per helper process;
- context minimization and untrusted-text containment;
- immutable price/capability snapshot, finite worst-case reservation, final
  dispatch fence, one-owned usage, reconciliation, and unresolved hold;
- explicit offline/unavailable-provider behavior;
- exact retained request/response/requested-and-returned-model/prompt/schema/
  settings/tokens/cost and capability/price snapshots;
- exact replay from the retained original response, distinct from a new live
  re-execution; and
- drift detection that invalidates baseline comparability when the returned
  model or material capability semantics change;
- one bounded live source-claim extraction over a project-authored,
  independently adjudicated source package; and
- one bounded live evidence-bound candidate investigation over
  project-authored positive and matched-negative candidate packages.

The first live request occurs only after all non-live credential, security, and
budget cases pass. It uses a user-supplied test access profile and a deliberately
small hard limit. That qualification request proves dispatch and settlement
plumbing only. The two semantic requests run only after it passes, each with
its own explicit authorization, finite reservation, strict operation-specific
schema, retained result, typed oracle assertions, and settlement. They are not
replaced by a canned transcript or by reusing the qualification response.

Gates:

- EVAL-0033 through EVAL-0035, EVAL-0064, EVAL-0067, EVAL-0076, EVAL-0077,
  EVAL-0081 synchronous path, EVAL-0083, and EVAL-0089.

### Slice 7 — Synthetic generic reversion proof

Deliver:

- category-neutral stale-value/relation candidate analyzer;
- first actor/AI/FaceGen domain interpretation;
- materially different REFR/link/placement domain interpretation;
- supported finding/case construction, symptoms, effect extent, and
  remediation/validation;
- matched intentional/harmless negatives and ambiguity abstention;
- rename, unrelated-addition/reorder, relevant-winner, malformed, and
  unsupported metamorphic tests.

Gates:

- EVAL-0001, EVAL-0002, EVAL-0032, EVAL-0084 through EVAL-0086.

The production mechanism and generic types may not contain fixture or real-mod
names/IDs.

### Slice 8 — Controlled-real generalization

Deliver:

- private-manifest validation and exact input fingerprint checks;
- EVAL-0016 positive and package-specific matched patch control;
- EVAL-0017 positive and linked-reference/placement patch control;
- declared-purpose/document passage applicability;
- explicit limits on unvalidated runtime symptoms and incomplete patch fields;
- held-out replacement bookkeeping if any result drives a production change.

Gates:

- EVAL-0016 and EVAL-0017 plus every upstream local/provider/record case used
  by them.

### Slice 9 — End-to-end output, clean/replay equivalence, and closeout

Deliver:

- one complete synthetic and one complete controlled-real run from CLI;
- human-readable summary and `infinium.run-output/v1`;
- exact complete replay from retained fixture dependencies;
- clean versus incremental equivalence at each exercised layer;
- non-mutation and secret-canary reports;
- failure/gap/unsupported demonstration;
- required-case result index and M1 completion record.

Gates:

- EVAL-0040 and all milestone-required cases in the accepted evaluation
  baseline.

## Requirement-to-evaluation traceability

Only the following requirement behavior is claimed by M1. Broader delivery
requirements remain future work.

| M1 claim | Requirements | Primary cases |
|---|---|---|
| Exact target/profile admission | SCOPE-001 through SCOPE-004, SCOPE-006, and the M1 plugin/record/qualified-loose subset of SCOPE-005; archive/generated/configuration/root breadth remains an explicit gap | EVAL-0045, EVAL-0051, EVAL-0054 |
| Read-only and authorized writes/operations | AUTH-001 through AUTH-003 | EVAL-0046, EVAL-0080 |
| Untrusted content, credentials, operation boundary, sensitive traces | SEC-001 through SEC-004 | EVAL-0033 through EVAL-0035, EVAL-0080, EVAL-0089 |
| Modular development configuration and retained run behavior | SCAN-001, SCAN-002, SCAN-006, and SCAN-009; M1-bounded lifecycle transitions, exact work counters, usage/cost accounting, checkpoint mechanics, and dependency-valid reuse toward SCAN-004, SCAN-005, and SCAN-007 without claiming their complete hierarchical progress/ETA, user-control, or M3 delivery | EVAL-0026, EVAL-0037 through EVAL-0040, EVAL-0081, EVAL-0082, EVAL-0088 |
| Snapshot/context/configuration/replay truth | SNAP-001, SNAP-002, SNAP-005, and SNAP-006; M1-bounded dependency fingerprints/invalidation toward SNAP-003 and SNAP-004 without claiming historical stale-result presentation or their complete later delivery | EVAL-0026, EVAL-0037, EVAL-0051, EVAL-0083, EVAL-0087 |
| Typed evidence, provenance, hierarchy, LLM visibility, hypotheses, abstention, raw development output | EVID-001 through EVID-007 | EVAL-0001, EVAL-0002, EVAL-0032, EVAL-0067, EVAL-0083 |
| Finding classification, grouping, extent, recommendation, leads, continuity | FIND-001 through FIND-004, FIND-011, FIND-014 | EVAL-0001, EVAL-0002, EVAL-0079, EVAL-0084, EVAL-0086 |
| Honest coverage/no-safety claim | PROD-002, PROD-004, COVER-001 through COVER-003 | EVAL-0085 |
| Semantic reversion and cross-layer analysis | ANALYSIS-003 through ANALYSIS-005, ANALYSIS-016, ANALYSIS-017 | EVAL-0001, EVAL-0002, EVAL-0016, EVAL-0017, EVAL-0032, EVAL-0065 |
| Local documentation evidence and acquisition/application separation | DOC-002, local installed-entity portion of DOC-003, local/fixture source-retention and claim-application portions of DOC-006 through DOC-008, and DOC-011; no Nexus/LOOT/community/mapped-non-Nexus acquisition, DOC-004 adjudication, or DOC-005 conflict-resolution claim | EVAL-0033, EVAL-0037, EVAL-0039, EVAL-0067, EVAL-0083 |
| Direct OpenAI/offline/context/cost/reproducibility/user-owned access | M1 backend portions of AI-001 through AI-007 and OPS-001; no AI-002 user-facing UI/model-routing delivery beyond exact CLI/configuration disclosure and the single accepted profile | EVAL-0034, EVAL-0064, EVAL-0067, EVAL-0076, EVAL-0077, EVAL-0081, EVAL-0083, EVAL-0089 |
| M1 local history and run-owned output | M1 persistence/history subset of OPS-002 without user retention/deletion UX; M1 portion of OPS-003 | EVAL-0040, EVAL-0079, EVAL-0087 |
| M1 GPL/dependency posture | DIST-001, DIST-002 | dependency/licence audit plus EVAL-0046/0080 for technical authority; public redistribution is not claimed |
| MO2/Mutagen environment disclosure | M1 MO2 portion of TOOL-001; M1 CLI/configuration portions of TOOL-002 and TOOL-003; no LOOT detection/integration delivery claim | EVAL-0046, EVAL-0051, EVAL-0052 |

No M1 claim is made for M2 UX requirements, M3 maturity/preset/scale breadth,
runtime validation/log application, or M4 packaging/distribution.

## Verification commands

The accepted implementation must keep these commands working from repository
root:

```powershell
dotnet restore Infinium.sln --locked-mode
dotnet build Infinium.sln -c Release --no-restore
dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Unit"
dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Contract"
dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Integration"
dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Evaluation"
dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Security"
dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Fault"
dotnet run --project src/Infinium.Cli -c Release --no-build -- evaluate --manifest test-data/manifests/m1-suite.json --output artifacts/m1-evaluation
dotnet run --project src/Infinium.Cli -c Release --no-build -- verify-evaluation --input artifacts/m1-evaluation
git diff --check
```

Private controlled-real runs use an untracked evaluator-supplied manifest path;
the retained public result records only permitted identities, hashes, structural
expectations, and omission markers.

The implementation plan may add narrower commands, but changing these
milestone entry points requires a reviewed plan amendment.

## Review cycle

For each slice:

1. implement the complete declared contract;
2. run the slice's unit, contract, integration, security/fault, and evaluation
   subset;
3. inspect raw outputs, failures, abstentions, gaps, provenance, and coverage;
4. perform a semantic review against requirements, ADRs, anti-overfitting
   rules, and expected case answers;
5. correct issues;
6. rerun the affected checks and the accumulated M1 regression set;
7. record exact commands/results and intentional behavior changes.

Test success without semantic/diff review is insufficient.

## Completion criteria

M1 is complete only when:

- every slice is complete;
- every required case has an accepted specification and retained passing run
  against one exact implementation commit;
- all commands above pass from a clean worktree;
- EVAL-0016 and EVAL-0017 pass without fixture-specific production behavior;
- every supported Mutagen shape has independent ground truth;
- the first live OpenAI work passes credential, context, reservation,
  provider-capability, billing-authority, provenance, drift, and secret-canary
  gates under the accepted exact model/profile;
- the synthetic proof replays completely from retained dependencies;
- no protected setup root changes;
- human-readable and JSON results agree semantically;
- failures, unsupported capability, coverage gaps, and uncertainty remain
  visible;
- no excluded capability is implied by naming or output;
- requirement-to-case-to-slice traceability has no material gap; and
- the completion record is reviewed and accepted by the project owner.

## Rollback and migration

M1 creates no supported legacy-data migration and reads no authoritative state
from `legacy/`. Development databases and payload stores are disposable until
an accepted later plan declares compatibility.

Every slice must permit:

- deleting only its product-owned disposable development state;
- rebuilding projections from authoritative retained records;
- restoring the last consistent database/CAS pair where fault testing applies;
- disabling an incomplete analyzer/provider capability without fabricating
  coverage; and
- reverting code without modifying the user's MO2/game setup.

## Completion record

To be filled only after M1 completes:

- accepted plan revision:
- implementation commit:
- exact SDK/dependency/native versions:
- accepted evaluation specification revisions:
- required case result index:
- verification commands and results:
- retained run/artifact IDs:
- controlled-real manifest revisions:
- security/non-mutation/secret review:
- known gaps and excluded capabilities:
- intentional behavior changes:
- completion date:
- accepted by:

## Deferred follow-up

- RQ-028 numeric readiness/maturity thresholds and calibrated presets;
- RQ-029 runtime-log provenance;
- RQ-030 packaging/signing/update architecture;
- LOOT/Nexus/search integration;
- archive/NIF/PEX/native/generated/configuration analyzer breadth;
- frontend workflow;
- M3 scale and personal-trust calibration; and
- M4 public packaging, exports, supportability, and policy confirmation.
