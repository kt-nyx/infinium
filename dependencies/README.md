# Dependency licence and provenance review

Status: Completed
Disposition: M1 dependency review plus WP7/WP8 frontend and desktop additions verified; awaiting Checkpoint D review
Last reviewed: 2026-08-28

[`dependency-manifest.json`](dependency-manifest.json) inventories every NuGet
package resolved by the 21 committed product, test, and repository frontend-
toolchain project lockfiles under `src`, `tests`, and `eng/tooling`. The lockfiles are authoritative
for exact versions, dependency edges, and NuGet content hashes; the manifest
adds licence and source provenance.

Infinium project metadata states the accepted GPLv3-family posture without
introducing an operative `GPL-3.0-only` or `GPL-3.0-or-later` selector. No
project licence file or distributable package is introduced by Slice 0.

The resolved graph is compatible with the accepted GPLv3-family posture:

- Mutagen, GameFinder, NexusMods.Paths, Loqui, Noggog.CSharpExt, and
  Reloaded.Memory are GPLv3-family dependencies.
- The remaining resolved identities use the manifest-recorded MIT (including
  Node.js bundled notices), Apache-2.0, BSD-3-Clause (including WebView2 bundled
  notices), or public-domain classifications. GPL-3.0-only identities and the
  GPL-3.0-only variant with bundled MIT component notices remain explicit at
  the GPL boundary; no umbrella MIT claim is made.
- The Slice 6 WP1 `Microsoft.ML.Tokenizers` and embedded `O200kBase` 2.0.0
  packages are MIT-licensed and source-pinned. Their affected transitive
  `Microsoft.Bcl.Memory` 9.0.4 floor is explicitly overridden by Microsoft's
  patched MIT-licensed 9.0.14 package.
- `ini-parser-netstandard` `2.5.3` declares MIT and is content-locked, but its
  package metadata does not identify an immutable source revision. That
  provenance limitation is explicit and does not close the later public
  redistribution audit required by DIST-003.

This review establishes the bounded non-public M1 dependency posture. It does
not claim public packaging, corresponding-source delivery, SBOM generation, or
final redistribution compliance.

Phase D adds the locked `Node.js.redist.win/24.14.1` and
`Microsoft.TypeScript.MSBuild/5.9.3` build tools, the
`Microsoft.Web.WebView2/1.0.4129.50` SDK, and packaged React/ReactDOM `18.3.1`
production assets. The manifest records exact NuGet hashes, npm source-tarball
SHA-256 values, packaged-asset SHA-256 values, and licence evidence. Final
frontend checks restore from cache with package sources disabled; the desktop
host never downloads the Evergreen runtime and instead reports a missing or
outdated prerequisite inertly.
