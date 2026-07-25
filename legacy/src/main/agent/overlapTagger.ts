import { z } from "zod";
import type { ModInfo, ProfileSnapshot, Settings } from "../../shared/types";
import { logger } from "../logging";
import { createSkyrimChatModel } from "./openaiClient";

const overlapTagSchema = z
  .string()
  .regex(
    /^(system:(ai|combat|perks|survival|animations|skeleton|loot_balance|followers)|ui:(hud|icons|map|menu)|visual:(lighting|weather)|location:[a-z0-9_]{3,})$/,
  );

const responseSchema = z.object({
  tags: z.array(
    z.object({
      id: z.string(),
      overlapTags: z.array(overlapTagSchema).default([]),
    }),
  ),
});

const truncate = (text: string, max: number): string =>
  text.length <= max ? text : `${text.slice(0, max)}\n...[truncated]`;

const getNexusDescription = (mod: ModInfo): string | undefined => {
  const bucket = mod.metadata?.["nexus"] as Record<string, unknown> | undefined;
  const raw = bucket?.["description"];
  if (typeof raw === "string" && raw.trim()) return raw;
  return undefined;
};

const shouldTagModAtComplexity = (mod: ModInfo, complexity: number): boolean => {
  // Keep the LLM cost bounded at low complexity.
  if (complexity <= 1) return false;

  if (!mod.enabled) return false;

  // Prefer plugin-backed mods, plus anything with Nexus description available.
  const hasPlugins = Array.isArray(mod.plugins) && mod.plugins.length > 0;
  const hasNexusDesc = Boolean(getNexusDescription(mod));

  if (complexity >= 4) {
    // More exhaustive: tag anything that is enabled and has a description, plus plugin-backed mods.
    return hasNexusDesc || hasPlugins;
  }

  // Balanced: only tag mods with descriptions or mods in higher-impact buckets.
  if (!hasNexusDesc && !hasPlugins) return false;

  const likelyRelevantGroup =
    mod.categoryGroup === "framework_like" ||
    mod.categoryGroup === "overhaul_like" ||
    mod.categoryGroup === "content_like" ||
    mod.categoryGroup === "ui_like";

  return hasNexusDesc && likelyRelevantGroup;
};

export const inferOverlapTagsWithAgent = async (
  profile: ProfileSnapshot,
  settings: Settings,
  complexity: number,
): Promise<ProfileSnapshot> => {
  const candidates = profile.mods.filter((m) => shouldTagModAtComplexity(m, complexity));

  if (candidates.length === 0) {
    await logger.debug(`[OverlapTagger] No candidate mods to tag for profile=${profile.profileId}.`);
    return profile;
  }

  // Batch to avoid huge prompts and keep latency predictable.
  const batchSize = complexity >= 4 ? 30 : 20;
  const model = createSkyrimChatModel({
    settings,
    complexity,
    // We want strict JSON.
    modelKwargs: { response_format: { type: "json_object" } },
  });

  await logger.info(
    `[OverlapTagger] Tagging overlap targets via LLM for profile=${profile.profileId}: ` +
      `candidates=${candidates.length}, batchSize=${batchSize}`,
  );

  const byId = new Map<string, string[]>();

  for (let start = 0; start < candidates.length; start += batchSize) {
    const batch = candidates.slice(start, start + batchSize);

    const batchPayload = batch.map((m) => ({
      id: m.id,
      name: m.name,
      nexusCategory: m.nexusCategory,
      categoryGroup: m.categoryGroup,
      scopeHint: m.scopeHint,
      plugins: m.plugins ?? [],
      description: truncate(getNexusDescription(m) ?? "", 2500),
    }));

    const system = [
      "You are classifying mod overlap targets for a Skyrim SE/AE MO2 profile.",
      "You will be given a list of mods with IDs, titles, and (when available) Nexus descriptions.",
      "",
      "Your job: for EACH mod, output a small set of overlapTags (or [] if unsure).",
      "",
      "Only tag when the description/title clearly indicates the mod affects that domain/target.",
      "Avoid guessing. Avoid broad tags like 'locations' without a specific target.",
      "",
      "Allowed tags (only these):",
      "- system:ai | system:combat | system:perks | system:survival | system:animations | system:skeleton | system:loot_balance | system:followers",
      "- ui:hud | ui:icons | ui:map | ui:menu",
      "- visual:lighting | visual:weather",
      "- location:<snake_case_target> (ONLY if the mod clearly targets a specific location/interior; prefer deriving from the title when it names a place, e.g. 'Riften Aerin's House' -> location:riften_aerins_house)",
      "",
      "Important:",
      "- Do NOT infer overlap from the substring 'ai' inside other words. Use real meaning from the text.",
      "- Many mods are unrelated; returning [] is totally fine.",
      "",
      "Output JSON with shape: { \"tags\": [ { \"id\": string, \"overlapTags\": string[] } ] }",
    ].join("\n");

    const user = [
      "Mods to tag (JSON):",
      JSON.stringify(batchPayload, null, 2),
    ].join("\n");

    const started = Date.now();
    const resp = await model.invoke([{ role: "system", content: system }, { role: "user", content: user }]);
    const duration = Date.now() - started;

    let parsed: unknown;
    try {
      parsed = typeof resp.content === "string" ? JSON.parse(resp.content) : resp.content;
    } catch {
      await logger.warn(`[OverlapTagger] Failed to parse JSON for batch start=${start}; skipping batch.`);
      continue;
    }

    const validated = responseSchema.safeParse(parsed);
    if (!validated.success) {
      await logger.warn(
        `[OverlapTagger] Invalid response schema for batch start=${start}; skipping batch. ` +
          `error=${validated.error.message}`,
      );
      continue;
    }

    await logger.info(
      `[OverlapTagger] Tagged batch ${start}-${Math.min(start + batchSize, candidates.length)} in ${duration}ms`,
    );
    await logger.debug(
      `[OverlapTagger] Tagged batch output preview: ${truncate(JSON.stringify(validated.data), 1500)}`,
    );

    for (const entry of validated.data.tags) {
      if (!entry?.id) continue;
      byId.set(entry.id, Array.from(new Set(entry.overlapTags ?? [])));
    }
  }

  if (byId.size === 0) {
    await logger.warn(`[OverlapTagger] No overlap tags produced for profile=${profile.profileId}.`);
    return profile;
  }

  const mergedMods = profile.mods.map((m) => {
    const tags = byId.get(m.id);
    if (!tags) return m;
    return {
      ...m,
      overlapTagsAgent: tags.length ? tags : undefined,
    } as ModInfo;
  });

  await logger.info(
    `[OverlapTagger] Completed overlap tagging for profile=${profile.profileId}: taggedMods=${byId.size}`,
  );

  return { ...profile, mods: mergedMods };
};


