import { fetchJson, type FetchImpl } from "./fetch";
import type { AtsAdapter, RawJob } from "./types";
import { JobSchema, JobDetailSchema } from "../schemas";
import type { CompanyRef, Job, JobDetail } from "../schemas";
import { parsePayRangeText } from "../compensation";

type AshbyRawJob = {
  id: string;
  title: string;
  department?: string;
  team?: string;
  employmentType?: string;
  locationName?: string;
  secondaryLocations?: Array<{ locationName?: string }>;
  isRemote?: boolean;
  isListed?: boolean;
  publishedAt?: string;
  jobUrl: string;
  applyUrl?: string;
  descriptionHtml?: string;
  descriptionPlain?: string;
  compensationTierSummary?: string;
};

function mapEmployment(t?: string): Job["employment_type"] {
  const v = (t ?? "").toLowerCase();
  if (v.includes("fulltime") || v === "full_time") return "full_time";
  if (v.includes("parttime") || v === "part_time") return "part_time";
  if (v.includes("contract")) return "contract";
  if (v.includes("intern")) return "internship";
  return "unknown";
}

export interface AshbyFetchOptions { fetchImpl?: FetchImpl }

async function fetchJobs(atsSlug: string, opts: AshbyFetchOptions = {}): Promise<RawJob[]> {
  const url = `https://api.ashbyhq.com/posting-api/job-board/${encodeURIComponent(atsSlug)}?includeCompensation=true`;
  const body = await fetchJson<{ jobs: AshbyRawJob[] }>(url, { fetchImpl: opts.fetchImpl });
  return (body.jobs ?? []).map((j) => ({ source_id: j.id, raw: j }));
}

// Ashby's public posting API does not expose a per-job endpoint, so we fetch
// the whole board and scan. The edge cache and in-request batching keep this
// acceptable; callers should prefer search_jobs for discovery and reserve
// fetchJob for a small number of targeted lookups.
async function fetchJob(
  atsSlug: string,
  jobId: string,
  opts: AshbyFetchOptions = {}
): Promise<RawJob | null> {
  const list = await fetchJobs(atsSlug, opts);
  return list.find((j) => j.source_id === jobId) ?? null;
}

function normalize(raw: RawJob, company: CompanyRef): Job {
  const r = raw.raw as AshbyRawJob;
  const locs: string[] = [];
  if (r.locationName) locs.push(r.locationName);
  if (r.secondaryLocations) for (const s of r.secondaryLocations) if (s.locationName) locs.push(s.locationName);
  const locations = Array.from(new Set(locs));
  const plain = r.descriptionPlain ?? "";
  const remote: Job["remote"] = r.isRemote
    ? "remote"
    : locations.join(" ").toLowerCase().includes("hybrid")
      ? "hybrid"
      : locations.length > 0
        ? "onsite"
        : "unknown";
  const job = {
    id: `ashby:${company.company_slug}:${raw.source_id}`,
    source: "ashby",
    company: company.name,
    company_slug: company.company_slug,
    title: r.title,
    locations,
    remote,
    employment_type: mapEmployment(r.employmentType),
    department: r.department ?? null,
    posted_at: r.publishedAt ?? null,
    url: r.applyUrl ?? r.jobUrl,
    description_preview: plain.slice(0, 500),
  };
  return JobSchema.parse(job);
}

function toDetail(raw: RawJob, company: CompanyRef): JobDetail {
  const base = normalize(raw, company);
  const r = raw.raw as AshbyRawJob;
  const compensation = r.compensationTierSummary ? parsePayRangeText(r.compensationTierSummary) : null;
  const detail = {
    ...base,
    description: r.descriptionPlain ?? "",
    description_html: r.descriptionHtml ?? "",
    team: r.team ?? null,
    compensation,
    raw: r,
  };
  return JobDetailSchema.parse(detail);
}

export const ashbyAdapter: AtsAdapter & {
  fetchJobs: (slug: string, opts?: AshbyFetchOptions) => Promise<RawJob[]>;
  fetchJob: (slug: string, id: string, opts?: AshbyFetchOptions) => Promise<RawJob | null>;
} = {
  name: "ashby",
  fetchJobs,
  fetchJob,
  normalize,
  toDetail,
};
