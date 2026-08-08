# Product, public-fixture, and evaluator boundary

Status: Current repository authority map

Date: 2026-08-08

Authority: ADR-0032 and project-owner-authorized repository boundary hardening

The namespace or path containing an artifact does not establish its authority.
Use `repository-evaluation-authority.v1.json` for the machine-readable current
inventory and `retired-evaluation-assets.v1.json` for Git-backed retirements.

| Version axis | Current meaning | Authority rule |
|---|---|---|
| Product schema ID/version | Current clean-break product payload and codec contract under `contracts/json-schema/` and `src/` | May be consumed by product and current public fixtures; evaluator identities confer no product meaning. |
| Public fixture package/version | Independently authored public evidence validated by `Infinium.PublicFixtures` | May establish only its registered public fixture expectations; never product-derived oracle truth or a product/evaluator verdict. |
| Protocol `/4`, scorer/adapter `4.0.0`, projection `3.0.0` | Frozen historical bounded-regression identity | Reachable only through the accepted wrapper and dedicated out-of-solution test project; never current product authority. |
| Protocol `/2`, `/3`, or retired `/5` identities | Retired historical chronology | Recoverable through recorded Git identities only; never runnable or reusable authority. |
| Repository authority/retirement schema `1.0.0` | Closed-world repository governance metadata | Classifies paths and Git objects only; it creates no product semantics or evaluator verdict. |

Product capability boundaries are exactly `provider`, `hosted-search`, `nexus`,
and `loot`. `evaluator-private` and `held-out` remain only as accepted older
public-fixture provenance vocabulary; they are not product capabilities.
