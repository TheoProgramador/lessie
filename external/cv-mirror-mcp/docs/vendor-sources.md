# Vendor sources

Citations for every parsing quirk simulated in `src/lint.mjs`. We cite public, durable sources only — vendor docs, support articles, and open-source ATS clones. Closed-network insights and individual recruiter testimony are explicitly NOT used.

## Workday

| Rule | Source | Notes |
|---|---|---|
| `WORKDAY_MULTI_COLUMN` | Workday Help — "Resume Parsing Best Practices" | Workday's resume parser reads PDFs in document-stream order, which interleaves multi-column layouts. The recommended layout in their docs is single-column. |
| `WORKDAY_QUARTER_DATES` | Workday community forums + Workday Recruiting documentation | Date-format detection covers Month-Year and ISO; non-standard formats fall through. |
| `WORKDAY_CONTACT_MISSING` | Workday Recruiting — Apply with CV behaviour | The auto-populate step requires inline email + phone; values stored only in PDF metadata or headers/footers do not always populate. |

## Greenhouse

| Rule | Source | Notes |
|---|---|---|
| `GREENHOUSE_EMOJI` | Greenhouse "Resume Parsing — formatting tips" support article | Greenhouse's parser strips most emoji codepoints. Surrounding context can be lost when a header reads "Projects 🚀". |
| `GREENHOUSE_FANCY_BULLETS` | Greenhouse formatting tips + community-reported behaviour | Bullet glyphs outside ASCII are sometimes collapsed to spaces, occasionally merging two bullet lines into one. |
| `GREENHOUSE_NONSTANDARD_HEADER` | Greenhouse documentation on resume sections | Section detection looks for keyword headers ("Experience", "Education", etc.). Non-standard headers like "My Story" route content to "Other". |

## Lever

| Rule | Source | Notes |
|---|---|---|
| `LEVER_HEADER_FOOTER` | Lever Help Center — "Resume parsing format guidance" | Lever's parser historically dropped PDF header/footer content. They've improved, but employer/date in headers is still risky. |
| `LEVER_NO_STANDARD_HEADERS` | Lever Help Center | Lever uses header detection to delimit sections; without explicit headers it cannot reliably build the timeline. |

## Taleo (Oracle)

| Rule | Source | Notes |
|---|---|---|
| `TALEO_ISO_DATES` | Oracle Talent Acquisition Cloud — Resume Parsing docs | Date extractor expects Month-Year format. ISO-only CVs may fail to populate the employment-duration fields. |
| `TALEO_LOW_TEXT_DENSITY` | Oracle docs + multiple support tickets | Image-based PDFs (e.g. exported as image from a designer tool) fail Taleo extraction entirely. |
| `TALEO_SMART_QUOTES` | Oracle support forums | Older Taleo deployments (still in production at many Fortune-500 companies as of 2026) occasionally mangle smart quotes during text extraction. |

## iCIMS

| Rule | Source | Notes |
|---|---|---|
| `ICIMS_MULTI_COLUMN` | iCIMS Talent Cloud documentation + support | iCIMS's parser is among the worst at multi-column. Sidebars often merge with the line above. |
| `ICIMS_FEW_HEADERS` | iCIMS resume-formatting guidelines | Section detection is keyword-based; without standard headers, content gets dumped into a single "Description" field. |

---

## Update cadence

ATS vendors update their parsers ~1–2 times per year. We re-validate rules against each vendor's latest docs each quarter. If you spot a behaviour our rules don't match (or no longer match), open an issue with:

1. The vendor + rule code
2. A link to the current vendor doc that contradicts our rule
3. (Optional) A redacted CV that triggers the difference
