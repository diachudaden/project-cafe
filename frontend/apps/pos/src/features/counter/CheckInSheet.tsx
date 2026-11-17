import { useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Divider, Drawer, Input, List, Modal, Radio, Select, Space, Tag, Typography } from "antd";
import type { CustomerLookup, FloorSeat, PaymentMethod, PricePlanView, SessionView } from "@cafecr/api-client";
import { displayName, paymentLabels, seatTypeLabels, tierLabels, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { AddOnSheet } from "./AddOnSheet";
import { PaymentDoneModal } from "./PaymentDoneModal";
import { PaymentRequestModal } from "./PaymentRequestModal";
import { AddOnGrid, CartChips, useMenu } from "./AddOnGrid";
import { cartLines, checkInTotals, type Cart } from "./addOnMenu";

const hours = (minutes: number) => `${Number((minutes / 60).toFixed(2))} tiếng`;
const planLabel = (p: PricePlanView) => `${displayName(p.name)} · ${hours(p.durationMinutes)} · ${vnd(p.price)}`;

/**
 * Tap a free seat → choose plan, optional add-ons and member → one payment for plan + add-ons (TASK-026).
 * A walk-in without add-ons is still 3 taps.
 */
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
  const [addingItems, setAddingItems] = useState(false);
  const [paymentRequest, setPaymentRequest] = useState<Awaited<ReturnType<typeof api.payments.createCheckIn>> | null>(null);
  const plan = plans.data?.find((p) => p.id === (planId ?? plans.data?.[0]?.id));
  const products = useMenu(branchId);
  const [cart, setCart] = useState<Cart>({});
  const lines = cartLines(cart, products.data);
  const totals = checkInTotals(plan?.price, lines);
  const items = lines.map(({ productId, quantity }) => ({ productId, quantity }));
  const itemsSummary = lines.map((l) => `${l.quantity} ${displayName(l.name)}`).join(", ");

  const checkIn = useMutation({
    mutationFn: () => api.sessions.checkIn({ seatId: seat.id, pricePlanId: plan!.id, paymentMethod: "Cash", customerId: member?.customerId, items }),
    onSuccess: (s) => {
      setDone(s);
      void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
      void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
      void queryClient.invalidateQueries({ queryKey: ["recent-closed", branchId] });
    },
    onError: notify.fail,
  });
  const createPaymentRequest = useMutation({
    mutationFn: () => api.payments.createCheckIn({
      seatId: seat.id,
      pricePlanId: plan!.id,
      customerId: member?.customerId,
      items,
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
        what="Check-in"
        branchId={branchId}
        request={paymentRequest}
        title={`Thanh toán check-in ${seat.code}`}
        description={[plan ? `${seat.code} · ${planLabel(plan)}` : `${seat.code} · Check-in`, itemsSummary && `Đồ thêm: ${itemsSummary}`].filter(Boolean).join(" · ")}
        onClose={() => {
          setPaymentRequest(null);
          void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
        }}
      />
    );
  }

  if (done && addingItems) {
    return <AddOnSheet branchId={branchId} sessionId={done.id} seatCode={done.seatCode} onClose={onClose} />;
  }

  if (done) {
    return (
      <PaymentDoneModal
        done={{
          what: "Check-in", seatCode: done.seatCode, amount: done.bill.total, method: done.bill.paymentMethod,
          billCode: done.bill.code, endAt: done.endAt, customerName: done.customer?.fullName, pointsAwarded: done.pointsAwarded,
          detail: done.items.length > 0 ? `Gồm đồ thêm: ${done.items.map((i) => `${i.quantity} ${displayName(i.name)}`).join(", ")}` : undefined,
        }}
        onClose={onClose}
        actions={<Button size="large" onClick={() => setAddingItems(true)}>Thêm đồ ăn / uống</Button>}
      />
    );
  }

  return (
    <Drawer open placement="bottom" height="90%" onClose={onClose} title={`Check-in ${seat.code} · ${seatTypeLabels[seat.type]}`}
      footer={
        <Space direction="vertical" style={{ width: "100%" }}>
          <CartChips lines={lines} cart={cart} onCartChange={setCart} />
          <div>
            <TotalRow label="Gói" value={plan ? vnd(totals.plan) : "—"} />
            <TotalRow label={`Đồ thêm${totals.count ? ` (${totals.count} món)` : ""}`} value={vnd(totals.addOns)} />
            <TotalRow label={<Typography.Text strong>Tổng cộng</Typography.Text>} value={<Typography.Text strong style={{ fontSize: 18 }}>{vnd(totals.total)}</Typography.Text>} />
          </div>
          <Radio.Group block optionType="button" buttonStyle="solid" value={payment} onChange={(e) => setPayment(e.target.value)}
            options={(["Cash", "VietQr"] as PaymentMethod[]).map((p) => ({ value: p, label: paymentLabels[p] }))} />
          {payment === "VietQr" ? (
            <Button type="primary" block size="large" style={{ height: 56, fontSize: 18 }} disabled={!plan}
              loading={createPaymentRequest.isPending} onClick={() => createPaymentRequest.mutate()}>
              Tạo QR thanh toán {plan ? vnd(totals.total) : ""}
            </Button>
          ) : (
            <Button type="primary" block size="large" style={{ height: 56, fontSize: 18 }} disabled={!plan} loading={checkIn.isPending}
              onClick={() => Modal.confirm({
                title: `Thu ${vnd(totals.total)} (${paymentLabels.Cash})?`,
                content: itemsSummary ? `Gói ${vnd(totals.plan)} + đồ thêm ${vnd(totals.addOns)}: ${itemsSummary}` : undefined,
                okText: "Đã thu tiền", cancelText: "Hủy", onOk: () => checkIn.mutateAsync(),
              })}>
              Thu tiền {plan ? vnd(totals.total) : ""}
            </Button>
          )}
        </Space>
      }>
      <Space direction="vertical" style={{ width: "100%" }} size="large">
        <div>
          <Typography.Text strong>Gói ({plans.data?.length ?? 0})</Typography.Text>
          <Select
            size="large"
            showSearch
            optionFilterProp="label"
            style={{ width: "100%" }}
            loading={plans.isLoading}
            placeholder="Chọn gói"
            notFoundContent="Chi nhánh chưa có gói nào đang áp dụng."
            value={plan?.id}
            onChange={setPlanId}
            options={(plans.data ?? []).map((p) => ({ value: p.id, label: planLabel(p) }))}
          />
          {plan && (
            <Typography.Text type="secondary">
              {plan.branchId ? "Giá riêng chi nhánh" : "Giá toàn chuỗi"} · gia hạn {vnd(plan.extendPrice)} / {hours(plan.durationMinutes)}
            </Typography.Text>
          )}
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
        {plan && (
          <div>
            <Divider orientation="left" plain style={{ margin: "0 0 8px" }}>Đồ thêm (tùy chọn) · cùng một lần thanh toán</Divider>
            <AddOnGrid branchId={branchId} cart={cart} onCartChange={setCart} />
          </div>
        )}
      </Space>
    </Drawer>
  );
}

function TotalRow({ label, value }: { label: ReactNode; value: ReactNode }) {
  return (
    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
      <Typography.Text type="secondary">{label}</Typography.Text>
      <span>{value}</span>
    </div>
  );
}
