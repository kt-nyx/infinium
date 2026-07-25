import type { ProfileSnapshot } from "../../../shared/types";
import { runLootForProfile } from "../../loot/lootManager";
import type { LootReport } from "../../loot/lootManager";
import type { AgentTool } from "./types";

export const lootTool: AgentTool<ProfileSnapshot, LootReport> = {
  name: "get_loot_report",
  description:
    "Runs a real LOOT (libloot) analysis for the supplied profile snapshot and returns a structured LootReport, including aggregated missing masters, warnings, sorted load order, and rich per-plugin metadata (LOOT messages, requirements, incompatibilities, tags, stats).",
  invoke: async (snapshot) => runLootForProfile(snapshot),
};
