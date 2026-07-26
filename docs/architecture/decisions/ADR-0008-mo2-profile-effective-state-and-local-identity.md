# ADR-0008: MO2 profile, effective-state, and local-identity acquisition

Status: Accepted  
Date: 2026-07-25  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Infinium must reconstruct the effective state of one selected Mod Organizer 2
profile without mutating or executing through the user's live setup.
[RESEARCH-0005](../../research/investigations/RESEARCH-0005-mo2-effective-state-acquisition.md),
[RESEARCH-0006](../../research/investigations/RESEARCH-0006-mo2-profile-selection-semantics.md),
and
[RESEARCH-0011](../../research/investigations/RESEARCH-0011-mo2-identity-installer-and-manual-state.md)
show that MO2's disk state can support a versioned deterministic
reconstruction, but that saved profile selection, current physical mod state,
source identity, and installer history are different claims.

RESEARCH-0013 reviewed these results together and found a defensible route for
a bounded M1 proof. Controlled conformance remains unexecuted, so accepting
the architecture does not qualify an implementation or a provider surface.

## Decision

1. Infinium shall use a version-pinned MO2 adapter, initially targeting MO2
   `2.5.2`, to reconstruct selected-profile state from validated disk,
   configuration, and physical-provider inputs.
2. One canonical MO2 instance and profile must be explicitly selected for each
   authoritative installation snapshot. MO2 must be closed during capture.
3. `General/selected_profile` is labeled **MO2 saved selection**. It may
   suggest one unique valid profile only after instance resolution; it never
   establishes the analysis target or run binding.
4. The adapter shall reconstruct and retain:
   - profile enablement and mod priority;
   - enabled plugins and captured order inputs;
   - complete ordered loose-file provider chains and winners;
   - physical Data, enabled-mod, secondary-root, overwrite, hidden/skipped,
     unmanaged, and supported mapping contributions; and
   - raw control/configuration observations separately from derived state.
5. Unsupported MO2 versions, unknown mappers, inaccessible or ambiguous
   objects, path collisions, and capture drift shall fail or create explicit
   coverage gaps. They shall not be guessed away.
6. Infinium shall not launch a helper through the user's real MO2, install or
   load an MO2 plugin, or directly operate USVFS for the initial product.
   Disposable MO2 instances may be used only for controlled conformance.
7. A **local installed entity** is the physical snapshot entity represented by
   the captured mod state. A **source identity mapping** is separate,
   versioned evidence and may be zero-to-many or many-to-zero.
8. MO2 `meta.ini`, download sidecars, declared versions, Nexus identifiers,
   folder names, and archive names are mapping evidence rather than unique
   installed-entity keys.
9. Normal FOMOD choice history, general reinstall/merge history, and manual
   change attribution are unavailable unless separate retained evidence proves
   them. Bounded present-state installer compatibility may be reported later
   with ambiguity; it shall not be called historical reconstruction.
10. The exact M1 adapter surface is positively allowlisted. New MO2 versions,
    mappers, installer plugins, or runtime-only VFS surfaces require explicit
    qualification and, when the authority boundary changes, a new or
    superseding ADR.

## Qualification gates

This ADR accepts the boundary and initial target; it does not claim conformance
has passed. Before an M1 plan or release may call an exercised surface
supported:

- EVAL-0051 must compare the reconstruction with authoritative behavior in
  synthetic atomic and small controlled-real disposable profiles;
- EVAL-0046 must prove that production acquisition does not write protected
  setup state or launch MO2/USVFS;
- the supported mapper/game-plugin inventory and canonical path behavior must
  be explicit;
- capture dependencies must follow ADR-0010; and
- unsupported archive/provider behavior must remain excluded or gapped.

## Consequences

- Infinium can acquire useful MO2 state without making the live VFS or a
  privileged MO2 plugin a product dependency.
- Saved selection improves onboarding without silently choosing the user's
  analysis target.
- Identity ambiguity and missing installer history remain visible rather than
  being converted into invented certainty.
- The adapter must reproduce substantial MO2 behavior and maintain a narrow
  versioned capability matrix.

## Validation

- No authoritative capture begins while MO2 is running.
- A stale, malformed, missing, or ambiguous saved selection never creates a
  run.
- Controlled fixtures cover provider priority, Data/unmanaged state,
  overwrite, hidden/skipped state, duplicates, renamed mods, unsupported
  mappings, and changed-during-capture behavior.
- Correcting a source mapping creates a new analysis-context revision without
  rewriting the physical installation snapshot.
- Removing USVFS and any MO2 plugin from a developer or user machine changes
  no supported Infinium production capability.

## References

- [ADR-0002 — Snapshot and context binding](ADR-0002-snapshot-context-binding.md)
- [ADR-0003 — Read-only authority](ADR-0003-read-only-authority.md)
- [RESEARCH-0005 — MO2 effective-state acquisition](../../research/investigations/RESEARCH-0005-mo2-effective-state-acquisition.md)
- [RESEARCH-0006 — MO2 profile-selection semantics](../../research/investigations/RESEARCH-0006-mo2-profile-selection-semantics.md)
- [RESEARCH-0011 — MO2 identity and installer state](../../research/investigations/RESEARCH-0011-mo2-identity-installer-and-manual-state.md)
- [RESEARCH-0013 — Wave B integration](../../research/investigations/RESEARCH-0013-wave-b-authoritative-local-state-integration.md)
