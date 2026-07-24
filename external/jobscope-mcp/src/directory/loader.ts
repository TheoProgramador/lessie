import data from "./data.json";
import { CompanyRefSchema, type CompanyRef } from "../schemas";

export interface Directory {
  generated_at: string;
  companies: CompanyRef[];
}

let cached: Directory | null = null;

export function loadDirectory(): Directory {
  if (cached) return cached;
  const companies = (data as { companies: unknown[] }).companies.map((c) =>
    CompanyRefSchema.parse(c)
  );
  cached = {
    generated_at: (data as { generated_at: string }).generated_at,
    companies,
  };
  return cached;
}

export function getCompany(companySlug: string): CompanyRef | undefined {
  return loadDirectory().companies.find((c) => c.company_slug === companySlug);
}

export interface ListCompaniesFilter {
  query?: string;
  ats?: string[];
  tags?: string[];
}

export function listCompanies(filter: ListCompaniesFilter): CompanyRef[] {
  const q = filter.query?.toLowerCase();
  const ats = filter.ats ? new Set(filter.ats) : null;
  const tags = filter.tags ? new Set(filter.tags) : null;
  return loadDirectory().companies.filter((c) => {
    if (ats && !ats.has(c.ats)) return false;
    if (
      q &&
      !(
        c.name.toLowerCase().includes(q) ||
        c.company_slug.toLowerCase().includes(q)
      )
    )
      return false;
    if (tags) {
      const cTags = new Set(c.tags ?? []);
      let any = false;
      for (const t of tags)
        if (cTags.has(t)) {
          any = true;
          break;
        }
      if (!any) return false;
    }
    return true;
  });
}

export function directoryGeneratedAt(): string {
  return loadDirectory().generated_at;
}
