import type { Settings } from "./types";

export const defaultSettings = (): Settings => ({
  mo2Instances: [],
  lootMode: "auto",
  analysisMode: "offline",
  analysisDefaults: {
    useLoot: true,
    useNexus: false,
    useRag: false,
    complexity: 2,
    opinionatedness: 2,
  },
  logLevel: "info",
});
