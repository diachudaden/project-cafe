import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Descriptions, Input, Modal, Segmented, Space, Table, Tag, Typography } from "antd";
import type { ClosingItem } from "@cafecr/api-client";
import { addDays, closingLabels, dateTime, dayMonth, shiftTypeLabels, time, todayVn, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function ClosingsPage() {
  return <RequireBranch title="Kết ca">{(branchId) => <Closings branchId={branchId} />}</RequireBranch>;
}

function Closings({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<"Submitted" | "">("Submitted");
  const [open, setOpen] = useState<ClosingItem | null>(null);
  const [note, setNote] = useState("");
  const today = todayVn();
  const closings = useQuery({
    queryKey: ["closings", branchId, status],
    queryFn: () => api.finance.closings(branchId, addDays(today, -60), today, status || undefined),
  });
  const review = useMutation({
    mutationFn: (approve: boolean) => api.finance.reviewClosing(open!.id, approve, note || undefined),
    onSuccess: () => { notify.ok("Đã xử lý biên bản."); setOpen(null); setNote(""); void queryClient.invalidateQueries({ queryKey: ["closings"] }); },
    onError: notify.fail,
  });

  return (
    <Card title="Biên bản kết ca" extra={
      <Segmented value={status} onChange={(v) => setStatus(v as "Submitted" | "")} options={[{ value: "Submitted", label: "Chờ duyệt" }, { value: "", label: "60 ngày gần đây" }]} />
    }>
      <Table
        rowKey="id" size="middle" loading={closings.isLoading} dataSource={closings.data} scroll={{ x: "max-content" }} pagination={{ pageSize: 20 }}
        onRow={(r) => ({ onClick: () => setOpen(r), style: { cursor: "pointer" } })}
        columns={[
          { title: "Ca", render: (_, r) => `${shiftTypeLabels[r.shiftType]} ${dayMonth(r.date)} ${time(r.startAt)}–${time(r.endAt)}` },
          { title: "Người kết", render: (_, r) => r.closing.closedByName },
          { title: "Dự kiến", render: (_, r) => vnd(r.closing.expectedCash) },
          { title: "Đếm được", render: (_, r) => vnd(r.closing.countedCash) },
          {
            title: "Chênh lệch", render: (_, r) => (
              <Typography.Text type={r.closing.difference === 0 ? undefined : "danger"}>{vnd(r.closing.difference)}</Typography.Text>
            ),
          },
          { title: "Trạng thái", render: (_, r) => <Tag color={closingLabels[r.closing.status].color}>{closingLabels[r.closing.status].label}</Tag> },
        ]}
      />
      <Modal open={!!open} title="Biên bản kết ca" onCancel={() => setOpen(null)} footer={open?.closing.status === "Submitted" ? (
        <Space>
          <Button danger loading={review.isPending} onClick={() => review.mutate(false)}>Từ chối</Button>
          <Button type="primary" loading={review.isPending} onClick={() => review.mutate(true)}>Duyệt</Button>
        </Space>
      ) : null}>
        {open && (
          <Space direction="vertical" style={{ width: "100%" }}>
            <Descriptions column={1} size="small" bordered items={[
              { label: "Ca", children: `${shiftTypeLabels[open.shiftType]} ${dayMonth(open.date)}` },
              { label: "Người kết ca", children: open.closing.closedByName },
              { label: "Nộp lúc", children: dateTime(open.closing.submittedAt) },
              { label: "Tiền dự kiến", children: vnd(open.closing.expectedCash) },
              { label: "Tiền đếm được", children: vnd(open.closing.countedCash) },
              { label: "Chênh lệch", children: vnd(open.closing.difference) },
              { label: "Ghi chú", children: open.closing.note ?? "—" },
              { label: "Mệnh giá", children: Object.entries(open.closing.denominations).map(([k, v]) => `${vnd(Number(k))} × ${v}`).join(", ") || "—" },
              ...(open.closing.reviewNote ? [{ label: "Ghi chú duyệt", children: open.closing.reviewNote }] : []),
            ]} />
            {open.closing.status === "Submitted" && <Input.TextArea placeholder="Ghi chú (bắt buộc khi từ chối)" value={note} onChange={(e) => setNote(e.target.value)} />}
          </Space>
        )}
      </Modal>
    </Card>
  );
}
