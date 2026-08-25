# Deferred-question and residual-risk register

Status: Accepted  
Owner: Project owner  
Prepared: 2026-07-28  
Accepted: 2026-07-28  
Last reviewed: 2026-08-25
Applies to: completed M0/M1 work and the M2 planning boundary

## Purpose

This register distinguishes:

- questions deliberately deferred because their evidence does not exist yet;
- unsupported capabilities outside M1;
- accepted design risks, including those now qualified only for bounded M1
  scope; and
- historical blockers that governed M1 planning and completion.

A recorded deferral is not implicit coverage, permission to guess, or a waiver
of the requirement before its stated delivery milestone.

## Deferred research questions

| RQ | Current M0 disposition | Required next evidence | Reopen/schedule point | M1 effect |
|---|---|---|---|---|
| RQ-028 | Deferred independent semantic evidence under ADR-0035 | Fresh independently authored and qualified development/validation/held-out evidence, false-positive/false-negative and abstention distributions, taxonomy-stratified coverage, carryover/reconciliation errors, stale/readiness presentation results, and user review evidence | Reconsider only after M2 acceptance at the M3 Evaluation Readiness Gate under a new accepted plan satisfying ADR-0035 | M1 and M2 make no numerical semantic-reliability or readiness claim. Public product checks prove contracts, provenance, isolation, and bounded behavior only |
| RQ-029 | Later evidence | Exact log producer/version inventory, stable fingerprints, capture-session boundaries, clock/fingerprint failure cases, privacy/retention behavior, and controlled exact/matched/likely/unknown/historical fixtures | Before any automatic log application; schedule in the M3 plan or earlier if M2/M1 scope changes | Runtime-log ingestion/application is excluded from M1 |
| RQ-030 | Later evidence | Final application artifacts, signing identity/business decision, installer/update alternatives, WebView2/runtime delivery behavior, rollback/recovery threat model, SBOM and GPL distribution closure | M4 packaging/update planning after M2 architecture qualification and before public packaging | No installer, updater, signing, or public distribution claim in M1 |
| RQ-038 | Resolved for M1 by accepted ADR-0025 | The bounded synchronous adapter, credential/budget boundary, retained replay, and product-conformance evidence were accepted in M1; future work must requalify material drift | Reopen only if the selected model/profile or material provider capability changes | M1's accepted evidence applies only to the exact bounded profile and does not grant future model or production-wide authority |

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
| Exhaustive loose-file absence authority | M1's structural provider snapshot can prove an exact declared winner but cannot prove that every possible loose path was exhaustively byte-verified. Missing loose FaceGen paths therefore remain `unknown`, not `absent` | M3 exact-effective-installation and asset-coherence planning, or an earlier accepted scope amendment that defines, implements, and qualifies an exhaustive byte-verified loose-provider index |

## Accepted-design qualification risks

| Risk | Current evidence | M1 control/gate | Residual after M1 |
|---|---|---|---|
| SQLite/CAS crash consistency and native-version drift | M1 implements the exact loaded SQLite identity, guarded VFS, authoritative schema, CAS, backup/restore, migrations, replay, and bounded fault tests | The accepted M1 floor exercises its bounded EVAL-0087 obligations | Longer-duration corruption/scale evidence remains M3 |
| Custom lifecycle correctness | M1 implements the accepted state machine, fencing, recovery, coordinator/worker boundary, and fault tests; an earlier audit added finite worker RPC deadlines after reproducing an orphaned crash-recovery worker | The accepted M1 floor exercises the bounded lifecycle and EVAL-0088 obligations | Tuning, frontend lifecycle UX, and multi-hour behavior remain later work |
| Same-user worker compromise | Per-worker Job Objects contain lifetime/resources and finite RPC deadlines bound coordinator-loss recovery, but neither mechanism is a sandbox | Keep M1 parsers managed and positively allowlisted; exclude any operation needing compromise containment | Stronger isolation requires new research/ADR |
| Named-pipe caller/role confusion | ADR-0019 specifies restricted role-separated contracts; the bounded M1 malformed/race/reconnect/nonce/limit suite passed | Requalify each materially expanded IPC role or operation | Public hostile-local-user hardening remains bounded by Windows same-user model |
| Credential half-commit or revocation race | ADR-0020's generation/intents/helper design and the bounded M1 EVAL-0089/secret-canary surface passed | Requalify shipped-product enrollment and every materially expanded credential operation | Same-user malware/admin/debugger protection is not claimed |
| Provider cost/billing ambiguity | ADR-0023 defines conservative reservation/holds | EVAL-0081 synchronous path; the qualification and each semantic call are separately authorized/reserved/settled; unresolved usage retains its hold | Background/Batch/cache/concurrent modes remain disabled |
| LLM semantic variability | Accepted schema-bound OpenAI path is not deterministic | Exact retained request/response, typed semantic assertions, matched negatives, held-out replacement discipline | No general model reliability claim beyond evaluated prompts/model |
| Non-date-pinned OpenAI model drift | OpenAI currently lists only the moving `gpt-5.6-sol` snapshot identity | Accepted ADR-0025 requires retained-result replay, returned-model/capability fingerprints, and invalidation/requalification on material drift | Identical live re-execution cannot be promised; only the retained original result is exactly replayable |
| Controlled-real fixture availability | Exact private source identities remain non-redistributable; Slice 3.5 reverified every required private dependency against its retained manifest | Hash/pin verification and private acquisition manifest; synthetic equivalents remain primary | Public one-command corpus unavailable |
| Missing independent semantic verdict | Public conformance can demonstrate accepted contracts and bounded behavior, but it does not establish independently qualified semantic reliability | ADR-0035 defers independent semantic-oracle work through M1 and M2; v1-v13 are historical non-authorizing packages and no successor is authorized | M1 and M2 gain no independent semantic or private reliability/readiness claim. Reconsider only after M2 acceptance at the M3 Evaluation Readiness Gate with all ADR-0035 prerequisites and a new accepted M3 plan |
| Archived protocol `/4` representation limit | The retired `/4` tool could not represent the accepted partial `RACE/DATA` state without either inventing a later-layer boolean or dropping lower-layer facts | ADR-0033 archives `/4` with no active execution or review role; current slices use the continuation profile | No current evaluator validates the complete accepted Slice 4 semantic contract |
| Future evaluator authorability and stability | The retired `/5` proof system accepted noncanonical self-authored witness values before independent review rejected it; the current product boundary is not yet the stable user-meaningful surface needed for proportionate independent evaluation | Reconsider after M2 acceptance during M3 planning, with exercised producer/consumer interfaces, stable versioned output and replay, a bounded claim, independently authorable neutral truth, an accepted budget/stopping rule, one small feasibility package, and a new accepted M3 plan | No protocol identity is selected; absence of an independent verdict remains explicit until a future valid evaluation exists |
| Mutagen defects/circular truth | Mutagen is selected and xEdit excluded; Slice 3.5 sealed independent byte/structure expectations before production parsing | Slice 4 production comparison through EVAL-0052 | Support remains only for positively qualified shapes |
| MO2 reconstruction drift | Slice 3 implements the exact version-pinned adapter and EVAL-0051 passed for MO2 `2.5.2` with the accepted empty additional-mapper inventory | Conditional repeat for every new version/operation | Other versions/managers remain unsupported |
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

## M1 authorization conditions (historical; satisfied)

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

ADR-0035 and the accepted semantic-oracle-deferral amendment supersede the old
expectation that M1 would obtain a private or independent semantic verdict.
Slices 5-9 must satisfy all applicable layers of the accepted M1 continuation
verification profile, but those layers are product-conformance, provenance,
isolation, and byte-integrity evidence only. Every implementation record must
state that the independent semantic verdict is explicitly deferred through M1
and M2; no retained historical package or missing private verdict can be
reinterpreted as current semantic authority.

Conditions 1 through 5 are satisfied for the accepted plan. Condition 6 is an
ongoing change-control rule. Implementation preflight must still verify the
actual dependency locks, fixture availability appropriate to the first slice,
clean worktree, and security/runtime assumptions; document acceptance does not
manufacture those execution facts.

On 2026-07-30, the owner reconciled the placed-reference/quest traceability
surface without broadening M1: EVAL-0017 remains the materially different REFR
proof, while planned EVAL-0006/EVAL-0007 and `QUST` forced-reference alias
semantics are deferred beyond M1. EVAL-0052 therefore excludes those quest
shapes, and the accepted plan no longer requires them from Slice 4.

## M1 completion blockers (historical; cleared for accepted scope)

The conditions below governed M1 closeout. The accepted M1 implementation and
post-M1 cleanup records show that none remained open within the delivered
scope. They remain useful change-control criteria if future work reopens the
same boundaries.

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

The independent semantic verdict is explicitly deferred through M1 and M2
under ADR-0035. This is not a silent waiver and does not block product
conformance work in Slices 5-9, but it does block any claim that public
conformance establishes independent semantic reliability, private reliability,
or M3 readiness. The formerly active provider-analysis closeout and all of M1
were subsequently accepted; that completion still does not create an
independent verdict or grant M3 readiness.

## Register maintenance

Every risk closure records the evidence and exact revision that closed it.
Scope changes append a dated disposition; they do not erase why a capability
was excluded. A residual risk can be accepted only by the project owner and
cannot substitute for a failed Must requirement within the claimed milestone
scope.
