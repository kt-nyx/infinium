# M1 Slice 4 protocol `/5` WP1R successor-model acceptance

Status: Accepted

Date: 2026-08-07

Work ID: `M1/S4.5/PRE-B2/V5/WP1R`

Input commit: `e7d4c74c25814744dd370177e8404fc038152da8`

## Accepted identity and delta

ADR-0031 accepts semantic model
`infinium.m1-slice4.protocol-5-evidence-contract/1.0.1` at SHA-256
`3f375adcc59e436a75f14f8e46afcf0286bb202cb05fe256af6b679a726bab66`.
Its schema SHA-256 is
`0783d6543bba756c1478d2601bf538bc6a3095be0a3d88fe6db0d90308bc8c09`
and its contract SHA-256 is
`7c5296dd671900d7d7a59e655204b80362f93c37897b8d8cbb7887b8205e25b7`.

WP1R commit `cd23a96be50820326db1f1247edb11c3c86f230b` remains the immutable
historical acceptance of version `1.0.0` at model hash
`76a1a364542d959942bfdf79332bdfba0a6dd83eeb2d7052f5e2a76e6c2e37e6`.
The resumed WP1 audit found a fixed-row ordering error and incomplete proof
dimensions in its metadata and validator, not a semantic-rule defect. Version
`1.0.1` is the nonsemantic proof-metadata successor: its complete authorized
FaceGen delta is byte-equivalent in meaning and `1.0.0` remains reproducible at
the WP1R commit.

The immutable predecessor remains
`infinium.m1-slice4.protocol-4-evidence-contract/1.2.0` at SHA-256
`09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5`.
Its contract, schema, and acceptance attestation hashes also match the exact
lineage pins in the successor model.

The complete authorized semantic delta is:

- add `P5-GAP-LOOSE-AVAILABILITY` at exact pair
  `face-gen-loose-assets` /
  `exhaustive-byte-verified-loose-provider-index` with
  `snapshot-and-result` scope;
- replace the two unknown-loose FaceGen rules with distinct `P5` successor
  IDs that retain their fact dispositions and archive effects while adding
  exactly one loose-gap contribution per independently affected path;
- update the two corresponding admitted-region required-gap sets;
- replace only the loose coverage registry's owning-gap statement; and
- add `INV-FACEGEN-LOOSE-AVAILABILITY` and the global composition policy.

No other rule, state class, constructor, fact, coverage effect, normalization,
atomic boundary, or protocol `/4` artifact changed.

## Deterministic global composition proof

The strengthened accepted machine summary SHA-256 is
`f137c39302db01a4d348f4ca5a8b9626cc38e604012d353fe4c25cc2e9e38b95`.
It reports:

- 15 families, 77 publication rules, 63 admitted rules composed, 9 gap rules,
  10 coverage populations, and 11 atomic boundaries;
- 23,660 raw states: 110 admitted, 6,180 excluded, and 17,370 invalid;
- 869 successful witnesses: 109 complete snapshots, one no-snapshot witness,
  741 pairwise cross-family aggregates, one global N-way witness, all 15
  unordered FaceGen mesh/tint combinations, and two explicit
  localized/discovery capability events;
- 65 coverage effects, 47 positive effects, 14 incomplete effects, 20 gap
  effects, and 19 admitted gap-bearing states;
- zero contradictions, uncovered compositions, overlaps, or duplicate/
  overlapping owners; and
- 183 constructor assignments and 732 exact fact templates composed, including
  exact retained effects for all 10 admitted projection-family rules with zero
  effectless bypasses; and
- 35/35 rejected model-derived global mutations.

The composition digest is
`cbea94aaed2dc20a329187a4ace76a2679605530613bdf647aac9018232795ee`;
the mutation digest is
`61b3e9d457ce202cbe6219bf9db085d4045a80b236c0ab33a2463280f3edfb84`.

Windows PowerShell and PowerShell each ran the strengthened gate twice. All
four 1,870-byte machine summaries were byte-identical at SHA-256
`f137c39302db01a4d348f4ca5a8b9626cc38e604012d353fe4c25cc2e9e38b95`.
An initial runtime comparison exposed culture-sensitive ordering in the
composition digest; ordinal sorting corrected that deterministic tooling
defect before formal parent review.

## Independent audits and parent review

Three fresh, read-only, non-delegating public audits covered:

1. the owner-authorized delta and exact gap ownership;
2. exhaustive cross-family composition and missing validator dimensions; and
3. successor identity, historical preservation, and documentation surfaces.

The semantic audit required both unknown-loose branches to own the new gap and
required explicit treatment of the supported-archive rule's historical
`resolved` class. ADR-0031 preserves that class because the FaceGen/archive
decision is resolved while the independent loose value remains an accepted
unknown whose capability is disclosed by coverage, gap, and result lifecycle.
Changing the state class would exceed the sole authorized delta.

The composition audit reproduced all 110 family-local states and found no
second semantic contradiction. It identified two registry capability events
without direct publication-rule producers; the global proof now constructs
and validates those explicit localized/discovery witnesses. The history audit
confirmed ADR-0031 as the next number, predecessor byte identity, the distinct
successor namespace, the historical hard-stop preservation requirement, and
the WP0 trailing-whitespace defect.

Parent review verified the exact WP1R exit criteria, bounded semantic delta,
complete composition counts, immutable predecessor hashes, deterministic
runtime agreement, protected-path scope, documentation consistency, and
`git diff --check`. No material finding remained and no parent-review
correction pass was consumed.

## Boundary

WP1R is semantic recovery under a new successor identity, not a WP1 correction
attempt, evaluator repair/retry, candidate verdict, or private qualification.
No evaluator implementation began. Product/candidate source, tests, builds,
artifacts, execution, and adaptation; private data; oracle answers; B2; C2;
Stage D; Slice 5; live/billable calls; protocol `/6`; and push remained outside
the work.
