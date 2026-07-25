import { ChatOpenAI } from "@langchain/openai";
import type { ChatOpenAICallOptions } from "@langchain/openai";
import type { Settings } from "../../shared/types";
import { logger } from "../logging";

export class OpenAIConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "OpenAIConfigError";
  }
}

export const isOpenAIConfigError = (error: unknown): error is OpenAIConfigError =>
  error instanceof OpenAIConfigError;

interface ModelOptions {
  settings: Settings;
  complexity?: number;
  opinionatedness?: number;
  callOptions?: ChatOpenAICallOptions;
  maxTokensOverride?: number;
  /**
   * Optional extra model kwargs to merge on top of the default JSON
   * response_format. This is kept separate from callOptions so we don't
   * rely on untyped properties on ChatOpenAICallOptions.
   */
  modelKwargs?: Record<string, unknown>;
}

/**
 * Creates a configured ChatOpenAI instance for Skyrim analysis and issue expansion.
 *
 * All OpenAI configuration stays in the main process and is driven off environment
 * variables plus analysis settings (complexity / opinionatedness).
 */
export const createSkyrimChatModel = (options: ModelOptions): ChatOpenAI => {
  const apiKey = process.env.OPENAI_API_KEY;

  if (!apiKey) {
    const message =
      "OPENAI_API_KEY is not set. Agentic analysis and AI issue expansion are disabled.";
    void logger.error(message);
    throw new OpenAIConfigError(message);
  }

  const { complexity = options.settings.analysisDefaults.complexity, opinionatedness } = options;

  // Keep temperature fairly low for deterministic, structured output,
  // but allow slightly more creativity at higher opinionatedness.
  const baseTemperature = 0.1;
  const extraTemperature = Math.min(Math.max((opinionatedness ?? 2) - 2, 0), 3) * 0.05;

  // Use complexity to scale how long / detailed responses can be.
  const normalizedComplexity = Math.min(Math.max(complexity, 1), 5);
  const defaultMaxTokens = 800 + (normalizedComplexity - 1) * 200;
  const maxTokens =
    typeof options.maxTokensOverride === "number" && Number.isFinite(options.maxTokensOverride)
      ? options.maxTokensOverride
      : defaultMaxTokens;

  const modelName = process.env.OPENAI_MODEL ?? "gpt-5.1";

  const mergedModelKwargs = {
    response_format: { type: "json_object" as const },
    ...(options.modelKwargs ?? {}),
  } satisfies Record<string, unknown>;

  // #region agent log
  fetch("http://127.0.0.1:7242/ingest/159f7078-f8e7-477a-832f-d9540d2560cb", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      location: "src/main/agent/openaiClient.ts:createSkyrimChatModel",
      message: "model_config",
      data: {
        model: modelName,
        maxTokens,
        temperature: baseTemperature + extraTemperature,
        responseFormatType: (mergedModelKwargs as Record<string, unknown>)?.["response_format"],
      },
      timestamp: Date.now(),
      sessionId: "debug-session",
      runId: "pre-fix",
      hypothesisId: "H4",
    }),
  }).catch(() => {});
  // #endregion

  return new ChatOpenAI({
    apiKey,
    model: modelName,
    temperature: baseTemperature + extraTemperature,
    maxTokens,
    ...options.callOptions,
    // Default to JSON mode so the model is constrained to return a single
    // valid JSON object. Callers can override/extend via `modelKwargs` in
    // ModelOptions if needed.
    modelKwargs: mergedModelKwargs,
  });
};
