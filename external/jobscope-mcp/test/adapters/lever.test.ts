import { describe, expect, it, vi } from "vitest";
import fixture from "../fixtures/lever/jobs-plaid.json";
import { leverAdapter } from "../../src/adapters/lever";

const company = {
  name: "Plaid",
  company_slug: "plaid",
  ats: "lever",
  ats_slug: "plaid",
};

describe("leverAdapter", () => {
  it("has name 'lever'", () => {
    expect(leverAdapter.name).toBe("lever");
  });

  it("fetchJobs returns RawJob[]", async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(fixture), { status: 200 }));
    const jobs = await leverAdapter.fetchJobs("plaid", { fetchImpl });
    expect(jobs.length).toBeGreaterThan(0);
  });

  it("normalize produces a valid Job", () => {
    const raw = { source_id: String((fixture as unknown[])[0] && (fixture as { id: string }[])[0].id), raw: (fixture as unknown[])[0] };
    const job = leverAdapter.normalize(raw, company);
    expect(job.id).toBe(`lever:plaid:${raw.source_id}`);
    expect(job.source).toBe("lever");
    expect(job.title).toBeTruthy();
  });

  it("fetchJob returns null on upstream 404", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(new Response("{}", { status: 404 }));
    const result = await leverAdapter.fetchJob("plaid", "123", { fetchImpl });
    expect(result).toBeNull();
  });

  it("maps commitment to employment_type", () => {
    const raw = {
      source_id: "1",
      raw: {
        id: "1",
        text: "Engineer",
        categories: { team: "Eng", location: "Remote", commitment: "Full-time" },
        workplaceType: "remote",
        createdAt: 1713139200000,
        hostedUrl: "https://jobs.lever.co/plaid/1",
        applyUrl: "https://jobs.lever.co/plaid/1/apply",
        descriptionPlain: "hi",
        description: "<p>hi</p>",
        lists: [],
      },
    };
    const job = leverAdapter.normalize(raw, company);
    expect(job.employment_type).toBe("full_time");
    expect(job.remote).toBe("remote");
  });

  describe("leverAdapter — compensation", () => {
    it("toDetail extracts compensation from a 'Compensation' lists entry", () => {
      const raw = {
        source_id: "X",
        raw: {
          id: "X",
          text: "Engineer",
          categories: { team: "Eng", location: "Remote", commitment: "Full-time" },
          workplaceType: "remote",
          createdAt: 1713139200000,
          hostedUrl: "https://jobs.lever.co/x/X",
          applyUrl: "https://jobs.lever.co/x/X/apply",
          descriptionPlain: "hi",
          description: "<p>hi</p>",
          lists: [
            { text: "Compensation", content: "<p>$140,000 - $180,000 USD</p>" },
          ],
        },
      };
      const detail = leverAdapter.toDetail(raw, company);
      expect(detail.compensation).toEqual({
        min: 140000,
        max: 180000,
        currency: "USD",
        interval: "yearly",
      });
    });

    it("toDetail returns null compensation when no salary list entry", () => {
      const raw = {
        source_id: "X",
        raw: {
          id: "X",
          text: "Engineer",
          categories: { team: "Eng", location: "Remote", commitment: "Full-time" },
          workplaceType: "remote",
          createdAt: 1713139200000,
          hostedUrl: "https://jobs.lever.co/x/X",
          applyUrl: "https://jobs.lever.co/x/X/apply",
          descriptionPlain: "hi",
          description: "<p>hi</p>",
          lists: [],
        },
      };
      const detail = leverAdapter.toDetail(raw, company);
      expect(detail.compensation).toBeNull();
    });
  });
});
