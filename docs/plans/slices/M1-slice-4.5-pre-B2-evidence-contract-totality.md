# M1 Slice 4.5 — Pre-B2 evidence-contract totality closure

Status: Accepted; WP1-WP4 complete; evidence contract/model accepted; `M1/S4.5/PRE-B2/WP5` is next
Owner: Project owner
Accepted: 2026-08-05
Work ID: `M1/S4.5/PRE-B2`
Parent: [M1 Slice 4.5 — Held-out evaluation v2](M1-slice-4.5-held-out-evaluation-v2.md)
Predecessor: [Protocol `/4` oracle-contract completion](M1-slice-4.5-protocol-4-oracle-contract-completion.md), hard-stopped historical evidence
Notation: [work-breakdown notation](../work-breakdown-notation.md)

## Objective

Replace fixture-led discovery of public oracle-authority omissions with a
deterministic, comprehensive public contract pass. Define every admissible
evidence state and every protocol `/4` fact dependency, prove that each state
has exactly one publication, coverage, and gap disposition, independently
review that total contract without product behavior, and only then classify
the frozen product candidate against it.

This plan implements the project owner's accepted layered-evidence rule:
retain independently proven lower-layer facts, omit unavailable higher-layer
claims, and expose the exact gap without pretending that missing evidence was
decoded or resolved. It is public-only and does not authorize private B2.

## Fixed identities and terminal starting state

- evaluator: `3693d19563c636cd2879804633ca4ce52448d2c1`;
- protocol: `infinium.evaluator-v2/4`;
- projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`;
- frozen candidate: `a98d648bd0adb2751ee0c09828e0227b1583950f`;
- prior public hard-stop commit:
  `9d29d7ab7e8499f522df07ce4fe5b4cfb3bbc945`; and
- prior authorability evidence: 1,073 main facts across all fifteen families,
  a 42-fact zero-denominator exercise, all ten mutations rejected under
  PowerShell 7, an unresolved Windows PowerShell compatibility defect, and an
  unresolved partial-decode `RACE/DATA` disposition.

The prior draft, fixture package, validator, and review attestation are inputs
and historical evidence. They are not accepted complete oracle authority.
Candidate source/tests remain uninspected until WP5.

## Governing owner decisions

1. [ADR-0029](../../architecture/decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md)
   governs partial success and contract totality.
2. Structural, observed, decoded, resolved, and semantic evidence are distinct
   prerequisites. Failure at a later layer does not erase an independently
   established earlier fact.
3. Every fact obligation for every admissible state receives exactly one of:
   typed value, typed null/unknown, omission plus exact gap, or result rejection.
4. For admitted but undecodable `RACE/DATA`, retain structural/common facts and
   the generic technical taxonomy subject/assignments; omit unproven `DATA`
   count and `face_gen_head`; count the race in the denominator but not as
   completed; and emit the exact unsupported-shape gap.
5. Fixtures validate the decision table. They do not determine policy one
   example at a time.
6. Mechanical omissions may be corrected and rechecked within the public work
   packages. Any genuinely new semantic choice stops for project-owner
   disposition.
7. Protocol `/4`, its projection, evaluator implementation, candidate freeze,
   and private inputs remain unchanged. Protocol `/5` is unauthorized.

## Authority and required reading

Read repository `AGENTS.md` and its complete required product/architecture
sequence before acting. Then read, in order:

1. [work-breakdown notation](../work-breakdown-notation.md);
2. [M1 revision 3](../milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md);
3. [ADR-0027](../../architecture/decisions/ADR-0027-public-evaluation-protocol-private-held-out-corpus.md),
   [ADR-0028](../../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md),
   and [ADR-0029](../../architecture/decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md);
4. [data and trust model](../../architecture/data-and-trust-model.md) and
   [severity, confidence, and coverage](../../product/severity-confidence-and-coverage.md);
5. [evaluation strategy](../../evaluation/evaluation-strategy.md),
   [fixture guidelines](../../evaluation/fixture-guidelines.md),
   [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md), and
   [evaluator-private governance v2](../../evaluation/evaluator-private-fixture-governance-v2.md);
6. [semantic owner disposition](../../evaluation/m1-slice4-semantic-authority-owner-disposition.md),
   [oracle-authority matrix](../../evaluation/m1-slice4-heldout-oracle-authority-matrix.md),
   [blocked construction draft](../../evaluation/specifications/m1-slice4-protocol-4-oracle-construction.md),
   and [public authorability review](../../evaluation/m1-slice4-protocol-4-oracle-authorability-review.md);
7. the frozen evaluator `/4` public documentation, schemas, and canonicalizer
   at the exact evaluator commit; and
8. the parent plan, predecessor plan, public candidate freeze, and Slice 4.5
   implementation record.

Accepted public documents supply semantics. Frozen evaluator code supplies
canonical mechanics only. Candidate behavior, product output, private data,
prior chats, and rollout memory are not semantic authority.

## Global scope and prohibitions

### Common preflight for every work package

Before editing, confirm the working directory is the public `infinium`
repository, record branch/HEAD and `git status --short --branch`, preserve all
pre-existing user changes, and verify that the evaluator, candidate, prior
hard-stop commit, and both public freeze records match the identities above.
Confirm the package's dependencies are committed ancestors and that no
overlapping work is in progress. Use only positively scoped public paths; do
not test the private boundary by enumerating it.

WP1 through WP4 must additionally verify that no product source, product tests,
candidate diff, candidate assembly, or candidate output has entered the role's
context. Stop on any identity mismatch, overlapping edit, authority conflict,
or isolation failure.

### Included

- a normative evidence-layer and fact-dependency inventory for all fifteen
  projected fact families;
- a machine-readable total decision model;
- deterministic completeness, exclusivity, dependency, vocabulary, coverage,
  and gap validation;
- generated generic synthetic coverage of admitted state classes and their
  boundaries;
- repair of the public authorability validator's Windows PowerShell defect;
- one independent product-blind public review after the mechanical gate passes;
- frozen candidate conformance classification after that review; and
- public status, implementation-record, and governance handoff closeout.

### Prohibited throughout

- reading, searching, enumerating, mounting, modifying, or executing anything
  under `../infinium-evaluator-fixtures/`;
- accessing the legacy archive;
- modifying `tools/evaluation/Infinium.EvaluatorV2/**`, protocol `/4`, projection
  `3.0.0`, either freeze JSON, or the frozen candidate;
- inspecting candidate source, tests, assemblies, diffs, or output before WP5;
- using candidate source, tests, assemblies, output, or prior behavior as
  contract authority;
- real-mod, private-case, record-name, or fixture-identity exceptions;
- private oracle authoring, corpus repair/qualification/freeze/tagging, B2, C2,
  Stage D, adaptation, comparison, scoring, or protocol `/5`;
- live or billable calls; and
- pushing.

Each work package ends in one focused local commit. Do not combine packages
when doing so would weaken the fresh-context or product-blind boundary.

## Work-package sequence

### WP1 — Evidence-state and fact-dependency model

Work ID: `M1/S4.5/PRE-B2/WP1`

Package status: Complete at commit
`aeac73110e813ccf26902e434712442546a1166c`; proposed contract package only.

Objective: turn the accepted layered-evidence policy and all existing public
protocol rules into one proposed normative contract whose state space and fact
dependencies are explicit before any new example or candidate inspection.

Create:

- `docs/evaluation/specifications/m1-slice4-protocol-4-evidence-contract.md`;
- `docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.schema.json`;
  and
- `docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json`.

The Markdown contract defines the five derivation layers, common vocabulary,
normalization, atomic-result boundaries, and fact-level publication rules. The
machine model must enumerate all fifteen active families and the admissible
state dimensions needed by them, including publication/failure, record
admission, member presence and occurrence, shape support, decode state, link
state, target resolution, template/race applicability, provider/asset state,
localization capability, taxonomy evidence, coverage, and gap aggregation.

For each state class and fact obligation, encode:

- minimum evidence layer and prerequisites;
- exact emitted type/value source, typed-null/unknown rule, or omission;
- coverage population, denominator participation, and completion effect;
- exact gap population and missing capability when incomplete;
- atomic rejection behavior; and
- the accepted authority that supplies the rule.

The model must contain the accepted partial `RACE/DATA` row explicitly and
must distinguish “not observed,” “observed but undecodable,” “decoded null,”
“decoded unresolved,” and “not applicable.” It may factor common rules, but no
fact family may inherit an unstated default.

Verification and exit:

- JSON and schema parse;
- all fifteen families and every frozen canonical fact/vocabulary constructor
  are inventoried without candidate inspection;
- a manual trace demonstrates the `RACE/DATA` disposition and at least one
  state at each layer boundary;
- document links and `git diff --check` pass; and
- one focused local commit records the proposed contract package.

Stop for the owner if existing accepted authority leaves a genuine semantic
choice after applying ADR-0029. Do not solve it by choosing candidate behavior.
WP1 unblocks WP2; it does not mark the proposed contract accepted.

### WP2 — Executable totality validator

Work ID: `M1/S4.5/PRE-B2/WP2`
Depends on: `M1/S4.5/PRE-B2/WP1`

Package status: Complete; deterministic totality and all required negative
self-tests pass under Windows PowerShell 5.1 and PowerShell 7 with semantically
identical summaries. The focused correction replaces complement-style
exclusions with 118 explicit, predicated exclusion regions and expands the
negative suite to 24 mutations without changing the state-space totals. WP4
acceptance authority remains unopened.

Objective: make completeness a deterministic gate rather than a reviewer
impression.

Add an executable public validator under `eng/` that:

- expands or otherwise proves the complete admitted state space;
- requires exactly one applicable disposition per state/fact obligation;
- rejects uncovered and overlapping rules;
- rejects use of decoded/resolved/semantic facts without their prerequisites;
- validates typed null, unknown, omission, rejection, coverage, and gap
  consistency;
- validates closed vocabularies, all fifteen families, stable rule IDs, and
  authority citations; and
- emits a deterministic human- and machine-readable summary.

Repair `eng/validate-m1-slice4-protocol4-authorability.ps1` so the required
Windows PowerShell invocation and PowerShell 7 both complete without changing
semantic results. Add negative self-tests for missing, duplicate, overlapping,
dependency-invalid, coverage-inconsistent, and gap-inconsistent rules.

The totality gate must pass from a clean public checkout. A validator defect or
mechanically uncovered branch returns to WP1/WP2 and is repaired. A newly
required semantic choice stops for the owner. One focused local commit
completes WP2 and unblocks WP3.

### WP3 — Generated synthetic state coverage

Work ID: `M1/S4.5/PRE-B2/WP3`
Depends on: `M1/S4.5/PRE-B2/WP2`

Package status: Complete after focused validator/schema correction and full
WP3 re-review. The tracked generated coverage remains answer-free and the
source contract/model remain proposed; only WP4 may accept them.

Objective: derive the public exercise matrix from the accepted state model,
then use examples to validate every rule and boundary.

Extend the generic public authorability package with a deterministic generator
or model-driven mapper that covers:

- every state class and disposition at least once;
- every fact family and common lexical rule;
- every transition between adjacent evidence layers;
- matched positive/negative, null/missing/unknown, unsupported, rejection,
  zero-denominator, duplicate, aggregation, and ordering cases; and
- pairwise combinations for interacting dimensions, plus targeted higher-order
  cases identified by dependencies.

Retain answer isolation: generated input cases and rule-to-case coverage may be
tracked, while derived scratch outputs remain under ignored `work/`. Expected
facts must be constructed from the public contract, never copied from product
output. Re-run the existing ten mutations and add any category-level mutations
required by the totality model.

Completion requires zero uncovered rules/states, deterministic repeated
generation, both PowerShell runtimes passing where required, all mutations
rejected, no duplicate final fact IDs, and one focused local commit. WP3
unblocks WP4.

Closeout evidence: the generator classified all 23,660 raw model states with
110 admitted, 6,180 excluded, 17,370 invalid/terminal, zero uncovered, and
zero overlapping. Its 515 compact cases include all 110 admitted states, 110
matched negatives, representatives for all 236 state constraints, and all
1,713 family-local dimension/value pairs. Mappings cover all 15 families, 9
state classes, 77 publication rules, 24 constructor groups, 10 normalization
rules, 11 atomic boundaries, 8 gap rules, 10 coverage populations, 6 manual
transitions, and the exact partial `RACE/DATA` invariant. Both PowerShell
hosts passed generation and validation, produced byte-identical tracked
artifacts, and rejected all 33 WP3 mutations. The strengthened validator
recomputes every reported mapping from the model and reconstructed truth;
the schema uses closed typed structures for every normative collection and a
fixed answer-isolation registry. The unchanged authorability
validator passed on both hosts with 1,073 facts, zero duplicate fact IDs, all
10 fixed coverage populations, and all 10 existing mutations rejected.

### WP4 — Independent product-blind totality review

Work ID: `M1/S4.5/PRE-B2/WP4`
Depends on: `M1/S4.5/PRE-B2/WP3`

Package status: Complete. A single fresh product-blind reviewer accepted the
contract/model after exact public-authority review, cross-runtime WP2/WP3
validation, independent schema/adversarial checks, and a fresh 1,124-fact plus
42-fact zero-denominator authorability rehearsal. The immutable public record
is the
[WP4 totality review attestation](../../evaluation/m1-slice4-protocol-4-totality-review-attestation.md).
WP5 is next and was not started.

Objective: obtain an independent public authorability and totality judgment
without candidate knowledge.

Use one genuinely fresh reviewer with no inherited implementation conversation
and a positive allowlist containing only accepted public authorities, the
proposed contract/model, generated answer-free inputs and coverage mapping,
the public validators, and frozen evaluator `/4` canonical mechanics. Exclude
all product source/tests/output, private data, expected-answer artifacts, and
prior reviewer conclusions.

The reviewer independently checks:

- complete coverage and mutual exclusivity of the admitted state model;
- correctness of evidence prerequisites and graceful-degradation outcomes;
- exact authorability of every fact/value from permitted evidence;
- coverage/gap arithmetic and the partial `RACE/DATA` rule;
- fixture/model agreement without fixture-specific rules; and
- deterministic validator claims on both supported PowerShell runtimes.

The reviewer records an immutable public attestation and does not edit the
contract. Mechanical findings return to WP1-WP3 for correction and a fresh
clean re-review; this is not limited to one arbitrary fixture correction.
Any unresolved semantic choice, need for candidate behavior, evaluator `/4`
representation gap, or answer-isolation breach stops for the owner.

After a clean pass, mark the evidence contract/model accepted, bind exact file
hashes and commit identity in the attestation, and make one focused closeout
commit. WP4 unblocks WP5.

### WP5 — Frozen candidate conformance classification

Work ID: `M1/S4.5/PRE-B2/WP5`
Depends on: clean `M1/S4.5/PRE-B2/WP4` pass

Objective: compare the now-immutable public contract with frozen candidate
`a98d648bd0adb2751ee0c09828e0227b1583950f` exactly once.

Only now may the agent inspect candidate source, tests, and public artifacts.
Run the applicable focused/full public verification and classify the result:

1. **documentation-only conformance** — candidate already implements the
   accepted total contract;
2. **product mismatch** — candidate behavior conflicts with the accepted
   contract; or
3. **evaluator `/4` representation gap** — the accepted semantic disposition
   cannot be represented by frozen `/4` mechanics.

Do not repair or refreeze the product in WP5. A product mismatch requires a
separate owner-authorized product realignment plan and candidate freeze. An
evaluator gap remains an unresolved M1 evaluation gap; do not create `/5`.
Record exact checks, identities, and classification in one focused commit.
Only documentation-only conformance unblocks WP6 directly.

### WP6 — Public closeout and private-governance handoff

Work ID: `M1/S4.5/PRE-B2/WP6`
Depends on: documentation-only conformance from `M1/S4.5/PRE-B2/WP5`, or an owner-authorized successor disposition

Objective: make public status coherent and prepare, but do not execute, the
next separate authority role.

Update the plan indexes, parent plan, baseline/scope amendments, authority
matrix, owner disposition, and Slice 4.5 implementation record with exact
commits, hashes, checks, review isolation, classification, and remaining gates.
Define a positive public allowlist for the later private role. A separately
authorized fresh corpus-governance/custodian task must decide from sanitized
evidence whether the unchanged private inputs remain eligible after the public
clarification. This plan neither makes that decision nor authorizes B2.

Completion requires link/identity/protected-path/diff checks, one focused local
commit, a clean tree except for preserved user work, and an explicit statement
that B2, C2, Stage D, Slice 5, protocol `/5`, private access, and push did not
occur.

## Package gates

```text
WP1 model
  -> WP2 deterministic totality
  -> WP3 model-derived exercises
  -> WP4 fresh product-blind acceptance
  -> WP5 frozen candidate classification
  -> WP6 public closeout / governance handoff
  -> separate owner authorization, if any
  -> separate private corpus-governance decision
  -> separate fresh B2 task, if still eligible and authorized
```

Passing a later gate never retroactively supplies evidence to an earlier one.
Candidate conformance cannot validate the contract, and private outcomes cannot
repair public authority.

## Global stop conditions

Stop and return to the project owner if:

- a state requires a new semantic or authority choice not settled by accepted
  public documents;
- totality requires product behavior, private information, or fixture identity;
- the frozen evaluator or projection would need to change;
- an answer-isolation or role-separation boundary is breached;
- a product mismatch or evaluator `/4` representation gap is classified;
- a protected identity differs;
- any private path or answer-bearing detail is accessed or disclosed; or
- independent review leaves a material finding unresolved.

Do not respond by inventing a default, broadening the evaluator, creating `/5`,
weakening a fact/coverage/gap obligation, or launching another private pass.

## Plan completion criteria

This plan is complete only when WP1 through WP6 have exact closeout records,
the public totality model is accepted and deterministically passes, a fresh
product-blind review has no material finding, frozen candidate conformance is
classified, public status is coherent, and the next separate authority role is
explicit. Completion is not a B2 authorization and is not a held-out product
verdict.
