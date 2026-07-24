import { z } from "zod";

export const RemoteEnum = z.enum(["remote", "hybrid", "onsite", "unknown"]);
export const EmploymentTypeEnum = z.enum([
  "full_time",
  "part_time",
  "contract",
  "internship",
  "unknown",
]);

export const JobSchema = z.object({
  id: z.string(),
  source: z.string(),
  company: z.string(),
  company_slug: z.string(),
  title: z.string(),
  locations: z.array(z.string()),
  remote: RemoteEnum,
  employment_type: EmploymentTypeEnum,
  department: z.string().nullable(),
  posted_at: z.string().nullable(),
  url: z.string(),
  description_preview: z.string(),
});
export type Job = z.infer<typeof JobSchema>;

export const CompensationSchema = z.object({
  min: z.number().optional(),
  max: z.number().optional(),
  currency: z.string().optional(),
  interval: z.enum(["yearly", "monthly", "hourly"]).optional(),
});

export const JobDetailSchema = JobSchema.extend({
  description: z.string(),
  description_html: z.string(),
  team: z.string().nullable(),
  compensation: CompensationSchema.nullable(),
  raw: z.unknown(),
});
export type JobDetail = z.infer<typeof JobDetailSchema>;

export const CompanyRefSchema = z.object({
  name: z.string(),
  company_slug: z.string(),
  ats: z.string(),
  ats_slug: z.string(),
  url: z.string().optional(),
  tags: z.array(z.string()).optional(),
});
export type CompanyRef = z.infer<typeof CompanyRefSchema>;

export const SearchJobsInputSchema = z.object({
  query: z.string().optional(),
  location: z.string().optional(),
  remote: z.enum(["any", "remote", "hybrid", "onsite"]).default("any"),
  companies: z.array(z.string()).optional(),
  ats: z.array(z.string()).optional(),
  posted_since: z
    .union([z.string(), z.null()])
    .default("30d"),
  limit: z.number().int().min(1).max(200).default(50),
});
export type SearchJobsInput = z.infer<typeof SearchJobsInputSchema>;

export const GetJobInputSchema = z.object({
  source: z.string(),
  company_slug: z.string(),
  job_id: z.string(),
});
export type GetJobInput = z.infer<typeof GetJobInputSchema>;

export const ListCompaniesInputSchema = z.object({
  query: z.string().optional(),
  ats: z.array(z.string()).optional(),
  tags: z.array(z.string()).optional(),
  limit: z.number().int().min(1).max(500).default(100),
});
export type ListCompaniesInput = z.infer<typeof ListCompaniesInputSchema>;
