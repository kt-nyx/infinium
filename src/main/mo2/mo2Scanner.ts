import { promises as fs } from "node:fs";
import path from "node:path";
import type { ModInfo, ProfileSnapshot } from "../../shared/types";

const readLines = async (filePath: string): Promise<string[]> => {
  try {
    const content = await fs.readFile(filePath, "utf-8");
    return content.split(/\r?\n/).map((line) => line.trim());
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") {
      return [];
    }
    throw error;
  }
};

export const getProfiles = async (instancePath: string): Promise<string[]> => {
  const profilesDir = path.join(instancePath, "profiles");
  try {
    const entries = await fs.readdir(profilesDir, { withFileTypes: true });
    return entries
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .sort();
  } catch (error) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") {
      return [];
    }
    throw error;
  }
};

const parseModList = async (instancePath: string, profileId: string): Promise<ModInfo[]> => {
  const modlistPath = path.join(instancePath, "profiles", profileId, "modlist.txt");
  const modsDir = path.join(instancePath, "mods");
  const lines = await readLines(modlistPath);

  return lines
    .filter((line) => line && !line.startsWith("#"))
    .map((line) => {
      const enabled = line.startsWith("+");
      const name = line.slice(1).trim();
      return {
        id: name,
        name,
        enabled,
        path: path.join(modsDir, name),
        plugins: [],
        metadata: {
          // TODO: Parse mod meta.ini/meta.json for richer metadata.
        },
      } satisfies ModInfo;
    });
};

const parsePlugins = async (instancePath: string, profileId: string): Promise<string[]> => {
  const profileDir = path.join(instancePath, "profiles", profileId);
  const candidates = ["plugins.txt", "loadorder.txt"];

  for (const file of candidates) {
    const filePath = path.join(profileDir, file);
    const lines = await readLines(filePath);
    if (lines.length > 0) {
      return lines
        .filter((line) => line && !line.startsWith("#"))
        .map((line) => line.replace(/^\*+/, ""));
    }
  }

  return [];
};

const attachPluginsToMods = (mods: ModInfo[], pluginLoadOrder: string[]): void => {
  pluginLoadOrder.forEach((pluginName) => {
    const owningMod = mods.find((mod) =>
      pluginName.toLowerCase().startsWith(mod.name.toLowerCase()),
    );
    if (owningMod) {
      owningMod.plugins.push(pluginName);
    } else {
      // Plugins like official DLCs may not match mod folder names; keep them unattached for now.
    }
  });
};

const inferGame = (): ProfileSnapshot["game"] => {
  // TODO: Inspect MO2 ini/meta files or the instance name to determine Skyrim edition.
  return "SkyrimSE";
};

export const scanProfile = async (
  instancePath: string,
  profileId: string,
): Promise<ProfileSnapshot> => {
  const mods = await parseModList(instancePath, profileId);
  const pluginLoadOrder = await parsePlugins(instancePath, profileId);
  attachPluginsToMods(mods, pluginLoadOrder);

  return {
    profileId,
    game: inferGame(),
    mo2InstancePath: instancePath,
    mods,
    pluginLoadOrder,
    lootAvailable: false,
    nexusAvailable: false,
  };
};
