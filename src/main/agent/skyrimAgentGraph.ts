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

const buildReasoningPrompts = (
  profile: ProfileSnapshot,
  offlineIssues: Issue[],
  offlineRecommendations: Recommendation[],
  flags: AgentFlags,
) => {
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
    "- get_loot_report: run or re-run a real LOOT report for this profile (missing masters, warnings, and load order details).",
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
    "Output requirements for this reasoning phase:",
    "- Do NOT output JSON.",
    "- Do NOT repeat or echo the full profile or offline analysis JSON.",
    "- Instead, produce a concise natural-language summary of the most important issues and recommendations you would make,",
    "  referring to mods and plugins by name/ID as needed.",
    "- This summary will later be converted into a strict JSON structure by a separate step.",
    "- If you find no new issues or recommendations beyond the offline analysis, clearly state that no additional issues",
    "  were found in this profile.",
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
    "Using the tools as needed, analyse this profile and the offline findings. Then produce a concise natural-language",
    "summary of any additional issues, insights, or recommendations you would add. Do NOT output JSON in this phase; just",
    "describe the findings in plain text.",
  ].join("\n");

  return { systemPrompt, userPrompt };
};

const formatSummaryToStructured = async (
  summary: string,
  settings: Settings,
  flags: AgentFlags,
): Promise<{ issues: Issue[]; recommendations: Recommendation[] }> => {
  const model = createSkyrimChatModel({
    settings,
    complexity: flags.complexity,
    opinionatedness: flags.opinionatedness,
  });

  const systemPrompt = [
    "You are a post-processing assistant that converts analysis summaries of Skyrim modlist and load order issues",
    "into a strict JSON structure.",
    "",
    "You are given:",
    "- A natural-language summary of issues and recommendations about a Skyrim SE/AE MO2 profile.",
    "",
    "Your task:",
    '- Convert that summary into a JSON object with exactly two keys: "issues" and "recommendations".',
    "- Each issue must conform to the Issue schema:",
    "  {",
    '    "id": string,',
    '    "severity": "critical" | "high" | "medium" | "low" | "suggestion",',
    '    "category": one of:',
    '      "missing_masters" | "hard_incompatibility" | "soft_conflict" | "outdated_or_wrong_version" |',
    '      "performance_risk" | "script_load" | "load_order" | "redundancy" | "aesthetic_suggestion" |',
    '      "configuration" | "other",',
    '    "subcategory"?: string,',
    '    "summary": string,',
    '    "details": string,',
    '    "affectedMods": string[],',
    '    "affectedPlugins": string[],',
    '    "risky": boolean,',
    '    "confidence": "high" | "medium" | "low",',
    '    "source": array including at least "agent" (you) and any other relevant sources like "loot" or "rules".',
    "  }",
    "",
    "- Each recommendation must conform to:",
    "  {",
    '    "issueId": string (must match an Issue.id),',
    '    "steps": string[],',
    '    "notes"?: string',
    "  }",
    "",
    "Important formatting rules:",
    "- Respond with ONLY the JSON object, no extra text, no backticks, no comments.",
    "- If there are no issues or recommendations beyond the offline analysis, you MUST respond with:",
    '  { "issues": [], "recommendations": [] }',
  ].join("\n");

  const userPrompt = [
    "Here is your analysis summary of the profile and offline findings:",
    "",
    summary,
    "",
    "Now produce the JSON object as specified above.",
  ].join("\n");

  const parseStructured = (
    raw: string,
  ): { issues: Issue[]; recommendations: Recommendation[] } | null => {
    try {
      const parsed = agentOutputSchema.safeParse(JSON.parse(raw));
      if (!parsed.success) {
        void logger.error(
          `Structured agent output failed schema validation: ${parsed.error.message}. Raw: ${raw}`,
        );
        return null;
      }

      const issues: Issue[] = parsed.data.issues.map((issue) => {
        const id = issue.id && issue.id.trim().length > 0 ? issue.id : `agent-${randomUUID()}`;
        const sourceSet = new Set(issue.source ?? []);
        sourceSet.add("agent");
        return { ...issue, id, source: Array.from(sourceSet) };
      });

      const recommendations: Recommendation[] = parsed.data.recommendations.map((rec) => ({
        issueId: rec.issueId,
        steps: rec.steps,
        notes: rec.notes,
      }));

      return { issues, recommendations };
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      void logger.error(
        `Structured agent output was not valid JSON: ${message}. Raw (truncated): ${raw.slice(0, 2000)}`,
      );
      return null;
    }
  };

  try {
    // First attempt: direct formatting
    const response = await model.invoke([
      new SystemMessage(systemPrompt),
      new HumanMessage(userPrompt),
    ]);
    const raw = messageContentToString(response).trim();

    const first = parseStructured(raw);
    if (first) {
      return first;
    }

    // Repair attempt: ask the model to rewrite its previous answer into valid JSON.
    const repairPrompt = [
      "You previously attempted to answer but did not follow the JSON-only instructions.",
      "Here is your previous answer:",
      "",
      raw,
      "",
      'Now respond ONLY with a valid JSON object with keys "issues" and "recommendations" as specified earlier.',
    ].join("\n");

    const repairResponse = await model.invoke([
      new SystemMessage(systemPrompt),
      new HumanMessage(repairPrompt),
    ]);
    const rawRepair = messageContentToString(repairResponse).trim();
    const repaired = parseStructured(rawRepair);
    if (repaired) {
      return repaired;
    }

    await logger.error(
      "Structured agent formatting failed after repair attempt; falling back to offline-only results.",
    );
    return { issues: [], recommendations: [] };
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    await logger.error(
      `Structured agent formatting step threw: ${message}. Falling back to offline-only results.`,
    );
    return { issues: [], recommendations: [] };
  }
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

  // Phase 1: reasoning + tools, free-form text (no JSON mode).
  const reasoningModel = createSkyrimChatModel({
    settings,
    complexity: flags.complexity,
    opinionatedness: flags.opinionatedness,
    // Override default JSON mode: we want natural-language reasoning here.
    modelKwargs: {
      response_format: { type: "text" },
    },
  });

  type AgentInvokeResult = {
    messages?: BaseMessage[];
  };

  // Narrow the untyped agent to the minimal invoke surface we rely on.
  const agent = createReactAgent({
    llm: reasoningModel,
    tools,
  }) as unknown as {
    invoke: (
      input: { messages: BaseMessage[] },
      config?: { streamMode?: "values" },
    ) => Promise<AgentInvokeResult>;
  };

  const { systemPrompt, userPrompt } = buildReasoningPrompts(
    profile,
    offlineIssues,
    offlineRecommendations,
    flags,
  );

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

  // Prefer the last message, but fall back to the last non-empty content
  // message if the final one is empty (some models can occasionally emit an
  // empty final message after tool calls).
  let contentMessage: BaseMessage | undefined = messages[messages.length - 1];
  let rawText = contentMessage ? messageContentToString(contentMessage).trim() : "";

  if (!rawText) {
    for (let i = messages.length - 2; i >= 0; i -= 1) {
      const candidateText = messageContentToString(messages[i]).trim();
      if (candidateText) {
        contentMessage = messages[i];
        rawText = candidateText;
        break;
      }
    }
  }

  if (!contentMessage || !rawText) {
    await logger.error(
      "Skyrim agent did not return any non-empty final message; falling back to offline results only.",
    );
    return { issues: [], recommendations: [] };
  }

  const summaryForLog = truncate(rawText, 4000);
  const summaryForFormatting = truncate(rawText, 8000);

  await logger.debug(
    `Skyrim agent completed reasoning phase; totalMessages=${messages.length}, ` +
      `summaryLength=${rawText.length}. Summary preview:\n${summaryForLog}`,
  );

  // Phase 2: formatting – convert the natural-language summary into strict JSON.
  return formatSummaryToStructured(summaryForFormatting, settings, flags);
};
