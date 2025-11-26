import { ipcMain } from "electron";
import { promises as fs } from "node:fs";
import path from "node:path";
import type { AnalysisResult, Settings } from "../shared/types";
import { loadSettings, saveSettings } from "./config";
import { detectLootPaths } from "./loot/lootManager";
import { detectMo2Instances } from "./mo2/mo2Detector";
import { getProfiles, scanProfile } from "./mo2/mo2Scanner";
import type { AnalysisRunOptions } from "../shared/analysis";
import { runAgenticAnalysis, runOfflineAnalysis } from "./analysis/pipeline";
import { logger, getLogFilePath } from "./logging";

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

const expandIssue = async (issueId: string, summary: string): Promise<string> => {
  // TODO: delegate to agent for natural-language expansion.
  return `Detailed explanation for ${issueId}: ${summary}`;
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

  ipcMain.handle("mo2:detect", async () => detectMo2Instances());

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
      const snapshot = await scanProfile(payload.instancePath, payload.profileId);
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
      const snapshot = await scanProfile(payload.instancePath, payload.profileId);
      const offline = await runOfflineAnalysis(snapshot, settings, payload.options);
      return runAgenticAnalysis(snapshot, settings, offline, payload.options);
    },
  );

  ipcMain.handle(
    "analysis:expandIssue",
    async (_event, payload: { issueId: string; summary: string }) =>
      expandIssue(payload.issueId, payload.summary),
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
