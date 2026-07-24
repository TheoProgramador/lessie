import { ListCompaniesInputSchema } from "../schemas";
import type { CompanyRef } from "../schemas";
import { listCompanies } from "../directory/loader";

export const LIST_COMPANIES_DESCRIPTION = [
  "Browse the bundled company directory.",
  "Use this to discover which companies the MCP can search before calling `search_jobs`.",
  "Supports substring `query`, `ats` filter, and `tags` filter.",
].join(" ");

export const listCompaniesInputSchema = ListCompaniesInputSchema;

export async function listCompaniesTool(rawInput: unknown): Promise<{
  companies: CompanyRef[];
  total: number;
}> {
  const input = ListCompaniesInputSchema.parse(rawInput);
  const matches = listCompanies({ query: input.query, ats: input.ats, tags: input.tags });
  const trimmed = matches.slice(0, input.limit);
  return { companies: trimmed, total: matches.length };
}
