import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

// Guards the palette in _palette.scss: every text colour reaches WCAG AA (4.5:1) on the surfaces it
// sits on, and UI outlines (inputs, focus ring) reach 3:1.

const source = readFileSync(fileURLToPath(new URL("./_palette.scss", import.meta.url)), "utf8");

function theme(name: "dark" | "light"): Record<string, string> {
  // The dark map comes first, then the light one.
  const [, dark, light] = source.split(/\n {2}(?:dark|light): \(/);
  const block = name === "dark" ? dark : light;
  if (!block) throw new Error(`No ${name} theme in _palette.scss`);
  return Object.fromEntries([...block.matchAll(/([a-z0-9-]+): (#[0-9a-f]{6})/g)].map((m) => [m[1], m[2]]));
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [x, y] = [luminance(a), luminance(b)];
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}

const SURFACES = ["bg", "surface-1", "surface-2", "surface-3"];
const TEXTS = ["text", "text-muted", "primary-text", "correct-text", "wrong-text"];
const FILLS = [
  ["on-primary", "primary"],
  ["on-correct", "correct"],
  ["on-wrong", "wrong"],
];

describe.each(["dark", "light"] as const)("%s theme", (name) => {
  const colours = theme(name);

  it.each(TEXTS.flatMap((text) => SURFACES.map((surface) => [text, surface])))("%s on %s passes AA", (text, surface) => {
    expect(contrast(colours[text], colours[surface])).toBeGreaterThanOrEqual(4.5);
  });

  it.each(FILLS)("%s on %s passes AA", (text, fill) => {
    expect(contrast(colours[text], colours[fill])).toBeGreaterThanOrEqual(4.5);
  });

  it.each(["border-strong", "focus"])("%s stands out from the surfaces (3:1)", (outline) => {
    for (const surface of ["bg", "surface-1"]) expect(contrast(colours[outline], colours[surface])).toBeGreaterThanOrEqual(3);
  });
});
