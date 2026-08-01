# RESEARCH-0053 — Mutagen 0.54.2 conformance with accepted Slice 4 fixtures

Status: Completed investigation; disposition pending

Date: 2026-08-01

Researcher: Codex agent with independent review

Primary RQ: RQ-004

Decision required: reconcile ADR-0009, EVAL-0052, and the accepted Bethesda
fixture structures before Slice 4 production implementation

## 1. Question and result

Can the exact accepted public Slice 4 Bethesda controls be consumed through
the pinned `Mutagen.Bethesda.Skyrim` `0.54.2` semantic API while preserving the
positive EVAL-0052 allowlist and accepted no-environment authority boundary?

The answer is **no for two required shapes**:

1. the accepted four-byte RACE/DATA control is enumerated, but the required
   typed `Race.Flags` accessor throws; and
2. the accepted top-level REFR controls are present in the independent byte
   oracle, but Mutagen enumerates zero placed objects because its Skyrim model
   expects REFR records under CELL/worldspace child topology.

These are blocking conformance disagreements, not optional coverage gaps.

## 2. Governing boundary

The probe used:

- repository baseline `e4e83963d7292f1c688f74970400d70a2acf84e6`;
- `Mutagen.Bethesda.Skyrim` `0.54.2` from the locked repository graph;
- .NET SDK `10.0.302` and runtime `10.0.10` on Windows x64;
- public `BETH-NPC-DEV` and `BETH-REFR-DEV` package bytes plus their accepted
  snapshot order and independent byte facts; and
- no Data folder, automatic environment discovery, archive/string lookup,
  xEdit, private fixture, external application, provider, or protected-root
  operation.

The two-pass import first established every plugin's `MasterStyle`, then used
those exact supplied master styles for semantic construction:

```csharp
SkyrimMod.Create(SkyrimRelease.SkyrimSE)
    .FromStreamFactory(
        () => new MemoryStream(bytes, writable: false),
        modKey)
    .WithLoadOrder(orderedMasterStyles)
    .WithNoDataFolder()
    .SingleThread()
    .ThrowIfUnknownSubrecord(true)
    .Construct();
```

`FromStreamFactory` returned a fresh read-only stream for every invocation.
No typical-environment or default-load-order facility was used.

## 3. RACE reproduction

The exact public `BETH-NPC-DEV` accepted-order population produced:

```text
per-plugin NPC counts: 0, 2, 1, 1, 1, 1, 1
total NPC record versions: 7
winning NPC records (include deleted): 3
04-LightActors.esl master style: Small
all other package plugin master styles: Full
```

Both ordinary enumeration routes agreed:

```csharp
mods.Sum(mod => mod.Npcs.Count)                                  // 7
mods.Sum(mod => mod.EnumerateMajorRecords<INpcGetter>().Count()) // 7
mods.Npc().WinningOverrides(includeDeletedRecords: true).Count() // 3
```

The independent public byte facts identify four-byte RACE/DATA controls,
including `02000000` for the positive `FaceGenHead` control and `00000000` for
the matched negative. Mutagen enumerates the RACE records, but this required
allowlisted access:

```csharp
race.Flags.HasFlag(Race.Flag.FaceGenHead)
```

throws `ArgumentOutOfRangeException` from
`RaceBinaryOverlay.GetFlagsCustom()`. The generated overlay expects the full
schema-shaped RACE DATA payload rather than the frozen four-byte flags control.

EVAL-0052 requires resolved RACE plus `FaceGenHead`. Treating the value as
unsupported would fail the positive gate, while decoding it with a new
first-party semantic parser would change ADR-0009's accepted dependency and
authority boundary.

## 4. REFR reproduction

The public `BETH-REFR-DEV` byte oracle records the required REFR `NAME`,
`XLKR`, `XLRL`, `XOWN`, and `DATA` facts. The fixtures encode those records in
a top-level `GRUP(REFR)` so the independent byte structure is unambiguous.

Mutagen `0.54.2` models Skyrim placed objects beneath CELL/worldspace child
groups. Against the exact accepted package, both supported semantic routes
produce an empty population:

```csharp
mods.PlacedObject().WinningOverrides(includeDeletedRecords: true).Count() // 0
mods.Sum(mod => mod.EnumerateMajorRecords<IPlacedObjectGetter>().Count())  // 0
```

This is not an absent-input case: the independent oracle contains the REFR
records and links. Publishing an empty authoritative index would fabricate
coverage and violate EVAL-0052.

## 5. Alternatives and rejected implicit fallback

### Correct and reseal the fixtures

Construct valid Skyrim CELL/worldspace-nested REFR fixtures and a Mutagen-
consumable RACE shape, independently re-derive the expected facts, and replace
every affected public and evaluator-private revision under the accepted
governance. This preserves ADR-0009 but requires careful anti-overfitting and
oracle-independence review.

### Authorize a bounded first-party semantic parser

Research and amend ADR-0009 to define the exact field/record authority,
decompression and resource bounds, worker isolation, malformed behavior, and
independent evaluation for an Infinium-owned parser. A provisional probe showed
that raw decoding is technically possible, but it also demonstrated that this
is a material architecture choice rather than a harmless adapter workaround.

### Qualify another exact Mutagen version

Accept only after a pinned version consumes every required shape and passes the
full independent EVAL-0052 matrix. A floating or prerelease upgrade is not an
answer.

No alternative is selected by this investigation. Ordinary implementation
must not revise the authority boundary implicitly.

## 6. Independent review and additional controls

Independent review confirmed the two compatibility blockers and rejected the
provisional raw-projector implementation. It also identified controls required
for any future first-party parsing proposal:

- enforce decompression bounds during streaming, not after expansion;
- execute parsing through the accepted contained worker and stage output for
  coordinator validation;
- compare every allowlisted field, repeated order, link, state, chain, winner,
  full/light translation, malformed member, and unsupported denominator to the
  independent oracle;
- do not publish unsupported record families inside authoritative typed
  indexes; and
- do not infer taxonomy areas from record-family presence or incomplete field
  evidence.

The provisional code and tests were removed. They are not evidence that
EVAL-0052 or EVAL-0086 passed.

## 7. Disposition

RQ-004 is reopened for this bounded compatibility decision. Slice 4 is blocked
until the project owner accepts a reconciled fixture or architecture path and
the affected plan, ADR, evaluation specification, manifests, and fixture
revisions agree. No production implementation is retained and no later slice
is authorized by this result.
