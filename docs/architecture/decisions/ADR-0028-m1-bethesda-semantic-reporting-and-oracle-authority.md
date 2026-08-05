# ADR-0028: Bound M1 Bethesda semantic reporting and oracle authority

Status: Accepted
Date: 2026-08-05
Deciders: Project owner

## Context

The Slice 4.5 authority-completion rehearsal proved that protocol `/4` is
independently authorable, but it also exposed six places where the proposed
oracle-construction rules and the frozen Slice 4 candidate represented
different product semantics. Neither the candidate nor the rehearsal draft is
automatically authoritative. The project owner must choose the intended
product behavior before a private oracle can be frozen.

This ADR records that choice. It is a public product and evaluation contract;
it does not accept any private expected output, inspect any private fixture, or
authorize held-out scoring.

## Decision

### 1. `EDID` is admitted identifying metadata

`EDID` is in the bounded M1 semantic-field allowlist for `NPC_`, `RACE`, and
`REFR`. Its presence and occurrence count may be projected. `EDID` alone must
not establish purpose, affected area, consequence, finding, or user intent.

### 2. FaceGen applicability uses a closed precedence order

For each winning NPC, determine FaceGen applicability in this order:

1. a deleted winner is not applicable and is not a coverage gap;
2. an undecodable template-traits decision is unknown with the template
   decision reported as the missing capability;
3. definite template-traits inheritance is not applicable;
4. a missing, null, or unresolved race—or a race whose `FaceGenHead` decision
   is unknown—is unknown with race resolution reported as the missing
   capability;
5. a resolved race explicitly lacking `FaceGenHead` is not applicable; and
6. otherwise FaceGen is applicable.

Merely using a template for features other than traits does not suppress the
NPC's own FaceGen assessment.

### 3. Asset availability is a semantic tri-state

The preferred product model is a single `availability` value: `present`,
`absent`, or `unknown`. This is the typed form of the owner's proposed
three-state field and avoids mixing booleans with a string sentinel. Protocol
`/4` retains its existing `present` and `exact_absence_known` fields as a
transport encoding:

| Semantic state | `present` | `exact_absence_known` |
|---|---:|---:|
| `present` | `true` | `false` |
| `absent` | `false` | `true` |
| `unknown` | `false` | `false` |

`true/true` is invalid. A winning provider is required only for `present`.
Exact absence requires an exhaustive, byte-verified loose-provider index.
Archive-member support is a separate coverage capability and does not change
the loose-asset state. Product and user-facing surfaces should expose the
tri-state rather than asking consumers to interpret the transport pair.

### 4. Backend coverage uses a fixed registry

Every published bounded-M1 snapshot retains these ten coverage populations,
including zero-denominator rows:

- `plugins`;
- `npc-records`;
- `race-records`;
- `placed-reference-records`;
- `unsupported-records`;
- `face-gen-loose-assets`;
- `face-gen-archive-assets`;
- `localized-strings`;
- `automatic-environment-discovery`; and
- `taxonomy-subjects`.

This complete registry is required in backend, persisted, exported, and test
surfaces. A user-facing summary may omit zero-denominator rows provided the
full detail remains available. A zero denominator is completed, not unknown.

### 5. Coverage gaps are layered

Stable broad categories drive aggregation and UI grouping; exact details name
the affected signature, field, shape, capability, or reason when known. The
protocol `/4` projection continues to compare only semantic population,
denominator, and missing capability. Product-generated gap IDs and incidental
reason prose remain outside held-out authority.

### 6. Taxonomy emission is hybrid and evidence-bounded

The persisted axis/facet identifiers already used by the accepted product
taxonomy remain canonical. Every contribution receives the required technical
`surface.plugin-data` and `delivery.plugin-container` assignments. Further
assignments are emitted only when decoded semantic or provider evidence makes
them meaningful. A mandatory
all-null matrix is rejected. Explicit unknown, unsupported, unmapped, or
not-applicable assignments are emitted only when they communicate a real
conclusion required by the subject contract.

FaceGen loose-provider subjects are created for every declared provider chain,
including a single-provider chain. No generic provider-topology subject is
inferred solely from the plugin list. Names, signatures, and `EDID` values do
not independently authorize semantic classification.

## Alternatives considered

- Treating the frozen candidate as the oracle was rejected because held-out
  truth must remain independently authored.
- Accepting the rehearsal specification wholesale was rejected because it was
  a review proposal, not accepted product authority.
- Creating protocol `/5` was rejected because protocol `/4` can represent all
  selected semantics without a schema or scorer change.
- Keeping the two booleans as the preferred domain concept was rejected because
  the tri-state is clearer and rules out an otherwise ambiguous fourth state.

## Consequences

- The six authority questions are resolved and may be used by a fresh oracle
  reviewer after the public contracts and candidate are brought into
  conformance.
- The frozen Slice 4 candidate is historical evidence, not a conforming build
  of this accepted contract. Product implementation and public requalification
  require a separate authorized task.
- The non-normative authority-completion draft remains useful review evidence
  but is superseded wherever it conflicts with this ADR.
- Protocol `/4`, projection `3.0.0`, and evaluator behavior remain unchanged.
- Private B2 remains blocked until the public product/specification alignment is
  complete and its exact candidate identity is frozen.

## Requirements affected

`SCOPE-005`, `EVID-006`, `FIND-001`, `COVER-001`, `COVER-002`, `UX-002`,
`ANALYSIS-003`, `ANALYSIS-005`, `ANALYSIS-006`, `ANALYSIS-016`, and
`ANALYSIS-019`.
