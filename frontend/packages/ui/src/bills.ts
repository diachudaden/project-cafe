/** Route of a bill's detail page in the customer app, or null when the row has no bill (redemption, adjustment). */
export function billLink(billCode: string | null | undefined): string | null {
  const code = billCode?.trim();
  return code ? `/bills/${encodeURIComponent(code)}` : null;
}

/** Normalizes a bill code from user input or a `?code=` link: upper case, A–Z/0–9 only, max 8 chars. */
export function normalizeBillCode(raw: string | null | undefined): string {
  return (raw ?? "").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 8);
}
