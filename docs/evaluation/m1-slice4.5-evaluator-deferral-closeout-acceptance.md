# M1 Slice 4.5 evaluator-deferral closeout acceptance

Status: Accepted

Accepted: 2026-08-07

Accepted by: Project owner through the accepted
`M1/S4.5/EVAL-CLOSEOUT` plan and its owner-authorized resume

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP5`

## Acceptance judgment

`M1/S4.5/EVAL-CLOSEOUT` is accepted and complete. Slice 4.5 closes as
public conformance complete for the exact frozen Slice 4 candidate and scope,
with private held-out evaluation deferred and no valid current private product
verdict. The missing held-out verdict remains an explicit residual risk; it is
not a reliability, readiness, M1-completion, or hidden-evaluation claim.

The accepted terminal status is:

```text
Slice 4 public conformance: passed for its exact frozen candidate and scope.
Protocol /4: frozen historical evaluator; bounded public regression use only.
Protocol /5: retired unqualified; no implementation or verdict.
Private held-out evaluation: deferred; no valid current product verdict.
Slice 4.5: closed by owner disposition with explicit residual risk.
Slice 5: eligible to begin under the M1 continuation verification profile.
M1: active.
```

This block records the accepted 2026-08-07 closeout state. Current Slice 5
status is WP1 complete and reviewed; WP2 is next.

No private corpus, expected output, answer-bearing material, candidate/product
output, B2, C2, Stage D, adaptation, comparison, scoring, live call, or
billable call was used. No push occurred.

## Exact public commit chain

| Package | Commit | Accepted result |
|---|---|---|
| WP0 evidence preservation | `2b41ad8c06f3da0f692cd963b524ff5b5d279bd0` | Failed WP1V proof evidence preserved without qualifying `/5` |
| WP1 authority | `5541feb5d0c3950477ab63eb2f32a46a84a203ce` | ADR-0032 accepted; evaluator deferred; durable semantics migrated |
| WP2/T1 clarification | `be1340d1f343eab07d2b615c9e12fd03ce49da29` | Historical freeze and current regression boundaries separated |
| WP2 retirement/tooling | `738b2333088c2df018f18466e05660f920879061` | Active `/5` machine surface removed; bounded `/4` tooling accepted |
| WP3 continuation/status | `2462a990ccb6a1f6de55f3c3b14ddef16c461261` | Six-layer continuation profile and repository status ledger accepted |
| WP4 correction | `40b863471031d024b858f781ee1678fd95171ed9` | Ledger section classifications corrected after independent review |
| WP5 closeout | commit containing this record | Full public verification, final status, and Slice 5 handoff |

The branch remained `codex/m1-slice-4.5-protocol-5-successor`. Commits were
not amended, squashed, rebased, force-pushed, or pushed.

## Protocol `/4` retained boundary

The immutable freeze manifest remains 6,972 bytes with SHA-256
`2e30980f9e8628bf88c519e12c510c86a9c3ff2f6a7374b796fd8e6b769907d6`.
All 23 manifest paths match their Git blobs at frozen evaluator commit
`3693d19563c636cd2879804633ca4ce52448d2c1`. All 20 current non-test
runtime/schema paths remain byte-identical to that freeze.

The three current public regression tests evolved only through authorized
public product-realignment commit
`a98d648bd0adb2751ee0c09828e0227b1583950f` and have these current
identities:

| Current public regression test | Bytes | SHA-256 |
|---|---:|---|
| `tests/Infinium.EvaluationTests/BethesdaOracleAgreementEvaluationTests.cs` | 30,972 | `f02b67afaad9a22d893a0b819fa33175c6a2d256db8d8255c45241c5b51a4a51` |
| `tests/Infinium.EvaluationTests/BethesdaSemanticExtractionEvaluationTests.cs` | 20,267 | `73e0ee3c3f4c617982e7d0e5de0d596feee4d958a951d8ef7fc9418b8084991a` |
| `tests/Infinium.EvaluationTests/EvaluatorV2PublicProtocolTests.cs` | 34,302 | `c7c99bcf234ad3a72a1e04a52fa9835fb3d1f912c95b0b0aaf80a22bdcb5b01f` |

They are current regression evidence, not original qualification bytes. The
known partial `RACE/DATA` state remains excluded. `BOUNDED_REGRESSION_PASS`
means only historical-freeze, reusable-core, calibration, and allowlisted
current public-regression health.

## Protocol `/5` retirement

Protocol `/5` hard-stopped before implementation or freeze. Its last complete
failed-evidence checkpoint is
`2b41ad8c06f3da0f692cd963b524ff5b5d279bd0`. WP2 removed these 13 active
machine paths:

- `docs/evaluation/specifications/m1-slice4-protocol-5-global-composition-summary.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-contract-summary.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-document.schema.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-contract.md`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-model.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-model.schema.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-rule-coverage-ledger.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-successor-evidence-contract.md`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-successor-model.schema.json`;
- `eng/build-m1-slice4-protocol5-rule-ledger.ps1`;
- `eng/validate-m1-slice4-protocol5-global-composition.ps1`; and
- `eng/validate-m1-slice4-protocol5-projection-contract.ps1`.

The consumed `/5` identities remain reserved only to prevent reuse. There is
no `/5` implementation, qualification, freeze, private use, verdict, or resume
authority, and no `/6` or other future protocol identity is selected.

## Independent WP4 review

Reviewer A audited evaluator boundaries at exact clean commit
`2462a990ccb6a1f6de55f3c3b14ddef16c461261` and returned
`WP4 REVIEWER A ACCEPT` with no finding. It independently reproduced both
PowerShell-host wrapper/refusal results, the 23/20/3 identity split, the
immutable freeze, the excluded gap, the absence of active `/5`, and the public
claim boundary.

Reviewer B initially returned `WP4 REVIEWER B REJECT`. Its mechanical ledger
reproduction was exact, but it found current retirement dispositions in
ADR-0030/0031 classified as historical and historical revision-3 gate text
classified as current normative. The single shared WP4 correction changed only
the ledger policy/generated classifications. Reviewer B then returned
`WP4 REVIEWER B ACCEPT`: 366 files, 3,658 rows, 9,667 occurrences, zero
row/file/term differences, with 7,655 current normative, 713 current status,
and 1,299 historical occurrences.

No replacement reviewer was used. Reviewer A made no finding. Reviewer B's
one finding set used the one shared WP4 correction and one targeted re-review.

## WP5 public verification

The parent began WP5 from clean reviewed commit
`40b863471031d024b858f781ee1678fd95171ed9` and ran the full public command
floor. Locked restore passed. Release build passed with zero warnings and zero
errors. The literal `Category=` filters passed:

| Filter | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `M1Unit` | 89 | 0 | 1 |
| `M1Contract` | 31 | 0 | 0 |
| `M1Integration` | 33 | 0 | 0 |
| `M1Evaluation` | 54 | 0 | 9 |
| `M1Security` | 9 | 0 | 0 |
| `M1Fault` | 13 | 0 | 0 |

The supplementary `TestCategory=` filters passed:

| Filter | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `M1Unit` | 99 | 0 | 1 |
| `M1Contract` | 50 | 0 | 0 |
| `M1Integration` | 34 | 0 | 0 |
| `M1Evaluation` | 73 | 0 | 9 |
| `M1Security` | 95 | 0 | 4 |
| `M1Fault` | 93 | 0 | 3 |

The unfiltered suite passed 268 tests with 0 failures and 10 expected
platform/private-availability skips. `dotnet format --verify-no-changes`, the
dependency-manifest check, and `git diff --check` passed.

The bounded wrapper ran twice under Windows PowerShell 5.1 and twice under
PowerShell 7. All four normalized stdout streams were byte-identical at
SHA-256 `23453b58e141b03267c8b17d25bf1c3fb610154e7ea041d8e6ac8e523d151e85`.
Each reported 23/23 historical blobs, 20/20 current core, 3/3 evolved tests,
56/56 calibration cases, 8/8 focused tests, and exact terminal
`BOUNDED_REGRESSION_PASS`. Refusal tests passed 11/11 under each host.

WP5 found and corrected two public integration drifts before final acceptance:

1. one repository-structure contract test still required the superseded phrase
   `sole active held-out evaluation protocol`; it now asserts the accepted
   `/4` historical bounded-only and `/5` retired boundaries; and
2. the public Slice 2 substrate fixture pinned an older SHA-256 for the accepted
   M1 plan. Oracle version `1.0.3` records the new independently computed plan
   fingerprint
   `201410b74f84a34d217350b2f8f433e26323b26491061c19fe98ae2d3ae47e27`
   and reseals the public oracle at
   `8673abc9adf53439139f9d1beec92824ba41b8611a4855f5978a0fb4a2b67cbb`.
   No semantic expected value changed.

Repository-wide Markdown links, tracked JSON, occurrence-ledger, removed
identity, protected-path, freeze, private-locator, and stale-status checks pass
in the final closeout state. Exact final counts are recorded in the terminal
mechanical-count section for Slice 4.5 below.

## Residual risk and next eligible work

M1 has no current private held-out verdict and therefore no independent private
reliability/readiness claim. Frozen `/4` cannot represent the complete accepted
current semantic contract. Future evaluator reconsideration requires a stable
versioned user-meaningful output after Slice 9, M3 planning, a new ADR and plan,
independently authorable expectations, answer-free totality review, and
separate implementation, private qualification, scoring, and closeout roles.

The next eligible product work is `M1/S5`, subject to its normal repository
preflight and an accepted slice execution plan. Its handoff is:

```text
Resume Infinium from the clean local commit containing
docs/evaluation/m1-slice4.5-evaluator-deferral-closeout-acceptance.md.
Work only on M1/S5. Read AGENTS.md and the complete required authority chain,
then the accepted M1 plan and M1 continuation verification profile. Verify the
branch/status and retained Slice 4.5 closeout evidence. Do not access private
evaluator fixtures, candidate/product outputs, or the legacy archive. Do not
implement until an accepted Slice 5 execution plan maps applicable continuation
Layers 1-4 and 6 to exact requirements, cases, public fixtures, commands,
coverage/gaps, replay/safety evidence, fresh review, and claim limits. No live
or billable call and no push are authorized.
```

## Final repository-wide mechanical counts

The link check covered 183 Markdown files and 1,974 inline links, including
1,260 local links, with zero unresolved local targets. Strict parsing passed
for all 185 tracked JSON files. Independent occurrence-ledger reconciliation
covered 367 source files, 3,762 rows, and 9,771 occurrences with zero missing
rows, extra rows, or term-count differences: 7,731 current normative, 711
current status, and 1,329 historical occurrences.
