# ADR-0006: GPL product and tool-dependency boundary

Status: Accepted
Date: 2026-07-25
Accepted: 2026-07-25
Last reviewed: 2026-07-25
Supersedes: None
Superseded by: ADR-0007 for xEdit-specific provisions only

Subsequent decision:

- 2026-07-25 — [ADR-0007](ADR-0007-exclude-xedit-from-infinium.md)
  removes xEdit from Infinium's product, development, dependency, integration,
  and evaluation boundaries. The xEdit statements below are retained as the
  original accepted decision history; they are no longer operative. All other
  ADR-0006 provisions remain accepted.
- 2026-07-25 — [ADR-0009](ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
  accepts the pinned Mutagen `0.54.2` boundary, and
  [ADR-0011](ADR-0011-loot-semantic-and-managed-data-boundary.md) accepts the
  conditional pinned libloot `0.29.6`/managed-data boundary. Their later
  technical decisions replace ADR-0006's candidate-only descriptions without
  changing its licensing or distribution decision.
- 2026-07-25 —
  [ADR-0008](ADR-0008-mo2-profile-effective-state-and-local-identity.md)
  rejects direct USVFS operation for the initial product because no M1 need was
  demonstrated. ADR-0006's licence analysis remains applicable if a later
  accepted decision reopens that component.

## Context

Infinium depends on established Skyrim tooling and may need GPL-family
libraries for its core analysis. The project owner wants anyone to be able to
use, modify, redistribute, or sell Infinium, but wants distributed derivatives
to preserve source access and the same downstream freedoms rather than become
closed proprietary forks. That intent is strong copyleft, not permissive
licensing.

[RESEARCH-0002](../../research/investigations/RESEARCH-0002-helper-tool-licensing.md)
establishes the checked upstream licences and distinguishes complete
user-facing applications, linked libraries, managed data, and first-party
helpers. Technical suitability and safe operation remain the responsibility of
the owning integration investigations.

## Decision drivers

- Infinium must remain free and open source for recipients of distributed
  derivatives.
- Commercial use, modification, forking, and redistribution are allowed.
- The project should use mature modding libraries when they materially improve
  correctness rather than recreating them solely to avoid copyleft.
- Users should retain ownership and version control of the common modding
  applications already present in their setup.
- Missing or unsupported external applications must reduce declared
  capabilities honestly rather than produce silent fallback behavior.
- Licensing compatibility does not prove technical fitness, correctness,
  read-only behavior, or safe execution.

## Considered options

### Option A — Permissive Infinium

MIT or Apache-2.0 would maximize downstream relicensing freedom but would allow
a distributed proprietary fork to withhold its modified source. Direct linkage
to the leading GPL libraries would also force a separate combined-work
decision. This does not match the owner's intent.

### Option B — MPL-2.0 or another weak-copyleft licence

File-level copyleft would preserve source for covered files while allowing a
larger work to contain differently licensed files. It would only partially
address the desired distributed-derivative protection and would not simplify
the leading GPL-library integrations.

### Option C — GPLv3-family Infinium

GPLv3 permits commercial use and redistribution while requiring distributed
derivatives to preserve GPL source and downstream freedoms. It also provides a
coherent licensing path for GPLv3 Mutagen.Bethesda, libloot, and USVFS if their
technical integrations are later accepted.

This option is selected.

### Option D — AGPLv3 or a noncommercial/source-available licence

AGPLv3 adds network-use source obligations that are unnecessary for the current
local desktop product. A noncommercial restriction would conflict with the
accepted free/open-source intent and with the owner's willingness to allow
commercial forks.

## Decision

1. Infinium-owned application and library code shall use the **GNU General
   Public License version 3 family**.
2. Commercial use, modification, forking, and redistribution remain allowed.
   Distributed derivatives must preserve the source access and recipient
   freedoms required by GPLv3.
3. The exact `GPL-3.0-only` versus `GPL-3.0-or-later` selector is deferred
   until the project needs its operative licence file. That release detail may
   not silently weaken or replace the accepted GPLv3 strong-copyleft posture.
4. MO2, LOOT, and xEdit are user-installed applications. Infinium shall not
   bundle, download, install, replace, or update them.
5. MO2 is required for the initial supported product. LOOT and xEdit are
   capability dependencies: Infinium may start and run unaffected analyzers
   without them while reporting the resulting gaps.
6. Initial setup and application settings shall attempt supported automatic
   detection, allow user confirmation and path override, validate executable
   identity/version/operation compatibility, and expose status and affected
   capabilities.
7. Mutagen.Bethesda is the leading bundled semantic-analysis library candidate.
   RQ-004 must still prove its Skyrim SE record, override, archive, strings,
   performance, version, and failure behavior before an implementation ADR or
   plan accepts the package and exact version.
8. The user-installed LOOT application is the preferred first integration
   boundary. Bundled libloot is a conditional fallback only if RQ-005 proves
   that supported LOOT executable invocation cannot provide the necessary
   structured, deterministic, non-mutating evidence.
9. Directly bundling or operating USVFS is disfavored. RQ-001 shall first
   compare deterministic effective-file reconstruction and bounded execution
   through the user's MO2. USVFS may enter an implementation only if evidence
   shows those approaches cannot supply required authoritative state and a
   separate accepted decision covers its operational risk.
10. LOOT masterlist and prelude are managed, versioned data rather than
    permanently bundled application payloads by default. Their acquisition,
    cache, offline behavior, exact revision, integrity, syntax compatibility,
    and provenance require the accepted LOOT/data contract. A future pinned
    offline seed remains possible but is not selected here.
11. First-party analysis workers, adapters, and integration scripts may be
    bundled with Infinium under its GPLv3-family licence.
12. Every selected library/runtime and transitive dependency must be compatible
    with the operative GPLv3 selector. Release artifacts must provide all
    required licences, notices, corresponding source, build and installation
    information, modification notices, and SBOM identity.
13. Tool or library selection never authorizes execution or setup mutation.
    AUTH-001 through AUTH-003 and the owning integration RQ remain independent
    gates.

## Accepted dependency posture

| Candidate | Distribution posture | Remaining technical gate |
|---|---|---|
| MO2 | Required user-installed application; never bundled or managed by Infinium | RQ-001/RQ-002 authoritative profile and effective-state contract |
| LOOT executable | User-installed application; preferred first LOOT boundary | RQ-005 supported structured, deterministic, non-mutating invocation |
| xEdit | User-installed application and ground-truth/optional-analysis tool | RQ-006 supported functions, invocation, writes, caches, and failure behavior |
| Mutagen.Bethesda | Leading bundled library candidate | RQ-004 capability, correctness, performance, failure, and version validation |
| libloot | Conditional bundled library candidate | RQ-005 must show user-installed LOOT is insufficient and direct library use adds necessary coverage |
| USVFS | Do not bundle or directly operate by default | RQ-001 must prove necessity after reconstruction and execution-through-MO2 alternatives |
| LOOT masterlist/prelude | Managed versioned CC0 data, not a fixed bundled payload by default | RQ-005 acquisition, compatibility, cache, integrity, and provenance contract |
| First-party workers/scripts | Bundled under Infinium's GPLv3-family licence | Applicable architecture, security, and evaluation gates |

These statuses are accepted constraints and provisional dependency choices, not
proof that any still-unvalidated technical mechanism works.

## Consequences

### Positive

- Distributed Infinium derivatives preserve GPLv3 source and recipient
  freedoms.
- Mutagen.Bethesda and libloot can be considered directly without inventing a
  licensing-only process boundary.
- Users keep their existing MO2, LOOT, and xEdit versions and configurations.
- Tool availability and coverage become explicit product state.
- USVFS complexity is not imported merely because MO2 uses it internally.

### Negative

- A distributed combined Infinium application cannot include
  GPL-incompatible dependencies.
- Binary releases require corresponding-source and notice discipline.
- User-installed applications introduce discovery, path, version, and
  reproducibility variation.
- Some analysis capabilities may be unavailable until a missing or unsupported
  external application is configured.
- Conditional dependency choices still require substantial technical research.

### Risks and mitigations

- **Unclear GPL selector:** choose `only` or `or-later` before the first
  operative licence/public code distribution and audit every dependency
  against it.
- **Tool version drift:** validate identity and supported version, capture exact
  tool provenance in scans, and fail with an explicit capability gap.
- **Loading private application internals:** never load a libloot or USVFS
  binary merely because it is present inside the user's MO2/LOOT installation;
  ship an accepted pinned library dependency or use the supported application
  boundary.
- **USVFS operational fragility:** prefer deterministic reconstruction or the
  user's MO2-managed execution and require an explicit necessity finding.
- **Stale LOOT data:** use controlled versioned acquisition/cache with scan
  provenance rather than an invisible frozen installer copy.
- **Licence drift:** lock dependencies and fail packaging when licence,
  corresponding-source, or notice evidence changes.

## Requirements affected

- TOOL-001 through TOOL-003
- DIST-001 through DIST-003
- AUTH-003
- ANALYSIS-002
- EVID-002
- SNAP-006
- COVER-001 through COVER-003
- OPS-001 and OPS-003

## Validation

- The application can detect and validate supported user-installed tool paths,
  accept explicit overrides, and report missing/unsupported/misconfigured
  states.
- Removing LOOT or xEdit does not prevent Infinium from starting; affected
  capabilities and coverage become unavailable explicitly.
- No installer or updater payload contains MO2, LOOT, or xEdit.
- Any accepted Mutagen/libloot/USVFS dependency has an exact capability
  experiment, immutable version, compatible licence tree, corresponding-source
  mechanism, notices, and provenance.
- RQ-001 evaluates effective-state alternatives before proposing direct USVFS
  operation.
- Every scan records exact external-tool, bundled-library, and managed-data
  identities used by its analyzers.
- A release-compliance fixture fails on a missing licence, notice, SBOM entry,
  immutable source, corresponding-source artifact, or required build/install
  information.

## References

- [RESEARCH-0002](../../research/investigations/RESEARCH-0002-helper-tool-licensing.md)
- [GNU GPL version 3](https://www.gnu.org/licenses/gpl-3.0.html)
- [GNU GPL FAQ](https://www.gnu.org/licenses/gpl-faq.en.html)
- [Mutagen repository](https://github.com/Mutagen-Modding/Mutagen)
- [libloot repository](https://github.com/loot/libloot)
- [USVFS repository](https://github.com/ModOrganizer2/usvfs)
- [LOOT documentation](https://loot.github.io/docs/)
