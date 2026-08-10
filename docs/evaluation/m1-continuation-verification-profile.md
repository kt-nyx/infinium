# M1 continuation verification profile

Status: Accepted and effective

Last reviewed: 2026-08-10

Date: 2026-08-08

Authority: [ADR-0032](../architecture/decisions/ADR-0032-defer-m1-held-out-evaluator-and-continue-public-verification.md)
and the accepted
[evaluator-deferral and M1-continuation plan](../plans/milestones/m1/slices/s4.5/plan.md)

Applies to: M1 Slices 5 through 9

Execution policy: [Development execution policy](../execution-policy.md)

## Claim and activation boundary

This is the normative development and validation gate that replaces the
held-out-`PASS` sequencing prerequisite for the remaining M1 slices.
`M1/S4.5/EVAL-CLOSEOUT` is accepted and complete, so Slice 4.5 is closed.
The current eligible package is stated in
[`../current-state.md`](../current-state.md). The profile's activation evidence
is the [closeout acceptance record](evaluator-history.md).

The profile proves public product conformance within each accepted slice's
declared scope. It does not produce a private held-out verdict, qualify an
evaluator or corpus, demonstrate production reliability/readiness, or complete
M1 by itself. Protocol `/4` bounded regression is optional supporting tool
health evidence only where its allowlisted representable subset is relevant;
it is not one of the six replacement layers and cannot satisfy any layer on its
own.

This profile defines evidence required for acceptance; it does not impose a
finite correction budget on ordinary product work. A failed command, fixture
defect, schema mismatch, incomplete implementation, or review finding returns
to correction and re-review under the development execution policy. Only an
authority decision or safety/isolation escalation condition pauses the affected
path. Evaluator-specific freeze and no-retry rules remain confined to a
separately authorized evaluator task.

For every row below, the owning slice must retain the exact executed command,
commit, fixture and result identities, pass/fail/skip counts, unsupported
surfaces, coverage and gaps, and fresh-review result in its implementation
record. A listed case remains pending until retained evidence from the owning
slice shows that its applicable assertions passed.

## Common command floor

Every Slice 5-9 implementation runs the applicable focused commands during
development and, before acceptance, this accumulated public floor from the
repository root:

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

Each accepted slice execution plan must add exact commands for its new
contracts, fixtures, manifests, replay artifacts, security boundaries, and
end-to-end operations. A placeholder command, broad green suite, or historical
result does not satisfy an unexercised obligation.

## Six required layers

### Layer 1 — Contract and schema conformance

Accepted public product requirements and ADRs are the only source of expected
behavior. Producers, consumers, storage, wire artifacts, exports, and replay
must be updated together for a clean-break contract change. Tests directly
exercise schema, canonicalization, typed null/unknown/omission, terminal,
coverage, and gap behavior. Unsupported states fail or degrade exactly as
declared.

| Mapping | Required value |
|---|---|
| Requirements | `EVID-001` through `EVID-003`, `EVID-006`, `COVER-001` through `COVER-003`, `ANALYSIS-016`, `ANALYSIS-019`, `SNAP-005`, `SNAP-006`, `OPS-002`, `OPS-003` |
| Cases | EVAL-0052, EVAL-0065, EVAL-0067, EVAL-0082, EVAL-0083, EVAL-0085, EVAL-0086, plus every slice-specific contract case |
| Evidence | Versioned schemas/contracts; producer-consumer-storage-wire-output-replay compatibility evidence; positive, invalid, null, unknown, omitted, unsupported, terminal, coverage, and gap assertions; exact schema and producer identities |
| Commands | Common `M1Unit`, `M1Contract`, `M1Integration`, and `M1Evaluation` commands; schema/manifest parsers and slice-specific round-trip commands named by the accepted slice plan |
| Owning slices | Each slice owns its changed contracts; Slice 5 first owns evidence/case/replay contracts, Slice 6 provider-operation contracts, Slice 7 generic analyzer contracts, Slice 8 controlled-real bindings, and Slice 9 the stable end-to-end output contract |

### Layer 2 — Independently expected public fixtures

Expected values are pre-authored from format rules, retained bytes,
authoritative documentation, or explicit manual adjudication independent of
the implementation path under test. Every positive has a meaningful negative
or abstention case. Expected-output changes require new independent evidence
and review. Product output is never copied into an expected fixture as truth.

| Mapping | Required value |
|---|---|
| Requirements | `EVID-002`, `EVID-003`, `EVID-006`, `EVID-007`, `COVER-001`, `ANALYSIS-003` through `ANALYSIS-005`, `ANALYSIS-019` |
| Cases | EVAL-0001, EVAL-0002, EVAL-0016, EVAL-0017, EVAL-0032, EVAL-0052, EVAL-0085, EVAL-0086 |
| Evidence | Pre-execution expected observations and outcomes; independent byte/format/source/adjudication provenance; partition history; matched negative or abstention for every positive; reviewed explanation and successor evidence for any expectation change |
| Commands | Common `M1Contract` and `M1Evaluation` commands; deterministic fixture-generation/validation commands and exact oracle/expectation checks declared by the owning slice without using product output |
| Owning slices | Slice 5 uses staged ownership: WP2 owns documentation/claim/provenance cases, WP3 owns joins/candidates/hypotheses/abstention including scale/stress, WP4 owns findings/cases/taxonomy/lineage/coverage/gaps, WP5 owns publication/replay/recovery/query/output/platform cases, and WP6 assembles the comprehensive cross-stage corpus. Slice 7 owns both synthetic domain packages; Slice 8 owns the controlled-real EVAL-0016/EVAL-0017 packages; Slice 9 audits retained expected-value provenance across the complete runs |

### Layer 3 — Model-derived, mutation, and metamorphic checks

The owning slice exercises all bounded state classes relevant to its change,
including missing, malformed, unsupported, ambiguous, and partial evidence.
Renaming identities and reordering unrelated inputs preserve semantics;
changing one relevant dependency changes only dependent output. Tests prove
forbidden facts remain absent and retain complete raw candidates, failures,
abstentions, coverage, and gaps.

| Mapping | Required value |
|---|---|
| Requirements | `EVID-006`, `EVID-007`, `COVER-001` through `COVER-003`, `ANALYSIS-003` through `ANALYSIS-005`, `ANALYSIS-016`, `ANALYSIS-019`, `SNAP-002`, `SNAP-004` |
| Cases | EVAL-0001, EVAL-0002, EVAL-0026, EVAL-0032, EVAL-0052, EVAL-0082, EVAL-0085, EVAL-0086 |
| Evidence | State-class inventory; positive/negative/abstention matrix; mutation and metamorphic outcomes; dependency-change closure; forbidden-fact-absence assertions; retained raw candidate/failure/coverage/gap artifacts |
| Commands | Common `M1Unit`, `M1Contract`, and `M1Evaluation` commands plus deterministic model/state enumeration, mutation, metamorphic, and raw-artifact validation commands defined by each accepted slice plan |
| Owning slices | Each slice owns changed-state coverage; Slice 7 owns the complete generic-mechanism mutation/metamorphic gate; Slice 8 repeats applicable transformations against controlled-real packages; Slice 9 audits accumulated coverage |

### Layer 4 — Determinism, replay, and operational safety

Clean and incremental execution agree for identical resolved inputs. Retained
replay reproduces deterministic downstream artifacts. Output paths and writes
remain confined, dependency or identity drift fails closed, and the full
applicable unit, contract, integration, evaluation, security, fault, format,
and manifest checks pass.

| Mapping | Required value |
|---|---|
| Requirements | `SNAP-001` through `SNAP-006`, `SCAN-005` through `SCAN-007`, `SCAN-009`, `SEC-001` through `SEC-004`, `AUTH-001` through `AUTH-003`, `OPS-001` through `OPS-003`, `AI-003`, `AI-004`, `AI-006`, `AI-007` |
| Cases | EVAL-0026, EVAL-0033 through EVAL-0040 as applicable, EVAL-0046, EVAL-0064, EVAL-0076, EVAL-0077, EVAL-0080 through EVAL-0083, EVAL-0087 through EVAL-0089 |
| Evidence | Repeated clean/incremental and replay artifact identities; dependency and resolved-input manifests; write/non-mutation and secret-canary reports; lifecycle/fault evidence; exact build, format, dependency, and full-suite results |
| Commands | Entire common command floor; slice-specific clean/incremental comparison, replay verification, write-confinement, non-mutation, fault, credential, budget, and manifest commands |
| Owning slices | Slices 5-8 own each operational surface they introduce; Slice 9 owns complete clean/replay equivalence, non-mutation/secret reports, and the accumulated operational gate |

### Layer 5 — Generalization and controlled-real evidence

Slice 7 must prove the generic mechanism across the two materially different
accepted domains—actor/AI/FaceGen and REFR/link/placement—with matched
negatives. Slice 8 must run the qualified controlled-real EVAL-0016 and
EVAL-0017 packages. Any case that changes implementation is development or
validation evidence, not held-out evidence, and unevaluated taxonomy regions
remain explicit gaps.

| Mapping | Required value |
|---|---|
| Requirements | `ANALYSIS-003` through `ANALYSIS-005`, `ANALYSIS-016`, `FIND-001` through `FIND-004`, `FIND-011`, `FIND-014`, `COVER-002`, `COVER-003` |
| Cases | EVAL-0001, EVAL-0002, EVAL-0016, EVAL-0017, EVAL-0032, EVAL-0079, EVAL-0084 through EVAL-0086 |
| Evidence | Two generic-domain positive/negative packages and shared-mechanism proof; qualified controlled-real manifests, fingerprints, purpose passages, positives and patch controls; taxonomy-stratified coverage and explicit unevaluated gaps; partition-transition records for product-driving cases |
| Commands | Slice 7 synthetic generic-mechanism command(s), full applicable `M1Evaluation`, and Slice 8 exact EVAL-0016/EVAL-0017 manifest-validation and execution commands named by their accepted execution plan |
| Owning slices | Slice 7 owns the two-domain generic proof. Slice 8 owns EVAL-0016 and EVAL-0017 controlled-real execution. Slice 9 verifies both are present in the required-case result index |

### Layer 6 — Fresh review and claim control

Every later slice receives a fresh semantic and diff review against its
accepted plan and public authority. Passing tests do not replace review of
correctness, completeness, provenance, gaps, and plan drift. Each
implementation record states exactly what passed, what remains unsupported,
and that no private held-out verdict exists. No M1 or later claim uses
`held-out`, `independently validated`, `reliable`, `ready`, or equivalent
language beyond the evidence actually obtained.

Reviewers classify findings as must-fix, follow-up, non-blocking,
owner/authority decision, or safety/isolation breach and return `ACCEPT`,
`CORRECT`, or `ESCALATE`. `CORRECT` may repeat until must-fix findings close;
correction count is retained as evidence, not used as a stop threshold.

| Mapping | Required value |
|---|---|
| Requirements | `EVID-002`, `EVID-006`, `EVID-007`, `COVER-001` through `COVER-003`, `ANALYSIS-016`, `PROD-002`, `PROD-004`, `SNAP-006` |
| Cases | Every case claimed by the slice, with EVAL-0083, EVAL-0084, and EVAL-0085 always reviewed for provenance, case construction, coverage, and claim wording |
| Evidence | Fresh review record with exact input commit, authority/plan checklist, findings, correction count, final judgment, changed paths, verification results, unsupported/gap inventory, private-access prohibition, and claim-boundary statement |
| Commands | `git diff --check`, changed-path/protected-path scan, relative-link validation, strict changed-JSON parsing, status/claim occurrence scan, common full suite, and every owning-slice command cited in the review record |
| Owning slices | Every Slice 5-9 implementation record owns its review and claim boundary; Slice 9 owns the final requirement/case/slice traceability and M1 completion review |

## Slice sequencing and implementation records

- Slice 5 packages must satisfy their applicable Layers 1-4 and 6 in the
  dependency order declared by the active plan; `current-state.md` identifies
  the live handoff.
- Slice 6 must satisfy applicable Layers 1-4 and 6 before any live provider
  operation; live authorization remains separate and bounded.
- Slice 7 must satisfy all applicable layers and the exact two-materially-
  different-domain obligation in Layer 5.
- Slice 8 must satisfy all six layers, including EVAL-0016 and EVAL-0017 with
  their package-specific matched controls.
- Slice 9 must satisfy all six layers across the accumulated M1 surface and
  produce the required-case index, stable versioned end-to-end output, and M1
  completion record.

Each Slice 5-9 implementation record must contain: accepted plan and input
commit; implementation commit; changed paths and intentional behavior; exact
contract/schema/fixture/run identities; commands and pass/fail/skip counts;
coverage, gaps, abstentions, and unsupported surfaces; clean/replay and safety
evidence where applicable; fresh-review inputs, findings, corrections, and
judgment; proof that no private material or held-out verdict entered the work;
and the precise next-slice/M1 status.

## Protocol `/4` supporting regression

When a later public change touches the `/4` allowlisted representable subset,
run the bounded wrapper described in
[the bounded-regression usage contract](m1-slice4-protocol-4-bounded-regression-usage.md).
`BOUNDED_REGRESSION_PASS` means only historical freeze integrity, current
reusable-core integrity, and allowlisted current public regression health. The
known partial `RACE/DATA` gap remains excluded. This result cannot satisfy a
held-out, reliability, readiness, Slice 4.5, M1, or complete-product claim.

## Future evaluator reconsideration

A new evaluator plan may be proposed only after Slice 9 and during M3 planning,
when all of the following hold:

1. a stable versioned end-to-end output contract exists;
2. the surface is limited to user-meaningful semantic outcomes rather than
   internal IDs, incidental prose, or implementation-specific diagnostics;
3. every expected value is independently authorable without product output or
   the implementation path under test;
4. an answer-free totality and authorability review passes before candidate or
   private access;
5. public evaluator implementation, private corpus qualification, scoring, and
   closeout retain separate roles and authority; and
6. a new accepted ADR and milestone plan define the protocol identity, scope,
   correction limits, contamination handling, and claim boundary.

No future protocol identity is selected here. Retired protocol `/5` cannot be
reused, and neither `/6` nor private work is authorized by this profile.
