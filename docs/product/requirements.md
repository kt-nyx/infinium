# Product requirements

Status: Accepted  
Last reviewed: 2026-08-23

Accepted amendments:

- 2026-08-23 — ADR-0035 defers independent semantic-oracle qualification
  throughout M1 and M2 and creates the M3 Evaluation Readiness Gate after M2
  acceptance. M1/M2 continue to require product conformance, deterministic
  references, integration/replay/safety evidence, and fresh review, but may
  not claim an independent semantic verdict or semantic reliability. ADR-0034's
  prompt fidelity and separate proposal/extraction, support, applicability,
  and host-decision semantics remain accepted.

- 2026-08-07 — ADR-0032 defers the current M1 private held-out evaluator
  without a product verdict, retires protocol `/5` unqualified, and replaces
  only the held-out-`PASS` sequencing prerequisite for later M1 slices with
  the accepted public M1 continuation verification profile. This does not
  weaken any product requirement, required M1 case, private default-deny rule,
  or reliability/readiness claim boundary.
- 2026-08-05 — The owner accepted the bounded M1 Bethesda semantic-reporting
  contract in
  [ADR-0028](../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md):
  `EDID` identifying metadata, closed FaceGen precedence, semantic tri-state
  asset availability, a fixed backend coverage registry, layered gaps, and
  hybrid evidence-bounded taxonomy emission.
- 2026-07-28 — The owner reaffirmed direct, schema-constrained OpenAI
  Responses API calls through user-supplied, usage-priced Platform API keys
  and rejected Codex/ChatGPT-plan access as the core LLM adapter. ADR-0024
  records the rejected alternative; ADR-0013 remains authoritative.
- 2026-07-28 — The owner accepted an OpenAI-first initial provider direction
  without lowest-common-denominator capability parity, while retaining
  provider-independent authoritative domain/evidence truth. The owner also
  accepted latest-capable Nexus API routing under
  [ADR-0012](../architecture/decisions/ADR-0012-nexus-latest-capable-api-routing.md)
  and a narrow product-intent exception permitting configurable automatic
  maintenance of accepted LOOT managed data. The exact OpenAI
  Responses/search capability boundary and LOOT refresh/activation mechanism
  were subsequently accepted in ADR-0013 and ADR-0014.
- 2026-07-25 — Wave C owner disposition
  [RESEARCH-0024](../research/investigations/RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md)
  accepts taxonomy `0.1.0`, the logical typed-index/causal-join candidate
  contract, and the bounded root/generated/configuration/PEX/asset/record
  roadmaps. Exact supported shapes and evaluation execution remain gated.
- 2026-07-25 — Wave B and ADR-0008 through ADR-0011 resolve RQ-001 through
  RQ-007 and RQ-014 for M0. The initial technical boundary is MO2 `2.5.2`
  quiescent reconstruction with explicit profile binding; exact Steam
  `1.6.1170.0`; allowlisted Mutagen `0.54.2`; canonical structural snapshots
  with scoped SHA-256 dependency validity; and conditional libloot `0.29.6`
  when a milestone claims LOOT coverage. These decisions do not mark their
  conformance cases or supported-surface qualification as passed.
- 2026-07-25 — ADR-0007 removes xEdit from Infinium's product, development,
  dependency, integration, and evaluation boundaries. TOOL-001 through
  TOOL-003 now apply to MO2 and LOOT only; Bethesda semantic qualification uses
  independently specified first-party fixture truth rather than an xEdit
  oracle.
- 2026-07-25 — Accepted the RQ-031 retention, replay, deletion, and export
  policy with an owner clarification that permitted private source material
  must remain available long enough to complete useful extraction, LLM
  analysis, claim/case/finding synthesis, prose generation, provenance, and
  audit. Metadata-first retention shall not cause premature deletion. Nexus
  material exposed through supported APIs follows the development-risk
  decision in ADR-0005 unless a reversal trigger occurs.
- 2026-07-25 — Replaced the briefly adopted permissive-first-party posture in
  DIST-001 and DIST-002 with the GPLv3-family product and dependency boundary
  accepted in ADR-0006; added TOOL-001 through TOOL-003 for user-installed
  MO2/LOOT/xEdit discovery, configuration, and capability disclosure. The
  xEdit portion was later superseded by ADR-0007.
- 2026-07-25 — Originally added DIST-001 through DIST-003 with a permissive
  first-party posture. This historical amendment was superseded later the same
  day by the GPLv3-family amendment above.

This document converts the product interview into referenceable requirements.
It was accepted as part of the product baseline on 2026-07-25. Individual
entries inherit the document status; a proposed material change must return the
affected document to Draft or follow the documented supersession process before
it changes authoritative behavior.

Priority vocabulary:

- **Must:** Gates completion of the stated delivery milestone.
- **Should:** Targeted for the stated delivery milestone but may be deferred by
  an explicit product decision without failing that milestone.
- **Could:** Valuable, non-gating extension that must not distort earlier
  design.
- **Deferred:** Explicitly after M4.

Delivery milestones are defined in
[scope and milestones](scope-and-milestones.md). A delivery target states when
the complete behavior is intended to be available; priority determines whether
it gates that milestone. It is not permission for an earlier milestone to
violate the requirement when that milestone exercises the relevant behavior.
Foundational scope, authority, evidence, provenance, and safety constraints
apply from the first implementation slice that touches them, with
milestone-appropriate acceptance depth. Unless an entry states another delivery
target, its full-capability target is **M3 — Trusted personal preflight**.
Requirement-level acceptance tests are defined in the milestone plan and linked
evaluation cases once the relevant research and architecture decisions exist.

## Product and audience

### PROD-001 — Primary user

Priority: Must

The initial product shall serve experienced Skyrim mod users rather than mod
developers, while using progressive disclosure to preserve later accessibility.

### PROD-002 — Primary job

Priority: Must

The product shall focus on finding consequential setup problems before the user
commits to a real playthrough.

### PROD-003 — Finding-centric workflow

Priority: Must

The primary workspace shall be organized around cases and findings, not around
enumerating all mods or raw conflicts.

### PROD-004 — No safety guarantee

Priority: Must

The product shall never represent an absence of findings as proof that a
playthrough is safe. Readiness must be qualified by coverage and uncertainty.

## Target scope

### SCOPE-001 — Game target

Priority: Must

The initial product shall support exactly the Steam Windows x64 Skyrim Special
Edition `1.6.1170.0` executable identity recorded by ADR-0009: file size
`37,157,144` bytes and SHA-256
`C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`.
Updating or broadening support is a deliberate, tested support decision. Other
hashes, runtimes, channels, editions, and platforms are unsupported targets,
not best-effort variants.

### SCOPE-002 — Mod manager target

Priority: Must

The product through M4 shall support Mod Organizer 2 only.

### SCOPE-003 — Profile target

Priority: Must

Every analysis shall target one explicitly selected MO2 profile. MO2's
per-instance saved selection may seed a startup suggestion, but it is not the
authoritative run binding and must not start a scan without explicit user
selection or confirmation.

### SCOPE-004 — Manual initiation

Priority: Must

Scans, targeted reanalysis, and independently run documentation
acquisition/extraction through M4 shall be manually initiated. Configured stages
inside an initiated operation may proceed automatically. Continuous monitoring,
unsolicited analysis-related paid/network work, and automatic analysis triggers
are deferred. Bounded change detection during an initiated operation is required
for snapshot integrity and is not an automatic analysis trigger.

Configured non-billable maintenance of an accepted managed reference-data
source may run automatically only when an accepted source/integration decision
defines its exact allowlisted source, schedule, network/retention behavior,
failure handling, and immutable provenance. Such maintenance shall not start
analysis, general documentation acquisition, Nexus acquisition, broader web
search, LLM work, or findings. LOOT masterlist/prelude freshness is the only
currently accepted instance of this exception; its exact
mechanism is governed by ADR-0014.

### SCOPE-005 — Effective installation

Priority: Must

Effective-installation reconstruction and coverage accounting shall encompass
the selected profile's plugins, records, loose files, archives, generated
output, configuration, base game directory, and relevant root-level
components. Analyzer depth for each domain remains governed by its specific
requirements; unsupported semantics shall appear as coverage gaps rather than
being implied by reconstruction alone. These are required state/input surfaces,
not an exhaustive or mutually exclusive taxonomy of mod types or affected game
areas. The accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md) maps them without
weakening this reconstruction scope.

### SCOPE-006 — Platform target

Priority: Must

The product through M4 shall target Windows desktop only.

## Authority and safety

### AUTH-001 — Read-only initial product

Priority: Must

The product through M4 shall not modify MO2 state, mod/plugin priority or
enabled state, mod files, game files, configuration, or generated output.

### AUTH-002 — Allowed local writes

Priority: Must

The product may write only product-owned settings, secure credential storage,
application installation/update files through the approved packaging/update
mechanism, data stores, cache and temporary workspaces, history, checkpoints,
logs and diagnostic traces, assumptions, evaluation artifacts, and exports.
These writes shall remain isolated from the user's modding setup. Product data,
cache/temp, diagnostics, and update staging shall use validated
product-controlled locations; credentials shall use approved OS-backed storage
where required; user-selected exports shall reject destinations within
protected setup roots; and user-facing retention/deletion operations shall
affect only explicitly selected retained objects within the applicable
authorized location. AUTH-003 separately permits isolated cache/temp writes
owned by an approved external tool when they cannot affect the user's setup.

### AUTH-003 — External tools

Priority: Must

The product may invoke only approved operations that do not mutate the user's
MO2, modlist, game, or profile state. Commands, versions, outputs, and any
tool-local cache/temp side effects shall be recorded. A tool without a safe
non-mutating mode is not eligible for initial integration.

## Security and privacy

### SEC-001 — Untrusted-content isolation

Priority: Must

Delivery: M1 — Backend semantic proof

Retrieved/local documentation, HTML, logs, reports, mod metadata, external-tool
output, model output, and installed binary assets or executable-format inputs
shall be treated as untrusted data. They shall not grant authority, execute
code, alter instructions, or reach privileged rendering/tool surfaces without
sanitization and validation. Binary/static analyzers shall use bounded parsing
and shall not load DLLs, execute PEX or SWF code, or invoke runtime behavior to
discover dependencies.

### SEC-002 — Credential handling

Priority: Must

Delivery: Before the first authenticated provider integration; no later than M1

Provider credentials shall use an approved architecture-appropriate secure
entry/storage mechanism, remain outside ordinary application/render state, and
never enter prompts, logs, exports, or ordinary traces. Users shall be able to
revoke and delete stored credentials. Once local revocation/deletion is
confirmed, no queued, new, or retry operation shall start using that
authorization. Any already in-flight request that cannot be cancelled shall
remain visible and follow the provider adapter's declared cancellation and
usage/cost behavior.

### SEC-003 — Privileged-operation boundary

Priority: Must

Delivery: M1 — Backend semantic proof

Filesystem, process, network, navigation, and tool operations shall be exposed
through a narrow validated allowlist appropriate to the selected architecture.
Paths, URLs, commands, arguments, and caller messages shall be schema/scope
validated before privileged use. The exact thread/process/IPC topology remains
an architecture decision.

### SEC-004 — Diagnostic and export sensitivity

Priority: Must

Delivery: Staged across M1 and M4

M1 developer traces and raw structured artifacts shall be labeled with their
potentially sensitive contents. M4 externally shareable diagnostic bundles
shall require explicit selection plus inspectable inclusion/redaction and
source-policy review and shall exclude credentials in all cases.

## Scan configuration and lifecycle

### SCAN-001 — Modular analyzers

Priority: Must

Analysis capabilities shall be independently configurable and runnable.

### SCAN-002 — Granular development controls

Priority: Must

Delivery: M1 — Backend semantic proof

Development builds shall expose granular analyzer, source, budget, cache, and
trace controls. Presets may be introduced after real cost and quality data
exists.

### SCAN-003 — Pre-run estimate

Priority: Must

Before paid or long-running work, the product shall show estimated time, LLM
cost where the selected access mode is usage-priced, configured limits,
expected coverage when estimable, and any provider capability that prevents
enforcing or promptly reconciling a selected limit. For subscription/plan
work, show planned work/turn shape and provider-reported rate/credit state
where available; do not invent an API-dollar estimate or promise that current
headroom guarantees completion.

### SCAN-004 — Progressive progress

Priority: Must

Progress shall be visible at scan, stage, and analyzer levels and include at
least state, completed/remaining work, supported cases and lead-only
investigations found, elapsed/remaining time, and current/estimated
consumptive usage plus applicable cost. A
documentation-acquisition operation shall provide equivalent rollups over its
sources/entities and extraction work rather than reporting inapplicable case
progress. Usage/cost shall be recorded once at its originating operation and
rolled up without duplication. A parent scan's current cost shall include new
spend from attached configured child acquisition but distinguish reused
historical work and its original cost from spend incurred by the current run.
If a child acquisition is explicitly detached, its pre-detachment attributable
spend remains in the parent rollup while later spend is shown under the
separately authorized acquisition continuation and is not added to the parent.
Post-detachment progress, remaining-time estimates, and duration likewise
belong to the acquisition rather than extending the parent operation.

### SCAN-005 — Pause, cancel, checkpoint, resume

Priority: Must

Delivery: M3 — Trusted personal preflight

Long-running analysis and evidence-acquisition operations shall be pausable,
cancellable, checkpointed, and resumable where practical. Pausing/resuming
continues the same run. Cancellation makes that execution terminal; completed
valid work/checkpoints may seed a separately user-initiated run through
validated reuse rather than silently reviving or mutating the cancelled run.
A retry within an active run is a recorded attempt under that run; retrying
work from any terminal run requires a separately user-initiated run with
explicit reuse rather than reopening the terminal run. Pausing or cancelling a
parent shall by default stop scheduling all new attached-child work and request
pause or cancellation of those children. Any in-flight work that cannot be
interrupted cleanly shall remain visible. Continuing an attached child while
the parent is paused, or detaching a reusable child acquisition so it can
continue independently, shall require an explicit user choice showing remaining
work and cost. Detachment shall preserve immutable initiation/parent
provenance, record the lifecycle change, and separate later
progress/time/cost/control from the parent.

### SCAN-006 — Failure isolation

Priority: Must

One analyzer, source, or external request failure shall not invalidate unrelated
analysis/acquisition work. Coverage shall use the standardized completed,
completed-with-gaps, failed, skipped-by-configuration, skipped-by-limit, and
unsupported states.

### SCAN-007 — Incremental reuse

Priority: Must

Delivery: M3 — Trusted personal preflight

Manual scans may reuse work only when all declared inputs and analyzer versions
remain valid. A forced clean scan shall be available for validation. Clean
analysis recomputation bypasses reusable derived analytical outputs for its
declared scope but does not by itself reacquire live external evidence. Source
refresh is an independent policy/action; a user may explicitly combine clean
recomputation with source refresh.

### SCAN-008 — Resource defaults

Priority: Should

The product shall use conservative, idiomatic default concurrency so analysis
can run in the background and shall expose a small set of basic resource limits
appropriate to background use. The exact controls require performance and job
architecture research. Detailed tuning remains after M4.

### SCAN-009 — Saved scan configurations

Priority: Must

Delivery: Staged across M1 and M2

M1 shall support reusable, versioned scan-configuration artifacts. M2 shall let
users save, name, clone, inspect, and modify configurations. Every analysis run
shall retain the exact effective scan configuration independently of later
edits to the saved configuration. A saved user-facing configuration may bundle
operational values with semantic-context overrides for convenience, but
analysis-run startup shall resolve and retain those into the distinct effective
scan configuration and semantic analysis context.

### SCAN-010 — Calibrated user presets

Priority: Must

Delivery: M3 — Trusted personal preflight

User-facing builds shall offer empirically calibrated depth/cost presets while
retaining granular overrides and a visible effective scan configuration,
including any semantic-context changes the preset applies. Preset names and
contents shall be based on measured coverage, time, and cost rather than assumed
during early development.

## Snapshot and reproducibility

### SNAP-001 — Installation-snapshot and analysis-context binding

Priority: Must

Every scan and finding shall be bound to an immutable installation snapshot and
a versioned semantic analysis context. The analysis run separately retains its
effective scan configuration so operational changes such as budgets, tracing,
cache execution policy, or concurrency do not falsely change semantic context.

Every artifact derived directly within an analysis run shall identify that
originating run and its resolved input manifest. Evidence-acquisition artifacts
instead retain their acquisition-run ownership under DOC-011 even when the
acquisition is a configured child of a scan. A repeated run against live
sources or a nondeterministic model is not presumed identical merely because
its snapshot and configured context match.

Runtime logs and test results shall instead bind to an installation snapshot
and test-session/collection provenance. When used by an investigation, they
gain an explicit application link to the consuming analysis run/context;
changing unrelated model or scan configuration does not rewrite their physical
provenance.

### SNAP-002 — Mid-scan change detection

Priority: Must

Relevant physical/input changes during a scan shall invalidate affected stages
rather than silently combining states. Editing a saved analysis input or scan
configuration shall create a new version and shall not mutate a started run's
bindings in place. Unaffected reusable work may continue or feed a separately
user-initiated derived run through validated reuse.

### SNAP-003 — Stale result presentation

Priority: Must

When current profile state differs, retained historical results shall remain
accessible but be unmistakably marked stale. Results shall also be marked stale
relative to a newly selected analysis context when an analysis-affecting input
changed, or when retained run inputs no longer satisfy the selected
validity/freshness policy. Explicit deletion under OPS-002 may remove history.

### SNAP-004 — Safe carryover

Priority: Must

Delivery: M3 — Trusted personal preflight

Findings, evidence, assumption decisions, and cached work may carry to a new
snapshot, analysis context, or scan configuration only when dependency-aware
validation demonstrates that relevant inputs did not change. Reuse shall retain
the artifact's original provenance and record a consuming-run reuse edge rather
than rebinding history. When impact is uncertain, the product may request
scoped typed user input whose declared dependencies can be evaluated, or it
shall recompute/skip the affected work. A bare confirmation to “reuse anyway”
shall not be represented as dependency-validated carryover.

### SNAP-005 — Reproducible configuration

Priority: Must

Every scan shall retain its exact analyzer, source, provider/model, budget,
candidate-breadth, threshold, cache, tracing, concurrency, and resource
configuration.

### SNAP-006 — Replayability disclosure

Priority: Must

The product shall distinguish auditability from executable replay. Every
retained analysis or evidence-acquisition run shall report whether replay is
complete, partial, or unavailable and identify unretained files, unavailable
tool/model versions, or other missing dependencies. Retained historical results
remain inspectable even when replay is not possible. Every such retained run
shall disclose material gaps in its audit trail separately from replayability.
Retention or deletion actions shall update these disclosures without silently
corrupting the remaining audit record.

## Evidence and trust

### EVID-001 — Typed evidence model

Priority: Must

The system shall distinguish observation, external claim, candidate,
hypothesis, finding, recommendation, and coverage gap.

### EVID-002 — Provenance

Priority: Must

Every material conclusion shall retain the identities/fingerprints of its local
inputs, source, applicable versions, retrieval or observation time,
analyzer/tool/model identity, and permitted supporting or contradicting
evidence. This requirement does not imply copying every source byte; referenced
or deleted inputs shall be reflected honestly in auditability and
replayability.

### EVID-003 — Evidence hierarchy

Priority: Must

Authority shall be claim-type-specific rather than one global ranking. Direct
local state and deterministic analysis are authoritative for installed and
effective state. Applicable author-maintained and curated LOOT claims are
authoritative for stated intent, instructions, and documented constraints.
Conflicts between these classes require applicability reasoning rather than
silent override. Corroborated community evidence, uncorroborated reports, and
heuristics remain progressively weaker.

### EVID-004 — Visible LLM involvement

Priority: Must

The detail view shall identify whether an LLM extracted documentation,
interpreted deterministic evidence, or originated a hypothesis.

### EVID-005 — Grounded novel hypotheses

Priority: Must

All scan depths may originate undocumented hypotheses when grounded in specific
local observations. Breadth and cost are configurable.

### EVID-006 — Abstention

Priority: Must

An analyzer shall abstain and report missing information when it cannot support
a conclusion.

### EVID-007 — Development transparency

Priority: Must

Delivery: M1 — Backend semantic proof

Development and evaluation runs shall retain raw candidates, intermediate
evidence, failures, and abstentions without release maturity weighting.

## Findings, cases, and readiness

### FIND-001 — Independent dimensions

Priority: Must

Finding records shall represent severity, confidence, and evidence
independently and shall retain versioned taxonomy-bound classifications
sufficient to describe what was modified, what part of the game may be
affected, what consequence may occur, and how broadly it may manifest. Their
presentation shall also resolve the applicable linked disposition and
suppression state without treating either as an analyzer-produced finding
field. Originating analyzer maturity shall remain visible in evidence
provenance rather than being treated as finding confidence. The exact
distinction and controlled values for technical modification surfaces,
affected game systems/content areas, consequence types, and the faceted effect
extent shall use the accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md). Persisted
classifications shall identify the taxonomy version used so later refinement
does not silently reinterpret historical findings. Each classification shall
retain its subject, axis/facet, applicability state, declared/observed/
predicted/established role, supporting evidence, conditions, and provenance.
Unknown, unsupported, unmapped, and not-applicable shall remain distinct.

### FIND-002 — Causal case grouping

Priority: Must

Related findings shall be grouped into cases around a shared likely cause and
usually a shared resolution, not merely a shared mod or record type. A
supported case shall contain at least one finding. A lead-only investigation
case may group candidates/hypotheses before promotion but shall remain visibly
distinct.

### FIND-003 — Effect extent and symptoms

Priority: Must

Findings shall attempt to estimate the applicable direct-subject, spatial,
persistence/lifecycle, and causal-propagation facets plus likely observable
symptoms, with explicit confidence.

### FIND-004 — Resolution or validation

Priority: Must

Every finding shall attempt to provide a supported remediation with
risks/reversibility and a verification step where meaningful. If resolution is
unsupported or the item is advisory-only, it shall provide targeted validation,
further investigation, or a clear explanation of what evidence/action is
missing or unnecessary.

### FIND-005 — Finding lifecycle

Priority: Must

The product shall support at least unreviewed, investigating, action-required,
resolved, accepted-as-is, not-applicable, and false-positive dispositions.
Suppression shall be an independent visibility/routing state and shall not
change readiness or disposition by itself. A resolved disposition shall remain
a user review assertion distinct from evidence that the remediation was
verified against a new/current snapshot; the UI shall expose that distinction.

### FIND-006 — Persistent decisions

Priority: Must

User dispositions, suppression choices, and review annotations (notes) shall
persist across scans while relevant evidence is unchanged. Each disposition
shall remain bound to its exact originating finding revision and applicability;
it is not implicit state on a logical finding. Applying prior review state to a
new run or successor revision shall preserve its original
applicability/provenance and record the validated carryover. Materially changed
dependencies shall preserve the old state/annotations as history, require a new
applicable disposition, and leave the changed finding visible by default rather
than silently carrying suppression.

### FIND-007 — Categorical readiness

Priority: Must

Readiness shall use explicit categorical states rather than an opaque numeric
score. Coverage and finding counts may remain numerical.

### FIND-008 — Accepted risks

Priority: Must

Findings with an applicable accepted-as-is disposition shall stop blocking
readiness while remaining visible in retained profile history.

### FIND-009 — User-facing maturity policy

Priority: Must

Delivery: M3 — Trusted personal preflight

Analyzers meeting the selected, empirically calibrated maturity threshold may
create readiness-blocking findings when the applicable evidence threshold is
also met. Experimental analyzer output shall remain clearly maturity-labeled
and shall not affect readiness by itself. An experimental conclusion that meets
its declared finding-evidence threshold may remain a non-blocking finding; one
below that threshold remains a lead unless additional typed corroborating or
confirming evidence satisfies the threshold. Development evaluation shall not
use maturity to hide, down-rank, or reclassify analyzer contributions. The
user-facing readiness/presentation policy shall be versioned and applied
without mutating raw analyzer output, typed analytical results, or semantic
analysis context.

### FIND-010 — Case and finding disposition

Priority: Must

Individual finding dispositions shall remain canonical. A case shall summarize
member state, and any case-level bulk action shall record the resulting
per-finding disposition changes explicitly.

### FIND-011 — Speculative lead presentation

Priority: Must

Grounded candidates/hypotheses below the finding threshold shall remain
inspectable with separate counts and filtering. They shall not inflate finding
or supported-case counts or affect readiness unless promoted through a declared
evidence policy. Promotion shall create a linked case revision rather than
retroactively relabeling the earlier lead-only output.

### FIND-012 — Run/review-state-bound and provisional readiness

Priority: Must

Every readiness result shall identify the analysis run, snapshot, semantic
context, effective scan configuration, coverage, readiness-policy version, the
resolved applicable disposition set (including unreviewed/default state), and
evaluation time from which it was derived. Starting a new scan shall not
overwrite the prior applicable result.
Changing review state or readiness policy may create a new readiness evaluation
over the same analysis run, but shall not mutate the run, its analytical
output, its semantic context, or a prior retained/exported readiness
evaluation. Any readiness shown from running, cancelled, limit-reached, or
otherwise partial work shall be labeled provisional or incomplete and shall
not inherit unperformed coverage from another run. A
targeted/module/investigation run shall not replace a broader preflight
readiness result unless its declared scope plus validated carryover satisfies
the full selected readiness policy; otherwise it exposes only
scope-limited/provisional status or no readiness. Newer applicable evidence or
findings may mark the prior broader result stale for current presentation
without borrowing its coverage or rewriting its historical record.

### FIND-013 — Advisory readiness boundary

Priority: Must

Advisory findings shall remain visible and separately countable but shall not
affect readiness by default because they are not established breakage. A user
may explicitly mark an advisory action-required, after which that review
decision affects readiness like any other action-required disposition.

### FIND-014 — Cross-run finding and case identity

Priority: Must

Logical findings and cases shall remain distinct from their immutable
run-specific revisions. Reuse, supersession, disposition carryover, or case
reconciliation across runs shall occur only when declared causal,
applicability, and dependency equivalence establishes continuity of the same
underlying analytical condition or shared cause. A changed conclusion within
that continuity shall use explicit revision/supersession lineage. Ambiguous
matches shall remain separate or require review rather than being silently
merged, split, or reidentified from names alone.

## Coverage and readiness

### COVER-001 — Coverage states

Priority: Must

Every stage, analyzer, and source in a run's declared coverage universe shall
report completed, completed-with-gaps, failed, skipped-by-configuration,
skipped-by-limit, or unsupported status. The effective configuration shall
retain explicitly excluded members needed to make skipped-by-configuration
meaningful. Applicable populations shall report labeled denominators, completed
counts, gaps, and exclusion reasons rather than receiving a single misleading
status.

### COVER-002 — Multidimensional reporting

Priority: Must

Coverage shall use labeled denominators by taxonomy facet or source population
under the applicable accepted taxonomy and identify the taxonomy version where
classification affects meaning. The product shall not combine unlike
dimensions into one overall analyzed/safety percentage.

### COVER-003 — Readiness and gaps

Priority: Must

Material failures, unsupported areas, stale state, and coverage gaps shall
affect categorical readiness explicitly rather than being hidden behind finding
counts.

## User experience

### UX-001 — Summary-first results

Priority: Must

Delivery: M2 — Frontend workflow proof

Any analysis run with reportable results—including completed-with-gaps,
cancelled, or limit-reached work—shall present either its applicable categorical
readiness or an explicit scope-limited/no-readiness status, followed by a
concise prose summary, finding and supported-case counts, a separate lead-only
investigation count, coverage, cost, duration, and failures, then the
prioritized case/investigation list where entries exist. Lead-only entries
shall remain visibly distinct throughout that list.

### UX-002 — Progressive disclosure

Priority: Must

Delivery: M2 — Frontend workflow proof

High-level screens shall remain simple and approachable while case, finding, and
evidence details become progressively denser on demand.

### UX-003 — Focused mod view

Priority: Must

Delivery: M2 — Frontend workflow proof

The product shall provide a focused view for one mod showing at least its
findings/cases, relevant evidence/documentation, and analysis/coverage status.

### UX-004 — Avoid MO2 duplication

Priority: Must

Delivery: M2 — Frontend workflow proof

The product shall not make raw all-conflict or general mod management views the
primary workflow where MO2 already provides them.

### UX-005 — Targeted verification

Priority: Must

Delivery: M2 — Frontend workflow proof

After an external setup change, users shall be able to manually rerun affected
checks and verify a resolution without requiring an unrelated exhaustive scan.

### UX-006 — Responsive modern character

Priority: Must

Delivery: M2 — Frontend workflow proof

The interface shall behave as a responsive modern data/analysis tool for mod
users rather than a developer-only record editor or Windows-settings-style
application.

## User intent and assumptions

### INTENT-001 — Infer then ask

Priority: Must

The system shall infer intent from local state and documentation, asking
targeted questions only where the answer materially changes analysis.

### INTENT-002 — No scan interruption

Priority: Must

Intent questions shall not interrupt long-running scans. Ambiguous cases shall
enter an investigation-level needs-input state while unrelated work continues;
needs-input is not a finding disposition or a paused job state.

### INTENT-003 — Priority as weak intent

Priority: Must

MO2 priority and plugin order may support an intent inference but shall not
silence a meaningful finding by themselves.

### INTENT-004 — Assumptions page

Priority: Must

Users shall be able to create, inspect, confirm, edit, delete, and revalidate
profile-specific assumptions. Assumption origin (inferred or user-provided)
shall remain distinct from user-confirmation state. These actions shall create
versioned assumption and analysis-context state rather than mutate historical
contexts; deletion from the effective set shall remain distinct from explicit
history deletion under OPS-002.

### INTENT-005 — Structured reuse

Priority: Must

Confirmed assumptions shall inform later analysis without becoming universal
rules. Changed dependencies shall trigger revalidation.

## Analysis requirements

The requirements below describe product capabilities and current delivery
scope, not a closed taxonomy of mod types, technical modification surfaces, or
affected game areas. The accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md) maps analyzers and
their coverage across these capabilities; one interaction or analyzer may span
several of them.

### ANALYSIS-001 — Requirements and masters

Priority: Must

Detect missing, disabled, incompatible, or otherwise unsatisfied requirements
and masters where evidence supports applicability.

### ANALYSIS-002 — Established tools

Priority: Must

Integrate LOOT rather than reimplementing its mature functionality. When an
approved established tool already supplies another required deterministic
capability, prefer a validated adapter over reimplementation; each additional
tool remains bounded by accepted research and milestone scope.

### ANALYSIS-003 — Meaningful record interactions

Priority: Must

Analyze record conflicts only when evidence suggests risk, intentional feature
loss, incoherence, or a known requirement. Raw conflicts alone are not findings.

### ANALYSIS-004 — Semantic purpose and reversion

Priority: Must

Use mod purpose, override chains, and changed-field semantics to investigate
scope-incongruent stale-value reversions and partial feature erosion.

### ANALYSIS-005 — Cross-record and cross-layer reasoning

Priority: Must

Analyze related records, assets, scripts, configuration, and generated output
as coherent systems where their relationships affect correctness.

### ANALYSIS-006 — Asset conflicts

Priority: Must

Report asset overwrites only when they create or support a meaningful problem,
not as an alternative to MO2's conflict browser.

### ANALYSIS-007 — Patch effectiveness

Priority: Must

Evaluate whether patches are required, present, enabled, correctly ordered,
applicable to installed versions, effective, complete, superseded, obsolete, or
internally inconsistent.

### ANALYSIS-008 — Version coherence

Priority: Must

Check version compatibility across plugins, assets, patches, runtime, native
components, generated outputs, and documentation.

### ANALYSIS-009 — Root and unmanaged state

Priority: Must

Inspect relevant base-game and root-directory components such as SKSE, native
loaders, ENB, ReShade, and direct/unmanaged files.

### ANALYSIS-010 — Generated outputs

Priority: Must

Delivery: M3 — Trusted personal preflight

Use named, version-pinned analyzer modules for supported generators. For
unsupported generators, report only generic presence, provider, and bounded
structure observations plus explicit gaps; freshness shall remain unknown
unless a qualified adapter can prove the relevant run, input, configuration,
and output closure.

### ANALYSIS-011 — Configuration

Priority: Should

Layer generic syntax/winner checks, known schemas, documentation rules, and
targeted unfamiliar-configuration investigation. Configuration is initially
lower priority than semantic and compatibility analysis.

### ANALYSIS-012 — Installer choices

Priority: Should

Infer likely FOMOD or installer selections only where installed files, retained
archives, metadata, or other qualified evidence permits, and expose ambiguity.
Do not claim exact historical selections when MO2 did not persist them or when
manual installation destroyed the needed provenance. The design may
accommodate later prospective choice recording, but that feature is not
required by M3 or M4.

### ANALYSIS-013 — Missing referenced assets

Priority: Must

Delivery: M3 — Trusted personal preflight

Use format-specific analyzers to detect references to unavailable assets or
other missing components.

### ANALYSIS-014 — Performance boundary

Priority: Must

Report performance only when a concrete mechanism indicates instability,
unplayability, a known limit, or major long-term risk. Do not provide general
optimization or speculative performance grading.

### ANALYSIS-015 — Playthrough lifecycle safety

Priority: Must

Delivery: M3 — Trusted personal preflight

Extract and apply new-game, installation, upgrade, downgrade, removal,
regeneration, and save-safety instructions.

### ANALYSIS-016 — Declared analyzer contract

Priority: Must

Delivery: M1 — Backend semantic proof

Every analyzer shall declare its supported scope/exclusions, inputs and
dependencies, evidence and abstention thresholds, coverage semantics,
offline/network/LLM needs, expected cost/scale, maturity, and linked evaluation
cases. Scope and exclusions shall identify the applicable technical
modification surfaces, affected game systems/content areas, consequence types,
and effect-extent facets from the accepted
[taxonomy](mod-impact-taxonomy.md) without claiming unsupported coverage.

### ANALYSIS-017 — Candidate-first LLM escalation

Priority: Must

Delivery: M1 — Backend semantic proof

High-volume local state shall be indexed and reduced to evidence-backed
candidates before semantic LLM investigation. Exhaustive modes may broaden
deterministic candidate generation, documentation coverage, and investigation
budgets, but shall not default to naïve all-pairs model comparison. Any bounded
exception must declare its population, cost, rationale, and evaluation. Every
candidate shall retain its originating analysis run and analyzer, selection
rationale, supporting evidence, scoped population, and validity dependencies.
Candidate generation shall use snapshot-bound typed indexes and qualified
causal or dependency joins; shared taxonomy labels, filenames, locations, or
mod pairs alone shall not establish an interaction. Canonical participant
identity, explicit negative/gap outcomes, and lane provenance shall be
retained. A ranking score may order work already admitted to a lane but shall
not remove supported deterministic or mandatory work from that lane.

### ANALYSIS-018 — Inspectable change impact

Priority: Should

Delivery: M3 — Trusted personal preflight

Users should be able to compare the current installation snapshot and analysis
context with a selected prior/reference state and inspect relevant changed
providers, winners, records, assets, configuration, runtime components,
documentation, patches, and generated outputs. The comparison shall explain
dependent invalidation and carryover, and shall never treat a user-designated
reference as proof that the prior setup was correct or safe.

### ANALYSIS-019 — Bounded Bethesda semantic reporting

Priority: Must

Delivery: M1 — Backend semantic proof

The bounded M1 Bethesda analyzer shall implement the accepted `NPC_`, `RACE`,
and `REFR` field boundary; FaceGen applicability precedence; semantic
`present`/`absent`/`unknown` asset state; fixed ten-population backend coverage
registry; layered coverage gaps; and hybrid evidence-bounded taxonomy emission
defined by [ADR-0028](../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md).
`EDID` may identify a record but shall not independently establish semantic
classification, consequence, finding, or intent. Evaluation transport may
encode the tri-state losslessly, but product and user-facing contracts shall
not expose an ambiguous fourth state. Each applicable mesh and tint loose-path
obligation is counted once. Unknown loose availability shall remain unknown,
shall not be inferred from archive evidence, and shall expose population
`face-gen-loose-assets` with missing capability
`exhaustive-byte-verified-loose-provider-index` in snapshot and result gaps.
Positive loose coverage is `unsupported` at zero completion,
`completed_with_gaps` at partial completion, and `completed` only at exact
completion with no owning loose gap; `0/0` remains completed.

## Documentation intelligence

### DOC-001 — Full enabled-mod coverage goal

Priority: Must

Delivery: M3 — Trusted personal preflight

When full enabled-mod documentation coverage is configured, the operation shall
schedule acquisition/claim extraction for every identifiable enabled mod unless
excluded by explicit source or budget configuration. It may prioritize
high-risk interactions, but shall report the eligible, completed, skipped,
limited, failed, and identity-unresolved counts rather than silently omitting
lower-priority mods.

### DOC-002 — Independent acquisition and extraction

Priority: Must

Documentation acquisition and claim extraction shall be independently runnable
and eligible for dependency-validated reuse across profile scans when their
source/entity/version and extraction dependencies remain equivalent.

### DOC-003 — Identity mapping

Priority: Must

Represent each physical local MO2 mod directory as an installed entity before
attempting source identity. Infer zero-to-many source mappings where possible
and provide editable mappings for renamed, split, merged, generated, personal,
and non-Nexus mods; one source may also map to multiple local entities. MO2
metadata and source/file IDs are evidence, not universal identity proof.
Mapping edits shall create versioned analysis-context input state rather than
rewrite the mapping used by historical or active runs.

### DOC-004 — Claim inspection and adjudication

Priority: Must

Users shall be able to inspect supporting passages and classify claims as
correct, not applicable, outdated, or incorrectly extracted. Every adjudication
shall record whether it concerns reusable source/extraction correctness or
local installation applicability. Source-level review shall preserve the
original extraction and create review/revision history reusable across
profiles; local applicability decisions shall enter the semantic analysis
context. An outdated classification shall record the affected source/version
scope rather than becoming an unscoped global assertion.

### DOC-005 — Source conflicts

Priority: Must

Resolve conflicting authoritative claims by version applicability, date,
authority, specificity, and explicit supersession or expose unresolved
uncertainty.

### DOC-006 — Source policy

Priority: Must

Author-maintained sources and curated LOOT metadata are primary external
evidence. Exact captured LOOT userlist/configuration is local user input, while
direct results from a qualified read-only libloot operation are deterministic
tool evidence. Community posts and bug reports are investigative leads unless
corroborated. Prohibited scraping shall not be used.

### DOC-007 — Local documentation

Priority: Must

Inspect supported documentation shipped inside mods and mapped non-Nexus
sources.

### DOC-008 — Historical support

Priority: Must

Subject to source policy and explicit retention/deletion choices, retain the
exact cited passage plus source identity, applicability metadata, revision/date
where available, and a content fingerprint. Retain permitted private source
content for at least as long as it is needed to complete useful extraction,
deterministic and user-authorized LLM analysis, claim/case/finding synthesis,
prose generation, provenance, audit, replay, refresh, and the applicable
private diagnostic history. Metadata-first durable storage is a minimization
default, not authority to discard a source or cited passage before its
dependent work is complete. Longer full-source retention remains
source-specific and user-deletable. If cited content is removed, the affected
audit view and replayability disclosure shall show the loss. Full user-facing
revision browsing is after M4.

### DOC-009 — Freshness policy

Priority: Must

Documentation evidence shall expose retrieval/revision age and source-specific
freshness. Users shall be able to refresh external evidence explicitly. The
effective scan configuration shall declare automatic refresh behavior; the
semantic analysis context shall separately declare when retained evidence is
acceptable for conclusions despite its age.

Accepted managed reference data may additionally expose a configurable
automatic maintenance policy under SCOPE-004. A refresh may update only future
run availability/current-view freshness; it shall never replace the immutable
source revision already bound to an active or historical run.

### DOC-010 — Governed broader web search

Priority: Should

Search beyond approved primary/technical sources shall be independently
toggleable and governed by the source registry. Community results retain their
investigative authority classification and shall not be promoted merely because
an LLM selected or summarized them.

### DOC-011 — Acquisition-run provenance

Priority: Must

Each documentation acquisition/extraction operation, whether directly
user-started or a configured child of a user-initiated analysis, shall retain
its exact source/entity/version request, parent/initiation provenance,
acquisition configuration, resolved source revision,
adapter/extractor/provider/model identities, calls and outputs, coverage, cost,
auditability, and replayability. Local or in-archive documentation inputs shall
additionally identify the supplying installation snapshot. Reusable extracted
claims shall remain source-bound and shall gain an explicit application link
when consumed by a profile analysis; acquisition alone shall not create profile
readiness or findings. The acquisition configuration shall distinguish source
reuse/refresh from extraction-output reuse/clean recomputation so either layer
can be validated without implicitly changing the other.

## Runtime validation and diagnosis

### VALID-001 — Validation plans

Priority: Must

Delivery: M3 — Trusted personal preflight

Cases shall offer targeted, safe in-game validation ideas when static evidence
cannot establish runtime behavior. A validation plan shall identify the
case/finding and applicable snapshot/context, prerequisites, bounded steps,
expected/alternative/inconclusive observations, risks including save or test
state contamination, cleanup/reversibility, and what the result can and cannot
establish. It shall not automate gameplay or imply global correctness from one
test.

### VALID-002 — Test outcomes

Priority: Should

Users shall be able to record observed, apparently-correct, inconclusive,
untestable, or different behavior and attach notes/evidence.

### VALID-003 — Log provenance

Priority: Must

Delivery: Before any feature automatically supplies logs to an investigation;
no later than M3

Logs shall be classified as exact, matched, likely, unknown, or historical
relative to an installation snapshot and, where applicable, a test session.
Only exact and matched evidence enters an investigation automatically, and the
application to a consuming analysis run/context shall be recorded only when
that run actually uses the evidence. Attaching later evidence to validation or
review history shall not retroactively make it an input to an earlier run.

### VALID-004 — Manual log import

Priority: Should

Initial log collection shall be manually initiated and associated with an
installation snapshot, plus a test session when known.

### VALID-005 — Case-scoped conversation

Priority: Should

Conversational diagnosis shall be scoped to an existing case or to a
symptom-report investigation that creates lead-only investigation cases or
supported cases when the finding threshold is already met. Symptom reports and
clarifications shall be versioned user-statement evidence bound to their
snapshot/context and known test session; later revisions shall not rewrite
earlier consuming cases or runs. Submitting an initial symptom report for
diagnosis shall explicitly initiate a bounded analysis run; saving, editing, or
clarifying a report without requesting analytical follow-up shall not create a
run by itself. Each submitted model-backed follow-up, or any follow-up that can
produce/revise analytical output, shall likewise be a manually initiated
bounded analysis run with retained configuration, inputs, calls, cost, and
output provenance. It shall create linked revisions rather than mutate an
earlier case/run. A symptom report or clarification shall remain user-statement
evidence unless it explicitly supplies a typed analysis-affecting input, in
which case the product shall also create a new analysis-context version.

### VALID-006 — Manually delimited test session

Priority: Could

Delivery: Not committed; does not gate M3 or M4

Infinium may let a user explicitly start and stop a tracked test-session window
around a game launch the user performs outside Infinium. The feature may
correlate changed logs and user observations but shall not launch a process,
automate gameplay, determine success without user evidence, or introduce a
custom in-game component.

### VALID-007 — Product-initiated MO2 launch

Priority: Deferred

Delivery: After M4; requires a new authority/side-effect ADR

A product-initiated, user-authorized launch through MO2 may create a tracked
test session only after expected game/MO2 writes and isolation are explicitly
researched and accepted.

## LLM providers, privacy, and cost

### AI-001 — Provider-independent truth and capability-profiled adapters

Priority: Must

Authoritative domain, evidence, finding, case, coverage, and readiness
contracts shall remain provider-independent. The initial supported LLM
implementation shall target OpenAI and may expose OpenAI-specific operations,
tools, execution modes, and invocation provenance when they improve product
value and satisfy the evidence, safety, privacy, cost, and evaluation
requirements. Later providers may implement different declared capability
profiles; lowest-common-denominator feature parity is not required.

### AI-002 — Provider and model control

Priority: Must

When enabling LLM-backed analyzers or acquisition/extraction operations, users
shall explicitly enable a supported provider/access/account configuration. A
sole initial OpenAI provider is valid. Its initial LLM execution surface shall
be the direct Responses API using a user-supplied, usage-priced Platform API
key; Codex and ChatGPT-plan access are not core-provider modes. The UI shall
disclose authentication, models, capabilities, billing, quota, hard-limit
enforceability, and retention. When several providers or supported access
modes exist in the future, users shall select among them. Sensible automatic
model routing shall permit advanced overrides. Local-only scans shall not
require provider configuration.

### AI-003 — Context minimization

Priority: Must

Send only task-relevant context. Provider credentials may leave secure storage
only through the dedicated authentication boundary required by SEC-002 and,
along with other secrets, shall never enter model/task context. Unnecessary
usernames, absolute paths, and unrelated values shall be removed; contextual
overrides may include a non-secret value only when the task needs it.

### AI-004 — Cost and execution limits

Priority: Must

Provide pre-run estimates, hard per-operation/run limits, live usage, stage
attribution, clean stopping, resumability under unchanged run bindings, and
disclosure of skipped work. Continuing after a configured hard limit requires
a new user-initiated run with revised configuration and validated reuse rather
than mutating the exhausted run. Usage shall count once against every applicable
operation/run rollup and limit without duplicating ledger ownership. Exhausting
a child or operation limit terminates that bounded node but shall not stop
unaffected parent work whose own limits remain available; the resulting skipped
work and coverage gap shall remain explicit. Before billable work starts, it
shall reserve a declared worst-case amount against every applicable consumptive
hard limit so concurrent work cannot oversubscribe shared budget, and every
operation shall pass any applicable hard-deadline check before dispatch.
Completion shall reconcile the reservation to observed usage. If an adapter
cannot provide a finite enforceable bound for a configured consumptive
hard-limit dimension, that capability gap shall be explicit and the work shall
not start under that hard-limit configuration. Provider-side adjustments,
uncancellable charges, or uninterruptible work completing after a deadline
shall remain visible in reconciled actual usage/cost/duration and shall not
authorize further work.

### AI-005 — Provider capability reporting

Priority: Should

Expose historical usage, rate limits, remaining credits, quota, hard-limit
enforceability, and billing-reconciliation latency only where the provider
supplies reliable data, with available information visible near provider
configuration and pre-run review. Platform API usage-priced billing, provider
credits, rate/quota data, and locally calculated cost are different facts and
shall not share a misleading “remaining cost” field. Missing capabilities must
be explicit.

### AI-006 — LLM reproducibility

Priority: Must

Subject to the active retention policy and user deletion, retain the exact
request, structured context, response, provider/model, prompt and schema
version, tool results, settings, tokens, and cost. When any replay dependency
is not retained, preserve the permitted audit metadata/fingerprints and update
the run's replayability disclosure. Evaluation shall pin an exact model version
when the provider exposes a stable pin and shall record the limitation when it
does not.

### AI-007 — User-owned provider usage

Priority: Must

Delivery: Before the first authenticated or billable LLM provider integration;
no later than M1

Through M4, authenticated or billable LLM-backed work shall use authorization
supplied by the user for the selected provider/account and shall attribute
usage and applicable cost to that exact access configuration. Initial OpenAI
LLM work uses a user-supplied Platform API key and incurs usage-priced Platform
API billing; a ChatGPT subscription does not fund or authenticate that API
usage. Codex and unofficial reuse of ChatGPT credentials are excluded from the
core model path. A local or otherwise non-billable provider may operate
without credentials when its declared contract permits it. Infinium shall not
silently fall back to project-funded inference, shared project credentials,
or another provider/access/account. Any future supported subscription-backed
general API, operated service, shared credential, or subsidized inference
model requires a separate product, business, privacy, security, research, and
architecture decision.

## External tool environment

### TOOL-001 — User-installed established applications

Priority: Must

Delivery: Staged across M1 and M2

MO2 and the LOOT application shall exist only as applications installed and
maintained by the user. Infinium shall not bundle, download, install, replace,
or update those applications. MO2 is required for the initial supported
product. The current LOOT application is not the structured headless-analysis
boundary; its presence gates only any later explicitly accepted capability
that invokes it, not capabilities delivered solely through the accepted
bundled libloot/data boundary. xEdit is not an Infinium dependency or
integration.

### TOOL-002 — Detection, validation, and override

Priority: Must

Delivery: M1 configuration contract and M2 setup/settings workflow

Infinium shall attempt to detect supported MO2 and LOOT installations using
validated sources. The user shall be able to confirm or override every detected
path during initial setup and later in settings, and to supply a missing path
manually. Before any executable is used, it shall be validated for identity,
supported version, accessibility, and an accepted operation-specific contract.
Detecting a LOOT installation does not authorize invocation or imply that it is
needed by the libloot semantic boundary.

### TOOL-003 — Tool capability disclosure

Priority: Must

Delivery: M1 human-readable CLI and M2 frontend

Infinium shall report each integrated external application as available, missing,
unsupported, misconfigured, or not yet validated. Scan configuration and
pre-run review shall identify every unavailable analyzer or coverage area
caused by tool state and shall never silently substitute fabricated,
incomplete, or mutating behavior.

## Distribution and licensing

### DIST-001 — Free and strong-copyleft first-party licensing

Priority: Must

Delivery: Before the M1 implementation plan is accepted

Infinium-owned application and library code intended for distribution shall be
free and open-source software under the GNU General Public License version 3
family. Commercial use, modification, forking, and redistribution are allowed;
distributed derivatives shall preserve the source access and downstream
freedoms required by GPLv3. The exact `GPL-3.0-only` versus
`GPL-3.0-or-later` selector shall be chosen before an operative licence file or
public code distribution requires it and shall not reopen the accepted
GPLv3-family product posture unless compatibility evidence requires a broader
decision.

### DIST-002 — GPL-compatible dependency boundary

Priority: Must

Infinium may link, embed, modify, or distribute GPL-compatible libraries when
an accepted architecture or integration decision establishes their technical
need and exact boundary. Every selected dependency and transitive dependency
shall be compatible with the operative Infinium GPLv3 selector, and every
corresponding-source, notice, installation-information, modification, and
redistribution obligation shall be satisfied. Licence compatibility does not
by itself approve a dependency, authorize execution, prove non-mutation, or
establish adequate analytical coverage.

### DIST-003 — Redistribution compliance

Priority: Must

Delivery: Before the first public packaged build containing third-party
payloads

Every distributed executable, library, runtime, data seed, and asset shall have
an exact immutable version, licence classification, payload owner, notices,
source-availability or corresponding-source mechanism where required,
trademark treatment, update/recall owner, and SBOM entry. Packaging a helper
does not establish that its operation is safe, read-only, technically adequate,
or authorized.

## Offline, history, exports, and scale

### OPS-001 — Offline local analysis

Priority: Must

Every analyzer and evidence-acquisition/extraction operation shall declare
whether it requires only local inputs, cached external evidence, live network
access, or an LLM provider. Offline operations shall run every configured
component whose declared inputs are available and expose unavailable features
and cached-source freshness explicitly.

### OPS-002 — Local history

Priority: Must

Persist installation snapshots, analysis contexts, saved/effective scan
configurations, analysis runs, evidence-acquisition runs, their resolved input
manifests, jobs/checkpoints, replayability, audit-trail gaps, coverage,
findings and their logical identities and revision/supersession lineage, case
logical identities and revisions/lineage, dispositions/suppression, readiness
evaluations, versioned readiness policies, the applicable disposition/policy
inputs of each evaluation, evidence, claims, candidates, hypotheses,
recommendations, test sessions/log provenance, LLM provenance, symptom-report
revisions, review annotations and their revisions, exports, cost, and duration
indefinitely by default. Provide user-accessible retention and deletion
controls that preview effects on history, citations, auditability,
replayability, active/paused operation resumability, and downstream reuse
before deletion. The preview shall identify independently retained copies that
contain selected material, including exports, run-owned outputs, and developer
traces. Deleting a source record shall not silently delete those artifacts, and
deleting an artifact shall not delete its sources. Every deleted object must be
explicitly included, directly or through an inspectable confirmed cascade.

This indefinite-history default applies to retained product records and
derived history; it does not override source-specific permission or
minimization rules for exact source bodies, excerpts, game/mod bytes, tool
artifacts, or provider content. Permitted source material remains available
through its required dependent work under DOC-008, after which an allowed
metadata-first state may preserve the history with explicit citation,
replayability, and audit gaps.

### OPS-003 — Run outputs and exports

Priority: Must

Delivery: Staged across M1 through M4

M1 analysis runs shall emit human-readable CLI output and versioned JSON as
run-owned outputs and may emit clearly labeled developer traces. These are
local run artifacts, not user-created exports, and are not externally
shareable merely because they can be inspected or copied. M2 shall provide a
separate user-initiated structured JSON export and an explicitly selected,
sensitivity-labeled developer-trace export, and may stage HTML and/or Markdown
export; both HTML and Markdown shall be available by M3. M4 shall add a
privacy- and redistribution-reviewed diagnostic bundle suitable for external
sharing.

Every user-created export shall retain an exact selection manifest containing
the selected source object identities/revisions—including readiness
evaluation, run, snapshot, and context identities where applicable—plus
filters, intended sharing class, export configuration/schema/generator
version, creation time, declared omissions, applicable source
citation/redistribution decisions, and privacy/redaction choices. Retention
permission shall not be treated as permission to redistribute source content.
An externally shareable artifact shall omit restricted material or replace it
with a permitted citation, reference, fingerprint, or explicit omission
marker. Creating, deleting, or regenerating an export shall not mutate source
objects, runs, or findings.

### OPS-004 — High-end scale

Priority: Must

Delivery: M3 — Trusted personal preflight

Support high-end lists around 2,000 enabled mods, 2,500 plugins, millions of
file entries, large override graphs, and multi-hour exhaustive scans.

### OPS-005 — Responsive UI

Priority: Must

Delivery: M2 — Frontend workflow proof

The interface shall remain responsive and incrementally useful while analysis
runs.

## Deferred requirements

The following are explicitly deferred until after M4:

- write-capable remediation or autonomous setup changes;
- patch generation;
- other mod managers;
- other Skyrim runtimes, editions, and total conversions;
- other games;
- continuous monitoring;
- custom in-game instrumentation;
- product-initiated MO2/game launch for tracked test sessions;
- save-to-installation-snapshot association;
- community/shared compatibility service;
- full documentation revision browser;
- prospective installer-choice recording;
- detailed local-resource controls;
- game-performance recommendations and automated in-game benchmarking;
- global general-purpose chat.
