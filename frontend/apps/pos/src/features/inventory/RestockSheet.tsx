import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Drawer, Empty, Input, List, Space, Spin, Tag, Typography } from "antd";
import { QtyStepper, errorText, transferNeedsReason, unitsText } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

/**
 * "Lôi hàng ra": transfer inner → outer. Quantities are pre-filled with the suggestion (par − expected now),
 * so the usual case is open → "Lôi hàng" (2 taps). Above the suggestion or par asks for a reason; it never blocks.
 */
export function RestockSheet({ branchId, open, onClose }: { branchId: string; open: boolean; onClose: () => void }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const suggestion = useQuery({
    queryKey: ["transfer-suggestion", branchId],
    queryFn: () => api.inventory.transferSuggestion(branchId),
    enabled: open,
    staleTime: 0,
  });
  const [qty, setQty] = useState<Record<string, number>>({});
  const [reason, setReason] = useState("");

  useEffect(() => {
    if (open && suggestion.data) {
      setQty(Object.fromEntries(suggestion.data.map((s) => [s.itemId, s.suggested])));
      setReason("");
    }
  }, [open, suggestion.data]);

  const rows = suggestion.data ?? [];
  const lines = rows.filter((r) => (qty[r.itemId] ?? 0) > 0).map((r) => ({ itemId: r.itemId, qty: qty[r.itemId] }));
  const needsReason = useMemo(
    () => rows.some((r) => (qty[r.itemId] ?? 0) > 0 && transferNeedsReason(qty[r.itemId], r.suggested, r.expectedNow, r.par)),
    [rows, qty],
  );
  const overInner = rows.filter((r) => (qty[r.itemId] ?? 0) > r.innerQty);

  const submit = useMutation({
    mutationFn: () => api.inventory.createTransfer(branchId, lines, reason.trim() || null),
    onSuccess: (doc) => {
      notify.ok(`Đã lôi hàng ra kho ngoài (${doc.code}).`);
      void queryClient.invalidateQueries({ queryKey: ["transfer-suggestion", branchId] });
      onClose();
    },
    onError: notify.fail,
  });

  return (
    <Drawer open={open} onClose={onClose} title="Lôi hàng ra kho ngoài" placement="right" width={520} destroyOnClose
      extra={
        <Button type="primary" size="large" disabled={lines.length === 0 || (needsReason && !reason.trim()) || overInner.length > 0}
          loading={submit.isPending} onClick={() => submit.mutate()}>
          Lôi hàng ({lines.length} món)
        </Button>
      }>
      {suggestion.isLoading && <Spin />}
      {suggestion.isError && <Alert type="error" showIcon message={errorText(suggestion.error)} />}
      {suggestion.data && (
        <Space direction="vertical" style={{ width: "100%" }}>
          <Typography.Text type="secondary">Số đã điền sẵn = mức tối đa kho ngoài − tồn dự kiến. Sửa nếu cần.</Typography.Text>
          <List
            dataSource={rows}
            locale={{ emptyText: <Empty description="Chưa cấu hình mặt hàng cho chi nhánh." /> }}
            renderItem={(r) => {
              const value = qty[r.itemId] ?? 0;
              const above = value > 0 && transferNeedsReason(value, r.suggested, r.expectedNow, r.par);
              return (
                <List.Item>
                  <Space direction="vertical" style={{ width: "100%" }} size={4}>
                    <Space wrap>
                      <Typography.Text strong>{r.itemName}</Typography.Text>
                      {r.expectedNow < r.min && <Tag color="red">Dưới mức tối thiểu</Tag>}
                      {above && <Tag color="gold">Vượt gợi ý</Tag>}
                    </Space>
                    <Typography.Text type="secondary">
                      Dự kiến còn {r.expectedNow} · tối đa {r.par} · gợi ý {r.suggested} · kho trong {unitsText(r.innerQty, r.baseUnit, r.units)}
                    </Typography.Text>
                    <QtyStepper value={value} baseUnit={r.baseUnit} units={r.units} onChange={(v) => setQty({ ...qty, [r.itemId]: v })} />
                    {value > r.innerQty && <Typography.Text type="danger">Kho trong chỉ còn {r.innerQty} {r.baseUnit}.</Typography.Text>}
                  </Space>
                </List.Item>
              );
            }}
          />
          {needsReason && (
            <Input.TextArea rows={2} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)}
              placeholder="Lý do lôi nhiều hơn gợi ý (bắt buộc)" />
          )}
        </Space>
      )}
    </Drawer>
  );
}
