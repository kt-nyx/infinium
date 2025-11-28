import { dialog, ipcMain } from "electron";
import { promises as fs } from "node:fs";
import path from "node:path";
import type { AnalysisResult, Issue, ProfileSnapshot, Settings } from "../shared/types";
import { loadSettings, saveSettings } from "./config";
import { detectLootPaths } from "./loot/lootManager";
import {
  detectMo2Instances,
  detectMo2InstancesFromFilesystem,
  detectMo2InstancesFromRegistry,
  getMo2SearchInfo,
} from "./mo2/mo2Detector";
import { getProfiles, scanProfile } from "./mo2/mo2Scanner";
import type { AnalysisRunOptions } from "../shared/analysis";
import { runAgenticAnalysis, runOfflineAnalysis } from "./analysis/pipeline";
import { logger, getLogFilePath } from "./logging";
import { expandIssueExplanation } from "./agent/issueExpansion";
import { isOpenAIConfigError } from "./agent/openaiClient";

let cachedSettings: Settings | null = null;

const ensureSettings = async (): Promise<Settings> => {
  if (!cachedSettings) {
    cachedSettings = await detectLootPaths(await loadSettings());
  }
  return cachedSettings;
};

const persistSettings = async (settings: Settings): Promise<Settings> => {
  cachedSettings = await detectLootPaths(settings);
  await saveSettings(cachedSettings);
  return cachedSettings;
};

const exportAnalysis = async (
  analysis: AnalysisResult,
  format: "json" | "html",
  filePath: string,
): Promise<string> => {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  if (format === "json") {
    await fs.writeFile(filePath, JSON.stringify(analysis, null, 2), "utf-8");
  } else {
    const html = `<!doctype html><html><head><meta charset="utf-8"><title>Skyrim AI Report</title></head><body><pre>${JSON.stringify(
      analysis,
      null,
      2,
    )}</pre></body></html>`;
    await fs.writeFile(filePath, html, "utf-8");
  }
  return filePath;
};

export const registerIpcHandlers = (): void => {
  ipcMain.handle("settings:get", async () => {
    await logger.info("Loading settings via IPC");
    return ensureSettings();
  });

  ipcMain.handle("settings:save", async (_event, payload: Settings) => {
    await logger.info("Persisting settings via IPC");
    return persistSettings(payload);
  });

  ipcMain.handle("mo2:detect", async () => {
    const settings = await ensureSettings();
    await logger.debug(
      `[IPC] mo2:detect called; existingInstances=${(settings.mo2Instances ?? []).length}`,
    );
    const detected = await detectMo2Instances();

    const existingByPath = new Map<string, { name: string; path: string }>();
    for (const inst of settings.mo2Instances ?? []) {
      existingByPath.set(inst.path.toLowerCase(), inst);
    }
    let changed = false;

    for (const inst of detected) {
      const key = inst.path.toLowerCase();
      if (!existingByPath.has(key)) {
        existingByPath.set(key, inst);
        changed = true;
      }
    }

    const mergedInstances = Array.from(existingByPath.values());

    if (changed) {
      const nextSettings: Settings = {
        ...settings,
        mo2Instances: mergedInstances,
      };
      await persistSettings(nextSettings);
    }

    return mergedInstances;
  });

  ipcMain.handle("mo2:detectRegistry", async () => {
    const settings = await ensureSettings();
    const detected = await detectMo2InstancesFromRegistry();

    const existingByPath = new Map<string, { name: string; path: string }>();
    for (const inst of settings.mo2Instances ?? []) {
      existingByPath.set(inst.path.toLowerCase(), inst);
    }
    let changed = false;

    for (const inst of detected) {
      const key = inst.path.toLowerCase();
      if (!existingByPath.has(key)) {
        existingByPath.set(key, inst);
        changed = true;
      }
    }

    const mergedInstances = Array.from(existingByPath.values());

    if (changed) {
      const nextSettings: Settings = {
        ...settings,
        mo2Instances: mergedInstances,
      };
      await persistSettings(nextSettings);
    }

    return mergedInstances;
  });

  ipcMain.handle("mo2:detectFilesystem", async () => {
    const settings = await ensureSettings();
    const detected = await detectMo2InstancesFromFilesystem();

    const existingByPath = new Map<string, { name: string; path: string }>();
    for (const inst of settings.mo2Instances ?? []) {
      existingByPath.set(inst.path.toLowerCase(), inst);
    }
    let changed = false;

    for (const inst of detected) {
      const key = inst.path.toLowerCase();
      if (!existingByPath.has(key)) {
        existingByPath.set(key, inst);
        changed = true;
      }
    }

    const mergedInstances = Array.from(existingByPath.values());

    if (changed) {
      const nextSettings: Settings = {
        ...settings,
        mo2Instances: mergedInstances,
      };
      await persistSettings(nextSettings);
    }

    return mergedInstances;
  });

  ipcMain.handle("mo2:getEnvInstance", () => {
    const envPath = process.env.SKYRIM_AI_MO2_INSTANCE ?? process.env.MO2_INSTANCE_PATH;
    return envPath ?? null;
  });

  ipcMain.handle("mo2:getSearchInfo", () => getMo2SearchInfo());

  ipcMain.handle("mo2:browse", async () => {
    const result = await dialog.showOpenDialog({
      title: "Select Mod Organizer 2 instance folder",
      properties: ["openDirectory"],
    });

    if (result.canceled || result.filePaths.length === 0) {
      return null;
    }

    const chosenPath = result.filePaths[0];

    try {
      // Accept either a folder that directly contains ModOrganizer.exe (portable install)
      // or an MO2 instance-style folder that contains profiles/ and mods/ subdirectories.
      const exePath = path.join(chosenPath, "ModOrganizer.exe");
      const profilesDir = path.join(chosenPath, "profiles");
      const modsDir = path.join(chosenPath, "mods");

      let looksValid = false;

      try {
        const exeStats = await fs.stat(exePath);
        if (exeStats.isFile()) {
          looksValid = true;
        }
      } catch {
        // ignore, we'll try instance-style layout below
      }

      if (!looksValid) {
        try {
          const [profilesStats, modsStats] = await Promise.all([
            fs.stat(profilesDir),
            fs.stat(modsDir),
          ]);
          if (profilesStats.isDirectory() && modsStats.isDirectory()) {
            looksValid = true;
          }
        } catch {
          // still not valid
        }
      }

      if (!looksValid) return null;
    } catch {
      return null;
    }

    return {
      name: path.basename(chosenPath),
      path: chosenPath,
    };
  });

  ipcMain.handle("mo2:listProfiles", async (_event, instancePath: string) =>
    getProfiles(instancePath),
  );

  ipcMain.handle(
    "analysis:runOffline",
    async (
      _event,
      payload: { instancePath: string; profileId: string; options?: AnalysisRunOptions },
    ) => {
      const settings = await ensureSettings();
      await logger.info("Offline analysis requested via IPC");
      await logger.debug(
        `[IPC] scanProfile (offline) for instance="${payload.instancePath}", profile="${payload.profileId}"`,
      );
      const snapshot = await scanProfile(payload.instancePath, payload.profileId);
      await logger.debug(
        `[IPC] scanProfile (offline) complete: mods=${snapshot.mods.length}, ` +
          `plugins=${snapshot.pluginLoadOrder.length}`,
      );
      return runOfflineAnalysis(snapshot, settings, payload.options);
    },
  );

  ipcMain.handle(
    "analysis:runAgentic",
    async (
      _event,
      payload: { instancePath: string; profileId: string; options?: AnalysisRunOptions },
    ) => {
      const settings = await ensureSettings();
      await logger.info("Agentic analysis requested via IPC");
      await logger.debug(
        `[IPC] scanProfile (agentic) for instance="${payload.instancePath}", profile="${payload.profileId}"`,
      );
      const snapshot = await scanProfile(payload.instancePath, payload.profileId);
      await logger.debug(
        `[IPC] scanProfile (agentic) complete: mods=${snapshot.mods.length}, ` +
          `plugins=${snapshot.pluginLoadOrder.length}`,
      );
      const offline = await runOfflineAnalysis(snapshot, settings, payload.options);
      try {
        return await runAgenticAnalysis(snapshot, settings, offline, payload.options);
      } catch (error) {
        if (isOpenAIConfigError(error)) {
          const message =
            "OpenAI API key missing; set OPENAI_API_KEY in the environment to enable agentic analysis.";
          await logger.error(message);
          throw new Error(message);
        }
        await logger.error(`Agentic analysis failed: ${(error as Error).message ?? String(error)}`);
        throw error;
      }
    },
  );

  ipcMain.handle(
    "analysis:expandIssue",
    async (
      _event,
      payload: {
        issue: Issue;
        profile: ProfileSnapshot;
        messages?: { role: "user" | "assistant"; content: string }[];
      },
    ): Promise<string> => {
      const settings = await ensureSettings();
      try {
        return await expandIssueExplanation({
          issue: payload.issue,
          profile: payload.profile,
          settings,
          messages: payload.messages,
        });
      } catch (error) {
        if (isOpenAIConfigError(error)) {
          const message =
            "OpenAI API key missing; set OPENAI_API_KEY in the environment to enable AI issue expansion.";
          await logger.error(message);
          throw new Error(message);
        }
        await logger.error(`Issue expansion failed: ${(error as Error).message ?? String(error)}`);
        throw new Error("Issue expansion failed; see logs for details.");
      }
    },
  );

  ipcMain.handle(
    "analysis:export",
    async (
      _event,
      payload: { analysis: AnalysisResult; filePath: string; format: "json" | "html" },
    ) => {
      await logger.info(`Exporting analysis as ${payload.format.toUpperCase()}`);
      return exportAnalysis(payload.analysis, payload.format, payload.filePath);
    },
  );

  ipcMain.handle("logs:tail", async (_event, limit = 200) => {
    try {
      const logPath = getLogFilePath();
      const contents = await fs.readFile(logPath, "utf-8");
      return contents.split(/\r?\n/).slice(-limit);
    } catch {
      return [];
    }
  });
};
