import { promises as fs } from "node:fs";
import path from "node:path";
import type { ModInfo, ProfileSnapshot } from "../../shared/types";
import { logger } from "../logging";

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
    const profiles = entries
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .sort();
    await logger.debug(
      `[MO2] Discovered ${profiles.length} profiles under instance="${instancePath}": ` +
        profiles.join(", "),
    );
    return profiles;
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

  const mods = lines
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

  await logger.debug(
    `[MO2] Parsed modlist for profile="${profileId}" at "${modlistPath}": ` +
      `totalLines=${lines.length}, mods=${mods.length}`,
  );

  return mods;
};

const parsePlugins = async (instancePath: string, profileId: string): Promise<string[]> => {
  const profileDir = path.join(instancePath, "profiles", profileId);
  const pluginsPath = path.join(profileDir, "plugins.txt");
  const loadOrderPath = path.join(profileDir, "loadorder.txt");

  const pluginLines = await readLines(pluginsPath);
  const loadOrderLines = await readLines(loadOrderPath);

  const parseList = (lines: string[]) => lines.filter((line) => line && !line.startsWith("#"));

  const rawPluginEntries = parseList(pluginLines);
  // loadorder.txt entries are plain plugin names; only plugins.txt uses a
  // leading "*" marker. Do not strip anything here.
  const loadOrderAll = parseList(loadOrderLines);

  // Build a map of explicit MO2-managed plugin states from plugins.txt:
  // - lines starting with "*" are explicitly active
  // - lines without "*" are explicitly inactive
  const explicitState = new Map<string, "active" | "inactive">();
  rawPluginEntries.forEach((line) => {
    const isActive = line.startsWith("*");
    const name = line.replace(/^\*+/, "");
    if (!name) return;
    explicitState.set(name.toLowerCase(), isActive ? "active" : "inactive");
  });

  // If loadorder.txt is missing, fall back to plugins.txt semantics only.
  if (!loadOrderAll.length) {
    const fallback = rawPluginEntries
      .filter((line) => line.startsWith("*"))
      .map((line) => line.replace(/^\*+/, ""));
    await logger.debug(
      `[MO2] loadorder.txt missing for profile="${profileId}", using plugins.txt only: ` +
        `totalLines=${rawPluginEntries.length}, activePlugins=${fallback.length}`,
    );
    return fallback;
  }

  // If plugins.txt has no entries at all, treat everything present in
  // loadorder.txt as active (MO2/game fallback semantics).
  if (!rawPluginEntries.length) {
    await logger.debug(
      `[MO2] plugins.txt missing or empty; using full loadorder.txt for profile="${profileId}" ` +
        `with ${loadOrderAll.length} plugins.`,
    );
    return loadOrderAll;
  }

  // Main rule set:
  // - Any plugin that appears in loadorder.txt but NOT in plugins.txt is
  //   considered active and kept in the exact order from loadorder.txt.
  // - Any plugin that appears in both files and is prefixed with "*" in
  //   plugins.txt is active and kept in the exact order from loadorder.txt.
  // - Any plugin that appears in both files but is NOT prefixed with "*" in
  //   plugins.txt is considered inactive and excluded.
  const pluginsInOrder: string[] = [];

  loadOrderAll.forEach((name) => {
    const lower = name.toLowerCase();
    const state = explicitState.get(lower);

    if (state === "inactive") {
      // Explicitly disabled in plugins.txt; skip.
      return;
    }

    // Either explicitly active ("*") or not present in plugins.txt at all
    // (managed outside MO2) -> treat as active.
    pluginsInOrder.push(name);
  });

  await logger.debug(
    `[MO2] Parsed plugins for profile="${profileId}" from plugins.txt (total=${rawPluginEntries.length}) ` +
      `and loadorder.txt (total=${loadOrderAll.length}): resultingActivePlugins=${pluginsInOrder.length}`,
  );

  return pluginsInOrder;
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

  await logger.debug(
    `[MO2] Finished scan for profile="${profileId}" in instance="${instancePath}": ` +
      `mods=${mods.length}, plugins=${pluginLoadOrder.length}`,
  );

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
