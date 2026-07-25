import { z } from "zod";
import type { ProfileSnapshot, Settings } from "../../../shared/types";
import type { AgentTool } from "./types";
import {
  NexusClient,
  type NexusCommentHit,
  type NexusCollectionBugReportSummary,
  type NexusCollectionSummary,
  type NexusFileContentsSummary,
  type NexusModFileInfo,
  type NexusThreadComment,
} from "../../nexus/nexusClient";

export type SearchNexusCommentsInput = {
  query: string;
  maxResults?: number;
};

export type CommentClassification = "symptom" | "solution" | "outdated" | "other";

export interface NexusCommentSummary {
  summary: string;
  totalComments: number;
  uniqueComments: number;
  byCategory: Record<CommentClassification, number>;
  importantCommentIds: string[];
}

export type SummarizeNexusCommentsInput = {
  comments: NexusCommentHit[];
  issueContext?: string;
  maxComments?: number;
};

export type GetNexusCommentThreadInput = {
  threadId: string;
  maxComments?: number;
};

export type GetNexusModFilesInput = {
  nexusId: number;
};

export type GetNexusModFileContentsSummaryInput = {
  nexusId: number;
  maxEntries?: number;
};

export type GetCollectionDetailsInput = {
  slug: string;
  domainName?: string;
};

export type GetCollectionBugReportsInput = {
  slug: string;
  domainName?: string;
  status?: string;
  maxResults?: number;
};

export const searchNexusCommentsSchema = z.object({
  query: z
    .string()
    .min(3)
    .describe(
      "Full-text search query for Nexus comments, such as an error message, crash code, or conflict description.",
    ),
  maxResults: z
    .number()
    .int()
    .min(1)
    .max(50)
    .optional()
    .describe("Maximum number of comment results to return (default 20)."),
});

export const getNexusCommentThreadSchema = z.object({
  threadId: z
    .string()
    .describe("Nexus comment thread ID to inspect for detailed discussion or bug report context."),
  maxComments: z
    .number()
    .int()
    .min(1)
    .max(100)
    .optional()
    .describe("Maximum number of comments to return from the thread (default 50)."),
});

export const getNexusModFilesSchema = z.object({
  nexusId: z
    .number()
    .int()
    .describe("Nexus Mods numeric ID for the mod whose files and versions you want to inspect."),
});

export const getNexusModFileContentsSummarySchema = z.object({
  nexusId: z
    .number()
    .int()
    .describe(
      "Nexus Mods numeric ID for the mod whose archive contents you want to summarize (scripts, textures, meshes, etc.).",
    ),
  maxEntries: z
    .number()
    .int()
    .min(50)
    .max(2000)
    .optional()
    .describe(
      "Maximum number of archive entries to sample when summarizing file contents (default 500).",
    ),
});

export const getCollectionDetailsSchema = z.object({
  slug: z
    .string()
    .describe(
      "Nexus Collection slug (the part of the URL identifying the collection) to fetch details for.",
    ),
  domainName: z
    .string()
    .optional()
    .describe(
      "Optional Nexus game domain (e.g. skyrimspecialedition). If omitted, the current profile's game domain is used.",
    ),
});

export const getCollectionBugReportsSchema = z.object({
  slug: z
    .string()
    .describe(
      "Nexus Collection slug whose bug reports you want to inspect for known issues and workarounds.",
    ),
  domainName: z
    .string()
    .optional()
    .describe(
      "Optional Nexus game domain (e.g. skyrimspecialedition). If omitted, the current profile's game domain is used.",
    ),
  status: z
    .string()
    .optional()
    .describe('Optional bug report status filter (e.g. "OPEN" or "CLOSED"). Defaults to "OPEN".'),
  maxResults: z
    .number()
    .int()
    .min(1)
    .max(50)
    .optional()
    .describe("Maximum number of bug reports to return (default 20)."),
});

export const summarizeNexusCommentsSchema = z.object({
  comments: z
    .array(
      z.object({
        id: z.string(),
        body: z.string(),
        createdAt: z.string(),
        creatorName: z.string(),
        threadId: z.string().optional(),
        relevance: z.number().optional(),
      }),
    )
    .min(1)
    .max(100)
    .describe(
      "Raw Nexus comment hits previously returned by search_nexus_comments or get_nexus_comment_thread.",
    ),
  issueContext: z
    .string()
    .optional()
    .describe(
      "Optional short description of the issue you are investigating (e.g. the CTD/error text or suspected conflict).",
    ),
  maxComments: z
    .number()
    .int()
    .min(5)
    .max(50)
    .optional()
    .describe(
      "Maximum number of comments to consider when summarising (defaults to 25 after de-duplication).",
    ),
});

const normalizeBody = (body: string): string =>
  body
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();

const classifyComment = (body: string): CommentClassification => {
  const text = body.toLowerCase();

  const hasCrashTerms =
    text.includes("ctd") ||
    text.includes("crash") ||
    text.includes("crashes") ||
    text.includes("freeze") ||
    text.includes("freezes") ||
    text.includes("exception") ||
    text.includes("error") ||
    text.includes("bug") ||
    text.includes("conflict");

  const hasSolutionTerms =
    text.includes("fixed by") ||
    text.includes("this fixed") ||
    text.includes("workaround") ||
    text.includes("solution") ||
    text.includes("resolved") ||
    text.includes("solve this") ||
    text.includes("you can fix") ||
    text.includes("what worked") ||
    text.includes("i fixed it by");

  const hasOutdatedTerms =
    text.includes("outdated") ||
    text.includes("no longer supported") ||
    text.includes("abandoned") ||
    text.includes("old version") ||
    text.includes("retired");

  if (hasSolutionTerms) return "solution";
  if (hasOutdatedTerms) return "outdated";
  if (hasCrashTerms) return "symptom";
  return "other";
};

const postProcessCommentHits = (
  hits: NexusCommentHit[],
  maxCount: number,
): { deduped: NexusCommentHit[]; classifications: Map<string, CommentClassification> } => {
  const seenBodies = new Set<string>();
  const deduped: NexusCommentHit[] = [];
  const classifications = new Map<string, CommentClassification>();

  const sorted = [...hits].sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  for (const hit of sorted) {
    const norm = normalizeBody(hit.body);
    if (!norm) continue;
    if (seenBodies.has(norm)) continue;
    seenBodies.add(norm);

    const classification = classifyComment(hit.body);
    classifications.set(hit.id, classification);

    deduped.push(hit);
    if (deduped.length >= maxCount) break;
  }

  return { deduped, classifications };
};

export const createSearchNexusCommentsTool = (
  profile: ProfileSnapshot,
  settings: Settings,
): AgentTool<SearchNexusCommentsInput, NexusCommentHit[]> => {
  const client = new NexusClient(settings, profile.game);
  return {
    name: "search_nexus_comments",
    description:
      "Performs a global search of Nexus Mods comments for a full-text query (typically an error message, crash code, or conflict description). " +
      "Use this mainly when you have a specific symptom string to investigate or when following up on known collection bug reports, not as the primary evidence source for a particular mod.",
    invoke: async (input) => {
      const { query, maxResults } = input;
      const raw = await client.searchComments({ query, limit: maxResults });
      const { deduped } = postProcessCommentHits(
        raw,
        typeof maxResults === "number" ? maxResults : 20,
      );
      return deduped;
    },
  };
};

export const createGetNexusCommentThreadTool = (
  profile: ProfileSnapshot,
  settings: Settings,
): AgentTool<GetNexusCommentThreadInput, NexusThreadComment[]> => {
  const client = new NexusClient(settings, profile.game);
  return {
    name: "get_nexus_comment_thread",
    description:
      "Fetches recent comments from a specific Nexus comment thread by ID. " +
      "Use this after discovering a relevant thread (for example from a collection bug report or a targeted comment search) when you need to read the detailed discussion.",
    invoke: async (input) => {
      const { threadId, maxComments } = input;
      return client.getCommentThread({ threadId, limit: maxComments });
    },
  };
};

export const createGetNexusModFilesTool = (
  profile: ProfileSnapshot,
  settings: Settings,
): AgentTool<GetNexusModFilesInput, NexusModFileInfo[]> => {
  const client = new NexusClient(settings, profile.game);
  return {
    name: "get_nexus_mod_files",
    description:
      "Lists Nexus mod files (MAIN, UPDATE, OPTIONAL, etc.) for a given numeric Nexus mod ID, including version, file category, description, and changelog text. " +
      "Use this when you suspect the wrong file version or optional file choice is causing issues.",
    invoke: async (input) => client.getModFiles(input.nexusId),
  };
};

export const createGetNexusModFileContentsSummaryTool = (
  profile: ProfileSnapshot,
  settings: Settings,
): AgentTool<GetNexusModFileContentsSummaryInput, NexusFileContentsSummary | null> => {
  const client = new NexusClient(settings, profile.game);
  return {
    name: "get_nexus_mod_file_contents_summary",
    description:
      "Summarizes what types of files a Nexus mod archive contains (scripts, meshes, textures, animations, etc.) based on archive paths, " +
      "using a sampled subset of entries. Use this when you need to reason about script load or conflict potential beyond simple heuristics.",
    invoke: async (input) =>
      client.getModFileContentsSummary({
        nexusId: input.nexusId,
        limit: input.maxEntries,
      }),
  };
};

export const createGetCollectionDetailsTool = (
  profile: ProfileSnapshot,
  settings: Settings,
): AgentTool<GetCollectionDetailsInput, NexusCollectionSummary | null> => {
  const client = new NexusClient(settings, profile.game);
  return {
    name: "get_collection_details",
    description:
      "Fetches basic details for a Nexus Collection by slug, including its name, description, and game domain. " +
      "Use this when the user explicitly mentions a collection or a mod description/comments reference a specific collection.",
    invoke: async (input) => client.getCollection(input.slug, input.domainName),
  };
};

export const createGetCollectionBugReportsTool = (
  profile: ProfileSnapshot,
  settings: Settings,
): AgentTool<GetCollectionBugReportsInput, NexusCollectionBugReportSummary[]> => {
  const client = new NexusClient(settings, profile.game);
  return {
    name: "get_collection_bug_reports",
    description:
      "Retrieves bug reports for a Nexus Collection by slug, including titles, summaries, status, and associated comment thread IDs. " +
      "Use this when analyzing issues for users of a known collection to see if their symptom matches a known open bug.",
    invoke: async (input) =>
      client.getCollectionBugReports({
        slug: input.slug,
        domainName: input.domainName,
        status: input.status,
        limit: input.maxResults,
      }),
  };
};

export const createSummarizeNexusCommentsTool = (): AgentTool<
  SummarizeNexusCommentsInput,
  NexusCommentSummary
> => ({
  name: "summarize_nexus_comments_for_issue",
  description:
    "Given a set of Nexus comment hits (for example from search_nexus_comments or get_nexus_comment_thread), " +
    "de-duplicates and classifies them into symptom reports vs proposed solutions/workarounds, then returns a short natural-language summary. " +
    "Use this to turn noisy comment threads into concise context, primarily for explicit error-string investigations or collection bug reports.",
  invoke: async (input) => {
    const { comments, issueContext, maxComments } = input;
    const limit = typeof maxComments === "number" ? maxComments : 25;

    const { deduped, classifications } = postProcessCommentHits(comments, limit);

    const counts: Record<CommentClassification, number> = {
      symptom: 0,
      solution: 0,
      outdated: 0,
      other: 0,
    };

    deduped.forEach((c) => {
      const cls = classifications.get(c.id) ?? "other";
      counts[cls] += 1;
    });

    const pickExamples = (cls: CommentClassification, max: number): NexusCommentHit[] =>
      deduped.filter((c) => (classifications.get(c.id) ?? "other") === cls).slice(0, max);

    const symptomExamples = pickExamples("symptom", 2);
    const solutionExamples = pickExamples("solution", 2);

    const pieces: string[] = [];

    const headerContext = issueContext?.trim()
      ? ` for the issue "${issueContext.trim()}"`
      : "";

    pieces.push(
      `Analysed ${deduped.length} unique Nexus comments${headerContext}. ` +
        `${counts.symptom} look like symptom reports, ${counts.solution} mention fixes or workarounds, ` +
        `${counts.outdated} talk about outdated/abandoned information, and ${counts.other} are other discussion or noise.`,
    );

    if (symptomExamples.length) {
      const snippets = symptomExamples.map((c) =>
        `"${c.body.slice(0, 160).replace(/\s+/g, " ").trim()}${c.body.length > 160 ? "…" : ""}"`,
      );
      pieces.push(
        `Users commonly report symptoms such as: ${snippets.join(
          " | ",
        )}. Treat these as user reports, not absolute facts.`,
      );
    }

    if (solutionExamples.length) {
      const snippets = solutionExamples.map((c) =>
        `"${c.body.slice(0, 160).replace(/\s+/g, " ").trim()}${c.body.length > 160 ? "…" : ""}"`,
      );
      pieces.push(
        `Suggested fixes or workarounds mentioned include: ${snippets.join(
          " | ",
        )}. These should be treated as medium-confidence suggestions unless also supported by changelogs or mod descriptions.`,
      );
    }

    if (!symptomExamples.length && !solutionExamples.length) {
      pieces.push(
        "No clear symptom or solution patterns emerged from these comments. They may be off-topic, very old, or too mixed to summarise confidently.",
      );
    }

    const importantCommentIds: string[] = [
      ...solutionExamples.map((c) => c.id),
      ...symptomExamples.map((c) => c.id),
    ];

    return {
      summary: pieces.join(" "),
      totalComments: input.comments.length,
      uniqueComments: deduped.length,
      byCategory: counts,
      importantCommentIds,
    };
  },
});




