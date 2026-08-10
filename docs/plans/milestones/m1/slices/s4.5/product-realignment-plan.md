# M1 Slice 4.5 — Public Bethesda semantic realignment and candidate freeze

Status: Completed

Last reviewed: 2026-08-10
Owner: Project owner
Prepared: 2026-08-05
Accepted: 2026-08-05
Completed: 2026-08-05
Parent plan: `infinium.plan.m1.backend-semantic-proof/3`
Parent slice: [M1 Slice 4.5 — Held-out evaluation v2](../../../../../evaluation/evaluator-history.md)
Starting public disposition: `40cdf30ad9cd63a91198623c88040d49b8dc40b3`

## Objective

Bring the public Bethesda semantic contracts, extractor, publication validation,
and public tests into conformance with ADR-0028 and the accepted Slice 4 semantic-
authority owner disposition. Qualify and freeze one new exact public product
candidate that can be handed to the single authorized private B2 resume.

This is a clean-break public product-contract correction. It is not an evaluator
revision, private oracle task, held-out scoring task, or Slice 5 implementation.

## Owner decisions recorded by this plan

The project owner accepted both bounded recommendations on 2026-08-05:

1. Each FaceGen loose-provider chain is represented through frozen protocol `/4`
   as a distinct `record-semantic-subject`. The product subject is rooted in the
   winning NPC contribution and uses a deterministic semantic suffix containing
   the normalized canonical FaceGen path. Do not use the frozen evaluator's
   single global `provider-topology` subject type for these chains.
2. The M1 product reports a missing loose FaceGen path as `unknown` because the
   current installation snapshot provides structural provider authority, not an
   exhaustive byte-verified loose-provider index. Building that stronger
   absence authority is deferred to M3 planning; this slice does not widen the
   MO2 snapshot contract to manufacture `absent` conclusions.

The exact public subject form for this slice is:

```text
{winning-contribution-id}:semantic:face-gen-loose-provider-chain:{normalized-relative-path}
```

Its `subject_type` is `record-semantic-subject`. Protocol `/4` therefore
canonicalizes each subject from the winning contribution's semantic identity
plus the independently authorable normalized path suffix. Mesh and tint paths
remain distinct, and a single-provider chain still creates a subject.

## Governing authority

Read and apply repository `AGENTS.md` in its required order before editing. The
task-specific authorities are:

- [ADR-0028](../../../../../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md);
- [semantic-authority owner disposition](../../../../../evaluation/evaluator-history.md);
- [final held-out oracle-authority matrix](../../../../../evaluation/m1-slice4-heldout-oracle-authority-matrix.md);
- [authority-completion conformance mismatch](../../../../../evaluation/evaluator-history.md);
- [final held-out scope amendment](../../../../../evaluation/evaluator-history.md);
- [evaluator-v2 baseline amendment](../../../../../evaluation/m1-evaluation-baseline-evaluator-v2-amendment.md);
- [parent Slice 4.5 plan](../../../../../evaluation/evaluator-history.md);
- [Slice 4 implementation record](../s4/record.md);
- [Slice 4.5 implementation record](record.md);
- the accepted evaluation strategy, case catalog, fixture guidelines,
  anti-overfitting rules, and evaluator-private fixture governance v2; and
- frozen evaluator `/4` at
  `3693d19563c636cd2879804633ca4ce52448d2c1`.

Higher-authority accepted product documents and ADRs win if this plan is found
to conflict with them. Record a real contradiction and stop rather than choosing
an implementation behavior ad hoc.

## Required preflight

Before implementation:

1. Read the required authorities above and the active source/test surfaces.
2. Inspect `git status`, current branch, `HEAD`, remotes, and recent history.
3. Confirm the accepted plan is present and that unrelated worktree changes will
   not be overwritten or included.
4. Confirm the historical candidate
   `98fe8a5a173116427bf78077673fd10e8d018103` and evaluator commit
   `3693d19563c636cd2879804633ca4ce52448d2c1` resolve locally.
5. Confirm no private fixture path is needed. Do not enumerate or inspect the
   sibling `infinium-evaluator-fixtures` repository.
6. Confirm no required source change falls outside the bounded files or semantic
   surfaces identified here. Escalate material scope expansion before editing.
7. Preserve owner-approved planning-document changes already present in the
   worktree. Do not absorb unrelated user changes.

## Scope

### Included

- public Bethesda semantic domain contracts and schema/producer versions;
- Bethesda semantic extraction for EDID, FaceGen applicability, loose-asset
  availability, fixed coverage, layered gaps, and hybrid taxonomy;
- public coordinator publication validation;
- generic worker serialization/round-trip compatibility where affected;
- public unit, contract, integration, and evaluation tests;
- public EVAL-0052 and applicable EVAL-0086 development/regression evidence;
- public compatibility checks against the exact frozen evaluator `/4`;
- independent public-only semantic review and one bounded correction pass;
- a new exact product candidate build, identity, dependency inventory, and
  public freeze/handoff record; and
- append-only Slice 4.5 implementation-record closeout.

### Explicit non-scope

- evaluator protocol, projection, canonicalizer, schemas, scorer, adapter, or
  calibration changes;
- protocol `/5`;
- private fixtures, expected outputs, corpus files, fingerprints, freeze, tag,
  qualification, or scoring;
- `adapt`, `score`, `score-corpus`, or any private candidate execution;
- new Bethesda record families or semantic-field allowlists;
- exhaustive loose-file absence capture or a widened MO2 snapshot contract;
- archive activation/member-precedence implementation;
- localized-string or automatic-environment-discovery implementation beyond
  honest coverage/gap reporting;
- fixture-, mod-, NPC-, race-, title-, zone-, or case-specific production rules;
- legacy archive inspection;
- Slice 5 work, live/billable calls, or pushing.

## Clean-break public contract

### Versioning

- Bump `BethesdaSemanticSnapshot.SchemaVersion` from `1.0.0` to `2.0.0`.
- Bump `BethesdaSemanticExtractor.ProducerVersion` from `1.0.0` to `2.0.0`.
- Update every public producer, consumer, validator, declaration, assertion, and
  fixture that binds those versions.
- Do not add compatibility or migration behavior for the superseded in-memory
  M1 snapshot contract.
- Do not bump the generic managed-worker envelope merely because its nested
  Bethesda payload changed; change it only if its own contract actually changes.
- Keep the accepted taxonomy ID/version and evaluator `/4` identities unchanged.

### FaceGen applicability

Replace the old gap-named applicability enum with exact semantic states whose
snake-case transport values are:

- `applicable`;
- `not_applicable_deleted_winner`;
- `unknown_template_traits_decision`;
- `not_applicable_template_traits`;
- `unknown_race`; and
- `not_applicable_race_without_face_gen_head`.

Model the template-traits decision explicitly as known inherited, known not
inherited, or unknown. Preserve the existing `/4`-required boolean
`templates_traits` as a derived transport fact: it is true only for a definite
traits-inherited decision. Additional product state may be serialized, but
must not require a frozen evaluator change.

Apply this order to each winning NPC:

1. deleted winner: not applicable; no applicability gap;
2. unknown template-traits decision: unknown; template capability gap;
3. definite traits inheritance: not applicable; no applicability gap;
4. missing, null, unresolved, or semantically unknown race/FaceGenHead decision:
   unknown; race capability gap;
5. resolved race without `FaceGenHead`: not applicable; no applicability gap;
6. otherwise: applicable.

Non-trait template use does not suppress the NPC's own FaceGen assessment.

### Loose-asset availability

Add a typed `BethesdaAssetAvailability` concept with `Present`, `Absent`, and
`Unknown`. Product and user-facing consumers use this value. Retain
`Present` and `ExactAbsenceKnown` only as derived `/4` transport properties:

| Availability | `Present` | `ExactAbsenceKnown` | Winner |
|---|---:|---:|---|
| `Present` | `true` | `false` | required |
| `Absent` | `false` | `true` | null |
| `Unknown` | `false` | `false` | null |

`true/true` is invalid. A non-present state cannot carry a winner, and a present
state must carry a winner from its declared ordered provider chain.

Under the current M1 snapshot authority:

- an exact declared loose winner produces `Present`;
- a missing loose path produces `Unknown`;
- `Absent` remains a valid contract state but is not produced without a future
  explicit exhaustive byte-verified loose-provider assurance; and
- archive-member support never changes loose availability.

### EDID boundary

Keep `EDID` allowlisted for `NPC_`, `RACE`, and `REFR`, including exact observed
counts. Its text alone cannot create purpose, affected-area, consequence,
finding, user-intent, or taxonomy assignments.

### Layered gaps

Model stable broad gap categories separately from exact detail. Preserve exact
denominator and missing-capability facts. Emit and aggregate these public
population/capability pairs:

| Condition | Population | Missing capability |
|---|---|---|
| unsupported record signature | `unsupported-records:{signature}` | `allowlisted-record-family-semantics` |
| unsupported field | `unsupported-fields:{signature}:{field}` | `allowlisted-record-field-semantics` |
| unsupported field shape | `unsupported-shapes:{signature}:{field}` | `allowlisted-record-shape-semantics` |
| unresolved localized string | `localized-strings` | `localized-string-resolution` |
| unresolved archive availability | `face-gen-archive-assets` | `archive-activation-and-member-precedence` |
| unavailable automatic discovery | `automatic-environment-discovery` | `automatic-environment-discovery` |
| unknown template-traits decision | `face-gen-applicability:template` | `complete-template-traits-decision` |
| unknown winning-race decision | `face-gen-applicability:race` | `resolved-winning-race` |

Signatures and fields are lowercase. Aggregate identical pairs and count exact
affected members. Generated gap IDs and reason prose remain product-conformance
facts, not held-out oracle authority.

### Fixed coverage registry

Every successful bounded-M1 semantic snapshot contains exactly these ten rows,
including completed zero-denominator rows:

1. `plugins`;
2. `npc-records`;
3. `race-records`;
4. `placed-reference-records`;
5. `unsupported-records`;
6. `face-gen-loose-assets`;
7. `face-gen-archive-assets`;
8. `localized-strings`;
9. `automatic-environment-discovery`; and
10. `taxonomy-subjects`.

Use the denominator/completed-count definitions from the owner disposition.
Any incomplete count or attached gap prevents a plain `completed` state. A zero
denominator has completed count zero and state `completed`.

For the two FaceGen asset rows specifically, `face-gen-loose-assets` counts only
paths resolved to exact `Present` or exact `Absent`; a current missing path is
`Unknown` and is not completed. `face-gen-archive-assets` separately counts the
applicable paths without a loose winner whose archive availability was resolved.
Archive completion or its capability gap never changes the loose-asset state.

### Hybrid taxonomy

- Every plugin-record contribution emits `surface.plugin-data` and
  `delivery.plugin-container` through the existing canonical axis/facet pairs.
- Add area, consequence, purpose, or extent only when decoded semantic/provider
  evidence supports it.
- Unsupported subjects may express a genuine unsupported area or unknown
  consequence, but do not receive a mandatory filler matrix.
- Names, signatures, and EDID values alone do not authorize classification.
- Remove the generic provider-topology subject inferred from enabled plugins.
- Emit one distinct `record-semantic-subject` for each declared FaceGen mesh or
  tint loose-provider chain using the exact subject form recorded above.
- Each FaceGen chain subject emits `surface.asset` and
  `delivery.loose-data-file`; this includes single-provider chains.
- Reject duplicate final semantic taxonomy tuples.

## Implementation surfaces

The implementer must inspect the complete dependency graph, but the expected
bounded edit set is:

- `src/Infinium.Bethesda/BethesdaSemanticContracts.cs`;
- `src/Infinium.Bethesda/BethesdaSemanticExtractor.cs`;
- `src/Infinium.Coordinator/BethesdaSemanticPublicationValidator.cs`;
- generic worker/coordinator declarations only where the changed public version
  or serialization contract requires them;
- `tests/Infinium.UnitTests/BethesdaSemanticExtractorTests.cs`;
- `tests/Infinium.ContractTests/BethesdaSemanticContractTests.cs`;
- `tests/Infinium.IntegrationTests/BethesdaSemanticBoundaryIntegrationTests.cs`;
- `tests/Infinium.IntegrationTests/BethesdaSemanticWorkerEndToEndIntegrationTests.cs`;
- `tests/Infinium.EvaluationTests/BethesdaSemanticExtractionEvaluationTests.cs`;
- `tests/Infinium.EvaluationTests/BethesdaOracleAgreementEvaluationTests.cs`;
- `tests/Infinium.EvaluationTests/EvaluatorV2PublicProtocolTests.cs`; and
- affected public fixture/schema documentation and the Slice 4.5 implementation
  record.

Do not mechanically update a historical oracle until its authority is
classified. Preserve sealed historical evidence; create or identify a current
public development oracle when the old artifact intentionally represents the
superseded contract.

## Required public test matrix

### Contract and invariants

- schema and producer version `2.0.0`;
- exact applicability transport vocabulary;
- availability mapping for all three states;
- rejection of `true/true`, present-without-winner, non-present-with-winner,
  and a winner outside the declared chain;
- exact fixed coverage registry, uniqueness, arithmetic, and gap joins;
- exact layered gap population/capability vocabulary and aggregation;
- required taxonomy core tuples and duplicate-tuple rejection; and
- deterministic JSON round trip through the public worker boundary.

### Semantic behavior

- EDID counts for NPC/RACE/REFR and adversarial EDID-only cases that produce no
  semantic classification;
- deleted plus otherwise-problematic race/template cases proving deleted-first;
- unknown template decision before race evaluation;
- definite traits inheritance before race evaluation;
- non-trait template use remaining applicable;
- missing, null, unresolved, and semantically unknown race decisions;
- resolved race without `FaceGenHead` and the ordinary applicable case;
- mesh and tint present chains with exact winners;
- missing loose paths remaining unknown regardless of archive support;
- no current structural-only input producing exact absent;
- all ten coverage rows in populated and all-zero snapshots;
- per-signature, per-field, and per-shape gaps with identical-pair aggregation;
- contribution technical-core taxonomy for supported and unsupported records;
- meaningful sparse semantic assignments without filler tuples;
- distinct single- and multi-provider FaceGen chain subjects; and
- absence of a generic plugin-list provider-topology subject.

### Evaluation and anti-overfitting

- update current public EVAL-0052 and applicable EVAL-0086 development cases;
- use generic synthetic/adversarial cases rather than real-mod or fixture-name
  branches;
- preserve public negative and mutation coverage;
- ensure current-contract tests do not silently treat superseded sealed oracle
  outputs as present authority; and
- prove that incidental product IDs, prose, paths outside the projected semantic
  path value, and ordering outside declared sequences do not become oracle facts.

## Publication validation

The coordinator must reject, before authoritative publication:

- an unsupported Bethesda semantic schema or producer version;
- any invalid availability/winner combination;
- a missing, duplicate, extra, or arithmetically invalid fixed coverage row;
- coverage state inconsistent with counts or attached gaps;
- a gap whose category/detail/population/capability relationship is invalid;
- duplicate taxonomy semantic tuples;
- a contribution without both required technical-core assignments; and
- a generic plugin-only provider-topology subject or a malformed FaceGen chain
  subject.

Validation must not invent missing rows, repair malformed facts, or silently
coerce unknown into absent.

## Review cycle

1. The implementing agent completes the entire bounded change and runs focused
   checks.
2. The implementing agent performs a semantic/diff review against every section
   of ADR-0028 and this plan, not merely a test-pass review.
3. One fresh public-only independent reviewer receives the accepted public
   authority, current diff, and verification evidence. The reviewer must not
   access private fixtures and must assess correctness, contract completeness,
   anti-overfitting, frozen-evaluator compatibility, and scope drift.
4. The implementer reconciles the review and performs at most one focused
   correction pass.
5. Rerun all affected and final checks, then perform a final re-review. Material
   unresolved findings block candidate freeze.

The independent public reviewer is not the later private oracle reviewer or
custodian and gains no private or scoring authority.

## Verification

Run from the public repository:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
```

Also:

- run focused contract, extractor, boundary, worker, oracle-agreement, and
  evaluator public-protocol tests during implementation;
- build and invoke evaluator `/4` only from a detached checkout of exact commit
  `3693d19563c636cd2879804633ca4ce52448d2c1` for public adapter/scorer
  compatibility smokes against public data;
- run the frozen evaluator calibration twice into separate disposable roots and
  confirm byte-identical canonical evidence;
- verify the evaluator/projection/scorer/adapter source and identity are
  unchanged; and
- verify no production branch depends on fixture names, private paths, or
  held-out case identities.

Do not use private inputs or expected answers for any verification in this task.

## Candidate freeze and public handoff

After review and all checks pass:

1. Create one focused local implementation candidate commit. Do not push.
2. Build that exact commit from a clean detached worktree.
3. Record the exact source commit and `Infinium.Bethesda.dll` length and SHA-256.
4. Record the complete runtime dependency inventory and hashes, build commands,
   logs/evidence locations, test counts, skips, and review/correction results.
5. Confirm required runtime roots are identical between the reviewed candidate
   and the detached candidate build.
6. Add an append-only public candidate-freeze/handoff artifact that binds the
   new candidate to frozen evaluator `/4` and states that B2 remains unrun.
7. Update `docs/plans/implementation-records/M1-slice-4.5.md` with exact evidence
   and the candidate commit.
8. Create a separate documentation-only closeout commit if needed to record the
   already-frozen candidate identity. Do not amend the candidate commit.
9. Leave the repository clean and do not push.

The historical candidate and all earlier evaluator/corpus incidents remain
immutable append-only evidence.

## Stop conditions

Stop and return to the project owner if:

- an accepted authority contradicts this plan;
- distinct FaceGen chains cannot be represented by the accepted
  `record-semantic-subject` form under frozen `/4`;
- implementation or public qualification requires changing evaluator `/4`;
- an M1 requirement is interpreted to require exact loose absence from the
  current structural-only snapshot;
- a new semantic ambiguity prevents one deterministic generic behavior;
- public tests pass only through fixture-specific production behavior;
- the task would touch private fixtures, the legacy archive, Slice 5, or an
  unaccepted earlier-slice architecture expansion;
- the frozen evaluator identity or projection differs from the accepted record;
  or
- independent review leaves a material issue unresolved.

Do not create `/5`, widen the private task, or use candidate output as oracle
truth in response to a stop.

## Completion criteria

This public realignment task is complete only when:

- the clean-break public contract and all consumers are updated together;
- all accepted semantics and invariants have generic public tests;
- the full verification set passes;
- independent public review and re-review are complete;
- frozen evaluator `/4` compatibility is demonstrated without modifying it;
- one exact new public product candidate and dependency inventory are frozen;
- public handoff and implementation records contain exact identities and
  verification evidence;
- no private access, oracle work, corpus work, or scoring occurred; and
- the local repository is clean, with no push performed.

This does not complete Slice 4.5. It only unblocks the separately authorized,
fresh, private B2 oracle-reviewer resume.

## Post-freeze boundary

After this plan completes, stop. A separately authorized fresh private B2
reviewer may receive only the positive-allowlist public authority bundle, the
new candidate identity as a binding, and the already-frozen private inputs. The
reviewer receives no product source, product output, or candidate runtime. A
different custodian qualifies and freezes the corpus, and a later separate C2
task performs the one-shot score. Those roles and tasks must not be combined
with this implementation context.

## Completion record

The clean-break implementation, public verification, independent review,
single correction pass, detached-candidate build, and candidate freeze
completed at `a98d648bd0adb2751ee0c09828e0227b1583950f`. The candidate was not
amended. Exact artifact, dependency, check, review, and boundary evidence is in
the [public product candidate freeze](../../../../../evaluation/evaluator-history.md).
The separate documentation closeout is
`2fc724af9e6cc483e98e48c2163b533a071671fa`. No private work or push occurred.

This completes this subordinate public realignment plan, not Slice 4.5. One
fresh private oracle reviewer may resume B2 once; C2 and Stage D remain later
separate tasks.

That was the exact post-freeze status at this historical checkpoint. The
single authorized B2 resume was later consumed without an oracle or verdict,
and ADR-0032 now defers private held-out evaluation. Protocol `/4` is bounded
historical regression evidence, protocol `/5` is retired unqualified, and no
private evaluator work is authorized. Evaluator-deferral closeout is accepted:
Slice 4.5 is closed by owner disposition. That closeout made Slice 5 eligible;
later execution status is maintained only in
[current project state](../../../../../current-state.md).

## Historical fresh-agent implementation handoff

The implementation prompt used for this completed task is retained below as
historical execution evidence:

```text
Implement the accepted Infinium M1 Slice 4.5 public Bethesda semantic
realignment plan in full:

docs/plans/slices/M1-slice-4.5-public-product-realignment.md

Work in the public infinium repository only. Read AGENTS.md first and follow its
required reading order, then read the entire accepted realignment plan and every
authority it lists before editing. Reconstruct the actual Git/worktree state;
do not assume the branch or HEAD. Preserve any owner-approved uncommitted plan,
index, or deferred-register changes that already exist, and do not overwrite or
include unrelated user changes.

Implement the whole bounded clean-break contract across producers, consumers,
validation, schemas/versions, public fixtures, and tests. Use the accepted
record-semantic-subject representation for each FaceGen provider chain. Keep
missing loose paths unknown under the current structural-only snapshot; do not
expand MO2 capture to claim exact absence. Keep evaluator protocol /4,
projection, canonicalizer, schemas, scorer, adapter, and calibration unchanged.

Do not read, search, enumerate, inspect, modify, or run anything in
infinium-evaluator-fixtures. Do not inspect the legacy archive. Do not run
private B2, adapt, score, score-corpus, C2, or Stage D. Do not create /5. Do not
perform live or billable calls. Do not push.

Run the plan's focused and full verification. Then perform a full semantic and
diff review, arrange one fresh public-only independent review, reconcile at
most one focused correction pass, rerun the checks, and re-review. Do not stop
merely because tests pass.

When the implementation is accepted, create a focused local candidate commit,
build and verify that exact commit from a clean detached worktree, record the
DLL and dependency identities, add the append-only public freeze/handoff and
Slice 4.5 implementation-record evidence, and use a separate documentation
closeout commit if necessary so the candidate commit is never amended. Leave
the repository clean and report exact commits, artifacts, checks, skips,
review findings/corrections, private-access state, and push state.

If any stop condition in the accepted plan occurs, stop and report it to the
owner. Do not improvise a new semantic rule, expand scope, alter the evaluator,
or use product output as oracle truth.
```
