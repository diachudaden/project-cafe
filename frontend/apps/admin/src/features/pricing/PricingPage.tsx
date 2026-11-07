import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, DatePicker, Form, Input, InputNumber, Modal, Select, Table, Tag, Typography } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import type { PricePlanView } from "@cafecr/api-client";
import { dateTime, hasRole, vnd } from "@cafecr/ui";
import { api, useAuth } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

interface PlanForm { scope: "branch" | "chain"; name: string; durationMinutes: number; price: number; extendPrice: number; validFrom?: Dayjs | null; validTo?: Dayjs | null }

export function PricingPage() {
  return <RequireBranch title="Bảng giá">{(branchId) => <Pricing branchId={branchId} />}</RequireBranch>;
}

function Pricing({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const isAdmin = hasRole(useAuth().state.user, "Admin");
  const [editing, setEditing] = useState<PricePlanView | "new" | null>(null);
  const [form] = Form.useForm<PlanForm>();
  const plans = useQuery({ queryKey: ["plans", branchId], queryFn: () => api.catalog.allPlans(branchId) });
  const save = useMutation({
    mutationFn: (f: PlanForm) => {
      const input = {
        branchId: f.scope === "chain" ? null : branchId, name: f.name, durationMinutes: f.durationMinutes, price: f.price, extendPrice: f.extendPrice,
        validFrom: f.validFrom?.toISOString() ?? null, validTo: f.validTo?.toISOString() ?? null,
      };
      return editing === "new" ? api.catalog.createPlan(input) : api.catalog.updatePlan((editing as PricePlanView).id, input);
    },
    onSuccess: () => { setEditing(null); void queryClient.invalidateQueries({ queryKey: ["plans"] }); },
    onError: notify.fail,
  });
  const open = (p: PricePlanView | "new") => {
    form.setFieldsValue(p === "new"
      ? { scope: "branch", name: "", durationMinutes: 240, price: 45000, extendPrice: 40000, validFrom: null, validTo: null }
      : { scope: p.branchId ? "branch" : "chain", name: p.name, durationMinutes: p.durationMinutes, price: p.price, extendPrice: p.extendPrice,
          validFrom: p.validFrom ? dayjs(p.validFrom) : null, validTo: p.validTo ? dayjs(p.validTo) : null });
    setEditing(p);
  };

  return (
    <Card title="Bảng giá" extra={<Button type="primary" onClick={() => open("new")}>Thêm bảng giá</Button>}>
      <Typography.Paragraph type="secondary">Bảng giá riêng của chi nhánh thay thế bảng giá toàn chuỗi cùng thời lượng. Chỉ Admin sửa được bảng giá toàn chuỗi.</Typography.Paragraph>
      <Table rowKey="id" size="middle" loading={plans.isLoading} dataSource={plans.data} pagination={false} scroll={{ x: "max-content" }}
        columns={[
          { title: "Tên", dataIndex: "name" },
          { title: "Phạm vi", dataIndex: "branchId", render: (b) => (b ? "Chi nhánh" : "Toàn chuỗi") },
          { title: "Thời lượng", dataIndex: "durationMinutes", render: (m: number) => `${m / 60} giờ` },
          { title: "Giá", dataIndex: "price", render: (v: number) => vnd(v) },
          { title: "Gia hạn", dataIndex: "extendPrice", render: (v: number) => vnd(v) },
          { title: "Hiệu lực", render: (_, p) => `${dateTime(p.validFrom)} → ${dateTime(p.validTo)}` },
          { title: "", dataIndex: "isActive", render: (a: boolean) => (a ? <Tag color="green">Đang áp dụng</Tag> : <Tag>Hết hiệu lực</Tag>) },
          { title: "", render: (_, p) => (p.branchId || isAdmin) && <Button size="small" onClick={() => open(p)}>Sửa</Button> },
        ]} />
      <Modal open={!!editing} title={editing === "new" ? "Thêm bảng giá" : "Sửa bảng giá"} onCancel={() => setEditing(null)} onOk={() => form.submit()} confirmLoading={save.isPending}>
        <Form form={form} layout="vertical" onFinish={(f) => save.mutate(f)}>
          <Form.Item label="Phạm vi" name="scope">
            <Select options={[{ value: "branch", label: "Chi nhánh đang chọn" }, ...(isAdmin ? [{ value: "chain", label: "Toàn chuỗi" }] : [])]} />
          </Form.Item>
          <Form.Item label="Tên" name="name" rules={[{ required: true, max: 150 }]}><Input /></Form.Item>
          <Form.Item label="Thời lượng (phút)" name="durationMinutes" rules={[{ required: true }]}><InputNumber min={15} max={1440} step={15} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Giá (VND)" name="price" rules={[{ required: true }]}><InputNumber min={0} step={1000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Giá gia hạn (VND)" name="extendPrice" rules={[{ required: true }]}><InputNumber min={0} step={1000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Hiệu lực từ" name="validFrom"><DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Hiệu lực đến" name="validTo"><DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: "100%" }} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
