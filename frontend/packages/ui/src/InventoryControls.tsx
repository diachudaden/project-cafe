import { useState } from "react";
import { Alert, Button, Input, InputNumber, Modal, Space, Tag, Typography } from "antd";
import type { ItemUnit } from "@cafecr/api-client";
import { canConfirm, splitUnits, varianceState, type VarianceState } from "./inventory";

/** Base-unit stepper; with packing units, a second row adds "n thùng/hộp" in one tap each. */
export function QtyStepper({ value, baseUnit, units = [], onChange, min = 0, size = "large" }: {
  value: number; baseUnit: string; units?: ItemUnit[]; onChange: (value: number) => void; min?: number; size?: "middle" | "large";
}) {
  const set = (v: number) => onChange(Math.max(min, Math.trunc(v)));
  return (
    <Space direction="vertical" size={4}>
      <Space.Compact>
        <Button size={size} aria-label="Bớt 1" onClick={() => set(value - 1)}>−</Button>
        <InputNumber size={size} min={min} value={value} style={{ width: 88 }} inputMode="numeric"
          onChange={(v) => set(Number(v ?? 0))} addonAfter={baseUnit} />
        <Button size={size} aria-label="Thêm 1" onClick={() => set(value + 1)}>+</Button>
      </Space.Compact>
      {units.length > 0 && (
        <Space size={4} wrap>
          {[...units].sort((a, b) => a.factor - b.factor).map((u) => (
            <Button key={u.unitName} size="small" onClick={() => set(value + u.factor)}>+1 {u.unitName.toLowerCase()} ({u.factor})</Button>
          ))}
          {value > 0 && units.length > 0 && (
            <Typography.Text type="secondary">= {splitUnits(value, baseUnit, units).map((p) => `${p.qty} ${p.unitName.toLowerCase()}`).join(" + ")}</Typography.Text>
          )}
        </Space>
      )}
    </Space>
  );
}

const chip: Record<VarianceState, { color: string; label: string }> = {
  ok: { color: "green", label: "Khớp" },
  warn: { color: "gold", label: "Trong ngưỡng" },
  over: { color: "red", label: "Vượt ngưỡng" },
  pending: { color: "default", label: "Chưa đếm" },
};

export function VarianceChip({ variance, tolerance }: { variance: number | null | undefined; tolerance: number }) {
  const state = varianceState(variance, tolerance);
  const sign = variance && variance > 0 ? "+" : "";
  return <Tag color={chip[state].color}>{chip[state].label}{variance !== null && variance !== undefined && variance !== 0 ? ` (${sign}${variance})` : ""}</Tag>;
}

/**
 * App modal for irreversible actions (never window.confirm). With `requireReason`, the confirm button stays disabled
 * until the reason has `reasonMinLength` characters.
 */
export function ConfirmDialog({ open, title, body, requireReason = false, reasonMinLength = 10, reasonLabel = "Lý do",
  confirmText = "Xác nhận", cancelText = "Huỷ", danger = false, busy = false, onConfirm, onCancel, children }: {
  open: boolean; title: string; body: React.ReactNode; requireReason?: boolean; reasonMinLength?: number; reasonLabel?: string;
  confirmText?: string; cancelText?: string; danger?: boolean; busy?: boolean;
  onConfirm: (reason: string) => void; onCancel: () => void; children?: React.ReactNode;
}) {
  const [reason, setReason] = useState("");
  const enabled = canConfirm(requireReason, reason, reasonMinLength, busy);
  return (
    <Modal open={open} title={title} onCancel={busy ? undefined : onCancel} maskClosable={!busy} closable={!busy} destroyOnClose
      afterClose={() => setReason("")}
      footer={[
        <Button key="cancel" size="large" disabled={busy} onClick={onCancel}>{cancelText}</Button>,
        <Button key="ok" size="large" type="primary" danger={danger} disabled={!enabled} loading={busy} onClick={() => onConfirm(reason.trim())}>
          {confirmText}
        </Button>,
      ]}>
      <Space direction="vertical" style={{ width: "100%" }}>
        {typeof body === "string" ? <Typography.Paragraph style={{ marginBottom: 0 }}>{body}</Typography.Paragraph> : body}
        {children}
        {requireReason && (
          <>
            <Input.TextArea autoFocus rows={3} maxLength={1000} placeholder={`${reasonLabel} (ít nhất ${reasonMinLength} ký tự)`}
              value={reason} onChange={(e) => setReason(e.target.value)} />
            {reason.trim().length > 0 && reason.trim().length < reasonMinLength && (
              <Alert type="warning" showIcon message={`Cần thêm ${reasonMinLength - reason.trim().length} ký tự.`} />
            )}
          </>
        )}
      </Space>
    </Modal>
  );
}
