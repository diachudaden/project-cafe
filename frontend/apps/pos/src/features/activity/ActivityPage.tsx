import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Card, List, Typography } from "antd";
import { dateTime } from "@cafecr/ui";
import { api } from "../../api";

export function ActivityPage() {
  const queryClient = useQueryClient();
  const feed = useQuery({ queryKey: ["notifications"], queryFn: api.notifications.feed });

  // Opening the feed marks everything as read.
  useEffect(() => {
    if (feed.data?.unread) {
      void api.notifications.markRead().then(() => queryClient.invalidateQueries({ queryKey: ["notifications"] }));
    }
  }, [feed.data?.unread, queryClient]);

  return (
    <Card title="Hoạt động">
      <List dataSource={feed.data?.items} locale={{ emptyText: "Chưa có hoạt động." }}
        renderItem={(n) => (
          <List.Item style={{ opacity: n.readAt ? 0.65 : 1 }}>
            <List.Item.Meta title={n.title} description={<>{n.body}<br /><Typography.Text type="secondary">{dateTime(n.createdAt)}</Typography.Text></>} />
          </List.Item>
        )} />
    </Card>
  );
}
