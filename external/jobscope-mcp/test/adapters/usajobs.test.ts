import { describe, expect, it, vi } from "vitest";
import fixture from "../fixtures/usajobs/search-sample.json";
import { usajobsAdapter, configureUsajobs } from "../../src/adapters/usajobs";

const company = {
  name: "USAJobs (US federal)",
  company_slug: "usajobs",
  ats: "usajobs",
  ats_slug: "usajobs",
};

describe("usajobsAdapter", () => {
  it("has name 'usajobs'", () => {
    expect(usajobsAdapter.name).toBe("usajobs");
  });

  it("fetchJobs uses the configured API key and user-agent", async () => {
    configureUsajobs({ apiKey: "TEST-KEY", userAgent: "test-ua" });
    const fetchImpl = vi.fn().mockImplementation((url: string, init: RequestInit) => {
      expect(url).toContain("data.usajobs.gov/api/Search");
      const headers = init.headers as Record<string, string>;
      expect(headers["Authorization-Key"]).toBe("TEST-KEY");
      expect(headers["User-Agent"]).toBe("test-ua");
      return Promise.resolve(new Response(JSON.stringify(fixture), { status: 200 }));
    });
    const jobs = await usajobsAdapter.fetchJobs("usajobs", { fetchImpl });
    expect(jobs.length).toBeGreaterThan(0);
  });

  it("returns empty and logs warning when no API key configured", async () => {
    configureUsajobs({ apiKey: undefined, userAgent: undefined });
    const jobs = await usajobsAdapter.fetchJobs("usajobs");
    expect(jobs).toEqual([]);
  });

  it("normalize produces a valid Job", () => {
    configureUsajobs({ apiKey: "TEST", userAgent: "test" });
    const item = (fixture as { SearchResult: { SearchResultItems: Array<{ MatchedObjectId: string; MatchedObjectDescriptor: unknown }> } })
      .SearchResult.SearchResultItems[0];
    const raw = { source_id: item.MatchedObjectId, raw: item };
    const job = usajobsAdapter.normalize(raw, company);
    expect(job.id).toBe(`usajobs:usajobs:${item.MatchedObjectId}`);
    expect(job.source).toBe("usajobs");
    expect(job.title).toBeTruthy();
  });

  it("toDetail surfaces compensation when remuneration is structured", () => {
    configureUsajobs({ apiKey: "TEST", userAgent: "test" });
    const raw = {
      source_id: "ABC123",
      raw: {
        MatchedObjectId: "ABC123",
        MatchedObjectDescriptor: {
          PositionTitle: "Engineer",
          OrganizationName: "Department of Energy",
          PositionLocationDisplay: "Washington, DC",
          PositionURI: "https://www.usajobs.gov/job/ABC123",
          PublicationStartDate: "2026-04-20T00:00:00",
          PositionRemuneration: [
            { MinimumRange: "120000", MaximumRange: "160000", RateIntervalCode: "PA", Currency: "USD" },
          ],
          QualificationSummary: "Lead engineering work.",
          UserArea: { Details: { JobSummary: "Full description text." } },
        },
      },
    };
    const detail = usajobsAdapter.toDetail(raw, company);
    expect(detail.compensation).toEqual({
      min: 120000,
      max: 160000,
      currency: "USD",
      interval: "yearly",
    });
  });
});
