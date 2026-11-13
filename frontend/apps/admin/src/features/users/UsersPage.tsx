import { useEffect, useState, type ReactNode } from "react";
import { Form, Input, Select, Space, Tabs, Tag } from "antd";
import { CrownOutlined, SearchOutlined, ShopOutlined, TeamOutlined, UserOutlined } from "@ant-design/icons";
import { roles, type AdminUser, type AdminUserDetail, type CreateUserInput, type Role, type UpdateUserInput } from "@cafecr/api-client";
import { adminApi } from "../../api";
import { CrudPage } from "../../components/CrudPage";

const roleLabels: Record<Role, string> = {
  Admin: "Quản trị viên",
  BranchManager: "Quản lý chi nhánh",
  Staff: "Nhân viên",
  Customer: "Khách hàng",
};

const roleColors: Record<Role, string> = {
  Admin: "red",
  BranchManager: "gold",
  Staff: "blue",
  Customer: "green",
};

/** The three role groups shown as tabs; "Quản lý" covers branch managers and admins. */
const roleTabs: { key: string; label: string; icon: ReactNode; roles: Role[] }[] = [
  { key: "staff", label: "Nhân viên (POS)", icon: <ShopOutlined />, roles: ["Staff"] },
  { key: "manager", label: "Quản lý", icon: <CrownOutlined />, roles: ["BranchManager", "Admin"] },
  { key: "customer", label: "Khách hàng", icon: <UserOutlined />, roles: ["Customer"] },
  { key: "all", label: "Tất cả", icon: <TeamOutlined />, roles: [] },
];

type UserForm = CreateUserInput;

export function UsersPage() {
  const [tab, setTab] = useState("staff");
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  // Debounce typing so every keystroke does not hit the API.
  useEffect(() => {
    const timer = setTimeout(() => setSearch(text.trim()), 300);
    return () => clearTimeout(timer);
  }, [text]);

  const current = roleTabs.find((t) => t.key === tab)!;

  return (
    <CrudPage<AdminUser, UserForm, CreateUserInput, UpdateUserInput, AdminUserDetail>
      title="Tài khoản & vai trò"
      queryKey="users"
      resource={adminApi.users}
      listParams={{ search: search || undefined, role: current.roles }}
      toolbar={
        <Space direction="vertical" style={{ width: "100%" }}>
          <Tabs
            activeKey={tab}
            onChange={setTab}
            items={roleTabs.map((t) => ({ key: t.key, label: <span>{t.icon} {t.label}</span> }))}
          />
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder="Tìm theo họ tên, email hoặc số điện thoại"
            value={text}
            onChange={(e) => setText(e.target.value)}
            style={{ maxWidth: 420 }}
          />
        </Space>
      }
      columns={[
        { title: "Họ tên", dataIndex: "fullName" },
        { title: "Email", dataIndex: "email" },
        { title: "Điện thoại", dataIndex: "phoneNumber" },
        {
          title: "Vai trò",
          dataIndex: "roles",
          render: (r: Role[]) => r.map((x) => <Tag key={x} color={roleColors[x]}>{roleLabels[x] ?? x}</Tag>),
        },
      ]}
      initialValues={{ roles: current.roles.length ? [current.roles[0]] : ["Customer"] }}
      toFormValues={async (user) => {
        const detail = await adminApi.users.get(user.id);
        return { email: detail.email, fullName: detail.fullName, phoneNumber: detail.phoneNumber, roles: detail.roles, password: "" };
      }}
      toCreate={(v) => ({ ...v, phoneNumber: v.phoneNumber || null })}
      toUpdate={({ password: _password, ...v }) => ({ ...v, phoneNumber: v.phoneNumber || null })}
      renderFields={(mode) => (
        <>
          <Form.Item label="Email" name="email" rules={[{ required: true, type: "email", max: 256 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          {mode === "create" && (
            <Form.Item
              label="Mật khẩu ban đầu"
              name="password"
              extra="Tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
              rules={[{ required: true, min: 12, max: 128 }]}
            >
              <Input.Password autoComplete="new-password" />
            </Form.Item>
          )}
          <Form.Item label="Họ tên" name="fullName" rules={[{ required: true, max: 150 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Điện thoại" name="phoneNumber" rules={[{ max: 32 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Vai trò" name="roles" rules={[{ required: true, type: "array", min: 1 }]}>
            <Select mode="multiple" options={roles.map((r) => ({ value: r, label: roleLabels[r] }))} />
          </Form.Item>
        </>
      )}
    />
  );
}
