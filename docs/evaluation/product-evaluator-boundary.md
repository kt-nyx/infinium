# Product, public-fixture, and evaluator boundary

Status: Accepted
Disposition: Current repository authority map

Last reviewed: 2026-08-10

Authority: ADR-0032, ADR-0033, and project-owner-authorized repository boundary hardening

The namespace or path containing an artifact does not establish its authority.
Use `repository-evaluation-authority.v1.json` for the machine-readable current
inventory, `retired-evaluation-assets.v1.json` for Git-backed retirements, and
[Evaluator history](evaluator-history.md) for the condensed chronology.

| Authority class | Rule |
|---|---|
| Product contracts and codecs | Current product meaning comes only from accepted product/architecture authority and the active contract implementations. Evaluator identities confer no product meaning. |
| Public fixture packages | A registered package may establish only its bounded, independently authored expectations; never product-derived oracle truth or a product/evaluator verdict. |
| Retired evaluator protocols | Retired identities are permanently reserved and recoverable only through the history inventory or excluded archive. They have no current command, test, review, or authority role. |
| Repository governance metadata | The authority and retirement inventories classify repository paths and Git objects only; they create no product semantics or evaluator verdict. |

Product capability boundaries are exactly `provider`, `hosted-search`, `nexus`,
and `loot`. `evaluator-private` and `held-out` remain only as accepted older
public-fixture provenance vocabulary; they are not product capabilities.

Current public-fixture paths and identities are listed only in the
machine-readable repository authority inventory. Each registered package owns
its exact manifest identity, version, and hashes. This boundary intentionally
does not duplicate that mutable inventory or its seals. Registered public
packages establish only their bounded expectations; they do not establish a
private verdict or broader product readiness or reliability claims.
