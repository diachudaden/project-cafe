import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  PaymentEvents,
  attentionReasonLabel,
  formatVnd,
  paymentKeys,
  type PaymentNeedsAttentionEvent,
  type PaymentReceivedEvent,
  type SeatAssignedEvent,
  type SeatHoldExpiredEvent,
} from '@chonrieng/api-client/payments';
import { announcePayment, installAudioUnlock } from './paymentAlerts';
import { usePayments } from './PaymentsContext';

export interface PaymentToast {
  kind: 'success' | 'info' | 'warning';
  title: string;
  detail?: string;
}

/**
 * Gọi MỘT lần ở layout POS. Lắng nghe sự kiện thanh toán từ SignalR:
 * kêu "tink tink", đọc số tiền, hiện thông báo và làm mới dữ liệu liên quan.
 *
 * INTEGRATE: `notify` nối vào hệ toast của packages/ui (ví dụ toast.success).
 */
export function usePaymentEvents(notify: (t: PaymentToast) => void): void {
  const { connection } = usePayments();
  const qc = useQueryClient();
  const notifyRef = useRef(notify);
  useEffect(() => {
    notifyRef.current = notify;
  });

  useEffect(() => installAudioUnlock(), []);

  useEffect(() => {
    if (!connection) return;

    const onReceived = (e: PaymentReceivedEvent) => {
      announcePayment(e.amount);
      notifyRef.current({ kind: 'success', title: `Đã nhận ${formatVnd(e.amount)}`, detail: `Bill #${e.billId}` });
      void qc.invalidateQueries({ queryKey: paymentKeys.bill(e.billId) });
      void qc.invalidateQueries({ queryKey: ['ledger'] });
    };

    const onSeat = (e: SeatAssignedEvent) => {
      notifyRef.current({
        kind: e.isDefault ? 'info' : 'success',
        title: `Bill #${e.billId} → chỗ ${e.seatCode}`,
        detail: e.isDefault ? 'Chỗ khách chọn đã có người, hệ thống xếp chỗ khác cùng loại.' : undefined,
      });
      void qc.invalidateQueries({ queryKey: paymentKeys.seats() });
    };

    const onAttention = (e: PaymentNeedsAttentionEvent) => {
      notifyRef.current({
        kind: 'warning',
        title: attentionReasonLabel(e.reason),
        detail: [e.billId ? `Bill #${e.billId}` : null, e.amount ? formatVnd(e.amount) : null].filter(Boolean).join(' · '),
      });
      void qc.invalidateQueries({ queryKey: paymentKeys.attention() });
    };

    const onHoldExpired = (e: SeatHoldExpiredEvent) => {
      notifyRef.current({ kind: 'info', title: `Hết hạn giữ chỗ cho bill #${e.billId}` });
      void qc.invalidateQueries({ queryKey: paymentKeys.seats() });
    };

    connection.on(PaymentEvents.PaymentReceived, onReceived);
    connection.on(PaymentEvents.SeatAssigned, onSeat);
    connection.on(PaymentEvents.PaymentNeedsAttention, onAttention);
    connection.on(PaymentEvents.SeatHoldExpired, onHoldExpired);

    return () => {
      connection.off(PaymentEvents.PaymentReceived, onReceived);
      connection.off(PaymentEvents.SeatAssigned, onSeat);
      connection.off(PaymentEvents.PaymentNeedsAttention, onAttention);
      connection.off(PaymentEvents.SeatHoldExpired, onHoldExpired);
    };
  }, [connection, qc]);
}
