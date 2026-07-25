import type { Issue, ModInfo, ProfileSnapshot, Recommendation, Settings } from "../../../shared/types";
import type { VfsIndex } from "../../mo2/vfsIndex";

export interface AgentFlags {
  useLoot: boolean;
  useNexus: boolean;
  useRag: boolean;
  complexity: number;
  opinionatedness: number;
}

export interface OrchestratorInput {
  profile: ProfileSnapshot;
  offlineIssues: Issue[];
  offlineRecommendations: Recommendation[];
  settings: Settings;
  vfs?: VfsIndex;
  flags: AgentFlags;
}

export interface OrchestratorBudgets {
  maxDigests: number;
  maxCandidates: number;
  maxInvestigations: number;
  maxToolCalls: number;
  maxModelCalls: number;
  maxTextCharsPerDigest: number;
}

export interface OrchestratorRunContext {
  runId: string;
  startedAt: string;
  flags: AgentFlags;
  budgets: OrchestratorBudgets;
  counters: {
    toolCalls: number;
    modelCalls: number;
  };
}

export interface SignalIndex {
  pluginToModIds: Record<string, string[]>;
  baseline: {
    issueCount: number;
    recommendationCount: number;
    categories: Record<string, number>;
    affectedModIds: string[];
    affectedPlugins: string[];
  };
  interestingMods: Array<{
    id: string;
    name: string;
    score: number;
    reasons: string[];
  }>;
}

export interface PerModDigestFacet {
  kind: string;
  value: string;
  confidence: "high" | "medium" | "low";
  evidence: string[];
}

export interface PerModDigestV2 {
  schemaVersion: number;
  modId: string;
  modName: string;
  enabled: boolean;
  plugins: string[];
  nexusId?: number;
  categoryGroup?: string;
  scopeHint?: string;
  importanceBucket?: string;
  stale?: boolean;
  systemsAffected: string[];
  facets: PerModDigestFacet[];
  supportLinks: Array<{ kind: string; url: string; label?: string }>;
  evidenceSnippets: string[];
  // Copy-through AI-enriched signals (already bounded elsewhere).
  requirementsAgent?: ModInfo["requirementsAgent"];
  loadOrderRulesAgent?: ModInfo["loadOrderRulesAgent"];
  variantAgent?: ModInfo["variantAgent"];
  scriptPerfRiskAgent?: ModInfo["scriptPerfRiskAgent"];
  redundancyCandidatesAgent?: ModInfo["redundancyCandidatesAgent"];
  // Optional Nexus deep-dive enrichment (bounded).
  nexusFilesPreview?: unknown;
  nexusFileContentsSummary?: unknown;
}

export type InvestigationPlanStep =
  | { tool: "get_nexus_mod_files"; args: { nexusId: number } }
  | { tool: "get_nexus_mod_file_contents_summary"; args: { nexusId: number; maxEntries?: number } }
  | { tool: "search_mod_docs"; args: { query: string; k?: number } };

export interface IssueCandidateScore {
  severity: number; // 0..1
  confidence: number; // 0..1
  novelty: number; // 0..1
  total: number; // 0..1
  reasons: string[];
}

export interface IssueCandidate {
  id: string;
  kind: string;
  hypothesis: string;
  systemsAffected: string[];
  affectedModIds: string[];
  affectedPlugins: string[];
  evidenceRefs: Array<{ source: string; modId?: string; url?: string; snippet: string }>;
  investigationPlan: InvestigationPlanStep[];
  score: IssueCandidateScore;
}

export const budgetsForFlags = (flags: AgentFlags): OrchestratorBudgets => {
  // Conservative defaults; later stages can tune these thresholds.
  const complexity = Math.max(0, Math.min(10, flags.complexity));

  const maxDigests = complexity >= 5 ? 120 : complexity >= 4 ? 80 : complexity >= 3 ? 50 : 30;
  const maxCandidates = complexity >= 5 ? 250 : complexity >= 4 ? 160 : complexity >= 3 ? 120 : 80;
  const maxInvestigations = complexity >= 5 ? 12 : complexity >= 4 ? 8 : complexity >= 3 ? 5 : 3;
  const maxToolCalls = complexity >= 5 ? 45 : complexity >= 4 ? 28 : complexity >= 3 ? 18 : 10;
  const maxModelCalls = complexity >= 5 ? 12 : complexity >= 4 ? 8 : complexity >= 3 ? 6 : 4;
  const maxTextCharsPerDigest = complexity >= 5 ? 8000 : complexity >= 4 ? 6000 : 4500;

  return {
    maxDigests,
    maxCandidates,
    maxInvestigations,
    maxToolCalls,
    maxModelCalls,
    maxTextCharsPerDigest,
  };
};


