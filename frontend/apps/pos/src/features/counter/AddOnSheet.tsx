import { useRef, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button, Drawer, Grid, Modal, Radio, Space, Typography, type InputRef } from "antd";
import type { PaymentMethod, PaymentRequestView } from "@cafecr/api-client";
import { displayName, paymentLabels, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { cartLines, checkInTotals, type Cart } from "./addOnMenu";
import { AddOnGrid, CartChips, useMenu } from "./AddOnGrid";
import { PaymentRequestModal } from "./PaymentRequestModal";
import { PaymentDoneModal, type PaymentDone } from "./PaymentDoneModal";

/** Sell add-on items (mì, trứng, bánh...) onto the seat's current bill; separate from extending the session. */
export function AddOnSheet({ branchId, sessionId, seatCode, onClose }: { branchId: string; sessionId: string; seatCode: string; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const screens = Grid.useBreakpoint();
  const searchRef = useRef<InputRef>(null);
  const products = useMenu(branchId);
  const [cart, setCart] = useState<Cart>({});
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [paymentRequest, setPaymentRequest] = useState<PaymentRequestView | null>(null);
  const [paid, setPaid] = useState<PaymentDone | null>(null);

  const lines = cartLines(cart, products.data);
  const { addOns: total, count } = checkInTotals(0, lines);
  const items = lines.map(({ productId, quantity }) => ({ productId, quantity }));
  const summary = lines.map((l) => `${l.quantity} ${displayName(l.name)}`).join(", ");

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
    void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
    // Prefix of ["seat-bill", billCode] used by SeatActionsSheet.
    void queryClient.invalidateQueries({ queryKey: ["seat-bill"] });
  };
  const sell = useMutation({
    mutationFn: () => api.sessions.sellItems(sessionId, items),
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
    mutationFn: () => api.payments.createAddOn(sessionId, items),
    onSuccess: setPaymentRequest,
    onError: notify.fail,
  });

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

  return (
    <Drawer open placement="bottom" height="85%" onClose={onClose} title={`Thêm đồ · ${seatCode}`}
      afterOpenChange={(open) => { if (open && screens.md) searchRef.current?.focus(); }}
      footer={
        <Space direction="vertical" style={{ width: "100%" }}>
          <CartChips lines={lines} cart={cart} onCartChange={setCart} />
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
        <AddOnGrid ref={searchRef} branchId={branchId} cart={cart} onCartChange={setCart} />
      </Space>
    </Drawer>
  );
}
