# RESEARCH-0033: Wave D revision integration

Status: Accepted

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Accepted: 2026-07-28

Accepted by: Project owner

Primary scope: Independent integration of
[RESEARCH-0030](RESEARCH-0030-nexus-latest-interface-qualification.md),
[RESEARCH-0031](RESEARCH-0031-loot-freshness-and-source-discovery.md), and
[RESEARCH-0032](RESEARCH-0032-openai-first-llm-and-web-search.md) against
RESEARCH-0025 through RESEARCH-0029 and the accepted product/ADR baseline

Decision enabled: Revised Wave D owner disposition, product amendments, and
bounded ADR work without implying implementation or conformance

Subsequent disposition: The repository integration described here is complete.
RESEARCH-0034/0035 later completed Gate C; references below to Gate C as an
independent outstanding prerequisite record this report's earlier Wave D
integration boundary rather than current milestone status.
The owner accepted ADR-0012 through ADR-0014 and the integrated Gate D
disposition on 2026-07-28. Later production conformance gates remain separate.

## Executive result

The revised Wave D reports are mutually coherent and materially improve the
original Wave D result:

- the authenticated Nexus capability experiment is complete;
- the owner has explicitly removed the former separate GraphQL-policy blocker
  and made unresolved Nexus API-policy interpretation non-blocking during
  development;
- latest-capable Nexus routing can cover descriptions, requirements, files,
  and changelogs more usefully than any one API generation;
- current-compatible LOOT data can be refreshed automatically without changing
  the immutable data pair bound to a run;
- mapped GitHub mod documentation can remain optional/later rather than an M1
  core lane; and
- OpenAI-specific capabilities can be used without putting provider objects or
  lowest-common-denominator constraints into Infinium's authoritative domain.

RESEARCH-0030 supersedes the operative interface recommendation and blockers
in RESEARCH-0025. RESEARCH-0031 revises the priority and refresh
recommendations in RESEARCH-0026. RESEARCH-0032 preserves the two safe semantic
operations and admission invariants from RESEARCH-0027, preserves the useful
authentication/capability findings from RESEARCH-0028, and supersedes their
portability-as-a-ceiling recommendation. RESEARCH-0029 remains a valid record
of the earlier review, but its authenticated-Nexus, separate-GraphQL-approval,
and provider-parity blockers are superseded.

The four Gate D clauses are supported and accepted at the M0 research/design
layer. Production adapters, secure credentials, budget enforcement, and
evaluation conformance remain later gates.

## Decision-state matrix

| Subject | State after the owner's 2026-07-28 direction | Exact boundary |
|---|---|---|
| Nexus policy posture | **Accepted owner direction; must be recorded as an executive decision** | During development, unresolved Nexus policy interpretation does not block bounded diagnostic use, introspection, or testing of Nexus-provided APIs. This does not authorize pages, browser automation, access-control bypass, unrelated bulk collection, mutation, downloads, secret disclosure, training, or public rehosting. |
| Nexus interface selection | **Accepted owner direction, technically qualified by RESEARCH-0030** | Prefer v3 for each content group it supplies, then v2 GraphQL where v3 lacks required content, then v1 only where neither newer route supplies the need or as an explicit degraded fallback. Bind the resolved route/schema/query to the acquisition run. |
| Nexus unsupported content | **Research finding, not discretionary policy** | Articles, mod-linked posts/comments, author/sticky posts, and mod bug reports remain unsupported API gaps. Generic GraphQL comments, collection bug reports, web-search snippets, or page access are not substitutes. |
| Automatic LOOT freshness | **Accepted owner direction at product-intent level** | Infinium should perform configurable, nonblocking maintenance checks on startup and at a reasonable interval while retaining manual refresh and offline use. It must never change the pair bound to an active or historical run. |
| LOOT 24-hour interval | **Accepted by ADR-0014** | Check after startup when the prior completed check is at least 24 hours old and no more than once per 24 hours while open. This is a configurable/versioned default, not a semantic property of historical runs. |
| LOOT pair transaction and GitHub transport | **Accepted design under ADR-0014; implementation pending** | Resolve compatible moving heads, fetch immutable commits, validate masterlist/prelude together, atomically activate one pair manifest, retain the prior known-good pair, and reject unsupported compatibility branches. |
| GitHub-hosted mod documentation | **Accepted prioritization direction** | Keep positively mapped official repositories as an optional/later bounded adapter. Do not make broad GitHub repository search or mod-documentation support an M1 core requirement. GitHub transport for LOOT managed data remains independently necessary. |
| Web search product role | **Accepted design under ADR-0013; implementation pending** | Governed search is useful for discovery. Results and model summaries remain investigative leads until an approved landing-source adapter acquires and fingerprints the source and exact passages. |
| Provider strategy | **Accepted owner direction at product-intent level** | OpenAI may be the only initially implemented provider. Provider portability must not remove useful OpenAI capabilities. Provider independence remains mandatory for domain truth, evidence authority, findings, cases, coverage, and readiness. |
| OpenAI Responses and hosted web search | **Accepted design under ADR-0013; live conformance pending** | Use Responses as the initial generation API, Structured Outputs for typed semantic operations, and hosted `web_search` only in governed discovery/research operations. No other model-selected tools are accepted for M1. |
| Background, Batch, and prompt caching | **Accepted qualification boundary; not enabled by acceptance alone** | Qualify and disclose each separately because they change cancellation, retention, cost, latency, or replay behavior. Synchronous stateless `store=false` requests are the initial default. |
| Exact model and production credential mechanism | **Undecided** | No model, SDK, secret store, desktop stack, or account-administration mechanism is selected by Wave D. |

## Gate D assessment

| Gate D clause | Revised evidence | Result |
|---|---|---|
| Every extracted claim resolves to permitted source evidence and applicable versions/conditions or abstains. | RESEARCH-0027's host-span/admission contract, RESEARCH-0029's real CC0 exercise, RESEARCH-0030's interface/revision routing, and RESEARCH-0032's explicit separation of search discovery from acquired source passages. | **Met at the research/design layer.** Selected provider/source adapters must still pass the cited extraction, applicability, and provenance evaluations. |
| Model output cannot become local-state authority or grant operation authority. | ADR-0001, the preserved two semantic operations, host validation/admission, provider-independent domain truth, and web-search-only model tooling with no local privileged tools. | **Met at the research/design layer.** Adversarial provider conformance remains required. |
| The contract works without provider-specific concepts in the core domain. | Canonical evidence/domain objects remain provider-independent; OpenAI Response items, web-search calls, citations, background/Batch state, cache usage, and rate/cost telemetry remain invocation records outside domain truth. | **Met.** This clause does not require feature parity across providers. |
| Authenticated or billable experimentation has explicit user authorization, credential handling, context, cost, and retention boundaries. | The owner explicitly authorized the bounded Nexus experiment and supplied the credential through a non-printing local file; RESEARCH-0030 retained no secret or raw source body. No authenticated or paid OpenAI call occurred. | **Met for the work performed.** Production credential storage and billable-provider conformance remain RQ-018/RQ-032/RQ-034 work. |

**Gate D disposition:** accepted and met at the M0 research/design layer.
ADR-0012 through ADR-0014 and the completed cross-document integration resolve
the prior acceptance blockers. No production support or passed evaluation is
implied.

Remaining conformance gates are:

- Nexus adapter contract/schema drift, failure normalization, secure
  credentials, untrusted markup, and EVAL-0068/EVAL-0083;
- LOOT EVAL-0053/EVAL-0046 plus pair-cache/activation/failure conformance;
- exact OpenAI model/Structured Output/search/cancellation/retention/cost and
  adversarial conformance;
- RQ-018 secure credential storage, RQ-032 privileged/content boundaries, and
  RQ-034 hard-limit reservation/reconciliation; and
- the independent Gate C and later Wave E/F prerequisites, which this Wave D
  revision does not resolve.

## Exact cross-document update map

| Target | Required update | Decision state |
|---|---|---|
| `docs/product/requirements.md` | Add an accepted amendment recording the owner's new directions. Narrowly amend `SCOPE-004`/`DOC-009` to allow configurable no-LLM LOOT managed-data maintenance without allowing unsolicited scans, general documentation acquisition, Nexus refresh, web search, or model work. Replace `AI-001`'s lowest-common-denominator implication with provider-independent domain/evidence truth plus provider-specific capability profiles. Clarify `AI-002` so OpenAI may be the sole initial supported provider and later providers may expose different declared capabilities. | Applied and accepted. |
| `docs/product/product-definition.md` | Update the LLM/provider summary only enough to say OpenAI-first capability does not weaken the deterministic/evidence boundary. Do not enumerate provider transport features as product truth. | Applied and accepted. |
| `docs/product/workflows.md` | Add nonblocking LOOT managed-data maintenance and its visible freshness/failure state; keep scans, Nexus acquisition, broader search, and LLM work manually initiated. Change provider-selection language so one supported OpenAI profile is valid initially. | Applied and accepted. |
| `docs/product/scope-and-milestones.md` | Replace “GPT reference provider” portability language with OpenAI as the initial supported LLM path and no second-provider M1 gate. Record the narrow LOOT-maintenance exception. | Applied and accepted. |
| `docs/architecture/overview.md` | Replace “provider-neutral LLM integration” with provider-independent domain truth plus capability-profiled provider adapters. Keep the application stack unaccepted. | Applied; stack remains undecided. |
| `docs/architecture/integrations.md` | Replace the old Nexus supported-interface/GraphQL blocker with latest-capable v3/v2/v1 routing and unsupported-content gaps; add the immutable LOOT pair-refresh lifecycle; replace the universal provider adapter wording with OpenAI-first Responses/search capability boundaries. | Applied under ADR-0012 through ADR-0014. |
| `docs/architecture/security-and-privacy.md` | Update only the Nexus API eligibility language; retain no-page/no-bypass rules. Add hosted-search query minimization and the rule that model-selected search cannot authorize landing acquisition or privileged tools. | Applied; later mechanism conformance remains. |
| `docs/architecture/jobs-caching-and-snapshots.md` | Distinguish scheduled LOOT managed-data maintenance from user-initiated documentation refresh; bind an immutable validated pair at run start; keep OpenAI web-search reruns as new live acquisition rather than replay. | Applied under ADR-0013/ADR-0014. |
| `docs/architecture/decisions/` | Add the ADR records described below and update the ADR index/supersession links. Do not silently rewrite ADR-0005 or ADR-0011 history. | Applied; ADR-0012 through ADR-0014 accepted. |
| `docs/research/open-questions.md` | Reconcile RQ-008 with the completed authenticated/latest-capable result; revise RQ-010 around local/LOOT core, low-priority GitHub docs, and governed search; revise RQ-011/RQ-012 around OpenAI-first capability with provider-independent domain truth. Preserve later conformance gaps. | Applied; RQ-008 and RQ-010 through RQ-012 resolved for M0. |
| `docs/research/source-registry.md` | Replace inactive Nexus tier blockers with the exact v3/v2/v1 content-routing matrix and API-only executive risk posture; revise LOOT freshness/pair provenance; demote mapped GitHub mod docs to optional/later; add OpenAI web search as a discovery provider, not a source-authority grant. | Applied under ADR-0012 through ADR-0014. |
| `docs/research/investigations/README.md` | Add RESEARCH-0030 through RESEARCH-0033. Mark RESEARCH-0025 through RESEARCH-0029 as retained prior evidence whose operative blockers/recommendations were revised or superseded, rather than deleting them. | Applied. |
| `docs/plans/milestones/M0-research-foundation.md` | Amend Wave D's title/status, RQ table, internal order, required outputs, and Gate D assessment. Preserve the original accepted plan history and state that adapter/evaluation conformance is not an M0 implementation claim. | Applied and accepted. |
| `docs/README.md` | Update current project state only after the authoritative amendments/ADRs are accepted. | Applied. |
| `docs/evaluation/case-catalog.md` and applicable specifications | Incorporate RESEARCH-0030's v3/v2/v1 fallback/drift cases, RESEARCH-0031's LOOT pair-refresh/rollback/offline cases, and RESEARCH-0032's web-discovery/acquisition separation, OpenAI item/provenance, cancellation, retention, and no-authority cases. Do not mark them passed. | Catalog inputs applied; specifications and execution remain pending. |

## ADR boundaries and final disposition

1. **Nexus API acquisition and executive development-risk ADR**

   - Supersede ADR-0005 rather than erasing it.
   - Restate its still-valid bounded-purpose, local-retention,
     no-page/no-bypass/no-rehost/no-training constraints.
   - Replace “documented supported interface only” with the owner's explicit
     API-wide development posture and the latest-capable v3 → v2 GraphQL → v1
     per-content route.
   - Final disposition: **Accepted in ADR-0012.** The policy-risk and GraphQL
     eligibility questions should not be reopened as blockers absent a
     reversal trigger.

2. **LOOT managed-data freshness ADR**

   - Supplement or partially supersede ADR-0011's managed-data refresh clause;
     retain ADR-0011's libloot version, no-apply, authority, worker, and
     conformance boundaries.
   - Own compatibility-line discovery, startup/interval checks, immutable
     pair manifests, staged validation, atomic activation, rollback, offline
     use, and run isolation.
   - Final disposition: **Accepted in ADR-0014.** Implementation and
     conformance remain pending.

3. **OpenAI-first LLM capability ADR**

   - Own Responses, Structured Outputs, the preserved extraction/investigation
     semantic operations, OpenAI-specific capability profiles/receipts, hosted
     web-search-only model tooling, stateless default, and separate
     background/Batch/cache qualification.
   - Preserve ADR-0001 authority and ADR-0002 provenance; do not select a model,
     credential store, hard-budget mechanism, desktop stack, database, or
     arbitrary model tools.
   - Final disposition: **Accepted in ADR-0013.** Exact implementation/model
     conformance remains pending.

4. **Later documentation persistence/source-enforcement ADR**

   - Keep this separate until RQ-013 and RQ-032 select storage, deletion,
     navigation, sanitization, and source-adapter enforcement mechanisms.
   - Status recommendation: **Not yet ready**. Wave D supplies its logical
     inputs but not the mechanism.

## Validation

- Read the authoritative product order, relevant accepted ADRs, source/open-RQ
  registries, M0 Wave D plan, RESEARCH-0025 through RESEARCH-0029, and
  RESEARCH-0030 through RESEARCH-0032.
- Reconciled direct owner decisions separately from research-derived defaults
  and optional mechanisms.
- Checked that latest-interface routing does not change evidence authority,
  that automatic LOOT maintenance does not mutate a run, and that OpenAI
  capability does not enter domain truth.
- Checked every original Gate D clause and retained later conformance gaps.
- Authored only this integration report; no adapter, credential, plan, ADR,
  requirement, registry, or evaluation status was changed.
