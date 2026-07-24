import { describe, expect, it } from "vitest";
import { listCompaniesTool } from "../../src/tools/list-companies";

describe("listCompaniesTool", () => {
  it("returns companies and total", async () => {
    const r = await listCompaniesTool({ limit: 100 });
    expect(r.companies.length).toBeGreaterThan(0);
    expect(r.total).toBeGreaterThanOrEqual(r.companies.length);
  });

  it("total reflects match count, not trim count", async () => {
    const r = await listCompaniesTool({ limit: 1 });
    expect(r.companies.length).toBe(1);
    expect(r.total).toBeGreaterThan(1);
  });

  it("filters by ats", async () => {
    const r = await listCompaniesTool({ ats: ["greenhouse"], limit: 100 });
    expect(r.companies.every((c) => c.ats === "greenhouse")).toBe(true);
  });

  it("trims to limit", async () => {
    const r = await listCompaniesTool({ limit: 1 });
    expect(r.companies.length).toBe(1);
  });
});
