# Current public fixture contracts

`Infinium.PublicFixtures` is the current public-fixture contract reader used by
product contract and evaluation tests. It validates project-authored public
fixture packages and assertion results against the active product schemas in
`contracts/json-schema/`.

The documentation fixture reader additionally closes the two independently
authored `DOC-CLAIM-CORE-DEV` and `DOC-CLAIM-ADVERSARIAL-VAL` packages, verifies
their exact retained UTF-8 source bytes and dependency fingerprints, and
admits their claim-import manifests through the active product validator.

This library is not an evaluator protocol, scorer, oracle authority, held-out
workflow, or product runtime dependency. It does not reference archived
evaluator code or schemas, private fixture material, or candidate/product
output. Public product-fixture tests reference this project directly.

`HistoricalLiveSemanticPackageVerifier` checks only current deferral metadata,
historical registry/reclassification bindings, and immutable file bytes and
hashes. It does not interpret expected semantic labels, execute a current
producer or consumer, synthesize product output, or report semantic success.
The `HistoricalSemanticPackageIntegrity` check runs this read-only historical
boundary validation.

`reseal-live-semantic-v2.mjs` is retained as a check-only historical-integrity
command. It accepts only `--check`; `--write` fails. There is no supported path
that can reseal changed historical answers or promote them to current
validation authority.

The registered `S6-CLAIM-DEV-v1` and `S6-CLAIM-VAL-v1` packages are retained as
historical development evidence. Their answer-bearing oracle and provenance
bytes remain audit-visible but are not loaded by a current product acceptance
gate and grant no semantic authority. Current source-claim verification uses
developer-authored product conformance tests plus the separate historical-byte
integrity gate; neither produces an independent semantic verdict or performs a
network, credential, or source-refresh operation.
