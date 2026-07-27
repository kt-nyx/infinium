# RESEARCH-0013: Wave B authoritative local-state integration

Status: Accepted  
Date: 2026-07-25  
Accepted: 2026-07-25  
Last reviewed: 2026-07-26
Researcher: Codex agent  
Review type: Independent Wave B integration  
Primary questions: RQ-001 through RQ-007 and RQ-014  
Decision enabled: Gate B disposition and accepted local-state, runtime,
semantic-layer, LOOT, identity, and snapshot ADR inputs

Acceptance:
The project owner accepts the integrated Wave B recommendations and the
**Gate B met with documented non-blocking gaps** result. ADR-0008 through
ADR-0011 make the selected boundaries authoritative. Acceptance advances the
architecture-decision transition below; it does not qualify an implementation,
pass EVAL-0046/EVAL-0051 through EVAL-0054, or broaden the bounded M1 envelope.

Accepted decision update:
[ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
resolves RQ-006 by rejecting the xEdit integration/oracle proposal recorded in
the original Wave B reports. This integrated disposition excludes xEdit from
Infinium and replaces the proposed oracle with independently specified,
first-party fixture truth. RESEARCH-0010 remains an input only as historical
evidence of the rejected option.

## 1. Scope, method, and authority

This report independently reviews and integrates:

- [the Wave B reference-environment manifest](WAVE-B-reference-environment-manifest.md);
- [RESEARCH-0005 — MO2 effective-state acquisition](RESEARCH-0005-mo2-effective-state-acquisition.md);
- [RESEARCH-0006 — MO2 profile-selection semantics](RESEARCH-0006-mo2-profile-selection-semantics.md);
- [RESEARCH-0007 — Skyrim runtime support](RESEARCH-0007-skyrim-runtime-support-contract.md);
- [RESEARCH-0008 — Mutagen semantic capability](RESEARCH-0008-mutagen-bethesda-semantic-capability.md);
- [RESEARCH-0009 — LOOT integration](RESEARCH-0009-loot-integration-and-data-contract.md);
- [RESEARCH-0010 — xEdit ground truth](RESEARCH-0010-xedit-ground-truth-and-invocation.md);
- [RESEARCH-0011 — MO2 identity and installer state](RESEARCH-0011-mo2-identity-installer-and-manual-state.md); and
- [RESEARCH-0012 — snapshot fingerprints](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md).

The review applied the accepted product baseline, ADR-0001 through ADR-0007,
the M0 Wave B required outputs and Gate B criteria, the integration/security/
snapshot documents, and EVAL-0046 and EVAL-0051 through EVAL-0054.

Research evidence, proposed mechanisms, accepted architecture, and passed
evaluation are different states:

```text
source/local evidence
  -> research question answered
  -> mechanism proposed
  -> ADR accepted
  -> implementation and operation qualified
  -> evaluation passed for an exact supported scope
```

Wave B acceptance reaches the architecture-decision transition through
ADR-0008 through ADR-0011. It does not qualify a production adapter or pass an
M1 evaluation.

### Independent checks performed

The review:

- traced each conclusion to an exact upstream source revision or local
  observation;
- independently queried the official upstream Git repositories on 2026-07-25
  and confirmed the material tag identities:
  - MO2 `v2.5.2` -> `9c130cbf2fc7225fb2916e46419af50671772aa0`;
  - Mutagen `0.54.2` ->
    `282bb99a77b2df7f1b092b06270e8e3c8fb55463`;
  - LOOT `0.29.1` ->
    `77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9`;
  - libloot `0.29.6` ->
    `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1`;
- independently queried official GitHub release metadata and confirmed that
  MO2 `v2.5.2`, Mutagen `0.54.2`, LOOT `0.29.1`, and libloot `0.29.6` were
  still the repositories' latest published releases on 2026-07-25;
- spot-checked exact source for MO2's `selected_profile`, LOOT's registered
  `--auto-sort`/`--loot-data-path` arguments, Mutagen's explicitly
  experimental archive-applicability path;
- checked the reports for drift from accepted read-only, explicit-selection,
  typed-evidence, snapshot, anti-overfitting, and taxonomy boundaries; and
- treated `Brain Blast Destruction 2024` only as a private, user-confirmed
  real-used shape/scale reference. It is not a correct, representative, or
  gold-standard modlist.

No MO2, Skyrim, LOOT, USVFS, installer, or protected user setup was
launched or changed by this integration review. Network effects were limited
to unauthenticated reads of official public repositories. Repository writes
are limited to this report.

## 2. Per-question answer and evidence-quality matrix

| RQ | Research answer | Strongest evidence | Evidence quality | Mechanism / evaluation state |
|---|---|---|---|---|
| RQ-001 | Use version-pinned, quiescent deterministic reconstruction of MO2 profile and provider state. Use MO2 only as a disposable conformance oracle. Real-instance `run` is not non-mutating, and direct USVFS has no demonstrated M1 necessity. | Exact MO2/USVFS source plus unchanged private aggregate observations | High for source behavior; medium for complete conformance | Boundary accepted by ADR-0008; EVAL-0051 not executed |
| RQ-002 | `General/selected_profile` is a per-instance saved-selection hint, not a current/live/last-played profile or analysis authority. Resolve the instance, suggest only a unique valid match, and require explicit user selection. | Exact MO2 and Qt behavior plus unchanged local INI/profile observation | High | Behavior accepted by ADR-0008; parser/global-instance fixtures pending |
| RQ-003 | Initial target is the exact local Steam Windows x64 `SkyrimSE.exe` `1.6.1170.0`, authorized by a versioned whole-file-hash support manifest. Version/PE/Steam/SKSE signals are supporting evidence only. | Local executable/base-data hashes, PE and Steam observations, one-byte negative control, Microsoft/Valve/SKSE primary sources | High for the private reference executable; moderate for public-copy coverage | Contract accepted by ADR-0009; EVAL-0054 not executed |
| RQ-004 | `Mutagen.Bethesda.Skyrim` `0.54.2` is the initial bounded plugin/low-level-BSA library, not an end-to-end MO2/archive/string authority. Allowlist only independently qualified record/field shapes. | Exact package/source graph, synthetic chain, base-master breadth, malformed cases, reproduced archived-string failure | High for capability/gap detection; low-to-medium for supported field breadth | Boundary accepted by ADR-0009; parser-independent fixture qualification and provider-aware strings unresolved |
| RQ-005 | LOOT 0.28.0/0.29.1 exposes no supported headless, structured, non-applying analysis boundary. A pinned narrow libloot 0.29.6 adapter is the accepted semantic route when LOOT coverage is delivered, with explicit immutable LOOT data and userlist inputs. | Exact LOOT source across two releases plus libloot 0.29.6 disposable metadata/sort probe | High for application rejection; medium for selected-profile libloot fidelity | Conditional delivery boundary accepted by ADR-0011; may be deferred from M1 |
| RQ-006 | Do not integrate xEdit in any product, development, dependency, or evaluation role. Use parser-independent first-party fixture truth for record-semantic qualification. | RESEARCH-0010 option analysis plus owner decision recorded in ADR-0007 | Accepted decision | Resolved by ADR-0007; historical integration/oracle proposal rejected |
| RQ-007 | Current physical mod state is distinct from mutable MO2 identity/provenance hints. Normal FOMOD choice history and general manual-change history are absent. Preserve ambiguity and model source mappings as versioned zero-to-many/many-to-zero relations. | Exact MO2 core/installer source plus sanitized private aggregate observations | High for MO2 core; medium-high for installer-plugin detail because source-to-binary mapping is incomplete | Boundary accepted by ADR-0008; bounded FOMOD reconstruction remains later work |
| RQ-014 | Use a versioned canonical structural/provider manifest, scoped same-stream SHA-256, quiescent double capture, and typed dependency closures. Metadata/file IDs are detectors or optimizations, not content proof. | Synthetic invalidation/share controls plus bounded 244,626-file/254.2-GB private-shape measurements | Medium-high for M1; medium for upper-bound/archive-member behavior | Mechanism accepted by ADR-0010; canonical encoding and controlled conformance pending |

All eight questions are resolved for M0 by ADR-0007 through ADR-0011. Their
implementation and exact supported surfaces remain subject to the named
qualification gates.

## 3. Cross-report consistency

| Boundary | Integrated conclusion | Consistency result |
|---|---|---|
| Suggested profile vs run binding | MO2's saved selection may suggest a target. Explicit confirmation and the sealed installation snapshot establish run authority. | Consistent across RESEARCH-0005, 0006, and 0012 |
| Persisted MO2 state vs runtime VFS | Disk/profile state, MO2's resolved static provider model, and one hooked process's runtime view are distinct. M1 uses quiescent reconstruction; runtime-only behavior is a gap. | Consistent; no report upgrades disk state into process-memory truth |
| MO2 provider model vs Mutagen | MO2 reconstruction owns effective paths/order. Mutagen receives exact inputs and owns only allowlisted binary semantics. | Consistent across RESEARCH-0005 and 0008 |
| Archives and strings | Low-level BSA reading is plausible. MO2/Skyrim archive activation, archive-member precedence, and provider-aware strings remain separate unresolved authority surfaces. | Consistent; RESEARCH-0008 correctly rejects its standard environment as authority |
| Mutagen validation boundary | Mutagen is the accepted bounded production semantic dependency and cannot be the sole author of its expected results. Hand-audited binary fixtures, direct byte/structure assertions, format invariants, negative/malformed cases, metamorphic variants, official-master invariants, and manual adjudication provide independent checks. | Consistent with ADR-0007 and ADR-0009; no circular Mutagen-generated fixture is called independent proof |
| LOOT application vs libloot | The accepted application-first posture was tested and found insufficient. ADR-0011 therefore accepts the narrow libloot route when LOOT coverage is delivered. | Consistent with ADR-0006 and ADR-0011 |
| LOOT data vs userlist | Curated masterlist/prelude, private userlist, local state, libloot output, and Infinium-derived diagnostics retain separate authority and exact revisions. | Consistent with ADR-0001 and EVID-003 |
| Local installed entity vs source identity | Physical snapshot identity is local; MO2/Nexus fields are mutable mapping evidence. A source mapping belongs to versioned context and can be many-to-many. | Consistent with ADR-0002 and DOC-003 |
| Runtime vs game data | Exact executable identity gates runtime-specific semantics. Base masters, archives, native components, and effective providers remain separate analyzer dependencies. | Consistent across RESEARCH-0007, 0008, and 0012 |
| Snapshot vs cache reuse | A snapshot records structural/content assurance by declared population. Each artifact uses its smallest complete dependency closure; a global snapshot ID is not a universal cache key. | Consistent with ADR-0002 |

No material contradiction was found. The most important apparent tension is
intentional: the reports define a route while leaving conformance execution
pending. That is not evidence that the route has already passed.

## 4. Wave B required-output coverage

| Required output | Coverage | Integrated assessment |
|---|---|---|
| Reproducible read-only environment/experiment manifest | Complete for the private reference environment | Exact binaries and profile controls are fingerprinted; private paths/content are excluded |
| Synthetic and controlled-real MO2 profiles or private manifests | Partial | Synthetic binary, LOOT, and filesystem probes exist; the private real-used profile has a sanitized manifest. A purpose-built synthetic MO2 oracle profile and small controlled-real MO2 profile have not yet been executed |
| Agreement/disagreement matrix against authoritative MO2 behavior | Partial | Source-derived authority/gap and proposed fixture matrices exist. Actual reconstruction-vs-MO2 controlled comparison has not run |
| Parser-independent record ground-truth specification | Defined at policy level; exact fixture corpus remains to be built | ADR-0007 defines the independent evidence families and anti-circularity rule; EVAL-0052 must instantiate them per supported record shape |
| MO2, Mutagen, and LOOT capability/gap matrices | Complete at research level | Each active boundary distinguishes direct capability, rejected authority, and unsupported state; xEdit is excluded rather than modeled as a capability |
| Snapshot/fingerprint benchmarks and invalidation examples | Complete for bounded M1 research | Real-shape measurement and synthetic same-time/size, rename, reorder, and container changes are recorded |
| Integration/semantic/snapshot ADR inputs | Complete and accepted | ADR-0008 through ADR-0011 record the selected boundaries |
| EVAL-0051, EVAL-0052, applicable EVAL-0053, EVAL-0054, and EVAL-0046 research prerequisites | Complete at the research-input level; full Wave F case specifications and execution remain pending | EVAL-0053 is conditional on M1 including LOOT; every invoked tool operation still needs its own EVAL-0046 pass |

The partial controlled-profile/oracle outputs are non-blocking for starting
independent Wave C research. They are blocking before accepting an M1
implementation plan that exercises the affected surface or claiming that an
M1 semantic field or provider surface is supported.

## 5. Exact M1 local-surface authority and gap map

The following is the accepted architecture envelope for the first backend
proof. It is not itself an accepted M1 implementation plan.

| M1 local surface | Accepted authority route | Current gap / admission rule |
|---|---|---|
| MO2 installation and instance | Exact executable/config identity under a versioned MO2 2.5.2 adapter | Unsupported version or ambiguous instance fails closed |
| Analysis profile | Explicit user choice of one canonical profile after optional saved-selection suggestion | No automatic binding; MO2 must be closed |
| Profile enablement and mod priority | Exact control-file bytes plus MO2-version reconciliation against discovered physical mod objects | Must pass controlled EVAL-0051 before adapter acceptance |
| Loose provider chains and winner | Version-pinned MO2 reconstruction over physical Data, enabled mod roots, secondary roots, overwrite, skip rules, and known mappings | Unknown mapper, reparse, inaccessible entry, or drift is a gap/failure |
| Enabled plugins and load order | Captured profile plugin/order inputs plus exact game-plugin semantics and winning plugin bytes | Duplicate/ambiguous/malformed state fails closed; controlled conformance pending |
| Plugin content | Guarded same-stream SHA-256 and parse from the exact sealed bytes | Every consumed byte must have a strong digest |
| Record chains, FormKeys, links, winners, and consumed fields | Mutagen 0.54.2 allowlist supplied with exact plugin order/bytes; independently specified fixture expectations | No field/shape enters supported scope until EVAL-0052 qualification; Mutagen may not solely author expected truth |
| Loose facegen/asset path used by initial proof | Version-pinned reconstruction route to a complete loose-provider chain plus winner content digest | M1 fixture must remain loose-only until archive precedence is qualified; controlled MO2 conformance remains mandatory |
| Physical game Data/unmanaged files | Direct structural observation and relevant scoped content hashes | Unknown ownership is represented, not guessed |
| Relevant profile configuration | Exact bytes plus a versioned supported parser/field contract | Arbitrary configuration semantics remain a gap |
| Installed-mod/source identity | Physical snapshot entity plus raw MO2 metadata observations and separately versioned source mapping | Nexus ID/version/archive hints never become unique identity automatically |
| Skyrim runtime | Exact candidate executable hash and versioned support-manifest consistency checks | Other/unknown hashes fail closed; semantic support remains analyzer-specific |
| Archives and effective archive members | RQ-001 provider population plus low-level reader and Skyrim/MO2 precedence contract | **Not admitted to initial M1 authority yet**; activation/member precedence remains unresolved |
| Archived localized strings | Provider-aware exact strings resolver validated against independent ground truth | **Not admitted yet**; Mutagen 0.54.2 standard lookup is known insufficient |
| Root/native components | Direct structural/content observations under later RQ-019 semantics | Outside the first proof unless the M1 plan explicitly adds and qualifies them |
| Generated output | Generic provider/current-state observation until a named adapter exists | Outside the first proof unless explicitly selected and qualified |

This envelope satisfies no final product-wide coverage claim. SCOPE-005 still
requires eventual accounting for all named local surfaces; unsupported
semantics remain visible coverage gaps.

## 6. Safety and non-mutation assessment

### Eligible read-only routes

- quiescent MO2/profile/config/provider reads;
- direct stable file reads, scoped hashing, and product-owned snapshot/cache
  writes outside protected roots;
- Mutagen/libloot read-and-compute calls over immutable or isolated inputs,
  subject to the accepted narrow boundaries and remaining qualification gates;
- exact runtime and metadata reads; and
- source/repository retrieval into disposable research or product-owned
  locations.

### Rejected or not-yet-qualified routes

- launching an observer through the user's real MO2;
- direct USVFS operation;
- LOOT `--auto-sort`, normal GUI/log/clipboard automation, or running LOOT
  through MO2;
- any xEdit detection, configuration, invocation, staging, copying, output
  parsing, capability reporting, or evaluation use under ADR-0007; and
- Mutagen's standard archive discovery/order/string environment as local-state
  authority.

The owner-authorized deletion of the obsolete `test profile` and repointing of
MO2's saved selection occurred before the investigations, while MO2 was closed,
and is recorded in the shared manifest. It is not an Infinium operation, an
approved research-tool side effect, or evidence that the product may mutate
profiles.

No Wave B result selects an external-tool operation known to write protected
setup state. xEdit's historical proposed oracle mode is rejected, not merely
unqualified. LOOT may be omitted from M1 without weakening unrelated
capability.

## 7. Gate B assessment

| Gate B criterion | Result | Rationale |
|---|---|---|
| Defensible route to exact effective state for every local surface exercised by M1 | **Met with scope constraints** | A bounded M1 can be designed around MO2 2.5.2 quiescent reconstruction, scoped strong hashes, independently qualified allowlisted plugin semantics, and loose-only asset evidence. Archives, archived strings, unqualified fields, and unknown mappings are excluded rather than guessed |
| Unsupported or unobservable state has explicit gap semantics | **Met** | Every report defines fail-closed unsupported, ambiguous, unavailable, drifted, or unqualified outcomes |
| No chosen external-tool operation is known to mutate protected setup state | **Met** | Mutating/unknown real-instance operations are rejected, and xEdit has no Infinium operation under ADR-0007 |
| Snapshot validity uses declared dependencies and measured behavior | **Met** | Same-size/time mutation controls disprove metadata-only reuse; ADR-0010 accepts structural roots, scoped SHA-256, double capture, and dependency closures |

### Overall result

**Gate B is Met with documented non-blocking gaps for progression to the next
independent M0 research wave.**

This result means:

- the project has defensible authority routes and honest fail-closed boundaries
  for a deliberately restricted M1 proof;
- no LLM or heuristic fallback is needed to compensate for unknown local
  state; and
- Wave C research may proceed without accepting a stack or integration.

It does **not** mean:

- EVAL-0051, EVAL-0052, EVAL-0053, EVAL-0054, or EVAL-0046 passed;
- deterministic reconstruction already agrees with MO2 on controlled
  profiles;
- Mutagen fields already agree with independently specified fixture truth;
- archive activation, archive-member precedence, or localized strings are
  supported;
- libloot operations are production-qualified; or
- an M1 implementation plan or support claim may be accepted without the
  applicable closure work below.

If a future M1 plan expands beyond the bounded envelope in section 5, the
corresponding gap becomes Gate-B-blocking for that plan and the gate must be
reassessed.

## 8. Residual risks and exact closure work

### Blocking before affected M1-plan or support-claim acceptance

1. Create a purpose-built disposable MO2 2.5.2 synthetic oracle instance and
   execute the EVAL-0051 provider/reconciliation/hidden/unmanaged/mapper
   matrix, recording actual agreement and disagreement.
2. Create at least one small controlled-real MO2 profile after synthetic
   behavior is understood. Do not use the creator's large profile as its
   correctness oracle.
3. Build parser-independent EVAL-0052 fixtures using hand-audited binary
   inputs, direct byte/structure assertions, format invariants, matched
   negative and malformed cases, metamorphic variants, official-master
   invariants, and documented manual adjudication.
4. Execute EVAL-0052 for every record family, field, link, override shape, and
   localization state proposed for M1. An unqualified shape remains excluded.
5. Define and validate the exact canonical MO2/provider-manifest comparator and
   encoding, including case, Unicode, duplicates, inaccessible paths, reparse
   points, and change-during-capture behavior.
6. Resolve the supported MO2 Skyrim game-plugin and mapper inventory required
   by the M1 fixture.
7. Keep archived assets and archived localized fields outside M1 unless the
   provider-aware archive/string follow-up proves exact precedence and
   independent agreement.
8. If M1 includes LOOT, select the exact libloot binding/worker/data contract
   and pass EVAL-0053 plus EVAL-0046. Otherwise state LOOT coverage as deferred.

### Non-blocking for Wave C research

- automatic MO2 saved-profile suggestion;
- broad public coverage of every legitimate Steam `1.6.1170.0` executable
  hash;
- FOMOD historical reconstruction;
- complete byte sealing of every unrelated loose file;
- USN/VSS acceleration;
- high-end user timing estimates and M3 performance calibration;
- other runtimes, managers, games, and platforms.

## 9. Accepted downstream changes

The owner accepts these changes. ADR-0008 through ADR-0011 apply the
architecture decisions; the registries, plan, and evaluation catalog record
their resulting status.

### RQ registry

Mark RQ-001 through RQ-007 and RQ-014 resolved for M0 by ADR-0007 through
ADR-0011. Automatic saved-profile suggestion remains deferrable. LOOT delivery
remains conditional on a milestone claiming LOOT-backed coverage.

### ADRs

The accepted cohesive ADR set is:

1. **ADR-0008 — MO2 profile, effective-state, and local-identity acquisition**
   - explicit target and saved-selection hint;
   - quiescent versioned reconstruction;
   - provider/mapping authority and gaps;
   - local installed entity versus source mapping;
   - rejection of real-instance execution and direct USVFS.
2. **ADR-0009 — Skyrim runtime and Bethesda semantic support**
   - versioned runtime-support manifest;
   - Mutagen package/version/lock and allowlist;
   - parser-independent fixture and semantic-qualification boundary;
   - archive/string exclusions and version-advance gates.
3. **ADR-0010 — Snapshot fingerprint, dependency, and invalidation**
   - canonical structural manifest;
   - scoped content identity and sealing states;
   - dependency closures and reuse proofs.
4. **ADR-0011 — LOOT semantic and managed-data boundary**, delivered only when
   a milestone claims LOOT coverage
   - application rejection/reopen condition;
   - exact libloot adapter;
   - masterlist/prelude/userlist authority and immutable acquisition.

No stack, process topology, persistence technology, or IPC decision is
accepted by these ADRs.

### Evaluation specifications

- Expand EVAL-0051 with the exact synthetic MO2 conformance matrix in
  RESEARCH-0005, 0006, and 0011.
- Replace RESEARCH-0010's rejected staged-oracle procedure with ADR-0007's
  parser-independent EVAL-0052 specification. Apply EVAL-0046 only to external
  tools that remain in product scope.
- Refine EVAL-0054 with exact-hash, same-version/unknown-hash, channel,
  malformed, unreadable, and capture-race cases.
- Adopt EVAL-0053 only if M1 includes LOOT; otherwise retain it as the first
  LOOT-delivery gate.
- Add snapshot invalidation cases from RESEARCH-0012 to EVAL-0013,
  EVAL-0014, EVAL-0024, EVAL-0026, EVAL-0037, EVAL-0078, and EVAL-0083.
- Preserve synthetic atomic fixtures first, then small controlled real-mod
  profiles, with matched negatives and metamorphic variants.

### Product/domain/taxonomy

- Clarify, through the normal accepted product-change process, the distinction
  between local installed entity and many-to-many source identity mapping.
- Prefer the user-facing term **MO2 saved selection** over “current profile.”
- Add typed current-state, identity, archive, installer-history, mapper,
  localization, and capture-assurance gaps where the existing domain model
  needs them.
- Do not derive mod-purpose, affected-game-area, consequence, severity,
  symptom, or effect-extent categories from MO2 object types, plugin record
  groups, file extensions, or adapter ownership. The separately accepted
  [Skyrim SE mod-impact taxonomy](../../product/mod-impact-taxonomy.md) is
  authoritative for those classifications.

### Source registry

Register exact MO2, Mutagen,
LOOT/libloot/data, Microsoft filesystem/PE, Valve, and SKSE authorities
with their claim scopes, versions, freshness, and reversal triggers. Do not
register a moving branch as a replay identity.

### Plan amendments and next-wave prerequisites

- Record this accepted Gate B result and its bounded M1 envelope in the M0
  plan.
- Wave C may begin using the exact runtime/MO2/semantic gaps here as inputs.
- Wave C must not turn record families, assets, or the first NPC scenario into
  the RQ-036 taxonomy.
- Before Wave E selects a process/security architecture, carry forward the
  libloot worker isolation, path/reparse containment, and protected-root
  requirements.
- The M1 plan must link every exercised local surface to an accepted authority
  ADR and a passed exact-scope evaluation case.

## 10. Requirements and evidence traceability

| Requirement / decision | Integrated evidence | Result |
|---|---|---|
| SCOPE-001, ADR-0004 | RESEARCH-0007 exact runtime identity and rejection states | ADR-0009 accepts one exact runtime only; no best-effort variant |
| SCOPE-002, SCOPE-003 | RESEARCH-0005/0006 exact instance/profile route | Explicit one-profile binding; saved selection is only a hint |
| SCOPE-005 | Section 5 integrated surface map | Every M1 surface has a route, exclusion, or explicit gap |
| AUTH-001 through AUTH-003, ADR-0003 | Cross-report side-effect matrices and section 6 | No protected-state-mutating operation selected |
| SNAP-001 through SNAP-006, ADR-0002 | RESEARCH-0012 dependency/capture model | Immutable origin, drift invalidation, scoped reuse, honest assurance/replay |
| EVID-001 through EVID-006, ADR-0001 | MO2/Mutagen/LOOT authority separation plus independent fixture truth | Observations, interpretations, tool results, mappings, and gaps stay typed |
| ANALYSIS-002 | RESEARCH-0009 and ADR-0007 | Reuse LOOT semantics through a qualified boundary; exclude xEdit |
| ANALYSIS-003 | RESEARCH-0008 and ADR-0007 | Allowlisted record semantics require parser-independent ground truth |
| ANALYSIS-005, ANALYSIS-006 | RESEARCH-0005/0008 | Provider chains precede meaningful asset/archive analysis |
| ANALYSIS-008, ANALYSIS-009 | RESEARCH-0007 | Runtime identity remains separate from native/data compatibility |
| ANALYSIS-012, DOC-003 | RESEARCH-0011 | Identity/FOMOD uncertainty is explicit and correctable |
| TOOL-001 through TOOL-003, ADR-0006, ADR-0009, ADR-0011 | Tool/version/capability matrices | User applications remain user-installed; Mutagen/libloot semantic boundaries are accepted but supported operations remain qualification-gated |
| COVER-001 through COVER-003 | Every report's unsupported/gap contract | No unqualified surface becomes fabricated coverage |
| OPS-004 | RESEARCH-0012 bounded real-shape measurement | M1 strategy is plausibly bounded; M3 scale calibration remains open |
| EVAL-0046 | RESEARCH-0005/0009 safety procedures | ADR qualification obligation accepted; full case specification and execution pending |
| EVAL-0051 | RESEARCH-0005/0006/0011 | Research matrix exists and ADR qualification obligation is accepted; full case specification and execution pending |
| EVAL-0052 | RESEARCH-0008 and ADR-0007/0009 | Parser-independent fixture policy accepted; corpus, full case specification, and execution pending |
| EVAL-0053 | RESEARCH-0009 and ADR-0011 | Conditional ADR qualification obligation accepted; full case specification and execution pending if LOOT enters M1 |
| EVAL-0054 | RESEARCH-0007 and ADR-0009 | Qualification obligation accepted; full case specification and execution pending |
| RQ-036 | All reports preserve technical-surface versus game-area distinctions | Wave B creates no product taxonomy |

## 11. Corrections made to input reports

ADR-0007 supersedes the xEdit-specific recommendations in RESEARCH-0002,
RESEARCH-0004, RESEARCH-0008, RESEARCH-0010, and the original version of this
integration report. Historical reports retain explicit supersession notices
rather than silently rewriting the option analysis. Other important
limitations remain at their correct authority level:

- the private real-used profile is non-normative;
- source tracing is not a controlled conformance pass;
- a Mutagen round trip is not independent ground truth;
- Mutagen-authored expected output is not independent ground truth;
- the accepted libloot boundary is not a qualified adapter operation; and
- a structurally indexed population is not fully byte-sealed.

## 12. Conclusion

Wave B establishes a coherent local-state direction:

```text
explicit MO2 instance/profile
  -> quiescent version-pinned provider reconstruction
  -> dependency-scoped strong input capture
  -> exact runtime gate
  -> allowlisted Mutagen plugin semantics
  -> parser-independent first-party fixture conformance
  -> typed observations, gaps, and provenance
```

LOOT is optional for the first proof and should use a narrow pinned libloot
boundary only if its milestone claims LOOT coverage. Archives and localized
strings remain excluded from supported M1 semantics until provider-aware
ground truth exists.

The Wave B result is therefore:

> **Met with documented non-blocking gaps for M0 research progression.**
> Controlled MO2 conformance and parser-independent field/provider
> qualification are still mandatory before the corresponding M1
> implementation or support claims can be accepted.
