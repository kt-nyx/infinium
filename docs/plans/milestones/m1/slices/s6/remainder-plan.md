# M1 Slice 6 remainder plan

Status: Proposed

Disposition: Accepted-ready; no execution authority until project-owner
acceptance of the exact planning candidate

Owner: Project owner

Planning lead: Slice 6 planning lead

Prepared: 2026-08-16

Last reviewed: 2026-08-16

Starting branch: `codex/m1-s6`

Starting commit: `313ecfc04a22330c4c5dc52a79aae87d13982a74`

Plan identity: `infinium.m1-s6.remainder-plan/1.0.0`

Plan ID: `3d11108f-a57f-4dd8-81ac-00916a498e00`

## 1. Purpose and authority status

This plan closes the remaining Milestone 1 Slice 6 path from the accepted
WP1-WP8 state. It corrects the public semantic-validation authority conflict
recorded in
`docs/research/investigations/RESEARCH-0056-slice6-live-semantic-authority-conflict.md`,
incorporates the settlement/recovery correction already present at the starting
commit, and defines one finite WP9-WP11 campaign.

This is a proposed accepted-plan amendment plus a proposed public-fixture
authority amendment and proposed finite-campaign successor. It is not an ADR:
the accepted architecture already decides host admission, durable evidence,
stateless/cache-off provider use, persistence, credential ownership, and finite
effect accounting. No new architecture choice is required.

Until the project owner accepts the exact planning candidate:

- this document and the companion authority amendment are not implementation
  or effect authority;
- the v2 packages are designs, not frozen validation inputs;
- no credential, network, native helper, provider, or billable action is
  authorized; and
- the accepted Slice 6 plan and current-state handoff remain current.

Acceptance authorizes R1-R3 immediately and grants dormant, conditional R4-R7
authority under the one successor campaign. Each effect becomes exercisable
only after its exact committed artifacts, predecessor evidence, expiry, and
stage conditions pass. No second routine owner decision is required between
WP9, WP10, and WP11. Acceptance never supplies a secret or waives a gate.

## 2. Verified planning baseline

The planning lead verified before drafting:

- branch `codex/m1-s6`;
- clean starting worktree;
- exact HEAD `313ecfc04a22330c4c5dc52a79aae87d13982a74`;
- that HEAD descends from the accepted Slice 6 history; and
- the current-state statement that WP1-WP8 are accepted and no effect in the
  current finite campaign has occurred.

The starting commit is the immutable implementation and verification floor for
the next candidate. Its settlement/recovery correction is retained wholesale;
R2 integrates the new semantic path with it and may correct it only when
ordinary verification exposes a real defect. No candidate may be based on an
earlier commit or recreate the fix selectively.

The planning conflict is authoritative and narrow: frozen
`LLM-CLAIM-LIVE-VAL/1.0.0` admits no proposal, while WP11 requires the exact
persisted WP10 admission. Product implementation cannot make both facts true.

## 3. Accepted inputs and companion amendments

This plan is governed by the repository entry documents, the accepted M1 and
Slice 6 plans, the continuation verification profile, and the accepted product
requirements and ADRs linked from those plans. Its task-specific additions are:

- `docs/research/investigations/RESEARCH-0056-slice6-live-semantic-authority-conflict.md`;
- `docs/evaluation/specifications/m1-slice6-live-semantic-v2-amendment.md`; and
- `docs/plans/milestones/m1/slices/s6/m1-slice6-remainder-authority-amendment.v1.json`.

If those three artifacts, this plan, or their exact hashes disagree, execution
stops as an authority conflict. Historical plans, private fixtures, archived
evaluators, and retired protocols are not inputs.

## 4. Clean-break identities and preservation rules

### 4.1 Frozen current-package preservation

R1 must prove that every tracked byte and identity in these frozen current
package
trees remains unchanged from the planning base:

- `S6-CLAIM-VAL-v1/1.0.0`;
- `LLM-CLAIM-LIVE-VAL/1.0.0`;
- `S6-CANDIDATE-VAL-v3/2.0.0`;
- `LLM-INVESTIGATE-LIVE-VAL/1.0.0`; and
- `PROV-LIVE-COMPOSED-VAL/1.0.0`.

They remain frozen historical evidence. They are not rewritten, aliased,
reinterpreted, migrated in place, or accepted as WP10/WP11 campaign inputs.
Registry successor work preserves each of the existing 38 entry objects
exactly, including identity, version, partition, path, authority length,
SHA-256, and any status. Only registry headers/count/version and five appended
v2 entries may differ. Replacement-campaign eligibility is denied by exact
campaign bindings, never by rewriting historical registry rows.

### 4.2 New public package family

R1 owns exactly five new identities:

1. `S6-CLAIM-LIVE-VAL-v2/2.0.0` — answer-free WP10 source material;
2. `LLM-CLAIM-LIVE-VAL-v2/2.0.0` — WP10 wrapper and independently authored
   oracle;
3. `S6-CANDIDATE-LIVE-VAL-v2/2.0.0` — answer-free WP11 positive and matched
   negative contexts;
4. `LLM-INVESTIGATE-LIVE-VAL-v2/2.0.0` — WP11 wrapper and independently
   authored oracle; and
5. `PROV-LIVE-COMPOSED-VAL-v2/2.0.0` — cross-stage provenance wrapper and
   independently authored oracle.

The public registry successor is
`fixtures/public/public-fixture-registry.v2.json`, identity/version
`infinium.repository.public-fixture-registry/1.7.0`, with 43 entries: the
current 38 entries plus these five. Its exact schema is
`contracts/repository/public-fixture-registry.v2.schema.json`; the v1 registry
and schema remain unchanged. Package identity, wrapper identity, schema
version, registry version, campaign version, and product contract version are
separate axes and may not be substituted.

Repository campaign contracts make one coordinated clean break:

- `infinium.repository.m1-slice6-campaign-stage-request/2.0.0`;
- `infinium.m1-s6.campaign-stage-evidence/v2`;
- `infinium.m1-s6.campaign-composed-evidence/v2`; and
- `infinium.repository.m1-slice6-finite-campaign-authorization/2.0.0`;
- `infinium.repository.wp9-production-profile-authorization/2.0.0`.

The last identity is defined by
`contracts/repository/wp9-production-profile-authorization.v2.schema.json` and
validates only successor manifest
`docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json`.
The existing v1 manifest and hard-coded v1 schema remain unchanged and have no
successor-campaign authority.

The product contracts `infinium.llm.source-claim-extraction/v1` and
`infinium.llm.candidate-investigation/v1`, SQLite schema 6, and storage contract
1.5.0 stay
unchanged unless R2 finds a genuine shape or invariant defect. Such a defect is
not permission for compatibility code: it requires a clean-break revision of
all affected producers, consumers, persistence, replay, invalid-state tests,
and docs within this slice, or escalation if accepted authority is incomplete.

### 4.3 Semantic design

The WP10 input independently covers supported/applicable,
negative/unsupported, conditional, version-scoped, contradictory,
hostile-authority, deleted, and insufficient/abstention states. The frozen
oracle permits exactly one admitted proposal: the genuinely supported and
applicable case. Every other proposal is rejected, suppressed, or absent for a
case-owned reason. Product output must never author or revise expected truth.

WP11 contains exactly one positive and one materially matched negative. The
positive must bind the exact persisted WP10 acquisition, proposal, host
admission, claim artifact, and application link. The negative must remain
answerable from independently frozen public host evidence and must not create
a second WP10 claim merely to simplify the join. Composed evidence must prove
the full identity and digest chain without a fourth provider request.

The companion fixture-authority amendment owns detailed state semantics,
answer isolation, authorship order, and freeze checks. Any ambiguity is a stop
for the affected R1 path, not permission to infer truth from product output.

## 5. Finite successor campaign

The successor campaign identity is
`infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66`.
Its credential-effect authorization
expires at `2026-08-31T23:00:00.0000000Z`; all other campaign authority expires
at `2026-08-31T23:59:00.0000000Z`. Expiry is checked immediately before every
effect and cannot be extended by resumption, evidence creation, or owner
acceptance after the fact.

The campaign preserves the accepted exact Responses profile: provider OpenAI,
endpoint `https://api.openai.com/v1/responses`, model `gpt-5.6-sol`, service tier
`default`, reasoning effort `medium`, reasoning context `current_turn`, reasoning
mode `standard`, strict structured output, `store: false`, `background: false`,
`stream: false`, `tool_choice: none`, empty `tools`, truncation `disabled`, and
prompt-cache mode `explicit` with null key and breakpoint. Conversation,
previous response, prompt template, file/image/audio input, hosted search,
Batch, parallel tool use, metadata map, caller-selected host/model/tier, and
arbitrary provider options are absent. R3 binds its exact canonical
fingerprint. It authorizes at most:

| Stage | Starts | Request bytes | Input tokens | Output tokens | Raw response bytes | Deadline | Maximum cost |
|---|---:|---:|---:|---:|---:|---:|---:|
| WP9 qualification | 1 | 16,384 | 20,480 | 256 | 262,144 | 60 s | USD 0.14 |
| WP10 source claim | 1 | 65,536 | 73,728 | 4,096 | 1,048,576 | 120 s | USD 0.60 |
| WP11 investigation | 1 | 65,536 | 73,728 | 4,096 | 1,048,576 | 120 s | USD 0.60 |
| Aggregate | 3 | 147,456 | 167,936 | 8,448 | 2,359,296 | — | USD 1.34 |

The aggregate DNS-resolution maximum is 3. The cumulative native credential
API ceilings after WP9, WP10, and WP11 are respectively CredWriteW `1/1/1`,
CredReadW `3/4/5`, CredDeleteW `0/0/0`, CredFree `2/3/4`, and total
`6/8/10`; the final aggregate maximum is therefore CredWriteW 1, CredReadW 5,
CredDeleteW 0, CredFree 4, total 10. The campaign permits exactly one
credential enrollment and three sequential provider starts. It prohibits
automatic retry, manual retry, alternate provider/model/key, parallel dispatch,
a fourth request, ceiling transfer between stages, and use of an earlier
campaign's credential evidence.

Before each provider send, the coordinator durably reserves the entire stage,
then records the possible-start latch. A known or possible start consumes that
stage and one DNS allowance. A genuinely ambiguous possible start retains the
full unresolved byte/token/response/cost reservation, blocks every later stage,
and can never be retried. A pre-start failure is released undispatched. A known
settled response remains settled even if later sidecar or semantic evidence is
rejected; reopening reconciles it to terminal known-settled state and never
creates retry authority. Each stage permits at most one credential read/free
pair and one DNS resolution.

Every native credential effect, including stage reads, must begin strictly
before credential-effect expiry; every provider effect must begin strictly
before campaign expiry. A started operation may finish retention, settlement,
and adjudication after expiry, but expiry never creates retry authority.

The accepted safety-identifier contract is unchanged. R2 preserves one stable
per-product-user seed created once from 32 operating-system cryptographic RNG
bytes in atomic create-new local state
`product-user-safety-identifier.v1.seed`. The only transmitted value is the
64-character lowercase-hex SHA-256 of UTF-8 domain
`infinium.openai.safety-identifier/v1`, a NUL framing byte, and the seed. Raw
seed bytes are never transmitted. Credential/key bytes, target/profile/
generation, account/email, OS user/machine, source/prompt, file/mod, and
advertising/telemetry identities are forbidden inputs.

Before the first possible provider start, the coordinator atomically writes
`product-user-safety-identifier.v1.use` under schema
`infinium.product-user-safety-identifier-use/v1` with the exact transmitted
projection, and the ledger retains the same projection. Every stage reopen
requires seed, use latch, and ledger projection to agree byte-for-byte.
Missing, corrupt, torn, deleted, or changed seed/use-latch state after use is a
terminal campaign stop and can never regenerate identity or authorize retry.

## 6. Advancement and intervention policy

After the owner accepts this planning amendment once, advancement is automatic
when the current package's machine gates and fresh independent review are
accepted and the next package's exact preconditions remain true. There is no
routine owner stop between WP9, WP10, and WP11.

Manual intervention is limited to:

- the one owner acceptance of this exact planning candidate;
- the user's one API-key paste into the helper-owned masked modal;
- a genuine authority or product-contract conflict;
- a secret or answer-isolation breach;
- an ambiguous possible provider start;
- expiry or an unavailable owner-controlled resource; or
- any known-started transport, provider, semantic, or evidence failure that
  cannot be corrected offline without a new effect.

Ordinary implementation, test, schema, codec, fixture, validator, docs, and
review defects are `CORRECT`: batch the findings, repair the coherent
candidate, run focused checks, and return to the meaningful acceptance gate.
Only `docs/execution-policy.md` escalation conditions stop the affected path.

## 7. Remaining vertical packages

### R1 — v2 authority, packages, oracles, registry, and clean-break contracts

**Objective and authority.** Materialize the accepted v2 public validation
authority without observing or comparing product output. Authority is this
accepted plan, its fixture-authority amendment, and repository/public-fixture
rules. R1 has no external-effect authority.

**Exact inputs.** The five frozen current packages and current registry;
fixture guidelines and anti-overfitting rules; RESEARCH-0056; product contract
schemas only as structural references; the accepted answer-free examples; and
the immutable starting commit.

**Outputs and owned identities.** The five package directories in Section 4.2,
all ten exact public manifest/oracle schemas and seven auxiliary-file schemas
named by the fixture-authority amendment, independent oracle files, registry
`fixtures/public/public-fixture-registry.v2.json` plus exact schema identity
`infinium.repository.public-fixture-registry/1.7.0`, all five repository
campaign schemas v2, repository schema registration, fixture reader/resealer
support, exact hash inventory, and an append-only R1 record entry. R1 owns the
contract definitions and registry; R2 owns production consumer integration.
R1 does not create campaign stage requests or live evidence.

**Seams.** Input authors create answer-free source/candidate material without
oracle or product-output access. Separate oracle authors derive truth only from
accepted requirements and frozen public source evidence. Fresh reviewers check
both streams before their identities are joined. Producers and consumers must
reject v1/v2 cross-binding, unknown versions, duplicate IDs, missing cases,
unexpected admissions, digest drift, noncanonical JSON, and answer-bearing
input fields. Registry reads and resealing round-trip exact bytes.

**Dependencies and automatic advancement.** Owner acceptance precedes edits.
All five inputs and oracles must be independently frozen, registry closure must
pass, and product comparison must still be absent. An accepted R1 handoff
automatically opens R2.

**Allowed.** Public answer-free fixture authoring; oracle authoring in an
isolated role; schemas, readers, resealers, tests, docs, and offline validation.

**Prohibited.** Product comparison before freeze; deriving expected truth from
product output; private/archive access; product implementation; credentials,
native helper, DNS/network/provider/cost; modifying frozen v1 bytes.

**Verification.** Focused: strict JSON/schema checks, required-case matrix,
unique-ID and exact-one-admission checks, forbidden-field/answer-token scans,
v1 directory hash comparison, v2 manifest hash/length closure, registry count
43, and repository-contract negative tests. Full: documentation validation,
all public-fixture reader/resealer tests, contract tests, and the accepted
non-native common verification floor applicable to the changed projects.

**Independent review.** One fixture-input reviewer with no oracle authorship;
one oracle reviewer with no input/product-output authorship; and one provenance
reviewer for identities, hashes, registry, and answer isolation. Each reports
`ACCEPT`, `CORRECT`, or a classified escalation.

**Evidence and handoff.** Exact file list, authorship separation, SHA-256 and
length ledger, v1 preservation proof, case/admission matrix, focused/full logs,
review identities and dispositions, candidate commit, and clean worktree.

**Correction/stop.** All ordinary defects are corrected before a single R1
refreeze. Authority ambiguity, answer contamination, or inability to preserve
v1 is a genuine stop. No provider retry rule is applicable because no provider
start is allowed.

### R2 — product integration, persistence, replay, provenance, and rehearsal

**Objective and authority.** Integrate the frozen v2 authority across the
complete WP10-to-WP11 vertical path while retaining the starting commit's
settlement/recovery behavior. R2 is ordinary product implementation with no
external-effect authority.

**Exact inputs.** Accepted R1 frozen packages/oracles/registry/contracts and
handoff; HEAD `313ecfc04a22330c4c5dc52a79aae87d13982a74`; active product
contracts, schemas, SQLite/storage versions, coordinator, CLI, and accepted
campaign evidence model.

**Outputs and owned contracts.** Updated package consumers, stage coordinator,
request materialization, semantic admission, evidence acquisition, SQLite
persistence, reload/replay, stage-evidence v2, composed-evidence v2, campaign
authorization v2 and production-profile-authorization v2 handling,
scripts/templates/tests/docs, and non-live evidence.
Remove any fixture-specific production literal: the WP11 positive must be
opened from the exact persisted WP10 acquisition, proposal, admission,
admitted-artifact identity and payload digest/bytes, and application chain.
R2 also preserves the Section 5 safety-seed projection, atomic use-latch,
ledger binding, reopen, forbidden-input, and terminal fail-closed seams without
changing their accepted versions.

**Seams.** Producers emit v2 identities and canonical digests; consumers
default-deny v1, mixed, unknown, or absent campaign bindings. The repository
stage-request v2 contract uses a closed discriminated evidence-root union:
`persisted-source-claim-application` for the positive and
`frozen-host-evidence` for the negative. The positive reopens the full exact
WP10 chain. The negative materializes only its independently frozen public host
evidence root into the authoritative general evidence/application tables,
persists its provenance and payload digest/bytes, and never invents a second
WP10 claim or source-claim application. Both normalize to product-facing
evidence IDs under unchanged candidate-investigation v1. Persistence must
atomically retain acquisition, proposal, admission decision, claim artifact,
application link, host-evidence root, evidence application, candidate result,
costs, attempts, deadlines, and settlement. Reload and replay must reconstruct
identical domain values, payload bytes, root discriminator, and provenance.
Invalid-state tests cover missing/duplicated/mismatched rows, swapped digests,
orphan application, wrong root discriminator, positive/negative root swap,
fabricated negative source-claim application, wrong campaign/stage/package,
stage-order violations, safety-seed regeneration, forbidden identifier input,
missing/corrupt/torn/changed use-latch state or ledger-projection disagreement,
incomplete raw evidence, pre-start failures, committed starts, recovery,
expiry, and exhausted ceilings.

**Dependencies and automatic advancement.** R1 acceptance is required. R2
advances automatically when focused checks, non-live semantic comparison to
the already frozen oracle, persistence/replay, invalid-state, recovery, and
full non-native checks pass and independent product/provenance review accepts.

**Allowed.** Product/tests/contracts/docs changes strictly needed for the
vertical v2 path; offline synthetic host evidence; non-live CLI rehearsal.

**Prohibited.** Fixture exceptions; compatibility shims; changing oracle truth
to fit output; private/archive access; credential/native/network/provider/cost;
campaign materialization or package refreeze after every edit.

**Verification.** Focused: source-claim and investigation contract/codec tests,
semantic admission, exact-one artifact/application persistence, replay, v2
stage/composed evidence validation, invalid-state/adversarial cases, settlement
and interrupted-state recovery, and an offline WP9-WP11 rehearsal. Full: the
accepted common non-live floor once the vertical candidate is coherent,
including formatting, build, unit, integration, contract, fixture-boundary,
architecture, security, and documentation checks.

**Independent review.** Fresh product/semantic reviewer, persistence/replay
reviewer, and security/effect-boundary reviewer. Reviewers inspect the entire
R1-R2 diff from the immutable floor, not isolated patches.

**Evidence and handoff.** Candidate commit; exact changed contracts and seams;
test/log ledger; oracle comparison; SQLite query evidence for exact joins;
recovery evidence; review findings and corrections; clean status.

**Correction/stop.** Ordinary code/test/contract defects are corrected in one
coherent candidate. A need to alter accepted architecture, loosen answer
isolation, or expand native/provider/private authority is a genuine affected-
path stop. No provider start or retry is allowed.

### R3 — coherent candidate freeze, full non-live floor, and finite authority

**Objective and authority.** Freeze one coherent R1-R2 candidate, run the full
non-live acceptance floor once, and bind exact code, package, profile,
credential, budget, and expiry identities into the successor campaign.

**Exact inputs.** Accepted R1-R2 handoffs; immutable starting floor; accepted
request profile; campaign-authority v2 schema; all five v2 package hashes; and
the existing finite ceilings.

**Outputs and owned identities.** One candidate commit, exact registry and
package digests, campaign ID
`infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66`,
pre-effect credential authorization manifest ID
`infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5`,
at exact path
`docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json`,
validated by
`contracts/repository/wp9-production-profile-authorization.v2.schema.json`
identity `infinium.repository.wp9-production-profile-authorization/2.0.0`,
profile ID
`openai-platform-c2f213dbc4d9461c9fa8485050ab324d`, generation ID
`g-cb0c3748ef2b4745b97a9311c89f2b65`, target fingerprint
`7c4683448a864da4b7cb96a07cf13db93cff9b1a1eb22ed013250a2975a9c071`,
the Section 5 expiries/ceilings, exact-hash review/admission markers for the
candidate, campaign, and pre-effect credential authorization manifest, and
independently accepted candidate evidence. The one owner acceptance of this
planning amendment is the source of conditional authority; R3 introduces no
second owner checkpoint.
The raw credential target is never recorded.

**Seams.** Campaign materialization rejects dirty or wrong commit/branch,
mutable/unfrozen packages, wrong hashes, wrong profile/config, secret-bearing
files, stale evidence, mismatched expiry, any earlier campaign identity, or
nonzero effect counters. It also binds the exact safety-identifier state/use-
latch schemas and paths, domain/projection algorithm, forbidden inputs, and
pre-start/reopen fail-closed rules. Stage manifests do not exist before their
stage gate.

**Dependencies and automatic advancement.** R2 acceptance is required. A
single coherent freeze is followed by the complete non-live common floor and
fresh product, security, fixture, provenance, verification, and diff review.
Accepted R3 evidence automatically opens R4 while unexpired.

**Allowed.** Offline build/test/validation, local Git candidate creation,
machine-readable campaign materialization, independent review, record update.

**Prohibited.** Rebinding after each small correction; push; credentials,
Credential Manager, modal, native helper, DNS/network/provider/cost; creating
WP9/WP10/WP11 success evidence before its stage.

**Verification.** Focused corrections first. Then exactly one complete clean
common floor for the coherent candidate, strict campaign/schema validation,
hash recomputation, tracked-secret scans, v1 preservation, exact credential-
manifest v2 schema validation, v2 registry closure,
zero-effect-counter proof, safety-identifier generation/projection/use-latch/
reopen/invalid-state tests, expiry arithmetic, branch/commit ancestry, and
clean-worktree check. If a must-fix changes a bound byte, rerun only affected
focused checks, rebind once, then rerun the complete floor for the new coherent
candidate.

**Independent review.** Fresh bounded reviewers for product/semantics,
security/credentials/budgets/effects, fixture/oracle independence/provenance,
and verification/diff/authority consistency, followed by a different fresh
final acceptance reviewer.

**Evidence and handoff.** Exact candidate commit, clean status, commands and
exit codes, process-cleanup evidence, all identity/hash/budget bindings,
classified findings and corrections, final `ACCEPT`, and R4 preflight.

**Correction/stop.** Ordinary defects return to a batched correction pass.
Accepted-authority conflict, secret/isolation breach, or need for broader
effect authority stops only the affected path. No provider retry is possible.

### R4 — one masked credential enrollment and evidence acceptance

**Objective and authority.** Perform exactly one helper-owned production-
profile enrollment and accept its sanitized evidence. Authority is the
accepted successor campaign and unexpired R3 binding.

**Exact inputs.** Exact R3 candidate and campaign; exact reviewed/admitted
pre-effect credential authorization manifest; unexpired credential-effect
authorization; helper executable/configuration and target fingerprint; zero
enrollment/effect counters; user-supplied key pasted only into the masked modal.

**Outputs and contracts.** One credential enrollment, helper exit/evidence,
sanitized enrollment evidence bound to the already materialized, reviewed, and
admitted R3 credential authorization manifest, cumulative counter state, and
an append-only record entry. No secret value, reversible derivative, raw
target, or Credential Manager content may enter logs, files, process arguments,
shell history, Git, or reviewer evidence.

**Seams.** The orchestrator invokes only the accepted wrapper; the coordinator
alone launches the helper, and only that helper may access the exact credential
target and secret value. The normal closed
sequence is target pre-read, one write, verification read, and free for each
successful read: CredWriteW 1, CredReadW 2, CredDeleteW 0, CredFree 1, total 4.
The collision path is CredReadW 1/CredFree 1, performs no write, and stops. The
helper-launch, readiness, authority-lock, and profile-materialization maxima
are each one. The user performs the sole secret-bearing action. Evidence
validates profile/generation/target fingerprint, timestamps, result, and
counter transition. Replay reads sanitized evidence only. Missing, duplicate,
stale, or mismatched evidence is invalid.

The only authorized wrapper grammar is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/run-m1-slice6-credential.ps1 -Operation EnrollOrVerifyProfile -AuthorizationManifest docs/plans/milestones/m1/slices/s6/wp9-production-profile-authorization.v2.json -OutputRoot artifacts/m1-slice6/wp9-profile
```

The wrapper invokes the coordinator; the coordinator alone launches the
one-shot helper. The orchestrator must not invoke the helper executable
directly or vary the operation, manifest path, or output root.

**Dependencies and automatic advancement.** R3 acceptance, exact clean
candidate, zero counters, and expiry checks. Accepted machine evidence plus a
fresh security review automatically opens R5; no owner checkpoint is added.

**Allowed.** One native helper launch and one masked user paste.

**Prohibited.** Direct Credential Manager access by the orchestrator, renderer,
reviewer, or any process other than the accepted helper; handling/copying the
key outside the modal; alternate key/profile/target; repeated enrollment;
network/provider request; cost; screenshots containing secrets; or retry after
an ambiguous possible effect.

**Verification.** Preflight identity/hash/expiry/counter/process checks;
postflight sanitized schema, no-secret, single-transition, and process cleanup
checks; full common floor is not repeated unless tracked bytes changed.

**Independent review.** Fresh credential/security reviewer examines only
sanitized evidence and effect accounting.

**Evidence and handoff.** Sanitized manifest/evidence paths and digests,
timestamps, exit state, counter ledger, clean status, reviewer disposition, R5
preflight. The key itself is never evidence.

**Correction/stop.** Evidence-format or documentation defects with an
unambiguous single enrollment are correctable offline. Secret exposure,
ambiguous enrollment, expiry, helper mismatch, or unavailable owner-controlled
key is a genuine stop. Enrollment is never retried automatically or manually
under this campaign.

### R5 — WP9 transport qualification and evidence acceptance

**Objective and authority.** Execute exactly one bounded qualification request
to prove the accepted transport/profile path, then accept its evidence.

**Exact inputs.** Accepted R4 credential evidence; exact campaign/candidate;
WP9 request bytes; unchanged
`M1-PLAT-PROVIDER-CAPABILITY-VAL-v1/1.0.0` non-semantic validation binding;
stateless/cache-off profile; zero provider-start counters; unexpired authority
and unused WP9 ceilings.

**Outputs and contracts.** Before any possible start, the coordinator alone
materializes the exact WP9 stage-request v2 manifest from the accepted
credential predecessor and canonical request and commits its exact bytes. A
fresh reviewer appends the exact-hash stage-review marker; the coordinator then
appends the exact stage-admission marker and, only at execution preflight,
records the ledger's `stage-reserved` transition. Rejection omits admission,
records stopped evidence, and does not send. After admission: one WP9
request/response evidence set,
raw-response retention under accepted limits, parsed qualification result,
cost/token/byte/deadline/counter settlement, recovery state, stage-evidence
handoff/acceptance transitions, and sanitized review handoff.

**Seams.** Request materialization, start commitment, provider transport,
response capture, parsing, persistence, settlement/recovery, reload/replay, and
stage evidence v2 must agree on exact IDs/digests and whether a start occurred.
Malformed, partial, duplicate, wrong-profile, over-limit, or ambiguous states
default deny advancement.

**Dependencies and automatic advancement.** R4 acceptance and exact preflight,
including a fresh exact local official-document snapshot/profile/model/
capability/price comparison. Any drift or need for a public refresh is a
pre-start affected-path stop because the campaign grants no extra network
operation.
If the one response passes machine gates, persisted settlement/recovery is
complete, ceilings hold, and fresh independent review accepts, R6 opens
automatically.

**Allowed.** One qualification provider start and only the network/DNS effects
owned by that request.

**Prohibited.** Semantic WP10 work in the same request, public network outside
the exact admitted request, retry, alternate
provider/model/key, parallel call, ceiling transfer, fourth request, unrelated
public network, or manual reinterpretation of failure as success.

**Verification.** Exact stage-manifest production, predecessor/hash review,
ledger admission/rejection, and preflight; post-start raw/parsed/evidence validation;
profile/cache/stateless checks; counters, bytes, tokens, cost, deadline,
settlement/recovery and replay; focused accumulated WP9 regression. No repeated
full floor unless tracked implementation changed.

**Independent review.** Fresh transport/effect and evidence reviewer with no
authority to start another request.

**Evidence and handoff.** Exact start disposition, provider request/response
IDs where permitted, timestamps, digests, usage/cost, cumulative counters,
recovery outcome, classified findings, acceptance, and R6 preflight.

**Correction/stop.** Pre-start local defects are corrected before the single
start. Post-start evidence/persistence defects may be corrected offline if the
result is unambiguous. Any semantic/transport failure requiring a new request,
ambiguous possible start, ceiling breach, expiry, or secret/isolation breach is
a genuine stop. No retry is legal.

### R6 — WP10 live source-claim extraction and evidence acceptance

**Objective and authority.** Execute one WP10 request against the frozen v2
input, validate all required semantic states, and persist exactly one admitted
supported claim and application for WP11.

**Exact inputs.** Accepted R5 stage evidence and exact acceptance digest; exact campaign/candidate; frozen
`S6-CLAIM-LIVE-VAL-v2/2.0.0` input and
`LLM-CLAIM-LIVE-VAL-v2/2.0.0` oracle; unused WP10 ceiling; unexpired authority.

**Outputs and contracts.** Before any possible start, the coordinator alone
materializes the exact WP10 stage-request v2 manifest from the accepted WP9
evidence digest and canonical v2 request and commits its exact bytes. A fresh
reviewer appends the exact-hash stage-review marker; the coordinator then
appends the exact stage-admission marker and, only at execution preflight,
records the ledger's `stage-reserved` transition. Rejection omits admission,
records stopped evidence, and does not send. After admission: one raw/parsed
WP10 acquisition; proposal set; case-level
semantic comparison; host decisions; exactly one admitted claim artifact and
application link; durable SQLite rows; stage-evidence handoff/acceptance v2;
settlement/recovery and replay evidence.

**Seams.** Provider output is untrusted proposal data. Host validation and the
independently frozen oracle decide admission. Producer, consumer, persistence,
reload/replay, and invalid-state checks bind the exact acquisition/proposal/
decision/artifact/application identities and digests. Unsupported,
conditional, version-scoped, contradictory, hostile, deleted, and insufficient
content cannot become admitted truth.

**Dependencies and automatic advancement.** R5 acceptance and exact preflight,
including a fresh exact local official-document snapshot/profile/model/
capability/price comparison. Any drift or need for a public refresh is a
pre-start affected-path stop.
Exactly one admission plus accepted machine gates, persistence/replay,
settlement, and fresh independent semantic/provenance review automatically
opens R7.

**Allowed.** One WP10 provider start and its bounded evidence effects.

**Prohibited.** Oracle edits after comparison, admission of any non-supported
case, product-authored truth, fixture-specific exceptions, second request,
retry, alternate provider/model/key, parallelism, ceiling transfer, or WP11
start before R6 acceptance.

**Verification.** Exact stage-manifest production, predecessor/hash review,
ledger admission/rejection, and preflight; schema/canonicalization; all case outcomes;
exact-one admission; rejection/suppression reasons; SQLite identity/digest
joins; reload/replay; invalid states; cost/usage/deadline/counters;
settlement/recovery; focused accumulated WP9-WP10 regression.

**Independent review.** Fresh semantic/admission reviewer and provenance/
persistence reviewer, neither authorized to edit the oracle or start a request.

**Evidence and handoff.** Raw/parsed digests, case matrix, exact admitted chain,
SQLite query evidence, stage-evidence digest, usage/cost/counters,
settlement/recovery, findings and acceptance, and R7 preflight.

**Correction/stop.** Local post-processing or evidence defects are correctable
offline when the one response/start is unambiguous and truth is unchanged. A
semantic failure, missing/extra admission, answer-isolation concern, ambiguous
start, expiry, or any need for another provider response is a genuine stop. No
retry is legal.

### R7 — WP11 investigation, composition, freeze, and Slice 6 closeout

**Objective and authority.** Execute the one WP11 request, consume the exact
persisted R6 predecessor, prove the matched negative and composed provenance,
run accumulated regression, freeze implementation-active contracts, and
prepare Slice 6 acceptance.

**Exact inputs.** Accepted R6 stage evidence and exact acceptance digest plus
the persisted admitted chain; frozen
`S6-CANDIDATE-LIVE-VAL-v2/2.0.0`,
`LLM-INVESTIGATE-LIVE-VAL-v2/2.0.0`, and
`PROV-LIVE-COMPOSED-VAL-v2/2.0.0`; exact campaign/candidate; unused WP11
ceiling; unexpired authority.

**Outputs and contracts.** Before any possible start, the coordinator alone
materializes the exact WP11 stage-request v2 manifest from the accepted WP10
stage-evidence digest, persisted chain, and canonical v2 request and commits its
exact bytes. A fresh reviewer appends the exact-hash stage-review marker; the
coordinator then appends the exact stage-admission marker and, only at execution
preflight, records the ledger's `stage-reserved` transition. Rejection omits
admission, records stopped evidence, and does not send. After admission: one
raw/parsed WP11 acquisition;
positive and matched-negative results; exact predecessor/application
consumption evidence; durable candidate state; stage-evidence handoff/
acceptance v2; composed-evidence v2; complete
usage/cost/counter settlement; recovery/replay evidence; accumulated regression
and contract-freeze evidence; append-only closeout and current-state handoff.

**Seams.** The positive is materialized by reopening persisted R6 identities
and bytes, never by a source literal or newly synthesized claim. The matched
negative uses its independent host evidence and creates no extra claim.
Composition joins both stages, package/oracle hashes, campaign/profile,
persistence rows, usage, settlement, and recovery. Missing, duplicate, swapped,
or stale links, a fourth request, or unproven recovery invalidate closeout.

**Dependencies and automatic advancement.** R6 acceptance and exact preflight,
including a fresh exact local official-document snapshot/profile/model/
capability/price comparison. Any drift or need for a public refresh is a
pre-start affected-path stop.
Machine gates and fresh independent review may automatically proceed through
offline composition and accumulated regression. Final project-owner Slice 6
acceptance remains the milestone governance decision; no additional provider
or credential effect is required for it.

**Allowed.** One WP11 provider start; bounded evidence persistence; offline
composition, regression, documentation, and contract-freeze review.

**Prohibited.** Reconstructing the predecessor, a second WP10 claim, retry,
alternate provider/model/key, parallel dispatch, fourth request, ceiling
transfer, oracle modification, private scoring, network beyond the one request,
push, or claiming owner acceptance.

**Verification.** Exact stage-manifest production, predecessor/hash review,
ledger admission/rejection, and preflight; positive/negative semantic comparison;
persisted-predecessor byte/ID/digest proof; candidate persistence/replay and
invalid states; composed provenance; full campaign usage/cost/deadline/counter
and settlement/recovery closure; then the complete accepted common floor and
architecture/security/fixture-boundary/documentation checks. Inspect and stop
repository-owned test processes after verification.

**Independent review.** Fresh WP11 semantic reviewer, cross-stage provenance
reviewer, security/effect reviewer, and final Slice 6 verification/diff reviewer.
No reviewer may initiate or retry an external effect.

**Evidence and handoff.** Exact final commit and clean worktree; three-start
ledger; all raw/parsed/stage/composed digests; persisted joins and replay;
settlement/recovery; full verification logs; classified corrections; contract
freeze statement; closeout record/current-state changes; owner-ready decision.

**Correction/stop.** Offline defects are corrected and focused checks rerun;
the complete floor is repeated only if the coherent candidate changes. A
post-start semantic failure requiring another response, ambiguous start,
ceiling/expiry breach, secret/isolation breach, or authority conflict is a
genuine stop. No retry or fourth request is legal.

## 8. Verification protocol

R1-R3 implementation must add named, deterministic gates to
`eng/verify-m1-slice6.ps1` (or the accepted successor entry point) for:

- `LiveSemanticV2Authority` — five-package, oracle, answer-isolation, v1
  preservation, and registry closure;
- `CampaignV2NonLive` — complete offline WP9-WP11 rehearsal, persistence,
  replay, invalid states, settlement/recovery, and effect denial;
- `NonLiveAll` — the full accepted non-native common floor; and
- `Layer6Review -BaselineCommit 313ecfc04a22330c4c5dc52a79aae87d13982a74
  -CandidateCommit HEAD` — architecture, security, fixture boundary, tracked
  secret, diff, and authority checks.

Exact command syntax may be corrected during R2 if the script's accepted
interface differs, but the named semantics and one-command auditable entry
points are required. Focused tests run during correction. The complete common
floor runs only after a coherent candidate and again only after a bound-byte
correction. Every run records command, commit, UTC start/end, exit code, log
digest, and process cleanup. A pass from a dirty tree or different commit is
not acceptance evidence.

## 9. Review and finding disposition

Meaningful boundaries use fresh, bounded, read-only reviewers. Authors do not
self-certify fixture truth, oracle independence, effect safety, or final plan
acceptance. Reviews return findings classified exactly as:

- `MUST-FIX / CORRECT` — ordinary defect repaired autonomously;
- `FOLLOW-UP` — valid work outside this accepted slice, recorded without
  weakening current acceptance;
- `NON-BLOCKING` — observation with no acceptance impact;
- `OWNER/AUTHORITY DECISION` — genuine incomplete or conflicting authority;
  or
- `SAFETY/ISOLATION BREACH` — affected path stops immediately.

Findings are batched before correction. A full review is not restarted for
each wording or mechanical fix; the corrected surface receives focused review,
then the next meaningful boundary receives a fresh reviewer. No correction-
pass budget applies.

## 10. Implementation handoff contract

The replacement orchestrator must:

1. re-read repository entry documents, this accepted plan, its companion
   amendments, and the current record tail;
2. verify exact branch, accepted planning commit, clean status, ancestry,
   expiry, and zero campaign effects;
3. implement R1-R3 as one vertical, effect-free candidate while using the
   package acceptance boundaries above;
4. use fresh bounded reviewers and correct every `MUST-FIX / CORRECT` finding;
5. never access private/archive material, push, or perform native/provider
   effects before the exact stage gate;
6. at R4 request only the user's masked-modal paste, then advance WP9-WP11
   automatically after accepted evidence; and
7. stop only the affected path for a listed genuine escalation and never retry
   a possibly started provider operation.

The handoff must always name the last accepted package, exact candidate commit,
clean/dirty state, counters, remaining ceilings, expiry, evidence paths, open
findings, and next exact gate. Historical names or records never substitute for
live verification.

## 11. Owner acceptance semantics

The owner must accept the exact committed versions and SHA-256 digests of this
plan, the fixture-authority amendment, the research conclusion, and the
machine-readable authority amendment together. Acceptance must explicitly bind
the five v2 design identities, campaign ID, profile/generation/target
fingerprint, one enrollment, three starts, stage and aggregate ceilings,
no-retry rule, and both expiries.

The copy-pasteable statement is supplied with the final planning handoff once
the exact candidate commit and digests exist. Any content change after owner
acceptance requires a new candidate and renewed acceptance. The planning lead
must not mark this plan or its authority amendment Accepted on the owner's
behalf.
