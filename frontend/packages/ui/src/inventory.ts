import type { ItemUnit } from "@cafecr/api-client";

/** Splits base units into the largest packing units first: 45 gói with Hộp=20 → [{Hộp, 2}, {gói, 5}]. */
export function splitUnits(qty: number, baseUnit: string, units: ItemUnit[]): { unitName: string; qty: number }[] {
  let rest = Math.max(0, Math.trunc(qty));
  const parts: { unitName: string; qty: number }[] = [];
  for (const unit of [...units].sort((a, b) => b.factor - a.factor)) {
    const n = Math.floor(rest / unit.factor);
    if (n > 0) {
      parts.push({ unitName: unit.unitName, qty: n });
      rest -= n * unit.factor;
    }
  }
  if (rest > 0 || parts.length === 0) parts.push({ unitName: baseUnit, qty: rest });
  return parts;
}

/** "2 hộp + 5 gói". */
export function unitsText(qty: number, baseUnit: string, units: ItemUnit[]): string {
  if (qty < 0) return `−${unitsText(-qty, baseUnit, units)}`;
  return splitUnits(qty, baseUnit, units).map((p) => `${p.qty} ${p.unitName.toLowerCase()}`).join(" + ");
}

/** Σ qty × factor; unknown unit names count as base units. */
export function toBaseUnits(parts: Record<string, number>, units: ItemUnit[]): number {
  return Object.entries(parts).reduce((sum, [name, n]) => sum + (Number(n) || 0) * (units.find((u) => u.unitName === name)?.factor ?? 1), 0);
}

/** Same rule as the API: above the suggestion, or overfilling par, needs a reason (never blocked). */
export function transferNeedsReason(qty: number, suggested: number, expectedNow: number, par: number): boolean {
  return qty > suggested || expectedNow + qty > par;
}

export type VarianceState = "ok" | "warn" | "over" | "pending";

/** Green within ½ tolerance, amber within tolerance, red beyond it. */
export function varianceState(variance: number | null | undefined, tolerance: number): VarianceState {
  if (variance === null || variance === undefined) return "pending";
  const abs = Math.abs(variance);
  if (abs > tolerance) return "over";
  return abs * 2 > tolerance ? "warn" : "ok";
}

/** ConfirmDialog's primary button: enabled when no reason is required or the trimmed reason is long enough. */
export function canConfirm(requireReason: boolean, reason: string, minLength: number, busy = false): boolean {
  return !busy && (!requireReason || reason.trim().length >= minLength);
}
