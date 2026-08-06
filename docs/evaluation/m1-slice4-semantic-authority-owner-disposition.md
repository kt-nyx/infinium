# M1 Slice 4 semantic-authority owner disposition

Status: Accepted
Owner: Project owner
Recorded: 2026-08-05
Protocol: `infinium.evaluator-v2/4`
Projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`

## Purpose and authority

This is the accepted evaluation-level disposition of the six public semantic
mismatches recorded during Slice 4.5 authority completion. ADR-0028 is the
governing product decision. This record supplies the exact bounded vocabulary
and arithmetic needed by a later product-blind oracle reviewer.

It does not freeze an oracle, authorize private-repository access, or authorize
Stage C2 scoring.

## Exact oracle rules

### Allowed fields

`EDID` is allowlisted identifying metadata for `NPC_`, `RACE`, and `REFR`.
Emit its observed allowlisted-field count. Do not derive a taxonomy assignment
or consequence from the value itself.

### Layered partial-decode disposition

[ADR-0029](../architecture/decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md)
governs admitted records whose later decode or resolution is incomplete. Keep
every independently proven lower-layer fact, omit only claims whose own
prerequisites are unavailable, and expose the exact coverage gap. Null,
unknown, absent, undecodable, and not applicable are distinct states.

For an admitted `RACE` contribution with a structurally present but
unsupported `DATA` shape:

- emit its override-chain and common contribution facts;
- emit the contribution taxonomy subject and its mandatory generic
  `surface.plugin-data` and `delivery.plugin-container` assignments;
- do not emit a `DATA` allowlisted-field count unless the exact occurrence
  count was independently observed;
- do not emit `face_gen_head` or a complete resolved race fact that depends on
  decoding it;
- include it in `race-records` denominator arithmetic but not the completed
  count;
- include the technical subject in taxonomy-subject arithmetic and count it
  complete when its required generic assignments are emitted; and
- emit/aggregate population `unsupported-shapes:race:data` with missing
  capability `allowlisted-record-shape-semantics`.

This is the authoritative disposition of the second public authorability gap.
All other admissible state/fact combinations must be closed by the accepted
[Pre-B2 evidence-contract totality plan](../plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md)
before a new private task can be considered.

### FaceGen applicability

Apply the ADR-0028 precedence exactly. Deleted and definite trait-templated
winners are not applicable and produce no applicability gap. Unknown template
decisions use population `face-gen-applicability:template` and capability
`complete-template-traits-decision`. Unknown race decisions use population
`face-gen-applicability:race` and capability `resolved-winning-race`.

For each applicable NPC, evaluate both canonical FaceGen loose paths. Encode
availability as:

- present: `present=true`, `exact_absence_known=false`, winner required;
- absent: `present=false`, `exact_absence_known=true`, winner null; or
- unknown: both booleans false, winner null.

The fourth boolean combination is invalid. Archive activation and member
precedence is reported separately.

### Fixed coverage registry and arithmetic

Every published snapshot emits all ten populations below. A zero denominator
has completed count zero and state `completed`.

| Population | Denominator | Completed count |
|---|---|---|
| `plugins` | manifest plugins | plugins admitted to the ordered effective-installation model |
| `npc-records` | admitted `NPC_` contributions | contributions decoded through the bounded contract |
| `race-records` | admitted `RACE` contributions | contributions decoded through the bounded contract |
| `placed-reference-records` | admitted `REFR` contributions | contributions decoded through the bounded contract |
| `unsupported-records` | contributions outside the record-family allowlist | contributions conclusively classified as unsupported |
| `face-gen-loose-assets` | two canonical paths per applicable NPC | paths resolved to exact present or exact absent |
| `face-gen-archive-assets` | applicable paths without a loose winner that require an archive decision | paths whose archive availability is resolved |
| `localized-strings` | encountered localized values requiring resolution | values resolved by the available string-table authority |
| `automatic-environment-discovery` | one when discovery is requested, otherwise zero | one only when discovery completes |
| `taxonomy-subjects` | semantic subjects required by the bounded emission contract | subjects whose required meaningful assignments are constructed |

Use the accepted coverage-state vocabulary. Any incomplete count or attached
gap prevents a plain `completed` state.

### Layered gap projection

Internally, group gaps under stable categories such as unsupported record,
field, shape, capability, or FaceGen applicability. For protocol `/4`, project
these exact public populations and capabilities when applicable:

| Condition | Population | Missing capability |
|---|---|---|
| unsupported record signature | `unsupported-records:{signature}` | `allowlisted-record-family-semantics` |
| unsupported field | `unsupported-fields:{signature}:{field}` | `allowlisted-record-field-semantics` |
| unsupported field shape | `unsupported-shapes:{signature}:{field}` | `allowlisted-record-shape-semantics` |
| unresolved localized string | `localized-strings` | `localized-string-resolution` |
| unresolved archive availability | `face-gen-archive-assets` | `archive-activation-and-member-precedence` |
| unavailable automatic discovery | `automatic-environment-discovery` | `automatic-environment-discovery` |
| unknown template-traits decision | `face-gen-applicability:template` | `complete-template-traits-decision` |
| unknown winning-race decision | `face-gen-applicability:race` | `resolved-winning-race` |

Use lowercase signatures and fields in projected populations. Aggregate
identical population/capability pairs and count the exact affected members.
Implementation gap IDs and reason prose are not oracle authority.

### Hybrid taxonomy emission

The canonical bounded-M1 persisted pairs are:

- `declared-purpose-and-intended-feature-area` / `purpose-kind`;
- `technical-modification-surface` / `semantic-mechanism`;
- `technical-modification-surface` / `realization-and-delivery`;
- `affected-game-system-or-content-area` / `affected-area`;
- `consequence-type` / `consequence-type`;
- `effect-extent` / `direct-subject-breadth`;
- `effect-extent` / `spatial-breadth`;
- `effect-extent` / `persistence-and-lifecycle-breadth`; and
- `effect-extent` / `causal-propagation-or-blast-radius`.

Every plugin-record contribution emits technical assignments for
`surface.plugin-data` and `delivery.plugin-container`. Recognized semantic
subjects add only assignments supported by decoded fields or links.
Unsupported-record subjects retain the
technical assignments and may emit explicit unsupported area or unknown
consequence when that communicates the limitation; they do not emit filler
tuples for every axis.

Each declared FaceGen loose-provider chain creates its own provider subject,
even when it has only one provider. Exact loose-chain authority supports
`surface.asset` and `delivery.loose-data-file`. Do not create a generic global
provider-topology subject from the plugin list alone.

## Effect on Slice 4.5

The owner questions are closed, but the product/specification mismatch is not
yet implemented away. The next public task must update the Bethesda semantic
extractor and its public tests, qualify the corrected candidate, and freeze its
new exact identity. Only then may a fresh isolated B2 oracle reviewer receive
the allowlisted public authority and already-frozen private inputs. Protocol
`/4` is retained; protocol `/5` is neither needed nor authorized.

## Subsequent implementation status

The required public realignment, independent review, requalification, and
candidate freeze completed on 2026-08-05 at
`a98d648bd0adb2751ee0c09828e0227b1583950f`. The exact public handoff is
[the Slice 4.5 product candidate freeze](m1-slice4.5-public-product-candidate-freeze.json).
At that checkpoint, one fresh isolated private B2 oracle reviewer was permitted
to receive the allowlisted public authority and already-frozen private inputs
once. That authorization was subsequently consumed by the terminal attempt
described below; C2 and Stage D remain unrun.

## Superseding oracle-contract authorability status

The single authorized B2 resume subsequently ran and stopped without an
oracle or product verdict. The public-only contract-completion attempt then
used its one permitted correction pass and hard-stopped when independent
re-review found a second material cross-family authority gap. This does not
rewrite the six accepted semantic decisions above, but it means they are not
yet a complete exact oracle-construction bundle. Candidate conformance was not
inspected. Project-owner milestone-plan disposition was required, and no new
B2, corpus, downstream stage, or protocol `/5` was authorized by that attempt.

## Accepted successor disposition

The project owner accepted ADR-0029 and the public-only
[Pre-B2 evidence-contract totality plan](../plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md).
That plan replaces fixture-led correction with a total state-to-fact model,
mechanical completeness gate, model-derived synthetic coverage, fresh
product-blind review, and only then frozen-candidate classification. Its first
work package is `M1/S4.5/PRE-B2/WP1`. No private execution or protocol change
is authorized by this disposition.
