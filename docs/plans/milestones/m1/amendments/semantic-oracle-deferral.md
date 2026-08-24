# M1 amendment: defer independent semantic-oracle qualification

Status: Accepted
Date: 2026-08-23
Last reviewed: 2026-08-23
Authority: [ADR-0035](../../../../architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md)

## Practical effect

M1 product work continues under the revised
[M1/M2 product-conformance profile](../../../../evaluation/m1-continuation-verification-profile.md).
No independent semantic-oracle package, comparison, review receipt, seal, or
`PASS` is required for M1 acceptance. This amendment changes evaluation timing
only; it does not change accepted product requirements, product semantics, the
ordinary correction-and-review process, or any effect-authority boundary.

For Slice 6, semantic-admission packages v1-v13 are historical
non-authorizing development evidence. They remain available for byte/hash and
manifest integrity, but are not compared with current producer or consumer
output. The unaccepted v14 draft is not a candidate or authority. No successor
may be created before an accepted M3 plan reopens the work.

The next Slice 6 gate is a separate fresh-agent product closeout under ordinary
product conformance. Slice 6 remains implementation-active and cannot be
accepted by this amendment; Slice 7 remains unopened.

## M1/M2 acceptance evidence

The required evidence is:

1. contract and schema conformance;
2. developer-owned bounded examples;
3. invalid-state, mutation, and metamorphic coverage;
4. persistence, migration, replay, and operational safety;
5. controlled integration and generalization evidence appropriate to the
   delivered scope; and
6. fresh semantic, security, provenance, and diff review.

`TestCategory=Evaluation` remains part of the normal test floor. Only tests
whose purpose is independent semantic answer-key qualification are historical
or deferred.

## Later re-entry

The M2 acceptance / M3 planning boundary is the **M3 Evaluation Readiness
Gate**. It uses ADR-0035's complete prerequisite list and grants no authority
until a new accepted M3 evaluation plan explicitly authorizes a bounded
feasibility package and any later authoring, review, sealing, or comparison.
