import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Card, Descriptions, Divider, List, Skeleton, Tag, Typography } from "antd";
import { closeReasonLabels, dateTime, displayName, errorText, paymentLabels, time, vnd } from "@cafecr/ui";
import { api } from "../api";

/** One bill of the signed-in member: what was paid, how, and the points it earned. */
export function BillPage() {
  const { code = "" } = useParams();
  const navigate = useNavigate();
  const bill = useQuery({ queryKey: ["my-bill", code], queryFn: () => api.loyalty.bill(code), refetchInterval: 30_000 });
  const b = bill.data;

  return (
    <div style={{ padding: 16 }}>
      <Button type="link" onClick={() => navigate("/me")} style={{ paddingInline: 0 }}>← Hóa đơn của tôi</Button>
      {bill.isLoading && <Skeleton active />}
      {bill.isError && (
        <Alert type="error" showIcon message="Không xem được hóa đơn" description={errorText(bill.error)}
          action={<Button onClick={() => void bill.refetch()}>Thử lại</Button>} />
      )}
      {b && (
        <>
          <Card>
            <Typography.Text type="secondary">Mã bill</Typography.Text>
            <Typography.Title level={2} copyable style={{ letterSpacing: 4, margin: 0 }}>{b.code}</Typography.Title>
            <Typography.Title level={3} style={{ margin: "8px 0 0" }}>{vnd(b.total)}</Typography.Title>
            <Typography.Text type="secondary">{b.branchName} · {b.seatCode} · {dateTime(b.createdAt)}</Typography.Text>
            <div style={{ marginTop: 8 }}>
              {b.sessionStatus === "Active" ? <Tag color="green">Đang ngồi</Tag> : <Tag>Đã kết thúc</Tag>}
              {b.closeReason && <Tag color={closeReasonLabels[b.closeReason].color}>{closeReasonLabels[b.closeReason].label}</Tag>}
            </div>
          </Card>

          <Card title="Lượt ngồi" style={{ marginTop: 16 }}>
            <Descriptions column={1} size="small" items={[
              { label: "Gói", children: displayName(b.planName) },
              { label: "Thời gian", children: `${time(b.startAt)} → ${time(b.endAt)}` },
              { label: "Gia hạn", children: b.extensions.length ? `${b.extensions.length} lần` : "Không" },
            ]} />
            {b.extensions.length > 0 && (
              <List size="small" dataSource={b.extensions} renderItem={(e) => (
                <List.Item>
                  <div style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: 12 }}>
                    <span>+{Math.round((e.addedMinutes / 60) * 10) / 10} giờ · {dateTime(e.createdAt)}</span>
                    <span>{vnd(e.price)}</span>
                  </div>
                </List.Item>
              )} />
            )}
          </Card>

          {b.items.length > 0 && (
            <Card title="Đồ thêm" style={{ marginTop: 16 }}>
              <List size="small" dataSource={b.items} renderItem={(i) => (
                <List.Item>
                  <div style={{ display: "flex", justifyContent: "space-between", width: "100%", gap: 12 }}>
                    <span>{i.quantity} × {displayName(i.name)} <Typography.Text type="secondary">({paymentLabels[i.paymentMethod]})</Typography.Text></span>
                    <span>{vnd(i.amount)}</span>
                  </div>
                </List.Item>
              )} />
            </Card>
          )}

          <Card title="Thanh toán" style={{ marginTop: 16 }}>
            <Descriptions column={1} size="small" items={[
              { label: "Tiền mặt", children: vnd(b.cashPaidTotal) },
              { label: "VietQR", children: vnd(b.vietQrPaidTotal) },
              { label: "Tổng", children: <b>{vnd(b.total)}</b> },
            ]} />
            <Divider style={{ margin: "12px 0" }} />
            <Typography.Text strong>Điểm đã cộng: +{b.points.reduce((s, p) => s + p.points, 0)}</Typography.Text>
            <List size="small" dataSource={b.points} renderItem={(p) => (
              <List.Item extra={<b style={{ color: "#389e0d" }}>+{p.points}</b>}>
                <List.Item.Meta title={p.reason} description={dateTime(p.createdAt)} />
              </List.Item>
            )} />
            {b.claimedAt && <Typography.Text type="secondary">Tích điểm lúc {dateTime(b.claimedAt)}</Typography.Text>}
          </Card>
        </>
      )}
    </div>
  );
}
