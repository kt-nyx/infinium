# Infinium historical public evaluator `/4`

Protocol `infinium.evaluator-v2/4`, scorer and adapter `4.0.0`, and projection
`infinium.evaluator-v2.slice4-semantic-projection/3.0.0` are frozen historical
public evidence. The evaluator is retained only for the bounded public
regression use defined in
[`docs/evaluation/m1-slice4-protocol-4-bounded-regression-usage.md`](../../../docs/evaluation/m1-slice4-protocol-4-bounded-regression-usage.md).

The only current authorized entry point is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-m1-slice4-protocol4-bounded-regression.ps1
```

or the same script under PowerShell 7. A successful run terminates with
`BOUNDED_REGRESSION_PASS`. That label means only that the historical 23-file
freeze, current 20-file reusable core, and explicitly allowlisted current
public regression checks remain healthy. It is not the original frozen test
suite, a complete current semantic result, a private held-out verdict, Slice
4.5 or M1 `PASS`, a reliability claim, or a product verdict.

## Known exclusion

Protocol `/4` cannot represent the accepted partial `RACE/DATA` outcome that
retains independently proven common contribution facts while omitting only the
unavailable later-layer `face_gen_head` fact. The bounded wrapper does not
adapt or execute product output and does not exercise that state. `/4` must not
be changed or used to reject the accepted product behavior.

## Historical direct commands

The frozen executable still contains `protocol`, `calibrate`, `adapt`,
`score`, `compare-prepared`, and `score-corpus` for historical
reproducibility. Except for calibration invoked by the bounded wrapper, those
commands are not authorized for new active execution by the evaluator-deferral
plan. In particular, adaptation, oracle comparison, corpus scoring, private
manifests, and candidate execution are prohibited. Their presence is not an
active held-out workflow or an invitation to resume B2/C2.

Protocol `/3` schemas remain as immutable historical bytes supporting its
freeze record and `/4` predecessor provenance. No `/2` schema remains in the
current tool tree. Neither predecessor is an accepted or runnable alternative
for new work. Their exact inventory is in the bounded-regression usage
document.

## Frozen projection boundary

The `/4` reflection adapter historically projected named Slice 4 members
rather than recursively flattening the complete result. Included facts cover
the bounded result state, plugin/provider topology, evaluator-owned record and
contribution identity, override/winner state, selected NPC/RACE/REFR fields,
typed links, allowlisted-field counts, FaceGen/provider topology, evaluator-
owned taxonomy tuples, coverage, and capability gaps.

Excluded values include product-generated IDs, exact failure-code spelling,
typed AIDT subfields, physical paths, dependency fingerprints, timestamps,
exception or reason prose, display text, and incidental serialization fields.
The historical family authority remains in
`docs/evaluation/m1-slice4-heldout-oracle-authority-matrix.md`; it does not
broaden the current bounded-regression claim.

The immutable freeze is
`docs/evaluation/evaluator-v2-stage-a-final-bounded-freeze.json` at evaluator
commit `3693d19563c636cd2879804633ca4ce52448d2c1`. The wrapper verifies all 23
raw Git blobs at that commit and all 20 current non-test core files before any
public check runs.
