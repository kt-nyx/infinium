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
  profiles: string[];
  selectedProfileId: string | null;
  onProfileChange: (profileId: string) => void;
  onRefreshInstances: () => void;
  onRefreshProfiles: (instancePath: string) => void;
}

const SettingsPanel = ({
  open,
  onOpenChange,
  settings,
  onSave,
  instances,
  selectedInstancePath,
  onInstanceChange,
  profiles,
  selectedProfileId,
  onProfileChange,
  onRefreshInstances,
  onRefreshProfiles,
}: SettingsPanelProps) => {
  const [draft, setDraft] = useState<Settings>(settings);

  useEffect(() => {
    if (open) {
      // This effect intentionally re-syncs the local draft state with the latest
      // incoming settings whenever the dialog is (re)opened.
      // eslint-disable-next-line react-hooks/set-state-in-effect
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
                  selectedOptions={selectedInstancePath ? [selectedInstancePath] : []}
                  value={selectedInstancePath ?? undefined}
                  onOptionSelect={(_, data) => {
                    if (data.optionValue) {
                      const path = String(data.optionValue);
                      onInstanceChange(path);
                      onRefreshProfiles(path);
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
              </div>
            </Field>
            <Field label="Profile">
              <Dropdown
                placeholder="Select a profile"
                selectedOptions={selectedProfileId ? [selectedProfileId] : []}
                value={selectedProfileId ?? undefined}
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
            <Field label="Nexus Personal API Key">
              <Input
                type="password"
                value={draft.nexusApiKey ?? ""}
                onChange={(_, data) => setDraft({ ...draft, nexusApiKey: data.value })}
              />
            </Field>
            <Field label="RAG Index Path">
              <Input
                value={draft.ragIndexPath ?? ""}
                onChange={(_, data) => setDraft({ ...draft, ragIndexPath: data.value })}
              />
            </Field>
            <Field label="LOOT Mode">
              <Dropdown
                selectedOptions={[draft.lootMode]}
                onOptionSelect={(_, data) =>
                  setDraft({ ...draft, lootMode: data.optionValue as Settings["lootMode"] })
                }
              >
                {(["auto", "portable", "installed", "custom"] as Settings["lootMode"][]).map(
                  (mode) => (
                    <Option key={mode} value={mode}>
                      {mode}
                    </Option>
                  ),
                )}
              </Dropdown>
            </Field>
            <Field label="Default analysis type">
              <Dropdown
                selectedOptions={[draft.analysisMode ?? "offline"]}
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
            </Field>
            <Field label="Log Level">
              <Dropdown
                selectedOptions={[draft.logLevel]}
                onOptionSelect={(_, data) =>
                  setDraft({ ...draft, logLevel: data.optionValue as Settings["logLevel"] })
                }
              >
                {(["error", "warn", "info", "debug"] as Settings["logLevel"][]).map((level) => (
                  <Option key={level} value={level}>
                    {level}
                  </Option>
                ))}
              </Dropdown>
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
              <Slider
                min={1}
                max={5}
                value={draft.analysisDefaults.complexity}
                onChange={(_, data) => updateAnalysisDefault("complexity", data.value)}
              />
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
