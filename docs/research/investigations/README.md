# Research investigations

Status: Draft  
Last reviewed: 2026-07-25

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
