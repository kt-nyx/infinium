import { randomUUID } from "node:crypto";
import type { Issue, ProfileSnapshot, Recommendation, Settings } from "../../shared/types";
import { lootTool } from "./tools/lootTool";
import { docsTool } from "./tools/docsTool";
import { rulesTool } from "./tools/rulesTool";
import { createNexusTool } from "./tools/nexusTool";

interface AgentFlags {
  useLoot: boolean;
  useNexus: boolean;
  useRag: boolean;
  complexity: number;
  opinionatedness: number;
}

interface AgentInput {
  profile: ProfileSnapshot;
  offlineIssues: Issue[];
  offlineRecommendations: Recommendation[];
  settings: Settings;
  flags: AgentFlags;
}

export const runSkyrimAgent = async (
  input: AgentInput,
): Promise<{ issues: Issue[]; recommendations: Recommendation[] }> => {
  // TODO: replace with LangGraph executor once tool schemas are finalized.
  const { profile, flags, settings } = input;
  const collectedIssues: Issue[] = [];
  const collectedRecommendations: Recommendation[] = [];

  if (flags.useLoot && !profile.lootAvailable) {
    await lootTool.invoke(profile);
  }

  if (flags.useNexus && settings.nexusApiKey) {
    const nexusTool = createNexusTool(settings);
    await nexusTool.invoke(12345);
  }

  const docSnippets = flags.useRag
    ? await docsTool.invoke({
        query: `stability tips for ${profile.profileId}`,
        k: flags.complexity + 1,
      })
    : [];
  if (flags.useRag && docSnippets.length) {
    const issue: Issue = {
      id: `agent-doc-${randomUUID()}`,
      severity: "low",
      category: "configuration",
      summary: "Documentation insights available",
      details: docSnippets[0]?.text ?? "No snippet",
      affectedMods: [],
      affectedPlugins: [],
      risky: false,
      confidence: "medium",
      source: ["rag", "agent"],
    };
    collectedIssues.push(issue);
    collectedRecommendations.push({
      issueId: issue.id,
      steps: ["Review documentation snippets for targeted fixes."],
      notes: docSnippets[0]?.sourceUrl,
    });
  }

  if (flags.opinionatedness >= 2) {
    const issue: Issue = {
      id: `agent-opinion-${randomUUID()}`,
      severity: flags.opinionatedness >= 4 ? "medium" : "low",
      category: "redundancy",
      summary: "Agent recommends consolidating visual mods",
      details:
        "Multiple texture/lighting tweaks detected. Consolidating them can reduce load order complexity and improve stability.",
      affectedMods: profile.mods.slice(0, 2).map((mod) => mod.id),
      affectedPlugins: profile.pluginLoadOrder.slice(0, 2),
      risky: false,
      confidence: "low",
      source: ["agent"],
    };
    collectedIssues.push(issue);
    collectedRecommendations.push({
      issueId: issue.id,
      steps: [
        "Decide on a preferred lighting/texture pack.",
        "Disable redundant aesthetic mods to free plugin slots.",
      ],
    });
  }

  if (flags.complexity >= 3) {
    const rulesInsight = await rulesTool.invoke({ profile });
    collectedIssues.push(
      ...rulesInsight.issues.map((issue) => ({
        ...issue,
        id: `agent-rule-${randomUUID()}`,
        source: [...new Set([...(issue.source ?? []), "agent"])],
      })),
    );
    collectedRecommendations.push(...rulesInsight.recommendations);
  }

  return { issues: collectedIssues, recommendations: collectedRecommendations };
};
