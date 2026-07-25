import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import reactPlugin from "eslint-plugin-react";
import reactHooksPlugin from "eslint-plugin-react-hooks";
import importX from "eslint-plugin-import-x";

// Flat ESLint configuration for JS/TS + React, with type-aware rules.
export default tseslint.config(
  // 1. Ignore build artifacts and dependencies
  {
    ignores: [
      "dist/**",
      "node_modules/**",
      // Ignore generated declaration files and JS build outputs in src.
      "**/*.d.ts",
      "src/**/*.js",
    ],
  },

  // 2. Base config for source and tests
  {
    files: ["src/**/*.{ts,tsx,js,jsx}"],
    extends: [
      js.configs.recommended,
      ...tseslint.configs.recommendedTypeChecked,
      reactPlugin.configs.flat.recommended,
    ],
    plugins: {
      "react-hooks": reactHooksPlugin,
      import: importX,
    },
    languageOptions: {
      parserOptions: {
        // Use the project tsconfigs so ESLint gets full type information.
        project: ["./tsconfig.main.json", "./tsconfig.renderer.json"],
      },
      globals: {
        // Electron app touches both browser (renderer) and Node (main) APIs.
        ...globals.browser,
        ...globals.node,
      },
    },
    settings: {
      react: {
        // Let eslint-plugin-react auto-detect the installed React version.
        version: "detect",
      },
    },
    rules: {
      // Modern JSX transform (no need to import React in every file)
      "react/react-in-jsx-scope": "off",
      "react/jsx-uses-react": "off",

      // Hooks best practices
      "react-hooks/rules-of-hooks": "error",
      "react-hooks/exhaustive-deps": "warn",
    },
  },

  // 3. Tests: lint without full type-checking to avoid project lookup issues
  {
    files: ["test/**/*.{ts,tsx,js,jsx}"],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      globals: {
        ...globals.node,
        ...globals.browser,
      },
    },
  },

  // 4. Node-specific globals for the main process and build tooling
  {
    files: ["src/main/**/*.{ts,tsx,js,jsx}", "config/**/*.{ts,js,cjs,mjs}"],
    languageOptions: {
      globals: {
        ...globals.node,
      },
    },
  }
);
