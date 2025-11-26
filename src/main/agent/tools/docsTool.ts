import type { AgentTool } from "./types";
import { searchModDocs, type DocSnippet } from "../../rag/docsSearcher";

export const docsTool: AgentTool<{ query: string; k?: number }, DocSnippet[]> = {
  name: "search_mod_docs",
  description: "Retrieves documentation snippets from the local RAG index (mocked).",
  invoke: async ({ query, k }) => searchModDocs(query, { k }),
};
