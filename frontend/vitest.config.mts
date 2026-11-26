import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Unit tests of framework-free modules (e.g. lib/live-masterclass through its fake hub): plain Node, no DOM.
export default defineConfig({
  resolve: { alias: { "@": fileURLToPath(new URL("./", import.meta.url)) } },
  test: { environment: "node", include: ["**/*.test.ts"], exclude: ["node_modules/**", ".next/**"] },
});
