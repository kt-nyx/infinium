# M1-to-M2 Foundation — Frontend Application Foundation implementation record

Status: Accepted
Disposition: WP9 acceptance-evidence correction candidate; Checkpoint E pending; M2 inactive

Last reviewed: 2026-08-28
Owner: Project owner
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Planning base: `32dbb2c48754666336d2da571e554ad8897ed71c`

## Plain-language state

Phase A verified the application authority surface. The Phase B correction
binds typed prepared runs to the exact retained inputs and supported durable
analysis operation it claims to execute. WP3 and WP4 passed focused correction,
consolidated re-review, and the complete accepted verification floor. The first
Phase C candidate reached `c551c12e22522e7a2cef8c21a322aa76db8fc23e`; the
later corrected candidate at `2ec3be78da4d05d8c6ada68a3e18544a446f2f03` is
accepted at Checkpoint C. Independent corrections cover
report publication/readback, recursive request validation, export deletion,
provenance, paging, and populated migration gaps. Targeted verification now
has a corrected implementation candidate with fresh acquisition, closed scope
and correlation, atomic ordinary successor admission, and immutable lineage.
The project owner
accepted RESEARCH-0058, ADR-0038, and the WP6 addendum on 2026-08-26 from
architecture source `bd936a02562a8df1ddcb62f275cc45b6c225e594`. A fresh
corrected WP6 implementation is accepted at Checkpoint C. WP7 and WP8 each
completed focused implementation, review, correction, and re-review on the
same mutable Phase D candidate. The architecture steward accepted Checkpoint D
at `6b9b92a5f3dae0e90219f521919555956a8b5623`. The initial Phase E/WP9
candidate at `aadad64cc5e9e328474cfeb1a7130ea80fe5a254` is under a narrow
acceptance-evidence correction: its implementation diagnostics remain useful,
but its receipt is not valid Checkpoint E evidence. Checkpoint E remains pending
and M2 remains inactive.

The implementation has assigned every foundation capability to an exact owner,
and implemented the common and setup-to-live-run boundaries future frontend
work can build on. WP3 adds typed
tool/profile/configuration setup, honest estimates, and non-secret provider
status. WP4 adds prepared manual-run initiation with immutable durable bindings,
receipts, progress, reconnect, and restart behavior. Phase C is accepted. No
product UI, provider effect, or generic native authority was added by that
acceptance.

## Package status

| Phase | Work package | Status | Accepted candidate/evidence |
|---|---|---|---|
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1` | Accepted on phase candidate | Receipt below; final phase commit deferred until Checkpoint A |
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP2` | Accepted after correction | Corrected receipt and complete floor below |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3` | Accepted after correction | Earlier receipt retained as superseded evidence; corrected receipt below |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4` | Accepted after correction | Earlier receipt retained as superseded evidence; corrected receipt below |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5` | Accepted at Checkpoint C | Corrected producer/consumer evidence below; accepted at `2ec3be78da4d05d8c6ada68a3e18544a446f2f03` |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6` | Accepted at Checkpoint C | Corrected targeted-verification evidence below; accepted at `2ec3be78da4d05d8c6ada68a3e18544a446f2f03` |
| D | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP7` | Focused cycle passed | Generated consumer implementation and correction/re-review evidence below |
| D | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP8` | Accepted at Checkpoint D | Executable desktop-consumption proof and correction/re-review evidence below; accepted at `6b9b92a5f3dae0e90219f521919555956a8b5623` |
| E | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP9` | Acceptance-evidence correction active | Initial candidate `aadad64cc5e9e328474cfeb1a7130ea80fe5a254`; exact clean-commit proof rerun required before Checkpoint E review |

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

Consolidated review disposition: the architecture blocker is resolved by owner
acceptance, but the implementation blocker remains. The current RPC is still
declared-unimplemented, native-only-never-map, and typed `Unsupported`. Phase D
is blocked and no Phase D artifact is included in this candidate.

## Documentation-only targeted-verification architecture and adoption

Planning base: `7c0ceee255c8b9ef79f4116f848a0938376d6ac3`.

The correction investigation inspected the current snapshot capture,
retained-input/prepared-run admission, `bethesda-semantic-v1`,
`managed-analysis-v1`, candidate/dependency expansion, finding/case identity,
lineage, persistence/migration, and `StartTargetedVerification` seams. It found
that snapshot capture and `managed-analysis-v1` are reusable, but the current
run-bound Bethesda semantic endpoint cannot own required pre-start extraction.
The proposal therefore uses ADR-0016's evidence-acquisition run category and
requires a new directly initiated local acquisition route, typed preparation,
correlation/coverage, and targeted delivered-input contracts.

The architecture package is:

- [RESEARCH-0058](../../../../research/investigations/RESEARCH-0058-targeted-verification-executable-architecture.md);
- [ADR-0038](../../../../architecture/decisions/ADR-0038-targeted-verification-preparation-and-execution.md); and
- [Accepted WP6 addendum](wp6-targeted-verification-addendum.md).

Its recommendation is to capture a new installation snapshot, perform the
qualified semantic extraction under a separate evidence-acquisition owner,
correlate every required source member (including proven target absence),
derive an inspectable dependency-closed scope, and atomically start one
ordinary `managed-analysis-v1` successor with exact initiation and ADR-0022
analytical lineage. It rejects source-snapshot reuse, ad-hoc fact filtering,
automatic full-run fallback, and a new unexecutable analysis operation kind.

This entry is an architecture receipt, not an implementation or checkpoint receipt.
It does not change application/domain/storage/renderer versions, complete an
EVAL case, accept WP5/WP6 or Checkpoint C, unblock Phase D, or activate M2.

Follow-up review correction from proposal commit
`ca300c5626772bc4a5144ed45e3f7c13f5cc86a5` replaced the incomplete semantic-
extraction lifecycle claim with a directly initiated ADR-0016 evidence-
acquisition owner. It also added one typed source-to-target correlation/
coverage row per required member, positive proven-absence coverage, and the
zero-current-hypothesis route into guarded ADR-0022 reconciliation. The review
restored EVAL-0093's accepted target wording, restored the capability matrix's
accepted exit/traceability fields, removed proposal documents from current RPC
consumers and contract families, and restored the canonical WP6 work identity.

The correction re-review covered product meaning, lifecycle and ownership,
immutable results, snapshot/input provenance, dependency closure,
cross-snapshot identity/absence, coverage/readiness/reconciliation, renderer
security, matrix/evaluation traceability, functional naming, migration, and
plan/status consistency. Documentation validation, both planning JSON Schema
validations, functional-naming verification, `git diff --check`, and the
documentation-only path audit passed. No production build or test ran; the
final repository-owned `dotnet`/`testhost` survivor audit reported zero.

### Owner acceptance receipt

- Proposal commit: `ca300c5626772bc4a5144ed45e3f7c13f5cc86a5`.
- Corrected reviewed architecture commit:
  `bd936a02562a8df1ddcb62f275cc45b6c225e594`.
- Owner decision: accepted RESEARCH-0058's recommendation, ADR-0038, and the
  WP6 targeted-verification addendum on 2026-08-26.
- Pre-adoption clarification: identity/scope correlation that is unsupported,
  ambiguous, or incomplete is non-startable; fully correlated known-member
  analyzer/content unavailability, inaccessibility, or malformation may proceed
  only as an explicitly limited retained gap.
- Pre-adoption clarification: `ProvenAbsent` is a positive completed coverage
  observation, not proof of resolution, setup correctness, or game safety;
  ADR-0022 `NotObserved` remains guarded and scope-limited.
- Authority effect: a fresh corrected Phase C/WP6 implementation orchestrator
  is authorized to implement the accepted vertical.
- Traceability effect: the capability matrix and application-contract inventory
  now carry ADR-0038's expanded requirement/evaluation/security authority,
  accepted transition evidence, and five `Proposed` later contract families;
  EVAL-0093 retains its accepted target wording while linking the accepted
  detailed obligations.
- Non-effect: no RPC, contract byte, storage schema, renderer mapping, runtime
  behavior, WP5/WP6 or Checkpoint C receipt, Phase D gate, or M2 state changed.

Adoption review and verification: documentation validation passed 153 metadata
files, 155 Markdown link sources, and 19 JSON files. Both affected planning JSON
documents passed their exact JSON Schemas. Functional naming passed 184 exact
reviewed exceptions with zero unexplained findings. Status/authority assertions
proved accepted document metadata and source lineage, unchanged EVAL-0093 target
wording, five `Proposed` later contract families, the still-missing capability
vertical, and the still-declared-unimplemented/native-only RPC. `git diff
--check` and the documentation-only changed-path audit passed. No production
build or test ran; final repository-owned `dotnet`/`testhost` survivors were
zero.

## Corrected WP6 targeted-verification implementation candidate

Designation: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6`
targeted-verification implementation correction. Starting commit:
`fb7478cd6c824572b534935774ed01af8e6b4382`. The final immutable candidate
identity is the commit containing this record and is reported by the
implementation orchestrator after commit creation.

Plain-language result: the application can now prepare an inspectable recheck
after external setup changes, capture and analyze a fresh target snapshot,
retain exactly how every required source member correlates to that target, and
start one ordinary managed-analysis successor without changing the source
result. The implementation is a candidate for corrected Checkpoint C review;
it does not accept WP5, WP6, Checkpoint C, begin Phase D, or activate M2.

### Contract and execution vertical

- Application protobuf `1.10.0` adds clean-break begin, status, cancellation,
  start, and successor-readback RPCs plus bounded preparation, scope,
  correlation, evidence progress/checkpoint, gap, and lineage projections.
  Generated C# client/server bindings remain build-owned by `Grpc.Tools`.
- Domain contract `1.4.0` adds functional targeted source, scope dependency,
  correlation coverage, reuse proof, plan, acquisition, and initiation-lineage
  contracts with deterministic identity and bounded validation.
- A native diagnostic consumer exercises all five generated RPCs over the real
  named-pipe application service. `StartTargetedVerification` is deliberately
  `native-only-never-map`; renderer contract `1.1.0` adds no operation.
- Preparation captures a fresh saved-profile snapshot, directly initiates the
  ADR-0016 evidence-acquisition owner, hydrates one canonical retained finding
  or case from authoritative records, closes its required members/shared-cause
  dependencies, and assigns exactly one evidenced correlation status to every
  member. Identity/scope ambiguity, unsupported correlation, or missing proof
  is non-startable; correlated analyzer/content gaps may start only as an
  inspectable limited plan with the member retained in the denominator.
- Start resolves current delivered candidates plus the separate finding/case
  coverage denominator and atomically admits their three content-addressed
  input payloads, explicit reuse decisions, target snapshot, operation hash,
  durable command, successor run/job, admission, and initiation lineage. The
  only executable operation is `managed-analysis-v1`; source-snapshot proof,
  caller narrowing, generic fallback, fabricated absent candidates, and source
  result mutation are rejected.
- Successor completion retains exact/revision/related/ambiguous/distinct
  ADR-0022 reconciliation. `ProvenAbsent` remains a completed scoped coverage
  observation and never becomes a claim that the original issue is resolved or
  the installation is safe.

### Persistence, migration, and version identities

- Storage contract `1.15.0` and schema `16` add append-only preparation,
  snapshot/acquisition, attempt/fence/checkpoint/progress/publication, scope,
  correlation, reuse, content-addressed operation input, start admission,
  application link, initiation lineage, and result-link state with rebuildable
  current projections.
- Migration `targeted-verification-preparation-0016` accepts the exact schema-15
  source, handles zero population, rejects nonzero retired targeted-operation
  state, binds expected objects/triggers, and fails closed on interruption,
  drift, or tamper. Backup/restore, replay, recovery, projection rebuild,
  deletion-impact, and payload reconciliation include the new state.
- Protobuf contract-set fingerprint:
  `c51f6c400547b948fd7f350ef5ac72f29d6032b2671cfba957a7be71cfc44e74`.
  Schema-16 fingerprint:
  `727285fbdb9a4a91e850a6bfad3749262be75e6388eae14edf9954eed23d783c`.
  The application inventory records 48 declared and 42 implemented RPCs.

### Consolidated review and corrections

The consolidated product-meaning, invariant, security, provenance, lifecycle,
persistence, migration, generated-contract, evaluation, naming, and diff review
found and corrected the following must-fix defects on the same mutable
candidate:

- negative deterministic seeds could violate identifier bounds;
- a malformed queued snapshot request could repeat a recovery crash loop;
- targeted admission initially shared the wrong prepared-run foreign-key path
  and did not enforce gesture uniqueness across both admission families;
- empty coverage and duplicate typed references could misstate missing input;
- correlation initially required statuses only for mandatory members, admitted
  invalid status/proof shapes, and allowed out-of-scope observations;
- changed stable identities were not included when rebuilding current delivered
  candidates; finding closure could duplicate its root and self-edge;
- cancellation did not atomically cancel and fence active evidence acquisition;
- event hashes did not bind their projection bytes, and read/rebuild did not
  reject projection/event tamper;
- evidence attempt, progress, checkpoint, structural comparison, and effective
  configuration were not all visible through native readback;
- reconciliation retry could duplicate immutable relationship rows;
- the three successor operation inputs were admitted before the atomic start
  transaction, allowing failed admission to leave payloads; they are now
  admitted, linked, counted, and exact-retry-validated inside that transaction.
- the first plan mislabeled the finding/case payload as reusable documentation
  while the successor retained different source inputs; source documentation,
  analyzer declarations, analysis context, and effective configuration now
  require explicit exact-fingerprint reuse proofs, the prior delivered input is
  explicitly recomputed, and unclassified source inputs fail closed.
- the first complete floor exposed four historical migration fixtures that
  downgraded schema metadata without removing schema-16 targeted objects, plus
  two assertions still fixed at schema 15; the fixtures now use one exact
  schema-16 removal manifest and assert the implementation-active schema.

No owner/authority decision was required. Review found no new analysis
operation, generic execution authority, renderer/backend proxy, path/command/
credential/provider authority, source-result mutation, private-fixture access,
or independent semantic-oracle claim.

### Verification receipt

Focused verification before the final floor:

- targeted planner, lifecycle, persistence, and migration selection: 10 passed;
- real named-pipe managed-analysis targeted successor integration: 1 passed;
- targeted application/authority/renderer contract selection: 34 passed;
- earlier full unit selection during development: 275 passed, 1 expected skip;
- earlier full contract selection during development: 216 passed.

Final complete verification floor on the corrected review-ready candidate:

- `dotnet restore Infinium.sln --locked-mode --nologo`: passed;
- `dotnet build Infinium.sln -c Release --no-restore --nologo`: passed with
  zero warnings and zero errors;
- `dotnet test Infinium.sln -c Release --no-build --nologo`: 731 passed,
  10 expected skips, zero failed (unit 277/1 skip, contract 216, integration
  138, security 22, fault 10, evaluation 68/9 skips);
- `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity
  minimal`: passed;
- documentation validation: passed 153 metadata files, 155 Markdown link
  sources, and 19 JSON files;
- functional naming: passed 190 exact reviewed exceptions with zero
  unexplained findings;
- dependency-manifest check and `git diff --check`: passed.

The first complete-floor attempt exposed stale downgrade fixtures and was
treated as diagnostic evidence; after the focused 5/5 migration/restart rerun,
the complete floor above passed. Repository-owned `dotnet`/`testhost` process
audits after every development batch and final floor reported exactly zero
survivors; the final audit matches both repository executable paths and
repository-bearing command lines so system-hosted repository processes are
included.

## Corrected Checkpoint C review-disposition candidate

Correction base: `4364b4a82a1d789b481cfa40ff9ef1d61d25972f`.
The correction commit is the commit containing this record and is reported by
the orchestrator after creation. This section records a correction candidate,
not acceptance of WP5, WP6, Checkpoint C, Phase D, or M2.

Plain-language result: the native application now truthfully reports that
targeted verification is implemented, can create limited plans from real
qualified processing evidence, rechecks every retained authority before a
start, and prevents one user gesture from authorizing different durable
actions. The primary native test now begins from the public RPC and reaches a
completed ordinary analysis plus lineage readback without manually inserting a
ready plan.

### Architecture-steward findings corrected

1. Bootstrap capability truth now distinguishes the implemented native
   targeted-verification workflow from pending corrected Checkpoint C review
   and desktop delivery. It no longer claims that executable architecture or
   capability is unavailable.
2. Production correlation maps exact, fully qualified known-member processing
   evidence to `Unsupported`, `Inaccessible`, or `Malformed`. Those members
   remain in the denominator and yield only an inspectable limited plan;
   ambiguous, unsupported, or incomplete identity/scope correlation remains
   non-startable. `ProvenAbsent` remains only an absence observation.
3. Start-time binding now revalidates the source terminal state and canonical
   occurrence/payload/signature, exact source operation and snapshot, distinct
   target capture/publication, preparation and canonical plan projections,
   scope/correlation denominator, acquisition attempt/publication/payload
   seals, reuse/configuration/context proofs, recomputed delivered and coverage
   inputs, resolved manifest, exact managed request, deadline, and coordinator
   fence before atomic persistence can create successor authority.
4. Preparation, cancellation, ordinary prepared-run start, and targeted start
   now share one globally one-shot gesture check. Immediate SQLite write
   transactions serialize independent connections; exact replay is evaluated
   first and remains idempotent. A two-connection preparation/cancellation race
   proves exactly one command wins.
5. The primary generated-C# named-pipe qualification invokes begin/status/start/
   readback, runs the actual MO2 snapshot capture and Bethesda semantic
   extractor through their durable owners, invokes the production planner,
   reaches `Ready` or `ReadyWithGaps`, completes exactly
   `managed-analysis-v1`, and reads initiation plus reconciliation lineage.
   It never calls `StoreTargetedPlan` or another persistence method to make
   that preparation ready.
6. Focused production-correlation tests cover unchanged identity, removed
   provider/mod, removed record/contribution, identity-change ambiguity, and
   production-reachable limited processing states. Existing planner,
   lifecycle, migration, backup/restore, tamper, hostile-input, renderer-denial,
   reconciliation, and evaluation tests remain part of the accepted floor.

The correction review also found and fixed three defects while rechecking the
same mutable candidate: canonical record/contribution/provider/asset matching
initially compared raw semantic identifiers rather than delivered stable IDs;
source delivered input was initially hydrated from a payload store that does
not own its embedded managed-operation bytes; and the first immediate-transaction
gesture hardening used transactionless helper reads. The final implementation
uses canonical delivered identities, verifies the embedded source delivered
input against its retained byte fingerprint, and attaches every new read to
the active transaction. A further re-review bound target links to the exact
capture operation/publication/payload and checked both source-root and target-
snapshot identities.

### Corrected evidence and unchanged identities

- Focused targeted unit/concurrency selection: 10 passed, 0 failed.
- Focused production/native integration selection: 4 passed, 0 failed.
- Broader targeted-verification plus finding/case selection before the final
  concurrency addition: 14 unit and 2 integration tests passed, 0 failed.
- Final complete floor: restore passed; Release build passed with zero warnings
  and zero errors; 734 tests passed with 10 expected skips and zero failures
  (unit 278/1 skip, contract 216, integration 140, security 22, fault 10,
  evaluation 68/9 skips); format, documentation validation, functional naming,
  dependency-manifest check, and `git diff --check` passed.
- Every .NET build/test batch and final handoff cleanup reported exactly zero
  repository-owned `dotnet`, `testhost`, or `vstest` survivors.
- No protobuf or generated-client byte changed. Application `1.10.0`, domain
  `1.4.0`, storage `1.15.0`, renderer `1.1.0`, schema `16`, 48 declared/42
  implemented RPCs, protobuf fingerprint
  `c51f6c400547b948fd7f350ef5ac72f29d6032b2671cfba957a7be71cfc44e74`,
  and schema fingerprint
  `727285fbdb9a4a91e850a6bfad3749262be75e6388eae14edf9954eed23d783c`
  remain unchanged. Generated C# output remains build-owned by `Grpc.Tools`.
- No database migration or expected-object shape changed; the correction
  strengthens schema-16 transactional use and read-time seal validation.

The consolidated semantic, security, provenance, lifecycle, persistence,
migration, evaluation, naming, and diff re-review found no remaining must-fix
defect, generic execution/renderer/local-machine authority, source-result
mutation, private-fixture access, independent semantic-oracle claim, product-
meaning change, or owner decision. Corrected Checkpoint C architecture-steward
review remains the next and only authorized gate.

The first complete-floor attempt correctly rejected one new capability-matrix
evidence sentence at 266 characters against its 256-character schema bound.
The sentence was shortened without changing meaning, the focused schema test
and changed-document review passed. The next floor passed all 734 tests but
the format gate caught one misplaced `Microsoft.Data.Sqlite` import; the import
was ordered, its focused format check passed, and the complete floor above was
rerun on the corrected candidate.

## Corrected Checkpoint C contract and identity correction candidate

Date: 2026-08-27
Starting commit: `08dffeb2723df2af2c1d478e1ceb991ec5e0183a`
Status: Implementation candidate awaiting corrected Checkpoint C architecture-steward review

Architecture-steward review found that the preparation status projection was
smaller than the accepted addendum, canonical source signatures were derived
from result-index metadata, and production correlation still allowed raw
semantic equality or slot position to imply identity. The same mutable
candidate corrected all three findings without changing ADR-0038 meaning.

The application `1.11.0` preparation readback now exposes, through generated
C# protobuf/gRPC clients, the exact canonical finding or case signature plus
analyzer family/version, semantic-contract version, and identity-contract
version. It independently pages at most 100 scope members, dependency edges,
target analyzers, lifecycle events, and artifact reuse or recomputation
decisions. It projects capture
time/profile revision, acquisition request/input
seals, attempts/fences, publication/provenance, terminal evidence, context and
configuration fingerprints, manifest identity/fingerprint, direct roots,
dependency/proof edges, one coverage row per visible member, correlation policy,
target-analyzer compatibility, recompute/reuse validity proofs, denominator,
expected work/limits, coverage classifications, gaps, and startability. Returned
data remains typed, inert, path-free, and free of payload bodies, commands,
SQL, URLs, credentials, generic execution, or renderer authority.

`TargetedVerificationSourceIdentity` now resolves exactly one retained finding
or case from the immutable canonical payload and treats its `IdentityEnvelope`
as authority. Begin, plan construction, readback, and start compare the result
index only as a bounded lookup and then bind/revalidate the canonical logical
identity, signature, analyzer family/version, semantic version, and identity
version. Start rejects signature or version substitution before any successor
run, command, operation input, admission, lineage, or scheduling mutation.
Supported and lead-only case projection kinds are normalized only for lookup;
their canonical case envelope remains authoritative.

Production correlation now compares raw Bethesda/MO2 semantic values only
after applying the exact `candidate-delivered-source` typed identity function.
Raw plugin names, form keys, participant/contribution strings, and normalized
paths cannot equal a source stable ID directly. A different contribution or
asset in the same record/field/component/ordinal or NPC/mesh/tint slot is
`Ambiguous` without retained typed continuity/equivalence/provider-lineage
proof and is non-startable. Independently removed contributions can still be
`ProvenAbsent` when complete qualified enumeration contains neither their
typed identity nor a conflicting same-slot mapping. Qualified processing
`Unsupported`, `Inaccessible`, and `Malformed` states remain reachable only
after exact identity correlation and retain limited-plan denominator gaps.

Focused evidence includes generated-message round trip of every new field;
unknown request-field and invalid-bound rejection; real named-pipe finding and
case identity-envelope readback; independent member, dependency, analyzer,
lifecycle, and artifact pagination;
raw-name/path/form-key adversarial equality; contribution and asset slot
changes without continuity proof; removed contribution with its record still
present; all-absent and mixed-absence denominator/no-fabrication planner cases;
qualified limited states; source signature/version tamper with zero partial
writes; and the unseeded begin through fresh capture, semantic acquisition,
production plan, exact `managed-analysis-v1`, completion, and lineage readback
path. Storage remains schema `16`, storage contract `1.15.0`, and fingerprint
`727285fbdb9a4a91e850a6bfad3749262be75e6388eae14edf9954eed23d783c`:
the append-only rows already retained the required evidence, so this correction
adds bounded read queries rather than a new database migration or expected-object
shape. Unaccepted schema-1.0 targeted plans are invalidated by the clean-break
schema-1.1 validator rather than migrated into successor authority.

Application contract `1.11.0`, domain contract `1.5.0`, and the persisted
`TargetedVerificationPlan` schema `1.1.0` are the advanced contract axes.
Renderer contract `1.1.0`, all renderer operation mappings, 48
declared/42 implemented RPCs, and generated-output ownership are unchanged.
The protobuf contract-set fingerprint is
`eaf72f2bd8c04ad16035ff7ae45ea4c08b514216a0b0f07ce50e7560c55342d8`;
C# generated output remains build-owned by `Grpc.Tools` under
`src/Infinium.Application/Infinium.Application.csproj`. Checkpoint C, WP5,
WP6, Phase D, and M2 remain unaccepted/inactive.

The consolidated correction review found and fixed four additional must-fix
defects on the same candidate: canonical bytes were still selected through the
result index instead of the retained finding/case checkpoint; dependency and
target-analyzer lists lacked independent cursors; an unsigned lifecycle cursor
could overflow the signed persistence boundary; and the changed persisted plan
bytes still declared schema 1.0.0. Re-review confirmed checkpoint-selected
canonical bytes, five independently bounded pages, typed overflow rejection,
schema 1.1.0 invalidation, and typed drift failures with no renderer or generic
execution authority.

Final focused verification passed 9 targeted planner/reconciliation unit tests,
4 preparation-contract tests, and 4 production/named-pipe integration tests.
The first complete-floor attempt passed all 738 tests with 10 expected skips but
found whitespace-only formatter drift in two expanded test files. The same
candidate was formatted, its 5 directly affected tests and focused format check
passed, and changed-surface re-review found no semantic change. The repeated
complete floor then passed locked restore, warning-free Release build, all 738
tests with the same 10 expected skips, full format verification, documentation
validation, functional naming, dependency-manifest check, and `git diff --check`.
Repository-owned `dotnet`/`testhost`/`vstest` survivor count was zero after every
.NET batch.

## Corrected Checkpoint C coherent readback and prepared-manifest candidate

Date: 2026-08-27
Starting commit: `1b6e7927d027333a9a53a4c8a9ccb80be7e2599a`
Status: Implementation candidate awaiting another corrected Checkpoint C
architecture-steward review

Plain-language result: one preparation status response can no longer combine
an older preparation revision with newer acquisition, plan, diagnostic, or
lifecycle state. The same read now verifies the retained evidence before it
describes that evidence. Preparation also owns the exact target resolved
manifest and analysis inputs before start; start consumes those exact bytes and
cannot silently replace their meaning by accepting another successor run ID.
This remains native implementation evidence, not acceptance of WP5, WP6,
Checkpoint C, Phase D, or M2.

### Coherent and evidence-bound readback

`AuthoritativeStore.GetTargetedPreparationReadbackSnapshot` holds the store's
read lock across preparation, canonical source occurrence/checkpoint/run/
operation/snapshot, plan, prepared-input, diagnostic, acquisition,
publication, and evidence hydration. It captures the preparation revision,
fingerprint, lifecycle state, plan binding, and acquisition identity, then
rechecks the exact preparation and acquisition generations before returning.
Begin, Get, Cancel, and exact replay responses all use this one coherent
operation. A deterministic lock-step integration test pauses readback while a
producer tries to advance state; the producer remains blocked, the first
response contains one complete old generation, and the next response contains
one complete new generation.

Before evidence is projected, readback validates every historical preparation
event hash and every acquisition event-to-projection hash binding. It also
validates the target snapshot link against the exact capture operation,
completed attempt and fences, publication, payload identity and bytes,
snapshot identity, source/target structural fingerprints, and confirmed-
profile revision. `target_snapshot_captured_at` now comes from the retained
`InstallationSnapshotContract.CapturedAt` value. Acquisition readback validates
the request bytes/hash, sealed input, accepted producer and enumeration
versions, attempts and fencing, all checkpoint bytes/hashes and publication
checkpoint values, progress ownership, publication payload identity/hash/
length and staged manifest, exact provenance fields, and application links.
Only after that binding succeeds is a provenance fingerprint calculated.
Tampered history returns the typed application conflict response and is never
projected as authoritative history.

The consolidated changed-surface review found that the older diagnostic and
evidence component readers were still public even though only the aggregate
read can prove revision coherence. They are now private implementation details;
the coherent snapshot is the sole persistence readback operation exposed to
the application layer. It also found that canonical source, source-operation,
and source-snapshot hydration still occurred after the coherent store lock was
released. Those retained bytes and records now participate in the same locked
snapshot and are validated before return. The same review added explicit mutation coverage for a
historical acquisition event's retained projection bytes and publication
payload length, and made the deterministic concurrency hook fail-safe so a
failed assertion cannot leave the store read lock held. Re-review found no
remaining alternate readback authority.

Preparation and acquisition events are merged by their retained occurrence
times with deterministic tie-breaking; synthetic arithmetic no longer
manufactures a chronology. The acquisition evidence page now contains the
actual retained capture gaps, semantic extraction gaps, and semantic failures,
classified as inert `lifecycle`, `capture`, `semantic`, or `semantic-failure`
summaries. Terminal gaps have an independent cursor and a maximum of 100
entries per response. Plan-wide limitation gaps and non-startable reasons are
computed from the whole retained plan on every member page, while detailed
coverage rows remain correlated to the visible member page.

### Preparation-owned target input authority

`TargetedVerificationPlan` schema `1.2.0` adds the preallocated successor run
identity plus exact retained references for the recomputed target
`CandidateDeliveredInput`, the separate complete correlation-coverage input,
and the target resolved-input manifest. Production planning creates and
content-addresses all three payloads in the same transaction that publishes
the immutable plan. The manifest binds the preparation, preallocated successor,
source occurrence/payload, fresh snapshot, acquisition/semantic output, scope,
coverage and its serialized-input fingerprint, delivered input,
configuration/context revisions and fingerprints, and explicit retained reuse
proofs.

Start recomputes everything dependent on the fresh snapshot, validates it
byte-for-byte against the prepared delivered and coverage inputs, validates
the prepared manifest bytes and content address, and binds those same retained
rows into the ordinary `managed-analysis-v1` request and atomic admission. An
omitted requested run ID resolves to the prepared successor; a different
requested run ID fails before any run, command, operation input, admission,
lineage, or scheduling write. Exact retry preserves the prepared manifest.
There is no targeted analysis operation, generic fallback, renderer mapping,
or caller-authored input authority.

The correction reuses schema-16 `targeted_operation_inputs`, payload admission,
and plan rows, so storage contract `1.15.0`, storage schema `16`, migration
fingerprint `727285fbdb9a4a91e850a6bfad3749262be75e6388eae14edf9954eed23d783c`,
and the expected-object inventory are unchanged. Existing unaccepted plan
schema `1.1.0` bytes fail the clean-break `1.2.0` validator rather than being
upgraded into successor authority.

### Contract, evaluation, and focused evidence

Application protocol/contract `1.12.0` adds bounded terminal-gap request and
response pagination. Domain contract `1.6.0` owns the new plan bytes. The
protobuf contract-set fingerprint is
`77076fc13a34bfc7a3d2e3c6808c6e5dbb8048bd38622286eb078c7c705c918b`.
Renderer contract `1.1.0`, its five operation mappings, storage contract
`1.15.0`, 48 declared/42 implemented RPCs, and generated-output ownership are
unchanged. C# client/server output remains build-owned by `Grpc.Tools` in
`src/Infinium.Application/Infinium.Application.csproj`; no generated file is
checked in.

Focused Release verification passed 8 targeted planner unit tests, 35
application/renderer contract tests, and 4 production correlation/native
integration tests. The integration evidence includes revision-coherent
readback, historical event/projection and snapshot/acquisition seal mutations,
actual completed/cancelled/failed/limited gap projection, global pagination
truth, pre-start manifest retention and tamper rejection, exact retry and
requested-run conflict, and the unseeded generated-C# named-pipe path from
begin through real capture, real semantic acquisition, production planning,
exact `managed-analysis-v1` completion, and initiation/reconciliation lineage
readback. Repository-owned `dotnet`/`testhost`/`vstest` survivor count was zero
after the focused .NET batch.

The implementation/evaluation trace remains the accepted EVAL-0019, EVAL-0020,
EVAL-0027, EVAL-0040, EVAL-0041, EVAL-0043, EVAL-0047, EVAL-0048, EVAL-0069,
EVAL-0078, EVAL-0079, and EVAL-0093 set recorded by the WP6 addendum, capability
matrix, and application-contract inventory. Developer-owned conformance tests
remain implementation evidence only; no private evaluator, archive, or
independent semantic oracle was accessed or claimed.

The consolidated semantic, security, provenance, lifecycle, persistence,
migration, contract, generated-client, evaluation, naming, and diff review
found and corrected four additional must-fix defects on the same candidate.
The semantic acquisition record used a descriptive producer label rather than
the real accepted producer identity; prepared coverage used its domain-ledger
fingerprint where the managed operation requires the serialized payload
fingerprint; component evidence readers and later canonical-source hydration
left alternate or post-lock readback seams; and new legitimate product-lineage
`successor` uses lacked exact functional-naming review entries. The corrections
bind the real producer/version, keep the canonical ledger and payload-byte
fingerprints distinct, make the single aggregate snapshot the only application
readback authority including retained source bytes, and add exact reviewed
functional-domain allowlist entries. Re-review found no remaining mixed
generation, unvalidated evidence, manifest substitution, pagination truth,
renderer/generic authority, source mutation, storage-migration, private-
evaluator, or product-meaning defect. Owner decisions and remaining
implementation limitations: none. Checkpoint C review remains the external
acceptance gate.

The complete final verification floor passed locked restore, warning-free
Release build, all 738 tests with 10 expected skips, format verification,
documentation validation, functional naming, dependency-manifest consistency,
and `git diff --check`. Repository-owned `dotnet`/`testhost`/`vstest` survivor
count was zero after every .NET batch and at closeout.

## Corrected Checkpoint C stable-pagination and lifecycle-evidence candidate

Date: 2026-08-27

Status: Implementation candidate awaiting another corrected Checkpoint C
architecture-steward review

Starting commit: `2a8fd071f64e26d5e7c00a6a2053311031511a3e`

This correction removes diagnostic text from terminal-gap continuation. The
application now returns a bounded opaque token that binds the preparation ID,
revision and fingerprint, acquisition durable sequence, continuation offset,
and exact ordered gap-set fingerprint. A token is at most 160 UTF-8 bytes and
therefore always fits the generated request contract. Malformed, substituted,
or stale tokens fail closed; a source-like stable ID, path fragment, or other
hostile diagnostic text has no continuation authority.

Lifecycle readback now uses schema-17 `targeted_lifecycle_evidence`, an
append-only unified ledger with an immutable per-preparation sequence. Each row
retains and seals the ordering, owner and owner event, owner sequence/revision,
kind, generation, coordinator fence, complete event JSON, complete projection
JSON, timestamp, inert summary, and evidence fingerprint. Readback cross-checks
the sealed row against the exact retained preparation or acquisition owner
event before projecting it. Lifecycle cursors bind the immutable sequence and
seal; later appends, including equal-timestamp events, cannot renumber earlier
pages. Projection rebuild, restart, and backup/restore retain the same order.

The schema-16 to schema-17 migration is
`targeted-lifecycle-evidence-0017`. It validates the schema-16 source, creates
the strict table, index, and update/delete denial triggers, backfills existing
preparation and acquisition histories deterministically, validates population
and seals, updates metadata/receipt/user-version atomically, and rolls back
without partial schema-17 objects when retained history is tampered. Storage
contract `1.16.0`, schema `17`, and schema fingerprint
`69c73053cc861efd6edd2ce27cfeba6c8bda0c42afd22e713bdbc691fbdaca50`
are the current expected-object authority. The expected append-only object
inventory now includes `targeted_lifecycle_evidence`,
`idx_targeted_lifecycle_preparation`, and its two mutation-denial triggers.

Application protocol/contract `1.13.0` reserves the former numeric lifecycle
continuation fields, adds bounded opaque request/response lifecycle cursors,
and exposes each owner's durable sequence. Domain `1.6.0`, targeted-plan
`1.2.0`, and renderer `1.1.0` are unchanged. The exact protobuf contract-set
fingerprint is
`d234d44dabf902041461b5c2318fd5c71f10eff46e7ec75f9a586812fab014c7`.
The application inventory remains 48 declared/42 implemented RPCs.
Generated C# client/server output remains build-owned by `Grpc.Tools` through
`src/Infinium.Application/Infinium.Application.csproj`; no generated output is
checked in. `StartTargetedVerification` remains native-only-never-map, and the
renderer registry gains no operation.

Focused Release evidence passes 13 lifecycle/persistence/migration tests, 33
application/renderer contract tests, and the primary generated-C#
gRPC named-pipe corpus test. That primary test still performs unseeded begin,
real capture, separately owned semantic acquisition, production planning,
prepared-plan inspection, exact `managed-analysis-v1` start and completion,
and initiation/reconciliation lineage readback. It additionally pages four
hostile 161-512-character capture gaps without duplication or omission,
rejects malformed/substituted/stale gap cursors, and proves a lifecycle page is
unchanged when successor admission attempts to append concurrently and after
that append commits.

The consolidated review found and corrected nine must-fix defects on the same
candidate: the initial aggregate evidence query exposed a newly committed
snapshot link to an older preparation generation; unbound acquisition history
could similarly appear ahead of the preparation revision; the lifecycle page
query itself was initially broader than that revision-scoped validation;
schema-16 equal-timestamp backfill used a lexical tie-break that could invert
known cross-owner causality; cursor decoding accepted noncanonical alternate-
width or uppercase hexadecimal components; seeded tests used non-product
lifecycle names that could not be sealed; owner-event hashes were not rechecked
during whole-population validation; and store disposal was not serialized with repository
operations. The corrected readback selects only evidence named by the retained
generation while whole-store migration/open/rebuild validation still covers
every bound and temporarily unbound event. Equal-timestamp backfill now uses
the accepted producer phases plus each owner's retained sequence to reconstruct
a deterministic causally valid order. Re-review found no product-meaning
change, weakened provenance, generic backend/renderer authority, lifecycle
renumbering, hostile-text authority, partial migration path, or functional-
naming exception. No allowlist change was required. No private evaluator,
evaluator archive, development-history archive, legacy archive, or independent
semantic oracle was accessed.

The ninth review/floor defect was bounded documentation evidence that exceeded
the inventory's existing 512-character bound and the capability matrix's
existing 256-character bound. The evidence was split or shortened without
changing meaning, and the two affected authority-contract tests passed on the
same candidate.

The complete final floor passed locked restore, warning-free Release build,
all 749 tests with 10 expected skips and zero failures, format verification,
documentation validation (153 metadata files, 155 Markdown link sources, and
19 JSON files), functional naming (193 exact reviewed exceptions and zero
unexplained findings), dependency-manifest consistency, and `git diff --check`.
The exact correction commit is reported in the handoff because a commit cannot
contain its own final identity. Repository-owned `dotnet`/`testhost`/`vstest`
survivor count was zero after every .NET batch and at closeout. WP5, WP6,
Checkpoint C, Phase D, and M2 remain unaccepted/inactive.

## Corrected Checkpoint C non-startable-plan and migration-tamper candidate

Date: 2026-08-27

Status: Implementation candidate awaiting another corrected Checkpoint C
architecture-steward review

Starting commit: `122d9523f04b793697a16f960234a0a81e3d3fc5`

Plan publication now treats a dependency-closed but non-startable result as a
valid product outcome. `plan-published` may seal `Invalidated` as well as
`Ready` and `ReadyWithGaps`; `Failed` remains reserved for an actual
preparation failure. Ambiguous and missing-required-proof production plans are
retained with their immutable plan bytes, complete coverage denominator,
non-startable reasons, gaps, prepared inputs, and lifecycle evidence. Readback
remains available, while attempted admission is rejected before any run, job,
durable command, operation input, targeted admission, initiation lineage, or
successor acquisition link is written.

Schema-16 acquisition-event migration now parses a closed JSON object and
requires the exact field set for each retained event kind: `admitted`,
`recovered`, `dispatched`, `published`, `failed`, and `cancelled`. The event's
projection hash is checked and each kind is cross-bound to its retained run,
command, generation, attempt, fence, lease, publication, payload, failure
projection, or preparation cancellation authority as applicable. Event IDs,
owner sequences, timestamps, and the closed acquisition projection shape are
validated before schema 17 can seal the unified lifecycle row. Unknown, added,
missing, duplicated, or substituted fields therefore cannot be normalized into
new trusted history.

Migration mutation tests temporarily remove the exact append-only trigger only
inside the tamper transaction, restore the exact retained trigger SQL, and
then prove the computed schema fingerprint, stored schema fingerprint,
schema-16 user version, and trigger are all intact before migration. Rejection
leaves schema 16 in place, creates no `targeted_lifecycle_evidence` table, and
writes no schema-17 migration receipt. Legitimate non-startable history still
migrates and remains inspectable after restart. Existing rebuild, restart,
backup/restore, cancellation, ordinary invalidation, failure, `Ready`, and
`ReadyWithGaps` evidence remains green.

No serialized schema, protobuf, domain contract, generated-client input,
expected database object, or migration SQL byte changed. Application remains
`1.13.0`, domain remains `1.6.0`, storage remains `1.16.0`/schema `17`, the
persisted targeted plan remains `1.2.0`, the protobuf fingerprint remains
`d234d44dabf902041461b5c2318fd5c71f10eff46e7ec75f9a586812fab014c7`,
and the storage schema fingerprint remains
`69c73053cc861efd6edd2ce27cfeba6c8bda0c42afd22e713bdbc691fbdaca50`.
The inventory remains 48 declared/42 implemented RPCs. Generated C# ownership
and `StartTargetedVerification` native-only-never-map policy are unchanged.

Focused Release evidence passed 36 correction/lifecycle/migration cases, the
complete 70-test persistence/lifecycle class, and the primary generated-C#
named-pipe corpus test. The named-pipe test continues to execute unseeded begin,
real fresh capture, separately owned semantic acquisition, production
planning, inspectable readback, exact `managed-analysis-v1` start and
completion, and initiation/reconciliation lineage. A broad pre-floor pass
reported 774 passed, 10 expected skips, and zero failures. Repository-owned
`dotnet`/`testhost`/`vstest` survivor count was zero after every batch.

The consolidated semantic, security, provenance, lifecycle, migration,
contract, evaluation, naming, and diff review found one additional test-
evidence defect: the trigger-preserving mutation helper proved the recomputed
schema fingerprint but did not independently assert the stored fingerprint and
schema-16 user version. The same candidate now verifies all three source
authorities before and after rejected migration. Re-review found no remaining
non-startable taxonomy, partial-successor, closed-event-shape, retained-binding,
migration-rollback, renderer/generic authority, private-evaluator, or product-
meaning defect. Remaining implementation limitations and owner decisions:
none. WP5, WP6, Checkpoint C, Phase D, and M2 remain unaccepted/inactive.

## Accepted Checkpoint C and Phase D activation receipt

Date: 2026-08-27

Accepted candidate: `2ec3be78da4d05d8c6ada68a3e18544a446f2f03`

Disposition: the architecture steward independently accepted corrected WP5,
WP6, and Checkpoint C and authorized Phase D. This receipt supersedes the
earlier suspended Checkpoint C status while preserving every correction and
review detail above.

Independent verification reported 774 passed tests, 10 expected skips, zero
failures, all non-test gates passing, a clean worktree, and zero
repository-owned `dotnet`/`testhost`/`vstest` survivors. Phase D begins with
WP7. WP8 remains gated on WP7's focused implementation, review, correction,
and re-review cycle. Phase E remains blocked, Checkpoint D is not accepted,
and M2 remains inactive.

## WP7 generated consumer implementation and focused review

Date: 2026-08-27

WP7 now has one deterministic generator over the sole reviewed renderer
envelope schema. The schema's closed metadata owns native targets and operation
messages alongside payload fields; the generator emits the registry, strict
schema-derived TypeScript payload/client bindings, and a generated C# catalog
with closed native-client, projection-codec, exhaustive dispatch, and
host-control adapter signatures. Registry `1.1.0` / renderer contract `1.2.0` contains seven
operations and thirteen message shapes: bootstrap, `ListResultItems`,
`GetResultDetail`, `GetProgress`, all four `SubscribeEvents` variants,
transport-only cancellation, and authoritative resync. Each application
operation maps to exactly one generated application-client method. Raw run
commands and targeted-verification RPCs remain unmapped.

The TypeScript consumer includes exhaustive runtime decoders, discriminated
failure unions, lossless decimal-string `uint64`/`int64`, explicit optional-
scalar availability, an 8,192-byte cursor / 10,923-character base64url bound,
opaque product-identity UTF-8 validation, closed story fake/generated-bridge
client selection,
and deterministic setup, empty, active, completed, failed, gap, lead-only,
stale, conflict, reconnect, and 100,000-summary paged stories. Product text,
including hostile markup-shaped strings, remains inert data.

The first review/correction pass found and corrected these must-fix defects on
the same candidate:

- the existing bootstrap adapter rejected the real `ResultExploration` and
  `DurableUserReview` capability values;
- initial result kinds, sorts, summary/detail fields, lifecycle values, and
  progress shapes diverged from the real protobuf projection;
- JSON integers could lose precision in JavaScript above `2^53`, and optional
  scalar availability was collapsed;
- the first cursor and identity grammar conflated authenticated cursors,
  renderer IDs, and opaque product identities;
- the first event union omitted lifecycle, projection-invalidated, resync,
  coordinator/fence/durable-sequence/run-scope metadata, and the decoder read
  progress before discriminating event kind;
- case occurrence identity was incorrectly required for finding, gap, and
  failure summaries;
- the first failure union allowed mismatched outcomes/error codes and optional
  conflict/resync metadata; and
- the first generated C# output catalogued operations but did not generate the
  exact native adapter signatures WP8 needs;
- the first ownership model treated the handwritten registry and schema as two
  reviewed inputs and still handwrote TypeScript field templates in the
  generator; one schema now owns fields and native-target metadata and all
  three checked-in outputs participate in the drift check;
- the first native codec omitted request-derived conflict provenance and did
  not prove result list/detail/progress or all four event protobuf projections
  through the active renderer schema; the closed codec and round-trip suite now
  cover accepted and typed non-success projections without invented values;
- result-page continuation, signed cost bounds, and shared fake/generated-
  bridge request validation initially left malformed parity gaps; the corrected
  client rejects inconsistent cursors, overflow, duplicate kinds, bad bounds,
  overlong/NUL-bearing search, and unknown native states before dispatch.

A second consolidated review found and corrected these must-fix defects on the
same candidate:

- generator dispatch and signature metadata still relied on a fixed operation
  count and handwritten native associations; the sole envelope source now owns
  every operation kind, request/response or event type, and client method, and
  generation derives the TypeScript and C# surfaces exhaustively;
- the generated bridge client did not initially apply the complete runtime
  schema to received responses/events, and runtime checks did not yet enforce
  every required field, closed value, added-property denial, contract version,
  message-size bound, opaque identity rule, or canonical cursor rule; the
  shared validator and hostile mutation corpus now fail closed, including
  base64url decode/re-encode equality and an 8,192 decoded-byte cursor ceiling;
- projected result/detail/progress/event identities were not all bound to the
  originating request; the C# codec and generated TypeScript client now reject
  cross-run, cross-item, cross-subscription, and cross-request substitution;
- initial story equivalence was list-only and did not cross an independent
  serialized transport boundary; every required story now exercises and
  compares its applicable bootstrap, list, detail, progress, cancellation, and
  event semantics through both the story client and generated bridge client;
- the gap story used the ordinary completed lifecycle and fake cancellation
  skipped shared request validation; the corrected story uses
  `completed-with-gaps`, and both client modes validate the same request;
- drift checking regenerated tracked files before comparing them; it now
  derives outputs in memory and performs a non-mutating byte comparison, with
  a clean-tree proof;
- the inventory and compact records contained stale ownership and package-gate
  language, and functional naming found three chronology-flavored helper
  tokens; the records now identify the exact mapped RPCs and explicitly
  unmapped operations, and the helpers use product-behavior names; and
- the first final rerun exposed one analyzer-only repeated-array finding in the
  signature-reflection test; a shared immutable expected set corrected it
  without changing product behavior; and
- the renderer registry introduced an invented consumer-only maturity value,
  outside the execution-policy maturity vocabulary; the sole source, registry
  schema, and regenerated registry now use the established
  `producer-consumer-validated` machine value.

A third independent review reopened WP7 for these must-fix findings on the same
candidate:

- TypeScript request, response, and event operation partitions, payload maps,
  and dispatch were not all generated from the sole schema metadata; the
  generator now emits exact keyed partitions, handlers, exhaustive dispatch,
  and runtime omission/extra-entry assertions;
- the generated bridge validated payload shapes but did not bind every outer
  response/event field to the originating request or subscription, reject
  repeated/non-monotonic events, route cancellation through the same response
  validator, or enforce the 1 MiB bound at the serialized production seam;
- result pages did not reject kinds outside the request filter, and progress
  event projections did not independently bind their inner run identity to the
  request and event metadata;
- story checks did not state an explicit semantic expectation for every story,
  and the full typed non-success set was not compared through both client
  modes; and
- TypeScript's Apache-2.0 license was documented in prose but not explicitly
  curated in the machine-readable dependency input.

The corrected candidate uses a serialized string bridge, validates and binds
contract/session/sequence/request/subscription/operation/revision fields,
enforces monotonic envelope and durable-event sequences, rejects replay and
oversize messages, and applies the same path to cancellation. C# and
TypeScript reject cross-run and unrequested-kind list items and cross-run inner
progress events. All eleven story names have exact configuration, list,
detail, lifecycle, cancellation, and event expectations, while all seven typed
non-success outcomes are compared across every unary operation in story-fake
and generated-bridge modes. The dependency curation now names
`Microsoft.TypeScript.MSBuild/5.9.3` as Apache-2.0 explicitly.

The review item proposing renderer mappings for `ListFindings`,
`ListFindingReports`, and `GetFindingReport` was rejected as a contract-
authority mismatch, not deferred as a defect. The accepted Phase C consumer
seam identifies `ListResultItems` plus `GetResultDetail` as the bounded
100,000-item renderer path. The legacy `ListFindings` operation deliberately
has no generated WP7 consumer, and the report operations remain native-only.
No product meaning was invented to map them.

Final re-review exercises every corrected seam. Repository-owned Node `24.14.1` and
TypeScript `5.9.3` are NuGet-restored from a locked tool project; the cached
restore passes with package sources cleared and forced assets reevaluation.
Strict TypeScript compilation passes, the six-rule first-party security/source
lint passes over six TypeScript files, and the compiled frontend unit suite
passes. The suite pages exactly 100,000 summaries in pages of at most 100 and
covers hostile, malformed, unknown, mutation, metamorphic, max-`uint64`,
max/oversized cursor, opaque-identity, all lifecycle/event variants, stale,
conflict, reconnect/resync, and generated bridge-client/story fake-client
equivalence cases.

Focused Release contract evidence passes 259/259 tests, including 71/71 in the
renderer contract class. A real named-pipe
coordinator integration test passes 1/1 and sends the exact
`ApplicationGrpcService.GetApplicationBootstrap` projection through
`RendererBootstrapAdapter`, proving both Phase C capabilities survive. Exact-
root process cleanup reported zero repository-owned
`dotnet`/`testhost`/`vstest` survivors after every batch.

The generated ownership, commands, offline behavior, versions, licenses, and
remaining WP8-only limitations are recorded in
`frontend-toolchain-and-generation.md`. The focused WP7 implementation,
review, correction, and re-review cycle passes and authorizes continuation to
WP8 on the same Phase D candidate. WP7's real-service evidence is deliberately
narrow: the named-pipe test proves the actual application bootstrap service and
adapter; result, detail, progress, and event consumption are proved through
real protobuf shapes and the generated serialized bridge boundary, while the
desktop host's real-service request adapters belong to WP8. This does not
accept Checkpoint D, begin Phase E, or activate M2.

## WP8 receipt — Protected desktop consumption proof

WP8 adds a minimal non-elevated WPF host, packaged React diagnostic renderer,
and a narrow client-only application bridge. The host maps only
`ApplicationQuery`, `EventStream`, and `KeysetCursor` capabilities through one
session-owned named-pipe connection; it cannot acquire the native durable-
command surface. Generated operation dispatch maps bootstrap, result pages,
result detail, progress, and events to exact typed client methods. Targeted
verification remains native-only and unmapped.
The solution now contains 21 projects (20 under `src`/`tests` plus the existing
public-fixture tool project), including the new narrow application client,
desktop host, and dedicated desktop test projects.

The browser is confined to exact
`https://app.infinium.invalid/index.html`. Its non-resolving virtual host uses
`HostResourceAccessKind.Deny`, an exact compiled asset-manifest hash, restrictive
CSP, and `trusted-types 'none'`. Release disables host objects, DevTools,
context menus, script dialogs, remote/debug overrides, downloads, permissions,
new windows, external frames/resources, authentication, client/server
certificate continuation, autofill, password saving, external drops, and
inherited privileged WebView2 arguments. The runtime check uses
`CompareBrowserVersions` against exact floor `151.0.4129.50`; missing,
outdated, environment-overridden, and HKCU/HKLM policy-overridden states fail
closed. Production accepts no filesystem-root argument and uses a fixed local
root and exclusive WebView2 user-data folder.

The generated transport contract is registry `1.3.0`, renderer contract
`1.4.0`, and registry SHA-256
`411a9c05604c7664773aa62c36f62817273ecaff228f20e074063bed1414cfa9`.
It contains nine closed operations and sixteen exact message shapes. A fresh
renderer receives a host-generated 128-bit session initialization only after
exact-origin navigation and must acknowledge the same session, registry
version, and SHA before any application request. Renderer requests and host
events use separate contiguous sequence domains; responses echo request
sequence. Reload, renderer failure, browser loss, or reconnect rotates the
session and rejects stale/replayed traffic. One bounded serialized outbound
queue assigns event order at committed delivery, permits at most 64 active
requests, caps every UTF-8 message at 1 MiB and chunks at 256 KiB, and cancels
rather than dropping durable events when its 64-item lossless queue overflows.
Cancellation requires a one-shot host-attested native WPF gesture bound to the
same session, operation, and prior active request; self, late, stale, or replayed
cancellation is rejected.

The first executable candidate exposed and corrected these must-fix defects:

- a renderer-selected session could not prove host ownership; generated
  transport initialization and acknowledgement now establish it;
- one shared sequence counter raced renderer requests with host events;
  independent directional domains and an interleaving test corrected it;
- handwritten operation switching could drift from the registry; generated
  typed dispatch and mutation/totality checks now own selection;
- a 64-entry historical request-ID set exhausted a session before 100,000-item
  paging; contiguous request sequence now prevents replay while the bound
  applies only to concurrent work;
- renderer-minted gesture UUIDs granted no user authority; the accessible WPF
  cancel control now issues and consumes an exact host-attested grant;
- the first pager multiplied page number by 100 and reused a global cursor,
  corrupting partial and previous/next pages; cached page records now retain
  exact logical start, accepted count, and continuation;
- initial subscription/resync code captured stale React state and could leave
  an old real host stream alive; explicit renderer refs and serialized host
  replacement cancel and await the previous stream before resubscribing;
- a stream failure invented a projection revision; the bridge now emits
  resync only from the last authoritative event revision or re-establishes the
  session when none exists;
- outbound delivery and disposal did not initially prove slow-consumer,
  overflow, blocked-send, and stale-post behavior; one owned queue and awaited
  subscription tasks now provide that closure;
- browser/controller loss initially reloaded a dead control and retained old
  handlers; single-flight recreation detaches the old environment/controller,
  disposes the old bridge, creates a new controller, and resynchronizes;
- the first host transitively packaged the complete application, analysis,
  persistence, MO2, and provider graph at about 92 MiB; the new
  `Infinium.ApplicationClient` project carries only protobuf, descriptor,
  named-pipe, generated-contract, codec, and client concerns;
- the real progress path exposed a null `Cost` projection in the accepted
  Phase A-C service; the service now returns its canonical empty cost shape;
  and
- mutable adjacent asset hashes and incomplete dependency evidence could not
  prove provenance; the compiled manifest anchor, deterministic desktop-asset
  generator, React source/package hashes, WebView2 notices, and curated license
  files now fail drift independently.

The corrected direct desktop suite passes 18/18. It covers exact origin,
runtime and HKCU/HKLM/environment policy denial, session/sequence/replay,
more than 1,000 sequential requests, 65-concurrent rejection, host gestures,
self/late/stale cancellation, replacement subscriptions, stream failure before
an authoritative event, oversized outbound denial, deterministic slow-consumer
overflow/resync, awaited disposal, stale-send suppression, asset anchoring,
and partial/previous/evicted/1,000-page logical paging. The populated-state
integration seed passes 1/1 and retains 150 findings including hostile markup-
shaped text. The actual WebView bridge qualification passes 1/1 through real
accepted bootstrap, two cursor pages, detail, progress, event subscription,
host cancellation, five authoritative resync-and-resubscribe cycles, and three
reload/session rotations. The lifecycle qualification passes 1/1 after actual
renderer crash, exact browser-process termination, coordinator termination and
restart, and WPF shell restart. Exact-root cleanup reports zero repository-
owned .NET, coordinator, desktop, or WebView2 survivors.

Automated accessibility evidence covers Chromium's full accessibility tree and
Windows UI Automation exposure for document, landmark/group, list, button,
edit, text/status, accessible-name, and focus paths. Actual keyboard Tab
movement, result-focus restoration, computed body contrast of at least 7:1,
200% zoom/reflow without truncated rows or horizontal document overflow,
reduced-motion emulation, bounded zoom policy, and ARIA logical position/count
on mounted rows pass. This is a repeatable automated screen-reader-consumable
surrogate; no manual Narrator walkthrough was performed or claimed.

The repeatable command
`powershell -NoProfile -ExecutionPolicy Bypass -File eng/qualify-desktop.ps1`
passed with locked offline frontend restore, deterministic contract/asset
drift, strict type, lint, and frontend tests before the native checks. On the
recorded Windows `10.0.26200.0`, 32-logical-processor AMD64 reference machine
with Evergreen WebView2 `151.0.4129.107`, its retained receipt reports:

Process-to-browser-ready is measured from executable creation until the
exclusive WebView2 process tree first appears. Window-show-to-bootstrap ends
only after exact-origin navigation, transport acknowledgement, real
`GetApplicationBootstrap`, and React rendering. Each bridge sample ends after
the generated request, named-pipe round trip, generated projection, exact
response, and React state update. Private working set sums the qualification
WPF process and every process reported by its exclusive WebView2 environment.

| Measurement | Raw samples / result |
|---|---|
| Process to browser-ready | `597, 547, 559, 463, 517, 526 ms`; p50 `526`, p95/max `597` |
| Window show to accepted bootstrap | `814.5471 ms` |
| Finding page bridge | `125.6093, 63.0448, 63.0681, 62.2585, 62.6262, 64.2206, 58.4372, 62.6299, 62.8699, 62.8668 ms`; p50 `62.8668`, p95/max `125.6093` |
| Finding detail bridge | `250.1823, 62.6795, 62.6340, 56.2876, 61.7804, 60.8160, 61.9753, 62.9696, 61.5254, 62.0303 ms`; p50 `61.9753`, p95/max `250.1823` |
| Progress bridge | `69.6017, 62.9998, 61.7418, 60.6117, 62.7543, 62.5354, 62.2732, 62.9664, 62.9582, 63.5777 ms`; p50 `62.7543`, p95/max `69.6017` |
| Authoritative resync | `62.5677, 62.9370, 61.6318, 62.8755, 61.1398 ms`; p50 `62.5677`, max `62.9370` |
| Reload to bootstrap | `92.5748, 91.8473, 93.4831 ms`; p50 `92.5748`, max `93.4831` |
| Idle WPF + WebView private set | `358,297,600, 358,305,792, 359,387,136, 358,711,296, 356,519,936` bytes; p50 `358,305,792`, max `359,387,136` |
| Active WPF + WebView private set | `411,574,272, 411,574,272, 411,996,160, 412,983,296, 412,057,600` bytes; p50 `411,996,160`, max `412,983,296` |
| Observed request/response/event maxima | `912 / 37,261 / 1,826` UTF-8 bytes |
| Host package / assets | `6,592,576` bytes in 31 files / `278,005` bytes in 11 files |
| Installed Evergreen runtime | `894,464,255` bytes in 779 files |

The secret-canary evidence spans thrown application-client failures, serialized
bridge output, renderer/WPF text and focus state, coordinator stdout/stderr,
ordinary retained root/IPC/log files, reload, renderer/browser crash, and
failure artifacts in the exclusive qualification user-data root. The canary
was absent. Hostile product text remained a text node, with no created image or
script effect. React/ReactDOM `18.3.1` are MIT; WebView2 SDK `1.0.4129.50` is
BSD-3-Clause with bundled notices; repository Node `24.14.1` and TypeScript
`5.9.3` remain locked and offline-capable. No credential or live provider was
used.

Focused WP8 re-review result: no remaining must-fix, owner/authority, or
safety/isolation finding. Remaining limitations are intentional: this is a
diagnostic surface, not the M2 interface or installer; Evergreen WebView2 must
already be installed; public redistribution/SBOM closure remains later
distribution work; and the automated accessibility surrogate does not claim a
manual Narrator session. The Phase D candidate awaits Checkpoint D. It does not
accept that checkpoint, begin Phase E, or activate M2.

### Independent WP8 correction and re-review receipt

The first WP8 re-review above was reopened by independent review. The same
mutable candidate corrected every must-fix finding without changing product
meaning or renderer authority:

- host events now require exactly the prior host-event sequence plus one;
  forward gaps and replay fail without committing sequence state, and an exact
  recovery event succeeds;
- generated TypeScript owns complete schema-derived discriminated request,
  response, and transport-event envelopes. Only `application.cancel` may carry
  the generated gesture proof, and renderer subscription-request maps remain
  bounded to 64;
- `DesktopApplicationClient` now owns one reconnectable least-authority
  session connection. It rereads a fresh descriptor and creates a fresh pipe
  for at most three bounded attempts, invalidates failed channels, and proves
  same-renderer-session coordinator recovery. A renderer reports successful
  resynchronization only after accepted bootstrap, progress, and first-page
  results;
- host gesture grants expire after five seconds and are retired when delivery
  fails or their target completes. Self, late, stale, wrong-target, and replay
  cancels remain rejected, and the WPF async click handler contains delivery
  errors without granting authority;
- a closed replaced subscription could previously discard a final serialized
  event without advancing the shared host-event domain. The generated client
  now validates and advances that closed-subscription event before dropping its
  content, preserving exact sequence continuity for the next live event or
  gesture;
- live hostile navigation found that a deliberately cancelled top navigation
  was being mistaken for renderer failure by `NavigationCompleted`. The host
  now records and consumes the exact denied navigation identity without
  rotating a valid session;
- actual WebView qualification now asserts release settings and attempts denied
  top navigation, external frame/resource, `window.open`, download, and
  geolocation permission. The exact canonical origin remains loaded and no
  download artifact is created;
- the 100,000-summary proof runs all 1,000 pages through
  `GeneratedBridgeApplicationClient`, transfers no page larger than 100,
  retains no more than 500 summaries, and mounts exactly 13 logical rows;
- lifecycle qualification performs accepted real finding/progress work,
  recovers a coordinator restart inside the same renderer session, and then
  resumes authoritative state and one real subscription after renderer reload
  and WPF shell restart. The separate no-prior-event failure path still rotates
  rather than inventing a revision;
- missing and outdated Evergreen states now exercise the actual inert WPF
  fallback without creating a WebView; and
- qualification launches use only a 128-bit opaque qualification identity that
  derives one fixed temporary root. Memory and cleanup traverse only the exact
  launched process tree; cleanup never scans or stops the shared production
  WebView root or another repository application; and
- the repeated final harness exposed a cancellation/disposal race when a
  subscription token source was disposed between ownership lookup and
  cancellation. Cancellation is now idempotent and contains
  `ObjectDisposedException`; five consecutive direct-suite runs and the full
  focused harness rerun passed after the correction;
- a final generated-type mutation review found that the envelope merge omitted
  branch-level `not.required` exclusions. The sole-source generator now emits
  every forbidden branch property as exact optional `never`; nine compile-time
  mutations prove non-cancel gesture denial, required cancel proof, transport
  event omission, and response gesture denial;
- host-event sequences were previously consumed before `PostWebMessage`
  succeeded. The single ordered outbound pump now peeks the next sequence and
  commits it only after successful delivery; the failed-grant test validates
  initialization and the next delivered grant through renderer acceptance as
  exact sequences one and two; and
- no-prior-event stream recovery previously awaited browser recreation from
  the owned subscription that recreation must dispose. Recovery is now
  generation-guarded and scheduled after fail-closed lifetime cancellation;
  a callback that disposes the originating bridge completes without deadlock
  and creates a distinct replacement session. The live rerun also corrected a
  transient-status assertion to observe the durable active-subscription state
  after an accepted event updates the status region;
- the real application bootstrap still described frontend planning and
  checkpoint chronology to users. Its capability reasons now state only
  durable functional availability and native-only limitations, and the real
  coordinator integration asserts the exact text and excludes phase,
  checkpoint, and milestone language;
- story progress previously relied on defaults that could contradict the
  represented lifecycle. Every story now owns explicit mutually exclusive
  counters whose sum equals its total: setup is queued, active/reconnect split
  completed/running/queued, completed and lead-only are completed, failed is
  failed, gap is gap-only, and terminal empty remains zero;
- story detail could fabricate an accepted projection for a requested identity
  absent from the story, while list ignored kind and search. Detail now accepts
  only an exact present run/kind/item and otherwise returns typed `not-found`;
  list applies kind filtering and a case-insensitive inert-summary substring
  search over source-offset pagination. Story-fake and generated bridge tests
  prove exact parity for both positive and negative cases; and
- evaluation traceability and contract entry documentation overstated Phase D
  coverage or omitted independent version axes. The corrected records narrow
  EVAL-0088, EVAL-0089, and EVAL-0091 to exercised facets and identify the
  renderer `1.4.0`, registry `1.3.0`, 9-operation/16-message, sole-source,
  fingerprinted boundary and its denied authorities; and
- the story client treated an unknown run as an empty list and synthesized a
  progress/event projection, unlike the real application. The story boundary
  now exports one exact accepted run identity, returns typed `not-found` for
  unknown-run list/progress requests, and fails unknown-run subscription
  initiation without delivering an event. Same-request fake/generated bridge
  tests prove all three failure paths and all ordinary story inputs use the
  accepted run; and
- the first complete-floor attempt exposed five integration-policy defects:
  live WebView categories shared a WPF `Application` with another test thread,
  two capability evidence atoms exceeded the repository short-text bound,
  central package expectations omitted the three pinned frontend/desktop
  packages, the dependency test omitted the repository frontend toolchain
  lock, and the completed React acquisition directory remained under `work`.
  Live categories now become explicitly inconclusive before touching WPF when
  the qualification harness root is absent, while `eng/qualify-desktop.ps1`
  still runs each in its own populated process. The evidence atoms are split,
  the exact 17-package policy and 21 product/test/toolchain locks are asserted,
  and generator scope matches the manifest's exact sorted lock list. The
  verified task-owned acquisition directory was removed, repository scans now
  exclude the ignored `work` root, a Git-index assertion keeps it untracked,
  and future third-party acquisition is directed outside the repository.
- final dependency prose review removed an inaccurate umbrella MIT statement:
  the README now names every license family recorded by the manifest, and
  explicit provenance limitations describe admitted use within each package's
  recorded build, test, or runtime role rather than claiming all are runtime
  dependencies.

The corrected direct desktop suite passes 21/21; an unfiltered desktop run
passes those 21 with two expected live-qualification skips when the exclusive
harness root is absent. The focused renderer contract class passes 78/78. The populated-state seed, actual hostile/accessible
WebView qualification, and renderer/browser/coordinator/reload/shell lifecycle
qualification each pass 1/1. The locked offline frontend restore, generated
contract/asset drift, strict type check, six-policy lint, and frontend unit
suite all pass. Exact launched desktop/WebView and repository-owned
`dotnet`/`testhost`/`vstest` survivor counts are zero.

The complete sanitized measurement bytes are now tracked, rather than claimed
from an ignored artifact, in
`desktop-qualification-receipt.v1.json`. The repeatable final focused run on
Windows `10.0.26200.0`, 32-logical-processor AMD64 and Evergreen WebView2
`151.0.4129.107` records six process-to-browser samples with p50 `682 ms`, p95
and maximum `882 ms`; accepted bootstrap `748.0687 ms`; finding-page p50
`62.4288 ms`; finding-detail p50 `62.9515 ms`; progress p50 `60.9923 ms`;
second-page `62.1245 ms`; transport cancel `872.8485 ms`; authoritative-resync
p50 `62.5618 ms`; and reload p50 `94.2052 ms`. Idle combined private working
set has p50 `368,091,136` bytes and maximum `369,188,864`; active has p50
`439,914,496` and maximum `440,352,768`. Observed request/response/event maxima
are `912 / 37,261 / 1,826` UTF-8 bytes. The host package is `6,607,628` bytes
in 31 files, assets are `279,745` bytes in 11 files, and the installed runtime
is `894,464,255` bytes in 779 files. The tracked receipt retains every raw
launch, host/browser memory split, bridge sample, second-page/cancel result,
package/runtime count, exact contract fingerprint, licence entry, coverage
flag, and zero-survivor result.

Independent corrected WP8 re-review result: no remaining must-fix,
owner/authority, or safety/isolation finding. Intentional limitations remain
unchanged: this diagnostic is not the polished M2 interface or installer;
Evergreen is an inertly reported prerequisite; public distribution/SBOM
closure remains later work; and automated Chromium AX plus Windows UI
Automation evidence is a screen-reader-consumable surrogate, not a claimed
manual Narrator walkthrough. Checkpoint D remains external and pending. Phase E
and M2 remain inactive.

### Phase D complete verification-floor receipt

After the first complete-floor defects recorded above were corrected and their
changed surfaces re-reviewed, the same review-ready Phase D candidate passed
the complete accepted repository floor on 2026-08-28:

| Command | Result |
|---|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed; all locked project graphs restored without changing accepted dependency identities. |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed with 0 warnings and 0 errors; repository-owned build-process survivors: 0. |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | Passed: 845 tests, 12 expected skips, 0 failures; repository-owned `dotnet`/`testhost`/`vstest` survivors: 0. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed with no formatting drift; repository-owned format-process survivors: 0. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1` | Passed: 154 metadata files, 156 Markdown link sources, and 20 JSON files. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-functional-naming.ps1` | Passed: 195 exact reviewed exceptions and 0 unexplained findings. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed; the generated manifest matches the exact sorted 21-project lock set and curated dependency/licence/provenance bytes. |
| `git diff --check` | Passed. |

| Test assembly | Passed | Expected skips | Failed |
|---|---:|---:|---:|
| `Infinium.ContractTests` | 266 | 0 | 0 |
| `Infinium.DesktopTests` | 21 | 2 | 0 |
| `Infinium.UnitTests` | 316 | 1 | 0 |
| `Infinium.EvaluationTests` | 68 | 9 | 0 |
| `Infinium.IntegrationTests` | 142 | 0 | 0 |
| `Infinium.FaultTests` | 10 | 0 | 0 |
| `Infinium.SecurityTests` | 22 | 0 | 0 |
| **Complete solution** | **845** | **12** | **0** |

The two desktop skips are intentional in the unfiltered solution process:
live WPF/WebView2 categories fail closed before touching `Application.Current`
unless the exclusive qualification harness root is present. The separate
`powershell -NoProfile -ExecutionPolicy Bypass -File eng/qualify-desktop.ps1`
run passed the 21 direct desktop tests, populated real-state preparation, live
hostile/accessibility WebView qualification, and renderer/browser/coordinator/
reload/shell lifecycle qualification, with each live category executing rather
than skipping and an exact qualification-process survivor count of 0.

This receipt establishes only a completed Phase D candidate awaiting
Checkpoint D architecture-steward review. It does not accept Checkpoint D,
populate WP9 closeout, begin Phase E, activate M2, or claim the polished M2
interface.

### Inherited browser authority and stable-runtime correction

A subsequent independent changed-surface review found that release startup
covered only four legacy WebView2 environment variables and one registry
family, while direct `MainWindow` use and browser recreation could bypass the
startup check. It also found that the host relied on the default channel mask
rather than binding discovery and creation to Stable Evergreen. The same Phase
D candidate was corrected without changing product meaning, application RPCs,
renderer registry `1.3.0`, renderer contract `1.4.0`, the nine-operation/
sixteen-message closure, or registry SHA-256
`411a9c05604c7664773aa62c36f62817273ecaff228f20e074063bed1414cfa9`.

The corrected host now:

- rejects nonempty browser executable, user-data, browser-argument, compatibility
  channel-preference, channel-search, release-channel, wait-for-script-debugger,
  and script-debugger-pipe environment variables;
- scans `BrowserExecutableFolder`, `UserDataFolder`,
  `AdditionalBrowserArguments`, compatibility `ReleaseChannelPreference`,
  `ChannelSearchKind`, `ReleaseChannels`, and `DowngradeVersion` under HKLM then
  HKCU in both 64-bit and 32-bit registry views, using the actual AUMID when
  present, production and actual executable filenames, and only each policy's
  supported wildcard identity;
- accepts only absent or exact-empty values, but turns any unreadable
  environment/policy/identity source
  into inert value-free refusal text with no retained inner exception;
- runs the production-used guard before launch-option parsing, root creation,
  or `MainWindow` construction; runs it again at every browser initialization,
  immediately before every new environment creation, and before a reloaded or
  recovered renderer establishes a new transport session; and
- uses one options instance with `ReleaseChannels=Stable` and
  `ChannelSearchKind=MostStable` for both runtime discovery and `CreateAsync`,
  then verifies the created `BrowserVersionString` is Stable and at least
  `151.0.4129.50` before controller creation.

The expanded Release direct desktop suite passes 31/31 with zero warnings or
errors. Its deterministic matrix covers all eight environment variables, all
seven policy families, both hives/views, AUMID/production/actual executable
identities, supported and denied wildcards, empty values, extensionless-name
non-matching, unreadable-reader sanitization, startup ordering, direct-window
refusal before runtime/Core creation, the second pre-creation check, repeated
initialization, stable options, exact floor, preview-channel, and malformed
version states. The first live rerun exposed an evidence-counter mismatch:
ordinary reload rotated the transport without re-entering browser initialization.
Adding the same guard before post-navigation session establishment corrected
that gap. The final dedicated qualification passed 31 direct desktop tests,
1 populated-state preparation test, 1 hostile/accessibility WebView test, 1
renderer/browser/coordinator/shell lifecycle test, and six production launches;
all attempts cleaned exact owned process trees to zero. Two intermediate live
runs then exposed that CSP can block a hostile resource before the host's
resource-request event observes it. The test now issues that attempt separately
and accepts either the renderer's blocked-fetch result or the host's explicit
resource denial, while retaining the exact negative assertion that no hostile
resource loads. This removed the evidence race without weakening the policy.

The refreshed tracked receipt records Stable Evergreen `151.0.4129.107`, all
four required inherited-override/stable-selection/recovery/value-exclusion
coverage atoms, and boolean process-tree proof without retaining full command
lines. Its new raw run records launch p50 `825 ms` and maximum `1,399 ms`,
bootstrap `1,636.3623 ms`, finding-page p50 `62.3815 ms`, detail p50
`62.7881 ms`, progress p50 `62.295 ms`, second page `61.8183 ms`, transport
cancellation `1,044.4341 ms`, authoritative-resync p50 `62.4257 ms`, reload
p50 `93.991 ms`, idle private-working-set p50 `355,110,912` bytes, active p50
`410,873,856` bytes, and a `6,615,820`-byte 31-file host package. Observed
message maxima and packaged assets remain `912 / 37,261 / 1,826` bytes and
`279,745` bytes respectively.

The complete-floor receipt immediately above remains truthful for the byte set
that passed it, but this later host-policy correction postdates that run. The
corrected bytes therefore await independent changed-surface re-review and a
new complete accepted floor before commit. Checkpoint D remains pending; Phase
E and M2 remain inactive.

Architecture re-review then corrected one policy-identity detail on the same
candidate: pinned WebView2 applies the AUMID-to-executable-to-wildcard fallback
to `UserDataFolder` too; only `DowngradeVersion` is non-wildcard. The production
metadata was corrected accordingly. Tests now bind independent literal lists
of all eight environment variables and seven registry families (including each
wildcard rule), plus mixed-case AUMID/production/actual executable evidence, so
an omitted production entry or case-sensitive identity comparison cannot
self-validate. Follow-up evaluation also tightened "empty" to only null or the
exact empty string: whitespace-only environment/registry values now fail
closed, environment-reader failures have no value echo, and the closed options
test binds empty browser arguments, Stable/MostStable selection, disabled SSO,
disabled extensions, exclusive user data, and disabled custom crash reporting.
This correction changes host policy bytes only; the renderer
9-operation/16-message boundary and all application contracts remain unchanged.

Independent architecture and evaluation re-review of the final corrected bytes
found no remaining must-fix, follow-up, owner/authority, safety/isolation, or
ADR-0017 stack-reopen finding. The reviewers independently confirmed the exact
eight-variable/seven-family matrix, `UserDataFolder` wildcard fallback, only
`DowngradeVersion` remaining non-wildcard, null/exact-empty acceptance,
nonempty-whitespace denial, value-free reader failures, Stable-only option
parity, startup/create/recovery/session guard ordering, refreshed receipt
parity, unchanged renderer authority, and exact zero process survivors. The
corrected Phase D candidate therefore proceeds to one new complete accepted
verification floor; Checkpoint D remains independent and pending.

### Inherited WebView2 correction complete verification-floor receipt

After the final independent re-review passed, the same corrected byte set ran
the complete accepted repository floor:

| Command | Result |
|---|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed; every project was up to date under locked restore. |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed with zero warnings and zero errors; repository-owned `dotnet`/`testhost`/`vstest` survivors after the batch: 0. |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | Passed: 855 tests, 12 expected skips, zero failures; repository-owned `dotnet`/`testhost`/`vstest` survivors after the batch: 0. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed with no formatting drift. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1` | Passed: 154 metadata files, 156 Markdown link sources, and 20 JSON files. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-functional-naming.ps1` | Passed: 195 exact reviewed exceptions and zero unexplained findings. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed with no dependency-manifest drift. |
| `git diff --check` | Passed with no whitespace errors. |

The solution totals were: Contract Tests 266 passed; Desktop Tests 31 passed
and 2 expected live-harness skips; Unit Tests 316 passed and 1 expected skip;
Evaluation Tests 68 passed and 9 expected skips; Integration Tests 142 passed;
Fault Tests 10 passed; and Security Tests 22 passed. The aggregate is 855
passed, 12 expected skips, and zero failures. The separate final desktop
qualification passed 31 direct tests, 1 populated-state preparation test, 1
live hostile/accessibility WebView test, 1 lifecycle/reconnect/reload/shell
test, and six product-host launches with zero repository-launched desktop,
coordinator, or WebView2 survivors. Final repository-owned
`dotnet`/`testhost`/`vstest` survivors were also zero.

This completes the requested WP8 correction implementation, review,
correction, re-review, focused qualification, and complete floor on one Phase D
candidate. It does not accept Checkpoint D, begin Phase E, or activate M2.

## Checkpoint D acceptance and Phase E activation receipt

Date: 2026-08-28

Accepted candidate: `6b9b92a5f3dae0e90219f521919555956a8b5623`

Parent: `ed870882cf6887b05fe91641cb3118b5252ea5d6`

Disposition: the architecture steward accepted Checkpoint D after the Phase D
implementation, testing, correction, independent re-review, desktop
qualification, and complete verification cycles. The accepted floor reported
855 passed tests, 12 expected skips, zero failures, every non-test gate
passing, a clean worktree, and zero repository-owned test-process survivors.
No ADR-0017 stack-reopen trigger remains.

Authority effect: Phase E/WP9 is active for integrated acceptance, same-
candidate correction, measurement and evidence consolidation, authoritative
record closeout, and the M2 planning handoff. It may classify the result only
as an M2-ready contract candidate and must stop at Checkpoint E.

Non-effect: this receipt does not create or accept an M2 plan, activate M2,
change accepted product meaning or architecture, map the five native-only
targeted-verification RPCs to the renderer, authorize private evaluator or
archive access, or claim Milestone-stable or production-ready status.

## Phase E/WP9 integrated acceptance and closeout candidate

Date: 2026-08-28

Starting commit: `6b9b92a5f3dae0e90219f521919555956a8b5623`

Disposition: the complete frontend foundation is an **M2-ready contract
candidate** awaiting Checkpoint E architecture-steward and final owner
acceptance. This is not Milestone-stable, production-ready, an M2 plan, or M2
activation.

### Delivered diagnostic and evidence path

Phase E added a strict, answer-free 16-step manifest and schema at
`frontend-foundation-acceptance.v1.json` and
`frontend-foundation-acceptance.v1.schema.json`. Every step names the real
native-generated or diagnostic-desktop consumer, its authority, current
maturity, applicable EVAL-0090 through EVAL-0094 cases, concrete proof paths,
and fail-closed boundary. A contract test enforces the exact ordered 16 steps,
all five evaluation identities, real evidence files, inactive M2 state, the
accepted Checkpoint D baseline, and `native-only-never-map` policy for all five
targeted-verification RPCs.

`eng/verify-frontend-foundation.ps1` is the repeatable offline acceptance
runner. It builds the exact contract and integration projects without restore,
runs three authority/inventory/matrix tests and eight cross-package native
workflow tests, invokes the accepted desktop qualification, reads the retained
TRX and desktop receipts, writes a sanitized summary under
`artifacts/frontend-foundation-acceptance/`, and proves zero repository-owned
`dotnet`/`testhost`/`vstest` survivors. `eng/qualify-desktop.ps1` now uses
absolute project paths and performs the same exact-root cleanup after each of
its four .NET test batches. No runtime product operation or renderer mapping
was added.

The successful integrated rerun reported 16 of 16 workflow steps passing;
3 contract/authority tests and 8 native integration tests passing with no skips
or failures; 31 ordinary desktop tests, 1 populated-state preparation test,
1 real hostile/accessibility WebView2 test, and 1 lifecycle/recovery test
passing; and zero repository-owned test or desktop qualification survivors.
Restore and all execution remained offline. No network, live or billable
provider, credential, private evaluator, archive, or deferred semantic-oracle
material was accessed.

### Consolidated review, correction, and re-review

The consolidated review covered product/workflow meaning; domain-versus-
presentation authority; immutable analysis and append-only user state;
setup/tool/profile/configuration; budget/provider/credential separation;
progress/events/reconnect/recovery; result/report/evidence/provenance truth;
pagination and resource bounds; renderer/bridge/IPC/local-operation security;
persistence/migration/backup/replay/deletion/export; accessibility and measured
performance; generated fake/real parity; functional naming, dependencies, and
licences; evaluator isolation and claim wording; and the complete diff and
documentation.

One must-fix harness defect was found during the first receipt attempt. Windows
PowerShell 5 did not expose ordered-dictionary keys as properties to
`Measure-Object`, so the product and desktop proof passed but receipt total
aggregation failed. The runner now emits a `PSCustomObject`; PowerShell parsing,
strict JSON, documentation validation, and whitespace checks passed; and the
entire integrated native-plus-desktop workflow was rerun successfully on the
same candidate. No product, contract, architecture, authority, or security
byte changed in that correction.

The first attempted complete floor then found three functional-naming defects:
the diagnostic runner embedded the transition path and next-milestone receipt
labels literally, and the contract guard read the planning state key literally.
The runner now discovers the uniquely named foundation planning root and
composes planning-only milestone labels and keys; the contract guard likewise
composes the frozen planning key. No allowlist entry was added. Functional
naming then passed with the existing 195 reviewed exceptions and zero
unexplained findings, and the complete 16-step native-plus-desktop runner was
rerun successfully again with zero survivors. This correction also changed no
product or architecture byte.

Changed-surface re-review found no remaining must-fix, follow-up,
owner/authority, safety/isolation, or ADR-0017 stack-reopen defect. The proof
uses existing producer-owned state and real generated consumers, keeps the
renderer at nine operations and sixteen message shapes, leaves targeted
verification native-only, preserves exact lineage and bounded pagination,
records local-private export without source mutation, retains zero generic
path/command/URL/provider/credential authority, and makes no M2, production,
semantic-accuracy, or semantic-oracle claim.

Final diff re-review also corrected one maturity-label inconsistency: the exact
native five-RPC targeted-verification step is producer-consumer-validated, while
only a future user-facing M2 interaction remains implementation-active. The
manifest and contract guard now enforce that distinction without changing the
native-only renderer policy or any runtime byte.

### Contract maturity

Producer-consumer-validated surfaces are exact version/fingerprint
negotiation; bounded bootstrap; setup/tool/profile/saved-configuration/estimate;
prepared manual-run admission; progress, event, cancellation, reconnect, and
restart; canonical persistence and readback; generated native clients; the
closed generated renderer client and controlled desktop bridge; the native-only
targeted-verification preparation/start/readback path; and structured export
lifecycle and recovery.

Surfaces remain implementation-active until the real M2 interface consumes
them: complete summary/readiness and scope-limited presentation; supported-case
and lead-only queues; complete finding/case/report/evidence/provenance/focused-
mod presentation; review/disposition/annotation and assumption controls; export
interaction; and any future user-facing targeted-verification interaction. The
five targeted-verification RPCs cannot be renderer-mapped without separately
accepted architecture. Six provider RPCs remain declared-unimplemented.

### Measurements and M2 planning inputs

The fresh reference-machine repeat observed browser-ready launch at 1,194 ms p50
and 1,372 ms maximum; bootstrap at 1,540.3463 ms; finding page at 62.231 ms p50;
detail at 62.1908 ms p50; progress at 62.2446 ms p50; authoritative resync at
62.6954 ms p50; renderer reload at 93.6069 ms p50; idle private working set at
355,483,648 bytes p50; active private working set at 411,131,904 bytes p50; a
6,615,820-byte 31-file package; and zero launched-process survivors. These are
development observations, not production guarantees or service-level
objectives.

The future M2 plan should start from the retained 100-item page, 500-summary
cache, 13-mounted-row virtualization, 1 MiB message, 256 KiB chunk, and 64-item
stream bounds; preserve exact coordinator-owned truth, lineage, privacy, and no
fallback; cover every unavailable/partial/stale/conflict/reconnect state; and
set performance acceptance thresholds only after representative hardware and
workload measurement. It must separately decide polished result/review/export
interaction, any authorized targeted-verification interaction, credential
enrollment, installer/distribution/runtime servicing, and representative
assistive-technology/hardware qualification.

### Complete verification-floor receipt

After the functional-naming correction and its complete integrated rerun, the
same candidate ran one fresh complete accepted repository floor:

| Command | Result |
|---|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed; every project was up to date under locked restore; repository-owned test-process survivors: 0. |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed with zero warnings and zero errors; repository-owned test-process survivors: 0. |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | Passed: 856 tests, 12 expected skips, zero failures; repository-owned `dotnet`/`testhost`/`vstest` survivors: 0. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed with no formatting drift; repository-owned test-process survivors: 0. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1` | Passed: 154 metadata files, 156 Markdown link sources, and 22 JSON files. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-functional-naming.ps1` | Passed: 195 exact reviewed exceptions and zero unexplained findings. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed with no dependency-manifest drift. |
| `git diff --check` | Passed with no whitespace errors. |

The exact solution totals are: Contract Tests 267 passed; Desktop Tests 31
passed and 2 expected live-harness skips; Unit Tests 316 passed and 1 expected
platform skip; Evaluation Tests 68 passed and 9 expected skips; Integration
Tests 142 passed; Fault Tests 10 passed; and Security Tests 22 passed. The
aggregate is 856 passed, 12 expected skips, and zero failures.

The separate final corrected integrated receipt passes 3 contract/authority
tests and 8 native workflow tests. Its desktop qualification passes 31 direct
tests, 1 populated-state preparation test, 1 live hostile/accessibility
WebView2 test, 1 lifecycle/reconnect/reload/shell test, and six fresh host
launches. Every focused batch, every final-floor .NET command, the final
desktop cleanup, and the final repository audit report zero owned survivors.

This completes the WP9 implementation, focused verification, consolidated
review, same-candidate corrections, changed-surface re-review, integrated
native/desktop qualification, measurement repeat, and complete repository
floor. The candidate stops at Checkpoint E and requests architecture-steward
and final owner acceptance. M2 remains inactive.

## Checkpoint E evidence-integrity correction

Date: 2026-08-28

Starting candidate: `aadad64cc5e9e328474cfeb1a7130ea80fe5a254`

Disposition: correction candidate on the same mutable Phase E candidate.
Checkpoint E is not accepted and M2 remains inactive.

The earlier Phase E section records the chronology and diagnostics that led to
the initial candidate. A subsequent acceptance review found that its generated
artifact at `artifacts/frontend-foundation-acceptance/summary.json` was not
bound to the delivered candidate. It recorded
`6b9b92a5f3dae0e90219f521919555956a8b5623`, the Checkpoint D baseline,
because the runner inspected HEAD while Phase E still existed as uncommitted
working-tree bytes. The same artifact wrote `passed` for all 16 workflow steps
from manifest presence alone; descriptive selectors were not tied to selected,
executed, passed tests or evaluated machine evidence. The earlier 856-test
floor remains historical diagnostic evidence, but neither it nor the
self-attested receipt can authorize Checkpoint E.

The correction replaces that model with closed proof types:

- executable proofs retain the exact project, batch, fully qualified test
  identity, selection filter, test/execution identities, outcome, run-specific
  TRX path, and TRX SHA-256;
- desktop qualification proofs bind exact ordinary, live, or lifecycle test
  identities to run-specific TRX plus required fields in the same desktop
  receipt;
- machine evidence names one exact JSON file, JSON Pointer, typed predicate,
  observed value, and source SHA-256;
- architecture and documentation references are explicitly optional,
  non-behavioral references and cannot make a workflow step pass; and
- each workflow-step result is derived only when every required behavioral
  proof verifies, with at least one behavioral proof required per step.

The acceptance runner now requires an externally supplied lowercase 40-byte
commit and tree identity. It rejects dirty state or a commit/tree mismatch
before building or testing, repeats the same check after all commands, and uses
one 128-bit run identity in every retained TRX and the desktop receipt. Desktop
qualification records its candidate commit, tree, run identity, four test-batch
receipts, exact tests, and TRX hashes. The acceptance summary retains every
proof receipt rather than a proof count.

The old `ordinary_effects` booleans were removed. The new receipt distinguishes
declared prohibited scope from enforced controls: .NET execution uses
`--no-restore`, frontend restore uses the accepted offline task, candidate
identity is checked before and after, and no provider or credential test is
selected. These statements do not claim ambient network or process observation
that the harness cannot enforce.

Mutation coverage rejects a nonexistent or misspelled selector, a mismatched
test project, an existing selected test absent from TRX, failed and skipped
required tests, a missing required test receipt, a stale/substituted TRX hash,
a stale desktop candidate receipt, dirty and mismatched candidates, a missing
JSON evidence field, and an
unverified workflow step. The standalone mutation suite passes 14/14, the four
focused foundation authority tests pass, and the eight native integrated
workflow tests pass, all with zero failures or skips. Repository-owned
`dotnet`/`testhost`/`vstest` survivors after every focused .NET batch were zero.

### Consolidated correction review

The consolidated evidence, provenance, security, evaluation, functional-
naming, and complete-diff review found and corrected these must-fix defects on
the same candidate:

- the first proof resolver compared the fully qualified test and TRX but did
  not compare the manifest's declared project with the project that produced
  that batch; exact project binding and a negative mutation now close the seam;
- the first corrected runners overwrote a valid summary on success but could
  leave an older ignored summary visible after a failed rerun; each runner now
  removes only its exact prior summary after clean-candidate preflight, so a
  failed attempt leaves no current-looking receipt;
- the initial machine/reference path patterns were repository-prefixed but
  still admitted dot-only path segments; the schema now permits only closed
  ordinary repository-relative segments and the runtime independently checks
  machine-evidence containment; and
- changed-surface review found one stale internal catalog reference after test
  discovery was simplified. The resolver now uses the manifest identity only
  as the requested selection and requires the same exact identity to appear
  once with `Passed` outcome in the retained TRX.

Re-review confirms that every behavioral proof binds an executed project,
selection, result, receipt/hash, and candidate run; reference material cannot
contribute pass authority; all 16 steps require verified behavioral evidence;
desktop receipt substitution, stale artifacts, hostile paths, missing fields,
and dirty/mismatched candidates fail closed. The correction changes no domain
or presentation authority, immutable analysis or append-only user state,
provider/credential behavior, persistence contract, migration, renderer/IPC
surface, generated contract, dependency, licence, accessibility claim, or
measurement meaning. EVAL-0090 through EVAL-0094 remain the exact trace set,
private/evaluator isolation is unchanged, and measurements remain planning
observations rather than production guarantees. Mutation 14/14, foundation
authority 4/4, documentation, functional naming, and `git diff --check` pass
after the corrections with zero repository-owned test-process survivors.

The complete correction must be committed before its integrated evidence can
be produced. The ignored post-commit acceptance artifact and its exact final
commit/tree binding are therefore reported in the orchestrator handoff rather
than pre-attested here. Checkpoint E review may resume only if that clean
committed run, changed-surface re-review, desktop qualification, and complete
repository floor pass. This correction changes no product RPC, renderer
operation, persistence contract, architecture boundary, product meaning, or
M2 state.
