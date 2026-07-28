# RESEARCH-0035 — Gate C controlled-real case qualification

Status: Completed — recommendation accepted; RQ-025 Gate C prerequisite
satisfied
Date: 2026-07-28
Last reviewed: 2026-07-28
Researcher: Codex agent
Accepted: 2026-07-28
Accepted by: Project owner
Primary RQ: RQ-025
M0 wave: C — Gate C closeout
Question: RQ-025 Gate C closeout
Decision enabled: admit exact EVAL-0016 and EVAL-0017 controlled-real
candidates to Wave F case specification

## 1. Question and result

RQ-025 required two exact, locally reproducible, independently grounded
controlled-real candidates:

- EVAL-0016 for the first cross-layer scope-incongruent reversion proof; and
- EVAL-0017 for a materially different category proving the same generic
  stale-structural-relation mechanism outside the first category.

Both candidates are now pinned:

| Evaluation | Selected candidate | Positive mechanism | Matched control |
|---|---|---|---|
| EVAL-0016 | AI Overhaul `1.8.6` + Children of the Pariah `1.2.3.6` | appearance winner omits selected AI Overhaul package relations while supplying different appearance fields and loose FaceGen | author-supplied CotP AI Overhaul patch demonstrably restores the selected packages |
| EVAL-0017 | Candlehearth `1.1.1` + Nightgate Inn Revived `1.3` | later visual placement override moves one player bed while restoring Candlehearth's changed linked-reference relation to vanilla | author-supplied Nightgate/Candlehearth patch combines Candlehearth's link with Nightgate's placement |

These are **qualified candidates**, not passed product evaluations. Wave F
subsequently supplied and the owner accepted their case specifications,
manifest designs, and acceptance baseline. Actual fixture construction and
conformance execution remain M1 work.

## 2. Method and authority

The qualification used:

- exact retained source archives and archive-member hashes;
- current Nexus v3 `3.0.0` file/version identity, with v1 used only to
  corroborate current file-list visibility/category;
- retained author descriptions and author-supplied patch membership;
- a project-authored minimal TES4/GRUP reader with no Mutagen or xEdit
  dependency;
- raw record offsets, lengths, flags, subrecord bytes, and master lists;
- independent master-index translation review;
- the accepted loose-only FaceGen qualification from
  [RESEARCH-0034](RESEARCH-0034-loose-facegen-qualification.md); and
- positive, matched-control, unsupported, and claim-boundary review under the
  accepted [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md).

The private reference profile supplied discovery inputs only. A generic
discovery scan examined 2,408 locally present plugins for `REFR` cases where a
patch combined one source's `XESP`/`XLKR` relation with another source's
`DATA`. It returned nine leads and one bounded parse failure. Candidate
selection then used independent byte and author-intent evidence; profile
frequency or mod popularity did not determine correctness.

## 3. EVAL-0016 — `REAL-NPC-0001`

### 3.1 Exact source closure

The selected source and member identities are recorded in
[`gate-c-case-manifests.json`](artifacts/RESEARCH-0035/gate-c-case-manifests.json).
The previous installed-versus-archive USSEP ambiguity is closed by using the
exact `4.3.3` archive member:

- archive SHA-256
  `57465E9609359BC047412A7D387FFFAE499263C0BBAC915921D95F23391DDC6F`;
- plugin SHA-256
  `C33F42E503E1C3908BFB0F241778D5D7A5114599A07B1B6E0773F0828C6E1876`.

The distinct transformed installed USSEP copy is excluded. The official
Fishing master required by AI Overhaul is pinned at SHA-256
`F30A9C18C3E375E002CC26E5DD3CDF72A615D574738581FBA2BFD58215024FE7`.

The selected Children of the Pariah installer closure is:

1. `00 - Universal`;
2. `01 - Closed Mouths`; and
3. `CotP - AI Overhaul Patch` only for the matched-control profile.

The selected loose NIF/DDS pairs for `0001339A:Skyrim.esm` and
`0001AA63:Skyrim.esm` reconstruct exactly from that archive. The controlled
provider input contains those loose assets and declares archive participation
excluded; it is an analyzer fixture, not a claim about a live game's complete
archive shadow chain.

### 3.2 Independently grounded positive

The raw byte map shows:

- for `0001339A:Skyrim.esm`, AI Overhaul adds package raw FormID
  `A6561007`; the later appearance plugin omits it;
- for `0001AA63:Skyrim.esm`, AI Overhaul adds packages `1AF40A07` and
  `1E220506`; the later appearance plugin omits them;
- Children of the Pariah supplies different `PNAM` head parts, hair color, and
  exact origin-named loose FaceGen pairs; and
- the positive winner therefore combines the appearance mod's declared scope
  with older package topology.

The expected package and appearance values were read from raw subrecords and
recorded with offsets before any future production analyzer exists. Mutagen is
not the oracle.

### 3.3 Matched-control boundary

The author patch restores the selected packages:

- `A6561004` in the patch resolves to the same AI Overhaul-local package as
  `A6561007` in the source plugin;
- `1AF40A04` and `1E220503` likewise resolve to the same selected AI
  Overhaul/USSEP package identities after the patch's different master-index
  mapping; and
- the patch preserves the appearance plugin's selected `PNAM` and color data.

However, the byte map also shows that the patch does **not** forward every
`AIDT` byte changed by AI Overhaul. Therefore:

- it is a matched control for the selected package-reversion conclusion;
- it is not proof that every AI field is resolved;
- a package-specific negative may pass while another independently qualified
  field still produces a finding; and
- patch name or presence cannot suppress unrelated evidence.

This corrects the earlier broader research wording without changing the
generic candidate mechanism.

## 4. EVAL-0017 — `REAL-REFR-0001`

### 4.1 Why this replaces the Ryn placement lead

The earlier Ryn pair adjusted placements but did not demonstrate the selected
structural-reversion mechanism. `REAL-REFR-0001` does.

Candlehearth is author-described as a gameplay overhaul providing extended inn
rentals and safe storage across Skyrim's inns. Nightgate Inn Revived is
author-described as a visual interior/exterior overhaul that does not edit
quests and requires patches for mods changing the same location. Its official
patch archive contains the Candlehearth patch used here.

These purposes are materially different from the first actor
appearance-versus-package category while exercising the same generic pattern:
a later presentation-scoped override restores an earlier structural value.

### 4.2 Independent raw-byte truth

For `00017061:Skyrim.esm`
(`HeljarchenNightGateInnPlayerBedREF`):

| Source | `XLKR` raw bytes | `DATA` placement |
|---|---|---|
| Skyrim | `000000006F8C0300` | vanilla placement |
| Candlehearth | `000000009B7A0200` | Candlehearth placement |
| Nightgate Inn Revived | `000000006F8C0300` | redesigned-interior placement |
| Author patch | `000000009B7A0200` | redesigned-interior placement |

Thus the unpatched later visual override:

- moves the bed to the redesigned interior;
- restores the linked-reference relation to the vanilla value; and
- drops the different relation introduced by the gameplay mod.

The author patch combines Candlehearth's relation with Nightgate's placement.
Raw offsets, lengths, values, master lists, and the exact source hashes are in
[`eval-0017-independent-byte-map.json`](artifacts/RESEARCH-0035/eval-0017-independent-byte-map.json).

### 4.3 Claim boundary and likely symptom

The supported deterministic conclusion is limited to the structural reversion
and the patch's demonstrated merge. Author intent supports a likely symptom:
the moved player bed may lose the structural association used by
Candlehearth's inn-rental behavior.

No runtime test was performed. The case may estimate a localized functional
symptom and propose the author patch, but it may not claim that the symptom was
observed, that a quest is broken, or that the modlist is globally safe.

## 5. Current acquisition and redistribution

Authenticated read-only checks on 2026-07-28 found every selected Nexus file
identity through the latest v3 file/version model:

| Mod/file | v3 version identity | Current category |
|---|---|---|
| AI Overhaul `543984` | `7318624816368` | old version |
| Children of the Pariah `571480` | `7318624843864` | old version |
| USSEP `551952` | `7318624824336` | archived |
| Candlehearth `419795` | `7318624692179` | main |
| Nightgate main `516261` | `7318624788645` | main |
| Nightgate patches `524195` | `7318624796579` | old version |

The project has no affirmative permission to redistribute the archives,
plugins, or assets. They remain evaluator-supplied private inputs. Tracked
artifacts contain only public IDs, source links, hashes, selected installer
choices, raw structural expectations, and claim boundaries.

An unavailable future source creates an acquisition gap or replacement case;
it does not authorize rehosting or accepting merely similar bytes.

## 6. Anti-overfitting disposition

- Synthetic qualification precedes controlled-real execution.
- Real names and IDs occur only in case manifests, expectations, and
  provenance-bearing source evidence.
- The production mechanism remains typed stale-value/relation detection.
- EVAL-0016 and EVAL-0017 use materially different accepted taxonomy regions.
- Each positive has an author-supplied matched control whose effectiveness is
  demonstrated field by field.
- Patch titles are not allowlists.
- Expected values were produced by a non-Mutagen raw reader before product
  implementation.
- Neither private profile frequency nor a known record/EditorID may enter
  production behavior.
- Accepted Wave F manifests require renamed synthetic equivalents and
  materially independent held-out slots before the corresponding
  implementation-conformance claims.

## 7. Residual limits

- The accepted Wave F fixture manifests pin every applicable official master
  currently identified by the cases; execution must verify those private
  dependencies exactly.
- EVAL-0052 still governs production parser support for each consumed field
  and shape.
- The exact likely Nightgate/Candlehearth runtime symptom remains unvalidated.
- Controlled-real payload acquisition is private and cannot provide a
  one-command public corpus.
- These two cases are a minimum generalization pair, not broad Skyrim domain
  coverage.

## 8. Disposition

RQ-025 is resolved for M0:

> EVAL-0016 and EVAL-0017 now have exact, reconstructible, independently
> grounded controlled-real candidates with matched controls, source and member
> hashes, current acquisition identities, claim boundaries, and materially
> different categories.

Together with RESEARCH-0034, this satisfies the remaining Gate C prerequisites.
It does not mark either evaluation passed or authorize production
implementation.
