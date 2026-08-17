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

`LiveSemanticV2AuthorityVerifier` is the read-only R1 successor-authority
reader. It closes all five v2 packages, their 23 repository schemas, the
additive 43-row registry, the exact WP10-to-WP11 semantic join, and the
qualification/WP10/WP11 no-fourth-call composition. It never reseals fixtures,
executes product behavior, reads credentials, or performs network/provider
operations. The named `LiveSemanticV2Authority` Slice 6 gate runs its focused
positive and mutation contract suite and separately proves the five v1 package
trees plus registry v1 remain unchanged.

`reseal-live-semantic-v2.mjs` is the only resealer for this successor family.
It defaults to `--check`; explicit `--write` can update only the five v2
manifests and registry v2. Its fixed allowlist cannot write a v1 path, and its
round-trip contract test proves canonical write/check stability while retaining
registry v1 byte-for-byte.

The source-claim reader closes the registered `S6-CLAIM-DEV-v1` and
`S6-CLAIM-VAL-v1` packages. It strictly parses only their answer-free execution
input, minimized-context manifest, deterministic retained transcripts, and
separately frozen harness oracle/provenance. Product code receives no oracle
document, and these packages perform no network, credential, or source-refresh
operation. The reader verifies registry-bound manifest bytes, transitive hashes
for every non-manifest file, exact deterministic context derivation, recursive
answer isolation, typed oracle/provenance closure, and all fourteen scenario
expectation families before comparison.
