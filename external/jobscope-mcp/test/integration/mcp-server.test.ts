import { describe, expect, it, vi } from "vitest";

// McpAgent extends Cloudflare Durable Objects, which are unavailable in Node.
// Stub the module so JobscopeMcp can be instantiated in a plain Node test env.
vi.mock("agents/mcp", () => {
  return {
    McpAgent: class McpAgent {
      // subclasses declare `server` and `init()` — no base behaviour needed for tests
    },
  };
});

import * as sjt from "../../src/tools/search-jobs";
import { JobscopeMcp } from "../../src/mcp-server";

describe("JobscopeMcp", () => {
  it("registers four tools", async () => {
    const mcp = new (JobscopeMcp as unknown as new () => {
      init(): Promise<void>;
      server: { _registeredTools?: Record<string, unknown> };
    })();
    await mcp.init();

    const tools = (mcp as unknown as { server: { _registeredTools?: Record<string, unknown> } })
      .server._registeredTools;
    expect(tools && Object.keys(tools).sort()).toEqual([
      "get_job",
      "list_companies",
      "list_sources",
      "search_jobs",
    ]);
  });

  it("search_jobs handler serializes result to JSON text content", async () => {
    vi.spyOn(sjt, "searchJobsTool").mockResolvedValue({
      jobs: [],
      warnings: [],
      meta: { queried_companies: 0, queried_sources: [], returned: 0 },
    });
    const mcp = new (JobscopeMcp as unknown as new () => {
      init(): Promise<void>;
      server: {
        _registeredTools?: Record<
          string,
          { handler: (input: unknown, extra: unknown) => Promise<{ content: Array<{ text: string }> }> }
        >;
      };
    })();
    await mcp.init();
    // SDK stores the registered callback as `handler` (not `callback`) on the tool object
    const handler = mcp.server._registeredTools!["search_jobs"].handler;
    const result = await handler({ remote: "any", limit: 10 }, {});
    expect(JSON.parse(result.content[0].text)).toEqual({
      jobs: [],
      warnings: [],
      meta: { queried_companies: 0, queried_sources: [], returned: 0 },
    });
    vi.restoreAllMocks();
  });
});
