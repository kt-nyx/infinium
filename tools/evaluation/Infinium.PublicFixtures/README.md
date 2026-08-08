# Current public fixture contracts

`Infinium.PublicFixtures` is the current public-fixture contract reader used by
product contract and evaluation tests. It validates project-authored public
fixture packages and assertion results against the active product schemas in
`contracts/json-schema/`.

The WP2 documentation reader additionally closes the two independently
authored `DOC-WP2-CORE-DEV` and `DOC-WP2-ADVERSARIAL-VAL` packages, verifies
their exact retained UTF-8 source bytes and dependency fingerprints, and
admits their claim-import manifests through the active product validator.

This library is not an evaluator protocol, scorer, oracle authority, held-out
workflow, or product runtime dependency. It does not reference
`Infinium.EvaluatorV2`, its frozen `/4` protocol schemas, predecessor `/3`
schemas, private fixture material, or candidate/product output.

The historical protocol `/4` boundary remains under
`tools/evaluation/Infinium.EvaluatorV2/` and may be invoked only through
`eng/invoke-m1-slice4-protocol4-bounded-regression.ps1`. Public product-fixture
tests must reference this project directly instead of importing fixture code
through the frozen evaluator assembly.
