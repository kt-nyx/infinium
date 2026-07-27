# Glossary

Status: Draft  
Last reviewed: 2026-07-26

This glossary supplies short definitions. Normative semantics live in the
[domain model](product/domain-model.md) and accepted
[Skyrim SE mod-impact taxonomy](product/mod-impact-taxonomy.md).

- **Analyzer:** A bounded module that consumes declared evidence and emits
  typed observations, claims, candidates, hypotheses, findings,
  recommendations, or coverage gaps.
- **Affected game area:** Versioned, multi-label taxonomy classification of
  the game system or content area in which an effect may manifest, distinct
  from the technical surface modified and the resulting consequence.
- **Acquisition permission:** Source-specific decision about whether Infinium
  may retrieve or inspect material through a particular interface and purpose.
  It does not imply private retention, provider transmission, or external
  redistribution permission.
- **Analysis context:** Versioned non-physical inputs used alongside an
  installation snapshot that can affect semantic interpretation, including
  assumptions, analyzer/ruleset semantics, evidence policy, model/prompt
  settings, and finding thresholds.
- **Analysis run:** One execution bound to an installation snapshot, analysis
  context, effective scan configuration, and resolved input manifest.
- **Assumption:** Structured, profile-scoped knowledge about user intent or
  expected configuration; inferred/user-provided origin is separate from user
  confirmation state.
- **Auditability:** Ability to inspect the retained historical record,
  provenance, configuration, and known gaps even when the original execution
  cannot be replayed.
- **Authoritative external claim:** A version-applicable statement from a mod
  author, curated LOOT source, or another approved primary source within that
  source's authority.
- **Blast radius:** Informal name for the causal-propagation facet of effect
  extent. Normative records use `extent.propagation.*` assignments rather than
  one scalar blast-radius value.
- **Candidate:** An interaction selected for further investigation; not yet a
  finding.
- **Causal join:** A bounded relationship query that selects candidate
  interactions from specific shared records, paths, references, dependencies,
  claims, or other qualified mechanisms. Merely sharing a taxonomy label,
  location, or mod pair is not a causal join.
- **Case:** A group of findings, hypotheses, symptoms, and evidence attributed
  to a shared likely cause and usually a shared resolution. A supported case
  has at least one finding; a lead-only investigation case has none and is
  counted separately.
- **Case revision:** The immutable run-specific representation of a logical
  case's membership and conclusion, linked to earlier or later revisions.
- **Confidence:** Strength of support for a conclusion, independent of its
  potential severity.
- **Consequence type:** Versioned taxonomy classification of what kind of
  problem may result, kept separate from cause, affected area, technical
  surface, severity, confidence, symptoms, and effect extent.
- **Coverage:** Multidimensional account of what completed, failed, was skipped,
  was limited, or is unsupported, reported against labeled populations.
- **Coverage gap:** Something the tool could not inspect or classify.
- **Declared mod purpose:** Source-supported description of what a mod is
  intended to add, remove, replace, or alter. It is distinct from a hosting-site
  category, the technical surfaces actually modified, and unintended or
  predicted affected game areas. Normative values use the accepted
  `purpose.*` and `purpose-target.*` taxonomy facets.
- **Disposition:** Persistent user/review state of a finding, such as resolved,
  accepted-as-is, not-applicable, or false-positive.
- **Effective installation:** The files, plugins, records, configuration, and
  native components the selected game/profile state will actually expose.
- **Evidence:** A provenance-bearing input supporting or contradicting a claim
  or finding.
- **Effect extent:** Versioned, faceted classification of how broadly an
  effect may manifest across direct subject, spatial, persistence/lifecycle,
  and causal-propagation dimensions. It is separate from severity.
- **Evidence acquisition run:** A source/entity-scoped operation that acquires
  or extracts reusable evidence independently of profile analysis. It is
  user-started or a configured child of a user-initiated analysis; local
  documentation inputs additionally bind to their installation snapshot.
- **Export artifact:** A versioned rendering of explicitly selected retained
  data that records its source-object selection, filters, generator, omissions,
  and privacy choices without mutating its sources.
- **External redistribution permission:** Source- and artifact-specific
  decision about whether material may appear in an externally shareable
  artifact. It is independent of acquisition and private retention.
- **External claim:** A statement obtained from documentation or another
  source, distinct from a locally measured fact.
- **Finding:** A supported conclusion about a problem, risk, advisory, or
  incompatibility.
- **Logical finding:** Cross-run identity that links immutable finding
  revisions only when causal, applicability, and dependency equivalence
  establishes continuity of the same underlying condition or analytical
  question; changed conclusions retain explicit supersession lineage.
- **Logical case:** Cross-run identity that links immutable case revisions only
  when shared cause, applicability, and dependency equivalence is established;
  reviewed merges and splits retain explicit lineage.
- **Finding revision:** Immutable successor/supersession record created when
  new evidence changes an earlier finding's analytical conclusion; review
  disposition remains separate.
- **Hard limit:** Immutable scheduling/authorization ceiling expressed as a
  consumptive usage bound, such as calls, tokens, or estimated provider cost,
  or as an elapsed-time deadline. Before dispatch, work must reserve bounded
  consumptive usage and pass the deadline check as applicable. It is not a
  guarantee that uninterruptible in-flight work stops exactly at a deadline or
  that delayed, rounded, or otherwise uncontrollable provider-side invoice
  adjustments cannot occur; both remain explicit variance.
- **Hypothesis:** A proposed interpretation that has not met the declared
  finding threshold.
- **Installation snapshot:** Logically immutable manifest of one selected MO2
  profile's physical and effective installation state. It excludes mutable
  assumptions and scan configuration.
- **Local installed entity:** One physical MO2 mod-directory subject whose
  enabled state, priority, files, providers, and snapshot membership come from
  local state. It may have zero, one, or several separately evidenced source
  identity mappings.
- **Maturity:** Evaluated reliability of an analyzer within declared scope; not
  the confidence of an individual finding.
- **Metadata-first durable minimization:** Policy of retaining references,
  fingerprints, derived structures, and explicit gaps instead of unnecessary
  exact source content after configured dependent work is materialized. It
  does not require premature deletion during useful analysis.
- **Needs input:** Investigation/case state indicating that a material user
  answer is required; it is neither a finding disposition nor a paused job.
- **Observation:** An atomic measured fact.
- **Profile snapshot:** Informal alias for installation snapshot; normative
  documents should use installation snapshot.
- **MO2 saved selection:** Per-instance remembered profile hint used to suggest
  a startup default. It is not authoritative selection for a run; the user
  explicitly selects or confirms the profile before capture.
- **Private retention permission:** Source- and purpose-specific decision about
  which material Infinium may keep in product-owned local storage and for how
  long. It does not imply provider-transmission or redistribution permission.
- **Protected setup root:** Any resolved path whose contents can affect the
  selected MO2 instance/profile, installed mods, game/runtime, configuration,
  or generated output. Through M4, Infinium write destinations—including paths
  reached through aliases, links, or reparse points—must not overlap these
  roots.
- **Provider-transmission permission:** Source- and operation-specific decision
  about whether minimized material may be sent to a user-selected model or
  service provider. It is independent of private retention and external
  redistribution.
- **Readiness:** Categorical evaluation bound to an analysis run, coverage,
  readiness-policy version, resolved applicable disposition set (including
  unreviewed/default state), and evaluation time; advisories are non-blocking
  by default, partial-run results are provisional, and no state is a guarantee
  of runtime stability.
- **Readiness policy:** Versioned review/presentation policy controlling which
  evidence, maturity, severity, disposition, failure, and coverage conditions
  affect readiness. It is not analyzer output or semantic analysis context.
- **Review priority:** Presentation/routing value derived from severity,
  confidence, effect extent, intent, reversibility, and maturity without
  replacing those dimensions.
- **Recommendation:** Proposed remediation, investigation, or validation step.
- **Review annotation:** User-authored review history that does not become
  analyzer output, evidence, or semantic context unless explicitly converted
  into an appropriately typed object.
- **Resolved input manifest:** Record of the exact source revisions, tool/model
  identities, request settings, and referenced input evidence actually used by
  an analysis or evidence-acquisition run. Outputs remain in the run record.
- **Replayability:** Ability to re-execute a run's declared replay scope from
  retained or still-available dependencies. It is reported as complete,
  partial, or unavailable and is distinct from auditability.
- **Run-owned output:** Execution artifact, such as M1 CLI/JSON output or a
  developer trace, emitted as part of a run. It is not a user-created export
  and has no external-sharing classification merely because it is inspectable
  or copyable.
- **Reuse edge:** Explicit link from a consuming run to an artifact created by
  another run, including the dependency-equivalence proof; it does not change
  the artifact's origin.
- **Scope-incongruent reversion:** A later mod restoring stale or older values
  outside its declared purpose and thereby removing intended behavior.
- **Scan:** User-facing term for a profile-analysis operation whose normative
  persisted entity is an analysis run. A scan may be broad or targeted and may
  create a linked child acquisition run; an independently run evidence
  acquisition operation is not itself a scan.
- **Scan configuration:** Versioned per-run scope and operational settings such
  as enabled analyzers/sources, budgets, analytical reuse/clean policy,
  external-source acquisition/refresh policy, tracing, and concurrency.
- **Clean scan:** A new analysis run that bypasses reusable derived analytical
  outputs for its declared scope while using explicitly resolved source inputs;
  it does not by itself reacquire live external sources.
- **Semantic analyzer:** Analyzer that interprets the gameplay meaning or
  cross-record consistency of effective data.
- **Severity:** Worst credible consequence if a finding manifests, considered
  independently from confidence.
- **Sharing class:** Declared intended handling of an export, such as
  private/local or externally shareable. It determines required review and
  omission behavior but never overrides privacy, citation, licensing, or
  redistribution restrictions.
- **Suppression:** Independent visibility/routing state that hides a finding
  from default views without changing its disposition or readiness effect.
- **Symptom report:** Versioned user-statement evidence bound to a snapshot,
  analysis context, and known test session; it may seed investigation but does
  not prove a cause.
- **Source refresh:** Explicit reacquisition of selected live external evidence,
  producing a new acquisition result/source revision when the content or
  retrieval changes; it is separate from clean analytical recomputation.
- **Technical modification surface:** Versioned taxonomy classification of how
  a mod
  changes effective state, such as records, assets, scripts, configuration,
  native components, or generated output. Normative values use the accepted
  `surface.*` and `delivery.*` facets and do not imply an affected area or
  consequence.
- **Taxonomy assignment:** Versioned classification attached to a specific
  subject with an axis/facet, optional code, applicability state, role,
  evidence, conditions, and provenance. Unknown, unsupported, unmapped, and
  not-applicable are distinct states rather than catch-all codes.
- **Typed index:** Snapshot-bound lookup structure over one declared local
  evidence surface, such as path providers, record overrides, FormKey links,
  script APIs, configuration references, or documentation claims. Its type and
  qualification boundary determine which causal joins are valid.
- **Test session:** An installation-snapshot-bound tracking window associated
  with a game/test execution and used to correlate logs and observations with
  the correct physical state. Separately imported evidence references a test
  session only when one can be identified; use by an analysis run is linked
  separately.
