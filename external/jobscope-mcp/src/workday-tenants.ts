import type { CompanyRef } from "./schemas";

// Hand-curated Workday tenants verified to respond to unauthenticated POST
// requests as of 2026-04-25. Each entry's `ats_slug` follows the format
// "tenant:wd-shard:career-site" (colon-separated; the workday adapter parses
// it). Tenants drift over time as companies migrate; the directory refresh
// pipeline (Plan 2B) will eventually re-validate these on a schedule.
export const WORKDAY_TENANTS: CompanyRef[] = [
  { name: "Albemarle Corporation", company_slug: "albemarle", ats: "workday", ats_slug: "albemarle:wd5:External", tags: ["enterprise", "manufacturing"] },
  { name: "Capital One", company_slug: "capitalone", ats: "workday", ats_slug: "capitalone:wd12:Capital_One", tags: ["enterprise", "finance"] },
  { name: "Boeing", company_slug: "boeing", ats: "workday", ats_slug: "boeing:wd1:EXTERNAL_CAREERS", tags: ["enterprise", "manufacturing"] },
  { name: "Caterpillar", company_slug: "cat", ats: "workday", ats_slug: "cat:wd5:CaterpillarCareers", tags: ["enterprise", "manufacturing"] },
  { name: "Cigna", company_slug: "cigna", ats: "workday", ats_slug: "cigna:wd5:CignaCareers", tags: ["enterprise", "healthcare"] },
  { name: "Citigroup", company_slug: "citi", ats: "workday", ats_slug: "citi:wd5:2", tags: ["enterprise", "finance"] },
  { name: "Cornell University", company_slug: "cornell", ats: "workday", ats_slug: "cornell:wd1:CornellCareerPage", tags: ["university"] },
  { name: "Equifax", company_slug: "equifax", ats: "workday", ats_slug: "equifax:wd5:External", tags: ["enterprise", "finance"] },
  { name: "Illinois Tool Works", company_slug: "itw", ats: "workday", ats_slug: "itw:wd5:External", tags: ["enterprise", "manufacturing"] },
  { name: "LabCorp", company_slug: "labcorp", ats: "workday", ats_slug: "labcorp:wd1:External", tags: ["enterprise", "healthcare"] },
  { name: "Mastercard", company_slug: "mastercard", ats: "workday", ats_slug: "mastercard:wd1:CorporateCareers", tags: ["enterprise", "finance"] },
  { name: "Leidos", company_slug: "leidos", ats: "workday", ats_slug: "leidos:wd5:External", tags: ["enterprise", "government"] },
  { name: "Micron Technology", company_slug: "micron", ats: "workday", ats_slug: "micron:wd1:External", tags: ["enterprise", "tech"] },
  { name: "NVIDIA", company_slug: "nvidia", ats: "workday", ats_slug: "nvidia:wd5:NVIDIAExternalCareerSite", tags: ["enterprise", "tech"] },
  { name: "Pfizer", company_slug: "pfizer", ats: "workday", ats_slug: "pfizer:wd1:PfizerCareers", tags: ["enterprise", "pharma"] },
  { name: "Salesforce", company_slug: "salesforce", ats: "workday", ats_slug: "salesforce:wd12:External_Career_Site", tags: ["enterprise", "tech"] },
  { name: "Takeda Pharmaceutical", company_slug: "takeda", ats: "workday", ats_slug: "takeda:wd3:External", tags: ["enterprise", "pharma"] },
  { name: "Target", company_slug: "target", ats: "workday", ats_slug: "target:wd5:targetcareers", tags: ["enterprise", "retail"] },
  { name: "T-Mobile", company_slug: "tmobile", ats: "workday", ats_slug: "tmobile:wd1:External", tags: ["enterprise", "tech"] },
  { name: "Travelers Companies", company_slug: "travelers", ats: "workday", ats_slug: "travelers:wd5:External", tags: ["enterprise", "finance"] },
  { name: "Visa", company_slug: "visa", ats: "workday", ats_slug: "visa:wd5:Visa", tags: ["enterprise", "finance"] },
  { name: "Williams Companies", company_slug: "williams", ats: "workday", ats_slug: "williams:wd5:External", tags: ["enterprise", "manufacturing"] },
];
