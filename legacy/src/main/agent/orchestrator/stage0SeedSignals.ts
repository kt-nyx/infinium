import type { Issue, ModInfo, ProfileSnapshot } from "../../../shared/types";
import type { OrchestratorInput, OrchestratorRunContext, SignalIndex } from "./types";
import type { VfsCategory } from "../../mo2/vfsTaxonomy";

const toLowerKey = (s: string): string => s.toLowerCase();

const unique = (items: string[]): string[] => Array.from(new Set(items));

const buildPluginToModIds = (input: OrchestratorInput): Record<string, string[]> => {
  // Prefer deterministic plugin providers from VFS scan if available.
  if (input.vfs?.pluginFileProviders) {
    const out: Record<string, string[]> = {};
    Object.entries(input.vfs.pluginFileProviders).forEach(([pluginLower, modIds]) => {
      out[pluginLower] = unique((modIds ?? []).map(String));
    });
    return out;
  }

  const pluginToModIds = new Map<string, Set<string>>();
  input.profile.mods.forEach((mod) => {
    (mod.plugins ?? []).forEach((pluginName) => {
      const key = toLowerKey(pluginName);
      const existing = pluginToModIds.get(key) ?? new Set<string>();
      existing.add(mod.id);
      pluginToModIds.set(key, existing);
    });
  });

  const out: Record<string, string[]> = {};
  pluginToModIds.forEach((set, key) => {
    out[key] = Array.from(set.values());
  });
  return out;
};

const summarizeBaseline = (offlineIssues: Issue[], offlineRecommendations: { issueId: string }[]) => {
  const categories: Record<string, number> = {};
  const affectedModIds: string[] = [];
  const affectedPlugins: string[] = [];

  for (const issue of offlineIssues) {
    categories[issue.category] = (categories[issue.category] ?? 0) + 1;
    if (issue.affectedMods?.length) affectedModIds.push(...issue.affectedMods);
    if (issue.affectedPlugins?.length) affectedPlugins.push(...issue.affectedPlugins);
  }

  return {
    issueCount: offlineIssues.length,
    recommendationCount: offlineRecommendations.length,
    categories,
    affectedModIds: unique(affectedModIds),
    affectedPlugins: unique(affectedPlugins),
  };
};

const scoreInterestingMod = (params: {
  mod: ModInfo;
  vfsSignals?: {
    signatureFlags?: { hasScripts?: boolean; hasInterfaceSwf?: boolean; hasSkseDll?: boolean; hasBsa?: boolean };
    outgoingByCat?: Partial<Record<VfsCategory, number>>;
    incomingByCat?: Partial<Record<VfsCategory, number>>;
    overwrittenByOverwrite?: number;
  };
}): { score: number; reasons: string[] } => {
  const mod = params.mod;
  let score = 0;
  const reasons: string[] = [];

  if (!mod.enabled) return { score: 0, reasons: [] };

  const add = (points: number, reason: string) => {
    score += points;
    reasons.push(reason);
  };

  if (mod.importanceBucket === "high") add(6, "importance:high");
  else if (mod.importanceBucket === "medium") add(3, "importance:medium");

  if (mod.stale === true) add(4, "stale:true");
  if (mod.scopeHint === "broad") add(3, "scope:broad");

  if (mod.categoryGroup === "framework_like") add(6, "categoryGroup:framework_like");
  if (mod.categoryGroup === "overhaul_like") add(5, "categoryGroup:overhaul_like");
  if (mod.categoryGroup === "content_like") add(2, "categoryGroup:content_like");
  if (mod.categoryGroup === "ui_like") add(2, "categoryGroup:ui_like");

  // Deterministic file-surface signals (from VFS scan).
  const flags = params.vfsSignals?.signatureFlags;
  if (flags?.hasSkseDll) add(6, "files:skse_dll");
  if (flags?.hasInterfaceSwf) add(4, "files:interface");
  if (flags?.hasScripts) add(4, "files:scripts");
  if (flags?.hasBsa) add(1, "files:bsa");

  const outgoing = params.vfsSignals?.outgoingByCat ?? {};
  const incoming = params.vfsSignals?.incomingByCat ?? {};
  const outHigh =
    (outgoing.skse_dll ?? 0) + (outgoing.scripts ?? 0) + (outgoing.interface ?? 0);
  const inHigh =
    (incoming.skse_dll ?? 0) + (incoming.scripts ?? 0) + (incoming.interface ?? 0);
  if (outHigh >= 10) add(3, `conflicts:outgoing:${outHigh}`);
  if (outHigh >= 50) add(3, "conflicts:manyOutgoing");
  if (inHigh >= 10) add(2, `conflicts:incoming:${inHigh}`);
  if (inHigh >= 50) add(2, "conflicts:manyIncoming");

  const overwrittenByOverwrite = params.vfsSignals?.overwrittenByOverwrite ?? 0;
  if (overwrittenByOverwrite >= 1) add(4, `overwriteOverrides:${overwrittenByOverwrite}`);

  const pluginCount = (mod.plugins ?? []).length;
  if (pluginCount >= 1) add(2, `plugins:${pluginCount}`);
  if (pluginCount >= 5) add(2, "manyPlugins");

  const overlapTags = mod.overlapTagsAgent ?? mod.overlapTags ?? [];
  if (overlapTags.length) add(2, `overlapTags:${overlapTags.length}`);

  if (mod.requirementsAgent?.length) add(3, "requirementsAgent");
  if (mod.loadOrderRulesAgent?.length) add(3, "loadOrderRulesAgent");
  if (mod.variantAgent?.mismatch) add(4, "variantMismatch");
  if (mod.scriptPerfRiskAgent?.level === "high") add(4, "scriptPerfRisk:high");
  else if (mod.scriptPerfRiskAgent?.level === "medium") add(2, "scriptPerfRisk:medium");

  if (typeof mod.nexusId === "number" && mod.nexusId > 0) add(1, "hasNexusId");
  if (mod.nexusCategory) add(1, "hasNexusCategory");

  // Score bump for explicit topics: suggests domain-level impact.
  if (mod.topics?.length) add(1, `topics:${mod.topics.length}`);

  return { score, reasons };
};

export const stage0SeedSignals = async (
  input: OrchestratorInput,
  _ctx: OrchestratorRunContext,
): Promise<SignalIndex> => {
  const pluginToModIds = buildPluginToModIds(input);
  const baseline = summarizeBaseline(input.offlineIssues, input.offlineRecommendations);

  // Precompute incoming/outgoing conflict totals per mod from VFS edges (bounded).
  const outgoingByMod: Record<string, Partial<Record<VfsCategory, number>>> = {};
  const incomingByMod: Record<string, Partial<Record<VfsCategory, number>>> = {};
  const overwriteVictimCounts: Record<string, number> = {};

  const edges = input.vfs?.edgeCounts ?? {};
  Object.entries(edges).forEach(([winner, losers]) => {
    Object.entries(losers ?? {}).forEach(([loser, counts]) => {
      const byCat = (counts?.byCategory ?? {}) as Partial<Record<VfsCategory, number>>;
      const outRow = outgoingByMod[winner] ?? {};
      const inRow = incomingByMod[loser] ?? {};
      (Object.entries(byCat) as Array<[VfsCategory, number]>).forEach(([cat, n]) => {
        if (!cat) return;
        outRow[cat] = (outRow[cat] ?? 0) + (n ?? 0);
        inRow[cat] = (inRow[cat] ?? 0) + (n ?? 0);
      });
      outgoingByMod[winner] = outRow;
      incomingByMod[loser] = inRow;

      if (winner === "__overwrite__") {
        overwriteVictimCounts[loser] = (overwriteVictimCounts[loser] ?? 0) + (counts?.total ?? 0);
      }
    });
  });

  const interesting = input.profile.mods
    .filter((m) => m.enabled)
    .map((m) => {
      const sig = input.vfs?.perModSignature?.[m.id];
      const { score, reasons } = scoreInterestingMod({
        mod: m,
        vfsSignals: {
          signatureFlags: sig?.flags,
          outgoingByCat: outgoingByMod[m.id],
          incomingByCat: incomingByMod[m.id],
          overwrittenByOverwrite: overwriteVictimCounts[m.id] ?? 0,
        },
      });
      return { id: m.id, name: m.name, score, reasons };
    })
    .filter((m) => m.score > 0)
    .sort((a, b) => b.score - a.score)
    .slice(0, 30);

  return {
    pluginToModIds,
    baseline,
    interestingMods: interesting,
  };
};





