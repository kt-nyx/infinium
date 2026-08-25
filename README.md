# Infinium

Infinium is an evidence-driven pre-playthrough quality-assurance and diagnostic
tool for large Skyrim Special Edition modlists managed with Mod Organizer 2.
The rebuilt product is being delivered incrementally and is not yet an
end-user-ready preflight application.

Start with [Infinium documentation](docs/README.md), then follow the single
[current project state](docs/current-state.md) handoff. Do not infer current
work from historical plans or evaluator records.

## Repository status

- The live milestone, slice, and next eligible action are stated only in
  [current project state](docs/current-state.md).
- Private held-out evaluation is deferred and has no valid current product
  verdict; protocols `/4` and `/5` are retired and have no active execution,
  testing, review, or authority role.
- The abandoned implementation is outside the repository at
  `../infinium-legacy-archive/` and remains recoverable through Git commit
  `7dd3da6`; do not inspect it without explicit archaeological authorization.
- Superseded evaluator-development staging and completed milestone chronology
  are consolidated at `../infinium-development-history-archive/` commit
  `6f8976db6c560456201a9166caf4f36506be5477` and are likewise out of scope
  unless explicitly authorized.
- Retired public protocol `/4` is preserved in the excluded sibling repository
  `../infinium-evaluator-archive/` and must not be inspected or restored during
  ordinary product work.

## Primary navigation

- [Product definition](docs/product/product-definition.md)
- [Requirements](docs/product/requirements.md)
- [Architecture decisions](docs/architecture/decisions/README.md)
- [Execution policy](docs/execution-policy.md)
- [Milestone planning boundary](docs/plans/milestones/README.md)
- [Post-M1 cleanup closeout](docs/plans/transitions/post-m1-cleanup/README.md)
- [Evaluation strategy](docs/evaluation/evaluation-strategy.md)
- [Evaluator history and current disposition](docs/evaluation/evaluator-history.md)

Historical evaluator packages, retired fixture identities, and superseded
attempt plans are not implementation inputs. Their current disposition and
recovery boundary are summarized by the evaluator-history pointer.
