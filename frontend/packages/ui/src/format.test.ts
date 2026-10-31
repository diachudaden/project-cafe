import { describe, expect, it } from "vitest";
import { addDays, minutesLeftText, vnd, vnIso, weekStart } from "./format";

describe("format", () => {
  it("formats money like 45.000đ", () => {
    expect(vnd(45000)).toBe("45.000đ");
    expect(vnd(1250000)).toBe("1.250.000đ");
  });

  it("finds the Monday of a week and builds +07:00 timestamps", () => {
    expect(weekStart("2026-11-08")).toBe("2026-11-02");
    expect(weekStart("2026-11-02")).toBe("2026-11-02");
    expect(addDays("2026-10-31", 1)).toBe("2026-11-01");
    expect(vnIso("2026-11-02", "08:00")).toBe("2026-11-02T08:00:00+07:00");
  });

  it("describes remaining time in Vietnamese", () => {
    expect(minutesLeftText(-5)).toBe("Quá giờ 5 phút");
    expect(minutesLeftText(14)).toBe("Còn 14 phút");
    expect(minutesLeftText(130)).toBe("Còn 2 giờ 10 phút");
  });
});
