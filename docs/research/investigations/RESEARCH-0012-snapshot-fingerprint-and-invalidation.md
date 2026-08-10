# RESEARCH-0012: Snapshot fingerprint and dependency invalidation

Status: Completed
Disposition: recommendation accepted by ADR-0010
Date: 2026-07-25  
Last reviewed: 2026-07-25  
Researcher: Codex agent  
Primary RQ: RQ-014 — Which fingerprint/dependency strategy proves
installation-snapshot and cache validity without prohibitive IO?  
M0 wave: Wave B  
Decision enabled: snapshot/cache ADR and the snapshot-validity portion of
Gate B

Accepted disposition:
[ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
accepts the canonical structural manifest, scoped SHA-256, quiescent double
capture, dependency-closure, and invalidation/reuse boundary. Exact schema
implementation and conformance remain downstream gates.

## 1. Question and accepted constraints

This investigation asks how Infinium can:

1. identify one quiescent, explicitly selected MO2/profile/game state;
2. detect changes during capture and later analysis;
3. prove that a cached artifact's declared inputs are still equivalent;
4. avoid re-reading hundreds of gigabytes or opening hundreds of thousands of
   small files when the consuming operation does not depend on their bytes;
5. preserve exact provenance without making modification time, a file name, or
   guessed ownership authoritative; and
6. invalidate only dependent work.

The answer is constrained by:

- [SNAP-001 through SNAP-006](../../product/requirements.md#snapshot-and-reproducibility),
  which require immutable snapshot/run binding, mid-operation change
  detection, visible staleness, dependency-proven carryover, and honest replay
  disclosure;
- [SCAN-007](../../product/requirements.md#scan-007--incremental-reuse), which
  permits reuse only when all declared inputs remain valid;
- [EVID-002](../../product/requirements.md#evid-002--provenance), which
  requires input fingerprints and exact analyzer/tool/model identities;
- [OPS-004](../../product/requirements.md#ops-004--high-end-scale), which
  requires millions-of-entry and multi-hour high-end operation by M3;
- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety),
  which prohibit changes to protected setup state;
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md),
  which makes deterministic local state authoritative;
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md),
  which separates immutable snapshot origin from consuming-run reuse;
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md),
  which permits only non-mutating observation; and
- [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md),
  which permits a concrete Windows/NTFS/MO2 solution without pretending to
  support another filesystem, manager, or game.

This report recommends a mechanism. It does not accept a storage schema,
implementation stack, hash library, or ADR.

## 2. Scope, non-scope, and preflight

### In scope

- MO2 profile/control inputs and ordered provider roots;
- physical provider entries, game Data/root inputs, plugins, archives, loose
  files, configuration, tools, rulesets, and generated-output manifests;
- content hashing, metadata tuples, file IDs, canonical directory manifests,
  hierarchical/dependency roots, and lazy/tiered hashing;
- rename, reorder, same-size/same-time byte changes, metadata-only changes,
  archive-member changes, and mid-capture mutation;
- NTFS file identity, reparse points, sharing semantics, and the bounded
  potential of the NTFS change journal;
- dependency-aware cache and artifact invalidation; and
- measured read-only IO on synthetic inputs and one bounded aggregate view of
  the user-confirmed reference profile.

### Explicitly out of scope

- continuous monitoring or filesystem-change-triggered analysis;
- selecting a database, job system, worker topology, or application stack;
- using a filesystem watcher as proof;
- creating, modifying, deleting, or requiring an NTFS change journal;
- proving adversarial tamper resistance against a privileged attacker;
- snapshotting open Skyrim/MO2 processes or runtime memory;
- copying a complete effective tree;
- treating `<REFERENCE_PROFILE>` as correct, representative, a performance
  requirement, or a reusable fixture; and
- defining archive load/winner semantics that belong to RQ-001/RQ-004.

### Access and effects

- Local private data: read-only aggregate enumeration and bounded byte reads
  from `<REFERENCE_PROFILE>` providers. No raw names, paths, or content were
  retained in this report.
- Network: unauthenticated access to current Microsoft and NIST primary
  documentation only.
- Authenticated APIs, credentials, paid providers, LLM calls, MO2, Skyrim,
  LOOT execution: none.
- Writes: one four-file synthetic mutation fixture under the OS temporary
  directory. It was deleted after the observations. No protected setup or
  repository file was changed by the probe.
- Stopping conditions: any protected-state write, inaccessible population that
  would be silently skipped, need to launch a user tool, or benchmark whose
  cost ceased to be bounded. One broad benchmark was stopped at 300 seconds
  and is reported as censored rather than inferred.

The reference-profile control fingerprints from the
[Wave B manifest](WAVE-B-reference-environment-manifest.md) were checked
before the experiments. A final check is recorded in section 4.5.

## 3. Sources and exact versions

All web sources were retrieved on 2026-07-25.

| Source | Exact identity | Relevance |
|---|---|---|
| [NIST FIPS 180-4](https://csrc.nist.gov/pubs/fips/180-4/upd1/final) | Final publication, August 2015; revision is planned but not yet published | Specifies SHA-256 and its change-detection/collision-resistance properties. |
| [Microsoft `FILE_ID_INFO`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info) | Microsoft Learn, last updated 2024-02-22 | A volume serial number plus 128-bit file ID identifies an open file on one computer; this is identity, not content. |
| [Microsoft `GetFileInformationByHandle`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle) | Microsoft Learn, last updated 2022-07-27 | Handle-based size, time, link count, volume, and file-index observations used by the probe. |
| [Microsoft `CreateFile`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea) | Current Microsoft Learn contract retrieved 2026-07-25 | Conflicting access/share modes fail; sharing restrictions remain until the handle closes; delete sharing also governs rename. |
| [Microsoft `WriteFile`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-writefile) | Current Microsoft Learn contract retrieved 2026-07-25 | Last-write time is not fully updated until writing handles close; time alone is therefore not a race-proof identity. |
| [Microsoft reparse points](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points) and [operations](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-operations) | Learn pages last updated 2025-07-09 and 2021-01-07 respectively | Reparse points can redirect normal filesystem behavior and have tagged payloads; they must not be followed invisibly. |
| [Microsoft file streams](https://learn.microsoft.com/en-us/windows/win32/fileio/file-streams) | Current Microsoft Learn page retrieved 2026-07-25 | NTFS files may contain named streams; ordinary default-stream hashing does not enumerate every stream. |
| [Microsoft change-journal records](https://learn.microsoft.com/en-us/windows/win32/fileio/change-journal-records) | Microsoft Learn, last updated 2021-01-07 | The NTFS journal records facts/reasons for changes but may delete old records and does not store reversible content. |
| [Microsoft change-journal identifier](https://learn.microsoft.com/en-us/windows/win32/fileio/using-the-change-journal-identifier) | Microsoft Learn, last updated 2021-01-07 | Journal ID plus USN continuity is required; reset/recreation makes prior records unusable and the documented operation requires administrator authority. |
| [RESEARCH-0005](RESEARCH-0005-mo2-effective-state-acquisition.md) | Completed, 2026-07-25 | Supplies the quiescent, version-pinned provider/control-input model and excludes real-instance MO2/USVFS observation. |
| [RESEARCH-0006](RESEARCH-0006-mo2-profile-selection-semantics.md) | Completed, 2026-07-25 | Separates a disk suggestion from explicit selection and snapshot binding. |
| [RESEARCH-0007](RESEARCH-0007-skyrim-runtime-support-contract.md) | Completed, 2026-07-25 | Supplies the pinned runtime and support-manifest dependencies. |
| [RESEARCH-0008](RESEARCH-0008-mutagen-bethesda-semantic-capability.md) | Completed, 2026-07-25 | Requires exact plugin/archive/string inputs and parser versioning; rejects standard archive discovery as local-state authority. |
| [RESEARCH-0009](RESEARCH-0009-loot-integration-and-data-contract.md) | Completed, 2026-07-25 | Requires exact engine, masterlist, prelude, userlist, configuration, and effective-state dependencies for LOOT-derived work. |

### Local benchmark identity

| Component | Observed identity |
|---|---|
| OS | Windows 11 Home `10.0.26200`, build `26200` |
| CPU | AMD Ryzen 9 7950X3D, 16 cores / 32 logical processors |
| Memory | 31.1 GiB visible |
| Volume | Local `Z:` NTFS, 4 KiB allocation unit |
| Device | Crucial `CT4000P3PSSD8`, NVMe |
| Runtime | .NET SDK `10.0.302`; Microsoft.NETCore.App `10.0.10` |
| MO2/profile/runtime/tool subjects | Exact identities in the Wave B reference manifest |

These facts describe one machine on one date. They are not product minimums or
calibrated user estimates.

## 4. Experiments, manifests, and results

### 4.1 Reference-provider metadata-manifest pass

Disposable probe identity: `rq014-inline-v1`, PowerShell `7.6.3`-hosted C#
compiled in memory against .NET `10.0.10`. It had no repository commit and was
not retained as production/research source. The exact population/selection and
API behavior needed to reproduce the measurement are recorded below; the lack
of a retained source artifact prevents byte-for-byte probe replay and is a
stated limitation.

The read-only probe:

1. parsed `modlist.txt` only to locate enabled physical mod directories;
2. added the instance overwrite directory;
3. traversed each root in deterministic root and per-directory name order;
4. did not follow reparse points;
5. counted visited directories and, for each file, observed normalized relative
   path, root ordinal, length, last-write UTC ticks, and attributes;
6. fed a length-delimited equivalent of those values to SHA-256; and
7. emitted aggregates only.

The benchmark encoding is not the proposed production encoding: it lowercased
paths and used the probe's comparator rather than a conformance-tested MO2
adapter comparator.

| Observation | Result |
|---|---:|
| Enabled provider/overwrite roots found | 1,792 |
| Directories visited | 28,971 |
| Files observed | 244,626 |
| Aggregate logical bytes | 254,193,831,116 |
| Plugin files / bytes | 2,375 / 495,622,977 |
| Archive files / bytes | 377 / 63,997,741,439 |
| Files at most 1 MiB / bytes | 218,762 / 21,469,796,149 |
| Access errors | 0 |
| Reparse entries | 0 |
| Elapsed | 20.354 s |
| Benchmark-schema manifest SHA-256 | `D47BBA4C301B4F9445E1063ECD379A3B4FA045A264F1C82D4255F4285566F96B` |

The population excludes physical game Data/root state, disabled providers, and
some integration-specific inputs. It is a bounded real-shape observation, not
a complete installation snapshot.

### 4.2 File-open and SHA-256 measurements

Files were selected in stable root/traversal order. Each hashed file was opened
read-only with `FileShare.Read`, observed by handle before and after streaming,
and hashed with SHA-256. Results are single runs with uncontrolled Windows
cache, antivirus, and background IO. No changed-during-read condition or open
error was observed.

The stable selection was: first 10,000 files for handle identity; every
`.esp`/`.esm`/`.esl`; archive files until the first stable-order item that took
the subset past 4 GiB; first 20,000 non-plugin/non-archive files at most 64 KiB;
and medium non-plugin/non-archive files greater than 64 KiB and at most 16 MiB
until the subset passed 2 GiB. Content reads used a 1 MiB buffer and sequential
scan hint. The handle-identity pass opened without reading content.

| Population | Files | Bytes read/observed | Elapsed | Rate |
|---|---:|---:|---:|---:|
| Handle/file-ID metadata, first 10,000 files | 10,000 | 2,660,251,311 represented bytes; contents not read | 90.222 s | 110.8 files/s |
| All plugins | 2,375 | 495,622,977 | 17.316 s | 27.3 MiB/s; 137.2 files/s |
| First archive subset past 4 GiB bound | 7 | 5,049,988,821 | 5.608 s | 858.8 MiB/s |
| First 20,000 small loose files, at most 64 KiB each | 20,000 | 237,853,422 | 88.082 s | 2.58 MiB/s; 227.1 files/s |
| First medium loose subset past 2 GiB bound | 7,310 | 2,147,641,545 | 64.482 s | 31.8 MiB/s; 113.4 files/s |

An earlier combined attempt opened all files for file identity and then
scheduled broader plugin/archive/loose hashing. It produced no completed result
before the 300-second bound and was terminated by the runner. It is valid
censored evidence that this implementation of mandatory per-entry opens is
not a cheap capture primitive; it is not a throughput measurement.

Interpretation:

- bulk sequential archive hashing is fast on this machine;
- file count/open latency dominates small and medium provider populations;
- a double metadata-manifest pass is tens of seconds for this observed
  population;
- opening every entry for a file ID or strong hash on every scan would add
  minutes here and could become prohibitive at OPS-004 scale; and
- scoped plugin hashing is cheap enough for the initial semantic proof.

### 4.3 Synthetic invalidation controls

A four-file fixture was created under the OS temporary directory. It was not a
Skyrim-format fixture; it isolated filesystem and container-identity behavior.

| Change | Metadata result | Strong/content result | Required invalidation |
|---|---|---|---|
| Overwrite 4 KiB with different bytes; restore original length, time, and attributes | Tuple remained identical | SHA-256 changed | Every byte-dependent artifact invalidates; metadata cannot validate reuse |
| Change only last-write time | Metadata changed | SHA-256 unchanged | Snapshot observation changes; byte-only parse artifacts may carry if attributes/time are not declared semantic inputs |
| Rename file without changing bytes | Path changed | Content hash unchanged | Provider/path/winner outputs invalidate; a path-independent content parse may carry by explicit reuse |
| Reorder equal-length profile lines | Length unchanged | File hash changed | Ordered profile/provider/load-order derivations invalidate |
| Replace one same-length member in a same-length container; restore container time | Container length/time remained identical | Container SHA-256 changed | Archive index and member/provider derivations invalidate |

The container control used ZIP only to test the identity proposition. It does
not establish BSA load, compression, member, or winner semantics.

### 4.4 Sharing and mid-read controls

On the synthetic file:

- a reader opened with read access and `FileShare.Read`; a new writer open was
  rejected;
- a writer opened first with write access and permissive sharing; a later
  read-only/`FileShare.Read` open was rejected.

This agrees with the `CreateFile` sharing contract and supplies a useful
per-file capture primitive: open the consumed input with read sharing only,
observe handle metadata, hash and parse the same byte stream, observe handle
metadata again, then close.

It is not a global transaction:

- it does not freeze directories or other files;
- it does not protect a file that the analyzer has not opened;
- a filter, unsupported filesystem behavior, named stream, reparse target, or
  privileged adversary may require separate treatment; and
- a long-running analysis must still validate its declared dependencies and
  provider structure rather than trusting the initial timestamp.

### 4.5 Artifact retention and final input check

Retained in the repository:

- this procedure;
- sanitized aggregate counts and timings;
- mutation truth-table results; and
- primary-source/version links.

Not retained:

- the temporary fixture;
- benchmark source/binaries;
- raw file names, mod names, relative paths, file IDs, source bytes, or
  per-file hashes; and
- the private token-to-path mapping.

The temporary fixture contained four files and was deleted from the OS
temporary directory after the probe. During final review, the provider pass
reproduced all counts and the exact benchmark-schema manifest SHA-256 above in
10.918 seconds. All six profile-control SHA-256 values and all four
runtime/MO2/LOOT executable SHA-256 values also matched the Wave B
manifest, and no MO2 process was running. A mismatch would have invalidated the
corresponding benchmark context rather than being explained away.

## 5. Findings

### F1 — A metadata tuple is a change detector, not content identity

Length, time, attributes, path, and file ID are valuable structural
observations. None proves bytes. The synthetic same-size/same-time overwrite
preserved the tuple and changed SHA-256. Windows also permits explicit time
changes, and `WriteFile` documents delayed final last-write updates.

Consequence: modification time or `(size, mtime)` may select work that *might*
be reusable, but it cannot authorize byte-dependent reuse by itself.

### F2 — File ID proves object/alias identity only within its declared scope

`FILE_ID_INFO` combines a volume serial number with a 128-bit file ID. It can:

- detect two paths to the same object/hard link;
- distinguish a rename from new content while the object persists; and
- improve dependency/alias accounting when a handle is already open.

It does not prove content or semantic equivalence. Opening every file only to
obtain an ID was much more expensive than the directory-manifest pass.

Consequence: collect file ID opportunistically for consumed files, aliases,
reparse handling, and journal experiments. Do not make it a mandatory
all-entry M1 capture cost.

### F3 — Strong hashing is required at byte-dependent boundaries; global byte
identity requires global proof

SHA-256 is an available, portable, versionable content identity with negligible
collision risk for this use. The observed performance problem was small-file
open count, not SHA-256 throughput over large sequential files.

Every parser/tool/analyzer artifact must depend on the strong digest of the
exact bytes it consumed. It does not follow that an analyzer which never opens
a texture must hash that texture merely to parse plugin records.

Consequence: exactness is declared per capture surface and artifact dependency
set. “Profile enumerated” must not be displayed as “all content hashed.”

The converse is equally important: a structural/tiered snapshot cannot claim
that *every* byte in the physical population is identical. That claim requires
strong hashes for the complete declared population, or a separately accepted
unchanged-since proof anchored to prior strong hashes. M1 needs exactness for
the local surfaces its proof actually exercises. Whether an M3 readiness run
must offer or require complete byte sealing for a broader population remains a
measured RQ-027/architecture decision; it cannot be implied by metadata.

### F4 — Provider structure and content are separate validity layers

The winner for `meshes/x.nif` can change because:

- the current winning file's bytes changed;
- another provider added/removed/renamed that path;
- mod priority changed;
- provider enablement changed;
- MO2 adapter/skip/mapper semantics changed; or
- an archive/load rule changed.

A content hash of the previous winner detects only the first condition.
Conversely, a metadata directory root can show structural change without
proving which bytes changed.

Consequence: an artifact that claims an effective winner depends on both the
relevant provider-structure node and the winning content node.

### F5 — Quiescence plus double capture and same-stream parsing is defensible
for M1

RQ-001/RQ-002 require MO2 to be closed and selection to be explicit. Within
that boundary:

1. hash control inputs;
2. enumerate a canonical provider manifest;
3. open each consumed file with write/delete sharing denied;
4. hash and parse the same bytes;
5. re-observe handle metadata;
6. repeat the applicable control/structural manifests after capture or stage;
7. reject any mismatch or inaccessible dependency.

This does not create a filesystem transaction, but it prevents the ordinary
mixed-state failure modes relevant to a user changing a modlist during a scan.
Bounded retry may create a new visible attempt; it must not hide drift.

### F6 — Archives need container and member dependency layers

For initial correctness:

- an archive's complete bytes are identified by a strong container digest;
- its member index depends on that digest plus exact archive-parser and game
  semantics;
- an extracted/parsed member depends on the member bytes actually consumed;
  and
- effective member winners also depend on the archive/provider/load graph.

Changing one member conservatively invalidates the container index and all
container-dependent outputs. Later member-content/Merkle identities may permit
validated carryover of unaffected members after the new container is parsed,
but container metadata/CRC alone is not promoted to a cryptographic content
proof.

### F7 — Reparse points require explicit object and target treatment

The capture must not silently traverse an entry with
`FILE_ATTRIBUTE_REPARSE_POINT`. It must record the link object's path, tag,
payload/digest, and volume identity separately from any allowed resolved
target. Cycles, unknown tags, inaccessible targets, cross-volume targets, and
targets outside validated input roots are gaps or capture failures.

No reparse entry appeared in the bounded reference-provider population. This
is lack of an observed case, not proof that supported profiles never contain
one.

### F8 — The NTFS change journal is a possible accelerator, not M1 authority

A future cache can avoid rehashing a previously content-addressed file only if
it can prove uninterrupted journal coverage from the saved checkpoint:

- same volume and journal ID;
- saved USN still within the retained range;
- no gap/reset/wrap;
- every relevant file ID and structural reason mapped to declared dependency
  nodes; and
- every cross-volume/reparse/unsupported area handled independently.

The official documentation says records may be discarded, the journal may be
reset, and documented journal operations require administrator authority. The
journal reports changes, not bytes. Infinium must not create/resize/delete a
journal or require elevation merely to make M1 caching work.

Consequence: M1 rehashes every consumed byte dependency. A separate later
prototype may test read-only journal continuity as an optional fast path whose
failure falls back to rehash/re-enumeration. A filesystem watcher is not a
substitute and continuous monitoring remains out of scope.

### F9 — A global snapshot ID must not become a global cache key

If every artifact is keyed only by one whole-installation root, an unrelated
texture rename invalidates a plugin-record parse. If an artifact is keyed only
by its file hash, a provider-order change can leave a stale effective winner.

Consequence: the snapshot retains one canonical manifest identity for origin
and audit, while each artifact records the smallest complete typed dependency
set needed for its claim. Cross-snapshot use creates a reuse edge with a
dependency-equivalence proof; it never rebinds origin.

## 6. Proposed capture, fingerprint, and dependency model

### 6.1 Versioned identities

Every digest is tagged with:

```text
fingerprint schema
hash algorithm
path-comparator / MO2-adapter semantics
entry or artifact type
producer/parser/tool/ruleset identity where applicable
```

Use SHA-256 for M1 content and manifest identities. It is already available in
the leading .NET environment, sufficient for provenance identity, and the
measurement does not justify another dependency. A later algorithm may coexist
only under a new algorithm identifier; old digests are never silently
reinterpreted.

The physical installation manifest remains distinct from the semantic analysis
context, effective scan configuration, resolved external-source inputs, and
review state required by ADR-0002. An artifact may depend on nodes from several
of those domains, but their identities are not collapsed into the installation
snapshot.

### 6.2 Canonical physical/provider manifest

The production encoding must be unambiguous: typed fields with explicit
lengths, not delimiter-concatenated text. Leaves include, as applicable:

```text
source-root identity and ordered priority
MO2-normalized path key plus preserved observed spelling
entry kind
length
relevant attributes and last-write observation
reparse identity or explicit absence
file/volume identity when observed
content-digest state: present | not-required-for-scope | unavailable
content digest when present
```

Entries are sorted by the version-pinned MO2 adapter's conformance-tested path
comparison and provider rules. Unicode/case behavior must not be invented from
the benchmark's lowercase key. The canonical root is a Merkle-style or
equivalent SHA-256 manifest identity so subtrees can become dependency nodes.

### 6.3 Quiescent capture protocol

1. Validate exact MO2 instance, explicit profile, adapter, runtime, and tool
   identities; require MO2 not running.
2. Read each small control input once under a read-only handle, hash the bytes,
   and parse those same bytes.
3. Build structural manifest A for every declared provider/game/root surface;
   fail or gap inaccessible/unsupported entries.
4. Derive the provider graph from A and the captured controls.
5. For the configured analysis scope, open every consumed plugin, archive,
   loose/configuration file, generated manifest, and tool/data input with
   write/delete sharing denied. Hash and parse the same stream; compare
   handle-based size/time before and after. A library that cannot consume the
   guarded stream must either parse a product-owned content-addressed copy or
   run its path-based read while the guard handle remains open and the digest
   is verified before and after. Failure to preserve that invariant is a gap,
   not permission to trust the path.
6. Build archive-member and other semantic indexes from exact container/input
   digests and exact parser semantics.
7. Rehash control inputs and build structural manifest B. Recheck MO2/process,
   instance, runtime, and selected-profile identities.
8. If A/B or any per-file observation differs, mark the attempt invalidated.
   A bounded retry is a recorded new attempt, never a silent continuation.
9. Seal an immutable snapshot manifest with explicit structural and
   content-scope coverage. Start analysis only against that sealed identity.
10. At long stage boundaries and before final result publication, revalidate
    the applicable structural/control roots. Every later live-file read
    rechecks the expected strong digest; a mismatch invalidates only work whose
    input proof no longer holds and prevents mixed-state publication.

For M1, a complete double structural pass plus scoped plugin/archive/asset
hashes is defensible. The reference observation suggests roughly 40 seconds
for the two provider metadata passes plus the byte costs of the small declared
proof scope. This is an order-of-magnitude observation, not an estimate shown
to users.

### 6.4 Snapshot assurance and coverage

Do not expose one binary “exact” flag. Retain at least:

- structural populations declared/completed/gapped;
- control-input digests;
- content-addressed populations declared/completed/gapped;
- archive/member semantics version and coverage;
- inaccessible/reparse/named-stream/unsupported conditions;
- capture start/end and invalidated attempts; and
- the exact analysis scope sealed against the snapshot.

An M1 run can be exact for its exercised plugin/record/loose-asset surfaces
while honestly reporting that unrelated loose bytes were structurally indexed
but not content-addressed. A later analyzer cannot claim those bytes without
hashing them or producing a new validated snapshot/run input.

If a configured operation requests complete byte identity, it must enter a
separate **fully byte-sealed** state only after every file in its declared
population has a strong digest or a later accepted continuity proof. Failure,
cancellation, or a skipped file leaves that population incomplete. This
explicit state preserves a path to exhaustive high-assurance capture without
making its unmeasured cost the M1 default.

### 6.5 Dependency graph and cache keys

Recommended node classes:

| Node | Minimum validity dependencies |
|---|---|
| Profile/control observation | Exact control bytes, profile identity, format/adapter version |
| Physical content blob | SHA-256 of bytes; optional volume/file ID is alias evidence |
| Provider/path entry | Root identity, normalized path, entry kind, relevant metadata/reparse identity |
| Effective path winner/chain | All relevant provider/path nodes, ordered mod/provider graph, adapter/game semantics |
| Installed-mod/source identity evidence | Exact MO2 metadata bytes and format semantics; source-archive identity/content when consumed; versioned user identity mapping remains an analysis-context dependency |
| Plugin parse/index | Plugin content digest, exact parser/package version and parse options |
| Record override/winner | Ordered effective plugin identities, per-plugin parse artifacts, runtime/game semantics |
| Archive member index | Archive container digest, parser and game/archive semantics |
| Effective archive member | Member/container evidence plus archive/provider/load and loose-file precedence nodes |
| Runtime/root component | Content digest, effective path/provider, version-extraction rule |
| LOOT/libloot result | Exact executable/library/binding, plugin/header/content inputs, masterlist/prelude/userlist/configuration/condition inputs |
| Approved external-tool result | Exact executable, arguments/configuration, materialized effective inputs, observed outputs |
| Source/extraction/model boundary result | Exact source/entity/revision and retained bytes/digest, adapter/extractor/provider/model/prompt/schema/settings and boundary output |
| Analyzer result | Exact upstream evidence node revisions, analyzer/ruleset/schema, analysis-context dependencies |
| Finding/case revision | Exact analytical results/evidence consumed; origin snapshot/run remains immutable |

Metadata tuples may index a prior hash candidate. They do not complete the
content-validity proof unless a separately accepted continuity mechanism, such
as a fully validated journal path, proves the object had no relevant change
since that strong hash.

### 6.6 Invalidation examples

| Event | Recompute/invalidate | Potential validated carryover |
|---|---|---|
| Same path/size/time, changed bytes | Content node and all descendants | Unrelated content/provider nodes |
| Metadata-only time change, same bytes | Snapshot structural observation; descendants that declare time semantic | Byte-only parse/index after strong-hash equivalence |
| Rename within one provider | Old/new path and winner chains; path/identity-sensitive analyzers | Path-independent parse keyed by content digest |
| Mod priority or profile reorder | Ordered provider/winner graph and descendants | Physical content parses whose bytes/parser did not change |
| New higher-priority file | Effective path chain/winner and descendants | Unrelated path subtrees and content parses |
| Archive member change | Container/member index and archive-dependent descendants | Unrelated archives; later unchanged member artifacts only after member-level proof |
| Tool/ruleset/data revision | That tool/analyzer node and descendants | Local physical/source nodes |
| Mid-capture drift | Capture attempt or affected stage becomes invalidated | Separately proven independent acquisition/source work |
| Reparse target/tag/payload change | Reparse node, target mapping, dependent provider/winner state | Unrelated roots |
| Inaccessible dependency | No reuse proof; gap/failure | Artifacts whose dependency closure excludes it |

## 7. Alternatives and rejection thresholds

| Alternative | Benefit | Failure/risk | Disposition |
|---|---|---|---|
| Modification time only | Cheapest | Same-size/time byte changes and delayed time updates defeat it | Reject as validity proof |
| `(path, size, mtime, attributes)` manifest only | Fast structural detection; measured in tens of seconds | Does not prove content or aliases | Keep as structural layer only |
| Add mandatory file ID for every entry | Alias/rename identity | No content proof; measured open cost was minutes | Reject as universal capture step |
| Hash every provider file every scan | Strong whole-population byte identity | Small-file/open cost is material and wastes IO outside narrower declared scope | Reject as unconditional M1/default step; retain explicit fully byte-sealed mode pending RQ-027 |
| Copy/materialize complete effective tree | Freezes ordinary downstream view | Hundreds of GB of writes, lost losing-provider context, still needs authoritative reconstruction | Reject as snapshot authority |
| Tiered structural manifest plus scoped strong hashes and dependency graph | Exact for declared consumers, inspectable gaps, bounded first-proof cost | Requires typed dependencies and careful coverage UI | Recommend |
| NTFS journal as mandatory validation | Can accelerate repeat scans | Elevation/availability/reset/wrap/mapping complexity; not cross-filesystem | Reject for M1; optional later research |
| FileSystemWatcher/continuous monitor | Responsive hints | Event loss/races and out-of-scope continuous behavior | Reject as authority |
| Volume snapshot/VSS | Strong point-in-time view in some configurations | Privilege, operational, storage, reparse/volume, and deployment complexity not justified for M1 | Defer; reopen only if quiescent protocol fails controlled race cases |

Reopen the recommended approach if controlled tests show:

- a consumed input can change without the same-stream/share/double-manifest
  protocol detecting or preventing mixed evidence;
- the full M1 scope cannot be sealed without whole-population hashing;
- canonical provider/path behavior cannot match MO2;
- double structural passes are impractical on controlled OPS-004 stress
  profiles; or
- an unsupported filesystem/reparse/archive surface is required by M1.

## 8. Uncertainty, limitations, and contrary evidence

- The reference benchmark is one non-representative profile on one nearly full
  local NVMe volume. It has fewer than the eventual millions-of-entry stress
  target. Cache state, antivirus, thermal state, and other IO were uncontrolled.
- Selection was stable traversal order rather than a randomized/stratified
  corpus. The results characterize this observed population, not every modlist.
- The metadata benchmark did not include game Data/root, disabled mods,
  generated-output roots, or every configuration/tool input.
- The benchmark implementation was disposable PowerShell-hosted C#/.NET code,
  not production-quality async/native IO. The absolute rates must not become
  product estimates.
- The file-ID measurement used `GetFileInformationByHandle`'s NTFS file index
  for the probe; the recommendation prefers `FILE_ID_INFO` where supported.
- ZIP demonstrated container identity only. BSA/BA2 member/load semantics and
  member-level carryover remain dependent on RQ-001/RQ-004 conformance.
- Named NTFS streams were not enumerated. Initial game semantics normally
  consume the default stream, but an analyzer/tool that depends on another
  stream must declare and fingerprint it.
- Sharing semantics and double capture protect ordinary cooperative filesystem
  access, not a privileged attacker, raw-volume writer, kernel filter defect,
  or every memory-mapped edge case.
- No real mutation was attempted against protected setup state.
- The investigation did not complete a full 254.2-GB content pass, so it cannot
  set the duration or product requirement for a fully byte-sealed run.
- USN continuity was researched from primary documentation but not prototyped.
  It is therefore not claimed as an accepted optimization.
- A hierarchical manifest narrows invalidation only after the authoritative MO2
  comparator/provider model is accepted. This report does not define that
  model independently.
- RQ-027 must later benchmark synthetic million-entry and upper-bound stress
  profiles and calibrate user-visible estimates.

## 9. Recommendation

Confidence:

- **High** that modification time, metadata tuples, and file IDs alone cannot
  prove byte-dependent cache validity.
- **High** that mandatory per-entry opens/hashes are the wrong default at the
  observed scale.
- **Medium-high** that a quiescent double structural manifest plus same-stream
  scoped SHA-256 capture is sufficient for M1's declared local surfaces.
- **Medium** for upper-bound performance and archive/member narrowing pending
  controlled scale and BSA/MO2 conformance.

ADR-0010 accepts a snapshot/cache boundary that:

1. selects a versioned canonical provider/control manifest plus scoped
   SHA-256 content identities and typed dependency graph;
2. requires quiescent explicit-profile capture, same-stream hash/parse, denied
   write/delete sharing, before/after handle checks, and double structural
   revalidation;
3. treats metadata/file IDs only as structural and optimization evidence;
4. requires every reusable artifact to declare the smallest complete
   dependency closure, with origin preserved and reuse proven explicitly;
5. represents structural and content-addressed coverage separately;
6. defines a fully byte-sealed state for operations that require global byte
   identity, without treating it as achieved by metadata or requiring it for
   M1's narrower proof;
7. uses conservative archive-container invalidation until member-level
   equivalence is proven;
8. fails or gaps inaccessible, changing, unknown-reparse, unsupported adapter,
   and unsupported filesystem behavior;
9. excludes mandatory USN, watcher, VSS, and whole-tree-copy dependencies from
   M1; and
10. permits an optional read-only USN fast path only after a separate prototype
   proves journal continuity, privilege, mapping, fallback, and evaluation
   behavior.

The ADR must define the canonical encoding/comparator, manifest node types,
hash algorithm identifiers, snapshot sealing states, retry limits, and storage
transaction that makes a snapshot immutable. It must not turn the entire
snapshot root into the only artifact cache key.

## 10. ADR, evaluation, and follow-up work enabled

### Proposed snapshot/cache ADR inputs

- capture protocol and assurance states from section 6;
- dependency node/key matrix from section 6.5;
- invalidation truth table from sections 4.3 and 6.6;
- M1 rejection of timestamp-only, universal file-ID/open, universal hashing,
  and mandatory journal/watcher paths; and
- explicit reopen thresholds from section 7.

### Evaluation specifications

Update or create reviewed specifications for:

- **EVAL-0013:** relevant byte, provider, profile-order, archive, runtime, and
  tool/ruleset changes invalidate every dependent node;
- **EVAL-0014:** unrelated change carries only through a complete inspectable
  dependency proof; a bare user override does not validate it;
- **EVAL-0021/EVAL-0025:** retained and missing bytes produce correct replay
  and audit-gap disclosure;
- **EVAL-0023:** operational cache/tracing/concurrency changes do not become
  semantic dependencies;
- **EVAL-0024/EVAL-0026:** reuse preserves origin and a mid-run input edit
  never mutates an active binding;
- **EVAL-0037:** clean derived recomputation remains separate from source
  refresh;
- **EVAL-0051:** controlled MO2 fixtures compare byte-stable provider manifests
  and changed-during-capture behavior;
- **EVAL-0052/EVAL-0053:** exact parser/tool/data inputs and results participate
  in dependency validation;
- **EVAL-0078:** path/provider/content/context change impact is scoped and
  explained; and
- **EVAL-0083:** end-to-end provenance resolves every fingerprint and reuse
  proof.

Every positive mutation needs a matched negative or metamorphic control:
same content renamed, metadata-only change, unrelated provider subtree change,
and same bytes under unchanged parser semantics.

### Follow-up

- Wave B integration must reconcile RQ-001 through RQ-007 input manifests with
  the dependency nodes above.
- RQ-015/RQ-017 must make snapshot sealing and invalidation transactional
  across worker restarts and UI queries.
- RQ-013 must store dependency edges, algorithm/schema identities, immutable
  origin, and reuse proofs without pathological query/write amplification.
- RQ-027 must benchmark synthetic 100k, 1M, and upper-bound entry populations,
  cold/warm repeat scans, archive-heavy and small-file-heavy shapes, and
  realistic background limits.
- RQ-032 must validate path aliases, reparse containment, protected-root
  separation, and any native file-handle/journal boundary.
- A later bounded USN investigation is warranted only if repeated scoped
  rehashing becomes material after RQ-027.

### Gate B implication

This report supplies a defensible, measured snapshot-validity strategy for M1:
declared dependencies, not time/ownership guesses, determine validity. The
observed cost of double structural enumeration plus scoped M1 content hashing
is not prohibitive on the reference machine.

It did **not** declare Gate B met overall at report completion. The later
RESEARCH-0013 integration review and ADR-0008 through ADR-0011 accepted Gate B
with documented non-blocking gaps; implementation qualification still depends
on integrated MO2/provider, runtime, Bethesda/archive/string, and any selected LOOT
contracts; controlled synthetic conformance; operation-specific non-mutation;
and independent review. An M1 surface without a complete dependency node and
authoritative capture path remains unsupported.

## 11. Suggested RQ-014 status

Accepted registry status:

> **Resolved for M0 by ADR-0010; implementation conformance pending.** Use a versioned canonical structural manifest,
> same-stream scoped SHA-256 content identities, quiescent double capture, and
> typed dependency-aware reuse. Metadata tuples and file IDs are optimizations,
> not byte identity; mandatory full-population hashing, filesystem watchers,
> and mandatory USN/VSS are rejected for M1. Upper-bound calibration and any
> optional journal acceleration remain follow-up.

## 12. Requirements and evidence traceability

| Requirement / decision | Evidence | Result or downstream use |
|---|---|---|
| SNAP-001, SNAP-002; ADR-0002 | Same-size/time, reorder, archive, share-mode controls and capture protocol in §§4–6 | Immutable sealed input identity; drift invalidates rather than blends |
| SNAP-003, SNAP-004 | Typed dependency graph and invalidation table in §6 | Staleness/carryover is dependency-proven and origin-preserving |
| SNAP-005, SNAP-006 | Versioned algorithm/schema/tool/parser identities and assurance state in §6 | Configuration/replay gaps remain explicit |
| SCAN-007 | Metadata-versus-content findings and cache-key model in §§5–6 | Reuse never relies on time alone; clean recomputation remains possible |
| EVID-002 | Exact local environment, source versions, SHA-256 and input-manifest model in §§3–6 | Material conclusions resolve to observed inputs and producer identities |
| SCOPE-004 | No watcher/automatic trigger; revalidation occurs only inside user-initiated work | Preserves manual-initiation boundary |
| SCOPE-005 | Provider, plugin, archive, loose, root/tool dependency node classes | Required local surfaces can declare exact capture or gaps |
| AUTH-001 through AUTH-003; ADR-0003 | Read-only local probes, temporary-only fixture, no tool launch, share-only capture | No selected operation requires protected writes |
| OPS-004 | 244,626-file / 254.2-GB aggregate and bounded hash/open measurements | Rejects universal per-entry opens; informs RQ-027 |
| RESEARCH-0005/0006 | Quiescent explicit-profile precondition | Snapshot does not bind a stale suggestion or live MO2 memory |
| RESEARCH-0007/0008 | Runtime/parser/archive dependency inputs | Runtime and semantic results cannot outlive their exact bytes/semantics |
| RESEARCH-0009 | LOOT engine/data/userlist/configuration dependency contract | LOOT-derived cache identity cannot be reduced to plugin names |
| EVAL-0013/0014 and successors | Mutation and matched-negative matrix in §§4, 6, and 10 | Supplies reviewed fixture requirements, not passing product tests |
| Wave B Gate | Measured strategy and explicit unresolved integrations in §10 | Snapshot-validity branch was research-supported; RESEARCH-0013 later accepted the overall gate with documented non-blocking gaps |
