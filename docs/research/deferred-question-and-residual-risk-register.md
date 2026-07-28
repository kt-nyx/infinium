# Deferred-question and residual-risk register

Status: Accepted  
Owner: Project owner  
Prepared: 2026-07-28  
Accepted: 2026-07-28  
Last reviewed: 2026-07-28  
Applies to: completed M0 Wave F and accepted M1 backend semantic proof

## Purpose

This register distinguishes:

- questions deliberately deferred because their evidence does not exist yet;
- unsupported capabilities outside M1;
- accepted design risks awaiting implementation qualification; and
- true blockers that prevent M1 planning or completion.

A recorded deferral is not implicit coverage, permission to guess, or a waiver
of the requirement before its stated delivery milestone.

## Deferred research questions

| RQ | Current M0 disposition | Required next evidence | Reopen/schedule point | M1 effect |
|---|---|---|---|---|
| RQ-028 | Later numerical evidence under the accepted Wave F calibration protocol | Per-analyzer development/validation/held-out results, false-positive/false-negative and abstention distributions, taxonomy-stratified coverage, carryover/reconciliation errors, stale/readiness presentation results, and user review evidence | Collect during M1; set M3 thresholds only in an accepted M3 readiness-policy/calibration decision | No numeric maturity/readiness threshold is invented for M1. Development output remains unfiltered; M1 proves measurement and policy plumbing |
| RQ-029 | Later evidence | Exact log producer/version inventory, stable fingerprints, capture-session boundaries, clock/fingerprint failure cases, privacy/retention behavior, and controlled exact/matched/likely/unknown/historical fixtures | Before any automatic log application; schedule in the M3 plan or earlier if M2/M1 scope changes | Runtime-log ingestion/application is excluded from M1 |
| RQ-030 | Later evidence | Final application artifacts, signing identity/business decision, installer/update alternatives, WebView2/runtime delivery behavior, rollback/recovery threat model, SBOM and GPL distribution closure | M4 packaging/update planning after M2 architecture qualification and before public packaging | No installer, updater, signing, or public distribution claim in M1 |
| RQ-038 | Resolved for M1 by accepted ADR-0025 | Implementation and evaluation evidence for the exact `gpt-5.6-sol` synchronous Responses profile and its non-date-pinned drift/requalification policy | Reopen only if the selected model/profile or material provider capability changes | Live M1 work may proceed only through the accepted profile and its credential, budget, provenance, and evaluation gates |

## Conditional research retained outside M1

| RQ/capability | Why excluded from M1 | Trigger |
|---|---|---|
| RQ-005 / LOOT-libloot | The first semantic proof does not need LOOT semantics; enabling it adds native binding, managed-data, userlist, fidelity, and non-mutation gates | Accepted later milestone slice plus EVAL-0053/EVAL-0046 |
| RQ-008 / Nexus acquisition | M1 uses synthetic/local retained documentation and does not need live Nexus retrieval | Any M1 amendment that claims Nexus-backed acquisition |
| RQ-019 / root-native analyzer | Valuable for M3 breadth but not needed for the two semantic proofs | Named analyzer plan with exact static shapes |
| RQ-020 / generated output | No complete generic manifest contract exists | Version-pinned named generator selection |
| RQ-021 / configuration ecosystems | Lower priority and semantically distinct | Named schema/DSL slice |
| RQ-022 / PEX/VMAD | Static structural support is bounded but not required by the first proofs | M1 amendment or later analyzer plan |
| RQ-023 archive/NIF breadth | Loose-only FaceGen boundary is sufficient for EVAL-0016; archive parity and a production NIF parser remain unselected | Any archive-positive or NIF-reference coverage claim |
| RQ-027 production budgets | M0 establishes feasibility, not production thresholds | Architecture prototype and representative measured runs |

## Accepted-design qualification risks

| Risk | Current evidence | M1 control/gate | Residual after M1 |
|---|---|---|---|
| SQLite/CAS crash consistency and native-version drift | ADR-0015 selects the mechanism; no implementation exists | EVAL-0087 plus exact loaded SQLite identity and fault injection | Longer-duration corruption/scale evidence remains M3 |
| Custom lifecycle correctness | ADR-0016 defines semantics; Dapr was rejected without prototype | State-machine/property tests, crash/fence tests, EVAL-0038/EVAL-0088 where exercised | Tuning and multi-hour behavior remain M3 |
| Same-user worker compromise | Job Objects contain lifetime/resources but are not a sandbox | Keep M1 parsers managed and positively allowlisted; exclude any operation needing compromise containment | Stronger isolation requires new research/ADR |
| Named-pipe caller/role confusion | ADR-0019 specifies restricted role-separated contracts | EVAL-0088 malformed/race/reconnect/nonce/limit suite | Public hostile-local-user hardening remains bounded by Windows same-user model |
| Credential half-commit or revocation race | ADR-0020 defines generation/intents/helper | EVAL-0089 and secret-canary review before live call | Same-user malware/admin/debugger protection is not claimed |
| Provider cost/billing ambiguity | ADR-0023 defines conservative reservation/holds | EVAL-0081 synchronous path; the qualification and each semantic call are separately authorized/reserved/settled; unresolved usage retains its hold | Background/Batch/cache/concurrent modes remain disabled |
| LLM semantic variability | Accepted schema-bound OpenAI path is not deterministic | Exact retained request/response, typed semantic assertions, matched negatives, held-out replacement discipline | No general model reliability claim beyond evaluated prompts/model |
| Non-date-pinned OpenAI model drift | OpenAI currently lists only the moving `gpt-5.6-sol` snapshot identity | Accepted ADR-0025 requires retained-result replay, returned-model/capability fingerprints, and invalidation/requalification on material drift | Identical live re-execution cannot be promised; only the retained original result is exactly replayable |
| Controlled-real fixture availability | Exact private source identities exist but redistribution is not permitted | Hash/pin verification and private acquisition manifest; synthetic equivalents remain primary | Public one-command corpus unavailable |
| Mutagen defects/circular truth | Mutagen is selected; xEdit excluded | Independent byte/structure expectations and EVAL-0052 | Support remains only for positively qualified shapes |
| MO2 reconstruction drift | Version-pinned research exists; no adapter | Disposable MO2 `2.5.2` fixtures and EVAL-0051 | Other versions/managers remain unsupported |
| WPF/WebView2 architecture | Accepted but not needed by CLI-first M1 | No M1 graphical claim | M2 qualification may reopen ADR-0017 |

## Unsupported M1 capability register

M1 must report these as unsupported or not configured rather than silently
omitting them:

- graphical desktop workflow;
- LOOT/libloot findings and automatic managed-data implementation;
- live Nexus acquisition and unsupported Nexus content surfaces;
- hosted broader web search;
- OpenAI background mode, Batch, explicit provider caching, and concurrent
  live billable dispatch;
- providers other than OpenAI;
- OpenAI models, reasoning profiles, service tiers, and model routing other
  than the exact accepted M1 baseline;
- ChatGPT/Codex-plan access;
- archive-positive FaceGen and general archive-member semantic coverage beyond
  positively qualified low-level reads;
- production NIF-reference analysis;
- PEX/VMAD, generated-output, named configuration, native/root, lifecycle,
  performance, and runtime-log analyzers;
- readiness maturity thresholds suitable for M3;
- high-end creator-profile and upper-bound scale;
- WPF/WebView2 UI, user export workflow, installer, updater, signing, and
  public diagnostic bundles.

## Source, licensing, and privacy residuals

- Nexus API use continues under ADR-0005/ADR-0012's owner risk decision; a
  negative Nexus response or material policy change reopens the affected path.
- Controlled-real mod/plugin/assets remain private evaluator inputs. Hashes,
  public source IDs, structural expectations, and permitted claims may be
  tracked; bytes are not redistributed without permission.
- The exact `GPL-3.0-only` versus `GPL-3.0-or-later` selector remains deferred
  until an operative license or distribution requires it. M1 code remains
  within the accepted GPLv3-family posture.
- Exact dependency and transitive-license review is required before any public
  payload is distributed.
- Live provider evaluation receives a user-supplied test credential at runtime;
  no credential or unnecessary private profile/path data enters tracked
  fixtures or diagnostics.

## M1 authorization conditions

The owner accepted the M1 plan after the Wave F review established these
conditions at the documentation/design level:

1. the M1 evaluation baseline and both specification/manifest sets must be
   reviewed and accepted;
2. the M1 milestone plan must be reviewed and accepted;
3. ADR-0025 or an accepted replacement must select the exact live OpenAI
   profile;
4. every M1 implementation dependency must fit accepted ADRs and GPL posture;
5. no unresolved requirement-to-case traceability gap may remain; and
6. any material change to the exact M1 scope must update this register,
   evaluation baseline, and plan before implementation.

Conditions 1 through 5 are satisfied for the accepted plan. Condition 6 is an
ongoing change-control rule. Implementation preflight must still verify the
actual dependency locks, fixture availability appropriate to the first slice,
clean worktree, and security/runtime assumptions; document acceptance does not
manufacture those execution facts.

## M1 completion blockers

- any required case lacks an accepted specification or passing retained run;
- effective-state or record truth depends circularly on the implementation
  under test;
- a real case cannot be reconstructed from its exact private manifest and no
  reviewed successor is accepted;
- provider work cannot preserve credential and finite reservation boundaries;
- an exercised parser/tool needs compromise containment beyond the accepted
  worker boundary;
- protected setup roots change during an M1 operation;
- the implementation fabricates coverage, source authority, or certainty; or
- the final requirement/case/slice traceability audit has a material gap.

## Register maintenance

Every risk closure records the evidence and exact revision that closed it.
Scope changes append a dated disposition; they do not erase why a capability
was excluded. A residual risk can be accepted only by the project owner and
cannot substitute for a failed Must requirement within the claimed milestone
scope.
