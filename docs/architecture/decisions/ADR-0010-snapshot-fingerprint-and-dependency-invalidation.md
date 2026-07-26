# ADR-0010: Snapshot fingerprint and dependency invalidation

Status: Accepted  
Date: 2026-07-25  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Infinium needs immutable installation snapshots and safe reuse at modlist
scales where mandatory hashing or copying of every byte is unnecessarily
expensive. RESEARCH-0012 showed that timestamps, metadata tuples, and file IDs
cannot establish content identity, while a quiescent structural capture plus
scoped strong hashes is practical for a bounded M1 proof.

## Decision

1. An installation snapshot uses a versioned canonical structural/provider
   manifest, scoped SHA-256 content identities, and a typed dependency graph.
2. Snapshot capture requires an explicitly selected quiescent MO2 profile,
   stable file handles where byte identity is consumed, same-stream hash/parse
   where practical, denied write/delete sharing, before/after handle checks,
   and double structural revalidation.
3. Metadata, timestamps, sizes, and file IDs are structural observations,
   change detectors, or optimizations. They are never sufficient proof of
   byte identity for byte-dependent artifacts.
4. Structural coverage and content-addressed coverage are represented
   separately. Snapshot assurance states disclose which declared populations
   are structurally captured, selectively content-sealed, fully byte-sealed,
   unsupported, inaccessible, or drifted.
5. Every reusable artifact declares the smallest complete dependency closure.
   Reuse preserves immutable origin and records a consuming-run reuse edge plus
   the validity proof. A global snapshot ID is not a universal cache key.
6. Container identity and archive-member dependencies are separate. Until
   member-level equivalence is proven, changing an archive container
   conservatively invalidates dependent member results.
7. Inaccessible, changing, ambiguous, unknown-reparse, unsupported-adapter,
   and unsupported-filesystem inputs fail or create explicit coverage gaps.
8. M1 shall not require filesystem watchers, USN journal access, VSS,
   whole-tree copying, mandatory per-entry open/file-ID acquisition, universal
   hashing, or a fully byte-sealed entire modlist.
9. A later read-only USN or other acceleration path may be added only after it
   proves continuity, privilege, mapping, fallback, and invalidation behavior.
   It remains an optimization rather than validity authority.
10. Canonical encoding, comparator behavior, node types, algorithm identifiers,
    retry limits, and immutable storage transaction are versioned semantic
    inputs. Changing them invalidates affected snapshots or artifacts.

## Qualification gates

Before an M1 plan or release relies on this mechanism:

- synthetic controls must cover same-size/time byte changes, metadata-only
  changes, rename/reorder cases, aliases, reparse points, inaccessible files,
  archive changes, and changed-during-capture behavior;
- EVAL-0013, EVAL-0014, EVAL-0024, EVAL-0026, EVAL-0037, EVAL-0051,
  EVAL-0052, EVAL-0053 when applicable, EVAL-0078, and EVAL-0083 must use the
  accepted dependency/invalidation semantics for the surfaces they exercise;
- the exact canonical schema and transaction boundary must be specified in
  the M1 plan; and
- RQ-027 retains upper-bound scale and performance calibration.

## Consequences

- Narrow analyzers can avoid hashing unrelated data while retaining defensible
  validity.
- Cache reuse becomes dependency-specific and explainable.
- Snapshot assurance is not reduced to one misleading complete/incomplete
  flag.
- Implementation requires careful canonicalization, race handling,
  dependency storage, and provenance.

## Validation

- A same-size, same-timestamp byte mutation invalidates every byte-dependent
  result.
- An unrelated change carries over only through an inspectable complete
  dependency proof.
- A mid-run edit never mutates an active snapshot/run binding.
- Reused artifacts retain their original producing snapshot/run identity.
- No optimization is allowed to convert uncertain continuity into a valid
  snapshot.

## References

- [ADR-0002 — Snapshot and context binding](ADR-0002-snapshot-context-binding.md)
- [ADR-0008 — MO2 profile and effective state](ADR-0008-mo2-profile-effective-state-and-local-identity.md)
- [RESEARCH-0012 — Snapshot fingerprints](../../research/investigations/RESEARCH-0012-snapshot-fingerprint-and-invalidation.md)
- [RESEARCH-0013 — Wave B integration](../../research/investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
