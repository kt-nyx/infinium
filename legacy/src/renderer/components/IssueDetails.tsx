import {
  Button,
  Dialog,
  DialogBody,
  DialogSurface,
  DialogTitle,
  DialogTrigger,
  Divider,
  Tag,
  Text,
  Textarea,
} from "@fluentui/react-components";
import {
  ArrowMaximize24Regular,
  ChevronDown16Regular,
  ChevronRight16Regular,
  Dismiss24Regular,
  QuestionCircle24Regular,
} from "@fluentui/react-icons";
import type { Issue, ModInfo, Recommendation, Severity } from "../../shared/types";
import { memo, type CSSProperties, useMemo, useState } from "react";

type IssueChatMessage = {
  role: "user" | "assistant";
  content: string;
};

export interface IssueDetailsProps {
  issue?: Issue;
  recommendations: Recommendation[];
  mods: ModInfo[];
  onExpand: (issue: Issue) => void;
  expanding?: boolean;
  onClose?: () => void;
  chatMessages: IssueChatMessage[];
  chatInput: string;
  onChatInputChange: (value: string) => void;
  defaultPrompt: string;
}

const detailLabel: CSSProperties = {
  fontSize: 12,
  textTransform: "uppercase",
  color: "#a1a1aa",
};

const severityColors: Record<Severity, { bg: string; fg: string; label: string }> = {
  critical: { bg: "#7f1d1d", fg: "#fee2e2", label: "Critical" },
  high: { bg: "#b91c1c", fg: "#fee2e2", label: "High" },
  medium: { bg: "#9a3412", fg: "#ffedd5", label: "Medium" },
  low: { bg: "#0369a1", fg: "#e0f2fe", label: "Low" },
  suggestion: { bg: "#065f46", fg: "#d1fae5", label: "Suggestion" },
};

const IssueDetails = memo(function IssueDetails({
  issue,
  recommendations,
  mods,
  onExpand,
  expanding,
  onClose,
  chatMessages,
  chatInput,
  onChatInputChange,
  defaultPrompt,
}: IssueDetailsProps) {
  const [chatHeight, setChatHeight] = useState(260);
  const [showChatWindow, setShowChatWindow] = useState(false);
  const [chatWindowBounds, setChatWindowBounds] = useState({
    width: 1024,
    height: 640,
    top: 60,
    left: 80,
  });
  const [isDraggingWindow, setIsDraggingWindow] = useState(false);
  const [isResizingWindow, setIsResizingWindow] = useState(false);
  const [expandedMissingMasters, setExpandedMissingMasters] = useState<Record<string, boolean>>({});
  const [showMissingMastersSection, setShowMissingMastersSection] = useState(false);
  const [showAffectedMods, setShowAffectedMods] = useState(false);
  const [showAffectedPlugins, setShowAffectedPlugins] = useState(false);
  const [expandedLootPluginMessages, setExpandedLootPluginMessages] = useState<
    Record<string, boolean>
  >({});

  const handleChatResizeStart = (event: React.MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    const startY = event.clientY;
    const startHeight = chatHeight;

    const handleMouseMove = (ev: MouseEvent) => {
      const delta = ev.clientY - startY;
      let nextHeight = startHeight + delta;
      nextHeight = Math.max(140, Math.min(520, nextHeight));
      setChatHeight(nextHeight);
    };

    const handleMouseUp = () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
  };

  const handleWindowDragStart = (event: React.MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDraggingWindow(true);
    const startX = event.clientX;
    const startY = event.clientY;
    const { top, left } = chatWindowBounds;

    const handleMouseMove = (ev: MouseEvent) => {
      const deltaX = ev.clientX - startX;
      const deltaY = ev.clientY - startY;
      setChatWindowBounds((prev) => ({
        ...prev,
        left: Math.max(16, Math.min(window.innerWidth - 200, left + deltaX)),
        top: Math.max(16, Math.min(window.innerHeight - 120, top + deltaY)),
      }));
    };

    const handleMouseUp = () => {
      setIsDraggingWindow(false);
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
  };

  const handleWindowResizeStart = (event: React.MouseEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsResizingWindow(true);
    const startX = event.clientX;
    const startY = event.clientY;
    const { width, height } = chatWindowBounds;

    const handleMouseMove = (ev: MouseEvent) => {
      const deltaX = ev.clientX - startX;
      const deltaY = ev.clientY - startY;
      const nextWidth = Math.max(560, Math.min(window.innerWidth - 80, width + deltaX));
      const nextHeight = Math.max(320, Math.min(window.innerHeight - 80, height + deltaY));
      setChatWindowBounds((prev) => ({
        ...prev,
        width: nextWidth,
        height: nextHeight,
      }));
    };

    const handleMouseUp = () => {
      setIsResizingWindow(false);
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
    };

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);
  };

  if (!issue) {
    return <Text>Select an issue on the left to view details.</Text>;
  }

  const palette = severityColors[issue.severity];
  const hasCustomPrompt = chatInput.trim().length > 0;

  const missingMasterNames =
    issue.lootMissingMasters && issue.lootMissingMasters.length > 0
      ? Array.from(new Set(issue.lootMissingMasters.flatMap((entry) => entry.masters))).sort()
      : [];

  const isMissingMastersIssue =
    issue.category === "missing_masters" &&
    Array.isArray(issue.lootMissingMasters) &&
    issue.lootMissingMasters.length > 0;

  type MissingMastersModEntry = {
    modId: string;
    modName: string;
    plugins: {
      pluginName: string;
      masters: string[];
    }[];
  };

  let missingMastersByMod: MissingMastersModEntry[] = [];
  let totalMissingPluginsForMods = 0;

  if (isMissingMastersIssue) {
    const lootMissing = issue.lootMissingMasters ?? [];
    const lootByPlugin = new Map<string, { plugin: string; masters: string[] }>();
    lootMissing.forEach((entry) => {
      lootByPlugin.set(entry.plugin.toLowerCase(), {
        plugin: entry.plugin,
        masters: entry.masters,
      });
    });

    const affectedModIds = new Set(issue.affectedMods ?? []);

    const entries: MissingMastersModEntry[] = [];

    mods.forEach((mod) => {
      if (!affectedModIds.has(mod.id)) {
        return;
      }

      const pluginEntries: MissingMastersModEntry["plugins"] = [];

      (mod.plugins ?? []).forEach((pluginName) => {
        const match = lootByPlugin.get(pluginName.toLowerCase());
        if (match) {
          pluginEntries.push({
            pluginName: match.plugin,
            masters: match.masters,
          });
        }
      });

      if (pluginEntries.length > 0) {
        entries.push({
          modId: mod.id,
          modName: mod.name || mod.id,
          plugins: pluginEntries,
        });
      }
    });

    missingMastersByMod = entries;
    totalMissingPluginsForMods = entries.reduce((count, entry) => count + entry.plugins.length, 0);
  }

  const backedByNexus = issue.source.includes("nexus");
  const evidence = issue.evidence;
  const overlapGroups = issue.overlapGroups ?? [];
  const facets = issue.facets ?? [];
  const supportLinks = issue.supportLinks ?? [];
  const evidenceRefs = issue.evidenceRefs ?? [];

  const modsById = useMemo(() => {
    const map = new Map<string, ModInfo>();
    for (const m of mods) map.set(m.id, m);
    return map;
  }, [mods]);

  const overlapGroupsResolved = useMemo(() => {
    const importanceBucketScore = (bucket: ModInfo["importanceBucket"] | undefined): number => {
      if (bucket === "high") return 3;
      if (bucket === "medium") return 2;
      if (bucket === "low") return 1;
      return 0;
    };

    const scoreForMod = (m: ModInfo | undefined): number => {
      if (!m) return 0;
      if (typeof m.importanceScore === "number") return m.importanceScore;
      return importanceBucketScore(m.importanceBucket);
    };

    return overlapGroups
      .map((g) => {
        const resolved = (g.modIds ?? [])
          .map((id) => modsById.get(id))
          .filter((m): m is ModInfo => Boolean(m))
          .sort((a, b) => {
            const ds = scoreForMod(b) - scoreForMod(a);
            if (ds !== 0) return ds;
            return (a.name || a.id).localeCompare(b.name || b.id);
          });

        return { tag: g.tag, mods: resolved };
      })
      .filter((g) => g.mods.length > 0)
      .sort((a, b) => b.mods.length - a.mods.length || a.tag.localeCompare(b.tag));
  }, [overlapGroups, modsById]);

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: 12,
        padding: 16,
        borderRadius: 8,
        backgroundColor: "#f9fafb",
        border: "1px solid #e5e7eb",
        height: "100%",
        boxSizing: "border-box",
      }}
    >
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "flex-start",
          gap: 16,
        }}
      >
        <div style={{ display: "flex", gap: 24 }}>
          <div>
            <Text style={detailLabel}>Severity</Text>
            <div style={{ marginTop: 4 }}>
              <Tag
                size="medium"
                style={{ backgroundColor: palette.bg, color: palette.fg, fontWeight: 600 }}
              >
                {palette.label}
              </Tag>
            </div>
          </div>
          <div>
            <Text style={detailLabel}>Risk</Text>
            <Text>{issue.risky ? "Potentially game-breaking" : "Safe but noisy"}</Text>
          </div>
          <div>
            <Text style={detailLabel}>Confidence</Text>
            <Text>{issue.confidence}</Text>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          {backedByNexus && (
            <Tag
              size="small"
              shape="rounded"
              appearance="outline"
              style={{
                borderColor: "#2563eb",
                color: "#1d4ed8",
                backgroundColor: "transparent",
              }}
            >
              Backed by Nexus
            </Tag>
          )}
          {onClose && (
            <Button
              appearance="subtle"
              icon={<Dismiss24Regular />}
              onClick={onClose}
              style={{ color: "#b91c1c" }}
              aria-label="Close details"
            />
          )}
        </div>
      </div>
      <div>
        <Text style={detailLabel}>Category</Text>
        <Text>
          {issue.categoryNormalized ? `${issue.categoryNormalized} (${issue.category})` : issue.category}
        </Text>
      </div>
      {(facets.length > 0 || supportLinks.length > 0 || evidenceRefs.length > 0) && (
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: 10,
            padding: 10,
            borderRadius: 8,
            backgroundColor: "#ffffff",
            border: "1px solid #e5e7eb",
          }}
        >
          {facets.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <Text style={detailLabel}>Facets</Text>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
                {facets.slice(0, 24).map((f, idx) => (
                  <Tag key={`${f.kind}:${f.value}:${idx}`} size="small" shape="rounded">
                    {`${f.kind}:${f.value}`}{" "}
                    <span style={{ opacity: 0.75 }}>{`(${f.confidence})`}</span>
                  </Tag>
                ))}
              </div>
            </div>
          )}
          {supportLinks.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
              <Text style={detailLabel}>Support links</Text>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {supportLinks.slice(0, 10).map((l) => (
                  <li key={`${l.kind}:${l.url}`}>
                    <Text size={200}>
                      <a href={l.url} target="_blank" rel="noreferrer">
                        {l.label ?? l.url}
                      </a>{" "}
                      <span style={{ color: "#6b7280" }}>{`(${l.kind})`}</span>
                    </Text>
                  </li>
                ))}
              </ul>
            </div>
          )}
          {evidenceRefs.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
              <Text style={detailLabel}>Evidence</Text>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {evidenceRefs.slice(0, 12).map((e, idx) => (
                  <li key={`${e.source}:${e.modId ?? ""}:${idx}`}>
                    <Text size={200}>
                      {e.snippet}
                      {e.url && (
                        <>
                          {" "}
                          <a href={e.url} target="_blank" rel="noreferrer">
                            source
                          </a>
                        </>
                      )}{" "}
                      <span style={{ color: "#6b7280" }}>
                        {`(${e.source}${e.modId ? ` • ${e.modId}` : ""})`}
                      </span>
                    </Text>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
      {evidence && (evidence.nexusModUrl || evidence.nexusCollectionSlug) && (
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            gap: 4,
            padding: 8,
            borderRadius: 6,
            backgroundColor: "#eff6ff",
            border: "1px solid #bfdbfe",
          }}
        >
          <Text style={{ fontSize: 12, fontWeight: 600, color: "#1d4ed8" }}>
            Nexus evidence
          </Text>
          {evidence.nexusModUrl && (
            <Text size={200}>
              <a href={evidence.nexusModUrl} target="_blank" rel="noreferrer">
                View mod on Nexus
              </a>
              {evidence.nexusFileVersion && ` • Latest version: ${evidence.nexusFileVersion}`}
            </Text>
          )}
          {evidence.nexusCollectionSlug && (
            <Text size={200}>
              Collection:{" "}
              <a
                href={`https://www.nexusmods.com/collections/${encodeURIComponent(
                  evidence.nexusCollectionSlug,
                )}`}
                target="_blank"
                rel="noreferrer"
              >
                {evidence.nexusCollectionSlug}
              </a>
            </Text>
          )}
        </div>
      )}
      <div>
        <Text style={detailLabel}>Details</Text>
        <Text>{issue.details}</Text>
      </div>
      {overlapGroupsResolved.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          <Text style={detailLabel}>Overlaps (grouped)</Text>
          <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
            {overlapGroupsResolved.map((group) => (
              <div
                key={group.tag}
                style={{
                  border: "1px solid #e5e7eb",
                  borderRadius: 8,
                  padding: 10,
                  background: "#fafafa",
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
                  <Text weight="semibold">
                    {(() => {
                      const tag = group.tag;
                      if (tag.startsWith("location:")) {
                        return `Location: ${tag.replace(/^location:/, "").replace(/_/g, " ")}`;
                      }
                      const labelMap: Record<string, string> = {
                        "system:ai": "System: AI / NPC behavior",
                        "system:combat": "System: Combat",
                        "system:perks": "System: Perks / leveling",
                        "system:survival": "System: Survival / needs",
                        "system:animations": "System: Animations",
                        "system:skeleton": "System: Skeleton",
                        "system:loot_balance": "System: Loot / leveled lists",
                        "system:followers": "System: Followers",
                        "ui:hud": "UI: HUD",
                        "ui:icons": "UI: Icons",
                        "ui:map": "UI: Map",
                        "ui:menu": "UI: Menus",
                        "visual:lighting": "Visual: Lighting",
                        "visual:weather": "Visual: Weather",
                      };
                      return labelMap[tag] ?? tag;
                    })()}
                  </Text>
                  <Text size={200} style={{ color: "#6b7280" }}>
                    {group.mods.length} mods
                  </Text>
                </div>
                <div style={{ marginTop: 8, display: "flex", flexDirection: "column", gap: 4 }}>
                  {group.mods.map((m) => (
                    <Text key={m.id} size={300}>
                      {(m.name || m.id) +
                        (typeof m.importanceScore === "number"
                          ? ` (score ${m.importanceScore})`
                          : m.importanceBucket
                            ? ` (${m.importanceBucket})`
                            : "")}
                    </Text>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
      {issue.lootMissingMasters && issue.lootMissingMasters.length > 0 && (
        <div>
          <button
            type="button"
            onClick={() => setShowMissingMastersSection((prev) => !prev)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 8,
              width: "100%",
              padding: 0,
              margin: 0,
              border: "none",
              background: "none",
              cursor: "pointer",
              textAlign: "left",
            }}
          >
            {showMissingMastersSection ? <ChevronDown16Regular /> : <ChevronRight16Regular />}
            <Text style={detailLabel}>Missing Masters</Text>
            <Text
              style={{
                fontSize: 12,
                color: "#6b7280",
              }}
            >
              ({missingMasterNames.length} plugin
              {missingMasterNames.length === 1 ? "" : "s"})
            </Text>
          </button>
          {showMissingMastersSection && (
            <div style={{ marginTop: 4 }}>
              {missingMasterNames.length ? (
                <ul style={{ paddingLeft: 18 }}>
                  {missingMasterNames.map((master) => (
                    <li key={master}>
                      <Text>{master}</Text>
                    </li>
                  ))}
                </ul>
              ) : (
                <Text>—</Text>
              )}
            </div>
          )}
        </div>
      )}
      <div>
        {isMissingMastersIssue ? (
          <>
            <button
              type="button"
              onClick={() => setShowAffectedMods((prev) => !prev)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                width: "100%",
                padding: 0,
                margin: 0,
                border: "none",
                background: "none",
                cursor: missingMastersByMod.length ? "pointer" : "default",
                textAlign: "left",
              }}
            >
              {missingMastersByMod.length ? (
                showAffectedMods ? (
                  <ChevronDown16Regular />
                ) : (
                  <ChevronRight16Regular />
                )
              ) : null}
              <Text style={detailLabel}>Affected Mods</Text>
              {missingMastersByMod.length ? (
                <Text
                  style={{
                    fontSize: 12,
                    color: "#6b7280",
                  }}
                >
                  ({missingMastersByMod.length} mod
                  {missingMastersByMod.length === 1 ? "" : "s"}, {totalMissingPluginsForMods} plugin
                  {totalMissingPluginsForMods === 1 ? "" : "s"})
                </Text>
              ) : null}
            </button>
            {showAffectedMods && (
              <>
                {missingMastersByMod.length ? (
                  <div
                    style={{
                      marginTop: 4,
                      display: "flex",
                      flexDirection: "column",
                      gap: 4,
                    }}
                  >
                    <div
                      style={{
                        display: "flex",
                        justifyContent: "flex-end",
                        gap: 8,
                        marginBottom: 4,
                      }}
                    >
                      <Button
                        size="small"
                        appearance="subtle"
                        onClick={() => {
                          const next: Record<string, boolean> = {};
                          missingMastersByMod.forEach((entry) => {
                            next[entry.modId] = true;
                          });
                          setExpandedMissingMasters(next);
                        }}
                      >
                        Expand all mods
                      </Button>
                      <Button
                        size="small"
                        appearance="subtle"
                        onClick={() => {
                          setExpandedMissingMasters({});
                        }}
                      >
                        Collapse all mods
                      </Button>
                    </div>
                    {missingMastersByMod.map((modEntry) => {
                      const isModExpanded = expandedMissingMasters[modEntry.modId] ?? false;
                      const toggleMod = () =>
                        setExpandedMissingMasters((prev) => ({
                          ...prev,
                          [modEntry.modId]: !isModExpanded,
                        }));

                      return (
                        <div
                          key={modEntry.modId}
                          style={{
                            padding: 8,
                            borderRadius: 4,
                            border: "1px solid #e5e7eb",
                            backgroundColor: "#ffffff",
                          }}
                        >
                          <button
                            type="button"
                            onClick={toggleMod}
                            style={{
                              display: "flex",
                              alignItems: "center",
                              gap: 8,
                              width: "100%",
                              padding: 0,
                              margin: 0,
                              border: "none",
                              background: "none",
                              cursor: "pointer",
                              textAlign: "left",
                            }}
                          >
                            {isModExpanded ? <ChevronDown16Regular /> : <ChevronRight16Regular />}
                            <Text weight="semibold">{modEntry.modName}</Text>
                            <Text
                              style={{
                                fontSize: 12,
                                color: "#6b7280",
                              }}
                            >
                              ({modEntry.plugins.length} plugin
                              {modEntry.plugins.length === 1 ? "" : "s"})
                            </Text>
                          </button>
                          {isModExpanded && (
                            <div style={{ marginTop: 4 }}>
                              <div
                                style={{
                                  display: "flex",
                                  justifyContent: "flex-end",
                                  gap: 8,
                                  marginBottom: 4,
                                }}
                              >
                                <Button
                                  size="small"
                                  appearance="subtle"
                                  onClick={() => {
                                    setExpandedMissingMasters((prev) => {
                                      const next = { ...prev };
                                      modEntry.plugins.forEach((plugin) => {
                                        const key = `${modEntry.modId}::${plugin.pluginName}`;
                                        next[key] = true;
                                      });
                                      return next;
                                    });
                                  }}
                                >
                                  Expand all plugins
                                </Button>
                                <Button
                                  size="small"
                                  appearance="subtle"
                                  onClick={() => {
                                    setExpandedMissingMasters((prev) => {
                                      const next = { ...prev };
                                      modEntry.plugins.forEach((plugin) => {
                                        const key = `${modEntry.modId}::${plugin.pluginName}`;
                                        delete next[key];
                                      });
                                      return next;
                                    });
                                  }}
                                >
                                  Collapse all plugins
                                </Button>
                              </div>
                              <ul style={{ paddingLeft: 18 }}>
                                {modEntry.plugins.map((plugin) => {
                                  const pluginKey = `${modEntry.modId}::${plugin.pluginName}`;
                                  const isPluginExpanded =
                                    expandedMissingMasters[pluginKey] ?? false;
                                  const togglePlugin = () =>
                                    setExpandedMissingMasters((prev) => ({
                                      ...prev,
                                      [pluginKey]: !isPluginExpanded,
                                    }));

                                  return (
                                    <li key={plugin.pluginName} style={{ marginBottom: 4 }}>
                                      <button
                                        type="button"
                                        onClick={togglePlugin}
                                        style={{
                                          display: "flex",
                                          alignItems: "center",
                                          gap: 8,
                                          width: "100%",
                                          padding: 0,
                                          margin: 0,
                                          border: "none",
                                          background: "none",
                                          cursor: "pointer",
                                          textAlign: "left",
                                        }}
                                      >
                                        {isPluginExpanded ? (
                                          <ChevronDown16Regular />
                                        ) : (
                                          <ChevronRight16Regular />
                                        )}
                                        <Text>{plugin.pluginName}</Text>
                                        <Text
                                          style={{
                                            fontSize: 12,
                                            color: "#6b7280",
                                          }}
                                        >
                                          ({plugin.masters.length} missing master
                                          {plugin.masters.length === 1 ? "" : "s"})
                                        </Text>
                                      </button>
                                      {isPluginExpanded && plugin.masters.length > 0 && (
                                        <ul style={{ marginTop: 2, paddingLeft: 26 }}>
                                          {plugin.masters.map((master) => (
                                            <li key={master}>
                                              <Text>{master}</Text>
                                            </li>
                                          ))}
                                        </ul>
                                      )}
                                    </li>
                                  );
                                })}
                              </ul>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                ) : (
                  <Text>—</Text>
                )}
              </>
            )}
          </>
        ) : (
          <>
            <button
              type="button"
              onClick={() => setShowAffectedMods((prev) => !prev)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                width: "100%",
                padding: 0,
                margin: 0,
                border: "none",
                background: "none",
                cursor: issue.affectedMods.length ? "pointer" : "default",
                textAlign: "left",
              }}
            >
              {issue.affectedMods.length ? (
                showAffectedMods ? (
                  <ChevronDown16Regular />
                ) : (
                  <ChevronRight16Regular />
                )
              ) : null}
              <Text style={detailLabel}>Affected Mods</Text>
              {issue.affectedMods.length ? (
                <Text
                  style={{
                    fontSize: 12,
                    color: "#6b7280",
                  }}
                >
                  ({issue.affectedMods.length})
                </Text>
              ) : null}
            </button>
            {showAffectedMods && issue.affectedMods.length > 0 && (
              <ul style={{ marginTop: 4, paddingLeft: 18 }}>
                {issue.affectedMods.map((modId) => (
                  <li key={modId}>
                    <Text>{modId}</Text>
                  </li>
                ))}
              </ul>
            )}
            {!issue.affectedMods.length && <Text>—</Text>}
          </>
        )}
      </div>
      <div>
        {!isMissingMastersIssue && (
          <>
            <button
              type="button"
              onClick={() => setShowAffectedPlugins((prev) => !prev)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                width: "100%",
                padding: 0,
                margin: 0,
                border: "none",
                background: "none",
                cursor: issue.affectedPlugins.length ? "pointer" : "default",
                textAlign: "left",
              }}
            >
              {issue.affectedPlugins.length ? (
                showAffectedPlugins ? (
                  <ChevronDown16Regular />
                ) : (
                  <ChevronRight16Regular />
                )
              ) : null}
              <Text style={detailLabel}>Affected Plugins</Text>
              {issue.affectedPlugins.length ? (
                <Text
                  style={{
                    fontSize: 12,
                    color: "#6b7280",
                  }}
                >
                  ({issue.affectedPlugins.length})
                </Text>
              ) : null}
            </button>
            {showAffectedPlugins && issue.affectedPlugins.length > 0 && (
              <ul style={{ marginTop: 4, paddingLeft: 18 }}>
                {issue.affectedPlugins.map((pluginName) => (
                  <li key={pluginName}>
                    <Text>{pluginName}</Text>
                  </li>
                ))}
              </ul>
            )}
            {!issue.affectedPlugins.length && <Text>—</Text>}
          </>
        )}
      </div>
      {issue.lootPluginMessages && issue.lootPluginMessages.length > 0 && (
        <div>
          <Text style={detailLabel}>LOOT Messages</Text>
          <div style={{ marginTop: 4 }}>
            {(() => {
              const pluginMap = new Map<string, typeof issue.lootPluginMessages>();
              issue.lootPluginMessages?.forEach((msg) => {
                const key = msg.plugin || "(Unknown plugin)";
                const existing = pluginMap.get(key) ?? [];
                existing.push(msg);
                pluginMap.set(key, existing);
              });

              const entries = Array.from(pluginMap.entries()).sort(([a], [b]) =>
                a.localeCompare(b, undefined, { sensitivity: "base" }),
              );

              if (!entries.length) {
                return <Text>—</Text>;
              }

              return (
                <>
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "flex-end",
                      gap: 8,
                      marginBottom: 4,
                    }}
                  >
                    <Button
                      size="small"
                      appearance="subtle"
                      onClick={() => {
                        const next: Record<string, boolean> = {};
                        entries.forEach(([pluginName]) => {
                          next[pluginName] = true;
                        });
                        setExpandedLootPluginMessages(next);
                      }}
                    >
                      Expand all
                    </Button>
                    <Button
                      size="small"
                      appearance="subtle"
                      onClick={() => {
                        setExpandedLootPluginMessages({});
                      }}
                    >
                      Collapse all
                    </Button>
                  </div>
                  <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                    {entries.map(([pluginName, messages]) => {
                      const key = pluginName;
                      const isExpanded = expandedLootPluginMessages[key] ?? false;
                      const toggle = () =>
                        setExpandedLootPluginMessages((prev) => ({
                          ...prev,
                          [key]: !isExpanded,
                        }));

                      return (
                        <div
                          key={key}
                          style={{
                            padding: 8,
                            borderRadius: 4,
                            border: "1px solid #e5e7eb",
                            backgroundColor: "#ffffff",
                          }}
                        >
                          <button
                            type="button"
                            onClick={toggle}
                            style={{
                              display: "flex",
                              alignItems: "center",
                              gap: 8,
                              width: "100%",
                              padding: 0,
                              margin: 0,
                              border: "none",
                              background: "none",
                              cursor: "pointer",
                              textAlign: "left",
                            }}
                          >
                            {isExpanded ? <ChevronDown16Regular /> : <ChevronRight16Regular />}
                            <Text weight="semibold">{pluginName}</Text>
                            <Text
                              style={{
                                fontSize: 12,
                                color: "#6b7280",
                              }}
                            >
                              ({messages.length} message
                              {messages.length === 1 ? "" : "s"})
                            </Text>
                          </button>
                          {isExpanded && (
                            <div style={{ marginTop: 4 }}>
                              {(["error", "warning", "note"] as const).map((severityKey) => {
                                const messagesForSeverity = messages.filter(
                                  (msg) => msg.severity === severityKey,
                                );
                                if (!messagesForSeverity.length) {
                                  return null;
                                }

                                const label =
                                  severityKey === "error"
                                    ? "Errors"
                                    : severityKey === "warning"
                                      ? "Warnings"
                                      : "Notes";

                                return (
                                  <div key={`${key}-${severityKey}`} style={{ marginBottom: 4 }}>
                                    <Text weight="semibold">{label}</Text>
                                    <ul style={{ marginTop: 2, paddingLeft: 18 }}>
                                      {messagesForSeverity.map((msg, index) => (
                                        <li key={`${key}-${severityKey}-${index}`}>
                                          <Text>
                                            {msg.text}
                                            {msg.condition ? ` (condition: ${msg.condition})` : ""}
                                          </Text>
                                        </li>
                                      ))}
                                    </ul>
                                  </div>
                                );
                              })}
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </>
              );
            })()}
          </div>
        </div>
      )}
      <div>
        <Text style={detailLabel}>Recommendations</Text>
        {recommendations.length ? (
          <ul>
            {recommendations.map((rec) => (
              <li key={rec.issueId + rec.steps.join("-")}>{rec.steps.join(" → ")}</li>
            ))}
          </ul>
        ) : (
          <Text>No recommendations yet.</Text>
        )}
      </div>
      <Divider />
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <Text style={detailLabel}>AI Chat</Text>
        <Button
          appearance="subtle"
          size="small"
          icon={<ArrowMaximize24Regular />}
          aria-label="Open chat in its own window"
          onClick={() => setShowChatWindow(true)}
        />
      </div>
      <div
        style={{
          marginTop: 4,
          padding: 8,
          borderRadius: 4,
          backgroundColor: "#f3f4f6",
          height: chatHeight,
          overflowY: "auto",
          display: "flex",
          flexDirection: "column",
          gap: 6,
        }}
      >
        {chatMessages.length === 0 ? (
          <Text size={200} style={{ color: "#9ca3af" }}>
            No messages yet. Ask the AI to explain this issue or type a follow-up question.
          </Text>
        ) : (
          chatMessages.map((msg, index) => (
            <div
              key={`${msg.role}-${index}`}
              style={{
                alignSelf: msg.role === "user" ? "flex-end" : "flex-start",
                maxWidth: "80%",
                padding: "6px 10px",
                borderRadius: 8,
                backgroundColor: msg.role === "user" ? "#dbeafe" : "#e5e7eb",
                color: "#111827",
                whiteSpace: "pre-wrap",
                fontSize: 12,
              }}
            >
              {msg.content}
            </div>
          ))
        )}
      </div>
      <div
        style={{
          height: 6,
          cursor: "row-resize",
          alignSelf: "stretch",
          margin: "4px 0 8px",
          borderRadius: 999,
          backgroundColor: "#e5e7eb",
        }}
        onMouseDown={handleChatResizeStart}
      />
      <Textarea
        value={chatInput}
        onChange={(_ev, data) => onChatInputChange(data.value)}
        placeholder="Ask a follow-up about this issue, or leave blank to use the default expansion prompt."
        resize="vertical"
        rows={3}
      />
      <div style={{ display: "flex", gap: 8, marginTop: 8, alignItems: "center" }}>
        <Button appearance="secondary" onClick={() => onExpand(issue)} disabled={expanding}>
          {expanding
            ? "Asking AI..."
            : hasCustomPrompt
              ? "Send"
              : "Ask AI to expand using default prompt"}
        </Button>
        <Dialog>
          <DialogTrigger>
            <Button
              appearance="subtle"
              size="small"
              icon={<QuestionCircle24Regular />}
              aria-label="What does this do?"
            />
          </DialogTrigger>
          <DialogSurface
            style={{
              maxWidth: 720,
              width: "min(720px, 90vw)",
            }}
          >
            <DialogBody
              style={{
                width: "100%",
                display: "block",
                gridTemplateColumns: "1fr",
              }}
            >
              <DialogTitle>
                <span
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    gap: 12,
                    width: "100%",
                  }}
                >
                  <span>Default AI expansion prompt</span>
                  <DialogTrigger>
                    <Button
                      appearance="subtle"
                      size="small"
                      icon={<Dismiss24Regular />}
                      aria-label="Close"
                    />
                  </DialogTrigger>
                </span>
              </DialogTitle>
              <div
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: 10,
                  marginTop: 12,
                  width: "100%",
                }}
              >
                <Text>
                  When you leave the chat input blank, this default prompt is sent to the AI to
                  generate a detailed explanation and fix for the current issue.
                </Text>
                <div
                  style={{
                    marginTop: 6,
                    padding: 10,
                    borderRadius: 4,
                    backgroundColor: "#f3f4f6",
                    border: "1px solid #e5e7eb",
                    width: "100%",
                    boxSizing: "border-box",
                  }}
                >
                  <Text style={{ ...detailLabel, marginBottom: 4 }}>Default prompt</Text>
                  <Text
                    style={{
                      fontFamily: "monospace",
                      whiteSpace: "pre-wrap",
                      fontSize: 12,
                      color: "#111827",
                    }}
                  >
                    {defaultPrompt}
                  </Text>
                </div>
              </div>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      </div>
      {showChatWindow && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            backgroundColor: "rgba(15, 23, 42, 0.45)",
            zIndex: 40,
          }}
          onClick={() => setShowChatWindow(false)}
        >
          <div
            style={{
              position: "absolute",
              top: chatWindowBounds.top,
              left: chatWindowBounds.left,
              width: chatWindowBounds.width,
              height: chatWindowBounds.height,
              backgroundColor: "#f9fafb",
              borderRadius: 10,
              boxShadow: "0 20px 45px rgba(15,23,42,0.45)",
              display: "flex",
              flexDirection: "column",
              padding: 16,
              boxSizing: "border-box",
            }}
            onClick={(ev) => ev.stopPropagation()}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                cursor: isDraggingWindow ? "grabbing" : "grab",
                paddingBottom: 8,
                borderBottom: "1px solid #e5e7eb",
              }}
              onMouseDown={handleWindowDragStart}
            >
              <Text weight="semibold">AI Chat for this issue</Text>
              <Button
                appearance="subtle"
                icon={<Dismiss24Regular />}
                onClick={() => setShowChatWindow(false)}
                aria-label="Close chat window"
              />
            </div>
            <div
              style={{
                marginTop: 8,
                padding: 8,
                borderRadius: 4,
                backgroundColor: "#f3f4f6",
                flex: 1,
                overflowY: "auto",
                display: "flex",
                flexDirection: "column",
                gap: 6,
              }}
            >
              {chatMessages.length === 0 ? (
                <Text size={200} style={{ color: "#9ca3af" }}>
                  No messages yet. Ask the AI to explain this issue or type a follow-up question.
                </Text>
              ) : (
                chatMessages.map((msg, index) => (
                  <div
                    key={`window-${msg.role}-${index}`}
                    style={{
                      alignSelf: msg.role === "user" ? "flex-end" : "flex-start",
                      maxWidth: "80%",
                      padding: "6px 10px",
                      borderRadius: 8,
                      backgroundColor: msg.role === "user" ? "#dbeafe" : "#e5e7eb",
                      color: "#111827",
                      whiteSpace: "pre-wrap",
                      fontSize: 12,
                    }}
                  >
                    {msg.content}
                  </div>
                ))
              )}
            </div>
            <Textarea
              value={chatInput}
              onChange={(_ev, data) => onChatInputChange(data.value)}
              placeholder="Ask a follow-up about this issue, or leave blank to use the default expansion prompt."
              resize="vertical"
              rows={3}
              style={{ marginTop: 8 }}
            />
            <div style={{ display: "flex", justifyContent: "space-between", marginTop: 8 }}>
              <Button appearance="secondary" onClick={() => onExpand(issue)} disabled={expanding}>
                {expanding
                  ? "Asking AI..."
                  : hasCustomPrompt
                    ? "Send"
                    : "Ask AI to expand using default prompt"}
              </Button>
            </div>
            <div
              style={{
                position: "absolute",
                right: 4,
                bottom: 4,
                width: 16,
                height: 16,
                cursor: isResizingWindow ? "nwse-resize" : "nwse-resize",
              }}
              onMouseDown={handleWindowResizeStart}
            />
          </div>
        </div>
      )}
    </div>
  );
});

export default IssueDetails;
