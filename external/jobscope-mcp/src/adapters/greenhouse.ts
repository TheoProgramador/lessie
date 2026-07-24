import { fetchJson, type FetchImpl } from "./fetch";
import type { AtsAdapter, RawJob } from "./types";
import { JobSchema, JobDetailSchema } from "../schemas";
import { NotFoundError } from "../errors";
import type { CompanyRef, Job, JobDetail } from "../schemas";
import { parsePayRangeText, type Compensation } from "../compensation";

type GreenhouseRawJob = {
  id: number;
  title: string;
  location?: { name?: string };
  offices?: Array<{ name?: string; location?: string }>;
  absolute_url: string;
  updated_at?: string;
  first_published?: string;
  departments?: Array<{ name?: string }>;
  content?: string;
  metadata?: unknown;
};

function stripHtml(html: string): string {
  return html
    .replace(/<[^>]+>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/\s+/g, " ")
    .trim();
}

function inferRemote(locationText: string): "remote" | "hybrid" | "onsite" | "unknown" {
  const lower = locationText.toLowerCase();
  if (!lower) return "unknown";
  if (lower.includes("remote")) return "remote";
  if (lower.includes("hybrid")) return "hybrid";
  return "unknown";
}

function compensationFromMetadata(metadata: unknown): Compensation | null {
  if (!Array.isArray(metadata)) return null;
  for (const entry of metadata) {
    const e = entry as { name?: string; value?: unknown };
    if (typeof e.name !== "string") continue;
    if (!e.name.toLowerCase().match(/pay range|salary range|compensation/)) continue;
    if (typeof e.value === "string") {
      const parsed = parsePayRangeText(e.value);
      if (parsed) return parsed;
    }
    if (Array.isArray(e.value)) {
      const min = parseFloat(String((e.value as unknown[])[0] ?? ""));
      const max = parseFloat(String((e.value as unknown[])[1] ?? ""));
      if (!isNaN(min) || !isNaN(max)) {
        return {
          min: !isNaN(min) ? min : undefined,
          max: !isNaN(max) ? max : undefined,
          currency: "USD",
          interval: "yearly",
        };
      }
    }
  }
  return null;
}

function locationList(raw: GreenhouseRawJob): string[] {
  const list: string[] = [];
  if (raw.location?.name) list.push(raw.location.name);
  if (raw.offices) for (const o of raw.offices) if (o.name) list.push(o.name);
  return Array.from(new Set(list));
}

export interface GreenhouseFetchOptions {
  fetchImpl?: FetchImpl;
}

async function fetchJobs(atsSlug: string, opts: GreenhouseFetchOptions = {}): Promise<RawJob[]> {
  const url = `https://boards-api.greenhouse.io/v1/boards/${encodeURIComponent(atsSlug)}/jobs?content=true`;
  const body = await fetchJson<{ jobs: GreenhouseRawJob[] }>(url, { fetchImpl: opts.fetchImpl });
  return (body.jobs ?? []).map((j) => ({ source_id: String(j.id), raw: j }));
}

async function fetchJob(
  atsSlug: string,
  jobId: string,
  opts: GreenhouseFetchOptions = {}
): Promise<RawJob | null> {
  const url = `https://boards-api.greenhouse.io/v1/boards/${encodeURIComponent(atsSlug)}/jobs/${encodeURIComponent(jobId)}?content=true`;
  try {
    const body = await fetchJson<GreenhouseRawJob>(url, { fetchImpl: opts.fetchImpl });
    return { source_id: String(body.id), raw: body };
  } catch (e) {
    if (e instanceof NotFoundError) return null;
    throw e;
  }
}

function normalize(raw: RawJob, company: CompanyRef): Job {
  const r = raw.raw as GreenhouseRawJob;
  const locations = locationList(r);
  const locationText = locations.join(" ");
  const descriptionPlain = r.content ? stripHtml(r.content) : "";
  const job = {
    id: `greenhouse:${company.company_slug}:${raw.source_id}`,
    source: "greenhouse",
    company: company.name,
    company_slug: company.company_slug,
    title: r.title,
    locations,
    remote: inferRemote(locationText),
    employment_type: "unknown" as const,
    department: r.departments?.[0]?.name ?? null,
    posted_at: r.first_published ?? r.updated_at ?? null,
    url: r.absolute_url,
    description_preview: descriptionPlain.slice(0, 500),
  };
  return JobSchema.parse(job);
}

function toDetail(raw: RawJob, company: CompanyRef): JobDetail {
  const base = normalize(raw, company);
  const r = raw.raw as GreenhouseRawJob;
  const plain = r.content ? stripHtml(r.content) : "";
  const detail = {
    ...base,
    description: plain,
    description_html: r.content ?? "",
    team: r.departments?.[0]?.name ?? null,
    compensation: compensationFromMetadata(r.metadata),
    raw: r,
  };
  return JobDetailSchema.parse(detail);
}

export const greenhouseAdapter: AtsAdapter & {
  fetchJobs: (slug: string, opts?: GreenhouseFetchOptions) => Promise<RawJob[]>;
  fetchJob: (slug: string, id: string, opts?: GreenhouseFetchOptions) => Promise<RawJob | null>;
} = {
  name: "greenhouse",
  fetchJobs,
  fetchJob,
  normalize,
  toDetail,
};
