#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const PLUGIN_NAME = /^[^<>:"/\\|?*\u0000-\u001F]+\.(esm|esp|esl)$/iu;

function canonicalPath(value) {
  if (
    typeof value !== "string" ||
    value.length === 0 ||
    /^[a-z]:/iu.test(value) ||
    /^[\\/]/u.test(value)
  ) {
    throw new Error("unsafe-path");
  }
  const parts = value.replaceAll("\\", "/").split("/");
  if (
    parts.some(
      (part) =>
        part.length === 0 ||
        part === "." ||
        part === ".." ||
        /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/iu.test(part),
    )
  ) {
    throw new Error("unsafe-path");
  }
  return parts.join("/").toLowerCase();
}

function sha256(value) {
  return crypto.createHash("sha256").update(value).digest("hex").toUpperCase();
}

function applicability(record) {
  if (record.deleted) {
    return { status: "coverage-gap", reason: "deleted-winning-record" };
  }
  if (!record.raceResolved) {
    return { status: "coverage-gap", reason: "race-unresolved" };
  }
  if (!record.faceGenHead) {
    return {
      status: "not-applicable",
      reason: "race-does-not-use-facegen-head",
    };
  }
  if (record.useTemplate) {
    return { status: "coverage-gap", reason: "template-source-required" };
  }
  if (record.templateTraits) {
    return {
      status: "coverage-gap",
      reason: "template-traits-source-required",
    };
  }
  if (!PLUGIN_NAME.test(record.originPlugin)) {
    return { status: "invalid-input", reason: "invalid-origin-plugin" };
  }
  const maxId = record.pluginClass === "light" ? 0x0fff : 0x00ffffff;
  if (
    !Number.isInteger(record.localId) ||
    record.localId < 0 ||
    record.localId > maxId
  ) {
    return { status: "invalid-input", reason: "local-id-out-of-range" };
  }
  return null;
}

function providerIndex(providers) {
  const index = new Map();
  for (const provider of providers) {
    const key = canonicalPath(provider.path);
    if (!index.has(key)) {
      index.set(key, []);
    }
    index.get(key).push({
      ...provider,
      key,
      hash: sha256(provider.content),
      structuralState:
        provider.structuralValid === false ? "malformed" : "not-checked",
    });
  }
  return index;
}

function detectGlobalProviderFailure(index) {
  for (const providers of index.values()) {
    if (providers.some((provider) => provider.changedDuringCapture)) {
      return { status: "invalidated", reason: "changed-during-capture" };
    }
    const bySource = new Map();
    for (const provider of providers) {
      const existing = bySource.get(provider.source);
      if (existing && existing.path !== provider.path) {
        return { status: "coverage-gap", reason: "normalization-collision" };
      }
      bySource.set(provider.source, provider);
    }
    const priorities = new Set();
    for (const provider of providers) {
      if (priorities.has(provider.priority)) {
        return {
          status: "coverage-gap",
          reason: "ambiguous-provider-priority",
        };
      }
      priorities.add(provider.priority);
    }
  }
  return null;
}

function resolveKey(key, index, archivesExcluded) {
  const providers = [...(index.get(key) ?? [])].sort(
    (left, right) => left.priority - right.priority,
  );
  if (providers.some((provider) => provider.kind !== "loose")) {
    return { key, state: "archive-unqualified", providerCount: providers.length };
  }
  if (providers.length === 0) {
    return {
      key,
      state: archivesExcluded ? "absent" : "archive-unqualified",
      providerCount: 0,
    };
  }
  const winner = providers.at(-1);
  return {
    key,
    state: "present",
    winner: winner.source,
    winnerOriginalPath: winner.path,
    winnerHash: winner.hash,
    providerCount: providers.length,
    providerChain: providers.map((provider) => ({
      source: provider.source,
      priority: provider.priority,
      originalPath: provider.path,
      comparisonKey: provider.key,
      hash: provider.hash,
    })),
    structuralState: winner.structuralState,
  };
}

function qualify(input) {
  const blocked = applicability(input.record);
  if (blocked) {
    return { id: input.id, ...blocked };
  }

  let index;
  try {
    index = providerIndex(input.providers);
  } catch (error) {
    return { id: input.id, status: "invalid-input", reason: error.message };
  }
  const providerFailure = detectGlobalProviderFailure(index);
  if (providerFailure) {
    return { id: input.id, ...providerFailure };
  }

  const fileId = input.record.localId.toString(16).padStart(8, "0");
  const origin = input.record.originPlugin.toLowerCase();
  const meshKey = `meshes/actors/character/facegendata/facegeom/${origin}/${fileId}.nif`;
  const tintKey = `textures/actors/character/facegendata/facetint/${origin}/${fileId}.dds`;
  const mesh = resolveKey(meshKey, index, input.archivesExcluded);
  const tint = resolveKey(tintKey, index, input.archivesExcluded);

  let completeness;
  let status = "supported-exact";
  if (
    mesh.state === "archive-unqualified" ||
    tint.state === "archive-unqualified"
  ) {
    completeness = "gap";
    status = "coverage-gap";
  } else if (mesh.state === "present" && tint.state === "present") {
    completeness = "complete";
  } else if (mesh.state === "absent" && tint.state === "absent") {
    completeness = "absent";
  } else {
    completeness = "partial";
  }

  return { id: input.id, status, mesh, tint, completeness };
}

function subsetEqual(actual, expected, location = "$") {
  if (expected === null || typeof expected !== "object") {
    if (actual !== expected) {
      throw new Error(`${location}: expected ${expected}, got ${actual}`);
    }
    return;
  }
  for (const [key, value] of Object.entries(expected)) {
    subsetEqual(actual?.[key], value, `${location}.${key}`);
  }
}

function main() {
  const root = path.dirname(fileURLToPath(import.meta.url));
  const inputsPath = process.argv[2] ?? path.join(root, "facegen-inputs.json");
  const expectedPath =
    process.argv[3] ?? path.join(root, "facegen-expected.json");
  const outputPath =
    process.argv[4] ?? path.join(root, "facegen-qualification-results.json");

  const inputs = JSON.parse(fs.readFileSync(inputsPath, "utf8"));
  const expectations = JSON.parse(fs.readFileSync(expectedPath, "utf8"));
  const results = [];
  const failures = [];

  for (const input of inputs.cases) {
    const result = qualify(input);
    results.push(result);
    try {
      subsetEqual(result, expectations.cases[input.id]);
    } catch (error) {
      failures.push({ id: input.id, error: error.message });
    }
  }

  const output = {
    schema: "infinium-facegen-qualification-results/1",
    generatedAt: new Date().toISOString(),
    node: process.version,
    inputCaseCount: inputs.cases.length,
    passedCaseCount: inputs.cases.length - failures.length,
    failedCaseCount: failures.length,
    failures,
    results,
  };
  fs.writeFileSync(outputPath, `${JSON.stringify(output, null, 2)}\n`);
  if (failures.length > 0) {
    process.exitCode = 1;
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
