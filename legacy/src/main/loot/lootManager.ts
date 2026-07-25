import { app } from "electron";
import { spawn } from "node:child_process";
import { promises as fs } from "node:fs";
import path from "node:path";
import type { ProfileSnapshot, Settings } from "../../shared/types";
import { loadSettings } from "../config";
import { logger } from "../logging";

export interface LootMissingMaster {
  plugin: string;
  masters: string[];
}

export type LootMessageSeverity = "error" | "warning" | "note";

export interface LootPluginMessage {
  plugin: string;
  severity: LootMessageSeverity;
  text: string;
  language?: string;
  condition?: string;
}

export interface LootPluginMetadata {
  name: string;
  index: number;
  sortedIndex: number;
  isActive: boolean;
  isMaster: boolean;
  isLightPlugin: boolean;
  isEmpty: boolean;
  loadsArchive: boolean;
  version?: string;
  bashTags?: string[];
  missingMasters?: string[];
  requirements?: string[];
  incompatibilities?: string[];
  messages?: LootPluginMessage[];
}

export interface LootStats {
  pluginsAnalysed: number;
  missingMasterCount: number;
  warningCount: number;
}

export interface LootReportMetadata {
  lootModeUsed: "libloot" | "mocked" | "unavailable";
  gameId: string;
  gamePath: string;
  stats: LootStats;
  ambiguousLoadOrder?: boolean;
  plugins?: LootPluginMetadata[];
  rawLiblootMetadata?: Record<string, unknown>;
}

export interface LootReport {
  timestamp: string;
  summary: string;
  missingMasters: LootMissingMaster[];
  warnings: string[];
  loadOrder: string[];
  metadata?: LootReportMetadata;
}

interface LiblootProfile {
  plugins: string[];
  modRoots: string[];
}

interface LiblootRequest {
  game: ProfileSnapshot["game"];
  gamePath: string;
  profile: LiblootProfile;
}

interface LiblootStatsInternal {
  pluginsAnalysed: number;
  missingMasterCount: number;
  warningCount: number;
}

interface LiblootMissingMasterInternal {
  plugin: string;
  masters: string[];
}

type LiblootMessageSeverityInternal = LootMessageSeverity;

interface LiblootPluginMessageInternal {
  plugin: string;
  severity: LiblootMessageSeverityInternal;
  text: string;
  language?: string;
  condition?: string;
}

interface LiblootPluginMetadataInternal {
  name: string;
  index: number;
  sortedIndex: number;
  isActive: boolean;
  isMaster: boolean;
  isLightPlugin: boolean;
  isEmpty: boolean;
  loadsArchive: boolean;
  version?: string;
  bashTags?: string[];
  missingMasters?: string[];
  requirements?: string[];
  incompatibilities?: string[];
  messages?: LiblootPluginMessageInternal[];
}

interface LiblootMetadataInternal {
  lootVersion?: string | null;
  gameId: string;
  normalizedGamePath: string;
  stats: LiblootStatsInternal;
  ambiguousLoadOrder?: boolean;
  plugins?: LiblootPluginMetadataInternal[];
  // Allow the helper to attach arbitrary extra metadata without the TS
  // side needing to know about it ahead of time.
  extra?: Record<string, unknown>;
}

interface LiblootResponseInternal {
  timestamp: string;
  missingMasters: LiblootMissingMasterInternal[];
  warnings: string[];
  sortedLoadOrder: string[];
  metadata: LiblootMetadataInternal;
  error?: string;
}

const fileExists = async (targetPath: string): Promise<boolean> => {
  try {
    await fs.access(targetPath);
    return true;
  } catch {
    return false;
  }
};

const writeLootDebugLog = async (payload: unknown): Promise<void> => {
  try {
    const baseDir = app?.getPath?.("userData") ?? process.cwd();
    const logDir = path.join(baseDir, "logs");
    await fs.mkdir(logDir, { recursive: true });
    const target = path.join(logDir, "loot-libloot-debug.log");
    const line = `[${new Date().toISOString()}] ${JSON.stringify(payload, null, 2)}\n`;
    await fs.appendFile(target, line, "utf-8");
  } catch {
    // Swallow logging errors; LOOT analysis should not fail because debug
    // logging could not be written.
  }
};

/**
 * Historically this function tried to infer and validate LOOT.exe paths based
 * on multiple settings fields. With the libloot-based helper approach, all of
 * that configuration has been superseded by a single Skyrim SE/AE data path.
 *
 * We keep the function around to avoid breaking IPC handlers, but it now acts
 * as a no-op pass-through.
 */
export const detectLootPaths = (settings: Settings): Promise<Settings> => Promise.resolve(settings);

const resolveLiblootHelperPath = async (): Promise<string> => {
  const exeName = process.platform === "win32" ? "loot-helper.exe" : "loot-helper";

  const appPath = app?.getAppPath?.() ?? process.cwd();

  const candidates = [
    // Packaged app: Electron exposes the resources directory via
    // process.resourcesPath.
    path.join(process.resourcesPath ?? appPath, "loot-helper", exeName),
    // Dev: helper copied into resources/loot-helper locally.
    path.join(appPath, "resources", "loot-helper", exeName),
    // Dev: run directly from the Rust build output if available.
    path.join(appPath, "loot-helper", "target", "release", exeName),
    path.join(appPath, "loot-helper", "target", "debug", exeName),
  ];

  for (const candidate of candidates) {
    if (await fileExists(candidate)) {
      return candidate;
    }
  }

  throw new Error(
    `libloot helper executable not found. Looked in: ${candidates
      .map((c) => `"${c}"`)
      .join(", ")}. Ensure the helper is built and bundled correctly.`,
  );
};

const resolveGameDataDir = (gamePath: string): string => {
  const normalized = path.normalize(gamePath);
  const lastSegment = path.basename(normalized).toLowerCase();
  if (lastSegment === "data") {
    return normalized;
  }
  return path.join(normalized, "Data");
};

const resolvePluginListForProfile = async (
  snapshot: ProfileSnapshot,
  gamePath: string,
): Promise<string[]> => {
  const fromProfile = snapshot.pluginLoadOrder ?? [];
  const seen = new Set<string>();
  const result: string[] = [];

  const pushIfNew = (name: string) => {
    const key = name.toLowerCase();
    if (seen.has(key)) return;
    seen.add(key);
    result.push(name);
  };

  // Seed with the profile-reported load order so we preserve MO2 ordering
  // where available.
  fromProfile.forEach((name) => {
    if (name) pushIfNew(name);
  });

  const dataDir = resolveGameDataDir(gamePath);

  let dataPlugins: string[] = [];
  try {
    const entries = await fs.readdir(dataDir, { withFileTypes: true });
    dataPlugins = entries
      .filter((entry) => entry.isFile())
      .map((entry) => entry.name)
      .filter((name) => /\.(esm|esp|esl)$/i.test(name));
  } catch (error) {
    await logger.warn(
      `[LOOT] Failed to read Skyrim Data directory at "${dataDir}": ${(error as Error).message}. ` +
        "Proceeding with MO2-reported plugin list only.",
    );
  }

  if (dataPlugins.length) {
    // Ensure the canonical base game masters are present first in the list if
    // they physically exist in the Data directory. This prevents LOOT from
    // falsely flagging them as missing masters when MO2 does not list them
    // explicitly in the profile plugins.
    const baseMasters = [
      "Skyrim.esm",
      "Update.esm",
      "Dawnguard.esm",
      "HearthFires.esm",
      "Dragonborn.esm",
    ];

    const lowerFromData = new Map<string, string>();
    dataPlugins.forEach((name) => {
      lowerFromData.set(name.toLowerCase(), name);
    });

    baseMasters.forEach((master) => {
      const resolved = lowerFromData.get(master.toLowerCase());
      if (resolved) {
        pushIfNew(resolved);
      }
    });

    // Add any remaining plugins from Data (including Creation Club content)
    // that weren't already present, in a stable alphabetical order.
    const remaining = dataPlugins
      .filter((name) => !seen.has(name.toLowerCase()))
      .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));

    remaining.forEach((name) => pushIfNew(name));
  }

  return result;
};

const buildLiblootRequest = async (
  snapshot: ProfileSnapshot,
  gamePath: string,
): Promise<LiblootRequest> => {
  const enabledMods = snapshot.mods.filter((mod) => mod.enabled);
  const modRoots = Array.from(
    new Set(enabledMods.map((mod) => mod.path).filter((p) => typeof p === "string" && p.length)),
  );

  const plugins = await resolvePluginListForProfile(snapshot, gamePath);

  return {
    game: snapshot.game,
    gamePath,
    profile: {
      plugins,
      modRoots,
    },
  };
};

const runLiblootHelper = async (request: LiblootRequest): Promise<LiblootResponseInternal> => {
  const helperPath = await resolveLiblootHelperPath();

  return await new Promise<LiblootResponseInternal>((resolve, reject) => {
    const child = spawn(helperPath, [], {
      stdio: ["pipe", "pipe", "pipe"],
    });

    const chunks: Buffer[] = [];
    const errChunks: Buffer[] = [];

    child.stdout.on("data", (data: Buffer) => {
      chunks.push(data);
    });
    child.stderr.on("data", (data: Buffer) => {
      errChunks.push(data);
    });

    child.on("error", (error) => {
      reject(error);
    });

    child.on("close", (code) => {
      const stdout = Buffer.concat(chunks).toString("utf-8").trim();
      const stderr = Buffer.concat(errChunks).toString("utf-8").trim();

      if (code !== 0) {
        reject(
          new Error(
            `libloot helper exited with code ${code}. ` +
              (stderr ? `stderr: ${stderr.slice(0, 2000)}` : "No stderr output."),
          ),
        );
        return;
      }

      if (!stdout) {
        reject(new Error("libloot helper produced no output on stdout."));
        return;
      }

      try {
        const parsed = JSON.parse(stdout) as LiblootResponseInternal;
        if (parsed.error) {
          reject(new Error(`libloot helper reported error: ${parsed.error}`));
          return;
        }
        resolve(parsed);
      } catch (error) {
        reject(
          new Error(
            `Failed to parse libloot helper JSON output: ${(error as Error).message}. ` +
              `Raw output (truncated): ${stdout.slice(0, 2000)}`,
          ),
        );
      }
    });

    child.stdin.write(JSON.stringify(request));
    child.stdin.end();
  });
};

const parseLiblootResponseToReport = (
  snapshot: ProfileSnapshot,
  request: LiblootRequest,
  response: LiblootResponseInternal,
): LootReport => {
  if (!response.metadata || !response.metadata.stats) {
    throw new Error("libloot helper returned malformed metadata or stats.");
  }

  const {
    missingMasters: missingMastersRaw,
    warnings: helperWarnings,
    sortedLoadOrder,
    metadata,
  } = response;

  const stats: LootStats = {
    pluginsAnalysed: metadata.stats.pluginsAnalysed,
    missingMasterCount: metadata.stats.missingMasterCount,
    warningCount: metadata.stats.warningCount,
  };

  const missingMasters: LootMissingMaster[] = (missingMastersRaw ?? []).map((entry) => ({
    plugin: entry.plugin,
    masters: entry.masters,
  }));

  const pluginMessages: LootPluginMessage[] = [];
  if (metadata.plugins) {
    metadata.plugins.forEach((plugin) => {
      if (!plugin.messages) return;
      plugin.messages.forEach((msg) => {
        pluginMessages.push({
          plugin: plugin.name,
          severity: msg.severity,
          text: msg.text,
          language: msg.language,
          condition: msg.condition,
        });
      });
    });
  }

  const loadOrder =
    sortedLoadOrder && sortedLoadOrder.length ? sortedLoadOrder : snapshot.pluginLoadOrder;

  const metadataOut: LootReportMetadata = {
    lootModeUsed: "libloot",
    gameId: metadata.gameId ?? snapshot.game,
    gamePath: metadata.normalizedGamePath ?? request.gamePath,
    stats,
    ambiguousLoadOrder: metadata.ambiguousLoadOrder,
    plugins: metadata.plugins,
    rawLiblootMetadata: metadata.extra,
  };

  const summary = [
    "LOOT (libloot) analysis completed.",
    `Plugins analysed: ${stats.pluginsAnalysed}.`,
    `Missing masters: ${stats.missingMasterCount}.`,
    `Warnings: ${stats.warningCount}.`,
  ].join(" ");

  return {
    timestamp: response.timestamp,
    summary,
    missingMasters,
    // Preserve only the raw warnings reported by the libloot helper. Any
    // higher-level aggregation (e.g., combining plugin messages or missing
    // master summaries) is handled at the rules/evaluation layer so that we
    // don't conflate different kinds of signals here.
    warnings: helperWarnings ?? [],
    loadOrder,
    metadata: metadataOut,
  };
};

export const runLootForProfile = async (snapshot: ProfileSnapshot): Promise<LootReport> => {
  const settings = await loadSettings();

  const gamePath = settings.skyrimSeDataPath;
  if (!gamePath) {
    throw new Error(
      "Skyrim SE/AE data path is not configured. Configure the Skyrim SE Data Path in Settings before running LOOT analysis.",
    );
  }

  const request = await buildLiblootRequest(snapshot, gamePath);

  await logger.info(
    `[LOOT] Running libloot helper for game=${request.game} gamePath=${request.gamePath} ` +
      `plugins=${request.profile.plugins.length} modRoots=${request.profile.modRoots.length}`,
  );

  const response = await runLiblootHelper(request);

  // Persist a verbose snapshot of the raw libloot helper response for
  // debugging and inspection. This is written to a separate debug log file so
  // that it does not spam the main app log or console.
  await writeLootDebugLog({
    kind: "libloot-helper-response",
    profileId: snapshot.profileId,
    game: snapshot.game,
    request: {
      plugins: request.profile.plugins,
      modRoots: request.profile.modRoots,
      gamePath: request.gamePath,
    },
    response,
  });

  const report = parseLiblootResponseToReport(snapshot, request, response);

  const pluginCount = report.metadata?.stats.pluginsAnalysed ?? snapshot.pluginLoadOrder.length;
  const missingMasterCount = report.metadata?.stats.missingMasterCount ?? 0;
  const warningCount = report.metadata?.stats.warningCount ?? report.warnings.length;

  await logger.debug(
    `[LOOT] libloot analysis completed: pluginsAnalysed=${pluginCount}, ` +
      `missingMasters=${missingMasterCount}, warnings=${warningCount}`,
  );

  return report;
};

// Expose for unit tests.
export const __test_parseLiblootResponseToReport = parseLiblootResponseToReport;
