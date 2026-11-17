import type { PaymentRequestStatus } from "@cafecr/api-client";

export type PaymentPresentation = "pending" | "paid" | "expired" | "cancelled" | "attention";

export function paymentPresentation(status: PaymentRequestStatus | undefined): PaymentPresentation {
  switch (status) {
    case "Paid": return "paid";
    case "Expired": return "expired";
    case "Cancelled": return "cancelled";
    case "NeedsAttention": return "attention";
    default: return "pending";
  }
}

export function isPaymentTerminal(status: PaymentRequestStatus | undefined): boolean {
  return paymentPresentation(status) !== "pending";
}

/**
 * Where to go when a VietQR payment screen is closed. Money received (Paid, or NeedsAttention where the
 * transfer arrived but needs a manager) → back to the floor for the next customer. Not paid (Expired,
 * Cancelled) → back to the previous screen to try again.
 */
export function afterPaymentClose(status: PaymentRequestStatus | undefined): "floor" | "back" {
  return status === "Paid" || status === "NeedsAttention" ? "floor" : "back";
}

export type AddOnSheetEvent = { type: "drawer-closed" } | { type: "sale-done" } | { type: "qr-closed"; status: PaymentRequestStatus | undefined };

/**
 * Add-on sheet exits: closing the drawer before selling → "cancel" (caller's previous screen); a cash sale
 * confirmed or a paid QR → "done" (caller returns to the floor); an expired/cancelled QR → "retry" (stay on
 * the add-on drawer with the cart).
 */
export function addOnSheetExit(event: AddOnSheetEvent): "cancel" | "done" | "retry" {
  switch (event.type) {
    case "drawer-closed": return "cancel";
    case "sale-done": return "done";
    case "qr-closed": return afterPaymentClose(event.status) === "floor" ? "done" : "retry";
  }
}
