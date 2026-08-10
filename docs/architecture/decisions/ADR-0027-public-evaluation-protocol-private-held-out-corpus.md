# ADR-0027: Public evaluation protocol, private held-out corpus, and separate evaluator ownership

Status: Accepted
Disposition: partially superseded by ADR-0030, ADR-0032, and ADR-0033
Date: 2026-08-04
Accepted: 2026-08-04
Accepted by: Project owner
Last reviewed: 2026-08-10
Supersedes: Part of ADR-0026
Superseded by: ADR-0030 for Decision 15; ADR-0032 for the M1 Slice 5 held-out-`PASS` prerequisite; ADR-0033 for all active protocol `/4` retention and execution

Accepted clarification, 2026-08-04: independently authorable held-out
semantics and implementation-specific public conformance are separate proof
surfaces. The normative boundary is recorded in
[the final Slice 4 held-out scope amendment](../../evaluation/evaluator-history.md).

Accepted successor disposition, 2026-08-07: ADR-0030 supersedes only Decision
15's `/4`-finality and no-`/5` restriction. Every other decision and boundary
in this ADR remains authoritative. Protocol `/4` remains immutable historical
evidence; `/5` is a separately qualified successor, not a repair or retry.

Accepted evaluator-deferral disposition, 2026-08-07: ADR-0032 retires the
unqualified `/5` line and supersedes this ADR only where the current M1 plan
requires a held-out `PASS` before Slice 5. Decisions 1 through 14, including
private default denial, answer isolation, no retry or repair, exact identity,
contamination handling, provenance, and separate roles, remain authoritative.

Accepted protocol-retirement disposition, 2026-08-10: ADR-0033 retires and
archives protocol `/4`. No evaluator protocol from this ADR remains runnable or
required by current review. Decisions 1 through 14 remain historical governance
input for any newly authorized future evaluator, not current execution authority.

## Context

ADR-0026 correctly separated answer-bearing validation and held-out fixtures
from ordinary implementation context, but evaluator v1 also copied protocol,
schema, package-validation, scorer, and repair authority into the private
repository. That split produced independently evolving contract trees and a
repair/reseal loop in which evaluator failures prevented a valid product
verdict. Slice 4 implementation commit
`98fe8a5a173116427bf78077673fd10e8d018103` passed its retained public gates,
but evaluator v1 issued no authoritative held-out product result.

The private data must remain answer-isolated. The rules used to interpret and
score that data do not need to be private and must be reviewable, calibrated,
and frozen independently of any hidden case.

## Decision drivers

- Preserve materially independent held-out evidence and a separate private Git
  history.
- Make every interpretation, normalization, comparison, and error boundary
  publicly reviewable and testable.
- Prevent product implementation from repairing the evaluator and prevent a
  scorer from repairing either candidate or corpus.
- Distinguish a valid product mismatch from an invalid evaluation attempt.
- Bind every verdict to exact candidate, evaluator, and corpus identities.
- Retain provenance without exposing private paths, locators, inputs, answers,
  or raw results.

## Considered options

### Continue evaluator v1 in the private repository

Rejected. Private copies of public contracts drifted, evaluator maintenance
became entangled with scoring, and repeated admission failures yielded no
product verdict.

### Publish the held-out corpus

Rejected. Public inputs and expected answers would remove the independent
measurement the separate repository exists to preserve.

### Public rules with private data and thin scoring

Selected. The public repository owns the versioned protocol, schemas,
canonicalization, scorer, black-box adapter, calibration suite, and evaluator
qualification. The private repository owns hidden inputs and expected outputs,
a shallow identity/hash manifest, corpus qualification evidence, and private
raw and sanitized run records.

## Decision

1. Infinium evaluator v2 is a public protocol. Its schemas,
   canonicalization, comparison rules, terminal vocabulary, scorer,
   black-box candidate adapter, and answer-known calibration suite live in the
   public product repository under a dedicated evaluator tool boundary.
2. The separate evaluator-private repository remains required, but its active
   v2 role is limited to hidden fixture inputs, hidden expected typed outputs,
   a shallow manifest pinning the exact public evaluator identity and required
   file hashes, corpus qualification/freeze evidence, access records, and raw
   plus sanitized run records.
3. A private copy of the full public contract tree is not an independent
   authority. Private data pins one exact public evaluator commit, protocol ID,
   schema IDs, and file hashes; interpretation comes only from that public
   version.
4. Scoring is thin and read-only. It verifies the exact
   candidate/evaluator/corpus tuple, executes the frozen candidate as a black
   box through the public adapter, validates and canonicalizes candidate and
   expected output under public rules, compares typed values and identities,
   and emits exactly one terminal result.
5. The terminal results are `PASS`, `FAIL`, and `EVALUATOR_ERROR`. `FAIL` is a
   product verdict only after valid identity, execution, schema, oracle, and
   comparison admission. Any evaluator, manifest, corpus, oracle,
   infrastructure, or admission failure is `EVALUATOR_ERROR` and carries no
   product verdict.
6. Public evaluator qualification and freeze must complete before any private
   candidate scoring. Qualification includes answer-known positive,
   mutation, malformed, determinism, and write-boundary calibration.
7. Evaluator/corpus authoring and maintenance, candidate scoring, and public
   closeout are separate fresh tasks or roles. Product implementation does not
   repair, reseal, retry, or tune the private evaluator. The scorer edits
   nothing. Public closeout consumes only a sanitized attestation.
8. One held-out invocation is bound immutably to an exact candidate commit and
   artifact identity, evaluator commit and protocol identity, and corpus
   ID/version/hash. A changed member creates a new invocation, not a retry of
   the old tuple.
9. Automatic reseal, repair, fallback, or retry loops are prohibited. A
   terminal `EVALUATOR_ERROR` blocks the gate until a separately authorized
   maintenance task qualifies and freezes a successor evaluator or corpus.
10. If hidden input or answer detail is revealed to implementation, or a hidden
    result drives product behavior, that case version becomes development
    coverage. A materially independent replacement must be qualified and
    frozen before a later held-out claim.
11. Exact provenance is retained, but sanitized public results contain no
    private path, locator, answer, raw candidate output, raw oracle, or
    answer-bearing case identity beyond the approved corpus identity.
12. Separate roles are semantic independence controls, not bureaucracy. Role
    splits or additional approvals that do not improve answer isolation,
    oracle independence, or terminal scoring integrity are not required.
13. Stronger OS identity, VM, or private-CI isolation remains an optional
    future enforcement mechanism. It is not an M1 prerequisite unless the
    project owner or the exercised threat model requires it.
14. Private held-out comparison is limited to facts independently derivable
    from public semantic/byte rules, the answer-free execution manifest,
    accepted taxonomy vocabulary, and hidden input bytes. Exact diagnostic
    strings, library-specific typed mappings, internal IDs, serialization,
    persistence, and provenance identifiers remain public-conformance-only.
15. Protocol `/4` is the final M1 evaluator revision. One fresh Stage B2
    oracle review may resume after its freeze. A remaining authority gap is
    recorded for owner milestone disposition; it does not authorize `/5`,
    evaluator expansion, or use of product output as truth.

## Consequences

### Positive

- Evaluator behavior can be reviewed and mutation-tested without revealing
  held-out data.
- Candidate failures and evaluator failures have unambiguous meanings.
- The private repository becomes smaller and cannot silently redefine public
  rules.
- One frozen evaluator can score materially independent future corpora.

### Negative

- The public repository must maintain a standalone evaluator tool and stable
  schemas.
- Private corpus qualification must pin and audit public hashes before use.
- A terminal evaluator error requires a separate maintenance cycle rather than
  an in-place repair or retry.

### Risks and mitigations

- **Public scorer overfits known fixtures:** calibration uses generic
  answer-known cases and mutations; production and scorer code prohibit hidden
  case branches and hard-coded expected values.
- **Private corpus introduces hidden rules:** all interpretation is rejected
  unless representable by the pinned public protocol and schemas.
- **Wrong candidate is executed:** candidate commit, built artifact length and
  SHA-256, adapter identity, and dependency inventory are checked before
  execution.
- **Evaluator failure is reported as product failure:** terminal-state tests
  require every pre-comparison failure to emit `EVALUATOR_ERROR`.
- **Result writing escapes its authority:** the scorer accepts one explicit
  result directory, resolves it before use, and rejects traversal, aliases,
  and output paths outside that root.

## Requirements affected

- EVID-001 through EVID-007
- SNAP-005 and SNAP-006
- SEC-001, SEC-003, and SEC-004
- OPS-002 and OPS-003
- ANALYSIS-003 and ANALYSIS-016
- EVAL-0052 and EVAL-0086

## Validation

- Public schemas and typed readers reject identity drift, duplicate JSON
  properties, malformed output, malformed oracles, and unknown terminal
  states.
- Calibration proves known-correct acceptance and intended-assertion rejection
  for winner, chain, FormKey, missing/extra fact, link, ownership, placement,
  and unsupported/gap mutations.
- Broken manifests and malformed oracles produce `EVALUATOR_ERROR`, never
  `FAIL`.
- Repeated calibration runs produce byte-identical canonical assertion and
  sanitized result output.
- Repository scans reject private payloads/answers in public Git and
  fixture-specific identifiers in production runtime projects.
- A held-out verdict is accepted only with a sanitized attestation bound to an
  exact candidate/evaluator/corpus tuple.

## References

- [ADR-0026](ADR-0026-evaluator-private-fixture-repository-and-delegated-access.md)
- [Evaluator-private fixture governance v2](../../evaluation/evaluator-private-fixture-governance-v2.md)
- [Historical M1 evaluator-v2 baseline amendment](../../evaluation/history/m1-evaluator-v2-baseline-amendment.md)
- [M1 Slice 4.5 execution plan](../../evaluation/evaluator-history.md)
