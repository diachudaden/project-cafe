import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, DatePicker, Form, InputNumber, Modal, Popconfirm, Space, Table, Tag, Typography } from "antd";
import dayjs from "dayjs";
import type { PayrollView } from "@cafecr/api-client";
import { hasRole, monthStart, todayVn, vnd } from "@cafecr/ui";
import { api, useAuth } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function PayrollPage() {
  return <RequireBranch title="Lương">{(branchId) => <Payroll branchId={branchId} />}</RequireBranch>;
}

function Payroll({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { state } = useAuth();
  const isAdmin = hasRole(state.user, "Admin");
  const [month, setMonth] = useState(() => dayjs(monthStart(todayVn())).subtract(1, "month").format("YYYY-MM-DD"));
  const [editing, setEditing] = useState<PayrollView | null>(null);
  const [form] = Form.useForm<{ bonus: number; deduction: number }>();
  const payroll = useQuery({ queryKey: ["payroll", branchId, month], queryFn: () => api.finance.payroll(branchId, month) });
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["payroll", branchId] });
  const generate = useMutation({ mutationFn: () => api.finance.generatePayroll(branchId, month), onSuccess: () => { notify.ok("Đã tính lương tạm từ chấm công đã duyệt."); refresh(); }, onError: notify.fail });
  const adjust = useMutation({
    mutationFn: (v: { bonus: number; deduction: number }) => api.finance.adjustPayroll(editing!.id, v.bonus, v.deduction),
    onSuccess: () => { setEditing(null); refresh(); }, onError: notify.fail,
  });
  const finalize = useMutation({ mutationFn: api.finance.finalizePayroll, onSuccess: () => { notify.ok("Đã chốt lương và gửi cho nhân viên."); refresh(); }, onError: notify.fail });

  return (
    <Card title="Bảng lương tháng" extra={
      <Space>
        <DatePicker picker="month" value={dayjs(month)} onChange={(d) => d && setMonth(d.startOf("month").format("YYYY-MM-DD"))} />
        <Button type="primary" loading={generate.isPending} onClick={() => generate.mutate()}>Tính lương tạm</Button>
      </Space>
    }>
      <Typography.Paragraph type="secondary">
        Lương = giờ công × lương giờ + giờ đêm (22:00–06:00) × phụ cấp + thưởng − phạt. Chỉ tính chấm công đã duyệt. {isAdmin ? "" : "Chỉ Admin được chốt lương."}
      </Typography.Paragraph>
      <Table
        rowKey="id" size="middle" loading={payroll.isLoading} dataSource={payroll.data} scroll={{ x: "max-content" }} pagination={false}
        summary={(rows) => (
          <Table.Summary.Row>
            <Table.Summary.Cell index={0} colSpan={6}>Tổng</Table.Summary.Cell>
            <Table.Summary.Cell index={1}>{vnd(rows.reduce((s, r) => s + r.netAmount, 0))}</Table.Summary.Cell>
            <Table.Summary.Cell index={2} colSpan={2} />
          </Table.Summary.Row>
        )}
        columns={[
          { title: "Nhân viên", dataIndex: "employeeName" },
          { title: "Giờ công", dataIndex: "totalHours" },
          { title: "Lương giờ", dataIndex: "baseAmount", render: (v: number) => vnd(v) },
          { title: "Phụ cấp đêm", dataIndex: "allowance", render: (v: number) => vnd(v) },
          { title: "Thưởng", dataIndex: "bonus", render: (v: number) => vnd(v) },
          { title: "Phạt", dataIndex: "deduction", render: (v: number) => vnd(v) },
          { title: "Thực nhận", dataIndex: "netAmount", render: (v: number) => <b>{vnd(v)}</b> },
          { title: "Trạng thái", dataIndex: "status", render: (s: string) => <Tag color={s === "Finalized" ? "green" : "gold"}>{s === "Finalized" ? "Đã chốt" : "Tạm tính"}</Tag> },
          {
            title: "", render: (_, p) => p.status === "Draft" && (
              <Space>
                <Button size="small" onClick={() => { form.setFieldsValue({ bonus: p.bonus, deduction: p.deduction }); setEditing(p); }}>Thưởng/phạt</Button>
                {isAdmin && <Popconfirm title="Chốt lương? Không thể sửa sau khi chốt." onConfirm={() => finalize.mutate(p.id)}><Button size="small" type="primary">Chốt</Button></Popconfirm>}
              </Space>
            ),
          },
        ]}
      />
      <Modal open={!!editing} title={`Điều chỉnh: ${editing?.employeeName}`} onCancel={() => setEditing(null)} onOk={() => form.submit()} confirmLoading={adjust.isPending}>
        <Form form={form} layout="vertical" onFinish={(v) => adjust.mutate(v)}>
          <Form.Item label="Thưởng (VND)" name="bonus"><InputNumber min={0} step={10000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Phạt (VND)" name="deduction"><InputNumber min={0} step={10000} style={{ width: "100%" }} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
