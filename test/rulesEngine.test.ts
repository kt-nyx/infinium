import { describe, expect, it } from "vitest";
import type { ProfileSnapshot } from "../src/shared/types";
import { evaluate } from "../src/main/rules/rulesEngine";

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
      plugins: ["Immersive Citizens.esp"]
    },
    {
      id: "AI Overhaul",
      name: "AI Overhaul",
      enabled: true,
      path: "C:/MO2/mods/AI Overhaul",
      plugins: ["AI Overhaul.esp"]
    }
  ],
  pluginLoadOrder: ["Skyrim.esm"],
  lootAvailable: false,
  nexusAvailable: false
};

describe("rulesEngine", () => {
  it("flags multiple AI overhauls", () => {
    const result = evaluate(baseProfile);
    expect(result.issues.some((issue) => issue.category === "soft_conflict")).toBe(true);
  });
});