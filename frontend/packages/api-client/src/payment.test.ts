import { describe, expect, it, vi } from "vitest";
import { createFeatureApi } from "./features";
import type { Http } from "./http";

describe("payment API client", () => {
  it("creates check-in and extension payment requests", async () => {
    const request = vi.fn<Http["request"]>()
      .mockResolvedValueOnce({ code: "TTABCDEFGH", amount: 45000, qrUrl: "https://qr.example/tt", expiresAt: "2026-11-10T10:10:00Z" })
      .mockResolvedValueOnce({ code: "TTABCDEFGJ", amount: 40000, qrUrl: "https://qr.example/tt2", expiresAt: "2026-11-10T10:10:00Z" })
      .mockResolvedValueOnce({ id: "session-1" });
    const api = createFeatureApi({ request, download: vi.fn() } as Http);
    const checkIn = { seatId: "seat-1", pricePlanId: "plan-1", customerId: "customer-1" };
    const cashCheckIn = { seatId: "seat-1", pricePlanId: "plan-1", paymentMethod: "Cash" as const };

    await expect(api.payments.createCheckIn(checkIn)).resolves.toMatchObject({ amount: 45000 });
    await expect(api.payments.createExtension("session-1")).resolves.toMatchObject({ amount: 40000 });
    await api.sessions.checkIn(cashCheckIn);

    expect(request.mock.calls).toEqual([
      ["POST", "/api/payments/requests", checkIn],
      ["POST", "/api/sessions/session-1/extend/payment-request", {}],
      ["POST", "/api/sessions", cashCheckIn],
    ]);
  });

  it("checks payment status and cancels using the encoded payment code", async () => {
    const request = vi.fn<Http["request"]>()
      .mockResolvedValueOnce({ code: "TT/A", amount: 45000, status: "Paid", expiresAt: "2026-11-10T10:10:00Z", paidAt: null, billCode: "ABCDEFGH" })
      .mockResolvedValueOnce(undefined);
    const api = createFeatureApi({ request, download: vi.fn() } as Http);

    await expect(api.payments.status("TT/A")).resolves.toMatchObject({ status: "Paid", billCode: "ABCDEFGH" });
    await api.payments.cancel("TT/A");

    expect(request.mock.calls).toEqual([
      ["GET", "/api/payments/TT%2FA"],
      ["DELETE", "/api/payments/requests/TT%2FA"],
    ]);
  });

  it("sells add-ons as a separate bill and looks up add-on bills by code (TASK-029)", async () => {
    const request = vi.fn<Http["request"]>()
      .mockResolvedValueOnce({ items: [], billCode: "ADDON001", billTotal: 7000, pointsAwarded: 0, customerName: null })
      .mockResolvedValueOnce({ id: "session-1", kind: "AddOn", bill: { code: "SEAT0001", total: 40000 }, addOnBills: [{ code: "ADDON001", total: 7000, paymentMethod: "Cash", createdAt: "2026-11-17T10:00:00Z" }] });
    const api = createFeatureApi({ request, download: vi.fn() } as Http);

    const sale = await api.sessions.sellItems("session-1", [{ productId: "noodle", quantity: 1 }]);
    const view = await api.sessions.bill("ADDON001");

    expect([sale.billCode, sale.billTotal]).toEqual(["ADDON001", 7000]);
    expect(view.kind).toBe("AddOn");
    expect(view.bill.total).toBe(40000);
    expect(view.addOnBills?.map((b) => b.code)).toEqual(["ADDON001"]);
    expect(request.mock.calls).toEqual([
      ["POST", "/api/sessions/session-1/items", { items: [{ productId: "noodle", quantity: 1 }] }],
      ["GET", "/api/bills/ADDON001"],
    ]);
  });
});
