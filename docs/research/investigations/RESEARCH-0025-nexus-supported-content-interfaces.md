# RESEARCH-0025: Nexus supported content interfaces

Status: Completed; operative recommendation superseded

Subsequent revision: Authenticated
[RESEARCH-0030](RESEARCH-0030-nexus-latest-interface-qualification.md) and
accepted ADR-0012 supersede this report's operative interface recommendation,
authenticated-qualification blocker, and separate GraphQL-approval blocker.
This report remains dated historical evidence.

Date: 2026-07-26

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-008

M0 wave: D — Documentation acquisition and provider-neutral LLM boundary

Decision enabled: Nexus source-adapter and source/entity/version acquisition
contract; exact source-registry capability/gap update; inputs for RQ-011,
RQ-013, RQ-032, EVAL-0068, and Gate D

## Executive answer

No one current Nexus interface provides all of the requested documentation
surfaces.

Nexus currently exposes three materially different interface tiers:

1. The official **v3 OpenAPI 3.0.0** contract is current and provides mod
   identity/name, sanitized short summaries through a batch operation,
   file/file-version identity and metadata, and typed file-version/DLC
   dependency definitions. Every relevant read operation is marked
   **Experimental**. It does not expose long mod descriptions, general
   page-level requirements, article bodies, changelog reads, or posts.
2. The officially linked **legacy v1 REST API** still documents reads for mod
   details, changelogs, and file metadata. The current first-party
   `@nexusmods/nexus-api` client models long BBCode descriptions, mod update
   timestamps, file descriptions, file changelogs, upload timestamps, and a
   requirements field on the v1 mod-details result. Nexus describes v1 as a
   legacy integration surface and publishes no v1 stability/deprecation
   promise comparable to v3.
3. The maintained first-party client also uses a **v2 GraphQL** endpoint and
   exposes selectable mod descriptions, summaries, versions, `updatedAt`,
   page-level Nexus/DLC/dependent-mod requirements, file descriptions, file
   changelog text, and file identities. The client and its generated
   documentation are current primary evidence that this interface exists, but
   the investigation found no separately versioned public GraphQL schema,
   stability badge, or deprecation contract for those mod-content fields.

No inspected current official contract or maintained first-party client
exposes general mod articles, ordinary mod-page posts/comments, sticky/author
posts, or mod-page bug-report reads. Those are unsupported source surfaces and
must remain coverage gaps; ADR-0005 prohibits page, browser-session, or
undocumented-endpoint fallbacks.

Revision identity is uneven. File versions have useful stable-looking IDs,
version strings, and upload times. Mod descriptions have only a coarse
mod-level `updated_timestamp`/`updatedAt`; changelogs are keyed by author
version strings; and page-level requirements expose item IDs in the
first-party GraphQL model but no documented update time. None is an adequate
independent content revision for every field. The acquisition contract should
therefore retain the strongest available entity/revision signals, retrieval
time, interface/spec/client identity, response validators when actually
observed, and a fingerprint of every permitted retained payload or excerpt.
It must not present a mod update time as proof that an individual description
or requirement did not change.

No Nexus credential was available through an approved non-printing handoff.
The investigation made no authenticated request and acquired no mod-content
payload. Two correctly identified unauthenticated requests confirmed that the
documented v3 and v1 mod-detail routes return HTTP 401 without a credential,
but they did not qualify response bodies, visibility, age filtering, quota
headers, validators, or live schema behavior. That bounded authenticated
qualification remains an explicit M0 experiment gap rather than permission to
broaden access.

## 1. Question and accepted constraints

### 1.1 Primary question

Which currently supported Nexus Mods APIs/interfaces provide descriptions,
requirements, articles, changelogs, files, posts, and revision identity?

### 1.2 Linked accepted requirements

| Requirement | Relevance |
|---|---|
| `SCOPE-004` | Acquisition remains manually initiated through M4. |
| `SEC-001`–`SEC-002` | Returned text is untrusted; credentials remain outside prompts, logs, exports, and ordinary traces. |
| `SCAN-003`–`SCAN-006` | Auth, rate-limit, cost/time, failure, cancellation, and skipped-source behavior must be visible. |
| `SNAP-001`, `SNAP-006` | Acquisition evidence retains resolved source/interface revision and honest replay gaps. |
| `EVID-002`–`EVID-003` | Claims retain source/version/retrieval provenance and do not become local-state authority. |
| `DOC-001`–`DOC-006` | Full configured coverage reports every eligible, unresolved, unavailable, and failed source; no prohibited fallback is allowed. |
| `DOC-008`–`DOC-011` | Permitted source passages remain available through useful dependent work; freshness, acquisition ownership, resolved revision, and application links remain explicit. |
| `AI-003`, `AI-006` | Only task-relevant permitted content may enter a provider envelope, with exact retained request/response provenance. |
| `OPS-001`–`OPS-003` | Live, cached, authenticated, replay, retention, and export behavior remain separately declared. |

### 1.3 Accepted ADR constraints

| ADR | Constraint |
|---|---|
| [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md) | Nexus text may establish a sourced claim within its authority; it cannot decide local installed/effective state. |
| [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md) | Source evidence remains acquisition-run/source/version bound and gains a separate application link when consumed. |
| [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md) | Acquisition writes only product-owned data and never modifies the modding setup. |
| [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md) | Work is manually initiated and bounded to the initial Skyrim SE/MO2 product. |
| [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md) | Only documented supported interfaces may be used. Scraping, browser automation, undocumented/private endpoints, bulk unrelated collection, training, rehosting, and raw public redistribution remain excluded. |

The accepted [Wave A policy investigation](RESEARCH-0001-nexus-access-policy.md),
[retention policy](RESEARCH-0003-retention-replay-export-policy.md), and
[Wave A integration](RESEARCH-0004-wave-a-policy-and-evidence-handling-integration.md)
control acquisition and handling. This report inventories technical
capability; it does not reopen those policy decisions.

## 2. Scope, non-scope, and preflight

### 2.1 In scope

- Current official v3 OpenAPI operations and schemas relevant to requested
  content.
- The officially linked legacy v1 Swagger contract.
- The current maintained first-party Node client, its documented REST/GraphQL
  methods, types, exact commit, and package version.
- Authentication schemes, published rate limits, documented/read-observed
  errors, freshness/revision signals, and coverage semantics.
- Bounded public-contract retrieval and unauthenticated negative requests.
- The minimum acquisition-contract recommendation enabled by these facts.

### 2.2 Explicit non-scope

- HTML or authenticated-page access, crawling, browser automation, search
  cache reconstruction, or undocumented/private endpoint discovery.
- Mod-file payload downloads, content-preview downloads, media retrieval, or
  account changes.
- Authenticated experiments without a credential supplied through an approved
  non-printing handoff.
- Age-restricted/private/unpublished/moderated content behavior beyond what
  current contracts state.
- Selecting a production HTTP/GraphQL library, credential store, database,
  cache duration, provider, model, or application architecture.
- Treating a first-party client implementation as a stability promise that
  Nexus did not publish.
- Updating the RQ registry, source registry, ADRs, product documents,
  evaluation catalog, milestone plan, or investigation index.
- Legal advice or a conclusion that technical availability changes ADR-0005's
  policy risk.

### 2.3 Preflight and effects

| Item | Treatment |
|---|---|
| Local private data | Not required or accessed. |
| Network | Public official Nexus help, API specifications, and official GitHub repository; two unauthenticated documented API requests. |
| Credential check | Environment-variable names were checked without printing values; no Nexus credential was available. No credential, cookie, token, or account identifier was accessed. |
| Authenticated/paid access | Not available and not used. |
| External tools | PowerShell 7.6.3 for public HTTP retrieval/hashing and GitHub API reads; Git 2.55.0 and Node 24.11.1 only for environment identification. |
| Remote side effects | Ordinary public-document/API access logs only. The two negative API requests returned 401 and did not retrieve content or change state. |
| Local side effects | This report only. No fetched specification, response body, source text, or raw artifact was written to the repository. |
| Stop condition | Stop before a content-bearing request without an approved credential; stop at any unsupported content surface rather than use a page or undocumented fallback. |

## 3. Sources and exact identities

All external sources were retrieved or rechecked on **2026-07-26**.

| ID | Official/primary source | Exact identity | Claim-level relevance |
|---|---|---|---|
| S1 | [Nexus API v3 documentation](https://api-docs.nexusmods.com/) and [OpenAPI specification](https://api.nexusmods.com/openapi.yaml) | OpenAPI `3.0.3`; API `3.0.0`; HTTP `Last-Modified: Thu, 23 Jul 2026 15:06:41 GMT`; 106,306 UTF-8 bytes; SHA-256 `7ff2bdbded673ca33bed3e6e835d34dbbf42ee29b7b8becd2581556f5b601b07` | Current documented v3 operations, schemas, auth, stability badges, and deprecation policy. |
| S2 | [Officially linked legacy v1 Swagger documentation](https://app.swaggerhub.com/apis-docs/NexusMods/nexus-mods_public_api_params_in_form_data/1.0#/) and [JSON specification](https://api.swaggerhub.com/apis/NexusMods/nexus-mods_public_api_params_in_form_data/1.0/swagger.json) | Swagger `2.0`; API `1.0`; HTTP `Last-Modified: Mon, 27 May 2019 10:12:20 GMT`; 16,634 UTF-8 bytes; SHA-256 `7730560e9e0abe299a602e3a563cd53230cabd9853ceb6352b08f9188d9a4c2a` | Official legacy routes for mod details, changelogs, and files; sparse response-schema/error evidence. |
| S3 | [Official `node-nexus-api` repository](https://github.com/Nexus-Mods/node-nexus-api) | `master` commit [`ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0`](https://github.com/Nexus-Mods/node-nexus-api/commit/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0), committed 2026-07-23; package `@nexusmods/nexus-api` `1.7.2` | Current first-party REST/GraphQL client methods, endpoint use, error handling, and documented application-auth behavior. |
| S4 | Pinned first-party [`Nexus.ts`](https://github.com/Nexus-Mods/node-nexus-api/blob/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0/src/Nexus.ts), [`types.ts`](https://github.com/Nexus-Mods/node-nexus-api/blob/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0/src/types.ts), [`typesGraphQL.ts`](https://github.com/Nexus-Mods/node-nexus-api/blob/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0/src/typesGraphQL.ts), and [`parameters.ts`](https://github.com/Nexus-Mods/node-nexus-api/blob/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0/src/parameters.ts) | Same commit as S3 | Exact client evidence for v1 REST, `/v2/graphql`, descriptions, requirements, changelogs, files, timestamps, quota headers, and limitations. Code presence is not a separately published schema-stability guarantee. |
| S5 | [Nexus API Acceptable Use Policy](https://help.nexusmods.com/article/114-api-acceptable-use-policy) | Last updated 2020-12-01 | Personal/development keys, public-app registration, accurate application headers, user initiation, revocation, and unacceptable use. ADR-0005 owns the project's policy interpretation. |
| S6 | [Nexus public API rate-limit guidance](https://help.nexusmods.com/article/105-i-have-reached-a-daily-or-hourly-limit-api-requests-have-been-consumed-rate-limit-exceeded-what-does-this-mean) | Last updated 2026-06-03 | Published 20,000-request daily state, subsequent 500-per-hour state, reset times, and remaining-request response headers. |

### 3.1 Source precedence and applicability

S1 is the current versioned v3 public contract. Its own overview calls S2
legacy and states that Experimental endpoints may change significantly or be
removed and are not recommended for production. S2 remains officially linked
and S3 still implements its reads, but neither fact gives v1 the v3 stable
endpoint guarantee.

S3/S4 are strong current first-party capability evidence. They document
GraphQL query use and exact fields expected by the maintained client. They are
not equivalent to a separately published GraphQL introspection snapshot,
schema version, field-level deprecation policy, or statement that every
client-exposed field is supported for independent third-party production use.

## 4. Reproducible checks and artifact manifest

### 4.1 Check C1 — v3 contract identity and operation inventory

Read-only procedure:

```powershell
$response = Invoke-WebRequest `
  -Uri 'https://api.nexusmods.com/openapi.yaml' `
  -UseBasicParsing
$bytes = [Text.Encoding]::UTF8.GetBytes($response.Content)
[Convert]::ToHexString(
  [Security.Cryptography.SHA256]::HashData($bytes)
)
```

Observed:

- The specification returned HTTP 200 and the identity recorded in S1.
- Global security permits either `apikey` header authentication or bearer
  JWT. Only explicitly public operations override security.
- Every requested-content-relevant read below is Experimental:

  - `GET /games/{game_domain}/mods/{game_scoped_id}`;
  - `GET /mods/{id}/files`;
  - `GET /mod-files/{id}`;
  - `GET /mod-files/{id}/versions`;
  - `GET /mod-file-versions/{id}`;
  - the file-version dependency, range, DLC, materialized, and batch reads;
  - `POST /mods/batch`.

- `GetModDetails` contains only global ID, game-scoped ID, game ID, and
  available mod name.
- `ModDetail` from the batch operation adds a sanitized short summary, status,
  thumbnail, and adult-content flag, but no long description, version,
  update time, article, changelog, or post data.
- v3 distinguishes persistent mod files from `ModFileVersion` objects. The
  latter expose an opaque ID, game-scoped ID, version, category, upload time,
  and primary state.
- v3 typed dependencies expose authored file-version ranges and DLC targets
  with unique IDs. They are not a documented replacement for every
  human-authored requirement shown on a mod page.
- The current v3 contract has `POST /mods/{id}/changelogs` for authors but no
  general changelog read.

### 4.2 Check C2 — legacy v1 contract

Read-only procedure:

```powershell
$response = Invoke-WebRequest -Uri (
  'https://api.swaggerhub.com/apis/NexusMods/' +
  'nexus-mods_public_api_params_in_form_data/1.0/swagger.json'
) -UseBasicParsing
$spec = $response.Content | ConvertFrom-Json
$spec.paths.psobject.Properties.Name
```

Observed:

- The specification returned HTTP 200 and the identity recorded in S2.
- It declares `apikey` header authentication.
- It documents:

  - `GET /v1/games/{game_domain_name}/mods/{id}.json`;
  - `GET /v1/games/{game_domain_name}/mods/{mod_id}/changelogs.json`;
  - `GET /v1/games/{game_domain_name}/mods/{mod_id}/files.json`; and
  - `GET /v1/games/{game_domain_name}/mods/{mod_id}/files/{file_id}.json`.

- The mod-details description says the server caches the result for five
  minutes. This is server behavior, not a client retention permission or an
  entity revision guarantee.
- The Swagger response schemas are too sparse to qualify current field
  presence, nullability, visibility filtering, or error bodies by themselves.

### 4.3 Check C3 — maintained first-party client

Read-only procedure:

```powershell
$commit = Invoke-RestMethod `
  -Uri 'https://api.github.com/repos/Nexus-Mods/node-nexus-api/commits/master' `
  -Headers @{ 'User-Agent' = 'Infinium-Research/0.0.0' }
$commit.sha
```

The pinned S3/S4 sources were then inspected without executing the client.

Observed:

- REST methods include `getModInfo`, `getChangelogs`, and `getModFiles`.
- `IModInfo` models:

  - summary and long BBCode description;
  - mod version and author;
  - available/moderation state;
  - created and updated timestamps; and
  - an optional detailed requirements object.

- `IFileInfo` models file ID, category, file description, HTML changelog,
  version, upload timestamp, mod version, filename, size, primary status, and
  a content-preview link.
- The GraphQL client posts to `https://api.nexusmods.com/v2/graphql`.
  Documented methods include `modsByUid`, `modFilesByUid`, and
  `modRequirements`.
- GraphQL `IMod` models long description, summary, version, `createdAt`,
  `updatedAt`, status, identity, and optionally nested requirements.
- `IModRequirements` separates DLC requirements, mods requiring this mod, and
  Nexus requirements. Requirement rows model IDs, target mod/game identity,
  notes, external-requirement flag, and URL.
- GraphQL `IModFile` models file identity, description, changelog text,
  version, date, category, name, size, and owning mod.
- The client reads `x-rl-daily-remaining` and
  `x-rl-hourly-remaining` and handles HTTP 429. Its local throttling algorithm
  is a client implementation, not a published server quota guarantee.
- The GraphQL error formatter explicitly guards against the server not
  guaranteeing one error-response shape. This supports an adapter requirement
  to preserve unknown/malformed error outcomes rather than assume one schema.

### 4.4 Check C4 — bounded unauthenticated negative requests

Two requests used accurate temporary research identity headers and no
credential:

```powershell
$headers = @{
  'Application-Name'    = 'Infinium-Research'
  'Application-Version' = '0.0.0'
  Accept                = 'application/json'
}

Invoke-WebRequest `
  -Uri 'https://api.nexusmods.com/v3/games/skyrimspecialedition/mods/1' `
  -Headers $headers

Invoke-WebRequest `
  -Uri 'https://api.nexusmods.com/v1/games/skyrimspecialedition/mods/1.json' `
  -Headers $headers
```

Both returned HTTP 401 with no captured response body and no observed
`x-rl-daily-remaining` or `x-rl-hourly-remaining` value. This verifies only
that these reads require authentication in the tested state. It does not
verify the behavior of a valid key, bearer JWT, visibility/age filtering,
response validators, quota accounting, or content fields.

### 4.5 Negative and boundary controls

- No credential value, account cookie, JWT, SSO flow, or account page was
  accessed.
- No content-bearing API response, mod file, content preview, image, article,
  post, comment, or bug report was acquired.
- No endpoint was inferred by inspecting network traffic, website JavaScript,
  or a browser session.
- No GraphQL introspection or guessed query was issued.
- No rate-limit load test, retry loop, or deliberate malformed request set was
  performed.
- No raw public document/specification was retained as a tracked artifact.

### 4.6 Artifact manifest

| Artifact | Retention | Redistribution treatment |
|---|---|---|
| This investigation | Tracked proposed Markdown | Project-authored synthesis with direct official links; no Nexus mod content or credential. |
| v3/v1 specifications and first-party source | Not retained as raw tracked files | URL, immutable identity where available, size/hash for API specifications, and paraphrased observations only. |
| Unauthenticated API results | No body was returned or retained | Status/header observations only. |
| Authenticated API payloads | Not acquired | Experiment gap. |
| Mod descriptions, requirements, articles, changelogs, file records, posts, comments, and bug reports | Not retained | Capability/schema observations only. |

## 5. Content-surface capability matrix

`Current contract` below distinguishes a published API contract from a
first-party client capability. It does not independently grant policy
permission; ADR-0005 supplies the accepted operating boundary.

| Surface | v3 OpenAPI `3.0.0` | Legacy v1 documented/current client | First-party v2 GraphQL client | Revision/freshness signal | Infinium coverage result |
|---|---|---|---|---|---|
| Mod short summary | Experimental `POST /mods/batch` returns sanitized plain-text summary | v1 `IModInfo.summary` | `IMod.summary` | Mod-level update time only in v1/GraphQL; content fingerprint still required | Available through non-stable/legacy tiers; qualify exact selected route |
| Long mod description | Not exposed | `GET .../mods/{id}.json`; current client models BBCode `description` | `IMod.description` | v1 `updated_timestamp` / GraphQL `updatedAt` are coarse mod-level signals, not description revision IDs | Candidate M1 primary source through v1; GraphQL additionally requires supported-interface approval, and either route requires authenticated payload validation |
| Page-level Nexus requirements | Not exposed as one page-level model | Current v1 client models optional `requirements`, but the legacy Swagger has no response schema | `modRequirements` exposes DLC, Nexus requirement, and reverse dependent-mod pages | Requirement item IDs; no documented requirement-set update time; retain payload fingerprint | Provisional capability only until authenticated schema/visibility and interface-support qualification |
| File-version/DLC dependencies | Experimental typed dependency/range/DLC endpoints | Not a separately documented typed v1 dependency route | Can coexist with page-level requirements | Definition/range/target IDs and exact file-version IDs; no documented update time | Available as structured file-version dependency evidence; not equivalent to every prose/page requirement |
| Mod articles | Not exposed | Not exposed | Not exposed in inspected client | None | Unsupported; explicit gap, no page fallback |
| General mod changelog | Write only; no general read | Documented `GET .../changelogs.json`, keyed by version; file metadata also carries `changelog_html` | `IModFile.changelogText` supplies file-level patch notes | Author version key or file/version ID plus fingerprint; no per-entry revision timestamp | Available through legacy/file tiers; distinguish mod-level versus file-version changelog |
| File metadata and versions | Experimental mod-file/file-version reads and batches, with typed identities | Documented file list/detail; current client models descriptions, versions, times, update links, and categories | `IModFile` exposes selectable metadata | Strongest surface: global/game-scoped IDs, file-version ID, version, upload time, category; fingerprint response | Available; v3 identity may complement v1/GraphQL descriptive fields |
| File payload/download link | Upload/download behavior exists elsewhere, but no payload was needed or tested | v1 documents download-link generation | Client supports downloads/content indexes | File/version identity, not permission to retain/redistribute payload | Outside this RQ and unnecessary for documentation acquisition |
| Author/sticky posts | Not exposed | Not exposed | Not exposed in inspected client | None | Unsupported; explicit gap |
| Ordinary posts/comments | Not exposed | Not exposed | Not exposed in inspected client | None | Unsupported; explicit gap |
| Mod-page bug reports | Not exposed | Not exposed | No mod-page bug-report read found; client feedback/issue structures are marked internal or concern other products | None | Unsupported; do not confuse internal feedback or collection structures with mod-page bug reports |

## 6. Authentication, rate limits, errors, and visibility

### 6.1 Authentication

- v3 declares global alternatives:

  - `apikey` request header; or
  - `Authorization: Bearer <JWT>`.

- v1 Swagger declares `apikey` header authentication.
- The first-party client documents per-user/per-application API keys, user or
  Nexus revocation, accurate application identity, SSO, and experimental
  OAuth/JWT support.
- ADR-0005 and S5 restrict a personal key to permitted personal/testing use and
  require registration review for a public-facing application.
- No current source establishes the final Infinium desktop credential flow.
  RQ-012/RQ-018/RQ-032 own provider/account and secure-storage mechanisms.

### 6.2 Rate limits and request accounting

S6 publishes:

- 20,000 requests per 24-hour period;
- after reaching that state, 500 requests per hour;
- daily reset at 00:00 GMT and hourly reset on each hour; and
- remaining quota in response headers.

S4 identifies the concrete remaining headers as
`x-rl-daily-remaining` and `x-rl-hourly-remaining` and handles 429. The
authenticated experiment needed to verify exact selected-interface accounting,
headers, reset values, and backoff signals was not possible. The acquisition
contract must therefore treat published quota as a provider capability and
live header observations as request evidence, not promise that one pre-run
request count can enforce every server-side limit.

### 6.3 Errors and unsupported outcomes

- v3 relevant read operations document combinations of 400, 403, and 404 and
  an RFC 9457 `ProblemDetails` response; some batch/write operations also
  document 422.
- Global authentication produced an observed 401, although the inspected
  relevant operation tables do not consistently enumerate 401 or 429.
- v1 Swagger response/error schemas are sparse. The observed unauthenticated
  route returned 401 with no captured body.
- The first-party client handles HTTP error, Nexus error, timeout, rate limit,
  protocol, JWT expiry, and GraphQL error classes, but explicitly does not
  assume one GraphQL error shape.

The future adapter must preserve at least:

```text
completed
completed-with-gaps
failed-authentication-or-revoked
failed-rate-limited
failed-transient
failed-contract-or-malformed
unavailable-by-visibility-or-policy
unsupported-interface-or-content
skipped-by-configuration
skipped-by-limit
```

These are acquisition outcomes mapped to the accepted coverage vocabulary, not
claim confidence or evidence authority. A 403/404 must not be silently
interpreted as “the mod has no documentation,” because moderation, visibility,
age, permissions, deletion, wrong identity, and true absence remain distinct
possibilities until the interface supports distinguishing them.

## 7. Source/entity/version acquisition contract proposal

Every Nexus acquisition unit should retain:

```text
source_id = nexus-mods
interface_tier: v3-openapi | legacy-v1 | first-party-graphql
interface_contract_identity
adapter_identity
request_operation
request_entity_identity
requested_field_group
authentication_mode (never credential value)
application_identity
retrieval_started_at
retrieval_completed_at
http_status
provider_error_class
rate_limit_observations
visibility_or_age_observations
response_etag?
response_last_modified?
resolved_entity_identities[]
source_revision_signals[]
permitted_payload_fingerprint?
retained_body_or_excerpt_ref?
retention_policy_identity
coverage_outcome
gap_reason?
```

Content-specific resolved identities should include:

- mod: game domain/game ID, game-scoped mod ID, global/composite ID where
  exposed, mod version, and coarse mod update time;
- requirement: requirement/definition/range/target IDs, referenced mod/file/
  version/DLC IDs, conditions/notes, and enclosing mod revision signals;
- changelog: mod or file-version identity, author version key, file-version ID
  where applicable, and payload fingerprint;
- file: persistent mod-file ID, exact mod-file-version ID, game-scoped file ID
  where exposed, version, category, upload time, and provider response
  fingerprint.

The contract must preserve these limits:

1. Retrieval time is always known; source revision is known only to the degree
   the interface exposes it.
2. `updated_timestamp`/`updatedAt` is coarse mod freshness, not proof of which
   field changed.
3. A version string is author data and is not unique revision identity.
4. A content hash proves equality to a retained permitted payload, not that
   the server's source has not changed since retrieval.
5. Server-side five-minute caching is not a source revision, client TTL, or
   permission rule.
6. A later clean refresh creates a new acquisition result. It does not mutate
   the source revision or claims consumed by an earlier analysis run.
7. Legacy/experimental/first-party-client contract tier is retained so a
   future contract change invalidates only dependent acquisition/extraction
   work.

## 8. Alternatives evaluated

| Alternative | Coverage | Stability/policy fit | Decision |
|---|---|---|---|
| v3 only | File identity/version/dependencies and short summaries, but no long descriptions, general changelog reads, articles, or posts | Best current formal contract; relevant reads are all Experimental | Insufficient for M1 documentation intelligence |
| Legacy v1 only | Best documented route for long descriptions, changelogs, and file descriptions | Officially linked and current client still uses it, but explicitly legacy and sparsely specified | Viable bounded source tier, not a durable single-interface assumption |
| First-party GraphQL only | Richest modeled field selection, including page-level requirements | Current first-party capability, but no independently versioned/stability-badged mod-content schema was found | Qualification candidate, not an unqualified production contract |
| Layered v3 identity/dependency plus legacy v1 documentation, with GraphQL only after technical qualification and separate supported-interface approval | Best honest coverage while preserving per-tier contract identity and gaps | v3/v1 have official published contracts; first-party GraphQL client evidence establishes technical capability but not by itself ADR-0005 eligibility; more adapter complexity | **Recommended** |
| Scrape or automate Nexus pages to fill articles/posts | Broad apparent coverage | Explicitly prohibited by ADR-0005 and unstable | Reject |
| Use undocumented GraphQL fields discovered through introspection/site traffic | Could expose more content | Undocumented/private endpoint discovery is prohibited; schema support unknown | Reject |
| Ask users to paste page text as a fallback | Manual but expensive; provenance/freshness weak | Not a supported-API acquisition and not an automatic rights/policy workaround | Do not present as Nexus adapter coverage |
| Omit unsupported Nexus surfaces and rely on approved non-Nexus/installed documentation | Partial but honest | Clean boundary; RQ-010 governs each source | Required contingency/complement |

## 9. Contrary evidence, uncertainty, and limitations

### 9.1 Contrary evidence

- Nexus continues active v3 development and maintains a public client with
  rich v1/GraphQL reads. It would be too strong to call descriptions,
  requirements, changelogs, or files technically unavailable.
- Conversely, official-client code presence is weaker than a stable published
  contract. It would be too strong to call every modeled GraphQL field a
  production-supported public API.
- v3 file-version dependencies are structured and current, but they represent
  only one requirements mechanism. Treating them as full page-level or prose
  requirements would lose relevant author intent.
- A mod-level update timestamp is useful refresh evidence, but it does not
  identify which content changed. Treating all source content as revisionless
  would also discard useful coarse invalidation data.

### 9.2 Material uncertainty and experiment gaps

1. No authenticated payload was retrieved, so current v1/GraphQL field
   presence, nullability, HTML/BBCode form, and schema agreement remain
   unverified.
2. No authenticated request established the exact per-interface quota
   accounting, remaining/reset headers, retry behavior, ETag, Last-Modified,
   caching, cancellation, revocation, or malformed-error body.
3. No current official source found during this bounded investigation explains
   whether the independent v2 GraphQL mod-content schema is a supported public
   contract, its version, or its deprecation promise.
4. The v1 Swagger is from 2019 and supplies little response-schema detail even
   though it remains officially linked and used by the current client.
5. The optional v1 requirements field and GraphQL `modRequirements` require
   live comparison before Infinium can claim page-level requirements coverage.
6. Age-restricted, moderated, deleted, unpublished, private, quarantined, and
   otherwise visibility-limited mod behavior was not qualified.
7. No supported article, post/comment, sticky/author-post, or mod bug-report
   read interface was found. Absence from the inspected contracts is strong
   capability evidence, but Nexus may later publish a new supported interface.
8. Nexus's response to the owner's policy clarification request is not
   available in this investigation. Any response remains an ADR-0005 review
   trigger.

## 10. Recommendation

Confidence:

- **High** that v3 currently lacks the desired long-form documentation
  coverage and that articles/posts/bug reports have no supported read fallback;
- **High** that legacy v1 remains the documented description/changelog/file
  candidate and that the current first-party client exposes richer GraphQL
  requirements capability;
- **Medium** that v1 and first-party GraphQL will return the modeled fields
  reliably for Infinium until the bounded authenticated experiment is run; and
- **Low** that any current non-stable tier is a durable production contract
  without ongoing reverification or Nexus confirmation.

Recommended M0 disposition:

1. Model Nexus as one source with separately versioned interface capabilities,
   not one generic “Nexus API.”
2. Use current v3 operations for the exact file/file-version/dependency
   identities they expose after authenticated qualification.
3. Qualify legacy v1 mod details and changelogs as the initial long-description
   and changelog read candidate. Keep the adapter explicitly legacy,
   independently disableable, and policy/contract-expiring.
4. Treat v1/GraphQL page-level requirements as provisional until one bounded
   authenticated comparison proves current payload shape, identity,
   visibility, and revision signals. GraphQL also requires separate evidence
   that Nexus documents, supports, or expressly approves the interface for
   this operation; successful authentication or a live payload would not
   establish that policy/contract fact. Prefer the least unstable supported
   route that provides the required fields; do not infer that choice now.
5. Do not claim articles, posts/comments, sticky/author posts, or mod bug
   reports as Nexus coverage. Record per-mod/per-content gaps and allow RQ-010
   sources or installed documentation to complement them under their own
   policies.
6. Implement the acquisition identity/freshness contract in §7. Retain
   response validators if observed, but always preserve retrieval time,
   interface identity, entity/revision signals, and permitted content
   fingerprint.
7. Run one later bounded authenticated qualification matrix using an
   owner-supplied personal development credential through an approved
   non-printing handoff:

   - one visible Skyrim SE mod with a long description, page requirements,
     changelog, and several file versions;
   - one valid mod with absent optional content;
   - one invalid/nonexistent ID;
   - one visibility-limited outcome if the account and API lawfully expose it;
   - v3 and v1; include only the minimal first-party GraphQL queries needed for
     comparison if separate supported-interface evidence or express approval
     has first satisfied ADR-0005;
   - exact status, content type, schema, entity IDs, timestamps, validators,
     rate headers, and sanitized payload fingerprints;
   - no mod-file download and no retained raw content in the repository.

8. Stop rather than broaden access if the credential is unavailable, the
   GraphQL capability is not clearly approved for third-party use, or a source
   surface is unsupported.

## 11. Exact downstream work enabled

This report proposes, but does not apply:

1. **Source-registry update:** add three Nexus interface tiers, exact S1–S4
   identities, the content matrix in §5, coarse-versus-exact revision
   semantics, auth/rate observations, and explicit article/post/bug-report
   gaps.
2. **RQ-008 status:** `Researched; contract inventory complete, bounded
   authenticated content/revision qualification pending before Gate D can
   claim Nexus acquisition coverage.` If M1 deliberately excludes live Nexus
   acquisition, record that narrower source gap instead of implying the
   experiment passed.
3. **RQ-011 input:** claim extraction must accept a source unit whose exact
   content type, interface tier, entity/revision identity, conditions,
   supporting excerpt, and gap state are explicit.
4. **RQ-013 input:** acquisition records must preserve interface-contract
   identity, coarse revision versus content fingerprint, source payload/
   excerpt/claim separation, and refresh lineage.
5. **RQ-032 input:** sanitize BBCode/HTML, preserve URL/navigation allowlists,
   isolate API credentials, distinguish visibility/age/auth failures, and
   enforce a policy/contract-expiry circuit breaker.
6. **Evaluation input:**

   - `EVAL-0068`: supported v3/v1 source, unsupported article/post surfaces,
     expired/changed interface contract, and no page fallback;
   - `EVAL-0010`–`EVAL-0012`: exact excerpt resolution, conditions/version
     applicability, contradiction, and abstention over a permitted retained
     source unit;
   - `EVAL-0033`/`EVAL-0034`: hostile BBCode/HTML/instruction content remains
     untrusted and cannot grant authority;
   - `EVAL-0064`: cached permitted evidence works offline while live-only and
     unsupported content are explicit;
   - `EVAL-0083`: acquisition interface/spec/client identity and revision
     provenance survive end to end.

No new ADR is warranted from this inventory alone. A later documentation
source/provider/transmission mechanism ADR may select the adapter enforcement
boundary after RQ-010 through RQ-013 and the authenticated qualification.

## 12. Requirements-and-evidence traceability

| Requirement/decision | Evidence | Result/downstream use |
|---|---|---|
| `DOC-001`, `COVER-001`–`COVER-003` | S1–S4; §5 | Coverage is content- and interface-specific; unsupported articles/posts remain denominated gaps. |
| `DOC-002`, `DOC-011`, ADR-0002 | S1–S4; §7 | Acquisition retains source/interface/entity/revision identity and a later analysis-application link. |
| `DOC-005`, `DOC-008`, `DOC-009` | Mod/file timestamps, IDs, version keys, and missing field revisions in S1–S4 | Preserve coarse revision signals plus permitted content fingerprints; refresh creates a new acquisition revision. |
| `DOC-006`, ADR-0005 | S1/S2 contract limits and absence matrix | Use only documented supported/officially maintained interfaces; never fill a gap through pages or private endpoints. |
| `SEC-001`, `AI-003` | Description/HTML/BBCode fields in S3/S4 | Treat all content as untrusted, minimize provider context, and add no operation authority. |
| `SEC-002`, SCOPE-004 | S1, S2, S5; unauthenticated C4 | Require user-owned revocable auth through a secure adapter and manual initiation; no credential was available or exposed here. |
| `SCAN-003`–`SCAN-006`, `OPS-001` | S4/S6, C4, §6 | Expose quota/auth/error capability gaps and isolate failed/unsupported content units. |
| `EVID-002`–`EVID-003`, ADR-0001 | S1–S4; §5 | Author-source evidence remains distinct from local state; interface capability never becomes claim authority by itself. |
| `SNAP-006`, `OPS-002`–`OPS-003`, RQ-031 | Prior Wave A reports; §7 | Retained permitted payload/excerpt can support audit/replay; missing bodies or unsupported surfaces remain explicit gaps and are not redistribution permission. |
| RQ-008 / Gate D | S1–S6 and checks C1–C4 | Supported-surface inventory is answered; authenticated payload/revision qualification remains an explicit M0 gap. |

## 13. Semantic self-review checklist

- Current v3, legacy v1, and first-party GraphQL capability are not collapsed
  into one stability class.
- A first-party client implementation is not presented as a separately
  published GraphQL schema guarantee.
- File-version dependencies are not treated as every page/prose requirement.
- Mod-level update time is not represented as an exact description or
  requirements revision.
- Articles, posts/comments, sticky/author posts, and mod bug reports remain
  unsupported rather than silently replaced by page access.
- No authenticated behavior, age filtering, quota header, cache validator, or
  live schema agreement is claimed as tested.
- No API capability is represented as policy, retention, transmission, or
  redistribution permission.
- The recommendation narrows to the M0 source contract and qualification gap;
  it selects no production stack or persistence mechanism.
