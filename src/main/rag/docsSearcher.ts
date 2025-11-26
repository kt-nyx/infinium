export interface DocSnippet {
  text: string;
  sourceUrl: string;
  sourceTitle?: string;
}

export const searchModDocs = async (
  query: string,
  opts?: { k?: number },
): Promise<DocSnippet[]> => {
  const k = opts?.k ?? 3;
  // TODO: connect to local SQLite + embeddings index.
  return Array.from({ length: k }).map((_, idx) => ({
    text: `Mocked documentation snippet #${idx + 1} for query: ${query}`,
    sourceUrl: "https://stepmodifications.org/wiki/SkyrimSE:Guide",
    sourceTitle: "STEP Mock Reference",
  }));
};
