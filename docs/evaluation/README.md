# Evaluation

Status: Active navigation

Last reviewed: 2026-08-10

Evaluation documents define how product claims are demonstrated. They do not
make implementation, fixture, or product-acceptance claims by themselves.

## Product verification

- [Evaluation strategy](evaluation-strategy.md)
- [Case catalog](case-catalog.md)
- [Fixture guidelines](fixture-guidelines.md)
- [Anti-overfitting rules](anti-overfitting-rules.md)
- [M1 evaluation baseline](m1-evaluation-baseline.md)
- [M1 continuation verification profile](m1-continuation-verification-profile.md)
- [Platform and operational specifications](specifications/m1-platform-and-operational.md)
- [Semantic and ground-truth specifications](specifications/m1-semantic-and-ground-truth.md)
- [Platform fixture catalog](specifications/platform-fixture-catalog.md)
- [Semantic fixture catalog](specifications/semantic-fixture-catalog.md)

Executable public fixtures live under `test-data/public-fixtures/`; their tools
live under `tools/fixtures/`. Documentation contains specifications and
navigation, not executable fixture packages.

## Authority and evaluator boundary

- [Product/evaluator boundary](product-evaluator-boundary.md)
- [Repository authority inventory](repository-evaluation-authority.v1.json)
- [Evaluator-private governance v2](evaluator-private-fixture-governance-v2.md)
- [Evaluator history and current disposition](evaluator-history.md)
- [Retired asset Git inventory](retired-evaluation-assets.v1.json)

Frozen protocol `/4` remains at its required historical paths and can run only
through the bounded regression wrapper described in
[bounded regression usage](m1-slice4-protocol-4-bounded-regression-usage.md).
Protocol `/5` and predecessor evaluator attempts are Git-only history, not
current navigation or implementation inputs.
