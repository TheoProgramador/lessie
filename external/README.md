# External Integrations

External MCP servers and other third-party runtimes live under this folder. Keep their source code outside the backend projects.

## LinkedIn MCP Server

People Discovery expects the `eliasbiondo/linkedin-mcp-server` project at:

```text
external/linkedin-mcp-server
```

Install it from the repository root:

```powershell
cd external
git clone https://github.com/eliasbiondo/linkedin-mcp-server.git linkedin-mcp-server
cd linkedin-mcp-server
uv sync
uv run patchright install
```

On Windows, if Patchright is not found:

```powershell
uv run python -m patchright install
```

Run the LinkedIn login flow once:

```powershell
uv run linkedin-mcp-server --login
```

Log in to LinkedIn in the browser window. The MCP server stores browser session data under `~/.linkedin-mcp-server/browser-data` by default.

Lessie starts the MCP through backend configuration:

```json
{
  "Mcp": {
    "PeopleDiscovery": {
      "Enabled": true,
      "Provider": "LinkedInMcp",
      "Command": "uv",
      "Arguments": ["run", "linkedin-mcp-server"],
      "WorkingDirectory": "external/linkedin-mcp-server",
      "TimeoutSeconds": 60,
      "ToolName": "search_people"
    }
  }
}
```

If the MCP is not installed or authenticated, `/api/people-discovery/search` returns a controlled MCP error instead of fake data.

After installing `uv` with winget, Visual Studio may not see the updated PATH until it is restarted. In development, `Backend/src/Api/appsettings.Development.json` can point `Mcp:PeopleDiscovery:Command` directly to the installed `uv.exe`.

## APInfo MCP Server

APInfo is kept in `external/` only as historical source code. It is no longer registered in the Lessie runtime because Opportunity Discovery now favors providers with less browser automation and less manual captcha flow.

The old local .NET MCP server lives at:

```text
external/apinfo-mcp-server
```

It is started by the backend with:

```powershell
dotnet run --project external/apinfo-mcp-server/src/ApInfoMcpServer/ApInfoMcpServer.csproj
```

Run this once after package restore if Playwright needs browser assets:

```powershell
pwsh external/apinfo-mcp-server/src/ApInfoMcpServer/bin/Release/net9.0/playwright.ps1 install chromium
```

By default the APInfo MCP uses visible Microsoft Edge (`Headless=false`, `BrowserChannel=msedge`) so a human can click APInfo captcha when contact data is requested. It does not solve captcha, send resumes, or bypass APInfo controls.

## JobSpy MCP

Opportunity Discovery also evaluates JobSpy providers independently from APInfo.

Evaluated candidates:

- `borgius/jobspy-mcp-server`: MIT, cloned under `external/jobspy-mcp-server`, but rejected for runtime integration because the MCP server fails during startup with the installed SDK before exposing tools.
- `borgius/jobspy-js`: MIT, cloned under `external/jobspy-js`, selected for integration because it starts over stdio and exposes MCP tools successfully.

Install dependencies from the selected repository:

```powershell
cd external/jobspy-js
npm install
```

Discovered MCP tools:

```text
scrape_jobs - Scrape job listings from LinkedIn, Indeed, Glassdoor, Google, ZipRecruiter, Bayt, Naukri and BDJobs.
fetch_job   - Fetch details for a single provider-specific job id.
```

Lessie starts the selected MCP through backend configuration:

```json
{
  "OpportunityProviders": {
    "JobSpy": {
      "Enabled": true,
      "Command": "npx",
      "Arguments": ["vite-node", "src/mcp/index.ts"],
      "WorkingDirectory": "external/jobspy-js",
      "TimeoutSeconds": 180,
      "CacheMinutes": 5
    }
  }
}
```

The backend discovers the search tool dynamically at runtime and currently uses the `scrape_jobs` tool because its schema contains search, location and site parameters. JobSpy results are normalized to Opportunity Discovery rows and are not persisted by Lessie.

## jd-intel MCP

Opportunity Discovery can also use `prPMDev/jd-intel` through its published MCP package. This provider favors public ATS APIs over browser scraping and normalizes Greenhouse, Lever, Ashby, SmartRecruiters, Teamtailor, Recruitee and Workday jobs.

Default backend configuration:

```json
{
  "OpportunityProviders": {
    "JdIntel": {
      "Enabled": true,
      "Command": "npx",
      "Arguments": ["-y", "jd-intel-mcp"],
      "TimeoutSeconds": 90,
      "CacheMinutes": 10,
      "PostedWithinHours": 720,
      "MaxCompanies": 12,
      "FetchJobsToolName": "fetch_jobs",
      "Companies": []
    }
  }
}
```

Leave `Companies` empty to let jd-intel use its registry broadly, or set a curated list of target companies to reduce latency and noise. Lessie still deduplicates centrally across APInfo, JobSpy, Jobscope, jobsearch-buddy and jd-intel.

## Resume external MCPs

Resume Improvement can enrich its prompt with optional external MCP signals:

- `RChilliDevTeam/rchilli-mcp-hub`: remote streamable HTTP MCP for resume parsing, job-description parsing and resume/JD scoring. Keep disabled until a valid RChilli token is configured because resume data leaves the machine.
- `Rocketech-Software-Development/formacv-mcp`: stdio MCP via `npx -y @formacv/mcp` for CV tailoring/formatting/anonymization. Keep disabled until FormaCV tenant settings or demo settings are intentionally configured.
- `thechandanbhagat/cv-forge`: stdio MCP via `npx -y cv-forge` for parsing job requirements and generating application-package material.

Default configuration keeps these three disabled:

```json
{
  "ResumeImprovements": {
    "ExternalMcp": {
      "RChilli": {
        "Enabled": false,
        "ServerUrl": "https://mcp.rchilli.ai/mcp",
        "AccessToken": ""
      },
      "FormaCV": {
        "Enabled": false,
        "Command": "npx",
        "Arguments": ["-y", "@formacv/mcp"],
        "PrimaryToolName": "tailor_cv",
        "Environment": {
          "FORMACV_API_KEY": "",
          "FORMACV_SERVER_URL": ""
        }
      },
      "CvForge": {
        "Enabled": false,
        "Command": "npx",
        "Arguments": ["-y", "cv-forge"],
        "PrimaryToolName": "parse_job_requirements"
      }
    }
  }
}
```

When enabled, these MCPs add diagnostic context to the existing Lessie resume workflow; they do not replace CV Mirror or the internal ATS analyzer.
