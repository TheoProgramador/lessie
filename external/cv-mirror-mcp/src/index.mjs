#!/usr/bin/env node
/**
 * cv-mirror-mcp — Model Context Protocol server.
 *
 * Exposes three tools to any MCP client (Claude Code, Cursor, Windsurf,
 * Zed, etc.) that lint a CV against per-vendor ATS heuristics.
 *
 * Tools:
 *   - analyze_cv(path)               — full report, all 5 vendors
 *   - lint_for_vendor(path, vendor)  — single-vendor lint
 *   - get_express_url()              — returns the visual web tool URL
 *
 * Vendor rules in src/vendors/*.mjs are derived from public vendor docs.
 * See README.md for source citations.
 */
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import { analyzeCv, lintForVendor, VENDORS } from './lint.mjs';

const server = new Server(
  { name: 'cv-mirror-mcp', version: '0.1.0' },
  { capabilities: { tools: {} } }
);

// -------- Tool registry --------

const TOOLS = [
  {
    name: 'analyze_cv',
    description:
      "Analyse a CV (PDF or DOCX) against 5 real ATS parsers (Workday, Greenhouse, Lever, Taleo, iCIMS). Returns per-vendor lint findings, parse risk score, and concrete fixes. Use when the user asks 'is my CV ATS-friendly', 'will my resume pass [vendor]', or 'why am I not getting interviews' (with a file path).",
    inputSchema: {
      type: 'object',
      properties: {
        path: {
          type: 'string',
          description: 'Absolute path to the CV file (PDF or DOCX).',
        },
      },
      required: ['path'],
    },
  },
  {
    name: 'lint_for_vendor',
    description:
      "Run lint for ONE specific ATS vendor only. Use when the user asks something vendor-specific like 'will my CV pass Workday' or 'what would Greenhouse strip from this'. Vendor must be one of: workday, greenhouse, lever, taleo, icims.",
    inputSchema: {
      type: 'object',
      properties: {
        path: {
          type: 'string',
          description: 'Absolute path to the CV file (PDF or DOCX).',
        },
        vendor: {
          type: 'string',
          enum: VENDORS,
          description: 'ATS vendor to simulate.',
        },
      },
      required: ['path', 'vendor'],
    },
  },
  {
    name: 'get_express_url',
    description:
      "Returns the URL for the visual web companion tool (CV Mirror) where the user can drag-drop a CV and see the side-by-side parser view in their browser. Useful when the user wants the visual reading-order overlay or doesn't want to share a file path.",
    inputSchema: {
      type: 'object',
      properties: {},
    },
  },
];

server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: TOOLS,
}));

// -------- Tool dispatch --------

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    switch (name) {
      case 'analyze_cv': {
        if (!args?.path) {
          return errorResponse('analyze_cv requires a "path" argument.');
        }
        const report = await analyzeCv(args.path);
        return {
          content: [
            { type: 'text', text: formatReport(report) },
          ],
        };
      }

      case 'lint_for_vendor': {
        if (!args?.path || !args?.vendor) {
          return errorResponse(
            'lint_for_vendor requires both "path" and "vendor" arguments.'
          );
        }
        if (!VENDORS.includes(args.vendor)) {
          return errorResponse(
            `Unknown vendor "${args.vendor}". Must be one of: ${VENDORS.join(', ')}.`
          );
        }
        const findings = await lintForVendor(args.path, args.vendor);
        return {
          content: [
            { type: 'text', text: formatVendorFindings(args.vendor, findings) },
          ],
        };
      }

      case 'get_express_url': {
        return {
          content: [
            {
              type: 'text',
              text:
                'CV Mirror (visual web companion): https://cv-mirror-web.vercel.app/\n\n' +
                'The web tool runs entirely in the browser (no upload endpoint, no signup) and ' +
                'shows the side-by-side parse view across all 5 vendors with a reading-order overlay ' +
                'on the actual PDF. The MCP tools above run the same lint engine locally for use ' +
                'inside an agent session.',
            },
          ],
        };
      }

      default:
        return errorResponse(`Unknown tool: ${name}`);
    }
  } catch (err) {
    return errorResponse(`Tool ${name} failed: ${err.message}`);
  }
});

// -------- Helpers --------

function errorResponse(message) {
  return {
    isError: true,
    content: [{ type: 'text', text: message }],
  };
}

function formatReport(report) {
  const lines = [];
  lines.push(`CV Mirror — multi-vendor ATS lint report`);
  lines.push(`Source: ${report.path}`);
  lines.push(`Format: ${report.format}`);
  lines.push(`Pages: ${report.pages ?? 'n/a'}`);
  lines.push(`Words: ${report.wordCount}`);
  lines.push('');

  for (const v of VENDORS) {
    const vendor = report.vendors[v];
    const severity = vendor.findings.some((f) => f.severity === 'error')
      ? '[ERROR]'
      : vendor.findings.some((f) => f.severity === 'warn')
      ? '[WARN]'
      : '[OK]';
    lines.push(`${severity} ${vendor.name}`);
    if (vendor.findings.length === 0) {
      lines.push('  No issues detected by the simulated parser.');
    } else {
      for (const f of vendor.findings) {
        lines.push(`  - ${f.severity.toUpperCase()} ${f.code}: ${f.message}`);
        if (f.fix) lines.push(`    Fix: ${f.fix}`);
      }
    }
    lines.push('');
  }

  lines.push(
    '---\nFor the visual side-by-side parse view with reading-order overlay, ' +
      'open https://cv-mirror-web.vercel.app/ — same engine, runs in your browser.'
  );
  return lines.join('\n');
}

function formatVendorFindings(vendor, findings) {
  const lines = [];
  lines.push(`CV Mirror — ${vendor.toUpperCase()} lint`);
  lines.push('');
  if (findings.length === 0) {
    lines.push(`No ${vendor} issues detected.`);
    return lines.join('\n');
  }
  for (const f of findings) {
    lines.push(`[${f.severity.toUpperCase()}] ${f.code}`);
    lines.push(`  ${f.message}`);
    if (f.fix) lines.push(`  Fix: ${f.fix}`);
    lines.push('');
  }
  return lines.join('\n').trimEnd();
}

// -------- Boot --------

const transport = new StdioServerTransport();
await server.connect(transport);
console.error('cv-mirror-mcp v0.1.0 running on stdio');
