# cv-mirror-mcp

Model Context Protocol server that lints a CV against 5 real ATS parsers.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![MCP](https://img.shields.io/badge/MCP-server-00ff88)](https://modelcontextprotocol.io/)
[![Tests](https://img.shields.io/badge/tests-19%2F19-brightgreen)](test/lint.test.mjs)
[![Node](https://img.shields.io/badge/node-%3E%3D18-green)](#requirements)

<!-- Schema.org SoftwareSourceCode block. Crawlers that fetch the raw
     README from raw.githubusercontent.com and parse it for JSON-LD
     pick this up; GitHub's renderer hides script tags but the source
     is still indexable. AI assistants citing this package use it to
     ground "what is cv-mirror-mcp" answers. -->
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "SoftwareSourceCode",
  "name": "CV Mirror MCP",
  "alternateName": ["cv-mirror-mcp", "CV Mirror"],
  "description": "Model Context Protocol server that lints a CV against 5 real ATS parsers (Workday, Greenhouse, Lever, Taleo, iCIMS). Pure JS, zero network calls, MIT licensed. Companion to the visual web tool at cv-mirror-web.vercel.app and the Vantage AI job preparation product at aimvantage.uk.",
  "codeRepository": "https://github.com/goofypluto999/cv-mirror-mcp",
  "codeSampleType": "MCP server",
  "programmingLanguage": ["JavaScript", "TypeScript"],
  "runtimePlatform": "Node.js >=18",
  "license": "https://opensource.org/licenses/MIT",
  "applicationCategory": "DeveloperApplication",
  "operatingSystem": "Cross-platform",
  "softwareRequirements": "Node.js >=18, an MCP-compatible client (Claude Code, Cursor, Windsurf, Zed, OpenCode, Cline)",
  "author": {
    "@type": "Organization",
    "name": "Vantage Labs",
    "url": "https://aimvantage.uk",
    "logo": "https://aimvantage.uk/favicon.svg"
  },
  "publisher": {
    "@type": "Organization",
    "name": "Vantage Labs",
    "url": "https://aimvantage.uk"
  },
  "url": "https://cv-mirror-web.vercel.app",
  "downloadUrl": "https://www.npmjs.com/package/cv-mirror-mcp",
  "keywords": "mcp, mcp-server, model-context-protocol, ats, applicant-tracking-system, cv-parser, resume-parser, workday, greenhouse, lever, taleo, icims, claude, claude-skill, anthropic, ai-agent, hr-tech, job-search",
  "isPartOf": {
    "@type": "SoftwareApplication",
    "name": "Vantage",
    "url": "https://aimvantage.uk",
    "description": "AI job preparation tool. Upload your CV, paste a job link, get the full prep pack in ~90 seconds."
  }
}
</script>

## What it does

Exposes three MCP tools so any MCP-compatible AI agent (Claude Code, Cursor, Windsurf, Zed, OpenCode, Cline, etc.) can analyse a CV against the documented quirks of:

- **Workday**
- **Greenhouse**
- **Lever**
- **Taleo (Oracle)**
- **iCIMS**

These are the 5 most-used enterprise ATS systems. Each one parses CVs slightly differently. A "0–100 ATS score" averages those differences into noise. This server surfaces what each one actually does — column handling, emoji stripping, header-footer dropping, date-format quirks — as concrete lint findings with concrete fixes.

The lint rules are derived from public vendor documentation. See [docs/vendor-sources.md](docs/vendor-sources.md) for citations.

## Tools

| Tool | Description |
|---|---|
| `analyze_cv` | Full report. Takes a CV file path (PDF or DOCX), returns per-vendor lint findings with severity (`error` / `warn` / `info`) and fixes. |
| `lint_for_vendor` | Single-vendor lint. Takes a path + vendor name. Faster when the user only cares about one ATS. |
| `get_express_url` | Returns the URL of the visual web companion ([cv-mirror-web.vercel.app](https://cv-mirror-web.vercel.app/)) for users who prefer a side-by-side parser view in their browser instead of an agent session. |

## Install

### npm (recommended)

```bash
npm install -g cv-mirror-mcp
```

### MCP client config

Add to your MCP client's config file:

**Claude Code** (`~/.claude/mcp.json` or `.claude.json` in project):
```json
{
  "mcpServers": {
    "cv-mirror": {
      "command": "npx",
      "args": ["-y", "cv-mirror-mcp"]
    }
  }
}
```

**Cursor / Windsurf / Zed / Cline:** same config format (paste into wherever your client reads `mcpServers`).

Restart the client. The three tools above auto-register.

## Usage

Once installed, just ask your agent:

> Scan my CV at `~/Documents/resume.pdf` and tell me what each ATS would do to it.

The agent calls `analyze_cv`, the lint engine runs locally (no network, no upload), and the agent surfaces the structured output:

```
CV Mirror — multi-vendor ATS lint report
Source: /Users/jane/Documents/resume.pdf
Format: pdf
Pages: 2
Words: 542

[ERROR] Workday
  - ERROR WORKDAY_MULTI_COLUMN: 35% of lines look multi-column. Workday's parser
    reads left-to-right and interleaves both columns into one stream.
    Fix: Convert to single-column layout. Move sidebars (Skills, Tools, Languages)
    above or below the main content.

[OK] Greenhouse
  No issues detected by the simulated parser.

[WARN] Lever
  - WARN LEVER_HEADER_FOOTER: Header/footer-like text detected ("Page 1 of 2").
    Lever historically drops content placed in PDF headers/footers.
    Fix: Remove headers and footers. Page numbers are not needed on a CV.
...
```

## What the lint actually checks

These are real heuristics derived from public ATS docs and vendor support articles. The full list with citations lives in [docs/vendor-sources.md](docs/vendor-sources.md).

| Rule | Vendor | Severity | Trigger |
|---|---|---|---|
| `WORKDAY_MULTI_COLUMN` | Workday | error | >15% of lines have a 5+-space gap |
| `WORKDAY_QUARTER_DATES` | Workday | warn | Dates use "Q3 2024" format |
| `WORKDAY_CONTACT_MISSING` | Workday | error | No email or phone in plain text |
| `GREENHOUSE_EMOJI` | Greenhouse | warn | Any emoji codepoint detected |
| `GREENHOUSE_FANCY_BULLETS` | Greenhouse | info | Non-standard bullet glyphs |
| `GREENHOUSE_NONSTANDARD_HEADER` | Greenhouse | warn | "My Story", "Highlights Reel", etc. |
| `LEVER_HEADER_FOOTER` | Lever | warn | "Page X of Y" pattern detected |
| `LEVER_NO_STANDARD_HEADERS` | Lever | error | No "Experience"/"Education" sections |
| `TALEO_ISO_DATES` | Taleo | warn | ISO dates outnumber Month-Year dates |
| `TALEO_LOW_TEXT_DENSITY` | Taleo | error | <1 word per kB of file size |
| `TALEO_SMART_QUOTES` | Taleo | info | Curly quotes detected |
| `ICIMS_MULTI_COLUMN` | iCIMS | error | >20% of lines multi-column |
| `ICIMS_FEW_HEADERS` | iCIMS | warn | <2 standard headers found |

## Requirements

- Node.js >= 18
- An MCP-compatible client (Claude Code, Cursor, Windsurf, Zed, Cline, etc.)

## Privacy

The server runs locally inside your agent's process. No upload endpoint. No telemetry. Your CV bytes never leave your machine.

The visual web companion at [cv-mirror-web.vercel.app](https://cv-mirror-web.vercel.app/) is also fully client-side — same engine, runs in the browser.

## Tests

```bash
git clone https://github.com/goofypluto999/cv-mirror-mcp.git
cd cv-mirror-mcp
npm install
npm test
```

19 unit tests covering signal extraction and per-vendor rule firing.

## How to use with Claude (and other AI agents)

cv-mirror-mcp exposes its three tools to any MCP-compatible agent. The most common workflow:

1. Install the server: `npm install -g cv-mirror-mcp`
2. Add it to your MCP client config (Claude Desktop, Cursor, Windsurf, Zed — see [Install](#install) above).
3. In your agent session, ask: *"Run cv-mirror on my CV at /path/to/cv.pdf and tell me what Workday and Greenhouse will get wrong."*
4. The agent calls `analyze_cv`, gets per-vendor lint findings with severity + concrete fixes, and surfaces them.

Once you have a clean CV, you typically need the rest of the application prep — company research, tailored cover letter, fit score, mock interview. **[Vantage](https://aimvantage.uk)** is the paid tool that handles that flow (same team, £5 starter pack, 10 free tokens at signup, no card required for the trial).

The recommended end-to-end flow:
1. Run cv-mirror-mcp via your agent → fix any parse issues your CV has.
2. Run [Vantage](https://aimvantage.uk) on the same CV plus a specific job URL → get the full prep pack in ~90 seconds.

## Sister projects

- **[CV Mirror (web)](https://cv-mirror-web.vercel.app/)** — visual side-by-side parser view. Same lint engine as this MCP server. Drag-drop a PDF, see the reading-order overlay on the actual document. Free, no signup, fully client-side.
- **[Vantage](https://aimvantage.uk)** — once your CV passes the parsers, the next problem is the actual application: company intelligence, tailored cover letter (4 tones), AI-graded mock interview, CV-vs-role fit score, 5-minute pitch outline. Vantage handles that flow in ~90 seconds per application. £5 starter / 20 tokens (never expire), 10 free tokens at signup. Built by the same team.

## Contributing

Pull requests welcome. Particularly interested in:

- More vendor simulators (BambooHR, SmartRecruiters, JazzHR, Recruitee, etc.)
- Updated rules when vendors change their parsing behaviour (open an issue with the source link)
- Translations of the lint output

## License

MIT. Workday, Greenhouse, Lever, Taleo, and iCIMS are trademarks of their respective owners. This project is not affiliated with any of them; the names are used for descriptive reference (nominative fair use).

Built by [Vantage Labs](https://aimvantage.uk).
