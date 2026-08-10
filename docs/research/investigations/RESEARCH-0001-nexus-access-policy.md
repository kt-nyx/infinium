# RESEARCH-0001: Nexus access and evidence-handling policy

Status: Completed
Disposition: operational disposition accepted and amended
Date: 2026-07-25

Last reviewed: 2026-07-28

Researcher: Codex agent

Subsequent disposition: ADR-0012 supersedes ADR-0005's narrower
documented-supported-interface constraint, accepts Nexus-provided read APIs
including v2 GraphQL during development, and selects latest-capable
v3/v2/v1 routing. The bounded purpose, no-page/no-bypass/no-rehost/no-training,
registration, and reversal constraints derived from this report remain
operative.

Primary RQ: RQ-009

M0 wave: A — Policy and evidence-handling guardrails

Decision enabled: Source and distribution policy for Nexus documentation
acquisition; prerequisite constraints for RQ-008, RQ-013, RQ-031, RQ-032, and
Wave D

## Executive conclusion

Infinium must not crawl or scrape Nexus Mods pages. More importantly, the
current Nexus Mods Terms of Service, updated 2026-05-12, applies expressly to
Nexus APIs and prohibits text/data mining and automated analytical techniques
over Nexus site or service data. That wording reaches Infinium's intended
automated claim extraction and likely its LLM analysis, even when the input
arrives through an API rather than HTML.

An older API Acceptable Use Policy, updated 2020-12-01, simultaneously invites
third-party API applications, tolerates personal API keys for testing or
personal use, and describes public-application registration. The current v3
API documentation also proves that supported API integration continues to
exist. Neither source says, however, that API access under the AUP is an
exception to the newer Terms' automated-analysis prohibition. API capability
therefore does not establish policy permission for Infinium's use.

The investigation's conservative recommendation was to block that workflow
pending written clarification or an express exemption. After reviewing the
conflicting provisions, the complementary local nature of the product, and
section 10's personal/mod-enhancement purpose, the project owner accepted
[ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md).
Infinium will therefore proceed with bounded, user-initiated retrieval and
value-added analysis through supported Nexus APIs while clarification is
requested. The ambiguity remains a recorded risk rather than an operational
block. HTML scraping, unsupported interfaces, bulk collection/rehosting, model
training, and unapproved source redistribution remain prohibited.

## Question and accepted constraints

### Primary question

Which Nexus policies constrain scraping, caching, redistribution, and public
application registration?

### Linked accepted requirements

| Requirement | Relevance to this investigation |
|---|---|
| `SCOPE-004` | Nexus acquisition must remain manually initiated through M4. |
| `SEC-001` | Nexus HTML, BBCode, API data, comments, and other user-generated content remain untrusted data. |
| `SEC-002` | API credentials require secure entry/storage, revocation, and exclusion from logs, prompts, and exports. |
| `SEC-004` | Externally shareable diagnostics require privacy and source-policy review. |
| `EVID-002` | Source, version, retrieval time, and applicable evidence must remain attributable. |
| `EVID-003` | Author text may establish stated intent, but access permission and local applicability are separate questions. |
| `DOC-001` | Full enabled-mod documentation coverage cannot silently assume that every Nexus page is obtainable. |
| `DOC-002` | Independent acquisition is allowed only when its source method is permitted. |
| `DOC-006` | Prohibited scraping must not be used. |
| `DOC-008` | Exact-passage and full-source retention are conditional on source policy. |
| `DOC-009` | Nexus policy and source freshness must be versioned and explicitly refreshed. |
| `DOC-011` | Each allowed acquisition must retain request, source revision, adapter, coverage, audit, and replay provenance. |
| `AI-003` | Nexus credentials and unnecessary context may not enter model context. |
| `OPS-001` | A Nexus adapter must declare live-network, cached-source, authentication, and offline behavior. |
| `OPS-002` | Indefinite product history does not override source-specific retention limits. |
| `OPS-003` | Private retention cannot be treated as permission to redistribute Nexus content in exports. |

### Accepted ADR constraints

| ADR | Constraint |
|---|---|
| [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md) | An applicable author statement can be authoritative for stated intent, but an API, model, or policy inference cannot become local-state authority. |
| [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md) | Nexus evidence must retain acquisition-run/source/version provenance and gain a separate application link when used. |
| [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md) | Acquisition may write only product-owned data and may not mutate the modding setup. |
| [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md) | Documentation acquisition remains manually initiated and bounded to the initial product scope. |
| [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md) | Supported, bounded, user-initiated API retrieval and local diagnostic transformation may proceed under the owner's accepted interpretation; scraping, unsupported interfaces, bulk/rehost behavior, model training, and public source redistribution remain excluded. |

The investigation also follows the current
[integration boundary](../../architecture/integrations.md), which requires
Nexus adapters to follow API and scraping policies and report unavailable
content as coverage gaps, and the
[security/privacy boundary](../../architecture/security-and-privacy.md), which
keeps retrieved content untrusted and separates private retention from
external sharing.

## Scope and non-scope

### In scope

- Current official Nexus policies governing website and API access.
- Supported authentication and public-application registration posture.
- Published rate-limit behavior.
- The difference between API capability and permission to analyze, cache, or
  redistribute returned data.
- A bounded capability check for descriptions, requirements, articles,
  changelogs, files, posts/comments, and bug reports.
- Safe constraints for later Wave D research and M4 registration planning.

### Non-scope

- Legal advice or a conclusion about the enforceability of any contract term.
- Negotiating an exemption or registering an application.
- Authenticated, paid, premium, or age-restricted access.
- Calling a mod-content endpoint or downloading any mod file.
- Scraping public or authenticated Nexus pages.
- A complete endpoint contract for RQ-008.
- Choosing an implementation architecture, credential store, cache database,
  or export format.
- Applying proposed changes to the RQ registry, source registry, ADRs,
  evaluation catalog, or milestone plan.

## Preflight and access authorization

| Item | Decision |
|---|---|
| Local private data | Not required or accessed. |
| Network | Required only for public official policy/developer documentation and the official public GitHub repository. |
| Authenticated APIs | Not authorized and not used. |
| Paid access | Not authorized and not used. |
| External tools | PowerShell 7.6.3 was used only for public-document HTTP retrieval and hashing. |
| Expected side effects | Ordinary remote access logs; no authenticated account state, local modding state, or external service state changed. |
| Retained artifacts | This synthesized Markdown report only; no fetched policy page, API response, mod text, user data, or API key was retained. |
| Stopping condition | Stop before any mod-content retrieval, automated extraction, or authenticated experiment because current permission is ambiguous. |

## Sources and exact versions

All sources were retrieved or rechecked on **2026-07-25**.

| ID | Official/primary source | Version or update identity | Authority and claim-level relevance |
|---|---|---|---|
| S1 | [Nexus Mods Terms of Service](https://help.nexusmods.com/article/18-terms-of-service) | Last updated 2026-05-12 | Current contract-level source. Section 1 includes subdomains, applications, and APIs. Section 10 constrains copying, commercial use, and recording data beyond normal use. Section 11 prohibits text/data mining and web scraping. Sections 16, 17, and 23 place age-content filtering responsibility on API users. Sections 24 and 27 constrain downstream use and linking. |
| S2 | [Nexus Mods API Acceptable Use Policy](https://help.nexusmods.com/article/114-api-acceptable-use-policy) | Last updated 2020-12-01 | API-specific operational policy. Covers personal keys, public-app registration, identifying headers, user initiation, server-side key storage, mass rehosting, and enforcement discretion. |
| S3 | [Nexus public-API rate-limit help](https://help.nexusmods.com/article/105-i-have-reached-a-daily-or-hourly-limit-api-requests-have-been-consumed-rate-limit-exceeded-what-does-this-mean) | Last updated 2026-06-03 | Publishes 20,000 requests per 24-hour period and, once that is reached, 500 requests per hour; reset behavior and remaining-request headers. |
| S4 | [Official Nexus Mods GitHub organization](https://github.com/Nexus-Mods) | Inspected 2026-07-25 | Current developer-navigation source. Calls v3 the actively developed API and v1 legacy. |
| S5 | [Nexus Mods API v3 documentation](https://api-docs.nexusmods.com/) | ReDoc shell inspected 2026-07-25 | Current official developer documentation entry point. It loads S6. |
| S6 | [Nexus Mods API v3 OpenAPI specification](https://api.nexusmods.com/openapi.yaml) | OpenAPI 3.0.3; API version 3.0.0; HTTP `Last-Modified: Thu, 23 Jul 2026 15:06:41 GMT`; SHA-256 `7ff2bdbded673ca33bed3e6e835d34dbbf42ee29b7b8becd2581556f5b601b07` | Current API capability, stability, security-scheme, and endpoint evidence. It is not a grant of analysis, retention, or redistribution permission. |
| S7 | [Officially linked v1 Swagger specification](https://api.swaggerhub.com/apis/NexusMods/nexus-mods_public_api_params_in_form_data/1.0/swagger.json) | Swagger 2.0; API version 1.0; HTTP `Last-Modified: Mon, 27 May 2019 10:12:20 GMT`; SHA-256 `7730560e9e0abe299a602e3a563cd53230cabd9853ceb6352b08f9188d9a4c2a` | Legacy v1 capability evidence for mod details, changelogs, files, and downloads. Stale schema age and "legacy" labeling limit its architectural authority. |
| S8 | [Official `node-nexus-api` repository](https://github.com/Nexus-Mods/node-nexus-api) | `master` commit [`ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0`](https://github.com/Nexus-Mods/node-nexus-api/commit/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0), committed 2026-07-23 | Maintained first-party client evidence for available REST/GraphQL methods and authentication behavior. Client code does not create policy permission. |
| S9 | [Pinned first-party client type definitions](https://github.com/Nexus-Mods/node-nexus-api/blob/ac96897d5ea4eb1288dae4ed43a01ce9cdc075c0/src/types.ts) | Same commit as S8; retrieved file SHA-256 `1cdc87c547be6fffa7d65b47cd342b4e3f11f8bcba53cc793f9d7cdd3331513b` | Capability evidence for description, summary, requirements, files, and limited collection bug-report structures. It does not establish that every exposed client method is a stable public contract. |

### Source precedence and applicability

S1 is materially newer than S2 and expressly includes APIs. S2 is more
specific to application integration but predates S1 by more than five years.
Neither document defines precedence between them or says that conforming API
use is exempt from S1 section 11. S4–S9 establish that the API exists and what
some interfaces can technically return; they do not amend S1 or S2.

No historical Vortex, Mod Organizer 2, Wabbajack, or other third-party behavior
was treated as permission for Infinium.

## Documentation checks and artifacts

### Check 1 — Current policy inspection

Procedure:

1. Open S1, S2, and S3 as public documentation.
2. Record their displayed update dates.
3. Compare the scope and prohibited-operation wording.
4. Do not follow links into account settings or invoke API access.

Observed:

- S1 section 1 applies the Terms to the Nexus website, listed subdomains,
  Nexus applications, and APIs.
- S1 section 10 limits site use to personal/mod-enhancement purposes, requires
  attribution, prohibits unlicensed commercial use, and prohibits recording
  service/API data in a manner significantly exceeding normal and expected
  use without express permission.
- S1 section 11 prohibits conducting, facilitating, authorizing, or permitting
  text/data mining or web scraping in relation to the site or its services for
  any purpose. Its examples include bots/scrapers that obtain, copy, monitor,
  or republish data and automated techniques that analyze digital text/data to
  generate information or develop, train, fine-tune, or validate AI systems.
  It also separately rejects using Nexus data to develop, train, fine-tune, or
  validate an AI system or model. Nexus directs exemption requests to
  `support@nexusmods.com`.
- S2 allows an open API integration model, but its unacceptable-use examples
  prohibit mass fetching to rehost data, server-side storage/use of user keys
  without user action, personal keys in a public application, and blank or
  impersonated request metadata.
- S3 publishes quota behavior and says clients must wait when rate-limited.

No source stated a client-side cache duration, a right to retain exact response
bodies indefinitely, or a general right to redistribute returned text.

### Check 2 — Current API-document identity

Reproducible read-only procedure:

```powershell
$html = Invoke-WebRequest -Uri 'https://api-docs.nexusmods.com/'
$spec = Invoke-WebRequest -Uri 'https://api.nexusmods.com/openapi.yaml'
$spec.StatusCode
$spec.Headers['Last-Modified']
```

Observed:

- The documentation shell returned HTTP 200 and initialized ReDoc from S6.
- S6 returned HTTP 200, identified OpenAPI 3.0.3 / Nexus Mods API 3.0.0, and
  declared `https://api.nexusmods.com/v3` as the production server.
- S6 described stable, beta, and experimental endpoint levels and minimum
  deprecation periods of 90 days for stable endpoints and 10 days for beta
  endpoints.
- S6 declared API-key and bearer-JWT security schemes. Some explicitly public
  feed endpoints override authentication with an empty security requirement;
  most operations inherit the global schemes.
- The current v3 "get mod" endpoint was marked experimental and exposed only
  limited mod identity/name fields in its documented schema. This is not a
  full documentation-content source.

### Check 3 — Legacy and maintained-client capability

Reproducible read-only procedure:

```powershell
$v1 = Invoke-WebRequest -Uri `
  'https://api.swaggerhub.com/apis/NexusMods/nexus-mods_public_api_params_in_form_data/1.0/swagger.json'
$repo = Invoke-RestMethod -Uri `
  'https://api.github.com/repos/Nexus-Mods/node-nexus-api/commits/master'
$repo.sha
```

Observed:

- S7 still described legacy v1 `GET` endpoints for mod details, changelogs,
  file lists, individual file metadata, and download-link generation.
- The legacy mod-details operation documented server caching for five minutes.
  That is server behavior, not permission for an Infinium client to retain the
  response.
- The pinned first-party client still modeled mod descriptions/summaries,
  changelogs, files, and requirements and contained REST/GraphQL access
  methods.
- No inspected current official contract exposed a general mod-article,
  ordinary post/comment, or mod bug-report read API.

### Negative and boundary controls

- No API key, account cookie, bearer token, SSO flow, or paid membership was
  used.
- No mod-content API endpoint was called.
- No Nexus page was crawled, enumerated, or scraped.
- No Cloudflare, login, age, premium, robots, or rate-limit control was tested
  or bypassed.
- No live quota experiment was performed because published policy was
  sufficient and a quota test would add load without resolving the policy
  conflict.
- No fetched page/specification was written to the repository or retained as a
  raw artifact.

### Artifact manifest

| Artifact | Retention | Redistribution treatment |
|---|---|---|
| This investigation | Tracked proposed research document | May be shared as project-authored synthesis with direct source URLs; it contains no Nexus mod-page body or API response. |
| Policy pages | Not retained | URL, update date, and paraphrased policy claims only. |
| v3 OpenAPI / v1 Swagger / client types | Not retained | URL, version/commit, selected capability observations, and SHA-256 only. |
| Mod descriptions, requirements, articles, changelogs, file records, posts/comments, bug reports | Not acquired | No retention or redistribution decision can be inferred. |
| Credentials or private user data | Not accessed | Not applicable. |

## Findings

### Confirmed policy facts

1. **HTML crawling and scraping are prohibited.** S1 section 11 is explicit,
   applies to services including APIs, and provides an exemption contact.
   `robots.txt`, technical accessibility, search-engine indexing, and a public
   page are not alternate permission sources.

2. **Automated claim extraction is within the current risk boundary.** S1
   section 11 expressly includes automated analysis of text/data to generate
   information. Infinium's deterministic extraction, classification, and LLM
   summarization are automated analysis even when they do not train a model.
   The separate AI-development/training prohibition is additional, not a safe
   harbor for inference or extraction.

3. **Supported API integration still exists.** S4–S6 identify v3 as current
   and actively developed. S2 still supplies an application policy. This is
   strong contrary evidence against interpreting S1 as intending to abolish
   every API application, but it does not define which automated processing is
   permitted.

4. **Personal API keys have a bounded role.** S2 tolerates them for personal
   use and testing. It does not endorse their use by a public application.

5. **Public applications require contact and review.** Once an application is
   intended for a wider end-user audience beyond testing, S2 requires contact
   with Nexus support. The documented process is:

   1. provide a testing build usable with a personal API key;
   2. make requested changes and provide an application name, short
      description, and suitable logo;
   3. receive an application slug for SSO.

   Registration is not endorsement. Open-source licensing is strongly
   encouraged, not stated as universally mandatory, and Nexus reserves
   registration discretion based on application purpose, data handling, and
   policy compliance.

6. **Requests must identify the real application.** S2 asks for accurate,
   current `Application-Name` and `Application-Version` headers. Blank or
   impersonated metadata is unacceptable.

7. **User authorization must remain local and user initiated.** S2 prohibits
   storing user API keys on an application's server and/or using them without
   user-initiated action. This aligns with Infinium's manual-initiation and
   user-owned-credential requirements but does not resolve S1 section 11.

8. **Rate limits are an operational ceiling, not an acquisition budget.** S3
   publishes 20,000 requests per 24-hour period and a 500-per-hour state once
   the daily limit is reached, with resets at 00:00 GMT and each hour.
   Remaining values are returned in response headers. The documentation does
   not say that every v3/GraphQL operation has identical accounting or that
   staying below the quota makes an otherwise prohibited use acceptable.

9. **API consumers have an age-content obligation.** S1 says third parties
   using the APIs are responsible for filtering returned age-restricted
   content. The inspected sources do not define a complete third-party
   implementation contract or an age-verification signal suitable for
   Infinium.

10. **Mass collection and rehosting are prohibited.** S2 explicitly identifies
    en-masse fetching intended to rehost Nexus information as unacceptable.
    S1 section 10 separately restricts excessive recording and unlicensed
    commercial use.

11. **Nexus does not grant a blanket downstream redistribution licence.** S1
    says uploaders retain ownership, and user rights are subject to the licence
    terms chosen for the uploaded content. Nexus's own broad licence from an
    uploader is not a general licence to Infinium. Exact mod text, comments,
    media, and files therefore cannot be included in shareable exports merely
    because Nexus can display or return them.

12. **Policy is mutable.** S1 directs users to check current terms on each use.
    A Nexus adapter needs a dated policy-verification field and a stop behavior
    when its approved access assumptions have expired or materially changed.

### Interpretation for Infinium

- The current API AUP is necessary but not sufficient permission.
- A manually started scan does not remove the section 11 issue; manual
  initiation governs request authority, while extraction remains automated.
- A local-only personal architecture does not remove the section 11 issue.
- Sending Nexus text to a third-party LLM increases the unresolved use and
  redistribution/processing questions; it is not permitted merely because the
  user supplied both API and LLM credentials.
- Private retention, replayability, and external redistribution are separate.
  Nothing found supports using one as evidence for another.
- Until clarified, a Nexus coverage gap is the correct product behavior.
  Falling back to HTML, a browser session, search-result caches, or a different
  user key would evade the policy decision rather than resolve it.

## Permitted/prohibited/unknown operation matrix

`Permitted` below means supported by the inspected policy for the narrow stated
operation, not a general legal opinion. `Accepted project interpretation`
identifies an operation authorized by ADR-0005 despite unresolved policy
wording. `Unknown` remains an operational stop when neither the sources nor
ADR-0005 define the boundary.

| Operation | Status | Evidence and Wave A rule |
|---|---|---|
| Read official public policy/API documentation to establish guardrails | Permitted | This investigation's bounded documentation review. Do not turn it into mod-content collection. |
| Ordinary manual personal viewing of content the user's account is authorized to see | Permitted within stated personal-use/account limits | S1 sections 9–10 and age restrictions. This is not permission for automated extraction or redistribution. |
| Crawl/scrape Nexus HTML, including public mod pages | Prohibited | S1 section 11. Do not use browser automation, headless browsing, search-result reconstruction, or undocumented HTML calls as an adapter. |
| Bypass login, Cloudflare, age, premium, API, or other controls | Prohibited | S1 account/security/access terms and the M0 stopping rules. |
| Use a personal API key for private personal use or a testing build | Permitted for ADR-0005-scoped use | S2 and ADR-0005. Calls remain user initiated, correctly identified, bounded to relevant mods, and within normal/rate-limited use. |
| Use a personal API key in a public application | Prohibited | S2. Register the public app instead. |
| Store user API keys on an application-operated server | Prohibited | S2. Also conflicts with Infinium's accepted personal/local credential boundary. |
| Make credentialed calls without a user-initiated action | Prohibited | S2 and `SCOPE-004`. Background retries after revocation or outside an initiated operation are not allowed. |
| Send blank or impersonated application headers | Prohibited | S2. Use the real application name/version and later the registered identity. |
| Fetch en masse to mirror/rehost Nexus data | Prohibited | S2 unacceptable-use example and S1 section 10. |
| Exceed normal/expected recording or published rate limits | Prohibited | S1 section 10 and S3. Rate-limit headroom is not permission for bulk acquisition. |
| Run deterministic extraction/classification over supported Nexus API data | Accepted project interpretation | ADR-0005 treats bounded diagnostic transformation for the user's relevant mods as personal/mod-enhancement and third-party application use. The contrary section 11 reading remains a recorded risk and Nexus confirmation is still requested. |
| Send minimized supported-API evidence to a user-selected LLM for claim extraction or investigation | Accepted project interpretation with provider controls | ADR-0005 permits inference, not training/fine-tuning/validation. Transmission must be disclosed, minimized, credential-free, and independently governed by the selected provider/source-retention policy. |
| Train, fine-tune, or validate an AI system/model on Nexus data | Prohibited absent express exemption | S1 section 11. Out of Infinium scope regardless. |
| Cache exact supported Nexus API responses locally for private provenance/replay and useful dependent analysis | Accepted project interpretation, subject to storage research | ADR-0005 permits necessary local product-owned retention with deletion/refresh controls. The accepted RQ-031 disposition requires permitted material to remain available through useful extraction, analysis, case/finding synthesis, prose, provenance, and audit; ADR-0015 selects the storage mechanism, while measured defaults remain implementation work. This does not permit page-text scraping or public corpus storage. |
| Retain short exact cited passages privately | Accepted project interpretation, subject to minimization | ADR-0005 permits private provenance/audit evidence. Exact passages remain separate from externally shareable content and require source/revision attribution and deletion handling. |
| Retain source URLs, retrieval timestamps, response status, policy version, and content fingerprints without source body | Conditionally permissible operational metadata | These minimize copied content, but they do not permit deriving the fingerprint through a prohibited acquisition. Record them only for an otherwise allowed request. |
| Redistribute mod descriptions, articles, changelogs, comments, bug reports, media, or files | Prohibited unless the specific rights and Nexus policy permit it | S1 sections 10, 22, and 24 do not grant Infinium a blanket licence. Shareable exports must omit raw content by default. |
| Redistribute derived structured claims or short quotations | Conditional | ADR-0005 permits derived findings in user-created exports but not raw-source republication. Exact quotation size, attribution, linking, and source-specific rights remain export-policy inputs and questions for Nexus. |
| Deep-link from an export/application to a mod page | Accepted project interpretation, confirmation requested | Canonical attribution links support rather than replace Nexus. Preserve the unresolved section 27 wording in the support request and revise if Nexus answers otherwise. |
| Register a testing build for public use | Permitted/required transition | Follow S2's support-review process before leaving testing. Registration is not guaranteed or endorsement. |
| Display API-returned age-restricted material without an accepted filter/age design | Prohibited for a public integration | S1 makes API consumers responsible for filtering; the exact supported mechanism is unresolved. |

## Content-surface policy and capability matrix

This is a policy-oriented boundary check, not the complete RQ-008 endpoint
survey. `Capability found` means a current official document or maintained
first-party client identifies a mechanism. It does not mean Infinium may use
that mechanism for automated analysis.

| Content surface | Current supported-capability evidence | Policy result for Infinium | Coverage consequence now |
|---|---|---|---|
| Mod description/summary | Legacy v1 mod-details endpoint and S8/S9 client types include description/summary. Current documented v3 `get mod` is experimental and exposes only limited identity/name fields. | ADR-0005 permits bounded analysis only through a supported interface; raw external redistribution remains restricted. | RQ-008 may perform bounded live capability experiments and must identify the supported successor contract. |
| Requirements | S8/S9 include GraphQL requirements structures; v3 documents file-version dependency endpoints, which are not automatically equivalent to every page-level requirement. | ADR-0005 permits supported-API retrieval and analysis; code presence alone does not prove public support. | RQ-008 must distinguish supported contracts from first-party/private implementation details. |
| Articles | No supported general article-read interface was found in S6, S7, or S8/S9. | HTML scraping is prohibited. | Unsupported/unknown source; do not make core coverage depend on it. |
| Changelogs | Legacy v1 documents reading all mod changelogs. Current v3 inspected documentation contains a changelog write operation but no equivalent general read operation. | ADR-0005 applies only if RQ-008 finds a supported read interface; no raw export rights inferred. | Treat as unavailable unless RQ-008 verifies a supported current interface. |
| File metadata and versions | Legacy v1 and current v3 document file/version metadata; v3 also exposes dependency operations. | ADR-0005 permits bounded supported-API use subject to authentication, quota, age, purpose, and retention controls. | Available candidate input for RQ-008 experiments. |
| Mod file download | Legacy v1 documents download-link generation; account/premium/download terms vary. No download was attempted. | Not required for RQ-009, and API availability is not redistribution permission. | Use locally installed/retained user files for local analysis; research downloads separately only if a later plan requires them. |
| Author posts/stickies | No supported general read interface was found. | Page scraping is prohibited. | Unsupported source/coverage gap. |
| Ordinary comments/posts | No supported general read interface was found. | Page scraping is prohibited; comments are investigative rather than authoritative under `DOC-006`. | Exclude from core coverage. |
| Mod bug reports | No supported general mod bug-report read interface was found. S9 contains collection bug-report types, which are not evidence of a mod-page bug API. | Page scraping is prohibited; bug reports are investigative evidence. | Exclude from core coverage unless Nexus later documents and permits an interface. |

## Alternatives evaluated

| Alternative | Policy fit | Coverage | Decision |
|---|---|---|---|
| Scrape public Nexus pages | Directly conflicts with current S1 section 11. | Potentially broad but unstable. | Reject. |
| Browser automation using the user's logged-in session | Still automated access/analysis; also expands credential, age, navigation, and privileged-content risk. | Broad only by evading supported-interface gaps. | Reject. |
| Supported API plus automated/LLM extraction under a bounded mod-enhancement interpretation | Technically plausible and consistent with section 10, the API AUP, and third-party API practice, but not expressly reconciled with section 11. | Best potential primary-source coverage. | **Selected by ADR-0005**, with explicit restrictions, reverification, and reversal triggers. |
| Supported API used only for identifiers/version metadata, with no text analysis | Lower copied-content exposure, but still requires clarity on whether Infinium's automated processing is allowed and which fields are within the approved purpose. | Insufficient alone for documentation intelligence. | Candidate narrower fallback only after Nexus confirms it. |
| Manual user copy/paste of Nexus passages into Infinium | Avoids Infinium page acquisition, but automated analysis of Nexus service data may still be within section 11; provenance, retention, and redistribution remain unresolved. | Labor-intensive and incomplete. | Not a policy workaround. May be reconsidered only after clarification. |
| User opens Nexus pages manually; Infinium stores only source links/notes authored by the user | Avoids automated source acquisition and raw-source caching. The current Terms' deep-link scope and derived-use boundary still deserve clarification. | Lead-only/manual workflow; cannot satisfy `DOC-001`. | Possible non-acquisition UX, not a substitute for a permitted adapter. |
| Use author-maintained non-Nexus repositories/sites under their own policies | Does not rely on Nexus permission. | Partial; many mods lack equivalent sources. | Advance through RQ-010 and show Nexus-specific gaps. |
| Omit Nexus acquisition and use bundled/local documentation plus LOOT | Cleanest policy posture if the accepted operating assumption is reversed. | Materially incomplete but honest. | Contingency only if ADR-0005 is reversed, registration is restricted, or supported coverage proves unavailable. Pending clarification alone does not activate it. |
| Seek written Nexus clarification/exemption and later register the application | Uses the process named by S1/S2 and can address the exact product/data flow. | Could unlock supported coverage without evasion. | Recommended. |

## Contrary evidence, uncertainty, and limitations

### Material contrary evidence

- S2 says Nexus provides an open API to facilitate third-party applications.
- S4–S6 show active 2026 API development.
- S8 is a maintained first-party client whose normal operation necessarily
  processes API data.

Those facts make a total ban on all software processing of API responses an
unlikely practical reading. They do not establish an exception for Infinium's
bulk documentation acquisition, semantic claim extraction, persistent evidence
store, LLM processing, or public exports. The policy documents leave that
boundary unstated.

### Unresolved questions

1. Does compliance with S2 create an exception to S1 section 11 for supported
   API operations?
2. Does Nexus classify local deterministic claim extraction as prohibited
   text/data mining when it analyzes only mods installed by the user?
3. May an API response or exact cited passage be cached privately, for how
   long, and under what deletion/freshness conditions?
4. May a local application send a minimal excerpt to a user-selected LLM for
   inference (not training), and which provider/retention constraints apply?
5. Which derived facts, structured claims, quotations, source URLs, and
   fingerprints may appear in private reports versus externally shareable
   exports?
6. Are canonical deep links to individual mods/articles/files allowed in
   application UI and exported citations?
7. Which current API, GraphQL, or other interface is an approved public
   contract for descriptions, requirements, articles, changelogs, files,
   author posts, comments, and bug reports?
8. Which v3/GraphQL operations share the published v1-style quota, and what
   response headers/retry behavior are guaranteed?
9. How must a third-party desktop application filter age-restricted API
   content, and what verified user/account signal may it rely on?
10. Does a personal/local application need formal registration before a closed
    beta, and what exact public-release threshold and review lead time apply?

### Limitations

- No written Nexus interpretation was requested or received during this
  bounded investigation.
- The API AUP is old enough that its registration/authentication details may
  lag current v3 practice.
- The official v3 specification was updated recently and contains experimental
  operations; capability may change.
- The maintained first-party client includes GraphQL surfaces not fully
  documented in the inspected v3 OpenAPI contract. Code presence alone is not
  a stability or public-support promise.
- No authenticated response, content visibility, age filtering, quota header,
  cancellation behavior, or revocation behavior was tested.
- Copyright, database right, uploader-specific licences, privacy, and local
  law may impose constraints beyond Nexus policy. This report does not resolve
  them.

## Original recommendation and accepted disposition

Confidence remains **high** that HTML scraping and unsupported bulk extraction
must not proceed, and **medium** on how Nexus would interpret the supported-API
analysis boundary because current official documents conflict or leave it
unstated.

The investigation originally recommended blocking acquisition. ADR-0005
supersedes that operational recommendation, but not the underlying evidence or
uncertainty:

1. RQ-008 may perform bounded live experiments against documented, supported
   mod-content APIs using the owner's personal key during personal/development
   use.
2. Every request must be user initiated, accurately identified, relevant to the
   selected profile/corpus experiment, rate limited, sanitized, and recorded.
3. Retrieved evidence may be processed deterministically and, when explicitly
   selected, sent in minimized form to a user-selected LLM for inference only.
4. Raw Nexus content remains product-private. Externally shareable outputs
   default to derived findings, attribution, links, permitted short citations,
   and explicit omissions rather than source-body reproduction.
5. Unsupported content surfaces remain coverage gaps; no API gap may fall back
   to HTML scraping, browser automation, or undocumented endpoints.
6. The project will still ask `support@nexusmods.com` for a written policy
   determination using the concrete data-flow description below:

   - local Windows desktop application;
   - user-owned, revocable authorization;
   - every acquisition manually initiated;
   - accurate application identity headers;
   - bounded per-enabled-mod API retrieval with caching/backoff;
   - deterministic extraction of requirements/incompatibilities/instructions;
   - optional minimal-context use of a user-selected LLM for inference, never
     model training;
   - local product-owned retention of exact cited excerpts, source
     identifiers/revisions, fingerprints, and derived claims;
   - user deletion and explicit refresh;
   - externally shareable exports that can omit text and retain only permitted
     citations/fingerprints;
   - no rehosting, public corpus, shared compatibility database, or background
     unsolicited acquisition.

7. Request explicit answers to all ten unresolved questions above, plus any
   application-specific rate/batch guidance.
8. Preserve any response as dated policy evidence,
   constrain the adapter and export matrix to its exact scope, and seek public
   application registration once Nexus's required testing-build threshold is
   reached.
9. If Nexus denies or materially conditions the workflow, trigger ADR-0005's
   reversal review and stop affected acquisition until the decision is
   superseded.
10. Do not treat local retention as unlimited or as redistribution permission.
    The accepted RQ-031 disposition selects useful-analysis retention,
    metadata-first durable minimization, refresh, deletion, and replay
    semantics; RQ-013 must select a mechanism and measured defaults that
    preserve them.
11. Exclude all raw Nexus text/media/file material and unapproved derived
   content from externally shareable exports by default. A later source-policy
   decision may relax that only for verified permitted classes.

## Exact follow-ups enabled

ADR-0005 applies the operational decision. The remaining items are downstream
work:

1. **Nexus support request:** request confirmation for the bounded data flow
   described above, then preserve the dated response as a follow-up RQ-009
   investigation input.
2. **Source registry update — applied:** the Nexus record now includes:

   - official source class;
   - policy URLs/update dates;
   - API documentation versions;
   - HTML scraping prohibited;
   - supported-API analysis and necessary private retention permitted by
     ADR-0005, with the underlying ambiguity and reversal triggers recorded;
   - raw external redistribution prohibited by default and exact derived-claim,
     quotation, and deep-link boundaries pending confirmation;
   - user-initiated local personal key allowed only for testing/otherwise
     permitted uses;
   - public-app registration required;
   - accurate app headers, rate limiting, age filtering, untrusted-content
     handling, and explicit gap semantics.

3. **RQ-008 input:** map public interfaces, then perform bounded authenticated
   experiments only against documented, supported operations. Preserve
   unavailable surfaces as gaps.
4. **RQ-013 input:** model source bytes, permitted excerpt cache, derived
   claims, citations/fingerprints, and application links as separate retained
   classes; do not select a Nexus cache policy yet.
5. **RQ-031 input — applied to the accepted disposition:** Nexus-specific rows
   permit private source evidence through useful dependent work under ADR-0005
   while keeping raw-text external sharing excluded by default.
6. **RQ-032 input:** require age-content filtering, HTML/BBCode sanitization,
   external-navigation controls, user-key isolation/revocation, and a
   policy-expiry circuit breaker before an authenticated adapter.
7. **ADR follow-up:** review or supersede ADR-0005 when Nexus responds; later
   mechanism ADRs must implement its acquisition/provider/retention/export
   boundary without silently broadening it.
8. **Evaluation proposals:**

   - extend `EVAL-0068` with a Nexus source whose access policy is prohibited or
     expired and assert that no request/extraction occurs and a coverage gap is
     emitted;
   - extend `EVAL-0040` so private Nexus evidence is omitted or replaced by an
     approved citation/fingerprint in externally shareable exports;
   - add an authenticated-source boundary case, or extend the appropriate
     security case, for local secure key use, user initiation, revocation,
     accurate app identity, age filtering, quota backoff, and no server-side
     key storage.

9. **M4 registration input:** budget a Nexus testing-build review and
   registration gate before public release; do not assume acceptance, timing,
   or an SSO/JWT mechanism until confirmed.

## RQ-009 disposition

Accepted update for `RQ-009`:

> **Answered for M0 by ADR-0005, with external clarification pending.** HTML
> scraping and unsupported/bulk/rehost behavior remain prohibited. The project
> accepts bounded, user-initiated retrieval and diagnostic transformation
> through supported Nexus APIs as personal/mod-enhancement and third-party
> application use. Preserve the contrary section 11 interpretation as a risk;
> request Nexus confirmation; and review or supersede ADR-0005 on a policy
> change, contrary response, registration decision, or material data-flow
> expansion.

## Requirements-and-evidence traceability

| Requirement/decision | Evidence | Finding/recommendation |
|---|---|---|
| `DOC-006`; integration boundary | S1 sections 1 and 11 | Reject HTML scraping, browser automation, and undocumented page acquisition. |
| `SCOPE-004`, `DOC-002`, `DOC-011` | S2 personal-key and unacceptable-use sections | Require a user-initiated acquisition run with real application identity; never use keys in unsolicited work. |
| `SEC-002`, `AI-003` | S2 key-storage/use prohibition; S6 auth schemes | Keep user credentials local/secure and outside prompts/logs/exports; registration/auth mechanism remains follow-up work. |
| `DOC-001`, `COVER-001`–`COVER-003` | S1 section 11; capability matrix | Represent unavailable Nexus content as explicit source/entity coverage gaps. |
| `DOC-008`, `SNAP-006`, `OPS-002` | S1 sections 10–11; no cache term in S1/S2 | Do not assume exact-passage/full-body retention; disclose replay/audit loss. |
| `DOC-009` | S1 section 5 and displayed update dates | Version and periodically reverify source policy; stop when approval assumptions expire or change. |
| `SEC-004`, `OPS-003` | S1 sections 10, 22, 24, and 27 | Omit raw Nexus material and unapproved derived content from shareable exports; retention is not redistribution. |
| `SEC-001` | S1 age-content sections; accepted security boundary | Sanitize all returned content and resolve API-consumer age filtering before integration. |
| `OPS-001`, `SCAN-003`–`SCAN-006` | S3 quota/reset/help behavior | Declare live-network/auth/quota needs, back off on limits, and report skipped source coverage. |
| ADR-0001 | S1/S2 plus capability evidence S4–S9 | Keep policy claims, API capability, source content, model interpretation, and local applicability distinct. |
| ADR-0002 | S6/S7 version and revision observations | Retain exact source/policy/API identity and acquisition/application links without rebinding history. |
| ADR-0005; M0 Gate A | S1 section 10 purpose, S1 section 11 ambiguity, S2 and active API evidence | Bounded supported-API acquisition is an accepted project interpretation; the ambiguity is a documented non-blocking risk and Gate A may pass with unsupported surfaces represented as gaps. |

## Semantic self-review checklist

- The report distinguishes API capability from permission and identifies
  ADR-0005—not availability alone—as the project authority to proceed.
- The report distinguishes current policy, older API policy, capability,
  interpretation, and unresolved ambiguity.
- No claim says Nexus has prohibited every API application.
- No claim presents the owner's personal/mod-enhancement interpretation as
  Nexus confirmation or legal certainty.
- No claim treats server caching, a rate quota, a public page, or a maintained
  client as retention/redistribution permission.
- The report requires a coverage gap rather than an undocumented fallback.
- ADR-0005 selects an operational policy boundary, not an implementation
  architecture.
- RQ-008 capability work, the accepted RQ-031 policy disposition, and RQ-032
  security-control work remain separate.
