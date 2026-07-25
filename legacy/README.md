# Infinium

Agentic desktop assistant for Skyrim Mod Organizer 2 (MO2) profiles. Runs only on Windows, bundles Electron + React + LangChain/LangGraph with real LOOT integration and mocked integrations for Nexus Mods and local RAG.

## Requirements

- Windows 10/11
- [mise](https://mise.com/) for runtime management (`node` pinned to **24.11.1 LTS** via `.mise.toml`).
- [Scoop](https://scoop.sh/) to install optional global tools (`mise`, `git`, `sqlite3`, etc.).
- npm 10.9+ (bundled with Node 24).
- An OpenAI API key (`OPENAI_API_KEY`) set in your environment to enable agentic analysis and AI issue expansion.

```powershell
scoop install git
scoop install nodejs-lts
# install mise via scoop shim or the official installer
```

## Getting Started

```powershell
# install project toolchain
mise install
npm install

# run renderer + Electron (Nexus/RAG features mocked for now)
npm run dev
```

### Scripts

- `npm run dev` – starts Vite dev server, tsup watcher, and Electron (manual reload on Electron rebuild for now – see TODO in `README`).
- `npm run build` – bundles main/preload (`tsup`) and renderer (`vite`).
- `npm run package` – invokes `electron-builder` (Windows NSIS target) after `build`.
- `npm run lint`, `npm run lint:fix`, `npm run format`, `npm run typecheck` – quality gates.
- `npm run test`, `npm run test:watch` – executes Vitest (Node+happy-dom environments) for backend/shared utilities.

## Project Structure

```
src/
  main/           # Electron main process, IPC handlers, config/logging, domain logic
  renderer/       # Vite + React + Fluent UI single-page UI
  shared/         # Shared TypeScript models referenced by both layers
```

Key domain folders (`src/main/*`) encapsulate LOOT integration, MO2 scanning, nexus client, RAG search, LangChain tools, and the analysis pipeline. Renderer components use a small IPC wrapper (via `preload.ts`) to request analyses and settings.

## Roadmap & TODOs

- Wire Nexus API + OAuth/key storage once API envelope is finalized.
- Replace mocked RAG search with SQLite + embeddings provider.
- Flesh out LangGraph agent (`src/main/agent/skyrimAgentGraph.ts`) and tool wiring.
- Improve dev ergonomics (hot reload Electron on rebuild, add proper Husky hooks running `lint-staged`).
- Sign Windows installer and verify GPL-compliant LOOT bundling before release.

## Licensing

- App source: MIT.
- libloot and any bundled helper binaries are GPL-3.0 – shipped as external components, never statically linked into the MIT-licensed Electron app.
- See `electron-builder.yml` for packaging considerations around LOOT/libloot payloads.
