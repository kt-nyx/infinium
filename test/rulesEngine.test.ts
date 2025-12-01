import { describe, expect, it } from "vitest";
import type { ProfileSnapshot } from "../src/shared/types";
import { evaluate } from "../src/main/rules/rulesEngine";
import type { LootReport, LootPluginMessage } from "../src/main/loot/lootManager";

const baseProfile: ProfileSnapshot = {
  profileId: "TestProfile",
  game: "SkyrimSE",
  mo2InstancePath: "C:/MO2",
  mods: [
    {
      id: "Immersive Citizens",
      name: "Immersive Citizens",
      enabled: true,
      path: "C:/MO2/mods/Immersive Citizens",
      plugins: ["Immersive Citizens.esp"],
    },
    {
      id: "AI Overhaul",
      name: "AI Overhaul",
      enabled: true,
      path: "C:/MO2/mods/AI Overhaul",
      plugins: ["AI Overhaul.esp"],
    },
  ],
  pluginLoadOrder: ["Skyrim.esm"],
  lootAvailable: false,
  nexusAvailable: false,
};

describe("rulesEngine", () => {
  it("flags multiple AI overhauls", () => {
    const result = evaluate(baseProfile);
    expect(result.issues.some((issue) => issue.category === "soft_conflict")).toBe(true);
  });

  it("aggregates missing masters into a single issue with structured payload", () => {
    const lootReport: LootReport = {
      timestamp: "2025-01-01T00:00:00Z",
      summary: "Test LOOT report",
      missingMasters: [
        { plugin: "SomePlugin.esp", masters: ["MasterA.esm"] },
        { plugin: "AnotherPlugin.esp", masters: ["MasterB.esm", "MasterC.esm"] },
      ],
      warnings: [],
      loadOrder: ["Skyrim.esm"],
      metadata: {
        lootModeUsed: "libloot",
        gameId: "SkyrimSE",
        gamePath: "C:/Games/SkyrimSE",
        stats: {
          pluginsAnalysed: 2,
          missingMasterCount: 3,
          warningCount: 0,
        },
      },
    };

    const result = evaluate(baseProfile, lootReport);
    const missingMastersIssues = result.issues.filter(
      (issue) => issue.category === "missing_masters",
    );

    expect(missingMastersIssues).toHaveLength(1);
    const issue = missingMastersIssues[0];
    expect(issue.lootMissingMasters).toBeDefined();
    expect(issue.lootMissingMasters).toHaveLength(2);
    expect(issue.affectedPlugins).toEqual(["SomePlugin.esp", "AnotherPlugin.esp"]);
  });

  it("promotes LOOT plugin warnings into a per-plugin issue with messages attached", () => {
    const messages: LootPluginMessage[] = [
      {
        plugin: "CompatPatchNeeded.esp",
        severity: "warning",
        text: "This plugin requires a compatibility patch with SomeOtherMod.esp.",
      },
      {
        plugin: "CompatPatchNeeded.esp",
        severity: "note",
        text: "You may find patches on the mod's Nexus page.",
      },
    ];

    const lootReport: LootReport = {
      timestamp: "2025-01-01T00:00:00Z",
      summary: "Test LOOT report",
      missingMasters: [],
      warnings: [],
      loadOrder: ["Skyrim.esm", "CompatPatchNeeded.esp"],
      metadata: {
        lootModeUsed: "libloot",
        gameId: "SkyrimSE",
        gamePath: "C:/Games/SkyrimSE",
        stats: {
          pluginsAnalysed: 2,
          missingMasterCount: 0,
          warningCount: 1,
        },
        plugins: [
          {
            name: "CompatPatchNeeded.esp",
            index: 1,
            sortedIndex: 1,
            isActive: true,
            isMaster: false,
            isLightPlugin: false,
            isEmpty: false,
            loadsArchive: false,
            messages,
          },
        ],
      },
    };

    const result = evaluate(baseProfile, lootReport);
    const lootMessageIssues = result.issues.filter(
      (issue) => issue.subcategory === "loot_plugin_messages",
    );

    expect(lootMessageIssues.length).toBeGreaterThanOrEqual(1);
    const issue = lootMessageIssues[0];
    expect(issue.lootPluginMessages).toBeDefined();
    expect(issue.lootPluginMessages?.some((msg) => msg.text.includes("compatibility patch"))).toBe(
      true,
    );
  });
});