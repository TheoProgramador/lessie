import { JobscopeMcp } from "./mcp-server";

export { JobscopeMcp };

const CANONICAL_PATH = "/jobscope-mcp";
const LEGACY_PATH = "/mcp";

interface RateLimitBinding {
  limit: (opts: { key: string }) => Promise<{ success: boolean }>;
}

interface Env {
  RATE_LIMITER?: RateLimitBinding;
  MCP_OBJECT: DurableObjectNamespace;
  USAJOBS_API_KEY?: string;
  USAJOBS_USER_AGENT?: string;
}

export default {
  async fetch(
    request: Request,
    env: Env,
    ctx: ExecutionContext
  ): Promise<Response> {
    const url = new URL(request.url);
    const path = url.pathname;

    // Skip rate limiting for health / index responses (cheap, and used by
    // uptime monitors). Gate everything else on per-IP rate limit.
    const isHealth = path === "/" || path === "/health";
    if (!isHealth && env.RATE_LIMITER) {
      const clientIp = request.headers.get("CF-Connecting-IP") ?? "unknown";
      const { success } = await env.RATE_LIMITER.limit({ key: clientIp });
      if (!success) {
        return new Response(
          "Too many requests. Please slow down and try again in a minute.",
          {
            status: 429,
            headers: {
              "content-type": "text/plain; charset=utf-8",
              "retry-after": "60",
            },
          }
        );
      }
    }

    if (path === CANONICAL_PATH || path.startsWith(`${CANONICAL_PATH}/`)) {
      return JobscopeMcp.serve(CANONICAL_PATH).fetch(request, env as never, ctx);
    }
    if (path === LEGACY_PATH || path.startsWith(`${LEGACY_PATH}/`)) {
      return JobscopeMcp.serve(LEGACY_PATH).fetch(request, env as never, ctx);
    }
    if (path === "/sse" || path.startsWith("/sse/")) {
      return JobscopeMcp.serveSSE("/sse").fetch(request, env as never, ctx);
    }
    if (isHealth) {
      return Response.json({
        name: "jobscope-mcp",
        version: "0.1.0",
        status: "ok",
        mcpEndpoint: CANONICAL_PATH,
        legacyMcpEndpoint: LEGACY_PATH,
        sseEndpoint: "/sse",
        canonicalUrl: `https://mcp.davidloor.com${CANONICAL_PATH}`,
        source: "https://github.com/davo20019/jobscope-mcp",
      });
    }
    return new Response("Not found", { status: 404 });
  },
};
