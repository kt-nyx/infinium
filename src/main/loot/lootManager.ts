import { app } from "electron";
import { promises as fs } from "node:fs";
import path from "node:path";
import type { ProfileSnapshot, Settings } from "../../shared/types";

export interface LootReport {
  timestamp: string;
  summary: string;
  missingMasters: Array<{ plugin: string; masters: string[] }>;
  warnings: string[];
  loadOrder: string[];
  metadata?: Record<string, unknown>;
}

const portableFolder = (): string => {
  const userData = app?.getPath?.("userData") ?? process.cwd();
  return path.join(userData, "loot-portable");
};

const portableExecutable = (): string => path.join(portableFolder(), "LOOT.exe");

export const detectLootPaths = async (settings: Settings): Promise<Settings> => {
  const draft: Settings = { ...settings };

  if (!draft.lootPortablePath) {
    draft.lootPortablePath = portableExecutable();
  }

  if (draft.lootMode === "auto") {
    draft.lootMode = draft.lootInstalledPath
      ? "installed"
      : draft.lootCustomPath
        ? "custom"
        : "portable";
  }

  // TODO: unzip bundled LOOT portable archive on first run.
  try {
    if (draft.lootMode === "portable") {
      await fs.access(draft.lootPortablePath);
    }
  } catch {
    // Portable executable missing – leave as TODO for download/extraction.
  }

  return draft;
};

export const runLootForProfile = (snapshot: ProfileSnapshot): Promise<LootReport> => {
  // TODO: Invoke LOOT CLI with the selected executable and parse JSON output.
  const now = new Date().toISOString();
  const mockedMissingMasters = snapshot.pluginLoadOrder.length
    ? [
        {
          plugin: snapshot.pluginLoadOrder[0],
          masters: ["Unofficial Skyrim Special Edition Patch.esp"],
        },
      ]
    : [];

  return Promise.resolve({
    timestamp: now,
    summary: "Mocked LOOT analysis (real integration pending)",
    missingMasters: mockedMissingMasters,
    warnings: mockedMissingMasters.length
      ? [`LOOT detected missing masters for ${mockedMissingMasters[0].plugin}.`]
      : [],
    loadOrder: snapshot.pluginLoadOrder,
    metadata: {
      lootModeUsed: snapshot.lootAvailable ? "configured" : "mocked",
    },
  });
};
