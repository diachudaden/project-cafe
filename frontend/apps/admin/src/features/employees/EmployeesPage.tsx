import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Form, InputNumber, Modal, Select, Table, Tag } from "antd";
import type { EmployeeView, Role } from "@cafecr/api-client";
import { roleLabels, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useBranch } from "../../branch/BranchContext";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

interface EmployeeForm { hourlyRate: number; nightAllowance: number; branchIds: string[]; homeBranchId: string }

export function EmployeesPage() {
  return <RequireBranch title="Nhân viên">{(branchId) => <Employees branchId={branchId} />}</RequireBranch>;
}

function Employees({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { branches, branchName } = useBranch();
  const [editing, setEditing] = useState<EmployeeView | null>(null);
  const [form] = Form.useForm<EmployeeForm>();
  const employees = useQuery({ queryKey: ["employees", branchId], queryFn: () => api.workforce.employees(branchId) });
  const save = useMutation({
    mutationFn: async (f: EmployeeForm) => {
      await api.workforce.updateEmployee(editing!.employeeId, f.hourlyRate, f.nightAllowance);
      await api.workforce.setBranches(editing!.employeeId, f.branchIds, f.homeBranchId);
    },
    onSuccess: () => { notify.ok("Đã lưu."); setEditing(null); void queryClient.invalidateQueries({ queryKey: ["employees"] }); },
    onError: notify.fail,
  });

  return (
    <Card title="Nhân viên của chi nhánh">
      <Table rowKey="employeeId" size="middle" loading={employees.isLoading} dataSource={employees.data} scroll={{ x: "max-content" }} pagination={{ pageSize: 30 }}
        columns={[
          { title: "Họ tên", dataIndex: "fullName" },
          { title: "Email", dataIndex: "email" },
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
