import { describe, expect, it } from "vitest";
import {
  JobSchema,
  JobDetailSchema,
  CompanyRefSchema,
  SearchJobsInputSchema,
  GetJobInputSchema,
  ListCompaniesInputSchema,
} from "../src/schemas";

const baseJob = {
  id: "greenhouse:stripe:123",
  source: "greenhouse",
  company: "Stripe",
  company_slug: "stripe",
  title: "Senior Backend Engineer",
  locations: ["Remote - US", "San Francisco"],
  remote: "remote" as const,
  employment_type: "full_time" as const,
  department: "Engineering",
  posted_at: "2026-04-20T00:00:00Z",
  url: "https://boards.greenhouse.io/stripe/jobs/123",
  description_preview: "We are looking for ...",
};

describe("schemas", () => {
  it("accepts a well-formed Job", () => {
    expect(() => JobSchema.parse(baseJob)).not.toThrow();
  });

  it("rejects Job with invalid remote value", () => {
    expect(() => JobSchema.parse({ ...baseJob, remote: "partial" })).toThrow();
  });

  it("accepts JobDetail extending Job", () => {
    const detail = {
      ...baseJob,
      description: "full text",
      description_html: "<p>full</p>",
      team: "Payments",
      compensation: { min: 200000, max: 260000, currency: "USD", interval: "yearly" as const },
      raw: { anything: true },
    };
    expect(() => JobDetailSchema.parse(detail)).not.toThrow();
  });

  it("accepts CompanyRef", () => {
    expect(() =>
      CompanyRefSchema.parse({
        name: "Stripe",
        company_slug: "stripe",
        ats: "greenhouse",
        ats_slug: "stripe",
        tags: ["payments"],
      })
    ).not.toThrow();
  });

  it("applies defaults on SearchJobsInput including posted_since '30d'", () => {
    const parsed = SearchJobsInputSchema.parse({});
    expect(parsed.remote).toBe("any");
    expect(parsed.limit).toBe(50);
    expect(parsed.posted_since).toBe("30d");
  });

  it("accepts posted_since as ISO date string", () => {
    const parsed = SearchJobsInputSchema.parse({ posted_since: "2026-01-01" });
    expect(parsed.posted_since).toBe("2026-01-01");
  });

  it("accepts posted_since as relative shorthand", () => {
    const parsed = SearchJobsInputSchema.parse({ posted_since: "7d" });
    expect(parsed.posted_since).toBe("7d");
  });

  it("accepts posted_since as null to disable filtering", () => {
    const parsed = SearchJobsInputSchema.parse({ posted_since: null });
    expect(parsed.posted_since).toBeNull();
  });

  it("rejects SearchJobsInput with limit over 200", () => {
    expect(() => SearchJobsInputSchema.parse({ limit: 500 })).toThrow();
  });

  it("requires source, company_slug, job_id on GetJobInput", () => {
    expect(() => GetJobInputSchema.parse({ source: "greenhouse" })).toThrow();
  });

  it("applies defaults on ListCompaniesInput", () => {
    expect(ListCompaniesInputSchema.parse({}).limit).toBe(100);
  });
});
