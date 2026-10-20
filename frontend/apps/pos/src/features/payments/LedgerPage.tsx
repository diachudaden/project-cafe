import { useState, type FormEvent } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  LedgerCategory,
  LedgerDirection,
  PaymentApiError,
  PaymentMethod,
  SummaryPeriod,
  formatVnd,
  ledgerCategoryLabels,
  paymentKeys,
  paymentMethodLabels,
  type LedgerEntryDto,
  type LedgerQuery,
} from '@chonrieng/api-client/payments';
import { usePayments } from './PaymentsContext';
import './payments.css';

// Trang này dành cho Quản lý. Đặt trong apps/admin nếu tách app (chỉ cần đổi đường import).

const periodLabels: Record<SummaryPeriod, string> = {
  [SummaryPeriod.Day]: 'Ngày',
  [SummaryPeriod.Week]: 'Tuần',
  [SummaryPeriod.Month]: 'Tháng',
  [SummaryPeriod.Quarter]: 'Quý',
  [SummaryPeriod.Year]: 'Năm',
};

const expenseCategories = [
  LedgerCategory.Ingredients,
  LedgerCategory.Payroll,
  LedgerCategory.Utilities,
  LedgerCategory.Rent,
  LedgerCategory.Refund,
  LedgerCategory.Other,
] as const;

/** yyyy-MM-dd theo giờ Việt Nam. */
function vnDate(d: Date): string {
  return new Date(d.getTime() + 7 * 3600_000).toISOString().slice(0, 10);
}

function startOfYear(): string {
  return `${vnDate(new Date()).slice(0, 4)}-01-01`;
}

export function LedgerPage({ branchId }: { branchId?: number }) {
  const [period, setPeriod] = useState<SummaryPeriod>(SummaryPeriod.Month);
  const [from, setFrom] = useState(startOfYear());
  const [to, setTo] = useState(vnDate(new Date()));
  const [direction, setDirection] = useState<LedgerDirection | undefined>();
  const [page, setPage] = useState(1);

  return (
    <div className="ledger">
      <header className="ledger-head">
        <h1>Thu chi</h1>
        <div className="ledger-filters">
          <label>
            Từ <input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} />
          </label>
          <label>
            Đến <input type="date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} />
          </label>
          <div className="seg" role="radiogroup" aria-label="Gom theo">
            {Object.values(SummaryPeriod).map((p) => (
              <button key={p} role="radio" aria-checked={p === period} className={p === period ? 'on' : ''} onClick={() => setPeriod(p)}>
                {periodLabels[p]}
              </button>
            ))}
          </div>
        </div>
      </header>

      <Summary branchId={branchId} period={period} from={from} to={to} />

      <div className="ledger-split">
        <History
          query={{ branchId, from, to, direction, page, pageSize: 20 }}
          onDirection={(d) => { setDirection(d); setPage(1); }}
          onPage={setPage}
        />
        {branchId !== undefined && <ExpenseForm branchId={branchId} />}
      </div>
    </div>
  );
}

function Summary({ branchId, period, from, to }: { branchId?: number; period: SummaryPeriod; from: string; to: string }) {
  const { api } = usePayments();
  const q = { branchId, period, from, to };
  const { data, isLoading } = useQuery({
    queryKey: paymentKeys.ledgerSummary(q),
    queryFn: () => api.ledgerSummary(q),
    placeholderData: keepPreviousData,
  });

  if (isLoading || !data) return <p className="pay-muted">Đang tải báo cáo…</p>;

  return (
    <section aria-label="Tổng hợp">
      <div className="ledger-stats">
        <Stat label="Tổng thu" value={data.totalIn} tone="in" />
        <Stat label="Tổng chi" value={data.totalOut} tone="out" />
        <Stat label="Lãi / lỗ" value={data.net} tone={data.net >= 0 ? 'in' : 'out'} />
      </div>

      <table className="ledger-table">
        <thead>
          <tr>
            <th>Kỳ</th>
            <th className="num">Thu</th>
            <th className="num">Chi</th>
            <th className="num">Lãi / lỗ</th>
          </tr>
        </thead>
        <tbody>
          {data.periods.map((p) => (
            <tr key={p.periodStart} title={p.byCategory.map((c) => `${ledgerCategoryLabels[c.category]}: ${formatVnd(c.total)}`).join('\n')}>
              <td>{p.label}</td>
              <td className="num in">{formatVnd(p.totalIn)}</td>
              <td className="num out">{formatVnd(p.totalOut)}</td>
              <td className={`num ${p.net >= 0 ? 'in' : 'out'}`}>{formatVnd(p.net)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone: 'in' | 'out' }) {
  return (
    <div className={`stat ${tone}`}>
      <span>{label}</span>
      <strong>{formatVnd(value)}</strong>
    </div>
  );
}

function History({
  query,
  onDirection,
  onPage,
}: {
  query: LedgerQuery;
  onDirection: (d: LedgerDirection | undefined) => void;
  onPage: (p: number) => void;
}) {
  const { api } = usePayments();
  const { data } = useQuery({
    queryKey: paymentKeys.ledger(query),
    queryFn: () => api.listLedger(query),
    placeholderData: keepPreviousData,
  });

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;

  return (
    <section className="ledger-history" aria-label="Lịch sử thu chi">
      <div className="ledger-history-head">
        <h2>Lịch sử</h2>
        <div className="seg">
          <button className={query.direction === undefined ? 'on' : ''} onClick={() => onDirection(undefined)}>Tất cả</button>
          <button className={query.direction === LedgerDirection.In ? 'on' : ''} onClick={() => onDirection(LedgerDirection.In)}>Thu</button>
          <button className={query.direction === LedgerDirection.Out ? 'on' : ''} onClick={() => onDirection(LedgerDirection.Out)}>Chi</button>
        </div>
      </div>

      <table className="ledger-table">
        <thead>
          <tr>
            <th>Thời gian</th>
            <th>Danh mục</th>
            <th>Cách</th>
            <th className="num">Số tiền</th>
            <th aria-label="Thao tác" />
          </tr>
        </thead>
        <tbody>
          {data?.items.map((e) => <HistoryRow key={e.id} entry={e} />)}
        </tbody>
      </table>

      {pages > 1 && (
        <nav className="ledger-pager">
          <button disabled={(query.page ?? 1) <= 1} onClick={() => onPage((query.page ?? 1) - 1)}>‹</button>
          <span>{query.page} / {pages}</span>
          <button disabled={(query.page ?? 1) >= pages} onClick={() => onPage((query.page ?? 1) + 1)}>›</button>
        </nav>
      )}
    </section>
  );
}

function HistoryRow({ entry: e }: { entry: LedgerEntryDto }) {
  const { api } = usePayments();
  const qc = useQueryClient();
  const reverse = useMutation({
    mutationFn: (reason: string) => api.reverseLedger(e.id, reason),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['ledger'] }),
    onError: (err) => window.alert(err instanceof PaymentApiError ? err.message : 'Lỗi kết nối'),
  });

  const sign = e.direction === LedgerDirection.In ? '+' : '−';
  const tone = e.direction === LedgerDirection.In ? 'in' : 'out';

  return (
    <tr className={e.reversalOfId ? 'is-reversal' : ''}>
      <td>{new Date(e.occurredAtUtc).toLocaleString('vi-VN')}</td>
      <td>
        {ledgerCategoryLabels[e.category]}
        {e.billId && <span className="pay-muted"> · bill #{e.billId}</span>}
        {e.reversalOfId && <span className="pay-tag">đảo #{e.reversalOfId}</span>}
        {e.note && <div className="pay-muted">{e.note}</div>}
      </td>
      <td>{paymentMethodLabels[e.method]}</td>
      <td className={`num ${tone}`}>
        {e.reversalOfId ? '(' : sign}
        {formatVnd(e.amount)}
        {e.reversalOfId ? ')' : ''}
      </td>
      <td>
        {!e.reversalOfId && (
          <button
            className="pay-link"
            disabled={reverse.isPending}
            onClick={() => {
              const reason = window.prompt('Lý do đảo bút toán này?');
              if (reason?.trim()) reverse.mutate(reason.trim());
            }}
          >
            Đảo
          </button>
        )}
      </td>
    </tr>
  );
}

function ExpenseForm({ branchId }: { branchId: number }) {
  const { api } = usePayments();
  const qc = useQueryClient();
  const [amount, setAmount] = useState('');
  const [category, setCategory] = useState<LedgerCategory>(LedgerCategory.Ingredients);
  const [method, setMethod] = useState<PaymentMethod>(PaymentMethod.Cash);
  const [note, setNote] = useState('');
  const [error, setError] = useState<string | null>(null);

  const add = useMutation({
    mutationFn: () => api.addExpense({ branchId, amount: Number(amount), category, method, note: note || undefined }),
    onSuccess: () => {
      setAmount('');
      setNote('');
      setError(null);
      void qc.invalidateQueries({ queryKey: ['ledger'] });
    },
    onError: (e) => setError(e instanceof PaymentApiError ? e.message : 'Lỗi kết nối'),
  });

  const submit = (ev: FormEvent) => {
    ev.preventDefault();
    if (Number(amount) > 0) add.mutate();
  };

  return (
    <form className="ledger-form" onSubmit={submit}>
      <h2>Ghi khoản chi</h2>
      <label>
        Số tiền
        <input inputMode="numeric" value={amount} onChange={(e) => setAmount(e.target.value.replace(/\D/g, ''))} placeholder="0" required />
      </label>
      <label>
        Danh mục
        <select value={category} onChange={(e) => setCategory(Number(e.target.value) as LedgerCategory)}>
          {expenseCategories.map((c) => (
            <option key={c} value={c}>{ledgerCategoryLabels[c]}</option>
          ))}
        </select>
      </label>
      <label>
        Cách trả
        <select value={method} onChange={(e) => setMethod(Number(e.target.value) as PaymentMethod)}>
          <option value={PaymentMethod.Cash}>{paymentMethodLabels[PaymentMethod.Cash]}</option>
          <option value={PaymentMethod.ManualTransfer}>{paymentMethodLabels[PaymentMethod.ManualTransfer]}</option>
        </select>
      </label>
      <label>
        Ghi chú
        <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: 5kg cà phê hạt" />
      </label>
      {Number(amount) > 0 && <p className="pay-muted">{formatVnd(Number(amount))}</p>}
      {error && <p className="pay-error">{error}</p>}
      <button className="pay-btn" disabled={add.isPending || !(Number(amount) > 0)}>Ghi sổ</button>
    </form>
  );
}
