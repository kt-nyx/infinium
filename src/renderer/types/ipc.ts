import type { AnalysisRunOptions } from "../../shared/analysis";
import type { AnalysisResult, Issue, ProfileSnapshot, Settings } from "../../shared/types";

type IssueChatMessage = {
  role: "user" | "assistant";
  content: string;
};

export interface RendererApi {
  settings: {
    get: () => Promise<Settings>;
    save: (payload: Settings) => Promise<Settings>;
  };
  mo2: {
    detect: () => Promise<{ name: string; path: string }[]>;
    listProfiles: (instancePath: string) => Promise<string[]>;
    getEnvInstance: () => Promise<string | null>;
    detectRegistry: () => Promise<{ name: string; path: string }[]>;
    detectFilesystem: () => Promise<{ name: string; path: string }[]>;
    getSearchInfo: () => Promise<{
      envCandidates: string[];
      commonPaths: string[];
      registryKeys: string[];
      filesystemRoots: string[];
    }>;
    browse: () => Promise<{ name: string; path: string } | null>;
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
    expandIssue: (
      issue: Issue,
      profile: ProfileSnapshot,
      messages?: IssueChatMessage[],
    ) => Promise<string>;
  };
  logs: {
    tail: (limit?: number) => Promise<string[]>;
  };
}
