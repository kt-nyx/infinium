import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

const projectRoot = path.resolve(__dirname, "..");

export default defineConfig({
  root: path.resolve(projectRoot, "src/renderer"),
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true
  },
  build: {
    outDir: path.resolve(projectRoot, "dist/renderer"),
    emptyOutDir: true
  },
  resolve: {
    alias: {
      "@shared": path.resolve(projectRoot, "src/shared"),
      "@renderer": path.resolve(projectRoot, "src/renderer"),
      "@main": path.resolve(projectRoot, "src/main")
    }
  }
});


