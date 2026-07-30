# RESEARCH-0051: Skyrim SE secondary-root and mapper inventory

Status: Completed; plan/specification disposition pending  
Date: 2026-07-29  
Last reviewed: 2026-07-29  
Researcher: Codex  
Acceptance: Research complete; recommendation not yet owner-accepted

## Question and requirements

Does the exact MO2 `2.5.2` Skyrim SE game plugin admitted by M1 Slice 3
provide a secondary Data root or a file mapping that contributes to effective
Data, and can that identity safely populate the production qualified-mapper
allowlist required by ADR-0008 and EVAL-0051?

This investigation is bounded by SCOPE-002, SCOPE-003, AUTH-001 through
AUTH-003, ADR-0008, ADR-0010, EVAL-0046, and EVAL-0051.

## Scope and non-scope

In scope:

- the evaluator-private exact `game_skyrimse.dll` admitted by the Slice 3
  support manifest;
- the corresponding first-party Skyrim SE game-plugin source and interfaces;
- secondary Data directories and `IPluginFileMapper` mappings that can affect
  the M1 effective-Data surface; and
- whether the existing generic `QualifiedMapping` request can safely admit
  that plugin identity.

Out of scope:

- arbitrary third-party MO2 plugins;
- other games, managers, MO2 versions, Skyrim channels, or runtimes;
- archive-member precedence;
- loading a plugin or launching MO2/USVFS in production; and
- selecting a new third-party mapper without separate qualification.

## Sources and exact versions

Primary sources:

- [Mod Organizer `v2.5.2`](https://github.com/ModOrganizer2/modorganizer/tree/9c130cbf2fc7225fb2916e46419af50671772aa0),
  commit `9c130cbf2fc7225fb2916e46419af50671772aa0`.
- [ModOrganizer2 `modorganizer-game_skyrimSE`](https://github.com/ModOrganizer2/modorganizer-game_skyrimSE/tree/82ac42f83f717b4884c8c551cb67b545e15177bc),
  commit `82ac42f83f717b4884c8c551cb67b545e15177bc`, the last first-party source
  revision before the admitted plugin's August 2024 build timestamp. The
  exact [`mappings()` implementation](https://github.com/ModOrganizer2/modorganizer-game_skyrimSE/blob/82ac42f83f717b4884c8c551cb67b545e15177bc/src/gameskyrimse.cpp#L330-L341)
  is the mapper authority used below.
- The evaluator-private exact plugin:
  - file: `game_skyrimse.dll`;
  - byte length: `440,320`;
  - SHA-256:
    `5EAACE8EC5E3F1E6DC6E85FFE22ABDD30C99DFA414807E2D7E2EF242CC90A429`;
  - PE/COFF timestamp: `2024-08-04T08:15:51Z`; and
  - embedded plugin version: `1.7.1`.

Repository decisions and specifications:

- ADR-0008;
- RESEARCH-0005;
- EVAL-0051; and
- the accepted M1 fixture-manifest specification.

## Experiments and artifacts

The exact private DLL was inspected without execution. Its SHA-256, byte
length, PE/COFF timestamp, embedded plugin/version strings, mapped filenames,
and RTTI interface names were recorded. The binary contains
`IPluginFileMapper`, `plugins.txt`, and `loadorder.txt`.

The first-party source at commit `82ac42f` was inspected. `GameSkyrimSE`
implements `mappings()` and returns exactly two mappings:

- selected-profile `plugins.txt` to the Skyrim SE LocalAppData plugin-list
  location; and
- selected-profile `loadorder.txt` to the Skyrim SE LocalAppData load-order
  location.

Both mappings have `isDirectory = false`. `GameSkyrimSE` does not override a
secondary-Data-directory method and declares the normal Skyrim game Data
directory through the inherited Gamebryo contract. Source changes after
`82ac42f` through the next archived revision do not alter these mappings.

No MO2 process, game process, USVFS controller, or plugin was launched for this
investigation.

## Findings

1. The admitted Skyrim SE game plugin is a known file mapper, but its known
   mappings do not contribute a loose provider to virtual Data. They redirect
   two already-captured profile control files to Skyrim's LocalAppData
   locations for hooked processes.
2. The exact supported Steam Skyrim SE target has one primary physical Data
   root and an empty game-plugin-provided secondary Data-root inventory.
3. Slice 3 already captures and seals `plugins.txt` and `loadorder.txt`
   directly. Modeling those files as loose Data providers would be incorrect.
4. `QualifiedMapping` accepts a caller-supplied source root and virtual prefix.
   Adding the trusted game-plugin SHA-256 to
   `QualifiedMapperSha256s` would therefore let a caller associate that trusted
   identity with an arbitrary Data-contributing root. The exact plugin source
   does not grant that authority.
5. The current empty production qualified-mapper allowlist is the correct
   fail-closed state for the exact supported target.
6. The accepted EVAL-0051 atomic requirement for a positive
   “supported secondary-root/mapper contribution” cannot be met by the exact
   admitted Skyrim SE game plugin. A positive third-party mapper would be a new
   supported surface, not evidence that can be invented from this target.

## Alternatives

### Add the Skyrim game-plugin hash to the generic mapper allowlist

Rejected. The generic request does not constrain mappings to the plugin's two
fixed control-file sources and non-Data targets. It would amplify a trusted
hash into arbitrary loose-provider authority.

### Treat the profile control-file mappings as effective-Data providers

Rejected. Their targets are Skyrim's LocalAppData plugin/load-order files, not
virtual Data. Slice 3 already captures their source bytes under the correct
control-file authority.

### Select and qualify a third-party mapper for a positive fixture

Viable only as a separately reviewed expansion. It requires an exact binary
and source/behavior identity, deterministic mapping discovery, a closed
per-mapper schema, non-mutation execution, redistributable evaluator setup,
and EVAL-0046/EVAL-0051 evidence. If its authority boundary differs from
ADR-0008, it also requires a new or superseding ADR.

### Amend the exact-target EVAL-0051 matrix

Recommended. Require:

- an explicit exact game-plugin inventory, including the empty secondary-Data
  set;
- proof that known non-Data game-plugin mappings are captured through their
  correct control-file authority;
- project-authored generic mapper-registry mechanics tests; and
- unknown/unqualified mapper fail-closed coverage.

A positive real mapper should become conditional on one being deliberately
selected and qualified for the exact supported target.

## Uncertainty and limitations

The exact DLL was not rebuilt from source, so source-to-binary identity is
correlated through the first-party revision date, embedded version and
interface/mapping strings, behavior shape, and absence of intervening mapping
changes rather than a reproducible-build match. This is sufficient to reject
invented Data authority; it is not a general qualification of every function
in the plugin.

No independent MO2 UI/VFS oracle observation was completed by this
investigation. That separate EVAL-0051 blocker remains.

## Recommendation

Keep `QualifiedMapperSha256s` empty. Record the exact Skyrim game-plugin
secondary Data-root inventory as empty and its two fixed profile-control
mappings as known non-Data mappings already covered by direct sealed control
inputs.

Submit a reviewed M1 plan/EVAL-0051/fixture-manifest amendment making the
positive supported mapper case conditional rather than mandatory for this
exact target. Do not mark EVAL-0051 passed until the remaining independent
explicit-profile MO2 UI/VFS comparison is complete.

## ADR or follow-up enabled

No ADR is required to retain the current empty mapper allowlist and exact
target boundary. The recommendation requires a reviewed amendment to the M1
plan, EVAL-0051 specification, and fixture-manifest matrix.

Choosing a third-party mapper instead requires a new bounded investigation and
may require a new/superseding ADR under ADR-0008 item 10.
