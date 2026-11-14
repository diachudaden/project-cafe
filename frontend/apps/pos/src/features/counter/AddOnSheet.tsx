import { useMemo, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Drawer, Empty, Grid, Input, List, Modal, Radio, Segmented, Skeleton, Space, Typography, type InputRef } from "antd";
import { MinusOutlined, PlusOutlined, SearchOutlined } from "@ant-design/icons";
import type { PaymentMethod, PaymentRequestView, ProductView } from "@cafecr/api-client";
import { displayName, hasRole, paymentLabels, vnd } from "@cafecr/ui";
import { api, useAuth } from "../../api";
import { useNotify } from "../useNotify";
import { ALL_CATEGORIES, menuState } from "./addOnMenu";
import { PaymentRequestModal } from "./PaymentRequestModal";
import { PaymentDoneModal, type PaymentDone } from "./PaymentDoneModal";

/** Sell add-on items (mì, trứng, bánh...) onto the seat's current bill; separate from extending the session. */
export function AddOnSheet({ branchId, sessionId, seatCode, onClose }: { branchId: string; sessionId: string; seatCode: string; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { state: auth } = useAuth();
  const screens = Grid.useBreakpoint();
  const searchRef = useRef<InputRef>(null);
  const products = useQuery({ queryKey: ["products", branchId], queryFn: () => api.catalog.products(branchId) });
  const [cart, setCart] = useState<Record<string, number>>({});
  const [category, setCategory] = useState(ALL_CATEGORIES);
  const [search, setSearch] = useState("");
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [paymentRequest, setPaymentRequest] = useState<PaymentRequestView | null>(null);
  const [paid, setPaid] = useState<PaymentDone | null>(null);

  const byId = useMemo(() => new Map((products.data ?? []).map((p) => [p.id, p])), [products.data]);
  const categories = useMemo(() => [...new Set((products.data ?? []).map((p) => p.category))], [products.data]);
  const view = menuState({ isLoading: products.isLoading, error: products.error, products: products.data, term: search, category });
  // The cart is keyed by product id, so it survives search and category changes.
  const lines = Object.entries(cart).filter(([id, q]) => q > 0 && byId.has(id)).map(([productId, quantity]) => ({ productId, quantity }));
  const total = lines.reduce((sum, l) => sum + (byId.get(l.productId)?.price ?? 0) * l.quantity, 0);
  const count = lines.reduce((sum, l) => sum + l.quantity, 0);
  const summary = lines.map((l) => `${l.quantity} ${displayName(byId.get(l.productId)?.name)}`).join(", ");
  const canManageMenu = hasRole(auth.user, "Admin", "BranchManager");

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
    void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
    // Prefix of ["seat-bill", billCode] used by SeatActionsSheet.
    void queryClient.invalidateQueries({ queryKey: ["seat-bill"] });
  };
  const sell = useMutation({
    mutationFn: () => api.sessions.sellItems(sessionId, lines),
    onSuccess: (sale) => {
      refresh();
      setPaid({
        what: "Đồ thêm", seatCode, amount: total, method: "Cash", billCode: sale.billCode, billTotal: sale.billTotal,
        customerName: sale.customerName, pointsAwarded: sale.pointsAwarded, detail: summary,
      });
    },
    onError: notify.fail,
  });
  const createQr = useMutation({
    mutationFn: () => api.payments.createAddOn(sessionId, lines),
    onSuccess: setPaymentRequest,
    onError: notify.fail,
  });
  const change = (id: string, delta: number) =>
    setCart((c) => ({ ...c, [id]: Math.min(99, Math.max(0, (c[id] ?? 0) + delta)) }));

  if (paid) {
    return <PaymentDoneModal done={paid} onClose={onClose} />;
  }

  if (paymentRequest) {
    return (
      <PaymentRequestModal
        what="Đồ thêm"
        branchId={branchId}
        request={paymentRequest}
        title={`Thanh toán đồ thêm ${seatCode}`}
        description={summary}
        onClose={() => { setPaymentRequest(null); refresh(); onClose(); }}
      />
    );
  }

  const stepper = (id: string, size: "large" | "small" = "large") => {
    const q = cart[id] ?? 0;
    return (
      <Space size="small">
        <Button shape="circle" size={size} icon={<MinusOutlined />} disabled={q === 0} onClick={() => change(id, -1)} aria-label="Bớt" />
        <Typography.Text strong style={{ minWidth: 24, display: "inline-block", textAlign: "center", fontSize: size === "large" ? 18 : 14 }}>{q}</Typography.Text>
        <Button shape="circle" size={size} type={q > 0 ? "primary" : "default"} icon={<PlusOutlined />} onClick={() => change(id, 1)} aria-label="Thêm" />
      </Space>
    );
  };

  let body;
  switch (view.kind) {
    case "loading":
      body = <Skeleton active paragraph={{ rows: 6 }} />;
      break;
    case "error":
      body = (
        <Alert type="error" showIcon message="Không tải được thực đơn" description={view.message}
          action={<Button onClick={() => void products.refetch()} loading={products.isFetching}>Thử lại</Button>} />
      );
      break;
    case "empty":
      body = (
        <Empty description="Chi nhánh chưa có món nào đang bán.">
          {canManageMenu
            ? <Button href="/admin/pricing" target="_blank" rel="noopener">Thêm món ở Bảng giá</Button>
            : <Typography.Text type="secondary">Nhờ quản lý thêm món.</Typography.Text>}
        </Empty>
      );
      break;
    case "noResult":
      body = (
        <Empty description={`Không tìm thấy “${view.term}”`}>
          <Button onClick={() => setSearch("")}>Xóa tìm kiếm</Button>
        </Empty>
      );
      break;
    case "list":
      body = (
        <>
          {view.searching && <Typography.Text type="secondary">{view.items.length} món</Typography.Text>}
          <List
            dataSource={view.items}
            renderItem={(p: ProductView) => (
              <List.Item actions={[<span key="q">{stepper(p.id)}</span>]}>
                <List.Item.Meta title={displayName(p.name)} description={`${vnd(p.price)} · ${p.category}`} />
              </List.Item>
            )}
          />
        </>
      );
      break;
  }

  return (
    <Drawer open placement="bottom" height="85%" onClose={onClose} title={`Thêm đồ · ${seatCode}`}
      afterOpenChange={(open) => { if (open && screens.md) searchRef.current?.focus(); }}
      footer={
        <Space direction="vertical" style={{ width: "100%" }}>
          {lines.length > 0 && (
            <div>
              <Typography.Text type="secondary">Đã chọn</Typography.Text>
              <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginTop: 4, maxHeight: 120, overflowY: "auto" }}>
                {lines.map((l) => (
                  <Space key={l.productId} size={4} style={{ border: "1px solid rgba(128,128,128,.3)", borderRadius: 20, padding: "2px 4px 2px 10px" }}>
                    <Typography.Text>{displayName(byId.get(l.productId)?.name)}</Typography.Text>
                    {stepper(l.productId, "small")}
                  </Space>
                ))}
              </div>
            </div>
          )}
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
        <Input ref={searchRef} allowClear prefix={<SearchOutlined />} placeholder="Tìm món (vd: mi, trung op, uong)" value={search}
          onChange={(e) => setSearch(e.target.value)} disabled={view.kind === "loading" || view.kind === "error"} />
        {categories.length > 1 && (
          <Segmented block value={search.trim() ? ALL_CATEGORIES : category} disabled={!!search.trim()} onChange={(v) => setCategory(v as string)}
            options={[{ value: ALL_CATEGORIES, label: "Tất cả" }, ...categories.map((c) => ({ value: c, label: c }))]} />
        )}
        {body}
      </Space>
    </Drawer>
  );
}
