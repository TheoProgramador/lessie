import { describe, expect, it } from "vitest";
import { parsePayRangeText, type Compensation } from "../src/compensation";

describe("parsePayRangeText", () => {
  it("parses 'k' shorthand USD ranges", () => {
    expect(parsePayRangeText("$120k - $160k")).toEqual<Compensation>({
      min: 120000,
      max: 160000,
      currency: "USD",
      interval: "yearly",
    });
  });

  it("parses comma-separated USD ranges", () => {
    expect(parsePayRangeText("$120,000 - $160,000 USD")).toEqual<Compensation>({
      min: 120000,
      max: 160000,
      currency: "USD",
      interval: "yearly",
    });
  });

  it("parses GBP with £ symbol", () => {
    expect(parsePayRangeText("Up to £80,000")).toEqual<Compensation>({
      max: 80000,
      currency: "GBP",
      interval: "yearly",
    });
  });

  it("parses EUR with € symbol", () => {
    expect(parsePayRangeText("€60,000 – €90,000")).toEqual<Compensation>({
      min: 60000,
      max: 90000,
      currency: "EUR",
      interval: "yearly",
    });
  });

  it("detects hourly intervals", () => {
    expect(parsePayRangeText("$25 - $35 / hour")).toEqual<Compensation>({
      min: 25,
      max: 35,
      currency: "USD",
      interval: "hourly",
    });
  });

  it("returns null for ambiguous or unparseable strings", () => {
    expect(parsePayRangeText("competitive salary")).toBeNull();
    expect(parsePayRangeText("")).toBeNull();
    expect(parsePayRangeText("DOE")).toBeNull();
  });

  it("handles single-value compensation as min only", () => {
    expect(parsePayRangeText("$150,000")).toEqual<Compensation>({
      min: 150000,
      currency: "USD",
      interval: "yearly",
    });
  });
});
