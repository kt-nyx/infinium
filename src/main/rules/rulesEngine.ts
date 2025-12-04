import type {
  Issue,
  LootIssuePluginMessage,
  ProfileSnapshot,
  Recommendation,
  Severity,
} from "../../shared/types";
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

  // Build a quick lookup from plugin name -> IDs of mods that ship it. This lets
  // LOOT-derived issues report both affected plugins and affected mods for the UI.
  const pluginToModIds = new Map<string, Set<string>>();
  profile.mods.forEach((mod) => {
    mod.plugins.forEach((pluginName) => {
      const key = pluginName.toLowerCase();
      const existing = pluginToModIds.get(key) ?? new Set<string>();
      existing.add(mod.id);
      pluginToModIds.set(key, existing);
    });
  });

  const getModsForPlugin = (pluginName: string): string[] =>
    Array.from(pluginToModIds.get(pluginName.toLowerCase()) ?? []);

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

  if (lootReport?.warnings?.length) {
    const warnings = lootReport.warnings;
    // Suppress generic LOOT warnings that only restate conditions that already
    // have dedicated issues (e.g., missing masters and ambiguous load order),
    // to avoid duplicate, redundant issues in the UI.
    const filteredWarnings = warnings.filter((rawText) => {
      const text = rawText ?? "";

      // Already surfaced via the `missing_masters` issue.
      if (/missing master/i.test(text)) {
        return false;
      }

      // Already surfaced via the `ambiguous_load_order` issue when metadata
      // explicitly reports it.
      if (lootReport.metadata?.ambiguousLoadOrder && /ambiguous load order/i.test(text)) {
        return false;
      }

      return true;
    });

    if (filteredWarnings.length) {
      const issue = buildIssue({
        severity: "medium",
        category: "configuration",
        subcategory: "loot_general_warnings",
        summary: "LOOT reported general warnings",
        details:
          filteredWarnings.length === 1
            ? filteredWarnings[0]
            : `LOOT reported ${filteredWarnings.length} general warnings. Review the summary below and consider re-running LOOT directly for full context:\n\n- ${filteredWarnings.join(
                "\n- ",
              )}`,
        affectedMods: [],
        affectedPlugins: [],
        risky: false,
        confidence: "medium",
        source: ["loot"],
      });
      issues.push(issue);
    }
  }

  if (lootReport?.metadata?.ambiguousLoadOrder) {
    const issue = buildIssue({
      severity: "medium",
      category: "load_order",
      subcategory: "ambiguous_load_order",
      summary: "LOOT detected an ambiguous load order",
      details:
        "LOOT reports that the load order may be ambiguous, meaning multiple valid orders exist. " +
        "This can make diagnosing conflicts harder; review your mod list and consider letting LOOT fully sort the plugins, " +
        "then re-run this analysis.",
      affectedMods: [],
      affectedPlugins: [],
      risky: true,
      confidence: "medium",
      source: ["loot"],
    });
    issues.push(issue);
  }

  if (lootReport?.missingMasters?.length) {
    const missingMasters = lootReport.missingMasters;
    const totalPlugins = missingMasters.length;
    const totalMasters = missingMasters.reduce((count, entry) => count + entry.masters.length, 0);

    const issue = buildIssue({
      severity: "critical",
      category: "missing_masters",
      summary: "Some plugins are missing required masters",
      details:
        `LOOT reported ${totalMasters} missing masters across ${totalPlugins} plugins. ` +
        "Expand the list below to see which plugins are affected and which masters are missing.",
      affectedMods: Array.from(
        new Set(missingMasters.flatMap((entry) => getModsForPlugin(entry.plugin))),
      ),
      affectedPlugins: missingMasters.map((entry) => entry.plugin),
      risky: true,
      confidence: "high",
      source: ["loot"],
      lootMissingMasters: missingMasters.map((entry) => ({
        plugin: entry.plugin,
        masters: [...entry.masters],
      })),
    });

    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Review the list of plugins with missing masters.",
        "Reinstall or re-enable the missing masters listed for each plugin.",
        "Check for mod updates or compatibility patches that supply the required masters.",
      ],
    });
  }

  // Optionally leverage richer LOOT metadata when available, without breaking
  // existing callers. These rules are additive: if the helper does not provide
  // metadata.plugins, behaviour falls back to the original rules.
  if (lootReport?.metadata?.plugins?.length) {
    const pluginMessagesByPlugin = new Map<string, LootIssuePluginMessage[]>();
    const pluginsWithMessages: string[] = [];
    const modsWithMessages = new Set<string>();
    let hasPluginError = false;
    let hasPluginWarning = false;

    lootReport.metadata.plugins.forEach((plugin) => {
      // Dirty plugins: treat as a high-priority performance / stability risk.
      if (plugin.missingMasters && plugin.missingMasters.length) {
        // Already covered by missingMasters above; avoid duplicating issues.
      }

      const hasDirtyInfo =
        (
          lootReport.metadata?.rawLiblootMetadata as { dirtyPlugins?: string[] } | undefined
        )?.dirtyPlugins?.includes(plugin.name) ?? false;

      if (hasDirtyInfo) {
        const issue = buildIssue({
          severity: "high",
          category: "performance_risk",
          subcategory: "dirty_plugin",
          summary: `${plugin.name} is flagged as dirty by LOOT`,
          details:
            "LOOT reports that this plugin has ITMs or UDRs. Cleaning it with xEdit can reduce instability and conflicts.",
          affectedMods: [],
          affectedPlugins: [plugin.name],
          risky: true,
          confidence: "medium",
          source: ["loot"],
        });
        issues.push(issue);
        recommendations.push({
          issueId: issue.id,
          steps: [
            "Open the plugin in SSEEdit / xEdit.",
            "Apply automatic cleaning as recommended by current LOOT documentation.",
            "Re-run LOOT and this analysis after cleaning.",
          ],
        });
      }

      if (plugin.incompatibilities && plugin.incompatibilities.length) {
        const issue = buildIssue({
          severity: "high",
          category: "hard_incompatibility",
          summary: `${plugin.name} has known incompatibilities`,
          details: `LOOT metadata lists these incompatible files or plugins: ${plugin.incompatibilities.join(
            ", ",
          )}.`,
          affectedMods: [],
          affectedPlugins: [plugin.name],
          risky: true,
          confidence: "medium",
          source: ["loot"],
        });
        issues.push(issue);
        recommendations.push({
          issueId: issue.id,
          steps: [
            "Disable one of the incompatible plugins or install the recommended compatibility patch.",
            "Review the LOOT message details and mod pages for guidance.",
          ],
        });
      }

      // Collect LOOT plugin messages (errors, warnings, and informational
      // notes) into a single aggregated issue later, while still exposing the
      // full structured payload for the UI to display. Suppress messages that
      // merely restate missing masters to avoid duplicating the dedicated
      // missing-masters issue.
      if (plugin.messages && plugin.messages.length) {
        const missingMasterNames = new Set(
          (plugin.missingMasters ?? []).map((name) => name.toLowerCase()),
        );

        const filteredMessages = plugin.messages.filter((msg) => {
          const textLower = msg.text.toLowerCase();
          const mentionsMissingMasters =
            textLower.includes("missing master") || textLower.includes("missing masters");
          if (mentionsMissingMasters && missingMasterNames.size > 0) {
            // Treat this as redundant with the dedicated missing-masters
            // issue; don't surface it again as a generic LOOT message.
            return false;
          }
          return true;
        });

        if (!filteredMessages.length) {
          return;
        }

        const mappedMessages: LootIssuePluginMessage[] = filteredMessages.map((msg) => ({
          plugin: plugin.name,
          severity: msg.severity,
          text: msg.text,
          language: msg.language,
          condition: msg.condition,
        }));

        pluginMessagesByPlugin.set(plugin.name, mappedMessages);
        pluginsWithMessages.push(plugin.name);

        const owningMods = getModsForPlugin(plugin.name);
        owningMods.forEach((id) => modsWithMessages.add(id));

        if (filteredMessages.some((msg) => msg.severity === "error")) {
          hasPluginError = true;
        }
        if (filteredMessages.some((msg) => msg.severity === "warning")) {
          hasPluginWarning = true;
        }
      }
    });

    if (pluginMessagesByPlugin.size > 0) {
      const hasError = hasPluginError;
      const hasWarning = hasPluginWarning;
      const issueSeverity: Severity = hasError ? "high" : hasWarning ? "medium" : "suggestion";

      const summarySuffix = hasError || hasWarning ? "warnings" : "notes";

      const allMessages: LootIssuePluginMessage[] = [];
      pluginMessagesByPlugin.forEach((msgs) => {
        allMessages.push(...msgs);
      });

      const issue = buildIssue({
        severity: issueSeverity,
        category: "configuration",
        subcategory: "loot_plugin_messages",
        summary: `LOOT reported plugin ${summarySuffix}`,
        details:
          "LOOT reported messages for one or more plugins. Expand the list below to see the per-plugin " +
          "errors, warnings, and notes, then consult LOOT and the relevant mod pages for full guidance.",
        affectedMods: Array.from(modsWithMessages),
        affectedPlugins: Array.from(new Set(pluginsWithMessages)),
        risky: hasError || hasWarning,
        confidence: "medium",
        source: ["loot"],
        lootPluginMessages: allMessages,
      });

      issues.push(issue);
      recommendations.push({
        issueId: issue.id,
        steps: [
          "Open LOOT and review the full messages for each affected plugin.",
          "Check the mod descriptions and sticky posts for recommended patches or load order guidance.",
          "Install any required compatibility patches and re-run LOOT and this analysis.",
        ],
      });
    }
  }

  return { issues, recommendations };
};
