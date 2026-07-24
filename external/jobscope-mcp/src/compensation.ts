export interface Compensation {
  min?: number;
  max?: number;
  currency?: string;
  interval?: "yearly" | "monthly" | "hourly";
}

const CURRENCY_SYMBOLS: Record<string, string> = {
  $: "USD",
  "€": "EUR",
  "£": "GBP",
  "¥": "JPY",
  "₹": "INR",
};

const CURRENCY_CODES = new Set(["USD", "EUR", "GBP", "JPY", "INR", "CAD", "AUD", "CHF"]);

function detectCurrency(text: string): string | undefined {
  for (const sym of Object.keys(CURRENCY_SYMBOLS)) {
    if (text.includes(sym)) return CURRENCY_SYMBOLS[sym];
  }
  for (const code of CURRENCY_CODES) {
    if (text.toUpperCase().includes(code)) return code;
  }
  return undefined;
}

function detectInterval(text: string): "yearly" | "monthly" | "hourly" {
  const lower = text.toLowerCase();
  if (lower.match(/(\bper\s*hour\b|\bhourly\b|\/\s*hour\b|\/hr\b)/)) return "hourly";
  if (lower.match(/(\bper\s*month\b|\bmonthly\b|\/\s*month\b)/)) return "monthly";
  return "yearly";
}

function toNumber(raw: string): number | null {
  const cleaned = raw.replace(/[,\s]/g, "").toLowerCase();
  const kMatch = cleaned.match(/^(\d+(?:\.\d+)?)k$/);
  if (kMatch) return Math.round(parseFloat(kMatch[1]) * 1000);
  const mMatch = cleaned.match(/^(\d+(?:\.\d+)?)m$/);
  if (mMatch) return Math.round(parseFloat(mMatch[1]) * 1000000);
  const plain = cleaned.match(/^\d+(?:\.\d+)?$/);
  if (plain) return parseFloat(plain[0]);
  return null;
}

export function parsePayRangeText(text: string): Compensation | null {
  if (!text) return null;
  const lower = text.toLowerCase();
  if (lower.includes("competitive") || lower.includes("doe")) return null;

  const currency = detectCurrency(text);
  const interval = detectInterval(text);

  const numberPattern = /(?:[$€£¥₹])?\s*([\d,.]+\s*[km]?)/gi;
  const matches = [...text.matchAll(numberPattern)]
    .map((m) => toNumber(m[1].trim()))
    .filter((n): n is number => n !== null && n > 0);

  if (matches.length === 0) return null;

  const upToMatch = lower.match(/\bup\s*to\b/);
  if (upToMatch && matches.length === 1) {
    return { max: matches[0], currency, interval };
  }
  if (matches.length === 1) {
    return { min: matches[0], currency, interval };
  }
  const [min, max] =
    matches.length >= 2
      ? [Math.min(matches[0], matches[1]), Math.max(matches[0], matches[1])]
      : [matches[0], matches[0]];
  return { min, max, currency, interval };
}
