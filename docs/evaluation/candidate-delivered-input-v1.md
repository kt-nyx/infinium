# WP3 delivered candidate input and expansion v1

Status: implementation-active WP3 product contract.

This document defines the public, answer-free boundary used to construct the
M1 Slice 5 WP3 candidate population. The machine contracts are:

- `candidate-delivered-input.v1.schema.json`;
- `candidate-delivered-expansion.v1.schema.json`.

Neither contract may carry candidate lanes, dispositions, candidate or
hypothesis states, abstentions, gaps, failures, expected output, fixture IDs,
oracle metadata, generator IDs, or generator seeds. Those values are derived
after admission by the product candidate source and selector or remain in an
isolated oracle.

## Delivered factual input

`candidate-delivered-input/v1` binds every fact to one originating run, source
snapshot, analysis context, and effective configuration. Its `payload_id` is
the logical artifact identity selected by the input producer; it is not an
answer or an output identity. Exact bytes are bound independently by the
execution-input artifact-reference fingerprint and the public fixture
manifest. Product adapters may derive a stable logical ID for local projections,
but a public fixture author does not reproduce a product identity algorithm.

The four factual collections have these meanings:

- `link_facts` retain one prior-versus-winning Bethesda record-link slot. The
  states are `absent`, `null`, `resolved`, or `unresolved`; only `resolved`
  carries a target participant. Source contribution identities remain bounded
  source text, while product graph nodes use opaque contribution IDs. The
  source derives a deterministic-required join. Equal comparable links are a
  resolved negative; a difference or introduced/removed link is a complete
  relationship; any unresolved side is ambiguous and retains the missing
  canonical-target requirement.
- `face_gen_facts` retain factual applicability, exact mesh/tint availability,
  actual provider presence, and bounded locality/specificity observations.
  The source derives an optional-ranked relationship. Not-applicable facts are
  resolved negatives. Unknown applicability or applicable facts with unknown
  availability remain ambiguous. Optional order is descending locality, then
  descending specificity, then the stable population member ID.
- `coverage_gap_facts` retain an explicit delivered population, denominator,
  missing capability, reason, dependencies, and evidence. The source maps the
  fact to a mandatory-evidence unsupported ledger member; the input does not
  prescribe the ledger disposition.
- `documentation_facts` retain the exact WP2 application, claim, passage,
  revision, local subject, run/snapshot/context binding, factual applicability,
  dependencies, and supporting/contradicting evidence. The source derives a
  mandatory-evidence join. Cross-run, cross-snapshot, or cross-context facts
  remain unsupported. Applicable and not-applicable are distinct; unknown or
  contradicted applicability remains ambiguous.

Every fact ID is unique across the complete document. Dependencies and
supporting evidence are nonempty, unique, and bounded. The schema and domain
invariants close every property, enum, array, text, offset, and correlation.
Every retained candidate decision carries the originating delivered
`source_fact_id` and an exact `derived-from` dependency edge, so independent
comparison and later audit do not reproduce opaque population-member IDs.

The production Bethesda/WP2 adapter is only a projection into this contract.
The same `DeliveredIndexCandidatePopulationSource` consumes both adapted real
inputs and public-fixture inputs; no fixture-only population source is part of
the semantic gate.

## Deterministic expansion

`candidate-delivered-expansion/v1` is the product-reachable scale construction
contract. It contains a subject count and one or more factual series. A series
emits at indexes `0, every, 2 * every, ...`; patterns repeat in declared order.
Resolved link target offsets are computed modulo `subject_count`. Subject and
fact identities are constructed deterministically from the admitted snapshot,
configuration, factual kind, series ordinal, and subject ordinal. The
`expansion_id` is an artifact binding and does not affect generated identities.

Expansion is bounded before construction:

- subject count: 1 through 1,000,000;
- total series: 1 through 128;
- patterns per series: 1 through 64;
- expanded factual rows: at most 1,000,000;
- materialized input: at most 100,000 facts.

The exact same expansion enumerator provides an identity-independent SHA-256
semantic-fact stream receipt for both materialized validation profiles and
non-materialized stress profiles. The receipt never contains generated opaque
product IDs or the `expansion_id`.

Each semantic record is a sequence of UTF-8 fields. Every field is framed as
eight lowercase hexadecimal digits containing its UTF-8 byte length, followed
by `:`, followed by the field bytes. Each complete record is framed once more
with the same eight-hex-digit byte-length prefix before its bytes are appended
to the SHA-256 stream. Integers use invariant base-10 text, booleans use
`true`/`false`, absent optional values use `none`, and enum values use their
JSON tokens. Records occur in this exact order:

1. one header: `header`, run ID, snapshot ID, analysis-context ID,
   configuration ID, subject count;
2. link facts in series order then ascending subject index: `link`, series
   index, subject index, repeating-pattern index, field, component-or-`none`,
   link ordinal (exactly the zero-based series index), prior state, prior target subject index-or-`none`, winning
   state, winning target subject index-or-`none`;
3. FaceGen facts in the same ordering: `facegen`, series index, subject index,
   pattern index, applicability, mesh availability, mesh-provider-present,
   tint availability, tint-provider-present, locality, specificity;
4. documentation facts in the same ordering: `documentation`, series index,
   subject index, pattern index, applicability, supplying-snapshot-matches,
   analysis-context-matches, has-contradicting-evidence. Each expanded
   documentation fact retains one explicit generated supporting-evidence ID;
5. coverage facts in the same ordering: `coverage-gap`, series index, subject
   index, pattern index `0`, missing capability, reason, denominator `1`.

Resolved target subject indexes use `(subject_index + offset) mod
subject_count`. Stress evidence may assert expansion counts and this receipt
without constructing or publishing a candidate aggregate. A validation profile
intended for full product comparison must remain below the 64 MiB candidate
aggregate/CAS boundary; neither producer nor persistence widens that bound for
evaluation.

Expanded source-fact identities are public coordinate keys, not hashes:
`candidate-{kind}-fact-s{series:D3}-n{subject:D8}`, where `kind` is exactly
`link`, `facegen`, `documentation`, or `coverage-gap`; `series` is the
zero-based kind-local series index padded to three digits; and `subject` is the
emitted subject index padded to eight digits. These are the `source_fact_id`
values retained by candidate decisions. All population, participant, evidence,
dependency, candidate, and hypothesis IDs remain product-owned opaque
identities and are excluded from independent oracle construction.

## Independent fixture use

A product-blind fixture author may read the accepted WP3 plan and public
Bethesda/WP2 authorities together with these two schemas and this field guide.
The author supplies only closed delivered facts or expansion parameters and
keeps expected candidate behavior in a separately frozen oracle. Product
source, tests, output, builds, private fixtures, and product identity algorithms
are not fixture-authoring inputs. Exact comparison uses a canonical semantic
projection keyed by retained factual IDs and relationships, rather than copying
the product's opaque-ID construction algorithm into the oracle.

The standard retained product artifact path is
`inputs/candidate-delivered-input.json` for semantic packages and
`inputs/candidate-delivered-expansion.json` for scale or stress packages. The
root `execution-input.json` lists that artifact in `input_payload_refs` with
exact version `1.0.0`, byte length, and SHA-256 fingerprint.
