# Data and trust model

Status: Accepted
Disposition: synthesis; actively maintained
Last reviewed: 2026-08-10
## Principle

Infinium must be able to explain where every conclusion came from and which
system had authority to assert it.

Wave E accepts this durable model: SQLite plus coordinator-owned
content-addressed payload storage (ADR-0015), append-only finding/case
reconciliation and review-state carryover decisions (ADR-0022), and a
single-owned atomic cost ledger (ADR-0023). M1 Slice 2 implements and publicly
exercises the bounded authoritative-store, lifecycle, identity/lineage
substrate, and write-authority portions. The Slice 5 plan owns clean-break
contracts, migration, typed
evidence/provenance, candidate, finding, case, reconciliation, replay, and
corpus behavior; current delivery status lives in
[current project state](../current-state.md). Slice 6 owns the provider
cost-ledger boundary. Unimplemented portions and full evaluation conformance
remain pending rather than being inferred from the delivered substrate.

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
   - tool output, including direct diagnostics from a qualified read-only
     libloot operation over exact captured inputs;
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

Direct read-only libloot results, curated masterlist/prelude claims, private
userlist inputs, and Infinium-derived interpretations remain separately
provenanced. The accepted integration boundary does not make every libloot
result a curated external claim.

## Required separation

The storage and contracts must not collapse these into one generic issue:

- observation;
- external claim;
- candidate;
- hypothesis;
- finding;
- recommendation;
- coverage gap.

They also must not collapse a physical local installed entity into a Nexus or
other source identity. A local MO2 mod directory is the authoritative subject
for enabled state, priority, files, providers, and snapshot membership. Zero,
one, or several source mappings may describe where its content came from, and
one source artifact may contribute to several local entities. MO2 metadata,
retained installer archives, and installed files provide mapping evidence; no
single field is universal identity proof. Missing exact FOMOD choices or manual
installation history is an explicit gap rather than a reconstructed fact.

A taxonomy assignment is a versioned derived classification, not a new
evidence class or independent assertion of truth. It retains its subject,
axis/facet, applicability state, classification role, evidence, conditions,
confidence reference, and derivation provenance. Declared, observed,
predicted, and established roles remain distinct; taxonomy codes never replace
causal evidence, severity, confidence, or authority.

Each transition records the applicable analyzer, ruleset, tool, prompt, model,
and evidence identities, including explicit absence of LLM involvement.
For a provider operation, prompt provenance binds the exact UTF-8 instruction
text serialized into the canonical transmitted request and the SHA-256 of
those exact bytes. A prompt ID or fingerprint recorded elsewhere cannot stand
in for the transmitted text.

Reusable external claims retain their evidence-acquisition-run provenance.
Consuming analysis runs add application links; they do not rewrite the claims
as locally observed or profile-owned evidence.

## Layered derivation and partial success

Within one claim derivation, Infinium distinguishes structural, observed,
decoded, resolved, and semantic prerequisites. These layers do not replace the
claim-type-specific evidence classes above. They state what must already be
proven before a particular fact may be published.

Under [ADR-0029](decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md),
a later decode, resolution, or semantic failure does not erase an independently
true earlier structural or observed fact. The earlier fact remains available;
the unsupported later claim is null, unknown, omitted, or terminal only as its
own accepted contract specifies; and the exact affected coverage and missing
capability remain visible. Unavailable values are never guessed from product
behavior or treated as completed work.

Every fact-producing contract therefore declares its evidence prerequisites,
null/unknown/omission behavior, coverage effect, gap effect, and atomic-failure
boundary. Where a bounded input state space exists, those dispositions must be
total and mechanically checkable before examples are used to claim
qualification.

Permitted source bodies/excerpts are private acquisition artifacts rather than
claims or authority-bearing conclusions. They remain available through their
configured extraction, deterministic/LLM analysis, case/finding synthesis,
prose, provenance, and audit consumers. Later source-specific minimization or
deletion preserves explicit provenance and gap state; it never silently
upgrades the surviving derived claim or implies that its source is still
inspectable.

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

Validation is axis-specific. Faithful proposal/extraction, evidence support,
local applicability, and the host admission decision are distinct facts. An
unsupported proposition lacks sufficient support but is not thereby false; a
contradicted proposition has direct opposing evidence; and an abstained host
decision publishes no conclusion. Only supported and applicable propositions
can be admitted. See
[ADR-0034](decisions/ADR-0034-prompt-fidelity-and-semantic-admission-axes.md).

Untrusted mod-page text, comments, logs, and local documentation are data. They
cannot grant tools, change authority, or instruct the agent to ignore product
rules.

Provider-hosted web-search actions, URLs, source lists, inline citations, and
synthesized prose are discovery provenance and investigative leads. They are
not acquired source passages or authoritative external claims. Normal source
authority requires a separately approved host-controlled adapter to acquire
and fingerprint the landing content, establish source/author/revision identity,
and extract exact supporting passages. A model-selected source cannot grant
its own acquisition or authority.

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
