import type { ProfileSnapshot } from "../../../shared/types";
import { runLootForProfile } from "../../loot/lootManager";
import type { LootReport } from "../../loot/lootManager";
import type { AgentTool } from "./types";

export const lootTool: AgentTool<ProfileSnapshot, LootReport> = {
  name: "get_loot_report",
  description: "Runs LOOT against the supplied profile snapshot and returns the parsed report.",
  invoke: async (snapshot) => runLootForProfile(snapshot),
};
