import { describe, expect, it } from "vitest";
import type { ProfileSnapshot } from "../src/shared/types";
import {
  __test_parseLiblootResponseToReport as parseLiblootResponseToReport,
  type LootReport,
} from "../src/main/loot/lootManager";

type LiblootResponseForTest = Parameters<typeof parseLiblootResponseToReport>[2];

describe("lootManager libloot parsing", () => {
  const baseSnapshot: ProfileSnapshot = {
    profileId: "TestProfile",
    game: "SkyrimSE",
    mo2InstancePath: "C:/MO2",
    mods: [],
    pluginLoadOrder: ["Skyrim.esm", "Update.esm"],
    lootAvailable: true,
    nexusAvailable: false,
  };

  it("maps a minimal libloot response into LootReport", () => {
    const response: LiblootResponseForTest = {
      timestamp: "2025-01-01T00:00:00Z",
      missingMasters: [
        {
          plugin: "SomePlugin.esp",
          masters: ["MasterA.esm", "MasterB.esm"],
        },
      ],
      warnings: ["helper warning"],
      sortedLoadOrder: ["Skyrim.esm", "Update.esm"],
      metadata: {
        lootVersion: "0.28.3",
        gameId: "SkyrimSE",
        normalizedGamePath: "C:/Games/SkyrimSE",
        stats: {
          pluginsAnalysed: 2,
          missingMasterCount: 2,
          warningCount: 1,
        },
        ambiguousLoadOrder: false,
        plugins: [],
        extra: {},
      },
      // Optional error field omitted to represent the success-path shape.
    };

    const request = {
      game: baseSnapshot.game,
      gamePath: "C:/Games/SkyrimSE/Data",
      profile: {
        plugins: baseSnapshot.pluginLoadOrder,
        modRoots: [],
      },
    };

    const report: LootReport = parseLiblootResponseToReport(
      baseSnapshot,
      request,
      response,
    );

    expect(report.timestamp).toBe(response.timestamp);
    expect(report.loadOrder).toEqual(response.sortedLoadOrder);
    expect(report.missingMasters).toHaveLength(1);
    expect(report.missingMasters[0].plugin).toBe("SomePlugin.esp");
    expect(report.metadata?.lootModeUsed).toBe("libloot");
    expect(report.metadata?.stats.pluginsAnalysed).toBe(2);
  });
});


