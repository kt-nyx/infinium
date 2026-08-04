# M1 Slice 4.5 — Held-out evaluation v2 implementation record

Status: Stage A in progress; private qualification and scoring not run
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

To be completed after implementation and review:

- public evaluator-v2 tool location:
- files moved out of production assemblies:
- protocol and schema IDs:
- calibration commands/results:
- boundary and anti-overfitting checks:
- full verification:
- reviewer and correction pass:
- frozen public evaluator-v2 commit:
- final head and status:
- push state:

## Later stages

- Stage B private corpus qualification: not run.
- Stage C held-out scoring: not run.
- Evaluator-v2 held-out result: not run.
- Slice 4.5 overall completion: pending later stages.
