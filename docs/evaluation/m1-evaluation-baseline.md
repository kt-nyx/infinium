# M1 evaluation baseline

Status: Accepted  
Owner: Project owner  
Prepared: 2026-07-28  
Accepted: 2026-07-28  
Last reviewed: 2026-07-28  
Target milestone: M1 — Backend semantic proof

## Purpose

This document defines the common acceptance contract for every evaluation used
to claim M1 completion. The case specifications supply case-specific inputs and
assertions:

- [semantic and local-ground-truth specifications](specifications/m1-semantic-and-ground-truth.md);
- [semantic fixture manifests](fixtures/m1-semantic-fixture-manifests.md);
- [platform and operational specifications](specifications/m1-platform-and-operational.md);
- [platform fixture manifests](fixtures/m1-platform-fixture-manifests.md).

Acceptance of this baseline or a case specification does not mark a fixture
executed, an implementation conformant, an analyzer mature, or a product
surface supported. Those claims require retained M1 execution evidence.

## Authority

This baseline consumes:

- the accepted [product requirements](../product/requirements.md);
- taxonomy `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`;
- accepted ADR-0001 through ADR-0023, with ADR-0024 rejected;
- accepted ADR-0025, which defines the implementation-authoritative live M1
  profile;
- the accepted [anti-overfitting rules](anti-overfitting-rules.md);
- the [fixture guidelines](fixture-guidelines.md); and
- the accepted M0 research dispositions.

If a fixture exposes a contradiction with an accepted requirement or ADR, work
stops at that boundary. A passing test does not silently amend the governing
document.

## M1 claim boundary

M1 evaluates a CLI-first backend proof containing:

- exact target/profile admission and bounded MO2 `2.5.2` effective-state
  reconstruction;
- positively allowlisted Mutagen `0.54.2` record semantics;
- immutable snapshots, contexts, configurations, runs, and resolved inputs;
- SQLite/CAS persistence, coordinator-owned publication, bounded workers, and
  named-pipe contracts;
- typed evidence, typed indexes, causal joins, mandatory candidate lanes, and
  explicit negative/gap populations;
- local/fixture documentation claims and direct synchronous OpenAI Responses
  operations under the accepted credential and budget boundaries and the
  exact profile defined by accepted ADR-0025, including separate live
  source-claim-extraction and evidence-bound-candidate-investigation
  validation operations after transport qualification;
- synthetic scope-incongruent reversion positives and matched negatives;
- the two qualified controlled-real generalization candidates; and
- human-readable CLI plus versioned JSON run output.

M1 does not claim a graphical UI, LOOT/libloot coverage, Nexus acquisition,
hosted web search, background/Batch/cached provider execution, archive-positive
FaceGen, a production NIF parser, runtime-log application, named generator or
configuration adapters, broader semantic-family coverage, M3 scale, M4
packaging, or public supportability.

## Case state vocabulary

Each case has two independent states:

1. **Specification state**
   - proposed;
   - accepted;
   - superseded.
2. **Execution state**
   - not run;
   - running;
   - passed;
   - failed;
   - blocked;
   - invalidated.

Only the project owner accepts specifications. Only a retained execution
against an accepted specification and exact implementation revision may pass a
case. Changing an expected answer, input manifest, assertion, or support
boundary creates a new specification revision and invalidates incomparable
prior execution.

## Fixture partitions and answer isolation

Before any execution, every fixture is registered as development, validation,
or held-out. The registry records partition history.

- Development fixtures may guide implementation.
- A validation fixture becomes development if its result directly changes
  code, rules, prompts, thresholds, or ranking.
- A held-out fixture remains inaccessible to tuning and implementation inputs
  until its declared evaluation point. If it influences production behavior,
  it becomes development and receives a materially independent replacement.

Expected answers and answer-bearing adjudication are stored separately from
the runtime input package. Fixture IDs, mod names, expected labels, and known
record identities cannot enter generic ranking, analysis, retrieval, or model
context unless they are legitimate source evidence for that exact evaluated
operation.

## Required specification fields

Every case specification records:

- case ID and revision;
- purpose and claim boundary;
- requirements, ADRs, and upstream research;
- scope and explicit non-scope;
- partition and transition history;
- exact input/fixture manifest;
- independent ground-truth authority and review;
- setup and execution procedure;
- expected observations;
- expected candidates, hypotheses, findings, cases, abstentions, and gaps;
- supported-case versus lead-only state;
- taxonomy ID/version and applicable assignments;
- expected coverage populations and denominators;
- assertions and failure interpretation;
- replay dependencies and expected replayability;
- sensitive/private/copyrighted input treatment;
- retained execution artifacts; and
- what passing does not prove.

## Common execution envelope

Every M1 evaluation run retains:

- exact repository commit and dirty-state prohibition;
- build configuration and dependency lock identities;
- OS, .NET, native dependency, SQLite, MO2, game-runtime, Mutagen, and other
  applicable version identities;
- fixture/specification revisions and complete input fingerprints;
- effective analyzer/source/provider/budget/cache/trace configuration;
- semantic analysis context and readiness-policy version where exercised;
- process roles, protocol/schema versions, and security configuration;
- start/end time and deterministic seed where applicable;
- all raw observations, candidates, intermediate evidence, abstentions,
  failures, gaps, and coverage records;
- model request/response/schema/prompt/model/settings/usage/cost when applicable;
- human-readable and machine-readable results; and
- replayability plus audit-gap assessment.

An uncommitted implementation may be used for development diagnosis but cannot
produce retained milestone-passing evidence.

## Common assertion rules

1. Assertions compare typed values and stable identities, not display prose.
2. Deterministic stages reproduce exactly for identical declared inputs.
3. Model-backed stages may vary linguistically but must satisfy the same typed
   semantic assertions.
4. Every positive has a meaningful matched negative.
5. Malformed, unsupported, absent, ambiguous, and limited inputs have explicit
   expected outcomes.
6. Coverage denominators and exclusions are asserted independently from
   finding counts.
7. No-finding output is never a safety guarantee.
8. A lead cannot affect readiness.
9. Maturity or presentation policy cannot hide or relabel raw development
   output.
10. A failed dependency or source creates a typed gap; it never activates
    fabricated fallback data.
11. A provider/tool/model result cannot grant local or operation authority.
12. Historical results remain bound to their exact inputs and are never
    rewritten by later review or execution.

## Ground-truth independence

MO2 expected effective state comes from controlled disposable instances and
authoritative MO2-observable behavior. Bethesda record expectations come from
pre-registered, hand-audited binary/structure assertions and retained
independent adjudication. The Mutagen path under test is never the sole oracle,
and xEdit has no fixture or adjudication role.

For controlled-real cases, author documentation and author-supplied patches
bound intent and demonstrated resolution only within the exact inspected
fields. Patch presence or naming never establishes complete compatibility.

## Taxonomy and generalization

Every applicable case uses taxonomy version `0.1.0` and keeps declared purpose,
observed surface, predicted/established affected area, consequence, faceted
extent, severity, confidence, authority, symptoms, and maturity separate.

The first proof is not generic until the same abstraction passes a materially
different category proof. Passing the two selected real candidates remains
minimum generalization evidence, not broad Skyrim coverage.

## Security and non-mutation

Evaluation uses disposable product roots and disposable or read-only copied
setup inputs. Protected roots receive before/after manifests and canaries.
Every product, tool, cache, temporary, credential, diagnostic, and output write
must be authorized by class and included in retained effects.

No test may place a real reusable credential in tracked fixtures. Provider
integration uses a dedicated test access profile supplied at execution time,
and retained artifacts must pass secret-canary review before acceptance.

## M1 required case set

The following accepted specifications and passing executions gate M1:

- semantic mechanism/generalization: EVAL-0001, EVAL-0002, EVAL-0016,
  EVAL-0017;
- run, lifecycle, and candidate correctness: EVAL-0026, EVAL-0032,
  EVAL-0037, EVAL-0038;
- untrusted content and authority: EVAL-0033 through EVAL-0035;
- acquisition/output/initiation: EVAL-0039, EVAL-0040, EVAL-0045;
- non-mutation and supported local truth: EVAL-0046, EVAL-0051, EVAL-0052,
  EVAL-0054;
- provider-independent/offline and typed contracts: EVAL-0064, EVAL-0065,
  EVAL-0067;
- finding/case continuity: EVAL-0079;
- authenticated-provider capability and billing authority: EVAL-0076,
  EVAL-0077;
- product writes, budget, controls, provenance, grouping, coverage, and
  taxonomy: EVAL-0080 through EVAL-0086; and
- persistence, process/IPC, and credential lifecycle: EVAL-0087 through
  EVAL-0089.

The non-live/pre-dispatch portions of EVAL-0034, EVAL-0076, EVAL-0077,
EVAL-0081, and EVAL-0089 apply before the deliberately tiny live provider
qualification request. General dispatch opens only if that request's retained
transport, schema, credential, usage, settlement, and canary assertions pass.
EVAL-0067 and EVAL-0083 then require two separately authorized live semantic
requests: source-claim extraction and evidence-bound candidate investigation.
Background, Batch, explicit provider caching, and concurrent live-call
variants remain disabled until their extensions pass.

LOOT-dependent EVAL-0053 is not an M1 gate because M1 does not claim LOOT
coverage. It becomes mandatory before that boundary is enabled.

## Failure and waiver policy

M1 has no silent waivers. A failing required case results in one of:

- implementation correction followed by a fresh run;
- independently evidenced specification correction with revision history;
- explicit M1 scope reduction that still satisfies the accepted M1 product
  goal and updates every affected plan/case/coverage claim; or
- blocked M1 completion.

Unsupported input is a passing result only when the accepted specification
requires explicit unsupported/gap behavior and the product does not claim that
surface.

## Completion evidence

M1 completion must retain:

- a case-result index linking every required specification revision to a
  passing execution;
- exact commands and logs;
- implementation commit;
- fixture/input manifest revisions;
- machine-readable assertions and coverage;
- failed-attempt history;
- secret/non-mutation review;
- performance/resource observations without claiming M3 scale;
- known gaps and disabled capabilities; and
- independent semantic review confirming that the evidence supports only the
  declared M1 claims.
