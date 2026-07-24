# jobscope-mcp

Open-source Model Context Protocol (MCP) server that gives AI agents (Claude Code, Codex, and any other MCP client) specialized tools to discover job openings across Applicant Tracking System (ATS) platforms.

The agent supplies reasoning and memory. jobscope-mcp supplies normalized, multi-source job search.

## Sources supported

- **Greenhouse** (`boards-api.greenhouse.io`) — tech startups and scale-ups
- **Lever** (`api.lever.co`) — tech startups and scale-ups
- **Ashby** (`api.ashbyhq.com`) — tech startups and scale-ups
- **Workday** (`*.myworkdayjobs.com`) — Fortune 500 enterprise (e.g., NVIDIA, Pfizer, Cigna, Mastercard, Capital One, Boeing, Cornell). Uses a CSRF cookie-bootstrap step (GET first, then POST) to authenticate.
- **USAJobs** (`data.usajobs.gov`) — US federal employment across every sector and every state

The MCP bundles a hand-curated company directory of 59 companies and queries the public JSON APIs of these five platforms directly at request time. The Workday tenant list is small relative to Workday's full footprint because most tenants require accurate `tenant:wd-shard:career-site` URL discovery; the automated seed pipeline (future work) will expand coverage by scraping company career pages and following redirects.

## Tools

- `search_jobs` - fan out across the directory, return a normalized ranked list
- `get_job` - fetch full detail for one posting
- `list_companies` - browse the bundled directory
- `list_sources` - introspect which ATSs are configured and when the directory was built

## Design philosophy

- Stateless. No user data is persisted anywhere.
- Thin over clever. The MCP wraps public APIs and normalizes output. Reasoning stays in the agent.
- Complementary to the underlying ATS platforms and aggregators.

See `docs/superpowers/specs/2026-04-23-jobscope-mcp-design.md` for the full design.

## Running locally

```sh
npm install
npm test
npm run dev    # starts wrangler dev on http://127.0.0.1:8787
```

Health check: `curl http://127.0.0.1:8787/health`

## License

MIT. See `LICENSE`.

## Privacy

See `PRIVACY.md`.
