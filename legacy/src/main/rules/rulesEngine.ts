import type {
  Issue,
  LootIssuePluginMessage,
  ModInfo,
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

  // Nexus-enriched heuristic: multiple large/broad mods affecting the same domains.
  // "Importance" alone is not enough; this attempts to focus on overlap risk.
  const broadMods = profile.mods.filter(
    (mod) =>
      mod.enabled &&
      mod.scopeHint === "broad" &&
      (mod.categoryGroup === "overhaul_like" || mod.categoryGroup === "framework_like" || mod.categoryGroup === "content_like"),
  );

  const byTag = new Map<string, ModInfo[]>();
  for (const mod of broadMods) {
    const tags = mod.overlapTagsAgent ?? mod.overlapTags ?? [];
    for (const tag of tags) {
      // Location targets are handled separately; avoid counting "locations" as a
      // broad overlap unless there is an explicit target match.
      if (tag.startsWith("location:")) continue;
      const key = String(tag);
      const arr = byTag.get(key) ?? [];
      arr.push(mod);
      byTag.set(key, arr);
    }
  }

  const overlappingTags = Array.from(byTag.entries())
    .filter(([, mods]) => mods.length >= 2)
    .sort((a, b) => b[1].length - a[1].length);

  // Location-specific overlaps: only count when the same location target key matches.
  const locationMods = profile.mods.filter(
    (m) =>
      m.enabled &&
      ((m.overlapTagsAgent ?? m.overlapTags ?? []).some((t) => t.startsWith("location:"))),
  );
  const byLocationTarget = new Map<string, ModInfo[]>();
  for (const mod of locationMods) {
    const tags = mod.overlapTagsAgent ?? mod.overlapTags ?? [];
    for (const tag of tags) {
      if (!tag.startsWith("location:")) continue;
      const arr = byLocationTarget.get(tag) ?? [];
      arr.push(mod);
      byLocationTarget.set(tag, arr);
    }
  }
  const overlappingLocations = Array.from(byLocationTarget.entries())
    .filter(([, mods]) => mods.length >= 2)
    .sort((a, b) => b[1].length - a[1].length);

  if (overlappingTags.length || overlappingLocations.length) {
    const affected = new Map<string, ModInfo>();
    for (const [, mods] of overlappingTags) {
      for (const mod of mods) affected.set(mod.id, mod);
    }
    for (const [, mods] of overlappingLocations) {
      for (const mod of mods) affected.set(mod.id, mod);
    }
    const affectedMods = Array.from(affected.values());
    const domainPreview = overlappingTags
      .slice(0, 5)
      .map(([tag, mods]) => `${tag} (${mods.length})`)
      .join(", ");
    const locationPreview = overlappingLocations
      .slice(0, 3)
      .map(([tag, mods]) => `${tag.replace(/^location:/, "location:")} (${mods.length})`)
      .join(", ");

    const issue = buildIssue({
      severity: "medium",
      category: "soft_conflict",
      summary: "Multiple large mods may overlap in purpose",
      details:
        `Several enabled, broad-scope mods appear to touch the same domains based on their Nexus metadata/description cues. ` +
        `This increases conflict risk and troubleshooting complexity. Overlaps: ${[domainPreview, locationPreview].filter(Boolean).join("; ")}.`,
      affectedMods: affectedMods.map((m) => m.id),
      affectedPlugins: affectedMods.flatMap((m) => m.plugins),
      risky: true,
      confidence: "low",
      source: ["rules", "nexus"],
      overlapGroups: [
        ...overlappingTags.map(([tag, mods]) => ({ tag, modIds: mods.map((m) => m.id) })),
        ...overlappingLocations.map(([tag, mods]) => ({ tag, modIds: mods.map((m) => m.id) })),
      ],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Pick one domain at a time (e.g. combat, perks, survival) and confirm which mod you want as the 'owner' of that domain.",
        "Check each mod’s description for required patches and load order notes.",
        "If you intentionally stack multiple domain mods, ensure you have compatibility patches and a clear troubleshooting plan.",
      ],
    });
  }

  // AI-enriched: requirements / patch requirements from descriptions.
  const missingRequirements: Array<{
    modId: string;
    requirement: NonNullable<ModInfo["requirementsAgent"]>[number];
  }> = [];

  const enabledModsByIdLower = new Map<string, ModInfo>();
  profile.mods.forEach((m) => {
    if (!m.enabled) return;
    enabledModsByIdLower.set(m.id.toLowerCase(), m);
  });

  const enabledModsByNameLower = new Map<string, ModInfo>();
  profile.mods.forEach((m) => {
    if (!m.enabled) return;
    enabledModsByNameLower.set((m.name || m.id).toLowerCase(), m);
  });

  const enabledPluginsLower = new Set<string>();
  profile.mods.forEach((m) => {
    if (!m.enabled) return;
    (m.plugins ?? []).forEach((p) => enabledPluginsLower.add(p.toLowerCase()));
  });

  profile.mods.forEach((m) => {
    if (!m.enabled) return;
    const reqs = m.requirementsAgent ?? [];
    reqs.forEach((req) => {
      if (req.kind !== "required" && req.kind !== "patch") return;

      // Skip low-confidence requirements to avoid noisy false positives.
      if (req.confidence === "low") return;

      // If the requirement points to a plugin, we can check it deterministically.
      if (req.targetPlugin) {
        const has = enabledPluginsLower.has(req.targetPlugin.toLowerCase());
        if (!has) missingRequirements.push({ modId: m.id, requirement: req });
        return;
      }

      // If the requirement points to a mod id, check enabled mods.
      if (req.targetModId) {
        const has = enabledModsByIdLower.has(req.targetModId.toLowerCase());
        if (!has) missingRequirements.push({ modId: m.id, requirement: req });
        return;
      }

      // If only a name is provided, treat as medium confidence unless exact match.
      if (req.targetModName) {
        const nameLower = req.targetModName.toLowerCase();
        const exact = enabledModsByNameLower.has(nameLower);
        if (!exact && req.confidence === "high") {
          // At high confidence, still allow fuzzy includes check.
          const anyMatch = profile.mods.some(
            (mm) => mm.enabled && (mm.name || mm.id).toLowerCase().includes(nameLower),
          );
          if (!anyMatch) missingRequirements.push({ modId: m.id, requirement: req });
        } else if (!exact && req.confidence === "medium") {
          // Only flag if there is zero substring match (otherwise ambiguous).
          const anyMatch = profile.mods.some(
            (mm) => mm.enabled && (mm.name || mm.id).toLowerCase().includes(nameLower),
          );
          if (!anyMatch) missingRequirements.push({ modId: m.id, requirement: req });
        }
      }
    });
  });

  if (missingRequirements.length) {
    const affected = new Set<string>();
    const detailsLines: string[] = [];
    missingRequirements.slice(0, 30).forEach(({ modId, requirement }) => {
      affected.add(modId);
      const target =
        requirement.targetModId ??
        requirement.targetPlugin ??
        requirement.targetModName ??
        "<unknown requirement>";
      detailsLines.push(`- ${modId}: missing ${requirement.kind} -> ${target} (${requirement.confidence})`);
    });

    const issue = buildIssue({
      severity: "medium",
      category: "configuration",
      subcategory: "missing_requirements_from_descriptions",
      summary: "Mods may be missing required dependencies or patches",
      details:
        "Some mod descriptions indicate required dependencies or patches that do not appear to be enabled in this profile.\n\n" +
        detailsLines.join("\n") +
        (missingRequirements.length > 30 ? `\n\n(+${missingRequirements.length - 30} more)` : ""),
      affectedMods: Array.from(affected),
      affectedPlugins: [],
      risky: true,
      confidence: "low",
      source: ["rules", "nexus", "agent"],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Open each affected mod’s Nexus description and confirm the stated requirements/patches.",
        "Install/enable the missing dependency or compatibility patch, or remove the mod if you don’t want that dependency.",
        "Re-run analysis after changes to confirm the requirements are satisfied.",
      ],
    });
  }

  // AI-enriched: variant correctness.
  const variantMismatches = profile.mods.filter(
    (m) => m.enabled && m.variantAgent?.mismatch && (m.variantAgent.confidence === "high" || m.variantAgent.confidence === "medium"),
  );
  if (variantMismatches.length) {
    const issue = buildIssue({
      severity: "high",
      category: "outdated_or_wrong_version",
      subcategory: "variant_mismatch",
      summary: "Possible wrong mod variant installed (SE/AE/module mismatch)",
      details:
        "Some mods appear to have a variant/module mismatch based on description/file metadata. Double-check installed options (SE vs AE, lite vs full, Nemesis vs FNIS, etc.).",
      affectedMods: variantMismatches.map((m) => m.id),
      affectedPlugins: variantMismatches.flatMap((m) => m.plugins),
      risky: true,
      confidence: "low",
      source: ["rules", "nexus", "agent"],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Open the Nexus Files tab for each affected mod and confirm the intended file for your runtime/setup.",
        "Reinstall the mod using the correct variant/module and ensure conflicting variants are not simultaneously enabled.",
        "If the mod includes a DLL, verify it matches your SKSE/runtime version.",
      ],
    });
  }

  // AI-enriched: script/performance risk.
  const highScriptPerf = profile.mods.filter(
    (m) => m.enabled && m.scriptPerfRiskAgent?.level === "high",
  );
  if (highScriptPerf.length) {
    const issue = buildIssue({
      severity: "medium",
      category: "script_load",
      subcategory: "ai_script_perf_risk",
      summary: "One or more mods are flagged as high script/performance risk",
      details:
        "Based on mod descriptions (and, at higher complexity, file contents), some enabled mods appear to be script-heavy or performance-risky. This can increase stutter, script lag, or instability when stacked.",
      affectedMods: highScriptPerf.map((m) => m.id),
      affectedPlugins: highScriptPerf.flatMap((m) => m.plugins),
      risky: false,
      confidence: "low",
      source: ["rules", "nexus", "agent"],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Review the descriptions for the flagged mods for known performance notes and recommended settings.",
        "Avoid stacking multiple script-heavy overhauls unless you have a clear compatibility plan.",
        "Consider lowering update frequencies, disabling optional features, or trimming non-essential script-heavy mods.",
      ],
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

  const getNexusBucket = (mod: ModInfo): Record<string, unknown> | undefined => {
    const raw = mod.metadata as { nexus?: unknown } | undefined;
    if (!raw || typeof raw !== "object") {
      return undefined;
    }

    const value = raw.nexus;
    if (!value || typeof value !== "object") {
      return undefined;
    }

    return value as Record<string, unknown>;
  };

  const compareVersionStrings = (a: string, b: string): number | null => {
    const normalize = (v: string): number[] | null => {
      const cleaned = v.trim();
      if (!cleaned) return null;
      const parts = cleaned.split(/[.\-_\s]+/).map((part) => {
        const numeric = parseInt(part.replace(/[^\d]/g, ""), 10);
        return Number.isNaN(numeric) ? 0 : numeric;
      });
      return parts.length ? parts : null;
    };

    const aParts = normalize(a);
    const bParts = normalize(b);
    if (!aParts || !bParts) return null;

    const len = Math.max(aParts.length, bParts.length);
    for (let i = 0; i < len; i += 1) {
      const av = aParts[i] ?? 0;
      const bv = bParts[i] ?? 0;
      if (av > bv) return 1;
      if (av < bv) return -1;
    }
    return 0;
  };

  // Nexus-backed rules: these only trigger when Nexus enrichment has populated
  // ModInfo with additional metadata. If no Nexus data is present, the rules
  // simply do nothing.
  const hasAnyNexusData = profile.mods.some((mod) => Boolean(getNexusBucket(mod)));

  interface OutdatedModEntry {
    mod: ModInfo;
    installed: string;
    latest: string;
    url?: string;
  }

  interface LowEndorsementEntry {
    mod: ModInfo;
    downloads: number;
    endorsements: number;
    url?: string;
  }

  const outdatedMods: OutdatedModEntry[] = [];
  const lowEndorsementMods: LowEndorsementEntry[] = [];

  if (hasAnyNexusData) {
    profile.mods
      .filter((mod) => mod.enabled)
      .forEach((mod) => {
        const nexusMeta = getNexusBucket(mod);

        // LE-only mod used in an SE/AE profile (or vice versa).
        const gameSupport = mod.gameSupport;
        if (gameSupport) {
          const isLeInSeAe =
            gameSupport === "SkyrimLE" &&
            (profile.game === "SkyrimSE" || profile.game === "SkyrimAE");
          const isSeInLe = gameSupport === "SkyrimSE" && profile.game === "SkyrimLE";

          if (isLeInSeAe || isSeInLe) {
            const gameLabel =
              gameSupport === "SkyrimLE"
                ? "Skyrim LE (Oldrim)"
                : gameSupport === "SkyrimSE"
                  ? "Skyrim Special Edition"
                  : "a different Skyrim edition";

            const issue = buildIssue({
              severity: "high",
              category: "hard_incompatibility",
              summary: `${mod.name} appears incompatible with this game's edition`,
              details:
                `${mod.name} is marked as targeting ${gameLabel} based on Nexus metadata, ` +
                `but this profile is for ${profile.game}. Mixing LE and SE/AE mods can lead to crashes and corruption.`,
              affectedMods: [mod.id],
              affectedPlugins: mod.plugins,
              risky: true,
              confidence: "high",
              source: ["rules", "nexus"],
              evidence: {
                nexusModUrl: (nexusMeta?.url as string | undefined) ?? undefined,
                nexusModId: typeof mod.nexusId === "number" ? mod.nexusId : undefined,
              },
            });
            issues.push(issue);
            recommendations.push({
              issueId: issue.id,
              steps: [
                "Verify the mod's Nexus page to confirm which game editions it supports.",
                "Install the correct port or version for this game edition, or disable the mod in this profile.",
              ],
            });
          }
        }

        const url = (nexusMeta?.url as string | undefined) ?? undefined;

        // Outdated installed version vs latest Nexus version: collect for aggregation.
        const installed = mod.installedVersion?.trim();
        const latest = mod.latestVersion?.trim();
        if (installed && latest && installed !== latest) {
          const cmp = compareVersionStrings(installed, latest);
          // If comparison fails, treat as outdated; otherwise require installed < latest.
          if (cmp === null || cmp < 0) {
            outdatedMods.push({ mod, installed, latest, url });
          }
        }

        // Optional, conservative Nexus-based heuristics: collect low endorsements separately.
        const downloads = Number((nexusMeta?.downloads as number | undefined) ?? 0);
        const endorsements = Number((nexusMeta?.endorsements as number | undefined) ?? 0);
        if (downloads >= 20000 && endorsements >= 0) {
          const ratio = downloads > 0 ? endorsements / downloads : 0;
          if (ratio > 0 && ratio < 0.01) {
            lowEndorsementMods.push({ mod, downloads, endorsements, url });
          }
        }
      });
  }

  if (outdatedMods.length) {
    const affectedMods = outdatedMods.map((entry) => entry.mod.id);
    const affectedPlugins = outdatedMods.flatMap((entry) => entry.mod.plugins);

    const lines = outdatedMods.map((entry) => {
      const versionPart = `"${entry.installed}" → "${entry.latest}"`;
      const urlPart = entry.url ? ` (Nexus: ${entry.url})` : "";
      return `- ${entry.mod.name}: ${versionPart}${urlPart}`;
    });

    const summary =
      outdatedMods.length === 1
        ? `${outdatedMods[0].mod.name} appears outdated compared to the latest Nexus version`
        : `${outdatedMods.length} mods appear outdated compared to their latest Nexus versions`;

    const details =
      (outdatedMods.length === 1
        ? "The following mod appears outdated based on its installed version compared to the latest version reported on Nexus:\n\n"
        : "The following mods appear outdated based on their installed versions compared to the latest versions reported on Nexus:\n\n") +
      lines.join("\n") +
      "\n\nUpdating may fix bugs, improve compatibility, or add features. Review each mod's changelog and compatibility notes before updating.";

    const issue = buildIssue({
      severity: "medium",
      category: "outdated_or_wrong_version",
      summary,
      details,
      affectedMods,
      affectedPlugins,
      risky: false,
      confidence: "medium",
      source: ["rules", "nexus"],
    });
    issues.push(issue);
    recommendations.push({
      issueId: issue.id,
      steps: [
        "Review the list of outdated mods above.",
        "For each mod, open its Nexus page and read the changelog and compatibility notes.",
        "If appropriate for your setup, update the mod to the latest version, then re-run LOOT (if used) and this analysis.",
      ],
    });
  }

  if (lowEndorsementMods.length) {
    const affectedMods = lowEndorsementMods.map((entry) => entry.mod.id);
    const affectedPlugins = lowEndorsementMods.flatMap((entry) => entry.mod.plugins);

    const lines = lowEndorsementMods.map((entry) => {
      const stats = `${entry.endorsements} endorsements / ${entry.downloads} downloads`;
      const urlPart = entry.url ? ` (Nexus: ${entry.url})` : "";
      return `- ${entry.mod.name}: ${stats}${urlPart}`;
    });

    const details =
      "Compared to their total downloads, these mods have relatively few endorsements on Nexus. " +
      "This does not prove that any mod is bad or unsafe, but it can be a soft signal that users may have mixed experiences.\n\n" +
      "Treat this as a low-confidence heuristic: if you are actively troubleshooting issues or curating for quality, you may want to review comments and consider alternatives for the following mods:\n\n" +
      lines.join("\n");

    const issue = buildIssue({
      severity: "low",
      category: "other",
      summary: "Some mods have unusually low endorsements compared to downloads on Nexus",
      details,
      affectedMods,
      affectedPlugins,
      risky: false,
      confidence: "low",
      source: ["rules", "nexus"],
    });
    issues.push(issue);
  }

  return { issues, recommendations };
};
