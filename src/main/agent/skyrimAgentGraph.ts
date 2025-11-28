import { randomUUID } from "node:crypto";
import { z } from "zod";
import type { BaseMessage } from "@langchain/core/messages";
import { HumanMessage, SystemMessage } from "@langchain/core/messages";
import { createReactAgent } from "@langchain/langgraph/prebuilt";
import type { Issue, ProfileSnapshot, Recommendation, Settings } from "../../shared/types";
import { logger } from "../logging";
import { createSkyrimChatModel } from "./openaiClient";
import {
  createDocsLangChainTool,
  createLootLangChainTool,
  createNexusLangChainTool,
  createRulesLangChainTool,
} from "./tools/langchainTools";

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

const severitySchema = z.enum(["critical", "high", "medium", "low", "suggestion"]);

const issueCategorySchema = z.enum([
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
]);

const confidenceSchema = z.enum(["high", "medium", "low"]);

const issueSchema = z.object({
  id: z.string(),
  severity: severitySchema,
  category: issueCategorySchema,
  subcategory: z.string().optional(),
  summary: z.string(),
  details: z.string(),
  affectedMods: z.array(z.string()),
  affectedPlugins: z.array(z.string()),
  risky: z.boolean(),
  confidence: confidenceSchema,
  source: z.array(z.enum(["loot", "rules", "nexus", "rag", "agent"])).nonempty(),
});

const recommendationSchema = z.object({
  issueId: z.string(),
  steps: z.array(z.string()),
  notes: z.string().optional(),
});

const agentOutputSchema = z.object({
  issues: z.array(issueSchema),
  recommendations: z.array(recommendationSchema),
});

const messageContentToString = (message: BaseMessage): string => {
  const { content } = message;
  if (typeof content === "string") {
    return content;
  }

  if (Array.isArray(content)) {
    return content
      .map((part) => {
        if (typeof part === "string") return part;
        if ("text" in part && typeof (part as { text: unknown }).text === "string") {
          return (part as { text: string }).text;
        }
        return "";
      })
      .join("\n");
  }

  try {
    return JSON.stringify(content);
  } catch {
    return String(content);
  }
};

const extractJson = (raw: string): unknown => {
  const firstBrace = raw.indexOf("{");
  const lastBrace = raw.lastIndexOf("}");
  if (firstBrace === -1 || lastBrace === -1 || lastBrace <= firstBrace) {
    throw new Error("No JSON object found in agent output.");
  }

  const candidate = raw.slice(firstBrace, lastBrace + 1);
  return JSON.parse(candidate);
};

export const runSkyrimAgent = async (
  input: AgentInput,
): Promise<{ issues: Issue[]; recommendations: Recommendation[] }> => {
  const { profile, offlineIssues, offlineRecommendations, settings, flags } = input;

  await logger.debug(
    `Skyrim agent starting for profile=${profile.profileId} ` +
      `(mods=${profile.mods.length}, plugins=${profile.pluginLoadOrder.length}); ` +
      `flags=${JSON.stringify(flags)}`,
  );

  const tools = [];
  if (flags.useLoot) {
    tools.push(createLootLangChainTool(profile));
  }
  // Even if complexity is low, exposing rules to the agent lets it ask for
  // heuristic insights when needed.
  tools.push(createRulesLangChainTool(profile));
  if (flags.useRag) {
    tools.push(createDocsLangChainTool());
  }
  if (flags.useNexus && settings.nexusApiKey) {
    tools.push(createNexusLangChainTool(settings));
  }

  const model = createSkyrimChatModel({
    settings,
    complexity: flags.complexity,
    opinionatedness: flags.opinionatedness,
  });

  type AgentInvokeResult = {
    messages?: BaseMessage[];
  };

  // Narrow the untyped agent to the minimal invoke surface we rely on.
  const agent = createReactAgent({
    llm: model,
    tools,
  }) as unknown as {
    invoke: (
      input: { messages: BaseMessage[] },
      config?: { streamMode?: "values" },
    ) => Promise<AgentInvokeResult>;
  };

  const complexityDescription =
    flags.complexity <= 1
      ? "Focus only on the most severe, clearly unsafe issues and keep explanations very short."
      : flags.complexity >= 4
        ? "Consider subtle and less obvious issues as well, and provide detailed explanations."
        : "Balance between obvious high-impact issues and a few subtle findings. Moderate explanation length.";

  const opinionatednessDescription =
    flags.opinionatedness <= 1
      ? "Avoid opinionated suggestions. Focus only on stability, hard incompatibilities, and clear errors."
      : flags.opinionatedness >= 4
        ? "Be very opinionated: suggest removing redundant mods, replacing questionable mods, and curating for performance and aesthetics."
        : "Be somewhat opinionated: suggest some curation and quality-of-life improvements but prioritize stability.";

  const systemPrompt = [
    "You are a Skyrim SE/AE modding expert specializing in Mod Organizer 2 (MO2), LOOT, Nexus Mods, and STEP-style best practices.",
    "You are assisting a user by reviewing their MO2 profile and an existing offline analysis.",
    "",
    "Key domain objects:",
    "- ProfileSnapshot: describes the MO2 profile, including enabled mods, their paths, plugins, and plugin load order.",
    "- Issue: a single finding about the modlist or load order, with severity, category, risk, affected mods/plugins, and confidence.",
    "- Recommendation: concrete steps to address a specific issue.",
    "- offlineIssues / offlineRecommendations: baseline findings already produced by local heuristics and LOOT-style checks.",
    "",
    "Tools available to you:",
    "- get_loot_report: run or re-run a LOOT-style report for this profile (mocked but structurally correct).",
    "- get_known_issue_rules: query local heuristic rules for known problems and suggestions.",
    "- search_mod_docs: search local documentation / guides for additional context and troubleshooting steps.",
    "- get_nexus_metadata: look up metadata for a specific Nexus mod ID when Nexus configuration is available.",
    "",
    "When to use tools:",
    "- Use LOOT and rules tools when you need more details on load order, missing masters, or known incompatibilities.",
    "- Use documentation search when you need nuanced guidance or best practices for a particular mod or conflict.",
    "- Use Nexus metadata when you need to confirm mod versions, game support, or basic metadata.",
    "",
    "Your tasks:",
    "1) Identify issues in the modlist and load order, including missing masters, hard incompatibilities, soft conflicts, version mismatches, performance or script-load concerns, redundancies, and aesthetic/QoL opportunities.",
    "2) Explain issues in clear language suitable for someone who understands basic Skyrim modding but is not a power user.",
    "3) Provide concrete recommendations with step-by-step guidance to resolve or mitigate each issue.",
    "4) For every issue, set: severity, category, optional subcategory, confidence, risky (true if it can cause crashes / broken saves), and affectedMods/affectedPlugins using identifiers from the profile.",
    "5) Respect analysis settings:",
    `   - Complexity: ${complexityDescription}`,
    `   - Opinionatedness: ${opinionatednessDescription}`,
    "",
    "Behavior constraints:",
    "- Prefer using offlineIssues and offlineRecommendations as a strong baseline; you may refine or extend them.",
    "- Use tools when you need details beyond what is present in the offline findings and profile snapshot.",
    "- Do NOT invent mods, patches, or plugins that are not present in the profile, offline findings, or tool outputs.",
    "- Always reference mods and plugins using the IDs and names present in the profile snapshot.",
    "",
    "Final output format (IMPORTANT):",
    '- When you are done, respond with ONLY a single JSON object with keys "issues" and "recommendations".',
    "- Do not include any explanation outside of the JSON, no markdown, and no backticks.",
    "- The JSON must conform exactly to the following TypeScript interfaces (minus optional fields):",
    "  issues: Issue[]",
    "  recommendations: Recommendation[]",
    "",
    "If the offline baseline already captured an issue well, you may reuse its ID and add more detailed details/steps,",
    "or you may create a new issue with a new ID if the nature of the problem is different.",
  ].join("\n");

  const userPrompt = [
    "You are reviewing the following MO2 profile snapshot and offline baseline.",
    "",
    "ProfileSnapshot (JSON):",
    JSON.stringify(profile, null, 2),
    "",
    "Offline Issues (JSON):",
    JSON.stringify(offlineIssues, null, 2),
    "",
    "Offline Recommendations (JSON):",
    JSON.stringify(offlineRecommendations, null, 2),
    "",
    "Using the tools as needed, produce your final JSON-only response with keys `issues` and `recommendations`.",
  ].join("\n");

  const truncate = (text: string, max = 2000): string =>
    text.length <= max ? text : `${text.slice(0, max)}\n...[truncated]`;

  await logger.debug(
    `Skyrim agent system prompt (truncated):\n${truncate(systemPrompt)}\n` +
      `Skyrim agent user prompt summary: ` +
      `offlineIssues=${offlineIssues.length}, offlineRecommendations=${offlineRecommendations.length}, ` +
      `profileMods=${profile.mods.length}, profilePlugins=${profile.pluginLoadOrder.length}`,
  );

  await logger.debug(
    `Invoking Skyrim agent with ${tools.length} tools ` +
      `(loot=${flags.useLoot}, nexus=${flags.useNexus}, rag=${flags.useRag}), ` +
      `complexity=${flags.complexity}, opinionatedness=${flags.opinionatedness}`,
  );

  const inputMessages = [new SystemMessage(systemPrompt), new HumanMessage(userPrompt)];

  let messages: BaseMessage[] = [];

  try {
    const state = await agent.invoke({ messages: inputMessages });
    messages = state.messages ?? [];

    const lastForLog = messages[messages.length - 1];
    const lastPreview =
      lastForLog != null ? truncate(messageContentToString(lastForLog), 1000) : "<no message>";

    await logger.debug(
      `Skyrim agent invoke completed; totalMessages=${messages.length}, ` +
        `lastMessagePreview (truncated):\n${lastPreview}`,
    );
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    await logger.error(`Skyrim agent invoke failed: ${errorMessage}`);
    return { issues: [], recommendations: [] };
  }

  const lastMessage = messages[messages.length - 1];

  if (!lastMessage) {
    await logger.error("Skyrim agent did not return any messages.");
    return { issues: [], recommendations: [] };
  }

  const rawText = messageContentToString(lastMessage);

  const rawPreview = truncate(rawText, 4000);

  await logger.debug(
    `Skyrim agent completed; totalMessages=${messages.length}, ` +
      `lastMessageLength=${rawText.length}. Final message preview:\n${rawPreview}`,
  );

  try {
    const parsedJson = extractJson(rawText);
    const parsed = agentOutputSchema.safeParse(parsedJson);
    if (!parsed.success) {
      await logger.error(
        `Failed to parse agent JSON output: ${parsed.error.message}. Raw: ${rawText}`,
      );
      return { issues: [], recommendations: [] };
    }

    await logger.debug(
      `Skyrim agent JSON parsed successfully: issues=${parsed.data.issues.length}, ` +
        `recommendations=${parsed.data.recommendations.length}`,
    );

    const issues: Issue[] = parsed.data.issues.map((issue) => {
      const id = issue.id && issue.id.trim().length > 0 ? issue.id : `agent-${randomUUID()}`;
      const sourceSet = new Set(issue.source ?? []);
      sourceSet.add("agent");
      return {
        ...issue,
        id,
        source: Array.from(sourceSet),
      };
    });

    const recommendations: Recommendation[] = parsed.data.recommendations.map((rec) => ({
      issueId: rec.issueId,
      steps: rec.steps,
      notes: rec.notes,
    }));

    return { issues, recommendations };
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    await logger.error(
      `Error parsing or validating agent output as JSON: ${errorMessage}. Raw: ${rawText}`,
    );
    return { issues: [], recommendations: [] };
  }
};
