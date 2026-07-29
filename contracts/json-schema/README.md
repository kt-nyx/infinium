# JSON schemas

These files are the M1 Slice 1 machine contracts for evaluation packages,
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
| `replay-dependencies.v1.schema.json` | —; versioned by fixture and schema file |
| `evaluation-assertion-result.v1.schema.json` | `infinium.evaluation.assertion-result/v1` |
| `analyzer-declaration.v1.schema.json` | `infinium.analyzer.declaration/v1` |
| `effective-scan-configuration.v1.schema.json` | `infinium.scan.effective-configuration/v1` |
| `run-output.v1.schema.json` | `infinium.run-output/v1` |
| `cli-summary.v1.schema.json` | `infinium.cli-summary/v1` |
| `diagnostic-trace.v1.schema.json` | `infinium.diagnostic.trace/v1` |

`common.v1.schema.json` contains shared closed definitions and is not itself an
instance contract.

## Evaluation-package isolation

The public manifest, executable input, independent oracle, replay dependency
manifest, and assertion result are separate documents. The executable-input
contract is closed at every object boundary and has no arbitrary extension or
metadata object. Consequently, expected labels, expected findings, oracle
paths, answer-bearing adjudication, and fixture-specific notes are rejected
rather than ignored.

The public manifest requires exact package/oracle/provenance/replay
fingerprints, partition history, and taxonomy `0.1.0`. The separate oracle
requires independently reviewed ground-truth methods, an explicit gap
declaration, and every expected typed collection even when its correct value
is an empty array. Replay dependencies retain exact byte fingerprints when
applicable and explicit availability, clean-recomputation, boundary-replay,
audit, deletion, and permission states.

## Output invariants

`infinium.run-output/v1` keeps observations, deterministic results, external
claims, application links, discovery leads, model proposals, proposal admissions,
candidates, hypotheses, findings, recommendations,
supported cases, lead-only cases, abstentions, invalid inputs, gaps, and
failures in separate required collections. A parallel required
`collection_states` object distinguishes a correct empty collection from
unsupported, not-applicable, or failed production. Analyzer coverage uses
labeled denominators and cannot represent one aggregate safety percentage.
Readiness is only the M1 placeholder and always carries
`no_safety_guarantee: true`.

Analyzer maturity is fixed to `Experimental` in M1, raw development output is
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
