# Open research questions

Status: Draft  
Last reviewed: 2026-07-25

The product baseline was accepted on 2026-07-25. The
[M0 research-foundation plan](../plans/milestones/M0-research-foundation.md) is
Accepted and sequences these investigations. Status is updated only from a
bounded investigation and the applicable reviewed or accepted disposition; no
item is answered merely because the legacy code chose an approach.

RQ-026's resolution concludes licensing and the high-level integration posture.
RQ-001 through RQ-007 and RQ-014 now have completed investigations and a
reviewed, accepted Wave B disposition in
[RESEARCH-0013](investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md).
ADR-0007 through ADR-0011 resolve them for M0. Their named conformance,
supported-surface, and implementation gates remain open without reopening the
accepted product-licence or user-installed-application boundary.

| ID | Question | Decision enabled | Status |
|---|---|---|---|
| RQ-001 | How can Infinium obtain authoritative MO2 profile and effective VFS state by deterministic reconstruction or bounded execution through the user's MO2, and is direct USVFS operation ever necessary? | MO2 integration ADR | Resolved for M0 by ADR-0008; EVAL-0051 and implementation conformance pending |
| RQ-002 | Does MO2 expose a current/last-selected profile reliably? | Profile selection behavior | Resolved for M0 by ADR-0008; saved selection is suggestion-only |
| RQ-003 | Which exact Skyrim SE runtime version is initially pinned, how is it detected, and how is support deliberately advanced? | Runtime support contract | Resolved for M0 by ADR-0009; EVAL-0054 and public-release breadth pending |
| RQ-004 | Which exact Mutagen.Bethesda package and authority boundary should Infinium accept, and which Skyrim SE plugin, archive, string, record, and field surfaces must be positively qualified or excluded? | Bethesda semantic layer ADR | Resolved for M0 by ADR-0009; supported-shape/archive/string qualification pending |
| RQ-005 | Can supported invocation of the user-installed LOOT application provide the required structured, deterministic, non-mutating evidence; if not, which needs justify a pinned bundled libloot dependency, and how are LOOT data inputs managed? | LOOT integration ADR | Resolved for M0 by ADR-0011; LOOT delivery remains milestone-conditional and qualification-gated |
| RQ-006 | Which functions of user-installed xEdit provide unique automated or ground-truth value, and which detection, version, invocation, cache/temp, and failure contract is supported? | External-tool scope | Resolved by ADR-0007: xEdit is excluded from every Infinium boundary; its investigated oracle role was rejected |
| RQ-007 | What metadata does MO2 retain about Nexus identity, source archives, FOMOD choices, hidden files, and manual changes? | Identity/FOMOD design | Resolved for M0 by ADR-0008; bounded FOMOD reconstruction remains later work |
| RQ-008 | Which currently supported Nexus Mods APIs/interfaces provide descriptions, requirements, articles, changelogs, files, posts, and revision identity? | Nexus adapter/source coverage | Not started |
| RQ-009 | Which Nexus policies constrain scraping, caching, redistribution, and public application registration? | Source and distribution policy | Answered for M0 by ADR-0005; Nexus confirmation pending |
| RQ-010 | Which non-Nexus sources should be approved, and how can they be searched legally and reliably? | Source registry | Not started |
| RQ-011 | What is the smallest safe, provider-neutral LLM contract for claim extraction and investigation? | LLM provider ADR | Not started |
| RQ-012 | Which provider authentication modes and APIs support explicit user-account selection, billing attribution, models, structured output, batching, cost, quota, rate limits, and stable model snapshots? | Provider capability and user-owned-usage design | Not started |
| RQ-013 | How should reusable documentation/LLM evidence, acquisition runs, source revisions, and profile-application links be stored, cached, and versioned? | Evidence store/cache ADR | Not started |
| RQ-014 | Which fingerprint/dependency strategy proves installation-snapshot and cache validity without prohibitive IO? | Snapshot/cache ADR | Resolved for M0 by ADR-0010; exact schema and implementation conformance pending |
| RQ-015 | Which job store/process model supports linked analysis/acquisition runs, same-run pause/resume, terminal cancellation, checkpoint reuse into a new run, retries, single-owner cost rollups, deletion safety, and UI restarts? | Worker/job architecture ADR | Not started |
| RQ-016 | Which desktop/application stack best satisfies UI, security, deployment, and analysis-isolation requirements: Electron, Avalonia, Tauri/WebView2, or another design? | Application stack ADR | Not started |
| RQ-017 | Which process and data-query boundary, including whether IPC is needed, keeps the UI responsive at high-end scale? | Process/data-access ADR | Not started |
| RQ-018 | Which secure credential-entry and storage mechanisms fit the selected desktop architecture? | Security ADR | Not started |
| RQ-019 | Which root-level Skyrim components can be identified and version-checked deterministically? | Native/root analyzer catalog | Not started |
| RQ-020 | Which generated-output tools expose usable manifests or stable formats? | Generator analyzer roadmap | Not started |
| RQ-021 | Which configuration ecosystems merit named schemas first? | Configuration roadmap | Not started |
| RQ-022 | How far can compiled Papyrus be analyzed structurally and semantically without unreliable claims? | Script analyzer scope | Not started |
| RQ-023 | Which asset formats can be checked for referenced-file completeness efficiently? | Asset analyzer scope | Not started |
| RQ-024 | Which semantic record families and field relationships should follow the first proof? | Analyzer roadmap | Not started |
| RQ-025 | Which real mod combinations provide stable, redistributable or locally reproducible evaluation cases? | Evaluation corpus | Not started |
| RQ-026 | What licensing/distribution obligations apply to bundled helpers and external tools? | Packaging/licensing ADR | Resolved for M0 by ADR-0006 |
| RQ-027 | What high-end time, memory, disk, and cost baselines are realistic on the creator's profile? | Performance budgets/presets | Not started |
| RQ-028 | What evidence, analyzer-maturity, false-positive, coverage, targeted-run carryover, and stale-result thresholds govern M3/M4 readiness and user-facing filtering? | Readiness and maturity acceptance policy | Not started |
| RQ-029 | Which fingerprints and capture workflows can classify imported or tracked logs as exact, matched, likely, unknown, or historical? | Runtime-evidence provenance design | Not started |
| RQ-030 | Which packaging, signing, update, and distribution mechanisms fit M4 without weakening the local security boundary? | Packaging/update ADR | Not started |
| RQ-031 | Which source bytes, tool/model boundary outputs, and tool/model/executable versions may be retained legally and practically; which retained content may appear in each export sharing class; and what replay and redistribution guarantees follow? | Retention/replayability/export policy | Answered for M0 with source-specific conditions and measured-storage follow-up |
| RQ-032 | Which sanitization, navigation, protected-root/write-destination authorization, subprocess, and export-redaction controls satisfy AUTH-002, SEC-001, SEC-003, and SEC-004 in the selected architecture? | Security-boundary ADR | Not started |
| RQ-033 | Which causal/dependency continuity keys and reconciliation workflow can link or explicitly supersede logical findings and cases across runs without false merges, false splits, or disposition leakage? | Finding/case identity and lineage design | Not started |
| RQ-034 | Which deadline-check, atomic reservation, and reconciliation model can enforce concurrent operation/acquisition/analysis hard limits across providers, including elapsed-time deadlines, maximum-call bounds, batching, cancellation, rounding, delayed billing, and adapter capability gaps? | Cost-ledger and budget-enforcement design | Not started |
| RQ-035 | Which local indexes, interaction-graph representation, candidate-generation rules, and staged ranking strategy can retain meaningful interactions at high-end scale without naïve all-pairs model comparison? | M1 candidate-selection design and performance plan | Not started |
| RQ-036 | What purposes/intended feature areas do Skyrim SE mods declare; through which technical surfaces can they alter effective state; which game systems/content areas, consequence types, and effect extents can they affect; and which distinct empirically grounded taxonomies should Infinium use without conflating them? | M1 product taxonomy specification plus analyzer, coverage, navigation, and evaluation coverage map | Not started |

## RQ-036 minimum investigation output

The RQ-036 investigation must:

- inventory technical modification surfaces using primary technical references
  and observed mod structures, including but not limited to plugins/records,
  assets/archives, scripts, configuration, native/runtime components, and
  generated output;
- inventory player-visible game systems and content areas using authoritative
  game/tool documentation plus a representative real-mod corpus rather than
  deriving a closed list from brainstorming or record names alone;
- determine how source-supported declared mod purpose or intended feature area
  relates to—but remains distinguishable from—observed modification surfaces
  and actual or predicted affected game areas;
- distinguish declared purpose, modification surface, affected game area,
  consequence type, severity, symptom, gameplay scope, and blast radius, and
  identify where a many-to-many relationship is required;
- recommend whether each classification is hierarchical, faceted, or
  multi-label, including explicit unknown, unsupported, and cross-cutting
  handling;
- map the proposed versioned taxonomy to analyzer declarations, candidate
  routing, findings/cases, UI navigation, coverage/readiness, change impact,
  remediation/validation, evaluation, and roadmap planning;
- validate the proposal against materially different real and synthetic mods
  and document areas where Skyrim behavior or available tooling does not permit
  confident classification;
- update the
  [taxonomy research dependency map](taxonomy-dependency-map.md) and every
  affected provisional inventory when a taxonomy recommendation is proposed.

RQ-004 and RQ-019 through RQ-024 investigate particular technical surfaces,
formats, or analyzer families. Their outputs shall inform and map to RQ-036
rather than independently defining incompatible game-area or mod-type
taxonomies.

## Investigation requirements

Each completed question should produce a document under
[`investigations/`](investigations/) containing:

- question and product requirements;
- current date and exact versions;
- primary sources;
- local experiments and artifacts;
- alternatives;
- findings and uncertainty;
- recommendation;
- ADR or follow-up questions enabled.

Research conclusions remain proposals until they receive the applicable
accepted disposition—for example an ADR, product-specification amendment,
milestone-plan amendment, or explicitly accepted owner policy disposition.
