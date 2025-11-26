import {
  Button,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  Tooltip,
} from "@fluentui/react-components";
import {
  DocumentArrowDown24Regular,
  DocumentText24Regular,
  Settings24Regular,
} from "@fluentui/react-icons";
import { useCallback, useEffect, useMemo, useState } from "react";
import IssueDetails from "./components/IssueDetails";
import IssuesList from "./components/IssuesList";
import LogsPanel from "./components/LogsPanel";
import SettingsPanel from "./components/SettingsPanel";
import { useAnalysis } from "./hooks/useAnalysis";
import type { Issue, Settings } from "../shared/types";

const App = () => {
  const {
    analysis,
    settings,
    instances,
    profiles,
    selectedInstance,
    setSelectedInstance,
    selectedProfile,
    setSelectedProfile,
    loading,
    error,
    currentIssue,
    recommendationsForIssue,
    selectedIssueId,
    setSelectedIssueId,
    runOffline,
    runAgentic,
    exportReport,
    refreshSettings,
    refreshInstances,
    refreshProfiles,
  } = useAnalysis();

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [logsOpen, setLogsOpen] = useState(false);
  const [logs, setLogs] = useState<string[]>([]);
  const [expandingIssue, setExpandingIssue] = useState(false);
  const [expandedNotes, setExpandedNotes] = useState<Record<string, string>>({});

  const fetchLogs = useCallback(async () => {
    try {
      const lines = await window.api.logs.tail(400);
      setLogs(lines);
    } catch (err) {
      console.warn("Failed to read logs", err);
      setLogs(["Failed to read logs"]);
    }
  }, []);

  useEffect(() => {
    if (logsOpen) {
      void fetchLogs();
    }
  }, [logsOpen, fetchLogs]);

  const handleExpandIssue = useCallback(async (issue: Issue) => {
    setExpandingIssue(true);
    try {
      const text = await window.api.analysis.expandIssue(issue.id, issue.summary);
      setExpandedNotes((prev) => ({ ...prev, [issue.id]: text }));
    } catch (err) {
      console.warn("Expand issue failed", err);
    } finally {
      setExpandingIssue(false);
    }
  }, []);

  const selectedExpandedText = useMemo(
    () => (selectedIssueId ? expandedNotes[selectedIssueId] : undefined),
    [expandedNotes, selectedIssueId],
  );

  const handleSaveSettings = async (nextSettings: Settings) => {
    await window.api.settings.save(nextSettings);
    await refreshSettings();
  };

  const handleInstanceChange = (path: string) => {
    setSelectedInstance(path);
    setSelectedProfile(null);
  };

  const handleIssueSelect = (issueId: string) => {
    setSelectedIssueId((current) => (current === issueId ? "" : issueId));
  };

  const handleAnalyzeClick = async () => {
    const mode = settings.analysisMode ?? "offline";
    const options = settings.analysisDefaults;
    if (mode === "agentic") {
      await runAgentic(options);
    } else {
      await runOffline(options);
    }
  };

  const analysisModeLabel =
    (settings.analysisMode ?? "offline") === "agentic"
      ? "Full analysis (agentic)"
      : "Offline analysis only";

  return (
    <div style={{ padding: 24, display: "flex", flexDirection: "column", gap: 16 }}>
      <header style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
        <div>
          <Text size={700} weight="semibold">
            Skyrim AI Modlist Reviewer
          </Text>
          <Text size={200}>Windows-only Electron prototype • Agentic analysis coming soon</Text>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <Tooltip content="View recent log lines" relationship="label">
            <Button
              icon={<DocumentText24Regular />}
              appearance="secondary"
              onClick={() => setLogsOpen(true)}
            >
              Logs
            </Button>
          </Tooltip>
          <Tooltip content="Configure MO2, LOOT, Nexus, and analysis mode" relationship="label">
            <Button
              icon={<Settings24Regular />}
              appearance="secondary"
              onClick={() => setSettingsOpen(true)}
            >
              Settings
            </Button>
          </Tooltip>
          <Tooltip content="Run analysis with current settings" relationship="label">
            <Button
              appearance="primary"
              onClick={() => {
                void handleAnalyzeClick();
              }}
              disabled={!selectedInstance || !selectedProfile || loading}
            >
              {loading ? "Analyzing..." : "Analyze"}
            </Button>
          </Tooltip>
          <Tooltip content="Export the current analysis as JSON" relationship="label">
            <Button icon={<DocumentArrowDown24Regular />} onClick={() => void exportReport("json")}>
              Export JSON
            </Button>
          </Tooltip>
        </div>
      </header>

      {error && (
        <MessageBar intent="warning">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      <Text size={200} style={{ color: "#9ca3af" }}>
        Mode: {analysisModeLabel}
      </Text>

      {loading && (
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <Spinner size="small" />
          <Text>Analyzing profile...</Text>
        </div>
      )}

      <div style={{ display: "grid", gridTemplateColumns: "minmax(280px, 35%) 1fr", gap: 24 }}>
        <IssuesList
          issues={analysis.issues}
          selectedId={selectedIssueId}
          onSelect={handleIssueSelect}
        />
        {currentIssue && (
          <IssueDetails
            issue={currentIssue}
            recommendations={recommendationsForIssue}
            onExpand={handleExpandIssue}
            expanding={expandingIssue}
            expandedText={selectedExpandedText}
            onClose={() => setSelectedIssueId("")}
          />
        )}
      </div>

      <SettingsPanel
        open={settingsOpen}
        onOpenChange={setSettingsOpen}
        settings={settings}
        onSave={handleSaveSettings}
        instances={instances}
        selectedInstancePath={selectedInstance}
        onInstanceChange={handleInstanceChange}
        profiles={profiles}
        selectedProfileId={selectedProfile}
        onProfileChange={setSelectedProfile}
        onRefreshInstances={refreshInstances}
        onRefreshProfiles={refreshProfiles}
      />
      <LogsPanel open={logsOpen} onOpenChange={setLogsOpen} logs={logs} />
    </div>
  );
};

export default App;
