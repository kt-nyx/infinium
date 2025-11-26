import type { AnalysisRunOptions } from "../../shared/analysis";
import type { AnalysisResult, Settings } from "../../shared/types";

export interface RendererApi {
  settings: {
    get: () => Promise<Settings>;
    save: (payload: Settings) => Promise<Settings>;
  };
  mo2: {
    detect: () => Promise<{ name: string; path: string }[]>;
    listProfiles: (instancePath: string) => Promise<string[]>;
  };
  analysis: {
    runOffline: (
      instancePath: string,
      profileId: string,
      options?: AnalysisRunOptions,
    ) => Promise<AnalysisResult>;
    runAgentic: (
      instancePath: string,
      profileId: string,
      options?: AnalysisRunOptions,
    ) => Promise<AnalysisResult>;
    export: (
      analysis: AnalysisResult,
      filePath: string,
      format: "json" | "html",
    ) => Promise<string>;
    expandIssue: (issueId: string, summary: string) => Promise<string>;
  };
  logs: {
    tail: (limit?: number) => Promise<string[]>;
  };
}
