export interface AgentTool<I = unknown, O = unknown> {
  name: string;
  description: string;
  invoke: (input: I) => Promise<O>;
}
