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

The current WP3 public candidate evidence consists of three separately closed
packages under `docs/evaluation/fixtures/m1-slice5-wp3-candidates-v1/`:

- `CAND-WP3-SEMANTIC-DEV-v1/1.0.0` (`development`) contains the answer-free
  direct delivered-input artifact and independently frozen semantic projection;
- `CAND-WP3-SCALE-VAL-v1/1.0.0` (`validation`) contains the answer-free bounded
  expansion and its independently enumerated population projection; and
- `CAND-WP3-STRESS-DEV-v1/1.0.0` (`development`) uses the same expansion recipe
  at the streaming-only stress boundary.

Their accepted public-manifest SHA-256 values are respectively
`94799a0d9fd5c90594d5da7074297fe257e44aad69b98487bdc7ea5619370afb`,
`98e1f3bcb88e40c52abbbddc62ed9f3d613e90d09c4a15d51be081bc8a1bf2c8`,
and `5b5507622d217223aa2a28a049d5c82b7e411238aaa6c10f415f27c594d1ebbf`.
The scale validation package was compared only after the development semantic
package had exposed the shared lane, hypothesis, and unsupported-abstention
defects; it did not author or tune those corrections. These packages establish
only bounded WP3 candidate construction and do not establish findings, cases,
readiness, reliability, or any private verdict.
