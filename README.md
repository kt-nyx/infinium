# Infinium

Infinium is an evidence-driven pre-playthrough quality-assurance and diagnostic
tool for large Skyrim Special Edition modlists managed with Mod Organizer 2.
The rebuilt product is being delivered incrementally and is not yet an
end-user-ready preflight application.

Start with [Infinium documentation](docs/README.md), then follow the single
[current project state](docs/current-state.md) handoff. Do not infer current
work from historical plans or evaluator records.

## Repository status

- M0 research is complete; M1 backend semantic proof is active.
- Slice 5 is undergoing owner-approved pre-closeout repository normalization
  before its implementation, fixtures, and contracts are revalidated and
  presented for owner acceptance.
- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified. Frozen protocol `/4` is bounded
  historical regression tooling only.
- The abandoned implementation is outside the repository at
  `../infinium-legacy-archive/` and remains recoverable through Git commit
  `7dd3da6`; do not inspect it without explicit archaeological authorization.
- Superseded evaluator-development staging is consolidated at
  `../infinium-evaluator-development-archive/` and is likewise out of scope
  unless explicitly authorized.

## Primary navigation

- [Product definition](docs/product/product-definition.md)
- [Requirements](docs/product/requirements.md)
- [Architecture decisions](docs/architecture/decisions/README.md)
- [Execution policy](docs/execution-policy.md)
- [M1 plan hierarchy](docs/plans/milestones/m1/README.md)
- [Evaluation strategy](docs/evaluation/evaluation-strategy.md)
- [Evaluator history and current disposition](docs/evaluation/evaluator-history.md)

Historical evaluator packages, retired fixture identities, and superseded
attempt plans are not implementation inputs. Their exact recoverable identities
are listed in the evaluator-history Git inventory.
