# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

Infinium is a Windows-only Electron desktop application that analyzes Skyrim Mod Organizer 2 (MO2) profiles using agentic AI. It combines local heuristics with LangGraph agents to identify mod conflicts, load order issues, missing dependencies, and configuration problems.

The app integrates real LOOT functionality via a bundled Rust helper (`loot-helper`) that wraps libloot, and includes mocked integrations for Nexus Mods API and RAG-based documentation search.

## Essential Commands

### Development Workflow
```powershell
# First-time setup
mise install          # Install Node 24.11.1 LTS via mise
npm install           # Install dependencies

# Development (hot reload renderer, manual reload for main/preload changes)
npm run dev           # Starts Vite dev server + tsup watcher + Electron

# Building
npm run build         # Build main/preload (tsup) + renderer (vite)
npm run package       # Build + create Windows NSIS installer

# Build Rust LOOT helper only (runs automatically in predev/prebuild/prepackage)
npm run build:loot-helper
```

### Quality Gates
```powershell
npm run lint          # ESLint check
npm run lint:fix      # ESLint with auto-fix
npm run format        # Prettier format
npm run typecheck     # TypeScript compile check

# Tests (Vitest with Node + happy-dom environments)
npm test              # Run all tests once
npm run test:watch    # Watch mode
```

## Architecture

### Process Structure
- **Main process** (`src/main/`): Electron main, IPC handlers, domain logic (MO2 scanning, LOOT integration, Nexus client, LangChain agent, rules engine)
- **Renderer** (`src/renderer/`): Vite + React + Fluent UI single-page UI
- **Preload** (`src/main/preload.ts`): Context-isolated IPC bridge exposing `window.api`
- **Shared** (`src/shared/`): TypeScript types/models used by both main and renderer

### Key Domain Components

#### MO2 Integration (`src/main/mo2/`)
- `mo2Detector.ts`: Auto-detects MO2 instances from Windows Registry, filesystem scans, and environment variables (`SKYRIM_AI_MO2_INSTANCE`, `MO2_INSTANCE_PATH`)
- `mo2Scanner.ts`: Parses MO2 profile files (`modlist.txt`, `plugins.txt`, `loadorder.txt`) to build a `ProfileSnapshot`
- `mo2Meta.ts`: Reads `meta.ini` files for Nexus ID linking and version info

#### LOOT Integration (`src/main/loot/`)
- Uses a bundled Rust helper (`loot-helper`) that wraps libloot (GPL-3.0, shipped as external component)
- `lootManager.ts`: Spawns the helper binary, passes profile data as JSON, returns load order + missing masters + warnings
- Helper binary resolved from multiple locations (packaged: `process.resourcesPath/loot-helper/`, dev: `resources/loot-helper/`, Rust build output: `loot-helper/target/release/`)

#### Analysis Pipeline (`src/main/analysis/pipeline.ts`)
Two-stage analysis:
1. **Offline analysis** (`runOfflineAnalysis`): Rules engine + optional LOOT + optional Nexus enrichment
2. **Agentic analysis** (`runAgenticAnalysis`): Runs offline baseline, then invokes LangGraph agent with tools (LOOT, Nexus, RAG, rules) to enrich/expand issues

Complexity levels (1-5) control depth: level 1 = basic/fast, level 4+ = thorough with Nexus deep-dives. Opinionatedness (1-5) controls how curated vs stability-focused recommendations are.

#### Agentic AI (`src/main/agent/`)
- `skyrimAgentGraph.ts`: LangGraph ReAct agent that takes offline issues + profile snapshot, uses LangChain tools to investigate, returns enriched issues/recommendations
- `modAnalysisPass.ts`: Optional AI pass (gated by complexity >= 2) that analyzes mod descriptions/file metadata to tag overlap domains, extract requirements/patches/load-order rules, triage script/performance risk, and detect redundancies. Results stored as `overlapTagsAgent`, `requirementsAgent`, `loadOrderRulesAgent`, `variantAgent`, `scriptPerfRiskAgent`, `redundancyCandidatesAgent` on `ModInfo`
- `issueExpansion.ts`: LLM-based issue detail expansion for UI chat (OpenAI API required)
- `openaiClient.ts`: OpenAI model factory (requires `OPENAI_API_KEY` env var)
- Tools (`agent/tools/langchainTools.ts`): LOOT, rules, Nexus metadata/files/comments, RAG docs search, collection bug reports

#### Nexus Mods Integration (`src/main/nexus/`)
- **Currently mocked** (API envelope not finalized)
- `nexusClient.ts`: Placeholder for Nexus API client
- `enrichMods.ts`: Enriches `ModInfo` with Nexus metadata (latest version, category, downloads, importance scoring, staleness, topic/domain hints)
- `nexusSearch.ts`: Mod search by name

#### Rules Engine (`src/main/rules/rulesEngine.ts`)
Heuristic checks that run on every analysis:
- Missing masters
- Hard incompatibilities (LE vs SE/AE edition mismatches)
- Load order violations
- Redundancy detection
- Staleness warnings
Outputs baseline `Issue[]` and `Recommendation[]` consumed by agent

### Data Flow
1. User selects MO2 instance + profile in UI
2. Renderer calls `window.api.analysis.runAgentic()` (or `runOffline()`)
3. IPC handler (`src/main/ipcHandlers.ts`) calls `scanProfile()` to parse MO2 files → `ProfileSnapshot`
4. Offline analysis: rules engine + optional LOOT + optional Nexus enrichment
5. Agentic analysis: runs offline baseline, optional AI mod analysis pass (complexity >= 2), then LangGraph agent with tools
6. Agent reads descriptions, calls Nexus tools for metadata/files/comments, searches docs, produces enriched issues/recommendations
7. Result (`AnalysisResult`) returned to renderer with merged offline + agent findings

### Type System (`src/shared/types.ts`)
- `ProfileSnapshot`: MO2 profile state (mods, plugin load order, game edition)
- `ModInfo`: Mod metadata with optional Nexus fields (`nexusId`, `nexusCategory`, `gameSupport`, `installedVersion`, `latestVersion`), AI-enriched fields (`overlapTagsAgent`, `requirementsAgent`, etc.), and importance/staleness hints
- `Issue`: Finding with severity, category, affected mods/plugins, confidence, sources (loot/rules/nexus/rag/agent), evidence (Nexus URLs/IDs/comment IDs)
- `Recommendation`: Steps to address an issue
- `AnalysisResult`: Profile snapshot + issues + recommendations + metadata (complexity, opinionatedness, agent used, Nexus used/error)

## Development Practices

### Testing
- Vitest tests in `test/` directory (Node + happy-dom environments)
- Tests cover: LOOT manager, MO2 metadata parsing, rules engine, Nexus client/health, mod analysis pass schema
- Run individual test file: `npx vitest run test/mo2Meta.test.ts`

### Tooling
- **Runtime**: Node 24.11.1 LTS pinned via `.mise.toml` (managed by mise)
- **Build**: tsup (main/preload), Vite (renderer), Rust/cargo (loot-helper)
- **Linting**: ESLint (typescript-eslint, react-hooks, import-x)
- **Formatting**: Prettier
- **Git hooks**: Husky + lint-staged (auto-lint/format on commit)

### Environment Variables
- `OPENAI_API_KEY` (required): Enables agentic analysis and AI issue expansion
- `SKYRIM_AI_MO2_INSTANCE` or `MO2_INSTANCE_PATH` (optional): Hints for MO2 instance auto-detection
- Store secrets in `.env` (gitignored); never commit API keys

### LOOT Helper Build
The Rust helper is automatically built before dev/build/package via predev/prebuild/prepackage scripts. If you modify `loot-helper/`, re-run `npm run build:loot-helper` or restart `npm run dev`.

### Packaging
- `electron-builder` creates Windows NSIS installer
- GPL-3.0 libloot/loot-helper binaries bundled as external resources (never statically linked into MIT-licensed app code)
- Helper binaries placed in `resources/loot-helper/` and copied to `extraResources` by electron-builder

## Key Constraints
- **Windows-only**: Uses Windows Registry for MO2 detection, Windows paths, PowerShell conventions
- **OpenAI dependency**: Agentic features require OpenAI API key
- **GPL-3.0 boundary**: libloot and loot-helper are GPL-3.0; main app is MIT. They communicate via subprocess/JSON, never linked
- **Nexus/RAG mocked**: Nexus API and RAG search are placeholders pending API finalization

## Roadmap Notes (from README)
- Wire real Nexus API + OAuth/key storage
- Replace mocked RAG with SQLite + embeddings
- Improve dev ergonomics (hot reload Electron main process, better Husky setup)
- Code signing for Windows installer
- Finalize GPL-compliant LOOT bundling strategy
