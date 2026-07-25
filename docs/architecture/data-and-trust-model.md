# Data and trust model

Status: Draft  
Last reviewed: 2026-07-24

## Principle

Infinium must be able to explain where every conclusion came from and which
system had authority to assert it.

## Evidence classes and authority

- **Snapshot-bound local observations**
   - file contents and providers;
   - enabled state and order;
   - parsed records and winners;
   - runtime and configuration.
- **Deterministic derived evidence**
   - missing dependency;
   - override chain;
   - reference resolution;
   - tool output, including diagnostics from an invoked LOOT configuration;
   - content/version relationship.
- **Authoritative external claims**
   - applicable mod-author instructions;
   - curated LOOT masterlist/prelude metadata;
   - official technical documentation.
- **Corroborated community evidence**
- **Uncorroborated reports**
- **Heuristic or LLM inference**

These are not one total ranking. Local/deterministic evidence is authoritative
for installed and effective state. Applicable author/curated claims are
authoritative for stated intent, instructions, and documented constraints.
Neither silently rewrites the other; applicability and contradiction remain
explicit. Community evidence and inference are weaker within the claim types
they address.

Retained stored observations are immutable within their installation snapshot;
the live filesystem they describe is not presumed immutable.

LOOT userlist entries remain user-supplied local configuration even when LOOT
consumes them. They do not inherit the authority of curated LOOT metadata.

## Required separation

The storage and contracts must not collapse these into one generic issue:

- observation;
- external claim;
- candidate;
- hypothesis;
- finding;
- recommendation;
- coverage gap.

Each transition records the applicable analyzer, ruleset, tool, prompt, model,
and evidence identities, including explicit absence of LLM involvement.

Reusable external claims retain their evidence-acquisition-run provenance.
Consuming analysis runs add application links; they do not rewrite the claims
as locally observed or profile-owned evidence.

## LLM use

An LLM receives a bounded evidence package appropriate to its task. It may emit
only a schema-constrained result containing:

- proposed claims or hypotheses;
- cited inputs;
- supporting and contradicting evidence;
- missing information;
- confidence;
- impact/symptom interpretation;
- suggested remediation or validation.

The system validates identifiers, citations, schema, applicability, and
provenance before results become stored claims or findings.

Untrusted mod-page text, comments, logs, and local documentation are data. They
cannot grant tools, change authority, or instruct the agent to ignore product
rules.

## Finding threshold

Semantic analysis applies the versioned evidence thresholds in its analysis
context before user-facing/release presentation:

- promotion from hypothesis to finding requires at least plausible support plus
  the analyzer's declared evidence threshold;
- severity describes consequence independently of confidence;
- automatic action-required/readiness effects for Blocker or Major findings
  require the configured evidence and analyzer-maturity threshold;
- lower-confidence high-impact findings remain visible for review rather than
  being down-severitized or silently promoted to readiness blockers;
- inferential risks must cite specific local observations;
- speculative leads remain separate;
- lead-only investigation cases remain separate from supported-case counts and
  cannot affect readiness;
- advisories explain a concrete reason to care and remain non-blocking by
  default unless the user marks one action-required.

Development evaluation retains all raw output and typed classifications before
presentation/readiness policy. That later policy may route or gate a result but
does not change whether the analytical result is a hypothesis/lead or finding.

## Conflicting evidence

Conflict resolution considers:

- local applicability;
- versions;
- dates/revisions;
- authority;
- specificity;
- explicit supersession;
- direct contradictory observations.

If no justified resolution exists, the system records uncertainty rather than
choosing silently.

## Readiness

Readiness is a view over:

- one identified analysis run;
- unresolved readiness-relevant findings;
- accepted risks;
- coverage;
- failures;
- unsupported scope;
- installation-snapshot and analysis-context applicability;
- effective scan-configuration scope;
- resolved-run-input validity and freshness;
- readiness-policy version, resolved applicable disposition set (including
  unreviewed/default state), and evaluation time.

The readiness policy is a versioned review/presentation input, not analyzer
output or semantic analysis context. Changing it creates a new evaluation over
the same applicable run rather than changing that run.

It is not stored or presented as an objective probability of stability.

Normative severity, confidence, maturity, coverage, and readiness meanings live
in
[`../product/severity-confidence-and-coverage.md`](../product/severity-confidence-and-coverage.md).
