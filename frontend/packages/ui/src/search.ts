/**
 * Vietnamese-friendly text helpers: staff often type without accents ("mi", "trung op"),
 * so search compares text with diacritics removed and đ → d.
 */
export function normalizeVi(text: string | null | undefined): string {
  return (text ?? "")
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase()
    .replace(/\s+/g, " ")
    .trim();
}

/** Strips the "[DEMO]" / "[DEMO ONLY]" / "[DEMO DATA]" prefix that seeded records carry. */
export function displayName(name: string | null | undefined): string {
  return (name ?? "").replace(/^\s*\[DEMO(?: ONLY| DATA)?\]\s*/i, "");
}

/**
 * True when every whitespace-separated token of `term` appears in at least one of `fields`
 * (normalized). An empty term matches everything.
 */
export function matchesSearch(term: string, ...fields: (string | null | undefined)[]): boolean {
  const tokens = normalizeVi(term).split(" ").filter(Boolean);
  if (tokens.length === 0) return true;
  const haystack = fields.map((f) => normalizeVi(displayName(f)));
  return tokens.every((t) => haystack.some((h) => h.includes(t)));
}
