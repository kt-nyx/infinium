import { app } from "electron";
import { promises as fs } from "node:fs";
import path from "node:path";

const resolveBaseDir = (envVar: string, fallbackFolder: string): string => {
  const env = process.env[envVar];
  if (env) return env;

  try {
    const userData = app?.getPath?.("userData");
    if (userData) return path.join(userData, fallbackFolder);
  } catch {
    // ignore; fall back below
  }

  return path.join(process.cwd(), fallbackFolder);
};

export const resolveDigestCacheDir = (): string =>
  resolveBaseDir("SKYRIM_AI_CACHE_DIR", ".tmp-cache");

export const resolveTraceDir = (): string =>
  resolveBaseDir("SKYRIM_AI_TRACE_DIR", ".tmp-traces");

export const ensureDir = async (dir: string): Promise<void> => {
  await fs.mkdir(dir, { recursive: true });
};





