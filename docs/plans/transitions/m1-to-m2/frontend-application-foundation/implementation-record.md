# M1-to-M2 Foundation — Frontend Application Foundation implementation record

Status: Accepted
Disposition: Checkpoint C under correction; WP1-WP4 remain accepted, WP5-WP6 receipts are suspended, and Phase D is blocked

Last reviewed: 2026-08-26
Owner: Project owner
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Planning base: `32dbb2c48754666336d2da571e554ad8897ed71c`

## Plain-language state

Phase A verified the application authority surface. The Phase B correction
binds typed prepared runs to the exact retained inputs and supported durable
analysis operation it claims to execute. WP3 and WP4 passed focused correction,
consolidated re-review, and the complete accepted verification floor. The first
Phase C candidate reached `c551c12e22522e7a2cef8c21a322aa76db8fc23e`, but its
Checkpoint C receipt remains suspended. Independent corrections now cover
report publication/readback, recursive request validation, export deletion,
provenance, paging, and populated migration gaps. Targeted-verification
execution remains fail closed without durable mutation. RESEARCH-0058,
Proposed ADR-0038, and the Proposed WP6 addendum now document a candidate
architecture, but they are not accepted and no production surface has changed.

The implementation has assigned every foundation capability to an exact owner,
and implemented the common and setup-to-live-run boundaries future frontend
work can build on. WP3 adds typed
tool/profile/configuration setup, honest estimates, and non-secret provider
status. WP4 adds prepared manual-run initiation with immutable durable bindings,
receipts, progress, reconnect, and restart behavior. Phase C code exists only as
an under-correction candidate. No product UI, provider effect, or generic native
authority was added.

## Package status

| Phase | Work package | Status | Accepted candidate/evidence |
|---|---|---|---|
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1` | Accepted on phase candidate | Receipt below; final phase commit deferred until Checkpoint A |
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP2` | Accepted after correction | Corrected receipt and complete floor below |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3` | Accepted after correction | Earlier receipt retained as superseded evidence; corrected receipt below |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4` | Accepted after correction | Earlier receipt retained as superseded evidence; corrected receipt below |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5` | Under correction | Earlier receipt below is suspended pending corrected producer/consumer evidence |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6` | Under correction | Earlier receipt below is suspended; targeted verification has an architecture decision gap |
| D | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP7` | Not started | None |
| D | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP8` | Not started | None |
| E | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP9` | Not started | None |

## Phase checkpoint receipts

### Checkpoint A receipt

Candidate: the correction commit containing this record on branch
`codex/frontend-application-foundation-phase-a`, based on Phase A candidate
`4ddd31f6244b325fc401b2fc2fe29ad429d79ef8`. The exact immutable correction
commit is reported by the orchestrator after commit creation; the intended
post-commit working tree is clean.

Completed packages: WP1 and WP2 only. Phase B, M2 activation, desktop UI,
provider effects, private evaluation, merge, push, and publication are absent.

Complete accepted verification floor:

| Command | Passed | Failed | Skipped/result |
|---|---:|---:|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | — | 0 | Dependency graph current |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | — | 0 | 0 warnings, 0 errors |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Unit"` | 247 | 0 | 1 expected skip |
| same command, `TestCategory=Contract` | 188 | 0 | 0 skipped |
| same command, `TestCategory=Integration` | 120 | 0 | 0 skipped |
| same command, `TestCategory=Evaluation` | 89 | 0 | 9 expected skips |
| same command, `TestCategory=Security` | 152 | 0 | 3 expected skips |
| same command, `TestCategory=Fault` | 117 | 0 | 3 expected skips |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | 710 | 0 | 10 expected skips |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | — | 0 | No formatting drift |
| `eng/update-dependency-manifest.ps1 -Check` | — | 0 | No dependency drift |
| `eng/validate-documentation.ps1` | — | 0 | 150 metadata files, 152 link sources, 19 documentation JSON files |
| `eng/verify-functional-naming.ps1` | — | 0 | 177 reviewed exceptions, 0 unexplained findings |
| `git diff --check` | — | 0 | Clean diff |

The Contract floor includes strict JSON/schema closure, protobuf compilation
and compatibility, unknown-field/enum rejection, canonical round trips, exact
application fingerprint, renderer-registry digest, and generated C# binding
drift coverage. Every one of the 14 floor steps ran the exact-root cleanup
procedure for `Z:\Development\Large Projects\Skyrim\infinium`; each reported
zero repository-owned `dotnet` or `testhost` survivors.

Consolidated review: `ACCEPT` after all must-fix findings listed in the WP
receipts and these final corrections:

- Must fix: the first application fingerprint calculation used the wrong path
  root/normalization. The repository's existing exact-byte compatibility test
  caught it. The renderer correction adds explicit non-success protobuf enum
  values, and the newly bound full contract fingerprint is
  `9fd040e628aa5708f6fd570de1a3d20214e115c6014e660d82b1581d0db15d28`.
- Must fix: planning labels appeared in active schema/test/service strings.
  Replaced them with functional ownership/reason text; the naming verifier now
  reports zero unexplained findings without adding an allowlist exception.
- Must fix: the reference bootstrap codec rejected unknown protobuf fields but
  the live gRPC handler did not yet invoke that validation. The handler now
  fails closed with a typed invalid-argument result, covered through the real
  named-pipe client.
- Follow-up: all Phase B-D feature workflows and product consumers remain with
  WP3-WP8. No later operation is implied by the common substrate.
- Non-blocking: feature-specific fields remain proposed or
  implementation-active under their later owners; only common primitives are
  producer-consumer-validated at Checkpoint A.
- Owner/authority decision: none.
- Safety/isolation breach: none.

Checkpoint result: Phase B is automatically unblocked by the accepted plan,
but it was not started. M2 remains inactive.

## WP1 receipt — Capability and authority inventory

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1`

Candidate: mutable Phase A working candidate based on
`d363347339c83d76787de71f6ef1cf4d30288040`; package binding is intentionally
deferred until the review-ready Phase A candidate passes the complete floor.

Delivered:

- strict `application-contract-inventory.v1` schema and answer-free instance;
- exact inventory of 22 declared application RPCs, 16 implemented handlers,
  current generated/native/test consumers, persistence/readback paths, bounds,
  versions, requirements, evaluation cases, and security authorities;
- explicit denial inventory for direct renderer gRPC, raw orchestration,
  snapshot path inputs, retained-artifact diagnostics, provider/accounting,
  credential enrollment, provider dispatch, and every generic privileged
  primitive;
- capability matrix status changed to implementation-active with 19 of 19
  capabilities owned, zero unknown or unowned entries, and exact Phase/WP
  ownership; and
- protobuf README limits/version/fingerprint reconciled with the implementation.

Focused verification:

- `dotnet restore <absolute Infinium.ContractTests.csproj> --locked-mode --nologo`:
  passed; 8 projects restored, 0 failures.
- `dotnet test <absolute Infinium.ContractTests.csproj> -c Release --no-build
  --filter 'FullyQualifiedName~ApplicationFoundationAuthorityContractTests|
  FullyQualifiedName~SchemaCompatibilityTests|
  FullyQualifiedName~ProtoAuthorityContractTests'`: 14 passed, 0 failed,
  0 skipped.
- `eng/validate-documentation.ps1`: passed; 150 metadata files, 152 Markdown
  link sources, and 19 documentation JSON files.
- Every run used the exact resolved root
  `Z:\Development\Large Projects\Skyrim\infinium`; repository-owned
  `dotnet`/`testhost` survivors after each run: 0.

Review result: `ACCEPT` after correction.

- Must fix: the matrix described provider profile/enrollment RPCs as existing
  behavior although all six provider RPCs are schema-declared without
  coordinator overrides. Corrected the evidence/gap and made all six explicit
  `declared-unimplemented` inventory entries.
- Must fix: `contracts/protobuf/README.md` retained older queue/filter/
  capability/staging/deadline values and protocol `1.2.0` fingerprint text.
  Corrected it to the accepted runtime values and exact `1.3.0` fingerprint.
- Follow-up: setup, typed run, result, review, export, TypeScript, and desktop
  operations remain assigned to WP3-WP8 and were not implemented.
- Non-blocking: `ListFindings` currently produces only an empty unfiltered page;
  the gap is explicit and remains owned by WP5.
- Owner/authority decision: none.
- Safety/isolation breach: none. No archive, private fixture, provider,
  credential, network, or billable operation was used.

## WP2 receipt — Common application and renderer contract substrate

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP2`

Candidate: same mutable Phase A candidate based on
`d363347339c83d76787de71f6ef1cf4d30288040`; package binding is deferred to
the single Checkpoint A commit.

Delivered:

- application protocol `1.4.0` with an implemented bounded
  `GetApplicationBootstrap` projection and exact 20-run recent-work ceiling;
- separate application `1.4.0`, domain `1.3.0`, storage `1.10.0`, and renderer
  `1.0.0` compatibility axes, with full protocol fingerprint
  `9fd040e628aa5708f6fd570de1a3d20214e115c6014e660d82b1581d0db15d28`;
- common typed application errors, optimistic revision/conflict tokens,
  user-operation receipts, and transport-cancellation receipts;
- closed renderer request/response/event envelope, exact three-operation
  registry, deterministic registry digest
  `b218f5a18198f9e9a2b1ee4ccc4b2ada88a6f8c3668dfca4d2d956f6fbd75704`,
  and a display-safe bootstrap adapter;
- strict reference validators that reject malformed, oversized, unknown,
  default, incompatible, stale, replayed, out-of-order, wrong-session, and
  replay-window-exhausted input; and
- generated C# protobuf consumers plus strict JSON/schema and deterministic
  registry-input drift tests. TypeScript product generation remains WP7.

Focused verification:

- coordinator project restore/build: passed after correcting one protobuf
  namespace error; 0 warnings and 0 errors on the corrected build.
- combined foundation, substrate, schema-compatibility, and protobuf-authority
  contract/security selection: 19 passed, 0 failed, 0 skipped.
- named-pipe producer/consumer integration selection
  `CliCoordinatorWorkerNamedPipeFlowCompletesAndInspectsImmutableBindings`:
  1 passed, 0 failed, 0 skipped after restoring/building the solution launch
  artifacts.
- every run used the exact resolved root
  `Z:\Development\Large Projects\Skyrim\infinium`; repository-owned
  `dotnet`/`testhost` survivors after each run: 0.

Review result: `ACCEPT` after correction.

- Must fix: the first bootstrap readback used the ascending run list and would
  have returned the oldest records while naming them recent. Added the bounded
  `AuthoritativeStore.ListRecentRuns` readback and preserved deterministic
  chronological presentation of the selected recent window.
- Must fix: the first renderer response envelope had typed outcome/error
  vocabulary but no closed bootstrap payload. Added the exact nested bootstrap
  projection and a validating native adapter so protobuf producer data and the
  renderer consumer shape are exercised together.
- Must fix: application, domain, and storage compatibility had reused the
  application version string even though they are independent axes. Bound each
  axis to its current contract version and updated application and worker
  handshake checks together.
- Must fix: the first focused integration invocation lacked the solution-built
  CLI executable. Restored and built the solution launch artifacts, then reran
  the same integration path successfully.
- Follow-up: saved configuration, provider enrollment, run preparation,
  finding queries, review actions, export, TypeScript generation, and the
  desktop product consumer remain owned by WP3-WP8.
- Non-blocking: the registry contains only bootstrap, transport cancellation,
  and resync because later feature operations have not reached their owning
  implementation package.
- Owner/authority decision: none.
- Safety/isolation breach: none. No generic renderer operation, direct gRPC,
  listener, path, SQL, command, URL, credential, provider, filesystem, or
  coordinator-proxy authority was introduced.

### WP2 correction receipt — Renderer contract consistency

Candidate: mutable correction candidate on branch
`codex/frontend-application-foundation-phase-a`, based on Phase A candidate
commit `4ddd31f6244b325fc401b2fc2fe29ad429d79ef8`. The exact immutable
correction commit is reported by the orchestrator after commit creation.

Delivered:

- one exact five-message registry: bootstrap request/response, transport-
  cancellation request/response, and resync-required event;
- operation-specific bounded payload schemas shared by the registry-driven
  tests and the runtime validator;
- accepted responses plus explicit rejected, conflict, unsupported,
  unavailable, cancelled, indeterminate, and resync-required non-success
  responses, with request-only gesture proof;
- one-shot gesture and request replay protection whose session state advances
  only after schema, registry, session, ordering, replay-window, and typed-
  outcome validation all succeed; and
- additive protobuf non-success values and validators that reject unspecified,
  unknown, numerically undefined, or success-disguised failure states.

Focused verification:

- corrected application/renderer substrate plus exact protobuf-fingerprint
  selection: 35 passed, 0 failed, 0 skipped;
- two earlier build-only focused attempts found analyzer-required return-type
  and MSTest-attribute corrections; both were corrected on the same candidate;
- every focused run used the exact resolved root
  `Z:\Development\Large Projects\Skyrim\infinium`; repository-owned
  `dotnet`/`testhost` survivors after each run: 0.

Review result: `ACCEPT` after focused correction, correction re-review, and
the corrected complete floor. This evidence reinstates Checkpoint A.

- Must fix: the original renderer schema permitted mismatched operation/kind
  pairs, required cancellation gesture data on responses, and could combine
  success payloads with failure vocabulary. Replaced it with five exact
  envelope branches and disjoint accepted/non-success payloads.
- Must fix: the original registry named message kinds but did not bind exact
  payload shapes or outcomes. Added exact per-message shapes, gesture rules,
  and closed outcome sets; runtime definitions are parsed from that validated
  deterministic input.
- Must fix: the original validator committed request identity before replay-
  window validation and did not retain one-shot gestures. Validation now
  stages all state and commits request, gesture, and sequence together only
  after every check passes.
- Must fix: the denied-authority registry schema could omit one denied name,
  and protobuf conflict validation could accept an undefined numeric
  disposition. Both schemas/validators now reject those cases.
- Follow-up: TypeScript generation, real desktop-host session ownership, and
  feature workflow operations remain WP7/WP8 and WP3-WP6 respectively.
- Non-blocking: the registry intentionally remains limited to the three Phase
  A operations and five legal messages; no Phase B operation is implied.
- Owner/authority decision: none.
- Safety/isolation breach: none. No private/archive, provider, network,
  credential, generic path, SQL, command, URL, filesystem, or coordinator-
  proxy authority was accessed or added.

## Superseded WP3 receipt — Setup, profile, configuration, estimate, and enrollment status

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3`

Candidate: mutable Phase B working candidate based on accepted Phase A commit
`59b6de3d80443c0150c15c8d83b5b29d1b3536ef`; package binding is deferred to
the single Checkpoint B commit.

Delivered:

- application protocol `1.5.0` with exact full-contract fingerprint
  `c8b540b067fea288ff3c31c1ac71c46a0541812aa0aa4fc4efae96eba8eb7824`,
  typed `GetSetupState`, and `SubmitSetupCommand` RPCs with closed
  MO2/LOOT-specific location fields;
- exact `available`, `missing`, `unsupported`, `misconfigured`, and
  `not-yet-validated` tool states without tool invocation or network access;
- bounded MO2 profile discovery plus exact canonical `ModOrganizer.ini`
  saved-selection decoding, kept separate from explicit confirmation;
- versioned saved-configuration create/clone/update/delete with optimistic
  revisions, durable replay-safe receipts, restart, and readback;
- immutable prepared effective configuration with a separate supplied semantic
  context and explicit work/time/cost/provider-dispatch availability states;
- non-secret provider enrollment intent/status that truthfully reports native
  secret entry and secure-store choreography unavailable in this phase; and
- schema `12`, storage contract `1.11.0`, exact migration fingerprint
  `e3dcd08192656fcc24b8374198bb1fbf66d9dd75fc6cf160b2558be16059b3ce`,
  backup/restore identity integration, and append-only setup receipts.

Focused verification:

- `ApplicationSetupPersistenceTests`: 2 passed, 0 failed, 0 skipped;
- application/renderer/inventory/fingerprint contract selection: 37 passed,
  0 failed, 0 skipped; and
- every focused run used the exact repository root and reported zero
  repository-owned `dotnet` or `testhost` survivors.

Review result: `ACCEPT` after correction and focused re-review.

- Must fix: an early enrollment-intent path invoked credential persistence
  before the setup revision receipt was committed. Removed that cross-store
  side effect; Phase B now records only a replay-safe non-secret intent and
  truthfully reports that native secret entry is unavailable.
- Must fix: profile confirmation could outlive a changed MO2 root. Run
  preparation now rediscovers the exact bounded profile set and rejects a
  confirmed identity that is no longer present; setup readback also stops
  calling a missing candidate confirmed.
- Must fix: the first prepared-run table made a content-derived effective
  configuration identity unique per preparation. Removed that false
  uniqueness so multiple snapshots may reuse the same immutable effective
  configuration while retaining distinct preparation identities.
- Must fix: create/clone commands could update an existing configuration when
  supplied a current revision. They now require the absent revision `r0`,
  while update requires an active existing configuration.
- Follow-up: native credential entry and exact-target secure-store interaction
  remain unavailable and are not implied by the non-secret intent receipt.
- Non-blocking: LOOT presence can be recorded, but invocation remains
  `not-yet-validated` and unavailable under current authority.
- Owner/authority decision: none.
- Safety/isolation breach: none. No secret, archive/private fixture, provider,
  network, billable, generic path, SQL, command, or URL authority was used.

## Superseded WP4 receipt — Prepared manual run, lifecycle, live state, and reconnect

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4`

Candidate: same mutable Phase B candidate based on accepted Phase A commit
`59b6de3d80443c0150c15c8d83b5b29d1b3536ef`; package binding is deferred to
the single Checkpoint B commit.

Delivered:

- typed `PrepareManualRun` and `SubmitPreparedRun` RPCs that do not accept raw
  analysis JSON, generic provider requests, or generic filesystem authority;
- immutable binding to confirmed profile revision, saved-configuration
  revision, installation snapshot, semantic context, effective configuration,
  resolved input manifest, preparation, and one-shot initiation gesture;
- atomic durable start receipt and `prepared_run_submissions` binding so an
  idempotency key cannot be rebound to a different preparation or gesture;
- coordinator-owned mapping into existing offline run scheduling, progress,
  bounded event, pause/resume/cancel, snapshot/resync, and durable-command
  readback infrastructure; and
- generated C# native diagnostic execution proving setup, replay/conflict,
  honest estimate, offline completion, reconnect, coordinator restart, and
  durable readback without UI-owned state.

Focused verification:

- `TypedSetupAndPreparedManualRunSurviveReconnectAndRestartOffline`: 1 passed,
  0 failed, 0 skipped after the Release launch artifacts were rebuilt;
- the same run exercised typed malformed/missing tool locations, exact version
  classification, saved-selection/confirmation separation, configuration
  replay/conflict, non-secret enrollment status, preparation, idempotent start,
  completion, reconnect, restart, and gesture/binding readback; and
- every focused run reported zero repository-owned `dotnet` or `testhost`
  survivors after exact-root cleanup.

Review result: `ACCEPT` after correction and focused re-review.

- Must fix: the first prepared start durably stored its four-part run binding
  but not the initiation gesture or preparation identity. Added an append-only
  atomic submission binding and replay comparison for both identities.
- Must fix: the first integration run used stale Debug/Release launch
  artifacts and timed out before coordinator startup. Rebuilt the exact Release
  solution artifacts and reran the same generated-client path successfully.
- Must fix: explicit local tool validation initially admitted fully qualified
  UNC roots and did not distinguish inaccessible roots. UNC/device-style roots
  now fail closed before filesystem access; explicit local validation performs
  one bounded accessibility probe and rejects reparse roots.
- Follow-up: Phase C result/review projections and Phase D renderer/desktop
  consumers remain unimplemented.
- Non-blocking: offline Phase B execution uses the existing substrate worker;
  it does not claim broader analyzer orchestration or provider execution.
- Owner/authority decision: none.
- Safety/isolation breach: none. No live/provider/billable operation, private
  evaluator material, archive, UI durable state, or renderer gRPC authority was
  introduced.

## Superseded Checkpoint B receipt — Phase B candidate

Candidate: the single review-ready Phase B candidate based directly on accepted
Phase A commit `59b6de3d80443c0150c15c8d83b5b29d1b3536ef`. The exact immutable
Checkpoint B commit is reported by the orchestrator after commit creation.

Disposition at the time: `ACCEPT` for WP3 and WP4. This disposition is now
superseded by the Phase B correction. Checkpoint B is suspended, Phase C is
blocked and was not started, and M2 remains inactive.

Complete accepted verification floor on the final reviewed bytes:

- locked restore passed, and Release build passed with 0 warnings and 0 errors;
- Unit: 249 passed, 0 failed, 1 expected platform skip;
- Contract: 188 passed, 0 failed, 0 skipped, including JSON Schema, protobuf,
  generated-contract, inventory, and exact fingerprint drift checks;
- Integration: 121 passed, 0 failed, 0 skipped;
- Evaluation: 90 passed, 0 failed, 9 expected private/environment skips;
- Security: 154 passed, 0 failed, 3 expected private/platform skips;
- Fault: 117 passed, 0 failed, 3 expected environment skips;
- complete unfiltered solution: 713 passed, 0 failed, 10 expected skips;
- formatting, dependency-manifest, documentation, functional-naming, and
  `git diff --check` gates passed; documentation validation covered 150
  metadata files, 152 Markdown link sources, and 19 JSON files; functional
  naming covered 177 exact reviewed exceptions with zero unexplained findings;
  and
- exact-root cleanup after every test invocation and every dotnet gate reported
  0 repository-owned `dotnet` or `testhost` survivors.

Final consolidated correction/re-review result:

- Must fix: schema `12` exposed stale schema-version assertions and downgrade
  fixtures in four unit and three integration cases. Updated only their current
  schema expectations, exact schema-12 objects, and migration-history teardown;
  focused rechecks then passed 4/4 and 3/3 respectively.
- Must fix: the formatting gate found one indentation-only collection-expression
  issue in the new coordinator workflow. Applied the repository formatter,
  reran all non-test gates, then reran the complete accepted floor on the
  corrected final bytes.
- Contract maturity: the WP3 setup/configuration and WP4 prepared-run-to-live-
  lifecycle paths are producer-consumer-validated through generated protobuf,
  coordinator validation, durable storage, generated C# native diagnostics,
  reconnect, restart, conflict, invalid-state, and readback evidence. Native
  secret enrollment remains explicitly unavailable, and Phase C/D result,
  review, renderer, and desktop consumers remain proposed/unimplemented.
- Owner/authority decision: none.
- Safety/isolation breach: none. Verification used only offline developer
  fixtures/fakes; there was no private/archive access or network, provider,
  credential, live, or billable operation.

## Corrected WP3 receipt — Typed setup and authoritative preparation

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3`

Candidate: one mutable correction candidate beginning at Phase B commit
`f7b39021097a6954c4d9d1d83ff05a10885c4072`. The exact immutable correction
commit is reported by the implementation orchestrator after commit creation.

Delivered:

- application contract `1.6.0`, storage contract `1.12.0`, and schema `13`,
  with full protobuf fingerprint
  `a95cf80fa175461c93aa0ad36c8d7dc02bd8dee55b97fd142c4d0d85ddd86353`
  and storage fingerprint
  `caf9ea9d8b0064c7be838b8dcb396d83c00a7286ae2be18c44dffff334a4dce3`;
- a closed `AnalysisCapabilityKind` contract. The only accepted Phase B
  mapping is `DeliveredIndexLocal` to analyzer
  `candidate-source-delivered-indexes-v1`; unknown, unsupported, legacy
  arbitrary, non-local, provider-authorizing, or parallel configurations are
  rejected rather than mapped to invented behavior;
- exact saved-configuration detail plus bounded list, create, clone, update,
  delete, optimistic conflict, replay, restart, and schema-12 migration
  behavior. Legacy arbitrary analyzer strings migrate to the explicit
  `Unsupported` gap state;
- profile candidates and explicit confirmation bound to the canonical MO2
  installation identity as well as profile name, so another installation root
  with the same displayed profile cannot reuse confirmation;
- preparation-time validation of the retained installation snapshot, semantic
  context, resolved input manifest, saved configuration revision, effective
  configuration, profile revision, and retained input-package fingerprint;
- local-only estimates with unavailable elapsed/coverage dimensions and zero
  provider/cost authority; the configured wall-time limit is carried into the
  resolved durable analysis request; and
- provider enrollment intent that remains pending but reports
  `configured=false` and `verified=false` when no credential exists.

Focused evidence:

- real generated-service setup and prepared analysis scenario: 1 passed, 0
  failed, 0 skipped;
- setup/schema persistence suites: 7 passed, 0 failed, 0 skipped;
- application inventory, capability matrix, and exact protobuf fingerprint:
  3 passed, 0 failed, 0 skipped; and
- credential canary remained absent from response text, durable database,
  setup receipts, backup manifest/database, repository-owned log/output files,
  and restart readback.

Review result: `ACCEPT` after correction and focused re-review.

- Must fix: application `1.6.0` initially advertised negotiated minor `5`;
  corrected it to minor `6` across runtime and inventory.
- Must fix: the typed capability field initially reused the retired analyzer-
  string protobuf tag; reserved the old tag/name and moved the enum to a new
  field number so legacy bytes fail closed.
- Must fix: non-local and multi-lane values could imply execution authority the
  supported operation did not have; restricted Phase B to one local lane and
  zero provider authority, and mapped the saved wall-time limit into execution.
- Must fix: historical schema fixtures did not remove the later schema-13
  migration receipt before replaying from schema 8; corrected the fixtures and
  passed the two affected integration cases plus the 7-test migration suite.
- Owner/authority decision: none; the accepted existing analyzer declaration
  supplied the exact closed mapping, so no product default was invented.
- Safety/isolation breach: none.

## Corrected WP4 receipt — Durable prepared analysis and lifecycle

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4`

Candidate: the same mutable correction candidate beginning at
`f7b39021097a6954c4d9d1d83ff05a10885c4072`; binding occurs once in the final
correction commit reported by the orchestrator.

Delivered:

- `SubmitPreparedRun` resolves the saved typed configuration to the existing
  `managed-analysis-v1` path and atomically creates the durable run, exact
  operation request/hash, command receipt, and prepared-submission authority
  before scheduling;
- the retained-input resolver revalidates completed source run/operation,
  append-only bindings, operation hash, snapshot payload/hash/completion,
  semantic payload/hash/context fingerprint, input-manifest reference and
  fingerprint, analyzer declaration, and prior output before admission;
- a canonical SHA-256 submission fingerprint covers command/requested-run
  identity, preparation and revision, initiation kind, gesture, deadline, all
  immutable bindings, and resolved operation kind/hash. Only the original
  meaning-equivalent request returns `AlreadyAccepted`;
- a durable global unique gesture constraint prevents reuse by another command,
  while exact retry of the original command remains valid;
- missing or substituted snapshot, context, manifest, package, configuration,
  profile, operation, or run identities fail before a new run is created; and
- existing authoritative progress, bounded events, cancel/reconnect,
  snapshot/resync, restart, terminal output, analyzer coverage, and gap
  readback remain coordinator-owned and expose no raw orchestration JSON.

Real operation-path evidence:

- the offline fixture publishes a real retained MO2 snapshot and semantic
  payload, executes a completed `managed-analysis-v1` source through a local
  named-pipe worker, retains that exact input package, and starts the prepared
  run through the generated application client;
- readback proves operation kind `managed-analysis-v1`, exact request SHA-256,
  nonzero 64-character submission fingerprint, the bound effective
  configuration, configured 60,000 ms wall-time, analyzer
  `candidate-source-delivered-indexes-v1`, retained snapshot identity,
  completed analyzer coverage, and zero authoritative coverage gaps; and
- the same scenario proves exact retry, changed-run/deadline rejection, stale
  preparation rejection, global gesture-reuse rejection, authoritative
  progress, reconnect, coordinator restart, output readback, and rejection of
  nonexistent/substituted inputs.

Review result: `ACCEPT` after correction and focused re-review.

- Must fix: the Phase B candidate scheduled prepared commands without a real
  analysis operation, allowing generic substrate completion. Replaced that
  proof with the retained-input managed-analysis path above.
- Must fix: lower-level generic durable-command replay was temporarily made as
  strict as a prepared submission. Restored generic command semantics while
  preserving exact prepared-run ID/deadline/operation/fingerprint checks;
  focused lifecycle tests passed 2/2 and the real service scenario passed 1/1.
- Must fix: process cleanup originally matched only absolute coordinator paths;
  expanded it to the exact repository root plus Debug/Release coordinator
  tokens so relative launch command lines are also repository-owned.
- Owner/authority decision: none.
- Safety/isolation breach: none. No generic path, implicit scan, network,
  provider request, credential, live, or billable authority was added or used.

## Corrected Checkpoint B receipt — Phase B accepted

Disposition: `ACCEPT` for corrected WP3 and WP4. Checkpoint B is reinstated.
Phase C is automatically unblocked by the accepted plan but was not started;
M2 remains inactive.

Complete accepted verification floor on the exact final candidate:

- locked restore passed; Release build passed with 0 warnings and 0 errors;
- Unit: 249 passed, 0 failed, 1 expected platform skip;
- Contract: 188 passed, 0 failed, 0 skipped, including schema/protobuf,
  generated-contract, inventory, capability-matrix, and fingerprint drift;
- Integration: 119 passed, 0 failed, 0 skipped;
- Evaluation: 90 passed, 0 failed, 9 expected private/environment skips;
- Security: 154 passed, 0 failed, 3 expected private/platform skips;
- Fault: 117 passed, 0 failed, 3 expected environment skips; and
- complete unfiltered solution: 713 passed, 0 failed, 10 expected skips.

The first diagnostic floor attempt found one generic durable-command replay
regression; the next integration attempt found two stale historical-schema
fixtures. Both were corrected on the same mutable candidate, focused checks
and affected-surface review passed, and the complete accepted floor above was
then run from restore/build on the exact final bytes.

Documentation, JSON Schema, protobuf/generated drift, dependency manifest,
formatting, functional naming, and `git diff --check` passed. Documentation
validation covered 150 metadata files, 152 Markdown link sources, and 19 JSON
files. Functional naming covered 177 exact reviewed exceptions with zero
unexplained findings. After every test invocation, exact repository-owned
cleanup reported 0 surviving `dotnet` or `testhost` processes.

Contract maturity: WP3 setup/configuration and WP4 prepared-run-to-live-run are
`Producer-consumer-validated` through generated protobuf, service validation,
durable storage/migration, real retained-input offline execution, conflict and
invalid-state cases, reconnect, restart, and readback.

Remaining limitations: native credential entry and secure-store choreography,
LOOT invocation, provider execution, Phase C result/review projections, Phase D
renderer/desktop consumers, independent semantic-oracle evaluation, and M2
activation remain unavailable or out of scope. The retained input manifest is
authoritative as an immutable reference/fingerprint in the durable source
operation and retained package; Phase B adds no generic manifest-content API.

Security/provenance: all execution evidence used developer-owned offline
fixtures/fakes and repository state. No private evaluator material, archive,
external network, provider, credential, live, or billable operation was
accessed, and no product output authored expected truth.

## Suspended WP5 receipt — Bounded result exploration candidate

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5`

Candidate: one mutable Phase C candidate beginning at accepted corrected
Checkpoint B commit `8bc653f460cbeab1bda03dcd8a6cdb40e769ad08`. The exact
Checkpoint C commit is reported by the implementation orchestrator after the
single Phase C commit is created.

Delivered:

- application protocol `1.7.0` exposes bounded overview/readiness, separate
  supported-case and lead-only queues, exact finding/case/abstention/failure/
  gap detail, evidence expansion, and focused-mod views;
- result projections are run-bound indexes over the exact immutable retained
  finding/case payload and carry its payload identity and SHA-256. Detail
  readback decodes that canonical payload and does not author new evidence;
- queries accept only six item kinds, two sorts, a 160-character inert search,
  at most 100 items, and authenticated five-minute cursors bound to run,
  projection identity, filter/search, sort, page size, and the last key;
- exact subject matching uses the retained subject array, so substring and
  lookalike identities do not enter a focused view; and
- no Phase C result RPC contains a payload path, SQL, URL, active markup,
  arbitrary object lookup, generic query, whole-run download, or
  full-population export primitive.

Focused evidence and measurements:

- `ResultReviewWorkflowIntegrationTests`: 4 passed, 0 failed, 0 skipped after
  correction and re-review;
- real named-pipe generated C# consumer through the managed-analysis corpus:
  1 passed, 0 failed, 0 skipped;
- 100,000 retained summaries, 100-item severity/search page: 84 ms measured
  local query latency and 15,679-byte protobuf message, below the 1,048,576-
  byte application bound; and
- deterministic repeat, hostile `<script>` text remaining inert, canonical
  payload round-trip, supported/lead separation, exact focus isolation, cursor
  expiry/query/sort/scope/projection invalidation, coverage/failure/gap state,
  and the no-safety-guarantee claim all passed.

Review result at the time: `ACCEPT` after focused correction and re-review;
superseded by the current correction review because the FindingReport and
application-service contract evidence was incomplete.

- Must fix: schema-14 objects were initially absent from the exact database
  object allowlist; added the seven tables, three indexes, and eight
  append-only triggers to the supported schema identity.
- Must fix: the first severity cursor used only the item ID and could skip or
  repeat across severity groups; bound the cursor to both severity rank and
  item ID.
- Must fix: case proof evidence was initially mislabeled as contradicting
  evidence and LLM involvement defaulted to false; removed the invented
  contradiction and changed LLM involvement to explicit
  `unknown-not-inferred` state.
- Must fix: completed-with-gaps runs were initially presented as provisional;
  corrected the readiness mapping to scope-limited while preserving all gaps.
- Must fix: the first focused view returned detailed run-wide coverage/gap
  text alongside exact-subject items. It now returns only exact-subject
  coverage and focused gap/failure/abstention text, plus a generic notice when
  other run-level gaps exist, so unrelated details are neither merged nor
  silently hidden.
- Owner/authority decision: none.
- Safety/isolation breach: none.

## Suspended WP6 receipt — Durable user review candidate

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6`

Candidate: the same mutable Phase C candidate beginning at
`8bc653f460cbeab1bda03dcd8a6cdb40e769ad08`.

Delivered:

- append-only review events and assumption events with revision-bound current
  projections, typed stale-revision conflicts that return current safe state,
  idempotency rebinding rejection, successor edit/removal state, and replay
  from history without rewriting source findings, cases, contexts, or runs;
- review carryover only when the retained reconciliation assessment proves
  exact continuation and causal, applicability, dependency, and producer
  equivalence. Names, prose, visual similarity, or a fabricated assessment ID
  cannot authorize carryover;
- the superseded targeted-verification implementation attempted to create a
  new manually initiated durable run and bind the exact source finding/case and
  only scopes retained by that source, and
  records scope-limited readiness without reopening a terminal source run or
  borrowing unrelated coverage;
- local-private structured JSON export contains exact selected result, review,
  and assumption records and identities; exact review/assumption revisions;
  the immutable run binding and lifecycle generation; filters; context,
  schema, and generator identities; sharing class; omissions;
  privacy/source-policy decisions; and retained provenance. Selection is
  limited to 100 identities and artifact size to 1 MiB; and
- structured export files use a dedicated typed write class, validate their
  length/SHA on read, participate in exact backup manifests and restore, and
  are discoverable through deletion preview without exposing their path over
  the application contract.

Focused evidence:

- append-only trigger rejection, optimistic concurrency, typed conflict,
  annotation successor/removal history, inferred-assumption origin,
  successor context identity, effective removal, rebuild/replay, restart,
  backup/restore, export integrity, deletion preview, and canonical source
  immutability passed in the 4-test focused Phase C suite;
- at that superseded point, generated-client calls proved review success and
  stale conflict, assumption creation, structured export, and a non-executable
  targeted-verification record on the real named-pipe coordinator path; and
- source terminal state stayed `CompletedWithGaps`, the targeted successor was
  a distinct queued run with `scope-limited` readiness, and no unrelated scope
  identity was accepted.

Review result at the time: `ACCEPT` after focused correction and re-review;
superseded by the current correction review because targeted verification was
not executable and export deletion was absent.

- Must fix: targeted verification initially passed a gesture through the
  prepared-run foreign-key path without a preparation. It now uses the typed
  durable run operation plus its own append-only manual gesture/source/scope
  record, with a future deadline and a distinct successor run.
- Must fix: local-private export metadata survived backup while the export file
  did not. Export artifacts are now exact length/SHA-bound backup-manifest
  members and restore atomically with database and payload artifacts.
- Must fix: deletion preview initially used substring search over JSON and
  could match a lookalike identity. It now uses exact `json_each` selection
  equality.
- Must fix: export provenance initially included only result payloads. It now
  includes selected review event identities and assumption successor context
  identities, the four run-binding identities, and selected review events must
  belong to the exact export run.
- Must fix: a continuity assessment originally authorized a caller to supply
  substituted disposition/suppression/annotation values. Carryover now
  requires and reproduces the exact retained source review state as well as
  the four exact-continuation gates.
- Must fix: targeted verification originally allowed an active source and did
  not check that a jointly selected finding actually belonged to the selected
  case. It now requires a distinct successor from an immutable terminal source
  and validates the exact finding/case relationship before creating work.
- Must fix: the first export artifact was only an exact identity manifest. It
  now contains the selected inert result records, review events with revisions,
  current assumption successors with revisions/context identities, and exact
  source-run binding/readiness metadata; readback validates both manifest and
  artifact fingerprints.
- Owner/authority decision: none.
- Safety/isolation breach: none.

## Suspended Checkpoint C receipt — Phase C candidate

Disposition: superseded by correction review. The candidate did not establish
the complete FindingReport query surface, recursive service-contract defenses,
an accepted executable targeted-verification mapping, or structured-export
deletion. Checkpoint C is suspended; Phase D is blocked and M2 remains inactive.

### Correction architecture stop — executable targeted verification

Classification: owner/authority decision for the targeted-verification path.
Independent Phase C corrections may continue, but Checkpoint C cannot be
restored until this decision is accepted and implemented.

The accepted product requirement says that, after an external setup change, a
user can manually rerun affected checks. ADR-0037 requires a new manually
initiated run linked to exact prior subjects and declared scope. The current
executable catalog, however, supports only `managed-analysis-v1`, whose request
is bound to a retained delivered-input package, installation snapshot, semantic
context, effective configuration, and resolved input manifest. No accepted
product or architecture contract defines how an exact source finding/case scope
and the changed external setup produce the successor snapshot/input package or
how that scope restricts the delivered analyzer without omitting dependencies.

The earlier candidate instead registered operation kind `targeted-verification`,
which `ManagedRunExecutor` cannot execute, created its successor run before the
targeted-verification row, and did not schedule authoritative work. Reusing the
source run's retained input would not verify the external change; filtering its
facts ad hoc would invent a product-reachable scope-expansion rule. Both are
rejected. The smallest required authority is a research/ADR/plan correction
that defines the typed targeted-scope expansion input, changed-snapshot binding,
dependency closure, mapping into `managed-analysis-v1`, and exact coverage/gap
semantics. No new operation kind or generic-substrate fallback is authorized.

### Superseded limited independent Phase C correction receipt

Historical authority: owner-authorized correction from clean commit
`ef4cead98b48b0a97982867a1baa9b588cd7e70b`. This receipt covers only the
independent WP5/WP6 defects. It does not research, design, implement, execute,
or claim completion of targeted verification. WP5/WP6 remain under correction,
Checkpoint C remains suspended, and Phase D remains blocked.

Corrected WP5 surface:

- `FindingCaseAnalysisPhase` now projects and publishes schema-validated
  FindingReport payloads in the same transaction as the canonical retained
  finding/case output. `finding_report_publications` is an append-only,
  run-bound queue index over those payloads and their exact canonical source;
- application `1.8.0` adds bounded `ListFindingReports` and
  `GetFindingReport` operations. The generated named-pipe C# consumer proves
  report publication/readback for supported findings, resolved negatives,
  limited/lead-only results, abstentions, failures, and coverage gaps. Detail
  preserves affected subjects, taxonomy, analyzer-local assessment boundaries,
  action or explicit non-applicability, uncertainty, reversibility, risks,
  validation, evidence identities, coverage, failures, exclusions, gaps, and
  exact report/source provenance without inventing evidence; and
- all result/report lists now require an existing exact run. Result and report
  cursors bind run, authoritative projection identity, exact allowlisted query,
  sort, page size, last key, and expiry. Evidence metadata now labels exact
  provenance and artifact schema identity/version rather than mislabeling them
  as producer identity/version.

Corrected independent WP6 surface:

- every Phase C request passes the recursive protobuf validator before service
  execution. Unknown fields at any nesting level, unknown/unsupported enum
  numerics, invalid closed tokens/defaults, malformed identities, oversized
  text/collections/cursors, and missing required values fail as typed invalid
  arguments. This validates only the targeted-verification transport shape and
  grants no executable authority for that blocked path;
- assumption cursors now bind an authoritative profile projection fingerprint,
  which changes after a relevant successor event or explicit projection rebuild,
  produces projection invalidation rather than cross-revision replay, and remains
  stable across an ordinary restart and backup/restore; and
- structured exports have typed preview/delete operations, append-only
  `created`, `deletion-requested`, and `deleted` events, a typed active/pending/
  deleted projection, exact idempotent retry, guarded local-private file
  removal, and tombstoned readback. Startup completes pending deletions and
  removes undeclared product-owned crash orphans. Active artifact tampering is
  reported as integrity failure; deletion safely removes missing or tampered
  artifacts without exposing filesystem authority. Backup includes only active
  artifact files while retaining every database event/tombstone, and restore
  completes pending deletion honestly.

Focused verification and measurements:

- `ResultsReviewApplicationContractTests`: 3 passed, including all 16 Phase C
  request types with recursive unknown-field rejection plus invalid-enum and
  oversized-text cases;
- focused result/report plus durable review/export lifecycle integration: 4
  passed. The lifecycle covers create/get/preview/delete/exact retry, missing
  artifact, tampering, injected pending-deletion interruption, restart,
  backup/restore, append-only history, assumption projection mutation, and
  byte-for-byte source immutability;
- real managed-analysis named-pipe generated consumer: 1 passed, proving
  FindingReport queue/detail, complete retained recommendation fields,
  provenance, structured-export preview/delete, unknown-field, invalid-enum,
  oversized, and wrong-run service rejection without targeted execution; and
- 100,000 retained summaries with a 100-item severity/search page: 77 ms local
  query latency and 15,679-byte protobuf message, below the 1,048,576-byte
  application limit.

Classified review result for this limited surface: `ACCEPT` after correction
and affected-surface re-review, without restoring Phase C acceptance.

- Must fix: FindingReport existed only as a detached projection. Added the
  atomic producer, retained payload/index, generated-service queue/detail
  consumer, canonical-source enrichment, and hostile-text/round-trip evidence.
- Must fix: Phase C services performed inconsistent shallow validation. Added
  one recursive closed validator at every Phase C RPC entry and real service
  rejection evidence.
- Must fix: export creation wrote a sensitive local artifact without a deletion
  lifecycle. Added append-only intent/completion, guarded deletion, typed
  tombstone/readback, restart orphan/pending recovery, and backup/restore
  semantics.
- Must fix: evidence fields claimed producer identity/version when the retained
  values were artifact provenance and schema metadata; renamed the contract
  fields. Report summaries separately label the retained source payload record
  while report provenance retains the canonical report-contract payload ID.
  A re-review also found general reports using finding/gap/taxonomy identities
  as `source_assignment_id`; publication now binds the real managed-analysis
  assignment identity, with the canonical finding/case input identity used only
  by direct phase-level execution where no managed assignment exists.
- Must fix: nonexistent result runs could look like an empty population and
  assumption cursors survived relevant mutation. Added exact run ownership and
  authoritative projection binding/invalidation.
- Owner/authority decision: targeted verification remains the previously
  recorded architecture blocker; no new independent correction blocker was
  found.
- Safety/isolation breach: none.

Contract state after this limited correction: application `1.8.0`, domain
`1.3.0`, storage `1.14.0`, renderer `1.1.0`, storage schema `15`; protobuf
fingerprint
`093158cf0212c899cc192df3bc9f2a2436e0191e3e8c6a9b5acc3142bcab71e9`;
storage fingerprint
`a64750491c8cd7e79d96e3190710b4b0c71c6377a83df2a5e25df0bc554f7b1f`.
The new operations and FindingReport contract remain implementation-active;
their focused producer/consumer and lifecycle seams are validated but are not
frozen or Phase C accepted.

No complete verification floor was run because the owner explicitly retained
targeted verification as an unresolved Checkpoint C blocker. No private
evaluator material, archive, network, provider, credential, live, or billable
operation was accessed. Documentation validation passed 150 metadata files,
152 Markdown link sources, and 19 JSON files. Functional naming passed 184
exact reviewed exceptions with zero unexplained findings.

### Limited fail-closed and populated-migration correction receipt

Authority: owner-authorized correction from reviewed predecessor
`8ec26bc9926af3f99c2757249486f14bf14fd759`. This receipt covers only
fail-closed targeted-verification admission, runtime/documentation truth, and
populated report-publication migration behavior. It does not research, design,
implement, execute, or claim completion of targeted verification. WP5/WP6
remain under correction, Checkpoint C remains suspended, and Phase D remains
blocked.

Corrected behavior:

- `StartTargetedVerification` retains its future-facing typed RPC declaration,
  performs normal negotiation and recursive request validation, then returns
  typed `Unsupported`. The handler does not call persistence. A real generated
  C# client over the current-user named pipe proves a well-formed request leaves
  runs, operations, jobs, durable commands, payloads, lineage, audit,
  targeted-verification rows, lifecycle events, and prepared gesture receipts
  unchanged, and the requested run remains absent;
- application `1.9.0` adds `FindingReportAvailability` to report-list readback.
  A run with retained result projection rows but no report publication rows now
  returns explicit `Unavailable` with `retained_results_present=true`, rather
  than an empty page that could mean no reportable results; and
- populated schema-14-to-15 migration evidence starts with retained result and
  finding/case payload bytes. Migration preserves those exact canonical bytes,
  creates no inferred report, and exposes the explicit unavailable projection
  gap. Current schema-15 runs with published reports retain the ordinary bounded
  page/detail path.

Runtime and documentation truth:

- bootstrap keeps Result Exploration and Durable User Review `Partial`.
  FindingReport/readback and export deletion/recovery are described as focused
  evidence; Checkpoint C, desktop consumption, and M2 remain unaccepted;
- the contract inventory binds reviewed predecessor
  `8ec26bc9926af3f99c2757249486f14bf14fd759`, records 37 implemented RPCs, and
  classifies `StartTargetedVerification` as `declared-unimplemented` and
  native-only until an accepted implementation exists; and
- current-state, the foundation README, capability matrix, evaluation cases,
  verification profile, protobuf README, version axes, and fingerprint now
  state the same fail-closed and migration-gap boundary.

Contract state after this correction: application `1.9.0`, domain `1.3.0`,
storage `1.14.0`, renderer `1.1.0`, storage schema `15`; protobuf fingerprint
`d4db44c3c64f4c661162c938696c8d9ffc3d258f81eac18e9a6479d09c3491f9`;
storage fingerprint
`a64750491c8cd7e79d96e3190710b4b0c71c6377a83df2a5e25df0bc554f7b1f`.
The new report-availability response remains implementation-active, with a
generated named-pipe consumer and populated migration evidence. No complete
verification floor was run because executable targeted verification remains an
unresolved Checkpoint C blocker.

Focused verification:

- `dotnet build Infinium.sln -c Release --no-restore`: passed with zero warnings
  and zero errors;
- ContractTests `TestCategory=Contract`: 179 passed, zero failed or skipped;
- UnitTests `TestCategory=Unit`: 249 passed, one environment-dependent symbolic-
  link test skipped, zero failed;
- `ResultReviewWorkflowIntegrationTests`: 5 passed, including deterministic
  result/report exploration, 100,000-summary bounds, populated migration,
  export deletion/recovery, and exact continuity carryover;
- real generated-client managed-analysis named-pipe test: 1 passed, including
  truthful Partial bootstrap claims, targeted `Unsupported` with ten durable
  population counts unchanged, explicit report-projection unavailability, and
  ordinary FindingReport queue/detail readback; and
- documentation validation passed 150 metadata files, 152 Markdown link
  sources, and 19 JSON files. Functional naming passed 184 exact reviewed
  exceptions with zero unexplained findings; format verification and
  `git diff --check` passed.

Classified review result for this limited surface: `ACCEPT` after correction
and affected-surface re-review, without accepting WP5/WP6 or Checkpoint C.

- Must fix: the first migration guard was mistakenly placed on ordinary result
  listing. The focused populated-migration test caught the error; the guard now
  exists only on report listing, and the five-test result/review rerun passes.
- Must fix: the inventory audit equated the existence of any service handler
  with executable implementation. It now separately audits implemented RPCs
  and the exact allowlisted fail-closed handler, so the inventory can truthfully
  mark `StartTargetedVerification` declared-unimplemented while retaining its
  typed `Unsupported` boundary.
- Must fix: re-review found the new availability message lacked the standard
  future/privileged reservations. It now reserves fields 90-99 and path, SQL,
  URL, query, payload-path, command, and download names; the final protobuf
  fingerprint binds those bytes.
- Follow-up: none for the independently authorized surface.
- Owner/authority decision: executable targeted verification still requires
  the accepted changed-snapshot and exact-scope mapping already recorded above.
- Safety/isolation breach: none.

Historical contract state before the first limited independent correction:

- application `1.7.0`, domain `1.3.0`, storage `1.13.0`, renderer `1.1.0`,
  storage schema `14`;
- protobuf contract-set fingerprint
  `8e6b8b3cdeeb634a744d57be49fcfb6b6d77d3fbbeb9afb020c9e17a6b9336bf`;
- storage schema fingerprint
  `ca3b9b41dde2ed93ea3f86cee9ece3bd4c28705e23da4a76794db6437d8968ba`;
- renderer registry fingerprint
  `b5631f491ff2b781dcefe6b36318fbd75831ad8d9f6cedb3b2cde946b7cecada`;
  and
- result/review operations are under correction and therefore
  `Implementation-active`. Earlier generated-protobuf, persistence, migration,
  conflict, named-pipe, restart, replay, and backup/restore evidence remains
  diagnostic only until the missing verticals and adversarial cases pass.

The complete-floor receipt below belongs to the superseded Phase C candidate.
It is retained as historical diagnostic evidence and does not restore
Checkpoint C for the current correction.

Remaining limitations: no desktop host or TypeScript consumer, no Phase D,
no native credential entry or provider execution, no LOOT invocation, no
whole-run/full-population export, no broader analyzer coverage, no independent
semantic-oracle qualification, and no M2 activation or product-readiness
claim.

Security/provenance: Phase C used only developer-owned offline repository
fixtures and local product roots. It did not access private evaluator material,
archives, network, providers, credentials, live systems, or billable effects,
and no expected truth was authored by product output.

### Superseded candidate verification receipt — Checkpoint C suspended

The first accumulated-floor attempts remained diagnostic rather than final:

- Unit exposed one stale exact audit assertion after the dedicated Export
  write class increased the protected write-class count from six to seven.
  The exact audit test was corrected and passed 1/1 focused.
- Integration exposed one shared schema-8 downgrade helper that did not remove
  schema-14 result/review objects before validating the historical schema.
  The helper was corrected once for both consumers and both focused migration
  tests passed 2/2.

Both were ordinary fixture drift. The same mutable candidate was re-reviewed,
`git diff --check` passed, and the complete final floor was restarted from
locked restore on the corrected review-ready candidate.

Prior candidate commands and diagnostic results (not current acceptance):

- `dotnet restore Infinium.sln --locked-mode --nologo`: passed; every project
  was already up to date and no package was downloaded.
- `dotnet build Infinium.sln -c Release --no-restore --nologo`: passed with 0
  warnings and 0 errors.
- `dotnet test Infinium.sln -c Release --no-build --nologo --filter
  "TestCategory=Unit"`: 249 passed, 0 failed, 1 skipped.
- `dotnet test Infinium.sln -c Release --no-build --nologo --filter
  "TestCategory=Contract"`: 190 passed, 0 failed, 0 skipped across the tagged
  Contract, Unit, and Evaluation assemblies.
- `dotnet test Infinium.sln -c Release --no-build --nologo --filter
  "TestCategory=Integration"`: 125 passed, 0 failed, 0 skipped across the
  tagged Integration, Contract, and Evaluation assemblies.
- `dotnet test Infinium.sln -c Release --no-build --nologo --filter
  "TestCategory=Evaluation"`: 91 passed, 0 failed, 9 skipped.
- `dotnet test Infinium.sln -c Release --no-build --nologo --filter
  "TestCategory=Security"`: 158 passed, 0 failed, 3 skipped.
- `dotnet test Infinium.sln -c Release --no-build --nologo --filter
  "TestCategory=Fault"`: 118 passed, 0 failed, 3 skipped.
- `dotnet test Infinium.sln -c Release --no-build --nologo`: 719 passed, 0
  failed, 10 skipped across all six test assemblies.
- `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity
  minimal`: passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  eng/validate-documentation.ps1`: passed for 150 metadata files, 152 Markdown
  link sources, and 19 JSON files.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  eng/verify-functional-naming.ps1`: passed with 184 exact reviewed exceptions
  and zero unexplained findings.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  eng/update-dependency-manifest.ps1 -Check`: passed with no drift.
- `git diff --check`: passed.

The Contract category includes the application-inventory and capability-matrix
JSON Schema validation, renderer envelope/registry schema and canonical-hash
checks, protobuf compilation/unknown-state checks, generated C# binding use,
and exact transitive protobuf fingerprint verification. Storage migration
tests bind schema `14` to fingerprint
`ca3b9b41dde2ed93ea3f86cee9ece3bd4c28705e23da4a76794db6437d8968ba`.

Every final category and unfiltered test invocation performed exact-root
repository-owned `dotnet`/`testhost` cleanup and reported 0 survivors. The
final handoff check also reported 0 survivors.

Consolidated review disposition: superseded by the correction request. The
targeted-verification operation still requires steward and owner disposition
of the documentation-only architecture proposal before implementation. Phase D
is blocked and no Phase D artifact is included in this candidate.

## Documentation-only targeted-verification correction proposal

Planning base: `7c0ceee255c8b9ef79f4116f848a0938376d6ac3`.

The correction investigation inspected the current snapshot capture,
retained-input/prepared-run admission, `bethesda-semantic-v1`,
`managed-analysis-v1`, candidate/dependency expansion, finding/case identity,
lineage, persistence/migration, and `StartTargetedVerification` seams. It found
that the existing executable operations are sufficient only when joined by a
new typed preparation and targeted delivered-input contract.

The proposal package is:

- [RESEARCH-0058](../../../../research/investigations/RESEARCH-0058-targeted-verification-executable-architecture.md);
- [Proposed ADR-0038](../../../../architecture/decisions/ADR-0038-targeted-verification-preparation-and-execution.md); and
- [Proposed WP6 addendum](wp6-targeted-verification-addendum.md).

Its recommendation is to capture a new installation snapshot, perform the
qualified semantic extraction, derive an inspectable dependency-closed scope,
and atomically start one ordinary `managed-analysis-v1` successor with exact
initiation and ADR-0022 analytical lineage. It rejects source-snapshot reuse,
ad-hoc fact filtering, automatic full-run fallback, and a new unexecutable
operation kind.

This entry is a proposal receipt, not an implementation or checkpoint receipt.
It does not change application/domain/storage/renderer versions, complete an
EVAL case, accept WP5/WP6 or Checkpoint C, unblock Phase D, or activate M2.

## Final closeout fields

WP9 will replace this placeholder with the accepted contract maturity,
diagnostic-consumer evidence, complete verification receipt, resource
measurements, remaining limitations, M2 planning inputs, and exact final
candidate.
