import path from "node:path";
import { describe, expect, it } from "vitest";
import { scanProfile } from "../src/main/mo2/mo2Scanner";
import { buildVfsIndex } from "../src/main/mo2/vfsIndex";
import { stage2ReduceToCandidates } from "../src/main/agent/orchestrator/stage2ReduceToCandidates";
import { budgetsForFlags } from "../src/main/agent/orchestrator/types";

describe("VFS index (Track A)", () => {
  it("respects MO2 left-pane priority (later wins) and ignores .mohidden", async () => {
    const instancePath = path.resolve(process.cwd(), "test/fixtures/mo2/priority_order");
    const profile = await scanProfile(instancePath, "Test");

    const vfs = await buildVfsIndex({ profile, scope: "hotspots" });

    // ModB is later in modlist.txt, so it should overwrite ModA on interface/test.swf.
    const edge = vfs.edgeCounts?.ModB?.ModA;
    expect(edge).toBeDefined();
    expect(edge?.byCategory?.interface ?? 0).toBe(1);

    // Ensure hidden files do not create conflicts.
    const samples = vfs.edgeSamples?.ModB?.ModA ?? [];
    expect(samples.some((p) => p.toLowerCase().includes("test2.swf"))).toBe(false);
  });

  it("emits deterministic file-conflict candidates from VFS edges", async () => {
    const instancePath = path.resolve(process.cwd(), "test/fixtures/mo2/priority_order");
    const profile = await scanProfile(instancePath, "Test");
    const vfs = await buildVfsIndex({ profile, scope: "hotspots" });

    const input = {
      profile,
      offlineIssues: [],
      offlineRecommendations: [],
      settings: {
        mo2Instances: [],
        lootMode: "auto",
        analysisDefaults: { useLoot: false, useNexus: false, useRag: false, complexity: 3, opinionatedness: 5 },
        logLevel: "error",
      },
      vfs,
      flags: { useLoot: false, useNexus: false, useRag: false, complexity: 3, opinionatedness: 5 },
    } as const;

    const ctx = {
      runId: "test",
      startedAt: new Date().toISOString(),
      flags: input.flags,
      budgets: budgetsForFlags(input.flags),
      counters: { toolCalls: 0, modelCalls: 0 },
    };

    const { candidates } = await stage2ReduceToCandidates(
      input as any,
      ctx as any,
      {
        pluginToModIds: {},
        baseline: { issueCount: 0, recommendationCount: 0, categories: {}, affectedModIds: [], affectedPlugins: [] },
        interestingMods: [],
      },
      [],
    );

    expect(candidates.some((c) => c.kind === "file_conflict_interface")).toBe(true);
  });
});

