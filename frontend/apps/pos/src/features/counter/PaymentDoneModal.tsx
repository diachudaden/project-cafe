import type { ReactNode } from "react";
import { Button, Modal, Result, Space, Tag, Typography } from "antd";
import type { PaymentMethod } from "@cafecr/api-client";
import { displayName, paymentLabels, time, vnd } from "@cafecr/ui";

export interface PaymentDone {
  /** What was paid for: "Check-in", "Gia hạn", "Đồ thêm". */
  what: string;
  seatCode: string;
  amount: number;
  method: PaymentMethod;
  billCode: string;
  billTotal?: number;
  endAt?: string;
  customerName?: string | null;
  pointsAwarded: number;
  detail?: string;
}

/** The same "Đã thanh toán" screen for cash and VietQR: amount, bill code, member points, then "Xong". */
export function PaymentDoneModal({ done, onClose, actions }: { done: PaymentDone; onClose: () => void; actions?: ReactNode }) {
  return (
    <Modal open onCancel={onClose} footer={
      <Space>
        {actions}
        <Button type="primary" size="large" onClick={onClose} autoFocus>Xong</Button>
      </Space>
    }>
      <PaymentDoneResult done={done} />
    </Modal>
  );
}

export function PaymentDoneResult({ done }: { done: PaymentDone }) {
  const sub = [
    `${done.what} ${done.seatCode}`,
    `${vnd(done.amount)} · ${paymentLabels[done.method]}`,
    done.endAt ? `đến ${time(done.endAt)}` : null,
  ].filter(Boolean).join(" · ");
  return (
    <Result
      status="success"
      title="Đã thanh toán"
      subTitle={<>{sub}{done.detail && <div>{done.detail}</div>}</>}
      extra={
        <Space direction="vertical" align="center">
          <Typography.Text type="secondary">Mã bill{done.billTotal !== undefined ? ` · tổng ${vnd(done.billTotal)}` : ""}</Typography.Text>
          <Typography.Title copyable style={{ letterSpacing: 6, margin: 0 }}>{done.billCode}</Typography.Title>
          {done.pointsAwarded > 0 && done.customerName
            ? <Tag color="green" style={{ fontSize: 15, padding: "4px 10px" }}>+{done.pointsAwarded} điểm cho {displayName(done.customerName)}</Tag>
            : <Typography.Text type="secondary">Khách lẻ · khách có thể tự tích điểm bằng mã bill trong 7 ngày</Typography.Text>}
        </Space>
      }
    />
  );
}
