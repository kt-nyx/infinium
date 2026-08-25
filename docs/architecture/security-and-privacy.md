# Security and privacy

Status: Accepted
Disposition: synthesis; actively maintained
Last reviewed: 2026-08-08

## Security posture

Infinium reads large amounts of local state, invokes tools, stores API
credentials, retrieves untrusted content, and uses a native desktop shell plus
authority-bearing coordinator, worker, and helper operations. Security is a
product requirement even for a personal-first tool.
Normative product constraints are SEC-001 through SEC-004, AUTH-001 through
AUTH-003, AI-003, AI-007, and the applicable export/history requirements.

## Authority

- The UI receives a narrow allowlisted API.
- Authority-bearing filesystem, process, and network actions are
  coordinator-authorized and occur only in the exact coordinator, bounded
  worker, one-shot helper, or minimal native-host role accepted for that
  operation.
- ADR-0018 accepts the standalone coordinator, bounded-worker, and one-shot
  helper process roles. ADR-0019 accepts schema-validated, role-separated
  local IPC over current-user-restricted Windows named pipes.
- Callers/senders are authenticated to the extent supported by the selected
  architecture.
- Arbitrary paths, commands, URLs, and tool arguments are not accepted without
  validation and scope checks.
- Product write surfaces use authority appropriate to their class:
  product-controlled locations for data/cache/diagnostics/update staging,
  approved OS-backed storage for credentials where required, and explicitly
  selected non-protected destinations for exports. User-facing
  retention/deletion is limited to explicitly selected retained objects within
  those authorized locations. None may target protected setup roots.
- The product through M4 does not edit the modding setup.
- Evaluator-private fixture inputs and answers use ADR-0026's separate Git
  history and purpose-bound delegated access. This is a context and process
  boundary, not an OS sandbox; final held-out acceptance may require a separate
  worker, identity, VM, or private CI broker.

## Untrusted content

Mod descriptions, comments, README files, logs, reports, HTML, and LLM output
are untrusted data.

- Never execute retrieved scripts or remote code.
- Do not render remote HTML with privileged integration.
- Sanitize display content.
- Restrict navigation and external links.
- Treat prompt injection as data contamination, not instruction.
- Validate every model-emitted identifier and citation.

## Untrusted local artifacts and parser safety

Installed plugins, archives, NIF/PEX/SWF files, DLLs, configuration, generated
output, and other mod artifacts are untrusted binary or structured inputs.

- Never load an installed DLL, execute PEX/SWF, or invoke embedded content to
  discover behavior.
- Enforce declared file-size, member-count, nesting/depth, allocation, string,
  and traversal limits appropriate to each format.
- Normalize and authorize every path; archive members and references cannot
  escape the resolved snapshot or approved product-controlled temporary area.
- Parsing must be cancellable or time-bounded and must return explicit
  malformed, unsupported, or limited states.
- Native or crash-prone parsers should be isolated behind a bounded worker
  process. The accepted Wave E design uses Job Objects for lifecycle/resource
  containment but does not call that boundary a security sandbox.
- Wave C selected no production NIF parser dependency. A future choice requires
  version/licence review, independent malformed-input fixtures, and a positive
  allowlist of supported shapes before product coverage is claimed.

Nexus acquisition follows ADR-0005 as amended by ADR-0012. The owner's
development-risk direction permits Nexus-provided API reads, GraphQL
introspection, and bounded API testing; it does not permit page scraping,
browser automation, traffic inspection, access bypass, unrelated bulk
collection, mutation, downloads, or rehosting. A negative Nexus response or
material policy change triggers ADR-0012 review and may stop the affected path.

## Credentials

- Credential entry shall use the narrowest architecture-supported handoff.
  Credentials may exist transiently in a dedicated entry control but shall not
  persist in general renderer/application state.
- Direct API-key profiles use an OS-backed reusable-secret boundary.
- Credentials never enter logs, exports, LLM prompts, or ordinary diagnostic
  traces.
- Use an OS-backed secure store where available.
- Separate credentials by provider/account.
- Permit revocation and deletion.
- After local revocation/deletion is confirmed, start no queued, new, or retry
  use of that authorization. Disclose any already in-flight request that cannot
  be cancelled and its resulting usage/cost.
- Through M4, authenticated or billable provider inference uses authorization
  supplied by the user for that account; a credential-free local provider may
  operate under its declared contract, but no project-funded or shared project
  credential is a fallback.
- ADR-0036 separately permits an explicitly selected, bounded project-funded
  credential for development and conformance work. That development authority
  is never available to the shipped product and does not weaken the preceding
  no-fallback rule.
- Managed-plan and usage-priced API access are separate profiles. Neither may
  silently fall back to the other, and plan usage is not described as free or
  converted into an invented dollar cost.
- Do not store user keys on a project-operated server in the personal/local
  architecture.

## Context minimization

LLM calls receive only task-relevant data. Common removals include:

- irrelevant absolute paths;
- Windows usernames;
- API keys/tokens;
- unrelated configuration values;
- unrelated mods and evidence.

The user accepts contextual cloud processing, but public release defaults must
remain conservative and inspectable.

Hosted web-search queries shall prefer public mod/project names, relevant
versions, one bounded interaction or symptom, and technical terms derived from
typed evidence. They shall omit credentials, account identifiers, usernames,
absolute paths, unrelated mods, private notes, and raw logs unless an accepted
operation proves a specific need. Model-selected search cannot authorize
landing-page acquisition, choose source authority, or obtain local privileged
tools. Search results remain discovery/lead data until the host separately
acquires and validates the source.

Context minimization governs what an operation sends to a provider; it does
not require deleting permitted private source material before the configured
extraction, analysis, case/finding synthesis, prose, provenance, and audit work
that depends on it has completed. Durable minimization follows the accepted
RQ-031 policy and remains distinct from provider-payload minimization.

## Exports

Diagnostic bundles require explicit selection plus privacy and source-policy
review showing:

- included files/data;
- redactions;
- material omitted or replaced because it is not redistributable;
- installation-snapshot, analysis-context, and source identities;
- whether prompts/responses are included.

Run-owned JSON, retained prompts/responses, and developer traces may contain
local paths or other sensitive diagnostic context. They must be labeled
accordingly and are not export artifacts or considered externally shareable
merely because they are inspectable or copyable. Explicitly exporting any such
artifact creates a distinct export artifact and applies the full selection and
review policy. Every user-created export retains its exact source-object
selection, filters, generator/schema version, intended sharing class,
omissions, applicable source citation/redistribution decisions, and
privacy/redaction choices without mutating its sources. Private retention
permission is not treated as external redistribution permission. Retention or
deletion controls must preview any resulting loss of active/paused-operation
resumability, downstream reuse, replayability, or auditability without implying
that the associated historical audit record is also deleted unless the user
separately selects it for deletion. They must also identify independently
retained copies containing the selected material, including exports, run-owned
outputs, and developer traces; source records and those artifacts are deleted
only when each is explicitly included, directly or through an inspectable
confirmed cascade.

## External tools

Tool executables, versions, commands, working directories, outputs, and known
product- or tool-owned cache/temp effects must be recorded. Approved operations
must not mutate MO2, mod, profile, game, configuration, generated-output, or
other user setup state through M4. Tool invocation must not inherit arbitrary
shell content from mod metadata.

For the accepted Wave B boundaries:

- MO2 profile capture requires an explicitly selected instance/profile and a
  quiescent supported state; the user's real MO2 process is not launched or
  used as an execution host, and direct USVFS operation is not authorized;
- xEdit has no product, development, dependency, fallback, or evaluation
  surface;
- Mutagen receives only Infinium-resolved ordered plugin inputs and may emit
  semantics only for positively qualified shapes;
- if LOOT coverage is delivered through the accepted libloot boundary, only
  allowlisted read-only operations are reachable; set/write/apply operations
  are forbidden;
- application/library version, immutable data identities, private userlist
  identity, and every allowed product-owned cache/temp effect are retained in
  provenance.

## Accepted Wave E security mechanisms and remaining gates

RESEARCH-0038 through RESEARCH-0041 support, and ADR-0017 through ADR-0021
accept:

- a packaged local renderer in a minimal non-elevated WPF/WebView2 host with
  deny-by-default navigation, content, permission, download, frame, and
  external-link behavior;
- role-separated, finite, schema-validated local IPC contracts over
  current-user-restricted Windows named pipes;
- Windows Credential Manager generic credentials addressed only by opaque
  non-secret profile/generation metadata;
- a coordinator-launched one-shot helper that alone presents credential entry,
  accesses the exact credential target, and dispatches one authorized provider
  request;
- handle-resolved authorization for protected-path writes and rejection of
  reparses, unexpected hard links, device/alternate-stream syntax, and
  caller-selected recursive deletion;
- typed direct process launch without a shell, with explicit executable,
  arguments, working directory, environment, inherited handles, and Job Object
  containment; and
- coordinator validation and adoption of worker-staged outputs before
  authoritative publication.

Provider-retention conformance, M4 packaging/update policy, later
shareable-export redaction, and any stronger worker isolation remain follow-up
work. Job Objects do not
restrict a
compromised process's ambient same-user filesystem or network authority; any
M1 parser or tool whose threat model requires compromise containment must gain
an accepted stronger boundary or remain excluded.
