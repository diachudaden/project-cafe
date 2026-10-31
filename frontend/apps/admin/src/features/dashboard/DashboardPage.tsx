import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { Alert, Card, Col, Empty, List, Row, Statistic, Typography } from "antd";
import { time, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useBranch } from "../../branch/BranchContext";

export function DashboardPage() {
  const { branchId, branchName } = useBranch();
  const navigate = useNavigate();
  const dashboard = useQuery({
    queryKey: ["dashboard", branchId],
    queryFn: () => api.reports.dashboard(branchId ?? undefined),
    refetchInterval: 10_000,
  });
  const d = dashboard.data;

  return (
    <Row gutter={[16, 16]}>
      <Col span={24}>
        <Typography.Title level={4} style={{ margin: 0 }}>Tổng quan hôm nay · {branchName(branchId)}</Typography.Title>
      </Col>
      {dashboard.isError && <Col span={24}><Alert type="error" message={dashboard.error.message} /></Col>}
      <Col xs={12} md={6}><Card><Statistic title="Doanh thu hôm nay" value={d ? vnd(d.revenueToday) : "—"} /></Card></Col>
      <Col xs={12} md={6}><Card><Statistic title="Hóa đơn" value={d?.billsToday ?? "—"} /></Card></Col>
      <Col xs={12} md={6}><Card><Statistic title="Khách đang ngồi" value={d ? `${d.activeSessions}/${d.seats}` : "—"} /></Card></Col>
      <Col xs={12} md={6}><Card><Statistic title="Lấp đầy" value={d?.occupancyNow ?? 0} suffix="%" /></Card></Col>
      <Col xs={24} md={12}>
        <Card title="Cảnh báo">
          {d && d.alerts.length === 0 && <Empty description="Không có cảnh báo." />}
          <List
            dataSource={d?.alerts ?? []}
            renderItem={(a) => (
              <List.Item style={{ cursor: "pointer" }} onClick={() => navigate(a.link.replace(/^\/admin/, "") || "/")}>
                <Alert style={{ width: "100%" }} type={a.type === "closing-difference" ? "error" : "warning"} message={a.message} showIcon />
              </List.Item>
            )}
          />
        </Card>
      </Col>
      <Col xs={24} md={12}>
        <Card title="Đang trong ca">
          <List
            dataSource={d?.onShift ?? []}
            locale={{ emptyText: "Chưa có ai tham gia ca." }}
            renderItem={(e) => (
              <List.Item>
                <List.Item.Meta title={e.name} description={`${e.branchName} · vào ${time(e.checkInAt)}, hết ca ${time(e.shiftEnd)}`} />
              </List.Item>
            )}
          />
        </Card>
      </Col>
    </Row>
  );
}
