# M1 semantic and local-ground-truth specification revision 2 amendment

Status: Accepted
Accepted: 2026-08-01
Accepted by: Project owner
Last reviewed: 2026-08-01
Specification set: `infinium.eval.m1.semantic-and-ground-truth/2`
Predecessor: [`infinium.eval.m1.semantic-and-ground-truth/1`](m1-semantic-and-ground-truth.md)

## Revision model

This accepted amendment creates revision `/2` without rewriting retained
execution or claims bound to revision `/1`. Revision `/2` incorporates `/1`
except for the two replacements below. ADR-0026 and
[evaluator-private fixture governance](../evaluator-private-fixture-governance.md)
are controlling authority for those replacements. All other cases, assertions,
support boundaries, and non-pass claims remain unchanged.

## Replacement for Section 2.3 private-fixture access

Expected results remain pre-registered in a separately access-controlled oracle
and are compared only after immutable output publication. Complete validation
and held-out packages use ADR-0026's separate private Git store.

An ordinary implementation agent may autonomously delegate scoring,
maintenance, or independent replacement work to a fresh-context evaluator
under the canonical governance policy, but receives no raw input, oracle, or
answer-bearing output. A private answer reveal or result-driven production
tuning contaminates and reclassifies that fixture version and requires a
materially independent private replacement.

The existing prohibitions on production-authored truth, Mutagen/xEdit-derived
answers, non-independent oracle methods, and answer-bearing execution input
remain in force.

## Replacement for EVAL-0052 fixture partition and input

The EVAL-0052 fixture set is:

- development `BETH-NPC-DEV`, `BETH-REFR-DEV`, `BETH-LIGHT-VAL`,
  `BETH-MALFORMED-VAL`, and `BETH-UNSUPPORTED-VAL`;
- evaluator-private validation `BETH-LIGHT-VAL-002`,
  `BETH-MALFORMED-VAL-002`, and `BETH-UNSUPPORTED-VAL-002`;
- controlled-real validation projections from RESEARCH-0035; and
- sealed evaluator-private `BETH-HO-002`, which supersedes the invalidated
  `BETH-HO-001` registry entry after its retained complete package proved
  unavailable during ADR-0026 migration.

The three public predecessor versions retain their immutable answer-bearing
bytes as development/regression evidence. Their partition histories and the
sanitized evaluator-private registry bind the materially independent
replacements. This revision does not execute Slice 4 production semantics or
claim EVAL-0052, EVAL-0086, or M1 passed.

The byte-identical public v1 held-out registry is retained as a historical
artifact. Revision `/2` does not accept its former complete-retention claim as
evidence; only the independently authored and reviewed successor may supply the
held-out obligation.
