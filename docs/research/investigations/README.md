# Research investigations

Status: Draft  
Last reviewed: 2026-07-28

Place one bounded, dated investigation per file:

```text
RESEARCH-NNNN-short-title.md
```

Use this outline:

1. Status, date, last-reviewed date, researcher, and acceptance metadata where
   applicable
2. Question and requirements
3. Scope and non-scope
4. Sources and exact versions
5. Experiments/artifacts
6. Findings
7. Alternatives
8. Uncertainty and limitations
9. Recommendation
10. ADR or follow-up enabled

The product-documentation baseline and current
[M0 research-foundation plan](../../plans/milestones/M0-research-foundation.md)
are accepted. Investigations may begin only within that plan's sequence,
preflight, authority, source, artifact, and review constraints. Use the
[research-agent handoff template](../../plans/research-investigation-agent-handoff-template.md)
for bounded assignments.

Investigations begin as **Proposed** and become **Completed** only after their
evidence, uncertainty, recommendation, and required integration review are
finished. `Completed` describes research-work state, not decision authority.
The index must identify the separate accepted, rejected, deferred, or still
proposed disposition enabled by the report.

## Current investigations

| Investigation | Status | Disposition |
|---|---|---|
| [RESEARCH-0001](RESEARCH-0001-nexus-access-policy.md) | Completed | RQ-009 answered for M0 by ADR-0005; ADR-0012 amends API eligibility/routing; Nexus confirmation remains pending |
| [RESEARCH-0002](RESEARCH-0002-helper-tool-licensing.md) | Completed | RQ-026 resolved by accepted ADR-0006 |
| [RESEARCH-0003](RESEARCH-0003-retention-replay-export-policy.md) | Completed | RQ-031 answered for M0 by accepted owner disposition; measured-storage follow-up remains |
| [RESEARCH-0004](RESEARCH-0004-wave-a-policy-and-evidence-handling-integration.md) | Completed | Wave A integrated; Gate A met |
| [RESEARCH-0005](RESEARCH-0005-mo2-effective-state-acquisition.md) | Completed; recommendation accepted | RQ-001 resolved for M0 by ADR-0008; EVAL-0051 and implementation conformance pending |
| [RESEARCH-0006](RESEARCH-0006-mo2-profile-selection-semantics.md) | Completed; recommendation accepted | RQ-002 resolved for M0 by ADR-0008; saved selection is suggestion-only |
| [RESEARCH-0007](RESEARCH-0007-skyrim-runtime-support-contract.md) | Completed; recommendation accepted | RQ-003 resolved for M0 by ADR-0009; EVAL-0054 and release breadth pending |
| [RESEARCH-0008](RESEARCH-0008-mutagen-bethesda-semantic-capability.md) | Completed; recommendation accepted | RQ-004 resolved for M0 by ADR-0009; supported-shape/archive/string qualification pending |
| [RESEARCH-0009](RESEARCH-0009-loot-integration-and-data-contract.md) | Completed; recommendation accepted | RQ-005 resolved for M0 by ADR-0011; LOOT delivery remains milestone-conditional and qualification-gated |
| [RESEARCH-0010](RESEARCH-0010-xedit-ground-truth-and-invocation.md) | Completed; recommendation rejected | Historical RQ-006 evidence retained; ADR-0007 excludes xEdit and replaces the proposed oracle with parser-independent first-party fixture truth |
| [RESEARCH-0011](RESEARCH-0011-mo2-identity-installer-and-manual-state.md) | Completed; recommendation accepted | RQ-007 resolved for M0 by ADR-0008; bounded FOMOD work remains later |
| [RESEARCH-0012](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md) | Completed; recommendation accepted | RQ-014 resolved for M0 by ADR-0010; exact schema and conformance pending |
| [RESEARCH-0013](RESEARCH-0013-wave-b-authoritative-local-state-integration.md) | Accepted | Wave B integrated; Gate B accepted as met with documented non-blocking gaps |
| [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md) | Completed; recommendation accepted | RQ-019 bounded static inventory/layered-identity roadmap accepted; named analyzer qualification remains conditional |
| [RESEARCH-0015](RESEARCH-0015-generated-output-tool-surfaces.md) | Completed; recommendation accepted | RQ-020 generic inspection and version-pinned generated-output adapter roadmap accepted |
| [RESEARCH-0016](RESEARCH-0016-configuration-ecosystem-survey.md) | Completed; recommendation accepted | RQ-021 generic layer plus MCM Helper, SPID/KID/BOS, and OAR ordering accepted |
| [RESEARCH-0017](RESEARCH-0017-compiled-papyrus-analysis-boundary.md) | Completed; recommendation accepted | RQ-022 bounded static PEX/VMAD contract accepted; generic behavior/performance claims excluded |
| [RESEARCH-0018](RESEARCH-0018-asset-reference-completeness.md) | Completed; recommendation accepted | RQ-023 NIF-first scope accepted; its loose-only FaceGen qualification prerequisite was subsequently completed by RESEARCH-0034 |
| [RESEARCH-0019](RESEARCH-0019-semantic-record-family-roadmap.md) | Completed; recommendation accepted | RQ-024 roadmap resolved for M0; exact record/link shapes remain qualification-gated |
| [RESEARCH-0020](RESEARCH-0020-evaluation-corpus-and-real-mod-candidates.md) | Completed; recommendation accepted | RQ-025 two-layer corpus strategy accepted; its incomplete/unselected candidate state was subsequently closed by RESEARCH-0035 |
| [RESEARCH-0021](RESEARCH-0021-skyrim-mod-impact-taxonomy.md) | Completed; recommendation accepted | RQ-036 resolved for M0 by accepted taxonomy version `0.1.0` |
| [RESEARCH-0022](RESEARCH-0022-candidate-index-and-ranking.md) | Completed; recommendation accepted | RQ-035 typed-index/causal-join design accepted; independent EVAL-0032 execution pending |
| [RESEARCH-0023](RESEARCH-0023-scale-performance-baselines.md) | Completed; recommendation accepted | RQ-027 method and rough feasibility accepted; exact production baseline deferred |
| [RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md) | Accepted | Wave C recommendations integrated; its remaining RQ-023/RQ-025 prerequisites were subsequently completed by RESEARCH-0034/0035 |
| [RESEARCH-0025](RESEARCH-0025-nexus-supported-content-interfaces.md) | Completed; recommendation revised | Historical unauthenticated inventory retained; authenticated/latest-capable recommendation and former GraphQL blocker superseded by RESEARCH-0030/ADR-0012 |
| [RESEARCH-0026](RESEARCH-0026-non-nexus-source-governance.md) | Completed; recommendation revised | Historical source-governance evidence retained; LOOT freshness, low-priority GitHub docs, and governed-search recommendation revised by RESEARCH-0031/0032 |
| [RESEARCH-0027](RESEARCH-0027-provider-neutral-llm-contract.md) | Completed; recommendation partially superseded | Two semantic operations/admission invariants retained; provider-neutrality as a capability ceiling superseded by the OpenAI-first direction and RESEARCH-0032 |
| [RESEARCH-0028](RESEARCH-0028-provider-capability-and-authentication.md) | Completed; recommendation partially superseded | Authentication/capability evidence retained; initial second-provider/parity implication superseded by RESEARCH-0032 |
| [RESEARCH-0029](RESEARCH-0029-wave-d-documentation-and-provider-boundary-integration.md) | Completed; prior integration result superseded | Valid record of the 2026-07-26 state; Nexus/GraphQL/provider-parity blockers superseded by RESEARCH-0030 through RESEARCH-0033 |
| [RESEARCH-0030](RESEARCH-0030-nexus-latest-interface-qualification.md) | Completed; recommendation accepted in ADR-0012 | Authenticated v3/v2/v1 capability qualification and latest-capable routing resolve RQ-008 for M0 |
| [RESEARCH-0031](RESEARCH-0031-loot-freshness-and-source-discovery.md) | Completed; recommendation accepted | Current-compatible LOOT pair refresh and minimal non-Nexus source posture accepted through ADR-0014 |
| [RESEARCH-0032](RESEARCH-0032-openai-first-llm-and-web-search.md) | Completed; recommendation accepted | OpenAI-first Responses/search capability boundary accepted through ADR-0013 |
| [RESEARCH-0033](RESEARCH-0033-wave-d-revision-integration.md) | Accepted | Revised Wave D independently reconciled; Gate D accepted as met at the M0 research/design layer |
| [RESEARCH-0034](RESEARCH-0034-loose-facegen-qualification.md) | Completed; recommendation accepted | RQ-023 loose-only FaceGen decision boundary qualified for pre-resolved record/provider inputs; archive-positive and production-adapter conformance remain later work |
| [RESEARCH-0035](RESEARCH-0035-gate-c-real-mod-qualification.md) | Completed; recommendation accepted | RQ-025 resolved for M0 with exact, independently grounded EVAL-0016 and materially different EVAL-0017 candidates plus matched controls |
| [RESEARCH-0036](RESEARCH-0036-evidence-persistence-and-versioning.md) | Completed; recommendation accepted | ADR-0015 accepts SQLite plus a content-addressed payload store and rebuildable projections |
| [RESEARCH-0037](RESEARCH-0037-job-checkpoint-and-run-lifecycle.md) | Completed; recommendation accepted | RQ-015 resolved by accepted ADR-0016 with an application-owned transactional SQLite lifecycle and bounded scheduler |
| [RESEARCH-0038](RESEARCH-0038-desktop-application-stack-comparison.md) | Completed; recommendation accepted | ADR-0017 accepts .NET 10, React/TypeScript, and a minimal direct WPF/WebView2 host |
| [RESEARCH-0039](RESEARCH-0039-process-and-data-query-boundary.md) | Completed; recommendations accepted | ADR-0018/ADR-0019 accept the standalone coordinator/process authority and named-pipe IPC/query contract |
| [RESEARCH-0040](RESEARCH-0040-credential-entry-and-storage.md) | Completed; recommendation accepted | ADR-0020 accepts Credential Manager generic credentials and a one-shot helper boundary |
| [RESEARCH-0041](RESEARCH-0041-security-boundary-controls.md) | Completed; recommendation accepted | ADR-0021 accepts layered renderer, filesystem, subprocess, staging, diagnostics, and export controls |
| [RESEARCH-0042](RESEARCH-0042-finding-case-continuity-and-reconciliation.md) | Completed; recommendation accepted | ADR-0022 accepts opaque logical identities and evidence-bearing append-only reconciliation |
| [RESEARCH-0043](RESEARCH-0043-cost-ledger-and-budget-enforcement.md) | Completed; recommendation accepted | ADR-0023 accepts coordinator-owned atomic multi-scope reservations and one-owned usage accounting for the direct API path |
| [RESEARCH-0044](RESEARCH-0044-wave-e-architecture-and-security-integration.md) | Completed; recommendation accepted | ADR-0015 through ADR-0023 accept the integrated Wave E architecture; later dispositions reject the RESEARCH-0045 Codex proposal and Dapr |
| [RESEARCH-0045](RESEARCH-0045-openai-user-access-modes.md) | Completed; recommendation rejected | Research proves ChatGPT-plan and Platform API access are distinct; owner retained direct Responses/API-key access and rejected ADR-0024 |
| [RESEARCH-0046](RESEARCH-0046-dapr-workflow-desktop-lifecycle-qualification.md) | Closed; Dapr rejected without prototype | Owner selected the application-owned SQLite lifecycle and retained the unexecuted Dapr comparison plan as decision provenance |
| [RESEARCH-0047](RESEARCH-0047-readiness-maturity-calibration-plan.md) | Completed; recommendation accepted | RQ-028 empirical calibration protocol accepted without inventing numerical M3/M4 thresholds |
| [RESEARCH-0048](RESEARCH-0048-openai-m1-model-qualification.md) | Completed; recommendation accepted | ADR-0025 accepts the exact `gpt-5.6-sol` synchronous Responses profile and drift policy |
| [RESEARCH-0049](RESEARCH-0049-wave-f-evaluation-and-m1-planning-integration.md) | Completed; recommendation accepted | Wave F outputs integrated, independently reviewed, and accepted; Gate F is met and M0 is complete |
| [RESEARCH-0050](RESEARCH-0050-sqlite-opened-object-write-authority.md) | Completed; implementation qualification in progress | The Slice 2 shim-VFS approach qualifies as ADR-0021's separately qualified equivalent for SQLite-family opens; non-SQLite write conversion and full EVAL-0080 remain pending |

Supporting artifact:

- [Wave B local reference environment manifest](WAVE-B-reference-environment-manifest.md)
  — completed shared preflight and sanitized private-reference manifest.

Wave B's eight bounded investigations and independent integration review are
complete and accepted. Gate B is met with documented non-blocking gaps for M0
research progression. ADR-0008 through ADR-0011 accept the selected MO2,
runtime/Mutagen, snapshot, and conditional LOOT/libloot boundaries; ADR-0007
rejects xEdit entirely. This does not accept an application stack, database,
IPC/process topology, M1 implementation plan, or claim that the named
evaluation cases passed.

Wave C's ten bounded investigations and integrated owner disposition are
complete and accepted. The accepted taxonomy is
[`infinium.skyrim-se.mod-impact-taxonomy/0.1.0`](../../product/mod-impact-taxonomy.md).
The category-neutral anti-overfitting rules and RESEARCH-0034/0035 are accepted
by the project owner. They complete the remaining RQ-023/RQ-025 qualification
work, so Gate C is met at the M0 research/qualification layer. This does not
claim that EVAL-0016, EVAL-0017, EVAL-0032, EVAL-0086, or any analyzer
implementation has passed execution.

Wave D's original reports remain as dated evidence. The revised
RESEARCH-0030 through RESEARCH-0033 round completed authenticated Nexus
qualification, LOOT/source-freshness research, OpenAI-first capability
research, and independent integration. ADR-0012 removes the former
authenticated-Nexus and separate GraphQL-policy blockers. The owner accepted
RESEARCH-0033 and ADR-0012 through ADR-0014 on 2026-07-28. Gate D is met at
the M0 research/design layer. This does not claim provider, libloot,
credential, budget, or evaluation conformance, and no Nexus page fallback is
authorized.

Wave E's eight original bounded investigations and independent integration are
complete. The later OpenAI access-mode investigation is also complete, but its
Codex/ChatGPT-plan recommendation and ADR-0024 were rejected; direct Responses
under ADR-0013 remains current. RQ-015 is resolved by accepted ADR-0016;
RESEARCH-0046 is closed without a prototype and records the rejection of
Dapr. ADR-0015 through ADR-0023 accept the complete Wave E architecture, and
Gate E is met at the M0 architecture/design layer. Those ADR acceptances do
not claim that the application stack, evidence database, IPC mechanism,
credential/access path, security boundary, continuity mechanism, or cost
ledger is implemented or that any evaluation case passes.

Wave F's research, evaluation specifications, deferred-question ledger, M1
plan, and independent integration review are complete through RESEARCH-0049.
The project owner accepted the Wave F recommendations, baseline, case
specifications/manifests, ADR-0025, residual-risk register, and M1 plan on
2026-07-28. Gate F is met and M0 is complete. No evaluation execution or M1
implementation result is implied.
