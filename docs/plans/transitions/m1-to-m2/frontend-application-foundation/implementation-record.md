# M1-to-M2 Foundation — Frontend Application Foundation implementation record

Status: Accepted
Disposition: Checkpoint B reached; WP1-WP4 accepted, Phase C not started

Last reviewed: 2026-08-25
Owner: Project owner
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Planning base: `32dbb2c48754666336d2da571e554ad8897ed71c`

## Plain-language state

Phases A and B have verified the real application surface, assigned every
foundation capability to an exact owner, and implemented the common and
setup-to-live-run boundaries future frontend work can build on. WP3 adds typed
tool/profile/configuration setup, honest estimates, and non-secret provider
status. WP4 adds prepared manual-run initiation with immutable durable bindings,
receipts, progress, reconnect, and restart behavior. No product UI, Phase C
workflow, provider effect, or generic native authority was added.

## Package status

| Phase | Work package | Status | Accepted candidate/evidence |
|---|---|---|---|
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1` | Accepted on phase candidate | Receipt below; final phase commit deferred until Checkpoint A |
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP2` | Accepted after correction | Corrected receipt and complete floor below |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3` | Accepted after correction | Receipt below; final phase commit deferred until Checkpoint B |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4` | Accepted after correction | Receipt below; complete floor and Checkpoint B receipt below |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5` | Not started | None |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6` | Not started | None |
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

## WP3 receipt — Setup, profile, configuration, estimate, and enrollment status

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

## WP4 receipt — Prepared manual run, lifecycle, live state, and reconnect

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

## Checkpoint B receipt — Phase B accepted candidate

Candidate: the single review-ready Phase B candidate based directly on accepted
Phase A commit `59b6de3d80443c0150c15c8d83b5b29d1b3536ef`. The exact immutable
Checkpoint B commit is reported by the orchestrator after commit creation.

Disposition: `ACCEPT` for WP3 and WP4. Checkpoint B is reached. Phase C is
unblocked by the accepted plan but was not started; M2 remains inactive.

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

## Final closeout fields

WP9 will replace this placeholder with the accepted contract maturity,
diagnostic-consumer evidence, complete verification receipt, resource
measurements, remaining limitations, M2 planning inputs, and exact final
candidate.
