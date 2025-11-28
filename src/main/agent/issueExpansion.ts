import type { Issue, ProfileSnapshot, Settings } from "../../shared/types";
import { logger } from "../logging";
import { createSkyrimChatModel } from "./openaiClient";

export interface IssueChatMessage {
  role: "user" | "assistant";
  content: string;
}

export interface IssueExpansionInput {
  issue: Issue;
  profile: ProfileSnapshot;
  settings: Settings;
  messages?: IssueChatMessage[];
}

const messageContentToString = (content: unknown): string => {
  if (typeof content === "string") return content;
  if (Array.isArray(content)) {
    return (content as unknown[])
      .map((part) => {
        if (typeof part === "string") return part;
        if (part && typeof part === "object" && "text" in part) {
          const text = (part as { text?: unknown }).text;
          if (typeof text === "string") return text;
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

export const expandIssueExplanation = async (input: IssueExpansionInput): Promise<string> => {
  const { issue, profile, settings, messages = [] } = input;

  const model = createSkyrimChatModel({
    settings,
    complexity: settings.analysisDefaults.complexity,
    opinionatedness: settings.analysisDefaults.opinionatedness,
    callOptions: {
      maxTokens: 600,
    },
  });

  const systemPrompt = [
    "You are a Skyrim SE/AE modding expert helping a user understand and troubleshoot a single analysis issue.",
    "The user is comfortable with basic modding concepts (mods, plugins, load order, MO2, LOOT)",
    "but is not a power user.",
    "",
    "You are participating in an interactive chat about this issue.",
    "",
    "For the given issue and profile context:",
    "- Briefly explain why this issue matters and what can go wrong if ignored.",
    "- Describe how the user can verify or reproduce the problem in MO2, LOOT, or in-game.",
    "- Provide concrete, step-by-step guidance to fix or mitigate the issue.",
    "- Tailor the level of detail to the analysis settings (complexity / opinionatedness).",
    "",
    "Output format:",
    "- Return a short markdown or plain-text reply.",
    "- You may use bullet points and short paragraphs.",
    "- Do NOT return JSON and do NOT wrap the answer in backticks.",
  ].join("\n");

  const relevantMods = profile.mods.filter((mod) => issue.affectedMods.includes(mod.id));

  const contextBlock = [
    "Issue (JSON):",
    JSON.stringify(issue, null, 2),
    "",
    "Profile context (only directly relevant mods and basic metadata):",
    JSON.stringify(
      {
        profileId: profile.profileId,
        game: profile.game,
        affectedMods: relevantMods,
        affectedPlugins: issue.affectedPlugins,
      },
      null,
      2,
    ),
    "",
    "Analysis settings:",
    JSON.stringify(settings.analysisDefaults, null, 2),
  ].join("\n");

  const chatMessages: { role: "system" | "user" | "assistant"; content: string }[] = [
    { role: "system", content: systemPrompt },
    {
      role: "user",
      content:
        "Here is the static context for this issue and profile. Use this for the whole conversation:\n\n" +
        contextBlock,
    },
  ];

  for (const msg of messages) {
    chatMessages.push({
      role: msg.role,
      content: msg.content,
    });
  }

  const response = await model.invoke(chatMessages);

  const text = messageContentToString(response.content);
  await logger.debug(`Issue chat reply generated for ${issue.id}`);
  return text.trim();
};
