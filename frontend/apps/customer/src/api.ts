import { QueryClient } from "@tanstack/react-query";
import { AuthSession, createFeatureApi } from "@cafecr/api-client";
import { createAuth } from "@cafecr/ui";

export const session = new AuthSession({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  storage: window.sessionStorage,
  storageKey: "cafecr.customer.refreshToken",
});

export const api = createFeatureApi(session.http);

export const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false } },
});

export const { AuthProvider, useAuth } = createAuth(session, () => queryClient.clear());
