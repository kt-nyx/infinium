# ADR-0014: LOOT managed-data freshness and immutable pair activation

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: ADR-0011's managed-data refresh mechanics only  
Superseded by: None

## Context

ADR-0011 requires compatible immutable LOOT masterlist/prelude revisions,
validated parsing, atomic product-owned caching, explicit freshness, offline
behavior, rollback, and provenance. The owner has now directed Infinium to keep
this curated data current through nonblocking startup and periodic maintenance
rather than relying only on manual refresh.

RESEARCH-0031 verified the current LOOT `v0.29` compatibility line, immutable
masterlist/prelude identities, conditional HTTP behavior, recent update
frequency, and the limitations of copying LOOT's own direct-write updater.

## Decision drivers

- Curated LOOT metadata changes often enough that invisible stale data is
  undesirable.
- Startup must remain responsive and offline-capable.
- A refresh must never alter the inputs of a running or historical scan.
- Masterlist and prelude are a validated compatibility pair, not independent
  mutable files.
- New metadata syntax/libloot lines require adapter qualification.

## Decision

1. Infinium follows only the compatibility line declared by its accepted
   libloot/adapter support manifest, initially `v0.29`. A new compatibility
   branch is an adapter-upgrade event, not a routine data update.
2. After the application becomes usable, it performs a nonblocking conditional
   refresh check when the last completed check is at least 24 hours old. While
   open, it performs no more than one scheduled check per 24-hour window.
3. Users can disable automatic refresh and can invoke **Check now / Refresh
   now**. The UI exposes last check, last success, active revisions, age,
   failure, rollback, and offline state.
4. Moving branches are discovery aliases only. Changed bytes are fetched by
   immutable commit-qualified identity.
5. The product stages masterlist and prelude together, verifies repository,
   ref, commit, blob, SHA-256, byte size, redirects, and licence expectation,
   then parses and validates the pair through the failure-isolated pinned
   adapter.
6. Only a fully validated immutable pair manifest may be activated. Activation
   changes one product-owned pointer/record atomically; no half-updated pair is
   visible.
7. The prior known-good pair and every pair referenced by retained runs remain
   available subject to explicit retention/deletion policy.
8. An analysis run binds one active immutable pair at startup. A refresh
   finishing later affects only future runs and current-view freshness.
9. Refresh failure leaves the active pair unchanged. Offline scans may use the
   cached pair only when the selected freshness policy permits its age;
   otherwise LOOT-backed coverage is unavailable/incomplete.
10. This automatic maintenance exception applies only to accepted LOOT managed
    data. It does not authorize automatic scans, Nexus acquisition, general
    documentation collection, broader web search, or LLM work.

The 24-hour interval is an initial configurable/versioned default, not a
semantic property of historical runs.

## Preserved ADR-0011 boundaries

This decision does not change:

- pinned libloot `0.29.6` and its later version-advancement gate;
- the allowlisted read-and-compute/no-apply boundary;
- authority separation among curated data, userlist/configuration, local
  state, libloot results, and Infinium conclusions;
- worker/binding and packaging requirements; or
- EVAL-0053/EVAL-0046 qualification.

## Consequences

### Positive

- New scans normally use current compatible curated data.
- Historical runs remain reproducible.
- Failed or corrupt updates cannot displace known-good data.
- Startup and offline use remain available.

### Negative

- The product needs scheduled maintenance, pair manifests, content-addressed
  storage, atomic activation, rollback, and visible freshness state.
- GitHub branch/API behavior becomes a managed-data transport dependency.
- Retaining historical run inputs consumes additional disk.

## Validation

EVAL-0053 must cover at least changed/unchanged conditional checks, one- and
two-component changes, branch rewinds, missing commits, corrupt/truncated
content, pair incompatibility, crash during activation, rollback, refresh/run
binding races, offline first/cached runs, freshness rejection, unsupported
compatibility branches, and replay after branch advancement.

## Requirements affected

- SCOPE-004
- DOC-006, DOC-009, and DOC-011
- SNAP-006
- SCAN-003 through SCAN-006
- OPS-001 and OPS-002

## References

- [ADR-0011](ADR-0011-loot-semantic-and-managed-data-boundary.md)
- [RESEARCH-0031](../../research/investigations/RESEARCH-0031-loot-freshness-and-source-discovery.md)
- [RESEARCH-0033](../../research/investigations/RESEARCH-0033-wave-d-revision-integration.md)
