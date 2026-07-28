# RESEARCH-0041: Security-boundary controls

Status: Completed  
Date: 2026-07-28  
Last reviewed: 2026-07-28  
Researcher: Codex agent  
Primary question: RQ-032  
Decision enabled: Security-boundary ADR; M1 filesystem/process/renderer
conformance specifications

Acceptance: Recommendation accepted by the project owner through ADR-0021 on
2026-07-28

Subsequent owner disposition: the reusable-secret helper is the accepted
boundary for direct OpenAI API-key and Nexus-key dispatch. The later
Codex/ChatGPT-plan provider-process proposal was rejected in ADR-0024 and adds
no process or authority to this security model.

## Executive result

For the accepted .NET/WPF-WebView2/coordinator/named-pipe/worker topology,
Infinium should use a **layered, deny-by-default security boundary** rather
than treating WebView2, IPC, process separation, or path normalization as a
security boundary by itself.

The recommended boundary is:

1. a non-elevated WebView2 shell that loads only packaged React assets from one
   application origin, renders source/model/tool content as inert structured
   text, denies remote navigation and browser capabilities, and exposes no
   host objects;
2. a small versioned WebView message contract whose exact sender origin,
   operation, schema, size, sequence, and user-gesture requirements are checked
   before the native host relays a request;
3. a coordinator that remains the only application authorization, durable
   state, query, scheduling, budget, and publication authority, reached through
   the role-separated, current-user-only named-pipe contracts accepted by
   ADR-0019;
4. a dedicated trusted reusable-secret/provider-dispatch helper, separate
   from all parser and tool workers, which alone performs native API-key
   entry, exact-target Credential Manager access, and authorized API-key
   provider dispatch;
5. handle-bound filesystem authorization built from fixed write classes,
   product-owned roots, an immutable protected-root registry, final
   handle-resolved paths and file identities, reparse/hard-link rejection, and
   handle-relative descendant operations rather than string-prefix checks;
6. typed subprocess adapters that launch exact absolute executables without a
   command shell, with fixed argument schemas, a constructed environment and
   working directory, explicit inherited handles, and Job Object containment;
7. per-attempt worker staging only: the coordinator independently validates
   staged manifests and bytes, admits content into the content-addressed store,
   and commits authoritative references; and
8. structured diagnostics and explicit export sharing classes, with
   credentials excluded by construction and potentially sensitive private
   artifacts never treated as externally shareable merely because they can be
   inspected or copied.

This is feasible as a bounded M1 mechanism. M1 should implement the common
authorization/path/process/staging controls, the CLI/coordinator boundaries,
and the dedicated helper before the first authenticated provider operation.
It should keep run-owned JSON inside product-controlled storage (and permit
human output on stdout) rather than add arbitrary user-selected export
destinations. The WPF/WebView2 controls become mandatory when that shell is
exercised.

Windows Job Objects and ordinary same-user worker processes provide crash,
lifetime, handle, and resource containment. They do **not** prevent a
compromised worker from using the Windows user's ambient filesystem or network
rights. AppContainer is a credible stronger parser-worker boundary, but its
runtime, file-brokering, native-library, and user-installed-tool compatibility
must be prototyped before selection. Until then, Infinium must not describe a
general worker as sandboxed.

The recommendation is research, not an accepted architecture or proof that an
evaluation has passed. It assumes the proposals in RESEARCH-0038 through
RESEARCH-0040 only to make the controls concrete.

## 1. Question and governing constraints

RQ-032 asks:

> Which sanitization, navigation, protected-root/write-destination
> authorization, subprocess, and export-redaction controls satisfy AUTH-002,
> SEC-001, SEC-003, and SEC-004 in the selected architecture?

The controlling requirements are:

- `AUTH-001`: no write may mutate MO2, the modlist, game, profile,
  configuration, or generated output through M4;
- `AUTH-002`: product writes use approved product-controlled, OS-backed, or
  explicitly selected non-protected authority;
- `AUTH-003`: only qualified non-mutating external-tool operations are
  eligible;
- `SEC-001`: HTML, documentation, logs, model/tool output, and binary/static
  inputs are untrusted data and cannot grant authority or execute;
- `SEC-002`: credentials stay outside ordinary renderer/application state;
- `SEC-003`: filesystem, process, network, navigation, and tool operations use
  narrow validated allowlists; and
- `SEC-004`: credentials are excluded from all diagnostics/exports and
  externally shareable artifacts require inspectable selection/redaction.

Accepted ADR-0001 keeps untrusted claims and model output outside authority.
ADR-0003 prohibits setup mutation. ADR-0008 through ADR-0011 require
non-mutating MO2/Mutagen/libloot behavior, while ADR-0012 through ADR-0014 add
untrusted Nexus/OpenAI/managed-data inputs without granting them local
authority.

This report supplied controls for the following subsequently accepted Wave E
boundaries:

- RESEARCH-0038: .NET 10, a minimal non-elevated WPF/WebView2 shell, local
  React assets, and an independently usable engine/CLI/worker;
- RESEARCH-0039: one coordinator owns the database, authorization, scheduling,
  query, and durable publication; clients and workers use separate local
  named-pipe contracts; and
- RESEARCH-0040: generic credentials use an exact-target Credential Manager
  broker and attempt-scoped dispatch authorization.

RQ-030 still owns signing, installation, update channels, and release
distribution. RQ-034 owns atomic cost reservation and reconciliation. This
report defines the security interfaces those decisions must preserve.

## 2. Threat model and authority map

### 2.1 Inputs treated as hostile

The boundary assumes malformed or adversarial:

- Nexus/author/local HTML and Markdown;
- source text, metadata, URLs, citations, images, and filenames;
- logs, reports, configurations, archives, plugins, PEX/SWF/PE/static binary
  data, LOOT data, and native parser inputs;
- model prompts returned as data, model prose, tool calls, refusals, structured
  output, and hosted-search results;
- renderer messages, gRPC/protobuf messages, cursors, IDs, paths, export
  selections, and worker manifests;
- subprocess stdout/stderr, exit codes, result files, and inherited
  environment state; and
- stale or aliased filesystem state, including junctions, symbolic links,
  mount points, hard links, alternate data streams, device paths, and changes
  between validation and use.

An untrusted value can select only data already reachable through an
authoritative typed identity. It cannot create a filesystem, process, URL,
credential, provider, database, or model-operation capability.

### 2.2 Trusted components

The minimum trusted components are:

- the coordinator's authorization and publication services;
- the product-write/path authorization module;
- the exact typed process launcher and worker supervisor;
- the WPF host's origin/message/navigation gate when the graphical shell is
  present;
- the dedicated native credential/provider-dispatch helper;
- the selected OS credential, file, process, named-pipe, and WebView2
  primitives; and
- accepted adapter code for each exact external operation.

React is trusted to present the packaged application, but it is not an
authorization boundary. General workers, native parsers, external tools,
acquired content, model output, and the database's stored values are not
trusted to grant authority. A database row or payload containing a string that
looks like a path, URL, operation, or credential target remains data.

### 2.3 Privilege and network posture

All Infinium executables run as the interactive standard user. The UI,
coordinator, helper, and worker paths must fail rather than request elevation.
There is no Windows service, administrator broker, remote client, TCP control
listener, or browser-direct coordinator connection through M4.

Only an accepted source/provider adapter may make network requests. WebView2
does not fetch source pages, images, fonts, scripts, updates, or API data.
General parser/tool workers receive no credentials and no network operation.

## 3. WebView2 origin and renderer controls

### 3.1 Packaged origin

The host should map one non-resolving HTTPS name such as
`https://app.infinium.invalid` to the versioned packaged renderer directory
using `SetVirtualHostNameToFolderMapping` with cross-origin access denied.
Microsoft documents that virtual-host mapping gives local content an ordinary
origin and that cross-origin access remains subject to the selected
`CoreWebView2HostResourceAccessKind` and CSP. The host should navigate only to
the known entry document and bundle-manifest resources.

The renderer must not load through `file:`, `NavigateToString`, a local HTTP
server, a remote application site, or acquired HTML. The WebView2 user-data
folder is a validated product-controlled location and contains no credentials
or authoritative evidence store. Browser extensions, OS-account SSO,
autofill/password saving, and service workers are disabled or unused.

The release CSP should be equivalent to:

```text
default-src 'none';
script-src 'self';
style-src 'self';
img-src 'self';
font-src 'self';
connect-src 'none';
media-src 'none';
object-src 'none';
frame-src 'none';
worker-src 'none';
base-uri 'none';
form-action 'none';
manifest-src 'self';
require-trusted-types-for 'script';
trusted-types 'none';
```

There is no `unsafe-inline`, `unsafe-eval`, remote origin, `blob:`, or `data:`
exception in the baseline. The renderer uses bundled CSS classes rather than
inline styles. If a required UI dependency cannot run under this policy, the
dependency must change or a reviewed CSP change must explain the new sink and
evaluation; development convenience is not sufficient.

### 3.2 Inert HTML, Markdown, and prose

M1 should not render raw HTML at all.

- Plain text is presented through React text children/text nodes, never
  `dangerouslySetInnerHTML`, `innerHTML`, `document.write`, `eval`, or
  `ExecuteScript`.
- Source HTML is retained as evidence where policy permits, but displayed as
  escaped source or as separately extracted plain text. Its tags, styles,
  event handlers, forms, frames, media, and URLs are not mounted into the app
  DOM.
- Markdown, if needed, is parsed with raw-HTML support disabled into a small
  typed AST. The renderer constructs React elements for paragraphs, headings,
  lists, emphasis, code, and link labels. It never converts untrusted Markdown
  to an HTML string.
- Link destinations remain data and are handled only by the external-link
  operation below. Inline remote images and arbitrary URI-bearing elements are
  not supported.
- Model prose, tool output, logs, and search snippets follow the same path.

React's own documentation warns that passing untrusted markup to
`dangerouslySetInnerHTML` creates an XSS hole. CSP and Trusted Types are
defense in depth; they do not turn unreviewed HTML into trusted content.

If a later milestone proves that rich source HTML is essential, it requires a
separate reviewed sanitizer decision: a pinned maintained sanitizer such as
DOMPurify, a minimal positive element/attribute/protocol allowlist, no SVG or
MathML by default, a single named Trusted Types policy, post-sanitization
mutation prohibition, and adversarial fixtures. It is not part of M1.

### 3.3 Navigation and browser capability policy

The WPF host must:

- cancel every top-level or frame `NavigationStarting` event except the exact
  packaged origin and known application routes/resources;
- deny every frame, `NewWindowRequested`, `DownloadStarting`,
  `PermissionRequested`, basic-authentication, file-picker, screen-capture,
  notification, geolocation, camera, microphone, clipboard-read, and
  unexpected script-dialog request;
- intercept resource requests and fail non-application origins even if a CSP
  regression occurs;
- disable host objects (`AreHostObjectsAllowed = false`) and never call
  `AddHostObjectToScript`;
- keep web messaging enabled only for the exact application document and
  validate the source on every message; and
- avoid executing dynamically constructed script. Host-to-renderer data uses
  `PostWebMessageAsJson` with a real JSON serializer.

Microsoft's WebView2 security guidance explicitly recommends origin checking,
parameter validation, specific messages instead of generic proxies, and a
standard-user host. Navigation events and download/permission events allow the
host to cancel the corresponding browser actions.

External links never navigate WebView2. The renderer sends only an opaque
stored-link identity. The coordinator resolves it to the exact provenance URL
and returns a typed external-link descriptor. The host:

1. parses an absolute URI;
2. accepts `https` only for the baseline;
3. rejects credentials/userinfo, control characters, IP-literal local
   targets, and every non-HTTPS scheme including `file`, `javascript`, `data`,
   `ms-*`, and custom handlers;
4. opens accepted/registry-approved source hosts in the user's default browser
   after an explicit click; and
5. shows the normalized host and requires confirmation for an otherwise valid
   unregistered host.

That final launch is a dedicated HTTPS-browser operation, not the generic
subprocess API. It may use the OS URL association, but accepts no executable,
verb, argument list, working directory, or environment from the renderer.

### 3.4 Web message contract

The host accepts one operation-specific JSON envelope with:

- protocol major/minor and application build;
- coordinator/renderer instance and document epoch;
- request ID, correlation ID, operation enum, and optional idempotency key;
- one DTO selected by the operation enum; and
- a 64 KiB encoded-message ceiling, with lower per-operation limits.

It rejects wrong source/origin, frames, stale document epochs, unknown
operations or fields where strictness is required, duplicate non-idempotent
requests, malformed JSON, oversized/deep payloads, non-finite numbers, and
unexpected Unicode/control characters in identifier fields. A current user
gesture is required for credential enrollment, external-link opening,
retention/deletion confirmation, and any later export picker.

The bridge contains no path read/write, SQL, URL fetch, process launch,
provider dispatch, credential, generic object, or arbitrary gRPC method. A
renderer operation becomes only a request to the host/coordinator; it is never
authorization.

## 4. Coordinator and IPC controls

The ADR-0019 transport is accepted with the following mandatory security
properties:

- one application-client endpoint and a separate worker endpoint;
- local Windows named pipes only, with `CurrentUserOnly`, an explicit
  current-user/elevation DACL, remote-client rejection, finite buffers, and no
  TCP/reflection/browser endpoint;
- an endpoint descriptor under a restrictive product runtime root;
- coordinator-instance/fencing epoch and an ephemeral nonce in the handshake;
- caller role derived from the endpoint/launch relationship, not a claimed
  payload field;
- generated protobuf contracts, explicit major-version rejection, method and
  message limits in both directions, deadlines, rate limits, and bounded
  streams; and
- independent coordinator authorization of every request against lifecycle,
  immutable run/context, path, credential, provider, budget, and operation
  state.

The application endpoint exposes typed queries and durable product commands,
not arbitrary filesystem/database/provider/tool methods. The worker endpoint
accepts only one launch-bound assignment, progress, cancellation, staged
manifest, and terminal receipt. A worker bootstrap nonce is delivered through
an inherited one-shot channel and bound to the expected launched process; it
is not a reusable bearer credential or a way to reach application queries.

Named-pipe DACLs and nonces protect against accidental cross-user,
wrong-elevation, stale-instance, and unlaunched-client connections. They do not
claim to protect against arbitrary malicious code already running as the same
user. Authorization must therefore remain operation-specific even after the
transport handshake.

## 5. Credential and provider-dispatch helper

RESEARCH-0040 correctly keeps secrets outside React, the database, prompts,
logs, exports, and ordinary application messages, but it left the broker's
process placement open. Placing `CredReadW` in the coordinator would make every
database/query/scheduler dependency part of the credential-bearing process.
Placing it in a general worker would expose credentials to untrusted
parser/tool code and the worker protocol.

The bounded M1 placement should instead be one dedicated trusted native/.NET
helper executable with two exact modes:

1. **Enrollment mode:** show a native non-echoing entry/replacement dialog for
   one coordinator-created enrollment intent and call only exact-target
   `CredWriteW`/`CredDeleteW`.
2. **Dispatch mode:** for one already authorized provider attempt, call
   exact-target `CredReadW`, construct and send that provider request, discard
   the secret as early as practical, and stage the non-secret response plus a
   non-secret status/usage receipt.

The helper contains no WebView, database access, parser/tool adapter, generic
vault enumeration, secret-reveal method, arbitrary URL fetch, or application
query service. General workers can never address Credential Manager.

### 5.1 Launch and final dispatch

The coordinator first records an exact non-secret intent/assignment containing:

- provider profile and credential generation;
- provider, account/billing scope, and purpose;
- operation/run/attempt identity;
- endpoint/request shape and allowed redirect policy;
- deadline, request/payload identity, response limits, and staging identity;
- local credential revocation epoch; and
- budget reservation/dispatch authority supplied by RQ-034.

It launches the exact helper binary suspended, places it in its Job Object,
passes only an explicit duplex bootstrap handle set using
`PROC_THREAD_ATTRIBUTE_HANDLE_LIST`, and then resumes it. No secret, launch
nonce, request body, path, or credential target is placed on the command line
or in the environment. A pair of inherited anonymous pipes or equivalent
one-shot private handle channel is sufficient; no broadly discoverable helper
service is required.

Immediately before network dispatch, the helper asks the coordinator through
that channel to confirm the assignment, credential generation/revocation
epoch, deadline, and budget authorization. The coordinator can deny but cannot
return secret bytes. Once confirmed, the helper resolves the exact Credential
Manager target itself and dispatches once. Disable/delete closes this final
gate before physical deletion as required by RESEARCH-0040.

The helper returns only non-secret structured status/usage and a staged-output
manifest. Model/source response bytes may be sensitive, but are not
credentials; they remain in the assigned staging area and are admitted by the
coordinator through the same path as worker output. A helper crash or
indeterminate network result is reconciled as an explicit uncertain attempt,
not retried merely because the helper disappeared.

This split is feasible for the first OpenAI and Nexus operations. It adds one
small executable and one-shot launch channel, but avoids a persistent secret
service and keeps secret bytes out of React, application gRPC, worker gRPC,
command lines, environment blocks, SQLite, and disk payloads. Provider SDK
selection must still prove that it does not log or persist the key internally.

## 6. Filesystem and write authorization

### 6.1 Write classes

Every write carries one closed enum selected by coordinator code:

| Class | Authorized location | Caller supplies |
|---|---|---|
| Settings/history/database/checkpoint | Fixed product data root | Object ID and typed content, never a path |
| Cache/content store | Fixed product cache/data root | Content identity/manifest only |
| Attempt temp/staging | Coordinator-created per-attempt root | Relative typed artifact name only |
| Diagnostics/run-owned output | Fixed product diagnostic/run root | Run/artifact identity only |
| Credential | Exact Infinium Credential Manager target | Opaque profile/generation through helper only |
| Update staging | Separately accepted installer/update-controlled root | Deferred to RQ-030 |
| User export | Explicitly selected non-protected directory plus new leaf | Deferred beyond bounded M1 |

No generic `write(path, bytes)`, `delete(path)`, `copy(from, to)`, or
`run(command)` exists at any client/worker boundary.

### 6.2 Protected-root registry

At profile/setup capture, the coordinator builds a versioned protected-root
registry covering at least:

- Skyrim game root and `Data`;
- the selected MO2 instance, profile, mods, overwrite, downloads, configuration,
  and organizer-managed state;
- game/profile configuration under the user's Documents/Saved Games locations;
- detected generator input/output and other generated-output roots;
- user-installed MO2/LOOT and configured external-tool install/config/data
  roots; and
- every separately discovered unmanaged/root component location.

Including downloads and tool roots is deliberately conservative: Infinium has
no reason to write them through M4. Each root records the submitted display
path, handle-resolved final volume path, volume serial/file ID, source and
snapshot/context identity, and whether resolution was complete. Missing,
unopenable, ambiguous, network, or unsupported filesystem roots make a
destination ineligible rather than weakening the rule.

The registry is immutable for a run/operation. A changed root creates new
context/authorization state. Protected roots are denied write targets whether
reached directly, through `..`, case/short-name variation, mount point,
junction, symbolic link, or another alias.

### 6.3 Handle-bound authorization algorithm

`Path.GetFullPath`, `PathCchCanonicalize`, and case-insensitive string-prefix
comparison are useful for display/input rejection but are not authority.
Microsoft's `PathCchCanonicalize` documentation explicitly says it cannot by
itself turn untrusted paths into subpath/identity-safe comparisons. Windows
reparse points and hard links make name-only checks insufficient.

The common write module should:

1. reject relative roots, device/NT namespace input, alternate data streams,
   wildcards, trailing-dot/space ambiguity, reserved components, and
   unsupported/network filesystems for product-owned writes;
2. open the authorized root/destination directory as a handle, resolve it with
   `GetFinalPathNameByHandleW`, and obtain `FILE_ID_INFO`;
3. compare the final component-bounded path and root identity against every
   protected root; never use simple string prefix;
4. retain that directory handle for the operation;
5. traverse product-owned descendants one component at a time relative to the
   retained directory handle, with no separators, `.`/`..`, colon, or device
   syntax in a component and with reparse-point-open semantics so any reparse
   component is rejected;
6. use the documented user-mode `NtCreateFile` `RootDirectory` form (or a
   separately qualified equivalent) for handle-relative opens/creates so the
   validated directory cannot be swapped between check and use;
7. use `FILE_CREATE` for new artifacts, not overwrite/truncate; for an existing
   product object, open once without delete sharing, validate the final
   handle/path/identity/link count, and perform the operation on that same
   handle;
8. reject existing writable product files with unexpected multiple hard links,
   every reparse point, or changed identity; and
9. revalidate retained root identity before destructive batches and abort on
   mismatch.

`FILE_ID_INFO` combines volume serial and file ID to identify an opened object
on one computer. `GetFinalPathNameByHandleW` identifies the opened target.
`NtCreateFile` supports names relative to an already opened directory handle.
These properties close the ordinary check-then-reopen path race. M1 must
prototype this small P/Invoke module against NTFS and supported Windows
versions before relying on it.

User-selected export later follows the same rule: the native picker returns a
path, but the coordinator opens and validates the final parent directory,
retains its handle, rejects protected targets, and creates a **new** simple
leaf relative to that handle. Overwrite/replace, network shares, cloud
placeholders, unsupported filesystems, and reparse-bearing descendants remain
disabled until separately qualified. A renderer-supplied path never becomes a
destination capability.

Same-user malware or an administrator can tamper with Infinium's process,
handles, memory, or ACLs and is outside this boundary. The handle design is
still required for static aliases and ordinary TOCTOU safety; the same-user
limitation is not permission to use prefix checks.

### 6.4 Deletion

Retention/deletion begins from durable object IDs and an inspectable
coordinator-generated deletion plan. The plan enumerates every exact database
record and product-owned payload/checkpoint/export copy, previews
replay/resume/audit consequences, and requires confirmation of that plan
revision. Execution revalidates identity and uses handles within the fixed
product roots.

There is no caller-selected recursive delete. A changed, hard-linked, reparse,
unrecognized, or outside-root object is skipped with a failure/gap. Protected
roots are never eligible, even if a corrupted database claims ownership.

## 7. Subprocess and worker controls

### 7.1 One typed launch path

Each eligible executable operation has a compile-time/manifest adapter that
declares:

- exact application/helper/worker or validated user-installed executable
  identity and allowed version;
- operation enum and fixed argument grammar;
- required input/output/staging capabilities;
- working directory and environment allowlist;
- stdin/stdout/stderr behavior and byte/time limits;
- child-process, CPU, memory, handle, and cancellation policy;
- network/credential requirements; and
- expected product/tool cache/temp effects.

The launcher uses an absolute executable path and direct process creation.
For ordinary .NET launches, `UseShellExecute=false` and `ArgumentList` prevent
file-association and single-string command construction. For the hardened
worker/helper path, a Win32 launcher should use `CreateProcessW` suspended,
an explicit Unicode environment block, `STARTUPINFOEX` with only the required
inherited handle list, Job Object assignment, and then resume.

Forbidden:

- `cmd.exe`, PowerShell, WSH, batch files, shell verbs, file associations,
  arbitrary scripts, PATH executable search, or `UseShellExecute=true`;
- concatenated argument strings or arguments copied from mod/source/model
  text;
- inherited provider credentials, proxy credentials, arbitrary `PATH`,
  `COMSPEC`, temp directories, working directory, or unrelated environment;
- inheritable coordinator/database/credential handles; and
- an adapter that accepts arbitrary executable, command, URL, or operation
  names.

The sole shell-associated exception is the separately implemented validated
HTTPS external-browser operation in section 3.3.

The child environment is constructed from a documented minimal set needed by
that exact binary. `TEMP`/`TMP` and working directory point to its per-attempt
product staging root. Standard output/error are drained asynchronously,
bounded, decoded with an explicit policy, treated as untrusted text, and never
embedded verbatim in logs or errors.

### 7.2 Job Object containment

The coordinator creates a private Job Object before launch, uses
`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`, disallows breakaway, sets an active
process limit appropriate to the adapter, and applies measured memory/CPU/time
limits where safe. Launch-suspended/assign/resume avoids a child escaping
before assignment. Closing or cancelling the assignment terminates the
contained process tree only when the adapter's cooperative/cancellation
contract permits it.

Microsoft documents that Job Objects manage and limit a process tree and that
kill-on-close terminates associated descendants. It also states that Job
Objects do not apply modern process security limits; security permissions
remain per process. Job containment therefore limits lifetime/resources and
contains crashes. It is not a filesystem/network/credential sandbox.

### 7.3 Per-attempt staging and publication

The coordinator creates one random per-attempt staging directory under the
product staging root, records its identity/quota, and assigns only that
capability. A worker may write only declared relative artifact names there.
It cannot open SQLite, the authoritative content store, another attempt's
staging area, credentials, or a durable publication method.

On worker/helper completion:

1. the worker closes outputs and submits a bounded manifest containing
   relative names, declared types, sizes, hashes, and assignment identity;
2. the coordinator fences the attempt and prevents further publication;
3. it independently opens each staged object through the safe path module,
   rejects reparse points, hard links, extra files, wrong types, excessive
   counts/sizes, changed files, and schema violations;
4. it recomputes hashes while reading bounded bytes;
5. it writes/adopts verified bytes into a new content-addressed object through
   coordinator-owned handles; and
6. one coordinator transaction records the content identity, validation,
   provenance, and durable result references.

Staging names, paths, manifests, and worker success are never authoritative
content identities. Failed admission leaves a typed failed/limited/gap state
and quarantined or deletable staging, not a partial publication.

### 7.4 Stronger isolation option

AppContainer/LPAC can provide real process, file, registry, credential, and
network isolation. Microsoft describes it as a Windows security boundary and
documents explicit DACL/capability grants. It is attractive for parsers that
can consume copied read-only inputs and emit only staged outputs.

It is not selected for M1 without a bounded prototype because Infinium must
prove:

- self-contained .NET/native dependency startup;
- access to exact copied inputs and output staging without broad user-data
  capability;
- named-pipe/bootstrap compatibility;
- Mutagen/libloot/native loader behavior;
- no-network execution;
- useful diagnostics and cancellation; and
- behavior on the exact supported Windows baseline and packaging model.

User-installed MO2/LOOT operations are especially poor candidates for an
unproven AppContainer because they may rely on ordinary desktop paths and
subprocess behavior. If a milestone requires stronger compromise containment
before the prototype passes, the affected parser/operation remains excluded
rather than being called sandboxed.

## 8. Diagnostics, development controls, and exports

### 8.1 Structured diagnostics

Logging uses a closed event schema and allowlisted fields. It must not serialize
arbitrary requests, exceptions, DTOs, HTTP headers/bodies, environment blocks,
renderer messages, source/model text, stdout/stderr, or credential objects.

- IDs, counts, hashes, versions, bounded status codes, duration, and declared
  path aliases are preferred.
- Untrusted strings are length/control-character bounded and stored as
  evidence payloads when needed, not copied into log templates.
- Absolute paths are represented by stable aliases in ordinary traces.
- HTTP authorization/cookie headers and provider SDK wire logging are
  disabled.
- Secrets, secret hashes, credential blobs/targets where sensitive, command
  lines, environment blocks, and native entry-control state are prohibited.
- Release errors return typed codes; stack traces and raw developer artifacts
  stay in labeled private diagnostic storage.
- Infinium-created process dumps are disabled for the credential helper and
  provider dispatch. OS/user/administrator-forced memory dumps are outside the
  same-user threat model and must not be represented as prevented.

Secret canary tests must exercise success, error, cancellation, crash,
enrollment, replacement, delete, network, logging, tracing, CLI, JSON, staging,
and export paths.

### 8.2 WebView2 developer controls

Release builds set `AreDevToolsEnabled=false`, disable default context menus
and browser accelerator paths that open DevTools, expose no remote debugging,
and ship no source maps containing sensitive build/runtime state.

WebView2 documents that environment/registry browser arguments can enable a
remote-debugging port and expose WebViews to third-party debuggers. A release
must clear inherited debugger environment variables before WebView creation
and fail closed (disable the privileged message bridge and report an error) if
effective `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`,
`WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER`, or documented per-app policy contains
remote-debugging/script-debugger switches. It must not silently accept
arbitrary additional browser flags.

Development builds may enable DevTools only behind an explicit build/runtime
control with a persistent visual warning and a disposable renderer data root.
While DevTools/remote debugging is enabled, real credential enrollment,
Credential Manager access, billable/provider dispatch, external-tool launch,
and protected/local evidence access through the renderer bridge are disabled.
Those boundaries remain testable through the CLI/helper integration harness.

### 8.3 Sharing classes and export policy

Every retained/output object has one sharing class:

| Class | Meaning |
|---|---|
| `InternalPrivate` | Authoritative store/cache/checkpoint/staging; not an export |
| `PrivateDiagnostic` | Run-owned JSON, prompt/response, raw trace, or source-bearing artifact; inspectable locally but not externally shareable |
| `LocalPrivateExport` | Explicit user-created copy that may retain sensitive/restricted data and is labeled not ready to share |
| `ExternallyShareable` | Separately generated, previewed, redacted, and source-policy-reviewed artifact |

Changing class creates a new export artifact; it never relabels or mutates the
source. An export records source object/revision identities, filters,
generator/schema version, intended class, included/omitted fields, path
aliasing, source redistribution decisions, privacy/redaction policy, and final
destination identity.

Redaction is structural and happens before serialization:

- credentials and authorization headers are impossible fields;
- usernames and absolute paths become declared aliases unless explicitly
  required in a private diagnostic export;
- raw source text, prompts/responses, logs, and local notes are excluded from
  `ExternallyShareable` by default and require individual policy permission;
- restricted/private source bodies are replaced by citations, hashes, and
  omission reasons;
- mod/profile names and provider/account identifiers are separately
  previewable fields; and
- generated prose is rechecked for secret/path canaries and labeled as
  generated, not used as the redaction mechanism itself.

M1 emits human-readable stdout and versioned run-owned JSON in the fixed
product output root, both labeled for possible sensitivity. It does not
implement `LocalPrivateExport`, `ExternallyShareable`, or arbitrary
destinations. Later exports require the handle-bound destination mechanism in
section 6 and the full EVAL-0040/SEC-004 review.

## 9. Alternatives considered

| Option | Disposition | Reason |
|---|---|---|
| Render acquired HTML in the privileged WebView origin | Reject | Untrusted active content would share an origin with the native message bridge |
| Render sanitized raw HTML in M1 | Reject/defer | Plain text and typed Markdown AST cover the initial need with a much smaller sink; rich HTML needs a separate sanitizer decision |
| Allow remote app/source navigation inside WebView2 | Reject | Expands browser/network/origin authority and makes source content bridge-adjacent |
| Expose .NET host objects or a generic message proxy | Reject | Converts renderer compromise into native method discovery/authority |
| Let UI/CLI/workers open SQLite or accept arbitrary paths/SQL | Reject | Bypasses one authorization and publication authority |
| Put Credential Manager access in the coordinator | Reject for M1 | Makes the broad durable/query process credential-bearing |
| Put credentials in a general provider/parser worker | Reject | Secrets would cross the general worker boundary and coexist with untrusted parsers/tools |
| Dedicated one-shot credential/provider helper | Recommend | Small exact TCB; no persistent service; secrets never cross general IPC |
| String canonicalization/prefix-only path checks | Reject | Reparse points, hard links, aliases, component boundaries, and TOCTOU invalidate the proof |
| Handle-resolved roots plus relative handle operations | Recommend | Binds authorization and operation to opened Windows objects |
| `Process.Start` with executable/argument strings from adapters | Reject | Search, quoting, shell, environment, and confused-deputy risks |
| Exact typed launcher plus Job Object | Recommend baseline | Strong lifetime/resource/process-tree containment with explicit sandbox limit |
| AppContainer for all workers immediately | Defer to prototype | Stronger isolation, but unproven .NET/native/tool/file-broker compatibility and packaging cost |
| Elevated broker/Windows service | Reject through M4 | Unnecessary authority and a materially larger installation/IPC security surface |
| Arbitrary user-selected exports in M1 | Defer | Run-owned JSON proves the output contract without prematurely expanding write destinations/redaction UX |

## 10. Bounded M1 subset

M1 should implement and qualify:

1. a shared coordinator-side authorization vocabulary for operation, role,
   immutable run/context, write class, path capability, credential generation,
   provider purpose, deadline, and budget authority;
2. fixed product data/cache/staging/diagnostic roots plus the protected-root
   registry;
3. the handle-resolved/handle-relative Windows write module, initially on the
   exact supported local filesystem/Windows baseline;
4. durable-ID-based retention/deletion planning with no caller recursive path;
5. the exact typed direct process launcher, explicit environment/handles,
   suspended Job Object assignment, and bounded stdout/stderr;
6. one isolated worker using assigned per-attempt staging and coordinator
   validation/admission/publication;
7. the dedicated credential/provider-dispatch helper and inherited one-shot
   bootstrap channel before any authenticated OpenAI or Nexus operation;
8. application/worker named-pipe roles, limits, nonce/epoch, and fail-closed
   authorization from RESEARCH-0039;
9. structured allowlisted diagnostics and credential canary tests;
10. local human output and versioned run-owned JSON in product-controlled
    storage; and
11. when the WPF/WebView2 shell or its M1 spike is exercised, the exact local
    origin/CSP, inert rendering, navigation/capability denial, message schema,
    external-link, and release-debug controls above.

M1 may defer:

- rich sanitized HTML;
- user-selected exports and externally shareable bundles;
- export overwrite/replace, network/cloud destinations, and unsupported
  filesystems;
- AppContainer/LPAC worker enforcement, provided no worker is called
  sandboxed and the operation's accepted threat model does not require it;
- installer/update staging beyond the later RQ-030 decision;
- OS-level network denial for general workers;
- remote clients, Windows service, elevation, and cross-user operation; and
- polished graphical security UX beyond the bounded WebView2 prototype.

Authenticated/provider M1 work cannot defer the dedicated helper. Native or
crash-prone parser M1 work cannot defer process isolation, bounds, and
staging/publication validation.

## 11. Evaluation obligations

No case is passed by this research. Existing cases should gain these concrete
obligations:

| Evaluation | Required RQ-032 proof |
|---|---|
| `EVAL-0033` | Hostile HTML/Markdown/model/search/tool/log content remains inert; CSP, raw-HTML prohibition, no host objects, exact-origin messages, and no generic operation prevent it from obtaining native methods, secrets, source policy, local state, or tools |
| `EVAL-0034` | Secret canaries are absent from React, WebView storage/messages, application/worker gRPC, command line/environment, SQLite/CAS/staging, logs/traces/errors/dumps, stdout/JSON, and exports; only the dedicated helper observes secret bytes |
| `EVAL-0035` | Wrong-origin/stale/oversized/unknown messages, wrong pipe role/nonce, arbitrary path/URL/SQL/command/argument/provider/credential target, direct/aliased protected roots, reparse/hard-link/device/ADS inputs, and stale capabilities fail closed |
| `EVAL-0040` | M1 run-owned output remains distinct from later exports; each later class records selection/redaction/source policy/destination; credentials are impossible and protected destinations are rejected |
| `EVAL-0046` | Every qualified user-installed external-tool operation uses its exact adapter/identity/args/env/cwd/temp contract, reaches no shell or mutation operation, records its allowed effects, and leaves all protected setup roots unchanged |
| `EVAL-0080` | Every settings, database, cache, staging, checkpoint, trace, credential, deletion, output, and later export write uses its declared class and stays in authority under direct, junction, symlink, mount, short-name/case, hard-link, component-boundary, changed-path, and check/use adversaries |
| `EVAL-0088` | Internal worker/helper launch, inherited handles, environment, Job Object containment, role/protocol boundaries, staging, and coordinator-only publication remain bounded across startup races, malformed input, slow clients, and crashes |

Related cases:

- `EVAL-0064`: local CLI/coordinator/worker use has no WebView, credential, or
  network dependency;
- `EVAL-0077`: only the exact helper dispatches with the selected
  user-supplied credential after current coordinator authorization;
- `EVAL-0081`: dispatch authorization, budget reservation, credential
  disable/delete, helper crash, retry, and indeterminate response cannot
  oversubscribe or use stale authority;
- `EVAL-0082`: security/analyzer/source/budget/cache/tracing controls and
  effective values remain independently configurable and retained; and
- `EVAL-0083`: accepted staged bytes and conclusions retain the full
  worker/helper/operation/security-policy provenance without treating transport
  or model/source content as authority.

Additional adversarial fixtures should cover:

- CSP violation and Trusted Types enforcement in the packaged runtime;
- raw HTML, malformed Markdown, dangerous URL schemes, punycode/confusable
  hosts, redirects, downloads, permissions, frames, new windows, and resource
  requests;
- DevTools/remote-debugging environment and registry switches in release;
- malformed/replayed/out-of-order WebView and gRPC messages;
- cross-user, wrong-elevation, stale-instance, wrong-role, and spoofed-helper
  pipe connections;
- process launch quoting, PATH search, inherited handles/environment, early
  child spawn, breakaway, output flooding, timeout, and process-tree cleanup;
- worker output mutation after manifest, extra files, decompression/allocation
  bombs, hash/size/schema mismatch, and stale fenced publication;
- product root/destination replacement, nested reparse points, aliases into
  protected roots, existing hard links, and deletion-plan drift; and
- secret-provider success/error/cancel/crash/revocation races and SDK logging.

## 12. Accepted recommendation and ADR

ADR-0021 accepts a security boundary that:

1. accepts the layered renderer/host/coordinator/helper/worker authority map;
2. fixes local packaged inert WebView2 content, strict CSP/Trusted Types, no
   raw HTML/host objects/remote navigation, and exact message controls;
3. accepts role-separated bounded named-pipe contracts with coordinator-only
   authorization and publication;
4. places native credential entry, exact-target Credential Manager access, and
   provider dispatch exclusively in a dedicated one-shot helper, with no
   secret-bearing general IPC;
5. accepts fixed write classes, the protected-root registry, handle-resolved
   identity, handle-relative operations, reparse/hard-link rejection, and
   durable-ID deletion;
6. accepts typed no-shell process launch, explicit args/env/cwd/handles,
   launch-suspended Job Object containment, and its non-sandbox limitation;
7. accepts per-attempt staging with independent coordinator validation/CAS
   admission/database publication;
8. accepts structured diagnostics and the four sharing classes, while
   deferring arbitrary/export-sharing destinations beyond M1;
9. requires release DevTools/remote-debugging fail-closed behavior and
   privilege disabling in development-debug mode;
10. records AppContainer as a stronger parser-worker candidate requiring a
    prototype rather than an implied current guarantee; and
11. gates each exercised boundary on the applicable evaluations in section 11.

RQ-032 is resolved for M0 by accepted ADR-0021. Acceptance does not prove the
WebView2 runtime, Windows file P/Invoke module, helper, process launcher,
worker containment, redaction, or evaluation cases.

## 13. Confidence, uncertainty, and reopen triggers

Confidence:

- **High** that acquired/model/tool content must never render as active HTML in
  the privileged application origin.
- **High** that the renderer, clients, and workers need narrow typed contracts
  and cannot authorize arbitrary paths/processes/URLs/credentials.
- **High** that credentials must be isolated from general workers and ordinary
  IPC; the dedicated helper is a bounded feasible M1 placement.
- **High** that string-only path checks and check-then-reopen writes cannot
  satisfy the protected-root requirement.
- **High** that worker staging must be independently admitted and published by
  the coordinator.
- **Medium-high** that the proposed `NtCreateFile`-based handle-relative module
  is the right Windows M1 mechanism; it needs a real P/Invoke/filesystem spike.
- **Medium** that the exact CSP works unchanged with the selected React
  component stack.
- **Medium** that AppContainer is worth the compatibility cost for selected
  parser workers; no M1 selection is made.

Material uncertainties:

1. the exact supported Windows/filesystem baseline, path-length behavior, and
   `NtCreateFile` P/Invoke/return-status handling need integration proof;
2. WebView2 environment/enterprise-policy precedence and every debugger path
   require testing against the pinned runtime;
3. future component libraries may require CSP/Trusted Types changes;
4. provider SDKs may copy secrets or payloads internally and need
   version-specific inspection;
5. user-installed external tools may require environment, child-process, or
   temp behavior that makes a proposed adapter ineligible;
6. Job Object limits can break tools that already use jobs unless exact
   operation tests prove compatibility;
7. same-user malicious code remains outside named-pipe, Credential Manager,
   ordinary worker, and product-root ACL guarantees; and
8. user-selected export overwrite, cloud/network destinations, source-policy
   UX, update security, and AppContainer compatibility remain later work.

Reopen the decision if:

- the application stack or process topology changes materially;
- the handle-relative file prototype cannot meet functional/compatibility
  requirements;
- an M1 operation requires a threat model stronger than same-user process
  containment;
- the first AppContainer prototype proves a low-cost reliable baseline;
- WebView2 cannot enforce the required origin/CSP/debug controls;
- a provider supplies a delegated authorization flow that removes stored
  secrets; or
- product scope adds remote clients, elevation, services, write-capable mod
  operations, or shared/community exports.

## 14. Source register

Primary sources were retrieved on 2026-07-28. API package versions shown in
Microsoft URLs are documentation views, not implementation pins.

| Subject | Primary source | Used for |
|---|---|---|
| WebView2 security | Microsoft, [Develop secure WebView2 apps](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security) | Origin validation, specific messages, feature restriction, standard-user host |
| Local WebView2 content | Microsoft, [Using local content in WebView2 apps](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content) | Origin and virtual-host behavior |
| Virtual host mapping | Microsoft, [`SetVirtualHostNameToFolderMapping`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping) | Packaged HTTPS origin and cross-origin access mode |
| Navigation | Microsoft, [Navigation events for WebView2 apps](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/navigation-events) | Host cancellation and frame navigation |
| Host objects | Microsoft, [`AreHostObjectsAllowed`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2settings.arehostobjectsallowed) | Host-object denial |
| Web messaging | Microsoft, [`IsWebMessageEnabled`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2settings.iswebmessageenabled) and [`WebMessageReceivedEventArgs`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2webmessagereceivedeventargs) | Exact-origin JSON message boundary |
| Browser downloads/permissions | Microsoft, [`DownloadStarting`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.downloadstarting) and [`PermissionRequested`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.permissionrequested) | Deny browser side effects/capabilities |
| DevTools/debugging | Microsoft, [`AreDevToolsEnabled`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2settings.aredevtoolsenabled), [WebView2 browser flags](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/webview-features-flags), and [WebView2 globals/debugger variables](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/win32/webview2-idl) | Release debug restrictions and inherited flag risk |
| React HTML sink | React, [Common components: `dangerouslySetInnerHTML`](https://react.dev/reference/react-dom/components/common#dangerously-setting-the-inner-html) | Raw untrusted HTML is an XSS sink |
| CSP/Trusted Types | MDN, [Content Security Policy guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/Guides/CSP) and [Trusted Types directive](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/trusted-types) | Defense-in-depth CSP and injection-sink enforcement |
| Named-pipe gRPC | Microsoft, [.NET 10 inter-process gRPC](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess?view=aspnetcore-10.0) and [gRPC over named pipes](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess-namedpipes?view=aspnetcore-10.0) | Local generated application transport |
| Named-pipe security | Microsoft, [Named-pipe security and access rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights), [`.NET PipeOptions`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0), and [`NamedPipeTransportOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.transport.namedpipes.namedpipetransportoptions?view=aspnetcore-10.0) | DACL/current-user checks and finite buffers |
| gRPC limits | Microsoft, [.NET 10 gRPC security](https://learn.microsoft.com/en-us/aspnet/core/grpc/security?view=aspnetcore-10.0) and [configuration](https://learn.microsoft.com/en-us/aspnet/core/grpc/configuration?view=aspnetcore-10.0) | Explicit send/receive and stream limits |
| Credential Manager | Microsoft, [`CredWriteW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritew), [`CredReadW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw), and [`CredDeleteW`](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-creddeletew) | Exact-target credential helper operations |
| Path canonicalization warning | Microsoft, [`PathCchCanonicalize`](https://learn.microsoft.com/en-us/windows/win32/api/pathcch/nf-pathcch-pathcchcanonicalize) | String canonicalization is not subpath/identity authorization |
| Final path and identity | Microsoft, [`GetFinalPathNameByHandleW`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew) and [`FILE_ID_INFO`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info) | Handle-resolved target and volume/file identity |
| Handle-relative open | Microsoft, [`NtCreateFile`](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile) | Relative opens/creates under an already opened directory |
| Reparse points and links | Microsoft, [Reparse points and file operations](https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-points-and-file-operations), [Symbolic-link effects](https://learn.microsoft.com/en-us/windows/win32/fileio/symbolic-link-effects-on-file-systems-functions), and [Hard links and junctions](https://learn.microsoft.com/en-us/windows/win32/fileio/hard-links-and-junctions) | Alias/reparse/hard-link behavior |
| Direct process launch | Microsoft, [`.NET 10 ProcessStartInfo.UseShellExecute`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute?view=net-10.0), [`ArgumentList`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist?view=net-10.0), and [`CreateProcessW`](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessw) | No-shell exact executable, separate args, explicit environment/handles |
| Process containment | Microsoft, [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) and [process creation flags](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags) | Suspended assignment, kill-on-close, tree/resource control |
| Stronger worker isolation | Microsoft, [Launch an AppContainer](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer), [AppContainer isolation](https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation), and [Windows application isolation](https://learn.microsoft.com/en-us/windows/security/book/application-security-application-isolation) | Stronger OS-enforced file/network/process boundary and compatibility cost |

## 15. Semantic self-review

- ADR-0015 through ADR-0021 accept the researched WPF/WebView2,
  gRPC/named-pipe, SQLite, job, credential, and security design. This report
  does not claim implementation or conformance.
- It keeps deterministic/source/model authority distinct and gives untrusted
  content no operation authority.
- It closes the credential placement gap without exposing secret bytes to the
  coordinator's general IPC or any parser/tool worker.
- It preserves coordinator-only validation, content-store admission, and
  durable publication.
- It does not describe Job Objects or same-user worker processes as a sandbox.
- It does not imply that arbitrary exports, rich HTML, AppContainer, updates,
  or evaluations exist or pass.
- It does not authorize a write to any modding/setup root or a shell command.
- It leaves implementation pins, UI component choice, update signing, cost
  transactions, and unsupported filesystem/tool behavior to their owning
  decisions and plans.
