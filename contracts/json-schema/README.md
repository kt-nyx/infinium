# JSON schemas

These files are the product machine contracts for evaluation packages,
analyzer/configuration declarations, run-owned output, CLI summaries, and
sensitive developer traces. They use JSON Schema Draft 2020-12. Every schema
has a stable absolute `$id`. Root output/declaration contracts also carry a
versioned `schema_id`; fixture package documents carry the exact version fields
defined by the accepted fixture package contract.

References between files use repository-local relative paths. Consumers shall
resolve those paths from this directory and shall not retrieve schemas from the
network.

## Contract inventory

| File | Instance `schema_id` when present |
|---|---|
| `fixture-public-manifest.v1.schema.json` | `infinium.evaluation.fixture-public-manifest/v1` |
| `fixture-execution-input.v1.schema.json` | —; versioned by fixture and schema file |
| `fixture-oracle.v1.schema.json` | —; versioned by fixture, oracle, and schema file |
| `fixture-provenance.v1.schema.json` | —; versioned by fixture and schema file |
| `fixture-redistribution.v1.schema.json` | —; versioned by fixture and schema file |
| `fixture-partition-history.v1.schema.json` | —; versioned by fixture and schema file |
| `replay-dependencies.v1.schema.json` | —; versioned by fixture and schema file |
| `taxonomy-projections.v1.schema.json` | `infinium.evaluation.taxonomy-projections/v1` |
| `taxonomy-subject-bindings.v1.schema.json` | `infinium.evaluation.taxonomy-subject-bindings/v1` |
| `evaluation-assertion-result.v1.schema.json` | `infinium.evaluation.assertion-result/v1` |
| `analyzer-declaration.v1.schema.json` | `infinium.analyzer.declaration/v1` |
| `effective-scan-configuration.v1.schema.json` | `infinium.scan.effective-configuration/v1` |
| `run-output.v1.schema.json` | `infinium.run-output/v1` |
| `cli-summary.v1.schema.json` | `infinium.cli-summary/v1` |
| `diagnostic-trace.v1.schema.json` | `infinium.diagnostic.trace/v1` |
| `documentation-evidence.v1.schema.json` | `infinium.documentation.evidence/v1` |
| `candidate-analysis.v1.schema.json` | `infinium.analysis.candidate/v1` |
| `candidate-delivered-input.v1.schema.json` | `infinium.analysis.candidate-delivered-input/v1` |
| `candidate-delivered-expansion.v1.schema.json` | `infinium.analysis.candidate-delivered-expansion/v1` |
| `provider-access-profile.v1.schema.json` | `infinium.provider.access-profile/v1` |
| `provider-operation.v1.schema.json` | `infinium.provider.operation/v1` |
| `provider-response.v1.schema.json` | `infinium.provider.response/v1` |
| `source-claim-extraction.v1.schema.json` | `infinium.llm.source-claim-extraction/v1` |
| `source-claim-application-decision.v1.schema.json` | `infinium.host.source-claim-application-decision/v1` |
| `candidate-investigation.v1.schema.json` | `infinium.llm.candidate-investigation/v1` |
| `provider-execution-input.v1.schema.json` | `infinium.provider.execution-input/v1` |
| `effective-scan-configuration.v2.schema.json` | `infinium.scan.effective-configuration/v2` |
| `run-output.v2.schema.json` | `infinium.run-output/v2` |
| `cli-summary.v2.schema.json` | `infinium.cli-summary/v2` |
| `renderer-envelope.v1.schema.json` | —; sole renderer source, exact renderer contract version `1.4.0` |
| `renderer-operation-registry.v1.schema.json` | —; generated-registry validator version `1.3.0`, exact renderer contract version `1.4.0` |

`common.v1.schema.json` contains shared closed definitions and is not itself an
instance contract.

`renderer-envelope.v1.schema.json` is the sole owned renderer source. Its
`x-infinium-registry` metadata deterministically generates
`contracts/renderer/renderer-operation-registry.v1.json`, the strict
TypeScript contracts, and the C# catalog/adapters. Renderer contract `1.4.0`
and registry schema `1.3.0` are independent axes; the generated registry has
nine closed operations, sixteen exact directional message shapes, and
canonical SHA-256
`411a9c05604c7664773aa62c36f62817273ecaff228f20e074063bed1414cfa9`.
The schema and generated registry explicitly deny `path`, `sql`, `command`,
`command_line`, `url`, `credential`, `provider_request`, `filesystem`, and
`coordinator_proxy`. They expose no generic native, application, gRPC, or
targeted-verification authority.

The nine Slice 6 contract families are `Slice-frozen` after owner acceptance
of exact verified product candidate
`a17ff8f05ca916b4a6db2b4b3e78ba99e1313442`.
The v2 configuration and output contracts are additive provider-active
supplements. They bind the exact frozen local v1 artifact by identity or
fingerprint and do not reinterpret or replace the Slice 5 v1 bytes. All nine
contracts are closed at every declared object boundary and contain no
credential target, secret byte, authorization-header, or raw-header field.
Provider execution and operation carry the accepted versioned local input-bound
policy `openai-responses-o200k-byte-envelope/v2`. This maturity transition does
not authorize provider dispatch, credentials, or later-package execution and
does not change or reinterpret frozen Slice 5 v1 bytes.

The candidate stage delivered-input and expansion contracts contain only snapshot-bound
Bethesda/documentation stage facts and deterministic construction parameters. Candidate lanes,
dispositions, gaps, failures, expected output, fixture identities, oracle
metadata, and generator seeds are not part of either product contract.

## Evaluation-package isolation

The public manifest, executable input, independent oracle, provenance,
redistribution, partition-history, replay dependency manifest, and assertion
result are separate documents. Every fixture document has a closed schema. The executable-input
contract is closed at every object boundary and has no arbitrary extension or
metadata object. Consequently, expected labels, expected findings, oracle
paths, answer-bearing adjudication, and fixture-specific notes are rejected
rather than ignored.

The public manifest requires exact package/oracle/provenance/replay
fingerprints, partition history, and taxonomy `0.1.0`. The separate oracle
requires independently reviewed ground-truth methods, resolvable method
references, type-specific expected collections (including failures), an
explicit gap declaration, and every expected typed collection and collection
production state even when its correct value is an empty array. Replay dependencies retain exact byte fingerprints when
applicable and explicit availability, clean-recomputation, boundary-replay,
audit, deletion, and permission states.

When one retained package artifact is referenced more than once within an
input or oracle document, every occurrence must repeat exactly the first
reference's canonical artifact ID spelling, artifact version, fingerprint,
availability, and optional byte-length presence and value.

## Output invariants

`infinium.run-output/v1` keeps observations, deterministic results, external
claims, application links, discovery leads, model proposals, proposal admissions,
candidates, hypotheses, findings, recommendations,
supported cases, lead-only cases, abstentions, invalid inputs, gaps, and
failures in separate required collections. A parallel required
`collection_states` object distinguishes a correct empty collection from
unsupported, not-applicable, or failed production. Analyzer coverage uses
labeled denominators and cannot represent one aggregate safety percentage.
CLI summaries retain nonnegative elapsed duration and keep provider usage,
locally calculated actual cost, reserved cost, and unresolved holds in
separate fields. Readiness remains a placeholder and always carries
`no_safety_guarantee: true`.

Analyzer maturity is fixed to `Experimental` in the current bounded contracts, raw development output is
mandatory, and preset/maturity suppression is forbidden. Effective analyzer,
source, budget, cache, trace, candidate, threshold, provider, resource, and
semantic-override settings each retain their origin independently.

Developer traces are always labeled
`sensitive-development-diagnostic`, are never externally shareable exports,
use ADR-0021 sharing class `PrivateDiagnostic`, and assert that credential
material is absent. Schema validation is necessary
but does not replace secret-canary scanning or semantic consistency checks
between fingerprints, partition history, collection contents/states, CLI
output, and the sealed oracle.

The stable run-output and CLI-summary C# document models serialize and
deserialize through their embedded schemas in both directions. Their richer
in-memory aggregate models are named separately and cannot be mistaken for the
wire documents.

Taxonomy-bearing packages retain a closed projection document and a separate
answer-free binding document. Each sealed taxonomy subject maps to exactly one
literal production subject participant ID; duplicate, missing, unexpected, or
reused targets are invalid. The projection and binding documents carry exact
fixture and taxonomy identities. A projection's source set is exactly its
retained accepted-order receipt and independent byte facts, with every source
reference matching both the already snapshotted metadata and the normalized
`accepted_order_construction_input` execution-role reference. The receipt is
validated by the closed
`bethesda-accepted-order-construction-input.v1.schema.json` contract and is
distinct from installation-snapshot and runtime plugin-order declarations.
Every retained oracle file must be owned by the expected oracle's exact
reference closure.

Readers consume each document through one bounded, read-only file snapshot,
reject duplicate object properties recursively, and hash and parse the same
captured bytes. Every `date-time` value uses the canonical .NET round-trip
representation with a zero UTC offset, for example
`1970-01-01T00:00:00.0000000+00:00`.

Slice 6 provider-operation and response documents use conditional lifecycle
shapes: identities appear only once their producer state is reachable, usage
quantities distinguish an available zero from an unavailable value, and
capability/price snapshots contain their closed typed contents rather than an
unreachable definition or opaque JSON blob. Operation kind selects the exact
seven-dimensional qualification or semantic ceiling.
