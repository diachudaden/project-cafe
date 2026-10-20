import { createContext, useContext, useMemo, type ReactNode } from 'react';
import type { HubConnection } from '@microsoft/signalr';
import { createPaymentsApi, type Fetcher, type PaymentsApi } from '@chonrieng/api-client/payments';

interface PaymentsContextValue {
  api: PaymentsApi;
  connection: HubConnection | null;
}

const PaymentsContext = createContext<PaymentsContextValue | null>(null);

/**
 * Bọc layout POS:
 *   <PaymentsProvider fetcher={apiFetch} connection={posHubConnection}>…</PaymentsProvider>
 *
 * INTEGRATE: `fetcher` là hàm fetch đã gắn JWT + baseURL của api-client;
 * `connection` là HubConnection tới /hubs/pos mà POS đang dùng.
 */
export function PaymentsProvider({
  fetcher,
  connection,
  children,
}: {
  fetcher: Fetcher;
  connection: HubConnection | null;
  children: ReactNode;
}) {
  const value = useMemo(() => ({ api: createPaymentsApi(fetcher), connection }), [fetcher, connection]);
  return <PaymentsContext.Provider value={value}>{children}</PaymentsContext.Provider>;
}

export function usePayments(): PaymentsContextValue {
  const ctx = useContext(PaymentsContext);
  if (!ctx) throw new Error('usePayments phải nằm trong <PaymentsProvider>');
  return ctx;
}
