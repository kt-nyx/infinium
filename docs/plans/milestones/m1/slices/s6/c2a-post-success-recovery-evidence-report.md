# M1 Slice 6 C2A post-success recovery evidence report

Status: Accepted
Last reviewed: 2026-08-20
Owner: Project owner

## Outcome

The zero-effect recovery completed exactly once under activation commit
`ccb1934b3627a7bf9bd93c54a626ed1e3db20aaa` and runtime authority
`infinium.m1-s6.runtime-authority/credential-evidence-recovery/d433e048-da99-4acb-ac74-7bc4ce512e50`,
SHA-256 `7003d3dcc061c94a7c8b3bd398ad67b2313ed93bf62a918e1bc40ff7abd38b2f`.
It appended one ledger event and performed no helper, UI, Credential Manager,
DNS, network, provider, billable, product-state, or existing-evidence effect.

## Exact ledger evidence

- pre-recovery ledger SHA-256:
  `d69019c4674ca3928011b269053ac672d7cf1163b063d7b26310a3c753453f38`;
- post-recovery ledger SHA-256:
  `add1f5f7f3e5b8c010a988de2130647172dd3efdd1cd8ad9b8c67dbeae20e0ff`;
- lines: five before, six after;
- the exact post-recovery byte prefix preceding line six hashes to the complete
  pre-recovery ledger SHA-256, proving no prefix rewrite;
- appended sequence: `6`;
- state: `credential-evidence-accepted`;
- event: `credential-post-success-validator-defect-evidence-accepted`;
- predecessor event hash:
  `a1369f547801fa282334585a17f31ebf52f7028ad836b3026738f340ce50b2f9`;
- appended event hash:
  `a56e6accea6bb34fd983492791dc3b02cd1df4f05c1d128edec7782898433e1a`;
- accepted evidence ID: `wp9-production-profile-enrollment-evidence-v2`;
- accepted evidence SHA-256:
  `0fe89804afc3aaaa04d59961e711099adbe656466fd033e54c55ad709cb3042a`;
- recorded at: `2026-08-20T04:09:09.6466746Z`.

The independent reviewer recomputed the event hash from the canonical ledger
material. Provider calls, DNS resolutions, request/input/output/raw-response
bytes, reservations, settlements, observed usage, possible-start latch, and
safety projection are all zero or absent. The native tuple
`W1/R2/D0/F1/T4` is retained historical C2A enrollment evidence, not a recovery
effect; the recovery itself authorized and performed zero native calls.

## Preserved identities

- successful enrollment evidence SHA-256:
  `0fe89804afc3aaaa04d59961e711099adbe656466fd033e54c55ad709cb3042a`;
- conservative failure evidence SHA-256:
  `1c83f83842a7a67a22aa658fb61140cf93eb01b23a8b8064167a3e79319c16cb`;
- durable product state: four files, identity SHA-256
  `6154788ac6fa8a9c2858b3451b4d35a11448b234672c0892ff49b86763058b56`;
- coordinator SHA-256:
  `8aa7d7873f24495c0caebad8ad84afef5cfa9d7d60e524d80455d65a85d0d191`;
- helper SHA-256:
  `60b51d2e46508560409553ab898a4cf45ef46f75a0cf3d77fc01dcf4bd00a9a5`;
- recovery runner SHA-256:
  `bc124063de91fc95d561408453f45793fcc032f62e3908175c5ccc2a46a26d35`;
- 126-file executable inventory SHA-256:
  `18f6c8da8b66e02c2100439272b58a5d6e2353ca454f9d0832a626f870d4fe71`.

The execution clone remained tracked-clean and detached at the exact activation
commit. No helper process survived or was launched by recovery.

## Independent acceptance and stop

Independent post-recovery review returned PASS with no must-fix. C2A is
accepted. The current owner direction requires a mandatory stop here. C2B
preparation, materialization, authority, and execution remain prohibited and
require a fresh explicit owner direction in a later task.
