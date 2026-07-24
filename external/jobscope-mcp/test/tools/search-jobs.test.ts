import { describe, expect, it, vi } from "vitest";
import { searchJobsTool, SEARCH_JOBS_DESCRIPTION } from "../../src/tools/search-jobs";
import * as engine from "../../src/search/engine";

describe("searchJobsTool", () => {
  it("has a description", () => {
    expect(SEARCH_JOBS_DESCRIPTION.length).toBeGreaterThan(50);
  });

  it("uses directory when companies are not supplied", async () => {
    const spy = vi.spyOn(engine, "runSearch").mockResolvedValue({
      jobs: [],
      warnings: [],
      meta: { queried_companies: 3, queried_sources: ["greenhouse"], returned: 0 },
    });
    const result = await searchJobsTool({ remote: "any", limit: 50 });
    expect(spy).toHaveBeenCalled();
    expect(result.meta.queried_companies).toBeGreaterThan(0);
    spy.mockRestore();
  });

  it("passes through ats filter to company selection", async () => {
    const spy = vi.spyOn(engine, "runSearch").mockResolvedValue({
      jobs: [],
      warnings: [],
      meta: { queried_companies: 0, queried_sources: ["ashby"], returned: 0 },
    });
    await searchJobsTool({ remote: "any", limit: 50, ats: ["ashby"] });
    const call = spy.mock.calls[0][0];
    expect(call.companies.every((c) => c.ats === "ashby")).toBe(true);
    spy.mockRestore();
  });
});
