# M1/S6/C2 inert authority-package report

Status: Proposed

Disposition: Ready for owner review; not owner-accepted

Date: 2026-08-19

Last reviewed: 2026-08-19

## Practical meaning

This package is a sealed permission envelope, not an execution. It identifies
the exact corrected program, profile, campaign, model settings, prices,
ceilings, identities, paths, and ordering rules that a later C2 executor must
use. Until the owner accepts the exact final commit and package SHA-256, it
permits no helper, credential, native, network, provider, or billable action.

After exact owner acceptance, a separately authorized executor may derive the
first typed runtime-authority manifest for C2A credential enrollment. C2B,
C2C, and C2D remain locked behind the accepted evidence from their immediate
predecessor. Acceptance does not create a runtime-authority file, stage
request, ledger admission, production lock, safety-use latch, or effect.

## C1.1 correction

C1 commit `25227d30e8e5e5f97655645faf2f3ad71319749b` correctly froze its
effect-free v2 rehearsal but could not consume fresh C2 IDs. C1.1 commit
`9aea07380a3d3cc2a6f70be6d32907a96e7720da` adds v3 profile, campaign,
stage-request, stage-evidence, and composed-evidence schemas plus a runtime
version selector. v2 remains usable only for effect-free historical rehearsal;
every external effect requires fresh v3 campaign and subject authority and
explicitly rejects the retired campaign and credential IDs.

The reproducible C1.1 Release closure is:

- coordinator SHA-256 `59ffbe971c22a9a2907df79429c05dbc8b468a272f822b7cd23fff2872d30d36`;
- credential-helper SHA-256 `3d900e9cb4b11dfbf5fd2edb82a15d26d61b42d920203a99ac4657de67adc20b`;
- 126-file execution inventory SHA-256 `715bae71c42dafdfac5a400907fedc95317409ab5a9e21665a42216345262c13`.
- exact detached-worktree C1 readiness receipt SHA-256
  `ca6d9ef80ff17bfc3272684be4c54c8a60e6e9067282aef091fa9507dcef52f8`.

## Bound authority

The typed package is
`m1-slice6-c2-authority-package.v1.json`, SHA-256
`10b1704591f36a85a6e680b13f28744ad5e81b786efad9aca276ce791b169b9c`.
It binds:

- campaign `infinium.m1-s6.finite-live-campaign/aef3cd3f-9321-4cdc-86a2-2d61510c2e28`;
- credential authorization `infinium.m1-s6.wp9.production-profile-authorization/52b2cfdb-ccd4-49c0-8f6a-ace8c426012e`;
- profile `openai-platform-ecd3de4b9fac443593347905970d942d`;
- generation `g-6eefeaf6e4a74273bf4ee69f02449f47`;
- target fingerprint `990e46a57687417a1a1865bab3b11823f3b37d35961fb8101e32a8977e2a4b67`;
- three fresh stage IDs and four fresh typed-runtime-authority IDs listed in
  the manifest.

Profile authorization SHA-256 is
`1146c2882982683de0b789a376ab45514633de96c834f389d01d2265ce426072`;
campaign authorization SHA-256 is
`c0de630d84a2979fa1c768617880e5692b511fd79a768c2dcabeea34468115b7`;
official-document snapshot SHA-256 is
`f45b9049197739d3d7286bd0be4a0144f2637687109468c966c805db0dd98ab7`.

The profile expires at `2026-08-31T23:00:00.0000000Z`, the package at
`2026-08-31T22:45:00.0000000Z`, and the campaign at
`2026-08-31T23:59:00.0000000Z`. The earlier package expiry prevents a late
acceptance from silently consuming almost-expired effect authority. Every
effect also requires immediate expiry and official-document drift checks.

## Profile, ceilings, and arithmetic

The accepted profile remains `gpt-5.6-sol` through synchronous Responses with
medium reasoning, strict structured output, `store:false`, default service
tier, no streaming, no tools, and explicit cache mode with no breakpoints or
cache key. No model migration was made.

Current conservative price reservation uses cache-write input at 6,250
nano-USD/token and output at 30,000 nano-USD/token:

- WP9: `20,480*6,250 + 256*30,000 = 135,680,000`, below 140,000,000;
- WP10: `73,728*6,250 + 4,096*30,000 = 583,680,000`, below 600,000,000;
- WP11: the same `583,680,000`, below 600,000,000;
- aggregate: 1,303,040,000, below 1,340,000,000 nano-USD ($1.34).

All accepted request-byte, token, response-byte, deadline, native-call,
start-count, retry, and no-fourth-call ceilings are unchanged. A price increase
may only reduce scope within those ceilings or stop for owner disposition; it
cannot raise a ceiling implicitly.

## Sequencing and evidence

The package binds stage identities, limits, public validation packages, and a
closed request-derivation rule. It intentionally does not fabricate later
request bytes: each request can be materialized only from its accepted
predecessor evidence, then canonicalized, hashed, validated under the v3 stage
schema, independently reviewed, and durably admitted.

The order is C2A credential enrollment, C2B WP9 qualification, C2C WP10 source
claims, then C2D WP11 candidates. Each known or possible provider start spends
its single start and full reservation. Ambiguity is terminal, with no retry,
fallback, parallel call, counter reset, ceiling transfer, or fourth call.

Sanitized evidence remains under `artifacts/m1-slice6/wp9-profile`,
`wp9-live`, `wp10-live`, and `wp11-live`. Each evidence digest requires a
distinct independent acceptance recorded in the hash-chained coordinator
ledger before its successor can exist.

## Official documentation

The exact retrieval times and content hashes are in
`c2-openai-official-document-snapshot.v1.json`. Primary sources were the
[gpt-5.6-sol model page](https://developers.openai.com/api/docs/models/gpt-5.6-sol),
[latest-model guide](https://developers.openai.com/api/docs/guides/latest-model),
[prompt-caching guide](https://developers.openai.com/api/docs/guides/prompt-caching),
[reasoning guide](https://developers.openai.com/api/docs/guides/reasoning),
[structured-outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs),
[safety guide](https://developers.openai.com/api/docs/guides/safety-best-practices),
[pricing](https://developers.openai.com/api/docs/pricing), and
[data controls](https://developers.openai.com/api/docs/guides/your-data).

## Owner inputs and use

The package is reviewable now. The remaining owner-controlled inputs are:

1. accept or reject the exact final package commit and package SHA-256;
2. at C2A manual entry, confirm the intended user-owned OpenAI Platform
   account; and
3. at C2A manual entry, confirm that direct API usage is the intended billing
   scope rather than a subscription entitlement.

Recommended use is to accept only the exact final bytes after reviewing this
report. A later separately authorized executor must first derive and validate
the credential runtime-authority manifest from that acceptance, perform the
immediate pre-effect checks, and run the exact command template in the typed
package. After each effect, stop for independent evidence acceptance before
deriving the next stage. No live effect proceeds automatically.

## Verification and review

The exact C1.1 commit passed the effect-free C1 readiness floor in a detached
worktree: nine live-semantic contract checks, 19 runtime-authority/ledger/
safety tests, one schema-closure test, four fake-store/literal-loopback
integration tests, and four credential-security tests. Its receipt SHA-256 is
listed above. Two consecutive SourceRevisionId-pinned non-incremental Release
builds reproduced the same binary closure.

The C2 candidate passes the purpose-built inert validator, all four active
repository-authority schema validations, documentation and local-link checks,
strict JSON/schema compilation, the fresh-v3 contract tests, PowerShell parse,
binary/path/digest and public-package cross-bindings, pricing arithmetic,
forbidden-materialization checks, and diff checks. The validator reports zero
provider requests and zero billable operations.

Independent review first identified the original v2/fresh-ID runtime conflict.
Review of the corrected package then caught and closed two authority-proof
defects: timestamp constants generated without fractional seconds, and an
absence proof that did not yet include the newly bound future runtime-authority
root. The final exact schema preserves timestamps byte-for-byte, and any
premature materialization under `artifacts/m1-slice6/c2-authority` now fails
the inert validator.

The deliberate deviations from a literal v2 copy are the C1.1 v3 clean break,
fresh identities, a current official-document snapshot, and deferred closed
derivation of stage request bytes. Profile-enrollment evidence remains on its
independently versioned accepted v2 evidence shape because it carries the
fresh manifest identity as data and does not itself grant authority. No
accepted product meaning, ceiling, or answer-isolation rule was weakened.

## Prohibited and unresolved

The package does not authorize API-key creation or discovery, account or
billing changes, private fixtures, evaluator or archive work, destructive
operations, push, C3, Slice 7, fallback, retries, parallel dispatch, inherited
authority, or reuse of retired IDs. No active runtime authority, provider
request, production state, lock, latch, or effect evidence exists.

The final package commit and independent-review verdict are supplied by the
owner handoff after this candidate passes its final non-live verification
floor. A Git commit helps identify the reviewed bytes but does not grant
runtime authority.
