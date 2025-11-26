import { Button, Dropdown, Field, Option } from "@fluentui/react-components";
import { ArrowClockwise16Regular } from "@fluentui/react-icons";
import { memo, type CSSProperties } from "react";

export interface ProfileSelectorProps {
  instances: { name: string; path: string }[];
  selectedInstance: string | null;
  onInstanceChange: (path: string) => void;
  profiles: string[];
  selectedProfile: string | null;
  onProfileChange: (profile: string) => void;
  onRefreshInstances: () => void;
}

const containerStyles: CSSProperties = {
  display: "flex",
  flexDirection: "column",
  gap: "12px",
};

const instanceRowStyles: CSSProperties = {
  display: "flex",
  gap: "8px",
  alignItems: "flex-end",
};

const ProfileSelector = memo((props: ProfileSelectorProps) => {
  const {
    instances,
    selectedInstance,
    onInstanceChange,
    profiles,
    selectedProfile,
    onProfileChange,
    onRefreshInstances,
  } = props;

  return (
    <div style={containerStyles}>
      <div style={instanceRowStyles}>
        <Field label="MO2 Instance" style={{ flexGrow: 1 }}>
          <Dropdown
            placeholder="Select an instance"
            value={selectedInstance ?? undefined}
            selectedOptions={selectedInstance ? [selectedInstance] : []}
            onOptionSelect={(_, data) => {
              if (data.optionValue) {
                onInstanceChange(String(data.optionValue));
              }
            }}
          >
            {instances.map((instance) => (
              <Option key={instance.path} value={instance.path}>
                {instance.name || instance.path}
              </Option>
            ))}
          </Dropdown>
        </Field>
        <Button
          icon={<ArrowClockwise16Regular />}
          onClick={onRefreshInstances}
          appearance="secondary"
        >
          Refresh
        </Button>
      </div>

      <Field label="Profile">
        <Dropdown
          placeholder="Select a profile"
          value={selectedProfile ?? undefined}
          selectedOptions={selectedProfile ? [selectedProfile] : []}
          onOptionSelect={(_, data) => {
            if (data.optionValue) {
              onProfileChange(String(data.optionValue));
            }
          }}
          disabled={!selectedInstance || profiles.length === 0}
        >
          {profiles.map((profile) => (
            <Option key={profile} value={profile}>
              {profile}
            </Option>
          ))}
        </Dropdown>
      </Field>
    </div>
  );
});

export default ProfileSelector;
