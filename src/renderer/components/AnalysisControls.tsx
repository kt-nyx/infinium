import { Button, Field, Slider, Switch, tokens } from "@fluentui/react-components";
import { memo, type CSSProperties } from "react";

export interface AnalysisControlOptions {
  useLoot: boolean;
  useNexus: boolean;
  useRag: boolean;
  complexity: number;
  opinionatedness: number;
}

export interface AnalysisControlsProps {
  options: AnalysisControlOptions;
  onChange: (opts: AnalysisControlOptions) => void;
  onRunOffline: () => void;
  onRunAgentic: () => void;
  disabled?: boolean;
}

const containerStyles: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
  gap: tokens.spacingHorizontalM,
};

const AnalysisControls = memo((props: AnalysisControlsProps) => {
  const { options, onChange, onRunOffline, onRunAgentic, disabled } = props;

  const updateOption = <K extends keyof AnalysisControlOptions>(
    key: K,
    value: AnalysisControlOptions[K],
  ) => {
    onChange({ ...options, [key]: value });
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
      <div style={containerStyles}>
        <Switch
          label="Use LOOT"
          checked={options.useLoot}
          onChange={(_, data) => updateOption("useLoot", data.checked)}
        />
        <Switch
          label="Use Nexus"
          checked={options.useNexus}
          onChange={(_, data) => updateOption("useNexus", data.checked)}
        />
        <Switch
          label="Use RAG"
          checked={options.useRag}
          onChange={(_, data) => updateOption("useRag", data.checked)}
        />
      </div>

      <div style={containerStyles}>
        <Field label="Complexity">
          <Slider
            min={1}
            max={5}
            value={options.complexity}
            onChange={(_, data) => updateOption("complexity", data.value)}
          />
        </Field>
        <Field label="Opinionatedness">
          <Slider
            min={1}
            max={5}
            value={options.opinionatedness}
            onChange={(_, data) => updateOption("opinionatedness", data.value)}
          />
        </Field>
      </div>

      <div style={{ display: "flex", gap: 12 }}>
        <Button appearance="primary" onClick={onRunOffline} disabled={disabled}>
          Run Offline Analysis
        </Button>
        <Button appearance="outline" onClick={onRunAgentic} disabled={disabled}>
          Run Full Analysis
        </Button>
      </div>
    </div>
  );
});

export default AnalysisControls;
