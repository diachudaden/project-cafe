import { describe, expect, it } from "vitest";
import { canConfirm, splitUnits, toBaseUnits, transferNeedsReason, unitsText, varianceState } from "./inventory";

const units = [{ unitName: "Thùng", factor: 480 }, { unitName: "Hộp", factor: 20 }];

describe("packing units", () => {
  it("splits base units largest first", () => {
    expect(splitUnits(525, "gói", units)).toEqual([{ unitName: "Thùng", qty: 1 }, { unitName: "Hộp", qty: 2 }, { unitName: "gói", qty: 5 }]);
    expect(unitsText(45, "gói", units)).toBe("2 hộp + 5 gói");
    expect(unitsText(0, "gói", units)).toBe("0 gói");
  });

  it("converts '2 hộp + 5 gói' to base units", () => {
    expect(toBaseUnits({ Hộp: 2, gói: 5 }, units)).toBe(45);
  });
});

describe("rules shared with the API", () => {
  it("needs a reason above the suggestion or over par", () => {
    expect(transferNeedsReason(38, 38, 22, 60)).toBe(false);
    expect(transferNeedsReason(39, 38, 22, 60)).toBe(true);
    expect(transferNeedsReason(1, 0, 60, 60)).toBe(true);
  });

  it("colours the variance chip", () => {
    expect(varianceState(0, 6)).toBe("ok");
    expect(varianceState(-4, 6)).toBe("warn");
    expect(varianceState(-15, 6)).toBe("over");
    expect(varianceState(null, 6)).toBe("pending");
  });
});

describe("ConfirmDialog button", () => {
  it("is disabled until the reason is long enough when required", () => {
    expect(canConfirm(false, "", 10)).toBe(true);
    expect(canConfirm(true, "   ngắn   ", 10)).toBe(false);
    expect(canConfirm(true, "Đổ vỡ 2 gói khi lau kệ", 10)).toBe(true);
    expect(canConfirm(true, "Đổ vỡ 2 gói khi lau kệ", 10, true)).toBe(false); // sending
  });
});
