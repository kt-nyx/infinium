# M1 Slice 4 protocol `/4` product-blind totality review attestation

Status: accepted

Work ID: `M1/S4.5/PRE-B2/WP4`

Reviewed input commit: `1d7e372f4c8feb9cccffbb4304910fc289e14b76`

Review date: 2026-08-06

## Disposition

**PASS.** The public protocol `/4` evidence contract and totality model are
complete, mutually exclusive, independently authorable, model-derived,
answer-isolated, and exactly representable by the frozen evaluator mechanics.
WP4 accepts the contract/model. This attestation authorizes WP5 as the next
work package; it does not perform WP5, inspect the candidate, use a private
corpus, score anything, or revise the evaluator.

## Reviewer isolation and command boundary

One fresh Codex reviewer performed the review in a newly created detached
worktree whose initial HEAD was exactly the reviewed input commit. No agent or
sub-agent was delegated. Before substantive reading, `git status
--short --branch` reported only `## HEAD (no branch)`, `git rev-parse HEAD`
matched the reviewed commit, and exact existence checks returned false for:

- `tools/evaluation/Infinium.EvaluatorV2/bin`;
- `tools/evaluation/Infinium.EvaluatorV2/obj`; and
- `work`.

The positive allowlist was the prompt's exact `AGENTS.md`, product and
architecture authorities, evaluation authorities, milestone/work-package
authorities, WP4 semantic authorities, three public validation scripts, eight
answer-free fixture files, and nine frozen evaluator `/4` mechanics/schema
files. Repository-wide discovery and directory enumeration were not used.

Exact read/validation allowlist:

- `AGENTS.md`;
- `docs/README.md`;
- `docs/product/product-definition.md`;
- `docs/product/requirements.md`;
- `docs/product/mod-impact-taxonomy.md`;
- `docs/product/workflows.md`;
- `docs/product/domain-model.md`;
- `docs/product/severity-confidence-and-coverage.md`;
- `docs/product/analysis-catalog.md`;
- `docs/product/scope-and-milestones.md`;
- `docs/architecture/overview.md`;
- `docs/architecture/data-and-trust-model.md`;
- `docs/architecture/decisions/ADR-0001-evidence-authority-boundary.md`;
- `docs/architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md`;
- `docs/architecture/decisions/ADR-0018-process-and-authority-topology.md`;
- `docs/architecture/decisions/ADR-0027-public-evaluation-protocol-private-held-out-corpus.md`;
- `docs/architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md`;
- `docs/architecture/decisions/ADR-0029-layered-evidence-and-partial-semantic-publication.md`;
- `docs/evaluation/evaluation-strategy.md`;
- `docs/evaluation/case-catalog.md`;
- `docs/evaluation/fixture-guidelines.md`;
- `docs/evaluation/anti-overfitting-rules.md`;
- `docs/evaluation/evaluator-private-fixture-governance.md`;
- `docs/evaluation/evaluator-private-fixture-governance-v2.md`;
- `docs/plans/milestones/M1-backend-semantic-proof.md`;
- `docs/plans/milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md`;
- `docs/plans/work-breakdown-notation.md`;
- `docs/plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md`;
- `docs/evaluation/specifications/m1-semantic-and-ground-truth-v2-amendment.md`;
- `docs/evaluation/m1-slice4-semantic-authority-owner-disposition.md`;
- `docs/evaluation/m1-slice4-heldout-oracle-authority-matrix.md`;
- `docs/evaluation/specifications/m1-slice4-protocol-4-evidence-contract.md`;
- `docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.schema.json`;
- `eng/validate-m1-slice4-protocol4-totality.ps1`;
- `eng/generate-m1-slice4-protocol4-state-coverage.ps1`;
- `eng/validate-m1-slice4-protocol4-authorability.ps1`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/README.md`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/execution-manifest.json`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/zero-denominator-execution-manifest.json`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/synthetic-byte-input.json`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/zero-denominator-byte-input.json`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/coverage-ledger.json`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/generated-state-coverage.json`;
- `docs/evaluation/fixtures/protocol-4-oracle-authorability/generated-state-coverage.schema.json`;
- `tools/evaluation/Infinium.EvaluatorV2/EvaluatorProtocol.cs`;
- `tools/evaluation/Infinium.EvaluatorV2/SemanticCanonicalizer.cs`;
- `tools/evaluation/Infinium.EvaluatorV2/protocol/protocol.json`;
- `tools/evaluation/Infinium.EvaluatorV2/protocol/evaluator-v2-common.v4.schema.json`;
- `tools/evaluation/Infinium.EvaluatorV2/protocol/execution-manifest.v4.schema.json`;
- `tools/evaluation/Infinium.EvaluatorV2/protocol/expected-semantic-output.v4.schema.json`;
- `tools/evaluation/Infinium.EvaluatorV2/protocol/assertion-results.v4.schema.json`;
- `tools/evaluation/Infinium.EvaluatorV2/protocol/prepared-comparison-manifest.v4.schema.json`; and
- `tools/evaluation/Infinium.EvaluatorV2/protocol/sanitized-result.v4.schema.json`.

The reviewer did not enter or enumerate `bin/`, `obj/`, `LegacyV1/`, an old
`work/`, private fixtures, the legacy archive, product source/tests, candidate
source/tests/assemblies/diffs/output, build output, or retained expected
output. The earlier oracle-authorability attestation, historical reviewer
attestations, prior WP4 transcripts, and the implementation-record closeout
were not read before judgment. Candidate behavior was never consulted.

## Frozen identities

- evaluator commit: `3693d19563c636cd2879804633ca4ce52448d2c1`
- protocol: `infinium.evaluator-v2/4`
- projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`
- frozen candidate identity (metadata only):
  `a98d648bd0adb2751ee0c09828e0227b1583950f`

The candidate commit and contents were not inspected.

## Independent pre-acceptance hashes

All values are lowercase SHA-256 computed from exact allowlisted files at the
reviewed input commit.

| Artifact | SHA-256 |
|---|---|
| evidence contract | `15762d8d59c54b5cc3576f3871c4d48d50ec8d2e56cdea0ba48de80de0a1db3c` |
| totality model | `d2bec77686d1eb2f060ce248b75ffbf5c02df5a5df62292de924d7c6257c1916` |
| totality-model schema | `8fb30b09f1a6ebce8e0414f4da7ce1ec106fcd43427d47957961891b609f7e4f` |
| WP2 validator | `9006dc11c759428ca2c76863b25e1f93e123001514579624f036b5d666c1af48` |
| WP3 generator/validator | `ec8474157e56fcd28ee8d20a9f26d67979a52e3e8c37c45b0053e4df517b46b8` |
| authorability validator | `2e1e34156cea698d3893b9da15397d8a334973edd5430a645b7bf027add68b24` |
| execution manifest | `ac9209afdd9f1ed140e50459d6f9c528d94d05c08c6012569730ecf2ac2a314c` |
| synthetic byte input | `a295c3c8d8066a76ba2df5cb0e1d820f7541a22212da90278491576a9fd76483` |
| zero-denominator manifest | `43a856d2ee7ec9b92b4fc470d6536b655ac6b1e6e518d38339c4462e34371b44` |
| zero-denominator byte input | `ae85ccb1a6f572dfafc6de0613e03aff83a72d6c0f31b0fb2250f42d9e2136f0` |
| authorability coverage ledger | `69f6c54ba892cc4f97614ae53d3af332eb740c9428b96b6ce171892164f9906f` |
| tracked generated state coverage | `4c2c5c27d37a90d5c178d5740c07de4ef2cd98730dfabaf7540cd505f9e16989` |
| generated-coverage schema | `7625d45f94aac4ff537c0d3cb2edf9d01ce9e356a2df6321e1ae4d9586986be6` |
| evaluator protocol mechanics | `982b02a1b994d9562fce0ca7dc2a55d874b96ae32b5ca261444b4c9526719d36` |
| semantic canonicalizer | `57e9eac1d5b2b6913608eeb7c303606e2f94ddf7ce39ae42b7317ae80ae85d33` |
| evaluator protocol metadata | `fcb13653d264cc691d5960995d3155c3bef1a616705a6b86af6be2794c8f8ea3` |
| evaluator common schema | `32e0bb9c0ef3e40bb301257485b0bb9d0731061d76b4768756e98347bc229c23` |
| expected-semantic-output schema | `1e10cce07ea2762e985202bdd8ba0d0af3bfe4dc82fc79fb3a7b6a7265a66257` |

## Runtime identities

- Windows PowerShell: Desktop `5.1.26100.8875`, CLR
  `4.0.30319.42000`, Windows build `10.0.26100.8875`.
- PowerShell: Core `7.6.3`, `GitCommitId=7.6.3`, Win32NT on Microsoft
  Windows `10.0.26200`.

## Commands and scratch boundary

All review outputs were freshly created under ignored `work/wp4-review/`.
Nothing was copied from a prior work directory. The substantive commands were:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/validate-m1-slice4-protocol4-totality.ps1 -ModelPath docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json -SchemaPath docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.schema.json -SummaryPath work/wp4-review/ps51/wp2-summary-proposed.json
pwsh.exe -NoProfile -File eng/validate-m1-slice4-protocol4-totality.ps1 -ModelPath docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json -SchemaPath docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.schema.json -SummaryPath work/wp4-review/ps7/wp2-summary-proposed.json

powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/generate-m1-slice4-protocol4-state-coverage.ps1 -ModelPath docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json -SchemaPath docs/evaluation/fixtures/protocol-4-oracle-authorability/generated-state-coverage.schema.json -ArtifactPath work/wp4-review/ps51/generated-state-coverage.json -SummaryPath work/wp4-review/ps51/wp3-summary-proposed.json
pwsh.exe -NoProfile -File eng/generate-m1-slice4-protocol4-state-coverage.ps1 -ModelPath docs/evaluation/specifications/m1-slice4-protocol-4-totality-model.json -SchemaPath docs/evaluation/fixtures/protocol-4-oracle-authorability/generated-state-coverage.schema.json -ArtifactPath work/wp4-review/ps7/generated-state-coverage.json -SummaryPath work/wp4-review/ps7/wp3-summary-proposed.json

powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/validate-m1-slice4-protocol4-authorability.ps1 -PackageRoot docs/evaluation/fixtures/protocol-4-oracle-authorability -ExpectedOutput work/wp4-review/expected/expected-semantic-output.v4.json -ZeroDenominatorExpectedOutput work/wp4-review/expected-zero/expected-semantic-output.v4.json
pwsh.exe -NoProfile -File eng/validate-m1-slice4-protocol4-authorability.ps1 -PackageRoot docs/evaluation/fixtures/protocol-4-oracle-authorability -ExpectedOutput work/wp4-review/expected/expected-semantic-output.v4.json -ZeroDenominatorExpectedOutput work/wp4-review/expected-zero/expected-semantic-output.v4.json
```

The two independent adversarial artifacts were validated with the WP3 script's
`-ValidateOnly -SkipSelfTests` mode. `-SkipSelfTests` was used only for these
additional adversarial copies after both complete 33-mutation runs passed.

## WP2 totality result

Both runtimes returned `passed` with semantically identical summaries:

- raw states: 23,660;
- admitted: 110;
- excluded: 6,180;
- invalid/terminal: 17,370;
- uncovered: 0;
- overlap: 0;
- families: 15;
- dimensions: 21;
- vocabularies: 17;
- constructors: 24;
- publication rules: 77;
- gaps: 8;
- authority entries: 14;
- atomic boundaries: 11; and
- explicit excluded regions: 118.

All 24 mutations were rejected on both runtimes:

`omitted-admitted-region-and-rule`, `omitted-invalid-region`,
`omitted-explicit-excluded-region`, `overlapping-admitted-regions`,
`overlapping-invalid-regions`, `admitted-excluded-overlap`,
`invalid-excluded-overlap`, `empty-catch-all-excluded-predicate`,
`unknown-invalid-atomic-boundary`, `unknown-constraint-authority`,
`duplicate-stable-id`, `overlapping-rules`,
`invalid-evidence-layer-dependency`, `unknown-dimension`,
`unknown-closed-vocabulary-value`, `unknown-constructor-reference`,
`unknown-rule-authority-reference`, `inconsistent-coverage-arithmetic`,
`missing-required-gap`, `duplicate-gap-ownership`,
`invalid-partial-race-data-arithmetic`,
`invalid-partial-race-data-assignment`,
`invalid-partial-race-data-field-publication`, and
`invalid-partial-race-data-gap-resolution`.

## WP3 generation result

Both runtimes returned `passed` with semantically identical summaries. Their
676,359-byte generated artifacts were byte-identical and shared SHA-256
`4c2c5c27d37a90d5c178d5740c07de4ef2cd98730dfabaf7540cd505f9e16989`.
That hash also exactly matched the pre-acceptance tracked artifact.

- selected state cases: 515 (110 admitted, 221 invalid, 184 excluded);
- matched negatives: 110;
- constraint mappings: 236;
- pairwise mappings: 1,713;
- rule mappings: 77;
- constructor mappings: 24; and
- uncovered required obligations: 0.

All 33 mutations were rejected on both runtimes:

`missing-state-case`, `false-rule-coverage-claim`, `unstable-case-order`,
`duplicate-rule-mapping`, `duplicate-case-id`,
`missing-constraint-mapping`, `missing-pairwise-mapping`,
`unknown-case-reference`, `broken-matched-negative`,
`answer-bearing-property`, `duplicate-gap-owner`,
`partial-race-rule-omission`, `state-digest-drift`,
`wrong-family-mapping-case`, `wrong-state-class-case`,
`wrong-disposition-case`, `wrong-constructor-case`,
`wrong-atomic-boundary-case`, `changed-lexical-inputs`,
`missing-lexical-inputs`, `changed-gap-population`,
`changed-gap-capability`, `changed-gap-scope`, `wrong-coverage-case`,
`wrong-transition-rule`, `wrong-transition-case`,
`nonexistent-partial-rule-case`, `category-coverage-drift`,
`missing-existing-mutation-id`, `extra-existing-mutation-id`,
`summary-count-drift`, `weakened-forbidden-registry`, and
`simultaneous-multi-surface-corruption`.

The generated artifact's fixed forbidden-property registry was exact, and no
answer-bearing, product, candidate, private, or expected-output path/property
was present.

## Schema and independent adversarial result

The genuine totality model and tracked generated-coverage artifact validated
against their respective Draft 2020-12 schemas. The evaluator's expected
output schema and common fact schema exactly admit the independently authored
documents: identities, result state, scalar fact types/value types, lengths,
and fact count are within their declared contracts. The public authorability
validator independently rechecked all closed vocabularies and typed values.

Two separately created ignored adversarial copies were rejected:

1. Removing `expected_facts` from the artifact-declared forbidden registry
   while adding an `expected_facts` property failed for fixed-registry drift
   and the answer-bearing property.
2. Simultaneous corruption of constructor, state-class, disposition, lexical,
   gap, coverage, transition, and higher-order partial-RACE mappings failed
   each affected integrity check and reported eight uncovered obligation
   surfaces.

## Independent authorability result

Without reading or reusing a prior expected output, the reviewer constructed
fresh expected documents solely from the allowlisted authorities, answer-free
manifests/byte ledgers, model, and evaluator canonical mechanics.

Both runtimes returned `PASS` with semantically identical summaries:

- generic expected facts: 1,124;
- zero-denominator expected facts: 42;
- duplicate fact IDs: 0;
- fixed coverage populations: 10;
- generic expected SHA-256:
  `6688e0d149b6f04c805893723d8e7295563577562df9a9d52a40c8eef7d3d306`;
- zero-denominator expected SHA-256:
  `bfe456c8fd0a04d284d1f3ec599ff5115d42d48c31c18094cdbbcf677cb37e74`.

Family counts were: result 2, plugins 15, override chains 170, NPC
contributions 148, RACE contributions 21, placed-reference contributions 64,
allowlisted fields 68, resolved NPCs 107, resolved RACE 11, resolved placed
references 27, FaceGen 51, taxonomy 352, coverage 40, gaps 24, and result gaps
24. Fact IDs were unique and in ordinal order. FormKeys, contribution and
semantic identities, URI segments, provider/path case, sequences, typed
nulls, finite numbers, taxonomy tuples, coverage rows, and gap aggregation
followed the frozen canonicalizer. All ten retained authorability mutations
were rejected by both runtimes.

## Partial `RACE/DATA` judgment

The complete cross-family path is exact:

- ten independently authorable common contribution/kind facts survive;
- `DATA` count, `face_gen_head`, and resolved-RACE facts are absent;
- no meaning is inferred from undecoded `DATA`;
- `race-records` receives one denominator and zero completion from this
  member;
- `taxonomy-subjects` receives one denominator and one completion;
- the only assignments for the partial subject are
  `technical-modification-surface / semantic-mechanism /
  surface.plugin-data` and
  `technical-modification-surface / realization-and-delivery /
  delivery.plugin-container`;
- exactly one owning gap aggregates one affected member at
  `unsupported-shapes:race:data` /
  `allowlisted-record-shape-semantics`, with scope
  `snapshot-and-result`; and
- snapshot/result projection repeats visibility but does not duplicate gap
  ownership or aggregation.

## Evaluator representation judgment

Frozen evaluator `/4` can represent every accepted state exactly. Its
canonicalizer emits stable ordinal fact IDs; typed string, integer, finite
number, boolean, and null values; explicit null versus omission; exact link
and FaceGen transport states; fixed coverage rows; singular aggregated gaps;
and sparse taxonomy tuples. Product-generated IDs are canonicalized away.
No candidate behavior or evaluator revision is needed to author an answer.

## Acceptance transition verification

Only status/provenance machinery changed. Accepted artifact hashes are:

| Artifact | Accepted SHA-256 |
|---|---|
| evidence contract | `2f66e3311d2bfac037689b2c3959c65e10d3722a6c52dfb39877354f39699845` |
| totality model | `09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5` |
| totality-model schema | `dea1801b78ecb28792580bcd080c2b8330e5356d879c57326334aa1548a64d9c` |
| WP2 validator | `87ae8f81937463427cc271184fd8e768f09864f85741e483a8cdefa658cfac8e` |
| WP3 generator/validator | `7189cba35d5b64c7d9a8aae4af3332b7db5c79365ed9c09cf7a9044ee664d5f5` |
| tracked generated state coverage | `85e6c54214dc1a73205568d6461d0c0c45d0742cd6de42b6feca87c8b9fe8714` |
| generated-coverage schema | `aa6e01caad9498f30f8ed93e56ebdb271ac7096b63da46d3e0098ecd879714bc` |

After the transition, the genuine accepted model and regenerated coverage
artifact passed their accepted-status schemas. Both runtimes reran the full
WP2 24-mutation and WP3 33-mutation suites with unchanged semantic totals and
identical summaries. Both accepted generated artifacts were byte-identical at
676,359 bytes and the tracked hash above. Authorability also reran as `PASS`
on both hosts with identical summaries.

The pre-acceptance generated artifact was normalized in memory by replacing
only `source_model.model_status` and `source_model.sha256` with their accepted
values. It then compared exactly equal to the accepted artifact. Therefore
acceptance changed no state classification, case, mapping, constructor,
disposition, normalization, coverage effect, gap ownership, outcome, or
partial `RACE/DATA` obligation.

Final disposition: **WP4 PASS and complete; contract/model accepted; WP5 is
authorized as next and remains unstarted.**
