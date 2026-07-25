import { Card, CardHeader, Tag, Text } from "@fluentui/react-components";
import type { Issue, Severity } from "../../shared/types";
import { memo, useMemo } from "react";

export interface IssuesListProps {
  issues: Issue[];
  selectedId?: string;
  onSelect: (issueId: string) => void;
}

const severityOrder: Issue["severity"][] = ["critical", "high", "medium", "low", "suggestion"];

const severityColors: Record<Severity, { bg: string; fg: string; label: string }> = {
  critical: { bg: "#7f1d1d", fg: "#fee2e2", label: "Critical" },
  high: { bg: "#b91c1c", fg: "#fee2e2", label: "High" },
  medium: { bg: "#9a3412", fg: "#ffedd5", label: "Medium" },
  low: { bg: "#0369a1", fg: "#e0f2fe", label: "Low" },
  suggestion: { bg: "#065f46", fg: "#d1fae5", label: "Suggestion" },
};

const IssuesList = memo(function IssuesList({ issues, selectedId, onSelect }: IssuesListProps) {
  const grouped = useMemo(() => {
    const map = new Map<Issue["severity"], Issue[]>();
    issues.forEach((issue) => {
      const current = map.get(issue.severity) ?? [];
      current.push(issue);
      map.set(issue.severity, current);
    });
    return map;
  }, [issues]);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
      {severityOrder.map((severity) => {
        const bucket = grouped.get(severity);
        if (!bucket?.length) {
          return null;
        }
        const palette = severityColors[severity];
        return (
          <div key={severity}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 4 }}>
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: 999,
                  backgroundColor: palette.bg,
                }}
              />
              <Text weight="semibold" style={{ textTransform: "uppercase", fontSize: 12 }}>
                {palette.label}
              </Text>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {bucket.map((issue) => {
                const backedByNexus = issue.source.includes("nexus");
                return (
                  <Card
                    key={issue.id}
                    onClick={() => onSelect(issue.id)}
                    appearance={issue.id === selectedId ? "filled" : "filled-alternative"}
                    style={{
                      cursor: "pointer",
                      borderLeft: `3px solid ${palette.bg}`,
                    }}
                  >
                    <CardHeader
                      header={
                        <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
                          <Text weight="semibold">{issue.summary}</Text>
                          <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
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
                            <Tag
                              size="small"
                              shape="rounded"
                              style={{ backgroundColor: palette.bg, color: palette.fg }}
                            >
                              {palette.label}
                            </Tag>
                          </div>
                        </div>
                      }
                      description={`${issue.categoryNormalized ?? issue.category} • ${issue.affectedMods.length} mods`}
                    />
                  </Card>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
});

export default IssuesList;
