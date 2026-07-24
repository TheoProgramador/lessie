export interface CookieEntry {
  cookieHeader: string;
  csrfToken: string;
  expiresAt: number;
}

const TTL_MS = 25 * 60 * 1000;

interface SetCookieResult {
  cookies: Map<string, string>;
  csrfToken: string | null;
}

function getSetCookieLines(response: Response): string[] {
  const headers = response.headers as Headers & { getSetCookie?: () => string[] };
  if (typeof headers.getSetCookie === "function") {
    return headers.getSetCookie();
  }
  // Fallback: combined header. This split is approximate; it splits on commas
  // that look like a separator between cookie strings (followed by space and
  // a name=value pattern) rather than commas inside Expires dates.
  const combined = headers.get("set-cookie");
  if (!combined) return [];
  return combined.split(/,\s*(?=[A-Za-z0-9!#$%&'*+\-.^_`|~]+=)/);
}

export function parseSetCookieHeaders(response: Response): SetCookieResult {
  const lines = getSetCookieLines(response);
  const cookies = new Map<string, string>();
  for (const line of lines) {
    const semi = line.indexOf(";");
    const pair = (semi >= 0 ? line.slice(0, semi) : line).trim();
    const eq = pair.indexOf("=");
    if (eq < 0) continue;
    const name = pair.slice(0, eq).trim();
    const value = pair.slice(eq + 1).trim();
    if (name) cookies.set(name, value);
  }
  let csrfToken: string | null = null;
  for (const [name, value] of cookies) {
    if (/csrf-token/i.test(name)) {
      csrfToken = value;
      break;
    }
  }
  return { cookies, csrfToken };
}

export function buildCookieHeader(cookies: Map<string, string>): string {
  const pairs: string[] = [];
  for (const [name, value] of cookies) pairs.push(`${name}=${value}`);
  return pairs.join("; ");
}

const cookieCache = new Map<string, CookieEntry>();

export function getCachedCookies(key: string): CookieEntry | null {
  const entry = cookieCache.get(key);
  if (!entry) return null;
  if (Date.now() > entry.expiresAt) {
    cookieCache.delete(key);
    return null;
  }
  return entry;
}

export function setCachedCookies(
  key: string,
  entry: Omit<CookieEntry, "expiresAt">
): CookieEntry {
  const stored: CookieEntry = { ...entry, expiresAt: Date.now() + TTL_MS };
  cookieCache.set(key, stored);
  return stored;
}

export function clearCookieCache(): void {
  cookieCache.clear();
}
