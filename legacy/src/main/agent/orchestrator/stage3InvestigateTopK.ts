import type { Issue, Recommendation } from "../../../shared/types";
import { logger } from "../../logging";
import { NexusClient, NexusError } from "../../nexus/nexusClient";
import { searchModDocs } from "../../rag/docsSearcher";
import type {
  IssueCandidate,
  OrchestratorInput,
  OrchestratorRunContext,
} from "./types";

type ToolCallLog = {
  tool: string;
  argsPreview: string;
  ok: boolean;
  durationMs: number;
  error?: string;
};

const truncate = (text: string, max: number): string =>
  text.length <= max ? text : `${text.slice(0, max)}…`;

const severityFromScore = (severity: number): Issue["severity"] => {
  if (severity >= 0.9) return "critical";
  if (severity >= 0.75) return "high";
  if (severity >= 0.5) return "medium";
  if (severity >= 0.25) return "low";
  return "suggestion";
};

const confidenceFromScore = (confidence: number): Issue["confidence"] => {
  if (confidence >= 0.7) return "high";
  if (confidence >= 0.45) return "medium";
  return "low";
};

const normalizeCategory = (
  kind: string,
): { category: string; categoryNormalized: string } => {
  switch (kind) {
    case "overlap":
      return { category: "overlap", categoryNormalized: "soft_conflict" };
    case "file_conflict_interface":
    case "file_conflict_scripts":
    case "file_conflict_skse":
      return { category: kind, categoryNormalized: "soft_conflict" };
    case "missing_requirement":
      return { category: "missing_requirement", categoryNormalized: "configuration" };
    case "variant_mismatch":
      return { category: "variant_mismatch", categoryNormalized: "outdated_or_wrong_version" };
    case "script_perf":
      return { category: "script_perf", categoryNormalized: "script_load" };
    case "overwrite_nonempty":
      return { category: "overwrite_nonempty", categoryNormalized: "configuration" };
    default:
      return { category: kind, categoryNormalized: "other" };
  }
};

const buildIssueFromCandidate = (candidate: IssueCandidate): Issue => {
  const { category, categoryNormalized } = normalizeCategory(candidate.kind);
  const severity = severityFromScore(candidate.score.severity);
  const confidence = confidenceFromScore(candidate.score.confidence);

  const risky =
    candidate.kind === "missing_requirement" ||
    candidate.kind === "variant_mismatch" ||
    candidate.kind === "overlap";

  const detailsLines: string[] = [
    candidate.hypothesis,
    "",
    "Evidence:",
    ...candidate.evidenceRefs.slice(0, 8).map((e) => `- ${e.snippet}${e.url ? ` (${e.url})` : ""}`),
  ];

  return {
    id: `agent-${candidate.id}`,
    severity,
    category,
    categoryNormalized,
    summary: candidate.hypothesis,
    details: truncate(detailsLines.join("\n"), 4000),
    affectedMods: candidate.affectedModIds,
    affectedPlugins: candidate.affectedPlugins,
    risky,
    confidence,
    source: ["agent"],
    facets: candidate.systemsAffected.map((tag) => {
      const [kindRaw, valueRaw] = tag.split(":");
      return {
        kind: kindRaw || "tag",
        value: valueRaw || tag,
        confidence,
        evidence: [],
      };
    }),
    supportLinks: candidate.evidenceRefs
      .filter((e) => Boolean(e.url))
      .slice(0, 6)
      .map((e) => ({ kind: e.source, url: e.url as string, label: e.snippet })),
    evidenceRefs: candidate.evidenceRefs.slice(0, 12),
  };
};

const buildRecommendationsForIssue = (issue: Issue): Recommendation[] => {
  const kind = issue.category;

  if (kind === "overlap") {
    return [
      {
        issueId: issue.id,
        steps: [
          "Pick one mod to be the primary owner of the overlapping system (e.g., combat/UI/lighting).",
          "Check each mod’s description for required compatibility patches and load order guidance.",
          "If stacking intentionally, install patches and test one domain at a time to isolate conflicts.",
        ],
      },
    ];
  }
  if (kind.startsWith("file_conflict_")) {
    return [
      {
        issueId: issue.id,
        steps: [
          "Identify which mod should win for the conflicting files (UI/scripts/SKSE plugin) based on the mod authors’ compatibility guidance.",
          "Install any required compatibility patch for the losing mod (or swap to a compatible alternative).",
          "If this is a UI/SWF conflict, ensure you are using the correct preset/patch set for your UI stack (SkyUI/TrueHUD/moreHUD/etc.).",
        ],
      },
    ];
  }
  if (kind === "missing_requirement") {
    return [
      {
        issueId: issue.id,
        steps: [
          "Open the mod’s Nexus description and confirm listed requirements/patches.",
          "Install/enable the missing dependency or compatibility patch.",
          "Re-run analysis after changes to confirm the requirement is satisfied.",
        ],
      },
    ];
  }
  if (kind === "variant_mismatch") {
    return [
      {
        issueId: issue.id,
        steps: [
          "Open the mod’s Nexus Files tab and confirm the intended file/variant for your runtime/setup (SE vs AE, module options).",
          "Reinstall the correct variant and ensure conflicting variants aren’t simultaneously enabled.",
          "If a DLL/SKSE plugin is involved, verify it matches your SKSE/runtime version.",
        ],
      },
    ];
  }
  if (kind === "script_perf") {
    return [
      {
        issueId: issue.id,
        steps: [
          "Review the mod’s description for performance notes and recommended settings.",
          "If multiple script-heavy mods are stacked, trim non-essential features/mods first.",
          "Monitor for script lag/stutter and consider lowering update frequencies where applicable.",
        ],
      },
    ];
  }
  if (kind === "overwrite_nonempty") {
    return [
      {
        issueId: issue.id,
        steps: [
          "In MO2, create a dedicated output mod (e.g., \"Nemesis Output\" / \"DynDOLOD Output\" / \"Synthesis Output\").",
          "Configure the generating tool to \"Create files in mod instead of overwrite\" or move existing files out of overwrite into the output mod.",
          "Keep overwrite empty during normal play; re-run analysis after moving outputs to confirm conflicts are understood and controlled.",
        ],
      },
    ];
  }
  return [
    {
      issueId: issue.id,
      steps: ["Review the evidence and verify the issue in MO2/LOOT.", "Apply the recommended fixes and re-run analysis."],
    },
  ];
};

const safeArgsPreview = (args: unknown): string => {
  try {
    return truncate(JSON.stringify(args), 300);
  } catch {
    return truncate(String(args), 300);
  }
};

export const stage3InvestigateTopK = async (
  input: OrchestratorInput,
  ctx: OrchestratorRunContext,
  candidates: IssueCandidate[],
): Promise<{
  selectedCandidateIds: string[];
  investigated: IssueCandidate[];
  issues: Issue[];
  recommendations: Recommendation[];
  toolCalls: ToolCallLog[];
  issueMappings: Array<{ candidateId: string; issueId: string }>;
}> => {
  const sorted = [...candidates].sort((a, b) => b.score.total - a.score.total);
  const topK = sorted.slice(0, ctx.budgets.maxInvestigations);
  const selectedCandidateIds = topK.map((c) => c.id);

  const nexus =
    input.flags.useNexus && input.settings.nexusApiKey
      ? new NexusClient(input.settings, input.profile.game)
      : null;

  const toolCalls: ToolCallLog[] = [];
  const investigated: IssueCandidate[] = [];
  const issues: Issue[] = [];
  const recommendations: Recommendation[] = [];
  const issueMappings: Array<{ candidateId: string; issueId: string }> = [];

  for (const cand of topK) {
    if (ctx.counters.toolCalls >= ctx.budgets.maxToolCalls) {
      await logger.warn(
        `[Orchestrator][Stage3] Tool-call budget exhausted; stopping investigation at ${investigated.length}/${topK.length}`,
      );
      break;
    }

    const enrichedRefs = [...cand.evidenceRefs];

    for (const step of cand.investigationPlan) {
      if (ctx.counters.toolCalls >= ctx.budgets.maxToolCalls) break;

      const started = Date.now();
      const argsPreview = safeArgsPreview(step.args);

      try {
        if (step.tool === "get_nexus_mod_files") {
          if (!nexus) continue;
          ctx.counters.toolCalls += 1;
          const files = await nexus.getModFiles(step.args.nexusId);
          const preview = files
            .slice(0, 6)
            .map((f) => `${f.category}:${f.name} (v${f.version})`)
            .join("; ");
          if (preview) {
            enrichedRefs.push({
              source: "nexusFiles",
              snippet: `Nexus files preview: ${preview}`,
              url: `https://www.nexusmods.com/${input.profile.game === "SkyrimLE" ? "skyrim" : "skyrimspecialedition"}/mods/${step.args.nexusId}`,
            });
          }
        } else if (step.tool === "get_nexus_mod_file_contents_summary") {
          if (!nexus) continue;
          ctx.counters.toolCalls += 1;
          const summary = await nexus.getModFileContentsSummary({
            nexusId: step.args.nexusId,
            limit: step.args.maxEntries ?? 500,
          });
          if (summary) {
            enrichedRefs.push({
              source: "nexusFileContents",
              snippet:
                `Nexus file contents (sampled): scripts=${summary.byCategory.scripts}, ` +
                `meshes=${summary.byCategory.meshes}, textures=${summary.byCategory.textures}, ` +
                `animations=${summary.byCategory.animations}, other=${summary.byCategory.other}`,
            });
          }
        } else if (step.tool === "search_mod_docs") {
          ctx.counters.toolCalls += 1;
          const docs = await searchModDocs(step.args.query, { k: step.args.k });
          docs.slice(0, 3).forEach((d) => {
            enrichedRefs.push({
              source: "docs",
              url: d.sourceUrl,
              snippet: `Docs: ${truncate(d.text, 240)}`,
            });
          });
        }

        toolCalls.push({
          tool: step.tool,
          argsPreview,
          ok: true,
          durationMs: Date.now() - started,
        });
      } catch (e) {
        const message = (e as Error).message ?? String(e);
        toolCalls.push({
          tool: step.tool,
          argsPreview,
          ok: false,
          durationMs: Date.now() - started,
          error: message,
        });
        // Partial-results policy: keep going, but record the failure as evidence.
        enrichedRefs.push({
          source: "toolError",
          snippet: `Tool ${step.tool} failed: ${message}`,
        });

        if (e instanceof NexusError) {
          // If Nexus is failing, avoid spamming more calls on this candidate.
          break;
        }
      }
    }

    const investigatedCandidate: IssueCandidate = {
      ...cand,
      evidenceRefs: enrichedRefs.slice(0, 20),
    };

    investigated.push(investigatedCandidate);
    const issue = buildIssueFromCandidate(investigatedCandidate);
    issues.push(issue);
    issueMappings.push({ candidateId: investigatedCandidate.id, issueId: issue.id });
    recommendations.push(...buildRecommendationsForIssue(issue));
  }

  await logger.info(
    `[Orchestrator][Stage3] investigated=${investigated.length}/${topK.length} ` +
      `issues=${issues.length} recs=${recommendations.length} toolCalls=${toolCalls.length}`,
  );

  return { selectedCandidateIds, investigated, issues, recommendations, toolCalls, issueMappings };
};



