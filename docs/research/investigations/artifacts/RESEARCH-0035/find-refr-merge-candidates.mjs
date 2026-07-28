#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { inspectPlugin } from "./tes4-inspect.mjs";

const STRUCTURAL_SUBRECORDS = new Set(["XESP", "XLKR"]);

function pluginPaths(root) {
  const results = [];
  const pending = [root];
  while (pending.length > 0) {
    const directory = pending.pop();
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const fullPath = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        pending.push(fullPath);
      } else if (/\.(esm|esp|esl)$/iu.test(entry.name)) {
        results.push(fullPath);
      }
    }
  }
  return results;
}

function canonicalKey(plugin, record) {
  const raw = Number.parseInt(record.formId.slice(2), 16);
  const index = raw >>> 24;
  if (index === 0xfe) {
    return null;
  }
  const origin =
    index < plugin.masters.length
      ? plugin.masters[index]
      : index === plugin.masters.length
        ? plugin.file
        : null;
  if (!origin) {
    return null;
  }
  return `${origin.toLowerCase()}|${(raw & 0x00ffffff)
    .toString(16)
    .padStart(6, "0")}`;
}

function signature(record, allowed) {
  return record.subrecords
    .filter((subrecord) => allowed.has(subrecord.signature))
    .map((subrecord) => `${subrecord.signature}:${subrecord.hex}`)
    .join("|");
}

function recordMap(plugin) {
  const map = new Map();
  for (const record of plugin.records) {
    if (record.signature !== "REFR") {
      continue;
    }
    const key = canonicalKey(plugin, record);
    if (key) {
      map.set(key, record);
    }
  }
  return map;
}

function main() {
  const [modsRoot, outputPath] = process.argv.slice(2);
  if (!modsRoot) {
    throw new Error(
      "Usage: node find-refr-merge-candidates.mjs <mods-root> [output.json]",
    );
  }

  const locations = pluginPaths(modsRoot);
  const byName = new Map();
  for (const location of locations) {
    const name = path.basename(location).toLowerCase();
    if (!byName.has(name)) {
      byName.set(name, []);
    }
    byName.get(name).push(location);
  }

  const cache = new Map();
  function load(location) {
    if (!cache.has(location)) {
      const plugin = inspectPlugin(location);
      cache.set(location, { plugin, records: recordMap(plugin) });
    }
    return cache.get(location);
  }

  const candidates = [];
  const failures = [];
  for (const patchPath of locations) {
    let patch;
    try {
      patch = load(patchPath);
    } catch (error) {
      failures.push({ file: path.basename(patchPath), error: error.message });
      continue;
    }
    if (patch.plugin.masters.length < 2 || patch.records.size === 0) {
      continue;
    }

    const availableMasters = patch.plugin.masters.flatMap((master) =>
      (byName.get(master.toLowerCase()) ?? []).map((location) => ({
        master,
        location,
      })),
    );
    for (let leftIndex = 0; leftIndex < availableMasters.length; leftIndex += 1) {
      for (
        let rightIndex = leftIndex + 1;
        rightIndex < availableMasters.length;
        rightIndex += 1
      ) {
        const leftInfo = availableMasters[leftIndex];
        const rightInfo = availableMasters[rightIndex];
        if (leftInfo.location === rightInfo.location) {
          continue;
        }
        let left;
        let right;
        try {
          left = load(leftInfo.location);
          right = load(rightInfo.location);
        } catch (error) {
          failures.push({
            file: `${path.basename(leftInfo.location)} / ${path.basename(rightInfo.location)}`,
            error: error.message,
          });
          continue;
        }

        for (const [key, patchRecord] of patch.records) {
          const leftRecord = left.records.get(key);
          const rightRecord = right.records.get(key);
          if (!leftRecord || !rightRecord) {
            continue;
          }

          const structuralAllowed = STRUCTURAL_SUBRECORDS;
          const dataAllowed = new Set(["DATA"]);
          const patchStructural = signature(patchRecord, structuralAllowed);
          const leftStructural = signature(leftRecord, structuralAllowed);
          const rightStructural = signature(rightRecord, structuralAllowed);
          const patchData = signature(patchRecord, dataAllowed);
          const leftData = signature(leftRecord, dataAllowed);
          const rightData = signature(rightRecord, dataAllowed);

          if (
            leftStructural === rightStructural ||
            leftData === rightData ||
            patchStructural.length === 0
          ) {
            continue;
          }

          let structuralSource = null;
          let placementSource = null;
          if (
            patchStructural === leftStructural &&
            patchData === rightData &&
            patchData !== leftData
          ) {
            structuralSource = leftInfo.master;
            placementSource = rightInfo.master;
          } else if (
            patchStructural === rightStructural &&
            patchData === leftData &&
            patchData !== rightData
          ) {
            structuralSource = rightInfo.master;
            placementSource = leftInfo.master;
          }
          if (!structuralSource) {
            continue;
          }

          candidates.push({
            patch: patch.plugin.file,
            record: key,
            structuralSource,
            placementSource,
            patchStructural,
            patchData,
            left: {
              file: leftInfo.master,
              structural: leftStructural,
              data: leftData,
            },
            right: {
              file: rightInfo.master,
              structural: rightStructural,
              data: rightData,
            },
          });
        }
      }
    }
  }

  const result = {
    schema: "infinium-research-refr-merge-candidates/1",
    generatedAt: new Date().toISOString(),
    pluginCount: locations.length,
    candidateCount: candidates.length,
    candidates,
    parseFailureCount: failures.length,
    failures,
  };
  const json = `${JSON.stringify(result, null, 2)}\n`;
  if (outputPath) {
    fs.writeFileSync(outputPath, json);
  } else {
    process.stdout.write(json);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
