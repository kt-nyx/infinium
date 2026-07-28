# ADR-0017: Windows desktop application stack

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Infinium needs a modern, dense, progressively disclosed desktop UI while its
authoritative local analysis uses the .NET-native Mutagen dependency and
isolates long-running, IO-heavy, native, and crash-prone work from rendering.
The stack must support a human-readable CLI-first M1 proof, offline local work,
a replaceable presentation shell, untrusted-content isolation, and eventual
Windows desktop packaging without making the abandoned implementation
authoritative.

RESEARCH-0038 compared Electron, Avalonia, Tauri, and a direct .NET-hosted
WebView2 shell. A minimal WPF/WebView2 host preserves React's frontend
ecosystem without adding Electron's privileged Node/Chromium application layer
or Tauri's Rust core alongside the already required .NET engine.

## Decision drivers

- Mutagen and the accepted bounded Bethesda semantic work fit .NET directly.
- Finding, evidence, provenance, progress, and cost views benefit from the
  mature React/TypeScript data-interface ecosystem.
- The CLI and engine must remain usable without a graphical shell.
- Rendering, local analysis, durable state, credentials, and privileged
  operations require distinct authority boundaries.
- Windows is the accepted initial platform; cross-platform shell parity is not
  an M1–M4 requirement.
- The owner does not want the product's visual language constrained to a
  Windows Settings/Store aesthetic.
- M1 should not carry an unnecessary privileged runtime or implementation
  language.

## Considered options

### Hardened Electron plus a .NET engine

Electron provides a consistent bundled Chromium, mature React tooling, and
documented sandbox, context-isolation, CSP, IPC, and packaging controls. It is
viable, but would add a privileged Node.js application layer, a bundled browser
payload, Electron-specific preload/fuse/update work, and a second rapid
Chromium lifecycle while the .NET engine boundary remains necessary.

Electron remains the first fallback candidate if direct WebView2 proves
materially unreliable or an accepted cross-platform requirement later makes a
bundled identical browser more valuable. It is not pre-authorized: such
evidence must reopen or supersede this ADR before the stack changes.

### Avalonia-centered .NET application

Avalonia is a strong all-.NET runner-up with native accessibility support,
virtualized controls, headless testing, and no requirement to resemble a
Microsoft Store application. It reduces language/runtime count but would
require more bespoke work for Infinium's observability-style tables, graphs,
timelines, evidence views, and dense interaction patterns. No commercial
Avalonia component is assumed.

### Tauri/WebView2 plus a .NET engine

Tauri offers a strong capability model and small WebView2 shell, but adds Rust
and another privileged bridge whose main role would be relaying to the required
.NET engine. Its core benefit can be obtained with fewer layers through a
direct .NET WebView2 host.

### Direct WPF/WebView2 host plus a .NET engine

A minimal WPF host can embed React under the Evergreen WebView2 Runtime while
remaining in the same .NET ecosystem as the engine. WPF hosts the window and
security boundary rather than becoming a second XAML application. The costs
are installed-runtime detection, forward-compatibility testing, renderer
failure handling, a carefully constrained host bridge, and later packaging
decisions.

Concretely, WPF creates the native desktop window and owns WebView2 lifecycle;
WebView2 is the embedded Edge/Chromium renderer; and WebView2 loads packaged
React HTML/CSS/JavaScript. React owns the visible product. Its typed messages
cross a narrow WPF bridge to the application client, so no local web server or
browser-direct privileged API is required.

## Decision

1. The initial engine and executable-host runtime family is C# on .NET 10 LTS.
   Implementation plans and dependency manifests shall pin and qualify exact
   supported patch/runtime versions.
2. Domain, evidence, analyzer, adapter, orchestration, and application-service
   libraries remain UI-independent .NET components.
3. A human-readable CLI is the first application client and must execute and
   inspect the M1 proof without React, WPF, or WebView2.
4. React and TypeScript are the presentation stack for the Windows desktop UI.
5. The desktop shell is a minimal non-elevated C# WPF host embedding
   WebView2. It uses the Evergreen WebView2 Runtime by default and detects,
   versions, and reports missing or unsupported runtime state.
6. WPF owns only necessary window, lifecycle, renderer, and native
   security-boundary responsibilities. Product navigation, finding/case
   presentation, observability views, and visual design remain in React; WPF
   shall not become a parallel XAML product UI.
7. The renderer loads only packaged first-party application assets under a
   controlled local origin. Acquired HTML or remote content is never loaded as
   active privileged application content.
8. The React renderer has no direct filesystem, database, subprocess,
   credential, provider, MO2, LOOT, Mutagen, libloot, game, or arbitrary
   network authority. It consumes only a narrow, versioned, validated
   presentation/operation contract. Exact process topology, IPC, credential,
   and security mechanisms are separate decisions.
9. Renderer requests and results are bounded, schema-validated, cancellable
   where applicable, and use opaque identities, server-side filtering,
   aggregation, sorting, pagination, and stable cursors. The renderer shall not
   receive an entire high-end profile, evidence graph, conflict population, or
   history at startup.
10. Long-running, CPU-heavy, IO-heavy, memory-heavy, native, privileged, or
    crash-prone work executes outside the renderer and WPF UI event loop.
    WebView2's renderer processes do not count as isolation for Mutagen,
    libloot, parsing, indexing, tools, documentation acquisition, or analysis.
11. Renderer reload, WebView2 process failure, shell close, and shell restart
    cannot own or mutate authoritative run, evidence, checkpoint, cost, or
    coverage state. Reconnection queries the durable application authority.
12. The UI shell is replaceable: changing WPF/WebView2 or React must not change
    snapshot identities, evidence semantics, analyzer results, run ownership,
    finding/case truth, or CLI contracts.

## Explicit non-decisions and milestone exclusions

This ADR does not select:

- coordinator/worker process topology, local IPC, query transport, or
  presentation DTO details;
- the credential entry/storage mechanism or detailed renderer/path/subprocess
  security controls;
- a production design system or React component set;
- exact self-contained, single-file, installer, signing, update, Evergreen
  repair, offline-runtime, or Fixed Version packaging;
- an exact production model, SQLite binding, ORM, or native-helper packaging;
  or
- any cross-platform support.

M1 remains CLI-first and is not gated on a graphical shell. The first
executable WebView2 spike must satisfy the applicable security boundary before
handling real local or authenticated data. Polished graphical workflow,
resource thresholds, accessibility acceptance, and packaging belong to M2/M4
plans as applicable.

## Consequences

### Positive

- The authoritative engine uses the ecosystem required by Mutagen while the UI
  uses a mature modern web-development ecosystem.
- WPF does not determine the application's visual style; Infinium can look and
  behave like a modern analysis tool rather than a Windows Settings page.
- The shell avoids Electron's bundled Node/Chromium application layer and
  Tauri's additional Rust core.
- CLI-first delivery and shell replaceability preserve backend progress and
  testability independently of the UI.

### Negative

- The product carries C# and TypeScript toolchains plus a native WebView2
  hosting and message boundary.
- Evergreen runtime variance, updates, process failures, and missing-runtime
  repair require explicit testing and support.
- WebView2 still uses multiple browser processes and does not guarantee lower
  runtime memory than Electron; real measurements are required.
- Accessibility and security depend on application design and contract tests,
  not on framework selection alone.

### Risks and mitigations

- **The host grows into a privileged second application layer:** keep WPF
  minimal, prohibit generic bridges, and enforce replaceable,
  UI-independent application contracts.
- **Untrusted content reaches a privileged origin:** use packaged local assets,
  inert structured text, denied unexpected navigation/downloads/permissions,
  and the separately accepted desktop security boundary.
- **Evergreen updates break the application:** declare a minimum supported
  runtime, feature-detect, test forward compatibility and process-failure
  events, and provide explicit missing/outdated-runtime handling.
- **High-end result sets freeze the renderer:** require engine-side bounded
  queries, pagination/backpressure, cancellation, and renderer virtualization.
- **Framework choice is mistaken for analysis isolation:** retain independent
  engine/workers and durable state outside both WPF and WebView2.

## Requirements affected

- SCOPE-006
- AUTH-001 through AUTH-003
- SEC-001 through SEC-004
- SCAN-002 through SCAN-006
- UX-001 through UX-006
- AI-003 and AI-007
- TOOL-001 through TOOL-003
- DIST-001 through DIST-003
- OPS-001, OPS-004, and OPS-005

## Validation

Before the graphical stack is accepted for M2 implementation:

- EVAL-0026, EVAL-0033 through EVAL-0035, EVAL-0038 through EVAL-0041,
  EVAL-0044 through EVAL-0046, EVAL-0064, EVAL-0077, EVAL-0079 through
  EVAL-0083 must be specified and passed for the exercised boundary;
- a disposable spike must host packaged React assets under a controlled
  origin, issue a paginated finding query and cancellable progress operation,
  and virtualize at least 100,000 synthetic finding summaries without sending
  the full population to the renderer;
- hostile navigation, new-window, download, permission, unknown-origin,
  unknown-operation, malformed, oversized, replayed, out-of-order, arbitrary
  path, and arbitrary command attempts must fail closed;
- renderer crash/reload and shell restart during a durable job must preserve
  authoritative lifecycle and progress outside the renderer;
- the stack must exercise missing/outdated Evergreen detection, browser
  process-failure recovery, and local-only operation without network or
  credentials;
- keyboard, focus, naming, landmarks, contrast, zoom, reduced motion, screen
  reader, automated accessibility, browser, host, and WebDriver checks must
  cover the representative workflow; and
- cold/warm startup, idle/active private working set, query latency, message
  size, package size, runtime dependencies, and license inventory must be
  measured. Thresholds belong in the accepted M2 plan.

Failure of a material security, accessibility, stability, or resource
threshold reopens the choice and triggers an equivalent Avalonia comparison
before substituting another shell.

## References

- [ADR-0003 — Read-only authority](ADR-0003-read-only-authority.md)
- [ADR-0004 — Initial target scope](ADR-0004-initial-target-scope.md)
- [ADR-0006 — GPL product and tool dependency boundary](ADR-0006-gpl-product-and-tool-dependency-boundary.md)
- [ADR-0009 — Skyrim runtime and Bethesda semantic support](ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
- [RESEARCH-0038 — Desktop application stack comparison](../../research/investigations/RESEARCH-0038-desktop-application-stack-comparison.md)
- [RESEARCH-0044 — Wave E architecture and security integration](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core),
  retrieved 2026-07-28
- [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/),
  retrieved 2026-07-28
- [WebView2 overview](https://learn.microsoft.com/en-us/microsoft-edge/webview2/),
  retrieved 2026-07-28
- [WebView2 security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security),
  retrieved 2026-07-28
- [WebView2 distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution),
  retrieved 2026-07-28
- [Electron security guidance](https://www.electronjs.org/docs/latest/tutorial/security),
  retrieved 2026-07-28
- [Avalonia Windows platform guide](https://docs.avaloniaui.net/docs/platform-specific-guides/windows),
  retrieved 2026-07-28
- [Tauri process model](https://v2.tauri.app/concept/process-model/), retrieved
  2026-07-28
