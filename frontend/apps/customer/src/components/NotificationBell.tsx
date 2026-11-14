import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { App, Badge, Button, Drawer, Empty, List, Typography, theme } from "antd";
import { customerLink, dateTime, newPaymentNotifications, type FeedItem } from "@cafecr/ui";
import { api } from "../api";
import { usePageVisible } from "./usePageVisible";

export const FEED_KEY = ["my-notifications"];

/**
 * Bell + drawer for the signed-in customer. Polls the feed (10 s visible / 60 s hidden); a NEW unread
 * payment or points notification shows a toast (and a browser notification when the tab is hidden and
 * permission was granted) and refreshes points, bills and the current session right away.
 */
export function NotificationBell() {
  const { notification } = App.useApp();
  const { token } = theme.useToken();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const visible = usePageVisible();
  const [open, setOpen] = useState(false);
  const previous = useRef<FeedItem[] | undefined>(undefined);

  const feed = useQuery({
    queryKey: FEED_KEY,
    queryFn: api.notifications.feed,
    refetchInterval: visible ? 10_000 : 60_000,
    refetchIntervalInBackground: true,
  });
  const markRead = useMutation({
    mutationFn: (ids?: string[]) => api.notifications.markRead(ids),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: FEED_KEY }),
  });

  // Coming back to the tab: check right away instead of waiting for the slow interval.
  useEffect(() => {
    if (visible) void feed.refetch();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible]);

  useEffect(() => {
    if (!feed.data) return;
    const fresh = newPaymentNotifications(previous.current, feed.data.items);
    previous.current = feed.data.items;
    if (fresh.length === 0) return;

    for (const queryKey of [["points"], ["my-bills"], ["my-session"], ["my-bill"]]) void queryClient.invalidateQueries({ queryKey });
    for (const n of fresh) {
      const link = customerLink(n.link);
      notification.success({
        key: n.id,
        message: n.title,
        description: n.body,
        placement: "top",
        duration: 6,
        onClick: () => { notification.destroy(n.id); markRead.mutate([n.id]); navigate(link); },
      });
      if (document.visibilityState === "hidden" && "Notification" in window && Notification.permission === "granted") {
        try {
          const browser = new Notification(n.title, { body: n.body, tag: n.id });
          browser.onclick = () => { window.focus(); markRead.mutate([n.id]); navigate(link); browser.close(); };
        } catch {
          // Some mobile browsers only allow notifications from a service worker; the in-app toast still shows.
        }
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [feed.data]);

  const items = feed.data?.items ?? [];
  const openItem = (n: FeedItem) => {
    if (!n.readAt) markRead.mutate([n.id]);
    setOpen(false);
    navigate(customerLink(n.link));
  };

  return (
    <>
      <Badge count={feed.data?.unread ?? 0} size="small" offset={[-4, 4]}>
        <Button shape="circle" aria-label="Thông báo" onClick={() => setOpen(true)}>🔔</Button>
      </Badge>
      <Drawer open={open} onClose={() => setOpen(false)} placement="right" width={Math.min(420, window.innerWidth)} title="Thông báo"
        extra={(feed.data?.unread ?? 0) > 0 && <Button size="small" onClick={() => markRead.mutate(undefined)}>Đánh dấu đã đọc</Button>}>
        <List
          dataSource={items}
          locale={{ emptyText: <Empty description="Chưa có thông báo." /> }}
          renderItem={(n) => (
            <List.Item onClick={() => openItem(n)} style={{ cursor: "pointer", background: n.readAt ? undefined : token.colorPrimaryBg, paddingInline: 8, borderRadius: 8 }}>
              <List.Item.Meta
                title={<span style={{ fontWeight: n.readAt ? 400 : 600 }}>{n.title}</span>}
                description={<>
                  <div>{n.body}</div>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>{dateTime(n.createdAt)}</Typography.Text>
                </>}
              />
            </List.Item>
          )}
        />
      </Drawer>
    </>
  );
}
