import type { ModInfo, ProfileSnapshot, Settings } from "../../shared/types";
import { logger } from "../logging";
import {
  NexusClient,
  NexusConfigError,
  NexusError,
  NexusUnavailableError,
  type NexusModMetadata,
} from "./nexusClient";

type GameSupport = ModInfo["gameSupport"];

const mapDomainToGameSupport = (domain: string | undefined): GameSupport => {
  const normalized = (domain ?? "").toLowerCase();

  if (normalized === "skyrim") {
    return "SkyrimLE";
  }

  if (normalized === "skyrimspecialedition") {
    // Skyrim SE and AE share the same Nexus domain; treat as SE for support.
    return "SkyrimSE";
  }

  return "Unknown";
};

type CategoryHeuristics = {
  nexusCategory?: string;
  categoryGroup?: ModInfo["categoryGroup"];
  categoryAmbiguity?: ModInfo["categoryAmbiguity"];
  topics?: string[];
  overlapDomains?: string[];
  overlapTags?: string[];
};

const normalizeOverlapKey = (raw: string): string =>
  raw
    .toLowerCase()
    .trim()
    .replace(/['"]/g, "")
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "");

const escapeRegExp = (value: string): string => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

const hasWord = (haystackLower: string, word: string): boolean => {
  const w = escapeRegExp(word.toLowerCase());
  return new RegExp(`\\b${w}\\b`, "i").test(haystackLower);
};

const hasAnyWord = (haystackLower: string, words: string[]): boolean =>
  words.some((w) => hasWord(haystackLower, w));

const hasPhrase = (haystackLower: string, phraseLower: string): boolean =>
  haystackLower.includes(phraseLower.toLowerCase());

const hasAnyPhrase = (haystackLower: string, phrasesLower: string[]): boolean =>
  phrasesLower.some((p) => hasPhrase(haystackLower, p));

const extractLocationTargetFromName = (name: string): string | undefined => {
  // Prefer the part after the last " - " segment for mods that encode targets
  // in the title (e.g. "Snazzy Interiors - Riften Aerin's House").
  const parts = name.split(" - ").map((p) => p.trim()).filter(Boolean);
  if (parts.length < 2) return undefined;

  const candidate = parts[parts.length - 1];
  const lower = candidate.toLowerCase();

  // Skip obvious non-target suffixes.
  if (
    lower.includes("patch") ||
    lower.includes("patches") ||
    lower.includes("patch hub") ||
    lower.includes("patch collection") ||
    lower.includes("collection") ||
    lower.includes("settings loader")
  ) {
    return undefined;
  }

  // Heuristic: require some location-ish signal in the suffix.
  const locationCue =
    /\b(house|manor|keep|inn|hall|tower|cave|crypt|ruins|sanctuary|temple|camp|farm|shack|lodge|palace|college|outskirts|docks|bridge)\b/i.test(
      candidate,
    ) ||
    /\b(whiterun|solitude|riften|windhelm|dawnstar|morthal|falkreath|markarth|winterhold|riverwood|rorikstead|dragon bridge|karthwasten|ivarstead|shor's stone)\b/i.test(
      candidate,
    );

  return locationCue ? candidate : undefined;
};

const buildCategoryGroupAndTopics = (
  mod: ModInfo,
  metadata: NexusModMetadata,
): CategoryHeuristics => {
  const textParts: string[] = [
    mod.name,
    metadata.name,
    metadata.summary,
    metadata.description ?? "",
    metadata.category ?? "",
    ...(metadata.tags ?? []),
  ]
    .filter((part) => Boolean(part))
    .map((part) => String(part));

  const combined = textParts.join(" ").toLowerCase();

  const nexusCategory = metadata.category?.trim() || undefined;
  const normalizedCategory = (nexusCategory ?? "").toLowerCase();

  // Primary classification is the Nexus category field. This is the first
  // source of truth, but we allow some name/description cues to refine it.
  type CategoryGroupHint = {
    group?: ModInfo["categoryGroup"];
    ambiguity?: ModInfo["categoryAmbiguity"];
  };

  const byCategory: CategoryGroupHint = (() => {
    if (!normalizedCategory) return {};

    // Patch ecosystem
    if (normalizedCategory === "patches") return { group: "patch_like", ambiguity: "low" };

    // Explicit overhaul bucket
    if (normalizedCategory === "overhauls") return { group: "overhaul_like", ambiguity: "low" };

    // Utility-ish buckets
    if (normalizedCategory === "utilities") return { group: "utility_like", ambiguity: "medium" };
    if (normalizedCategory === "bug fixes") return { group: "utility_like", ambiguity: "medium" };
    if (normalizedCategory === "modders resources")
      return { group: "framework_like", ambiguity: "medium" };
    if (normalizedCategory === "vr") return { group: "utility_like", ambiguity: "high" };

    // UI buckets
    if (normalizedCategory === "user interface") return { group: "ui_like", ambiguity: "medium" };
    if (normalizedCategory === "presets - enb and reshade")
      return { group: "ui_like", ambiguity: "low" };

    // Content buckets (quests/locations/cities/followers/etc.)
    if (
      normalizedCategory === "quests and adventures" ||
      normalizedCategory === "dungeons" ||
      normalizedCategory === "locations - new" ||
      normalizedCategory === "locations - vanilla" ||
      normalizedCategory === "cities, towns, villages, and hamlets" ||
      normalizedCategory === "player homes" ||
      normalizedCategory === "guilds/factions" ||
      normalizedCategory === "followers & companions" ||
      normalizedCategory === "followers & companions - creatures" ||
      normalizedCategory === "creatures and mounts"
    ) {
      return { group: "content_like", ambiguity: "medium" };
    }

    // Mechanics/gameplay-ish buckets are extremely ambiguous on Nexus (ranging
    // from tiny tweaks to sweeping overhauls). Default to "other" and rely on
    // description/title cues to promote into overhaul/framework buckets.
    if (
      normalizedCategory === "gameplay" ||
      normalizedCategory === "combat" ||
      normalizedCategory === "skills and leveling" ||
      normalizedCategory === "magic - gameplay" ||
      normalizedCategory === "magic - spells & enchantments" ||
      normalizedCategory === "alchemy" ||
      normalizedCategory === "crafting" ||
      normalizedCategory === "races, classes, and birthsigns" ||
      normalizedCategory === "stealth" ||
      normalizedCategory === "shouts" ||
      normalizedCategory === "immersion" ||
      normalizedCategory === "npc"
    ) {
      return { group: "other", ambiguity: "high" };
    }

    if (normalizedCategory === "cheats and god items") {
      return { group: "utility_like", ambiguity: "high" };
    }

    // Asset-heavy buckets
    if (
      normalizedCategory === "models and textures" ||
      normalizedCategory === "visuals and graphics" ||
      normalizedCategory === "environmental" ||
      normalizedCategory === "buildings" ||
      normalizedCategory === "audio" ||
      normalizedCategory === "animation" ||
      normalizedCategory === "body, face, and hair" ||
      normalizedCategory === "armour" ||
      normalizedCategory === "armour - shields" ||
      normalizedCategory === "weapons" ||
      normalizedCategory === "weapons and armour" ||
      normalizedCategory === "clothing and accessories" ||
      normalizedCategory === "items and objects - player" ||
      normalizedCategory === "items and objects - world"
    ) {
      // Animation can sometimes hide frameworks/behaviour complexity; treat as medium ambiguity.
      if (normalizedCategory === "animation") {
        return { group: "assets_like", ambiguity: "medium" };
      }
      if (normalizedCategory === "environmental" || normalizedCategory === "buildings") {
        return { group: "assets_like", ambiguity: "high" };
      }
      return { group: "assets_like", ambiguity: "low" };
    }

    // Everything else
    return { group: "other", ambiguity: "high" };
  })();

  // Refinement using mod title/description (secondary source of truth).
  // If authors chose a broad category like Gameplay/Utilities, these cues can
  // help detect frameworks/patches.
  const refined = (() => {
    if (combined.includes("framework") || combined.includes("api")) {
      return { group: "framework_like", ambiguity: "medium" as const };
    }
    if (combined.includes("patch") || combined.includes("compatibility patch")) {
      return { group: "patch_like", ambiguity: "low" as const };
    }
    // Treat "settings loader"/presets/replacers as narrow-scope even if Nexus
    // categories are broad.
    if (
      combined.includes("settings loader") ||
      combined.includes("mcm") ||
      combined.includes("preset") ||
      combined.includes("replacer") ||
      combined.includes("retexture") ||
      combined.includes("texture")
    ) {
      return { group: "patch_like", ambiguity: "low" as const };
    }

    // Promote to overhaul_like only on stronger cues than the bare word
    // "overhaul" (to avoid misclassifying things like "Well Overhaul").
    const strongOverhaulCues =
      combined.includes("complete overhaul") ||
      combined.includes("major overhaul") ||
      combined.includes("comprehensive") ||
      combined.includes("overhaul of") ||
      combined.includes("perk overhaul") ||
      combined.includes("combat overhaul") ||
      combined.includes("magic overhaul") ||
      combined.includes("race overhaul") ||
      combined.includes("standing stone overhaul") ||
      combined.includes("religion overhaul") ||
      combined.includes("shout overhaul") ||
      combined.includes("loot overhaul") ||
      combined.includes("encounter zone") ||
      combined.includes("leveled list overhaul") ||
      combined.includes("ai overhaul");

    if (strongOverhaulCues) {
      return { group: "overhaul_like", ambiguity: byCategory?.ambiguity ?? ("medium" as const) };
    }
    return byCategory;
  })() as CategoryGroupHint;

  const topics = new Set<string>();
  const overlapDomains = new Set<string>();
  const overlapTags = new Set<string>();
  if (
    normalizedCategory === "combat" ||
    hasWord(combined, "combat") ||
    hasWord(combined, "enemy")
  ) {
    topics.add("combat");
    overlapDomains.add("combat");
    overlapTags.add("system:combat");
  }
  if (
    normalizedCategory === "user interface" ||
    hasWord(combined, "hud") ||
    hasWord(combined, "interface") ||
    hasWord(combined, "ui")
  ) {
    topics.add("ui");
    overlapDomains.add("ui");
    if (hasWord(combined, "hud") || hasPhrase(combined, "truehud") || hasPhrase(combined, "morehud")) {
      overlapTags.add("ui:hud");
    }
    if (hasAnyWord(combined, ["icon", "icons"]) || hasPhrase(combined, "interaction icons")) {
      overlapTags.add("ui:icons");
    }
    if (hasAnyPhrase(combined, ["paper map", "map markers"]) || hasAnyWord(combined, ["map", "atlas"])) {
      overlapTags.add("ui:map");
    }
    if (
      hasAnyPhrase(combined, ["main menu", "wait menu", "skill menu", "loading screen", "load screen"]) ||
      (normalizedCategory === "user interface" && hasWord(combined, "menu"))
    ) {
      overlapTags.add("ui:menu");
    }
  }
  if (normalizedCategory.includes("followers") || hasAnyWord(combined, ["follower", "followers", "companion"])) {
    topics.add("followers");
    overlapDomains.add("followers");
    overlapTags.add("system:followers");
  }
  if (
    hasWord(combined, "ai") ||
    hasAnyWord(combined, ["behavior", "behaviour"]) ||
    hasAnyPhrase(combined, ["ai overhaul", "npc ai", "npc behaviour", "npc behavior"])
  ) {
    topics.add("AI");
    overlapDomains.add("ai");
    overlapTags.add("system:ai");
  }
  if (hasAnyWord(combined, ["perk", "perks"]) || hasAnyPhrase(combined, ["skill tree"]) || hasWord(combined, "leveling")) {
    topics.add("perks");
    overlapDomains.add("perks");
    overlapTags.add("system:perks");
  }
  if (
    hasAnyWord(combined, ["needs", "hunger", "thirst", "survival"])
  ) {
    topics.add("survival");
    overlapDomains.add("survival");
    overlapTags.add("system:survival");
  }
  if (
    hasAnyWord(combined, ["lighting", "shadow", "shadows", "lantern", "lanterns", "lights"]) ||
    hasAnyPhrase(combined, ["light limit", "enb light", "enb lights"])
  ) {
    topics.add("lighting");
    overlapDomains.add("lighting");
    overlapTags.add("visual:lighting");
  }
  if (hasAnyWord(combined, ["weather", "climate", "fog", "storms", "storm", "rain", "snow"])) {
    topics.add("weather");
    overlapDomains.add("weather");
    overlapTags.add("visual:weather");
  }
  if (hasAnyPhrase(combined, ["paper map", "map markers"]) || hasAnyWord(combined, ["map", "atlas"])) {
    overlapDomains.add("map");
    overlapTags.add("ui:map");
  }
  if (
    hasAnyPhrase(combined, ["main menu", "wait menu", "skill menu", "loading screen", "load screen", "load screens"])
  ) {
    overlapDomains.add("menus");
    overlapTags.add("ui:menu");
  }
  if (normalizedCategory === "animation" || hasAnyWord(combined, ["animation", "animations"])) {
    overlapDomains.add("animations");
    overlapTags.add("system:animations");
  }
  if (hasWord(combined, "skeleton") || hasAnyPhrase(combined, ["xp32", "xpmse"])) {
    overlapDomains.add("skeleton");
    overlapTags.add("system:skeleton");
  }
  if (
    hasAnyPhrase(combined, ["leveled list", "levelled list", "encounter zone"]) ||
    hasAnyPhrase(combined, ["loot overhaul"]) ||
    hasWord(combined, "loot")
  ) {
    overlapDomains.add("loot_balance");
    overlapTags.add("system:loot_balance");
  }
  if (normalizedCategory === "quests and adventures" || hasAnyWord(combined, ["quest", "quests"])) {
    overlapDomains.add("quests");
  }
  if (
    normalizedCategory === "cities, towns, villages, and hamlets" ||
    normalizedCategory.startsWith("locations") ||
    hasAnyWord(combined, ["city", "cities", "town", "towns", "village", "villages"])
  ) {
    overlapDomains.add("locations");
  }

  // Location-specific target tags (only match when the same target appears).
  const locationTarget = extractLocationTargetFromName(mod.name);
  if (locationTarget) {
    overlapTags.add(`location:${normalizeOverlapKey(locationTarget)}`);
  }
  if (
    combined.includes("city") ||
    combined.includes("town") ||
    combined.includes("village") ||
    combined.includes("whiterun") ||
    combined.includes("solitude") ||
    combined.includes("riften") ||
    combined.includes("windhelm")
  ) {
    topics.add("cities");
  }
  return {
    nexusCategory,
    categoryGroup: refined?.group,
    categoryAmbiguity: refined?.ambiguity,
    topics: topics.size ? Array.from(topics) : undefined,
    overlapDomains: overlapDomains.size ? Array.from(overlapDomains) : undefined,
    overlapTags: overlapTags.size ? Array.from(overlapTags) : undefined,
  };
};

const inferScopeHint = (
  categoryAmbiguity: ModInfo["categoryAmbiguity"] | undefined,
  categoryGroup: ModInfo["categoryGroup"] | undefined,
  combinedLower: string,
): ModInfo["scopeHint"] => {
  // Strong broad-scope cues from text.
  const broadCues =
    combinedLower.includes("complete overhaul") ||
    combinedLower.includes("major overhaul") ||
    combinedLower.includes("massive") ||
    combinedLower.includes("total") ||
    combinedLower.includes("comprehensive") ||
    combinedLower.includes("rework") ||
    combinedLower.includes("revamp");

  const narrowCues =
    combinedLower.includes("tweak") ||
    combinedLower.includes("small") ||
    combinedLower.includes("minor") ||
    combinedLower.includes("optional") ||
    combinedLower.includes("lightweight") ||
    combinedLower.includes("settings loader") ||
    combinedLower.includes("mcm") ||
    combinedLower.includes("preset") ||
    combinedLower.includes("replacer") ||
    combinedLower.includes("retexture") ||
    combinedLower.includes("texture") ||
    combinedLower.includes("patch") ||
    combinedLower.includes("addon") ||
    combinedLower.includes("fix") ||
    combinedLower.includes("paper map") ||
    combinedLower.includes("map marker") ||
    combinedLower.includes("map") ||
    combinedLower.includes("main menu") ||
    combinedLower.includes("loading screen");

  if (broadCues) return "broad";
  if (narrowCues) return "narrow";

  // If category is ambiguous, default to medium unless it's clearly patch/assets.
  if (categoryAmbiguity === "high") {
    if (categoryGroup === "patch_like" || categoryGroup === "assets_like") return "narrow";
    return "medium";
  }

  // Otherwise fall back to group expectations.
  if (categoryGroup === "framework_like" || categoryGroup === "overhaul_like") return "broad";
  if (categoryGroup === "content_like") return "medium";
  if (categoryGroup === "ui_like" || categoryGroup === "utility_like") return "medium";
  return "narrow";
};

const buildImportanceScore = (
  mod: ModInfo,
  metadata: NexusModMetadata,
  categoryGroup?: ModInfo["categoryGroup"],
  categoryAmbiguity?: ModInfo["categoryAmbiguity"],
): { score: number; scopeHint: ModInfo["scopeHint"] } => {
  let score = 0;

  // Base weight from category-derived group.
  switch (categoryGroup) {
    case "framework_like":
      score += 4;
      break;
    case "overhaul_like":
      // If the category is highly ambiguous (e.g. Gameplay/Immersion/NPC), we
      // give a smaller base score and rely more on text cues.
      score += categoryAmbiguity === "high" ? 2 : 3;
      break;
    case "content_like":
      score += 2;
      break;
    case "ui_like":
      score += 1;
      break;
    case "utility_like":
      score += 1;
      break;
    case "patch_like":
      score += 0;
      break;
    case "assets_like":
      score += 0;
      break;
    default:
      break;
  }

  // Secondary cues from the description/title.
  const combined = [
    metadata.name,
    metadata.summary,
    metadata.description ?? "",
    metadata.category ?? "",
    ...(metadata.tags ?? []),
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  if (combined.includes("framework")) score += 2;
  if (combined.includes("skse")) score += 2;
  if (combined.includes("dll")) score += 2;
  if (combined.includes("complete overhaul") || combined.includes("major overhaul")) score += 1;

  const downloads = metadata.downloads ?? 0;
  // Popularity is a weak proxy for impact (tiny QoL mods can be extremely
  // popular). Keep this influence small to avoid over-inflating importance.
  if (downloads >= 2_000_000) {
    score += 2;
  } else if (downloads >= 500_000) {
    score += 1;
  }

  if (mod.plugins.some((plugin) => plugin.toLowerCase().endsWith(".esm"))) {
    score += 2;
  }

  // Multiple plugins generally increase surface area.
  if (mod.plugins.length >= 3) score += 1;
  if (mod.plugins.length >= 6) score += 1;

  const scopeHint = inferScopeHint(categoryAmbiguity, categoryGroup, combined);
  if (scopeHint === "broad") score += 1;

  // Guardrail: highly ambiguous categories with narrow scope and no strong
  // technical signals should not become medium/high importance just because
  // they're popular.
  const hasTechnicalSignal =
    combined.includes("skse") || combined.includes("dll") || combined.includes("framework");
  const hasMasterPlugin = mod.plugins.some((plugin) => plugin.toLowerCase().endsWith(".esm"));
  if (
    categoryAmbiguity === "high" &&
    scopeHint === "narrow" &&
    !hasTechnicalSignal &&
    !hasMasterPlugin &&
    mod.plugins.length <= 1
  ) {
    score = Math.min(score, 3);
  }

  // Second guardrail: narrow-scope UI/utility/patch/assets mods should almost
  // never become medium/high importance. Keep them "low" unless they're
  // explicitly framework-like or clearly broad-scope.
  if (
    scopeHint === "narrow" &&
    categoryGroup !== "framework_like" &&
    categoryGroup !== "overhaul_like"
  ) {
    score = Math.min(score, 3);
  }

  return { score, scopeHint };
};

const buildImportanceBucketFromScore = (score: number): ModInfo["importanceBucket"] => {
  if (score >= 8) return "high";
  if (score >= 4) return "medium";
  return "low";
};

const isStale = (metadata: NexusModMetadata): boolean => {
  const status = (metadata.status ?? "").toLowerCase();
  if (status === "archived" || status === "hidden" || status === "deprecated") {
    return true;
  }

  const updatedAt = metadata.lastUpdated;
  if (!updatedAt) {
    return false;
  }

  const updated = new Date(updatedAt);
  if (Number.isNaN(updated.getTime())) {
    return false;
  }

  const now = Date.now();
  const ageMs = now - updated.getTime();
  const days = ageMs / (1000 * 60 * 60 * 24);
  // Treat mods older than roughly a year as stale; this is a heuristic and
  // may be refined later.
  return days > 365;
};

const buildNexusMetadataBucket = (metadata: NexusModMetadata) => ({
  nexusId: metadata.nexusId,
  name: metadata.name,
  summary: metadata.summary,
  description: metadata.description,
  version: metadata.version,
  url: metadata.url,
  gameDomain: metadata.game,
  category: metadata.category,
  tags: metadata.tags,
  downloads: metadata.downloads,
  endorsements: metadata.endorsements,
  status: metadata.status,
  lastUpdated: metadata.lastUpdated,
  requirements: metadata.requirements,
});

export const enrichProfileWithNexus = async (
  profile: ProfileSnapshot,
  settings: Settings,
): Promise<{ profile: ProfileSnapshot; used: boolean; error?: string }> => {
  if (!settings.nexusApiKey) {
    // Nexus is not configured; return the original profile unchanged.
    await logger.debug(
      `[NexusEnrichment] Skipping Nexus enrichment for profile=${profile.profileId}; no API key configured.`,
    );
    return { profile, used: false };
  }

  const modsWithIds = profile.mods.filter(
    (mod) =>
      typeof mod.nexusId === "number" && Number.isFinite(mod.nexusId) && (mod.nexusId as number) > 0,
  );

  if (!modsWithIds.length) {
    await logger.debug(
      `[NexusEnrichment] No mods with nexusId found for profile=${profile.profileId}; skipping Nexus enrichment.`,
    );
    return { profile, used: false };
  }

  const client = new NexusClient(settings, profile.game);

  let metadataList: NexusModMetadata[];

  try {
    const nexusIds = modsWithIds.map((mod) => mod.nexusId as number);

    metadataList = await client.getModMetadataBatch(nexusIds);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);

    if (error instanceof NexusConfigError) {
      await logger.warn(
        `[NexusEnrichment] Nexus configuration error while enriching profile=${profile.profileId}: ${message}`,
      );
    } else if (error instanceof NexusUnavailableError) {
      await logger.warn(
        `[NexusEnrichment] Nexus temporarily unavailable while enriching profile=${profile.profileId}: ${message}`,
      );
    } else if (error instanceof NexusError) {
      await logger.warn(
        `[NexusEnrichment] Nexus error while enriching profile=${profile.profileId}: ${message}`,
      );
    } else {
      await logger.warn(
        `[NexusEnrichment] Unexpected error while enriching profile=${profile.profileId}: ${message}`,
      );
    }

    // In all error cases, fall back to the original profile so offline
    // analysis and the agent can still run using non-Nexus data, but surface
    // the error so callers can track Nexus health.
    return {
      profile,
      used: false,
      error: message,
    };
  }

  const byId = new Map<number, NexusModMetadata>();
  metadataList.forEach((meta) => {
    byId.set(meta.nexusId, meta);
  });

  if (!byId.size) {
    await logger.debug(
      `[NexusEnrichment] Nexus returned no metadata for any mods in profile=${profile.profileId}; leaving profile unchanged.`,
    );
    return { profile, used: false };
  }

  const enrichedMods: ModInfo[] = profile.mods.map((mod) => {
    const nexusId = mod.nexusId;
    if (!nexusId || !byId.has(nexusId)) {
      return mod;
    }

    const nexusMeta = byId.get(nexusId) as NexusModMetadata;

    const gameSupport: GameSupport = mapDomainToGameSupport(nexusMeta.game);
    const { nexusCategory, categoryGroup, categoryAmbiguity, topics, overlapDomains, overlapTags } =
      buildCategoryGroupAndTopics(
      mod,
      nexusMeta,
    );
    const { score: importanceScore, scopeHint } = buildImportanceScore(
      mod,
      nexusMeta,
      categoryGroup,
      categoryAmbiguity,
    );
    const importanceBucket = buildImportanceBucketFromScore(importanceScore);
    const stale = isStale(nexusMeta);

    const existingMetadata = mod.metadata ?? {};

    const enrichedMetadata: Record<string, unknown> = {
      ...existingMetadata,
      nexus: buildNexusMetadataBucket(nexusMeta),
    };

    return {
      ...mod,
      latestVersion: nexusMeta.version,
      gameSupport,
      nexusLastUpdated: nexusMeta.lastUpdated,
      nexusStatus: nexusMeta.status,
      nexusDownloads: nexusMeta.downloads,
      nexusEndorsements: nexusMeta.endorsements,
      nexusCategory,
      categoryGroup,
      categoryAmbiguity,
      scopeHint,
      overlapDomains,
      overlapTags,
      importanceBucket,
      importanceScore,
      stale,
      topics,
      metadata: enrichedMetadata,
    } as ModInfo;
  });

  await logger.info(
    `[NexusEnrichment] Enriched profile=${profile.profileId} with Nexus metadata for ${byId.size} mod(s).`,
  );

  return {
    profile: {
      ...profile,
      mods: enrichedMods,
    },
    used: true,
  };
};



