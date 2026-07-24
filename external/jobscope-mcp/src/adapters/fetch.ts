import { NetworkError, NotFoundError, ParseError, RateLimitError } from "../errors";

export type FetchImpl = typeof fetch;

export interface FetchJsonOptions {
  fetchImpl?: FetchImpl;
  headers?: Record<string, string>;
  retryDelayMs?: number;
  timeoutMs?: number;
}

const DEFAULT_TIMEOUT_MS = 15000;
const DEFAULT_USER_AGENT = "jobscope-mcp/0.2.0 (+https://github.com/davo20019/jobscope-mcp)";

async function attempt(
  url: string,
  init: RequestInit,
  fetchImpl: FetchImpl,
  timeoutMs: number
): Promise<Response> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetchImpl(url, { ...init, signal: controller.signal });
  } catch (e) {
    if (e instanceof Error && e.name === "AbortError") {
      throw new NetworkError(`timed out after ${timeoutMs}ms: ${url}`);
    }
    throw new NetworkError(
      e instanceof Error ? `fetch failed: ${e.message}` : "fetch failed"
    );
  } finally {
    clearTimeout(timer);
  }
}

async function executeRequest<T>(
  url: string,
  init: RequestInit,
  options: FetchJsonOptions
): Promise<T> {
  const fetchImpl = options.fetchImpl ?? fetch;
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;

  let response = await attempt(url, init, fetchImpl, timeoutMs);

  if (response.status === 429) {
    const delay = options.retryDelayMs ?? 1000;
    await new Promise((r) => setTimeout(r, delay));
    response = await attempt(url, init, fetchImpl, timeoutMs);
    if (response.status === 429) {
      throw new RateLimitError(`rate limited by ${new URL(url).host}`);
    }
  }

  if (response.status === 404) {
    throw new NotFoundError(`not found: ${url}`);
  }
  if (!response.ok) {
    throw new NetworkError(`${response.status} ${response.statusText} from ${url}`);
  }

  try {
    return (await response.json()) as T;
  } catch (e) {
    throw new ParseError(
      e instanceof Error ? `parse failed: ${e.message}` : "parse failed"
    );
  }
}

export async function fetchJson<T = unknown>(
  url: string,
  options: FetchJsonOptions = {}
): Promise<T> {
  const headers = {
    accept: "application/json",
    "user-agent": DEFAULT_USER_AGENT,
    ...(options.headers ?? {}),
  };
  return executeRequest<T>(url, { method: "GET", headers }, options);
}

export async function postJson<T = unknown>(
  url: string,
  body: unknown,
  options: FetchJsonOptions = {}
): Promise<T> {
  const headers = {
    accept: "application/json",
    "content-type": "application/json",
    "user-agent": DEFAULT_USER_AGENT,
    ...(options.headers ?? {}),
  };
  return executeRequest<T>(url, { method: "POST", headers, body: JSON.stringify(body) }, options);
}
