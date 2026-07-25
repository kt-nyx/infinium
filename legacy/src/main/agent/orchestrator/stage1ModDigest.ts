import { createHash } from "node:crypto";
import { promises as fs } from "node:fs";
import path from "node:path";
import type { ModInfo } from "../../../shared/types";
import { logger } from "../../logging";
import { NexusClient } from "../../nexus/nexusClient";
import type { OrchestratorInput, OrchestratorRunContext, PerModDigestFacet, PerModDigestV2, SignalIndex } from "./types";
import { ensureDir, resolveDigestCacheDir } from "./storage";
import type { VfsCategory } from "../../mo2/vfsTaxonomy";

const DIGEST_SCHEMA_VERSION = 1;

const truncate = (text: string, max: number): string =>
  text.length <= max ? text : `${text.slice(0, max)}…`;

const cap = <T>(items: T[] | undefined | null, max: number): T[] =>
  Array.isArray(items) ? items.slice(0, max) : [];

const stableJson = (value: unknown): string => {
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
};

const digestKey = (
  mod: ModInfo,
  modelId: string,
  vfsKey?: {
    signatureCounts?: Record<string, number>;
    outgoingHigh?: number;
    incomingHigh?: number;
    overwriteVictim?: number;
  },
): string => {
  const fingerprint = {
    schema: DIGEST_SCHEMA_VERSION,
    modelId,
    id: mod.id,
    name: mod.name,
    enabled: mod.enabled,
    nexusId: mod.nexusId,
    installedVersion: mod.installedVersion,
    latestVersion: mod.latestVersion,
    nexusLastUpdated: mod.nexusLastUpdated,
    nexusStatus: mod.nexusStatus,
    stale: mod.stale,
    scopeHint: mod.scopeHint,
    categoryGroup: mod.categoryGroup,
    importanceScore: mod.importanceScore,
    plugins: mod.plugins ?? [],
    overlapTagsAgent: mod.overlapTagsAgent ?? [],
    overlapTags: mod.overlapTags ?? [],
    requirementsAgent: mod.requirementsAgent ?? [],
    loadOrderRulesAgent: mod.loadOrderRulesAgent ?? [],
    variantAgent: mod.variantAgent ?? null,
    scriptPerfRiskAgent: mod.scriptPerfRiskAgent ?? null,
    redundancyCandidatesAgent: mod.redundancyCandidatesAgent ?? [],
    vfs: vfsKey ?? null,
  };
  const h = createHash("sha256").update(stableJson(fingerprint)).digest("hex");
  return h;
};

const cachePathFor = (profileId: string, modId: string, key: string): string => {
  const base = resolveDigestCacheDir();
  return path.join(base, "digests", "v2", profileId, `${modId}-${key}.json`);
};

const readCached = async (
  profileId: string,
  mod: ModInfo,
  modelId: string,
  vfsKey?: Parameters<typeof digestKey>[2],
): Promise<PerModDigestV2 | null> => {
  const key = digestKey(mod, modelId, vfsKey);
  const filePath = cachePathFor(profileId, mod.id, key);
  try {
    const raw = await fs.readFile(filePath, "utf-8");
    const parsed = JSON.parse(raw) as PerModDigestV2;
    if (parsed?.schemaVersion !== DIGEST_SCHEMA_VERSION) return null;
    if (parsed?.modId !== mod.id) return null;
    return parsed;
  } catch {
    return null;
  }
};

const writeCached = async (
  profileId: string,
  mod: ModInfo,
  modelId: string,
  digest: PerModDigestV2,
  vfsKey?: Parameters<typeof digestKey>[2],
): Promise<void> => {
  const key = digestKey(mod, modelId, vfsKey);
  const filePath = cachePathFor(profileId, mod.id, key);
  await ensureDir(path.dirname(filePath));
  await fs.writeFile(filePath, JSON.stringify(digest, null, 2), "utf-8");
};

const deriveSystemsAffected = (mod: ModInfo): string[] => {
  const tags = (mod.overlapTagsAgent ?? mod.overlapTags ?? []).map(String);
  const domains = (mod.overlapDomains ?? []).map((d) => `domain:${d}`);
  return Array.from(new Set([...tags, ...domains])).slice(0, 30);
};

const deriveFacets = (params: {
  mod: ModInfo;
  vfsSignals?: {
    signatureCounts?: Partial<Record<VfsCategory, number>>;
    outgoingByCat?: Partial<Record<VfsCategory, number>>;
    incomingByCat?: Partial<Record<VfsCategory, number>>;
  };
}): PerModDigestFacet[] => {
  const facets: PerModDigestFacet[] = [];
  const mod = params.mod;
  const tags = mod.overlapTagsAgent ?? mod.overlapTags ?? [];
  for (const tag of cap(tags, 20)) {
    const [kindRaw, valueRaw] = String(tag).split(":");
    if (!kindRaw || !valueRaw) continue;
    facets.push({
      kind: kindRaw,
      value: valueRaw,
      confidence: mod.overlapTagsAgent?.length ? "medium" : "low",
      evidence: [],
    });
  }

  const sig = params.vfsSignals?.signatureCounts ?? {};
  const pushFileFacet = (value: string, evidence: string) => {
    facets.push({
      kind: "files",
      value,
      confidence: "high",
      evidence: [evidence],
    });
  };
  if ((sig.scripts ?? 0) > 0) pushFileFacet("scripts", `pex=${sig.scripts ?? 0}`);
  if ((sig.interface ?? 0) > 0) pushFileFacet("interface", `swf=${sig.interface ?? 0}`);
  if ((sig.skse_dll ?? 0) > 0) pushFileFacet("skse_dll", `dll=${sig.skse_dll ?? 0}`);
  if ((sig.plugins ?? 0) > 0) pushFileFacet("plugins", `plugins=${sig.plugins ?? 0}`);
  if ((sig.bsa ?? 0) > 0) pushFileFacet("bsa", `bsa=${sig.bsa ?? 0}`);

  const outgoing = params.vfsSignals?.outgoingByCat ?? {};
  const incoming = params.vfsSignals?.incomingByCat ?? {};
  const outHigh = (outgoing.skse_dll ?? 0) + (outgoing.scripts ?? 0) + (outgoing.interface ?? 0);
  const inHigh = (incoming.skse_dll ?? 0) + (incoming.scripts ?? 0) + (incoming.interface ?? 0);
  if (outHigh > 0) {
    facets.push({ kind: "conflicts", value: "outgoing", confidence: "medium", evidence: [`highRisk=${outHigh}`] });
  }
  if (inHigh > 0) {
    facets.push({ kind: "conflicts", value: "incoming", confidence: "medium", evidence: [`highRisk=${inHigh}`] });
  }

  return facets.slice(0, 25);
};

const baseSupportLinks = (mod: ModInfo): Array<{ kind: string; url: string; label?: string }> => {
  const links: Array<{ kind: string; url: string; label?: string }> = [];
  const bucket = (mod.metadata?.["nexus"] as Record<string, unknown> | undefined) ?? undefined;
  const url = (bucket?.["url"] as string | undefined) ?? undefined;
  if (url) {
    links.push({ kind: "nexus", url, label: "Nexus mod page" });
  }
  return links;
};

const evidenceSnippetsFromAgents = (mod: ModInfo, maxChars: number): string[] => {
  const snippets: string[] = [];
  const push = (label: string, raw: string | undefined) => {
    const text = (raw ?? "").trim();
    if (!text) return;
    snippets.push(truncate(`${label}: ${text}`, 300));
  };
  (mod.requirementsAgent ?? []).slice(0, 8).forEach((r) => push("requirement", r.evidence));
  (mod.loadOrderRulesAgent ?? []).slice(0, 8).forEach((r) => push("loadOrder", r.evidence));
  if (mod.variantAgent) push("variant", mod.variantAgent.evidence);
  if (mod.scriptPerfRiskAgent?.reasons?.length) {
    push("scriptPerf", mod.scriptPerfRiskAgent.reasons.join("; "));
  }
  const joined = snippets.join("\n");
  if (joined.length <= maxChars) return snippets;

  // If we exceeded cap, truncate list.
  const out: string[] = [];
  let used = 0;
  for (const s of snippets) {
    if (used + s.length + 1 > maxChars) break;
    out.push(s);
    used += s.length + 1;
  }
  return out;
};

const evidenceSnippetsFromVfs = (params: {
  modId: string;
  vfs?: OrchestratorInput["vfs"];
  max: number;
}): string[] => {
  const { vfs, modId } = params;
  if (!vfs) return [];

  const sig = vfs.perModSignature?.[modId];
  const snippets: string[] = [];
  if (sig?.flags?.hasSkseDll) snippets.push(`files:skse_dll present (dll=${sig.counts.skse_dll ?? 0})`);
  if (sig?.flags?.hasScripts) snippets.push(`files:scripts present (pex=${sig.counts.scripts ?? 0})`);
  if (sig?.flags?.hasInterfaceSwf) snippets.push(`files:interface present (swf=${sig.counts.interface ?? 0})`);
  if (sig?.flags?.hasBsa) snippets.push(`files:bsa present (bsa=${sig.counts.bsa ?? 0})`);

  // Summarize high-risk outgoing overwrites (top victims, bounded).
  const outgoing = vfs.edgeCounts?.[modId] ?? {};
  const victims = Object.entries(outgoing)
    .map(([loser, c]) => ({ loser, total: c.total ?? 0, by: c.byCategory ?? {} }))
    .sort((a, b) => b.total - a.total)
    .slice(0, 3);
  victims.forEach((v) => {
    const high = (v.by.skse_dll ?? 0) + (v.by.scripts ?? 0) + (v.by.interface ?? 0);
    if (high > 0) snippets.push(`conflicts: overrides ${v.loser} (highRiskFiles=${high})`);
  });

  // Overwrite folder overriding this mod.
  const overwriteVictim = vfs.edgeCounts?.["__overwrite__"]?.[modId]?.total ?? 0;
  if (overwriteVictim > 0) snippets.push(`overwrite: overrides this mod (${overwriteVictim} files)`);

  return snippets.slice(0, params.max);
};

const shouldNexusEnrich = (mod: ModInfo, input: OrchestratorInput): boolean => {
  if (!input.flags.useNexus) return false;
  if (!input.settings.nexusApiKey) return false;
  if (typeof mod.nexusId !== "number" || mod.nexusId <= 0) return false;
  if (input.flags.complexity < 4) return false;
  return (
    mod.importanceBucket === "high" ||
    mod.stale === true ||
    mod.scopeHint === "broad" ||
    mod.categoryGroup === "framework_like" ||
    mod.categoryGroup === "overhaul_like"
  );
};

const selectModsForDigest = (input: OrchestratorInput, signals: SignalIndex): ModInfo[] => {
  const enabled = input.profile.mods.filter((m) => m.enabled);

  const baselineSet = new Set(signals.baseline.affectedModIds);
  const interestingSet = new Set(signals.interestingMods.map((m) => m.id));

  // Always include baseline-affected + interesting. Then fill with remaining
  // enabled mods that ship plugins (surface area).
  const scored = enabled.map((m) => {
    let score = 0;
    if (baselineSet.has(m.id)) score += 1000;
    if (interestingSet.has(m.id)) score += 500;
    score += (m.plugins?.length ?? 0) * 5;
    if (m.importanceBucket === "high") score += 25;
    if (m.stale) score += 15;
    if (m.scopeHint === "broad") score += 10;
    if (m.categoryGroup === "framework_like") score += 10;
    if (m.categoryGroup === "overhaul_like") score += 8;

    // VFS-derived file-surface: prioritize SKSE DLLs, scripts, and UI surface.
    const sig = input.vfs?.perModSignature?.[m.id];
    if (sig?.flags?.hasSkseDll) score += 40;
    if (sig?.flags?.hasScripts) score += 18;
    if (sig?.flags?.hasInterfaceSwf) score += 14;
    if (sig?.flags?.hasBsa) score += 4;

    // VFS conflicts: mods that overwrite or are overwritten in hotspots are worth digesting.
    const outgoing = input.vfs?.edgeCounts?.[m.id] ?? {};
    const outgoingHigh = Object.values(outgoing).reduce((acc, c) => {
      const by = c.byCategory ?? {};
      return acc + (by.skse_dll ?? 0) + (by.scripts ?? 0) + (by.interface ?? 0);
    }, 0);
    if (outgoingHigh >= 10) score += 12;
    if (outgoingHigh >= 50) score += 10;

    const incomingFromOverwrite = input.vfs?.edgeCounts?.["__overwrite__"]?.[m.id]?.total ?? 0;
    if (incomingFromOverwrite >= 1) score += 20;
    return { mod: m, score };
  });

  return scored
    .sort((a, b) => b.score - a.score)
    .map((x) => x.mod);
};

export const stage1ModDigestMap = async (
  input: OrchestratorInput,
  ctx: OrchestratorRunContext,
  signals: SignalIndex,
): Promise<{ digests: PerModDigestV2[]; cacheHits: number; cacheMisses: number }> => {
  const modelId = process.env.OPENAI_MODEL ?? "unknown-model";

  const selected = selectModsForDigest(input, signals).slice(0, ctx.budgets.maxDigests);
  const digests: PerModDigestV2[] = [];
  let cacheHits = 0;
  let cacheMisses = 0;

  const nexus = input.settings.nexusApiKey ? new NexusClient(input.settings, input.profile.game) : null;

  const pluginsByModId = (() => {
    const out: Record<string, string[]> = {};
    const providers = input.vfs?.pluginFileProviders ?? {};
    Object.entries(providers).forEach(([pluginLower, modIds]) => {
      (modIds ?? []).forEach((modId) => {
        const arr = out[modId] ?? [];
        arr.push(pluginLower);
        out[modId] = arr;
      });
    });
    return out;
  })();

  for (const mod of selected) {
    const sigCounts = input.vfs?.perModSignature?.[mod.id]?.counts ?? undefined;
    const outgoingHigh = (() => {
      const row = input.vfs?.edgeCounts?.[mod.id];
      if (!row) return 0;
      return Object.values(row).reduce((acc, c) => {
        const by = c.byCategory ?? {};
        return acc + (by.skse_dll ?? 0) + (by.scripts ?? 0) + (by.interface ?? 0);
      }, 0);
    })();
    const incomingHigh = (() => {
      const edges = input.vfs?.edgeCounts ?? {};
      let acc = 0;
      Object.entries(edges).forEach(([, losers]) => {
        const c = (losers ?? {})[mod.id];
        if (!c) return;
        const by = c.byCategory ?? {};
        acc += (by.skse_dll ?? 0) + (by.scripts ?? 0) + (by.interface ?? 0);
      });
      return acc;
    })();
    const overwriteVictim = input.vfs?.edgeCounts?.["__overwrite__"]?.[mod.id]?.total ?? 0;
    const vfsKey = { signatureCounts: sigCounts as Record<string, number> | undefined, outgoingHigh, incomingHigh, overwriteVictim };

    const cached = await readCached(input.profile.profileId, mod, modelId, vfsKey);
    if (cached) {
      cacheHits += 1;
      digests.push(cached);
      continue;
    }
    cacheMisses += 1;

    const digest: PerModDigestV2 = {
      schemaVersion: DIGEST_SCHEMA_VERSION,
      modId: mod.id,
      modName: mod.name,
      enabled: mod.enabled,
      plugins: cap((pluginsByModId[mod.id] ?? mod.plugins) as string[], 200),
      nexusId: typeof mod.nexusId === "number" ? mod.nexusId : undefined,
      categoryGroup: mod.categoryGroup,
      scopeHint: mod.scopeHint,
      importanceBucket: mod.importanceBucket,
      stale: mod.stale,
      systemsAffected: deriveSystemsAffected(mod),
      facets: deriveFacets({
        mod,
        vfsSignals: {
          signatureCounts: input.vfs?.perModSignature?.[mod.id]?.counts,
          outgoingByCat: undefined,
          incomingByCat: undefined,
        },
      }),
      supportLinks: baseSupportLinks(mod),
      evidenceSnippets: [
        ...evidenceSnippetsFromVfs({ modId: mod.id, vfs: input.vfs, max: 8 }),
        ...evidenceSnippetsFromAgents(mod, Math.max(500, ctx.budgets.maxTextCharsPerDigest / 4)),
      ],
      requirementsAgent: mod.requirementsAgent,
      loadOrderRulesAgent: mod.loadOrderRulesAgent,
      variantAgent: mod.variantAgent,
      scriptPerfRiskAgent: mod.scriptPerfRiskAgent,
      redundancyCandidatesAgent: mod.redundancyCandidatesAgent,
    };

    // Optional targeted Nexus enrichment (bounded).
    if (
      nexus &&
      shouldNexusEnrich(mod, input) &&
      ctx.counters.toolCalls + 2 <= ctx.budgets.maxToolCalls
    ) {
      try {
        ctx.counters.toolCalls += 1;
        const files = await nexus.getModFiles(mod.nexusId as number);
        digest.nexusFilesPreview = files.slice(0, 10);

        if (input.flags.complexity >= 5 && ctx.counters.toolCalls + 1 <= ctx.budgets.maxToolCalls) {
          ctx.counters.toolCalls += 1;
          const contents = await nexus.getModFileContentsSummary({
            nexusId: mod.nexusId as number,
            limit: 500,
          });
          digest.nexusFileContentsSummary = contents ?? undefined;
        }
      } catch (e) {
        await logger.warn(
          `[Orchestrator][Stage1] Nexus enrichment failed for mod=${mod.id} nexusId=${String(mod.nexusId)}: ${
            (e as Error).message ?? String(e)
          }`,
        );
      }
    }

    // Final bounding: ensure evidence text doesn't blow up the cache file.
    const approxText = stableJson(digest);
    if (approxText.length > ctx.budgets.maxTextCharsPerDigest) {
      digest.evidenceSnippets = cap(digest.evidenceSnippets, 6);
      digest.nexusFilesPreview = undefined;
      digest.nexusFileContentsSummary = undefined;
    }

    await writeCached(input.profile.profileId, mod, modelId, digest, vfsKey);
    digests.push(digest);
  }

  await logger.info(
    `[Orchestrator][Stage1] digests=${digests.length} cacheHits=${cacheHits} cacheMisses=${cacheMisses} toolCalls=${ctx.counters.toolCalls}`,
  );

  return { digests, cacheHits, cacheMisses };
};





