# RESEARCH-0008: Mutagen.Bethesda Skyrim semantic capability

Status: Completed
Disposition: recommendation accepted by ADR-0009
Date: 2026-07-25
Last reviewed: 2026-07-26
Researcher: Codex agent
Primary research question: RQ-004
M0 wave: B — Authoritative local state and deterministic ground truth
Decision enabled: Bethesda semantic layer ADR

Accepted disposition:
[ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
accepts `Mutagen.Bethesda.Skyrim` `0.54.2` as the initial semantic dependency
for positively allowlisted shapes over authoritative inputs. It does not
accept Mutagen's standard environment/archive/string authority or claim that
EVAL-0052 has passed.

Subsequent accepted decision:
[ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
excludes xEdit from Infinium and supersedes every xEdit-dependent proposal in
this report. Mutagen qualification must instead use independently specified,
first-party fixture truth; the same Mutagen path under test may not be the sole
authority for expected results.

## 1. Question and accepted constraints

RQ-004 asks:

> Does the leading bundled Mutagen.Bethesda candidate provide correct,
> performant, failure-bounded coverage for Skyrim SE plugins, override chains,
> archives, strings, and target record families, and which exact
> package/version is acceptable?

The governing requirements and accepted decisions are:

- SCOPE-001 and ADR-0004 limit current semantic claims to the one deliberately
  supported Skyrim SE runtime. RESEARCH-0007 proposed runtime `1.6.1170.0`;
  ADR-0009 subsequently accepted its exact executable identity.
- SCOPE-005 requires plugin, record, loose-file, archive, and generated-output
  state to be reconstructed without implying unsupported coverage.
- AUTH-001 through AUTH-003 and ADR-0003 require a read-only product through
  M4 and explicit accounting for tool and cache writes.
- SCAN-001, SCAN-006, SCAN-007, and SCAN-008 require modular analyzers,
  failure isolation, valid reuse, and bounded resource behavior.
- SNAP-001, SNAP-002, and SNAP-005 require exact input and analyzer identity,
  immutable run binding, and changed-input detection.
- EVID-001, EVID-002, EVID-006, and COVER-001 through COVER-003 require typed
  provenance, abstention, and explicit unsupported or failed coverage.
- ANALYSIS-003 requires meaningful record interactions rather than an
  undifferentiated conflict dump. ANALYSIS-016 requires each analyzer to
  declare its evidence needs, scope, cost, outputs, and failure behavior.
- EVAL-0052 requires supported override chains, links, and winners to agree
  with independently specified fixture truth and unsupported semantics to
  become explicit gaps.
- ADR-0001 makes deterministic local observations authoritative for local
  state. An LLM may not repair or invent parser output.
- ADR-0002 prevents evidence from different installation snapshots or
  analysis contexts from being blended.
- ADR-0006 accepts GPL-3.0 as the project license family and identifies
  Mutagen.Bethesda as a bundled-library candidate. Licensing compatibility
  does not establish technical fitness.

This report evaluates a candidate and proposes a bounded adoption contract. It
does not accept a production package, application runtime, semantic schema,
record-family taxonomy, or architecture.

## 2. Scope and explicit non-scope

### In scope

- exact current stable Skyrim package, target frameworks, source revision,
  licence, direct dependencies, and observed resolved dependency graph;
- read-only plugin parsing for Skyrim SE full and light plugins;
- master declarations, FormKeys, links, override chains, and winning records;
- direct Skyrim SE BSA reading and file extraction;
- localized-string discovery and resolution from loose files and archives;
- malformed, truncated, unknown-subrecord, and superficially changed input
  behavior;
- a breadth smoke test against the five official base-game master files;
- preliminary time and managed-memory observations;
- the boundary between Mutagen-owned semantics and MO2/effective-state
  semantics;
- rejection criteria, unsupported cases, and a parser-independent
  qualification plan for EVAL-0052.

### Out of scope

- accepting the proposed Bethesda semantic layer ADR;
- defining the separate mod-impact taxonomy, which was later accepted through
  RQ-036 and remains outside this report's authority;
- claiming correctness for every generated record type or field merely
  because an API type exists;
- using `Brain Blast Destruction 2024` as a correctness, performance, or
  representative-modlist corpus;
- launching MO2, LOOT, Skyrim, Steam, SKSE, or another external tool;
- mutating the user's game, MO2 instance, profiles, mods, load order, archives,
  plugins, or configuration;
- benchmarking a high-end real modlist or selecting production performance
  budgets, which remain RQ-027 work;
- proving MO2 provider, archive, loose-file, or plugin-order reconstruction,
  which remains RQ-001 work;
- selecting snapshot fingerprints and invalidation rules, which remains
  RQ-014 work;
- proving every malformed or adversarial input is resource-bounded;
- evaluating plugin writing or patch generation, which is outside the
  read-only MVP;
- assessing other Bethesda games, Skyrim VR, classic Skyrim, another Skyrim SE
  runtime, or another platform.

The user-confirmed real profile is useful only as an example of MO2 profile
shape. Synthetic atomic fixtures, integration-synthetic profiles, and small
controlled real-mod profiles remain the required future correctness path.

## 3. Preflight, environment, and access

The shared Wave B environment is recorded in
[WAVE-B-reference-environment-manifest.md](WAVE-B-reference-environment-manifest.md).
The relevant inputs were:

- Windows AMD64;
- .NET SDK `10.0.302`, host/runtime `10.0.10`;
- the locally installed official Skyrim SE base-game `Data` files associated
  with the candidate runtime;
- no running MO2 process during the read-only probes;
- upstream Mutagen tag `0.54.2`, commit
  `282bb99a77b2df7f1b092b06270e8e3c8fb55463`;
- a disposable probe targeting `net10.0`.

No real profile or third-party mod was parsed. The five official base masters
and `Skyrim - Interface.bsa` were read directly from the game installation.
They were not copied into the repository or documented by private absolute
path.

## 4. Sources and exact versions

All web sources were retrieved on 2026-07-25. Source-code links use the exact
tag commit rather than a moving branch.

| Source | Authority and version | Claim-level relevance |
|---|---|---|
| [NuGet: Mutagen.Bethesda.Skyrim 0.54.2](https://www.nuget.org/packages/Mutagen.Bethesda.Skyrim/0.54.2) | Official package metadata; stable `0.54.2` | Package identity, target frameworks, licence, direct dependencies, repository revision |
| [Mutagen release 0.54.2](https://github.com/Mutagen-Modding/Mutagen/releases/tag/0.54.2) | Official release, 2026-07-08 | Current stable release notes, including a reverted optimization that had caused parsing errors |
| [Mutagen source tag 0.54.2](https://github.com/Mutagen-Modding/Mutagen/tree/0.54.2) | Maintainer source at exact tag/commit | Generated Skyrim model, import, load-order, archive, and strings implementation |
| [Plugin importing documentation](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/docs/plugins/Importing.md) | Official repository documentation | Lazy read-only overlays, mutable import cost, builder behavior |
| [Winning-overrides documentation](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/docs/loadorder/Winning-Overrides.md) | Official repository documentation | Winning-record and override-context API intent |
| [Archive documentation](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/docs/Archives.md) | Official repository documentation | BSA/BA2 reading; no archive writing |
| [Strings documentation](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/docs/Strings.md) | Official repository documentation | Lazy localized-string resolution and stated loose/archive precedence |
| [`BinaryReadParameters`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Plugins/Binary/Parameters/BinaryReadParameters.cs) | Exact implementation | Parallel default and unknown-subrecord option |
| [`StringsFolderLookupFactory`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Strings/DI/StringsFolderLookupFactory.cs) | Exact implementation | Loose strings first, then first applicable archive supplied by archive lookup |
| [`GetApplicableArchivePaths`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Archives/DI/GetApplicableArchivePaths.cs) | Exact implementation | Archive applicability and ordering path; implementation comment labels per-ModKey operation experimental |
| [`CheckArchiveApplicability`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Archives/DI/CheckArchiveApplicability.cs) | Exact implementation | Filename applicability logic used by the standard archive lookup |
| [Issue 675: valid extra BSAs omitted](https://github.com/Mutagen-Modding/Mutagen/issues/675) | Open upstream defect, updated 2026-07-10 | Numbered/extra BSA applicability gap |
| [Issue 678: nullified translated string](https://github.com/Mutagen-Modding/Mutagen/issues/678) | Open upstream defect, created 2026-07-15 | Localized Skyrim SE field can resolve null when a needed archive is omitted |
| [Issue 578: duplicate string handling by load order](https://github.com/Mutagen-Modding/Mutagen/issues/578) | Open upstream defect | Archive string precedence does not generally follow effective load order |
| [Issue 597: CELL `XCIM` parsing](https://github.com/Mutagen-Modding/Mutagen/issues/597) | Open upstream contrary report | Reported field/context discrepancy under a complex Skyrim load order |
| [Issue 537: MagicEffect without `DATA`](https://github.com/Mutagen-Modding/Mutagen/issues/537) | Open upstream contrary report | Reported parser failure on a noncanonical record shape |
| [Issue 506: BSA reader unit tests](https://github.com/Mutagen-Modding/Mutagen/issues/506) | Open upstream test-gap report | Maintainer-recognized limited BSA-reader test coverage |
| [Issue 502: duplicate folders in BSA](https://github.com/Mutagen-Modding/Mutagen/issues/502) | Open upstream boundary report | BSA exception on duplicate folder entries |
| [Issue 468: executable-origin records](https://github.com/Mutagen-Modding/Mutagen/issues/468) | Open upstream coverage question | SkyrimSE.exe-origin records are not proven by plugin-only parsing |

Issues 597 and 537 are contrary reports, not reproduced facts in this
investigation. They are retained because acceptance must not be based only on
happy-path evidence.

### Exact candidate package

The narrow candidate is:

```text
Mutagen.Bethesda.Skyrim 0.54.2
repository commit 282bb99a77b2df7f1b092b06270e8e3c8fb55463
NuGet content hash:
i7Oy40FQy/ppU8oj1x9RrDqB9JOBHpJXk5j1nBIm0NK6mAX9HDWkvLzcs9fMlhVO9Ri7TsgS1gslD+n0oxKT9g==
```

The game-specific package is preferred over the `Mutagen.Bethesda` umbrella
package because Infinium supports only Skyrim SE and gains no product value
from shipping models for unrelated games. This is a package-surface
recommendation within the already accepted Mutagen candidate, not a change to
ADR-0006's project-level identification.

The package targets `net9.0` and `net10.0`. Its package manifest declares the
following direct dependency floors for both targets; the disposable restore
resolved the same versions:

| Package | Manifest constraint | Observed resolution | Observed NuGet content hash |
|---|---:|---:|---|
| `Mutagen.Bethesda.Core` | `>= 0.54.2` | `0.54.2` | `4W3E/y/DHYYmr30H8vhXtCJW3zHusTNKgQlRK7I+tqs5+TC7fxZ7MrUlUzVvn8LZUhMbIJsyX9aBeEfAROLqoQ==` |
| `Loqui` | `>= 3.7.0` | `3.7.0` | `RZtHEVssmRBogMsFtx3AZPQIaXaqZ8gij4+iboIigeKojhmiLn/23Fkqrd4ZbwaeCdfn2ESldJsgJIWfOZxvrA==` |
| `Noggog.CSharpExt` | `>= 4.3.0` | `4.3.0` | `OxvBRNqqhU4vsXZADTA2nUgy2TydqytXQGMScsI7Nks1LkMaWN8IwWJN4JrPDQwnaBfQJnSdP44o9lJtzYGJNQ==` |

The disposable `net10.0` restore resolved this complete graph:

```text
DynamicData/9.4.31
FluentResults/3.15.2
GameFinder.Common/4.9.0
GameFinder.RegistryUtils/4.9.0
GameFinder.StoreHandlers.GOG/4.9.0
GameFinder.StoreHandlers.Steam/4.9.0
GameFinder.StoreHandlers.Xbox/4.9.0
GameFinder.Wine/4.9.0
ini-parser-netstandard/2.5.3
K4os.Compression.LZ4.Streams/1.3.8
K4os.Compression.LZ4/1.3.8
K4os.Hash.xxHash/1.0.8
Loqui/3.7.0
Microsoft.Extensions.DependencyInjection.Abstractions/9.0.4
Microsoft.Extensions.Logging.Abstractions/9.0.4
Mutagen.Bethesda.Core/0.54.2
Mutagen.Bethesda.Kernel/0.54.2
Mutagen.Bethesda.Skyrim/0.54.2
NexusMods.Paths/0.19.1
Noggog.CSharpExt/4.3.0
OneOf/3.0.271
Reloaded.Memory/9.4.2
SharpZipLib/1.4.2
StrongInject/1.4.4
System.IO.Abstractions/22.1.1
System.Reactive/6.1.0
TestableIO.System.IO.Abstractions.Wrappers/22.1.1
TestableIO.System.IO.Abstractions/22.1.1
Testably.Abstractions.FileSystem.Interface/10.1.0
TransparentValueObjects.Abstractions/1.1.0
ValveKeyValue/0.13.1.398
```

That graph is an observation, not a release lock. Any milestone plan adopting
the package must commit a NuGet lock file, restore in locked mode, inventory
licences/notices, and make the lock identity part of analyzer provenance.

## 5. Experiments, artifacts, and side effects

### Experiment A — Exact source and package audit

Steps:

1. Resolve tag `0.54.2` from the official Git repository.
2. Clone that tag into a disposable user-temp directory.
3. inspect the Skyrim project, package metadata, import, load-order, archive,
   strings, and generated-record source;
4. restore `Mutagen.Bethesda.Skyrim` `0.54.2` into a disposable `net10.0`
   console probe;
5. compare the assembly versions, resolved package graph, and NuGet content
   hashes with the package metadata.

Result: source, package metadata, assembly version, and commit agreed on
`0.54.2`/`282bb99a...`.

Reproduction skeleton, using only disposable destinations and a caller-supplied
read-only game `Data` directory:

```powershell
git clone --branch 0.54.2 --depth 1 `
  https://github.com/Mutagen-Modding/Mutagen.git <TEMP_SOURCE>
git -C <TEMP_SOURCE> rev-parse HEAD
dotnet new console --framework net10.0 --output <TEMP_PROBE>
dotnet add <TEMP_PROBE> package Mutagen.Bethesda.Skyrim --version 0.54.2
dotnet restore <TEMP_PROBE>
dotnet run --project <TEMP_PROBE> -c Release -- `
  <TEMP_OUTPUT> <READ_ONLY_GAME_DATA> `
  <TEMP_SOURCE>\Mutagen.Bethesda.Core.UnitTests\Archives\test.bsa
```

The C# probe implemented the exact fixture and access steps described in
experiments B–E. It set `Parallel = false` for malformed-input cases so the
observed failure was attributable to one bounded input. The final disposable
probe identities were:

```text
Probe.csproj
SHA-256 9D29621CE93B0CA0B8CE65AFE42956ABFCB7A224C932722E2085B2D94CCC1F63

Program.cs
SHA-256 9E25FAC7D19609B7886CAF280522F0D14784642D8E1F08EBE11C2E4FDA2116F3

probe-run-4.txt
SHA-256 F1FF9B64B7BE1B8D87AE14E50ADA617A32B3E12403DE047566665C115382807F
```

### Experiment B — Synthetic plugin and override-chain probe

The probe used Mutagen to create:

- one full master with a race and NPC;
- one overriding plugin that changes the NPC name;
- one light plugin with an ESL FormKey;
- an explicit master declaration and link from the NPC to the race.

It then parsed the files read-only, built an ordered load order and link cache,
enumerated the NPC override chain, selected the winner, and resolved the race
link.

Observed output:

```text
synthetic chain=RQ004WinningNpc>RQ004BaseNpc
winner=RQ004WinningNpc
race=RQ004Race
masters=RQ004Base.esm
light=True
lightForm=000800:RQ004Light.esl
```

This verifies API coherence and an internally generated round trip. It is not
independent semantic ground truth because Mutagen produced and consumed the
same fixture.

### Experiment C — Direct BSA and localized-string probe

The probe:

1. read Mutagen's small upstream Skyrim BSA test fixture;
2. enumerated its two files and extracted a known eight-byte payload;
3. opened the installed `Skyrim - Interface.bsa` directly;
4. verified that a Dawnguard English strings file physically exists inside;
5. constructed Mutagen's standard data-folder strings lookup;
6. parsed `Dawnguard.esm` with the data folder and inspected the localized name
   of NPC FormKey `0029A1:Dawnguard.esm`.

Observed output:

```text
archiveFixture files=2
targetBytes=8
sha256=DE4C5AD1B752AC88A0B1E10A91146C9717A14780F8EB3B0B7A671BE980E7ED8E
archiveInterface files=386
dawnguardEnglish=True
strings dawnguardAvailable=
npcLanguages=0
npcString=<null>
```

Direct archive reading worked. The standard strings lookup did not discover
the physically present Dawnguard strings and returned a null localized field.
This locally reproduces the material failure mechanism described by issue 678
under the tested invocation. It does not prove every custom
`IGameEnvironment` or dependency-injection configuration fails.

### Experiment D — Failure and unknown-subrecord probe

The probe supplied:

- empty, seven-byte, and truncated plugins;
- a valid synthetic plugin with one arbitrary byte flipped;
- a syntactically valid unknown zero-length subrecord in `TES4`;
- a syntactically valid unknown zero-length subrecord in an NPC;
- empty, seven-byte, and truncated BSAs;
- empty, short, and oversized-count strings tables.

All malformed inputs returned or threw within 7 ms in this small probe. Plugin
exceptions included `MalformedDataException`, `ModGroupsMalformedException`,
and `SubrecordException`; archive exceptions included `ArchiveException`,
`EndOfStreamException`, and `ArgumentOutOfRangeException`; malformed strings
tables produced `ArgumentException`.

The flipped plugin remained parseable. Unknown-subrecord behavior was:

| Location/mode | Result |
|---|---|
| `TES4`, read-only strict parameter | Accepted |
| `TES4`, mutable lenient | Accepted |
| `TES4`, mutable strict | Accepted |
| NPC, read-only strict parameter | `SubrecordException` |
| NPC, mutable lenient | Accepted |
| NPC, mutable strict | `SubrecordException` |

Therefore strict mode is useful but not a complete “unknown data” detector,
and lenient parsing can intentionally omit unmodeled subrecords. A successful
parse is not proof of content integrity or complete semantic coverage.

### Experiment E — Official-base-master breadth and preliminary cost

For each official base master, the probe performed a read-only import, then
materialized every major record and enumerated all modeled FormLinks.

| File | Bytes | Major records | FormLinks | Elapsed | Managed-memory delta |
|---|---:|---:|---:|---:|---:|
| `Skyrim.esm` | 249,753,412 | 869,687 | 4,397,074 | 9,545 ms | +1,070,783,960 |
| `Update.esm` | 18,429,562 | 16,044 | 225,349 | 813 ms | -975,655,312 |
| `Dawnguard.esm` | 24,813,534 | 95,133 | 498,563 | 661 ms | +54,558,792 |
| `HearthFires.esm` | 3,681,749 | 17,901 | 146,218 | 108 ms | -104,425,088 |
| `Dragonborn.esm` | 64,259,475 | 178,654 | 661,989 | 1,956 ms | +322,688,800 |

The negative deltas reflect forced garbage collection between iterations and
make the memory figures unsuitable as precise benchmarks. The Skyrim.esm
result nevertheless demonstrates that defeating the overlay model by eagerly
materializing all records and all links creates a large high-water cost. This
test establishes breadth and a design warning, not acceptable high-scale
performance.

### Artifact manifest

No raw artifacts were added to the repository.

| Artifact | Location/retention | Purpose |
|---|---|---|
| Exact upstream clone | Disposable user temp; untracked | Source and upstream test-fixture inspection |
| Probe source/build/output | Disposable user temp; untracked | Reproducible experiments B–E |
| NuGet packages | Normal user NuGet cache | Exact package restore and graph inspection |
| Official base-game inputs | Existing game installation; read only | Base-master and interface-BSA smoke tests |

Read-only binary input identities were:

| Input | Bytes | SHA-256 |
|---|---:|---|
| Upstream `test.bsa` at the pinned commit | 187 | `B3C773F13927C4062BF2C970C4AFB1914E3C55943A7FDFF3B4BA7CE680681D85` |
| `Skyrim - Interface.bsa` | 105,799,354 | `5C8D5275EEAAA87EEC84C893DA8DC3BF977E0197EBA86560BB0D1DC651432957` |
| `Skyrim.esm` | 249,753,412 | `2BBC77FDEC35A70EF96B710F8C525E50A1DB9E63E11A391A0EB9EE8F56D36107` |
| `Update.esm` | 18,429,562 | `5F2985B205EA57428164B47E1A5DF57F9B5A1AC0399D4C8B5CF30FC0A60FB008` |
| `Dawnguard.esm` | 24,813,534 | `1208E5153E35366E0ADA1A887720D6D636E2D8592D007FE142B37A57E46B476E` |
| `HearthFires.esm` | 3,681,749 | `70E0D5D6DC42224349D33E8C7BCA73DA447463F671CACC9C15FC0273C93E0008` |
| `Dragonborn.esm` | 64,259,475 | `3B8BF5EAD27337F829FA4D474F0363324124A9696D33FE1AEE7B01262EFF5BD1` |

The synthetic outputs contain no private paths, credentials, or third-party
mod content. The local probe and clone may be deleted after review.

### Observed side effects and authorization result

| Operation | Observed write/effect | Product authorization relevance |
|---|---|---|
| First attempted `dotnet` restore invocation | .NET first-run initialization occurred and installed the normal ASP.NET HTTPS development certificate before the malformed `--locked-mode:$false` argument failed | Tool-owned development-machine effect; it must not be confused with an Infinium scan effect |
| NuGet restore/build | Wrote package/cache/build data under the user NuGet cache and disposable temp root | Acceptable research-only tool cache; a production restore is a build/install concern, not a scan operation |
| Synthetic probe | Wrote only to the disposable probe-output directory | No game/MO2 mutation |
| Base-master/BSA reads | Opened existing files read-only | Consistent with AUTH-001 |
| Protected-input verification | Rehashed all five base masters after probing; hashes matched the shared preflight | No detected mutation |

No external modding application or game process was launched. No setup file was
written. The product must not invoke `dotnet restore`, mutate trust stores, or
perform build-time package acquisition during a user scan.

## 6. Findings

### F1 — The exact current stable candidate is the narrow Skyrim package

As of 2026-07-25, `Mutagen.Bethesda.Skyrim` `0.54.2` is the current stable
game-specific package. Newer visible versions are prereleases and were not
evaluated. The package is tied to exact source commit `282bb99a...`, targets
.NET 9 and .NET 10, and is GPL-3.0-only.

Interpretation: `0.54.2` is the only version this report can recommend, and
only under the restrictions below. “Latest Mutagen” is not a valid production
selector.

### F2 — Mutagen provides useful typed plugin semantics

The source and local probes demonstrate:

- typed Skyrim record groups and getters;
- full-plugin and ESL FormKeys;
- master declarations;
- read-only lazy overlays;
- FormLink enumeration and resolution through a link cache;
- ordered override-chain and winning-record APIs;
- direct BSA enumeration and extraction.

These capabilities are materially better than treating plugins as unstructured
bytes and are suitable foundations for candidate-first deterministic analysis.

### F3 — The probe does not prove field correctness

The synthetic plugin round trip used Mutagen on both sides. The official
base-master test proved that broad parsing and enumeration completed, but did
not compare field values with independently specified expectations. Generated
API breadth is not evidence
that every field, optional shape, record family, executable-origin record, or
noncanonical real-mod encoding is modeled correctly.

Interpretation: target record families and target fields require a positive
allowlist established through EVAL-0052. Everything else is
`unsupported`, `not evaluated`, or `failed`, never implicitly “covered.”

### F4 — Mutagen must not own effective load-order authority

The winning-override API's results are meaningful only relative to the ordered
mods supplied to it. RQ-001, not Mutagen's discovery conveniences, must
provide:

- exact enabled plugin set and order;
- exact winning plugin bytes;
- master/light identity;
- exact loose-file providers plus enabled archive identities, physical
  providers, and relevant order/configuration inputs;
- changed-during-capture and snapshot validity.

Using a “typical” game environment or automatically discovered data folder as
local-state authority would violate the accepted trust model.

RQ-004 must still turn the captured archive inputs into validated
archive-member and winner semantics. RQ-001 supplies the authoritative
physical/configuration inputs; it does not make Mutagen's archive decisions
correct automatically.

### F5 — Direct archive byte access is useful, but archive applicability is not
authoritative

`Archive.CreateReader` successfully read and extracted from both the upstream
fixture and the installed Skyrim SE BSA. That low-level reader can be useful
when Infinium supplies the exact archive population and provider order.

The standard applicability path is not sufficient for MO2:

- its own source labels per-ModKey operation experimental;
- filename matching omits valid numbered/extra archives, as open issue 675
  reports;
- its ordering inputs are game/INI/load-order oriented rather than an
  authoritative MO2 provider graph;
- archive activation and asset precedence are separate from plugin override
  order.

Interpretation: accept low-level archive reading conditionally; reject
Mutagen's standard archive discovery/applicability/order as Infinium local
truth.

### F6 — Standard localized-string resolution is a blocking gap at 0.54.2

The tested standard lookup failed to discover a physically present Dawnguard
strings file and returned a null localized NPC name. Open issues 678 and 578
show that archive applicability and duplicate-string precedence remain active
upstream concerns.

This is not merely cosmetic. A null or wrong localized field can change
evidence, purpose inference, user-facing record identity, and downstream
semantic conclusions.

Interpretation: 0.54.2 is not acceptable as an authoritative end-to-end
archive/string environment. Before a localized field is supported, Infinium
must either:

1. supply a provider-aware custom strings source/resolver built from
   RQ-001's effective archive state and validate it against independently
   specified fixtures and observable game-format behavior; or
2. adopt a later exact Mutagen version whose relevant fixes pass the same
   tests.

Until then, localized fields depending on an unresolved strings table are
coverage gaps.

### F7 — Failure is quick in small probes but not yet production-bounded

Malformed samples failed quickly, but exception types vary by layer and one
arbitrary byte change remained valid. Strict unknown-subrecord mode caught an
unknown NPC subrecord but not an unknown TES4 subrecord.

Interpretation: the integration must:

- normalize parser/archive/strings failures into typed evidence and coverage
  states;
- isolate work at least per plugin/archive and preferably in a killable worker
  boundary for untrusted large inputs;
- impose cancellation, time, memory, and output budgets;
- record the exact failing input identity and operation;
- avoid converting “parsed” into “valid” or “complete”;
- treat observed unknown/unmodeled content as a gap even if a lenient route
  can continue.

The current evidence does not prove resistance to decompression bombs,
pathological record counts, cyclic/reference explosions, or every malformed
binary shape.

### F8 — Lazy, candidate-scoped access is required for scale

Mutagen's overlay design can avoid parsing untouched fields. The deliberately
eager Skyrim.esm pass erased much of that advantage and showed a large
time/memory high-water cost on one master alone.

Interpretation: high-scale analysis should:

- build cheap structural indexes first;
- select relevant record families and candidate chains;
- access each needed overlay property once;
- avoid `ToArray()` over all records/links except bounded index stages;
- checkpoint derived indexes against immutable input hashes and exact parser
  configuration;
- measure realistic atomic, integration, controlled-real-mod, and scale
  fixtures under RQ-027 before setting budgets.

This report does not establish that Mutagen is performant enough for a
thousand-mod exhaustive scan. It establishes a plausible route that still
needs measurement.

### F9 — Record-family support must follow RQ-036 taxonomy research

Mutagen's generated record groups describe technical serialization surfaces;
they do not define product consequence, severity, symptom, or purpose
taxonomies. RQ-004 must map parser capability and gaps into RQ-036 rather than
inventing an incompatible list.

The eventual semantic allowlist should be a matrix of:

- exact parser/package and supported runtime;
- record family and exact fields;
- supported shapes/flags/localization state;
- link and override-chain behavior;
- deterministic authority and independently specified validation evidence;
- known exclusions and failure modes;
- analyzer(s) consuming the result.

### F10 — Mutagen still requires parser-independent qualification

The probe used Mutagen on both the writer and reader sides for part of its
synthetic coverage. That is useful for integration smoke testing but cannot
independently establish semantic correctness. Therefore EVAL-0052 is not yet
passed.

Interpretation: Mutagen may be the runtime semantic library, but expected
results must come from hand-audited binary fixtures, direct byte/structure
assertions, format invariants, matched negative and malformed cases,
metamorphic variants, official-master invariants, and documented manual
adjudication. ADR-0007 explicitly rejects an external xEdit oracle.

### F11 — Package reproducibility needs more than a direct version

An exact direct `PackageReference` still resolved a sizable transitive graph.
Version `0.54.2` also follows a release that reverted a parsing regression,
which is evidence that apparently performance-oriented changes can alter
correctness.

Interpretation: adoption requires:

- locked restore with exact content hashes;
- SBOM and licence/notice inventory;
- analyzer provenance containing package/lock identity;
- regression execution before every version advance;
- no floating range or automatic “latest stable” upgrade.

## 7. Alternatives evaluated

| Alternative | Correctness/ground truth | Performance/integration | Failure and maintenance | Disposition |
|---|---|---|---|---|
| `Mutagen.Bethesda.Skyrim` 0.54.2 for plugin semantics plus Infinium-owned effective state, direct archive indexing, and provider-aware strings | Plausible when each supported field passes parser-independent fixture qualification; preserves gaps | In-process typed/lazy API; candidate-scoped path is plausible | Requires worker isolation, normalization, string workaround, exact lock | **Recommended conditionally** |
| Mutagen 0.54.2 “typical environment” as the complete plugin/archive/string authority | Locally contradicted for Dawnguard strings; wrong authority boundary for MO2 | Convenient | Open applicability/precedence defects; can silently null data | **Reject** |
| A later Mutagen prerelease or future stable | May contain fixes | Unknown | Exact behavior, graph, and regressions untested | **Defer; rerun full gate on an exact version** |
| xEdit in any product, development, or evaluation role | Mature external inspection tool, but unnecessary for the selected boundary | Adds a second application/invocation contract | Adds dependency, version, write, and authority complexity | **Reject under ADR-0007** |
| Build a bespoke Skyrim plugin/BSA/strings parser | Full control in theory | Very high implementation and test cost | Duplicates mature domain work; large correctness burden | **Reject for M1 unless Mutagen and independent fixture evidence later fail the gate** |
| Parse raw plugin headers only and avoid semantic records | Strong for a small structural subset | Cheap | Cannot satisfy meaningful record interaction analysis | **Use only as a bounded structural/index layer** |

Rejection criteria for Mutagen remain:

- no independently verified path for an M1-required record field or override
  shape;
- unresolved silent loss/corruption in an M1-required localized field;
- inability to isolate pathological inputs within accepted resource limits;
- disagreement with independently specified expectations on supported winners,
  FormKeys, links, or field values that cannot be bounded as an explicit
  unsupported shape;
- a package/runtime constraint incompatible with the later accepted stack;
- an unacceptable dependency/licence/security finding in the exact locked
  graph.

## 8. Contrary evidence, uncertainty, and unsupported cases

### Contrary evidence

- Release 0.54.2 explicitly reverted an optimization that caused parsing
  errors in specific scenarios.
- Open issues 675, 678, and 578 contradict complete archive/string coverage.
- Open issues 597, 537, 502, 506, and 468 provide record-, archive-, test-, and
  executable-origin boundary evidence.
- The local Dawnguard probe demonstrated a user-visible localized-field loss.
- Strict unknown-subrecord behavior differed by record location.
- The arbitrary byte flip remained parseable, showing that parse success is
  not file-integrity verification.

### Material uncertainty

- No parser-independent field-level qualification has been completed.
- No controlled real-mod corpus was evaluated.
- No high-end MO2 profile or scale fixture was benchmarked.
- No archive-vs-loose or archive-vs-archive MO2 provider matrix was tested.
- The correct provider-aware string-resolution implementation is not yet
  selected.
- Executable-origin records, injected records, deleted records, compressed
  records, unusual master styles, ONAM behavior, and many noncanonical record
  shapes remain untested.
- The small malformed corpus cannot establish hard time or memory bounds.
- A later exact Mutagen release may fix or regress any observed behavior.
- .NET 9 versus .NET 10 application selection is not decided by this report.

### Unsupported until follow-up

- any record family/field absent from the reviewed EVAL-0052 allowlist;
- localized fields whose effective strings provider cannot be resolved;
- automatic BSA applicability or precedence inferred only by Mutagen;
- an archive population not supplied by the authoritative RQ-001 state;
- unknown or unmodeled subrecords where their semantic effect is not proven
  irrelevant;
- malformed inputs that exceed the accepted worker budget;
- another runtime/game/platform;
- claims based on SkyrimSE.exe-origin data not represented in the supported
  input model;
- whole-list performance or completeness inferred from official masters.

## 9. Recommendation

### Recommended answer

Adopt `Mutagen.Bethesda.Skyrim` `0.54.2` as the **provisional, pinned
game-specific semantic-library candidate**, not as a complete environment or
universal parser. Use it for allowlisted Skyrim SE plugin records, links, and
override chains and for low-level reading of archives whose exact population
and effective order the combined RQ-001/RQ-004 contract supplies. Do not use
its typical environment, automatic load-order discovery, standard archive
applicability/order, or standard strings lookup as authoritative MO2 state.

Confidence is:

- **high** that 0.54.2 provides useful typed/lazy plugin and direct BSA APIs;
- **high** that its standard archive/string environment is insufficient for
  Infinium at this version;
- **medium** that a provider-aware integration can make it suitable for M1;
- **low** that broad record-family correctness or high-end performance is
  already established.

### Preconditions for architectural acceptance

1. RQ-001 provides an authoritative, immutable effective plugin/provider/
   archive snapshot rather than allowing Mutagen to rediscover one.
2. ADR-0007 remains enforced: no xEdit dependency or oracle is introduced.
3. EVAL-0052 is expanded and passed for every M1 record family, field, link,
   override shape, and localization state.
4. The archive/string gap is solved by a provider-aware resolver or a later
   exact version and validated against independent ground truth.
5. Parser operations run in a failure-isolated, cancellable, budgeted boundary
   and emit typed failures/gaps.
6. RQ-027 measures lazy/candidate-scoped behavior on atomic, integration,
   controlled-real-mod, and scale fixtures.
7. The exact dependency graph is locked, inventoried, and included in
   provenance.
8. The supported field/family matrix maps to RQ-036 without defining severity,
   symptom, consequence, or purpose categories from serialization types.
9. The Wave B integration review confirms compatibility with the runtime and
   snapshot contracts.

Until these preconditions pass, a milestone may use Mutagen in a research
fixture but may not represent Bethesda semantic coverage as generally
supported.

## 10. Downstream work enabled

### Proposed Bethesda semantic layer ADR

The ADR should decide:

- direct package `Mutagen.Bethesda.Skyrim`, exact accepted version and source
  revision;
- locked dependency and update policy;
- authoritative state inputs from the MO2 snapshot layer;
- approved plugin, archive, and strings responsibilities;
- explicit rejection of typical-environment and automatic discovery as state
  authority;
- record/field allowlist and coverage-gap contract;
- parser-independent fixture, review, and release-gate requirements;
- worker isolation, cancellation, budget, and normalized failure schema;
- version-advance and regression procedure.

ADR-0009 accepts the semantic boundary and pinned initial dependency. The
preconditions in section 9 remain qualification gates before any corresponding
implementation or supported-shape claim.

### Proposed EVAL-0052 expansion

EVAL-0052 should use independent expected outputs and include:

1. full, master, and light plugin identity and FormKey handling;
2. explicit masters, missing masters, and full/light override chains;
3. winning records, all contexts, deletions, injected references, and
   compressed records where applicable;
4. links across masters and unresolved-link behavior;
5. each M1-selected record family and exact consumed fields;
6. localized strings from loose files, base-game BSAs, mod BSAs, duplicate
   IDs, and archive/loose precedence supplied by RQ-001;
7. archives with conventional, suffixed, numbered/extra, disabled, and
   competing names;
8. unknown subrecords in headers and ordinary records;
9. malformed/truncated plugins, archives, and strings tables;
10. a changed-during-capture input and exact abstention;
11. renamed mods, unrelated reordering, and equivalent synthetic variants to
    prevent fixture-name rules;
12. agreement with independently specified expectations for exact winners,
    FormKeys, links, and values, with every unmodeled shape producing an
    explicit coverage gap.

Mutagen-generated fixtures alone must not be the oracle. Use hand-audited
binary fixtures, direct byte and structural assertions, documented format
invariants, matched negative/malformed cases, metamorphic variants,
official-master invariants, and reviewed expected values as appropriate.

### Proposed registry and specification updates

After independent review, the coordinator should propose:

- RQ-004 status text from section 11;
- source-registry entries for the exact NuGet package/tag and relevant open
  issues;
- EVAL-0052 detail from this section;
- an RQ-036 capability mapping from Mutagen record/field surfaces, without
  treating them as final product taxonomies;
- RQ-027 benchmark cases for lazy/candidate-scoped semantic access;
- the Bethesda semantic layer ADR above.

The coordinated Wave B acceptance applies these updates through ADR-0009, the
research registry, evaluation catalog, and M0 plan.

### Follow-up research

- RQ-001: exact plugin/archive/provider input contract.
- ADR-0007/RQ-006: retain complete xEdit exclusion and parser-independent
  ground-truth requirements.
- RQ-014: capture consistency, hashes, and invalidation.
- RQ-027: performance budgets and realistic scale tests.
- RQ-036: technical modification-surface and game-area taxonomy.
- Focused archive/string follow-up: archive activation and member precedence,
  archive-vs-archive and loose-vs-archive winners, provider-aware
  strings-source interface, language fallback, and issue 675/678/578
  conformance.
- Focused semantic matrix: parser-independent qualification for the record
  families selected for the M1 vertical proof.

### Gate B contribution

This investigation does **not** establish Gate B by itself. It supplies a
defensible conditional plugin-semantic route and identifies blocking
archive/string and independent-ground-truth work. It did not validate Skyrim's
effective archive-member, archive-vs-archive, or loose-vs-archive winner
semantics. At this report's completion, Gate B was therefore not met for any
M1 path that depended on archived
asset precedence or localized archive strings until the RQ-001/RQ-004
provider-aware route passes the applicable EVAL-0051/EVAL-0052 cases. The full
wave also remains dependent on RQ-003, RQ-014, and any selected LOOT
operation.

## 11. Suggested RQ-004 status

Accepted registry status:

> **Resolved for M0 by ADR-0009; supported-shape qualification pending.**
> Mutagen `0.54.2` is the accepted initial semantic dependency, while
> parser-independent field/override qualification, provider-aware
> archive/string resolution, and performance/failure gates remain required.
> Standard Mutagen environment conveniences remain excluded.

## 12. Requirements and evidence traceability

| Requirement/decision | Evidence or finding | Proposed verification/disposition |
|---|---|---|
| SCOPE-001, ADR-0004 | Exact Skyrim package/revision; official base-master smoke test; no cross-game claim | Bind ADR and EVAL-0052 to the accepted runtime identity |
| SCOPE-005 | F2–F6 separate plugin semantics from MO2/provider/archive/string authority | RQ-001 input contract plus Bethesda semantic ADR |
| AUTH-001–AUTH-003, ADR-0003 | Section 5 accounts for all reads/writes; no setup mutation or tool launch | Production worker may write only Infinium-owned cache/checkpoint state |
| SCAN-001 | Typed semantics can be exposed as bounded record-family analyzers | Declare one analyzer contract per supported family/use |
| SCAN-006 | Small malformed inputs failed quickly but heterogeneously | Killable worker, normalized failures, adversarial budget evaluation |
| SCAN-007 | Exact package/input identities are available; cache correctness not proven | RQ-014 dependency/invalidation contract |
| SCAN-008 | F8 shows eager access is unsuitable as a default | RQ-027 measured lazy/candidate budgets |
| SNAP-001, SNAP-002 | Parser needs immutable authoritative bytes and changed-input rejection | RQ-001/RQ-014 snapshot integration |
| SNAP-005 | Exact package, source commit, graph, and settings identified | Locked restore plus analyzer-configuration provenance |
| EVID-001 | FormKeys, chains, links, parse failures, and gaps can be typed | Semantic evidence schema in proposed ADR |
| EVID-002 | Package hashes, source revision, input hashes, and probe environment recorded | Include equivalent identities in every production run |
| EVID-006 | F3, F6, and F7 require abstention rather than inferred semantics | EVAL-0052 unsupported/failed cases |
| COVER-001–COVER-003 | F3/F6/F9 define positive allowlisting and explicit gaps | Reviewed record/field/localization capability matrix |
| ANALYSIS-003 | Override-chain and link APIs are plausible inputs, not a conflict dump | EVAL-0052 plus M1 meaningful-interaction analyzer |
| ANALYSIS-016 | Section 9 defines evidence, costs, outputs, and failure preconditions | Milestone analyzer-contract review |
| ADR-0001 | Mutagen consumes deterministic bytes but does not own local-state discovery | RQ-001 supplies exact authoritative inputs |
| ADR-0002 | Package/input identity and immutable snapshots are explicit dependencies | Never mix records/strings/providers across snapshots |
| ADR-0006 | Exact package remains GPL-3.0-only and technically conditional | Lock/SBOM/notices review; no new licensing decision |
| EVAL-0052 | Synthetic round trip and base breadth are insufficient; independent fixture qualification not run | Expand and execute the independent matrix in section 10 |
| RQ-036 | Serialization groups are only technical surfaces | Map supported fields/families into researched taxonomy |
| Gate B | Conditional route exists; string and independent conformance gaps block general support | Wave B integration must assess exact M1 surfaces |

## Investigation conclusion

> **Mutagen.Bethesda is useful but not self-sufficient.** Pin
> `Mutagen.Bethesda.Skyrim` `0.54.2` only as the provisional semantic parser for
> independently verified record/field families and as a low-level reader for
> explicitly supplied archives. Infinium must own authoritative MO2 state and
> provider precedence, must not trust the standard archive/string environment
> at this version, and must preserve unsupported semantics as coverage gaps.
> Parser-independent conformance, provider-aware localized strings, failure isolation, and
> scale measurement remain preconditions to the applicable qualified support
> claims; ADR-0009 later accepted the bounded dependency itself.
