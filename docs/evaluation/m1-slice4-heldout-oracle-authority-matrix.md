# M1 Slice 4 final held-out oracle-authority matrix

Status: **normative for evaluator v2 protocol `/4` and projection `3.0.0`**.

This matrix is the complete bounded projection contract for the final Slice 4
successor. A held-out author may derive every included value from retained
input bytes, the answer-free execution manifest, and the public contracts
named here. Product-generated identifiers are never oracle authority.

<!-- active-fact-families: result,plugins,override_chains,npc_contributions,race_contributions,placed_reference_contributions,allowlisted_fields,npcs,races,placed_references,face_gen,taxonomy,coverage,gaps,result_gaps -->

## Active held-out fact families

| Fact family | Held-out status | Oracle authority and derivation | Canonicalization and ordering | Null or missing rule | Boundary reason |
|---|---|---|---|---|---|
| `result` | Included | Whether a snapshot is published and whether one or more failures exist in the public result envelope. | Two booleans: `snapshot_present` and `failure_present`. | Always present. Exact failure codes and prose are not projected. | A held-out author can determine valid publication state without reproducing product failure classification IDs. |
| `plugins` | Included | Accepted manifest plugin order and answer-free local installed entity IDs. | Manifest order; lowercase plugin/provider IDs; ordered masters. | Required manifest facts are present; absent masters produce an empty sequence. | Load/provider topology is independently authorable. |
| `override_chains` | Included | Record headers and ordered contributions decoded from retained plugin bytes; winner is the final effective contribution under accepted load order. | Rooted by canonical ID-first FormKey; contribution sequence remains ordered; winner fields use source plugin, load order, FormKey, flags, deleted, and compressed. | Required for observed chains. | No product contribution or winner ID is required. |
| `npc_contributions` | Included | NPC records independently decoded from retained bytes. | Rooted by evaluator-owned semantic contribution identity; links by field, component, and ordinal. | Optional links expose explicit null state; `ai_data_present` is always boolean. | Typed AIDT subfields remain public conformance facts but are excluded from held-out scoring. |
| `race_contributions` | Included | RACE records independently decoded from retained bytes. | Rooted by evaluator-owned semantic contribution identity. | Required projected fields are present. | Independently authorable record semantics. |
| `placed_reference_contributions` | Included | REFR records independently decoded from retained bytes. | Rooted by evaluator-owned semantic contribution identity; links by field/component/ordinal; finite placement numbers retain semantic numeric comparison. | Optional links and placement follow explicit-null versus missing source semantics. | Independently authorable placement, ownership, and reference semantics. |
| `allowlisted_fields` | Included | Count of independently observed allowlisted fields per semantic contribution. | Rooted by evaluator-owned semantic contribution identity plus lowercase field name. | No fact when the field is unobserved. | Counts are derivable without product IDs. |
| `npcs` | Included | Resolved effective NPC facts derived from retained contributions and accepted order. | Rooted by canonical FormKey; same NPC rules as contribution facts. | Optional links are explicit null; AIDT is presence-only. | Effective semantics are independently reconstructible. |
| `races` | Included | Resolved effective RACE facts. | Rooted by canonical FormKey. | Required projected fields are present. | Effective semantics are independently reconstructible. |
| `placed_references` | Included | Resolved effective REFR facts. | Rooted by canonical FormKey; link and placement rules match contribution facts. | Optional links are explicit null; placement is absent when not encoded. | Effective semantics are independently reconstructible. |
| `face_gen` | Included | Canonical NPC FormKey plus answer-free loose-provider chains and winner provider ID. | Rooted by canonical NPC FormKey; provider order preserved; paths slash-normalized and lowercase. | Missing loose assets preserve `present` and `exact_absence_known`; winner may be explicit null. | Manifest provider IDs identify installed entities and are not product answer IDs. |
| `taxonomy` | Included | Semantic taxonomy tuple derived from independently observed record/provider semantics and the public taxonomy contract. | Rooted by evaluator-owned canonical subject, subject type, axis, facet, code-or-null, applicability, and role; facts include those fields plus taxonomy ID/version. Duplicate semantic tuples are invalid. | Null code is explicit. Product assignment, analyzer/adjudicator, evidence, and reason fields are not projected. | The semantic tuple is authorable; product-generated bookkeeping identities are not. |
| `coverage` | Included | Population, denominator, completed count, and state computed from independently authorable projected populations. | Rooted and sorted by population. | Required for declared populations. Denominator labels and gap-ID lists are excluded. | Coverage arithmetic is authorable without product gap identities. |
| `gaps` | Included | Population, denominator, and missing capability. | Rooted by population plus missing capability and sorted ordinally. | Empty when no snapshot-level gap exists. | Capability absence is semantic; generated gap IDs are not. |
| `result_gaps` | Included | Result-envelope population, denominator, and missing capability. | Same as `gaps`. | Empty when no result-level gap exists. | Preserves failure-boundary coverage without generated IDs. |

Evaluator-owned semantic contribution identity is the normalized tuple of
source plugin, load order, record FormKey, signature, raw flags, deleted state,
and compressed state. Provider-topology taxonomy subjects are derived from the
ordered manifest plugin/provider tuple. Unsupported-record taxonomy subjects
are derived from source plugin, signature, and canonical FormKey.

## Public-conformance-only fields

| Fact family or product field | Held-out status | Independent authority and derivation | Canonicalization and ordering | Null or missing rule | Boundary reason |
|---|---|---|---|---|---|
| Exact failure `code`, count, input, and message | Public-conformance-only | Product failure contract and public fault fixtures. | Product serialization rules. | Product contract governs absence. | Hidden bytes prove failure, no snapshot, and state; they do not authorize exact product diagnostic vocabulary. |
| Typed AIDT aggression, confidence, energy, responsibility, mood, assistance, warn, warn-or-attack, attack, and aggro-radius behavior | Public-conformance-only | Selected Mutagen-backed product contract and public fixture byte assertions. | Product contract field mapping. | Product contract governs nullable AIDT. | Hidden raw bytes authorize AIDT presence/shape but the accepted held-out authority withholds typed interpretation. |
| Taxonomy `assignment_id`, `analyzer_or_adjudicator_id`, evidence-reference strings, and reason | Public-conformance-only | Public product taxonomy generation/provenance contract. | Product hash/string construction and ordering. | Product contract governs presence. | Reproducing these values would require an internal algorithm, not independent semantic judgment. |
| Product contribution, participant, winner-contribution, gap, coverage-gap, and source-snapshot IDs | Public-conformance-only | Public serialization and persistence contract tests. | Product-owned ID algorithms. | Product contract governs presence. | Their held-out semantic information is represented by smaller evaluator-owned identities. |
| Exact `.esl` without TES4 light-flag failure behavior | Public-conformance-only | Public byte fixture plus the accepted product failure contract. | Exact code `esl-header-flag-missing`; product input naming. | One failure and no snapshot for this public regression. | Valid/invalid light classification remains independently authorable; exact failure spelling does not. |

These checks protect product behavior; they are not copied into a held-out
answer key.

## Excluded held-out fields

| Fact family or field | Held-out status | Independent authority and derivation | Canonicalization and ordering | Null or missing rule | Boundary reason |
|---|---|---|---|---|---|
| Top-level exact `failures/*` facts | Excluded | Replaced by `result/failure_present` plus top-level state. | Not applicable. | Never emitted. | Exact diagnostic spelling is not hidden truth. |
| Global `links` collection | Excluded | Link semantics are emitted only in their owning NPC/REFR context. | Not applicable. | Never emitted. | Removes redundant product participant/contribution plumbing without losing semantic links. |
| `resolved_participants` collection | Excluded | Canonical record FormKeys root the resolved facts. | Not applicable. | Never emitted. | Product participant IDs add no independent semantic distinction. |
| Coverage denominator labels and `gap_ids` | Excluded | Population, denominator, completed count, state, and missing capability remain included. | Not applicable. | Never emitted. | Display labels and product gap joins are not needed to author the semantic claim. |
| Physical paths, dependency fingerprints, timestamps, producer/build metadata, prose, and incidental serializer fields | Excluded | Tuple and retained-byte admission cover relevant identity separately. | Not applicable. | Never emitted. | These values are environment- or invocation-specific rather than held-out semantics. |

The projection must not contain product-generated `contribution_id`,
`participant_id`, `winner_contribution_id`, `assignment_id`,
`analyzer_or_adjudicator_id`, evidence IDs, `gap_id`, `gap_ids`, snapshot IDs,
exact failure codes, failure prose, taxonomy reasons, coverage denominator
labels, typed AIDT subfields, physical paths, dependency fingerprints,
timestamps, or incidental serializer fields. Changing only one of these
internal values must leave projected facts byte-identical.

Missing projected facts and explicit typed null facts are distinct. Maps and
semantic sets sort by their evaluator-owned identity; manifest sequences,
override contributions, masters, providers, and ordinal links preserve their
contract order. Any duplicate final fact ID is a candidate-output contract
violation rather than an implicit tie-break.
