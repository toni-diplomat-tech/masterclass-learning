/**
 * Finds a cookie in a backend response's Set-Cookie headers, to relay it to the browser.
 * The expiry comes from Max-Age when present, else from Expires.
 */
export function readSetCookie(setCookies: string[], name: string): { value: string; expires?: Date } | null {
  for (const setCookie of setCookies) {
    const [pair, ...attributes] = setCookie.split(";");
    const separator = pair.indexOf("=");
    if (pair.slice(0, separator).trim() !== name) continue;

    const attribute = (key: string) =>
      attributes
        .map((a) => a.trim())
        .find((a) => a.toLowerCase().startsWith(`${key}=`))
        ?.slice(key.length + 1);
    const maxAge = attribute("max-age");
    const expiresText = attribute("expires");
    const expires =
      maxAge !== undefined ? new Date(Date.now() + Number(maxAge) * 1000) : expiresText ? new Date(expiresText) : undefined;
    return {
      value: pair.slice(separator + 1).trim(),
      expires: expires && !Number.isNaN(expires.getTime()) ? expires : undefined,
    };
  }
  return null;
}
