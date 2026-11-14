import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Card, Empty, List, Space, Spin, Tag, Typography } from "antd";
import type { FloorSeat } from "@cafecr/api-client";
import { addDays, autoReleaseText, closeReasonLabels, displayName, minutesLeftText, seatStateStyles, seatTypeLabels, shiftTypeLabels, time, todayVn, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useCurrentShift } from "../../PosLayout";
import { useNotify } from "../useNotify";
import { CheckInSheet } from "./CheckInSheet";
import { SeatActionsSheet } from "./SeatActionsSheet";

export function CounterPage() {
  const current = useCurrentShift();
  if (current.isLoading) return <Spin size="large" />;
  return current.data ? <Floor branchId={current.data.branchId} /> : <JoinShift />;
}

/** Without an open shift the counter is locked: every bill must belong to a shift. */
function JoinShift() {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const today = todayVn();
  const shifts = useQuery({ queryKey: ["my-shifts", "counter"], queryFn: () => api.shifts.mine(addDays(today, -1), addDays(today, 1)), refetchInterval: 60_000 });
  const join = useMutation({
    mutationFn: api.attendance.checkIn,
    onSuccess: (a) => {
      notify.ok(a.isLate ? `Đã tham gia ca (trễ ${a.lateMinutes} phút).` : "Đã tham gia ca. Chúc ca làm vui vẻ!");
      void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
    },
    onError: notify.fail,
  });
  const upcoming = (shifts.data ?? []).filter((s) => s.assignmentStatus === "Accepted" && !s.checkOutAt);

  return (
    <Card title="Bạn chưa tham gia ca nào">
      <Typography.Paragraph>Bấm "Tham gia ca" trên ca của bạn để mở quầy. Nút mở từ 15 phút trước giờ bắt đầu.</Typography.Paragraph>
      <List
        dataSource={upcoming}
        locale={{ emptyText: <Empty description="Hôm nay bạn không có ca nào đã nhận." /> }}
        renderItem={(s) => (
          <List.Item actions={[
            <Button key="join" type="primary" size="large" disabled={!s.canCheckIn} loading={join.isPending} onClick={() => join.mutate(s.shiftId)}>Tham gia ca</Button>,
          ]}>
            <List.Item.Meta
              title={`${shiftTypeLabels[s.type]} · ${time(s.startAt)}–${time(s.endAt)}`}
              description={`${s.branchName} · ${s.branchAddress}${s.colleagues.length ? ` · Cùng ca: ${s.colleagues.map((c) => c.name).join(", ")}` : ""}`}
            />
          </List.Item>
        )}
      />
    </Card>
  );
}

function Floor({ branchId }: { branchId: string }) {
  const floor = useQuery({ queryKey: ["floor", branchId], queryFn: () => api.catalog.floor(branchId), refetchInterval: 10_000 });
  const [checkIn, setCheckIn] = useState<FloorSeat | null>(null);
  const [actions, setActions] = useState<FloorSeat | null>(null);
  const seats = floor.data?.zones.flatMap((z) => z.seats) ?? [];
  const ending = seats.filter((s) => s.state === "EndingSoon" || s.state === "Overdue");

  return (
    <Space direction="vertical" style={{ width: "100%" }} size="middle">
      <Space wrap>
        {Object.entries(seatStateStyles).map(([k, s]) => (
          <Tag key={k} style={{ background: s.background, color: s.color, borderColor: s.color, fontSize: 14, padding: "2px 10px" }}>
            {s.label}: {seats.filter((x) => x.state === k).length}
          </Tag>
        ))}
      </Space>
      {ending.length > 0 && (
        <Alert type="warning" showIcon message={`Sắp hết giờ / quá giờ: ${ending.map((s) => s.code).join(", ")}`} />
      )}
      {floor.data?.zones.map((zone) => (
        <Card key={zone.id} size="small" title={zone.name}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))", gap: 12 }}>
            {zone.seats.map((seat) => {
              const style = seatStateStyles[seat.state];
              return (
                <button
                  key={seat.id}
                  type="button"
                  disabled={seat.state === "Maintenance" || seat.state === "Held"}
                  onClick={() => (seat.session ? setActions(seat) : setCheckIn(seat))}
                  style={{
                    minHeight: 96, borderRadius: 12, border: `2px solid ${style.color}`, background: style.background, color: style.color,
                    cursor: seat.state === "Maintenance" || seat.state === "Held" ? "not-allowed" : "pointer", textAlign: "left", padding: 12, fontSize: 15,
                  }}
                >
                  <div style={{ fontSize: 18, fontWeight: 700 }}>{seat.code}</div>
                  <div>{seatTypeLabels[seat.type]}</div>
                  <div>{seat.session ? minutesLeftText(seat.session.minutesLeft) : style.label}</div>
                  {seat.state === "Overdue" && autoReleaseText(seat.session?.autoReleaseAt) && (
                    <div style={{ fontSize: 12, fontWeight: 600 }}>{autoReleaseText(seat.session?.autoReleaseAt)}</div>
                  )}
                  {seat.session?.customerName && <div style={{ fontSize: 12 }}>{displayName(seat.session.customerName)}</div>}
                </button>
              );
            })}
          </div>
        </Card>
      ))}
      <RecentlyClosed branchId={branchId} />
      {checkIn && <CheckInSheet branchId={branchId} seat={checkIn} onClose={() => setCheckIn(null)} />}
      {actions && <SeatActionsSheet branchId={branchId} seat={actions} onClose={() => setActions(null)} />}
    </Space>
  );
}

/** Seats freed in the last 2 hours, so staff can see which ones the system released by itself. */
function RecentlyClosed({ branchId }: { branchId: string }) {
  const recent = useQuery({
    queryKey: ["recent-closed", branchId],
    queryFn: () => api.sessions.recentClosed(branchId, 2),
    refetchInterval: 30_000,
  });
  const rows = recent.data ?? [];
  if (recent.isError || rows.length === 0) return null;
  const auto = rows.filter((r) => r.closeReason === "AutoReleased").length;

  return (
    <Card size="small" title={`Vừa trả chỗ (2 giờ qua) · ${rows.length}${auto ? ` · ${auto} tự trả` : ""}`}>
      <List
        size="small"
        dataSource={rows.slice(0, 8)}
        renderItem={(r) => {
          const reason = r.closeReason ? closeReasonLabels[r.closeReason] : null;
          return (
            <List.Item extra={<Typography.Text type="secondary">{time(r.closedAt)}</Typography.Text>}>
              <Space wrap size={6}>
                <Typography.Text strong>{r.seatCode}</Typography.Text>
                {reason && <Tag color={reason.color} style={{ margin: 0 }}>{reason.label}</Tag>}
                <Typography.Text type="secondary">
                  {r.customerName ? `${displayName(r.customerName)} · ` : ""}bill {r.billCode} · {vnd(r.billTotal)}
                </Typography.Text>
              </Space>
            </List.Item>
          );
        }}
      />
    </Card>
  );
}
