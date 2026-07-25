import { promises as fs } from "node:fs";
import path from "node:path";
import { logger } from "../../logging";
import type { Issue, Recommendation } from "../../../shared/types";
import type { IssueCandidate, OrchestratorInput, OrchestratorRunContext } from "./types";
import { ensureDir, resolveTraceDir } from "./storage";

const truncate = (text: string, max: number): string =>
  text.length <= max ? text : `${text.slice(0, max)}…`;

const safeJson = (value: unknown): string => {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return JSON.stringify({ error: "failed_to_stringify" }, null, 2);
  }
};

const sanitizeCandidates = (candidates: IssueCandidate[]): unknown[] =>
  candidates.map((c) => ({
    id: c.id,
    kind: c.kind,
    hypothesis: c.hypothesis,
    systemsAffected: c.systemsAffected.slice(0, 30),
    affectedModIds: c.affectedModIds.slice(0, 100),
    affectedPlugins: c.affectedPlugins.slice(0, 150),
    investigationPlan: c.investigationPlan.slice(0, 20),
    score: c.score,
    evidenceRefs: c.evidenceRefs.slice(0, 12).map((e) => ({
      source: e.source,
      modId: e.modId,
      url: e.url,
      snippet: truncate(e.snippet, 500),
    })),
  }));

const sanitizeIssues = (issues: Issue[]): unknown[] =>
  issues.map((i) => ({
    id: i.id,
    severity: i.severity,
    category: i.category,
    categoryNormalized: i.categoryNormalized,
    summary: i.summary,
    confidence: i.confidence,
    risky: i.risky,
    affectedMods: (i.affectedMods ?? []).slice(0, 120),
    affectedPlugins: (i.affectedPlugins ?? []).slice(0, 200),
    source: i.source,
  }));

const sanitizeRecommendations = (recs: Recommendation[]): unknown[] =>
  recs.map((r) => ({
    issueId: r.issueId,
    steps: (r.steps ?? []).slice(0, 12).map((s) => truncate(s, 500)),
    notes: r.notes ? truncate(r.notes, 1000) : undefined,
  }));

const sanitizeVfsSummary = (input: OrchestratorInput): unknown | undefined => {
  const vfs = input.vfs;
  if (!vfs) return undefined;

  const topEdges = (() => {
    const out: Array<{ winner: string; loser: string; total: number; byCategory?: unknown; samples?: unknown }> = [];
    Object.entries(vfs.edgeCounts ?? {}).forEach(([winner, losers]) => {
      Object.entries(losers ?? {}).forEach(([loser, counts]) => {
        out.push({
          winner,
          loser,
          total: counts.total ?? 0,
          byCategory: counts.byCategory ?? {},
          samples: (vfs.edgeSamples?.[winner]?.[loser] ?? []).slice(0, 6),
        });
      });
    });
    return out.sort((a, b) => b.total - a.total).slice(0, 20);
  })();

  return {
    schemaVersion: vfs.schemaVersion,
    taxonomyVersion: vfs.taxonomyVersion,
    scope: vfs.scope,
    categoriesScanned: (vfs.categoriesScanned ?? []).slice(0, 50),
    categoryStats: vfs.categoryStats,
    overwriteSummary: vfs.overwriteSummary,
    coverage: vfs.coverage,
    topEdges,
  };
};

export const writeTraceArtifact = async (params: {
  input: OrchestratorInput;
  ctx: OrchestratorRunContext;
  stageSummary: unknown;
  candidates: IssueCandidate[];
  selectedCandidateIds: string[];
  investigatedCandidates: IssueCandidate[];
  toolCalls: unknown[];
  issueMappings: Array<{ candidateId: string; issueId: string; finalIssueId: string }>;
  finalIssues: Issue[];
  finalRecommendations: Recommendation[];
}): Promise<{ analysisTraceId: string; traceFilePath: string }> => {
  const dir = path.join(resolveTraceDir(), "orchestrator");
  await ensureDir(dir);

  const analysisTraceId = `${params.ctx.runId}.json`;
  const traceFilePath = path.join(dir, analysisTraceId);

  const payload = {
    kind: "infinium_orchestrator_trace_v1",
    runId: params.ctx.runId,
    profileId: params.input.profile.profileId,
    startedAt: params.ctx.startedAt,
    modelId: process.env.OPENAI_MODEL ?? "unknown-model",
    flags: params.input.flags,
    budgets: params.ctx.budgets,
    counters: params.ctx.counters,
    vfsSummary: sanitizeVfsSummary(params.input),
    stageSummary: params.stageSummary,
    candidates: sanitizeCandidates(params.candidates),
    selectedCandidateIds: params.selectedCandidateIds,
    investigatedCandidates: sanitizeCandidates(params.investigatedCandidates),
    toolCalls: params.toolCalls,
    issueMappings: params.issueMappings,
    finalIssues: sanitizeIssues(params.finalIssues),
    finalRecommendations: sanitizeRecommendations(params.finalRecommendations),
  };

  const json = safeJson(payload);
  await fs.writeFile(traceFilePath, json, "utf-8");

  await logger.info(
    `[Orchestrator][Trace] wrote traceId=${analysisTraceId} bytes=${json.length} path=${traceFilePath}`,
  );

  return { analysisTraceId, traceFilePath };
};




