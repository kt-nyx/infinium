# Evaluation strategy

Status: Draft  
Last reviewed: 2026-07-26

Infinium is not trustworthy because it produces plausible reports. It becomes
trustworthy only when evidence reconstruction, candidate selection, semantic
classification, abstention, and user-facing/release presentation are evaluated
separately against known answers.

ADR-0008 through ADR-0011 accept Wave B's target boundaries and qualification
obligations, not the full Wave F case specifications or their execution. Until
the named cases pass, no MO2, Mutagen, snapshot, runtime, or LOOT surface may
be presented as qualified merely because its research recommendation was
accepted. ADR-0007 excludes xEdit from both production and evaluation;
Bethesda expected results must be specified independently of the Mutagen path
under test.

## Evaluation layers

### 1. State reconstruction

Verify:

- selected profile identity;
- enabled mods and priorities;
- enabled plugins and load order;
- file/archive provider chains;
- record override chains and winners;
- root/runtime/configuration state.

Ground truth comes from controlled fixtures, agreement with authoritative MO2
effective-state behavior, and independently specified record expectations
grounded in hand-audited binary/structure assertions, format invariants, and
retained adjudication. The Mutagen code path under test may not be the sole
source of its own expected results.

### 2. Deterministic analyzer correctness

For each analyzer:

- positive fixtures;
- structurally similar negative fixtures;
- boundary cases;
- malformed/unsupported input;
- changed-version behavior;
- faithful reproduction of configured external-tool inputs and outputs,
  including LOOT;
- clean/incremental equivalence under the same resolved inputs;
- declared abstention.

### 3. Candidate selection

Measure whether meaningful interactions enter investigation without requiring
all-pairs comparison. Track:

- planted candidate recall;
- candidate volume;
- irrelevant candidate rate;
- cost/latency contribution;
- reason selected;
- canonical participant and causal-join correctness;
- mandatory-lane membership independent of ranking-score perturbation;
- explicit matched-negative, unsupported, and gap handling;
- coverage denominators for every exercised typed index and join.

### 4. Documentation extraction

Evaluate:

- exact citation support;
- entity and version applicability;
- claim type;
- conditions and exceptions;
- conflict/supersession handling;
- abstention when text is ambiguous;
- resistance to instructions embedded in source content.

### 5. Semantic investigation

Evaluate:

- planted harmful interaction detected;
- matched intentional interaction not misclassified;
- supporting and contradicting evidence;
- correct cause grouping;
- faceted effect extent, causal propagation, and symptoms;
- useful remediation or validation;
- uncertainty.

### 6. Case construction

Verify that several observations/findings with one cause form one case and that
distinct causes involving the same mod remain separate. Lead-only investigation
cases remain distinct from supported cases, cannot affect readiness, and gain a
new linked revision if a hypothesis is promoted to a finding.

### 7. User-facing/release presentation

User-facing/release maturity and severity policies are evaluated after raw
analyzer output. Development runs retain all candidates and do not hide results
based on maturity. Presentation policy must not change the underlying typed
classification between observation, claim, hypothesis/lead, finding, and
recommendation.

### 8. Security and privacy boundaries

Evaluate untrusted-content isolation, prompt-injection resistance, credential
redaction/storage boundaries, privileged-operation validation, and
diagnostic/export sensitivity without assuming a particular desktop stack.
Also verify that approved external-tool operations leave the modding setup
unchanged, record allowed cache/temp side effects, and that profile/source
changes never initiate analysis or paid/network work by themselves.

### 9. Operational lifecycle and provenance

Evaluate run/input immutability, analysis-versus-acquisition ownership,
clean/reuse/refresh separation, dependency-aware carryover, pause/cancel/limit
terminal behavior, cost rollups, retention/deletion effects, export
immutability, product-write authority isolation, and targeted-run readiness
boundaries. Verify in particular that permitted source material is not
discarded before configured extraction, analysis, case/finding synthesis,
prose, provenance, and audit consumers complete, and that later minimization
or deletion produces accurate dependency and gap reporting.

## Taxonomy-dependent evaluation

Evaluation selection and reporting must use the accepted
[Skyrim SE mod-impact taxonomy](../product/mod-impact-taxonomy.md) and its
versioned distinctions among declared purpose and intended target, technical
modification surface, affected game system or content area, consequence type,
and effect extent. The corpus should stratify positive,
matched-negative, boundary, and unsupported cases across those dimensions
rather than treating hosting-site categories, record families, or the first
NPC proof as a complete inventory of game behavior. Metrics must disclose
taxonomy areas with no or insufficient evaluation coverage.

## Profile ladder

1. Atomic synthetic fixtures
2. Small multi-analyzer synthetic profiles
3. Small controlled real-mod profiles
4. Medium representative profiles
5. Creator's large real profile as a private scale/shape reference
6. Upper-bound stress profiles

Scale testing does not replace correctness testing.
`Brain Blast Destruction 2024` occupies only rung 5. It is a real, previously
used profile, but it is not a correctness oracle, gold standard, representative
corpus, or source of product-specific rules. Atomic synthetic fixtures and
small controlled real-mod profiles establish behavior first.

## Core metrics

Exact thresholds are set after a baseline corpus exists.

- state reconstruction accuracy;
- planted-problem recall;
- matched-negative precision;
- citation correctness;
- end-to-end provenance completeness and correctness;
- version/applicability correctness;
- source-level versus local claim-adjudication scope correctness;
- false-positive and abstention rate;
- case grouping accuracy;
- classification accuracy and evaluation coverage across the accepted
  declared-purpose, technical-surface, affected-area, consequence, and
  effect-extent taxonomy axes;
- cross-run finding/case reconciliation accuracy, including false-merge and
  false-split rates;
- supported-case versus lead-only classification/count correctness;
- remediation/validation usefulness adjudication;
- clean/incremental semantic equivalence;
- clean-recomputation versus source-refresh provenance;
- pause/resume/cancel terminal-state correctness;
- independent acquisition/application provenance;
- symptom-report provenance and non-retroactive revision behavior;
- carryover provenance and finding/case-revision lineage;
- run/review-state/time-bound and provisional readiness correctness;
- advisory/readiness and unverified-resolution presentation correctness;
- deterministic repeatability;
- LLM semantic consistency;
- replayability-status and audit-gap correctness;
- runtime, memory, disk, network, tokens, and cost;
- cost ownership/rollup correctness;
- concurrent budget reservation/reconciliation and hard-limit correctness;
- coverage and failure-report correctness;
- export provenance/source immutability, sharing-class/source-policy
  compliance, and retention-impact correctness;
- useful-analysis source-retention and post-materialization minimization
  correctness;
- product-write authority and protected-setup isolation correctness;
- manual-initiation and external-tool non-mutation correctness.

## User adjudication

When a user adjudication is retained as labeled evaluation data, it keeps scope
appropriate to the decision rather than becoming a universal rule:

- finding dispositions retain the exact finding revision, the decision's
  installation-snapshot/analysis-context applicability, the finding's
  originating run and resolved inputs, and any explicit carryover lineage;
- local applicability decisions retain their installation snapshot, analysis
  context, affected claim/revision, and decision provenance;
- source/extraction reviews retain claim, source revision, extraction method,
  and extractor/prompt/model identity without being made profile-specific;
- confirmation, correction, or rejection of an inferred assumption retains the
  profile/context, inference provenance, dependencies, and distinction from a
  directly user-provided assumption.

Each retained label also records:

- fixture identity where applicable;
- retained original evidence/output or its deletion/provenance record;
- analyzer/ruleset/tool/model versions;
- user label;
- optional explanation;
- later review history.

It does not automatically become a production rule or universal truth.

## Reproducibility

Deterministic stages should reproduce exactly for identical declared inputs.

LLM stages retain exact request/response/configuration when the applicable
retention policy permits it. Replay of a retained model output is exact. A
forced clean LLM rerun creates a distinct run and may differ linguistically,
but must meet the same semantic assertions on the evaluation corpus. Evaluation
also verifies that a run's declared complete, partial, or unavailable
replayability matches the dependencies actually retained.

Clean analytical recomputation is compared with incremental execution against
the same resolved evidence. Clean extraction is compared against the same
resolved source revision/bytes. Live source refresh is a separate operation and
may legitimately change the resolved evidence; it must not be used as the basis
for a false cache-equivalence failure.

Retention-policy evaluation must also demonstrate that a source permitted for
private use survives through every configured dependent evaluation stage that
requires it. A metadata-only post-materialization state is valid only when the
remaining artifacts honestly expose any lost clean-extraction, replay,
citation, or audit capability.

## Acceptance bar for personal trust

- no fabricated citations or definitive unsupported claims;
- agreement with MO2 for effective state and with independently specified
  fixture truth for every supported record/field/shape;
- faithful reproduction and provenance of invoked LOOT metadata, userlist
  inputs, and diagnostics;
- known planted problems reliably detected;
- harmless counterparts not misclassified;
- failures and coverage gaps always visible;
- stale installation snapshots or analysis contexts unmistakable;
- useful next actions;
- no new work authorized past declared deadlines or consumptive limits, with
  any uninterruptible in-flight overrun or provider-side variance disclosed
  and reconciled;
- runs declared completely replayable produce stable downstream outcomes, and
  runs with missing dependencies disclose the resulting replay limits.

M3 cannot pass this bar without EVAL-0018 or successor scale cases demonstrating
OPS-004 at the creator's large profile and an upper-bound stress profile. It
also cannot pass without EVAL-0051 through EVAL-0053, or reviewed successor
cases, demonstrating MO2 effective-state agreement, independently specified
Bethesda record-semantic correctness, and faithful LOOT integration. EVAL-0055
or a successor must
demonstrate the configured full-documentation coverage/accounting contract.
M2 cannot claim its workflow proof without EVAL-0056 or a reviewed successor.
At every milestone, each requirement claimed complete must have at least one
reviewed linked evaluation case, and all of its gating cases for the delivered
scope must pass.
