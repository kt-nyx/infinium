# Documentation source registry

Status: Draft
Last reviewed: 2026-07-26

This document defines the registry model, initial authority classes, and
reviewed source-level decisions. Exact endpoints, observed capabilities, and
unregistered sources still require their owning research.

## Registry fields

```text
source_id
name
source_class: official | author | curated | community
authority_scopes/claim_types
default_evidence_role: authoritative | investigative
games
content_types
access_method
authentication
allowed_operations
prohibited_operations
private_retention
provider_transmission
external_redistribution
allowed_content_forms
freshness/revision behavior
search strategy
citation requirements
cache/redistribution policy
failure and coverage semantics
last policy verification
reverification/reversal triggers
```

Retained-content codes used below follow
[RESEARCH-0003](investigations/RESEARCH-0003-retention-replay-export-policy.md):

- `B` — exact bytes/body or lossless structured payload;
- `X` — an exact supporting excerpt;
- `D` — derived content that does not reproduce restricted expression;
- `M` — metadata, identity, revision, fingerprint, dependency, or policy data;
- `O` — an explicit omission, deletion, unavailability, reference, or citation
  marker.

These describe retained forms, not evidence authority or redistribution
permission.

## Initial authority classes

### Primary/authoritative within declared scope

- Mod author descriptions and requirements
- Author-maintained articles
- Author changelogs
- Sticky or author posts where an allowed interface exists
- Curated LOOT masterlist/prelude metadata
- Official project repositories and documentation
- Documentation bundled with the installed mod

These establish authoritative claims, subject to version applicability,
freshness, specificity, and supersession. Local state may disprove a claim's
applicability or described effect in the selected installation; it does not
rewrite what the source stated.

### Investigative

- User comments
- Bug reports
- Forums
- Reddit and other community discussions
- Unofficial guides

These may originate leads but do not become definitive findings without
corroboration or strong local evidence.

MO2/LOOT userlist rules and user-authored compatibility notes are local user
inputs. They may be highly relevant to intent and effective tool behavior but
are not reclassified as curated LOOT or author evidence.

## Access principles

- Use supported APIs and permitted access methods.
- Do not evade scraping or authentication restrictions.
- Do not make ordinary posts/comments a dependency of core coverage.
- Record source revision or exact captured material where permitted.
- Mark unavailable material as a coverage gap.
- Treat retrieved text as untrusted data.
- Preserve citations near every retained extracted claim.
- Apply citation and redistribution policy independently to exports; permission
  to cache or retain material privately does not imply permission to share it.

## Registered source decisions

### Nexus Mods

| Field | Current decision |
|---|---|
| Source ID | `nexus-mods` |
| Source class | Official platform carrying author and community content; authority remains claim-type- and author-specific |
| Governing evidence | [RESEARCH-0001](investigations/RESEARCH-0001-nexus-access-policy.md), [ADR-0005](../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md), current Nexus Terms, API AUP, API specifications, and rate-limit guidance, all verified 2026-07-25 |
| Access method | Documented, supported Nexus API operations only |
| Authentication/initiation | User-owned revocable authorization; every acquisition manually initiated through M4; accurate Infinium name/version; personal key only for permitted personal/development use; public registration/approval before public-facing release |
| Acquisition decision | Conditionally allowed under ADR-0005 when bounded to relevant mods and normal API use |
| Private retention decision | Conditionally allowed in product-owned local storage for source bodies/excerpts and derived material needed to complete useful extraction, deterministic and user-authorized LLM analysis, claim/case/finding synthesis, prose, provenance, audit, replay, refresh, and applicable private history. Metadata-first durable minimization applies after required dependent work is materialized; exact mechanism/duration awaits RQ-013 and measured policy. |
| Provider-transmission decision | Conditionally allowed for minimized credential-free excerpts through an explicitly selected provider for inference only; disclosure and provider retention controls required |
| External-redistribution decision | Derived diagnostic findings, attribution, and canonical links allowed by project interpretation; raw source bodies/media/files excluded by default; exact short-quotation and deep-link terms remain subject to export policy and Nexus confirmation |
| Allowed content forms | Private `B/X/D/M` as necessary and reviewed; shareable `D/M/O` by default, with `X` only after quotation/source-policy review |
| Prohibited operations | HTML scraping/crawling; browser-session automation; undocumented/private endpoints; access-control bypass; bulk unrelated acquisition; unattended background collection; rehosting/public corpus; substitute catalog/download service; model training/fine-tuning/validation; raw source republication |
| Unsupported surfaces | Emit per-source/per-entity coverage gaps. Posts, comments, articles, changelogs, bug reports, or other content without a supported read interface may not fall back to page access. |
| Freshness/revision | Record retrieval time, source/entity identity, content/API revision where exposed, adapter version, and governing policy version |
| Citation/export | Preserve author/source attribution and source identity near derived claims; keep private retention separate from every export copy |
| Reverification triggers | Nexus response; Terms/AUP/API/registration change; registration rejection or restriction; server/shared-database proposal; public source export; model-training proposal; materially broader acquisition |
| Current residual risk | Section 11 is broad enough to support a contrary interpretation. The project owner accepts that ambiguity as non-blocking within ADR-0005 and will review or supersede the ADR on a trigger. |

## Registered dependency and licensing authorities

These sources govern project dependency/licensing decisions rather than
becoming mod-analysis evidence. Their exact checked revisions and claim-level
citations are preserved in
[RESEARCH-0002](investigations/RESEARCH-0002-helper-tool-licensing.md);
[ADR-0006](../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
records the accepted licensing/dependency disposition, and
[ADR-0007](../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
supersedes its xEdit-specific provisions. ADR-0008 through ADR-0011 record the
accepted Wave B version and use boundaries; their named conformance work
remains pending.

| Authority | Checked identity | Registered use |
|---|---|---|
| [GNU GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) and [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.en.html) | Retrieved 2026-07-25 | GPLv3-family product obligations, redistribution/source duties, and linked-versus-separate-program interpretation |
| [MO2 repository](https://github.com/ModOrganizer2/modorganizer) | `v2.5.2`, commit `9c130cbf2fc7225fb2916e46419af50671772aa0` | GPLv3 classification, user-installed required-application provenance, and ADR-0008's pinned deterministic-reconstruction/conformance authority |
| [USVFS repository](https://github.com/ModOrganizer2/usvfs) | `v0.5.0`, commit `9f7fd9660d51784aa2117cb45f2095e87312d558` | GPLv3 classification, component/API-hooking status, and disfavored candidate boundary |
| [LOOT repository](https://github.com/loot/loot) | `0.29.1`, commit `77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9` | GPLv3 classification, user-installed application provenance, and evidence that the current application is not the accepted structured headless-analysis boundary |
| [libloot repository](https://github.com/loot/libloot) | `0.29.6`, commit `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1` | `GPL-3.0-or-later` classification and ADR-0011's conditional pinned read-only semantic-library boundary |
| [xEdit repository](https://github.com/TES5Edit/TES5Edit) | `xedit-4.1.5f`, commit `f5c00f3fa3ee39511185515802647246c807f759` | Historical RQ-006 research provenance only. ADR-0007 rejects the investigated integration/oracle proposal and excludes xEdit from Infinium |
| [Mutagen repository](https://github.com/Mutagen-Modding/Mutagen) and [Mutagen.Bethesda package](https://www.nuget.org/packages/Mutagen.Bethesda/0.54.2) | `0.54.2`, commit `282bb99a77b2df7f1b092b06270e8e3c8fb55463` | GPLv3/`GPL-3.0-only` classification and ADR-0009's accepted pinned, positively allowlisted Bethesda semantic-library boundary |
| [Skyrim SE LOOT masterlist](https://github.com/loot/skyrimse) and [LOOT prelude](https://github.com/loot/prelude) | `v0.29`, commits `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f` and `ea316265c11b5c6e6f51d53deb34c4054f4c2349` | CC0 classification and ADR-0011's managed-data provenance; exact acquisition, freshness, compatibility, and cache behavior remain qualification work |

Reverify the applicable source when a version is selected, its licence or
package graph changes, an external application becomes a proposed bundled
payload, the exact Infinium GPLv3 selector is chosen, or a release artifact is
assembled.

## Registered Wave C technical authorities

These sources define technical-format or exact-version research inputs. They
do not become mod-author intent evidence, prove a local component's identity,
or select a production dependency. Their claim-level citations and limitations
remain in the owning investigation.

| Authority group | Checked identity | Registered use and limit |
|---|---|---|
| Microsoft PE/COFF, version-information, DLL-search, and `LoadLibraryEx` documentation | Official Microsoft documentation retrieved 2026-07-25 | Platform-format and loader-boundary authority for [RESEARCH-0014](investigations/RESEARCH-0014-root-native-component-surfaces.md); documentation does not identify local bytes |
| SKSE | Release `v2.2.6`; commit `9398d04592a7eb9d754f2997701116df1022f1b4` | Exact release/source evidence for supported runtime, root layout, plugin metadata, and loader relationships in RESEARCH-0014 |
| CommonLibSSE-NG | Commit `b93280e832f263dbef44e44cbe2936622a02f91a` | Exact Address Library format/runtime-selection evidence in RESEARCH-0014; not proof that an arbitrary local plugin uses that implementation |
| SSE Engine Fixes | Release `7.0.20`; commit `af982b0b57d8d8935686faaf1f8c49508baf0bd1` | Versioned representative companion/preloader relationship evidence in RESEARCH-0014 |
| ReShade | Commit `f191dc03ce8fb435fb1df2ed59fac1e7f944c90e`, retrieved 2026-07-25 | Moving-branch source evidence for proxy-name and static identity signals in RESEARCH-0014; not a local-build identity claim |
| ENBSeries Skyrim SE publisher pages | Moving official pages; label `0.504` observed 2026-07-25 | Publisher/version-label evidence in RESEARCH-0014; mutable bytes and absent content hashes make the label insufficient for exact identity |
| Ultimate ASI Loader | Release `v9.7.2`, retrieved 2026-07-25 | Contrary evidence in RESEARCH-0014 that generic proxy filenames are non-unique |

The remaining Wave C investigations register their exact technical sources
within their own source tables because those authorities are analyzer-research
inputs rather than general documentation providers:

- generated-output tools:
  [RESEARCH-0015](investigations/RESEARCH-0015-generated-output-tool-surfaces.md);
- configuration ecosystems:
  [RESEARCH-0016](investigations/RESEARCH-0016-configuration-ecosystem-survey.md);
- PEX/VMAD:
  [RESEARCH-0017](investigations/RESEARCH-0017-compiled-papyrus-analysis-boundary.md);
- asset formats and references:
  [RESEARCH-0018](investigations/RESEARCH-0018-asset-reference-completeness.md);
- Bethesda semantic record families:
  [RESEARCH-0019](investigations/RESEARCH-0019-semantic-record-family-roadmap.md);
- corpus candidates and source availability:
  [RESEARCH-0020](investigations/RESEARCH-0020-evaluation-corpus-and-real-mod-candidates.md);
- taxonomy research:
  [RESEARCH-0021](investigations/RESEARCH-0021-skyrim-mod-impact-taxonomy.md).

Before any of those sources becomes a production parser, library, bundled
payload, generated-data feed, or supported adapter, its exact current version,
licence, data policy, and supported shapes require a separate accepted
qualification decision.

## Search behavior

Default search should use approved primary/technical sources. Broader community
web search is opt-in and retains authority classification.

An LLM does not choose arbitrary source authority. The registry and adapter
policy determine what can be searched and how each claim is classified and
applied. Authority is assigned for declared claim types/scopes rather than as a
single total rank for everything a source might say.
