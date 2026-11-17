import type { NotificationFeed } from "@cafecr/api-client";

export type FeedItem = NotificationFeed["items"][number];

/** Notification types that mean "your points / bill just changed" for a member. */
export const paymentNotificationTypes = new Set(["payment.received", "points.earned"]);

/**
 * Unread payment notifications in `next` that were not in `previous`. The first load (`previous`
 * undefined) returns nothing, so opening the app never replays old notifications as toasts.
 */
export function newPaymentNotifications(previous: FeedItem[] | undefined, next: FeedItem[]): FeedItem[] {
  if (!previous) return [];
  const seen = new Set(previous.map((n) => n.id));
  return next.filter((n) => !seen.has(n.id) && !n.readAt && paymentNotificationTypes.has(n.type));
}

/** "+45 điểm" / "−10 điểm"; null when nothing changed. */
export function pointsDeltaText(before: number | undefined, after: number | undefined): string | null {
  if (before === undefined || after === undefined || before === after) return null;
  const delta = after - before;
  return `${delta > 0 ? "+" : "−"}${Math.abs(delta).toLocaleString("vi-VN")} điểm`;
}

/** Only links the customer app can open; anything else falls back to the profile page. */
export function customerLink(link: string | null | undefined): string {
  if (!link) return "/me";
  return /^\/(bills\/[A-Za-z0-9]+|me|points|claim)?$/.test(link) ? link : "/me";
}
