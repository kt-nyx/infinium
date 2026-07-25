# Taxonomy research dependency map

Status: Draft  
Last reviewed: 2026-07-25

This document inventories where Infinium needs classifications related to how
mods change Skyrim and what parts of the game they may affect. It is a scope map
for [RQ-036](open-questions.md), not the taxonomy result.

No closed list of mod purposes/types, technical modification surfaces, affected
game areas, consequence classes, or effect extents is accepted yet. Examples
in the current product documents preserve discovery intent and help scope
research; they must not become hard-coded enums, assumed hierarchies, or
complete coverage claims before RQ-036 is investigated and its resulting
product taxonomy is accepted.

## Classification concepts in scope

RQ-036 must determine the exact names, boundaries, relationships, and controlled
values for at least these provisional concepts:

- **Declared purpose/intended feature area:** what authoritative mod
  documentation says the mod is intended to add, remove, replace, or alter. It
  is source-supported intent, not proof of the mod's complete effective impact.
- **Technical modification surface:** how effective state is changed, such as
  plugin records, assets, scripts, configuration, native/runtime components,
  or generated output.
- **Affected game system/content area:** where the player-visible or systemic
  effect may manifest, such as progression, actors, world content, combat,
  interface, or presentation.
- **Consequence/impact class:** what kind of problem may result.
- **Effect extent:** how broadly the effect may manifest, provisionally
  represented by gameplay scope and blast radius.
- **Cross-cutting relationships:** many-to-many mappings where one technical
  surface affects several game areas, or one game area depends on several
  surfaces.

The investigation may split, merge, rename, or reject these provisional axes.
It must preserve raw observed facts so classification can be revised without
rewriting what a mod actually contained or what an analyzer observed.

## Product dependency inventory

| Product area | Current provisional descriptions | How the accepted taxonomy will be used |
|---|---|---|
| Product problem and effective-installation scope | Records, assets, archives, scripts, configuration, runtime/native components, generated output, and documentation | Describe required input/state coverage without presenting those mechanisms as exhaustive mod or game-area categories |
| Analyzer contracts and capability catalog | Requirements/masters, record interactions, assets, patches, runtime, generated output, configuration, installer choices, lifecycle safety, documentation, and runtime evidence | Declare supported surfaces, affected areas, consequence classes, exclusions, and unknown/unsupported scope |
| Semantic candidate generation | Record families, shared files, dependencies, feature graphs, runtime relationships, documentation claims, and scope-incongruent changes | Build and route candidates across taxonomy facets without assuming one record or file family equals one game area |
| Documentation intelligence and intent | Author descriptions, requirements, compatibility statements, lifecycle instructions, and local documentation | Classify declared purpose/intended feature area and claimed affected areas only when the source supports that mapping; do not treat a hosting-site category or inferred label as complete intent |
| Findings and cases | Domain, impact class, gameplay scope, blast radius, symptoms, severity, and confidence | Keep technical cause, affected area, consequence, extent, severity, and evidential confidence inspectable without conflation |
| Summary, navigation, and focused mod views | Finding counts, filters, case queues, mod detail, search, and drill-down | Provide faceted or hierarchical navigation that supports multi-label and cross-cutting findings |
| Coverage and readiness gaps | Parsed plugins, indexed providers, supported record families, documentation coverage, named analyzers, and unsupported areas | Report labeled denominators and unevaluated taxonomy areas without manufacturing one overall analyzed/safety percentage |
| Review priority and investigation depth | Impact, blast radius, symptoms, user intent, candidate breadth, and analyzer maturity | Route attention and optional deeper analysis while leaving severity and confidence independent |
| Change impact, remediation, and validation | Changed providers/records/assets/configuration/runtime/generated output plus predicted symptoms and test plans | Explain which technical surfaces and game areas may be affected and choose bounded validation steps |
| Evaluation and anti-overfitting | NPC/non-NPC, cell/quest, item/crafting, asset, runtime, configuration, and other provisional case labels | Stratify positive, negative, boundary, cross-cutting, unknown, and unsupported cases and expose unevaluated taxonomy coverage |
| Analyzer roadmap and milestone breadth | Semantic record/system families and named native, asset, script, configuration, generator, and lifecycle analyzers | Select implementation order from product risk and evidence rather than treating the current catalog order as a natural taxonomy |
| Architecture and integration reporting | MO2, filesystem, Bethesda semantic layer, LOOT, documentation, generators, logs, and LLM adapters | Map component capabilities and gaps to product taxonomy coverage without treating adapter boundaries as game-area boundaries |
| Historical persistence and exports | Findings, coverage, evaluations, and exported summaries | Retain the taxonomy version so later revisions do not silently reinterpret historical classifications |

## Current documents containing provisional inventories

- [`../product/product-definition.md`](../product/product-definition.md):
  example mechanisms behind the product problem and deterministic/LLM
  boundary.
- [`../product/requirements.md`](../product/requirements.md):
  effective-installation surfaces, analyzer capabilities, finding
  classifications, and coverage populations.
- [`../product/domain-model.md`](../product/domain-model.md):
  snapshot contents, finding fields, and coverage records.
- [`../product/severity-confidence-and-coverage.md`](../product/severity-confidence-and-coverage.md):
  provisional classification axes, impact candidates, and coverage examples.
- [`../product/analysis-catalog.md`](../product/analysis-catalog.md):
  planning sections, semantic-family examples, and cross-layer feature graphs.
- [`../product/workflows.md`](../product/workflows.md):
  results aggregation, filtering, case detail, and verification.
- [`../product/scope-and-milestones.md`](../product/scope-and-milestones.md):
  NPC/non-NPC proof shorthand and M3 capability breadth.
- [`../architecture/overview.md`](../architecture/overview.md) and
  [`../architecture/integrations.md`](../architecture/integrations.md):
  responsibility and adapter boundaries that must report taxonomy coverage but
  are not themselves a game-area taxonomy.
- [`../evaluation/evaluation-strategy.md`](../evaluation/evaluation-strategy.md),
  [`../evaluation/case-catalog.md`](../evaluation/case-catalog.md),
  [`../evaluation/fixture-guidelines.md`](../evaluation/fixture-guidelines.md),
  and
  [`../evaluation/anti-overfitting-rules.md`](../evaluation/anti-overfitting-rules.md):
  corpus stratification, provisional domain labels, fixture metadata, and
  generalization requirements.
- [`open-questions.md`](open-questions.md): specialized technical-surface and
  analyzer-roadmap questions whose outputs must map back to RQ-036.

## Rules before taxonomy acceptance

- Preserve example lists as explicitly provisional and non-exhaustive.
- Do not implement closed production enums from those examples.
- Do not infer declared purpose, affected game area, or consequence solely from
  record type, file extension, hosting-site mod category, or analyzer ownership.
- Support multi-label, cross-cutting, unknown, and unsupported classification
  in research and fixture designs.
- Keep severity, confidence, symptoms, and evidential authority separate from
  game-area classification.
- Keep raw observations and source claims independently addressable so a later
  taxonomy revision can reclassify derived output without rewriting evidence.
- Version persisted taxonomy-bound findings, coverage, evaluations, and
  exports.

## Related classifications outside RQ-036

The following are not taxonomies of mod type or affected game area and should
not be forced under RQ-036:

- evidence/source authority classes;
- severity and confidence scales;
- analyzer maturity and readiness states;
- job lifecycle and coverage execution states;
- finding dispositions and suppression;
- source-registry classes;
- installed-mod identity/topology states such as renamed, split, merged,
  generated, personal, or unresolved;
- runtime-evidence association/freshness classes such as exact, matched,
  likely, unknown, or historical;
- scan-depth presets, cost/resource controls, and operational configuration;
- evaluation fixture types such as synthetic, real-mod, or scale.

They may consume or report taxonomy-bound data, but their own definitions are
governed by their respective product and evaluation requirements.
