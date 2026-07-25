import type { Issue, Recommendation } from "../../../shared/types";
import { KNOWN_ISSUE_CATEGORIES } from "../../../shared/types";
import type { OrchestratorInput } from "./types";

const normalizeString = (s: string): string =>
  s
    .toLowerCase()
    .trim()
    .replace(/[^\p{L}\p{N}]+/gu, " ")
    .replace(/\s+/g, " ")
    .trim();

const uniq = <T>(items: T[]): T[] => Array.from(new Set(items));

const knownCategories = new Set<string>(KNOWN_ISSUE_CATEGORIES as readonly string[]);

export const normalizeCategory = (raw: string | undefined | null): string => {
  const c = (raw ?? "").trim();
  if (!c) return "other";
  if (knownCategories.has(c)) return c;

  // Map some orchestrator/internal buckets into canonical groups for UI.
  const lower = c.toLowerCase();
  if (lower.includes("missing") && lower.includes("master")) return "missing_masters";
  if (lower.includes("incompat")) return "hard_incompatibility";
  if (lower.includes("load") && lower.includes("order")) return "load_order";
  if (lower.includes("script") || lower.includes("papyrus")) return "script_load";
  if (lower.includes("perf")) return "performance_risk";
  if (lower.includes("redundan")) return "redundancy";
  if (lower.includes("config")) return "configuration";
  if (lower.includes("overlap") || lower.includes("conflict")) return "soft_conflict";

  return "other";
};

const issueKey = (issue: Issue): string => {
  const cat = issue.categoryNormalized ?? normalizeCategory(issue.category);
  const mods = uniq((issue.affectedMods ?? []).map((m) => m.toLowerCase())).sort().join(",");
  const plugins = uniq((issue.affectedPlugins ?? []).map((p) => p.toLowerCase())).sort().join(",");
  const summary = normalizeString(issue.summary ?? "");
  return `${cat}::${summary}::${mods}::${plugins}`;
};

const chooseHigherSeverity = (a: Issue["severity"], b: Issue["severity"]): Issue["severity"] => {
  const order: Issue["severity"][] = ["critical", "high", "medium", "low", "suggestion"];
  return order.indexOf(a) <= order.indexOf(b) ? a : b;
};

const chooseHigherConfidence = (
  a: Issue["confidence"],
  b: Issue["confidence"],
): Issue["confidence"] => {
  const order: Issue["confidence"][] = ["high", "medium", "low"];
  return order.indexOf(a) <= order.indexOf(b) ? a : b;
};

const mergeIssue = (base: Issue, incoming: Issue): Issue => {
  const mergedSources = uniq([...(base.source ?? []), ...(incoming.source ?? [])]);
  const mergedMods = uniq([...(base.affectedMods ?? []), ...(incoming.affectedMods ?? [])]);
  const mergedPlugins = uniq([...(base.affectedPlugins ?? []), ...(incoming.affectedPlugins ?? [])]);

  const categoryNormalized = base.categoryNormalized ?? incoming.categoryNormalized ?? normalizeCategory(base.category);

  const evidenceRefs = uniq(
    [...(base.evidenceRefs ?? []), ...(incoming.evidenceRefs ?? [])].map((e) =>
      `${e.source}::${e.modId ?? ""}::${e.url ?? ""}::${e.snippet}`,
    ),
  ).slice(0, 20);

  const evidenceRefsExpanded = [
    ...(base.evidenceRefs ?? []),
    ...(incoming.evidenceRefs ?? []),
  ]
    .filter((e, idx, arr) => {
      const key = `${e.source}::${e.modId ?? ""}::${e.url ?? ""}::${e.snippet}`;
      return arr.findIndex((x) => `${x.source}::${x.modId ?? ""}::${x.url ?? ""}::${x.snippet}` === key) === idx;
    })
    .slice(0, 20);

  const supportLinks = [
    ...(base.supportLinks ?? []),
    ...(incoming.supportLinks ?? []),
  ]
    .filter((l, idx, arr) => arr.findIndex((x) => x.url === l.url && x.kind === l.kind) === idx)
    .slice(0, 12);

  const facets = [
    ...(base.facets ?? []),
    ...(incoming.facets ?? []),
  ]
    .filter(
      (f, idx, arr) =>
        arr.findIndex((x) => x.kind === f.kind && x.value === f.value && x.confidence === f.confidence) === idx,
    )
    .slice(0, 30);

  // Prefer the more informative details, but keep bounded.
  const baseDetails = (base.details ?? "").trim();
  const incomingDetails = (incoming.details ?? "").trim();
  const details =
    incomingDetails.length > baseDetails.length
      ? incomingDetails.slice(0, 4000)
      : baseDetails.slice(0, 4000);

  return {
    ...base,
    severity: chooseHigherSeverity(base.severity, incoming.severity),
    confidence: chooseHigherConfidence(base.confidence, incoming.confidence),
    risky: Boolean(base.risky || incoming.risky),
    categoryNormalized,
    affectedMods: mergedMods,
    affectedPlugins: mergedPlugins,
    source: mergedSources,
    details,
    evidenceRefs: evidenceRefsExpanded.length ? evidenceRefsExpanded : undefined,
    supportLinks: supportLinks.length ? supportLinks : undefined,
    facets: facets.length ? facets : undefined,
  };
};

const recommendationsKey = (rec: Recommendation): string => `${rec.issueId}::${rec.steps.join("|")}`;

export const stage4AssembleReport = async (input: OrchestratorInput, agentDelta: {
  issues: Issue[];
  recommendations: Recommendation[];
}): Promise<{
  issues: Issue[];
  recommendations: Recommendation[];
  mergedAgentIssues: number;
  issueIdRemap: Record<string, string>;
}> => {
  const issuesById = new Map<string, Issue>();
  const issuesByKey = new Map<string, string>(); // key -> id
  const issueIdRemap: Record<string, string> = {};

  // Seed with baseline issues (always keep).
  for (const base of input.offlineIssues) {
    const normalized = base.categoryNormalized ?? normalizeCategory(base.category);
    const seeded: Issue = normalized === base.categoryNormalized ? base : { ...base, categoryNormalized: normalized };
    issuesById.set(seeded.id, seeded);
    issuesByKey.set(issueKey(seeded), seeded.id);
  }

  let mergedAgentIssues = 0;

  // Add/merge agent issues.
  for (const agentIssue of agentDelta.issues) {
    const normalized = agentIssue.categoryNormalized ?? normalizeCategory(agentIssue.category);
    const normalizedAgent =
      normalized === agentIssue.categoryNormalized ? agentIssue : { ...agentIssue, categoryNormalized: normalized };
    const key = issueKey(normalizedAgent);

    const existingId = issuesByKey.get(key);
    if (existingId) {
      const existing = issuesById.get(existingId);
      if (existing) {
        issuesById.set(existingId, mergeIssue(existing, normalizedAgent));
        mergedAgentIssues += 1;
        issueIdRemap[normalizedAgent.id] = existingId;
      }
      continue;
    }

    issuesById.set(normalizedAgent.id, normalizedAgent);
    issuesByKey.set(key, normalizedAgent.id);
    issueIdRemap[normalizedAgent.id] = normalizedAgent.id;
  }

  // Recommendations: baseline + agent (dedupe).
  const recsByKey = new Map<string, Recommendation>();
  for (const r of input.offlineRecommendations) {
    recsByKey.set(recommendationsKey(r), r);
  }
  for (const r of agentDelta.recommendations) {
    const k = recommendationsKey(r);
    if (!recsByKey.has(k)) {
      recsByKey.set(k, r);
    }
  }

  return {
    issues: Array.from(issuesById.values()),
    recommendations: Array.from(recsByKey.values()),
    mergedAgentIssues,
    issueIdRemap,
  };
};


