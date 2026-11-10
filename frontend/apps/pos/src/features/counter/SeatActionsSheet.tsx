import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button, Descriptions, Drawer, Modal, Radio, Space, Typography } from "antd";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { FloorSeat, PaymentMethod } from "@cafecr/api-client";
import { minutesLeftText, paymentLabels, seatStateStyles, time, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { PaymentRequestModal } from "./PaymentRequestModal";

/** One tap to extend or free a seat; money actions always confirm first. */
export function SeatActionsSheet({ branchId, seat, onClose }: { branchId: string; seat: FloorSeat; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [paymentRequest, setPaymentRequest] = useState<Awaited<ReturnType<typeof api.payments.createExtension>> | null>(null);
  const session = seat.session!;
  const plans = useQuery({ queryKey: ["active-plans", branchId], queryFn: () => api.catalog.activePlans(branchId) });
  const extendPrice = plans.data?.[0]?.extendPrice;
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
    void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
  };
  const extend = useMutation({
    mutationFn: () => api.sessions.extend(session.sessionId),
    onSuccess: (s) => { notify.ok(`Đã gia hạn ${s.seatCode} đến ${time(s.endAt)}. Bill ${s.bill.code}: ${vnd(s.bill.total)}.`); refresh(); onClose(); },
    onError: notify.fail,
  });
  const createPaymentRequest = useMutation({
    mutationFn: () => api.payments.createExtension(session.sessionId),
    onSuccess: setPaymentRequest,
    onError: notify.fail,
  });
  const close = useMutation({
    mutationFn: () => api.sessions.close(session.sessionId),
    onSuccess: () => { notify.ok(`${seat.code} đã trống.`); refresh(); onClose(); },
    onError: notify.fail,
  });
  const style = seatStateStyles[seat.state];

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
        { label: "Khách", children: session.customerName ?? "Khách lẻ" },
        { label: "Bill", children: `${session.billCode} · ${vnd(session.billTotal)}` },
      ]} />
      <Space direction="vertical" style={{ width: "100%", marginTop: 16 }}>
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
            onClick={() => Modal.confirm({ title: `Gia hạn thêm 4 tiếng${extendPrice ? ` (+${vnd(extendPrice)})` : ""}?`, okText: "Đã thu tiền", cancelText: "Hủy", onOk: () => extend.mutateAsync() })}>
            Gia hạn thêm 4 tiếng{extendPrice ? ` (+${vnd(extendPrice)})` : ""}
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
