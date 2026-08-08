# Evaluator-private fixture governance v2

Status: Accepted
Accepted: 2026-08-04
Accepted by: Project owner
Last reviewed: 2026-08-08
Authority: [ADR-0027](../architecture/decisions/ADR-0027-public-evaluation-protocol-private-held-out-corpus.md)
with the M1 deferral and sequencing disposition in
[ADR-0032](../architecture/decisions/ADR-0032-defer-m1-held-out-evaluator-and-continue-public-verification.md)
Predecessor: [Evaluator-private fixture governance v1](evaluator-private-fixture-governance.md)

## Purpose

This is the canonical operational policy for evaluator v2. It preserves
ADR-0026's separate private repository and answer isolation while making all
evaluation rules public and limiting the private repository to hidden data and
its lifecycle evidence.

## Current M1 disposition

This document governs isolation and lifecycle rules if a future evaluator is
separately authorized; it is not an active M1 scoring runbook. ADR-0032 defers
private held-out evaluation until after Slice 9 and M3 planning around a stable
user-meaningful output contract. No current B2, C2, Stage D, corpus authoring,
qualification, adaptation, comparison, scoring, repair, or replacement task is
authorized. Ordinary Slice 5 work must not read the private repository and
must use public staged work-package fixtures instead.

## Public-rule and private-data boundary

The public Infinium repository owns:

- protocol and schema identities;
- candidate-output and expected-output shapes;
- normalization, canonicalization, comparison, aggregation, and terminal-state
  rules;
- the scorer and candidate black-box adapter;
- evaluator qualification and public calibration/mutation tests; and
- sanitized result vocabulary and shape.

The evaluator-private repository may contain only:

- hidden input bytes and answer-free case manifests;
- hidden expected typed outputs/oracles;
- a shallow manifest pinning one exact public evaluator commit, protocol ID,
  and required public file hashes;
- independent input/oracle/corpus qualification and freeze evidence;
- access, disclosure, contamination, retirement, and replacement records; and
- private raw execution/scoring output plus sanitized run records.

It must not maintain a separately evolving copy of public schemas, scorer
logic, canonicalization rules, comparison rules, candidate adapter, or full
public contract tree.

## Forbidden patterns

- private rules not expressible by the pinned public protocol;
- expected truth derived from candidate output;
- product implementation repairing a corpus, oracle, scorer, or manifest;
- scorer repair, reseal, fallback, or retry;
- automatic mutation of inputs or expected answers;
- converting an evaluator admission failure into product `FAIL`;
- retrying the same terminal invocation after changing any tuple member; and
- additional role choreography that does not improve semantic independence.

## Responsibilities

### Corpus author

Constructs hidden inputs from the accepted scope and public protocol. The
author does not receive production output or hidden expected answers from a
predecessor case.

### Oracle reviewer

Derives expected typed outputs from independent evidence after input freeze.
The reviewer does not use production output as truth and records the methods
and exact frozen input identity.

### Public evaluator reviewer

Reviews public rules, schemas, adapter, scorer, calibration, error boundaries,
and write confinement without private data. This role completes before the
public evaluator is frozen.

### Scorer

Receives the frozen public evaluator, exact candidate artifact, and frozen
private corpus. It verifies identities, executes once, compares once, writes
only within the designated private result directory, emits one terminal
result, and repairs nothing.

### Public closeout

Receives only the sanitized attestation. It checks public identity bindings
and updates status; it does not inspect raw private inputs, oracles, paths, or
results.

Ordinary product work stops on any private evaluator or corpus failure. It
does not create or manage Stage B authoring, Stage C scoring, audit,
replacement, repair, or retry as recursive subtasks. Stage B authoring or
maintenance, Stage C scoring, and successor maintenance require separate
owner-authorized fresh tasks. Corpus/evaluator maintenance has no authority to
score a product, and the scorer has no authority to maintain any tuple member.

ADR-0030 and ADR-0031 historically authorized a public `/5` successor attempt
and its distinct semantic model after a public `/4` representation gap.
ADR-0032 retired `/5` unqualified before evaluator implementation, freeze,
private use, or verdict. WP1/WP1R/WP1V are historical records and WP2-WP4
never started. No `/5`, Stage B2, C2, Stage D, corpus, adaptation, comparison,
or scoring work is currently authorized.

The private held-out evaluator is deferred until after Slice 9 and M3 planning
around a stable versioned user-meaningful output contract. Re-entry requires
independently authorable expected values, answer-free totality/authorability
review, separate public implementation/private qualification/scoring/closeout
roles, and a new accepted ADR and plan. No future protocol identity is selected
here. Every default-deny, answer-isolation, no-retry/no-repair, exact-identity,
contamination, provenance, and role-separation rule below remains authoritative.

## Corpus qualification and freeze

1. Pin the accepted public protocol and evaluator commit plus every required
   public file hash.
2. Freeze hidden input bytes before oracle construction.
3. Derive expected output independently and validate both input and oracle
   against the public schemas.
4. Prove that every expected assertion uses only public interpretation rules.
5. Verify exact corpus ID, version, member inventory, hashes, partition,
   redistribution/privacy state, and answer isolation.
6. Record contamination as clean and create an immutable freeze/tag or
   equivalent retained revision.
7. Do not change the frozen corpus. Corrections create a successor version and
   preserve retirement/supersession history.

## Terminal scoring

One invocation binds:

```text
candidate commit + built artifact hash
evaluator commit + protocol/schema/scorer/adapter hashes
corpus ID + version + aggregate/member hashes
```

The scorer validates the tuple and answer-free execution manifest, validates
candidate and expected output, canonicalizes both under public rules, compares
typed values and identities, and emits exactly one of:

- `PASS`: every valid applicable assertion matched;
- `FAIL`: after candidate and input admission, a candidate invocation threw,
  its projected output violated the public candidate-output contract, or a
  valid comparison found one or more product mismatches; or
- `EVALUATOR_ERROR`: evaluator/corpus manifest, tuple identity, retained input,
  evaluator binary/dependency, oracle, publication, or other evaluator
  infrastructure/admission was invalid. This is not a product verdict.

No same-task repair or retry is permitted. A changed candidate, evaluator, or
corpus is a new invocation and must be described as such.

## Disclosure, reveal, contamination, and replacement

Normal return is a sanitized attestation containing only approved identities,
hashes, terminal result, failure stage/category when allowed, assertion counts,
and contamination state. It contains no private locator/path, input,
answer, raw output, or answer-bearing member identity.

If a private case is revealed or its result directly drives production code,
rules, prompts, thresholds, or ranking:

1. record the disclosure and contamination;
2. reclassify that case version as development;
3. retire it from future held-out claims;
4. retain it as public or private development regression coverage as policy
   permits; and
5. qualify and freeze a materially independent replacement before a later
   held-out claim.

An oracle error is corrected only from new independent evidence. The old case
and any verdict depending on it are invalidated, not rewritten.

## EVALUATOR_ERROR handling

`EVALUATOR_ERROR` maps to a blocked gate and carries no inference about product
correctness. Maintenance occurs in a new task with no scoring authority. After
a successor evaluator or corpus is publicly/private qualified and frozen, a
new scoring task may run a newly bound invocation. It must not be described as
an automatic retry of the prior tuple.

## Enforcement boundary

Fresh-context roles and repository separation are the procedural boundary for
any future evaluator work.
Separate OS identities, VMs, or private CI brokers may be added later when the
owner or threat model requires stronger enforcement. Their absence alone does
does not itself authorize or qualify evaluator work.
