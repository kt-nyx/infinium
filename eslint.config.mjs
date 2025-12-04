import jiti from "jiti";

// Allow ESLint (and editors) to load the TypeScript flat config.
const require = jiti(import.meta.url);

export default require("./eslint.config.ts").default;
















