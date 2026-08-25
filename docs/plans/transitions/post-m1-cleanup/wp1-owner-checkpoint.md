# Cleanup inventory owner checkpoint

Status: Proposed
Disposition: Owner checkpoint requested; read-only inventory; no archive transfer or deletion performed

Prepared: 2026-08-25
Last reviewed: 2026-08-25
Source commit: `6a9c57815716c0fac35381b71e5766b1d7d2f0d0`

## Plain-language result

The cleanup boundary is now explicit. Every active tracked file, every
untracked working-candidate file, every ignored local file, and every
authorized sibling-archive top-level group has a recorded disposition. There
are no undecided entries. The private evaluator fixture repository was not
read. Nothing has yet been moved or deleted.

The exact machine-readable authorities are:

- [active repository cleanup manifest](cleanup-manifest.v1.json);
- [ignored and sibling-root local inventory](local-cleanup-inventory.v1.json).

These two files are self-excluded generated outputs. Regenerating them after
owner acceptance changes the approved scope and therefore requires another
checkpoint before destructive work continues.

## Exact active-repository scope

The manifest contains 1,596 entries:

| Disposition | Files | Meaning |
| --- | ---: | --- |
| `KEEP` | 905 | Retain as current product, governance, research, fixture, test, tooling, or owner working-candidate material. |
| `EXTRACT` | 59 | Preserve current behavior while removing a historical dependency or replacing a planning-named surface. |
| `ARCHIVE` | 621 | Copy to the development-history archive, verify the copy, then remove the active copy. |
| `DELETE` | 11 | Remove incomplete retained-state staging after exact-path revalidation; original tracked blobs remain recoverable from the source commit. |
| `UNDECIDED` | 0 | No unresolved disposition remains. |

The manifest preserves all 11 untracked cleanup working-candidate files as
`KEEP`; none can become cleanup collateral.

The 621 archive files are primarily completed milestone plans/records,
repository-only provider-run schemas and scripts, historical provider fixtures,
and tracked run/effect evidence. The 11 tracked deletions are the incomplete
`artifacts/m1-slice6/successor-product-state/staging/` tree. It is not a usable
database checkpoint.

## Local generated data

The separate local inventory contains 13,420 ignored files totaling about
4.10 GiB:

| Disposition | Files | Approximate size | Meaning |
| --- | ---: | ---: | --- |
| `ARCHIVE` | 13 | 0.10 MiB | Preserve the stale local `human-guide/` once as a dated, non-authoritative documentation snapshot. |
| `DELETE` | 13,407 | 4.10 GiB | Reproducible package cache, build output, test output, dumps, temporary databases, logs, and ignored run artifacts. |

The generated-data cleanup will be implemented as an exact-root, dry-run-first
tool. Its allowed roots are `.packages/`, project `bin/`, project `obj/`,
project `TestResults/`, and ignored root `artifacts/`. It must resolve every
target beneath the repository root and refuse any other path. Tracked files and
ordinary untracked files are never eligible through that tool.

## Sibling archives

- The evaluator-development archive has 8 unique groups (24.32 MiB) approved
  for transfer into the organized development-history archive. Three exact
  duplicate groups and one Python cache group (19.43 MiB total) are approved
  for deletion. Each duplicate names its byte-identical retained counterpart
  in the local inventory.
- The legacy archive retains its unique abandoned source/configuration. Its
  reproducible dependency, build, package, cache, and log output is approved
  for deletion. The Rust `loot-helper` source remains; its nested generated
  `target/` directory does not.
- The legacy archive's `.env` is approved for secret-safe deletion. Its value
  and digest were neither recorded nor printed. If that old credential may
  still be active, provider-side revocation remains an owner action.
- The retired protocol archive remains immutable and is not a cleanup
  destination.
- The private evaluator fixture repository is recorded as excluded and was not
  inspected.

## Fixture inventory and replacement design

The cumulative public registry currently lists 57 package entries. Twenty-seven
point into the historical provider fixture tree; 30 point into current
Bethesda, documentation, candidate construction, platform, lifecycle,
finding-case, scope-reversion, contract-example, and analysis-pipeline evidence.

WP3 will archive the 27 historical provider entries and their packages. It
will replace the cumulative registry with a current-only registry containing
the 30 current entries, subject to functional identity/path cleanup in WP7.
Tests that currently use historical provider packages are marked `EXTRACT`, so
their genuinely current behavior must be supplied with small current fixtures
before the historical inputs disappear.

## Reachability review

- No production project directly reads `contracts/repository/`,
  `fixtures/public/`, or the public registry. The fixture-tooling assembly has
  friend access to application internals for tests, but it is not a production
  dependency.
- References to the historical provider fixture tree are confined to
  manifest-classified historical tools/tests or to `EXTRACT` tests that also
  cover reusable provider behavior.
- Current SQLite migration readers remain `KEEP`. Their old serialized
  migration IDs and database column/table names are compatibility data; they
  will be specifically allowlisted instead of rewritten in place.
- Current Bethesda, analysis lifecycle, findings, analysis-pipeline, research,
  architecture, and product documents remain `KEEP`.
- The ordinary retained-state regression is marked for extraction/removal.
  The required environment variable is not configured and the default tree is
  incomplete staging rather than a database. Synthetic temporary-database
  persistence/replay tests remain the current replacement.
- Local excludes contain only `reference/` and `human-guide/`. No `reference/`
  tree exists; the guide will be archived before both stale excludes are
  removed.

## Secret scan

The scanner examined 1,828 text files without logging matched values. It found:

1. the known legacy `.env`, classified `DELETE_SECRET`; and
2. one deliberately fake, API-key-shaped test canary in
   `ProviderAdapterIntegrationTests.cs`.

No real credential was found in the active repository or the retained portions
of the authorized archives by this scan.

## Baseline verification

The current working candidate passed:

- locked restore;
- Release solution build: 0 warnings, 0 errors;
- analysis-composition contract tests: 3 passed;
- focused replay/composition/cross-stage integration tests: 24 passed;
- documentation validation;
- naming-checker self-test and baseline verification; and
- `git diff --check`.

The naming baseline currently contains 2,573 exact cleanup-debt entries and no
unexplained findings. WP3 and WP7 must reduce cleanup debt to zero, leaving only
individually justified compatibility entries for frozen serialized or migration
identities.

## Checkpoint decision

Owner acceptance of this checkpoint authorizes the exact `ARCHIVE`, `DELETE`,
`DELETE_SECRET`, and sibling-root actions in the two generated inventories.
Any later scope change to those actions requires a regenerated manifest and a
new owner checkpoint.
