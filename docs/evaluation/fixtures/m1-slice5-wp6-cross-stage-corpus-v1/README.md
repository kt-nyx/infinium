# M1 Slice 5 WP6 independent cross-stage corpus v1

Status: `author-frozen`

This corrected `1.0.5` public development corpus supplies four generic synthetic paths:
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
hashes. Unchanged incremental and retained replay reuse the exact WP2 checkpoint,
then recompute WP3 with the current run binding and WP4 from that closure. The r2
document changes only a non-claim formatting line: WP2 creates a new revision and
dependent checkpoint, WP3/WP4 recompute transitively, and unrelated typed facts
remain stable without claiming checkpoint or aggregate reuse. No cross-revision
semantic fingerprint equality is asserted.

The ownership audit limits direct four-case coverage to exercised facts.
`ANALYSIS-019` Bethesda analysis, `OPS-004` scale/structural-limit,
`ADR-0017` desktop/Windows-stack, and `ADR-0023` cost-ledger/budget authority
are indexed only as bounded inherited evidence and are not directly exercised.

Version `1.0.5` refreshes only the accumulated WP3 public-manifest byte and
SHA-256 seals after their mechanical closure correction. WP3 semantic oracle,
projection, and expected candidate truth bytes were unchanged; product output
did not influence this corpus truth.

Package: `infinium.m1s5.wp6.cross-stage.clean-incremental-replay.generic-a/1.0.5`

Registry: `infinium.m1s5.wp6.cross-stage-corpus.20260810.1/1.0.5`

Partition: `development`

Claim boundary: public generic synthetic local/fixture Slice 5 cross-stage
development evidence only. It is not private held-out evidence, a reliability,
readiness, safety, broad-domain, native-filesystem, M1-completion, or owner-
acceptance verdict.

Isolation attestation: the author used only the authorized public documents and
frozen WP2-WP5 fixture handoffs. No product source, tests, engineering scripts,
build/product output, Git history/diff, private/evaluator-private repository,
legacy archive, human guide, live/provider material, or product execution was
accessed.
