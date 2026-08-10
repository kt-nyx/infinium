# RESEARCH-0034 — Loose-only FaceGen identity/provider qualification

Status: Completed
Disposition: recommendation accepted; RQ-023 Gate C prerequisite
satisfied
Date: 2026-07-28
Last reviewed: 2026-07-28
Researcher: Codex agent
Accepted: 2026-07-28
Accepted by: Project owner
Primary RQ: RQ-023
M0 wave: C — Gate C closeout
Question: RQ-023 Gate C closeout
Decision enabled: admit the bounded loose-only NPC-to-FaceGen identity and
provider relationship to M1 evaluation planning

## 1. Question and result

The remaining RQ-023 question was whether the convention-derived
NPC-to-FaceGen relationship could be qualified narrowly enough to support the
first record-plus-asset proof without claiming archive parity, rendering, or
format-wide asset correctness.

The result is **yes, at the pre-resolved input boundary**:

- the logical mesh and tint keys use the NPC's origin plugin and local FormID,
  not the winning override plugin or runtime load-order prefix;
- full-origin, `.esl`-origin, and ESL-flagged `.esp`-origin identities use the
  same eight-hex local-ID filename rule;
- loose providers are resolved as an ordered chain independently of the
  winning plugin record;
- exact loose absence is supported only when the captured fixture excludes
  archive participation;
- unresolved applicability, templates, deletion, ambiguous provider order,
  capture drift, normalization collision, and archive dependence remain typed
  gaps or invalid states rather than missing-file findings; and
- asset presence is independent of NIF/DDS structural or visual correctness.

The executed matrix passed 20 of 20 pre-registered cases. Archive-positive
FaceGen support remains conditional and outside this Gate C closeout.

## 2. Governing boundaries

This is intentionally a bounded actor-record/FaceGen convention, not a
cross-category generic mechanism. The category-generalization requirement
therefore applies to the higher-level reversion mechanism evaluated by
EVAL-0016/EVAL-0017, not to forcing this domain analyzer into unrelated asset
categories.

This qualification applies only after another qualified component supplies:

- origin plugin identity and local FormID;
- full versus light origin class;
- winning record identity;
- resolved race and `FaceGenHead` applicability;
- `UseTemplate` and template-`Traits` state;
- deletion state; and
- a snapshot-bound provider list and archive-exclusion statement.

It does not qualify those upstream parsers or reconstruction mechanisms.
EVAL-0051 and EVAL-0052, or reviewed successors, still govern authoritative
MO2 provider input and Bethesda record/field parsing. The result therefore
does not silently turn a research probe into production architecture.

## 3. Independent-input matrix

The project-authored package under
[`artifacts/RESEARCH-0034`](artifacts/RESEARCH-0034/README.md) separates:

1. synthetic inputs;
2. pre-registered expected results; and
3. the decision/provider probe.

The decision function does not receive expected results. Comparison occurs
only after each result is produced. The cases cover:

| Area | Executed coverage |
|---|---|
| Identity | full origin, `.esl` origin, ESL-flagged `.esp` origin, later winning override |
| Provider state | complete chain/winner, shadowing, exact loose absence, partial pair |
| Matched negatives | wrong plugin directory, wrong ID, wrong extension, unrelated files |
| Applicability | unresolved race, no `FaceGenHead`, `UseTemplate`, templated `Traits`, deleted winner |
| Validation | unsafe plugin identity, light-ID overflow, normalization collision, duplicate priority, capture drift |
| Unsupported | archive possible, explicit archive provider, structurally malformed but present NIF/DDS |

Execution environment:

- Node.js `24.11.1`;
- 20 inputs;
- 20 passed;
- 0 failed.

The result file records the winner hashes and complete synthetic provider
chains for the positive cases.

## 4. Real-path corroboration

The private reference installation was used only as corroboration, not as a
fixture or oracle. Hash-pinned observations showed:

- a full-origin Skyrim NPC override whose FaceGen directory remains
  `Skyrim.esm`;
- a light `.esl` with on-disk record FormID `0x02000D61` whose FaceGen filename
  is the local ID `00000D61`; and
- an ESL-flagged `.esp` with on-disk record FormID `0x0500092C` whose FaceGen
  filename is `0000092C`.

Both light examples had TES4 light flag `0x00000200` and matching loose NIF/DDS
pairs under the origin plugin filename. Exact hashes are retained in
`real-path-corroboration.json`. No private absolute path or payload is tracked.

## 5. Supported conclusions

After the applicable upstream input is qualified:

- derive:
  - `meshes/actors/character/facegendata/facegeom/<origin>/<local-id-X8>.nif`;
  - `textures/actors/character/facegendata/facetint/<origin>/<local-id-X8>.dds`;
- compare provider keys case-insensitively with normalized separators;
- preserve original path spelling and the comparison key separately;
- record every known loose provider and the effective loose winner;
- report exact loose absence only from an archive-excluded captured fixture;
- keep mesh and tint results separate, including partial pairs; and
- report malformed present assets separately from completeness.

The relationship does not establish visual correctness, absence of dark face,
runtime use, semantic appropriateness, morph agreement, or global mod
compatibility.

## 6. Remaining non-blocking work

- Archive activation, precedence, and archive-member FaceGen resolution remain
  conditional on EVAL-0051 or an accepted successor.
- Production record parsing and provider acquisition remain subject to their
  M1 case specifications.
- NIF/DDS structural validation remains a separate analyzer result.
- The two private real-path examples should eventually receive
  redistributable synthetic equivalents at the actual plugin-byte layer.

These are disclosed support boundaries, not reasons to keep the narrow
loose-only identity/provider prerequisite open.

## 7. Disposition

RQ-023 is resolved for M0:

> The loose-only NPC-to-FaceGen identity, applicability, provider, shadowing,
> normalization, malformed, and archive-exclusion matrix is qualified at the
> pre-resolved input boundary. Archive-positive support remains conditional.

This closes the RQ-023 portion of Gate C without broadening M1 scope or making
an archive-backed conclusion.
