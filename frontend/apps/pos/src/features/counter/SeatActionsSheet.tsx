import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button, Descriptions, Drawer, Modal, Space } from "antd";
import { useQuery } from "@tanstack/react-query";
import type { FloorSeat } from "@cafecr/api-client";
import { minutesLeftText, seatStateStyles, time, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

/** One tap to extend or free a seat; money actions always confirm first. */
export function SeatActionsSheet({ branchId, seat, onClose }: { branchId: string; seat: FloorSeat; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
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
  const close = useMutation({
    mutationFn: () => api.sessions.close(session.sessionId),
    onSuccess: () => { notify.ok(`${seat.code} đã trống.`); refresh(); onClose(); },
    onError: notify.fail,
  });
  const style = seatStateStyles[seat.state];

  return (
    <Drawer open placement="bottom" height="auto" onClose={onClose} title={`${seat.code} · ${style.label}`}>
      <Descriptions column={1} size="small" items={[
        { label: "Thời gian", children: `${time(session.startAt)} → ${time(session.endAt)} (${minutesLeftText(session.minutesLeft)})` },
        { label: "Khách", children: session.customerName ?? "Khách lẻ" },
        { label: "Bill", children: `${session.billCode} · ${vnd(session.billTotal)}` },
      ]} />
      <Space direction="vertical" style={{ width: "100%", marginTop: 16 }}>
        <Button type="primary" block size="large" style={{ height: 56 }} loading={extend.isPending}
          onClick={() => Modal.confirm({ title: `Gia hạn thêm 4 tiếng${extendPrice ? ` (+${vnd(extendPrice)})` : ""}?`, okText: "Đã thu tiền", cancelText: "Hủy", onOk: () => extend.mutateAsync() })}>
          Gia hạn thêm 4 tiếng{extendPrice ? ` (+${vnd(extendPrice)})` : ""}
        </Button>
        <Button block size="large" style={{ height: 56 }} loading={close.isPending}
          onClick={() => Modal.confirm({ title: `Trả chỗ ${seat.code}?`, okText: "Trả chỗ", cancelText: "Hủy", onOk: () => close.mutateAsync() })}>
          Trả chỗ
        </Button>
      </Space>
    </Drawer>
  );
}
