# ADR-0021: Desktop and local-operation security boundary

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Infinium must present untrusted documentation and analytical output, parse
untrusted local artifacts, invoke narrowly approved external operations, and
write its own durable and temporary data without granting those inputs access
to the user's modding setup, credentials, or application authority.

The desktop, coordinator, IPC, and credential decisions accepted by ADR-0017
through ADR-0020 establish where responsibilities live. They do not by
themselves define the renderer,
filesystem, process-launch, worker-publication, diagnostic, or export controls
needed to satisfy AUTH-001 through AUTH-003 and SEC-001 through SEC-004.
WebView2, same-user processes, IPC, path normalization, and Windows Job Objects
are mechanisms, not security boundaries merely by existing.

## Decision drivers

- Untrusted source, model, tool, log, renderer, IPC, and local-file content
  must remain data and must never grant authority.
- Product writes must be provably confined to their approved write class and
  must not reach protected setup roots through path aliases or races.
- External tools and workers must receive only an exact, typed operation rather
  than shell, path, environment, or publication authority.
- A responsive web-based desktop UI must not become a privileged browser.
- Credentials must be absent from ordinary renderer, worker, persistence,
  diagnostic, and export channels.
- A managed provider process must not inherit generic agent, shell, filesystem,
  plugin, MCP, browser-control, or network-listener authority merely because
  its upstream software supports those capabilities.
- M1 needs a bounded security substrate without claiming that an ordinary
  same-user worker is sandboxed.
- Later sharing and stronger isolation must be possible without weakening or
  rewriting the initial authority model.

## Considered options

### Render acquired HTML or permit remote navigation in the application WebView

Rejected. Active untrusted content would become adjacent to the native bridge,
and remote browser capabilities would add unnecessary navigation, resource,
download, permission, and origin authority.

### Expose generic host objects, native methods, paths, SQL, commands, or URLs

Rejected. A generic bridge turns content or renderer compromise into native
method discovery and makes authorization depend on callers supplying
privileged primitives.

### Authorize files by normalized string prefix and launch tools through a shell

Rejected. String normalization does not prove final Windows object identity in
the presence of reparses, hard links, aliases, or check/use races. Shell launch
and caller-composed command strings add search, quoting, environment, and
confused-deputy risks.

### Use typed operations, handle-bound writes, isolated staging, and direct
process launch

Selected. This makes authority explicit, independently testable, and narrow
enough for the bounded M1 surface.

### Describe Job Object workers as sandboxes

Rejected. Job Objects provide lifetime, process-tree, handle, and resource
containment, but do not remove the interactive user's ambient filesystem or
network rights from a compromised worker. AppContainer or another stronger
boundary remains conditional on a separate compatibility prototype and
decision.

## Decision

1. Infinium shall use a layered, deny-by-default local security boundary. The
   coordinator remains the authorization and authoritative-publication
   boundary selected by ADR-0018 and ADR-0019. This ADR does not give the
   renderer, clients, workers, stored values, or untrusted inputs independent
   authority.
2. The WPF/WebView2 shell selected by ADR-0017 shall load only versioned
   packaged application assets from one non-resolving application HTTPS
   origin. It shall render acquired, model, tool, log, and diagnostic content
   as inert structured text or a typed Markdown representation without raw
   HTML. This ADR governs that shell's security controls, not the stack
   selection.
3. The application origin shall use a restrictive Content Security Policy and
   Trusted Types where supported. Host objects, remote application/source
   navigation, remote frames/resources, downloads, permissions, new windows,
   and unnecessary browser features shall be disabled or denied. A validated
   external HTTPS link may open through a typed host operation outside the
   application WebView; content cannot create that authority itself.
4. The WebView bridge shall accept only exact-origin, versioned, closed-schema
   messages with finite size, sequence, operation, and user-gesture rules.
   It shall expose no generic native proxy, path, SQL, command, URL, credential
   target, or provider-operation primitive. The coordinator IPC contract
   remains owned by ADR-0019 rather than being redefined here.
5. Release builds shall disable DevTools and remote debugging and shall fail
   closed if inherited flags or configuration would re-enable privileged
   debugging. A development-debug renderer shall not be able to exercise
   credentials, billable/provider work, external tools, or protected evidence
   through the bridge.
6. Every write shall use a fixed write class. Product data, cache/content,
   attempt staging, diagnostics/run-owned output, and later export classes
   shall each have distinct authority. Callers shall supply durable object
   identities or typed relative artifact names, never arbitrary destination
   paths or generic write/delete/copy operations.
7. The write-authority module shall maintain an immutable protected-root
   registry and shall authorize Windows objects by opened-handle identity,
   final resolved path, volume/file identity, and handle-relative descendant
   operations. It shall reject reparse traversal, unexpected hard links,
   device and alternate-data-stream syntax, cross-volume ambiguity, stale
   capability use, and final targets or ancestors that enter protected roots.
   User-facing deletion shall operate from a coordinator-generated,
   durable-ID-based plan and shall not accept a recursive caller path.
8. Every subprocess shall use an exact absolute executable selected by a typed
   adapter and direct no-shell launch with a closed argument schema, constructed
   environment, explicit working directory, explicit inherited handles, and
   bounded input/output/time/resource behavior. The launch shall assign the
   process to its Job Object before untrusted execution where the exact
   supported operation permits it.
9. Job Objects are accepted only as process-lifetime, tree, handle, and
   resource containment. Infinium shall not call these workers sandboxed or
   claim that they prevent a compromised same-user process from using ambient
   user rights. An operation whose threat model requires that stronger
   containment remains unsupported until an AppContainer/LPAC or other
   mechanism passes a bounded compatibility prototype, receives its own
   reviewed decision, and passes operation-specific evaluation.
10. A worker may write only within its coordinator-created per-attempt staging
    root. It cannot publish authoritative state. The coordinator shall
    independently validate the worker manifest, schema, identities, sizes,
    hashes, and bytes, admit accepted payloads to the content-addressed store,
    and transactionally publish authoritative references under the current
    fence.
11. Diagnostics shall be structured and field-allowlisted. Credentials shall
    be excluded by construction and tested with canaries across errors, traces,
    stdout, run-owned JSON, persistence, staging, and later exports. The
    credential lifecycle, exact-target storage, revocation, and provider
    dispatch remain owned by ADR-0020; this ADR governs only the local
    operation and diagnostic boundary around them.
12. Retained/output objects shall use four explicit sharing classes:
    `InternalPrivate`, `PrivateDiagnostic`, `LocalPrivateExport`, and
    `ExternallyShareable`. Changing class creates a new provenance-bearing
    export artifact; it never relabels or mutates the source. M1 shall emit
    only human-readable local output and versioned run-owned JSON in fixed
    product-controlled storage, labeled as potentially sensitive.
    User-selected destinations, `LocalPrivateExport`, and
    `ExternallyShareable` generation are deferred beyond bounded M1 and later
    require inspectable selection, redaction, source-policy review, and the
    same handle-bound destination authorization.
13. Only an accepted, typed source/provider adapter may issue a network
    request. WebView2 and general parser/tool workers receive no generic
    network operation. The credential helper may reach only the exact
    provider/endpoint authorized under ADR-0020; this ADR does not redefine
    credential or dispatch lifecycle.
14. All Infinium executables shall run non-elevated through M4. This decision
    adds no Windows service, administrator broker, remote client, TCP control
    listener, browser-direct coordinator connection, or product-initiated
    setup mutation.

## M1 boundary

M1 must qualify the shared authorization vocabulary; fixed roots and protected
roots; the handle-bound Windows write/deletion module; typed direct process
launch and Job Object containment; one isolated staged worker with
coordinator-only publication; structured diagnostics and secret-canary checks;
and run-owned local/JSON output. The exact renderer controls are mandatory when
the WPF/WebView2 shell or its M1 spike is exercised.

Authenticated provider work also requires the separately governed ADR-0020
credential helper and lifecycle before the first reusable-secret dispatch.
M1 may defer rich sanitized HTML, arbitrary export destinations and sharing
bundles, AppContainer/LPAC, OS-level network denial for general workers,
installer/update staging, remote/elevated operation, and polished graphical
security UX. Deferral is not permission to weaken the controls on an exercised
surface.

## Consequences

### Positive

- Untrusted content and renderer state remain separated from native authority.
- File and process operations become typed, reviewable, and adversarially
  testable.
- Worker failure or malformed output cannot directly publish authoritative
  evidence.
- The design supports a modern web UI without treating the UI as trusted
  analytical or local-operation authority.
- Diagnostic privacy and eventual export sharing have explicit, non-mutating
  classifications.

### Negative

- Windows handle-relative authorization and suspended process setup require
  low-level implementation and platform-specific tests.
- Strict CSP, Trusted Types, and inert rendering constrain UI component choices.
- Some user-installed tools may be ineligible if they require uncontrolled
  environment, child-process, temporary-write, or Job Object behavior.
- M1 does not provide arbitrary exports or strong hostile-code sandboxing.

### Risks and mitigations

- **Path race or alias bypass:** use opened-handle identity and relative
  operations, then test direct, junction, symlink, mount, hard-link, short-name,
  case, device, ADS, replacement, and check/use adversaries.
- **Renderer bridge expansion:** require closed schemas and review every new
  operation as authority, with hostile-content cases under EVAL-0033 and
  EVAL-0035.
- **Native/parser compromise:** isolate execution and stage output, disclose
  the same-user limit, and exclude an operation when its threat model requires
  stronger containment.
- **Secret leakage through SDKs or diagnostics:** keep secret bytes in the
  dedicated helper, inspect exact provider bindings, and run EVAL-0034 and
  EVAL-0089 canary/race coverage before authenticated use.
- **False confidence from process separation:** test launch, inherited handles,
  process trees, publication fences, and crash behavior under EVAL-0088, while
  retaining the explicit non-sandbox statement.

## Requirements affected

- AUTH-001 through AUTH-003
- SEC-001 through SEC-004
- AI-003 and AI-007
- SCAN-004 and SCAN-005
- TOOL-001 through TOOL-003
- OPS-002 through OPS-004

## Validation

No evaluation is passed by accepting this ADR.

- EVAL-0033 and EVAL-0035 must prove that hostile content, renderer/IPC
  messages, and arbitrary privileged primitives fail closed.
- EVAL-0034 and EVAL-0089 must prove credential exclusion and the separately
  governed credential lifecycle across success, failure, cancellation, crash,
  and dispatch races.
- EVAL-0040 must preserve the distinction between sensitive run-owned output
  and separately created later exports.
- EVAL-0046 must prove each exact external-tool operation is non-mutating and
  leaves protected setup roots unchanged.
- EVAL-0080 must exercise every product write class against supported Windows
  path, alias, reparse, hard-link, deletion, and race adversaries.
- EVAL-0088 must exercise internal role boundaries, worker/helper launch,
  inherited handles, Job Objects, staging, coordinator-only publication,
  malformed input, slow clients, and crashes.

The decision must be revisited if the desktop/process topology changes, the
handle-relative prototype cannot meet the supported Windows/filesystem
contract, an M1 operation requires stronger compromise containment, WebView2
cannot enforce the required origin/debug controls, or product scope adds
elevation, remote clients, services, setup writes, or shared/community
exports.

## References

- [Product requirements](../../product/requirements.md)
- [Security and privacy](../security-and-privacy.md)
- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0003](ADR-0003-read-only-authority.md)
- [ADR-0017](ADR-0017-windows-desktop-application-stack.md)
- [ADR-0018](ADR-0018-process-and-authority-topology.md)
- [ADR-0019](ADR-0019-local-ipc-and-application-query-contract.md)
- [ADR-0020](ADR-0020-credential-storage-and-provider-dispatch.md)
- [RESEARCH-0038](../../research/investigations/RESEARCH-0038-desktop-application-stack-comparison.md)
- [RESEARCH-0039](../../research/investigations/RESEARCH-0039-process-and-data-query-boundary.md)
- [RESEARCH-0040](../../research/investigations/RESEARCH-0040-credential-entry-and-storage.md)
- [RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
- [RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
