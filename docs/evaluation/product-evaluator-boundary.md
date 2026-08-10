# Product, public-fixture, and evaluator boundary

Status: Current repository authority map

Last reviewed: 2026-08-10

Date: 2026-08-08

Authority: ADR-0032 and project-owner-authorized repository boundary hardening

The namespace or path containing an artifact does not establish its authority.
Use `repository-evaluation-authority.v1.json` for the machine-readable current
inventory, `retired-evaluation-assets.v1.json` for Git-backed retirements, and
[Evaluator history](evaluator-history.md) for the condensed chronology.

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

The current public candidate evidence consists of three separately closed
packages under `fixtures/public/candidates/`:

- `CAND-SEMANTIC-DEV-v1/1.0.0` (`development`) contains the answer-free
  direct delivered-input artifact and independently frozen semantic projection;
- `CAND-SCALE-VAL-v1/1.0.0` (`validation`) contains the answer-free bounded
  expansion and its independently enumerated population projection; and
- `CAND-STRESS-DEV-v1/1.0.0` (`development`) uses the same expansion recipe
  at the streaming-only stress boundary.

Their accepted public-manifest SHA-256 values are respectively
`2fbfc34b1220a4882f676cea6142fe9a2ee604a8d941b51270e11cd2720ffaf0`,
`7f7fd239f6d83deb0e626fb148b67433402feded38722e17b32bf03e2044be89`,
and `4f676ff6760bb6ff61973dc2f196cdfef9f87892fd5a287e2c39879d960f25cf`.
The scale validation package was compared only after the development semantic
package had exposed the shared lane, hypothesis, and unsupported-abstention
defects; it did not author or tune those corrections. These packages establish
only bounded candidate construction and do not establish findings, cases,
readiness, reliability, or any private verdict.
