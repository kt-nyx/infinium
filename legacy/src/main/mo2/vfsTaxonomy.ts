export type VfsScope = "hotspots" | "extended" | "full";

export type VfsCategory =
  | "scripts"
  | "interface"
  | "skse_dll"
  | "skse_config"
  | "plugins"
  | "ini"
  | "behavior"
  | "nemesis"
  | "dyndolod"
  | "synthesis"
  | "bsa"
  | "other_hotspot";

export const VFS_TAXONOMY_VERSION = 1;

export const isMoHiddenPath = (virtualPath: string): boolean => virtualPath.toLowerCase().endsWith(".mohidden");

export const normalizeVirtualPathKey = (virtualPath: string): string =>
  virtualPath.replace(/\\/g, "/").replace(/^\/+/, "").toLowerCase();

export const classifyVirtualPath = (
  virtualPath: string,
): VfsCategory | null => {
  const key = normalizeVirtualPathKey(virtualPath);
  if (!key) return null;
  if (isMoHiddenPath(key)) return null;

  // v1 classifier rules (hotspot-centric; refine over time).
  if (/^scripts\/.*\.pex$/i.test(key)) return "scripts";
  if (/^interface\/.*\.swf$/i.test(key)) return "interface";
  if (/^skse\/plugins\/.*\.dll$/i.test(key)) return "skse_dll";
  if (/^skse\/.*\.(ini|toml|json)$/i.test(key)) return "skse_config";
  if (/^nemesis_engine\//i.test(key)) return "nemesis";
  if (/^meshes\/.*(behavior|behaviour)/i.test(key)) return "behavior";
  if (/^.*\.(esm|esp|esl)$/i.test(key)) return "plugins";
  if (/^.*\.bsa$/i.test(key)) return "bsa";

  // low-priority / broad signals (use cautiously)
  if (/^.*\.ini$/i.test(key)) return "ini";

  return null;
};

export const hotspotRootsForScope = (scope: VfsScope): string[] => {
  if (scope === "full") {
    return [""]; // scan mod root with exclusion rules/caps elsewhere
  }
  if (scope === "extended") {
    return ["scripts", "interface", "skse", "nemesis_engine", "meshes"];
  }
  // hotspots
  return ["scripts", "interface", "skse", "nemesis_engine"];
};

export const shouldSkipFullScanPath = (virtualPath: string): boolean => {
  const key = normalizeVirtualPathKey(virtualPath);
  if (!key) return true;
  if (isMoHiddenPath(key)) return true;

  // Ignore obvious non-game/noise files in full scan unless explicitly classified.
  const parts = key.split("/");
  const baseName = parts.length ? parts[parts.length - 1] : key;
  if (baseName === "meta.ini") return true;
  if (/\.(log|md|txt)$/i.test(baseName)) return true;

  return false;
};

