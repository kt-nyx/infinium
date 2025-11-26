import { contextBridge, ipcRenderer } from "electron";
import type { AnalysisRunOptions } from "../shared/analysis";
import type { AnalysisResult, Settings } from "../shared/types";

const api = {
  settings: {
    get: (): Promise<Settings> => ipcRenderer.invoke("settings:get"),
    save: (payload: Settings): Promise<Settings> => ipcRenderer.invoke("settings:save", payload),
  },
  mo2: {
    detect: (): Promise<{ name: string; path: string }[]> => ipcRenderer.invoke("mo2:detect"),
    listProfiles: (instancePath: string): Promise<string[]> =>
      ipcRenderer.invoke("mo2:listProfiles", instancePath),
  },
  analysis: {
    runOffline: (
      instancePath: string,
      profileId: string,
      options?: AnalysisRunOptions,
    ): Promise<AnalysisResult> =>
      ipcRenderer.invoke("analysis:runOffline", { instancePath, profileId, options }),
    runAgentic: (
      instancePath: string,
      profileId: string,
      options?: AnalysisRunOptions,
    ): Promise<AnalysisResult> =>
      ipcRenderer.invoke("analysis:runAgentic", { instancePath, profileId, options }),
    export: (
      analysis: AnalysisResult,
      filePath: string,
      format: "json" | "html",
    ): Promise<string> => ipcRenderer.invoke("analysis:export", { analysis, filePath, format }),
    expandIssue: (issueId: string, summary: string): Promise<string> =>
      ipcRenderer.invoke("analysis:expandIssue", { issueId, summary }),
  },
  logs: {
    tail: (limit = 200): Promise<string[]> => ipcRenderer.invoke("logs:tail", limit),
  },
} as const;

contextBridge.exposeInMainWorld("api", api);

declare global {
  interface Window {
    api: typeof api;
  }
}
