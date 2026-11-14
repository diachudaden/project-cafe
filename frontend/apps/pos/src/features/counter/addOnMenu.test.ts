import { describe, expect, it } from "vitest";
import type { ProductView } from "@cafecr/api-client";
import { ApiError } from "@cafecr/api-client";
import { ALL_CATEGORIES, cartLines, changeCart, checkInTotals, errorDiagnostics, filterMenu, menuState } from "./addOnMenu";

const p = (id: string, name: string, category: string): ProductView =>
  ({ id, branchId: null, name, category, price: 10000, isActive: true, sortOrder: 0 }) as ProductView;
const menu = [p("1", "[DEMO] Mì ly", "Đồ ăn"), p("2", "Trứng ốp la", "Đồ ăn"), p("3", "Trà đá", "Đồ uống")];
const names = (items: ProductView[]) => items.map((i) => i.id);

describe("filterMenu", () => {
  it("filters by category when there is no search term", () => {
    expect(names(filterMenu(menu, "", "Đồ uống"))).toEqual(["3"]);
    expect(names(filterMenu(menu, "", ALL_CATEGORIES))).toEqual(["1", "2", "3"]);
  });

  it("searches across all categories, ignoring the selected tab", () => {
    expect(names(filterMenu(menu, "mi", "Đồ uống"))).toEqual(["1"]);
    expect(names(filterMenu(menu, "trung op", "Đồ uống"))).toEqual(["2"]);
    expect(names(filterMenu(menu, "uong", "Đồ ăn"))).toEqual(["3"]);
  });
});

describe("menuState", () => {
  const base = { isLoading: false, error: null, products: menu, term: "", category: ALL_CATEGORIES };

  it("shows loading first", () => {
    expect(menuState({ ...base, isLoading: true, products: undefined }).kind).toBe("loading");
  });

  it("never reports an API error as an empty menu", () => {
    const error = new ApiError(500, "Đã có lỗi máy chủ.");
    expect(menuState({ ...base, error, products: undefined })).toEqual({ kind: "error", message: "Đã có lỗi máy chủ.", diagnostics: "HTTP 500" });
  });

  it("shows the HTTP status and traceId of a server error (TASK-026)", () => {
    const error = new ApiError(500, "Yêu cầu không thành công.", {}, [], "An error occurred while processing your request.", "00-abc-def-00");
    expect(errorDiagnostics(error)).toBe("HTTP 500 · traceId 00-abc-def-00");
    expect(errorDiagnostics(new ApiError(0, "Không kết nối được máy chủ."))).toBeNull();
    expect(errorDiagnostics(new Error("x"))).toBeNull();
  });

  it("distinguishes an empty menu from a search with no result", () => {
    expect(menuState({ ...base, products: [] }).kind).toBe("empty");
    expect(menuState({ ...base, term: "pho" })).toEqual({ kind: "noResult", term: "pho" });
  });

  it("lists matches and flags searching", () => {
    const state = menuState({ ...base, term: "tra" });
    expect(state.kind === "list" && state.searching && names(state.items)).toEqual(["3"]);
  });
});

describe("check-in with add-ons (TASK-026)", () => {
  const priced = [{ ...p("1", "Mì ly", "Đồ ăn"), price: 15000 }, { ...p("2", "Trứng", "Đồ ăn"), price: 8000 }];

  it("adds the plan price and the add-on subtotal into one total", () => {
    const lines = cartLines({ "1": 2, "2": 1 }, priced);
    expect(checkInTotals(45000, lines)).toEqual({ plan: 45000, addOns: 38000, count: 3, total: 83000 });
  });

  it("is just the plan price without add-ons, and 0 before a plan is chosen", () => {
    expect(checkInTotals(45000, [])).toEqual({ plan: 45000, addOns: 0, count: 0, total: 45000 });
    expect(checkInTotals(undefined, cartLines({ "1": 1 }, priced)).total).toBe(15000);
  });

  it("drops zero quantities and products no longer on the menu", () => {
    expect(cartLines({ "1": 0, "2": 1, gone: 3 }, priced).map((l) => l.productId)).toEqual(["2"]);
  });

  it("clamps quantities to 0–99", () => {
    expect(changeCart({ "1": 99 }, "1", 1)["1"]).toBe(99);
    expect(changeCart({}, "1", -1)["1"]).toBe(0);
  });
});
