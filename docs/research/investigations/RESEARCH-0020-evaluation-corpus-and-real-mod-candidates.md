# RESEARCH-0020: Evaluation corpus and real-mod candidates

Status: Completed — recommendation accepted by project owner; candidate
qualification subsequently completed by RESEARCH-0035
Date: 2026-07-25  
Last reviewed: 2026-07-28
Researcher: Codex agent  
Primary question: RQ-025  
Decision enabled: reproducible corpus structure and the candidate-repair and
replacement criteria later completed by RESEARCH-0035

Acceptance note: The project owner accepted the corpus strategy and bounded
candidate disposition on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
RESEARCH-0035 subsequently repaired and qualified the exact EVAL-0016
candidate and selected a materially different EVAL-0017 candidate, resolving
RQ-025 for M0. References below to an incomplete EVAL-0016, an unselected
EVAL-0017, or an open Gate C record the state of this 2026-07-25 investigation
and are superseded by RESEARCH-0035. The qualified candidates have not yet
passed execution; their Wave F specifications and manifests were accepted on
2026-07-28.

## 1. Question and bounded answer

RQ-025 asks:

> Which real mod combinations provide stable, redistributable or locally
> reproducible evaluation cases?

The answer is a **two-layer corpus**, not a committed copy of third-party mod
payloads:

1. **Redistributable synthetic fixtures** should carry acceptance-critical
   parser, override, provider, matched-negative, malformed, unsupported, and
   metamorphic coverage. Their expectations must be specified independently
   of the parser under test.
2. **Locally reproducible controlled-real cases** should test whether the same
   general rules survive real packaging, records, dependencies, assets, and
   author intent. Their manifests may be committed, but third-party payloads
   must not be redistributed without affirmative permission.

Two retained local combinations provide one incomplete first-gate candidate
and one non-gating discovery lead:

- **EVAL-0016 candidate:** AI Overhaul `1.8.6` plus Children of the Pariah archive
  `1.2.3.6`, with the latter archive's author-supplied AI Overhaul patch as the
  matched negative. The unpatched winner preserves the appearance overhaul
  while reverting observable AI Overhaul package and AI-data changes on
  overlapping NPC records; the patch forwards both sides. It cannot become the
  EVAL-0016 gate until the mandatory loose FaceGen provenance closure is
  selected, hashed, independently qualified, and exercised.
- **Placement-reconciliation lead, not EVAL-0017:** Ryn's Standing Stones
  `1.5` plus Ryn's Farms `2.0`, with Ryn's official Standing Stones/Farms
  patch `1.1` as a resolution comparison. The patch adjusts four `REFR`
  placements owned by Ryn's Farms, but current evidence does not show the
  accepted scope-incongruent-reversion/topology mechanism or a qualified
  bounded symptom. EVAL-0017 therefore remains unselected and requires a
  replacement.

These are **research candidates, not qualified evaluation oracles**. Exact
retained source archives and most selected members have been hashed, but the
NPC manifest has an unresolved installed-versus-archive USSEP provenance
difference and no complete FaceGen selection. Current public re-acquisition,
redistribution permission, independent byte-level expectations, and the Ryn
symptom also remain unqualified. If a candidate cannot pass those steps,
replace it; do not add a mod-name, FormID, cell, NPC, or fixture-specific
production rule to make it pass.

The private reference profile was used only to discover retained artifacts and
test corpus feasibility. It is not a gold standard, scale baseline, source of
universal expectations, or fixture to ship.

## 2. Authority, scope, and method

### 2.1 Accepted authority

This report applies:

- the accepted product definition, requirements, workflows, domain model,
  severity/confidence/coverage contract, analysis catalog, and M0 plan;
- [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md):
  supported, user-initiated Nexus access only, with no HTML scraping, crawling,
  authentication bypass, or public corpus built from restricted source
  material;
- [ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md):
  xEdit has no product, development, fixture, comparison, or oracle role;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md):
  a controlled-real run uses an explicit, quiescent MO2 profile and exact
  effective state;
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md):
  the active runtime is pinned and Mutagen `0.54.2` remains a bounded parser
  dependency rather than ground truth;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md):
  every case binds exact bytes and its complete dependency closure;
- the accepted evaluation strategy, case catalog, fixture guidelines, and
  anti-overfitting rules; and
- the [taxonomy dependency map](../taxonomy-dependency-map.md), which prevents
  corpus labels from silently defining the product taxonomy.

This report consumes the completed Wave C surface investigations:

- [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md);
- [RESEARCH-0015](RESEARCH-0015-generated-output-tool-surfaces.md);
- [RESEARCH-0016](RESEARCH-0016-configuration-ecosystem-survey.md);
- [RESEARCH-0017](RESEARCH-0017-compiled-papyrus-analysis-boundary.md);
- [RESEARCH-0018](RESEARCH-0018-asset-reference-completeness.md); and
- [RESEARCH-0019](RESEARCH-0019-semantic-record-family-roadmap.md).

### 2.2 In scope

- corpus layers and admission rules;
- exact locally retained candidate identities;
- acquisition and replacement routes;
- redistribution, privacy, and retention boundaries;
- positive, matched-negative, boundary, unsupported, and adversarial cases;
- independent ground-truth obligations;
- the incomplete EVAL-0016 candidate manifest and EVAL-0017 replacement
  criteria; and
- deliberately varied evidence inputs for RQ-036 without defining taxonomy.

### 2.3 Out of scope

- accepting either candidate as an evaluation oracle;
- editing the case catalog or open-question registry;
- selecting an M1 implementation architecture;
- implementing analyzers or production rules;
- declaring final purpose, surface, affected-area, consequence, symptom, or
  extent categories;
- distributing third-party mod bytes;
- claiming current Nexus file availability without a supported authenticated
  file-metadata check;
- launching Skyrim, MO2, LOOT, an installer, or a helper;
- using xEdit for inspection or ground truth; and
- treating the creator's last real profile as representative of all modlists.

### 2.4 Method and side effects

The investigation:

1. derived required fixture shapes from RESEARCH-0014 through RESEARCH-0019;
2. used the private reference profile only as a read-only discovery source for
   exact retained archives and installed plugin bytes;
3. inspected retained author descriptions and file metadata already present
   locally;
4. read archive member lists and extracted only selected plugin members into
   an operating-system temporary directory;
5. computed SHA-256 and byte lengths for the retained archives and selected
   members, while retaining the separately observed installed USSEP plugin as
   a distinct unresolved provenance variant;
6. used Mutagen `0.54.2` in a disposable read-only probe to count records,
   inspect masters, and identify promising overlap shapes; and
7. separated those parser observations from the independent truth that must
   still be authored and reviewed.

No game, manager, tool, installer, plugin, archive, profile, load order, or
repository file other than this report was changed. No Nexus page was crawled
or scraped, no authentication was performed, and no remote mod payload was
downloaded. Absolute private paths and the profile name are intentionally
omitted.

## 3. Corpus model

### 3.1 Corpus tiers

| Tier | Contents | May be committed? | Acceptance role |
|---|---|---|---|
| S — synthetic | Project-authored minimal plugins, assets, archives, configs, generated-output trees, native-file stubs, and malformed inputs | Yes, after license and provenance review | Primary deterministic conformance and regression gate |
| R — controlled real | Exact third-party archives/plugins acquired lawfully by the evaluator, plus committed manifests and expectations | Manifest and project-authored expectations only unless permission expressly permits bytes | Generalization gate after Tier S passes |
| B — breadth/discovery | Diverse real mod metadata and local structures used to find missing surfaces or taxonomy inputs | Metadata/derived facts only under source policy; payloads remain private | Research input, never a release gate by itself |
| U — unsupported/adversarial | Unknown versions, malformed records, ambiguous provider states, inaccessible dependencies, and deliberately unsupported formats | Synthetic forms may be committed | Abstention, failure isolation, and coverage-gap gate |

Tier R cases must never be the only test of a rule. A production analyzer is
admitted through generic Tier S fixtures first, then challenged with Tier R.

### 3.2 Manifest identity

Every fixture or controlled-real case needs:

- case ID and manifest-schema version;
- acquisition class: project-authored, redistributable third-party, or
  evaluator-supplied private;
- canonical source identifier and source revision/file ID when available;
- source filename, claimed version, byte length, and SHA-256;
- selected archive-member path, byte length, and SHA-256;
- installer choices or an exact project-authored extraction recipe;
- all plugin names and exact order;
- exact active runtime and base-data manifest;
- complete master, archive, asset, config, generated-output, and documentation
  dependency closure used by the assertion;
- expected observation, candidate, finding, coverage, and abstention states;
- ground-truth author, reviewer, method, and evidence;
- permissions and allowed sharing class;
- known limitations and replacement conditions; and
- invalidation keys from ADR-0010.

A title plus semantic version is not enough. When MO2 metadata, archive
filename, Nexus file metadata, and selected bytes disagree, the archive/member
hash is authoritative for the case identity and the discrepancy remains
visible.

### 3.3 Admission gates

A controlled-real candidate becomes an accepted evaluation case only if:

1. every required byte is present and hash-verified;
2. the exact dependency closure is obtainable by the evaluator or already
   retained lawfully;
3. installation choices and plugin order are deterministic;
4. author intent and the proposed issue are documented independently of the
   analyzer result;
5. a matched negative changes only the causal condition or adds the intended
   resolution;
6. raw structural expectations are specified independently of Mutagen;
7. a second reviewer confirms the expected issue and negative;
8. the symptom or impact is bounded and reproducible, or the case is explicitly
   limited to a structural/documented-compatibility conclusion;
9. the generic synthetic case already passes;
10. unsupported and ambiguous variants abstain honestly; and
11. no production rule mentions the mod, author, filename, FormID, EditorID,
    NPC, cell, worldspace, or case ID.

## 4. EVAL-0016 incomplete candidate manifest

### 4.1 Candidate identity

Candidate ID: `REAL-NPC-0001`  
Manifest schema: `research-controlled-real-candidate/1` — report-local,
Proposed, and not a persisted-schema decision  
Evaluation target: EVAL-0016  
Candidate state: **locally retained; reconstruction incomplete; not
qualified; not redistributable**

| Role | Canonical source | Retained source identity | Bytes | SHA-256 |
|---|---|---|---:|---|
| Behavior mod archive | [AI Overhaul SSE, Nexus mod 21654](https://www.nexusmods.com/skyrimspecialedition/mods/21654) | `AI Overhaul 1.8.6 AE-21654-1-8-6-1726786718.zip`; version `1.8.6`; select `AIO Main` | 1,082,060 | `03525F80AD7B62EFB550F6F451C67F7FA9EF560589F496F837292753D477E75D` |
| Behavior plugin | Same archive | `AIO Main\AI Overhaul.esp` | 2,183,246 | `FED6F25FFA2DAC3A7A578ADD18B0FD763E6C48D33732000480BAD9069BEE55D2` |
| Appearance mod archive | [Children of the Pariah, Nexus mod 97981](https://www.nexusmods.com/skyrimspecialedition/mods/97981) | `Children of the Pariah-97981-1-2-3-6-1733959242.7z`; filename identifies version `1.2.3.6` | 179,418,163 | `EAB2FA2B538E224B92C2E1E32E3DDFC7E509E3F883A5E15822A1A7787C35C014` |
| Appearance plugin | Same archive | `Children of the Pariah FOMOD\Data\00 - Universal\Children of the Pariah.esp` | 88,141 | `C60DB49682CA14B4651A75C25B7690C8C833EF010412481E7F60C904318434FF` |
| Resolution plugin | Same Children of the Pariah archive | `Children of the Pariah FOMOD\Data\Patches\CotP - AI Overhaul Patch\CotP - AIO Patch.esp` | 30,319 | `16E49FC6337094B88EA60AA2241FE888D194407859BC5405089F785A83485416` |

The local MO2 metadata displayed `1.2.3.5` for Children of the Pariah while
the retained archive filename identifies `1.2.3.6`. This is exactly why the
case is pinned to archive/member hashes, not the manager's normalized display
version. Current Nexus file ID and downloadability were not checked through an
authenticated supported API and remain an acquisition gate.

### 4.2 Dependency closure and order

Required official data:

- `Skyrim.esm`;
- `Update.esm`;
- `Dawnguard.esm`;
- `HearthFires.esm`;
- `Dragonborn.esm`; and
- `ccBGSSSE001-Fish.esm`, because this exact AI Overhaul plugin declares it.

The five base masters must match the exact hashes in
[RESEARCH-0007](RESEARCH-0007-skyrim-runtime-support-contract.md). The Fishing
master must be added to the case manifest by exact hash during qualification.

Required third-party master and unresolved provenance variant:

| Role | Exact identity | Bytes | SHA-256 | Candidate disposition |
|---|---|---:|---|---|
| Retained USSEP source archive | Version `4.3.3.0`; Nexus mod 266; `1_Unofficial Skyrim Special Edition Patch-266-4-3-3-1728878067.7z` | 135,966,820 | `57465E9609359BC047412A7D387FFFAE499263C0BBAC915921D95F23391DDC6F` | Private source container |
| Exact archive member | `unofficial skyrim special edition patch.esp` from the retained archive | 19,589,391 | `C33F42E503E1C3908BFB0F241778D5D7A5114599A07B1B6E0773F0828C6E1876` | Proposed controlled-case input; semantic observations must be rerun against these bytes |
| Installed plugin observed during discovery | `Unofficial Skyrim Special Edition Patch.esp` from the private live installation | 19,589,317 | `AB07420A872173809211FB8F4EB1C6DA08FA950804D854F4D991EEA485DF69B8` | Distinct transformed or otherwise changed variant; transformation provenance is unknown, so it is not reproducible from the declared archive recipe and is not an accepted case input |

The earlier probe used the installed variant as part of the discovery
environment. The retained archive does not reproduce those bytes. Until the
transformation is identified and independently authorized or the candidate is
rerun against the exact archive member, no manifest may collapse these two
plugins into one identity.

The positive order is:

1. official masters;
2. exact USSEP archive-member bytes listed above;
3. `AI Overhaul.esp`;
4. `Children of the Pariah.esp`;
5. no compatibility patch.

The matched-negative order is identical, with
`CotP - AIO Patch.esp` placed after both source plugins.

No unrelated profile mod may be present in the controlled fixture. EVAL-0016
and M1 are cross-layer gates, so the exact Children of the Pariah installer
choices and all required loose FaceGen mesh/tint providers must be selected,
hashed, ordered, and independently qualified under RESEARCH-0018/0019. The
atomic profile must prove that archives cannot participate.

A record-only subset may be retained as
`REAL-NPC-0001-RECORD-SUBSET` for narrower research. Its omission of FaceGen
must produce an explicit asset-coverage gap, and it cannot pass EVAL-0016 or
the M1 first-proof gate.

### 4.3 Why it remains a plausible incomplete candidate

Retained author material describes AI Overhaul as changing NPC AI and package
behavior. Retained author material for Children of the Pariah describes a
visual Orc NPC overhaul, warns that mods editing the same NPC records are not
strictly compatible, and supplies the AI Overhaul patch.

The disposable Mutagen probe observed:

- 424 NPC records in `AI Overhaul.esp`;
- 52 NPC records in `Children of the Pariah.esp`;
- nine overlapping NPC FormKeys between those two plugins; and
- 47 NPC overrides in the compatibility patch, including all nine overlaps.

Examples suitable for independent qualification include:

- `01339A:Skyrim.esm`: the AI Overhaul override includes package
  `1056A6:AI Overhaul.esp`; the later appearance plugin omits it; the
  compatibility patch restores it while retaining Children of the Pariah head
  parts; and
- `01AA63:Skyrim.esm`: the AI Overhaul override includes package
  `0AF41A:AI Overhaul.esp` and a USSEP package; the later appearance plugin
  omits them; the compatibility patch restores them while retaining the
  appearance fields.

These are research observations from the parser under test, not yet accepted
truth. They show that the candidate has the exact appearance-versus-behavior
shape selected by RESEARCH-0019 without requiring a mod-name-specific rule.
Because discovery used the distinct installed USSEP variant, the observations
must be rerun against the proposed archive-member input before they belong to
the controlled-case manifest.

### 4.4 Independent ground truth

Qualification must create a project-authored expectation document containing:

1. raw plugin group/record/subrecord offsets and lengths for at least two
   selected overlapping NPC records;
2. the exact master-index-to-FormKey translation used by those records;
3. raw expected package links, AI-data values, factions when asserted, and
   appearance/head-part links for each source and patch;
4. the exact winning-record order for positive and negative profiles;
5. an independent assertion that the unpatched winner restores an older
   behavior value while intentionally supplying newer appearance values;
6. an independent assertion that the patch preserves the selected appearance
   values and forwards the selected behavior values;
7. author-source evidence for both mods' intended scopes and the patch's
   purpose; and
8. reviewer sign-off that no expected field was copied from Mutagen output.

An independently written minimal binary reader, reviewed byte map, or another
accepted non-Mutagen method may establish items 1 through 4. xEdit may not.
The author patch corroborates intent but is not by itself an infallible oracle.

### 4.5 Required controls

| Control | Construction | Expected result |
|---|---|---|
| Positive | Exact sources and loose FaceGen closure, appearance plugin after AI Overhaul, no patch | Cross-layer scope-incongruent reversion candidate/finding for independently qualified fields and provider relationships |
| Matched negative | Add exact author patch after both while retaining the same FaceGen providers | No stale-behavior finding for the fields the patch demonstrably forwards; unchanged record-to-FaceGen provenance remains visible |
| Intentional overlap negative | Synthetic counterpart in which later plugin explicitly owns both appearance and behavior | No assumption that every cross-scope overlap is wrong |
| Non-overlap negative | Same plugins, record not present in both source plugins | No candidate |
| Order metamorphic | Reverse the two source plugins while preserving bytes | Candidate shape changes with the actual winner; result may not reuse |
| Patch stale/mismatched | Change one source byte/version while retaining old patch | Patch applicability becomes unknown/stale; never auto-valid |
| Missing master | Remove USSEP or Fishing from the controlled snapshot | Dependency failure/coverage gap, not a semantic finding |
| Lite boundary | Use an exact AI Overhaul Lite variant only after separately pinning its contract | Separate case identity; do not transfer Main expectations |
| Unsupported shape | Exercise an NPC/template field not on the accepted allowlist | Unsupported/gap, not inferred compatibility |
| Malformed record | Project-authored malformed NPC/subrecord fixture | Isolated parse failure with no fabricated finding |

### 4.6 Acquisition, privacy, and redistribution

- Existing retained archives preserve the source candidates, but they do not
  yet make the exact case locally reproducible: USSEP transformation
  provenance, Fishing bytes, exact FOMOD choices, and the mandatory loose
  FaceGen closure remain incomplete.
- Another evaluator must obtain exact files through Nexus's normal user flow
  or a supported, user-initiated API route. A hash mismatch creates a new
  candidate version; it is not "close enough."
- The report does not establish permission to redistribute either archive,
  plugin, patch, assets, or retained description. None should enter Git.
- A committed case manifest may contain mod IDs, filenames, versions, sizes,
  hashes, normalized installer choices, derived structural expectations, and
  source links.
- Absolute paths, account identifiers, profile identity, download tokens, and
  retained full descriptions must remain private.

### 4.7 Replacement rule

Replace `REAL-NPC-0001` if:

- exact files cannot be lawfully re-acquired by a second evaluator;
- current supported API metadata proves the retained version unavailable and
  the project cannot maintain a lawful private replay;
- an exact current-version pair cannot reproduce the same generic shape;
- independent byte review contradicts the probe;
- the archive-member USSEP rerun does not preserve the observed shape;
- exact loose FaceGen providers and archive independence cannot be
  reconstructed;
- the patch does not actually preserve the asserted fields; or
- the case requires a name/FormID exception.

A replacement must still be an appearance-scoped NPC overhaul plus a
behavior-scoped NPC mod, include or document an intended resolution, use a
small exact dependency closure, and pass the same controls. Do not choose a
candidate merely because a patcher recognizes its title.

## 5. Placement-reconciliation lead; EVAL-0017 unselected

### 5.1 Candidate identity

Candidate ID: `REAL-PLACEMENT-LEAD-0001`  
Manifest schema: `research-controlled-real-candidate/1` — report-local,
Proposed, and not a persisted-schema decision  
Evaluation relationship: EVAL-0017 discovery input only; not the selected gate  
Candidate state: **locally reproducible placement lead; not a qualified
EVAL-0017 case; not redistributable**

| Role | Canonical source | Retained source identity | Bytes | SHA-256 |
|---|---|---|---:|---|
| Exterior overhaul A archive | [Ryn's Standing Stones, Nexus mod 64969](https://www.nexusmods.com/skyrimspecialedition/mods/64969) | `Ryn's Standing Stones-64969-1-5-1680321668.rar`; version `1.5` | 1,486,039 | `0F4B96F5D2D089CA7C66CFF3BB2D332BC64E2642367D0F8ED22D8FAFA0A468C8` |
| Exterior overhaul A plugin | Same archive | `Ryn's Standing Stones.esp` | 1,152,324 | `CEAD77EEF8263B3356946E7ACDA84DAFB1F55B30C9425EC22C51C27CE4855050` |
| Exterior overhaul B archive | [Ryn's Farms, Nexus mod 72305](https://www.nexusmods.com/skyrimspecialedition/mods/72305) | `Ryn's Farms-72305-2-0-1684909101.rar`; version `2.0` | 848,642 | `239FF841A926A0FC0D354532C09D73249DE71B914BF469E20E2150E5F01716FC` |
| Exterior overhaul B plugin | Same archive | `Ryn's Farms.esp` | 1,041,104 | `1BBB41DF3B38964DA6613F372FD4DF7EEA8584CDCB54CE12895E35CA9642F5F8` |
| Official resolution archive | [Ryn's Skyrim Official Patch Hub, Nexus mod 73778](https://www.nexusmods.com/skyrimspecialedition/mods/73778) | `Ryn's Standing Stones Ryn's Farms PATCH-73778-1-1-1697489072.rar`; version `1.1` | 131,675 | `F591A06FD6172B6039B073C01395127E79FD7EF892DDC5D130738AEB090FE464` |
| Official resolution plugin | Same archive | `Ryn's_Standing_Stones_Ryn's_Farms_PATCH.esp` | 175,182 | `081F78E885F63288B97805748B3318D553CBCDA12F0452E8BCB21E8165E2859C` |

The installed private copy of `Ryn's Farms.esp` differed from the retained
archive member. The candidate therefore uses the archive member listed above,
not the installed copy. The Standing Stones and patch installed members did
match their retained archive members. This difference reinforces that
controlled-real cases must be reconstructed from exact archive recipes rather
than copied from a large live profile.

Current Nexus file IDs and public downloadability were not checked through an
authenticated supported API and remain qualification gates.

### 5.2 Dependency closure and order

All three plugins declare only:

- `Skyrim.esm`;
- `Update.esm`;
- `Dawnguard.esm`;
- `HearthFires.esm`; and
- `Dragonborn.esm`.

Those masters must match the exact base-data hashes in RESEARCH-0007.

The positive order is:

1. official masters;
2. `Ryn's Standing Stones.esp`;
3. `Ryn's Farms.esp`;
4. no compatibility patch.

The resolution-comparison order adds
`Ryn's_Standing_Stones_Ryn's_Farms_PATCH.esp` after both.

No other profile content may participate. Exact archive assets required to
render the affected exterior must be included when the case asserts a visible
symptom; a record-only run must state that visual validation is absent.

### 5.3 Why it is materially different but not the selected mechanism

This candidate is not another NPC record conflict:

- the author material describes both source mods as exterior/location
  overhauls;
- the main plugins share no NPC or `REFR` FormKeys in the disposable probe;
- the official patch contains four `REFR` overrides of records originating in
  `Ryn's Farms.esp`; and
- those overrides change placement coordinates when Standing Stones is also
  present.

The four observed origin records are:

| FormKey | Base | Patch relation observed |
|---|---|---|
| `000EC2:Ryn's Farms.esp` | `0BB953:Skyrim.esm` | Patch changes placement |
| `000EC6:Ryn's Farms.esp` | `0BB953:Skyrim.esm` | Patch changes placement |
| `000EC7:Ryn's Farms.esp` | `0BB953:Skyrim.esm` | Patch changes placement |
| `000F40:Ryn's Farms.esp` | `0BB952:Skyrim.esm` | Patch changes placement |

This observes `REFR.Base`, `REFR.Placement`, origin identity, containing
exterior context, and an author-maintained compatibility resolution. It is a
real spatial interaction between records that do not directly override each
other, so candidate selection must not rely on same-FormKey all-pairs
comparison.

However, the numeric changes are small, the exact visual defect has not been
independently reproduced, and the pair does not exercise the accepted
scope-incongruent stale-value/topology mechanism selected by RESEARCH-0019.
It therefore is not an EVAL-0017 candidate or matched negative for that
mechanism. It may later qualify as a distinct documented
placement-reconciliation case only after a dedicated analyzer contract,
independent author-source evidence, and controlled symptom validation. At this
investigation's completion, EVAL-0017 was still unselected and
replacement-required; RESEARCH-0035 later selected `REAL-REFR-0001`.

### 5.4 Independent ground truth

Qualification must add:

1. raw record/subrecord offsets, lengths, FormIDs, base links, and six
   placement values for all four patch overrides;
2. independent master-index translation for each FormKey;
3. the containing cell/world identity as structural provenance;
4. an exact comparison of unpatched and patched placements independent of
   Mutagen;
5. author-maintained evidence that the patch is intended for this exact pair;
6. a controlled screenshot or deterministic geometry assertion that identifies
   the bounded problem corrected by each movement;
7. a matched screenshot/assertion after the patch;
8. confirmation that no unrelated mod, generated output, or live-profile edit
   is required; and
9. reviewer sign-off.

If visual proof is required, it must be captured in a disposable controlled
profile, with exact runtime, camera/location procedure, time/weather controls
when relevant, and before/after provenance. In-game observation validates the
case; it does not become a production dependency.

### 5.5 Required controls

| Control | Construction | Expected result |
|---|---|---|
| Placement lead | Exact two source archives/plugins, no patch | Documented spatial-interaction lead only; no EVAL-0017 result |
| Resolution comparison, not EVAL-0017 matched negative | Add exact official patch after both | Evidence for four author-selected placement corrections only after independent symptom proof and a distinct qualified placement analyzer |
| Single-mod negatives | Each source mod alone | No pair-specific compatibility finding |
| Unrelated-position negative | Synthetic nearby-but-noninteracting placements | No inference that proximity alone is a problem |
| Order metamorphic | Reverse main-plugin order | Same pair remains structurally present; no fabricated same-record winner because none exists |
| Patch stale/mismatched | Change either source byte/version | Patch applicability becomes unknown/stale |
| Extra-overhaul boundary | Add a third exterior mod in a controlled fixture | Report widened dependency/candidate scope; do not transfer the pair result |
| Enable-parent generalization | Synthetic Stage 2 fixture from RESEARCH-0019 | Must detect qualified topology reversion independently of this spatial case |
| Linked-reference generalization | Synthetic Stage 2 fixture from RESEARCH-0019 | Must detect qualified link reversion independently of this spatial case |
| Unsupported geometry | Navmesh, landscape, collision, or occlusion claim outside qualified analyzers | Explicit gap/abstention |
| Malformed `REFR` | Project-authored malformed record/group fixture | Isolated failure, no fabricated placement conclusion |

### 5.6 Acquisition, privacy, and redistribution

- Existing retained archives make the exact case locally reproducible for the
  project owner.
- Other evaluators must use Nexus's ordinary user flow or a supported,
  user-initiated API route and verify the exact hashes.
- No redistribution permission for the archives, plugins, patch, or assets was
  established. Do not commit or publicly mirror them.
- Commit only the manifest, project-authored expectations, derived structural
  facts, source links, and project-owned screenshots if their publication does
  not expose restricted source material.
- Omit private paths, account data, download tokens, and profile identity.

### 5.7 Replacement rule

Retire or replace `REAL-PLACEMENT-LEAD-0001` if:

- a second evaluator cannot lawfully acquire the exact files;
- independent inspection contradicts the observed four-record relation;
- author-maintained evidence does not establish the patch's purpose;
- the unpatched symptom is not reproducible or materially diagnostic;
- qualification would require broad geometry, navmesh, or landscape claims
  outside the accepted slice; or
- the analyzer would need mod-name, FormID, cell, or coordinate exceptions.

The required EVAL-0017 replacement was a materially different category proof
and, for the accepted Stage 2 roadmap, should exercise the placed-reference
topology selected by RESEARCH-0019: a later visual/spatial override that drops
an upstream enable-parent or linked-reference relation, optionally with a
narrow forced quest-alias edge, plus an author-maintained patch or
independently specified resolution. This report left EVAL-0017 unselected and
did not authorize substituting the placement lead; RESEARCH-0035 later
qualified a different exact candidate.

## 6. Redistributable synthetic corpus required before the real cases

The following matrix turns RESEARCH-0014 through RESEARCH-0019 into corpus
obligations. It supplies breadth for evaluation and RQ-036 without asserting
that these rows are final product categories.

| Surface input | Required positive | Matched negative | Boundary/unsupported |
|---|---|---|---|
| Generic effective provider state | Loose winner and qualified archive winner with exact provenance | Shadowed identical provider or inactive archive | Unqualified archive applicability, duplicate case/path, changed-during-capture |
| Root/native components | Static PE/resource/version relationship known from a versioned manifest | Same filename with nonmatching identity; shadowed candidate | Malformed PE, renamed unknown component, unsupported machine type; never load code |
| Generated output | Qualified generator input/config/output manifest with one provably stale dependency | Exact matching closure | Unknown generator/version, partial logs, ambiguous output ownership, historical evidence |
| MCM Helper JSON | Valid pinned schema and references | Equivalent valid configuration | Malformed JSON, wrong schema version, unknown semantic value |
| SPID/KID/BOS rule DSLs | Valid pinned syntax plus independently resolved supported links | Valid nonmatching rule | Unknown version/token, malformed line, semantically unsupported condition |
| OAR configuration | Valid pinned JSON/schema and resolvable supported identifiers | Valid rule not applicable to snapshot | Unsupported condition semantics, stale generated behavior state |
| PEX/VMAD | Exact attachment/property/type/reference observations for pinned versions | Equivalent supported property set | Unsupported opcode/control-flow claim, malformed bytecode, dynamic/native behavior |
| NIF references | Typed required texture/reference whose provider is absent | Same source with qualified provider present | Optional empty slot, unsupported NIF version, malformed native-parser input, unqualified archive |
| Plugin-to-asset | Qualified NPC/other record field to derived asset path and loose provider | Same exact record with complete loose providers | Light-ID/path rule not qualified, archive-dependent provider, or asset surface intentionally omitted outside an EVAL-0016/M1 gate |
| NPC scope reversion | Appearance versus package/AI/faction reversion | Patch or intentional combined ownership | Template-controlled inheritance, unsupported field, malformed record |
| `REFR` topology | Dropped enable parent or linked reference; optional forced quest alias | Patch preserves topology; unrelated visual edit | Unsupported placed subtype, broad quest logic, malformed group/link |
| `REFR` placement | Independently planted incompatibility with bounded symptom | Resolved position | Mere proximity, coordinate change without consequence evidence, navmesh/landscape inference |

Every row requires:

- at least one positive;
- a one-cause matched negative;
- a malformed or failure-isolation case where parsing is involved;
- an unsupported/unknown-version case;
- a metamorphic case where order, provider, irrelevant naming, or unrelated
  content changes without changing the semantic expectation; and
- provenance and coverage assertions separate from finding assertions.

Project-authored synthetic fixtures should use generic names and invented
records. They must not copy third-party records, assets, prose, or patches.

## 7. Corpus composition and anti-overfitting

### 7.1 Minimum composition for the first implementation plan

Before an M1 plan can use EVAL-0016/EVAL-0017 as gates, the corpus should
contain:

- the complete synthetic NPC positive/negative/boundary matrix from
  RESEARCH-0019;
- the complete synthetic `REFR` enable-parent/linked-reference positive,
  negative, boundary, and malformed matrix from RESEARCH-0019;
- the complete loose-only FaceGen identity, applicability, provider,
  shadowing, and archive-independence matrix from RESEARCH-0018/0019;
- one exact locally reconstructible cross-layer manifest for
  `REAL-NPC-0001` or an accepted EVAL-0016 replacement;
- one exact locally reconstructible EVAL-0017 replacement exercising the
  selected non-NPC reversion/topology mechanism; the optional
  `REAL-PLACEMENT-LEAD-0001` does not satisfy this requirement;
- independent truth packages for every consumed field and structural shape;
- at least one cross-cutting case involving more than one technical surface;
- at least one unknown/unsupported real candidate that remains a visible gap;
  and
- a corpus inventory showing which intended classification dimensions remain
  unrepresented.

### 7.2 Rules that prevent case-shaped production code

- Synthetic cases precede real cases.
- Real names and IDs exist only in manifests and expectations.
- Production detection operates on typed evidence, override/link/provider
  relations, qualified documentation claims, and explicit applicability.
- Rename-only metamorphic variants must produce the same semantic result.
- Equivalent synthetic structure with different IDs must produce the same
  semantic result.
- A patch title is supporting documentation, not a magic allowlist.
- A known patch may validate resolution but may not suppress all other
  findings involving the same mods.
- A failure on a real case causes fixture, evidence, or analyzer review; it
  does not authorize a special case.
- The large private profile may measure discovery volume later, but it may not
  determine correctness.

## 8. Inputs for RQ-036, not a taxonomy

The accepted taxonomy consumed the corpus as observed breadth evidence. These are
**candidate observations**, not definitive categories:

| Corpus evidence | Distinct classification questions it raises |
|---|---|
| AI Overhaul plus Children of the Pariah | Declared behavior versus appearance intent; plugin-record and asset surfaces; NPC-related affected area; lost behavior versus visual effect; local versus multi-NPC extent |
| Standing Stones plus Farms | Two declared exterior/location intents; placed-reference and asset surfaces; world/exterior affected area; clipping/placement consequence; local spatial extent |
| Root/native fixtures | Runtime integration intent; root/native surface; startup/stability/security consequences; process-wide extent |
| Generated-output fixtures | Tool-output intent and surface; animation/grass/LOD affected areas; stale-state consequence; profile/world extent |
| Configuration fixtures | Feature-configuration intent; JSON/INI/DSL surfaces; behavior/UI/distribution consequences; rule- or mod-scoped extent |
| PEX/VMAD fixtures | Scripted-feature intent; attachment/property/bytecode surfaces; quest/gameplay/state consequences; local or persistent extent |
| Asset-reference fixtures | Presentation/interface/audio intent; mesh/texture/interface/sound surfaces; missing-content consequence; object/cell/global extent |
| `REFR` topology fixtures | World/quest interaction intent; plugin/link surfaces; activation/progression consequence; reference/cell/quest extent |

The accepted RQ-036 taxonomy now governs names, boundaries, multiplicity,
unknown states, and cross-facet relationships. This report does not turn
"NPC," "`REFR`," "root," "script," or "config" into competing affected-area
codes, and it does not infer severity from a surface label.

The corpus should later be audited against the accepted taxonomy. Missing
classification coverage should produce new generic fixtures or candidates,
not relabel existing cases to make the matrix look complete.

## 9. Acquisition and source-policy plan

### 9.1 Permitted routes

For third-party controlled-real cases:

- a human evaluator may use the ordinary authenticated Nexus download flow;
- Infinium may later use Nexus-provided read APIs only after explicit user
  initiation and within ADR-0005 as amended by ADR-0012;
- public author-maintained GitHub repositories, documentation, and release
  assets may be used under their licenses and site terms; and
- locally retained files may be analyzed privately under the accepted
  development-risk posture.

### 9.2 Prohibited assumptions and routes

- Do not scrape or crawl Nexus HTML.
- Do not bypass authentication, rate limits, or file-access requirements.
- Do not publish a reusable corpus of Nexus descriptions, articles, posts, or
  payloads.
- Do not infer redistribution permission from download access, a compatibility
  patch's existence, or Infinium's GPL license.
- Do not treat a search-result snippet as source truth.
- Do not make release reproducibility depend on the creator's private profile.

### 9.3 Re-acquisition workflow

Before accepting either real candidate:

1. query supported file metadata or inspect the normal author download page
   manually;
2. record exact Nexus file ID, timestamp, filename, version, availability, and
   permissions;
3. acquire through the permitted user flow;
4. hash before extraction;
5. reproduce the declared installer choices in a disposable workspace;
6. hash selected members;
7. compare against this report;
8. construct an atomic MO2 profile with only the declared closure;
9. capture the exact effective snapshot; and
10. retain only what the selected permission/retention class allows.

If the hash differs, create a new candidate revision and repeat ground truth.

## 10. Alternatives considered

| Alternative | Benefit | Problem | Disposition |
|---|---|---|---|
| Ship real mod archives in the repository | One-command reproduction | No affirmative redistribution permission; large and policy-sensitive | Reject |
| Use only the creator's full profile | Already available and realistic | Confounded, mutable, private, huge, and nonrepresentative | Reject as oracle; retain only for later scale/discovery |
| Use only synthetic fixtures | Fully redistributable and exact | Misses real packaging, dependencies, author intent, and generalization failures | Reject as complete corpus; make it the primary conformance layer |
| Use only known patch lists | Cheap candidate discovery | Patch existence does not prove issue, applicability, severity, or correctness | Use as evidence-bearing candidate input only |
| Use current/latest versions without hashes | Easier acquisition | Reproducibility and expectations drift | Reject |
| Use xEdit output as truth | Familiar ecosystem convention | Contradicts ADR-0007 and would not make expectations parser-independent | Reject |
| Generate expectations from Mutagen | Fast | Tests parser against itself | Reject as sole truth; allow discovery assistance only |
| Select two NPC cases | Easy continuity | Fails required material generalization and encourages NPC-shaped architecture | Reject |
| Treat Ryn pair as automatically qualified because patch is official | Strong provenance hint | Exact symptom and significance remain unverified | Reject; provisional candidate only |

## 11. Uncertainty, blockers, and follow-up

### 11.1 What is established

High confidence:

- a layered synthetic-plus-private-real corpus is compatible with product,
  policy, reproducibility, and anti-overfitting requirements;
- exact retained source archives and the listed source/patch members exist for
  both proposed combinations;
- `REAL-NPC-0001` contains the intended NPC appearance-versus-behavior shape
  and an author-supplied patch, subject to rerun against the archive-member
  USSEP input and mandatory FaceGen closure;
- `REAL-PLACEMENT-LEAD-0001` is materially non-NPC and its official patch
  changes four source-owned `REFR` placements, but it is not the selected
  EVAL-0017 mechanism; and
- neither real payload set should be redistributed on current evidence.

Moderate confidence:

- `REAL-NPC-0001` may qualify after provenance repair, FaceGen closure, and
  independent byte review;
- the NPC patch supplies a strong matched negative; and
- the Ryn pair is useful at least as a documented spatial-compatibility
  discovery lead.

Low confidence until controlled validation:

- the exact visible symptom and materiality of the four Ryn placement changes;
- continued public availability of every exact retained Nexus file;
- redistribution permissions beyond committed manifests/derived expectations;
  and
- whether current versions preserve the same shapes.

### 11.2 Exit blockers recorded at investigation completion

At this report's 2026-07-25 completion, RQ-025 remained **In progress and
M0-exit-blocking**. This report supplied a
corpus model, one incomplete EVAL-0016 candidate, and one non-gating placement
lead, but it does not yet supply the two exact reproducible EVAL-0016/EVAL-0017
candidate manifests required for M0 disposition. RESEARCH-0035 later closed
the following historical requirements:

- supported/manual current file metadata and acquisition are recorded;
- Fishing and all other dependency bytes are pinned where applicable;
- independent ground-truth packages are authored and reviewed;
- the USSEP archive-member input is adopted and the affected observations are
  rerun, or the installed transformation receives exact provenance;
- the mandatory loose FaceGen closure and archive independence are pinned;
- atomic positive and negative profiles are constructed;
- `REAL-NPC-0001` becomes an exact reconstructible cross-layer candidate or is
  replaced;
- a valid exact EVAL-0017 reversion/topology candidate is selected; and
- redistribution/retention classes are confirmed for every retained artifact.

Those are evaluation-planning and fixture-qualification tasks, not reasons to
invent broader production behavior during M0.

## 12. Recommendation and confidence

Accept the following research recommendation:

1. make redistributable synthetic fixtures the authoritative conformance
   corpus;
2. maintain third-party real-mod cases as hash-pinned, evaluator-supplied
   private inputs unless affirmative redistribution permission is recorded;
3. retain `REAL-NPC-0001` as an incomplete EVAL-0016 candidate pending USSEP
   provenance repair, exact loose FaceGen closure, independent truth, and an
   atomic rerun;
4. retain `REAL-PLACEMENT-LEAD-0001` only as optional
   placement-reconciliation discovery evidence, not as EVAL-0017 or its
   matched negative;
5. keep EVAL-0017 unselected until an exact materially different category
   exercising the same generic scope-incongruent-reversion mechanism is
   identified;
6. require independent byte/structure/semantic truth and matched controls
   before either becomes accepted;
7. keep the private reference profile out of correctness and release gates;
8. retain the varied surfaces and intents as taxonomy observations only; and
9. reject any implementation rule shaped around these mod names or IDs.

Confidence is **High** in the corpus model, **Moderate** that the incomplete
EVAL-0016 candidate can be repaired, and **High** that the current Ryn
placement lead is not sufficient for EVAL-0017.

## 13. Requirements and evidence traceability

| Requirement/decision | Evidence or rule in this report | Remaining gate |
|---|---|---|
| EVAL-0016 | Incomplete NPC candidate, patch negative design, controls, and ground-truth plan | Repair USSEP provenance, pin loose FaceGen closure, then independent truth and atomic run |
| EVAL-0017 | Placement-reconciliation discovery lead and exact replacement criteria | Select a reproducible non-NPC reversion/topology candidate; current Ryn pair is not the gate or matched negative |
| EVAL-0052 | Raw byte/structure expectations required separately from Mutagen | Author and review truth packages |
| ADR-0005 | Supported/manual acquisition only; no scraping or public Nexus corpus | Current file metadata check |
| ADR-0007 | No xEdit source, fixture, comparison, or oracle | Enforced throughout |
| ADR-0008 | Atomic explicit MO2 profiles; live private profile only discovery | Construct controlled profiles |
| ADR-0009 | Exact runtime/data and bounded Mutagen role | Pin Fishing and case-specific data |
| ADR-0010 | Hash-pinned sources, members, order, and dependency closure | Materialize versioned manifests |
| ANALYSIS-017/EVAL-0032 | Real spatial case has no direct shared `REFR`; candidate provenance must explain selection without all-pairs LLM work | RQ-035 benchmark |
| EVID-001/EVID-005/EVID-006 | Probe observations, author claims, candidate state, findings, and gaps remain distinct | End-to-end evaluation |
| EVAL-0086/RQ-036 | Corpus supplies varied observations without defining taxonomy | Accepted taxonomy; stratified case execution pending |
| Fixture guidelines | Synthetic-first, matched negatives, malformed/unsupported/metamorphic controls | Fixture construction |
| Anti-overfitting rules | No real names or IDs in production logic; private profile not oracle | Code and evaluation review |

## 14. Sources

Repository authority and research:

- [M0 research foundation plan](../../plans/milestones/M0-research-foundation.md)
- [Evaluation strategy](../../evaluation/evaluation-strategy.md)
- [Evaluation case catalog](../../evaluation/case-catalog.md)
- [Fixture guidelines](../../evaluation/fixture-guidelines.md)
- [Anti-overfitting rules](../../evaluation/anti-overfitting-rules.md)
- [Taxonomy dependency map](../taxonomy-dependency-map.md)
- [RESEARCH-0001: Nexus access policy](RESEARCH-0001-nexus-access-policy.md)
- [RESEARCH-0003: retention, replay, and export policy](RESEARCH-0003-retention-replay-export-policy.md)
- [RESEARCH-0007: runtime support contract](RESEARCH-0007-skyrim-runtime-support-contract.md)
- [RESEARCH-0008: Mutagen semantic capability](RESEARCH-0008-mutagen-bethesda-semantic-capability.md)
- [RESEARCH-0013: Wave B local-state integration](RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
- [RESEARCH-0014 through RESEARCH-0019](README.md)

Canonical author/source routes for the controlled-real candidates:

- [AI Overhaul SSE, Nexus mod 21654](https://www.nexusmods.com/skyrimspecialedition/mods/21654)
- [Children of the Pariah, Nexus mod 97981](https://www.nexusmods.com/skyrimspecialedition/mods/97981)
- [Unofficial Skyrim Special Edition Patch, Nexus mod 266](https://www.nexusmods.com/skyrimspecialedition/mods/266)
- [Ryn's Standing Stones, Nexus mod 64969](https://www.nexusmods.com/skyrimspecialedition/mods/64969)
- [Ryn's Farms, Nexus mod 72305](https://www.nexusmods.com/skyrimspecialedition/mods/72305)
- [Ryn's Skyrim Official Patch Hub, Nexus mod 73778](https://www.nexusmods.com/skyrimspecialedition/mods/73778)

The exact descriptions, file identities, and payload observations used for
candidate discovery came from already retained local metadata and archives.
They are private research inputs, not redistributed source material. Live
current-version and permissions claims were deliberately not made without a
supported authenticated check.
