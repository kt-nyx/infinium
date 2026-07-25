import type { ModInfo } from "../../shared/types";
import { logger } from "../logging";
import { promises as fs } from "node:fs";
import path from "node:path";

export interface Mo2InstalledFile {
  modid?: number;
  fileid?: number;
}

export interface Mo2Meta {
  modid?: number;
  version?: string;
  newestVersion?: string;
  repository?: string;
  nexusFileStatus?: number;
  lastNexusQuery?: string;
  lastNexusUpdate?: string;
  nexusLastModified?: string;
  nexusCategory?: number;
  installedFiles?: Mo2InstalledFile[];
}

export const parseMo2MetaIni = (raw: string): Mo2Meta | null => {
  if (!raw.trim()) {
    return null;
  }

  const lines = raw.split(/\r?\n/);
  let currentSection = "";
  const general: Record<string, string> = {};
  const installedFiles: Record<string, Record<string, string>> = {};

  const sectionHeaderRegex = /^\s*\[([^\]]+)]\s*$/;
  const keyValueRegex = /^\s*([^=]+)=(.*)$/;

  lines.forEach((line) => {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith(";") || trimmed.startsWith("#")) {
      return;
    }

    const sectionMatch = trimmed.match(sectionHeaderRegex);
    if (sectionMatch) {
      currentSection = sectionMatch[1];
      return;
    }

    const kvMatch = trimmed.match(keyValueRegex);
    if (!kvMatch) {
      return;
    }

    const key = kvMatch[1].trim();
    const value = kvMatch[2].trim();

    if (!currentSection || currentSection === "General") {
      general[key] = value;
    } else if (currentSection === "installedFiles") {
      // Keys look like "1\\modid", "1\\fileid", "size", etc.
      const parts = key.split(/\\+/);
      if (parts.length === 2) {
        const [index, subKey] = parts;
        if (!installedFiles[index]) {
          installedFiles[index] = {};
        }
        installedFiles[index][subKey] = value;
      }
    }
  });

  const meta: Mo2Meta = {};

  const toInt = (v: string | undefined): number | undefined => {
    if (!v) return undefined;
    const parsed = Number.parseInt(v, 10);
    if (Number.isNaN(parsed) || parsed <= 0) {
      return undefined;
    }
    return parsed;
  };

  meta.modid = toInt(general["modid"]);
  meta.version = general["version"] || undefined;
  meta.newestVersion = general["newestVersion"] || undefined;
  meta.repository = general["repository"] || undefined;
  meta.nexusFileStatus = toInt(general["nexusFileStatus"]);
  meta.lastNexusQuery = general["lastNexusQuery"] || undefined;
  meta.lastNexusUpdate = general["lastNexusUpdate"] || undefined;
  meta.nexusLastModified = general["nexusLastModified"] || undefined;
  meta.nexusCategory = toInt(general["nexusCategory"]);

  const installed: Mo2InstalledFile[] = [];
  Object.keys(installedFiles).forEach((index) => {
    const bucket = installedFiles[index];
    const modid = toInt(bucket["modid"]);
    const fileid = toInt(bucket["fileid"]);
    if (modid || fileid) {
      installed.push({ modid, fileid });
    }
  });

  if (installed.length) {
    meta.installedFiles = installed;
  }

  const hasAnyField =
    meta.modid !== undefined ||
    !!meta.version ||
    !!meta.newestVersion ||
    !!meta.repository ||
    !!meta.nexusFileStatus ||
    !!meta.lastNexusQuery ||
    !!meta.lastNexusUpdate ||
    !!meta.nexusLastModified ||
    !!meta.nexusCategory ||
    !!meta.installedFiles?.length;

  return hasAnyField ? meta : null;
};

export const enrichModInfoWithMo2Meta = (mod: ModInfo, meta: Mo2Meta): ModInfo => {
  const existingMetadata = mod.metadata ?? {};
  const existingMo2Bucket = (existingMetadata.mo2 ?? {}) as Record<string, unknown>;

  const nextMo2Bucket: Record<string, unknown> = {
    ...existingMo2Bucket,
    modid: meta.modid ?? existingMo2Bucket.modid,
    version: meta.version ?? existingMo2Bucket.version,
    newestVersion: meta.newestVersion ?? existingMo2Bucket.newestVersion,
    repository: meta.repository ?? existingMo2Bucket.repository,
    nexusFileStatus: meta.nexusFileStatus ?? existingMo2Bucket.nexusFileStatus,
    lastNexusQuery: meta.lastNexusQuery ?? existingMo2Bucket.lastNexusQuery,
    lastNexusUpdate: meta.lastNexusUpdate ?? existingMo2Bucket.lastNexusUpdate,
    nexusLastModified: meta.nexusLastModified ?? existingMo2Bucket.nexusLastModified,
    nexusCategory: meta.nexusCategory ?? existingMo2Bucket.nexusCategory,
    installedFiles: meta.installedFiles ?? existingMo2Bucket.installedFiles,
  };

  const nextMetadata: Record<string, unknown> = {
    ...existingMetadata,
    mo2: nextMo2Bucket,
  };

  const next: ModInfo = {
    ...mod,
    metadata: nextMetadata,
  };

  if (typeof next.nexusId !== "number" && typeof meta.modid === "number") {
    next.nexusId = meta.modid;
  }

  if (!next.installedVersion && meta.version) {
    next.installedVersion = meta.version;
  }

  return next;
};

export const readMo2MetaForMod = async (
  modsRoot: string,
  modFolderName: string,
): Promise<Mo2Meta | null> => {
  const metaPath = path.join(modsRoot, modFolderName, "meta.ini");
  try {
    const raw = await fs.readFile(metaPath, "utf-8");
    const parsed = parseMo2MetaIni(raw);
    if (!parsed) {
      await logger.debug(
        `[MO2] meta.ini for mod="${modFolderName}" under "${modsRoot}" contained no usable metadata.`,
      );
      return null;
    }
    return parsed;
  } catch (error) {
    const err = error as NodeJS.ErrnoException;
    if (err.code === "ENOENT") {
      return null;
    }
    await logger.warn(
      `[MO2] Failed to read meta.ini for mod="${modFolderName}" under "${modsRoot}": ${err.message}`,
    );
    return null;
  }
};


