# RESEARCH-0030: Nexus latest-interface qualification

Status: Completed
Disposition: recommendation accepted by ADR-0012
Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-008

M0 wave: D — Documentation acquisition and LLM boundary revision

Decision enabled: Nexus interface-selection and acquisition ADR amendment;
source-registry capability update; removal of the previous authenticated and
GraphQL-policy qualification blockers

Subsequent disposition: The project owner accepted the executive risk
direction and latest-capable routing in
[ADR-0012](../../architecture/decisions/ADR-0012-nexus-latest-capable-api-routing.md).
Production adapter and conformance work remain undelivered.

## Executive answer

Infinium should use a **latest-capable-interface-per-content** policy rather
than selecting one Nexus API version for every operation:

1. Prefer the current v3 API for every content type it actually exposes.
2. Use the live v2 GraphQL API for required fields v3 does not expose.
3. Use legacy v1 only for a required content type neither v3 nor v2 supplies,
   or as an explicitly degraded fallback while a newer-interface capability
   is unavailable.
4. Resolve and record that routing at the start of an acquisition run. Do not
   silently change interfaces inside the run.

Bounded authenticated tests confirmed:

- v3 supplies current mod identity, batch summaries, file containers, exact
  file-version identities, upload times, and typed file-version/DLC
  dependencies. It does not supply long descriptions, page-level legacy
  requirements, descriptive file text, or read access to general mod
  changelogs.
- live v2 GraphQL supplies long descriptions, mod version/update time,
  page-level requirements, reverse dependent-mod pages, file descriptions,
  and file-level changelog arrays. Its schema is introspectable, but is not
  published as a versioned public schema.
- v1 supplies the same tested long description as GraphQL, general mod-level
  changelogs, and descriptive file metadata. It remains the only qualified
  route for general mod changelog reads, but should otherwise be a fallback.
- no usable mod-identity-to-article, mod-post/comment-thread,
  author/sticky-post, or mod-page bug-report read was found. The GraphQL schema
  contains a generic comment service and collection bug reports, but exposes
  no relation from a `Mod` to a comment thread and no mod bug-report type.
  Those requested surfaces remain explicit coverage gaps.

The former requirement for separate proof that Nexus expressly approves v2
GraphQL is no longer a project blocker. The project owner has explicitly
directed development to assume that Infinium may use **any Nexus-provided API
needed for its diagnostic purpose**, including GraphQL, and may introspect and
test those APIs. Unresolved interpretation of Nexus policy must not block
development or capability research. This is an executive development-risk
direction and should be recorded by an ADR amendment. It does not authorize
page scraping, browser automation, access-control bypass, unrelated bulk
collection, mutation, file downloads, public rehosting, or secret disclosure.

## 1. Question and accepted constraints

### 1.1 Question

Which current Nexus API should Infinium use for descriptions, requirements,
articles, changelogs, files, posts, and revision identity when the product
prefers the latest interface but must retain required content coverage?

### 1.2 Linked requirements and decisions

| Authority | Relevance |
|---|---|
| `DOC-001`–`DOC-006` | Source coverage, authority, and unsupported gaps remain explicit. |
| `DOC-008`–`DOC-011` | Retained evidence requires source/interface/revision and acquisition-run provenance. |
| `SEC-001`–`SEC-002` | API text is untrusted and credentials never enter ordinary output. |
| `SCAN-003`–`SCAN-006` | Quota, error, pagination, and source failures remain visible. |
| `SNAP-001`, `SNAP-006` | A run records resolved live inputs and honest replay limits. |
| `EVID-002`–`EVID-003` | API capability does not change claim authority or local applicability. |
| ADR-0001 | Nexus text may support external claims but cannot determine local state. |
| ADR-0002 | Acquired source evidence retains its own run and revision identity. |
| ADR-0005 | The project accepts Nexus supported-API analysis under an explicit owner risk decision. |
| 2026-07-28 owner direction | Any Nexus-provided API read interface needed by Infinium, including GraphQL, may be researched and used during development; unresolved policy interpretation is non-blocking. |

### 1.3 Scope and non-scope

In scope:

- current v3 OpenAPI and relevant read schemas;
- current first-party client and live v2 GraphQL schema;
- legacy v1 description, changelog, and file reads;
- bounded authenticated field-presence, pagination, revision, quota, and error
  observations;
- latest-first content routing and provenance.

Out of scope:

- Nexus pages, browser sessions, scraping, or traffic inspection;
- mod files, content previews, media, or download links;
- mutations or account changes;
- exhaustive load/rate testing;
- implementing a production adapter or credential store;
- deciding public-release registration, redistribution, or legal questions.

## 2. Sources and exact identities

All sources and interfaces were retrieved or rechecked on 2026-07-28.

| ID | Primary source | Exact identity and relevance |
|---|---|---|
| S1 | [Nexus v3 API documentation](https://api-docs.nexusmods.com/) and [OpenAPI](https://api.nexusmods.com/openapi.yaml) | OpenAPI `3.0.3`; API `3.0.0`; `Last-Modified: Mon, 27 Jul 2026 15:25:39 GMT`; 106,234 bytes; SHA-256 `58227301a8c8a30f01fae8c9fdf913cfdd989bee46eaff7edbb5619146ee6d7c`. |
| S2 | [Legacy v1 Swagger](https://app.swaggerhub.com/apis-docs/NexusMods/nexus-mods_public_api_params_in_form_data/1.0#/) and [JSON](https://api.swaggerhub.com/apis/NexusMods/nexus-mods_public_api_params_in_form_data/1.0/swagger.json) | Swagger `2.0`; API `1.0`; 16,634 bytes; SHA-256 `7730560e9e0abe299a602e3a563cd53230cabd9853ceb6352b08f9188d9a4c2a`. |
| S3 | [First-party `node-nexus-api`](https://github.com/Nexus-Mods/node-nexus-api) | Package `1.7.3`; commit `00c526204368fd2386d67ca5a88a491284587e07`, committed 2026-07-27. Current REST/GraphQL methods and field-model evidence. |
| S4 | Live authenticated v2 GraphQL introspection at `https://api.nexusmods.com/v2/graphql` | HTTP 200; 347 types, 66 query fields, and 97 mutation fields. Canonicalized schema-shape SHA-256 `4bccad0de29d7fd978a6fa282a757a112eade3bdad75ed7b16b1f523682247ec`. |
| S5 | [Nexus rate-limit guidance](https://help.nexusmods.com/article/105-i-have-reached-a-daily-or-hourly-limit-api-requests-have-been-consumed-rate-limit-exceeded-what-does-this-mean) | Updated 2026-06-03. Publishes 20,000 requests per 24 hours and 500 per hour after that limit; live headers remain the request-specific evidence. |

S1 labels every relevant v3 read used here **Experimental**. “Latest” therefore
means the newest Nexus interface that supplies the required capability, not a
promise of stability.

## 3. Authenticated experiment

### 3.1 Authorization and secret handling

The owner supplied a personal development API key in repository-root
`temp.txt` and explicitly authorized bounded research calls.

- The key was read only inside each request process.
- It was never printed, placed on a command line, copied into a document,
  retained in an artifact, or sent to another service.
- No account values from the validation response were retained or reported.
- Fewer than 60 authenticated read-only requests were issued in total,
  including schema introspection and negative controls.
- No raw Nexus content body was written to the repository.
- `temp.txt` was already ignored before this report was written.

The probes used Node.js `24.11.1` and accurate temporary application identity
headers. Standard Nexus request logs and the observed API quota consumption
were the only remote effects.

### 3.2 Bounded entities and calls

Tests used two public Skyrim Special Edition mod IDs:

- one content-rich entity with a long description, a legacy requirement,
  reverse dependents, a mod changelog, and multiple files;
- one valid entity with no page-level requirement rows.

Negative controls used one nonexistent ID. Calls covered:

- v1 mod details, changelogs, files, validation, and conditional GET;
- v2 GraphQL schema/type introspection, direct mod, mod-by-UID, requirements,
  and file projections;
- v3 mod, mod batch, files, file versions, and file-version dependencies.

No file payload, media, article, post, comment, bug report, page, or mutation
was requested.

## 4. Verified capability matrix

| Content surface | v3 `3.0.0` | v2 GraphQL live schema | v1 live read | Recommended route |
|---|---|---|---|---|
| Mod identity/name | Yes: global/composite, game-scoped, and game IDs plus name | Yes | Yes | **v3** |
| Short summary | Yes through `POST /mods/batch` | Yes through direct `mod`; one tested `modsByUid` projection returned an empty summary | Yes | **v3 batch** |
| Long description | No | Yes, non-null string | Yes | **v2**, v1 fallback |
| Mod version and coarse update time | No | Yes: `version`, `createdAt`, `updatedAt` | Yes: version and timestamps | **v2**, v1 fallback |
| Page-level/legacy requirements | No read model | Yes: DLC, Nexus requirements, reverse dependents, notes, URLs, IDs, pagination, and `legacyModRequirementsEnabled` | Tested mod payload omitted the optional client-modeled `requirements` field | **v2** |
| File-version/DLC dependencies | Yes: typed stored ranges, alternatives, and DLC definitions | Not the same semantic surface | No equivalent typed route | **v3** |
| File containers and exact versions | Yes: file container IDs, version IDs, positions, names, versions, categories, upload times, and primary state | Legacy-style file identities | Yes | **v3** identity; v2 descriptive supplement |
| File descriptions | No | Yes | Yes | **v2**, v1 fallback |
| File-level changelog | No | Yes: `changelogText[]` | Yes: `changelog_html` | **v2**, v1 fallback |
| General mod changelog | Write only, no read | No mod-level changelog read found | Yes: version-keyed changelog read | **v1 only** |
| Articles | No | No article type/query found | No | Unsupported gap |
| Mod posts/comments | No | Generic comments exist, but `Mod` has no thread/comment relation | No | Unsupported from mod identity |
| Author/sticky posts | No | Comment objects have pin/creator fields, but no mod-to-thread discovery path | No | Unsupported from mod identity |
| Mod bug reports | No | Collection bug reports exist; no mod bug-report read/type relation | No | Unsupported gap |

### 4.1 Description and requirement comparison

For the content-rich entity:

- v1 and direct v2 GraphQL returned long descriptions of identical length and
  identical SHA-256 after projecting only that field;
- summary and version values agreed;
- v1 and GraphQL created/updated values represented the same instants despite
  different serialization formats;
- v1 omitted `requirements`;
- GraphQL returned one forward Nexus requirement and a paginated reverse
  dependency population.

The valid absent-content control returned a description and zero forward,
reverse, and DLC requirement rows without error.

### 4.2 GraphQL schema/client drift

The current first-party client `1.7.3` contains a convenience method that
queries a top-level `modRequirements` field. The live introspected `Query`
type does not contain that field, and the call failed schema validation.
Requirements remain available as nested `Mod.modRequirements`, which was
successfully queried.

This is direct evidence that Infinium must not treat client compilation or
first-party source presence as live GraphQL conformance. It should:

- use its own versioned, minimal query documents;
- introspect/qualify required schema shapes;
- fail a capability closed when required fields disappear;
- record client, schema, query, and response identities independently.

The tested direct `mod` query returned the non-empty summary, while the
`modsByUid` projection returned an empty summary for the same entity. v3 batch
is therefore the preferred summary route.

### 4.3 Pagination

GraphQL requirement pages expose `offset`, `count`, `nodesCount`, and
`totalCount`. Two bounded reverse-requirement requests at offsets zero and two
returned distinct two-row pages with stable totals. File and mod batch
operations also expose bounded/page semantics in the current client or
contract, but their production limits remain adapter capability data rather
than product constants.

## 5. Revision, caching, quota, and errors

### 5.1 Revision and freshness

No interface exposes a field-level revision ID for a description,
requirement-set, or general mod changelog.

Available signals are:

- v3 exact file-version IDs and `uploaded_at`;
- GraphQL/v1 coarse mod `updatedAt`/`updated_timestamp`;
- requirement item IDs without requirement-set update time;
- v1 general changelog version keys;
- GraphQL/v1 file IDs, versions, and upload dates;
- retrieval time and permitted projected-content fingerprint;
- response `ETag` where observed.

The v1 GET honored `If-None-Match` with HTTP 304 and an empty body. A GraphQL
POST returned HTTP 200 despite an identical `If-None-Match`, so its ETag must
not be assumed to support conditional refresh.

For replay and invalidation, every acquisition unit should retain:

```text
source_id = nexus-mods
interface: v3 | v2-graphql | v1
interface_contract_or_schema_identity
adapter_identity
query_or_operation_identity
entity_ids
requested_content_group
retrieval_time
coarse_source_revision_signals[]
response_validator?
permitted_projected_payload_fingerprint
coverage_or_error_outcome
```

“Use latest” applies when resolving a new acquisition. A completed or resumed
run uses its recorded routing/schema/query identity. A new live schema or
route creates new acquisition evidence; it does not rewrite earlier evidence.

### 5.2 Quota and rate visibility

Authenticated v1/v3 responses exposed:

- `x-rl-daily-remaining`;
- `x-rl-hourly-remaining`;
- daily/hourly reset headers; and
- 429/backoff behavior in the first-party client.

The initial live values were 20,000 daily and 2,000 hourly. The latter is an
observed live header, not a replacement for Nexus's published post-daily-limit
500-per-hour rule. GraphQL responses exposed no equivalent remaining/reset
headers in these tests.

Consequences:

- v1/v3 quota estimates can use the latest observed headers;
- GraphQL quota/headroom must be reported as unavailable unless a later
  documented capability supplies it;
- Infinium must not invent “remaining API calls” for GraphQL;
- provider quota is not an Infinium hard-budget mechanism.

### 5.3 Error normalization

The invalid-ID controls produced:

- v1: HTTP 404 with a legacy error object;
- v3: HTTP 404 with RFC-style problem JSON;
- GraphQL: HTTP 200 with `errors`, null data, and `NOT_FOUND`;
- v3 batch: HTTP 200 with an empty result for an unknown composite ID.

The adapter must normalize transport status, GraphQL error state, visibility,
true absence, and empty batch results without interpreting any of them as “no
documentation” automatically.

## 6. Alternatives

| Alternative | Result |
|---|---|
| v3 only | Rejected: cannot provide long descriptions, page requirements, file descriptions, or general changelog reads. |
| v2 GraphQL only | Rejected: lacks v3's current typed file/version/dependency contract and lacks general mod changelog reads; live schema can drift. |
| v1 only | Rejected: legacy, weaker typed identities, and missing tested requirements field. |
| First-party client without live qualification | Rejected: its top-level requirements method currently disagrees with the live schema. |
| Latest-capable route per content type | **Recommended:** maximizes current coverage while making fallbacks and drift explicit. |
| Page scraping for unsupported surfaces | Rejected and outside the owner's API authorization. |

## 7. Recommendation

Confidence: **High** for the tested field presence, schema shape, and routing;
**medium** for durability because v3 reads are Experimental and v2 has no
published versioned schema.

1. Accept the latest-capable routing order in this report.
2. Amend or supersede ADR-0005 so it states unambiguously that, during
   development, unresolved Nexus policy interpretation does not block use,
   introspection, or testing of any Nexus-provided API read interface needed
   by Infinium, including v2 GraphQL. Retain the no-page/no-bypass/no-rehost
   boundary.
3. Remove the separate “GraphQL express approval” Gate D blocker. Current
   first-party maintenance plus authenticated live schema/call evidence is
   sufficient under the owner's executive risk direction.
4. Treat v3/v2/v1 as separately versioned capabilities and record the resolved
   route per content group.
5. Require a small schema/contract qualification at startup or adapter
   freshness intervals, with cached last-known-good capability data for
   offline disclosure. Do not introspect before every request.
6. Preserve unsupported article/post/sticky/bug-report populations as source
   coverage gaps. Do not substitute unrelated generic comments, collection bug
   reports, search snippets, or page access.
7. Reverify the routing when the v3 OpenAPI hash/version changes, the GraphQL
   schema fingerprint changes, the first-party client changes materially, a
   required call fails schema validation, or Nexus responds to the owner's
   policy inquiry.

## 8. Exact downstream updates enabled

- RQ-008 may be marked researched and recommended for resolution by the
  applicable accepted ADR/source decision.
- The prior authenticated Nexus qualification blocker is satisfied.
- The prior separate GraphQL-policy approval blocker is superseded by the
  owner's 2026-07-28 executive direction.
- The source registry should replace its v3/v1-first posture with the
  per-content routing matrix above.
- EVAL-0068 should cover v3 success, v2 fallback, v1-only changelog,
  live-schema drift, unsupported source surfaces, and no page fallback.
- EVAL-0083 should require interface/spec/schema/query/fingerprint provenance
  across acquisition, extraction, and later application.
- RQ-013/RQ-032 should implement immutable routing/schema provenance,
  untrusted HTML/BBCode handling, credential isolation, and failure-closed
  capability changes.

No production adapter, credential mechanism, persistence mechanism, or public
release permission is accepted by this report alone.

## 9. Validation performed

- Read the complete required authoritative product order, relevant ADRs,
  registries, M0 Wave D plan, RESEARCH-0025, and RESEARCH-0029.
- Re-fetched and fingerprinted the current v3 and v1 specifications and
  current first-party client revision.
- Validated the supplied key without retaining or displaying account data.
- Ran bounded authenticated v1/v2/v3 positive, absent-content, pagination,
  conditional-request, and invalid-ID controls.
- Introspected and canonical-fingerprinted the live v2 schema.
- Compared projected description/version/time fields without retaining raw
  source bodies.
- Made no mutation, file download, page request, browser request, scraping
  request, paid/provider call, or raw-source tracked write.
- Performed a semantic review for API-tier conflation, field-level revision
  overclaiming, GraphQL support assumptions, unsupported-source substitution,
  and accidental architecture acceptance.
- Validated the report's local links and ran `git diff --check`.
