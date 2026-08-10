# Analysis pipeline cross-stage corpus independent cross-stage corpus v1

Status: `author-frozen`

This corrected `1.0.7` public development corpus supplies four generic synthetic paths:
clean execution, unchanged incremental execution, one formatting-only source
dependency revision with targeted invalidation, and complete retained replay.

Only `ordinary-product-inputs.v1.json` is product-reachable. The harness must
select one neutral `input.*` request plus `shared_facts`, validate recursively
that no harness/oracle metadata is present, and forward nothing else.
`ordinary-product-input.schema.json` is the closed harness-side pre-dispatch
validator and sets `additionalProperties: false` recursively.
`harness-envelope.v1.json` and `expected-results.v1.json` are isolated.

The package also retains logically separate provenance, replay-dependency,
redistribution, and partition-history records. The manifest's ordered
`package_file_paths` closes all ten files. Its ordered `files` array hash-binds
the nine non-self files, and `content_aggregate_sha256` deterministically hashes
their ordered `path:bytes:sha256` lines. The manifest cannot contain its own
hash; fresh independent review must freeze and report that exact external
manifest byte length and SHA-256.

The oracle freezes exact stage counts where accepted authority defines them and
uses explicit invariants/gaps instead of guessing opaque product IDs or output
hashes. Unchanged incremental and retained replay reuse the exact documentation stage checkpoint,
then recompute candidate stage with the current run binding and finding/case stage from that closure. The r2
document changes only a non-claim formatting line: documentation stage creates a new revision and
dependent checkpoint, candidate stage/finding/case stage recompute transitively, and unrelated typed facts
remain stable without claiming checkpoint or aggregate reuse. No cross-revision
semantic fingerprint equality is asserted.

The ownership audit limits direct four-case coverage to exercised facts.
`ANALYSIS-019` Bethesda analysis, `OPS-004` scale/structural-limit,
`ADR-0017` desktop/Windows-stack, and `ADR-0023` cost-ledger/budget authority
are indexed only as bounded inherited evidence and are not directly exercised.

Version `1.0.5` refreshes only the accumulated candidate stage public-manifest byte and
SHA-256 seals after their mechanical closure correction. candidate stage semantic oracle,
projection, and expected candidate truth bytes were unchanged; product output
did not influence this corpus truth.

Version `1.0.6` makes every harness case executable without oracle preloading.
Each case selects its exact `input_id`, mode, revision, and prior-result binding;
requires coordinator, documentation stage-finding/case stage, atomic publication, typed application query,
human/JSON output, no-external-effect, and oracle-timing receipts; seals the
product observation; and only then loads its selected oracle pointer. Missing,
unselected, or unexpected receipt data fails the comparison closed. All of
this metadata is harness-only and is forbidden from the product projection.

The source-authority entries resolve immutably at accepted starting revision
`e7de0305515657223c513195f8323b2649b6c7c8`. Their recorded paths, byte lengths,
and SHA-256 identities belong to that revision. Later worktree, status, or index
changes are not drift and must not replace these identities; adopting later
authority requires a newly versioned authoring and independent review cycle.

Version `1.0.7` makes the prior-result chain executable: D01 captures and
retains its singly published analysis result as exact harness binding
`result.001`; D02-D04 consume that retained identity with zero hidden
substitutions. The replay-dependency record makes deletion or substitution a
fail-closed case unavailability, never a replacement opportunity.

Each case captures its opaque run identity from the coordinator observation and
uses only the accepted bounded Application `result-query-request`/`query-results`
surface to retrieve that run's published analysis result. The response requires
a bounded typed result plus present, semantically equivalent human and JSON
projections. No documentation-field or other field-level query is claimed.

Package: `infinium.public-fixtures.cross-stage-analysis.cross-stage.clean-incremental-replay.generic-a/1.0.7`

Registry: `infinium.public-fixtures.cross-stage-analysis.cross-stage-corpus.20260810.1/1.0.7`

Partition: `development`

Claim boundary: public generic synthetic local/fixture cross-stage analysis
development evidence only. It is not private held-out evidence, a reliability,
readiness, safety, broad-domain, native-filesystem, milestone-completion, or owner-
acceptance verdict.

Isolation attestation: the author used only the authorized public documents and
frozen documentation stage-operations stage fixture handoffs. No product source, tests, engineering scripts,
build/product output, Git history/diff, private/evaluator-private repository,
legacy archive, human guide, live/provider material, or product execution was
accessed.
