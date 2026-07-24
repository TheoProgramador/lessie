import { GetJobInputSchema } from "../schemas";
import type { JobDetail } from "../schemas";
import { getCompany } from "../directory/loader";
import { getAdapter } from "../adapters/registry";
import { NotFoundError } from "../errors";

export const GET_JOB_DESCRIPTION = [
  "Fetch full detail for a single job, including the complete description.",
  "Use this after triage via `search_jobs` to read a posting you want to consider seriously.",
  "Requires source (ats name), company_slug (from the directory), and job_id (as returned by search_jobs).",
].join(" ");

export const getJobInputSchema = GetJobInputSchema;

export async function getJobTool(rawInput: unknown): Promise<JobDetail> {
  const input = GetJobInputSchema.parse(rawInput);
  const company = getCompany(input.company_slug);
  if (!company) throw new NotFoundError(`unknown company: ${input.company_slug}`);
  const adapter = getAdapter(company.ats);
  if (!adapter) throw new NotFoundError(`unknown source: ${company.ats}`);
  const raw = await adapter.fetchJob(company.ats_slug, input.job_id);
  if (!raw) throw new NotFoundError(`job not found: ${input.source}/${input.company_slug}/${input.job_id}`);
  return adapter.toDetail(raw, company);
}
