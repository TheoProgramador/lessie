# Privacy

jobscope-mcp is stateless by design.

- The server does not collect, store, or transmit user inputs, queries, or identifiers.
- The only data shipped with the server is a public company directory (company names, ATS slugs, tags) bundled as static JSON at build time.
- At request time, the server fetches job postings from public ATS endpoints (Greenhouse, Lever, Ashby) and returns normalized results. No user data is attached to these requests beyond a generic `user-agent` header identifying the MCP.
- When deployed on Cloudflare Workers, edge caches may briefly store upstream public API responses to reduce repeat calls. These caches contain only public ATS data, not user queries or identities.
- The server does not log request bodies or tool inputs with identifying information.

If you self-host and add logging, observability, or rate limiting, ensure those systems are configured to preserve the above guarantees.
