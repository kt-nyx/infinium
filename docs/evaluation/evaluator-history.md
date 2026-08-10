# Evaluator history and current disposition

Status: Accepted
Disposition: Historical navigation

Last reviewed: 2026-08-10

Authority: ADR-0032, ADR-0033, the accepted Slice 4.5 closeout, and the
repository evaluation authority inventory

## Current disposition

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and cannot be resumed or reused.
- Protocol `/4` is retired and archived outside the active repository under
  ADR-0033. It has no current execution, test, review, or authority role.
- Ordinary product work uses public staged fixtures and the M1 continuation
  verification profile. It must not inspect the evaluator-private repository.

## Condensed chronology

| Period | Outcome |
|---|---|
| Evaluator v1 | The run was invalidated and produced no product verdict. |
| Evaluator v2 protocols `/2` and `/3` | A `/2` Stage C invocation occurred, but Stage C.5 invalidated its verdict. `/3` was superseded before a valid successor corpus existed. |
| Protocol `/4` | Public calibration and bounded semantic authority were frozen at evaluator commit `3693d19563c636cd2879804633ca4ce52448d2c1`. The authorized B2 resume stopped before oracle construction, candidate execution, scoring, or verdict. Later public proof work classified a representation gap. |
| Protocol `/5` | Successor-model and composition-proof attempts hard-stopped before qualification. No implementation, freeze, private use, or verdict exists. |
| Deferral closeout | ADR-0032 retired `/5`, deferred private held-out evaluation, limited `/4` to bounded regression, and allowed M1 product work to continue with layered public evidence. Slice 4.5 then closed by owner disposition. |
| Protocol `/4` retirement | ADR-0033 removed the remaining bounded-regression implementation and integration from Infinium and preserved the exact public closure in the separate non-authoritative `../infinium-evaluator-archive/` repository. |

Historical correction limits, one-shot rules, and terminal stops applied only
to their named evaluator operations. They are not the correction policy for
ordinary product development.

## Retained navigation

- [Product/evaluator boundary](product-evaluator-boundary.md)
- [Repository authority inventory](repository-evaluation-authority.v1.json)
- [Evaluator v2 governance](evaluator-private-fixture-governance-v2.md)
- [Retired asset Git identities](retired-evaluation-assets.v1.json)
- [Protocol `/4` retirement decision](../architecture/decisions/ADR-0033-retire-and-archive-protocol-4-evaluator.md)
- [Slice 4.5 record](../plans/milestones/m1/slices/s4.5/record.md)

## Normalization and recovery

Superseded evaluator plans, incident narratives, intermediate freezes,
proof-only fixtures, occurrence ledgers, and protocol `/5` records were
removed from the active tree during Slice 5 pre-closeout normalization. Every
removed file is recorded by its former path and exact Git blob in
`retired-evaluation-assets.v1.json`, whose source commit is
`fcf71e184b7544a964530d581792c4948d47cda6`. Git recovery preserves the exact
historical bytes; those bytes do not regain current authority when recovered.

ADR-0033 subsequently retired the remaining protocol `/4` runtime, schemas,
test project, wrapper, profile, freeze, and matrix at Infinium source commit
`f73a2659a07305702eda775699cacd612f8d9fe2`. Their exact paths and Git blobs
are recorded in the same inventory. A convenience snapshot exists at sibling
repository `../infinium-evaluator-archive/`, commit
`c490de9689d8e9f8dfc7eccb3d056ab5b083e9fd`; it is excluded from ordinary
product context and has no executable or evidentiary authority.

Historical plan-path families normalize as follows:

| Former family | Current navigation |
|---|---|
| `docs/plans/slices/M1-slice-4.5-*` | This history plus the [Slice 4.5 plan and record](../plans/milestones/m1/slices/s4.5/README.md) |
| `docs/plans/implementation-records/M1-slice-*.md` | The owning slice directory under `docs/plans/milestones/m1/slices/` |
| `docs/evaluation/fixtures/m1-slice5-*` | Functional public-fixture families under `fixtures/public/` |
| `docs/evaluation/fixtures/independent-slice3-evaluator-20260729/` and `protocol-4-oracle-authorability/` | Git-only historical evidence in the retired-asset inventory |

Do not reconstruct a retired evaluator workflow from these mappings. They are
for historical interpretation and byte recovery only.
