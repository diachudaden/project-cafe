import { describe, expect, it } from "vitest";
import { customerLink, newPaymentNotifications, pointsDeltaText, type FeedItem } from "./notifications";

const n = (id: string, type = "payment.received", readAt: string | null = null): FeedItem =>
  ({ id, type, title: id, body: id, link: `/bills/${id}`, createdAt: "2026-11-14T03:00:00Z", readAt });

describe("newPaymentNotifications", () => {
  it("never replays the first load", () => {
    expect(newPaymentNotifications(undefined, [n("a")])).toEqual([]);
  });

  it("returns new unread payment and points notifications only", () => {
    const previous = [n("a")];
    const next = [n("d", "points.earned"), n("c", "session.auto_released"), n("b"), n("e", "payment.received", "2026-11-14T03:01:00Z"), n("a")];
    expect(newPaymentNotifications(previous, next).map((x) => x.id)).toEqual(["d", "b"]);
  });

  it("returns nothing when the feed did not change", () => {
    const feed = [n("a"), n("b")];
    expect(newPaymentNotifications(feed, feed)).toEqual([]);
  });
});

describe("pointsDeltaText", () => {
  it("formats gains and losses with Vietnamese grouping", () => {
    expect(pointsDeltaText(45, 85)).toBe("+40 điểm");
    expect(pointsDeltaText(100, 1350)).toBe("+1.250 điểm");
    expect(pointsDeltaText(85, 75)).toBe("−10 điểm");
  });

  it("is null when unknown or unchanged", () => {
    expect(pointsDeltaText(undefined, 10)).toBeNull();
    expect(pointsDeltaText(10, 10)).toBeNull();
  });
});

describe("customerLink", () => {
  it("keeps app routes and falls back to /me", () => {
    expect(customerLink("/bills/AB12CD34")).toBe("/bills/AB12CD34");
    expect(customerLink("/")).toBe("/");
    expect(customerLink("/pay")).toBe("/me");
    expect(customerLink("https://evil.example")).toBe("/me");
    expect(customerLink(null)).toBe("/me");
  });
});
