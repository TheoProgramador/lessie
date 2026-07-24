import { describe, expect, it, beforeEach } from "vitest";
import {
  parseSetCookieHeaders,
  buildCookieHeader,
  getCachedCookies,
  setCachedCookies,
  clearCookieCache,
} from "../../src/adapters/cookie-utils";

describe("parseSetCookieHeaders", () => {
  it("parses a single Set-Cookie", () => {
    const headers = new Headers();
    headers.append("set-cookie", "PLAY_SESSION=abc123; Path=/; HttpOnly");
    const response = new Response("", { headers });
    const { cookies, csrfToken } = parseSetCookieHeaders(response);
    expect(cookies.get("PLAY_SESSION")).toBe("abc123");
    expect(csrfToken).toBeNull();
  });

  it("parses multiple Set-Cookie headers and extracts CSRF token from a *csrf-token* cookie", () => {
    const headers = new Headers();
    headers.append("set-cookie", "PLAY_SESSION=abc; Path=/");
    headers.append("set-cookie", "calypso-csrf-token=xyz789; Path=/");
    headers.append("set-cookie", "wday_vps_cookie=other; Path=/");
    const response = new Response("", { headers });
    const { cookies, csrfToken } = parseSetCookieHeaders(response);
    expect(cookies.get("PLAY_SESSION")).toBe("abc");
    expect(cookies.get("calypso-csrf-token")).toBe("xyz789");
    expect(cookies.get("wday_vps_cookie")).toBe("other");
    expect(csrfToken).toBe("xyz789");
  });

  it("returns empty map when no Set-Cookie headers present", () => {
    const response = new Response("", { headers: new Headers() });
    const { cookies, csrfToken } = parseSetCookieHeaders(response);
    expect(cookies.size).toBe(0);
    expect(csrfToken).toBeNull();
  });

  it("strips attributes after the first semicolon (HttpOnly, Path, etc.)", () => {
    const headers = new Headers();
    headers.append("set-cookie", "name=value; Expires=Wed, 21 Oct 2026 07:28:00 GMT; Path=/; HttpOnly");
    const response = new Response("", { headers });
    const { cookies } = parseSetCookieHeaders(response);
    expect(cookies.get("name")).toBe("value");
    expect(cookies.size).toBe(1);
  });
});

describe("buildCookieHeader", () => {
  it("joins cookies with '; ' separator", () => {
    const map = new Map<string, string>([
      ["a", "1"],
      ["b", "2"],
    ]);
    expect(buildCookieHeader(map)).toBe("a=1; b=2");
  });

  it("returns empty string for empty map", () => {
    expect(buildCookieHeader(new Map())).toBe("");
  });
});

describe("cookie cache", () => {
  beforeEach(() => clearCookieCache());

  it("returns null for missing key", () => {
    expect(getCachedCookies("missing")).toBeNull();
  });

  it("returns set entry within TTL", () => {
    setCachedCookies("k", { cookieHeader: "a=1", csrfToken: "tok" });
    const entry = getCachedCookies("k");
    expect(entry?.cookieHeader).toBe("a=1");
    expect(entry?.csrfToken).toBe("tok");
  });

  it("evicts entry after TTL expires", () => {
    const stored = setCachedCookies("k", { cookieHeader: "a=1", csrfToken: "" });
    stored.expiresAt = Date.now() - 1;
    expect(getCachedCookies("k")).toBeNull();
  });

  it("clearCookieCache empties the cache", () => {
    setCachedCookies("a", { cookieHeader: "x=1", csrfToken: "" });
    setCachedCookies("b", { cookieHeader: "y=2", csrfToken: "" });
    clearCookieCache();
    expect(getCachedCookies("a")).toBeNull();
    expect(getCachedCookies("b")).toBeNull();
  });
});
