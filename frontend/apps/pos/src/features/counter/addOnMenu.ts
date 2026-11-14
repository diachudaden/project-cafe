import { ApiError, type ProductView } from "@cafecr/api-client";
import { errorText, matchesSearch } from "@cafecr/ui";

export const ALL_CATEGORIES = "__all__";

/**
 * Items to show. While a search term is present the category tab is ignored, so a match in another
 * category is never hidden; the term matches the display name and the category, accent-insensitive.
 */
export function filterMenu(products: ProductView[], term: string, category: string): ProductView[] {
  if (term.trim()) return products.filter((p) => matchesSearch(term, p.name, p.category));
  return category === ALL_CATEGORIES ? products : products.filter((p) => p.category === category);
}

export type MenuState =
  | { kind: "loading" }
  | { kind: "error"; message: string; diagnostics: string | null }
  | { kind: "empty" }
  | { kind: "noResult"; term: string }
  | { kind: "list"; items: ProductView[]; searching: boolean };

/** One state per situation, so an API error is never shown as "no items". */
export function menuState(input: { isLoading: boolean; error: unknown; products: ProductView[] | undefined; term: string; category: string }): MenuState {
  if (input.isLoading) return { kind: "loading" };
  if (input.error) return { kind: "error", message: errorText(input.error), diagnostics: errorDiagnostics(input.error) };
  const products = input.products ?? [];
  if (products.length === 0) return { kind: "empty" };
  const items = filterMenu(products, input.term, input.category);
  const searching = !!input.term.trim();
  if (items.length === 0) return searching ? { kind: "noResult", term: input.term.trim() } : { kind: "empty" };
  return { kind: "list", items, searching };
}

/** "HTTP 500 · traceId 00-…" so staff can send the exact server log reference; null for network/client errors. */
export function errorDiagnostics(error: unknown): string | null {
  if (!(error instanceof ApiError) || error.status === 0) return null;
  return error.traceId ? `HTTP ${error.status} · traceId ${error.traceId}` : `HTTP ${error.status}`;
}
