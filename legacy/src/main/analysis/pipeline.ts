import type { AnalysisRunOptions } from "../../shared/analysis";
import type { AnalysisResult, ProfileSnapshot, Settings } from "../../shared/types";
import { evaluate } from "../rules/rulesEngine";
import { runLootForProfile } from "../loot/lootManager";
import { logger, setLogLevel } from "../logging";
import { runOrchestratedSkyrimAnalysis } from "../agent/orchestrator";
import { runAiModAnalysisPass } from "../agent/modAnalysisPass";
import { enrichProfileWithNexus } from "../nexus/enrichMods";
import { buildVfsIndex } from "../mo2/vfsIndex";
import type { VfsScope } from "../mo2/vfsTaxonomy";

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

  const useNexus = options.useNexus && Boolean(settings.nexusApiKey);

  let profileForAnalysis: ProfileSnapshot = snapshot;
  let nexusUsed = false;
  let nexusError: string | undefined;

  if (useNexus) {
    const enrichment = await enrichProfileWithNexus(snapshot, settings);
    profileForAnalysis = enrichment.profile;
    nexusUsed = enrichment.used;
    nexusError = enrichment.error;
  }

  const lootReport = options.useLoot ? await runLootForProfile(profileForAnalysis) : undefined;
  const ruleResults = evaluate(profileForAnalysis, lootReport);

  return {
    profile: {
      ...profileForAnalysis,
      lootAvailable: Boolean(lootReport),
      // Nexus is considered available when configured, enabled for this run,
      // and at least one enrichment call succeeded without a fatal error.
      nexusAvailable: Boolean(settings.nexusApiKey && useNexus && nexusUsed && !nexusError),
    },
    issues: ruleResults.issues,
    recommendations: ruleResults.recommendations,
    metadata: {
      offlineOnly: true,
      complexityLevel: options.complexity,
      opinionatedness: options.opinionatedness,
      agentUsed: false,
      createdAt: new Date().toISOString(),
      nexusUsed,
      nexusError,
    },
  };
};

export const runAgenticAnalysis = async (
  snapshot: ProfileSnapshot,
  settings: Settings,
  offlineBaseline: AnalysisResult,
  overrides?: AnalysisRunOptions,
): Promise<AnalysisResult> => {
  setLogLevel(settings.logLevel);
  const options = resolveOptions(settings, overrides);
  await logger.info(`Running agentic analysis for ${snapshot.profileId}`);

  const useNexus = options.useNexus && Boolean(settings.nexusApiKey);

  let profileForAgent: ProfileSnapshot = snapshot;
  let nexusUsed = offlineBaseline.metadata.nexusUsed ?? false;
  let nexusError: string | undefined = offlineBaseline.metadata.nexusError;

  if (useNexus) {
    const enrichment = await enrichProfileWithNexus(snapshot, settings);
    profileForAgent = enrichment.profile;
    if (enrichment.used) {
      nexusUsed = true;
    }
    if (enrichment.error) {
      nexusError = enrichment.error;
    }
  }

  // Optional: AI mod analysis pass (overlap/redundancy, requirements/patches,
  // variant correctness, script/perf triage). Gated by complexity.
  if (options.complexity >= 2) {
    try {
      profileForAgent = await runAiModAnalysisPass({
        profile: profileForAgent,
        settings,
        complexity: options.complexity,
      });
    } catch (error) {
      await logger.warn(
        `[ModAnalysisPass] Failed; continuing without AI mod analysis: ${(error as Error).message ?? String(error)}`,
      );
    }
  }

  // Build deterministic VFS/conflict index for the profile (hotspots-first by default).
  const envScope = (process.env.SKYRIM_AI_VFS_SCOPE ?? "").toLowerCase().trim();
  const scope: VfsScope =
    envScope === "full" || envScope === "extended" || envScope === "hotspots"
      ? (envScope as VfsScope)
      : "hotspots";
  const maxFilesPerMod = process.env.SKYRIM_AI_VFS_MAX_FILES_PER_MOD
    ? Number(process.env.SKYRIM_AI_VFS_MAX_FILES_PER_MOD)
    : undefined;
  const maxTotalFiles = process.env.SKYRIM_AI_VFS_MAX_TOTAL_FILES
    ? Number(process.env.SKYRIM_AI_VFS_MAX_TOTAL_FILES)
    : undefined;
  const maxMs = process.env.SKYRIM_AI_VFS_MAX_MS ? Number(process.env.SKYRIM_AI_VFS_MAX_MS) : undefined;

  let vfsIndex: Awaited<ReturnType<typeof buildVfsIndex>> | undefined;
  try {
    vfsIndex = await buildVfsIndex({
      profile: profileForAgent,
      scope,
      maxFilesPerMod,
      maxTotalFiles,
      maxMs,
    });
  } catch (error) {
    await logger.warn(
      `[VFS] Failed to build VFS index; continuing without VFS signals: ${(error as Error).message ?? String(error)}`,
    );
    vfsIndex = undefined;
  }

  const agentOutput = await runOrchestratedSkyrimAnalysis({
    profile: profileForAgent,
    offlineIssues: offlineBaseline.issues,
    offlineRecommendations: offlineBaseline.recommendations,
    settings,
    vfs: vfsIndex,
    flags: {
      useLoot: options.useLoot,
      useNexus: options.useNexus,
      useRag: options.useRag,
      complexity: options.complexity,
      opinionatedness: options.opinionatedness,
    },
  });
  await logger.info(
    `Agentic analysis completed: agentIssues=${agentOutput.issues.length}, ` +
      `agentRecommendations=${agentOutput.recommendations.length}`,
  );

  return {
    profile: {
      // Prefer the enriched view of the profile (including Nexus-derived
      // metadata) while preserving any additional fields from the offline
      // baseline such as lootAvailable flags.
      ...offlineBaseline.profile,
      ...profileForAgent,
      lootAvailable: offlineBaseline.profile.lootAvailable || options.useLoot,
      nexusAvailable: Boolean(settings.nexusApiKey && options.useNexus && nexusUsed && !nexusError),
    },
    // The orchestrator performs Stage4 merge + dedupe against the offline baseline.
    issues: agentOutput.issues,
    recommendations: agentOutput.recommendations,
    metadata: {
      offlineOnly: false,
      complexityLevel: options.complexity,
      opinionatedness: options.opinionatedness,
      agentUsed: true,
      createdAt: new Date().toISOString(),
      analysisTraceId: agentOutput.analysisTraceId,
      nexusUsed,
      nexusError,
    },
  };
};
