# M1 Slice 4 held-out scope final amendment

Status: Accepted

Accepted: 2026-08-04

Accepted by: Project owner

Last reviewed: 2026-08-05

Amends: Held-out portions of EVAL-0052 and applicable EVAL-0086

Accepted clarification: the exact bounded semantic choices within this
unchanged partition are governed by
[ADR-0028](../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md)
and the
[semantic-authority owner disposition](m1-slice4-semantic-authority-owner-disposition.md).
Protocol `/4` and projection `3.0.0` remain final.

## Purpose

This amendment defines the final M1 split between independently authorable
held-out semantics and public-conformance-only product contracts. It narrows
the held-out proof claim; it does not roll back Slice 4 functionality, weaken
the public product contract, or authorize a production change.

The overall evidence claim is:

```text
public conformance verifies implementation-specific details;
held-out evaluation verifies independently specifiable semantic behavior;
both partitions are reported separately.
```

## Independently authorable held-out semantics

A fact may be included in the private held-out oracle only when its expected
value can be derived independently from public byte-format and semantic rules,
the answer-free execution manifest, accepted taxonomy vocabulary and
applicability rules, and the hidden input bytes themselves.

Held-out construction must not require product implementation source, product
output, Mutagen as the oracle, an internal persistence or hash-ID algorithm,
exact product diagnostic spelling, or invocation-specific provenance
plumbing. Included facts use public evaluator-owned canonicalization and the
normative Slice 4 oracle-authority matrix.

For EVAL-0052, the held-out partition covers the independently specifiable
result boundary, plugin order and master facts, canonical records and
FormKeys, semantic contribution/override/winner facts, allowlisted field
presence, links, NPC/RACE/REFR semantics, `AIDT` presence, placement numbers,
FaceGen/provider topology, and independently specifiable coverage/gaps.

For applicable EVAL-0086, the held-out partition covers only the semantic
classification tuple: taxonomy ID/version, evaluator-owned canonical subject,
subject type, axis, facet, code or explicit null, applicability, and role.

## Public conformance-only product contracts

Public deterministic tests remain authoritative for legitimate product
contracts that an independent hidden oracle cannot reproduce without adopting
the implementation's own choices. This partition includes:

- exact stable product failure-code strings;
- typed `BethesdaAiDataFact` subfield mapping under the selected Mutagen-backed
  contract;
- taxonomy assignment IDs and analyzer/adjudicator IDs;
- internal evidence-reference strings;
- product contribution, participant, winner, gap, coverage-gap, persistence,
  serialization, and other internal provenance identifiers; and
- implementation-specific diagnostic or display vocabulary.

Public conformance is a distinct authoritative proof surface, not weaker
testing. No project status or report may imply that every product output field
is held-out verified.

## EVAL-0052 amendment

EVAL-0052 remains the public product-conformance requirement for every
allowlisted Slice 4 record, field, shape, failure, and publication behavior.
Its held-out claim is limited to independently specifiable semantic behavior.
For malformed or pathological cases the held-out assertion is the correct
terminal state, no authoritative semantic snapshot, and presence of a
reported failure—not an exact diagnostic code.

Typed `AIDT` subfields remain public-conformance-only. The held-out oracle
asserts `AIDT` presence and accepted field presence/shape only.

## Applicable EVAL-0086 amendment

EVAL-0086 continues to require public taxonomy contract and provenance tests.
Its Slice 4 held-out assertion compares the semantic classification tuple only.
Internal assignment, analyzer/adjudicator, evidence-reference, subject
plumbing, and provenance IDs are excluded. Duplicate candidate assignments
that collapse to one semantic tuple are a candidate-output contract violation.

## Partition reporting and status

The original Slice 4 product candidate remains historical public evidence but
does not conform to the later ADR-0028 contract.
The historical `/2` Stage C product verdict remains invalidated. Protocol `/3`
is qualified public evidence but was superseded before successor corpus
qualification because its projection required non-independently-authorable
expected values. Protocol `/4` is qualified and frozen. Public
product/specification realignment, independent review, requalification, and a
new candidate freeze completed at
`a98d648bd0adb2751ee0c09828e0227b1583950f`. The existing independently
reviewed successor inputs may now resume B2 oracle construction once. Stage C2
has not run and Stage D has not started.

Passing public conformance alone does not complete the held-out partition.
Passing the narrowed held-out partition does not claim conformance for excluded
internal product fields. Both partitions must be reported separately.

### Qualification status after implementation

Protocol `/4` was qualified and frozen at
`3693d19563c636cd2879804633ca4ce52448d2c1`. The existing independently
byte-reviewed successor inputs may now resume B2 once under the
[final handoff](evaluator-v2-stage-a-final-bounded-freeze.json). Oracle
qualification, comparison, corpus freeze/tag, Stage C2, and Stage D have not
run under `/4`.

## Final-revision hard stop

Protocol `/4` is the final M1 held-out evaluator revision. After `/4` is
frozen, a fresh private reviewer may resume B2 once. If that reviewer still
cannot author an authoritative oracle because of another public-contract or
projection gap, do not create protocol `/5`, do not expand the evaluator
again, and do not use product output as truth. Record the held-out gate as an
unresolved evaluation gap and return to the project owner for milestone-plan
disposition.

This amendment does not waive the held-out gate, unblock Slice 5, authorize
Stage C2, or start Stage D.

## Superseding final-revision disposition

The authorized `/4` B2 resume ran once and stopped without an oracle or product
verdict. The subsequent public contract-completion attempt used its single
correction pass, but independent re-review found a second material authority
gap before candidate inspection. The final-revision hard stop therefore
remains active. Project-owner milestone-plan disposition is required; no
private successor, corpus qualification, C2, Stage D, Slice 5, or `/5` is
authorized.
