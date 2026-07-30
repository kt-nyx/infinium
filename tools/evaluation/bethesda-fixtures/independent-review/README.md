# Independent Bethesda fixture review

These tools author M1 Slice 3.5 Bethesda oracle evidence without using the
fixture generator, Infinium's production parser, production parser tests,
Mutagen, xEdit, held-out content, or taxonomy answers.

- `bounded_raw_reader.py` is a bounded raw TES4 reader. It keeps physical-file
  and decompressed-record offsets separate, enforces the frozen count/depth/
  decompression limits, rejects malformed framing, and derives only the
  allowlisted byte facts.
- `manual_hex_audit.ps1` is the separately implemented annotated hexadecimal
  worksheet technique. Its traversal and decompression implementation do not
  call the Python reader.
- `build_oracles.py` first requires agreement between the two retained reports,
  then mechanically emits `oracle/independent-byte-facts.json` and
  `expected-oracle.json`.

Run the two evidence techniques and builder only after an explicit frozen-byte
receipt:

```powershell
$package = 'test-data/evaluation/m1-semantic/BETH-NPC-DEV'
$scratch = 'work/oracle-review'

python tools/evaluation/bethesda-fixtures/independent-review/bounded_raw_reader.py `
  $package "$scratch/BETH-NPC-DEV-reader.json"

& tools/evaluation/bethesda-fixtures/independent-review/manual_hex_audit.ps1 `
  -PackagePath $package `
  -OutputPath "$scratch/BETH-NPC-DEV-manual.json"

python tools/evaluation/bethesda-fixtures/independent-review/build_oracles.py `
  $package `
  "$scratch/BETH-NPC-DEV-reader.json" `
  "$scratch/BETH-NPC-DEV-manual.json"
```

Any mismatch between techniques, earliest malformed boundary, declared
mutation scope, or accepted allowlist blocks oracle acceptance and must be
reported before fixture bytes or answers change.
