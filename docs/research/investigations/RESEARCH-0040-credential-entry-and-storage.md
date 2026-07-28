# RESEARCH-0040: Credential entry and storage

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary question: RQ-018

Decision enabled: Credential-entry and secure-storage ADR; M1 credential-broker
contract; security and provider-integration evaluation specifications

Acceptance: Recommendation accepted by the project owner through ADR-0020 on
2026-07-28

Subsequent owner disposition: this report governs user-supplied reusable API
keys and equivalent bearer secrets. The owner rejected the later
Codex/ChatGPT-plan proposal in ADR-0024. The Credential Manager mechanism here
therefore is the accepted boundary for direct OpenAI Platform API-key
access, Nexus keys, and any later accepted reusable-secret profile.

## Executive result

For the accepted Windows-only, .NET 10, WPF/WebView2 application direction,
Infinium should store user-supplied reusable provider secrets as Windows
Credential Manager **generic credentials** through the narrow Win32
`CredWriteW`, `CredReadW`, and `CredDeleteW` APIs.

The renderer must never submit, retrieve, display, copy, or otherwise receive a
stored secret. It may request that the native shell begin a credential flow,
after which the coordinator launches a dedicated one-shot credential/provider
helper. The helper's native modal collects the value and the helper writes it
directly to the credential store. That helper is the only Infinium component
allowed to invoke Credential Manager or observe secret bytes. It exposes no
`get secret`, `list credentials`, or generic vault operation to the renderer.

Infinium's durable application store should contain only:

- an opaque credential-profile identity;
- provider, purpose, display label, and non-secret account/billing-scope
  metadata;
- a credential generation from which only the helper derives the exact
  Credential Manager target;
- lifecycle, verification, and capability state; and
- immutable operation/run bindings to the selected profile and generation.

The target name itself is not persisted in SQLite, payloads, backups, or
ordinary IPC.

The Credential Manager entry should use:

- `CRED_TYPE_GENERIC`;
- an opaque target such as `Infinium:<credential-profile-id>:<generation-id>`;
- `CRED_PERSIST_LOCAL_MACHINE`, not roaming/enterprise persistence; and
- a credential blob containing only the provider secret, subject to the Win32
  generic-credential limit of 2,560 bytes.

The recommendation does not make a WebView renderer, the database, a run, or a
budget reservation an authorization holder. Immediately before network
dispatch, the trusted provider boundary must revalidate the exact credential
profile, generation, provider, purpose, account/billing scope, local
revocation epoch, and operation authority. It then resolves the secret for
that attempt only. There is no automatic fallback to another profile,
credential, account, project, or project-funded key.

Local disable/delete must close the dispatch gate before attempting physical
credential deletion. Queued, retry, paused, reserved-but-undispatched, and new
work then cannot use the authorization. Work that has crossed the final
dispatch gate is in flight: Infinium requests cancellation when supported,
reconciles any resulting usage/cost, and discloses work that cannot be
cancelled. Provider-side credential revocation remains a user action in the
provider's own account console through M4; local deletion is not represented
as provider-side revocation.

This mechanism is sufficient for M1, subject to an accepted ADR and
conformance tests. PasswordVault/Credential Locker, DPAPI-backed ciphertext in
the product database, plaintext settings, environment variables, browser
storage, consumer-account OAuth invention, and a project-operated secret
service are not selected.

## 1. Question, requirements, and decision boundary

RQ-018 asks:

> Which secure credential-entry and storage mechanisms fit the selected
> desktop architecture?

The controlling requirements and accepted decisions are:

- `AUTH-002`: credential writes use an approved OS-backed store and remain
  separate from protected modding roots;
- `SEC-002`: secrets stay outside ordinary application/render state and never
  enter prompts, logs, exports, or ordinary traces; users can revoke and
  delete stored credentials;
- `SEC-003`: privileged operations use narrow, validated allowlists;
- `SEC-004`: credentials are excluded from diagnostics and exports in all
  cases;
- `AI-003`: credentials never enter model/task context;
- `AI-004`: credential revocation blocks work even when budget was reserved
  and any uncancellable actual usage remains visible;
- `AI-005`: provider usage, quota, rate, cost, and credit facts are exposed
  only when reliably available and remain distinct;
- `AI-007`: authenticated/billable work uses a credential supplied by the
  user for the explicitly selected account and has no project/shared-key
  fallback;
- ADR-0012: Nexus API operations require the same later credential boundary;
- ADR-0013: OpenAI is the only required initial LLM provider, while credential
  entry/storage and hard-budget enforcement remain separate decisions; and
- accepted ADR-0017: the desktop direction is a non-elevated
  WPF/WebView2 host with React/TypeScript presentation and an independently
  executable .NET engine.

The report defines an M1 mechanism for API keys and equivalent opaque bearer
secrets. It did not itself:

- accept RESEARCH-0038's application-stack recommendation, later accepted by
  ADR-0017;
- select the RQ-017 process/IPC topology, later accepted by
  ADR-0018/ADR-0019;
- decide RQ-032's complete UI, path, subprocess, navigation, and export
  security boundary, later accepted by ADR-0021;
- define RQ-034's cost-reservation ledger, later accepted by ADR-0023;
- enable OpenAI background Responses, Batch, or prompt caching;
- define a future provider-delegated login flow;
- mutate a provider account or revoke a key at the provider;
- operate a project-side credential service; or
- select M4 installer/uninstaller, migration, backup, or enterprise policy.

If a later ADR supersedes the shell with a materially different design, the invariant
credential-broker contract remains applicable but the entry control and secure
store binding require re-review.

## 2. Sources, versions, and research method

Sources were retrieved on 2026-07-28. The relevant OS interfaces are stable
Windows contracts rather than independently versioned products.

| Source | Relevant contract |
|---|---|
| Microsoft, [`CREDENTIALW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentialw) | `CRED_TYPE_GENERIC`, 2,560-byte blob maximum, and session/local-machine/enterprise persistence semantics |
| Microsoft, [`CredWriteW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew) | Creates or replaces one credential in the current token's logon-session credential set |
| Microsoft, [`CredReadW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw) | Reads one credential selected by target and type |
| Microsoft, [`CredDeleteW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-creddeletew) | Deletes one credential selected by target and type |
| Microsoft, [Kinds of credentials](https://learn.microsoft.com/en-us/windows/win32/secauthn/kinds-of-credentials) | Generic credentials are application-defined and can be read/written by user processes |
| Microsoft, [Handling passwords](https://learn.microsoft.com/en-us/windows/win32/secbp/handling-passwords) | Current Windows guidance prefers eliminating secrets, then Credential Manager, then DPAPI for persistent local secrets; collect late and discard early |
| Microsoft, [Credential Locker for Windows apps](https://learn.microsoft.com/en-us/windows/apps/develop/security/credential-locker) | `PasswordVault` works from WPF/WinForms; credentials can roam and the app abstraction has its own cardinality/identity behavior |
| Microsoft, [`PasswordVault`](https://learn.microsoft.com/en-us/uwp/api/windows.security.credentials.passwordvault?view=winrt-28000) | A regular non-AppContainer desktop app can access the user's lockers rather than receiving an AppContainer-scoped vault boundary |
| Microsoft, [`CryptProtectData`](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata) | User-scoped DPAPI is normally bound to the same logon credential and computer, provides integrity protection, and differs sharply from machine scope |
| Microsoft, [.NET `ProtectedData`](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata?view=windowsdesktop-10.0) | Windows-only managed wrapper over DPAPI |
| Microsoft, [Secure WebView2 applications](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security) | Treat WebView content as insecure, validate message origin and parameters, avoid generic proxies, and keep WebView hosts non-elevated |
| Microsoft, [WPF `PasswordBox.Password`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.passwordbox.password?view=windowsdesktop-10.0) | Reading `Password` creates a plaintext managed string |
| Microsoft, [.NET `SecureString`](https://learn.microsoft.com/en-us/dotnet/api/system.security.securestring?view=net-10.0) | Not recommended for new .NET development; eventual use still requires plaintext and it is not a complete secret-use boundary |

Repository evidence included the accepted product documents, ADR-0001 through
ADR-0014, RESEARCH-0028's provider-profile/capability boundary,
RESEARCH-0032/0033's OpenAI-first result, and the application stack subsequently
accepted by ADR-0017.

No live credential was used and no real secret was read. No runtime proof can
be honest before an implementation exists. This report therefore performs a
contract comparison and failure/race analysis, not a security-certification
claim.

## 3. Threat and authority model

### 3.1 Protected against

The M1 design must prevent routine or accidental disclosure through:

- React component state, browser storage, developer tools, DOM inspection,
  query strings, URLs, web messages, or renderer crash state;
- ordinary engine/UI messages and presentation-query results;
- the relational evidence/history store and content-addressed payload store;
- scan configurations, run manifests, prompts, model context, tool calls, and
  source-acquisition requests;
- structured logs, HTTP logs, traces, exception text, telemetry, exports, and
  diagnostic bundles;
- user-selected account/profile display and provider capability metadata;
- automatic fallback or confused-deputy selection of another credential; and
- continued queued/new/retry use after local authorization is revoked.

### 3.2 Not protected against

Neither Credential Manager nor DPAPI is a sandbox from:

- malware or injected code running as the same Windows user with sufficient
  access;
- an administrator, debugger, or memory-dump collector that can inspect the
  credential-bearing process;
- a compromised provider, account, operating system, or trusted provider
  adapter;
- provider-side retention or billing after a request has been dispatched; or
- a user copying a key from the provider portal before Infinium receives it.

Microsoft explicitly documents that generic credentials can be read and
written by user processes. The accepted boundary is defense against Infinium's
ordinary renderer/data/diagnostic surfaces and accidental disclosure, not a
claim of same-user malware resistance. That limitation must be present in the
security model rather than hidden behind the phrase “secure store.”

### 3.3 Trusted components

The minimum credential trusted-computing boundary is:

1. the dedicated one-shot credential/provider helper, including its native
   entry control, three exact-target Credential Manager operations, and
   attempt-scoped provider adapter;
2. the inherited private handle channel used to bind that helper to one
   coordinator-authorized enrollment or dispatch assignment; and
3. the Windows credential and logon-key services.

The React renderer, evidence/history database, job records, model, acquired
content, coordinator, general IPC contracts, parser/tool workers, and external
tools are outside that secret-bearing boundary. ADR-0018/ADR-0021 accept the
helper placement that closes RQ-017's process-boundary gap. Process
separation never justifies sending a secret through React, application gRPC,
worker gRPC, command-line arguments, environment variables, SQLite, or disk
payloads.

## 4. Alternatives

| Option | Benefits | Material costs/risks | Disposition |
|---|---|---|---|
| Win32 Credential Manager generic credential | Purpose-built OS credential storage; direct exact-target read/write/delete; no secret ciphertext in Infinium's database; Windows/.NET fit; Microsoft-preferred persistent-secret mechanism | Same-user processes can read generic credentials; Win32 interop and buffer handling; 2,560-byte blob limit; metadata/credential activation is not one OS transaction; local-machine/user binding complicates backup | **Recommend for M1** behind an allowlisted broker |
| WinRT `PasswordVault` / Credential Locker | Managed API; intended for desktop credentials; add/retrieve/remove lifecycle | Regular full-trust desktop apps are not AppContainer-isolated from the user's other lockers; roaming semantics are contrary to a deliberately local credential default; `PasswordCredential` encourages password/string-shaped handling; no advantage over a target-specific broker for Infinium | Reject as M1 baseline |
| DPAPI `ProtectedData`, `CurrentUser`, ciphertext in product store | Windows-native cryptography; no explicit key management; can transactionally version ciphertext beside metadata; avoids credential-store enumeration | Secret ciphertext enters the general data backup/retention surface; any same-user process with ciphertext can normally decrypt; database restore/migration is machine/user-bound; deletion and accidental duplication require blob-level auditing | Credible fallback only if Credential Manager's size/operational constraints fail a future supported credential type |
| DPAPI machine scope | Service/multi-user convenience | Any user on the machine can decrypt; violates the intended per-user boundary | Reject |
| `SecureString` as storage | Reduces some plaintext-memory duration | Microsoft does not recommend it for new .NET; provider/Win32 use still requires plaintext conversion; not durable OS storage | Reject as storage or security boundary |
| Plaintext settings, JSON, SQLite field, command line, environment variable, browser storage | Simple | Violates SEC-002/SEC-004; easily inherited, logged, backed up, inspected, or exported | Reject |
| User pastes the key for every operation without storage | No durable secret | Poor multi-hour resumability and background usability; repeated renderer/UI exposure; does not solve trusted use boundary | Optional “use once” mode later, not the M1 baseline |
| Project-hosted secret vault/proxy | Central policy and delegated login possibilities | Changes the local product, privacy, billing, operations, and business model; user keys would leave the local architecture | Reject through M4 |
| Windows Hello/passkey | Avoids reusable secrets when the remote provider supports it | Initial providers require their own API authorization; Infinium cannot convert Windows Hello into an OpenAI/Nexus API credential | Revisit only for a provider-documented delegated flow |

The comparison selects Credential Manager because it is the narrowest
purpose-built Windows storage mechanism for the present API-key shape. It does
not assert that Credential Manager offers process-level isolation.

## 5. Accepted M1 credential model

### 5.1 Non-secret durable metadata

The application store should represent:

```text
CredentialProfile {
  credential_profile_id
  provider_id
  purpose                  // inference, source acquisition, optional admin usage
  display_label
  auth_mode
  credential_generation
  lifecycle_state
  revocation_epoch
  created_at
  updated_at
  last_verified_at
  verification_state
  verification_failure_class
  last_used_at
  account_or_organization_id
  billing_scope_type
  billing_scope_id
  billing_scope_display_label
  scope_selection_method
  capability_snapshot_id
}
```

Account, project, organization, and billing-scope values are non-secret but
private metadata. They remain local by default and are excluded from ordinary
external exports unless explicitly required and reviewed. A user-entered label
is not verified billing identity; provider-returned scope evidence remains
separately sourced.

An ordinary inference key and any broader administrative usage/cost key are
separate profiles and purposes. Infinium must not ask for broader
administrative authorization merely to improve the usage display.

### 5.2 Credential Manager record

For each active generation:

```text
Type       = CRED_TYPE_GENERIC
TargetName = Infinium:<credential-profile-id>:<generation-id>
Persist    = CRED_PERSIST_LOCAL_MACHINE
UserName   = opaque profile/generation identifier
Blob       = exact provider secret bytes
```

Provider name, account email, project name, and key prefix need not appear in
the Credential Manager target or username. The application database supplies
the user-facing label. `CRED_PERSIST_ENTERPRISE` is excluded so Infinium does
not intentionally roam a credential to another computer.

The broker must fail closed when the encoded secret exceeds 2,560 bytes. It
must not truncate, split, write plaintext elsewhere, or silently switch to
DPAPI. Supporting a larger future credential type requires explicit mechanism
qualification.

The M1 implementation should own a minimal reviewed interop wrapper around
`Advapi32`:

- import only `CredWriteW`, `CredReadW`, `CredDeleteW`, and `CredFree`;
- derive and read/delete only the exact target from helper-received opaque
  profile/generation identifiers;
- never expose `CredEnumerate`, arbitrary target lookup, or a raw credential
  result above the broker;
- map `ERROR_NOT_FOUND`, unavailable logon session, invalid parameter, and
  other OS failures to typed states;
- use safe handles/finalization for returned native allocations; and
- clear mutable plaintext buffers as soon as possible.

### 5.3 Entry and replacement

The React renderer may send only a narrow request such as:

```text
begin_credential_enrollment {
  credential_profile_draft_id
  expected_provider_id
  expected_purpose
  replacement_of_generation?
}
```

After origin, schema, state, and user-gesture validation, the coordinator
creates an exact non-secret enrollment intent and launches the dedicated
one-shot helper through inherited private handles. The helper opens the native
modal itself; the WPF host only parents/presents that flow and never receives
the entered value. The secret never becomes a web-message or ordinary IPC
field. The dialog:

- identifies the provider, purpose, and selected profile/account label;
- uses a masked native password control and allows paste because provider keys
  are normally copied;
- does not prepopulate, reveal, or copy back an existing secret;
- obtains the value only on submission;
- applies bounded length/shape checks without logging the value;
- clears the control on submit, cancel, close, timeout, or error; and
- returns only success, cancellation, or a typed error to the renderer.

Reading a WPF `PasswordBox.Password` produces a managed plaintext string.
`SecurePassword` does not eliminate eventual plaintext conversion and
`SecureString` is not recommended as a new .NET security boundary. The
implementation must therefore prefer mutable native/byte buffers where its
Credential Manager and HTTP boundaries permit, minimize unavoidable managed
string lifetime and copying, and never claim reliable erasure of immutable
managed strings. The important architectural control is an opaque credential
handle outside the renderer and durable stores, not a `SecureString` label.

Metadata and Credential Manager cannot commit atomically. Enrollment and
replacement should use a recoverable intent:

1. write a durable non-secret `pending_enrollment` record containing the new
   profile/generation identifiers, not a target name;
2. have the helper derive the target and write the secret to that exact target;
3. atomically activate the new profile generation in the application store;
4. for replacement, mark the old generation ineligible before removing its
   credential; and
5. have the helper derive and delete the old exact target, retaining
   `delete_pending` until confirmed.

On restart, Infinium resolves every pending intent from its profile/generation
identifiers and the helper-derived exact target; it never needs broad
credential enumeration. A failed/crashed enrollment cannot
silently activate a missing secret. A failed old-generation deletion remains
visible as residual secure-store material even though it is no longer eligible
for dispatch.

### 5.4 Verification

Provider adapters should define a bounded non-inference authentication probe
when an authoritative supported API supplies one. Verification records only
typed results and non-secret provider/account/scope evidence.

States should include:

```text
unverified
verified
rejected
scope_mismatch
verification_unavailable
secure_store_missing
secure_store_unavailable
```

An offline/network failure is not an invalid credential. A `401`/equivalent
does not justify deletion or fallback; it blocks automatic retry until the user
replaces or explicitly rechecks the profile. A rate limit is not an
authentication failure. Where no bounded validation call exists, explicit
save-as-unverified may be allowed with a visible status.

## 6. Dispatch and secret-use boundary

### 6.1 No reveal operation

The renderer and general presentation/query API may:

- list configured credential profiles and their non-secret status;
- begin add/replace;
- request verify, disable, re-enable, or delete;
- select a profile for a scan configuration; and
- view capabilities and non-secret usage metadata.

They may not:

- read, reveal, copy, export, fingerprint, or compare secret values;
- supply a Credential Manager target name;
- select an arbitrary store target;
- ask the broker to enumerate the user's credentials; or
- cause a provider request without a separately validated operation/run
  authority.

### 6.2 Attempt-scoped resolution

The coordinator authorizes one non-secret dispatch assignment of the following
shape, delivered to the dedicated helper through its inherited one-shot
channel:

```text
resolve_for_dispatch {
  operation_id
  provider_profile_id
  expected_generation
  expected_revocation_epoch
  expected_provider_id
  expected_purpose
  expected_billing_scope
  deadline_and_budget_authorization_ref
}
```

The coordinator and helper together must:

1. validate the immutable operation/run binding and selected profile;
2. validate current lifecycle/generation/revocation state;
3. pass RQ-034's budget reservation and deadline checks;
4. let only the helper derive and read the exact current Credential Manager
   target from the authorized profile/generation;
5. bind the secret only to the expected provider host and adapter inside that
   helper;
6. recheck the coordinator-owned dispatch gate immediately before starting
   transport;
7. classify transport start as the in-flight boundary; and
8. discard the helper attempt's secret material immediately after request
   construction/dispatch, clearing mutable buffers where possible.

The provider HTTP layer must never log authorization headers, query-embedded
tokens, complete request headers, or SDK configuration objects containing a
secret. It should not keep a long-lived provider client whose configuration
retains the key when an attempt-scoped header or credential callback is
possible. Any SDK that requires long-lived plaintext credential storage needs
separate review before qualification.

A renderer reload, UI restart, database query, run replay, or checkpoint does
not carry a secret. It carries the profile/generation reference and must
re-resolve current authorization before any future dispatch.

### 6.3 Account and purpose binding

The same physical secret must not acquire new authority merely because a user
selects it in a different field. Provider profile purpose and verified
account/billing scope are checked at dispatch. Authentication, quota, rate, or
billing failure cannot trigger another credential. If a user wants to switch
profiles, that occurs only in a new user-authorized operation/run under the
applicable immutability rules.

## 7. Disable, deletion, and provider revocation

### 7.1 Lifecycle states

At minimum:

```text
pending_enrollment
active_unverified
active_verified
disabled
rejected
revocation_pending
delete_pending
deleted
secure_store_missing
secure_store_error
```

“Revocation” must be qualified:

- **local disable** closes the dispatch gate but retains the secure-store item;
- **local deletion** closes the gate and removes the Credential Manager item;
- **provider revocation** invalidates the key at the provider and is performed
  by the user through the provider's own console through M4.

Infinium may link to the provider console after validated external navigation.
It must not imply that local deletion invalidated a key that may still exist
elsewhere.

### 7.2 Race-safe local deletion

Deletion should:

1. atomically increment the profile's revocation epoch and set
   `revocation_pending`, making every generation ineligible;
2. prevent new, queued, paused, retry, and reserved-but-undispatched work from
   crossing the dispatch gate;
3. cancel or terminate local undispatched attempts and release only their
   unused reservations;
4. request cancellation of already dispatched work where supported;
5. have the helper derive and delete the exact Credential Manager target for
   each known profile generation;
6. set `deleted` only after confirmed absence, treating `ERROR_NOT_FOUND` as
   already absent; and
7. retain typed `delete_pending`/error state and retry instructions if physical
   removal cannot be confirmed.

An attempt that passed the final gate and began transport before the revocation
epoch changed is in flight, even if its response has not arrived. Its provider
usage/cost may remain unknown or continue. It remains visible and is reconciled
under the provider and RQ-034 contracts.

For provider-managed background work, deletion may remove Infinium's ability
to poll or cancel the provider object. Before confirming deletion, the UI must
show that consequence and attempt cancellation where authorized. After
deletion, no authenticated poll may use the removed credential merely to make
history look complete. The provider request identity and unresolved
usage/cancellation state remain as an honest audit gap. ADR-0013 keeps
background/Batch outside the initial default, so this does not expand M1.

### 7.3 Replacement and renewal

Replacement creates a new generation and never overwrites the active target in
place. New work can select the new generation only after its activation.
Existing immutable operation/run bindings do not silently change credentials.
Pending undispatched work bound to the retired generation stops and requires a
new user-authorized run or operation according to the job/run ADR.

The old target is deleted after activation and remains visibly
`delete_pending` if removal fails. Infinium does not store a hash of the secret
or claim that a replacement value differs; generation records the user action,
not secret equality.

## 8. Usage, quota, and provider-account information

Usage/account display is outside the secret store. It should reference the
credential profile but preserve RESEARCH-0028's distinctions:

- per-attempt provider-reported token/tool usage;
- versioned locally estimated cost;
- rate-window headroom/reset;
- historical administrative usage/cost where separately authorized;
- configured spend limit;
- local Infinium budget/reservation state; and
- prepaid credit balance, which remains unavailable unless a provider supplies
  reliable authority.

The ordinary OpenAI inference credential must not be silently treated as an
administrative credential. If a provider requires a broader key for historical
usage/cost, the user creates a separate credential profile with purpose and
scope disclosed. Absence of that profile yields an explicit unavailable/not
authorized capability rather than blocking inference.

Provider account, organization, project, and billing-scope identifiers are
retained only as non-secret host-authored metadata. They never enter model
context. A capability refresh may resolve its credential at dispatch under the
same broker, deadline, authorization, and logging rules as any other provider
operation.

## 9. Diagnostics, memory, and failure handling

### 9.1 Prohibited retention

Credential bytes and secret-bearing objects must not enter:

- browser/React state or browser persistence;
- ordinary IPC serialization;
- application settings or relational/content stores;
- run/configuration/checkpoint/evidence artifacts;
- prompts, Responses input, hosted-search query, source requests, or model
  output;
- structured logs, traces, exception messages, HTTP capture, metrics labels,
  exports, clipboard output, or screenshots captured by the product; or
- full-memory crash dumps retained or exported by Infinium.

Redaction after logging is not the primary control; secret-bearing fields and
headers should never be offered to the logging pipeline. Error messages expose
provider/status/request IDs and a typed failure class, not request headers,
URLs containing tokens, credential target names, or raw SDK objects.

Release builds should not retain full process dumps for a process that can hold
a secret. Developer-initiated OS/debugger memory capture is outside Infinium's
ability to sanitize and must be treated as credential-bearing private material,
never as an exportable diagnostic.

### 9.2 Memory limitation

The broker and provider adapter minimize plaintext lifetime and clear mutable
buffers. They cannot honestly guarantee erasure of immutable managed strings,
HTTP/TLS library internals, provider SDK internals, paging, hibernation, or an
external process dump. M1 qualification must inspect the selected adapter and
logging configuration for accidental copies rather than asserting that a
language type made memory “secure.”

### 9.3 Failure behavior

| Failure | Required result |
|---|---|
| Credential target missing | Profile becomes `secure_store_missing`; no fallback; local-only work remains available |
| Credential store/logon session unavailable | Typed capability failure; no plaintext fallback or repeated blind retry |
| Secret exceeds Win32 limit | Enrollment fails before write with supported-size explanation |
| Metadata commit fails after credential write | Pending profile/generation intent lets the helper derive and resolve/delete the exact target on restart |
| Old target deletion fails during replacement | New generation may remain active; residual old target is visibly `delete_pending` |
| User deletes while work is queued/reserved | Dispatch blocked; queued work stops; unused reservation released |
| User deletes while request is in flight | Cancellation requested where possible; actual/unknown usage and cost remain visible |
| Provider rejects credential | Mark rejected for automatic work; do not delete, fallback, or interpret as rate exhaustion |
| Network/provider unavailable during verification | Remain unverified/unavailable, not rejected |
| Restored product data refers to unavailable credential | `reauthentication_required`/missing; never try a different target |
| Provider/account scope differs from confirmed scope | Fail closed with scope mismatch; require explicit user correction/new run |

## 10. Portability, backup, retention, and uninstall

Credentials are deliberately not part of:

- product data backups;
- run/evidence exports;
- diagnostic bundles;
- portable configuration exports; or
- synchronization between machines/users.

A backup/export may contain a redacted requirement such as “credential profile
`X` for OpenAI inference must be re-entered,” but not a Credential Manager
blob, secret target suitable for arbitrary lookup, or key fingerprint.
Restoring or moving Infinium data marks credential profiles as
`reauthentication_required` until the user enters and verifies a new local
generation.

Credential Manager and user-scoped DPAPI both depend on Windows user/machine
state and can become unavailable after profile/account recovery events.
Re-entry is the recovery path. Infinium must not weaken to machine-wide DPAPI,
plaintext backup, or enterprise roaming merely to make a backup portable.

Product-history retention and credential retention are separate controls.
Deleting a run does not delete a credential profile; deleting a credential
does not rewrite historical run/provider references. History preserves the
non-secret profile/generation identity and marks future secret-dependent replay
unavailable.

Complete uninstall cleanup belongs to RQ-030/M4. The mechanism must make it
possible to enumerate Infinium's own profile records and delete each exact
helper-derived target after explicit user confirmation. If the product
database is lost while a Credential Manager item remains, Windows Credential
Manager is the manual recovery/removal surface; broad scanning of the user's
credential set is not an M1 authorization.

## 11. Accepted M1 subset

M1 should implement and qualify only:

1. one Windows Credential Manager broker for generic API-key credentials;
2. a dedicated one-shot credential/provider helper with native add/replace
   entry for the accepted WPF/WebView2 shell and a non-echoing helper/CLI path
   for development;
3. opaque provider-profile/generation metadata and recoverable
   enrollment/deletion intents;
4. exact-target write/read/delete with local-machine persistence;
5. OpenAI inference/source-discovery purpose and Nexus acquisition purpose as
   separate profile types where exercised;
6. attempt-scoped resolution and final dispatch-gate revalidation;
7. no fallback to another credential/account;
8. verify, disable, replace, and delete lifecycle;
9. queued/retry/in-flight handling integrated with the accepted job and budget
   boundaries;
10. non-secret profile/capability/usage status for the UI/CLI; and
11. logs, traces, prompts, outputs, diagnostics, and export tests proving
    absence of the secret.

Deferred beyond the M1 subset:

- OAuth/device-code/passkey/delegated-login flows;
- Windows Hello gating of local secret use;
- credential portability or encrypted secret backup;
- project-operated/shared credentials;
- automatic provider-account mutation or key revocation;
- enterprise policy/managed identity;
- multiple credential-store backends;
- provider background/Batch secret lifecycle; and
- M4 uninstall, repair, and migration UX.

Local deterministic M1 capabilities must run without any credential profile.

## 12. Evaluation implications

Existing evaluation cases should gain the following exact obligations:

| Case | Required RQ-018 coverage |
|---|---|
| `EVAL-0034` | Secret is absent from renderer state/storage/messages, database/content payloads, prompts, request bodies, logs, traces, exceptions, exports, and product crash artifacts; deletion closes the gate before physical removal and exposes in-flight uncertainty |
| `EVAL-0064` | Local-only work starts with no credential store/profile and provider work becomes an explicit unavailable capability |
| `EVAL-0076` | Profile status and non-secret usage/capability facts remain distinct; missing admin access and prepaid credits are not invented |
| `EVAL-0077` | Exact selected profile/generation/account/purpose is used; auth/quota/scope failure never falls back |
| `EVAL-0080` | Only exact Infinium-owned Credential Manager targets are written/deleted; direct and renderer-driven arbitrary target access is unreachable |
| `EVAL-0081` | Delete/disable races with reservation, dispatch, retry, synchronous abort, and any later enabled background mode without releasing actual spend or authorizing new work |
| `EVAL-0083` | Historical invocation provenance contains profile/generation and verified scope metadata but no secret, secret hash, or exportable vault value |
| `EVAL-0089` | Owns enrollment/replacement/deletion intent recovery, Credential Manager availability and size limits, half-commit crashes, backup/restore reauthentication, and full credential lifecycle consistency before authenticated integration |

Additional synthetic/integration cases should cover:

- entry attempts from the wrong WebView origin, malformed messages, and no
  current user gesture;
- cancellation/close/crash at every enrollment/replacement intent boundary;
- credential blob at/above the size limit;
- missing/locked/unavailable credential store;
- crash after secret write but before metadata activation;
- failed deletion and restart recovery;
- replace/delete/dispatch interleavings;
- `401`, `403`, `429`, timeout, offline, and billing-scope mismatch;
- renderer reload and UI restart while a provider operation runs;
- malicious source/model text asking for credentials or broker operations;
- developer/release logging configurations and exception paths; and
- backup/restore on the same and a different Windows user/machine.

Passing these cases proves the selected product boundary against the tested
implementation. It does not prove resistance to same-user malware or an
administrator/debugger.

## 13. Uncertainty and limitations

1. ADR-0017 accepts RQ-016's direct WPF/WebView2 stack. The durable
   helper/broker boundary does not depend on WPF, but its native dialog
   parenting and presentation require re-review if the shell changes.
2. ADR-0018/ADR-0020/ADR-0021 accept the one-shot helper and inherited
   private-handle placement; its launch/handshake path still needs
   implementation qualification.
3. RESEARCH-0043 subsequently defined the accepted atomic
   dispatch/reservation transaction. This report defines the credential-side
   gate and race semantics in ADR-0023.
4. No .NET Win32 wrapper, OpenAI adapter, Nexus adapter, or logging stack has
   undergone runtime secret-copy or crash analysis.
5. Credential Manager does not isolate generic secrets from other same-user
   processes. A future stronger Windows sandbox/service boundary would require
   separate architecture work.
6. Exact Windows editions, enterprise policies, unavailable logon-session
   behavior, credential-roaming policy, and user/profile recovery cases need
   implementation conformance.
7. The 2,560-byte generic credential limit is sufficient for ordinary API keys
   but has not been proven for every future provider authorization artifact.
8. Provider SDKs may internally retain credentials or log request
   configuration. Each selected SDK/version requires inspection and
   adversarial tests.
9. Provider-side revocation, expiry, and scope-discovery behavior remains
   provider-specific and volatile.

These are qualification obligations, not reasons to put secrets in a more
convenient surface.

## 14. Accepted recommendation and ADR

ADR-0020 accepts a credential boundary that:

1. accepts Windows Credential Manager generic credentials as the M1 secure
   store;
2. fixes `CRED_TYPE_GENERIC`, helper-derived opaque target identity,
   `CRED_PERSIST_LOCAL_MACHINE`, exact-target-only broker operations, and the
   2,560-byte fail-closed limit;
3. accepts a dedicated one-shot credential/provider helper whose native
   entry/replacement control and attempt-scoped dispatch keep secrets outside
   the shell, coordinator, ordinary IPC, and general workers;
4. requires opaque credential-profile/generation metadata and recoverable
   non-secret enrollment/deletion intents;
5. prohibits renderer/database/log/prompt/export access and any reveal/list or
   generic vault operation;
6. requires attempt-scoped provider resolution, final dispatch-gate
   revalidation, exact provider/purpose/account binding, and no fallback;
7. distinguishes local disable, local deletion, and user-performed provider
   revocation;
8. specifies queued/retry/reserved/in-flight behavior and honest usage/cost
   reconciliation;
9. excludes credentials from backup/export and requires re-entry after
   migration/recovery;
10. records same-user-process and managed-memory limitations explicitly; and
11. gates authenticated M1 integration on EVAL-0034, EVAL-0077, EVAL-0080,
    EVAL-0081 where concurrent billable work is exercised, EVAL-0083, and the
    additional lifecycle/failure cases above.

RQ-018 is resolved for M0 by accepted ADR-0020. RQ-017, RQ-032, and RQ-034
consume this boundary without broadening renderer authority or treating a
profile reference, stored secret, dispatch authorization, and budget
reservation as the same concept. Implementation and conformance remain
pending.
