import { describe, expect, it, vi } from "vitest";
import { fetchJson, postJson } from "../../src/adapters/fetch";
import { NetworkError, NotFoundError, RateLimitError } from "../../src/errors";

function mockResponse(status: number, body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json" },
    ...init,
  });
}

describe("fetchJson", () => {
  it("returns parsed JSON on 200", async () => {
    const stub = vi.fn().mockResolvedValue(mockResponse(200, { ok: true }));
    const result = await fetchJson("https://example.com/x", { fetchImpl: stub });
    expect(result).toEqual({ ok: true });
  });

  it("throws NotFoundError on 404", async () => {
    const stub = vi.fn().mockResolvedValue(mockResponse(404, { err: true }));
    await expect(fetchJson("https://example.com/x", { fetchImpl: stub })).rejects.toBeInstanceOf(
      NotFoundError
    );
  });

  it("retries once on 429 and throws RateLimitError if still limited", async () => {
    const stub = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(429, {}))
      .mockResolvedValueOnce(mockResponse(429, {}));
    await expect(
      fetchJson("https://example.com/x", { fetchImpl: stub, retryDelayMs: 1 })
    ).rejects.toBeInstanceOf(RateLimitError);
    expect(stub).toHaveBeenCalledTimes(2);
  });

  it("retries once on 429 and returns data if the retry succeeds", async () => {
    const stub = vi
      .fn()
      .mockResolvedValueOnce(mockResponse(429, {}))
      .mockResolvedValueOnce(mockResponse(200, { ok: true }));
    const result = await fetchJson("https://example.com/x", { fetchImpl: stub, retryDelayMs: 1 });
    expect(result).toEqual({ ok: true });
  });

  it("wraps network failures in NetworkError", async () => {
    const stub = vi.fn().mockRejectedValue(new TypeError("fetch failed"));
    await expect(fetchJson("https://example.com/x", { fetchImpl: stub })).rejects.toBeInstanceOf(
      NetworkError
    );
  });

  it("wraps slow responses in NetworkError when timeout elapses", async () => {
    const stub = vi.fn().mockImplementation((_url: string, init?: RequestInit) => {
      return new Promise<Response>((_resolve, reject) => {
        init?.signal?.addEventListener("abort", () =>
          reject(new DOMException("aborted", "AbortError"))
        );
        // never resolves on its own
      });
    });
    await expect(
      fetchJson("https://example.com/x", { fetchImpl: stub, timeoutMs: 10 })
    ).rejects.toBeInstanceOf(NetworkError);
  });
});

describe("postJson", () => {
  it("sends a POST with JSON body and returns parsed response", async () => {
    const stub = vi.fn().mockImplementation((url: string, init: RequestInit) => {
      expect(init.method).toBe("POST");
      expect(init.body).toBe(JSON.stringify({ q: "x" }));
      const headers = init.headers as Record<string, string>;
      expect(headers["content-type"]).toBe("application/json");
      return Promise.resolve(new Response(JSON.stringify({ ok: true }), { status: 200 }));
    });
    const result = await postJson("https://example.com/search", { q: "x" }, { fetchImpl: stub });
    expect(result).toEqual({ ok: true });
  });

  it("respects custom headers on POST", async () => {
    const stub = vi.fn().mockImplementation((_url: string, init: RequestInit) => {
      const headers = init.headers as Record<string, string>;
      expect(headers["x-custom"]).toBe("yes");
      return Promise.resolve(new Response("{}", { status: 200 }));
    });
    await postJson("https://example.com/x", {}, { fetchImpl: stub, headers: { "x-custom": "yes" } });
    expect(stub).toHaveBeenCalledOnce();
  });

  it("retries POST once on 429 like fetchJson does", async () => {
    const mockResp = (status: number, body: unknown) =>
      new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
    const stub = vi
      .fn()
      .mockResolvedValueOnce(mockResp(429, {}))
      .mockResolvedValueOnce(mockResp(200, { ok: true }));
    const result = await postJson("https://example.com/x", {}, { fetchImpl: stub, retryDelayMs: 1 });
    expect(result).toEqual({ ok: true });
    expect(stub).toHaveBeenCalledTimes(2);
  });
});
