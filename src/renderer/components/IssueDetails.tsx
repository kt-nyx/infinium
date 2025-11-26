import { Button, Divider, Tag, Text } from "@fluentui/react-components";
import type { Issue, Recommendation, Severity } from "../../shared/types";
import { memo, type CSSProperties } from "react";

export interface IssueDetailsProps {
  issue?: Issue;
  recommendations: Recommendation[];
  onExpand: (issue: Issue) => void;
  expanding?: boolean;
  expandedText?: string;
  onClose?: () => void;
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

const IssueDetails = memo(
  ({ issue, recommendations, onExpand, expanding, expandedText, onClose }: IssueDetailsProps) => {
    if (!issue) {
      return <Text>Select an issue on the left to view details.</Text>;
    }

    const palette = severityColors[issue.severity];

    return (
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
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
        <div>
          <Text style={detailLabel}>Category</Text>
          <Text>{issue.category}</Text>
        </div>
        <div>
          <Text style={detailLabel}>Details</Text>
          <Text>{issue.details}</Text>
        </div>
        <div>
          <Text style={detailLabel}>Affected Mods</Text>
          <Text>{issue.affectedMods.join(", ") || "—"}</Text>
        </div>
        <div>
          <Text style={detailLabel}>Affected Plugins</Text>
          <Text>{issue.affectedPlugins.join(", ") || "—"}</Text>
        </div>
        <Divider />
        {expandedText && (
          <div>
            <Text style={detailLabel}>AI Insights</Text>
            <Text>{expandedText}</Text>
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
        <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
          <Button appearance="secondary" onClick={() => onExpand(issue)} disabled={expanding}>
            {expanding ? "Asking AI..." : "Ask AI to expand"}
          </Button>
          {onClose && (
            <Button appearance="subtle" onClick={onClose}>
              Close
            </Button>
          )}
        </div>
      </div>
    );
  },
);

export default IssueDetails;
