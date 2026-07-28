# ADR-0005: Proceed with supported Nexus API analysis under a bounded-use interpretation

Status: Accepted
Date: 2026-07-25
Accepted: 2026-07-25
Accepted by: Project owner
Last reviewed: 2026-07-28
Supersedes: None
Superseded by: ADR-0012 for API-interface eligibility and selection only

Subsequent decision:

- 2026-07-28 —
  [ADR-0012](ADR-0012-nexus-latest-capable-api-routing.md) records the owner's
  API-wide development-risk direction and selects latest-capable v3, v2
  GraphQL, then v1 per-content routing. Its narrower interface-eligibility
  decision supersedes the documented-supported-interface-only provisions
  below. ADR-0005's bounded purpose, no-page/no-bypass/no-rehost/no-training,
  private-retention, provider-transmission, public-registration, and reversal
  constraints remain accepted.

## Context

Infinium's primary product value includes retrieving author-maintained
documentation for the mods in a user's selected MO2 profile, extracting
requirements and compatibility claims, and applying those claims to exact local
state. Nexus Mods is expected to be an important source for that documentation.

The current Nexus Terms of Service expressly include APIs. Section 10 permits
personal use and use for creating and sharing enhancements to video games and
existing video game modifications, while limiting downloading, copying, and
commercial use outside those purposes. Section 11 broadly prohibits text/data
mining, automated access, and automated analytical techniques over Nexus data.
The older API Acceptable Use Policy simultaneously provides an open API for
third-party modding applications, tolerates personal keys for testing and
personal use, and defines registration and operational rules for public
applications.

[RESEARCH-0001](../../research/investigations/RESEARCH-0001-nexus-access-policy.md)
found no express reconciliation between those provisions and conservatively
recommended stopping automated Nexus content analysis pending written
clarification. The project owner has reviewed that ambiguity and made the
product-risk decision below. This is a project interpretation and operational
decision, not legal advice or a representation that Nexus has approved
Infinium.

## Decision drivers

- Infinium is a complementary local diagnostic tool, not a substitute Nexus
  catalog, download service, mod host, or public content mirror.
- The analysis is performed for a registered user's selected installed mods and
  is intended to enhance the safe use and combination of existing game
  modifications.
- Users must still obtain and install mods through Nexus or another lawful
  source; Infinium does not redistribute mod files.
- Retrieved evidence and derived findings are product-private by default.
- Supported Nexus APIs and the API AUP exist specifically to enable
  third-party modding applications.
- Waiting for policy correspondence before any capability research would block
  a central product path despite a plausible permitted-purpose interpretation.
- The project can constrain acquisition, retention, model transmission, and
  export much more narrowly than a crawler, dataset harvester, model-training
  corpus, rehost, or competing public service.

## Considered options

### Option A — Stop all Nexus content acquisition pending written permission

This is the lowest policy-risk option and was the original RESEARCH-0001
recommendation. It would prevent realistic RQ-008 and Wave D experiments and
could remove an important evidence source from M1 for an indefinite period.

### Option B — Permit conventional metadata only, but prohibit semantic analysis

This would enable identity, version, file, and update queries while excluding
the documentation analysis that differentiates Infinium. It does not resolve
the project's core product need.

### Option C — Proceed with bounded supported-API retrieval and value-added analysis

This treats Infinium as a permitted personal/mod-enhancement use and a
community-benefiting third-party API application. It accepts that section 11 is
worded broadly enough to create a contrary interpretation, while constraining
the product away from the competitive, bulk-extraction, redistribution, and
model-training uses most likely targeted by that provision.

Option C is selected.

## Decision

Infinium will proceed on the working assumption that Nexus content made
available through a supported API may be retrieved and transformed for
Infinium's local mod-analysis and diagnostic purposes.

This permission assumption applies only when all of the following are true:

1. Acquisition uses a documented, supported Nexus API or another interface
   Nexus expressly approves for the operation.
2. The request is initiated by the user as part of an explicit acquisition or
   analysis operation and is bounded to relevant mods, normally those mapped to
   the selected MO2 profile.
3. Development/personal use follows the personal-key rules, and a
   public-facing release completes the then-current Nexus application
   registration or approval process.
4. Requests identify Infinium accurately, respect authentication, quota,
   backoff, revocation, age-filtering, and content-visibility requirements, and
   never impersonate another application.
5. Retrieved content is used to extract evidence, diagnose the user's modlist,
   explain findings, and propose validation or resolution—not to build a
   general Nexus mirror, replacement search/catalog service, public corpus, or
   competing download service.
6. Infinium does not train, fine-tune, or validate a model on Nexus content.
7. Exact source content and derived evidence are retained locally only to the
   extent needed for provenance, audit, replay, refresh, and the user's private
   diagnostic history. Users receive deletion and refresh controls.
8. Cloud-model transmission, when enabled, is user-selected, minimized to the
   evidence needed for the operation, excludes credentials, and is disclosed
   separately from local processing. A local-model path may avoid that
   transmission but is not required by this decision.
9. Raw Nexus text, media, files, or a reusable source corpus are not published,
   resold, or included in externally shareable exports by default. Exports use
   derived findings, attribution, permitted short citations, links, and
   omission/redaction rules defined by the eventual source/export policy.
10. Every retained item preserves source identity, retrieval time, applicable
    revision where available, and the policy/adapter version used.

This decision does **not** permit:

- HTML scraping or crawling;
- browser automation over Nexus pages;
- undocumented/private endpoint discovery or use;
- authentication or access-control bypass;
- bulk acquisition unrelated to the user's relevant mod set;
- background acquisition not initiated by the user;
- rehosting or constructing a public substitute for Nexus;
- model training, fine-tuning, or validation on Nexus data;
- treating private retention as permission to redistribute; or
- assuming that unsupported surfaces such as posts, comments, articles, or bug
  reports may be scraped when no supported API exposes them.

The project will still request written confirmation from Nexus. Development is
not blocked while that request is pending.

### Accepted retention clarification

The project owner's accepted RQ-031 disposition clarifies item 7 above.
"Only to the extent needed" includes retaining permitted private source
material long enough to complete useful extraction, deterministic and LLM
analysis, claim/case/finding synthesis, user-facing prose, provenance, audit,
replay, refresh, and private diagnostic history. Metadata-first retention is a
durable-minimization policy after configured dependent work is materialized;
it does not require premature deletion that would make the product ineffective
or its conclusions unauditable.

## Consequences

### Positive

- RQ-008 and later Wave D experiments may test supported Nexus APIs using
  bounded authenticated requests.
- The primary documentation-intelligence use case remains viable.
- Product behavior remains complementary to Nexus and tied to actual user
  modding activity.
- The decision is explicit, reviewable, and reversible rather than hidden in
  implementation assumptions.

### Negative

- Nexus may interpret section 11 more broadly and reject or condition the use.
- The adapter, cache, and provider boundary require more policy-aware controls.
- Some desired content may remain unavailable because Nexus exposes no
  supported read interface.
- Public release depends on the applicable Nexus registration/approval process
  and may require changes requested by Nexus.

### Risks and mitigations

- **Contrary Nexus interpretation:** preserve the response as dated evidence,
  stop affected acquisition, and supersede this ADR.
- **Terms or API-policy change:** version policy evidence and use a
  policy-expiry/reverification gate before authenticated acquisition.
- **Excessive or competitor-like collection:** bound requests to relevant mods,
  cache responsibly, use conditional refresh, and prohibit public corpus or
  rehosting behavior.
- **Cloud-provider disclosure:** make remote transmission explicit, minimize
  excerpts, record provider/retention behavior, and permit users to disable the
  remote stage.
- **Source redistribution:** keep exact source content private by default and
  independently authorize every external export class.
- **Unsupported content pressure:** emit a coverage gap rather than scrape or
  automate the website.

## Requirements affected

- DOC-001, DOC-002, DOC-006, DOC-008, DOC-009, and DOC-011
- SCOPE-004
- EVID-002 and EVID-003
- AI-003
- SEC-001, SEC-002, and SEC-004
- OPS-001 through OPS-003

## Validation and reversal triggers

The decision must be reviewed when:

- Nexus replies to the project's clarification request;
- Nexus changes its Terms, API AUP, registration rules, or relevant API
  contract;
- the intended data flow expands beyond bounded user-relevant acquisition;
- Infinium proposes server-side shared storage, a community compatibility
  database, public source-text exports, or model training;
- a selected cloud provider introduces materially different data use or
  retention; or
- Nexus rejects registration, restricts the application, or asks the project
  to stop or modify the behavior.

RQ-008 must verify exact supported interfaces and content coverage. RQ-031 has
since established the accepted M0 retention/replay/export policy, including
useful-analysis private retention. RQ-013 and RQ-032 must still select and
validate the storage, provider, sanitization, credential, deletion, and export
mechanisms. This ADR grants no unsupported interface and selects no
implementation stack.

## References

- [Nexus Terms of Service](https://help.nexusmods.com/article/18-terms-of-service),
  last updated 2026-05-12; retrieved 2026-07-25
- [Nexus API Acceptable Use Policy](https://help.nexusmods.com/article/114-api-acceptable-use-policy),
  last updated 2020-12-01; retrieved 2026-07-25
- [Nexus API rate-limit guidance](https://help.nexusmods.com/article/105-i-have-reached-a-daily-or-hourly-limit-api-requests-have-been-consumed-rate-limit-exceeded-what-does-this-mean),
  last updated 2026-06-03; retrieved 2026-07-25
- [RESEARCH-0001](../../research/investigations/RESEARCH-0001-nexus-access-policy.md)
- [RESEARCH-0003](../../research/investigations/RESEARCH-0003-retention-replay-export-policy.md)
- [RESEARCH-0004](../../research/investigations/RESEARCH-0004-wave-a-policy-and-evidence-handling-integration.md)
