#!/usr/bin/env node

import { spawn } from "node:child_process";
import { copyFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, "..");

const isWindows = process.platform === "win32";
const exeName = isWindows ? "loot-helper.exe" : "loot-helper";

const lootHelperDir = path.join(projectRoot, "loot-helper");
const buildOutputPath = path.join(lootHelperDir, "target", "release", exeName);
const resourcesLootHelperDir = path.join(projectRoot, "resources", "loot-helper");
const destPath = path.join(resourcesLootHelperDir, exeName);

async function run() {
  console.log("[loot-helper] Building Rust helper via cargo --release...");

  await new Promise((resolve, reject) => {
    const child = spawn("cargo", ["build", "--release"], {
      cwd: lootHelperDir,
      stdio: "inherit",
    });

    child.on("error", (error) => {
      reject(error);
    });

    child.on("close", (code) => {
      if (code !== 0) {
        reject(new Error(`cargo build --release exited with code ${code}`));
        return;
      }
      resolve();
    });
  });

  console.log(`[loot-helper] Copying built helper from "${buildOutputPath}" to "${destPath}"...`);
  await mkdir(resourcesLootHelperDir, { recursive: true });
  await copyFile(buildOutputPath, destPath);

  console.log("[loot-helper] Helper build and copy completed.");
}

run().catch((error) => {
  console.error("[loot-helper] Failed to build helper:", error);
  process.exitCode = 1;
});

