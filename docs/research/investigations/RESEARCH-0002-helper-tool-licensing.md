# RESEARCH-0002: Helper-tool licensing and distribution posture

Status: Completed — operational disposition accepted in ADR-0006
Date: 2026-07-25
Last reviewed: 2026-07-25
Researcher: Codex agent
Primary RQ: RQ-026 — What licensing and distribution obligations apply to bundled helpers or external tools?
M0 wave: Wave A
Decision enabled: Define the M1 allowlist/exclusion posture for helper tools, libraries, redistributed project data, and desktop-shell candidates before any implementation dependency is accepted.

Subsequent accepted decision:
[ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
partially supersedes ADR-0006 and every xEdit-specific posture or follow-up in
this report. xEdit is no longer a user-installed Infinium application,
optional analyzer, development dependency, or evaluation oracle. Its source
and licensing analysis below is retained only as historical decision
provenance.

> This is a technical licensing investigation, not legal advice. Copyright,
> trademark, derivative-work, and distribution conclusions can be
> fact-specific. Counsel should review the chosen product license, integration
> boundary, distribution package, and notices before a public or commercial
> release.

## 1. Question and accepted authority

What obligations and architectural constraints follow if Infinium:

1. invokes a helper already installed by the user;
2. redistributes an unmodified helper executable or runtime;
3. links or embeds a library;
4. redistributes separately licensed project data;
5. modifies or forks a helper; or
6. describes interoperability with another product?

The investigation covers the candidates that can materially affect M1:
Mod Organizer 2 (MO2) and USVFS; LOOT's executable/command-line surface,
libloot, masterlists, prelude, and userlist boundary; xEdit;
Mutagen.Bethesda; and Electron, Avalonia, Tauri, and WebView2 as packaging
alternatives.

Accepted requirements and decisions:

- [AUTH-001 through AUTH-003](../../product/requirements.md) require explicit
  authorization before writes, escalation, or execution. A license-compatible
  helper is not automatically safe to run.
- [ANALYSIS-002](../../product/requirements.md) requires Infinium to integrate
  LOOT rather than reimplement its mature functionality and favors validated
  adapters for other established deterministic tools.
- [EVID-002](../../product/requirements.md) and
  [SNAP-006](../../product/requirements.md) require provenance sufficient to
  explain results and later reproduce the tool/data boundary.
- [SEC-003 and SEC-004](../../product/requirements.md) require narrow validated
  allowlists for privileged tool/network operations and sensitivity/source-
  policy review for externally shareable diagnostics.
- [OPS-001 and OPS-003](../../product/requirements.md) require honest
  online/offline capability disclosure and redistribution-reviewed exports.
- [TOOL-001 through TOOL-003](../../product/requirements.md) require
  user-installed MO2 and LOOT, detected or user-configured validated paths,
  and explicit capability/gap reporting. ADR-0007 removes xEdit from those
  requirements.
- [DIST-001 through DIST-003](../../product/requirements.md) require
  GPLv3-family Infinium code, GPL-compatible dependencies, and artifact-level
  redistribution compliance.
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md)
  excludes setup mutations through M4.
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md),
  [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md),
  and
  [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md)
  constrain evidence authority, provenance, and the initial Skyrim/MO2/Windows
  scope.
- The accepted
  [M0 research-foundation plan](../../plans/milestones/M0-research-foundation.md)
  originally made RQ-026 exit-blocking and requires every M1 helper or
  architectural distribution candidate to have a known posture or be excluded.

The accepted
[ADR-0006](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
selects GPLv3-family licensing and the external-application/bundled-dependency
posture enabled by this research. The exact `GPL-3.0-only` versus
`GPL-3.0-or-later` selector, library versions, integration mechanisms, desktop
shell, and distribution channel remain downstream decisions.

## 2. Scope and non-scope

### In scope

- Current upstream license texts, manifests, release metadata, distribution
  documentation, and data-repository licenses as retrieved on 2026-07-25.
- Obligations triggered by the five materially different integration and
  distribution modes above.
- Copyleft boundary risk, source/notices duties, separately licensed data,
  trademark/logo cautions, and update/runtime-service boundaries.
- A conservative M1 posture and exact research, ADR, and release-engineering
  follow-ups.

### Explicitly out of scope

- Legal advice or a jurisdiction-specific derivative-work opinion.
- The research report itself selecting Infinium's own licence, business model,
  architecture, helper, or desktop shell. ADR-0006 subsequently records the
  owner's GPLv3-family and tool-dependency decision.
- Auditing every transitive dependency in a not-yet-selected lockfile or every
  file inside a future installer.
- Assessing licenses for Bethesda game files, mods, mod metadata, generated
  reports, user screenshots, or provider/model outputs.
- Proving technical fitness, output fidelity, or read-only behavior for MO2,
  LOOT, or any parser; those remain with their integration RQs. The historical
  xEdit candidate is resolved by exclusion under ADR-0007.
- Running third-party executables, downloading release archives, modifying the
  user's modding setup, or publishing a distribution.

## 3. Terms used in this report

- **User-installed invocation**: Infinium locates a copy independently obtained
  and installed by the user, then starts it as a separate process through a
  documented interface. Infinium does not ship that helper.
- **Executable redistribution**: Infinium's installer, archive, updater, or
  download service supplies the helper binary, even if the binary is
  unmodified.
- **Linking/embedding**: Infinium loads library code into an Infinium process
  or produces a program linked to that library. Static versus dynamic linking
  is not assumed to remove copyleft risk.
- **Data redistribution**: Infinium supplies metadata, rules, or another
  content repository independently of the executable that consumes it.
- **Aggregate**: Separately usable programs merely placed on the same
  distribution medium. Whether a concrete arrangement is an aggregate or a
  single combined work is a legal conclusion, not something this report can
  settle by naming the transport.
- **Interoperability**: Infinium reads a documented file format or invokes a
  separate program without copying its code. Compatibility statements do not
  confer rights to another project's marks or artwork.

## 4. Sources and exact versions

All web sources were retrieved on 2026-07-25. Repository revisions were
resolved using `git ls-remote --refs`; a tag identifies the source revision,
not a conclusion that every binary asset attached to that release was
independently inspected.

### Governing license interpretation sources

| ID | Source and authority | Exact applicability |
|---|---|---|
| G1 | [GNU GPL version 3](https://www.gnu.org/licenses/gpl-3.0.html), Free Software Foundation, license text | GPLv3 conveying, notice, source, modified-work, aggregate, and trademark-grant boundaries (§§4–7). |
| G2 | [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.en.html), Free Software Foundation, current guidance | Licensor-side interpretation of linking, command-line/pipes/sockets, aggregates, and output. It is useful evidence, not binding law or a substitute for counsel. |
| M1 | [Mozilla Public License 2.0 FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/), Mozilla | Official explanation of MPL's file-level source obligations and larger-work boundary. |

### Helper and parser sources

| ID | Source and exact revision | Authority and claim relevance |
|---|---|---|
| H1 | [MO2 repository](https://github.com/ModOrganizer2/modorganizer) and [GPLv3 license](https://github.com/ModOrganizer2/modorganizer/blob/v2.5.2/LICENSE), tag `v2.5.2`, commit `9c130cbf2fc7225fb2916e46419af50671772aa0` | Official source and license for the current checked MO2 release. |
| H2 | [MO2 v2.5.2 release](https://github.com/ModOrganizer2/modorganizer/releases/tag/v2.5.2) | Official release metadata; identifies included libloot and USVFS versions. |
| H3 | [USVFS repository](https://github.com/ModOrganizer2/usvfs), [README](https://github.com/ModOrganizer2/usvfs/blob/v0.5.0/README.md), and [GPLv3 license](https://github.com/ModOrganizer2/usvfs/blob/v0.5.0/LICENSE), tag `v0.5.0`, commit `9f7fd9660d51784aa2117cb45f2095e87312d558` | Official license plus upstream's alpha/work-in-progress and MO2-component description. |
| H4 | [LOOT repository](https://github.com/loot/loot) and [license](https://github.com/loot/loot/blob/0.29.1/LICENSE), tag `0.29.1`, commit `77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9` | Official GPLv3 project source for the LOOT executable. |
| H5 | [libloot repository](https://github.com/loot/libloot), [license](https://github.com/loot/libloot/blob/0.29.6/LICENSE), and [Cargo manifest](https://github.com/loot/libloot/blob/0.29.6/Cargo.toml), tag `0.29.6`, commit `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1` | Official source; the manifest declares `GPL-3.0-or-later`. |
| H6 | [LOOT documentation](https://loot.github.io/docs/), [masterlist versioning](https://loot.github.io/docs/contributing/masterlist-versioning/), and [FAQ/data layout](https://loot.github.io/docs/help/loot-faqs/) | Official syntax-version and local-data-boundary documentation. |
| H7 | [Skyrim Special Edition masterlist](https://github.com/loot/skyrimse/tree/v0.29) and [CC0 license](https://github.com/loot/skyrimse/blob/v0.29/LICENSE), branch `v0.29`, commit `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f` | Official game masterlist data repository and its own license. |
| H8 | [LOOT prelude](https://github.com/loot/prelude/tree/v0.29), [README](https://github.com/loot/prelude/blob/v0.29/README.md), and [CC0 license](https://github.com/loot/prelude/blob/v0.29/LICENSE), branch `v0.29`, commit `ea316265c11b5c6e6f51d53deb34c4054f4c2349` | Official common-metadata repository, purpose, and its own license. |
| H9 | [xEdit repository](https://github.com/TES5Edit/TES5Edit), [MPL-2.0 license](https://github.com/TES5Edit/TES5Edit/blob/xedit-4.1.5f/LICENSE.txt), and [release](https://github.com/TES5Edit/TES5Edit/releases/tag/xedit-4.1.5f), tag `xedit-4.1.5f`, commit `f5c00f3fa3ee39511185515802647246c807f759` | Official source, license, and checked release revision. |
| H10 | [xEdit command-line documentation](https://tes5edit.github.io/docs/2-overview.html) | Official interface documentation; supports treating xEdit as a separately invoked executable candidate, subject to the integration RQ. |
| H11 | [Mutagen repository](https://github.com/Mutagen-Modding/Mutagen), [GPLv3 license](https://github.com/Mutagen-Modding/Mutagen/blob/0.54.2/LICENSE.txt), and [release](https://github.com/Mutagen-Modding/Mutagen/releases/tag/0.54.2), tag `0.54.2`, commit `282bb99a77b2df7f1b092b06270e8e3c8fb55463` | Official project source, license, and checked release revision. |
| H12 | [Mutagen.Bethesda 0.54.2 on NuGet](https://www.nuget.org/packages/Mutagen.Bethesda/0.54.2) | Official package registry entry; identifies the package as `GPL-3.0-only`. |

### Desktop-shell and runtime sources

| ID | Source and exact revision | Authority and claim relevance |
|---|---|---|
| P1 | [Electron repository](https://github.com/electron/electron), [MIT license](https://github.com/electron/electron/blob/v43.2.0/LICENSE), and [release](https://github.com/electron/electron/releases/tag/v43.2.0), tag `v43.2.0`, commit `1ce868077c0a9fab03f9b90911eb07eb18e54347` | Official Electron source, core license, and checked current release. |
| P2 | [Electron distribution overview](https://www.electronjs.org/docs/latest/tutorial/distribution-overview) and [application distribution](https://www.electronjs.org/docs/latest/tutorial/application-distribution) | Official packaging/rebranding guidance. Electron distributions carry Chromium and other third-party notices in addition to Electron's MIT notice. |
| P3 | [Avalonia repository](https://github.com/AvaloniaUI/Avalonia), [MIT license](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/licence.md), [notice file](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/NOTICE.md), and [release](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.0), tag `12.1.0`, commit `a21b9f573172f705a944dcc8aad7f036b9986f39` | Official framework license, third-party notices, and checked release. |
| P4 | [Avalonia tooling FAQ](https://docs.avaloniaui.net/tools/faq) | Official distinction between the MIT framework and separately licensed professional tools/products. |
| P5 | [Tauri repository](https://github.com/tauri-apps/tauri), [SPDX license declaration](https://github.com/tauri-apps/tauri/blob/tauri-v2.11.5/LICENSE.spdx), [architecture](https://github.com/tauri-apps/tauri/blob/tauri-v2.11.5/ARCHITECTURE.md), and [release](https://github.com/tauri-apps/tauri/releases/tag/tauri-v2.11.5), tag `tauri-v2.11.5`, commit `7cd71369c00978a3783b6ae3e9972358abbe4ae6` | Official dual `MIT OR Apache-2.0` code license and distributor-responsibility statement. |
| P6 | [Tauri Windows installer documentation](https://v2.tauri.app/distribute/windows-installer/) | Official WebView2 bootstrapper/offline/fixed-runtime packaging modes and size/update trade-offs. |
| P7 | [WebView2 SDK 1.0.4078.44](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4078.44) and [package license](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4078.44/License) | Microsoft-published package; current stable package on the retrieval date. Its license is BSD-3-Clause-like and requires binary redistributions to reproduce the notice, conditions, and disclaimer. |
| P8 | [WebView2 Evergreen versus Fixed Version](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/evergreen-vs-fixed-version), [distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution), and [production guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/developer-guide) | Official runtime distribution and servicing guidance. The runtime is redistributable; Evergreen is recommended for most apps, while a packaged Fixed Version shifts updates to the application distributor. |

### Source limitations

- GitHub's displayed license classification was corroborating metadata, not the
  sole basis for any finding. Each repository's own license/manifests were
  checked.
- `v0.29` masterlist/prelude references are moving branches, not immutable
  releases. The commits above pin the evidence reviewed.
- This investigation did not unpack release binaries. A future package review
  must use the actual selected archive, package lock, and installer payload;
  source-repository licensing alone cannot prove the completeness of binary
  notices.

## 5. Experiments, documentation checks, and artifacts

### Environment

- Windows, PowerShell `7.6.3`
- Git `2.55.0.windows.2`
- Repository branch at preflight: `main`, two commits ahead of `origin/main`,
  otherwise clean
- Network documentation/repository access only

### Check E1 — immutable release and data revision resolution

Reproducible form:

```powershell
git ls-remote --refs <official-repository-url> <exact-tag-or-branch-ref>
```

This was run against every revision listed in the source tables. It confirmed
the full commit IDs for MO2 `v2.5.2`, USVFS `v0.5.0`, LOOT `0.29.1`, libloot
`0.29.6`, xEdit `xedit-4.1.5f`, Mutagen `0.54.2`, Electron `v43.2.0`, Avalonia
`12.1.0`, Tauri `tauri-v2.11.5`, and the LOOT `v0.29` data branches.

Safe side effects: network reads and normal transient process/network state
only; no checkout, helper execution, cache installation, game/mod-manager
access, or workspace write.

### Check E2 — claim-level document comparison

For each candidate, the repository license was compared with the release or
package manifest and applicable distribution documentation. In particular:

- libloot's manifest says `GPL-3.0-or-later`;
- Mutagen.Bethesda's package says `GPL-3.0-only`, disproving a possible
  assumption that it is a permissive .NET dependency;
- Skyrim SE masterlist and prelude each carry their own CC0 license rather than
  inheriting LOOT's executable license;
- Avalonia's MIT framework is distinct from optional commercial tooling;
- the WebView2 SDK package and WebView2 Runtime have distinct distribution
  surfaces.

Safe side effects: HTTP reads only. No authenticated API, paid service,
scraping bypass, or third-party executable was used.

### Artifact manifest

No raw external artifact was added. The durable evidence in this report is:

- direct official URLs;
- exact tags/branches and commit IDs;
- the reproducible `git ls-remote` method; and
- the obligations and traceability tables below.

No private mod list, userlist, profile, game file, credential, or personal path
was inspected or recorded.

### External-tool write/cache behavior

No external helper was executed, so this investigation observed no
tool-specific writes, caches, or temporary files. License permission does not
establish compliance with AUTH-001 through AUTH-003. Before any helper is
allowed in M1, its own integration RQ must record process execution,
update/network behavior, logs, caches, temporary directories, and any possible
save/write path.

## 6. Findings

### 6.1 Verified legal and packaging facts

1. GPLv3 permits conveying covered object code, but §§4–6 impose license,
   notice, and corresponding-source conditions. Modified covered works add
   further source and licensing duties. GPLv3 §5 also distinguishes an
   aggregate from a larger combined work.
2. The FSF's GPL FAQ treats static and dynamic linking to a GPL library as a
   combined program. It generally treats arm's-length communication through
   ordinary command-line arguments, pipes, or sockets as evidence of separate
   programs, while warning that complex or intimate communication can point
   the other way. This is interpretation evidence, not a fact-independent safe
   harbor.
3. Merely using an unmodified GPL program internally, or requiring the user to
   obtain it separately, is different from Infinium conveying that program.
   Infinium still must respect the helper's interface and must not copy covered
   code into its own product.
4. MPL-2.0 is file-level copyleft. Distribution of xEdit or modified
   MPL-covered files requires access to the covered source and preservation of
   the MPL terms, but a larger work can contain differently licensed files
   when the MPL boundary is respected.
5. MIT, BSD-3-Clause-like, and `MIT OR Apache-2.0` components permit broad
   reuse, but still require the applicable copyright/license notices.
   Apache-2.0 adds its own notice/change/patent conditions when that option is
   chosen.
6. Software licenses do not supply a general right to use project or company
   trademarks as Infinium branding. Compatibility references should be
   factual and nominative; project logos should not be bundled unless their
   separate asset terms allow it.

### 6.2 Obligations matrix

“Counsel review” below means review before that mode is accepted for a public
or commercial distribution, not that ordinary M0 documentation work must stop.

| Candidate / asset | User-installed separate invocation | Bundle unmodified executable/runtime | Link or embed library | Redistribute project data | Modify, fork, or brand | M1 posture |
|---|---|---|---|---|---|---|
| MO2 `v2.5.2` | No Infinium redistribution of MO2; record detected version and use a documented, explicitly authorized boundary. | GPLv3 duties for the conveyed MO2 payload: license/notices and corresponding source for the exact binary; audit all bundled components. Aggregate versus combined-work status remains fact-specific. | Treat in-process plugin/API linkage or copied MO2 code as a combined-work design requiring the normal GPL and technical review. | MO2 profiles/configuration are user data, not permission to redistribute MO2 or a user's private setup. | Modified covered source remains GPL-compliant; publish source as required. Use MO2 name only for factual compatibility; no logo/rebranding assumption. | **Required user-installed application. Never bundle, download, install, replace, or update it.** RQ-001/RQ-002 own profile and effective-state behavior. |
| USVFS `v0.5.0` | A separate already-installed MO2/USVFS path avoids Infinium conveying USVFS, but technical supportability is weak: upstream calls it alpha/work in progress and an MO2 component. | GPLv3 executable/DLL redistribution duties plus exact-source offer/delivery and dependency audit. | Infinium's GPLv3-family posture removes the first-party licence conflict, but loading/operating USVFS retains substantial API-hooking, version, failure, security, and maintenance risk. | Not applicable beyond user configuration/provenance. | Modified source remains under GPL obligations; upstream instability makes a fork an additional maintenance risk. | **Do not bundle or directly operate by default.** RQ-001 must first evaluate deterministic reconstruction and bounded execution through the user's MO2; later adoption requires demonstrated necessity and a separate accepted mechanism. |
| LOOT executable / command-line surface `0.29.1` | Preferred boundary if a technically suitable, user-installed command-line mode is proven. Record version, command, inputs, outputs, network/update state, and writes. | GPLv3 license/notices/source duties for exact payload. A truly arm's-length executable may remain a separate aggregate, but packaging and IPC must be reviewed. | Not applicable unless LOOT code/libraries are brought in-process. | See masterlist/prelude rows. Tool output is not automatically GPL; copied covered material would require separate analysis. | Modified LOOT remains GPL-covered. Use factual “compatible with LOOT” wording; do not present Infinium as LOOT. | **User-installed application and preferred first RQ-005 boundary. Never bundle or manage it.** Missing/unsupported status disables only dependent capabilities. |
| libloot `0.29.6` | A user-installed standalone library does not create a meaningful separate-process boundary if Infinium loads it; “installed separately” is not a useful versioning or support contract. | Shipping the shared library conveys GPL-3.0-or-later code and triggers its source/notices duties. | Compatible in principle with GPLv3-family Infinium, subject to exact selector, transitive dependency, corresponding-source, and implementation review. | libloot consumes data but does not confer the data repositories' licenses. | Modified/forked library remains GPL; every distributed modification retains its obligations. | **Conditional bundled-library candidate.** RQ-005 may select it only if supported user-installed LOOT invocation is insufficient and direct use adds necessary coverage. |
| Skyrim SE masterlist, branch `v0.29` | User's locally obtained copy can be read subject to provenance and privacy rules. | Not an executable. | Not applicable. | CC0 permits redistribution without GPL source duties or required attribution. Still pin commit, record upstream/retrieval, and preserve syntax-version compatibility for provenance and support. | CC0 permits modification; local changes must not be misrepresented as upstream. | **Managed, versioned data rather than a fixed bundled payload by default.** RQ-005 owns acquisition/cache/offline/provenance behavior; a future pinned offline seed remains possible. |
| LOOT prelude, branch `v0.29` | Same as masterlist. | Not an executable. | Not applicable. | CC0; same provenance, syntax compatibility, and update-integrity duties as masterlists. | CC0 permits modification; distinguish Infinium changes from upstream. | Same managed-data posture as the masterlist. Do not assume the executable's GPL applies to this repository. |
| User `userlist.yaml` and local LOOT state | Reading a user's local file is not project redistribution, but access requires accepted scope and privacy handling. | Never include a user's file in the product installer. | Not applicable. | Do not upload, publish, or place it in shared fixtures by default. A private snapshot may reference/copy it only under RQ-031 policy with sanitization/provenance. | Generated edits are user-authorized mutations and must satisfy AUTH requirements; no license inference from the filename. | Treat as private user input, not redistributable LOOT data. |
| xEdit `xedit-4.1.5f` | Historical candidate boundary only; no xEdit conveyance by Infinium was proposed. | MPL-2.0 requires notices and a clear source-availability path for covered code. Audit the exact release archive and its dependencies before repackaging. | No current reason to embed. If MPL files are used, keep covered files/source under MPL; other larger-work files may use another license. | Outputs and user/game inputs need their own rights analysis; MPL does not automatically license all output. | Modified MPL-covered files and their source remain MPL; separate new files can use another license subject to the actual boundary. Use factual compatibility naming. | **Rejected by ADR-0007. Historical licensing evidence only; no Infinium role remains.** |
| Mutagen.Bethesda `0.54.2` | A NuGet library is normally loaded into the consuming process. Having the user install the package separately would add burden without creating a useful application contract. | Redistributing its assemblies conveys GPL-3.0-only code and triggers exact corresponding-source/license obligations. | Compatible in principle with GPLv3-family Infinium, subject to exact selector, transitive dependency, corresponding-source, and implementation review. | Parsed user/game data does not become GPL merely because Mutagen processed it, absent covered content in output. | A fork remains GPL-covered and distributed modifications retain GPL obligations. | **Leading bundled semantic-library candidate.** RQ-004 must validate capability, correctness, performance, failure, and version behavior before exact adoption. |
| Electron `v43.2.0` | Not normally user-provided; framework is packaged with the app. | MIT notice for Electron plus Chromium/third-party notices (`LICENSES.chromium.html` and applicable payload notices). Audit the exact lock and native modules. | MIT core does not impose copyleft on Infinium, but each dependency keeps its own terms. | Not applicable. | Rebranding guidance applies; do not reuse Electron logos without complying with separate trademark policy. Modified MIT code retains notice. | Legally plausible packaging candidate, not selected. Require locked dependency/SBOM and notice-generation design before M1 acceptance. |
| Avalonia `12.1.0` | Framework packages are normal application dependencies. | MIT notice plus applicable `NOTICE.md`/package dependency notices. | MIT framework does not impose copyleft on Infinium. | Not applicable. | Optional professional tooling/products have separate terms; do not infer their rights from the framework. Avoid Avalonia branding/logo use. | Legally plausible candidate, not selected. Audit exact NuGet lock and distinguish open framework from paid tooling. |
| Tauri `v2.11.5` | Not normally user-provided; Rust/JS dependencies are packaged. | Select and comply with MIT or Apache-2.0 for Tauri code; retain notices and inventory every Cargo/npm/native dependency. Tauri's own architecture document assigns upstream-license compliance to distributors. | Permissive Tauri core does not impose copyleft, but dependencies and application code retain their terms. | Not applicable. | Tauri's README gives its logo separate CC-BY-NC-ND terms; do not package or adapt it as product artwork. | Legally plausible candidate, not selected. Require exact-lock SBOM/notices and an accepted WebView2 runtime mode. |
| WebView2 SDK `1.0.4078.44` | Not applicable as a user-installed tool; it is a build/runtime SDK dependency. | BSD-3-Clause-like package terms require reproduction of copyright, conditions, and disclaimer in binary distribution materials. | Permissive SDK terms do not impose copyleft on Infinium. | Not applicable. | Microsoft/contributor names cannot endorse the product without permission. | Plausible dependency; include the package license in automated notices. |
| WebView2 Evergreen Runtime | Infinium can detect an existing runtime or direct the user to Microsoft's installer. | Microsoft documents the runtime and bootstrapper/standalone installer as redistributable. If installer/bootstrapper is packaged, preserve the selected download's Microsoft terms and third-party notices. | Runtime is a separate platform dependency rather than source linked under an OSS license. | Not applicable. | Do not use Microsoft/Edge marks as endorsement. | Prefer system/Evergreen mode for a Windows web shell unless a later ADR proves a need for fixed/offline packaging. Capture exact installer terms at release freeze. |
| WebView2 Fixed Version Runtime | Not applicable; the app supplies its private runtime. | Microsoft permits packaging a Fixed Version, but Infinium becomes responsible for regularly shipping security/runtime updates and a much larger payload. Exact selected runtime terms/notices must be archived. | Runtime remains a separately serviced platform payload. | Not applicable. | Same mark caution. | Exclude from M1 absent a documented strict-compatibility/offline requirement, update SLA, and package review. |

### 6.3 Candidate-specific interpretation

#### MO2 and USVFS

Reading MO2 profile/configuration files through independently implemented
format support is interoperability, not bundling. It still requires RQ-001 to
prove the format, precedence, coverage gaps, and non-mutating behavior.
Invoking an installed MO2 component is also distinct from redistribution, but
USVFS is not documented as a stable standalone integration product. Its
upstream status and API-hooking shape make direct adoption a poor M1 default
even though GPLv3-family Infinium removes the prior first-party licence
conflict. RQ-001 must evaluate reconstruction and execution through the user's
MO2 first.

MO2's release containing libloot and USVFS is not evidence that Infinium may
extract those components without separately satisfying their licenses and
dependency obligations.

#### LOOT code versus LOOT data

LOOT, libloot, masterlist, prelude, and userlist are five different boundaries:

- LOOT is a GPL executable/application.
- libloot is a GPL-3.0-or-later library.
- game masterlists are separately published CC0 data repositories.
- prelude is separately published CC0 common metadata.
- userlist and local state are user-controlled input, not an upstream
  redistributable asset.

This separation is operationally important. Infinium may legally be able to
redistribute a pinned CC0 metadata seed without thereby gaining a right to
bundle libloot, and it may invoke user-installed LOOT without obtaining a
right to publish a user's local userlist. RQ-005 must determine whether a
supported command-line surface actually provides the necessary deterministic,
read-only evidence; this report does not infer fitness from license alone.

#### xEdit

xEdit's MPL-2.0 posture is materially different from GPL libraries: covered
source remains available at the file level while a larger work may contain
other licenses. However, the intended candidate boundary is still a separate
executable. Its ability to write plugins or caches makes safe invocation an
authorization and integration question independent of MPL compliance.

#### Mutagen.Bethesda

The official package is GPL-3.0-only. Under ADR-0006, a normal C#
`PackageReference` is now a coherent bundled-library candidate rather than a
licensing reason to invent a subprocess wrapper. RQ-004 must still establish
Mutagen's semantic coverage, correctness, performance, failure behavior,
supported versions, transitive dependency posture, and exact package version.
GPL compatibility is necessary but not sufficient for adoption.

#### Desktop shells

Electron, Avalonia, and Tauri have permissively licensed cores and are all
plausible from this narrow licensing perspective. None is “license free”:

- Electron carries Chromium and npm/native dependency notices.
- Avalonia's framework is MIT, but optional professional tooling/products have
  separate terms.
- Tauri requires a Cargo/npm/native dependency inventory, a deliberate
  MIT-versus-Apache compliance choice, and a WebView2 distribution decision on
  Windows.
- Direct WebView2 use requires both SDK notice handling and a separately
  documented runtime installation/servicing posture.

License posture therefore does not select a shell. The locked dependency graph,
security/update model, accessibility, operational packaging, and accepted
architecture criteria still decide.

### 6.4 Hosted API and update-service terms

No candidate requires an authenticated hosted API as the licensing mechanism
assessed here. Network update surfaces still matter:

- LOOT masterlist/prelude acquisition from Git hosting must obey the accepted
  source/update/provenance policy; repository CC0 terms do not guarantee
  availability or immutability.
- A WebView2 Evergreen bootstrapper may download from Microsoft's CDN and the
  runtime then auto-updates. That network behavior must be disclosed and
  incorporated into packaging/support policy.
- Release downloads from GitHub/NuGet are distribution inputs, not permission
  to ignore the artifact's own license.

If Infinium later uses a hosted API, marketplace, paid package feed, or update
service beyond these documented download paths, its current terms, retention,
rate/cost, and redistribution rules require a separate review.

## 7. Alternatives evaluated

| Alternative | License/distribution advantage | Material downside or rejection criterion |
|---|---|---|
| Implement independent readers and analyzers from documented/user-owned formats; no helper shipped | Lowest third-party conveyance risk; clear local-first provenance; full authority control | Reject if evaluation shows inadequate semantic coverage or unsustainable reimplementation. Do not copy GPL implementation code while reproducing interoperability. |
| Require user-installed MO2/LOOT plus the historically considered xEdit boundary | Avoids Infinium redistributing helper binaries; updates remain with user/upstream | xEdit portion rejected by ADR-0007. Reject any remaining helper whose supported interface cannot guarantee deterministic evidence, bounded writes, version detection, or useful coverage. User burden and version drift remain. |
| Bundle an unmodified GPL helper as a clearly separate executable aggregate | Reproducible availability and version pinning may improve | Requires exact source/notices delivery and payload audit; reject if IPC/package design is legally ambiguous, updater cannot maintain obligations, or product license conflicts. Counsel review required. |
| Link libloot or Mutagen into an explicitly GPL-compatible open-source worker/product | Direct semantic access with a coherent copyleft compliance path; ADR-0006 accepts the required GPLv3-family product posture | Still requires complete corresponding source, build/install information where applicable, dependency compliance, and technical validation. Reject a concrete dependency if the intended distribution cannot satisfy those duties. |
| Obtain alternative/commercial rights from a rights holder | Could permit a different distribution model | Availability, price, scope, contributor ownership, and maintenance are unknown. No such offer was verified. Do not plan around it without a written grant reviewed by counsel. |
| Build a generic separate GPL companion service | May strengthen process/source separation and isolate GPL code | Still fact-specific if it is purpose-built or uses intimate RPC. Adds process, update, security, provenance, and maintenance complexity. Reject if it is merely a legal fiction or harms the product contract. |
| Use CC0 LOOT data without LOOT code | Useful metadata seed with low redistribution friction | Metadata alone may not reproduce LOOT semantics; version/schema/update integrity and attribution of local changes remain. Reject as an analyzer substitute unless evaluation proves coverage. |
| Electron shell | Mature packaging; permissive core | Larger bundled surface and Chromium/npm notice/security burden. |
| Avalonia shell | Permissive native .NET framework; may align with a C# backend if independently chosen | Exact dependency/tooling split must be audited; optional professional tooling is not covered by framework MIT terms. |
| Tauri plus Evergreen WebView2 | Permissive core and avoids bundling a private Chromium runtime on Windows | Cargo/npm dependency audit, WebView2 presence/network/update dependency, and installer-mode terms remain. |
| Direct WebView2/Avalonia-native UI rather than a web-shell framework | Potentially smaller framework/license surface | Larger custom engineering surface; still needs SDK/runtime notices and servicing. License simplicity alone is not an architecture criterion. |

## 8. Contrary evidence, uncertainty, limitations, and unsupported cases

### Contrary and boundary evidence

- “GPL means it cannot be bundled” is false. GPL permits redistribution when
  its conditions are met. ADR-0006 resolves first-party compatibility by
  selecting GPLv3-family Infinium; source, notices, exact-selector,
  dependency, and technical gates remain.
- “A subprocess is always separate” is unsupported. The FSF FAQ expressly
  treats communication semantics and intimacy as relevant.
- “Dynamic linking avoids GPL” is contrary to the FSF's published
  interpretation.
- “All LOOT assets are GPL” is false for the checked masterlist and prelude
  repositories, which each publish CC0.
- “All .NET/NuGet libraries under consideration are permissive” is false:
  Mutagen.Bethesda 0.54.2 is GPL-3.0-only.
- “MPL forces the whole application to be open source” overstates MPL-2.0's
  file-level boundary.
- “A permissive shell eliminates compliance work” is false because third-party
  dependency notices, assets, installers, and runtime terms remain.

### Material uncertainty

1. **The exact GPLv3 selector is undecided.** ADR-0006 accepts GPLv3-family
   strong copyleft, but `GPL-3.0-only` versus `GPL-3.0-or-later` must be chosen
   before an operative licence/public code distribution and checked against
   the final dependency tree.
2. **Combined-work boundaries are fact-specific.** This report cannot decide
   whether a future plugin, DLL load, IPC schema, companion service, or bundled
   executable is legally one work.
3. **Binary payloads were not unpacked.** Exact third-party notices, build
   scripts, runtime assets, and source-correspondence artifacts must be checked
   against the selected release archive.
4. **Trademark review was limited.** No candidate-specific clearance search
   was performed. The conservative rule is factual compatibility naming and
   no upstream logo/product branding.
5. **WebView2 runtime terms are versioned distribution artifacts.** Microsoft
   documentation establishes supported redistribution modes, but the exact
   EULA/third-party notices accompanying the chosen bootstrapper, standalone
   installer, or Fixed Version package must be archived and reviewed at
   packaging freeze.
6. **Masterlist/prelude branches move.** The checked commits establish this
   investigation's evidence only, not a permanent future state.
7. **Output rights are not exhaustively assessed.** Tool output is generally
   not automatically covered merely because a GPL program produced it, but
   output containing copied covered content, third-party metadata, game data,
   or user content needs its own analysis.
8. **No technical helper experiment was run.** Command-line support,
   deterministic behavior, cache/temp writes, and failure behavior remain
   unsupported until their integration investigations execute safe probes.

## 9. Accepted recommendation, as amended by ADR-0007

Confidence: **High** for the observed licence classifications and accepted
application-versus-library/data boundary; **Medium** for future exact package
and combined-work implications because they depend on implementation facts,
the final GPL selector, and the complete dependency tree.

ADR-0006, as partially superseded by ADR-0007, accepts the following posture:

1. License Infinium under the GPLv3 family while deferring the exact `only`
   versus `or-later` selector until an operative licence/public distribution
   requires it.
2. Keep MO2 and LOOT user-installed and user-maintained. Detect, validate, and
   allow path override; never bundle, download, install, replace, or update
   them. xEdit is excluded from all Infinium boundaries under ADR-0007.
3. Treat Mutagen.Bethesda as the leading bundled semantic-library candidate,
   subject to RQ-004 technical and dependency validation.
4. Prefer supported invocation of the user's LOOT. Treat bundled libloot as a
   conditional fallback only if RQ-005 proves that the executable boundary is
   insufficient.
5. Do not bundle or directly operate USVFS by default. RQ-001 must evaluate
   deterministic effective-file reconstruction and execution through the
   user's MO2 first; a later accepted mechanism requires demonstrated need.
6. Treat LOOT executable, libloot, masterlist, prelude, userlist, and output as
   separately versioned/licensed/owned objects. Manage current compatible
   masterlist/prelude data with exact provenance rather than freezing an
   invisible installer copy by default.
7. Keep Electron, Avalonia, and Tauri/WebView2 as unselected packaging
   alternatives. Any accepted shell still needs an exact-lock SBOM, notice
   generation, runtime/update treatment, and GPL-compatibility audit.

Preconditions for accepting a concrete bundled dependency:

- exact version and immutable source revision;
- actual package contents and transitive licences audited against the selected
  GPLv3 selector;
- accepted owning integration/architecture decision and evaluation scope;
- corresponding-source, build/install information, SBOM, and notices mechanism
  tested where applicable;
- authority, write, cache/temp, network, cancellation, and failure behavior
  proven by the owning integration RQ; and
- evaluation shows necessary generalizable coverage or implementation value.

## 10. Exact follow-ups enabled

1. **RQ-001/RQ-002:** validate MO2 discovery, user path override, profile
   semantics, deterministic effective-file reconstruction, and bounded
   execution through the user's MO2 before any USVFS proposal.
2. **RQ-004:** validate Mutagen's plugin/override/archive/strings coverage,
   correctness against parser-independent first-party fixture truth, performance, failures, supported
   versions, transitive dependencies, and exact package candidate.
3. **RQ-005:** test supported user-installed LOOT invocation first; compare
   structured output and control needs against pinned libloot; define
   masterlist/prelude/userlist acquisition, cache, provenance, and writes.
4. **RQ-006:** resolved by ADR-0007: retain complete xEdit exclusion and
   require parser-independent record-semantic qualification.
5. **Application setup:** carry TOOL-001 through TOOL-003 into the M1 CLI
   configuration contract and M2 first-run/settings/capability UI.
6. **Packaging evaluation:** fail when a shipped library/runtime lacks an exact
   licence, immutable source, corresponding-source mechanism, required
   build/install information, notices, or SBOM owner.
7. **Release engineering:** choose `GPL-3.0-only` or
   `GPL-3.0-or-later` before an operative licence/public code distribution and
   audit the exact dependency graph against it.
8. **Product/privacy:** keep LOOT userlist and local helper state private unless
   the user explicitly authorizes a sanitized export.

## 11. RQ-026 disposition

Disposition: **Resolved for M0 by ADR-0006.**

Registry summary:

> Infinium uses GPLv3-family strong copyleft. MO2 and LOOT are
> user-installed applications with validated detection/override and explicit
> capability gaps. xEdit is outside Infinium under ADR-0007.
> Mutagen.Bethesda is the leading bundled semantic-library
> candidate; libloot is conditional on RQ-005 proving user-installed LOOT
> insufficient; direct USVFS use is disfavored pending RQ-001 necessity
> evidence. LOOT masterlist/prelude are managed versioned CC0 data rather than
> a fixed bundled payload by default. Every concrete operation, package,
> version, source/notices mechanism, and technical behavior remains gated by
> its owning research and ADR.

Reopen RQ-026 if the GPLv3-family posture changes, a GPL-incompatible
dependency becomes necessary, an external application becomes a proposed
bundled payload, a new helper class enters M1, or the distribution model
materially changes.

## 12. Requirements and evidence traceability

| Requirement / decision | Evidence in this investigation | Result and downstream use |
|---|---|---|
| AUTH-001 through AUTH-003 | No helper was executed; §§5 and 6 identify that licensing does not prove no-write behavior. | Every allowed helper still needs an explicit integration authority/write/cache experiment. |
| ANALYSIS-002 | LOOT executable, libloot, and LOOT data boundaries are assessed separately in §§6.2–6.4; RQ-005 retains responsibility for a validated adapter. | Enables the required LOOT integration without treating licensing as proof of adequate analysis coverage. |
| EVID-002 | Source table includes official URLs, exact versions/revisions, retrieval date, and claim relevance. | Tool/library/data versions can be attached to future evidence packets. |
| SNAP-006 | Masterlist, prelude, helper, and userlist provenance are distinct; moving branches are pinned. | Snapshot schema should record each contributing asset independently. |
| SEC-003 | Subprocess, library, CDN/update, URL, and installer boundaries are separated; helper acceptance requires a validated allowlist. | Feeds the architecture-specific command/path/URL allowlist rather than granting broad process or network authority. |
| SEC-004 | Private userlist state and source-policy review for shareable artifacts are documented in §§6.2, 6.4, and 10. | Prevents a local helper input or restricted source from leaking through diagnostics. |
| OPS-001 | Masterlist and WebView2 network/update dependencies are explicit in §6.4. | Future operations can declare local, cached, or live-network requirements honestly. |
| OPS-003 | Output rights, source-policy review, exact payload terms, and notice/source-link tests are explicit in §§6.2, 8, and 10. | Enables redistribution-reviewed exports and packages rather than assuming inspectability implies shareability. |
| TOOL-001 through TOOL-003 | §§6.2, 9, and 10 distinguish user-installed applications from bundled libraries and require detection, override, validation, and visible capability gaps; ADR-0007 removes xEdit from that contract. | Defines the setup/settings and degraded-capability contract for MO2, LOOT, and accepted bundled libraries. |
| DIST-001 through DIST-003 | §§6, 8, 9, and 10 distinguish GPLv3-family first-party code, user-installed applications, linked GPL libraries, MPL-covered files, and CC0 data. | Enables compatible bundled-library candidates while retaining artifact-level source/notices/SBOM evidence. |
| ADR-0001 | Parser/helper licensing is not treated as proof of deterministic correctness; sources and interpretations are separated. | Evaluation must establish evidence authority and semantic adequacy before accepting any dependency. |
| ADR-0002 | Tool, data, revision, and local-user-input provenance are separated. | Supports reproducible snapshots without redistributing private state. |
| ADR-0003 | No helper execution was treated as authorized merely because its license permits use. | Every helper path still must preserve the accepted no-setup-mutation boundary. |
| ADR-0004 | The candidate review stays within the initial Windows, pinned Skyrim Special Edition runtime, and MO2-centered product scope while avoiding an implicit framework choice. | A later architecture ADR can use these packaging facts without broadening target scope. |
| ADR-0006 / M0 Gate A / RQ-026 | Obligations matrix covers every named M1 helper and shell candidate; ADR-0006 selects GPLv3-family licensing and the application/library/data boundary. | RQ-026 is resolved while concrete package versions, operations, and technical integrations remain explicit downstream gates. |
| ADR-0007 / RQ-006 | Historical xEdit licensing and boundary evidence is retained, but the integration/oracle recommendation is rejected. | xEdit creates no setup, packaging, capability, runtime, or evaluation requirement for Infinium. |
