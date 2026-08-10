# RESEARCH-0031 — LOOT freshness and source discovery

Status: Completed
Disposition: recommendation accepted by ADR-0014
Date: 2026-07-28  
Last reviewed: 2026-07-28  
Researcher: Codex agent  
Primary question: revised RQ-010 and the managed-data freshness portion of
RQ-005  
M0 wave: D follow-up  
Decision enabled: amendment of ADR-0011's refresh policy and revised
non-Nexus source-registry disposition

Subsequent disposition: The owner accepted configurable automatic LOOT
freshness maintenance, the 24-hour default, and the immutable-pair activation
mechanism in
[ADR-0014](../../architecture/decisions/ADR-0014-loot-managed-data-refresh.md).
Implementation and EVAL-0053 conformance remain pending.

## Executive answer

Infinium can keep LOOT's curated Skyrim SE data current without giving up
reproducibility:

- follow the moving `v0.29` heads only as discovery aliases for the currently
  accepted libloot `0.29.6` / metadata-syntax `0.29` compatibility line;
- use lightweight conditional branch checks after startup when the previous
  check is at least 24 hours old, then no more often than every 24 hours while
  the application remains open;
- download changed bytes by immutable commit, validate masterlist and prelude
  together with the pinned adapter, and atomically activate a new immutable
  pair manifest only after validation succeeds;
- keep the previous known-good pair, allow explicit refresh and offline reuse,
  and never change the pair bound to a running or historical analysis; and
- treat a new compatibility branch as an adapter-upgrade event, not as data
  that can be adopted automatically.

The checked upstream heads on 2026-07-28 are the same revisions already
identified by RESEARCH-0009:

- Skyrim SE masterlist `v0.29`:
  `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f`; and
- prelude `v0.29`:
  `ea316265c11b5c6e6f51d53deb34c4054f4c2349`.

GitHub's branch and raw-content interfaces returned usable `ETag` values and
`304 Not Modified` responses in bounded tests. The pair is only about 1.23
MiB, so a daily revision check and changed-content download are operationally
small. This does not eliminate the accepted parser, cache, rollback, or
EVAL-0053 qualification gates.

The user's requested automatic refresh changes the earlier manual-only policy
in ADR-0011 section 6.7 and conflicts with the present wording of SCOPE-004.
It therefore requires an explicit product-requirement/ADR amendment; this
report does not change either authority implicitly.

For other non-Nexus sources, keep the initial product narrow:

1. snapshot-bound local documentation;
2. the separately versioned LOOT masterlist/prelude pair;
3. Nexus interfaces covered by the revised Nexus investigation; and
4. opt-in governed web discovery when a provider adapter is accepted.

Mapped GitHub mod documentation remains a useful optional adapter, but it is
too rare to justify core M1 coverage or broad repository-search machinery.
General web search should discover leads, not grant acquisition permission or
evidence authority. Provider-specific search capabilities are evaluated in
the parallel OpenAI research.

## 1. Question and requirements

### 1.1 Research question

How should Infinium:

- keep LOOT's Skyrim SE masterlist and prelude reasonably current;
- preserve exact reproducibility when those moving sources change;
- select compatible data for the accepted libloot boundary;
- behave on startup, during long-running scans, while offline, and after a
  failed or invalid update; and
- support non-Nexus source discovery without overbuilding rare GitHub-hosted
  mod-documentation paths or confusing discovery with authority?

### 1.2 Governing requirements and decisions

- ADR-0011 accepts libloot `0.29.6` and immutable, pair-validated,
  atomically cached masterlist/prelude inputs.
- SNAP-001, SNAP-003, SNAP-005, SNAP-006, and ADR-0010 require exact run
  bindings, visible staleness, and honest replayability.
- DOC-006 makes applicable curated LOOT metadata authoritative within its
  stated scope.
- DOC-009 separates source refresh behavior from semantic age-acceptance.
- DOC-010 requires broader web search to be independently toggleable and
  source-registry governed.
- DOC-011 requires acquisition identity, revision, coverage, cost, and
  application provenance.
- OPS-001 requires explicit offline/cached/live behavior.
- AUTH-002 permits product-owned cache and history writes only within the
  approved boundary.
- SCOPE-004 currently requires independently run acquisition/refresh work to
  be manually initiated. The owner has now directed that LOOT managed-data
  refresh be attempted on startup and at a reasonable interval. That is a
  proposed explicit exception/amendment, not something research may reconcile
  silently.

## 2. Scope and non-scope

### In scope

- official LOOT, libloot, Skyrim SE masterlist, and prelude sources;
- compatibility-line selection and revision identity;
- read-only public GitHub revision/content checks;
- HTTP validators, rate behavior, staged validation, atomic activation,
  rollback, offline use, and scan binding;
- a concrete default startup/interval policy;
- the minimal non-Nexus source inventory;
- a provider-independent web-discovery boundary; and
- implications for ADR-0011, SCOPE-004, RQ-010, and the source registry.

### Out of scope

- selecting the application stack, database, worker transport, or credential
  store;
- implementing the updater or libloot adapter;
- executing LOOT or changing its application-owned data;
- changing the user's LOOT settings, masterlist, prelude, or userlist;
- rerunning the already accepted libloot semantic experiment when the upstream
  pair and accepted library revision have not changed;
- selecting or live-testing an LLM/search provider;
- designing broad GitHub repository, archive, release-asset, or code-search
  support;
- arbitrary website crawling or page scraping; and
- accepting the recommended requirement or ADR amendments.

## 3. Sources and exact identities

Sources were retrieved or reverified on 2026-07-28 unless stated otherwise.

| Source | Exact identity | Use |
|---|---|---|
| [LOOT repository](https://github.com/loot/loot/tree/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9) | Release `0.29.1`, commit `77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9`, published 2026-04-18 | Current released updater, source URL, branch, and file-revision behavior |
| [LOOT update task](https://github.com/loot/loot/blob/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9/src/gui/qt/tasks/update_masterlist_task.cpp) and [Qt helpers](https://github.com/loot/loot/blob/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9/src/gui/qt/helpers.cpp) | LOOT `0.29.1` | Establishes plain `GET`, direct file replacement, Git blob SHA-1 calculation, and date sidecar behavior |
| [LOOT game settings](https://github.com/loot/loot/blob/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9/src/gui/state/game/game_settings.h) | `DEFAULT_MASTERLIST_BRANCH = "v0.29"` | Current released compatibility branch |
| [LOOT masterlist versioning](https://loot.github.io/docs/contributing/masterlist-versioning/) and [release procedure](https://loot.github.io/docs/team/making-a-release/) | Current official documentation | Explains compatibility branches and creation of a new branch when metadata syntax changes |
| [libloot repository](https://github.com/loot/libloot/tree/136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1) | Release `0.29.6`, commit `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1`, published 2026-06-27 | Accepted semantic/parser identity |
| [libloot metadata-syntax changelog](https://github.com/loot/libloot/blob/136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1/docs/metadata/changelog.rst) | Syntax line `0.29`, dated 2026-02-04 | Confirms the current metadata-syntax compatibility line |
| [Skyrim SE masterlist](https://github.com/loot/skyrimse/tree/4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f) | Branch `v0.29`; commit `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f`; commit date 2026-07-23; repository ID `72236811`; CC0-1.0 | Current curated Skyrim SE data |
| [LOOT prelude](https://github.com/loot/prelude/tree/ea316265c11b5c6e6f51d53deb34c4054f4c2349) | Branch `v0.29`; commit `ea316265c11b5c6e6f51d53deb34c4054f4c2349`; commit date 2026-04-11; repository ID `395074183`; CC0-1.0 | Current shared curated definitions |
| [GitHub REST API versioning](https://docs.github.com/en/rest/about-the-rest-api/api-versions), [branch endpoint](https://docs.github.com/en/rest/branches/branches?apiVersion=2026-03-10), [best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api?apiVersion=2026-03-10), and [rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api?apiVersion=2026-03-10) | REST API version `2026-03-10` | Branch-head identity, conditional requests, backoff, and public rate behavior |
| [RESEARCH-0009](RESEARCH-0009-loot-integration-and-data-contract.md) and [ADR-0011](../../architecture/decisions/ADR-0011-loot-semantic-and-managed-data-boundary.md) | Accepted Wave B evidence and decision | Existing libloot/data boundary, pair validation, atomic cache, and replay contract |
| [RESEARCH-0026](RESEARCH-0026-non-nexus-source-governance.md) | Proposed Wave D evidence | Existing discovery/acquisition/authority split and non-Nexus source templates |

The latest LOOT and libloot repository `master` heads observed were
`66701b071f8608d05ffdb7d1547f6ead69f9317b` and
`c0b810e69f6c5d8b77e737c43c9d9948b56aa419`, both dated 2026-07-24. They are
moving development references only and are not selected product dependencies.
The LOOT development head still declares `v0.29` as its default compatibility
branch.

## 4. Bounded read-only checks

### 4.1 Current branch and content identity

Public GitHub REST `GET` requests using API version `2026-03-10` resolved:

| Input | Commit | Git blob SHA-1 | Exact bytes | SHA-256 |
|---|---|---|---:|---|
| `loot/skyrimse` `v0.29` `masterlist.yaml` | `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f` | `775bdd12a48662b749b936a7ae77c951c9bc014e` | 1,116,555 | `68ccc51e800e294fe8e5fcf93c1cbbea0c3326dffb29aae67623d486fed6f02d` |
| `loot/prelude` `v0.29` `prelude.yaml` | `ea316265c11b5c6e6f51d53deb34c4054f4c2349` | `4988f2a853a15aa3f14bd6713d794f256b1c738b` | 173,300 | `2c95fc5f5088597d9cc85bbf341d867c2f7e2740ddae2c6329942cf6b56adb15` |

Both repositories report CC0-1.0. Their `LICENSE` files currently share Git
blob SHA-1 `0e259d42c996742e9e3cba14c677129b2c1b6311`.

The combined payload is 1,289,855 bytes, about 1.23 MiB. No file was retained
in the repository.

### 4.2 Conditional requests

The `loot/skyrimse` branch endpoint returned:

- `200 OK`;
- the exact branch-head commit above;
- a weak `ETag`;
- public rate-limit headers reporting a 60-request hourly limit; and
- `304 Not Modified` when the same URL was requested with `If-None-Match`.

The immutable raw-content endpoints also returned weak `ETag` values and
`304 Not Modified` to matching conditional requests. GitHub documents that a
properly authenticated conditional `304` does not consume the primary rate
limit. Infinium does not need GitHub authentication for the proposed two-repo
daily public check; anonymous requests may still consume the 60-per-hour
public allowance and must respect returned rate/reset/retry headers.

### 4.3 Observed update frequency

A bounded sample of the latest 100 commits on each `v0.29` branch showed:

- Skyrim SE masterlist: 6 commits in the preceding 30 days and 10 in the
  preceding 90 days; the 100-commit sample spans 2026-02-22 through
  2026-07-23.
- Prelude: no commit in the preceding 90 days; the newest commit is
  2026-04-11 and the 100-commit sample reaches back to 2024-07-05.

This is only a recent activity sample, not a service-level promise. It supports
a daily check as conservative and inexpensive; it does not prove that updates
will never be urgent or bursty.

### 4.4 LOOT application updater behavior

LOOT `0.29.1`:

- retrieves its configured prelude and masterlist sources with ordinary HTTP
  `GET` operations;
- writes changed response data directly to the destination;
- calculates a Git-compatible blob SHA-1;
- records that hash and a date-only update timestamp in an adjacent
  `*.metadata.toml` file; and
- can update before sorting when that application setting is enabled.

The checked update task does not itself provide Infinium's required immutable
commit identity, pair transaction, staged parse, content-addressed history, or
atomic pair activation. LOOT's behavior is appropriate evidence about its own
application, but it is not a cache implementation to copy wholesale.

No LOOT executable was run, no application-owned file was read or changed, and
no MO2/game/profile state was accessed.

## 5. Findings

### 5.1 “Latest” must mean latest compatible, validated revision

The moving default branch cannot be a run identity. The correct meanings are:

- **compatibility line:** the branch named in an accepted Infinium support
  manifest for the pinned semantic adapter, currently `v0.29`;
- **latest discovered revision:** the current commit at that branch head;
- **latest validated pair:** the newest discovered masterlist/prelude
  combination that passed exact transport, identity, integrity, parser, and
  pair-validation checks; and
- **run input:** one immutable validated pair manifest selected at run start.

These meanings prevent two unsafe interpretations:

1. following a newly created `v0.30` branch before the pinned adapter supports
   syntax `0.30`; and
2. replaying a historical run against whatever bytes happen to occupy
   `v0.29` later.

The installed user's LOOT version does not select Infinium's parser line.
Infinium's accepted libloot/adapter support manifest does. A future libloot or
metadata-syntax upgrade requires its own compatibility and conformance review.

### 5.2 The active unit is a pair manifest, not two mutable files

Masterlist and prelude are separate repositories with no cross-repository
transaction marker. Branch compatibility is necessary but does not prove that
an arbitrary partially downloaded combination is valid.

The smallest safe active record is:

```text
loot_managed_data_pair
  compatibility_manifest_id
  libloot_version/commit
  adapter/parser version
  masterlist repository id/ref/commit/blob/sha256/size/url
  prelude repository id/ref/commit/blob/sha256/size/url
  retrieval attempts, validators, status, and time
  pair-validation result and validator version
  activation time
  superseded pair id?
```

An unchanged component may be reused from the content-addressed cache, but a
changed component still creates and validates a new pair manifest.

### 5.3 Recommended refresh lifecycle

#### Default schedule

1. After the application becomes usable, schedule a nonblocking refresh check
   only when the last completed check is at least 24 hours old.
2. While the application stays open, perform no more than one scheduled check
   per 24-hour window.
3. Retain an explicit **Check now / Refresh now** action.
4. Provide an automatic-refresh toggle and visible last-check, last-success,
   active-revision, and failure/staleness state.
5. Do not make a profile change, scan completion, or result view an additional
   refresh trigger.

The 24-hour value is an initial policy default, not an evaluation-derived
universal constant. It should be versioned/configurable without changing the
immutable identity of work already performed.

#### Network and validation sequence

1. Read the accepted compatibility manifest.
2. Conditionally resolve both configured branch heads through stable,
   versioned GitHub REST operations.
3. If neither head changed, record a successful no-change check without
   creating a new pair.
4. Fetch changed content through commit-qualified URLs. Reuse an unchanged
   cached component only after verifying its identity.
5. Verify repository/ref/commit/blob identity, exact byte size, SHA-256,
   response and redirect policy, and licence expectation.
6. Stage both immutable components in a product-owned location.
7. In the failure-isolated adapter, parse the masterlist with that prelude and
   execute the accepted compatibility/conformance checks.
8. Create a new immutable pair manifest.
9. Atomically change one product-owned active-pair pointer/record.
10. Retain the prior known-good pair and all pair inputs referenced by retained
    runs.

No half-updated pair becomes visible.

#### Failure, rollback, and offline behavior

- Transport, identity, integrity, parse, or compatibility failure leaves the
  active pair unchanged.
- Respect `Retry-After` and rate-limit reset headers. Otherwise use bounded
  exponential backoff and stop retrying after a declared attempt limit; do not
  retry on every navigation or scan stage.
- Startup and the main UI remain usable while a refresh fails or is pending.
- A scan may start against the current validated pair when its configured
  freshness policy permits that age. The pre-run view must show that choice.
- If no validated pair exists, or the selected policy rejects the cached
  pair's age, LOOT-backed capability is unavailable/incomplete rather than
  silently reading LOOT's mutable application data.
- Rollback means selecting a retained known-good pair for later runs. It never
  mutates a historical run or overwrites the failed candidate as though it had
  succeeded.

### 5.4 Refresh never mutates an active scan

A scan resolves the active pair to an exact pair-manifest ID during run
startup. A refresh that finishes afterward:

- may become the default for a later run;
- may mark the older run's external evidence stale for current presentation
  under its freshness policy; but
- cannot replace inputs, findings, coverage, or readiness inside the active or
  retained run.

If a refresh completes between pre-run review and dispatch, Infinium must
either bind the pair shown during review or visibly recompute the affected
review before using the new pair. It must not substitute the new revision
silently.

### 5.5 Automatic maintenance is a narrow product-policy exception

ADR-0011 currently says refresh is manual. SCOPE-004 says independently run
documentation acquisition/extraction is manually initiated through M4.
Startup/interval managed-data refresh is network acquisition even when it does
not trigger analysis or LLM use.

The owner's direction supports a narrow exception:

> Infinium may perform a configurable, nonblocking, no-LLM maintenance check
> and refresh of the accepted LOOT managed-data sources on startup/interval.
> It does not initiate a scan, create findings, consume a user's model budget,
> follow arbitrary sources, or change a running run's inputs.

The amendment should not generalize to unattended Nexus collection, general
documentation refresh, web search, LLM work, or profile-triggered analysis.

### 5.6 Source discovery, acquisition, extraction, and authority remain distinct

The minimum pipeline remains:

```text
query/context
  -> provider search result or known link        (discovery)
  -> active source-row and operation decision    (admission)
  -> exact source/revision retrieval              (acquisition)
  -> cited claim proposal and validation          (extraction)
  -> local deterministic applicability            (application)
  -> hypothesis/finding threshold                 (analysis)
```

A search result's URL, title, snippet, rank, provider-generated summary, or
model citation is not by itself an author-maintained source claim. If the
landing source cannot be acquired through an accepted source operation, the
result remains an investigative lead with explicit provenance and coverage
limits.

Provider-hosted search may fetch or quote source material on Infinium's behalf.
That is still a provider attempt whose query, filters, returned source
identities, excerpt boundaries where available, model/tool identity, and
retention/cost behavior must be recorded. It does not bypass the source
registry or upgrade community material.

### 5.7 Minimal non-Nexus source posture

The proposed initial set is:

| Source | Initial role | Priority |
|---|---|---|
| Local/in-archive installed documentation | Snapshot-bound documentation with qualified author/version provenance | Core |
| LOOT Skyrim SE masterlist and prelude | Curated, separately versioned managed data through ADR-0011 | Core when LOOT-backed coverage is claimed |
| General web-search provider | Opt-in discovery; approved-domain mode first, broader community mode separately toggleable; results remain lead-only until admitted/acquired/applied | Useful after provider/source ADR |
| Positively mapped official GitHub project docs | Bounded exact-ref documentation/release reads | Optional/later |
| Mapped GitHub issues/comments | Investigative only | Optional/later, non-core |
| Per-author explicit site/API/feed | Host/interface-specific source row | Add only when demonstrated useful |

The LOOT repositories' GitHub transport is core because it supplies accepted
managed data. That does not justify making GitHub-hosted mod documentation a
core product lane.

### 5.8 Provider-independent search contract

The host-side record should be able to preserve, when available:

```text
search_operation_id
manually_initiated_parent_operation
provider/adapter/tool/model identity
query and query-fingerprint
query-purpose and source-mode
allowed-domain/source filters
locale/region/safe-search context
result/citation URL, title, snippet, rank, and published/updated time
provider request/result identifiers
provider-hosted fetch/quoted-span metadata
retrieval time
usage/cost/cancellation/retention receipt
registry-admission result
downstream acquisition/application links
```

This is a persistence/provenance shape, not a lowest-common-denominator
feature cap. A selected provider may expose richer search, citation, or
hosted-fetch capabilities. Unsupported fields remain absent and capability
gaps remain explicit.

For initial search behavior:

- search approved primary/technical domains first;
- make broader community discovery separately opt-in;
- minimize query context to the relevant mod identities, versions, plugins,
  symptoms, or interaction;
- never submit the whole modlist merely because the provider can search;
- do not automatically open every result or recursively follow links;
- cap results, calls, time, and cost per candidate/investigation;
- deduplicate and canonicalize results locally;
- preserve search-provider output separately from source evidence; and
- apply the existing authoritative versus investigative source rules after
  acquisition.

OpenAI-specific tool support, source/citation behavior, and adapter selection
belong to the parallel provider report.

## 6. Alternatives

| Alternative | Benefit | Disposition |
|---|---|---|
| Reuse the user's LOOT-managed files | Simple and often current | Reject as default: mutable application-owned state lacks Infinium pair transactions and reproducible acquisition identity; retain only as an explicit snapshotted custom-source mode if later accepted |
| Invoke LOOT to update its data | Delegates update behavior | Reject: current LOOT is not the accepted headless boundary and its application lifecycle/write behavior is unnecessary |
| Fetch moving raw branch URLs only | Very simple, no GitHub REST call | Reject for run provenance: body hashes identify bytes but do not establish the exact repository commit; conditional raw reads alone do not select the compatibility line safely |
| Clone or pull both Git repositories | Strong Git history | Reject as default: unnecessary Git dependency, more disk/state/error surface, and no cross-repository atomicity |
| Check on every startup regardless of age | Maximally eager | Reject: repeated launches create needless polling and rate use |
| Check weekly | Very low network use | Reject as default: the active Skyrim SE masterlist changed six times in the observed 30-day sample; daily revision checks are still inexpensive |
| Adopt any newer default branch automatically | Appears most current | Reject: a new branch may require unsupported metadata syntax and parser semantics |
| Block application startup until refresh succeeds | Ensures a check before use | Reject: destroys offline/failure usability and is unnecessary because validated cached pairs are explicit |
| Make GitHub mod-documentation search core | Direct official-source path for some projects | Defer: author-mapped repositories are uncommon and do not justify core complexity |
| Treat search snippets/model answers as source evidence | Avoids landing acquisition | Reject: revision, authorship, context, and exact wording are not established adequately |
| Disable all general search | Simplest policy | Not preferred: governed opt-in discovery can surface obscure documented interactions that are central to the product's value |

## 7. Uncertainty, gaps, and stop conditions

1. EVAL-0053 and EVAL-0046 have not run against a production adapter/cache.
   This report does not claim LOOT-backed implementation coverage.
2. The exact stack-specific atomic-file/database mechanism remains RQ-013,
   RQ-015, and RQ-017 work.
3. The current masterlist/prelude pair was not reparsed because both upstream
   commits and the accepted libloot revision are unchanged from the prior
   investigation. Production qualification must parse every candidate pair
   before activation.
4. No official cross-repository pair manifest exists. Infinium therefore
   proves compatibility by its accepted branch mapping and pair validation,
   not by assuming synchronized commit times.
5. GitHub anonymous rate limits and secondary limits may change. Follow
   returned headers and official current API guidance; do not add a user
   GitHub credential merely for two daily public revision checks without a
   separate need/security decision.
6. The 24-hour default is a reasoned initial policy based on current source
   activity and low request/payload cost. It is not yet user-calibrated.
7. A future metadata-syntax branch, repository move, licence change, API
   removal, persistent `401/403/429`, branch rewind, missing immutable commit,
   invalid parse, or unexpected redirect is a stop/reverification trigger.
8. Search-provider acquisition/quotation, query retention, citations, result
   rights, cost, and privacy vary. The provider-specific report and later ADR
   must qualify the selected adapter.
9. Arbitrary public HTML, unsupported crawling, search-cache copying, and
   recursive browsing remain outside this recommendation.

## 8. Recommendation

### 8.1 LOOT managed data

Amend ADR-0011 and the affected product wording to accept:

1. **Latest-compatible policy:** current validated heads of the explicitly
   supported `v0.29` masterlist/prelude line, not arbitrary newest branches.
2. **Automatic maintenance:** configurable nonblocking startup eligibility
   check after 24 hours, then at most daily while open, plus manual refresh.
3. **Immutable acquisition:** versioned GitHub REST branch resolution,
   commit-qualified content, exact repository/commit/blob/SHA-256 identity.
4. **Pair transaction:** stage, validate with the pinned adapter, and
   atomically activate one pair manifest.
5. **Known-good rollback:** never replace the active pair on partial or failed
   validation; retain prior pairs and every pair referenced by retained runs.
6. **Run isolation:** bind once at run start; background refresh affects only
   future runs and current-view freshness.
7. **Offline honesty:** use an explicitly aged cached pair when policy allows,
   otherwise expose the LOOT-backed coverage gap.
8. **Compatibility upgrades:** treat a new metadata branch/libloot line as a
   researched adapter upgrade, not routine source refresh.

The automatic-maintenance exception should be narrow and explicit so it does
not weaken SCOPE-004's manual initiation of scans, general documentation
acquisition, web search, or LLM work.

### 8.2 Non-Nexus sources and web search

Revise RQ-010's proposed disposition to:

> Approve local installed documentation and LOOT managed data as the minimal
> non-Nexus core. Keep mapped official GitHub project documentation as an
> optional, later bounded adapter rather than M1 core coverage. Permit an
> opt-in governed search-provider discovery lane once its provider/source
> contract is accepted. Search results remain investigative discovery records;
> landing-source acquisition, evidence authority, and local applicability are
> separate decisions.

The provider-neutral record should preserve common provenance but must not
prevent the initial OpenAI adapter from using richer supported search/citation
features.

## 9. Exact downstream decisions and evaluation work enabled

### Product amendment

Amend SCOPE-004/DOC-009 narrowly to permit configured automatic maintenance of
accepted LOOT managed data without permitting automatic analysis, general
source collection, search, or model work.

### ADR-0011 amendment

Replace its manual-only refresh clause with the latest-compatible,
startup/daily, pair-transaction, run-isolation contract in section 8.1.
Retain every existing libloot, authority-separation, no-apply, worker, cache,
and evaluation gate.

### Source registry

- Promote the LOOT rows only when the managed-data adapter qualification is
  accepted.
- Record branch resolution, pair manifest, validators, and automatic schedule
  explicitly.
- Reclassify mapped GitHub mod documentation as optional/later.
- Replace the disabled generic search template only after the selected
  provider's exact discovery/fetch/citation behavior is qualified.

### Evaluation inputs

Extend EVAL-0053 with:

- `200` changed and `304` unchanged branch checks;
- one changed component and two changed components;
- branch rewind and missing immutable commit;
- corrupt/truncated content and valid-individually/invalid-as-pair input;
- crash between staging and activation;
- activation rollback and retained prior pair;
- refresh completing before versus after run binding;
- offline first run, offline cached run, and freshness-policy rejection;
- new unsupported compatibility branch; and
- exact replay using a retained historical pair after the branch advances.

Add a search-discovery contract case proving:

- approved-domain and opt-in community modes remain distinct;
- a result/snippet cannot grant source authority or trigger recursive fetch;
- unavailable landing acquisition stays a lead/gap;
- a cited acquired source retains exact source/revision/applicability
  provenance; and
- provider-specific richer fields do not enter local-state or operation
  authority.

## 10. Validation performed

- Read the authoritative product and architecture documents, ADR-0011,
  RESEARCH-0009, RESEARCH-0026, RESEARCH-0029, the source registry, and the
  research procedure.
- Reverified exact current LOOT/libloot release and development identities,
  compatibility branch, masterlist/prelude heads, licence, blob IDs, byte
  sizes, and SHA-256 values.
- Inspected the pinned LOOT updater, helper, settings, and metadata-version
  sources.
- Exercised bounded public branch and immutable-content reads plus conditional
  `ETag` requests; retained no upstream body in the repository.
- Sampled recent official branch commit activity only to inform, not prove,
  the initial check interval.
- Did not run LOOT, perform a write/mutation API call, use a credential, access
  a real profile, invoke a search/LLM provider, or retain a third-party
  payload.
- Wrote only this investigation report.
