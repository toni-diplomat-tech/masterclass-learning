# Oxlint, type-aware, lints and type-checks the frontend

The frontend is linted by oxlint instead of ESLint with `eslint-config-next`. Its config is TypeScript (`frontend/oxlint.config.mts`), and it runs type-aware through `oxlint-tsgolint` with type checking on, so `npm run lint` reports lint findings, type-aware findings (floating promises, base-to-string, …) and TypeScript errors in one fast pass. Before, type errors only surfaced in `next build` and no type-aware rules ran at all. The config enables the `nextjs`, `react` (with the hooks rules), `jsx-a11y` and `import` plugins to cover what `next/core-web-vitals` and `next/typescript` checked; every rule turned off is listed there with its reason.

## Consequences

- Oxlint type-checks with TypeScript 7 (Go), while `next build` still uses the project's TypeScript 5; on a rare disagreement, `next build` is the final word.
- A TypeScript config needs Node 22.18 or newer wherever lint runs.
- `denyWarnings` is on: a warning fails lint, so rules are either errors or off.
