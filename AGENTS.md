# Infinium repository guidance

This repository is being rebuilt from a product specification. The abandoned
implementation is not part of the active working tree and is not
authoritative. A complete maintainer-local archive exists outside the
repository at sibling path `../infinium-legacy-archive/`; the tracked portion
also remains recoverable from Git history through commit `7dd3da6`. Do not
inspect, restore, or use that archive unless the user explicitly requests it.

Superseded evaluator-development staging and the completed M0/M1 development
history are consolidated at `../infinium-development-history-archive/` commit
`6f8976db6c560456201a9166caf4f36506be5477`. That archive is also out of scope
unless the user explicitly authorizes archaeological review. Its presence does
not grant private-fixture access or current evaluator authority.

Retired public protocol `/4` code and its final regression closure are in the
separate sibling Git repository `../infinium-evaluator-archive/`. That archive
is non-authoritative and out of scope unless the user explicitly requests
protocol archaeology. Do not run or restore it during ordinary product work.

## Required reading

Before any research, planning, architecture, or implementation work, read only
the core entry set:

1. `docs/README.md`
2. `docs/current-state.md`
3. `docs/execution-policy.md`

Then load only the task-specific authority identified by those entry documents.
For product behavior, this normally means the relevant accepted product
document, ADR, milestone/slice summary, and owning full plan. Read the full
implementation record only when chronology or exact retained evidence matters.

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
  `docs/evaluation/product-conformance-verification-profile.md`, and the active
  slice plan. Do not read private-fixture files or historical evaluator plans
  for ordinary product work;
- separately authorized private evaluator work:
  `docs/evaluation/evaluator-private-fixture-governance-v2.md`, the exact
  accepted evaluator plan, and the private repository's own `AGENTS.md` and
  `GOVERNANCE.md` before any private access;
- implementation: the active accepted milestone plan, the active slice's
  compact entry document, and its full accepted plan; load prerequisite or
  current implementation records only when the task depends on their evidence
  or chronology.

Do not load historical evaluator plans, incident chronology, hard-stop records,
or private governance for ordinary product implementation unless the active
plan identifies one as a direct task input. Historical execution constraints
do not become current defaults merely because an old record is linked.

## Working rules

### Functional implementation naming

- Active implementation names describe domain meaning, behavior,
  responsibility, or architecture. Do not name source files, namespaces,
  types, members, commands, configuration, fixtures, tests, or new serialized
  identities after milestones, slices, work packages, waves, evaluator stages,
  campaign attempts, or temporary development chronology.
- Terms such as `stage`, `development`, `candidate`, `recovery`, `generation`,
  and numeric versions are permitted only when they name a genuine product or
  compatibility concept. Planning prose and archived history may retain
  planning IDs.
- Preserve frozen wire/schema/database/evidence bytes where required, but use a
  functional code-facing alias and an exact reviewed compatibility allowlist.
- Run `eng/verify-functional-naming.ps1` for affected implementation work. New
  allowlist entries require explicit review and must name the exact path,
  token/context, retained consumer, reason, and removal/review condition.
- See `docs/governance/functional-implementation-naming.md` for the complete
  policy.

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
- Follow `docs/execution-policy.md` for ordinary work: implement,
  test, review, correct, and re-review until the package is accepted or a
  genuine escalation condition occurs.
- Use one mutable working candidate for ordinary development. Bring the full
  affected vertical path to coherence, run focused checks, perform one
  consolidated semantic/security/provenance/diff review, batch corrections on
  that same candidate, recheck the affected surface, then run the complete
  accepted verification floor once the package is review-ready and bind the
  passing accepted candidate once.
- A failed complete floor is diagnostic evidence that the candidate was not
  final. Correct the same working candidate and rerun focused checks and
  changed-surface review before attempting a new final floor. Do not create
  freeze/bind/record churn for intermediate corrections.
- If the same conceptual defect recurs after two completed correction
  attempts, pause that path for explicit design diagnosis. Escalate to the
  owner only when the durable resolution would choose missing product meaning,
  change accepted architecture, expand scope or authority, weaken isolation,
  or require an otherwise unauthorized effect.
- Treat failed tests, review findings, fixture defects, schema/codec mismatch,
  validator bugs, stale documentation, and incomplete implementation as
  recoverable work, not owner-level blockers by themselves.
- Do not impose correction-pass budgets on ordinary product work. Review
  findings must be classified as must-fix, follow-up, non-blocking,
  owner/authority decision, or safety/isolation breach.
- Escalate only the affected path when accepted authority conflicts or is
  materially incomplete, scope/authority must expand, an owner-controlled
  dependency is unavailable after safe alternatives, or continuing would
  violate a security, private-answer, protected-root, destructive, or external
  effect boundary. Continue independent in-scope work where possible.
- Treat contracts as implementation-active until producer, consumer,
  persistence, round-trip, invalid-state, and focused fixture evidence support
  freezing them. Update all affected seams together when implementation
  evidence requires a clean-break revision.
- Freeze exact independent fixture/oracle inputs only when a current accepted
  plan authorizes them; ADR-0035 defers independent semantic-oracle work
  throughout M1 and M2. Freeze external-effect manifests,
  durable effect evidence, and final accepted contracts/implementation at
  their owning immutable boundary. Do not freeze ordinary intermediate
  corrections.
- Runtime effect authority comes from a closed typed manifest plus durable
  coordinator-owned admission/use/settlement state. Git may bind reviewed
  bytes, but branch/HEAD state, commit subjects, log order, pickaxe, line
  attribution, and historical message discovery never grant runtime authority.
- Keep current navigation compact: state the live handoff, accepted inputs,
  meaningful gaps, and next gate. Preserve material chronology in the owning
  implementation record or Git history instead of copying it into current
  entry documents.

## Special evaluator and private-fixture boundaries

This section applies when work touches evaluator authority or private-fixture
operations. Its freeze, isolation, no-retry, and terminal-stop rules do not
define the correction policy for ordinary product development.

### Repository authority map

- Current product contracts and codecs live under `contracts/json-schema/`,
  `contracts/protobuf/`, and `src/`; repository-governance schemas under
  `contracts/repository/` are contract-test metadata and never product inputs;
  current public-fixture readers live under
  `fixtures/tooling/Infinium.PublicFixtures/` and use the active product
  validator.
- Protocol `/4` is retired from the active repository. Its final public
  snapshot is archived at `../infinium-evaluator-archive/` commit
  `c490de9689d8e9f8dfc7eccb3d056ab5b083e9fd`; it has no current entry point,
  test requirement, review gate, or authority.
- The former `docs/evaluation/fixtures/independent-slice3-evaluator-20260729/`
  tree is Git-only historical evaluator evidence recorded in the retirement
  inventory. It is not a current public fixture, executable workflow, or
  product authority.
- Retired compatibility code, predecessor schemas, and obsolete proof tools
  exist only through Git identities recorded in
  `docs/evaluation/retired-evaluation-assets.v1.json`.

Do not infer current authority from a namespace, filename, schema version, or
historical path. Consult
`docs/evaluation/product-evaluator-boundary.md` and its linked machine-readable
inventory; product/default-solution projects must not reference archived
evaluator code or retired paths. Product schema versions, public fixture
package versions, evaluator protocol/scorer/projection versions, and repository
authority-manifest versions are independent axes and must never be substituted
for one another.

For M1 and M2, ordinary semantic fixtures are developer-owned conformance
evidence, not independent semantic-oracle qualification. WP1 owns
closed product contracts, codecs, state invariants, schema-4 migration, and
repository-boundary enforcement plus minimal answer-free contract examples.
WP2-WP5 each own small positive, negative, malformed, lifecycle, abstention,
mutation, and metamorphic cases for behavior introduced by that package. WP3
owns candidate scale/stress construction and any product-reachable expansion
contract. WP6 assembles cross-package conformance evidence. No rejected or
preauthored comprehensive Slice 5 corpus is a prerequisite for product
implementation.

The current M1 handoff is stated only in `docs/current-state.md`; do not infer
it from this file or historical records. For Slice 5, the rejected WP1-generated
28-package corpus, its registry, generator, fixture-only tests, and WP1
fixture-independence/generator-feasibility gates do not exist as current inputs
and must not be reconstructed from historical names. Current public fixture
discovery is closed-world and limited to the exact paths and identities in
`docs/evaluation/repository-evaluation-authority.v1.json`; no path, namespace,
or historical record can add an implicit fixture. Product output never authors
expected truth.

The separately versioned evaluator-private fixture repository is default-deny
for ordinary Infinium work. Do not read its files directly while implementing,
debugging, tuning, or reviewing production behavior.

Evaluator v2 protocol, schemas, canonicalization, scorer, adapter, and
calibration are archived historical material under ADR-0033. Ordinary product
implementation must not create, orchestrate, repair, replace, or retry private
evaluation work. It
stops the affected private operation on a private evaluator or corpus failure;
unrelated public product work may continue within its accepted scope. Stage B
authoring and maintenance, Stage C scoring, and successor maintenance are
separately authorized fresh tasks, not recursive implementation subtasks. Private
scoring returns only the sanitized handoff permitted by governance v2.
Evaluator or corpus maintenance has no product-scoring authority, and the
scorer has no maintenance authority.

Protocol `/4` is retired and archived. It may not be run, restored, resumed,
or used as review evidence. Its identities are permanently reserved. Current
product review uses only the accepted M1/M2 product-conformance verification profile and
owning slice plans.

ADR-0032 supersedes ADR-0030's active protocol `/5` authorization and
ADR-0031 only as active `/5` model authority. Protocol `/5` is retired
unqualified: it has no implementation, freeze, private use, or verdict, and
its identities must not be reused or resumed. Private held-out evaluation is
deferred with no valid current product verdict. Do not access private material
or run corpus qualification, B2, C2, Stage D, adaptation, comparison, or
scoring. Evaluator-deferral closeout is accepted. M1 Slices 5-9 use the
accepted product-conformance verification profile. ADR-0035 also defers every
independent semantic-oracle package through M2: no current semantic package is
validation authority, and no authoring, review, sealing, registration,
comparison, or oracle `PASS` is authorized before an accepted M3 plan reopens
that work at the M3 Evaluation Readiness Gate. Do not select a
future protocol identity or weaken any isolation, no-retry, identity, freeze,
layered-evidence, coverage, gap, provenance, or answer-isolation rule.
