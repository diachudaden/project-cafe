import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Checkbox, Input, List, Modal, Space, Spin, Typography } from "antd";
import type { PendingHandover } from "@cafecr/api-client";
import { ConfirmDialog, CountSummaryTable, QtyStepper, errorText, shiftTypeLabels, time } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

/** "Có bàn giao kho ngoài từ ca trước": the next shift accepts the count or reports a mismatch. Work is never blocked. */
export function HandoverBanner({ branchId }: { branchId: string }) {
  const pending = useQuery({
    queryKey: ["pending-handovers", branchId],
    queryFn: () => api.inventory.pendingHandovers(branchId),
    refetchInterval: 30_000,
  });
  const [open, setOpen] = useState<PendingHandover | null>(null);
  const first = pending.data?.[0];
  if (!first) return null;

  return (
    <>
      <Alert type="info" showIcon style={{ margin: "12px 16px 0" }}
        message={`Có bàn giao kho ngoài từ ca trước (${shiftTypeLabels[first.shiftType]} ${time(first.shiftStart)}–${time(first.shiftEnd)}, ${first.countedByName}).`}
        action={<Button type="primary" onClick={() => setOpen(first)}>Xem bàn giao</Button>} />
      {open && <HandoverModal branchId={branchId} handover={open} onClose={() => setOpen(null)} />}
    </>
  );
}

function HandoverModal({ branchId, handover, onClose }: { branchId: string; handover: PendingHandover; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const summary = useQuery({ queryKey: ["count-summary", handover.countId], queryFn: () => api.inventory.countSummary("Outer", handover.countId) });
  const [asking, setAsking] = useState(false);
  const [disputing, setDisputing] = useState(false);
  const [mine, setMine] = useState<Record<string, number>>({});
  const [note, setNote] = useState("");
  const done = (text: string) => {
    notify.ok(text);
    void queryClient.invalidateQueries({ queryKey: ["pending-handovers", branchId] });
    onClose();
  };
  const accept = useMutation({ mutationFn: () => api.inventory.acceptHandover(handover.countId), onSuccess: () => done("Đã nhận bàn giao kho ngoài."), onError: notify.fail });
  const dispute = useMutation({
    mutationFn: () => api.inventory.disputeHandover(handover.countId,
      Object.entries(mine).map(([itemId, countedQty]) => ({ itemId, countedQty })), note.trim()),
    onSuccess: () => done("Đã báo không khớp, quản lý sẽ chốt số."),
    onError: notify.fail,
  });
  const title = `Bàn giao kho ngoài – ${shiftTypeLabels[handover.shiftType]} ${time(handover.shiftStart)}–${time(handover.shiftEnd)}`;

  return (
    <Modal open width={900} title={title} onCancel={onClose} footer={disputing ? [
      <Button key="back" onClick={() => setDisputing(false)}>Quay lại</Button>,
      <Button key="send" type="primary" danger disabled={Object.keys(mine).length === 0 || !note.trim()} loading={dispute.isPending}
        onClick={() => dispute.mutate()}>Gửi báo không khớp</Button>,
    ] : [
      <Button key="dispute" danger onClick={() => setDisputing(true)}>Báo không khớp</Button>,
      <Button key="accept" type="primary" onClick={() => setAsking(true)}>Xác nhận nhận</Button>,
    ]}>
      {summary.isLoading && <Spin />}
      {summary.isError && <Alert type="error" showIcon message={errorText(summary.error)} />}
      {summary.data && !disputing && <CountSummaryTable summary={summary.data} countColumn="Đếm (ca trước)" />}
      {summary.data && disputing && (
        <Space direction="vertical" style={{ width: "100%" }}>
          <Typography.Text>Chọn món không khớp và nhập số bạn tự đếm.</Typography.Text>
          <List dataSource={summary.data.rows} renderItem={(r) => (
            <List.Item>
              <Space direction="vertical" size={4}>
                <Checkbox checked={r.itemId in mine} onChange={(e) => {
                  const next = { ...mine };
                  if (e.target.checked) next[r.itemId] = r.counted ?? 0; else delete next[r.itemId];
                  setMine(next);
                }}>
                  <b>{r.name}</b> · ca trước đếm {r.counted}
                </Checkbox>
                {r.itemId in mine && (
                  <QtyStepper value={mine[r.itemId]} baseUnit={r.baseUnit} units={r.units} onChange={(v) => setMine({ ...mine, [r.itemId]: v })} />
                )}
              </Space>
            </List.Item>
          )} />
          <Input.TextArea rows={2} maxLength={1000} placeholder="Ghi chú (bắt buộc)" value={note} onChange={(e) => setNote(e.target.value)} />
        </Space>
      )}
      <ConfirmDialog open={asking} title="Xác nhận nhận bàn giao kho ngoài?"
        body="Số đếm của ca trước sẽ là tồn đầu ca của bạn." confirmText="Xác nhận nhận" busy={accept.isPending}
        onCancel={() => setAsking(false)} onConfirm={() => accept.mutate()} />
    </Modal>
  );
}
