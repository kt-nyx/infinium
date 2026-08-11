# Dependency licence and provenance review

Status: Completed
Disposition: M1 Slice 0 dependency licence and provenance review verified
Last reviewed: 2026-08-11

[`dependency-manifest.json`](dependency-manifest.json) inventories every NuGet
package resolved by the committed lockfiles. The lockfiles are authoritative
for exact versions, dependency edges, and NuGet content hashes; the manifest
adds licence and source provenance.

Infinium project metadata states the accepted GPLv3-family posture without
introducing an operative `GPL-3.0-only` or `GPL-3.0-or-later` selector. No
project licence file or distributable package is introduced by Slice 0.

The resolved graph is compatible with the accepted GPLv3-family posture:

- Mutagen, GameFinder, NexusMods.Paths, Loqui, Noggog.CSharpExt, and
  Reloaded.Memory are GPLv3-family dependencies.
- The remaining production and test dependencies are MIT-licensed.
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
