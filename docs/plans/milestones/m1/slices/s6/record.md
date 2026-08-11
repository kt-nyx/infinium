# M1 Slice 6 implementation record

Status: Deferred

Disposition: Append-only implementation evidence

Last reviewed: 2026-08-10

## M1/S6/WP1 entry — 2026-08-10

### Authority and package gate

- Active package: `M1/S6/WP1` only.
- Accepted authority commit `d43904c6d145fd94dc5340db282e300cdc8dd640`
  is an ancestor of the handoff.
- Exact handoff and input commit:
  `6ac66e7d79c63a231bbbf22209015a894cd4bd6d`.
- Accepted Slice 5 implementation candidate
  `5514919b8f742d00e59752fa7125da487a390926` is an ancestor of the handoff.
- `docs/current-state.md` was inspected at the handoff and authorizes only
  contracts, codecs, migration, finite-bound policy, answer-free examples,
  public-authority updates, focused verification, review, correction, and
  re-review for WP1.
- The accepted Slice 6 plan, its compact entry, RESEARCH-0054, orchestrator
  handoff, prerequisite Slice 5 handoff/claim boundary, accepted ADRs, M1
  continuation profile, evaluation rules, package-relevant specifications,
  and closed-world public-fixture authority were read before this record was
  created.

### Git preflight

- Branch: `codex/m1-s6`.
- HEAD: `6ac66e7d79c63a231bbbf22209015a894cd4bd6d`.
- Worktree: clean before this record was created.
- The branch has no configured upstream and
  `refs/remotes/origin/codex/m1-s6` does not exist locally.
- Local `refs/remotes/origin/main`:
  `d88ba5a5806944f4ec5e919f754dffadc00ebc5f`.
- Merge base of HEAD and local `origin/main`:
  `d88ba5a5806944f4ec5e919f754dffadc00ebc5f`.

### Allowed and prohibited boundary

- Allowed: the complete WP1 vertical surface under Section 12 of the accepted
  plan, including product contracts/codecs/invariants, schema-6 migration and
  storage declarations, non-secret application/query shapes, helper protocol
  v2 identity and fingerprint, additive v2 configuration/publication/output
  references, answer-free examples, closed-world public-fixture authority,
  focused tests, documentation, and the WP1 verifier gates.
- Prohibited: Credential Manager/native credential calls, a runtime helper,
  public DNS/network/provider requests, provider SDKs, semantic prompt or
  answer fixtures, live packages, private fixtures, sibling private access,
  legacy/evaluator archives, later packages, and any reinterpretation of
  Slice 5 frozen v1 contracts.
- Ordinary/default gates for this package are required to remain credential-
  and network-free.

### Initial claim boundary

- This package begins with public, answer-free contract evidence only.
- No private material, held-out verdict, legacy archive, evaluator archive,
  Credential Manager operation, provider request, or other external effect was
  used in preflight.
- New Slice 6 contracts remain `Implementation-active` pending package
  acceptance. Slice 5 contracts remain `Slice-frozen`.

## M1/S6/WP1 candidate closeout — 2026-08-10

### Implemented vertical surface

- Added all nine closed product schemas, matching domain records, strict
  canonical codecs, invalid-state invariants, the exact stateless/cache-off
  provider-active configuration factory, and additive run-output/CLI-summary
  v2 supplements that fingerprint rather than reinterpret frozen v1 bytes.
- Added non-secret enrollment, selection/confirmation, status, operation,
  budget, settlement, and offline replay command/query/projection shapes.
- Added helper-private protocol v2 with closed assignment, endpoint,
  revalidation, and receipt enums, finite limits, cache-separated usage, a new
  generated-contract-set fingerprint, and retained independent helper v1
  generation/decodability.
- Added migration `M1-S6-0006` from exact schema 5/storage `1.4.0` to schema
  6/storage `1.5.0`, including 22 append-only provider/evidence history tables,
  three rebuildable projections, active-generation and one-live-attempt
  projections, unique request/fingerprint and scope indexes, exact origin
  validation, migration provenance, restore validation, deletion-history,
  backup inclusion/exclusion, and projection-source declarations.
- Added the conservative local input proof: one token per canonical UTF-8 byte
  plus a fixed 4,096-token structured-envelope margin. The qualification bound
  is exactly `16,384 + 4,096 = 20,480`; the semantic maximum is
  `65,536 + 4,096 = 69,632`, below the admitted 73,728-token ceiling. No
  tokenizer, provider preflight, DNS, or network operation participates.
- Added the bounded, component-wise upward-rounded, checked signed-64
  nano-USD price-rule shape across provider/model/tier/context/cache/token/tool/
  region/currency/revision dimensions.
- Added one public answer-free package containing exactly one synthetic shape
  example for every WP1 schema; advanced the closed-world registry and schema
  from 18/`1.0.0` to 19/`1.1.0`; and updated the resealer, bounded reader,
  discovery, count, authority-boundary, schema-inventory, and protocol-
  inventory tests together.
- Added `wp1-contract-traceability.v1.json`. Its executable contract test
  recursively compares every schema-declared field path, including local
  `$defs`, with the inventory and requires authority, producer, consumer,
  persistence, output, and replay mappings for each contract.

### Frozen compatibility and structural absence

- `git diff --quiet 6ac66e7d79c63a231bbbf22209015a894cd4bd6d --` over the
  frozen effective-configuration v1, run-output v1, CLI-summary v1, and their
  output codecs passed in both verifier gates.
- The nine product schemas, domain/application shapes, persistence columns,
  public examples, and ordinary outputs contain no credential target, secret
  bytes, bearer/authorization header, raw headers, reveal operation, arbitrary
  URL, or caller-selected host. Reserved protobuf names document fields that
  must continue to be rejected and are not active fields.
- Public example validation rejects oracle/expected-answer/expected-label and
  secret-bearing fields, covers exactly the nine named schemas, and cannot be
  discovered except through the exact current registry identity/path.
- No private fixture, sibling private repository, legacy/evaluator archive,
  Credential Manager/native credential API, provider SDK, public network/DNS,
  semantic prompt fixture, helper runtime, live request, or later package was
  accessed or implemented.

### Exact retained identities

- Schema-5 origin fingerprint:
  `e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d`.
- Schema-6 final fingerprint:
  `c820c0935dc4e5ff4c68dd70b40be6a6e232661357db89e2cb4b454850382124`.
- Canonical full protobuf contract-set fingerprint, including helper v2:
  `80bb28272b9d514b6f0819d0f7532a3c9704fc3f4d543cdb803f88798fe4534c`.
- Product-schema SHA-256 identities, in the plan's order:
  `1fa748283ca95d0107a1631b58fdedf0c8ab43c4fbc980daba0f1a24c8072a9f`,
  `77a302ff97d9616cf054981b7ecfc9cc6e5a1c1f1280c5b154ee75d6b2bfc31b`,
  `c87d860e6d3498ca8b7f0874fb6d85d9251deb66a851028f614bb2e6662cb623`,
  `2917f5e8709d7604068dcd80f1d721cd5742aaa3a04d5d60ea4bf5295978e38a`,
  `b1f8aadf8a828aed783bf34f93de098177cbe75a6ed09d313e81e587ccbfb27a`,
  `f1223caf2b620c1a4bf1ad8b71a1411998554a1d9bf2a2076c2f52910fa9aad7`,
  `547e7367b61f6a1c9cb7b45a2780fe618084ccd8df965cb3783d53f55f050f93`,
  `7afe0979d84f428e5f41e9144d606f7ec3597b1c6341765e713ca288673411b9`,
  and `26cd1ed73c9908d2ec385bf28176e086c7b8908286ab5800682905f17f656bcf`.
- Answer-free example authority: 10,089 bytes,
  SHA-256 `e895bf72f9daf8da091ff3bf74f9ee41017f6d4d350a5586366026d3efbec45d`.
- Public registry: 8,815 bytes, 19 packages,
  SHA-256 `8283b51d878d7a8f0907381baf5d6509c1477c13e475064c31471fa0d0e9414e`.
- Field traceability inventory: 11,270 bytes, nine contracts,
  SHA-256 `1d4448b7fe6b4a161a5b79bbcb0181bb5eaa0fbcdd91691ca31ffd1bd600671c`.

### Final commands and observed counts

1. `dotnet restore Infinium.sln --locked-mode` — all projects up to date.
2. `dotnet build Infinium.sln -c Release --no-restore` — succeeded with zero
   warnings and zero errors.
3. `dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderContract|FullyQualifiedName~ProviderFiniteBound|FullyQualifiedName~OperationalContract"`
   — 12 passed, 0 failed, 0 skipped.
4. `dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Provider|FullyQualifiedName~Helper|FullyQualifiedName~RunOutput"`
   — 14 passed, 0 failed, 0 skipped.
5. `dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Schema6|FullyQualifiedName~ProviderPersistence|FullyQualifiedName~BackupRestore"`
   — 3 passed, 0 failed, 0 skipped.
6. `powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate Contracts -OutputRoot artifacts/m1-slice6/wp1`
   — passed; nested contract filter 14/0/0; receipt declares network and
   credential access false.
7. `powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate StateTotality -OutputRoot artifacts/m1-slice6/wp1`
   — passed; nested unit filters 12/0/0 and 3/0/0; receipt declares network and
   credential access false.
8. `dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo`
   — 162 passed, 0 failed, 1 environment-dependent symlink test skipped.
9. `dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo`
   — 147 passed, 0 failed, 0 skipped.
10. `dotnet test Infinium.sln -c Release --no-build --nologo` — Unit 162/0/1,
    Contract 147/0/0, Integration 68/0/0, Evaluation 53/0/8. The eight
    evaluation skips are the existing private/runtime-dependent cases; no
    private material was accessed.
11. `git diff --check` — passed. Frozen Slice 5 v1 diff check, recursive
    field-traceability equality, forbidden-field scan, canonical round trips,
    exact schema-5-to-6 migration, backup/restore, projection declarations,
    registry closure, answer isolation, and protocol fingerprint checks all
    passed through the commands above.

### Review findings and disposition

- Must-fix, corrected: the first finite-bound margin exceeded qualification
  admission; replaced with the proved fixed 4,096-token margin and added both
  boundary counterexamples.
- Must-fix, corrected: initial lifecycle state columns implied mutation on an
  append-only table; moved current profile/operation state to three explicitly
  rebuildable projections and retained immutable identity/intent/event rows.
- Must-fix, corrected: helper v2 initially used open strings for endpoint and
  outcome states; replaced them with fail-closed enums and resealed the full
  generated-contract-set fingerprint.
- Must-fix, corrected: ordinary full regression found stale schema-5 and
  schema/proto inventory assertions; advanced them and added a direct exact
  schema-5-to-6 migration proof.
- Environment cleanup, corrected: a stale ignored retired-path `obj` tree made
  the closed-root tests fail. Only the verified generated build artifacts and
  their empty parents were removed; no tracked or user-authored file was
  removed.
- Routine verifier defects, corrected: the first PowerShell gate draft used
  unsupported Windows PowerShell syntax/APIs; the final exact Windows
  PowerShell invocations pass and their receipts were inspected.
- Non-blocking: one symlink-security unit test and eight private/runtime exact-
  environment evaluation tests remain intentionally skipped. They are not WP1
  gaps and no authority expansion was used to run them.
- No owner/authority decision, safety/isolation breach, or unresolved WP1
  must-fix finding remains. Candidate acceptance and advancement to WP2 remain
  orchestrator/independent-review actions; this record does not change
  `docs/current-state.md`.

## M1/S6/WP1 correction-pass entry — 2026-08-10

### Rejection and corrected authority boundary

- Independent contract and repository-boundary review rejected candidate
  `e9ed1bcaf9cc50c5cf07376009a8371e41247b5e`. The preceding closure statement
  is retained as historical evidence, but is superseded: unresolved WP1
  must-fix findings did remain at that candidate.
- The correction pass began on branch `codex/m1-s6` at that exact rejected
  commit with a clean worktree. `docs/current-state.md` still authorizes WP1
  only; no WP2 or later-package authority was inferred.
- Both final reviewer reports and the accepted Slice 6 Section 12 authority
  were re-opened before product edits. The pass is bounded to the rejected
  contract, codec, migration, helper/application wire, traceability, fixture,
  verification, and documentation seams.
- The earlier claim that a fixed 4,096-token framing margin was proved is
  withdrawn. Repository-local accepted authority and dependencies are being
  checked for an exact tokenizer or a genuinely derivable framing-inclusive
  grammar. No network, provider preflight, private fixture, credential store,
  legacy archive, or evaluator archive will be used to manufacture that proof.
  If the local evidence cannot support it, only that finite-input-bound path
  will be reported as the plan-defined owner/authority escalation.

### Correction implementation and evidence

- Operation documents now carry an explicit operation kind, nullable
  state-reachable identities, conditional JSON branches, availability-aware
  quantities, and typed reachable capability and price snapshot contents.
  Exhaustive evidence validates every non-unspecified operation state and all
  ordered state pairs against the closed transition graph, plus adversarial
  semantic proposal and public-output cross-state substitutions.
- Transport qualification and both semantic operation kinds are bound across
  domain, JSON, helper, persistence, and tests to their exact accepted
  seven-dimensional ceilings. This does not cure the separate framing-inclusive
  input-token proof gap.
- Schema 6 now persists the exact installation, analysis-context, effective-
  configuration, resolved-manifest, prompt, output-schema, request/settings,
  profile/generation, capability, price, and seven-dimensional limit bindings.
  Capability fields and price rules are typed relational data. Composite
  foreign keys reject cross-profile generation and cross-operation attempt,
  request, reservation, fence, response, usage, settlement, replay, and
  projection substitutions. A partial unique index on the constant
  `live_billable_slot=1` enforces one global live billable attempt; distinct
  live attempts are tested adversarially. Backup/restore retains exact replay
  field values.
- The application protobuf contract advanced additively to 1.2.0 with bounded
  non-secret provider profile, operation, budget, replay, and command shapes.
  CLI and run-output supplements distinguish not-used, unavailable, pending,
  live, completed, failed, and unresolved states without fabricated usage or
  downstream identities. Slice 5 v1 byte surfaces remain unchanged.
- Helper v2 final revalidation now binds provider account, billing scope,
  effective configuration, capability snapshot, price snapshot, settings,
  output schema, operation kind, request, reservation, generation, revocation,
  attempt, dispatch, and fence epoch. Its independent transitive fingerprint is
  `a7f338ec8c8f4a60cd6314ae84f2f0442ed7a29750d16ac7b7b8339b6a8c1af2`;
  the separately versioned full application contract-set fingerprint is
  `cac374c5d50a12701789bf9ea8ee62cd0a0096167de7d31b5fc5fb8cab5cba6d`.
  Independent v1/v2 frame parsing and fail-closed top-level/nested unknown,
  numeric enum, limit, assignment, final-revalidation, and outcome evidence is
  executable.
- Traceability now uses accepted requirement and ADR identities only, includes
  ADR-0020, explicitly assigns every declared/reachable field to a semantic
  seam, and validates field coverage, accepted IDs, source/output paths, and
  exact migration tables. RESEARCH-0054 is treated as evidence rather than
  direct product authority.
- Corrected schema-6 fingerprint:
  `21a46b9a08db42504a0f3edcb523fb94118e3753385f894008d0671282b33d4a`.
  Corrected answer-free example authority: 12,914 bytes, SHA-256
  `aee234a164321203abcb7623b193b6f268df420102801912e01b754fdf7580eb`.
  Corrected 19-package public registry: 8,815 bytes, SHA-256
  `f1ee0275a1b01f162871645f9c1f6e8f787311e1dc9cc3f13bb2d8bc2e1af069`.
  Corrected traceability inventory: 20,142 bytes, SHA-256
  `7d5a62d8a2606c5cf6124ca4dc2c4ff8c6467ba0eec4b8e91161e1a1ed7dda9c`.

### Correction verification

1. `dotnet restore Infinium.sln --locked-mode` remained the locked restore
   basis from the WP1 candidate; no dependency was added.
2. `dotnet build Infinium.sln -c Release --no-restore` — passed with zero
   warnings and zero errors.
3. The combined focused unit filters for provider contracts, finite limits,
   operational contracts, schema 6, provider persistence, and backup/restore —
   18 passed, 0 failed, 0 skipped.
4. The focused provider/helper/run-output contract filter — 16 passed, 0
   failed, 0 skipped.
5. `eng/verify-m1-slice6.ps1 -Gate Contracts` — passed, including 16 focused
   contract tests, independent helper/application fingerprints, forbidden-field
   scan, registry closure, and frozen Slice 5 v1 diff.
6. `eng/verify-m1-slice6.ps1 -Gate StateTotality` — passed, including 14
   domain/transition tests and 4 migration/persistence/restore tests. Its
   receipt declares local input-bound proof `authority-required` and provider
   dispatch admission `fail-closed`; it makes no framing-margin claim.
7. `dotnet test Infinium.sln -c Release --no-build --nologo` — Unit 165/0/1,
   Contract 149/0/0, Integration 68/0/0, Evaluation 53/0/8. The skips are the
   existing environment/private-runtime-dependent cases; no private material
   was accessed.
8. `dotnet format Infinium.sln --verify-no-changes --no-restore`,
   `eng/validate-documentation.ps1`, and
   `eng/update-dependency-manifest.ps1 -Check` — passed after the mechanical
   dependency-manifest reseal.
9. `git diff --check`, frozen Slice 5 v1/current-state diff, rejected-margin and
   invented-ID scan, forbidden secret-field scan, semantic/diff review, and
   repository-boundary review — passed. No current-state, later-WP, private,
   archive, credential, provider, DNS, or public-network boundary was touched.

### Remaining authority escalation

- Repository-local accepted authority and locked dependencies contain no exact
  provider tokenizer and no complete provider-framing grammar from which the
  required framing-inclusive input-token bound can be proved. Inventing another
  margin would repeat the rejected defect. The product therefore exposes no
  successful local input-bound proof and fails provider dispatch admission
  closed at that boundary.
- This one path requires the plan-defined owner/authority decision: supply and
  accept an exact local tokenizer/framing policy or revise the accepted bound
  contract. All independent WP1 corrections above are complete and coherent;
  this record remains `Deferred` and does not advance `docs/current-state.md`.

## M1/S6/WP1 final correction reseal — 2026-08-10

### Superseding evidence

- Final semantic re-review strengthened the executable state-totality proof to
  enumerate every supported operation state, every ordered transition pair,
  every bounded transport/receipt/settlement/replay combination, and every
  state-reachable identity inversion. It also exercises zero and one-above
  boundaries for every qualification-limit dimension and one-above boundaries
  for both semantic operation kinds.
- Final relational review added distinct adversarial inserts for cross-graph
  request, reservation, dispatch fence, response, usage, settlement, replay,
  and operation-projection ownership, in addition to the cross-profile
  generation and two-distinct-live-attempt cases. Invalid typed capability
  and price snapshot contents and price-rule dimensions are rejected by the
  persisted schema, not merely by application validation.
- Restore evidence now compares every retained replay binding individually:
  profile/generation/revocation, operation kind, installation/context/effective
  configuration/resolved manifest, prompt and output-schema identities and
  fingerprints, canonical request, capability and price snapshot identities,
  settings, and all seven authorization limits.
- Those persisted snapshot checks legitimately resealed the schema-6
  fingerprint to
  `9cc35e3709a9a7fb4bdc0470e4ee488441648cb9b43055d0319f22af878464f4`.
  The earlier `21a46b9a...` value in this append-only record is superseded.
  The final traceability/example/registry bytes and hashes remain exactly the
  values recorded in the correction section above.

### Final rerun evidence

1. `dotnet restore Infinium.sln --locked-mode` — all projects up to date.
2. `dotnet format Infinium.sln --no-restore`, followed by
   `dotnet format Infinium.sln --verify-no-changes --no-restore` — passed.
3. `dotnet build Infinium.sln -c Release --no-restore` — passed with zero
   warnings and zero errors.
4. `eng/verify-m1-slice6.ps1 -Gate Contracts` — passed: 17/0/0 focused
   contract tests, nine schemas, nine answer-free examples, 19 public fixture
   packages, independent helper/application fingerprints, frozen Slice 5 v1
   bytes, and forbidden-field closure.
5. `eng/verify-m1-slice6.ps1 -Gate StateTotality` — passed: 15/0/0 exhaustive
   domain/transition/limit tests and 4/0/0 migration, relational ownership,
   singleton-live-attempt, backup, and restore tests. Its receipt retains the
   exact fail-closed finite-bound escalation and grants neither network nor
   credential access.
6. `dotnet test Infinium.sln -c Release --no-build --nologo` — Unit 166/0/1,
   Contract 150/0/0, Integration 68/0/0, Evaluation 53/0/8. The unchanged
   skips remain environment/private-runtime dependent; no private material was
   accessed.
7. `eng/validate-documentation.ps1`,
   `eng/update-dependency-manifest.ps1 -Check`, `git diff --check`, strict
   secret/forbidden-ID/rejected-margin scans, frozen Slice 5/current-state
   diff, and changed-path review — passed.

### Final finding disposition

- Routine correction: the first run after persisted snapshot hardening failed
  only its now-stale schema fingerprint assertion; declaration, test, gate,
  and this append-only evidence were resealed to the observed fingerprint and
  all checks reran green.
- Owner/authority escalation remains limited to the framing-inclusive local
  input-token proof described above. No other must-fix, follow-up, safety,
  isolation, or owner-decision finding remains in WP1.
