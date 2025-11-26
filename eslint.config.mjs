import js from "@eslint/js";
import tsPlugin from "@typescript-eslint/eslint-plugin";
import tsParser from "@typescript-eslint/parser";
import importX from "eslint-plugin-import-x";
import reactHooks from "eslint-plugin-react-hooks";

const tsLanguageOptions = {
  parser: tsParser,
  parserOptions: {
    ecmaVersion: "latest",
    sourceType: "module"
  }
};

export default [
  js.configs.recommended,
  {
    files: ["src/**/*.{ts,tsx}"],
    languageOptions: tsLanguageOptions,
    plugins: {
      "@typescript-eslint": tsPlugin,
      "import-x": importX
    },
    rules: {
      "no-undef": "off",
      "no-unused-vars": "off",
      "@typescript-eslint/consistent-type-imports": ["error", { prefer: "type-imports" }],
      "@typescript-eslint/no-unused-vars": ["warn", { argsIgnorePattern: "^_" }],
      "import-x/no-relative-packages": "warn"
    }
  },
  {
    files: ["src/renderer/**/*.{ts,tsx}"],
    languageOptions: {
      ...tsLanguageOptions,
      globals: {
        window: true,
        document: true
      }
    },
    plugins: {
      "react-hooks": reactHooks
    },
    rules: {
      ...reactHooks.configs.recommended.rules
    }
  },
  {
    files: ["src/main/**/*.{ts,tsx}", "src/shared/**/*.{ts,tsx}", "test/**/*.{ts,tsx}"],
    languageOptions: {
      ...tsLanguageOptions,
      globals: {
        window: false,
        document: false,
        console: true,
        process: true,
        __dirname: true
      }
    }
  }
];