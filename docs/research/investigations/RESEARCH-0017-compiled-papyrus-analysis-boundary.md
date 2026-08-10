# RESEARCH-0017: Compiled Papyrus analysis boundary

Status: Completed
Disposition: recommendation accepted by project owner
Date: 2026-07-25
Last reviewed: 2026-08-10
Researcher: Codex agent
Primary question: RQ-022
M0 wave: C
Decision enabled: Bounded compiled-Papyrus analyzer contract, candidate
generation rules, evaluation cases, and RQ-036 technical-surface evidence

Acceptance note: The project owner accepted this bounded static-analysis
recommendation on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
Proposal-era taxonomy/status wording below is retained as provenance; generic
runtime behavior and performance claims remain excluded.

## 1. Question and requirements

**RQ-022:** How far can compiled Papyrus be analyzed structurally and
semantically without unreliable claims?

The answer is a deliberately bounded **static** analyzer. Skyrim PEX files
retain substantially more than hashes or filenames: class and inheritance
names, properties, states, function signatures and flags, local variables, and
bytecode instructions are recoverable. Those facts support useful
provider-aware API, dependency, and cross-layer compatibility candidates.
They do not prove the behavior, safety, performance, or authorial intent of a
script in the live game.

This investigation is governed by:

- [SCOPE-005](../../product/requirements.md#scope-005--effective-installation),
  including effective scripts and explicit coverage gaps;
- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety),
  which keep analysis read-only and constrain external operations;
- [ANALYSIS-005](../../product/requirements.md#analysis-005--cross-record-and-cross-layer-reasoning),
  [ANALYSIS-008](../../product/requirements.md#analysis-008--version-coherence),
  [ANALYSIS-013](../../product/requirements.md#analysis-013--missing-referenced-assets),
  [ANALYSIS-016](../../product/requirements.md#analysis-016--declared-analyzer-contract),
  and
  [ANALYSIS-017](../../product/requirements.md#analysis-017--candidate-first-llm-escalation);
- [EVID-001 through EVID-004](../../product/requirements.md#evidence-and-trust),
  which require typed evidence, provenance, and abstention;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md),
  the accepted effective-provider boundary;
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md),
  including pinned Mutagen `0.54.2` and field-qualified Bethesda semantics;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md),
  which requires hashes, dependency closure, and scoped invalidation; and
- [the accepted M0 plan](../../plans/milestones/m0/plan.md#wave-c--analysis-surfaces-taxonomy-corpus-and-candidate-scale),
  which permits this bounded survey without requiring a complete named-script
  analyzer roadmap.

Relevant existing evaluation boundaries include
[EVAL-0032](../../evaluation/case-catalog.md),
[EVAL-0046](../../evaluation/case-catalog.md),
[EVAL-0051](../../evaluation/case-catalog.md),
[EVAL-0052](../../evaluation/case-catalog.md),
[EVAL-0061](../../evaluation/case-catalog.md),
[EVAL-0065](../../evaluation/case-catalog.md),
[EVAL-0083](../../evaluation/case-catalog.md), and
[EVAL-0085](../../evaluation/case-catalog.md). Section 10 proposes a dedicated
compiled-Papyrus evaluation family.

## 2. Scope and explicit non-scope

### In scope

- accessible effective Skyrim SE `Scripts/*.pex` artifacts and their
  provider/winner relationships under ADR-0008;
- lower providers at the same effective path, when retained as comparison
  evidence rather than mistaken for winners;
- the Skyrim PEX header, string table, debug section, user flags, objects,
  inheritance names, variables, properties, states, functions, and bytecode;
- static instruction, literal, type-name, property-access, and call-site
  observations;
- bounded intraprocedural control-flow reconstruction from explicit jumps;
- public-surface and dependency fingerprints used to compare competing PEX
  providers;
- cross-layer candidates that join qualified PEX facts with qualified plugin
  VMAD script attachments, property data, or fragment names;
- co-located Papyrus source and debug-information availability as provenance
  and coverage facts;
- malformed, unsupported, inaccessible, unstable, and resource-limited
  outcomes;
- exact-byte, structure, candidate, finding, and abstention boundaries; and
- sanitized shape evidence from one user-confirmed real MO2 profile.

### Out of scope

- recovering the exact original `.psc`, including comments, formatting,
  imports, source ordering, or compiler transformations;
- inferring a compiler identity or version that is not encoded in the file;
- proving arbitrary behavioral correctness, compatibility, safety,
  thread-safety, determinism, quest safety, save safety, or author intent;
- executing bytecode, loading native components, launching Skyrim, MO2, or a
  helper tool, or modifying the installation;
- resolving live object bindings, alias values, registrations, current states,
  suspended stacks, save-persisted state, event ordering, or runtime frequency;
- determining the implementation or availability of a `native` function from
  the PEX declaration alone;
- reporting performance problems from file size, instruction count, call
  count, or a generic “script-heavy” heuristic;
- defining mod purpose, affected game area, consequence, severity, likely
  symptom, or extent solely from a PEX file or script filename; those remain
  separate RQ-036 taxonomy axes;
- a complete catalog of SKSE/native Papyrus extensions or every named script
  framework;
- selecting production architecture merely because a capable library surface
  exists; and
- treating the private reference profile as correct, representative, or a
  source of profile-specific production rules.

## 3. Sources and exact identities

Sources were retrieved on 2026-07-25. Exact revisions are used where
available. The Creation Kit wiki pages are Bethesda-origin language/runtime
documentation preserved by UESP, but are moving community-hosted pages rather
than immutable source revisions.

| Source | Exact identity | Authority | Claim-level relevance |
|---|---|---|---|
| [Mutagen `0.54.2`](https://github.com/Mutagen-Modding/Mutagen/tree/282bb99a77b2df7f1b092b06270e8e3c8fb55463) | Tag `0.54.2`; commit `282bb99a77b2df7f1b092b06270e8e3c8fb55463` | Accepted, pinned project dependency source | Existing `Mutagen.Bethesda.Pex` reader/model, Skyrim PEX test corpus, and VMAD model |
| [Mutagen PEX reader](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Pex/DataTypes/PexFile.cs), [PEX object model](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Pex/DataTypes/PexObject.cs), and [tests](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core.UnitTests/Pex/PexTests.cs) | Same pinned commit | Primary implementation evidence for this project’s selected dependency | Parsed header, object, function, property, state, instruction, round-trip, and fixture coverage |
| [Mutagen Skyrim VMAD model](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Common%20Subrecords/VirtualMachineAdapter.xml) and [reader](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Common%20Subrecords/AVirtualMachineAdapter.cs) | Same pinned commit | Primary implementation evidence for this project’s selected dependency | Script names, properties and typed values, and fragment script/function names available from plugin records |
| [Caprica](https://github.com/Orvid/Caprica/tree/2042c902ec269e33c1061ccd8aac0760c981253b) | Tag `v0.3.0`; commit `2042c902ec269e33c1061ccd8aac0760c981253b` | Independent maintained compiler implementation | Corroborating PEX format, version, game-ID, object, function, debug, and instruction structure |
| [Caprica PEX file](https://github.com/Orvid/Caprica/blob/2042c902ec269e33c1061ccd8aac0760c981253b/Caprica/pex/PexFile.cpp), [object](https://github.com/Orvid/Caprica/blob/2042c902ec269e33c1061ccd8aac0760c981253b/Caprica/pex/PexObject.cpp), [function](https://github.com/Orvid/Caprica/blob/2042c902ec269e33c1061ccd8aac0760c981253b/Caprica/pex/PexFunction.cpp), and [instructions](https://github.com/Orvid/Caprica/blob/2042c902ec269e33c1061ccd8aac0760c981253b/Caprica/pex/PexInstruction.h) | Same pinned Caprica commit | Primary implementation evidence for Caprica; independent corroboration for format facts | Skyrim big-endian parsing, versions `3.1`/`3.2`, native/body distinction, and Skyrim opcode ceiling |
| [Champollion](https://github.com/Orvid/Champollion/tree/108bb84fb960884639560c04cd67143ba0a9608f) and [release](https://github.com/Orvid/Champollion/releases/tag/v1.3.2) | Tag `v1.3.2`; commit `108bb84fb960884639560c04cd67143ba0a9608f` | Independent maintained decompiler implementation/release | Independent parser/decompiler, header-info and assembly modes, and evidence that decompilation is reconstruction rather than source recovery |
| [Champollion README](https://github.com/Orvid/Champollion/blob/108bb84fb960884639560c04cd67143ba0a9608f/README.md) and [PEX reader](https://github.com/Orvid/Champollion/blob/108bb84fb960884639560c04cd67143ba0a9608f/Pex/FileReader.cpp) | Same pinned Champollion commit | Primary documentation and implementation for Champollion | Its stated functional-equivalence goal and parsed PEX structures |
| [UESP compiled-script format](https://en.uesp.net/wiki/Skyrim_Mod:Compiled_Script_File_Format) | Moving UESP technical page retrieved 2026-07-25 | Community technical reference used by Mutagen; not an official formal specification | Field ordering and binary-format cross-check |
| [Creation Kit states](https://ck.uesp.net/wiki/States_%28Papyrus%29) and [script extension](https://ck.uesp.net/wiki/Extending_Scripts_%28Papyrus%29) | Moving Bethesda-origin wiki pages retrieved 2026-07-25 | Primary-origin language/runtime documentation, community hosted | Runtime state dispatch, dynamic state names, and inheritance precedence |
| [Creation Kit Papyrus index](https://ck.uesp.net/wiki/Category%3APapyrus), [latent functions](https://ck.uesp.net/wiki/Category%3ALatent_Functions), and [threading discussion](https://ck.uesp.net/wiki/Differences_from_Previous_Scripting) | Moving Bethesda-origin wiki pages retrieved 2026-07-25 | Primary-origin language/runtime documentation, community hosted | Latency, concurrent event instances, registrations, and runtime scheduling limits |
| [RESEARCH-0013](RESEARCH-0013-wave-b-authoritative-local-state-integration.md), [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md), and [Wave B reference manifest](WAVE-B-reference-environment-manifest.md) | Accepted/proposed project inputs as present on 2026-07-25 | Project research | Effective-provider, exact-runtime, native-component, private-reference, and non-mutation boundaries |

### Source applicability notes

- Mutagen `0.54.2` is already accepted for Bethesda plugin/record semantics.
  This investigation establishes that the same pinned repository also contains
  a PEX reader/model; it does **not** silently accept every semantic inference
  built on that model.
- The Mutagen PEX README cites the UESP format page, and its tests prove
  successful parsing/round-tripping for a small mixed fixture set. Neither is
  a formal conformance or adversarial-security claim.
- Caprica and Champollion are useful independent implementations. Their
  agreement strengthens structural confidence, but multiple implementations
  derived from community knowledge are not equivalent to an official formal
  PEX specification.
- Champollion describes reconstructed source intended to recompile to
  functionally equivalent PEX. That is a useful tool goal, not evidence that
  comments, imports, original control structures, or author intent are
  recoverable.
- The Creation Kit pages describe runtime semantics. They establish why
  bytecode structure alone cannot resolve current state, event scheduling, or
  native implementation behavior.

## 4. Experiments and artifacts

### 4.1 Environment and safety

The bounded survey used:

- Windows OS build `10.0.26200`;
- PowerShell `7.6.3`;
- .NET runtime `10.0.9`;
- Python `3.14.3`;
- Git `2.55.0.windows.2`;
- Mutagen source tag `0.54.2`;
- Caprica source tag `v0.3.0`; and
- Champollion release `v1.3.2`.

No game, manager, compiler, installed mod executable, or native component was
launched. Champollion was run only against selected PEX bytes in read-only
header/decompile experiments. Public source repositories and the official
Champollion release archive were downloaded under the operating-system temp
directory. A recursive cleanup attempt was rejected by the execution
environment, so those public-source, release, and decompiler-output temp
artifacts remained at handoff and can be removed by normal operating-system
temp cleanup. Their absolute paths are intentionally omitted. Repository
writes are limited to this report.

The downloaded `Champollion.v1.3.2.zip` was 583,399 bytes with SHA-256:

`EA53054276AC8006CCD3B323286BFBC6E34A454FA419D08DA9BD440CBD31B383`

`Champollion.exe --version` reported `Champollion PEX decompiler v1.3.2`.
This identifies the research executable bytes; it is not a recommendation to
ship or invoke that executable in production.

### 4.2 Pinned-source inspection

The three independent PEX models agree on the Skyrim-relevant top-level
shape:

1. magic and version/game header;
2. compilation and source/build metadata;
3. string table;
4. optional debug information;
5. user flags; and
6. objects containing inheritance, variables, properties, states, functions,
   and instructions.

Mutagen `0.54.2` specifically exposes `PexFile.CreateFromFile` and
`CreateFromStream`, verifies the magic, parses the whole stream, and rejects
unconsumed trailing bytes. Its object model exposes names, parent class,
documentation, automatic state, variables, properties, states, function
signatures/flags, locals, opcodes, and operands. Its tests parse eleven
Skyrim/Fallout fixtures and round-trip them through the writer.

This is sufficient evidence that a separate parser is **not automatically
required**. It is not sufficient evidence that the current reader is hardened
for hostile input, enforces Infinium’s exact accepted-version policy, validates
control-flow targets, or provides the dependency/candidate semantics required
by this product.

### 4.3 Sanitized real-profile shape probe

The user-confirmed `Brain Blast Destruction 2024` profile was used only as
private shape evidence. The probe enumerated enabled physical mod roots and
loose `Scripts/*.pex` files. It did **not** reconstruct the authoritative
effective VFS, inspect archives, or treat the profile as correct.

| Observation | Sanitized result |
|---|---:|
| Enabled mod-list entries | 1,793 |
| Resolved physical mod directories | 1,791 |
| Loose PEX provider instances | 7,701 |
| Unique loose relative script paths | 7,557 |
| Relative paths with more than one potential provider | 95 |
| Maximum potential provider-chain length | 25 |
| Collision paths whose observed provider bytes were all identical | 12 |
| Collision paths with at least two distinct byte identities | 83 |
| Header-probe successes / failures | 7,701 / 0 |
| Game ID `1` | 7,701 |
| PEX `3.2` / `3.1` | 7,697 / 4 |
| Debug flag present | 7,701 |
| Co-located source candidates | 4,219 |
| Size range | 431 to 354,679 bytes |
| Median / 95th-percentile size | 796 / 4,011 bytes |

Twenty-four size-stratified samples all parsed successfully in Champollion
header-information mode. One median-sized PEX with a co-located `.psc`
decompiled successfully; the reconstructed 539-byte text was not byte-equal
to the 477-byte co-located source.

The pair is a boundary illustration, not a quality benchmark: the source could
be stale, reformatted, or from another build, and one sample cannot estimate
decompiler accuracy. It does demonstrate why “decompiled text equals original
source” is not a safe contract.

The reference profile’s `modlist.txt` and sampled PEX hashes were verified
unchanged before and after observation. No raw mod names, paths, source text,
header identity strings, or other private values are retained here.

### 4.4 Header preflight mutation probe

A small in-memory research preflight was exercised against Mutagen’s public
Skyrim `Art.pex` fixture. It accepted the unchanged file and rejected six
mutations:

- invalid magic;
- a truncated fixed header;
- an over-limit declared header string;
- a non-Skyrim game ID;
- an unsupported major/minor version; and
- a header string truncated relative to its declared length.

This establishes that cheap format/version/size preflight is feasible. The
probe is not retained as production code and is not a parser-conformance,
fuzzing, or security qualification. Full nested-count, index, opcode, jump,
allocation, timeout, and decompression/archive bounds remain required.

### 4.5 Privacy observation

Skyrim PEX headers may include source filename, compiler username, and machine
name. Every loose PEX header in the private shape probe contained non-empty
values for those three fields.

These fields are useful to establish presence, format, or an explicitly
requested diagnostic. They are not needed for ordinary compatibility
analysis. Derived evidence, logs, exports, and model context should omit or
redact username and machine name by default. Source filenames should be
normalized and exposed only where relevant. Raw retained private bytes remain
subject to the project’s local-data and export controls.

## 5. Capability and gap matrix

“Deterministic” below means reproducible for the exact captured bytes and
accepted parser/version contract. It does not mean that the observation alone
is a product finding.

| Surface or question | Available evidence | Maximum justified conclusion | Required gap/abstention |
|---|---|---|---|
| Effective script identity | Effective relative path, provider chain, winner, size, SHA-256 | Exact bytes and provider at the accepted snapshot | Unknown if ADR-0008 cannot resolve loose/archive precedence or bytes drift |
| Header | Magic, major/minor, game ID, compile time, source/user/machine strings | File structurally presents as a supported Skyrim PEX header | Header metadata does not identify compiler, mod release, trust, or runtime behavior |
| PEX versions | Observed `3.1` and `3.2`; Caprica identifies both as Skyrim-era versions | Parse/version support can be explicitly gated to tested Skyrim shapes | Do not infer “SE-built” or compatibility solely from `3.2`; test `3.1` deliberately |
| Debug data | Presence, modification time, function/line mappings where encoded | Debug information exists and can enrich provenance/navigation | Absence is a coverage fact, not a defect; line maps do not recover source |
| Class/object surface | Object name, parent class, flags, auto state | Declared class identity and inheritance edge | Parent availability and runtime binding require other effective artifacts |
| Variables/properties | Names, type names, defaults, flags, getter/setter bodies | Declared storage/API surface and static type-name edges | Private/runtime values and VMAD-filled instance values are not in PEX |
| States | State names, auto state, functions per state | Declared state set and implementations | Current state and runtime dispatch target are unresolved; names may be constructed dynamically |
| Functions | Name, return type, parameters, flags, locals, body | Declared callable surface and bytecode presence/absence | A name/signature does not prove successful calls or behavior |
| Native functions | Native flag and signature; normally no bytecode body | The PEX declares a native boundary | Implementation, provider, version, latency, side effects, and availability require external evidence |
| Instructions | Opcode and typed operands for the supported Skyrim opcode set | Exact static bytecode facts and literal/name references | Operand meaning must be opcode-qualified; unknown opcodes/invalid types reject or gap |
| Intraprocedural flow | Explicit conditional/unconditional relative jumps and returns | A bounded control-flow graph after validating every target | Dynamic calls, events, native behavior, exceptions/runtime failures, and scheduling remain unresolved |
| Calls | Static/parent/method call opcodes, declared names, receiver operands, literals | Declared call-site candidate and, sometimes, static target name | Method receiver type and live dispatch may be ambiguous; current-state and inheritance rules apply |
| Dependencies | Parent/type names, static-call targets, selected method/property references | Candidate dependency edges with per-edge kind and resolution status | Imports are not recoverable as an exact source construct; name edges can be incomplete or ambiguous |
| Plugin attachment | Qualified effective VMAD script name and fragment script name | Candidate required `Scripts/<name>.pex` artifact | Only after ADR-0009 VMAD field shapes are qualified; record reachability/intent remains separate |
| Plugin property fill | Qualified VMAD property name/type/value plus effective PEX property declaration | Structural presence/type compatibility or mismatch candidate | Runtime object validity and intended value are not proven |
| Provider conflict | Same normalized path plus ordered providers and hashes | Identical duplicate or distinct-byte competition | Same path, even with distinct bytes, is not itself a problem |
| API comparison | Normalized class/parent/property/state/function signatures | Added, removed, or changed declared public surface | Whether consumers rely on a change and whether it is harmful require evidence |
| Source availability | Co-located effective `.psc`, declared source filename, source hash | A source candidate exists for separate provenance analysis | Co-location and timestamps do not prove the source produced the PEX |
| Decompiled text | Tool/version/hash, output, diagnostics | A tool reconstruction useful for investigation | Never label as original source or authoritative semantics |
| Build metadata | Compile timestamp and source/user/machine strings | Self-contained header metadata from exact bytes | Do not infer freshness, release order, compiler version, or authorship |
| Performance | Static size/opcodes/call sites | Candidate locations for later investigation at most | No performance finding without concrete runtime/log/profiler evidence |
| Save/runtime state | None in standalone PEX | No static conclusion | Requires a separately scoped, version-qualified save/runtime analyzer |

## 6. Findings

### F1 — Compiled Skyrim Papyrus is structurally rich enough for bounded analysis

**Verified fact:** PEX retains class names, parent names, variables,
properties, states, function signatures and flags, locals, and bytecode
instructions. Mutagen `0.54.2`, Caprica `v0.3.0`, and Champollion `v1.3.2`
independently expose this shape.

**Interpretation:** Infinium can do materially more than inventory script
filenames or hashes. Structural PEX evidence belongs in the deterministic
evidence layer.

**Boundary:** The evidence describes compiled declarations and instructions.
It does not prove what the live game will do.

### F2 — The accepted Mutagen pin already provides the strongest first integration candidate

**Verified fact:** The exact Mutagen `0.54.2` revision accepted by ADR-0009
contains `Mutagen.Bethesda.Pex`, including file/stream readers, a typed model,
Skyrim opcode definitions, fixtures, and round-trip tests.

**Recommendation:** Qualify that existing surface first. Do not introduce a
second parser or external decompiler merely because PEX analysis was
previously assumed to require one.

**Boundary:** A production decision still needs a narrow ADR or implementation
plan that defines supported PEX versions, parser containment, resource limits,
invalid-input behavior, and the exact semantic adapters built above Mutagen.
This report is evidence for that decision, not the decision itself.

### F3 — Exact byte identity and declared API are the stable comparison layers

A useful provider comparison has three independent fingerprints:

1. **byte fingerprint:** SHA-256 and length;
2. **structural fingerprint:** normalized header/game version, class/parent,
   properties, states, function signatures/flags, and supported instructions;
3. **public-surface fingerprint:** normalized externally relevant class,
   property, state, and function declarations.

Identical provider bytes can be classified as duplicate evidence without
semantic speculation. Distinct bytes can be structurally compared. A changed
public surface is stronger evidence than a changed hash, but it remains a
candidate until a consumer, author requirement, or cross-layer dependency
shows why the change matters.

### F4 — Provider awareness is mandatory

The effective artifact is the accepted ADR-0008 winner at normalized
`Scripts/<name>.pex`; lower providers are comparison evidence. A naïve physical
enumeration cannot determine the runtime winner, particularly when archives,
overwrite, mappings, or other provider roots participate.

The private shape probe found both identical and distinct-byte same-path
providers. This confirms that “more than one provider” is not a useful issue
rule: some chains are byte-identical, and distinct chains still require
semantic and intent evidence.

If archive participation or precedence is not supported for the selected
snapshot, the analyzer must expose an effective-state coverage gap instead of
presenting the loose-file winner as authoritative.

### F5 — PEX version and build metadata have narrow meanings

For the accepted Skyrim scope:

- magic, endian interpretation, game ID, and tested PEX version identify the
  format branch;
- `3.1` and `3.2` both occur in real Skyrim SE setups;
- compilation time, source filename, username, and machine name are embedded
  build metadata; and
- no inspected header field identifies the compiler product/version or mod
  release.

Therefore:

- accept only explicitly tested Skyrim version/game combinations;
- do not label all `3.1` as obsolete or all `3.2` as SE-authored;
- do not use compilation time or filesystem time as freshness proof; and
- minimize privacy-sensitive identity strings.

### F6 — Source and debug artifacts enrich provenance but cannot become authority by proximity

Co-located `.psc` and PEX debug information can support navigation,
explanations, named-call inspection, and source-to-bytecode comparison.
Neither proves that the source generated the effective PEX. A useful source
relationship needs, in descending strength:

1. a reproducible build or publisher manifest linking source/revision to
   exact PEX bytes;
2. compiler output reproduced from exact source and dependencies;
3. author-maintained release/package evidence; or
4. a clearly labeled co-location/name/timestamp heuristic.

Absent that evidence, treat the `.psc` as a source candidate. Treat
decompiler output as reconstructed investigative text with tool/version/input
provenance, never as the original source.

### F7 — Static dependencies are useful but incomplete

The analyzer can produce typed edges such as:

- `extends` from a class to its declared parent;
- `type-reference` from properties, variables, parameters, locals, and return
  types;
- `static-call`, `parent-call`, and `method-call` from bytecode call sites;
- `property-read` and `property-write`;
- `plugin-attaches-script` from qualified VMAD;
- `plugin-fills-property` from qualified VMAD; and
- `fragment-references-script/function` from qualified VMAD.

Each edge must preserve the exact originating artifact, provider, structural
location, and resolution status.

These edges are not an exact recovery of source `Import` statements. Method
receivers can be ambiguous, inheritance/state dispatch is runtime-sensitive,
and name construction can hide relationships. “No static edge observed”
therefore means **not observed**, not “no dependency exists.”

### F8 — VMAD-to-PEX joins provide high-value cross-layer candidates

Mutagen’s pinned Skyrim VMAD model exposes script entries, property names,
typed property values/arrays, and fragment script/function names. Once the
relevant VMAD shapes pass ADR-0009 qualification, Infinium can check:

- an attached/fragment script name has an effective PEX;
- the effective PEX declares the expected property name;
- the VMAD value category is compatible with the declared PEX property type;
- a referenced fragment function exists in the effective script surface; and
- a PEX provider change removed or changed an API used by effective plugin
  records.

These are structural compatibility candidates. They do not prove that the
record is reachable in play, that an object reference is valid at runtime, or
that a property’s chosen value matches author intent.

### F9 — Native calls define a hard semantic boundary

The PEX can state that a function is native and can preserve native call
sites. Its implementation is outside that bytecode, in the game runtime,
SKSE, or another native provider.

Static PEX analysis can only say:

- which native signature is declared;
- which static call sites appear;
- whether a separately versioned accepted API manifest contains a matching
  declaration; and
- whether the associated native provider is present/version-coherent under a
  separate analyzer.

It cannot infer implementation side effects, latency, thread behavior,
runtime registration success, or compatibility from the PEX alone. These
limits connect to RESEARCH-0014; they do not collapse PEX and native-component
evidence into one conclusion.

### F10 — Runtime dispatch, persistence, scheduling, and performance prevent general behavioral proof

Creation Kit documentation establishes that:

- a script has a current state;
- current-state and inheritance precedence select implementations;
- state names can be assembled as strings at runtime;
- latent functions can suspend execution;
- multiple instances of an event can coexist; and
- scripts can register for later events.

Live object binding, current state, saved stacks/values, event sources,
registrations, engine/native behavior, and runtime frequency are absent from a
standalone PEX. Even a perfect disassembly therefore cannot generally prove:

- which path or state will execute;
- whether a call succeeds;
- whether event ordering is safe;
- whether a loop/call site is frequent enough to matter;
- whether a script corrupts a quest or save; or
- whether two scripts are behaviorally compatible.

Static size, instruction count, call count, or the presence of loops may
prioritize investigation. Per ANALYSIS-014 and EVAL-0061, they must not become
generic performance findings.

### F11 — Malformed input is primarily a parser/coverage outcome

A production analyzer must distinguish:

- unsupported game/version;
- malformed or inconsistent structure;
- resource limit exceeded;
- inaccessible file;
- unstable bytes/snapshot;
- parser defect;
- supported and parsed;
- parsed with unsupported semantic features; and
- analyzed with complete or partial dependency resolution.

Bad magic, invalid indexes, impossible object sizes, unknown opcodes, invalid
operand forms, out-of-range jump targets, excessive counts, truncated
sections, trailing data, and allocation/time limits require explicit tests.
A parse failure is deterministic coverage/error evidence. It becomes a user
finding only when product rules and impact evidence establish a meaningful
setup risk.

### F12 — PEX evidence supplies only technical taxonomy axes

PEX analysis can support provisional RQ-036 axes such as:

- technical surface: compiled Papyrus;
- artifact relationship: winner, lower provider, attached script, fragment,
  parent, type reference, call site, property fill;
- evidence kind: exact bytes, parsed declaration, instruction, debug/source
  candidate, qualified VMAD join; and
- analysis status: parsed, unsupported, malformed, partial, or unresolved.

It must not infer mod purpose, game area, consequence, severity, symptom, or
extent from a script filename, instruction count, API name, or analyzer label
alone. Those require separate evidence and taxonomy research.

## 7. Candidate-generation contract

The recommended analyzer emits typed evidence first. Candidate rules then
combine evidence; adjudication decides whether a case/findings threshold is
met.

### Deterministic observations

- effective path, ordered providers, selected winner, size, and SHA-256;
- PEX header/game/version and parse status;
- debug/source-candidate availability;
- normalized class, parent, property, state, and function declarations;
- native/global flags;
- supported opcodes, operands, static literals/name references, and validated
  jump targets;
- structural/public-surface fingerprints; and
- qualified VMAD script/property/fragment facts.

### Strong candidate families

| Candidate | Minimum evidence | Required restraint |
|---|---|---|
| Missing attached script | Qualified effective VMAD script or fragment name; authoritative effective script namespace; no matching PEX | Gap if archives/providers are not covered; do not infer gameplay symptom automatically |
| VMAD property mismatch | Qualified VMAD property name/value category; effective PEX declaration; absent or incompatible property | Preserve record/plugin/provider provenance; distinguish inherited properties |
| Missing parent/type | Effective PEX declares a script type; authoritative effective namespace lacks it | Built-in/native types and unresolved archives/manifests must be classified before candidate |
| Public API regression | Lower/expected provider declares API; winner removes or incompatibly changes it; effective consumer evidence references it | Hash difference alone is insufficient; consumer/expectation evidence is mandatory |
| Fragment target mismatch | Qualified fragment script/function name; effective PEX lacks script/function | Account for inheritance/state and field-shape qualification |
| Cross-layer version skew | Exact PEX/provider and exact plugin/native component evidence disagree with a versioned accepted contract | No contract means unknown, not incompatible |
| Malformed effective PEX | Effective winner fails bounded parser validation | Parser defect/unsupported variant must be separated from malformed input |

### Investigative candidates only

- distinct-byte same-path providers without consumer or intent evidence;
- changed implementation fingerprint with unchanged public surface;
- literal native/API name without an accepted external manifest;
- declared source filename differing from effective path/object name;
- co-located source that does not reproduce or match decompiled text;
- suspicious control-flow shape, unreachable-block hypothesis, or dynamic
  state construction; and
- code-size, loop, allocation-like, registration, or call-frequency
  heuristics.

These remain speculative evidence or selection signals. They must not be
promoted merely because an LLM can narrate a plausible failure.

### Coverage-only outcomes

- source absent;
- debug information absent;
- decompilation unavailable or failed;
- unsupported PEX version/opcode;
- archive/provider precedence unresolved;
- native implementation/provider manifest unavailable;
- dynamic dispatch target unresolved; and
- runtime/save state unavailable.

## 8. Alternatives considered

### A. Filename/hash inventory only

This is cheap and exact for bytes but misses declared API, inheritance,
dependencies, VMAD mismatches, and public-surface regressions.

**Disposition:** retain as baseline evidence, not the complete analyzer.

### B. Use pinned Mutagen PEX parsing plus Infinium semantic adapters

This reuses the already selected dependency and typed model while keeping
provider resolution, validation, evidence typing, fingerprints, dependency
edges, and candidate generation inside Infinium’s contracts.

**Disposition:** recommended first qualification path.

### C. Invoke Champollion and analyze reconstructed source

Champollion has mature inspection, assembly, and source reconstruction, but an
external-process integration adds executable provenance, output-file,
containment, licensing, versioning, and failure-mode work. Decompiled source
also invites unjustified source-level conclusions.

**Disposition:** optional investigative adapter only after a separate
decision; not the deterministic core.

### D. Embed or port Caprica/Champollion parsing code

Both provide useful independent implementations. Embedding/porting adds C++
build, maintenance, and licensing/integration work without first proving a
gap in the accepted Mutagen surface.

**Disposition:** fallback/comparison implementation, not current default.

### E. Build a new first-party PEX parser immediately

This maximizes control over bounds and diagnostics but duplicates a typed
parser already present in Mutagen.

**Disposition:** unjustified unless qualification exposes correctness,
security, performance, or feature gaps that cannot be addressed around or
upstream of Mutagen.

### F. Analyze source only

Source can provide imports, comments, and clearer constructs, but many
packages omit it, source may be stale, and it is not necessarily the effective
runtime artifact.

**Disposition:** optional corroborating/provenance layer, never a replacement
for effective PEX analysis.

### G. Decompile every PEX and send all text to an LLM

This amplifies cost, latency, privacy exposure, prompt volume, and
reconstruction error. It also performs expensive semantic work before
deterministic candidate selection.

**Disposition:** reject as the default. Use deterministic narrowing, then
case-scoped LLM synthesis only where it can add documented value.

### H. Omit compiled-script analysis

This avoids parser risk but loses important effective-provider, attachment,
property, dependency, and API-compatibility evidence.

**Disposition:** reject for the intended product, while allowing the analyzer
to be modular and explicitly disabled/gapped.

## 9. Uncertainty and limits

### High-confidence conclusions

- Skyrim PEX exposes the structural surfaces listed in the matrix.
- Mutagen `0.54.2` contains a usable typed PEX reader/model and Skyrim
  fixtures.
- effective-provider resolution and exact byte identity must precede semantic
  comparison;
- PEX alone cannot supply native implementation semantics or live state; and
- decompiled text is not the original source.

### Medium-confidence conclusions

- normalized public-surface and dependency fingerprints will provide useful
  candidate reduction at large profile scale;
- VMAD-to-PEX attachment/property/fragment joins will produce high-value
  findings after field qualification; and
- `3.1` and `3.2` should both be considered for structural Skyrim SE support,
  subject to explicit fixtures and version policy.

### Unresolved

- Mutagen PEX reader behavior under adversarial counts, corrupt indexes,
  malformed object sizes, extreme files, and cancellation/resource limits;
- precise false-positive/false-negative rates for dependency and public-API
  candidates;
- authoritative archive-inclusive provider behavior for scripts in the first
  implementation slice;
- which native API manifests are maintainable and authoritative enough for
  version-coherence rules;
- whether source/decompiler comparison adds enough value to justify an M1
  analyzer;
- exact analyzer stage placement and cache granularity; and
- final taxonomy labels or user-facing severity mappings, which are governed
  by the separately accepted product taxonomy and severity policy.

## 10. Recommendation and evaluation implications

### Recommendation

The accepted bounded recommendation is:

1. resolve the authoritative effective script namespace under ADR-0008;
2. capture exact bytes, provider chain, winner, size, and SHA-256 under
   ADR-0010;
3. perform a cheap bounded Skyrim PEX preflight;
4. qualify Mutagen `0.54.2`’s PEX reader against adversarial and representative
   fixtures;
5. emit typed structural evidence, not findings;
6. derive normalized API/dependency/control-flow fingerprints;
7. join only qualified VMAD and accepted external component evidence;
8. generate candidate families with explicit prerequisites;
9. adjudicate candidates with source/intent/consumer evidence; and
10. expose every unsupported, unresolved, or dynamic boundary as coverage.

The LLM layer may explain a selected case, correlate author documentation, or
propose validation/resolution steps. It should consume bounded typed evidence,
not all decompiled scripts, and may not promote a candidate beyond its
underlying evidence.

### Preconditions before implementation

- an accepted analyzer/implementation milestone plan;
- an ADR or explicit accepted plan section for the Mutagen PEX integration
  boundary;
- version/game-ID/opcode policy and fixtures;
- parser size/count/depth/time/cancellation limits and containment review;
- archive-inclusive provider coverage decision or explicit gap;
- VMAD field-shape qualification for every cross-layer rule;
- privacy rules for PEX header identity metadata;
- stable evidence schema and fingerprint normalization rules; and
- acceptance thresholds from the dedicated evaluation family.

### Proposed dedicated evaluation family

Add a new case-catalog entry for **compiled Papyrus structural and
cross-layer analysis** with at least:

- minimal valid Skyrim `3.2` and `3.1` PEX fixtures;
- debug-present and debug-absent fixtures;
- native function with no body;
- states, inherited overrides, and dynamically constructed state-name
  examples;
- static, parent, and method calls plus property access;
- exact identical-provider and distinct-provider chains;
- public API additions/removals/type changes with both harmful-consumer and
  harmless-unused counterexamples;
- effective VMAD attachment/property/fragment match and mismatch pairs;
- source present/absent/stale and non-identical decompiler-output cases;
- wrong game ID, unsupported version/opcode, bad string index, invalid value
  type, truncated section, impossible object size, invalid jump target,
  trailing bytes, and excessive-count/size cases;
- archive provider unsupported/resolved cases;
- cancellation and resource-limit cases;
- privacy redaction for username and machine name; and
- a negative case proving that file size/instruction count does not emit a
  performance finding.

The family should cross-reference:

- EVAL-0032 for candidate-first selection;
- EVAL-0046 for read-only/external-tool safety;
- EVAL-0051 for effective providers;
- EVAL-0052 for qualified VMAD fields;
- EVAL-0061 for grounded performance;
- EVAL-0065 for declared analyzer contracts;
- EVAL-0083 for evidence provenance; and
- EVAL-0085 for visible coverage gaps.

## 11. Downstream work enabled

This report enables:

- a bounded PEX analyzer contract and implementation spike using the accepted
  Mutagen pin;
- effective-script inventory and provider fingerprints;
- VMAD attachment/property/fragment compatibility candidates;
- public API regression candidates across provider chains;
- explicit PEX privacy and malformed-input requirements;
- a dedicated compiled-Papyrus evaluation family;
- candidate-scale estimation without all-pairs decompilation;
- RQ-036 technical-surface and relationship evidence; and
- a clear boundary between PEX evidence, native-component evidence from
  RESEARCH-0014, and later runtime/log/save evidence.

It does **not** enable:

- general Papyrus behavioral verification;
- arbitrary native API semantics;
- script performance findings;
- exact source recovery;
- production use of Champollion or Caprica;
- agentic modification or patch generation; or
- a complete named-script analyzer roadmap.

## 12. Accepted RQ-022 disposition

Record RQ-022 as:

> **Bounded M0 survey answered; Conditional for broader roadmap.** Exact
> Skyrim PEX structure, provider-aware API comparison, selected dependency
> edges, and qualified VMAD cross-layer candidates are feasible. Arbitrary
> behavioral, native, save-state, scheduling, performance, and author-intent
> claims are outside the static boundary. The named analyzer catalog and
> empirical precision/recall remain open.

This does not block the first proof case unless that case selects
compiled-Papyrus or VMAD-to-PEX semantics. If selected, the parser and field
qualification preconditions in section 10 become blocking for that slice.

## 13. Traceability

| Research conclusion | Product/decision destination |
|---|---|
| Effective winner before PEX semantics | ADR-0008; ANALYSIS-016; EVAL-0051 |
| Exact bytes and dependency closure | ADR-0010; SNAP-001 through SNAP-006 |
| Mutagen `0.54.2` already exposes PEX | ADR-0009 follow-up/implementation plan |
| VMAD-to-PEX rules require field qualification | ADR-0009; EVAL-0052 |
| Candidate-first API/dependency analysis | ANALYSIS-017; EVAL-0032 |
| Native functions are external semantic boundaries | RESEARCH-0014; ANALYSIS-008 |
| No generic static performance claims | ANALYSIS-014; EVAL-0061 |
| Header identity metadata is privacy-sensitive | SEC-004; AI-003 |
| Parse/semantic gaps remain visible | COVER-001 through COVER-003; EVAL-0085 |
| Technical surface does not imply purpose/severity | Accepted taxonomy and taxonomy dependency map |
| Dedicated PEX evaluation family required | Evaluation case catalog follow-up |

## 14. Conclusion

Compiled Papyrus is neither opaque nor a reliable oracle of game behavior.
Its useful middle ground is large: Infinium can deterministically inventory
the effective PEX winner, parse its declared class/API/state/function
structure, inspect supported bytecode, compare provider surfaces, and join
those facts with qualified plugin attachments and property data.

The same evidence cannot reveal the exact original source, live state,
runtime object bindings, native implementations, scheduling, save-persisted
execution, actual frequency, correctness, safety, performance, or intent.
Those boundaries are structural facts about the domain, not shortcomings that
an LLM can safely guess past.

The most idiomatic next step is to qualify the PEX reader already present in
the accepted Mutagen `0.54.2` dependency, add Infinium-specific validation and
typed evidence adapters, and evaluate narrowly defined candidate families.
Keep decompilation optional and investigative, keep model use case-scoped, and
make every unsupported or dynamic boundary visible as coverage.
