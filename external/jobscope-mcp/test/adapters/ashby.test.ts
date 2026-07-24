import { describe, expect, it, vi } from "vitest";
import fixture from "../fixtures/ashby/jobs-linear.json";
import { ashbyAdapter } from "../../src/adapters/ashby";

const company = {
  name: "Linear",
  company_slug: "linear",
  ats: "ashby",
  ats_slug: "linear",
};

describe("ashbyAdapter", () => {
  it("has name 'ashby'", () => {
    expect(ashbyAdapter.name).toBe("ashby");
  });

  it("fetchJobs returns RawJob[]", async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(fixture), { status: 200 }));
    const jobs = await ashbyAdapter.fetchJobs("linear", { fetchImpl });
    expect(jobs.length).toBeGreaterThan(0);
  });

  it("normalize produces a valid Job and honors isRemote", () => {
    const raw = {
      source_id: "abc",
      raw: {
        id: "abc",
        title: "Engineer",
        department: "Engineering",
        team: "Core",
        employmentType: "FullTime",
        locationName: "Remote",
        isRemote: true,
        publishedAt: "2026-04-20T00:00:00Z",
        jobUrl: "https://jobs.ashbyhq.com/linear/abc",
        descriptionHtml: "<p>hi</p>",
        descriptionPlain: "hi",
      },
    };
    const job = ashbyAdapter.normalize(raw, company);
    expect(job.id).toBe("ashby:linear:abc");
    expect(job.remote).toBe("remote");
    expect(job.employment_type).toBe("full_time");
    expect(job.department).toBe("Engineering");
  });

  describe("ashbyAdapter — compensation", () => {
    it("toDetail extracts compensation from compensationTierSummary", () => {
      const raw = {
        source_id: "abc",
        raw: {
          id: "abc",
          title: "Engineer",
          department: "Eng",
          team: "Core",
          employmentType: "FullTime",
          locationName: "Remote",
          isRemote: true,
          publishedAt: "2026-04-20T00:00:00Z",
          jobUrl: "https://jobs.ashbyhq.com/x/abc",
          descriptionHtml: "<p>hi</p>",
          descriptionPlain: "hi",
          compensationTierSummary: "$160,000 - $210,000",
        },
      };
      const detail = ashbyAdapter.toDetail(raw, company);
      expect(detail.compensation).toEqual({
        min: 160000,
        max: 210000,
        currency: "USD",
        interval: "yearly",
      });
    });

    it("toDetail returns null compensation when summary is absent", () => {
      const raw = {
        source_id: "abc",
        raw: {
          id: "abc",
          title: "Engineer",
          department: "Eng",
          team: "Core",
          employmentType: "FullTime",
          locationName: "Remote",
          isRemote: true,
          publishedAt: "2026-04-20T00:00:00Z",
          jobUrl: "https://jobs.ashbyhq.com/x/abc",
          descriptionHtml: "<p>hi</p>",
          descriptionPlain: "hi",
        },
      };
      const detail = ashbyAdapter.toDetail(raw, company);
      expect(detail.compensation).toBeNull();
    });
  });
});
