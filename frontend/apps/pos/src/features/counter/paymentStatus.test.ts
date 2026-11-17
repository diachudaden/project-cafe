import { describe, expect, it } from "vitest";
import { addOnSheetExit, afterPaymentClose, isPaymentTerminal, paymentPresentation } from "./paymentStatus";

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

  it("returns to the floor once money is received and to the previous screen otherwise", () => {
    expect(afterPaymentClose("Paid")).toBe("floor");
    expect(afterPaymentClose("NeedsAttention")).toBe("floor");
    expect(afterPaymentClose("Expired")).toBe("back");
    expect(afterPaymentClose("Cancelled")).toBe("back");
    expect(afterPaymentClose(undefined)).toBe("back");
  });

  it("ends the add-on sheet as done only after a sale, as cancel when the drawer is closed", () => {
    expect(addOnSheetExit({ type: "drawer-closed" })).toBe("cancel");
    expect(addOnSheetExit({ type: "sale-done" })).toBe("done");
    expect(addOnSheetExit({ type: "qr-closed", status: "Paid" })).toBe("done");
    expect(addOnSheetExit({ type: "qr-closed", status: "Expired" })).toBe("retry");
    expect(addOnSheetExit({ type: "qr-closed", status: "Cancelled" })).toBe("retry");
  });
});
