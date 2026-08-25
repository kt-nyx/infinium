# Product-conformance verification profile

Status: Accepted
Disposition: Active and effective
Last reviewed: 2026-08-25

Authority: [ADR-0032](../architecture/decisions/ADR-0032-defer-m1-held-out-evaluator-and-continue-public-verification.md)
and [ADR-0035](../architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md)

Applies to: ordinary product implementation and acceptance work through M2

Execution policy: [Development execution policy](../execution-policy.md)

## Plain-language purpose and claim boundary

This profile verifies that Infinium behaves according to its accepted product
contracts and safety rules while M1 and M2 build a functional product. It does
not require a separately authored semantic answer key. Passing this profile
establishes **product conformance within the delivered scope**; it does not
establish an independent semantic verdict, held-out qualification, semantic
reliability, production readiness, or M3's "Trusted personal preflight" claim.

Independent semantic-oracle work is deferred until the M2 acceptance / M3
planning boundary. Historical semantic packages have no product authority and
are checked only for immutable integrity and non-authorizing classification.
Private evaluator isolation remains default-deny.

For every accepted work package, retain the exact commands, candidate commit,
contract and fixture identities, pass/fail/skip counts, coverage and gaps,
unsupported surfaces, and fresh-review result in the owning implementation
record. Failed tests and review findings return to correction and re-review
under the execution policy; they do not authorize weaker evidence.

## Common command floor

Every implementation runs focused checks during development and this
accumulated floor once its coherent candidate is review-ready:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
```

`TestCategory=Evaluation` remains mandatory because product-behavior tests are
useful. Only independent semantic answer-key qualification is deferred. Each
owning plan adds exact commands for its contracts, fixtures, manifests,
replay, security boundaries, and end-to-end operations.

## Six required layers

### 1. Contract and schema conformance

JSON, protobuf, repository metadata, SQL constraints, typed invariants,
canonicalization, and invalid/null/unknown/omitted/unsupported states must match
accepted requirements and ADRs. Producer, consumer, persistence, wire, export,
and replay seams change together. Product output cannot define its own
contract.

### 2. Developer-owned bounded examples

Small positive, negative, malformed, lifecycle, abstention, and boundary
examples must exercise the current product contract. Their expected results
come from accepted requirements, deterministic format rules, or explicit
developer reasoning and are reviewed with the implementation. They are
conformance evidence, not independent semantic qualification. Deterministic
golden tests for exact bytes, codecs, hashes, or algorithms remain permitted.

### 3. Invalid-state, mutation, and metamorphic coverage

Tests must exercise missing, malformed, unsupported, ambiguous, partial, and
forbidden states. Meaning-preserving transformations—such as renaming opaque
identities or reordering unrelated inputs—must preserve results; changing one
relevant dependency must affect only dependent output. Raw candidates,
failures, abstentions, coverage, and gaps remain visible.

### 4. Persistence, migration, replay, and operational safety

SQL constraints, migrations, backup/restore, clean versus incremental runs,
and retained replay must preserve accepted meaning. Dependency or identity
drift fails closed. Writes remain confined. Credential, privacy, budget,
authorization, lifecycle, fault, and external-effect boundaries must pass
their applicable tests.

### 5. Controlled integration and generalization evidence

Each milestone must exercise the interfaces and representative scope it
actually delivers. M1 retains the accepted two-domain generic proof and
controlled-real obligations owned by later slices. M2 must exercise its
producer and consumer interfaces end to end. Unevaluated regions remain
explicit gaps, and product-driving cases are development evidence.

### 6. Fresh semantic, security, provenance, and diff review

A fresh reviewer checks correctness, plan fidelity, product meaning,
producer/consumer ownership, provenance, security, coverage, gaps, changed
paths, and claim wording. Passing tests do not replace this review. Findings
are classified under the execution policy and corrected on the same candidate
until accepted or genuinely escalated. The review must state that no current
semantic-oracle or private held-out verdict was used.

## Explicitly deferred requirements

M1 and M2 acceptance do **not** require:

- independent semantic-oracle authoring;
- oracle pre-seal audit;
- clean-room semantic-review or adversarial-package receipts;
- oracle sealing or registration as current validation authority;
- comparison of current product output with historical semantic labels; or
- an oracle `PASS`.

No active verification command may synthesize current product output from
historical expected fields, run the current producer or consumer against those
fields, or report historical integrity as semantic success.

## Historical semantic packages

Historical integrity checks are limited to immutable files, bytes, hashes,
manifests, registry bindings, reclassification records, and proof that no
package grants current authority. Package identities and retained effect
evidence remain unchanged. A changed historical expected answer may not be
resealed or promoted through an ordinary tooling command.

## Milestone sequencing

- M1 and its post-M1 cleanup are complete under this profile.
- M2 remains separately planned and authorized work; it must satisfy the
  applicable six layers and its eventual accepted plan.
- M2 acceptance exercises its stable product interfaces end to end and records
  the explicit absence of independent semantic qualification.
- M4 remains the public-facing MVP.

## M3 Evaluation Readiness Gate

After M2 is accepted, M3 planning may consider independent semantic evaluation
only when ADR-0035's complete prerequisites hold: exercised end-to-end
producer/consumer interfaces; a stable versioned user-meaningful output
contract; comparable persistence and replay; a bounded written claim;
independently authorable neutral truth; an accepted budget and stopping rule;
one small feasibility package; and a new accepted M3 evaluation plan explicitly
authorizing authoring, review, sealing, and comparison.

The gate authorizes planning and bounded evaluation work; it neither requires
nor guarantees a large oracle program. No future protocol or package identity
is selected here.
