import {
  Button,
  Dropdown,
  Field,
  MessageBar,
  MessageBarBody,
  Option,
  Spinner,
  Text,
  Tooltip,
} from "@fluentui/react-components";
import {
  DocumentArrowDown24Regular,
  DocumentText24Regular,
  Settings24Regular,
} from "@fluentui/react-icons";
import { useCallback, useEffect, useRef, useState } from "react";
import IssueDetails from "./components/IssueDetails";
import IssuesList from "./components/IssuesList";
import LogsPanel from "./components/LogsPanel";
import SettingsPanel from "./components/SettingsPanel";
import { useAnalysis } from "./hooks/useAnalysis";
import type { Settings } from "../shared/types";

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
    chatMessagesForCurrentIssue,
    issueChatInput,
    setIssueChatInput,
    issueChatLoading,
    sendIssueChat,
    defaultIssuePrompt,
  } = useAnalysis();

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [logsOpen, setLogsOpen] = useState(false);
  const [logs, setLogs] = useState<string[]>([]);
  const [leftPaneWidth, setLeftPaneWidth] = useState(35); // percentage
  const layoutRef = useRef<HTMLDivElement | null>(null);
  const [toasts, setToasts] = useState<
    { id: number; message: string; intent: "info" | "success" | "warning" }[]
  >([]);
  const [singleInstancePrompted, setSingleInstancePrompted] = useState(false);
  const [deepSearchRunning, setDeepSearchRunning] = useState(false);
  const [deepSearchProgress, setDeepSearchProgress] = useState(0);
  const [deepSearchStage, setDeepSearchStage] = useState<string | null>(null);
  const deepSearchCancelRef = useRef(false);
  const toastIdRef = useRef(0);
  const previousInstancePathsRef = useRef<string[]>([]);

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

  const handleResizeStart = useCallback(
    (event: React.MouseEvent<HTMLDivElement>) => {
      event.preventDefault();
      const container = layoutRef.current;
      if (!container) return;

      const startX = event.clientX;
      const rect = container.getBoundingClientRect();
      const startWidthPx = (leftPaneWidth / 100) * rect.width;

      const handleMouseMove = (ev: MouseEvent) => {
        const delta = ev.clientX - startX;
        const newWidthPx = startWidthPx + delta;
        let nextPercent = (newWidthPx / rect.width) * 100;
        // Keep within reasonable bounds.
        nextPercent = Math.max(20, Math.min(60, nextPercent));
        setLeftPaneWidth(nextPercent);
      };

      const handleMouseUp = () => {
        window.removeEventListener("mousemove", handleMouseMove);
        window.removeEventListener("mouseup", handleMouseUp);
      };

      window.addEventListener("mousemove", handleMouseMove);
      window.addEventListener("mouseup", handleMouseUp);
    },
    [leftPaneWidth],
  );

  const showToast = useCallback(
    (message: string, intent: "info" | "success" | "warning" = "info") => {
      setToasts((prev) => {
        const nextId = toastIdRef.current + 1;
        toastIdRef.current = nextId;
        return [...prev, { id: nextId, message, intent }];
      });

      // Auto-dismiss after a short delay.
      window.setTimeout(() => {
        setToasts((prev) => prev.slice(1));
      }, 4000);
    },
    [],
  );

  const handleSaveSettings = (nextSettings: Settings): void => {
    void window.api.settings.save(nextSettings);
    void refreshSettings();
  };

  const handleInstanceChange = (path: string) => {
    setSelectedInstance(path);
    setSelectedProfile(null);
  };

  const handleBrowseInstanceClick = () => {
    void handleBrowseInstance();
  };

  const handleBrowseInstance = async () => {
    try {
      const picked = await window.api.mo2.browse();
      if (!picked) {
        showToast(
          "Selected folder does not look like a Mod Organizer 2 install or instance. Pick a folder that contains either ModOrganizer.exe or profiles/ and mods/ subfolders.",
          "warning",
        );
        return;
      }

      const nextSettings: Settings = {
        ...settings,
        mo2Instances: [
          ...settings.mo2Instances.filter(
            (inst) => inst.path.toLowerCase() !== picked.path.toLowerCase(),
          ),
          picked,
        ],
        selectedInstanceId: picked.path,
        selectedProfileId: undefined,
      };

      await window.api.settings.save(nextSettings);
      await refreshSettings();
      await refreshInstances();

      setSelectedInstance(picked.path);
      setSelectedProfile(null);

      showToast(`Using MO2 instance at ${picked.path}`, "success");
    } catch (err) {
      console.warn("MO2 browse failed", err);
      showToast("Failed to select Mod Organizer 2 instance.", "warning");
    }
  };

  const handleRefreshInstances = (): void => {
    void refreshInstances();
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

  useEffect(() => {
    const previous = previousInstancePathsRef.current;
    const prevSet = new Set(previous.map((p) => p.toLowerCase()));
    const currentPaths = instances.map((inst) => inst.path);
    const newOnes = currentPaths.filter((p) => !prevSet.has(p.toLowerCase()));

    if (newOnes.length > 0) {
      newOnes.forEach((path) => {
        showToast(`Found Mod Organizer 2 instance at ${path}`, "info");
      });
    }

    if (!selectedInstance && instances.length === 1 && !singleInstancePrompted) {
      const inst = instances[0];
      showToast(
        `Detected a single Mod Organizer 2 instance at ${inst.path}. Select it in Settings or via the instance selector below.`,
        "info",
      );
      setSingleInstancePrompted(true);
    }

    previousInstancePathsRef.current = currentPaths;
  }, [instances, selectedInstance, showToast, singleInstancePrompted]);

  const handleStartDeepSearch = async () => {
    if (deepSearchRunning) {
      return;
    }

    deepSearchCancelRef.current = false;
    setDeepSearchRunning(true);
    setDeepSearchProgress(0);
    setDeepSearchStage("Quick scan");

    try {
      // Stage 1: quick scan (existing detection)
      await refreshInstances();
      setDeepSearchProgress(25);
      if (deepSearchCancelRef.current) return;

      // Stage 2: registry scan
      setDeepSearchStage("Registry");
      await window.api.mo2.detectRegistry();
      await refreshInstances();
      setDeepSearchProgress(60);
      if (deepSearchCancelRef.current) return;

      // Stage 3: filesystem scan
      setDeepSearchStage("Filesystem");
      await window.api.mo2.detectFilesystem();
      await refreshInstances();
      setDeepSearchProgress(100);
      setDeepSearchStage("Done");
    } catch (err) {
      console.warn("Deep MO2 search failed", err);
      showToast(
        "Deep search for Mod Organizer 2 instances failed; see logs for details.",
        "warning",
      );
    } finally {
      setDeepSearchRunning(false);
      deepSearchCancelRef.current = false;
    }
  };

  const handleStopDeepSearch = () => {
    if (!deepSearchRunning) return;
    deepSearchCancelRef.current = true;
    setDeepSearchStage("Stopping…");
  };

  const renderMainContent = () => {
    if (!selectedInstance) {
      return (
        <div
          style={{
            flex: 1,
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            minHeight: 0,
          }}
        >
          <div style={{ maxWidth: 560, width: "100%", paddingTop: 16 }}>
            <Text size={500} weight="semibold">
              Select a Mod Organizer 2 instance to get started
            </Text>
            <Text size={200} style={{ display: "block", marginTop: 4, color: "#6b7280" }}>
              The reviewer needs an MO2 instance to read your profiles and mods. Choose a detected
              instance, or browse manually.
            </Text>
            <div style={{ marginTop: 16 }}>
              <Field label="Detected MO2 instances">
                <div style={{ display: "flex", gap: 8 }}>
                  <Dropdown
                    placeholder="Select an instance"
                    selectedOptions={[]}
                    onOptionSelect={(_, data) => {
                      if (data.optionValue) {
                        const path = String(data.optionValue);
                        handleInstanceChange(path);
                      }
                    }}
                  >
                    {instances.map((instance) => (
                      <Option key={instance.path} value={instance.path}>
                        {instance.name || instance.path}
                      </Option>
                    ))}
                  </Dropdown>
                  <Button
                    appearance="secondary"
                    onClick={() => {
                      void refreshInstances();
                    }}
                  >
                    Refresh
                  </Button>
                  <Button appearance="secondary" onClick={handleBrowseInstanceClick}>
                    Browse…
                  </Button>
                </div>
              </Field>
              <div style={{ marginTop: 12, display: "flex", gap: 8, alignItems: "center" }}>
                <Button
                  appearance="secondary"
                  onClick={() => {
                    void handleStartDeepSearch();
                  }}
                  disabled={deepSearchRunning}
                >
                  Run deep search
                </Button>
                <Button
                  appearance="secondary"
                  onClick={handleStopDeepSearch}
                  disabled={!deepSearchRunning}
                >
                  Stop
                </Button>
                {deepSearchStage && (
                  <Text size={200} style={{ color: "#6b7280" }}>
                    {deepSearchStage}
                  </Text>
                )}
              </div>
              {deepSearchRunning && (
                <div
                  style={{
                    marginTop: 8,
                    width: "100%",
                    height: 6,
                    backgroundColor: "#e5e7eb",
                    borderRadius: 999,
                    overflow: "hidden",
                  }}
                >
                  <div
                    style={{
                      width: `${deepSearchProgress}%`,
                      height: "100%",
                      backgroundColor: "#3b82f6",
                      transition: "width 0.2s ease-out",
                    }}
                  />
                </div>
              )}
              <Text size={100} style={{ marginTop: 8, color: "#9ca3af" }}>
                Search strategies:
                <br />
                • Environment variables: SKYRIM_AI_MO2_INSTANCE, MO2_INSTANCE_PATH
                <br />
                • Common folders: C:/Modding, C:/Games, Program Files &quot;Mod Organizer&quot;,
                LocalAppData/ModOrganizer
                <br />
                • Optional registry scan under Mod Organizer Team keys
                <br />• Optional filesystem scan under common modding and games directories
              </Text>
              {instances.length === 0 && (
                <MessageBar intent="warning" style={{ marginTop: 8 }}>
                  <MessageBarBody>
                    No Mod Organizer 2 instances were detected yet. Use “Browse…” to locate your MO2
                    instance manually.
                  </MessageBarBody>
                </MessageBar>
              )}
            </div>
          </div>
        </div>
      );
    }

    return (
      <div
        ref={layoutRef}
        style={{
          display: "flex",
          alignItems: "stretch",
          width: "100%",
          minHeight: 0,
          flex: 1,
        }}
      >
        <div
          style={{
            flexBasis: `${leftPaneWidth}%`,
            minWidth: 260,
            maxWidth: "60%",
            paddingRight: 12,
            boxSizing: "border-box",
          }}
        >
          <IssuesList
            issues={analysis.issues}
            selectedId={selectedIssueId}
            onSelect={handleIssueSelect}
          />
        </div>
        <div
          style={{
            width: 6,
            cursor: "col-resize",
            alignSelf: "stretch",
            backgroundColor: "#e5e7eb",
            borderRadius: 999,
            margin: "0 4px",
          }}
          onMouseDown={handleResizeStart}
        />
        <div
          style={{
            flex: 1,
            minWidth: 0,
            paddingLeft: 12,
            boxSizing: "border-box",
          }}
        >
          {currentIssue && (
            <IssueDetails
              issue={currentIssue}
              recommendations={recommendationsForIssue}
              onExpand={(issue) => {
                void sendIssueChat(issue);
              }}
              expanding={issueChatLoading}
              chatMessages={chatMessagesForCurrentIssue}
              chatInput={issueChatInput}
              onChatInputChange={setIssueChatInput}
              onClose={() => setSelectedIssueId("")}
              defaultPrompt={defaultIssuePrompt}
            />
          )}
        </div>
      </div>
    );
  };

  return (
    <div style={{ padding: 24, display: "flex", flexDirection: "column", gap: 16 }}>
      {toasts.length > 0 && (
        <div
          style={{
            position: "fixed",
            top: 16,
            right: 16,
            display: "flex",
            flexDirection: "column",
            gap: 8,
            zIndex: 1000,
          }}
        >
          {toasts.map((toast) => (
            <MessageBar key={toast.id} intent={toast.intent}>
              <MessageBarBody>{toast.message}</MessageBarBody>
            </MessageBar>
          ))}
        </div>
      )}
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

      {renderMainContent()}

      <SettingsPanel
        open={settingsOpen}
        onOpenChange={setSettingsOpen}
        settings={settings}
        onSave={handleSaveSettings}
        instances={instances}
        selectedInstancePath={selectedInstance}
        onInstanceChange={handleInstanceChange}
        onBrowseInstance={handleBrowseInstanceClick}
        profiles={profiles}
        selectedProfileId={selectedProfile}
        onProfileChange={setSelectedProfile}
        onRefreshInstances={handleRefreshInstances}
      />
      <LogsPanel open={logsOpen} onOpenChange={setLogsOpen} logs={logs} />
    </div>
  );
};

export default App;
