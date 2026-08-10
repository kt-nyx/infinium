# Analysis pipeline cross-stage corpus v1.0.7 independent corpus review

Review date: 2026-08-10
Reviewed package: `infinium.public-fixtures.cross-stage-analysis.cross-stage.clean-incremental-replay.generic-a/1.0.7`
Reviewed registry: `infinium.public-fixtures.cross-stage-analysis.cross-stage-corpus.20260810.1/1.0.7`
Partition: `development`
Verdict: **ACCEPT**

## Repository normalization revalidation — 2026-08-10

Reviewer: `codex-independent-fixture-normalization-review-20260810`

Verdict: **ACCEPT**

The reviewer independently revalidated the functionally renamed and relocated public package after all producer, reader, authority, and accumulated-package references were resealed. The review confirmed the exact ten-file package closure, nine non-self hash-bound files, four ordered cases, eleven accumulated package registrations, answer isolation, all thirteen source-authority blobs at revision `e7de0305515657223c513195f8323b2649b6c7c8`, and the unchanged bounded semantic claim.

Final normalized freeze:

- `fixture-manifest.v1.json`: 14,217 bytes; SHA-256 `318447b1f329ab5752f0e045b9950cfd28e57cb2dc17240cdcea2dc8e635f5e8`
- ordered content aggregate: SHA-256 `5666d39defeae38cd9691fc5e6bc62ca7b99d4867e57512409e6d134f25034c7`
- status: `independently-reviewed-accepted-normalization-revalidation`

This acceptance covers repository normalization and seal integrity only. It does not broaden the package into private-held-out, reliability, readiness, safety, broad-domain, milestone-completion, or owner-acceptance evidence.

The author-frozen v1.0.7 package closes MF-06 and MF-07 without altering ordinary facts or expected semantic truth. D01 now produces, captures, and retains exact `result.001`; D02-D04 consume only that D01 result under explicit execution order, no-substitution, and deletion-fails-closed rules. Every case uses the accepted bounded Application `result-query-request` / `query-results` surface with a captured run identity and makes no field-level query claim. MF-01 through MF-07 are closed.

## Exact frozen identity and closure

The manifest is included in the ten declared paths and intentionally excluded from its recursively hash-bound nine-entry `files` array. This review externally freezes it as:

- `fixture-manifest.v1.json`: **12,447 bytes**; SHA-256 `0ec59305ac08d4b50ff6b44ff422dfd52e1b1555fd789d74785421b7832f0363`
- recomputed ordered nine-entry aggregate: SHA-256 `6f44fdd34b871cdb46339fe8763e374395142579e5381dd8c800614e48dbc5b3`
- closure: 10 declared paths, 10 files present, no extra or missing file

| Path | Bytes | Recomputed SHA-256 |
|---|---:|---|
| `expected-results.v1.json` | 4,796 | `7e6808925f7ef9029c998a5e4bd970546b4dcda6f54931a6987234dfc0dc5e36` |
| `fixture-manifest.v1.json` | 12,447 | `0ec59305ac08d4b50ff6b44ff422dfd52e1b1555fd789d74785421b7832f0363` |
| `harness-envelope.v1.json` | 18,394 | `5bb82e3a3a4980dc5c163a2c024cdc68ce641dfc0381d450e00eb2013f7592e2` |
| `ordinary-product-input.schema.json` | 7,942 | `23c9cb6aa1457535507b03089b4a2e4147bde2726bdcf50f458888c1f36f7b3f` |
| `ordinary-product-inputs.v1.json` | 7,900 | `c1a2f33d3a2e1c29fb3e222ea36c6584ac888d4aa20abef4e9db5bb71355c6a5` |
| `partition-history.v1.json` | 2,595 | `b33a96d415d07d326a3d9cb0a11ebacc4e22564ad63f6587c7ec6db10f31b445` |
| `provenance.v1.json` | 956 | `43419c96a3a6e6dab235d46c545f941201f8597197193326ade22356bb9964f8` |
| `README.md` | 4,873 | `ff83d651b06baf1298623f971945fd8fce81e3a058b5afeb488b14aa19fb02c7` |
| `redistribution.v1.json` | 609 | `e5d8869d7ed8859200b5473ece40a229f6df0ae60dbf01bae95695f1326817db` |
| `replay-dependencies.v1.json` | 2,479 | `684cfa56786d5987ccf3ac8d011eef4f1e945d29126a1782074d8acbbd433aaa` |

All nine internal lengths and hashes match current package bytes. The aggregate independently recomputes from the declared ordered `path:bytes:sha256` plus LF representation.

## Validation checklist

| Check | Result |
|---|---|
| JSON and strict ordinary-input schema | PASS. All nine JSON documents parse and the ordinary input validates. Independently constructed top-level/nested extras, missing required data, invalid mode, duplicate dependency ID, and uppercase SHA mutations are rejected. |
| Answer isolation | PASS. The product-reachable input has no answer, oracle, expected, case, EVAL, package, partition, winner, canary, or receipt metadata. Harness bindings/receipts and oracle remain isolated and fail closed. |
| Package/source evidence | PASS. Revision 1 remains 167 UTF-8 bytes at `c30f163b5ef32be392d1f046d0cf84c054e060df1f5b99e9954f86ec9763fe18`; revision 2 remains 202 bytes at `3545a682abed293ddcbec2f1525a956d9e7264500ff7f709f18b3fd1b3425c92`. Accepted passage offsets and semantic facts are unchanged. |
| Four request bindings | PASS. D01-D04 map one-to-one to `input.001`-`input.004`; mode, revision, prior-result literal, and network-off state match exactly. |
| Four oracle bindings/order | PASS. Case IDs match `/cases/0` through `/cases/3`; both per-case locations prohibit pre-observation loading. Global order selects and resolves bindings, validates/dispatches ordinary input, captures run identity, executes/publishes once, issues the bounded result query, seals observation/receipts, then loads and compares the selected oracle. |
| D01 result production/capture/retention | PASS. D01 has no prior input, produces exact `result.001`, captures it from its single atomic publication as `published-analysis-result-binding`, retains it, and reports zero hidden substitutions. The global flow names D01 as the sole producer. |
| D02-D04 exact consumption | PASS. Each consumes `result.001` from D01 using `exact-retained-identity`; the fixed execution order places D01 first. The replay record retains `prior-result.001`, names all three consumers, records zero substitutions, forbids hidden substitution, and makes deletion render D02-D04 unavailable rather than permitting replacement. |
| Coordinator/documentation stage-finding/case stage/publication | PASS. All cases require coordinator admission/completion, the authority-correct clean/reuse/recompute phase disposition, exactly one atomic publication, and zero partial publication. Changed-source invalidation and retained replay remain distinct. |
| Generic Application result query | PASS. Every case uses `surface: Application`, request `result-query-request`, response `query-results`, captured opaque run binding, bounded response, one published typed analysis result, empty field predicates, and `field_level_query_claim: none`. It makes no invented documentation-application lookup claim. |
| Human/JSON/no-effects | PASS. Each query receipt and output receipt requires human and JSON presence plus semantic equality. Network calls, provider dispatches, and external mutations are zero. |
| Registrations | PASS. All 11 package declarations resolve at exact current bytes/hashes: both documentation stage packages, candidate stage semantic/scale/stress, all four finding/case stage identities, and both operations stage partitions. |
| Ownership | PASS. The ownership block is unchanged from v1.0.6. Direct assertions remain bounded; `ANALYSIS-019`, `OPS-004`, `ADR-0017`, and `ADR-0023` remain accurate inherited-only mappings. |
| Source-authority revision | PASS. Manifest and provenance agree on `e7de0305515657223c513195f8323b2649b6c7c8`. The immutable resolution policy and all 13 frozen path/byte/SHA entries are unchanged from v1.0.6; later mutable state cannot substitute authority, and adopting later authority requires a new version/review. |
| Provenance/replay/history/redistribution | PASS. Product source/test/output/private access and product comparison are false; prohibited sources are empty. Replay closure now includes the prior-result dependency and correct deletion/substitution semantics. Partition history is append-only with every comparison flag false. Content remains project-authored synthetic, public-redistributable, non-private, and secret-free. |

## Semantic invariance

After replacing `1.0.7` with `1.0.6` and making no other transformation, these files are byte-identical to the independently reviewed v1.0.6 bodies:

- `ordinary-product-inputs.v1.json`
- `expected-results.v1.json`
- `ordinary-product-input.schema.json`
- `provenance.v1.json`
- `redistribution.v1.json`

Therefore ordinary facts, source text and offsets, documentation stage-finding/case stage truth, phase/replay expected results, product-output non-use, strict schema, source-authority revision, and redistribution truth did not change. Harness changes are limited to the explicit prior-result flow, captured run bindings, accepted generic result-query receipts, and sequencing needed to close MF-06/MF-07. Replay dependencies add the corresponding retained prior result and fail-closed deletion/substitution behavior. README, partition history, and manifest record and seal those changes.

`product_output_used`, author `product_output_access`, provenance `product_comparison_occurred`, and every partition-history comparison flag remain false. No semantic truth was derived from product output.

## Finding closure

| Finding | Status | Closure basis |
|---|---|---|
| MF-01 — answer-bearing causal conclusion | **CLOSED** | Ordinary input remains answer-free. |
| MF-02 — candidate stage/finding/case stage truth not independently derivable | **CLOSED** | Neutral facts and exact oracle are version-only unchanged. |
| MF-03 — package not exactly closed | **CLOSED** | Ten-file closure, hashes, aggregate, schema, and governance records pass. |
| MF-04 — incomplete documentation stage-operations stage accumulation | **CLOSED** | All 11 current registrations resolve exactly. |
| MF-05 — unscoped/inaccurate ownership | **CLOSED** | Accepted direct/inherited mappings are unchanged. |
| MF-06 — D02-D04 lacked an executable prior result | **CLOSED** | D01 exact production/capture/retention, ordered consumers, retained replay dependency, no substitution, and deletion failure are explicit and consistent. |
| MF-07 — unsupported field-level query | **CLOSED** | All four cases use only bounded Application `result-query-request` / `query-results`, captured run identity, typed result presence, and no field predicate or field-level claim. |

## Classified findings

### Must fix

None.

### Follow-up

None.

### Non-blocking

None.

### Owner/authority decision

None.

### Safety/isolation breach

None observed.

## Isolation attestation

I reviewed only the ten v1.0.7 author files, allowed current public authority, accepted public documentation stage-operations stage fixture handoffs, and the previously reviewed v1.0.6 public author freeze for normalized comparison. I did not inspect product source, tests, product/build output, current uncommitted product changes, any final whole-slice review report, private/evaluator-private material, legacy or historical evaluator implementation/material, Git state/diff/history, protocol `/5`, human-guide material, or live/provider material. No product comparison was performed or used.

## Verdict

**ACCEPT.** Frozen v1.0.7 satisfies exact package closure, hashing, aggregate, strict answer-free input validation, executable ordered D01-D04 bindings, exact retained prior-result semantics, post-observation oracle order, authority-correct documentation stage-finding/case stage/publication/output receipts, accepted generic bounded Application result query, 11-package accumulation, ownership, immutable source-authority provenance, replay/partition/redistribution governance, and semantic invariance. MF-01 through MF-07 are closed with no remaining finding or isolation breach.

## 2026-08-10 fixture-root relocation pin

The accepted package was subsequently moved without semantic edits from the
development-era test-data root to the consolidated `fixtures/public/` root.
Mechanical byte comparison after applying only that exact path-prefix mapping
confirmed that the manifest's 11 accumulated `authority_path` values are its
only content changes. The independently reviewed package files, expected
truth, registrations, source-authority revision, and ordered content aggregate
remain unchanged.

- exact relocated manifest length: 14,107 bytes;
- exact relocated manifest SHA-256:
  `5d1f8a4492d74c6430ebf4b26650559a1d04c2bc7b5a332bcd17cb6f3e1fbc7f`;
- unchanged content aggregate SHA-256:
  `5666d39defeae38cd9691fc5e6bc62ca7b99d4867e57512409e6d134f25034c7`.

This pin verifies relocation only and does not replace or broaden the
independent semantic verdict above.

Verdict: **ACCEPT** for the exact fixture-root relocation.

## v1.0.8 repository-cleanup normalization revalidation

Reviewer: `codex-fresh-product-blind-cross-stage-normalization-review-20260810-final`

Verdict: **ACCEPT**

Review target: `infinium.public-fixtures.cross-stage-analysis.cross-stage.clean-incremental-replay.generic-a/1.0.8`

- pending author freeze: `fixture-manifest.v1.json` 14,097 bytes; SHA-256 `7d641d09c80ce6b30db1e8c160479be8e8dece7cf2740c44d988529ae734bb7a`
- final accepted manifest: `fixture-manifest.v1.json` 14,108 bytes; SHA-256 `1bb5b29b3ad384da034032a83b6b0b65156f1c873f6d89e503aa2f954c66de6a`
- ordered nine-entry content aggregate: SHA-256 `97340e1d98cb0dd28391f63fa751026a4b084eab5e64d68a72126b33f7473a03`
- author two-run tree digest: SHA-256 `2c06eb0344b32ee7ff72d4573a383ee72a33071bb57d812107aec88387b755ad`
- closure: 10 exact package files plus this external review record; 9 non-self hash/length bindings; manifest self included only in `package_file_paths`; 11 accumulated registrations resolve at their declared current bytes, SHA-256 identities, versions, partitions, and authority paths
- registry consistency: package identity, version, partition, package path, and authority path agree; the cross-stage registry manifest hash intentionally remains at the prior accepted pin until this independent acceptance is propagated

The final freeze preserves the accepted append-only 1.0.7 event for the D01
producer and D02-D04 exact `result.001` consumers, deletion/substitution
failure, captured opaque run bindings, bounded Application
`result-query-request`/`query-results` surface, single atomic publications, and
post-observation oracle loading. The appended 1.0.8 event remains
normalization-only. The four ordinary requests and four oracle pointers remain
one-to-one. The r1/r2 source bytes, hashes, passage offsets, neutral facts,
exact counts, negative and abstention behavior, supported/lead-only grouping,
coverage gap, replay dependencies, thirteen immutable source-authority pins,
redistribution, partition, and claim boundary remain unchanged.

The three candidate-analysis registrations now resolve exactly at their
current `1.0.1` public-manifest byte and SHA-256 seals, and those exact pins
agree with the public registry. Current candidate and cross-stage functional
prose contains no legacy documentation-stage, candidate-stage,
finding/case-stage, or operations-stage wording. Older mechanical terms remain
only inside the retained historical v1.0.7 review chronology, not current
author-facing fixture authority. The immutable source-authority list remains
bound to its accepted starting revision and is not silently rewritten as
current mutable authority.

Strict parsing passed for all nine JSON files. The closed ordinary-input schema
validated the sole product projection and rejected independently introduced
top-level and nested properties, missing required data, an invalid mode, an
uppercase hash, and a duplicate dependency. The product projection contains
no case, EVAL, oracle, expected-result, package, partition, receipt, query,
review, or other harness-only metadata. All non-self file lengths and hashes,
the ordered aggregate, both retained source bodies, all claim slices, the
four-case request/oracle/receipt joins, the retained prior-result dependency,
and all accumulated registrations independently recomputed exactly. No
must-fix, follow-up, non-blocking, owner/authority-decision, or
safety/isolation finding remains.

Allowed sources used: repository `AGENTS.md`; accepted public product and
architecture documents and ADRs; the named public evaluation documents;
current JSON product schemas and public-registry schema; the public fixture
registry; all eleven files in this cross-stage directory; and only the current
retained public fixture manifests explicitly referenced by this package.

Prohibited sources not accessed: product source, tests, engineering scripts,
build or product output, Git status/diff/history/objects, private or evaluator-
private material, sibling/archive repositories, human-guide or legacy
material, live/provider material, and product execution. No product output or
implementation comparison authored or changed expected truth.
