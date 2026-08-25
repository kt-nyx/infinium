# Post-M1 cleanup and M2-readiness implementation plan

Status: Accepted
Disposition: Consumed by the completed post-M1 cleanup implementation

Last reviewed: 2026-08-25
Owner: Project owner
Accepted: 2026-08-25
Plan ID: `TRANSITION/POST-M1-CLEANUP`
Parent baseline: owner-accepted M1 product candidate
`926080092e056973d254562424a030672fb4d917`
Planning base: `6a9c57815716c0fac35381b71e5766b1d7d2f0d0`
Active work: None; see [implementation record](implementation-record.md)

## 0. Plain-language outcome

This package turns the completed M1 repository from a record of how the backend
was developed into a clean foundation for future product work.

When it is complete:

- development agents may make intentional OpenAI API calls through one safe,
  budgeted path when a future accepted task genuinely needs them;
- ordinary builds and tests never need a credential, a live provider, or a
  maintainer-local Slice 6 database;
- the one-time Slice 6 campaign implementation and evidence are outside the
  active repository, while genuinely reusable provider behavior remains;
- current public-fixture discovery contains only current conformance packages,
  while historical/rejected provider packages and completed milestone
  chronology resolve through the development-history archive;
- reproducible build, test, dump, package-cache, database, log, and ignored run
  output is removed through a safe repeatable local-hygiene command;
- the legacy and development archives are organized, secret-free, and
  recoverable;
- the retained provider code is divided by responsibility instead of being
  concentrated in campaign-sized files;
- the scope-reversion analyzer labels its current severity and confidence as
  an explicit provisional policy rather than an unexplained universal truth;
- the backend exposes a versioned finding-report projection that M2 can consume
  without interpreting internal storage objects;
- active code, tests, tools, and filenames use functional or architectural
  names; and
- an automated repository rule prevents milestone, slice, work-package,
  campaign chronology, or similar planning language from leaking back into
  implementation names.

This is a transition package between milestones. It does not reopen M1, create
an M1 Slice 10, or activate M2. M1's accepted bytes and historical claims remain
unchanged in Git history. M2 remains a separately planned frontend milestone.

## 1. Owner decisions incorporated by this plan

### 1.1 Development OpenAI credential

The owner has already created a dedicated OpenAI project, applied the USD 10.00
project-level usage limit, created the key presently used for development, and
also created a service account.

This plan does **not** require replacing a secure, dedicated project key merely
because a service account now exists. The current key may continue as the
development credential if it has never been exposed. A service-account key is
preferred for the next ordinary rotation because it gives the credential a
project-owned identity and simpler independent revocation, but migration is not
an acceptance gate for this cleanup.

Any key ever found in plaintext, including a key stored in an archive `.env`
file, is treated as exposed. It must not be copied into another archive or
compared with the active key. The plaintext file is deleted and the owner is
asked to revoke the corresponding provider-side key if it may still be active.

The OpenAI project limit is an outer provider-side boundary shared by every key
in that project. Infinium's local reservation and settlement remain a separate
inner boundary. A service account does not create an independent spending cap.

### 1.2 Standing development use versus shipped-product use

The cleanup will establish standing **development eligibility** for intentional
OpenAI calls needed to implement or validate provider-dependent behavior. Each
call still requires an explicit live invocation, a typed request manifest,
finite request limits, local budget admission, durable usage settlement, and
sanitized provenance. No branch name, commit message, filename, or planning
marker grants runtime authority.

This development permission is distinct from the shipped product contract.
The shipped product continues to use the user's selected provider/account and
must never fall back silently to the project's development credential.

The cleanup itself is effect-free. It requires no provider call, credential
read, network request, or billable use.

### 1.3 Archive disposition

The existing `../infinium-evaluator-development-archive/` directory will be
reorganized into the broader sibling Git repository
`../infinium-development-history-archive/`. This reuses the existing
development archive rather than creating another overlapping archive.

The following repositories remain separate:

- `../infinium-evaluator-archive/` remains the immutable retired protocol `/4`
  archive and is not a destination for product-development history;
- `../infinium-legacy-archive/` remains the abandoned application archive, but
  is cleaned and given a recoverable Git/manifest boundary; and
- `../infinium-evaluator-fixtures/` remains active, private, and default-deny.
  It is not inspected or modified by this plan.

### 1.4 Severity, confidence, reporting, modularity, and naming

The owner accepts these directions:

- retain the present scope-reversion labels as a visible provisional analyzer
  policy, while deferring broad calibration;
- define the backend report meaning before M2 and leave presentation work to
  M2;
- remove historical code before modularizing what remains; and
- perform a complete naming pass plus permanent governance so implementation
  names describe responsibility, domain meaning, or architecture rather than
  development chronology.

## 2. Authority and governing inputs

This plan consumes rather than changes the accepted product meaning in:

- [current project state](../../../current-state.md);
- [development execution policy](../../../execution-policy.md);
- [product scope and milestones](../../../product/scope-and-milestones.md);
- [severity, confidence, coverage, and readiness](../../../product/severity-confidence-and-coverage.md);
- ADR-0013, ADR-0020, ADR-0023, ADR-0025, ADR-0034, and ADR-0035;
- the [M1 and M2 product-conformance profile](../../../evaluation/product-conformance-verification-profile.md);
- the accepted M1 Slice 6 through Slice 9 entries, plans, and records only for
  exact historical dependencies being retired or preserved; and
- the repository's historical-archival separation rule.

This plan adopts accepted ADR-0036, **Development provider access and product
credential separation**. It defines the durable development-only credential,
cost, invocation, logging, and product-isolation boundary without superseding
ADR-0020's user-owned shipped-product credential rule.

## 3. Scope

### 3.1 In scope

- Development-provider governance and removal of the consumed Slice 6 execution
  exception from current policy.
- Functional naming governance in documentation, `AGENTS.md`, automated checks,
  and the accepted verification floor.
- Exact classification of historical versus reusable Slice 6 code, tests,
  repository schemas, engineering scripts, documentation, and local artifacts.
- Whole-repository classification of current versus historical fixtures,
  completed M0/M1 planning records, obsolete public APIs/commands, supported
  compatibility readers, tracked artifact evidence, ignored generated data,
  package caches, test dumps/results, and owner-local excluded documentation.
- External transfer of unique historical material with content manifests and
  deletion of reproducible or secret material.
- Removal of the retained Slice 6 database regression from mandatory and
  ordinary test entry points.
- Removal of one-time campaign commands, authority readers, campaign ledgers,
  exact campaign pricing, attempt materializers, and recovery paths after any
  reusable behavior is extracted.
- Modularization of credential entry/rotation, provider request dispatch,
  OpenAI transport, capability/profile selection, budget accounting, semantic
  admission, persistence, and replay.
- Explicit provisional scope-reversion severity/confidence policy.
- A stable backend finding-report projection for later M2 consumption.
- Repository-wide functional renaming, including less obvious forms of
  development-stage language.
- Navigation, verification, and implementation-record closeout.
- Completion and verification of the already-started analysis-composition
  generalization in the current working candidate.

### 3.2 Out of scope

- M2 UI or desktop-host implementation.
- Full severity/confidence calibration or claims of analyzer reliability.
- A new analyzer or wider Skyrim semantic coverage.
- Private fixtures, held-out evaluation, semantic-oracle work, or evaluator
  protocol restoration.
- Changing historical provider responses, costs, ledgers, accepted commits, or
  evidence identities.
- Renaming frozen serialized identifiers, database migrations, or historical
  evidence in place.
- A live OpenAI conformance campaign during this cleanup.
- Provider-side key revocation performed by Infinium or an agent. The owner
  performs provider-console revocation when needed.
- Merge, push, release, or publication.

## 4. Current cleanup inventory

The planning-base scan and the later whole-repository hygiene pass found these
tracked history groups and local generated-data groups:

| Group | Current count | Current size | Initial disposition |
|---|---:|---:|---|
| Stage-named production files | 16 | 1,100,622 bytes | Extract reusable behavior; archive/delete campaign-only remainder; rename retained files. |
| Stage-named tests | 16 | 742,581 bytes | Keep and rename current behavior tests; archive/delete campaign-history tests. |
| Stage-prefixed repository-only schemas | 86 of 108 | 780,209 bytes | Archive historical authority schemas unless a current non-historical consumer is proven. |
| Slice 6 engineering scripts | 32 | 697,587 bytes | Archive historical runners/reconstructors; replace only genuinely current checks with functional tools. |
| Slice 6 plan/evidence files | 87 | 1,763,945 bytes | Archive full chronology; retain only the smallest current summary required for architecture/navigation. |
| Tracked root `artifacts/` evidence | 201 | about 0.67 MiB | All is Slice 6 development evidence; archive exact unique evidence, then remove the active runtime-artifact tree. |
| Historical/rejected provider fixture packages | 26 packages / 137 files | about 1.11 MiB | Archive with the campaign and remove from the current registry. |
| Entire Slice 6 provider fixture family | 149 files | about 1.19 MiB | Archive unless an exact package is rewritten as a small functionally named current conformance fixture. |
| Completed M0/M1 milestone plans and records | 126 files | about 2.58 MiB | Archive detailed chronology; retain a compact M1 closeout and current navigation only. |
| Ignored build/test output | 6,897 files | about 3.33 GiB | Delete; this includes `bin/`, `obj/`, `TestResults/`, and a 432 MiB hang dump. |
| Local NuGet cache | 2,721 files | about 476 MiB | Delete at final local-hygiene closeout; it is reproducible by restore. |
| Ignored root `artifacts/` output | about 3,787 files | about 313.8 MiB | Delete after secret/private-material screening; do not bulk-archive reproducible run debris. |
| Ignored local `human-guide/` | 13 files | about 100 KiB | Archive as a dated non-authoritative explainer, then remove the stale local exclude and working copy. |

These counts are a starting inventory, not deletion authority. The first
implementation package produces the exact tracked-path and digest manifest.
Every item receives one of four dispositions:

- `KEEP`: current functional product or governance input;
- `EXTRACT`: reusable behavior moves into a functional module before the old
  wrapper leaves;
- `ARCHIVE`: unique historical source or evidence moves to the development
  history archive; or
- `DELETE`: secret, reproducible output, cache, duplicate, or valueless
  temporary material.

No file may be removed while classified `UNDECIDED`.

### 4.1 Known archive candidate groups

The exact manifest must include, at minimum:

- the campaign/successor classes under `src/Infinium.Coordinator/`,
  `src/Infinium.Persistence/`, and `src/Infinium.CredentialHelper/`;
- matching `M1Slice6*` and `Wp*` campaign tests;
- historical `contracts/repository/m1-slice6-*`, `wp4-*`, `wp8-*`, and
  `wp9-*` schemas that have no current product consumer;
- consumed `eng/*m1-slice6*`, `eng/*wp*`, Slice 8, and Slice 9 historical
  harnesses after their current coverage is represented by functional checks;
- the detailed `docs/plans/milestones/m1/slices/s6/` campaign chronology and
  evidence, while preserving a compact active-repository summary and Git
  recovery pointer;
- the other completed M0/M1 plans, amendments, implementation records, and
  evidence directories after their current conclusions are condensed into one
  M1 closeout summary and archive recovery pointer;
- all tracked `artifacts/` content, which is currently Slice 6 campaign,
  credential-recovery, or incomplete retained-state history;
- `fixtures/public/provider/`, old public-fixture registries and repository
  schemas, historical fixture readers/verifiers/resealers, and the tests that
  exist only to preserve their historical bytes;
- historical evaluator-navigation documents whose only remaining role is
  chronology, while retaining a small current evaluator-boundary statement and
  permanent retired-identity denylist;
- the ignored maintainer-local `human-guide/`, whose links and implementation
  tour predate the current documentation layout, as explicitly dated
  non-authoritative documentation;
- the retained Slice 6 product-state regression source and any complete,
  sanitized maintainer-local checkpoint that still exists; and
- superseded evaluator-development staging already held under
  `../infinium-evaluator-development-archive/`.

### 4.2 Known deletion candidates

- `../infinium-legacy-archive/.env` and any other secret-bearing local file;
- legacy `node_modules/`, `dist/`, build outputs, temporary caches/traces, logs,
  TypeScript build-info files, and other reproducible outputs;
- evaluator-development `__pycache__`, bytecode, duplicate diagnostics, and
  generated outputs that add no unique evidence;
- incomplete/reproducible local staging under
  `artifacts/m1-slice6/successor-product-state/` after manifesting what existed;
- every ignored run directory under the root `artifacts/` tree after screening,
  including copied SQLite stores, WAL/SHM files, logs, native binaries, and
  intermediate verification receipts that are not unique accepted evidence;
- all repository-local `bin/`, `obj/`, and `TestResults/` directories,
  including the 432 MiB `testhost` hang dump and old TRX output;
- the repository-local `.packages/` cache after the final restore/test receipt;
- Python bytecode, dump, coverage, binary-log, test-result, temporary database,
  and stale backup files not admitted by the exact archive manifest; and
- stale local `.git/info/exclude` entries for `human-guide/` and nonexistent
  `reference/` after those owner-local paths have been resolved.

### 4.3 Explicit keep/extract boundaries from the hygiene pass

Large or old-looking content is not automatically disposable. The following
remains in the active repository unless a later package proves a narrower
replacement:

- Bethesda, lifecycle, findings, analysis-pipeline, and other current
  developer-conformance fixtures referenced by the current-only registry;
- accepted product documents, current architecture, and ADRs, including
  partially superseded or rejected ADRs whose rationale still explains a
  current decision boundary;
- research investigations and their directly referenced benchmark/qualification
  evidence under `docs/research/investigations/artifacts/`;
- product schemas/codecs, current persistence and replay behavior, and the
  narrow migration readers needed to open every still-supported database;
- dependency manifests and their current update/validation tools; and
- all pre-existing user changes in the working tree.

The obsolete `SourceClaimFixtureReader` APIs marked with compile-time
`Obsolete(..., error: true)`, historical live-package verifier, dead
credential-helper qualification/crash-probe commands, and throw-only native
qualification entry points are removal candidates. If a current audit,
migration, or denial rule depends on a small part of them, WP4 extracts only
that narrow function under a functional name. It does not retain the retired
wrapper.

## 5. Work-package sequence

```text
WP1  Authority, baseline, archive manifest, and naming governance
  -> WP2  Secret cleanup and archive-repository preparation
  -> WP3  Historical campaign and retained-database retirement
  -> WP4  Reusable provider extraction and modularization
  -> WP5  Provisional severity/confidence policy
  -> WP6  Backend finding-report projection
  -> WP7  Repository-wide functional rename and semantic naming audit
  -> WP8  Consolidated review, complete verification, archive proof, and handoff
```

Plan acceptance and activation authorize WP1. WP1 then produces the exact
path/digest/disposition/transfer manifest required by the archival policy. WP2
and its destructive/archive actions wait for one owner checkpoint accepting
that exact manifest. After that checkpoint, WP2 through WP8 may proceed
automatically as each predecessor exit gate passes. Ordinary defects return to
same-candidate correction and re-review under the execution policy.

### 5.1 Package-control matrix

| Package | Direct inputs | Contract maturity/result | Unblocks |
|---|---|---|---|
| WP1 | Accepted M1 baseline, current policies/ADRs, active tree, authorized archive roots | New development-provider ADR and naming governance become accepted only when their exact owner-approved form is recorded; cleanup manifest becomes owner-accepted transfer authority | WP2 |
| WP2 | Owner-accepted transfer manifest and exact archive roots | Archive layout/manifests become immutable recovery evidence; local generated/runtime data is safely reproducible and disposable; no product contract changes | WP3 |
| WP3 | Verified archive destination and classified source inventory | Historical contracts remain immutable archive evidence; current product loses campaign authority, cumulative historical fixture discovery, completed-plan clutter, and the local-database test dependency | WP4 |
| WP4 | Extracted reusable provider behaviors and accepted provider/security ADRs | Current provider seams become producer-consumer-validated; historical compatibility readers remain narrow and explicit | WP5 |
| WP5 | Accepted severity/confidence definitions and frozen scope-reversion contracts | Provisional assessment policy is producer-consumer-validated; frozen v1/v2 result meaning is unchanged | WP6 |
| WP6 | Current run-output/finding/case/coverage/provenance contracts | `FindingReport` remains implementation-active until an actual M2 frontend consumer validates it | WP7 |
| WP7 | Naming governance, retained active code/contracts/tests/tools, compatibility allowlist | Functional implementation names become the accepted active surface; frozen historical identities remain compatibility-only | WP8 |
| WP8 | Coherent WP1-WP7 candidate and both archive manifests | Final retained contracts reach the maturity already allowed by their owners; the report projection remains explicitly implementation-active for M2 | Owner acceptance and later M2 planning |

All packages use the shared recoverable-failure and genuine-escalation rules in
Section 14. Package-local exit gates add evidence; they do not create arbitrary
correction limits.

## 6. WP1 — Authority, baseline, archive manifest, and naming governance

Work ID: `TRANSITION/POST-M1-CLEANUP/WP1`

### Plain-language objective

Establish the rules and exact inventory before anything is moved or deleted.
This package also makes functional naming a permanent repository rule before
the cleanup starts producing replacement code.

### Allowed paths and actions

- Read the active repository and the three explicitly authorized sibling
  archives named in Section 1.3.
- Do not read `../infinium-evaluator-fixtures/`.
- Add a proposed/accepted ADR for development-provider access and product
  credential separation.
- Update `AGENTS.md`, the execution policy, plan navigation, and verification
  documentation.
- Update the work-breakdown notation to recognize a bounded transition-plan
  identifier without pretending the transition is a milestone slice.
- Add `docs/governance/functional-implementation-naming.md`.
- Add a machine-readable naming allowlist and a read-only naming checker.
- Produce an exact cleanup manifest covering tracked and authorized local
  archive candidates, with source path, source Git identity when applicable,
  SHA-256, byte length, classification, destination, rationale, and recovery
  method.
- Include ignored/local material in a separate non-authoritative inventory so
  generated output cannot be mistaken for tracked evidence and owner-local
  files cannot be silently deleted.
- Perform a reference/reachability pass over production projects, solution
  members, repository schemas, fixture registries/readers, engineering entry
  points, completed plan records, local excludes, and supported database
  migration readers.
- Perform secret-name and secret-pattern detection without printing values.

### Functional naming policy

The governance document and `AGENTS.md` must require:

1. Active implementation names describe domain meaning, responsibility,
   behavior, boundary, or architecture.
2. Milestone, slice, work-package, wave, evaluator-stage, campaign-attempt, or
   planning-gate names do not appear in active filenames, namespaces, types,
   members, commands, runtime configuration keys, or new serialized identities.
3. The audit covers direct and disguised forms, including `M1`, `Slice6`, `S6`,
   `WP9`, `WaveE`, plan-derived `Stage`, `C1/C2/C3`, `R1/R2`, `PRE-B2`,
   `Successor`, `Continuation`, `ReplacementCandidate`, `Approach`, and similar
   chronological wording.
4. Words such as `stage`, `phase`, `development`, `candidate`, `recovery`, and
   `generation` remain legal only when they name a real product/domain concept,
   not the code's position in a plan. The manual audit records that distinction.
5. `V1`/`V2` or numeric versions are used only where simultaneous contracts,
   codecs, migrations, or compatibility readers genuinely require version
   distinction. The ordinary current implementation uses a functional name.
6. Historical planning documents and implementation records may use planning
   names because chronology is their function.
7. Frozen wire/schema/database/evidence identities may remain unchanged only
   through an explicit compatibility allowlist entry.
8. Every allowlist entry names the exact path or symbol, category, reason,
   retained consumer, and removal/review condition. Broad directory or token
   exemptions are forbidden.
9. New allowlist entries require explicit review; the checker reports zero
   unexplained findings.

The checker scans tracked active production code, product contracts, current
engineering tools, ordinary tests, project files, and runtime configuration.
It ignores planning/record prose but separately checks that those documents do
not advertise historical identifiers as current entry points.

### Vertical deliverables

- Accepted development-provider ADR and updated execution policy.
- Accepted functional implementation naming governance.
- Root agent instructions that make the naming rule visible to future agents.
- `eng/verify-functional-naming.ps1` or equivalent read-only checker.
- Exact, reviewed cleanup/transfer manifest.
- A current-versus-historical fixture inventory and a current-only registry
  design that removes cumulative historical packages from ordinary discovery.
- A generated-data hygiene specification covering safe exact-root cleanup,
  ignore rules, retained evidence, and final workspace state.
- Baseline build/test receipt for the current working candidate, including the
  completed `AnalysisComposition` generalization.

### Verification

- Naming checker detects representative forbidden names and accepts only exact
  reasoned compatibility exemptions.
- Secret scan reports paths and classifications but never values.
- Every manifest source exists or is explicitly marked absent; every tracked
  source has a source commit and content digest.
- Current solution build, focused analysis-composition tests, documentation
  validation, and `git diff --check` pass.

### Exit gate

No archive mutation or deletion occurs until the manifest has zero undecided
items in the affected group, no proposed destination enters the evaluator
archive or private fixture repository, every reusable dependency has an
extraction target, and the project owner accepts the exact manifest. That
checkpoint is acceptance of exact destructive/transfer scope, not a review of
ordinary implementation mechanics.

## 7. WP2 — Secret, local runtime-data cleanup, and archive preparation

Work ID: `TRANSITION/POST-M1-CLEANUP/WP2`

### Plain-language objective

Make the archives safe and recoverable before adding more historical material,
and remove local generated debris that has no archival value.

### Allowed actions

- Delete the known plaintext `.env` from the legacy archive without reading or
  logging its value.
- Produce an owner-facing instruction to revoke any potentially active key that
  was ever stored there. Provider-console revocation remains an owner action.
- Remove only manifest-classified reproducible/cache/log material from the
  legacy and evaluator-development archives.
- Rename `../infinium-evaluator-development-archive/` to
  `../infinium-development-history-archive/` after resolving both absolute
  paths and proving the destination does not already exist.
- Organize the destination as:

```text
infinium-development-history-archive/
  evaluator-development/
  m1-provider-development/
    source/
    tests/
    repository-contracts/
    engineering/
    documentation/
    retained-state/
  manifests/
```

- Initialize the reorganized development-history archive as a Git repository
  with an archive README, safety guidance, ignore rules, source-identity
  manifest, and first clean snapshot.
- Give the cleaned legacy archive its own Git/manifest boundary linked to
  recoverable source commit `7dd3da6`.
- Add or update repository ignore rules for dumps, coverage output, binary logs,
  TRX results, temporary databases, and equivalent generated files not already
  covered by directory rules.
- Add a safe, repository-root-resolving local cleanup command that can remove
  only an explicit allowlist of `.packages/`, `bin/`, `obj/`, `TestResults/`,
  and ignored root `artifacts/` output. It must support dry-run output and must
  refuse paths outside the resolved repository root.
- After archive admission screening, delete the current ignored build/test/run
  debris, including the hang dump, old test results, temporary SQLite stores,
  logs, copied native libraries, and local package cache. Do not archive this
  reproducible material.
- Archive the ignored `human-guide/` as a dated non-authoritative snapshot,
  verify it, remove its working copy, and remove its stale local exclude entry.
  Remove the `reference/` exclude only after confirming that no such local path
  exists.

### Safety rules

- Resolve and print the exact source/destination roots before moving.
- Never move/delete by unresolved wildcard, broad parent path, `$HOME`, or
  computed cross-shell command.
- Scan archive candidates for credentials, private fixtures, game/mod payloads,
  and prohibited generated binaries before admission.
- Secret-bearing material is deleted, never archived.
- The immutable evaluator archive is read-only and byte-preserved.

### Verification and exit gate

- Archive Git repositories are clean and their manifests reproduce every kept
  file's SHA-256 and byte length.
- Deleted-junk manifests list paths/categories without retaining secret values.
- The local cleanup command's dry run names only approved generated roots, and
  its post-run audit finds no stale dump, TRX, temporary database, run-log,
  build-output, or package-cache file in those roots.
- No tracked path, untracked user change, accepted research artifact, current
  fixture, or archive-admitted item was removed by local hygiene cleanup.
- The protocol `/4` archive commit and snapshot digest remain unchanged.
- The private fixture repository was not read or modified.
- WP3 begins only after the development-history archive is ready to receive the
  exact active-repository transfer.

## 8. WP3 — Historical campaign and retained-database retirement

Work ID: `TRANSITION/POST-M1-CLEANUP/WP3`

### Plain-language objective

Move the one-time Slice 6 machinery out of the product and remove the local
database test that made normal verification depend on historical developer
state.

### Required sequence

1. Copy every `ARCHIVE` item from the reviewed manifest into its exact
   development-history destination.
2. Recompute source/destination hashes and commit the destination archive.
3. Prove archive recovery by reconstructing a sampled source, test, schema,
   script, document, and retained-state item from the committed archive.
4. Remove the corresponding active-repository files and references.
5. Delete items classified `DELETE` only after exact target revalidation.
6. Build and use compile/test failures as reachability evidence, extracting any
   unexpectedly current behavior rather than restoring a historical wrapper.

### Retained Slice 6 database regression

Remove
`RetainedSlice6SuccessorDecisionChainReplaysOfflineWithoutChangingFrozenEvidence`
from the ordinary integration suite, remove
`INFINIUM_M1_SLICE6_RETAINED_PRODUCT_ROOT` from active verification, and remove
the test from Slice 8/9 or successor harness selection.

Archive the test source and a complete sanitized checkpoint only if the exact
checkpoint exists and passes the archive admission scan. The planning machine
currently has no configured retained-root environment variable and only an
incomplete local staging tree at the default path. Incomplete staging is
manifested and deleted rather than presented as the historical database.

Current product persistence/replay remains covered by synthetic, temporary,
repository-independent tests. No mandatory test may skip because a historical
maintainer artifact is absent.

### Historical product-development removal

Retire one-time behavior including:

- finite/successor campaign runners and ledgers;
- campaign authority/version loaders;
- exact campaign pricing and hard-coded campaign identities;
- campaign-specific provider accounting and stage coordination;
- exact attempt/evidence materializers and one-off recovery commands;
- dormant credential-helper command-line entry points used only by those
  campaigns; and
- repository schemas, engineering scripts, and detailed authority artifacts
  whose only consumer was the retired campaign.

This package also performs the broader history extraction proven by WP1:

- archive and remove all tracked root `artifacts/` evidence;
- archive and remove `fixtures/public/provider/`, the historical/rejected
  packages from ordinary discovery, old cumulative registries, obsolete
  source-claim/candidate readers, the historical live-package verifier, and
  their campaign-only tests and resealing tools;
- introduce one current-only public-fixture registry and update functional
  readers/tests to use it; its packages are current conformance evidence, not a
  chronology of every prior package;
- resolve the current registry's `Proposed` provider-contract example: either
  accept it as bounded current conformance evidence with a functional identity,
  or keep it as an unregistered answer-free contract example; a proposed item
  does not remain in current discovery by inertia;
- archive stage-prefixed repository-governance schemas and completed
  engineering harnesses after current behavior has a functional check;
- archive detailed completed M0/M1 milestone/slice plans, amendments, records,
  and evidence after producing a compact M1 closeout summary with exact archive
  repository/commit recovery information; and
- reduce active evaluator-history metadata to the smallest current boundary and
  retired-identity protection needed to prevent accidental reuse.

The current active tree must not preserve old versions merely so one registry,
test, or validator can assert that the newer historical version appended the
older historical version. Git and the development-history archive own that
chronology.

Before removal, extract reusable credential enrollment/rotation, provider
dispatch, accounting, admission, replay, and persistence behavior into the WP4
targets. Historical serialized IDs remain readable only when a current replay
or migration requirement is proven.

### Verification and exit gate

- Active `src/`, active contracts, and current engineering entry points contain
  no route that can resume or retry the retired campaign.
- Ordinary fixture discovery contains no historical/rejected provider package,
  and a repository-wide search finds no active reader or test whose only
  purpose is historical package integrity.
- No tracked file remains under root `artifacts/`; runtime/test output is
  ignored and locally disposable.
- Completed M0/M1 chronology resolves through the compact closeout to a clean,
  hash-verified archive commit rather than through detailed active-tree plans.
- The solution builds without archived files or archive paths.
- Unit/contract/integration/security/fault tests for current provider behavior
  pass offline.
- The complete Integration category has zero skip caused by the removed
  retained database.
- A clean copy of the active repository can run its required floor with all
  sibling archives unavailable.

## 9. WP4 — Reusable provider extraction and modularization

Work ID: `TRANSITION/POST-M1-CLEANUP/WP4`

### Plain-language objective

Keep the parts Infinium will need for future agentic analysis, but make each
part small enough to understand and test independently.

### Target modules

- `OpenAiResponsesAdapter`: request serialization, bounded transport, response
  capture, and provider error classification.
- `ProviderCredentialStore`: exact-target Credential Manager operations and
  non-secret profile/generation state.
- `ProviderCredentialEnrollment`: enrollment, verification, rotation, disable,
  and deletion without campaign terminology.
- `ProviderRequestAuthority`: final credential/profile/deadline/capability and
  budget checks immediately before dispatch.
- `ProviderUsageBudget`: reservation, settlement, unknown exposure, and
  reconciliation.
- `OpenAiProviderProfileCatalog`: allowed model, endpoint, feature, and pricing
  snapshots.
- `ProviderProposalAdmissionPolicy`: prompt fidelity plus proposal, support,
  applicability, and host-decision validation.
- `ProviderEvidencePersistence`: current provider attempt/result/usage and
  replay storage, separated from historical campaign migrations.
- `RetainedProviderEvidenceReader`: effect-free reading of accepted retained
  evidence where a current consumer exists.

Historical compatibility is not a general museum for retired code. For every
historical reader, codec, denylist, or migration path, record the supported
current input that requires it and a removal condition. In particular:

- remove the compile-blocked obsolete source-claim fixture APIs rather than
  retaining methods that no caller can legally invoke;
- remove the credential-helper's terminally disabled qualification and crash
  probe CLI switches and their throw-only methods/tests;
- replace the full Slice 6 authority/campaign loaders with a small functional
  retired-identity registry only where current safety rules must refuse reuse;
  and
- retain legacy SQLite readers only when a supported store can still contain
  those bytes, with direct migration/replay tests that do not depend on a
  maintainer database.

Large coordinator/helper `Program` files are reduced to argument parsing and
dispatch into these modules. Persistence files are split by current stored
concept; old schema numbers remain only in migration/compatibility code.

### Development API entry point

Add one explicitly live development/conformance entry point with:

- a clear `-Live` or equivalent opt-in;
- a closed typed manifest naming operation, credential profile, model/profile,
  maximum input/output, deadline, and maximum local cost;
- use of Credential Manager without revealing or exporting the key;
- project/account metadata sufficient to detect wrong-profile selection
  without storing the secret;
- local reservation/settlement no greater than the owner-configured project
  boundary;
- no fallback to another key, account, provider, or model;
- sanitized request ID, token, cost, and outcome evidence; and
- a separate offline fake-provider mode used by automated tests.

The existing secure dedicated project key is permitted. A service-account key
may be enrolled later as an ordinary new credential generation. The cleanup
does not compare, reveal, or rotate keys and does not make a live call.

### Verification and exit gate

- Every module has positive, malformed, failure, cancellation, and replay tests
  appropriate to its responsibility.
- Credential canaries remain absent from command lines, environment, IPC,
  persistence, logs, diagnostics, outputs, and archives.
- Provider requests cannot start from ordinary test execution.
- Fake-provider end-to-end analysis proves the complete current path.
- No retained module references a Slice, WP, campaign attempt, or archived
  source path except an exact allowlisted migration/compatibility reader.
- No active command exists only to print that it is disabled, and no active
  public API exists only to fail at compile time or throw unconditionally.
- Every retained historical compatibility path names a current supported input
  and has positive, malformed, and refusal evidence.

## 10. WP5 — Provisional severity and confidence policy

Work ID: `TRANSITION/POST-M1-CLEANUP/WP5`

### Plain-language objective

Make the current labels honest and explainable without pretending Infinium has
already calibrated severity and confidence across many problem types.

### Deliverables

- Add `ScopeReversionAssessmentPolicy` as the single owner of the current
  `Moderate` severity and `StronglySupported` confidence assignment.
- Give the policy a functional version and an explicit basis:
  - the demonstrated consequence is meaningful but bounded within the current
    proof scope;
  - the supported finding uses several exact evidence links and no material
    contradiction within that scope; and
  - the analyzer remains `Experimental` and the labels are not cross-analyzer
    calibrated.
- Remove duplicated literal assignment from v1/v2 analyzers and invariants
  where possible without changing frozen contract meaning.
- Preserve accepted v1/v2 serialized results. If an additive basis/reference is
  needed, put it in the new report projection rather than revising a frozen
  contract in place.
- Document the known limitation and the later calibration work required before
  M3 reliability/readiness claims.

### Verification and exit gate

- Existing accepted cases retain exactly the same severity/confidence values.
- Policy tests prove the basis, analyzer scope, and experimental maturity are
  visible.
- Another analyzer cannot silently inherit this policy.
- No output describes `Moderate` or `StronglySupported` as universally
  calibrated.

## 11. WP6 — Backend finding-report projection

Work ID: `TRANSITION/POST-M1-CLEANUP/WP6`

### Plain-language objective

Give M2 one clear backend object that says what a user-facing finding report
means, while keeping internal evidence/storage objects intact underneath it.

### Proposed functional contract

Add a versioned `FindingReport` projection containing:

- stable report, run, analyzer, finding, case, and subject identities;
- a concise title and plain-language conclusion;
- what happened and why it matters;
- affected mods/plugins/records/assets or other exact subjects;
- consequence type and effect-extent taxonomy assignments;
- severity, confidence, analyzer maturity, and the basis for each;
- supporting and contradicting evidence references;
- assumptions, applicability conditions, uncertainty, and unresolved questions;
- coverage populations, failures, exclusions, and gaps relevant to the report;
- recommended action and validation steps;
- replay/source provenance; and
- explicit unsupported/not-established statements.

The projection is derived from retained product output. It neither invents new
evidence nor mutates findings. Raw run output remains the canonical analysis
artifact. The projection is a stable presentation contract, not the M2 visual
layout.

### Contract maturity

The report contract begins `Implementation-active`. It gains a real backend
producer, canonical JSON codec, validator, malformed fixtures, and a simple
test/query consumer during this package. It remains open to an explicit
clean-break revision until M2 validates it with the actual frontend consumer.

### Verification and exit gate

- Positive, resolved-negative/lead-only, abstention, failure, limited, and
  coverage-gap examples project truthfully.
- Missing evidence or unsupported scope remains visible rather than becoming
  an empty success.
- Projection is deterministic and replay-equivalent.
- Hostile text remains data and is not converted into markup or instructions.
- Frozen run-output bytes remain unchanged.

## 12. WP7 — Repository-wide functional rename and semantic naming audit

Work ID: `TRANSITION/POST-M1-CLEANUP/WP7`

### Plain-language objective

Rename everything that remains according to what it actually does, then run a
second human review for planning language that a simple search might miss.

### Required obvious renames when the targets remain

| Current name | Functional target |
|---|---|
| `M1ProviderCatalog` | `OpenAiProviderProfileCatalog` |
| `Wp9ProductionEnrollmentSurface` | `ProviderCredentialEnrollmentSurface` |
| `Wp9ProductionProfileEnrollmentRunner` | `ProviderProfileEnrollment` |
| `M1Slice6CampaignProviderAccounting` | `ProviderUsageAccounting` |
| `M1Slice6CampaignSemanticAdmission` | `ProviderProposalAdmissionPolicy` |
| `M1Slice6CampaignV2InputAdapter` | `RetainedProviderEvidenceAdapter`, if retained |
| `AuthoritativeStore.M1Slice6SuccessorV6` | split by current stored function; old version name remains only in migration history |
| `CrossStageCorpusIntegrationTests` | `AnalysisPipelineCorpusIntegrationTests` |
| `ManagedCrossStageCorpusIntegrationTests` | `ManagedAnalysisPipelineCorpusIntegrationTests` |
| `M1Slice9ControlledCompositionIntegrationTests` | `ControlledAnalysisCompositionIntegrationTests` |
| `M1Slice9ControlledHandoff` | `ControlledAnalysisHandoff` |
| `Wp8PreLiveReadinessContractTests` | `ProviderDispatchReadinessContractTests`, if current |
| `verify-cross-stage-corpus.ps1` | consolidate into `verify-analysis-pipeline.ps1`, or rename to the exact functional corpus responsibility |
| `M1-S7-SYNTHETIC-v1` fixture identity/path | a functionally named scope-reversion conformance identity in the current-only registry |
| `M1-S9-SYNTHETIC-v1` fixture identity/path | a functionally named analysis-composition conformance identity, if the standalone fixture remains |
| `m1-continuation-verification-profile.md` | `product-conformance-verification-profile.md` |

Campaign-only types are archived, not cosmetically renamed.

### Comprehensive second pass

After the obvious search is clean, reviewers inspect:

- filenames and directory names;
- namespaces, types, members, test names, commands, options, environment keys,
  configuration fields, logs, errors, and comments;
- schema IDs, JSON property names, protobuf names, SQL tables/columns, migration
  names, and persisted artifact kinds;
- public fixture identities/paths, registry versions, tooling names, local
  ignore entries, and generated-artifact directory conventions;
- terms such as `stage`, `phase`, `campaign`, `successor`, `continuation`,
  `pre-live`, `post-success`, `replacement`, `recovery`, `candidate`, `current`,
  `next`, `final`, `new`, `old`, and version suffixes; and
- synonyms or abbreviations that encode chronology without matching the
  automated token list.

Each occurrence is classified as functional, historical compatibility,
migration identity, planning/record prose, archive-only, or noncompliant. The
final inventory contains zero unexplained occurrences.

Frozen identities such as the serialized `m1_slice9_composition` property or a
historical migration ID remain byte-compatible through an exact allowlist and
functional code-facing alias. They are not reused for new output.

### Verification and exit gate

- The automated naming checker reports zero unexplained findings.
- A manual semantic naming review reports `ACCEPT`.
- No serialized round-trip, migration, replay, CLI compatibility, or stored
  artifact identity changes accidentally.
- Documentation links and code references use the new functional names.

## 13. WP8 — Consolidated review, complete verification, and handoff

Work ID: `TRANSITION/POST-M1-CLEANUP/WP8`

### Plain-language objective

Prove the smaller repository still provides the accepted M1 backend behavior,
that history can be recovered from the right archive, and that M2 can plan
against clear current contracts.

### Consolidated review

Review the complete candidate for:

- semantic preservation and frozen-contract compatibility;
- credential secrecy and separation of development/product authorization;
- provider and billable effect closure;
- local budget correctness and no silent fallback;
- persistence, migration, backup/restore, and replay;
- archive completeness, deletion scope, and recovery;
- absence of active references to archive paths;
- severity/confidence honesty;
- report-projection truth and uncertainty;
- naming governance and final rename completeness;
- private/evaluator isolation;
- documentation/current-state accuracy; and
- the complete diff, including the carried-in `AnalysisComposition` change.

Findings are classified under the development execution policy. All must-fix
findings are corrected and the changed surface is re-reviewed before the final
floor.

### Complete verification floor

Run the accepted common floor from the product-conformance profile, plus:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-functional-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate All -OutputRoot artifacts/post-m1-cleanup-final/analysis-pipeline
```

Also prove:

- all ordinary tests pass with no OpenAI credential available;
- all ordinary tests pass with the sibling archives unavailable;
- no test reads `INFINIUM_M1_SLICE6_RETAINED_PRODUCT_ROOT`;
- no cleanup test starts a provider/network/billable effect;
- no repository-owned test process remains after verification;
- the active repository contains no secret or archive payload;
- the current public-fixture registry contains no historical/rejected package
  and old cumulative registries are archive-only;
- there are no active compile-blocked APIs, throw-only retired entry points, or
  commands whose sole behavior is reporting that they are disabled;
- the development-history and legacy archives are clean Git worktrees;
- archive manifests and sample recovery pass;
- the immutable evaluator archive is unchanged; and
- the private fixture repository was neither read nor modified.

After the passing final receipt is bound and repository-owned processes are
zero, run the safe local cleanup command once more. The final handoff records
the passing source/contract candidate first, then confirms that generated
`bin/`, `obj/`, `TestResults/`, ignored runtime artifacts, dumps, and the local
package cache were removed without changing tracked bytes. Future restore and
test runs may recreate these ignored files; they remain disposable local data,
not repository content.

### Documentation closeout

- Update `docs/current-state.md` only after the owner accepts the final cleanup
  candidate.
- Replace consumed Slice 6 exception language with the standing development
  provider policy.
- Rename/update the current conformance profile and remove stale statements
  that Slice 6 or Slice 7 is still pending.
- Link the functional naming governance from active navigation and agent
  instructions.
- Create one implementation record containing the transfer manifest identities,
  archive commits, deletion classifications, test counts, review result,
  remaining limitations, and exact final candidate.
- Replace detailed completed M0/M1 navigation with one compact M1 closeout that
  states what shipped in the backend, the accepted verification receipt, and
  exact archive recovery coordinates.
- State that M1 remains complete and M2 remains separately authorized.

### Final acceptance criteria

The package is ready for owner acceptance only when:

1. every transferred file is committed and hash-verified in its destination;
2. every deletion is exact, classified, and recoverable when recovery is
   promised;
3. the active solution has no dependency on a sibling archive or historical
   Slice 6 checkpoint;
4. reusable provider behavior is functional, modular, offline-testable, and
   capable of later explicitly live development use;
5. the product cannot use the development credential as a silent fallback;
6. severity/confidence policy and report projection are truthful and tested;
7. the naming checker and manual naming audit have zero unexplained findings;
8. the consolidated review returns `ACCEPT`;
9. the complete final floor passes on one exact candidate; and
10. repository-owned test-process survivors equal zero;
11. the active fixture registry and ordinary tooling contain current
    conformance inputs only; and
12. the final local hygiene audit finds no generated debris in the explicit
    cleanup roots and no tracked-byte change after cleanup.

## 14. Recoverable failures and genuine escalation

Ordinary compile failures, stale links, missing tests, incorrect classifications,
archive-manifest mismatches, schema-reader dependencies, naming-check false
positives, and modularization defects are recoverable work. Correct the same
candidate, rerun focused checks, and re-review.

Escalate only the affected path if:

- an item contains private evaluator material or a secret whose safe handling
  is not covered here;
- the only way to remove historical machinery would change accepted product
  meaning or a frozen contract rather than adding a compatibility seam;
- an exact archive destination conflicts with the immutable evaluator archive
  or an existing unrelated repository;
- a required current behavior cannot be separated from campaign authority
  without selecting new architecture;
- a provider-side credential must be revoked and the owner has not completed
  that external action; or
- a destructive target cannot be resolved and proven to remain within the
  exact named archive/repository root.

Independent in-scope work continues when one path is escalated.

## 15. Expected result for the larger product

This cleanup does not add another kind of modlist problem. Its value is that it
turns the M1 proof into a maintainable backend platform: future analyzers and
the M2 interface can depend on functional provider, evidence, reporting, and
replay components without inheriting the one-time campaign that happened to
create them.
