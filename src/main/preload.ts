import { contextBridge, ipcRenderer } from "electron";
import type { AnalysisRunOptions } from "../shared/analysis";
import type { AnalysisResult, Issue, ProfileSnapshot, Settings } from "../shared/types";

type IssueChatMessage = {
  role: "user" | "assistant";
  content: string;
};

const api = {
  settings: {
    get: (): Promise<Settings> => ipcRenderer.invoke("settings:get"),
    save: (payload: Settings): Promise<Settings> => ipcRenderer.invoke("settings:save", payload),
  },
  mo2: {
    detect: (): Promise<{ name: string; path: string }[]> => ipcRenderer.invoke("mo2:detect"),
    listProfiles: (instancePath: string): Promise<string[]> =>
      ipcRenderer.invoke("mo2:listProfiles", instancePath),
    getEnvInstance: (): Promise<string | null> => ipcRenderer.invoke("mo2:getEnvInstance"),
    detectRegistry: (): Promise<{ name: string; path: string }[]> =>
      ipcRenderer.invoke("mo2:detectRegistry"),
    detectFilesystem: (): Promise<{ name: string; path: string }[]> =>
      ipcRenderer.invoke("mo2:detectFilesystem"),
    getSearchInfo: (): Promise<{
      envCandidates: string[];
      commonPaths: string[];
      registryKeys: string[];
      filesystemRoots: string[];
    }> => ipcRenderer.invoke("mo2:getSearchInfo"),
    browse: (): Promise<{ name: string; path: string } | null> => ipcRenderer.invoke("mo2:browse"),
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
    expandIssue: (
      issue: Issue,
      profile: ProfileSnapshot,
      messages?: IssueChatMessage[],
    ): Promise<string> => ipcRenderer.invoke("analysis:expandIssue", { issue, profile, messages }),
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
