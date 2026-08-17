import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import process from "node:process";

const mode = process.argv.length === 2 ? "--check" : process.argv[2];
if ((mode !== "--check" && mode !== "--write") || process.argv.length > 3) {
  throw new Error("Usage: node fixtures/tooling/reseal-live-semantic-v2.mjs [--check|--write]");
}

const root = process.cwd();
if (!fs.existsSync(path.join(root, "Infinium.sln"))) {
  throw new Error("Run the v2 resealer from the Infinium repository root.");
}

const sourceRoot = "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2";
const candidateRoot = "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2";
const liveRoot = "fixtures/public/provider/live-campaign";
const manifestPaths = [
  `${sourceRoot}/public-manifest.json`,
  `${candidateRoot}/public-manifest.json`,
  `${liveRoot}/LLM-CLAIM-LIVE-VAL-v2/public-manifest.json`,
  `${liveRoot}/LLM-INVESTIGATE-LIVE-VAL-v2/public-manifest.json`,
  `${liveRoot}/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json`,
];
const registryPath = "fixtures/public/public-fixture-registry.v2.json";
const allowedWrites = new Set([...manifestPaths, registryPath]);

const absolute = (relative) => {
  const result = path.resolve(root, relative);
  if (!result.startsWith(`${path.resolve(root)}${path.sep}`)) throw new Error(`Escaping path: ${relative}`);
  return result;
};
const read = (relative) => fs.readFileSync(absolute(relative));
const json = (relative) => JSON.parse(read(relative).toString("utf8"));
const canonical = (value) => Buffer.from(`${JSON.stringify(value, null, 2)}\n`, "utf8");
const sha = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");
const binding = (relative, bytes = read(relative)) => ({ path: relative, bytes: bytes.length, sha256: sha(bytes) });

function inputManifest(relativeRoot) {
  const value = json(`${relativeRoot}/public-manifest.json`);
  const physical = fs.readdirSync(absolute(relativeRoot)).sort();
  const expected = ["context-manifest.v2.json", "execution-input.v2.json", "oracle-provenance.v2.json",
    "oracle.v2.json", "partition-history.v2.json", "public-manifest.json"];
  if (JSON.stringify(physical) !== JSON.stringify(expected)) throw new Error(`${relativeRoot} file closure drifted.`);
  for (const identity of value.file_identities) {
    const bytes = read(`${relativeRoot}/${identity.path}`);
    identity.bytes = bytes.length;
    identity.sha256 = sha(bytes);
  }
  return canonical(value);
}

const generated = new Map();
generated.set(manifestPaths[0], inputManifest(sourceRoot));
generated.set(manifestPaths[1], inputManifest(candidateRoot));

function liveManifest(packageId, inputPath, predecessorPath, oraclePath) {
  const relative = `${liveRoot}/${packageId}/public-manifest.json`;
  const value = json(relative);
  value.product_input = binding(inputPath);
  const predecessorBytes = generated.get(predecessorPath) ?? read(predecessorPath);
  value.predecessor_manifest = binding(predecessorPath, predecessorBytes);
  const oracle = binding(oraclePath);
  value.oracle.bytes = oracle.bytes;
  value.oracle.sha256 = oracle.sha256;
  return canonical(value);
}

generated.set(manifestPaths[2], liveManifest("LLM-CLAIM-LIVE-VAL-v2",
  `${sourceRoot}/execution-input.v2.json`, manifestPaths[0], `${liveRoot}/LLM-CLAIM-LIVE-VAL-v2/oracle.v2.json`));
generated.set(manifestPaths[3], liveManifest("LLM-INVESTIGATE-LIVE-VAL-v2",
  `${candidateRoot}/execution-input.v2.json`, manifestPaths[1], `${liveRoot}/LLM-INVESTIGATE-LIVE-VAL-v2/oracle.v2.json`));

{
  const relative = manifestPaths[4];
  const value = json(relative);
  const qualificationPath = value.qualification.manifest_path;
  const qualification = binding(qualificationPath);
  value.qualification.manifest_bytes = qualification.bytes;
  value.qualification.manifest_sha256 = qualification.sha256;
  for (const stage of value.stage_wrappers) {
    const bytes = generated.get(stage.manifest_path);
    if (!bytes) throw new Error(`Unknown v2 stage manifest: ${stage.manifest_path}`);
    stage.manifest_bytes = bytes.length;
    stage.manifest_sha256 = sha(bytes);
  }
  const oraclePath = `${liveRoot}/PROV-LIVE-COMPOSED-VAL-v2/oracle.v2.json`;
  const oracle = binding(oraclePath);
  value.oracle.bytes = oracle.bytes;
  value.oracle.sha256 = oracle.sha256;
  generated.set(relative, canonical(value));
}

{
  const value = json(registryPath);
  if (value.schema_identity !== "infinium.repository.public-fixture-registry/1.7.0"
      || value.registry_version !== "1.7.0" || value.package_count !== 43 || value.packages.length !== 43) {
    throw new Error("Registry v2 identity/count is not exact.");
  }
  for (const entry of value.packages.slice(38)) {
    if (!manifestPaths.includes(entry.authority_file)) throw new Error(`Unexpected v2 registry authority: ${entry.authority_file}`);
    const bytes = generated.get(entry.authority_file);
    entry.authority_bytes = bytes.length;
    entry.authority_sha256 = sha(bytes);
  }
  generated.set(registryPath, canonical(value));
}

let differences = 0;
for (const [relative, expected] of generated) {
  if (!allowedWrites.has(relative)) throw new Error(`Write-set escape: ${relative}`);
  const current = read(relative);
  if (!current.equals(expected)) {
    differences++;
    if (mode === "--write") fs.writeFileSync(absolute(relative), expected);
    else process.stderr.write(`drift: ${relative}\n`);
  }
}

if (mode === "--check" && differences !== 0) {
  throw new Error(`${differences} live-semantic v2 authority files require resealing.`);
}
process.stdout.write(JSON.stringify({ mode, write_scope: [...allowedWrites], differences,
  v1_writes: 0, package_manifests: 5, registry_entries: 43 }) + "\n");
