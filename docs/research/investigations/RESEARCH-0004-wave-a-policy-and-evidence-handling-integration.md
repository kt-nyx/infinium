# RESEARCH-0004: Wave A policy and evidence-handling integration

Status: Completed — Gate disposition amended by ADR-0005, ADR-0006, ADR-0007, and accepted RQ-031 owner disposition

Date: 2026-07-25

Last reviewed: 2026-07-25

Researcher: Codex review agent

Reviewed wave: A — Policy and evidence-handling guardrails

Primary inputs:

- [RESEARCH-0001: Nexus access and evidence-handling policy](RESEARCH-0001-nexus-access-policy.md)
- [RESEARCH-0002: Helper-tool licensing and distribution posture](RESEARCH-0002-helper-tool-licensing.md)
- [RESEARCH-0003: Retention, replay, and export policy boundary](RESEARCH-0003-retention-replay-export-policy.md)
- [ADR-0005: Proceed with supported Nexus API analysis](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md)
- [ADR-0006: GPL product and tool-dependency boundary](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
- [ADR-0007: Exclude xEdit from Infinium](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)

Decision enabled: Independent review of the three Wave A investigations,
consolidated constraints for later waves, and an evidence-backed Gate A result.
This report integrates Wave A research. ADR-0005 and ADR-0006 were accepted
after the initial independent review and change the operational Nexus,
licensing/tool, and Gate A dispositions recorded below. Neither accepts an
unvalidated concrete source adapter, external-tool operation, library version,
provider, or storage mechanism.

ADR-0007 was accepted after this Wave A integration. It supersedes every
xEdit-specific application, integration, and evaluation posture below. Those
statements remain only as historical evidence of the option that was
researched and later rejected.

## 1. Review scope and authority

This review checks whether the three Wave A reports answer RQ-009, RQ-026, and
RQ-031 against current primary evidence; remain consistent with the accepted
product baseline, ADR-0001 through ADR-0007, and the accepted
[M0 plan](../../plans/milestones/M0-research-foundation.md); and jointly satisfy
the exact three-part Gate A.

It does not:

- itself obtain Nexus permission or make the owner's risk decision later
  recorded in ADR-0005;
- execute a helper or prove that a helper operation is non-mutating;
- choose an Infinium product licence, architecture, provider, evidence store,
  source adapter, parser, helper, shell, export format, or deletion mechanism;
- as an independent review, update the RQ or source registries, evaluation
  catalog, ADRs, product documents, or milestone plan; the subsequent accepted
  owner disposition applies the amendments explicitly identified below;
- perform Wave B, D, or E primary research; or
- provide legal advice.

RQ-031 is answered for M0 through the accepted owner disposition in
RESEARCH-0003. RQ-009's operational disposition is accepted through ADR-0005,
and RQ-026 is resolved through ADR-0006. Other recommendations become
constraints only through the accepted downstream artifact appropriate to their
authority.

### Accepted RQ-026 amendment

The original independent review correctly classified the tool licences and
separated applications, libraries, data, user input, and output, but its
conservative exclusions preceded the owner's GPLv3-family decision. ADR-0006
supersedes those proposed exclusions with the accepted posture consolidated in
§3.2.

This amendment concludes licensing and high-level dependency-posture research.
It does not answer RQ-001/RQ-002/RQ-004/RQ-005/RQ-006 or authorize any
external-tool or library operation.

### Accepted RQ-031 amendment

The owner accepts the reviewed RQ-031 policy with an explicit operational
clarification: permitted private source material remains available long enough
to produce useful extracted evidence, LLM analysis, claims, cases, findings,
prose, provenance, and audit. Metadata-first retention governs unnecessary
durable duplication and export, not premature deletion. Supported-API Nexus
material follows ADR-0005's accepted development-risk posture unless a
negative response or other reversal trigger requires review of the affected
path.

## 2. Review method

The review:

1. read the repository guidance, complete required product/architecture
   baseline, accepted M0 plan, research procedure, Wave review prompt, and all
   three investigations in full;
2. compared every report's question, scope, sources, checks, uncertainty,
   recommendation, RQ disposition, and traceability against its owning RQ and
   the M0 deliverable contract;
3. rechecked the material Nexus Terms/AUP, OpenAI data-control/agreement/model
   availability claims against current official sources;
4. reran the documented immutable-revision checks for every helper, data
   repository, and desktop candidate pinned in RESEARCH-0002;
5. cross-checked the four permission axes, helper/software/data/output
   distinctions, sharing classes, replay vocabulary, and deletion semantics;
6. checked named M1 helper candidates for an explicit experiment and
   distribution posture or exclusion;
7. checked local links and referenced requirement, RQ, ADR, and EVAL
   identifiers; and
8. inspected repository status and the final diff and ran `git diff --check`.

No Nexus mod-content endpoint, authenticated provider, paid service, local
private profile, game/mod-manager installation, or external helper executable
was accessed.

## 3. Report-by-report assessment

### 3.1 RESEARCH-0001 — RQ-009

**Assessment:** Reviewable and materially answers the policy question. Its
conservative research recommendation was a documented blocker; ADR-0005 later
accepted a bounded-use project interpretation for supported API analysis while
preserving the ambiguity and requesting Nexus confirmation.

Strengths:

- Uses the current official Nexus
  [Terms of Service](https://help.nexusmods.com/article/18-terms-of-service),
  the official
  [API AUP](https://help.nexusmods.com/article/114-api-acceptable-use-policy),
  official API documentation/specifications, and a pinned maintained
  first-party client.
- Separates API capability from permission and gives the newer Terms their
  proper applicability to Nexus APIs and automated analysis.
- Documents reproducible public-document checks while correctly stopping
  before authenticated or mod-content access.
- Covers acquisition, automated extraction, model use, caching, quotation,
  redistribution, registration, rate-limit, age-filtering, and deep-link
  uncertainty without inventing a safe fallback.
- Preserves Nexus absence as a coverage gap rather than weakening DOC-001,
  DOC-006, or DOC-008.

Current-source review confirmed that the Terms remain dated 2026-05-12, apply
to APIs, prohibit automated access and analytical techniques over service data,
and provide an exemption contact. The AUP remains dated 2020-12-01 and still
describes personal testing keys, public-application registration, user
initiation, request identity, and unacceptable use. Neither inspected source
states that AUP-compliant API use exempts Infinium's automated claim-extraction
workflow from the newer Terms.

Limitations that remain visible under ADR-0005:

- No written Nexus interpretation or exemption exists.
- RQ-008 has not yet established which current documented API operations expose
  descriptions, requirements, articles, changelogs, posts, comments, or bug
  reports.
- RQ-008 may perform bounded live experiments only against documented,
  supported operations. Every unsupported surface remains a coverage gap and
  may not fall back to scraping or browser automation.

RESEARCH-0001 was amended to incorporate ADR-0005 and the accepted RQ-031
operational dispositions while preserving its original evidence, contrary
interpretation, uncertainty, and reversal triggers.

### 3.2 RESEARCH-0002 — RQ-026

**Assessment:** Reviewable and answers the Wave A licensing/distribution
posture for every helper or architectural helper candidate currently named for
M1. It deliberately does not prove technical fitness or read-only operation.

The report distinguishes:

- user-installed separate invocation;
- executable/runtime redistribution;
- library linking or embedding;
- separately licensed project data;
- private user input;
- tool output;
- modification/forking; and
- factual interoperability naming versus branding.

The following posture is accepted through ADR-0006. It settles licensing and
high-level dependency treatment while leaving the cited technical research
questions open:

| Candidate | Accepted posture |
|---|---|
| MO2 | Required user-installed application; never bundled, downloaded, installed, replaced, or updated by Infinium. |
| USVFS | Direct bundling or operation is disfavored; RQ-001 must first test deterministic reconstruction and bounded execution through the user's MO2 installation. |
| LOOT executable | User-installed application and preferred first RQ-005 boundary; never bundled or managed by Infinium. |
| libloot | Conditional bundled-library candidate only if RQ-005 proves supported LOOT executable invocation insufficient. |
| LOOT Skyrim SE masterlist and prelude | Managed, versioned CC0 data with exact provenance and integrity controls; not assumed to be a fixed bundled payload. |
| LOOT userlist/local state | Private user input; never inherit curated-data authority or CC0 status. |
| xEdit | **Historical proposal rejected by ADR-0007.** No Infinium product, development, dependency, integration, or evaluation role remains. |
| Mutagen.Bethesda | Leading bundled-library candidate, subject to RQ-004 technical and exact-package validation. |
| Electron | Permissive core candidate with exact-lock dependency/SBOM/notices obligations; not selected. |
| Avalonia | Permissive framework candidate with exact NuGet/notices and commercial-tool separation; not selected. |
| Tauri/WebView2 | Permissive candidate with Cargo/npm/native inventory and explicit runtime distribution/update posture; not selected. |
| WebView2 Fixed Version | Excluded from M1 absent an accepted need and servicing/package review. |

The immutable upstream references for all eleven checked software/data
revisions still resolve to the exact commits recorded in the report. The
licence classifications and software/data separation remain applicable.

This satisfies the Wave A distribution-posture question for the named
candidates. It does **not** authorize an experiment: before any helper
execution, the owning integration RQ must identify the exact operation and
prove command, inputs, outputs, update/network behavior, cache/temp/log writes,
failure behavior, and preservation of AUTH-001 through AUTH-003. A future
helper or dependency that enters M1 scope must receive the same posture or be
explicitly excluded.

RESEARCH-0002 was amended to incorporate ADR-0006's accepted disposition while
preserving its original evidence, alternatives, uncertainty, and technical
gates.

### 3.3 RESEARCH-0003 — RQ-031

**Assessment:** Reviewable and answers the logical retention, replay, export,
and deletion policy boundary needed before RQ-013. Exact source permissions
and measured storage defaults correctly remain source- and follow-up-specific.

The report correctly separates:

- acquire/inspect;
- private exact-byte or excerpt retention;
- model/provider transmission; and
- external redistribution.

It also preserves the accepted artifact distinctions:

- `P` — product-private retained state;
- `R` — run-owned local output;
- `L` — user-created private/local export;
- `S` — externally shareable export;
- `T` — sensitivity-labeled developer-trace export; and
- `BND` — M4 reviewed diagnostic bundle.

The coordinator's sharing-code correction is complete. `BND` is used for the
diagnostic-bundle class in its definition, matrices, cross-references, and
recommendations. `D` is used only as the retained-content form for a derived
observation, claim, finding, or statistic; no diagnostic-bundle use of `D`
remains.

Replay terminology is internally coherent:

- `DR` — deterministic recomputation from exact inputs and executable logic;
- `BR` — downstream replay from a retained boundary output; and
- `RA` — a new reproduction attempt that reacquires or reinvokes a boundary.

`Complete`, `partial`, and `unavailable` are scoped to the declared replay
mechanism and subgraph rather than promising model byte identity. Auditability
remains separate.

Deletion semantics agree with OPS-002 and ADR-0002: preview the selected
logical objects and effects; never silently cascade from a source to an
independent run output/export/trace or back; permit only an inspectable
confirmed cascade; preserve non-payload deletion/audit facts; and update
current replay/audit disclosure without rewriting surviving immutable history.

Current official OpenAI documentation still supports the report's material
reference-provider facts: API content is not used for training by default;
default abuse-monitoring logs may retain customer content up to 30 days;
ZDR/MAM require approval; `/v1/responses` and provider tools have
capability-specific application-state behavior; `store=false` is not ZDR; and
remote MCP/network services add third-party retention boundaries. The current
Services Agreement still assigns Input/Output rights only between OpenAI and
the customer while leaving Input rights and Output use with the customer.
These are provider-specific evidence inputs, not an accepted M1 adapter.

RESEARCH-0003 was amended to record the accepted disposition and clarify the
minimum useful private-retention window. Its independent permission axes,
sharing classes, replay model, deletion semantics, and source-specific
conditions remain unchanged.

## 4. Cross-report consistency and dependency findings

### 4.1 No material contradiction found

The reports agree on these boundaries:

- A technically available API is not policy permission.
- A software licence is not proof that an operation is safe, deterministic, or
  non-mutating.
- A tool or model output is not automatically free of embedded source, game,
  mod, user, privacy, or redistribution restrictions.
- Private possession or retention does not imply provider transmission or
  external redistribution.
- Run-owned output, private exports, shareable exports, trace exports, and the
  M4 bundle are separate retained objects and authority classes.
- Exact boundary-output retention can support BR while clean DR or provider
  reproduction remains partial or unavailable.
- Deletion changes current audit/replay availability without silently
  rewriting surviving history.
- Unknown permission is an operational stop on that axis.

### 4.2 Dependencies and residual gaps

1. **Nexus's written interpretation remains unresolved.** ADR-0005 accepts the
   bounded supported-API interpretation as a documented project risk, so it is
   no longer an operational Gate A blocker. A contrary Nexus response or policy
   change triggers review and may stop the affected path.
2. **The source registry now carries the decision, but exact interfaces remain
   unverified.** The Nexus row records all four permission axes and ADR-0005;
   RQ-008 must add exact supported operations, revisions, and capability gaps
   before a production adapter is selected.
3. **RQ-010 remains necessary.** No generic "public author source" permission
   follows from Nexus failure or from a page being publicly readable.
4. **Helper experiment safety remains unproven.** RQ-001/RQ-005/RQ-006 and
   other owning integration work must prove actual write/cache/temp/network
   behavior before invocation.
5. **Infinium's product licence/distribution model remains undecided.** This
   blocks linked GPL-family helper acceptance, not all research or all
   subprocess candidates.
6. **Exact binary/transitive package audits remain downstream.** Repository
   licence texts and tags do not prove a future installer's complete notice,
   source, asset, runtime, and dependency compliance.
7. **Concrete storage mechanisms and measured policies remain open.** RQ-013
   must implement the logical separation; RQ-014/RQ-027 must measure disk/IO
   before numeric defaults.
8. **Provider-specific behavior remains unaccepted.** RQ-011/RQ-012 must define
   and test the provider-neutral contract and selected adapter capabilities.
9. **Source/content rights remain per class.** Local bundled documentation,
   real-mod fixtures, game bytes, provider tools, and non-Nexus sources require
   their own four-axis decisions.

## 5. Consolidated Wave A constraints

Later research and proposed architecture must consume all of the following:

1. No Nexus HTML scraping, browser-session automation, search-cache
   reconstruction, copy/paste workaround, or undocumented endpoint.
2. Nexus acquisition uses only documented supported APIs, remains user
   initiated and bounded to relevant mods, and follows ADR-0005. Necessary
   private retention and minimized optional inference are allowed; raw external
   redistribution remains independently restricted.
3. Every source/input class has independent acquire, private-retain,
   provider-transmit, and redistribute decisions with evidence date and
   reverification/stop behavior.
4. Metadata, fingerprints, references, dependency edges, and omission markers
   are the general retention default. Exact bytes require affirmative
   source-specific permission, necessity, protection, deletion accounting, and
   visible storage cost.
5. Software executable/library, separately licensed data, private user input,
   and tool output remain distinct objects and rights classes.
6. A helper's M1 distribution posture does not authorize execution. Every
   operation still needs an owning safety/integration experiment.
7. ADR-0006 now permits GPL-compatible bundled-library candidates. Mutagen is
   leading, libloot is conditional, and USVFS remains disfavored; exact
   versions and operations remain stopped until their owning technical gates.
8. Run output cannot be relabeled as a user-created shareable export. Every
   export is a new independently retained object with an exact selection,
   source-policy, omission, redaction, and intended-sharing manifest.
9. `P/R/L/S/T/BND`, `B/X/D/M/O`, and `DR/BR/RA` are separate proposed
   vocabularies for sharing authority, retained content form, and replay
   mechanism. A later accepted specification may rename them but must preserve
   their distinctions.
10. Deletion uses explicit object selection and an inspectable confirmed
    cascade, discovers independently retained copies, protects active/paused
    work, and never preserves deleted payload through diagnostics.

## 6. Exact Gate A assessment

The accepted Gate A is conjunctive:

| Gate clause | Evidence-backed result |
|---|---|
| No planned Wave D source access relies on a prohibited or unknown method. | Passes under ADR-0005. Wave D is constrained to documented supported APIs, and the owner has accepted bounded diagnostic analysis through those interfaces as the project's working interpretation. Unsupported content surfaces are gaps, not planned access methods. |
| No planned tracked artifact assumes private retention permits redistribution. | Passes under the accepted RQ-031 owner disposition. RESEARCH-0003 explicitly separates the four axes and all tracked/export artifact classes; RESEARCH-0001 and RESEARCH-0002 preserve source/output-specific rights. |
| Every external helper considered for M1 has a known experiment and distribution posture, or is excluded. | Passes under ADR-0006 for every helper currently named. Each candidate is excluded, user-installed, or has a conditional owning RQ for the exact operation plus a distinct distribution posture. No operation is thereby approved. |

All three clauses now pass at the accepted decision/research-disposition level.
The residual Nexus policy ambiguity is material and must remain visible, but
ADR-0005 makes it non-blocking for development by choosing the supported-API,
personal/mod-enhancement interpretation and exact limits. Written Nexus
confirmation can narrow, affirm, or reverse that decision; it is no longer a
prerequisite to RQ-008 or Wave B.

## 7. Next-wave consequence and stopped operations

Gate A is met with the documented non-blocking Nexus policy risk, so Wave B may
formally begin. Wave B must consume ADR-0006's accepted dependency posture,
RQ-031 artifact rules, and all per-tool/library operation preflights.

The following Nexus operations remain stopped:

- HTML/page scraping, crawling, browser-session automation, or search-cache
  reconstruction;
- undocumented/private endpoint use or access-control bypass;
- bulk acquisition unrelated to the user's relevant mod set;
- background acquisition not initiated by the user;
- public corpus creation, rehosting, or a substitute Nexus catalog/service;
- model training, fine-tuning, or validation on Nexus content;
- raw source-body inclusion in externally shareable artifacts by default;
- treating an unsupported content surface as permission to use a page
  fallback; and
- public release without the then-current registration/approval process.

Bounded authenticated supported-API experiments, deterministic extraction,
necessary private retention, and minimized optional inference may proceed
within ADR-0005.

Independently of Nexus, external-tool and library operations remain stopped
until each exact boundary passes its owning correctness,
read-only/cache/temp/network/failure preflight. ADR-0006 permits Mutagen and
conditional libloot candidates but does not accept a version or implementation;
USVFS remains disfavored pending RQ-001 necessity evidence.

## 8. Exact downstream proposals

The initial integration proposals are retained below. ADR-0005, ADR-0006, the
Nexus and dependency-authority source-registry entries, the RQ-009/RQ-026
dispositions, and the M0 Gate A amendments have now been applied by the project
owner. Remaining mechanism and evaluation items are still proposals.

### 8.1 RQ dispositions

- **RQ-009:** Mark Answered for M0 by ADR-0005, with external clarification
  pending. Preserve the ambiguity and reopen/review on Nexus response, policy
  change, registration decision, or material expansion of the data flow.
- **RQ-026:** Resolved for M0 by ADR-0006. Reopen if the GPLv3-family posture
  changes, a GPL-incompatible dependency becomes necessary, an external
  application becomes a proposed bundled payload, a new M1 helper class enters
  scope, or the distribution model materially changes.
- **RQ-031:** Answered for M0 with source-specific legal conditions, the
  accepted useful-analysis retention clarification, and measured-storage
  follow-up. Reopen for a new
  source/provider/output class, changed Nexus result, contrary legal review, or
  a storage design unable to preserve the required semantics.

### 8.2 Source-registry changes

Items 1 and 2 have been applied at the policy-model level. Stable dependency
and licensing authorities from RESEARCH-0002 are also registered without
substituting for the future package manifest required by item 5. RQ-008 must
refine the Nexus record with exact supported interfaces and observed behavior.

1. Add a Nexus source record containing the exact current Terms/AUP/API
   evidence, HTML prohibition, ADR-0005 supported-API analysis decision,
   private-retention/provider/export boundaries, registration, identity-header,
   rate-limit, age-filter, and policy-expiry behavior.
2. Extend the registry schema with separate acquisition, private exact
   retention/excerpt, provider transmission, and external redistribution
   decisions; allowed `B/X/D/M/O` forms; evidence/version/retrieval date;
   reverification trigger; and replay/deletion consequences.
3. Add LOOT masterlist/prelude as separately versioned official curated data
   sources only when an accepted integration uses them; do not inherit LOOT
   executable licensing or grant a userlist curated authority.
4. Register only exact non-Nexus sources approved by RQ-010. Do not create a
   blanket public-web permission.
5. Keep software dependency licence/SBOM compliance in a dedicated package
   manifest or ADR-backed release process rather than conflating it with
   documentation evidence authority in the source registry.

### 8.3 Proposed ADRs and accepted-specification inputs

- ADR-0006 now governs licensing and the high-level tool/dependency posture.
  Exact library adoption, external-tool operations, shell/runtime selection,
  and final packaging still require their owning ADRs or accepted plans.
- Evidence persistence/retention/replay/deletion ADR after RQ-013.
- Documentation-source/provider-transmission/export mechanism ADR after RQ-010
  through RQ-013, constrained by ADR-0005 and any Nexus response.
- RQ-032 security/export-control ADR input for credentials, content
  sanitization, path/navigation, subprocess, redaction, and deletion
  authorization.
- A reviewed normative vocabulary for the four permission axes, artifact
  classes, retained forms, and replay mechanisms in the appropriate product
  specification or ADR-backed contract.

### 8.4 Plan amendments

The ADR-0005 amendment has been applied. The response-driven amendment remains
conditional.

- Record ADR-0005 as an accepted M0 input and permit RQ-008 supported-API
  experiments while preserving all prohibited operations and reversal
  triggers.
- Preserve the Nexus support response as dated evidence and amend the plan only
  if it narrows or reverses ADR-0005.
- Carry ADR-0006's application/library/data posture into the M1 dependency
  allowlist and reopen RQ-026 on its stated triggers.

### 8.5 Evaluation and next-wave prerequisites

- Apply RESEARCH-0001/ADR-0005's supported, unsupported, expired, and
  policy-reversed source cases to EVAL-0068 and export-omission behavior to
  EVAL-0040.
- Apply RESEARCH-0003's scoped BR/DR distinction to EVAL-0021, deletion status
  transitions to EVAL-0025, sharing contexts to EVAL-0040, and independent-copy
  cascades to EVAL-0041.
- Extend EVAL-0083 for requested/resolved model identity, retained boundary
  output, and retired-model clean-rerun failure.
- Before Wave B helper experiments, carry forward the RQ-026 exact candidate
  posture and require an operation-specific AUTH-001 through AUTH-003
  side-effect manifest.
- Before Wave D source experiments, require a reviewed four-axis source row,
  RQ-009 permission/exclusion disposition, Wave B identity inputs, Wave C
  taxonomy inputs, and the accepted ADR-0001 boundary.
- Before Wave E storage selection, carry forward the distinct source,
  boundary-output, run-output, trace, export, deletion, and replay objects
  without treating the proposed shorthand codes as already accepted schema.

## 9. Validation

- All local Markdown links in RESEARCH-0001 through RESEARCH-0004 resolve.
- Referenced RQ, requirement, ADR, and EVAL identifiers checked in the reports
  exist in the current registries/catalogs.
- Current material Nexus and OpenAI policy/provider claims were rechecked
  against official primary sources on 2026-07-25.
- Every immutable helper/data/framework revision listed in RESEARCH-0002 was
  re-resolved with `git ls-remote --refs` and matched the recorded commit.
- The `BND`/`D` correction was checked across all of RESEARCH-0003.
- `git diff --check` completed without whitespace errors.
- The initial independent review changed only this integration report. The
  later owner disposition was applied through ADR-0005 and coordinated updates
  to the research, registry, plan, and gate result; those amendments receive
  their own repository-wide link, identifier, whitespace, and semantic checks.

Current Gate A classification: **Met with documented non-blocking gaps**
