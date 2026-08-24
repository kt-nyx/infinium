import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import process from "node:process";

if (process.argv.length !== 3 || process.argv[2] !== "--check") {
  throw new Error("Historical semantic packages are check-only. Usage: node fixtures/tooling/reseal-live-semantic-v2.mjs --check");
}

const root = path.resolve(process.cwd());
if (!fs.existsSync(path.join(root, "Infinium.sln"))) {
  throw new Error("Run the historical-integrity check from the Infinium repository root.");
}

const packages = [
  "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2",
  "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2",
  "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2",
  "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2",
  "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2",
];

const resolveInside = (relative) => {
  const value = path.resolve(root, relative);
  if (!value.startsWith(`${root}${path.sep}`)) throw new Error(`Escaping path: ${relative}`);
  return value;
};
const read = (relative) => fs.readFileSync(resolveInside(relative));
const sha = (bytes) => crypto.createHash("sha256").update(bytes).digest("hex");

for (const packageRoot of packages) {
  const reclassificationPath = `${packageRoot}/reclassification.v2.json`;
  const reclassification = JSON.parse(read(reclassificationPath).toString("utf8"));
  if (reclassification.to_partition !== "development" || reclassification.current_semantic_authority !== false) {
    throw new Error(`${reclassificationPath} is not historical non-authorizing development evidence.`);
  }
  const retained = reclassification.retained_manifest;
  const bytes = read(`${packageRoot}/${retained.path}`);
  if (bytes.length !== retained.bytes || sha(bytes) !== retained.sha256) {
    throw new Error(`${packageRoot} retained manifest binding drifted.`);
  }
}

process.stdout.write(`Historical semantic package integrity: ${packages.length} reclassifications verified; no files written.\n`);
