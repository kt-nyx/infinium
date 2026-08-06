# M1 Slice 4 protocol `/4` oracle-construction specification

Status: Blocked draft; not accepted as oracle authority

Attempted: 2026-08-05

Owner: Project owner

Evaluator commit: `3693d19563c636cd2879804633ca4ce52448d2c1`

Protocol: `infinium.evaluator-v2/4`

Scorer and adapter: `4.0.0`

Projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`

Product taxonomy: `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`

## 1. Purpose and authority

This draft attempts to define the normative product-independent construction contract for every
active semantic fact in evaluator-v2 protocol `/4`. A fresh oracle author uses
the accepted public semantic rules, the answer-free execution manifest, and
retained input bytes. The author does not need product source, product output,
Mutagen output, a candidate-shaped product snapshot, or any product-generated
identifier.

The required independent authorability re-review found a second material
authority gap after the single permitted correction pass: the cross-family
projection of an admitted but undecodable record shape is not uniquely
specified. Therefore this draft is not an accepted authority, and its rules
must not be used to authorize a private oracle or candidate comparison. See
the [public review attestation](../m1-slice4-protocol-4-oracle-authorability-review.md).

ADR-0029 and the semantic owner disposition subsequently resolve that specific
partial-decode choice. This draft nevertheless remains blocked historical
evidence: the accepted
[Pre-B2 totality plan](../../plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md)
requires a successor evidence contract and machine-checkable state model to
prove all fifteen families complete before any authority is accepted.

The shortened taxonomy label `infinium.mod-impact-taxonomy/0.1.0` in the slice
plan resolves here to the canonical accepted product identity above. The
canonical identity from the accepted taxonomy and evaluation baseline controls
the literal `taxonomy_id` fact.

The governing semantic decisions are ADR-0028, the semantic-authority owner
disposition, the final held-out scope amendment, and the final oracle-authority
matrix. The frozen evaluator supplies canonical mechanics only. If a retained
byte cannot be decoded under the positive M1 allowlist, the oracle records the
specified gap or terminal boundary; it never borrows a product interpretation.

The fifteen active families are:

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

Every fact is a tuple of `fact_id`, `fact_type`, `value_type`, and `value`.
Facts are finally sorted by ordinal `fact_id`. Duplicate final fact IDs are
invalid; they are never resolved by last-write, first-write, or another
tie-break.

## 2. Common lexical and typed-value contract

### 2.1 Strings, casing, and signatures

- Comparisons are ordinal and case-sensitive after the normalization stated
  here.
- Plugin filenames and local installed entity/provider IDs are lowercase.
- Bethesda record and subrecord signatures are literal uppercase ASCII.
- The admitted record signatures are `NPC_`, `RACE`, and `REFR`.
- The admitted link field tokens are `TPLT`, `RNAM`, `HCLF`, `PKID`, `PNAM`,
  `NAME`, `XLKR`, `XLRL`, and `XOWN`.
- The only non-null component tokens are `linked-reference` and `keyword`, and
  both are restricted to `XLKR`.
- FaceGen relative paths replace `\` with `/` and then lowercase invariantly.
- No published string is trimmed or Unicode-normalized implicitly. An input
  requiring such repair is invalid at input admission.

### 2.2 Canonical FormKeys and full/light origin

A canonical FormKey is:

```text
xxxxxxxx:plugin.ext
```

`xxxxxxxx` is exactly eight lowercase hexadecimal digits. `plugin.ext` is a
lowercase filename with a nonempty basename and extension `esm`, `esp`, or
`esl`; `/`, `\`, and `:` are forbidden in the filename.

The local ID is translated through the source plugin's ordered TES4 master
list before formatting. A regular/full origin uses the ordinary 24-bit local
FormID namespace and publishes master style `full`. A light origin uses the
TES4 ESL flag and compact light namespace and publishes master style `light`.
The filename extension alone does not make an origin light: a `.esl` without
the required TES4 light flag is invalid input, while an ESL-flagged `.esp` is
`light`. The canonical eight-digit local-ID field retains leading zeroes.

Missing masters, an untranslatable master index, an out-of-range full/light
local ID, or ambiguous origin is invalid input. It does not create a guessed
FormKey.

### 2.3 URI path segments

Whenever a template below says `{seg(value)}`, encode the UTF-8 bytes of the
already-normalized value using RFC 3986 percent encoding equivalent to .NET
`Uri.EscapeDataString`:

- leave only ASCII letters, digits, `-`, `.`, `_`, and `~` unescaped;
- encode every other byte as `%HH` using uppercase hexadecimal;
- encode space as `%20`, never `+`; and
- encode `/`, `:`, `|`, `=`, and `%` as data, not delimiters.

Percent escaping never performs case, slash, FormKey, or semantic
normalization. Those transformations occur before escaping.

### 2.4 Sequence and set ordering

- Manifest plugin order, TES4 masters, override contributions, provider
  chains, and ordinal link occurrences are sequences. Preserve their semantic
  order.
- A sequence position is zero-based and formatted as four decimal digits:
  `0000`, `0001`, and so on. More than 10,000 entries remains representable;
  formatting has a minimum width of four and does not truncate.
- Record maps sort by canonical FormKey using ordinal comparison.
- Contribution sets sort by evaluator-owned semantic contribution identity.
- Link sets sort by link identity.
- Taxonomy semantic sets sort by the complete taxonomy identity.
- Coverage rows sort by population. Gap rows sort by population then missing
  capability.
- Final facts always sort by complete `fact_id`, regardless of family
  construction order.

Duplicate plugin names, plugin load orders, provider IDs within one chain,
provider-chain paths, record-map identities, semantic taxonomy tuples, gap
aggregation keys, or fact IDs are invalid at their owning boundary.

### 2.5 Typed primitive values and numbers

Allowed `value_type` values are `string`, `integer`, `number`, `boolean`, and
`null`; arrays and objects are never fact values.

- `integer` is an exact signed Int64. Integral decimal or exponent JSON tokens
  are accepted only when exactly representable as Int64.
- `number` is any finite IEEE-754 value. NaN and infinities are invalid.
- Semantic number comparison is numeric: `10`, `10.0`, and `1e1` are equal;
  negative zero equals zero.
- `boolean`, `string`, and `null` require the matching JSON kind.
- `null` facts use `value_type: null` and JSON `null`.

### 2.6 Explicit null versus missing facts

An explicit null is a fact. A missing fact is no fact with that ID. They are
never equivalent.

- A singleton link property represented as JSON null emits only its `/state`
  fact as typed null. Its field, component, ordinal, and target facts are
  missing.
- A present link object emits all five link facts. Its component may be typed
  null. Its target is typed null only for state `null`.
- A missing placement emits no placement component facts.
- A taxonomy tuple with no code emits a typed-null `/code` fact and uses the
  literal identity token `null` in the fact-ID root.
- An empty sequence emits no child facts. No length fact is implied.
- A field with observed count zero is missing from `allowlisted_fields`.

An expected fact omitted by the candidate is `missing_fact`; an unlisted
candidate fact is `extra_fact`.

### 2.7 Evaluator-owned semantic identities

For each decoded record contribution construct:

```text
source={source_plugin_lower}
|order={load_order_D4}
|record={canonical_form_key}
|signature={signature_lower}
|flags={raw_flags_as_8_or_more_lower_hex_digits}
|deleted={true_or_false}
|compressed={true_or_false}
```

The lines above are one continuous string without newlines. Hex formatting has
a minimum width of eight, is lowercase, and is not truncated for a larger
nonnegative value. `raw_flags` must fit the accepted nonnegative signed Int64
transport.

Evaluator-owned taxonomy subjects are:

- `record-contribution`: exactly the semantic contribution identity;
- `record-semantic-subject`:
  `{semantic_contribution_identity}|semantic={suffix_lower}`; and
- `unsupported-record`:
  `unsupported-record|source={source_plugin_lower}|signature={signature_lower}|record={canonical_form_key}`.

The closed M1 semantic suffix registry is:

```text
area.actors.ai-packages
area.actors.appearance-identity
area.world.placed-objects-activation
face-gen-loose-provider-chain:{normalized-relative-path}
```

The FaceGen suffix is created once for each distinct declared mesh or tint
loose-provider chain, including one-provider chains. A plugin list alone does
not create a provider subject. Frozen `/4` can mechanically parse the legacy
`provider-topology` subject type, but ADR-0028 makes it unpublished and invalid
for a new M1 expected oracle.

Product contribution, participant, winner, assignment, analyzer, evidence,
gap, coverage-gap, snapshot, and source-snapshot IDs are excluded.

### 2.8 Links

The exact link-state strings are `null`, `resolved`, and `unresolved`.
`unspecified`, any enum default, and any other state are invalid.

Link identity is:

```text
{field_lower}:{component_lower_or_value}:{ordinal_D4}
```

where a null component uses the literal `value`. The identity is escaped as a
single segment in repeatable-link roots.

Singleton fields are `TPLT`, `RNAM`, `HCLF`, `NAME`, `XLRL`, and `XOWN` and
use ordinal zero. Repeatable `PKID` and `PNAM` values increment independently
from zero in byte order. Each paired `XLKR` occurrence produces two link
objects with the same occurrence ordinal: component `linked-reference` for the
reference FormKey and component `keyword` for the keyword FormKey.

For a present link object:

- state `null` requires a null target;
- state `resolved` requires a canonical target FormKey whose record resolves;
- state `unresolved` requires a canonical target FormKey whose origin can be
  translated but whose target record does not resolve;
- non-`XLKR` component is typed null; and
- `XLKR` component is one of the two tokens above.

Zero/null FormID is state `null`. Failure to translate the target's master
index is invalid input, not `unresolved`.

### 2.9 Exact published vocabularies

FaceGen applicability:

```text
applicable
not_applicable_deleted_winner
unknown_template_traits_decision
not_applicable_template_traits
unknown_race
not_applicable_race_without_face_gen_head
```

Loose-asset semantic availability is `present`, `absent`, or `unknown` and is
transported as:

| Availability | `present` | `exact_absence_known` | Winner |
|---|---:|---:|---|
| `present` | `true` | `false` | required |
| `absent` | `false` | `true` | null |
| `unknown` | `false` | `false` | null |

`true/true`, present without a winner, non-present with a winner, or a winner
outside the declared chain is invalid. No `unspecified` asset state is
published.

Coverage states admitted by the bounded backend contract are:

```text
completed
completed_with_gaps
failed
skipped_by_configuration
skipped_by_limit
unsupported
```

A successfully attempted fixed-registry row uses `completed` when completed
equals denominator and no row gap exists; otherwise it uses
`completed_with_gaps` when some supported work completed, or `unsupported`
when the entire nonzero population lacks the required capability. `failed`,
`skipped_by_configuration`, and `skipped_by_limit` remain admitted only when
that lifecycle condition is actually established. A zero denominator is
always `completed` with completed count zero.

Taxonomy applicability strings are `assigned`, `unknown`, `unsupported`,
`unmapped`, and `not-applicable`. Classification-role strings are `declared`,
`observed`, `predicted`, and `established`. `assigned` requires a non-null
code; every other applicability uses a null code. Role records the evidence
role and is not inferred from applicability.

The canonical taxonomy axis/facet pairs are:

| Axis | Facet |
|---|---|
| `declared-purpose-and-intended-feature-area` | `purpose-kind` |
| `technical-modification-surface` | `semantic-mechanism` |
| `technical-modification-surface` | `realization-and-delivery` |
| `affected-game-system-or-content-area` | `affected-area` |
| `consequence-type` | `consequence-type` |
| `effect-extent` | `direct-subject-breadth` |
| `effect-extent` | `spatial-breadth` |
| `effect-extent` | `persistence-and-lifecycle-breadth` |
| `effect-extent` | `causal-propagation-or-blast-radius` |

The fixed coverage populations are:

```text
plugins
npc-records
race-records
placed-reference-records
unsupported-records
face-gen-loose-assets
face-gen-archive-assets
localized-strings
automatic-environment-discovery
taxonomy-subjects
```

The closed projected gap pairs are:

| Condition | Population | Missing capability |
|---|---|---|
| Unsupported signature | `unsupported-records:{signature_lower}` | `allowlisted-record-family-semantics` |
| Unsupported field | `unsupported-fields:{signature_lower}:{field_lower}` | `allowlisted-record-field-semantics` |
| Unsupported shape | `unsupported-shapes:{signature_lower}:{field_lower}` | `allowlisted-record-shape-semantics` |
| Unresolved localized value | `localized-strings` | `localized-string-resolution` |
| Unresolved archive availability | `face-gen-archive-assets` | `archive-activation-and-member-precedence` |
| Requested discovery unavailable | `automatic-environment-discovery` | `automatic-environment-discovery` |
| Unknown template decision | `face-gen-applicability:template` | `complete-template-traits-decision` |
| Unknown race decision | `face-gen-applicability:race` | `resolved-winning-race` |

No underscores, enum display labels, `unspecified`, product gap ID, or reason
prose may substitute for these strings.

## 3. Fact-family construction

### 3.1 `result`

Always emit:

| Fact ID | Type | Value type | Value |
|---|---|---|---|
| `result/snapshot_present` | `state` | `boolean` | whether an authoritative snapshot is published |
| `result/failure_present` | `state` | `boolean` | whether one or more public-envelope failures exist |

The result document's top-level state is compared separately and is exactly
one of `completed`, `completed_with_gaps`, `invalid_input`,
`changed_during_read`, or `failed`. Exact failure count, IDs, codes, inputs,
messages, and prose are excluded.

If snapshot is false, emit no snapshot family. `result_gaps` may still exist.
A valid published snapshot cannot accompany `invalid_input`,
`changed_during_read`, or `failed`.

Mutation examples: flip either boolean, publish snapshot families while
snapshot is false, or omit either fact; each is invalid or a mismatch.

### 3.2 `plugins`

For manifest plugin sequence ordinal `i`, root `plugins/{i_D4}` emits:

```text
/plugin_name   plugin string   lower plugin filename
/load_order    plugin integer  manifest load order
/provider_id   plugin string   lower local installed entity ID
/master_style  plugin string   full | light
/masters/{j_D4} plugin string  lower master filename in TES4 order
```

Plugin name, load order, and provider ID must be unique. Masters are decoded
from retained TES4 bytes; zero masters emits no master child fact. Manifest
sequence and decoded plugin order must agree exactly.

Positive: a light plugin at sequence 1 emits `plugins/0001/master_style =
light`. Negative: reversing masters is a mismatch. Null/missing: an empty
master list is missing child facts, not a null fact. Mutation: change only a
provider ID's case; canonical output remains lowercase.

### 3.3 `override_chains`

For each canonical record FormKey `F`, root
`override_chains/{seg(F)}` emits:

```text
/identity/signature       record_identity string  uppercase signature
/identity/form_key        form_key       string  F
/identity/origin_plugin   record_identity string  lower origin plugin
/identity/origin_local_id record_identity integer translated origin local ID
```

For contribution sequence ordinal `i`, root `/contributions/{i_D4}` emits the
contribution fields in section 3.4. `/winner` emits:

```text
/source_plugin winner string
/load_order    winner integer
/form_key      winner string
/deleted       winner boolean
/compressed    winner boolean
/raw_flags     winner integer
```

The chain is ordered by accepted load order. Winner is the final effective
contribution, not a product winner ID. A chain exists once per observed
canonical FormKey and contains at least one contribution. Duplicate FormKeys,
out-of-order contributions, or a winner not semantically identical to the
effective contribution are invalid.

### 3.4 Common contribution facts

At any `{root}/contribution` or override contribution root emit:

```text
/identity/signature       record_identity string
/identity/form_key        form_key string
/identity/origin_plugin   record_identity string
/identity/origin_local_id record_identity integer
/source_plugin            contribution string
/load_order               contribution integer
/deleted                  contribution boolean
/compressed               contribution boolean
/raw_flags                contribution integer
```

Signatures are uppercase; plugins lowercase. Deleted contributions still emit
all structural facts. Compression affects representation, not decoded
semantics.

### 3.5 `npc_contributions`

For semantic contribution identity `C`, root
`npc_contributions/{seg(C)}` emits common contribution facts plus:

```text
/kind                npc string   npc
/configuration_flags npc integer
/template_flags      npc integer
/uses_template       npc boolean
/templates_traits    npc boolean
/template            singleton TPLT link
/race                singleton RNAM link
/hair_color          singleton HCLF link
/ai_data_present     npc boolean
/packages/{seg(link_identity)}   repeatable PKID links
/head_parts/{seg(link_identity)} repeatable PNAM links
```

`templates_traits` is true only for a definite traits-inherited decision.
Unknown template-traits decisions do not masquerade as false. The full
template decision is reflected by FaceGen applicability/gaps; `/4` retains the
boolean transport.

`ai_data_present` reports AIDT presence only. Typed AIDT subfields are
excluded. Repeatable link arrays preserve byte occurrence through ordinal but
sort by link identity in canonical output.

### 3.6 `race_contributions`

For contribution identity `C`, root
`race_contributions/{seg(C)}` emits common contribution facts plus:

```text
/kind          race string  race
/face_gen_head race boolean decoded RACE FaceGenHead decision
```

An undecodable RACE `DATA` shape is an unsupported-shape gap; it does not
publish a guessed boolean contribution.

### 3.7 `placed_reference_contributions`

For contribution identity `C`, root
`placed_reference_contributions/{seg(C)}` emits common contribution facts plus:

```text
/kind               reference string reference
/base               singleton NAME link
/linked_references/{seg(link_identity)} paired XLKR links
/location_reference singleton XLRL link
/owner               singleton XOWN link with fact_type ownership
/placement/position/{x|y|z} placement number
/placement/rotation/{x|y|z} placement number
```

All six placement facts exist together when `DATA` placement is encoded. None
exist when it is absent. Values must be finite. Partial vectors are invalid.

### 3.8 `allowlisted_fields`

For contribution identity `C` and observed field `K`, identity string
`{C}:{K_lower}` and root `allowlisted_fields/{seg(identity)}` emit:

```text
/field field string  uppercase K
/count field integer positive observed occurrence count
```

The closed countable sets are:

- `NPC_`: `EDID`, `ACBS`, `TPLT`, `RNAM`, `AIDT`, `PKID`, `PNAM`, `HCLF`;
- `RACE`: `EDID`, `DATA`; and
- `REFR`: `EDID`, `NAME`, `XLKR`, `XLRL`, `XOWN`, `DATA`.

`EDID` is identifying metadata only. Its count never creates taxonomy,
purpose, area, consequence, intent, finding, or case facts. Repeated fields
aggregate within one contribution only. Same field in two override
contributions produces two identities. Unobserved fields produce no facts.

### 3.9 `npcs`, `races`, and `placed_references`

Resolved maps use roots:

```text
npcs/{seg(canonical_form_key)}
races/{seg(canonical_form_key)}
placed_references/{seg(canonical_form_key)}
```

Each emits the same semantic body and `kind` fact as its contribution family,
using the winning contribution. There is one resolved object per non-ambiguous
effective record. A deleted winner remains represented structurally and
affects FaceGen precedence. No resolved object is published when input
decoding cannot authoritatively choose its required scalar shape.

The resolved map does not copy a product participant ID. Mutating only such an
ID must leave these fact families byte-identical.

### 3.10 `face_gen`

There is exactly one FaceGen assessment for every winning NPC, rooted at
`face_gen/{seg(npc_form_key)}`. Emit:

```text
/npc_form_key   face_gen string  canonical NPC FormKey
/applicability  face_gen string  exact vocabulary from section 2.9
/origin_plugin  face_gen string  lower origin plugin
/origin_local_id face_gen integer canonical origin local ID
```

Apply precedence exactly:

1. deleted winner;
2. unknown template-traits decision;
3. definite trait inheritance;
4. missing, null, unresolved, or semantically unknown race/FaceGenHead;
5. resolved race without `FaceGenHead`; and
6. applicable.

The corresponding applicability strings are the six strings in section 2.9.
Deleted and definite trait-template states create no applicability gap.
Unknown template creates the template gap pair. Unknown race creates the race
gap pair. Non-trait template use does not suppress the NPC's own assessment.

The canonical paths are:

```text
meshes/actors/character/facegendata/facegeom/{origin_plugin_lower}/{origin_local_id_8lowerhex}.nif
textures/actors/character/facegendata/facetint/{origin_plugin_lower}/{origin_local_id_8lowerhex}.dds
```

For `mesh` and `tint`, emit:

```text
/{kind}/normalized_relative_path face_gen string
/{kind}/provider_ids/{i_D4}      face_gen string
/{kind}/winner_provider_id       face_gen string or typed null
/{kind}/present                  face_gen boolean
/{kind}/exact_absence_known      face_gen boolean
```

For an applicable NPC, match a declared chain by normalized path. Preserve its
provider order and explicit winner. A matched chain means `present`. Without a
chain, availability is `absent` only when the manifest supplies independent
exhaustive byte-verified loose-index authority; otherwise it is `unknown`.
The current M1 structural manifest supplies no such absence authority, so its
missing path is `unknown`.

For a non-applicable or applicability-unknown NPC, the two protocol framing
asset objects remain authorable from the canonical paths but are not evaluated
as coverage members: provider sequence is empty, winner is null, and semantic
availability is `unknown`. This does not assert asset absence.

Archive support never changes loose availability. An applicable non-present
path enters the archive coverage denominator and either completes from
separate archive authority or emits the archive gap.

### 3.11 `taxonomy`

For complete taxonomy identity `(subject, subject_type, axis, facet, code,
applicability, role)`, root is:

```text
taxonomy/{seg(subject)}/{seg(subject_type)}/{seg(axis)}/{seg(facet)}/
  {seg(code_or_literal_null)}/{seg(applicability)}/{seg(role)}
```

The displayed line break is not part of the ID. Emit:

```text
/taxonomy_id taxonomy string  infinium.skyrim-se.mod-impact-taxonomy
/canonical_subject taxonomy string
/subject_type taxonomy string
/axis taxonomy string
/facet taxonomy string
/applicability taxonomy string
/role taxonomy string
/taxonomy_version/major taxonomy integer 0
/taxonomy_version/minor taxonomy integer 1
/taxonomy_version/patch taxonomy integer 0
/code taxonomy string or typed null
```

Every admitted plugin-record contribution emits exactly these required core
tuples on its `record-contribution` subject:

1. axis `technical-modification-surface`, facet `semantic-mechanism`, code
   `surface.plugin-data`, applicability `assigned`, role `observed`; and
2. axis `technical-modification-surface`, facet
   `realization-and-delivery`, code `delivery.plugin-container`,
   applicability `assigned`, role `observed`.

Create sparse semantic subjects only from decoded evidence:

- NPC `PKID` or `AIDT` semantics create suffix `area.actors.ai-packages` with
  assigned/established area code of the same value.
- NPC `RNAM`, `PNAM`, or `HCLF` semantics create suffix
  `area.actors.appearance-identity` with assigned/established area code of the
  same value. `EDID` alone does not.
- REFR `NAME`, `XLKR`, `XLRL`, `XOWN`, or placement semantics create suffix
  `area.world.placed-objects-activation` with assigned/established area code
  of the same value.
- Each declared FaceGen chain subject emits `surface.asset` under semantic
  mechanism and `delivery.loose-data-file` under realization/delivery, both
  assigned/observed.

An unsupported-record subject emits only the two required technical-core
tuples above. Its exact unsupported semantic limitation is carried by the gap
family. This minimal rule closes the owner disposition's optional wording and
prevents a filler null matrix while retaining the independently meaningful
technical facts.

Purpose, consequence, and extent tuples require independent applicable
evidence beyond record signatures, filenames, EDID, or provider topology. The
answer-free byte-only authorability package supplies none, so it emits none.
An explicit non-assigned tuple is permitted only when another public subject
contract requires communicating that real conclusion; it is not matrix fill.

Duplicate complete taxonomy identities are invalid. Product assignment,
analyzer/adjudicator, evidence, and reason values are excluded.

### 3.12 `coverage`

Emit exactly all ten fixed populations. For population `P`, root
`coverage/{seg(P)}` emits:

```text
/population  coverage string  P
/denominator coverage integer nonnegative
/completed   coverage integer 0..denominator
/state       coverage string  exact coverage vocabulary
```

Arithmetic is:

| Population | Denominator | Completed |
|---|---|---|
| `plugins` | manifest plugins | plugins admitted |
| `npc-records` | admitted `NPC_` contributions | successfully decoded contributions |
| `race-records` | admitted `RACE` contributions | successfully decoded contributions |
| `placed-reference-records` | admitted `REFR` contributions | successfully decoded contributions |
| `unsupported-records` | contributions outside family allowlist | conclusively classified unsupported contributions |
| `face-gen-loose-assets` | two paths per applicable NPC | exact present or exact absent paths |
| `face-gen-archive-assets` | applicable paths without a loose winner requiring archive decision | archive-resolved paths |
| `localized-strings` | encountered localized values requiring resolution | resolved values |
| `automatic-environment-discovery` | one when requested, otherwise zero | one when completed |
| `taxonomy-subjects` | required contribution, semantic, unsupported, and declared FaceGen-chain subjects | subjects with all required meaningful tuples |

Every gap attached to a population prevents plain `completed`. A zero row is
retained and completed. Counts are computed from semantic members, not number
of JSON objects or product gap IDs.

### 3.13 `gaps` and `result_gaps`

Aggregate by exact `(population, missing_capability)`. For collection `G`
equal to `gaps` or `result_gaps`, root is:

```text
G/{seg(population)}/{seg(missing_capability)}
```

and emits:

```text
/population         gap string
/denominator        gap integer exact affected-member count
/missing_capability gap string
```

Identical pairs aggregate by sum of affected semantic members. Distinct
signature/field/shape details remain distinct populations. Denominator is
positive. Duplicate aggregate keys, zero/negative denominators, generated IDs,
and reason prose are invalid.

`gaps` is the published snapshot gap set. `result_gaps` is the result-envelope
gap set and is independently emitted even when it has the same pairs. With a
snapshot, the bounded extractor's envelope carries every snapshot gap, so the
expected sets are equal. Without a snapshot, emit only publicly established
envelope gaps; do not invent an exact diagnostic gap from failure prose.

## 4. Invalid-state and aggregation rules

The oracle stops rather than repairing any of these conditions:

- invalid or duplicate manifest identity;
- unknown enum/default/`unspecified` vocabulary;
- noncanonical FormKey, plugin, provider, signature, path, or percent escape;
- nonfinite number or out-of-range integer;
- partial placement vector;
- illegal link state/component/target combination;
- unsupported scalar shape presented as decoded truth;
- invalid FaceGen availability/winner combination;
- missing, extra, duplicate, or arithmetically inconsistent coverage row;
- gap key outside the closed vocabulary;
- taxonomy code/applicability mismatch, subject suffix outside the registry,
  provider-topology subject, filler tuple, or duplicate semantic tuple; or
- duplicate final fact ID.

An unsupported but well-framed record, field, or shape uses the exact gap and
coverage rules. Malformed bytes that prevent authoritative framing cause the
applicable terminal no-snapshot state; they are not counted as an unsupported
semantic member.

## 5. Generic examples and mutations

The tracked answer-free rehearsal package is
[`../fixtures/protocol-4-oracle-authorability/`](../fixtures/protocol-4-oracle-authorability/README.md).
It includes generic UTF-8 synthetic bytes and an answer-free execution
manifest. It covers:

- a normal full plugin and a light-origin plugin;
- positive and matched-negative record shapes;
- singleton, repeatable, null, resolved, unresolved, and paired-`XLKR` links;
- present multi-provider and present single-provider FaceGen chains plus
  structural-only unknown paths;
- all four semantic suffix forms;
- zero and nonzero fixed coverage rows and all eight gap pairs;
- explicit taxonomy code null in a mutation exercise;
- duplicate fact, duplicate semantic tuple, duplicate provider, invalid link
  state, invalid FaceGen transport, nonfinite number, and missing coverage-row
  mutations; and
- all fifteen active families.

The package embeds no expected fact list. A fresh reviewer constructs expected
facts into ignored `work/` storage, runs the public mechanical validator, and
records only an answer-isolated attestation in tracked documentation.

Positive example: two `XLKR` values at occurrence zero become separate
`linked-reference` and `keyword` link identities with the same ordinal.

Matched negative: an EDID-only contribution receives only technical-core
taxonomy; it does not receive an area or consequence tuple.

Null/missing example: a null `XOWN` singleton emits only typed-null
`/owner/state`; absent placement emits no placement facts.

Mutation example: replacing a null singleton with string state `unspecified`
is invalid, rather than a different acceptable expected value.

## 6. Public authority allowlist for a future private role

A future separately authorized private authoring task may receive only:

1. accepted product, architecture, evaluation, and governance documents named
   by the accepted Slice 4.5 plan;
2. this specification;
3. the completed public realignment plan;
4. the final oracle-authority matrix and semantic-authority owner disposition;
5. frozen evaluator `/4` public documentation, schemas, adapter, and
   canonicalizer as mechanics; and
6. the answer-free private execution manifest and retained bytes supplied by
   the custodian under governance v2.

Product source, product tests, candidate diffs, candidate assemblies/output,
public synthetic expected answers, prior oracles, private predecessor answers,
and prior-session or memory-derived behavior are not semantic authority.
Candidate identity may be supplied only as a tuple binding after an oracle is
independently authored; it does not supply a value.

This allowlist does not authorize that future task, corpus qualification,
candidate execution, comparison, or scoring.
