import { describe, expect, it } from "vitest";
import { receiptFileName } from "./receipt";

describe("receiptFileName", () => {
  it("matches the API's attachment name and strips unsafe characters", () => {
    expect(receiptFileName("QW12ER34")).toBe("bill-QW12ER34.pdf");
    expect(receiptFileName("../QW12")).toBe("bill-QW12.pdf");
  });
});
