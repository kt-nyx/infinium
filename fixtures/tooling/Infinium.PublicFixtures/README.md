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
