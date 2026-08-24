# Anti-overfitting rules

Status: Accepted
Last reviewed: 2026-08-23

ADR-0035 defers independent semantic-oracle qualification through M1 and M2.
These anti-overfitting rules still apply to developer-owned conformance,
deterministic references, mutation/metamorphic testing, and claims. Historical
or held-out isolation rules remain intact but grant no current evaluation
authority.

The first semantic proof may use one category, technical surface, affected game
area, or interaction shape because it exercises useful product capabilities.
That starting category does not define the product's scope or prove that a
mechanism is generic.

This policy distinguishes a deliberately bounded domain analyzer from a
mechanism claimed to generalize. A domain analyzer may remain within its
declared category when that boundary is honest and useful. A mechanism may be
described as generic only after it survives a materially different category
that exercises the same claimed abstraction.

## Prohibited production behavior

- Real mod names or IDs hard-coded into generic semantic classification logic
  solely to make a fixture pass.
- Logic keyed to fixture plugin/file names rather than the represented
  structure and evidence.
- Special cases introduced solely to pass one evaluation.
- Generic types or stages named after the first proof category.
- Assuming that all semantic conflicts share the first proof's category,
  technical surface, affected game area, consequence, or interaction shape.
- Treating load order as proof of intent.
- Treating a known patch name as proof of patch effectiveness.
- Suppressing analyzers or negative controls through maturity weighting during
  development or conformance evaluation.
- Changing expected outputs to match an unexplained implementation result.
- Using expected labels, adjudications, fixture IDs, or answer-bearing test
  notes as model, retrieval, ranking, or analyzer inputs.
- Tuning against a held-out case without reclassifying it as a development
  case and replacing the lost holdout coverage.

This does not prohibit named tool/generator adapters, installed-mod identity
mappings, source-derived mod-specific compatibility claims, or curated LOOT
rules. Those remain data/provenance-bearing evidence rather than hidden
fixture-specific semantic exceptions.

## Required generalization checks

- Rename mods/plugins without changing semantics.
- Add unrelated mods without changing conclusions.
- Reorder only unrelated mods without changing conclusions.
- Change an effective winner and observe only dependency-relevant changes.
- Pair every harmful fixture with a structurally similar intentional/harmless
  fixture.
- Test malformed and unsupported data.
- Test ambiguous purpose and require abstention or an intent question.
- For the first proof in any category, validate the same claimed generic
  mechanism against at least one materially different category before
  declaring that mechanism generic. The contrasting proof may differ by
  technical surface, affected game area, consequence, interaction shape, or
  another relevant accepted taxonomy axis; it is not permanently defined as
  “NPC versus non-NPC.”
- Treat one contrasting proof as the minimum generalization gate, not evidence
  of broad or exhaustive domain coverage. Exact classification uses the
  accepted
  [Skyrim SE mod-impact taxonomy](../product/mod-impact-taxonomy.md);
  unevaluated taxonomy regions remain explicit coverage gaps.
- For a bounded state machine or projection, enumerate the admitted semantic
  state classes and prove exactly one fact/coverage/gap disposition for each.
  Fixtures must map to that model; fixture discovery alone is not evidence that
  the contract is total.
- When one field or member is unsupported, verify both that independently
  proven lower-layer facts survive and that no higher-layer value is guessed.
  Pair this with a matched fully decoded case and a case that requires atomic
  rejection.

## Evaluation partition and answer isolation

Repository placement and autonomous agent access follow the accepted
[evaluator-private fixture governance v2](evaluator-private-fixture-governance-v2.md).
Evaluator rules, adapter, scorer, and calibration remain public. Private corpus
maintenance and scoring are separate fresh tasks; product implementation does
not repair or retry either and receives no raw private inputs or answers unless
the fixture is explicitly contaminated, reclassified to development, and
independently replaced.

- Classify fixtures as development, validation, or held-out before using their
  results to change production behavior.
- Record expected observations, conclusions, abstentions, and coverage gaps
  before running the implementation under evaluation.
- Build expected results from independent format, structure, author-source, or
  retained adjudication evidence rather than the production parser or model
  path under test.
- Do not expose expected answers or answer-bearing adjudication to an LLM or
  retrieval system being evaluated. Author documentation may remain legitimate
  evidence when the case is specifically testing documentation reasoning, but
  its provenance and allowed conclusion scope must be explicit.
- When a held-out case influences implementation or prompt behavior, move it
  into the development set, record that transition, and add a materially
  independent replacement holdout.
- Changes to pre-registered expected results require new independent evidence,
  an explanation of the prior error, and review; an implementation mismatch is
  not by itself evidence that the expectation was wrong.

## Generic and domain responsibilities

Generic infrastructure may implement:

- provenance;
- override chains;
- changed-field sets;
- stale-value/reversion patterns;
- declared-purpose claims;
- evidence combination;
- candidates, hypotheses, findings, and cases.

Domain analyzers may encode stable Skyrim semantics:

- record relationships;
- field meaning;
- feature-graph construction;
- impact and validation rules.

Domain knowledge must be reusable across arbitrary mods within its documented
scope.

## Review requirement

Any rule introduced after a failing real-mod case must answer:

1. What general class of behavior does this represent?
2. What independent synthetic positive demonstrates it?
3. What matched negative prevents overreach?
4. Which declared domain scope owns it?
5. What evidence threshold and abstention behavior apply?
6. Was the triggering case development, validation, or held-out, and did its
   use require replacement holdout coverage?
7. Were the expected result and all answer-bearing adjudication isolated from
   the implementation, retrieval path, and model being evaluated?
8. Which general evidence-state class and fact dependency does the rule cover,
   and did the totality gate prove that no overlapping or uncovered disposition
   remains?

The evaluator authority check remains hard: if an expected value cannot be
authored from public rules, the answer-free manifest, accepted taxonomy rules,
and retained input bytes, it is not a held-out fact. ADR-0030/ADR-0031's `/5`
attempt is historical; ADR-0032 retired it unqualified before implementation
or freeze. Its failed proof artifacts, product output, private answers, and
fixture identities cannot become expected truth.

For M1 Slices 5-9, the continuation profile requires developer-authored public
conformance cases, mutation/metamorphic checks, matched negatives, visible
abstention/coverage/gaps, and fresh semantic/diff review. An independent
semantic verdict is explicitly deferred under ADR-0035 and is not implied by
these checks. Slice 7 must
exercise the same generic mechanism across the two materially different
actor/AI/FaceGen and REFR/link/placement domains. Slice 8 must execute
controlled-real EVAL-0016 and EVAL-0017. Product-driving results are
development/validation evidence, not held-out evidence, and unevaluated
taxonomy regions remain explicit gaps.
