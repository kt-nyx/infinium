# MO2 fixture guidance (Infinium)

Fixtures are **tiny, purpose-built MO2 instance snapshots** used for fast, repeatable tests of the Track‑A VFS reconstruction and deterministic candidate generation.

## Directory layout

Each fixture is a standalone “mini instance” with:

- `profiles/<ProfileName>/modlist.txt`
- `mods/<ModName>/...`
- optional `overwrite/...`

Example:

- `test/fixtures/mo2/priority_order/`

## How to derive a new fixture from your real MO2 setup (minimal-copy workflow)

- Pick **one story** (one failure mode) you want to lock in with a test.
- Identify the **2–4 mods** involved and the **1–10 files** that actually matter (usually a hotspot path).
- Copy only those minimal file subtrees into `test/fixtures/mo2/<caseName>/mods/<ModName>/...`
- Create `profiles/<ProfileName>/modlist.txt` with the same enabled state and **order** for those mods.
- If the story involves generated outputs, copy only the relevant outputs into `overwrite/...`.
- Keep case names stable and descriptive (examples): `ui_swf_conflict`, `scripts_conflict`, `skse_dll_conflict`, `overwrite_nemesis`, `dyndolod_output`.

## Notes

- `.mohidden` files should be included when relevant to “Hide” semantics; Infinium must ignore them for VFS winners/conflicts.
- Keep fixtures small. The goal is stable, deterministic signal coverage—not realism by size.

