import { useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Alert, Button, Descriptions, Input, InputNumber, Modal, Space, Statistic, Steps, Table, Typography } from "antd";
import { closingLabels, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

const NOTES = [500000, 200000, 100000, 50000, 20000, 10000, 5000, 2000, 1000];

/** "Rời ca": count cash by denomination → compare with expected → hand over (submit + auto logout). */
export function ClosingWizard({ shiftId, onClose, onDone }: { shiftId: string; onClose: () => void; onDone: () => void }) {
  const notify = useNotify();
  const preview = useQuery({ queryKey: ["closing-preview", shiftId], queryFn: () => api.finance.closingPreview(shiftId) });
  const [step, setStep] = useState(0);
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [note, setNote] = useState("");

  // Resume a saved draft (reference: "kết ca có thể lưu nháp").
  useEffect(() => {
    const draft = preview.data?.closing;
    if (draft && Object.keys(counts).length === 0) {
      setCounts(draft.denominations);
      setNote(draft.note ?? "");
    }
  }, [preview.data]);

  const counted = NOTES.reduce((sum, n) => sum + n * (counts[String(n)] ?? 0), 0);
  const expected = preview.data?.expectedCash ?? 0;
  const difference = counted - expected;
  const save = useMutation({
    mutationFn: (submit: boolean) => api.finance.submitClosing(shiftId, counts, note || null, submit),
    onSuccess: (_, submit) => {
      if (submit) {
        Modal.success({ title: "Đã kết ca", content: "Biên bản đã gửi quản lý duyệt. Bạn sẽ được đăng xuất.", onOk: onDone });
      } else notify.ok("Đã lưu nháp.");
    },
    onError: notify.fail,
  });
  const locked = preview.data?.closing && ["Submitted", "Approved"].includes(preview.data.closing.status);

  return (
    <Modal open width={640} title="Kết ca" onCancel={onClose} footer={null}>
      <Steps current={step} size="small" style={{ marginBottom: 16 }} items={[{ title: "Đếm tiền" }, { title: "Đối chiếu" }, { title: "Bàn giao" }]} />
      {locked && <Alert type="info" message={`Biên bản ${closingLabels[preview.data!.closing!.status].label.toLowerCase()}.`} />}
      {!locked && step === 0 && (
        <>
          <Table size="small" pagination={false} rowKey={(n) => n} dataSource={NOTES}
            columns={[
              { title: "Mệnh giá", render: (_, n) => vnd(n) },
              {
                title: "Số tờ", render: (_, n) => (
                  <InputNumber min={0} value={counts[String(n)] ?? 0} onChange={(v) => setCounts({ ...counts, [String(n)]: Number(v ?? 0) })} />
                ),
              },
              { title: "Thành tiền", render: (_, n) => vnd(n * (counts[String(n)] ?? 0)) },
            ]} />
          <Statistic title="Tổng đếm được" value={vnd(counted)} style={{ marginTop: 12 }} />
          <Space style={{ marginTop: 12 }}>
            <Button loading={save.isPending} onClick={() => save.mutate(false)}>Lưu nháp</Button>
            <Button type="primary" onClick={() => setStep(1)}>Tiếp: đối chiếu</Button>
          </Space>
        </>
      )}
      {!locked && step === 1 && preview.data && (
        <>
          <Descriptions column={1} bordered size="small" items={[
            { label: "Hóa đơn trong ca", children: preview.data.billCount },
            { label: "Tiền mặt dự kiến", children: vnd(expected) },
            { label: "VietQR (tham khảo)", children: vnd(preview.data.vietQrTotal) },
            { label: "Tiền đếm được", children: vnd(counted) },
            { label: "Chênh lệch", children: <Typography.Text type={difference === 0 ? "success" : "danger"} strong>{vnd(difference)}</Typography.Text> },
          ]} />
          {preview.data.activeSessions > 0 && <Alert style={{ marginTop: 12 }} type="info" message={`Còn ${preview.data.activeSessions} khách đang ngồi; ca sau sẽ tiếp tục phục vụ.`} />}
          {difference !== 0 && <Input.TextArea style={{ marginTop: 12 }} placeholder="Lý do chênh lệch (bắt buộc)" value={note} onChange={(e) => setNote(e.target.value)} />}
          <Space style={{ marginTop: 12 }}>
            <Button onClick={() => setStep(0)}>Quay lại</Button>
            <Button type="primary" disabled={difference !== 0 && !note.trim()} onClick={() => setStep(2)}>Tiếp: bàn giao</Button>
          </Space>
        </>
      )}
      {!locked && step === 2 && (
        <>
          <Typography.Paragraph>Xác nhận bàn giao {vnd(counted)} tiền mặt cho ca sau. Sau khi nộp, bạn sẽ rời ca và được đăng xuất.</Typography.Paragraph>
          <Space>
            <Button onClick={() => setStep(1)}>Quay lại</Button>
            <Button type="primary" danger loading={save.isPending} onClick={() => save.mutate(true)}>Nộp biên bản & rời ca</Button>
          </Space>
        </>
      )}
    </Modal>
  );
}
