# M1 Slice 2 implementation record

Status: Complete after review, correction, and re-review
Review completed: 2026-07-29
Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted revision dated 2026-07-28
Slice: 2 — Persistence, lifecycle, coordinator, worker, and CLI substrate
Implementation commit: `20125512901f85ba1235320f9c8a23c4f0f37aee`
Review corrections:

- `6eafe0b7c8e5d6de10feb75b4e812b5a99ac5ab0`
  (`fix: address M1 Slice 2 review findings`);
- `7edcb381b7c59d5f0a7da440389171cf71cdddd1`
  (`fix: qualify Slice 2 SQLite write authority`); and
- final closeout follow-up commit with subject
  `fix: address M1 Slice 2 review findings`.

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

The independent review/fix/re-review cycle corrected additional material
issues in the original implementation:

- coordinator identity and lease acquisition could permit overlapping
  authorities when intermediate product-root directories changed;
- dispatch and attempt creation, and publication and terminal lifecycle state,
  were split across transactions and admitted pause/resume races;
- cancellation and indeterminate transport recovery could misclassify a
  different durable command as a successful replay;
- pause/cancel observation, checkpoint admission, recovery, and publication
  fencing did not consistently require the newest live attempt and active
  coordinator lease;
- startup recovery could dispatch before the worker pipe listened;
- worker launch, inherited handles, staged manifests, diagnostics, scheduler
  admission, IPC connections, subscriptions, and command rate were not all
  bounded at their real execution boundaries;
- write classes, opened-object identities, private storage ACLs, pipe peer
  elevation/integrity checks, write audit records, backup/restore validation,
  and existing-store schema integrity were incomplete;
- the initial synthetic evaluation input was not a complete schema-valid,
  answer-isolated fixture package; and
- dependency provenance and license curation were not reproducibly generated
  from the locked packages.

The corrections are retained in focused review commits and were subjected to
repeated semantic and diff review. The final closeout converted every
remaining delivered product mutation to retained handle-relative authority,
bounded inherited worker handles, published the runtime descriptor only after
the coordinator was listening, made CAS placement bounded and atomic, and
made backup creation validate or remove its complete bundle. Restore now
checkpoints away SQLite sidecars, pins the exact post-audit database across
semantic validation, requires the exact expected file and directory
inventory, compares identity and bytes across its handle-relative root
rename, and either rolls back or deletes a rejected tree if the former
staging name is obstructed. These corrections close the remaining
ADR-0021/EVAL-0080 blocker without changing the accepted architecture.

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
| `dotnet test Infinium.sln -c Release --no-build --nologo` | Passed; 143 checks passed and 1 environment-dependent symbolic-link check skipped: 68 unit passed/1 skipped, 50 contract, 13 integration, and 12 evaluation. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"` | Passed; 60 applicable checks passed and 1 symbolic-link check skipped. |
| `dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "Name~GuardedSqlite"` | Passed; 2 SQLite opened-object, replacement, WAL-persistence, restart, and hard-link checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"` | Passed; 20 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"` | Passed; 14 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"` | Passed; 14 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"` | Passed; 7 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"` | Passed; 13 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Security"` | Passed; 48 cross-category security checks passed and 1 symbolic-link check skipped. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Fault"` | Passed; 53 cross-category fault checks. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed; the committed dependency manifest matches all locked packages and curated provenance/license inputs. |
| `git diff --check` | Passed. |

The milestone-wide `evaluate` and `verify-evaluation` CLI entry points are not
run here: they belong to the later evaluation-harness slice and do not exist
in the accepted Slice 2 CLI command set. Adding placeholders would have
started later-slice work.

Raw inspection included lifecycle generations and immutable bindings; accepted
durable-command kind/generation/start identity; named-pipe DACL, peer token,
role, nonce, version, and connection/subscription limits; current progress and
typed cursor resynchronization; stable mutex identity and lease fencing;
worker Job membership, inherited-handle allowlist, progress/staging/terminal
receipts, UTF-8 diagnostic limits, and safe-boundary cancellation; SQLite
identity/schema/integrity, write-class audit rows, backup manifests, restored
payload hashes, tamper rejection, and reconciliation; the seven-file public
fixture package and oracle-isolation mutation; and the generated dependency
manifest plus curated license/provenance input.

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
- Product storage uses six closed write classes, protected current-user/SYSTEM
  ACLs, non-delete-shared retained root/class identities, handle-relative
  descendant mutation, and durable audit events for class binding, runtime
  descriptor, checkpoint, staging, payload, backup, and restore writes.
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

- **Closed Slice 2 gate:** the write-authority layer has closed write classes,
  private ACLs, non-delete-shared root/class/staging handles, volume/file
  identities, final-path checks, reparse/hard-link rejection, bounded
  handle-relative copying and deletion, and typed audit records. The qualified
SQLite shim opens and pins the database/WAL/SHM/journal family before exact
`win32` VFS use. CAS admission hashes the exact non-write-shared source bytes
while copying to a temporary leaf, atomically renames only the completed
object, retains staging across a late relational failure, and deletes it only
after commit. Restore revalidates the full staged tree across its
handle-relative root rename, publishes only its exact validated file and
directory inventory, and removes a rejected target if rollback is obstructed.
Runtime metadata and backup/restore non-SQLite writes use the same primitive.
  [RESEARCH-0050](../../research/investigations/RESEARCH-0050-sqlite-opened-object-write-authority.md)
  records why the SQLite portion is ADR-0021's separately qualified
  equivalent. The delivered Slice 2 portion of EVAL-0080 now passes.
- The review's earlier pre-database rejection-audit concern was rechecked
  against EVAL-0080. The accepted case requires an audit record for each
  product write; an authority rejection before database availability performs
  no write. It must fail closed and remain diagnostically visible, but a
  separate durable log of rejected bootstrap attempts would be a new audit
  contract rather than a current Slice 2 gate.
- The generic immutable protected-root registry is implemented and exercised
  with synthetic opened objects. Populating it with actual selected
  MO2/game/profile/mod/generated-output identities requires Slice 3's
  supported-target selection and was not started here.
- The default created-at ascending run query has authenticated bounded keyset
  pagination. Additional allowlisted run filters/sorts and finding queries
  fail explicitly as unsupported until their producer slices exist.
- Event subscription has bounded queues and typed invalid/gap/expiry,
  coordinator-restart, projection-rebuild, and overflow resynchronization.
  Retained multi-event replay beyond the current Slice 2 lifecycle/progress
  projection remains deferred to its later event producers.
- The worker is the one-operation Slice 2 substrate, not a persistent pool or
  hostile-code sandbox. AppContainer/LPAC and same-user malicious-process
  exclusion are not claimed.
- Development schema compatibility is clean-break and disposable as allowed by
  the accepted M1 plan; no legacy-data migration is supported.

No analyzer, MO2 snapshot, Bethesda parsing, source acquisition, provider,
credential, UI, evaluation-harness, or other later-slice implementation was
included. Nothing was pushed.
