# ADR-0009: Skyrim runtime and Bethesda semantic support

Status: Accepted  
Date: 2026-07-25  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Runtime identity, effective MO2 state, plugin semantics, archive behavior, and
localized strings are separate authority surfaces.
[RESEARCH-0007](../../research/investigations/RESEARCH-0007-skyrim-runtime-support-contract.md)
identified a conservative exact-runtime gate.
[RESEARCH-0008](../../research/investigations/RESEARCH-0008-mutagen-bethesda-semantic-capability.md)
found `Mutagen.Bethesda.Skyrim` `0.54.2` suitable as a bounded semantic-library
candidate while demonstrating that its standard archive/string environment is
not authoritative for Infinium.

[ADR-0007](ADR-0007-exclude-xedit-from-infinium.md) excludes xEdit from every
product, development, and evaluation boundary. Parser qualification therefore
uses independently specified first-party fixture truth.

## Decision

1. The initial active runtime target is Steam Skyrim SE for Windows x64,
   runtime `1.6.1170.0`, App ID `489830`.
2. The first accepted support-manifest entry uses:
   - executable byte length `37,157,144`;
   - SHA-256
     `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`;
   - AMD64 PE32+ GUI identity;
   - fixed file/product version `1.6.1170.0`; and
   - the Steam channel/distribution evidence recorded by RESEARCH-0007.
3. Exact whole-file SHA-256 agreement plus immutable support-manifest
   consistency authorizes runtime-specific semantic coverage. Version strings,
   PE fields, Steam data, SKSE, Address Library, and native-component signals
   remain typed supporting or compatibility evidence, not substitutes.
4. Runtime detection is read-only and fail-closed. Results distinguish
   `supported-exact`, `unsupported-known`, `unrecognized-build`,
   `indeterminate`, `inconsistent-metadata`, and scoped dependency gaps.
5. `Mutagen.Bethesda.Skyrim` `0.54.2`, source commit
   `282bb99a77b2df7f1b092b06270e8e3c8fb55463`, is the accepted initial
   Bethesda semantic-library dependency.
6. Mutagen may provide only positively allowlisted Skyrim SE plugin records,
   fields, FormKeys, links, override chains, winners, and low-level BSA reads
   over exact inputs supplied by the authoritative snapshot/provider layer.
7. Mutagen's typical-environment discovery, automatic load-order authority,
   standard archive applicability/order, and standard localized-string lookup
   are not Infinium authority.
8. Each supported record/field/shape/localization state must pass
   parser-independent EVAL-0052 qualification. The Mutagen path under test may
   not be the sole author of expected results.
9. Effective archive activation/member precedence and archived localized
   strings remain unsupported for the initial M1 envelope until a
   provider-aware route passes the relevant EVAL-0051/EVAL-0052 cases.
10. The package graph shall be locked and hashed, inventoried in the SBOM and
    notices, and recorded in analyzer provenance. Upgrading Mutagen or the
    runtime-support manifest is a semantic requalification event.
11. Parser operations must use a later accepted cancellable,
    resource-bounded, failure-isolated execution boundary. This ADR does not
    select the application process topology or IPC mechanism.
12. xEdit has no fallback, oracle, manual-evaluation requirement, or other
    Infinium role. ADR-0007 remains controlling.

## Qualification gates

Acceptance selects the runtime and library boundary, not supported field
breadth. Before an M1 plan or release claims a semantic surface:

- EVAL-0054 must pass the exact-hash, same-version/unknown-hash, other-channel,
  malformed, missing, unreadable, and capture-race matrix;
- EVAL-0052 must pass every consumed record family, field, link, override
  shape, and localization state using independent fixture expectations;
- malformed and pathological inputs must fail inside accepted resource and
  cancellation bounds;
- exact snapshot/provider inputs must come from ADR-0008 and ADR-0010; and
- archive/string gaps must remain explicit until their separate route passes.

## Consequences

- Runtime-specific analysis is conservative and reproducible.
- Mutagen becomes the sole programmatic Bethesda semantic dependency.
- The initial supported semantic matrix will be deliberately narrower than
  Mutagen's generated API surface.
- Legitimate but unregistered executable variants may initially be rejected
  until deliberately qualified.
- Archive applicability and localized-string resolution require additional
  first-party provider-aware work.

## Validation

- A one-byte executable change fails the exact runtime gate even when version
  metadata still matches.
- No runtime detector launches, verifies, updates, or repairs Skyrim or Steam.
- Mutagen receives exact ordered bytes rather than discovering the user's
  environment.
- Unsupported fields and shapes produce coverage gaps, not inferred values.
- Dependency and packaging manifests contain no xEdit component.

## References

- [ADR-0007 — Exclude xEdit](ADR-0007-exclude-xedit-from-infinium.md)
- [ADR-0008 — MO2 profile and effective state](ADR-0008-mo2-profile-effective-state-and-local-identity.md)
- [RESEARCH-0007 — Skyrim runtime support](../../research/investigations/RESEARCH-0007-skyrim-runtime-support-contract.md)
- [RESEARCH-0008 — Mutagen capability](../../research/investigations/RESEARCH-0008-mutagen-bethesda-semantic-capability.md)
- [RESEARCH-0013 — Wave B integration](../../research/investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
