# ADR-0012: Nexus latest-capable API routing and development-risk posture

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: ADR-0005's API-interface eligibility and selection clauses only  
Superseded by: None

## Context

ADR-0005 accepted bounded Nexus API retrieval and diagnostic transformation
under an explicit project-owner risk decision, but limited eligible operations
to documented supported interfaces. RESEARCH-0025 consequently treated Nexus's
live v2 GraphQL interface as blocked pending separate proof of express
third-party approval.

The owner has now directed that unresolved policy interpretation must not block
development use, introspection, or testing of Nexus-provided APIs for
Infinium's diagnostic purpose. The owner also selected a latest-interface
preference with older-interface fallback only when newer interfaces do not
provide required content.

[RESEARCH-0030](../../research/investigations/RESEARCH-0030-nexus-latest-interface-qualification.md)
authenticated and qualified the current v3, v2 GraphQL, and v1 capabilities.

## Decision drivers

- Nexus documentation is central to Infinium's product value.
- No one Nexus API generation exposes all required content.
- Current API capability and historical-run reproducibility must coexist.
- API-policy ambiguity is an accepted development risk, not a recurring
  technical blocker.
- Page scraping, access bypass, public rehosting, and unrelated bulk
  collection remain unnecessary.

## Decision

### Executive development-risk direction

During development, Infinium may use, introspect, and test any Nexus-provided
read API needed for its local modlist-diagnostic purpose, including the live v2
GraphQL interface. A lack of separate public schema publication or express
GraphQL approval is not a project blocker.

This decision does not authorize:

- HTML scraping, crawling, or browser-session automation;
- traffic interception or access-control/authentication bypass;
- unrelated bulk collection;
- mutations, endorsements, downloads, or account changes;
- public source-text rehosting or a substitute Nexus catalog;
- model training, fine-tuning, or validation on Nexus content; or
- secret or account-data disclosure.

ADR-0005's bounded-purpose, relevant-mod-set, private-retention,
provider-transmission, attribution, export, registration, and reversal
constraints remain operative except where they depended on the narrower
interface-eligibility clause.

### Latest-capable routing

For each requested content group, a new acquisition shall:

1. prefer the current v3 API when v3 supplies the required content;
2. use v2 GraphQL when v3 lacks the required content;
3. use v1 only when neither newer route supplies the need, or as an explicitly
   disclosed degraded fallback; and
4. record the resolved interface, contract/schema identity, query/operation,
   entity identity, retrieval time, revision signals, permitted projected
   payload fingerprint, and coverage/error outcome.

The currently qualified routing is:

- v3 for mod/file identity, batch summaries, exact file-version identity,
  upload time, and typed file-version/DLC dependencies;
- v2 GraphQL for long descriptions, mod version/update time, page-level
  requirements and reverse dependents, file descriptions, and file-level
  changelogs; and
- v1 for general mod changelogs and as an explicit fallback for descriptive
  content.

Articles, mod-linked posts/comments, author or sticky posts, and mod-page bug
reports remain unsupported API coverage gaps. Generic GraphQL comments,
collection bug reports, web-search snippets, and page access are not
substitutes.

Routing is resolved and frozen for an acquisition run. A resumed or historical
run does not silently adopt a newer route or live schema. Required GraphQL
shapes use Infinium-owned minimal versioned queries and capability
qualification; first-party client source alone is not live-schema conformance.

## Consequences

### Positive

- Infinium can use the richest current Nexus source for each required content
  type.
- The former GraphQL-policy blocker is removed explicitly.
- Interface drift and fallbacks remain observable and reproducible.
- Unsupported page-only content stays an honest gap.

### Negative

- Three interface generations require separate adapters and failure
  normalization.
- v3 reads are currently Experimental and GraphQL has no published versioned
  public schema.
- The accepted policy-risk posture may need reversal if Nexus responds
  negatively.

## Validation and reversal triggers

Before production support is claimed:

- required routes and schema shapes must pass EVAL-0068 and EVAL-0083;
- credentials and untrusted markup must pass the later RQ-018/RQ-032
  boundaries;
- transport status, GraphQL errors, visibility, absence, and empty results
  must remain distinct; and
- quota/headroom must be reported only where the interface exposes reliable
  data.

Re-review this ADR when the v3 contract, GraphQL schema fingerprint, or
first-party client changes materially; when a required call fails schema
qualification; when the product expands beyond bounded local diagnosis; or
when Nexus responds to the owner's inquiry, restricts the application, or asks
the project to stop or change the behavior.

## Requirements affected

- DOC-001 through DOC-006
- DOC-008 through DOC-011
- EVID-002 and EVID-003
- SNAP-006
- SEC-001 and SEC-002
- OPS-001 through OPS-003

## References

- [ADR-0005](ADR-0005-nexus-supported-api-analysis.md)
- [RESEARCH-0030](../../research/investigations/RESEARCH-0030-nexus-latest-interface-qualification.md)
- [RESEARCH-0033](../../research/investigations/RESEARCH-0033-wave-d-revision-integration.md)
