import { describe, expect, it, vi } from "vitest";
import { getJobTool } from "../../src/tools/get-job";
import * as registry from "../../src/adapters/registry";
import * as dir from "../../src/directory/loader";

describe("getJobTool", () => {
  it("throws NotFoundError when company is unknown", async () => {
    await expect(
      getJobTool({ source: "greenhouse", company_slug: "nope-xyz", job_id: "1" })
    ).rejects.toThrow(/unknown company/);
  });

  it("throws NotFoundError when adapter is unknown", async () => {
    vi.spyOn(dir, "getCompany").mockReturnValue({
      name: "Stripe",
      company_slug: "stripe",
      ats: "mystery",
      ats_slug: "stripe",
    });
    await expect(
      getJobTool({ source: "greenhouse", company_slug: "stripe", job_id: "1" })
    ).rejects.toThrow(/unknown source/);
    vi.restoreAllMocks();
  });

  it("returns detail when adapter returns raw", async () => {
    vi.spyOn(dir, "getCompany").mockReturnValue({
      name: "Stripe",
      company_slug: "stripe",
      ats: "greenhouse",
      ats_slug: "stripe",
    });
    vi.spyOn(registry, "getAdapter").mockReturnValue({
      name: "greenhouse",
      fetchJobs: async () => [],
      fetchJob: async () => ({ source_id: "1", raw: { id: 1 } }),
      normalize: (_r, c) => ({
        id: `greenhouse:${c.company_slug}:1`,
        source: "greenhouse",
        company: c.name,
        company_slug: c.company_slug,
        title: "Engineer",
        locations: [],
        remote: "remote",
        employment_type: "full_time",
        department: null,
        posted_at: null,
        url: "https://x",
        description_preview: "hi",
      }),
      toDetail: (_r, c) => ({
        id: `greenhouse:${c.company_slug}:1`,
        source: "greenhouse",
        company: c.name,
        company_slug: c.company_slug,
        title: "Engineer",
        locations: [],
        remote: "remote",
        employment_type: "full_time",
        department: null,
        posted_at: null,
        url: "https://x",
        description_preview: "hi",
        description: "full text",
        description_html: "<p>full</p>",
        team: null,
        compensation: null,
        raw: { id: 1 },
      }),
    });
    const result = await getJobTool({ source: "greenhouse", company_slug: "stripe", job_id: "1" });
    expect(result.id).toBe("greenhouse:stripe:1");
    expect(result.description).toBe("full text");
    vi.restoreAllMocks();
  });
});
