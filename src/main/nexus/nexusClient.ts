import type { Settings } from "../../shared/types";

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
    if (!this.apiKey) {
      return createMockMetadata(nexusId);
    }

    // TODO: Implement authenticated requests to the official Nexus Mods API once API surface is finalized.
    return createMockMetadata(nexusId);
  }

  async getModMetadataBatch(nexusIds: number[]): Promise<NexusModMetadata[]> {
    return Promise.all(nexusIds.map((id) => this.getModMetadata(id)));
  }
}
