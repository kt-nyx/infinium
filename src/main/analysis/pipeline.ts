import type { AnalysisRunOptions } from "../../shared/analysis";
import type { AnalysisResult, ProfileSnapshot, Settings } from "../../shared/types";
import { evaluate } from "../rules/rulesEngine";
import { runLootForProfile } from "../loot/lootManager";
import { logger, setLogLevel } from "../logging";
import { runSkyrimAgent } from "../agent/skyrimAgentGraph";

const resolveOptions = (settings: Settings, overrides?: AnalysisRunOptions) => ({
  ...settings.analysisDefaults,
  ...overrides,
});

export const runOfflineAnalysis = async (
  snapshot: ProfileSnapshot,
  settings: Settings,
  overrides?: AnalysisRunOptions,
): Promise<AnalysisResult> => {
  setLogLevel(settings.logLevel);
  const options = resolveOptions(settings, overrides);
  await logger.info(`Running offline analysis for ${snapshot.profileId}`);

  const lootReport = options.useLoot ? await runLootForProfile(snapshot) : undefined;
  const ruleResults = evaluate(snapshot, lootReport);

  return {
    profile: {
      ...snapshot,
      lootAvailable: Boolean(lootReport),
      nexusAvailable: Boolean(settings.nexusApiKey),
    },
    issues: ruleResults.issues,
    recommendations: ruleResults.recommendations,
    metadata: {
      offlineOnly: true,
      complexityLevel: options.complexity,
      opinionatedness: options.opinionatedness,
      agentUsed: false,
      createdAt: new Date().toISOString(),
    },
  };
};

export const runAgenticAnalysis = async (
  snapshot: ProfileSnapshot,
  settings: Settings,
  offlineBaseline: AnalysisResult,
  overrides?: AnalysisRunOptions,
): Promise<AnalysisResult> => {
  const options = resolveOptions(settings, overrides);
  await logger.info(`Running agentic analysis for ${snapshot.profileId}`);

  const agentOutput = await runSkyrimAgent({
    profile: snapshot,
    offlineIssues: offlineBaseline.issues,
    offlineRecommendations: offlineBaseline.recommendations,
    settings,
    flags: {
      useLoot: options.useLoot,
      useNexus: options.useNexus,
      useRag: options.useRag,
      complexity: options.complexity,
      opinionatedness: options.opinionatedness,
    },
  });

  const issueMap = new Map(offlineBaseline.issues.map((issue) => [issue.id, issue]));
  agentOutput.issues.forEach((issue) => {
    if (!issueMap.has(issue.id)) {
      issueMap.set(issue.id, issue);
    }
  });

  const recommendationMap = new Map(
    offlineBaseline.recommendations.map((rec) => [`${rec.issueId}-${rec.steps.join("|")}`, rec]),
  );
  agentOutput.recommendations.forEach((rec) => {
    const key = `${rec.issueId}-${rec.steps.join("|")}`;
    if (!recommendationMap.has(key)) {
      recommendationMap.set(key, rec);
    }
  });

  return {
    profile: {
      ...offlineBaseline.profile,
      lootAvailable: offlineBaseline.profile.lootAvailable || options.useLoot,
      nexusAvailable: Boolean(settings.nexusApiKey && options.useNexus),
    },
    issues: Array.from(issueMap.values()),
    recommendations: Array.from(recommendationMap.values()),
    metadata: {
      offlineOnly: false,
      complexityLevel: options.complexity,
      opinionatedness: options.opinionatedness,
      agentUsed: true,
      createdAt: new Date().toISOString(),
    },
  };
};
