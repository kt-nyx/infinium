# M1 backend semantic proof plan revision 2 amendment

Status: Accepted
Accepted: 2026-08-01
Accepted by: Project owner
Last reviewed: 2026-08-10
Plan revision: `infinium.plan.m1.backend-semantic-proof/2`
Predecessor: [M1 backend semantic proof revision 1](../plan.md)

## Revision model

Revision `/2` incorporates the accepted predecessor plan except for the
Slice 3.5 replacements below. The predecessor file and all retained execution
bound to its SHA-256 remain unchanged. ADR-0026, RESEARCH-0052, semantic
specification revision `/2`, and evaluator-private fixture governance are
additional authority for this amendment.

## Slice 3.5 deliverable replacement

Replace the Slice 3.5 package/storage clauses with:

- Infinium tracks complete `BETH-NPC-DEV`, `BETH-REFR-DEV`,
  `BETH-LIGHT-VAL`, `BETH-MALFORMED-VAL`, and `BETH-UNSUPPORTED-VAL`
  development packages, retained generators/source descriptions, and exact
  emitted bytes;
- the separate evaluator-private Git history retains complete materially
  independent `BETH-LIGHT-VAL-002`, `BETH-MALFORMED-VAL-002`, and
  `BETH-UNSUPPORTED-VAL-002` validation packages and sealed `BETH-HO-002`;
- `BETH-HO-002` supersedes unavailable retained package `BETH-HO-001`; the
  byte-identical public v1 registry remains historical invalidation evidence;
- complete manifests, inputs, oracles, provenance, partition history, replay
  dependencies, redistribution decisions, construction/review evidence, and
  access records remain in the store appropriate to each partition;
- Infinium receives only sanitized private-store revision, document/package
  fingerprints, review/independence attestations, and supersession metadata;
- private work uses purpose-bound fresh-context delegated roles and returns no
  raw answer-bearing information to Slice 4 implementation context; and
- implementation records use private-store revision and location class, never
  a remote URL, credential, local path, payload locator, or raw result.

All original gates remain. This amendment qualifies inputs and independent
truth only; it does not execute or pass EVAL-0052/EVAL-0086, authorize live or
billable calls, or start Slice 4.
