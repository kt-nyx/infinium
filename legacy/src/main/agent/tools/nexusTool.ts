import type { ProfileSnapshot, Settings } from "../../../shared/types";
import type { AgentTool } from "./types";
import { NexusClient, type NexusModMetadata } from "../../nexus/nexusClient";

export const createNexusTool = (
  settings: Settings,
  game: ProfileSnapshot["game"],
): AgentTool<number, NexusModMetadata> => {
  const client = new NexusClient(settings, game);
  return {
    name: "get_nexus_metadata",
    description:
      "Fetches real Nexus Mods metadata for a given numeric Nexus mod ID using the configured personal API key. " +
      "Returns the mod's name, summary, latest version, game domain, category, basic stats (downloads/endorsements), " +
      "requirements, and the canonical Nexus URL. Use this to confirm versions, game support, and basic mod info.",
    invoke: async (nexusId) => client.getModMetadata(nexusId),
  };
};
