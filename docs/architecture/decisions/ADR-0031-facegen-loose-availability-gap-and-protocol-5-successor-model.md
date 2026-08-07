# ADR-0031: Accept the protocol `/5` successor semantic model

Status: Accepted

Date: 2026-08-07

Work ID: `M1/S4.5/PRE-B2/V5/WP1R`

## Context

The immutable accepted model
`infinium.m1-slice4.protocol-4-evidence-contract/1.2.0` proved protocol `/4`
totality, and ADR-0030 authorized a separately qualified protocol `/5`
successor derived from it. During `/5` WP1, global composition review found
that two accepted FaceGen states incremented `face-gen-loose-assets` without
completion but supplied no gap owning that incomplete population. The
family-local model was total, but those states could not compose into a legal
coverage snapshot.

The owner has now authorized one semantic delta. Historical model `1.2.0`,
its evidence contract, schema, hashes, acceptance record, protocol `/4`, and
the WP1 hard-stop remain immutable evidence.

## Decision

1. Accept `infinium.m1-slice4.protocol-5-evidence-contract/1.0.0` as the
   semantic model for the public `/5` successor. It is an explicit overlay on
   immutable model `1.2.0`; omitted content is inherited byte-for-byte.
2. Replace only
   `P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED` and
   `P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-UNSUPPORTED` with their named `P5`
   successor rules. Both retain the exact unknown loose-availability fact,
   contribute loose coverage `+1/+0`, and own one
   `P5-GAP-LOOSE-AVAILABILITY` contribution per affected path.
3. `P5-GAP-LOOSE-AVAILABILITY` has population
   `face-gen-loose-assets`, missing capability
   `exhaustive-byte-verified-loose-provider-index`, aggregation key
   `population+missing_capability`, and scope `snapshot-and-result`.
4. Archive coverage remains independent. A supported archive decision adds
   `+1/+1`; an unsupported decision adds `+1/+0` and retains
   `P4-GAP-ARCHIVE`.
5. Aggregate each loose mesh or tint obligation once. The loose gap affected
   count equals the loose denominator minus completed count. Unknown is never
   converted to absence, presence, failure, skipping, or completion.
6. For a positive loose population, zero completion is `unsupported`, partial
   completion is `completed_with_gaps`, and exact completion with no loose gap
   is `completed`. `0/0` remains `completed` under existing semantics.
7. The supported-archive rule retains state class `resolved`: the FaceGen
   applicability and archive decision are resolved while the independently
   represented loose-availability value is an accepted unknown. The coverage
   lifecycle, owning gap, and top-level result—not that rule classification—
   disclose the missing loose-index capability. Reclassifying it would be a
   second semantic change and is not authorized.
8. A deterministic global composition proof is required before this model or
   any dependent `/5` artifact is accepted. It must enumerate all
   contradictions rather than stopping at the first.

This ADR narrowly supersedes ADR-0030 and the accepted `/5` plan only where
they require model `1.2.0` to remain the active, unchanged `/5` semantic input.
All isolation, historical freeze, answer isolation, no-retry/no-repair,
layered-evidence, exact-identity, and later-role boundaries remain unchanged.

## Consequences

- Protocol `/4` and model `1.2.0` remain reproducible historical evidence.
- `/5` projection work consumes the successor identity and its verified
  materialized model, never a silently edited `1.2.0` artifact.
- The accepted change is mechanically inspectable as two rule replacements,
  two admitted-region gap updates, one coverage-registry replacement, one gap
  rule addition, and one cross-family invariant addition.
- Any other semantic choice, accepted-model edit, protocol `/6`, product use,
  private use, or scoring work remains a hard stop.

## Alternatives rejected

- Treating unknown as exact absence would invent a fact.
- Treating archive resolution as loose-resolution authority would collapse
  independent evidence layers.
- Leaving positive incomplete loose coverage without a gap would preserve the
  contradiction.
- Mutating or reusing model `1.2.0` or the changed `P4` rule identities would
  destroy historical reproducibility.
