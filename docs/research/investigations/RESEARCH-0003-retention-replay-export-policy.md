# RESEARCH-0003: Retention, replay, and export policy boundary

Status: Completed
Disposition: owner disposition accepted for M0
Date: 2026-07-25

Accepted: 2026-07-25

Last reviewed: 2026-07-28

Researcher: Codex agent

Subsequent disposition: Accepted ADR-0012 through ADR-0014 and
RESEARCH-0033 resolve Wave D's Nexus routing, OpenAI capability, governed web
discovery, and LOOT managed-data refresh decisions while preserving this
report's accepted retention/replay/export semantics. Exact persistence,
credential, security, cost, and landing-source adapter mechanisms remain
Wave E work.

Primary RQ: RQ-031

M0 wave: A — Policy and evidence-handling guardrails

Decision enabled: Evidence-store/cache ADR inputs, provider and source adapter
contracts, deletion semantics, replay disclosures, export sharing classes, and
Wave A Gate A review

> This is a technical and policy recommendation, not legal advice. Copyright,
> contract, privacy, database-right, and fair-dealing/fair-use conclusions are
> source-, jurisdiction-, and fact-specific. Public/commercial distribution
> and any ambiguous private-copying or provider-input use should receive
> qualified legal review.

## Executive answer

Infinium should retain **identities, versions, source references, fingerprints,
typed provenance, dependency edges, and omission/deletion records by default**.
It should retain exact bytes only at a declared boundary where all of the
following are true:

1. the bytes were acquired through an authorized method;
2. private retention is permitted for that source/data class;
3. exact retention is necessary to complete useful extraction, analysis,
   claim/case/finding synthesis, prose generation, provenance, audit, replay,
   refresh, private history, or another accepted product behavior;
4. the product can protect, delete, and account for every independently
   retained copy; and
5. practical disk cost has been made visible.

Permission to acquire, permission to retain privately, permission to transmit
to a model/provider, and permission to redistribute are four separate
decisions. A positive decision on one axis does not imply another.

The accepted product sharing classes also remain separate:

- product-private retained state;
- inspectable local run-owned output;
- a user-created private/local export;
- an externally shareable report, Markdown, HTML, or structured export;
- an explicitly selected sensitivity-labeled developer-trace export; and
- the M4 privacy- and redistribution-reviewed diagnostic bundle.

Run-owned output and a developer trace are not externally shareable merely
because the user can inspect or copy them. A private/local export may contain
more user-selected sensitive material than a shareable export, but it remains
an independently retained copy with no redistribution guarantee. An externally
shareable artifact must use only permitted exact content/excerpts or replace
restricted content with derived claims, metadata/fingerprints, references, or
explicit omission markers. Credentials are excluded from every class except
their dedicated OS-backed credential store; they never enter ordinary retained
state or any export.

Replay must state both **the scope being replayed** and **the mechanism**:

- deterministic recomputation from exact inputs and executable logic;
- exact downstream replay from a retained tool/model/source boundary output;
  or
- a reproduction attempt that recontacts a source/provider or substitutes an
  unavailable tool/model and may differ.

`Complete`, `partial`, and `unavailable` describe whether every dependency for
the declared replay scope is currently resolvable. They do not mean that a
nondeterministic model can be called again to produce byte-identical text.
A run can have complete exact downstream replay from a retained model response
while clean model recomputation is unavailable. The product must show both
facts rather than collapse them.

The original Nexus prerequisite, RESEARCH-0001, conservatively treated
automated API analysis and retention as blocked pending clarification. The
project owner subsequently accepted
[ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md):
bounded supported-API retrieval, private retention needed for diagnostic
provenance/replay, deterministic extraction, and minimized optional LLM
inference may proceed as personal/mod-enhancement use while Nexus confirmation
is pending. This changes the Nexus rows below from a total operational block
to a conditional source class. It does not permit page scraping, unsupported
interfaces, public corpus creation, model training, or raw source
redistribution, and it does not collapse the four independent permissions.

### Accepted owner clarification

The project owner accepts this policy with the following controlling
clarification:

- metadata-first retention governs unnecessary durable duplication and export;
  it does not require deletion of permitted private source material before the
  product has completed useful extraction, deterministic analysis,
  user-authorized LLM analysis, claim/case/finding synthesis, prose generation,
  provenance, audit, replay, refresh, and applicable private-history work;
- within ADR-0005, Nexus material acquired through documented supported APIs
  may be retained privately for those purposes during development under the
  owner's accepted policy-risk decision; and
- a negative or limiting Nexus response, relevant policy change, or another
  ADR-0005 reversal trigger stops and reviews the affected acquisition and
  retention path. It does not silently substitute scraping or another
  unsupported source.

## 1. Question and accepted constraints

### 1.1 Primary question

Which source bytes, tool/model boundary outputs, and
tool/model/executable versions may be retained legally and practically; which
retained content may appear in each export sharing class; and what replay and
redistribution guarantees follow?

### 1.2 Linked accepted requirements

| Requirement | Relevance |
|---|---|
| `AUTH-001`–`AUTH-003` | Retention, cache, trace, deletion, export, and approved tool-owned cache/temp writes must remain isolated from protected setup state. |
| `SEC-001` | Retained source, tool, model, and export content remains untrusted data. |
| `SEC-002` | Credentials never enter prompts, ordinary retained state, logs, traces, or exports and can be revoked/deleted. |
| `SEC-004` | Developer traces need sensitivity labels; the M4 diagnostic bundle needs explicit selection, redaction, and source-policy review. |
| `SCAN-005`, `SCAN-007` | Deletion may affect resume/reuse; clean recomputation is distinct from source refresh. |
| `SNAP-001`–`SNAP-006` | Runs retain resolved inputs, provenance, and honest complete/partial/unavailable replay and audit-gap disclosures. |
| `EVID-001`–`EVID-003`, `EVID-007` | Retention must preserve typed evidence and provenance without implying that every source byte is copied. |
| `DOC-002`, `DOC-004`, `DOC-008`, `DOC-009`, `DOC-011` | Source bytes, cited passages, extraction revisions, acquisition runs, application links, freshness, and deletion loss remain separately attributable. |
| `AI-001`, `AI-003`, `AI-006`, `AI-007` | The core contract is provider-neutral; context is minimized; exact permitted requests/responses/configuration are retained; authenticated use is user-owned. |
| `OPS-001` | An operation declares local, cached, network, and provider dependencies. |
| `OPS-002` | History is indefinite by default, but user deletion previews and explicitly scopes every cascade and independent copy. |
| `OPS-003` | Run-owned outputs and each user-created export class remain distinct; retention permission is not redistribution permission. |

### 1.3 Accepted ADR constraints

| ADR | Constraint |
|---|---|
| [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md) | Retention and export do not change evidence type or authority. A model output does not become local-state truth because it is retained. |
| [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md) | Snapshots, contexts, run configurations, acquisition ownership, boundary outputs, reuse, review state, and replay remain distinct and historically immutable. |
| [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md) | Product-owned storage/export/deletion authority never expands to mutation of the modding setup. |
| [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md) | This policy is bounded to the initial Windows/MO2/pinned Skyrim SE target and manually initiated work. |
| [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md) | Supported, bounded, user-initiated Nexus API acquisition and diagnostic transformation may proceed; necessary private retention and minimized optional inference are conditional, while scraping, unsupported interfaces, training, and raw external redistribution remain excluded. |

This investigation supplies evidence and recommendations. It does not select a
database, object store, provider adapter, encryption mechanism, archive format,
or accepted architecture.

## 2. Scope, non-scope, and preflight

This section records the boundaries of the original RQ-031 investigation. Its
statement that authenticated Nexus access was out of scope describes what that
research run did, not what later work may do under ADR-0005.

### 2.1 In scope

- Installation-snapshot metadata, references, and copied source-byte choices.
- Local game, mod, configuration, documentation, userlist, and log material.
- Parsed observations, indexes, evidence graphs, claims, findings, cases,
  recommendations, readiness, review history, jobs, caches, and checkpoints.
- URLs, revisions, timestamps, fingerprints, exact cited excerpts, and source
  bodies.
- Nexus-origin/service content under
  [RESEARCH-0001](RESEARCH-0001-nexus-access-policy.md).
- LOOT executable/library/data/user-input boundaries and other approved helper outputs
  under [RESEARCH-0002](RESEARCH-0002-helper-tool-licensing.md).
- Tool executable/library identity versus byte retention.
- Provider-neutral prompt, request, response, tool-result, usage/cost, and
  model-version retention.
- Current OpenAI API/service behavior only as the initial reference provider.
- Operational replay classes, deletion cascades, independent copies, disk
  implications, export content, and Gate A consequences.

### 2.2 Explicit non-scope

- Legal advice or a jurisdiction-specific fair-use/fair-dealing opinion.
- Written Nexus clarification, an exemption, or live Nexus content access.
- An unbounded survey of LLM providers.
- Authenticated, billable, or paid API calls.
- Selecting OpenAI endpoints/models, an evidence store, encryption, deduplication,
  compression, backup, or archival technology.
- Measuring numeric disk/time budgets; RQ-014 and RQ-027 own those measurements.
- Selecting Infinium's own product licence or resolving the helper combined-work
  questions left by RQ-026. This investigation did not make that decision;
  ADR-0006 subsequently resolved the high-level posture.
- Public packaging, support intake, or the final M4 diagnostic-bundle design.
- Altering the source registry, RQ registry, product baseline, ADRs, evaluation
  catalog, plan, or either prior report.

### 2.3 Access and effects

| Item | Treatment |
|---|---|
| Local private data | Not required or accessed. Repository documents only were read. |
| Network | Public official product, policy, developer-documentation, and contract pages only. |
| Authenticated/paid APIs | Not authorized and not used. |
| External executables | No helper, game, mod manager, or analyzer executable was run. |
| Workspace writes | This new proposed investigation only. |
| Retained raw artifacts | None. The report retains direct URLs, displayed versions/dates, and synthesized observations. |
| Stop conditions | Nexus content access; paid/authenticated provider access; source copying not already authorized; executable experiments; or architecture selection. |

## 3. Terms and policy model

### 3.1 Four independent permissions

Every source/adapter record needs four explicit decisions:

1. **Acquire/inspect** — may Infinium read or retrieve the material through the
   proposed method?
2. **Private retention** — may Infinium preserve exact bytes or excerpts in
   product-controlled local state?
3. **Model/provider transmission** — may Infinium send the selected content to
   a third-party or local model under that provider's terms, retention, region,
   and tool-subprocessor behavior?
4. **External redistribution** — may a user-created artifact intended for
   external sharing contain the exact bytes, an excerpt, or a derived form?

`Unknown` on an axis is a stop on that axis, not permission to use the most
convenient fallback. Model transmission is not merely private retention: it is
a disclosure to the chosen provider and sometimes to provider tools or other
third parties.

### 3.2 Product sharing classes

| Code | Accepted product distinction | Intended authority |
|---|---|---|
| `P` | Product-private retained state | Product-controlled local history/evidence/cache. Not an export. |
| `R` | Inspectable run-owned human-readable/JSON output | Local execution artifact owned by a run. Inspectable/copyable, but not externally shareable by classification. |
| `L` | User-created private/local export | Explicit independently retained export intended for the user's local/private use. May include selected sensitive material when private retention allows it; no external-sharing guarantee. |
| `S` | Externally shareable report/Markdown/HTML/structured export | Explicit export whose generator permits only redistribution-cleared content, permitted excerpts, derived claims, metadata/references/fingerprints, or omission markers. |
| `T` | Sensitivity-labeled developer-trace export | Explicitly selected export of trace material. Its intended sharing class must still be recorded; the sensitivity label is not redistribution permission. |
| `BND` | M4 externally shareable diagnostic bundle | Explicit selection plus inspectable privacy redaction and source-policy/redistribution review. Credentials always excluded. |

Every `L`, `S`, `T`, or `BND` artifact retains the exact selection manifest
required by `OPS-003`. A `T` export intended only for local/private developer
use follows `L` content permissions; one intended for external transmission
must satisfy `S` or `BND` permissions as well.

### 3.3 Retained-content forms

| Code | Form |
|---|---|
| `B` | Exact bytes/body or lossless structured payload |
| `X` | Exact excerpt necessary to support a claim |
| `D` | Derived observation/claim/finding/statistic that does not reproduce restricted expression |
| `M` | Metadata, source identity/revision, size, time, version, fingerprint, dependency, or licence/policy decision |
| `O` | Explicit omission/deletion/unavailability marker or reference/citation |
| `—` | Excluded |

These are content forms, not authority levels. A fingerprint can still be
sensitive, personal, or source-derived and must be classified accordingly.

### 3.4 Replay mechanisms and statuses

#### Mechanisms

- **DR — deterministic recomputation:** rerun the exact deterministic
  transformation with exact inputs, configuration, ruleset/tool executable
  identity, and relevant environment dependencies. The normalized boundary
  output must match; an unexplained difference is a failed replay.
- **BR — boundary replay:** consume a retained, integrity-checked source,
  tool, or model boundary output without reacquiring or reinvoking that
  boundary. Downstream deterministic logic can replay exactly even if the
  original boundary is nondeterministic or no longer callable.
- **RA — reproduction attempt:** reacquire a live source, call a provider
  again, or use a replacement/unpinned/unavailable model/tool. It creates a new
  run/evidence revision and can be semantically compared; it is not replay of
  the old boundary.

#### Statuses

- **Complete:** every dependency for the declared replay scope and mechanism is
  present, integrity-valid, readable, and executable where execution is
  required.
- **Partial:** at least one declared subgraph can replay, while one or more
  branches, evidence payloads, citations, or transformations cannot. The
  product names the last intact boundaries and affected outputs.
- **Unavailable:** no material path for the declared replay scope reaches its
  required result. Surviving history may remain auditable.

The disclosure should use forms such as:

```text
downstream replay: complete (BR from retained model/tool outputs)
clean deterministic recomputation: partial (tool executable unavailable)
clean provider reproduction: unavailable (model snapshot retired)
audit trail: complete except deleted source excerpt
```

`Complete` without its scope/mechanism is ambiguous and should not be shown.

## 4. Sources and exact versions

All external sources were retrieved or rechecked on **2026-07-25**.

| ID | Primary/authoritative source | Version/revision identity | Claim-level relevance |
|---|---|---|---|
| I1 | [Accepted product requirements](../../product/requirements.md) | Accepted; reviewed 2026-07-25 | Normative private-history, LLM provenance, deletion, run-output, export-class, and diagnostic-bundle distinctions. |
| I2 | [Domain model](../../product/domain-model.md) | Accepted; reviewed 2026-07-25 | Normative source/run/export ownership and replay/dependency relationships. |
| I3 | [Jobs, caching, and snapshots](../../architecture/jobs-caching-and-snapshots.md) | Draft; reviewed 2026-07-24 | Required resolved-input, clean/reuse/refresh, checkpoint, and replay behavior; no storage mechanism is accepted. |
| I4 | [Security and privacy](../../architecture/security-and-privacy.md) | Draft; reviewed 2026-07-24 | Credential exclusion, context minimization, trace sensitivity, export review, and independent-copy deletion effects. |
| I5 | [Evaluation strategy](../../evaluation/evaluation-strategy.md) and [case catalog](../../evaluation/case-catalog.md) | Draft; reviewed 2026-07-25 | Required replay, source policy, retention, export, and deletion test surfaces. |
| P1 | [RESEARCH-0001: Nexus access policy](RESEARCH-0001-nexus-access-policy.md) and [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md) | Research completed and owner decision accepted 2026-07-25 | HTML scraping and unsupported interfaces prohibited; bounded supported-API analysis, necessary private retention, and minimized optional inference accepted as a project interpretation; external source redistribution remains independently constrained. |
| P2 | [RESEARCH-0002: helper-tool licensing](RESEARCH-0002-helper-tool-licensing.md) and [ADR-0006](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md) | Research completed and operational disposition accepted 2026-07-25; exact helper/data revisions in the report | Selects the GPLv3 family and separates user-installed applications, possible bundled libraries, managed CC0 LOOT data, private userlist, and output. Tool output is not automatically licensed/shareable. |
| O1 | [OpenAI data controls](https://developers.openai.com/api/docs/guides/your-data) | Live API documentation; retrieved 2026-07-25; page exposes endpoint-specific current behavior but no displayed page revision/date | API data is not used to train by default; abuse-monitoring logs may retain prompts/responses up to 30 days; application-state behavior varies by endpoint/tool; ZDR/MAM require approval. |
| O2 | [OpenAI Services Agreement](https://openai.com/policies/services-agreement/) | Updated 2025-12-01; effective 2026-01-01 | As between OpenAI and the customer, customer retains Input rights and owns Output; customer remains responsible for Input rights and Output use. Services/third-party restrictions still apply. |
| O3 | [OpenAI API deprecations](https://developers.openai.com/api/docs/deprecations#2026-04-22-legacy-gpt-model-snapshots) | Entry dated 2026-04-22; retrieved 2026-07-25 | Model and alias availability is time-bounded. Named/date-pinned snapshots can still be shut down, so model identity is provenance, not an executable archive. |
| B1 | [Skyrim Special Edition EULA](https://store.steampowered.com/eula/489830_eula_0) | Steam App 489830 EULA; no displayed revision/update date; retrieved 2026-07-25 | The game licence permits use of one copy and archival media in its stated scope and restricts copying, reproduction, electronic transmission, and distribution except as permitted by the agreement/applicable law. It does not grant Infinium a shareable game-byte licence. |

### 4.1 Source applicability cautions

- P1 and P2 own their conclusions. This report consumes them without relaxing
  or subtly replacing them.
- O2 allocates rights **between OpenAI and its customer**. It does not grant
  rights in mod, game, author, tool, web-search, remote MCP, or other
  third-party material placed in Input or reproduced in Output.
- O1 is endpoint- and capability-specific. A provider record must pin the
  actual endpoint, storage setting, tools, project data-control setting, and
  retrieval date instead of storing one generic `openai_retention=30d` value.
- B1 is useful negative evidence against assuming that access to installed game
  bytes includes a right to copy them into exports. This report does not decide
  any statutory exception or whether a particular transient technical copy is
  independently permitted.
- Mod licences and author-site terms vary. No generic "installed mod" or
  "public web page" licence exists.

## 5. Documentation checks and artifacts

### Environment and tooling

- Host: Microsoft Windows NT 10.0.26200.0
- PowerShell: 7.6.3
- Git: 2.55.0.windows.2
- OpenAI developer-documentation MCP service: public read-only search/fetch
  interface; the service exposed no client/version identifier
- OpenAI Services Agreement and Skyrim EULA: public web retrieval only
- No authenticated API, external helper, game, mod-manager, or local private
  artifact was used

### Check C1 — accepted-class and identifier review

Procedure:

1. Read the accepted requirements, domain model, workflows, M0 plan, accepted
   ADRs, security/privacy, jobs/caching/snapshots, integrations, source
   registry, and evaluation documents.
2. Enumerate every product-owned artifact and sharing class named by
   `AUTH-002`, `OPS-002`, `OPS-003`, `SEC-004`, and the export workflow.
3. Trace replay dependencies through `SNAP-006`, `AI-006`, `DOC-008`,
   `DOC-011`, and ADR-0002.

Observed:

- The accepted baseline already rejects one generic export class.
- Run-owned CLI/JSON and raw traces are local execution artifacts, not
  user-created exports.
- Deletion must preview and explicitly select independent copies rather than
  silently cascading from source to export or export to source.
- Auditability and executable replay are separate.

### Check C2 — prerequisite-report boundary review

Procedure:

1. Read P1 and P2 in full.
2. For each conclusion, classify acquisition, private retention, model
   transmission, and redistribution separately.
3. Reject any matrix rule that treats API capability, a software licence,
   output production, or private possession as a blanket downstream licence.

Observed:

- P1 found the Nexus automated-analysis wording unresolved; ADR-0005 accepts a
  bounded supported-API interpretation for project operation while preserving
  the ambiguity, support request, and reversal triggers.
- P2 permits no inference from executable licence to output rights and requires
  LOOT executable, libloot, masterlist, prelude, and userlist to remain
  separate assets.

### Check C3 — OpenAI reference-provider data boundary

Procedure:

1. Fetch O1 from the official OpenAI developer-documentation service.
2. Inspect the data-use statement, abuse-monitoring defaults, application-state
   table, `/v1/responses` notes, ZDR/MAM approval, prompt caching, provider
   tools, and third-party transmission notes.
3. Inspect O2 sections 3–4 and the displayed effective/update dates.
4. Inspect O3's dated model-shutdown table.
5. Make no API request and create no provider application state.

Observed facts:

- OpenAI states that API data is not used to train/improve models unless the
  customer opts in.
- Default abuse-monitoring logs may include prompts/responses and derived
  metadata and may be retained up to 30 days, subject to stated legal/harm
  exceptions.
- ZDR and MAM are approval-based controls, not defaults available merely by
  setting a request parameter.
- `/v1/responses` has 30-day application-state retention by default or when
  `store=true`, and response data is stored for at least 30 days. Under ZDR,
  `store` is forced false.
- Background Responses mode, audio output, prompt caching, hosted containers,
  remote MCP, and network-connected tools have distinct storage/third-party
  boundaries.
- O2 assigns Output to the customer as between the parties while making the
  customer responsible for rights in Input and use of Output.
- O3 demonstrates that a recorded model snapshot/alias may later become
  unavailable. Recording the slug does not preserve the executable model.

### Check C4 — local game-byte negative boundary

Procedure:

1. Inspect B1's English software-use, archive/backup, copying, transmission,
   and distribution clauses.
2. Do not inspect or copy any installed game file.

Observed:

- B1 supplies no blanket right for Infinium to duplicate game bytes into a
  product evidence store, send them to a model provider, or redistribute them.
- A source reference, local path token, size, version, and fingerprint can
  support change detection without putting the exact game payload into a
  shareable artifact.

### Artifact manifest

| Artifact | Retention | Redistribution treatment |
|---|---|---|
| This investigation | Proposed tracked Markdown | Project-authored synthesis with direct source links; no private/source body retained. |
| Official policy/contract/docs pages | Not retained as bodies | URL, displayed version/date, retrieval date, and paraphrased claim-level observations only. |
| Provider requests/responses | None created | Not applicable. |
| Game/mod/config/tool/model bytes or outputs | None accessed or retained | Not applicable. |
| Credentials/private profile data | Not accessed | Not applicable. |

## 6. Verified facts versus policy interpretation

### 6.1 Verified facts

1. The accepted product baseline requires exact provenance but explicitly does
   not require copying every source byte.
2. The accepted baseline distinguishes private history, run-owned artifacts,
   user-created exports, sensitivity-labeled trace exports, and an M4
   reviewed diagnostic bundle.
3. The accepted baseline requires explicit independent-copy discovery and
   confirmed deletion cascades.
4. P1 blocks Nexus HTML scraping and identifies an unresolved supported-API
   analysis boundary; ADR-0005 authorizes bounded user-initiated API analysis,
   necessary private retention, and minimized optional inference while
   preserving external redistribution as a separate decision.
5. P2 establishes that LOOT executable, libloot, CC0 masterlist, CC0 prelude,
   private userlist, and tool output are separate rights/provenance classes.
6. The OpenAI reference provider has endpoint/tool-specific application-state
   behavior in addition to abuse-monitoring retention.
7. OpenAI output ownership between provider/customer does not discharge the
   customer's Input-rights and Output-use responsibilities.
8. OpenAI may retire a recorded model snapshot or alias; Infinium cannot retain
   an executable copy of a hosted model merely by recording its name.
9. B1 does not grant a blanket right to redistribute or transmit game bytes.

### 6.2 Recommended interpretations

1. **Metadata-first is the durable general default after required work is
   materialized.** It must not remove permitted source material before useful
   dependent analysis and audit complete. Exact-byte retention is an
   adapter/source-specific capability, not a property of the evidence store.
2. **Boundary outputs are first-class replay dependencies.** Retaining a
   provider/tool output can preserve exact downstream replay without promising
   clean regeneration of that output.
3. **Private export is not shareable export.** It can be richer only when
   private copying is permitted and explicitly selected, and it must carry a
   conspicuous no-redistribution/sensitivity declaration.
4. **Derived content still needs review.** A tool/model result may reproduce
   protected or private input. "Generated by a tool/model" is not a licence.
5. **Hosted model identity is not an executable version archive.** Store the
   exact requested and resolved model identifiers when exposed, settings,
   endpoint, API/adapter version, response, and deprecation evidence; disclose
   clean-rerun fragility.
6. **A local installed mod is local-state input, not a Nexus source adapter.**
   Infinium may observe the user's local installation within the accepted
   product authority and applicable rights, but must not use that fact to
   reconstruct or acquire unsupported Nexus page content. Documentation
   acquisition must use the supported API boundary accepted by ADR-0005.

## 7. Evidence-class retention, replay, and export matrix

Matrix cells use `B`, `X`, `D`, `M`, and `O` from §3.3. A qualifier such as
`B*` means exact content is allowed only after the stated source/private-copy
decision. `—` means excluded. All classes exclude credentials.

| Evidence/artifact class | `P` private state | `R` run-owned output | `L` private/local export | `S` externally shareable report/MD/HTML/JSON | `T` sensitivity-labeled trace export | `BND` M4 diagnostic bundle | Replay consequence / governing rule |
|---|---|---|---|---|---|---|---|
| Installation-snapshot manifest, object identities, versions, sizes, timestamps, source references, fingerprints | `M` required | `M` selected summary | `M` | `M` after path/privacy minimization | `M`, may include sensitive paths only when explicitly selected | `M` redacted/tokenized | Enables dependency comparison, not byte-level recomputation by itself. |
| Exact local game/mod/archive/generated-output bytes | Reference by default; `B*` only when private copying is permitted, necessary, isolated, and selected | `D/M/O`; no payload by default | `B*` only for permitted private copy, with no-sharing label | `D/M/O`; never exact restricted payload | `B*` only for private trace purpose and explicit selection | `D/M/O`; exact bytes only with affirmative redistribution right and need | Without bytes, DR depends on the original matching installation; BR may still work from retained parsed/tool outputs. |
| User-created configuration, MO2 profile data, `userlist.yaml`, notes, symptom reports, logs | `B` permitted as private user input when in scope; minimize secrets/paths | `X/D/M` task-relevant | `B/X/D/M` explicitly selected | `X/D/M/O` sanitized and intentionally shared by user | `B/X/D/M` sensitive/private by default | `X/D/M/O` reviewed/redacted | Exact local replay may require bytes. User ownership/control does not erase third-party/private data embedded within them. |
| Parsed local observations, provider chains, record structures, normalized config, deterministic indexes | `B/D/M` | `D/M`, selected inspectable detail | `B/D/M` | `D/M`; exclude expression-heavy copied payloads | `B/D/M` under sensitivity label | `D/M`, reviewed | Usually best replay boundary: compact relative to sources, but parser/analyzer version remains required to recompute. |
| Source URL/entity/revision/date/status/content fingerprint/policy decision | `M` required where permitted | `M` | `M` | `M/O`, subject to link/citation policy | `M` | `M/O` reviewed | Supports identity/audit and reacquisition attempt; does not reproduce deleted body. |
| Exact author/source cited excerpt | `X*` when acquisition and private excerpt retention are permitted and necessary | `X*` or `O` | `X*` | `X*` only when quotation/redistribution policy permits; otherwise `D/M/O` | `X*` | `X*` only after source-policy review; otherwise `D/M/O` | Deleting excerpt reduces citation audit and clean extraction; a claim may survive with fingerprint/omission marker and explicit gap. |
| Full external source body/HTML/PDF/media | `B*` only with source-specific permission and necessity | `—` by default; `M/O` | `B*` private only | `—`; exact body only under affirmative redistribution licence and selected purpose | `B*` private only | Normally `—`; exceptional affirmatively licensed selection | Largest source replay benefit and disk/privacy risk. Do not retain merely because retrieval succeeded. |
| Nexus supported-API content | `B/X/D/M` only as necessary for bounded private extraction, deterministic/user-authorized LLM analysis, claim/case/finding synthesis, prose, provenance, audit, replay, refresh, and applicable private history under ADR-0005; page/scraped content excluded | `X/D/M/O`, with raw bodies omitted by default | `B/X/D/M` private and explicitly selected | `D/M/O`; permitted short citation only after export-policy review, never raw-body republication | `B/X/D/M` sensitivity-labeled and private by default | `D/M/X/O` after source/privacy review | Exact private API evidence can support useful analysis and extraction audit/BR; deletion or unavailable refresh must produce explicit completion/replay/citation/audit gaps. |
| LOOT Skyrim SE masterlist and prelude at pinned commits | `B/M` permitted under checked CC0 terms; pin each independently | `D/M`, excerpts where useful | `B/D/M` | `B/X/D/M` legally plausible under CC0, but provenance/integrity still required | `B/D/M` | `B/X/D/M` reviewed | Exact bytes + commit + syntax/tool inputs support DR. Moving branch name alone is insufficient. |
| LOOT userlist/local LOOT state | `B/M` as private user input | `D/M`; exact rules only if needed and labeled private | `B/X/D/M` explicitly selected private | `D/M/O` sanitized; exact user rules only through deliberate user sharing and embedded-rights review | `B/X/D/M` sensitive | `D/M/O` reviewed/redacted | Required for faithful LOOT recomputation; deleting it may leave BR from retained LOOT output. |
| Tool executable/library/package bytes | `M` default: detected/requested version, hash, source/ref, licence posture; `B*` only under per-artifact private archive permission and need | `M` | `M`; binary payload normally excluded | `M/O`; never include executable merely for replay | `M`; payload excluded unless separate permitted private purpose | `M/O` | Exact hash/version alone makes a still-installed copy verifiable but does not preserve availability. Executable archive can improve DR but creates licence, malware, disk, update, and deletion obligations. |
| LOOT/approved-helper command, environment/config, stdout/stderr, structured output, tool-local manifests | `B/D/M` when generated by authorized operation; inspect embedded input/source rights | `B/D/M` as run-owned result | `B/D/M` private | `D/M/X/O`; exact output only when redistribution-cleared and privacy-safe | `B/D/M` sensitive | `D/M/X/O` reviewed | Retained exact output enables BR. Tool licence does not automatically license copied upstream/game/user material in output. |
| Prompt template, prompt-builder revision, system/developer instructions, output schema, adapter code identity | `B/M` required for exact provenance; use project-owned/licensed content | `M`, selected safe prompt identity | `B/M` | `B/M` if project-owned and no secrets/restricted embedded content | `B/M` | `B/M` reviewed | Required for DR/RA comparison; provider-hosted prompt objects are insufficient without a retained local immutable representation. |
| Exact model request and structured context | `B/M` when every included source is permitted for private retention/provider use; minimized | `D/M` by default; exact content only if run-owned output contract explicitly includes it and labels sensitivity | `B/M` explicit private selection | `D/M/O`; exact request only after every embedded source/privacy right clears | `B/M` sensitive | `D/M/O`; exact only after review | Required to attempt clean model reproduction. It does not make nondeterministic output byte-repeatable. |
| Provider tool inputs/results: web search, file search, remote MCP, hosted shell/container output | `B/D/M*` per upstream source, provider tool, third-party, and retention policy | `D/M/X/O` | `B/X/D/M*` private | `D/M/X/O*` only with source rights | `B/X/D/M*` sensitive | `D/M/X/O*` reviewed | Each tool is a separate source boundary. Provider output ownership does not clear third-party result content. |
| Exact model response and returned structured/tool/reasoning items | `B/M` when input/provider/source policy permits; retain opaque encrypted reasoning items if the chosen contract needs stateless continuation | `D/M` or selected `B` under labeled run contract | `B/M` private | `D/M`; exact response only after source/privacy/accuracy review | `B/M` sensitive | `D/M/X/O` reviewed; exact only when safe and useful | Exact response enables BR. Clean re-call is RA and may differ. Opaque encrypted state is not human-readable evidence and must retain provider/schema dependencies. |
| Provider/model/endpoint/project data-control/request settings, prompt/schema/tool/adapter versions | `M` required | `M` | `M` | `M` with account/project identifiers tokenized | `M` | `M` redacted | Required to explain behavior. A model slug is not a retained executable and may later be unavailable. |
| Token, cache-token, request, duration, cost, limit, reservation, reconciliation metadata | `B/M` required ledger; exclude credentials/billing secrets | `D/M` | `B/M` private | `D/M` aggregated/redacted | `B/M` sensitive | `D/M` reviewed | Audit/cost replay only; cannot regenerate semantic output. Provider system/billing data may have distinct retention. |
| Checkpoints and reusable caches | `B/D/M` only with declared ownership, dependencies, source rights, and TTL/retention policy | `M` hit/miss/result references | `M`; payload normally excluded | `M/O` | `B/M` private if explicitly selected | `M/O` | May restore same non-terminal run or seed explicit reuse in a new run. Cache hit is not evidence of source permission or replay completeness. |
| Run-owned human-readable CLI and versioned JSON | `B/M` independently retained execution artifacts | `B` | Can be selected into a new `L` export, creating another copy | Cannot be relabeled `S`; regenerate through export policy | Can seed `T`, creating another copy | Can seed `BND` after review | Helpful audit/BR presentation boundary, but may duplicate restricted/sensitive content and must be discovered in deletion preview. |
| Raw candidates, intermediate evidence, abstentions, failures, developer traces | `B/D/M` required in development within active policy | Selected trace summary only; trace remains separate | `B/D/M` explicit private | `D/M/O` only through reviewed export generator | `B/D/M` explicit and sensitivity-labeled; external intent adds `S`/`BND` constraints | `D/M/O` reviewed/redacted | Supports diagnostic audit and analyzer replay; potentially high-volume and high-sensitivity. |
| Claims, candidates, hypotheses, findings, cases, recommendations, coverage | `B/D/M` | `D/M`, selected detail | `B/D/M` | `D/M/X/O`; include citations only as allowed | `B/D/M` | `D/M/X/O` reviewed | Derived output can survive source deletion but must expose citation/audit/replay loss and never upgrade authority. |
| Readiness evaluations, policies, dispositions, suppression, review annotations and revision history | `B/D/M` | `D/M` applicable run-time view | `B/D/M` | `D/M` with private annotations excluded/redacted by default | `B/D/M` sensitive | `D/M` reviewed | Recomputable only if run, policy, and exact applicable review-state inputs survive. Deletion never rewrites older retained/exported evaluations. |
| User-created export plus exact selection/omission/redaction/source-policy manifest | `B/M` as independently retained export record | Reference only | `B/M` | `B/M` for the actual cleared artifact | `B/M` | `B/M` | Export is a snapshot, not a live view. Deleting/regenerating it does not mutate sources; deleting sources does not silently delete it. |

## 8. Acquisition × retention × model transmission × redistribution matrix

`Allowed` below means allowed for the narrow described path under current
evidence, not a general legal conclusion. `Conditional` requires the named
rights/policy and explicit product controls. `Blocked` means Infinium must not
perform the operation. `Unknown` is operationally blocked until resolved.

| External/input class | Acquire or inspect | Retain exact bytes privately | Send to selected model/provider | Redistribute externally | Actionable boundary |
|---|---|---|---|---|---|
| Nexus content exposed through a documented supported API | Allowed under ADR-0005 when user initiated, bounded to relevant mods, authenticated/identified correctly, and policy-current | Conditional and minimized, but retained long enough for useful extraction, analysis, case/finding synthesis, prose, provenance, audit, replay, refresh, and applicable private history, with deletion controls | Conditional, minimized, disclosed, credential-free, and inference-only; never training/fine-tuning/validation | Derived findings and attribution by default; raw bodies excluded; quotation/link rules remain source/export-policy inputs | Stop unsupported surfaces, scraping, bulk/rehost behavior, or any operation outside ADR-0005; review on Nexus response or policy change. |
| Public Nexus policy/API specifications used to establish guardrails | Allowed as bounded public documentation review | Metadata/synthesis; no body retained by default | Not needed | Project-authored synthesis and short permitted quotation/reference only | Keep policy research separate from mod-content acquisition. |
| User's already installed local mod/archive bytes, including a file originally downloaded from Nexus | Inspect under local-installation/product authority and applicable user/source rights; not a Nexus source adapter | Conditional; metadata/fingerprint/reference default, exact copy only when source/private-copy right and necessity are established | Conditional and minimized; origin alone supplies no right and does not substitute for a supported documentation interface | Conditional on mod/file licence and embedded rights; default no exact bytes | Analyze local state without treating origin as source-acquisition or redistribution permission. |
| Skyrim/game/Creation content and official binaries | Inspect installed state as required by accepted scope, subject to supported parsing/legal review | Reference/fingerprint default; exact duplicate only if specifically permitted/necessary | No by default; send only minimal derived observations where source rights and provider use permit | No exact bytes; derived observations/metadata only | B1 rejects a blanket copy/transmission/distribution assumption. |
| User-created MO2 configuration, LOOT userlist, notes, logs, and locally authored documents | Allowed when manually initiated/in scope | Allowed as private user data, subject to privacy, third-party embedded content, and deletion | Conditional on explicit task, minimization, provider disclosure, and embedded-content rights | User may deliberately export sanitized/selected content; no automatic sharing | Treat as private input, never curated LOOT or author data. |
| Bundled documentation inside an installed mod | Allowed by `DOC-007` as local input, subject to file/source terms | Exact excerpt/body conditional; fingerprint/reference default | Conditional on file/source terms and minimization | Conditional; default derived claim + citation/fingerprint/omission | Bind to supplying installation snapshot and acquisition run. |
| LOOT Skyrim SE masterlist and prelude at P2's pinned commits | Allowed through official repositories under checked CC0 data licences | Allowed with exact commit, content hash, retrieval, and syntax provenance | Allowed from licence perspective; still minimize and preserve untrusted-data boundary | Allowed from checked CC0 perspective; preserve provenance and do not misrepresent modifications | Tool/data versions remain separate. Availability and integrity are not guaranteed by CC0. |
| LOOT userlist/local LOOT state | Allowed as user-controlled local input | Allowed privately | Conditional/minimized and user-authorized | Not by default; only deliberate sanitized sharing | Never inherit curated-data authority or CC0 status. |
| LOOT/approved-helper output over local/source inputs | Allowed only after the owning integration RQ proves an authorized read-only operation | Allowed privately if output/source terms permit and embedded data is classified | Conditional on embedded input rights and minimization | Conditional; tool licence alone does not clear output | Retain exact command, tool hash/version, inputs, output, and cache/temp effects separately. |
| Tool executable/library/package | Detect/hash/invoke only after integration approval; MO2 and LOOT remain user-installed under ADR-0006, while ADR-0007 excludes xEdit | Metadata/reference default; exact private archive conditional on licence and source/private-copy posture | Not applicable as model context; never upload binary merely for versioning | Only under exact software licence/source/notices duties; excluded from reports/diagnostics | User-installed applications, bundled libraries, and managed data follow ADR-0006/ADR-0007 and their owning integration gates. |
| Approved non-Nexus author/official web source | Unknown until RQ-010/source registry approves exact access method and content class | Conditional on source licence/terms and necessity | Conditional on source + provider permissions | Conditional on quotation/licence/citation rules | No blanket "public page" policy. Stop each unsupported adapter. |
| OpenAI API request/response | Customer may submit only Input for which it has necessary rights under O2 | Infinium may retain its own permitted exact request/response privately under accepted `AI-006`; upstream source/privacy rules still govern | The request itself is the authorized transmission; endpoint/tool/subprocessor data controls must be disclosed and pinned | Output belongs to customer as between OpenAI/customer, but redistribution remains conditional on upstream source/privacy/accuracy/third-party rights | Use provider-neutral policy; see §9 for current OpenAI-specific behavior. |
| OpenAI Web Search, remote MCP, hosted/network tool results | Acquire only through explicitly configured capability and each third-party/source policy | Conditional by result source and provider/tool retention | Tool transmission follows the selected provider/tool path and third-party terms | Conditional; no blanket right from model Output assignment | Record each tool/source hop and its own policy/version; do not collapse into "model output." |

## 9. Initial OpenAI reference-provider boundary

This section is provider-specific evidence for an adapter. The core product
contract remains provider-neutral and must represent the same fields as
capabilities, not assume OpenAI defaults.

### 9.1 Current facts needed by the retention matrix

| Boundary | Current official OpenAI statement | Infinium consequence |
|---|---|---|
| Training use | API data is not used to train/improve OpenAI models unless the customer explicitly opts in. | Record opt-in state where reliably observable; do not describe "not trained on" as "not retained." |
| Abuse monitoring | Logs may include prompts/responses/derived metadata and are retained up to 30 days by default, subject to stated exceptions. | Pre-run disclosure must say provider-side customer content may persist even with local deletion and `store=false`. |
| MAM/ZDR | Eligible customers require prior OpenAI approval and additional requirements. ZDR excludes customer content from abuse logs and forces Responses/Chat Completions `store=false`, subject to documented limitations. | Never promise ZDR from a local toggle. Adapter capability must be verified for the selected organization/project/model/endpoint. |
| `/v1/responses` application state | 30 days by default or when `store=true`; response data stored for at least 30 days. | Proposed default for a minimal M1 adapter is `store=false`, but this does not remove default abuse-monitoring retention. Record requested/effective storage behavior. |
| Responses background/audio/cache/container features | Background mode writes roughly ten minutes for polling; audio output retains one hour; prompt cache application state may persist up to 24 hours; hosted containers persist while active then delete on expiry/deletion. | Each feature changes the retention manifest. Keep unneeded hosted/stateful features disabled; do not infer one endpoint-wide period. |
| Conversations/files/vector stores/batches and other stateful endpoints | Several retain application state until deleted and may be ZDR-ineligible. | Do not add them to the initial adapter merely for convenience. Each needs an explicit retention/deletion/provider-capability review. |
| Remote MCP/network services | Data sent to third-party services follows those services' retention policies. | Create an additional source/provider hop; OpenAI terms do not replace that review. |
| Input/Output rights | O2: customer retains Input rights and owns Output as between OpenAI/customer; customer warrants Input rights and is responsible for Output use. | Retain permitted exact Output privately, but never use Output ownership to cure Nexus/mod/game/tool/source restrictions. |
| Model availability | O3 publishes shutdown dates for model snapshots and aliases. | Retain requested/resolved model identity and response. A retired snapshot turns clean provider rerun into RA/unavailable, while BR may remain complete. |

### 9.2 Proposed minimal adapter posture

1. Use a provider-neutral request/response envelope and record:
   provider, account/project pseudonymous identity, endpoint, region where
   applicable, adapter/API version, requested and provider-returned model
   identity, storage flag/effective policy, tools, settings, schema/prompt
   versions, token/cache/cost metadata, and exact permitted request/response.
2. Default OpenAI Responses calls to `store=false` for local product needs
   unless a later reviewed feature requires server-side state.
3. Disclose that `store=false` does not equal ZDR and does not disable the
   default abuse-monitoring boundary.
4. Do not claim ZDR/MAM unless the adapter can verify the active organization
   and project setting and model/endpoint eligibility.
5. Do not use stateful conversations, files, vector stores, background mode,
   remote MCP, hosted containers, web search, or other provider tools until
   their input rights, provider/third-party retention, deletion, cost, and
   replay behavior are in the operation manifest.
6. Preserve returned encrypted reasoning/tool items only as opaque provider
   boundary outputs when required for stateless continuation/replay. They do
   not become inspectable evidence or a replacement for cited reasoning.
7. Treat provider-side deletion and local deletion as distinct asynchronous
   operations. Local history must not claim remote deletion unless the provider
   confirms it through a supported interface.

## 10. Replay-dependency matrix

| Boundary/result | Exact dependencies for DR | Exact dependencies for BR | When replay is complete | When partial | When unavailable / RA only |
|---|---|---|---|---|---|
| Installation snapshot capture | Exact snapshot algorithm/version; source files or a matching live installation; MO2/profile/root/config/provider state; declared environment semantics | Retained immutable manifest plus any retained parsed observations needed downstream | Every required snapshot population and fingerprint validates | Some source populations remain, others are deleted/unreadable/unsupported | No matching source state or retained downstream boundary for requested scope |
| File/archive/config parsing | Exact input bytes; parser/ruleset/schema; settings; relevant runtime/environment | Retained normalized parsed output + schema/version reader | All requested parsed structures reproduce or are replayed | Some formats/records replay; source bytes or parser missing elsewhere | Neither input/parser nor retained normalized boundary exists |
| External documentation acquisition | Exact retained permitted source body/revision and adapter | Retained source bytes/excerpt or retained extracted claim boundary, according to requested layer | Requested source/extraction layer is entirely retained and integrity-valid | Citation metadata/claims survive but body/excerpt or some entities are absent | Live reacquisition is the only route; that is a new acquisition run/RA |
| Claim extraction | Exact permitted source bytes/revision; extractor/prompt/schema/model/tool inputs and versions | Retained exact structured claim output, supporting permitted excerpt/fingerprint, and extractor identity | All claim outputs can be consumed exactly downstream | Claims survive with deleted passage/audit gap, or only some source units remain | No source and no retained extraction output |
| LOOT deterministic invocation | Exact LOOT executable/hash/version; masterlist, prelude, userlist, game/profile inputs, config/locale/environment, command | Retained exact normalized LOOT output plus all provenance identities | Full configured result can be rerun or boundary-replayed | Output survives but clean invocation dependency is missing, or some diagnostics were not retained | Neither runnable exact inputs/tool nor retained output exists; replacement LOOT is RA |
| Approved-helper invocation | Exact executable/hash/version; scripts/arguments; local inputs; environment; all relevant config | Retained exact normalized tool output and command/provenance | Same rule as LOOT for declared result scope | Only retained output or only some input branches survive | Only a new/replacement helper invocation can regenerate evidence |
| Deterministic analyzer/index | Exact code/ruleset/taxonomy/schema; input boundaries; configuration; dependency graph | Retained analyzer output under readable versioned schema | Exact downstream analytical artifacts reproduce | Some upstream boundary or analyzer version is missing but retained results remain inspectable | Neither runnable logic/input nor retained output remains |
| Model call | Exact permitted request/context; endpoint/provider; requested/resolved model; settings; tools; prompt/schema/adapter versions; provider availability | Retained exact response and all required returned tool/reasoning items plus schema reader | BR is complete when downstream uses retained response; clean RA availability is disclosed separately | Response exists but cited/provider-tool input or some returned item was deleted; downstream unaffected branches replay | No response for BR; retired/unavailable model means only replacement RA, never old-call replay |
| Provider built-in/remote tool call | Exact tool input, tool/provider/third-party identity, source revision/response and settings | Retained exact permitted tool result plus source/policy provenance | Every selected result boundary survives | Result survives without source body/citation, or some tool branches are absent | Recontact is RA and governed by current third-party policy |
| Job resume from checkpoint | Same non-terminal run bindings; exact checkpoint; scheduler/job/schema version; outstanding dependency state; current authorization/budget permits dispatch | Not applicable: resume executes work rather than replaying a terminal result | Paused run continues without binding changes | Completed units reusable but same-run resume is broken; a new run may consume them through validated reuse | Terminal run cannot resume; no valid checkpoint remains |
| Cached artifact reuse | Exact artifact bytes; cache key/dependency proof; producing versions; consumer schema | Same retained artifact is the boundary | Consumer validates every dependency and records reuse edge | Some cached branches invalid/deleted and recompute/skip is explicit | Cache absent/invalid; recomputation or gap required |
| Findings/cases/recommendations | Exact upstream evidence boundaries; analyzer/model outputs; promotion/taxonomy logic; context/config | Retained immutable typed analytical outputs and lineage | Historical results render and downstream projections reproduce | Conclusion survives but citations/audit inputs are missing | Result object deleted; no other copy may silently recreate historical identity |
| Readiness evaluation | Exact run/coverage; readiness policy version; applicable disposition set; evaluation time semantics | Retained immutable readiness evaluation and source identities | Historical evaluation renders exactly or recomputes from exact inputs | Evaluation survives but underlying run evidence has gaps | Evaluation deleted and exact inputs absent; newer evaluation is not replay |
| Run-owned CLI/JSON | Exact run objects plus generator/schema/version | Retained artifact bytes + integrity metadata | Generator reproduces or bytes replay | Artifact survives but source objects are deleted, or sources survive but generator is unavailable | Both artifact and sufficient source/generator inputs absent |
| User-created export | Exact selected source object revisions; filters; sharing class; policy/redaction decisions; generator/schema/version | Retained export bytes plus immutable selection manifest | Export bytes replay or regenerate identically from exact selection | Export survives with subsequently deleted sources, or sources survive but generator/policy version is absent | Export deleted and selection cannot be regenerated; another export is a new artifact |

### 10.1 Hosted-model version guarantee

Infinium can guarantee that it retained the **declared provider/model identity
and exact response**, not that the provider will continue serving that model.
For an alias, record both the requested alias and any resolved model identifier
returned by the provider. If the provider exposes no stable resolved snapshot,
record that capability gap. When a model is retired:

- BR from its retained response can remain complete;
- a clean rerun of the old boundary becomes unavailable;
- a replacement-model call is a new run/revision and RA;
- semantic comparison belongs to evaluation, not historical replay; and
- surviving audit history is unchanged except for a current availability
  disclosure.

## 11. Deletion, cascade, and independently retained-copy rules

### 11.1 Required object-graph behavior

1. **Preview before deletion.** Resolve the exact selected objects, ownership
   class, storage location, dependent runs/jobs/citations/caches/findings,
   replay/audit effects, and independent copies.
2. **No implicit source-to-copy cascade.** Deleting a source object does not
   delete a run-owned output, export, trace export, diagnostic bundle, backup,
   or other artifact containing rendered bytes.
3. **No implicit copy-to-source cascade.** Deleting an export or run-owned
   artifact does not delete its selected sources.
4. **Every cascade is inspectable and confirmed.** The user may explicitly
   include a dependency closure or independent-copy closure, but the preview
   enumerates classes/counts and permits deselection before confirmation.
5. **Shared physical blobs use logical ownership.** Deduplicated storage may
   remove a physical blob only when every retained logical reference whose
   policy permits access is included in the deletion. Refcount implementation
   remains an architecture choice.
6. **External copies are reported, not silently controlled.** A user-selected
   export outside product-controlled storage, provider-side application state,
   user backup, or manually copied run artifact may not be deletable by
   Infinium. Record the limitation and any supported remote-deletion state.
7. **Active/paused work is protected.** Preview whether deletion invalidates,
   cancels, or prevents resume. No checkpoint/input is deleted out from under
   a run without the explicit selected effect required by `OPS-002` and
   `SCAN-005`.

### 11.2 Surviving audit history

Deletion creates a non-content-bearing event sufficient to state:

- deleted object identity/type or a permitted pseudonymous replacement;
- deletion time and initiating authority;
- direct versus confirmed-cascade selection;
- payload/excerpt/fingerprint/reference classes removed;
- affected dependencies, citations, checkpoints, reuse, and replay scopes;
- replay/audit status before and after; and
- independent copies that remain or could not be controlled.

The deletion event does not preserve deleted payload through a log message,
error, trace, export preview, thumbnail, full path, prompt, or exception dump.
If a fingerprint or source identifier is itself selected for deletion or is
sensitive, the surviving marker retains only the minimum permitted fact that a
dependency was removed. "Preserve a fingerprint" is not an override of an
explicit deletion scope or source policy.

Surviving immutable runs/findings/evaluations are not rewritten. Their current
views gain an audit/replay gap linked to the deletion event. An exported old
view remains an independent historical copy; the product cannot retroactively
edit it.

### 11.3 Cascade effects by class

| Deleted class | Direct effect | Surviving independent copy rule |
|---|---|---|
| Exact source body/file | Clean acquisition/parsing/extraction may become partial/unavailable; retained claims/parsed outputs can survive with gap | Exports/run outputs/traces containing bytes remain until explicitly selected; preview identifies them |
| Exact cited excerpt | Claim audit loses inspectable support; fingerprint/reference/claim may survive | Any rendered quotation in another artifact is a separate copy |
| Tool executable archive | Clean DR may depend on still-installed matching tool or become unavailable; prior output unchanged | Installer/package copies outside product control are disclosed where known, not deleted implicitly |
| Tool output | BR from that boundary degrades; DR may still recreate it if exact tool/inputs survive | Copied output in run JSON/export/trace remains separately |
| Model exact request/context | Clean RA loses exact input; retained response may remain BR-capable but with audit gap | Request embedded in a trace/private export remains separately |
| Model response/returned items | BR for dependent model stage degrades or fails; re-calling is a new run | Rendered model text in findings/run output/export/trace remains separately |
| Cache | Performance/reuse changes; historical source/output need not change | A promoted run-owned result is not deleted as "cache" |
| Checkpoint | Same-run resumability may be lost; completed immutable results remain | A reusable completed artifact is selected independently from transient checkpoint state |
| Finding/case/recommendation | Object is absent from current retained history; sources are not deleted | Exported/rendered copies remain; logical lineage must show an allowed deletion marker rather than reidentifying history |
| Review annotation/disposition | Later readiness recomputation may be impossible or different; prior readiness remains immutable | Prior export/readiness evaluation may contain the old value |
| Run-owned CLI/JSON | No source deletion; one audit/rendering copy disappears | Other output formats, exports, and traces remain independent |
| Export/trace/bundle | Source run/evidence remains unchanged | Other exports and local/manual copies are independent |
| Provider-side state | Local record shows requested/confirmed/failed/unknown remote deletion; local evidence is not silently deleted | Provider retention/legal exceptions and third-party tool copies remain provider-specific |

## 12. Practical disk and retention implications

No numeric budget is justified before RQ-014/RQ-027 measurement. The evidence
store decision should nevertheless plan for these qualitative cost tiers:

1. **Low relative cost:** identities, versions, hashes, URLs, dependency edges,
   coverage, policy decisions, token/cost records, and deletion markers.
2. **Moderate but high-cardinality cost:** parsed observations, file/provider
   indexes, record structures, candidates, claims, findings, and JSON outputs.
3. **Potentially large cost:** exact external/local documents, model contexts
   and provider-tool results, verbose traces, repeated run-owned output, and
   checkpoints.
4. **Potentially dominant cost:** copied mod/game archives, extracted assets,
   executable packages, full-source bodies/media, and many independently
   rendered exports/bundles.

Recommended practical controls:

- metadata/fingerprint/reference default with selective exact-byte promotion;
- content-addressed deduplication only behind logical ownership and deletion
  accounting;
- per-object byte size and inclusive/exclusive retained-size estimates;
- a preview that includes independent copies and projected replay changes;
- separate retention policies for source bodies, boundary outputs, caches,
  checkpoints, traces, and exports rather than one age-based purge;
- compression/encryption treated as storage/security mechanisms, not rights;
- no automatic promotion of transient provider/tool cache into durable
  evidence;
- no automatic deletion of immutable run evidence merely because a cache TTL
  expired;
- disk-pressure behavior that pauses/skips new work or asks for a new explicit
  retention choice rather than silently discarding dependencies; and
- measured M1/M3 defaults from RQ-014/RQ-027, not invented quotas.

## 13. Alternatives evaluated

| Alternative | Advantage | Material failure/rejection criterion |
|---|---|---|
| Retain every input, executable, request, output, and trace indefinitely | Maximum potential clean recomputation and forensic detail | Reject: contradicts source-specific rights, context minimization, disk practicality, independent-copy deletion, provider boundaries, and P1. |
| Retain only fingerprints and final reports | Small, privacy-minimizing store | Reject: cannot support exact cited passages, extraction review, tool/model BR, development transparency, or complete M1 fixture replay. |
| Retain all boundary outputs but reference source/executable bytes | Strong downstream BR at lower disk/licensing exposure | Recommended default pattern, but clean DR becomes partial/unavailable and citation audit may suffer; must disclose it. |
| Retain exact permitted sources and tools for every evaluation fixture; retain permitted personal-run sources through useful dependent work, then apply metadata-first durable minimization | Strong reproducibility where private-retention rights are reviewed without sacrificing ordinary analysis quality | Accepted general pattern, but longer-term real-mod/game/tool byte retention and disk remain source- and purpose-specific. |
| Depend on provider-side stored Responses/conversations/files | Reduces local state management and can simplify multi-turn workflows | Reject as core replay store: endpoint retention/deletion varies, ZDR compatibility differs, model/service availability changes, and local history must remain provider-neutral/offline-capable. |
| Store only OpenAI model slug and re-call on replay | Minimal local model data | Reject: nondeterminism and O3 retirement make this RA, not replay. |
| Treat a private/local export as the user's responsibility and allow every retained byte | Simple implementation | Reject: export is an independently retained product artifact; source/private-copy limits, sensitive content, credentials, deletion, and intended sharing class still apply. |
| One generic "shareable" flag on every object | Easy filtering | Reject: accepted requirements distinguish six authority/sharing contexts and content may be permitted as metadata/derived form but not exact bytes. |
| Automatically cascade source deletion through all derived artifacts | Strong erasure intuition | Reject: violates explicit independent-copy selection, immutable historical artifacts, and export/source independence. Offer an inspectable selectable closure instead. |
| Never cascade deletion | Minimizes accidental loss | Insufficient: users need a usable way to remove all selected independent copies. Provide an explicit previewed closure. |
| Treat all tool/model output as project-owned and redistributable | Simplifies reports | Reject under P2/O2: output may reproduce third-party/game/mod/user/source content; provider assignment is only between provider/customer. |

## 14. Contrary evidence, uncertainty, and limitations

### 14.1 Contrary and boundary evidence

- Exact-byte retention can materially improve clean replay and source
  adjudication; metadata-first retention therefore has a real quality cost.
  The recommendation preserves selective promotion rather than banning bytes.
- GPL does not generally govern every byte emitted by a GPL tool, and OpenAI
  assigns Output to the customer as between the parties. Those facts oppose a
  blanket "never share output" rule. They still do not clear copied third-party
  material.
- A user may have source-specific permission to share a mod, document,
  screenshot, config, or log. The conservative default is not a finding that
  every such item is legally prohibited; it is a requirement to record the
  affirmative permission before exact external inclusion.
- A date-pinned hosted model is stronger provenance than a rolling alias, but
  O3 shows it is not a perpetual availability guarantee.
- Provider-side state can be operationally useful. This report excludes it
  from the proposed minimal adapter by default, not from every future reviewed
  capability.

### 14.2 Material uncertainty

1. No written Nexus clarification/exemption exists. ADR-0005 accepts a bounded
   supported-API interpretation for acquisition, necessary private retention,
   and minimized optional inference; the legal/policy ambiguity and exact
   external quotation/redistribution boundaries remain unresolved.
2. Mod licences, author-site terms, bundled-document rights, and user-created
   material vary. RQ-010 and RQ-025 must make exact source/fixture decisions.
3. B1 has no displayed revision date and does not resolve statutory
   interoperability, transient-copy, fair-use/fair-dealing, or jurisdictional
   rights. Counsel should review any design that durably duplicates game bytes.
4. ADR-0006 resolves the GPLv3-family product licence and the high-level
   application/library/data posture. The exact GPL selector and concrete
   integration mechanisms remain unresolved pending their stated gates.
5. Tool output can embed source bytes in tool-specific ways that RQ-005/RQ-006
   must inspect empirically.
6. No provider call tested actual `store=false`, ZDR/MAM state, returned model
   identity, deletion, tool-result retention, or billing metadata. O1/O2/O3
   establish documented behavior only.
7. OpenAI policies, endpoint capabilities, and model availability are mutable.
   Provider records need a dated policy/capability verification.
8. The exact minimum metadata that may survive a user's comprehensive deletion
   is a privacy/security/architecture question, especially when hashes or
   source IDs are personal/sensitive.
9. Backup, deduplication, encryption, secure erasure, provider deletion
   confirmation, and export discovery depend on the later selected storage
   architecture.
10. Practical disk tiers are qualitative. No compression ratio, object count,
    retention duration, or high-end cost was measured.

### 14.3 Unsupported cases

- No conclusion for another LLM provider.
- No automatic permission for public web search, remote MCP, or provider
  connector results.
- No exact Nexus cache duration or permitted excerpt size.
- No general maximum quotation length.
- No conclusion that a content fingerprint is non-infringing or non-personal
  in every context.
- No binary-archive or tool-output inspection.
- No numeric retention or deletion SLA.

## 15. Recommendation

Confidence: **High** for the product-class separation, metadata-first default,
boundary-output replay model, OpenAI documented retention facts, and
conservative Nexus/tool-output rules; **Medium** for exact private-copy
boundaries because they depend on source terms, local law, and future concrete
storage/integration facts.

### 15.1 Accepted M0 policy

1. Make a versioned **retention decision** part of every source, adapter, and
   artifact class. It records acquisition, private retention, provider
   transmission, redistribution, allowed forms (`B/X/D/M/O`), authority,
   policy/licence evidence, review date, and expiry/reverification trigger.
2. Default durable snapshots to references/fingerprints and derived structures
   after required dependent work is materialized. Promote exact bytes through a
   source-specific permission/necessity decision, and never apply the
   metadata-first default so early that useful extraction, analysis,
   case/finding synthesis, prose generation, provenance, or audit is impaired.
3. Retain exact permitted deterministic/tool/model boundary outputs needed for
   M1 replay. Prefer them over copying entire source trees or tool packages.
4. Store exact tool/library/executable version, content hash, detected path or
   immutable source/package reference, licence posture, command/config, and
   availability. Archive executable bytes only when private retention is
   affirmatively permitted/needed and every licence/security/deletion
   obligation is met.
5. Keep source bytes, excerpt cache, claims, tool/model outputs, run-owned
   output, trace, and export as distinct logical objects even when physical
   deduplication is later selected.
6. Give every run two replay disclosures: downstream replay and clean
   recomputation/reproduction availability, each with complete/partial/
   unavailable status and missing dependencies.
7. Treat recontacting a live source/provider or changing a model/tool as a new
   run/revision (RA), never as replay of the old boundary.
8. Implement the six sharing contexts in §3.2. Do not expose a generic
   "shareable" boolean.
9. Generate `S` and `BND` artifacts through allow-by-form/source decisions. Use
   `D/M/O` when exact bytes/excerpts are not redistribution-cleared.
10. Keep `T` exports sensitivity-labeled and record whether their intended
    sharing class is private or external. The label never replaces source
    review.
11. Use explicit graph-based deletion preview/cascade and independent-copy
    discovery. Preserve non-payload deletion/audit events without logging the
    deleted content.
12. For the OpenAI reference adapter, propose `store=false`, retain permitted
    exact local request/response, disclose default abuse-monitoring retention,
    and do not claim ZDR/MAM or use stateful/tool features without verified
    capability.
13. Apply ADR-0005 to Nexus content: supported-API acquisition and enough
    private retention to complete useful extraction, deterministic analysis,
    minimized user-authorized inference, claim/case/finding synthesis, prose,
    provenance, audit, replay, refresh, and applicable private history may
    proceed within its bounds. Page scraping, unsupported interfaces, model
    training, public corpus/rehosting, and raw external inclusion remain
    excluded. A negative Nexus response or another ADR-0005 reversal trigger
    stops and reviews the affected path.
14. Preserve P2 exactly: software, data, user input, executable version, and
    output rights remain separate.
15. Require qualified legal review before public/commercial release and before
    accepting ambiguous durable copies of game/mod/source/tool material.

### 15.2 Preconditions for an exact-byte class

An adapter may set `private_exact_bytes=allowed` only when its reviewed record
answers:

- exact source/entity/content class and acquisition method;
- rights/policy evidence and retrieval/review date;
- purpose and necessity of exact retention;
- allowed location, encryption/sensitivity, and access boundary;
- whether provider transmission is separately allowed;
- exact excerpt/full-body and external redistribution forms;
- freshness, TTL, or indefinite-retention rule;
- dependency and replay benefit;
- expected/observed size and duplication behavior;
- deletion and remote/independent-copy behavior; and
- stop/reverification conditions.

An unknown answer keeps the relevant axis disabled.

## 16. Explicit Wave A Gate A consequences

RQ-031 satisfies the retention/export portion of Gate A after independent
review and owner disposition because the accepted rule never infers
redistribution from private retention and classifies the tracked artifacts
required by M1.

It does not by itself accept P1 or P2. ADR-0005 resolves the project's
operational interpretation of P1, and ADR-0006 resolves P2's licensing and
high-level dependency posture. Therefore:

1. Wave D may perform bounded authenticated experiments against documented,
   supported Nexus operations under ADR-0005. Unsupported surfaces remain
   coverage gaps and may not fall back to page access.
2. Gate A's source-method clause can pass with the ADR-0005 boundary and
   documented residual risk. Written Nexus clarification remains a review
   trigger rather than a prerequisite to development.
3. Every Wave D source actually used must have an approved four-axis record;
   an approved access method alone is insufficient.
4. Every M1 external application, bundled-library candidate, and managed-data
   input follows ADR-0006 and remains ineligible for operation until its owning
   integration, authority, and exact-package gates pass.
5. RQ-013/evidence-store selection may now begin subject to the accepted
   logical object/separation, useful-analysis retention, replay, and deletion
   constraints here. It may not choose a schema that makes source bytes,
   boundary outputs, run output, traces, and exports indistinguishable.
6. This single investigation cannot declare the wave gate met. The revised
   integration review must combine ADR-0005, the four-axis retention rules, and
   helper postures; the expected source result is **Met with documented
   non-blocking policy risk**.

## 17. Exact downstream changes enabled

These are proposals for coordinator review. This investigation does not apply
them.

### 17.1 Proposed ADRs

1. **Evidence persistence, retention, replay, and deletion ADR** after RQ-013:
   select logical object classes, dependency graph, boundary-output replay,
   status calculation, dedup ownership, deletion events/cascades, and storage
   mechanism.
2. **Documentation source/provider transmission/export ADR** after RQ-010,
   RQ-011/RQ-012, and Nexus clarification or exclusion: select the four-axis
   policy record and enforcement boundary.
3. Feed the sharing-class/selection-manifest constraints into the later
   RQ-032 security/export-control ADR rather than accepting redaction mechanics
   here.

### 17.2 Proposed product/registry specification changes

1. Add a normative retention-policy vocabulary for `B/X/D/M/O`, the four
   permissions, and DR/BR/RA only after product review determines whether it
   belongs in requirements/domain or an accepted ADR-backed specification.
2. Extend the source registry record with:
   acquisition decision; private byte/excerpt decision; provider-transmission
   decision; external-redistribution decision; allowed retained/export forms;
   policy evidence/version/retrieval; review expiry; and replay/deletion
   consequences.
3. Register Nexus supported-API acquisition as conditionally allowed by
   ADR-0005. Record necessary private retention as conditional and
   source-specific; do not invent an unmeasured cache duration or permit raw
   external redistribution.
4. Add OpenAI provider capability fields for endpoint, `store`, abuse-control
   mode/evidence, stateful/tool application-state behavior, third-party hops,
   remote deletion, requested/resolved model, and policy verification date.

### 17.3 Proposed evaluation cases

1. Expand `EVAL-0021` so retained model/tool BR is complete while clean
   provider/tool recomputation can separately be unavailable.
2. Expand `EVAL-0025` with deletion of source bytes/excerpts, executable
   archives, and boundary outputs, asserting exact status transitions without
   history mutation.
3. Expand `EVAL-0040` across all six sharing contexts and all five content
   forms; assert that run-owned JSON cannot be relabeled as shareable.
4. Expand `EVAL-0041` with independently retained exports, run output, trace,
   diagnostic bundle, deduplicated blobs, provider state, and an external
   user-selected copy that Infinium cannot delete.
5. Expand `EVAL-0068` with permitted supported-API access, an unsupported
   Nexus surface that must not fall back to scraping, policy expiry/reversal,
   permitted excerpt deletion, and a full-body-retention-denied source.
6. Expand `EVAL-0083` to distinguish requested model alias, resolved snapshot
   when exposed, adapter/API/prompt/schema/tool identities, retained response,
   and retired-model clean-rerun failure.
7. Add/extend an export adversarial case in which tool/model output reproduces
   restricted source bytes and must be downgraded to `D/M/O`, not passed through
   as "generated output."
8. Add/extend a provider-retention case proving that `store=false`, ZDR/MAM,
   provider deletion, remote MCP, and local deletion are not conflated.

### 17.4 Follow-up research/implementation-plan inputs

- RQ-005 and any future approved-helper RQ: inspect actual commands, outputs, embedded source
  content, cache/temp writes, and reproducibility.
- RQ-010: approve exact non-Nexus sources across all four permission axes.
- RQ-011/RQ-012: define provider-neutral retained envelopes and verify the
  OpenAI reference adapter's actual model/settings/storage/tool capability.
- RQ-013: compare evidence-store mechanisms against this logical/deletion/replay
  contract.
- RQ-014/RQ-027: measure source, parsed, output, cache, checkpoint, trace, and
  export byte populations before setting numeric defaults.
- RQ-018/RQ-032: select credential, encryption, path, sanitization, navigation,
  export, and deletion authorization controls.
- RQ-025: record exact private and redistribution treatment for every real-mod
  fixture.
- M4: perform source-policy/privacy review of the concrete diagnostic bundle
  schema and support workflow.

## 18. RQ-031 disposition

Accepted owner disposition for `RQ-031`:

> **Answered for M0 with source-specific legal conditions and measured-storage
> follow-up.** Adopt metadata/fingerprint/reference retention by default;
> retain permitted private source material long enough to complete useful
> extraction, analysis, case/finding synthesis, prose generation, provenance,
> and audit; selectively retain exact permitted source and boundary bytes
> longer when necessary; enforce
> separate acquire, private-retain, provider-transmit, and redistribute
> decisions; preserve distinct private/run/export/trace/diagnostic sharing
> classes; and report scoped deterministic recomputation, boundary replay, or
> reproduction-attempt status as complete, partial, or unavailable. Deletion
> must use an inspectable explicit cascade and must discover independent copies
> without rewriting surviving history. Nexus supported-API content follows
> ADR-0005's accepted development-risk decision and independent export
> restrictions unless a negative response or other reversal trigger occurs;
> helper/output rights remain class-specific, and numeric disk policies remain
> for RQ-014/RQ-027.

Reopen RQ-031, or create a dated follow-up, when:

- Nexus supplies written permission/denial;
- a new source/provider/tool/output class enters M1/M4;
- a concrete store cannot implement the required deletion/replay semantics;
- legal review changes a source/private-copy/export conclusion; or
- measured disk behavior makes selective exact retention impractical.

## 19. Requirements-and-evidence traceability

| Requirement/decision | Evidence | Result/downstream use |
|---|---|---|
| `AUTH-002`, ADR-0003 | I1, I4; §11 | All storage/deletion/export writes stay in their authorized product/OS/export locations and cannot mutate protected setup state. |
| `SEC-001`, `AI-003` | I1, I4, O1 | Exact retention/provider use never grants instruction authority; context and exports remain minimized and sanitized. |
| `SEC-002` | I1, I4 | Credentials are excluded from every ordinary artifact/export and remote/local deletion remain distinct. |
| `SEC-004`, `OPS-003` | I1, I2, I4; §§3, 7 | Six sharing contexts replace a generic shareable flag; trace label and M4 review have distinct meanings. |
| `SNAP-001`, `SNAP-005`, `EVID-002` | I1–I3; §§7, 10 | Retain exact identities/fingerprints/versions and resolved input/output boundaries without copying every source byte. |
| `SNAP-006` | I1–I3; §§3.4, 10 | Complete/partial/unavailable are scoped to DR, BR, or RA; audit gaps remain separate. |
| `DOC-008`, `DOC-011` | I1–I3, P1; §§7–8, 15 | Permitted source material remains available through useful dependent analysis; longer exact passages/bodies are conditional, and claims keep source/acquisition/application provenance and deletion gaps. |
| `AI-001`, `AI-006` | I1, O1–O3; §§7, 9–10 | Provider-neutral exact envelopes retain permitted request/response/settings; OpenAI-specific state/model limits are capabilities. |
| `AI-007` | I1, O2 | Provider use stays on the user's selected authorization/account; ownership allocation does not expand source rights. |
| `OPS-001` | I1, O1; §§8–9 | Local/cached/live/provider/tool dependencies and unavailable capabilities are explicit. |
| `OPS-002` | I1–I4; §11 | Deletion previews replay/resume/reuse/citation effects and explicitly selects every independent copy/cascade. |
| P1 / RQ-009 / ADR-0005 | P1 and ADR-0005; §§7–8, 15–16 | Supported-API acquisition, necessary private retention, and minimized optional inference follow the accepted bounded-use interpretation; scraping, unsupported interfaces, training, and raw source redistribution remain excluded. |
| P2 / RQ-026 / ADR-0006 | P2 and ADR-0006; §§7–8, 15–16 | The GPLv3 family and the application/library/data posture are accepted; exact integration mechanisms remain gated, and executable, library, CC0 data, private userlist, and output boundaries stay separate. |
| ADR-0001 | I1, P1, P2, O2 | Retention/output ownership never changes evidence authority or cures third-party rights. |
| ADR-0002 | I2–I3; §§10–11 | Boundary outputs, reuse, immutable history, deletion gaps, and current availability remain explicit rather than rebound. |
| `EVAL-0021`, `EVAL-0025`, `EVAL-0040`, `EVAL-0041`, `EVAL-0068`, `EVAL-0083` | I5; §17.3 | Supplies exact extensions for replay, deletion, source policy, sharing-class, independent-copy, and model-identity tests. |
| M0 Gate A | M0 plan, P1, P2, ADR-0005, ADR-0006; §16 | Retention/export and dependency conditions pass; supported Nexus API work may proceed with documented non-blocking risk and exact ADR bounds. |

## 20. Semantic self-review checklist

- Acquisition, private retention, provider transmission, and external
  redistribution are never collapsed.
- Product-private state, run-owned output, private export, externally shareable
  export, trace export, and M4 diagnostic bundle are distinct.
- No row treats a licence, tool output, model output, API capability, public
  page, private possession, or `store=false` as blanket permission.
- Nexus cache, extraction, and optional inference rules come only from accepted
  ADR-0005 and remain separate from external redistribution.
- Metadata-first minimization never discards permitted private source material
  before useful dependent analysis, prose, provenance, and audit complete.
- LOOT executable/libloot/masterlist/prelude/userlist/output boundaries remain
  separate.
- Exact downstream replay and clean recomputation are separately disclosed.
- A hosted model slug is not represented as a retained executable.
- Deletion updates current replay/audit disclosures without rewriting surviving
  immutable history.
- Independent rendered copies are neither silently deleted nor silently
  ignored.
- Disk recommendations avoid unmeasured numeric budgets.
- The RQ-031 policy semantics and useful-analysis retention clarification are
  accepted through the owner disposition and downstream accepted amendments.
  Storage mechanisms, numeric defaults, and provider-specific implementations
  remain proposed; ADR-0005 separately governs the Nexus operating-risk
  decision.
