import { describe, expect, it } from "vitest";
import { billLink, normalizeBillCode } from "./bills";

describe("billLink", () => {
  it("links a points-history row with a bill code to its bill page", () => {
    expect(billLink("QW12ER34")).toBe("/bills/QW12ER34");
  });

  it("leaves rows without a bill non-clickable", () => {
    expect(billLink(null)).toBeNull();
    expect(billLink(undefined)).toBeNull();
    expect(billLink("  ")).toBeNull();
  });
});

describe("normalizeBillCode", () => {
  it("upper-cases, strips separators and caps at 8 characters", () => {
    expect(normalizeBillCode("qw12-er34xyz")).toBe("QW12ER34");
    expect(normalizeBillCode(null)).toBe("");
  });
});
