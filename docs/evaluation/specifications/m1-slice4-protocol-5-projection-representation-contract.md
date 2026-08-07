# M1 Slice 4 protocol `/5` projection-representation contract

Status: Proposed; WP1 hard-stopped on an accepted-model composition gap
Last reviewed: 2026-08-07
Work ID: `M1/S4.5/PRE-B2/V5/WP1`
Contract ID: `infinium.m1-slice4.protocol-5-projection-representation/1.0.0`

This draft is not accepted representation authority. The owner authorized its
construction, not its acceptance. WP1 found that accepted semantic model
`1.2.0` contains a FaceGen/coverage composition with no legal coverage-row
outcome. See the
[WP1 representability hard stop](../m1-slice4-protocol-5-wp1-representability-hard-stop.md).
No downstream implementation may treat this draft as complete.

## Purpose and authority

This contract drafts the intended public representation boundary between a
candidate adapter and protocol `/5` semantic canonicalization. It changes how
accepted facts are represented, not which facts are true.

Immutable semantic authority is the accepted model
`infinium.m1-slice4.protocol-4-evidence-contract/1.2.0`, SHA-256
`09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5`.
ADR-0028 and ADR-0029 supply its semantics. ADR-0030 authorizes `/5` solely to
restore complete representation. Candidate behavior, product output, private
data, private answers, and frozen `/4` behavior are not semantic authority.

The defining invariant is:

> Every accepted semantic outcome has at least one schema-valid `/5` document
> that canonicalizes to exactly that outcome, with no missing required facts
> and no extra facts.

## Proposed artifacts

- this draft document;
- [`m1-slice4-protocol-5-projection-representation-model.json`](m1-slice4-protocol-5-projection-representation-model.json);
- [`m1-slice4-protocol-5-projection-representation-model.schema.json`](m1-slice4-protocol-5-projection-representation-model.schema.json);
- [`m1-slice4-protocol-5-projection-document.schema.json`](m1-slice4-protocol-5-projection-document.schema.json); and
- accepted semantic model `1.2.0`, referenced by exact identity and hash rather
  than copied or changed.

The draft machine model inventories every accepted family, state class,
constructor group, coverage population, gap rule, atomic boundary, and
higher-order invariant intended for the `/5` document. It does not supply the
missing legal coverage outcome identified by the hard stop. The JSON Schema
validates lexical and structural shape. The
WP2 validator enforces cross-property, constructor, arithmetic, and exact-fact
rules that JSON Schema cannot express alone.

## Projection document

A projection document has exact identities, one accepted top-level result
state, and a `families` object containing all fifteen accepted family
containers. Every family container is always present as an array. An empty
array means that the accepted outcome has no object in that family; it does
not mean unknown, unsupported, null, or failed.

Each family object contains:

- `object_id`: the already canonical, family-local semantic identity; and
- `properties`: one or more independently authorable canonical properties.

Each property contains:

- `property_id`: a canonical relative path inside the object;
- `fact_type`: one accepted model fact type;
- `value_type`: `string`, `integer`, `number`, `boolean`, or `null`; and
- `value`: exactly the matching JSON primitive.

The canonical fact ID is:

```text
result/{property_id}                         when family=result and object_id=root
{family}/{object_id}/{property_id}           for every other object
```

No input-supplied absolute fact ID exists. This prevents a property from
escaping its declared family or object. `object_id` and `property_id` cannot
contain empty path segments, `.` or `..` segments, backslashes, control
characters, or percent-encoded path separators. Percent escapes must be
uppercase and must represent UTF-8 bytes. The canonicalizer rejects a duplicate
object ID, duplicate property ID within an object, or duplicate final fact ID.

Canonicalization processes family names in the model's fixed order, sorts
objects and properties by `StringComparer.Ordinal`, constructs final fact IDs,
validates every property against the selected accepted constructor group, and
then sorts final facts by ordinal fact ID. Input array order is not semantic.

## Presence, omission, null, and empty semantics

These meanings are universal and cannot be overridden by a family:

| Shape | Meaning | Canonical result |
|---|---|---|
| Family array present and empty | Accepted outcome contains no object in that family | No family facts |
| Object absent | No independently authorable object exists | No facts for that object |
| Object present with required common properties | The common facts are independently authorable | Emit exactly those common facts |
| Conditional property group absent | Its prerequisites are unavailable or the model says omit | Emit no facts from that group |
| Property present with `value_type=null`, `value=null` | Accepted typed null | Emit one typed-null fact |
| Empty sequence | The sequence is authoritatively empty | Emit no child facts; retain unrelated object facts |
| Accepted unknown state | Emit only the exact accepted unknown-valued facts and required gaps/coverage | Never coerce to null, false, absent, empty, or omitted |
| Unresolved link | Emit decoded target plus exact `unresolved` state | Never coerce to null or omit |
| Unsupported later layer | Retain earlier constructor groups, omit unsupported groups, and emit exact gap/coverage outcome | Never invent a later fact |
| Terminal state | Reject snapshot publication under the named atomic boundary | No partial snapshot fact set |

The schema disallows a JSON `null` object, family, object ID, property ID,
fact type, or non-null typed value. Null is legal only as a property whose
`value_type` is exactly `null` and whose `value` is JSON null.

## Container and constructor rules

The machine model would be normative for the complete list only after WP1
acceptance. The following summarizes
the family-level object boundary.

| Family | Object presence | Required base when present | Conditional groups |
|---|---|---|---|
| `result` | Exactly one `root` object | `FC-RESULT-STATE` | None |
| `plugins` | One object per admitted plugin | `FC-PLUGINS-CORE` | `FC-PLUGINS-MASTERS`, including authoritative empty |
| `override_chains` | One per admitted chain | identity, contributions, winner | None after structural admission; malformed framing rejects |
| `npc_contributions` | One per admitted contribution, including partial | `FC-NPCCONTRIB-COMMON` | scalars and independent link constructors |
| `race_contributions` | One per admitted contribution, including partial | `FC-RACECONTRIB-COMMON` | `FC-RACECONTRIB-FACEGEN` only after exact decode |
| `placed_reference_contributions` | One per admitted contribution, including partial | `FC-REFRCONTRIB-COMMON` | independent links and atomic placement |
| `allowlisted_fields` | One only for exact observed positive count | whole `FC-ALLOWLISTED-FIELD` | No partial field object |
| `npcs` | One only for a complete resolved winner | whole `FC-NPCS-RESOLVED` | Null/unresolved link values remain inside the complete object |
| `races` | One only for a complete resolved winner | whole `FC-RACES-RESOLVED` | No partial resolved-race object |
| `placed_references` | One only for a complete resolved winner | whole `FC-REFRS-RESOLVED` | Placement may be wholly absent; a partial placement rejects |
| `face_gen` | Exactly one assessment per winning NPC | `FC-FACEGEN-CORE` plus both asset objects | Provider children may be empty; winners may be typed null |
| `taxonomy` | One per authorized tuple | whole `FC-TAXONOMY-TUPLE` | `code` may be typed null only for an authorized nonassigned conclusion |
| `coverage` | Exactly ten fixed-registry objects in every published snapshot | whole `FC-COVERAGE-ROW` | None; zero is explicitly `0/0/completed` |
| `gaps` | One per positive snapshot aggregate key | whole `FC-GAPS-ROW` | Empty means no snapshot gaps |
| `result_gaps` | One per positive result aggregate key | whole `FC-RESULT-GAPS-ROW` | With a snapshot, exact snapshot gaps are repeated here |

Within a present object, every selected constructor group is all-or-nothing
unless the accepted constructor itself defines a repeatable sequence or typed
null. A group cannot be selected with a missing required property or with an
extra property outside its accepted templates.

## Complete semantic-state mapping

All nine accepted state classes map as follows:

- `not-observed`: the affected object or conditional property group is absent;
  no absence fact is inferred.
- `observed-undecodable`: retain independently established structural/observed
  groups; omit decode-dependent groups; emit the exact owning gap and coverage
  incompletion.
- `decoded-null`: retain the object and emit the constructor's typed-null
  property; omission is not equivalent.
- `decoded-unresolved`: retain decoded link properties, canonical target, and
  `state=unresolved`.
- `resolved`: emit every applicable constructor group exactly.
- `semantic-applicable`: emit only facts whose semantic prerequisites are
  established, including exact applicability, taxonomy, coverage, and gaps.
- `unsupported`: retain earlier groups, omit unavailable later groups, emit
  exact unsupported coverage/gap facts, or reject only where the accepted
  atomic boundary requires it.
- `not-applicable`: emit the exact accepted non-applicable semantic fact when
  its family requires one; emit no applicability gap and no inapplicable asset
  denominator.
- `terminal-rejection`: reject the document or snapshot under the exact atomic
  boundary; never publish a weakened snapshot.

This mapping applies to all 77 accepted publication rules, not only the partial
`RACE/DATA` state.

## Exact partial `RACE/DATA` representation

One admitted undecodable `RACE/DATA` contribution has one schema-valid witness
with:

- a `race_contributions` object containing every
  `FC-RACECONTRIB-COMMON` property and no `face_gen_head` property;
- no `allowlisted_fields` object for the structural-only `DATA` occurrence;
- no `races` object for the incomplete winner;
- one taxonomy subject containing exactly the two generic technical tuples;
- `race-records` coverage incremented in denominator but not completion, with
  the exact accepted incomplete state;
- `taxonomy-subjects` coverage incremented and completed;
- one `gaps` and one identical `result_gaps` aggregate for
  `unsupported-shapes:race:data` /
  `allowlisted-record-shape-semantics`, affected count one; and
- no fact sourced from undecoded `DATA`.

The same representation mechanism applies to partial NPC and REFR
contributions, independently absent optional placement, typed-null links,
unknown asset availability, sparse taxonomy, and every other conditional
constructor. No family, signature, fixture, title, mod, race, or pilot identity
changes the mechanism.

## Coverage and gap invariants

- Every published snapshot has exactly the ten fixed coverage objects.
- `0/0/completed` is the only zero-denominator row.
- `0 <= completed <= denominator` for every row.
- `completed` requires equality and no owning row gap.
- A partially completed positive row with an owning gap is
  `completed_with_gaps`.
- A wholly unsupported positive row is `unsupported` with completion zero.
- Later-layer failure never increments completion.
- Gaps use only the eight accepted model rules and their exact population /
  missing-capability pairs.
- Gap counts are positive sums over unique owners. A fact obligation cannot
  create the same owner twice.
- With a snapshot, `gaps` is an exact multiset-equivalent copy in
  `result_gaps`. Without a snapshot, only independently established envelope
  gaps may appear in `result_gaps`.

## Malformed, contradictory, and atomic rejection

Schema rejection covers unknown or missing containers, unknown fields, wrong
primitive types, mismatched `value_type`, malformed path tokens, unsupported
family names, and invalid top-level identities/states.

Canonical rejection covers duplicate object/property/final fact identity,
unknown fact template, family/fact-type mismatch, missing or partial selected
constructor groups, a property without its evidence prerequisites, forbidden
higher-layer facts, noncanonical values, invalid FormKeys/identities, invalid
link components/states, nonfinite/partial placement, invalid asset state,
duplicate taxonomy tuple, missing/extra/contradictory coverage, unknown or
nonpositive gap, snapshot/result-gap mismatch, and any of the eleven accepted
atomic-boundary violations.

A rejection is deterministic and belongs to the accepted rejection class in
the machine model: `schema`, `identity`, `constructor`, `dependency`,
`normalization`, `duplicate`, `coverage`, `gap`, or `atomic`. No rejection may
be converted into a product `FAIL` unless the surrounding scorer has already
completed all ADR-0027 admission gates.

## Candidate and expected symmetry

Candidate and expected `/5` semantic outputs bind the same protocol,
projection, fact vocabulary, canonicalizer, and representation contract. Their
identity metadata differs, but their semantic fact arrays are produced from
the same canonical representation and compared without side-specific defaults.
Expected construction cannot use candidate output. Candidate adaptation cannot
read expected or private data.

## Acceptance and later changes

WP2 must prove every admitted model state has at least one exact witness and
that every invalid or mutated shape rejects or produces the exact different
fact set. WP3 may implement this contract but cannot weaken or silently amend
it. A required semantic choice or change to accepted model `1.2.0` stops for
the owner.
