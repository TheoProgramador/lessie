import { fetchJson, type FetchImpl } from "./fetch";
import type { AtsAdapter, RawJob } from "./types";
import { NotFoundError } from "../errors";
import { JobSchema, JobDetailSchema } from "../schemas";
import type { CompanyRef, Job, JobDetail } from "../schemas";
import { parsePayRangeText, type Compensation } from "../compensation";

type LeverRawJob = {
  id: string;
  text: string;
  categories?: { team?: string; location?: string; commitment?: string; department?: string };
  workplaceType?: string;
  createdAt?: number;
  hostedUrl: string;
  applyUrl?: string;
  descriptionPlain?: string;
  description?: string;
  lists?: Array<{ text?: string; content?: string }>;
};

function mapEmployment(commitment?: string): Job["employment_type"] {
  if (!commitment) return "unknown";
  const c = commitment.toLowerCase();
  if (c.includes("full")) return "full_time";
  if (c.includes("part")) return "part_time";
  if (c.includes("contract") || c.includes("contractor")) return "contract";
  if (c.includes("intern")) return "internship";
  return "unknown";
}

function mapRemote(workplaceType?: string, location?: string): Job["remote"] {
  const w = (workplaceType ?? "").toLowerCase();
  if (w === "remote") return "remote";
  if (w === "hybrid") return "hybrid";
  if (w === "on-site" || w === "onsite") return "onsite";
  const loc = (location ?? "").toLowerCase();
  if (loc.includes("remote")) return "remote";
  if (loc.includes("hybrid")) return "hybrid";
  if (loc.length > 0) return "onsite";
  return "unknown";
}

function stripHtml(html: string): string {
  return html.replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
}

function compensationFromLists(lists?: Array<{ text?: string; content?: string }>): Compensation | null {
  if (!lists) return null;
  for (const entry of lists) {
    const heading = (entry.text ?? "").toLowerCase();
    if (!heading.match(/compensation|salary|pay range/)) continue;
    const text = stripHtml(entry.content ?? "");
    const parsed = parsePayRangeText(text);
    if (parsed) return parsed;
  }
  return null;
}

export interface LeverFetchOptions { fetchImpl?: FetchImpl }

async function fetchJobs(atsSlug: string, opts: LeverFetchOptions = {}): Promise<RawJob[]> {
  const url = `https://api.lever.co/v0/postings/${encodeURIComponent(atsSlug)}?mode=json`;
  const body = await fetchJson<LeverRawJob[]>(url, { fetchImpl: opts.fetchImpl });
  return body.map((j) => ({ source_id: j.id, raw: j }));
}

async function fetchJob(
  atsSlug: string,
  jobId: string,
  opts: LeverFetchOptions = {}
): Promise<RawJob | null> {
  const url = `https://api.lever.co/v0/postings/${encodeURIComponent(atsSlug)}/${encodeURIComponent(jobId)}?mode=json`;
  try {
    const body = await fetchJson<LeverRawJob>(url, { fetchImpl: opts.fetchImpl });
    return { source_id: body.id, raw: body };
  } catch (e) {
    if (e instanceof NotFoundError) return null;
    throw e;
  }
}

function normalize(raw: RawJob, company: CompanyRef): Job {
  const r = raw.raw as LeverRawJob;
  const loc = r.categories?.location ?? "";
  const locations = loc ? [loc] : [];
  const plain = r.descriptionPlain ?? "";
  const job = {
    id: `lever:${company.company_slug}:${raw.source_id}`,
    source: "lever",
    company: company.name,
    company_slug: company.company_slug,
    title: r.text,
    locations,
    remote: mapRemote(r.workplaceType, loc),
    employment_type: mapEmployment(r.categories?.commitment),
    department: r.categories?.team ?? r.categories?.department ?? null,
    posted_at: r.createdAt ? new Date(r.createdAt).toISOString() : null,
    url: r.applyUrl ?? r.hostedUrl,
    description_preview: plain.slice(0, 500),
  };
  return JobSchema.parse(job);
}

function toDetail(raw: RawJob, company: CompanyRef): JobDetail {
  const base = normalize(raw, company);
  const r = raw.raw as LeverRawJob;
  const detail = {
    ...base,
    description: r.descriptionPlain ?? "",
    description_html: r.description ?? "",
    team: r.categories?.team ?? null,
    compensation: compensationFromLists(r.lists),
    raw: r,
  };
  return JobDetailSchema.parse(detail);
}

export const leverAdapter: AtsAdapter & {
  fetchJobs: (slug: string, opts?: LeverFetchOptions) => Promise<RawJob[]>;
  fetchJob: (slug: string, id: string, opts?: LeverFetchOptions) => Promise<RawJob | null>;
} = {
  name: "lever",
  fetchJobs,
  fetchJob,
  normalize,
  toDetail,
};
