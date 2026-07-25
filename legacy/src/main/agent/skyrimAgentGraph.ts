import type { Issue, ProfileSnapshot, Recommendation, Settings } from "../../shared/types";
import { runOrchestratedSkyrimAnalysis } from "./orchestrator";

export interface AgentFlags {
  useLoot: boolean;
  useNexus: boolean;
  useRag: boolean;
  complexity: number;
  opinionatedness: number;
}

export interface AgentInput {
  profile: ProfileSnapshot;
  offlineIssues: Issue[];
  offlineRecommendations: Recommendation[];
  settings: Settings;
  flags: AgentFlags;
}

/**
 * Backwards-compatible wrapper: the old single-pass ReAct agent has been
 * replaced by an explicit Stage0–Stage4 orchestrator.
 */
export const runSkyrimAgent = async (
  input: AgentInput,
): Promise<{ issues: Issue[]; recommendations: Recommendation[] }> => {
  const out = await runOrchestratedSkyrimAnalysis(input);
  return { issues: out.issues, recommendations: out.recommendations };
};
