import type { Settings } from "../../../shared/types";
import type { AgentTool } from "./types";
import { NexusClient, type NexusModMetadata } from "../../nexus/nexusClient";

export const createNexusTool = (settings: Settings): AgentTool<number, NexusModMetadata> => {
  const client = new NexusClient(settings);
  return {
    name: "get_nexus_metadata",
    description: "Fetches metadata for a Nexus mod ID using the user's API key (mocked for now).",
    invoke: async (nexusId) => client.getModMetadata(nexusId),
  };
};
