import { fetchJson, type FetchImpl } from "./fetch";
import type { AtsAdapter, RawJob } from "./types";
import { JobSchema, JobDetailSchema } from "../schemas";
import type { CompanyRef, Job, JobDetail } from "../schemas";
import type { Compensation } from "../compensation";

type UsajobsItem = {
  MatchedObjectId: string;
  MatchedObjectDescriptor: {
    PositionTitle: string;
    OrganizationName?: string;
    DepartmentName?: string;
    PositionLocationDisplay?: string;
    PositionLocation?: Array<{ LocationName?: string }>;
    PositionURI?: string;
    ApplyURI?: string[];
    PublicationStartDate?: string;
    PositionRemuneration?: Array<{
      MinimumRange?: string;
      MaximumRange?: string;
      RateIntervalCode?: string;
      Currency?: string;
    }>;
    QualificationSummary?: string;
    UserArea?: { Details?: { JobSummary?: string } };
    PositionSchedule?: Array<{ Code?: string; Name?: string }>;
    PositionRemoteIndicator?: string;
  };
};

let cachedConfig: { apiKey?: string; userAgent?: string } = {};

export function configureUsajobs(config: { apiKey?: string; userAgent?: string }) {
  cachedConfig = config;
}

function intervalFromRateCode(code?: string): "yearly" | "monthly" | "hourly" {
  if (code === "PH") return "hourly";
  if (code === "PM") return "monthly";
  return "yearly";
}

function parseRemuneration(rem?: UsajobsItem["MatchedObjectDescriptor"]["PositionRemuneration"]): Compensation | null {
  if (!rem || rem.length === 0) return null;
  const r = rem[0];
  const min = r.MinimumRange ? parseFloat(r.MinimumRange) : undefined;
  const max = r.MaximumRange ? parseFloat(r.MaximumRange) : undefined;
  if ((min == null || isNaN(min)) && (max == null || isNaN(max))) return null;
  return {
    min: min != null && !isNaN(min) ? min : undefined,
    max: max != null && !isNaN(max) ? max : undefined,
    currency: r.Currency || "USD",
    interval: intervalFromRateCode(r.RateIntervalCode),
  };
}

function inferRemote(d: UsajobsItem["MatchedObjectDescriptor"]): "remote" | "hybrid" | "onsite" | "unknown" {
  const indicator = (d.PositionRemoteIndicator ?? "").toLowerCase();
  if (indicator === "y") return "remote";
  const text = (d.PositionLocationDisplay ?? "").toLowerCase();
  if (text.includes("remote")) return "remote";
  if (text.includes("anywhere")) return "remote";
  if (text.includes("hybrid")) return "hybrid";
  if (text.length > 0) return "onsite";
  return "unknown";
}

function employmentFromSchedule(d: UsajobsItem["MatchedObjectDescriptor"]): Job["employment_type"] {
  const code = d.PositionSchedule?.[0]?.Code ?? "";
  if (code === "1") return "full_time";
  if (code === "2") return "part_time";
  if (code === "3") return "contract";
  if (code === "5") return "internship";
  return "unknown";
}

function locations(d: UsajobsItem["MatchedObjectDescriptor"]): string[] {
  const out: string[] = [];
  if (d.PositionLocation) {
    for (const loc of d.PositionLocation) {
      if (loc.LocationName) out.push(loc.LocationName);
    }
  }
  if (out.length === 0 && d.PositionLocationDisplay) {
    out.push(d.PositionLocationDisplay);
  }
  return Array.from(new Set(out));
}

export interface UsajobsFetchOptions {
  fetchImpl?: FetchImpl;
  query?: string;
  locationName?: string;
  resultsPerPage?: number;
}

async function fetchJobs(_slug: string, opts: UsajobsFetchOptions = {}): Promise<RawJob[]> {
  if (!cachedConfig.apiKey) {
    return [];
  }
  const params = new URLSearchParams();
  params.set("ResultsPerPage", String(opts.resultsPerPage ?? 100));
  if (opts.query) params.set("Keyword", opts.query);
  if (opts.locationName) params.set("LocationName", opts.locationName);
  params.set("DatePosted", "30");
  const url = `https://data.usajobs.gov/api/Search?${params.toString()}`;
  const body = await fetchJson<{ SearchResult?: { SearchResultItems?: UsajobsItem[] } }>(url, {
    fetchImpl: opts.fetchImpl,
    headers: {
      "Authorization-Key": cachedConfig.apiKey,
      "User-Agent": cachedConfig.userAgent ?? "jobscope-mcp/0.2.0",
      "Host": "data.usajobs.gov",
    },
  });
  const items = body.SearchResult?.SearchResultItems ?? [];
  return items.map((i) => ({ source_id: i.MatchedObjectId, raw: i }));
}

async function fetchJob(slug: string, jobId: string, opts: UsajobsFetchOptions = {}): Promise<RawJob | null> {
  const items = await fetchJobs(slug, opts);
  return items.find((i) => i.source_id === jobId) ?? null;
}

function normalize(raw: RawJob, company: CompanyRef): Job {
  const item = raw.raw as UsajobsItem;
  const d = item.MatchedObjectDescriptor;
  const summary = d.QualificationSummary ?? d.UserArea?.Details?.JobSummary ?? "";
  const job = {
    id: `usajobs:${company.company_slug}:${raw.source_id}`,
    source: "usajobs",
    company: d.OrganizationName ?? d.DepartmentName ?? "US Federal Government",
    company_slug: company.company_slug,
    title: d.PositionTitle,
    locations: locations(d),
    remote: inferRemote(d),
    employment_type: employmentFromSchedule(d),
    department: d.DepartmentName ?? null,
    posted_at: d.PublicationStartDate ?? null,
    url: d.ApplyURI?.[0] ?? d.PositionURI ?? "https://www.usajobs.gov",
    description_preview: summary.slice(0, 500),
  };
  return JobSchema.parse(job);
}

function toDetail(raw: RawJob, company: CompanyRef): JobDetail {
  const base = normalize(raw, company);
  const item = raw.raw as UsajobsItem;
  const d = item.MatchedObjectDescriptor;
  const detail = {
    ...base,
    description: d.UserArea?.Details?.JobSummary ?? d.QualificationSummary ?? "",
    description_html: d.UserArea?.Details?.JobSummary ?? "",
    team: null,
    compensation: parseRemuneration(d.PositionRemuneration),
    raw: item,
  };
  return JobDetailSchema.parse(detail);
}

export const usajobsAdapter: AtsAdapter & {
  fetchJobs: (slug: string, opts?: UsajobsFetchOptions) => Promise<RawJob[]>;
  fetchJob: (slug: string, id: string, opts?: UsajobsFetchOptions) => Promise<RawJob | null>;
} = {
  name: "usajobs",
  fetchJobs,
  fetchJob,
  normalize,
  toDetail,
};
