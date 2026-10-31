import { Form, Input, Select } from "antd";
import { roles, type AdminUser, type AdminUserDetail, type CreateUserInput, type Role, type UpdateUserInput } from "@cafecr/api-client";
import { adminApi } from "../../api";
import { CrudPage } from "../../components/CrudPage";

const roleLabels: Record<Role, string> = {
  Admin: "Quản trị viên",
  BranchManager: "Quản lý chi nhánh",
  Staff: "Nhân viên",
  Customer: "Khách hàng",
};

type UserForm = CreateUserInput;

export function UsersPage() {
  return (
    <CrudPage<AdminUser, UserForm, CreateUserInput, UpdateUserInput, AdminUserDetail>
      title="Tài khoản"
      queryKey="users"
      resource={adminApi.users}
      columns={[
        { title: "Email", dataIndex: "email" },
        { title: "Họ tên", dataIndex: "fullName" },
        { title: "Điện thoại", dataIndex: "phoneNumber" },
      ]}
      initialValues={{ roles: ["Customer"] }}
      // The list response has no roles, so editing loads the detail endpoint.
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
