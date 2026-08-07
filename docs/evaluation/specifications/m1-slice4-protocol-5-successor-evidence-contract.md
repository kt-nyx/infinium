# M1 Slice 4 protocol `/5` successor evidence contract

Status: Accepted

Version: `infinium.m1-slice4.protocol-5-evidence-contract/1.0.1`

Work ID: `M1/S4.5/PRE-B2/V5/WP1R`

## Lineage and authority

This contract is the ADR-0031 overlay on immutable accepted contract
`infinium.m1-slice4.protocol-4-evidence-contract/1.2.0`, whose model SHA-256 is
`09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5`.
Every rule, vocabulary, fact constructor, state partition, normalization,
atomic boundary, coverage population, and invariant not named below is
inherited unchanged. This file does not revise protocol `/4`.

Historical version `1.0.0` remains frozen at WP1R commit
`cd23a96be50820326db1f1247edb11c3c86f230b`. Version `1.0.1` changes no
semantic rule. It restores the inherited fixed-row order and strengthens the
machine proof metadata after the resumed WP1 audit found those nonsemantic
defects.

## Authorized replacement

For each semantically applicable FaceGen mesh or tint path with no loose
winner, when exhaustive byte-verified authority cannot establish exact loose
absence:

- publish the existing accepted unknown asset value: normalized path, empty
  providers, null winner, `present=false`, and
  `exact_absence_known=false`;
- add one to the `face-gen-loose-assets` denominator and zero to completion;
- contribute exactly one owning `P5-GAP-LOOSE-AVAILABILITY` member;
- aggregate that member only by
  `face-gen-loose-assets` plus
  `exhaustive-byte-verified-loose-provider-index`;
- publish the aggregate in the snapshot and result;
- keep mesh and tint as independent obligations and never count an obligation
  more than once.

The loose gap affected count MUST equal the aggregate loose denominator minus
completed count. A positive all-incomplete loose population is `unsupported`;
a positive mixed population is `completed_with_gaps`; a positive exact
completion with no loose gap is `completed`. Existing zero-denominator
semantics remain `0/0/completed`.

Archive decisions are orthogonal. A resolved archive obligation contributes
`face-gen-archive-assets +1/+1`. An unresolved archive obligation contributes
`+1/+0` and separately owns `P4-GAP-ARCHIVE`. Archive evidence cannot alter
the loose unknown value, loose arithmetic, loose gap, or loose lifecycle.

`P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED` replaces
`P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED` and adds only the loose gap.
`P5-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED` replaces
`P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED` and adds the loose gap
beside the inherited archive gap. Their existing state classes remain
unchanged for the reason recorded in ADR-0031.

## Global composition invariant

Every admitted rule/state must produce at least one complete legal snapshot
composition. Across primary and additional population effects, gaps, result
publication, fixed coverage rows, and atomic boundaries:

- incomplete positive coverage has an exact owning gap unless an accepted
  terminal lifecycle prevents publication;
- complete coverage has no capability gap for that population, except the
  inherited `unsupported-records` semantic-limitation row defined by `1.2.0`;
- each coverage-owned gap corresponds to an incomplete population and no
  owner is missing, duplicated, or unrelated;
- effects on one population agree and aggregate arithmetically;
- all ten fixed coverage rows occur once;
- lower-layer retention never implies higher-layer completion;
- snapshot gaps are mirrored once in result gaps; and
- an atomic rejection publishes no partial object.

The accepted global composition validator materializes the overlay, verifies
its exact bounded delta, and composes constructor dispositions, fact templates,
coverage, gaps, lifecycle, publication, fixed rows, and atomic boundaries into
complete fifteen-family witnesses. It exercises all fifteen unordered
mesh/tint path combinations, no-snapshot publication, N-way cross-family
aggregation, and model-derived mutations, then emits a runtime-independent
machine summary bound to the validator hash. Its output must agree
byte-for-byte over two runs on every required PowerShell runtime.
