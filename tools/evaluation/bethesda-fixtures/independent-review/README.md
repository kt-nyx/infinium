# Independent Bethesda fixture review

These tools author M1 Slice 3.5 Bethesda oracle evidence without using the
fixture generator, Infinium's production parser, production parser tests,
Mutagen, xEdit, held-out content, or taxonomy answers.

- `bounded_raw_reader.py` is a bounded raw TES4 reader. It keeps physical-file
  and decompressed-record offsets separate, enforces the frozen count/depth/
  decompression limits, rejects malformed framing, and derives only the
  allowlisted byte facts.
- `manual_hex_audit.ps1` is the separately implemented annotated hexadecimal
  worksheet technique. Its traversal, decompression, master/FormKey, field,
  link, chain, winner, and boundary decoding do not call the Python reader.
- `build_oracles.py` first requires exact structural and semantic agreement
  between the two retained reports, then mechanically emits
  `oracle/independent-byte-facts.json` and `expected-oracle.json`. Mutation
  partitions compare logical baseline and variant facts; artifact dependency
  alone never classifies a fact as changed.
- `build_taxonomy_projections.py` independently expands the accepted byte facts
  into the exhaustive EVAL-0086 subject set and emits both the sealed taxonomy
  projection and its answer-free literal production-subject binding. It does
  not import or inspect production analyzer code or output. Its only read
  dependencies are the two source artifacts declared in its output:
  `oracle/independent-byte-facts.json` and
  `inputs/snapshot/accepted-order.json`; it does not reopen plugin bytes or an
  independent-reader report.
- `self_test.py` corrupts reader FormKey/link/chain output, empties manual
  semantic output, and verifies the AIDT/DATA one-byte mutation partitions.

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

# After all five byte oracles exist, build the three EVAL-0086 projections.
python tools/evaluation/bethesda-fixtures/independent-review/build_taxonomy_projections.py `
  test-data/evaluation/m1-semantic/BETH-NPC-DEV `
  test-data/evaluation/m1-semantic/BETH-REFR-DEV `
  test-data/evaluation/m1-semantic/BETH-UNSUPPORTED-VAL

# Re-run build_oracles.py for those three packages so expected-oracle.json
# owns the finished taxonomy projection in its exact reference closure.

python tools/evaluation/bethesda-fixtures/independent-review/self_test.py
```

Any mismatch between techniques, earliest malformed boundary, declared
mutation scope, or accepted allowlist blocks oracle acceptance and must be
reported before fixture bytes or answers change.
