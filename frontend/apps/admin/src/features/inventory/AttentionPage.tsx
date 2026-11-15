import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Badge, Button, Card, Empty, Input, InputNumber, Modal, Space, Spin, Table, Tag, Typography } from "antd";
import type { AttentionHandover, InventoryIssue, StockDocument } from "@cafecr/api-client";
import {
  ConfirmDialog, CountSummaryTable, dateTime, errorText, handoverStatusLabels, issueKindLabels, shiftTypeLabels, stockDocumentTypeLabels, time,
} from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function AttentionPage() {
  return <RequireBranch title="Kho · Cần xử lý">{(branchId) => <Attention branchId={branchId} />}</RequireBranch>;
}

function Attention({ branchId }: { branchId: string }) {
  const attention = useQuery({ queryKey: ["inventory-attention", branchId], queryFn: () => api.inventory.attention(branchId), refetchInterval: 30_000 });
  const [countId, setCountId] = useState<string | null>(null);
  const [resolving, setResolving] = useState<AttentionHandover | null>(null);
  const data = attention.data;
  if (attention.isLoading) return <Spin />;
  if (attention.isError) return <Alert type="error" showIcon message={errorText(attention.error)} />;
  if (!data || data.total === 0) return <Card title="Kho · Cần xử lý"><Empty description="Không có việc cần xử lý." /></Card>;
  const shift = (t: string | null, a: string | null, b: string | null) => (t ? `${shiftTypeLabels[t as keyof typeof shiftTypeLabels]} ${time(a)}–${time(b)}` : "");

  return (
    <Space direction="vertical" style={{ width: "100%" }} size="large">
      <Card title={<Badge count={data.pendingCounts.length} offset={[12, 0]}>Phiếu đếm chờ duyệt</Badge>}>
        <Table rowKey="countId" size="middle" pagination={false} dataSource={data.pendingCounts} locale={{ emptyText: "Không có" }}
          columns={[
            { title: "Mã", dataIndex: "code" },
            { title: "Ca", render: (_, c) => shift(c.shiftType, c.shiftStart, c.shiftEnd) },
            { title: "Người đếm", dataIndex: "confirmedByName" },
            { title: "Lúc", dataIndex: "confirmedAt", render: dateTime },
            { title: "Vượt ngưỡng", dataIndex: "outOfTolerance", render: (v: number) => <Tag color="red">{v} món</Tag> },
            { title: "Lý do", dataIndex: "reason" },
            { title: "", render: (_, c) => <Button type="primary" onClick={() => setCountId(c.countId)}>Xem & duyệt</Button> },
          ]} />
      </Card>
      <Card title={<Badge count={data.handovers.length} offset={[12, 0]}>Bàn giao kho ngoài</Badge>}>
        <Table rowKey="countId" size="middle" pagination={false} dataSource={data.handovers} locale={{ emptyText: "Không có" }}
          columns={[
            { title: "Mã", dataIndex: "code" },
            { title: "Ca", render: (_, h) => shift(h.shiftType, h.shiftStart, h.shiftEnd) },
            { title: "Trạng thái", dataIndex: "status", render: (s: AttentionHandover["status"]) => <Tag color={handoverStatusLabels[s].color}>{handoverStatusLabels[s].label}</Tag> },
            { title: "Chờ", dataIndex: "minutesWaiting", render: (m: number) => `${m} phút` },
            { title: "Ca sau", render: (_, h) => h.receivedByName ? `${h.receivedByName}: ${h.disputeNote ?? ""}` : "Chưa nhận" },
            { title: "", render: (_, h) => <Button onClick={() => setResolving(h)}>Chốt số</Button> },
          ]} />
      </Card>
      <DraftAdjustments branchId={branchId} docs={data.draftAdjustments} />
      <Issues branchId={branchId} title="Ghi phiếu sai (lôi hàng ghi nhầm số)" issues={data.misrecordedIssues} />
      <Issues branchId={branchId} title="Hao hụt thật / dư không rõ" issues={data.lossIssues} />
      {countId && <ReviewCountModal branchId={branchId} countId={countId} onClose={() => setCountId(null)} />}
      {resolving && <ResolveModal branchId={branchId} handover={resolving} onClose={() => setResolving(null)} />}
    </Space>
  );
}

function useRefresh(branchId: string) {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: ["inventory-attention", branchId] });
    void queryClient.invalidateQueries({ queryKey: ["balances", branchId] });
  };
}

function ReviewCountModal({ branchId, countId, onClose }: { branchId: string; countId: string; onClose: () => void }) {
  const notify = useNotify();
  const refresh = useRefresh(branchId);
  const summary = useQuery({ queryKey: ["count-summary", countId], queryFn: () => api.inventory.countSummary("Outer", countId) });
  const [note, setNote] = useState("");
  const [rejecting, setRejecting] = useState(false);
  const approve = useMutation({ mutationFn: () => api.inventory.approve(countId, note.trim() || null), onSuccess: () => { notify.ok("Đã duyệt phiếu đếm."); refresh(); onClose(); }, onError: notify.fail });
  const reject = useMutation({ mutationFn: (r: string) => api.inventory.reject(countId, r), onSuccess: () => { notify.ok("Đã từ chối; ca có thể đếm lại."); refresh(); onClose(); }, onError: notify.fail });

  return (
    <Modal open width={960} title="Duyệt phiếu đếm kho ngoài" onCancel={onClose} footer={[
      <Button key="reject" danger onClick={() => setRejecting(true)}>Từ chối, yêu cầu đếm lại</Button>,
      <Button key="approve" type="primary" loading={approve.isPending} onClick={() => approve.mutate()}>Duyệt số đếm</Button>,
    ]}>
      {summary.data ? (
        <>
          <CountSummaryTable summary={summary.data} />
          <Alert style={{ marginTop: 12 }} type="warning" showIcon message={`Lý do của nhân viên: ${summary.data.reason ?? "—"}`} />
          <Input.TextArea style={{ marginTop: 12 }} rows={2} placeholder="Ghi chú duyệt (không bắt buộc)" value={note} onChange={(e) => setNote(e.target.value)} />
          <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>Duyệt chỉ xác nhận đã xem; muốn sửa số thì lập phiếu điều chỉnh. Từ chối sẽ đảo tồn của phiếu và cho đếm lại.</Typography.Paragraph>
        </>
      ) : <Spin />}
      <ConfirmDialog open={rejecting} title="Từ chối phiếu đếm?" body="Tồn sổ kho ngoài quay về trước lần đếm này và ca cần đếm lại." danger
        requireReason reasonMinLength={5} confirmText="Từ chối phiếu" busy={reject.isPending} onCancel={() => setRejecting(false)} onConfirm={(r) => reject.mutate(r)} />
    </Modal>
  );
}

function ResolveModal({ branchId, handover, onClose }: { branchId: string; handover: AttentionHandover; onClose: () => void }) {
  const notify = useNotify();
  const refresh = useRefresh(branchId);
  const summary = useQuery({ queryKey: ["count-summary", handover.countId], queryFn: () => api.inventory.countSummary("Outer", handover.countId) });
  const [final, setFinal] = useState<Record<string, number>>({});
  const [note, setNote] = useState("");
  const rows = summary.data?.rows ?? [];
  const disputed = Object.fromEntries((summary.data?.handover?.disputeLines ?? []).map((l) => [l.itemId, l.countedQty]));
  const resolve = useMutation({
    mutationFn: () => api.inventory.resolveHandover(handover.countId, rows.map((r) => ({ itemId: r.itemId, finalQty: final[r.itemId] ?? disputed[r.itemId] ?? r.counted ?? 0 })), note.trim()),
    onSuccess: () => { notify.ok("Đã chốt số bàn giao."); refresh(); onClose(); },
    onError: notify.fail,
  });

  return (
    <Modal open width={760} title={`Chốt số bàn giao ${handover.code}`} onCancel={onClose} footer={[
      <Button key="close" onClick={onClose}>Huỷ</Button>,
      <Button key="ok" type="primary" disabled={!note.trim()} loading={resolve.isPending} onClick={() => resolve.mutate()}>Chốt số</Button>,
    ]}>
      {handover.disputeNote && <Alert style={{ marginBottom: 12 }} type="warning" showIcon message={`Ca sau (${handover.receivedByName}): ${handover.disputeNote}`} />}
      <Table rowKey="itemId" size="small" pagination={false} dataSource={rows} loading={summary.isLoading}
        columns={[
          { title: "Mặt hàng", dataIndex: "name" },
          { title: "Ca trước đếm", dataIndex: "counted" },
          { title: "Ca sau đếm", render: (_, r) => disputed[r.itemId] ?? "—" },
          { title: "Số chốt", render: (_, r) => <InputNumber min={0} value={final[r.itemId] ?? disputed[r.itemId] ?? r.counted ?? 0} onChange={(v) => setFinal({ ...final, [r.itemId]: Number(v ?? 0) })} /> },
        ]} />
      <Input.TextArea style={{ marginTop: 12 }} rows={2} placeholder="Ghi chú (bắt buộc)" value={note} onChange={(e) => setNote(e.target.value)} />
      <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>Chênh lệch so với số ca trước được ghi bằng phiếu điều chỉnh; đầu ca sau = số chốt.</Typography.Paragraph>
    </Modal>
  );
}

function DraftAdjustments({ branchId, docs }: { branchId: string; docs: StockDocument[] }) {
  const notify = useNotify();
  const refresh = useRefresh(branchId);
  const [rejecting, setRejecting] = useState<string | null>(null);
  const approve = useMutation({ mutationFn: (id: string) => api.inventory.approve(id, null), onSuccess: () => { notify.ok("Đã duyệt và ghi sổ phiếu điều chỉnh."); refresh(); }, onError: notify.fail });
  const reject = useMutation({ mutationFn: ({ id, note }: { id: string; note: string }) => api.inventory.reject(id, note), onSuccess: () => { notify.ok("Đã bỏ đề xuất."); setRejecting(null); refresh(); }, onError: notify.fail });
  if (docs.length === 0) return null;
  return (
    <Card title={<Badge count={docs.length} offset={[12, 0]}>Phiếu điều chỉnh đề xuất</Badge>}>
      <Table<StockDocument> rowKey="id" size="middle" pagination={false} dataSource={docs}
        columns={[
          { title: "Mã", dataIndex: "code" },
          { title: "Loại", dataIndex: "type", render: (t: StockDocument["type"]) => stockDocumentTypeLabels[t] },
          { title: "Nội dung", render: (_, d) => d.lines.map((l) => `${l.itemName} ${l.qty > 0 ? "+" : ""}${l.qty} ${l.baseUnit}`).join(", ") },
          { title: "Lý do", render: (_, d) => <>{d.reason}{d.note && <><br /><Typography.Text type="secondary">{d.note}</Typography.Text></>}</> },
          { title: "", render: (_, d) => (
            <Space>
              <Button size="small" onClick={() => setRejecting(d.id)}>Bỏ qua</Button>
              <Button size="small" type="primary" loading={approve.isPending && approve.variables === d.id} onClick={() => approve.mutate(d.id)}>
                {d.fromWarehouse || d.toWarehouse ? "Duyệt & ghi sổ" : "Xác nhận ghi nhận"}
              </Button>
            </Space>
          ) },
        ]} />
      <ConfirmDialog open={rejecting !== null} title="Bỏ phiếu điều chỉnh đề xuất?" body="Tồn kho không thay đổi." requireReason reasonMinLength={5}
        confirmText="Bỏ đề xuất" busy={reject.isPending} onCancel={() => setRejecting(null)} onConfirm={(note) => reject.mutate({ id: rejecting!, note })} />
    </Card>
  );
}

function Issues({ branchId, title, issues }: { branchId: string; title: string; issues: InventoryIssue[] }) {
  const notify = useNotify();
  const refresh = useRefresh(branchId);
  const [closing, setClosing] = useState<string | null>(null);
  const resolve = useMutation({ mutationFn: ({ id, note }: { id: string; note: string }) => api.inventory.resolveIssue(id, note), onSuccess: () => { notify.ok("Đã đóng issue."); setClosing(null); refresh(); }, onError: notify.fail });
  if (issues.length === 0) return null;
  return (
    <Card title={<Badge count={issues.length} offset={[12, 0]}>{title}</Badge>}>
      <Table<InventoryIssue> rowKey="id" size="middle" pagination={false} dataSource={issues} scroll={{ x: "max-content" }}
        columns={[
          { title: "Mặt hàng", dataIndex: "itemName" },
          { title: "Loại", dataIndex: "kind", render: (k: InventoryIssue["kind"]) => <Tag color={issueKindLabels[k].color}>{issueKindLabels[k].label}</Tag> },
          { title: "Kỳ", render: (_, i) => `${dateTime(i.periodFrom)} → ${dateTime(i.periodTo)}` },
          { title: "Lệch kho trong", dataIndex: "innerVar" },
          { title: "Lệch kho ngoài", dataIndex: "outerVar" },
          { title: "Tổng", dataIndex: "total", render: (v: number, i) => <b>{v} {i.baseUnit}</b> },
          { title: "Ngưỡng", dataIndex: "tolerance", render: (v: number) => `±${v}` },
          { title: "", render: (_, i) => <Button size="small" onClick={() => setClosing(i.id)}>Đã xử lý</Button> },
        ]} />
      <ConfirmDialog open={closing !== null} title="Đóng issue đối soát?" body="Ghi lại cách đã xử lý (ví dụ: đã nhắc người lập phiếu, đã kiểm tra camera)."
        requireReason reasonMinLength={5} reasonLabel="Cách xử lý" confirmText="Đóng issue" busy={resolve.isPending}
        onCancel={() => setClosing(null)} onConfirm={(note) => resolve.mutate({ id: closing!, note })} />
    </Card>
  );
}
