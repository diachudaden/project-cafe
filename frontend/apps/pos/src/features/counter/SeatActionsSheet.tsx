import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button, Descriptions, Divider, Drawer, List, Modal, Radio, Space, Tag, Typography } from "antd";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ApiError, type FloorSeat, type PaymentMethod } from "@cafecr/api-client";
import { autoReleaseText, displayName, minutesLeftText, paymentLabels, seatStateStyles, time, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { AddOnSheet } from "./AddOnSheet";
import { PaymentRequestModal } from "./PaymentRequestModal";

/** One tap to extend or free a seat; money actions always confirm first. */
export function SeatActionsSheet({ branchId, seat, onClose }: { branchId: string; seat: FloorSeat; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [paymentRequest, setPaymentRequest] = useState<Awaited<ReturnType<typeof api.payments.createExtension>> | null>(null);
  const session = seat.session!;
  const [addingItems, setAddingItems] = useState(false);
  const plans = useQuery({ queryKey: ["active-plans", branchId], queryFn: () => api.catalog.activePlans(branchId) });
  // The bill view carries the session's own plan and its add-on lines.
  const bill = useQuery({ queryKey: ["seat-bill", session.billCode], queryFn: () => api.sessions.bill(session.billCode) });
  const sessionPlan = plans.data?.find((p) => p.id === bill.data?.pricePlanId);
  const extendPrice = sessionPlan?.extendPrice;
  const extendHours = sessionPlan ? `${Number((sessionPlan.durationMinutes / 60).toFixed(2))} tiếng` : "1 lượt";
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
    void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
    void queryClient.invalidateQueries({ queryKey: ["recent-closed", branchId] });
  };
  // 409 = the session changed meanwhile (e.g. the system auto-released the seat): show why, reload the floor, close.
  const failAndRefresh = (error: unknown) => {
    notify.fail(error);
    if (error instanceof ApiError && error.status === 409) { refresh(); onClose(); }
  };
  const extend = useMutation({
    mutationFn: () => api.sessions.extend(session.sessionId),
    onSuccess: (s) => { notify.ok(`Đã gia hạn ${s.seatCode} đến ${time(s.endAt)}. Bill ${s.bill.code}: ${vnd(s.bill.total)}.`); refresh(); onClose(); },
    onError: failAndRefresh,
  });
  const createPaymentRequest = useMutation({
    mutationFn: () => api.payments.createExtension(session.sessionId),
    onSuccess: setPaymentRequest,
    onError: notify.fail,
  });
  const close = useMutation({
    mutationFn: () => api.sessions.close(session.sessionId),
    onSuccess: () => { notify.ok(`${seat.code} đã trống.`); refresh(); onClose(); },
    onError: failAndRefresh,
  });
  const style = seatStateStyles[seat.state];

  if (addingItems) {
    return <AddOnSheet branchId={branchId} sessionId={session.sessionId} seatCode={seat.code} onClose={() => { setAddingItems(false); void bill.refetch(); }} />;
  }

  if (paymentRequest) {
    return <PaymentRequestModal
      branchId={branchId}
      request={paymentRequest}
      title={`Thanh toán gia hạn ${seat.code}`}
      description="Gia hạn lượt ngồi"
      onClose={() => {
        setPaymentRequest(null);
        refresh();
        onClose();
      }}
    />;
  }

  return (
    <Drawer open placement="bottom" height="auto" onClose={onClose} title={`${seat.code} · ${style.label}`}>
      <Descriptions column={1} size="small" items={[
        { label: "Thời gian", children: `${time(session.startAt)} → ${time(session.endAt)} (${minutesLeftText(session.minutesLeft)})` },
        ...(seat.state === "Overdue" && autoReleaseText(session.autoReleaseAt)
          ? [{ label: "Tự trả chỗ", children: <Typography.Text type="warning">{autoReleaseText(session.autoReleaseAt)} ({time(session.autoReleaseAt)}) nếu không gia hạn</Typography.Text> }]
          : []),
        { label: "Khách", children: session.customerName ?? "Khách lẻ" },
        { label: "Gói", children: (bill.data ? displayName(bill.data.planName) : "…") },
        { label: "Bill", children: `${session.billCode} · ${vnd(bill.data?.bill.total ?? session.billTotal)}` },
      ]} />
      {(bill.data?.items.length ?? 0) > 0 && (
        <>
          <Divider orientation="left" plain style={{ margin: "8px 0" }}>Đồ thêm</Divider>
          <List size="small" dataSource={bill.data!.items}
            renderItem={(i) => (
              <List.Item extra={<Tag>{paymentLabels[i.paymentMethod]}</Tag>}>
                {i.quantity} × {displayName(i.name)} · {vnd(i.amount)} <Typography.Text type="secondary">({time(i.createdAt)})</Typography.Text>
              </List.Item>
            )} />
        </>
      )}
      <Space direction="vertical" style={{ width: "100%", marginTop: 16 }}>
        <Button block size="large" style={{ height: 56 }} onClick={() => setAddingItems(true)}>
          Thêm đồ ăn / uống (mì, trứng, bánh…)
        </Button>
        <div>
          <Typography.Text strong>Thanh toán gia hạn</Typography.Text>
          <Radio.Group block optionType="button" buttonStyle="solid" value={payment} onChange={(event) => setPayment(event.target.value)}
            options={(["Cash", "VietQr"] as PaymentMethod[]).map((method) => ({ value: method, label: paymentLabels[method] }))} />
        </div>
        {payment === "VietQr" ? (
          <Button type="primary" block size="large" style={{ height: 56 }} loading={createPaymentRequest.isPending}
            onClick={() => createPaymentRequest.mutate()}>
            Tạo QR gia hạn
          </Button>
        ) : (
          <Button type="primary" block size="large" style={{ height: 56 }} loading={extend.isPending}
            onClick={() => Modal.confirm({ title: `Gia hạn thêm ${extendHours}${extendPrice != null ? ` (+${vnd(extendPrice)})` : ""}?`, okText: "Đã thu tiền", cancelText: "Hủy", onOk: () => extend.mutateAsync() })}>
            Gia hạn thêm {extendHours}{extendPrice != null ? ` (+${vnd(extendPrice)})` : ""}
          </Button>
        )}
        <Button block size="large" style={{ height: 56 }} loading={close.isPending}
          onClick={() => Modal.confirm({ title: `Trả chỗ ${seat.code}?`, okText: "Trả chỗ", cancelText: "Hủy", onOk: () => close.mutateAsync() })}>
          Trả chỗ
        </Button>
      </Space>
    </Drawer>
  );
}
