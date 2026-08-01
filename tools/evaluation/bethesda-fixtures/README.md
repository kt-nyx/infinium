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

Bethesda fixture version `1.0.1` emits full 128-byte Skyrim `RACE/DATA`
records and nests format-valid `REFR` records beneath interior CELL block,
sub-block, child, and persistent-child groups. `verify` also retains the
declared one-byte and record-order mutation invariants after that nesting.

After promoting a reviewed input freeze, refresh the non-answer-bearing Slice 3
capture-binding receipts:

```powershell
node tools/evaluation/bethesda-fixtures/snapshot-receipts.mjs
```

Each receipt preregisters the exact provider/plugin order and content hashes.
The evaluation test independently reconstructs that projection from an actual
`Mo2SnapshotCapture` result and hashes the captured winner bytes. The receipt
is not itself represented as a captured MO2 snapshot.

The independent reviewer uses `independent-review/bounded_raw_reader.py` and
`independent-review/manual_hex_audit.ps1` against the same frozen bytes, then
builds the supplemental and seven-document oracle content with
`independent-review/build_oracles.py`. These tools do not import generator
answer tables or production parser code. The PowerShell technique independently
decodes the answer-bearing semantic projection; oracle construction stops if
that projection disagrees with the Python reader.

After independent byte and taxonomy review is complete, bind all retained
inputs and oracle evidence into the five complete fixture packages:

```powershell
node tools/evaluation/bethesda-fixtures/finalize-packages.mjs
```

Package finalization does not execute a semantic analyzer and does not pass
`EVAL-0052` or `EVAL-0086`.
