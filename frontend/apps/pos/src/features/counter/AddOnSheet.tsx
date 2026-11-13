import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Drawer, Empty, Input, List, Modal, Radio, Segmented, Space, Typography } from "antd";
import { MinusOutlined, PlusOutlined, SearchOutlined } from "@ant-design/icons";
import type { PaymentMethod, PaymentRequestView, ProductView } from "@cafecr/api-client";
import { paymentLabels, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { PaymentRequestModal } from "./PaymentRequestModal";

const ALL = "__all__";
const clean = (name: string) => name.replace(/^\[DEMO\]\s*/, "");

/** Sell add-on items (mì, trứng, bánh...) onto the seat's current bill; separate from extending the session. */
export function AddOnSheet({ branchId, sessionId, seatCode, onClose }: { branchId: string; sessionId: string; seatCode: string; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const products = useQuery({ queryKey: ["products", branchId], queryFn: () => api.catalog.products(branchId) });
  const [cart, setCart] = useState<Record<string, number>>({});
  const [category, setCategory] = useState(ALL);
  const [search, setSearch] = useState("");
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [paymentRequest, setPaymentRequest] = useState<PaymentRequestView | null>(null);

  const byId = useMemo(() => new Map((products.data ?? []).map((p) => [p.id, p])), [products.data]);
  const categories = useMemo(() => [...new Set((products.data ?? []).map((p) => p.category))], [products.data]);
  const visible = (products.data ?? []).filter((p) =>
    (category === ALL || p.category === category) && (!search.trim() || p.name.toLowerCase().includes(search.trim().toLowerCase())));
  const lines = Object.entries(cart).filter(([, q]) => q > 0).map(([productId, quantity]) => ({ productId, quantity }));
  const total = lines.reduce((sum, l) => sum + (byId.get(l.productId)?.price ?? 0) * l.quantity, 0);
  const count = lines.reduce((sum, l) => sum + l.quantity, 0);
  const summary = lines.map((l) => `${l.quantity} ${clean(byId.get(l.productId)?.name ?? "")}`).join(", ");

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
    void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
    void queryClient.invalidateQueries({ queryKey: ["seat-bill"] });
  };
  const sell = useMutation({
    mutationFn: () => api.sessions.sellItems(sessionId, lines),
    onSuccess: () => { notify.ok(`Đã thêm ${summary} vào bill ${seatCode} (+${vnd(total)}).`); refresh(); onClose(); },
    onError: notify.fail,
  });
  const createQr = useMutation({
    mutationFn: () => api.payments.createAddOn(sessionId, lines),
    onSuccess: setPaymentRequest,
    onError: notify.fail,
  });
  const change = (p: ProductView, delta: number) =>
    setCart((c) => ({ ...c, [p.id]: Math.min(99, Math.max(0, (c[p.id] ?? 0) + delta)) }));

  if (paymentRequest) {
    return (
      <PaymentRequestModal
        branchId={branchId}
        request={paymentRequest}
        title={`Thanh toán đồ thêm ${seatCode}`}
        description={summary}
        onClose={() => { setPaymentRequest(null); refresh(); onClose(); }}
      />
    );
  }

  return (
    <Drawer open placement="bottom" height="85%" onClose={onClose} title={`Thêm đồ · ${seatCode}`}
      footer={
        <Space direction="vertical" style={{ width: "100%" }}>
          <Radio.Group block optionType="button" buttonStyle="solid" value={payment} onChange={(e) => setPayment(e.target.value)}
            options={(["Cash", "VietQr"] as PaymentMethod[]).map((m) => ({ value: m, label: paymentLabels[m] }))} />
          {payment === "VietQr" ? (
            <Button type="primary" block size="large" style={{ height: 56, fontSize: 18 }} disabled={count === 0}
              loading={createQr.isPending} onClick={() => createQr.mutate()}>
              Tạo QR {count > 0 ? `· ${count} món · ${vnd(total)}` : ""}
            </Button>
          ) : (
            <Button type="primary" block size="large" style={{ height: 56, fontSize: 18 }} disabled={count === 0} loading={sell.isPending}
              onClick={() => Modal.confirm({
                title: `Thu ${vnd(total)} (${paymentLabels.Cash})?`, content: summary, okText: "Đã thu tiền", cancelText: "Hủy",
                onOk: () => sell.mutateAsync(),
              })}>
              Thu tiền {count > 0 ? `· ${count} món · ${vnd(total)}` : ""}
            </Button>
          )}
        </Space>
      }>
      <Space direction="vertical" style={{ width: "100%" }} size="middle">
        <Typography.Text type="secondary">Đồ thêm được cộng vào bill hiện tại của chỗ, không thay đổi thời gian ngồi.</Typography.Text>
        <Input allowClear prefix={<SearchOutlined />} placeholder="Tìm món" value={search} onChange={(e) => setSearch(e.target.value)} />
        {categories.length > 1 && (
          <Segmented block value={category} onChange={(v) => setCategory(v as string)}
            options={[{ value: ALL, label: "Tất cả" }, ...categories.map((c) => ({ value: c, label: c }))]} />
        )}
        <List
          loading={products.isLoading}
          dataSource={visible}
          locale={{ emptyText: <Empty description="Chưa có món nào. Quản lý thêm món ở trang Bảng giá." /> }}
          renderItem={(p) => {
            const q = cart[p.id] ?? 0;
            return (
              <List.Item
                actions={[
                  <Space key="q" size="small">
                    <Button shape="circle" size="large" icon={<MinusOutlined />} disabled={q === 0} onClick={() => change(p, -1)} />
                    <Typography.Text strong style={{ minWidth: 24, display: "inline-block", textAlign: "center", fontSize: 18 }}>{q}</Typography.Text>
                    <Button shape="circle" size="large" type={q > 0 ? "primary" : "default"} icon={<PlusOutlined />} onClick={() => change(p, 1)} />
                  </Space>,
                ]}
              >
                <List.Item.Meta title={clean(p.name)} description={`${vnd(p.price)} · ${p.category}`} />
              </List.Item>
            );
          }}
        />
      </Space>
    </Drawer>
  );
}
