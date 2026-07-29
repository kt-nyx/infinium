# M1 Slice 2 implementation record

Status: Completed
Completed: 2026-07-29
Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted revision dated 2026-07-28
Slice: 2 — Persistence, lifecycle, coordinator, worker, and CLI substrate
Implementation commit: `20125512901f85ba1235320f9c8a23c4f0f37aee`

## Outcome

Slice 2 establishes the first executable Infinium substrate:

- an exact, fail-closed SQLite `3.53.4` binding assertion, schema migration,
  STRICT/foreign-key authoritative store, immutable run bindings, append-only
  lifecycle/lineage/audit ledgers, rebuildable projections, fenced attempts
  and checkpoints, hash-verified content-addressed admission, reconciliation,
  and consistent database/payload backup and restore;
- application-owned lifecycle policy and durable idempotent commands with
  generation compare-and-swap, requested/observed pause and cancellation
  edges, terminal closure, stale-attempt rejection, and coordinator-restart
  recovery under a new fencing epoch;
- a single standard-user coordinator authority, restrictive random per-instance
  application and worker named pipes, bounded gRPC negotiation, role and
  version separation, nonce binding, authenticated keyset cursors, and no TCP
  listener;
- a coordinator-launched managed worker with a private inherited bootstrap
  channel, one-use process/attempt binding, fenced assignment, progress and
  control RPCs, finite limits, Job Object containment, staged manifest and
  terminal receipts, and coordinator-only validation/admission/publication;
- CLI `start`, `status`, `wait`, `cancel`, and `inspect` commands with
  human-readable or versioned JSON output and detached coordinator startup;
  and
- a public synthetic Slice 2 fixture package plus unit, integration,
  evaluation, security, and fault coverage.

The final semantic review corrected four material issues: Release packaging
initially omitted two worker runtime assemblies; coordinator restart did not
recover interrupted runs; an idempotency key could be rebound to different
inputs; and cancellation could leave an attempt publication-eligible. The
corrections are included in the implementation commit and were reverified.

## Retained artifacts and identities

- Fixture package: `M1-PLAT-SLICE2-SUBSTRATE-v1`
- Fixture families: `M1-PLAT-LIFECYCLE-v1`,
  `M1-PLAT-LINEAGE-v1`, `M1-PLAT-WRITES-v1`,
  `M1-PLAT-PERSIST-v1`, and `M1-PLAT-IPC-v1`
- Storage migration: `M1-S2-0001`
- Storage contract/schema: `1.0.0` / `1`
- Protocol: `1.0`
- Implementation commit:
  `20125512901f85ba1235320f9c8a23c4f0f37aee`

Integration tests created randomized run, attempt, coordinator, pipe, staging,
receipt, and payload identities under isolated temporary product roots. Those
roots were deleted after each test, so no product run ID is retained or
presented as durable evaluation evidence.

## Exact implementation identity

- .NET SDK: `10.0.302`; target/runtime: `net10.0` / `10.0.10`, Windows x64
- SQLite: `3.53.4`
- SQLite source ID:
  `2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc`
- Loaded win-x64 `e_sqlite3.dll` SHA-256:
  `6ad8e149f8ce3ed3716402b4b3a2268ebbdc7b64391b5fafed747e03bb1b9418`
- Required compile options include `THREADSAFE=1` and
  `DEFAULT_WAL_SYNCHRONOUS=2`
- Direct Slice 2 additions: Google.Protobuf `3.31.1`, gRPC `2.80.0`,
  Microsoft.Data.Sqlite.Core `10.0.10`, and
  SQLitePCLRaw.bundle_e_sqlite3 `3.0.5`
- Dependency manifest revision: `m1-slice-2/1`, with 11 direct and 72 resolved
  package identities

## Verification

Final commands were run from the repository root on Windows x64:

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed; all 15 projects matched committed lock files. |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed; 0 warnings and 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | Passed; 99 of 99 checks: 35 unit, 50 contract, 5 integration, and 9 evaluation. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"` | Passed; 29 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"` | Passed; 20 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"` | Passed; 6 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"` | Passed; 11 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"` | Passed; 6 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"` | Passed; 12 applicable checks. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `git diff --check` | Passed. |

The milestone-wide `evaluate` and `verify-evaluation` CLI entry points are not
run here: they belong to the later evaluation-harness slice and do not exist
in the accepted Slice 2 CLI command set. Adding placeholders would have
started later-slice work.

Raw inspection included lifecycle generations and immutable bindings, named
pipe role/nonce/version rejections, page bounds and a tampered authenticated
cursor, competing coordinator failure, worker staging and terminal receipts,
cancelled-attempt non-publication, backup tamper rejection, payload
reconciliation, and a killed-coordinator restart that completed the retained
run under a higher fencing epoch.

## Evaluation cases and accepted-plan gates

The retained synthetic checks exercise the Slice 2 portions of:

- EVAL-0026: immutable run bindings and retained run identity substrate;
- EVAL-0038 where exercised: requested/observed pause, resume, cancellation,
  stale publication rejection, and terminal closure;
- EVAL-0079 substrate: separate opaque logical and occurrence identities,
  typed reconciliation tables, and append-only lineage events;
- EVAL-0080: fixed write classes, path/device/alternate-stream/reparse
  rejection, create-new attempt staging, bounded typed output slots, Job
  Object lifetime/resource containment, and coordinator-only publication;
- EVAL-0087: exact schema/native identity, migrations, authoritative versus
  rebuildable state, CAS reconciliation, backup/restore, and newer-schema
  refusal; and
- EVAL-0088: single authority, role-separated current-user named pipes,
  handshake/version/nonce/launch binding, finite protocol limits, authenticated
  keyset pagination, durable command recovery, worker fencing, CLI operation,
  and restart recovery.

No complete milestone evaluation case is claimed to pass. The fixture is a
generic substrate fixture with no real mod names, answer-bearing production
rules, analyzer answers, or controlled-real evidence.

## Security, non-mutation, and review

- No legacy archive or historical implementation was inspected or referenced.
- No MO2, Skyrim, mod, profile, generated-output, credential, provider, or
  external-service state was read or changed.
- Runtime tests wrote only to randomized temporary product roots and deleted
  them after stopping their coordinator.
- Coordinator and worker launch use exact absolute executables, no shell,
  constructed environments, bounded private channels, and finite
  time/memory/output contracts.
- Workers cannot access SQLite or the authoritative payload store and cannot
  publish; coordinator validation checks the current run/attempt fences,
  declared slot, canonical manifest digest, bytes, size, and SHA-256.
- Generic production mechanisms contain no real-mod-, fixture-, NPC-, race-,
  title-, zone-, or category-specific shortcut.

The final diff/re-review covered the accepted Slice 2 deliverables,
requirements, ADR-0015 through ADR-0021, the platform evaluation
specification, fixture rules, anti-overfitting rules, dependency/license
closure, process cleanup, unsupported behavior, and later-slice exclusions.

## Known gaps and deferred work

- This slice qualifies only the product-root/reparse and typed staging
  substrate of EVAL-0080. The complete immutable protected-root registry,
  opened-object/file-ID registry, NT handle-relative descendant operations,
  hard-link/short-name/mount/replacement adversaries, and selected MO2/game
  root population require the supported-target snapshot context delivered by
  Slice 3. No broad write authority is claimed in their absence.
- The default created-at ascending run query has authenticated bounded keyset
  pagination. Additional allowlisted run filters/sorts and finding queries
  fail explicitly as unsupported until their producer slices exist.
- Event subscription exposes a bounded current snapshot, but retained replay
  windows and slow-client overflow/resync behavior remain to be completed with
  the later projection/event producers. Durable work itself is independent of
  transport cancellation and coordinator restart recovery is proven.
- The worker is the one-operation Slice 2 substrate, not a persistent pool or
  hostile-code sandbox. AppContainer/LPAC and same-user malicious-process
  exclusion are not claimed.
- Development schema compatibility is clean-break and disposable as allowed by
  the accepted M1 plan; no legacy-data migration is supported.

No analyzer, MO2 snapshot, Bethesda parsing, source acquisition, provider,
credential, UI, evaluation-harness, or other later-slice implementation was
included. Nothing was pushed.
