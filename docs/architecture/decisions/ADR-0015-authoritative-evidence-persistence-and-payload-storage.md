# ADR-0015: Authoritative evidence persistence and payload storage

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Infinium needs a durable local system of record for immutable snapshots, runs,
acquisitions, evidence, provenance, findings, cases, review history, reuse,
cost, deletion, and replay. Files or provider payloads alone cannot provide the
transactional joins, reverse dependencies, pagination, integrity constraints,
or explicit deletion impact required by the product.

RESEARCH-0036 found that a typed relational store plus a separately managed
large-payload store best fits those requirements. The same design must
implement ADR-0010's dependency-validity model and the accepted RQ-035
typed-index and causal-join contract without making one generic relationship
or JSON document the product schema.

## Decision drivers

- Immutable, inspectable evidence and run history must remain authoritative
  independently of mutable UI projections.
- Physical deduplication must not collapse source, acquisition, extraction,
  claim, application, or run identity.
- Provenance, dependencies, reuse, lineage, and deletion impact require typed
  traversal in both directions.
- The local single-user product should not require a separately administered
  database server.
- Large opaque bodies and traces must not make ordinary relational queries or
  backups unmanageable.
- Storage must support safe migrations, crash recovery, integrity checks,
  explicit retention, and replayability disclosure.

## Considered options

### SQLite plus a product-controlled content-addressed payload store

SQLite provides local transactional relational storage, indexes, referential
integrity, backup and integrity primitives, and simple desktop deployment. A
separate content-addressed store keeps large immutable bodies, traces,
checkpoints, and outputs outside ordinary relational rows while SQLite remains
the authority for their identities, ownership, retention, and availability.

Its principal costs are the single-writer constraint, coordinated
database/payload publication and backup, explicit WAL/checkpoint management,
and the need to pin a native SQLite line containing the documented WAL-reset
corruption fix.

### DuckDB

DuckDB is strong for columnar analytics and bulk queries. Its analytical focus
and concurrency model are a poorer fit for Infinium's many small durable
appends, referential constraints, checkpoint transitions, and interactive
point and reverse lookups. It may later be evaluated as a disposable
analytical or export adjunct, not as the M1 authority.

### PostgreSQL

PostgreSQL offers stronger multi-writer and server operations, but adds a
database service, cluster lifecycle, ports, credentials, installation,
upgrades, and a larger backup boundary. No accepted local M1 requirement
justifies that operational cost.

### Files, JSON, an append-only directory, or a graph database

Files remain appropriate for immutable payloads and exports, but not as the
sole transactional query and integrity model. A dedicated graph database adds
another runtime and consistency boundary without evidence that indexed typed
relational joins are insufficient.

## Decision

1. SQLite is Infinium's authoritative local relational system of record.
2. Infinium shall ship and assert an exact supported native SQLite version
   containing the documented WAL-reset corruption fix. An affected,
   unqualified, or unexpectedly substituted native build must fail closed for
   authoritative storage.
3. Exactly one product-owned storage-authority process owns access to the
   authoritative SQLite database and its coordinated payload store. Other
   components use application contracts rather than opening the store. The
   executable topology and transport are selected separately.
4. The authoritative schema uses typed normalized tables, stable opaque
   logical identities, immutable revisions, and explicit versioned domain
   fields. It shall not use a generic entity-attribute-value table, provider
   JSON, or one untyped relationship table as product truth.
5. Source entity, source revision, acquisition representation, extraction
   revision, admitted claim/evidence revision, and snapshot/run application
   are distinct logical identities even when their physical bytes are
   identical.
6. Large source bodies, opaque tool/provider responses, lengthy traces,
   checkpoint bundles, and outputs use a product-controlled immutable
   content-addressed payload store. SQLite owns their hashes, sizes, codecs,
   locations, logical owners, policies, availability, and retention state;
   payload files are not an independent source of truth.
7. Payload publication uses product-root staging, hash and size verification,
   atomic same-volume placement, and transactional registration. A reconciler
   detects and handles unreferenced staging files, orphaned objects, missing
   objects, and hash/size mismatches without inventing or silently rebinding
   evidence.
8. Durable provenance, dependency, support/contradiction, source-to-extraction-
   to-claim-to-application, reuse, lineage, causal-candidate, payload-
   ownership, and deletion-impact paths are typed and indexed in every needed
   traversal direction. Edge kinds and endpoint kinds are allowlisted, and
   domain-specific qualifications remain typed.
9. Historical facts, runs, revisions, applications, review events,
   availability changes, and deletion receipts are append-only. Current
   review, freshness, readiness, search, summary, and presentation state are
   rebuildable versioned projections or caches and are never the sole
   historical authority.
10. A stage or run may become complete only in a transaction that also commits
    all admitted outputs, provenance, dependency/reuse edges, coverage, gaps,
    payload references, and accounting ownership required for that completion.
11. Reuse preserves the original producing identity and records a consuming
    reuse edge plus a complete dependency-validity proof under ADR-0010.
    Replayability remains explicitly classified as clean recomputation,
    boundary replay, audit only, or unavailable.
12. Schema evolution uses explicit versions and ordered application-owned
    forward migrations. Complex or destructive changes require a verified
    backup, copy-transform-validate-swap where appropriate, recorded migration
    provenance, integrity/foreign-key checks, and refusal to open an
    unsupported newer schema. A migration may change representation but may
    not reinterpret an immutable historical fact.
13. A complete backup consists of a documented-consistent SQLite snapshot plus
    a pinned, hash-verified manifest of all referenced payloads. Restore
    verifies database integrity, foreign keys, payload presence, hashes, and
    compatible schema/application versions.
14. Deletion is an application-planned graph operation. A version-bound preview
    identifies direct and transitive effects on evidence, cases, findings,
    runs, checkpoints, citations, exports, replay, resumability, coverage,
    audit, and reclaimed storage. Authoritative foreign keys default to
    restrictive behavior; broad database cascades are limited to disposable
    projections or inseparable implementation children.
15. Logical deletion and an immutable receipt precede physical payload
    removal. A payload is physically removed only when no live logical owner,
    retention obligation, independent retained copy, or backup pin remains.

## Explicit non-decisions and M1 exclusions

This ADR does not select an ORM, SQLite binding, migration framework,
encryption-at-rest mechanism, exact physical schema, filesystem layout, IPC
transport, inline-versus-external payload threshold, or production capacity
limit. Those choices require the applicable implementation plan, dependency
review, security decision, and measurement.

M1 need only exercise the durable object and typed-edge families used by its
bounded proof. It does not claim universal taxonomy/analyzer storage breadth,
automatic retention expiry, checkpoint garbage collection, multi-process
database ownership, multi-user access, remote access, or a graph/analytics
adjunct.

## Consequences

### Positive

- Evidence, history, reuse, replay, and deletion remain queryable and
  auditable under one transactional authority.
- Large immutable payloads can be deduplicated without merging logical
  provenance or slowing ordinary product queries.
- Current UI projections can be rebuilt without destroying historical truth.
- The selected local architecture avoids administering a database server.

### Negative

- The product must coordinate SQLite, WAL/checkpoint behavior, payload
  publication, backup, restore, migration, and cleanup carefully.
- One storage authority serializes writes and must keep reads and publication
  work bounded.
- Database and payload files do not share one atomic filesystem/database
  transaction, so staging and reconciliation require fault-injection proof.

### Risks and mitigations

- **SQLite native-version drift:** pin, inventory, hash, and assert the loaded
  native build; reject affected or unsupported versions.
- **Database/payload crash inconsistency:** stage and verify payloads before
  transactional registration, reconcile orphans and missing objects, and
  never manufacture replacement evidence.
- **Long readers or write volume impede WAL checkpoints:** keep read
  transactions short, bound queries, control checkpoint policy, and measure
  WAL growth at representative scale.
- **Deletion destroys shared or replay-critical material:** require a
  dependency-versioned preview, restrictive foreign keys, explicit
  confirmation, backup pins, and typed post-deletion gap records.
- **Schema changes alter meaning:** make semantic reinterpretation a new
  revision with lineage rather than a physical migration side effect.

## Requirements affected

- AUTH-002
- SCAN-005 through SCAN-007
- SNAP-001 through SNAP-006
- EVID-001 through EVID-007
- FIND-002, FIND-005, FIND-006, FIND-012, and FIND-014
- COVER-001 through COVER-003
- DOC-002 through DOC-005 and DOC-008 through DOC-011
- AI-003, AI-004, and AI-006
- OPS-002 through OPS-005

## Validation

Before an implementation relies on this mechanism:

- EVAL-0021, EVAL-0024 through EVAL-0026, EVAL-0037 through EVAL-0041,
  EVAL-0068, EVAL-0078 through EVAL-0080, EVAL-0083, and EVAL-0087 must be
  specified and passed for the exercised surfaces;
- synthetic fixtures must cover every implemented durable object and typed
  edge, adversarial fan-out, reverse dependencies, invalid reuse, distinct
  source/application identities, and append-only review history;
- fault injection must cover payload staging/publication, stage completion,
  migration, checkpoint writes, backup/restore, deletion, missing payloads,
  corruption, and interrupted reconciliation;
- the selected binding must prove the loaded patched SQLite version, foreign
  key enforcement, required compiled features, migration behavior, and
  documented-consistent backup;
- representative query plans and latency, WAL growth, disk usage, payload
  threshold, backup, and restore must be measured rather than inferred from
  the bounded research probe; and
- clean recomputation, boundary replay, audit-only, unavailable replay, shared
  payload deletion, and independent retained-copy cases must remain
  distinguishable.

## References

- [ADR-0001 — Evidence authority boundary](ADR-0001-evidence-authority-boundary.md)
- [ADR-0002 — Snapshot and context binding](ADR-0002-snapshot-context-binding.md)
- [ADR-0010 — Snapshot fingerprint and dependency invalidation](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [RESEARCH-0036 — Evidence persistence and versioning](../../research/investigations/RESEARCH-0036-evidence-persistence-and-versioning.md)
- [RESEARCH-0044 — Wave E architecture and security integration](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
- [SQLite write-ahead logging](https://www.sqlite.org/wal.html), retrieved
  2026-07-28
- [SQLite transaction control](https://www.sqlite.org/lang_transaction.html),
  retrieved 2026-07-28
- [SQLite Online Backup API](https://www.sqlite.org/backup.html), retrieved
  2026-07-28
- [SQLite STRICT tables](https://www.sqlite.org/stricttables.html), retrieved
  2026-07-28
- [SQLite foreign keys](https://www.sqlite.org/foreignkeys.html), retrieved
  2026-07-28
- [SQLite PRAGMA reference](https://www.sqlite.org/pragma.html), retrieved
  2026-07-28
