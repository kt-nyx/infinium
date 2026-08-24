# Product, public-fixture, and evaluator boundary

Status: Accepted
Disposition: Current repository authority map

Last reviewed: 2026-08-23

Authority: ADR-0032, ADR-0033, ADR-0035, and project-owner-authorized repository boundary hardening

The namespace or path containing an artifact does not establish its authority.
Use `repository-evaluation-authority.v1.json` for the machine-readable current
inventory, `retired-evaluation-assets.v1.json` for Git-backed retirements, and
[Evaluator history](evaluator-history.md) for the condensed chronology.

| Authority class | Rule |
|---|---|
| Product contracts and codecs | Current product meaning comes only from accepted product/architecture authority and the active contract implementations. Evaluator identities confer no product meaning. |
| Public fixture packages | A registered package may establish only its bounded, independently authored expectations; never product-derived oracle truth or a product/evaluator verdict. |
| Developer-owned conformance evidence | Current M1/M2 examples, mutations, deterministic references, integrations, replay, safety checks, and reviews may establish product conformance within delivered scope; they do not establish an independent semantic verdict. |
| Historical semantic packages | Semantic-admission v1-v13 remain byte/hash-visible development history. Their current authority package is none; they cannot gate M1/M2 or be compared with current product output. |
| Independent semantic qualification | Deferred until the M3 Evaluation Readiness Gate after M2 acceptance. Only a new accepted M3 evaluation plan can authorize a bounded feasibility package and any authoring, review, sealing, or comparison. |
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
