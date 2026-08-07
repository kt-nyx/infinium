# Severity, confidence, maturity, coverage, and readiness

Status: Accepted  
Last reviewed: 2026-08-05

This document separates concepts that must not be collapsed into one score.
That separation is normative for product design. Category values for declared
purpose and intended target, technical modification surface, affected area,
consequence type, and effect extent are governed by the accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md), while
user-facing/release thresholds still require evaluation data.

## Taxonomy-bound classification axes

Infinium classifies an interaction through five independent axes:

- the **declared purpose kind and intended target** supported by author
  documentation or other applicable intent evidence;
- the **technical modification surface** through which a mod changes effective
  state, such as records, assets, scripts, configuration, native components,
  or generated output;
- the **affected game system or content area**, such as progression, actors,
  world content, combat, interface, or presentation;
- the **consequence type**, describing what could go wrong;
- the **effect extent**, split into subject, spatial, persistence, and causal
  propagation facets.

The accepted taxonomy is versioned, multi-label, and open to evidence-backed
extension. Hosting-site categories, record types, and file formats must not be
treated automatically as authoritative mod intent, player-visible game areas,
or consequences.

These versioned classifications will be used for:

- analyzer scope, exclusions, capability declarations, and coverage gaps;
- candidate generation, interaction routing, investigation breadth, and review
  prioritization;
- finding classification, case presentation, filtering, search, and focused
  mod views;
- scan summaries, coverage matrices, and readiness-gap disclosure;
- impact, symptom, remediation, validation, and change-impact explanations;
- evaluation-corpus stratification, generalization checks, analyzer maturity,
  and roadmap decisions.

## Consequence type

Consequence type describes what kind of consequence may occur, not
which record type was edited, which game system was touched, how severe the
result is, or how broadly it manifests. The normative values are the
`consequence.*` codes in the accepted
[taxonomy](mod-impact-taxonomy.md). Severity, faceted effect extent,
confidence, symptoms, and user intent remain separate dimensions.

## Severity

Severity estimates the worst credible consequence if the finding manifests:

- **Blocker:** Starting or continuing a safe/useful test or playthrough is not
  reasonable until reviewed.
- **Major:** Likely to break progression, global behavior, substantial content,
  stability, or an important user requirement.
- **Moderate:** Meaningful but bounded functional loss or inconsistency.
- **Minor:** Localized low-impact defect or redundancy.
- **Advisory:** Maintenance, reproducibility, or best-practice information that
  is not established breakage.

Severity does not decrease merely because confidence is low.

## Confidence

- **Confirmed:** Direct local state or an applicable authoritative rule proves
  the conclusion within supported scope.
- **Strongly supported:** Several independent evidence items support a concrete
  mechanism with no material contradiction.
- **Plausible:** Specific local evidence supports the hypothesis, but intent,
  applicability, or runtime effect remains unresolved.
- **Speculative lead:** Worth investigation but not an established finding.

Candidates and hypotheses may be speculative. Promotion to a finding requires
at least plausible support plus the originating analyzer's declared evidence
threshold. Speculative leads remain separately inspectable and are excluded
from finding counts.

## Analyzer maturity

Maturity describes evaluated analyzer reliability, not finding confidence:

- **Experimental:** Research/development output with insufficient evaluation.
- **Preview:** Bounded supported scope with meaningful tests but known gaps.
- **Reliable:** Meets defined precision, recall, provenance, and failure
  standards for its declared scope.
- **Trusted:** Demonstrates sustained reliability on synthetic and real cases
  beyond the Reliable threshold.

The versioned readiness policy defines which maturity levels are eligible to
influence readiness; research and evaluation must establish that threshold
rather than the label definitions deciding it in advance.

Development/evaluation mode preserves all raw output without using maturity to
hide or down-rank analyzer contributions. User-facing/release presentation uses
maturity only after raw output is stored. Maturity may control routing and
readiness eligibility, but it does not relabel a finding as a lead or a lead as
a finding; that boundary is governed by evidence and the declared finding
threshold.

## Review priority

Review priority is a presentation/routing value derived from, but never
replacing:

- severity;
- confidence;
- faceted effect extent, including causal propagation;
- affected user intent;
- reversibility;
- analyzer maturity;
- cost of validation.

The underlying dimensions remain visible and independently filterable.

Before promotion, a lead-only investigation may use explicitly predicted impact
for routing but does not receive a finding severity as though its hypothesis
were established.

## Coverage

Coverage is multidimensional. It must be reported by meaningful denominator,
such as:

- enabled plugins parsed;
- effective file providers indexed;
- supported record families analyzed;
- enabled mods with resolved identity;
- enabled mods with current documentation;
- native components version-verified;
- configured generators with a named analyzer;
- analyzers completed, failed, skipped, limited, or unsupported.

These are operational denominator examples, not a complete list of mod types
or affected areas. Coverage reporting must map applicable denominators and
unevaluated areas to the accepted taxonomy version while retaining raw
population definitions.

Infinium must not combine unlike dimensions into a single "93% analyzed" or
"safety coverage" number. It may show several labeled percentages/counts and a
coverage matrix.

For the bounded M1 Bethesda contract, backend, persistence, export, and test
surfaces retain the complete fixed registry defined by ADR-0028: plugins, NPC
records, race records, placed-reference records, unsupported records, FaceGen
loose assets, FaceGen archive assets, localized strings, automatic environment
discovery, and taxonomy subjects. Rows with a zero denominator are retained and
completed. A user-facing summary may omit those zero rows for clarity only when
the complete detail remains accessible.

Coverage follows the layered-evidence rule in
[ADR-0029](../architecture/decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md).
An item may contribute independently established structural or observed facts
while remaining incomplete for a decoded, resolved, or semantic population.
Retaining those lower-layer facts must not increment a higher-layer completed
count. The report identifies the exact population and missing capability rather
than discarding the whole item or presenting the unavailable layer as null,
absent, or complete.

The fixed `face-gen-loose-assets` row counts each applicable mesh and tint path
once. Unknown loose availability contributes `+1/+0` and owns the
`face-gen-loose-assets` / `exhaustive-byte-verified-loose-provider-index` gap
at snapshot and result scope; archive resolution is independent. Its lifecycle
is `unsupported` for a positive denominator with zero completion,
`completed_with_gaps` for positive partial completion, `completed` for exact
completion without the gap, and `completed` for `0/0`.

## Readiness

Readiness is categorical:

- **Not analyzed:** No retained reportable analysis result exists for the
  selected target.
- **Results stale:** A retained result exists, but the current installation
  state or selected analysis-affecting context differs materially from its
  installation snapshot/context, or its retained run inputs no longer satisfy
  the selected validity/freshness policy.
- **Analysis incomplete:** Required analyzers failed, were limited, or have
  material coverage gaps.
- **Action required:** One or more unresolved Blocker/Major findings meet the
  configured readiness evidence/analyzer-maturity threshold, or a finding was
  explicitly marked action-required by the user.
- **Review recommended:** No action-required finding, but unresolved supported
  readiness-relevant findings or readiness-policy review decisions remain.
- **Ready with accepted risks:** Blocking work is resolved and remaining
  supported risks were explicitly accepted.
- **No unresolved risks within analyzed coverage:** No unresolved supported
  readiness-relevant findings remain, with coverage and non-blocking advisories
  still shown explicitly.

Exactly one primary state is displayed. Initial precedence is: not analyzed,
results stale, action required, analysis incomplete, review recommended, ready
with accepted risks, then no unresolved risks within analyzed coverage.
Non-primary conditions remain visible as qualifiers; for example, an
action-required result still shows material analyzer failures rather than
hiding them. User-resolved findings that have not been revalidated remain
visibly labeled as unverified review assertions rather than analyzer-confirmed
fixes.

Readiness is calculated for one identified analysis run and its effective
scope, versioned readiness policy, and resolved applicable disposition set
(including unreviewed/default state) as of a recorded evaluation time. A later
review-state or readiness-policy change creates a new evaluation over that run
rather than rewriting a retained/exported earlier evaluation or changing
semantic analysis context.
While the run is active, cancelled, limit-reached, or otherwise partial, any
derived readiness is explicitly provisional or incomplete. Starting a run does
not erase the prior applicable run's result, and partial work never inherits
coverage from it.

A targeted analyzer, verification, case-follow-up, or symptom-investigation run
does not replace broader preflight readiness unless its declared scope and
validated carryover satisfy the selected full readiness policy. Otherwise it
shows only scope-limited/provisional status, or no readiness result, while the
prior broader result remains separately visible and may become stale if its
dependencies changed. Newer applicable evidence/findings can therefore prevent
the old result from remaining the current apparent readiness without
retroactively changing what that run reported.

In-game validation adds evidence and confidence but is not the primary readiness
gate. No readiness state guarantees runtime stability.

Advisory findings remain visible and countable but do not affect readiness by
default because they are not established breakage. An advisory explicitly
marked action-required by the user does affect readiness.

## Open calibration work

Research and evaluation must still define:

- calibration and evaluation coverage for the accepted technical-surface,
  affected-area, consequence, and effect-extent codes;
- exact evidence thresholds by analyzer;
- which maturity levels can block readiness at M3 and M4;
- how material coverage gaps affect readiness;
- when a plausible high-impact hypothesis is eligible for finding promotion
  through additional typed corroborating or confirming evidence and, once
  promoted, for automatic action-required readiness;
- user-facing/release defaults and filtering.
