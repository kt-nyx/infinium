# M1 Slice 9

Status: Proposed
Disposition: Proposed M1 end-to-end closeout plan, pending project-owner
acceptance; planning only, with no Slice 9 implementation or activation
authority

Last reviewed: 2026-08-24
Owner: Project owner

Slice 9 is the separately planned M1 closeout. In plain language, it is meant
to prove that the already accepted M1 parts work together through the real CLI,
produce one truthful stable result, replay from retained dependencies, and
leave a reviewable case-by-case completion record. It is not a new analyzer or
permission to broaden product meaning.

Live authorization is stated only in
[current project state](../../../../../current-state.md). This proposed entry
does not activate implementation, controlled-real input access, credentials,
providers, network use, external effects, private material, evaluator work,
semantic-oracle work, merge, or push.

## Proposed authority package

- [Proposed Slice 9 plan](plan.md)
- [Accepted Slice 8 closeout](../s8/README.md)
- [Accepted Slice 8 implementation record](../s8/record.md)
- [Accepted M1 milestone plan](../../plan.md)
- [Accepted M1 process-continuation amendment](../../amendments/process-continuation.md)
- [Accepted semantic-oracle deferral amendment](../../amendments/semantic-oracle-deferral.md)
- [M1/M2 product-conformance verification profile](../../../../../evaluation/m1-continuation-verification-profile.md)

## Proposed decision

Accept Slice 9 as an integration-and-evidence package over the frozen Slice 5
through Slice 8 product. The implementation would add a bounded durable
composition path for one synthetic and one controlled-real CLI run, project
the already accepted artifacts into the frozen `infinium.run-output/v1`
contract, prove persistence and replay, re-run the complete required M1 case
set on one final candidate, and produce the M1 completion record.

The plan proposes no in-place product-contract revision, no storage-schema
revision beyond frozen storage 1.10.0/schema 11, and no new product semantics.
If implementation evidence shows that the frozen contracts cannot express the
required composition truthfully, that is a genuine architecture/owner stop;
the implementer must not invent a successor contract or reinterpret a frozen
field.

## Predecessor and next decision

The exact planning base and Slice 8 acceptance commit is
`5f176a643d1d44d7c254d3b7e6c48f33944909a9`. The accepted predecessor product
is `c79661cd8eb016e483fa8b7396e7d4997b85d590`, with review-ready documentation
handoff `c5c995de7252ebf0002903c2d908fdb3bca80f40`.

The next decision is only whether to accept, reject, or amend this proposed
plan. Even acceptance does not imply that implementation is already active;
`docs/current-state.md` must record the exact implementation base and activation
boundary separately before any Slice 9 product work or controlled-real input
read begins.
