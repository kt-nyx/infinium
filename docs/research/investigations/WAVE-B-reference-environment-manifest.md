# Wave B local reference environment manifest

Status: Completed  
Owner: Project owner  
Captured: 2026-07-25  
Last reviewed: 2026-07-25

## Purpose and authority

This manifest records the shared local-environment preflight for Wave B. It is
a reproducibility aid for read-only research, not an accepted integration
decision, evaluation fixture, supported-version declaration, or model of what
Skyrim modlists generally look like.

`Brain Blast Destruction 2024` is a user-confirmed, previously used Skyrim SE
MO2 profile. Wave B may inspect it when a real MO2 instance or profile shape is
needed. It must not be treated as a gold standard, representative corpus,
correct setup, expected scale, or source of profile-specific production rules.
Future correctness evaluation will begin with synthetic atomic fixtures and
small purpose-built profiles using carefully selected real mods.

No raw profile contents, mod names, absolute private paths, credentials, or
redistribution-restricted artifacts belong in committed research. Reports may
retain sanitized structure, bounded aggregate measurements, fingerprints, and
findings needed to reproduce the investigation.

## Path tokens

| Token | Local meaning |
|---|---|
| `<SKYRIM_ROOT>` | User's Steam Skyrim Special Edition installation |
| `<MO2_INSTALL>` | User's Mod Organizer 2 installation |
| `<MO2_INSTANCE>` | MO2 portable instance for Skyrim SE |
| `<REFERENCE_PROFILE>` | `<MO2_INSTANCE>/profiles/Brain Blast Destruction 2024` |
| `<LOOT_EXE>` | User-installed LOOT executable configured within the MO2 instance |

The private token-to-path mapping remains local and is not required in
committed outputs.

## Captured identities

| Component | Captured identity | SHA-256 |
|---|---|---|
| Skyrim SE runtime | `SkyrimSE.exe` file/product version `1.6.1170.0`; 37,157,144 bytes | `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9` |
| Mod Organizer 2 | `ModOrganizer.exe` version `2.5.2` | `442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622` |
| LOOT | `LOOT.exe` version `0.28.0` | `C993642493FBA7ACE99F8D212BA6DA768EA994C4058291286BF7B4938537CB35` |

The locally installed versions are experiment subjects, not automatically the
versions Infinium will support. Repository source research may use newer exact
versions when the investigation identifies both the source revision and the
relationship to this local environment.

## Reference-profile fingerprint

These fingerprints establish only the captured input state. Line counts are
structural observations and are not counts of enabled or valid mods/plugins.

| Relative file | Bytes | Lines | SHA-256 |
|---|---:|---:|---|
| `modlist.txt` | 63,422 | 1,834 | `1EE6A3E230D9CEEA54816473B5952D2447C1EC4B0ACDED7C89853CF32B1C86E6` |
| `plugins.txt` | 83,733 | 2,281 | `FAA8811219D7E434969AC9774A9462E93898EAF2213631BDC31BA282FF179635` |
| `loadorder.txt` | 83,562 | 2,361 | `66D5E8A44544448B7AAE7702E5A783392C857755BA5DA0F69229CB48C046609D` |
| `archives.txt` | 11,185 | 394 | `445386996E9E2C9F88D5AD4A660B17F63113D1E5771965FDAF5CC9A5EA055625` |
| `settings.ini` | 83 | 4 | `A46C95CBB3DA8CBAEF34066DD99A7A8EAA8304FC7F4BD3BE22BD1305692DEEA6` |
| `lockedorder.txt` | 59 | 1 | `9962FBB07E4C40C4C39B6FE7CEDF3F1527CDDA5AD0A60D7D53A872B5AE52812D` |

## Preflight decisions

- The obsolete `test profile` was removed at the project owner's request on
  2026-07-25 by sending its exact directory to the Windows Recycle Bin.
- MO2's selected-profile setting was changed from that deleted profile to
  `Brain Blast Destruction 2024`. MO2 was not running during either action.
- Wave B research otherwise remains read-only with respect to MO2, the
  reference profile, installed mods, LOOT configuration, and the game
  installation.
- Network research may use current primary project documentation,
  specifications, repositories, and release artifacts. Every conclusion must
  record exact versions/revisions and retrieval date.
- Agents may create disposable probes and synthetic inputs only outside
  protected setup roots. No external-tool operation may run until its
  setup-owned side effects and read-only behavior are established.
- Wave B does not authorize launching Skyrim, changing the profile, sorting the
  load order, cleaning or saving plugins, generating output, or writing caches
  into the protected setup.
- No authenticated API, paid LLM, or credential access is required for Wave B.

## Drift and reuse

An investigation that depends on the captured profile must verify the
applicable file fingerprints immediately before and after its observation. A
mismatch invalidates the affected result; it is not repaired by assuming the
change was harmless.

The profile may support descriptive examples of real MO2 state. Synthetic
atomic fixtures and small purpose-built real-mod profiles remain the required
direction for controlled correctness tests, negative controls, and regression
evaluation.
