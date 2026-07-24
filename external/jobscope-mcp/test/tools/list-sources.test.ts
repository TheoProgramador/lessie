import { describe, expect, it } from "vitest";
import { listSourcesTool } from "../../src/tools/list-sources";

describe("listSourcesTool", () => {
  it("returns the five v2 sources with company counts", async () => {
    const r = await listSourcesTool({});
    const names = r.sources.map((s) => s.name).sort();
    expect(names).toEqual(["ashby", "greenhouse", "lever", "usajobs", "workday"]);
    for (const s of r.sources) {
      expect(s.company_count).toBeGreaterThanOrEqual(0);
      expect(s.last_refreshed_at).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    }
  });
});
