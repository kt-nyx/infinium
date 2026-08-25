# RESEARCH-0056 — Slice 6 live semantic authority conflict

Status: Completed

Disposition: Recommends the proposed Slice 6 remainder plan, public-fixture
authority amendment, and finite-campaign successor authority; no ADR

Date: 2026-08-16

Last reviewed: 2026-08-16

Researcher: Slice 6 planning lead

## 1. Question and requirements

Can the accepted `M1/S6` live sequence satisfy both of these requirements from
the repository's current frozen public authority?

1. WP10 exercises supported-positive, unsupported, conditional/version-scoped,
   contradictory, hostile, deleted, and abstention behavior and passes host
   admission; and
2. WP11 consumes the exact persisted admitted WP10 artifact and application
   link rather than synthesizing a parallel source graph.

The controlling requirements are `EVID-001`, `EVID-002`, `EVID-004`,
`EVID-006`, `EVID-007`, `SNAP-005`, `SNAP-006`, `AI-001` through `AI-007`,
`OPS-001` through `OPS-003`, `AUTH-002`, and `SEC-001` through `SEC-004`.
The controlling evaluation obligations are EVAL-0033 through EVAL-0035,
EVAL-0064, EVAL-0067, EVAL-0076, EVAL-0077, EVAL-0081, EVAL-0083, and
EVAL-0089 under continuation-profile Layers 1 through 4 and 6.

## 2. Scope and non-scope

This investigation uses only the active repository at clean branch
`codex/m1-s6`, HEAD `313ecfc04a22330c4c5dc52a79aae87d13982a74`, its accepted
documents, public fixtures, product code, tests, and Git history needed to
verify ancestry and current bindings.

It does not access private fixtures, sibling archives, retired protocols,
credentials, Credential Manager, DNS, public network, provider endpoints, or
billable operations. It does not create product code or a final executable
validation package.

## 3. Deterministic evidence

The current frozen live wrapper `LLM-CLAIM-LIVE-VAL/1.0.0` binds exact input
`S6-CLAIM-VAL-v1/1.0.0`. That input contains only:

- a contradictory Dune passage;
- a hostile instruction passage; and
- a deleted Ember passage.

Its frozen oracle contains eight harness scenarios with aggregate
`accepted_proposal_count: 0`, `admitted_correlation_count: 0`, and no admitted
proposal ID. This is consistent with the current host policy and is useful
negative/abstention evidence, but it cannot produce the one admitted artifact
required by the production campaign.

The accepted Slice 6 plan requires WP10 to pass a completed schema-valid live
semantic response through host validation/application and requires WP11 to
consume the exact admitted WP10 artifact and persisted application link.
Current production code enforces that dependency through the authoritative
SQLite source-claim admission/application records and composed-provenance
reopen checks. It correctly refuses to invent an artifact when WP10 admits
nothing.

HEAD `313ecfc04a22330c4c5dc52a79aae87d13982a74` already contains the bounded
late-settlement recovery and exact cumulative native-call correction recorded
at the current implementation-record tail. Those changes are required inputs
to the next coherent candidate; they do not resolve the semantic authority
conflict and must not be discarded or bypassed.

## 4. Findings

### F1 — The current v1 package cannot satisfy the accepted vertical path

Classification: owner/authority decision, resolved only by accepting a
clean-break public-fixture and plan amendment.

The current input supplies no independently supported passage. The oracle
correctly admits zero proposals. Changing product admission logic to accept
one of those proposals would violate the frozen oracle, source authority,
host-admission boundary, and answer isolation.

### F2 — Mutating or relabeling v1 is invalid

Classification: must preserve.

`LLM-CLAIM-LIVE-VAL/1.0.0`, `S6-CLAIM-VAL-v1/1.0.0`, their exact manifests,
inputs, transcripts, oracles, fingerprints, partition histories, and prior
non-live results are frozen historical evidence. A status edit may occur only
in a successor registry document; none of those package bytes may be edited,
reinterpreted, or reused under a new answer.

### F3 — A WP10-only wrapper change is incomplete

Classification: must fix in the proposed amendment.

The exact package identities are closed in repository schemas, coordinator
validators, request construction, stage evidence, composed evidence, offline
gates, campaign rehearsal, templates, registries, tests, and documentation.
WP11's current static validation package also carries source-acquisition and
application identities that do not describe a new v2 WP10 admission. A
coherent successor therefore needs new answer-free WP10 and WP11 input
packages, new live wrapper identities for both stages, and a new composed
provenance identity.

### F4 — No architecture decision is missing

Classification: non-blocking/no ADR.

ADR-0001 already makes model output an untrusted proposal; ADR-0002 and
ADR-0015 require immutable acquisition/application provenance and replay;
ADR-0013 and ADR-0025 already select the two strict Responses operations;
ADR-0018 through ADR-0021 already establish coordinator/helper authority and
secret isolation; and ADR-0023 already establishes atomic finite budgeting and
no retry after an ambiguous start. The correction chooses no new provider,
model, storage, process, security, or accounting architecture.

### F5 — The prior finite campaign cannot simply be edited and executed

Classification: owner/authority decision resolved by a successor authority.

The current campaign and credential expiries are immutable within their
accepted rollover grammar. Extending them and changing semantic package
identities requires a new successor campaign identity and a new exact
production-profile credential authorization. Historical campaign, credential,
review, admission, rollover, and evidence markers remain non-executable and
cannot be inherited.

## 5. Alternatives

1. Admit a v1 hostile, contradictory, or deleted proposal: rejected because it
   changes product meaning and violates the frozen oracle.
2. Let WP11 synthesize a source graph: rejected because it violates the exact
   persisted-predecessor requirement and provenance authority.
3. Mutate v1 in place: rejected because frozen validation evidence is
   immutable and the registry is closed-world.
4. Add only a new WP10 wrapper: rejected because WP11, composed provenance,
   schemas, consumers, and replay would remain bound to incompatible v1
   identities.
5. Create a new ADR: rejected because the existing accepted architecture
   already requires the recommended clean-break behavior.
6. Create a complete v2 public input/oracle family and successor campaign:
   recommended.

## 6. Recommended correction

Preserve all five current package trees and registry entries byte-for-byte and
add these new public validation identities:

- `S6-CLAIM-LIVE-VAL-v2/2.0.0` — answer-free WP10 input package;
- `LLM-CLAIM-LIVE-VAL-v2/2.0.0` — WP10 live wrapper and independent oracle;
- `S6-CANDIDATE-LIVE-VAL-v2/2.0.0` — answer-free WP11 positive/matched-negative
  input whose positive path binds the exact planned WP10 acquisition, proposal,
  admission, artifact identity/payload digest, and application identities;
- `LLM-INVESTIGATE-LIVE-VAL-v2/2.0.0` — WP11 live wrapper and independent
  typed-semantic oracle; and
- `PROV-LIVE-COMPOSED-VAL-v2/2.0.0` — no-fourth-call composed provenance
  wrapper and oracle.

The WP10 input must contain one genuinely supported, applicable passage whose
proposal alone can pass host admission, plus distinct unsupported,
conditional, version-scoped, contradictory, hostile, deleted, and insufficient
evidence/abstention cases. The oracle must require exactly one admitted
proposal and reject or abstain on every other case.

The WP11 positive must consume the exact durable WP10 source acquisition,
proposal, admission, admitted artifact identity/payload bytes and digest, and
application link. Its matched negative must
use independently authored host evidence and must not fabricate a second WP10
claim or application. Both contexts remain answer-free to the model, and the
oracle is frozen independently before product comparison.

The registry advances additively from current immutable
`fixtures/public/public-fixture-registry.v1.json` `1.6.0` to successor
`fixtures/public/public-fixture-registry.v2.json` `1.7.0`, with five new entries
and package count 43. All 38 existing entry objects remain exact, including
statuses; the successor campaign's explicit package binding makes them
non-executable for the replacement campaign.

## 7. Uncertainty and limitations

- Exact v2 passage text, offsets, byte lengths, file hashes, oracle result
  hashes, and manifest hashes must be authored and frozen by the R1 roles; they
  are deliberately not invented by this planning investigation.
- A live model response is nondeterministic. Passing requires the pre-authored
  typed tolerance and exact host admission, not prose equality.
- A post-start semantic failure cannot be retried inside the finite campaign.
- Official provider schemas, capability, price, and retention facts remain
  drift-prone and require the already planned pre-effect document check.
- This recommendation does not prove implementation correctness or authorize
  any external effect.

## 8. Follow-up enabled

The proposed
[`M1/S6 remainder plan`](../../plans/milestones/README.md),
the proposed
[`live semantic v2 fixture-authority amendment`](../../evaluation/evaluator-history.md),
and the proposed machine-readable
[`remainder authority amendment`](../../plans/milestones/README.md)
form one owner decision. Acceptance authorizes the bounded non-live R1-R3 work
and the exact gated finite campaign described there; until then, effect
authority remains none.
