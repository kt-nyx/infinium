# RESEARCH-0026: Non-Nexus source governance

Status: Completed; recommendation revised and partially superseded

Subsequent revision:
[RESEARCH-0031](RESEARCH-0031-loot-freshness-and-source-discovery.md) revises
the LOOT freshness and GitHub-documentation priority recommendations, while
[RESEARCH-0032](RESEARCH-0032-openai-first-llm-and-web-search.md) and accepted
ADR-0013 supply the governed search-provider boundary. Accepted ADR-0014 owns
the revised LOOT freshness mechanism. This report remains dated
historical evidence.

Date: 2026-07-26

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary research question: RQ-010

M0 wave: D

Decision enabled: Minimal M1 non-Nexus source-registry and search boundary

## Executive answer

Infinium should not approve “the public web” as a source. The smallest useful
M1 non-Nexus registry is:

1. documentation bundled with the selected local installation;
2. the already-governed, commit-pinned LOOT Skyrim SE masterlist and prelude;
3. documentation and release metadata from a positively mapped official
   project repository on GitHub, retrieved through versioned public REST
   `GET` operations;
4. author-owned sites only after a hostname/path-specific record verifies an
   explicit supported interface and terms for automated access; and
5. an optional investigative lane for public GitHub issues/comments inside an
   already mapped official repository.

Broader community-web discovery should remain disabled until an exact search
provider, API, terms, query privacy, result-retention, and cost contract are
reviewed. When delivered, search results are discovery leads only. Infinium
must acquire a cited landing source through its own approved source record
before using the source text as evidence.

This is a product-policy recommendation, not legal advice or a claim that a
platform has approved Infinium.

## 1. Question and accepted constraints

### 1.1 Primary question

Which non-Nexus sources should be approved, and how can they be searched
legally and reliably?

### 1.2 Linked accepted requirements

- `DOC-001` through `DOC-011`, especially source authority, local
  documentation, freshness, governed broader search, and acquisition-run
  provenance;
- `EVID-001` through `EVID-006`;
- `SCOPE-004`;
- `SEC-001` through `SEC-004`;
- `AI-001` through `AI-004`, `AI-006`, and `AI-007`;
- `OPS-001` through `OPS-003`; and
- accepted taxonomy `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`.

### 1.3 Accepted decision constraints

- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md)
  makes authority claim-type-specific and prevents a model or search rank from
  granting authority.
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md)
  preserves the read-only setup boundary.
- [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md)
  is Nexus-specific and cannot be generalized into public-web permission.
- [ADR-0011](../../architecture/decisions/ADR-0011-loot-semantic-and-managed-data-boundary.md)
  governs the LOOT masterlist/prelude identities and semantic boundary.
- [RESEARCH-0003](RESEARCH-0003-retention-replay-export-policy.md) requires
  independent acquire, private-retain, provider-transmit, and redistribute
  decisions. Unknown on one axis stops that operation.

## 2. Scope, non-scope, and preflight

### 2.1 In scope

- the minimum non-Nexus sources needed for M1 documentation work;
- source discovery versus evidence acquisition;
- official GitHub repositories, releases, documentation, issues, and comments;
- local/in-archive documentation;
- the accepted LOOT managed-data sources;
- conditions for later author-owned-site adapters;
- the boundary for optional community search;
- revision, freshness, authority, retention, transmission, export, failure,
  and reverification behavior; and
- exact proposed source-registry rows.

### 2.2 Explicit non-scope

- Nexus endpoints, which belong to RQ-008 and ADR-0005;
- selecting an LLM or general web-search provider;
- authenticated GitHub use or a GitHub App;
- crawling or scraping arbitrary sites;
- downloading mods, GitHub release assets, repository archives, or a raw
  documentation corpus;
- deciding quotation-law or fair-dealing questions;
- accepting a storage, networking, worker, or application architecture;
- promoting community material to author authority; and
- registering every possible mod host, forum, wiki, forge, or archive.

### 2.3 Access performed

Research used public official documentation and four unauthenticated,
read-only GitHub REST requests. No credentials, billable calls, browser
sessions, mod downloads, repository clones, release assets, or setup writes
were used. Response bodies were discarded and no raw external corpus was
tracked.

## 3. Sources and exact versions

All external sources were retrieved or rechecked on **2026-07-26**.

| ID | Primary source | Version/revision | Claim-level relevance |
|---|---|---|---|
| I1 | [Accepted product requirements](../../product/requirements.md), [source registry](../source-registry.md), and [M0 plan](../../plans/milestones/M0-research-foundation.md) | Current accepted/draft repository state reviewed 2026-07-26 | Normative source authority, retention, search, provenance, and Wave D scope. |
| I2 | [RESEARCH-0001](RESEARCH-0001-nexus-access-policy.md), [RESEARCH-0003](RESEARCH-0003-retention-replay-export-policy.md), and [RESEARCH-0004](RESEARCH-0004-wave-a-policy-and-evidence-handling-integration.md) | Completed/accepted Wave A inputs | Four-axis permissions, source-specific handling, and the prohibition on generalizing Nexus risk acceptance. |
| G1 | [GitHub Terms of Service](https://docs.github.com/en/site-policy/github-terms/github-terms-of-service) | Effective 2026-04-27 | Public-repository access, user-content/licence boundaries, API terms, rate-abuse restrictions, and mutable-policy trigger. |
| G2 | [GitHub Acceptable Use Policies](https://docs.github.com/en/site-policy/acceptable-use-policies/github-acceptable-use-policies) | Moving policy; retrieved 2026-07-26 | Prohibits excessive bulk activity and misuse of personal information; distinguishes API collection from scraping while retaining API/Terms obligations. |
| G3 | [GitHub REST contents](https://docs.github.com/en/rest/repos/contents), [releases](https://docs.github.com/en/rest/releases/releases), [issues](https://docs.github.com/en/rest/issues/issues), and [search](https://docs.github.com/en/rest/search/search) documentation | API version `2026-03-10` | Supported public read interfaces, public unauthenticated availability, entity fields, pagination, candidate search, and content/release/issue revision signals. |
| G4 | [GitHub REST rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api) and [best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api) | API version `2026-03-10` | Public unauthenticated limit, conditional requests, serial access, rate-error handling, and mutable secondary limits. |
| W1 | [RFC 9309](https://www.rfc-editor.org/rfc/rfc9309.html) | September 2022 | `robots.txt` is a crawler instruction mechanism, not access authorization; its rules still must be honored by a crawler. |
| W2 | [RFC 9111](https://www.rfc-editor.org/rfc/rfc9111.html) | June 2022 | HTTP `ETag`/`Last-Modified` validation and conditional-request semantics. |
| W3 | [RFC 4287](https://www.rfc-editor.org/rfc/rfc4287.html) | December 2005 | Atom entry/feed identity and publisher-significant `updated` metadata; useful revision signals, not rights permission. |
| L1 | [LOOT Skyrim SE masterlist](https://github.com/loot/skyrimse) and its [licence at the accepted commit](https://raw.githubusercontent.com/loot/skyrimse/4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f/LICENSE) | Tag family `v0.29`; commit `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f`; CC0-1.0 | Existing managed-data identity and broad reuse permission. |
| L2 | [LOOT prelude](https://github.com/loot/prelude) and its [licence at the accepted commit](https://raw.githubusercontent.com/loot/prelude/ea316265c11b5c6e6f51d53deb34c4054f4c2349/LICENSE) | Tag family `v0.29`; commit `ea316265c11b5c6e6f51d53deb34c4054f4c2349`; CC0-1.0 | Existing managed-data identity and broad reuse permission. |

### Source-applicability cautions

- GitHub is both a transport/platform and a host for independently licensed
  user content. Platform API availability does not license every README,
  changelog, issue, or attachment for unrestricted copying or redistribution.
- A repository licence is usable only for content it actually covers. A
  detected SPDX label is not proof that every non-code document or embedded
  third-party item has that licence.
- Public repository visibility or search rank does not prove that a repository
  is official for a mapped mod.
- `robots.txt`, an Atom feed, HTTP cache headers, a sitemap, and a search
  result are technical signals. None independently grants content rights or
  evidence authority.
- The accepted CC0 decisions apply to the pinned LOOT masterlist and prelude,
  not to private LOOT userlists or arbitrary repository content.

## 4. Bounded experiments

Environment:

- Windows PowerShell host;
- `curl.exe`;
- GitHub REST API version header `2026-03-10`;
- unauthenticated public access; and
- no retained response bodies.

### Experiment E1 — commit-pinned public document lookup

Request shape:

```text
GET /repos/loot/skyrimse/contents/README.md
    ?ref=4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f
Accept: application/vnd.github+json
X-GitHub-Api-Version: 2026-03-10
```

Observed:

- `200 OK`;
- selected API version `2026-03-10`;
- strong `ETag` equal to the returned blob identity;
- `Last-Modified`;
- `X-RateLimit-*` headers; and
- public unauthenticated `core` limit of 60 requests/hour.

Interpretation: a known official repository/path can be acquired through a
versioned supported interface and tied to both commit and blob identity.
Neither header nor API success establishes content authority or
redistribution rights.

### Experiment E2 — conditional validation

The first shell attempt to repeat E1 malformed the conditional header and
produced an ordinary duplicate `200` read; its body was discarded. The
corrected request with the exact `If-None-Match` value returned
`304 Not Modified`. This confirms a reliable freshness optimization for that
resource. GitHub documents that an authenticated correctly conditional `304`
does not count against the primary limit; the anonymous experiment must not
generalize that rate-accounting benefit.

### Experiment E3 — bounded community surface

Request shape:

```text
GET /repos/loot/skyrimse/issues?state=all&per_page=1
```

Observed:

- `200 OK`;
- API version, `ETag`, pagination, and rate headers; and
- no authentication requirement for the public repository.

Interpretation: public issues can be read through a supported API without page
scraping, but they remain community/investigative evidence unless an
independently verified author/maintainer statement has the relevant scope.

### Artifact manifest and side effects

| Item | Retention | Side effect |
|---|---|---|
| Request shapes and sanitized status/header observations above | This report only (`D/M`) | Four public REST reads consumed anonymous rate-limit capacity |
| Response bodies | Discarded | None retained |
| Credentials, cookies, local profile data, mod bytes | Not used | None |

## 5. Findings

### 5.1 Discovery, acquisition, and authority are three different decisions

Search may identify a candidate URL without permission to fetch its body.
Successful acquisition may provide text without proving author authority.
Author authority may establish what the author stated without proving that the
claim applies to the selected local installation.

The required progression is:

```text
search/discovery result
  -> registered source and permitted acquisition method
  -> captured source/entity/revision
  -> identity/author-role and applicability adjudication
  -> typed claim with its actual authority
```

A search-result snippet, model answer, ranking, hostname, repository topic, or
username may not skip a step.

### 5.2 Local documentation is the safest first non-Nexus source

Bundled documentation is already part of the user-selected installed state and
requires no network search. It should be enumerated through the immutable
installation snapshot, parsed only in supported formats, and tied to provider,
relative path, content identity, and installed-mod identity.

Its source role is still conditional:

- a document with established mod/author provenance may support author intent,
  instructions, and compatibility claims;
- an unattributed or repackaged document remains uncertain;
- a local README does not prove that its instructions match the installed
  version; and
- local availability does not grant public redistribution rights.

### 5.3 GitHub is supportable as a narrow transport, not a universal corpus

The versioned public REST API supplies the exact read surfaces M1 needs:
repository metadata, commit-addressed contents, release metadata/body, and,
when explicitly enabled, repository issues/comments. GitHub documents public
unauthenticated reads, rate limits, backoff, and conditional validation.

The reliable M1 order is:

1. use a pre-existing authoritative link or local identity mapping to select a
   repository;
2. use repository search only to propose candidates when mapping is missing;
3. verify the repository/author relationship before granting author authority;
4. acquire only allowlisted documentation/release paths and metadata;
5. resolve a moving branch/tag to a commit and retain the path/blob identity;
6. record the exact API version, request, response status, retrieval time,
   validators, and rate state; and
7. treat issues/comments as a separately toggled investigative population.

Global code search, repository cloning, archive download, release-asset
download, arbitrary recursive trees, and HTML page automation are unnecessary
for M1 and should be excluded.

### 5.4 Author-owned websites require per-host approval

No generic author-site adapter is supportable from public visibility alone. A
site becomes eligible only after a registry record establishes:

- exact hostname and allowed path/endpoint patterns;
- an authoritative mapping to the mod/project;
- a first-party API, Atom/RSS feed, static documentation endpoint, or other
  interface whose current terms permit the intended automated read;
- the Infinium user agent and rate/caching behavior;
- current `robots.txt` compatibility for crawler-like requests;
- content/licence and four-axis decisions;
- revision/freshness behavior; and
- stop/reverification triggers.

An explicit disallow stops access. A missing `robots.txt` does not supply
permission because RFC 9309 states that the protocol is not authorization.
An unreachable policy or ambiguous terms therefore remain a coverage gap.
A document already present in the selected installed state may enter through
the local-installed-documentation source with its real provider, path, and
provenance. A separately user-imported document would need its own reviewed
source/provenance contract; copying it locally does not turn it into bundled
installed documentation or cure an unresolved acquisition permission.

### 5.5 Community search must remain lead-only and opt-in

The only community lane supportable now is targeted public issues/comments
inside an already mapped GitHub project repository. It is:

- off by default;
- manually selected as part of the initiated operation;
- bounded by repository, installed mod/version, query, result/page count, and
  rate limits;
- classified investigative;
- not required for core coverage; and
- unable to affect readiness until corroborated by qualifying evidence.

A general web-search provider is not selected here. Its future registry record
must define the exact supported API/tool, query disclosure, authentication,
cost, provider/tool retention, result licence, snippet handling, geographic or
safe-search behavior, freshness, cancellation, and citation fields. Until
then, no generic community-web request is approved.

Even after approval, a result URL/title/snippet is a discovery record. The
landing content must pass its own source adapter. A provider-generated answer
is model output, not an acquired author/community source.

### 5.6 Taxonomy use does not change authority

Author-maintained documentation may support `declared` purpose/intended-target
assignments. Local documentation paths or repository content types do not by
themselves assign technical surface, affected area, consequence, or effect
extent. Community search may propose `predicted` investigation routing only;
it cannot establish taxonomy assignments by popularity or search rank.

## 6. Four-axis source decisions

`B/X/D/M/O` use the accepted RESEARCH-0003 content-form vocabulary.

| Source class | Acquire/inspect | Private retention | Provider transmission | External redistribution |
|---|---|---|---|---|
| Installed/bundled documentation | **Allow conditionally** for snapshot-bound supported files and bounded in-archive members | `B/X/D/M` through useful dependent work when the user's installed-copy/source terms permit; durable `X/D/M/O` default after materialization | Minimized credential-free `X/D/M` only through explicit user-selected inference and when no source restriction blocks disclosure | `D/M/O` default; `X` only after quotation/licence review; `B` only with affirmative redistribution right |
| Pinned LOOT Skyrim SE masterlist and prelude | **Allow** through the managed immutable-revision path in ADR-0011 | `B/X/D/M` | Relevant minimized `B/X/D/M` permitted by CC0, subject to AI context minimization | `B/X/D/M` permitted by CC0; preserve source/revision provenance |
| Mapped official GitHub project docs/releases | **Allow conditionally** through versioned public REST `GET` endpoints; exclude assets/archives | `X/D/M` by default; `B` only when the exact item licence/permission covers it and full-body retention is necessary | `D/M`; minimized `X` only when the item/source decision permits third-party inference disclosure | `D/M/O` default; `X/B` only under the exact content licence/quotation decision and required notices |
| GitHub issues/comments in a mapped official repository | **Allow conditionally**, opt-in and targeted through public REST `GET` endpoints | `X/D/M`; transient body only as needed; minimize personal data | Minimized `X/D/M` only under explicit inference choice and source/provider review | `D/M/O` default; exact excerpts require separate quotation/privacy review |
| Author-owned website | **Stop by default**; allow only after a hostname/path/interface-specific record passes | Source-specific; no generic permission | Source- and provider-specific; disabled unless affirmatively allowed | Source-specific; `D/M/O` default |
| General community-web search | **Not yet approved**; exact search-provider/API qualification required | No current permission | No current permission | No current permission |
| Arbitrary public HTML, forum, wiki, social site, search cache, AI answer, or archive snapshot | **Not approved** | — | — | — |

## 7. Proposed registry rows

These are proposed changes for coordinator review. This investigation does not
edit the registry.

### 7.1 `local-installed-documentation`

| Field | Proposed value |
|---|---|
| Source class / authority | Source class `author`; authoritative evidence role only when document-to-mod/author provenance is established, otherwise investigative/unresolved |
| Games/content | Skyrim SE; supported README, requirements, compatibility, changelog, install/update/removal, configuration, and generated-output instructions |
| Access | Snapshot-bound local filesystem and qualified archive reader; no execution or privileged rendering |
| Search | Allowlisted relative names/extensions plus bounded text indexing inside the selected installation; no arbitrary system search |
| Allowed/prohibited | Read/sanitize/extract; prohibit scripts, macros, external active content, setup writes, and traversal outside snapshot |
| Four axes | Use the installed/bundled row in section 6 |
| Freshness | Snapshot ID, provider chain, installed-mod identity, relative path/archive member, size, hash, parser version, and retrieval/parse time |
| Failure | Unsupported format, ambiguous provider/author, malformed content, or missing version becomes an explicit gap |
| Reverify | Snapshot/provider/content change; archive/parser-support change; discovered licence/author restriction; provider/export policy change |

### 7.2 `loot-skyrimse-masterlist` and `loot-prelude`

| Field | Proposed value |
|---|---|
| Source class / authority | `curated`; authoritative only for the curated metadata statements and conditions |
| Access | Accepted ADR-0011 managed-data acquisition at compatible immutable commits |
| Search | Structured plugin identity, condition, and metadata evaluation; not general prose/web search |
| Four axes | Use the LOOT row in section 6 |
| Freshness | Repository, exact commit/blob hashes, compatible pair identity, retrieval time, integrity result, adapter/libloot version, and stale/offline state |
| Failure | Missing/incompatible/unverified inputs disable LOOT-backed coverage without affecting unrelated analysis |
| Reverify | ADR-0011 supersession; repository/licence/format change; libloot/data compatibility change; selected revision unavailable; packaging/export change |

Separate rows are required because masterlist and prelude have independent
identities even when consumed together.

### 7.3 `github-official-project-documentation`

| Field | Proposed value |
|---|---|
| Source class / authority | `author` for positively mapped maintainer-owned documentation/release statements within their declared scope; platform/repository metadata remains `official` technical metadata |
| Access | GitHub public REST API `2026-03-10`: repository metadata, releases, and allowlisted contents at exact refs; no authentication in M1 |
| Search | Existing authoritative links first; repository-search API only for candidate discovery; no global code search |
| Allowed/prohibited | Public `GET`, pagination, validators, backoff; prohibit page scraping, recursive corpus acquisition, clone/archive/asset download, private resources, write endpoints, and token use |
| Four axes | Use the mapped official GitHub row in section 6 |
| Freshness | Repository numeric ID/full name/owner, mapping evidence, release ID/tag/target, commit SHA, content path/blob SHA, `ETag`, `Last-Modified`, retrieval time, API version, item licence, and moved/deleted state |
| Failure | Ambiguous official mapping, absent item licence for an operation, `401/403/404/410/429`, unsupported path, or exhausted rate becomes a gap/stop, never HTML fallback |
| Reverify | GitHub Terms/AUP/API version or rate change; repository transfer/rename/archive/delete; author disavowal; item-licence change; API deprecation/sunset; authenticated or public-service proposal |

### 7.4 `github-official-project-community`

| Field | Proposed value |
|---|---|
| Source class / authority | `community`, investigative by default; a maintainer/author statement is upgraded only after separate identity/role and scope adjudication |
| Access | Opt-in public REST issue/comment reads within a repository already accepted by the documentation row |
| Search | REST list/filter inside the mapped repository by state, labels, and update time, followed by bounded local text matching; no cross-repository search |
| Four axes | Use the GitHub issue/comment row in section 6 |
| Freshness | Repository ID, issue/comment ID, author account ID, author-association signal, created/updated/deleted state, URL, `ETag`, retrieval time, and query/filter |
| Failure | No community-source core-coverage penalty; report skipped/limited/removed/rate-limited state separately |
| Reverify | Same GitHub triggers plus privacy/removal request, changed issue visibility, broader cross-repository/global search, or use in externally shared output |

### 7.5 `author-site-explicit-interface` template

This is a template, not an active source row. Every enabled instance must have
a distinct source ID and exact hostname/path.

| Field | Required value before activation |
|---|---|
| Identity | Author/project mapping and exact hostname/path patterns |
| Interface | First-party API, Atom/RSS feed, static endpoint, or documented read method |
| Policy | Terms/licence and `robots.txt` retrieved with dates; absence/ambiguity is not approval |
| Access behavior | User agent, serial/rate/backoff, redirect boundary, maximum pages/bytes, no recursive crawl or authenticated area |
| Four axes | Explicit source-specific decisions and `B/X/D/M/O` forms |
| Freshness | Canonical URL, feed entry ID/updated when applicable, HTTP validators, content hash, retrieval time, and publisher revision/version |
| Failure/reverify | Policy/robots/interface/domain-owner/licence/revision change; redirect outside registered host; authorization request; `401/403/429`; unstable identity; public export or provider-transmission expansion |

### 7.6 `community-web-discovery` future template

This is disabled until a separate exact provider record is reviewed.

| Field | Required value before activation |
|---|---|
| Source class / authority | Discovery service only; every result remains an investigative lead |
| Interface | Named supported search API/tool and exact contract/version |
| Query boundary | Explicit opt-in; grounded mod/author/version terms; exclude local paths, usernames, credentials, unrelated profile contents, and full modlist by default |
| Result boundary | URL, title, provider snippet if permitted, rank, query, time, provider/index identity; no automatic landing-page body |
| Four axes | Provider-specific acquire/retain/transmit/redistribute decisions for query and results |
| Limits | Per-operation queries/results/pages/time/cost; cancellation and rate handling |
| Reverify | Provider terms/API/index/data-use/retention/cost change; use of hosted page-fetch tools; new result class; server/shared-store proposal |

## 8. Search and acquisition contract

The minimum workflow should be:

1. derive source candidates from installed identity, bundled documentation,
   accepted Nexus identity metadata, LOOT links, or an existing authoritative
   source link;
2. label how each candidate was discovered;
3. select an active registry row before any network body request;
4. construct a source-adapter request limited to exact host/entity/path,
   content classes, maximum bytes/pages/results, and rate budget;
5. record redirects and reject escape from the registered authority;
6. capture source/entity/version and content fingerprint before extraction;
7. assign evidence authority from source role and author identity, not the
   search provider;
8. retain/transmit only forms allowed on those independent axes;
9. preserve query, result-selection, acquisition, extraction, and application
   provenance as distinct events; and
10. abstain with a source-specific coverage gap if any required permission,
    identity, revision, or applicability decision is unresolved.

No search operation may silently acquire every result, follow related links,
or convert a provider summary into a cited source.

## 9. Alternatives

| Alternative | Benefit | Rejection/limitation |
|---|---|---|
| Permit arbitrary public HTML if `robots.txt` allows it | Broad coverage and simple crawler | Reject: RFC 9309 says robots rules are not authorization; terms, content rights, authority, revision, and redistribution remain unknown. |
| Use search-engine snippets/model answers as evidence | Avoids landing-page acquisition | Reject: snippets/answers are incomplete provider outputs and cannot establish source text, revision, author role, or applicability. |
| Approve every major modding forum/host now | Potentially many obscure leads | Reject for M1: each requires current interface/policy work and ordinary community posts are not core coverage. |
| Use only local and Nexus documentation | Lowest new policy surface | Viable fallback, but excludes well-maintained official project documentation and the already accepted LOOT data. |
| Clone mapped GitHub repositories | Strong local replay | Reject as default: excessive acquisition, weaker minimization, more licence/storage burden, and unnecessary for bounded docs. |
| GitHub public REST for mapped docs plus opt-in in-repository issues | Supported interface, revision data, bounded scope | Recommended, subject to exact content licence, mapping, rate, and four-axis controls. |
| Select a general search provider now | Immediate broad discovery | Defer: provider, auth, cost, query privacy, result rights, and hosted-fetch behavior belong to Wave D provider research and a separate registry decision. |

## 10. Contrary evidence, uncertainty, and unsupported cases

### Contrary and boundary evidence

- Public GitHub repositories are deliberately internet-accessible, and the
  Terms state that, absent specific restrictions, they do not restrict lawful
  third-party access. However, GitHub's user-content and AUP/API provisions do
  not provide a blanket external redistribution licence for every item.
- Exact source bodies improve extraction review and replay. The recommendation
  therefore permits source-specific private `B` rather than banning it, while
  rejecting bulk repository acquisition.
- A repository issue can contain a definitive maintainer answer. It still
  requires author/role and applicability adjudication rather than making every
  issue authoritative.
- RFC 9309 permits crawler behavior in some missing-file cases at the protocol
  level. Infinium applies the stricter product rule that missing robots
  information does not cure unclear terms or source permission.

### Material uncertainty

1. This report does not perform legal analysis of fair use/fair dealing,
   transient copies, or quotation limits.
2. GitHub's current AUP information-use wording, API Terms, and public-content
   clauses must be applied together; the item licence remains the safest basis
   for source-body retention, provider transmission, and redistribution.
3. Anonymous GitHub rate limits are too small for indiscriminate high-end
   modlist acquisition. M1 must remain identity-led and cached; authenticated
   operation requires later credential/security and GitHub-app review.
4. No concrete independent author-owned domain was selected, so no author-site
   access is currently active.
5. No general search provider was selected or tested.
6. Local mod documentation licences and provenance vary.
7. Search results may be stale, personalized, geo-dependent, incomplete, or
   removed. Search recall cannot be represented as source coverage.

### Explicitly unsupported

- ModDB, Bethesda.net, Steam Workshop, AFK Mods, forums, Reddit, Discord,
  social networks, wikis, archive services, and other forges are not approved
  by category.
- HTML crawling, headless-browser collection, login/session automation,
  search-cache acquisition, and undocumented endpoints are not approved.
- GitHub private repositories, authenticated API use, releases assets,
  repository archives, and global code search are not approved for M1.
- A source without a stable revision may be cited as a moving source with a
  retained fingerprint/time, but it cannot provide immutable clean
  reacquisition.

## 11. Recommendation

Confidence:

- **High** for separating discovery/acquisition/authority and the four
  permission axes; for local docs, pinned LOOT data, narrow GitHub REST reads,
  and no blanket public-web permission.
- **Medium** for content-specific GitHub and local-document private/provider
  permissions because licences and legal exceptions vary.
- **High** that author sites and general community search must remain
  source/provider-specific until their exact interfaces and terms are known.

Recommend accepting the six registry rows/templates in section 7 with these
preconditions:

1. only `local-installed-documentation`, the two LOOT rows, and
   `github-official-project-documentation` are M1 primary-source candidates;
2. GitHub official-project content requires positive identity mapping and
   item-level content/licence treatment;
3. `github-official-project-community` is optional, opt-in, bounded, and
   investigative;
4. author-site instances remain disabled until individually reviewed;
5. `community-web-discovery` remains disabled until an exact provider record
   is reviewed; and
6. no row weakens ADR-0005, ADR-0011, SEC-001, context minimization, or
   RESEARCH-0003's independent permission axes.

## 12. Exact downstream work enabled

### Source registry

Add the proposed rows in section 7 after coordinator review. Record active,
conditional-template, disabled, and unsupported status explicitly so a
template cannot be mistaken for acquisition permission.

### Architecture decision

RQ-010 alone does not warrant a separate durable architecture ADR. Its logical
source-policy constraints should be consumed by the later documentation-source
acquisition/persistence ADR anticipated by the ADR index. If Wave D selects an
authenticated GitHub or general search-provider mechanism, that durable
credential/provider boundary may warrant an ADR.

### Product specification

No product-requirement change is needed. The current `DOC-006` through
`DOC-011`, `SEC-001`, `AI-003`, and `OPS-001` through `OPS-003` requirements
already express the necessary behavior.

### Evaluation

Refine or instantiate:

- `EVAL-0010` and `EVAL-0011` with a permitted local or commit-pinned official
  source plus version/applicability controls;
- `EVAL-0031` with commit/blob/HTTP-validator refresh behavior;
- `EVAL-0033` with hostile instructions embedded in local, GitHub, and
  community text;
- `EVAL-0068` with allowed, disabled-template, and prohibited-source cases;
- `EVAL-0073` with search-result discovery that cannot bypass landing-source
  acquisition or community authority;
- `EVAL-0083` with discovery, acquisition, extraction, and application
  provenance as distinct edges; and
- a GitHub adapter contract case for `200`, `304`, redirect, removed,
  rate-limited, ambiguous-official-source, and unlicensed-body boundaries.

### Follow-up research

- RQ-011/RQ-012: determine whether any selected provider tool performs search
  or page fetching and keep those third-party boundaries out of core source
  authority.
- RQ-013: implement separate source, revision, acquisition, extraction,
  application, permission, and deletion objects.
- RQ-018/RQ-032: required before authenticated GitHub or other source access.
- New source-specific RQ or dated registry review for each author domain,
  community platform, forge, or general search provider proposed for use.

## 13. Suggested RQ-010 disposition

Suggested registry update:

> **Answered for M0 with a minimal conditional registry.** Approve
> snapshot-bound local documentation, pinned CC0 LOOT masterlist/prelude data,
> and positively mapped official GitHub documentation/release metadata through
> versioned public REST reads. Permit mapped-repository GitHub issues/comments
> only as an opt-in investigative lane. Author-owned web sources require
> hostname/path/interface-specific approval; broader community search remains
> disabled until an exact provider/API and four-axis record are reviewed.
> Search/discovery never grants acquisition permission or evidence authority.

Because RQ-010 is Conditional in the accepted M0 plan, this disposition is
sufficient for the current M1 source set without forcing broader-web delivery.

## 14. Requirements-and-evidence traceability

| Requirement/decision | Evidence | Result/downstream use |
|---|---|---|
| `DOC-006`, ADR-0001 | I1, I2; sections 5.1, 5.5 | Authority remains claim-type-specific; search/model selection cannot promote community material. |
| `DOC-007` | I1; sections 5.2, 7.1 | Snapshot-bound local documentation is the first approved non-Nexus source. |
| `DOC-008`, RESEARCH-0003 | I2; sections 6–7 | Every source retains separate acquire/private/provider/export decisions and useful-work retention. |
| `DOC-009` | G3, G4, W2, W3; sections 4, 7, 8 | Commit/blob IDs, HTTP validators, feed IDs/updates, fingerprints, and retrieval times form source-specific freshness. |
| `DOC-010`, `EVAL-0073` | G3, G4; sections 5.5, 7.4, 7.6 | Community search is opt-in, bounded, investigative, and cannot bypass landing-source policy. |
| `DOC-011`, `EVID-002` | I1; section 8 | Discovery, acquisition, extraction, and application retain distinct provenance. |
| `SEC-001`, `AI-003` | I1, W1; sections 5, 8 | Retrieved text is untrusted; queries/provider payloads are minimized and grant no authority. |
| `OPS-001` | Sections 6–7 | Each source declares local/cached/live/provider requirements and explicit unavailable states. |
| ADR-0011 | L1, L2; sections 6, 7.2 | LOOT masterlist/prelude remain separate immutable CC0 managed-data sources. |
| Taxonomy `0.1.0` | I1; section 5.6 | Documentation may support declared classifications but source type/search rank cannot imply observed impact or consequence. |
| RQ-010 / M0 Wave D | G1–G4, W1–W3, L1–L2; sections 11–13 | Supplies a bounded M1 registry and leaves broader source/provider expansion explicit. |

## 15. Validation

- Re-read the report against RQ-010, the accepted M0 Wave D scope, and the
  research handoff requirements.
- Semantically reviewed discovery/acquisition/authority separation, the four
  permission axes, alternatives, uncertainty, and unsupported cases.
- Validated local Markdown targets and cited identifiers.
- Ran `git diff --check`.
- Inspected the final path-specific diff. This investigation authored only
  `RESEARCH-0026-non-nexus-source-governance.md`.
