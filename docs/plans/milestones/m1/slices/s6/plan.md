# M1 Slice 6: Direct OpenAI credential, budget, and semantic operations

> Current evaluation-timing supersession (2026-08-23): ADR-0035 and the
> accepted M1 semantic-oracle deferral amendment preserve this plan's product
> meanings, contracts, safety rules, and ordinary verification, but supersede
> every requirement to author, review, seal, compare, or pass an independent
> semantic oracle during M1 or M2. Those passages remain historical. Slice 6
> closeout now uses the active product-conformance profile and cannot claim an
> independent semantic verdict.

> Active execution note (2026-08-21): the owner-authorized
> [practical development continuation](development-continuation.md) supersedes
> remaining credential-ceremony proliferation and per-correction authority or
> independent-review requirements. This accepted plan continues to define
> product meaning, stage order, answer isolation, and C3 obligations.

Status: Accepted

Disposition: Owner-accepted implementation authority; live handoff is
maintained in `docs/current-state.md`

Accepted: 2026-08-10

Accepted by: Project owner

Work ID: `M1/S6`

Parent: `M1`

Depends on: accepted M1 plan; accepted ADR-0001, ADR-0002, ADR-0013,
ADR-0015 through ADR-0023, and ADR-0025; accepted M1 continuation verification
profile; owner-accepted `M1/S5`; RESEARCH-0054 owner disposition

Initial authorized work package: `M1/S6/WP1`

Planning baseline: branch `main`, commit
`d88ba5a5806944f4ec5e919f754dffadc00ebc5f`

Last reviewed: 2026-08-10

## 0. Authority and status

This plan consumes accepted authority; it does not create product meaning. The
project owner accepted it on 2026-08-10. Current implementation authorization
is stated only in `docs/current-state.md`; plan acceptance alone never grants a
Credential Manager operation or live/billable request.

The live repository state at planning time is:

- `main` and `origin/main` both identify `d88ba5a`;
- the worktree was clean;
- accepted Slice 5 implementation candidate `5514919` is an ancestor;
- Slice 5 is owner-accepted and its contracts are `Slice-frozen`;
- at the planning baseline no slice was active and no Slice 6 implementation
  was authorized; and
- the owner subsequently accepted this plan and the exact request-profile
  disposition recorded below.

The accepted plan and current handoff authorize WP1 and permit later non-live
packages only through their stated prerequisite/acceptance gates and an exact
`current-state.md` update. They never grant native Credential Manager or live-
provider authority.

The project owner accepted the two exact-profile conclusions from
RESEARCH-0054 as ADR-0025 conformance closure:

1. explicitly use `reasoning.context: "current_turn"` and ordinary/standard
   reasoning mode to preserve ADR-0025's no-persisted-reasoning boundary; and
2. explicitly use `prompt_cache_options.mode: "explicit"` with no breakpoints
   or cache key to disable GPT-5.6's current implicit prompt caching.

No separate ADR is required. Any later proposal to enable multi-turn persisted
reasoning or provider prompt caching changes the accepted profile and requires
fresh owner/architecture disposition before implementation.

## 1. Objective and exact exit state

Slice 6 establishes the first complete, bounded, user-owned direct-provider
path over the provider-independent Slice 5 pipeline:

```text
explicit user selection and confirmation
  + exact access-profile/generation/revocation identity
  + immutable capability and price snapshots
  + closed request/profile/schema/settings
  + finite worst-case multi-scope reservation
    -> one-shot helper exact-target credential resolution
    -> immediate coordinator dispatch revalidation
    -> one direct synchronous Responses transport attempt
    -> bounded raw response and non-secret receipt staging
    -> coordinator validation, admission, usage settlement, and publication
    -> untrusted semantic proposal
    -> Slice 5 host validation/application/admission
    -> human and versioned JSON disclosure
    -> retained-response replay without another provider request
```

Slice 6 is complete only when:

1. provider profile, credential intent, capability, price, authorization,
   request, reservation, fence, response, usage, settlement, semantic proposal,
   and replay contracts are closed and consumed consistently by producer,
   persistence, wire/query, output, replay, fixtures, and tests;
2. SQLite migration `M1-S6-0006` advances exact accepted schema 5 to schema 6
   and storage contract `1.4.0` to `1.5.0` without changing Slice 5 truth;
3. a unique Credential Manager test namespace proves native enrollment,
   replacement, disable, deletion, recovery, exact-target behavior, backup
   non-portability, and cleanup without enumeration or leakage;
4. the real coordinator transaction performs atomic multi-scope reservation,
   final dispatch fencing, one-owned settlement, unresolved holds, and
   projection rebuild under deterministic concurrency and fault schedules;
5. the one-shot helper alone sees credential bytes and performs a single
   allowlisted request with no redirect, automatic retry, fallback, arbitrary
   URL/header, or long-lived credential client;
6. the exact M1 request profile is explicit and drift-detectable, including
   stateless reasoning, cache disabled, no provider tools, fixed serializer,
   strict schemas, finite bounds, and complete response/usage handling;
7. local-only work remains complete where inputs exist and every provider-
   dependent absence is an explicit unsupported/unavailable/gap state;
8. source-claim extraction and candidate investigation consume frozen Slice 5
   inputs and produce only untrusted proposals subject to host validation;
9. deterministic transcripts prove positive, matched-negative, hostile,
   malformed, refusal, incomplete, deletion, replay, and provenance behavior;
10. applicable continuation-profile Layers 1 through 4 and 6, the common
    command floor, and all Slice 6 non-live gates pass before any live request;
11. a separately authorized production profile is enrolled or exact-target
    verified, then a distinctly authorized tiny transport qualification passes
    through its credential/reservation/helper/adapter/admission/settlement path;
12. one later separately authorized source-claim request passes
    `LLM-CLAIM-LIVE-VAL`;
13. one later separately authorized candidate-investigation request containing
    both a positive and matched negative passes `LLM-INVESTIGATE-LIVE-VAL`;
14. `PROV-LIVE-COMPOSED-VAL` connects all three retained operations without
    sending a fourth request;
15. every credential or provider external effect has its distinct owner
    authorization and exact identities; every provider request additionally has
    tiny finite limits, no automatic retry, terminal or honestly unresolved
    settlement, and secret-canary evidence; and
16. a fresh contract/security/semantic/provenance/diff review accepts the
    corrected final implementation and the implementation record states exact
    claims, gaps, commands, counts, identities, commits, and no-private-verdict
    boundary.

Completion proves only the three exact M1 provider operations and their
synthetic substrate. It does not prove a production model router, broad model
quality, deterministic live re-execution, provider billing accuracy, credit
availability, ZDR, hosted search, background/Batch/concurrent/cache-dependent
dispatch, Nexus acquisition, general semantic reliability, readiness, private
held-out validation, or M1 completion.

## 2. Required reading for execution

Every implementing or reviewing agent reads, in order:

1. repository `AGENTS.md`;
2. `docs/README.md`, `docs/current-state.md`, and
   `docs/execution-policy.md`;
3. the accepted M1 plan and this full plan;
4. the M1 continuation verification profile;
5. Slice 5 `current.md`, then the Slice 5 handoff/claim-boundary section of its
   full accepted plan; load its record only for exact evidence/chronology;
6. accepted ADR-0001, ADR-0002, ADR-0013, ADR-0015 through ADR-0023, and
   ADR-0025;
7. RESEARCH-0054 and its owner disposition;
8. the M1 evaluation strategy, case catalog, fixture guidelines,
   anti-overfitting rules, relevant platform/semantic specifications, and
   fixture catalog sections; and
9. the package-specific source, schema, code, fixture, and prerequisite record.

Do not read evaluator-private files, historical evaluator plans, retired
protocol code, sibling archives, or the abandoned implementation archive.

## 3. In scope and excluded

### In scope

- exact user-owned OpenAI Platform API access profile and non-secret metadata;
- exact-target Windows Credential Manager generic credentials through the
  one-shot helper;
- direct synchronous `/v1/responses` over `https://api.openai.com` only;
- exact `gpt-5.6-sol` M1 profile and two strict semantic schemas;
- immutable capability/price snapshots, rational nano-USD calculation,
  atomic budgets, dispatch fencing, receipts, settlement, and replay;
- deterministic provider simulation and public independently expected cases;
- application/coordinator/helper integration, additive bounded queries,
  human/JSON disclosure, backup/restore, recovery, and audit;
- one tiny live qualification and two bounded live semantic requests only
  after their separate owner authorizations; and
- implementation record, fresh reviews, correction, re-review, and handoff.

### Excluded

- any private fixture, evaluator protocol, corpus qualification, held-out
  scoring, B2/C2/Stage D, successor evaluator, or verdict;
- any legacy/evaluator archive or historical protocol implementation;
- ChatGPT/Codex subscription access, project/shared keys, OAuth/device login,
  alternate provider, fallback, aliases, routing, or lower-cost model study;
- provider tools, hosted search, file/image input, background mode, Batch,
  streaming, WebSocket, conversation/previous-response state, persisted
  reasoning, Pro mode, prompt templates, explicit cache breakpoints, or cache-
  dependent budgeting;
- automatic retries, concurrent live billable attempts, admin APIs, usage/
  spend/credit expansion, provider-side revocation, or reconciliation calls;
- Nexus/LOOT/source refresh, generic Slice 7 mechanism, Slice 8 controlled-
  real cases, Slice 9 closeout, UI workflow delivery, packaging, or updates;
- arbitrary URL/header/path/command/SQL/credential-target primitives;
- setup/game/MO2/generated-output mutation or any protected-root write; and
- product implementation before this plan and current-state implementation
  authority are accepted.

## 4. Slice 5 frozen boundary

Slice 6 consumes Slice 5; it does not reinterpret it.

- Local observations remain host-authored facts.
- Documentation evidence retains exact source revision, passage, authority,
  applicability, purpose, contradiction, and application links.
- Candidate eligibility and score-independent admission remain host-owned.
- Model output is an untrusted external-claim or hypothesis/abstention
  proposal, never local evidence or automatic finding/case authority.
- Finding thresholds, recommendations, grouping, lineage, taxonomy, coverage,
  gaps, and publication remain Slice 5 host policy.
- Expected fixture truth never enters prompt, request, product input, model
  context, admission logic, or product output.
- Provider/search/Nexus remain `not-used` for unchanged Slice 5/local runs.
- Any necessary incompatible revision of a Slice 5 contract is a clean-break
  cross-seam plan amendment and owner review, not a compatibility wrapper.

## 5. Exact M1 provider profile

Every M1 provider request has one canonical serializer and this closed profile:

```json
{
  "model": "gpt-5.6-sol",
  "reasoning": {
    "effort": "medium",
    "context": "current_turn",
    "mode": "standard"
  },
  "text": {
    "format": {
      "type": "json_schema",
      "name": "<closed-operation-name>",
      "strict": true,
      "schema": "<closed-operation-schema>"
    }
  },
  "store": false,
  "service_tier": "default",
  "background": false,
  "stream": false,
  "tool_choice": "none",
  "tools": [],
  "truncation": "disabled",
  "max_output_tokens": "<finite-operation-limit>",
  "prompt_cache_options": {
    "mode": "explicit"
  }
}
```

The request has no prompt cache key/breakpoint, conversation,
`previous_response_id`, prompt-template ID, file/image/audio input, tool,
parallel tool use, web search, background/Batch field, metadata map, arbitrary
provider option, or caller-selected model/host/tier. If an explicitly listed
field is not accepted by the current provider schema, that is capability drift
and blocks live operation pending plan/authority review; it is not silently
omitted.

`X-Client-Request-Id`, when emitted, is correlation evidence only. Neither it
nor a local request fingerprint is treated as provider idempotency, retry
authority, or evidence that an ambiguous transport did not execute.

`tools: []` plus `tool_choice: "none"` is retained only if the current exact
Responses schema accepts that combination. Otherwise the canonical codec must
omit both and prove structural tool absence. The accepted request fingerprint
records the chosen representation.

Response admission requires the exact requested profile, an allowed terminal
state, bounded bytes, valid strict-schema output, returned model compatibility,
effective service-tier disclosure, request/Response identities where present,
and complete typed usage. Refusal, incomplete, failed, queued, in-progress,
cancelled, malformed, oversized, mismatched, or unknown states are non-success.

Cache read and write token fields are retained separately. For this cache-off
profile, any non-zero value is drift/failure. Missing fields remain unavailable
until the exact live qualification establishes an accepted, documented
equivalent; absence is never assumed zero.

The following are hard Slice 6 ceilings, not targets. A package manifest may
lower but never raise them:

| Operation | Canonical request-body bytes | Locally admitted input-token upper bound | `max_output_tokens` | Raw response bytes | Deadline | Calls | Nano-USD cap |
|---|---:|---:|---:|---:|---:|---:|---:|
| Transport qualification | 16,384 | 20,480 | 256 | 262,144 | 60 s | 1 | 140,000,000 |
| Source-claim extraction | 65,536 | 73,728 | 4,096 | 1,048,576 | 120 s | 1 | 600,000,000 |
| Candidate investigation | 65,536 | 73,728 | 4,096 | 1,048,576 | 120 s | 1 | 600,000,000 |

WP1 must prove the local input bound from canonical UTF-8 request bytes plus a
fixed structural-token margin or replace it with an exact local tokenizer. The
provider input-token-count endpoint is never part of admission. The nano-USD
ceilings cover the current documented 1.25-times cache-write input class plus
ordinary output price and component-wise upward rounding; actual reservation
uses the most expensive still-applicable documented class. No bound approaches
the provider's greater-than-272K input price tier. Failure to prove the stated
local bound blocks WP1 rather than increasing a ceiling implicitly.

## 6. Trust, process, credential, and transport model

### Coordinator

The coordinator alone owns durable state, profile metadata, user
authorization, reservations, final gates, helper launch, process handles,
staging authorization, response admission, settlement, publication, queries,
and recovery.

### Credential/provider helper

The helper is a coordinator-launched one-shot executable with inherited
private handles. It may perform exactly one credential-lifecycle assignment or
one provider-dispatch assignment. It has no ordinary application gRPC service,
database access, general query, publication authority, arbitrary URL, target,
path, header, or retry primitive.

The helper derives `Infinium:<profile-id>:<generation-id>` internally, calls
only exact-target `CredWriteW`, `CredReadW`, `CredDeleteW`, and `CredFree`, and
never enumerates. The 2,560-byte generic-credential maximum fails closed.

For transport it uses only the .NET base-class HTTP stack with redirects,
automatic retries, cookies, proxy credential fallback, and decompression
surprises explicitly controlled. It sends once to the closed Responses host
and path, stages bounded raw bytes and a non-secret receipt, and terminates.

### General workers and application clients

They remain secret-free and cannot call provider or credential primitives.
They receive only admitted typed payloads or explicit availability/gap state.

## 7. Clean-break contracts, wire, and storage

WP1 closes these current v1 product documents:

| File | Schema ID | Required content |
|---|---|---|
| `provider-access-profile.v1.schema.json` | `infinium.provider.access-profile/v1` | Opaque profile/generation/revocation/account/scope identities; provider/purpose; lifecycle/verification; non-secret intent/recovery/cleanup; no target or secret. |
| `provider-operation.v1.schema.json` | `infinium.provider.operation/v1` | Capability/price snapshots; authorization; owner/job/attempt/request; exact profile/settings/schema/request fingerprints; finite limits; reservation/fence/transport/receipt/usage/settlement/replay states. |
| `provider-response.v1.schema.json` | `infinium.provider.response/v1` | Bounded raw-response payload reference and fingerprint; HTTP/Response/status/refusal/incomplete/error; requested/returned model/tier/reasoning/cache settings; usage/rate facts; validation/admission. |
| `source-claim-extraction.v1.schema.json` | `infinium.llm.source-claim-extraction/v1` | Exact retained revision/passages and purpose; bounded claim/condition/applicability/citation proposals; contradictions/abstentions/gaps; validation/application links. |
| `candidate-investigation.v1.schema.json` | `infinium.llm.candidate-investigation/v1` | Exact host-selected candidate/roles/path/closure and evidence; bounded hypothesis/contradiction/missing/abstention proposals; validation/admission links. |
| `provider-execution-input.v1.schema.json` | `infinium.provider.execution-input/v1` | Answer-free owner/snapshot/context/config/request/profile/capability/price/limits/prompt/schema/operation inputs; no oracle or expected label. |
| `effective-scan-configuration.v2.schema.json` | `infinium.scan.effective-configuration/v2` | Clean provider-active successor to frozen v1; exact stateless/cache-off OpenAI profile, access-profile binding, finite provider limits, independent controls, and `not-used` only for hosted-search/Nexus/LOOT. |
| `run-output.v2.schema.json` | `infinium.run-output/v2` | Additive successor carrying provider-operation, evidence-acquisition, admission, usage/cost/hold, replay, drift, and gap references; no raw transport or secret. |
| `cli-summary.v2.schema.json` | `infinium.cli-summary/v2` | Human/JSON-aligned provider used/not-used/unavailable/live, cost, hold, replay, and gap projection over application queries; no direct database or helper access. |

All schemas are closed, bounded, versioned, canonical, and strict. Unknown,
unavailable, unsupported, not-applicable, not-used, rejected, incomplete,
failed-known, transport-ambiguous, unresolved, and deleted states are explicit.

WP1 updates or adds:

- domain records, invariants, canonicalizers, JSON codecs, and protobuf
  identities/summaries;
- additive application queries/commands for non-secret profile enrollment
  intent, selection/confirmation, status, operation, budget, usage, settlement,
  and replay;
- helper-private protocol v2, with a new protocol identity and fingerprint,
  rather than reinterpreting the current scaffold's v1 identity; privileged
  unknown fields continue to fail closed and v1 remains independently decodable;
- configuration v2 producer/consumer/persistence/replay support without
  altering or dual-interpreting frozen v1; v1 remains the exact local/provider-
  `not-used` Slice 5 contract and v2 is required for every provider operation;
- additive run-output/CLI-summary v2 documents for provider references and
  `not-used`/unavailable/live distinctions without embedding raw secret-bearing
  transport; frozen Slice 5 v1 documents retain their exact local-only meaning;
- schema 6 tables and append-only triggers for profiles/generations/intents,
  capability/price snapshots, operation authorizations, attempts, requests,
  reservations/scope items, fences, transport events, responses, usage,
  settlements/adjustments, semantic proposals/admissions, evidence-acquisition
  runs and their immutable parent/detachment/application links, and replay
  edges;
- exact active-generation, one-live-attempt, one-owner, unique local request/
  fingerprint, no-provider-idempotency, and append-only invariants; and
- backup/restore, deletion, projection rebuild, schema fingerprint, protocol
  fingerprint, and compatibility declarations.

No database row, protobuf field, JSON document, trace, output, backup,
environment, command line, or settings file contains a secret, bearer header,
Credential Manager target, or reveal operation.

## 8. State and accounting model

### Credential profile

```text
pending-enrollment -> active-unverified -> active-verified
active-* -> replacing -> active-* (new generation)
active-* -> disabled
active-*|disabled|replacing -> delete-pending -> deleted
any nonterminal state -> secure-store-unavailable|recovery-required
```

Activation follows durable intent -> helper write/verify -> coordinator atomic
activation. Replacement always creates a generation and makes the predecessor
ineligible before deletion. Disable retains the item but closes dispatch.
Deletion increments revocation, closes every undispatched path, deletes exact
known targets, and never restores eligibility after failure.

### Provider operation

```text
proposed -> confirmed -> reserved -> assigned -> final-gate-authorized
  -> transport-not-started | transport-may-have-started
  -> response-staged -> admitted | rejected
  -> settled | unresolved-hold
```

Every transition is append-only and fenced. `transport-may-have-started`
forbids automatic retry and retains the full reservation until a qualified
receipt or explicit later reconciliation. Cancellation releases only a proven
undispatched reservation.

### Price and usage

- price values are exact rationals identified by provider/model/tier/context
  band/cache class/token class/tool class/region/currency/revision;
- local cost uses checked signed 64-bit nano-USD and component-wise upward
  rounding;
- dispatch/input/output/reasoning/cache-read/cache-write/priced-tool/nano-USD
  dimensions remain typed even where the accepted profile fixes a dimension
  to zero;
- provider receipt usage, local calculation, provider billing, rate headroom,
  provider spend history/limit, and credit are distinct authorities; and
- projections are rebuildable from immutable events and never own usage.

## 9. Fixture and expected-truth ownership

Slice 6 authors fresh public executable revisions for the accepted families:

- WP1: provider contract/state totality and answer-free examples;
- WP2: `M1-PLAT-PROVIDER-CAPABILITY-v1`,
  `M1-PLAT-PROVIDER-AUTHORITY-v1`, and `M1-PLAT-BUDGET-v1` deterministic
  development/validation variants;
- WP3: synthetic `M1-PLAT-CREDENTIAL-v1`, minimization credential extension,
  private-frame, and fake secure-store variants;
- WP4: native exact-target/write-boundary credential variants;
- WP5: `M1-PLAT-OFFLINE-v1`, hostile-content/privileged-boundary variants,
  adapter responses, and transport faults;
- WP6: fresh EVAL-0067 claim/no-model/hostile/provider-transcript slots and
  acquisition provenance variants;
- WP7: fresh EVAL-0067 candidate positive/matched-negative variants and
  EVAL-0083 local/deterministic/contradiction/deletion slots;
- WP9: the one qualification manifest shared by provider capability,
  authority, budget, and credential live extensions;
- WP10: `LLM-CLAIM-LIVE-VAL`; and
- WP11: `LLM-INVESTIGATE-LIVE-VAL` and `PROV-LIVE-COMPOSED-VAL`.

Expected values are authored and frozen independently before product
comparison. Product code/output does not author or revise them. A validation
case that drives a product, prompt, schema, or oracle change becomes
development evidence and requires a materially independent replacement. For a
live case, replacement also requires a new separately authorized live request;
there is no automatic correction rerun.

No held-out package is created or accessed. Existing catalog references to
future held-out packages remain deferred and unpassed.

## 10. Common execution and review rules

Before every package:

```powershell
git status --short --branch
git rev-parse HEAD
git merge-base --is-ancestor 5514919b8f742d00e59752fa7125da487a390926 HEAD
```

The implementer records branch, HEAD, ancestry, worktree state, prerequisite
record, and allowed paths before edits. Unrelated changes are preserved.

Every package follows implement -> focused tests -> semantic/diff/security
review -> correction -> rerun -> fresh re-review. There is no correction-pass
budget. Findings are classified as must-fix, follow-up, non-blocking,
owner/authority decision, or safety/isolation breach.

No ordinary `dotnet test`, solution run, default verifier, `All`, or
`NonLiveAll` gate may access Credential Manager or the network. Native
Credential Manager tests require an explicit `CredentialNative` gate and unique
test namespace. Live tests require the separate live commands and manifests
defined below.

The final non-live common floor is:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate All -OutputRoot <temporary-output-root>
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate Layer6Review -BaselineCommit <accepted-slice6-implementation-base> -CandidateCommit HEAD -OutputRoot <temporary-output-root>
git diff --check
```

`eng/verify-m1-slice6.ps1 -Gate NonLiveAll` must be provably network- and
credential-free. It never invokes a live or native credential gate.
`Layer6Review` is also inert and must emit retained reports for the exact
changed-path/allowed-path/protected-path scan, relative-link validation, strict
parsing of every changed JSON file, status/claim occurrence scan, unsupported/
gap inventory, and private/archive-path absence. `NonLiveAll` invokes the same
Layer 6 implementation and retains the same candidate-bound report; the direct
command above proves that obligation independently.

## 11. Work-package sequence

```text
M1/S6
├── WP1 Contracts, codecs, migration, and finite-bound policy
├── WP2 Capability, price, budget, settlement, and deterministic simulator
├── WP3 One-shot helper and synthetic credential lifecycle
├── WP4 Separately authorized native Credential Manager qualification
├── WP5 Exact Responses adapter, offline behavior, and transport safety
├── WP6 Source-claim acquisition and deterministic admission
├── WP7 Candidate investigation and retained provenance/replay
├── WP8 Accumulated non-live verification and pre-live review
├── WP9 Separately authorized live transport qualification
├── WP10 Separately authorized live source-claim extraction
└── WP11 Separately authorized live candidate investigation and closeout
```

WP1 through WP3 and WP5 through WP8 may advance automatically after owner
acceptance of this plan and acceptance of each prerequisite package. WP4,
WP9, WP10, and WP11 always stop for their stated fresh owner authorization.
WP9's acceptance does not authorize WP10; WP10's acceptance does not authorize
WP11.

## 12. `M1/S6/WP1` — Contracts, codecs, migration, and finite-bound policy

**Objective.** Close the Slice 6 product contracts, canonical codecs,
persistence model, answer-free examples, and conservative finite-input policy
before implementing credentials or transport.

**Inputs.** Accepted authority; RESEARCH-0054 owner disposition; schema-5 /
storage-`1.4.0` database; current operational value-contract and helper-proto
scaffolds; Slice 5 frozen contracts.

**Allowed paths/actions.** Domain contracts/invariants/canonicalization; JSON
schemas/codecs; protobuf contracts and protocol fingerprints; Persistence
migration/backup/restore/projections; additive non-secret Application query
contracts; run-output/CLI-summary references; public answer-free contract
examples; WP1 tests and `eng/verify-m1-slice6.ps1` contract gates.

**Prohibited.** Credential Manager calls; helper implementation; network;
provider SDK; prompts with semantic fixture content; semantic model execution;
live packages; private/legacy/later-slice work; weakening Slice 5 contracts.

**Vertical deliverables.** The nine schemas from section 7; completed domain and
wire contracts, including effective-scan-configuration v2 and evidence-
acquisition-run ownership; migration `M1-S6-0006` to schema 6/storage `1.5.0`; strict
round trips and invalid-state totality; schema/protocol fingerprint updates;
backup/restore and projection declarations; exact local tokenizer or proved
conservative input-bound policy; price-rule shape; answer-free examples; a
contract traceability inventory mapping every field to authority/producer/
consumer/persistence/output/replay. The existing local-only effective-scan
configuration v1 and publication overload remain byte-for-byte compatible;
provider-active runs use v2 plus an additive provider publication supplement.
The public fixture authority registry, registry schema, resealer, reader, count
assertions, and repository-boundary tests advance together for every new Slice
6 public package; discovery remains closed-world.

**Contract maturity.** New Slice 6 contracts become `Implementation-active`
only after WP1 acceptance. Slice 5 remains `Slice-frozen`.

**Focused verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderContract|FullyQualifiedName~ProviderFiniteBound|FullyQualifiedName~OperationalContract"
dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Provider|FullyQualifiedName~Helper|FullyQualifiedName~RunOutput"
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Schema6|FullyQualifiedName~ProviderPersistence|FullyQualifiedName~BackupRestore"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate Contracts -OutputRoot artifacts/m1-slice6/wp1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate StateTotality -OutputRoot artifacts/m1-slice6/wp1
```

The implementing package must create the named filters/gates as exact,
non-placeholder commands. They are network- and credential-free.

**Retained evidence.** Schema/protocol/storage identities and fingerprints;
migration origin/final fingerprints; schema-5 to 6 upgrade/backup/restore;
contract totality matrix; round trips; forbidden-field scan; input-bound proof;
traceability inventory; commands/counts/diff/review.

**Review.** Fresh contract/persistence reviewer checks closure across every
seam, migration safety, conservative bound, provider-independent truth, secret
structural absence, and no Slice 5 reinterpretation.

**Recoverable failures.** Schema/codec mismatch, missing consumer, weak bound,
stale protocol fingerprint, migration defect, test/fixture defect, or ordinary
review finding returns to correction and re-review.

**Escalation.** Accepted authority cannot determine a required semantic field;
finite local input proof cannot be made without another provider operation; a
Slice 5 incompatible change is necessary; or continuation would cross a
secret/private/protected/destructive/external-effect boundary.

**Unblocks.** `M1/S6/WP2`.

## 13. `M1/S6/WP2` — Capability, price, budget, settlement, and simulator

**Objective.** Implement immutable capability/price snapshots and the real
coordinator-owned atomic reservation/final-gate/usage-settlement path against a
deterministic non-network provider simulator.

**Prerequisites.** WP1 accepted; contracts remain Implementation-active.

**Allowed paths/actions.** Coordinator/Application/Persistence budget and
operation services; price catalog and rational arithmetic; deterministic
provider simulator; capability/authority/budget public fixtures and oracles;
concurrency/fault/projection/replay/output tests; WP2 verifier gates.

**Prohibited.** Credential Manager; real helper credential access; DNS/network;
live credentials/calls; alternate provider/mode; single-threaded shortcut;
expected truth in product inputs.

**Vertical deliverables.** Closed capability/price catalog for the exact M1
profile; explicit unavailable facts; request/operation/run/profile/account/
billing/global scope limits; atomic vector reservation; immediate dispatch
revalidation; known-undispatched release; ambiguous-start full hold/no retry;
complete/partial/unavailable/overrun settlement; rate/billing/credit separation;
rebuildable projections; deterministic Response/HTTP/caching/model/tier/error
matrix; non-live human/JSON projections.

**Primary cases.** EVAL-0076, EVAL-0077, and EVAL-0081 synchronous substrate.
Applicable extensions include EVAL-0026, EVAL-0038, EVAL-0040, EVAL-0082,
EVAL-0087, and EVAL-0088 for changed surfaces.

**Focused verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderCapability|FullyQualifiedName~PriceCatalog|FullyQualifiedName~Budget"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderReservation|FullyQualifiedName~DispatchFence|FullyQualifiedName~UsageSettlement"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderCapability|FullyQualifiedName~ProviderAuthority|FullyQualifiedName~AtomicBudget"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate Budget -OutputRoot artifacts/m1-slice6/wp2
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate BudgetFaults -OutputRoot artifacts/m1-slice6/wp2
```

**Retained evidence.** Fixture/oracle/registry identities; rational price
catalog; reservation/fence/usage events; concurrent commit outcomes; fault
schedule; overflow/rounding; unresolved holds; projection rebuild equality;
zero network/credential receipt; commands/counts/diff/review.

**Review.** Fresh budget/transaction reviewer checks multi-scope atomicity,
one ownership, all price classes, cache-off treatment, fault recovery, no
fallback/retry, and real SQLite path rather than a test-only shortcut.

**Recoverable failures.** Oversubscription, partial debit, arithmetic error,
projection drift, fixture defect, missing failure state, or ordinary review
finding returns to correction and re-review.

**Escalation.** A provider price/capability class cannot be finitely bounded;
accepted cost authority conflicts; or an unauthorized external/private/secret
effect would be required.

**Unblocks.** `M1/S6/WP3`.

## 14. `M1/S6/WP3` — One-shot helper and synthetic credential lifecycle

**Objective.** Implement the complete helper protocol, process boundary, and
recoverable credential lifecycle against a deterministic fake secure store and
provider simulator, without touching Credential Manager or any network.

**Prerequisites.** WP2 accepted.

**Allowed paths/actions.** CredentialHelper; coordinator launcher and
supervision; inherited anonymous pipe handles; fake secure store behind the
exact narrow credential interface; profile/generation/intent persistence;
deterministic simulator dispatch; helper/credential/minimization/IPC fixtures;
canaries, backup/restore, and crash/recovery evidence.

**Prohibited.** Any native credential API; credential enumeration/reveal;
arbitrary target; shell; command-line/environment secret; public network;
long-lived helper; production/shared credential; protected root; normal-suite
external effect.

**Vertical deliverables.** Private versioned helper protocol with fixed
little-endian length-prefix framing, maximum frame/message/staging sizes,
recursive unknown-field rejection, duplicate singular/conflicting-oneof
rejection, monotonic sequence numbers, exactly one bootstrap and assignment,
single operation, final generation/revocation/budget revalidation, stage-before-
admit response handling, and terminal shutdown. Launch uses the exact helper
binary plus inherited private handles only, with no stdin/stdout protocol,
listening socket, ambient secret, or retry. Synthetic lifecycle covers pending
enrollment, activation, replacement, disable/revoke/delete, unavailable store,
size limit, crash, stale dispatch, backup reauthentication, and recovery.

**Primary cases.** EVAL-0034, EVAL-0035, EVAL-0077, EVAL-0080, EVAL-0088, and
EVAL-0089. Applicable extensions include EVAL-0026, EVAL-0038, EVAL-0040,
EVAL-0045, EVAL-0046 for the exact subprocess operation, EVAL-0081, and
EVAL-0087.

**Focused verification.**

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Credential|FullyQualifiedName~Helper"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CredentialIntent|FullyQualifiedName~HelperPrivateHandle|FullyQualifiedName~CredentialDispatch"
dotnet test tests/Infinium.SecurityTests/Infinium.SecurityTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Credential|FullyQualifiedName~SecretCanary|FullyQualifiedName~HelperAuthority"
dotnet test tests/Infinium.FaultTests/Infinium.FaultTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Helper|FullyQualifiedName~Credential"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialSynthetic -OutputRoot artifacts/m1-slice6/wp3
```

**Retained evidence.** Non-secret intents/generations/revocation; helper binary
and protocol fingerprints; canonical frame corpus; unknown/duplicate/order/
limit rejection; private-handle and process tree proof; fault/restart matrix;
transport counts; backup/restore; canary scan; zero native/network receipt;
commands/counts/diff/review.

**Review.** Fresh credential/security/process reviewer inspects protocol
strictness, private handles, secret lifetime/logging, final revalidation,
staging, crash containment, no retry, and fake/native seam equivalence.

**Recoverable failures.** Protocol/codec mismatch, half-commit recovery defect,
stale dispatch, leak, helper crash, process cleanup defect, or ordinary review
finding returns to correction and re-review.

**Escalation.** A complete fake-store seam cannot be expressed without native
effects, helper authority must broaden, or continuing would cross a secret,
protected-root, private, or external-effect boundary.

**Unblocks.** `M1/S6/WP4` and `M1/S6/WP5` independently.

## 15. `M1/S6/WP4` — Separately authorized native Credential Manager qualification

**Objective.** Qualify the exact Windows Credential Manager implementation and
cleanup protocol in one owner-authorized disposable namespace, without provider
network access.

**Entry gate.** WP3 accepted; clean exact candidate commit; the owner explicitly
accepts a manifest binding the unique test target derivation, allowed native
calls, no-enumeration rule, entry/cancel flow, backup/restore plan, cleanup and
absence proof, deadline, and canaries. General Slice 6 plan acceptance is not
native-effect authority.

**Allowed actions.** `CredWriteW`, exact-target `CredReadW`, exact-target
`CredDeleteW`, and `CredFree` through the narrow wrapper; helper-owned non-
echoing credential entry/cancel; fake-provider dispatch only; exact-target
cleanup and post-cleanup absence verification.

**Prohibited.** Credential enumeration; reveal/log/artifact secret; arbitrary,
production, shared, or pre-existing target; provider DNS/network; alternate
credential mechanism; implicit normal-suite invocation; namespace reuse after
uncertain cleanup.

**Live-local command.**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CredentialNative -AuthorizationManifest <owner-accepted-wp4-manifest> -OutputRoot artifacts/m1-slice6/wp4-native
```

The gate refuses stale/missing/mismatched authority and attempts bounded exact-
target cleanup on every terminal path. Cleanup uncertainty is visible and
blocks target reuse.

**Pass evidence.** Exact allowed-call oracle; target fingerprint rather than raw
target; lifecycle/generation receipts; entry/cancel, size-limit, unavailable-
store, replacement, revoke/delete, crash/restart, backup/restore results; secret
canaries; zero network; cleanup and exact-target absence proof.

**Review.** Fresh Windows credential/security reviewer inspects interop,
marshalling/freeing, exact target derivation, no enumeration/fallback, memory/
logging limits, UI ownership, fault paths, and cleanup.

**Failure rule.** Ordinary native wrapper, fixture, cleanup implementation, or
review defects return to correction and require a fresh manifest before another
native mutation. Any real secret exposure, unknown target/effect, unbounded
cleanup, or effect outside the manifest is a safety escalation.

**Unblocks.** The native prerequisite of `M1/S6/WP8`; it does not authorize a
provider request.

## 16. `M1/S6/WP5` — Exact Responses adapter, offline behavior, and transport safety

**Objective.** Implement the exact cache-off/stateless Responses codec and
one-shot HTTP transport using only deterministic loopback servers/simulators,
with offline and adversarial boundary proof.

**Prerequisites.** WP3 accepted; exact profile owner disposition recorded.

**Allowed paths/actions.** `Infinium.OpenAI`; helper transport branch;
coordinator request construction/admission; loopback deterministic HTTP
fixture under closed test authority; offline/network spies; hostile/minimized
contexts; response/refusal/incomplete/error/header/usage codecs; retained raw
payload staging/replay; adapter/security fixtures and verifier gates.

**Prohibited.** Public DNS or provider endpoint; API key; live request; provider
SDK; arbitrary URL/header; redirect/retry/proxy fallback; provider tools;
token-count endpoint; expected labels; source refresh; Slice 7 work.

**Vertical deliverables.** Canonical byte serializer; exact host/path/method and
header allowlist; BCL `HttpClient` transport; single-send semantics; finite
request/response/deadline bounds; profile/capability validation; strict output
and total typed non-success states; request/response/client-request/rate header
provenance; cache/reasoning/tier/model drift checks; explicitly unavailable
account/billing identities when absent from receipts; offline availability;
retained-response replay with network disabled; secret-free diagnostics.

**Primary cases.** EVAL-0033 through EVAL-0035, EVAL-0064, EVAL-0076,
EVAL-0077, and EVAL-0089 transport substrate. Applicable extensions include
EVAL-0026, EVAL-0037, EVAL-0038, EVAL-0045, EVAL-0080 through EVAL-0083,
EVAL-0087, and EVAL-0088.

**Focused verification.**

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~OpenAi|FullyQualifiedName~Responses|FullyQualifiedName~ContextMinimization"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderAdapter|FullyQualifiedName~ProviderOffline|FullyQualifiedName~RetainedResponseReplay"
dotnet test tests/Infinium.SecurityTests/Infinium.SecurityTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderBoundary|FullyQualifiedName~PromptInjection|FullyQualifiedName~SecretCanary"
dotnet test tests/Infinium.FaultTests/Infinium.FaultTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~ProviderTransport|FullyQualifiedName~AmbiguousDispatch"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate Adapter -OutputRoot artifacts/m1-slice6/wp5
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate OfflineSafetyReplay -OutputRoot artifacts/m1-slice6/wp5
```

**Retained evidence.** Exact serialized requests/fingerprints without secrets;
capability/price/profile fingerprints; simulator transcripts; response-state
matrix; network spy; redirect/retry proof; cache/reasoning/model/tier drift;
replay equality; canaries; commands/counts/diff/review.

**Review.** Fresh provider/security reviewer checks exact bytes, current
official-doc alignment, stateless/cache-off fields, no hidden SDK behavior,
host/DNS/TLS/proxy/redirect/retry policy, response totality, ambiguous start,
and retained replay.

**Recoverable failures.** Codec drift, missing response state, overbroad
network surface, implicit retry, malformed-fixture defect, or ordinary review
finding returns to correction/re-review without live operation.

**Escalation.** Current provider schema rejects an accepted exact field;
required endpoint/security behavior conflicts with accepted authority; finite
response/usage semantics cannot be closed; or any external transport occurs.

**Unblocks.** `M1/S6/WP6`.

## 17. `M1/S6/WP6` — Source-claim acquisition and deterministic admission

**Objective.** Implement source-claim extraction over project-authored public
inputs and admit its untrusted proposals through the frozen Slice 5 path using
deterministic retained provider transcripts only.

**Prerequisites.** WP5 accepted.

**Allowed paths/actions.** Source-claim execution-input and output schemas/
codecs; versioned prompt; context minimizer; evidence-acquisition orchestration;
Slice 5 validation/application links; additive provider publication supplement;
operation query/output/replay; public development/validation fixtures and
independently authored harness-only oracles.

**Prohibited.** Credential Manager; API key/network/live call; token-count
endpoint; model expected answers; source/Nexus/search refresh; model-created
facts/authority; finding/case/grouping/threshold/taxonomy changes; private or
held-out fixture; mutation of Slice 5 frozen contracts or local-only v1 output.

**Vertical deliverables.** Source-claim prompt/schema revision; answer-free
minimized input; deterministic valid positive, unsupported negative,
conditional/version-scoped, contradiction, abstention, empty, hostile,
malformed, refusal, incomplete, deleted, and drift transcripts; host citation/
identity/schema validation; accepted/rejected proposal retention; an owning
evidence-acquisition run for extraction, calls, claims, and cost; explicit
links from later analysis-run application/rollups; no-model path; human/JSON
transparency; retained replay and audit-only degradation.

**Primary cases.** EVAL-0067 and EVAL-0083. Applicable extensions/regressions
include EVAL-0033 through EVAL-0035, EVAL-0037, EVAL-0039, EVAL-0040,
EVAL-0045, EVAL-0064, EVAL-0082, EVAL-0084, and EVAL-0085 claim control.

**Focused verification.**

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~SourceClaimExtraction|FullyQualifiedName~ProviderContext"
dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~SourceClaimExtraction|FullyQualifiedName~ProviderProvenance"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~SourceClaimAdmission|FullyQualifiedName~SourceClaimReplay"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~LlmClaimTransparency|FullyQualifiedName~Slice5ProviderAdmission"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate SourceClaimSemantics -OutputRoot artifacts/m1-slice6/wp6
```

**Retained evidence.** Prompt/schema/request/profile identities; fixture/oracle/
registry revisions and partition history; minimized-context manifest; transcript/
response/proposal/admission/replay hashes; positive/negative/abstention/failure
results; acquisition/application provenance; forbidden-authority scan; Slice 5
regression; commands/counts/diff/review.

**Review.** Fresh source-claim/provenance reviewer begins answer-isolated from
harness oracles, checks product inputs/code and acquisition ownership, then
separately compares typed outputs with frozen public expectations.

**Recoverable failures.** Prompt/schema mismatch, weak minimization, admission
defect, missing contradiction/gap, fixture/oracle defect, replay drift, or
ordinary review finding returns to correction and re-review. A validation
expectation that drove correction is independently replaced.

**Escalation.** Slice 5 authority cannot express a required transition; new
product meaning is needed; answer isolation cannot be preserved; or a private,
live, source-refresh, or other external effect would be required.

**Unblocks.** `M1/S6/WP7`.

## 18. `M1/S6/WP7` — Candidate investigation and retained provenance/replay

**Objective.** Implement candidate investigation, positive/matched-negative
behavior, and composed provenance over deterministic retained transcripts.

**Prerequisites.** WP6 accepted.

**Allowed paths/actions.** Candidate-investigation schemas/codecs/prompt and
minimized context; candidate/hypothesis/evidence/contradiction links; Slice 5
admission through the provider supplement; public fixtures/oracles; retained
replay; cross-operation provenance and human/JSON output.

**Prohibited.** Credential Manager; network/live call; token-count endpoint;
source/Nexus/search refresh; expected truth in product input; automatic finding,
case, grouping, threshold, or taxonomy authority; private/held-out fixture;
Slice 7 behavior.

**Vertical deliverables.** Candidate positive and matched negative in one
operation input; conditional, unsupported, contradiction, abstention, hostile,
malformed, refusal, incomplete, deleted, and drift states; accepted/rejected
proposal retention; candidate/hypothesis and source-acquisition links; complete
raw-intermediate provenance; replay equality; no-model and unavailable-provider
output; no readiness/reliability/private-evaluation claim.

**Primary cases.** EVAL-0067 and EVAL-0083. Applicable extensions/regressions
include EVAL-0037, EVAL-0039, EVAL-0040, EVAL-0045, EVAL-0064, EVAL-0082,
EVAL-0084, and EVAL-0085 claim control.

**Focused verification.**

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidateInvestigation|FullyQualifiedName~ProviderContext"
dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidateInvestigation|FullyQualifiedName~ProviderProvenance"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidateAdmission|FullyQualifiedName~ProviderReplay"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidateLlmTransparency|FullyQualifiedName~ProviderProvenance"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate CandidateSemantics -OutputRoot artifacts/m1-slice6/wp7
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate ProvenanceReplay -OutputRoot artifacts/m1-slice6/wp7
```

**Retained evidence.** Prompt/schema/request/profile identities; public fixture/
oracle/registry identities; transcript/proposal/admission/replay hashes; positive/
matched-negative/abstention/failure results; composed provenance; forbidden-
authority and Slice 5 regression; commands/counts/diff/review.

**Review.** Fresh candidate/provenance reviewer uses the same answer-isolated
two-stage review pattern as WP6 and checks matched-negative construction,
complete raw intermediates, claim boundaries, replay, and gaps.

**Recoverable failures.** Same correction/re-review and independent validation-
replacement rules as WP6.

**Escalation.** Same authority, isolation, and external-effect boundaries as
WP6.

**Unblocks.** The deterministic prerequisite of `M1/S6/WP8`.

## 19. `M1/S6/WP8` — Accumulated non-live verification and pre-live review

**Objective.** Prove the complete non-live Slice 6 candidate, close every
applicable Layer 1-4/6 obligation, and prepare—but not execute—the exact live
authorization packets.

**Prerequisites.** WP4 and WP7 accepted; all public development/validation
fixtures frozen, independently reviewed, and registered; native test namespace
clean.

**Allowed paths/actions.** Whole-repository non-live test/review; fixture and
registry validation; migration/backup/replay/fault/canary verification; review
of the planning-approved official-doc snapshot; authorization-manifest
templates without secrets; implementation record through pre-live checkpoint;
corrections and re-review. Any internet-based official-doc refresh is a separate
planning/research action completed before candidate freeze, never a verifier
side effect.

**Prohibited.** Credential Manager mutation except a freshly reauthorized WP4
gate; DNS/network/provider request; API key; live manifest execution; private,
legacy, or later-slice work; self-acceptance.

**Required case inventory.** Primary: EVAL-0033 through EVAL-0035, EVAL-0064,
EVAL-0067, EVAL-0076, EVAL-0077, EVAL-0081 synchronous, EVAL-0083, EVAL-0089.
Applicable extensions: EVAL-0026, EVAL-0037 through EVAL-0040, EVAL-0045,
EVAL-0046 for exact native/subprocess operations, EVAL-0080, EVAL-0082,
EVAL-0087, and EVAL-0088. EVAL-0084/0085 receive regression and claim review.
Every N/A receives explicit accepted rationale.

**Verification.** Run every non-live WP command, then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate NonLiveAll -OutputRoot artifacts/m1-slice6/wp8
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate All -OutputRoot <temporary-output-root>
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate Layer6Review -BaselineCommit <accepted-slice6-implementation-base> -CandidateCommit HEAD -OutputRoot <temporary-output-root>
git diff --check
```

`NonLiveAll` and every ordinary/default/`All` gate fail closed if any network,
Credential Manager, or live-script invocation is reachable. The verifier emits
a content-bound zero-external-effect receipt.

**External-effect packet templates.** Prepare four distinct non-secret
manifests: one for production access-profile enrollment/verification and one
for each of the three provider requests. The credential manifest binds owner
authorization identity, candidate commit, provider/purpose/account/scope intent,
new or exact existing profile identity, generation/revocation expectations,
allowed native calls, entry/cancel, persistence/delete intent, deadline, and
canaries. Each request manifest additionally binds billing disclosure, exact
request/prompt/schema, fixture/oracle, capability/price snapshots, input/output/
call/nano-USD/deadline limits, request fingerprint, and no-retry rule. Values
are never inherited across effects or packages.

**Retained evidence.** Complete commands/results/counts/skips; case and
requirement matrix; fixture/oracle/registry identities; migration/replay/fault/
canary/effect receipts; official-doc snapshot; gaps/N/A reasons; review inputs,
findings, corrections, and final judgments; pre-live record and packets.

**Review.** Fresh independent contract/persistence, budget, credential/helper,
provider-adapter, semantic/provenance, and overall diff/claim reviewers inspect
the exact candidate and close must-fix findings through re-review.

**Acceptance.** WP8 accepts only the non-live candidate and qualification
packet readiness. It does not qualify the provider or authorize dispatch.

**Escalation.** Unresolved authority conflict; secret/private/protected effect;
unbounded provider dimension; inability to clean the native namespace; or a
live assertion is required to claim a non-live pass.

**Unblocks.** Owner decision whether to authorize `M1/S6/WP9`.

## 19A. Accepted finite campaign amendment — 2026-08-15

This clause is an explicit, one-campaign amendment to Sections 20–22. It does
not reinterpret their original separate-authorization language. The immutable
owner authority input is
`m1-slice6-finite-campaign-owner-authority.v1.json`, whose source attachment
SHA-256 is
`c9541bb5563304335e8f7af4d176eba3e507c719c4e135c542b8ac1bc4bc12be`.
It authorizes preparation and exact review of one schema-closed campaign and a
pre-effect semantic rollover. It does not pre-accept unknown future bytes or
create an owner marker for a replacement credential manifest.

The campaign order is fixed: WP9 qualification, WP10 source-claim extraction,
then WP11 candidate investigation. Each stage has one possible provider start;
the campaign maximum is three sequential starts, three DNS resolutions,
147,456 canonical request bytes, 167,936 locally admitted input tokens, 8,448
output tokens, 2,359,296 raw response bytes, and 1,340,000,000 reserved
nano-USD. Automatic retry, parallel dispatch, counter reset, alternate host,
path, model, credential, or provider, and a fourth request are prohibited. The
per-stage deadlines and ceilings remain the exact table in Section 5.

The credential envelope remains byte-semantically unchanged. Its expiry stays
`2026-08-17T15:25:00.0000000Z`; the provider campaign expires at
`2026-08-22T23:59:00.0000000Z`. An action must begin strictly before its
applicable expiry. A started operation may retain and adjudicate evidence after
expiry, but expiry never creates retry authority.

Semantic rollover is permitted only while every credential helper launch,
readiness, authority-lock, native call, profile materialization, DNS/public
network, and provider-dispatch count is zero. The exact credential manifest
may change only its candidate/build binding fields; its credential envelope,
target/profile/generation, UX, call grammar, durable lifecycle, outputs, stop
conditions, and expiry must compare field-by-field equal. A ceiling may only
decrease. Credential rollover closes permanently at the first helper launch,
readiness observation, authority-lock creation, or native call. Provider
rollover closes at the durable possible-start latch; a known or possible start
consumes its stage, retains the full unresolved hold when ambiguous, and may
never be retried.

The coordinator owns an append-only, hash-chained campaign ledger with exact
ready, reviewed, admitted, credential-handoff, credential-evidence-handoff,
credential-evidence-accepted, stage-reserved, transport-may-have-started,
stage-settled, stage-evidence-handoff, stage-accepted, completed, and
stopped states. Enrollment plus the three exact request credential reads yield
the finite native maximum `CredWriteW=1`, `CredReadW=5`, `CredDeleteW=0`,
`CredFree=4`, total 10, with every successful read paired to its allocation's
free. Collision, cancel, readiness failure, native ambiguity, provider
ambiguity, a stale/duplicate marker, or counter/hold inconsistency stops the
campaign without fallback.

Provider execution opens only the exact WP9 authoritative product-state
database and requires its already accepted active-verified profile,
generation, account, billing scope, capability, and price identities. It may
never fabricate enrollment, verification, or a replacement projection. Each
stage persists its operation, reservation, canonical request, raw response,
headers, usage, settlement, and network-disabled replay through the accepted
SQLite provider-accounting graph. A pre-start failure is released
undispatched. A known settled response remains settled with zero retry even if
later semantic or sidecar evidence cannot be reviewed; reopening reconciles
that exact SQLite settlement into a terminal known-settled ledger state. Only
a genuinely unknown possible start retains the full unresolved hold.

The application creates one stable per-product-user safety seed using 32 bytes
from the operating-system cryptographic RNG and atomic create-new local state.
The canonical Responses body carries only lowercase-hex SHA-256 of UTF-8
`infinium.openai.safety-identifier/v1`, a NUL framing byte, and that seed. No
credential, target/profile/generation, provider account/email, OS user/machine,
source/prompt, file/mod, advertising, or telemetry identity may contribute.
Missing/corrupt state fails closed and may never be silently regenerated after
possible start. Before the first possible start, the coordinator atomically
persists a versioned use latch containing the exact transmitted projection;
the campaign ledger retains the same projection. Every later stage requires
both bytes to reopen and agree. Missing, corrupt, torn, deleted, or changed
seed/use-latch bytes after use terminally stop the campaign rather than create
a replacement identity.

Bootstrap is exact: freeze the amendment implementation A; materialize the
campaign manifest C; obtain fresh independent review; append the distinct
campaign admission derived from the immutable owner authority; then roll the
B20 credential manifest to A only through a distinct campaign-rollover marker
after exact zero-effect and non-broadening comparison. This marker is not
`WP9_PROFILE_OWNER_ACCEPTANCE`. Provider request manifests remain
unmaterialized until the credential succeeds and its evidence is independently
accepted. WP10 remains unmaterialized until WP9 live evidence is independently
accepted, and WP11 remains unmaterialized until WP10 live evidence is
independently accepted. Semantic-driving bytes freeze at possible start;
evidence-only corrections may replay retained bytes without another request.
Credential evidence, each stage evidence set, and the composed no-fourth-call
evidence each require a distinct exact-SHA independent-acceptance marker in a
one-commit, three-document, append-only transition. Dedicated mutually
exclusive `M1Slice6CampaignCredentialEvidenceCloseout`,
`M1Slice6CampaignStageEvidenceCloseout`, and
`M1Slice6CampaignComposedEvidenceCloseout` Layer 6 modes validate those
transitions; a review marker never performs the acceptance transition itself.

Before any effect, a fresh temporary Git clone must rehearse ready -> reviewed
-> campaign admitted -> credential execution/evidence handoff -> all three
fake-store/literal-loopback stages -> composed closeout, plus every expiry,
identity, duplicate marker, broadened envelope, raised limit, counter reset,
post-latch rollover, ambiguous-start, safety-state drift/loss, host/path/tool,
retry, and fourth-call stop. The rehearsal and every verifier mode have zero
native/provider effect. Finite Layer 6 modes are mutually exclusive. Only an
exactly reviewed and admitted campaign may reach the real credential dialog.

The rehearsal and the `LiveEvidence`, `RetainedReplay`, and
`ComposedProvenance` gates execute the exact frozen WP10/WP11 public validation
package inputs and deterministic product oracles. Canonical Responses bytes
bind both the exact product input and an answer-free request-template hash.
WP11 must consume the exact admitted WP10 artifact and persisted application
link; it may not synthesize a parallel source graph. Offline validation opens
the hash-chained ledger and authoritative SQLite store independently, replays
the exact retained raw response and headers with network disabled, validates
native/canary evidence and stage/composed markers, and binds the resulting
semantic application, evidence, candidate, and hypothesis identities. The
active repository does not materialize stage manifests or markers during this
non-live rehearsal.

## 20. `M1/S6/WP9` — Campaign-gated live transport qualification

**Objective.** Send exactly one deliberately tiny request through the final
production path to qualify transport/profile/capability/price/credential/
budget/settlement behavior. The response is not semantic evidence.

**Entry gate.** WP8 accepted; clean exact candidate commit; fresh official-doc
drift check; and native test namespace clean. Outside the finite campaign, the
owner accepts the exact production-profile enrollment/verification manifest.
Inside the finite campaign, the exact committed campaign review, owner-derived
campaign admission, and candidate-scoped credential-rollover admission replace
that separate marker only after the field-by-field zero-effect comparison. A
general instruction to implement Slice 6 is insufficient.

**Production-profile sub-gate.** Before request reservation, run exactly one
explicit enrollment-or-verification operation. For a new profile, the helper
owns non-echoing secret entry and exact-target write; for an existing profile,
it performs only the manifest-authorized exact-target read/verification. The
sub-gate persists non-secret intent and a new verified generation, or a typed
cancel/unavailable/failure state. It sends no network request and cannot open
provider dispatch. Persistence or deletion of the accepted credential follows
the manifest; a partial or ambiguous native effect blocks request execution
until recovered under fresh authority.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/run-m1-slice6-credential.ps1 -Operation EnrollOrVerifyProfile -AuthorizationManifest <exact-owner-accepted-or-campaign-rollover-admitted-production-profile-manifest> -OutputRoot artifacts/m1-slice6/wp9-profile
```

After that sub-gate is independently accepted, materialize the distinct
qualification manifest with the resulting exact profile/generation/revocation
identity and repeat the candidate/doc/price/capability drift check. Under the
finite campaign amendment only, exact campaign admission supplies the bounded
owner authority; the stage still requires fresh exact-byte review and accepted
predecessor evidence. Outside that campaign, the original separate owner
acceptance requirement remains unchanged. The request remains closed until
then.

**Allowed request action.** Only after the sub-gate produces the exact verified
generation: exactly one direct synchronous request described by its distinct
manifest through coordinator -> SQLite reservation -> helper exact credential
read -> final revalidation -> Responses -> staging -> admission -> settlement.

**Prohibited.** Any unlisted/preflight/token-count/admin/retry/repair/semantic
request; changing code/prompt/schema/oracle while retaining validation status;
fallback; second credential/model/account/provider; private fixture.

**Live request command.** The implementation exposes a separate command that
refuses stale/missing/mismatched profile or request authority:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/run-m1-slice6-live.ps1 -Operation Qualification -AuthorizationManifest <owner-accepted-wp9-manifest> -OutputRoot artifacts/m1-slice6/wp9-live
```

No ordinary verifier calls this script.

**Pass evidence.** Exact enrollment/verification and request disclosures/
confirmations; non-secret profile intent/generation/lifecycle receipt; one
dispatch;
request/response IDs and fingerprints; requested/returned model/tier; effective
reasoning/cache settings; zero tool/cache use; usage/rate facts; local cost;
reservation/settlement; secret/target absence from all artifacts; retained-response replay with
network disabled; capability/profile drift result; EVAL-0076/0077/0081/0089
live assertions.

**Failure rule.** A cancelled/failed/ambiguous profile sub-gate sends no request.
Known-undispatched request failure may close with released reserve.
Possible transport start retains the full unresolved hold and forbids retry.
Any code/prompt/schema/oracle correction after dispatch requires WP8 re-review
and a fresh owner authorization before a replacement qualification; it is not
an automatic rerun.

**Review.** Fresh credential/provider/security/budget reviewer compares both
effects to their distinct manifests and returns `ACCEPT`, `CORRECT` only for
non-live evidence/implementation work, or `ESCALATE`. The reviewer cannot
self-authorize a new credential effect or request.

**Unblocks.** Outside the exact finite campaign, an owner decision whether to
authorize `M1/S6/WP10`; never automatic. Inside the exact admitted campaign,
only independently accepted WP9 live evidence, a fresh exact WP10 stage
manifest/review, and the coordinator ledger's legal next-stage admission can
unblock WP10. That finite transition consumes the campaign's preaccepted bound;
it is not inherited authority, an automatic retry, or reviewer self-approval.

## 21. `M1/S6/WP10` — Separately authorized live source-claim extraction

**Objective.** Send exactly one bounded source-claim-extraction request over an
independently adjudicated project-authored public validation package and prove
typed proposal/admission behavior.

**Entry gate.** WP9 accepted; `LLM-CLAIM-LIVE-VAL` inputs and harness-only
expectations frozen and independently reviewed before product comparison;
fresh exact candidate/doc/profile/price/capability check; and exact WP10 bytes
accepted under either the original separate owner acceptance or the admitted
finite campaign plus its fresh stage review.

**Allowed action.** Exactly one request containing the bounded positive,
negative/unsupported, conditional/version-scoped, contradiction, and hostile-
instruction passages defined by the accepted package and schema.

**Prohibited.** Expected answers; source refresh/search/Nexus; second request;
retry/repair; prompt/schema/oracle edit after seeing output while retaining
validation status; finding/case authority; private fixture.

**Live command.**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/run-m1-slice6-live.ps1 -Operation SourceClaimExtraction -AuthorizationManifest <owner-accepted-wp10-manifest> -OutputRoot artifacts/m1-slice6/wp10-live
```

**Pass evidence.** One owned operation/reservation/response/settlement with a
completed, schema-valid semantic response; exact citations and source bindings;
contradictions/abstentions retained; no authority promotion; host validation/
application; canaries; passing typed oracle result; retained-response replay;
EVAL-0067/0083 live assertions. Refusal, incomplete, error, malformed, or empty
output is retained as its typed non-success state but cannot pass WP10.

**Failure/replacement.** Ambiguity keeps the hold and closes WP11. A result that
drives code/prompt/schema/oracle change becomes development evidence. Outside
the campaign, a new materially independent validation package, WP8 re-review,
and separate owner-authorized request are required. Inside the campaign, the
possible-start latch consumes the stage and the campaign stops; campaign
authority cannot repair, retry, roll over, or replace a post-start stage.

**Review.** Fresh source-claim, provider, and provenance reviewers inspect
answer isolation, exact manifest, semantic transitions, settlement, replay,
and claim wording. Outside the campaign they cannot authorize WP11. Inside the
campaign their exact accepted evidence is one predecessor fact; only the
already admitted finite campaign plus the legal coordinator-ledger transition
and fresh WP11 stage review can advance.

**Unblocks.** Outside the campaign, owner decision whether to authorize
`M1/S6/WP11`; never automatic. Inside the campaign, exact independently
accepted WP10 evidence and the fresh exact WP11 stage review may consume the
third and final campaign stage through the coordinator ledger; no fourth call,
retry, or inherited authority exists.

## 22. `M1/S6/WP11` — Separately authorized live candidate investigation and closeout

**Objective.** Send exactly one bounded candidate-investigation request
containing both the accepted positive and matched negative, then assemble the
three-operation provenance graph and close Slice 6 without another request.

**Entry gate.** WP10 accepted; `LLM-INVESTIGATE-LIVE-VAL` positive/negative and
`PROV-LIVE-COMPOSED-VAL` expectations frozen/independently reviewed; fresh
candidate/doc/profile/price/capability check; and exact WP11 bytes accepted
under either the original separate owner acceptance or the admitted finite
campaign plus its fresh stage review.

**Allowed action.** One candidate-investigation request through the exact
production path, followed by local admission, retained replay, composed
provenance, accumulated non-live regression, record completion, and review.

**Prohibited.** A fourth provider request; qualification-response semantic use;
retry/repair; alternate candidate/model/key/provider; expected answer; finding/
case automatic authority; private/controlled-real/Slice 7 work.

**Live command.**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/run-m1-slice6-live.ps1 -Operation CandidateInvestigation -AuthorizationManifest <owner-accepted-wp11-manifest> -OutputRoot artifacts/m1-slice6/wp11-live
```

**Closeout verification.** After the live result is retained, disable network
and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate LiveEvidence -InputRoot artifacts/m1-slice6 -OutputRoot artifacts/m1-slice6/wp11-review
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate RetainedReplay -InputRoot artifacts/m1-slice6 -OutputRoot artifacts/m1-slice6/wp11-review
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate ComposedProvenance -InputRoot artifacts/m1-slice6 -OutputRoot artifacts/m1-slice6/wp11-review
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice6.ps1 -Gate NonLiveAll -OutputRoot artifacts/m1-slice6/wp11-review
```

Then rerun the entire common command floor. None of these commands may send a
provider request or touch Credential Manager.

**Pass evidence.** Candidate/hypothesis/abstention positive and matched-
negative result; exact evidence and contradiction bindings; one operation/
reservation/response/settlement; host admission; canaries; retained replay;
three distinct authorizations/requests/receipts/settlements; composed
provenance with qualification marked non-semantic; no Nexus/search; no fourth
call; all gaps and unsupported modes; final implementation/review record.

**Failure/replacement.** Same as WP10. A validation-driving change requires an
independent replacement and new owner-authorized live request; ambiguity never
permits retry. Local record/replay/review defects return to correction without
another request when retained evidence is sufficient.

**Review.** Fresh candidate-analysis, provenance, budget/security, Slice 5
boundary, and overall diff/claim reviewers accept the exact corrected
candidate. At least one final reviewer begins from fresh context and verifies
the three request count from immutable operation/transport evidence.

**Contract maturity and handoff.** After all review findings close, the owner
may accept Slice 6 and advance every Slice-6-owned contract, schema, protocol,
configuration, publication/output, and semantic-operation identity from
Implementation-active to `Slice-frozen`. Any identity lacking complete
producer/consumer/persistence/wire/output/replay/fixture evidence remains
explicitly Implementation-active and blocks Slice 6 acceptance; it is not
silently handed forward. The only successor eligibility is Slice 7 planning;
no Slice 7 implementation is authorized automatically.

## 23. Requirement and evaluation traceability

| Slice 6 surface | Requirements/ADRs | Primary cases | Applicable extensions/regressions |
|---|---|---|---|
| Exact provider profile and offline behavior | AI-001 through AI-007, OPS-001, ADR-0013/0025 | EVAL-0064, EVAL-0076, EVAL-0077 | EVAL-0037, EVAL-0045, EVAL-0082 |
| Context and authority isolation | SEC-001 through SEC-004, AUTH-002, ADR-0001/0018/0021 | EVAL-0033 through EVAL-0035 | EVAL-0039, EVAL-0080, EVAL-0088 |
| Credential lifecycle/helper | AUTH-002, SEC-002/004, AI-004/007, ADR-0018 through ADR-0021 | EVAL-0034, EVAL-0077, EVAL-0089 | EVAL-0038, EVAL-0045/0046, EVAL-0080/0087/0088 |
| Budget and settlement | SCAN-003 through SCAN-005, AI-004/005, ADR-0023 | EVAL-0076, EVAL-0077, EVAL-0081 | EVAL-0026, EVAL-0038, EVAL-0082/0087 |
| Typed semantic proposals | EVID-001/004/007, OPS-002, ADR-0001/0002/0013 | EVAL-0067 | EVAL-0033, EVAL-0039, EVAL-0084/0085 review |
| Provenance and replay | EVID-002, SNAP-005/006, AI-006, ADR-0002/0015/0025 | EVAL-0083 | EVAL-0026, EVAL-0037/0040, EVAL-0087 |

Layer 5 is not claimed by Slice 6. Every applicable changed-surface assertion
uses Layers 1-4 and 6; unchanged Slice 5 behavior remains accumulated
regression evidence rather than a new Slice 6 ownership claim.

## 24. Implementation record requirements

Create `docs/plans/milestones/m1/slices/s6/record.md` only when implementation
is authorized. It records, append-only:

- plan/authority/branch/base/candidate/ancestry and changed paths;
- package entry gates and acceptance decisions;
- contracts/schemas/protocol/storage/migration/fingerprints;
- fixture/oracle/registry revisions, partition transitions, and answer-
  isolation reviewers;
- exact commands and pass/fail/skip counts;
- provider-doc snapshots and capability/price/profile fingerprints;
- native credential namespace authorization, canary and cleanup receipts;
- all non-live effects and gaps;
- each live authorization separately, exact maximum bounds, request identity,
  transport count, receipt, usage, settlement, replay, and reviewer;
- every correction/re-review cycle and finding classification;
- no private access/held-out verdict and no archive use;
- final claims, unsupported modes, residual risks, implementation commit, and
  owner acceptance.

Secrets, raw credential targets, bearer headers, and private answers never
enter the record.

## 25. Copy-paste implementer prompt

```text
You are the implementer for Infinium {WORK_ID}.

Work only from the live repository and accepted authority. Read AGENTS.md,
docs/README.md, docs/current-state.md, docs/execution-policy.md, the accepted
M1 plan, continuation verification profile, Slice 5 current summary and exact
handoff boundary, the accepted Slice 6 plan in full, its prerequisite package
record, RESEARCH-0054 owner disposition, and the package-specific accepted
ADRs/evaluation specifications. Verify branch, HEAD, worktree, and that
5514919b8f742d00e59752fa7125da487a390926 is an ancestor before editing.

Implement the entire {WORK_ID} vertical scope exactly as planned. Preserve
Slice 5 frozen contracts and claim boundaries. Update every producer,
consumer, persistence, wire/query, output, replay, fixture, test, and document
seam affected by a clean-break contract change. Use public independently
expected development/validation fixtures only; product output never authors
truth.

Do not access private fixtures or any legacy/evaluator archive. Do not perform
live/billable/provider or Credential Manager operations unless this exact work
package has the separately required owner authorization manifest. Never run a
live operation implicitly, retry an ambiguous dispatch, expose a secret/target,
use a fallback provider/model/key, or push.

Run every focused command and review required by the package. Inspect semantic
correctness and the diff after tests pass, correct all must-fix findings, rerun,
and obtain fresh re-review. Append exact evidence to the Slice 6 implementation
record. Stop only for a genuine plan-defined escalation and report the affected
path while continuing independent in-scope work where possible.
```

## 26. Copy-paste independent reviewer prompt

```text
You are the fresh independent reviewer for Infinium {WORK_ID} at exact
candidate {COMMIT}.

Remain read-only. Read AGENTS.md, the core entry documents, live current state,
accepted M1 and Slice 6 plans, continuation verification profile, Slice 5
handoff, prerequisite/package record, relevant accepted ADRs, evaluation
specifications, and public fixture authority. Verify branch/commit/ancestry and
changed paths. Do not access private fixtures or archives and do not make any
provider, Credential Manager, protected-state, destructive, or billable call.

Review implementation and tests, then independently check product semantics,
contract closure across producer/consumer/persistence/wire/output/replay,
fixture answer isolation, finite bounds, credential/helper/process/network
authority, budget/settlement ownership, fault recovery, provenance, Slice 5
frozen boundaries, plan drift, and claim wording. Do not infer completion from
green tests.

Classify every finding as must-fix, follow-up, non-blocking, owner/authority
decision, or safety/isolation breach. Return ACCEPT only if no must-fix or
unresolved authority/safety issue remains; otherwise return CORRECT or
ESCALATE with file/line evidence and exact required re-verification. State
which commands/evidence you inspected, unsupported gaps, and that no private
held-out verdict was used. A live reviewer may assess retained evidence but
cannot authorize or retry a request.
```

## 27. Owner acceptance record

On 2026-08-10 the project owner recorded:

- acceptance of the RESEARCH-0054 explicit stateless/cache-off request controls
  as ADR-0025 conformance closure, with no separate ADR required;
- acceptance of the eleven-package sequence and WP1-WP3 plus WP5-WP8 automatic
  progression through their prerequisite and acceptance gates;
- acknowledgement that WP4 native Credential Manager effects and each of
  WP9/WP10/WP11 require their stated distinct external-effect authorization,
  including WP9's separate production-profile and qualification manifests;
- acceptance of schema 6/storage `1.5.0` clean-break planning;
- acceptance of public-only development/validation fixture authority and no
  held-out/private verdict; and
- confirmation that only `current-state.md` identifies the exact active work
  package and that neither plan acceptance nor ordinary implementation
  authorizes a native credential effect or live provider call.
