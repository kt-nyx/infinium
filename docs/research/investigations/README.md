# Research investigations

Status: Draft  
Last reviewed: 2026-07-26

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
| [RESEARCH-0001](RESEARCH-0001-nexus-access-policy.md) | Completed | RQ-009 answered for M0 by accepted ADR-0005; Nexus confirmation remains pending |
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
| [RESEARCH-0018](RESEARCH-0018-asset-reference-completeness.md) | Completed; recommendation accepted | RQ-023 NIF-first scope accepted; loose-only FaceGen qualification remains Gate C work |
| [RESEARCH-0019](RESEARCH-0019-semantic-record-family-roadmap.md) | Completed; recommendation accepted | RQ-024 roadmap resolved for M0; exact record/link shapes remain qualification-gated |
| [RESEARCH-0020](RESEARCH-0020-evaluation-corpus-and-real-mod-candidates.md) | Completed; recommendation accepted | RQ-025 two-layer corpus strategy accepted; EVAL-0016 incomplete and EVAL-0017 unselected |
| [RESEARCH-0021](RESEARCH-0021-skyrim-mod-impact-taxonomy.md) | Completed; recommendation accepted | RQ-036 resolved for M0 by accepted taxonomy version `0.1.0` |
| [RESEARCH-0022](RESEARCH-0022-candidate-index-and-ranking.md) | Completed; recommendation accepted | RQ-035 typed-index/causal-join design accepted; independent EVAL-0032 execution pending |
| [RESEARCH-0023](RESEARCH-0023-scale-performance-baselines.md) | Completed; recommendation accepted | RQ-027 method and rough feasibility accepted; exact production baseline deferred |
| [RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md) | Accepted | Wave C recommendations integrated; Gate C closeout is limited to RQ-023 and RQ-025 qualification/corpus prerequisites |

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
Gate C remains pending only the loose-only FaceGen qualification and exact
EVAL-0016/EVAL-0017 real-mod case work recorded in RESEARCH-0024. Acceptance
does not claim that EVAL-0032, EVAL-0086, or any analyzer implementation has
passed.
