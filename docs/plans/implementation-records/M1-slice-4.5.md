# M1 Slice 4.5 — Held-out evaluation v2 implementation record

Status: Stage A corrective pass complete; successor evaluator frozen; private qualification and scoring not run
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
Stage B is unblocked and must autodiscover the unique frozen evaluator from the
tracked freeze handoff. Branch publication target:
`codex/m1-slice-4.5-evaluator-v2`.

## Later stages

- Stage B private corpus qualification: not run.
- Stage C held-out scoring: not run.
- Evaluator-v2 held-out result: not run.
- Slice 4.5 overall completion: pending later stages.
