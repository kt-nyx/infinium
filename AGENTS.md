# Infinium repository guidance

This repository is being rebuilt from a product specification. The abandoned
implementation is not part of the active working tree and is not
authoritative. A complete maintainer-local archive exists outside the
repository at sibling path `../infinium-legacy-archive/`; the tracked portion
also remains recoverable from Git history through commit `7dd3da6`. Do not
inspect, restore, or use that archive unless the user explicitly requests it.

## Required reading order

Before research, planning, architecture, or implementation work:

1. `docs/README.md`
2. `docs/product/product-definition.md`
3. `docs/product/requirements.md`
4. `docs/product/mod-impact-taxonomy.md`
5. `docs/product/workflows.md`
6. `docs/product/domain-model.md`
7. `docs/product/severity-confidence-and-coverage.md`
8. `docs/product/analysis-catalog.md`
9. `docs/product/scope-and-milestones.md`
10. `docs/architecture/overview.md`
11. `docs/architecture/data-and-trust-model.md`
12. Relevant accepted ADRs under `docs/architecture/decisions/`

Before following links from an older plan, implementation record, attestation,
or occurrence ledger, establish the current repository state from:

13. `docs/evaluation/product-evaluator-boundary.md`
14. `docs/evaluation/repository-evaluation-authority.v1.json`
15. the active accepted slice plan and its current implementation record

Historical records preserve what happened at an earlier commit; they are not a
navigation path for current implementation. A historical path, package name,
command, schema, protocol identity, or status statement is never current merely
because it remains documented.

Then read the task-specific material:

- research: the relevant entry in `docs/research/open-questions.md`,
  `docs/research/source-registry.md` when sources are involved, and
  `docs/research/investigations/README.md`;
- architecture: `docs/architecture/decisions/README.md`, relevant proposed
  ADRs/research, and the applicable integration, jobs/caching, and
  security/privacy documents;
- ordinary public evaluation or analyzer work: `docs/evaluation/evaluation-strategy.md`,
  `docs/evaluation/case-catalog.md`, `docs/evaluation/fixture-guidelines.md`,
  `docs/evaluation/anti-overfitting-rules.md`,
  `docs/evaluation/m1-continuation-verification-profile.md`, and the active
  slice plan. Do not read private-fixture files or historical evaluator plans
  for ordinary product work;
- separately authorized private evaluator work:
  `docs/evaluation/evaluator-private-fixture-governance-v2.md`, the exact
  accepted evaluator plan, and the private repository's own `AGENTS.md` and
  `GOVERNANCE.md` before any private access;
- implementation: the active accepted milestone plan, active slice plan, and
  prerequisite implementation record.

## Working rules

- Treat accepted product documents and ADRs as authoritative.
- Treat the external abandoned-implementation archive as out of scope unless
  the user explicitly requests archaeological review.
- Do not copy legacy behavior without independent validation against current
  requirements.
- Put unresolved technical questions in `docs/research/open-questions.md`.
- Record researched evidence in `docs/research/investigations/`.
- Do not turn a research conclusion into architecture implicitly; create or
  update an ADR.
- Do not start implementation without an accepted milestone plan linking its
  requirements, decisions, and evaluation cases.
- Keep deterministic observations, external claims, hypotheses, findings, and
  recommendations distinct.
- Do not introduce real-mod-name or fixture-specific rules into production
  analysis.
- Preserve full provenance and expose coverage gaps rather than inventing
  certainty.

## Evaluator-private fixtures

### Repository authority map

- Current product contracts and codecs live under `contracts/json-schema/`,
  `contracts/protobuf/`, and `src/`; repository-governance schemas under
  `contracts/repository/` are contract-test metadata and never product inputs;
  current public-fixture readers live under
  `tools/evaluation/Infinium.PublicFixtures/` and use the active product
  validator.
- `tools/evaluation/Infinium.EvaluatorV2/` is frozen historical protocol `/4`
  evidence. It is outside the default solution graph and may run only through
  `eng/invoke-m1-slice4-protocol4-bounded-regression.ps1`.
- `docs/evaluation/fixtures/independent-slice3-evaluator-20260729/` is retained
  historical evaluator evidence. It is not a current public fixture, must not
  be validated with live product schemas, and has no current executable entry
  point or product authority.
- Retired compatibility code, predecessor schemas, and obsolete proof tools
  exist only through Git identities recorded in
  `docs/evaluation/retired-evaluation-assets.v1.json`.

Do not infer current authority from a namespace, filename, schema version, or
historical path. Consult
`docs/evaluation/product-evaluator-boundary.md` and its linked machine-readable
inventory; product/default-solution projects must not reference
`Infinium.EvaluatorV2` or retired paths. Product schema versions, public fixture
package versions, evaluator protocol/scorer/projection versions, and repository
authority-manifest versions are independent axes and must never be substituted
for one another.

For M1 Slice 5, semantic fixtures are staged work-package evidence. WP1 owns
closed product contracts, codecs, state invariants, schema-4 migration, and
repository-boundary enforcement plus minimal answer-free contract examples.
WP2-WP5 each author, freeze, and independently review the small semantic cases
for behavior introduced by that package before comparing them with product
output. WP3 owns candidate scale/stress construction and any product-reachable
expansion contract. WP6 assembles and independently reviews the comprehensive
cross-package corpus. No rejected or preauthored comprehensive Slice 5 corpus
is a prerequisite for product implementation.

Current M1 handoff: `M1/S5/WP1` is complete and reviewed at
`a333f016f66cafc393f165448e777276f3b6bd88`; `M1/S5/WP2` is the next eligible
package. The rejected WP1-generated 28-package corpus, its registry, generator,
fixture-only tests, and WP1 fixture-independence/generator-feasibility gates do
not exist as current inputs and must not be reconstructed from historical
names. Current public fixture discovery is limited to the six exact identities
in `docs/evaluation/repository-evaluation-authority.v1.json`. Later Slice 5
packages assign and freeze new semantic fixture identities within their own
scope; product output never authors expected truth.

The separately versioned evaluator-private fixture repository is default-deny
for ordinary Infinium work. Do not read its files directly while implementing,
debugging, tuning, or reviewing production behavior.

Evaluator v2 keeps protocol, schemas, canonicalization, scorer, adapter, and
calibration public under ADR-0027. Ordinary product implementation must not
create, orchestrate, repair, replace, or retry private evaluation work. It
stops on a private evaluator or corpus failure. Stage B authoring and
maintenance, Stage C scoring, and successor maintenance are separately
authorized fresh tasks, not recursive implementation subtasks. Private
scoring returns only the sanitized handoff permitted by governance v2.
Evaluator or corpus maintenance has no product-scoring authority, and the
scorer has no maintenance authority.

Protocol `/4` remains immutable historical evidence and may be used only
through the accepted bounded public regression profile with its known partial
`RACE/DATA` representation gap excluded. A bounded-regression pass is tool and
allowlisted public-regression health, not current semantic, held-out, Slice
4.5, M1, reliability, readiness, or product acceptance.

ADR-0032 supersedes ADR-0030's active protocol `/5` authorization and
ADR-0031 only as active `/5` model authority. Protocol `/5` is retired
unqualified: it has no implementation, freeze, private use, or verdict, and
its identities must not be reused or resumed. Private held-out evaluation is
deferred with no valid current product verdict. Do not access private material
or run corpus qualification, B2, C2, Stage D, adaptation, comparison, or
scoring. Evaluator-deferral closeout is accepted. M1 Slices 5-9 use the
accepted continuation verification profile. Do not select a
future protocol identity or weaken any isolation, no-retry, identity, freeze,
layered-evidence, coverage, gap, provenance, or answer-isolation rule.
