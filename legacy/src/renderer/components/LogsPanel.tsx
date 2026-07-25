import {
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Button,
  Textarea,
} from "@fluentui/react-components";

export interface LogsPanelProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  logs: string[];
}

const LogsPanel = ({ open, onOpenChange, logs }: LogsPanelProps) => (
  <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)}>
    <DialogSurface>
      <DialogBody>
        <DialogTitle>Logs</DialogTitle>
        <DialogContent>
          <Textarea readOnly resize="vertical" rows={12} value={logs.join("\n")} />
        </DialogContent>
        <DialogActions>
          <Button appearance="primary" onClick={() => onOpenChange(false)}>
            Close
          </Button>
        </DialogActions>
      </DialogBody>
    </DialogSurface>
  </Dialog>
);

export default LogsPanel;
