import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AutoComplete, Button, Card, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Space, Statistic, Table, Tag } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import { dateTime, monthStart, todayVn, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

const categories = ["Nguyên liệu", "Điện nước", "Thuê mặt bằng", "Sửa chữa", "Vệ sinh", "Khác"];

export function ExpensesPage() {
  return <RequireBranch title="Thu chi">{(branchId) => <Expenses branchId={branchId} />}</RequireBranch>;
}

function Expenses({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [month, setMonth] = useState(() => monthStart(todayVn()));
  const [adding, setAdding] = useState(false);
  const [reason, setReason] = useState("");
  const [form] = Form.useForm<{ category: string; amount: number; date: Dayjs; note?: string }>();
  const to = dayjs(month).endOf("month").format("YYYY-MM-DD");
  const expenses = useQuery({ queryKey: ["expenses", branchId, month], queryFn: () => api.finance.expenses(branchId, month, to) });
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["expenses", branchId] });
  const create = useMutation({
    mutationFn: (v: { category: string; amount: number; date: Dayjs; note?: string }) =>
      api.finance.createExpense({ branchId, category: v.category, amount: v.amount, date: v.date.toISOString(), note: v.note ?? null }),
    onSuccess: () => { notify.ok("Đã ghi khoản chi."); setAdding(false); form.resetFields(); refresh(); },
    onError: notify.fail,
  });
  const reverse = useMutation({
    mutationFn: (id: string) => api.finance.reverseExpense(id, reason || "Ghi sai"),
    onSuccess: () => { notify.ok("Đã ghi bút toán đảo."); setReason(""); refresh(); },
    onError: notify.fail,
  });

  return (
    <Card title="Sổ chi" extra={
      <Space>
        <DatePicker picker="month" value={dayjs(month)} onChange={(d) => d && setMonth(d.startOf("month").format("YYYY-MM-DD"))} />
        <Button type="primary" onClick={() => setAdding(true)}>Ghi khoản chi</Button>
      </Space>
    }>
      <Statistic title="Tổng chi trong tháng" value={vnd((expenses.data ?? []).reduce((s, e) => s + e.amount, 0))} style={{ marginBottom: 16 }} />
      <Table
        rowKey="id" size="middle" loading={expenses.isLoading} dataSource={expenses.data} scroll={{ x: "max-content" }} pagination={{ pageSize: 30 }}
        columns={[
          { title: "Ngày", dataIndex: "date", render: dateTime },
          { title: "Danh mục", dataIndex: "category" },
          { title: "Số tiền", dataIndex: "amount", render: (v: number) => vnd(v) },
          { title: "Ghi chú", dataIndex: "note" },
          { title: "Người ghi", dataIndex: "createdByName", render: (v) => v ?? "Admin" },
          {
            title: "", render: (_, e) => e.reversesExpenseId ? <Tag>Bút toán đảo</Tag> : e.isReversed ? <Tag color="red">Đã đảo</Tag> : (
              <Popconfirm title="Ghi bút toán đảo?" description={<Input placeholder="Lý do" value={reason} onChange={(ev) => setReason(ev.target.value)} />}
                onConfirm={() => reverse.mutate(e.id)}>
                <Button size="small">Đảo</Button>
              </Popconfirm>
            ),
          },
        ]}
      />
      <Modal open={adding} title="Ghi khoản chi" okText="Lưu" cancelText="Hủy" onCancel={() => setAdding(false)} onOk={() => form.submit()} confirmLoading={create.isPending}>
        <Form form={form} layout="vertical" initialValues={{ date: dayjs() }} onFinish={(v) => create.mutate(v)}>
          <Form.Item label="Danh mục" name="category" rules={[{ required: true, max: 80 }]}>
            <AutoComplete options={categories.map((c) => ({ value: c }))} />
          </Form.Item>
          <Form.Item label="Số tiền (VND)" name="amount" rules={[{ required: true }]}><InputNumber min={1000} step={1000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Ngày" name="date" rules={[{ required: true }]}><DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Ghi chú" name="note"><Input.TextArea maxLength={1000} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
