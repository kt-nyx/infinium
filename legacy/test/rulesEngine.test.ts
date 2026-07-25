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

  it("flags LE-only mods in SE/AE profiles when Nexus-derived gameSupport indicates a mismatch", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      game: "SkyrimSE",
      mods: [
        ...baseProfile.mods,
        {
          id: "Oldrim Only Mod",
          name: "Oldrim Only Mod",
          enabled: true,
          path: "C:/MO2/mods/Oldrim Only Mod",
          plugins: ["OldrimOnly.esp"],
          nexusId: 12345,
          gameSupport: "SkyrimLE",
          metadata: {
            nexus: {
              gameDomain: "skyrim",
              url: "https://www.nexusmods.com/skyrim/mods/12345",
            },
          },
        },
      ],
    };

    const result = evaluate(profile);
    const hardIncompat = result.issues.filter(
      (issue) => issue.category === "hard_incompatibility",
    );

    expect(hardIncompat.length).toBeGreaterThanOrEqual(1);
    const issue = hardIncompat[0];
    expect(issue.source).toContain("nexus");
    expect(issue.affectedMods).toContain("Oldrim Only Mod");
  });

  it("aggregates outdated mods into a single Nexus-backed issue with all affected mods listed", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        ...baseProfile.mods,
        {
          id: "Some Nexus Mod",
          name: "Some Nexus Mod",
          enabled: true,
          path: "C:/MO2/mods/Some Nexus Mod",
          plugins: ["SomeNexusMod.esp"],
          nexusId: 54321,
          installedVersion: "1.0.0",
          latestVersion: "1.2.0",
          metadata: {
            nexus: {
              url: "https://www.nexusmods.com/skyrimspecialedition/mods/54321",
            },
          },
        },
        {
          id: "Another Nexus Mod",
          name: "Another Nexus Mod",
          enabled: true,
          path: "C:/MO2/mods/Another Nexus Mod",
          plugins: ["AnotherNexusMod.esp"],
          nexusId: 98765,
          installedVersion: "2.0.0",
          latestVersion: "3.1.0",
          metadata: {
            nexus: {
              url: "https://www.nexusmods.com/skyrimspecialedition/mods/98765",
            },
          },
        },
      ],
    };

    const result = evaluate(profile);
    const outdatedIssues = result.issues.filter(
      (issue) => issue.category === "outdated_or_wrong_version",
    );

    expect(outdatedIssues).toHaveLength(1);
    const issue = outdatedIssues[0];
    expect(issue.source).toContain("nexus");
    expect(issue.affectedMods).toEqual(
      expect.arrayContaining(["Some Nexus Mod", "Another Nexus Mod"]),
    );
    expect(issue.details).toContain("Some Nexus Mod");
    expect(issue.details).toContain("Another Nexus Mod");
  });

  it("aggregates low-endorsement Nexus heuristics into a single low-severity issue", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        ...baseProfile.mods,
        {
          id: "Popular But Divisive Mod",
          name: "Popular But Divisive Mod",
          enabled: true,
          path: "C:/MO2/mods/Popular But Divisive Mod",
          plugins: ["Divisive.esp"],
          nexusId: 11111,
          metadata: {
            nexus: {
              url: "https://www.nexusmods.com/skyrimspecialedition/mods/11111",
              downloads: 100000,
              endorsements: 500,
              status: "published",
            },
          },
        },
        {
          id: "Another Questionable Mod",
          name: "Another Questionable Mod",
          enabled: true,
          path: "C:/MO2/mods/Another Questionable Mod",
          plugins: ["Questionable.esp"],
          nexusId: 22222,
          metadata: {
            nexus: {
              url: "https://www.nexusmods.com/skyrimspecialedition/mods/22222",
              downloads: 50000,
              endorsements: 200,
              status: "published",
            },
          },
        },
      ],
    };

    const result = evaluate(profile);
    const heuristicIssues = result.issues.filter(
      (issue) => issue.category === "other" && issue.source.includes("nexus"),
    );

    expect(heuristicIssues.length).toBeGreaterThanOrEqual(1);
    const lowEndorseIssue = heuristicIssues.find((issue) =>
      issue.summary.includes("endorsements compared to downloads"),
    );
    expect(lowEndorseIssue).toBeDefined();
    expect(lowEndorseIssue?.severity).toBe("low");
    expect(lowEndorseIssue?.affectedMods).toEqual(
      expect.arrayContaining(["Popular But Divisive Mod", "Another Questionable Mod"]),
    );
    expect(lowEndorseIssue?.details).toContain("Popular But Divisive Mod");
    expect(lowEndorseIssue?.details).toContain("Another Questionable Mod");
  });

  it("flags overlapping broad-scope mods using Nexus-enriched overlapDomains", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        {
          id: "Core Framework A",
          name: "Core Framework A",
          enabled: true,
          path: "C:/MO2/mods/Core Framework A",
          plugins: ["CoreFrameworkA.esm"],
          nexusCategory: "Utilities",
          categoryGroup: "framework_like",
          scopeHint: "broad",
          overlapDomains: ["combat"],
          overlapTags: ["system:combat"],
        },
        {
          id: "Core Overhaul B",
          name: "Core Overhaul B",
          enabled: true,
          path: "C:/MO2/mods/Core Overhaul B",
          plugins: ["CoreOverhaulB.esp"],
          nexusCategory: "Overhauls",
          categoryGroup: "overhaul_like",
          scopeHint: "broad",
          overlapDomains: ["combat"],
          overlapTags: ["system:combat"],
        },
        {
          id: "Major Overhaul C",
          name: "Major Overhaul C",
          enabled: true,
          path: "C:/MO2/mods/Major Overhaul C",
          plugins: ["MajorOverhaulC.esp"],
          nexusCategory: "Gameplay",
          categoryGroup: "overhaul_like",
          scopeHint: "broad",
          overlapDomains: ["combat"],
          overlapTags: ["system:combat"],
        },
      ],
      pluginLoadOrder: ["Skyrim.esm", "CoreFrameworkA.esm", "CoreOverhaulB.esp", "MajorOverhaulC.esp"],
      lootAvailable: false,
      nexusAvailable: true,
    };

    const result = evaluate(profile);
    const highImpactIssues = result.issues.filter(
      (issue) =>
        issue.category === "soft_conflict" &&
        issue.summary.includes("Multiple large mods may overlap in purpose"),
    );

    expect(highImpactIssues.length).toBeGreaterThanOrEqual(1);
    const issue = highImpactIssues[0];
    expect(issue.affectedMods).toEqual(
      expect.arrayContaining(["Core Framework A", "Core Overhaul B", "Major Overhaul C"]),
    );
  });

  it("does not automatically treat ambiguous categories as high-importance without strong cues", () => {
    // Note: rulesEngine does not compute importanceBucket; this test exists to
    // document that ambiguity-aware scoring lives in Nexus enrichment. Here we
    // simply ensure the rules heuristic does not fire when mods are not marked
    // as high/medium impact.
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        {
          id: "Small Gameplay Tweak 1",
          name: "Small Gameplay Tweak 1",
          enabled: true,
          path: "C:/MO2/mods/Small Gameplay Tweak 1",
          plugins: ["Tweak1.esp"],
          nexusCategory: "Gameplay",
          categoryGroup: "overhaul_like",
          categoryAmbiguity: "high",
          scopeHint: "narrow",
          importanceBucket: "low",
          importanceScore: 2,
        },
        {
          id: "Small Gameplay Tweak 2",
          name: "Small Gameplay Tweak 2",
          enabled: true,
          path: "C:/MO2/mods/Small Gameplay Tweak 2",
          plugins: ["Tweak2.esp"],
          nexusCategory: "Gameplay",
          categoryGroup: "overhaul_like",
          categoryAmbiguity: "high",
          scopeHint: "narrow",
          importanceBucket: "low",
          importanceScore: 2,
        },
        {
          id: "Small Gameplay Tweak 3",
          name: "Small Gameplay Tweak 3",
          enabled: true,
          path: "C:/MO2/mods/Small Gameplay Tweak 3",
          plugins: ["Tweak3.esp"],
          nexusCategory: "Gameplay",
          categoryGroup: "overhaul_like",
          categoryAmbiguity: "high",
          scopeHint: "narrow",
          importanceBucket: "low",
          importanceScore: 2,
        },
      ],
      pluginLoadOrder: ["Skyrim.esm", "Tweak1.esp", "Tweak2.esp", "Tweak3.esp"],
      lootAvailable: false,
      nexusAvailable: true,
    };

    const result = evaluate(profile);
    expect(
      result.issues.some((issue) =>
        issue.summary.includes("Many high-impact overhauls and frameworks enabled"),
      ),
    ).toBe(false);
  });

  it("flags missing requirements from agent-extracted requirementsAgent", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        {
          id: "Needs Dependency",
          name: "Needs Dependency",
          enabled: true,
          path: "C:/MO2/mods/Needs Dependency",
          plugins: ["NeedsDependency.esp"],
          requirementsAgent: [
            {
              kind: "required",
              targetPlugin: "SomeDependency.esm",
              evidence: "Requires SomeDependency.esm",
              confidence: "high",
            },
          ],
        },
      ],
      pluginLoadOrder: ["Skyrim.esm", "NeedsDependency.esp"],
      lootAvailable: false,
      nexusAvailable: true,
    };

    const result = evaluate(profile);
    const reqIssues = result.issues.filter(
      (issue) =>
        issue.category === "configuration" &&
        issue.subcategory === "missing_requirements_from_descriptions",
    );

    expect(reqIssues.length).toBeGreaterThanOrEqual(1);
    expect(reqIssues[0].affectedMods).toEqual(expect.arrayContaining(["Needs Dependency"]));
  });

  it("flags variant mismatches from agent-extracted variantAgent", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        {
          id: "Wrong Variant",
          name: "Wrong Variant",
          enabled: true,
          path: "C:/MO2/mods/Wrong Variant",
          plugins: ["WrongVariant.esp"],
          variantAgent: {
            expected: "AE",
            detected: "SE",
            mismatch: true,
            evidence: "Description indicates AE-only module",
            confidence: "high",
          },
        },
      ],
      pluginLoadOrder: ["Skyrim.esm", "WrongVariant.esp"],
      lootAvailable: false,
      nexusAvailable: true,
    };

    const result = evaluate(profile);
    const variantIssues = result.issues.filter(
      (issue) =>
        issue.category === "outdated_or_wrong_version" &&
        issue.subcategory === "variant_mismatch",
    );

    expect(variantIssues.length).toBeGreaterThanOrEqual(1);
    expect(variantIssues[0].affectedMods).toEqual(expect.arrayContaining(["Wrong Variant"]));
  });

  it("flags high script/performance risk from agent-extracted scriptPerfRiskAgent", () => {
    const profile: ProfileSnapshot = {
      ...baseProfile,
      mods: [
        {
          id: "Script Heavy Mod",
          name: "Script Heavy Mod",
          enabled: true,
          path: "C:/MO2/mods/Script Heavy Mod",
          plugins: ["ScriptHeavy.esp"],
          scriptPerfRiskAgent: {
            level: "high",
            reasons: ["Script-heavy background monitoring"],
            confidence: "high",
            fileSummaryUsed: true,
          },
        },
      ],
      pluginLoadOrder: ["Skyrim.esm", "ScriptHeavy.esp"],
      lootAvailable: false,
      nexusAvailable: true,
    };

    const result = evaluate(profile);
    const perfIssues = result.issues.filter(
      (issue) =>
        issue.category === "script_load" &&
        issue.subcategory === "ai_script_perf_risk",
    );

    expect(perfIssues.length).toBeGreaterThanOrEqual(1);
    expect(perfIssues[0].affectedMods).toEqual(expect.arrayContaining(["Script Heavy Mod"]));
  });
});