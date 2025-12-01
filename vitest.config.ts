import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "node:path";

const projectRoot = __dirname;

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: "happy-dom",
    setupFiles: [],
    include: ["src/**/*.{test,spec}.{ts,tsx}", "test/**/*.{test,spec}.{ts,tsx}"],
  },
  resolve: {
    alias: {
      "@shared": path.resolve(projectRoot, "src/shared"),
      "@main": path.resolve(projectRoot, "src/main"),
      "@renderer": path.resolve(projectRoot, "src/renderer"),
    },
  },
});












