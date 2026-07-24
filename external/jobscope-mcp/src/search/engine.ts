import type { AtsAdapter } from "../adapters/types";
import type { CompanyRef, Job, SearchJobsInput } from "../schemas";
import { applyFilters } from "./filter";
import { rankJobs } from "./rank";

export interface SearchArgs {
  input: SearchJobsInput;
  companies: CompanyRef[];
  adapters: AtsAdapter[];
}

export interface SearchResult {
  jobs: Job[];
  warnings: string[];
  meta: {
    queried_companies: number;
    queried_sources: string[];
    returned: number;
  };
}

export async function runSearch(args: SearchArgs): Promise<SearchResult> {
  const byAts = new Map<string, CompanyRef[]>();
  for (const c of args.companies) {
    const list = byAts.get(c.ats) ?? [];
    list.push(c);
    byAts.set(c.ats, list);
  }

  const warnings: string[] = [];
  const all: Job[] = [];
  const sourcesUsed = new Set<string>();

  const atsTasks: Promise<void>[] = [];

  for (const [atsName, companies] of byAts) {
    const adapter = args.adapters.find((a) => a.name === atsName);
    if (!adapter) {
      warnings.push(`unknown adapter: ${atsName}`);
      continue;
    }
    sourcesUsed.add(adapter.name);

    const concurrency = 8;
    const queue = [...companies];

    const workers = Array.from({ length: Math.min(concurrency, queue.length) }, async () => {
      while (queue.length > 0) {
        const company = queue.shift();
        if (!company) return;
        try {
          const raws = await adapter.fetchJobs(company.ats_slug);
          for (const r of raws) all.push(adapter.normalize(r, company));
        } catch (e) {
          const msg = e instanceof Error ? e.message : "unknown error";
          warnings.push(`${adapter.name}/${company.company_slug}: ${msg}`);
        }
      }
    });

    atsTasks.push(Promise.all(workers).then(() => undefined));
  }

  await Promise.all(atsTasks);

  const filtered = applyFilters(all, {
    query: args.input.query,
    location: args.input.location,
    remote: args.input.remote,
    posted_since: args.input.posted_since,
  });
  const ranked = rankJobs(filtered, args.input.query);
  const trimmed = ranked.slice(0, args.input.limit);

  return {
    jobs: trimmed,
    warnings,
    meta: {
      queried_companies: args.companies.length,
      queried_sources: Array.from(sourcesUsed).sort(),
      returned: trimmed.length,
    },
  };
}
