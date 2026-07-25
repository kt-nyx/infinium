// src/shared/types.ts

export type Severity = "critical" | "high" | "medium" | "low" | "suggestion";

/**
 * Canonical issue categories used by LOOT/rules and for stable UI grouping.
 *
 * The agent/orchestrator may emit novel categories; therefore `Issue.category`
 * is intentionally open-ended (string).
 */
export const KNOWN_ISSUE_CATEGORIES = [
  "missing_masters",
  "hard_incompatibility",
  "soft_conflict",
  "outdated_or_wrong_version",
  "performance_risk",
  "script_load",
  "load_order",
  "redundancy",
  "aesthetic_suggestion",
  "configuration",
  "other",
] as const;

export type KnownIssueCategory = (typeof KNOWN_ISSUE_CATEGORIES)[number];

export interface ModInfo {
  id: string;
  name: string;
  enabled: boolean;
  path: string;
  /**
   * Optional Nexus Mods numeric ID when the mod has been linked to a Nexus
   * entry. When present, additional Nexus-derived fields may also be set
   * either directly on this object or under metadata.nexus.
   */
  nexusId?: number;
  /**
   * Basic game-edition support hint derived from Nexus metadata. This is
   * intentionally coarse (LE vs SE/AE vs Unknown) and should be treated as a
   * signal rather than a guarantee.
   */
  gameSupport?: "SkyrimLE" | "SkyrimSE" | "SkyrimAE" | "Unknown";
  installedVersion?: string;
  latestVersion?: string;
  /**
   * Optional Nexus-derived fields that are convenient to access directly from
   * ModInfo without needing to inspect the metadata bucket.
   */
  nexusLastUpdated?: string;
  nexusStatus?: string;
  nexusDownloads?: number;
  nexusEndorsements?: number;
  /**
   * Nexus Mods category label (e.g. "Overhauls", "Patches", "User Interface").
   * This comes from the Nexus mod metadata and should be treated as the primary
   * "what kind of mod is this?" signal, though authors don't always choose the
   * perfect category.
   */
  nexusCategory?: string;
  /**
   * Derived grouping that buckets Nexus categories into broader buckets used by
   * heuristics and the agent to reason about scope/impact.
   */
  categoryGroup?:
    | "framework_like"
    | "overhaul_like"
    | "patch_like"
    | "content_like"
    | "assets_like"
    | "ui_like"
    | "utility_like"
    | "other";
  /**
   * Heuristic hint about how ambiguous the Nexus category is with respect to
   * impact/scope. Some categories (e.g. Gameplay/Immersion/NPC) can range from
   * tiny tweaks to massive overhauls, so we treat them as ambiguous and rely
   * more on title/description + file contents for scope estimation.
   */
  categoryAmbiguity?: "low" | "medium" | "high";
  /**
   * Best-effort scope hint inferred from Nexus category + title/description.
   * This is intended to guide deep-dive prioritization (e.g. whether to always
   * read description/modFiles for a mod at lower complexity).
   */
  scopeHint?: "narrow" | "medium" | "broad";
  /**
   * Hints about what gameplay/technical domains this mod likely affects.
   * These are used to find overlap/conflict candidates (e.g. multiple HUD mods,
   * multiple combat overhauls, multiple map/UI overhauls) even when individual
   * mods are not \"high importance\" on their own.
   */
  overlapDomains?: string[];
  /**
   * More specific overlap signals than overlapDomains. These are intended to
   * only match when two mods plausibly target the same thing, e.g.:
   * - "location:riften_aerins_house"
   * - "ui:hud"
   * - "ui:icons"
   */
  overlapTags?: string[];
  /**
   * Overlap tags inferred by the LLM agent from titles + descriptions.
   * When present, these should be preferred over heuristic overlapTags.
   */
  overlapTagsAgent?: string[];

  /**
   * Agent-extracted requirements, patches, and load-order rules from mod/Nexus
   * descriptions and file metadata. These are best-effort and should be treated
   * as signals with confidence attached.
   */
  requirementsAgent?: {
    kind: "required" | "soft" | "patch";
    /**
     * Prefer stable identifiers when possible.
     */
    targetModId?: string;
    targetModName?: string;
    targetPlugin?: string;
    evidence: string;
    confidence: "high" | "medium" | "low";
  }[];
  loadOrderRulesAgent?: {
    relation: "before" | "after";
    targetModId?: string;
    targetModName?: string;
    targetPlugin?: string;
    evidence: string;
    confidence: "high" | "medium" | "low";
  }[];

  /**
   * Agent judgment about whether the installed mod variant (SE/AE, lite/full,
   * Nemesis/FNIS, etc.) appears correct.
   */
  variantAgent?: {
    expected: string;
    detected: string;
    mismatch: boolean;
    evidence: string;
    confidence: "high" | "medium" | "low";
  };

  /**
   * Agent triage of script/performance risk, preferably grounded in description
   * claims plus (at higher complexity) Nexus archive file summaries.
   */
  scriptPerfRiskAgent?: {
    level: "low" | "medium" | "high";
    reasons: string[];
    confidence: "high" | "medium" | "low";
    fileSummaryUsed?: boolean;
  };

  /**
   * Agent suggestions that this mod may be redundant with another mod in the
   * profile (complementary vs redundant should be explained).
   */
  redundancyCandidatesAgent?: {
    otherModId: string;
    rationale: string;
    confidence: "high" | "medium" | "low";
  }[];
  /**
   * Optional importance bucket and staleness hint computed during Nexus
   * enrichment. These are used by the agent to decide how aggressively to
   * analyse a mod and by some rules-engine heuristics.
   */
  importanceBucket?: "high" | "medium" | "low";
  /**
   * Numeric score used to derive importanceBucket. This is exposed primarily so
   * we can tune thresholds and explain behavior in logs/UI if desired.
   */
  importanceScore?: number;
  stale?: boolean;
  /**
   * Optional topic hints inferred from Nexus category/title/description.
   * Best-effort; treat as hints rather than strict taxonomy.
   */
  topics?: string[];
  plugins: string[];
  metadata?: Record<string, unknown>;
}

/**
 * Lightweight representation of a Nexus mod used for search and ID attachment.
 */
export interface NexusModSearchResult {
  modId: number;
  name: string;
  summary: string;
  gameDomain: string;
  url: string;
  downloads: number;
  endorsements: number;
  lastUpdated?: string;
}

export interface ProfileSnapshot {
  profileId: string;
  game: "SkyrimLE" | "SkyrimSE" | "SkyrimAE";
  mo2InstancePath: string;
  mods: ModInfo[];
  pluginLoadOrder: string[];
  lootAvailable: boolean;
  nexusAvailable: boolean;
}

export type LootIssueMessageSeverity = "error" | "warning" | "note";

export interface LootMissingMastersDetail {
  plugin: string;
  masters: string[];
}

export interface LootIssuePluginMessage {
  plugin: string;
  severity: LootIssueMessageSeverity;
  text: string;
  language?: string;
  condition?: string;
}

export interface IssueEvidence {
  nexusModUrl?: string;
  nexusModId?: number;
  nexusFileVersion?: string;
  nexusCommentIds?: string[];
  nexusCollectionSlug?: string;
  nexusBugReportIds?: string[];
}

export interface Issue {
  id: string;
  severity: Severity;
  /**
   * Open-ended category string. Prefer values from `KNOWN_ISSUE_CATEGORIES`
   * where applicable, but novel categories are allowed.
   */
  category: string;
  /**
   * Optional UI-friendly normalization of category for grouping. The raw
   * `category` is preserved.
   */
  categoryNormalized?: string;
  subcategory?: string;
  summary: string;
  details: string;
  affectedMods: string[];
  affectedPlugins: string[];
  risky: boolean;
  confidence: "high" | "medium" | "low";
  source: Array<"loot" | "rules" | "nexus" | "rag" | "agent">;
  /**
   * When present, this issue represents an aggregated view of all missing
   * masters reported by LOOT. Each entry lists a plugin and its missing
   * masters, which the UI can render as a nested, expandable list.
   */
  lootMissingMasters?: LootMissingMastersDetail[];
  /**
   * Optional LOOT-derived messages associated with this issue, typically for
   * a single plugin. These are used to surface LOOT warnings/errors (and
   * related notes) in the issue details view.
   */
  lootPluginMessages?: LootIssuePluginMessage[];
  /**
   * Optional structured evidence associated with this issue, primarily used
   * to record Nexus-backed context such as mod URLs, file versions, comment
   * IDs, or collection bug reports.
   */
  evidence?: IssueEvidence;
  /**
   * Optional structured facets used by overlap/candidate-style issues.
   * Intended to be compact and referenced by evidence snippets/IDs.
   */
  facets?: Array<{
    kind: string;
    value: string;
    confidence: "high" | "medium" | "low";
    evidence: string[];
  }>;
  /**
   * Optional support links (docs, mod pages, guides) relevant to the issue.
   */
  supportLinks?: Array<{ kind: string; url: string; label?: string }>;
  /**
   * Optional compact evidence snippets (avoid bloating `details`).
   */
  evidenceRefs?: Array<{
    source: string;
    modId?: string;
    url?: string;
    snippet: string;
  }>;
  /**
   * Optional structured overlap breakdown for overlap-style issues. When
   * present, the UI can render separate grouped lists rather than a single flat
   * affectedMods list.
   */
  overlapGroups?: { tag: string; modIds: string[] }[];
}

export interface Recommendation {
  issueId: string;
  steps: string[];
  notes?: string;
}

export interface AnalysisResult {
  profile: ProfileSnapshot;
  issues: Issue[];
  recommendations: Recommendation[];
  metadata: {
    offlineOnly: boolean;
    complexityLevel: number;
    opinionatedness: number;
    agentUsed: boolean;
    createdAt: string;
    /**
     * Optional per-run trace artifact ID produced by the orchestrator.
     * This can be used to locate the stored debug trace on disk.
     */
    analysisTraceId?: string;
    /**
     * Optional Nexus usage metadata for this run. When present, indicates
     * whether Nexus was successfully used and, if not, why.
     */
    nexusUsed?: boolean;
    nexusError?: string;
  };
}

export interface Settings {
  mo2RootGuess?: string;
  mo2Instances: { name: string; path: string }[];
  selectedInstanceId?: string;
  selectedProfileId?: string;
  /**
   * Absolute path to the Skyrim SE/AE Data folder (or game root) used when
   * constructing libloot requests. Required for LOOT-powered analysis.
   */
  skyrimSeDataPath?: string;
  lootPortablePath?: string;
  lootInstalledPath?: string;
  lootMode: "portable" | "installed" | "custom" | "auto";
  lootCustomPath?: string;
  nexusApiKey?: string;
  ragIndexPath?: string;
  /**
   * Controls whether the default \"Analyze\" action runs offline-only heuristics
   * or performs a full agentic run with tools.
   */
  analysisMode?: "offline" | "agentic";
  analysisDefaults: {
    useLoot: boolean;
    useNexus: boolean;
    useRag: boolean;
    complexity: number;
    opinionatedness: number;
  };
  logLevel: "error" | "warn" | "info" | "debug";
}
