import { defineConfig } from "oxlint";

// The rules eslint-config-next's core-web-vitals preset turns on, all as errors.
const nextjsRules = [
  "google-font-display",
  "google-font-preconnect",
  "inline-script-id",
  "next-script-for-ga",
  "no-assign-module-variable",
  "no-async-client-component",
  "no-before-interactive-script-outside-document",
  "no-css-tags",
  "no-document-import-in-page",
  "no-duplicate-head",
  "no-head-element",
  "no-head-import-in-document",
  "no-html-link-for-pages",
  "no-img-element",
  "no-page-custom-font",
  "no-script-component-in-head",
  "no-styled-jsx-in-document",
  "no-sync-scripts",
  "no-title-in-document-head",
  "no-typos",
  "no-unwanted-polyfillio",
] as const;

export default defineConfig({
  plugins: ["typescript", "react", "nextjs", "jsx-a11y", "import", "unicorn", "oxc"],
  categories: {
    correctness: "error",
    suspicious: "error",
    perf: "warn",
  },
  rules: {
    ...Object.fromEntries(nextjsRules.map((rule) => [`nextjs/${rule}`, "error"])),
    "react/rules-of-hooks": "error",
    "react/exhaustive-deps": "error",
    "import/no-cycle": "error",
    "import/no-unassigned-import": ["error", { allow: ["**/*.css", "**/*.scss"] }],

    // Off on purpose:
    // React 19's JSX transform needs no React in scope.
    "react/react-in-jsx-scope": "off",
    // Flags exhaustive switches and calls to redirect()/notFound() (which never return); tsc already checks returns.
    "typescript/consistent-return": "off",
    // Backend replies are cast to their wire types on purpose; validating them is a separate decision.
    "typescript/no-unsafe-type-assertion": "off",
    // The single-field Join and Host login forms focus their field on purpose.
    "jsx-a11y/no-autofocus": "off",
    // role="status"/"img" are deliberate: <output> is for form results and the QR code is inline SVG markup.
    "jsx-a11y/prefer-tag-over-role": "off",
    // Index keys only appear on lists rendered once and never reordered (problems, a Recap's Questions).
    "react/no-array-index-key": "off",
  },
  options: {
    // Type-aware rules (oxlint-tsgolint) and TypeScript errors, so lint catches what only `next build` used to.
    typeAware: true,
    typeCheck: true,
    denyWarnings: true,
    reportUnusedDisableDirectives: "error",
  },
  ignorePatterns: [".next/**", "out/**", "build/**", "next-env.d.ts"],
});
