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

/** Product id → quantity; kept by id so it survives search and category changes. */
export type Cart = Record<string, number>;

export interface CartLine { productId: string; quantity: number; name: string; price: number; amount: number }

/** Lines with a quantity for products still on the menu, at menu prices (display only: the server re-prices). */
export function cartLines(cart: Cart, products: ProductView[] | undefined): CartLine[] {
  const byId = new Map((products ?? []).map((p) => [p.id, p]));
  return Object.entries(cart).flatMap(([productId, quantity]) => {
    const p = byId.get(productId);
    return p && quantity > 0 ? [{ productId, quantity, name: p.name, price: p.price, amount: p.price * quantity }] : [];
  });
}

/** Cart after +delta on one product, clamped to 0–99 (the API limit). */
export function changeCart(cart: Cart, productId: string, delta: number): Cart {
  return { ...cart, [productId]: Math.min(99, Math.max(0, (cart[productId] ?? 0) + delta)) };
}

/** Check-in footer: plan price + add-ons subtotal = grand total (one payment). */
export function checkInTotals(planPrice: number | undefined, lines: CartLine[]): { plan: number; addOns: number; count: number; total: number } {
  const plan = planPrice ?? 0;
  const addOns = lines.reduce((sum, l) => sum + l.amount, 0);
  return { plan, addOns, count: lines.reduce((sum, l) => sum + l.quantity, 0), total: plan + addOns };
}
