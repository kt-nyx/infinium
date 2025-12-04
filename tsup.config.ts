import { defineConfig } from "tsup";

export default defineConfig((options) => ({
  entry: {
    main: "src/main/main.ts",
    preload: "src/main/preload.ts",
  },
  format: ["cjs"],
  external: ["electron"],
  dts: false,
  minify: false,
  sourcemap: true,
  clean: !options.watch,
  outDir: "dist/main",
  outExtension: () => ({ js: ".cjs" }),
  platform: "node",
  target: "es2020",
  env: {
    NODE_ENV: process.env.NODE_ENV ?? "development",
  },
  onSuccess: options.watch ? "" : undefined,
}));
