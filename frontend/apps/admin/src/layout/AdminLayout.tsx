import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { Button, Layout, Menu, Space, Typography } from "antd";
import { useAuth } from "../auth/AuthProvider";

const menuItems = [
  { key: "/branches", label: "Chi nhánh" },
  { key: "/zones", label: "Khu vực" },
  { key: "/seats", label: "Chỗ ngồi" },
  { key: "/price-plans", label: "Bảng giá" },
  { key: "/users", label: "Tài khoản" },
];

export function AdminLayout() {
  const { state, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Layout.Sider breakpoint="lg" collapsedWidth={0} theme="light">
        <Typography.Title level={4} style={{ padding: "16px 24px", margin: 0 }}>
          Chốn Riêng
        </Typography.Title>
        <Menu
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Layout.Sider>
      <Layout>
        <Layout.Header
          style={{ background: "#fff", display: "flex", justifyContent: "flex-end", alignItems: "center", paddingInline: 16 }}
        >
          <Space>
            <Typography.Text>{state.user?.fullName || state.user?.email}</Typography.Text>
            <Button onClick={() => void logout()}>Đăng xuất</Button>
          </Space>
        </Layout.Header>
        <Layout.Content style={{ padding: 16 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
