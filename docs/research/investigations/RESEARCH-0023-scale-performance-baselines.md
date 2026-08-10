# RESEARCH-0023: Scale and performance baselines

Status: Completed
Disposition: method and rough-feasibility recommendation accepted;
exact baseline remains deferred

Date: 2026-07-25

Last reviewed: 2026-08-10
Researcher: Codex agent

Primary question: RQ-027

M0 wave: C

Decision enabled: accepted scale-measurement method, rough local-worker and
checkpoint feasibility bounds, provider-neutral LLM cost method, and
EVAL-0018/EVAL-0032 refinements

Acceptance note: The project owner accepted the method and rough-feasibility
disposition on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
Exact production budgets, architecture selection, and evaluation execution
remain deferred.

Current corpus note: RESEARCH-0035 subsequently qualified the exact
EVAL-0016/EVAL-0017 candidates. Any table below describing their prior
incomplete or unselected state is retained only as dated benchmark provenance.

## 1. Question and bounded answer

RQ-027 asks:

> What high-end time, memory, disk, and cost baselines are realistic on the
> creator's profile?

The bounded answer is:

1. The unchanged private reference profile establishes one useful creator
   **shape**, not a performance SLA or correctness oracle:
   - 1,793 enabled mod-list entries;
   - 2,280 enabled plugin-list entries;
   - 244,626 loose-provider file occurrences;
   - 223,819 case-insensitive unique relative paths;
   - 2,375 physical plugin-provider files;
   - 377 physical archive-provider files; and
   - 254,193,831,116 logical provider bytes.
2. A read-only metadata/relative-path pass over that population took 10.918 to
   33.437 seconds across the earlier optimized research probe and this
   investigation's simpler PowerShell preflight. The implementations and cache
   conditions differ, so this is an observed range rather than a calibrated
   product estimate.
3. The corrected durable
   [RQ-035 candidate design](RESEARCH-0022-candidate-index-and-ranking.md)
   avoids a dense mod-pair matrix for its declared synthetic shapes. At high
   scale it modeled 2,000 mods, 2,500 plugins, 2 million logical paths, 3
   million provider entries, 2,670,500 logical nodes, and 5,246,000 edges.
4. That high probe emitted 9,368 analyzed events involving 9,339 unique
   canonical participant pairs in its first repeat—0.467% of the 1,999,000
   mathematically possible pairs. This is a **population comparison**, not
   evidence that all meaningful real interactions were found.
5. The high probe's median fixture/index build, detection/ranking, and
   post-detection evaluation time was 90.647 ms. An isolated rerun of the full
   three-scale/nine-run durable probe took 597.989 ms wall time and peaked at
   173,563,904 bytes working set.
6. Those times start from normalized synthetic structures. They exclude real
   filesystem reconstruction, canonicalization, hashing, parsers, archives,
   documentation, durable production transactions, and model calls likely to
   dominate a real exhaustive scan.
7. A neutral ID-only checkpoint envelope corresponding to the corrected high
   typed arrays and 9,368 event rows occupied 53,449,115 bytes. Its median
   durable write was 238.008 ms and its median verified first-observed read was
   28.286 ms on this machine. This is a lower-bound representation
   measurement, not a database or serialization decision.
8. High-scale mandatory semantic work was 160 events, 40 assumed four-event
   calls, and a 212,000-token envelope under the RQ-035 assumptions. Blindly
   escalating all 8,600 optional investigative leads would add 2,150 calls and
   11,395,000 envelope tokens. Exhaustiveness must therefore broaden declared
   deterministic populations and explicit investigative lanes under visible
   budgets; it cannot mean “send every indexed lead.”
9. No dollar amount is defensible until a user selects a provider/model and
   Infinium captures a sourced, time-stamped price schedule plus actual or
   provider-qualified tokenization. This report defines the calculation
   without inventing current prices.

The RQ-027-specific measurements were not retained with complete replay
scripts, per-repeat logs, or sanitized result manifests. Independent review
could verify the durable RQ-035 inputs and the surviving final checkpoint
files, but could not recompute every RQ-027 timing, memory sample, private-shape
aggregate, or repeat range. Treat those values as **preliminary feasibility
observations**, not Gate C evidence, architecture-selection evidence, or a
baseline worth further precision before real adapters and a candidate
architecture exist.

The results make the logical candidate direction plausible at required
headline scale. They do **not** pass OPS-004, EVAL-0018, or EVAL-0032, select a
stack, establish production capacity, validate real-mod recall, or prove that
a full exhaustive scan will fit the proposed architecture budgets.

## 2. Governing requirements and decisions

This investigation is governed by:

- [OPS-004](../../product/requirements.md#ops-004--high-end-scale), which
  requires support for approximately 2,000 mods, 2,500 plugins, millions of
  file entries, large override graphs, and multi-hour exhaustive work;
- [SCAN-003](../../product/requirements.md#scan-003--pre-run-estimate) and
  [SCAN-004](../../product/requirements.md#scan-004--progressive-progress),
  which require time, cost, coverage, stage, and remaining-work estimates;
- [SCAN-005](../../product/requirements.md#scan-005--pause-cancel-checkpoint-resume),
  which defines pause, terminal cancellation, checkpoint reuse, and resume;
- [SCAN-008](../../product/requirements.md#scan-008--resource-defaults) and
  [SCAN-010](../../product/requirements.md#scan-010--calibrated-user-presets),
  which require conservative defaults and empirically measured presets;
- [AI-004](../../product/requirements.md#ai-004--cost-and-execution-limits),
  which requires estimates, finite reservations, live attribution, clean
  stopping, and explicit skipped work;
- [ANALYSIS-017](../../product/requirements.md#analysis-017--candidate-first-llm-escalation),
  which requires indexed candidate-first reduction and forbids default
  all-pairs model comparison;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md),
  which requires a quiescent, explicitly selected MO2 profile and
  version-pinned effective-state reconstruction;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md),
  which accepts structural manifests, scoped same-stream SHA-256, and typed
  dependency closures rather than unconditional whole-population hashing;
- [EVAL-0018](../../evaluation/case-catalog.md), which will test high-end
  progress, resume, and cost behavior;
- [EVAL-0032](../../evaluation/case-catalog.md), which will test indexed
  candidate recall, volume, provenance, latency, IO, memory, disk, and model
  escalation; and
- the accepted
  [M0 Wave C plan](../../plans/milestones/m0/plan.md#wave-c--analysis-surfaces-taxonomy-corpus-and-candidate-scale).

The private `Brain Blast Destruction 2024` profile occupies only rung 5 of the
[evaluation profile ladder](../../evaluation/evaluation-strategy.md#profile-ladder).
It is not representative, correct, public, or a source of fixture-specific
production rules.

## 3. Scope and explicit non-scope

### In scope

- a fresh read-only private-profile shape and drift check;
- prior Wave B provider-manifest and scoped-hash measurements;
- the final corrected, durable RQ-035 three-scale results;
- an independently instrumented rerun of the exact corrected probe;
- a neutral compact-index plus ID-only event-checkpoint sizing probe;
- elapsed time, IO populations, peak working set, checkpoint bytes, event
  volume, and provider-neutral model workload;
- first-observed and immediate-repeat reads without claiming controlled cold
  cache;
- proposed architecture calibration targets;
- pause/cancel/checkpoint and progress implications; and
- EVAL-0018/EVAL-0032 changes enabled by the evidence.

### Out of scope

- production code or an accepted implementation plan;
- a database, graph store, file format, worker topology, IPC, UI, or desktop
  stack selection;
- a product minimum/recommended hardware specification;
- a release SLA;
- game-performance advice or automated in-game benchmarking;
- model-quality evaluation;
- authenticated or paid provider calls;
- current provider prices;
- archive-member enumeration or decompression;
- full plugin parsing and semantic qualification;
- documentation acquisition or extraction;
- a complete creator-profile interaction graph;
- a full 254.2-GB hash pass;
- a controlled operating-system cold-cache flush;
- long-term database/source-body/history growth; and
- treating synthetic recall as correctness on real mods.

## 4. Inputs, environment, and artifacts

### 4.1 Repository inputs

| Input | Authority for this investigation |
|---|---|
| [RESEARCH-0012](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md) | Accepted Wave B structural-pass and scoped-hash measurements |
| [RESEARCH-0013](RESEARCH-0013-wave-b-authoritative-local-state-integration.md) | Accepted authority/gap integration and non-representative private-profile rule |
| [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md) through [RESEARCH-0019](RESEARCH-0019-semantic-record-family-roadmap.md) | Proposed surface populations and typed candidate relationships |
| [RESEARCH-0020](RESEARCH-0020-evaluation-corpus-and-real-mod-candidates.md) | Proposed corpus controls; at this benchmark's capture time EVAL-0016 was incomplete and EVAL-0017 was unselected; RESEARCH-0035 later qualified both candidates |
| [RESEARCH-0021](RESEARCH-0021-skyrim-mod-impact-taxonomy.md) | Proposed classification/coverage strata only |
| [RESEARCH-0022](RESEARCH-0022-candidate-index-and-ranking.md) | Final dependency: corrected logical design, durable synthetic generator, truth-separated results, and limitations |
| [Wave B private manifest](WAVE-B-reference-environment-manifest.md) | Sanitized token mapping and exact profile-control fingerprints |

Final RESEARCH-0022 identity consumed:

- report SHA-256:
  `091E0EC95F3F9FD26466C276167C240606ACB9D295683EF281AE242E64250986`;
- probe SHA-256:
  `2F47F4D8EAF0CBA9AA399F14DA0C0A8502E9DB236AB2257430BE22DFA992538A`;
- configuration SHA-256:
  `C0DFFFB108A39C39BEC53786D5384BBAAF9749DCF7692972A1C7DFDC4E4C6393`;
- truth-manifest SHA-256:
  `633027784C5EE35347DD01E938F94C978CC244243F0B154436C1AB9A9BB4FA2B`;
  and
- reviewed result SHA-256:
  `9A87D8763A8938B74379BB1B9F20158178C49B7F7E37886057AD50CE5D78EDBE`.

The durable source, configuration, truth, result, and replay instructions live
under
[artifacts/RESEARCH-0022](artifacts/RESEARCH-0022/README.md).

### 4.2 Observed benchmark machine

| Item | Exact observed value |
|---|---|
| Date | 2026-07-25 |
| OS | Windows 11 Home `10.0.26200`, build `26200`, x64 |
| CPU | AMD Ryzen 9 7950X3D, 16 cores / 32 logical processors |
| Visible physical memory | 32,625,680 KiB (31.1 GiB) |
| Primary benchmark volume | Local `Z:` NTFS, 4 KiB allocation unit |
| Physical device | Crucial `CT4000P3PSSD8`, NVMe |
| Free `Z:` space at preflight | 72,605,421,568 bytes |
| PowerShell | `7.6.3` |
| .NET SDK / runtime | `10.0.302` / Microsoft.NETCore.App `10.0.10` |
| Node.js used by RQ-035 and the RQ-027 rerun | `v24.11.1`, x64 |
| Python | `3.14.3` |

The checkpoint probes ran under the operating-system temporary directory on
local NTFS. Cache state, antivirus, background IO, thermal state, power
policy, and free-space effects were not controlled.

### 4.3 RQ-027 disposable artifacts

| Schema/artifact | Location token | Purpose |
|---|---|---|
| `infinium.rq027.private-shape-preflight/1` | In-memory output only | Sanitized current shape and drift check |
| `infinium.rq027.instrumented-rerun/2` | `<OS-TEMP>\infinium-rq027-instrumented-v2` | Hash-verified copy of corrected durable artifacts and process-level rerun measurement |
| `infinium.rq027.storage-envelope-benchmark/4` | `<OS-TEMP>\infinium-rq027-storage-v2` | Deterministic compact-index and ID-only event-checkpoint envelopes |

These are disposable research artifacts, not production prototypes. The
in-memory private result retained aggregates only. No private path, mod name,
record, source content, or model request entered the repository or provider
context.

The RQ-027 wrapper, private aggregate record, per-repeat timing logs, and
storage-probe script/result were not durably preserved. Hashes of surviving
final checkpoint files cannot reconstruct the missing measurement history.
Accordingly, the RQ-027-specific figures below are useful for rough sizing only
and must be remeasured from retained tooling after authoritative adapters and a
candidate physical architecture exist.

## 5. Experiment procedures

### 5.1 Private-profile preflight and shape pass

The investigation:

1. confirmed that MO2 was not running;
2. resolved the already documented private token mapping locally;
3. SHA-256 checked `modlist.txt`, `plugins.txt`, `loadorder.txt`,
   `archives.txt`, `settings.ini`, and `lockedorder.txt` against the Wave B
   manifest;
4. counted enabled mod and plugin entries;
5. enumerated enabled physical mod directories plus `overwrite` without
   following reparse points;
6. counted directories, provider occurrences, case-insensitive unique paths,
   logical bytes, plugins, archives, small files, and loose PEX files; and
7. repeated all six control-file hashes.

All controls matched before and after. MO2 remained closed. The pass opened no
file for writing and launched no modding/game executable.

### 5.2 Corrected candidate-probe rerun

After final RESEARCH-0022 became available:

1. all durable RQ-035 artifacts were copied to a separate OS-temp directory;
2. script/configuration SHA-256 were required to match the report;
3. `prepare` regenerated the copy's truth manifest and was required to produce
   the reviewed truth hash;
4. `run` executed with `node --expose-gc`;
5. the wrapper sampled process working set and private bytes and retained
   wall/CPU time;
6. all writes remained within the disposable copy; and
7. the tracked durable artifacts were left untouched.

The candidate generator used `xorshift32`, base seed hexadecimal
`0x5EEDC0DE` (decimal `1592639710`), and:

```text
(baseSeed + scaleIndex * 0x10000 + repeatIndex) >>> 0
```

| Scale | Three candidate seeds |
|---|---|
| Small/`Atomic` | `1592639710`, `1592639711`, `1592639712` |
| Intermediate/`Medium` | `1592705246`, `1592705247`, `1592705248` |
| High | `1592770782`, `1592770783`, `1592770784` |

The durable truth manifest contains eight rule families, equal supported,
matched-negative, and unsupported outcomes, and three repeats at each scale:
3,456 truth cases total. The RQ-027 checkpoint envelope separately used
decimal seed `1592639710` for deterministic non-zero payload bytes and emitted
an identical aggregate artifact hash for each of its three same-scale
repetitions.

Timing, RSS, result generation time, and the generated result hash may differ
from the reviewed run. Structural outcomes and arithmetic are the replay
contract.

### 5.3 Checkpoint-envelope sizing

For each corrected RQ-035 scale, the neutral storage probe:

1. used the exact retained typed-array byte count reported by RQ-035;
2. wrote that many deterministic non-zero bytes to `typed-index.bin`;
3. wrote one ID-only row per analyzed event;
4. included candidate/run/analyzer/rule identity, family, lane, score, focal
   subject, canonical participants, rationale IDs, evidence IDs, dependency
   IDs, population, and state;
5. wrote a small manifest;
6. flushed each file;
7. read and SHA-256 verified the three-file envelope twice;
8. repeated the complete write/read procedure three times; and
9. required identical bytes and aggregate SHA-256 across repeats.

The rows do not contain evidence bodies, paths, prose, parser objects,
database indexes, transaction logs, source documents, or LLM payloads. The
size is a lower-bound checkpoint envelope, not a production format.

## 6. Scale ladder and observed populations

The RQ-035 label `Atomic` means its smallest **scale probe**. It contains 200
mods and 50,000 logical paths and is not an atomic correctness fixture under
the evaluation guidelines.

| Population | Small/`Atomic` probe | Intermediate/`Medium` | High synthetic stress | Private creator shape |
|---|---:|---:|---:|---:|
| Mods / enabled entries | 200 | 800 | 2,000 | 1,793 |
| Plugins / enabled entries | 300 | 1,200 | 2,500 | 2,280 |
| Logical/unique paths | 50,000 | 500,000 | 2,000,000 | 223,819 loose physical unique paths |
| Provider entries | 75,000 | 750,000 | 3,000,000 | 244,626 loose provider occurrences |
| Typed reference edges | 40,000 | 300,000 | 1,000,000 | Not reconstructed here |
| Records | 20,000 | 150,000 | 500,000 | Not parsed here |
| Script definitions | 2,000 | 20,000 | 80,000 | 7,706 loose `.pex` provider files by extension; effective paths/archives not resolved |
| Modeled logical nodes | 74,800 | 693,500 | 2,670,500 | Not built |
| Modeled logical edges | 161,300 | 1,411,500 | 5,246,000 | Not built |

The high synthetic scale deliberately exceeds the private loose-file
population while matching its order of magnitude for mod/plugin counts. This
pairs one non-representative real shape with deterministic scaling rather than
extrapolating from either alone.

## 7. Measured results

### 7.1 Private real-shape IO population

| Observation | Fresh RQ-027 result |
|---|---:|
| Enabled physical mod directories plus overwrite | 1,792 roots |
| Directories visited | 28,971 |
| Provider-file occurrences | 244,626 |
| Case-insensitive unique relative paths | 223,819 |
| Aggregate logical bytes | 254,193,831,116 |
| Plugin-provider files | 2,375 |
| Archive-provider files | 377 |
| Files at most 1 MiB | 218,762 |
| Loose `.pex` provider files by extension | 7,706 |
| Access errors / reparse entries | 0 / 0 |
| Elapsed | 33,436.711 ms |
| Peak PowerShell process working set | 197,193,728 bytes |
| Profile-control drift | None |

RESEARCH-0012's different in-memory C# metadata pass observed the same roots,
directories, file count, and bytes in 20.354 seconds; a final reproduction
reported 10.918 seconds. Its scoped hashing measured:

- 2,375 plugins / 495,622,977 bytes in 17.316 seconds;
- seven archives / 5,049,988,821 bytes in 5.608 seconds;
- 20,000 small loose files / 237,853,422 bytes in 88.082 seconds; and
- 7,310 medium loose files / 2,147,641,545 bytes in 64.482 seconds.

Entry/open count is material and bulk sequential bytes alone are a poor time
predictor. The sample rates must not be multiplied into a full-scan estimate.

### 7.2 Corrected candidate event results

RQ-035 three-run medians:

| Scale | Fixture/index build | Detect and mandatory-rank | Post-detection evaluation | Total |
|---|---:|---:|---:|---:|
| Small/`Atomic` | 5.065 ms | 1.331 ms | 0.091 ms | 6.465 ms |
| Intermediate/`Medium` | 19.613 ms | 0.948 ms | 0.176 ms | 20.738 ms |
| High | 86.707 ms | 3.437 ms | 0.381 ms | 90.647 ms |

First-repeat event volume:

| Disposition | Small/`Atomic` | Intermediate/`Medium` | High |
|---|---:|---:|---:|
| Deterministic local | 12 | 36 | 96 |
| Mandatory semantic | 20 | 60 | 160 |
| Resolved negative | 32 | 96 | 256 |
| Gap | 32 | 96 | 256 |
| Investigative lead | 220 | 1,900 | 8,600 |
| Total analyzed events | 316 | 2,188 | 9,368 |

Across all nine runs, the truth-separated evaluator observed:

- 1,152/1,152 supported cases in the configured deterministic or mandatory
  lane with correct canonical participants;
- 1,152/1,152 matched negatives explicitly resolved and zero escalated;
- 1,152/1,152 unsupported cases explicitly gapped; and
- 720/720 mandatory cases present in the executed mandatory queue.

These are bounded author-coupled structural results. Detection did not receive
truth/expected outcomes, but the same research artifact authors truth,
construction, detector, and evaluator. They are not an independently authored
EVAL-0032 oracle or evidence of real-mod recall.

### 7.3 Canonical pair population

| Scale | Unique canonical pairs, repeat 0 | Range across repeats | All possible pairs | Population reduction |
|---|---:|---:|---:|---:|
| Small/`Atomic` | 313 | 312–314 | 19,900 | 98.427% |
| Intermediate/`Medium` | 2,178 | 2,178–2,182 | 319,600 | 99.319% |
| High | 9,339 | 9,339–9,355 | 1,999,000 | 99.533% |

Each event carries two actual range-checked participant mod IDs and uses a
canonical unordered pair. Resolved negatives, gaps, and leads are included in
the numerator. Pair reduction describes analyzed scope only; it is neither
event/bundle count nor recall evidence.

### 7.4 Exact full-probe rerun

| Metric | Instrumented RQ-027 result |
|---|---:|
| Exit code | 0 |
| Wall time | 597.989 ms |
| CPU time | 687.500 ms |
| Peak working set | 173,563,904 bytes |
| Peak private bytes | 158,466,048 bytes |
| Regenerated truth hash | `633027784C5EE35347DD01E938F94C978CC244243F0B154436C1AB9A9BB4FA2B` |
| Result artifact | 3,475,478 bytes |

The generated result hash differs from the reviewed result because timings,
RSS, and `generatedAt` are allowed replay differences. The run completed the
same structural assertions and workload arithmetic.

### 7.5 Compact index and checkpoint footprint

| Scale | Corrected typed-index bytes | Event rows / bytes | Total checkpoint bytes |
|---|---:|---:|---:|
| Small/`Atomic` | 1,587,252 | 316 / 106,331 | 1,693,832 |
| Intermediate/`Medium` | 13,658,596 | 2,188 / 746,198 | 14,405,045 |
| High | 50,238,256 | 9,368 / 3,210,610 | 53,449,115 |

Three-repeat durable-write and verified-read timings:

| Scale | Durable write median (range) | First-observed read/hash median (range) | Immediate-repeat read/hash median (range) |
|---|---:|---:|---:|
| Small/`Atomic` | 34.989 ms (20.431–55.101) | 1.408 ms (1.355–11.469) | 1.106 ms (1.065–1.131) |
| Intermediate/`Medium` | 67.036 ms (64.558–92.326) | 7.988 ms (7.966–8.193) | 7.297 ms (7.268–7.623) |
| High | 238.008 ms (236.090–271.813) | 28.286 ms (28.189–28.716) | 28.075 ms (27.662–28.129) |

Each scale produced identical bytes and one aggregate SHA-256 across its three
repetitions. “First-observed” does not mean controlled cold cache: Windows
cache state was not flushed.

### 7.6 Memory interpretation

| Scale | Corrected typed arrays | RQ-035 maximum observed RSS |
|---|---:|---:|
| Small/`Atomic` | 1,587,252 bytes | 55,762,944 bytes |
| Intermediate/`Medium` | 13,658,596 bytes | 83,255,296 bytes |
| High | 50,238,256 bytes | 165,064,704 bytes |

The exact full-probe rerun peaked higher at 173,563,904 bytes because it also
loaded durable truth/configuration and wrote the detailed result. The private
enumeration peaked at 197,193,728 bytes, illustrating that path strings and a
generic PowerShell `HashSet` can exceed the compact normalized graph.

None includes the full production evidence graph, parser lifetimes, source
bodies, database cache, UI, model payloads, or concurrent analyzers. The
evidence supports compact canonical IDs and stage-bounded object lifetimes; it
does not select a physical representation.

## 8. LLM escalation and cost method

### 8.1 Required work units

Keep these quantities separate:

1. **candidate event or causal bundle** — one evidence-scoped analytical unit;
2. **model request** — one bounded call containing one or more units;
3. **usage envelope** — finite reserved input/output/tool units for a request;
   and
4. **actual ledger entry** — the single-owned reconciled provider/local usage
   and cost.

One event must not be assumed to equal one call. Batching must preserve
per-event result/provenance and failure isolation.

### 8.2 Corrected RQ-035 workload envelope

The research-only workload assumed:

```text
batch_size = 4 events
fixed_input_per_call = 500 tokens
input_per_event = 900 tokens
maximum_output_per_event = 300 tokens

calls = ceil(events / batch_size)
input_envelope = events * 900 + calls * 500
output_cap = events * 300
total_envelope = input_envelope + output_cap
```

Representative results:

| Scale | Mandatory events / calls / tokens | Optional investigative events / calls / tokens | Combined envelope |
|---|---:|---:|---:|
| Small/`Atomic` | 20 / 5 / 26,500 | 220 / 55 / 291,500 | 318,000 |
| Intermediate/`Medium` | 60 / 15 / 79,500 | 1,900 / 475 / 2,517,500 | 2,597,000 |
| High | 160 / 40 / 212,000 | 8,600 / 2,150 / 11,395,000 | 11,607,000 |

The mandatory lane is score-independent; score perturbation changed ordering
but not membership in all nine runs. The probe plants no supported discovery
cases in the broad investigative population, so it cannot estimate that
lane's recall. It demonstrates only that blind escalation would dominate cost.

### 8.3 Provider-neutral monetary estimate

For provider/model price snapshot `P`, retrieved at time `t` from a registered
authoritative source:

```text
estimated_cost(P, t) =
    uncached_input_units * P.uncached_input_rate
  + cached_input_units   * P.cached_input_rate
  + output_units         * P.output_rate
  + request_count        * P.request_rate
  + tool_units           * P.tool_rate
  + other_declared_units * P.other_rate
```

The adapter must record:

- provider and exact model/snapshot identity;
- price source URL/API, currency, unit basis, retrieval time, and effective
  date when available;
- tokenizer/counting method and version;
- batch size, request count, prompt/schema/tool overhead, and per-event input;
- maximum output, retry bound, cache eligibility, rounding, and minimum
  charges;
- reservation amount;
- actual usage and billing-reconciliation delay; and
- any capability that prevents a finite hard-limit reservation.

Before exact tokenization exists, Infinium may show a labeled byte/item/call
planning envelope. It must not convert a guessed provider-neutral
bytes-to-token ratio into precise money.

No current price or dollar estimate appears in this report.

## 9. Progress, pause, cancellation, and checkpoint implications

### 9.1 Progress denominators

At scan, stage, analyzer, and lane levels retain:

- total known input population;
- enumerated, parsed, and indexed units;
- events generated;
- deterministic outcomes, resolved negatives, and gaps;
- mandatory work queued/completed/failed/unprocessed;
- investigative work queued/completed/limited;
- calls and usage reservations;
- actual usage/cost;
- checkpoint generation and age; and
- invalidated work.

The private pass shows that structural roots/files provide useful early
denominators. Candidate/lane totals become available after generation.
Remaining-time estimates should move from ranges to measured-rate projections
as these denominators become known.

### 9.2 Cooperative boundaries

Proposed calibration targets:

- filesystem/index loops observe pause/cancel between directories or bounded
  partitions, never only after the whole profile;
- in-memory graph work yields at least every 50,000 inputs or 250 ms,
  whichever comes first;
- candidate state checkpoints at analyzer/rule/lane boundaries and at most
  every 1,000 events;
- one provider request is an atomic external attempt; no new request dispatch
  occurs after applicable pause/cancel/limit/deadline state is observed;
- completed model attempts and ledger reconciliation persist before the next
  dependent batch; and
- uninterruptible parser/tool/provider work remains visible.

These are proposed calibration targets, not accepted timers.

### 9.3 Checkpoint identity

A resumable checkpoint must include:

- immutable run, snapshot, semantic-context, and effective
  scan-configuration identity;
- analyzer/rule/adapter/schema versions;
- completed partition/range identities;
- exact upstream dependency closures;
- deterministic event/lane ordering identity;
- pending, in-flight, failed, and limited work;
- cost reservations and actual ledger entries;
- artifact fingerprints; and
- coverage denominators.

Pause/resume continues the same run. Cancellation remains terminal.
Dependency-valid artifacts may seed a new manually initiated run; the
cancelled run itself is never reopened.

This is not the complete SCAN-005/AI-004 lifecycle contract. A later job and
provider plan must additionally define and evaluate:

- atomic worst-case reservation against every applicable shared limit before
  concurrent dispatch;
- a hard-deadline check immediately before each dispatch;
- node-scoped limit exhaustion with unaffected parent work allowed to continue;
- continuation after a hard limit only through a new run;
- retry behavior that distinguishes active runs from terminal runs;
- parent pause/cancel propagation to attached child work;
- explicit user-visible child continuation or detachment; and
- detached child progress, time, usage, and cost attribution.

These semantics are required by the accepted product contract but were not
measured or demonstrated by this preliminary benchmark.

## 10. Proposed architecture calibration budgets

These are conservative proposals for architecture comparison and later
prototype measurement. They are not SLAs, hardware minima, accepted
requirements, or permission to hide work that exceeds them.

| Dimension | Initial proposal | Evidence and interpretation |
|---|---|---|
| Private-shape structural capture | Target under 120 seconds on the reference NVMe, with progress before 1 second | Observed 10.918–33.437 seconds; leaves substantial implementation/provenance margin |
| Loaded-index candidate generation | Target under 5 seconds at the corrected RQ-035 high population | Observed 90.647-ms total synthetic stage; over 50x margin for richer general implementation |
| One active candidate worker | Target at or below 512 MiB steady memory; investigate/revise above 1 GiB | Observed 157.4 MiB maximum RQ-035 high RSS, 165.5 MiB full rerun peak, and 188 MiB private PowerShell peak |
| Candidate/index checkpoint | Target at or below 1 GiB per high run/snapshot layer, excluding source bodies, raw mod bytes, documentation, logs, and history | Measured 53.45-MB lower-bound envelope; approximately 20.1x representation margin |
| Durable checkpoint pause | Target under 5 seconds on the reference NVMe | Measured 238-ms median for 53.45 MB; production transactions will be richer |
| Checkpoint validation/read | Target under 2 seconds for the candidate/index layer | Measured 28-ms median for 53.45 MB |
| Cancellation/pause observation | Cooperative local stages should observe within 250 ms outside uninterruptible calls | Matches proposed bounded slices, not an external-call guarantee |
| Progress refresh | Internal progress at least once per second while measurable work advances; coalesce presentation to at most 10 Hz | Avoids apparent hangs and UI event flooding; UI architecture remains open |
| Initial model batching | Research envelope of 1–4 events/request until quality/failure-isolation data justifies more | RQ-035 used four; production batching/model quality untested |
| Investigative leads | Excluded from default mandatory queue; explicit population, marginal-recall evidence, and hard limit required | High optional envelope is 11.395M tokens versus 212k mandatory |

If a prototype misses a proposal, respond with measurement and revision,
narrower concurrency, streaming/spilling, or a documented coverage tradeoff—
not silent suppression or false “complete” status.

## 11. Architecture implications without architecture selection

The selected architecture should support:

- compact integer identities for high-volume joins while retaining expandable
  provenance separately;
- stage/partition streaming instead of retaining every parser/source object;
- durable immutable run artifacts and append/reconcile cost ledgers;
- bounded forward/reverse index queries;
- independent worker cancellation and crash isolation;
- incremental checkpoint writes rather than whole-run serialization;
- paged candidate/case access for the UI;
- typed dependency invalidation under ADR-0010;
- separate source-body/document/history budgets; and
- background-safe concurrency controls.

The evidence does not decide among SQLite, another embedded store, compact
binary artifacts, memory mapping, a graph database, or a hybrid. Wave E must
compare physical options against these measurements and missing production
layers.

## 12. EVAL-0018 implications

EVAL-0018 should become a matrix over:

1. private creator-profile shape;
2. deterministic high synthetic profile;
3. upper-bound stress beyond at least one OPS-004 dimension;
4. first run versus validated reuse;
5. foreground and default-background resource configurations;
6. pause during enumeration, parsing, candidate generation, checkpointing,
   and model work;
7. same-run resume;
8. terminal cancellation and validated reuse into a new run;
9. worker/UI restart recovery;
10. one analyzer/parser/provider failure;
11. operation/run hard-limit exhaustion; and
12. changed-input invalidation after checkpoint.

Required metrics/assertions:

- completed and remaining denominators;
- elapsed and remaining-time estimate error over time;
- current, estimated, and actual cost without double counting;
- peak memory, CPU, and read/write bytes/operations when reliable;
- checkpoint size, time, frequency, and restart cost;
- progress/update latency;
- pause/cancel observation and uninterruptible variance;
- no new work after applicable authority ends;
- valid same-run resume and terminal cancellation semantics;
- explicit skipped, failed, and gapped work;
- clean/incremental semantic equivalence; and
- private-profile data kept private.

This investigation supplies baselines and proposed thresholds only.
EVAL-0018 remains planned and unpassed.

## 13. EVAL-0032 implications

Adopt RESEARCH-0022 section 13 and add:

- exact environment, seed, manifest, source, and result identities;
- independent-fixture status kept separate from construction smoke;
- separate generator/disposition, mandatory-queue, negative-resolution, and
  gap recall;
- events, future causal bundles, canonical pairs, and lane populations kept
  distinct;
- per-stage CPU/wall time and actual IO once persistence exists;
- peak resident/private memory and compact-index bytes;
- checkpoint bytes, durable write, validated read, resume, and invalidation;
- first-observed versus repeat-cache measurements without false cold-cache
  claims;
- model events, calls, measured tokens, reservations, actual cost, and
  unprocessed work;
- identical same-seed deterministic artifacts/order;
- score-independent mandatory membership;
- separately configured investigative leads with marginal recall/cost;
- provenance completeness for every event; and
- private creator shape plus synthetic upper-bound stress, never private
  correctness truth.

The current probes demonstrate only author-coupled structural algorithm shape,
rough memory, and a lower-bound checkpoint envelope. They do not pass the
reviewed product case.

## 14. Alternatives evaluated

| Alternative | Benefit | Failure/risk | Disposition |
|---|---|---|---|
| Naïve all-pairs LLM comparison | Simple description | 1,999,000 mod pairs before record/file/doc structure; unbounded cost and weak provenance | Reject |
| Global score as coverage cutoff | Predictable queue | Conflates ranking with enabled-rule coverage | Reject; score only within admitted lanes |
| Send every indexed lead | Broad exploration | High optional envelope 11.395M tokens; discovery recall unmeasured | Explicit opt-in research lane only |
| Mandatory supported lanes plus within-lane score | Preserves declared coverage and allows scheduling | Requires explicit denominators and budgets | Recommend |
| Hash every provider byte on every run | Strong whole-population byte identity | Small-file open cost and 254.2-GB population are unnecessarily broad | Reject as default under ADR-0010 |
| Copy/materialize effective tree | Frozen input view | Hundreds of GB, duplicate IO, losing-provider context loss | Reject |
| Keep every rich graph/parser/source object in RAM | Simple traversal | Unknown amplification and poor background behavior | Reject as assumption; compare bounded physical designs |
| Persist only final findings | Minimal disk | Cannot resume, audit selection, invalidate dependencies, or expose coverage | Reject |
| Treat 53.45 MB as production checkpoint size | Simple budget | Omits rich provenance, strings, database indexes, transactions, source bodies, and history | Reject; lower bound only |
| Use current provider prices now | Immediate dollar figure | No selected provider/tokenizer; prices and models change | Reject |

## 15. Uncertainty, limitations, and contrary evidence

- The benchmark machine is one high-end desktop, not a minimum system.
- The `Z:` volume had limited free space; no controlled comparison volume ran.
- Real-profile counts cover physical enabled mod roots plus overwrite, not an
  authoritative effective VFS/archive-member reconstruction.
- The `.pex` extension count differs from RESEARCH-0017's narrower
  `Scripts/*.pex` population and is not a semantic script count.
- The private profile is smaller than the synthetic high scale in loose
  path/provider counts and cannot validate million-entry behavior alone.
- Earlier and current private enumeration implementations differ; their times
  are not one statistical distribution.
- The candidate probe constructs normalized arrays directly and does not pay
  the cost of obtaining them from Skyrim/MO2 bytes.
- Truth is separated from detector inputs but remains author-coupled to the
  same research artifact. Recall is not independent product validation.
- The broad lead lane contains no planted discovery positives and supplies no
  marginal-recall estimate.
- Pair reduction is a population comparison, not completeness evidence.
- The checkpoint is an ID-only lower bound and omits production storage
  amplification.
- Storage used ordinary NTFS files and flushes, not a database transaction or
  WAL.
- No controlled cold-cache experiment ran.
- No live model was called. Tokens are declared assumptions.
- No current price is included.
- Retries, refusals, schema failures, batching-quality loss, prompt growth,
  citation payloads, and explanation synthesis remain unmeasured.
- Full documentation acquisition may dominate time and storage.
- Parallel analyzers can multiply memory/IO; concurrency is uncalibrated.
- Pause/cancel/checkpoint behavior has not survived a process crash.
- Source-body retention and indefinite history require separate measured
  storage policy; the 1-GiB proposal excludes them.

The most important contrary results are:

- broad indexed leads can dominate model cost even when the algorithm avoids
  all pairs;
- compact candidate joins are so fast that upstream reconstruction,
  documentation, persistence, and model work—not the join itself—are likely
  to determine end-to-end duration; and
- the synthetic result cannot answer how often broad leads discover real,
  undocumented problems.

## 16. Recommendation and confidence

### Recommended answer

Use the RQ-035 surface-specific indexes, typed interaction graph, explicit
negative/gap outcomes, canonical participants, and score-independent mandatory
lanes as logical M1 architecture input. Carry the section 10 budgets into Wave
E architecture comparison and prototype measurement without accepting them as
SLAs.

For development:

- expose granular analyzers, lanes, and exact populations;
- treat broad investigative expansion as separately budgeted;
- retain checkpoint/progress/cost units from the first backend proof;
- record actual measurements per machine/run;
- keep deterministic state work local and use models only for bounded semantic
  interpretation or explanation;
- show event/call/token envelopes before provider selection;
- calculate money only from a sourced, time-stamped selected-model schedule;
  and
- calibrate user presets only after EVAL-0018/EVAL-0032 prototype data exists.

### Confidence

- **High** in the observed private shape and no-drift result.
- **High** that pair-population claims require canonical actual participants.
- **High** that mandatory supported-rule lanes must remain independent of
  ranking score.
- **High** that broad lead escalation needs explicit marginal-recall and cost
  control.
- **Medium-high** that a compact candidate/index worker is feasible within the
  proposed memory/disk targets on the reference machine.
- **Medium** in the structural-capture target because implementations/cache
  conditions differ.
- **Medium-low** in the 512-MiB/1-GiB targets until production
  provenance/persistence/parser layers exist.
- **Medium-low** in the synthetic recall evidence beyond the exact
  author-coupled experiment.
- **Low/unsupported** for end-to-end exhaustive duration, real-mod recall,
  provider dollar cost, release SLA, or minimum hardware.

## 17. Follow-up and accepted disposition

The owner accepted the method and rough-feasibility disposition. RQ-027 and
the planning indexes now reflect that decision; the implementation and
calibration work below remains pending:

1. retain RQ-027 as answered for M0 at method/rough-feasibility level, with
   exact production/EVAL calibration deferred;
2. carry section 10 into RQ-016/RQ-017 architecture comparison;
3. make checkpoint/progress/ledger capability a hard architecture-comparison
   criterion;
4. refine EVAL-0018 and EVAL-0032 with sections 12 and 13;
5. require RQ-034 to budget mandatory and investigative lanes explicitly and
   expose unprocessed work;
6. require provider research to source/time-stamp prices and tokenizer
   behavior;
7. add database/source-body/history storage measurements after RQ-013 and
   physical store candidates exist;
8. rerun against the private profile only after authoritative adapters exist;
9. add an upper-bound stress profile beyond at least one OPS-004 dimension;
   and
10. accept no performance ADR until architecture alternatives are measured
    against the same workload and checkpoint contract.

No new ADR is warranted from this report alone. It measures and proposes
budgets; it does not select the physical architecture that would make them
authoritative.

## 18. Accepted RQ-027 disposition

> **Researched at method and rough-feasibility level; exact baseline deferred.**
> RESEARCH-0023 records one private creator-profile shape, consumes the final
> corrected RQ-035 design, reports preliminary three-scale/checkpoint
> observations, and defines provider-neutral usage/cost calculation. Its
> RQ-027-specific measurement history is not durably replayable, so exact
> performance budgets must be remeasured after authoritative adapters and a
> candidate architecture exist. End-to-end production, upper-bound,
> persistence, independently authored candidate fixtures,
> authorized-provider, and EVAL-0018/EVAL-0032 calibration remain pending.

## 19. Requirements-and-evidence traceability

| Requirement / decision | Evidence or proposal | Residual work |
|---|---|---|
| OPS-004 | Private 1,793-mod/2,280-plugin shape; synthetic 2,000-mod/2,500-plugin/3M-provider/5.246M-edge scale | Authoritative adapter, upper-bound stress, end-to-end M3 pass |
| SCAN-003 | Provider-neutral time/event/call/token/cost method and uncertainty | Prototype rates and selected-provider capability |
| SCAN-004 | Known stage/lane denominators and progress proposal | Job implementation and UI evaluation |
| SCAN-005 | Preliminary partition/checkpoint/pause-resume-cancel implications only | Complete shared-limit, deadline, parent/child, detachment, retry, crash, and lifecycle EVAL-0018 contract |
| SCAN-008 | 512-MiB worker and cooperative background targets proposed | Multi-analyzer/concurrency calibration |
| SCAN-010 | Mandatory/optional workload evidence shows why presets need measurement | Model-quality and real-corpus data |
| AI-004 | Event/request/envelope/ledger separation and sourced-price formula only | Atomic multi-limit reservations, deadline enforcement, node-scoped exhaustion, detached attribution, RQ-034 enforcement, and provider adapters |
| ANALYSIS-017 | High analyzed scope 9,339 canonical pairs; mandatory model lane 160 events; no dense matrix | Independent production benchmark |
| ADR-0008 | Quiescent private profile, MO2 closed, controls stable | EVAL-0051 authoritative reconstruction |
| ADR-0010 | Structural population measured; no universal hash/copy assumption; typed checkpoint identity | Canonical implementation and invalidation tests |
| EVAL-0018 | Section 12 matrix and initial budgets | Proposed specification input; execution pending |
| EVAL-0032 | Corrected durable RQ-035 result plus section 13 IO/disk/checkpoint additions | Independent fixtures and execution |
| EVAL-0086 / RQ-036 | Multiple typed surfaces; taxonomy not causal authority | Taxonomy accepted; stratified case execution pending |
| Anti-overfitting | Deterministic seeds, matched negatives, gaps, no real-mod production rule | Independent fixtures and production review |

## 20. Conclusion

The current evidence supports this direction:

```text
quiescent profile and structural snapshot
  -> scoped parsing and compact surface indexes
  -> typed candidate events with canonical participants
  -> explicit deterministic, mandatory, negative, lead, and gap lanes
  -> separately budgeted semantic model work
  -> incremental durable checkpoints and progress
```

The creator's real profile makes local structural work substantial but
tractable on the reference machine. The corrected synthetic benchmark shows
that the candidate join need not allocate or traverse a dense mod-pair matrix.
The dominant risks move to the places the product must expose honestly:
upstream IO/parsing/documentation, independent semantic validation,
production provenance/storage, optional model volume, checkpoint lifecycle,
and unsupported coverage.

The next step is not to promise a scan duration or pick a database from these
numbers. It is to make each architecture candidate demonstrate the same
bounded workload, progress, pause/cancel, checkpoint, memory, disk, and
cost-accounting contract.
