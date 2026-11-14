import { describe, expect, it } from "vitest";
import type { ProductView } from "@cafecr/api-client";
import { ApiError } from "@cafecr/api-client";
import { ALL_CATEGORIES, errorDiagnostics, filterMenu, menuState } from "./addOnMenu";

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
