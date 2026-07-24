import { describe, expect, it, vi, beforeEach } from "vitest";
import fixture from "../fixtures/workday/jobs-nvidia.json";
import { workdayAdapter } from "../../src/adapters/workday";
import { clearCookieCache } from "../../src/adapters/cookie-utils";

const company = {
  name: "NVIDIA",
  company_slug: "nvidia",
  ats: "workday",
  ats_slug: "nvidia:wd5:NVIDIAExternalCareerSite",
};

beforeEach(() => clearCookieCache());

describe("workdayAdapter", () => {
  it("has name 'workday'", () => {
    expect(workdayAdapter.name).toBe("workday");
  });

  it("fetchJobs builds the correct POST URL from a composite slug", async () => {
    const fetchImpl = vi.fn().mockImplementation((url: string, init: RequestInit) => {
      if (init.method === "GET") {
        return Promise.resolve(new Response("<html></html>", { status: 200 }));
      }
      expect(url).toBe("https://nvidia.wd5.myworkdayjobs.com/wday/cxs/nvidia/NVIDIAExternalCareerSite/jobs");
      expect(init.method).toBe("POST");
      const body = JSON.parse(init.body as string);
      expect(body.limit).toBe(20);
      return Promise.resolve(
        new Response(
          JSON.stringify({ ...fixture, total: (fixture as { jobPostings: unknown[] }).jobPostings.length }),
          { status: 200 }
        )
      );
    });
    const jobs = await workdayAdapter.fetchJobs("nvidia:wd5:NVIDIAExternalCareerSite", { fetchImpl });
    expect(jobs.length).toBeGreaterThan(0);
    expect(jobs[0].source_id).toBeDefined();
    const firstPost = fetchImpl.mock.calls.find((c) => (c[1] as RequestInit).method === "POST");
    expect(firstPost).toBeDefined();
    const firstCallBody = JSON.parse((firstPost![1] as RequestInit).body as string);
    expect(firstCallBody.offset).toBe(0);
  });

  it("normalize produces a valid Job with deterministic id", () => {
    const firstPosting = (fixture as { jobPostings: Array<{ externalPath: string; title: string }> }).jobPostings[0];
    const raw = { source_id: firstPosting.externalPath, raw: firstPosting };
    const job = workdayAdapter.normalize(raw, company);
    expect(job.id).toBe(`workday:nvidia:${firstPosting.externalPath}`);
    expect(job.source).toBe("workday");
    expect(job.company).toBe("NVIDIA");
    expect(job.title).toBe(firstPosting.title);
    expect(Array.isArray(job.locations)).toBe(true);
  });

  it("normalize returns 'remote' when locationsText contains 'Remote'", () => {
    const raw = {
      source_id: "/job/USA-Remote/Engineer_R-1",
      raw: {
        title: "Engineer",
        externalPath: "/job/USA-Remote/Engineer_R-1",
        locationsText: "USA - Remote",
        postedOn: "Posted Yesterday",
        bulletFields: ["R-1"],
      },
    };
    const job = workdayAdapter.normalize(raw, company);
    expect(job.remote).toBe("remote");
  });

  it("paginates beyond the first page when total > limit", async () => {
    const calls: number[] = [];
    const fetchImpl = vi.fn().mockImplementation((_url: string, init: RequestInit) => {
      if (init.method === "GET") {
        return Promise.resolve(new Response("<html></html>", { status: 200 }));
      }
      const body = JSON.parse(init.body as string);
      calls.push(body.offset);
      const totalAvailable = 45;
      const offset = body.offset;
      const slice = totalAvailable - offset;
      const count = Math.min(20, slice > 0 ? slice : 0);
      return Promise.resolve(new Response(JSON.stringify({
        total: totalAvailable,
        jobPostings: Array.from({ length: count }, (_, i) => ({
          title: `J${offset + i}`,
          externalPath: `/job/${offset + i}`,
          locationsText: "",
          postedOn: "Posted Today",
          bulletFields: [`R-${offset + i}`],
        })),
      }), { status: 200 }));
    });
    const jobs = await workdayAdapter.fetchJobs("nvidia:wd5:NVIDIAExternalCareerSite", { fetchImpl });
    expect(calls).toEqual([0, 20, 40]);
    expect(jobs.length).toBe(45);
  });

  it("caps fetch at 200 jobs even if total is higher", async () => {
    const fetchImpl = vi.fn().mockImplementation((_url: string, init: RequestInit) => {
      if (init.method === "GET") {
        return Promise.resolve(new Response("<html></html>", { status: 200 }));
      }
      const body = JSON.parse(init.body as string);
      const offset = body.offset;
      return Promise.resolve(new Response(JSON.stringify({
        total: 1000,
        jobPostings: Array.from({ length: 20 }, (_, i) => ({
          title: `J${offset + i}`,
          externalPath: `/job/${offset + i}`,
          locationsText: "",
          postedOn: "Posted Today",
          bulletFields: [`R-${offset + i}`],
        })),
      }), { status: 200 }));
    });
    const jobs = await workdayAdapter.fetchJobs("nvidia:wd5:NVIDIAExternalCareerSite", { fetchImpl });
    expect(jobs.length).toBe(200);
  });

  it("does cookie-bootstrap GET when GET response sets cookies, then sends Cookie + CSRF on POST", async () => {
    const fetchImpl = vi.fn().mockImplementation((url: string, init: RequestInit) => {
      if (init.method === "GET") {
        const headers = new Headers();
        headers.append("set-cookie", "PLAY_SESSION=abc; Path=/");
        headers.append("set-cookie", "calypso-csrf-token=tok99; Path=/");
        return Promise.resolve(new Response("<html></html>", { status: 200, headers }));
      }
      const reqHeaders = init.headers as Record<string, string>;
      expect(reqHeaders["Cookie"]).toContain("PLAY_SESSION=abc");
      expect(reqHeaders["Cookie"]).toContain("calypso-csrf-token=tok99");
      expect(reqHeaders["X-CALYPSO-CSRF-TOKEN"]).toBe("tok99");
      return Promise.resolve(
        new Response(
          JSON.stringify({ ...fixture, total: (fixture as { jobPostings: unknown[] }).jobPostings.length }),
          { status: 200 }
        )
      );
    });
    const jobs = await workdayAdapter.fetchJobs("nvidia:wd5:NVIDIAExternalCareerSite", { fetchImpl });
    expect(jobs.length).toBeGreaterThan(0);
    const getCalls = fetchImpl.mock.calls.filter((c) => (c[1] as RequestInit).method === "GET");
    expect(getCalls.length).toBe(1);
  });

  it("reuses cached cookies on a second fetchJobs call (no second GET)", async () => {
    const fetchImpl = vi.fn().mockImplementation((url: string, init: RequestInit) => {
      if (init.method === "GET") {
        const headers = new Headers();
        headers.append("set-cookie", "PLAY_SESSION=abc; Path=/");
        headers.append("set-cookie", "calypso-csrf-token=tok99; Path=/");
        return Promise.resolve(new Response("<html></html>", { status: 200, headers }));
      }
      return Promise.resolve(
        new Response(
          JSON.stringify({ ...fixture, total: (fixture as { jobPostings: unknown[] }).jobPostings.length }),
          { status: 200 }
        )
      );
    });
    await workdayAdapter.fetchJobs("nvidia:wd5:NVIDIAExternalCareerSite", { fetchImpl });
    await workdayAdapter.fetchJobs("nvidia:wd5:NVIDIAExternalCareerSite", { fetchImpl });
    const getCalls = fetchImpl.mock.calls.filter((c) => (c[1] as RequestInit).method === "GET");
    expect(getCalls.length).toBe(1);
  });
});
