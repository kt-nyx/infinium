# M1 Slice 4.5 authority-completion product-conformance mismatch

Status: Terminal public conformance blocker; awaiting project-owner disposition  
Recorded: 2026-08-05  
Protocol: `infinium.evaluator-v2/4`  
Projection: `infinium.evaluator-v2.slice4-semantic-projection/3.0.0`

## Disposition

The owner-authorized public authority-completion attempt proved that a fresh,
product-blind reviewer could independently author all fifteen active projected
fact families. The subsequent, separately gated product-conformance review
found that the unchanged frozen candidate does not conform to that independent
specification.

This is the required stop condition. The proposed specification is not
accepted or frozen, no authority-completion freeze exists, private B2 must not
resume, and no product or evaluator change is authorized by this record. The
project owner must disposition the mismatch through the milestone plan.

## Frozen identities checked

- public starting commit and `origin/main`:
  `8e16b47eec626f74a68ad77d8d4c4e53abb05349`;
- unchanged candidate commit:
  `98fe8a5a173116427bf78077673fd10e8d018103`;
- unchanged candidate `Infinium.Bethesda.dll` identity already frozen by Stage
  A: 157,696 bytes, SHA-256
  `dc8ae44627fa40ca3937e4022c8e7914468e4d7a4cf1c40797a22ef2abec3655`;
- unchanged evaluator commit:
  `3693d19563c636cd2879804633ca4ce52448d2c1`;
- scorer and adapter: `4.0.0`; and
- protocol `/4` projection: `3.0.0`.

The diff from the candidate to the starting public commit is empty under
`src/Infinium.Bethesda`. The diff from the evaluator freeze to the starting
commit changes only the evaluator README under the evaluator root; evaluator
code, schemas, adapter, canonicalizer, and comparison behavior are unchanged.

## Independent authority and rehearsal identities

The proposed oracle-construction specification was authored before product
source, product tests, evaluator runtime, or product output was made available
to either authoring role. It is retained as a non-normative review artifact:

- proposed specification: 55,245 bytes, SHA-256
  `5aaadda10c92cf427dac2efa393783945ac110173d0901e2c7570fb0c378c492`;
- specification-author attestation: 4,550 bytes, SHA-256
  `532cd6312a68c3ce929ff41c21c73b41d0490628abaf4ace48f80b15183386d3`;
- answer-free rehearsal input: 5,000 bytes, SHA-256
  `f2ec7b97a218cef52aaa567306fd31a8bbf82bda9921a78b4600c6782a830ec5`;
- independent synthetic byte report: 11,800 bytes, SHA-256
  `55957824463916cabbc9b1d627dcdc038ba6964608da64756475e34aea6581ba`;
- authorability audit: 13,501 bytes, SHA-256
  `e0b7de3858679a27c2ec6328e982cf0dc240d3d7085887de52d8303b86d1063e`;
  and
- fresh reviewer attestation: 5,086 bytes, SHA-256
  `636c7ef73ba271ef2a9a4083bc139b9bf6bfe2619497b21a1b702570a5af4d66`.

The product-blind rehearsal produced five schema-contract-valid outputs. Its
rich member contained 5,793 sorted facts with no duplicate fact ID, 44
taxonomy subjects, 440 taxonomy tuples, all ten specified coverage rows, and
seven specified snapshot-gap tuples. The reviewer affirmatively covered all
fifteen active families and reported no unresolved authority gap or
contamination. Parent identity, sorting, duplicate, coverage, gap, terminal,
and FaceGen samples independently passed. These results establish
authorability only; they do not establish product conformance.

## Exact conformance mismatches

The mismatch is semantic and multi-surface, not a serialization-only spelling
issue.

| Surface | Independent proposed rule | Frozen candidate behavior | Projected consequence |
|---|---|---|---|
| Allowed-field boundary | The closed NPC/RACE/REFR field sets exclude `EDID`; an observed `EDID` is an unsupported-field occurrence. | `AllowedSemanticFields` includes `EDID` for all three families. `SemanticCanonicalizer.ProjectFields` projects every resulting allowlisted-field entry. | Candidate output can contain `allowlisted_fields/...:edid` facts where the independent oracle requires no such fact and an exact unsupported-field gap. |
| FaceGen precedence | After deleted and indeterminate-template handling, trait templating precedes race indeterminacy. `uses_template` without trait templating does not itself make FaceGen inapplicable. | `DetermineFaceGenApplicability` tests unresolved race before template state and returns `CoverageGapTemplateSource` whenever `usesTemplate` is true; it has no independent `indeterminate_template` branch. | Applicability values and gap presence differ for template and race combinations, even when the same retained NPC/RACE facts are supplied. |
| Loose FaceGen absence | A present loose provider requires `exact_absence_known=false`. A missing chain is exact only from byte-verified exhaustive loose-index authority; archive support is a separate runtime-wide gap. | `LooseAsset` returns `exact_absence_known=true` for a present chain and uses `ArchiveMemberPopulationSupported` as the boolean for a missing chain. | Both present and absent asset facts can disagree, and the archive-gap boundary is different. |
| Coverage registry | Every published snapshot emits exactly ten fixed rows, including zero denominators, with separate NPC, RACE, REFR, unsupported-record, FaceGen, localized, discovery, plugin, and taxonomy populations. | `BuildCoverage` starts with only `plugins` and `allowlisted_records`, then adds one row per observed gap population. | Row identities, row count, denominators, completed counts, and states cannot match the independent oracle. |
| Gap registry | Unsupported records, fields, and shapes use exact per-signature/per-field populations and capabilities; unsupported capabilities use the specification's underscore vocabulary and independently counted denominators. | `BuildGaps` aggregates records as `record_family`, fields as `record_field`, and uses candidate-specific capability strings such as `archive-activation-and-member-precedence`. It has no proposed per-shape projection. | Gap fact IDs, populations, missing-capability values, aggregation, and denominators differ. |
| Taxonomy | Each closed subject emits a mandatory ten-tuple matrix using axes/facets such as `technical-surface/mechanism`; record-semantic subjects are created from the closed semantic-key vocabulary. | `BuildTaxonomy` emits a bounded subset with existing taxonomy axes such as `technical-modification-surface/semantic-mechanism`; semantic subjects are area-derived, and provider topology is emitted only for more than one enabled plugin. | Subject identities, tuple counts, axes, facets, applicability/role combinations, and zero/single-plugin behavior differ. |

The candidate contracts also require scalar NPC and RACE values that the
proposed well-framed-unsupported-shape branches intentionally omit. That
structural difference reinforces the mismatches above but is not needed to
establish the stop condition.

## Conformance checks run

- exact starting HEAD and remote identity verification;
- empty candidate-to-starting-HEAD diff under `src/Infinium.Bethesda`;
- evaluator-freeze comparison confirming no code/schema/projection change;
- source-level review of `BethesdaSemanticContracts.cs`,
  `BethesdaSemanticExtractor.cs`, and the frozen semantic canonicalizer;
- public protocol/conformance test review; and
- focused public evaluator test execution:
  `dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --nologo --filter "FullyQualifiedName~EvaluatorV2PublicProtocolTests"`:
  12 passed, 1 expected platform-capability skip, 0 failed.

Passing existing tests confirms the frozen behavior; it does not resolve the
independent normative mismatch.

## Preserved boundaries

- the specification was not changed after product inspection;
- the frozen candidate was not changed;
- evaluator code, schemas, canonicalization, projection, and comparison were
  not changed;
- candidate output was not used as oracle truth;
- no private expected output or predecessor oracle was used;
- no private reviewer attempt 2 was launched;
- no `adapt`, `score`, or `score-corpus` command ran;
- the candidate was not executed against private inputs;
- Stage C2 and Stage D did not run; and
- corpus `infinium.m1.slice4.heldout/2.0.0` was not created, frozen, tagged, or
  pushed.

