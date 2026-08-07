# M1 Slice 4 protocol `/5` WP1 representability hard stop

Status: Recorded — blocking accepted-model composition gap; owner disposition required
Recorded: 2026-08-07
Work ID: `M1/S4.5/PRE-B2/V5/WP1`
Input commit: `2ffd40e34bd58c08e332e4f23b7e132afcf83f19`

## Finding

WP1 cannot satisfy the defining representation invariant without changing
accepted semantics. The accepted rule
`P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED` has no legal compositional
coverage outcome for `face-gen-loose-assets` under accepted model `1.2.0`.

This is a new public authority gap. It is not the frozen `/4` transport gap and
cannot be fixed by making a property optional or by implementing a different
canonicalizer.

## Exact contradiction

For one applicable FaceGen path with unknown loose availability and supported,
resolved archive authority, the accepted FaceGen rule requires:

- `face-gen-loose-assets`: denominator increment one, completion increment
  zero;
- `face-gen-archive-assets`: denominator increment one, completion increment
  one;
- no gap effect; and
- no failed, skipped, or other terminal lifecycle condition.

The accepted coverage model simultaneously requires:

- a positive `completed` row to have `completed == denominator` and no gap;
- `completed_with_gaps` or `unsupported` to have
  `gap_scope=snapshot-and-result`;
- `failed`, `skipped_by_configuration`, or `skipped_by_limit` only when that
  actual lifecycle is established; and
- every published snapshot to contain all ten fixed coverage rows.

It explicitly classifies a positive `completed_with_gaps` or `unsupported` row
with `gap_scope=none` as invalid under
`SC-COVERAGE-GAPPED-WITHOUT-GAP` / `AB-GAP`.

Therefore no legal row exists:

| Candidate row | Why prohibited |
|---|---|
| `1/1/completed` | Invents loose completion contrary to the FaceGen rule |
| `1/0/completed` | Violates exact completion arithmetic |
| `1/0/completed_with_gaps` | Invents a gap that the FaceGen rule does not emit |
| `1/0/unsupported` | Invents a gap/capability disposition and conflicts with the rule's resolved state |
| `1/0/failed` or skipped | Invents an unestablished lifecycle |
| Omitted row | Violates the fixed ten-row snapshot registry |

The existing archive gap is not available: archive capability is supported and
the archive obligation completes. Using a gap from an unrelated population
would violate exact ownership and would not make the isolated outcome total.

## Independent review

Four fresh read-only public audits were delegated with positive allowlists and
no recursive delegation:

1. property optionality, omission, null, empty, unknown, unresolved,
   unsupported, and terminal semantics;
2. coverage and gap arithmetic;
3. schema/canonicalizer expressiveness; and
4. malformed/adversarial boundaries plus an independent check of this exact
   composition.

The optionality and expressiveness audits confirmed that `/4`'s final flat fact
array can represent omission; the controlling `/4` defect is its implicit raw
projection shape and object-atomic canonicalizer. They also confirmed that a
general constructor-granular `/5` representation can solve that transport
defect without a RACE-specific branch.

The coverage audit found the FaceGen/coverage contradiction. The fourth fresh
audit independently reproduced it and classified it as a semantic/authority
choice, not a mechanical representation correction.

The parent directly loaded the accepted model and reproduced:

```text
rule=P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED
loose_denominator=increment-one
loose_completion=no-increment
gap_effect_count=0
no_gap_incomplete_admitted_regions=0
explicit_invalid_region=SC-COVERAGE-GAPPED-WITHOUT-GAP
```

## Why earlier totality evidence did not catch it

The accepted `/4` validator reports 23,660 raw states, 110 admitted, 6,180
excluded, 17,370 invalid/terminal, zero uncovered, and zero overlap across 15
families, 21 dimensions, 77 rules, 24 constructors, 10 coverage populations,
8 gap rules, and 11 atomic boundaries. Its 515 generated cases cover those
family-local partitions.

That proof classifies each fact family independently. Its only explicit
structured cross-family invariant is partial `RACE/DATA`. It does not compose
each FaceGen coverage effect with a legal coverage-lifecycle state and owning
gap set. Consequently both the FaceGen state and the coverage family's
gapped-without-gap rejection can pass independently while their required
combination is impossible.

This does not rewrite the accepted model or its prior attestation. It records a
later public contradiction found by the stronger representability gate.

## Draft artifacts retained

WP1 produced proposed, non-authoritative drafts:

- `specifications/m1-slice4-protocol-5-projection-representation-contract.md`;
- `specifications/m1-slice4-protocol-5-projection-representation-model.json`;
- `specifications/m1-slice4-protocol-5-projection-representation-model.schema.json`;
  and
- `specifications/m1-slice4-protocol-5-projection-document.schema.json`.

They establish the generic constructor-level direction and exact null,
omission, empty, identity, and partial-object rules, but they cannot be accepted
while any admitted semantic outcome lacks a legal witness. Their status is
`proposed-blocked`; WP2 must not use them as accepted input.

Their retained SHA-256 identities are:

| Proposed artifact | SHA-256 |
|---|---|
| projection document schema | `65d970728b724ac9a82969ddffa113db8c5d10602ef4d46377b2850d99cba2b5` |
| representation contract | `4a3f58ee76d809d283d106cfd4fb54c853abe61690a003ef529f8e2878b0529f` |
| representation model | `3d152524b1dbb7d16597f0f39393c78709b01c223af46dd3a4b5fd7344441963` |
| representation-model schema | `397be1db74f92ce20f746a4174094243a819af9ed7513d243c82c15f4b134840` |

The three JSON files parse successfully. The proposed representation model
validates against its schema and contains 15 unique family contracts, 24 unique
constructor bindings, and 9 unique state-class mappings. Every referenced
constructor group and publication rule occurs in accepted model `1.2.0`; the
source model hash remains
`09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5`.

## Required owner disposition

Resolving the contradiction requires a semantic choice beyond ADR-0030, such
as one of these classes of change:

- authorize an exact gap for unknown loose availability;
- authorize an incomplete-without-gap coverage meaning or state;
- change the FaceGen loose denominator/completion rule; or
- remove or redefine the admitted unknown-loose/archive-supported state.

This record does not select or recommend one. Any choice changes accepted
model `1.2.0`, coverage/gap semantics, or both and therefore requires a new
owner-authoritative decision and a successor semantic-model identity. Protocol
`/5` implementation cannot proceed by silently choosing a value.

## Boundaries and status

- WP0 remains complete at
  `2ffd40e34bd58c08e332e4f23b7e132afcf83f19`.
- WP1 did not pass and had zero correction attempts; the issue is not within
  the correction budget because it requires new authority.
- WP2, WP3, and WP4 did not start.
- No evaluator code, protocol/schema files under the evaluator tool, adapter,
  scorer, calibration, manifest, dependency manifest, freeze, product,
  candidate, or historical `/2`–`/4` artifact was modified.
- No product/candidate source, tests, build output, runtime artifact, detached
  candidate worktree, private repository/content, legacy archive, B2, C2,
  Stage D, Slice 5, adaptation, scoring, live call, billable call, or push was
  accessed or executed.
