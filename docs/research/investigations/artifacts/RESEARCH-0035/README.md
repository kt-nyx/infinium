# RESEARCH-0035 artifacts

Status: Completed
Captured: 2026-07-28
Last reviewed: 2026-07-28

This directory contains project-authored, non-redistributable-payload evidence
for the Gate C controlled-real candidate closeout.

- `gate-c-case-manifests.json` pins the source identities, dependency order,
  selected records, controls, and claim boundaries for EVAL-0016 and
  EVAL-0017.
- `eval-0016-independent-byte-map.json` and
  `eval-0017-independent-byte-map.json` are raw-offset/subrecord projections
  produced by the project-authored minimal TES4 reader.
- `tes4-inspect.mjs` is that read-only minimal reader. It handles TES4/GRUP
  traversal, compressed records, extended subrecord sizes, master lists, and
  selected raw subrecords. It does not use Mutagen or xEdit.
- `find-refr-merge-candidates.mjs` is the discovery-only scanner that located
  generic `REFR` cases where a patch combines one source's qualified
  structural relation with another source's placement.

The scripts accept evaluator-supplied local paths. The tracked outputs contain
only basenames, public source identity, hashes, byte offsets, and selected
derived expectations. No third-party archive/plugin/asset bytes or private
absolute paths are committed.

Run the reader with Node.js 24 or later. Example:

```powershell
node tes4-inspect.mjs --type REFR --form 0x00017061 `
  --subrecord XLKR --subrecord DATA `
  --output result.json <plugin paths>
```

The candidate scanner is a research discovery tool, not production analysis.
Its output must be independently checked with the retained byte map, author
intent evidence, and matched control before a case can be selected.
