import { z } from "zod";
import { DynamicStructuredTool } from "@langchain/core/tools";
import type { ProfileSnapshot, Settings } from "../../../shared/types";
import { logger } from "../../logging";
import type { AgentTool } from "./types";
import { lootTool } from "./lootTool";
import { docsTool } from "./docsTool";
import { rulesTool } from "./rulesTool";
import { createNexusTool } from "./nexusTool";

/**
 * Helper to adapt a simple AgentTool into a LangChain DynamicStructuredTool.
 * The caller supplies an input schema and an invocation function that can
 * close over any ambient state (profile snapshot, settings, etc.).
 */
const wrapStructuredTool = <I, O, TBaseInput>(
  base: AgentTool<TBaseInput, O>,
  schema: z.ZodTypeAny,
  func: (input: I) => Promise<O>,
): DynamicStructuredTool =>
  new DynamicStructuredTool({
    name: base.name,
    description: base.description,
    schema,
    func: async (input: I) => {
      const start = Date.now();
      const inputPreview = (() => {
        try {
          return JSON.stringify(input).slice(0, 500);
        } catch {
          return String(input);
        }
      })();

      await logger.debug(
        `[AgentTool:${base.name}] Invoked with input (truncated): ${inputPreview}`,
      );

      try {
        const output = await func(input);
        const duration = Date.now() - start;

        let outputPreview = "";
        try {
          outputPreview = JSON.stringify(output).slice(0, 500);
        } catch {
          outputPreview = String(output);
        }

        await logger.debug(
          `[AgentTool:${base.name}] Completed in ${duration}ms; output preview (truncated): ${outputPreview}`,
        );

        return output;
      } catch (error) {
        await logger.error(
          `[AgentTool:${base.name}] Failed: ${(error as Error).message ?? String(error)}`,
        );
        throw error;
      }
    },
  });

export const createLootLangChainTool = (profile: ProfileSnapshot) =>
  wrapStructuredTool(lootTool, z.object({}), async () => lootTool.invoke(profile));

export const createRulesLangChainTool = (profile: ProfileSnapshot) =>
  wrapStructuredTool(rulesTool, z.object({}), async () => rulesTool.invoke({ profile }));

export const createDocsLangChainTool = () =>
  wrapStructuredTool(
    docsTool,
    z.object({
      query: z
        .string()
        .describe(
          "Search query describing the mod, conflict, or issue you want more documentation about.",
        ),
      k: z
        .number()
        .int()
        .min(1)
        .max(10)
        .optional()
        .describe("Maximum number of documentation snippets to retrieve."),
    }),
    async (input: { query: string; k?: number }) => docsTool.invoke(input),
  );

export const createNexusLangChainTool = (settings: Settings) => {
  const nexus = createNexusTool(settings);
  return wrapStructuredTool(
    nexus,
    z.object({
      nexusId: z
        .number()
        .int()
        .describe("Nexus Mods numeric ID for the mod you want metadata for."),
    }),
    async (input: { nexusId: number }) => nexus.invoke(input.nexusId),
  );
};
