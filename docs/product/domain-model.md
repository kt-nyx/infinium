# Domain model

Status: Accepted  
Last reviewed: 2026-07-25

This is a conceptual model. Storage schemas and wire contracts require later
architecture decisions.

## Installation snapshot

A logically immutable manifest of the selected MO2 profile and effective
installation state:

- MO2 instance and profile identity;
- enabled mods and priorities;
- enabled plugins and load order;
- effective loose/archive providers;
- game and root-directory state;
- runtime and native components;
- relevant configuration and generated output;
- raw installed-mod source metadata and locally observed versions.

An installation snapshot does not imply that every source byte is copied into
Infinium. It records sufficient identity/fingerprints and retained evidence to
detect change and explain the analysis. New physical state creates a new
snapshot; historical reruns may require the original mod files. The listed
snapshot components are required state inputs rather than an exhaustive
taxonomy; their accepted taxonomy mappings depend on RQ-036.

## Snapshot assurance

A versioned statement of which declared snapshot populations were:

- structurally captured;
- selectively content-sealed with strong hashes;
- fully byte-sealed;
- inaccessible, ambiguous, unsupported, or changed during capture.

Structural identity and content identity are separate. Metadata, timestamps,
sizes, and file IDs can support structural observations or optimizations but do
not prove byte identity. Each derived artifact declares the smallest complete
dependency closure and the assurance required for its purpose.

## MO2 saved selection

The `General/selected_profile` value read from the exact resolved MO2
instance's canonical configuration. It is discovery/suggestion provenance
only. It is not the current live profile, the last game-launched profile, the
analysis target, the installation snapshot, or the run binding.

## Local installed entity and source identity mapping

A **local installed entity** identifies the physical mod state represented in
one installation snapshot. Its identity is not a Nexus mod ID, folder name,
archive name, or declared version.

A **source identity mapping** is versioned evidence relating one or more local
installed entities to zero or more external source entities/files/versions.
Mappings may be zero-to-many or many-to-zero, can contain contradictions or
ambiguity, and belong to the analysis context rather than rewriting the
physical snapshot.

MO2 metadata, download sidecars, archive fingerprints, and user corrections
can support a mapping. Declared/source versions remain distinct from locally
observed content-derived version evidence. Normal FOMOD choice history,
merge/reinstall history, and manual-change attribution are explicit gaps
unless separately retained evidence establishes them.

## Analysis context

A versioned set of non-physical inputs used alongside an installation snapshot
that can change interpretation or conclusions:

- analysis-affecting user inputs, including intent assumptions, identity
  mappings, and local external-claim applicability adjudications;
- analyzer/ruleset semantics and requested or pinned versions;
- evidence authority, applicability, and semantic age-acceptance policy;
- tool/provider/model/prompt/schema settings that affect semantic output;
- finding-promotion and semantic classification thresholds.

Editing an assumption creates a new analysis context, not a new installation
snapshot. Finding dispositions, readiness-policy changes, and ordinary review
annotations do not create a new analysis context and cannot alter analyzer
output.

An analysis context declares its applicability and may be reused with another
installation snapshot only when its own dependencies remain valid. The analysis
run, rather than the context alone, records the exact snapshot/context pairing.

## Scan configuration

A versioned execution/scope configuration for a run:

- enabled analyzers and sources;
- depth and candidate breadth;
- budgets and stopping limits;
- analytical cache-reuse and clean-recomputation policy;
- external-source acquisition and refresh policy;
- tracing and diagnostic verbosity;
- concurrency and other resource controls.

Changing scan configuration creates a new run configuration, not automatically
a new analysis context. Completed artifacts remain reusable when their declared
semantic and data dependencies are unchanged. Overall coverage/readiness is
still run-specific because configured scope may differ.

Once an installation snapshot, analysis context, or effective scan
configuration is bound to a started run, that binding is immutable. Editing
saved assumptions or configurations creates a new version. The active run may
finish against its retained versions or supply validated reuse to a separately
user-initiated derived run, but it never adopts the edit in place.

## Analysis run

The normative persisted entity behind a user-facing scan, targeted
reanalysis/verification, case follow-up, or symptom investigation. It is a
configured execution bound to exactly one installation snapshot and one
analysis context and using exactly one retained scan configuration. It also
records a resolved input manifest containing the actual external-source
revisions, tool/provider/model identities, prompt/schema versions, request
settings, and retained or referenced inputs used by that execution. The run
record owns its directly scoped jobs, tool/model calls and outputs, coverage,
evidence derivations, candidates, hypotheses, findings, recommendations, case
revisions, cost ledger entries, and duration. A linked child acquisition run
retains ownership of its own calls, outputs, and cost ledger; the analysis run
records only its linked application/control relationship and attributable cost
rollup.

Two runs may share an installation snapshot and configured analysis context but
produce different evidence if a live source changed or a model was rerun.
Deterministic reproduction uses the retained resolved input manifest. Exact
downstream replay across nondeterministic/tool boundaries also uses the retained
run outputs; it does not silently refetch sources or re-call models. Replay is
available only while every declared retained or external dependency remains
available. The run exposes complete, partial, or unavailable replayability and
the missing dependencies. It separately exposes material gaps in the retained
audit trail.

## Evidence acquisition run

A recorded operation that acquires and/or extracts reusable documentation or
source evidence independently of a profile analysis. It is either started
directly by the user or created as a configured child of a user-initiated
analysis; it is never unsolicited. It is bound to a retained
source/entity/version request, acquisition configuration, and resolved input
manifest containing the actual source revision, adapter/extractor/model
identities, request settings, and retained or referenced inputs. It owns its
jobs, calls and outputs, extracted claim revisions, coverage, cost, and
duration.

An acquisition run does not produce profile readiness or locally applicable
findings by itself. A consuming analysis run records an explicit application
link from source-bound evidence to its installation snapshot and analysis
context. Selecting acquisition targets from an installed profile may record
that selection provenance without making the extracted claims profile-bound.
Acquisition from local or in-archive documentation additionally binds the
physical input to the installation snapshot that supplied it.

Acquisition-run bindings are immutable once started. Its auditability,
replayability, and deletion effects are disclosed under the same principles as
analysis runs. A child acquisition's initiation/parent provenance is likewise
immutable. Explicit detachment records a lifecycle transition without
reclassifying it as independently initiated or rebinding prior calls/cost;
post-detachment control, progress, duration, and spend are reported separately
from the parent analysis run.

Permitted source bodies and excerpts are private acquisition artifacts,
distinct from the claims derived from them. They remain available through the
configured extraction and consuming deterministic/LLM analysis, case/finding
synthesis, prose, provenance, and audit work. Source-specific durable
minimization or user deletion may occur afterward; surviving claims and
findings retain provenance plus explicit citation, replay, or audit gaps rather
than implying that the deleted source remains inspectable.

## Evidence

Every evidence item has:

- type and, where applicable, versioned taxonomy classifications;
- source/provenance;
- declared applicability scope;
- retrieval or observation time;
- content fingerprint;
- authority classification appropriate to its evidence/claim type;
- originating and processing analyzer/tool/model identities where applicable;
- confidence in relevance;
- supporting payload or reference.

Evidence types include local observation, deterministic result, authoritative
external claim, community report, user statement, test result, and heuristic
signal.

Applicability depends on evidence type. Local observations are
installation-snapshot-bound; derived analytical evidence identifies its
originating run; reusable source claims identify their originating acquisition
run, are bound to source/entity/version conditions, and gain an explicit
run-application link when used against a local installation. Reusable source
evidence is not falsely made profile-specific.

## Observation

An atomic measured fact without interpretation.

Examples:

- Plugin B wins NPC record X.
- File path Y is provided by Mod C.
- Required master Z is disabled.
- DLL file Q reports version N.

## External claim

A sourced statement from documentation or another external authority.

It includes:

- claim type;
- subject and related entities;
- source-supported declared-purpose/intended-feature and claimed-affected-area
  classifications under the applicable accepted taxonomy, when the passage
  supports them;
- conditions;
- applicable versions;
- exact supporting passage, or an explicit deletion/unavailability marker plus
  retained fingerprint when policy/user deletion removes it;
- source revision/date;
- authority level;
- extraction method;
- originating acquisition run and extraction revision;
- source/extraction review history;
- consuming analysis-run application links;
- context-scoped local applicability-decision links.

An author claim is authoritative evidence of stated intent or instruction, not
automatically proof that a finding applies to the local installation. A review
that an extraction is incorrect, correct, or source-level outdated is attached
to the reusable claim revision and preserves the original extraction. A
decision that the claim does not apply to one local setup belongs to that
setup's semantic analysis context instead.

## Candidate

A selected interaction that warrants investigation. Candidate selection may use
shared records, files, dependencies, runtime relationships, generated-output
relationships, documentation claims, or scope-incongruent changes.

These selection inputs are examples rather than mod/game-area categories.
Candidate classification and routing use the accepted RQ-036 taxonomy,
including the distinction between source-supported declared purpose, observed
modification surface, and predicted affected game area.

A candidate retains its originating analysis run and analyzer, selection
rationale, supporting evidence, scoped population, and validity dependencies.
It is not shown as a confirmed problem merely because it was selected.

## Hypothesis

A proposed explanation combining specific observations and claims. It records:

- supporting evidence;
- contradicting evidence;
- missing information;
- predicted impact and symptoms;
- confidence;
- origin, including LLM involvement.

## Finding

A supported conclusion about a problem, risk, or advisory.

Independent dimensions:

- taxonomy-bound classifications describing technical modification surface,
  affected game area, and consequence;
- severity;
- confidence;
- effect extent, provisionally represented by gameplay scope and blast radius;
- evidence;
- originating installation snapshot, analysis context, and analysis run;
- validated reuse/application edges to any consuming run;
- expected symptoms;
- remediation;
- validation.

The exact classification axes and controlled values are provisional pending
RQ-036. A persisted finding identifies the taxonomy version used. Taxonomy
research may split or rename a provisional axis, but it must preserve the
separation between what was modified, what part of the game may be affected,
what consequence may occur, how severe it is, and how broadly it may manifest.

Severity does not absorb confidence. A potentially catastrophic but uncertain
problem remains high-impact and low-confidence. Analyzer maturity belongs to
the evidence/analyzer provenance rather than being an intrinsic finding value.
Disposition and suppression are likewise linked review/presentation state, not
fields emitted into the immutable analytical conclusion.

A finding conclusion is immutable within its originating run. New evidence or
reanalysis that changes confidence, scope, conclusion, remediation, or
validation produces a linked finding revision/supersession record and a new
case revision rather than mutating history.

A logical finding may have linked run-specific revisions. A revision is
reconciled with an existing logical finding only when declared causal,
applicability, and dependency equivalence establishes continuity of the same
underlying condition or analytical question. A changed or rejected conclusion
within that continuity retains explicit revision/supersession lineage.
Ambiguous similarity remains separate or requires review; names alone never
establish identity.

## Case

A case groups findings, hypotheses, symptoms, and evidence around a shared
likely cause and usually a shared resolution.

One underlying incompatibility may create several record, asset, and
documentation findings. Two unrelated problems involving the same mod remain
separate cases. Individual finding dispositions are canonical; a case summarizes
its members and explicit bulk actions record the resulting per-finding changes.

A **supported case** contains at least one finding. A **lead-only investigation
case** may group candidates and hypotheses before any finding meets its evidence
threshold. Lead-only cases remain explicitly labeled, are counted separately
from supported cases, and cannot affect readiness. Promotion creates a new case
revision containing the resulting finding rather than rewriting the earlier
lead state.

A logical case may have linked run-specific revisions. A new run can reuse or
revise a case without mutating its historical membership, conclusion, or
provenance. Reconciliation with an existing logical case requires causal,
applicability, and dependency equivalence. An approved merge or split creates
explicit lineage; ambiguous similarity never rewrites identity silently.

An investigation or case may be marked **needs input** when a user answer is
required to resolve material ambiguity. This is investigation workflow state,
not a finding disposition and not a paused scan job; unrelated analysis
continues.

## Recommendation

A sourced or inferred next action:

- remediation;
- alternative remediation;
- validation;
- further investigation;
- abstention with required information.

Every recommendation identifies its evidence, uncertainty, reversibility, and
risks. Through M4, recommendations are never executed.

## Review annotation

A user-authored note attached to a finding, case, mod, profile, or review
decision. An annotation is review history, not analyzer output or semantic
input, and does not change analysis context, findings, or readiness by itself.
Edits create annotation revisions; removal from current presentation does not
rewrite contexts or analytical history, while explicit retention/deletion may
remove retained revisions with disclosed audit effects.

If a user deliberately converts information from an annotation into an
assumption, local applicability decision, symptom report, or evidence item, the
system creates that typed object with its own provenance rather than silently
changing the annotation's authority.

## Assumption

Structured profile-specific intent or configuration knowledge:

```text
subject
value
scope
origin: inferred | user-provided
confirmation: unconfirmed | user-confirmed
confidence
supporting evidence
dependencies
last verified installation snapshot and, when applicable, analysis run
status
```

Origin records how an assumption entered the system; confirmation records a
separate user judgment. Inferred, unconfirmed assumptions may reprioritize
investigation but do not silently remove meaningful evidence. User-confirmed
assumptions, including directly user-provided ones, can resolve ambiguity.
Changed dependencies require revalidation.

Assumption records and the effective assumption set are versioned. Confirming,
editing, or deleting an assumption creates a new revision and analysis-context
version; deleting it from the effective set is not retroactive erasure from
historical contexts. The same non-retroactive rule applies to analysis-affecting
identity mappings and local claim-applicability decisions.

## Export artifact

A user-initiated rendering of selected retained data into a report, structured
file, or diagnostic bundle. It records an exact selection manifest containing
the source object identities/revisions—including readiness evaluation, run,
snapshot, and context identities where applicable—plus filters, export
configuration, generator/schema version, intended sharing class, creation
time, omissions, applicable source citation/redistribution decisions, and
privacy/redaction choices.

Creating, deleting, or regenerating an export never mutates its source objects
or their analytical outputs. An export is independently retained and may
honestly be less complete than its source data. Permission to retain evidence
privately does not imply permission to include that evidence in an externally
shareable export. Deleting source data does not silently delete an independently
retained export that contains a rendered copy; retention/deletion controls
shall disclose and explicitly include each such artifact in the deletion
selection, directly or through an inspectable confirmed cascade.

Run-owned CLI/JSON output and developer traces are execution artifacts rather
than export artifacts. Making them inspectable or copyable does not grant an
external-sharing classification or replace the explicit export workflow.

## Coverage record

Describes what was:

- completed;
- completed with gaps;
- failed;
- skipped by configuration;
- skipped by configured limit;
- unsupported.

Coverage identifies its originating analysis or evidence-acquisition run.
Analysis coverage additionally identifies installation snapshot, analysis
context, stage, analyzer, source, applicable taxonomy version and
classifications, and relevant entity population.
Acquisition coverage instead identifies requested/eligible source and entity
populations, retrieval/extraction stage, and gaps. A consuming analysis records
which acquisition coverage/evidence it applied without rebinding the original
record. Unlike denominators are never collapsed into a single overall
safety/coverage percentage.

## Disposition

Initial finding lifecycle:

- **unreviewed:** no user decision;
- **investigating:** review or evidence collection is active;
- **action required:** the user intends remediation before readiness;
- **resolved:** the user records that remediation was applied for the
  applicable state; this is not itself verification evidence;
- **accepted as-is:** the risk is understood and deliberately accepted;
- **not applicable:** the finding does not apply under the recorded local
  context;
- **false positive:** the conclusion is rejected and retained as labeled
  evaluation evidence.

Findings with an applicable resolved, accepted-as-is, not-applicable, or
false-positive disposition do not block readiness for the applicability under
which the decision was recorded. Dependency changes preserve that decision as
history but may require a new applicable review state.

Resolution disposition and resolution verification remain separate. A later
snapshot/analyzer/test may verify the remedy and creates new validation evidence
and review history. If reanalysis changes or supersedes an analytical
conclusion, it also creates the applicable finding/case revisions; absence of a
new finding is not represented by mutating the old one. Until verification,
the UI identifies the resolution as a user assertion rather than
analyzer-confirmed state.

Within retained history, disposition history is append-only and bound to the
exact immutable finding revision plus the installation-snapshot/analysis-context
applicability under which the decision was made. Applying it to a later revision
is an explicit, provenance-bearing carryover decision; it is never implicit
state on the logical finding. An explicit retention/deletion action may remove
history but never rewrites surviving entries. Disposition is review state
rather than analyzer input. Case state is derived from member findings plus
case-level review annotations and investigation state.

Suppression is an independent presentation/routing flag. It may hide a finding
from default queues but never changes severity, disposition, evidence, or
readiness effects by itself. It may carry to an equivalent finding revision
through validated reuse; a materially changed finding is visible by default and
does not silently inherit old suppression.

## Readiness policy

A versioned review/presentation policy that, within the normative readiness
semantics, defines:

- which evidence and analyzer-maturity levels may affect readiness;
- how severity, confidence, and user dispositions contribute;
- which failures and coverage gaps are material;
- state precedence and release/user-facing defaults.

It is not analyzer output or semantic analysis context. Changing it creates a
new readiness evaluation over an applicable analysis run; it does not mutate or
require recomputation of that run's analytical results.

## Readiness

A categorical evaluation derived from one analysis run, its scope/coverage and
readiness-policy version, plus the resolved applicable disposition set
(including unreviewed/default state) as of a recorded evaluation time. A
review-state change can create a new readiness evaluation over the same run
without mutating the run or an earlier retained or exported evaluation. A
readiness-policy change behaves the same way and does not create a semantic
analysis context.

Running or otherwise partial work may expose provisional readiness, but it
cannot borrow unperformed coverage from a previous run or overwrite that run's
historical result. Targeted runs do not replace broader readiness unless their
own scope plus validated carryover satisfies the selected readiness policy.
Newer applicable evidence may nevertheless make the prior result stale for
current presentation without mutating its historical value.

Advisories remain visible/countable but are not readiness-relevant by default;
an explicit action-required disposition makes a particular advisory relevant.
Normative states and dimension meanings are defined in
[severity, confidence, maturity, coverage, and readiness](severity-confidence-and-coverage.md).

## Test session

Associates runtime evidence with:

- installation snapshot;
- tracking-window start/end and launch/exit time when observed;
- runtime;
- plugin/configuration fingerprints;
- originating validation plan/run when applicable;
- selected validation cases;
- log provenance;
- user observations.

Log relevance:

- **exact:** captured during a tracked session whose relevant fingerprints
  remained unchanged;
- **matched:** imported independently but verified against all declared
  snapshot/session dependencies;
- **likely:** partial evidence suggests the same state, but material
  dependencies were not verified;
- **unknown:** insufficient provenance to associate the log;
- **historical:** known to describe a different physical installation or test
  session state.

Only exact and matched logs enter investigations automatically. Other logs may
be attached manually with their weaker provenance kept visible. Each use by an
analysis context/run is an explicit application link; the test session itself
does not change when analysis configuration changes. Linking later evidence to
review/validation history does not make it an input to an earlier immutable
analysis run; a consumption edge is recorded only when a run actually uses it.

## Symptom report

A versioned user statement describing observed or suspected behavior. It binds
to an installation snapshot and analysis context, plus a test session when one
is known, and records time, origin, affected scope, clarifying answers, and any
attached logs or observations.

A symptom report is evidence of what the user reported, not deterministic proof
of its cause or even of the described runtime mechanism. It may seed lead-only
investigation cases; a supported finding/case still requires the declared
evidence threshold. Editing or augmenting a report creates a revision and does
not rewrite cases or runs that consumed an earlier revision. A model-backed or
analytically productive follow-up is represented only after explicit user
initiation by a new bounded analysis run that consumes the applicable report
revision. Clarification remains user-statement evidence unless it explicitly
supplies an assumption, identity mapping, or local applicability decision. This
applies to the initial report as well as later clarification; the corresponding
typed input and new analysis-context version are created as well.

## Dependency-aware validity

Every reusable artifact declares the inputs that determine validity:

- content and profile fingerprints;
- analyzer/ruleset/tool versions;
- source revisions;
- prompt/schema/model configuration;
- analysis-affecting assumption, identity-mapping, and local
  claim-applicability-adjudication set versions;
- upstream evidence.

Carryover is allowed only when dependencies remain equivalent for the artifact's
purpose. An artifact's origin is never rebound: a consuming run records a reuse
edge and its validity proof, or emits a new revision when the conclusion
changes. When uncertain, recompute/skip the affected work or request scoped
typed user input whose dependencies can be evaluated. Generic user approval
does not convert unproven equivalence into validated carryover.
