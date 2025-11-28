import type { Issue, ProfileSnapshot, Recommendation } from "../../shared/types";
import rulesData from "./rulesData.json";
import type { LootReport } from "../loot/lootManager";

interface EvaluationResult {
  issues: Issue[];
  recommendations: Recommendation[];
}

let issueCounter = 0;
const nextId = (prefix: string): string => `${prefix}-${(issueCounter += 1)}`;

const buildIssue = (
  partial: Omit<Issue, "id" | "source"> & { source: Issue["source"] },
): Issue => ({
  id: nextId("rule"),
  ...partial,
});

export const evaluate = (profile: ProfileSnapshot, lootReport?: LootReport): EvaluationResult => {
  issueCounter = 0;
  const issues: Issue[] = [];
  const recommendations: Recommendation[] = [];

  const legacyPatterns = (rulesData?.legacyMods ?? []).map(
    (entry) => new RegExp(entry.pattern, "i"),
  );

  profile.mods
    .filter((mod) => mod.enabled)
    .forEach((mod) => {
      const looksLegacy = legacyPatterns.some((pattern) => pattern.test(mod.name));
      if (looksLegacy && profile.game !== "SkyrimLE") {
        const issue = buildIssue({
          severity: "high",
          category: "outdated_or_wrong_version",
          summary: `${mod.name} may only support Skyrim LE`,
          details:
            "The mod name suggests it targets Oldrim / Skyrim LE. Verify compatibility or find an SE/AE port.",
          affectedMods: [mod.id],
          affectedPlugins: mod.plugins,
          risky: true,
          confidence: "medium",
          source: ["rules"],
        });
        issues.push(issue);
        recommendations.push({
          issueId: issue.id,
          steps: [
            "Check the mod page for SE/AE compatibility notes.",
            "Replace with a ported version or disable it in this profile.",
          ],
        });
      }
    });

  const aiOverhauls = new Set((rulesData?.aiOverhauls ?? []).flat());
  const enabledAiMods = profile.mods.filter((mod) => mod.enabled && aiOverhauls.has(mod.name));
  if (enabledAiMods.length >= 2) {
    const issue = buildIssue({
      severity: "medium",
      category: "soft_conflict",
      summary: "Multiple AI overhauls detected",
      details:
        "Running more than one major AI overhaul can lead to unpredictable behavior. Disable redundant mods or carefully merge patches.",
      affectedMods: enabledAiMods.map((mod) => mod.id),
      affectedPlugins: enabledAiMods.flatMap((mod) => mod.plugins),
      risky: true,
      confidence: "medium",
      source: ["rules"],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Pick one primary AI overhaul that matches your desired gameplay.",
        "Use compatibility patches if you intentionally keep multiple",
      ],
    });
  }

  const scriptHeavySet = new Set(rulesData?.scriptHeavyMods ?? []);
  const scriptHeavyEnabled = profile.mods.filter(
    (mod) => mod.enabled && scriptHeavySet.has(mod.name),
  );
  if (scriptHeavyEnabled.length >= 3) {
    const issue = buildIssue({
      severity: "medium",
      category: "script_load",
      summary: "Many script-heavy mods enabled",
      details:
        "This profile enables several mods known to add heavy Papyrus load. Monitor performance and consider trimming to avoid script lag.",
      affectedMods: scriptHeavyEnabled.map((mod) => mod.id),
      affectedPlugins: scriptHeavyEnabled.flatMap((mod) => mod.plugins),
      risky: false,
      confidence: "low",
      source: ["rules"],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Profile your Papyrus logs for spikes.",
        "Consider staggering quests or disabling non-essential scripted mods.",
      ],
      notes: "Rule-based heuristic; adjust once we capture real script metrics.",
    });
  }

  if (lootReport?.missingMasters?.length) {
    lootReport.missingMasters.forEach((entry) => {
      const issue = buildIssue({
        severity: "critical",
        category: "missing_masters",
        summary: `${entry.plugin} is missing masters`,
        details: `LOOT reported missing masters: ${entry.masters.join(", ")}`,
        affectedMods: [],
        affectedPlugins: [entry.plugin],
        risky: true,
        confidence: "high",
        source: ["loot"],
      });
      issues.push(issue);
      recommendations.push({
        issueId: issue.id,
        steps: [
          "Reinstall or re-enable the listed masters.",
          "Check for mod updates or patches addressing the missing dependency.",
        ],
      });
    });
  }

  return { issues, recommendations };
};
