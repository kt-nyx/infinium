import { describe, expect, it, vi } from "vitest";
import type { ProfileSnapshot, Settings } from "../src/shared/types";
import {
  NexusClient,
  NexusConfigError,
  NexusNotFoundError,
  NexusUnavailableError,
  type NexusCommentHit,
  type NexusCollectionBugReportSummary,
  type NexusCollectionSummary,
  type NexusFileContentsSummary,
  type NexusModFileInfo,
  type NexusModMetadata,
  type NexusThreadComment,
} from "../src/main/nexus/nexusClient";

// Basic settings stub; only nexusApiKey is relevant for NexusClient.
const baseSettings: Settings = {
  mo2RootGuess: undefined,
  mo2Instances: [],
  selectedInstanceId: undefined,
  selectedProfileId: undefined,
  skyrimSeDataPath: undefined,
  lootPortablePath: undefined,
  lootInstalledPath: undefined,
  lootMode: "auto",
  lootCustomPath: undefined,
  nexusApiKey: "test-key",
  ragIndexPath: undefined,
  analysisMode: "offline",
  analysisDefaults: {
    useLoot: true,
    useNexus: true,
    useRag: false,
    complexity: 3,
    opinionatedness: 3,
  },
  logLevel: "info",
};

const baseProfile: ProfileSnapshot = {
  profileId: "TestProfile",
  game: "SkyrimSE",
  mo2InstancePath: "C:/MO2",
  mods: [],
  pluginLoadOrder: [],
  lootAvailable: false,
  nexusAvailable: false,
};

// Helper to create a client with a stubbed fetch implementation.
const createClientWithMockFetch = (
  fetchImpl: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>,
): NexusClient => {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis as any).fetch = fetchImpl;
  return new NexusClient(baseSettings, baseProfile.game);
};

describe("NexusClient", () => {
  it("throws NexusConfigError when no API key is configured", async () => {
    const settings: Settings = { ...baseSettings, nexusApiKey: undefined };
    const client = new NexusClient(settings, baseProfile.game);

    await expect(client.getModMetadata(123)).rejects.toBeInstanceOf(NexusConfigError);
  });

  it("returns metadata for a valid mod via getModMetadata", async () => {
    const mockResponse = {
      data: {
        legacyModsByDomain: {
          nodes: [
            {
              modId: 123,
              name: "Test Mod",
              summary: "Short summary",
              description: "Long description",
              version: "1.2.3",
              author: "Author",
              category: "Gameplay",
              downloads: 1000,
              endorsements: 100,
              status: "published",
              updatedAt: "2025-01-01T00:00:00Z",
              game: {
                domainName: "skyrimspecialedition",
              },
              modRequirements: {
                nexusRequirements: {
                  nodes: [],
                },
              },
            },
          ],
        },
      },
    };

    const client = createClientWithMockFetch(async () => {
      return new Response(JSON.stringify(mockResponse), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    const result: NexusModMetadata = await client.getModMetadata(123);

    expect(result.nexusId).toBe(123);
    expect(result.name).toBe("Test Mod");
    expect(result.version).toBe("1.2.3");
    expect(result.game).toBe("skyrimspecialedition");
    expect(result.url).toContain("/skyrimspecialedition/mods/123");
  });

  it("throws NexusNotFoundError when no nodes are returned for a mod ID", async () => {
    const mockResponse = {
      data: {
        legacyModsByDomain: {
          nodes: [],
        },
      },
    };

    const client = createClientWithMockFetch(async () => {
      return new Response(JSON.stringify(mockResponse), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    await expect(client.getModMetadata(999999)).rejects.toBeInstanceOf(NexusNotFoundError);
  });

  it("treats repeated HTTP 500 errors as temporary unavailability", async () => {
    const mockFetch = vi
      .fn()
      .mockResolvedValueOnce(
        new Response("server error", {
          status: 500,
        }),
      )
      .mockResolvedValueOnce(
        new Response("server error", {
          status: 500,
        }),
      )
      .mockResolvedValue(
        new Response("server error", {
          status: 500,
        }),
      );

    const client = createClientWithMockFetch(mockFetch as unknown as typeof fetch);

    await expect(client.getModMetadata(123)).rejects.toBeInstanceOf(NexusUnavailableError);
  });

  it("maps searchComments results into NexusCommentHit objects", async () => {
    const mockResponse = {
      data: {
        searchComments: {
          nodes: [
            {
              id: "c1",
              body: "This mod causes a CTD when entering Whiterun.",
              createdAt: "2025-01-01T00:00:00Z",
              creator: {
                name: "UserOne",
                username: "user1",
                displayName: "User One",
              },
            },
          ],
        },
      },
    };

    const client = createClientWithMockFetch(async () => {
      return new Response(JSON.stringify(mockResponse), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    const results: NexusCommentHit[] = await client.searchComments({
      query: "CTD Whiterun",
      limit: 5,
    });

    expect(results).toHaveLength(1);
    expect(results[0].id).toBe("c1");
    expect(results[0].creatorName).toBe("User One");
  });

  it("maps commentThread results into NexusThreadComment objects", async () => {
    const mockResponse = {
      data: {
        commentThread: {
          id: "t1",
          comments: {
            nodes: [
              {
                id: "c1",
                body: "Known conflict with Mod X.",
                createdAt: "2025-01-01T00:00:00Z",
                creator: {
                  name: "Author",
                  username: "author1",
                  displayName: "Mod Author",
                },
              },
            ],
          },
        },
      },
    };

    const client = createClientWithMockFetch(async () => {
      return new Response(JSON.stringify(mockResponse), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    const thread: NexusThreadComment[] = await client.getCommentThread({
      threadId: "t1",
      limit: 10,
    });

    expect(thread).toHaveLength(1);
    expect(thread[0].creatorName).toBe("Mod Author");
  });

  it("maps modFilesByUid results into NexusModFileInfo objects", async () => {
    const modResponse = {
      data: {
        legacyModsByDomain: {
          nodes: [
            {
              modId: 123,
              uid: "uid123",
              name: "Test Mod",
              summary: "Summary",
              description: "Description",
              version: "1.0.0",
              author: "Author",
              category: "Gameplay",
              downloads: 1000,
              endorsements: 100,
              status: "published",
              updatedAt: "2025-01-01T00:00:00Z",
              game: {
                id: 1704,
                domainName: "skyrimspecialedition",
              },
              modRequirements: {
                nexusRequirements: {
                  nodes: [],
                },
              },
            },
          ],
        },
      },
    };

    const filesResponse = {
      data: {
        modFilesByUid: {
          nodes: [
            {
              fileId: 1,
              name: "Main File",
              description: "Main file description",
              changelogText: ["Fixed crash on startup"],
              version: "1.0.0",
              date: 1700000000,
              category: "MAIN",
              primary: 1,
              totalDownloads: 5000,
              uniqueDownloads: 4000,
              requirementsAlert: 0,
              scannedV2: "VERIFIED",
            },
          ],
        },
      },
    };

    let callCount = 0;
    const client = createClientWithMockFetch(async () => {
      callCount += 1;
      const payload = callCount === 1 ? modResponse : filesResponse;
      return new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    const files: NexusModFileInfo[] = await client.getModFiles(123);
    expect(files).toHaveLength(1);
    expect(files[0].name).toBe("Main File");
    expect(files[0].primary).toBe(true);
    expect(files[0].changelogText[0]).toContain("Fixed crash");
  });

  it("summarizes mod file contents into NexusFileContentsSummary", async () => {
    const modResponse = {
      data: {
        legacyModsByDomain: {
          nodes: [
            {
              modId: 123,
              uid: "uid123",
              name: "Test Mod",
              summary: "Summary",
              description: "Description",
              version: "1.0.0",
              author: "Author",
              category: "Gameplay",
              downloads: 1000,
              endorsements: 100,
              status: "published",
              updatedAt: "2025-01-01T00:00:00Z",
              game: {
                id: 1704,
                domainName: "skyrimspecialedition",
              },
              modRequirements: {
                nexusRequirements: {
                  nodes: [],
                },
              },
            },
          ],
        },
      },
    };

    const contentsResponse = {
      data: {
        modFileContents: {
          nodes: [
            { modId: 123, fileId: 1, filePath: "scripts/myscript.pex", fileExtension: "pex" },
            { modId: 123, fileId: 1, filePath: "meshes/mesh.nif", fileExtension: "nif" },
            { modId: 123, fileId: 1, filePath: "textures/tex.dds", fileExtension: "dds" },
          ],
          nodesCount: 3,
        },
      },
    };

    let callCount = 0;
    const client = createClientWithMockFetch(async () => {
      callCount += 1;
      const payload = callCount === 1 ? modResponse : contentsResponse;
      return new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    const summary: NexusFileContentsSummary | null = await client.getModFileContentsSummary({
      nexusId: 123,
      limit: 10,
    });

    expect(summary).not.toBeNull();
    expect(summary?.byCategory.scripts).toBeGreaterThan(0);
    expect(summary?.byCategory.meshes).toBeGreaterThan(0);
    expect(summary?.byCategory.textures).toBeGreaterThan(0);
  });

  it("maps collection and bug reports into summary objects", async () => {
    const collectionResponse = {
      data: {
        collection: {
          slug: "test-collection",
          name: "Test Collection",
          description: "A test collection",
          game: {
            domainName: "skyrimspecialedition",
          },
        },
      },
    };

    const bugReportsResponse = {
      data: {
        collection: {
          bugReports: {
            nodes: [
              {
                id: "br1",
                title: "Crash on new game",
                description: "Known crash with certain ENB presets.",
                status: "OPEN",
                createdAt: "2025-01-01T00:00:00Z",
                commentThread: {
                  id: "t1",
                },
              },
            ],
          },
        },
      },
    };

    let callCount = 0;
    const client = createClientWithMockFetch(async () => {
      callCount += 1;
      const payload = callCount === 1 ? collectionResponse : bugReportsResponse;
      return new Response(JSON.stringify(payload), {
        status: 200,
        headers: {
          "Content-Type": "application/json",
        },
      });
    });

    const collection: NexusCollectionSummary | null = await client.getCollection(
      "test-collection",
    );
    expect(collection).not.toBeNull();
    expect(collection?.slug).toBe("test-collection");

    const reports: NexusCollectionBugReportSummary[] = await client.getCollectionBugReports({
      slug: "test-collection",
      status: "OPEN",
      limit: 5,
    });

    expect(reports).toHaveLength(1);
    expect(reports[0].title).toContain("Crash on new game");
    expect(reports[0].commentThreadId).toBe("t1");
  });
});


