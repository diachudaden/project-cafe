import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Drawer, Empty, Input, List, Space, Spin, Typography } from "antd";
import { QtyStepper, errorText } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

/**
 * "Huỷ hàng": waste document (PH) on the outer warehouse for spoiled / broken stock during the shift.
 * Reason is required by the API; the outer book may go below zero (it is only an estimate until the shift count).
 */
export function WasteSheet({ branchId, open, onClose }: { branchId: string; open: boolean; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  // The transfer suggestion lists the branch's configured items with the outer estimate, which is what staff need here.
  const items = useQuery({
    queryKey: ["transfer-suggestion", branchId],
    queryFn: () => api.inventory.transferSuggestion(branchId),
    enabled: open,
    staleTime: 0,
  });
  const [qty, setQty] = useState<Record<string, number>>({});
  const [reason, setReason] = useState("");
  const [photoUrl, setPhotoUrl] = useState("");

  useEffect(() => {
    if (open) {
      setQty({});
      setReason("");
      setPhotoUrl("");
    }
  }, [open]);

  const rows = items.data ?? [];
  const lines = rows.filter((r) => (qty[r.itemId] ?? 0) > 0).map((r) => ({ itemId: r.itemId, qty: qty[r.itemId] }));

  const submit = useMutation({
    mutationFn: () => api.inventory.createWaste(branchId, "Outer", lines, reason.trim(), photoUrl.trim() || null),
    onSuccess: (doc) => {
      notify.ok(`Đã ghi phiếu huỷ ${doc.code}.`);
      void queryClient.invalidateQueries({ queryKey: ["transfer-suggestion", branchId] });
      onClose();
    },
    onError: notify.fail,
  });

  return (
    <Drawer open={open} onClose={onClose} title="Huỷ hàng hỏng / đổ vỡ (kho ngoài)" placement="right" width={520} destroyOnClose
      extra={
        <Button danger type="primary" size="large" disabled={lines.length === 0 || !reason.trim()}
          loading={submit.isPending} onClick={() => submit.mutate()}>
          Ghi huỷ ({lines.length} món)
        </Button>
      }>
      {items.isLoading && <Spin />}
      {items.isError && <Alert type="error" showIcon message={errorText(items.error)} />}
      {items.data && (
        <Space direction="vertical" style={{ width: "100%" }}>
          <Typography.Text type="secondary">Chọn số lượng bị hỏng, đổ vỡ hoặc hết hạn. Món không huỷ để 0.</Typography.Text>
          <List
            dataSource={rows}
            locale={{ emptyText: <Empty description="Chưa cấu hình mặt hàng cho chi nhánh." /> }}
            renderItem={(r) => (
              <List.Item>
                <Space direction="vertical" style={{ width: "100%" }} size={4}>
                  <Typography.Text strong>{r.itemName}</Typography.Text>
                  <Typography.Text type="secondary">Dự kiến còn {r.expectedNow} {r.baseUnit}</Typography.Text>
                  <QtyStepper value={qty[r.itemId] ?? 0} baseUnit={r.baseUnit} units={r.units} onChange={(v) => setQty({ ...qty, [r.itemId]: v })} />
                </Space>
              </List.Item>
            )}
          />
          <Input.TextArea rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)}
            placeholder="Lý do huỷ (bắt buộc), ví dụ: làm đổ 2 chai, sữa hết hạn" />
          <Input value={photoUrl} onChange={(e) => setPhotoUrl(e.target.value)} placeholder="Link ảnh (không bắt buộc)" />
        </Space>
      )}
    </Drawer>
  );
}
