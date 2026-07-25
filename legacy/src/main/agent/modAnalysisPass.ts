import { z } from "zod";
import type { ModInfo, ProfileSnapshot, Settings } from "../../shared/types";
import { logger } from "../logging";
import { NexusClient } from "../nexus/nexusClient";
import { createSkyrimChatModel } from "./openaiClient";

const overlapTagSchema = z
  .string()
  .regex(
    /^(system:(ai|combat|perks|survival|animations|skeleton|loot_balance|followers)|ui:(hud|icons|map|menu)|visual:(lighting|weather)|location:[a-z0-9_]{3,})$/,
  );

const confidenceSchema = z.enum(["high", "medium", "low"]);

const requirementSchema = z.object({
  kind: z.enum(["required", "soft", "patch"]),
  targetModId: z.string().optional(),
  targetModName: z.string().optional(),
  targetPlugin: z.string().optional(),
  evidence: z.string(),
  confidence: confidenceSchema,
});

const loadOrderRuleSchema = z.object({
  relation: z.enum(["before", "after"]),
  targetModId: z.string().optional(),
  targetModName: z.string().optional(),
  targetPlugin: z.string().optional(),
  evidence: z.string(),
  confidence: confidenceSchema,
});

const variantSchema = z.object({
  expected: z.string(),
  detected: z.string(),
  mismatch: z.boolean(),
  evidence: z.string(),
  confidence: confidenceSchema,
});

const scriptPerfSchema = z.object({
  level: z.enum(["low", "medium", "high"]),
  reasons: z.array(z.string()).default([]),
  confidence: confidenceSchema,
  fileSummaryUsed: z.boolean().optional(),
});

const redundancySchema = z.object({
  otherModId: z.string(),
  rationale: z.string(),
  confidence: confidenceSchema,
});

const modAnalysisSchema = z.object({
  id: z.string(),
  overlapTagsAgent: z.array(overlapTagSchema).default([]),
  requirementsAgent: z.array(requirementSchema).nullish(),
  loadOrderRulesAgent: z.array(loadOrderRuleSchema).nullish(),
  variantAgent: variantSchema.nullish(),
  scriptPerfRiskAgent: scriptPerfSchema.nullish(),
  redundancyCandidatesAgent: z.array(redundancySchema).nullish(),
});

const responseSchema = z.object({
  mods: z.array(modAnalysisSchema),
});

const truncate = (text: string, max: number): string =>
  text.length <= max ? text : `${text.slice(0, max)}\n...[truncated]`;

const getNexusDescription = (mod: ModInfo): string => {
  const bucket = mod.metadata?.["nexus"] as Record<string, unknown> | undefined;
  const raw = bucket?.["description"];
  return typeof raw === "string" ? raw : "";
};

const shouldAnalyzeModAtComplexity = (mod: ModInfo, complexity: number): boolean => {
  if (!mod.enabled) return false;
  if (complexity <= 1) return false;

  const hasPlugins = Array.isArray(mod.plugins) && mod.plugins.length > 0;
  const hasDesc = Boolean(getNexusDescription(mod).trim());

  if (complexity >= 5) {
    return hasDesc || hasPlugins;
  }

  if (complexity >= 4) {
    return hasDesc || hasPlugins;
  }

  if (complexity >= 3) {
    return hasPlugins || (hasDesc && mod.categoryGroup !== "assets_like");
  }

  // Complexity 2: keep this focused to reduce cost.
  return (
    hasDesc &&
    (mod.categoryGroup === "framework_like" ||
      mod.categoryGroup === "overhaul_like" ||
      mod.categoryGroup === "content_like" ||
      mod.categoryGroup === "ui_like" ||
      mod.stale === true)
  );
};

const mapLimit = async <T, R>(
  items: T[],
  limit: number,
  mapper: (item: T) => Promise<R>,
): Promise<R[]> => {
  const results: R[] = new Array(items.length);
  let next = 0;

  const runners = new Array(Math.max(1, limit)).fill(0).map(async () => {
    while (true) {
      const idx = next;
      next += 1;
      if (idx >= items.length) return;
      results[idx] = await mapper(items[idx]);
    }
  });

  await Promise.all(runners);
  return results;
};

export const runAiModAnalysisPass = async (params: {
  profile: ProfileSnapshot;
  settings: Settings;
  complexity: number;
}): Promise<ProfileSnapshot> => {
  const { profile, settings, complexity } = params;

  const candidates = profile.mods.filter((m) => shouldAnalyzeModAtComplexity(m, complexity));
  if (!candidates.length) {
    await logger.debug(`[ModAnalysisPass] No candidate mods for profile=${profile.profileId}.`);
    return profile;
  }

  // Keep batch sizes small enough that the model has room to return valid JSON.
  const batchSize = complexity >= 5 ? 8 : complexity >= 4 ? 12 : 15;
  await logger.info(
    `[ModAnalysisPass] Starting AI mod analysis for profile=${profile.profileId}: ` +
      `candidates=${candidates.length}, batchSize=${batchSize}, complexity=${complexity}`,
  );

  // Pre-fetch Nexus file metadata at higher complexity.
  const nexus = settings.nexusApiKey ? new NexusClient(settings, profile.game) : null;

  const shouldFetchFiles = complexity >= 4 && nexus != null;
  const shouldFetchContents = complexity >= 5 && nexus != null;

  const fileInfoById = new Map<string, unknown>();
  const contentsById = new Map<string, unknown>();

  if (shouldFetchFiles || shouldFetchContents) {
    const modsWithIds = candidates.filter((m) => typeof m.nexusId === "number" && (m.nexusId as number) > 0);
    const concurrency = 3;

    await logger.info(
      `[ModAnalysisPass] Prefetching Nexus file metadata: mods=${modsWithIds.length}, ` +
        `files=${shouldFetchFiles}, contents=${shouldFetchContents}`,
    );

    await mapLimit(modsWithIds, concurrency, async (m) => {
      const nexusId = m.nexusId as number;
      try {
        if (shouldFetchFiles) {
          const files = await nexus!.getModFiles(nexusId);
          // Only include the most relevant subset to keep prompts small.
          fileInfoById.set(m.id, files.slice(0, 10));
        }
        if (shouldFetchContents) {
          const summary = await nexus!.getModFileContentsSummary({ nexusId, limit: 500 });
          if (summary) contentsById.set(m.id, summary);
        }
      } catch (e) {
        await logger.warn(
          `[ModAnalysisPass] Nexus prefetch failed for mod=${m.id}, nexusId=${nexusId}: ${
            (e as Error).message ?? String(e)
          }`,
        );
      }
    });

    await logger.debug(
      `[ModAnalysisPass] Prefetch complete: filesForMods=${fileInfoById.size}, contentsForMods=${contentsById.size}`,
    );
  }

  const model = createSkyrimChatModel({
    settings,
    complexity,
    // The mod analysis pass produces a fairly large JSON object; give it a
    // larger completion budget than the default global scaling.
    maxTokensOverride: complexity >= 5 ? 8000 : complexity >= 4 ? 5000 : 2500,
    modelKwargs: { response_format: { type: "json_object" } },
  });

  const profileModIndex = profile.mods.map((m) => ({ id: m.id, name: m.name }));

  const updates = new Map<string, Partial<ModInfo>>();

  for (let start = 0; start < candidates.length; start += batchSize) {
    const batch = candidates.slice(start, start + batchSize);

    const batchPayload = batch.map((m) => ({
      id: m.id,
      name: m.name,
      plugins: m.plugins ?? [],
      nexusCategory: m.nexusCategory,
      categoryGroup: m.categoryGroup,
      scopeHint: m.scopeHint,
      stale: m.stale ?? false,
      // Keep prompts bounded; the model can still infer a lot from the top part
      // of descriptions.
      description: truncate(getNexusDescription(m), 2000),
      // File metadata can be very verbose; include only at higher complexity.
      nexusFiles: complexity >= 5 ? fileInfoById.get(m.id) : undefined,
      fileContentsSummary: complexity >= 5 ? contentsById.get(m.id) : undefined,
    }));

    const system = [
      "You are extracting structured mod-analysis signals for a Skyrim SE/AE MO2 profile.",
      "",
      "Use the provided titles + Nexus descriptions as the primary evidence source.",
      "At higher complexity, you may also use included Nexus file metadata and file contents summaries if present.",
      "",
      "For each mod, produce EXACTLY these fields with EXACT field names and types:",
      "",
      "- id: string (copy from input)",
      "- overlapTagsAgent: string[] (only tag when clearly supported by text)",
      "- requirementsAgent: array of { kind: \"required\"|\"soft\"|\"patch\", targetModId?: string, targetModName?: string, targetPlugin?: string, evidence: string, confidence: \"high\"|\"medium\"|\"low\" } OR omit if none",
      "- loadOrderRulesAgent: array of { relation: \"before\"|\"after\", targetModId?: string, targetModName?: string, targetPlugin?: string, evidence: string, confidence: \"high\"|\"medium\"|\"low\" } OR omit if none",
      "- variantAgent: { expected: string, detected: string, mismatch: boolean, evidence: string, confidence: \"high\"|\"medium\"|\"low\" } OR omit if no variant issue",
      "- scriptPerfRiskAgent: { level: \"low\"|\"medium\"|\"high\", reasons: string[], confidence: \"high\"|\"medium\"|\"low\" } OR omit if N/A",
      "- redundancyCandidatesAgent: array of { otherModId: string, rationale: string, confidence: \"high\"|\"medium\"|\"low\" } OR omit/[] if none",
      "",
      "IMPORTANT: If a field doesn't apply (e.g., no variant issue), OMIT it entirely or set to null. Do NOT use different field names.",
      "",
      "Allowed overlap tags (only these):",
      "- system:ai | system:combat | system:perks | system:survival | system:animations | system:skeleton | system:loot_balance | system:followers",
      "- ui:hud | ui:icons | ui:map | ui:menu",
      "- visual:lighting | visual:weather",
      "- location:<snake_case_target> (ONLY for specific locations/interiors, derived from the title when it contains a clear target).",
      "",
      "Output JSON of shape: { \"mods\": [ { id, overlapTagsAgent, ... } ] }",
    ].join("\n");

    const user = [
      "Profile mod index (id -> name) for matching targetModId:",
      JSON.stringify(profileModIndex, null, 2),
      "",
      "Mods to analyze (JSON):",
      JSON.stringify(batchPayload, null, 2),
    ].join("\n");

    const started = Date.now();
    const resp = await model.invoke([{ role: "system", content: system }, { role: "user", content: user }]);
    const duration = Date.now() - started;

    let parsed: unknown;
    try {
      parsed = typeof resp.content === "string" ? JSON.parse(resp.content) : resp.content;
    } catch {
      await logger.warn(`[ModAnalysisPass] Failed to parse JSON for batch start=${start}; skipping batch.`);
      continue;
    }

    const validated = responseSchema.safeParse(parsed);
    if (!validated.success) {
      await logger.warn(
        `[ModAnalysisPass] Invalid response schema for batch start=${start}; skipping batch. error=${validated.error.message}`,
      );
      continue;
    }

    await logger.info(
      `[ModAnalysisPass] Completed batch ${start}-${Math.min(start + batchSize, candidates.length)} in ${duration}ms`,
    );
    await logger.debug(
      `[ModAnalysisPass] Batch output preview: ${truncate(JSON.stringify(validated.data), 2000)}`,
    );

    const batchIds = new Set(batch.map((m) => m.id));
    for (const m of validated.data.mods) {
      if (!batchIds.has(m.id)) continue;

      // Sanitize redundancy refs to known IDs.
      const redundancy =
        m.redundancyCandidatesAgent?.filter((r) => typeof r.otherModId === "string" && r.otherModId.length > 0) ??
        undefined;

      updates.set(m.id, {
        overlapTagsAgent: m.overlapTagsAgent?.length ? m.overlapTagsAgent : undefined,
        requirementsAgent: m.requirementsAgent?.length ? m.requirementsAgent : undefined,
        loadOrderRulesAgent: m.loadOrderRulesAgent?.length ? m.loadOrderRulesAgent : undefined,
        variantAgent: m.variantAgent ?? undefined,
        scriptPerfRiskAgent: m.scriptPerfRiskAgent ?? undefined,
        redundancyCandidatesAgent: redundancy?.length ? redundancy : undefined,
      });
    }
  }

  if (!updates.size) {
    await logger.warn(`[ModAnalysisPass] No usable mod analysis output for profile=${profile.profileId}.`);
    return profile;
  }

  const mergedMods = profile.mods.map((m) => {
    const patch = updates.get(m.id);
    if (!patch) return m;
    return { ...m, ...patch } as ModInfo;
  });

  await logger.info(
    `[ModAnalysisPass] Completed AI mod analysis for profile=${profile.profileId}: updatedMods=${updates.size}`,
  );

  return { ...profile, mods: mergedMods };
};


