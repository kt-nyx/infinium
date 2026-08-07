# M1 Slice 4 protocol `/5` WP1V projection proof closure

Status: Hard-stopped after final independent re-review

Work ID: `M1/S4.5/PRE-B2/V5/WP1V`

Date: 2026-08-07

Starting branch: `codex/m1-slice-4.5-protocol-5-successor`

Starting commit: `cd23a96be50820326db1f1247edb11c3c86f230b`

## Bounded purpose

WP1V repairs and independently validates the resumed WP1 public proof system.
It does not reopen ADR-0031 or successor semantics. It does not authorize WP2,
candidate or product execution, private material, B2, C2, scoring, protocol
`/6`, or product output as truth.

The starting worktree contained the intentional predecessor handoff of 15
modified and two untracked public files. The parent preserved and inventoried
that work before editing.

## Confirmed defects

The inherited global validator treated `coverage`, `gaps`, and `result_gaps`
as projection-only and replaced their admitted rules with empty constructors,
facts, coverage, and gap effects. Aggregate success therefore did not prove
rules such as `P4-COVERAGE-COMPLETE`, `P4-GAPS-EMIT`, and
`P4-RESULTGAPS-NO-SNAPSHOT`. The successor model also declared version `1.0.1`
while its authority pointer and schema still named contract `1.0.0`.

WP1V removes the empty-effect branch, retains all projection-family rule
effects, and corrects the authority pointer to
`infinium.m1-slice4.protocol-5-evidence-contract/1.0.1`. Historical `1.0.0`
remains reproducible at the WP1R commit.

## Finite rule closure

The mandatory
[`rule-coverage ledger`](specifications/m1-slice4-protocol-5-rule-coverage-ledger.json)
has identity `infinium.m1-slice4.protocol-5-rule-coverage-ledger/1.0.0` and
SHA-256
`8d88061dbcd7d206533c2ed245861c3c823f6576cd9ade727050d75bb8e22904`.
It contains exactly 77 unique accepted successor publication rules:

- 63 admitted rules, each with one exact complete canonical witness;
- 14 terminal rules, each with an exact no-publication rejection witness;
- all 15 families; and
- 10 admitted rules across `coverage`, `gaps`, and `result_gaps` with exact
  rows, arithmetic, lifecycles, populations, capabilities, aggregates, mirrors,
  and no-snapshot outcomes.

The validator independently reconstructs accepted rule metadata from the
successor model and rejects ledger omissions, duplicates, unknown IDs, family
or constructor misbinding, fact/property/object substitutions, authority
drift, missing witnesses, false terminal publication, and unproven empty
effects. The ledger is an explicit proof input; the document under test is not
used as its own oracle.

## Initial deterministic results

The global summary is 1,870 bytes at SHA-256
`f137c39302db01a4d348f4ca5a8b9626cc38e604012d353fe4c25cc2e9e38b95`.
It reports 77 rules, 63 admitted rules, 110 admitted states, 10 retained
projection-rule effect witnesses, zero effectless bypasses, 183 constructor
assignments, 732 fact templates, 869 successful composition witnesses, and
35/35 rejected global mutations. Its composition digest is
`cbea94aaed2dc20a329187a4ace76a2679605530613bdf647aac9018232795ee`.

The corrected projection summary is 2,378 bytes at SHA-256
`920fe2ef10f8c066dc81c20b2e93e00d3166591b10f3580e3f71886174ee58ba`.
It reports 77/77 rules closed, 63 exact admitted witnesses, 14 exact terminal
rejections, 15/15 families, 10/10 admitted support-family rules, 60 complete
snapshot witnesses, three no-snapshot witnesses, 63 schema-valid and exact
fact witnesses, 50/50 rejected model/document/ledger mutations, zero bypasses,
zero uncovered rules, zero overlaps, and zero issues.

Two Windows PowerShell 5.1 runs and two PowerShell 7 runs produced identical
global summaries at the global hash above and identical projection summaries
at the projection hash above. Each run executed both public validators.
Independent ledger generation under both runtimes also produced identical
537,465-byte ledger bytes at the ledger hash above.

## Review and correction budget

The one required fresh independent public review rejected the initial proof.
It found generic constructor-wide witness values instead of property-specific
types and values, stale current identity pins, mutations that did not all pass
through the full proof checks, and cross-runtime ledger serialization drift.

The sole permitted correction pass replaced those witnesses with exact typed
property values, added explicit FaceGen present/absent/unknown semantics,
made documents consume ledger witnesses, compared exact fact/type/value sets,
routed ledger and document mutations through reusable closure checks, derived
summary closure counts from validated sets, aligned current identity records,
and made ledger ordering and serialization runtime-independent. The initial
reviewer performed the required final re-review and returned `REJECT`. It found
that four resolved-link witnesses contain 72 noncanonical placeholder values
and that the validator compares those values back to the same ledger without
independently enforcing accepted link state, field, component, and FormKey
semantics. The claimed 63 exact admitted witnesses and zero-issue closure are
therefore false despite deterministic green validator output.

No further correction pass remains. WP1V is hard-stopped, WP1 is not proof-
closed, and WP2 has not started. The complete failure evidence and required
owner decision are preserved in the
[WP1V hard-stop record](m1-slice4-protocol-5-wp1v-proof-closure-hard-stop.md).
