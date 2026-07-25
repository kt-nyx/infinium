# ADR-0003: Exclude setup-mutation capabilities through M4

Status: Accepted  
Date: 2026-07-24  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Automated modlist changes are useful only after analysis is reliably grounded.
Premature write authority would amplify incorrect findings and complicate
trust, rollback, and testing.

## Decision drivers

- The first product must earn trust in its diagnosis before acting on it.
- Incorrect mutations can damage a modlist or invalidate a playthrough.
- Analyzer correctness, provenance, and verification should be testable
  independently of rollback/write safety.
- Product-owned persistence is necessary but must not become implicit setup
  authority.

## Considered options

1. **Autonomous remediation:** rejected through M4 because analytical errors
   would directly become setup changes.
2. **User-approved setup mutations in the initial product:** deferred because
   previews and approval do not solve immature analysis, rollback, or
   side-effect validation.
3. **Read-only advisor with isolated product-owned writes:** selected through
   M4.

## Decision

The architecture through M4 exposes no capability that modifies MO2, mod/plugin
priority or enabled state, plugins, game/mod files, configuration, or generated
output.

It may inspect state, run approved non-mutating tool operations, maintain its
own data, open relevant locations, and export reports.

Permitted writes are limited to the product-controlled, approved OS-backed, or
explicitly selected non-protected destinations defined by AUTH-002. Resolved
write destinations, including aliases and reparse points, must not overlap
protected setup roots. Approved external tools may use only documented,
isolated cache/temp behavior that cannot affect the user's setup.

It may delimit a test-session window around a launch performed by the user, but
product-initiated MO2/game launch is excluded through M4 because expected
runtime side effects require a later authority decision.

## Consequences

### Positive

- Analysis can be evaluated independently of mutation safety.
- Users retain control.
- Incorrect findings cannot directly damage a setup.
- The initial security boundary is smaller.

### Negative

- Resolutions occur outside the product.
- Verification must detect external changes.
- Some future workflows require a new authority ADR after M4.

## Requirements affected

- AUTH-001 through AUTH-003
- SEC-003
- FIND-004
- VALID-006 and VALID-007

## Validation

- Security and integration tests must prove no setup-mutating API is exposed.
- Every product write surface exercised—including user-selected exports,
  cache/temp storage, deletion, credential storage, and update staging—must
  reject destinations within protected setup roots and remain within the
  authority defined for its write class: product-controlled, approved
  OS-backed, or an explicitly selected non-protected export destination.
- External tool invocations must document allowed product- or tool-owned
  cache/temp effects and prove that approved operations do not mutate user setup
  state.

## References

- [Product requirements](../../product/requirements.md)
- [Security and privacy](../security-and-privacy.md)
- [Scope and milestones](../../product/scope-and-milestones.md)
