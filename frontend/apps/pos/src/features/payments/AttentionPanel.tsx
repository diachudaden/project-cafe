import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  PaymentApiError,
  formatVnd,
  matchStatusLabels,
  paymentKeys,
  type PaymentTransactionDto,
} from '@chonrieng/api-client/payments';
import { usePayments } from './PaymentsContext';
import './payments.css';

/**
 * Danh sách tiền vào chưa khớp được bill (khách xoá mã, chuyển thiếu, chuyển 2 lần).
 * Trưởng ca gán tay cho bill hoặc đóng lại kèm lý do (ví dụ đã hoàn tiền).
 */
export function AttentionPanel() {
  const { api } = usePayments();
  const { data = [], isLoading } = useQuery({
    queryKey: paymentKeys.attention(),
    queryFn: api.listNeedingAttention,
    refetchInterval: 60_000,
  });

  if (isLoading) return <p className="pay-muted">Đang tải…</p>;
  if (data.length === 0) return <p className="pay-muted">Không có giao dịch nào cần xử lý.</p>;

  return (
    <ul className="pay-list">
      {data.map((tx) => (
        <AttentionRow key={tx.id} tx={tx} />
      ))}
    </ul>
  );
}

function AttentionRow({ tx }: { tx: PaymentTransactionDto }) {
  const { api } = usePayments();
  const qc = useQueryClient();
  const [billId, setBillId] = useState(tx.billId ? String(tx.billId) : '');
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  const done = () => {
    setError(null);
    void qc.invalidateQueries({ queryKey: paymentKeys.attention() });
    void qc.invalidateQueries({ queryKey: ['ledger'] });
  };
  const fail = (e: unknown) => setError(e instanceof PaymentApiError ? e.message : 'Lỗi kết nối');

  const assign = useMutation({
    mutationFn: () => api.assignTransaction(tx.id, Number(billId), note || undefined),
    onSuccess: done,
    onError: fail,
  });
  const dismiss = useMutation({
    mutationFn: () => api.dismissTransaction(tx.id, note),
    onSuccess: done,
    onError: fail,
  });

  return (
    <li className="pay-row">
      <div className="pay-row-main">
        <strong>{formatVnd(tx.amount)}</strong>
        <span className="pay-tag">{matchStatusLabels[tx.matchStatus]}</span>
        <span className="pay-muted">{new Date(tx.transactionAtUtc).toLocaleString('vi-VN')}</span>
      </div>
      <p className="pay-row-content" title="Nội dung chuyển khoản gốc">
        “{tx.content}”
      </p>
      {tx.note && <p className="pay-muted">{tx.note}</p>}

      <div className="pay-row-actions">
        <input
          inputMode="numeric"
          placeholder="Bill #"
          value={billId}
          onChange={(e) => setBillId(e.target.value.replace(/\D/g, ''))}
          aria-label="Số bill"
        />
        <input placeholder="Ghi chú / lý do" value={note} onChange={(e) => setNote(e.target.value)} aria-label="Ghi chú" />
        <button className="pay-btn" disabled={!billId || assign.isPending} onClick={() => assign.mutate()}>
          Gán vào bill
        </button>
        <button className="pay-btn pay-btn-ghost" disabled={!note.trim() || dismiss.isPending} onClick={() => dismiss.mutate()}>
          Đã xử lý ngoài
        </button>
      </div>
      {error && <p className="pay-error">{error}</p>}
    </li>
  );
}
