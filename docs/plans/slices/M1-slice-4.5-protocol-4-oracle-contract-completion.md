# M1 Slice 4.5 — Protocol `/4` oracle-contract completion and held-out disposition

Status: Hard-stopped after the permitted authorability correction pass; owner disposition required
Owner: Project owner
Accepted: 2026-08-05
Parent plan: [M1 Slice 4.5 — Held-out evaluation v2](M1-slice-4.5-held-out-evaluation-v2.md)

## Objective

Complete the public, product-independent oracle-construction contract for all
fifteen active protocol `/4` fact families; prove that a fresh product-blind
reviewer can construct exact canonical facts from public authority and
answer-free synthetic bytes; and determine whether frozen candidate
`a98d648bd0adb2751ee0c09828e0227b1583950f` and frozen evaluator
`3693d19563c636cd2879804633ca4ce52448d2c1` already conform to that completed
contract.

This is the project-owner milestone-plan disposition required after the single
authorized private B2 resume stopped on another public-authority gap. It is a
public-only contract, verification, review, and status-closeout task. It does
not authorize private fixture access, another oracle-authoring attempt, corpus
qualification, candidate scoring, C2, Stage D, or Slice 5.

## Sanitized terminal state being dispositioned

The owner-provided sanitized B2 handoff records:

- B2 terminal status: blocked by an unresolved public-authority gap;
- private evidence commit:
  `4fea37a2c5ee512c2f14781361d60742e62b0d57`;
- candidate: `a98d648bd0adb2751ee0c09828e0227b1583950f`;
- evaluator: `3693d19563c636cd2879804633ca4ce52448d2c1`;
- protocol: `infinium.evaluator-v2/4`;
- projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`;
- intended corpus: `infinium.m1.slice4.heldout/2.0.0`;
- complete expected outputs authored: no;
- frozen inputs unchanged: yes;
- access and answer isolation: preserved;
- contamination at the terminal stop: clean;
- candidate execution or output inspection: no; and
- next required role: project-owner milestone-plan disposition.

The sanitized review established that the private positive member was
structurally decodable, but exact evaluator-visible identities and values were
not uniquely authorable. The disclosed public gap category includes the exact
identity token for required FaceGen taxonomy subjects and related exact
link/state vocabulary. No private path, member identity, input detail,
expected value, raw output, or answer-bearing hash is authority for this plan.

This terminal state is not a product `PASS` or `FAIL`. No valid held-out
comparison has occurred.

## Diagnosis

Two public defects must be corrected together.

### Authority-bundle omission

The accepted
[public product realignment plan](M1-slice-4.5-public-product-realignment.md)
already selects this exact FaceGen product subject form:

```text
{winning-contribution-id}:semantic:face-gen-loose-provider-chain:{normalized-relative-path}
```

It also selects `record-semantic-subject` and explains the frozen `/4`
canonical result. The B2 public positive allowlist did not include that plan.
The next public handoff must not omit accepted authority needed to construct a
held-out-visible value.

### Normative-contract incompleteness

The current
[oracle-authority matrix](../../evaluation/m1-slice4-heldout-oracle-authority-matrix.md)
correctly separates held-out semantics from product-generated identifiers, but
it is not yet a complete lexical construction contract. Conceptual phrases
such as “explicit null state,” “links by field/component/ordinal,” and
“derived from each provider chain” do not uniquely specify every exact fact
identifier and string value preserved by the frozen canonicalizer.

Because exact state strings, subject suffixes, components, casing,
normalization, typed-null rules, and ordering participate in fact identities
or values, they must be accepted public authority. They may not be recovered
from product output or treated as correct merely because the candidate emits
them.

## Owner decisions recorded by this plan

1. Protocol `/4`, projection `3.0.0`, schemas, canonicalizer, scorer, adapter,
   calibration, and evaluator commit
   `3693d19563c636cd2879804633ca4ce52448d2c1` remain final and unchanged.
   Protocol `/5` is neither needed nor authorized.
2. Candidate `a98d648bd0adb2751ee0c09828e0227b1583950f` remains the frozen product
   candidate while the public contract is completed. Its behavior is an object
   of conformance review, never oracle authority.
3. Public evaluation authority must include one normative oracle-construction
   specification that uniquely defines every active `/4` fact from accepted
   semantic rules, answer-free manifest values, and retained bytes.
4. The specification is written at the evaluator-owned canonical fact layer.
   Product-generated contribution, participant, assignment, evidence, gap,
   winner, and snapshot identifiers remain excluded from held-out truth.
5. Link-state vocabulary is exactly `null`, `resolved`, and `unresolved`.
   `unspecified` is invalid for publication and is never an expected held-out
   value.
6. Supported link field tokens are the literal uppercase Bethesda subrecord
   signatures admitted by the bounded M1 contract: `TPLT`, `RNAM`, `HCLF`,
   `PKID`, `PNAM`, `NAME`, `XLKR`, `XLRL`, and `XOWN`. The only non-null
   component tokens are `linked-reference` and `keyword` for the paired `XLKR`
   values.
7. The accepted `record-semantic-subject` suffix registry is closed for M1:
   `area.actors.ai-packages`, `area.actors.appearance-identity`,
   `area.world.placed-objects-activation`, and
   `face-gen-loose-provider-chain:{normalized-relative-path}`. The FaceGen
   suffix is emitted once for each distinct declared mesh or tint provider
   chain, including a single-provider chain.
8. Exact wire vocabulary for FaceGen applicability, asset availability,
   master style, taxonomy applicability/role, coverage state, gap population,
   missing capability, subject type, taxonomy ID/version, axis, facet, and code
   must be enumerated in the normative specification. No `Unspecified` enum
   member is an accepted published value.
9. The private attempt's terminal contamination state remains recorded as
   clean. This plan does not decide that the current inputs remain eligible
   after public authority is changed. A separate fresh corpus-governance role
   must make that determination from sanitized evidence after public
   completion.
10. A later B2 operation, if authorized, is a new owner-authorized successor
    authoring task under the same frozen `/4` evaluator. It is not an automatic
    retry or continuation of the terminal attempt. This plan does not itself
    authorize that task.

## Authority and dependencies

Read repository `AGENTS.md` and its required product/architecture sequence
before acting. Then read these task-specific authorities in order:

1. [M1 backend semantic proof revision 3](../milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md);
2. [ADR-0026](../../architecture/decisions/ADR-0026-evaluator-private-fixture-repository-and-delegated-access.md);
3. [ADR-0027](../../architecture/decisions/ADR-0027-public-evaluation-protocol-private-held-out-corpus.md);
4. [ADR-0028](../../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md);
5. [Skyrim SE mod-impact taxonomy](../../product/mod-impact-taxonomy.md);
6. [evaluator-private fixture governance v2](../../evaluation/evaluator-private-fixture-governance-v2.md);
7. [fixture guidelines](../../evaluation/fixture-guidelines.md);
8. [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md);
9. [evaluation baseline evaluator-v2 amendment](../../evaluation/m1-evaluation-baseline-evaluator-v2-amendment.md);
10. [final held-out scope amendment](../../evaluation/m1-slice4-heldout-scope-final-amendment.md);
11. [current oracle-authority matrix](../../evaluation/m1-slice4-heldout-oracle-authority-matrix.md);
12. [semantic-authority owner disposition](../../evaluation/m1-slice4-semantic-authority-owner-disposition.md);
13. [semantic and ground-truth specification](../../evaluation/specifications/m1-semantic-and-ground-truth.md)
    and its accepted v2 amendment;
14. [M1 semantic fixture manifests](../../evaluation/fixtures/m1-semantic-fixture-manifests.md);
15. [final bounded evaluator freeze](../../evaluation/evaluator-v2-stage-a-final-bounded-freeze.json);
16. [public product candidate freeze](../../evaluation/m1-slice4.5-public-product-candidate-freeze.json);
17. the parent [Slice 4.5 evaluator-v2 plan](M1-slice-4.5-held-out-evaluation-v2.md);
18. the completed [public product realignment plan](M1-slice-4.5-public-product-realignment.md);
19. the [Slice 4.5 implementation record](../implementation-records/M1-slice-4.5.md); and
20. frozen evaluator `/4` public documentation, schemas, adapter, and
    canonicalizer at exact commit
    `3693d19563c636cd2879804633ca4ce52448d2c1`.

The accepted documents and owner decisions above supply semantic authority.
The frozen evaluator supplies public canonical mechanics. Public candidate
source and tests may be inspected only in the conformance phase after the
product-blind specification and authorability evidence are immutable in the
working diff. Candidate behavior must not be fed back into the specification.
Prior Codex sessions, rollout memory, chat transcripts, and agent summaries are
not semantic authority. Do not expose memory-derived product behavior to the
product-blind reviewer. If the host requires a memory preflight, keep it outside
that role and verify every retained fact against the positive-allowlist public
files.

## Required preflight

Before editing:

1. Confirm the working directory is the public `infinium` repository, not the
   sibling evaluator-private repository.
2. Record `git status --short --branch`, `git log -5 --oneline`, and current
   branch/HEAD. Preserve all pre-existing user changes.
3. Verify that these public identities are ancestors of the current checkout:
   - evaluator `/4`: `3693d19563c636cd2879804633ca4ce52448d2c1`;
   - candidate: `a98d648bd0adb2751ee0c09828e0227b1583950f`;
   - candidate-freeze closeout: `2fc724a`; and
   - public status closeout: `76136c1`.
4. Verify the evaluator freeze and candidate freeze records retain the exact
   identities above. Do not rewrite either freeze record.
5. Confirm that no private fixture repository path is open, searched,
   enumerated, or mounted into a command.
6. Inventory every projected fact ID/value construction in frozen
   `SemanticCanonicalizer.cs` and map it to one of the fifteen active families.
7. Inventory every held-out-visible lexical value admitted by the current
   public authority. Record omissions before inspecting product source.

If the starting tree contains overlapping edits, an identity differs, a
required authority conflicts with this plan, or private access would be needed,
stop and report the discrepancy.

## Scope

### Included

- create a normative protocol `/4` oracle-construction specification;
- complete the oracle-authority matrix so its “complete” claim is true;
- close exact subject, link, enum, normalization, ordering, aggregation,
  and null/missing rules for all fifteen fact families;
- add generic answer-free public examples and authorability evidence;
- add or strengthen public tests that detect drift between the normative
  specification and frozen canonical mechanics;
- perform a product-blind authorability review before candidate inspection;
- compare the immutable completed specification to the frozen evaluator and
  candidate after the authorability gate passes;
- record whether the result is documentation-only conformance, product
  mismatch, or an unresolvable evaluator `/4` gap;
- update public Slice 4.5 status and implementation records with the sanitized
  B2 terminal state and this task's outcome; and
- produce a bounded handoff for the next separately authorized role.

### Explicit non-scope

- any read, search, enumeration, inspection, modification, or execution in
  `../infinium-evaluator-fixtures/`;
- hidden input bytes, hidden expected outputs, private manifests, private paths,
  answer-bearing hashes, or predecessor answers;
- prior-session or memory-derived product behavior as specification or review
  authority;
- modification of `tools/evaluation/Infinium.EvaluatorV2/**`;
- modification of evaluator schemas, canonicalizer, scorer, adapter,
  calibration, protocol identity, or projection identity;
- modification or replacement of
  `docs/evaluation/evaluator-v2-stage-a-final-bounded-freeze.json`;
- modification or replacement of
  `docs/evaluation/m1-slice4.5-public-product-candidate-freeze.json`;
- product implementation changes during this task;
- a new product candidate or candidate freeze;
- private B2, oracle authoring, corpus qualification/freeze/tagging,
  contamination adjudication, replacement, C2, Stage D, or scoring;
- `adapt`, `compare-prepared`, `score`, or `score-corpus` against private data;
- protocol `/5` or any evaluator expansion;
- Slice 5 implementation;
- legacy archive access;
- live or billable external calls; and
- pushing.

## Deliverables

### 1. Normative oracle-construction specification

Create:

```text
docs/evaluation/specifications/m1-slice4-protocol-4-oracle-construction.md
```

Mark it accepted and bind it to:

- evaluator commit `3693d19563c636cd2879804633ca4ce52448d2c1`;
- protocol `infinium.evaluator-v2/4`;
- scorer/adapter `4.0.0`;
- projection `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`;
- taxonomy `infinium.mod-impact-taxonomy/0.1.0`; and
- the owner decisions in this plan, ADR-0028, and the semantic-authority owner
  disposition.

For each of the fifteen active families—`result`, `plugins`,
`override_chains`, `npc_contributions`, `race_contributions`,
`placed_reference_contributions`, `allowlisted_fields`, `npcs`, `races`,
`placed_references`, `face_gen`, `taxonomy`, `coverage`, `gaps`, and
`result_gaps`—the specification must define:

- admissible input evidence and semantic authority;
- exact canonical fact-ID templates;
- fact type, value type, and exact string vocabulary;
- casing, escaping, path, FormKey, number, and identity normalization;
- stable ordering and ordinal rules;
- explicit null versus absent fact behavior;
- duplicate, aggregation, and invalid-state behavior;
- which values come from the answer-free manifest rather than hidden bytes;
- which product-generated values are intentionally excluded; and
- at least one generic positive, negative, null/missing, and mutation example
  where applicable.

The specification must be sufficient to author expected semantic facts
directly. It must not require constructing product IDs or serializing a
candidate-shaped product snapshot first.

### 2. Exact common lexical contract

The specification must include a single common lexical section used by every
family. At minimum it must close:

- canonical FormKey formatting and light/full origin semantics;
- evaluator-owned semantic contribution identity;
- URI escaping of fact-ID segments;
- manifest sequence versus semantic-set ordering;
- zero-based ordinal formatting and link identity;
- uppercase record/subrecord signatures versus lowercased identity segments;
- plugin/provider ID normalization;
- slash-normalized lowercase FaceGen paths;
- numeric finiteness and semantic equality;
- typed null versus missing fact rules;
- all accepted link states, singleton and repeatable link behavior, target
  FormKey rules, and `XLKR` component rules;
- all accepted taxonomy subject types and the closed semantic-suffix registry;
- taxonomy applicability and classification-role wire strings;
- FaceGen applicability and loose-asset transport vocabulary;
- all coverage-state strings admitted by this bounded projection;
- exact gap population/capability strings and aggregation keys; and
- invalid or unpublished `unspecified` states.

### 3. Normative matrix and authority-bundle repair

Update the oracle-authority matrix to reference the new specification as the
normative lexical construction contract. Remove or expand any sentence that
claims completeness while leaving a value implementation-defined.

Update the semantic-authority owner disposition only where needed to bind the
new exact lexical rules. Do not rewrite its historical six-question decision.

Define one public positive allowlist for a future private authoring handoff.
The allowlist must include the new specification and the completed public
realignment plan. It may include frozen evaluator documentation, schemas, and
canonicalizer as mechanics. It must not include product source, product tests,
candidate diffs, candidate output, or public synthetic expected answers as
semantic authority.

Do not create or execute the private handoff prompt in this task.

### 4. Answer-free public authorability package

Create or extend a public, generic, answer-free synthetic package that exercises
all fifteen families and every lexical rule added by this plan. Reuse existing
public Bethesda fixture tooling where it remains independent and appropriate.
Do not introduce real mod names, private identities, or facts inferred from the
sanitized hidden outcome.

The package must include:

- synthetic byte/manifest inputs with no expected output embedded in the
  authoring prompt;
- a coverage ledger mapping every specification rule to an exercised case;
- matched positive and negative cases;
- explicit-null and missing cases;
- resolved and unresolved link cases;
- singleton and repeatable link cases, including both `XLKR` components;
- all four accepted semantic-subject suffix forms;
- single-provider and multi-provider FaceGen chains;
- zero-denominator and gap-bearing coverage rows;
- duplicate and invalid-state mutations; and
- deterministic mechanical checks for identity, ordering, typed values,
  completeness, and duplicate fact IDs.

Generated scratch output belongs under an ignored `work/` root. Tracked inputs,
scripts, tests, or attestations must remain generic and answer-free where their
publication would otherwise trivialize the review.

### 5. Product-blind independent authorability review

Before inspecting `src/`, product tests, candidate diffs, candidate assemblies,
or candidate output, arrange one fresh public-only reviewer with this positive
allowlist:

- accepted product/architecture/evaluation documents;
- the new normative oracle-construction specification;
- the answer-free synthetic input package;
- frozen evaluator `/4` public schemas and canonicalization mechanics; and
- no product implementation or expected answers.

Use a genuinely fresh context with no inherited implementation conversation or
product-behavior memory. A bounded delegated sub-agent with no forked turns is
the preferred mechanism when available.

The reviewer must independently construct all expected canonical facts, prove
coverage of all fifteen families, and report whether any exact value remains
ambiguous. The reviewer must not edit the normative specification.

If the reviewer finds an authority gap, stop. The primary may make at most one
focused public-specification correction pass, then the same reviewer must
re-review the corrected specification against a reset answer-free exercise. A
second material authority gap ends the task and returns to the owner.

Retain an exact public attestation recording inputs, roles, methods, commands,
coverage, findings, corrections, and answer-isolation state. Do not claim that
authorability proves product conformance.

### 6. Frozen conformance comparison

Only after the product-blind authorability gate passes and its specification
and attestation are immutable in the working diff may the primary inspect the
current public product source and tests.

Compare the completed independent contract to:

- frozen canonicalizer and adapter mechanics at evaluator commit
  `3693d19563c636cd2879804633ca4ce52448d2c1`;
- frozen candidate source at
  `a98d648bd0adb2751ee0c09828e0227b1583950f`;
- the exact candidate freeze record; and
- public product/evaluator tests.

Use generic public fixtures only. Candidate behavior may confirm or fail
conformance; it may not revise the independent specification.

Classify the result as exactly one of:

1. **Documentation-only conformance:** the completed public specification,
   frozen evaluator, and frozen candidate agree. No new candidate or evaluator
   freeze is needed.
2. **Product mismatch:** evaluator `/4` represents the independent contract,
   but candidate `a98d648` differs. Record the exact generic mismatch and stop;
   do not edit product code in this task. A separate owner-authorized product
   correction and candidate-freeze plan is required, and private-input
   eligibility must be reconsidered under contamination rules.
3. **Unresolved evaluator `/4` gap:** the independently required fact cannot be
   represented or uniquely canonicalized by frozen `/4`. Record the M1 held-out
   gate as unresolved and stop. Do not create `/5`, change the evaluator, or
   waive the held-out gate.

### 7. Public status and closeout

Update, at minimum:

- `docs/plans/README.md`;
- `docs/plans/slices/README.md`;
- the parent Slice 4.5 plan;
- the Slice 4.5 implementation record;
- the M1 evaluator-v2 milestone amendment;
- the oracle-authority matrix;
- the semantic-authority owner disposition;
- applicable evaluation-specification indexes or amendments; and
- any contract tests that intentionally pin current status language.

Record that the authorized B2 resume ran once and terminated without an oracle,
candidate execution, scoring, or product verdict. Correct every live status,
front-matter, and index statement that says B2 is unrun or directly authorized
next. Preserve append-only historical sections that accurately described an
earlier checkpoint, and add a later superseding record rather than rewriting
their history.

If and only if documentation-only conformance is proven, record:

- public authority completion: passed;
- evaluator `/4`: unchanged and still frozen;
- candidate `a98d648`: unchanged and still frozen;
- private terminal contamination state: clean at the recorded stop;
- private input eligibility: pending separate fresh governance disposition;
- next role: fresh corpus-governance/custodian disposition; and
- B2 successor authoring, corpus qualification, C2, Stage D, Slice 5: not
  authorized by this closeout.

Do not state or imply that the prospective corpus is qualified or that a
held-out result exists.

## Public verification

The implementer must derive the final focused test filter from changed
surfaces, then run at least:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
```

Also run:

- all new oracle-contract and answer-free authorability checks;
- focused `M1Contract` and `M1Evaluation` categories;
- frozen evaluator public calibration twice and verify byte-identical output;
- repository scans for private locators, hidden identities, answer-bearing
  values, fixture-specific production branches, and forbidden `/5` changes;
- a changed-file/protected-path audit; and
- relative Markdown-link validation for every changed document.

Expected private/platform/machine-identity skips must be reported exactly and
must remain expected. Do not satisfy a skipped private test by accessing the
private repository.

## Review cycle

After implementation and verification:

1. review the complete diff against this plan, the parent plan, ADR-0027,
   ADR-0028, governance v2, and anti-overfitting rules;
2. verify every frozen evaluator and candidate identity;
3. confirm every active fact family is fully specified and exercised;
4. confirm no rule was copied from candidate output or hidden detail;
5. confirm no product/evaluator implementation or freeze artifact changed;
6. reconcile the fresh product-blind reviewer report;
7. make at most one focused correction pass for public specification or test
   defects;
8. rerun affected and full checks; and
9. perform a final semantic and diff re-review.

Do not stop merely because tests pass. Any remaining ambiguity in an expected
fact is a material finding.

## Stop conditions

Stop and return to the project owner if:

- a required exact fact remains ambiguous after the permitted correction pass;
- an accepted authority contradicts this plan;
- public authorability requires product source or output as truth;
- candidate inspection occurs before the product-blind authorability record is
  complete;
- frozen evaluator `/4` would need to change;
- a product mismatch is found;
- private-input eligibility cannot be decided without private access;
- any private locator, member identity, input detail, expected value, raw
  output, or answer-bearing hash is disclosed;
- a real-mod or private-case-specific rule would be required;
- frozen identities differ from their accepted records;
- an unauthorized protected path changes;
- a second material public authority gap is found after correction; or
- independent review leaves a material finding unresolved.

Do not respond to a stop by creating `/5`, editing the candidate, expanding
the evaluator, accessing private data, weakening the held-out gate, or
authorizing another B2 attempt.

## Completion criteria

This plan is complete only when:

- the normative oracle-construction specification covers all fifteen active
  fact families and every held-out-visible lexical rule;
- the oracle-authority matrix and public authority bundle are complete and
  mutually consistent;
- a fresh product-blind reviewer independently authors the answer-free
  rehearsal without unresolved ambiguity;
- frozen evaluator and candidate conformance are classified exactly once;
- the classification is documentation-only conformance, or a required stop is
  recorded without unauthorized repair;
- all public verification and review gates pass;
- public status no longer says B2 is unrun or directly authorized next;
- no private access, candidate scoring, protocol `/5`, product/evaluator code
  edit, candidate refreeze, or push occurred;
- the Slice 4.5 implementation record contains exact files, checks, results,
  skips, identities, reviewer findings, corrections, and remaining gates; and
- the repository is clean after one focused local commit, unless pre-existing
  user changes prevent that, in which case they are preserved and reported.

## Post-completion boundary

Successful public completion does not authorize private execution. The next
role is a separate fresh corpus-governance or custodian task that receives only
the accepted public artifacts and permitted sanitized B2 evidence. It must
decide whether the unchanged frozen inputs remain eligible for a future
held-out claim after the public clarification.

If eligibility is retained, the owner may separately authorize one new fresh
oracle author to construct a successor oracle under the same frozen `/4`
evaluator and completed public authority. If eligibility is not retained, the
affected case version must be reclassified and materially independent
replacement coverage must be qualified before any later held-out claim.

Only a separately qualified and frozen private corpus can unblock C2. Only one
valid held-out `PASS` for the exact candidate/evaluator/corpus tuple can
complete Slice 4.5 and unblock Slice 5.

## 2026-08-05 implementation disposition

The public attempt reached the required fresh product-blind authorability
review without inspecting candidate source or tests. The initial review found
an ambiguous unsupported-member attachment, a missing zero-denominator
exercise, and validator defects. The primary made the single focused
correction pass permitted by this plan. Re-review closed the original coverage
arithmetic and zero-row omission but found a second material authority gap in
the cross-family projection of an admitted undecodable record shape. It also
found that the required Windows PowerShell validator still did not complete.

The plan's hard stop therefore applies. The completed review evidence is
recorded in the
[public authorability attestation](../../evaluation/m1-slice4-protocol-4-oracle-authorability-review.md).
The construction document remains a blocked draft, not accepted oracle
authority. Candidate source/tests were not inspected, so none of the three
frozen-conformance classifications was reached. This is not a product verdict
or an evaluator `/4` representation-gap finding.

Evaluator `/4` and candidate `a98d648` remain frozen and unchanged. The next
role is the project owner for a new milestone-plan disposition. This record
does not authorize another correction or reviewer, private B2 successor,
corpus qualification, C2, Stage D, Slice 5, or protocol `/5`.

## Fresh-agent implementation handoff

Use the following bounded task summary when delegating implementation:

```text
Implement the accepted public-only plan at
docs/plans/slices/M1-slice-4.5-protocol-4-oracle-contract-completion.md.

Read AGENTS.md and every authority named by the plan before editing. Preserve
all existing user changes. Do not access ../infinium-evaluator-fixtures or the
legacy archive. Do not modify frozen evaluator /4 code, schemas, canonicalizer,
adapter, scorer, calibration, either freeze JSON, or product code. Do not run
private B2, adapt, compare-prepared, score, score-corpus, C2, Stage D, or Slice
5. Do not create /5, use candidate behavior as oracle truth, make live/billable
calls, or push.

Do not use prior Codex sessions, rollout memory, chat summaries, or inherited
product-behavior context as semantic authority. You are explicitly authorized
and required to delegate one bounded fresh public-only product-blind reviewer
with no forked turns; do not delegate any private work.

Complete the normative oracle-construction specification for all fifteen fact
families, generic answer-free authorability package, public tests, fresh
product-blind review, frozen conformance comparison, and public status
closeout exactly as planned. Inspect candidate source only after the
product-blind specification and review record are complete and immutable in
the working diff. Stop on every plan stop condition. Finish with one focused
local commit and report exact identities, files, checks, skips, review
findings/corrections, private-access state, and next-role boundary.
```
