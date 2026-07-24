import { describe, expect, it, vi } from "vitest";
import fixture from "../fixtures/greenhouse/jobs-stripe.json";
import { greenhouseAdapter } from "../../src/adapters/greenhouse";

const company = {
  name: "Stripe",
  company_slug: "stripe",
  ats: "greenhouse",
  ats_slug: "stripe",
};

describe("greenhouseAdapter", () => {
  it("has name 'greenhouse'", () => {
    expect(greenhouseAdapter.name).toBe("greenhouse");
  });

  it("fetchJobs returns RawJob[] for a slug", async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValue(new Response(JSON.stringify(fixture), { status: 200 }));
    const jobs = await greenhouseAdapter.fetchJobs("stripe", { fetchImpl });
    expect(jobs.length).toBeGreaterThan(0);
    expect(jobs[0].source_id).toBeDefined();
  });

  it("normalize produces a valid Job with deterministic id", () => {
    const raw = { source_id: "123", raw: fixture.jobs[0] };
    const job = greenhouseAdapter.normalize(raw, company);
    expect(job.id).toBe(`greenhouse:stripe:${raw.source_id}`);
    expect(job.source).toBe("greenhouse");
    expect(job.company).toBe("Stripe");
    expect(job.title).toBeTruthy();
    expect(Array.isArray(job.locations)).toBe(true);
    expect(["remote", "hybrid", "onsite", "unknown"]).toContain(job.remote);
  });

  it("fetchJob returns null on upstream 404", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(new Response("{}", { status: 404 }));
    const result = await greenhouseAdapter.fetchJob("stripe", "123", { fetchImpl });
    expect(result).toBeNull();
  });

  it("normalize infers remote=remote when location contains 'Remote'", () => {
    const raw = {
      source_id: "1",
      raw: {
        id: 1,
        title: "Engineer",
        location: { name: "Remote - United States" },
        absolute_url: "https://x",
        updated_at: "2026-04-20T00:00:00Z",
        departments: [],
        content: "hi",
      },
    };
    const job = greenhouseAdapter.normalize(raw, company);
    expect(job.remote).toBe("remote");
  });
});

describe("greenhouseAdapter — v2 improvements", () => {
  it("returns 'unknown' instead of 'onsite' when location has no remote/hybrid signal but is non-empty", () => {
    const raw = {
      source_id: "1",
      raw: {
        id: 1,
        title: "Engineer",
        location: { name: "San Francisco, CA" },
        absolute_url: "https://x",
        updated_at: "2026-04-20T00:00:00Z",
        departments: [],
        content: "hi",
      },
    };
    const job = greenhouseAdapter.normalize(raw, company);
    expect(job.remote).toBe("unknown");
  });

  it("toDetail surfaces compensation parsed from metadata pay-range field", () => {
    const raw = {
      source_id: "1",
      raw: {
        id: 1,
        title: "Engineer",
        location: { name: "Remote" },
        absolute_url: "https://x",
        updated_at: "2026-04-20T00:00:00Z",
        departments: [],
        content: "<p>desc</p>",
        metadata: [
          { name: "Pay Range", value: "$200,000 - $260,000 USD", value_type: "currency_range" },
        ],
      },
    };
    const detail = greenhouseAdapter.toDetail(raw, company);
    expect(detail.compensation).toEqual({
      min: 200000,
      max: 260000,
      currency: "USD",
      interval: "yearly",
    });
  });

  it("toDetail returns null compensation when no pay-range metadata", () => {
    const raw = {
      source_id: "1",
      raw: {
        id: 1,
        title: "Engineer",
        location: { name: "Remote" },
        absolute_url: "https://x",
        updated_at: "2026-04-20T00:00:00Z",
        departments: [],
        content: "hi",
      },
    };
    const detail = greenhouseAdapter.toDetail(raw, company);
    expect(detail.compensation).toBeNull();
  });
});
