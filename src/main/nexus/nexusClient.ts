import type { Settings } from "../../shared/types";
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
}

const createMockMetadata = (nexusId: number): NexusModMetadata => ({
  nexusId,
  name: `Mocked Nexus Mod #${nexusId}`,
  summary: "Nexus API integration pending. This payload is mocked for UI wiring.",
  version: "1.0.0",
  game: "skyrimspecialedition",
  tags: ["mock", "todo"],
  url: `https://www.nexusmods.com/skyrimspecialedition/mods/${nexusId}`,
});

export class NexusClient {
  constructor(private readonly settings: Settings) {}

  private get apiKey(): string | undefined {
    return this.settings.nexusApiKey;
  }

  async getModMetadata(nexusId: number): Promise<NexusModMetadata> {
    const hasKey = Boolean(this.apiKey);
    await logger.debug(
      `[NexusClient] getModMetadata called for nexusId=${nexusId}; apiKeyConfigured=${hasKey}`,
    );

    if (!hasKey) {
      await logger.debug(
        `[NexusClient] No API key configured; returning mocked metadata for nexusId=${nexusId}`,
      );
      return createMockMetadata(nexusId);
    }

    // TODO: Implement authenticated requests to the official Nexus Mods API once API surface is finalized.
    await logger.debug(
      `[NexusClient] Nexus API integration pending; returning mocked metadata for nexusId=${nexusId}`,
    );
    return createMockMetadata(nexusId);
  }

  async getModMetadataBatch(nexusIds: number[]): Promise<NexusModMetadata[]> {
    await logger.debug(
      `[NexusClient] getModMetadataBatch called for nexusIds=[${nexusIds.join(", ")}]`,
    );
    return Promise.all(nexusIds.map((id) => this.getModMetadata(id)));
  }
}
