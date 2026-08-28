# Frontend toolchain and generated-output ownership

Status: Completed

Last reviewed: 2026-08-28

## Plain-language purpose

The frontend contract can be rebuilt and checked without using whichever Node
installation happens to be on a developer's `PATH`. The repository restores an
exact Node executable and an exact TypeScript compiler through locked NuGet
packages, then calls those files by repository-relative path. After the first
ordinary restore primes `.packages/`, the complete frontend check works with
all package sources disabled.

## Pinned tools and licenses

| Tool | Locked identity | License | Use |
|---|---|---|---|
| Node.js Windows redistribution | `Node.js.redist.win/24.14.1` | package `LICENSE`; Node.js MIT license plus the notices for bundled third-party components | Executes the first-party generator, lint, compiler, and unit tests |
| TypeScript compiler | `Microsoft.TypeScript.MSBuild/5.9.3` | Apache-2.0 | Strict compilation and test emission |
| WebView2 SDK | `Microsoft.Web.WebView2/1.0.4129.50` | BSD-3-Clause with bundled notices | WPF host and protected renderer controller |
| React / ReactDOM assets | `18.3.1` | MIT | Packaged diagnostic renderer; exact source-tarball and packaged-file SHA-256 values are recorded in the dependency manifest |

The two identities and package SHA-512 values are locked in
`eng/tooling/Infinium.FrontendToolchain/packages.lock.json`. The desktop
projects also have exact NuGet locks. There is no npm dependency graph, npm
install script, global Node dependency, or live package download in the final
build. The Evergreen WebView2 runtime is an explicit local prerequisite; the
host never downloads it and reports missing/outdated state inertly.

## Generated ownership

The sole reviewed generation source is
`contracts/json-schema/renderer-envelope.v1.schema.json`. Its closed
`x-infinium-registry` metadata owns operation/native-target bindings alongside
the payload field schemas, so operation and field parity cannot drift through
separate handwritten templates. The only generator is
`eng/generate-renderer-contracts.mjs`. It owns these checked-in outputs:

- `contracts/renderer/renderer-operation-registry.v1.json`;
- `src/Infinium.Frontend/generated/renderer-contract.generated.ts`;
- `src/Infinium.Application/Runtime/RendererOperationCatalog.Generated.cs`.

`eng/generate-desktop-assets.mjs` separately compiles the first-party renderer,
packages the two reviewed React assets, writes the exact runtime asset manifest,
and generates `DesktopAssetCatalog.Generated.cs` with the manifest SHA-256.
`-Task CheckDesktop` regenerates the prospective bytes and fails on any asset,
manifest, registry-fingerprint, or compiled-anchor drift.

The application protobuf bindings have two explicit project owners:
`Infinium.Application` generates the full service-side/application surface,
while `Infinium.ApplicationClient` independently generates the narrow
client-only bindings needed by the desktop package. The latter avoids shipping
analysis, persistence, MO2, provider, or coordinator implementation assemblies.

The generated C# codec/dispatch interface is implemented by the closed
`RendererApplicationProjectionCodec`; its focused tests pass real protobuf
message shapes through the generated dispatch boundary and the active renderer
schema. `-Task CheckGenerated` derives all three outputs in memory and compares
them byte-for-byte without modifying the worktree; it fails if any output is
stale.

The registry is revision `1.3.0`; the host-owned session handshake and
host-attested cancellation grant are renderer contract `1.4.0`. The application, domain,
storage, protobuf fingerprint, and renderer registry are independent version
axes. WP7 does not change protobuf bytes or the application fingerprint.

The generated catalog contains nine closed operations and sixteen exact
message shapes. It maps bootstrap, `ListResultItems`, `GetResultDetail`,
`GetProgress`, and `SubscribeEvents` to one generated application-client
and projection-codec signature each. Transport cancellation and authoritative resync map to closed
host-control signatures. Transport-only session establishment and gesture grant
carry no application authority. Raw `SubmitRunCommand`, targeted verification, generic
RPC access, paths, SQL, commands, URLs, credentials, providers, filesystem
access, and coordinator proxies are absent.

The TypeScript output also owns separate exact request, response, and event
operation unions, keyed payload maps, response-handler maps, and exhaustive
dispatch. Runtime partition and handler-coverage assertions reject an omitted,
duplicated, reordered, added, or unhandled operation. The generated bridge
mode crosses a serialized JSON boundary capped at 1 MiB and binds every
response or event back to its originating session, sequence, request or
subscription, operation, and projection revision. Event envelope and durable
sequences advance in separate directional domains; replay fails closed. WP8's
real WPF/WebView2 path exercises this generated bridge through the narrow
desktop application-client assembly.

The deterministic story client accepts exactly its one exported opaque story
run identity. An unknown run returns typed `not-found` for list and progress;
subscription initiation fails without fabricating a representable event.
Result lists apply the requested kind set and a case-insensitive substring search to
the bounded inert summary, preserve logical identity order, and encode the
next source offset in the opaque cursor. Detail succeeds only for an item that
exists under that exact run, kind, and item identity; absent, setup, empty,
wrong-run, wrong-kind, and out-of-range detail requests return typed `not-found`.
Every story declares all mutually exclusive progress counters explicitly, and
the counters sum exactly to its available known denominator. The same tests
compare these semantics through the fake client and serialized generated
bridge.

Third-party source acquisition is not a build input. Any future provenance
refresh must use an external temporary directory, copy only reviewed pinned
assets and licence evidence into their owned repository paths, and remove the
temporary acquisition bytes. The ignored `work` root is excluded from
repository scans and must remain untracked.

## Commands and offline behavior

Prime the exact tool packages once:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task Restore
```

Prove the lock can restore from the repository package cache with all package
sources cleared:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task RestoreOffline
```

Run deterministic generation drift, strict compilation, policy lint, and unit
tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task CheckGenerated
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task CheckDesktop
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task TypeCheck
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task Lint
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-frontend.ps1 -Task Test
```

Run the real populated desktop, lifecycle, accessibility, resource, and process
qualification receipt without a live package source:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/qualify-desktop.ps1
```

The latest complete sanitized output of that command is tracked as
[`desktop-qualification-receipt.v1.json`](desktop-qualification-receipt.v1.json),
including raw second-page and transport-cancel samples, host/browser memory
splits, every launch sample, observed message maxima, package/runtime sizes,
license identities, live coverage flags, and exact process-tree cleanup.

`-Task All` runs ordinary locked restore followed by all four checks. The
offline restore uses `--force-evaluate`, and its accepted run completed with
NuGet sources cleared. The lint
is first-party and intentionally small: strict TypeScript already owns the type
rules, while the lint denies unbounded `any`, dynamic evaluation, dynamic
function construction, direct renderer network access, and denied authority
field vocabulary outside generated files.

## Current limitations

WP7/WP8 supply a diagnostic proof, not the polished M2 interface or installer.
The machine must already have Evergreen WebView2 `151.0.4129.50` or newer.
Public redistribution/SBOM closure remains a later distribution concern. This
record does not accept Checkpoint D, begin Phase E, or activate M2. Automated
accessibility qualification uses Chromium's full AX tree and Windows UI
Automation exposure plus keyboard, focus, contrast, zoom/reflow, reduced-
motion, names, landmarks, and live-status checks. No manual Narrator walkthrough
was performed or is claimed.
