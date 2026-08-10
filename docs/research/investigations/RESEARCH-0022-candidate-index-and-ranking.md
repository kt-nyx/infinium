# RESEARCH-0022: Candidate index and ranking

Status: Completed
Disposition: recommendation accepted by project owner
Date: 2026-07-25

Last reviewed: 2026-08-10
Researcher: Codex agent

Primary question: RQ-035

M0 wave: C

Decision enabled: accepted logical candidate-index and interaction-graph
contract, staged candidate-generation and scheduling policy, and a refined
EVAL-0032 experiment contract

Acceptance note: The project owner accepted the logical design and EVAL-0032
specification on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
The retained benchmark remains an author-coupled research probe, not an
independent product evaluation or architecture selection.

Current corpus note: RESEARCH-0035 subsequently qualified the exact
EVAL-0016/EVAL-0017 candidates. References below to their earlier incomplete
or unselected state are historical; EVAL-0032 and the controlled-real cases
now have accepted Wave F specifications and still require independent M1
execution.

## 1. Question and bounded answer

RQ-035 asks:

> Which local indexes, interaction-graph representation,
> candidate-generation rules, and staged ranking strategy can retain
> meaningful interactions at high-end scale without naïve all-pairs model
> comparison?

The recommended answer is:

1. reconstruct one immutable installation snapshot into surface-specific
   indexes rather than a universal mod-pair table;
2. project qualified observations into a typed, provenance-bearing
   interaction graph;
3. generate candidates from exact local joins, declared dependencies,
   override/reversion shapes, qualified consumer relationships, and applicable
   claims;
4. route each emitted event into one of five explicit dispositions:
   deterministic local conclusion, mandatory semantic review, investigative
   lead, resolved negative, or unsupported gap;
5. use ranking scores only to order work already admitted to a lane;
6. keep unprocessed and unsupported populations visible; and
7. give an LLM only the bounded subgraph and applicable evidence for an
   admitted semantic candidate.

A corrected three-scale synthetic probe supports the *algorithmic shape* of
that proposal. Across three repeats at each scale, a truth-separated
post-detection evaluator recorded:

- 1,152 of 1,152 supported cases in their expected deterministic or mandatory
  lane;
- 1,152 of 1,152 matched negatives explicitly resolved with zero escalation;
- 1,152 of 1,152 unsupported cases emitted as gaps; and
- 720 of 720 mandatory semantic cases present in the executed queue.

The high-scale fixture contained 2,000 mods, 2,500 plugins, 2 million logical
paths, 3 million provider entries, and 5.246 million modeled graph edges. A
representative high run emitted 9,368 analyzed events involving 9,339 unique
canonical participant-mod pairs. That is 0.467% of the 1,999,000 mathematically
possible pairs, or a 99.533% *analyzed-pair population reduction*.

That comparison is now valid because every event carries two actual,
range-checked mod IDs from generated index input and canonicalizes them before
pair counting. It is not evidence that all meaningful real interactions are
covered, that every event needs model work, or that pair count is equivalent
to candidate-bundle count.

The corrected probe no longer supports the previous score-cutoff failure
claim. It instead executes a mandatory lane that is independent of score and
tests the boundary by perturbing scores: membership stayed identical in all
nine runs while ordering changed. The result supports the policy that score
may schedule an admitted lane but may not define supported-rule coverage.

The probe is still synthetic and author-coupled. The same artifact contains
the truth materializer, fixture constructor, detector, and evaluator.
Detection does not receive truth, and evaluation occurs afterward, but this is
not an independently implemented analyzer or independently authored fixture
corpus. The reported recall is therefore bounded structural evidence, not
product validation.

This report does not select a database, storage engine, process topology,
model, provider, price, UI, or production architecture.

## 2. Authority and requirements

This proposal is governed by:

- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md),
  which keeps observations, claims, candidates, hypotheses, findings,
  recommendations, and gaps distinct and requires deterministic reduction
  before model investigation;
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md),
  which binds artifacts to one immutable snapshot and semantic context;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md),
  which supplies authoritative effective-provider and order state;
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md),
  which permits only positively qualified record, field, link, and archive
  semantics;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md),
  which requires the smallest complete validity dependency closure;
- [ANALYSIS-017](../../product/requirements.md#analysis-017--candidate-first-llm-escalation),
  which forbids naïve all-pairs model work and requires candidate-selection
  provenance and population accounting;
- [EVID-005](../../product/requirements.md#evid-005--grounded-novel-hypotheses),
  which permits undocumented hypotheses only from specific local evidence;
- [OPS-004](../../product/requirements.md#ops-004--high-end-scale);
- [EVAL-0032](../../evaluation/case-catalog.md);
- the evaluation strategy, fixture guidelines, and anti-overfitting rules;
  and
- the accepted [M0 Wave C plan](../../plans/milestones/m0/plan.md#wave-c--analysis-surfaces-taxonomy-corpus-and-candidate-scale).

The logical design consumes the proposed surface contracts in:

- [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md);
- [RESEARCH-0015](RESEARCH-0015-generated-output-tool-surfaces.md);
- [RESEARCH-0016](RESEARCH-0016-configuration-ecosystem-survey.md);
- [RESEARCH-0017](RESEARCH-0017-compiled-papyrus-analysis-boundary.md);
- [RESEARCH-0018](RESEARCH-0018-asset-reference-completeness.md);
- [RESEARCH-0019](RESEARCH-0019-semantic-record-family-roadmap.md);
- [RESEARCH-0020](RESEARCH-0020-evaluation-corpus-and-real-mod-candidates.md);
  and
- [RESEARCH-0021](RESEARCH-0021-skyrim-mod-impact-taxonomy.md).

RESEARCH-0020 currently supplies an incomplete, unqualified EVAL-0016
candidate and leaves EVAL-0017 unselected and replacement-required. Neither
real-mod lead is used as truth here. RESEARCH-0021's taxonomy is now accepted;
its labels may stratify later queues but do not establish interactions or
findings.

## 3. Scope and explicit non-scope

### In scope

- logical indexes required by the observed Wave C surfaces;
- a typed interaction-graph representation;
- deterministic candidate-rule prerequisites;
- candidate identity, provenance, and causal grouping;
- explicit lane assignment and within-lane scheduling;
- bounded use of accepted taxonomy labels as coverage strata or routing priors;
- a durable synthetic benchmark at atomic, medium, and high scales;
- explicit positives, matched negatives, unsupported cases, and distractors;
- canonical participant-pair accounting;
- provider-neutral model-workload envelopes;
- recall, volume, latency, and memory observations; and
- EVAL-0032 refinement.

### Out of scope

- production implementation;
- a database or serialization decision;
- accepted taxonomy;
- current provider pricing;
- parser or analyzer qualification;
- real-mod correctness;
- candidate-to-finding semantic accuracy;
- filesystem IO, hashing, archive decompression, plugin parsing, persistence,
  checkpoints, or model-call performance;
- duplicate/multi-symptom causal-bundle accuracy;
- full documentation acquisition; and
- any rule keyed to a real mod name, FormID, EditorID, filename, cell, NPC, or
  fixture identity.

## 4. Proposed logical indexes

These are contracts, not storage choices.

### 4.1 Canonical identity dictionaries

Map snapshot-bound identities to compact internal IDs:

- installation snapshot, run, analyzer, and rule version;
- installed entity and source mapping;
- actual participant mod identity;
- plugin, ModKey, and FormKey;
- effective logical path and provider;
- script/type/API symbol;
- configuration file, entry, and schema;
- runtime-consumed generated output and declared dependency;
- native component, release, and relationship manifest;
- external claim, revision, and applicability condition; and
- eligible population, unsupported state, and coverage gap.

Names are display/provenance values, not semantic keys.

### 4.2 Effective-provider path index

Key:

```text
(snapshot_id, qualified_namespace, canonical_lookup_key)
```

Value:

- ordered provider chain;
- effective winner;
- loose/archive origin;
- owner/source mapping;
- lookup semantics and qualification;
- retained fingerprints; and
- validity dependencies.

This answers who provides a path and whether a *qualified* consumer target is
present. Shared filenames alone do not establish a harmful interaction.

### 4.3 Record override and changed-field index

Key:

```text
(snapshot_id, FormKey, qualified_scope_version)
```

Value:

- ordered override chain;
- effective winner;
- source/effective field values for qualified fields;
- changed-field and reversion states;
- master/dependency relationships;
- support status for record, field, and runtime semantics; and
- provenance/dependencies.

The generic rule is scope-incongruent reversion, not an NPC-specific conflict.

### 4.4 Typed reference and reverse-consumer index

Key forward references by qualified source subject/field and reverse them by
target identity. Retain consumer, target, reference kind, effective status,
requiredness/applicability, qualification source, and dependency closure.

This is needed for missing asset targets, placed-reference topology,
script/VMAD consumers, named configuration references, and cross-layer blast
radius. Unparsed bytes and guessed strings remain gaps.

### 4.5 Script public-surface index

Index effective PEX definitions, public symbols, consuming scripts/VMAD
properties, provider identity, and analysis support state. Candidate rules
require a qualified public-surface or consumer relationship; co-occurrence of
scripts is not enough.

### 4.6 Configuration relationship index

Index only named, qualified parsers or schemas. Retain file/provider identity,
entry identity, parsed references, defaults, runtime/custom-condition support,
and dependency closure. Generic syntax-valid configuration is not semantic
authority.

### 4.7 Runtime-generated-output dependency index

Index exact runtime-consumed outputs against generator adapter version,
retained run/config/tool evidence, declared qualified inputs, current
fingerprints, and completion/freshness state.

Non-runtime intermediates remain evidence/provenance rather than modification
surfaces. Missing manifests or unknown tool versions produce gaps, not guessed
staleness.

### 4.8 Native/component relationship index

Index components only through qualified relationship manifests: loader versus
runtime, release, expected companions, compatibility constraints, effective
provider, and support state. File adjacency alone is not a compatibility rule.

### 4.9 Claim and applicability index

Index retained external claims by source, revision, subject, versions,
conditions, asserted relation, authority, and contradiction status. A claim
may corroborate or negate a candidate only when applicability is established.

### 4.10 Coverage and taxonomy indexes

Index analyzer-supported/excluded populations, gaps, and original proposed
taxonomy assignments. Taxonomy purpose/surface/area mappings may support
stratified sampling and coverage display. They do not create findings or
prove an interaction.

## 5. Interaction-graph contract

### 5.1 Node classes

- snapshot, run, analyzer, and rule;
- installed entity and actual participant mod;
- plugin/record/field;
- logical path/provider/archive member;
- script/API symbol/consumer;
- configuration entry/reference;
- generator/output/input dependency;
- native component/relationship manifest;
- external claim/revision;
- candidate, hypothesis, finding, case, and gap.

### 5.2 Edge classes

- `provides`, `wins_over`, `overrides`, `reverts`;
- `references`, `consumes`, `requires`;
- `generated_from`, `expected_companion_of`, `patches`;
- `declares`, `corroborates`, `contradicts`, `applicable_to`;
- `selected_by`, `supported_by`, `depends_on`;
- `grouped_into`, `resolved_as`, `unsupported_for`; and
- proposed taxonomy-assignment edges kept distinct from causal evidence.

Every edge retains origin, analyzer/rule version, snapshot/run binding,
qualification/support state, and invalidation dependencies.

### 5.3 Physical constraint

The logical graph does not require an object-per-node runtime representation.
Compact integer dictionaries, typed arrays, adjacency ranges, and indexed
tables are compatible. A dense mod-by-mod matrix is neither required nor
recommended.

## 6. Candidate generation

### 6.1 Admission rule

A candidate event may be emitted only when it has:

1. a declared analyzer/rule version;
2. an exact eligible population;
3. one or more specific local observations;
4. qualified semantics for every inference used;
5. actual participant identities where participants exist;
6. a reason selected;
7. supporting and contradicting evidence references;
8. complete validity dependencies; and
9. an explicit disposition or gap reason.

### 6.2 Strong rule families

| Rule family | Minimum qualified join | Initial disposition |
|---|---|---|
| Required asset target absent | Typed required reference plus effective-provider lookup | Deterministic local or mandatory semantic according to qualified consumer semantics |
| Scope-incongruent record reversion | Override chain plus qualified intended/effective field scope | Mandatory semantic unless exact rule semantics establish a local conclusion |
| Placed-reference topology reversion | Qualified link/topology delta plus effective winner | Mandatory semantic |
| Consumed script public-API regression | Effective public surface plus exact consumer | Mandatory semantic |
| Runtime-generated output stale/mixed | Qualified runtime output/run/config/dependency evidence | Mandatory semantic |
| Named configuration target absent | Qualified schema/parser reference plus target lookup | Deterministic local or mandatory semantic |
| Native expected companion absent | Qualified relationship manifest plus effective components | Deterministic local |
| Patch ineffective/stale | Exact patch applicability/effect plus effective winner/dependency state | Mandatory semantic |
| Applicable documentation conflict | Version/applicability-qualified claim plus matching local state | Corroboration, resolved negative, or mandatory semantic |

### 6.3 Investigative rules

Weak but specific local observations may enter a separately measured
investigative lane:

- incomplete typed references;
- suspicious but unqualified override shapes;
- bounded reverse-consumer neighborhoods;
- purpose/surface/area mismatch;
- geometry or spatial proximity with explicit unsupported semantics;
- unresolved documentation applicability.

These are not normal findings and should not be sent wholesale to a model.

### 6.4 Rejected generators

- all mod pairs;
- all plugin pairs;
- shared taxonomy label alone;
- shared filename alone;
- same cell/worldspace alone;
- record conflict count alone;
- embedding similarity alone;
- mod-name or known-patch allowlists;
- unqualified parser output; and
- whole-profile model discovery.

## 7. Candidate identity, grouping, and provenance

A candidate identity should include:

```text
snapshot
+ run/analyzer/rule version
+ focal causal subject
+ mechanism
+ qualified participant identities
+ evidence/dependency digest
```

Required fields include:

- exact participants and canonical participant order;
- originating run/analyzer/rule;
- focal subject and mechanism;
- reason selected;
- supporting and contradicting evidence;
- eligible population and support state;
- score features, if scheduled;
- disposition/lane;
- validity dependencies;
- grouping key and lineage.

Events should eventually group into a causal bundle by focal subject,
mechanism, dependency chain, and resolution boundary—not merely by mod pair.
This corrected benchmark does **not** test duplicate grouping or
multi-symptom false merges/splits. It counts events and participant pairs
separately and leaves causal-bundle evaluation to RQ-033/EVAL-0032.

## 8. Staged scheduling

### Stage 0 — exact local reconstruction

Build indexes and emit observations, negatives, and gaps without model work.

### Stage 1 — deterministic corroboration

Apply exact claims, applicability, contradictions, and supported local rules.
Preserve resolved negatives as inspectable evidence.

### Stage 2 — lane assignment

Assign exactly one disposition:

| Disposition | Meaning | Model-decision work |
|---|---|---|
| `deterministic-local` | Exact supported local conclusion | None required |
| `mandatory-semantic` | Enabled supported rule requires interpretation | Required unless explicitly budget-limited |
| `investigative-lead` | Specific local evidence exists but semantics/impact remain weak | Opt-in, sampled, or additionally corroborated |
| `resolved-negative` | Applicable evidence establishes the non-problem state | None |
| `gap` | Required semantics/data/support are unavailable | None; report coverage limitation |

### Stage 3 — within-lane ranking

Ranking may use:

- evidence specificity;
- semantic qualification;
- direct consumer/dependency edges;
- cross-layer corroboration;
- applicable independent documentation;
- potential structural blast radius;
- validation cost;
- user-selected depth; and
- diversity/coverage strata.

Score is not severity, confidence, truth, or admission authority. Changing it
may reorder a lane but must not silently remove enabled supported-rule
coverage.

### Stage 4 — bounded model work

Provide only the candidate subgraph, applicable claims, contradictions,
support state, and requested output schema. The model may synthesize a
hypothesis, consequence, resolution, or validation plan. It may not rewrite
local observations or turn a gap into certainty.

### Stage 5 — completion accounting

For every rule and lane report generated, resolved, queued, completed, failed,
skipped, budget-limited, and unsupported counts. Unprocessed work never becomes
a clean result.

## 9. Corrected durable experiment

### 9.1 Why the first probe was replaced

The first RQ-035 probe inserted truth labels and detector output in the same
construction branches, represented only some negatives as events, used random
pair keys rather than canonical actual participants, and simulated score
thresholds without executing the recommended mandatory-lane policy. Its
recall, negative-escalation, pair-reduction, and score-cutoff results were not
acceptable evidence.

All such results have been removed. The durable version-2 artifacts replace
the OS-temp probe.

### 9.2 Artifact manifest

Artifacts live under
[artifacts/RESEARCH-0022](artifacts/RESEARCH-0022/README.md).

| Artifact | Bytes | SHA-256 from reviewed run |
|---|---:|---|
| [README.md](artifacts/RESEARCH-0022/README.md) | 2,632 | `F603E0669ECCEAC6C45317CAF3372957EE2C70E47FA157E44299156C6369669C` |
| [benchmark-config.json](artifacts/RESEARCH-0022/benchmark-config.json) | 2,559 | `C0DFFFB108A39C39BEC53786D5384BBAAF9749DCF7692972A1C7DFDC4E4C6393` |
| [benchmark.mjs](artifacts/RESEARCH-0022/benchmark.mjs) | 28,796 | `2F47F4D8EAF0CBA9AA399F14DA0C0A8502E9DB236AB2257430BE22DFA992538A` |
| [benchmark-truth-manifest.json](artifacts/RESEARCH-0022/benchmark-truth-manifest.json) | 1,259,710 | `633027784C5EE35347DD01E938F94C978CC244243F0B154436C1AB9A9BB4FA2B` |
| [benchmark-results.json](artifacts/RESEARCH-0022/benchmark-results.json) | 3,475,513 | `9A87D8763A8938B74379BB1B9F20158178C49B7F7E37886057AD50CE5D78EDBE` |

Exact replay:

```powershell
node docs/research/investigations/artifacts/RESEARCH-0022/benchmark.mjs prepare
node --expose-gc docs/research/investigations/artifacts/RESEARCH-0022/benchmark.mjs run
```

The reviewed run used Node.js `v24.11.1`, Windows `10.0.26200` x64, an AMD
Ryzen 9 7950X3D, 33,408,696,320 physical bytes, and exposed GC.

Timing, RSS, `generatedAt`, and the result-file hash may change on replay.
Structural identities, dispositions, counts, canonical pairs, and workload
arithmetic should remain stable for the same script/config/truth bytes.

### 9.3 Evidence boundary

The experiment has two distinct checks:

1. **Construction-coupled smoke check.** After fixture construction, the
   script verifies that every explicit truth case was encoded into the intended
   input state. Because the same artifact authors truth and construction, this
   is not recall evidence.
2. **Truth-separated post-detection evaluation.** Detection receives only
   generated index inputs and public rule/lane configuration. It does not
   receive the truth manifest, expected class, or expected disposition.
   After detection and mandatory-lane ranking complete, a separate evaluator
   compares observation identity, disposition, and canonical participants
   against the truth manifest.

The second result is independent of detector *inputs*, but not independently
implemented or independently authored. It is suitable for a bounded
algorithm-shape experiment, not an acceptance oracle.

### 9.4 Exact truth and input construction

`prepare` materialized 3,456 explicit truth cases:

- eight rule families;
- three scales;
- three repeat seeds per scale;
- equal supported-positive, matched-negative, and unsupported cases; and
- actual canonical participant mod IDs for every case.

Seeds use:

```text
(0x5EEDC0DE + scaleIndex * 0x10000 + repeatIndex) >>> 0
```

| Scale | Seeds | Cases/family/outcome/run |
|---|---|---:|
| Atomic | `1592639710`, `1592639711`, `1592639712` | 4 |
| Medium | `1592705246`, `1592705247`, `1592705248` | 12 |
| High | `1592770782`, `1592770783`, `1592770784` | 32 |

The fixture constructor turns those cases into generated typed-array index
inputs. The detector sees state, relationships, observation identity, and
actual participant IDs, but no expected output.

### 9.5 Scale manifests

| Population | Atomic | Medium | High |
|---|---:|---:|---:|
| Mods | 200 | 800 | 2,000 |
| Plugins | 300 | 1,200 | 2,500 |
| Logical paths | 50,000 | 500,000 | 2,000,000 |
| Provider entries | 75,000 | 750,000 | 3,000,000 |
| Typed reference edges | 40,000 | 300,000 | 1,000,000 |
| Records | 20,000 | 150,000 | 500,000 |
| Script definitions | 2,000 | 20,000 | 80,000 |
| Script consumers | 4,000 | 40,000 | 160,000 |
| Config entries | 2,000 | 20,000 | 80,000 |
| Runtime-generated units | 100 | 500 | 2,000 |
| Native components | 100 | 500 | 2,000 |
| Patch-state units | 100 | 500 | 2,000 |
| Broad neighborhood leads/run | 100 | 1,000 | 5,000 |
| Modeled logical nodes | 74,800 | 693,500 | 2,670,500 |
| Modeled logical edges | 161,300 | 1,411,500 | 5,246,000 |

The probe uses integer identities, typed provider chains, typed reference
arrays, compact state arrays, and an event list. It allocates no mod-pair
matrix.

### 9.6 Mandatory-lane test

Supported families are declared deterministic or mandatory in configuration.
Detection assigns disposition before score. Only events already in
`mandatory-semantic` receive a score and enter ranking.

Each run then applies a perturbed score map. All nine runs assert:

- mandatory membership is identical under both score maps; and
- ordering changes under the perturbed scores.

This tests the proposed boundary directly. It does not compare model quality
or establish the best scoring features.

### 9.7 Provider-neutral workload assumptions

No model or provider was called. The envelope assumes:

- four events per call;
- 500 fixed input tokens per call;
- 900 input tokens per event; and
- 300 output-cap tokens per event.

```text
calls = ceil(events / 4)
input = events * 900 + calls * 500
output_cap = events * 300
total_envelope = input + output_cap
```

Dollar cost is intentionally absent and must be calculated later from current
selected-provider prices and measured tokenization.

## 10. Corrected results

### 10.1 Truth-separated evaluation across three repeats

| Metric | Atomic | Medium | High | Total |
|---|---:|---:|---:|---:|
| Supported eligible | 96 | 288 | 768 | 1,152 |
| Supported correct disposition and participants | 96 | 288 | 768 | 1,152 |
| Supported recall in declared synthetic scope | 100% | 100% | 100% | 100% |
| Matched negatives eligible | 96 | 288 | 768 | 1,152 |
| Explicitly resolved negative | 96 | 288 | 768 | 1,152 |
| Matched-negative escalation | 0 | 0 | 0 | 0 |
| Unsupported eligible | 96 | 288 | 768 | 1,152 |
| Explicitly gapped | 96 | 288 | 768 | 1,152 |
| Mandatory semantic eligible | 60 | 180 | 480 | 720 |
| Present in executed mandatory queue | 60 | 180 | 480 | 720 |
| Incorrect or duplicate truth outcomes | 0 | 0 | 0 | 0 |

The result file contains one outcome record for every truth case. Each scale
summary also repeats every matched-negative record, including expected and
observed disposition, canonical participants, escalation state, and queue
position.

### 10.2 Representative event volume

First repeat at each scale:

| Disposition | Atomic | Medium | High |
|---|---:|---:|---:|
| Deterministic local | 12 | 36 | 96 |
| Mandatory semantic | 20 | 60 | 160 |
| Resolved negative | 32 | 96 | 256 |
| Gap | 32 | 96 | 256 |
| Investigative lead | 220 | 1,900 | 8,600 |
| Total analyzed events | 316 | 2,188 | 9,368 |

Investigative leads dominate volume. They are deliberately generated
distractors and broad neighborhoods, but they have no finding labels and are
not counted as false-positive findings.

### 10.3 Canonical participant-pair accounting

For every event, the detector reads two actual mod IDs from generated input,
range-checks them through the fixture scale, rejects invalid self-pairs, and
sorts the pair before keying.

| Scale | Unique canonical pairs, repeat 0 | Range across repeats | All possible `n(n-1)/2` | Analyzed-pair population reduction |
|---|---:|---:|---:|---:|
| Atomic | 313 | 312–314 | 19,900 | 98.427% |
| Medium | 2,178 | 2,178–2,182 | 319,600 | 99.319% |
| High | 9,339 | 9,339–9,355 | 1,999,000 | 99.533% |

This is a scope/volume comparison, not recall proof. Resolved negatives, gaps,
and investigative leads are included in the numerator; model work is a much
smaller subset.

### 10.4 Mandatory versus optional workload

Representative run:

| Scale | Mandatory events/calls | Mandatory token envelope | Optional investigative events/calls | Optional investigative token envelope | Combined envelope |
|---|---:|---:|---:|---:|---:|
| Atomic | 20 / 5 | 26,500 | 220 / 55 | 291,500 | 318,000 |
| Medium | 60 / 15 | 79,500 | 1,900 / 475 | 2,517,500 | 2,597,000 |
| High | 160 / 40 | 212,000 | 8,600 / 2,150 | 11,395,000 | 11,607,000 |

The probe plants no supported cases in the broad investigative population, so
it cannot measure that lane's discovery recall. It does show its cost shape:
blindly escalating all investigative leads would dominate the declared model
envelope.

### 10.5 Latency and memory

Three-run medians:

| Scale | Fixture/index build | Detect and mandatory-rank | Post-detection evaluation | Total |
|---|---:|---:|---:|---:|
| Atomic | 5.065 ms | 1.331 ms | 0.091 ms | 6.465 ms |
| Medium | 19.613 ms | 0.948 ms | 0.176 ms | 20.738 ms |
| High | 86.707 ms | 3.437 ms | 0.381 ms | 90.647 ms |

Representative typed-array and RSS observations:

| Scale | Typed-array bytes | Maximum observed RSS |
|---|---:|---:|
| Atomic | 1,587,252 | 51,564,544 |
| Medium | 13,658,596 | 66,908,160 |
| High | 50,238,256 | 121,651,200 |

These are in-memory integer-array measurements on one machine. They exclude
real strings, provenance payloads, filesystem IO, hashing, parsers, archives,
persistence, checkpoints, documentation, and model calls. They are not
production budgets or SLAs.

## 11. Interpretation

### F1 — Causal local indexes avoid an all-pairs model stage for the tested shapes

Every supported synthetic case was reached through a path/provider lookup,
override/reversion state, typed consumer, declared dependency, qualified
component relation, or patch state. No detector traversed a dense mod-pair
matrix.

Confidence is bounded because the fixture states are deliberately normalized
and authored alongside the detector. Real adapter and independent-fixture
evaluation remain required.

### F2 — Negative and unsupported outcomes need first-class artifacts

All matched negatives now appear explicitly rather than disappearing because
the problem state is absent. All unsupported cases become gaps. This supports
inspectability and honest coverage denominators.

### F3 — Lane membership and ranking are separate contracts

Score perturbation changed mandatory ordering without changing membership.
This is the behavior the product should preserve. Budget exhaustion may leave
mandatory work unprocessed, but it must remain visible rather than be silently
filtered.

### F4 — Pair-population reduction is meaningful only with real participants

The corrected comparison uses actual generated input participant IDs and
canonical pair keys. Random or synthetic pair labels unrelated to detector
input are invalid. Even the corrected metric describes population size, not
semantic completeness.

### F5 — Broad indexed leads can still be impractical

At high scale, 8,600 investigative leads create an 11.395-million-token
optional envelope under the declared assumptions, compared with 212,000 for
the mandatory lane. “Not all-pairs” is necessary but insufficient; broad
rules need marginal-recall evidence, sampling, corroboration, or user-selected
budgets.

### F6 — The experiment does not evaluate findings

It evaluates candidate disposition, canonical participants, queue membership,
and gaps. It does not ask a model to decide harm, severity, confidence,
symptoms, cases, or resolutions. Investigative distractors are not finding
false positives.

### F7 — Taxonomy remains routing and coverage metadata

The eight families span records, topology, assets, scripts, configuration,
runtime-generated output, native components, and patch state. Proposed
taxonomy strata may expose missing coverage and prevent NPC-only tuning, but
exact joins—not labels—produce candidates.

### F8 — Construction smoke and recall must remain distinct

Construction smoke proves only that the generator encoded its own cases.
Truth-separated evaluation is stronger because detection does not receive
expected outputs, but it still shares authorship and implementation. EVAL-0032
must add independently authored fixtures and an independent implementation
boundary before product acceptance.

## 12. Alternatives and stop rules

| Alternative | Disposition |
|---|---|
| Naïve all-mod-pairs model comparison | Reject: quadratic scope, weak provenance, asks the model to reconstruct local state |
| All plugin pairs locally, then model | Reject: ignores actual shared records/references and still overgenerates |
| Surface-specific indexes plus typed graph | Recommend as logical contract |
| Global score as coverage cutoff | Reject by design; score may order admitted work |
| Mandatory supported-rule lanes | Recommend, subject to explicit configuration and coverage accounting |
| Send every investigative lead to a model | Reject by default; measured optional envelope dominates |
| Taxonomy pair routing as truth | Reject; retain only as prior/coverage stratum |
| Mod-name/known-patch allowlist | Reject as hidden fixture-specific rule |
| Hard top-k reported as clean | Reject; preserve visible queued/unprocessed counts |

Reject or revise a generator when:

- its eligible population cannot be declared;
- it requires all-pairs or whole-profile model discovery;
- a supported case is missed without a visible gap;
- matched negatives escalate unacceptably;
- unsupported states are silently treated as clean;
- it relies on fixture/mod names or IDs;
- it consumes unqualified semantics;
- candidate dependencies cannot be expressed; or
- volume/cost exceeds an accepted budget without demonstrated recall benefit.

Reopen this recommendation when:

- real adapter measurements contradict the synthetic shape;
- an independently authored fixture exposes misses;
- a new surface lacks an efficient causal index;
- causal grouping produces false merges/splits;
- a future accepted taxonomy revision changes coverage needs;
- provider batching/context economics materially change; or
- storage/query architecture cannot preserve this logical contract.

## 13. Accepted EVAL-0032 specification refinement

EVAL-0032 is specified as the reviewed matrix below. Independent fixtures and
execution remain pending.

### 13.1 Required profiles

1. independently specified atomic single-rule fixtures;
2. small multi-surface integration profile;
3. medium and high synthetic profiles;
4. upper-bound stress profile;
5. controlled-real EVAL-0016/EVAL-0017 replacements after qualification; and
6. private creator profile for non-oracle shape/scale only.

### 13.2 Required strata

- record scope reversion;
- placed-reference topology;
- typed asset target;
- script/API/VMAD;
- named configuration;
- runtime-generated output;
- native component relation;
- patch effect/applicability;
- applicable documentation;
- cross-layer case;
- matched negative for every positive;
- malformed, unknown, and unsupported;
- broad distractor;
- ambiguous intent;
- rename/reorder metamorphic case; and
- relevant-winner invalidation.

### 13.3 Required metrics

For every rule, lane, configuration, and accepted taxonomy stratum:

- eligible truth population and independent-or-author-coupled fixture status;
- construction-smoke result kept separate from recall;
- candidate-generation/disposition recall;
- deterministic-local and mandatory-queue recall;
- matched-negative resolution, escalation, and finding rate;
- unsupported-gap recall;
- events, causal bundles, and canonical participant pairs;
- distractor/irrelevant-candidate rate;
- duplicate grouping and false merge/split adjudication;
- latency, IO, memory, disk, checkpoint, and restart cost;
- model events, calls, tokens, and authorized actual cost;
- reason selected and provenance completeness;
- unprocessed/limited count; and
- clean/incremental equivalence.

### 13.4 Minimum assertions

- detection cannot consume expected class/disposition;
- independently authored expectations exist for acceptance fixtures;
- all enabled supported cases enter their required deterministic or mandatory
  lane;
- score changes reorder but do not alter mandatory membership;
- every matched negative has an explicit resolved or escalated record;
- unsupported cases become visible gaps;
- every participant pair derives from actual canonical input identities;
- pair arithmetic is exact and is not mislabeled as bundle or recall evidence;
- rename/unrelated insertion does not change causal results;
- relevant dependency changes invalidate only affected candidates;
- no model request receives whole-profile raw state;
- budget exhaustion leaves visible unprocessed work; and
- no all-pairs model loop or implicit dense pair matrix is used.

The current probe passes only the bounded author-coupled structural portion.
It does not pass EVAL-0032 as a product case.

## 14. Recommendation and confidence

### Recommended answer

Carry the logical index, typed graph, candidate provenance, and lane contracts
into M1 architecture comparison. Admit rule families only through explicit
analyzer declarations and independently specified evaluation fixtures. Keep
scores inside lanes and keep broad neighborhoods in a separately budgeted,
measured investigative path.

### Confidence

- **High** that candidate disposition, negative resolution, unsupported gaps,
  and ranking order need separate explicit contracts.
- **High** that pair-reduction claims require canonical actual participants.
- **Medium-high** that surface-specific indexes and typed causal joins are the
  correct logical design for the studied surfaces.
- **Medium** that the normalized synthetic shapes can be processed without a
  quadratic pair matrix at OPS-004-like headline populations.
- **Medium-low** in the measured recall as evidence beyond this exact
  author-coupled generator/detector.
- **Low/unsupported** for production capacity, real-mod recall, model quality,
  causal grouping accuracy, accepted cost, database choice, or a complete
  Skyrim interaction taxonomy.

### Preconditions before implementation

- accepted RQ-036 taxonomy/product integration (now satisfied);
- accepted M1 analyzer/rule declarations;
- exact evidence/candidate schemas;
- EVAL-0051/EVAL-0052 qualification for consumed providers, fields, links,
  and parsers;
- the accepted EVAL-0032 specification plus independently authored fixtures
  before execution;
- RQ-027 architecture-scale IO/storage/checkpoint budgets;
- RQ-033 causal grouping design;
- RQ-034 billable-work budget/deadline enforcement;
- architecture/security selection for persistence and query boundaries; and
- an accepted M1 milestone plan.

## 15. Downstream work enabled and disposition

Following acceptance:

1. RQ-035 is resolved for M0 at logical-design level, with product validation
   pending;
2. EVAL-0032 incorporates section 13 and awaits independent execution;
3. pass the logical index/query contract and durable artifacts to RQ-027;
4. require RQ-033 to evaluate causal grouping separately from pair counting;
5. require RQ-034 to budget mandatory and investigative lanes explicitly;
6. link accepted RQ-036 strata only to coverage/routing, not truth;
7. create analyzer/rule/candidate schemas in the M1 plan; and
8. create an ADR only when architecture selects durable storage/query
   mechanisms.

## 16. Accepted RQ-035 disposition

> **Resolved for M0 at logical-design level.** RESEARCH-0022 defines snapshot-bound
> surface indexes, a typed interaction graph, causal candidate rules, explicit
> negative/gap handling, canonical participants, and score-independent
> mandatory lanes. A durable three-scale author-coupled synthetic benchmark
> passed its truth-separated structural checks without an all-pairs matrix.
> Independent fixtures, real adapters, causal grouping, persistence,
> checkpoints, and architecture selection remain pending.

## 17. Traceability

| Requirement/decision | Corrected evidence or proposal | Residual work |
|---|---|---|
| ADR-0001 | Deterministic indexes, explicit negatives/gaps, bounded semantic lanes | Implementation and semantic evaluation |
| ADR-0002 | Candidate contract binds snapshot/context/run | Durable schema |
| ADR-0008 | Provider/order indexes consume authoritative snapshot input | EVAL-0051 |
| ADR-0009 | Rules require qualified fields, links, symbols, and archives | EVAL-0052 |
| ADR-0010 | Candidate contract retains validity dependencies | Cache/invalidation implementation |
| EVID-005 | Investigative leads require specific indexed local evidence | Semantic maturity policy |
| ANALYSIS-017 | Canonical high-run pair scope is 9,339 of 1,999,000 possible pairs; mandatory model lane is 160 events | Independent production benchmark |
| OPS-004 | High manifest has 2,000 mods, 2,500 plugins, 2M paths, and 3M providers | Real IO/storage/checkpoint budgets |
| EVAL-0032 | Separate truth manifest, post-detection evaluation, explicit negatives, gaps, canonical pairs, score perturbation | Independent fixtures and implementation |
| RESEARCH-0020 | Real leads are not treated as oracles; EVAL-0016 incomplete and EVAL-0017 unselected | Select/qualify replacements |
| RESEARCH-0021 | Accepted taxonomy used only as coverage/routing metadata | Versioned implementation pending |
| Anti-overfitting | Generic families, multiple surfaces/scales/seeds, no real identities | Independent metamorphic and controlled-real evaluation |

## 18. Conclusion

Infinium should not ask a model whether every mod interacts with every other
mod. It should first answer bounded local questions:

```text
What wins this path or record?
  -> What qualified fact changed, reverted, or became absent?
     -> Which exact consumer or dependency is affected?
        -> Which applicable claim corroborates or contradicts it?
           -> Which bounded event still requires semantic interpretation?
```

The corrected experiment supports this decomposition as a logical and
algorithmic direction. It also narrows what can honestly be claimed: the
synthetic detector covered its explicit, author-coupled supported scope;
resolved every explicit matched negative; gapped every explicit unsupported
case; canonicalized actual participants; and kept mandatory membership
independent of score. It does not establish real-mod recall, finding accuracy,
grouping quality, production capacity, or architecture.
