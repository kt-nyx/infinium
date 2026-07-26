# ADR-0011: LOOT semantic and managed-data boundary

Status: Accepted  
Date: 2026-07-25  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Infinium must reuse LOOT's mature curated metadata and sorting/condition
semantics without reimplementing them or allowing a helper to apply changes to
the user's setup. RESEARCH-0009 found that LOOT application versions
`0.28.0` and `0.29.1` do not expose a supported headless, structured,
non-applying analysis interface adequate for Infinium. The public libloot API
provides the necessary semantic surface, subject to exact binding, worker,
data, and conformance qualification.

## Decision

1. Infinium shall not use the installed LOOT `0.28.0`/`0.29.1` application as
   its automated analysis boundary. GUI, clipboard/log automation,
   `--auto-sort`, running through MO2, and private installed DLL loading are
   rejected.
2. LOOT remains a user-installed, user-maintained application. Infinium may
   detect and validate it only for accepted configuration/userlist discovery
   or other explicitly qualified application-fidelity evidence; it shall not
   bundle, download, install, update, replace, or apply through LOOT.
3. libloot `0.29.6`, source commit
   `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1`, is the accepted semantic core
   for a narrow bundled adapter when a milestone claims LOOT-backed coverage.
4. The adapter exposes only allowlisted read-and-compute operations. Applying
   load-order changes, writing metadata/userlist state, or reaching other
   mutating libloot APIs is forbidden.
5. The exact language binding, native payload, and failure-isolated worker
   transport remain coupled to the later accepted stack/process ADR and M1
   plan. They must preserve this semantic and write-authority boundary.
6. Curated masterlist/prelude data, private userlist/configuration, captured
   local state, direct libloot results, merged metadata, and
   Infinium-derived diagnostics remain separately typed authorities.
7. Masterlist and prelude acquisition uses compatible immutable revisions,
   validated integrity and parsing, atomic product-owned caching, explicit
   freshness/staleness, offline behavior, rollback, and complete provenance.
   Moving branch names are never replay identities.
8. Userlist and LOOT configuration are private selected-profile inputs. Their
   exact captured bytes/hashes and applicable condition context participate in
   snapshot and cache validity.
9. M1 may defer LOOT-backed analysis. If it does, the M1 plan must state the
   coverage gap; unrelated Infinium capabilities remain available.
10. A later LOOT application version may replace this boundary only if a new
    investigation proves a supported structured, non-mutating interface and a
    new or superseding ADR accepts it.

## Qualification gates

Before a milestone claims LOOT-backed coverage:

- the exact binding/native payload and transitive distribution obligations
  must be audited and locked;
- EVAL-0053 must prove deterministic structured behavior and authority
  separation across supported libloot/data revisions;
- EVAL-0046 must prove protected-state non-mutation, forbidden-operation
  unreachability, cancellation/failure containment, and path isolation;
- ADR-0008 and ADR-0010 must supply the exact effective-state projection and
  dependency validity; and
- direct libloot output must remain distinguishable from
  Infinium-derived conclusions.

## Consequences

- Infinium can obtain structured LOOT semantics without automating the GUI or
  applying user-state changes.
- A native library/binding/worker and managed-data lifecycle add packaging,
  security, and maintenance obligations.
- LOOT-backed coverage can be deferred without blocking the first bounded
  semantic proof.
- Users keep their own LOOT installation and configuration.

## Validation

- No allowed adapter path reaches a libloot set/write/apply operation.
- Changing masterlist, prelude, userlist, effective plugin state, conditions,
  adapter, binding, or library version changes the relevant input identity.
- Offline/stale states are explicit and never silently refresh or substitute
  moving data.
- Missing or unsupported LOOT application state does not block capabilities
  that use only qualified bundled libloot/data inputs.

## References

- [ADR-0001 — Evidence authority](ADR-0001-evidence-authority-boundary.md)
- [ADR-0003 — Read-only authority](ADR-0003-read-only-authority.md)
- [ADR-0006 — GPL and dependency boundary](ADR-0006-gpl-product-and-tool-dependency-boundary.md)
- [ADR-0008 — MO2 profile and effective state](ADR-0008-mo2-profile-effective-state-and-local-identity.md)
- [ADR-0010 — Snapshot invalidation](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [RESEARCH-0009 — LOOT integration](../../research/investigations/RESEARCH-0009-loot-integration-and-data-contract.md)
- [RESEARCH-0013 — Wave B integration](../../research/investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
