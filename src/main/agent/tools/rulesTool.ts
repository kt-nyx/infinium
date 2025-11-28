import type { Issue, ProfileSnapshot, Recommendation } from "../../../shared/types";
import type { AgentTool } from "./types";
import { evaluate } from "../../rules/rulesEngine";

export const rulesTool: AgentTool<
  { profile: ProfileSnapshot },
  { issues: Issue[]; recommendations: Recommendation[] }
> = {
  name: "get_known_issue_rules",
  description: "Evaluates local heuristic rules for the provided profile snapshot.",
  invoke: ({ profile }) => Promise.resolve(evaluate(profile)),
};
