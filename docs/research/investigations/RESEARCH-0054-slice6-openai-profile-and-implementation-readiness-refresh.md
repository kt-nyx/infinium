# RESEARCH-0054: Slice 6 OpenAI profile and implementation-readiness refresh

Status: Completed

Disposition: Recommendation accepted by the project owner on 2026-08-10;
explicit stateless/cache-off controls are ADR-0025 conformance closure and no
separate ADR is required

Date: 2026-08-10

Last reviewed: 2026-08-10

Decision enabled: Accepted exact Slice 6 request profile and implementer-ready
M1 Slice 6 plan

## Executive answer

The accepted Slice 6 architecture remains implementable without changing its
product meaning. Direct synchronous `/v1/responses`, exact
`gpt-5.6-sol`, medium reasoning, strict Structured Outputs, `store: false`,
default service tier, non-streaming execution, no provider tools, and retained-
result replay are still documented provider capabilities.

One material provider change required an explicit decision before Slice 6 could
be implementation authority. GPT-5.6 now uses an implicit prompt-cache
breakpoint by default and bills cache writes at 1.25 times ordinary input.
OpenAI now documents `prompt_cache_options.mode: "explicit"` with no explicit
breakpoints as the way to disable implicit cache reads and writes. Omitting the
field would therefore violate the accepted M1 intent to keep provider caching
disabled and would make the finite price bound depend on an execution mode the
plan did not authorize. The project owner closed that decision on 2026-08-10
by accepting the explicit stateless/cache-off plan profile with no separate ADR.

This investigation recommends:

1. explicitly add `reasoning.context: "current_turn"`, ordinary/standard
   reasoning mode, and
   `prompt_cache_options: { "mode": "explicit" }` with no breakpoint or cache
   key to every M1 request; the owner accepted these as Slice 6 plan-level
   ADR-0025 conformance closure with no separate ADR;
2. retain and validate `cached_tokens` and `cache_write_tokens`, requiring both
   to be zero for an accepted M1 response;
3. implement the one-shot helper transport with the .NET base-class HTTP stack
   and a closed hand-written request/response codec, not a provider SDK;
4. do not call the provider input-token-count endpoint in M1 because it would
   be an additional authenticated provider operation outside the accepted
   three-request live sequence;
5. enforce a conservative local input bound over the exact serialized request,
   fixed prompt/schema ceilings, and an admitted pinned tokenizer/overhead
   policy, while reserving the configured maximum rather than an expected
   count; and
6. close contracts, persistence, deterministic simulators, credentials,
   security, budget, semantic admission, replay, and Layers 1-4/6 before any
   live request is separately authorized.

No API key, provider request, Credential Manager entry, private fixture, or
legacy/evaluator archive was used in this investigation.

## 1. Question and authority

This report asks:

> Which current provider facts and repository seams must the accepted M1 Slice
> 6 plan make concrete, and has provider drift created a decision that cannot
> be resolved by implementation detail alone?

Authority remains, in order:

- accepted product requirements and ADR-0001, ADR-0002, ADR-0013, ADR-0015
  through ADR-0023, and ADR-0025;
- the accepted M1 plan, especially Slice 6;
- the M1 continuation verification profile;
- accepted M1 platform/semantic specifications and fixture catalogs; and
- the Slice 5 owner-accepted, slice-frozen handoff.

This report records current external facts and implementation implications. It
does not alter accepted product authority, authorize implementation, or
authorize any live/billable operation.

## 2. Live repository baseline

The planning baseline was inspected on branch `main` at
`d88ba5a5806944f4ec5e919f754dffadc00ebc5f`. The worktree was clean and
`origin/main` identified the same commit. Accepted Slice 5 implementation
candidate `5514919b8f742d00e59752fa7125da487a390926` is an ancestor; the later
commits are repository consolidation and Slice 5 owner-acceptance documents.

The current implementation surface is deliberately incomplete:

- `src/Infinium.OpenAI` is an empty project scaffold;
- `src/Infinium.CredentialHelper` exits with an inactive-scaffold diagnostic;
- `OperationalContracts.cs` and `helper.proto` already scaffold typed profile,
  assignment, reservation, final-gate, usage, and helper-frame concepts;
- those value contracts are not a producer, persistence model, codec, runtime
  path, or conformance result;
- authoritative persistence is schema 5 / storage contract `1.4.0` and has no
  Slice 6 access-profile, credential-intent, provider-operation, reservation,
  dispatch-fence, response, or usage-settlement tables; and
- Slice 5 output represents provider/search/Nexus as `not-used` and contains no
  provider prompt, request, result, provenance, or settlement.

The plan must therefore begin with a clean-break completion of the existing
placeholder contracts and update producers, consumers, schema, codec,
persistence, query/output, replay, fixtures, and tests together. It must not
reinterpret Slice 5 evidence, candidate, finding, case, or replay truth.

## 3. Current official OpenAI evidence

Official documentation was retrieved on 2026-08-10. These are mutable external
facts and must be refreshed before any later live authorization.

| Source | Current fact relevant to M1 |
|---|---|
| [GPT-5.6 Sol model](https://developers.openai.com/api/docs/models/gpt-5.6-sol) | Exact `gpt-5.6-sol` supports Responses and Structured Outputs. Published standard prices are $5.00/M input, $0.50/M cached input, and $30.00/M output. Inputs above 272K use the documented full-request long-context multiplier. Cache writes cost 1.25 times ordinary input. The page still lists no date-pinned Sol identity. |
| [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) | Responses accepts a strict JSON Schema through `text.format`; the host must still validate the returned result and handle refusal/incomplete/error states. |
| [GPT-5.6 model guidance](https://developers.openai.com/api/docs/guides/latest-model) | GPT-5.6 exposes persisted-reasoning context controls. M1 must select `current_turn` explicitly and must not use prior-response or multi-turn reasoning state. |
| [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching) | GPT-5.6 defaults to an implicit breakpoint. `prompt_cache_options.mode: "explicit"` disables that implicit breakpoint; with no explicit breakpoints, the request does not use prompt caching or incur cache-write charges. Reads and writes are reported separately as `cached_tokens` and `cache_write_tokens`. |
| [Counting tokens](https://developers.openai.com/api/docs/guides/token-counting) | The provider input-token endpoint accepts a Responses-shaped input and includes provider framing that a local tokenizer may miss. It is a separate provider endpoint, so M1 cannot silently use it as a preflight call. |
| [Data controls](https://developers.openai.com/api/docs/guides/your-data) | API data is not used for training by default. `/v1/responses` has ordinary abuse-monitoring retention and default application-state retention when stored; `store: false` avoids the stored-response application-state path but is not a claim of zero provider retention. Prompt caching has separate application-state behavior. |

### 3.1 Retained provider facts

The current direct response boundary must preserve separately:

- HTTP status and exact bounded response bytes;
- provider Response identity and terminal/incomplete/refusal/error state;
- requested and returned model;
- requested and returned service tier;
- request ID and relevant response headers where present;
- input, output, total, reasoning, cached-input, and cache-write token facts;
- rate-limit facts with scope, time, and availability;
- provider billing, administrative usage/spend, and credit as unavailable unless
  separately and safely evidenced;
- local rational-catalog calculation and its immutable price snapshot; and
- retained-response replay classification versus a new live execution.

Missing or malformed optional provider fields are typed unavailable or a
bounded adapter failure. They are never zero, unlimited, copied from another
scope, or inferred as provider billing.

## 4. Material drift finding: prompt caching

ADR-0025 accepted a narrow, stateless profile with no provider caching and
ADR-0023 forbids cache-dependent budget admission. The provider's new default
creates two unacceptable outcomes if Slice 6 merely omits cache controls:

1. a request may write prompt tokens to cache and incur a higher input price;
2. prompt material may enter an additional provider application-state path.

Reserving the 1.25-times cache-write price would make the operation financially
safe but would not preserve the accepted no-cache retention/execution
boundary. Conversely, assuming no cache write while leaving implicit mode
enabled would make the hard bound unsound.

The narrow preserving action is:

```json
"prompt_cache_options": {
  "mode": "explicit"
}
```

with no `prompt_cache_key` and no `prompt_cache_breakpoint`. The capability
snapshot records that this control is accepted by the exact model/profile.
The response codec retains both cache usage fields and admission requires zero
for each. A missing field is not evidence of zero and fails the live-profile
qualification unless current official documentation and the accepted plan
define an equivalent explicit proof.

Because ADR-0025 defines an exact request profile, the new stateless/cache-off
fields required explicit acceptance rather than silent introduction in code.
They preserve ADR-0025's no-persisted-reasoning and bounded-retention/cost
intent and do not change model, operation, provider, or trust authority. On
2026-08-10 the project owner accepted the Slice 6 plan-level conformance
closure and determined that no separate ADR is required.

## 5. Transport binding decision

### 5.1 Options

| Option | Benefit | Cost/risk | Disposition |
|---|---|---|---|
| Official provider .NET SDK | Generated/maintained models and convenience APIs | May own serialization, retries, logging, credential/client lifetime, default headers, and response transformations; adds a dependency whose exact behavior must be qualified inside the secret-bearing helper | Reject for M1 |
| Base-class `HttpClient` plus closed codecs | Exact request bytes, headers, URI, retry policy, response byte limits, and secret lifetime remain visible and testable; no new provider dependency | More explicit codec and drift work | Select for M1 |
| Shell/CLI/provider process | Easy manual experimentation | Violates no-shell, inherited-secret, process, exact-byte, and authority boundaries | Reject |

The selected binding is a narrow `HttpClient` owned only by the one-shot
helper. The coordinator constructs and fingerprints secret-free exact request
bytes. The helper adds only the authorization header after exact-target
credential resolution, sends once to the closed endpoint, disables automatic
redirects and automatic retries, stages the bounded raw response and non-secret
receipt, clears owned secret buffers where practical, and terminates.

The adapter does not expose a general HTTP client, arbitrary URI, arbitrary
header map, generic JSON document, or provider SDK object to another process.
This is an implementation binding inside accepted ADR-0018/0020/0021
authority; it does not create a new provider or domain authority.

## 6. Finite input and cost admission

The provider token-count endpoint is useful external evidence, but calling it
would be a fourth authenticated provider operation and would precede the
accepted request's own reservation/dispatch path. It is therefore disabled for
M1 product execution and cannot be used to make a request admissible.

The Slice 6 contract instead requires:

1. fixed, versioned prompt templates and strict output schemas;
2. closed bounded arrays, strings, passages, candidate sets, and request bytes;
3. one deterministic canonical request serializer;
4. a pinned locally executed token-count implementation or a more conservative
   proved upper-bound policy that includes prompt, schema, and framing;
5. a separately configured maximum input-token reservation at least as large as
   the proved upper bound;
6. a price rule that rejects unknown model, tier, context band, region, cache
   mode, currency, or token class;
7. component-wise rational calculation and upward rounding to checked signed
   64-bit nano-USD; and
8. reservation of the configured worst case, never an expected response or
   cache hit.

WP1 of the Slice 6 plan must close and independently review the tokenizer or
upper-bound algorithm before provider transport can become implementation-
active. If no conservative local proof is supportable for the exact profile,
that is an owner/authority escalation; it cannot be patched with a hidden
provider preflight call.

## 7. Contract and persistence implications

The existing Slice 1-era operational records are placeholders. Slice 6 needs
closed v1 product documents for:

- provider access profile and non-secret credential lifecycle intent;
- capability and price snapshots;
- provider operation authorization, exact request assignment, reservation,
  dispatch fence, transport state, response receipt, usage, settlement, and
  replay;
- source-claim-extraction proposal and validation/admission result; and
- evidence-bound-candidate-investigation proposal and validation/admission
  result.

The frozen `effective-scan-configuration/v1` can name an OpenAI mode but also
requires the provider boundary to be `not-used`; current invariants therefore
cannot represent an active provider run. Slice 6 must add a clean
`effective-scan-configuration/v2` provider-active contract and keep v1 as the
exact Slice 5 local/provider-not-used shape. Source-claim extraction must also
run under an evidence-acquisition owner with immutable parent/application/cost
links rather than being represented as an analysis-run-owned shortcut.

Schema migration `M1-S6-0006` should advance SQLite schema 5 to 6 and storage
contract `1.4.0` to `1.5.0`. It must add append-only histories and exact active
generation/fence indexes while retaining Slice 5 schema-5 migration provenance
and semantics. There is no dual current reader or migration from an abandoned
provider implementation.

Application/query output needs non-secret provider operation, authorization,
usage, cost, settlement, capability, and replay projections. Credential target
names, secret bytes, bearer headers, and arbitrary request headers remain
structurally absent.

## 8. Semantic-operation integration

The two provider operations consume Slice 5 evidence and admission contracts:

- source-claim extraction receives one exact retained project-authored source
  revision and bounded passages; its output is an untrusted claim proposal;
- candidate investigation receives a bounded host-selected candidate plus its
  exact supporting and contradicting evidence; its output is an untrusted
  hypothesis/abstention proposal; and
- host validation may reject, retain as a gap, or admit the proposal through
  existing Slice 5 application links.

The model receives no expected result, fixture oracle, local path, credential,
operation primitive, source-policy authority, finding threshold, or automatic
case authority. It cannot create local observations, change score-independent
candidate admission, promote itself to a finding, group a case, or rewrite
historical Slice 5 output.

Synthetic deterministic transcripts exercise the same codecs, validation,
admission, provenance, replay, and output paths before live authorization. Live
responses are compared against independently authored expected semantics only
after the response is retained; product output never authors truth.

## 9. Verification and authorization consequences

Before any live request, the accumulated implementation must pass:

- continuation-profile Layers 1 through 4 and 6;
- the common restore/build/test/format/dependency/diff floor;
- EVAL-0033, EVAL-0034, EVAL-0035, EVAL-0064, EVAL-0067,
  EVAL-0076, EVAL-0077, EVAL-0080, EVAL-0081 synchronous scope,
  EVAL-0083, and EVAL-0089 in their applicable non-live variants;
- deterministic provider, credential, budget, crash, ambiguity, malformed,
  refusal, incomplete, cache, returned-model, offline, canary, replay, backup,
  and projection-rebuild cases; and
- fresh security, contract, semantic, provenance, diff, and claim review.

Before any provider request, production access-profile enrollment or exact-
target verification is itself an explicit owner-authorized Credential Manager
effect. It is distinct from the disposable native test and cannot be hidden in
request preflight.

The three live provider operations remain three separate owner-authorized
packages:

1. tiny transport qualification;
2. source-claim extraction; and
3. candidate investigation.

Passing one does not authorize the next. An ambiguous dispatch or unresolved
settlement keeps the affected and subsequent live gates closed. The composed
provenance package consumes the three retained operations and authorizes no
fourth call.

## 10. Recommendation and ADR disposition

The project owner accepted the exact stateless/cache-off fields with the Slice
6 plan on 2026-08-10. No broader ADR change is supported by this evidence:

- model, reasoning effort, service tier, endpoint, structured-output, storage,
  tool, streaming, and replay selections remain unchanged;
- the cache control preserves rather than changes ADR-0025's intent;
- the `HttpClient` binding is the narrowest implementation of accepted
  helper/network authority; and
- the Slice 6 plan fixes hard numeric ceilings, while each external-effect
  manifest may lower but never raise them after the exact price snapshot is
  refreshed.

Any later proposal to enable persisted multi-turn reasoning or prompt caching
must be handled as a new authority change: amend the plan/ADR as appropriate,
update retention disclosure, and reserve/cache-reconcile the full write/read
price classes. Implementation must not infer that alternative.

## 11. Residual uncertainties

- Provider schemas, model availability, price, retention, rate headers, and
  response fields can drift again before live execution; every live package
  requires a fresh official-docs snapshot and comparability check.
- Official documentation does not make provider billing, spend limits, or
  prepaid balance generally available through a normal Responses receipt;
  those facts remain unavailable unless separately qualified without expanding
  credential purpose.
- A local tokenizer or conservative framing bound still requires implementation
  proof against the exact serializer and accepted fixtures.
- `store: false` is not zero-retention or provider-side deletion proof.
- A moving `gpt-5.6-sol` identity cannot establish deterministic live
  re-execution or broad future model reliability.

## 12. References

- [Current project state](../../current-state.md)
- [Accepted M1 plan](../../plans/milestones/m1/plan.md)
- [M1 continuation verification profile](../../evaluation/m1-continuation-verification-profile.md)
- [Slice 5 handoff](../../plans/milestones/m1/slices/s5/current.md)
- [RESEARCH-0040](RESEARCH-0040-credential-entry-and-storage.md)
- [RESEARCH-0043](RESEARCH-0043-cost-ledger-and-budget-enforcement.md)
- [RESEARCH-0048](RESEARCH-0048-openai-m1-model-qualification.md)
- [ADR-0013](../../architecture/decisions/ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0020](../../architecture/decisions/ADR-0020-credential-storage-and-provider-dispatch.md)
- [ADR-0023](../../architecture/decisions/ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md)
- [ADR-0025](../../architecture/decisions/ADR-0025-m1-openai-model-and-synchronous-responses-profile.md)
- OpenAI, [GPT-5.6 Sol](https://developers.openai.com/api/docs/models/gpt-5.6-sol), retrieved 2026-08-10
- OpenAI, [GPT-5.6 model guidance](https://developers.openai.com/api/docs/guides/latest-model), retrieved 2026-08-10
- OpenAI, [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs), retrieved 2026-08-10
- OpenAI, [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching), retrieved 2026-08-10
- OpenAI, [Counting tokens](https://developers.openai.com/api/docs/guides/token-counting), retrieved 2026-08-10
- OpenAI, [Data controls](https://developers.openai.com/api/docs/guides/your-data), retrieved 2026-08-10
