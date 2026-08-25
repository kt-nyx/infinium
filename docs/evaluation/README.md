# Evaluation

Status: Accepted
Disposition: Active navigation

Last reviewed: 2026-08-23

Evaluation documents define how product claims are demonstrated. They do not
make implementation, fixture, or product-acceptance claims by themselves.

[ADR-0035](../architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md)
defers independent semantic-oracle qualification throughout M1 and M2. The
[active profile](product-conformance-verification-profile.md) requires ordinary
product conformance and reserves independent evaluation for the M3 Evaluation
Readiness Gate after M2 acceptance.

## Product verification

- [Evaluation strategy](evaluation-strategy.md)
- [Case catalog](case-catalog.md)
- [Fixture guidelines](fixture-guidelines.md)
- [Anti-overfitting rules](anti-overfitting-rules.md)
- [Archived M1 evaluation baseline](evaluator-history.md)
- [Product-conformance verification profile](product-conformance-verification-profile.md)
- [Archived platform and operational specifications](evaluator-history.md)
- [Archived semantic and ground-truth specifications](evaluator-history.md)
- [Platform fixture catalog](specifications/platform-fixture-catalog.md)
- [Semantic fixture catalog](specifications/semantic-fixture-catalog.md)

Executable public fixtures live under `fixtures/public/`; their tools
live under `fixtures/tooling/`. Documentation contains specifications and
navigation, not executable fixture packages.

## Authority and evaluator boundary

- [Product/evaluator boundary](product-evaluator-boundary.md)
- [Repository authority inventory](repository-evaluation-authority.v1.json)
- [Evaluator-private governance v2](evaluator-private-fixture-governance-v2.md)
- [Evaluator history and current disposition](evaluator-history.md)
- [Historical evaluator archive pointer](evaluator-history.md)
- [Retired asset Git inventory](retired-evaluation-assets.v1.json)

Protocols `/4` and `/5` and all predecessor evaluator attempts are retired
history, not current navigation, review gates, or implementation inputs. The
last public `/4` closure is preserved in the excluded sibling archive recorded
by [ADR-0033](../architecture/decisions/ADR-0033-retire-and-archive-protocol-4-evaluator.md)
and the retired-asset inventory.
