import { promises as fs } from "node:fs";
import path from "node:path";
import type { ModInfo, ProfileSnapshot } from "../../shared/types";
import { logger } from "../logging";
import type { VfsCategory, VfsScope } from "./vfsTaxonomy";
import {
  VFS_TAXONOMY_VERSION,
  classifyVirtualPath,
  hotspotRootsForScope,
  normalizeVirtualPathKey,
  shouldSkipFullScanPath,
} from "./vfsTaxonomy";

export type VfsWinnerId = string | "__overwrite__";

export type ConflictEdgeCounts = {
  total: number;
  byCategory: Partial<Record<VfsCategory, number>>;
};

export type ModFileSignature = {
  counts: Partial<Record<VfsCategory, number>>;
  flags: {
    hasScripts: boolean;
    hasInterfaceSwf: boolean;
    hasSkseDll: boolean;
    hasBsa: boolean;
  };
};

export type VfsIndex = {
  schemaVersion: number;
  taxonomyVersion: number;
  scope: VfsScope;
  categoriesScanned: VfsCategory[];
  categoryStats: Partial<Record<VfsCategory, { totalPaths: number; collisions: number }>>;
  perModSignature: Record<string, ModFileSignature>;
  pluginFileProviders: Record<string, string[]>; // pluginLower -> modIds (priority ordered)
  edgeCounts: Record<string, Record<string, ConflictEdgeCounts>>; // winner -> loser -> counts
  edgeSamples: Record<string, Record<string, string[]>>; // winner -> loser -> sample virtual paths
  overwriteSummary: {
    nonEmpty: boolean;
    countsByCategory: Partial<Record<VfsCategory, number>>;
  };
  coverage: {
    startedAt: string;
    durationMs: number;
    scannedMods: number;
    scannedFiles: number;
    partial: boolean;
    stopReason?: string;
  };
};

const VFS_INDEX_SCHEMA_VERSION = 1;

const nowIso = (): string => new Date().toISOString();

const resolveVfsCacheDir = (): string => {
  const base = process.env.SKYRIM_AI_CACHE_DIR ?? ".tmp-cache";
  return path.join(base, "vfs");
};

const safeJson = (value: unknown): string => {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return JSON.stringify({ error: "failed_to_stringify" }, null, 2);
  }
};

const capPush = (arr: string[], value: string, max: number) => {
  if (arr.length >= max) return;
  arr.push(value);
};

const incNested = <T>(
  root: Record<string, Record<string, T>>,
  a: string,
  b: string,
  init: () => T,
): T => {
  const row = root[a] ?? {};
  const current = (row[b] as T | undefined) ?? init();
  row[b] = current;
  root[a] = row;
  return current;
};

const emptyEdge = (): ConflictEdgeCounts => ({ total: 0, byCategory: {} });

const emptySignature = (): ModFileSignature => ({
  counts: {},
  flags: {
    hasScripts: false,
    hasInterfaceSwf: false,
    hasSkseDll: false,
    hasBsa: false,
  },
});

const statMtimeMs = async (p: string): Promise<number | null> => {
  try {
    const st = await fs.stat(p);
    return st.mtimeMs;
  } catch {
    return null;
  }
};

type CachedModScan = {
  schemaVersion: number;
  taxonomyVersion: number;
  scope: VfsScope;
  modId: string;
  roots: Array<{ root: string; mtimeMs: number | null; files: string[] }>;
  signature: ModFileSignature;
  pluginsAtRoot: string[];
  bsasAtRoot: string[];
};

const cachePathForMod = (profileId: string, modId: string, scope: VfsScope): string =>
  path.join(resolveVfsCacheDir(), "mods", `${profileId}`, `${modId}-${scope}.json`);

const readCachedModScan = async (
  profileId: string,
  mod: ModInfo,
  scope: VfsScope,
): Promise<CachedModScan | null> => {
  const filePath = cachePathForMod(profileId, mod.id, scope);
  try {
    const raw = await fs.readFile(filePath, "utf-8");
    const parsed = JSON.parse(raw) as CachedModScan;
    if (parsed?.schemaVersion !== VFS_INDEX_SCHEMA_VERSION) return null;
    if (parsed?.taxonomyVersion !== VFS_TAXONOMY_VERSION) return null;
    if (parsed?.scope !== scope) return null;
    if (parsed?.modId !== mod.id) return null;

    // Validate mtimes for all cached roots.
    for (const r of parsed.roots ?? []) {
      const abs = r.root ? path.join(mod.path, r.root) : mod.path;
      const current = await statMtimeMs(abs);
      if (current !== r.mtimeMs) {
        return null;
      }
    }
    return parsed;
  } catch {
    return null;
  }
};

const writeCachedModScan = async (
  profileId: string,
  modId: string,
  scope: VfsScope,
  payload: CachedModScan,
): Promise<void> => {
  const filePath = cachePathForMod(profileId, modId, scope);
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, safeJson(payload), "utf-8");
};

const listRootFiles = async (modRoot: string): Promise<string[]> => {
  try {
    const entries = await fs.readdir(modRoot, { withFileTypes: true });
    return entries.filter((e) => e.isFile()).map((e) => String(e.name));
  } catch {
    return [];
  }
};

const walkFiles = async (absRoot: string, relRoot: string, opts: {
  maxDepth: number;
  maxFiles: number;
  shouldInclude: (relPath: string) => boolean;
  shouldSkipDir?: (relDir: string) => boolean;
}): Promise<string[]> => {
  const out: string[] = [];

  const walk = async (absDir: string, relDir: string, depth: number): Promise<void> => {
    if (out.length >= opts.maxFiles) return;
    if (depth > opts.maxDepth) return;
    let entries;
    try {
      entries = await fs.readdir(absDir, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      if (out.length >= opts.maxFiles) return;
      const name = String(entry.name);
      const abs = path.join(absDir, name);
      const rel = relDir ? `${relDir}/${name}` : name;

      if (entry.isDirectory()) {
        if (opts.shouldSkipDir && opts.shouldSkipDir(rel)) continue;
        await walk(abs, rel, depth + 1);
      } else if (entry.isFile()) {
        const combinedRel = relRoot ? `${relRoot}/${rel}` : rel;
        if (opts.shouldInclude(combinedRel)) out.push(combinedRel);
      }
    }
  };

  await walk(absRoot, "", 0);
  return out;
};

const defaultShouldSkipDirFull = (relDir: string): boolean => {
  const key = normalizeVirtualPathKey(relDir);
  // Avoid scanning the biggest asset folders by default. Can be relaxed later.
  return (
    key.startsWith("textures") ||
    key.startsWith("meshes") ||
    key.startsWith("sound") ||
    key.startsWith("music") ||
    key.startsWith("video")
  );
};

export const buildVfsIndex = async (params: {
  profile: ProfileSnapshot;
  scope: VfsScope;
  maxFilesPerMod?: number;
  maxTotalFiles?: number;
  maxMs?: number;
}): Promise<VfsIndex> => {
  const startedAt = Date.now();
  const coverageStarted = nowIso();

  const maxFilesPerMod = params.maxFilesPerMod ?? (params.scope === "full" ? 50000 : 6000);
  const maxTotalFiles = params.maxTotalFiles ?? (params.scope === "full" ? 400000 : 80000);
  const maxMs = params.maxMs ?? (params.scope === "full" ? 60_000 : 20_000);

  const winnerByPath = new Map<string, string>(); // virtualPathKey -> winnerModId/__overwrite__
  const edgeCounts: VfsIndex["edgeCounts"] = {};
  const edgeSamples: VfsIndex["edgeSamples"] = {};
  const perModSignature: VfsIndex["perModSignature"] = {};
  const pluginFileProviders = new Map<string, string[]>(); // pluginLower -> modIds in priority order
  const categoriesScanned = new Set<VfsCategory>();
  const categoryStats: VfsIndex["categoryStats"] = {};

  const overwriteSummary: VfsIndex["overwriteSummary"] = {
    nonEmpty: false,
    countsByCategory: {},
  };

  let scannedMods = 0;
  let scannedFiles = 0;
  let partial = false;
  let stopReason: string | undefined;

  const timeBudgetExceeded = (): boolean => Date.now() - startedAt > maxMs;

  const incSignature = (modId: string, cat: VfsCategory) => {
    const sig = perModSignature[modId] ?? emptySignature();
    sig.counts[cat] = (sig.counts[cat] ?? 0) + 1;
    if (cat === "scripts") sig.flags.hasScripts = true;
    if (cat === "interface") sig.flags.hasInterfaceSwf = true;
    if (cat === "skse_dll") sig.flags.hasSkseDll = true;
    if (cat === "bsa") sig.flags.hasBsa = true;
    perModSignature[modId] = sig;
  };

  const incCategoryTotal = (cat: VfsCategory) => {
    const stat = categoryStats[cat] ?? { totalPaths: 0, collisions: 0 };
    stat.totalPaths += 1;
    categoryStats[cat] = stat;
  };

  const incCategoryCollision = (cat: VfsCategory) => {
    const stat = categoryStats[cat] ?? { totalPaths: 0, collisions: 0 };
    stat.collisions += 1;
    categoryStats[cat] = stat;
  };

  const incEdge = (winner: string, loser: string, cat: VfsCategory, samplePath: string) => {
    const edge = incNested(edgeCounts, winner, loser, emptyEdge);
    edge.total += 1;
    edge.byCategory[cat] = (edge.byCategory[cat] ?? 0) + 1;

    const samples = incNested(edgeSamples, winner, loser, () => []);
    capPush(samples, samplePath, 20);
  };

  const recordProvider = (pluginName: string, modId: string) => {
    const key = pluginName.toLowerCase();
    const existing = pluginFileProviders.get(key) ?? [];
    if (!existing.includes(modId)) {
      existing.push(modId);
      pluginFileProviders.set(key, existing);
    }
  };

  const scanMod = async (mod: ModInfo): Promise<void> => {
    if (!mod.enabled) return;
    scannedMods += 1;

    const cached = await readCachedModScan(params.profile.profileId, mod, params.scope);
    let rootsFiles: Array<{ root: string; files: string[]; mtimeMs: number | null }> = [];
    let pluginsAtRoot: string[] = [];
    let bsasAtRoot: string[] = [];
    let signature: ModFileSignature = emptySignature();

    if (cached) {
      rootsFiles = (cached.roots ?? []).map((r) => ({ root: r.root, files: r.files ?? [], mtimeMs: r.mtimeMs }));
      pluginsAtRoot = cached.pluginsAtRoot ?? [];
      bsasAtRoot = cached.bsasAtRoot ?? [];
      signature = cached.signature ?? emptySignature();
    } else {
      const roots = hotspotRootsForScope(params.scope);

      const rootFiles = await listRootFiles(mod.path);
      pluginsAtRoot = rootFiles.filter((f) => /\.(esm|esp|esl)$/i.test(f));
      bsasAtRoot = rootFiles.filter((f) => /\.bsa$/i.test(f));

      rootsFiles = [];
      for (const root of roots) {
        const absRoot = root ? path.join(mod.path, root) : mod.path;
        const mtimeMs = await statMtimeMs(absRoot);

        // In hotspots/extended, we only walk within defined roots.
        const maxDepth = params.scope === "hotspots" ? 8 : params.scope === "extended" ? 10 : 30;
        const shouldInclude = (combinedRel: string): boolean => {
          if (params.scope === "full" && shouldSkipFullScanPath(combinedRel)) return false;
          const cat = classifyVirtualPath(combinedRel);
          return cat != null;
        };
        const shouldSkipDir =
          params.scope === "full" ? (relDir: string) => defaultShouldSkipDirFull(relDir) : undefined;

        const files = await walkFiles(absRoot, root, {
          maxDepth,
          maxFiles: maxFilesPerMod,
          shouldInclude,
          shouldSkipDir,
        });
        rootsFiles.push({ root, files, mtimeMs });
      }

      // Build signature from collected files + root presence.
      signature = emptySignature();
      if (pluginsAtRoot.length) {
        signature.counts.plugins = (signature.counts.plugins ?? 0) + pluginsAtRoot.length;
      }
      for (const f of rootsFiles.flatMap((r) => r.files)) {
        const cat = classifyVirtualPath(f);
        if (!cat) continue;
        signature.counts[cat] = (signature.counts[cat] ?? 0) + 1;
        if (cat === "scripts") signature.flags.hasScripts = true;
        if (cat === "interface") signature.flags.hasInterfaceSwf = true;
        if (cat === "skse_dll") signature.flags.hasSkseDll = true;
      }
      if (bsasAtRoot.length) {
        signature.counts.bsa = (signature.counts.bsa ?? 0) + bsasAtRoot.length;
        signature.flags.hasBsa = true;
      }

      const payload: CachedModScan = {
        schemaVersion: VFS_INDEX_SCHEMA_VERSION,
        taxonomyVersion: VFS_TAXONOMY_VERSION,
        scope: params.scope,
        modId: mod.id,
        roots: rootsFiles.map((r) => ({ root: r.root, mtimeMs: r.mtimeMs, files: r.files })),
        signature,
        pluginsAtRoot,
        bsasAtRoot,
      };
      await writeCachedModScan(params.profile.profileId, mod.id, params.scope, payload);
    }

    // Apply signature.
    perModSignature[mod.id] = signature;

    // Plugin providers (root-level only, v1).
    pluginsAtRoot.forEach((p) => recordProvider(p, mod.id));

    // Winner + edge aggregation for all categorized files.
    for (const f of rootsFiles.flatMap((r) => r.files)) {
      if (scannedFiles >= maxTotalFiles) {
        partial = true;
        stopReason = "maxTotalFiles";
        return;
      }
      if (timeBudgetExceeded()) {
        partial = true;
        stopReason = "maxMs";
        return;
      }

      const cat = classifyVirtualPath(f);
      if (!cat) continue;
      categoriesScanned.add(cat);
      incCategoryTotal(cat);

      const key = normalizeVirtualPathKey(f);
      const prev = winnerByPath.get(key);
      if (prev && prev !== mod.id) {
        incCategoryCollision(cat);
        incEdge(mod.id, prev, cat, f);
      }
      winnerByPath.set(key, mod.id);
      scannedFiles += 1;
      incSignature(mod.id, cat);
    }
  };

  // Scan enabled mods in the order returned by modlist.txt (top→bottom). Since
  // we update winners as we go, the *later* mods win (MO2 semantics).
  for (const mod of params.profile.mods) {
    if (scannedFiles >= maxTotalFiles || timeBudgetExceeded()) break;
    await scanMod(mod);
    if (partial) break;
  }

  // Overwrite folder: last overlay.
  const overwriteDir = path.join(params.profile.mo2InstancePath, "overwrite");
  try {
    const st = await fs.stat(overwriteDir);
    if (st.isDirectory()) {
      const files = await walkFiles(overwriteDir, "", {
        maxDepth: 30,
        maxFiles: Math.min(maxFilesPerMod, 20000),
        shouldInclude: (relPath) => {
          if (shouldSkipFullScanPath(relPath)) return false;
          return classifyVirtualPath(relPath) != null;
        },
      });

      for (const f of files) {
        if (scannedFiles >= maxTotalFiles) {
          partial = true;
          stopReason = stopReason ?? "maxTotalFiles";
          break;
        }
        if (timeBudgetExceeded()) {
          partial = true;
          stopReason = stopReason ?? "maxMs";
          break;
        }

        const cat = classifyVirtualPath(f);
        if (!cat) continue;
        categoriesScanned.add(cat);
        incCategoryTotal(cat);
        overwriteSummary.nonEmpty = true;
        overwriteSummary.countsByCategory[cat] = (overwriteSummary.countsByCategory[cat] ?? 0) + 1;

        const key = normalizeVirtualPathKey(f);
        const prev = winnerByPath.get(key);
        if (prev && prev !== "__overwrite__") {
          incCategoryCollision(cat);
          incEdge("__overwrite__", prev, cat, f);
        }
        winnerByPath.set(key, "__overwrite__");
        scannedFiles += 1;
      }
    }
  } catch {
    // no overwrite dir; ignore
  }

  const durationMs = Date.now() - startedAt;

  // Runtime hotspot discovery: log unexpectedly dense categories (future: optional scoped expansion).
  Object.entries(categoryStats).forEach(([cat, stat]) => {
    const totalPaths = stat?.totalPaths ?? 0;
    const collisions = stat?.collisions ?? 0;
    if (!totalPaths) return;
    const ratio = collisions / totalPaths;
    if (ratio >= 0.1 || totalPaths >= 5000) {
      void logger.info(
        `[VFS] dense category=${cat} totalPaths=${totalPaths} collisions=${collisions} ratio=${ratio.toFixed(3)}`,
      );
    }
  });

  const index: VfsIndex = {
    schemaVersion: VFS_INDEX_SCHEMA_VERSION,
    taxonomyVersion: VFS_TAXONOMY_VERSION,
    scope: params.scope,
    categoriesScanned: Array.from(categoriesScanned.values()),
    categoryStats,
    perModSignature,
    pluginFileProviders: Object.fromEntries(pluginFileProviders.entries()),
    edgeCounts,
    edgeSamples,
    overwriteSummary,
    coverage: {
      startedAt: coverageStarted,
      durationMs,
      scannedMods,
      scannedFiles,
      partial,
      stopReason,
    },
  };

  await logger.info(
    `[VFS] scope=${params.scope} scannedMods=${scannedMods} scannedFiles=${scannedFiles} ` +
      `partial=${String(partial)} durationMs=${durationMs} overwriteNonEmpty=${String(overwriteSummary.nonEmpty)}`,
  );

  return index;
};

