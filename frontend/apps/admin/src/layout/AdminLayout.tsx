import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Badge, Button, Layout, List, Menu, Popover, Select, Space, Typography, theme } from "antd";
import {
  ApartmentOutlined, AppstoreOutlined, BarChartOutlined, CalendarOutlined, ClockCircleOutlined, DashboardOutlined, DollarOutlined,
  FileDoneOutlined, FormOutlined, SafetyCertificateOutlined, TagsOutlined, TeamOutlined, WalletOutlined,
} from "@ant-design/icons";
import { ThemeToggle, dateTime, hasRole, roleLabels } from "@cafecr/ui";
import { api, useAuth } from "../api";
import { useBranch } from "../branch/BranchContext";

const managementMenu = [
  { key: "/", icon: <DashboardOutlined />, label: "Tổng quan" },
  { key: "/schedule", icon: <CalendarOutlined />, label: "Lịch ca" },
  { key: "/requests", icon: <FormOutlined />, label: "Đơn từ" },
  { key: "/attendance", icon: <ClockCircleOutlined />, label: "Chấm công" },
  { key: "/closings", icon: <FileDoneOutlined />, label: "Kết ca" },
  { key: "/expenses", icon: <WalletOutlined />, label: "Thu chi" },
  { key: "/payroll", icon: <DollarOutlined />, label: "Lương" },
  { key: "/reports", icon: <BarChartOutlined />, label: "Báo cáo" },
  { key: "/floor", icon: <AppstoreOutlined />, label: "Sơ đồ chỗ ngồi" },
  { key: "/pricing", icon: <TagsOutlined />, label: "Bảng giá" },
  { key: "/employees", icon: <TeamOutlined />, label: "Nhân viên" },
];

const adminMenu = [
  { key: "/branches", icon: <ApartmentOutlined />, label: "Chi nhánh" },
  { key: "/users", icon: <SafetyCertificateOutlined />, label: "Tài khoản & vai trò" },
];

function Notifications() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const feed = useQuery({ queryKey: ["notifications"], queryFn: api.notifications.feed, refetchInterval: 30_000 });
  const read = useMutation({
    mutationFn: () => api.notifications.markRead(),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });

  const content = (
    <List
      style={{ width: 360, maxHeight: 420, overflow: "auto" }}
      dataSource={feed.data?.items ?? []}
      locale={{ emptyText: "Chưa có hoạt động nào." }}
      renderItem={(n) => (
        <List.Item
          style={{ cursor: n.link ? "pointer" : undefined, opacity: n.readAt ? 0.6 : 1 }}
          onClick={() => n.link?.startsWith("/admin") && navigate(n.link.replace(/^\/admin/, "") || "/")}
        >
          <List.Item.Meta title={n.title} description={<>{n.body}<br /><Typography.Text type="secondary">{dateTime(n.createdAt)}</Typography.Text></>} />
        </List.Item>
      )}
    />
  );

  return (
    <Popover
      trigger="click"
      title={<Space style={{ width: "100%", justifyContent: "space-between" }}>Hoạt động<Button size="small" type="link" onClick={() => read.mutate()}>Đánh dấu đã đọc</Button></Space>}
      content={content}
    >
      <Badge count={feed.data?.unread ?? 0} size="small">
        <Button>Hoạt động</Button>
      </Badge>
    </Popover>
  );
}

export function AdminLayout() {
  const { state, logout } = useAuth();
  const { token } = theme.useToken();
  const { branchId, setBranchId, branches, canSeeAll } = useBranch();
  const navigate = useNavigate();
  const location = useLocation();
  const user = state.user!;
  const isAdmin = hasRole(user, "Admin");

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Layout.Sider breakpoint="lg" collapsedWidth={0} theme="light">
        <Typography.Title level={4} style={{ padding: "16px 24px", margin: 0 }}>
          Chốn Riêng
        </Typography.Title>
        <Menu
          mode="inline"
          selectedKeys={[location.pathname]}
          items={[
            ...managementMenu,
            ...(isAdmin ? [{ type: "divider" as const }, { type: "group" as const, label: "Quản trị", children: adminMenu }] : []),
          ]}
          onClick={({ key }) => navigate(key)}
        />
      </Layout.Sider>
      <Layout>
        <Layout.Header style={{ background: token.colorBgContainer, display: "flex", gap: 12, alignItems: "center", paddingInline: 16, flexWrap: "wrap", height: "auto", minHeight: 64 }}>
          <Select
            style={{ minWidth: 240 }}
            value={branchId ?? "all"}
            onChange={(v) => setBranchId(v === "all" ? null : v)}
            options={[
              ...(canSeeAll ? [{ value: "all", label: "Tất cả chi nhánh" }] : []),
              ...branches.map((b) => ({ value: b.id, label: b.name })),
            ]}
          />
          <div style={{ flex: 1 }} />
          <Notifications />
          <Typography.Text>
            {user.fullName || user.email} · {user.roles.map((r) => roleLabels[r]).join(", ")}
          </Typography.Text>
          <ThemeToggle />
          <Button onClick={() => void logout()}>Đăng xuất</Button>
        </Layout.Header>
        <Layout.Content style={{ padding: 16 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
