# M1 Slice 4 protocol `/5` WP1R successor-model acceptance

Status: Accepted

Date: 2026-08-07

Work ID: `M1/S4.5/PRE-B2/V5/WP1R`

Input commit: `e7d4c74c25814744dd370177e8404fc038152da8`

## Accepted identity and delta

ADR-0031 accepts semantic model
`infinium.m1-slice4.protocol-5-evidence-contract/1.0.0` at SHA-256
`76a1a364542d959942bfdf79332bdfba0a6dd83eeb2d7052f5e2a76e6c2e37e6`.
Its schema SHA-256 is
`dd82e34e0ea90eb7c59192779da32b6254cabf0f5d27cc955d36ca40ef16aa1d`
and its contract SHA-256 is
`8a89129a05da54103ae88eb8d8b106072f264d1cf5840c5652f08558d2500c1c`.

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

The accepted machine summary SHA-256 is
`ed1a6b2b7e40012319023c2a0d0b1f5106e9afb1791f2980c23f876a404530a8`.
It reports:

- 15 families, 77 publication rules, 63 admitted rules composed, 9 gap rules,
  10 coverage populations, and 11 atomic boundaries;
- 23,660 raw states: 110 admitted, 6,180 excluded, and 17,370 invalid;
- 853 successful witnesses: every admitted state, 741 pairwise cross-family
  aggregates, and two explicit localized/discovery capability events;
- 65 coverage effects, 47 positive effects, 14 incomplete effects, 20 gap
  effects, and 19 admitted gap-bearing states;
- zero contradictions, uncovered compositions, overlaps, or duplicate/
  overlapping owners; and
- 24/24 rejected global mutations.

The composition digest is
`e61ce15bc9a4595110a55ff235f72afe34d7cdda3255251a88e61645a98ef2f0`;
the mutation digest is
`390db153191e62687172f93b80031c0931fcf8850b5b70d401f17b251f01943d`.

Windows PowerShell and PowerShell each ran the gate twice. All four 1,319-byte
machine summaries were byte-identical at SHA-256
`ed1a6b2b7e40012319023c2a0d0b1f5106e9afb1791f2980c23f876a404530a8`.
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
