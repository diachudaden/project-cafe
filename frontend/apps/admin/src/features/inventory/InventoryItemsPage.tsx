import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Form, Input, InputNumber, Modal, Space, Switch, Table, Tabs, Tag, Tooltip, Typography } from "antd";
import { MinusCircleOutlined, PlusOutlined } from "@ant-design/icons";
import type { BranchSetting, InventoryItem, InventoryItemInput } from "@cafecr/api-client";
import { dateTime } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

/** Danh mục mặt hàng + đơn vị quy đổi, và định mức / min / par theo chi nhánh (TASK-027). */
export function InventoryItemsPage() {
  return (
    <Tabs items={[
      { key: "items", label: "Mặt hàng", children: <Items /> },
      { key: "settings", label: "Cấu hình chi nhánh", children: <RequireBranch title="Cấu hình chi nhánh">{(id) => <Settings branchId={id} />}</RequireBranch> },
    ]} />
  );
}

function Items() {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const items = useQuery({ queryKey: ["inventory-items", "all"], queryFn: () => api.inventory.items(true) });
  const [editing, setEditing] = useState<InventoryItem | "new" | null>(null);
  const [form] = Form.useForm<InventoryItemInput>();
  const save = useMutation({
    mutationFn: (v: InventoryItemInput) => {
      const input = { ...v, units: v.units ?? [] };
      return editing && editing !== "new" ? api.inventory.updateItem(editing.id, input) : api.inventory.createItem(input);
    },
    onSuccess: () => {
      notify.ok("Đã lưu mặt hàng.");
      setEditing(null);
      void queryClient.invalidateQueries({ queryKey: ["inventory-items"] });
    },
    onError: notify.fail,
  });
  const open = (item: InventoryItem | "new") => {
    setEditing(item);
    form.setFieldsValue(item === "new"
      ? { name: "", baseUnit: "gói", category: "Đồ uống", countDaily: true, sortOrder: (items.data?.length ?? 0) + 1, isActive: true, units: [] }
      : item);
  };

  return (
    <Card title="Mặt hàng kho" extra={<Button type="primary" icon={<PlusOutlined />} onClick={() => open("new")}>Thêm mặt hàng</Button>}>
      <Table<InventoryItem> rowKey="id" size="middle" loading={items.isLoading} dataSource={items.data} pagination={false} scroll={{ x: "max-content" }}
        columns={[
          { title: "Thứ tự kệ", dataIndex: "sortOrder", width: 90 },
          { title: "Tên", dataIndex: "name", render: (v, r) => <Space>{v}{!r.isActive && <Tag>Ngừng dùng</Tag>}</Space> },
          { title: "Nhóm", dataIndex: "category" },
          { title: "Đơn vị gốc", dataIndex: "baseUnit" },
          { title: "Quy đổi", render: (_, r) => r.units.map((u) => `1 ${u.unitName} = ${u.factor} ${r.baseUnit}`).join(" · ") || "—" },
          { title: "Đếm kết ca", dataIndex: "countDaily", render: (v: boolean) => (v ? <Tag color="blue">Mỗi ca</Tag> : <Tag>Xoay vòng</Tag>) },
          { title: "", render: (_, r) => <Button size="small" onClick={() => open(r)}>Sửa</Button> },
        ]} />
      <Modal open={editing !== null} title={editing === "new" ? "Thêm mặt hàng" : "Sửa mặt hàng"} okText="Lưu mặt hàng" cancelText="Huỷ"
        onCancel={() => setEditing(null)} onOk={() => form.submit()} confirmLoading={save.isPending} destroyOnClose>
        <Form form={form} layout="vertical" onFinish={(v) => save.mutate(v)}>
          <Form.Item label="Tên" name="name" rules={[{ required: true, max: 100 }]}><Input /></Form.Item>
          <Space>
            <Form.Item label="Đơn vị gốc" name="baseUnit" rules={[{ required: true, max: 30 }]}><Input style={{ width: 120 }} /></Form.Item>
            <Form.Item label="Nhóm" name="category" rules={[{ required: true, max: 50 }]}><Input style={{ width: 160 }} /></Form.Item>
            <Form.Item label="Thứ tự kệ" name="sortOrder"><InputNumber min={0} /></Form.Item>
          </Space>
          <Space size="large">
            <Form.Item label="Đếm mỗi ca" name="countDaily" valuePropName="checked"><Switch /></Form.Item>
            <Form.Item label="Đang dùng" name="isActive" valuePropName="checked"><Switch /></Form.Item>
          </Space>
          <Typography.Text strong>Đơn vị quy đổi</Typography.Text>
          <Form.List name="units">
            {(fields, { add, remove }) => (
              <>
                {fields.map((f) => (
                  <Space key={f.key} align="baseline">
                    <Form.Item name={[f.name, "unitName"]} rules={[{ required: true }]}><Input placeholder="Thùng / Hộp" style={{ width: 140 }} /></Form.Item>
                    <Form.Item name={[f.name, "factor"]} rules={[{ required: true }]}><InputNumber min={2} placeholder="Số đơn vị gốc" style={{ width: 150 }} /></Form.Item>
                    <MinusCircleOutlined onClick={() => remove(f.name)} />
                  </Space>
                ))}
                <Button type="dashed" onClick={() => add({ unitName: "", factor: 10 })} icon={<PlusOutlined />}>Thêm đơn vị</Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>
    </Card>
  );
}

function Settings({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const settings = useQuery({ queryKey: ["inventory-settings", branchId], queryFn: () => api.inventory.settings(branchId) });
  const [draft, setDraft] = useState<Record<string, Partial<BranchSetting>>>({});
  const save = useMutation({
    mutationFn: (s: BranchSetting) => {
      const v = { ...s, ...draft[s.itemId] };
      return api.inventory.saveSetting(s.itemId, branchId, {
        normPerSession: v.normPerSession, normOverride: v.normOverride ?? null, outerMin: v.outerMin ?? 0, outerPar: v.outerPar ?? 0, innerMin: v.innerMin ?? 0,
      });
    },
    onSuccess: (_, s) => {
      notify.ok(`Đã lưu cấu hình ${s.itemName}.`);
      setDraft((d) => { const n = { ...d }; delete n[s.itemId]; return n; });
      void queryClient.invalidateQueries({ queryKey: ["inventory-settings", branchId] });
    },
    onError: notify.fail,
  });
  const field = (s: BranchSetting, key: keyof BranchSetting, props: { min?: number; step?: number; allowNull?: boolean } = {}) => (
    <InputNumber min={props.min ?? 0} step={props.step ?? 1} style={{ width: 100 }}
      value={(draft[s.itemId]?.[key] ?? s[key]) as number | null}
      onChange={(v) => setDraft({ ...draft, [s.itemId]: { ...draft[s.itemId], [key]: v ?? (props.allowNull ? null : 0) } })} />
  );

  return (
    <Card title="Định mức và mức tồn theo chi nhánh">
      <Typography.Paragraph type="secondary">
        Định mức = số đơn vị gốc dùng cho mỗi lượt bán / gia hạn. Job đêm tự học định mức (trung vị 14 ngày, tối đa ±30%/lần); định mức đặt tay luôn được ưu tiên.
      </Typography.Paragraph>
      <Table<BranchSetting> rowKey="itemId" size="middle" loading={settings.isLoading} dataSource={settings.data} pagination={false} scroll={{ x: "max-content" }}
        columns={[
          { title: "Mặt hàng", dataIndex: "itemName", render: (v, r) => <>{v} <Typography.Text type="secondary">({r.baseUnit})</Typography.Text></> },
          { title: <Tooltip title="Định mức tự học từ phiếu đếm đã khoá">Định mức tự học</Tooltip>, render: (_, r) => (
            <Space direction="vertical" size={0}>{field(r, "normPerSession", { step: 0.1 })}
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>{r.normLearnedAt ? `học lúc ${dateTime(r.normLearnedAt)}` : "chưa học"}</Typography.Text></Space>
          ) },
          { title: "Định mức đặt tay", render: (_, r) => field(r, "normOverride", { step: 0.1, allowNull: true }) },
          { title: "Đang dùng", dataIndex: "effectiveNorm", render: (v: number) => <b>{v}</b> },
          { title: "Min kho ngoài", render: (_, r) => field(r, "outerMin") },
          { title: "Max kho ngoài", render: (_, r) => field(r, "outerPar") },
          { title: "Min kho trong", render: (_, r) => field(r, "innerMin") },
          { title: "", render: (_, r) => <Button size="small" type="primary" disabled={!draft[r.itemId]} loading={save.isPending && save.variables?.itemId === r.itemId}
            onClick={() => save.mutate(r)}>Lưu</Button> },
        ]} />
    </Card>
  );
}
