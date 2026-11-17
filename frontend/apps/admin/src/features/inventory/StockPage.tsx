import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Card, DatePicker, Descriptions, Form, Input, InputNumber, Modal, Radio, Segmented, Select, Space, Switch, Table, Tabs, Tag, Typography } from "antd";
import { DeleteOutlined, MinusCircleOutlined, PlusOutlined } from "@ant-design/icons";
import dayjs from "dayjs";
import type { CountSummary, InventoryItem, ReceiptLineInput, StockDocument, StockDocumentType, WarehouseType } from "@cafecr/api-client";
import {
  ConfirmDialog, CountSummaryTable, QtyStepper, addDays, dateTime, stockDocumentStatusLabels, stockDocumentTypeLabels, todayVn, unitsText, vnd,
} from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function StockPage() {
  return <RequireBranch title="Kho">{(branchId) => <Stock branchId={branchId} />}</RequireBranch>;
}

function Stock({ branchId }: { branchId: string }) {
  return (
    <Tabs items={[
      { key: "balances", label: "Tồn kho", children: <Balances branchId={branchId} /> },
      { key: "documents", label: "Phiếu kho", children: <Documents branchId={branchId} /> },
      { key: "inner-count", label: "Kiểm kê kho trong", children: <InnerCount branchId={branchId} /> },
    ]} />
  );
}

function Balances({ branchId }: { branchId: string }) {
  const [warehouse, setWarehouse] = useState<WarehouseType>("Inner");
  const items = useQuery({ queryKey: ["inventory-items"], queryFn: () => api.inventory.items() });
  const balances = useQuery({ queryKey: ["balances", branchId, warehouse], queryFn: () => api.inventory.balances(branchId, warehouse), refetchInterval: 60_000 });
  const units = (id: string) => items.data?.find((i) => i.id === id)?.units ?? [];
  const [receipt, setReceipt] = useState(false);
  const [waste, setWaste] = useState(false);

  return (
    <Card title="Tồn kho" extra={
      <Space>
        <Segmented value={warehouse} onChange={(v) => setWarehouse(v as WarehouseType)} options={[{ value: "Inner", label: "Kho trong (theo sổ)" }, { value: "Outer", label: "Kho ngoài (dự kiến)" }]} />
        <Button danger icon={<DeleteOutlined />} onClick={() => setWaste(true)}>Lập phiếu huỷ</Button>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setReceipt(true)}>Lập phiếu nhập</Button>
      </Space>
    }>
      {warehouse === "Outer" && <Alert style={{ marginBottom: 12 }} type="info" showIcon
        message="Kho ngoài: tồn sổ = số đếm gần nhất + chuyển vào − huỷ; dự kiến trừ thêm lượt bán × định mức. Số thật chỉ biết khi đếm kết ca." />}
      <Table rowKey="itemId" size="middle" loading={balances.isLoading} dataSource={balances.data} pagination={false}
        columns={[
          { title: "Mặt hàng", dataIndex: "itemName" },
          { title: "Tồn sổ", dataIndex: "qty", render: (v: number, r) => <>{v} {r.baseUnit} <Typography.Text type="secondary">{units(r.itemId).length ? `(${unitsText(v, r.baseUnit, units(r.itemId))})` : ""}</Typography.Text></> },
          ...(warehouse === "Outer" ? [{ title: "Dự kiến lúc này", dataIndex: "expectedNow", render: (v: number | null) => v ?? "—" }, { title: "Max", dataIndex: "par" }] : []),
          { title: "Min", dataIndex: "min" },
          { title: "", render: (_, r) => (r.belowMin ? <Tag color="red">{warehouse === "Inner" ? "Nên đặt hàng" : "Dưới mức tối thiểu"}</Tag> : null) },
        ]} />
      {receipt && <ReceiptModal branchId={branchId} items={items.data ?? []} onClose={() => setReceipt(false)} />}
      {waste && <WasteModal branchId={branchId} warehouse={warehouse} items={items.data ?? []} onClose={() => setWaste(false)} />}
    </Card>
  );
}

type ReceiptForm = { supplier?: string; invoiceNo?: string; photoUrl?: string; note?: string; paid: boolean; method: "Cash" | "VietQr"; lines: ReceiptLineInput[] };

function ReceiptModal({ branchId, items, onClose }: { branchId: string; items: InventoryItem[]; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<ReceiptForm>();
  const lines = Form.useWatch("lines", form) ?? [];
  const paid = Form.useWatch("paid", form);
  const total = lines.reduce((s, l) => s + (l?.unitQty ?? 0) * (l?.unitCost ?? 0), 0);
  const create = useMutation({
    mutationFn: (v: ReceiptForm) => api.inventory.createReceipt({
      branchId, supplier: v.supplier ?? null, invoiceNo: v.invoiceNo ?? null, photoUrl: v.photoUrl ?? null, note: v.note ?? null,
      paid: v.paid, method: v.paid ? v.method : null,
      lines: v.lines.map((l) => ({ ...l, unitName: l.unitName || null, expiryDate: l.expiryDate ? dayjs(l.expiryDate).format("YYYY-MM-DD") : null })),
    }),
    onSuccess: (doc) => {
      notify.ok(`Đã nhập kho ${doc.code}${doc.expenseId ? " và ghi khoản chi Nguyên liệu" : ""}.`);
      void queryClient.invalidateQueries({ queryKey: ["balances", branchId] });
      void queryClient.invalidateQueries({ queryKey: ["stock-documents", branchId] });
      onClose();
    },
    onError: notify.fail,
  });

  return (
    <Modal open width={860} title="Phiếu nhập kho trong" okText="Nhập kho" cancelText="Huỷ" onCancel={onClose} onOk={() => form.submit()} confirmLoading={create.isPending}>
      <Form form={form} layout="vertical" initialValues={{ paid: true, method: "Cash", lines: [{ itemId: undefined, unitName: null, unitQty: 1, unitCost: 0 }] }}
        onFinish={(v) => create.mutate(v)}>
        <Space wrap>
          <Form.Item label="Nhà cung cấp" name="supplier"><Input style={{ width: 220 }} /></Form.Item>
          <Form.Item label="Số hoá đơn" name="invoiceNo"><Input style={{ width: 160 }} /></Form.Item>
          <Form.Item label="Ảnh hoá đơn (link)" name="photoUrl"><Input style={{ width: 260 }} placeholder="https://…" /></Form.Item>
        </Space>
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((f) => {
                const item = items.find((i) => i.id === lines[f.name]?.itemId);
                return (
                  <Space key={f.key} align="baseline" wrap>
                    <Form.Item name={[f.name, "itemId"]} rules={[{ required: true, message: "Chọn mặt hàng" }]}>
                      <Select style={{ width: 200 }} placeholder="Mặt hàng" showSearch optionFilterProp="label" options={items.map((i) => ({ value: i.id, label: i.name }))} />
                    </Form.Item>
                    <Form.Item name={[f.name, "unitQty"]} rules={[{ required: true }]}><InputNumber min={1} style={{ width: 90 }} /></Form.Item>
                    <Form.Item name={[f.name, "unitName"]}>
                      <Select style={{ width: 120 }} options={[{ value: null, label: item?.baseUnit ?? "đơn vị gốc" }, ...(item?.units ?? []).map((u) => ({ value: u.unitName, label: `${u.unitName} (${u.factor})` }))]} />
                    </Form.Item>
                    <Form.Item name={[f.name, "unitCost"]} rules={[{ required: true }]}><InputNumber min={0} step={1000} style={{ width: 130 }} addonAfter="đ" /></Form.Item>
                    <Form.Item name={[f.name, "expiryDate"]}><DatePicker placeholder="Hạn dùng" format="DD/MM/YYYY" /></Form.Item>
                    {fields.length > 1 && <MinusCircleOutlined onClick={() => remove(f.name)} />}
                  </Space>
                );
              })}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add({ unitName: null, unitQty: 1, unitCost: 0 })}>Thêm dòng</Button>
            </>
          )}
        </Form.List>
        <Space style={{ marginTop: 16 }} size="large" align="baseline">
          <Typography.Text strong>Tổng tiền: {vnd(total)}</Typography.Text>
          <Form.Item label="Đã thanh toán" name="paid" valuePropName="checked" style={{ marginBottom: 0 }}><Switch /></Form.Item>
          {paid && (
            <Form.Item name="method" style={{ marginBottom: 0 }}>
              <Radio.Group options={[{ value: "Cash", label: "Tiền mặt" }, { value: "VietQr", label: "Chuyển khoản" }]} />
            </Form.Item>
          )}
        </Space>
        <Form.Item label="Ghi chú" name="note" style={{ marginTop: 12 }}><Input.TextArea maxLength={1000} /></Form.Item>
      </Form>
    </Modal>
  );
}

type WasteForm = { warehouse: WarehouseType; reason: string; photoUrl?: string; lines: { itemId?: string; unitName: string | null; unitQty: number }[] };

/** Phiếu huỷ (PH): spoiled / broken stock. The inner book cannot go negative (409 INSUFFICIENT_STOCK). */
function WasteModal({ branchId, warehouse, items, onClose }: { branchId: string; warehouse: WarehouseType; items: InventoryItem[]; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [form] = Form.useForm<WasteForm>();
  const lines = Form.useWatch("lines", form) ?? [];
  const selected = Form.useWatch("warehouse", form) ?? warehouse;
  const balances = useQuery({ queryKey: ["balances", branchId, selected], queryFn: () => api.inventory.balances(branchId, selected) });
  const onHand = (itemId?: string) => balances.data?.find((b) => b.itemId === itemId)?.qty;
  const factor = (item: InventoryItem | undefined, unitName: string | null) => item?.units.find((u) => u.unitName === unitName)?.factor ?? 1;
  const create = useMutation({
    mutationFn: (v: WasteForm) => api.inventory.createWaste(branchId, v.warehouse,
      v.lines.map((l) => ({ itemId: l.itemId!, qty: l.unitQty * factor(items.find((i) => i.id === l.itemId), l.unitName) })),
      v.reason.trim(), v.photoUrl?.trim() || null),
    onSuccess: (doc) => {
      notify.ok(`Đã ghi phiếu huỷ ${doc.code}.`);
      void queryClient.invalidateQueries({ queryKey: ["balances", branchId] });
      void queryClient.invalidateQueries({ queryKey: ["stock-documents", branchId] });
      onClose();
    },
    onError: notify.fail,
  });

  return (
    <Modal open width={720} title="Phiếu huỷ hàng hỏng / đổ vỡ" okText="Ghi huỷ" okButtonProps={{ danger: true }} cancelText="Đóng" onCancel={onClose}
      onOk={() => form.submit()} confirmLoading={create.isPending}>
      <Form form={form} layout="vertical" initialValues={{ warehouse, lines: [{ itemId: undefined, unitName: null, unitQty: 1 }] }}
        onFinish={(v) => create.mutate(v)}>
        <Form.Item label="Kho" name="warehouse">
          <Radio.Group options={[{ value: "Inner", label: "Kho trong" }, { value: "Outer", label: "Kho ngoài" }]} />
        </Form.Item>
        <Form.List name="lines">
          {(fields, { add, remove }) => (
            <>
              {fields.map((f) => {
                const item = items.find((i) => i.id === lines[f.name]?.itemId);
                const qty = onHand(item?.id);
                return (
                  <Space key={f.key} align="baseline" wrap>
                    <Form.Item name={[f.name, "itemId"]} rules={[{ required: true, message: "Chọn mặt hàng" }]}>
                      <Select style={{ width: 220 }} placeholder="Mặt hàng" showSearch optionFilterProp="label" options={items.map((i) => ({ value: i.id, label: i.name }))} />
                    </Form.Item>
                    <Form.Item name={[f.name, "unitQty"]} rules={[{ required: true }]}><InputNumber min={1} style={{ width: 90 }} /></Form.Item>
                    <Form.Item name={[f.name, "unitName"]}>
                      <Select style={{ width: 120 }} options={[{ value: null, label: item?.baseUnit ?? "đơn vị gốc" }, ...(item?.units ?? []).map((u) => ({ value: u.unitName, label: `${u.unitName} (${u.factor})` }))]} />
                    </Form.Item>
                    {item && qty !== undefined && <Typography.Text type="secondary">Tồn sổ {qty} {item.baseUnit}</Typography.Text>}
                    {fields.length > 1 && <MinusCircleOutlined onClick={() => remove(f.name)} />}
                  </Space>
                );
              })}
              <Button type="dashed" icon={<PlusOutlined />} onClick={() => add({ unitName: null, unitQty: 1 })}>Thêm dòng</Button>
            </>
          )}
        </Form.List>
        <Form.Item label="Lý do" name="reason" style={{ marginTop: 12 }} rules={[{ required: true, whitespace: true, message: "Phiếu huỷ cần ghi lý do" }]}>
          <Input.TextArea maxLength={1000} placeholder="Ví dụ: hết hạn, đổ vỡ khi vận chuyển" />
        </Form.Item>
        <Form.Item label="Ảnh (link)" name="photoUrl"><Input placeholder="https://…" /></Form.Item>
      </Form>
    </Modal>
  );
}

function Documents({ branchId }: { branchId: string }) {
  const [type, setType] = useState<StockDocumentType | undefined>();
  const [range, setRange] = useState<[string, string]>(() => [addDays(todayVn(), -7), todayVn()]);
  const docs = useQuery({ queryKey: ["stock-documents", branchId, type, range], queryFn: () => api.inventory.documents(branchId, range[0], range[1], type) });
  const [open, setOpen] = useState<StockDocument | null>(null);

  return (
    <Card title="Phiếu kho" extra={
      <Space wrap>
        <Select allowClear placeholder="Loại phiếu" style={{ width: 170 }} value={type} onChange={setType}
          options={(["Receipt", "Transfer", "Waste", "Adjustment"] as StockDocumentType[]).map((t) => ({ value: t, label: stockDocumentTypeLabels[t] }))} />
        <DatePicker.RangePicker value={[dayjs(range[0]), dayjs(range[1])]} format="DD/MM/YYYY"
          onChange={(v) => v?.[0] && v[1] && setRange([v[0].format("YYYY-MM-DD"), v[1].format("YYYY-MM-DD")])} />
      </Space>
    }>
      <Table<StockDocument> rowKey="id" size="middle" loading={docs.isLoading} dataSource={docs.data?.filter((d) => d.type !== "Count")} scroll={{ x: "max-content" }}
        onRow={(r) => ({ onClick: () => setOpen(r), style: { cursor: "pointer" } })}
        columns={[
          { title: "Mã", dataIndex: "code" },
          { title: "Loại", dataIndex: "type", render: (t: StockDocumentType) => stockDocumentTypeLabels[t] },
          { title: "Trạng thái", dataIndex: "status", render: (s: StockDocument["status"]) => <Tag color={stockDocumentStatusLabels[s].color}>{stockDocumentStatusLabels[s].label}</Tag> },
          { title: "Lúc", dataIndex: "createdAt", render: dateTime },
          { title: "Người lập", dataIndex: "createdByName" },
          { title: "Nội dung", render: (_, d) => d.lines.map((l) => `${l.itemName} ${l.qty > 0 && d.type === "Adjustment" ? "+" : ""}${l.qty}`).join(", ") },
          { title: "Lý do / ghi chú", render: (_, d) => d.reason ?? d.note ?? d.supplier ?? "" },
        ]} />
      {open && <DocumentModal doc={open} onClose={() => setOpen(null)} />}
    </Card>
  );
}

function DocumentModal({ doc, onClose }: { doc: StockDocument; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [delta, setDelta] = useState<Record<string, number>>({});
  const [reason, setReason] = useState("");
  const lines = Object.entries(delta).filter(([, v]) => v !== 0).map(([itemId, qtyDelta]) => ({ itemId, qtyDelta }));
  const adjust = useMutation({
    mutationFn: () => api.inventory.createAdjustment(doc.id, lines, reason.trim()),
    onSuccess: (dc) => {
      notify.ok(`Đã lập phiếu điều chỉnh ${dc.code}.`);
      void queryClient.invalidateQueries({ queryKey: ["stock-documents", doc.branchId] });
      void queryClient.invalidateQueries({ queryKey: ["balances", doc.branchId] });
      onClose();
    },
    onError: notify.fail,
  });
  const canAdjust = doc.type !== "Adjustment" && (doc.status === "Confirmed" || doc.status === "Approved");

  return (
    <Modal open width={760} title={`${stockDocumentTypeLabels[doc.type]} ${doc.code}`} onCancel={onClose} footer={canAdjust ? [
      <Button key="close" onClick={onClose}>Đóng</Button>,
      <Button key="dc" type="primary" disabled={lines.length === 0 || !reason.trim()} loading={adjust.isPending} onClick={() => adjust.mutate()}>Lập phiếu điều chỉnh</Button>,
    ] : null}>
      <Descriptions size="small" column={2} items={[
        { label: "Trạng thái", children: stockDocumentStatusLabels[doc.status].label },
        { label: "Lúc", children: dateTime(doc.createdAt) },
        { label: "Người lập", children: doc.createdByName },
        ...(doc.supplier ? [{ label: "Nhà cung cấp", children: doc.supplier }] : []),
        ...(doc.invoiceNo ? [{ label: "Số hoá đơn", children: doc.invoiceNo }] : []),
        ...(doc.total !== null ? [{ label: "Tổng tiền", children: `${vnd(doc.total)}${doc.expenseId ? " · đã ghi sổ chi" : ""}` }] : []),
        ...(doc.photoUrl ? [{ label: "Ảnh hoá đơn", children: <a href={doc.photoUrl} target="_blank" rel="noreferrer">Mở ảnh</a> }] : []),
        ...(doc.reason ? [{ label: "Lý do", children: doc.reason }] : []),
      ]} />
      <Table rowKey="itemId" size="small" pagination={false} dataSource={doc.lines} style={{ marginTop: 12 }}
        columns={[
          { title: "Mặt hàng", dataIndex: "itemName" },
          { title: "Số lượng", render: (_, l) => `${l.qty} ${l.baseUnit}${l.unitName ? ` (${l.unitQty} ${l.unitName})` : ""}` },
          ...(doc.type === "Transfer" ? [{ title: "Gợi ý", dataIndex: "suggestedQty" }] : []),
          ...(doc.type === "Receipt" ? [{ title: "Đơn giá", dataIndex: "unitCost", render: (v: number | null) => (v === null ? "—" : vnd(v)) }] : []),
          ...(canAdjust ? [{ title: "Điều chỉnh ±", render: (_: unknown, l: StockDocument["lines"][number]) => (
            <InputNumber value={delta[l.itemId] ?? 0} onChange={(v) => setDelta({ ...delta, [l.itemId]: Number(v ?? 0) })} style={{ width: 100 }} />
          ) }] : []),
        ]} />
      {canAdjust && (
        <>
          <Typography.Paragraph type="secondary" style={{ marginTop: 12 }}>
            Phiếu đã khoá không sửa được. Nhập số chênh (±) so với phiếu gốc để lập phiếu điều chỉnh (DC); tồn kho được ghi theo DC.
          </Typography.Paragraph>
          <Input.TextArea rows={2} placeholder="Lý do điều chỉnh (bắt buộc)" value={reason} onChange={(e) => setReason(e.target.value)} />
        </>
      )}
    </Modal>
  );
}

/** Weekly blind count of the storeroom: open → count → summary → confirm (popup). */
function InnerCount({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [sheetId, setSheetId] = useState<string | null>(null);
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [summary, setSummary] = useState<CountSummary | null>(null);
  const [asking, setAsking] = useState(false);
  const open = useMutation({
    mutationFn: () => api.inventory.openInnerCount(branchId),
    onSuccess: (s) => { setSheetId(s.countId); setCounts(Object.fromEntries(s.items.map((i) => [i.itemId, i.countedQty ?? 0]))); setSummary(null); },
    onError: notify.fail,
  });
  const sheet = open.data;
  const review = useMutation({
    mutationFn: async () => {
      await api.inventory.saveCountLines("Inner", sheetId!, sheet!.items.map((i) => ({ itemId: i.itemId, countedQty: counts[i.itemId] ?? 0 })));
      return api.inventory.countSummary("Inner", sheetId!);
    },
    onSuccess: setSummary,
    onError: notify.fail,
  });
  const confirm = useMutation({
    mutationFn: (reason: string) => api.inventory.confirmCount("Inner", sheetId!, reason || null),
    onSuccess: () => {
      notify.ok("Đã khoá phiếu kiểm kê kho trong.");
      setAsking(false); setSheetId(null); setSummary(null); open.reset();
      void queryClient.invalidateQueries({ queryKey: ["balances", branchId] });
    },
    onError: notify.fail,
  });

  if (!sheet) {
    return (
      <Card title="Kiểm kê kho trong">
        <Typography.Paragraph>Đếm mù toàn bộ kho trong (không hiện tồn sổ khi đếm). Sau khi xác nhận, tồn sổ được đưa về đúng số đếm; job đêm đối soát với kho ngoài.</Typography.Paragraph>
        <Button type="primary" loading={open.isPending} onClick={() => open.mutate()}>Bắt đầu kiểm kê</Button>
      </Card>
    );
  }

  const differs = summary?.rows.filter((r) => r.variance !== 0) ?? [];
  return (
    <Card title={`Kiểm kê kho trong · ${sheet.code}`}>
      {!summary ? (
        <Space direction="vertical" style={{ width: "100%" }}>
          <Table rowKey="itemId" size="small" pagination={false} dataSource={sheet.items}
            columns={[
              { title: "Mặt hàng", dataIndex: "name" },
              { title: "Số đếm", render: (_, i) => <QtyStepper size="middle" value={counts[i.itemId] ?? 0} baseUnit={i.baseUnit} units={i.units} onChange={(v) => setCounts({ ...counts, [i.itemId]: v })} /> },
            ]} />
          <Button type="primary" loading={review.isPending} onClick={() => review.mutate()}>Xem tổng hợp</Button>
        </Space>
      ) : (
        <Space direction="vertical" style={{ width: "100%" }}>
          <CountSummaryTable summary={summary} />
          <Space>
            <Button onClick={() => setSummary(null)}>Đếm lại</Button>
            <Button type="primary" onClick={() => setAsking(true)}>Xác nhận số đếm</Button>
          </Space>
        </Space>
      )}
      <ConfirmDialog open={asking} title="Xác nhận kiểm kê kho trong?" busy={confirm.isPending}
        body="Sau khi xác nhận sẽ không sửa được; tồn sổ kho trong sẽ bằng số đếm." requireReason={differs.length > 0} reasonLabel="Lý do chênh lệch"
        onCancel={() => setAsking(false)} onConfirm={(r) => confirm.mutate(r)}>
        {differs.length > 0 && <Alert type="warning" showIcon message={`${differs.length} món lệch sổ: ${differs.map((r) => `${r.name} ${r.variance! > 0 ? "+" : ""}${r.variance}`).join(", ")}`} />}
      </ConfirmDialog>
    </Card>
  );
}
