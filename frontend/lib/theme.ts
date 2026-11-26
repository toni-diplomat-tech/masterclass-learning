// The colour theme: System (follow the device), Dark or Light. An explicit choice lives in a cookie the
// browser reads and writes itself; the server never reads it, so every route keeps its static shell.
// System is the absence of the cookie and of <html data-theme>, handled by a CSS media query.

export const THEME_COOKIE = "mq_theme";

export const THEMES = ["system", "dark", "light"] as const;
export type Theme = (typeof THEMES)[number];

const ONE_YEAR_SECONDS = 60 * 60 * 24 * 365;

/** The theme stored in a `document.cookie` string; System when there is none or it is unknown. */
export function parseThemeCookie(cookieHeader: string): Theme {
  for (const part of cookieHeader.split(";")) {
    const [name, ...value] = part.trim().split("=");
    if (name !== THEME_COOKIE) continue;
    const theme = value.join("=");
    return theme === "dark" || theme === "light" ? theme : "system";
  }
  return "system";
}

/** The `document.cookie` assignment that stores a theme (System deletes the cookie). */
export function themeCookie(theme: Theme, secure: boolean): string {
  const attributes = `; path=/; SameSite=Lax${secure ? "; Secure" : ""}`;
  return theme === "system"
    ? `${THEME_COOKIE}=; max-age=0${attributes}`
    : `${THEME_COOKIE}=${theme}; max-age=${ONE_YEAR_SECONDS}${attributes}`;
}

/**
 * The blocking <head> script: applies the stored theme to <html> before first paint, so there is no
 * flash of the wrong theme. Kept tiny and dependency-free; it must not throw.
 */
export const THEME_SCRIPT = `(function(){try{var m=document.cookie.match(/(?:^|; )${THEME_COOKIE}=(dark|light)(?:;|$)/);if(m)document.documentElement.setAttribute("data-theme",m[1])}catch(e){}})()`;
