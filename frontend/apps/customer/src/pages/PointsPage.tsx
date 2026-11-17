import { useQuery } from "@tanstack/react-query";
import { Card, List, Progress, Statistic, Tag, Typography } from "antd";
import { dateTime, tierLabels, vnd } from "@cafecr/ui";
import { api } from "../api";
import { PointsDeltaBadge, useAnimatedPoints } from "../components/PointsDelta";
import { BillRow } from "../components/BillRow";

const thresholds: Record<string, number> = { Silver: 500_000, Gold: 2_000_000 };

export function PointsPage() {
  const points = useQuery({ queryKey: ["points"], queryFn: api.loyalty.points });
  const p = points.data;
  const { shown, delta } = useAnimatedPoints(p?.points);
  const tier = p ? tierLabels[p.tier] ?? { label: p.tier, color: "#ad6800" } : null;
  const progress = p?.nextTier ? Math.min(100, Math.round((p.totalSpent / thresholds[p.nextTier]) * 100)) : 100;

  return (
    <div style={{ padding: 16 }}>
      <Card>
        <Statistic title="Điểm hiện có" value={shown} suffix={<PointsDeltaBadge delta={delta} />} />
        {tier && <Tag color={tier.color} style={{ marginTop: 8, fontSize: 14 }}>Hạng {tier.label}</Tag>}
        {p && (
          <div style={{ marginTop: 12 }}>
            <Progress percent={progress} strokeColor="#8b5a2b" />
            <Typography.Text type="secondary">
              {p.nextTier ? `Chi thêm ${vnd(p.amountToNextTier)} để lên hạng ${tierLabels[p.nextTier].label}.` : "Bạn đang ở hạng cao nhất."} Tổng chi tiêu: {vnd(p.totalSpent)}
            </Typography.Text>
          </div>
        )}
      </Card>
      <Card title="Lịch sử điểm" style={{ marginTop: 16 }}>
        <List dataSource={p?.history} locale={{ emptyText: "Chưa có giao dịch điểm." }}
          renderItem={(h) => (
            <BillRow billCode={h.billCode} extra={<b style={{ color: h.points >= 0 ? "#389e0d" : "#cf1322" }}>{h.points >= 0 ? "+" : ""}{h.points}</b>}>
              <List.Item.Meta title={h.reason} description={h.billCode ? `${dateTime(h.createdAt)} · Bill ${h.billCode}` : dateTime(h.createdAt)} />
            </BillRow>
          )} />
      </Card>
    </div>
  );
}
