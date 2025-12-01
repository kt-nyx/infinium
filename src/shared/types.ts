// src/shared/types.ts

export type Severity = "critical" | "high" | "medium" | "low" | "suggestion";

export type IssueCategory =
  | "missing_masters"
  | "hard_incompatibility"
  | "soft_conflict"
  | "outdated_or_wrong_version"
  | "performance_risk"
  | "script_load"
  | "load_order"
  | "redundancy"
  | "aesthetic_suggestion"
  | "configuration"
  | "other";

export interface ModInfo {
  id: string;
  name: string;
  enabled: boolean;
  path: string;
  nexusId?: number;
  gameSupport?: "SkyrimLE" | "SkyrimSE" | "SkyrimAE" | "Unknown";
  installedVersion?: string;
  latestVersion?: string;
  plugins: string[];
  metadata?: Record<string, unknown>;
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

export interface Issue {
  id: string;
  severity: Severity;
  category: IssueCategory;
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
