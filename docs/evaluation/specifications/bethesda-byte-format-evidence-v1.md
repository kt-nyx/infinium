# Bethesda byte-format evidence profile v1

Status: Accepted
Disposition: Frozen for M1 Slice 3.5 independent fixture construction and review

Last reviewed: 2026-08-10

Frozen: 2026-07-30

This profile pins only the byte rules needed to construct and independently
audit the project-authored M1 Slice 3.5 inputs. It is not production parser
code, a general TES4 specification, or authority for fields outside the
positive allowlist in
[the M1 semantic specification](../specifications/m1-semantic-and-ground-truth.md).
The independent oracle must report any byte that cannot be reconciled with
both a manual hex worksheet and a separately implemented bounded raw reader.

## Evidence authority

The accepted evidence set is:

- the bounded M1 allowlist and truth rules in the semantic specification;
- Sections 8, 14, 15, 16, and 17 of
  [the semantic fixture catalog](semantic-fixture-catalog.md);
- the project-accepted full/light FaceGen qualification in
  [RESEARCH-0034](../../research/investigations/RESEARCH-0034-loose-facegen-qualification.md);
- the raw-header, group, subrecord, master-order, compression, and FormID
  method retained by
  [RESEARCH-0035](../../research/investigations/RESEARCH-0035-gate-c-real-mod-qualification.md);
  and
- the stricter Slice 3.5 bounds and role separation in
  [the accepted slice plan](../../plans/milestones/m1/slices/s3.5/plan.md).

The earlier RESEARCH-0035 reader is evidence of method shape only. It is not
an oracle implementation: decompressed logical offsets must not be reported as
physical file offsets, decompression must be bounded, trailing compressed data
must be rejected, and a dangling `XXXX` must fail.

## Frozen physical rules

- Integers and IEEE-754 single-precision bit patterns are little-endian.
- A major-record header is 24 bytes: four-byte signature, 32-bit data size,
  32-bit flags, 32-bit raw FormID, 32-bit revision, 16-bit version, and
  16-bit unknown field.
- A `GRUP` header is 24 bytes. Its 32-bit size includes the header itself.
  Group size, nesting, child count, and file bounds are checked before
  traversal.
- An ordinary subrecord header is a four-byte signature and a 16-bit data
  size. `XXXX` carries a four-byte extended size for exactly the immediately
  following subrecord. Chained, dangling, truncated, or overrun extended-size
  framing is malformed.
- Major-record flag `0x00040000` marks compressed record data. The first four
  bytes of the physical record data declare the decompressed byte count; the
  remaining bytes are a zlib stream. Physical container offsets and
  decompressed logical offsets are separate spaces.
- TES4 flag `0x00000200` is the light-plugin flag. A filename extension alone
  does not establish light origin.
- TES4 `MAST` subrecords are ordered, zero-terminated plugin names. A following
  `DATA` subrecord is retained as part of that master declaration. Master
  order is per plugin and is never borrowed from supplied load order.
- Major-record flag `0x00000020` is the deleted-record flag. Compression and
  deletion are independent bits.
- A null FormID is the four-byte little-endian value `00000000`.

## Frozen identity rules

- A raw FormID's high byte selects an entry in the current plugin's ordered
  master space; a record belonging to the current plugin uses the current
  plugin slot after its masters. The remaining 24 bits are the full-origin
  local ID.
- If the selected origin plugin is light-flagged, only the low 12 bits are the
  light local ID for this slice. Valid project-authored light object IDs are
  `0x800` through `0xFFF`, inclusive. `0x7FF`, `0x1000`, an unavailable master
  slot, and an extension/header mismatch are explicit invalid boundaries.
- Canonical identity is
  `<eight-uppercase-hex-local-id>:<case-preserved-origin-plugin>`. Both full
  and light local IDs are left-padded to exactly eight hexadecimal digits in
  this canonical identity. The separate FaceGen path component is also an
  eight-uppercase-hex local ID, but it is a path derivation rather than the
  canonical FormKey string.
  It is derived independently for every plugin from that plugin's TES4 master
  list and origin flags. Raw indices and supplied plugin order are not
  canonical identity.
- Override chains group equal canonical identities and retain supplied plugin
  order. The last member is the winner. A deleted winner remains the winner
  with deleted state; it does not fall back to the prior record.

## Frozen allowlisted payload rules

For `NPC_`, the independent oracle may retain:

- raw configuration and template flags, template link, `RNAM`, `AIDT`,
  ordered repeated `PKID`, ordered repeated `PNAM`, and `HCLF`;
- their physical or decompressed logical spans and exact bytes;
- canonical links resolved only through the supplied plugin population; and
- the bounded FaceGen applicability inputs.

The exact project-authored `NPC_` shapes are:

- `ACBS` is 24 bytes. Configuration flags are the 32-bit little-endian value
  at bytes `0..3`; template flags are the 16-bit little-endian value at bytes
  `18..19`. The remaining bytes are retained as raw typed-width values but
  have no additional Slice 3.5 meaning. A non-zero template-flags value is
  reported separately from the template link; neither implies inherited
  values.
- `TPLT`, `RNAM`, every `PKID`, every `PNAM`, and `HCLF` are exactly one
  four-byte FormID each. `TPLT=00000000` is the null template link.
- `AIDT` is exactly 20 bytes and is supported in this slice only as an ordered
  raw 20-byte value. No aggression, confidence, energy, responsibility, mood,
  assistance, warning, or attack-distance subfield interpretation is
  authorized by this fixture.
- Repeated `PKID` and `PNAM` values retain physical occurrence order and
  multiplicity. Zero occurrences, one occurrence, and multiple occurrences
  are distinct facts.
- A subject is called templated only when its independently observed `TPLT`
  link is non-null and its raw template-flags value is non-zero. This label
  does not authorize template inheritance.

For the project-authored `RACE` applicability controls, the `DATA` flags field
is a 32-bit little-endian value and `FaceGenHead` is bit `0x00000002`. The
fixture contains otherwise identical set and clear controls. This rule is
frozen before generation; generator data structures and future production
output are not authority for it.

For `REFR`, the independent oracle may retain `NAME`, ordered repeated `XLKR`,
`XLRL`, `XOWN`, and the six exact 32-bit IEEE-754 bit patterns in `DATA`.
`XESP` and enable-parent semantics are not in the higher-authority EVAL-0052
positive allowlist and are unsupported in Slice 3.5.

The exact project-authored `REFR` shapes are:

- `NAME`, `XLRL`, and `XOWN` are exactly one four-byte FormID each.
- Each `XLKR` is exactly eight bytes: a four-byte keyword FormID followed by a
  four-byte linked-reference FormID. Occurrence order and multiplicity are
  retained. Either component may be the null FormID.
- `DATA` is exactly 24 bytes: position X, Y, and Z followed by rotation X, Y,
  and Z, each retained as its exact four-byte IEEE-754 single-precision bit
  pattern. Decimal rendering is not canonical truth.

Unknown, null, unresolved, absent, repeated, deleted, templated, invalid, and
unsupported states remain distinct. No template inheritance, gameplay
meaning, conflict severity, user intent, taxonomy area, or finding is inferred
from these bytes.

## Canonical fact fingerprints

The supplemental answer artifact validates against
`bethesda-byte-oracle.v1.schema.json`. For
`infinium-canonical-json-sha256/v1`, a fact's `canonical_value` is serialized
as UTF-8 JSON with object keys sorted by Unicode code point, array order
preserved, no insignificant whitespace, uppercase hexadecimal strings, and
JSON integer values only. Its SHA-256 in lowercase hexadecimal is the
`canonical_value_fingerprint`. The matching `expected-oracle.json` item uses
the same `fact_id` as `expected_id` and the same fingerprint.

Every accepted fact cites both the manual worksheet and independent bounded
reader method IDs. A disagreement, missing byte span, unsupported positive
field, or unpinned interpretation blocks acceptance.
