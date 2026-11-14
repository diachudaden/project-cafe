import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Button, Card, Descriptions, List, Typography, theme } from "antd";
import { dateTime, minutesLeftText, time, vnd } from "@cafecr/ui";
import { api, useAuth } from "../api";
import { EnableNotifications } from "../components/EnableNotifications";
import { PointsDeltaBadge, useAnimatedPoints } from "../components/PointsDelta";

export function MePage() {
  const { state, logout } = useAuth();
  const points = useQuery({ queryKey: ["points"], queryFn: api.loyalty.points });
  const session = useQuery({ queryKey: ["my-session"], queryFn: api.loyalty.session, refetchInterval: 30_000 });
  const bills = useQuery({ queryKey: ["my-bills"], queryFn: api.loyalty.bills });
  const user = state.user!;
  const navigate = useNavigate();
  const { shown, delta } = useAnimatedPoints(points.data?.points);
  const { token } = theme.useToken();

  return (
    <div style={{ padding: 16 }}>
      {session.data && (
        <Card style={{ marginBottom: 16, background: session.data.minutesLeft <= 15 ? token.colorWarningBg : undefined }}>
          <Typography.Text strong>Đang ngồi tại {session.data.branchName} · {session.data.seatCode}</Typography.Text>
          <div style={{ fontSize: 22 }}>{minutesLeftText(session.data.minutesLeft)}</div>
          <Typography.Text type="secondary">Hết giờ lúc {time(session.data.endAt)} — nhờ nhân viên gia hạn nếu muốn ngồi thêm.</Typography.Text>
        </Card>
      )}
      <Card title="Mã thành viên" style={{ textAlign: "center" }}>
        <Typography.Title copyable style={{ letterSpacing: 4, margin: 0 }}>{points.data?.memberCode ?? "…"}</Typography.Title>
        <Typography.Text type="secondary">Đọc mã này cho nhân viên khi check-in để được cộng điểm tự động.</Typography.Text>
        <div style={{ marginTop: 12, fontSize: 18 }}>
          <b>{shown.toLocaleString("vi-VN")}</b> điểm <PointsDeltaBadge delta={delta} />
        </div>
        <EnableNotifications />
      </Card>
      <Card title="Tài khoản" style={{ marginTop: 16 }}>
        <Descriptions column={1} size="small" items={[
          { label: "Họ tên", children: user.fullName },
          { label: "Email", children: user.email },
          { label: "Điện thoại", children: user.phoneNumber ?? "—" },
        ]} />
        <Button block style={{ marginTop: 12 }} onClick={() => void logout()}>Đăng xuất</Button>
      </Card>
      <Card title="Hóa đơn đã tích điểm" style={{ marginTop: 16 }}>
        <List dataSource={bills.data} locale={{ emptyText: "Chưa có hóa đơn." }}
          renderItem={(b) => (
            <List.Item extra={`+${b.points}`} onClick={() => navigate(`/bills/${b.code}`)} style={{ cursor: "pointer" }}>
              <List.Item.Meta title={`${b.code} · ${vnd(b.total)}`} description={`${b.branchName} · ${dateTime(b.createdAt)}`} />
            </List.Item>
          )} />
      </Card>
    </div>
  );
}
