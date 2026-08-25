# ADR-0035: Defer independent semantic-oracle qualification until the M2 acceptance / M3 planning boundary

Status: Accepted
Date: 2026-08-23
Accepted: 2026-08-23
Last reviewed: 2026-08-25
Supersedes: ADR-0034 only where it required independent semantic-oracle validation during M1
Superseded by: None

## Plain-language decision

Infinium will build and verify the product through M1 and M2 without building a
second, answer-key-driven evaluation system alongside it. An **independent
semantic oracle** is an answer set written and reviewed independently of the
product, then sealed before product output is compared with it. That differs
from a **developer-owned conformance test**, which checks that implementation
follows an accepted contract, invariant, example, or safety rule while the
product is being developed.

Independent semantic-oracle qualification is deferred throughout M1 and M2.
It does not gate either milestone. The project will reconsider it after M2 is
accepted, while planning M3, at the **M3 Evaluation Readiness Gate**. This
keeps current effort focused on a coherent, functional product and postpones
the expensive independent answer-key program until stable, user-meaningful
interfaces exist.

## Decision

M1 and M2 continue to require ordinary product verification: contract and
schema conformance; domain and SQL invariants; developer-owned positive,
negative, malformed, lifecycle, abstention, mutation, and metamorphic tests;
deterministic byte/reference tests; prompt-byte identity and hashing;
producer/consumer ownership; persistence, migration, and replay; provenance;
decision-link cardinality; security, credential, budget, and external-effect
boundaries; controlled integration evidence; and fresh semantic, security,
provenance, and diff review.

M1 and M2 may claim only product conformance within their accepted scope. They
may not claim an independent semantic verdict, held-out qualification,
semantic reliability, or M3's "Trusted personal preflight" readiness. No
current semantic-oracle package grants product authority. Existing
semantic-admission packages are historical non-authorizing development
evidence, not validation authority.

Until an accepted M3 plan reactivates the work, no successor semantic oracle
may be authored, independently reviewed, pre-sealed, registered as current
authority, compared with product output, repaired after comparison, or used as
an acceptance gate. This restriction is specific to independent semantic
product-answer qualification. Deterministic reference or golden tests remain
permitted when they verify bytes, codecs, algorithms, or accepted contracts.

Private evaluator isolation and default-deny access remain unchanged. This
decision does not authorize private fixture access, evaluator work, protocol
selection, archive archaeology, provider calls, credentials, or external
effects.

## Relationship to ADR-0034

ADR-0034 remains accepted for exact transmitted prompt provenance and for its
four separate semantic axes: proposal or extraction, support, applicability,
and host decision. This ADR supersedes only ADR-0034's timing requirement that
those product semantics obtain an independent semantic-oracle comparison
during M1. They remain mandatory product meaning and must be verified through
the M1/M2 product-conformance profile.

## M3 Evaluation Readiness Gate

The gate is a planning checkpoint, not automatic permission or a promise to
build a large oracle program. Independent semantic-oracle work may resume only
when all of the following are true:

1. M2 has been accepted.
2. The relevant producer and consumer interfaces exist and are exercised end
   to end.
3. A stable, versioned, user-meaningful output contract exists.
4. Persistence and replay formats are stable enough to compare.
5. The intended evaluation claim is bounded and written down.
6. Neutral expected truth is independently authorable.
7. An evaluation budget and stopping rule are accepted.
8. One small feasibility package demonstrates that the package format and
   review rules are workable.
9. A new accepted M3 evaluation plan explicitly authorizes authoring, review,
   sealing, and comparison.

The accepted M3 plan decides whether independent semantic evaluation is
proportionate and whether it is required before claiming "Trusted personal
preflight." M4 remains the public-facing MVP milestone.

## Consequences

- The active M1/M2 verification profile has six product-conformance layers and
  retains `TestCategory=Evaluation`, but removes independent semantic-oracle
  authoring, receipt, sealing, comparison, and `PASS` requirements.
- Historical package verification checks immutable bytes, hashes, manifests,
  registry bindings, reclassification, and absence of current authority. It
  never executes current product semantics or reports semantic success.
- The absence of an independent semantic verdict is an explicit deferred risk,
  not evidence that semantic reliability has been established.
- At this ADR's acceptance, the provider-analysis package then called Slice 6
  remained implementation-active and still required a separate product-
  conformance closeout. That closeout and the rest of M1 were subsequently
  accepted. This dated implementation update does not change the ADR's
  continuing deferral of independent semantic-oracle work through M2.
