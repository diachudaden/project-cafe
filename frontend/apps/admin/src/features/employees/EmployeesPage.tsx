import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Form, Input, InputNumber, Modal, Segmented, Select, Space, Table, Tag, Typography } from "antd";
import { SearchOutlined } from "@ant-design/icons";
import type { EmployeeView, Role } from "@cafecr/api-client";
import { roleLabels, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useBranch } from "../../branch/BranchContext";
import { useNotify } from "../../components/useNotify";

interface EmployeeForm { hourlyRate: number; nightAllowance: number; branchIds: string[]; homeBranchId: string }

const roleOrder: Role[] = ["Admin", "BranchManager", "Staff", "Customer"];
const pageSizes = [
  { value: 20, label: "20 / trang" },
  { value: 50, label: "50 / trang" },
  { value: 100, label: "100 / trang" },
  { value: 0, label: "Hiện tất cả" },
];

const normalize = (text: string) => text.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "D").toLowerCase();

export function EmployeesPage() {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { branchId, branches, branchName, canSeeAll } = useBranch();
  const [editing, setEditing] = useState<EmployeeView | null>(null);
  const [form] = Form.useForm<EmployeeForm>();
  // "branch" lists the branch picked in the top bar; "all" lists every employee the caller manages.
  const [scope, setScope] = useState<"branch" | "all">(branchId ? "branch" : "all");
  const effectiveScope = branchId ? scope : "all";
  const [text, setText] = useState("");
  const [roleFilter, setRoleFilter] = useState<string[]>([]);
  const [branchFilter, setBranchFilter] = useState<string[]>([]);
  const [pageSize, setPageSize] = useState(20);
  const employees = useQuery({
    queryKey: ["employees", effectiveScope === "all" ? "all" : branchId],
    queryFn: () => (effectiveScope === "all" ? api.workforce.allEmployees() : api.workforce.employees(branchId!)),
  });

  const rows = useMemo(() => {
    const term = normalize(text.trim());
    const digits = term.replace(/\D/g, "");
    return (employees.data ?? []).filter((e) => {
      if (term) {
        const haystack = normalize([e.fullName, e.email, e.phoneNumber ?? ""].join(" "));
        const phone = (e.phoneNumber ?? "").replace(/\D/g, "");
        if (!haystack.includes(term) && !(digits.length > 0 && phone.includes(digits))) return false;
      }
      if (roleFilter.length && !e.roles.some((r) => roleFilter.includes(r))) return false;
      if (branchFilter.length && !e.branchIds.some((id) => branchFilter.includes(id))) return false;
      return true;
    });
  }, [employees.data, text, roleFilter, branchFilter]);

  const roleOptions = useMemo(() => {
    const present = new Set((employees.data ?? []).flatMap((e) => e.roles));
    return [...roleOrder.filter((r) => present.has(r)), ...[...present].filter((r) => !roleOrder.includes(r as Role))]
      .map((r) => ({ value: r, label: roleLabels[r as Role] ?? r }));
  }, [employees.data]);

  const filtered = !!text.trim() || roleFilter.length > 0 || branchFilter.length > 0;
  const save = useMutation({
    mutationFn: async (f: EmployeeForm) => {
      await api.workforce.updateEmployee(editing!.employeeId, f.hourlyRate, f.nightAllowance);
      await api.workforce.setBranches(editing!.employeeId, f.branchIds, f.homeBranchId);
    },
    onSuccess: () => { notify.ok("Đã lưu."); setEditing(null); void queryClient.invalidateQueries({ queryKey: ["employees"] }); },
    onError: notify.fail,
  });

  return (
    <Card
      title={effectiveScope === "all" ? (canSeeAll ? "Nhân viên toàn chuỗi" : "Nhân viên các chi nhánh tôi quản lý") : `Nhân viên · ${branchName(branchId).replace("[DEMO DATA] ", "")}`}
      extra={branchId && (
        <Segmented
          value={scope}
          onChange={(v) => setScope(v as "branch" | "all")}
          options={[{ value: "branch", label: "Chi nhánh đang chọn" }, { value: "all", label: "Tất cả chi nhánh" }]}
        />
      )}
    >
      <Space wrap style={{ marginBottom: 16 }}>
        <Input
          allowClear
          prefix={<SearchOutlined />}
          placeholder="Tìm họ tên, email, số điện thoại"
          value={text}
          onChange={(e) => setText(e.target.value)}
          style={{ width: 300 }}
        />
        <Select
          mode="multiple"
          allowClear
          placeholder="Vai trò"
          value={roleFilter}
          onChange={setRoleFilter}
          options={roleOptions}
          style={{ minWidth: 200 }}
        />
        <Select
          mode="multiple"
          allowClear
          placeholder="Chi nhánh"
          value={branchFilter}
          onChange={setBranchFilter}
          options={branches.map((b) => ({ value: b.id, label: b.name.replace("[DEMO DATA] ", "") }))}
          optionFilterProp="label"
          style={{ minWidth: 240 }}
        />
        <Select value={pageSize} onChange={setPageSize} options={pageSizes} style={{ width: 140 }} />
        {filtered && <Button onClick={() => { setText(""); setRoleFilter([]); setBranchFilter([]); }}>Xóa bộ lọc</Button>}
        <Typography.Text type="secondary">{rows.length} / {employees.data?.length ?? 0} nhân viên</Typography.Text>
      </Space>
      <Table rowKey="employeeId" size="middle" loading={employees.isLoading} dataSource={rows} scroll={{ x: "max-content" }}
        pagination={pageSize === 0 ? false : { pageSize, showSizeChanger: false }}
        columns={[
          { title: "Họ tên", dataIndex: "fullName" },
          { title: "Email", dataIndex: "email" },
          { title: "Điện thoại", dataIndex: "phoneNumber" },
          { title: "Vai trò", dataIndex: "roles", render: (r: string[]) => r.map((x) => <Tag key={x}>{roleLabels[x as Role] ?? x}</Tag>) },
          { title: "Lương giờ", dataIndex: "hourlyRate", render: (v: number) => vnd(v) },
          { title: "Phụ cấp đêm/giờ", dataIndex: "nightAllowance", render: (v: number) => vnd(v) },
          { title: "Chi nhánh", render: (_, e) => e.branchIds.map((id) => <Tag key={id} color={id === e.homeBranchId ? "blue" : undefined}>{branchName(id).replace("[DEMO DATA] ", "")}</Tag>) },
          { title: "", dataIndex: "locked", render: (l: boolean) => l && <Tag color="red">Đã khóa</Tag> },
          {
            title: "", render: (_, e) => (
              <Button size="small" onClick={() => {
                form.setFieldsValue({ hourlyRate: e.hourlyRate, nightAllowance: e.nightAllowance, branchIds: e.branchIds, homeBranchId: e.homeBranchId ?? e.branchIds[0] });
                setEditing(e);
              }}>Sửa</Button>
            ),
          },
        ]} />
      <Modal open={!!editing} title={editing?.fullName} onCancel={() => setEditing(null)} onOk={() => form.submit()} confirmLoading={save.isPending}>
        <Form form={form} layout="vertical" onFinish={(f) => save.mutate(f)}>
          <Form.Item label="Lương giờ (VND)" name="hourlyRate"><InputNumber min={0} step={1000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Phụ cấp mỗi giờ làm đêm (VND)" name="nightAllowance"><InputNumber min={0} step={1000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Chi nhánh được làm" name="branchIds" rules={[{ required: true }]}
            extra="Quản lý chi nhánh chỉ thêm/bớt được chi nhánh mình quản lý; chi nhánh khác được giữ nguyên.">
            <Select mode="multiple" options={branches.map((b) => ({ value: b.id, label: b.name }))} />
          </Form.Item>
          <Form.Item label="Chi nhánh chính" name="homeBranchId" rules={[{ required: true }]}>
            <Select options={branches.map((b) => ({ value: b.id, label: b.name }))} />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
