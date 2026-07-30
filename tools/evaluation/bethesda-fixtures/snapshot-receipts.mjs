import { createHash } from "node:crypto";
import { readFile, readdir, stat, writeFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, "../../..");
const fixtureRoot = path.join(
  repositoryRoot,
  "test-data",
  "evaluation",
  "m1-semantic",
);
const fixtureIds = [
  "BETH-NPC-DEV",
  "BETH-REFR-DEV",
  "BETH-LIGHT-VAL",
  "BETH-MALFORMED-VAL",
  "BETH-UNSUPPORTED-VAL",
];
const pluginExtensions = new Set([".esm", ".esp", ".esl"]);

for (const fixtureId of fixtureIds) {
  const inputRoot = path.join(fixtureRoot, fixtureId, "inputs");
  const constructionPath = path.join(inputRoot, "construction-manifest.json");
  const constructionBytes = await readFile(constructionPath);
  const construction = JSON.parse(constructionBytes);
  const plugins = construction.files
    .filter((file) => file.path.startsWith("plugins/"))
    .filter((file) => pluginExtensions.has(path.extname(file.path).toLowerCase()))
    .sort((left, right) => left.path.localeCompare(right.path, "en"));
  const mutations = construction.files
    .filter((file) => file.path.startsWith("mutations/"))
    .filter((file) => pluginExtensions.has(path.extname(file.path).toLowerCase()))
    .sort((left, right) => left.path.localeCompare(right.path, "en"));

  const providers = plugins.map((plugin, index) => ({
    provider_id: `${fixtureId.toLowerCase()}-provider-${String(index).padStart(2, "0")}`,
    priority: index,
    source_artifact_id: `inputs/${plugin.path}`,
    source_sha256: plugin.sha256,
  }));
  const pluginOrder = plugins.map((plugin, index) => ({
    load_order: index,
    file_name: path.basename(plugin.path),
    artifact_id: `inputs/${plugin.path}`,
    sha256: plugin.sha256,
    provider_id: providers[index].provider_id,
  }));
  const captureBindingValue = canonicalJson({ providers, plugin_order: pluginOrder });
  const captureBindingFingerprint = sha256(Buffer.from(captureBindingValue, "utf8"));
  const receipt = {
    schema_id: "infinium.evaluation.bethesda-snapshot-input/v1",
    schema_version: "1",
    fixture_id: fixtureId,
    fixture_version: "1.0.0",
    snapshot_contract_version: "3.0.0",
    adapter_id: "infinium.mo2-static-reconstruction/v3",
    selected_profile_name: fixtureId,
    construction_manifest_fingerprint: sha256(constructionBytes),
    provider_order: providers,
    plugin_order: pluginOrder,
    isolated_capture_variants: mutations.map((mutation) => ({
      artifact_id: `inputs/${mutation.path}`,
      sha256: mutation.sha256,
    })),
    capture_binding_algorithm: "infinium.fixture-snapshot-capture-binding/v1",
    expected_capture_binding_fingerprint: captureBindingFingerprint,
    capture_policy: {
      manager_launch: "forbidden",
      usvfs_launch: "forbidden",
      protected_root_write: "forbidden",
      supplied_order_only: true,
    },
  };

  const snapshotDirectory = path.join(inputRoot, "snapshot");
  await mkdir(snapshotDirectory, { recursive: true });
  const outputPath = path.join(snapshotDirectory, "accepted-order.json");
  await writeFile(outputPath, `${JSON.stringify(receipt, null, 2)}\n`, "utf8");

  const output = await readFile(outputPath);
  const outputStats = await stat(outputPath);
  process.stdout.write(
    `${fixtureId} ${outputStats.size} ${sha256(output)} ${captureBindingFingerprint}\n`,
  );
}

function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function canonicalJson(value) {
  if (Array.isArray(value)) {
    return `[${value.map(canonicalJson).join(",")}]`;
  }

  if (value !== null && typeof value === "object") {
    return `{${Object.keys(value)
      .sort()
      .map((key) => `${JSON.stringify(key)}:${canonicalJson(value[key])}`)
      .join(",")}}`;
  }

  return JSON.stringify(value);
}
