import { describe, expect, it } from "vitest";
import { displayName, matchesSearch, normalizeVi } from "./search";

const menu = [
  { name: "[DEMO] Mì ly", category: "Đồ ăn" },
  { name: "Trứng ốp la", category: "Đồ ăn" },
  { name: "Bánh mì", category: "Đồ ăn" },
  { name: "[DEMO ONLY] Trà đá", category: "Đồ uống" },
  { name: "Cà phê sữa đá", category: "Đồ uống" },
];
const find = (term: string) => menu.filter((p) => matchesSearch(term, p.name, p.category)).map((p) => displayName(p.name));

describe("normalizeVi", () => {
  it("removes diacritics, maps đ/Đ to d and collapses spaces", () => {
    expect(normalizeVi("Trứng  ốp   LA ")).toBe("trung op la");
    expect(normalizeVi("Đồ uống")).toBe("do uong");
    expect(normalizeVi("đá")).toBe("da");
    expect(normalizeVi(null)).toBe("");
  });
});

describe("displayName", () => {
  it("strips demo prefixes only at the start", () => {
    expect(displayName("[DEMO] Mì ly")).toBe("Mì ly");
    expect(displayName("[DEMO ONLY] Gói 4 tiếng")).toBe("Gói 4 tiếng");
    expect(displayName("[DEMO DATA] Nguyễn Văn An")).toBe("Nguyễn Văn An");
    expect(displayName("Mì [DEMO]")).toBe("Mì [DEMO]");
  });
});

describe("matchesSearch", () => {
  it("matches without accents", () => {
    expect(find("mi")).toEqual(["Mì ly", "Bánh mì"]);
    expect(find("tra da")).toEqual(["Trà đá"]);
  });

  it("requires every token, in any order", () => {
    expect(find("trung op")).toEqual(["Trứng ốp la"]);
    expect(find("op trung")).toEqual(["Trứng ốp la"]);
    expect(find("mi banh")).toEqual(["Bánh mì"]);
  });

  it("matches the category too", () => {
    expect(find("uong")).toEqual(["Trà đá", "Cà phê sữa đá"]);
    expect(find("uong sua")).toEqual(["Cà phê sữa đá"]);
  });

  it("ignores the demo prefix and matches everything on an empty term", () => {
    expect(find("demo")).toEqual([]);
    expect(find("   ")).toHaveLength(menu.length);
  });
});
