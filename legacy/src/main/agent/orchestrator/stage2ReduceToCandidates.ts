import { createHash } from "node:crypto";
import type { ModInfo } from "../../../shared/types";
import type {
  IssueCandidate,
  IssueCandidateScore,
  OrchestratorInput,
  OrchestratorRunContext,
  PerModDigestV2,
  SignalIndex,
} from "./types";
import type { VfsCategory } from "../../mo2/vfsTaxonomy";

const uniq = (items: string[]): string[] => Array.from(new Set(items));

const toKey = (s: string): string => s.toLowerCase();

const makeCandidateId = (kind: string, hypothesis: string, modIds: string[]): string => {
  const h = createHash("sha256")
    .update(JSON.stringify({ kind, hypothesis, modIds: [...modIds].sort() }))
    .digest("hex")
    .slice(0, 16);
  return `cand-${h}`;
};

const baseScore = (params: {
  severity: number;
  confidence: number;
  novelty: number;
  reasons: string[];
}): IssueCandidateScore => {
  const severity = Math.max(0, Math.min(1, params.severity));
  const confidence = Math.max(0, Math.min(1, params.confidence));
  const novelty = Math.max(0, Math.min(1, params.novelty));
  const total = Math.max(
    0,
    Math.min(1, 0.55 * severity + 0.35 * confidence + 0.1 * novelty),
  );
  return { severity, confidence, novelty, total, reasons: params.reasons };
};

const digestById = (digests: PerModDigestV2[]): Map<string, PerModDigestV2> =>
  new Map(digests.map((d) => [d.modId, d]));

const modById = (profileMods: ModInfo[]): Map<string, ModInfo> =>
  new Map(profileMods.map((m) => [m.id, m]));

const buildEvidenceRefs = (digests: PerModDigestV2[], max = 8) => {
  const out: Array<{ source: string; modId?: string; url?: string; snippet: string }> = [];
  for (const d of digests) {
    for (const s of (d.evidenceSnippets ?? []).slice(0, 3)) {
      out.push({ source: "digest", modId: d.modId, snippet: s });
      if (out.length >= max) return out;
    }
    for (const link of (d.supportLinks ?? []).slice(0, 1)) {
      out.push({ source: "supportLink", modId: d.modId, url: link.url, snippet: link.label ?? link.url });
      if (out.length >= max) return out;
    }
  }
  return out;
};

const candidateForOverlapTag = (tag: string, group: PerModDigestV2[]): IssueCandidate => {
  const affectedModIds = group.map((g) => g.modId);
  const affectedPlugins = uniq(group.flatMap((g) => g.plugins ?? []));
  const hypothesis = `Multiple mods appear to overlap on ${tag}`;

  const severity =
    tag.startsWith("system:") || tag.startsWith("visual:") ? 0.55 : tag.startsWith("ui:") ? 0.4 : 0.35;
  const confidence = group.some((g) => (g.facets ?? []).length) ? 0.55 : 0.35;
  const novelty = 0.5;
  const reasons = [`overlapTag:${tag}`, `mods:${group.length}`];

  const score = baseScore({ severity, confidence, novelty, reasons });

  const plan = group
    .map((g) => g.nexusId)
    .filter((id): id is number => typeof id === "number" && id > 0)
    .slice(0, 3)
    .flatMap((nexusId) => [
      { tool: "get_nexus_mod_files" as const, args: { nexusId } },
      { tool: "get_nexus_mod_file_contents_summary" as const, args: { nexusId, maxEntries: 500 } },
    ]);

  return {
    id: makeCandidateId("overlap", hypothesis, affectedModIds),
    kind: "overlap",
    hypothesis,
    systemsAffected: uniq([tag]),
    affectedModIds,
    affectedPlugins,
    evidenceRefs: buildEvidenceRefs(group),
    investigationPlan: plan,
    score,
  };
};

const candidateForMissingRequirement = (
  mod: PerModDigestV2,
  target: string,
  evidence: string,
): IssueCandidate => {
  const hypothesis = `Mod may be missing a required dependency/patch: ${target}`;
  const reasons = ["requirementsAgent", `target:${target}`];
  const score = baseScore({ severity: 0.6, confidence: 0.55, novelty: 0.35, reasons });

  const plan: IssueCandidate["investigationPlan"] = [];
  if (typeof mod.nexusId === "number" && mod.nexusId > 0) {
    plan.push({ tool: "get_nexus_mod_files", args: { nexusId: mod.nexusId } });
  }
  plan.push({ tool: "search_mod_docs", args: { query: `${mod.modName} requirements ${target}`, k: 3 } });

  return {
    id: makeCandidateId("missing_requirement", hypothesis, [mod.modId]),
    kind: "missing_requirement",
    hypothesis,
    systemsAffected: [],
    affectedModIds: [mod.modId],
    affectedPlugins: mod.plugins ?? [],
    evidenceRefs: [{ source: "requirementsAgent", modId: mod.modId, snippet: evidence }].slice(0, 5),
    investigationPlan: plan,
    score,
  };
};

const candidateForVariantMismatch = (mod: PerModDigestV2): IssueCandidate => {
  const hypothesis = "Possible wrong mod variant installed (SE/AE/module mismatch)";
  const reasons = ["variantAgent:mismatch"];
  const score = baseScore({ severity: 0.75, confidence: 0.6, novelty: 0.35, reasons });
  const plan: IssueCandidate["investigationPlan"] = [];
  if (typeof mod.nexusId === "number" && mod.nexusId > 0) {
    plan.push({ tool: "get_nexus_mod_files", args: { nexusId: mod.nexusId } });
    plan.push({ tool: "get_nexus_mod_file_contents_summary", args: { nexusId: mod.nexusId, maxEntries: 500 } });
  }
  return {
    id: makeCandidateId("variant_mismatch", hypothesis, [mod.modId]),
    kind: "variant_mismatch",
    hypothesis,
    systemsAffected: [],
    affectedModIds: [mod.modId],
    affectedPlugins: mod.plugins ?? [],
    evidenceRefs: [
      {
        source: "variantAgent",
        modId: mod.modId,
        snippet: mod.variantAgent?.evidence ?? "variantAgent mismatch",
      },
    ],
    investigationPlan: plan,
    score,
  };
};

const candidateForScriptPerf = (mod: PerModDigestV2): IssueCandidate => {
  const level = mod.scriptPerfRiskAgent?.level ?? "medium";
  const hypothesis = `Potential script/performance risk (${level})`;
  const reasons = [`scriptPerfRisk:${level}`];
  const severity = level === "high" ? 0.65 : level === "medium" ? 0.45 : 0.25;
  const confidence = mod.scriptPerfRiskAgent?.confidence === "high" ? 0.65 : 0.45;
  const score = baseScore({ severity, confidence, novelty: 0.25, reasons });
  const plan: IssueCandidate["investigationPlan"] = [];
  if (typeof mod.nexusId === "number" && mod.nexusId > 0) {
    plan.push({ tool: "get_nexus_mod_file_contents_summary", args: { nexusId: mod.nexusId, maxEntries: 500 } });
  }
  return {
    id: makeCandidateId("script_perf", hypothesis, [mod.modId]),
    kind: "script_perf",
    hypothesis,
    systemsAffected: [],
    affectedModIds: [mod.modId],
    affectedPlugins: mod.plugins ?? [],
    evidenceRefs: (mod.scriptPerfRiskAgent?.reasons ?? []).slice(0, 4).map((r) => ({
      source: "scriptPerfRiskAgent",
      modId: mod.modId,
      snippet: r,
    })),
    investigationPlan: plan,
    score,
  };
};

const candidateForFileConflict = (params: {
  kind: "file_conflict_interface" | "file_conflict_scripts" | "file_conflict_skse";
  winnerModId: string;
  loserModId: string;
  winnerName: string;
  loserName: string;
  category: VfsCategory;
  count: number;
  samplePaths: string[];
  useDocs: boolean;
}): IssueCandidate => {
  const hypothesis = `${params.winnerName} overwrites ${params.loserName} (${params.count} ${params.category} files)`;
  const severity =
    params.category === "skse_dll" ? 0.85 : params.category === "scripts" ? 0.7 : params.category === "interface" ? 0.55 : 0.4;
  const confidence = 0.7; // deterministic file-level evidence
  const novelty = 0.55;
  const reasons = [`vfsEdge:${params.category}`, `count:${params.count}`];
  const score = baseScore({ severity, confidence, novelty, reasons });

  const evidenceRefs: IssueCandidate["evidenceRefs"] = [
    { source: "vfsEdge", modId: params.winnerModId, snippet: `Overwrites ${params.loserModId}: ${params.count} ${params.category} files` },
    ...params.samplePaths.slice(0, 8).map((p) => ({ source: "vfsPath", snippet: p })),
  ];

  const investigationPlan: IssueCandidate["investigationPlan"] = params.useDocs
    ? [{ tool: "search_mod_docs", args: { query: `${params.winnerName} ${params.loserName} patch`, k: 3 } }]
    : [];

  return {
    id: makeCandidateId(params.kind, hypothesis, [params.winnerModId, params.loserModId]),
    kind: params.kind,
    hypothesis,
    systemsAffected: [`files:${params.category}`],
    affectedModIds: [params.winnerModId, params.loserModId],
    affectedPlugins: [],
    evidenceRefs,
    investigationPlan,
    score,
  };
};

const candidateForOverwriteNonEmpty = (params: {
  countsByCategory: Record<string, number>;
  topVictims: Array<{ modId: string; count: number }>;
}): IssueCandidate => {
  const hypothesis = "MO2 overwrite folder contains files that override your mod setup";
  const reasons = ["overwrite:nonempty"];
  const score = baseScore({ severity: 0.65, confidence: 0.75, novelty: 0.4, reasons });

  const evidenceRefs: IssueCandidate["evidenceRefs"] = [
    { source: "overwrite", snippet: `countsByCategory=${JSON.stringify(params.countsByCategory)}` },
    ...params.topVictims.slice(0, 5).map((v) => ({ source: "overwriteVictim", modId: v.modId, snippet: `Overrides ${v.modId}: ${v.count} files` })),
  ];

  return {
    id: makeCandidateId("overwrite_nonempty", hypothesis, params.topVictims.map((v) => v.modId).slice(0, 8)),
    kind: "overwrite_nonempty",
    hypothesis,
    systemsAffected: ["files:overwrite"],
    affectedModIds: params.topVictims.map((v) => v.modId).slice(0, 50),
    affectedPlugins: [],
    evidenceRefs,
    investigationPlan: [],
    score,
  };
};

const canonicalCategoryForKind = (kind: string): string => {
  if (kind === "overlap") return "soft_conflict";
  if (kind.startsWith("file_conflict_")) return "soft_conflict";
  if (kind === "missing_requirement") return "configuration";
  if (kind === "variant_mismatch") return "outdated_or_wrong_version";
  if (kind === "script_perf") return "script_load";
  if (kind === "overwrite_nonempty") return "configuration";
  return kind;
};

export const stage2ReduceToCandidates = async (
  input: OrchestratorInput,
  ctx: OrchestratorRunContext,
  signals: SignalIndex,
  digests: PerModDigestV2[],
): Promise<{ candidates: IssueCandidate[]; clustersBuilt: number }> => {
  const enabledModIds = new Set(input.profile.mods.filter((m) => m.enabled).map((m) => m.id.toLowerCase()));
  const enabledPluginLower = new Set((input.profile.pluginLoadOrder ?? []).map((p) => p.toLowerCase()));

  const candidates: IssueCandidate[] = [];

  // Cluster by tag/system.
  const bySystem = new Map<string, PerModDigestV2[]>();
  for (const d of digests) {
    for (const tag of (d.systemsAffected ?? []).slice(0, 25)) {
      const key = String(tag);
      const arr = bySystem.get(key) ?? [];
      arr.push(d);
      bySystem.set(key, arr);
    }
  }
  const overlapClusters = Array.from(bySystem.entries()).filter(([, ds]) => ds.length >= 2);
  overlapClusters.sort((a, b) => b[1].length - a[1].length);

  overlapClusters.slice(0, Math.min(120, ctx.budgets.maxCandidates)).forEach(([tag, group]) => {
    // Ignore extremely broad domain tags; focus on more specific overlap tags.
    if (tag.startsWith("domain:")) return;
    candidates.push(candidateForOverlapTag(tag, group));
  });

  // Requirements-based candidates (missing deps/patches).
  for (const d of digests) {
    for (const req of (d.requirementsAgent ?? []).slice(0, 12)) {
      if (req.kind !== "required" && req.kind !== "patch") continue;
      if (req.confidence === "low") continue;
      const target =
        req.targetModId ??
        req.targetPlugin ??
        req.targetModName ??
        "";
      if (!target) continue;

      if (req.targetPlugin) {
        if (!enabledPluginLower.has(req.targetPlugin.toLowerCase())) {
          candidates.push(candidateForMissingRequirement(d, target, req.evidence));
        }
        continue;
      }
      if (req.targetModId) {
        if (!enabledModIds.has(req.targetModId.toLowerCase())) {
          candidates.push(candidateForMissingRequirement(d, target, req.evidence));
        }
        continue;
      }
      // For name-only, treat as informational candidate (low confidence).
      candidates.push(
        {
          ...candidateForMissingRequirement(d, target, req.evidence),
          score: baseScore({
            severity: 0.35,
            confidence: 0.3,
            novelty: 0.25,
            reasons: ["requirementsAgent:nameOnly"],
          }),
        },
      );
    }
  }

  // Variant + script/perf candidates.
  for (const d of digests) {
    if (d.variantAgent?.mismatch) candidates.push(candidateForVariantMismatch(d));
    if (d.scriptPerfRiskAgent?.level === "high" || d.scriptPerfRiskAgent?.level === "medium") {
      candidates.push(candidateForScriptPerf(d));
    }
  }

  // Deterministic VFS-driven candidates (file conflicts + overwrite hygiene).
  if (input.vfs) {
    const vfs = input.vfs;
    const modsById = modById(input.profile.mods);
    const useDocs = Boolean(input.flags.useRag);

    // Overwrite non-empty is a classic high-signal MO2 hygiene issue.
    if (vfs.overwriteSummary?.nonEmpty) {
      const victims = Object.entries(vfs.edgeCounts?.["__overwrite__"] ?? {})
        .map(([modId, counts]) => ({ modId, count: counts.total ?? 0 }))
        .sort((a, b) => b.count - a.count)
        .slice(0, 10);

      candidates.push(
        candidateForOverwriteNonEmpty({
          countsByCategory: (vfs.overwriteSummary.countsByCategory ?? {}) as Record<string, number>,
          topVictims: victims,
        }),
      );
    }

    // File conflict edges: consider only high-risk categories and cap how many pairs we emit.
    const edgePairs: Array<{
      winner: string;
      loser: string;
      by: Partial<Record<VfsCategory, number>>;
      total: number;
      samples: string[];
    }> = [];

    Object.entries(vfs.edgeCounts ?? {}).forEach(([winner, losers]) => {
      if (!losers) return;
      if (winner === "__overwrite__") return; // handled above as separate hygiene candidate
      Object.entries(losers).forEach(([loser, counts]) => {
        const by = (counts.byCategory ?? {}) as Partial<Record<VfsCategory, number>>;
        const high = (by.skse_dll ?? 0) + (by.scripts ?? 0) + (by.interface ?? 0);
        if (high <= 0) return;
        const samples = (vfs.edgeSamples?.[winner]?.[loser] ?? []).slice(0, 12);
        edgePairs.push({ winner, loser, by, total: high, samples });
      });
    });

    edgePairs
      .sort((a, b) => b.total - a.total)
      .slice(0, Math.min(80, ctx.budgets.maxCandidates))
      .forEach((p) => {
        const winnerName = modsById.get(p.winner)?.name ?? p.winner;
        const loserName = modsById.get(p.loser)?.name ?? p.loser;

        const emit = (category: VfsCategory, kind: "file_conflict_interface" | "file_conflict_scripts" | "file_conflict_skse") => {
          const count = p.by[category] ?? 0;
          if (count <= 0) return;
          candidates.push(
            candidateForFileConflict({
              kind,
              winnerModId: p.winner,
              loserModId: p.loser,
              winnerName,
              loserName,
              category,
              count,
              samplePaths: p.samples,
              useDocs,
            }),
          );
        };

        emit("skse_dll", "file_conflict_skse");
        emit("scripts", "file_conflict_scripts");
        emit("interface", "file_conflict_interface");
      });
  }

  // De-dupe by id and cap.
  const byId = new Map<string, IssueCandidate>();
  for (const c of candidates) {
    const existing = byId.get(c.id);
    if (!existing || existing.score.total < c.score.total) {
      byId.set(c.id, c);
    }
  }

  const out = Array.from(byId.values())
    .sort((a, b) => b.score.total - a.score.total)
    .slice(0, ctx.budgets.maxCandidates);

  // Novelty bump for candidates not already represented in offline baseline categories.
  const baselineCategories = new Set(Object.keys(signals.baseline.categories ?? {}));
  out.forEach((c) => {
    const canonical = canonicalCategoryForKind(c.kind);
    if (!baselineCategories.has(canonical)) {
      c.score.novelty = Math.min(1, c.score.novelty + 0.15);
      c.score.total = Math.min(1, c.score.total + 0.03);
      c.score.reasons.push("novelty:baselineCategoryMissing");
    }
  });

  return { candidates: out, clustersBuilt: overlapClusters.length };
};





