# Documentation source registry

Status: Draft
Last reviewed: 2026-08-01

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

- Use registered APIs and permitted access methods under the applicable
  accepted source decision; expose any explicitly accepted policy risk.
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
| Governing evidence | [RESEARCH-0001](investigations/RESEARCH-0001-nexus-access-policy.md), [ADR-0005](../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md), accepted [ADR-0012](../architecture/decisions/ADR-0012-nexus-latest-capable-api-routing.md), [RESEARCH-0025](investigations/RESEARCH-0025-nexus-supported-content-interfaces.md), [RESEARCH-0030](investigations/RESEARCH-0030-nexus-latest-interface-qualification.md), current Nexus Terms, API AUP, API specifications, and rate-limit guidance; technical interfaces reverified through bounded authenticated tests on 2026-07-28 |
| Access method | Nexus-provided read APIs under ADR-0012; latest-capable v3, v2 GraphQL, then v1 per-content routing |
| Authentication/initiation | User-owned revocable authorization; every acquisition manually initiated through M4; accurate Infinium name/version; personal key only for permitted personal/development use; public registration/approval before public-facing release |
| Acquisition decision | Allowed for bounded development/local diagnostic use under ADR-0005 and ADR-0012; public release still requires then-current registration/approval and reversal review |
| Private retention decision | Conditionally allowed in product-owned local storage for source bodies/excerpts and derived material needed to complete useful extraction, deterministic and user-authorized LLM analysis, claim/case/finding synthesis, prose, provenance, audit, replay, refresh, and applicable private history. Metadata-first durable minimization applies after required dependent work is materialized. ADR-0015 accepts SQLite plus a coordinator-owned content-addressed payload store for the mechanism; measured duration/threshold policy remains implementation and product-tuning work. |
| Provider-transmission decision | Conditionally allowed for minimized credential-free excerpts through an explicitly selected provider for inference only; disclosure and provider retention controls required |
| External-redistribution decision | Derived diagnostic findings, attribution, and canonical links allowed by project interpretation; raw source bodies/media/files excluded by default; exact short-quotation and deep-link terms remain subject to export policy and Nexus confirmation |
| Allowed content forms | Private `B/X/D/M` as necessary and reviewed; shareable `D/M/O` by default, with `X` only after quotation/source-policy review |
| Prohibited operations | HTML scraping/crawling; browser-session or traffic automation; access-control bypass; bulk unrelated acquisition; unattended general collection; mutation/download/account operations; rehosting/public corpus; substitute catalog/download service; model training/fine-tuning/validation; raw source republication |
| Unsupported surfaces | Emit per-source/per-entity coverage gaps. Authenticated v3/v2/v1 qualification found no mod-linked articles, posts/comments, author/sticky posts, or mod-page bug-report reads. Generic GraphQL comments, collection bug reports, search snippets, and page access are not substitutes. |
| Freshness/revision | Record retrieval time, source/entity identity, content/API revision where exposed, adapter version, and governing policy version |
| Citation/export | Preserve author/source attribution and source identity near derived claims; keep private retention separate from every export copy |
| Reverification triggers | Nexus response; Terms/AUP/API/registration change; registration rejection or restriction; server/shared-database proposal; public source export; model-training proposal; materially broader acquisition |
| Current residual risk | Section 11 is broad enough to support a contrary interpretation. The project owner accepts that ambiguity as non-blocking within ADR-0005 and will review or supersede the ADR on a trigger. |

### Accepted Nexus interface-routing decision

The records below describe the accepted source-routing design under ADR-0012.
They are not a claim that a production adapter, secure credential mechanism,
or evaluation conformance exists.

| Capability ID | Status | Verified identity/date | Qualified content | Capability gaps and delivery gate |
|---|---|---|---|---|
| `nexus-mods-v3` | **Accepted preferred route; implementation not delivered** | OpenAPI API `3.0.0`, document `3.0.3`; SHA-256 `58227301a8c8a30f01fae8c9fdf913cfdd989bee46eaff7edbb5619146ee6d7c`; authenticated verification 2026-07-28 | Mod/file identity, batch summaries, exact file versions/upload times, and typed file-version/DLC dependencies | Relevant reads remain Experimental; no long description, page requirement, file prose, or general changelog read. Requires drift/error/credential/EVAL-0068/EVAL-0083 conformance. |
| `nexus-mods-v2-graphql` | **Accepted content fallback; implementation not delivered** | Live schema: 347 types, 66 query fields, 97 mutation fields; canonical shape SHA-256 `4bccad0de29d7fd978a6fa282a757a112eade3bdad75ed7b16b1f523682247ec`; first-party client `1.7.3` commit `00c526204368fd2386d67ca5a88a491284587e07`; authenticated verification 2026-07-28 | Long descriptions, mod version/update time, page requirements/reverse dependents, file descriptions, and file-level changelogs | No published versioned schema; client/schema drift was observed; no reliable quota headers. Requires minimal Infinium-owned query qualification and failure-closed drift handling. |
| `nexus-mods-v1-legacy` | **Accepted last-resort route; implementation not delivered** | Swagger API `1.0`; SHA-256 `7730560e9e0abe299a602e3a563cd53230cabd9853ceb6352b08f9188d9a4c2a`; authenticated verification 2026-07-28 | General mod changelog read; descriptive-content fallback | Legacy/sparse contract. It is not preferred where v3/v2 supply the required content. |

Each acquisition freezes its selected route, contract/schema/query, entity,
retrieval time, revision signals, permitted projected-content fingerprint, and
coverage/error outcome. “Latest” applies to route resolution for a new
acquisition, not to historical replay.

## Accepted Wave D non-Nexus source records

Rows remain subject to their stated decision status. An inactive or optional
template must never be instantiated merely because a hostname, path, feed,
`robots.txt`, public repository, or search result exists.

| Source ID | Status | Verified date/identity | Bounded role | Capability gaps or activation conditions |
|---|---|---|---|---|
| `local-installed-documentation` | **Accepted core source class; adapter not delivered** | Repository/product contract reviewed 2026-07-26; physical identity is future snapshot/provider/path/hash/parser provenance | Snapshot-bound supported local and qualified in-archive documentation; author authority only when document-to-mod/author/version provenance is established | Exact formats, archive shapes, parser limits, licence/provenance, provider identity, traversal rejection, and malformed/unsupported behavior require M1 contract qualification |
| `loot-skyrimse-masterlist` | **Accepted managed-data source and refresh design; implementation not delivered** | `v0.29` head commit `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f`; blob `775bdd12a48662b749b936a7ae77c951c9bc014e`; SHA-256 `68ccc51e800e294fe8e5fcf93c1cbbea0c3326dffb29aae67623d486fed6f02d`; CC0-1.0; reverified 2026-07-28 | Curated LOOT metadata through ADR-0011's immutable managed-data boundary | Accepted ADR-0014 owns startup/interval discovery, immutable fetch, pair activation, rollback, and run isolation. EVAL-0053 remains a delivery gate; a raw YAML item is not locally applicable by itself. |
| `loot-prelude` | **Accepted managed-data source and refresh design; implementation not delivered** | `v0.29` head commit `ea316265c11b5c6e6f51d53deb34c4054f4c2349`; blob `4988f2a853a15aa3f14bd6713d794f256b1c738b`; SHA-256 `2c95fc5f5088597d9cc85bbf341d867c2f7e2740ddae2c6329942cf6b56adb15`; CC0-1.0; reverified 2026-07-28 | Separate curated prelude identity consumed with a compatible masterlist revision | Same accepted ADR-0011/ADR-0014/EVAL-0053 boundary; masterlist and prelude become active only as one validated immutable pair. |
| `github-official-project-documentation` | **Optional/later; not M1 core** | GitHub REST API `2026-03-10`; public commit-pinned experiment verified 2026-07-26 | Positively mapped maintainer-owned documentation/release metadata | Rare source path; no broad repository-search machinery is justified for M1. Exact mapping, licence/content, path, redirect, rate, revision, transmission, and export controls remain required. |
| `github-official-project-community` | **Optional/later investigative lane** | GitHub REST API `2026-03-10`; bounded public issue read verified 2026-07-26 | Investigative issues/comments inside an already mapped official repository | Not core coverage; community transport never grants author authority. |
| `author-site-explicit-interface` | **Disabled template; not a source** | Template reviewed 2026-07-26; no hostname/path instance selected | Future host/path-specific first-party API, feed, or static-document endpoint | Requires exact author/project mapping, hostname/path, supported interface, terms/licence, crawler policy where applicable, rate/redirect/byte limits, four independent handling decisions, freshness, and stop triggers. Missing or ambiguous policy is a gap, not approval. |
| `openai-web-discovery` | **Accepted discovery-provider capability; not a source; implementation not delivered** | Official OpenAI Responses `web_search` documentation reverified 2026-07-28 in RESEARCH-0032 | Approved-domain discovery and separately opt-in community discovery records/leads | Governed by accepted ADR-0013; exact model/tool conformance remains pending. URLs, titles, actions, sources, citations, and model prose do not grant landing-source acquisition permission or evidence authority. |

The underlying research handling matrices, exact field sets, and alternatives
are in
[RESEARCH-0026](investigations/RESEARCH-0026-non-nexus-source-governance.md).
Discovery, acquisition, extraction, and local application remain distinct
provenance events. Search rank, snippets, provider-generated answers, repository
visibility, or local copying never grants acquisition permission or evidence
authority.

## Accepted Wave D provider-contract authorities

These are technical research authorities for accepted ADR-0013. They are not
mod-analysis evidence, adapter conformance, account authorization, or
source-transmission permission.

| Authority set | Status | Verified date | Registered research use and gap |
|---|---|---|---|
| JSON Schema 2020-12 core/validation | **Accepted design authority; implementation subset not qualified** | Retrieved 2026-07-26 | Standards baseline for schema-bound semantic operations; provider constrained-output subsets do not implement the full standard |
| OpenAI direct API documentation and OpenAPI `2.3.0` | **Accepted initial-provider design authority under ADR-0013** | Reverified 2026-07-28 | Responses, Structured Outputs, hosted web search, background, Batch, caching, usage/cost/rate, and retention design input; exact account/model conformance remains pending |
| Anthropic Claude Platform documentation | **Historical portability evidence; no M1 adapter proposed** | Retrieved 2026-07-26 | Preserved capability comparison only; a second-provider adapter does not gate M1 |
| Google Gemini structured-output documentation | **Historical schema-portability evidence** | Retrieved 2026-07-26 | Preserves the finding that schema shape still requires host semantic validation; not an M1 adapter proposal |

Provider research does not select the desktop stack, credential store, model,
or persistence mechanism. The original comparison is preserved in
[RESEARCH-0027](investigations/RESEARCH-0027-provider-neutral-llm-contract.md),
[RESEARCH-0028](investigations/RESEARCH-0028-provider-capability-and-authentication.md),
and RESEARCH-0029. The accepted OpenAI-first capability research is in
[RESEARCH-0032](investigations/RESEARCH-0032-openai-first-llm-and-web-search.md)
and ADR-0013. Provider-independent domain truth remains required;
lowest-common-denominator capability parity does not.

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
| [Skyrim SE LOOT masterlist](https://github.com/loot/skyrimse) and [LOOT prelude](https://github.com/loot/prelude) | `v0.29`, commits `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f` and `ea316265c11b5c6e6f51d53deb34c4054f4c2349` | CC0 classification plus ADR-0011/ADR-0014 managed-data provenance and refresh design; implementation and EVAL-0053 conformance remain pending |

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

## Evaluator-private repository authorities

These sources support RESEARCH-0052 and ADR-0026's development/evaluation
repository boundary. They do not authorize public disclosure of private
fixtures or grant ordinary workflows access to the private store.

| Authority | Checked identity | Registered use |
|---|---|---|
| Git project, [Git Tools - Submodules](https://git-scm.com/book/en/v2/Git-Tools-Submodules) | Retrieved 2026-08-01 | Primary documentation for separate histories, recorded submodule URLs/commit pointers, and recursive acquisition behavior used in the submodule rejection |
| GitHub Docs, [Repository roles for an organization](https://docs.github.com/en/organizations/managing-user-access-to-your-organizations-repositories/managing-repository-roles/repository-roles-for-an-organization) | Retrieved 2026-08-01 | Private-repository role and future multi-maintainer access-control evidence |
| GitHub Docs, [Sharing actions and workflows from your private repository](https://docs.github.com/en/actions/how-tos/reuse-automations/share-across-private-repositories) | Retrieved 2026-08-01 | Cross-repository workflow token/log exposure warning supporting private-runner isolation |

## Search behavior

Default search should use approved primary/technical sources. Broader community
web search is opt-in and retains authority classification.

An LLM does not choose arbitrary source authority. The registry and adapter
policy determine what can be searched and how each claim is classified and
applied. Authority is assigned for declared claim types/scopes rather than as a
single total rank for everything a source might say.
