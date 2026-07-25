import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  Input,
  Option,
  Slider,
  Switch,
} from "@fluentui/react-components";
import { useEffect, useState } from "react";
import type { Settings } from "../../shared/types";

export interface SettingsPanelProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  settings: Settings;
  onSave: (settings: Settings) => void;
  instances: { name: string; path: string }[];
  selectedInstancePath: string | null;
  onInstanceChange: (path: string) => void;
  onBrowseInstance: () => void;
  profiles: string[];
  selectedProfileId: string | null;
  onProfileChange: (profileId: string) => void;
  onRefreshInstances: () => void;
}

const SettingsPanel = ({
  open,
  onOpenChange,
  settings,
  onSave,
  instances,
  selectedInstancePath,
  onInstanceChange,
  onBrowseInstance,
  profiles,
  selectedProfileId,
  onProfileChange,
  onRefreshInstances,
}: SettingsPanelProps) => {
  const [draft, setDraft] = useState<Settings>(settings);
  const [nexusHealth, setNexusHealth] = useState<{ ok: boolean; message: string } | null>(null);
  const [testingNexus, setTestingNexus] = useState(false);

  useEffect(() => {
    if (open) {
      // This effect intentionally re-syncs the local draft state with the latest
      // incoming settings whenever the dialog is (re)opened.
      setDraft(settings);
    }
  }, [open, settings]);

  const updateAnalysisDefault = <K extends keyof Settings["analysisDefaults"]>(
    key: K,
    value: Settings["analysisDefaults"][K],
  ) => {
    setDraft({
      ...draft,
      analysisDefaults: {
        ...draft.analysisDefaults,
        [key]: value,
      },
    });
  };

  return (
    <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Settings</DialogTitle>
          <DialogContent>
            <Field label="MO2 Instance">
              <div style={{ display: "flex", gap: 8 }}>
                <Dropdown
                  placeholder="Select an instance"
                  // Keep the selection fully controlled so it doesn't get visually
                  // reset when other dropdowns change.
                  value={selectedInstancePath ?? draft.selectedInstanceId ?? undefined}
                  selectedOptions={
                    selectedInstancePath
                      ? [selectedInstancePath]
                      : draft.selectedInstanceId
                        ? [draft.selectedInstanceId]
                        : []
                  }
                  onOptionSelect={(_, data) => {
                    if (data.optionValue) {
                      const path = String(data.optionValue);
                      onInstanceChange(path);
                      setDraft({
                        ...draft,
                        selectedInstanceId: path,
                        selectedProfileId: undefined,
                      });
                    }
                  }}
                >
                  {instances.map((instance) => (
                    <Option key={instance.path} value={instance.path}>
                      {instance.name || instance.path}
                    </Option>
                  ))}
                </Dropdown>
                <Button appearance="secondary" onClick={onRefreshInstances}>
                  Refresh
                </Button>
                <Button appearance="secondary" onClick={onBrowseInstance}>
                  Browse…
                </Button>
              </div>
            </Field>
            <Field label="Profile">
              <Dropdown
                placeholder="Select a profile"
                // Prefer the live app selection, but fall back to the draft settings.
                value={selectedProfileId ?? draft.selectedProfileId ?? undefined}
                selectedOptions={
                  selectedProfileId
                    ? [selectedProfileId]
                    : draft.selectedProfileId
                      ? [draft.selectedProfileId]
                      : []
                }
                disabled={!selectedInstancePath || profiles.length === 0}
                onOptionSelect={(_, data) => {
                  if (data.optionValue) {
                    const profile = String(data.optionValue);
                    onProfileChange(profile);
                    setDraft({
                      ...draft,
                      selectedProfileId: profile,
                    });
                  }
                }}
              >
                {profiles.map((profile) => (
                  <Option key={profile} value={profile}>
                    {profile}
                  </Option>
                ))}
              </Dropdown>
            </Field>
            <Field label="Skyrim SE Data Path">
              <div style={{ display: "flex", gap: 8 }}>
                <Input
                  value={draft.skyrimSeDataPath ?? ""}
                  onChange={(_, data) =>
                    setDraft({
                      ...draft,
                      skyrimSeDataPath: data.value || undefined,
                    })
                  }
                  placeholder="C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition\Data"
                />
                <Button
                  appearance="secondary"
                  onClick={() => {
                    void (async () => {
                      const chosen = await window.api.game.browseSkyrimSeDataPath();
                      if (chosen) {
                        setDraft({
                          ...draft,
                          skyrimSeDataPath: chosen,
                        });
                      }
                    })();
                  }}
                >
                  Browse…
                </Button>
              </div>
            </Field>
            <Field label="Nexus Personal API Key">
              <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                <Input
                  type="password"
                  value={draft.nexusApiKey ?? ""}
                  onChange={(_, data) => {
                    setDraft({ ...draft, nexusApiKey: data.value });
                    // Clear any previous health status when the key changes.
                    setNexusHealth(null);
                  }}
                />
                <Button
                  appearance="secondary"
                  disabled={testingNexus || !draft.nexusApiKey}
                  onClick={() => {
                    void (async () => {
                      try {
                        setTestingNexus(true);
                        const result = await window.api.nexus.checkHealth();
                        setNexusHealth(result);
                      } catch (error) {
                        console.warn("Nexus health check failed", error);
                        setNexusHealth({
                          ok: false,
                          message:
                            "Failed to contact Nexus Mods. Check your internet connection and API key.",
                        });
                      } finally {
                        setTestingNexus(false);
                      }
                    })();
                  }}
                >
                  {testingNexus ? "Testing…" : "Test Nexus connection"}
                </Button>
              </div>
              {nexusHealth && (
                <div
                  style={{
                    marginTop: 4,
                    fontSize: 12,
                    color: nexusHealth.ok ? "#059669" : "#b91c1c",
                  }}
                >
                  {nexusHealth.message}
                </div>
              )}
            </Field>
            <Field label="RAG Index Path">
              <Input
                value={draft.ragIndexPath ?? ""}
                onChange={(_, data) => setDraft({ ...draft, ragIndexPath: data.value })}
              />
            </Field>
            {/* Legacy LOOT.exe mode/path controls have been removed in favour of libloot. */}
            <Field label="Default analysis type">
              {(() => {
                const mode = draft.analysisMode ?? "offline";
                return (
                  <Dropdown
                    placeholder="Select default analysis type"
                    value={mode}
                    selectedOptions={[mode]}
                    onOptionSelect={(_, data) =>
                      setDraft({
                        ...draft,
                        analysisMode: (data.optionValue as Settings["analysisMode"]) ?? "offline",
                      })
                    }
                  >
                    <Option value="offline">Offline only (rules + LOOT)</Option>
                    <Option value="agentic">Full analysis (agentic + tools)</Option>
                  </Dropdown>
                );
              })()}
            </Field>
            <Field label="Log Level">
              {(() => {
                const level = draft.logLevel ?? "info";
                return (
                  <Dropdown
                    placeholder="Select log verbosity"
                    value={level}
                    selectedOptions={[level]}
                    onOptionSelect={(_, data) =>
                      setDraft({
                        ...draft,
                        logLevel: data.optionValue as Settings["logLevel"],
                      })
                    }
                  >
                    {(["error", "warn", "info", "debug"] as Settings["logLevel"][]).map(
                      (optLevel) => (
                        <Option key={optLevel} value={optLevel}>
                          {optLevel}
                        </Option>
                      ),
                    )}
                  </Dropdown>
                );
              })()}
            </Field>

            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                gap: 12,
              }}
            >
              <Switch
                label="Use LOOT by default"
                checked={draft.analysisDefaults.useLoot}
                onChange={(_, data) => updateAnalysisDefault("useLoot", data.checked)}
              />
              <Switch
                label="Use Nexus"
                checked={draft.analysisDefaults.useNexus}
                onChange={(_, data) => updateAnalysisDefault("useNexus", data.checked)}
              />
              <Switch
                label="Use RAG"
                checked={draft.analysisDefaults.useRag}
                onChange={(_, data) => updateAnalysisDefault("useRag", data.checked)}
              />
            </div>

            <Field label="Default Complexity">
              {(() => {
                const value = draft.analysisDefaults.complexity;
                const label =
                  value <= 1
                    ? "Basic"
                    : value <= 3
                      ? "Balanced"
                      : value === 4
                        ? "Thorough"
                        : "Exhaustive";
                return (
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <Slider
                      min={1}
                      max={5}
                      value={value}
                      onChange={(_, data) => updateAnalysisDefault("complexity", data.value)}
                    />
                    <span style={{ fontSize: 12, opacity: 0.8 }}>{label}</span>
                  </div>
                );
              })()}
            </Field>
            <Field label="Default Opinionatedness">
              <Slider
                min={1}
                max={5}
                value={draft.analysisDefaults.opinionatedness}
                onChange={(_, data) => updateAnalysisDefault("opinionatedness", data.value)}
              />
            </Field>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              appearance="primary"
              onClick={() => {
                onSave(draft);
                onOpenChange(false);
              }}
            >
              Save
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
};

export default SettingsPanel;
