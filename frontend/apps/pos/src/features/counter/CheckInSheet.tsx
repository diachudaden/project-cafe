import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Drawer, Input, List, Modal, Radio, Result, Space, Tag, Typography } from "antd";
import type { CustomerLookup, FloorSeat, PaymentMethod, SessionView } from "@cafecr/api-client";
import { paymentLabels, seatTypeLabels, tierLabels, time, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { PaymentRequestModal } from "./PaymentRequestModal";

/** Tap a free seat → choose plan / member / payment → "Thu tiền" (3 taps for a walk-in). */
export function CheckInSheet({ branchId, seat, onClose }: { branchId: string; seat: FloorSeat; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const plans = useQuery({ queryKey: ["active-plans", branchId], queryFn: () => api.catalog.activePlans(branchId) });
  const [planId, setPlanId] = useState<string>();
  const [payment, setPayment] = useState<PaymentMethod>("Cash");
  const [search, setSearch] = useState("");
  const [members, setMembers] = useState<CustomerLookup[] | null>(null);
  const [member, setMember] = useState<CustomerLookup | null>(null);
  const [done, setDone] = useState<SessionView | null>(null);
  const [paymentRequest, setPaymentRequest] = useState<Awaited<ReturnType<typeof api.payments.createCheckIn>> | null>(null);
  const plan = plans.data?.find((p) => p.id === (planId ?? plans.data?.[0]?.id));

  const checkIn = useMutation({
    mutationFn: () => api.sessions.checkIn({ seatId: seat.id, pricePlanId: plan!.id, paymentMethod: "Cash", customerId: member?.customerId }),
    onSuccess: (s) => {
      setDone(s);
      void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
      void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
    },
    onError: notify.fail,
  });
  const createPaymentRequest = useMutation({
    mutationFn: () => api.payments.createCheckIn({
      seatId: seat.id,
      pricePlanId: plan!.id,
      customerId: member?.customerId,
    }),
    onSuccess: setPaymentRequest,
    onError: notify.fail,
  });

  const lookup = async () => {
    try {
      setMembers(await api.sessions.lookupCustomer(search));
    } catch (e) {
      notify.fail(e);
    }
  };

  if (paymentRequest) {
    return (
      <PaymentRequestModal
        branchId={branchId}
        request={paymentRequest}
        title={`Thanh toán check-in ${seat.code}`}
        description={`${seat.code} · ${plan?.durationMinutes ? `${plan.durationMinutes / 60} tiếng` : "Check-in"}`}
        onClose={() => {
          setPaymentRequest(null);
          void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
        }}
      />
    );
  }

  if (done) {
    return (
      <Modal open footer={<Button type="primary" size="large" onClick={onClose}>Xong</Button>} onCancel={onClose}>
        <Result status="success" title={`Đã check-in ${done.seatCode}`}
          subTitle={`${vnd(done.bill.total)} · ${paymentLabels[done.bill.paymentMethod]} · đến ${time(done.endAt)}`}
          extra={
            <Space direction="vertical" align="center">
              <Typography.Text type="secondary">Mã bill để khách tích điểm</Typography.Text>
              <Typography.Title copyable style={{ letterSpacing: 6, margin: 0 }}>{done.bill.code}</Typography.Title>
              {done.pointsAwarded > 0 && <Tag color="green">Đã cộng {done.pointsAwarded} điểm cho {done.customer?.fullName}</Tag>}
            </Space>
          } />
      </Modal>
    );
  }

  return (
    <Drawer open placement="bottom" height="auto" onClose={onClose} title={`Check-in ${seat.code} · ${seatTypeLabels[seat.type]}`}>
      <Space direction="vertical" style={{ width: "100%" }} size="large">
        <div>
          <Typography.Text strong>Gói</Typography.Text>
          <Radio.Group block optionType="button" buttonStyle="solid" value={plan?.id} onChange={(e) => setPlanId(e.target.value)}
            options={(plans.data ?? []).map((p) => ({ value: p.id, label: `${p.durationMinutes / 60} tiếng – ${vnd(p.price)}` }))} />
        </div>
        <div>
          <Typography.Text strong>Thành viên (tùy chọn)</Typography.Text>
          {member ? (
            <div><Tag color="green" closable onClose={() => setMember(null)}>{member.fullName} · {tierLabels[member.tier]?.label ?? member.tier} · {member.points} điểm</Tag></div>
          ) : (
            <Space.Compact style={{ width: "100%" }}>
              <Input placeholder="Mã thành viên, SĐT hoặc email" value={search} onChange={(e) => setSearch(e.target.value)} onPressEnter={() => void lookup()} />
              <Button onClick={() => void lookup()}>Tìm</Button>
            </Space.Compact>
          )}
          {members && !member && (
            <List size="small" dataSource={members} locale={{ emptyText: "Không tìm thấy thành viên." }}
              renderItem={(m) => <List.Item style={{ cursor: "pointer" }} onClick={() => setMember(m)}>{m.fullName} · {m.memberCode}</List.Item>} />
          )}
        </div>
        <div>
          <Typography.Text strong>Thanh toán</Typography.Text>
          <Radio.Group block optionType="button" buttonStyle="solid" value={payment} onChange={(e) => setPayment(e.target.value)}
            options={(["Cash", "VietQr"] as PaymentMethod[]).map((p) => ({ value: p, label: paymentLabels[p] }))} />
        </div>
        {payment === "VietQr" ? (
          <Button type="primary" block size="large" style={{ height: 56, fontSize: 18 }} disabled={!plan}
            loading={createPaymentRequest.isPending} onClick={() => createPaymentRequest.mutate()}>
            Tạo QR thanh toán {plan ? vnd(plan.price) : ""}
          </Button>
        ) : (
          <Button type="primary" block size="large" style={{ height: 56, fontSize: 18 }} disabled={!plan} loading={checkIn.isPending}
            onClick={() => Modal.confirm({ title: `Thu ${plan ? vnd(plan.price) : ""} (${paymentLabels.Cash})?`, okText: "Đã thu tiền", cancelText: "Hủy", onOk: () => checkIn.mutateAsync() })}>
            Thu tiền {plan ? vnd(plan.price) : ""}
          </Button>
        )}
      </Space>
    </Drawer>
  );
}
