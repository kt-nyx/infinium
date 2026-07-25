import { app } from "electron";
import { promises as fs } from "node:fs";
import path from "node:path";
import { z } from "zod";
import { defaultSettings } from "../shared/defaults";
import type { Settings } from "../shared/types";

const settingsSchema = z
  .object({
    mo2RootGuess: z.string().optional(),
    mo2Instances: z.array(
      z.object({
        name: z.string(),
        path: z.string(),
      }),
    ),
    selectedInstanceId: z.string().optional(),
    selectedProfileId: z.string().optional(),
    skyrimSeDataPath: z.string().optional(),
    lootPortablePath: z.string().optional(),
    lootInstalledPath: z.string().optional(),
    lootMode: z.enum(["portable", "installed", "custom", "auto"]),
    lootCustomPath: z.string().optional(),
    nexusApiKey: z.string().optional(),
    ragIndexPath: z.string().optional(),
    analysisMode: z.enum(["offline", "agentic"]).optional(),
    analysisDefaults: z.object({
      useLoot: z.boolean(),
      useNexus: z.boolean(),
      useRag: z.boolean(),
      complexity: z.number().min(1).max(5),
      opinionatedness: z.number().min(1).max(5),
    }),
    logLevel: z.enum(["error", "warn", "info", "debug"]),
  })
  .passthrough();

const SETTINGS_FILE = "settings.json";
let cachedPath: string | null = null;

const resolveSettingsPath = (): string => {
  if (cachedPath) {
    return cachedPath;
  }

  const fallbackDir = process.env.SKYRIM_AI_CONFIG_DIR ?? path.join(process.cwd(), ".tmp-config");

  try {
    const userData = app?.getPath?.("userData");
    if (userData) {
      cachedPath = path.join(userData, SETTINGS_FILE);
      return cachedPath;
    }
  } catch {
    // app not initialized; fall back below
  }

  cachedPath = path.join(fallbackDir, SETTINGS_FILE);
  return cachedPath;
};

const ensureDirectory = async (filePath: string): Promise<void> => {
  const dir = path.dirname(filePath);
  await fs.mkdir(dir, { recursive: true });
};

/**
 * Attempts to fix invalid paths by trying common drive letter replacements.
 * This is useful when drive letters change (e.g., N: to Z:).
 * Returns the corrected path if a valid replacement is found, otherwise returns the original.
 */
const tryFixPath = async (filePath: string | undefined): Promise<string | undefined> => {
  if (!filePath) {
    return filePath;
  }

  // Check if the path exists as-is (either as a directory or file)
  try {
    const stats = await fs.stat(filePath);
    if (stats.isDirectory() || stats.isFile()) {
      return filePath;
    }
  } catch {
    // Path doesn't exist, try to fix it
  }

  // Only attempt fixes on Windows absolute paths (drive letters)
  const driveLetterMatch = /^([A-Z]):/i.exec(filePath);
  if (!driveLetterMatch) {
    return filePath;
  }

  const oldDrive = driveLetterMatch[1].toUpperCase();
  
  // Get the current working directory's drive letter as a likely replacement
  const cwdDrive = process.cwd().match(/^([A-Z]):/i)?.[1]?.toUpperCase();
  
  // Try common drive letter replacements
  const candidates = new Set<string>();
  if (cwdDrive && cwdDrive !== oldDrive) {
    candidates.add(cwdDrive);
  }
  // Try common drive letters
  for (const drive of ["Z", "C", "D", "E", "F", "G", "H"]) {
    if (drive !== oldDrive) {
      candidates.add(drive);
    }
  }

  // Try each candidate drive letter
  for (const newDrive of candidates) {
    const correctedPath = filePath.replace(/^[A-Z]:/i, `${newDrive}:`);
    try {
      const stats = await fs.stat(correctedPath);
      if (stats.isDirectory() || stats.isFile()) {
        // Found a valid path! Log the correction
        console.warn(
          `[Config] Fixed invalid path by replacing drive letter: "${filePath}" -> "${correctedPath}"`,
        );
        return correctedPath;
      }
    } catch {
      // This candidate didn't work, try the next one
    }
  }

  // No valid replacement found, return original
  return filePath;
};

const applyEnvOverrides = (settings: Settings): Settings => {
  // Allow providing a default Nexus API key via environment variable, without
  // forcing it to be written to disk. The saved settings take precedence if a
  // key has already been configured in the UI.
  const envNexusKey = process.env.SKYRIM_AI_NEXUS_API_KEY ?? process.env.NEXUS_API_KEY;
  const envSkyrimSePath =
    process.env.SKYRIM_SE_DATA_PATH ??
    process.env.SKYRIM_AE_DATA_PATH ??
    process.env.SKYRIM_DATA_PATH;

  let next: Settings = { ...settings };

  if (envNexusKey && !next.nexusApiKey) {
    next = {
      ...next,
      nexusApiKey: envNexusKey,
    };
  }

  if (envSkyrimSePath && !next.skyrimSeDataPath) {
    next = {
      ...next,
      skyrimSeDataPath: envSkyrimSePath,
    };
  }

  return next;
};

export const loadSettings = async (): Promise<Settings> => {
  const defaults = defaultSettings();
  const targetPath = resolveSettingsPath();

  try {
    const raw = await fs.readFile(targetPath, "utf-8");
    const parsed: unknown = JSON.parse(raw);
    const merged = settingsSchema.safeParse(parsed);
    if (merged.success) {
      let settings = { ...defaults, ...merged.data };
      
      // Try to fix invalid paths (e.g., when drive letters change)
      const fixedSkyrimPath = await tryFixPath(settings.skyrimSeDataPath);
      if (fixedSkyrimPath !== settings.skyrimSeDataPath) {
        settings = { ...settings, skyrimSeDataPath: fixedSkyrimPath };
        // Auto-save the corrected path
        await saveSettings(settings);
      }
      
      return applyEnvOverrides(settings);
    }
    console.warn("Settings schema mismatch, falling back to defaults", merged.error);
    return applyEnvOverrides(defaults);
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT") {
      console.warn("Failed to load settings, returning defaults", error);
    }
    return applyEnvOverrides(defaults);
  }
};

export const saveSettings = async (payload: Settings): Promise<void> => {
  // TODO: encrypt sensitive fields (e.g., nexusApiKey) before persisting to disk.
  const targetPath = resolveSettingsPath();
  await ensureDirectory(targetPath);
  await fs.writeFile(targetPath, JSON.stringify(payload, null, 2), "utf-8");
};

export const getSettingsPath = (): string => resolveSettingsPath();
