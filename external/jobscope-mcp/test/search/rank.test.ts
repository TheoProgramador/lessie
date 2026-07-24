import { describe, expect, it } from "vitest";
import { rankJobs } from "../../src/search/rank";
import type { Job } from "../../src/schemas";

function mk(id: string, title: string, preview = ""): Job {
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
    description_preview: preview,
  };
}

describe("rankJobs", () => {
  it("ranks exact title match highest", () => {
    const jobs = [
      mk("1", "Senior Frontend Engineer"),
      mk("2", "Senior Backend Engineer"),
      mk("3", "Staff Backend Engineer"),
    ];
    const ranked = rankJobs(jobs, "senior backend engineer");
    expect(ranked[0].id).toBe("2");
  });

  it("falls back to token overlap in description_preview", () => {
    const jobs = [mk("1", "Engineer", "python backend kafka"), mk("2", "Engineer", "sales")];
    const ranked = rankJobs(jobs, "python kafka");
    expect(ranked[0].id).toBe("1");
  });

  it("is stable when no query is provided", () => {
    const a = mk("1", "A"), b = mk("2", "B");
    expect(rankJobs([a, b], undefined).map((j) => j.id)).toEqual(["1", "2"]);
  });
});
