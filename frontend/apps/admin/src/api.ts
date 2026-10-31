import { AuthSession, createAdminApi } from "@cafecr/api-client";

export const session = new AuthSession({
  baseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
  storage: window.sessionStorage,
  storageKey: "cafecr.admin.refreshToken",
});

export const adminApi = createAdminApi(session.http);
