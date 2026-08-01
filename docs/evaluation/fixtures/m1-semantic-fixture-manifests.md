# M1 semantic fixture-manifest specifications

Status: Accepted  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-08-01
Companion specification:
[M1 semantic and local-ground-truth evaluation specifications](../specifications/m1-semantic-and-ground-truth.md)
with the accepted
[revision 2 amendment](../specifications/m1-semantic-and-ground-truth-v2-amendment.md)

## 1. Purpose and current state

This document defines the accepted manifest inventory and fixture partitions for
EVAL-0001, EVAL-0002, EVAL-0016, EVAL-0017, EVAL-0032, EVAL-0051,
EVAL-0052, EVAL-0054, EVAL-0065, EVAL-0067, and EVAL-0083 through
EVAL-0086.

It is a specification and inventory, not one monolithic executable corpus.
Except for the research qualification artifacts and the Slice 3.5 Bethesda
packages explicitly identified below:

- many fixture payloads and oracle manifests have not been created;
- held-out slots other than the sealed `BETH-HO-002` entry have not been
  independently authored or sealed;
- no unlisted fixture package or oracle manifest is accepted as
  execution-ready;
- no Bethesda semantic fixture has been executed against Slice 4 production
  behavior; and
- fixture acceptance alone passes no evaluation case; EVAL-0051 and EVAL-0054
  separately passed through the retained Slice 3 execution identified in
  Section 16.1, while Slice 4 and later cases remain unexecuted.

The terms `development`, `validation`, and `held-out` have the meanings in the
[fixture guidelines](../fixture-guidelines.md). A slot marked `required before
pass` is a real missing dependency, not implied coverage.

## 2. Manifest package structure

Every fixture package must have a stable ID and version and contain or point to
the following logically separate documents:

```text
fixture/
  public-manifest.json
  execution-input.json
  expected-oracle.json        # inaccessible to the system under test
  provenance.json
  replay-dependencies.json
  redistribution.json
  partition-history.json
```

Physical colocation is not required. Controlled-real `execution-input.json`
and source bodies may remain in evaluator-private storage; the tracked public
manifest contains their fingerprints and reproducible acquisition metadata.

### 2.1 Public manifest fields

```text
schema_id
schema_version
fixture_id
fixture_version
evaluation_ids[]
purpose
classification: positive | negative | boundary | malformed | unsupported
partition: development | validation | held-out
partition_history[]
taxonomy_id
taxonomy_version
input_package_fingerprint
oracle_fingerprint
provenance_fingerprint
replay_dependency_fingerprint
redistribution_class
owner
review_state
created_at
```

The `oracle_fingerprint` proves which pre-registered answer set was used but
does not reveal the answer to the execution path.

### 2.2 Execution input fields

```text
fixture_id
fixture_version
installation_snapshot_input
analysis_context_input
effective_scan_configuration
runtime_support_input
mo2_instance_profile_input
plugin_order_input
provider_order_input
source_claim_inputs[]
analyzer_declarations[]
tool_library_versions[]
declared_archive_state
declared_supported_capabilities[]
declared_unsupported_capabilities[]
resource_and_time_limits
input_payload_refs[]
```

Execution inputs may state the declared capability being exercised but must not
contain expected candidate/finding/case labels, oracle paths, answer-bearing
comments, or fixture-specific shortcuts.

Complete private validation and held-out packages, autonomous delegated access,
sanitized publication, and contamination transitions follow
[evaluator-private fixture governance](../evaluator-private-fixture-governance.md)
and ADR-0026.

### 2.3 Oracle fields

```text
fixture_id
fixture_version
oracle_version
independent_authors_and_reviewers[]
ground_truth_methods[]
expected_observations[]
expected_external_claims[]
expected_candidates[]
expected_hypotheses[]
expected_findings[]
expected_recommendations[]
expected_supported_cases[]
expected_lead_only_cases[]
expected_abstentions[]
expected_invalid_inputs[]
expected_coverage_and_gaps[]
expected_taxonomy_assignments[]
expected_replayability
forbidden_claims[]
known_limits[]
pre_registered_at
change_history[]
```

An oracle change requires new independent evidence, an explanation of the
prior error, review, a new oracle version/fingerprint, and preservation of the
old version. An implementation mismatch is not evidence that the oracle is
wrong.

### 2.4 Replay dependency fields

Every dependency records:

```text
dependency_id
kind
identity_or_version
byte_length?
sha256?
retention_location_class
availability: retained | externally_reacquirable | evaluator_private |
              unavailable
required_for: clean_recomputation[] | boundary_replay[] | audit[]
permission_and_redistribution
deletion_effect
```

The expected replay state is one of `complete-clean`, `boundary-replay`,
`audit-only`, or `unavailable`; product-facing mapping to complete, partial, or
unavailable replayability must remain explicit.

## 3. Partition inventory

| Package | Partition | Cases | State | Answer-isolation owner |
|---|---|---|---|---|
| `SEM-NPC-POS-001` | Development | EVAL-0001 | Required; not created | Independent semantic-fixture reviewer |
| `SEM-NPC-NEG-001` | Development | EVAL-0002 | Required; not created | Independent semantic-fixture reviewer |
| `SEM-NPC-VAL-001` / `SEM-NPC-VAL-NEG-001` | Validation | EVAL-0001/0002 metamorphs | Required; not created | Evaluation owner |
| `SEM-NPC-HO-001` / `SEM-NPC-HO-NEG-001` | Held-out | EVAL-0001/0002 | Required before held-out pass; not authored/sealed | Independent holdout custodian |
| `REAL-NPC-0001-POS` / `REAL-NPC-0001-CTRL` | Validation | EVAL-0016 | Research-qualified candidate; final executable manifest not created | Evaluation owner |
| `REAL-REFR-0001-POS` / `REAL-REFR-0001-CTRL` | Validation | EVAL-0017 | Research-qualified candidate; final executable manifest not created | Evaluation owner |
| `CAND-ATOMIC-DEV` | Development | EVAL-0032 | Required; not created | Independent population author |
| `CAND-INTEGRATION-VAL` | Validation | EVAL-0032 | Required; not created | Evaluation owner |
| `CAND-SCALE-VAL` / `CAND-STRESS-VAL` | Validation | EVAL-0032 | Required; not created | Benchmark/evaluation owner |
| `CAND-HO-001` | Held-out | EVAL-0032 | Required before held-out recall claim; not authored/sealed | Independent holdout custodian |
| `MO2-ATOMIC-DEV` | Development | EVAL-0051 | Constructed and exercised through the independent Slice 3 evaluator package | MO2 fixture operator |
| `MO2-INTEGRATION-VAL` / `MO2-NEGATIVE-VAL` | Validation | EVAL-0051 | Constructed, independently observed, and passed for the exact admitted target | Independent MO2 observer |
| `MO2-HO-001` | Held-out | EVAL-0051 | Required before general conformance claim; not authored/sealed | Independent holdout custodian |
| `BETH-NPC-DEV` / `BETH-REFR-DEV` / `BETH-MALFORMED-VAL` | Development | EVAL-0052 | Constructed and independently accepted as Slice 4 inputs; malformed package reclassified after its cases influenced generator corrections; production comparison remains pending | Binary-fixture author |
| `BETH-LIGHT-VAL` / `BETH-UNSUPPORTED-VAL` | Development | EVAL-0052 | Constructed and independently reviewed; public answer exposure makes them regression/development evidence rather than independent validation | Independent binary reviewer |
| `BETH-LIGHT-VAL-002` / `BETH-MALFORMED-VAL-002` / `BETH-UNSUPPORTED-VAL-002` | Validation | EVAL-0052 | Materially independent sealed packages in the separate evaluator-private Git store; public revision/fingerprints and attestations retained; EVAL-0052 remains unexecuted | Isolated private input/oracle roles |
| `BETH-HO-002` | Held-out | EVAL-0052 | Sealed evaluator-private successor after `BETH-HO-001` retention was unavailable; historical v1 metadata is preserved and invalidated; supported-shape conformance remains pending | Independent holdout custodian |
| `TARGET-1170-PRIVATE-VAL` and target-negative package | Validation | EVAL-0054 | Constructed in the independent Slice 3 evaluator package and passed against the exact admitted target and complete preregistered negative matrix | Runtime-gate reviewer |
| `ANALYZER-CONTRACT-DEV` and boundary variants | Development/validation | EVAL-0065 | Required; not created | Contract reviewer |
| `EVID-TYPES-DEV` / `EVID-LLM-VAL` / `EVID-NO-LLM-VAL` / `EVID-HOSTILE-VAL` | Development/validation | EVAL-0067 | Required; not created | Evidence-model reviewer |
| `LLM-CLAIM-LIVE-VAL` | Validation | EVAL-0067/EVAL-0083 plus live-provider gates | Required for the M1 live semantic proof; not created | Source-claim and provider reviewers |
| `LLM-INVESTIGATE-LIVE-VAL` | Validation | EVAL-0067/EVAL-0083 plus live-provider gates | Required for the M1 live semantic proof; not created | Candidate-analysis and provider reviewers |
| `EVID-HO-001` | Held-out | EVAL-0067 | Required before broad type-boundary claim; not authored/sealed | Independent holdout custodian |
| `PROV-LOCAL-DEV` / `PROV-SOURCE-LLM-VAL` / `PROV-CONTRADICTION-VAL` / `PROV-DELETION-VAL` | Development/validation | EVAL-0083 | Required; not created | Provenance reviewer |
| `PROV-LIVE-COMPOSED-VAL` | Validation | EVAL-0083 plus applicable live-provider gates | Required if M1 retains its bounded live Responses proof; not created | Provenance and provider reviewers |
| `PROV-HO-001` | Held-out | EVAL-0083 | Required before broad provenance claim; not authored/sealed | Independent holdout custodian |
| `CASE-SHARED-DEV` / `CASE-DISTINCT-VAL` / `CASE-LEAD-VAL` / `CASE-METAMORPH-VAL` | Development/validation | EVAL-0084 | Required; not created | Causal-grouping reviewer |
| `CASE-HO-001` | Held-out | EVAL-0084 | Required before broad grouping claim; not authored/sealed | Independent holdout custodian |
| `COVER-MATRIX-DEV` / `COVER-ZERO-FINDING-VAL` / `COVER-PARTIAL-VAL` / `COVER-TARGETED-VAL` | Development/validation | EVAL-0085 | Required; not created | Coverage/readiness reviewer |
| `COVER-HO-001` | Held-out | EVAL-0085 | Required before broad presentation claim; not authored/sealed | Independent holdout custodian |
| `TAX-AXES-DEV` / `TAX-COUNTEREXAMPLE-VAL` / `TAX-STATE-VAL` / `TAX-HISTORY-VAL` | Development/validation | EVAL-0086 | Slice 4-applicable BETH-linked projections accepted; broader packages and history mechanics remain required | Taxonomy reviewer |
| `TAX-HO-001` | Held-out | EVAL-0086 | Required before broad classification claim; not authored/sealed | Independent holdout custodian |

The known-answer real candidates cannot be promoted to held-out by changing a
label. If their result changes production behavior, they become development
fixtures and require materially independent replacement validation cases.

## 4. Synthetic semantic packages

### 4.1 `SEM-NPC-POS-001`

**Input manifest.**

- Minimal project-authored master plus three plugins: baseline, behavioral
  override, and later appearance override.
- Two `NPC_` records with stable origin FormKeys.
- Behavioral override changes a qualified package relation.
- Appearance override changes selected `PNAM`/`HCLF` data, restores/omits the
  behavioral package relation, and supplies qualified loose-only FaceGen
  mesh/tint paths.
- Applicable author-like project-authored documentation states appearance-only
  purpose and does not support package removal.
- Archive participation is explicitly excluded.

**Independent oracle.**

- Exact TES4/master bytes, record offsets, FormKeys, order, winner, package
  identities, appearance values, FaceGen keys/provider chains, and source
  passage spans.
- Expected typed output is the EVAL-0001 result in the companion
  specification.

**Metamorphs.**

- Rename all mod folders/plugin display values while retaining semantic IDs.
- Insert and reorder unrelated plugins/files.
- Change only the relevant winner and assert dependency-scoped invalidation.
- Remove the intent passage and expect abstention/lead behavior.

**Ground-truth independence.** The byte fixture and oracle are written/reviewed
without calling Mutagen. The implementation receives input bytes and normal
source evidence, not oracle annotations.

**Replay/distribution.** `complete-clean`; project-authored bytes and prose
shall be GPL-compatible and trackable.

### 4.2 `SEM-NPC-NEG-001`

This package is structurally isomorphic to `SEM-NPC-POS-001`, but its
independent applicable source passage explicitly declares the qualified
behavioral replacement/removal as intended.

The oracle expects the same observations and possible initial candidate, an
applicable contradicting/resolving intent claim, no finding, no supported case,
and no consequence/severity/remediation. Removing or version-mismatching the
claim produces an abstention or lead-only needs-input state.

The package must not differ from the positive through a fixture name, magic ID,
plugin-name allowlist, or model-visible answer cue.

### 4.3 Held-out semantic pair

`SEM-NPC-HO-001` and `SEM-NPC-HO-NEG-001` must be authored after the production
path and development fixtures are stable by an independent custodian. They
must use:

- different plugin/file names and local FormKeys;
- a materially independent author-like intent passage;
- a different but allowlisted package/appearance combination;
- the same generic causal abstraction; and
- a sealed oracle whose hash is tracked before execution.

If their results affect implementation, both become development data and a new
pair is required.

## 5. Controlled-real private manifests

### 5.1 Common policy

The authoritative research-level source manifest is
[`RESEARCH-0035/gate-c-case-manifests.json`](../../research/investigations/artifacts/RESEARCH-0035/gate-c-case-manifests.json).
Final fixture manifests must copy its exact identities by reference/fingerprint,
not weaken them to names or versions.

Third-party archives, plugins, official game masters, and assets are
evaluator-supplied private inputs. They are validated before execution against
the tracked lengths and SHA-256 values. No similar, newer, currently installed,
or same-named file may substitute automatically.

Wave F closed the remaining source-purpose and official-master identity gaps
on 2026-07-28. The source excerpts below came from the authenticated Nexus v2
GraphQL `mod` query because v3 does not expose long descriptions. The
canonical query shape was:

```graphql
query($modId: ID!, $gameId: ID!) {
  mod(modId: $modId, gameId: $gameId) {
    modId
    name
    version
    updatedAt
    description
  }
}
```

The live GraphQL schema-shape identity is the RESEARCH-0030 SHA-256
`4BCCAD0DE29D7FD978A6FA282A757A112EADE3BDAD75ED7B16B1F523682247EC`.
The selected excerpts are fixture evidence for declared purpose and the need
for compatibility handling only; their current page versions do not prove
that every statement or patch applies to every older selected file version.

| Nexus mod | Page version/update | Exact description SHA-256 | Selected author passage retained for M1 |
|---|---|---|---|
| AI Overhaul `21654` | `1.9.5`; `2026-06-19T21:47:20Z` | `E395CDE7646EFD91CE2FF6F1838AD3FABDB868CB92036F3B619D379E31A91248` | “Highly noticeable tweaks & rewrites many of the vanilla NPCs AI” |
| Children of the Pariah `97981` | `1.2.5`; `2026-03-30T18:17:40Z` | `E37BA195C0881C4459407B1DE8A3DE8E8A3F70843ABAE52282CF0EEE557DB70A` | “Not strictly compatible with mods that edit the same NPC records (appearance, skills, inventories, etc.)” |
| Candlehearth `97542` | `1.1.1`; `2023-08-23T14:04:06Z` | `C9FE3D712CDA1E26F4CB7FD235A44337B5E571721C8E3B5C8F80A769D58D3C42` | “Candlehearth is an inn overhaul that adds extended inn rentals and safe storage to every inn in Skyrim.” |
| Nightgate Inn Revived `121244` | `1.6`; `2025-06-14T08:23:41Z` | `642E1DE639F08DD4AC1292E7E63FB40A38D4307918197D996931226926CE8CDA` | “This mod is purely a visual overhaul, it does not edit NPCs or quests.” “A patch will be needed to resolve conflicts.” |

The full private description bodies are not redistributed by this manifest.
The executable fixture uses only the retained excerpts above plus the exact
source metadata, unless an accepted later fixture revision admits and
privately retains a larger passage under the source policy.

The exact official-master closure measured from the supported local Steam
`1.6.1170.0` environment and the selected Creation Club inputs is:

| Master | Bytes | SHA-256 |
|---|---:|---|
| `Skyrim.esm` | 249753412 | `2BBC77FDEC35A70EF96B710F8C525E50A1DB9E63E11A391A0EB9EE8F56D36107` |
| `Update.esm` | 18429562 | `5F2985B205EA57428164B47E1A5DF57F9B5A1AC0399D4C8B5CF30FC0A60FB008` |
| `Dawnguard.esm` | 24813534 | `1208E5153E35366E0ADA1A887720D6D636E2D8592D007FE142B37A57E46B476E` |
| `HearthFires.esm` | 3681749 | `70E0D5D6DC42224349D33E8C7BCA73DA447463F671CACC9C15FC0273C93E0008` |
| `Dragonborn.esm` | 64259475 | `3B8BF5EAD27337F829FA4D474F0363324124A9696D33FE1AEE7B01262EFF5BD1` |
| `ccBGSSSE001-Fish.esm` | 1203374 | `F30A9C18C3E375E002CC26E5DD3CDF72A615D574738581FBA2BFD58215024FE7` |
| `ccQDRSSE001-SurvivalMode.esl` | 237674 | `109246CD704CE7765BEA99ED2B9B1800EEB069D46C7A308B8BA5DB2DEF4AF77B` |
| `ccBGSSSE037-Curios.esl` | 37476 | `F5A970ADA5CF32F3088F01BCCDAD0A6BE69A5B43E5E325AD4CBD7C6D1A15F4D3` |
| `ccBGSSSE025-AdvDSGS.esm` | 614669 | `68D8DDCBABD6EF491B175838B25D6E2870DF3D2FAFFDB535491740269E9335DC` |
| `_ResourcePack.esl` | 78418 | `D231915A4BBFE6E89536DFB0A46C5ADC8E4D2D23CE95D248DC24943776EB76FB` |

The exact selected plugin headers were independently inspected with the
project-authored TES4 reader. USSEP `4.3.3` requires all ten masters above;
AI Overhaul additionally requires USSEP and Fishing; Children of the Pariah
requires the five base masters. Candlehearth requires Skyrim, Update, and
Dragonborn; Nightgate requires the five base masters; its control patch
requires Skyrim plus the two selected source plugins.

### 5.2 `REAL-NPC-0001-POS`

Required closure:

- every official master referenced by the selected plugin closure;
- the exact official-master hashes already recorded by RESEARCH-0035,
  including `Skyrim.esm`, `Update.esm`, `Dawnguard.esm`, `HearthFires.esm`,
  `Dragonborn.esm`, and Fishing;
- exact USSEP `4.3.3` archive/plugin;
- AI Overhaul `1.8.6` archive/plugin;
- Children of the Pariah `1.2.3.6` archive with only `00 - Universal` and
  `01 - Closed Mouths` selected for the positive;
- selected records `0001339A:Skyrim.esm` and `0001AA63:Skyrim.esm`;
- selected loose NIF/DDS members and hashes;
- archive participation excluded for the controlled FaceGen-provider input;
  and
- exact retained author-purpose passages/revisions used by the case.

The Wave F closure table above now pins every Creation Club/resource master
named by the selected USSEP/AI Overhaul headers. The executable private
manifest must copy those exact identities and fail closed on any mismatch.

Independent truth:

- [`eval-0016-independent-byte-map.json`](../../research/investigations/artifacts/RESEARCH-0035/eval-0016-independent-byte-map.json);
- manually reviewed master-index translation;
- the RESEARCH-0034 pre-resolved loose FaceGen contract; and
- the RESEARCH-0035 package-specific claim boundary.

Expected positive package relations:

- `0001339A:Skyrim.esm`: AI Overhaul raw `PKID A6561007` is absent from the
  later appearance winner;
- `0001AA63:Skyrim.esm`: AI Overhaul raw `PKID 1AF40A07` and `1E220506` are
  absent from the later appearance winner.

The raw values are fixture evidence; production reasoning must compare
canonical FormKeys after master translation.

### 5.3 `REAL-NPC-0001-CTRL`

The control adds only the selected `CotP - AI Overhaul Patch` installer member
and exact patch plugin.

Expected control:

- raw `A6561004` resolves to the same canonical package as source raw
  `A6561007`;
- raw `1AF40A04` and `1E220503` resolve to the selected canonical package
  identities from the source chain;
- selected appearance data remains preserved; and
- no package-reversion finding exists for those selected relations.

The patch does not forward every `AIDT` byte changed by AI Overhaul. The oracle
must not declare the entire interaction fixed, suppress an independently
qualified `AIDT` result, or use the patch name as compatibility authority.

### 5.4 `REAL-REFR-0001-POS`

Required closure:

- every official master referenced by the selected plugin closure;
- exact `Skyrim.esm` already recorded by RESEARCH-0035;
- Candlehearth `1.1.1` archive/plugin;
- Nightgate Inn Revived `1.3` archive/plugin;
- `00017061:Skyrim.esm`; and
- exact retained author-purpose passages/revisions.

The Wave F closure table above pins the applicable `Update.esm`,
`Dawnguard.esm`, `HearthFires.esm`, and `Dragonborn.esm` dependencies from the
selected plugin headers. The executable private manifest must copy the five
base-master identities and fail closed on any mismatch.

Independent truth is
[`eval-0017-independent-byte-map.json`](../../research/investigations/artifacts/RESEARCH-0035/eval-0017-independent-byte-map.json)
plus manual master-list and subrecord review.

Expected values:

| Source | `XLKR` raw | `DATA` raw |
|---|---|---|
| Skyrim | `000000006F8C0300` | `4970AC4440B276C400000000000000000000000000000000` |
| Candlehearth | `000000009B7A0200` | `0080AC44008076C400000000000000000000000000000000` |
| Nightgate | `000000006F8C0300` | `93083AC48BCD61C48069083F000000000000000000000000` |

The oracle supports the structural-reversion conclusion and only a likely,
unobserved rental-association symptom.

### 5.5 `REAL-REFR-0001-CTRL`

The control adds the exact author patch archive/plugin. Expected patch values:

- `XLKR`: `000000009B7A0200`;
- `DATA`: `93083AC48BCD61C48069083F000000000000000000000000`.

The control proves only that this patch combines the selected relation and
placement. It does not establish observed gameplay, quest safety, or universal
patch correctness.

### 5.6 Replay and redistribution

All four controlled-real variants expect `complete-clean` replay only while
every exact private byte/source dependency and supported implementation
version is retained. Otherwise they become `audit-only` or `unavailable`
according to what remains; no automatic reacquisition or version substitution
occurs.

Tracked/redistributable material is limited to project-authored manifests,
hashes, public IDs/links, raw structural expectations, source citations where
permitted, claim boundaries, and evaluation output that does not embed
third-party bytes.

## 6. Candidate-selection packages

### 6.1 `CAND-ATOMIC-DEV`

The truth manifest must contain at least one typed relationship or explicit
unsupported/gap example and an eligible population for each stratum below.
Every positively supported M1 rule additionally requires a positive, matched
negative, and malformed/unsupported example:

- record stale-value/relation reversion;
- placed-reference/link topology;
- typed asset reference;
- script/API/VMAD relationship lead;
- named configuration relationship;
- generated-output dependency;
- native component relationship;
- patch effect/applicability;
- applicable documentation relationship; and
- cross-layer interaction.

Every truth entry records canonical participants, causal path, required lane,
dependencies, taxonomy strata, expected candidate/disposition/gap, and whether
the fixture independently establishes correctness or only construction smoke.
PEX/VMAD, named configuration, generated-output, native, and typed-asset strata
that lack a bounded M1 semantic analyzer must remain lead-only or
unsupported/gap populations; their inclusion does not create semantic support
or finding authority.

### 6.2 Integration, scale, and stress

`CAND-INTEGRATION-VAL` combines supported strata with broad distractors,
duplicate paths, ambiguous intent, renamed participants, unrelated insertion,
and relevant-winner changes.

`CAND-SCALE-VAL` and `CAND-STRESS-VAL` are deterministic project-authored
generators with pinned generator version, seed, population counts, planted
truth, and expected structural hashes. They measure candidate recall/volume and
operational cost without becoming a semantic oracle for real profiles.

The upper-bound structural stress package may use millions of file/index rows,
but its exact size and M1 performance bounds must be selected in the accepted
M1 plan from measured feasibility. It must not silently convert the creator's
private profile into the oracle.

### 6.3 Required oracle populations

For every rule/lane/configuration/taxonomy stratum:

- eligible positive population;
- mandatory/deterministic lane population;
- matched-negative population and expected resolution/escalation;
- unsupported/gap population;
- canonical events, bundles, and participant pairs;
- expected invalidation dependencies;
- expected unprocessed work under each test limit; and
- prohibited all-pairs or whole-profile-model behavior.

The model/candidate path receives only input indexes/relationships and
configuration. The harness computes recall and volume afterward.

## 7. MO2 effective-state packages

### 7.1 Disposable environment

All EVAL-0051 fixture roots must be disposable, outside any live user setup,
and pinned to MO2 `2.5.2`. The manifest records:

- MO2 executable/package fingerprint and canonical configuration source;
- instance and profile identities;
- `modlist.txt`, `plugins.txt`, `loadorder.txt`, `meta.ini`, and other
  positively allowlisted configuration inputs;
- physical Data, mod, overwrite, secondary-root, hidden/skipped, unmanaged,
  and supported mapper trees;
- canonical path/comparator/normalization contract;
- process/quiescence state; and
- before/after protected-root manifests for the separate non-mutation gate.

### 7.2 Required atomic matrix

`MO2-ATOMIC-DEV` includes:

1. valid explicit profile with differing saved selection;
2. missing/stale/ambiguous saved selection;
3. enabled/disabled mods and plugin-order inputs;
4. two- and three-provider loose-file chains;
5. physical Data/unmanaged winner;
6. overwrite contribution;
7. hidden/deleted/skipped contribution;
8. duplicate/case-normalization collision;
9. renamed mod with unchanged bytes and separate source mapping;
10. exact secondary-Data/additional-mapper inventory; include a positive real
    contribution only when the supported target inventory contains a
    deliberately qualified mapper, otherwise retain the independently
    researched empty inventory;
11. unknown mapper;
12. inaccessible object;
13. changed-during-capture;
14. same-size/time byte mutation; and
15. archive-visible population declared unsupported.

### 7.3 Independent oracle

An independent operator records the disposable instance's MO2 UI/VFS-visible
state and direct physical facts before the production adapter runs. The oracle
separates:

- MO2-visible order/winner state;
- direct physical state;
- source-mapping evidence;
- unsupported/ambiguous state; and
- saved-selection suggestion provenance.

`MO2-INTEGRATION-VAL` combines the atomic states in a small profile.
`MO2-NEGATIVE-VAL` contains malformed, ambiguous, unknown-mapper, running-MO2,
and drift cases. `MO2-HO-001` must be independently constructed and sealed.

### 7.4 Replay/distribution

Project-authored profile trees/configuration can be tracked. MO2 remains a
user-installed test dependency and is not bundled. Real profile data is
private and supplemental; `Brain Blast Destruction 2024` is prohibited as a
correctness oracle.

## 8. Bethesda semantic packages

### 8.1 Synthetic binary matrices

`BETH-NPC-DEV` covers:

- full and light origin identities;
- TES4 masters and ESL flag;
- compressed and uncompressed `NPC_`;
- winning/deleted/templated states;
- configuration flags/template flags and the template link where present;
- `RNAM`, `AIDT`, repeated `PKID`, repeated `PNAM`, and `HCLF`;
- the resolved `RACE` relationship and `FaceGenHead` flag used by the bounded
  FaceGen-applicability decision;
- override order and canonical FormKey/master translation; and
- unknown/unresolved links.

`BETH-REFR-DEV` covers:

- compressed and uncompressed `REFR`;
- `NAME`, `XLKR`, `XLRL`, `XOWN`, and `DATA`;
- canonical relation targets under differing master indices;
- override order/winner/deleted state; and
- absent/repeated/malformed subrecord boundaries where format-valid.

`BETH-LIGHT-VAL` covers `.esl` and ESL-flagged `.esp` origin/local-ID
boundaries, including valid maximum and out-of-range/invalid fixtures. Its
public answers make it development coverage; `BETH-LIGHT-VAL-002` supplies the
materially independent private validation replacement.

`BETH-MALFORMED-VAL` covers truncated records, invalid sizes, decompression
limits, pathological nesting/counts, invalid master references, and
changed-during-read input.

`BETH-UNSUPPORTED-VAL` includes at least one unallowlisted record family,
unallowlisted field/shape, localized-string dependency, archive member, and
typical-environment discovery request. Its public answers make it development
coverage; `BETH-UNSUPPORTED-VAL-002` supplies independent private validation
with independently constructed representatives and controls.

The current M1 Bethesda matrix does not contain `QUST`, quest-alias,
forced-reference/`ALFR`, or other quest-logic shapes. Those shapes belong to a
future reviewed EVAL-0006/EVAL-0007 package if that planned pair is promoted
beyond M1.

### 8.2 Oracle construction

For each project-authored plugin:

- retain the generator/source description and exact emitted bytes;
- independently hand-audit record headers, group structure, offsets, lengths,
  flags, FormIDs, masters, subrecords, field decoding, canonical FormKeys,
  links, override chain, and winner;
- optionally check structural invariants with a small non-Mutagen reader; and
- prohibit Mutagen and xEdit from creating or approving the expected values.

The controlled-real raw maps from RESEARCH-0035 are validation projections,
not redistributed plugin fixtures. `BETH-HO-002` must be independently authored
and sealed after the positive allowlist is fixed.

### 8.3 Replay/distribution

Project-authored binary fixtures are tracked under a compatible license.
Official/mod bytes remain private. Replay pins Mutagen `0.54.2`, source commit
`282bb99a77b2df7f1b092b06270e8e3c8fb55463`, locked transitive dependencies,
plugin bytes/order, runtime-support manifest, and analyzer schema.

## 9. Supported-target package

`TARGET-1170-PRIVATE-VAL` references an evaluator-supplied exact Steam
`SkyrimSE.exe` with:

- length `37,157,144`;
- SHA-256
  `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`;
- AMD64 PE32+ GUI;
- fixed file/product version `1.6.1170.0`;
- Steam App ID `489830`; and
- exact support-manifest version.

The negative package includes:

1. one-byte mutation with retained apparent version;
2. same-version synthetic metadata plus unknown hash;
3. known unsupported channel/runtime manifest;
4. malformed/truncated project-authored PE-like bytes;
5. missing path;
6. access-denied test double or disposable ACL fixture;
7. inconsistent metadata/hash;
8. changed-during-capture bytes;
9. non-MO2 manager declaration; and
10. non-Windows/non-x64 platform declaration.

Only project-authored negative bytes/metadata are redistributable. The exact
game executable remains private and is never committed or packaged.

## 10. Analyzer-contract and evidence packages

### 10.1 `ANALYZER-CONTRACT-DEV`

The declaration oracle requires every field listed by ANALYSIS-016 and the
companion EVAL-0065 specification. Boundary variants:

- missing dependency;
- disabled analyzer;
- unsupported taxonomy area;
- invalid analyzer/ruleset version;
- local-only operation with no provider;
- declared optional LLM path disabled; and
- single-analyzer execution with upstream substrate only.

The input declaration must not be generated from observed fixture results.
CLI and JSON outputs are compared for semantic equality.

### 10.2 Evidence packages

`EVID-TYPES-DEV` supplies one valid example and one empty-set example of each
typed domain object.

`EVID-LLM-VAL` supplies a project-authored inert OpenAI-like direct Response
transcript containing:

- capability/model/request/prompt/schema metadata;
- one schema-valid hypothesis proposal;
- one invalid citation proposal; and
- supporting and contradicting typed evidence.

One separately marked unsupported hosted-search item is included only to prove
that it cannot be admitted through the disabled M1 capability. It is not an
enabled search operation, acquired claim source, or support claim. Only a
separate project-authored local/fixture document revision and exact passage may
support an external claim in M1.

`EVID-NO-LLM-VAL` produces the corresponding deterministic result and explicitly
records no LLM involvement. `EVID-HOSTILE-VAL` embeds instruction-like text in
documentation/search/tool output and expects it to remain untrusted data.

All provider IDs, content, and usage are marked synthetic. These packages do
not call a network or use credentials.

### 10.3 Live source-claim extraction package

`LLM-CLAIM-LIVE-VAL` is a project-authored validation package for the accepted
source-claim extraction contract. It contains:

- one exact retained document revision with independently adjudicated positive,
  negative, conditional, version-scoped, and unsupported statements;
- stable passage offsets and source/applicability identities that are absent
  from the model-facing text;
- inert instruction-like content that must remain untrusted data;
- an operation-specific strict schema for cited claim proposals,
  applicability conditions, contradictions, abstentions, and unsupported
  items; and
- an oracle authored without using the model response.

After the provider qualification gate passes, M1 issues one direct,
synchronous, non-streaming
`gpt-5.6-sol`/medium/`store: false`/`service_tier: "default"` request for
this operation. It has its own explicit authorization, finite
input/output/call/elapsed-time/dollar bounds, reservation, response admission, usage
reconciliation, and secret-canary review. Passing requires exact cited passage
bindings, correct typed claim/applicability proposals within the accepted
tolerance, explicit abstention where evidence is insufficient, no invented
local state or source authority, and complete retained provenance. The raw
model output remains a proposal until host validation admits it.

### 10.4 Live evidence-bound candidate-investigation package

`LLM-INVESTIGATE-LIVE-VAL` is a project-authored validation package for the
accepted evidence-bound candidate-investigation contract. It supplies, in one
bounded operation:

- a typed positive candidate with only the local observations, admitted source
  claims, applicability links, contradictions, and declared gaps needed for
  the task;
- a materially matched negative whose local structural shape is similar but
  whose applicable intent evidence changes the answer;
- canonical evidence identities that the model must cite rather than
  regenerate;
- an operation-specific strict schema for hypotheses, supporting and
  contradicting evidence references, unresolved questions, proposed
  consequence/symptom bounds, and abstention; and
- an independently authored oracle that scores typed semantics rather than
  prose.

After the qualification gate and the live claim-extraction operation pass, M1
issues one separately authorized direct synchronous
`gpt-5.6-sol`/medium/`store: false`/`service_tier: "default"` request for
this operation. It receives no
fixture answer labels, private oracle data, credentials, or authority to read
local state. Passing requires an evidence-bound harmful hypothesis for the
positive, rejection or abstention for the matched negative, no fabricated
facts/citations, correct uncertainty and gaps, and complete retained
provenance. Host rules—not the model—decide whether proposals satisfy finding
admission and case grouping.

The provider qualification request, live claim-extraction request, and live
candidate-investigation request are three distinct billable operations. A
canned transcript may test replay and failure handling but cannot replace
either live semantic operation.

## 11. End-to-end provenance packages

The pre-registered provenance DAG for each package includes exact required and
forbidden edges.

### 11.1 `PROV-LOCAL-DEV`

One deterministic finding/case linked through:

```text
fixture bytes
  -> installation snapshot and assurance
  -> analysis context + effective scan configuration
  -> analysis run + resolved manifest
  -> parser/analyzer versions
  -> observation -> candidate -> hypothesis -> finding -> case/recommendation
  -> coverage and taxonomy assignments
```

Provider/source nodes must be explicitly `not-used`, not absent ambiguously.

### 11.2 `PROV-SOURCE-LLM-VAL`

Adds:

```text
project-authored local/fixture source fingerprint
  -> acquisition run -> document revision -> exact passage -> external claim
  -> application link
synthetic direct OpenAI capability/model/request/response
  -> proposal validation/admission
```

Nexus interface routing and hosted-search provenance are explicitly `not-used`
for M1. A future extension must add their exact interface/spec/schema/query/
fingerprint and search-action/source nodes, while keeping search discovery
separate from landing acquisition and source authority, before those
boundaries are enabled.

`PROV-LIVE-COMPOSED-VAL` does not issue an additional provider request. After
the platform/provider specification opens dispatch, it binds the exact
retained access-profile generation, requests, responses, usage/cost ownership,
authorizations, schema admissions, and downstream application edges from
`LLM-CLAIM-LIVE-VAL` and `LLM-INVESTIGATE-LIVE-VAL` and compares them with this
case's provenance contract. Credentials and secret-bearing helper inputs
remain outside the DAG and oracle. It also references the separate
qualification call jointly owned by the
[platform live-provider fixture families](m1-platform-fixture-manifests.md):
`M1-PLAT-PROVIDER-CAPABILITY-v1`,
`M1-PLAT-PROVIDER-AUTHORITY-v1`, `M1-PLAT-BUDGET-v1`, and
`M1-PLAT-CREDENTIAL-v1`. It does not create a fourth provider request.

### 11.3 Contradiction and deletion

`PROV-CONTRADICTION-VAL` requires both supporting and contradicting evidence
and the reason neither was silently discarded.

`PROV-DELETION-VAL` removes one source body/payload after configured dependent
work and requires:

- immutable historical output;
- retained permitted fingerprint/reference and deletion receipt;
- changed replay/audit disclosure;
- no inspectable-passage claim after deletion; and
- no new derivation authorized from the missing evidence.

Project-authored synthetic source/provider data is tracked. Any later private
variant follows source-specific retention and non-redistribution.

## 12. Case-grouping packages

`CASE-SHARED-DEV` contains at least three findings:

- two record/asset findings with the same typed stale-reversion cause,
  applicability, and resolution; and
- one superficially similar finding with a distinct cause.

Expected grouping is one two-member supported case plus one distinct supported
case.

`CASE-DISTINCT-VAL` supplies:

- same mod, different causal conditions;
- same record family, different applicability/dependencies;
- shared taxonomy codes, different causes; and
- similar titles/symptoms, different causes.

Each remains separate.

`CASE-LEAD-VAL` supplies a candidate/hypothesis below finding threshold and
expects one lead-only case, zero supported cases, and no readiness effect.

`CASE-METAMORPH-VAL` renames/reorders participants and randomizes candidate
enumeration; membership must remain identical. The oracle contains typed
causal conditions and expected membership but no model-visible case labels.

## 13. Coverage/readiness packages

The fixture population ledger is the independent oracle.

### 13.1 `COVER-MATRIX-DEV`

Include distinct populations for:

- plugins parsed;
- loose-provider paths indexed;
- supported record families/fields analyzed;
- enabled analyzers;
- source entities requested/acquired/applied; and
- taxonomy-classified eligible subjects.

Across those populations, include completed, completed-with-gaps, failed,
skipped-by-configuration, skipped-by-limit, and unsupported members. Each
member has one exact reason and denominator membership.

### 13.2 Boundary variants

- `COVER-ZERO-FINDING-VAL`: zero findings, nonzero unsupported/gap population.
- `COVER-PARTIAL-VAL`: one failed analyzer, one limited analyzer, and retained
  reportable work; readiness must be incomplete/provisional.
- `COVER-TARGETED-VAL`: one targeted analyzer with valid narrow scope and no
  permission to borrow prior full-scan coverage.
- Lead-only variant: one lead-only case and zero supported cases/findings.

Expected output prohibits a single combined analyzed/safety percentage and any
unqualified “safe” or guaranteed-readiness phrase.

## 14. Taxonomy packages

All product taxonomy fixtures pin
`infinium.skyrim-se.mod-impact-taxonomy/0.1.0`.

### 14.1 Required semantic matrix

Every assignment below also records exact subject, evidence, applicability
conditions, classifier, reason, and separate confidence reference when one
exists.

| Fixture subject | Exact expected `0.1.0` assignment or state |
|---|---|
| `TAX-01` | Declared `purpose.replace-overhaul` plus `purpose-target.actors.appearance-identity`; observed `surface.plugin-data`; established `area.actors.ai-packages`; predicted `consequence.incorrect-functional-behavior`. The declared appearance target does not overwrite the independently observed AI area. |
| `TAX-02` | Declared `purpose.replace-overhaul` and `purpose-target.presentation.visual`; observed `surface.plugin-data`, `surface.asset`, `surface.asset.texture-material`, `delivery.plugin-container`, and `delivery.loose-data-file`; established `area.presentation.visual`. Consequence and finding extent are `not-applicable` because the subject is a non-problematic effective contribution. |
| `TAX-03A` | Observed `surface.plugin-data` and established `area.actors.ai-packages`. |
| `TAX-03B` | Observed `surface.plugin-data` and established `area.world.placed-objects-activation`. Shared surface does not copy the affected area. |
| `TAX-04` | Observed `surface.plugin-data` and `surface.logic.compiled-papyrus`; predicted `area.quests.progression-objectives-aliases` plus `area.interface-controls`; predicted `consequence.content-feature-unavailable`; predicted `extent.propagation.cross-feature`. Each area requires separate evidence. |
| `TAX-05` | Purpose axis `unknown`; observed `surface.plugin-data`; all unsupported area/consequence/extent axes retain their independently adjudicated states rather than inheriting purpose. |
| `TAX-06` | Observed `surface.plugin-data` and `delivery.plugin-container`; affected-area axis `unsupported`; consequence and extent remain `unknown` or `not-applicable` exactly as independently pre-registered. |
| `TAX-07` | Observed `surface.logic.native-runtime` and `delivery.game-root-component`; affected-area axis `unmapped` for an independently evidenced physics-system concept not adequately represented by `0.1.0`. It is not forced into `area.gameplay`. |
| `TAX-08` | A raw provider/winner topology observation has purpose, affected-area, consequence, and effect-extent axes `not-applicable`; provider topology is not converted into a taxonomy code. An observed delivery/surface assignment is present only if the fixture separately establishes the content's qualified technical surface. |
| `TAX-09A` | Predicted `consequence.incorrect-functional-behavior` with separate Minor severity. |
| `TAX-09B` | Predicted `consequence.incorrect-functional-behavior` with separate Major severity. The identical consequence code does not equalize severity. |
| `TAX-10` | Predicted `extent.subject.single-instance`, `extent.spatial.single-reference-or-point`, `extent.persistence.installation-persistent`, and `extent.propagation.cross-system`; the propagation facet does not widen the direct-subject or spatial facets. |
| `TAX-11` | Non-authoritative hosting label says visual; applicable author evidence declares `purpose.provide-runtime-framework` and `purpose-target.mod-runtime-framework`; local evidence observes `surface.logic.native-runtime`, `surface.runtime-support-data`, and `area.runtime-session.mod-framework-services`. |
| `TAX-12A` | One `NPC_` subject has observed `surface.plugin-data` and established `area.actors.appearance-identity`. |
| `TAX-12B` | Another `NPC_` subject has observed `surface.plugin-data` and established `area.actors.ai-packages`. Record-family equality does not copy area. |
| `TAX-13` | Historical assignment retains taxonomy ID `infinium.skyrim-se.mod-impact-taxonomy`, version `0.1.0`, original role/evidence/reason, and original rendering after a current-view request. |
| `TAX-14` | Product `0.1.0` raw evidence/assignments remain unchanged; linked split/merge mechanics use only the explicitly non-product test-taxonomy pair in section 14.2. |

Every oracle entry pins:

- subject type/ID;
- axis and facet;
- exact code or non-assigned applicability state;
- classification role;
- evidence and applicability conditions;
- reason and separate confidence reference where applicable; and
- which plausible but unsupported assignments must be absent.

### 14.2 Historical/split-merge mechanics

Because no successor to product taxonomy `0.1.0` is accepted, `TAX-HISTORY-VAL`
uses:

- real product `0.1.0` only to assert original-version retention; and
- project-authored non-product schemas
  `infinium.test.taxonomy/1.0.0` and
  `infinium.test.taxonomy/2.0.0` to exercise split/merge mapping mechanics.

The test taxonomy is labeled non-authoritative in every artifact and can prove
only persistence/mapping mechanics. It cannot alter product classifications or
stand in for future Skyrim taxonomy acceptance.

### 14.3 Controlled-real projections

EVAL-0016/0017 may supply validation subjects for actor/AI/FaceGen and
world/placed-reference regions. They do not replace the broader synthetic
matrix and do not make those two regions the product taxonomy.

## 15. Redistribution and privacy matrix

| Material | Private retention | Repository tracking | External redistribution |
|---|---|---|---|
| Project-authored synthetic bytes/text/generators | Yes | Development material in Infinium; private validation/held-out material only in the separate private Git store | Yes under selected project-compatible terms when disclosure does not invalidate evaluation |
| Oracle manifests | Yes | Development oracles in Infinium; validation/held-out oracles only in the separate private Git store | Not in ordinary product/export artifacts |
| Third-party mod archives/plugins/assets | Evaluator-private when permitted | Hashes/manifests only | No without affirmative permission |
| Official Skyrim files/executable | Evaluator-private | Hashes/manifests only | No |
| MO2 application binaries | User-installed dependency | Version/fingerprint/instructions only | Not bundled by Infinium |
| Mutagen package | Per accepted dependency/distribution decision | Lock/SBOM/source identity as applicable | Only under satisfied license obligations |
| Author/source passages | Private acquisition artifact under source policy | Permitted citations/fingerprints or approved retained samples only | Source-specific; private retention is not redistribution permission |
| Creator profile or absolute paths | Private | Never | Never by default |
| Credentials/account data | Not a fixture input | Never | Never |
| Project-authored inert provider/search transcripts | Yes | Yes, clearly synthetic | Yes under project-compatible terms |
| Run-owned CLI/JSON/developer traces | Local and sensitivity-labeled | Only purpose-built synthetic evaluation output | Not automatically externally shareable |

## 16. Replay expectations

| Package family | Expected clean replay | Required boundary replay inputs | Expected gap when missing |
|---|---|---|---|
| Synthetic semantic/Bethesda/candidate/taxonomy | Complete | Generator/source bytes, seed, exact tool/analyzer versions | Unavailable clean recomputation; retained-output audit may remain |
| Controlled-real | Complete only in private evaluator environment | Exact private hashes/bytes, source revisions, exact analyzer/library versions | Audit-only or unavailable; never silent substitution |
| MO2 disposable | Complete with pinned user-installed MO2 `2.5.2` and fixture tree | Retained authoritative observation and adapter outputs | Boundary replay/audit only if MO2 package unavailable |
| Exact runtime | Complete only with evaluator's exact executable | Retained detector output/support manifest | Audit only; fingerprint does not reconstruct bytes |
| Inert provider/source provenance | Complete | Retained synthetic request/response/source payloads | Explicit missing boundary and downgraded replay |
| Live semantic provider operations | Exact retained-result replay; a new live request is not replay | Exact request/response, accepted model/profile, capability/price snapshots, authorization, usage/settlement, prompt/schema/source/candidate dependencies | Audit-only if the retained response or a required semantic input is unavailable; material provider drift requires requalification |
| Post-deletion provenance | Intentionally not clean-replayable across deleted boundary | Retained downstream output, fingerprint, deletion receipt | Exact declared audit/replay gap |

### 16.1 Independent Slice 3 construction record

`docs/evaluation/fixtures/independent-slice3-evaluator-20260729/` is the
separately isolated validation-package construction record for EVAL-0051 and
EVAL-0054. It contains a public execution-input manifest, oracle,
provenance/replay/redistribution records, target-negative matrix construction
scripts, and protected-root evidence tooling. Exact MO2 and Skyrim bytes remain
evaluator-private.

This record was subsequently executed and reviewed. Its current gate state is:

- EVAL-0054 exact and complete preregistered negative target inputs passed.
- EVAL-0051 direct physical inputs and the explicit-target MO2 UI/VFS oracle
  are retained and passed. RESEARCH-0051 established and the owner accepted an
  empty exact-target additional-mapper inventory, so no invented positive
  mapper is required.
- the evaluator did not inspect production source, tests, adapter output, prior
  evaluation output, or the abandoned implementation archive while authoring
  the oracle.

### 16.2 Slice 3 package non-reuse for Bethesda truth

The retained disposable Slice 3 instance was inspected read-only on
2026-07-30. Its enabled `FixturePlugin.esm` population exists only to exercise
MO2 plugin order and provider precedence:

- the lower-priority copy is exact official `Update.esm` bytes;
- the winning copy is the same official file with only its final byte XORed by
  `0x01`; and
- the disabled `FixtureDisabled.esm` is exact official `Dawnguard.esm` bytes.

That instance contains no project-authored bounded Bethesda binary generator,
hand-audited raw-offset semantic oracle, light/malformed/unsupported matrix, or
sealed holdout. Its official broad plugins and arbitrary final-byte mutation
cannot serve as `BETH-*` correctness fixtures and must not be relabeled as
such. Slice 3.5 constructs and independently accepts the required packages
without deriving expectations from this MO2 fixture or from Mutagen. The
accepted
[Slice 3.5 execution plan](../../plans/slices/M1-slice-3.5-bethesda-fixture-qualification.md)
defines the construction, role-isolation, snapshot-integration, verification,
and acceptance workflow.

## 17. Preconditions before executable fixture-package acceptance or execution

The owner may accept this manifest specification as the M1 fixture design
before the described packages exist. Before any package may itself be accepted
as an executable fixture or executed as an M1 gate:

1. assign fixture, oracle, reviewer, and holdout-custodian owners;
2. create and independently review every required synthetic input/oracle;
3. seal held-out package and oracle fingerprints before use;
4. verify all controlled-real private dependencies against the RESEARCH-0035
   manifest without committing payloads;
5. verify the recorded plan/specification reconciliation: EVAL-0051 uses the
   accepted exact-target additional-mapper inventory, while M1 EVAL-0052
   excludes `QUST`/forced-alias semantics and follows the NPC/RACE/REFR
   allowlist;
6. record exact taxonomy assignments and independent evidence;
7. validate privacy, license, retention, and redistribution fields;
8. validate all relative links and unique fixture/EVAL identifiers; and
9. record that production comparison remains pending until the applicable
   implementation slice.

Acceptance of this manifest specification approves the test design only. It
does not declare any package executable, accept an implementation, qualify a
dependency surface, or pass any EVAL case.
