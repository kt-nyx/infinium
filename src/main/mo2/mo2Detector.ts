import { promises as fs } from "node:fs";
import path from "node:path";

export interface Mo2InstanceInfo {
  name: string;
  path: string;
}

const COMMON_PATHS = [
  "C:/Modding/ModOrganizer2",
  "C:/Modding/MO2",
  `${process.env["ProgramFiles"] ?? "C:/Program Files"}/Mod Organizer`,
  `${process.env["LOCALAPPDATA"] ?? "C:/Users/Default/AppData/Local"}/ModOrganizer`,
];

const looksLikeMo2 = async (candidate: string): Promise<boolean> => {
  try {
    const stats = await fs.stat(path.join(candidate, "ModOrganizer.exe"));
    return stats.isFile();
  } catch {
    return false;
  }
};

const registryPlaceholder = async (): Promise<Mo2InstanceInfo[]> => {
  // TODO: query Windows Registry for installed MO2 instances.
  return [];
};

export const detectMo2Instances = async (): Promise<Mo2InstanceInfo[]> => {
  const found: Mo2InstanceInfo[] = [];
  const registryHits = await registryPlaceholder();
  registryHits.forEach((hit) => found.push(hit));

  for (const candidate of COMMON_PATHS) {
    if (!candidate) continue;
    if (await looksLikeMo2(candidate)) {
      found.push({ name: path.basename(candidate), path: candidate });
    }
  }

  const unique = new Map(found.map((inst) => [inst.path.toLowerCase(), inst]));
  return Array.from(unique.values());
};
