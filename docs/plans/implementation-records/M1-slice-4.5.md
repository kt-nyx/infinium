# M1 Slice 4.5 — Held-out evaluation v2 implementation record

Status: Public evaluator `/4` and conforming candidate frozen; private B2 authorized next; C2 and Stage D pending
Opened: 2026-08-04
Plan: `infinium.plan.m1.backend-semantic-proof/3`
Execution plan: [M1 Slice 4.5](../slices/M1-slice-4.5-held-out-evaluation-v2.md)

## Stage A boundary

This record covers only public evaluator-v2 architecture, implementation,
calibration, evaluator-v1 retirement, documentation integration, review, and
freeze. No evaluator-private fixture content may be read and no held-out
scoring may run in this stage.

## Recovery and preflight

- verified public baseline: `e0157b5af491a8759576ef6d3604b6a6ef378ca9`;
- branch: `codex/m1-slice-4.5-evaluator-v2`;
- frozen candidate: `98fe8a5a173116427bf78077673fd10e8d018103`;
- candidate detached worktree: outside the active repository worktree;
- candidate `Infinium.Bethesda.dll`: 157,696 bytes, SHA-256
  `dc8ae44627fa40ca3937e4022c8e7914468e4d7a4cf1c40797a22ef2abec3655`;
- core runtime diff from candidate through baseline: empty; and
- legacy archive and evaluator-private repository: not accessed.

Frozen-candidate verification:

- locked restore: passed;
- Release build: passed, 0 warnings and 0 errors;
- `M1Unit`: 88 passed, 1 expected skip;
- `M1Contract`: 25 total passed across applicable projects;
- `M1Integration`: 32 total passed across applicable projects;
- `M1Evaluation`: 41 total passed, 8 expected private skips;
- `M1Security`: 9 total passed;
- `M1Fault`: 13 total passed; and
- full suite: 230 passed, 9 expected skips, 0 failed.

## Stage A implementation

Public evaluator-v2 tool:
`tools/evaluation/Infinium.EvaluatorV2/Infinium.EvaluatorV2.csproj`.

Focused commits before public review:

- `a708756`: accepted ADR-0027, governance v2, M1 plan revision `/3`,
  Slice 4.5 plan, baseline amendment, incident record, and status corrections;
- `9823525`: standalone protocol, schemas, scorer, reflection adapter,
  calibration implementation, and evaluator-v1 boundary migration;
- `21844df`: public calibration, determinism, boundary, and exact-artifact
  adapter tests;
- `5e9d683`: sanitized exact evaluator identity binding and a stale repository
  structure assertion correction; and
- `8023cdf776a25210bcc80e7574c1aaecde278b6b`: the sole public review's
  focused correction pass and the frozen evaluator-v2 code/schema/test state.

Evaluator-v1 policy moved out of `Infinium.Application` into the tool's
`LegacyV1/` compatibility boundary:

- `AssertionResultReader.cs`;
- `BethesdaByteOracleValidator.cs`;
- `FixturePackageReader.cs`; and
- `WindowsRetainedArtifactIdentity.cs`.

The compatibility boundary is not reachable through an active tool command.
Public fixture bytes and regression tests were preserved. The production
project now embeds only the neutral analyzer-declaration, CLI-summary,
run-output, and common schemas. Its neutral schema validator gained JSON
Schema union-type support and resource lookup for the separately loaded tool,
and `Infinium.Application` grants the evaluator tool internal access to the
neutral bounded JSON reader. These are the only reviewed production project or
shared-helper exceptions; they do not alter semantic extraction, runtime
commands, coordinator publication, persistence, or worker behavior.

Protocol and schema identities:

- `infinium.evaluator-v2/1`;
- scorer `infinium.evaluator-v2.scorer` version `1.0.0`;
- adapter `infinium.evaluator-v2.slice4-reflection-adapter` version `1.0.0`;
- `infinium.evaluator-v2.execution-manifest/v1`;
- `infinium.evaluator-v2.candidate-semantic-output/v1`;
- `infinium.evaluator-v2.expected-semantic-output/v1`;
- `infinium.evaluator-v2.assertion-results/v1`;
- `infinium.evaluator-v2.sanitized-result/v1`; and
- `infinium.evaluator-v2.calibration-results/v1`.

## Public qualification and review evidence

The public `calibrate` command ran twice into distinct new result directories.
Both final invocations returned `PASS` with all 17 cases discriminating through
the real scorer pipeline at their intended terminal/category. Both output
files were byte-identical, SHA-256
`d53776b92e4d41902f05548dcc757503ebba40773e67d45c2a38ec08fc51da4d`.

The exact-artifact `adapt` command loaded the detached candidate DLL by
reflection, verified its retained identity, executed public fixture
`BETH-NPC-DEV`, and returned `PASS`, state `completed_with_gaps`, with 1,664
canonical facts. The adapter used the detached candidate's independently built
test-output dependency bundle, whose 229 retained files were inventoried and
hashed; its `Infinium.Bethesda.dll` was byte-identical to the separately
recorded candidate artifact. This was public adapter smoke evidence, not
held-out scoring and not expected-truth construction.

Evaluator-v2 focused tests: 6 passed and 1 platform-capability skip. They cover
all public mutation/error cases through the scorer, byte determinism,
stable-set versus ordered-sequence canonicalization, valid PASS-attestation
serialization, result-root escape and overwrite rejection, reparse-ancestor
rejection when host symbolic-link creation is available, absence of evaluator
fixture and partition policy from production source, exact dependency-bound
candidate execution, and rejection of undeclared dependency fallback.

The one permitted fresh-context public reviewer inspected only the public
repository and detached public candidate. It reported five material findings:
unverified corpus identity, incomplete candidate dependency identity, invalid
null omission in PASS attestations, calibration that bypassed the real scorer,
and alias/partial-publication risk in result writes. The single focused
correction pass:

- binds the corpus hash to corpus ID/version, accepted plugin/entity/order
  identities, unsupported capabilities, and exact oracle bytes, while
  admitting only `frozen` and `clean` corpus state;
- inventories and verifies every declared candidate file, loads only declared
  non-framework managed dependencies, and rejects evaluator-context fallback;
- preserves required null protocol fields and schema-validates every result
  before publication;
- routes 17 answer-known calibration cases through the real scorer, including
  candidate execution/output, manifest, oracle-tamper, and dependency-drift
  error boundaries; and
- requires a new result directory beneath a reparse-free parent chain, pins
  the Windows directory chain against rename, stages all durable output before
  publication, cleans partial publication, and reports write failure as
  `EVALUATOR_ERROR` at `result_write`.

No second reviewer or correction cycle was used.

Final-check results after the focused correction pass:

- locked restore: passed;
- Release build: passed, 0 warnings and 0 errors;
- `M1Unit`: 88 passed, 1 expected platform skip;
- `M1Contract`: 29 total passed across applicable projects;
- `M1Integration`: 32 total passed across applicable projects;
- `M1Evaluation`: 47 total passed, 9 expected platform/private skips;
- `M1Security`: 9 total passed;
- `M1Fault`: 13 total passed;
- full suite: 254 passed, 10 expected platform/private skips, 0 failed;
- `dotnet format --verify-no-changes`: passed;
- dependency-manifest check: passed;
- `git diff --check`: passed;
- core runtime isolation against `98fe8a5`: empty; and
- detached candidate status and retained DLL identity: unchanged.

Malformed-manifest CLI smoke: exit code 2, terminal `EVALUATOR_ERROR`, failure
stage `manifest`.

Retired pre-qualification evaluator-v2 commit:
`8023cdf776a25210bcc80e7574c1aaecde278b6b`. It produced no held-out product
verdict.

The following closeout commit changes this implementation record only. It does
not change the frozen protocol, schemas, scorer, adapter, or calibration.

That historical closeout was published at branch tip
`214815481363671551f0d6f3090029aaec5f5fc4`. The corrective successor is
published by the owner-authorized closeout below.

## Owner-authorized Stage A corrective pass

New frozen public evaluator commit:
`72616fb6fbb3db7021e8100adc12a251c427f8d1`.

Successor identities are protocol `infinium.evaluator-v2/2` version `2.0.0`,
scorer `infinium.evaluator-v2.scorer` `2.0.0`, adapter
`infinium.evaluator-v2.slice4-reflection-adapter` `2.0.0`, and projection
`infinium.evaluator-v2.slice4-semantic-projection` `2.0.0`. The machine-readable
freeze and exact 20-file public source/protocol inventory are in
[`../../evaluation/evaluator-v2-stage-a-freeze.json`](../../evaluation/evaluator-v2-stage-a-freeze.json).

The adapter now explicitly projects only named Slice 4 state, plugin,
record/contribution, override/winner, NPC, RACE, REFR, link, field-presence,
resolved-participant, FaceGen/provider-topology, taxonomy, coverage, gap, and
stable failure-code fields. Paths, snapshot plumbing IDs, dependency and
producer metadata, timestamps, exception text, reason/message/display prose,
and redundant manifest identities are excluded. FormKeys are ID-first
`xxxxxxxx:plugin.ext`.

`compare-prepared` validates Stage B prepared outputs through the same schema,
identity, comparison, aggregation, and result logic without loading or
executing product code. `score-corpus` admits one or more members as one Stage C
invocation and emits one sanitized aggregate without member identities.
Execution manifests now construct ordered loose-provider chains, exact
winners, retained asset identities, extra installed entities, and archive
member population support for positive, partial, and exact-absence FaceGen
cases.

After valid admission, candidate invocation exceptions are product
`FAIL/candidate_execution` and candidate projection violations are product
`FAIL/candidate_output_contract`. Manifest, tuple, retained-byte, evaluator
binary/dependency, oracle, candidate-admission, infrastructure, and
publication failures remain `EVALUATOR_ERROR` with no verdict.

Corrective verification:

- detached candidate restore/build: passed, clean at
  `98fe8a5a173116427bf78077673fd10e8d018103`;
- candidate `Infinium.Bethesda.dll`: 157,696 bytes, SHA-256
  `dc8ae44627fa40ca3937e4022c8e7914468e4d7a4cf1c40797a22ef2abec3655`;
- detached test-output candidate bundle: 64 DLLs;
- locked restore and Release solution build: passed, 0 warnings/errors;
- `M1Unit`: 88 passed, 1 expected platform skip;
- `M1Contract`: 29 total passed;
- `M1Integration`: 32 total passed;
- `M1Evaluation`: 49 total passed, 9 expected platform/private skips;
- `M1Security`: 9 total passed;
- `M1Fault`: 13 total passed;
- full suite: 256 passed, 10 expected platform/private skips, 0 failed;
- evaluator-focused tests: 8 passed, 1 expected symlink-capability skip;
- public calibration: 23 of 23 cases passed twice with byte-identical output,
  SHA-256 `35322d5b289160e646a57cfcdeeffc422d55cd5e1dc7c0862e1bf157c4893260`;
- detached-candidate CLI `adapt`: PASS with 1,378 explicit projected facts;
- CLI `compare-prepared`: correct output PASS; targeted mutation FAIL with
  exit code 1;
- CLI `score-corpus`: one-member PASS and two-member PASS;
- format, dependency-manifest, and `git diff --check`: passed; and
- core runtime diff against the frozen candidate: empty.

The first public CLI admission attempt used the three-DLL Bethesda project
output and was correctly rejected before scoring because the retained
dependency inventory omitted Mutagen. The documented 64-DLL detached
test-output bundle then passed; no held-out invocation or retry occurred.

No private fixture repository content or legacy archive content was accessed.
Stage B private qualification and Stage C held-out scoring have not run.
At that Stage A checkpoint, Stage B was unblocked and required to autodiscover
the unique frozen evaluator from the tracked freeze handoff. Branch publication target:
`codex/m1-slice-4.5-evaluator-v2`.

## Later stages

- Historical Stage C result commit:
  `30185b478904d08f073576d652000f06b76986db`, immutable `FAIL`, invocation
  count `1`.
- Sanitized Stage C.5 adjudication:
  `7a4842b91eca79d7f7623dc414d6e42f3fcf54e2`; product verdict invalidated,
  product correction not required, evaluator successor required, and complete
  materially independent corpus replacement required.
- Detailed public history:
  [Stage C.5 incident](../../evaluation/evaluator-v2-stage-c5-adjudication-incident.md).
- Successor public evaluator qualification: complete and frozen at
  `34ed0c84165e9a49f44a88ecd87cac967132ebd7`.
- Successor private corpus qualification: not run.
- Successor held-out scoring: not run.
- Stage D: not started.
- Slice 4.5 overall completion: pending later stages.

## Public evaluator `/3` successor closeout

Branch: `codex/m1-slice-4.5-evaluator-v2-successor`.

Focused commits:

- `eb455b9c2bd65fafca7bf06f906869720b45481d`: sanitized Stage C.5 incident
  and public successor disposition;
- `e36acbb015140a806cb6d19b99b8bcce6f57521f`: protocol `/3`, scorer and
  adapter `3.0.0`, schemas, calibration, and regression tests; and
- `34ed0c84165e9a49f44a88ecd87cac967132ebd7`: the one public review's single
  focused correction pass and exact executable freeze.

Protocol `infinium.evaluator-v2/3`, scorer `3.0.0`, and adapter `3.0.0` are
frozen. The adapter version changed because its manifest/schema binding changed.
Projection `infinium.evaluator-v2.slice4-semantic-projection` remains `2.0.0`
because its source and semantic fact selection did not change. The predecessor
`/2` freeze remains byte-unchanged and retired for the diagnosed surface.

Declared `value_type` now controls validation and comparison. Semantic
`number` accepts any finite JSON number and compares numeric token shapes such
as `10` and `10.0` equally. Semantic `integer` accepts exactly representable
signed Int64 JSON numbers, including integral decimal/exponent notation, and
compares exact Int64 values. Aggregate corpus fingerprints also bind canonical
member IDs, so identity drift cannot reuse a freeze.

The sole fresh public-only reviewer found three material issues: aggregate
fingerprints omitted member IDs, exact semantic integers rejected integral
decimal/exponent notation, and the incident document had an extra EOF blank
line. The single correction pass fixed all three and added candidate/oracle
integer regressions plus aggregate member-identity drift coverage. No second
review or correction cycle was used.

Exact-freeze verification:

- locked restore and Release build: passed, 0 warnings/errors;
- focused evaluator tests: 9 passed, 1 expected symlink-capability skip;
- `M1Unit`: 88 passed, 1 expected platform skip;
- `M1Contract`: 29 total passed;
- `M1Integration`: 32 total passed;
- `M1Evaluation`: 50 total passed, 9 expected platform/private skips;
- `M1Security`: 9 total passed;
- `M1Fault`: 13 total passed;
- full suite: 257 passed, 10 expected platform/private skips, 0 failed;
- public calibration: 39 of 39 passed twice with byte-identical output,
  SHA-256 `685ea06b4dc2327280a5db1b5411f9ea9b4528f067dcece311cdeb2a7634d640`;
- detached-candidate `BETH-REFR-DEV` CLI adapter/scorer smoke: PASS, 1,370
  projected facts and 1,371 of 1,371 assertions passed;
- prepared CLI smoke with semantic-number `10` versus `10.0`: PASS, 1,371 of
  1,371 assertions passed;
- format, dependency-manifest, and `git diff --check`: passed;
- candidate artifact: 157,696 bytes, SHA-256
  `dc8ae44627fa40ca3937e4022c8e7914468e4d7a4cf1c40797a22ef2abec3655`;
- required Slice 4 runtime roots have an empty diff against `98fe8a5`; and
- historical `/2` freeze SHA-256 remains
  `e39f740a9afbcc541032de3b3ce261e7169c029f965b82b6f2b03587999d8b8d`.

The exact 21-file public source/protocol/test inventory is in
[`../../evaluation/evaluator-v2-stage-a-successor-freeze.json`](../../evaluation/evaluator-v2-stage-a-successor-freeze.json).
No private fixture content or legacy archive content was accessed. Private
successor-corpus qualification and held-out scoring are not run. Successor
Stage B was unblocked at that `/3` checkpoint; Stage D had not started. This documentation-only closeout
does not modify the frozen evaluator code, schemas, tests, calibration, adapter,
or projection. No push was performed.

## Final bounded protocol `/4` Stage A freeze

Branch: `codex/m1-slice-4.5-final-heldout-projection`.

The final public evaluator code/schema/test/authority freeze is
`3693d19563c636cd2879804633ca4ce52448d2c1`. Protocol
`infinium.evaluator-v2/4`, scorer and adapter `4.0.0`, and projection
`infinium.evaluator-v2.slice4-semantic-projection` `3.0.0` are qualified and
frozen. Protocol `/3` at `34ed0c84165e9a49f44a88ecd87cac967132ebd7`
remains immutable historical evidence and is superseded before a valid
successor corpus because its projection required non-independently-authorable
oracle values.

The held-out projection now compares result snapshot/failure presence;
answer-free plugin/provider topology; evaluator-owned contribution, record,
winner, and taxonomy identities; independently authorable NPC/RACE/REFR,
FaceGen, link, coverage, and capability-gap semantics; and AIDT presence only.
Exact failure codes, typed AIDT subfields, product contribution/participant/
winner/gap IDs, taxonomy assignment/analyzer/evidence IDs, denominator labels,
paths, prose, timestamps, and invocation plumbing are not held-out facts.
They remain public-conformance or excluded surfaces as classified by the
[normative matrix](../../evaluation/m1-slice4-heldout-oracle-authority-matrix.md).

Public conformance retains the exact `esl-header-flag-missing` regression, full
typed `BethesdaAiDataFact` byte mapping, deterministic taxonomy assignment,
analyzer, and evidence-ID behavior, and all existing product, parser, fault,
security, and publication tests. No production Slice 4 code changed.

The one fresh-context public-only reviewer performed the required
oracle-authorability audit without private access. Its initial audit identified
four material issues: a stale matrix wording conflict, `/3` corpus fingerprint
domains, no-snapshot gaps under the wrong family, and insufficiently
directional invalid-snapshot calibration. The first two were already resolved
in the latest tree when reported. The single focused correction pass fixed the
remaining two. Re-review found no material findings. No second review or
semantic correction cycle occurred. A contract-test-only Markdown line-wrap
repair restored an established exact documentation phrase during full
verification.

Final verification:

- detached candidate commit:
  `98fe8a5a173116427bf78077673fd10e8d018103`;
- candidate `Infinium.Bethesda.dll`: 157,696 bytes, SHA-256
  `dc8ae44627fa40ca3937e4022c8e7914468e4d7a4cf1c40797a22ef2abec3655`;
- locked restore and Release solution build: passed, 0 warnings/errors;
- `M1Unit`: 88 passed, 1 expected platform skip;
- `M1Contract`: 29 total passed;
- `M1Integration`: 32 total passed;
- `M1Evaluation`: 53 passed, 9 expected platform/private skips;
- `M1Security`: 9 total passed;
- `M1Fault`: 13 total passed;
- full suite: 260 passed, 10 expected platform/private skips, 0 failed;
- focused evaluator suite: 12 passed, 1 expected reparse capability skip;
- final narrowed boundary checks: 2 passed;
- public calibration: 56 of 56 passed twice with byte-identical 15,220-byte
  output, SHA-256
  `32470f8ab69c53ef48b9b24947b3eb4b4e782cbb2cfc645345df0d3029c12f36`;
- prepared CLI smokes: known-correct PASS, taxonomy mutation FAIL, AIDT-presence
  mutation FAIL, failure-boundary PASS, and invalid published snapshot FAIL;
- aggregate public corpus smokes: one-member PASS, identity-drift
  EVALUATOR_ERROR, and mixed two-member FAIL with sanitized output;
- exact detached-candidate public NPC/REFR adapter and scorer smokes: 3 passed;
- format, dependency-manifest, and `git diff --check`: passed;
- required Slice 4 runtime roots have an empty diff against `98fe8a5`;
- production source contains no fixture/held-out branching; and
- the existing public evaluator-private registry metadata is unchanged.

The machine-readable 23-file source/test/protocol inventory, matrix identity,
and calibration identity are frozen in
[`../../evaluation/evaluator-v2-stage-a-final-bounded-freeze.json`](../../evaluation/evaluator-v2-stage-a-final-bounded-freeze.json).
Historical `/2` and `/3` handoffs remain unchanged.

The owner-supplied B2 input freeze
`534373b6ef0c676f794941b0787513ed187e16d3` and blocked review
`4f6b0fbacc2c7b991201870d9aeb6d5f5b67b0c3` remain clean: no input
correction was required, and no oracle, comparison, corpus fingerprint,
freeze, tag, candidate execution, or scoring occurred. A fresh private oracle
reviewer may resume B2 once under `/4`. Stage C2 has not run, Stage D has not
started, and Slice 5 remains blocked.

Protocol `/4` is the final M1 held-out evaluator revision. If the one resumed
B2 review finds another authority gap, do not create `/5`, expand the
evaluator, or use product output as truth. Record an unresolved evaluation gap
and return to the project owner for milestone-plan disposition. No private
repository or legacy archive was accessed, and no push was performed.

## 2026-08-05 authority-completion conformance stop

An owner-authorized public authority-completion attempt added no evaluator or
product behavior. A fresh public specification author completed the missing
oracle-authoring rules, and a separate fresh product-blind reviewer uniquely
authored all fifteen fact families on an answer-free synthetic rehearsal. The
rehearsal passed its independent mechanical and parent checks.

Only after that pass did the parent inspect the frozen product source and
public tests. The unchanged candidate differs materially from the independent
specification on the allowed-field boundary, FaceGen precedence and exact
absence, fixed coverage populations, gap identities/arithmetic, and taxonomy
subjects/matrices. Exact evidence and artifact identities are recorded in
[`../../evaluation/m1-slice4-authority-completion-conformance-mismatch.md`](../../evaluation/m1-slice4-authority-completion-conformance-mismatch.md).

The stop was applied without feeding product behavior back into the
specification and without changing the candidate, evaluator, protocol,
projection, or comparison. The focused public evaluator suite passed 12 tests
with 1 expected platform-capability skip, confirming the frozen behavior under
review. No private attempt 2, oracle, qualification, corpus `2.0.0`, tag,
candidate execution, C2, Stage D, `adapt`, `score`, or `score-corpus` occurred.
Private B2 remained blocked for owner disposition.

## 2026-08-05 owner semantic disposition

The project owner resolved all six public semantic questions in ADR-0028 and
the accepted semantic-authority owner disposition. The selected contract keeps
`EDID`; uses the closed FaceGen precedence; treats loose-asset availability as
present, absent, or unknown; retains all ten backend coverage rows; uses
layered gaps; and emits a required technical taxonomy core plus only meaningful
evidence-supported assignments.

This was a public documentation-only disposition. Product code, evaluator code,
schemas, private fixtures, candidate outputs, and frozen identities were not
changed or accessed. The next authorized work is public product/specification
realignment and requalification. B2, C2, and Stage D remain blocked until a new
conforming candidate is frozen.

Documentation closeout verification:

- branch: `codex/m1-slice-4.5-semantic-disposition`;
- `git diff --check`: passed;
- all relative Markdown links in changed files: resolved;
- focused `M1Contract` plus `M1Evaluation` test run: 110 passed, 9 expected
  private/platform-capability skips, 0 failed; and
- private fixture repository and legacy archive: not accessed.

## 2026-08-05 public realignment plan acceptance

The project owner accepted the bounded
[public Bethesda semantic realignment and candidate-freeze plan](../slices/M1-slice-4.5-public-product-realignment.md).
The accepted representation uses a distinct frozen-`/4`-compatible
`record-semantic-subject` for every FaceGen loose-provider chain. Missing loose
paths remain `unknown` under the current structural-only snapshot; exhaustive
byte-verified loose-file absence authority is deferred to M3 planning in the
[deferred-question and residual-risk register](../../research/deferred-question-and-residual-risk-register.md).

This planning acceptance changes no product or evaluator implementation and
does not freeze a new candidate. Private B2, C2, Stage D, and Slice 5 remain
blocked pending implementation, independent public review, qualification, and
a new exact candidate freeze.

## 2026-08-05 public Bethesda semantic realignment closeout

The accepted clean-break public contract is implemented and frozen at candidate
commit `a98d648bd0adb2751ee0c09828e0227b1583950f` on branch
`codex/m1-slice-4.5-semantic-disposition`. The candidate advances the Bethesda
semantic schema and producer to `2.0.0`, applies the closed FaceGen precedence,
keeps undeclared loose paths `unknown`, retains all ten coverage populations,
emits layered gaps, and uses a distinct `record-semantic-subject` for every
declared FaceGen mesh or tint provider chain. Producers, coordinator publication
validation, worker staging, public protocol compatibility tests, and all affected
public test layers changed together.

The one fresh public-only independent reviewer found three material issues:
unsupported field/shape counts could collapse the same record and field across
distinct override contributions; publication did not require exactly one FaceGen
assessment for every winning NPC; and publication admitted extra taxonomy claims
on otherwise valid semantic subjects. The single permitted correction pass fixed
all three and added generic regressions. Final semantic and diff re-review found
no unresolved material issue and no fixture-specific production branch.

Exact detached-candidate evidence:

- `Infinium.Bethesda.dll`: 171,520 bytes, SHA-256
  `017de3a40a2d3b6a268bb7c024f3e053bdcaff5da7622da0fdd14dd3693d2c7d`;
- 65-DLL evaluation runtime inventory aggregate: SHA-256
  `aa207221286b8c66d4e432c560b673e4fc5ae78e5d388f7a6bdaac8878985a7a`;
- locked restore and Release build: passed, 0 warnings and 0 errors;
- focused extractor, contract, boundary, worker, oracle-agreement, and evaluator
  public-protocol verification: 61 passed, 1 expected platform-capability skip;
- `M1Unit`: 89 passed, 1 expected platform-capability skip;
- `M1Contract`: 31 passed;
- `M1Integration`: 33 passed;
- `M1Evaluation`: 54 passed, 9 expected platform or machine-identity skips;
- `M1Security`: 9 passed;
- `M1Fault`: 13 passed;
- full suite: 268 passed, 10 expected platform or machine-identity skips, 0
  failed;
- format, dependency-manifest, and `git diff --check`: passed; and
- the detached worktree remained at the exact candidate with no staged or
  normalized content diff.

The complete artifact, dependency, command-result, review, and boundary evidence
is append-only in
[`../../evaluation/m1-slice4.5-public-product-candidate-freeze.json`](../../evaluation/m1-slice4.5-public-product-candidate-freeze.json).
The frozen evaluator remains exact commit
`3693d19563c636cd2879804633ca4ce52448d2c1`, protocol `/4`, with projection,
canonicalizer, schemas, scorer, adapter, and calibration unchanged. Its public
calibration ran twice with PASS and byte-identical 15,220-byte evidence, SHA-256
`32470f8ab69c53ef48b9b24947b3eb4b4e782cbb2cfc645345df0d3029c12f36`.

No evaluator-private repository or legacy archive content was accessed. Private
B2, `adapt`, `score`, `score-corpus`, C2, Stage D, protocol `/5`, and live or
billable calls were not run. The candidate was not amended and no push was
performed. One fresh private oracle reviewer may now resume B2 once under frozen
protocol `/4`; all later authority boundaries remain unchanged.
