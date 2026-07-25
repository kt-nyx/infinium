# Legacy implementation assessment

Status: Draft  
Last reviewed: 2026-07-24

## Location

The abandoned implementation, including its dependencies, build artifacts,
fixtures, caches, logs, configuration, untracked work, and dirty tracked state,
was moved intact on 2026-07-24 to:

```text
legacy/
```

The Git repository remains at the project root. The move intentionally produces
a large path-changing diff and preserves the pre-existing uncommitted state.

## Original approach

The legacy application used:

- Electron;
- React and TypeScript;
- Node-based main/renderer processes;
- a Rust LOOT helper;
- experimental MO2/VFS indexing;
- LangChain/OpenAI agent logic;
- Nexus integration;
- hard-coded and heuristic rules;
- Vitest tests.

## Verification snapshot before archival

During the 2026-07-22 assessment:

- tests passed: 36 tests across 8 files;
- TypeScript typecheck passed;
- production build passed;
- lint failed with 29 errors and 3 warnings;
- the renderer bundle exceeded the configured size warning threshold;
- the dependency audit reported 10 production vulnerabilities at that time;
- the worktree contained extensive tracked and untracked WIP.

These results describe the preserved snapshot; they are not guarantees about
future tooling or dependencies.

## Post-move validation

From the new `legacy/` working directory on 2026-07-24:

- `npm test -- --run` passed all 36 tests across 8 files;
- `npm run typecheck` passed.

This confirms that relative test and TypeScript project paths still resolve
after archival. It does not change the implementation's legacy or
non-authoritative status.

## Structural trust problems

Examples identified during review:

- plugin ownership inferred through mod-name prefix matching;
- hard-coded game assumptions;
- approximate VFS/provider reconstruction;
- incomplete archive/full-file coverage and weak invalidation;
- LOOT helper behavior not equivalent to a fully metadata-loaded LOOT analysis;
- inconsistent winner assumptions between components;
- sequential high-volume LLM analysis with weak evidence;
- hard-coded name lists and overlap heuristics treated as issues;
- mocked documentation/RAG content;
- renderer fallback to fabricated analysis on failure;
- stale and overlapping orchestration approaches;
- insecure Electron settings and broad IPC/filesystem authority;
- plaintext credential handling;
- large monolithic UI and service modules;
- documentation inconsistent with the implementation.

## Authority

The legacy implementation is not:

- the product specification;
- a reliable model of MO2 effective state;
- the source of truth for analysis behavior;
- an accepted architecture;
- evidence that a capability is feasible or correct.

It may be inspected for historical context, fixtures, ideas, and possible
mechanical reuse only after independent validation.

## Change policy

Do not modify `legacy/` during rewrite work unless a task explicitly concerns
legacy preservation, extraction, or verification. New implementation work must
not occur inside the legacy directory.
