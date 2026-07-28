# RESEARCH-0047: Readiness and analyzer-maturity calibration plan

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Acceptance: Recommendation accepted by the project owner on 2026-07-28

Primary RQ: RQ-028

M0 wave: F — Evaluation specifications, deferred-question ledger, and M1 plan

Decision enabled: Empirical evidence-collection and calibration plan for later
M3/M4 analyzer-maturity, readiness, stale-result, targeted-run carryover, and
user-facing filtering policies

## Executive answer

Infinium should not choose numerical analyzer-maturity, false-positive,
coverage, stale-result, targeted-run carryover, or user-facing filtering
thresholds during M0. The analyzers and representative evaluation corpus do not
exist yet, so any number chosen now would describe preference rather than
measured product behavior.

Wave F should instead accept a **versioned calibration protocol** that:

1. preserves the product's already accepted categorical semantics;
2. collects immutable raw output before any maturity or presentation filter;
3. partitions cases into development, calibration/validation, held-out
   acceptance, and later field-monitoring populations without causal-family or
   near-duplicate leakage;
4. measures each analyzer revision within its exact declared scope using
   positives, matched negatives, boundaries, malformed/unsupported cases,
   taxonomy-stratified coverage, and controlled-real evidence;
5. reports precision, recall, false-positive and false-negative consequences,
   abstention, provenance, coverage, readiness-decision error, and operational
   cost separately rather than collapsing them into one score;
6. reports uncertainty and independent sample/cluster counts with every rate;
7. calibrates candidate policies on validation data, freezes a policy version,
   and evaluates it once on untouched held-out data;
8. treats dependency closure for staleness and targeted-run carryover as a
   logical proof obligation, not a percentage that can be waived by a good
   aggregate score; and
9. creates separate, evidence-backed M3 and M4 readiness-policy versions rather
   than assuming one threshold is appropriate for personal and public use.

The later threshold-setting decision should start from the cost of each error.
A false-ready outcome, invalid carryover, fabricated authority, or missed
material staleness is not interchangeable with an extra review item. A
readiness-blocking Blocker/Major false positive is likewise more disruptive
than a low-priority lead. Thresholds therefore need per-decision and
per-analyzer constraints plus explicit minimum evidence sufficiency; a single
global precision, recall, confidence, maturity, or coverage number is
inadequate.

This report defines the evidence and decision procedure. It deliberately does
not claim that `Preview`, `Reliable`, or `Trusted` is the M3/M4 blocking
threshold, does not set a minimum sample count, and does not set acceptable
error percentages. Those values become defensible only after accepted analyzer
contracts, pre-registered cases, and retained evaluation runs exist.

## 1. Question and governing constraints

RQ-028 asks:

> What evidence, analyzer-maturity, false-positive, coverage, targeted-run
> carryover, and stale-result thresholds govern M3/M4 readiness and
> user-facing filtering?

The question has two parts that must remain separate:

- **already accepted product semantics**, which this investigation cannot
  renegotiate; and
- **empirical threshold values and release gates**, which require future
  analyzer data.

The plan is governed principally by:

- [PROD-004](../../product/requirements.md#prod-004--no-safety-guarantee):
  absence of findings is not proof of safety;
- [SCAN-010](../../product/requirements.md#scan-010--calibrated-user-presets):
  user-facing presets must be based on measured coverage, time, and cost;
- [SNAP-003](../../product/requirements.md#snap-003--stale-result-presentation)
  and
  [SNAP-004](../../product/requirements.md#snap-004--safe-carryover):
  staleness and carryover depend on material inputs and dependency-aware
  validity;
- [EVID-006](../../product/requirements.md#evid-006--abstention) and
  [EVID-007](../../product/requirements.md#evid-007--development-transparency):
  unsupported conclusions abstain, while development evaluation retains raw
  candidates, evidence, failures, and abstentions;
- [FIND-001](../../product/requirements.md#find-001--independent-dimensions):
  severity, confidence, evidence, taxonomy classification, maturity, and
  review state remain independent;
- [FIND-007](../../product/requirements.md#find-007--categorical-readiness)
  through
  [FIND-014](../../product/requirements.md#find-014--cross-run-finding-and-case-identity):
  categorical readiness, evidence/maturity eligibility, lead separation,
  run-bound evaluation, advisories, and cross-run continuity;
- [COVER-001](../../product/requirements.md#cover-001--coverage-states)
  through
  [COVER-003](../../product/requirements.md#cover-003--readiness-and-gaps):
  population-specific coverage and material gaps;
- [ANALYSIS-016](../../product/requirements.md#analysis-016--declared-analyzer-contract):
  each analyzer declares scope, thresholds, coverage semantics, maturity, and
  evaluation cases;
- [OPS-004](../../product/requirements.md#ops-004--high-end-scale): M3 includes
  the high-end scale target;
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md):
  model output cannot become unvalidated local or evidence authority;
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md):
  analytical output, review policy, and readiness evaluations have distinct
  immutable identities;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md):
  reuse requires the smallest complete dependency closure;
- [ADR-0022](../../architecture/decisions/ADR-0022-finding-and-case-continuity-and-reconciliation.md):
  continuity and review-state carryover require independent causal,
  applicability, dependency, and compatibility gates; and
- the accepted
  [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md),
  [fixture guidelines](../../evaluation/fixture-guidelines.md), and current
  [evaluation strategy](../../evaluation/evaluation-strategy.md).

## 2. Scope and explicit non-scope

### In scope

- an empirical corpus and partitioning plan for maturity/readiness calibration;
- matched-negative and boundary-case construction;
- taxonomy-aware stratification without a false Cartesian-completeness claim;
- per-analyzer, per-policy, and end-to-end metrics;
- false-positive, false-negative, false-ready, false-block, and abstention
  consequences;
- uncertainty reporting and evidence-sufficiency principles;
- readiness-policy calibration and held-out acceptance;
- targeted-run full-policy carryover and scope-limited-result evaluation;
- material-staleness detection and false-stale/missed-stale evaluation;
- user-facing filtering and review-burden calibration;
- M3-versus-M4 evidence differences;
- threshold-change, regression, demotion, and replacement-holdout governance;
- exact future evaluation and acceptance artifacts.

### Out of scope

- selecting any numerical release threshold before data exists;
- changing accepted severity, confidence, maturity, coverage, or readiness
  meanings;
- turning maturity into finding confidence or evidence authority;
- changing a hypothesis/lead into a finding through presentation policy;
- choosing or implementing an analyzer;
- creating executable fixtures or running an analyzer that does not exist;
- claiming that taxonomy `0.1.0` has complete evaluation coverage;
- treating the creator's large profile as correctness ground truth;
- setting M4 packaging, signing, update, or public-support policy;
- defining runtime-log association thresholds owned by RQ-029;
- editing the product baseline, evaluation catalog, RQ registry, ADRs, or M0
  plan in this bounded investigation.

## 3. Findings: accepted semantics versus later empirical decisions

The following are already authoritative and are not threshold candidates:

| Accepted semantic | Consequence for calibration |
|---|---|
| Readiness is categorical, not a safety score | Metrics may inform a policy but the UI does not expose an opaque probability of a safe playthrough |
| Severity and confidence are independent | Low confidence cannot reduce credible consequence; calibration evaluates each dimension separately |
| Analyzer maturity is evaluated reliability, not finding confidence | An analyzer cannot make a weakly evidenced item stronger merely by being mature |
| Finding admission precedes presentation/readiness policy | A maturity/filtering policy cannot relabel leads and findings |
| Development evaluation retains raw output | Maturity and default filters are applied only to stored typed results |
| Lead-only cases cannot affect readiness | Calibration measures lead routing separately from finding/readiness correctness |
| Advisories do not block by default | An individual user `action-required` decision is a separate readiness input |
| Readiness binds one run, policy, coverage scope, disposition set, and time | A new policy evaluation does not mutate the run or earlier evaluation |
| Targeted runs replace broad readiness only with full-policy scope plus validated carryover | Aggregate similarity or user approval cannot fill a missing dependency closure |
| Materially changed dependencies make applicable results stale | Elapsed age alone is not the universal staleness rule |
| Unlike coverage denominators remain separate | No overall coverage percentage can compensate for an unevaluated required population |

The later empirical decisions are:

- the evidence volume and diversity sufficient to assign each maturity level to
  one exact analyzer/identity-contract revision and declared scope;
- which maturity levels may affect M3 and M4 readiness;
- precision/recall and uncertainty bounds required for each decision lane;
- which coverage gaps are material under each readiness policy;
- default routing/filtering behavior for non-blocking findings and leads;
- acceptable false-block and review-burden behavior;
- evidence needed to maintain, demote, or restore maturity after change; and
- the exact M3 and M4 acceptance thresholds.

## 4. Current primary methodological sources

These sources guide evaluation design and uncertainty reporting. They do not
dictate Infinium's risk tolerance or substitute for Skyrim-domain ground truth.

| Source | Exact identity and retrieval | Authority and claim-level relevance |
|---|---|---|
| [NIST AI Risk Management Framework Core](https://airc.nist.gov/airmf-resources/airmf/5-sec-core/) | AI RMF 1.0, NIST AI 100-1; AIRC rendering retrieved 2026-07-28; AIRC notes that a revision is in progress | Primary US standards guidance. Measure calls for documented test sets and metrics, performance under deployment-like conditions, uncertainty, benchmarks, independent review, stated generalization limits, and repeated evaluation. It supports an evidence-backed, revisable process rather than invented thresholds. |
| [NIST/SEMATECH e-Handbook: confidence intervals for proportions](https://www.itl.nist.gov/div898/handbook/prc/section2/prc241.htm) | Current public handbook page retrieved 2026-07-28 | Primary NIST statistical guidance. It documents Wilson-style intervals and exact binomial intervals for small samples/failure counts. It supports reporting bounds rather than treating an observed zero-error rate as a true zero. |
| [NIST/SEMATECH e-Handbook: sample sizes required](https://www.itl.nist.gov/div898/handbook/prc/section2/prc242.htm) | Current public handbook page retrieved 2026-07-28 | Primary NIST statistical guidance. It derives sample size from a preselected detectable difference, significance, and power. It supports choosing risk/precision requirements before deriving sample size, not selecting a convenient universal count. |
| [scikit-learn classification metrics](https://scikit-learn.org/stable/modules/model_evaluation.html#classification-metrics) | scikit-learn 1.9.0 documentation displayed at retrieval, 2026-07-28 | Maintainer documentation for metric semantics. It supports separate confusion counts, precision, recall, and related measures rather than one aggregate score. Infinium remains responsible for domain units, grouping, and error cost. |
| [scikit-learn grouped cross-validation](https://scikit-learn.org/stable/modules/cross_validation.html#cross-validation-iterators-for-grouped-data) | scikit-learn 1.9.0 documentation displayed at retrieval, 2026-07-28 | Maintainer documentation. It explains that dependent samples violate independent-example assumptions and that groups must remain isolated across splits. This supports grouping fixture families, source lineages, and related profile variants. |
| [scikit-learn probability calibration](https://scikit-learn.org/stable/modules/calibration.html) | scikit-learn 1.9.0 documentation displayed at retrieval, 2026-07-28 | Maintainer documentation. It defines reliability calibration and requires calibration data independent of fitting data. It applies only if an analyzer emits a claimed numerical probability; Infinium's accepted categorical confidence does not imply such a probability. |
| [Google Machine Learning: dividing datasets](https://developers.google.com/machine-learning/crash-course/overfitting/dividing-datasets) | Current Google for Developers page retrieved 2026-07-28 | Primary maintainer training guidance. It distinguishes training, validation, and test sets, warns that repeatedly consulted test sets wear out, and requires duplicate control and deployment relevance. |
| [OpenAI: trustworthy third-party evaluations](https://openai.com/index/trustworthy-third-party-evaluations-foundations/) | OpenAI publication dated 2026-05-29, retrieved 2026-07-28 | Primary provider guidance relevant to Infinium's LLM-backed paths. It identifies the tested system/harness, tool access, retries, resource budget, claim, and validity checks as part of the evaluation result. It does not make an OpenAI model its own grader or establish Skyrim ground truth. |

### Source limitations

- These sources supply general measurement discipline, not Infinium-specific
  acceptance numbers.
- Standard binomial intervals assume independent Bernoulli trials. Many
  Infinium examples share a mod, causal family, profile, source passage, or
  transformation; reporting must account for that grouping rather than
  pretending every emitted finding is independent.
- Generic classifier metrics do not capture provenance correctness,
  unsupported-state handling, case grouping, dependency validity, or the
  different cost of false-ready and false-block outcomes.
- Probability calibration is inapplicable unless an output is explicitly
  defined and evaluated as a probability. An LLM's verbal or self-reported
  confidence must not be treated as a calibrated probability.

## 5. Research method and bounded experiment

No analyzer or executable readiness policy exists, so this investigation does
not fabricate a performance probe. It performed a bounded documentary
construct audit instead.

Environment:

- repository revision observed: `02376f7` on branch `main`, with pre-existing
  uncommitted documentation work;
- shell: PowerShell `7.6.3`;
- date: 2026-07-28;
- side effects: read-only inspection plus creation of this report;
- external access: public primary documentation listed above.

Procedure:

1. Cross-reference RQ-028 against accepted product meanings and ADRs.
2. Inventory existing evaluation cases that already exercise readiness,
   maturity, coverage, staleness, carryover, suppression, dispositions, and
   filtering.
3. Identify which assertions are logical invariants and which require
   empirical calibration.
4. Define dataset, unit, partition, metric, uncertainty, acceptance, and
   governance requirements without assigning unsupported numerical values.
5. Check the plan against anti-overfitting and held-out-data rules.

Artifact manifest:

| Artifact | State |
|---|---|
| This report | Tracked Markdown proposal; no private or binary evidence |
| Analyzer outputs | None; no analyzer exists for this purpose |
| Metric tables | Not produced; producing numbers without runs would be false precision |
| External source cache | None added to the repository |

The result is a calibration **protocol proposal**, not evidence that any
analyzer or readiness policy meets a maturity level.

## 6. Evaluation population and partition plan

### 6.1 Primary evaluation unit

The smallest scored unit must match the decision being evaluated:

- candidate admission: one eligible planted or independently adjudicated
  interaction;
- finding admission: one expected analytical condition, not every repeated
  output row it generates;
- case grouping: one expected shared-cause grouping decision;
- finding/case continuity: one cross-run reconciliation decision;
- coverage: one declared population member and its coverage outcome;
- staleness: one dependency-change scenario and expected affected closure;
- carryover: one proposed reuse edge and its complete validity decision;
- readiness: one run/policy/disposition/scope evaluation scenario;
- filtering: one result and its expected default/display route; and
- user review burden: one complete queue or profile-level review session.

Repeated symptoms, records, files, citations, or transformed variants under one
cause may provide useful sub-assertions, but they are not independent trials
for confidence calculations.

### 6.2 Required partitions

Every case family is assigned before execution:

1. **Development**
   - visible to implementation and prompt/rule authors;
   - used for debugging, error taxonomy, and contract formation;
   - never used as the sole maturity estimate.
2. **Calibration/validation**
   - used to select evidence thresholds, maturity eligibility, default routing,
     and readiness-policy candidates;
   - grouped away from development analogues where the claimed generalization
     requires that independence;
   - becomes development if its answer drives analyzer changes rather than
     policy calibration alone.
3. **Held-out acceptance**
   - policy and analyzer revision are frozen before reveal;
   - used once for the declared M3 or M4 decision;
   - becomes development after a revealed result changes production behavior,
     and receives a materially independent replacement.
4. **Field/adjudication monitoring**
   - later user-reviewed outcomes, incidents, false-positive decisions, and
     newly discovered misses;
   - retains exact applicability and cannot silently become universal truth;
   - may trigger demotion, new cases, and future policy calibration, but does
     not rewrite the historical acceptance run.
5. **Scale/operational**
   - creator profile and synthetic upper-bound profiles;
   - measures latency, memory, disk, candidate volume, queue size, and cost;
   - does not count as independent semantic correctness merely because it is
     large.

### 6.3 Group isolation and leakage control

Splits must keep the following related material in one group unless the
evaluation explicitly tests a pre-registered transformation:

- one causal mechanism instantiated through near-identical fixture variants;
- positive and trivially answer-revealing derivatives;
- versions or forks of the same mod combination;
- one source passage and paraphrases/extractions derived from it;
- one upstream plugin/asset fixture and its mutation family;
- one profile snapshot and mechanically altered copies;
- one author patch and fixtures whose expected answer directly encodes it; and
- one prompt/ruleset-tuning incident and its immediate reproductions.

The group manifest records why examples are dependent. Partition reports show
both raw item counts and independent group/family/profile counts.

### 6.4 Required case composition

For every analyzer's declared scope:

- independently constructed positives for each claimed detection mechanism;
- matched negatives with the same tempting structure but a harmless,
  intentional, inapplicable, already-correct, or unsupported outcome;
- boundary cases around evidence sufficiency, applicability, version, and
  dependency change;
- malformed and inaccessible inputs;
- explicit unsupported, unknown, unmapped, not-applicable, abstained, failed,
  skipped, and limited outcomes;
- metamorphic variants required by the anti-overfitting policy;
- controlled-real cases where redistribution/source policy permits;
- at least one materially different category proof before a mechanism is
  described as generic; and
- profile-level mixtures that expose competition among multiple plausible
  cases rather than only isolated toy decisions.

Every harmful positive should have one or more **causally matched negatives**,
not merely random clean examples. Useful negative families include:

- the same override shape where documentation supports the winner's intent;
- the same participants with a correct/effective patch;
- a similar patch name whose installed state is inapplicable;
- an authoritative claim whose version or installer condition is not met;
- a cosmetic/asset overwrite with no supported functional loss;
- the same display names but different causal identity;
- unchanged dependencies under an unrelated profile edit;
- a targeted run lacking one required carried population; and
- an old result whose age increased but whose declared validity dependencies
  and freshness policy remain satisfied.

### 6.5 Taxonomy stratification

Evaluation manifests use exact taxonomy version `0.1.0` and report coverage
across:

- declared purpose/intended target;
- observed technical surface and delivery;
- affected area;
- consequence;
- each effect-extent facet;
- single-area versus cross-cutting cases;
- assigned, unknown, unsupported, unmapped, and not-applicable states; and
- declared, observed, predicted, and established roles.

This is multi-label stratification, not a requirement to populate every point
in an enormous Cartesian product. Each analyzer declares which strata are
eligible, supported, excluded, and unevaluated. Sparse critical strata remain
visible rather than being masked by a large easy stratum.

## 7. Ground truth and adjudication

Expected results are pre-registered from evidence independent of the path under
test:

- fixture construction and hand-audited structure;
- authoritative MO2 behavior for effective state;
- independently specified Bethesda binary/semantic expectations;
- applicable author or curated documentation for intent/instructions;
- exact qualified tool inputs/results where the tool boundary is exercised;
- known-good patch comparison within its inspected claim boundary;
- targeted in-game observation only where static evidence cannot establish the
  result; and
- retained domain-expert adjudication with disagreement and uncertainty.

Each label records:

- the scored decision unit and expected outcome;
- supporting and contradicting evidence;
- applicability and version scope;
- adjudicator identity/role and review date;
- whether the answer was visible to the implementation, retrieval, model, or
  policy-calibration path;
- taxonomy assignments and confidence in the label;
- unresolved disagreement; and
- changes with independent evidence and revision history.

LLM-as-grader output may assist triage only after its own agreement and failure
behavior is evaluated. It cannot be the sole oracle for an LLM-backed
analyzer, source applicability, Skyrim semantics, or readiness.

## 8. Required per-analyzer measurements

Metrics are reported for one exact analyzer family, implementation revision,
identity contract, ruleset/prompt/schema/model configuration, declared scope,
and dataset revision.

### 8.1 Admission and semantic correctness

- true positives, false positives, false negatives, and true negatives;
- precision among emitted findings;
- recall among eligible expected findings;
- false discovery and miss rates;
- lead/finding admission correctness;
- abstention rate and abstention correctness;
- unsupported/unknown/gap classification correctness;
- severity and confidence classification agreement, reported separately;
- applicable taxonomy assignment accuracy/coverage by axis and role;
- citation, version, applicability, and provenance correctness;
- remediation/validation usefulness adjudication; and
- deterministic repeatability or LLM semantic consistency as applicable.

### 8.2 Candidate-stage behavior

- eligible planted-interaction recall;
- mandatory-lane recall independent of ranking perturbation;
- candidate volume per declared population;
- irrelevant-candidate rate;
- selection/join/rationale correctness;
- canonical-participant correctness;
- matched-negative routing;
- unsupported/gap population accounting; and
- latency, memory, tokens, and cost.

Candidate recall does not substitute for finding precision, and a low candidate
volume does not compensate for missed mandatory work.

### 8.3 Consequence-weighted error views

Report counts and rates separately for:

- Blocker/Major readiness-eligible findings;
- Moderate/Minor supported findings;
- Advisories;
- lead-only investigations;
- false-ready readiness decisions;
- false-block/action-required readiness decisions;
- hidden or de-emphasized results that the expected presentation policy would
  surface;
- invalid review-state/suppression carryover; and
- fabricated or misattributed authority/provenance.

A cost-weighted summary may help compare policy candidates, but it is an
additional view. It cannot replace the unweighted confusion counts and
critical-error inventory, and its weights require an explicit owner decision.

### 8.4 Case and continuity behavior

- case grouping precision and recall against shared-cause truth;
- false merge, false split, and ambiguous/unresolved rates;
- exact-continuation and analytical-revision correctness;
- review-state carryover precision;
- erroneous suppression carryover;
- `not-observed` versus `not-evaluated` correctness; and
- explanation/proof completeness.

### 8.5 Coverage behavior

For every declared denominator:

- eligible population;
- completed;
- completed with gaps;
- failed;
- skipped by configuration;
- skipped by limit;
- unsupported;
- unknown or still enumerating;
- false-complete and false-gap decisions; and
- applicable taxonomy strata without sufficient evaluation.

Micro-aggregates may show total volume. Macro views show analyzer, causal
family, profile, and taxonomy stratum so high-volume easy cases cannot hide a
sparse failing region.

## 9. Uncertainty and minimum-evidence sufficiency

### 9.1 Required reporting

Every proportion/rate report includes:

- numerator and denominator;
- exact scored unit;
- independent group/profile/family count;
- dataset partition and revision;
- point estimate;
- a predeclared interval method and confidence level;
- missing, disputed, and excluded labels;
- applicable prevalence in the sampled population;
- whether data were enriched for positives or matched negatives; and
- known dependence or distribution-shift limitations.

For unclustered binary outcomes, Wilson-style or exact binomial bounds are
reasonable starting methods. For grouped or repeated cases, use a predeclared
cluster-respecting method such as group/profile bootstrap or a hierarchical
model whose assumptions are reviewed. Report sensitivity to grouping when the
dataset is small.

### 9.2 No universal minimum count

The project should not set one arbitrary `n` for every analyzer. Before
sampling, the later calibration plan must specify:

- the maximum tolerable error or decision-relevant bound;
- the desired uncertainty width or one-sided bound;
- the minimum change worth detecting;
- statistical power/significance where a hypothesis test is appropriate;
- independent grouping unit;
- required critical taxonomy/interaction strata; and
- practical limits on acquiring trustworthy labels.

The required sample then follows from those choices and observed/assumed
prevalence, with conservative treatment of uncertainty. If the required sample
is impractical, the scope remains narrower, the maturity remains lower, or the
output stays non-blocking. The number is not reduced merely to make a release
gate pass.

### 9.3 Zero-event and sparse-stratum rules

- Zero observed false positives does not establish a zero false-positive rate;
  report its upper bound.
- A sparse or empty critical stratum is `insufficient evidence` or
  `unevaluated`, not perfect performance.
- Results may be pooled only under a predeclared, justified common mechanism;
  pooled and per-stratum views remain visible.
- Synthetic mutations can establish specific invariants and boundaries but do
  not by themselves establish real-mod prevalence or public generalization.
- Repeated LLM samples from the same item measure run variability, not
  independent domain breadth.

### 9.4 Non-negotiable invariant failures

Some failures block maturity/readiness eligibility regardless of an otherwise
strong aggregate rate until corrected and re-evaluated:

- fabricated local state, source passage, citation, or authority;
- a privileged or write-authority violation;
- a lead affecting readiness without promotion;
- raw analytical output mutated by presentation policy;
- an invalid dependency carryover represented as validated;
- a materially stale result presented as current;
- a targeted run borrowing unperformed full-policy coverage;
- unsupported or failed work represented as completed; or
- historical analytical/readiness output rewritten in place.

This is not a claim that one discovered defect permanently disqualifies an
analyzer. It requires correction, a new revision, regression coverage, and a
fresh acceptance run.

## 10. Maturity evidence model

Maturity is assigned to one exact analyzer revision and declared scope. It is
not inherited automatically by a rewritten analyzer, expanded scope, changed
identity contract, new record/asset family, new model/prompt/schema, or new
provider execution mode.

### Experimental

The accepted definition already covers research/development output with
insufficient evaluation. No performance threshold is needed to assign this
safe default. Experimental output:

- retains raw typed results;
- is visibly maturity-labeled;
- cannot affect readiness by itself; and
- remains discoverable through separate counts and filters.

### Preview candidate evidence package

The later policy should consider Preview only after:

- an accepted bounded analyzer contract and exact support/exclusion statement;
- independent positive, matched-negative, boundary, malformed, and unsupported
  case specifications;
- executed provenance, abstention, coverage, and anti-overfitting assertions;
- a development error taxonomy;
- at least one validation partition not used to write the implementation; and
- no unresolved non-negotiable invariant failure.

Exact quantitative gates remain unselected.

### Reliable candidate evidence package

The later policy should consider Reliable only after:

- the Preview evidence package;
- frozen analyzer and calibration-policy revisions;
- sufficient independent validation and held-out evidence to bound the
  decision-relevant error rates;
- controlled-real evidence within the claimed scope;
- materially different cases for any claimed generic mechanism;
- per-stratum precision, recall, abstention, provenance, and coverage review;
- validated change/carryover/staleness behavior where the analyzer claims it;
- operational behavior at the scale claimed by its milestone; and
- retained regression and residual-risk records.

### Trusted candidate evidence package

The later policy should consider Trusted only after Reliable behavior is
sustained:

- across more than one independent evaluation cycle;
- across materially independent real/profile/source distributions applicable
  to the scope;
- across relevant analyzer/model/tool version or time drift, or with explicit
  evidence that those identities are pinned;
- with field/adjudication feedback and incidents incorporated;
- without unexplained degradation in critical strata; and
- with a defined monitoring, demotion, and requalification process.

These packages define evidence kinds, not pass numbers. Wave F should not decide
whether M3 blockers require Reliable or Trusted, nor whether some bounded
Preview output can be non-blocking findings, until baseline distributions and
owner risk tolerances are reviewed.

## 11. Readiness-policy calibration procedure

For each milestone and policy version:

1. **Declare decisions.** List which analyzer outputs, maturity levels,
   evidence/confidence states, severities, dispositions, failures, and coverage
   populations may change each categorical readiness state.
2. **Declare error costs.** Distinguish false-ready, false-block,
   review-recommended inflation, false-incomplete, and stale/current errors.
3. **Freeze dataset partitions.** Record groups, taxonomy strata, expected
   deployment relevance, and holdout isolation.
4. **Generate raw runs.** Store all typed analyzer output before maturity,
   filtering, or readiness projection.
5. **Perform development error analysis.** Correct analyzer defects without
   treating development performance as release evidence.
6. **Calibrate candidates on validation data.** Compare candidate maturity and
   filtering/readiness policies, including uncertainty and review burden.
7. **Select and freeze one candidate.** Record the exact analyzer revisions,
   policy rules, threshold values, dataset revision, and rationale.
8. **Execute held-out acceptance once.** No threshold is changed after reveal
   to rescue the same acceptance attempt.
9. **Review slices and invariants.** Passing an aggregate metric does not
   override a critical-stratum or non-negotiable failure.
10. **Record the decision.** Accept, reject, narrow scope, retain a lower
    maturity, or gather additional evidence.
11. **Replace consumed holdouts.** Revealed cases become available for
    regression/development, while materially independent future holdouts are
    registered.
12. **Monitor and re-evaluate.** Incidents, user adjudications, dependency
    changes, and new distributions can demote maturity or stale the policy's
    evidence basis.

### M3 evidence emphasis

M3 is personal trust for the creator. Its policy may be calibrated to the
creator's intended workflow and risk tolerance, but it still requires:

- synthetic and controlled-real correctness;
- the accepted materially different proof requirement;
- creator-profile and upper-bound scale execution;
- all required M3 coverage populations and gaps;
- no use of the creator profile as the sole correctness oracle; and
- explicit residual risks and unsupported taxonomy regions.

### M4 evidence emphasis

M4 is public-facing and cannot merely reuse an M3 threshold label. Before its
policy is accepted it needs:

- materially broader profile shapes and mod-use patterns;
- independent user/domain review beyond the creator where feasible;
- public onboarding/default-filter comprehension and review-burden evidence;
- supported-environment, recovery, migration, and supportability evidence;
- field feedback and incident/demotion handling; and
- renewed confidence bounds for the population and scope actually claimed.

M4 may retain, tighten, or narrow M3 rules based on evidence. It must not lower
evidence sufficiency merely because broader use makes data collection harder.

## 12. User-facing filtering calibration

Filtering is a versioned presentation projection over immutable raw output. It
must not:

- delete or mutate candidates, hypotheses, findings, cases, evidence, or gaps;
- change finding admission;
- make an experimental output readiness-blocking;
- hide a material coverage/failure qualifier from readiness;
- convert suppression into resolution; or
- treat a lower queue position as lower severity or confidence.

Candidate policies should be compared using:

- default-queue recall for expected user-relevant supported findings;
- false escalation into prominent/action queues;
- critical supported findings hidden from the default route;
- correct separation and discoverability of leads and experimental output;
- time to first consequential finding;
- total items and estimated/observed review time;
- user adjudication of relevance and proposed action usefulness;
- filter comprehension and ability to recover non-default items;
- false-positive decisions by maturity/analyzer/severity;
- accepted-risk, resolved-unverified, advisory, and suppressed-state
  presentation correctness; and
- result stability under irrelevant reorder/rename/addition.

The calibration corpus should include full profile queues, not only isolated
cards, because review burden and prioritization are population effects. User
studies may remain small at M3 but must disclose their participant count and
creator bias. M4 needs broader usability evidence.

No default filter should be optimized solely for click reduction. A policy with
fewer items but missed consequential findings is not superior merely because
it is quieter.

## 13. Staleness calibration

`Results stale` is fundamentally dependency- and policy-based:

- physical snapshot changes;
- analysis-context changes;
- analyzer/tool/model/prompt/schema or semantic-policy changes;
- source revision/freshness invalidation;
- loss of retained dependencies required by the selected validity policy; or
- newer applicable evidence that prevents an older broad result from
  remaining the current apparent result.

Elapsed age may participate only through a source-specific accepted freshness
policy. There is no universal number of days after which all results become
stale.

Evaluation must use dependency mutation matrices:

- one relevant dependency changes;
- one unrelated dependency changes;
- several interacting dependencies change;
- a dependency becomes inaccessible/deleted;
- source age crosses or does not cross its declared freshness rule;
- analyzer semantics change compatibly or incompatibly;
- new applicable evidence arrives without altering the old historical run; and
- current state returns to an earlier byte-equivalent state with distinct
  historical provenance.

Measure:

- missed-material-staleness rate;
- false-stale rate;
- affected-closure precision and recall;
- explanation/dependency-proof completeness;
- historical/current presentation correctness; and
- current-readiness selection correctness.

A missed material dependency or unexplained current-state blend is a
correctness failure, not an error that a broad aggregate can average away.

## 14. Targeted-run and carryover calibration

A targeted run may replace a broader preflight result only when:

1. its newly executed scope;
2. every validated carried artifact and coverage record;
3. its exact policy-required populations; and
4. all applicable dependencies, gaps, failures, and dispositions

together satisfy the full selected readiness policy. This is a set/closure
proof, not a similarity score or minimum percentage.

Required scenarios:

- exact unchanged dependency closure with valid carryover;
- unrelated physical/context change;
- relevant winner/source/analyzer/context change;
- incomplete or unknown dependency declaration;
- deleted proof;
- compatible and incompatible analyzer revisions;
- changed finding/case causal identity;
- a targeted fix verification with all other broad coverage valid;
- a targeted run with one missing required population;
- cancelled/limited targeted work;
- newer applicable finding that stales the older broad current view; and
- a user request to reuse without typed evidence.

Measure:

- valid carryover acceptance;
- invalid carryover acceptance;
- safe carryover rejection/unnecessary recomputation;
- reuse-edge provenance completeness;
- full-policy closure correctness;
- targeted versus scope-limited/no-readiness presentation correctness;
- disposition/suppression carryover precision; and
- recomputation time/cost saved separately from correctness.

Efficiency may choose among equally valid plans. It may not relax dependency
proof. If impact is unknown, the accepted result is recompute, skip with an
explicit gap, or request dependency-evaluable typed input.

## 15. Threshold-change and maturity governance

Every adopted threshold or rule belongs to a versioned readiness/presentation
policy and records:

- policy ID/version and target milestone/use;
- exact analyzer families, revisions, scopes, and compatibility declarations;
- evidence and maturity eligibility;
- numerical thresholds where later selected;
- material-coverage rules and state precedence;
- dataset/partition/manifest revisions;
- calibration and held-out result identities;
- uncertainty method and bounds;
- owner/reviewer, rationale, residual risks, and acceptance date;
- superseded policy and migration/current-view behavior; and
- monitoring, demotion, and requalification triggers.

A policy change:

- creates a new readiness evaluation over an applicable retained run;
- never mutates raw analytical output or an older readiness evaluation;
- requires a new validation comparison and held-out evidence when it expands
  authority, scope, or default suppression;
- does not silently grandfather a maturity assignment across incompatible
  analyzer changes; and
- may demote an analyzer or narrow readiness eligibility when new evidence
  reveals risk.

Emergency demotion may immediately prevent new readiness-blocking use after a
documented incident. It still creates a new policy/maturity record, preserves
historical evaluations, exposes why the state changed, and schedules
requalification rather than silently editing the old threshold.

Recalibration triggers include:

- analyzer, ruleset, identity-contract, prompt, schema, model, provider mode,
  parser, tool, or taxonomy change affecting semantics;
- a newly observed non-negotiable failure;
- material false-positive/false-negative or false-ready drift;
- deployment/population shift;
- changed source/freshness policy;
- expanded supported surface or taxonomy stratum;
- repeated holdout use or contamination; and
- M3-to-M4 claim expansion.

## 16. Exact follow-up evaluation artifacts

Wave F should propose and the owner should review the following artifacts
before M1 implementation. Paths may be adjusted by the Wave F integration
owner if the repository adopts another consistent layout, but their contents
must remain explicit.

### M1 foundations that collect later calibration data

1. `docs/evaluation/specifications/m1-semantic-and-ground-truth.md`
   - final specifications for EVAL-0001, EVAL-0002, EVAL-0016, EVAL-0017,
     EVAL-0030, EVAL-0032, EVAL-0065, EVAL-0067, EVAL-0084, EVAL-0085, and
     EVAL-0086 as applicable;
   - raw-output retention and matched-negative assertions;
   - no M1 maturity/readiness threshold claim.
2. `docs/evaluation/fixtures/m1-semantic-fixture-manifests.md`
   - partition/group/taxonomy/ground-truth metadata;
   - answer-isolation and matched-negative links.
3. `docs/evaluation/specifications/m1-platform-and-operational.md`
   - EVAL-0026, EVAL-0027, EVAL-0047, EVAL-0069, EVAL-0078, EVAL-0079,
     EVAL-0082, EVAL-0087, and related run/persistence assertions where
     scheduled for M1;
   - explicit no-borrowed-coverage and immutable-policy projection behavior.
4. `docs/evaluation/fixtures/m1-platform-fixture-manifests.md`
   - dependency-mutation and cross-run continuity/carryover scenario manifests.

The Wave F integrator should decide which readiness/carryover cases are M1
gates versus accepted later specifications. M1 must at least preserve the raw
data and policy boundaries needed for later calibration; it need not claim M3
readiness.

### M3 calibration artifacts to create before M3 acceptance

5. `docs/evaluation/readiness-calibration-protocol.md`
   - accepted revision of this method, including exact interval/grouping rules
     and decision owners.
6. `docs/evaluation/manifests/m3-readiness-calibration-corpus.md`
   - immutable case/group/partition/taxonomy/profile manifest and partition
     transition history.
7. `docs/evaluation/results/m3-analyzer-maturity-report.md`
   - per-analyzer confusion counts, uncertainty, strata, provenance,
     abstention, coverage, and maturity recommendation.
8. `docs/evaluation/results/m3-readiness-policy-calibration.md`
   - compared policy candidates, error-cost rationale, default-filter burden,
     stale/carryover behavior, chosen frozen policy, and rejected alternatives.
9. `docs/evaluation/policies/m3-readiness-policy.md`
   - exact versioned policy selected after calibration.
10. `docs/evaluation/results/m3-readiness-held-out-acceptance.md`
    - one-shot held-out results, invariant review, residual gaps, disposition,
      and consumed/replacement holdout record.
11. `docs/evaluation/results/m3-readiness-residual-risk-register.md`
    - unsupported analyzers/taxonomy regions, insufficient strata, known
      failure modes, monitoring, and demotion triggers.

### M4 calibration artifacts to create before public release

12. `docs/evaluation/manifests/m4-readiness-calibration-corpus.md`
    - broader independent profile/user/environment distribution.
13. `docs/evaluation/results/m4-analyzer-maturity-and-field-report.md`
    - repeated-cycle and field/adjudication evidence.
14. `docs/evaluation/results/m4-filtering-and-usability-report.md`
    - public default-filter comprehension, review burden, recovery, and
      accessibility evidence.
15. `docs/evaluation/policies/m4-readiness-policy.md`
    - separately accepted public policy.
16. `docs/evaluation/results/m4-readiness-held-out-acceptance.md`
    - public-scope held-out acceptance and residual-risk review.

Every result artifact must have a machine-readable companion or embedded
versioned table sufficient to reproduce counts from retained case-result IDs.
Paths under `results/` are future outputs, not files to create during M0.

## 17. Existing evaluation-case refinements enabled

No new catalog ID is required merely to record this plan. Wave F should refine
the following existing cases when it writes their final specifications:

| Case | Required refinement |
|---|---|
| EVAL-0027 | Add the dependency-closure matrix, false-stale versus missed-stale outcomes, and the full-policy set proof for targeted replacement |
| EVAL-0030 | Add per-analyzer maturity evidence packages, raw-output invariance, categorical confidence reliability, and held-out threshold-selection isolation |
| EVAL-0036 | Assert discoverability/counting of leads under default filters without readiness effect |
| EVAL-0047 | Add erroneous-suppression-carryover measurement and changed-result default visibility |
| EVAL-0048 | Preserve advisory visibility and user-action-required semantics under every filter candidate |
| EVAL-0050 | Keep resolved-unverified state visible under default filtering |
| EVAL-0065 | Require exact analyzer revision/scope, maturity evidence links, thresholds, and exclusions |
| EVAL-0066 | Derive presets only after retained time/cost/coverage and quality calibration; do not treat a depth preset as a maturity policy |
| EVAL-0069 | Add immutable policy-version comparison, disposition-set resolution, maturity eligibility, and one-shot held-out acceptance assertions |
| EVAL-0078 | Add closure precision/recall and explicit unknown-impact behavior |
| EVAL-0079 | Report reconciliation and review-state carryover precision separately from identity candidate recall |
| EVAL-0082 | Assert that development output is unaffected by maturity/filter policy |
| EVAL-0085 | Add material-gap policy cases, no aggregate-coverage compensation, and default-filter visibility of readiness qualifiers |

If final specification work finds that these cases become unreviewably broad,
the Wave F integrator should allocate dedicated case IDs for analyzer-maturity
calibration, stale/current classification, full-policy targeted carryover, and
default-filter review burden. It should not hide several independently failing
constructs behind one pass/fail result.

## 18. Alternatives considered

### Set intuitive numerical thresholds during M0

Rejected. There is no analyzer distribution, representative corpus, error
prevalence, or measured review burden. Numbers would create false confidence
and encourage tuning to arbitrary gates.

### Use one global precision/recall or F-score

Rejected. It hides error consequence, analyzer scope, abstention, taxonomy
gaps, case grouping, provenance, stale/carryover correctness, and
false-ready/false-block asymmetry.

### Use severity and LLM confidence as the readiness score

Rejected. Severity and confidence are independent product dimensions, and LLM
self-confidence is not a calibrated probability. This would also bypass
evidence admission and analyzer maturity.

### Let the owner approve reuse or readiness despite missing proof

Rejected as validated carryover. The user may accept a disclosed risk or start
a run with a changed configuration, but a bare confirmation cannot manufacture
dependency equivalence or full coverage.

### Calibrate on all available cases, then report the same data

Rejected. It has no independent acceptance estimate and would violate the
accepted fixture partition/anti-overfitting policy.

### Set maturity once for an analyzer family

Rejected. Maturity is scoped to exact semantics, inputs, versions, and claimed
coverage. Compatible revision carryover requires evidence; expansion or drift
requires requalification.

### Use only synthetic cases

Rejected for M3/M4 trust. Synthetic cases are essential for precise ground
truth and boundary coverage but do not establish real-mod/profile
generalization or public deployment relevance.

### Use only real profiles and user false-positive labels

Rejected. Real lists have incomplete ground truth and unknown missed problems.
User dispositions are applicability-scoped review evidence, not universal
labels. Synthetic planted positives and matched negatives remain necessary.

### Make every uncertain output visible at equal priority

Rejected as the user-facing default because it can make a high-scale diagnostic
queue unusable. Raw output remains retained and discoverable; empirical
filtering/prioritization is appropriate after evidence exists.

### Hide experimental output entirely

Rejected. It prevents inspection, error analysis, coverage disclosure, and
development learning. Experimental output remains non-blocking and distinctly
labeled.

## 19. Contrary evidence, uncertainty, and limitations

- The prevalence of actual consequential interactions in real modlists is
  unknown. Precision observed in an artificially balanced fixture corpus will
  not directly predict user queue precision; reports must retain the sampled
  prevalence and later profile-level evidence.
- Some serious failures may be too rare for narrow confidence bounds. The
  appropriate response is conservative scope/maturity and targeted adversarial
  evidence, not a claim of zero risk.
- Real-mod ground truth may remain partially unknowable without in-game
  observation. Expected abstention or bounded prediction may be the correct
  result.
- Taxonomy `0.1.0` intentionally has thin evidence in several areas. Aggregate
  analyzer results must not imply coverage there.
- One creator cannot provide independent broad-user usability evidence.
  M3 may disclose that limitation; M4 must add broader evidence.
- Model/provider updates can change LLM-backed behavior even when the adapter
  contract is stable. Exact model identity and repeated evaluation are
  required; inability to pin a model limits maturity claims.
- Statistical intervals do not correct mislabeled ground truth, partition
  leakage, invalid grouping assumptions, or a harness that fails to represent
  the product path.
- A user-facing false positive is context-sensitive: a supported finding can
  be locally inapplicable without the analyzer being universally wrong.
  Evaluation labels must retain this scope.
- Full M4 thresholds may need post-M3 field evidence and therefore cannot be
  completed during M0 or M1.

## 20. Recommendation

Confidence: **High** for the protocol boundary; **intentionally unassigned** for
future numerical thresholds.

Accept this evidence-collection and calibration approach for RQ-028:

1. M0/Wave F defines and accepts the protocol and exact M1 data-retention/case
   requirements.
2. M1 emits complete raw analyzer/policy evaluation artifacts but does not
   claim M3/M4 maturity.
3. M2 evaluates policy presentation mechanics without treating UX proof as
   analyzer trust.
4. M3 sets its first numerical thresholds only after analyzer-specific
   validation data exists, freezes a versioned policy, and passes a held-out
   acceptance review plus creator/upper-scale evidence.
5. M4 performs a separate broader calibration and held-out acceptance rather
   than inheriting M3's personal-use evidence unchanged.
6. Any unavailable evidence or overly wide uncertainty narrows scope, lowers
   maturity, or keeps output non-blocking; it never becomes assumed safety.

Preconditions:

- accepted analyzer contracts and case specifications;
- independent ground truth and matched negatives;
- partition/group manifests;
- immutable raw results and exact run/configuration/model/tool identities;
- reviewed uncertainty and error-cost choices; and
- owner acceptance of each eventual readiness-policy version.

## 21. Decision and follow-up enabled

This investigation enables:

- acceptance of an M0 RQ-028 calibration/evidence-collection plan;
- Wave F refinement of EVAL-0027, EVAL-0030, EVAL-0036, EVAL-0047,
  EVAL-0048, EVAL-0050, EVAL-0065, EVAL-0066, EVAL-0069, EVAL-0078,
  EVAL-0079, EVAL-0082, and EVAL-0085;
- explicit M1 raw-output and partition metadata requirements;
- a later M3 readiness-policy/calibration owner decision after evidence exists;
- a separate later M4 public-policy decision; and
- an explicit residual-risk/unsupported-capability register.

It does **not** enable an architecture ADR. The readiness policy is a versioned
product review/presentation input already represented by the accepted domain
model. If later calibration would change accepted product meanings—rather than
choose values within them—the affected product document must be amended through
its change discipline. An ADR is warranted only if a new technical mechanism
or authority boundary is proposed.

## 22. Suggested RQ-028 status

Owner disposition: accepted on 2026-07-28. The RQ registry now records:

> **Calibration plan accepted for M0; numerical M3/M4 analyzer-maturity,
> evidence, error, material-coverage, stale-result, targeted-carryover, and
> filtering thresholds remain Later evidence and require versioned
> analyzer-specific calibration plus held-out acceptance.**

This closes the M0 planning obligation without falsely resolving the later
empirical threshold decision.

## 23. Requirements-and-evidence traceability

| Requirement/decision | Evidence in this report | Proposed later proof |
|---|---|---|
| PROD-004 | Categorical/no-safety semantics and false-ready treatment | EVAL-0085 and held-out readiness scenarios |
| SCAN-010 | Measured preset/filter inputs; no intuitive preset | EVAL-0066 plus retained time/cost/coverage/quality data |
| SNAP-003 | Dependency/policy-based staleness matrix | EVAL-0027 and EVAL-0078 refinements |
| SNAP-004 | Full dependency-closure carryover gate | EVAL-0027, EVAL-0078, and EVAL-0079 |
| EVID-006 | Abstention correctness and insufficient-evidence handling | Per-analyzer positive/negative/boundary/unsupported corpus |
| EVID-007 | Raw output precedes maturity and filtering | EVAL-0082 and common execution-envelope assertions |
| FIND-001 | Independent severity/confidence/maturity/taxonomy metrics | EVAL-0030 and EVAL-0086 |
| FIND-007/FIND-008 | Categorical readiness and accepted-risk policy projection | EVAL-0069 |
| FIND-009 | Analyzer-specific maturity packages and readiness eligibility | EVAL-0030, EVAL-0065, later maturity report |
| FIND-011 | Lead admission/count/filter separation | EVAL-0036 |
| FIND-012 | Run/policy/disposition/time binding and targeted scope | EVAL-0027 and EVAL-0069 |
| FIND-013 | Advisory non-blocking default | EVAL-0048 |
| FIND-014/ADR-0022 | Continuity and review-state carryover accuracy | EVAL-0079 |
| COVER-001/COVER-002 | Per-population states and taxonomy strata | EVAL-0085 and corpus manifest |
| COVER-003 | Material-gap policy calibration | EVAL-0027, EVAL-0069, EVAL-0085 |
| ANALYSIS-016 | Exact analyzer revision/scope/evidence/maturity contract | EVAL-0065 |
| OPS-004 | M3 creator and upper-bound scale evidence | EVAL-0018 or accepted successor |
| ADR-0001 | No fabricated authority; schema/evidence admission invariant | Per-analyzer provenance/citation cases and invariant review |
| ADR-0002 | Immutable run/readiness-policy evaluations | EVAL-0069 and retained policy comparison |
| ADR-0010 | Complete dependency-validity proof | EVAL-0027/EVAL-0078 mutation matrices |
| Anti-overfitting rules | Grouped partitions, matched negatives, holdout replacement | Corpus manifest and partition transition history |

## 24. Self-review

The completed investigation was re-read against RQ-028 and the M0 Wave F
scope.

Semantic checks performed:

- no numerical threshold was invented;
- accepted product semantics are distinguished from later evidence;
- severity, confidence, evidence, maturity, taxonomy, coverage, and readiness
  remain separate;
- matched negatives and anti-overfitting requirements are explicit;
- targeted carryover and staleness remain dependency-based rather than
  score-based;
- M3 and M4 evidence scopes remain distinct;
- the creator profile is not treated as a correctness oracle;
- existing cases are refined without silently marking them accepted or passed;
- no architecture or product amendment is implied; and
- limitations and sparse-data behavior remain visible.

No production code, evaluation fixture, registry, product document, ADR,
source registry, or milestone plan was modified by this investigation.
