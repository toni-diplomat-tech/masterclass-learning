# Theme cookie without a flash (Next.js 16.4)

Research for issue #22. Question: how does the root layout set `data-theme` on `<html>` from a
System / Dark / Light theme cookie with no flash of the wrong theme, without giving up static
rendering, and how is the toggle written back?

Primary source: the docs shipped with the installed Next.js 16.4.6, under
`frontend/node_modules/next/dist/docs/` (abbreviated `docs/` below). Next.js has a dedicated guide
for exactly this case: `docs/01-app/02-guides/preventing-flash-before-hydration.md`.

## Recommendation

**Do not read `cookies()` in the root layout.** Keep the layout static and put a small blocking
inline `<script>` in `<head>` that reads the theme cookie from `document.cookie` and sets
`data-theme` on `<html>` before first paint. Treat **System as the absence of the attribute** and
handle it in pure CSS with `prefers-color-scheme`. Write the toggle back **on the client with
`document.cookie`**, not with a Server Action.

## Findings

### 1. Reading `cookies()` in the root layout breaks the static shell

- This app runs with Cache Components on (`cacheComponents: true` in `frontend/next.config.ts`).
  With Cache Components, Partial Prerendering is the only rendering model for App Router pages
  (`docs/01-app/02-guides/building.md`, "With Cache Components…").
- `cookies()` is a Request-time API. With Cache Components, calling it inside `<Suspense>` streams
  only that subtree; "calling it outside a boundary prevents the route from being prerendered"
  (`docs/01-app/03-api-reference/04-functions/cookies.md`, "Good to know").
- `next build` validates that every page can produce a static shell; `cookies()` outside
  `<Suspense>` produces the `blocking-prerender-runtime` error (`building.md`, line ~84;
  `docs/01-app/02-guides/migrating-to-cache-components.md`, "`cookies`, `headers`, and
  `searchParams`").
- A cookie that drives an attribute on `<html>` cannot be wrapped: "reading it on the server makes
  the whole subtree request-bound, so there's no child to wrap in `<Suspense>`. An inline
  `<script>` in `<head>` that sets the attribute before paint keeps the shell static"
  (`migrating-to-cache-components.md`, Good to know after the cookies section).
- The flash guide says the same: reading the cookie in the root layout "opts the entire app out of
  static prerendering (and under Cache Components, forces blocking every segment under the
  layout)" (`preventing-flash-before-hydration.md`, "Storing the theme in a cookie").
- `generateViewport` follows the same rules, so a `viewport.colorScheme` computed from the cookie
  would also defer to request time (`docs/01-app/03-api-reference/04-functions/generate-viewport.md`,
  "With Cache Components").

**Which routes are static today.** Inferred from source (a `next build` was not run for this
note): every page (`/`, `/health`, `/host`, `/host/login`, `/host/masterclass/[id]/live`,
`/host/masterclass/[id]/results`, `/play`, `/recap/[token]`) reads request data (`cookies()`,
`headers()`, `params`, backend fetches) only inside a `<Suspense>` child, and the root layout reads
nothing. So each page should be `○` or `◐` (static shell plus streamed holes). Reading the theme
cookie in `frontend/app/layout.tsx` would take away the static shell from **every** route at once,
including the Participant join page and the live screens, whose fallbacks currently show instantly.

### 2. The no-flash pattern: blocking inline script in `<head>`

From `preventing-flash-before-hydration.md` ("Themes" and "Storing the theme in a cookie"):

- A `<script dangerouslySetInnerHTML>` in `<head>` runs synchronously while the browser parses the
  HTML, so the attribute is set before any content is painted.
- `<html>` needs `suppressHydrationWarning`, because the script changes the DOM before React
  hydrates; with it, "React keeps whatever is in the DOM … The DOM wins" ("Understanding
  `suppressHydrationWarning`").
- The attribute on `<html>` persists across client-side navigations, so the script only needs to
  run on the initial document (Themes, Good to know).
- `useEffect` would flash (runs after paint); `useLayoutEffect` still flashes between HTML arrival
  and hydration on slow connections ("Why not `useEffect`?").
- **Dev only:** Strict Mode's remount resets `<html>` attributes to those React manages, clearing
  the one the script set. The guide's fix is a `useLayoutEffect` in the theme toggle component that
  re-reads the stored value and re-applies it; it is a no-op in production ("Re-applying attributes
  in development").
- React warns in development about rendering `<script>` tags; the guide offers either the
  `InlineScript` helper (`type` `text/javascript` on server, `text/plain` on client, with
  `suppressHydrationWarning`) or returning the script from `useServerInsertedHTML` with a `useRef`
  once-flag, which "suits scripts that act on the document … such as the theme script".
- **CSP:** inline scripts are blocked by a strict Content Security Policy without
  `'unsafe-inline'`; a nonce would be needed (guide, Good to know under "Extracting a reusable
  component"). The repo currently sets no CSP (no `Content-Security-Policy` or nonce in
  `frontend/`), so this does not bite today, but a nonce-based CSP would itself force dynamic
  rendering, so keep this in mind before adding one.

### 3. System via `color-scheme` + `prefers-color-scheme`

The guide's sample defaults `<html data-theme="light">`. For a three-way choice, a better fit is:

- Static HTML ships **no** `data-theme` (= System). `frontend/app/globals.css` already implements
  System this way: dark variables and `color-scheme: dark` under
  `@media (prefers-color-scheme: dark)`.
- The script sets `data-theme="dark"` or `"light"` only when the cookie holds an explicit choice.
- CSS layers the override on top, for example:

  ```css
  :root { --background: #fff; --foreground: #171717; color-scheme: light; }
  @media (prefers-color-scheme: dark) {
    :root:not([data-theme="light"]) { --background: #0a0a0a; --foreground: #ededed; color-scheme: dark; }
  }
  :root[data-theme="dark"] { --background: #0a0a0a; --foreground: #ededed; color-scheme: dark; }
  ```

Consequences: System never depends on JavaScript, so it cannot flash and also follows a live OS
switch; if the script fails (blocked, cookie unreadable), the page falls back to System, not to a
wrong fixed theme. `color-scheme` keeps native controls and scrollbars in step. A static
`viewport.colorScheme: 'light dark'` (`generate-viewport.md`, `colorScheme`) is allowed because it
reads no request data, but it is redundant with the CSS property.

### 4. Writing the toggle back

Two options documented by Next.js:

| | Client `document.cookie` | Server Action `cookies().set` |
|---|---|---|
| Source | flash guide, end of "Storing the theme in a cookie" | `cookies.md` "Setting a cookie"; `docs/01-app/01-getting-started/07-mutating-data.md` "Cookies" |
| Round trip | none; instant | one POST per toggle |
| Side effects | none | Next "re-renders the current page and its layouts on the server so the UI reflects the new cookie value" (`07-mutating-data.md`) — pointless work here, and on live screens it re-runs server data fetches |
| `HttpOnly` possible | no | yes, but the head script must read the cookie, so it must not be `HttpOnly` anyway |

The guide's client write is:

```js
document.documentElement.setAttribute('data-theme', theme)
document.cookie = `theme=${encodeURIComponent(theme)}; path=/; max-age=31536000; SameSite=Lax`
```

For System, delete the attribute and expire the cookie (`max-age=0`). Add `; Secure` when
`location.protocol === 'https:'`, mirroring how `frontend/app/actions.ts` and
`frontend/app/host/actions.ts` set `secure` from `x-forwarded-proto` for the session cookies.

### 5. The existing cookie helpers

- `frontend/lib/set-cookie.ts` (`readSetCookie`) parses a **backend** response's `Set-Cookie`
  headers so a Server Action can relay the Participant / Host session cookie. It is not involved
  in a purely client-written theme cookie.
- `frontend/proxy.ts` only matches `/host` paths and redirects without a Host cookie. It could
  read the theme cookie, but it cannot change the prerendered HTML, so it is no help against the
  flash. Leave it alone.
- Neither helper needs to change for this feature. The theme cookie name should be a constant
  shared by the head script and the toggle (alongside the style of `lib/host-cookie.ts`).

## Trade-offs of the recommendation

- Pros: every route keeps its static shell; no flash on hard loads; System is pure CSS and robust;
  toggle is instant with no server round trip; no backend, Quiz engine or procedure involvement.
- Cons: one inline script (needs a nonce if a strict CSP is ever added); `suppressHydrationWarning`
  on `<html>` also hides any other genuine mismatch on that one element's attributes; a dev-only
  `useLayoutEffect` re-apply for Strict Mode; the server never knows the theme, so nothing
  server-rendered can branch on it (none needs to).
- Rejected: `cookies()` in the root layout (kills the static shell app-wide, and under Cache
  Components fails the build without a Suspense boundary, which `<html>` cannot have); `useEffect`
  or `useLayoutEffect` alone (flash); `localStorage` instead of a cookie (works the same with the
  script, but the issue asks for a cookie and a cookie leaves the door open to server reads later).
