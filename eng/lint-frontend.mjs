import { readFileSync, readdirSync } from "node:fs";
import { extname, resolve } from "node:path";

const root = resolve(import.meta.dirname, "../src/Infinium.Frontend");
const files = readdirSync(root, { recursive: true }).filter((path) => extname(path) === ".ts");
const forbidden = [
  [/\bany\b/u, "unbounded any type"],
  [/\beval\s*\(/u, "dynamic evaluation"],
  [/new\s+Function\s*\(/u, "dynamic function construction"],
  [/\bfetch\s*\(/u, "direct network access"],
  [/\bWebSocket\b/u, "direct socket access"],
  [/(?:^|[\s.'"])(?:path|sql|command_line|credential|provider_request|filesystem|coordinator_proxy)(?:$|[\s:'"])/imu, "denied authority field"],
];
const findings = [];
for (const file of files) {
  if (file.includes("generated")) continue;
  const text = readFileSync(resolve(root, file), "utf8");
  for (const [pattern, label] of forbidden) {
    if (pattern.test(text)) findings.push(`${file}: ${label}`);
  }
}
if (findings.length > 0) throw new Error(findings.join("\n"));
process.stdout.write(`Frontend lint passed: ${files.length} TypeScript files, ${forbidden.length} enforced policies.\n`);
