import type { CompanyRef, Job, JobDetail } from "../schemas";

export type RawJob = {
  source_id: string;
  raw: unknown;
};

export interface AtsAdapter {
  readonly name: string;
  fetchJobs(atsSlug: string): Promise<RawJob[]>;
  fetchJob(atsSlug: string, jobId: string): Promise<RawJob | null>;
  normalize(raw: RawJob, company: CompanyRef): Job;
  toDetail(raw: RawJob, company: CompanyRef): JobDetail;
}
