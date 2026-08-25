# M1-to-M2 Foundation — Frontend Application Foundation implementation record

Status: Accepted
Disposition: Checkpoint A reached; WP1 and WP2 accepted, Phase B not started

Last reviewed: 2026-08-25
Owner: Project owner
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Planning base: `32dbb2c48754666336d2da571e554ad8897ed71c`

## Plain-language state

Phase A has verified the real application surface, assigned every foundation
capability to an exact owner, and implemented the common boundary future
frontend work can build on. WP1 established the answer-free authority inventory.
WP2 added a bounded display-safe bootstrap and closed renderer contract source
without adding a product UI, provider access, generic native authority, or any
Phase B workflow.

## Package status

| Phase | Work package | Status | Accepted candidate/evidence |
|---|---|---|---|
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1` | Accepted on phase candidate | Receipt below; final phase commit deferred until Checkpoint A |
| A | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP2` | Accepted on phase candidate | Receipt below; final phase commit at Checkpoint A |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3` | Not started | None |
| B | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4` | Not started | None |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5` | Not started | None |
| C | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6` | Not started | None |
| D | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP7` | Not started | None |
| D | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP8` | Not started | None |
| E | `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP9` | Not started | None |

## Phase checkpoint receipts

### Checkpoint A receipt

Candidate: the commit containing this record on branch
`codex/frontend-application-foundation-phase-a`, based on
`d363347339c83d76787de71f6ef1cf4d30288040`. The exact immutable commit is
reported by the orchestrator after commit creation; the intended post-commit
working tree is clean.

Completed packages: WP1 and WP2 only. Phase B, M2 activation, desktop UI,
provider effects, private evaluation, merge, push, and publication are absent.

Complete accepted verification floor:

| Command | Passed | Failed | Skipped/result |
|---|---:|---:|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | — | 0 | Dependency graph current |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | — | 0 | 0 warnings, 0 errors |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Unit"` | 247 | 0 | 1 expected skip |
| same command, `TestCategory=Contract` | 159 | 0 | 0 skipped |
| same command, `TestCategory=Integration` | 120 | 0 | 0 skipped |
| same command, `TestCategory=Evaluation` | 89 | 0 | 9 expected skips |
| same command, `TestCategory=Security` | 140 | 0 | 3 expected skips |
| same command, `TestCategory=Fault` | 117 | 0 | 3 expected skips |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | 681 | 0 | 10 expected skips |
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
  caught it; the bound fingerprint is
  `b438e15250bf23f8b7f290ccf46292b5dcfd249ac15284881f7ef6251844bf48`.
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
  `b438e15250bf23f8b7f290ccf46292b5dcfd249ac15284881f7ef6251844bf48`;
- common typed application errors, optimistic revision/conflict tokens,
  user-operation receipts, and transport-cancellation receipts;
- closed renderer request/response/event envelope, exact three-operation
  registry, deterministic registry digest
  `c302d6cc6728cdab1d65a618313b357d828af01a41069156bc1bfca52433d9ac`,
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

## Final closeout fields

WP9 will replace this placeholder with the accepted contract maturity,
diagnostic-consumer evidence, complete verification receipt, resource
measurements, remaining limitations, M2 planning inputs, and exact final
candidate.
