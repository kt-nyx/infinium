# Product workflows

Status: Accepted  
Last reviewed: 2026-07-26

Accepted amendments:

- 2026-07-25 — Wave C owner disposition
  [RESEARCH-0024](../research/investigations/RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md)
  accepts taxonomy-backed summary/filtering and the candidate-first analysis
  boundary; implementation and evaluation remain pending.
- 2026-07-25 — ADR-0008 through ADR-0011 accept the Wave B profile,
  runtime/semantic, snapshot, and conditional LOOT boundaries.
- 2026-07-25 — ADR-0007 removes xEdit from setup, settings, capability, scan,
  and evaluation workflows.

## 1. Configure tools and select a profile

1. Detect supported user-installed MO2 and LOOT applications.
2. Let the user confirm or override detected paths and provide missing paths;
   retain the same controls in application settings.
3. Validate executable identity, version, accessibility, and supported
   capability, then disclose missing, unsupported, or misconfigured tools and
   the analysis coverage they disable.
4. Discover configured MO2 instances.
5. Read the exact instance's **MO2 saved selection** and suggest it only when
   it uniquely resolves to a valid profile.
6. Require the user to confirm one profile.
7. Display detected game/runtime and reject unsupported targets clearly.
8. Require MO2 to be closed, then capture or select the current installation
   snapshot.
9. Resolve profile-specific assumptions, identity mappings, and claim
   adjudications into a versioned analysis context; load review history
   separately.

MO2 is required for the initial product. The LOOT application is optional: its
absence disables only a later explicitly accepted application-dependent
capability, not qualified functionality delivered solely through the bundled
libloot/data boundary. Neither application is bundled, downloaded, installed,
replaced, or updated by Infinium. xEdit has no Infinium setup or workflow role.

## 2. Configure a scan

1. Choose or load a saved configuration.
2. Select analyzers and documentation sources.
3. Configure provider, model routing, candidate breadth, and budgets.
4. Choose validated analytical reuse or clean recomputation.
5. Separately choose the external-source freshness/refresh policy.
6. Review estimated time, cost, and coverage.
7. Resolve and retain the semantic analysis context and effective scan
   configuration separately.
8. Start manually.

Development builds prioritize granular controls. User-facing presets are
introduced only after empirical calibration.

## 3. Run and monitor analysis

The highest level shows:

- current stage;
- completed and remaining work;
- supported cases and lead-only investigations found;
- elapsed and estimated remaining time;
- current and estimated total cost.

The user may drill into stages, analyzers, operations, requests, failures, cache
hits, and reasons for investigations.

The job supports pause, cancel, checkpoint, retry, and resume where practical.
Pause/resume continues the same run. Cancellation is terminal; retained valid
checkpoints may seed a separately initiated run through explicit reuse. Failed
operation attempts remain recorded, and retry after a terminal run creates a
new manually initiated run. Pausing/cancelling a parent stops new attached-child
work by default; explicit child continuation/detachment shows remaining work
and cost, preserves initiation provenance, and separates post-detachment
progress/time/spend from the parent. Failures and bounded limit exhaustion are
isolated. Relevant profile changes invalidate affected work rather than
blending states.

## 4. Review results

The results page presents:

1. applicable categorical readiness bound to the selected analysis run,
   readiness-policy version, resolved applicable disposition set (including
   unreviewed/default state), and evaluation time, marked
   provisional/incomplete when derived from partial work—or an explicit
   scope-limited/no-readiness status when the run cannot produce readiness;
2. brief prose summary;
3. numerical findings by applicable accepted classification dimensions, such
   as severity, confidence, affected game area, consequence type, technical
   surface, effect extent, and disposition, using the accepted
   [taxonomy](mod-impact-taxonomy.md);
4. separate lead-only investigation count;
5. coverage and gaps;
6. scan duration and newly incurred cost, with reused historical work/cost
   identified separately;
7. stage/analyzer failures;
8. replayability and audit-trail gaps;
9. accepted risks;
10. user-resolved findings still lacking validation;
11. prioritized supported-case and investigation queue, with lead-only entries
    visibly distinguished.

No overall numerical safety score is shown.

## 5. Investigate a case

The collapsed entry shows:

- a supported conclusion, or a clearly labeled hypothesis/needs-input state
  for a lead-only investigation;
- impact and severity for a supported case, or explicitly predicted impact for
  a lead-only investigation;
- confidence;
- affected scope;
- likely symptoms;
- primary recommendation or validation.

Expansion reveals:

- findings;
- candidates and hypotheses where present;
- atomic observations;
- record/file/configuration provenance;
- external claims and exact supporting passages;
- supporting and contradicting evidence;
- LLM involvement;
- analyzer/tool/model identities;
- unresolved questions;
- alternative resolutions and risks.

Cases group a shared likely cause and usually a shared resolution. They are not
merely mod buckets.
Lead-only investigation cases contain no finding, remain separately counted,
and cannot affect readiness until the declared semantic evidence/promotion
policy supports creation of a new supported-case revision in a consuming
analysis run.

## 6. Review findings or correct analysis inputs

Finding-review actions include:

- mark action required;
- investigate;
- mark resolved;
- accept as-is;
- mark not applicable;
- mark false positive;
- add notes (review annotations).

These update disposition/review state without changing analyzer output. The
original evidence is not rewritten; it remains available subject to explicit
retention/deletion controls.

The user may independently suppress a finding from default queues. Suppression
does not resolve, accept, or otherwise change its readiness effect.

Corrections to installed-mod identity, local external-claim applicability, or
inferred user intent are analysis-affecting inputs. They create a new analysis
context and make only dependent prior work inapplicable. They never alter an
active or historical run in place; revised analytical output requires a
separately user-initiated run that may reuse unaffected work.

A correction to the extraction or source-level status of a reusable external
claim instead creates source-bound review/revision history while preserving the
original extraction. Dependent applications across affected runs/profiles are
marked for revalidation; revised analytical output is produced only by a
user-initiated targeted run. The correction is not made profile-specific merely
because it was entered while reviewing one case.

## 7. Verify a change

After the user changes the setup outside Infinium:

1. Capture a new installation snapshot.
2. Determine which evidence and work remain valid.
3. Explain proposed reuse and retained origin/provenance.
4. Where dependency impact is uncertain, request scoped typed information that
   can participate in validation, or recompute/skip the affected work; do not
   treat a bare “reuse anyway” confirmation as validated carryover.
5. On user initiation, create a new targeted analysis run that executes only
   affected analyzers and consumes validated carryover where possible.
6. Preserve prior dispositions as history and require new review state where
   changed dependencies invalidate their applicability.
7. Preserve historical results against the old installation snapshot,
   analysis context, scan configuration, and originating run.

## 8. Manage assumptions

The assumptions page shows versioned profile assumptions, their
inferred/user-provided origin, and confirmation state:

- intended runtime;
- chosen generators or frameworks;
- mod installed for assets only;
- preferred feature combinations;
- accepted missing optional components;
- intentional supersession.

Users can create, confirm, edit, delete, or revalidate assumptions without a
full scan. Inferred/user-provided origin remains separate from confirmation
state. Each action creates versioned assumption/context state and makes only
dependent prior results inapplicable without mutating their runs. The user may
then initiate targeted reanalysis with validated reuse rather than a full scan.
Deleting an assumption from the effective set does not erase the revision used
by historical contexts; history deletion is a separate retention action.

## 9. Refresh documentation

Documentation work is independently runnable:

1. Resolve installed-mod identities.
2. Show ambiguous mappings.
3. Choose source reuse/refresh and extraction reuse/clean-recomputation policy.
4. Retrieve allowed primary sources.
5. Inspect supported local/in-archive documentation.
6. Extract versioned structured claims.
7. Retain supporting passages and provenance.
8. Expose conflicts or extraction uncertainty.
9. Cache reusable results.

Permitted source material remains available through extraction and the
configured consuming analysis, case/finding synthesis, prose, provenance, and
audit work. Metadata-first cache minimization applies only after that dependent
work is materialized; an earlier user deletion explicitly previews the
resulting completion, citation, replay, and audit gaps.

The operation records an evidence-acquisition run independent of a profile
analysis. Profile-derived target selection may be recorded without making
external claims profile-bound; local/in-archive document inputs retain their
installation-snapshot provenance. A later analysis records exactly which claim
revisions it applied.

Broader web search is opt-in and uses a governed source registry.

## 10. Validate in game

For unresolved runtime questions:

1. Generate a targeted validation plan.
2. If VALID-006 is delivered, optionally start a manually delimited test
   session around a game launch the user performs outside Infinium. After M4,
   if VALID-007 and its authority ADR are delivered, Infinium may instead offer
   a user-authorized MO2 launch.
3. Associate changed logs with the correct installation snapshot, plus a test
   session when one exists, and link the validation activity to its originating
   case/run without adding the new evidence to that immutable run.
4. Let the user record observed, apparently-correct, inconclusive, untestable,
   or different behavior.
5. Store that outcome as evidence without mutating the originating analysis.
6. If the user initiates targeted reanalysis, create a new analysis run, record
   its explicit consumption/application links to the new evidence, and create
   any resulting hypothesis, finding, and case revisions while preserving the
   originating conclusions; do not treat a single successful test as proof of
   global correctness.

Static preflight remains the primary readiness basis.

## 11. Report a symptom

A user may describe a symptom without an existing case. The tool:

1. if the initial report explicitly supplies analysis-affecting intent,
   identity, or applicability information, creates the appropriate typed input
   and new analysis context; then stores the report as versioned user-statement
   evidence bound to the installation snapshot and resulting context, plus a
   test session when applicable;
2. treats submission as manual initiation of a bounded targeted analysis run
   with retained configuration and resolved inputs;
3. searches relevant local evidence, recent changes, logs, and documentation;
4. emits bounded clarifying questions and needs-input state when material
   information is missing; a submitted answer creates a report revision without
   changing the earlier run's inputs. If the submission explicitly initiates
   model-backed or otherwise analytical follow-up, it also starts a new bounded
   run. If the answer supplies analysis-affecting intent, identity, or
   applicability information, it creates the appropriate typed input and new
   analysis context before any consuming follow-up run starts;
5. creates one or more lead-only investigation cases, or supported cases when
   a finding already meets its declared evidence threshold;
6. links related cases rather than automatically merging distinct causes.

Later clarification creates a report revision; it does not rewrite a case or
run that consumed an earlier revision. A submitted model-backed case follow-up,
or any follow-up that may change analytical output, likewise creates a bounded
targeted run and linked case/finding revisions. Pure navigation, filtering, or
display of retained evidence does not create a run. These targeted runs do not
replace broader preflight readiness unless their scope plus validated carryover
satisfies the selected full readiness policy.

Conversation remains case- or symptom-investigation-scoped, not global chat.

## 12. Export

The user can export:

- human-readable report;
- structured JSON;
- Markdown;
- HTML;
- selected privacy- and redistribution-reviewed diagnostic bundle when
  delivered at M4;
- explicitly selected developer-trace export, clearly labeled as potentially
  sensitive and distinct from the raw run-owned trace.

Exports identify the applicable readiness evaluation—including its disposition
set and policy—installation snapshot/analysis context, coverage, originating
runs/resolved inputs, analyzer versions, provenance, replayability, missing
replay dependencies, and material audit gaps. They retain an exact selection
manifest containing source-object identities/revisions, filters, export
configuration/schema/generator version, intended sharing class, creation time,
omissions, applicable source citation/redistribution decisions, and
privacy/redaction choices. Material permitted for private retention but not
redistribution is omitted or replaced with a permitted citation, reference,
fingerprint, or explicit omission marker in externally shareable artifacts.
Export creation or deletion never mutates its sources. Deleting source data
does not silently delete independently retained exports containing rendered
copies; deletion preview identifies them and requires explicit inclusion,
directly or through an inspectable confirmed cascade.
