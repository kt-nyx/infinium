import { randomUUID } from "node:crypto";
import { logger } from "../../logging";
import type { Issue, Recommendation } from "../../../shared/types";
import type { OrchestratorInput, OrchestratorRunContext } from "./types";
import { budgetsForFlags } from "./types";
import { stage0SeedSignals } from "./stage0SeedSignals";
import { stage1ModDigestMap } from "./stage1ModDigest";
import { stage2ReduceToCandidates } from "./stage2ReduceToCandidates";
import { stage3InvestigateTopK } from "./stage3InvestigateTopK";
import { stage4AssembleReport } from "./stage4AssembleReport";
import { writeTraceArtifact } from "./traceArtifact";

const nowIso = (): string => new Date().toISOString();

const makeRunContext = (input: OrchestratorInput): OrchestratorRunContext => {
  return {
    runId: `run-${randomUUID()}`,
    startedAt: nowIso(),
    flags: input.flags,
    budgets: budgetsForFlags(input.flags),
    counters: { toolCalls: 0, modelCalls: 0 },
  };
};

export const runOrchestratedSkyrimAnalysis = async (
  input: OrchestratorInput,
): Promise<{ issues: Issue[]; recommendations: Recommendation[]; analysisTraceId?: string }> => {
  const ctx = makeRunContext(input);

  await logger.info(
    `[Orchestrator] start runId=${ctx.runId} profile=${input.profile.profileId} ` +
      `mods=${input.profile.mods.length} plugins=${input.profile.pluginLoadOrder.length} ` +
      `flags=${JSON.stringify(input.flags)} budgets=${JSON.stringify(ctx.budgets)}`,
  );

  const stageSummary: Record<
    string,
    { startedAt: string; durationMs: number; counts?: Record<string, number> }
  > = {};

  const stage0Started = Date.now();
  const signalIndex = await stage0SeedSignals(input, ctx);
  stageSummary.Stage0_SeedSignals = {
    startedAt: new Date(stage0Started).toISOString(),
    durationMs: Date.now() - stage0Started,
    counts: {
      pluginToModKeys: Object.keys(signalIndex.pluginToModIds).length,
      baselineIssues: signalIndex.baseline.issueCount,
      baselineRecs: signalIndex.baseline.recommendationCount,
      interestingMods: signalIndex.interestingMods.length,
    },
  };
  await logger.info(
    `[Orchestrator] Stage0 complete runId=${ctx.runId} ` +
      `pluginToModKeys=${Object.keys(signalIndex.pluginToModIds).length} ` +
      `baselineIssues=${signalIndex.baseline.issueCount} baselineRecs=${signalIndex.baseline.recommendationCount} ` +
      `interestingMods=${signalIndex.interestingMods.length} ` +
      `durationMs=${Date.now() - stage0Started}`,
  );

  const stage1Started = Date.now();
  const stage1 = await stage1ModDigestMap(input, ctx, signalIndex);
  stageSummary.Stage1_ModDigest_Map = {
    startedAt: new Date(stage1Started).toISOString(),
    durationMs: Date.now() - stage1Started,
    counts: {
      digests: stage1.digests.length,
      cacheHits: stage1.cacheHits,
      cacheMisses: stage1.cacheMisses,
      toolCalls: ctx.counters.toolCalls,
    },
  };
  await logger.info(
    `[Orchestrator] Stage1 complete runId=${ctx.runId} digests=${stage1.digests.length} ` +
      `cacheHits=${stage1.cacheHits} cacheMisses=${stage1.cacheMisses} durationMs=${Date.now() - stage1Started}`,
  );

  const stage2Started = Date.now();
  const stage2 = await stage2ReduceToCandidates(input, ctx, signalIndex, stage1.digests);
  stageSummary.Stage2_ReduceToCandidates = {
    startedAt: new Date(stage2Started).toISOString(),
    durationMs: Date.now() - stage2Started,
    counts: {
      candidates: stage2.candidates.length,
      clustersBuilt: stage2.clustersBuilt,
    },
  };
  await logger.info(
    `[Orchestrator] Stage2 complete runId=${ctx.runId} candidates=${stage2.candidates.length} ` +
      `clustersBuilt=${stage2.clustersBuilt} durationMs=${Date.now() - stage2Started}`,
  );

  const stage3Started = Date.now();
  const stage3 = await stage3InvestigateTopK(input, ctx, stage2.candidates);
  stageSummary.Stage3_InvestigateTopK = {
    startedAt: new Date(stage3Started).toISOString(),
    durationMs: Date.now() - stage3Started,
    counts: {
      investigated: stage3.investigated.length,
      issues: stage3.issues.length,
      recommendations: stage3.recommendations.length,
      toolCalls: stage3.toolCalls.length,
    },
  };
  await logger.info(
    `[Orchestrator] Stage3 complete runId=${ctx.runId} issues=${stage3.issues.length} ` +
      `recs=${stage3.recommendations.length} toolCalls=${stage3.toolCalls.length} durationMs=${Date.now() - stage3Started}`,
  );

  const stage4Started = Date.now();
  const stage4 = await stage4AssembleReport(input, {
    issues: stage3.issues,
    recommendations: stage3.recommendations,
  });
  stageSummary.Stage4_AssembleReport = {
    startedAt: new Date(stage4Started).toISOString(),
    durationMs: Date.now() - stage4Started,
    counts: {
      totalIssues: stage4.issues.length,
      totalRecs: stage4.recommendations.length,
      mergedAgentIssues: stage4.mergedAgentIssues,
    },
  };
  await logger.info(
    `[Orchestrator] Stage4 complete runId=${ctx.runId} totalIssues=${stage4.issues.length} ` +
      `totalRecs=${stage4.recommendations.length} mergedAgentIssues=${stage4.mergedAgentIssues} durationMs=${Date.now() - stage4Started}`,
  );

  const issueMappings = stage3.issueMappings.map((m) => ({
    candidateId: m.candidateId,
    issueId: m.issueId,
    finalIssueId: stage4.issueIdRemap[m.issueId] ?? m.issueId,
  }));

  let analysisTraceId: string | undefined;
  try {
    const trace = await writeTraceArtifact({
      input,
      ctx,
      stageSummary,
      candidates: stage2.candidates,
      selectedCandidateIds: stage3.selectedCandidateIds,
      investigatedCandidates: stage3.investigated,
      toolCalls: stage3.toolCalls,
      issueMappings,
      finalIssues: stage4.issues,
      finalRecommendations: stage4.recommendations,
    });
    analysisTraceId = trace.analysisTraceId;
  } catch (e) {
    await logger.warn(
      `[Orchestrator][Trace] Failed to write trace for runId=${ctx.runId}: ${(e as Error).message ?? String(e)}`,
    );
  }

  await logger.info(
    `[Orchestrator] completed runId=${ctx.runId} toolCalls=${ctx.counters.toolCalls} modelCalls=${ctx.counters.modelCalls} ` +
      `stageSummary=${JSON.stringify(stageSummary)}`,
  );
  return { issues: stage4.issues, recommendations: stage4.recommendations, analysisTraceId };
};


