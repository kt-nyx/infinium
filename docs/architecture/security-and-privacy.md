# Security and privacy

Status: Draft  
Last reviewed: 2026-07-25

## Security posture

Infinium reads large amounts of local state, invokes tools, stores API
credentials, retrieves untrusted content, and may use a privileged desktop
shell. Security is a product requirement even for a personal-first tool.
Normative product constraints are SEC-001 through SEC-004, AUTH-001 through
AUTH-003, AI-003, AI-007, and the applicable export/history requirements.

## Authority

- The UI receives a narrow allowlisted API.
- Privileged filesystem/process/network actions occur in a bounded worker or
  host.
- If IPC is selected, all IPC inputs are schema validated.
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

## Untrusted content

Mod descriptions, comments, README files, logs, reports, HTML, and LLM output
are untrusted data.

- Never execute retrieved scripts or remote code.
- Do not render remote HTML with privileged integration.
- Sanitize display content.
- Restrict navigation and external links.
- Treat prompt injection as data contamination, not instruction.
- Validate every model-emitted identifier and citation.

Nexus acquisition follows ADR-0005: documented supported APIs only, with no
page-scraping or browser-automation fallback. A negative Nexus response or
material policy change triggers review and stops the affected acquisition path
until the decision is superseded.

## Credentials

- Credential entry shall use the narrowest architecture-supported handoff.
  Credentials may exist transiently in a dedicated entry control but shall not
  persist in general renderer/application state.
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

## Open security research

- desktop shell isolation and update policy;
- IPC transport;
- credential storage;
- file/path authorization model;
- safe external-link behavior;
- documentation sanitization;
- subprocess restrictions;
- export redaction;
- provider data retention.
