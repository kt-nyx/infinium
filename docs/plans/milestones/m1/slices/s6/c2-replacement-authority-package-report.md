# M1/S6/C2 replacement authority-package report

Status: Proposed

Disposition: Inert replacement for the terminal C2 campaign; no external effect authorized

Date: 2026-08-19

Last reviewed: 2026-08-19

## Practical meaning

This package is a sealed permission envelope, not an execution. It binds the
corrected C1.2 executable closure, a wholly fresh credential/campaign identity
set, the exact OpenAI request profile, unchanged ceilings, sequential
predecessor rules, runtime-authority derivation paths, durable ledger grammar,
and sanitized evidence gates. It creates no key, dialog, credential record,
runtime authority, admission, provider request, production lock, safety latch,
or effect evidence.

If the owner accepts the exact final commit and package SHA-256, a later
separately reviewed activation may derive only C2A's credential runtime
authority. C2B, C2C, and C2D remain unavailable until the exact sanitized
evidence from their immediate predecessor is independently accepted. Nothing
live proceeds automatically.

## Why replacement was required

The accepted predecessor package reached one helper launch, but the helper
rejected its v3 manifest before the credential dialog, secret source,
containment descendant, or native store existed. Retained evidence proves
`ManualUiAttempted=false`, zero native credential operations, zero DNS/public
network/provider/billable operations, no observed API key, and no C2B
authority. The old campaign ledger terminalized with final event hash
`bb23edb886b68a90b2bb74f74e9b77cb021232e2d5b2633b33f62c82e3a576d8`.
That campaign and all of its credential, profile, generation, target, stage,
runtime-authority, package, and decision identities are terminal and cannot be
retried or reused.

The typed package names the terminal package and owner-decision IDs explicitly.
It binds the new package ID into the fresh non-reuse set, but intentionally does
not preassign a successor owner-decision ID: that ID may be generated only
after an actual owner acceptance and must be wholly fresh.

C1.2 commit `356ce66c18bb67d5b1de8815970e04bf88195a86` closes the missing
helper/coordinator seam. External effects now require fresh v4 authority;
dormant v2 and terminal v3 identities are explicitly rejected before UI or
native execution. Known pre-UI zero-effect failures retain their exact zero
counts rather than being summarized as unknown.

## Exact executable closure

- C1.2 readiness receipt SHA-256:
  `0457bb093ed3e5eccb4de0378fa453d12f33c1c2d9242bb9941ac7b764000fc8`.
- Coordinator SHA-256:
  `74b0f051886b524e9909b6478762464ee4b5dd3fe3da94fea8e4316dc1f8ea52`.
- Credential-helper SHA-256:
  `60b51d2e46508560409553ab898a4cf45ef46f75a0cf3d77fc01dcf4bd00a9a5`.
- 126-file Release inventory SHA-256:
  `abc0c47d665e733111ae65783616bf029e6a191ee714749d9f79ab617d1afad6`.
- Build command:
  `dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId=356ce66c18bb67d5b1de8815970e04bf88195a86`.

Two consecutive non-incremental builds reproduced the same closure. The exact
clean-commit C1 readiness gate passed with fake credential storage and literal
loopback only; it reported zero helper/UI/native/public-network/provider/
billable effects.

## Bound artifacts and identities

The typed package is
[m1-slice6-c2-replacement-authority-package.v2.json](m1-slice6-c2-replacement-authority-package.v2.json),
SHA-256
`c19248fcc843808588860968a04828cfab5105e5cf08ca98228fbc98419be2bf`.
It binds:

- profile authority SHA-256
  `2ad6eb68ac9f569c358da99989939993b29744f612ab2a22f533e5049ed865df`;
- campaign authority SHA-256
  `824b344073ed802b5cf78014eb0b425bf27713634086b008831b9ded549ae536`;
- official-document snapshot SHA-256
  `a7dc926b49e129aab43ccf1b4625bbd586c8ef7ccb49d056bf2f15afb88a9638`;
- campaign `infinium.m1-s6.finite-live-campaign/ff2d542a-04f0-448a-bcb8-a0ecbedde5b9`;
- credential authorization `infinium.m1-s6.wp9.production-profile-authorization/234b5227-0ad4-4f5e-acf5-5ac6b89fca2b`;
- profile `openai-platform-dc68f2ca9775415eb6fa78de5cafe14e`;
- generation `g-ff6d82e7a7d244f6b8a9d0164991be37`;
- target fingerprint
  `06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0`;
- Qualification stage `infinium.m1-s6.campaign-stage/Qualification/cf3ba7b9-e2cb-427d-b5cb-ae9f679c19c1`;
- SourceClaimExtraction stage `infinium.m1-s6.campaign-stage/SourceClaimExtraction/5eae2494-e3fe-41a7-8b3d-ed11148744e8`;
- CandidateInvestigation stage `infinium.m1-s6.campaign-stage/CandidateInvestigation/a9d1346f-ef84-44dc-a482-266b8e089dce`;
- fresh credential/WP9/WP10/WP11 runtime-authority IDs and exact future paths
  listed in the typed package.

The package expires at `2026-09-15T22:45:00.0000000Z`, the profile at
`2026-09-15T23:00:00.0000000Z`, and the campaign at
`2026-09-15T23:59:00.0000000Z`. Every effect also requires immediate expiry,
binary, profile, price, official-document, ledger, counter, output-absence,
runtime-authority, and safety-latch revalidation.

## Model, prices, and ceilings

The accepted profile remains `gpt-5.6-sol` through synchronous Responses with
medium reasoning, strict structured output, `store:false`, default service
tier, no streaming, no provider tools, no fallback, no parallel dispatch, and
no automatic retry. The refreshed official document bytes are identical to
the accepted predecessor snapshot, so no migration or profile change was
made.

The conservative reservation uses cache-write input at 6,250 nano-USD/token
and output at 30,000 nano-USD/token:

- WP9: `20,480*6,250 + 256*30,000 = 135,680,000`, ceiling 140,000,000;
- WP10: `73,728*6,250 + 4,096*30,000 = 583,680,000`, ceiling 600,000,000;
- WP11: `583,680,000`, ceiling 600,000,000;
- aggregate: 1,303,040,000, ceiling 1,340,000,000 nano-USD ($1.34).

Request-byte, token, raw-response, deadline, cost, native-call, start-count,
retry, and no-fourth-call ceilings are unchanged. A price increase may reduce
scope within the envelope or stop the path; it can never raise a ceiling
implicitly.

## Official documentation

The exact retrieval window is `2026-08-19T18:56:30.5740304Z` through
`2026-08-19T18:56:31.3349670Z`. Exact per-source timestamps, byte counts, and
content hashes are in
[c2-replacement-openai-official-document-snapshot.v2.json](c2-replacement-openai-official-document-snapshot.v2.json).
Sources are the official OpenAI
[model page](https://developers.openai.com/api/docs/models/gpt-5.6-sol),
[latest-model guide](https://developers.openai.com/api/docs/guides/latest-model),
[prompt-caching guide](https://developers.openai.com/api/docs/guides/prompt-caching),
[reasoning guide](https://developers.openai.com/api/docs/guides/reasoning),
[structured-outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs),
[safety guide](https://developers.openai.com/api/docs/guides/safety-best-practices),
[pricing](https://developers.openai.com/api/docs/pricing), and
[data controls](https://developers.openai.com/api/docs/guides/your-data).

## Sequencing, use, and evidence

The order remains C2A credential enrollment, C2B WP9 qualification, C2C WP10
source claims, then C2D WP11 candidates. The package binds stage identities,
templates, validation packages, and closed derivation equations, but does not
fabricate request bytes that depend on predecessor evidence. Each request may
exist only after predecessor acceptance, canonical derivation, v4 validation,
independent review, typed runtime-authority review, and durable admission.

Sanitized evidence remains under `artifacts/m1-slice6/wp9-profile`,
`wp9-live`, `wp10-live`, and `wp11-live`. Each evidence SHA requires a distinct
independent acceptance in the hash-chained coordinator ledger before its
successor can be materialized. A known or possible provider start consumes its
single start and full reservation. Ambiguity is terminal: no retry, fallback,
counter reset, ceiling transfer, or fourth call.

After owner acceptance, the next permitted step is only to derive and review
the exact credential runtime-authority bytes under
`artifacts/m1-slice6/c2-replacement-authority/runtime/credential.v1.json`.
The command templates are frozen in the typed package and must run from a
fresh local full clone at the accepted package commit, with a directory
`.git`. The exact C1.2 126-file closure is staged and hash-verified there; a
different rebuild is not a substitute. Acceptance alone does not run anything.

## Verification, review, and unresolved owner input

The purpose-built inert validator checks all four closed repository-authority
schemas, artifact and executable digests, the 126-file inventory, fresh-ID
non-reuse, terminal predecessor facts, unchanged official document bytes,
price arithmetic, non-broadening ceilings, abstract account/billing intent,
and absence of every runtime/output/state root. It reports zero provider and
billable operations.

Independent C1.2 review required direct negative proof for every retired
manifest/profile/generation/target/campaign/credential identity and corrected
two stale v3 labels. Those corrections passed affected re-review. Consolidated
replacement-package authority/security/provenance/diff review also passed
after closing Windows PowerShell compatibility, official-header provenance,
terminal package/decision non-reuse, and full-clone executable-staging rules.
Only final clean-commit validation remains before owner acceptance.

Two owner-controlled inputs remain deliberately unresolved:

1. exact acceptance of the final package commit and SHA-256; recommendation:
   accept only after the final verification/review report;
2. at a later separately authorized C2A dialog, confirmation that the pasted
   project-bound key belongs to the intended user-owned OpenAI Platform
   account and direct usage-priced API billing scope; recommendation: confirm
   manually at entry without recording concrete account identifiers or the
   secret.

The package prohibits key creation/discovery, secret handling before C2A,
private fixtures, evaluator/archive access, destructive operations, push, C3,
Slice 7, inherited authority, reuse of retired identities, and every live
effect before acceptance and typed durable admission.
