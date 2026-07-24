import { describe, expect, it } from "vitest";
import { adapters, getAdapter } from "../../src/adapters/registry";

describe("adapter registry", () => {
  it("exposes greenhouse, lever, ashby, workday, usajobs", () => {
    const names = adapters.map((a) => a.name).sort();
    expect(names).toEqual(["ashby", "greenhouse", "lever", "usajobs", "workday"]);
  });

  it("getAdapter returns the adapter by name", () => {
    expect(getAdapter("greenhouse")?.name).toBe("greenhouse");
    expect(getAdapter("workday")?.name).toBe("workday");
    expect(getAdapter("usajobs")?.name).toBe("usajobs");
    expect(getAdapter("nope")).toBeUndefined();
  });
});
