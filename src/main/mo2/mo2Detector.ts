import { promises as fs } from "node:fs";
import { exec } from "node:child_process";
import { promisify } from "node:util";
import path from "node:path";
import { logger } from "../logging";

export interface Mo2InstanceInfo {
  name: string;
  path: string;
}

const COMMON_PATHS = [
  // Explicit modding-centric folders
  "C:/Modding/ModOrganizer2",
  "C:/Modding/Mod Organizer 2",
  "C:/Modding/MO2",
  "C:/Games/ModOrganizer2",
  "C:/Games/Mod Organizer 2",
  "C:/Games/MO2",
  // Typical installed locations
  `${process.env["ProgramFiles"] ?? "C:/Program Files"}/Mod Organizer`,
  `${process.env["ProgramFiles(x86)"] ?? "C:/Program Files (x86)"}/Mod Organizer`,
  `${process.env["LOCALAPPDATA"] ?? "C:/Users/Default/AppData/Local"}/ModOrganizer`,
];

const FILESYSTEM_ROOTS = [
  "C:/Modding",
  "C:/Games",
  process.env["ProgramFiles"] ?? "C:/Program Files",
  process.env["ProgramFiles(x86)"] ?? "C:/Program Files (x86)",
].filter((p): p is string => Boolean(p));

const REGISTRY_KEYS = [
  // Most MO2 setups that use the official installer will at least touch HKCU.
  "HKCU\\Software\\Mod Organizer Team\\Mod Organizer",
  "HKLM\\SOFTWARE\\Mod Organizer Team\\Mod Organizer",
];

const execAsync = promisify(exec);

const looksLikeMo2 = async (candidate: string): Promise<boolean> => {
  try {
    const stats = await fs.stat(path.join(candidate, "ModOrganizer.exe"));
    const isFile = stats.isFile();
    if (isFile) {
      await logger.debug(`[MO2] Found ModOrganizer.exe under candidate="${candidate}"`);
    }
    return isFile;
  } catch {
    return false;
  }
};

const registryPlaceholder = (): Promise<Mo2InstanceInfo[]> => {
  // TODO: query Windows Registry for installed MO2 instances.
  // For now we rely on common locations plus optional env overrides.
  return Promise.resolve([]);
};

const getEnvInstanceCandidates = (): string[] => {
  const fromPrimaryEnv = process.env.SKYRIM_AI_MO2_INSTANCE;
  const fromGenericEnv = process.env.MO2_INSTANCE_PATH;
  const candidates = [fromPrimaryEnv, fromGenericEnv].filter(
    (value): value is string => typeof value === "string" && value.length > 0,
  );
  return candidates;
};

export const detectMo2Instances = async (): Promise<Mo2InstanceInfo[]> => {
  const found: Mo2InstanceInfo[] = [];
  const registryHits = await registryPlaceholder();
  registryHits.forEach((hit) => found.push(hit));

  const candidates: string[] = [...getEnvInstanceCandidates(), ...COMMON_PATHS];

  await logger.debug(
    `[MO2] detectMo2Instances scanning ${candidates.length} candidates: ` + candidates.join(", "),
  );

  for (const candidate of candidates) {
    if (!candidate) continue;
    if (await looksLikeMo2(candidate)) {
      found.push({ name: path.basename(candidate), path: candidate });
    }
  }

  const unique = new Map(found.map((inst) => [inst.path.toLowerCase(), inst]));
  const instances = Array.from(unique.values());
  await logger.debug(
    `[MO2] detectMo2Instances found ${instances.length} instances: ` +
      instances.map((i) => `${i.name} @ ${i.path}`).join("; "),
  );
  return instances;
};

const normalizeInstances = (instances: Mo2InstanceInfo[]): Mo2InstanceInfo[] => {
  const unique = new Map<string, Mo2InstanceInfo>();
  for (const inst of instances) {
    unique.set(inst.path.toLowerCase(), inst);
  }
  return Array.from(unique.values());
};

export const detectMo2InstancesFromRegistry = async (): Promise<Mo2InstanceInfo[]> => {
  const instances: Mo2InstanceInfo[] = [];

  for (const key of REGISTRY_KEYS) {
    try {
      const { stdout } = await execAsync(`reg query "${key}" /s`);
      const lines = stdout.split(/\r?\n/);

      for (const line of lines) {
        const match = line.match(/REG_SZ\s+(.*)$/i);
        if (!match) continue;
        const raw = match[1].trim();
        if (!raw) continue;

        let candidateDir = raw;
        if (raw.toLowerCase().endsWith("modorganizer.exe")) {
          candidateDir = path.dirname(raw);
        }

        if (await looksLikeMo2(candidateDir)) {
          instances.push({
            name: path.basename(candidateDir),
            path: candidateDir,
          });
        }
      }
    } catch {
      // Ignore registry access failures; MO2 may simply not be installed via an installer.
    }
  }

  const normalized = normalizeInstances(instances);
  await logger.debug(
    `[MO2] detectMo2InstancesFromRegistry found ${normalized.length} instances from registry.`,
  );
  return normalized;
};

export const detectMo2InstancesFromFilesystem = async (): Promise<Mo2InstanceInfo[]> => {
  const found: Mo2InstanceInfo[] = [];
  const visited = new Set<string>();

  const walk = async (root: string, depth: number) => {
    if (depth > 4) return; // keep search bounded

    let entries: Awaited<ReturnType<typeof fs.readdir>>;
    try {
      entries = await fs.readdir(root, { withFileTypes: true } as unknown as {
        withFileTypes: true;
      });
    } catch {
      return;
    }

    for (const entry of entries) {
      if (!("isDirectory" in entry) || !entry.isDirectory()) continue;
      const full = path.join(root, entry.name);
      const key = full.toLowerCase();
      if (visited.has(key)) continue;
      visited.add(key);

      if (await looksLikeMo2(full)) {
        found.push({
          name: path.basename(full),
          path: full,
        });
      }

      await walk(full, depth + 1);
    }
  };

  for (const root of FILESYSTEM_ROOTS) {
    await logger.debug(`[MO2] Walking filesystem root="${root}" for MO2 instances (depth<=4)`);
    await walk(root, 0);
  }

  const normalized = normalizeInstances(found);
  await logger.debug(
    `[MO2] detectMo2InstancesFromFilesystem found ${normalized.length} instances.`,
  );
  return normalized;
};

export const getMo2SearchInfo = () => ({
  envCandidates: getEnvInstanceCandidates(),
  commonPaths: COMMON_PATHS.filter(Boolean),
  registryKeys: REGISTRY_KEYS,
  filesystemRoots: FILESYSTEM_ROOTS,
});
