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
