# RESEARCH-0049: Wave F evaluation and M1 planning integration

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent with delegated independent work packages

Primary RQs: RQ-028, RQ-029, RQ-030, RQ-038

M0 wave: F — Evaluation specifications, deferred-question ledger, and M1 plan

Decision enabled: Gate F owner disposition and M1 implementation-plan
acceptance

Acceptance: Integrated recommendation accepted by the project owner on
2026-07-28; Gate F is met and M1 is authorized under its accepted plan

## Executive answer

Wave F has produced the complete review package required to decide whether M0
can close and M1 implementation planning can become authoritative:

- an accepted common M1 evaluation baseline;
- accepted semantic/local-ground-truth and platform/operational case
  specifications;
- accepted fixture-manifest specifications for both specification sets;
- an empirical RQ-028 calibration protocol that deliberately leaves numerical
  M3/M4 thresholds unset;
- explicit scheduling for RQ-029 and RQ-030;
- exact M1 OpenAI model/profile research and accepted ADR-0025;
- an explicit deferred-question, unsupported-capability, and residual-risk
  register; and
- an accepted M1 backend semantic proof plan with slices, contracts,
  requirement/case traceability, verification, and completion evidence.

The research/specification work completed independent review and the project
owner accepted the Wave F baseline, specifications/manifests, RQ-028
disposition, ADR-0025, risk register, and M1 plan on 2026-07-28. Gate F is met
and M0 is complete. This authorizes only the bounded accepted M1 plan; it does
not mark any fixture executed, evaluation passed, or implementation conformant.

## 1. Integrated outputs

### Evaluation

- [M1 evaluation baseline](../../evaluation/m1-evaluation-baseline.md)
- [M1 semantic and local-ground-truth specifications](../../evaluation/specifications/m1-semantic-and-ground-truth.md)
- [M1 semantic fixture manifests](../../evaluation/fixtures/m1-semantic-fixture-manifests.md)
- [M1 platform and operational specifications](../../evaluation/specifications/m1-platform-and-operational.md)
- [M1 platform fixture manifests](../../evaluation/fixtures/m1-platform-fixture-manifests.md)
- updated [case catalog](../../evaluation/case-catalog.md),
  [evaluation strategy](../../evaluation/evaluation-strategy.md), and
  [fixture guidelines](../../evaluation/fixture-guidelines.md)

Every detailed Wave F evaluation artifact is accepted as a specification or
fixture design. No fixture has been executed and no case has passed.

### Deferred questions and risk

- [RESEARCH-0047 — readiness/maturity calibration plan](RESEARCH-0047-readiness-maturity-calibration-plan.md)
- [deferred-question and residual-risk register](../deferred-question-and-residual-risk-register.md)
- updated [open-question ledger](../open-questions.md)

RQ-028 now has a concrete evidence-collection and later-calibration protocol,
but no intuitive numerical thresholds. RQ-029 is scheduled before any
automatic runtime-log application, no later than its M3 plan. RQ-030 is
scheduled for M4 packaging/update planning after the application architecture
is qualified.

### Exact OpenAI M1 profile

- [RESEARCH-0048 — OpenAI M1 model qualification](RESEARCH-0048-openai-m1-model-qualification.md)
- [ADR-0025 — M1 OpenAI model and synchronous Responses profile](../../architecture/decisions/ADR-0025-m1-openai-model-and-synchronous-responses-profile.md)

The accepted baseline is direct synchronous Responses using explicit
`gpt-5.6-sol`, explicit `reasoning.effort: medium`, strict Structured Outputs,
`store: false`, and no alias, fallback, provider tools, background mode,
Batch, conversation state, alternate access mode, or alternate provider.

OpenAI currently exposes no date-pinned Sol snapshot. Exact replay therefore
means replay of the retained original response. A repeated live request is new
execution and material provider/model drift requires requalification.
ADR-0025 is accepted.

### Implementation plan

- [M1 backend semantic proof plan](../../plans/milestones/M1-backend-semantic-proof.md)

The plan creates a clean source tree rather than reviving `legacy/`, remains
CLI-first, uses the accepted .NET/SQLite/coordinator/worker/IPC/security
architecture, and proves two materially different semantic cases without
making them the product's permanent taxonomy or inserting fixture-specific
production behavior. The plan is accepted and authorizes only its bounded M1
implementation scope.

## 2. Delegated work and integration method

Wave F was divided into independently bounded packages:

1. RQ-028 calibration protocol;
2. semantic/local-ground-truth case specifications and manifests;
3. platform/operational case specifications and manifests; and
4. root integration, exact-model qualification, traceability reconciliation,
   and status review.

Delegated files were required to remain Proposed, avoid registry/plan edits,
and make no execution or conformance claims. Root integration then reconciled
case coverage, provider prerequisites, requirement scope, open-question
status, ADR state, and plan boundaries.

The platform package was amended during integration to add EVAL-0076 and
EVAL-0077 because M1 includes real authenticated/billable OpenAI proof. Those
cases now gate a deliberately tiny provider-transport qualification request
together with EVAL-0034, EVAL-0081, and EVAL-0089. After it passes, EVAL-0067
and EVAL-0083 require separate live source-claim-extraction and
evidence-bound-candidate-investigation requests; the qualification response
cannot substitute for semantic evidence.

## 3. Controlled-real fixture closure

The semantic manifest retains EVAL-0016 and EVAL-0017 as private,
reconstructible validation candidates rather than held-out or redistributed
payloads. Wave F closed the prior official-master identity gap by:

- independently inspecting the exact selected plugin headers with the
  project-authored TES4 reader;
- hashing the required supported-environment base, Creation Club, and resource
  masters; and
- recording exact lengths and SHA-256 identities in the semantic fixture
  manifest.

Wave F also authenticated to Nexus v2 GraphQL, because v3 does not expose long
descriptions, and pinned description hashes, page version/update identities,
and short selected author-purpose passages for the four real-mod sources. The
current page descriptions establish only their bounded purpose/compatibility
claims; they do not automatically prove applicability to every older file
version.

Third-party and official bytes remain evaluator-private and must match the
manifest exactly before execution. No payload was added to the repository.

## 4. M1 case coverage

The accepted M1 gate contains:

- semantic positives, matched negatives, two materially different
  controlled-real cases, typed-index/causal-join candidate selection,
  independent MO2/Mutagen/target truth, analyzer modularity, typed evidence,
  provenance, grouping, coverage, and taxonomy;
- immutable runs, lifecycle, clean/freshness behavior, acquisition/application
  provenance, run output, manual initiation, non-mutation, offline/provider
  behavior, lineage, product-write authority, budget controls, development
  controls, persistence, process/IPC, and credential lifecycle; and
- explicit provider-capability and user-owned billing-authority gates before
  a deliberately tiny qualification request, followed by separately
  authorized and adjudicated live claim-extraction and candidate-investigation
  requests.

The plan narrows partial delivery claims explicitly:

- only the M1 plugin/record/qualified-loose part of SCOPE-005;
- no M3 safe-carryover delivery claim under SNAP-004;
- no DOC-004 adjudication or DOC-005 conflict-resolution claim;
- no user-facing retention/deletion UX completion under OPS-002; and
- no LOOT detection/integration completion under TOOL-001 through TOOL-003.

Excluded areas appear in coverage/capability output rather than disappearing.

## 5. Deferred and unsupported boundaries

M1 intentionally excludes:

- WPF/WebView2/React product UI;
- LOOT/libloot and managed-data refresh implementation;
- Nexus acquisition and hosted broader search;
- background/Batch/cached/concurrent live provider modes;
- provider tools, alternate OpenAI profiles, other providers, and
  ChatGPT/Codex-plan access;
- archive-positive FaceGen, production NIF parsing, PEX/VMAD, root/native,
  generated-output, named configuration, performance, lifecycle, and
  runtime-log analyzers;
- M3 maturity/readiness thresholds and high-end scale; and
- installer, updater, signing, public exports, and public supportability.

These are scheduled future work or unsupported capability, not implied M1
coverage.

## 6. Independent integration review

An independent delegated review completed on 2026-07-28. It found and the
integration pass corrected:

- a circular dependency between M1-plan acceptance and recording Gate F;
- wording that incorrectly made implementation-time fixture construction a
  prerequisite for accepting the specification design;
- a live-provider gap where the transport qualification request did not
  exercise either accepted semantic operation;
- overclaimed SCAN-004 and SNAP-003 traceability;
- overbroad DOC/AI requirement-range wording;
- stale Wave F index/status text and the missing RQ-038 M0-ledger row; and
- ambiguous service-tier request serialization.

The corrected package permits a coordinated M1-plan/Gate F disposition,
separates specification acceptance from fixture readiness/execution, requires
distinct live claim-extraction and candidate-investigation operations after
provider qualification, narrows partial requirement claims, and explicitly
serializes `service_tier: "default"`. Mechanical revalidation found no broken
relative links, identifier conflicts affecting Wave F, merge markers, or
whitespace errors. This review completed the research/integration work; the
later owner disposition accepted it without passing any evaluation.

## 7. Gate F audit

| Gate F condition | Current evidence | State |
|---|---|---|
| All M0 exit criteria pass | Waves A through F are accepted; Wave F research/integration and independent review are complete | Met |
| Evaluation strategy and M1 fixture/anti-overfitting/case specifications are accepted | Anti-overfitting rules and the M1 baseline/specifications/manifests are accepted | Met |
| Every M1 requirement claim has at least one reviewed evaluation case | Independent review corrected overclaims; the accepted M1 plan contains bounded requirement-to-case traceability | Met |
| M1 plan names exact scopes, artifacts, contracts, commands, and completion evidence | The accepted M1 plan does so, including exact provider profile and disabled capabilities | Met |
| No production implementation starts before plan acceptance | The plan was accepted before implementation; no M1 implementation has yet been claimed complete | Met |

Gate F was recorded as met on 2026-07-28 after:

1. the owner accepted RESEARCH-0047's protocol;
2. the owner accepted the common baseline, both specification sets, and both
   fixture-manifest sets;
3. the owner accepted the residual-risk/unsupported-capability register;
4. the owner accepted ADR-0025; and
5. the owner accepted the M1 plan and recorded Gate F as met in the same
   coordinated disposition.

## 8. Recommendation

The accepted owner-review packet contains:

1. RQ-028 calibration recommendation;
2. exact OpenAI profile/ADR-0025;
3. common M1 baseline;
4. semantic specifications and manifests;
5. platform/operational specifications and manifests;
6. deferred/risk register; and
7. M1 plan.

Acceptance of this packet closed M0 and authorized only the bounded M1 plan.
It did not pass any evaluation, qualify any analyzer/integration, or claim
that Infinium is ready for a playthrough.

## 9. Remaining uncertainty

- The live Sol profile has no date-pinned snapshot and requires drift-aware
  requalification.
- Exact operation-specific context/output/call/time/money limits must be
  frozen in the accepted evaluation configuration before the first paid call.
- Synthetic and held-out fixture bytes/oracles still need implementation-time
  construction, independent review, and sealing; the manifest specifications
  do not pretend they already exist.
- Controlled-real inputs remain privately reproducible rather than publicly
  redistributable.
- M1 measures only bounded semantic and operational proof; M3/M4 trust,
  usability, scale, and public supportability remain future claims.

No evaluation is passed and no implementation was executed by this integration
report. Implementation authority comes only from the separately accepted M1
plan and remains bounded by its gates.
