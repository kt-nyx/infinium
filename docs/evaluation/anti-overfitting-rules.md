# Anti-overfitting rules

Status: Proposed  
Last reviewed: 2026-07-25

The first semantic proof uses an NPC appearance-versus-behavior conflict because
it exercises many product capabilities. It does not define the product's scope.

## Prohibited production behavior

- Real mod names or IDs hard-coded into generic semantic classification logic
  solely to make a fixture pass.
- Logic keyed to fixture plugin/file names rather than the represented
  structure and evidence.
- Special cases introduced solely to pass one evaluation.
- Generic types or stages named after the initial NPC scenario.
- Assuming all semantic conflicts concern NPCs or appearance.
- Treating load order as proof of intent.
- Treating a known patch name as proof of patch effectiveness.
- Suppressing negative controls through maturity weighting in development.
- Changing expected outputs to match an unexplained implementation result.

This does not prohibit named tool/generator adapters, installed-mod identity
mappings, source-derived mod-specific compatibility claims, or curated LOOT
rules. Those remain data/provenance-bearing evidence rather than hidden
fixture-specific semantic exceptions.

## Required generalization checks

- Rename mods/plugins without changing semantics.
- Add unrelated mods without changing conclusions.
- Reorder only unrelated mods without changing conclusions.
- Change an effective winner and observe only dependency-relevant changes.
- Pair every harmful fixture with a structurally similar intentional/harmless
  fixture.
- Test malformed and unsupported data.
- Test ambiguous purpose and require abstention or an intent question.
- Validate at least one materially different non-NPC technical surface or
  affected game area before declaring the generic mechanism proven. The exact
  classification follows RQ-036; “non-NPC” is only the current proof-planning
  shorthand.

## Generic and domain responsibilities

Generic infrastructure may implement:

- provenance;
- override chains;
- changed-field sets;
- stale-value/reversion patterns;
- declared-purpose claims;
- evidence combination;
- candidates, hypotheses, findings, and cases.

Domain analyzers may encode stable Skyrim semantics:

- record relationships;
- field meaning;
- feature-graph construction;
- impact and validation rules.

Domain knowledge must be reusable across arbitrary mods within its documented
scope.

## Review requirement

Any rule introduced after a failing real-mod case must answer:

1. What general class of behavior does this represent?
2. What independent synthetic positive demonstrates it?
3. What matched negative prevents overreach?
4. Which declared domain scope owns it?
5. What evidence threshold and abstention behavior apply?
