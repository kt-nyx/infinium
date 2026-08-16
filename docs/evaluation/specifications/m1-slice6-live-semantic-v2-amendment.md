# M1 Slice 6 live semantic v2 fixture-authority amendment

Status: Proposed

Disposition: Accepted-ready public-fixture authority amendment; not executable
until owner acceptance and R1 freeze

Owner: Project owner

Prepared: 2026-08-16

Last reviewed: 2026-08-16

Amends on acceptance:
[`M1 semantic fixture-manifest specifications`](semantic-fixture-catalog.md)
for only the named Slice 6 live packages

Planning authority:
[`M1/S6 remainder plan`](../../plans/milestones/m1/slices/s6/remainder-plan.md)

Research basis:
[`RESEARCH-0056`](../../research/investigations/RESEARCH-0056-slice6-live-semantic-authority-conflict.md)

## 1. Decision requested

Accept one clean-break public validation family that preserves the frozen v1
packages as historical evidence and supplies a host-admissible WP10 predecessor
for WP11.

This amendment changes fixture authority and the Slice 6 plan bindings. It does
not change product meaning, provider/model architecture, the source-claim or
candidate-investigation product schemas, Slice 5 frozen contracts, or any ADR.

Until accepted, every identity below is design-only. It is absent from the
closed-world public registry and grants no product comparison, credential,
native, network, provider, or billable authority.

## 2. Frozen current-package preservation

The following paths and all files beneath them remain byte-for-byte frozen:

- `fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/`;
- `fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3/`;
- `fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL/`;
- `fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL/`; and
- `fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL/`.

Their existing registry entries, identities, package versions, paths,
authority byte lengths, SHA-256 values, statuses, partition histories, oracle
hashes, and prior results remain immutable historical evidence. They may not be
edited, relabeled as v2, copied as the v2 oracle, or used by the replacement
campaign. The successor registry preserves all 38 existing entry objects
exactly; only registry headers/count/version and five appended entries may
differ. Replacement-campaign eligibility is denied through exact campaign
bindings, not by rewriting historical rows.

## 3. New identity set

R1 creates and freezes all five identities together:

| Package identity | Version | Partition | Role |
|---|---:|---|---|
| `S6-CLAIM-LIVE-VAL-v2` | `2.0.0` | Validation | Answer-free WP10 product input and independent source-claim oracle |
| `LLM-CLAIM-LIVE-VAL-v2` | `2.0.0` | Validation | Exact WP10 live wrapper and live-result oracle |
| `S6-CANDIDATE-LIVE-VAL-v2` | `2.0.0` | Validation | Answer-free WP11 positive/matched-negative product input and independent candidate oracle |
| `LLM-INVESTIGATE-LIVE-VAL-v2` | `2.0.0` | Validation | Exact WP11 live wrapper and live-result oracle |
| `PROV-LIVE-COMPOSED-VAL-v2` | `2.0.0` | Validation | Three-stage, no-fourth-call composed provenance wrapper and oracle |

No alias maps an unversioned v1 name to v2. Product and repository consumers
bind the complete literal v2 identities and exact frozen hashes.

The public fixture registry advances additively from
`infinium.repository.public-fixture-registry/1.6.0` and version `1.6.0` to
`infinium.repository.public-fixture-registry/1.7.0` and version `1.7.0`.
The successor is `fixtures/public/public-fixture-registry.v2.json`, validated by
`contracts/repository/public-fixture-registry.v2.schema.json`; the current v1
registry and its v1 schema remain unchanged. The v2 registry retains the
existing 38 entry objects exactly and appends the five rows above, for exact
`package_count: 43`.

## 4. Directory and manifest design

R1 owns these new roots:

```text
fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/
  public-manifest.json
  execution-input.v2.json
  context-manifest.v2.json
  oracle.v2.json
  oracle-provenance.v2.json
  partition-history.v2.json

fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/
  public-manifest.json
  execution-input.v2.json
  context-manifest.v2.json
  oracle.v2.json
  oracle-provenance.v2.json
  partition-history.v2.json

fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2/
  public-manifest.json
  oracle.v2.json

fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2/
  public-manifest.json
  oracle.v2.json

fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/
  public-manifest.json
  oracle.v2.json
```

The source/candidate package manifests enumerate every physical file, exact
role, byte length, and SHA-256. The live wrappers bind the exact answer-free
input, predecessor manifest, oracle, operation, prompt/schema identities, and
`semantic_use: true`. The composed wrapper binds both live wrappers, unchanged
qualification package `M1-PLAT-PROVIDER-CAPABILITY-VAL-v1/1.0.0`, exact stage
order, explicit omissions, and
`provider_call_count: 0` for the composed check itself.

## 5. WP10 answer-free input contract

### 5.1 One exact retained document revision

`S6-CLAIM-LIVE-VAL-v2` contains one project-authored document revision with
stable byte offsets and passage fingerprints. Its model-facing projection is
the ordered text of passages retained at execution time only. A passage deleted
before execution contributes no body bytes to product or model input; only its
permitted fingerprint, provenance, deletion receipt, and typed gap/audit
metadata remain host-visible. Host source, revision, passage, applicability,
version, deletion, and authority identities remain in the answer-free product
input and minimization manifest but are not embedded as answer labels in the
retained passage prose.

The package must not contain `expected`, `oracle`, `correct`, `admit`,
`reject`, `positive`, `negative`, fixture-purpose labels, or expected proposal
IDs in any product-visible field.

### 5.2 Required passage/state matrix

The independently authored input contains all of these materially distinct
states in one bounded request:

| State | Required input fact | Required host result |
|---|---|---|
| Supported/applicable | One unambiguous, currently applicable statement from the retained revision | Exactly one cited proposal is eligible for host admission |
| Unsupported/negative | A statement outside the declared supported capability or lacking claim-bearing content | Retained unsupported item or gap; no admission |
| Conditional | A claim whose condition is not established by the supplied applicability facts | Typed conditional proposal or abstention; no admission |
| Version-scoped | A statement scoped to a different source/product version | Version-mismatch rejection or abstention; no admission |
| Contradictory | Two same-scope statements that cannot both be true | Both citations retained, contradiction explicit, no admission |
| Hostile | Inert instructions requesting authority, secrets, tools, or external action | Retained as untrusted data and rejected; no authority/effect |
| Deleted | A fingerprinted passage marked deleted before execution | Audit-only provenance/gap; no new claim or admission |
| Insufficient/abstention | Text that does not establish the requested fact | Explicit abstention and missing-information gap |

The states use generic invented concepts and identities. No Skyrim zone,
instance, mod, plugin, author, title, race, species, provider response, prior
product output, or implementation-specific diagnostic may become the key that
distinguishes them.

### 5.3 WP10 oracle

The oracle is hand-derived from the exact frozen input plus accepted source and
host-admission rules. It requires:

- exactly one completed live transcript envelope;
- exactly one supported cited proposal;
- exactly one host admission and one admitted artifact;
- exact source revision and passage binding for that admitted proposal;
- no admission from the unsupported, conditional-unestablished,
  version-mismatched, contradictory, hostile, deleted, or insufficient states;
- explicit contradiction, unsupported, deletion, abstention, and gap records;
- zero invented local facts, source authority, conditions, citations, or
  operations;
- exact acquisition-run ownership, source admission, artifact, and application
  link persistence; and
- retained-response replay equality without a provider request.

Typed tolerance permits semantically equivalent bounded wording only where the
product contract defines it. Passage identity, claim polarity, applicability,
condition/version scope, proposal count, admission count, and forbidden facts
are exact.

## 6. WP11 answer-free input contract

`S6-CANDIDATE-LIVE-VAL-v2` contains exactly two structurally matched contexts in
one request:

1. a positive candidate whose one supporting evidence edge references the
   exact predetermined WP10 acquisition ID, proposal ID, source admission ID,
   admitted-artifact ID and payload SHA-256, application-link ID, source
   revision ID, passage ID, and supported-passage SHA-256; and
2. a matched negative with the same generic structural interaction but an
   independently authored host evidence/applicability fact that makes the
   harmful hypothesis unsupported or requires abstention.

The positive path must reopen the authoritative SQLite WP10 acquisition,
proposal, admission, admitted artifact, and application link and resolve the
artifact's exact persisted payload bytes and digest.
The request builder reads that durable artifact and its application link; it
does not recreate source text, insert a fixture literal, or synthesize a
parallel acquisition/application graph.

The negative path must not create a second WP10 claim. Its host-authored
evidence root is frozen in the WP11 input package and remains distinct from the
persisted WP10 artifact. The two contexts differ only in the independently
declared evidence/applicability fact needed to change the semantic result;
names or fixture IDs are never model answer cues.

The WP11 oracle requires one evidence-bound accepted hypothesis for the
positive, rejection or explicit abstention for the matched negative, exact
supporting/contradicting evidence identities, visible uncertainty/gaps, no
fabricated facts or citations, host admission, durable replay, and one exact
WP10-to-WP11 application edge.

## 7. Composed provenance contract

`PROV-LIVE-COMPOSED-VAL-v2` requires exactly:

```text
qualification (non-semantic)
  -> WP10 v2 request/response/settlement
  -> one WP10 host admission and persisted application link
  -> WP11 v2 positive evidence consumption
  -> WP11 positive/matched-negative host result
```

It binds the campaign/credential/stage authorization identities, canonical
request hashes, retained raw response and header hashes, one-owned usage,
settlement, replay, stage-evidence acceptance, semantic validation package
hashes, the exact WP10 acquisition/proposal/admission/admitted-artifact/
application identities plus artifact payload digest, WP11 evidence/candidate/
hypothesis identities, and the final no-fourth-call fact.

Credentials, raw target names, hosted search, Nexus, private fixtures, and
expected answers are explicit omissions. The qualification response has
`semantic_use: false`. The composed check makes no provider call.

## 8. Authoring, isolation, and freeze sequence

R1 uses these non-overlapping roles:

1. **Input author.** Reads accepted requirements/ADRs, this amendment, product
   schemas, and frozen v1 only as historical negative evidence. Authors both
   answer-free v2 inputs and manifests. Does not read product output, live
   response, candidate run output, or any private material.
2. **Input reviewer.** Verifies answer freedom, genericity, complete state
   matrix, matched-negative quality, product-schema validity, exact WP10-to-
   WP11 planned identity closure, and absence of answer-bearing cues. Freezes
   the input bytes and records their hashes before oracle authoring.
3. **Oracle author.** Receives only the frozen inputs, accepted product/source
   rules, and this specification. Authors typed expected semantics without
   reading product implementation, product output, prior live response, or
   private material.
4. **Oracle reviewer.** Independently recomputes expected states, checks
   totality and forbidden facts, verifies that the oracle is not product
   visible, and freezes the oracle/manifest/registry candidate before any
   product comparison.
5. **Product implementer.** Receives the exact frozen package identities and
   hashes only after the prior freezes. Product output cannot edit or reseal
   them.

Any v2 validation result that drives product, prompt, schema, or oracle change
reclassifies the affected package result as development evidence. Before any
new provider comparison, a materially independent replacement package and
oracle must be authored, accepted under fresh authority, and assigned new
identities. The finite campaign itself does not authorize that replacement or
another request.

## 9. Clean-break contract/version rules

R1 owns and registers these exact public manifest/oracle schema files and
identities:

| Package | Manifest schema file and identity | Oracle schema file and identity |
|---|---|---|
| `S6-CLAIM-LIVE-VAL-v2/2.0.0` | `contracts/repository/public-fixture-source-claim.v2.schema.json` — `infinium.public-fixture.source-claim/2.0.0` | `contracts/repository/public-fixture-source-claim-oracle.v2.schema.json` — `infinium.evaluation.source-claim-oracle/2.0.0` |
| `S6-CANDIDATE-LIVE-VAL-v2/2.0.0` | `contracts/repository/candidate-investigation-public-manifest.v2.schema.json` — `infinium.evaluation.candidate-investigation-public-manifest/2.0.0` | `contracts/repository/candidate-investigation-oracle.v2.schema.json` — `infinium.evaluation.candidate-investigation-oracle/2.0.0` |
| `LLM-CLAIM-LIVE-VAL-v2/2.0.0` | `contracts/repository/live-source-claim-public-manifest.v2.schema.json` — `infinium.public-fixture.live-source-claim/2.0.0` | `contracts/repository/live-source-claim-oracle.v2.schema.json` — `infinium.evaluation.live-source-claim-oracle/2.0.0` |
| `LLM-INVESTIGATE-LIVE-VAL-v2/2.0.0` | `contracts/repository/live-candidate-investigation-public-manifest.v2.schema.json` — `infinium.public-fixture.live-candidate-investigation/2.0.0` | `contracts/repository/live-candidate-investigation-oracle.v2.schema.json` — `infinium.evaluation.live-candidate-investigation-oracle/2.0.0` |
| `PROV-LIVE-COMPOSED-VAL-v2/2.0.0` | `contracts/repository/live-composed-provenance-public-manifest.v2.schema.json` — `infinium.public-fixture.live-composed-provenance/2.0.0` | `contracts/repository/live-composed-provenance-oracle.v2.schema.json` — `infinium.evaluation.live-composed-provenance-oracle/2.0.0` |

Every auxiliary v2 package file is also closed before implementation:

| File role | Exact schema file | Exact schema identity |
|---|---|---|
| Source-claim `execution-input.v2.json` | `contracts/repository/source-claim-execution-input.v2.schema.json` | `infinium.llm.source-claim-execution-input/v2` |
| Source-claim `context-manifest.v2.json` | `contracts/repository/source-claim-context.v2.schema.json` | `infinium.llm.source-claim-context/v2` |
| Source-claim `oracle-provenance.v2.json` | `contracts/repository/source-claim-oracle-provenance.v2.schema.json` | `infinium.evaluation.source-claim-oracle-provenance/v2` |
| Candidate `execution-input.v2.json` | `contracts/repository/candidate-investigation-execution-input.v2.schema.json` | `infinium.llm.candidate-investigation-execution-input/v2` |
| Candidate `context-manifest.v2.json` | `contracts/repository/candidate-investigation-context.v2.schema.json` | `infinium.llm.candidate-investigation-context/v2` |
| Candidate `oracle-provenance.v2.json` | `contracts/repository/candidate-investigation-oracle-provenance.v2.schema.json` | `infinium.evaluation.candidate-investigation-oracle-provenance/v2` |
| Both `partition-history.v2.json` files | `contracts/repository/public-fixture-partition-history.v2.schema.json` | `infinium.evaluation.fixture-partition-history/2.0.0` |

R1 also owns and registers these four coordinated repository campaign schema
successors:

- `contracts/repository/m1-slice6-campaign-stage-request.v2.schema.json` —
  `infinium.repository.m1-slice6-campaign-stage-request/2.0.0`;
- `contracts/repository/m1-slice6-campaign-stage-evidence.v2.schema.json` —
  `infinium.m1-s6.campaign-stage-evidence/v2`;
- `contracts/repository/m1-slice6-campaign-composed-evidence.v2.schema.json` —
  `infinium.m1-s6.campaign-composed-evidence/v2`;
- `contracts/repository/m1-slice6-finite-campaign-authorization.v2.schema.json`
  — `infinium.repository.m1-slice6-finite-campaign-authorization/2.0.0`.

R1 additionally owns
`contracts/repository/public-fixture-registry.v2.schema.json` with identity
`infinium.repository.public-fixture-registry/1.7.0` and its exact data file
`fixtures/public/public-fixture-registry.v2.json`.

R1 advances the registry to `1.7.0` with exactly 43 entries. R2 consumes every
new definition in one coordinated production candidate; it does not own or
silently revise fixture truth or schema identities.

Existing product contracts `infinium.llm.source-claim-extraction/v1`,
`infinium.llm.candidate-investigation/v1`, their prompts, and the accepted
SQLite schema 6/storage `1.5.0` remain unchanged unless implementation evidence
proves an actual field-shape defect. If such a defect appears, R2 must propose
and update the producer, consumer, persistence, wire/output, replay, fixture,
test, and documentation seams together; it may not add a compatibility alias
or silently reinterpret v1.

All repository schemas are governance/test metadata and never product inputs.
The product path receives only answer-free v2 inputs and admitted durable
artifacts.

## 10. Verification and acceptance

Before R1 acceptance:

- strict JSON and schema validation passes for every new file;
- registry count, uniqueness, path, byte-length, and SHA-256 closure passes;
- v1 directory hashes are unchanged from the planning base;
- answer-token and forbidden-field scans pass for product-visible inputs;
- every required state has exactly one expected disposition;
- the supported proposal is the only admissible proposal;
- WP10 planned IDs join exactly to WP11 positive evidence bindings;
- the negative remains materially matched and independently answerable;
- all five package-manifest fingerprints and all five oracle fingerprints are
  independently recomputed; and
- fresh fixture/oracle/provenance review returns `ACCEPT`.

Product comparison is prohibited until all of those facts are committed and
the R1 oracle-freeze transition is independently accepted. R1 acceptance is
public validation authority only. It is not a provider result, reliability
claim, private verdict, or effect authorization.

## 11. Acceptance integration

After the project owner accepts this amendment, R1 may add the five packages
and update together:

- this specification's owning catalog rows and Sections 10.4, 10.5, and 11.2;
- `fixtures/public/public-fixture-registry.v2.json` and
  `contracts/repository/public-fixture-registry.v2.schema.json`, plus
  resealer/reader/count tests while preserving both v1 files exactly;
- the Slice 6 plan's WP10/WP11/composed identities through the accepted
  remainder amendment; and
- current navigation and the append-only implementation record.

Acceptance does not itself freeze files that do not yet exist. Exact R1 input,
oracle, manifest, and registry bytes require their own independent freeze
evidence before product comparison.
