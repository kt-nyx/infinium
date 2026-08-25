# M1 Slice 9 implementation and M1 completion record

Status: Completed

Disposition: WP1 through WP7 are implemented and the complete accepted
verification floor passes on the exact product candidate below. This record
requests an owner decision; it does not self-accept Slice 9 or M1.

Last reviewed: 2026-08-25
Owner: Project owner

## Plain-language result

Slice 9 now joins the already accepted M1 parts into one durable end-to-end
path. The shipped CLI can start, wait for, and read both the developer-owned
synthetic composition and the exact authorized controlled-real composition.
The same validated run output drives the human and JSON views. Clean,
incremental, and retained-downstream executions preserve the same typed
meaning, while provenance still distinguishes historical provider involvement
from the current effect-free run.

All implementation, focused checks, consolidated review, corrections,
re-review, and the complete verification floor passed on one committed product
candidate. The 34 required M1 cases are bound to that candidate in the final
result index. No new product schema, analyzer meaning, migration, or runtime
effect authority was introduced.

This result is deliberately narrow. It shows public product conformance for
the accepted synthetic and controlled surfaces. It is not an independent
semantic verdict, a broad Skyrim compatibility result, a runtime-safety result,
or a production-readiness claim.

## Authority and exact candidate

- accepted planning candidate:
  `1dd5419ebb3dea8893f7e45adbe16191cf0e823c`;
- accepted implementation base:
  `ce51f2d7fdd9d74083ca8c83f686b1193e867ff0`;
- exact activation handoff:
  `264c79c37e6c14f24f243749cdea6e9c47bb1ce1`;
- activation parent: the implementation base above;
- product candidate:
  `926080092e056973d254562424a030672fb4d917`;
- product tree: `fb9b9679b458f78dd3ef4d5f31dfdc3c059670f9`;
- implementation branch: `codex/m1-s9-implementation`;
- product-candidate worktree state for the accepted floor: clean;
- review-ready documentation-only handoff: the commit containing this record,
  the sanitized receipts, final result index, and compact navigation update;
  its exact identity is reported to the owner after it is created because a
  commit cannot contain its own identity.

The product candidate is the activation handoff's direct child. No merge or
push occurred. M2 remains inactive.

## Predecessors and frozen-boundary result

The accepted Slice 8 product remains
`c79661cd8eb016e483fa8b7396e7d4997b85d590`, tree
`fd706b21b51e4009cf02e338ef52fbc2fe3eb937`. Slice 9 preserved the accepted
Slice 5 through Slice 8 product contracts, including frozen
`infinium.run-output/v1`, the Slice 7 and Slice 8 scope-reversion contracts,
storage `1.10.0`, and schema 11.

WP1 confirmed that these contracts express the accumulated path truthfully.
No schema, protobuf, enum, analyzer declaration, migration, persistence
version, or frozen-field meaning changed. Changed producer and consumer seams
were exercised together through composition, persistence, round-trip,
invalid-state, CLI, replay, and focused conformance evidence.

The frozen-boundary inventory is
[evidence/frozen-boundary-inventory.v1.json](evidence/frozen-boundary-inventory.v1.json).
The exact stage-to-output and equivalence classification is
[evidence/composition-design.v1.json](evidence/composition-design.v1.json).

## Controlled-real admission

The authorized Slice 8 source handoff was used read-only only after the Slice 9
verifier accepted its containment and identity. Its absolute root and payload
bytes remain untracked.

- handoff ID: `m1-slice8-research0035-local-v1`;
- local manifest SHA-256:
  `8972ef0e160b9de04da281d48639b66d8bffcc153504c1d699f654f1eff6ecf5`;
- manifest bytes: 5,978;
- allowlisted controlled inputs: 26;
- admitted controlled bytes: 766,104,776;
- tracked public manifests: 3;
- missing, extra, hash-drifted, case-colliding, escaping, or reparsed entries:
  zero.

The three public manifest identities are retained in the final Slice 8
receipt. The source handoff was reparsed only through the bounded admission
path. The separately retained sanitized Slice 8 result remained exact at
10,553 bytes and SHA-256
`23d20c4646d14ece1ba209043c6de94da2f87c68b5c869e4c6169adb4a01f633`.
The source root and retained downstream state were unchanged after execution.

The maintainer-local Slice 6 checkpoint required by accepted historical replay
tests was also supplied read-only through
`INFINIUM_M1_SLICE6_RETAINED_PRODUCT_ROOT`. Its absolute path is deliberately
not tracked. Normal product callers retain repository-bound behavior; only the
internal test seam accepts the explicit retained root.

## WP1 through WP7 outcome

### WP1 — inventory and composition design

WP1 produced the exact frozen-boundary inventory, the stage-to-output mapping,
the versioned semantic-equivalence field classification, the 34-row repository
result-index schema and preregistration, and exact synthetic and controlled
admission identities. It confirmed that schema 11 and frozen run-output v1 are
sufficient without reinterpretation.

The exact canonical composition-envelope SHA-256 values are:

- synthetic:
  `cc48ef713282d7060a0dd9560972f2e16235e52c4147d6f5c9c4db31cd1fabb1`;
- controlled-real:
  `02d33986cd28326074cc7889f8949716cd961e630ebb82f139b0d327af135b77`.

Admission accepts only those exact canonical envelopes. A different envelope
cannot gain authority merely by satisfying the structural schema.

### WP2 — durable accumulated composition

WP2 added an optional coordinator-owned composition request to the existing
managed analysis path. It maps already retained local observation, matched
control, historical provider proposal, host admission, scope decision,
hypothesis, finding, supported case, recommendation, gap, taxonomy, coverage,
and partition facts into frozen run-output v1 collections.

The coordinator validates the exact envelope before work, publishes through
the existing atomic schema-11 path, and reopens the canonical result through
the existing query boundary. Historical producer and originating-run identities
remain intact; the current run contributes only a consuming-run provenance
edge. The durable operation request has a separate closed 896 KiB maximum;
ordinary 64 KiB checkpoint bounds remain unchanged.

### WP3 — synthetic CLI composition

The tracked package is `M1-S9-SYNTHETIC-v1`. Its 1,775-byte manifest has
SHA-256
`b14a50bf341d467c5922c7a9be200a5f61ef974c4dfb95d656c5701ee2220ac6`.
The final clean CLI run ID is `run-managed-cli`; start, wait, human results,
and JSON results all succeeded. Its output retains a supported observation, a
resolved negative, one retained historical model proposal and host admission,
an abstention, an explicit gap, and an unsupported lead. Every current
provider/model/credential/DNS/network/billable/live/source-refresh boundary is
`not-used`.

The same package also passed incremental and retained-history checks. Human
output embeds the exact canonical JSON used by the machine view.

### WP4 — controlled-real CLI composition

The final clean controlled run ID is `run-managed-cli-controlled`, package
identity `M1-S9-CONTROLLED-REAL-v1`. It preserves:

- four candidate decisions and four hypotheses;
- two findings, two supported cases, and two recommendations;
- fourteen explicit coverage gaps;
- two matched controls as resolved negatives; and
- one retained historical model proposal and its host admission.

The incremental run ID is `run-managed-cli-controlled-incremental`; the
retained-downstream run ID is `run-managed-cli-controlled-replay`. Both are
equivalent to the clean run under
`infinium.m1-slice9-semantic-equivalence-projection/v1`. The comparator keeps
typed provenance and taxonomy relationships semantic while normalizing only
declared run-instance identities.

The controlled request fits the closed 896 KiB operation-request bound. The
case evidence and result index retain only identities, hashes, counts,
dispositions, bounded claims, and public references; they contain no controlled
payload bytes or local absolute source root.

### WP5 — replay, lifecycle, output, and safety

Focused evidence covers clean, incremental, and retained-downstream replay;
relevant and unrelated dependency changes; malformed, missing, substituted,
and drifted envelopes; persistence and exact readback; terminal lifecycle and
stale publication; human/JSON equality; protected/source/controlled-root
non-mutation; canary scanning; and process cleanup.

The exact composition is replayable without current provider dispatch or
debit. Every forbidden effect count in the final case receipt is zero. The
final process-survivor count is zero.

### WP6 — required cases and six-layer conformance

[evidence/required-case-results.v1.json](evidence/required-case-results.v1.json)
is the final exact 34-row index. Every row is matched once, passes once, has
zero failures and skips, names its owning specification and gate, distinguishes
final execution from retained historical observation where relevant, and
binds the candidate and canonical 3,450-byte tracked case receipt.

The canonical tracked index is 40,425 bytes with SHA-256
`446225cd79cc85ab58f086dd3e1146c62fe683ffbdb4784623e10ecc3022afc0`.
It covers the accepted six layers: contract/schema, developer-owned behavior,
mutation/metamorphic behavior, persistence/replay, integration/safety, and
fresh consolidated review.

### WP7 — review, correction, floor, and handoff

WP7 reviewed the full affected vertical path for frozen-contract preservation,
semantics, taxonomy, provenance, coverage, replay, lifecycle, security,
isolation, output truthfulness, repository authority, and diff quality.
Corrections remained on the same mutable product candidate. Focused checks and
changed-surface re-review passed before the final candidate was committed and
the complete floor restarted.

## Consolidated review and correction ledger

| Classification | Finding | Same-candidate correction and re-review |
| --- | --- | --- |
| Must-fix | A v2 hypothesis state could be projected implicitly. | Mapped it explicitly and added exact assertions. |
| Must-fix | Controlled taxonomy subject type and ID were reversed. | Corrected the projection and asserted category-neutral subject types and stable IDs. |
| Must-fix | The controlled request exceeded the ordinary 64 KiB checkpoint bound. | Added a separate closed 896 KiB durable operation-request bound; checkpoint limits did not change. |
| Must-fix | Nested controlled-input enumeration could follow a reparse boundary before rejection. | Added the containment pre-scan and exact file-set validation before payload reads. |
| Must-fix | The first equivalence comparator normalized too broadly and checked some relationships only by count. | Added the versioned typed projection and exact provenance/taxonomy relationship comparison. |
| Must-fix | The repository result schema did not close all 34 positional rows. | Added `prefixItems` support to the repository validator and an exact closed tuple schema. |
| Must-fix | Any structurally valid composition envelope could have been admitted. | Bound admission to the exact canonical synthetic or controlled envelope SHA-256. |
| Must-fix | The Slice 9 verifier originally conflated the source handoff manifest with retained sanitized Slice 8 output. | Added separate exact preflights and retained-result identity checks. |
| Must-fix | One controlled test embedded a machine-specific absolute retained-output path. | Replaced it with a deterministic temporary-root derivation. |
| Must-fix | Historical Slice 6 replay tests assumed their retained checkpoint still lived in the active worktree. | Added an internal read-only test parameter and reused the established maintainer-local environment binding without changing normal callers. |
| Must-fix | A proposed registry bump would have changed a frozen public fixture registry. | Reverted it; the synthetic wrapper grants no new runtime fixture authority. |
| Must-fix | Baseline formatting and current-state assertions were stale for the activated Slice 9 candidate. | Applied only required formatting/coherence corrections and revalidated the entire floor. |
| Non-blocking | Runtime appearance/gameplay, quest/global/save safety, archive-wide completeness, broad compatibility, and M3 scale/performance remain unmeasured. | Retained as explicit gaps and excluded from the claim. |

No must-fix, authority conflict, safety/isolation breach, or unexplained
mandatory-evidence gap remains after re-review.

### Diagnostic attempts not bound as final evidence

The final evidence binds only the clean passing candidate. Earlier diagnostic
attempts were retained in this chronology, not promoted:

1. Evaluation correctly stopped on an empty stale unaccepted public-fixture
   directory. Read-only inspection proved it contained zero files and zero
   subdirectories; the exact empty directory was removed and the active
   deferral check passed.
2. The first unfiltered run exposed the missing maintainer-local Slice 6 replay
   seam. The same candidate was corrected, the three affected tests passed,
   and the product commit was amended before restarting the entire floor.
3. The first pipeline-harness invocation omitted that environment binding.
   The harness was restarted from a fresh root with the exact read-only binding
   and passed `Gate All`.

## Complete verification floor

The Section 9.2 floor passed from a clean worktree on product commit
`926080092e056973d254562424a030672fb4d917`.

- locked restore: passed;
- Release build: passed, 0 warnings and 0 errors;
- Unit category: 303 passed, 1 skipped, 0 failed;
- Contract category: 184 passed, 0 skipped, 0 failed;
- Integration category: 194 passed, 1 skipped, 0 failed;
- Evaluation category: 91 passed, 9 skipped, 0 failed;
- Security category: 185 passed, 3 skipped, 0 failed;
- Fault category: 119 passed, 3 skipped, 0 failed;
- unfiltered projects: Security 22/0, Fault 10/0, Evaluation 70/9,
  Unit 338/1, Contract 209/0, Integration 242/1 (passed/skipped), all with
  zero failures;
- formatting verification: passed;
- dependency manifest check: passed;
- documentation validation: passed for 189 metadata files, 191 Markdown link
  sources, and 91 JSON files;
- `eng/verify-analysis-pipeline.ps1 -Gate All`: passed;
- `eng/verify-m1-slice8.ps1`: 37 passed, 0 skipped, 0 failed;
- `eng/verify-m1-slice9.ps1 -Gate All`: 12 passed, 0 skipped, 0 failed;
- `git diff --check`: passed;
- final product-candidate `git status --short`: empty;
- repository-owned process survivors: zero.

The skips are declared predecessor/environment cases: unavailable private
runtime/MO2/game/profile/protected-root inputs, an optional legacy controlled
root, mutation/race cases requiring unavailable exact host conditions, the
Windows symlink-capability case, and one optional materialized historical
campaign replay. No mandatory Slice 9 test or required-case row skipped.

## Final receipts

| Evidence | Bytes | SHA-256 |
| --- | ---: | --- |
| Analysis pipeline `all.json` | 764 | `bb3a052b29cf0260e7867229997c04856b330bce860ea48fe47cdd6f6c128164` |
| Slice 8 verification receipt | 4,005 | `ebb05bc98961d9f87619a7a1d707b983f9aa786c1465286087387dabda04f34b` |
| Slice 8 controlled results | 10,553 | `23d20c4646d14ece1ba209043c6de94da2f87c68b5c869e4c6169adb4a01f633` |
| [Slice 9 case evidence](evidence/case-evidence-receipt.json) | 3,450 | `c98aafcc18b546ec5d94d24afe4dce7147bca4139092a2b3871b9b0a93da9966` |
| [Slice 9 verification receipt](evidence/slice9-verification-receipt.json) | 3,688 | `db1a07a1a3728a75278137116f658d0cc2fc53ec7133efde34524dec1e7672ec` |
| [Final required-case index](evidence/required-case-results.v1.json) | 40,425 | `446225cd79cc85ab58f086dd3e1146c62fe683ffbdb4784623e10ecc3022afc0` |

The verifier-generated Windows copies were 3,547, 3,786, and 41,553 bytes with
SHA-256 values `5f2e15adcebe3adb6d61d76134f5925301bbe5e1d3753705c623188dd7c793ef`,
`59b4519669f9e427630a86854a9bb2f94063b66bc6dc1619b40ae09d41f42ae1`,
and `ed4e827447dd4366209f5df409ea7f452d737090dbcce2b818811a5c1a1cc684`.
The documentation-only handoff canonicalizes those JSON documents to the
repository's required LF line endings; the tracked identities are the ones in
the table. The result-index rows bind the canonical tracked case receipt.

The temporary candidate-bound roots were fresh and outside the repository.
Their absolute locations are not durable authority; the hashes and sanitized
tracked receipts above are the durable handoff evidence.

## Bounded claim and retained gaps

The supported statement is: on the exact candidate, the accepted accumulated
coordinator path publishes frozen run-output v1 for the developer-owned
synthetic package and exact authorized Slice 8 controlled composition; the
human and JSON views agree; clean, incremental, and retained-downstream runs
are equivalent under the declared typed projection; historical provider
provenance remains visible; and every current forbidden effect remains unused.

The full review-ready claim inventory is
[evidence/claim-inventory.v1.json](evidence/claim-inventory.v1.json). The result
does not establish runtime appearance or gameplay, quest/global/progression or
save safety, archive-wide completeness, patch-wide correctness, broad
compatibility, reliability, readiness, precision/recall, M3 scale or
performance, provider repeatability, or account-wide billing facts.

No private fixture, evaluator repository, evaluator archive, legacy archive,
credential, provider, DNS/network, billable effect, external publication,
semantic oracle, independent comparison, merge, or push was used. No product
output authored expected truth.

## Owner decision requested

The requested decision is to accept, reject, or amend exact product candidate
`926080092e056973d254562424a030672fb4d917` together with the documentation-only
handoff containing this record and its sanitized evidence. Acceptance may mark
Slice 9 and M1 complete only for the bounded public product-conformance claim.
It does not activate M2, create an independent semantic verdict, reopen any
external-effect authority, or authorize merge/push unless the owner states
those permissions separately.
