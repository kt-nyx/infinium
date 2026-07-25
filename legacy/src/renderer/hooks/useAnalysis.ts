import { useCallback, useEffect, useMemo, useState } from "react";
import type { AnalysisRunOptions } from "../../shared/analysis";
import { defaultSettings } from "../../shared/defaults";
import type { AnalysisResult, Issue, Recommendation, Settings } from "../../shared/types";

// Empty baseline analysis used on first load so the UI starts
// with no issues until the user runs an analysis.
const emptyAnalysis: AnalysisResult = {
  profile: {
    profileId: "",
    game: "SkyrimSE",
    mo2InstancePath: "",
    mods: [],
    pluginLoadOrder: [],
    lootAvailable: false,
    nexusAvailable: false,
  },
  issues: [],
  recommendations: [],
  metadata: {
    offlineOnly: true,
    complexityLevel: 1,
    opinionatedness: 1,
    agentUsed: false,
    createdAt: new Date().toISOString(),
  },
};

// Sample mocked analysis kept only as a fallback if an analysis
// run fails, so the UI can still demonstrate behavior.
const mockAnalysis: AnalysisResult = {
  profile: {
    profileId: "MockProfile",
    game: "SkyrimSE",
    mo2InstancePath: "C:/Modding/MO2",
    mods: [
      {
        id: "Unofficial Patch",
        name: "Unofficial Skyrim Special Edition Patch",
        enabled: true,
        path: "C:/Modding/MO2/mods/USSEP",
        plugins: ["Unofficial Skyrim Special Edition Patch.esp"],
      },
    ],
    pluginLoadOrder: ["Skyrim.esm", "Update.esm", "Unofficial Skyrim Special Edition Patch.esp"],
    lootAvailable: false,
    nexusAvailable: false,
  },
  issues: [
    {
      id: "mock-issue-1",
      severity: "medium",
      category: "load_order",
      summary: "Mock issue: verify unofficial patch load order",
      details: "LOOT usually keeps USSEP right after the masters. This is placeholder text.",
      affectedMods: ["Unofficial Patch"],
      affectedPlugins: ["Unofficial Skyrim Special Edition Patch.esp"],
      risky: false,
      confidence: "medium",
      source: ["rules"],
    },
  ],
  recommendations: [
    {
      issueId: "mock-issue-1",
      steps: ["Ensure USSEP stays near the top of your load order."],
    },
  ],
  metadata: {
    offlineOnly: true,
    complexityLevel: 2,
    opinionatedness: 2,
    agentUsed: false,
    createdAt: new Date().toISOString(),
  },
};

const safeApi = () => window.api;

const defaultIssuePrompt =
  "Please act as a Skyrim SE/AE modding expert and expand on this issue in depth. " +
  "Explain in clear, non-jargon language what the problem is, why it matters for stability, performance, or compatibility, " +
  "which specific mods and plugins are involved, and any risks if it is ignored. " +
  "Describe how the user can confirm the issue in MO2, LOOT, or in-game, and then give concise, step-by-step instructions " +
  "to resolve or mitigate it, including any important trade-offs or alternative approaches.";

type IssueChatMessage = {
  role: "user" | "assistant";
  content: string;
};

export const useAnalysis = () => {
  const [analysis, setAnalysis] = useState<AnalysisResult>(emptyAnalysis);
  const [settings, setSettings] = useState<Settings>(defaultSettings());
  const [instances, setInstances] = useState<{ name: string; path: string }[]>([]);
  const [profiles, setProfiles] = useState<string[]>([]);
  const [selectedInstance, setSelectedInstance] = useState<string | null>(null);
  const [selectedProfile, setSelectedProfile] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedIssueId, setSelectedIssueId] = useState<string>("");
  const [issueChatMessages, setIssueChatMessages] = useState<Record<string, IssueChatMessage[]>>(
    {},
  );
  const [issueChatInput, setIssueChatInput] = useState<string>("");
  const [issueChatLoading, setIssueChatLoading] = useState(false);
  const [selectionInitialized, setSelectionInitialized] = useState(false);

  const refreshSettings = useCallback(async () => {
    try {
      const data = await safeApi().settings.get();
      setSettings(data);
      return data;
    } catch (err) {
      console.warn("Failed to load settings", err);
      const fallback = defaultSettings();
      setSettings(fallback);
      setError("Failed to load settings; using defaults.");
      return fallback;
    }
  }, []);

  const refreshInstances = useCallback(async () => {
    try {
      const data = await safeApi().mo2.detect();
      setInstances(data);
      return data;
    } catch (err) {
      console.warn("Failed to detect MO2 instances", err);
      setError("Unable to detect Mod Organizer 2; configure manually in Settings.");
      return [];
    }
  }, []);

  const refreshProfiles = useCallback(async (instancePath: string) => {
    try {
      const data = await safeApi().mo2.listProfiles(instancePath);
      setProfiles(data);
    } catch (err) {
      console.warn("Failed to list profiles", err);
      setProfiles([]);
    }
  }, []);

  useEffect(() => {
    if (selectionInitialized) {
      return;
    }

    const initialize = async () => {
      try {
        const [loadedSettings, detectedInstances, envInstancePath] = await Promise.all([
          refreshSettings(),
          refreshInstances(),
          safeApi()
            .mo2.getEnvInstance()
            .catch(() => null),
        ]);

        setInstances(detectedInstances);

        let nextInstance: string | null = null;

        if (loadedSettings.selectedInstanceId) {
          nextInstance = loadedSettings.selectedInstanceId;
        } else if (envInstancePath) {
          nextInstance = envInstancePath;
        }

        if (nextInstance) {
          setSelectedInstance(nextInstance);
        }

        if (loadedSettings.selectedProfileId) {
          setSelectedProfile(loadedSettings.selectedProfileId);
        }
      } finally {
        setSelectionInitialized(true);
      }
    };

    void initialize();
  }, [refreshInstances, refreshSettings, selectionInitialized]);

  useEffect(() => {
    if (selectedInstance) {
      void refreshProfiles(selectedInstance);
    } else {
      setProfiles([]);
    }
  }, [selectedInstance, refreshProfiles]);

  const runOffline = useCallback(
    async (options?: AnalysisRunOptions) => {
      if (!selectedInstance || !selectedProfile) {
        setError("Select a Mod Organizer 2 instance and profile before running analysis.");
        return analysis;
      }

      setLoading(true);
      setError(null);
      try {
        const result = await safeApi().analysis.runOffline(
          selectedInstance,
          selectedProfile,
          options,
        );
        setAnalysis(result);
        setSelectedIssueId("");
        setIssueChatMessages({});
        setIssueChatInput("");
        return result;
      } catch (err) {
        console.warn("Offline analysis failed", err);
        setError("Offline analysis failed; showing mocked data.");
        setAnalysis(mockAnalysis);
        return mockAnalysis;
      } finally {
        setLoading(false);
      }
    },
    [analysis, selectedInstance, selectedProfile],
  );

  const runAgentic = useCallback(
    async (options?: AnalysisRunOptions) => {
      if (!selectedInstance || !selectedProfile) {
        setError("Select a Mod Organizer 2 instance and profile before running analysis.");
        return analysis;
      }

      setLoading(true);
      setError(null);
      try {
        const result = await safeApi().analysis.runAgentic(
          selectedInstance,
          selectedProfile,
          options,
        );
        setAnalysis(result);
        setSelectedIssueId("");
        setIssueChatMessages({});
        setIssueChatInput("");
        return result;
      } catch (err) {
        console.warn("Agentic analysis failed", err);
        const message =
          err instanceof Error
            ? err.message
            : "Agentic analysis failed; falling back to offline result.";
        setError(message);
        return runOffline(options);
      } finally {
        setLoading(false);
      }
    },
    [analysis, runOffline, selectedInstance, selectedProfile],
  );

  const exportReport = useCallback(
    async (format: "json" | "html") => {
      const fallbackPath = `C:/Temp/infinium-report.${format}`;
      try {
        await safeApi().analysis.export(analysis, fallbackPath, format);
      } catch (err) {
        console.warn("Export failed", err);
      }
    },
    [analysis],
  );

  const currentIssue = useMemo<Issue | undefined>(
    () => analysis.issues.find((issue) => issue.id === selectedIssueId),
    [analysis.issues, selectedIssueId],
  );

  const recommendationsForIssue = useMemo<Recommendation[]>(
    () => analysis.recommendations.filter((rec) => rec.issueId === selectedIssueId),
    [analysis.recommendations, selectedIssueId],
  );

  const chatMessagesForCurrentIssue = useMemo<IssueChatMessage[]>(
    () => (currentIssue ? (issueChatMessages[currentIssue.id] ?? []) : []),
    [currentIssue, issueChatMessages],
  );

  const sendIssueChat = useCallback(
    async (issue?: Issue) => {
      const targetIssue = issue ?? currentIssue;
      if (!targetIssue) {
        return;
      }

      const prompt = issueChatInput.trim().length ? issueChatInput.trim() : defaultIssuePrompt;

      const issueId = targetIssue.id;
      const userMessage: IssueChatMessage = {
        role: "user",
        content: prompt,
      };

      setIssueChatLoading(true);
      try {
        const existingMessages = issueChatMessages[issueId] ?? [];
        const assistantReply = await safeApi().analysis.expandIssue(targetIssue, analysis.profile, [
          ...existingMessages,
          userMessage,
        ]);

        const assistantMessage: IssueChatMessage = {
          role: "assistant",
          content: assistantReply,
        };

        setIssueChatMessages((prev) => {
          const prevForIssue = prev[issueId] ?? [];
          return {
            ...prev,
            [issueId]: [...prevForIssue, userMessage, assistantMessage],
          };
        });

        if (issueChatInput.trim().length) {
          setIssueChatInput("");
        }
      } catch (err) {
        console.warn("Issue chat failed", err);
        const message = err instanceof Error ? `AI chat failed: ${err.message}` : "AI chat failed.";
        setError(message);
      } finally {
        setIssueChatLoading(false);
      }
    },
    [analysis.profile, currentIssue, issueChatInput, issueChatMessages],
  );

  return {
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
    issueChatMessages,
    chatMessagesForCurrentIssue,
    issueChatInput,
    setIssueChatInput,
    issueChatLoading,
    sendIssueChat,
    defaultIssuePrompt,
  };
};
