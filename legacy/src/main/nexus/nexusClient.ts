import type { NexusModSearchResult, ProfileSnapshot, Settings } from "../../shared/types";
import { logger } from "../logging";

export interface NexusModMetadata {
  nexusId: number;
  name: string;
  summary: string;
  version: string;
  author?: string;
  game: string;
  tags: string[];
  lastUpdated?: string;
  category?: string;
  url?: string;
  /**
   * Optional Nexus UID and gameId for advanced lookups (e.g. modFiles).
   */
  uid?: string;
  gameId?: number;
  /**
   * Optional additional Nexus-derived fields that are useful for deeper
   * analysis but not required by existing callers.
   */
  description?: string;
  downloads?: number;
  endorsements?: number;
  status?: string;
  requirements?: Array<{
    modId: string;
    modName: string;
    url: string;
    notes?: string;
    externalRequirement: boolean;
  }>;
}

// Official Nexus Mods GraphQL endpoint. The REST API lives under
// https://api.nexusmods.com/, while GraphQL is exposed via the v2
// GraphQL route on the same host.
const NEXUS_GRAPHQL_ENDPOINT = "https://api.nexusmods.com/v2/graphql";

/**
 * Base class for all Nexus-related errors. Callers that want to treat Nexus as
 * an optional data source can catch this type and degrade gracefully without
 * failing the overall analysis.
 */
export class NexusError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "NexusError";
  }
}

/**
 * Thrown when the user's Nexus configuration is invalid (e.g., missing or
 * rejected API key).
 */
export class NexusConfigError extends NexusError {
  constructor(message: string) {
    super(message);
    this.name = "NexusConfigError";
  }
}

/**
 * Thrown when Nexus is temporarily unavailable for the remainder of this run,
 * for example after repeated rate limiting or server errors.
 */
export class NexusUnavailableError extends NexusError {
  constructor(message: string) {
    super(message);
    this.name = "NexusUnavailableError";
  }
}

/**
 * Thrown when Nexus cannot find a mod for the given (gameDomain, modId)
 * combination.
 */
export class NexusNotFoundError extends NexusError {
  constructor(message: string) {
    super(message);
    this.name = "NexusNotFoundError";
  }
}

interface GraphQLErrorShape {
  message: string;
  extensions?: {
    code?: string;
    [key: string]: unknown;
  };
}

interface GraphQLResponse<T> {
  data?: T;
  errors?: GraphQLErrorShape[];
}

export interface NexusCommentHit {
  id: string;
  body: string;
  createdAt: string;
  creatorName: string;
  threadId: string;
  relevance?: number;
}

export interface NexusThreadComment {
  id: string;
  body: string;
  createdAt: string;
  creatorName: string;
  parentId?: string;
}

export interface NexusModFileInfo {
  fileId: number;
  name: string;
  version: string;
  category: string;
  description?: string;
  changelogText: string[];
  date: number;
  primary: boolean;
  totalDownloads: number;
  uniqueDownloads: number;
  requirementsAlert: number;
  scannedStatus: string;
}

export interface NexusFileContentsSummary {
  modId: number;
  fileId?: number;
  totalEntries: number;
  byCategory: {
    scripts: number;
    meshes: number;
    textures: number;
    animations: number;
    other: number;
  };
  samplePaths: string[];
}

export interface NexusCollectionSummary {
  slug: string;
  name: string;
  description: string;
  gameDomain: string;
}

export interface NexusCollectionBugReportSummary {
  id: string;
  title: string;
  description?: string;
  status: string;
  createdAt: string;
  commentThreadId: string;
}

interface ModRequirementNode {
  modId: string;
  modName: string;
  url: string;
  notes?: string | null;
  externalRequirement: boolean;
}

interface ModNode {
  modId: number;
  uid: string;
  name: string;
  summary: string;
  description: string;
  version: string;
  author?: string | null;
  category: string;
  downloads: number;
  endorsements: number;
  status: string;
  updatedAt: string;
  game: {
    id: number;
    domainName: string;
  };
  modRequirements?: {
    nexusRequirements?: {
      nodes: ModRequirementNode[];
    };
  };
}

interface LegacyModsByDomainResult {
  legacyModsByDomain: {
    nodes: ModNode[];
  };
}

interface ModsSearchResult {
  mods: {
    nodes: Array<{
      modId: number;
      name: string;
      summary: string;
      status: string;
      updatedAt: string;
      downloads: number;
      endorsements: number;
      game: {
        domainName: string;
      };
    }> | null;
  };
}

type SkyrimGame = ProfileSnapshot["game"];

const LEGACY_MODS_BY_DOMAIN_QUERY = `
  query LegacyModsByDomain($ids: [CompositeDomainWithIdInput!]!, $count: Int) {
    legacyModsByDomain(ids: $ids, count: $count) {
      nodes {
        modId
        uid
        name
        summary
        description
        version
        author
        category
        downloads
        endorsements
        status
        updatedAt
        game {
          id
          domainName
        }
        modRequirements {
          nexusRequirements(offset: 0, count: 50) {
            nodes {
              modId
              modName
              url
              notes
              externalRequirement
            }
          }
        }
      }
    }
  }
`;

interface SearchCommentsResult {
  searchComments: {
    nodes: Array<{
      id: string;
      body: string;
      createdAt: string;
      creator: {
        name?: string | null;
        username?: string | null;
        displayName?: string | null;
      };
      // The thread id is indirectly available via the thread, but not all
      // clients may have permission to see the full thread object. We keep
      // this flexible by allowing an optional thread field.
      thread?: {
        id: string;
      };
    }> | null;
  };
}

interface CommentThreadResult {
  commentThread: {
    id: string;
    comments: {
      nodes: Array<{
        id: string;
        body: string;
        createdAt: string;
        creator: {
          name?: string | null;
          username?: string | null;
          displayName?: string | null;
        };
        parent?: {
          id: string;
        } | null;
      }> | null;
    };
  };
}

interface ModFilesByUidResult {
  modFilesByUid: {
    nodes: Array<{
      fileId: number;
      name: string;
      description?: string | null;
      changelogText: string[];
      version: string;
      date: number;
      category: string;
      primary: number;
      totalDownloads: number;
      uniqueDownloads: number;
      requirementsAlert: number;
      scannedV2: string;
    }> | null;
  };
}

interface ModFileContentsResult {
  modFileContents: {
    nodes: Array<{
      modId: number;
      fileId: number;
      filePath: string;
      fileExtension: string;
    }> | null;
    nodesCount: number;
  };
}

interface CollectionResult {
  collection: {
    name: string;
    description: string;
    game: {
      domainName: string;
    };
    slug: string;
  };
}

interface CollectionBugReportsResult {
  collection: {
    bugReports: {
      nodes: Array<{
        id: string;
        title: string;
        description?: string | null;
        status: string;
        createdAt: string;
        commentThread: {
          id: string;
        };
      }> | null;
    };
  };
}

const SEARCH_COMMENTS_QUERY = `
  query SearchComments($filter: CommentsSearchFilter!, $first: Int) {
    searchComments(filter: $filter, first: $first) {
      nodes {
        id
        body
        createdAt
        creator {
          name
          username
          displayName
        }
      }
    }
  }
`;

const COMMENT_THREAD_QUERY = `
  query CommentThread($id: ID!, $first: Int) {
    commentThread(commentThreadId: $id) {
      id
      comments(sortBy: "created_at", sortDirection: "DESC", first: $first) {
        nodes {
          id
          body
          createdAt
          creator {
            name
            username
            displayName
          }
        }
      }
    }
  }
`;

const MOD_FILES_BY_UID_QUERY = `
  query ModFilesByUid($uids: [ID!]!, $count: Int) {
    modFilesByUid(uids: $uids, count: $count) {
      nodes {
        fileId
        name
        description
        changelogText
        version
        date
        category
        primary
        totalDownloads
        uniqueDownloads
        requirementsAlert
        scannedV2
      }
    }
  }
`;

const MOD_FILE_CONTENTS_QUERY = `
  query ModFileContents($modId: Int!, $count: Int) {
    modFileContents(
      filter: {
        modId: [{ value: $modId, op: EQUALS }]
      }
      count: $count
    ) {
      nodes {
        modId
        fileId
        filePath
        fileExtension
      }
      nodesCount
    }
  }
`;

const COLLECTION_QUERY = `
  query Collection($slug: String!, $domainName: String!, $viewAdultContent: Boolean!) {
    collection(slug: $slug, domainName: $domainName, viewAdultContent: $viewAdultContent) {
      slug
      name
      description
      game {
        domainName
      }
    }
  }
`;

const COLLECTION_BUG_REPORTS_QUERY = `
  query CollectionBugReports(
    $slug: String!
    $domainName: String!
    $status: BugReportStatus!
    $first: Int
  ) {
    collection(slug: $slug, domainName: $domainName, viewAdultContent: true) {
      bugReports(status: $status, sortBy: "created_at", sortDirection: "DESC", first: $first) {
        nodes {
          id
          title
          description
          status
          createdAt
          commentThread {
            id
          }
        }
      }
    }
  }
`;

const PERSONAL_API_KEY_QUERY = `
  query PersonalApiKey {
    personalApiKey {
      id
    }
  }
`;

export class NexusClient {
  private static readonly MAX_RETRIES = 2;
  private static readonly MAX_ERROR_THRESHOLD = 3;

  private readonly cache = new Map<number, NexusModMetadata>();
  private readonly pending = new Map<number, Promise<NexusModMetadata>>();
  private consecutiveErrors = 0;
  private disabledForRun = false;

  constructor(
    private readonly settings: Settings,
    /**
     * Optional Skyrim game hint for this client. When provided, it is used to
     * map to the appropriate Nexus game domain when looking up mods by ID.
     *
     * If omitted, the client defaults to the Skyrim SE/AE domain, which is
     * appropriate for the majority of use cases in this app.
     */
    private readonly game?: SkyrimGame,
  ) {}

  private get apiKey(): string | undefined {
    return this.settings.nexusApiKey;
  }

  private get isDisabled(): boolean {
    return this.disabledForRun;
  }

  private resolveGameDomain(): string {
    switch (this.game) {
      case "SkyrimLE":
        return "skyrim";
      case "SkyrimSE":
      case "SkyrimAE":
      default:
        // For SE and AE, Nexus uses the shared "skyrimspecialedition" domain.
        return "skyrimspecialedition";
    }
  }

  private async backoff(attempt: number): Promise<void> {
    const baseMs = 250;
    const delayMs = baseMs * (attempt + 1);
    await new Promise<void>((resolve) => {
      setTimeout(() => resolve(), delayMs);
    });
  }

  private async graphqlRequest<T>(
    query: string,
    variables?: Record<string, unknown>,
  ): Promise<T> {
    if (!this.apiKey) {
      // Do not attempt any network request if there is no key configured.
      this.disabledForRun = true;
      throw new NexusConfigError("Nexus API key is not configured.");
    }

    if (this.isDisabled) {
      throw new NexusUnavailableError(
        "Nexus requests have been disabled for this run due to previous errors.",
      );
    }

    const body = JSON.stringify({ query, variables });
    const maxAttempts = 1 + NexusClient.MAX_RETRIES;
    let lastError: unknown;

    for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
      try {
        const response = await fetch(NEXUS_GRAPHQL_ENDPOINT, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Accept: "application/json",
            // Per official Nexus Mods GraphQL docs:
            // https://graphql.nexusmods.com/
            // Authentication is provided via the `apikey` header.
            apikey: this.apiKey as string,
          },
          body,
        });

        if (!response.ok) {
          const status = response.status;
          const text = await response.text().catch(() => "");
          const truncatedBody = text.slice(0, 500);

          await logger.warn(
            `[NexusClient] GraphQL HTTP error status=${status}; body (truncated): ${truncatedBody}`,
          );

          const shouldRetry = (status === 429 || status >= 500) && attempt < maxAttempts - 1;

          if (shouldRetry) {
            await this.backoff(attempt);
            // Try again.
            // eslint-disable-next-line no-continue
            continue;
          }

          this.consecutiveErrors += 1;
          if (this.consecutiveErrors >= NexusClient.MAX_ERROR_THRESHOLD) {
            this.disabledForRun = true;
          }

          if (status === 401 || status === 403) {
            throw new NexusConfigError(
              "Nexus API key was rejected or lacks required permissions. " +
                "Check your personal API key and try again.",
            );
          }

          if (status === 429) {
            throw new NexusUnavailableError(
              "Nexus Mods API rate limit exceeded. Please wait a bit before trying again.",
            );
          }

          if (status >= 500) {
            throw new NexusUnavailableError(
              "Nexus Mods API returned a server error. Please try again later.",
            );
          }

          throw new NexusError(`Nexus GraphQL HTTP error (status ${status}).`);
        }

        const json = (await response.json()) as GraphQLResponse<T>;

        if (json.errors?.length) {
          const message = json.errors.map((e) => e.message).join("; ");
          const codes = json.errors
            .map((e) => e.extensions?.code)
            .filter((code): code is string => Boolean(code));

          await logger.warn(
            `[NexusClient] GraphQL responded with errors: ${message}` +
              (codes.length ? ` (codes: ${codes.join(", ")})` : ""),
          );

          this.consecutiveErrors += 1;
          if (this.consecutiveErrors >= NexusClient.MAX_ERROR_THRESHOLD) {
            this.disabledForRun = true;
          }

          throw new NexusError(`Nexus GraphQL responded with errors: ${message}`);
        }

        this.consecutiveErrors = 0;
        this.disabledForRun = false;

        if (!json.data) {
          throw new NexusError("Nexus GraphQL response did not contain a data payload.");
        }

        return json.data;
      } catch (error) {
        lastError = error;

        if (error instanceof NexusError) {
          throw error;
        }

        const message = error instanceof Error ? error.message : String(error);
        await logger.warn(
          `[NexusClient] Network error talking to Nexus GraphQL (attempt ${
            attempt + 1
          }/${maxAttempts}): ${message}`,
        );

        this.consecutiveErrors += 1;

        if (attempt < maxAttempts - 1) {
          await this.backoff(attempt);
          // eslint-disable-next-line no-continue
          continue;
        }

        if (this.consecutiveErrors >= NexusClient.MAX_ERROR_THRESHOLD) {
          this.disabledForRun = true;
        }

        throw new NexusUnavailableError(
          "Failed to contact the Nexus Mods GraphQL API after multiple attempts. " +
            "Nexus will be treated as temporarily unavailable for this run.",
        );
      }
    }

    throw lastError instanceof Error
      ? lastError
      : new NexusError("Unknown Nexus GraphQL error after retries.");
  }

  private toNexusModMetadata(node: ModNode): NexusModMetadata {
    const requirements =
      node.modRequirements?.nexusRequirements?.nodes?.map((req) => ({
        modId: String(req.modId),
        modName: req.modName,
        url: req.url,
        notes: req.notes ?? undefined,
        externalRequirement: Boolean(req.externalRequirement),
      })) ?? [];

    return {
      nexusId: node.modId,
      uid: node.uid,
      name: node.name,
      summary: node.summary,
      version: node.version,
      author: node.author ?? undefined,
      game: node.game.domainName,
      gameId: node.game.id,
      tags: [], // The current schema exposes tags via different endpoints; leave empty for now.
      lastUpdated: node.updatedAt,
      category: node.category,
      url: `https://www.nexusmods.com/${node.game.domainName}/mods/${node.modId}`,
      description: node.description,
      downloads: node.downloads,
      endorsements: node.endorsements,
      status: node.status,
      requirements,
    };
  }

  private async fetchModsByIds(nexusIds: number[]): Promise<Map<number, ModNode>> {
    if (!nexusIds.length) {
      return new Map<number, ModNode>();
    }

    const gameDomain = this.resolveGameDomain();
    const variables = {
      ids: nexusIds.map((id) => ({
        gameDomain,
        modId: id,
      })),
      count: nexusIds.length,
    };

    const data = await this.graphqlRequest<LegacyModsByDomainResult>(
      LEGACY_MODS_BY_DOMAIN_QUERY,
      variables,
    );

    const nodes = data.legacyModsByDomain?.nodes ?? [];
    const byId = new Map<number, ModNode>();

    nodes.forEach((node) => {
      byId.set(node.modId, node);
    });

    return byId;
  }

  async getModMetadata(nexusId: number): Promise<NexusModMetadata> {
    await logger.debug(
      `[NexusClient] getModMetadata called for nexusId=${nexusId}; apiKeyConfigured=${Boolean(
        this.apiKey,
      )}`,
    );

    const cached = this.cache.get(nexusId);
    if (cached) {
      return cached;
    }

    const inFlight = this.pending.get(nexusId);
    if (inFlight) {
      return inFlight;
    }

    const promise = (async (): Promise<NexusModMetadata> => {
      if (!this.apiKey) {
        throw new NexusConfigError("Nexus API key is not configured.");
      }

      if (this.isDisabled) {
        throw new NexusUnavailableError(
          "Nexus requests have been disabled for this run due to previous errors.",
        );
      }

      const byId = await this.fetchModsByIds([nexusId]);
      const node = byId.get(nexusId);

      if (!node) {
        await logger.warn(
          `[NexusClient] No Nexus Mods result found for modId=${nexusId} (gameDomain=${this.resolveGameDomain()})`,
        );
        throw new NexusNotFoundError(
          `No Nexus Mods entry found for modId=${nexusId} in game ${this.resolveGameDomain()}.`,
        );
      }

      const metadata = this.toNexusModMetadata(node);
      this.cache.set(nexusId, metadata);
      return metadata;
    })();

    this.pending.set(nexusId, promise);
    try {
      const result = await promise;
      return result;
    } finally {
      this.pending.delete(nexusId);
    }
  }

  async getModMetadataBatch(nexusIds: number[]): Promise<NexusModMetadata[]> {
    await logger.debug(
      `[NexusClient] getModMetadataBatch called for nexusIds=[${nexusIds.join(", ")}]`,
    );

    if (!nexusIds.length) {
      return [];
    }

    if (!this.apiKey) {
      // Mirror the single-mod behaviour: treat missing key as configuration
      // error that callers can handle as "no Nexus available".
      throw new NexusConfigError("Nexus API key is not configured.");
    }

    if (this.isDisabled) {
      throw new NexusUnavailableError(
        "Nexus requests have been disabled for this run due to previous errors.",
      );
    }

    const uniqueIds = Array.from(new Set(nexusIds)).filter((id) => Number.isFinite(id) && id > 0);

    const fromCache: NexusModMetadata[] = [];
    const idsToFetch: number[] = [];

    uniqueIds.forEach((id) => {
      const cached = this.cache.get(id);
      if (cached) {
        fromCache.push(cached);
      } else {
        idsToFetch.push(id);
      }
    });

    const fetched: NexusModMetadata[] = [];

    if (idsToFetch.length) {
      // To respect Nexus' GraphQL complexity limits, fetch metadata in small
      // batches instead of a single very large query.
      const CHUNK_SIZE = 25;
      for (let i = 0; i < idsToFetch.length; i += CHUNK_SIZE) {
        const chunk = idsToFetch.slice(i, i + CHUNK_SIZE);
        const byId = await this.fetchModsByIds(chunk);

        chunk.forEach((id) => {
          const node = byId.get(id);
          if (!node) {
            void logger.warn(
              `[NexusClient] No Nexus data found for modId=${id} in batch lookup (gameDomain=${this.resolveGameDomain()})`,
            );
            return;
          }

          const metadata = this.toNexusModMetadata(node);
          this.cache.set(id, metadata);
          fetched.push(metadata);
        });
      }
    }

    // Return metadata for all successfully resolved mods. If some mods could
    // not be found, they are simply omitted from the result set; callers
    // should treat that as "no Nexus data" for those IDs.
    return [...fromCache, ...fetched];
  }

  private getDisplayName(user: {
    name?: string | null;
    username?: string | null;
    displayName?: string | null;
  }): string {
    return (
      user.displayName?.trim() ||
      user.name?.trim() ||
      user.username?.trim() ||
      "Unknown user"
    );
  }

  async searchComments(params: {
    query: string;
    limit?: number;
  }): Promise<NexusCommentHit[]> {
    const { query, limit } = params;

    const filter = {
      query: [
        {
          value: query,
          op: "MATCHES",
        },
      ],
    };

    const data = await this.graphqlRequest<SearchCommentsResult>(SEARCH_COMMENTS_QUERY, {
      filter,
      first: typeof limit === "number" ? limit : 20,
    });

    const nodes = data.searchComments?.nodes ?? [];

    return nodes.map((node) => ({
      id: node.id,
      body: node.body,
      createdAt: node.createdAt,
      creatorName: this.getDisplayName(node.creator),
      threadId: "", // Thread id is not exposed directly here; may be enriched in future.
    }));
  }

  async getCommentThread(params: {
    threadId: string;
    limit?: number;
  }): Promise<NexusThreadComment[]> {
    const { threadId, limit } = params;

    const data = await this.graphqlRequest<CommentThreadResult>(COMMENT_THREAD_QUERY, {
      id: threadId,
      first: typeof limit === "number" ? limit : 50,
    });

    const nodes = data.commentThread?.comments?.nodes ?? [];

    return nodes.map((node) => ({
      id: node.id,
      body: node.body,
      createdAt: node.createdAt,
      creatorName: this.getDisplayName(node.creator),
      parentId: node.parent?.id,
    }));
  }

  async getModFiles(nexusId: number): Promise<NexusModFileInfo[]> {
    const meta = await this.getModMetadata(nexusId);
    const uid = meta.uid;

    if (!uid) {
      await logger.warn(
        `[NexusClient] getModFiles called for nexusId=${nexusId}, but no UID is available; returning empty list.`,
      );
      return [];
    }

    const data = await this.graphqlRequest<ModFilesByUidResult>(MOD_FILES_BY_UID_QUERY, {
      uids: [uid],
      count: 50,
    });

    const nodes = data.modFilesByUid?.nodes ?? [];

    return nodes.map((node) => ({
      fileId: node.fileId,
      name: node.name,
      version: node.version,
      category: node.category,
      description: node.description ?? undefined,
      changelogText: node.changelogText ?? [],
      date: node.date,
      primary: node.primary === 1,
      totalDownloads: node.totalDownloads,
      uniqueDownloads: node.uniqueDownloads,
      requirementsAlert: node.requirementsAlert,
      scannedStatus: node.scannedV2,
    }));
  }

  async getModFileContentsSummary(params: {
    nexusId: number;
    limit?: number;
  }): Promise<NexusFileContentsSummary | null> {
    const { nexusId, limit } = params;
    const meta = await this.getModMetadata(nexusId);

    const data = await this.graphqlRequest<ModFileContentsResult>(MOD_FILE_CONTENTS_QUERY, {
      modId: meta.nexusId,
      count: typeof limit === "number" ? limit : 500,
    });

    const nodes = data.modFileContents?.nodes ?? [];
    if (!nodes.length) {
      return null;
    }

    const summary: NexusFileContentsSummary = {
      modId: nexusId,
      fileId: undefined,
      totalEntries: data.modFileContents.nodesCount,
      byCategory: {
        scripts: 0,
        meshes: 0,
        textures: 0,
        animations: 0,
        other: 0,
      },
      samplePaths: [],
    };

    nodes.forEach((entry) => {
      const pathLower = entry.filePath.toLowerCase();
      if (pathLower.endsWith(".pex") || pathLower.includes("scripts/")) {
        summary.byCategory.scripts += 1;
      } else if (pathLower.includes("meshes/")) {
        summary.byCategory.meshes += 1;
      } else if (pathLower.includes("textures/")) {
        summary.byCategory.textures += 1;
      } else if (pathLower.includes("animations/") || pathLower.endsWith(".hkx")) {
        summary.byCategory.animations += 1;
      } else {
        summary.byCategory.other += 1;
      }

      if (summary.samplePaths.length < 20) {
        summary.samplePaths.push(entry.filePath);
      }
    });

    return summary;
  }

  async getCollection(
    slug: string,
    domainName?: string,
  ): Promise<NexusCollectionSummary | null> {
    const resolvedDomain = domainName ?? this.resolveGameDomain();

    const data = await this.graphqlRequest<CollectionResult>(COLLECTION_QUERY, {
      slug,
      domainName: resolvedDomain,
      viewAdultContent: true,
    });

    const node = data.collection;
    if (!node) {
      return null;
    }

    return {
      slug: node.slug,
      name: node.name,
      description: node.description,
      gameDomain: node.game.domainName,
    };
  }

  async getCollectionBugReports(params: {
    slug: string;
    domainName?: string;
    status?: string;
    limit?: number;
  }): Promise<NexusCollectionBugReportSummary[]> {
    const { slug, domainName, status, limit } = params;
    const resolvedDomain = domainName ?? this.resolveGameDomain();
    const bugStatus = status ?? "OPEN";

    const data = await this.graphqlRequest<CollectionBugReportsResult>(
      COLLECTION_BUG_REPORTS_QUERY,
      {
        slug,
        domainName: resolvedDomain,
        status: bugStatus,
        first: typeof limit === "number" ? limit : 20,
      },
    );

    const nodes = data.collection?.bugReports?.nodes ?? [];

    return nodes.map((node) => ({
      id: node.id,
      title: node.title,
      description: node.description ?? undefined,
      status: node.status,
      createdAt: node.createdAt,
      commentThreadId: node.commentThread.id,
    }));
  }

  async searchModsByName(query: string, limit = 10): Promise<NexusModSearchResult[]> {
    const gameDomain = this.resolveGameDomain();

    const filter = {
      op: "AND",
      gameDomainName: [
        {
          value: gameDomain,
          op: "EQUALS",
        },
      ],
      name: [
        {
          value: query,
          op: "WILDCARD",
        },
      ],
    };

    const MODS_SEARCH_QUERY = `
      query SearchMods($filter: ModsFilter, $count: Int) {
        mods(filter: $filter, count: $count) {
          nodes {
            modId
            name
            summary
            status
            updatedAt
            downloads
            endorsements
            game {
              domainName
            }
          }
        }
      }
    `;

    const data = await this.graphqlRequest<ModsSearchResult>(MODS_SEARCH_QUERY, {
      filter,
      count: limit,
    });

    const nodes = data.mods?.nodes ?? [];

    return nodes.map((node) => ({
      modId: node.modId,
      name: node.name,
      summary: node.summary,
      gameDomain: node.game.domainName,
      url: `https://www.nexusmods.com/${node.game.domainName}/mods/${node.modId}`,
      downloads: node.downloads,
      endorsements: node.endorsements,
      lastUpdated: node.updatedAt,
    }));
  }

  /**
   * Performs a minimal GraphQL query that requires a valid API key, used to
   * verify that the configured key is accepted and the Nexus API is reachable.
   * Any failures surface as NexusError/NexusConfigError/NexusUnavailableError.
   */
  async checkHealth(): Promise<void> {
    await this.graphqlRequest<{ personalApiKey: { id: string } | null }>(
      PERSONAL_API_KEY_QUERY,
      {},
    );
  }
}
