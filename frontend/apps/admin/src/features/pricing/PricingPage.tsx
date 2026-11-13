import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AutoComplete, Button, Card, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Switch, Table, Tag, Typography } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import type { PricePlanView, ProductView } from "@cafecr/api-client";
import { dateTime, hasRole, vnd } from "@cafecr/ui";
import { api, useAuth } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

interface PlanForm { scope: "branch" | "chain"; name: string; durationMinutes: number; price: number; extendPrice: number; validFrom?: Dayjs | null; validTo?: Dayjs | null }

export function PricingPage() {
  return (
    <RequireBranch title="Bảng giá">
      {(branchId) => (
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
          <Pricing branchId={branchId} />
          <Products branchId={branchId} />
        </Space>
      )}
    </RequireBranch>
  );
}

interface ProductForm { scope: "branch" | "chain"; name: string; category: string; price: number; isActive: boolean; sortOrder: number }

/** Add-on menu (mì, trứng, bánh, nước...) sold at the POS on top of a seat's bill. */
function Products({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const isAdmin = hasRole(useAuth().state.user, "Admin");
  const [editing, setEditing] = useState<ProductView | "new" | null>(null);
  const [form] = Form.useForm<ProductForm>();
  const products = useQuery({ queryKey: ["products-all", branchId], queryFn: () => api.catalog.allProducts(branchId) });
  const categories = [...new Set([...(products.data ?? []).map((p) => p.category), "Đồ ăn", "Đồ uống"])];
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["products-all"] });
  const save = useMutation({
    mutationFn: (f: ProductForm) => {
      const input = { branchId: f.scope === "chain" ? null : branchId, name: f.name, category: f.category, price: f.price, isActive: f.isActive, sortOrder: f.sortOrder ?? 0 };
      return editing === "new" ? api.catalog.createProduct(input) : api.catalog.updateProduct((editing as ProductView).id, input);
    },
    onSuccess: () => { notify.ok("Đã lưu món."); setEditing(null); refresh(); },
    onError: notify.fail,
  });
  const remove = useMutation({
    mutationFn: (id: string) => api.catalog.deleteProduct(id),
    onSuccess: () => { notify.ok("Đã xóa (món đã từng bán được chuyển sang ngừng bán)."); refresh(); },
    onError: notify.fail,
  });
  const open = (p: ProductView | "new") => {
    form.setFieldsValue(p === "new"
      ? { scope: "branch", name: "", category: "Đồ ăn", price: 10000, isActive: true, sortOrder: 0 }
      : { scope: p.branchId ? "branch" : "chain", name: p.name, category: p.category, price: p.price, isActive: p.isActive, sortOrder: p.sortOrder });
    setEditing(p);
  };
  const canEdit = (p: ProductView) => !!p.branchId || isAdmin;

  return (
    <Card title="Đồ thêm (mì, trứng, bánh, nước…)" extra={<Button type="primary" onClick={() => open("new")}>Thêm món</Button>}>
      <Typography.Paragraph type="secondary">
        Nhân viên POS bán các món này cho khách đang ngồi; tiền được cộng vào bill hiện tại, không ảnh hưởng thời gian ngồi. Chỉ Admin sửa được món toàn chuỗi.
      </Typography.Paragraph>
      <Table rowKey="id" size="middle" loading={products.isLoading} dataSource={products.data} pagination={false} scroll={{ x: "max-content" }}
        columns={[
          { title: "Tên", dataIndex: "name" },
          { title: "Nhóm", dataIndex: "category", render: (c: string) => <Tag>{c}</Tag> },
          { title: "Giá", dataIndex: "price", render: (v: number) => vnd(v) },
          { title: "Phạm vi", dataIndex: "branchId", render: (b) => (b ? "Chi nhánh" : "Toàn chuỗi") },
          { title: "Thứ tự", dataIndex: "sortOrder" },
          { title: "", dataIndex: "isActive", render: (a: boolean) => (a ? <Tag color="green">Đang bán</Tag> : <Tag>Ngừng bán</Tag>) },
          {
            title: "", render: (_, p) => canEdit(p) && (
              <Space>
                <Button size="small" onClick={() => open(p)}>Sửa</Button>
                <Popconfirm title="Xóa món này?" description="Món đã từng bán sẽ chỉ chuyển sang ngừng bán." okText="Xóa" cancelText="Hủy" onConfirm={() => remove.mutate(p.id)}>
                  <Button size="small" danger>Xóa</Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]} />
      <Modal open={!!editing} title={editing === "new" ? "Thêm món" : "Sửa món"} onCancel={() => setEditing(null)} onOk={() => form.submit()} confirmLoading={save.isPending} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={(f) => save.mutate(f)}>
          <Form.Item label="Phạm vi" name="scope">
            <Select options={[{ value: "branch", label: "Chi nhánh đang chọn" }, ...(isAdmin ? [{ value: "chain", label: "Toàn chuỗi" }] : [])]} />
          </Form.Item>
          <Form.Item label="Tên món" name="name" rules={[{ required: true, max: 100 }]}><Input placeholder="Mì ly, Trứng ốp la…" /></Form.Item>
          <Form.Item label="Nhóm" name="category" rules={[{ required: true, max: 50 }]}>
            <AutoComplete options={categories.map((c) => ({ value: c }))} filterOption={(input, o) => (o?.value ?? "").toLowerCase().includes(input.toLowerCase())} />
          </Form.Item>
          <Form.Item label="Giá (VND)" name="price" rules={[{ required: true }]}><InputNumber min={0} step={1000} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Thứ tự hiển thị" name="sortOrder"><InputNumber min={0} max={999} style={{ width: "100%" }} /></Form.Item>
          <Form.Item label="Đang bán" name="isActive" valuePropName="checked"><Switch /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
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
      <Typography.Paragraph type="secondary">POS hiển thị tất cả bảng giá đang áp dụng (riêng chi nhánh và toàn chuỗi) để nhân viên chọn khi check-in. Chỉ Admin sửa được bảng giá toàn chuỗi.</Typography.Paragraph>
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
