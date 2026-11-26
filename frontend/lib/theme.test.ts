import { runInNewContext } from "node:vm";
import { describe, expect, it } from "vitest";
import { parseThemeCookie, THEME_SCRIPT, themeCookie } from "./theme";

describe("theme cookie", () => {
  it("is System without the cookie", () => {
    expect(parseThemeCookie("")).toBe("system");
    expect(parseThemeCookie("mq_host=abc; other=1")).toBe("system");
  });

  it("reads an explicit Dark or Light choice among other cookies", () => {
    expect(parseThemeCookie("mq_host=abc; mq_theme=dark")).toBe("dark");
    expect(parseThemeCookie("mq_theme=light; mq_host=abc")).toBe("light");
  });

  it("treats an unknown value as System", () => {
    expect(parseThemeCookie("mq_theme=neon")).toBe("system");
  });

  it("round-trips a stored choice", () => {
    const stored = themeCookie("dark", false).split(";")[0];
    expect(parseThemeCookie(stored)).toBe("dark");
  });

  it("deletes the cookie for System and marks it Secure on https", () => {
    expect(themeCookie("system", false)).toMatch(/^mq_theme=; max-age=0;/);
    expect(themeCookie("light", true)).toContain("; Secure");
    expect(themeCookie("light", false)).not.toContain("Secure");
  });
});

/** Runs the <head> script against a fake document; returns the data-theme it set, if any. */
function run(cookie: string): string | null {
  let applied: string | null = null;
  const document = { cookie, documentElement: { setAttribute: (_: string, value: string) => (applied = value) } };
  runInNewContext(THEME_SCRIPT, { document });
  return applied;
}

describe("theme <head> script", () => {
  it("applies an explicit choice to <html> and leaves System alone", () => {
    expect(run("a=1; mq_theme=dark")).toBe("dark");
    expect(run("mq_theme=light")).toBe("light");
    expect(run("a=1")).toBeNull();
    expect(run("mq_theme=neon")).toBeNull();
    expect(run("xmq_theme=dark")).toBeNull();
  });
});
