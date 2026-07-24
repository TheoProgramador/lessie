/**
 * Tests for the lint engine. Run with: node test/lint.test.mjs
 *
 * Synthetic test fixtures only — no real CVs in the repo.
 * Each test exercises one vendor rule.
 */
import { strict as assert } from 'node:assert';
import { _internals, VENDORS } from '../src/lint.mjs';

const { computeSignals, VENDOR_RULES } = _internals;

let pass = 0;
let fail = 0;

function test(name, fn) {
  try {
    fn();
    console.log(`  ok  ${name}`);
    pass++;
  } catch (e) {
    console.error(`  FAIL ${name}: ${e.message}`);
    fail++;
  }
}

// Helper: build a fake "parsed" object as if returned by parse()
function fakeParsed(text, opts = {}) {
  return {
    format: opts.format || 'pdf',
    fileSize: opts.fileSize ?? 50_000,
    pages: opts.pages ?? 2,
    text,
    info: {},
  };
}

console.log('VENDORS exposed:');
test('exports the 5 expected vendors', () => {
  assert.deepEqual(VENDORS, ['workday', 'greenhouse', 'lever', 'taleo', 'icims']);
});

console.log('\nSignals:');
test('computeSignals counts words', () => {
  const p = fakeParsed('Hello world this is a CV');
  const s = computeSignals(p);
  assert.equal(s.wordCount, 6);
});

test('computeSignals detects multi-column lines', () => {
  const p = fakeParsed('Skills           Experience\nReact            Acme Corp\nNode             Beta Inc');
  const s = computeSignals(p);
  assert.ok(s.multiColumnLines >= 2, `expected >=2 multi-column lines, got ${s.multiColumnLines}`);
});

test('computeSignals detects emoji', () => {
  const s = computeSignals(fakeParsed('Projects 🚀 React App'));
  assert.equal(s.hasEmoji, true);
});

test('computeSignals detects smart quotes', () => {
  const s = computeSignals(fakeParsed('I’m a software engineer with “strong” skills'));
  assert.equal(s.hasSmartQuotes, true);
});

test('computeSignals detects standard headers', () => {
  const s = computeSignals(fakeParsed('Experience\nAcme Corp\n\nEducation\nMIT'));
  assert.ok(s.headersFound.includes('experience'));
  assert.ok(s.headersFound.includes('education'));
});

test('computeSignals catches non-standard headers', () => {
  const s = computeSignals(fakeParsed('My Story\nI grew up coding...'));
  assert.equal(s.hasNonStandardHeaders, true);
});

test('computeSignals detects email and phone', () => {
  const s = computeSignals(fakeParsed('jane@example.com\n+44 7900 123456'));
  assert.equal(s.hasEmail, true);
  assert.equal(s.hasPhone, true);
});

console.log('\nWorkday rules:');
test('flags multi-column layout as error', () => {
  const s = computeSignals(fakeParsed(
    Array.from({ length: 20 }, () => 'Skills           Experience').join('\n')
  ));
  const findings = VENDOR_RULES.workday(s);
  const f = findings.find((x) => x.code === 'WORKDAY_MULTI_COLUMN');
  assert.ok(f, 'expected WORKDAY_MULTI_COLUMN');
  assert.equal(f.severity, 'error');
});

test('flags missing contact info as error', () => {
  const s = computeSignals(fakeParsed('Just a CV with no contact details'));
  const findings = VENDOR_RULES.workday(s);
  assert.ok(findings.find((f) => f.code === 'WORKDAY_CONTACT_MISSING'));
});

console.log('\nGreenhouse rules:');
test('flags emoji as warn', () => {
  const s = computeSignals(fakeParsed('Projects 🚀\nReact App'));
  const findings = VENDOR_RULES.greenhouse(s);
  const f = findings.find((x) => x.code === 'GREENHOUSE_EMOJI');
  assert.ok(f);
  assert.equal(f.severity, 'warn');
});

test('flags non-standard headers', () => {
  const s = computeSignals(fakeParsed('My Story\nGrew up in...'));
  const findings = VENDOR_RULES.greenhouse(s);
  assert.ok(findings.find((f) => f.code === 'GREENHOUSE_NONSTANDARD_HEADER'));
});

console.log('\nLever rules:');
test('flags missing standard headers as error', () => {
  const s = computeSignals(fakeParsed('Just unstructured text'));
  const findings = VENDOR_RULES.lever(s);
  assert.ok(findings.find((f) => f.code === 'LEVER_NO_STANDARD_HEADERS'));
});

test('flags page-number footers as warn', () => {
  const s = computeSignals(fakeParsed('Experience\nAcme Corp\nPage 1 of 2'));
  const findings = VENDOR_RULES.lever(s);
  assert.ok(findings.find((f) => f.code === 'LEVER_HEADER_FOOTER'));
});

console.log('\nTaleo rules:');
test('flags ISO dates over month-year dates', () => {
  const s = computeSignals(fakeParsed('Acme: 2024-09 to 2026-03\nBeta: 2022-01 to 2024-08'));
  const findings = VENDOR_RULES.taleo(s);
  assert.ok(findings.find((f) => f.code === 'TALEO_ISO_DATES'));
});

test('flags low text density', () => {
  const s = computeSignals(fakeParsed('Hi', { fileSize: 500_000 }));
  const findings = VENDOR_RULES.taleo(s);
  assert.ok(findings.find((f) => f.code === 'TALEO_LOW_TEXT_DENSITY'));
});

console.log('\niCIMS rules:');
test('flags heavy multi-column as error', () => {
  const s = computeSignals(fakeParsed(
    Array.from({ length: 20 }, () => 'Skills           Experience').join('\n')
  ));
  const findings = VENDOR_RULES.icims(s);
  const f = findings.find((x) => x.code === 'ICIMS_MULTI_COLUMN');
  assert.ok(f);
  assert.equal(f.severity, 'error');
});

test('flags missing headers as warn', () => {
  const s = computeSignals(fakeParsed('Some text without headers'));
  const findings = VENDOR_RULES.icims(s);
  assert.ok(findings.find((f) => f.code === 'ICIMS_FEW_HEADERS'));
});

console.log('\nClean CV (synthetic baseline):');
test('a clean single-column CV produces no errors across all 5 vendors', () => {
  const cleanCv = `Jane Doe
jane@example.com
+44 7900 123456

Summary
Senior software engineer with 8 years of experience.

Experience
Acme Corp - Senior Engineer
Sep 2024 - Mar 2026
- Led migration to TypeScript across 200k LOC
- Reduced build time from 4 minutes to 1 minute

Beta Inc - Software Engineer
Jan 2022 - Aug 2024
- Built the billing service from scratch
- Owned on-call rotation for payments

Education
MIT - BSc Computer Science
Sep 2014 - Jun 2018

Skills
TypeScript, React, Node.js, PostgreSQL, AWS`;

  const s = computeSignals(fakeParsed(cleanCv));
  for (const v of VENDORS) {
    const findings = VENDOR_RULES[v](s);
    const errors = findings.filter((f) => f.severity === 'error');
    assert.equal(errors.length, 0, `vendor ${v}: expected 0 errors but got ${errors.length}: ${JSON.stringify(errors, null, 2)}`);
  }
});

console.log(`\n${pass} passed, ${fail} failed`);
if (fail > 0) process.exit(1);
