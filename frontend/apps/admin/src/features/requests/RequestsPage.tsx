import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Input, Popconfirm, Segmented, Space, Table, Tag } from "antd";
import { dateTime, dayMonth, requestStatusLabels, requestTypeLabels, shiftTypeLabels, time } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function RequestsPage() {
  return <RequireBranch title="Đơn từ">{(branchId) => <Requests branchId={branchId} />}</RequireBranch>;
}

function Requests({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<string>("Pending");
  const [note, setNote] = useState("");
  const requests = useQuery({ queryKey: ["requests", branchId, status], queryFn: () => api.workforce.requests(branchId, status || undefined) });
  const review = useMutation({
    mutationFn: ({ id, approve }: { id: string; approve: boolean }) =>
      approve ? api.workforce.approveRequest(id, note || undefined) : api.workforce.rejectRequest(id, note || undefined),
    onSuccess: () => { notify.ok("Đã xử lý đơn; lịch ca đã cập nhật."); setNote(""); void queryClient.invalidateQueries(); },
    onError: notify.fail,
  });

  return (
    <Card title="Đơn đổi ca / xin nghỉ / trả ca" extra={
      <Segmented value={status} onChange={(v) => setStatus(v as string)} options={[{ value: "Pending", label: "Chờ duyệt" }, { value: "", label: "Tất cả" }]} />
    }>
      <Table
        rowKey="id" size="middle" loading={requests.isLoading} dataSource={requests.data} scroll={{ x: "max-content" }} pagination={{ pageSize: 20 }}
        columns={[
          { title: "Loại", dataIndex: "type", render: (t) => requestTypeLabels[t as keyof typeof requestTypeLabels] },
          { title: "Ca", render: (_, r) => `${shiftTypeLabels[r.shiftType]} ${dayMonth(r.date)} ${time(r.startAt)}–${time(r.endAt)}` },
          { title: "Người gửi", dataIndex: "requesterName" },
          { title: "Nhận thay", dataIndex: "targetName", render: (v) => v ?? "—" },
          { title: "Lý do", dataIndex: "reason" },
          { title: "Gửi lúc", dataIndex: "createdAt", render: dateTime },
          { title: "Trạng thái", dataIndex: "status", render: (s: string) => <Tag color={requestStatusLabels[s]?.color}>{requestStatusLabels[s]?.label ?? s}</Tag> },
          {
            title: "", render: (_, r) => r.status === "Pending" && (
              <Space>
                <Popconfirm title="Duyệt đơn này?" description={<Input placeholder="Ghi chú (tùy chọn)" value={note} onChange={(e) => setNote(e.target.value)} />}
                  onConfirm={() => review.mutate({ id: r.id, approve: true })}>
                  <Button size="small" type="primary">Duyệt</Button>
                </Popconfirm>
                <Popconfirm title="Từ chối đơn này?" description={<Input placeholder="Lý do" value={note} onChange={(e) => setNote(e.target.value)} />}
                  onConfirm={() => review.mutate({ id: r.id, approve: false })}>
                  <Button size="small" danger>Từ chối</Button>
                </Popconfirm>
              </Space>
            ),
          },
        ]}
      />
    </Card>
  );
}
