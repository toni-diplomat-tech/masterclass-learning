# Sass, CSS Modules and next/font in Next.js 16.4

Research for #21. It covers the installed `next@16.4.6` only. Sources are the docs bundled in `frontend/node_modules/next/dist/docs/` (paths below are relative to that folder), Next's own compiled source in `frontend/node_modules/next/dist/`, and a throwaway spike built in a separate worktree and then discarded. The spike added `sass`, a shared partial, a global `.scss`, a `.module.scss`, `next/font/google` and `next/font/local`, then ran `next build`, `npm run lint`, `npm test` and `docker build`.

## Answer in short

- Run `npm i -D sass`. Plain `sass` (Dart Sass, pure JS) is the default and needs no config. `sass-embedded` is opt-in only.
- `*.module.scss` gives CSS Modules and any other `*.scss` is global. Both work under Turbopack, which `next build` uses by default.
- Share partials with `@use "../styles/tokens" as t;` (a relative path), or add `sassOptions.loadPaths` and write `@use "tokens"`. Both worked in the spike.
- Don't use `:export` to pass Sass values to JS: Turbopack doesn't support it. Use CSS custom properties.
- For the mono font, use `next/font/google`'s `JetBrains_Mono` or `next/font/local` with a committed `.woff2`. Set `variable: "--font-mono"` and put `mono.variable` on `<html>`.
- Impact on our tools: oxlint needs one config line, and only if we import a global `.scss`. Vitest is unaffected. The Docker build needs no change, but Google fonts are fetched during `docker build`.

## Sass

### Package: `sass` vs `sass-embedded`

- The docs say to install `sass` as a dev dependency. Next uses the `sass` package by default, and `sassOptions.implementation: 'sass-embedded'` switches to the other one (`02-guides/sass.md`, "Implementation").
- `next` declares `sass: ^1.3.0` as an optional peer dependency (`next/package.json`).
- The bundled `sass-loader` (`dist/compiled/sass-loader/cjs.js`) always `require("sass")` unless `implementation` is set. It uses the modern Sass API. The v16 upgrade guide says `sass-loader` was bumped to v16 "which supports modern Sass syntax" (`02-guides/upgrading/version-16.md`, "Modern Sass API").
- `sass@1.108.2` is pure JS. Its only native piece is the optional `@parcel/watcher`, which the build doesn't use, and it needs Node >= 20.19. `sass-embedded@1.108.2` pulls a platform-specific Dart binary through ~18 optional dependencies (`npm view sass-embedded optionalDependencies`). That means a Windows-made lockfile and a `linux-musl` Alpine build would have to agree, and it buys only speed we don't need.
- **Recommendation: `sass`.**

### `sassOptions`

- `sassOptions` in `next.config.ts` is typed only for `implementation`. All other keys are passed through untyped (`03-api-reference/05-config/01-next-config-js/sassOptions.md`; the schema in `dist/server/config-schema.js` is `z.object({implementation}).catchall(z.any())`).
- Next pulls out `additionalData`/`prependData` and `implementation` and hands the rest to Sass as compiler options (`dist/build/webpack/config/blocks/css/index.js`). Turbopack uses the same bundled `next/dist/compiled/sass-loader`; the native binary contains those same key strings. The spike's build log also shows the chain `resolve-url-loader, sass-loader` for a `.scss` file.
- `additionalData` is prepended to **every** `.scss` file, both modules and globals. In the spike, `additionalData: "$injected: #eee;"` reached the module's output.
- `sassOptions.functions` (custom JS Sass functions) is **not supported with Turbopack**. Config loading throws if it is set (`dist/server/config.js`; also `08-turbopack.md` "Unsupported and unplanned features" and `sassOptions.md`). We have no need for it.

### Global vs module imports

- `*.module.scss` / `*.module.sass` are CSS Modules. Import them as `import styles from "./x.module.scss"` from any component (`02-guides/sass.md`, intro; `01-getting-started/11-css.md`, "CSS Modules").
- Any other `.scss` file is global. Import it as a side effect (`import "./globals.scss"`), ideally only in the root layout. Global styles can be imported anywhere in `app/`, but Next doesn't remove them when you navigate between routes, so keep them truly global (`11-css.md`, "Global CSS").
- CSS order follows JS import order. The docs say to keep CSS imports in a single entry file and not to auto-sort imports. Check order in `next build`, because dev can differ (`11-css.md`, "Ordering and Merging", "Development vs Production").
- The spike confirmed both kinds: the module class compiled to `.IHXEBq_box{…}`, and the global `body{accent-color:#35f}` landed in the shared chunk.

### `@use` of shared partials, load paths

- Relative paths work from both modules and globals. For example, `@use "../../styles/tokens" as t;` resolves `styles/_tokens.scss` (spike).
- `sassOptions.loadPaths: ["styles"]` also works, so modules can write `@use "tokens" as t;` (spike). sass-loader joins relative `loadPaths` onto `process.cwd()` and also reads the `SASS_PATH` env var (`dist/compiled/sass-loader/cjs.js`, `getSassOptions`). The cwd is `frontend/` locally and `/app` in Docker, so `"styles"` works in both. `path.join(import.meta.dirname, "styles")` is the cwd-proof form.
- Importing from `node_modules` works, but Turbopack does **not** support webpack's `~` prefix: write `@import 'pkg/file'`, not `'~pkg/file'` (`08-turbopack.md`, "Sass node_modules imports"; `version-16.md`).
- A partial that only holds variables and mixins emits no CSS, so `@use`-ing it from many modules duplicates nothing. A partial that contains rules *would* be copied into each module that uses it. Keep rules in the global sheet.

### Turbopack caveats (CSS Modules via Lightning CSS)

From `08-turbopack.md` ("CSS and styling", "Unsupported and unplanned features", "CSS / Sass / SCSS decimal precision"):

- **`:export` / `:import` ICSS is not supported.** `02-guides/sass.md` still shows a "Sass Variables" example built on `:export`, but under Turbopack it doesn't work. In the spike, `:export { accent: … }` gave the warning `'export' is not recognized as a valid pseudo-class` and the value came back undefined, while the build still exited 0. Share design tokens as CSS custom properties instead (`--accent`), set in the global sheet.
- Only the function form `:global(...)` works; standalone `:local`/`:global` doesn't. `@value` isn't supported.
- `composes` from, or `@import` of, a plain `.css` file treats that file as global. Name it `.module.css` if you need module behavior.
- Lightning CSS rounds numbers to 5 decimal places, where webpack used 10.

## next/font: self-hosted mono font as a CSS variable

Sources: `01-getting-started/13-fonts.md` and `03-api-reference/02-components/font.md`.

- Both loaders self-host. `next/font/google` downloads the CSS and font files **at build time** and serves them from our origin, so "No requests are sent to Google by the browser". `next/font/local` takes a file path that is resolved relative to the calling file.
- `JetBrains_Mono` is in `next/font/google`. It's a variable font (weights 100-800, `'variable'` by default) with subsets `latin`, `latin-ext`, `cyrillic`, … (`dist/compiled/@next/font/dist/google/index.d.ts`).
- Expose the font as a variable with the `variable` option, then put `font.variable` on a parent element and use `var(--font-mono)` in CSS ("CSS Variables" section). `display` defaults to `'swap'`. Preloading depends on where the font is used: in the root layout it is preloaded on every route ("Preloading").
- Load each font once, in a definitions module such as `app/fonts.ts`, and import it where needed. Every loader call creates a separate hosted instance ("Using a font definitions file").
- The SWC transform enforces some rules (error strings in the `next-swc` binary). Loader calls must be at module scope and assigned to a `const`, with options written as explicit literals: no spreads, no variables.
- For `next/font/local` with one variable file, set `weight: "100 800"` and a `fallback` list. `adjustFontFallback` defaults to `'Arial'`. For a mono font, prefer `false` plus `fallback: ["ui-monospace", "monospace"]`, because Arial metrics don't match a monospace font.

Sketch (verified in the spike):

```ts
// app/fonts.ts
import { JetBrains_Mono } from "next/font/google";
export const mono = JetBrains_Mono({ subsets: ["latin", "latin-ext"], variable: "--font-mono", display: "swap" });
// or, fully offline:
// import localFont from "next/font/local";
// export const mono = localFont({ src: "./fonts/JetBrainsMono.woff2", variable: "--font-mono", weight: "100 800", fallback: ["ui-monospace", "monospace"], adjustFontFallback: false });
```

```tsx
// app/layout.tsx
<html lang="en" className={mono.variable}>
```

```scss
// any .module.scss
.code { font-family: var(--font-mono), ui-monospace, monospace; }
```

The spike's output had `--font-mono:"JetBrains Mono", "JetBrains Mono Fallback"`, and the `.woff2` files ended up under `.next/static/media/`.

**Google vs local:** the Google loader fails the build if Google Fonts can't be reached (`dist/compiled/@next/font/dist/google/loader.js`: `Failed to fetch … from Google Fonts`, after retries). So `docker build` and CI need outbound network. A committed `.woff2` with `next/font/local` removes that dependency, and JetBrains Mono's OFL license allows redistributing it. Either way, the browser only ever loads the font from our own origin.

## Impact on our tooling

### oxlint (`npm run lint`)

- **One config change, and only if we import a global `.scss`.** `import "./globals.scss"` fails `import/no-unassigned-import`, because `frontend/oxlint.config.mts` allows only `**/*.css`. The spike reproduced the error; adding `"**/*.scss"` to `allow` made lint clean. Module imports (`import styles from …`) are assigned imports and aren't affected.
- Type-check: `next` ships `declare module '*.module.scss'` as `{ readonly [key: string]: string }`, and also declares `*.scss` side-effect modules (`next/types/global.d.ts`). These load through `next-env.d.ts` like the types we already use. The spike's lint was clean, and dropping `.next/` and `next-env.d.ts` only brought back the existing `LayoutProps`/`PageProps` errors, none about `.scss`. Class names aren't typed, so a typo in `styles.boxx` passes lint and renders as `undefined`.
- `next/font` adds no lint issues. `nextjs/google-font-display` and `google-font-preconnect` target `<link>` tags to Google, and `no-page-custom-font` is for the Pages Router. None of them fire.

### Vitest (`npm test`)

- No impact. It runs plain-Node tests of framework-free `lib/` modules, which never import `.scss` or `next/font` (`frontend/vitest.config.mts`). The spike ran 24/24 tests unchanged. If a test ever imports a component that uses a `.module.scss`, Vitest would need `css.modules` setup and `sass` (which would already be installed). A `next/font` call can't run outside Next's compiler and would need mocking. Keep fonts and styles out of `lib/`.

### Docker build (`frontend/Dockerfile`)

- No Dockerfile change. The build stage runs `npm ci` without `NODE_ENV=production`, so dev dependencies, `sass` among them, get installed. Sass compiles during `next build`. The standalone runtime image contains no `sass` (the spike's image had 0 `sass` entries in `node_modules`) and serves CSS and fonts from `.next/static`, which the Dockerfile already copies. The spike's `docker build` on `node:22-alpine` succeeded, with the local font in `.next/static/media/`.
- `node:22` meets `sass`'s `node >= 20.19`. A local font file must be in the build context; `.dockerignore` only excludes `node_modules`, `.next` and `.env*`.
- `next/font/google` needs network during `RUN npm run build`. Use `next/font/local` if image builds must work offline or reproducibly.

## Deprecations to heed

- `sassOptions.functions` is not supported with Turbopack (startup error).
- The `~` prefix for imports from `node_modules` is not supported by Turbopack.
- The `:export` example in the Sass guide doesn't work under Turbopack.
- `@next/font` became `next/font` in v13.2; nothing to install (`font.md`, "Version Changes").
- Sass's own deprecation of `@import` in favor of `@use`/`@forward` isn't covered in Next's docs. The bundled loader runs Sass's modern API, so use `@use` from the start.
