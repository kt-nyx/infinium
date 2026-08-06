# M1 Slice 4 protocol `/4` evidence-state and fact-dependency contract

Status: proposed

Work ID: `M1/S4.5/PRE-B2/WP1`

Proposed: 2026-08-05

Protocol: `infinium.evaluator-v2/4`

Projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`
Frozen evaluator: `3693d19563c636cd2879804633ca4ce52448d2c1`

## Purpose and authority

This document proposes the public evidence-state and fact-dependency contract
required by the accepted
[Pre-B2 totality plan](../../plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md).
It covers all fifteen active protocol `/4` fact families. Its normative
machine-readable companion is the proposed
[totality model](m1-slice4-protocol-4-totality-model.json), validated by the
[model schema](m1-slice4-protocol-4-totality-model.schema.json).

The accepted semantic authority is supplied by
[ADR-0027](../../architecture/decisions/ADR-0027-public-evaluation-protocol-private-held-out-corpus.md),
[ADR-0028](../../architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md),
[ADR-0029](../../architecture/decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md),
the accepted
[semantic owner disposition](../m1-slice4-semantic-authority-owner-disposition.md),
the accepted
[oracle-authority matrix](../m1-slice4-heldout-oracle-authority-matrix.md),
and the accepted
[Skyrim SE mod-impact taxonomy](../../product/mod-impact-taxonomy.md).
The [data and trust model](../../architecture/data-and-trust-model.md) and
[coverage contract](../../product/severity-confidence-and-coverage.md) govern
evidence separation and coverage meaning.

The frozen evaluator
[protocol](../../../tools/evaluation/Infinium.EvaluatorV2/protocol/protocol.json),
[schema](../../../tools/evaluation/Infinium.EvaluatorV2/protocol/evaluator-v2-common.v4.schema.json),
and
[canonicalizer](../../../tools/evaluation/Infinium.EvaluatorV2/SemanticCanonicalizer.cs)
supply canonical mechanics at the exact evaluator commit above. They do not
supply product semantics. The historical
[oracle-construction draft](m1-slice4-protocol-4-oracle-construction.md) is an
inventory input only where it agrees with accepted authority; it remains a
blocked draft.

This package is proposed, not accepted. WP2 must implement the executable
totality proof, WP3 must derive exercises from the model, and WP4 is
responsible for independent product-blind acceptance. This document does not
authorize private work, candidate inspection, scoring, or protocol `/5`.

## Governing rules

1. A fact publishes only when its own stated prerequisites are satisfied.
2. A failure at a later evidence layer does not erase an independently proven
   earlier fact.
3. Every admitted state and fact obligation has exactly one disposition:
   exact typed value, typed null or accepted unknown, omission plus exact gap,
   or terminal rejection.
4. Null, unknown, absent, undecodable, unresolved, unsupported, and not
   applicable are different claims.
5. A coverage denominator member does not become complete merely because a
   lower-layer fact survives.
6. Gaps name the exact affected population and missing capability; they never
   substitute generated IDs or incidental reason prose.
7. Atomic publication invariants are not weakened to preserve partial output.
8. Candidate behavior, product output, private data, record names, and fixture
   identities cannot choose a rule or fill an omitted value.
9. No family inherits an unstated default. The model explicitly partitions
   its admissible and inadmissible common state classes and marks
   `unstated_default` as `prohibited`.

## Evidence layers

The layers are prerequisites within one derivation path, not a global ranking
of evidence classes.

| Layer | What it establishes | Required prior layers | It does not establish |
|---|---|---|---|
| `structural` | Envelope, signature, identity, order, declared shape, contribution membership, raw flags, deletion, and compression | none | exact member occurrence, typed value, resolution, or semantic meaning |
| `observed` | Exact occurrence, bytes, presence, positive count, or declared provider chain from the admitted source | structural | typed meaning, target existence, semantic absence, or taxonomy meaning |
| `decoded` | Typed scalar, typed null, link token/ordinal, or complete finite placement under an accepted shape contract | structural and observed | target/provider resolution, semantic applicability, or exact absence without exhaustive authority |
| `resolved` | Effective winner, translated target state, provider winner, localization result, or explicit unresolved target | structural, observed, and decoded | FaceGen applicability, purpose, consequence, or unrelated taxonomy meaning |
| `semantic` | Applicability, semantic availability, evidence-supported classification, and coverage completion | all earlier layers required by that claim | unsupported higher-layer claims, global safety, or candidate correctness |

The minimum layer is per fact constructor. For example, an override
contribution's source plugin is structural; an allowlisted occurrence count is
observed; `face_gen_head` is decoded; a link target state is resolved; and
FaceGen applicability is semantic.

## Common state classes

| State class | Exact meaning | Required disposition |
|---|---|---|
| `not-observed` | The member or subject is outside observed admitted evidence; absence is not inferred without exhaustive authority | no fact and no denominator participation unless another rule explicitly defines an exhaustive absence population |
| `observed-undecodable` | Structure or occurrence is proven, but the accepted shape cannot produce the typed value | retain satisfied lower-layer facts, omit dependent higher facts, increment the applicable denominator without completion, and emit the exact gap |
| `decoded-null` | The accepted decoder establishes semantic null | publish typed null only at constructors whose contract exposes null; do not substitute missing or unresolved |
| `decoded-unresolved` | A target FormKey decodes and translates, but no authoritative target record resolves | publish the complete link object with state `unresolved` and canonical target |
| `resolved` | Every prerequisite for the fact is established | publish the exact typed value |
| `semantic-applicable` | Accepted semantic prerequisites establish applicability | publish the family-specific applicable state and enter its coverage populations |
| `unsupported` | The input is well framed but the named bounded capability is unavailable | retain valid lower facts, omit unsupported higher facts, and emit the exact gap; use `unsupported` coverage only for a wholly unsupported nonzero population |
| `not-applicable` | Accepted semantics establish that the claim does not apply | publish an explicit not-applicable value only when required by the family contract; produce no applicability gap and no inapplicable denominator member |
| `terminal-rejection` | Lower evidence cannot be separated safely or an atomic invariant is violated | reject the owning publication; never repair, guess, or tie-break |

### Required distinctions

- `not-observed` says no qualifying observation entered this derivation.
- `observed-undecodable` says an observation exists but cannot be decoded.
- `decoded-null` is an explicit typed result.
- `decoded-unresolved` has a canonical non-null target that fails target
  resolution.
- `resolved` has the required target/provider/dependency authority.
- `semantic-applicable` authorizes the semantic population.
- `unsupported` identifies a missing capability.
- `not-applicable` establishes that the semantic population does not apply.

No transition among these states is implicit.

## Common canonical vocabulary and normalization

The exact fact families are:

```text
result
plugins
override_chains
npc_contributions
race_contributions
placed_reference_contributions
allowlisted_fields
npcs
races
placed_references
face_gen
taxonomy
coverage
gaps
result_gaps
```

The frozen schema admits fact types `state`, `plugin`, `winner`,
`override_chain`, `contribution`, `record_identity`, `form_key`, `link`,
`ownership`, `placement`, `field`, `npc`, `race`, `reference`, `face_gen`,
`taxonomy`, `coverage`, `gap`, and `failure`. The canonicalizer's actual
constructors are inventoried below and in every `FC-*` constructor group in
the model. Value types are exactly `string`, `integer`, `number`, `boolean`,
and `null`.

Common rules use stable IDs:

- `P4-NORM-FORMKEY`: `xxxxxxxx:plugin.ext`, with eight lowercase hexadecimal
  local-ID digits and lowercase `esm`, `esp`, or `esl` filename. TES4 master
  translation and light/full origin must be unambiguous; `.esl` extension
  alone never proves light origin.
- `P4-NORM-SEGMENT`: normalize the semantic value first, then RFC 3986
  percent-encode its UTF-8 bytes with uppercase escape hex and `%20` for
  space.
- `P4-NORM-ORDER`: preserve manifest, master, contribution, provider, and link
  occurrence sequences; format zero-based ordinals at minimum width four;
  sort semantic sets and final fact IDs ordinally.
- `P4-NORM-CASE`: plugin/provider IDs, paths, identity signatures, and gap
  signature/field components are lowercase; Bethesda signature fact values
  remain uppercase.
- `P4-NORM-NUMBER`: `integer` is exact signed `Int64`; `number` is finite
  IEEE-754 and compares numerically, including negative zero equal to zero.
- `P4-NORM-CONTRIBUTION`: semantic contribution identity is
  `source={plugin}|order={D4}|record={FormKey}|signature={lower}|flags={hex}|deleted={bool}|compressed={bool}`.
- `P4-NORM-LINK`: link identity is
  `{field_lower}:{component_lower_or_value}:{ordinal_D4}`.
- `P4-NORM-PATH`: FaceGen paths replace backslash with slash and lowercase
  invariantly before subject construction or escaping.
- `P4-NORM-TAXONOMY`: taxonomy identity includes subject, subject type, axis,
  facet, code-or-literal-`null`, applicability, and role.
- `P4-NORM-GAP`: aggregate by exact
  `(population, missing_capability)` and sum positive independently identified
  affected-member counts.

Duplicate final fact IDs are invalid. Explicit null is a fact; missing is no
fact. Empty sequences emit no child fact. An observed occurrence count must be
positive; zero produces no `allowlisted_fields` fact.

## Link, asset, taxonomy, and result vocabularies

Link states are exactly `null`, `resolved`, and `unresolved`. Link fields are
`TPLT`, `RNAM`, `HCLF`, `PKID`, `PNAM`, `NAME`, `XLKR`, `XLRL`, and `XOWN`.
Only paired `XLKR` links use components `linked-reference` and `keyword`;
other link components are typed null. A null singleton emits only typed-null
`/state`. A present link object emits field, component, ordinal, target, and
state. An untranslatable master index is invalid input, not `unresolved`.

FaceGen applicability is exactly:

```text
applicable
not_applicable_deleted_winner
unknown_template_traits_decision
not_applicable_template_traits
unknown_race
not_applicable_race_without_face_gen_head
```

Loose availability has exactly three semantic states:

| Semantic state | `present` | `exact_absence_known` | Winner |
|---|---:|---:|---|
| `present` | `true` | `false` | exact winner from declared chain |
| `absent` | `false` | `true` | typed null |
| `unknown` | `false` | `false` | typed null |

`true/true` is invalid. Archive state is a separate coverage capability and
never changes loose availability.

Taxonomy applicability is exactly `assigned`, `unknown`, `unsupported`,
`unmapped`, or `not-applicable`; roles are `declared`, `observed`,
`predicted`, or `established`. `assigned` requires a non-null code. Every
other applicability uses typed-null `/code`. The nine canonical axis/facet
pairs and the complete code registries remain those in the accepted taxonomy.
The protocol's M1 subject types are `record-contribution`,
`record-semantic-subject`, and `unsupported-record`; the mechanically parseable
legacy `provider-topology` subject is unpublished for new expected output.

The closed semantic suffixes are:

```text
area.actors.ai-packages
area.actors.appearance-identity
area.world.placed-objects-activation
face-gen-loose-provider-chain:{normalized-relative-path}
```

Top-level result states are `completed`, `completed_with_gaps`,
`invalid_input`, `changed_during_read`, and `failed`. Coverage states are
`completed`, `completed_with_gaps`, `failed`, `skipped_by_configuration`,
`skipped_by_limit`, and `unsupported`.

## Fact-family dependencies and dispositions

Every `FC-*` name below is a constructor group in the machine model. Its
`P4-*` rules state all admissible publications. A family has no behavior
outside its listed rules.

### `result`

`FC-RESULT-STATE` always constructs `result/snapshot_present` and
`result/failure_present` as boolean `state` facts. `P4-RESULT-PUBLISHED`
publishes both with `snapshot_present=true`; `P4-RESULT-NO-SNAPSHOT` publishes
both with `snapshot_present=false`. Exact failure codes/count/prose are not
projected. A no-snapshot result emits no snapshot family.

Atomic boundary: `AB-RESULT`.

### `plugins`

For plugin ordinal `i`, `FC-PLUGINS-CORE` constructs:

```text
plugins/{i}/plugin_name
plugins/{i}/load_order
plugins/{i}/provider_id
plugins/{i}/master_style
```

`FC-PLUGINS-MASTERS` constructs
`plugins/{i}/masters/{j}` in TES4 order. `P4-PLUGINS-ADMITTED` publishes exact
manifest/header facts and completes `plugins`. `P4-PLUGINS-NO-MASTERS`
publishes core facts and no master children. A non-manifest plugin creates no
fact or denominator member. Duplicate identity, malformed TES4 framing,
ambiguous origin, missing master, or untranslatable FormKey is terminal under
`AB-FORMKEY`.

### `override_chains`

`FC-OVERRIDE-IDENTITY` constructs signature, FormKey, origin plugin, and
origin local ID. `FC-OVERRIDE-CONTRIBUTIONS` constructs, for every ordered
contribution, identity plus source plugin, load order, deleted, compressed,
and raw flags. `FC-OVERRIDE-WINNER` constructs the same effective winner
fields except origin-local identity.

`P4-OVERRIDE-PARTIAL` and `P4-OVERRIDE-UNSUPPORTED-FAMILY` retain these
structural/effective-order facts when later semantic decode is unsupported.
`P4-OVERRIDE-RESOLVED` publishes the same complete chain for a decoded record.
Malformed record framing is terminal under `AB-RECORD-FRAMING`.

### `npc_contributions`

`FC-NPCCONTRIB-COMMON` constructs the common contribution identity/flags and
literal `kind=npc`. `FC-NPCCONTRIB-SCALARS` constructs configuration flags,
template flags, `uses_template`, `templates_traits`, and AIDT presence only.
`FC-NPCCONTRIB-LINKS` constructs singleton TPLT/RNAM/HCLF and repeatable
PKID/PNAM links.

- `P4-NPCCONTRIB-UNDECODABLE` retains common facts, omits constructors that
  depend on the unsupported shape, counts the admitted NPC in the
  `npc-records` denominator but not completion, and emits the exact shape gap.
- `P4-NPCCONTRIB-NULL-LINK` publishes typed-null singleton `/state` only.
- `P4-NPCCONTRIB-UNRESOLVED-LINK` publishes all five link facts with
  `unresolved` and the canonical target.
- `P4-NPCCONTRIB-RESOLVED` publishes the complete bounded contribution and
  increments both denominator and completion.
- An independently observed unsupported field uses
  `P4-NPCCONTRIB-UNSUPPORTED-FIELD`; unrelated supported constructors retain
  their own dispositions.

Invalid link construction or malformed framing is terminal, while a valid
null or unresolved link is completed work. The complete contribution rule
owns the single coverage increment; per-link null/unresolved rules never add a
second record count.

### `race_contributions`

`FC-RACECONTRIB-COMMON` constructs the common contribution and literal
`kind=race`. `FC-RACECONTRIB-FACEGEN` constructs only
`/face_gen_head` as the exact decoded boolean.

`P4-RACECONTRIB-PARTIAL-DATA` is the accepted partial `RACE/DATA` rule:
common facts publish; `face_gen_head` does not; `race-records` denominator
increments and completion does not; the exact unsupported-shape gap is
required. A complete bounded decode publishes `true` or `false` under
`P4-RACECONTRIB-DECODED`. No likely default is permitted.

### `placed_reference_contributions`

`FC-REFRCONTRIB-COMMON` constructs the common contribution and literal
`kind=reference`. `FC-REFRCONTRIB-LINKS` constructs NAME, paired XLKR, XLRL,
and XOWN link/ownership facts. `FC-REFRCONTRIB-PLACEMENT` constructs all six
finite position/rotation numbers.

Null singletons publish typed-null `/state`; missing placement publishes no
placement facts; unresolved links publish canonical targets and
`unresolved`. An unsupported field/shape retains common facts and makes the
admitted contribution incomplete with the exact gap. Partial/nonfinite
placement is terminal under `AB-PLACEMENT`. As with NPC links, the complete
contribution rule owns the one record-level coverage increment.

### `allowlisted_fields`

For one semantic contribution and field,
`FC-ALLOWLISTED-FIELD` constructs uppercase `/field` and positive exact
`/count`. The closed sets are:

- `NPC_`: `EDID`, `ACBS`, `TPLT`, `RNAM`, `AIDT`, `PKID`, `PNAM`, `HCLF`;
- `RACE`: `EDID`, `DATA`;
- `REFR`: `EDID`, `NAME`, `XLKR`, `XLRL`, `XOWN`, `DATA`.

Not observed produces no fact. Structural presence without independently
observed count produces no fact. When shape support is missing, the enclosing
contribution-family rule owns the exact shape gap; the field rule does not add
a second affected-member count. Unsupported fields produce only the exact
unsupported-field gap, owned by the enclosing contribution-family rule rather
than duplicated by this projection family.
`EDID` is identifying metadata only and never creates purpose, area,
consequence, intent, finding, or taxonomy meaning.

### `npcs`, `races`, and `placed_references`

Each resolved family is rooted by canonical FormKey and reproduces the
winning contribution's applicable semantic body:

- `FC-NPCS-RESOLVED` uses the NPC scalar/link/null/unresolved constructors;
- `FC-RACES-RESOLVED` requires the complete bounded race decode including
  `face_gen_head`;
- `FC-REFRS-RESOLVED` uses the REFR link/null/placement constructors.

If a required winning body is undecodable, no complete resolved map fact
publishes. Lower override/contribution facts and their existing coverage/gap
effects survive. A valid null or unresolved link remains an exact resolved-map
state. Ambiguous winners and invalid atomic bodies are terminal.

### `face_gen`

There is exactly one assessment per winning NPC. `FC-FACEGEN-CORE` constructs
NPC FormKey, exact applicability, origin plugin, and origin local ID.
`FC-FACEGEN-ASSET` constructs, independently for mesh and tint, normalized
path, ordered provider IDs, winner provider ID or typed null, `present`, and
`exact_absence_known`.

Apply precedence exactly:

1. deleted winner: `not_applicable_deleted_winner`, no applicability gap;
2. unknown template-traits decision:
   `unknown_template_traits_decision` plus `P4-GAP-TEMPLATE`;
3. definite trait inheritance: `not_applicable_template_traits`, no gap;
4. missing, null, unresolved race, or unknown `FaceGenHead` decision:
   `unknown_race` plus `P4-GAP-RACE`;
5. resolved race without `FaceGenHead`:
   `not_applicable_race_without_face_gen_head`, no gap;
6. otherwise: `applicable`.

Non-trait template use does not suppress the NPC's own assessment. For
non-applicable or applicability-unknown NPCs, protocol framing still emits the
two canonical paths, empty provider sequences, typed-null winners, and
false/false availability; those paths do not enter asset denominators.

For an applicable path:

- present loose chain: increment `face-gen-loose-assets` denominator and
  completion once;
- exact loose absence: increment and complete loose coverage; also enter
  `face-gen-archive-assets`, completing it only when archive authority
  resolves;
- unknown loose availability: increment but do not complete loose coverage;
  enter archive coverage when a decision is required; unresolved archive
  authority emits `P4-GAP-ARCHIVE`.

Mesh and tint apply these rules independently. `AB-ASSET` rejects a fourth
availability state, an inconsistent winner, or a missing/duplicate winning-NPC
assessment.

### `taxonomy`

`FC-TAXONOMY-TUPLE` constructs taxonomy ID, canonical subject, subject type,
axis, facet, applicability, role, version `0.1.0`, and code or typed null.

Every admitted plugin-record contribution emits exactly the two technical
core assignments on its `record-contribution` subject:

```text
technical-modification-surface / semantic-mechanism
  surface.plugin-data / assigned / observed
technical-modification-surface / realization-and-delivery
  delivery.plugin-container / assigned / observed
```

`P4-TAXONOMY-PARTIAL-RACE` applies this core even when `RACE/DATA` is
undecodable. The technical subject enters and completes the
`taxonomy-subjects` population independently of `race-records` completion.

Sparse semantic subjects are emitted only from decoded semantic/provider
evidence:

- NPC PKID/AIDT supports `area.actors.ai-packages`;
- NPC RNAM/PNAM/HCLF supports `area.actors.appearance-identity`;
- REFR NAME/XLKR/XLRL/XOWN/placement supports
  `area.world.placed-objects-activation`;
- every distinct declared FaceGen provider chain, including a single-provider
  chain, supports `surface.asset` and `delivery.loose-data-file`.

An unsupported record gets an `unsupported-record` technical subject plus the
two technical core assignments; its limitation remains in `gaps`. Purpose,
consequence, extent, or additional area tuples require independent applicable
evidence. Record signature, filename, and `EDID` are insufficient. Explicit
unknown/unsupported/unmapped/not-applicable tuples use typed-null code only
when a public subject contract requires the real conclusion. There is no null
matrix fill. `AB-TAXONOMY` rejects duplicate tuples, invalid subject/suffix,
legacy provider topology, filler tuples, and code/applicability mismatch.

### `coverage`

`FC-COVERAGE-ROW` constructs population, denominator, completed, and state for
all ten fixed rows. All ten publish in every snapshot, including zero rows.
`P4-COVERAGE-ZERO` requires `0/0/completed`. Plain `completed` requires exact
completion and no attached row gap. A partial row uses
`completed_with_gaps` when some supported work completed and `unsupported`
when its entire nonzero population lacks the required capability. `failed`,
`skipped_by_configuration`, and `skipped_by_limit` require the actual
lifecycle state. `AB-COVERAGE` rejects missing, extra, duplicate, or
arithmetically inconsistent rows.

### `gaps` and `result_gaps`

`FC-GAPS-ROW` and `FC-RESULT-GAPS-ROW` each construct population, positive
affected denominator, and missing capability at roots derived from the exact
pair. Empty sets emit no facts. With a snapshot, every snapshot gap is also
carried in `result_gaps`; this repeats visibility, not coverage ownership.
Without a snapshot, `result_gaps` contains only independently established
envelope gaps and never derives exact gaps from excluded failure prose.

`AB-GAP` rejects unknown pairs, nonpositive counts, and duplicate aggregate
keys.

## Coverage registry

| Population | Denominator | Completed | Important partial effect |
|---|---|---|---|
| `plugins` | manifest plugins | admitted ordered plugins | identity/framing failures are normally terminal |
| `npc-records` | admitted `NPC_` contributions | bounded decoded contributions | unsupported required decode is denominator-only |
| `race-records` | admitted `RACE` contributions | bounded decoded contributions | partial `RACE/DATA` is denominator-only |
| `placed-reference-records` | admitted `REFR` contributions | bounded decoded contributions | unsupported required decode is denominator-only |
| `unsupported-records` | outside-allowlist contributions | conclusively classified unsupported contributions | exact unsupported-record gap remains visible |
| `face-gen-loose-assets` | one per applicable mesh/tint path | exact present or exact absent paths | unknown never completes |
| `face-gen-archive-assets` | applicable no-loose-winner paths requiring archive decision | archive-resolved paths | unavailable authority produces archive gap |
| `localized-strings` | encountered localized values requiring resolution | resolved values | unresolved value produces localized gap |
| `automatic-environment-discovery` | one if requested, otherwise zero | one if requested discovery completes | requested unavailable is incomplete with gap |
| `taxonomy-subjects` | required contribution, semantic, unsupported, and FaceGen-chain subjects | subjects with all required meaningful assignments | partial race technical subject completes independently |

Every zero denominator is `0/0/completed`. Unlike populations are not merged.
Later-layer failure never increments higher-layer completion.

## Exact gap population and capability rules

| Rule | Condition | Population | Missing capability |
|---|---|---|---|
| `P4-GAP-UNSUPPORTED-RECORD` | unsupported signature | `unsupported-records:{signature_lower}` | `allowlisted-record-family-semantics` |
| `P4-GAP-UNSUPPORTED-FIELD` | unsupported field | `unsupported-fields:{signature_lower}:{field_lower}` | `allowlisted-record-field-semantics` |
| `P4-GAP-UNSUPPORTED-SHAPE` | allowlisted field with unsupported shape | `unsupported-shapes:{signature_lower}:{field_lower}` | `allowlisted-record-shape-semantics` |
| `P4-GAP-LOCALIZED` | unresolved localized value | `localized-strings` | `localized-string-resolution` |
| `P4-GAP-ARCHIVE` | unresolved required archive decision | `face-gen-archive-assets` | `archive-activation-and-member-precedence` |
| `P4-GAP-DISCOVERY` | requested discovery unavailable | `automatic-environment-discovery` | `automatic-environment-discovery` |
| `P4-GAP-TEMPLATE` | unknown template-traits decision | `face-gen-applicability:template` | `complete-template-traits-decision` |
| `P4-GAP-RACE` | missing/null/unresolved/unknown race decision | `face-gen-applicability:race` | `resolved-winning-race` |

Signatures and fields in projected populations are lowercase. Identical pairs
aggregate exact affected-member counts. A fact family may reference a gap
already emitted by its owning contribution rule only as “do not double
count”; gap ownership remains single.

## Atomic rejection boundaries

| Boundary | Atomic rejection |
|---|---|
| `AB-RESULT` | invalid snapshot/state combination or missing required result boolean |
| `AB-FORMKEY` | missing/ambiguous master translation, invalid full/light local ID, or noncanonical FormKey |
| `AB-RECORD-FRAMING` | bytes cannot establish safe envelope, signature, order, contribution membership, or flags |
| `AB-LINK` | invalid field/component/state/target/ordinal combination or untranslatable master index |
| `AB-PLACEMENT` | partial or nonfinite six-value placement |
| `AB-ASSET` | invalid availability pair, missing/extraneous winner, winner outside chain, or missing/duplicate assessment |
| `AB-COVERAGE` | registry membership, uniqueness, arithmetic, zero-row, state, or gap inconsistency |
| `AB-GAP` | unknown gap pair, duplicate pair, or nonpositive affected count |
| `AB-TAXONOMY` | invalid subject/suffix/code/applicability, filler tuple, provider-topology tuple, or duplicate semantic identity |
| `AB-TYPED-VALUE` | mismatched JSON kind/value type, inexact Int64, nonfinite number, object, or array |
| `AB-FACTSET` | duplicate final fact ID or unlisted constructor/vocabulary |

Graceful degradation applies only where the earlier fact remains independently
true and the model names the exact missing higher capability. These boundaries
are not converted into gaps merely to preserve a snapshot.

## Accepted partial `RACE/DATA` disposition

For one admitted `RACE` contribution with structurally present but unsupported
`DATA` shape:

1. `P4-OVERRIDE-PARTIAL` emits the chain identity, all ordered common
   contribution facts, and the structural/effective winner.
2. `P4-RACECONTRIB-PARTIAL-DATA` emits the race contribution's common facts
   and `kind=race` but omits `face_gen_head`.
3. `P4-FIELDS-STRUCTURAL-ONLY` emits no `DATA` allowlisted-field fact unless
   an exact occurrence count was independently observed. Structural presence
   alone never becomes count `1`.
4. `P4-RACES-PARTIAL-DATA` emits no complete resolved race.
5. `P4-TAXONOMY-PARTIAL-RACE` creates the `record-contribution` subject and
   emits both mandatory generic technical assignments. It emits no
   `DATA`-dependent semantic tuple.
6. `race-records` denominator increments by one and completion does not.
7. `taxonomy-subjects` denominator and completion each increment by one when
   the two required technical assignments emit.
8. One affected member is aggregated at
   `unsupported-shapes:race:data` with missing capability
   `allowlisted-record-shape-semantics` in both `gaps` and `result_gaps` when
   a snapshot publishes. `P4-RACECONTRIB-PARTIAL-DATA` exclusively owns this
   aggregate; the field and resolved-race rules do not add affected counts.

This rule is category-neutral partial-decode behavior, not a fixture or record
identity exception.

## Manual evidence-layer traces

### Structural to observed

A well-framed record header proves contribution identity, flags, order, and
declared member shape. It does not prove an occurrence count. Therefore a
structurally present `RACE/DATA` member without independently observed count
retains override/common facts and emits no `allowlisted_fields` count.

### Observed to decoded

An independently observed `RACE/DATA` member with unsupported shape proves
presence but no `face_gen_head`. The higher fact is omitted, the exact shape
gap is emitted, and `race-records` does not complete for that contribution.

### Decoded to resolved

A decoded and translatable NPC `RNAM` target with no target record publishes a
complete link with state `unresolved`. It is neither null nor resolved. An
untranslatable master index instead crosses `AB-FORMKEY`/`AB-LINK` and is
terminal.

### Resolved to semantic

A non-deleted NPC with a complete non-trait-template decision and resolved
race whose `FaceGenHead` decision is true becomes `applicable`. Provider and
asset state are then assessed independently for mesh and tint. Without those
semantic prerequisites, no asset denominator member is created.

### Atomic rejection

`present=true` plus `exact_absence_known=true` is not a fourth state. It is
rejected under `AB-ASSET`; no rule may choose the nearer valid state.

The model records these as `TRACE-*` entries bound to the exact `P4-*` rules.

## Totality and downstream boundary

The schema pins the five evidence layers, nine common state classes, fifteen
fact-family order, and ten coverage-population order. The model inventories
every frozen fact constructor, vocabulary, dependency dimension, rule,
coverage effect, gap effect, and atomic boundary. Each family explicitly
partitions common state classes and prohibits an unstated default.

WP1 does not claim the Cartesian state space has been mechanically proven.
That executable completeness, exclusivity, dependency, and consistency proof
belongs to WP2. Generated fixtures and mutation coverage belong to WP3, and
independent acceptance belongs to WP4. Candidate conformance belongs to WP5.
No later package is started or implied by this proposal.
