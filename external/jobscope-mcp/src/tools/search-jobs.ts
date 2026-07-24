import { SearchJobsInputSchema, type SearchJobsInput } from "../schemas";
import { loadDirectory, getCompany } from "../directory/loader";
import { adapters } from "../adapters/registry";
import { runSearch, type SearchResult } from "../search/engine";

export const SEARCH_JOBS_DESCRIPTION = [
  "Search open job postings across five sources: Greenhouse, Lever, Ashby, Workday, and USAJobs (US federal).",
  "Coverage spans tech startups, Fortune 500 enterprise (healthcare, finance, manufacturing, retail, universities), and US federal employment across every sector.",
  "Use this for any job search query: 'find me nursing jobs', 'find me government jobs', 'find me senior backend engineer roles', etc.",
  "By default returns postings from the last 30 days; pass `posted_since: null` to disable, or `posted_since: '7d'` / ISO date for other windows.",
  "Free-text query, location substring, remote-policy filter all supported.",
  "Use `companies` to restrict to specific directory slugs.",
  "Use `ats` to restrict to specific sources, e.g. `['usajobs']` for federal only or `['workday']` for enterprise only.",
  "Returns at most `limit` results (default 50, max 200), ranked by query relevance.",
].join(" ");

export const searchJobsInputSchema = SearchJobsInputSchema;

export async function searchJobsTool(rawInput: unknown): Promise<SearchResult> {
  const input = SearchJobsInputSchema.parse(rawInput) as SearchJobsInput;
  const dir = loadDirectory();
  let companies = input.companies
    ? input.companies.map((slug) => getCompany(slug)).filter((c): c is NonNullable<typeof c> => !!c)
    : dir.companies;
  if (input.ats && input.ats.length > 0) {
    const set = new Set(input.ats);
    companies = companies.filter((c) => set.has(c.ats));
  }
  return runSearch({ input, companies, adapters });
}
