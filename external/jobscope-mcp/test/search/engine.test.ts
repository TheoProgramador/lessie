import { describe, expect, it } from "vitest";
import { runSearch } from "../../src/search/engine";
import type { AtsAdapter } from "../../src/adapters/types";
import type { CompanyRef, Job } from "../../src/schemas";

function fakeAdapter(name: string, jobs: Job[]): AtsAdapter {
  return {
    name,
    fetchJobs: async () => jobs.map((j) => ({ source_id: j.id, raw: j })),
    fetchJob: async () => null,
    normalize: (raw) => raw.raw as Job,
    toDetail: (raw) => raw.raw as any,
  };
}

const company: CompanyRef = {
  name: "Stripe",
  company_slug: "stripe",
  ats: "greenhouse",
  ats_slug: "stripe",
};

function mkJob(id: string, title: string): Job {
  return {
    id,
    source: "greenhouse",
    company: "Stripe",
    company_slug: "stripe",
    title,
    locations: [],
    remote: "remote",
    employment_type: "full_time",
    department: null,
    posted_at: null,
    url: "https://x",
    description_preview: "",
  };
}

describe("runSearch", () => {
  it("fans out across adapters and returns filtered, ranked, trimmed results", async () => {
    const gh = fakeAdapter("greenhouse", [mkJob("1", "Backend Engineer"), mkJob("2", "Frontend Engineer")]);
    const result = await runSearch({
      input: { query: "backend", remote: "any", limit: 10, posted_since: null },
      companies: [company],
      adapters: [gh],
    });
    expect(result.jobs[0].title).toContain("Backend");
    expect(result.warnings).toEqual([]);
    expect(result.meta.queried_companies).toBe(1);
    expect(result.meta.queried_sources).toEqual(["greenhouse"]);
  });

  it("captures per-company failures as warnings and returns partial results", async () => {
    const gh: AtsAdapter = {
      name: "greenhouse",
      fetchJobs: async () => {
        throw new Error("boom");
      },
      fetchJob: async () => null,
      normalize: (r) => r.raw as Job,
      toDetail: (r) => r.raw as any,
    };
    const result = await runSearch({
      input: { remote: "any", limit: 10, posted_since: null },
      companies: [company],
      adapters: [gh],
    });
    expect(result.jobs).toEqual([]);
    expect(result.warnings.length).toBe(1);
    expect(result.warnings[0]).toContain("stripe");
  });
});
