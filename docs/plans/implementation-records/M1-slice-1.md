# M1 Slice 1 implementation record

Status: Completed
Completed: 2026-07-28
Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted revision dated 2026-07-28
Slice: 1 — Versioned domain, wire, output, and evaluation contracts

## Outcome

Slice 1 established the clean-break, versioned contract boundary required
before any M1 persistence or runtime implementation:

- fail-closed C# value objects, enums, records, and cross-record invariants for
  domain, operational, analyzer, output, CLI, diagnostic, provenance, coverage,
  replay, audit, and readiness contracts;
- six versioned protobuf contract surfaces separating common/domain,
  application, protocol, worker, and credential-helper authority;
- 11 closed Draft 2020-12 JSON Schemas for analyzer declarations, effective
  configuration, run and CLI output, diagnostics, fixtures, replay
  dependencies, and evaluation assertion results;
- schema-validating fixture-package and assertion-result readers with bounded
  input sizes and fail-closed unknown-member handling;
- enforced public/execution/oracle answer isolation, complete partition
  history, known-answer replacement requirements, independent ground-truth
  metadata, taxonomy identity, fingerprints, expected typed output, and
  expected-gap declarations; and
- contract, security, fault, integration, and structural-evaluation checks,
  including a real schema-validated analyzer-declaration JSON round trip.

The contract distinguishes model proposals from admitted typed artifacts.
Admission requires a validated proposal, an allowed matching artifact type, a
real artifact in the same run, and matching operation and invocation
provenance. LLM-derived material cannot become a local observation or
deterministic result.

No persistence, coordinator, worker, CLI workflow, analyzer behavior, product
fixture instance, provider call, protected-root access, credential operation,
or later-slice capability was added.

## Retained artifacts

- `src/Infinium.Domain/Contracts/`
- `src/Infinium.Application/Evaluation/`
- `contracts/json-schema/`
- `contracts/protobuf/infinium/`
- Slice 1 unit and contract tests under `tests/Infinium.UnitTests/` and
  `tests/Infinium.ContractTests/`
- Updated project references, embedded-schema resources, project lock files,
  repository-structure expectations, and contract README files

No runtime run identifier exists because Slice 1 defines and verifies
contracts without executing a product run. The synthetic IDs used by tests,
including `fixture-development-1`, `assertion-result-1`, and `run-1`, are test
data identities and not product run evidence.

The protobuf descriptor emitted during verification remained an ignored local
verification artifact under `artifacts/verification/`; its SHA-256 was
`25cac81bb86596558207549a9aa5aeb9f03a5ccfafa76ded16ab83ad9a41cf99`.

## Verification

Final commands were run from the repository root on Windows x64 with .NET SDK
`10.0.302`:

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode` | Passed; all 15 projects restored from committed locks. |
| `dotnet restore Infinium.sln --locked-mode --force --no-cache --nologo -p:RestorePackagesPath="<new empty artifacts/verification path>"` | Passed; clean restore into a previously absent package directory. |
| `dotnet build Infinium.sln -c Release --no-restore` | Passed; 0 warnings and 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Unit"` | Passed; 12 of 12 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Contract"` | Passed; 10 of 10 applicable checks across unit and contract assemblies. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Integration"` | Passed; 3 of 3 applicable checks across contract and integration assemblies. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Evaluation"` | Passed; 6 of 6 applicable checks across contract and evaluation assemblies. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Security"` | Passed; 5 of 5 applicable checks across unit and contract assemblies. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Fault"` | Passed; 11 of 11 applicable checks across unit and contract assemblies. |
| `dotnet test Infinium.sln -c Release --no-build` | Passed; 47 of 47 checks: 16 unit, 25 contract, 2 integration, and 4 evaluation. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed; no formatting changes required. |
| AJV CLI `5.0.0` plus `ajv-formats` `3.0.1`, Draft 2020 mode, over all 11 schemas | Passed; every schema compiled, including all local references. |
| `grpc_tools_node_protoc` `3.19.1` over all six `.proto` files with imports and source information | Passed; emitted a 93,643-byte descriptor set. |
| Two non-incremental Release builds followed by SHA-256 comparison of emitted DLL/PDB files | Passed; all 615 emitted DLL/PDB hashes matched. |
| `dotnet list Infinium.sln package --include-transitive --no-restore` | Passed; the resolved graph remained within the locked Slice 0 dependency inventory. |
| `rg -n -i "infinium-legacy-archive\|7dd3da6\|legacy[/\\]"` over production, test, contract, fixture, and tool roots | Passed; no prohibited reference found. |
| `git diff --check` | Passed. |

The external AJV and protobuf tools were used only as contract compilers. They
did not execute Infinium, contact a product data source, invoke a model, or
authorize any product-side external state.

## Evaluation cases and gates

This slice satisfies only the accepted contract portions of EVAL-0065,
EVAL-0067, and EVAL-0082:

- EVAL-0065: analyzer scope, dependencies, thresholds, evidence, coverage,
  maturity, and linked-case declaration contracts;
- EVAL-0067: typed evidence, LLM proposal/admission provenance, answer
  isolation, replay dependencies, and complete oracle expectation contracts;
- EVAL-0082: closed effective-configuration origin, budget, cache, tracing,
  breadth, threshold, provider, resource, and override contracts.

The fixture reader refuses packages missing a schema identity, fingerprint,
partition history, independent ground truth, taxonomy version, typed expected
outputs, or expected gap declarations. It also rejects answer-bearing
execution input and known-answer promotion without a different replacement
fixture identity and partition.

No complete accepted `EVAL-*` case is claimed to pass. Those cases still
require executable fixtures and the later runtime/analyzer slices.

## Review and corrections

The implementation received separate JSON-schema, protobuf, traceability,
semantic-diff, and final re-review passes. Corrections made during those passes
included:

- replacing permissive artifact unions with distinct closed typed contracts;
- aligning coverage vocabulary, diagnostic sharing class, CLI outcomes, and
  exact taxonomy identity across C#, JSON, and protobuf surfaces;
- making effective configuration structured and fail-closed, including a
  closed tracing-level vocabulary and exact M1 provider controls;
- requiring complete partition histories, independent ground-truth methods,
  and replacement metadata for known-answer promotion;
- validating JSON with the published embedded schemas rather than DTO shape
  alone;
- adding schema metadata and a real analyzer-declaration JSON round trip;
- rejecting dirty successful assertion results;
- closing helper and worker authority boundaries and finite resource ceilings;
  and
- preventing model-proposal admission from drifting across artifact type,
  validation state, run, operation, or invocation.

The final re-review found no real-mod-specific rule, fixture-specific
production behavior, invented certainty, answer leakage, unbounded helper
authority, protected-root action, or unclaimed later-slice implementation.

## Known gaps and deferred work

- The protobuf contracts have no predecessor baseline because this is their
  initial `v1`; future changes need descriptor compatibility comparison
  against this committed contract revision.
- The JSON schemas and readers are implemented, but no executable product
  fixture package exists in Slice 1.
- Runtime configuration resolution, persistence, lifecycle execution,
  coordinator/worker protocol handling, CLI emission, analyzers, and provider
  behavior remain in their declared later slices.
- Passing schema and contract tests does not establish M1 product correctness,
  fixture conformance, performance, or support claims.

No later-slice implementation was included.
