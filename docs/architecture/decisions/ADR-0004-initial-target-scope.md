# ADR-0004: Avoid premature manager/runtime abstraction

Status: Accepted  
Date: 2026-07-24  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Supporting several managers, runtimes, editions, and games would multiply
semantic and effective-state uncertainty before the Skyrim/MO2 product is
proven.

## Decision drivers

- The creator's real workflow supplies one concrete manager/game/platform
  target.
- Effective-state reconstruction and semantic analysis require deep
  target-specific validation.
- Premature abstraction would make unsupported environments appear more
  reliable than they are.
- Future reuse should remain possible without allowing hypothetical targets to
  distort the first implementation.

## Considered options

1. **Build manager/game/runtime abstractions before the first proof:** rejected
   because no second target exists to validate the abstraction.
2. **Support several Skyrim managers/runtimes as best-effort variants:**
   rejected because coverage and semantic guarantees would be ambiguous.
3. **Implement one concrete Windows/MO2/pinned-runtime target while keeping
   generic evidence concepts reusable:** selected.

## Decision

Initial adapters and semantic analyzers target one explicitly selected Mod
Organizer 2 profile using one deliberately pinned Skyrim Special Edition runtime
version on Windows desktop through M4.

Scans, targeted reanalysis, and independently run documentation acquisition are
manually initiated; configured child stages inside such an operation may run
automatically. Other managers, runtimes, editions, total conversions, and games
are deferred and treated as separate future targets.

Generic evidence/case concepts may remain reusable, but there is no generalized
manager/game/runtime abstraction until a second concrete target exists.

## Consequences

### Positive

- Exact profile reconstruction can be validated deeply.
- Domain analyzers avoid premature abstraction.
- Evaluation has a stable target.

### Negative

- The initial product is Windows/MO2/pinned-runtime-specific.
- Future targets may require new adapters and semantic models.

## Requirements affected

- SCOPE-001 through SCOPE-006

## Validation

- Unsupported targets fail clearly.
- No analyzer assumes behavior for an unvalidated runtime or manager.

## References

- [Product requirements](../../product/requirements.md)
- [Scope and milestones](../../product/scope-and-milestones.md)
- [Integration boundaries](../integrations.md)
