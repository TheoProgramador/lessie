import type { Job } from "../schemas";

export interface FilterOptions {
  query?: string;
  location?: string;
  remote?: "any" | "remote" | "hybrid" | "onsite";
  posted_since?: string | null;
}

function resolvePostedSince(input: string | null | undefined): number | null {
  if (input == null) return null;
  const m = input.match(/^(\d+)d$/);
  if (m) {
    const days = parseInt(m[1], 10);
    return Date.now() - days * 24 * 60 * 60 * 1000;
  }
  const ts = Date.parse(input);
  return isNaN(ts) ? null : ts;
}

export function applyFilters(jobs: Job[], opts: FilterOptions): Job[] {
  const q = opts.query?.toLowerCase();
  const loc = opts.location?.toLowerCase();
  const since = resolvePostedSince(opts.posted_since);
  const remote = opts.remote && opts.remote !== "any" ? opts.remote : null;
  return jobs.filter((j) => {
    if (q) {
      const hay = `${j.title} ${j.description_preview}`.toLowerCase();
      if (!hay.includes(q)) return false;
    }
    if (loc) {
      const hay = j.locations.join(" ").toLowerCase();
      if (!hay.includes(loc)) return false;
    }
    if (remote && j.remote !== remote) return false;
    if (since != null && j.posted_at) {
      const ts = Date.parse(j.posted_at);
      if (Number.isFinite(ts) && ts < since) return false;
    }
    return true;
  });
}
