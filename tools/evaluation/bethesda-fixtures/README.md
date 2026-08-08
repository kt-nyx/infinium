# Bethesda semantic fixture input generator

This directory contains the project-authored, deterministic binary input
generator for M1 Slice 3.5. It uses only .NET/BCL APIs and consumes no game,
mod, xEdit, Mutagen, network, held-out, oracle, or production-parser input.

Generate the five authorized input matrices into an isolated staging root:

```powershell
dotnet run --project tools/evaluation/bethesda-fixtures/Infinium.BethesdaFixtures.Generator.csproj -- generate --output artifacts/bethesda-fixture-staging --seed 3520260730
```

Do not generate directly over live package roots after custodian-owned
`inputs/snapshot/` receipts exist. Promote only files owned by the generated
`construction-manifest.json`; preserve all custodian-owned extras.

Verify two clean fixed-seed runs are byte-identical and that every emitted byte
is covered by a construction region:

```powershell
dotnet run --project tools/evaluation/bethesda-fixtures/Infinium.BethesdaFixtures.Generator.csproj -- verify --seed 3520260730
```

The generator owns only `inputs/`. It does not create fixture package metadata,
oracles, taxonomy answers, held-out content, snapshot captures, or parser code.

Bethesda fixture version `1.4.0` retains the `1.0.1` full 128-byte Skyrim `RACE/DATA`
records and nests format-valid `REFR` records beneath interior CELL block,
sub-block, child, and persistent-child groups. `verify` also retains the
declared one-byte and record-order mutation invariants after that nesting.

The generator emits two distinct answer-free execution controls. The effective
scan configuration is valid under `infinium.scan.effective-configuration/v1`
and intentionally has no `cases` property. The separate
`infinium.evaluation.bethesda-case-matrix/v1` artifact is sourced only from the
accepted Slice 3.5 scenario inventory and retained project-authored execution
inputs. It declares scenario identity, operation, and exact input membership;
oracle expectations and denominator classifications are not generator inputs.

After promoting a reviewed input freeze, refresh the non-answer-bearing Slice 3
capture-binding receipts:

```powershell
node tools/evaluation/bethesda-fixtures/snapshot-receipts.mjs
```

Each receipt preregisters the exact provider/plugin order and content hashes.
The evaluation test independently reconstructs that projection from an actual
`Mo2SnapshotCapture` result and hashes the captured winner bytes. The receipt
is not itself represented as a captured MO2 snapshot or runtime plugin-order
input. Canonical Bethesda execution packages bind it through the dedicated
required `accepted_order_construction_input` role; installation-snapshot and
runtime plugin-order roles are not inferred from it.

The independent reviewer uses `independent-review/bounded_raw_reader.py` and
`independent-review/manual_hex_audit.ps1` against the same frozen bytes, then
builds the supplemental and seven-document oracle content with
`independent-review/build_oracles.py`. These tools do not import generator
answer tables or production parser code. The PowerShell technique independently
decodes the answer-bearing semantic projection; oracle construction stops if
that projection disagrees with the Python reader.

For the three EVAL-0086 packages, build the exhaustive sealed taxonomy
projection and literal subject-binding document after the byte oracle, then run
`build_oracles.py` once more so `expected-oracle.json` owns the final taxonomy
payload in its exact oracle closure:

```powershell
python tools/evaluation/bethesda-fixtures/independent-review/build_taxonomy_projections.py `
  test-data/evaluation/m1-semantic/BETH-NPC-DEV `
  test-data/evaluation/m1-semantic/BETH-REFR-DEV `
  test-data/evaluation/m1-semantic/BETH-UNSUPPORTED-VAL
```

The taxonomy tool reads only accepted-order receipts and independent byte
facts. It does not reopen plugin bytes or independent-reader reports and does
not load Infinium production assemblies or output. Finalization rejects
incomplete binding bijections, any taxonomy source outside that exact sealed
two-artifact set, and any retained oracle file absent from the expected
oracle's exact reference closure.

After independent byte and taxonomy review is complete, reseal the six current
public fixture packages under the active product contracts:

```powershell
node tools/evaluation/reseal-current-public-fixtures.mjs
```

The current-only resealer rejects predecessor fixture identities, pins the
independently authored semantic-truth digests, materializes answer-free product
analysis inputs, and refreshes exact retained-byte closures. It does not
execute a semantic analyzer or pass `EVAL-0052` or `EVAL-0086`.
