# RESEARCH-0014: Root and native component surfaces

Status: Completed — recommendation accepted by project owner
Date: 2026-07-25
Last reviewed: 2026-07-26
Researcher: Codex agent
Primary question: RQ-019
M0 wave: C
Decision enabled: Bounded native/root analyzer catalog, EVAL-0057
specification input, and RQ-036 technical-surface evidence

Acceptance note: The project owner accepted this report's bounded
recommendation on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
Proposal-era wording below is retained as research provenance; the current
registry, taxonomy, and Gate C status are authoritative in their linked docs.

## 1. Question and requirements

**RQ-019:** Which root-level Skyrim components can be identified and
version-checked deterministically?

The useful product question is narrower than “can a DLL name be recognized?”
Infinium needs to know which conclusions can be made without executing
untrusted native code, which evidence identifies exact bytes versus only a
declared product/version, and when a component relationship remains unknown.

This investigation is governed by:

- [SCOPE-005](../../product/requirements.md#scope-005--effective-installation),
  which includes the base-game directory and relevant root-level components
  while requiring unsupported semantics to remain coverage gaps;
- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety),
  which prohibit setup mutation and constrain external operations;
- [ANALYSIS-008](../../product/requirements.md#analysis-008--version-coherence)
  and
  [ANALYSIS-009](../../product/requirements.md#analysis-009--root-and-unmanaged-state);
- [VALID-003](../../product/requirements.md#valid-003--log-provenance), which
  prevents a historical or weakly matched log from silently becoming current
  installation evidence;
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md),
  the accepted read-only authority boundary;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md),
  the accepted quiescent MO2 effective-state and provider boundary;
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md),
  including the separately gated exact Skyrim runtime identity;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md),
  which requires structural manifests, scoped hashes, and typed dependency
  closures; and
- [the accepted M0 plan](../../plans/milestones/M0-research-foundation.md#wave-c--analysis-surfaces-taxonomy-corpus-and-candidate-scale),
  which permits this bounded survey to inform RQ-036 without pretending the
  full named-analyzer roadmap is complete.

The current evaluation target is
[EVAL-0057](../../evaluation/case-catalog.md), with EVAL-0046, EVAL-0054, and
EVAL-0083 supplying the non-mutation, exact-runtime, and provenance boundaries.

## 2. Scope and explicit non-scope

### In scope

- the physical Skyrim installation root;
- the effective `Data/SKSE/Plugins` namespace and the provider of each winner,
  when the accepted MO2 reconstruction can determine it;
- SKSE loader/runtime binaries and SKSE native-plugin admission metadata;
- Address Library file identity and structural/runtime-header coherence;
- root proxy/injector candidates such as ENB, ReShade, and generic proxy
  loaders;
- named multi-file component relationships where upstream primary evidence
  defines them, using SSE Engine Fixes as a representative boundary case;
- safe static PE structure, import, export, resource, size, and SHA-256
  observations;
- exact, declared-only, ambiguous, malformed, inaccessible, and unsupported
  outcomes; and
- the limits of compatibility conclusions available from static evidence.

### Out of scope

- loading a DLL, invoking its entry point, launching Skyrim, MO2, SKSE, or any
  installed helper;
- malware detection, code signing policy, publisher trust, or a security
  guarantee about arbitrary native code;
- proving that a native plugin completes `SKSEPlugin_Load`, works in game, or
  is behaviorally compatible with every other plugin;
- reverse engineering arbitrary native machine code, signatures, trampoline
  use, or hard-coded offsets;
- a closed catalog of every injector, proxy loader, graphics wrapper, native
  plugin, or system dependency;
- driver, operating-system, redistributable, and hardware diagnosis except
  where an observed local component declares a bounded dependency;
- declaring a complete named-analyzer roadmap for M1 or later;
- defining mod purposes, affected game areas, consequences, severity, symptoms,
  or effect extent; those are separate RQ-036 axes; and
- treating the user's private installation or `Brain Blast Destruction 2024`
  profile as a correct, representative, or gold-standard modlist.

## 3. Sources and exact identities

Sources were retrieved on 2026-07-25. Exact revisions are used where the
upstream makes them available. A moving page or self-declared version field is
not treated as exact byte identity.

| Source | Exact identity | Authority | Claim-level relevance |
|---|---|---|---|
| [Microsoft PE/COFF specification](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format) | Microsoft documentation retrieved 2026-07-25 | Primary platform specification | PE machine type, optional-header format, imports, exports, and resource-table structure |
| [Microsoft version-information documentation](https://learn.microsoft.com/en-us/windows/win32/menurc/version-information) | Microsoft documentation retrieved 2026-07-25 | Primary platform documentation | Meaning and self-declared nature of file/product version resources |
| [Microsoft DLL search order](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-search-order) | Microsoft documentation retrieved 2026-07-25 | Primary platform documentation | Why a game-root proxy can become a load candidate and why static presence does not prove the actual runtime choice |
| [Microsoft `LoadLibraryEx`](https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-loadlibraryexa) | Microsoft documentation retrieved 2026-07-25 | Primary platform API documentation | Loading flags have execution and loader-state implications; static byte parsing is the safer preflight boundary |
| [SKSE 2.2.6 release](https://github.com/ianpatt/skse64/releases/tag/v2.2.6) and [source](https://github.com/ianpatt/skse64/tree/9398d04592a7eb9d754f2997701116df1022f1b4) | Tag `v2.2.6`; commit `9398d04592a7eb9d754f2997701116df1022f1b4` | Primary maintained source/release | Skyrim `1.6.1170` support, root components, loader entry point, and plugin-discovery behavior |
| [SKSE readme](https://github.com/ianpatt/skse64/blob/9398d04592a7eb9d754f2997701116df1022f1b4/skse64_readme.txt) and [change log](https://github.com/ianpatt/skse64/blob/9398d04592a7eb9d754f2997701116df1022f1b4/skse64_whatsnew.txt) | Same pinned SKSE commit | Primary maintained documentation | Root-versus-Data placement, loader use, `1.6.1170` support, and removal of the old Steam loader requirement |
| [SKSE plugin API](https://github.com/ianpatt/skse64/blob/9398d04592a7eb9d754f2997701116df1022f1b4/skse64/PluginAPI.h) and [plugin manager](https://github.com/ianpatt/skse64/blob/9398d04592a7eb9d754f2997701116df1022f1b4/skse64/PluginManager.cpp) | Same pinned SKSE commit | Primary implementation | `SKSEPlugin_Version` fields, version-independence flags, runtime/address-library admission checks, and the later executable load boundary |
| [CommonLibSSE-NG `ID.h`](https://github.com/CharmedBaryon/CommonLibSSE-NG/blob/b93280e832f263dbef44e44cbe2936622a02f91a/include/REL/ID.h) and [`ID.cpp`](https://github.com/CharmedBaryon/CommonLibSSE-NG/blob/b93280e832f263dbef44e44cbe2936622a02f91a/src/REL/ID.cpp) | Commit `b93280e832f263dbef44e44cbe2936622a02f91a` | Primary maintained source | Address Library path selection, header format, pointer size, encoded runtime, and version mismatch rejection |
| [SSE Engine Fixes 7.0.20 release](https://github.com/aers/EngineFixesSkyrim64/releases/tag/7.0.20), [`main.cpp`](https://github.com/aers/EngineFixesSkyrim64/blob/af982b0b57d8d8935686faaf1f8c49508baf0bd1/src/main.cpp), and [`CMakeLists.txt`](https://github.com/aers/EngineFixesSkyrim64/blob/af982b0b57d8d8935686faaf1f8c49508baf0bd1/CMakeLists.txt) | Tag `7.0.20`; commit `af982b0b57d8d8935686faaf1f8c49508baf0bd1` | Primary maintained source/release | Representative companion relationship: SKSE metadata, exact supported runtime branch, Address Library use, and required root preloader presence |
| [ReShade source](https://github.com/crosire/reshade/tree/f191dc03ce8fb435fb1df2ed59fac1e7f944c90e) | `main` commit `f191dc03ce8fb435fb1df2ed59fac1e7f944c90e` retrieved 2026-07-25 | Primary maintained source, not an immutable release claim | Proxy-name selection/collision behavior, `ReShadeVersion` export, and embedded version/product resources |
| [ReShade setup logic](https://github.com/crosire/reshade/blob/f191dc03ce8fb435fb1df2ed59fac1e7f944c90e/setup/MainWindow.xaml.cs), [`dll_main.cpp`](https://github.com/crosire/reshade/blob/f191dc03ce8fb435fb1df2ed59fac1e7f944c90e/source/dll_main.cpp), and [`version.rc2`](https://github.com/crosire/reshade/blob/f191dc03ce8fb435fb1df2ed59fac1e7f944c90e/res/version.rc2) | Same pinned ReShade commit | Primary implementation | Recognized product metadata, exported marker, possible D3D11/DXGI names, and refusal to overwrite an unrecognized proxy |
| [ENBSeries Skyrim SE download page](https://enbdev.com/download_mod_tesskyrimse.html) and [ENB news](https://enbdev.com/news.html) | Moving official pages; Skyrim SE label `0.504` observed 2026-07-25 | Primary publisher pages, not content-addressed release manifests | Current public version label and explicit evidence that publisher bytes may be updated without changing that label |
| [Ultimate ASI Loader](https://github.com/ThirteenAG/Ultimate-ASI-Loader) and [release `v9.7.2`](https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases/tag/v9.7.2) | Release `v9.7.2`, retrieved 2026-07-25 | Primary maintained source/release used as contrary evidence | Multiple generic proxy filenames can identify a different loader; filename alone is not product identity |
| [RESEARCH-0007](RESEARCH-0007-skyrim-runtime-support-contract.md) | Accepted Wave B input | Project research | Exact supported-runtime identity and separation from native-component compatibility |
| [RESEARCH-0013](RESEARCH-0013-wave-b-authoritative-local-state-integration.md) and [reference manifest](WAVE-B-reference-environment-manifest.md) | Accepted Wave B integration/reference input | Project research | Accepted local-state, private-reference, provider, snapshot, and non-mutation boundaries |

### Source applicability notes

- The pinned SKSE and Engine Fixes source establishes what those exact
  revisions declare and implement. It does not establish the behavior of
  arbitrary repacks or modified binaries with the same filenames.
- The pinned ReShade commit was the observed current branch revision, not a
  claim that the locally installed bytes were built from that commit.
- ENB's publisher page is authoritative for its public label, but the absence
  of content hashes and the publisher's updates without a label change make
  the label insufficient for exact identity.
- The ASI Loader source is not evidence that the private installation uses it.
  It is boundary evidence that common proxy names are non-unique.

## 4. Experiments and artifacts

### 4.1 Environment and safety

The read-only local survey used:

- Windows OS build `10.0.26200`;
- PowerShell `7.6.3`;
- .NET runtime `10.0.9`;
- Python `3.14.3`;
- `pefile` `2024.8.26`; and
- Git for shallow, temporary source checkout and revision confirmation.

The installed Skyrim root was resolved from Steam registry/library metadata.
Its absolute path is intentionally omitted. No game, manager, loader, DLL, or
helper was launched. No candidate module was mapped or passed to
`LoadLibrary`, `LoadLibraryEx`, `GetProcAddress`, or an equivalent runtime API.
PE observations came from bounded byte parsing only.

Network effects were unauthenticated reads of public upstream repositories and
pages. Temporary source checkouts containing only those public sources were
placed under the operating-system temp directory; this was the only non-repo
write. A recursive cleanup attempt was rejected by the execution environment,
so the temp checkout remained at handoff and can be removed by normal
operating-system temp cleanup. Its absolute path is omitted. Repository writes
are limited to this report.

### 4.2 Reproducible static-observation procedure

1. Resolve the Steam library containing `SkyrimSE.exe` without changing Steam
   or game state.
2. Enumerate the root non-recursively and separately inspect the effective
   `Data/SKSE/Plugins` namespace when provider reconstruction is available.
3. For each selected file, record normalized relative path, provider,
   length, modification metadata, accessibility, and SHA-256 under one
   capture boundary.
4. For a PE candidate, parse DOS/PE headers, machine type, PE32/PE32+ kind,
   sections, imports, delay imports, exports, and version resources using
   bounded static parsing.
5. Re-hash the selected files after observation and reject the observation if
   bytes changed.
6. Compare an exact observation only with a versioned accepted component
   manifest or an explicit relationship rule. Preserve unknowns otherwise.

This procedure is a research probe, not a production implementation. The
production capture must still satisfy ADR-0008 and ADR-0010 snapshot/provider
requirements, path/reparse safety, resource limits, parser isolation, and
EVAL-0046.

### 4.3 Sanitized private-reference observations

The table records exact bytes observed twice without drift during this
investigation. It is shape evidence from one real installation, not a public
release manifest or a general compatibility result.

| Relative root file | Length | SHA-256 | Declared/static observation |
|---|---:|---|---|
| `SkyrimSE.exe` | 37,157,144 | `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9` | Exact runtime accepted separately by ADR-0009 |
| `skse64_loader.exe` | 225,792 | `730C2743F6871FBAEB8606C1D3B7A55FECA045C3D74858A41B0C6D03CD989FBC` | PE32+ AMD64; product `SKSE64`; declared file version `0, 2, 2, 6` |
| `skse64_1_6_1170.dll` | 1,173,504 | `C9A2C8A80DF6BF2372C5F49468BB2E5AB67786157265B6F29ECE9F4EAC075D54` | PE32+ AMD64; declared file version `0, 2, 2, 6`; one export |
| `d3dx9_42.dll` | 88,576 | `CF366987DA6237559EB6E113EA717EC21762C9EEC3A87D9DC4FA9DDFE7789C26` | PE32+ AMD64; no version/product/company resource; 329 exports |
| `dxgi.dll` | 4,718,080 | `E9901C9BF38B2A52E2F262389317B0F4BE99B3A719FA576D355B3138FD5ADF24` | PE32+ AMD64; declared product `ReShade`, company `crosire`, file version `6.3.3.3`; includes `ReShadeVersion` and DXGI exports |
| `tbb.dll` | 401,280 | `692380CECD03181D7FD536E4402783E7F38EA0765B35BB52A3236256959B40CD` | PE32+ AMD64; declared version `2020.3.2020.0622` |
| `tbbmalloc.dll` | 246,656 | `C6F86E3FD7C43E67F96949A0A0012A989CDEA3364524FAEE4E115263250FB787` | PE32+ AMD64; declared version `2020.3.2020.0622` |
| `steam_api64.dll` | 298,384 | `1DB3FD414039D3E5815A5721925DD2E0A3A9F2549603C6CAB7C49B84966A1AF3` | PE32+ AMD64; declared version `07.40.51.27` |
| `bink2w64.dll` | 391,360 | `653247E35DB8D6453E83A008C805A877FD2D56A1D844282F9065CE2F34388FEC` | PE32+ AMD64; declared versions `2.7s` / `1.300s` |

All inspected PE files declared AMD64 machine type `0x8664` and PE32+ optional
header magic `0x20B`. That is useful structural evidence, not proof that each
file is appropriate, safe, or loadable.

The physical root's `Data/SKSE/Plugins` directory was absent. This does not
prove that the effective MO2 profile supplies no native plugins; ADR-0008
requires provider reconstruction across enabled mod roots, secondary roots,
overwrite, mappings, and supported physical Data inputs.

Two existing root logs were sampled:

- the `d3dx9_42` log described an SKSE plugin preloader, proxy registration,
  and DLL/SKSE plugin loading; and
- the ReShade log described ReShade `6.3.3.3` loading through `dxgi.dll` into
  `SkyrimSE.exe`.

These observations corroborate possible component roles but were not matched
to the current snapshot/session under VALID-003. They are therefore
**historical/unknown**, not authority that the current effective installation
loaded those bytes.

### 4.4 Source experiments

Three exact upstream revisions were checked out under a disposable temp root:

| Repository | Revision inspected | Observation |
|---|---|---|
| `ianpatt/skse64` | `9398d04592a7eb9d754f2997701116df1022f1b4` | Exact plugin metadata fields, admission checks, Address Library filename construction, and subsequent executable-load boundary |
| `aers/EngineFixesSkyrim64` | `af982b0b57d8d8935686faaf1f8c49508baf0bd1` | Exact SKSE declarations and failure when the required root preloader is not present |
| `crosire/reshade` | `f191dc03ce8fb435fb1df2ed59fac1e7f944c90e` | Exact proxy selection/collision logic, version resources, and exported marker |

No build, package execution, or installed-state comparison was performed.
These checkouts establish source behavior at their revisions; they do not
identify private installed bytes unless an independent exact release manifest
links those hashes.

## 5. Findings

### F1 — Deterministic inventory is broader than deterministic product identity

**Verified fact:** Infinium can deterministically observe an accessible
effective relative path, provider/winner, length, SHA-256, PE structure,
declared resources, imports, delay imports, and exports at a stable snapshot.

**Interpretation:** Those observations support a complete structural inventory
of the covered namespace. They do not automatically identify a product or
release. Inventory coverage and semantic/version coverage must be reported
separately.

### F2 — Exact bytes, declared identity, release identity, and compatibility are distinct

The evidence layers are:

1. **Effective observation:** a provider supplies bytes at a relative path.
2. **Byte identity:** length plus SHA-256 identifies the captured bytes.
3. **Declared identity:** PE resources, exports, config, or documentation claim
   a product/version/role.
4. **Release identity:** a versioned accepted manifest links an exact hash to a
   release component and expected role.
5. **Relationship/compatibility:** a versioned rule links exact or adequately
   identified components to one another and to the supported runtime.

Each later layer requires evidence not supplied by the earlier layer. In
particular:

- a filename is not byte or product identity;
- a self-declared version resource can be missing, stale, copied, or modified;
- a matching version label is not proof of matching bytes;
- an exact hash identifies bytes but says nothing about compatibility unless a
  reviewed manifest/rule gives the hash meaning; and
- a compatible-by-rule component can still fail at runtime.

### F3 — SKSE itself supplies useful, bounded static admission metadata

At SKSE `2.2.6`, the native-plugin manager scans
`Data/SKSE/Plugins/*.dll`. Its `SKSEPlugin_Version` data includes:

- metadata format version;
- plugin version, name, author, and support email;
- version-independence flags;
- up to 16 compatible runtime versions; and
- a minimum SKSE version requirement.

The implementation checks 64-bit structure, known flags, runtime
compatibility, selected post-AE/Address-Library flags, minimum SKSE, and the
expected Address Library filename before the later executable load. The
runtime-specific file form for the accepted runtime is
`Data/SKSE/Plugins/versionlib-1-6-1170-0.bin`.

**Boundary:** SKSE's own source uses a resource-style mapping mode while
inspecting the version export, then later performs an actual `LoadLibrary` and
calls the plugin. Infinium should reproduce only the bounded metadata parse,
not use the installed SKSE loader as an analyzer and not map or execute the
candidate. Passing the static admission rules is evidence that SKSE would
admit the declared metadata, not proof that `SKSEPlugin_Load` succeeds or that
the plugin behaves correctly.

### F4 — Address Library has deterministically checkable structure, not universal compatibility proof

The pinned CommonLibSSE-NG source selects the runtime-specific
`versionlib-{runtime}.bin` file for the AE runtime branch. Its header records
the format, four-part runtime version, name length, pointer size, and address
count; the implementation rejects format and runtime mismatches.

Infinium can therefore check:

- effective presence and provider;
- exact hash and length;
- bounded header validity;
- encoded runtime equality with the accepted runtime;
- pointer-size sanity; and
- contradictions such as multiple effective expectations for the same path.

It cannot infer from header presence alone that every plugin's requested IDs
exist, that a plugin uses only Address Library relocation, or that a
native-plugin combination is behaviorally compatible. Deeper ID-reference
validation would require a named analyzer and exact consumer expectations.

### F5 — Multi-file components require relationship manifests

SSE Engine Fixes `7.0.20` is a representative case:

- its SKSE plugin declares use of Address Library and exact supported runtime
  information in the selected source branch; and
- its initialization explicitly fails when the required root preloader is not
  active, directing the user to place the Part 2 preloader beside
  `SkyrimSE.exe`.

A correct finding is not “`d3dx9_42.dll` exists.” It is a versioned relation
among:

- the effective SKSE plugin;
- the effective root preloader and any companion payload;
- the selected configuration;
- the accepted Skyrim and SKSE runtime identities; and
- expected providers/versions from an exact supported release manifest.

The local preloader lacked a version resource, demonstrating why exact hashes
and release manifests are necessary. Local filenames and nearby TBB binaries
are not enough to assign an exact Engine Fixes release independently.

### F6 — Proxy paths identify a load position, not a unique product

Microsoft's DLL search-order documentation explains why a game-root DLL can
become a candidate before the corresponding system location for an unpackaged
application. ReShade's setup supports multiple proxy names and refuses to
replace an unrecognized existing proxy. Ultimate ASI Loader supports many of
the same common names.

Therefore `dxgi.dll`, `d3d11.dll`, `dinput8.dll`, `version.dll`, and similar
names are **roles/positions**, not unique component identities. A generic
detector may report:

- an observed proxy candidate and exact bytes;
- static imports/exports and declared metadata;
- one effective winner at a path; and
- an ambiguous or conflicting intended relationship when separate,
  trustworthy install-intent evidence expects different products at that path.

It may not conclude “ReShade,” “ENB,” “ASI Loader,” “duplicate,” or
“incompatible” from the filename alone.

### F7 — ReShade offers strong declared markers, but hashes still matter

The pinned ReShade source embeds product/version resources and exports
`ReShadeVersion`. The local `dxgi.dll` had all of those mutually consistent
markers for declared ReShade `6.3.3.3`.

This supports a high-confidence **declared identity**. Exact release identity
still requires a trusted hash-to-release mapping or a reproducible publisher
manifest. A modified binary could retain the same resource and export
surface.

### F8 — ENB provides direct negative evidence against version-label identity

On 2026-07-25 the official Skyrim SE page labeled the current public version
`0.504`. The official news page also described updates made without changing
that version number, including a July 2026 update.

Consequently, “ENB `0.504`” is not a unique byte identity. Exact supported
content must be represented by SHA-256, length, acquisition/source revision or
retrieval time, and a reviewed component manifest. Where an exact mapping is
unavailable, Infinium must report declared/publisher-label identity and
compatibility as indeterminate rather than choosing one silently.

### F9 — Static imports and OS rules do not prove the actual runtime module graph

The PE import table establishes declared static and delay-load relationships.
It does not cover arbitrary `LoadLibrary` calls, plugin discovery, proxy
configuration, injected modules, or code-generated paths. Actual selection can
also depend on already-loaded modules, API sets, Known DLLs, side-by-side
manifests, DLL redirection, and other Windows loader state.

Infinium should word a static edge as “declares an import,” “is an effective
candidate at,” or “is expected by the pinned component rule,” not “was loaded”
or “will load.” Exact matched runtime evidence may establish an observed load,
but only under VALID-003 and with its own session/snapshot provenance.

### F10 — Unknown and unavailable are required successful outcomes

A useful deterministic analyzer does not need to recognize every binary. It
must retain enough structural evidence to say why it stopped. At minimum,
internal analyzer results need distinguish:

- exact-known bytes under an accepted manifest;
- consistent declared identity without exact release identity;
- heuristic candidate;
- ambiguous identity;
- unrecognized but inventoried file;
- malformed or unsupported structure; and
- inaccessible or changed-during-capture input.

Compatibility evaluation separately needs:

- compatible by an accepted exact rule;
- incompatible by an accepted exact rule;
- incomplete expected relationship;
- indeterminate; and
- unsupported.

These are evidence/coverage states, **not** mod-purpose, technical-surface,
affected-game-area, consequence, severity, symptom, or extent taxonomies.
The accepted taxonomy may map native/runtime observations into its separate
technical-surface axis without collapsing those other axes.

### F11 — A relationship graph fits the evidence better than filename rules

The bounded model should represent components and evidence-qualified edges
such as:

- `supplies-effective-path`;
- `declares-import`;
- `expected-companion`;
- `admitted-by-static-SKSE-rule`;
- `targets-runtime`;
- `requires-address-library`; and
- `observed-loaded-in-exact-session`.

Every edge needs:

- source and exact version/revision;
- expected and observed endpoints;
- provider and snapshot identity where local;
- evidence class;
- applicability preconditions;
- contradiction/unknown state; and
- whether the edge is static, expected, or runtime-observed.

This is compatible with ADR-0010 typed dependency closures and prevents a
nearby filename from becoming an unsupported compatibility conclusion.

## 6. Alternatives evaluated

| Alternative | Benefit | Failure/rejection criterion | Disposition |
|---|---|---|---|
| Filename allowlist only | Cheap and simple | Same proxy name can identify multiple products; renamed or absent-resource components become false positives/negatives | Reject as identity/compatibility authority; retain filenames only as candidate hints |
| PE version-resource matching | Read-only and widely available | Resources are optional and self-declared; local preloader had none; modified bytes can retain a version | Retain as declared evidence only |
| Hash-only recognition | Exact captured-byte identity | No meaning without a trusted manifest; cannot cover unknown files or relationships | Use as the basis of exact manifests, not the whole analyzer |
| Execute/load each DLL and query it | May reveal runtime exports or initialization behavior | Executes untrusted native code, can mutate state/crash, violates the initial authority and safety model, still does not prove in-game compatibility | Reject |
| Invoke installed SKSE/Skyrim and infer from logs | Exercises the real loader | Launches user tooling/game, creates outputs, depends on runtime state, and produces session evidence rather than safe preflight proof | Reject for initial preflight; separately consider user-provided exact-session evidence under VALID-003 |
| Parse arbitrary machine code for runtime compatibility | Could theoretically detect signatures/offsets | Unbounded reverse-engineering cost, fragile inference, architecture-specific false certainty, and no generic behavioral proof | Reject as a general analyzer |
| Recognize only named products | Easier high-confidence rules | Hides unrecognized root state and cannot expose structural coverage gaps | Reject as the inventory boundary; use named modules on top of universal structural inventory |
| Universal static inventory plus versioned named relationship manifests | Preserves unknowns, supports exact evidence, and permits incremental analyzers | Requires manifest governance, qualification fixtures, and honest partial coverage | Recommend |

## 7. Uncertainty, contrary evidence, and limits

### Material uncertainty

- Public, publisher-authenticated hash manifests are not consistently
  available. Infinium needs a governed acquisition and review process before
  treating project-maintained hash manifests as exact release truth.
- This investigation did not download ENB binaries or redistribute
  non-redistributable artifacts. Its ENB conclusion is limited to the official
  public label/update policy and the general identity consequence.
- The private local root contains an internally consistent declared ReShade
  component and an unversioned preloader-shaped DLL, but their histories and
  exact acquisition packages were not independently reconstructed.
- No effective MO2 `Data/SKSE/Plugins` provider reconstruction was performed in
  this bounded root probe. Its absence in physical Data is explicitly not an
  effective-state conclusion.
- SKSE plugin metadata parsing must be specified and tested against malformed,
  truncated, oversized, adversarial, and unusual-but-valid PE files before
  implementation.
- The exact extent to which a generic analyzer should parse Address Library ID
  bodies, versus only headers and named consumer expectations, remains a later
  analyzer-depth decision.
- Product-specific proxy chaining and configuration can make two components
  coexist. A same-role or same-path expectation is not automatically a
  conflict without exact install-intent/configuration evidence.
- Native plugins may declare runtime independence. That declaration narrows
  the applicable SKSE admission rule; it does not prove actual independence.

### Unsupported conclusions

This survey does not support:

- “all installed native plugins are compatible”;
- “no root/native issue exists”;
- “a known filename is an authentic release”;
- “matching version resources mean matching code”;
- “a valid Address Library header satisfies every plugin”;
- “a static import is the module actually loaded”;
- “the latest release is always the correct release for the user's setup”;
- “an old log describes current state”; or
- “unrecognized means malicious or erroneous.”

### Rejection criteria for a future analyzer proposal

Reject or narrow a proposed native/root analyzer if it:

- loads or executes candidate native code during preflight;
- mutates the game, MO2, profile, mods, configuration, or generated outputs;
- omits provider/winner and snapshot identity;
- treats a filename or version resource as exact identity;
- treats locally inferred compatibility as a definitive upstream claim;
- hides unrecognized/inaccessible files from coverage;
- turns historical logs into current state without VALID-003 matching;
- lacks bounded parser/resource limits and malformed-input fixtures; or
- invents a mod/game-area taxonomy instead of contributing bounded evidence to
  RQ-036.

## 8. Recommendation

### Recommended answer to RQ-019

Infinium can deterministically identify and version-check root/native
components **only in layers**:

1. reconstruct and fingerprint the effective root/native namespace;
2. statically parse bounded PE and component metadata without loading code;
3. distinguish exact byte identity from declared product/version;
4. apply versioned named relationship rules only when every required endpoint
   has adequate identity and provenance; and
5. preserve ambiguous, unrecognized, malformed, inaccessible, and
   indeterminate outcomes as visible coverage states.

Build a universal structural inventory first. Add bounded named checks for:

- the accepted Skyrim runtime identity already governed by ADR-0009;
- the supported SKSE component set and SKSE native-plugin admission metadata;
- Address Library header/runtime coherence;
- exact ReShade identity/collision rules when a governed manifest exists;
- exact ENB identity/collision rules only when bytes and publisher/acquisition
  revision can be distinguished despite reused version labels; and
- multi-file products such as Engine Fixes only from exact component/companion
  relationship manifests.

This list is an initial evidence-backed analyzer set, not a closed taxonomy or
complete roadmap. Native/root analysis should not block M1 unless it is
explicitly selected for M1 scope, consistent with the accepted M0 plan.

### Confidence

- **High** that static inventory, hashing, PE structure, SKSE metadata, and
  Address Library header/runtime checks are feasible without setup mutation.
- **High** that filenames and version labels alone are inadequate, based on
  primary platform/product evidence and local boundary observations.
- **Medium** that the named component set above is the right first delivery
  slice; final milestone selection depends on the accepted taxonomy, remaining
  Gate C corpus/FaceGen work, exact manifest availability,
  architecture/security qualification, and M1 planning.
- **Low/unsupported** for generic behavioral compatibility inference from
  arbitrary native binaries.

### Preconditions

- accepted schema and governance for exact component/release/relationship
  manifests;
- provider-aware effective-state capture under ADR-0008;
- snapshot/invalidation implementation under ADR-0010;
- bounded, isolated static-parser design with resource limits;
- exact supported-runtime gate under ADR-0009;
- source-policy/licensing review for any retained publisher bytes or metadata;
- detailed EVAL-0057/EVAL-0046/EVAL-0083 specifications; and
- an accepted milestone plan before production implementation.

## 9. Exact downstream work enabled and disposition

The owner accepted the recommendation. The registry/taxonomy/status changes
below are now applied where indicated; analyzer implementation, qualification,
and later ADR work remain pending:

1. **RQ-019 registry update (applied):** bounded survey and conditional named
   analyzer status are recorded in the open-question registry.
2. **Taxonomy input (applied):** root/native/runtime informed the accepted
   technical modification surface. Do not derive declared
   purpose, affected game area, consequence, severity, symptom, or extent from
   the native/root label.
3. **Native/root analyzer ADR, only if selected for a milestone:** accept or
   reject static-only parsing, layered identity, manifest governance,
   relationship edges, parser isolation, and unsupported-state semantics.
   This report is not that decision.
4. **Product/domain specification:** define the layered observed/byte/declared/
   release/relationship evidence model and keep it distinct from user-facing
   finding confidence/severity.
5. **Source registry additions (applied):** pin the primary Microsoft, SKSE,
   CommonLibSSE-NG, Engine Fixes, ReShade, ENB, and contrary proxy-loader
   sources above, including moving-page limitations.
6. **EVAL-0057 detailed cases:**
   - exact known hash versus same declared version with one changed byte;
   - no version resource;
   - malformed/truncated and resource-exhaustion PE inputs;
   - 32-bit DLL in a 64-bit target;
   - exact/missing/malformed `SKSEPlugin_Version`;
   - unknown SKSE flags, wrong runtime, insufficient SKSE version, and missing
     or mismatched Address Library;
   - Address Library wrong format/runtime/pointer size;
   - proxy filename shared by different exact products;
   - ReShade `dxgi` and `d3d11` placement variants;
   - ENB same public label with different hashes;
   - Engine Fixes plugin present with missing/mismatched/ambiguous companion;
   - provider winner versus shadowed candidate and unsupported mapping;
   - changed-during-capture/inaccessible file;
   - stale runtime log not automatically applied; and
   - explicit unrecognized-but-inventoried outcome.
7. **EVAL-0046 extension:** prove the analyzer never loads/executes candidate
   code, never reaches protected writes, records all product cache/temp effects,
   and leaves disposable game/MO2 roots unchanged.
8. **EVAL-0083 extension:** carry source revision, manifest version, exact
   local hash/provider/snapshot, relationship rule, contradictions, and
   coverage gaps through every material conclusion.
9. **Security follow-up:** specify parser sandbox/failure isolation, file-size
   and structure-count bounds, reparse/path validation, TOCTOU handling, and
   sensitive-path redaction in the selected architecture.
10. **Manifest/acquisition follow-up:** determine how exact publisher or
    project-reviewed hashes are acquired, reviewed, versioned, revoked, and
    retained without redistributing restricted binaries.

## 10. Accepted RQ-019 disposition

Current registry wording:

> Bounded M0 survey answered; static structural inventory, layered identity,
> and versioned relationship manifests recommended. Named-analyzer breadth,
> manifest governance, security qualification, and EVAL-0057 remain
> conditional/pending.

The bounded M0 question is researched and its recommendation is accepted.
Named-analyzer depth, manifest governance, implementation, and qualification
remain conditional. They should block M1 only if native/root analysis is
selected into M1 scope.

## 11. Requirements-and-evidence traceability

| Requirement/decision | Evidence in this report | Supported conclusion | Residual work |
|---|---|---|---|
| SCOPE-005 | Static root inventory plus explicit physical/effective Data distinction | Root/native inputs can be reconstructed without implying semantic coverage | Effective provider reconstruction and scale qualification |
| AUTH-001 | No game, manager, loader, helper, or candidate DLL executed; no setup writes | Static survey is compatible with read-only intent | EVAL-0046 against production implementation |
| AUTH-002 | Only this report and disposable OS-temp source checkouts were written | Research probe stayed outside protected setup roots | Product cache/temp/path authorization design |
| AUTH-003 | No installed modding/game helper invoked; research-only PowerShell, Python, and Git operations and their temp effect are recorded | No unqualified modding-helper operation became architecture | Record/qualify every future production external operation |
| ANALYSIS-008 | SKSE, Address Library, Engine Fixes, ReShade, and ENB version/identity boundaries | Version coherence requires exact identity plus relationship rules | Governed manifests and named qualification |
| ANALYSIS-009 | Root proxy, loader, unmanaged-file, and native-plugin observations | Universal structural inventory is feasible and unknowns remain visible | Accepted analyzer scope and coverage UI |
| VALID-003 | Existing root logs classified historical/unknown | Logs do not silently assert current loaded state | Exact-session matching fixtures |
| ADR-0008 | Physical Data absence explicitly separated from effective MO2 state | Provider/winner provenance is mandatory | Use accepted reconstruction in implementation |
| ADR-0009 | Exact runtime hash kept separate from native compatibility | Root analyzers cannot broaden runtime support implicitly | EVAL-0054 and native manifest rules |
| ADR-0010 | Exact hashes, provider/snapshot, relationship edges, and invalidation preconditions | Findings can carry typed dependency closure | Schema and invalidation conformance |
| EVAL-0057 | Boundary-case list in section 9 | A detailed native/root ground-truth specification can now be written | Independent case review and execution |
| EVAL-0083 | Source/local/rule provenance layers | Material conclusions can remain inspectable | End-to-end implementation qualification |
| RQ-036 | Native/root technical-surface input only | Survey informed accepted taxonomy version `0.1.0` without conflating axes | Analyzer coverage remains unimplemented |

## 12. Conclusion

Root/native analysis is feasible and valuable when it is framed as
provider-aware static inventory plus exact, versioned relationship checking.
It is not feasible to derive comprehensive native compatibility from names,
version resources, import tables, or arbitrary binary inspection.

The strongest deterministic results are exact byte observations, structural
validity, SKSE-declared admission metadata, Address Library runtime-header
coherence, and manifest-backed component relationships. Everything else must
retain its declared-only, ambiguous, unknown, historical, or unsupported
status. That boundary supplied the accepted taxonomy with technical-surface
evidence and gives
EVAL-0057 a concrete specification direction without silently choosing an
architecture or promising a complete native-analyzer roadmap.
