import { McpAgent } from "agents/mcp";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";

import { isKnownError } from "./errors";
import { configureUsajobs } from "./adapters/usajobs";
import {
  searchJobsTool,
  searchJobsInputSchema,
  SEARCH_JOBS_DESCRIPTION,
} from "./tools/search-jobs";
import {
  getJobTool,
  getJobInputSchema,
  GET_JOB_DESCRIPTION,
} from "./tools/get-job";
import {
  listCompaniesTool,
  listCompaniesInputSchema,
  LIST_COMPANIES_DESCRIPTION,
} from "./tools/list-companies";
import {
  listSourcesTool,
  listSourcesInputSchema,
  LIST_SOURCES_DESCRIPTION,
} from "./tools/list-sources";

function toolResponse(result: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(result) }] };
}

function errorResponse(e: unknown) {
  const message = isKnownError(e) ? e.message : "Unexpected error.";
  return { isError: true, content: [{ type: "text" as const, text: message }] };
}

export class JobscopeMcp extends McpAgent {
  server = new McpServer({ name: "jobscope-mcp", version: "0.1.0" });

  async init() {
    // Configure USAJobs adapter from secrets if available; adapter degrades
    // gracefully (returns []) when no key is configured.
    const env = (this as unknown as { env?: { USAJOBS_API_KEY?: string; USAJOBS_USER_AGENT?: string } }).env;
    configureUsajobs({
      apiKey: env?.USAJOBS_API_KEY,
      userAgent: env?.USAJOBS_USER_AGENT,
    });

    this.server.registerTool(
      "search_jobs",
      { description: SEARCH_JOBS_DESCRIPTION, inputSchema: searchJobsInputSchema.shape },
      async (input) => {
        try {
          return toolResponse(await searchJobsTool(input));
        } catch (e) {
          return errorResponse(e);
        }
      }
    );

    this.server.registerTool(
      "get_job",
      { description: GET_JOB_DESCRIPTION, inputSchema: getJobInputSchema.shape },
      async (input) => {
        try {
          return toolResponse(await getJobTool(input));
        } catch (e) {
          return errorResponse(e);
        }
      }
    );

    this.server.registerTool(
      "list_companies",
      { description: LIST_COMPANIES_DESCRIPTION, inputSchema: listCompaniesInputSchema.shape },
      async (input) => {
        try {
          return toolResponse(await listCompaniesTool(input));
        } catch (e) {
          return errorResponse(e);
        }
      }
    );

    this.server.registerTool(
      "list_sources",
      { description: LIST_SOURCES_DESCRIPTION, inputSchema: { } },
      async () => {
        try {
          return toolResponse(await listSourcesTool({}));
        } catch (e) {
          return errorResponse(e);
        }
      }
    );
  }
}
