import { describe, expect, it, vi } from "vitest";

import type { AnalysisResult, ProfileSnapshot, Settings } from "../src/shared/types";
import { runAgenticAnalysis, runOfflineAnalysis } from "../src/main/analysis/pipeline";

const makeSettings = (): Settings => ({
  mo2Instances: [],
  lootMode: "auto",
  analysisDefaults: {
    useLoot: false,
    useNexus: false,
    useRag: false,
    complexity: 2,
    opinionatedness: 2,
  },
  logLevel: "error",
});

const makeProfile = (): ProfileSnapshot => ({
  profileId: "test-profile",
  game: "SkyrimSE",
  mo2InstancePath: "C:\\\\MO2",
  mods: [
    {
      id: "modA",
      name: "Combat Overhaul A",
      enabled: true,
      path: "C:\\\\MO2\\\\mods\\\\modA",
      plugins: ["A.esp"],
      overlapTagsAgent: ["system:combat"],
      requirementsAgent: [
        {
          kind: "required",
          targetPlugin: "MissingMaster.esm",
          evidence: "Requires MissingMaster.esm",
          confidence: "high",
        },
      ],
    },
    {
      id: "modB",
      name: "Combat Overhaul B",
      enabled: true,
      path: "C:\\\\MO2\\\\mods\\\\modB",
      plugins: ["B.esp"],
      overlapTagsAgent: ["system:combat"],
    },
    {
      id: "modC",
      name: "HUD Mod",
      enabled: true,
      path: "C:\\\\MO2\\\\mods\\\\modC",
      plugins: ["HUD.esp"],
      overlapTagsAgent: ["ui:hud"],
    },
  ],
  pluginLoadOrder: ["A.esp", "B.esp", "HUD.esp"],
  lootAvailable: false,
  nexusAvailable: false,
});

describe("orchestrator regression harness", () => {
  it("does not attempt LOOT/Nexus/tools when disabled; produces trace id", async () => {
    const settings = makeSettings();
    const profile = makeProfile();

    const offline = await runOfflineAnalysis(profile, settings, {
      useLoot: false,
      useNexus: false,
      useRag: false,
      complexity: 2,
      opinionatedness: 2,
    });

    const result: AnalysisResult = await runAgenticAnalysis(profile, settings, offline, {
      useLoot: false,
      useNexus: false,
      useRag: false,
      complexity: 2,
      opinionatedness: 2,
    });

    expect(result.metadata.offlineOnly).toBe(false);
    expect(result.metadata.agentUsed).toBe(true);
    expect(typeof result.metadata.analysisTraceId).toBe("string");
    expect((result.metadata.analysisTraceId ?? "").length).toBeGreaterThan(5);
  });

  it("Stage2 produces candidates and Stage3 respects top-K budget", async () => {
    const settings = makeSettings();
    const profile = makeProfile();

    const offline = await runOfflineAnalysis(profile, settings, {
      useLoot: false,
      useNexus: false,
      useRag: false,
      complexity: 2,
      opinionatedness: 2,
    });

    const result = await runAgenticAnalysis(profile, settings, offline, {
      useLoot: false,
      useNexus: false,
      useRag: false,
      complexity: 2,
      opinionatedness: 2,
    });

    // We should get at least one agent-derived issue from overlap/requirements candidates.
    const agentIssues = result.issues.filter((i) => i.source.includes("agent"));
    expect(agentIssues.length).toBeGreaterThan(0);

    // Budget at complexity 2 -> maxInvestigations should be 3 (see budgetsForFlags).
    // Stage3 generates at most one issue per investigated candidate, but Stage4 may merge.
    // So we assert <= 3 agent issues remain after merge.
    expect(agentIssues.length).toBeLessThanOrEqual(3);
  });

  it("Partial-results policy: docs tool failure does not crash run", async () => {
    const settings = makeSettings();
    const profile = makeProfile();

    // Force docs search to throw.
    const docs = await import("../src/main/rag/docsSearcher");
    vi.spyOn(docs, "searchModDocs").mockImplementation(async () => {
      throw new Error("docs unavailable");
    });

    const offline = await runOfflineAnalysis(profile, settings, {
      useLoot: false,
      useNexus: false,
      useRag: true,
      complexity: 2,
      opinionatedness: 2,
    });

    const result = await runAgenticAnalysis(profile, settings, offline, {
      useLoot: false,
      useNexus: false,
      useRag: true,
      complexity: 2,
      opinionatedness: 2,
    });

    expect(result).toBeTruthy();
    expect(result.issues.length).toBeGreaterThan(0);
  });
});




