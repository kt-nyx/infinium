import type { NexusModSearchResult, Settings } from "../../shared/types";
import type { ProfileSnapshot } from "../../shared/types";
import { NexusClient } from "./nexusClient";

/**
 * Search Nexus Mods for candidates that match a given MO2 mod name for a
 * specific Skyrim edition. Results are normalized into a compact structure
 * suitable for UI display when attaching nexusId values.
 */
export const searchNexusModsByName = async (
  settings: Settings,
  game: ProfileSnapshot["game"],
  query: string,
  limit?: number,
): Promise<NexusModSearchResult[]> => {
  const client = new NexusClient(settings, game);
  return client.searchModsByName(query, limit);
};




