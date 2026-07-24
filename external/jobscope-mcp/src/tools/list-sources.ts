import { z } from "zod";
import { adapters } from "../adapters/registry";
import { directoryGeneratedAt, loadDirectory } from "../directory/loader";

export const LIST_SOURCES_DESCRIPTION = [
  "Return the list of supported ATS sources with how many companies from the directory use each source and when the directory was built.",
  "Sources cover tech (Greenhouse, Lever, Ashby), enterprise (Workday), and US federal (USAJobs).",
].join(" ");

export const listSourcesInputSchema = z.object({}).default({});

export interface SourceInfo {
  name: string;
  company_count: number;
  last_refreshed_at: string;
}

export async function listSourcesTool(_raw: unknown): Promise<{ sources: SourceInfo[] }> {
  const dir = loadDirectory();
  const refreshed = directoryGeneratedAt();
  const sources = adapters.map((a) => ({
    name: a.name,
    company_count: dir.companies.filter((c) => c.ats === a.name).length,
    last_refreshed_at: refreshed,
  }));
  return { sources };
}
