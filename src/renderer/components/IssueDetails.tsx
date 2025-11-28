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
  Dismiss24Regular,
  QuestionCircle24Regular,
} from "@fluentui/react-icons";
import type { Issue, Recommendation, Severity } from "../../shared/types";
import { memo, type CSSProperties, useState } from "react";

type IssueChatMessage = {
  role: "user" | "assistant";
  content: string;
};

export interface IssueDetailsProps {
  issue?: Issue;
  recommendations: Recommendation[];
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
