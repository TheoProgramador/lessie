import type { Job } from "../schemas";

function tokens(s: string): string[] {
  return s.toLowerCase().match(/[a-z0-9]+/g) ?? [];
}

function overlap(a: string[], b: string[]): number {
  const set = new Set(b);
  let n = 0;
  for (const t of a) if (set.has(t)) n += 1;
  return n;
}

export function rankJobs(jobs: Job[], query: string | undefined): Job[] {
  if (!query) return jobs;
  const qLower = query.toLowerCase();
  const qTokens = tokens(query);
  const scored = jobs.map((j, idx) => {
    const titleLower = j.title.toLowerCase();
    let score = 0;
    if (titleLower === qLower) score += 1000;
    else if (titleLower.includes(qLower)) score += 500;
    score += overlap(tokens(j.title), qTokens) * 20;
    score += overlap(tokens(j.description_preview), qTokens) * 1;
    return { j, idx, score };
  });
  scored.sort((a, b) => b.score - a.score || a.idx - b.idx);
  return scored.map((s) => s.j);
}
