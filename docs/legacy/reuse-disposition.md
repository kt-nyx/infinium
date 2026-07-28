# Legacy reuse disposition

Status: Draft  
Last reviewed: 2026-07-28

## Default disposition

Preserve the old implementation as archaeological/reference material and build
the replacement from accepted product requirements and architecture decisions.

No file is approved for direct transplantation merely because it exists.

## Potentially useful after validation

- MO2 profile-file fixtures;
- synthetic/adversarial test ideas;
- selected `meta.ini` parsing concepts;
- subprocess JSON-boundary ideas;
- UI workflow concepts;
- Nexus identity/enrichment experiments;
- private large-profile shape/scale observations, never as a correctness
  oracle, representative corpus, or source of special-case rules;
- external-tool packaging lessons.

## Rewrite from accepted contracts

- domain/evidence/finding/case model;
- MO2 effective-state reconstruction;
- file/archive provider index;
- semantic analyzers and rules;
- LOOT integration;
- orchestration/jobs/caching;
- LLM prompts, schemas, and provider abstraction;
- IPC/security/credentials;
- renderer data contracts;
- failure and coverage behavior;
- report/readiness semantics.

## Prohibited reuse patterns

- mocked or fabricated production fallbacks;
- guessed mod/plugin ownership;
- hard-coded real mod names as semantic rules;
- generic overlap as incompatibility;
- model-generated local state;
- approximate cache validity presented as exact;
- unrestricted renderer/system authority;
- duplicated competing pipelines.

## Extraction process

Any proposed reuse should:

1. identify the legacy file under `legacy/`;
2. state the accepted requirement it helps satisfy;
3. document known defects and assumptions;
4. create independent tests/fixtures;
5. port the smallest general component into the new architecture;
6. re-review it without relying on legacy tests alone.
