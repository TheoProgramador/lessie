import { describe, expect, it } from "vitest";
import {
  NotFoundError,
  RateLimitError,
  NetworkError,
  ParseError,
  isKnownError,
} from "../src/errors";

describe("errors", () => {
  it("identifies known errors", () => {
    expect(isKnownError(new NotFoundError("job 123 not found"))).toBe(true);
    expect(isKnownError(new RateLimitError("too many"))).toBe(true);
    expect(isKnownError(new NetworkError("timeout"))).toBe(true);
    expect(isKnownError(new ParseError("bad json"))).toBe(true);
  });

  it("does not identify generic errors", () => {
    expect(isKnownError(new Error("???"))).toBe(false);
    expect(isKnownError("string")).toBe(false);
  });

  it("preserves the message", () => {
    expect(new NotFoundError("no").message).toBe("no");
  });
});
