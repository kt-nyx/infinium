# RESEARCH-0038: Desktop application stack comparison

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary question: RQ-016

Decision enabled: Application-stack ADR and bounded follow-up work for RQ-017,
RQ-018, RQ-030, and RQ-032

Acceptance: Recommendation accepted by the project owner through ADR-0017 on
2026-07-28

## Executive result

Infinium should use:

- a C#/.NET analysis engine expressed as UI-independent libraries and
  executable hosts;
- a human-readable CLI host as the first application client;
- a React/TypeScript presentation application for the eventual desktop UI;
  and
- a minimal C# WPF host embedding a WebView2 control backed by the Evergreen
  Microsoft Edge WebView2 Runtime for that UI.

The WPF layer should remain a window, lifecycle, and security-boundary host.
It should not become a second application UI written in XAML. The React
renderer should have no direct filesystem, database, subprocess, network
credential, or modding-tool authority. It should request narrow, validated
operations from the host/engine boundary subsequently accepted through
ADR-0018 and ADR-0019.

This is a **direct WebView2 host**, not Tauri and not Electron. It preserves the
web-frontend advantages the project wants while avoiding:

- Electron's bundled Node.js/Chromium privileged application layer and
  independent rapid Chromium release obligation;
- Tauri's additional Rust application core beside an already necessary .NET
  engine; and
- the UI-development and data-visualization burden of making Avalonia the
  complete presentation stack.

The choice does not collapse shell choice into process topology. A WebView2
renderer process is not sufficient isolation for Mutagen parsing, libloot
native calls, indexing, documentation processing, or long-running analysis.
The independently executable .NET engine/worker boundary remains necessary;
RQ-017 must select its exact process, transport, query, and lifecycle design.

The recommendation is decision-grade without pretending to establish exact
startup or memory budgets. No representative Infinium implementation exists
to benchmark, and shell microbenchmarks would not predict the cost of the
analysis engine. A bounded prototype should validate packaging, renderer
failure recovery, accessibility, list virtualization, query cancellation, and
idle/working resource use before the M2 UI plan is accepted.

## 1. Scope and decision boundary

RQ-016 asks which desktop/application stack best satisfies Infinium's UI,
security, deployment, and analysis-isolation requirements. This report
compares:

1. C#/.NET worker plus React/TypeScript in hardened Electron;
2. an Avalonia-centered .NET application;
3. C#/.NET worker plus React/TypeScript in Tauri/WebView2;
4. the recommended C#/.NET worker plus React/TypeScript in a direct
   WPF/WebView2 host; and
5. narrower rejected alternatives where they clarify the decision.

This report selects the languages and application-shell direction. It does
not select:

- the durable job/checkpoint model in RQ-015;
- the exact UI/worker transport or data-query protocol in RQ-017;
- the credential-entry/storage mechanism in RQ-018;
- M4 installer, signing, update, or distribution details in RQ-030;
- the full sanitization, navigation, subprocess, and write-authorization
  design in RQ-032; or
- exact database, schema, or persistence mechanisms in RQ-013.

Those questions must use this result as an input rather than infer their
answers from the shell technology.

## 2. Accepted constraints applied

The comparison treats the following accepted project constraints as
non-negotiable:

- Skyrim SE on Windows is the initial product target.
- MO2 is the only supported manager for MVP; MO2, the LOOT application, and
  the game are user-installed dependencies.
- Mutagen is the authoritative programmatic Bethesda-plugin integration.
- libloot is a pinned, read-only native semantic boundary where LOOT coverage
  is claimed; xEdit is not an Infinium integration.
- the application is a read-only advisor over protected setup roots;
  product-owned settings, evidence, indexes, history, and exports are separate
  authorized writes.
- deterministic local work must remain available without provider credentials
  or network access where its inputs are present.
- long-running, memory-heavy, CPU-heavy, IO-heavy, or crash-prone work must not
  run on the UI event loop.
- the UI shell must be replaceable without rewriting the domain and analysis
  engine.
- findings, cases, evidence, provenance, coverage, progress, cost, and
  readiness require dense drill-down views, but higher-level flows must remain
  approachable to experienced mod users.
- large result collections require progressive and paginated/virtualized
  access rather than loading the full history or interaction graph into the
  renderer.
- remote/source HTML, model output, logs, search results, paths, and tool
  output are untrusted data, not executable UI or operation authority.
- credentials cannot live in general renderer state, logs, traces, exports,
  or ordinary product persistence.
- first-party code is intended for GPLv3-family licensing; transitive
  distribution obligations still require artifact-level review.

Relevant accepted sources are the
[architecture overview](../../architecture/overview.md),
[data and trust model](../../architecture/data-and-trust-model.md),
[security and privacy boundary](../../architecture/security-and-privacy.md),
[integration boundaries](../../architecture/integrations.md), ADR-0003,
ADR-0004, ADR-0006 through ADR-0014, and RESEARCH-0013, RESEARCH-0024, and
RESEARCH-0033.

## 3. Research method and source currency

The comparison used current primary framework documentation and release/package
metadata retrieved on 2026-07-28. It did not use legacy Infinium code as a
design input.

### 3.1 Version observations

| Component | Current version observed | Publication observation | Source |
|---|---:|---:|---|
| .NET | `10.0.10`, LTS line | 2026-07-15 | [.NET release](https://github.com/dotnet/core/releases/tag/v10.0.10) and [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) |
| WebView2 SDK | `1.0.4078.44`, latest stable NuGet version | 2026-07-07 | [NuGet package](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4078.44) |
| Electron | `43.2.0` | 2026-07-21 | [Electron release](https://github.com/electron/electron/releases/tag/v43.2.0) |
| Avalonia | `12.1.0` | 2026-07-09 | [Avalonia release](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.0) |
| Tauri | `2.11.5` | 2026-07-01 | [Tauri release](https://github.com/tauri-apps/tauri/releases/tag/tauri-v2.11.5) |
| React | `19.2.8`, MIT | registry observation 2026-07-28 | [npm package](https://www.npmjs.com/package/react/v/19.2.8) |
| TypeScript | `7.0.2`, Apache-2.0 | registry observation 2026-07-28 | [npm package](https://www.npmjs.com/package/typescript/v/7.0.2) |
| TanStack React Virtual | `3.14.8`, MIT | registry observation 2026-07-28 | [package/docs](https://tanstack.com/virtual/latest/docs/framework/react/react-virtual) |
| Playwright | `1.62.0`, Apache-2.0 | registry observation 2026-07-28 | [Playwright docs](https://playwright.dev/docs/intro) |

These observations identify the evaluated family and current evidence date.
They are not implementation lockfiles. M1/M2 plans must pin exact versions and
record updates through dependency management.

### 3.2 Bounded metadata probe

A release-metadata probe confirmed that Electron's official
`electron-v43.2.0-win32-x64.zip` asset is 144,326,439 bytes (about 137.6 MiB)
compressed before Infinium application assets or installer overhead. This is a
useful packaging-floor observation, not a runtime-memory benchmark.

No comparative startup or memory benchmark was run. A trivial shell prototype
would measure framework samples rather than Infinium's real pagination,
evidence, process, and indexing behavior. Section 10 defines the prototype that
can generate meaningful measurements later.

## 4. Candidate architectures

### 4.1 Hardened Electron plus .NET engine

Shape:

```text
React/TypeScript renderer
        |
validated Electron IPC
        |
Electron main/utility process
        |
boundary selected by RQ-017
        |
.NET engine/workers -> Mutagen, libloot, MO2/LOOT inputs
```

Electron supplies a consistent bundled Chromium and the mature web-application
tooling expected by a React team. Its renderer sandbox, context isolation, IPC
sender validation, Content Security Policy, custom protocol, navigation
restrictions, fuses, and ASAR integrity can form a defensible desktop shell.
Electron's [security checklist](https://www.electronjs.org/docs/latest/tutorial/security),
[sandbox guidance](https://www.electronjs.org/docs/latest/tutorial/sandbox),
[context-isolation guidance](https://www.electronjs.org/docs/latest/tutorial/context-isolation),
and [ASAR integrity guidance](https://www.electronjs.org/docs/latest/tutorial/asar-integrity)
make the required controls explicit.

The concern is not that Electron cannot be secured. It is that Infinium gains
little from a privileged Node.js main process because the required engine is
already .NET. Electron would add:

- a second privileged application runtime and JavaScript package attack
  surface;
- a bundled Chromium payload;
- an independent update obligation under Electron's approximately eight-week
  major cadence and latest-three-major support policy
  ([release timelines](https://www.electronjs.org/docs/latest/tutorial/electron-timelines));
- Electron-specific preload, IPC, fuse, packaging, and signing expertise; and
- a sidecar/worker boundary that is still required for the .NET engine.

Electron's utility process can isolate Node work, but it neither replaces the
.NET worker nor establishes Infinium's job/checkpoint semantics. The official
[utility process API](https://www.electronjs.org/docs/latest/api/utility-process)
therefore does not materially simplify the core architecture.

Disposition: **viable, but not preferred**. It is the fallback if direct
WebView2 proves materially unreliable or if a future cross-platform need
requires an identical bundled browser and Electron's broader platform maturity
outweighs its burden.

### 4.2 Avalonia-centered .NET application

Shape:

```text
Avalonia UI and presentation models
        |
UI-independent .NET application contracts
        |
.NET engine/workers -> Mutagen, libloot, MO2/LOOT inputs
```

Avalonia 12.1.0 is MIT-licensed, directly aligned with .NET, and supports a
custom-styled application rather than forcing a Windows Settings/Fluent
appearance. On Windows it uses Win32 with Skia/Direct3D rendering
([Windows platform guide](https://docs.avaloniaui.net/docs/platform-specific-guides/windows)).
It has Windows UI Automation support
([accessibility guide](https://docs.avaloniaui.net/docs/app-development/accessibility)),
headless testing and Appium paths
([testing guide](https://docs.avaloniaui.net/docs/testing/)), and virtualized
collection controls such as
[ItemsRepeater](https://docs.avaloniaui.net/controls/data-display/collections/itemsrepeater).

This is the smallest language/runtime set among the serious candidates and
removes a web-renderer bridge. Its native accessibility path is attractive. A
well-designed Avalonia application can look modern, responsive, and entirely
unlike a Microsoft Store settings application.

Its cost is product-development leverage. Infinium is dominated by
filterable/virtualized findings, cases, provenance graphs, timelines,
progress/cost dashboards, evidence viewers, and interactive drill-down.
React/TypeScript has substantially broader maintained component,
visualization, state/query, accessibility-testing, and browser-debugging
choices for those patterns. Avalonia can implement them, but more behavior
would be custom application UI work. Avalonia's own
[performance guidance](https://docs.avaloniaui.net/troubleshooting/app-performance-issues)
notes limitations in the basic DataGrid path and recommends TreeDataGrid for
larger data sets; the current TreeDataGrid offering is under Avalonia Pro and
would require a separate licensing/distribution decision rather than being
assumed available to this GPL project.

Native AOT is available, but Avalonia documents reflection and dependency
constraints
([Native AOT guide](https://docs.avaloniaui.net/docs/deployment/native-aot)).
It should not be credited as a guaranteed Infinium packaging or startup win
until Mutagen, libloot binding, serialization, persistence, and UI dependencies
are proven compatible.

Disposition: **strong runner-up**. Select it if the project intentionally
prefers an all-.NET UI and accepts higher custom UI effort. Do not reject it
because of appearance; that concern is a design-system issue, not a framework
limit.

### 4.3 Tauri/WebView2 plus .NET engine

Shape:

```text
React/TypeScript renderer in WebView2
        |
Tauri command/event IPC
        |
Rust Tauri core
        |
sidecar boundary
        |
.NET engine/workers -> Mutagen, libloot, MO2/LOOT inputs
```

Tauri 2.11.5 is MIT/Apache-2.0 licensed and uses WebView2 on Windows. Its
capability/permission system, Content Security Policy, command/event IPC, and
sidecar support are deliberate security features. Relevant primary references
are its [process model](https://v2.tauri.app/concept/process-model/),
[IPC model](https://v2.tauri.app/concept/inter-process-communication/),
[capabilities](https://v2.tauri.app/security/capabilities/),
[CSP guidance](https://v2.tauri.app/security/csp/), and
[sidecar guidance](https://v2.tauri.app/develop/sidecar/).

Tauri is compelling when Rust is the application backend or when a small,
cross-platform WebView shell is the primary goal. Neither is true here:

- Mutagen makes .NET the natural authoritative engine.
- Windows is the accepted M1–M4 platform.
- Infinium would have C#, Rust, and TypeScript plus two privileged boundary
  layers.
- the Rust core would mostly relay lifecycle and capability calls to the .NET
  engine rather than own meaningful domain work.
- a direct .NET WebView2 host can apply the same Windows renderer technology
  without the extra language and sidecar orchestration layer.

Tauri's command allowlisting is useful but not self-proving: registered
commands and merged capabilities still require careful scope and validation.
It does not remove RQ-017 or RQ-032.

Disposition: **rejected for M1/M2** because its strongest benefit can be
obtained with fewer moving parts through a direct .NET WebView2 host.

### 4.4 Direct WPF/WebView2 host plus .NET engine

Shape:

```text
React/TypeScript renderer (local packaged assets only)
        |
narrow, versioned, validated host messages
        |
minimal non-elevated WPF/WebView2 host
        |
boundary selected by RQ-017
        |
UI-independent .NET engine/workers
        |
Mutagen / isolated libloot boundary / MO2 and LOOT inputs
```

WebView2 officially supports WPF and gives the host access to a Chromium-based
Edge renderer while reusing the installed Evergreen Runtime. The primary
references are the [WebView2 overview](https://learn.microsoft.com/en-us/microsoft-edge/webview2/),
[WPF integration guide](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf),
[security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security),
[distribution guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution),
and [process/event guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events).

The host should:

- run as a standard user and never elevate the UI process;
- load only packaged application assets under an application-controlled local
  origin;
- deny unexpected top-level navigation, new windows, downloads, permission
  prompts, and remote embedded content;
- use a strict Content Security Policy;
- expose no generic host-object, filesystem, process, database, or network
  proxy;
- accept only versioned message envelopes with operation-specific schemas,
  size limits, correlation IDs, and cancellation semantics;
- validate the message source and every operation again at the authoritative
  host/engine boundary;
- treat paths, HTML, Markdown, logs, source text, and model output as inert
  data;
- open an allowlisted external link through a validated host operation rather
  than navigating the embedded renderer; and
- handle renderer/browser process failure and runtime update events without
  corrupting durable jobs or authoritative state.

The application UI remains ordinary React/CSS. WPF does not determine its
visual language, navigation density, animation, charts, tables, or component
design. The host may use native surfaces only where a platform security or
window-management need justifies them; credential entry is specifically
deferred to RQ-018.

Evergreen is preferred over a Fixed Version runtime:

- Windows 11 includes the WebView2 Runtime, and many Windows 10 systems have it
  through other applications, but Infinium must detect rather than assume it;
- Evergreen receives automatic security updates;
- Infinium avoids shipping a second full browser payload; and
- runtime feature detection and a declared minimum version can handle platform
  variance.

This creates an operational obligation: Infinium must test forward
compatibility, detect missing/unsupported runtime versions, handle
`NewBrowserVersionAvailable` and process-failure events, and offer a clear
repair path. A fully offline installer may later use Microsoft's offline
runtime installer or a Fixed Version package, but RQ-030 must decide that
distribution tradeoff. RQ-016 does not select an updater.

Disposition: **recommended**.

### 4.5 Plain-language model of the selected stack

The recommendation is not “build the UI in WPF.” It is:

```text
Windows launches Infinium.exe
  -> a small WPF host creates the native window
  -> an embedded WebView2 control loads packaged HTML/CSS/JavaScript
  -> the React/TypeScript application renders the complete product UI
  -> a narrow typed message bridge asks the WPF host for allowed operations
  -> the WPF host calls the separate .NET coordinator
  -> bounded result/progress DTOs return to React for presentation
```

**WPF** is the mature .NET Windows desktop framework used here only for the
native window, application lifecycle, dialogs, WebView2 ownership, and the
trusted side of the renderer bridge. It is not the page/component framework.

**WebView2** is Microsoft's embeddable Edge/Chromium renderer. The Evergreen
Runtime is serviced on the machine independently of Infinium. It renders the
same locally packaged React build that a browser could render, while sitting
inside Infinium's native window instead of opening a web site or requiring a
local network server.

**React/TypeScript** therefore owns the visible interface: layout, styling,
navigation, tables, graphs, progressive disclosure, accessibility semantics,
and interaction. This is why the application can use web-frontend tools and
look unlike a Windows Settings application while still installing and
launching as a desktop executable with native lifecycle integration.

The trade is a real security and engineering boundary. React does not receive
filesystem, database, credential, subprocess, or arbitrary-network access.
Every allowed request crosses a versioned, schema-validated host bridge. That
bridge would also exist in Electron or Tauri under different APIs; direct
WPF/WebView2 is the smallest host layer among the compared choices because the
authoritative backend is already .NET.

## 5. Decision matrix

Ratings are comparative judgments under Infinium's accepted Windows-only,
.NET-engine, modern-web-UI constraints. “Strong” does not mean an evaluation
case has passed.

| Criterion | Electron + .NET | Avalonia + .NET | Tauri + .NET | Direct WPF/WebView2 + .NET |
|---|---|---|---|---|
| Mutagen/libloot/MO2 fit | Strong, through .NET sidecar | Excellent, one ecosystem | Strong, through .NET sidecar | Excellent, native .NET orchestration |
| Modern dense UI leverage | Excellent | Good; more custom work | Excellent | Excellent |
| Avoids forced Windows-settings appearance | Yes | Yes | Yes | Yes |
| Analysis isolation | Requires RQ-017 worker | Requires RQ-017 worker | Requires RQ-017 worker | Requires RQ-017 worker |
| Privileged shell surface | Largest; Node main/preload plus bridge | Smallest; no web bridge | Rust core plus bridge/capabilities | Small .NET host plus bridge |
| Language/tooling burden | C#, TypeScript/JS, Electron | C#/XAML | C#, Rust, TypeScript/JS | C#, TypeScript/JS, WebView2 |
| Browser consistency | Highest; bundled | Not applicable | Depends on installed WebView2 | Depends on installed WebView2 |
| Browser security-update ownership | Application ships Electron updates | Framework/app updates only | Shared Evergreen runtime plus Tauri updates | Shared Evergreen runtime plus SDK/host updates |
| Base package pressure | Highest; observed 137.6 MiB compressed Electron runtime asset | Likely low/moderate; measure with app | Low with installed runtime; higher offline | Low with installed runtime; higher offline |
| UI-shell replaceability | Good if engine boundary enforced | Good if view models do not become domain | Good if core stays relay-only | Excellent if host and renderer remain clients |
| Progressive/paginated views | Mature web ecosystem | Supported, more bespoke | Mature web ecosystem | Mature web ecosystem |
| Accessibility | Chromium plus app semantics; Electron APIs | Native UIA path | Chromium/WebView2 plus app semantics | Chromium/WebView2 plus app semantics |
| Automated UI testing | Browser tests; Electron Playwright support is experimental | Headless/Appium | Browser tests plus WebDriver path | Browser tests plus WebView2 WebDriver/host tests |
| Offline local analysis | Yes | Yes | Yes | Yes |
| Cross-platform option value | High | High | High | Low, intentionally aligned with scope |
| Relative maintenance risk | Browser/runtime cadence and two privileged layers | Custom UI breadth | Three languages and two backend layers | Runtime compatibility plus narrow bridge |
| Overall fit | Viable fallback | Strong runner-up | Unnecessary indirection | **Best fit** |

## 6. Required architecture boundaries under the recommendation

### 6.1 Engine and shell replaceability

The solution should have separate buildable units conceptually equivalent to:

- domain/evidence/contracts libraries with no UI dependency;
- deterministic analyzer and source/tool adapter libraries;
- an engine/application service layer;
- one or more executable worker/CLI hosts;
- a versioned presentation/query contract;
- a minimal WPF/WebView2 desktop host; and
- a React/TypeScript renderer consuming only the presentation contract.

The CLI must be able to execute and inspect the M1 proof without WebView2 or
React. Replacing the desktop shell must not change snapshot identities,
evidence semantics, analyzer outputs, job ownership, or case/finding truth.

### 6.2 Tool and crash isolation

Mutagen belongs in the .NET engine. libloot's selected native binding belongs
behind an engine adapter and, if RQ-017/RQ-032 determine it necessary, a
crash-contained subprocess. MO2 reconstruction, LOOT managed-data handling,
archive/asset parsing, generated-output inspection, and future native helpers
are engine concerns.

Neither React nor the WPF host may load libloot, parse a full load order,
enumerate the MO2 VFS, or own durable analysis transactions. Renderer/browser
multi-process isolation protects UI rendering; it does not provide semantic
or crash isolation for those workloads.

### 6.3 Data access and responsiveness

The renderer should receive:

- bounded summaries;
- stable opaque IDs and cursors;
- server/engine-side filtering, sorting, aggregation, and pagination;
- virtualized row/detail presentation;
- explicit loading, partial, stale, gap, unavailable, and failed states; and
- progress snapshots/events that can be reconciled against authoritative job
  state.

It should not receive an entire high-end profile graph, all record conflicts,
all evidence bodies, or the full historical store at startup. React
virtualization reduces DOM cost but does not replace engine-side query
bounding. RQ-017 must select the exact query and event transport.

### 6.4 Credentials and privileged operations

Stack selection establishes only these invariants:

- credentials are never durable React state, browser storage, URL/query data,
  ordinary settings, IPC logs, or exported state;
- the renderer can request a dedicated credential flow but cannot read back a
  stored secret;
- the non-elevated shell exposes only allowlisted operations;
- the engine revalidates every operation and path against the accepted
  authority model; and
- no generic command execution, arbitrary path read/write, arbitrary URL fetch,
  or database query bridge exists.

RQ-018 must select the OS-backed secret mechanism and entry flow. RQ-032 must
select the detailed allowlists, sanitization, subprocess, navigation, and
write-destination controls.

### 6.5 Untrusted and external content

The renderer may present structured text derived from source material, but it
must not render acquired source HTML in a privileged origin. If formatted text
is later supported, a reviewed allowlist sanitizer and inert rendering
contract are required. Model-generated prose is data under the same rule.

External documentation pages should open outside the embedded application
after scheme/target validation. Search snippets are discovery evidence, not
trusted UI. WebView2 remote debugging must be disabled in release builds and
must never expose production credentials or privileged host methods.

### 6.6 Failure and coverage-gap behavior

The shell presents authoritative state; it does not manufacture it. Closing,
reloading, or crashing the renderer cannot convert a running job to completed,
lose a terminal failure, reuse a checkpoint, spend reserved budget, or change
coverage. On reconnect, the UI must query and reconcile durable engine state.

A missing WebView2 Runtime may block the graphical shell but must not make a
CLI-capable deterministic engine unavailable. Unsupported or failed analyzers,
missing tools, unavailable sources, and partial queries remain typed coverage
gaps or failures in the domain/query contract. A renderer error, absent UI
component, or shortened page is never evidence that analysis coverage is
complete.

## 7. Packaging, licensing, and deployment implications

### 7.1 Packaging direction

The recommended stack can publish the .NET host/worker self-contained for the
selected Windows architecture, with the React assets packaged as application
resources. [.NET single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
is available, but Infinium should not promise one physical executable:
WebView2 loader/runtime requirements, libloot native artifacts, licenses,
symbol/source offers, CLI hosts, and supportability may make a structured
installation more honest and maintainable.

WebView2 Evergreen presence/version detection and repair instructions are
release requirements. An offline runtime payload and Fixed Version deployment
remain RQ-030 options, not defaults selected here.

### 7.2 Licensing direction

At the framework level:

- WPF is MIT-licensed;
- React is MIT-licensed;
- TypeScript is Apache-2.0-licensed;
- Electron is MIT-licensed;
- Avalonia is MIT-licensed; and
- Tauri is MIT/Apache-2.0-licensed.

These licenses are compatible in principle with a GPLv3-family first-party
application. The WebView2 SDK and Runtime are Microsoft redistributables under
their own terms rather than open-source first-party code. This report does not
replace the accepted artifact-level distribution process in ADR-0006:
the exact SDK/runtime mode, native binaries, notices, source/offer obligations,
and installer contents must pass DIST-002/DIST-003 review before release.
No commercial Avalonia component is assumed by the recommendation.

## 8. Accessibility and testing implications

The React renderer must meet keyboard, focus, naming, landmark, contrast,
zoom, reduced-motion, and screen-reader expectations in normal browser tests.
WebView2 uses the Edge accessibility tree, but framework accessibility does not
make an application accessible automatically.

The testing pyramid should include:

1. domain/analyzer contract tests without a UI;
2. presentation-query and message-schema tests without WebView2;
3. React component, accessibility, virtualization, and browser tests;
4. WPF host tests for navigation denial, message validation, lifecycle, and
   failure handling;
5. WebView2 WebDriver smoke tests
   ([WebDriver guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webdriver));
6. adversarial EVAL-0033/EVAL-0034/EVAL-0080/EVAL-0082 boundary tests; and
7. a small end-to-end run showing that closing/restarting the shell does not
   corrupt or grant ownership over the engine job.

The React application should also run in an ordinary browser test harness with
mock contract adapters. That improves UI iteration without making mocked data
production authority.

## 9. Resource and operational assessment

### 9.1 What can be concluded now

- Electron has the largest confirmed distribution floor among the compared
  web-shell choices because it ships Chromium and Node.
- direct WebView2 and Tauri can reuse the Evergreen Runtime already maintained
  on supported Windows systems.
- WebView2 still runs multiple browser processes; a small installer does not
  imply negligible runtime memory.
- Avalonia avoids a browser runtime and is the credible candidate for the
  lowest shell memory, but exact benefit depends on the real Infinium views.
- the analysis engine, indexes, parsers, retained evidence, and source bodies
  are likely to dominate working and disk cost during exhaustive scans.
- every candidate needs bounded queries, backpressure, cancellation, and
  worker isolation. Choosing a lighter shell does not solve high-end analysis
  scale.

### 9.2 What remains uncertain

- cold/warm startup time on supported Windows versions;
- idle private working set and GPU-process cost;
- memory while virtualizing representative finding/evidence lists;
- runtime compatibility after Evergreen updates;
- behavior when the browser process crashes during active jobs;
- packaging size for the exact .NET deployment and native dependencies;
- accessibility quality of the actual component/design-system choices; and
- whether any chosen advanced React component introduces an incompatible
  license or excessive bundle/runtime burden.

These are prototype/evaluation questions, not reasons to invent precision now.

## 10. Required bounded prototype before M2 acceptance

Build a disposable architecture spike after the application-stack ADR is
accepted and after RQ-017 supplies a provisional boundary. It should not become
production code by default.

The spike should:

1. host locally packaged React assets in WPF/WebView2 under a controlled
   origin;
2. expose two read-only versioned operations: a paginated finding query and a
   cancellable progress subscription/poll;
3. virtualize at least 100,000 synthetic finding-summary rows without sending
   them all to the renderer;
4. open a case detail containing large inert source/model text without
   privileged HTML rendering;
5. deny unexpected navigation, new windows, downloads, unknown messages,
   oversized payloads, arbitrary paths, and arbitrary commands;
6. demonstrate renderer crash/reload and shell restart while authoritative job
   state remains outside the renderer;
7. run without network/provider credentials for a local-only flow;
8. exercise missing/outdated WebView2 Runtime detection and browser-process
   failure handling;
9. run keyboard, zoom, screen-reader smoke, automated accessibility, and
   WebDriver checks; and
10. record cold/warm startup, idle/active private working set, query latency,
    payload size, installer size, and dependency/license inventory.

Acceptance thresholds belong in the M2 plan after baseline measurement.
Comparing an equivalent Avalonia proof is warranted only if the WebView2 spike
fails a material security, accessibility, stability, or resource threshold;
it is not required merely to manufacture benchmark symmetry.

## 11. Rejected or deferred variants

### 11.1 WinUI 3 as the complete UI

Rejected for M1/M2. It offers Windows-native integration but does not improve
the Mutagen engine, analysis isolation, or dense data-tooling problem, and it
increases dependence on Windows App SDK/packaging details. Its default design
language also runs against the owner's stated aesthetic preference. This is
not a claim that WinUI applications are inherently slow.

### 11.2 WPF as the complete UI

Rejected as the primary presentation stack. WPF remains a mature, supported
host technology, but building all observability-style UI in XAML gives up the
web ecosystem without gaining Avalonia's cross-platform option or materially
improving engine integration over the minimal host.

### 11.3 Blazor Hybrid/WebView

Deferred. It could reduce TypeScript use and preserve a web-shaped UI, but the
project would still carry a WebView boundary while giving up the React
ecosystem selected for dense data interaction. No accepted requirement makes
shared Razor/web server code valuable.

### 11.4 Local web server plus default browser

Rejected as the primary desktop experience. It simplifies renderer hosting but
complicates lifecycle, local endpoint authentication, origin/port management,
browser-profile variability, external navigation, and native setup/tool
discovery. It remains useful as a test harness or future remote/read-only
viewer only after a separate security decision.

### 11.5 Monolithic in-process UI and analysis

Rejected for every shell. It conflicts with long-running/crash-prone isolation,
restartable UI, cancellation, checkpoint, and high-end scale requirements.

## 12. ADR and follow-up implications

### 12.1 Accepted application-stack disposition

Create one ADR accepting:

- .NET 10 LTS as the initial engine/host runtime family;
- UI-independent .NET domain/analysis/application libraries;
- an independently usable CLI/worker host;
- React/TypeScript as the desktop presentation application;
- a minimal non-elevated WPF/WebView2 Evergreen shell;
- local packaged renderer assets, no remote privileged content;
- a narrow versioned validated host contract with no generic privileged bridge;
- UI-shell replaceability; and
- the requirement that exact versions be pinned by implementation plans and
  dependency manifests.

The ADR should record Electron, Avalonia, Tauri, WinUI, full-WPF, Blazor
Hybrid, and local-browser alternatives with the dispositions above.

### 12.2 Decisions that remain separate

Do not smuggle these into the stack ADR:

- RQ-017: worker topology, IPC/query transport, lifecycle, backpressure, and
  ownership;
- RQ-018: credential entry, OS-backed storage, deletion, and renewal;
- RQ-032: exact content sanitization, navigation, subprocess, protected-root,
  write-destination, and export-redaction controls;
- RQ-030: installer, code signing, update channel, offline WebView2 mode, and
  distribution;
- RQ-013/RQ-015: storage, schema evolution, job, checkpoint, and recovery
  mechanisms.

Each durable choice requires its own ADR if it meets the repository's ADR
criteria. Local React libraries, WPF window chrome, state-management details,
and visual component selections can remain milestone-plan decisions unless
they create a durable security, licensing, or data-contract dependency.

## 13. Evaluation implications

Existing cases that materially constrain or validate the selected stack:

| Evaluation | Stack implication |
|---|---|
| EVAL-0026 | Renderer or shell edits/restarts cannot mutate the immutable snapshot, context, or configuration already bound to a run. |
| EVAL-0033 | HTML/text/model/search/tool content cannot grant host, source, local-state, or operation authority. |
| EVAL-0034 | Secrets and unnecessary local context cannot leak through renderer messages, logs, traces, or exports. |
| EVAL-0035 | Out-of-scope paths, URLs, commands, tool arguments, and privileged operations are rejected at the host/engine boundary. |
| EVAL-0038 | Pause/resume/cancellation, terminality, child control, and restart state remain durable outside renderer state. |
| EVAL-0039 | Acquisition ownership and profile-application links remain independently queryable without UI re-ownership. |
| EVAL-0040 | Run-owned output and later exports preserve exact identity, configuration, omissions, and sharing policy outside renderer state. |
| EVAL-0041 | Retention/deletion preview and confirmation remain durable application operations, never renderer-side deletion. |
| EVAL-0044 | Cost ownership and attached/detached rollups remain correct outside renderer state. |
| EVAL-0045 | Startup, reconnect, profile changes, and UI events never initiate analysis or paid/network work implicitly. |
| EVAL-0046 | The host/worker boundary exposes only qualified non-mutating external-tool operations and records their allowed effects. |
| EVAL-0064 | Local-only flows launch and run without network or provider credentials. |
| EVAL-0077 | The renderer cannot cause billable work without current authenticated user authorization and budget authority. |
| EVAL-0079 | Finding/case continuity belongs to durable domain projections, not UI row identity. |
| EVAL-0080 | Every host/engine write is bounded to approved destinations; renderer-provided aliases or paths grant no authority. |
| EVAL-0081 | Atomic reservation, dispatch, cancellation, and reconciliation authority remains coordinator-owned and cannot be duplicated or bypassed by client failure. |
| EVAL-0082 | CLI/config controls remain independently effective and retained; renderer defaults or presentation filtering cannot replace them. |
| EVAL-0083 | End-to-end provenance remains queryable through the UI without putting provider/source objects into domain truth. |

The first executable WebView2 shell or disposable shell spike in any milestone
must pass the applicable EVAL-0033/EVAL-0035 origin, navigation, message, and
privileged-boundary checks plus credential non-observability before it handles
real local or authenticated data. Deferring the polished UI to M2 does not
defer this first-use security gate.

The M2 plan should extend that boundary coverage and add the usability,
accessibility, virtualization, and polished-shell cases for:

- renderer navigation/new-window/download denial;
- unknown-origin, unknown-operation, malformed, oversized, replayed, and
  out-of-order message rejection;
- shell and renderer crash/restart while a worker job exists;
- Evergreen runtime missing, upgrade, and process-failure behavior;
- pagination/virtualization at representative high-end result counts;
- WebView accessibility/keyboard/zoom behavior; and
- credential-flow non-observability from renderer storage and ordinary IPC.

## 14. Recommendation and RQ disposition

Recommend that the owner:

1. accept the direct WPF/WebView2 + React/TypeScript + independent .NET
   engine/CLI/worker direction;
2. authorize an application-stack ADR using the boundary in section 12.1;
3. mark RQ-016 **resolved for M0** after ADR-0017 acceptance;
4. use the chosen shell as input to RQ-017, RQ-018, RQ-030, and RQ-032; and
5. require the section 10 prototype before accepting the M2 UI implementation
   plan, without blocking the CLI-first M1 proof on polished UI work.

ADR-0017 resolves RQ-016 for M0. Acceptance does not claim that the WebView2
prototype, security boundary, packaging, or UI evaluation cases have passed.

## 15. Source register

Primary sources retrieved 2026-07-28:

- Microsoft, [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
  and [.NET 10.0.10 release](https://github.com/dotnet/core/releases/tag/v10.0.10).
- Microsoft, [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
  and [open-source WPF repository](https://github.com/dotnet/wpf).
- Microsoft, [WebView2 overview](https://learn.microsoft.com/en-us/microsoft-edge/webview2/),
  [WPF guide](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf),
  [security](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security),
  [distribution](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution),
  [process-related events](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events),
  and [WebDriver testing](https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/webdriver).
- Microsoft, [WebView2 SDK 1.0.4078.44](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4078.44)
  and [.NET single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview).
- Electron, [43.2.0 release](https://github.com/electron/electron/releases/tag/v43.2.0),
  [Electron 43 release notes](https://www.electronjs.org/blog/electron-43-0),
  [security](https://www.electronjs.org/docs/latest/tutorial/security),
  [sandbox](https://www.electronjs.org/docs/latest/tutorial/sandbox),
  [context isolation](https://www.electronjs.org/docs/latest/tutorial/context-isolation),
  [release timelines](https://www.electronjs.org/docs/latest/tutorial/electron-timelines),
  [utility processes](https://www.electronjs.org/docs/latest/api/utility-process),
  [ASAR integrity](https://www.electronjs.org/docs/latest/tutorial/asar-integrity),
  and [accessibility](https://www.electronjs.org/docs/latest/tutorial/accessibility).
- Avalonia, [12.1.0 release](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.1.0),
  [repository/license](https://github.com/AvaloniaUI/Avalonia),
  [Windows platform](https://docs.avaloniaui.net/docs/platform-specific-guides/windows),
  [accessibility](https://docs.avaloniaui.net/docs/app-development/accessibility),
  [testing](https://docs.avaloniaui.net/docs/testing/),
  [ItemsRepeater](https://docs.avaloniaui.net/controls/data-display/collections/itemsrepeater),
  [performance](https://docs.avaloniaui.net/troubleshooting/app-performance-issues),
  and [Native AOT](https://docs.avaloniaui.net/docs/deployment/native-aot).
- Tauri, [2.11.5 release](https://github.com/tauri-apps/tauri/releases/tag/tauri-v2.11.5),
  [repository/license](https://github.com/tauri-apps/tauri),
  [process model](https://v2.tauri.app/concept/process-model/),
  [IPC](https://v2.tauri.app/concept/inter-process-communication/),
  [capabilities](https://v2.tauri.app/security/capabilities/),
  [CSP](https://v2.tauri.app/security/csp/),
  [sidecars](https://v2.tauri.app/develop/sidecar/),
  [Windows installers](https://v2.tauri.app/distribute/windows-installer/),
  and [WebDriver testing](https://v2.tauri.app/develop/tests/webdriver/).
- React, [React package 19.2.8](https://www.npmjs.com/package/react/v/19.2.8);
  Microsoft, [TypeScript package 7.0.2](https://www.npmjs.com/package/typescript/v/7.0.2);
  TanStack, [React Virtual](https://tanstack.com/virtual/latest/docs/framework/react/react-virtual);
  and Microsoft, [Playwright](https://playwright.dev/docs/intro).

## 16. Limitations

- This is architecture research, not legal advice. Framework-level license
  compatibility does not replace exact binary/dependency review.
- Version and moving-document observations are current as of 2026-07-28.
- No production Infinium shell, worker, query, credential, installer, or
  security prototype was executed.
- Qualitative resource rankings must be replaced by measurements from the
  bounded prototype and representative engine workloads.
- WebView2 availability and update behavior are Windows dependencies, not
  guarantees supplied by the React application.
- The recommendation assumes Windows-only M1–M4 scope. A future cross-platform
  product would reopen the shell decision without requiring a rewrite of the
  engine if the accepted UI-independent engine boundary is preserved.
