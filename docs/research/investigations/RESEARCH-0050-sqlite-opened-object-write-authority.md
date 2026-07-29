# RESEARCH-0050: SQLite opened-object write authority

- **Status:** Completed
- **Date opened:** 2026-07-29
- **Last reviewed:** 2026-07-29
- **Researcher:** Codex agent
- **Primary question:** Can Slice 2 satisfy ADR-0021 for SQLite without
  replacing the accepted SQLite/WAL architecture?
- **Decision enabled:** M1 Slice 2 write-authority implementation
- **Disposition:** The bounded shim-VFS approach is viable on the supported
  Windows/NTFS baseline; full EVAL-0080 closure still requires integrating the
  same opened-object discipline across non-SQLite product writes.

## Executive answer

Yes. Microsoft.Data.Sqlite `10.0.10` exposes a per-connection `Vfs` selector,
and SQLite permits a registered VFS to delegate to the built-in Windows VFS.
A narrow Infinium shim can therefore establish authority before delegating
SQLite byte I/O and locking:

1. accept only the exact database leaf and its `-wal`, `-shm`, and `-journal`
   auxiliaries;
2. create or open each file relative to the retained write-class directory
   handle with `NtCreateFile`;
3. reject reparse objects, multiple hard links, volume changes, unexpected
   final paths, and changed file identity;
4. retain a non-delete-shared guard handle while SQLite's exact `win32` VFS
   opens and uses the same object;
5. use in-memory temporary storage and SQLite's persistent-WAL file control so
   the guarded WAL/SHM names remain stable; and
6. revalidate every retained SQLite-family guard before each authoritative
   transaction.

This is a separately qualified equivalent of ADR-0021's handle-relative
open/create requirement. It is not path-string-only authorization: the
path-based Windows VFS call occurs only after an atomically opened,
identity-validated object is pinned against rename or deletion.

The prototype does not close Slice 2 by itself. Backup manifests and payloads,
restore staging/publication, CAS publication, runtime descriptors, and some
directory creation still require the same opened-object conversion.

## Scope and non-scope

### In scope

- the exact patched SQLite/SQLitePCLRaw/Microsoft.Data.Sqlite stack already
  selected for M1;
- the main database, WAL, shared-memory, and rollback-journal filenames;
- VFS registration and per-connection selection;
- handle-relative create/open, final-object validation, guard lifetime, and
  restart behavior;
- replacement, deletion, and hard-link adversaries; and
- compatibility with the current STRICT schema, migrations, online backup,
  foreign keys, and lifecycle tests.

### Out of scope

- changing SQLite, WAL, or the authoritative-store architecture;
- replacing SQLite's byte I/O or lock implementation;
- unsupported/network filesystems;
- resistance to an independently malicious same-user process after a guard
  check; ADR-0021 already excludes that stronger sandbox claim;
- non-SQLite product-write conversion; and
- later-slice MO2/game/profile root population.

## Authoritative constraints

- [M1 Slice 2](../../plans/milestones/M1-backend-semantic-proof.md) makes
  EVAL-0080 a gate.
- [ADR-0015](../../architecture/decisions/ADR-0015-authoritative-evidence-persistence-and-payload-storage.md)
  requires the exact patched SQLite line and coordinated WAL behavior.
- [ADR-0021](../../architecture/decisions/ADR-0021-desktop-and-local-operation-security-boundary.md)
  requires opened-handle identity and handle-relative operations, permitting a
  separately qualified equivalent.
- [EVAL-0080](../../evaluation/specifications/m1-platform-and-operational.md)
  fails path-string-only authorization and unexpected hard-link, replacement,
  stale-capability, or protected-root effects.

## Sources and exact versions

Primary sources were checked on 2026-07-29.

| Subject | Primary source | Relevant result |
|---|---|---|
| SQLite VFS | [SQLite VFS](https://www.sqlite.org/vfs.html) | SQLite supports registered alternative VFS implementations; `xOpen` creates the SQLite file object and the built-in Windows VFS is `win32`. |
| VFS ABI | [SQLite VFS object](https://www.sqlite.org/c3ref/vfs.html) | Defines the versioned `sqlite3_vfs` callback layout used by the shim. |
| Persistent WAL | [SQLite file-control opcodes](https://sqlite.org/c3ref/c_fcntl_begin_atomic_write.html) | `SQLITE_FCNTL_PERSIST_WAL` preserves WAL and shared-memory files after the last connection closes. |
| Per-connection VFS selection | [Microsoft.Data.Sqlite connection strings](https://learn.microsoft.com/dotnet/standard/data/sqlite/connection-strings) | Microsoft.Data.Sqlite 10 adds the `Vfs` connection-string selector. |
| Binding architecture | [Microsoft.Data.Sqlite custom versions](https://learn.microsoft.com/dotnet/standard/data/sqlite/custom-versions) | Microsoft.Data.Sqlite uses SQLitePCLRaw and supports an explicitly selected native SQLite bundle/provider. |
| Relative Windows open | [RESEARCH-0041](RESEARCH-0041-security-boundary-controls.md) | The accepted local baseline uses `NtCreateFile` `RootDirectory`, final-path/file identity, retained handles, and reparse/hard-link rejection. |

Prototype package/runtime baseline:

- Microsoft.Data.Sqlite.Core `10.0.10`;
- SQLitePCLRaw.bundle_e_sqlite3 `3.0.5`;
- SQLite `3.53.4` with the already asserted native hash/source ID;
- .NET `10.0.9`, x64;
- Windows build `10.0.26200.0`; and
- local NTFS.

## Experiments and raw observations

### Guard-handle semantics

A file opened with read/write sharing but without delete sharing:

- rejected pathname rename with a sharing violation;
- rejected pathname deletion with a sharing violation; but
- did not prevent `CreateHardLinkW` from adding another link.

Consequence: pinning alone is insufficient. The shim must validate link count
when it first opens the file and before every authoritative transaction.

### Shim-VFS opening

The first wrapper attempt retained `DELETE` access on the guard. SQLite's
Windows VFS returned `SQLITE_CANTOPEN` because its new handle did not share the
guard's delete access. Removing `DELETE` from long-lived guards allowed the
exact parent `win32` VFS to open the already pinned database. Auxiliary
deletion, when requested, instead reopens and deletes the validated auxiliary
relative to the retained class handle.

### WAL and lifecycle compatibility

The integrated prototype:

- opened the authoritative database through the named shim VFS;
- retained the exact native binding assertion;
- configured WAL, `synchronous=FULL`, foreign keys, and in-memory temporary
  storage;
- enabled persistent WAL through `sqlite3_file_control`;
- retained and guarded the database, WAL, and shared-memory objects;
- rejected rename and deletion of all three while the store was open;
- retained WAL/SHM after clean close and reopened the store successfully; and
- rejected the next mutation after an external hard link changed the guarded
  database's link count.

The existing fifteen persistence/lifecycle tests passed with the prototype,
including migration, backup/restore, CAS, fencing, checkpoint, and atomic
transition behavior. This compatibility result does not claim that those
non-SQLite file operations yet satisfy EVAL-0080.

## Findings

1. A full custom SQLite I/O implementation is unnecessary for the accepted
   M1 threat model. A shim can retain SQLite's tested Windows locking and I/O
   while establishing Infinium authority before delegation.
2. A pathname plus preflight validation is insufficient. The file must be
   opened relative to a retained directory handle, validated, and kept pinned.
3. Persistent WAL is required for stable guarded WAL/SHM capabilities. It is
   an official SQLite control rather than an Infinium file-lifecycle guess.
4. Link-count revalidation is required because a non-delete-shared file handle
   does not prevent a same-user hard-link addition.
5. The VFS must fail closed on a non-`win32` parent, undeclared auxiliary name,
   on-disk temporary file, reparse object, multiple link, changed file ID,
   unexpected final path, or cross-volume object.
6. This approach fits the existing ADR's “separately qualified equivalent”
   language and does not require an ADR amendment.

## Alternatives

### Full custom Windows SQLite VFS

Rejected for M1 unless the shim later fails qualification. Reimplementing
SQLite file locking, shared memory, file controls, and crash behavior would
add substantially more corruption and maintenance risk.

### Standard VFS after path validation only

Rejected. It leaves a check/use replacement window and fails EVAL-0080.

### Replace WAL with rollback-journal mode

Rejected. ADR-0015 explicitly accepts coordinated WAL behavior and requires
qualification of that selected line.

### Weaken ADR-0021/EVAL-0080 for SQLite

Not needed by the prototype result and therefore not recommended.

## Uncertainty and limitations

- The supported production Windows/NTFS matrix still needs its release
  qualification beyond this host.
- A hostile same-user process can add a hard link after a validation check.
  ADR-0021 explicitly does not claim containment against such a process.
  Infinium revalidates at transaction boundaries and fails closed when the
  changed link count is observed.
- The prototype delegates locking and byte I/O to SQLite's exact `win32` VFS;
  a change in the native VFS name or ABI must fail closed and trigger review.
- Full EVAL-0080 remains pending until all delivered non-SQLite writes use
  opened-object operations and the complete adversarial matrix is rerun.

## Recommendation

Retain and harden the shim-VFS implementation. Next, convert the remaining
Slice 2 product writes to the shared handle-relative primitive:

1. backup database destination, payload copies, and manifest;
2. restore staging, validation, and publication;
3. CAS publication and cleanup;
4. runtime-descriptor temporary creation and atomic replacement; and
5. product/write-class directory creation.

Then execute the complete EVAL-0080 matrix, the accumulated Slice 2 suite, and
a final semantic/diff review before changing the implementation record from
blocked to complete.

## ADR or follow-up enabled

No new ADR is required for this result. If the shim fails supported-host,
crash/recovery, or race qualification, ADR-0021 requires the decision to be
reopened rather than silently falling back to path authorization.
