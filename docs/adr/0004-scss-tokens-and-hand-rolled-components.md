# SCSS tokens, CSS custom property themes and hand-rolled components

The frontend is styled with Sass (`sass`, a dev dependency) and CSS Modules, with no UI library. Build-time values (spacing, radii, type scale, breakpoints, motion, mixins) are Sass variables in `frontend/styles/`, reached from any `*.module.scss` with `@use "design" as *;` (Next puts `styles/` on the Sass load path). Theme colours are semantic tokens in Sass maps (`styles/_palette.scss`) that `app/globals.scss` emits as `--color-*` custom properties, so the theme can switch at runtime without rebuilding. The primitives (button, card, notice, option tile, leaderboard, page layout, theme toggle) live in `frontend/components/` with their own modules; screens compose them and keep only layout in their own module. Inline `style` objects are not used.

The theme is System, Dark or Light. An explicit choice is a client-written cookie (`mq_theme`) read by a blocking inline `<head>` script that sets `data-theme` on `<html>` before first paint. System is the absence of the attribute, handled by a `prefers-color-scheme` media query. The server never reads the cookie, so every route keeps its static shell.

## Considered options

- **A UI library (Tailwind, a component kit)**: rejected. The look is specific, the screen count is small, and Sass is the only dependency we wanted to add.
- **Reading the theme cookie in the root layout**: rejected. Under Cache Components it makes every route request-bound and loses the static shells.
- **Sharing tokens with Sass `:export`**: doesn't work under Turbopack, so tokens are shared as custom properties instead.

## Consequences

- `styles/palette.test.ts` checks every text and background pair in both themes for WCAG AA. A palette change that fails contrast fails `npm test`.
- The mono font (JetBrains Mono, via `next/font/google`) is downloaded at build time, so `docker build` needs network. Switch to `next/font/local` with a committed `.woff2` if builds must run offline.
- The inline theme script needs a nonce if a strict Content Security Policy is ever added.
