# RESEARCH-0007: Skyrim SE runtime support contract

Status: Completed — recommendation accepted by ADR-0009
Date: 2026-07-25
Last reviewed: 2026-07-25
Researcher: Codex agent
Primary research question: RQ-003
M0 wave: B — Authoritative local state and deterministic ground truth
Decision enabled: Runtime support contract and supported-target rejection

Accepted disposition:
[ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
accepts the exact Steam Windows x64 `1.6.1170.0` support-manifest entry and its
fail-closed advancement contract. EVAL-0054 and analyzer-specific semantic
qualification remain required before release support claims.

## 1. Question and accepted constraints

RQ-003 asks:

> Which exact Skyrim SE runtime version is initially pinned, how is it detected,
> and how is support deliberately advanced?

The governing requirements and decisions are:

- SCOPE-001 requires exactly one explicitly pinned Skyrim Special Edition
  runtime version and makes advancement a deliberate tested decision.
- SCOPE-005 requires runtime, base-game, root, configuration, plugin, record,
  loose-file, archive, and generated-output state to be reconstructed without
  implying unsupported semantic coverage.
- SCOPE-006 limits the initial product to Windows desktop.
- SNAP-001, SNAP-002, SNAP-005, and EVID-002 require exact identity,
  immutable run binding, mid-capture change detection, and provenance.
- ANALYSIS-008 and ANALYSIS-009 require version coherence and relevant
  root/runtime observations.
- TOOL-002 requires configured executables to be validated for identity,
  version, accessibility, and operation compatibility before use.
- COVER-001 through COVER-003 require unsupported and failed capability to
  remain explicit.
- ADR-0001 makes deterministic local runtime observations authoritative.
- ADR-0002 prevents a run from blending runtime or game-data states.
- ADR-0003 prohibits setup mutation through M4.
- ADR-0004 rejects best-effort behavior for another runtime, edition,
  distribution, manager, or platform.

This investigation proposes a detection and advancement contract. It does not
accept that contract as architecture and does not claim that every semantic
analyzer has already been validated against the candidate runtime.

## 2. Scope and non-scope

### In scope

- identifying the exact locally installed `SkyrimSE.exe`;
- distinguishing executable version metadata, executable content identity,
  distribution/channel evidence, game-data identity, and demonstrated
  semantic compatibility;
- comparing whole-file hashing, PE/version resources, Steam build/depot
  metadata, SKSE channel detection, and Address Library versioning concepts;
- defining unsupported, unrecognized, inconsistent, unreadable, and
  changed-during-capture behavior;
- proposing how the single active supported runtime is advanced;
- proposing evaluation work for EVAL-0054 and applicable parts of EVAL-0057.

### Out of scope

- accepting a production implementation, stack, parser, or storage schema;
- declaring Mutagen.Bethesda correct or selecting a package version (RQ-004);
- reconstructing the effective MO2 installation (RQ-001);
- selecting the installation-snapshot fingerprint strategy (RQ-014);
- fully identifying SKSE, Address Library, native DLL, Creation Club, or other
  root-component compatibility (RQ-019 and later analyzer work);
- launching Skyrim, Steam, MO2, SKSE, or any external analysis tool;
- verifying, updating, downgrading, repairing, or otherwise changing the
  user's game installation;
- supporting GOG, Epic, Microsoft Store/Game Pass, VR, classic Skyrim,
  another Skyrim runtime, or another platform through M4.

## 3. Preflight, access, and effects

The shared local preflight is recorded in
[the Wave B reference-environment manifest](WAVE-B-reference-environment-manifest.md).
This investigation used the user-confirmed Skyrim installation only as a
read-only experiment subject.

| Surface | Use |
|---|---|
| Local private data | Read `SkyrimSE.exe`, the local Steam app manifest, and five base master files. No profile contents or mod names were recorded. |
| Network | Public Microsoft, Valve/Steamworks, SKSE, and GitHub project sources only. |
| Authentication | None. |
| Paid or model operation | None. |
| External application execution | None. Skyrim, Steam, MO2, SKSE, and LOOT were not launched. |
| Writes | One copyrighted executable copy and two source-repository clones were created in an isolated OS temporary directory for a disposable negative control, then deleted after the observations were recorded. |
| Protected setup effects | None. Original executable and master-file hashes were re-read without writing to the installation. |

The raw Steam app manifest contained account-linked fields. Those fields and
the raw manifest body were deliberately excluded from tracked evidence. Only
the non-account build/depot observations needed by this question are retained
below.

## 4. Sources and exact versions

All online sources were retrieved on 2026-07-25.

| Source | Exact identity | Authority and claim supported |
|---|---|---|
| [Microsoft `VS_FIXEDFILEINFO`](https://learn.microsoft.com/en-us/windows/win32/api/verrsrc/ns-verrsrc-vs_fixedfileinfo) and [version-information overview](https://learn.microsoft.com/en-us/windows/win32/menurc/version-information) | Current Microsoft Learn pages; page does not expose an immutable revision | Primary platform documentation for numeric file/product version resources and author-declared version flags. It does not state that the resource proves byte identity. |
| [Microsoft PE/COFF format](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format) | Current Microsoft PE/COFF specification | Primary format authority for AMD64 machine `0x8664`, PE32+ magic `0x20B`, GUI subsystem `2`, sections, and header fields. |
| [Microsoft `Get-FileHash`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/get-filehash?view=powershell-7.6) | PowerShell 7.6 documentation | Primary documentation that SHA-256 represents file contents and changes when contents change. |
| [Microsoft `Get-AuthenticodeSignature`](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.security/get-authenticodesignature?view=powershell-7.6) | PowerShell 7.6 documentation | Primary documentation for observing embedded/catalog signature state. |
| [Steamworks `ISteamApps`](https://partner.steamgames.com/doc/api/ISteamApps?language=english) | `STEAMAPPS_INTERFACE_VERSION008`; live documentation | Primary Valve documentation: `GetAppBuildId` may change with backend updates and returns `0` outside a Steam-downloaded build; `GetCurrentBetaName` exposes branch; `GetFileDetails` exposes depot-manifest size/SHA-1; `GetInstalledDepots` exposes installed depots. These are current-app Steamworks calls, not proven third-party Infinium interfaces. |
| [Official Steam store entry](https://store.steampowered.com/app/489830/The_Elder_Scrolls_V_Skyrim_Special_Edition/) | App ID `489830` | Primary Valve identity for the Steam product. |
| [Bethesda patch-note support page](https://help.bethesda.net/app/answers/detail/a_id/57117/) | Live Bethesda Support answer `57117` | Primary publisher evidence that Bethesda currently directs Skyrim users to Steam for Steam patch notes. It does not publish a machine-readable runtime manifest or executable digest. |
| [Official SKSE site](https://skse.silverlock.org/) | Live page: SKSE `2.2.6` for Steam game `1.6.1170`; separate GOG build for game `1.6.1179` | Authoritative SKSE-project evidence that store/channel is material and that the current Steam and GOG version labels differ. It is not authority for Infinium semantic compatibility. |
| [SKSE release](https://github.com/ianpatt/skse64/releases/tag/v2.2.6) | `v2.2.6`, commit `9398d04592a7eb9d754f2997701116df1022f1b4` | Primary project source stating support for `1.6.1170`. |
| [SKSE executable identification source](https://github.com/ianpatt/skse64/blob/9398d04592a7eb9d754f2997701116df1022f1b4/skse64_loader_common/IdentifyEXE.cpp) | Same SKSE commit | Primary project implementation: reads `ProductVersion`, classifies Steam by the `.bind` PE section, classifies GOG/Epic by imports and Microsoft Store by `.xbld`, and rejects unsupported version/channel combinations. This is evidence for useful discriminators, not an adopted Infinium implementation. |
| [SKSE Address Library check](https://github.com/ianpatt/skse64/blob/9398d04592a7eb9d754f2997701116df1022f1b4/skse64/PluginManager.cpp) | Same SKSE commit | Primary project implementation deriving `versionlib-M-m-b-0.bin` from the runtime and disabling dependent plugins when it is absent. |
| [CommonLibSSE-NG version reader](https://github.com/CharmedBaryon/CommonLibSSE-NG/blob/b93280e832f263dbef44e44cbe2936622a02f91a/src/REL/Version.cpp), [Address Library selection](https://github.com/CharmedBaryon/CommonLibSSE-NG/blob/b93280e832f263dbef44e44cbe2936622a02f91a/include/REL/ID.h), and [header validation](https://github.com/CharmedBaryon/CommonLibSSE-NG/blob/b93280e832f263dbef44e44cbe2936622a02f91a/src/REL/ID.cpp) | `main` commit `b93280e832f263dbef44e44cbe2936622a02f91a` | Maintained primary source showing that runtime version selects a version-specific Address Library filename and that the library header version is checked. It supports treating Address Library as dependent compatibility evidence, not as independent runtime identity. |

Bethesda's current support page points users to Steam patch notes but did not
provide a stable, machine-readable runtime/build manifest or an official
published hash for this executable. No Bethesda- or Valve-published SHA-256 for
the local executable was identified in the primary sources checked. The initial
hash below is therefore a captured
and tested local reference identity, not a claim that Bethesda or Valve
published or attested that digest.

## 5. Reproducible experiments and artifact manifest

### Environment

- Windows 11 Home `10.0.26200`
- PowerShell `7.6.3`
- .NET `10.0.9`
- input token: `<SKYRIM_ROOT>/SkyrimSE.exe`
- local Steam metadata token:
  `<STEAM_LIBRARY>/steamapps/appmanifest_489830.acf`
- experiment date: 2026-07-25

Every installation input was opened read-only. The commands used standard
Windows/.NET file reads, `Get-FileHash`, `Get-AuthenticodeSignature`, and
bounds-addressed PE-header reads. No executable was loaded or run.

### Experiment A — executable identity and PE structure

Observed local executable:

| Field | Observation |
|---|---|
| Filename in configured game root | `SkyrimSE.exe` |
| Bytes | `37,157,144` |
| SHA-256 | `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9` |
| Numeric file version | `1.6.1170.0` |
| Numeric product version | `1.6.1170.0` |
| Company/product strings | `Bethesda Softworks`; `TESV: Skyrim` |
| Original-filename string | `TESV.exe` |
| PE machine | `0x8664` (AMD64) |
| Optional-header magic | `0x020B` (PE32+) |
| Subsystem | `0x0002` (Windows GUI) |
| Sections | `.text`, `.rdata`, `.data`, `.pdata`, `.text`, `_RDATA`, `.rsrc`, `.reloc`, `.bind` |
| PE timestamp | `0x65A5F4D5` (`2024-01-16T03:15:33Z`) |
| PE checksum | `0` |
| Version flags reported by Windows | not debug, patched, prerelease, private, or special |
| Authenticode | `NotSigned` |

The PE timestamp, zero checksum, version strings, flags, section names, and
absence of an Authenticode signature are observations only. None independently
proves an unmodified Bethesda/Steam executable.

### Experiment B — local Steam provenance signals

The local Steam app manifest reported:

| Field | Observation |
|---|---|
| App ID | `489830` |
| Name | `The Elder Scrolls V: Skyrim Special Edition` |
| State flags | `4` |
| Build ID | `13189953` |
| Depot `489831` | manifest `8442952117333549665`; `7,492,390,909` bytes |
| Depot `489832` | manifest `8042843504692938467`; `8,566,183,335` bytes |
| Depot `489833` | manifest `1914580699073641964`; `37,157,144` bytes |

The executable size equals the reported size of depot `489833`, which is
corroborating local provenance. The app-manifest format and these fields are
not documented as a stable third-party integration contract, the file is
locally mutable, and its full body contains volatile/private fields. It is not
sufficient as the runtime support gate.

### Experiment C — base-game data identity is separate

The following physical base masters were fingerprinted to demonstrate that
the executable and game data need distinct identities:

| File | Bytes | SHA-256 |
|---|---:|---|
| `Skyrim.esm` | 249,753,412 | `2BBC77FDEC35A70EF96B710F8C525E50A1DB9E63E11A391A0EB9EE8F56D36107` |
| `Update.esm` | 18,429,562 | `5F2985B205EA57428164B47E1A5DF57F9B5A1AC0399D4C8B5CF30FC0A60FB008` |
| `Dawnguard.esm` | 24,813,534 | `1208E5153E35366E0ADA1A887720D6D636E2D8592D007FE142B37A57E46B476E` |
| `HearthFires.esm` | 3,681,749 | `70E0D5D6DC42224349D33E8C7BCA73DA447463F671CACC9C15FC0273C93E0008` |
| `Dragonborn.esm` | 64,259,475 | `3B8BF5EAD27337F829FA4D474F0363324124A9696D33FE1AEE7B01262EFF5BD1` |

These are local reference fingerprints, not a complete supported game-data
manifest and not proof that the files are pristine retail bytes. Optional
Creation Club content, language data, root files, and MO2 providers can vary
independently. RQ-001 and RQ-014 own the authoritative effective-state and
dependency/fingerprint strategies.

### Experiment D — version/channel spoof negative control

1. Copy the local executable to an isolated OS temporary directory.
2. Record length, SHA-256, file/product versions, Authenticode status, and PE
   sections.
3. Flip only the final byte of the copy.
4. Re-read the same fields without executing the copy.
5. Re-hash the protected original and verify that it still matches the shared
   manifest.
6. Delete the disposable copy and source clones.

| Field | Before | After one-byte change |
|---|---|---|
| Bytes | `37,157,144` | `37,157,144` |
| SHA-256 | `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9` | `0BF7E411174B7D7BCAD23A1ADDECE6D2835F3F52A72AA7EE3617B0A15CD61D5E` |
| File/product version | `1.6.1170.0` | `1.6.1170.0` |
| PE architecture | AMD64 PE32+ | AMD64 PE32+ |
| Steam `.bind` section | present | present |
| Authenticode | `NotSigned` | `NotSigned` |

This negative control proves that the expected version resource, file length,
PE shape, and SKSE-style Steam discriminator can all remain unchanged after
the file's contents change. Those observations are useful diagnostics and
prefilters, but they cannot authorize exact runtime support.

### Artifact manifest

| Artifact | Retention |
|---|---|
| This report | Tracked, redistributable research prose and fingerprints |
| Original executable, masters, and Steam manifest | Remain in the user's installation; not copied into the repository |
| Raw Steam account fields | Not retained in the report |
| Modified executable copy | Deleted from the isolated temporary directory after the negative control |
| SKSE clone | Public source, exact commit recorded; disposable clone deleted |
| CommonLibSSE-NG clone | Public source, exact commit recorded; disposable clone deleted |

## 6. Findings

### F1 — The candidate initial target is Steam Skyrim SE `1.6.1170.0`

The user-confirmed reference setup, local PE resources, local Steam app/depot
metadata, the current official SKSE site, and SKSE `v2.2.6` agree on Steam
Skyrim SE runtime `1.6.1170`. The exact candidate executable identity is:

```text
target: skyrimse-steam-windows-x64
runtime_version: 1.6.1170.0
steam_app_id: 489830
executable_name: SkyrimSE.exe
executable_bytes: 37157144
executable_sha256:
  C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9
pe_machine: 0x8664
pe_optional_magic: 0x020B
pe_subsystem: 0x0002
expected_store_discriminator: .bind section present
authenticode_expectation: not signed; informational only
```

Confidence: **High** that this identifies the exact local reference executable
and the intended single runtime/channel. Confidence is **Moderate** that this
one hash covers every legitimate Steam copy a future public user might possess,
because only one local installation was observed and no publisher hash was
available. Through M3, an unobserved legitimate variant should still fail
closed as unrecognized rather than silently broadening support.

### F2 — Runtime version, executable identity, channel, game data, and semantic support are different facts

The contract must keep these distinct:

1. **Version claim:** PE file/product version `1.6.1170.0`.
2. **Executable identity:** whole-file SHA-256 plus byte length and validated
   PE structure.
3. **Distribution/channel evidence:** approved hash-to-channel mapping,
   `.bind` structure, Steam App ID/build/depot metadata where available.
4. **Game-data identity:** snapshot-bound fingerprints and providers for base
   masters, archives, language/Creation Club data, and root/effective files.
5. **Semantic compatibility:** the exact parser/analyzer/ruleset versions and
   evaluation results that have been demonstrated against this runtime and
   applicable data.

One layer does not silently prove another. In particular, `1.6.1170.0` text,
SKSE support, or a matching Address Library filename does not prove that
Infinium's own analyzers are correct.

### F3 — Exact whole-file identity is the support-authorizing gate

The disposable one-byte test refutes version resource, length, PE layout,
`.bind`, and Authenticode state as sufficient identities. The PE timestamp
likewise is not whole-file identity. The accepted support manifest should
therefore map one or more deliberately tested whole-file SHA-256 values to the
single active runtime/channel. Version and PE fields remain mandatory
consistency checks and user-facing explanations.

This is a conservative compatibility gate, not a malware verdict. An unknown
hash that claims `1.6.1170.0` is **unrecognized** or **modified relative to the
tested build**; it must not be labeled malicious or corrupt without separate
evidence.

### F4 — Steam build/depot metadata is corroborating provenance, not the hard gate

Valve documents build IDs, branches, depot file details, and installed depots
through Steamworks. The inspected local app manifest exposes similar useful
fields, but its file format is not a documented Infinium integration, it is
mutable/volatile, and a stock-game copy can legitimately exist outside a
Steam-managed root.

Consequently:

- if accepted Steam metadata is available, retain it as provenance;
- absence of the app manifest must not convert an exact approved executable
  copy into another runtime;
- conflicting metadata is a visible provenance mismatch requiring review;
- no Steamworks call is selected here, and mutating methods such as branch
  selection, content verification, or corruption marking are ineligible.

### F5 — Base-game data must bind the snapshot but not be conflated with the runtime gate

The same executable can coexist with changed base masters, optional Creation
Club content, different language data, or unmanaged root files. Those
differences can materially affect analysis but do not necessarily create a
different executable runtime.

The accepted snapshot strategy should fingerprint actual effective inputs and
let each analyzer declare which runtime/data dependencies it needs. A
non-baseline master or archive can produce an installation-integrity finding,
explicit semantic gap, or recomputation dependency without being mislabeled as
an unsupported executable version. Conversely, an approved executable hash
does not excuse missing or incoherent data.

### F6 — Address Library is compatibility evidence after runtime detection

SKSE and CommonLibSSE-NG derive a version-specific Address Library filename
from the already detected runtime and validate the library's embedded version.
Therefore:

- `versionlib-1-6-1170-0.bin` presence is not independent proof of runtime
  identity;
- the file can be missing, stale, copied into the wrong setup, or supplied by
  an unexpected provider;
- after authoritative effective-state reconstruction, its effective provider,
  internal version, and dependencies can support an Address Library/native
  compatibility observation;
- RQ-019 or an owning native-component investigation must define the full
  analyzer contract.

### F7 — Detection must be stable and fail closed

The detector should:

1. obtain the candidate game root from the accepted MO2 integration and
   explicit user configuration, not by scanning arbitrary executable names;
2. open the exact `SkyrimSE.exe` read-only and obtain a stable capture under
   the RQ-014 snapshot strategy;
3. bounds-check and parse PE architecture, subsystem, version resource, and
   useful store discriminators;
4. compute whole-file SHA-256 from the captured bytes;
5. resolve the digest against a versioned accepted support manifest;
6. record optional Steam build/depot provenance and separate game-data
   fingerprints;
7. bind the result, support-manifest revision, detector version, and all
   dependent analyzer versions to the installation snapshot/run.

No runtime-specific semantic analyzer may run merely because the version text
is numerically near, newer, older, or within the `1.6.x` family.

### F8 — Required result states

| State | Condition | Behavior |
|---|---|---|
| `supported-exact` | Hash is in the active support manifest and all structural/version/channel consistency checks pass | Runtime-dependent analyzers may run only within their separately declared tested scope. |
| `unsupported-known` | A valid executable is recognized as another version, channel, edition, or platform | Report observations; do not emit best-effort runtime-specific semantic conclusions. |
| `unrecognized-build` | Version may claim `1.6.1170.0`, but exact content identity is not approved | Explain the mismatch and stop runtime-specific semantic coverage; do not call it malicious. |
| `indeterminate` | File missing/unreadable, PE/version malformed, capture raced, or detector failed | Record failure and coverage gap; do not infer. |
| `supported-runtime/data-gap` | Executable is exact, but a separately declared game-data/native dependency is missing, changed, or unvalidated | Keep runtime identity supported while the affected analyzer is failed, unsupported, or completed with gaps. |
| `internal-inconsistency` | Approved digest resolves but fixed fields disagree with its immutable manifest | Treat as detector/manifest corruption or implementation error and fail closed. |

The final state name vocabulary is an implementation-contract decision; the
semantic distinctions above are the required proposal.

## 7. Alternatives evaluated

| Alternative | Benefit | Failure against requirements | Disposition |
|---|---|---|---|
| Trust only file/product version | Cheap and familiar | One-byte negative control retained the same value; easy to copy/spoof; channel is ambiguous | Reject as support gate; retain as diagnostic |
| Version plus PE timestamp/size/section discriminator | Cheap, explains Steam/GOG-like shape | One-byte control retained size and `.bind`; timestamp is mutable; exact content not established | Reject as support gate; use as prefilter/consistency evidence |
| Authenticode publisher signature | Could authenticate publisher and contents when present | Local reference executable is not Authenticode signed | Unavailable for this target |
| Steam build ID/app manifest only | Useful install provenance and depots | Local file is mutable/undocumented, build ID may change, copied/stock roots may lack it, and content identity is indirect | Corroborating evidence only |
| Steamworks `GetFileDetails`/`GetAppBuildId` | Documented Valve semantics | Current-app API accessibility and third-party use by Infinium are unproven; invocation boundary and side effects are not accepted | Do not select; optional follow-up only if needed |
| SKSE or Address Library compatibility as proof | Mature ecosystem knowledge | Establishes those projects' compatibility, not exact local bytes or Infinium analyzer correctness | Use only as external compatibility evidence |
| Fuzzy code-section or semantic hashing | Could admit wrapper-equivalent legitimate builds | Complex, risks accepting an untested variant, and has no demonstrated need for the single-runtime target | Defer; exact full-file allowlist first |
| Accept every hash reporting `1.6.1170.0` | Fewer false unsupported results | Violates exact pinning and admits unknown or mixed builds | Reject |
| Pin the entire Data directory as part of runtime identity | Very strict reproducibility | Conflates executable runtime with language, Creation Club, mods, root state, and exact snapshot inputs | Reject; fingerprint as analyzer dependencies instead |

## 8. Contrary evidence, uncertainty, and limitations

- Only one local Steam executable was hashed. Another legitimate Steam depot,
  language, region, historical branch, or wrapper-equivalent copy might have a
  different hash while reporting `1.6.1170.0`. The product's initial
  single-target policy permits conservative rejection until that variant is
  independently tested.
- No publisher-published executable digest was found. The proposed hash is a
  tested local reference identity, not a supply-chain attestation.
- The current SKSE website labels the GOG runtime `1.6.1179`, while the
  `v2.2.6` tag's bundled readme still says GOG `1.6.1170`. This source-version
  disagreement reinforces that store/channel and source revision cannot be
  inferred from a version number alone.
- The local app manifest's build/depot observations were not validated through
  Steamworks because no supported third-party Infinium call contract was
  established and this investigation could not launch or mutate Steam.
- Hashing proves byte identity with the tested file; it does not by itself
  prove Bethesda authorship, absence of malicious behavior, or semantic
  correctness.
- The five base-master digests do not cover archives, language data, Creation
  Club content, root files, or the effective MO2 state and are not proposed as
  a complete baseline.
- PE parsing in the experiment was bounded to the known file. A production
  parser must reject truncated, overlapping, malformed, oversized, or raced
  inputs safely under SEC-001 and SEC-003.
- Runtime `1.6.1170.0` is the candidate target because it is the creator's
  user-confirmed reference runtime, not because "latest" is inherently safer
  or more correct. Upstream current-version drift never advances support
  automatically.
- RQ-004, RQ-001, and RQ-014 remain independent Gate B dependencies.
  This investigation cannot prove record/archive semantics, effective state,
  or efficient stable capture.

## 9. Recommendation

### Recommended initial contract

Confidence in the contract shape: **High**.
Confidence in the one currently observed executable hash as the initial
candidate allowlist: **High for the creator's exact reference setup; Moderate
for future public installations**.

ADR-0009 accepts a runtime-support contract that:

1. selects a versioned runtime-support manifest whose single active target is
   Steam Skyrim SE for Windows x64, runtime `1.6.1170.0`;
2. records the executable SHA-256, byte length, PE architecture/subsystem,
   fixed file/product versions, store discriminator, expected signature state,
   distribution/App ID, detector version, and acceptance evidence;
3. makes an approved whole-file SHA-256 match plus immutable-manifest
   consistency the runtime-specific semantic-coverage gate;
4. treats version strings, PE fields, SKSE knowledge, Steam build/depot data,
   and Address Library observations as typed supporting or compatibility
   evidence rather than substitutes for exact identity;
5. keeps game-data/native-component fingerprints and semantic analyzer
   compatibility as separate declared dependencies;
6. emits explicit unsupported, unrecognized, indeterminate, inconsistent, and
   data-gap results without best-effort semantic claims;
7. never launches, verifies, updates, repairs, or changes Steam/Skyrim as part
   of detection.

The exact local hash should remain labeled **candidate support identity** until
the Wave B integration review confirms RQ-001/RQ-004/RQ-014 compatibility and
the applicable EVAL-0052/EVAL-0054/EVAL-0057 specifications are reviewed. That
precondition prevents "we hashed the creator's executable" from being mistaken
for complete semantic support.

### Deliberate support advancement

Through M4, advancement replaces the one active supported runtime; it does not
silently accumulate best-effort multi-runtime support.

1. **Detect without adopting:** an unknown/new executable produces
   `unsupported-known` or `unrecognized-build`. Upstream release labels,
   "latest" status, SKSE updates, or a matching Address Library do not change
   the active contract.
2. **Capture a candidate manifest:** record exact executable identity,
   channel/App ID and available build/depot provenance, base-game data
   identities, detector version, retrieval/capture method, and source dates.
3. **Revalidate the boundary:** run positive, wrong-version, wrong-channel,
   same-version/unknown-hash, malformed-PE, changed-during-capture, missing
   file, copied-root, and metadata-conflict tests.
4. **Revalidate semantics:** rerun every delivered runtime-dependent
   MO2/effective-state, record/link/winner, archive/string, root/native,
   version-coherence, and end-to-end provenance case against the candidate.
   Preserve unsupported analyzer areas as explicit gaps.
5. **Review compatibility data:** update SKSE/Address Library/native-component
   mappings only from independently sourced evidence; do not infer that all
   `1.6.x` components are ABI-compatible.
6. **Accept explicitly:** approve a new versioned support-manifest revision
   through the release/milestone decision process defined by the runtime ADR.
   A channel/contract-mechanism change requires a new or superseding ADR.
7. **Preserve history:** retain the old manifest revision for historical-run
   interpretation. New scans use only the newly active runtime. Historical
   evidence is not rewritten.

### Wave B gate assessment

RQ-003's portion of Gate B is **met at the proposal/evidence level with
documented dependencies**:

- there is a defensible exact executable-identity route;
- unsupported/unrecognized behavior is explicit;
- the investigation used no setup-mutating operation;
- the contract declares runtime, game-data, semantic, and snapshot
  dependencies separately rather than using modification time or guessed
  ownership.

Wave B as a whole is **not assessed by this report**. Gate B still depends on
authoritative MO2/effective-state reconstruction (RQ-001), Bethesda
record/archive ground truth (RQ-004), and the measured snapshot/dependency
strategy (RQ-014), plus any M1-selected LOOT boundary.

## 10. Downstream work enabled

### Proposed ADR

Create a proposed runtime-support/detection ADR containing:

- the versioned support-manifest schema and authority;
- exact versus supporting identity fields;
- the hard-gate result states;
- interaction with installation snapshots and analyzer dependencies;
- advancement, supersession, and historical interpretation;
- non-mutating detection boundaries.

The ADR should cite this report and must not accept Mutagen, a Steamworks
integration, or an RQ-014 fingerprint implementation implicitly.

### Proposed evaluation updates

Refine:

- **EVAL-0054:** add exact supported hash, same-version/unknown-hash, older,
  newer, GOG/other-channel, malformed PE, missing/unreadable, and
  changed-during-capture cases. Assert that runtime-specific semantic
  conclusions run only for `supported-exact`.
- **EVAL-0057:** distinguish executable runtime identity from base-game data,
  SKSE, Address Library, and native component compatibility. An exact runtime
  with a changed dependent component should create a scoped gap/finding rather
  than silently changing runtime identity.
- **EVAL-0083:** require the support-manifest revision, executable fingerprint,
  detector version, and separately applied game-data/native dependencies in
  end-to-end provenance.
- **EVAL-0046:** if a future detector consults an external application rather
  than reading files, prove the exact operation's non-mutation and cache/temp
  effects first.

### Follow-up research

- RQ-014 must select a stable, race-aware capture and dependency fingerprint
  strategy; mtime alone is insufficient.
- RQ-004 must demonstrate semantic compatibility with the candidate runtime
  and relevant base-game/plugin/archive inputs.
- RQ-001 must supply the authoritative game-root and effective data/provider
  inputs.
- RQ-019 must define SKSE, Address Library, native DLL, loader, and other
  root-component compatibility rules and ground truth.
- A public-release follow-up should obtain at least one independently captured
  legitimate Steam `1.6.1170.0` executable identity or documented depot-file
  identity before claiming that the single candidate hash covers public Steam
  installs.

### Suggested RQ-003 status

Accepted registry status:

> **Resolved for M0 by ADR-0009; EVAL-0054 and public-release breadth remain
> pending.** The initial Steam `1.6.1170.0` runtime and exact-hash detection
> contract are accepted for the bounded initial target.

## 11. Requirements and evidence traceability

| Requirement/decision | Evidence or finding | Proposed verification/disposition |
|---|---|---|
| SCOPE-001 | F1, F3, F7 and advancement procedure | Runtime ADR plus EVAL-0054 |
| SCOPE-005 | Experiment C; F2 and F5 | RQ-001/RQ-004/RQ-014; EVAL-0051/EVAL-0052/EVAL-0057 |
| SCOPE-006 | AMD64 PE32+ Windows GUI observation | EVAL-0054 wrong-platform cases |
| SNAP-001 | Exact executable and separate game-data fingerprints | Runtime manifest bound to installation snapshot |
| SNAP-002 | F7 requires stable capture and changed-input failure | RQ-014 plus changed-during-capture EVAL-0054 case |
| SNAP-005 | Versioned detector/support-manifest identity | Runtime ADR/run provenance contract |
| EVID-002 | Exact hashes, versions, source revisions, and dates | EVAL-0083 |
| ANALYSIS-008 | Runtime/component identity kept separate | EVAL-0057 |
| ANALYSIS-009 | Root/runtime evidence and Address Library boundary | RQ-019 and EVAL-0057 |
| TOOL-002 | Exact identity/version/accessibility/operation states | Runtime ADR and M1 configuration contract |
| COVER-001 through COVER-003 | F8 result/gap states | EVAL-0054/EVAL-0057/EVAL-0085 |
| ADR-0001 | Local bytes and deterministic parsing remain authority | Exact-hash gate; no LLM role |
| ADR-0002 | Manifest revision and fingerprints bind immutable run state | RQ-014 and EVAL-0026/EVAL-0083 |
| ADR-0003 | All experiments read protected setup only | EVAL-0046 if any future external operation is introduced |
| ADR-0004 | GOG/other runtimes reject rather than degrade | EVAL-0054 |

## 12. Conclusion

The initial candidate should be **the exact Steam Windows x64 Skyrim SE
`1.6.1170.0` executable captured in the creator's reference setup**, identified
by SHA-256
`C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`
and a versioned set of consistency fields. PE version resources, the Steam
`.bind` marker, local build/depot metadata, SKSE support, and Address Library
versioning are useful evidence, but none is an exact-identity substitute.

Support is not complete merely because that executable is recognized. The
runtime gate, actual game-data snapshot, native-component compatibility, and
tested analyzer semantics remain separate dependencies. Advancing support
requires a new candidate manifest, negative/boundary tests, complete
runtime-dependent semantic revalidation, explicit acceptance, and historical
manifest preservation; upstream "latest" status never advances Infinium
automatically.
