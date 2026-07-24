/**
 * Lint engine.
 *
 * Two stages:
 *   1. Parse the file (PDF or DOCX) into normalised text + structural metadata.
 *   2. Run vendor-specific heuristics derived from public docs.
 *
 * Vendor rules are intentionally conservative: each rule has a citation in
 * docs/vendor-sources.md. We avoid claims we cannot back up.
 */
import { readFile, stat } from 'node:fs/promises';
import { extname } from 'node:path';

export const VENDORS = ['workday', 'greenhouse', 'lever', 'taleo', 'icims'];

const VENDOR_NAMES = {
  workday: 'Workday',
  greenhouse: 'Greenhouse',
  lever: 'Lever',
  taleo: 'Taleo (Oracle)',
  icims: 'iCIMS',
};

// ---------------------------------------------------------------- parsing ----

async function parse(path) {
  const ext = extname(path).toLowerCase();
  const buf = await readFile(path);
  const fileSize = (await stat(path)).size;

  if (ext === '.pdf') {
    // Lazy-import so DOCX-only environments don't pay the load cost
    const { default: pdfParse } = await import('pdf-parse');
    const data = await pdfParse(buf);
    return {
      format: 'pdf',
      fileSize,
      pages: data.numpages,
      text: data.text || '',
      info: data.info || {},
      // pdf-parse doesn't give per-page bounding boxes; we use heuristics
      // on the text shape (line widths, multi-column gutters) below.
    };
  }

  if (ext === '.docx') {
    const mammoth = await import('mammoth');
    const result = await mammoth.extractRawText({ buffer: buf });
    return {
      format: 'docx',
      fileSize,
      pages: null,
      text: result.value || '',
      info: {},
    };
  }

  throw new Error(`Unsupported file extension: ${ext}. Use .pdf or .docx.`);
}

// ----------------------------------------------------- generic signal calc ---

function computeSignals(parsed) {
  const text = parsed.text || '';
  const lines = text.split('\n');
  const nonEmpty = lines.filter((l) => l.trim().length > 0);
  const wordCount = (text.match(/\b\w+\b/g) || []).length;

  // Multi-column heuristic: count lines with a large gap of whitespace inside them.
  // Two-column CVs typically have lines like "Skills          Experience" with 5+ spaces.
  const multiColumnLines = nonEmpty.filter((l) => /\S {5,}\S/.test(l)).length;
  const multiColumnRatio = nonEmpty.length > 0 ? multiColumnLines / nonEmpty.length : 0;

  // Image-only PDF detection: very low text relative to file size.
  // <1 word per kB is suspicious for a CV-shaped document.
  const wordsPerKB = parsed.fileSize > 0 ? wordCount / (parsed.fileSize / 1024) : 0;

  // Header/footer markers — Lever historically dropped these.
  const hasHeaderFooterLikeText = /^\s*page \d+( of \d+)?\s*$/im.test(text);

  // Date format detection (basic). Workday and Taleo are picky about non-standard
  // date formats in employment history.
  const datePatterns = {
    monthYear: /\b(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\s+\d{4}\b/gi,
    isoLike: /\b\d{4}-\d{1,2}\b/g,
    mmYY: /\b\d{1,2}\/\d{2,4}\b/g,
    quarter: /\bQ[1-4]\s+\d{4}\b/g,
  };
  const dateCounts = Object.fromEntries(
    Object.entries(datePatterns).map(([k, re]) => [k, (text.match(re) || []).length])
  );

  // Unicode quirks (emoji, special bullets, smart quotes). Greenhouse strips many.
  const hasEmoji = /[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(text);
  const hasSmartQuotes = /[‘’“”]/.test(text);
  const hasFancyBullets = /[▪▫◦❖➜➔]/.test(text);

  // Section headers — non-standard ones can confuse rule-based parsers.
  const standardHeaders = [
    'experience', 'work experience', 'professional experience',
    'education', 'skills', 'summary', 'objective', 'projects',
    'certifications', 'awards',
  ];
  const headersFound = standardHeaders.filter((h) =>
    new RegExp(`^\\s*${h}\\s*$`, 'im').test(text)
  );
  const hasNonStandardHeaders = /^\s*(my\s+story|the\s+journey|chapters|highlights\s+reel)\s*$/im.test(text);

  // Phone/email detection (must be findable for parsers to populate fields).
  const hasEmail = /[\w.+-]+@[\w-]+\.[\w.-]+/.test(text);
  const hasPhone = /(\+?\d[\d\s().-]{7,}\d)/.test(text);

  return {
    wordCount,
    nonEmptyLines: nonEmpty.length,
    multiColumnLines,
    multiColumnRatio,
    wordsPerKB,
    hasHeaderFooterLikeText,
    dateCounts,
    hasEmoji,
    hasSmartQuotes,
    hasFancyBullets,
    headersFound,
    hasNonStandardHeaders,
    hasEmail,
    hasPhone,
  };
}

// ------------------------------------------------------------ vendor rules ---
//
// Every rule is keyed to a public source. See docs/vendor-sources.md.
//
// Severity convention:
//   error — the parser is likely to drop or mis-extract a critical field
//   warn  — the parser is likely to mis-rank or mis-categorise content
//   info  — observation the human might want to know but parser handles it

const VENDOR_RULES = {
  workday: (s) => {
    const out = [];
    if (s.multiColumnRatio > 0.15) {
      out.push({
        code: 'WORKDAY_MULTI_COLUMN',
        severity: 'error',
        message: `${Math.round(s.multiColumnRatio * 100)}% of lines look multi-column. Workday's parser reads left-to-right and interleaves both columns into one stream, which corrupts your role-by-role history.`,
        fix: 'Convert to single-column layout. Move sidebars (Skills, Tools, Languages) above or below the main content.',
      });
    }
    if (s.dateCounts.quarter > 0 && s.dateCounts.monthYear === 0) {
      out.push({
        code: 'WORKDAY_QUARTER_DATES',
        severity: 'warn',
        message: 'Quarter-format dates (e.g. "Q3 2024") are not parsed as employment dates by Workday. Years-of-experience calculations will be wrong.',
        fix: 'Use Month-Year format ("Sep 2024 – Mar 2026") for every role.',
      });
    }
    if (!s.hasEmail || !s.hasPhone) {
      out.push({
        code: 'WORKDAY_CONTACT_MISSING',
        severity: 'error',
        message: `Workday auto-populates the contact form from your CV. Missing field: ${[!s.hasEmail && 'email', !s.hasPhone && 'phone'].filter(Boolean).join(', ')}.`,
        fix: 'Put email and phone in plain text near the top of page 1, not in headers/footers.',
      });
    }
    return out;
  },

  greenhouse: (s) => {
    const out = [];
    if (s.hasEmoji) {
      out.push({
        code: 'GREENHOUSE_EMOJI',
        severity: 'warn',
        message: 'Emoji detected. Greenhouse strips emoji and surrounding context can be lost, especially in headers like "Projects 🚀".',
        fix: 'Remove emoji from headers and bullet points.',
      });
    }
    if (s.hasFancyBullets) {
      out.push({
        code: 'GREENHOUSE_FANCY_BULLETS',
        severity: 'info',
        message: 'Non-standard bullet glyphs detected. Most are fine but some get collapsed to spaces, occasionally merging two bullets into one line.',
        fix: 'Use standard "•" or "-" bullets.',
      });
    }
    if (s.hasNonStandardHeaders) {
      out.push({
        code: 'GREENHOUSE_NONSTANDARD_HEADER',
        severity: 'warn',
        message: "Non-standard section headers (e.g. 'My Story') are not classified by Greenhouse's section detector. The content gets dumped into 'Other'.",
        fix: 'Use standard headers: Experience, Education, Skills, Summary, Projects.',
      });
    }
    return out;
  },

  lever: (s) => {
    const out = [];
    if (s.hasHeaderFooterLikeText) {
      out.push({
        code: 'LEVER_HEADER_FOOTER',
        severity: 'warn',
        message: 'Header/footer-like text detected ("Page 1 of 2"). Lever historically drops content placed in PDF headers/footers — make sure no employer name or date is in the header.',
        fix: 'Remove headers and footers. Page numbers are not needed on a CV.',
      });
    }
    if (s.headersFound.length === 0) {
      out.push({
        code: 'LEVER_NO_STANDARD_HEADERS',
        severity: 'error',
        message: 'No standard section headers found. Lever uses header detection to delimit Experience vs Education vs Skills.',
        fix: 'Add explicit "Experience" and "Education" headers as their own lines.',
      });
    }
    return out;
  },

  taleo: (s) => {
    const out = [];
    if (s.dateCounts.isoLike > 0 && s.dateCounts.monthYear < s.dateCounts.isoLike) {
      out.push({
        code: 'TALEO_ISO_DATES',
        severity: 'warn',
        message: 'ISO-style dates (2024-09) detected. Taleo prefers Month-Year text format and may fail to extract employment durations from ISO dates.',
        fix: 'Convert dates to "Sep 2024 – Mar 2026" format throughout.',
      });
    }
    if (s.wordsPerKB < 1 && s.wordCount < 200) {
      out.push({
        code: 'TALEO_LOW_TEXT_DENSITY',
        severity: 'error',
        message: `Very low text-per-kilobyte ratio (${s.wordsPerKB.toFixed(2)} words/kB). The PDF may be image-based or heavily designed; Taleo extracts only embedded text.`,
        fix: 'Re-export the CV from Word/Pages as a text-based PDF, not "Save as Image".',
      });
    }
    if (s.hasSmartQuotes) {
      out.push({
        code: 'TALEO_SMART_QUOTES',
        severity: 'info',
        message: 'Smart quotes (curly quotes) detected. Older Taleo deployments occasionally mangle them in extracted text.',
        fix: 'Use straight quotes if applying to Fortune-500 roles where Taleo is common.',
      });
    }
    return out;
  },

  icims: (s) => {
    const out = [];
    if (s.multiColumnRatio > 0.20) {
      out.push({
        code: 'ICIMS_MULTI_COLUMN',
        severity: 'error',
        message: `iCIMS's parser handles multi-column poorly (${Math.round(s.multiColumnRatio * 100)}% multi-column lines detected). Skills sections in a sidebar typically get merged with the line above them.`,
        fix: 'Single-column layout. If you must keep a "Skills" sidebar, move it to its own section above or below.',
      });
    }
    if (s.headersFound.length < 2) {
      out.push({
        code: 'ICIMS_FEW_HEADERS',
        severity: 'warn',
        message: 'Fewer than 2 standard section headers detected. iCIMS section detection looks for keyword headers; without them it dumps everything into "Description".',
        fix: 'Add at least Experience and Education headers as their own lines.',
      });
    }
    return out;
  },
};

// ---------------------------------------------------------------- exports ----

export async function analyzeCv(path) {
  const parsed = await parse(path);
  const signals = computeSignals(parsed);

  const vendors = {};
  for (const v of VENDORS) {
    vendors[v] = {
      name: VENDOR_NAMES[v],
      findings: VENDOR_RULES[v](signals),
    };
  }

  return {
    path,
    format: parsed.format,
    pages: parsed.pages,
    fileSize: parsed.fileSize,
    wordCount: signals.wordCount,
    vendors,
  };
}

export async function lintForVendor(path, vendor) {
  if (!VENDORS.includes(vendor)) {
    throw new Error(`Unknown vendor: ${vendor}`);
  }
  const parsed = await parse(path);
  const signals = computeSignals(parsed);
  return VENDOR_RULES[vendor](signals);
}

// Exposed for tests.
export const _internals = { computeSignals, VENDOR_RULES, parse };
