import { describe, expect, it } from "vitest";
import {
  loadDirectory,
  getCompany,
  listCompanies,
  directoryGeneratedAt,
} from "../../src/directory/loader";

describe("directory loader", () => {
  it("loads companies", () => {
    const dir = loadDirectory();
    expect(dir.companies.length).toBeGreaterThanOrEqual(5);
    expect(dir.generated_at).toMatch(/^\d{4}-\d{2}-\d{2}T/);
  });

  it("getCompany returns by slug", () => {
    const dir = loadDirectory();
    const any = dir.companies[0];
    expect(getCompany(any.company_slug)?.company_slug).toBe(any.company_slug);
    expect(getCompany("definitely-not-real")).toBeUndefined();
  });

  it("listCompanies filters by ats", () => {
    const gh = listCompanies({ ats: ["greenhouse"] });
    expect(gh.every((c) => c.ats === "greenhouse")).toBe(true);
  });

  it("listCompanies filters by query substring", () => {
    const all = listCompanies({});
    const first = all[0];
    const q = listCompanies({ query: first.name.slice(0, 3) });
    expect(q.length).toBeGreaterThan(0);
  });

  it("directoryGeneratedAt returns ISO string", () => {
    expect(directoryGeneratedAt()).toMatch(/^\d{4}-\d{2}-\d{2}T/);
  });
});
