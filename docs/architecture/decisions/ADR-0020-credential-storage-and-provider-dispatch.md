# ADR-0020: Credential storage and provider dispatch

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Infinium needs user-owned OpenAI and Nexus authorization before authenticated
provider work can enter M1. Credentials must remain outside React, ordinary
application state, the authoritative database and payload store, general
workers, prompts, logs, traces, outputs, and exports. Users also need exact
replacement, disable, deletion, and recovery semantics that cannot silently
fall back to another account or continue undispatched work after local
revocation.

[RESEARCH-0040](../../research/investigations/RESEARCH-0040-credential-entry-and-storage.md)
compared Windows Credential Manager, WinRT PasswordVault, DPAPI-backed product
storage, environment variables, local files, and delegated authorization.
[RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
recommended a dedicated one-shot credential/provider helper so neither the
coordinator nor a general worker becomes secret-bearing.
[RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
clarified that the coordinator launches and authorizes this helper; the WPF
host only requests and parents native entry UI.

This ADR selects secure storage, lifecycle, and authenticated dispatch
semantics. It does not select general renderer/path/process controls, the local
application transport, provider cost arithmetic, or a production model.

## Decision drivers

- SEC-002 requires architecture-appropriate secure storage, deletion, and
  closure of queued/new/retry authorization.
- Secret bytes must never become renderer state, ordinary IPC, durable product
  data, model context, logs, traces, diagnostics, outputs, or exports.
- The selected profile, generation, provider, purpose, account/billing scope,
  run/operation, deadline, and budget authorization must be checked again at
  dispatch.
- Auth, quota, network, scope, or billing failure must not select another
  credential or provider.
- Metadata and OS credential-store writes cannot commit atomically and
  therefore need recoverable intents.
- Local deterministic capabilities must work without a credential.
- The design must state the limitations of same-user full-trust processes and
  managed-memory handling honestly.

## Considered options

### Plaintext files, settings, environment variables, or ordinary database fields

These are easy to implement but enter broad process, logging, backup,
diagnostic, and retention surfaces. They do not satisfy SEC-002 and are
rejected.

### DPAPI ciphertext stored with product data

Current-user DPAPI avoids a separate key, but copies secret ciphertext into
the general database/backup/deletion surface and complicates restore and
machine/user migration. It is a future fallback only if an accepted credential
type cannot fit the selected Credential Manager boundary.

### WinRT PasswordVault

PasswordVault is OS-backed, but regular full-trust desktop applications do not
gain a stronger same-user boundary, its roaming posture is undesirable for the
local default, and its password/string model offers no clear advantage over a
target-specific broker.

### Windows Credential Manager generic credentials with a one-shot helper

Credential Manager provides user-scoped OS-backed storage. Exact-target-only
interop and a dedicated helper keep secret bytes out of the general process and
IPC topology. Metadata/secret atomicity, the generic-credential size limit,
same-user access, and recovery must remain explicit.

### Provider-delegated OAuth, device code, or passkey flow

This can avoid reusable local secrets when a provider supplies a suitable
flow. It is not a generic replacement for the secret-profile design and no
such flow is selected for initial OpenAI access. Future delegated flows remain
provider-specific candidates requiring research and an ADR.

## Decision

Infinium shall use Windows Credential Manager generic credentials
and a dedicated one-shot credential/provider helper for user-supplied reusable
secrets such as Nexus and OpenAI Platform API keys.

### Credential record and durable metadata

Each secret generation is stored as:

```text
Type       = CRED_TYPE_GENERIC
TargetName = Infinium:<credential-profile-id>:<generation-id>
Persist    = CRED_PERSIST_LOCAL_MACHINE
UserName   = opaque profile/generation identifier
Blob       = exact provider secret bytes
```

The helper derives and uses only this exact target from an authorized
profile/generation. Neither a target name nor secret bytes cross the renderer,
ordinary application IPC, general-worker IPC, command line, or environment.
The helper exposes no enumerate, arbitrary-target, reveal, copy, compare,
fingerprint, or secret-return operation.

The helper's reviewed Credential Manager wrapper is limited to exact-target
`CredWriteW`, `CredReadW`, `CredDeleteW`, and `CredFree`. An encoded secret
above the documented 2,560-byte generic-credential limit fails closed. It is
never truncated, split, written elsewhere, or silently redirected to DPAPI.
Enterprise persistence/roaming is not used.

The coordinator-owned database stores only opaque non-secret profile and
generation metadata: provider, purpose, display label, lifecycle and
verification state, generation, revocation epoch, account/billing-scope
metadata, capability reference, timestamps, and recoverable intent state.
Account and scope metadata remains private by default and does not prove
billing identity without separately retained provider evidence.

Ordinary inference/source credentials and any future broader administrative
usage credential are separate profiles and purposes. Infinium does not request
broader authorization merely to improve usage display.

### Enrollment and replacement

React may request enrollment or replacement only through a current
schema-validated user gesture. The coordinator creates the exact durable
non-secret intent, launches and authorizes the one-shot helper through inherited
private handles, and identifies the provider, purpose, profile, and new
generation. The WPF host only requests and parents the helper's native
non-echoing dialog; it never receives the value and cannot independently
authorize or launch a privileged helper.

The helper owns secret entry and exact-target write. It returns only
success/cancel/typed error. The flow uses a recoverable intent:

1. the coordinator records `pending_enrollment` for an exact new generation;
2. the helper writes and verifies the exact Credential Manager target;
3. the coordinator atomically activates the generation;
4. replacement makes the old generation ineligible before deletion; and
5. the helper deletes the old exact target, retaining visible
   `delete_pending` state until confirmed.

Restart recovery uses only known exact profile/generation identities. It never
enumerates the user's credential store. Replacement creates a new generation;
it never overwrites an active target in place or silently changes an existing
run/operation binding.

### Authenticated provider dispatch

The coordinator records and authorizes one non-secret attempt assignment bound
to the exact operation/run, provider profile and generation, revocation epoch,
provider, purpose, account/billing scope, endpoint/request shape, deadline,
budget reservation, response bounds, and staging identity. It then launches
one exact helper instance through inherited private handles.

Immediately before transport begins:

1. the helper asks the coordinator to revalidate the immutable assignment,
   current generation/revocation epoch, deadline, and budget dispatch
   authority;
2. only the helper resolves the exact Credential Manager target;
3. the helper binds the secret only to the selected provider host and
   qualified adapter;
4. transport start records the in-flight boundary;
5. the helper discards secret material as early as practical and clears mutable
   buffers where possible; and
6. it returns only non-secret status/usage plus a staged provider response
   manifest for coordinator validation and admission.

The helper contains no WebView, database access, application query service,
general parser/tool adapter, generic URL fetch, vault enumeration, or durable
publication authority. General workers remain secret-free. Provider SDKs or
HTTP clients that persist or log the credential require separate
qualification and cannot be used merely for convenience.

No authentication, quota, rate, network, scope, or billing failure may fall
back to another credential, account, provider, project-funded key, or shared
credential. Changing the selected profile for immutable work requires a new
explicitly authorized operation/run under the applicable reuse rules.

### Disable, deletion, and provider revocation

Infinium distinguishes:

- local disable, which closes the dispatch gate while retaining the OS item;
- local deletion, which closes the gate and removes every known exact target;
  and
- provider revocation, which the user performs through the provider's own
  console through M4.

Local deletion first increments the durable revocation epoch and makes every
generation ineligible. New, queued, paused, retry, and
reserved-but-undispatched work cannot cross the final dispatch gate. Unused
reservations are released only when the associated work is proven
undispatched.

Already dispatched work remains in flight. Infinium requests cancellation
where the qualified provider permits it, but preserves actual or unknown
usage/cost and cannot claim that local deletion revoked the provider-side key.
Physical exact-target deletion is helper-only; `deleted` is recorded only
after confirmed absence. Failures remain visible as `delete_pending` or a
typed secure-store error and never restore dispatch eligibility.

### Retention, backup, recovery, and memory limits

Secrets and Credential Manager target names are excluded from product backups,
portable configuration, run/evidence exports, diagnostic bundles, prompts,
logs, traces, crash artifacts, and synchronization. Restored or migrated
metadata requires local re-entry and a new generation; Infinium never weakens
storage merely to make credentials portable.

Deleting history does not delete a credential profile, and deleting a
credential does not rewrite historical non-secret profile/generation
provenance. Lost product metadata may require manual cleanup through Windows
Credential Manager; broad enumeration is not an M1 recovery authority.

The design does not claim protection from same-user malware, an administrator,
or a debugger. It also does not claim perfect erasure of immutable managed
strings. The implementation minimizes secret lifetime and copies, prefers
mutable native/byte buffers, clears mutable buffers where possible, and proves
non-retention through canary tests.

## M1 boundary and exclusions

Before the first authenticated OpenAI or Nexus operation, M1 shall implement
and qualify:

- one Credential Manager generic-credential backend;
- native/helper entry, exact-target write/read/delete, and one-shot provider
  dispatch;
- opaque profile/generation metadata and recoverable enrollment/deletion
  intents;
- separate provider/purpose profiles where exercised;
- verification, disable, replacement, deletion, and restart recovery;
- final generation/revocation/deadline/budget revalidation;
- queued/retry/reserved/in-flight semantics with no fallback; and
- secret-canary proof across every ordinary state, IPC, persistence,
  diagnostic, and output surface.

Deferred:

- OAuth, device-code, passkey, Windows Hello, or delegated-login flows;
- credential portability or encrypted secret backup;
- project-operated/shared credentials;
- automatic provider-account mutation or provider-side key revocation;
- multiple credential-store backends;
- background/Batch credential lifecycle;
- broader administrative credential use unless separately selected; and
- M4 uninstall, repair, migration, and public onboarding UX.

Local deterministic M1 work remains available with no credential profile.

## Consequences

### Positive

- Secret bytes remain outside the renderer, coordinator, general workers,
  ordinary IPC, durable product data, and export/diagnostic surfaces.
- Exact profile/generation and final-gate checks prevent stale or implicit
  dispatch authority.
- Replacement and deletion failures are recoverable and visible.
- Provider/account fallback and project-funded credential use are structurally
  excluded.

### Negative

- The product needs a small trusted helper, native Credential Manager interop,
  native secret entry, and recoverable two-store intents.
- Credentials are deliberately non-portable and must be re-entered after
  restore or migration.
- The 2,560-byte generic credential limit excludes larger future credential
  forms until another mechanism is reviewed.

### Risks and mitigations

- Metadata and secret writes can be half-committed. Durable intents and
  exact-target restart recovery prevent silent activation or orphan reuse.
- A same-user process may access generic credentials. Narrow target identity,
  absence from ordinary channels, process separation, and explicit threat
  disclosure reduce exposure without overstating OS isolation.
- A request can cross the dispatch boundary while deletion begins. The
  revocation epoch closes future dispatch, while in-flight cancellation and
  conservative cost reconciliation preserve honest state.
- Provider SDKs may retain or log keys. Exact-version adapter qualification and
  secret-canary evaluation gate their use.

## Requirements affected

- SEC-002 through SEC-004
- AUTH-002
- AI-002 through AI-007
- SCAN-003 through SCAN-006
- OPS-001 through OPS-003

## Validation

Acceptance selects the credential boundary; it does not prove secure
implementation.

Authenticated provider integration shall not proceed until:

- `EVAL-0034` proves secret and target exclusion from ordinary state, IPC,
  persistence, prompts, diagnostics, crash paths, outputs, and exports;
- `EVAL-0064` proves local-only operation without credentials;
- `EVAL-0077` proves exact user-owned provider/account/purpose selection and no
  fallback;
- `EVAL-0080`, `EVAL-0081`, and `EVAL-0083` prove authorized OS-backed writes,
  revocation/dispatch/budget races, and non-secret provenance;
- `EVAL-0088` proves the one-shot helper's launch, inherited-handle role,
  bounds, crash behavior, staging, and coordinator-only admission; and
- `EVAL-0089` proves exact-target enrollment, verification, replacement,
  disable, deletion, half-commit/restart recovery, unavailable-store and size
  failures, backup/restore reauthentication, and dispatch races.

Revisit this ADR if a required credential exceeds the generic-record limit, an
initial provider offers a superior reviewed delegated flow, the supported
Windows credential APIs change materially, a public-release migration
mechanism is selected, or qualified provider SDK behavior cannot preserve the
one-shot secret boundary.

## References

- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [ADR-0003](ADR-0003-read-only-authority.md)
- [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0018](ADR-0018-process-and-authority-topology.md)
- [ADR-0019](ADR-0019-local-ipc-and-application-query-contract.md)
- [RESEARCH-0040](../../research/investigations/RESEARCH-0040-credential-entry-and-storage.md)
- [RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
- [RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
