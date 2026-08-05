# Infinium public evaluator v2 successor

This standalone public tool implements protocol `infinium.evaluator-v2/4`,
scorer and adapter `4.0.0`, and the final bounded Slice 4 projection
`infinium.evaluator-v2.slice4-semantic-projection` `3.0.0`. Protocol `/3` at
evaluator commit `34ed0c84165e9a49f44a88ecd87cac967132ebd7` remains immutable
historical evidence: it was publicly qualified, then superseded before a valid
successor corpus because its projection required non-independently-authorable
oracle values.

## Projection contract

The reflection adapter serializes the public `BethesdaSemanticExtractionResult`
only as an inter-assembly transport, then explicitly projects named Slice 4
members. It never recursively flattens the complete result. Included facts are
result publication/failure presence; accepted plugin and provider topology;
evaluator-owned record and contribution identity; override sequence and
semantic winner fields; selected NPC, RACE, and REFR fields; typed links;
allowlisted-field counts; FaceGen/provider topology; evaluator-owned taxonomy
tuples; coverage; and typed capability gaps.

Excluded values include every product-generated contribution, participant,
winner, taxonomy-assignment, analyzer, evidence, gap, and snapshot ID; exact
failure-code spelling; typed AIDT subfields; denominator labels; physical
paths; `SnapshotAuthorizedPath`; dependency fingerprints; separately bound
producer/candidate metadata; timestamps; exception text; `Reason` or `Message`
prose; display text; manifest-redundant byte hashes/lengths; and incidental
serialization fields. The normative family-by-family authority is
`docs/evaluation/m1-slice4-heldout-oracle-authority-matrix.md`.

Fact IDs use slash-delimited public collection/field names. Set/map identities
are percent-escaped path segments sorted ordinally. Ordered plugin, master,
contribution, link-ordinal, and provider sequences use zero-padded ordinal
indexes. Duplicate identities are invalid. FormKeys are canonicalized from the
Slice 4 ID-first representation to `xxxxxxxx:plugin.ext`. Explicit null and a
missing fact differ. Missing and extra facts are product mismatches. Typed-fact
validation follows the declared semantic `value_type`: `integer` requires an
exactly representable signed Int64 JSON number (including integral decimal or
exponent notation), while `number` accepts any finite JSON
number, including an integral-valued token. Semantic numbers compare
numerically, so `10` and `10.0` are equal when both declare `number`; semantic
integers remain exact and type-distinct.

Aggregate corpus fingerprints bind each canonical member ID in declared order,
along with its execution inputs and oracle identity. Renaming a member without
refreezing the corpus is an admission error.

## Commands

Build once, then use the frozen tool output directory as `EVALUATOR_ROOT`.
Every result directory must be a new path below an existing, reparse-free
parent.

```text
Infinium.EvaluatorV2 protocol
Infinium.EvaluatorV2 calibrate --result-dir <new-directory>
Infinium.EvaluatorV2 adapt --manifest <execution-manifest.json> --result-dir <new-directory>
Infinium.EvaluatorV2 score --manifest <execution-manifest.json> --oracle <expected-output.json> --result-dir <new-directory>
Infinium.EvaluatorV2 compare-prepared --manifest <prepared-comparison-manifest.json> --candidate-output <prepared-candidate-output.json> --oracle <expected-output.json> --result-dir <new-directory>
Infinium.EvaluatorV2 score-corpus --manifest <corpus-execution-manifest.json> --result-dir <new-directory>
```

Stage B uses `compare-prepared`. It loads no candidate assembly and invokes no
reflection adapter. An answer-free public example may identify prepared output
with a synthetic 40-hex commit, its actual prepared JSON artifact length/hash,
and a `qualification_id` such as
`stage-b-independent-oracle-qualification`; that identity explicitly is not
the frozen product candidate. The prepared manifest corpus hash binds that
qualification ID plus the candidate-output and oracle bytes.

Stage C uses exactly one `score-corpus` command. Its manifest contains one or
more private members, each with its own answer-free `execution`, `oracle_path`,
and private `member_id`. The scorer emits private ordinal assertion files and
one sanitized aggregate. Member IDs and local paths never enter the sanitized
result. A one-member corpus is valid.

Loose-provider chains name a normalized relative path, ordered providers,
provider kind/priority, and exact winner. A retained provider asset supplies
all of path, byte length, and SHA-256; all three are omitted together when no
retained file is required. The manifest separately states whether archive
member population is authoritative. The adapter constructs additional local
installed entities and preserves the declared provider order and winner.

## Terminal boundary

Before candidate invocation, malformed tuple/manifest/oracle data, mismatched
retained bytes, evaluator identity/dependency drift, candidate admission, and
result publication are `EVALUATOR_ERROR`. After valid admission, a resolved
candidate invocation that throws is `FAIL/candidate_execution`; an invalid
candidate projection is `FAIL/candidate_output_contract`; semantic mismatches
are `FAIL/comparison`. A valid typed failed state compares normally.

After closeout, the tracked public freeze handoff at
`docs/evaluation/evaluator-v2-stage-a-final-bounded-freeze.json` is the sole
Stage B.2 autodiscovery authority. Stage B.2 must not substitute a SHA supplied
out of band, and no further successor iteration is authorized.
