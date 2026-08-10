# Analysis pipeline operations stage product-blind operational fixtures v1.0.2

Status: `independently reviewed; comparison complete 12/12; explicit native capability gaps`

Registry: `infinium.public-fixtures.analysis-operations.operational-cases.20260809.3`

Independent review:
[`codex-product-blind-operations-v1.0.2-review-20260809`](independent-review.md),
accepted at `2026-08-09T21:19:29.481Z`; sanitized native-capability correction
recorded at `2026-08-09T23:35:38.402Z`.

This version replaces the review-rejected v1.0.1 authoring bytes. Its frozen
truth is independently reviewed, and sanitized metadata records a complete
12/12 bounded typed-policy comparison. No expected truth changed, and the
comparison does not broaden the registered EVAL claims or establish
unconditional execution-ready native filesystem coverage. No product output
was inspected or used by the fixture author or product-blind reviewer.

## Isolation boundary

The fixture has three disjoint layers:

1. `harness-envelope.v1.json` contains case/package/EVAL identities, partition
   mappings, fault and mutation schedules, canary materialization, effect-spy
   controls, and oracle pointers. `safety-topologies.v1.json` contains the
   physical object graph and final-object derivation facts. Both are
   harness-only and must never be product input.
2. `ordinary-product-projections.v1.json` contains only ordinary entities,
   relations, and commands. For one execution, the harness selects exactly one
   projection object, validates it against
   `ordinary-product-projection.schema.json`, recursively rejects harness or
   answer metadata, and forwards only the selected object.
3. `expected-results.v1.json` is oracle-only and must never enter the product,
   model, retrieval, renderer, coordinator, worker, or adapter input path.

Projection validation is fail-closed and requires a retained receipt. The
schema sets `additionalProperties: false` at every object layer. The envelope
also denies package/case/EVAL IDs, canary values, oracle pointers, purposes,
partitions, expected fields, and fixture metadata recursively.

## Product-comparison closeout

A sanitized receipt recorded at `2026-08-09T22:58:29.392Z`, with capability
wording corrected at `2026-08-09T23:35:38.402Z`, states that the ordinary
projection adapter executed all 12 frozen bindings before loading
`expected-results.v1.json`. It then compared every complete expected object
with `JsonNode.DeepEquals`; all 12 passed and no expected truth was edited.
Each gate retained pre-dispatch closed-schema and answer-isolation receipts
from the actual validator.

Both safety bindings physically exercised distinct roots and objects, final
Windows object identity, protected-root canaries, handle-relative writes, NTFS
hard links, junction/mount-point reparse entries, relative/parent/case path
forms, and pinned-handle entry-replacement races. Writes occurred only after
`FinalObjectAuthorityPolicy` authorization, with inert external-effect
counters active.

Native symbolic-link creation was unavailable with Windows error `1314`. A
mount-point reparse substitute exercised the link-type-neutral final-open
policy, but does not qualify native symbolic links. Native 8.3 alias, UNC,
device, alternate-data-stream, and cross-volume qualification remain explicit
capability gaps or stand-ins. `real_external_adapter_qualification=false`
remains unchanged.

The product-blind reviewer recorded only these sanitized facts and did not
inspect product source, tests, contracts, raw output, or artifacts.

## Frozen files

| File | Role | Bytes | SHA-256 |
|---|---|---:|---|
| `ordinary-product-projection.schema.json` | harness-only closed projection validator | 2633 | `b59430067ccc0b50f6757d41b658b8fcc4317f57bc01e7aaa90bc8525011db5e` |
| `ordinary-product-projections.v1.json` | source of selected ordinary product objects | 26499 | `33f739fabf923da3bf8b864bf199a07d65a7fa9a04d54755c3034af4fab0bdca` |
| `safety-topologies.v1.json` | harness-only physical topology and final-object facts | 20262 | `e544e974055e6cf79c7753cd9a28f760c118e26535af1449fbae910c06e178ac` |
| `harness-envelope.v1.json` | harness-only identities and controls | 13601 | `4964bc553afdb9cba848c98542d7bf750b124b1ecedaf42134370505a76a2852` |
| `expected-results.v1.json` | oracle-only independent truth | 10477 | `b971504c46fb46bae2ba6fdd596a1ac730f492cd352792b8e48f6517cac8cf37` |
| `fixture-manifest.v1.json` | registry, isolation, review, and comparison metadata | 14424 | `794f87804efcea7432c60f14702da5774ab2c16d7b82d9222e87259334f56078` |
| `independent-review.md` | product-blind review and sanitized comparison closeout | 5853 | `5258a4a11b6e41be4270ad40459a8a51e1a3b272c0c3e313f85cce14ad84afec` |

Any byte change to the five author-frozen fixture-content files invalidates
their freeze and requires a versioned replacement plus fresh independent
review. Review-only changes to the manifest, README, and independent-review
record do not alter those frozen content identities.

## Package identities and counterparts

- Development:
  `infinium.public-fixtures.analysis-operations.publication-replay-query-output-recovery-safety.lantern-a/1.0.2`
- Validation:
  `infinium.public-fixtures.analysis-operations.publication-replay-query-output-recovery-safety.compass-b/1.0.2`

| Family | Development | Materially independent validation |
|---|---|---|
| atomic publication | `OPS-LANTERN-PUBLICATION-D01` — payload/edge staging fault | `OPS-COMPASS-PUBLICATION-V01` — durable-intent/pre-commit fault with different graph and revision |
| replay/invalidation | `OPS-LANTERN-REPLAY-D02` — clean/incremental/replay equality and one graph change | `OPS-COMPASS-REPLAY-V02` — same-identity changed-fact drift plus equal-fact different-identity substitute |
| bounded query | `OPS-LANTERN-QUERY-D03` — rank-descending, identity-ascending, page size 2 | `OPS-COMPASS-QUERY-V03` — time-descending, identity-descending, page size 3 |
| terminal/equivalent output | `OPS-LANTERN-OUTPUT-D04` — four terminal states with one fact/gap population | `OPS-COMPASS-OUTPUT-V04` — four states with different fact, gap, and review populations |
| recovery | `OPS-LANTERN-RECOVERY-D05` — crash after durable stage | `OPS-COMPASS-RECOVERY-V05` — crash after checkpoint and before stage |
| write/non-mutation safety | `OPS-LANTERN-SAFETY-D06` — delta topology, ordinal order, mixed authorized reparse/final-handle controls | `OPS-COMPASS-SAFETY-V06` — epsilon topology, permuted order, independently reassigned reparse/root/race outcomes |

Both safety cases contain five direct write-class destinations and the complete
bounded path/object matrix: direct non-authorized descendant, traversal,
absolute and relative aliases, symbolic link, junction, mount point, hard link,
short name, case variant, UNC, device path, alternate stream, cross-volume,
ancestor replacement, final-entry replacement, check/use replacement, stale
capability, and recursive deletion. Every target has a neutral synthetic
identity and frozen before-path, opened-object, replacement-entry, final-path,
final-open-object, capability, operation-support, owner-root, and root-authority
fact. All four race targets therefore have complete before/open/replace/final
transitions.

The validation command order is deliberately permuted and non-isomorphic. Its
reparse and race targets also resolve to different authority classes than the
development counterparts. The frozen decision vectors are mechanically
derived only from `final-object-authority-rule/1.0.0`; ordinal and path-token
classification cannot reproduce both. Harness-only inert network, process,
shell, credential, and external-tool spies plus fresh protected/external-effect
canaries establish the claimed zero-effect boundary.

## EVAL claim boundary

This revision retains only bounded claims for `EVAL-0035`, `EVAL-0037`,
`EVAL-0039`, `EVAL-0040`, and `EVAL-0080`. It explicitly does not claim:

- `EVAL-0046` real external-adapter qualification;
- `EVAL-0082` full lifecycle coverage;
- `EVAL-0087` full persistence/corruption coverage; or
- `EVAL-0088` full IPC/version/role coverage.

Atomic publication, recovery, and cursor cases remain narrower operations stage slice
evidence until comprehensive platform fixtures establish those larger EVAL
matrices.

## Author checks

- all six JSON files parse;
- 12 projection objects map one-to-one to 12 harness bindings and 12 oracle
  cases;
- each of six families has one development and one validation counterpart with
  symmetric cross-references;
- the product projection contains no package/case/EVAL identity, canary,
  oracle pointer, purpose, partition, expected field, or fixture identity;
- every selected product object has exactly the schema-approved top-level and
  nested shapes;
- both safety projections contain 25 operations, but use different command
  orders and independently derived vectors (development 9 accept/16 reject;
  validation 10 accept/15 reject);
- every required alias/reparse identity and race phase is present in the frozen
  topology, and a mechanical final-object derivation reproduces both oracle
  vectors exactly;
- no answer-bearing path token occurs in either ordinary product projection;
- all retained EVAL IDs are within the narrowed allowlist;
- embedded hashes match the frozen files; and
- no product comparison or execution was performed.

## Isolation attestation

The author remained product-blind. No product source, tests, contracts, build
output, artifact, Git diff/history, private/evaluator repository, legacy
archive, or product execution was accessed. The previously named JSON platform
spec/manifest paths remained absent and were not reconstructed; the accepted
Markdown platform fixture manifest remains the registration authority.

The later comparison closeout preserved that boundary: the product-blind
reviewer received only the sanitized 12/12 receipt summarized above.
