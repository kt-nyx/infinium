# RESEARCH-0036: Evidence persistence and versioning

- **Status:** Completed
- **Date opened:** 2026-07-28
- **Last reviewed:** 2026-07-28
- **Researcher:** Codex agent
- **Primary research question:** RQ-013
- **Research wave:** M0 Wave E
- **Decision enabled:** Evidence persistence, cache, and versioning ADR
- **RQ status:** Resolved for M0 by accepted ADR-0015
- **Acceptance:** Recommendation accepted by the project owner through
  ADR-0015 on 2026-07-28

## Executive answer

Infinium needs a durable local system of record, not merely a cache of scan
results. ADR-0015 accepts:

1. **SQLite as the authoritative relational store** for immutable
   runs, revisions, evidence, application links, findings, cases, review
   events, provenance, dependencies, retention state, and exact accounting.
2. **A product-controlled, content-addressed payload store** for large or
   opaque source bodies, boundary payloads, model/tool traces, checkpoints, and
   exports. SQLite owns the logical references, retention state, and
   provenance. The files are not an independent source of truth.
3. **Disposable, versioned projections and caches** for current review state,
   full-text search, summary counts, pagination, and other UI-oriented access
   paths. Deleting these must not delete the durable evidence graph.

This investigation recommended rather than selected the mechanism; ADR-0015
subsequently selected the database and payload-store boundary. Exact binding,
packaging arrangement, physical schema, and initial migration implementation
remain M1 planning and qualification work.
The recommendation also depends on shipping a patched SQLite version: the
official WAL documentation identifies a rare WAL-reset corruption bug fixed in
3.51.3 and in backports 3.50.7 and 3.44.6. A binding that embeds an affected
version is not acceptable for production.

The logical model must distinguish:

- a reusable source or documentation claim from the run that acquired it;
- a source revision from an acquisition representation of that revision;
- raw provider/tool output from admitted structured evidence;
- profile-independent evidence from its application to one snapshot or case;
- immutable historical truth from mutable current-review projections;
- physical payload deduplication from logical identity and retention policy;
- dependency reuse from proof that reuse was valid in a consuming run.

This approach supports the accepted requirements for inspectable provenance,
checkpointing, selective reuse, deletion previews, replay, stale-source
handling, and case-scoped review without making filesystem layout or provider
JSON the product's domain schema.

## Research question

RQ-013 asks:

> How should reusable documentation and LLM evidence be cached, shared, and
> versioned across profiles or snapshots without losing provenance?

This report expands that question to the persistence mechanics required by the
accepted product contracts:

- immutable analysis and acquisition runs;
- reusable evidence with exact origin and consuming-use links;
- retained source bodies or excerpts sufficient to produce useful findings
  and prose;
- explicit invalidation and dependency closures;
- deletion previews and explicit cascades;
- stable replay and audit semantics even after permitted source deletion;
- responsive queries on large profiles;
- safe schema evolution, backup, and corruption recovery.

## Scope and non-scope

### In scope

- the logical durable-versus-derived storage boundary;
- version identities for sources, evidence, runs, and applications;
- storage and retention of source bodies and excerpts;
- typed provenance, dependency, lineage, and reuse links;
- transaction, concurrency, query, migration, backup, and deletion behavior;
- comparison of SQLite with credible alternatives;
- a bounded local probe of representative relational query shapes;
- licensing and packaging implications of the storage engines.

### Out of scope

- selecting the application language, desktop framework, ORM, SQLite binding,
  IPC boundary, or background-process topology;
- selecting an encryption-at-rest implementation;
- a final physical schema or migration tool;
- choosing an exact inline-versus-external payload size threshold;
- a production performance guarantee or capacity limit;
- retention periods for individual source classes;
- implementation.

ADR-0015, ADR-0021, and ADR-0023 subsequently resolved the storage, security,
and accounting architecture questions. The remaining physical-schema,
measurement, retention-default, and implementation details belong in a future
accepted implementation milestone plan.

## Authoritative constraints

The recommendation was checked against:

- [requirements](../../product/requirements.md), particularly the snapshot,
  evidence, documentation, AI, operations, and export requirements;
- [domain model](../../product/domain-model.md);
- [data and trust model](../../architecture/data-and-trust-model.md);
- [jobs, caching, and snapshots](../../architecture/jobs-caching-and-snapshots.md);
- [security and privacy](../../architecture/security-and-privacy.md);
- [ADR-0002: snapshot context binding](../../architecture/decisions/ADR-0002-snapshot-context-binding.md);
- [ADR-0010: snapshot fingerprint and dependency invalidation](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md);
- [ADR-0013: OpenAI-first LLM capability boundary](../../architecture/decisions/ADR-0013-openai-first-llm-capability-boundary.md);
- [RESEARCH-0003: retention, replay, and export policy](RESEARCH-0003-retention-replay-export-policy.md);
- [RESEARCH-0012: snapshot fingerprint and invalidation](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md);
- [RESEARCH-0022: candidate indexes and ranking](RESEARCH-0022-candidate-index-and-ranking.md);
- [RESEARCH-0033: Wave D revision integration](RESEARCH-0033-wave-d-revision-integration.md);
- [evaluation strategy](../../evaluation/evaluation-strategy.md);
- [case catalog](../../evaluation/case-catalog.md);
- [fixture guidelines](../../evaluation/fixture-guidelines.md);
- [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md);
- [M0 research plan](../../plans/milestones/m0/plan.md).

The following are therefore requirements, not new decisions made here:

- runs and their resolved inputs are immutable;
- an acquisition run is not an analysis run;
- external source content, extraction, claims, and profile application are
  separate identities;
- reused artifacts retain their original provenance and acquire a consuming
  reuse edge with a validity proof;
- review actions are history, not mutations of findings;
- removal is previewed and explicit rather than hidden behind broad cascading
  deletion;
- exact retained boundary output may preserve downstream replay after an
  upstream body is deliberately removed;
- source absence or deletion becomes a coverage or replay gap, not invented
  certainty;
- typed indexes and causal joins are logical requirements, not a prior
  database selection.

## Sources and version check

Primary sources were retrieved on 2026-07-28.

| Subject | Primary source | Relevant result |
|---|---|---|
| SQLite release line | [SQLite chronology](https://sqlite.org/chronology.html) | The chronology listed 3.53.4, dated 2026-07-24, as the newest release at review time. |
| SQLite concurrency and durability | [Write-ahead logging](https://www.sqlite.org/wal.html) | WAL permits concurrent readers and one writer on the same machine; readers can delay checkpoints. The page documents a rare WAL-reset corruption bug and the fixed release lines. |
| SQLite transactions | [Transaction control](https://www.sqlite.org/lang_transaction.html) | SQLite serializes writes, supports explicit transactions, and does not permit simultaneous write transactions. |
| SQLite type enforcement | [STRICT tables](https://www.sqlite.org/stricttables.html) | `STRICT` tables add rigid type rules while preserving ordinary SQLite file compatibility. |
| SQLite referential integrity | [Foreign keys](https://www.sqlite.org/foreignkeys.html) | Foreign-key enforcement must be enabled on each connection and should not be assumed from library defaults. |
| SQLite validation | [PRAGMA reference](https://www.sqlite.org/pragma.html) | `quick_check`, `integrity_check`, and `foreign_key_check` provide complementary integrity checks. |
| SQLite migration limits | [ALTER TABLE](https://www.sqlite.org/lang_altertable.html) | SQLite directly supports a bounded set of alterations and documents a generalized copy-and-rebuild procedure for other changes. |
| SQLite backup | [Online Backup API](https://www.sqlite.org/backup.html) | A consistent live database snapshot should use the backup API or a supported snapshot mechanism, not a raw copy of the main file. |
| SQLite recovery | [Recovery API](https://www.sqlite.org/recovery.html) | Recovery is best effort and may extract deleted or inconsistent content; it is not a substitute for verified backups. |
| SQLite full-text search | [FTS5](https://www.sqlite.org/fts5.html) | FTS5 is available as a virtual-table extension and is suitable for a rebuildable search projection. |
| SQLite JSON representation | [SQLite JSONB](https://sqlite.org/jsonb.html) | SQLite JSONB is an internal SQLite representation and is not an interchange format; exact provider payloads should not depend on it as their durable external encoding. |
| SQLite BLOB placement | [Internal versus external BLOBs](https://www.sqlite.org/intern-v-extern-blob.html) | The official benchmark shows that the best boundary depends on payload size and environment. Its historical threshold is not a product rule; Infinium must measure its own workload. |
| SQLite fit and limits | [Appropriate uses](https://www.sqlite.org/whentouse.html), [limits](https://www.sqlite.org/limits.html) | SQLite is intended for local application storage and has limits far above the anticipated row counts; practical query design and binding behavior still require measurement. |
| SQLite licensing | [SQLite copyright](https://www.sqlite.org/copyright.html) | SQLite is dedicated to the public domain, subject to the provenance cautions on that page. |
| DuckDB positioning | [Why DuckDB](https://duckdb.org/why_duckdb), [DuckDB paper](https://duckdb.org/library/duckdb) | DuckDB is an embedded analytical, columnar database optimized for bulk analytical work. |
| DuckDB concurrency | [DuckDB concurrency](https://duckdb.org/docs/stable/connect/concurrency) | Multi-process read/write access is not the primary model; many small transactions are not its principal workload. |
| DuckDB current version | [DuckDB CLI overview](https://duckdb.org/docs/stable/clients/cli/overview) | The stable documentation listed 1.5.4 at review time. |
| DuckDB licensing | [DuckDB license](https://github.com/duckdb/duckdb/blob/main/LICENSE) | DuckDB is MIT licensed. |
| PostgreSQL topology | [PostgreSQL client/server architecture](https://www.postgresql.org/docs/current/tutorial-arch.html), [server reference](https://www.postgresql.org/docs/current/app-postgres.html) | PostgreSQL requires a database server process and cluster administration boundary. |
| PostgreSQL current version | [PostgreSQL 18.4 release](https://www.postgresql.org/docs/18/release-18-4.html) | 18.4 was current at review time. |
| PostgreSQL licensing | [PostgreSQL licence](https://www.postgresql.org/about/licence/) | PostgreSQL uses a permissive PostgreSQL License. |

Versions above are research snapshots, not permanent product pins. The accepted
ADR and dependency policy must define supported release lines and update rules.

## Evidence gathered

### Contract and schema-shape review

The existing accepted documents were traced from each required lifecycle into
the data it must preserve:

| Lifecycle | Minimum durable facts |
|---|---|
| Analysis run | Run identity, resolved configuration and feature set, snapshot identity, producer versions, stage/checkpoint history, admitted outputs, gaps, accounting ownership, terminal state |
| Acquisition run | Acquisition identity, source route and policy decision, request/retrieval metadata, observed external revision or fingerprint, body/excerpt availability, extraction outputs, failures, freshness |
| Source version | Logical source identity, immutable observed revision, retrieval observations, content hash where retained, source authority class, adapter/parser version |
| Evidence and claim | Immutable evidence/claim revision, exact origin, source support, extraction/model/tool producer, confidence basis, retained payload or boundary output |
| Snapshot application | Evidence revision, consuming run and snapshot, relevant mod/config/plugin context, applicability decision and proof |
| Reuse | Producing artifact, consuming run, dependency closure, observed dependency state, reuse decision, invalidation result |
| Finding and case | Immutable finding/case revisions, supporting and contradicting evidence links, taxonomy versions, severity/confidence rationale, resolution/validation proposals |
| Review | Append-only review event, actor, time, rationale, superseded event, derived current projection |
| Deletion | Preview identity, selected logical objects, affected dependents and capabilities, explicit scope, execution event, retained tombstone or availability gap |
| Replay/audit | Exact boundary payloads and versions, invocation metadata, deterministic input identities, output admission state, later availability transitions |

This shape is relational and join-heavy. The dominant operations are not only
large aggregate scans: they also include small transactional appends, reverse
dependency queries, paginated finding lists, exact run manifests, and
interactive provenance expansion.

### Bounded local probe

A bounded, disposable probe exercised representative relational shapes using
the local Node.js `node:sqlite` binding. It was not an application prototype.
The temporary database was deleted after recording the result.

Environment:

- Windows local filesystem on the development machine;
- Node.js 24.11.1;
- bundled SQLite 3.50.4;
- one connection using WAL, `synchronous=FULL`, foreign keys, and `STRICT`
  tables.

Synthetic population:

- 300,000 immutable revision rows;
- 1,200,000 typed dependency edges;
- 100,000 snapshot-application links;
- 30,000 current-projection rows.

Observed results:

| Operation | Local observation |
|---|---:|
| Bulk transaction population | 1,164 ms |
| Index construction | 1,688 ms |
| Direct dependency lookup | 0.96 ms |
| Reverse dependency lookup | 0.10 ms |
| Bounded four-hop dependency traversal | 0.23 ms |
| Application join returning 20,000 rows | 23.0 ms |
| Database plus WAL before checkpoint | 156.3 MB |
| Database after checkpoint | 107.5 MB |
| `quick_check` | `ok` |
| Foreign-key violations | 0 |

The probe supports feasibility of the relational shape; it does not establish
production latency, storage forecasts, concurrency behavior, or a safe maximum
profile size. The data was synthetic, highly regular, created in one bulk
transaction, and queried on a high-end local development machine with warm
caches. The inline probe script was not retained, so this run is procedurally
described rather than byte-for-byte replayable.

The binding's SQLite 3.50.4 is inside the affected WAL-reset range described by
the current SQLite documentation. The probe used one connection and did not
exercise the bug's multi-connection conditions. This reinforces the requirement
to control the shipped native SQLite version; the probe binding is not a
production recommendation.

## Findings

### 1. Durable history and current UI state must be different things

Findings, cases, source revisions, applications, and review decisions have
historical meaning. Updating a row in place would erase why a prior scan or
readiness result behaved as it did. Infinium should append immutable revisions
and review events, then derive explicit current projections.

Examples of mutable projections include:

- the latest review disposition for a finding;
- the current preferred case revision;
- the latest known source revision;
- current body/excerpt availability;
- current readiness status;
- summary counts, search tokens, and list-sort keys.

These projections may be rebuilt. The event and revision history may not be
discarded as a cache cleanup side effect.

### 2. Physical deduplication must not collapse logical identity

Two acquisition runs may obtain identical bytes through different allowed
routes, at different times, under different source identities or policies.
They may share one physical content-addressed payload, but they remain distinct
acquisition observations.

Likewise, a source's advertised revision identifier is not sufficient to prove
content identity. If an upstream service omits a revision or serves changed
bytes under the same advertised revision, Infinium needs the retrieval
observation and retained content hash to distinguish what it actually saw.

### 3. Source bodies and admitted evidence need separate retention

During development, Infinium is explicitly permitted by the project decision
to retain useful Nexus source material unless that decision is revised.
Nevertheless, the data model must already support later minimization:

- a raw source body can be removed while a permitted exact excerpt remains;
- a body and excerpt can be removed while an admitted structured claim and its
  derivation metadata remain;
- a clean upstream recomputation can become unavailable while downstream
  replay from a retained boundary output remains available;
- later deletion must record the new gap instead of rewriting the historical
  result.

The source body, excerpt, extraction output, admitted claim, and generated prose
therefore require independent logical identities and availability states.

### 4. Provider and tool payloads are evidence inputs, not the query model

Exact raw payloads can be retained as versioned byte or text objects for audit.
Fields that drive findings, filtering, dependency traversal, readiness, cost,
or deletion must be admitted into typed product records. Querying arbitrary
provider JSON as the authoritative domain model would make schema evolution,
validation, provider changes, and cross-provider comparison fragile.

SQLite JSON functions may be useful for bounded inspection. SQLite JSONB must
not become the interchange or archival format for provider payloads.

### 5. Typed relational joins are sufficient for the accepted causal model

The dependency and provenance requirements need both forward and reverse
navigation, edge qualifications, version identity, and bounded traversal. They
do not currently justify a graph database.

Typed edge tables with indexes in both directions support:

- “what produced or supports this finding?”;
- “which artifacts depend on this source revision?”;
- “what becomes stale if this profile input changes?”;
- “which consuming runs reused this extraction?”;
- “what replay capability is lost if this payload is removed?”.

Recursive relational traversal should be bounded by edge class and run or
snapshot scope. Frequently used summaries can be derived projections.

### 6. SQLite's concurrency model fits only with a single write owner

Infinium is a local desktop product, and accepted architecture already favors
explicit backend ownership. SQLite WAL supports responsive concurrent reads,
but it still permits only one writer. Long read transactions can prevent WAL
checkpoint progress.

The application should therefore have:

- one backend storage owner that serializes write transactions;
- short read transactions and paginated result access;
- bounded, atomic stage-result commits;
- no direct UI process writes;
- no expectation that independent processes can freely write the same file.

ADR-0018 and ADR-0019 subsequently resolved the M0 process and IPC design.
Implementation and qualification remain pending.

### 7. Large-profile behavior depends more on access paths than theoretical limits

The likely scale is well inside SQLite's documented hard limits. The actual
risks are unindexed reverse joins, unbounded recursive traversals, JSON scans,
large result materialization, long transactions, and search/index rebuilds.

The first implementation should use:

- typed foreign-key columns rather than string matching;
- compact internal join keys plus stable opaque external identifiers;
- composite indexes matching both forward and reverse causal joins;
- keyset pagination for long finding, evidence, and source lists;
- bounded graph traversal;
- batched writes inside explicit transactions;
- rebuildable full-text and summary projections;
- measurements against atomic fixtures and a high-scale real-profile shape.

The local probe is sufficient to avoid speculative engine escalation. It is not
a replacement for milestone performance gates.

## Accepted logical persistence model

The following is a conceptual model for the ADR and later schema design, not a
final table list.

### Identity and revision envelope

Every durable object that can be cited, reused, revised, invalidated, or
deleted needs:

- a stable opaque logical identifier;
- an immutable revision identifier where the object is versioned;
- an object kind from an allowlisted type;
- creation/admission time;
- producing run, tool, model, adapter, prompt/schema, and software versions as
  applicable;
- current availability and retention references outside the semantic payload.

A common identity/revision envelope can make provenance references uniform.
Domain payloads should still live in typed normalized tables rather than one
generic entity-attribute-value or JSON document store.

### Durable categories

At minimum, the durable store should represent:

- analysis runs, stages, checkpoints, and resolved run manifests;
- acquisition runs, attempts, routes, source policies, and failures;
- profile snapshots and typed input fingerprints;
- source entities, source revisions, acquisition representations, and
  extraction revisions;
- raw tool/model invocations and their exact retained payloads;
- admitted observations, claims, and evidence revisions;
- snapshot/profile application links;
- dependency, provenance, lineage, and reuse edges;
- findings, case revisions, severity/confidence assignments, coverage, gaps,
  readiness evaluations, and recommendation/validation proposals;
- review events, annotations, assumption changes, and exception/suppression
  events;
- deletion plans, deletion executions, availability changes, and audit events;
- cost and token-usage ownership records required by RQ-034;
- logical payload objects and their ownership, policy, and retention links.

### Source and acquisition versioning

Use distinct identities for:

1. **Source entity:** the logical author-maintained page, LOOT masterlist
   location, mod metadata record, local file, or other source.
2. **Source revision:** the immutable version Infinium believes the source had,
   using an upstream revision when reliable and an observed content fingerprint
   when necessary.
3. **Acquisition representation:** the actual response/body obtained through a
   route, at a time, with adapter/schema/request metadata and a content hash.
4. **Extraction revision:** a parser, deterministic transformer, or model
   interpretation of that representation.
5. **Claim/evidence revision:** an admitted semantic assertion with support,
   contradiction, authority class, and confidence basis.
6. **Application link:** a decision that the claim/evidence applies to a
   particular analysis run, snapshot, and local context, with applicability
   proof.

Identical physical bytes may be deduplicated. None of these logical layers may
be merged merely because the bytes match.

### Profile-application links

An application link should identify:

- the exact evidence or claim revision;
- the consuming analysis run and profile snapshot;
- relevant installed-mod, plugin, file, configuration, or tool-state context;
- application role and result;
- the deterministic inputs or model decision that established applicability;
- the validity proof and freshness decision;
- creation/admission time.

A uniqueness rule should prevent accidentally recording the same application
twice while allowing a later analysis run to make a new application decision.

### Payload storage

Use two physical classes:

1. **Inline relational values** for identifiers, small typed fields, short
   excerpts, hashes, state, and fields needed for indexed queries.
2. **External immutable payload objects** for large source bodies, opaque
   responses, binary files, lengthy traces, checkpoint bundles, and exports.

Each external payload record should carry at least:

- content hash and hash algorithm;
- byte size and media/encoding information;
- payload kind and codec/schema version;
- product-controlled relative location;
- verification state;
- logical owners and policy/retention classes;
- current local availability;
- any later deletion or quarantine event.

The exact inline threshold must be measured with the selected binding,
filesystem, backup strategy, antivirus behavior, and real payload distribution.
The historical SQLite BLOB benchmark is evidence that a threshold matters, not
evidence for a universal 100 KB cutoff.

### Safe payload commit and removal

A durable payload commit should:

1. write to a temporary file inside the controlled payload-store volume;
2. compute and verify the content hash and size;
3. atomically rename it to its content-addressed location;
4. commit the logical payload and owner references in one database
   transaction;
5. allow a reconciler to remove unreferenced temporary or orphaned objects.

Logical deletion should happen first through an explicit deletion plan.
Physical removal may occur only after no live logical owner, backup pin, or
required retention rule references the payload. A raw reference count is an
optimization, not the policy authority.

## Transactions and immutable completion

### Run and stage commits

- A run begins with an immutable resolved manifest.
- Stage work may append idempotent checkpoints.
- A stage is not marked complete until its admitted outputs, provenance,
  dependency/reuse edges, coverage, gaps, and accounting ownership have
  committed atomically.
- A run is not marked terminal-success until all required stage states and
  durable output references exist.
- Cancellation and failure append terminal events; they do not erase completed
  checkpoints.

This prevents a UI from presenting a complete run whose evidence graph was
only partially committed.

### Accepted SQLite operating direction and implementation constraints

If the ADR accepts SQLite:

- pin a patched supported release; at the current date, use 3.51.3 or newer,
  or the documented 3.50.7/3.44.6 backport line if a binding requires it;
- use WAL only with a single application-controlled write owner;
- enable foreign keys on every connection and verify that the setting took
  effect;
- use `STRICT` tables where domain typing permits;
- use strong durability for the authoritative database;
- keep read transactions short and control checkpoint behavior;
- make disposable caches physically or logically distinguishable from
  authoritative tables;
- never open user-selected arbitrary database files as an Infinium store.

Exact PRAGMAs, connection pooling, encryption, and native-binary loading rules
belong in the ADR and security design.

## Typed indexes and causal joins

The physical schema should provide explicit, indexed paths for:

- dependency: producer/input to dependent artifact and the reverse;
- provenance: derived artifact to producing invocation/source and the reverse;
- evidence support and contradiction;
- source revision to extraction to claim to application;
- profile snapshot to applications, findings, cases, coverage, and readiness;
- reuse: producing artifact to consuming run and the reverse;
- lineage: superseded revision to successor and the reverse;
- payload ownership and deletion impact.

Each edge needs:

- an allowlisted edge kind;
- exact endpoint object and revision kinds;
- run/snapshot scope where applicable;
- edge qualifications, such as dependency role and required freshness;
- producing/admitting event;
- enough evidence to explain why the edge exists.

Do not use one untyped “related objects” table. A small shared edge envelope is
acceptable only if endpoint and edge-type constraints are enforced and
domain-specific qualifications remain typed.

## Reuse, invalidation, and replay

Every reusable artifact needs the smallest complete dependency closure that can
prove whether reuse remains valid. At reuse time, Infinium should record:

- the producing artifact and original provenance;
- the consuming run;
- dependency identities and versions observed by the consumer;
- the rule or analyzer version used to validate reuse;
- freshness and source-policy decisions;
- whether the artifact was reused, refreshed, or rejected;
- any capability degraded by absent upstream material.

The reused object does not become newly produced evidence. The consuming reuse
edge explains why it was admitted into the new run.

Replay must be expressed by level:

- **Clean recomputation:** all original upstream inputs and versions remain
  available.
- **Boundary replay:** an exact retained tool/model/extraction output can feed
  later deterministic stages even if an upstream body was deleted.
- **Audit only:** the conclusion and provenance metadata remain, but neither
  clean recomputation nor boundary replay is possible.
- **Unavailable:** required retained material is missing; the gap is explicit.

Deleting a source body changes replay availability. It does not rewrite the
historical finding or pretend that the source was never used.

## Mutable review projections

Review behavior should be append-only:

- accept, reject, acknowledge, suppress, reopen, annotate, and change-assumption
  actions are events;
- a later event can supersede an earlier event without deleting it;
- “current disposition” is a derived projection;
- a projection carries its projector/schema version and last processed event;
- deleting and rebuilding the projection must produce the same current state
  from the retained event stream.

This rule also applies to current source freshness, current readiness, and
other labels that summarize immutable history.

## Schema evolution

The accepted ADR should require:

- an explicit storage schema version;
- ordered, forward migrations owned by the application;
- a verified backup before a destructive or complex migration;
- refusal to open a database written by an unsupported newer schema;
- transactional migrations where SQLite permits them;
- copy-transform-validate-swap for complex table changes;
- post-migration `quick_check` or `integrity_check` plus
  `foreign_key_check`;
- recorded migration identity, application version, start/end state, and
  result;
- codec/schema versions on retained external payloads and projections.

A physical migration may change representation. It must not silently reinterpret
an immutable historical fact. A new interpretation, taxonomy assignment,
finding, or extracted claim becomes a new revision linked by lineage.

Provider payloads must retain an exact raw representation where policy permits
and an explicit adapter/schema version. This allows a later parser to create a
new extraction revision without pretending the earlier extraction never
existed.

## Deletion preview and explicit cascade

Deletion behavior must be graph-aware and capability-aware.

### Preview

Before deletion, create a deletion-plan snapshot that reports:

- selected logical objects and payloads;
- direct and transitive dependents by typed edge;
- findings, cases, exports, citations, checkpoints, and runs affected;
- clean recomputation, boundary replay, audit, and resume capability lost;
- independent physical or logical copies that survive;
- shared payloads that cannot yet be physically removed;
- projected coverage and provenance gaps;
- estimated reclaimed bytes.

The preview itself needs an identity and dependency version so execution cannot
silently apply a stale plan after the graph changes.

### Execution

- Default authoritative foreign keys should use `RESTRICT`/`NO ACTION`.
- Broad database-level cascades should be limited to disposable projections or
  inseparable implementation-owned children.
- The application executes the confirmed logical set explicitly and records a
  deletion event.
- Minimal tombstones, hashes, audit facts, and capability-loss events may
  remain when policy permits, but removed source text must not survive inside a
  supposedly minimal tombstone.
- Shared physical payloads are removed only when all logical retention
  obligations and backup pins are gone.

This makes “delete this source body” different from “erase all historical
claims, findings, applications, and review work that ever depended on it.”

## Backup, integrity, and corruption recovery

### Backup

For SQLite, do not copy a live `.db` file without accounting for WAL state.
Use the Online Backup API or another documented consistent snapshot mechanism.

Because large payloads live outside the database, a complete backup needs:

- a database snapshot;
- a manifest of the exact content-addressed payloads referenced by that
  snapshot;
- temporary retention pins so concurrent cleanup cannot remove them;
- copied payloads verified against their recorded hashes;
- backup format and application/schema version metadata;
- a restore verification that checks database integrity, foreign keys, payload
  presence, and payload hashes.

The precise coordinator belongs in the ADR or implementation plan. Immutable
content-addressed payloads make a consistent set possible without freezing
ordinary reads.

### Corruption and reconciliation

- Disposable projections may be deleted and rebuilt.
- Authoritative corruption should restore from the newest verified compatible
  backup.
- SQLite recovery tooling is an emergency best-effort path, not the normal
  recovery plan.
- Payload hash failures should quarantine the payload, mark it unavailable, and
  propagate an explicit capability/coverage gap.
- A reconciler should detect temporary files, unreferenced payloads, missing
  payloads, and database references whose hash or size does not match.
- Repair must never manufacture evidence or silently rebind a source revision
  to different bytes.

## Alternatives considered

### SQLite plus external payload store — recommended for ADR acceptance

Strengths:

- embedded and local-first;
- transactional relational model fits provenance and causal joins;
- simple single-user packaging;
- mature backup, integrity, WAL, indexing, FTS, and migration primitives;
- public-domain core compatible with the project's GPL direction;
- local probe supports the expected query shape.

Costs and risks:

- one writer;
- application must control WAL/checkpoint and connection lifetime;
- native version must be pinned because language bindings can lag fixes;
- migration tooling is less rich than PostgreSQL;
- encryption is not supplied by ordinary SQLite and remains a separate
  security decision;
- external payloads require coordinated backup and cleanup.

### DuckDB — reject as the M1 authoritative store

DuckDB is compelling for columnar analytics, bulk imports, and ad hoc queries.
Infinium's authoritative workload also has many small transactional appends,
event history, referential integrity, interactive point/reverse lookups, and
checkpoint updates. DuckDB's documented concurrency model and analytical focus
make it a poorer primary fit.

It may be reconsidered later as a disposable analytical/export adjunct if
measured workloads justify another engine. Adding it speculatively would expand
binary packaging, migrations, licensing inventory, and consistency surfaces.

### PostgreSQL — reject for the local M1 product

PostgreSQL would provide strong multi-writer concurrency, extensive migration
and operational tools, and a path to a shared service. It also introduces a
server process, database cluster lifecycle, ports, credentials, installer and
upgrade handling, service health, and a more complex backup boundary.

No accepted M1 requirement needs those costs. Reconsider it only if Infinium
later becomes a multi-user/shared service or measured workloads exceed the
embedded design.

### Files, JSON, or an append-only directory as the system of record — reject

Files are appropriate for immutable large payloads and exports. Alone, they do
not provide the transactional joins, uniqueness, reverse dependencies,
pagination, referential checks, explicit deletion impact, or atomic completion
required by the product. Rebuilding a relational index from files would simply
create two sources of truth unless the file format itself became a much more
complex database.

### Dedicated graph database — reject without evidence

The accepted causal graph is typed, versioned, mostly bounded by a run,
snapshot, or source chain, and frequently presented in relational lists. Typed
tables and indexed joins satisfy the known operations. A graph engine would add
another runtime, storage, backup, migration, and packaging surface without a
measured need.

## Licensing and packaging

- SQLite's core is public domain and is compatible with a GPL application.
- DuckDB's MIT license and PostgreSQL's permissive license are also compatible
  in principle.
- The selected language binding, native binary distribution, optional
  extensions, compression library, encryption component, and migration tool
  each require their own dependency and provenance review.
- Infinium should ship the exact supported native engine version rather than
  rely on an arbitrary system SQLite.
- FTS5 or other compiled SQLite features must be verified in the selected
  distribution.
- Native binary hashes, source/version provenance, notices where applicable,
  and SBOM entries belong in release packaging.

The database license does not decide the application stack or eliminate the
need for dependency-level review.

## Recommendation for the ADR

The Wave E storage ADR should propose:

1. SQLite as the authoritative local relational system of record.
2. A pinned, patched SQLite release and a product-owned single-writer backend.
3. A product-controlled content-addressed payload store for large immutable
   bodies and traces.
4. Typed normalized domain tables with stable logical identities and immutable
   revisions.
5. Separate source, acquisition, extraction, claim/evidence, and
   snapshot-application identities.
6. Append-only review and availability history with rebuildable current
   projections.
7. Explicit provenance, dependency, reuse, lineage, and payload-ownership
   indexes in both traversal directions.
8. Raw provider/tool payload retention separate from admitted typed product
   records.
9. Explicit versioned migrations, validation, backup, and restore requirements.
10. Graph-aware deletion preview and application-controlled cascades.
11. A measurement gate for inline/external payload thresholds, real-profile
    query latency, WAL growth, checkpoint behavior, storage footprint, backup,
    and restore.
12. Deferral of binding, ORM, exact schema, encryption, and IPC details until
    their dependent decisions are researched.

## Required validation before implementation

The milestone plan that implements persistence should include:

- an atomic synthetic fixture exercising every durable object and edge kind;
- a large synthetic profile with adversarial fan-out and reverse dependencies;
- a small real-mod profile that is materially different from the first
  category proof under the anti-overfitting rules;
- tests for invalid reuse after every dependency class changes;
- tests proving profile-independent evidence is reused while application links
  remain snapshot-specific;
- run immutability and append-only review-history tests;
- crash/fault injection around payload staging, stage completion, migrations,
  deletion, and checkpoint writes;
- clean recomputation, boundary replay, audit-only, and unavailable replay
  cases;
- deletion preview/execution tests including shared payloads and independent
  copies;
- query plans and latency budgets for finding lists, reverse provenance,
  dependency impact, and source-to-application traversal;
- WAL growth and checkpoint tests with concurrent reads and cancellation;
- backup/restore and corruption-reconciliation drills;
- a native-version assertion that rejects affected or unsupported SQLite
  builds.

Relevant existing evaluation cases include EVAL-0013, EVAL-0014, EVAL-0021,
EVAL-0025, EVAL-0026, EVAL-0037 through EVAL-0041, EVAL-0068, EVAL-0078
through EVAL-0080, EVAL-0083, and the new dedicated persistence-integrity case
EVAL-0087 in the
[case catalog](../../evaluation/case-catalog.md). The ADR or implementation
plan should add narrower persistence cases if those cases do not fully exercise
transaction and recovery behavior.

## Confidence

- **High** that immutable history and mutable current projections must be
  distinct.
- **High** that source, acquisition, extraction, claim/evidence, and
  snapshot-application identities must remain distinct even when payload bytes
  are deduplicated.
- **High** that a typed relational store plus separately managed large payloads
  fits the accepted local-first lifecycle better than files alone.
- **Medium-high** that SQLite is the best M1 authoritative-store candidate
  given the single-user desktop boundary and required transactional joins.
- **Medium** that the accepted SQLite-plus-payload split will meet high-profile
  responsiveness and storage budgets; the bounded probe is supportive but not
  production evidence.
- **Low/unsupported** for an exact payload threshold, binding, ORM, encryption
  mechanism, final schema, production capacity, backup duration, or
  multi-process topology.

## Uncertainty, stopping rule, and reopen triggers

### Remaining uncertainty

- the selected application stack may constrain SQLite bindings or native
  update cadence;
- real Nexus bodies, model traces, and extracted evidence may shift the
  inline/external payload boundary;
- encryption-at-rest requirements could affect the database or payload-store
  mechanism;
- actual fan-out, search, and retention behavior is not known before M1
  fixtures exist;
- exact backup size and duration are not measured;
- provider terms may later change what bodies can be retained or exported.

### Stopping rule met

Research can stop for RQ-013 because:

- authoritative lifecycle requirements have been converted into storage
  requirements;
- three credible database approaches and file/graph alternatives were
  compared;
- primary documentation confirms concurrency, backup, migration, and licensing
  boundaries;
- a bounded relational probe found no engine-level blocker at representative
  row counts;
- remaining questions are implementation measurements or dependent decisions,
  not blockers to proposing the ADR.

### Reopen if

- the selected UI/backend stack cannot ship a patched supported SQLite binding;
- M1 measurements miss accepted latency, storage, checkpoint, backup, or
  restore budgets;
- a multi-user or remote-service requirement becomes accepted;
- encryption or policy requirements cannot be met by the accepted split store;
- exact replay requires transactional semantics across payloads that the staged
  protocol cannot provide;
- source retention rules require physical isolation by provider or policy;
- dependency traversal becomes demonstrably unsuitable for indexed relational
  joins.

## Decision provenance

This report supplied the research recommendation accepted in ADR-0015.
Acceptance selects the architecture but does not claim implementation or
evaluation conformance:

- SQLite and the authoritative/payload/projection boundary are accepted;
- no database library, ORM, native package, migration framework, or filesystem
  layout is selected;
- the accepted conceptual product and evidence-lifecycle contracts remain
  authoritative.
