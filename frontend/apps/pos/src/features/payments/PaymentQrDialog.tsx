import { useCallback, useEffect, useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import {
  PaymentApiError,
  PaymentEvents,
  formatVnd,
  type PaymentQrDto,
  type PaymentReceivedEvent,
  type PaymentResultDto,
  type SeatAssignedEvent,
} from '@chonrieng/api-client/payments';
import { usePayments } from './PaymentsContext';
import './payments.css';

export interface PaidInfo {
  billId: number;
  amount: number;
  seatCode: string | null;
  seatIsDefault: boolean;
}

interface Props {
  billId: number;
  onPaid?: (info: PaidInfo) => void;
  onClose: () => void;
}

type View =
  | { kind: 'loading' }
  | { kind: 'qr'; qr: PaymentQrDto }
  | { kind: 'expired'; qr: PaymentQrDto }
  | { kind: 'paid'; info: PaidInfo }
  | { kind: 'error'; message: string };

const errorText = (e: unknown) =>
  e instanceof PaymentApiError
    ? e.code === 'SEAT_TAKEN'
      ? 'Chỗ này vừa có người giữ. Chọn chỗ khác rồi tạo lại mã.'
      : e.message
    : 'Không kết nối được máy chủ.';

/**
 * Hộp thoại thanh toán của một bill: hiện mã VietQR, đếm ngược thời gian giữ chỗ,
 * tự chuyển sang "Đã nhận" khi webhook báo tiền về. Có nút thu tiền mặt cho nhân viên.
 */
export function PaymentQrDialog({ billId, onPaid, onClose }: Props) {
  const { api, connection } = usePayments();
  const [view, setView] = useState<View>({ kind: 'loading' });
  const [secondsLeft, setSecondsLeft] = useState(0);

  // onPaid chỉ gọi một lần, dù tiền báo về qua webhook hay nhân viên bấm "tiền mặt".
  const paidNotified = useRef(false);
  const markPaid = useCallback(
    (info: PaidInfo) => {
      setView({ kind: 'paid', info });
      if (!paidNotified.current) {
        paidNotified.current = true;
        onPaid?.(info);
      }
    },
    [onPaid],
  );

  const createQr = useMutation({
    mutationFn: () => api.createQr(billId),
    onSuccess: (qr) => setView({ kind: 'qr', qr }),
    onError: (e) => setView({ kind: 'error', message: errorText(e) }),
  });

  const payCash = useMutation({
    mutationFn: () => api.payCash(billId),
    onSuccess: (r: PaymentResultDto) =>
      markPaid({ billId, amount: r.amount, seatCode: r.seatCode, seatIsDefault: r.seatIsDefault }),
    onError: (e) => setView({ kind: 'error', message: errorText(e) }),
  });

  // Tạo mã khi mở hộp thoại.
  const { mutate: requestQr } = createQr;
  useEffect(() => {
    requestQr();
  }, [billId, requestQr]);

  // Đếm ngược thời gian giữ chỗ.
  const expiresAt = view.kind === 'qr' ? view.qr.expiresAtUtc : null;
  useEffect(() => {
    if (!expiresAt) return;
    const end = new Date(expiresAt).getTime();
    const tick = () => {
      const left = Math.max(0, Math.round((end - Date.now()) / 1000));
      setSecondsLeft(left);
      if (left === 0) setView((v) => (v.kind === 'qr' ? { kind: 'expired', qr: v.qr } : v));
    };
    tick();
    const id = window.setInterval(tick, 1000);
    return () => window.clearInterval(id);
  }, [expiresAt]);

  // Nghe tiền về cho đúng bill này. Âm báo do usePaymentEvents ở layout phát, ở đây chỉ đổi giao diện.
  // Server gửi PaymentReceived trước, SeatAssigned ngay sau (nếu bill cần chỗ).
  useEffect(() => {
    if (!connection) return;

    const onReceived = (e: PaymentReceivedEvent) => {
      if (e.billId !== billId) return;
      markPaid({ billId, amount: e.amount, seatCode: null, seatIsDefault: false });
    };
    const onSeat = (e: SeatAssignedEvent) => {
      if (e.billId !== billId) return;
      setView((v) =>
        v.kind === 'paid' ? { kind: 'paid', info: { ...v.info, seatCode: e.seatCode, seatIsDefault: e.isDefault } } : v,
      );
    };

    connection.on(PaymentEvents.PaymentReceived, onReceived);
    connection.on(PaymentEvents.SeatAssigned, onSeat);
    return () => {
      connection.off(PaymentEvents.PaymentReceived, onReceived);
      connection.off(PaymentEvents.SeatAssigned, onSeat);
    };
  }, [connection, billId, markPaid]);

  const mm = String(Math.floor(secondsLeft / 60)).padStart(2, '0');
  const ss = String(secondsLeft % 60).padStart(2, '0');

  return (
    <div className="pay-backdrop" role="dialog" aria-modal="true" aria-labelledby="pay-title">
      <div className="pay-card">
        <header className="pay-head">
          <h2 id="pay-title">Thanh toán bill #{billId}</h2>
          <button className="pay-x" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>

        {view.kind === 'loading' && <p className="pay-muted">Đang tạo mã…</p>}

        {view.kind === 'error' && (
          <div className="pay-state">
            <p className="pay-error">{view.message}</p>
            <button className="pay-btn" onClick={() => createQr.mutate()}>
              Thử lại
            </button>
          </div>
        )}

        {(view.kind === 'qr' || view.kind === 'expired') && (
          <div className="pay-body">
            <div className={`pay-qr ${view.kind === 'expired' ? 'is-expired' : ''}`}>
              <img src={view.qr.qrUrl} alt={`Mã VietQR ${formatVnd(view.qr.amount)}`} width={260} height={260} />
              {view.kind === 'expired' && <span className="pay-qr-overlay">Hết hạn</span>}
            </div>

            <div className="pay-amount">{formatVnd(view.qr.amount)}</div>
            <dl className="pay-meta">
              <dt>Nội dung</dt>
              <dd className="pay-code">{view.qr.code}</dd>
              <dt>Ngân hàng</dt>
              <dd>
                {view.qr.bank} · {view.qr.accountNumber}
              </dd>
            </dl>

            {view.kind === 'qr' ? (
              <p className="pay-wait" aria-live="polite">
                <span className="pay-dot" /> Đang chờ khách chuyển khoản · giữ chỗ còn {mm}:{ss}
              </p>
            ) : (
              <button className="pay-btn" onClick={() => createQr.mutate()} disabled={createQr.isPending}>
                Tạo lại mã
              </button>
            )}

            <p className="pay-hint">Nhắc khách giữ nguyên nội dung chuyển khoản để hệ thống tự nhận.</p>

            <button className="pay-btn pay-btn-ghost" onClick={() => payCash.mutate()} disabled={payCash.isPending}>
              Khách trả tiền mặt
            </button>
          </div>
        )}

        {view.kind === 'paid' && (
          <div className="pay-state pay-success">
            <div className="pay-check" aria-hidden>
              ✓
            </div>
            <p className="pay-amount">Đã nhận {view.info.amount ? formatVnd(view.info.amount) : ''}</p>
            {view.info.seatCode && (
              <p>
                Chỗ ngồi: <strong>{view.info.seatCode}</strong>
                {view.info.seatIsDefault && <span className="pay-muted"> (đã đổi do chỗ cũ có người)</span>}
              </p>
            )}
            <button className="pay-btn" onClick={onClose}>
              Xong
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
