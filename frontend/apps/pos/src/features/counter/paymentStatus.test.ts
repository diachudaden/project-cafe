import { describe, expect, it } from "vitest";
import { isPaymentTerminal, paymentPresentation } from "./paymentStatus";

describe("payment request presentation", () => {
  it("keeps polling only while the request is pending", () => {
    expect(isPaymentTerminal(undefined)).toBe(false);
    expect(isPaymentTerminal("Pending")).toBe(false);
    expect(isPaymentTerminal("Paid")).toBe(true);
    expect(isPaymentTerminal("Expired")).toBe(true);
    expect(isPaymentTerminal("Cancelled")).toBe(true);
    expect(isPaymentTerminal("NeedsAttention")).toBe(true);
  });

  it("maps settlement and terminal failures to distinct UI states", () => {
    expect(paymentPresentation("Paid")).toBe("paid");
    expect(paymentPresentation("Expired")).toBe("expired");
    expect(paymentPresentation("Cancelled")).toBe("cancelled");
    expect(paymentPresentation("NeedsAttention")).toBe("attention");
    expect(paymentPresentation("Pending")).toBe("pending");
  });
});
