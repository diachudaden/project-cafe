import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, List, Space, Spin, Typography } from "antd";
import { ApiError, type ConfirmCountResult, type CountSummary } from "@cafecr/api-client";
import { ConfirmDialog, CountSummaryTable, QtyStepper, errorText } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

const REASON_MIN = 10;

/**
 * Shift-close outer count (TASK-027 §4): blind count → summary → confirm popup.
 * The count screen never sees the expected quantity; "Xem tổng hợp" saves the first count.
 * Usual case with no variance: "Xem tổng hợp" → "Xác nhận số đếm" → popup "Xác nhận" (3 taps after counting).
 */
export function OuterCountStep({ shiftId, shiftLabel, onBack, onDone }: {
  shiftId: string; shiftLabel: string; onBack: () => void; onDone: (result: ConfirmCountResult | null) => void;
}) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const sheet = useQuery({ queryKey: ["shift-count", shiftId], queryFn: () => api.inventory.openShiftCount(shiftId), staleTime: Infinity });
  const [counts, setCounts] = useState<Record<string, number>>({});
  const [summary, setSummary] = useState<CountSummary | null>(null);
  const [asking, setAsking] = useState(false);

  useEffect(() => {
    if (sheet.data && Object.keys(counts).length === 0) {
      setCounts(Object.fromEntries(sheet.data.items.map((i) => [i.itemId, i.countedQty ?? 0])));
    }
  }, [sheet.data]);

  const review = useMutation({
    mutationFn: async () => {
      const s = sheet.data!;
      await api.inventory.saveCountLines("Outer", s.countId, s.items.map((i) => ({ itemId: i.itemId, countedQty: counts[i.itemId] ?? 0 })));
      return api.inventory.countSummary("Outer", s.countId);
    },
    onSuccess: setSummary,
    onError: notify.fail,
  });

  const confirm = useMutation({
    mutationFn: (reason: string) => api.inventory.confirmCount("Outer", sheet.data!.countId, reason || null),
    onSuccess: (result) => {
      setAsking(false);
      void queryClient.invalidateQueries({ queryKey: ["shift-count", shiftId] });
      notify.ok(result.status === "PendingApproval" ? "Đã khoá số đếm, có món vượt ngưỡng chờ quản lý duyệt." : "Đã khoá số đếm kho ngoài.");
      onDone(result);
    },
    onError: notify.fail,
  });

  if (sheet.isLoading) return <Spin />;
  if (sheet.isError) {
    const noItems = sheet.error instanceof ApiError && sheet.error.violations[0]?.rule === "INVALID_STATE";
    return (
      <Space direction="vertical" style={{ width: "100%" }}>
        <Alert type={noItems ? "info" : "error"} showIcon message={errorText(sheet.error)} />
        <Space>
          <Button onClick={onBack}>Quay lại</Button>
          {noItems && <Button type="primary" onClick={() => onDone(null)}>Bỏ qua đếm kho</Button>}
        </Space>
      </Space>
    );
  }

  const data = sheet.data!;
  if (data.status !== "Draft") {
    return (
      <Space direction="vertical" style={{ width: "100%" }}>
        <Alert type="success" showIcon message={`Số đếm kho ngoài ${data.code} đã được xác nhận.`} />
        <Space>
          <Button onClick={onBack}>Quay lại</Button>
          <Button type="primary" onClick={() => onDone(null)}>Tiếp: bàn giao</Button>
        </Space>
      </Space>
    );
  }

  if (!summary) {
    return (
      <Space direction="vertical" style={{ width: "100%" }}>
        <Typography.Text type="secondary">Đếm từng món trên kệ kho ngoài theo thứ tự. Có thể bấm "+1 hộp" để cộng nhanh theo đơn vị lớn.</Typography.Text>
        <List
          dataSource={data.items}
          renderItem={(item) => (
            <List.Item>
              <Space direction="vertical" size={4}>
                <Typography.Text strong>{item.name}</Typography.Text>
                <QtyStepper value={counts[item.itemId] ?? 0} baseUnit={item.baseUnit} units={item.units}
                  onChange={(v) => setCounts({ ...counts, [item.itemId]: v })} />
              </Space>
            </List.Item>
          )}
        />
        <Space>
          <Button onClick={onBack}>Quay lại</Button>
          <Button type="primary" size="large" loading={review.isPending} onClick={() => review.mutate()}>Xem tổng hợp</Button>
        </Space>
      </Space>
    );
  }

  const over = summary.rows.filter((r) => r.withinTolerance === false);
  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      <CountSummaryTable summary={summary} />
      <Space>
        <Button size="large" onClick={() => setSummary(null)}>Đếm lại</Button>
        <Button type="primary" size="large" onClick={() => setAsking(true)}>Xác nhận số đếm</Button>
      </Space>
      <ConfirmDialog
        open={asking}
        title={`Xác nhận số đếm kho ngoài – ${shiftLabel}?`}
        body="Sau khi xác nhận sẽ không sửa được. Sai thì quản lý lập phiếu điều chỉnh."
        requireReason={over.length > 0}
        reasonMinLength={REASON_MIN}
        reasonLabel="Lý do lệch"
        confirmText="Xác nhận"
        busy={confirm.isPending}
        onCancel={() => setAsking(false)}
        onConfirm={(reason) => confirm.mutate(reason)}>
        {over.length > 0 && (
          <Alert type="warning" showIcon message={`${over.length} món vượt ngưỡng`}
            description={<ul style={{ margin: 0, paddingLeft: 18 }}>{over.map((r) => (
              <li key={r.itemId}>{r.name}: đếm {r.counted}, dự kiến {r.expected} (lệch {r.variance! > 0 ? "+" : ""}{r.variance}, ngưỡng ±{r.tolerance})</li>
            ))}</ul>} />
        )}
      </ConfirmDialog>
    </Space>
  );
}

