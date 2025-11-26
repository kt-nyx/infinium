import { useCallback, useEffect, useMemo, useState } from "react";
import type { AnalysisRunOptions } from "../../shared/analysis";
import { defaultSettings } from "../../shared/defaults";
import type { AnalysisResult, Issue, Recommendation, Settings } from "../../shared/types";

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

export const useAnalysis = () => {
  const [analysis, setAnalysis] = useState<AnalysisResult>(mockAnalysis);
  const [settings, setSettings] = useState<Settings>(defaultSettings());
  const [instances, setInstances] = useState<{ name: string; path: string }[]>([]);
  const [profiles, setProfiles] = useState<string[]>([]);
  const [selectedInstance, setSelectedInstance] = useState<string | null>(null);
  const [selectedProfile, setSelectedProfile] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedIssueId, setSelectedIssueId] = useState<string>("");

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
    void refreshSettings();
    void refreshInstances();
  }, [refreshInstances, refreshSettings]);

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
        setAnalysis(mockAnalysis);
        return mockAnalysis;
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
    [selectedInstance, selectedProfile],
  );

  const runAgentic = useCallback(
    async (options?: AnalysisRunOptions) => {
      if (!selectedInstance || !selectedProfile) {
        return runOffline(options);
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
        return result;
      } catch (err) {
        console.warn("Agentic analysis failed", err);
        setError("Agentic analysis failed; falling back to offline result.");
        return runOffline(options);
      } finally {
        setLoading(false);
      }
    },
    [runOffline, selectedInstance, selectedProfile],
  );

  const exportReport = useCallback(
    async (format: "json" | "html") => {
      const fallbackPath = `C:/Temp/skyrim-ai-report.${format}`;
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
  };
};
