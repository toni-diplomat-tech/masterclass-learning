# Frontend design system

The look of every screen except `/health`: Apple calm (system font, whitespace, soft corners, frosted glass) and Spotify punch (dark-first, heavy headings, big pill buttons, raised cards), in Mr. Robot colours. The stack decision is [ADR 0004](adr/0004-scss-tokens-and-hand-rolled-components.md); this page is the spec. The code in `frontend/styles/` and `frontend/components/` is the source of truth for exact values.

## Palette

Semantic tokens, emitted as `--color-<name>` (`styles/_palette.scss`). `styles/palette.test.ts` checks AA contrast for every text/surface pair.

| Token | Dark | Light | Use |
|---|---|---|---|
| `bg` | `#0b0b0d` | `#f3f2ee` | page |
| `surface-1/2/3` | `#151518` / `#1f1f23` / `#2a2a2f` | `#ffffff` / `#e9e8e3` / `#dddcd6` | cards, chips and hovers, pressed |
| `text` / `text-muted` | `#f2f1ec` / `#a3a3a8` | `#121214` / `#5a5a61` | copy |
| `border` / `border-strong` | `#34343a` / `#6b6b73` | `#cfcec8` / `#85848c` | dividers / inputs (3:1) |
| `primary` / `on-primary` / `primary-text` | `#e0161e` / white / `#ff5c61` | `#c8101a` / white / `#b00e17` | main action, selection, links, rank 1 |
| `correct` / `on-correct` / `correct-text` | `#33e070` / `#04140a` / `#4ee887` | `#0e7a3c` / white / `#0b6a33` | Correct, Perfect score, success |
| `wrong` / `on-wrong` / `wrong-text` | `#4a4a52` / `#f2f1ec` / `#9a9aa2` | `#6c6c74` / white / `#5a5a61` | Incorrect and Timed out: muted grey, never red |
| `focus` | `#ff5c61` | `#c8101a` | keyboard focus ring |
| `glass`, `scrim`, `qr-bg` | | | frosted surfaces, overlays, the QR code's white backing |

## Tokens and layout (`styles/`)

- `_tokens.scss`: 4px spacing grid (`$space-1`…`$space-8`), radii (`sm` 8px, `md` 14px, `lg` 20px, `pill`), rem type scale, fluid `projector-size-*` sizes (`clamp()` on the viewport width), breakpoints `md` 40rem and `lg` 64rem, widths (phone 28rem, text 42rem, page 64rem, projector 96rem), 48px tap target, motion durations and easing.
- `_mixins.scss`: `up($bp)`, `focus-ring`, `frosted`, `surface($level)`, `mono`, `motion` (only without reduced motion), `visually-hidden`.
- `_design.scss` forwards both; modules start with `@use "design" as *;`. `_palette.scss` is only used by `app/globals.scss`.
- `app/globals.scss`: theme emission (light on `:root`, dark under the System media query and `[data-theme="dark"]`), reset, base typography, native inputs, selects and tables, and a global reduced-motion kill switch.

## Type

The system sans stack for text. Headings are black weight with tight tracking. JetBrains Mono (`next/font`, `--font-mono`) with tabular figures for Join codes, countdowns, scores, ranks and the wordmark.

## Primitives (`components/`)

- **Button**: pill, `primary` (red fill) / `secondary` (surface) / `danger` (red outline, filled on hover) / `ghost`; `md` / `lg`; `block`; `loading` shows a spinner and disables it. `buttonClass()` styles a link as a button.
- **Card**: raised surface with a shadow; `glass` gives the frosted sheet (the start-Session dialog).
- **Notice**: status or alert text with a coloured left edge (`info` / `success` / `error`).
- **ProblemsNotice**: an `error` Notice listing why an action was refused, one problem per line; nothing when there are none.
- **OptionTile / OptionTiles**: a letter badge plus the option text; `idle` / `selected` (red edge and badge) / `correct` (green edge, ✓ badge, small pop) / `wrong` (muted). One column on the phone, two on the projector.
- **Leaderboard**: rows with a mono rank and points; rank 1 has a red edge, ranks 2 and 3 a raised surface; `highlight` outlines one Participant in green; rows rise in one after another.
- **Page / Stack / Chips / Eyebrow**: the `<main>` width per screen, vertical rhythm, the meta chips (Workshop · Question n of m · Difficulty) and small uppercase labels.
- **AppHeader / ThemeToggle**: a sticky frosted bar on every screen, with the `>masterclass.quiz▌` wordmark (red blinking cursor) and the System / Dark / Light segmented control (icons only on phones, labels from `md` up).

## Screens

- **Participant phone** (`phone` width, one column, thumb-first): the join screen has a big mono Join code field in a card and a full-width Join button. On play, the Nickname is the hero; waiting shows breathing red dots. A live Question shows its chips, then the countdown ring (red and pulsing for the last 5 s), then the Question, then full-width tiles; "Answer locked" sits under them. On reveal, a result banner (green Correct, grey Incorrect or Timed out) sits above the tiles with counts. The Session score is a card with mono points and the rank. The Recap link is a full-width button.
- **Recap** (`text` width): a card per Workshop; Perfect score is a green pill; each missed Question is a sunken block with "Your Answer" in grey and "Correct" in green.
- **Host projector** (`projector` width, fluid sizes): the Join invitation puts the address and a giant mono Join code beside the QR code on a white card (stacked on narrow screens), with a large Participant count. The start-Session dialog is a frosted sheet. Close Masterclass is a danger button at the bottom. A live Question uses two columns of tiles, a big countdown and a big mono answer count. On reveal, the correct tile turns green and the others go muted, then the leaderboard follows. The projector follows the Host's theme toggle (no forced theme); Dark is recommended on a projector.
- **Back office, login, results** (`wide` width): cards for the forms (fields side by side from `md`), data tables that scroll sideways on phones, a status pill (green when Open) and mono Join codes.

## Motion

Short and subtle (120 to 360 ms, one easing): button and tile press scale, correct-tile pop on reveal, result banner pop, the countdown pulse in the last 5 s, leaderboard rows rising in sequence, waiting dots, the cursor blink. All of it sits behind `prefers-reduced-motion: no-preference`, and a global rule zeroes durations under `reduce`. Leaderboard reordering is not animated (rows re-render on each update).
