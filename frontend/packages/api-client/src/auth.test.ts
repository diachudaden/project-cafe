import { describe, expect, it, vi } from "vitest";
import { AuthSession, type AuthTokens } from "./auth";
import { ApiError } from "./http";

const user = { id: "u1", email: "admin@cafecr.invalid", fullName: "Admin", phoneNumber: null, roles: ["Admin" as const] };

function tokens(n: number): AuthTokens {
  return {
    accessToken: `access-${n}`,
    accessTokenExpiresAt: "2026-01-01T00:15:00Z",
    refreshToken: `refresh-${n}`,
    refreshTokenExpiresAt: "2026-01-08T00:00:00Z",
    user,
  };
}

function json(status: number, body?: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function memoryStorage(initial: Record<string, string> = {}) {
  const data = new Map(Object.entries(initial));
  return {
    getItem: (key: string) => data.get(key) ?? null,
    setItem: (key: string, value: string) => void data.set(key, value),
    removeItem: (key: string) => void data.delete(key),
  };
}

describe("AuthSession", () => {
  it("stores only the refresh token and sends the access token as bearer", async () => {
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValueOnce(json(200, tokens(1)))
      .mockResolvedValueOnce(json(200, { page: 1, pageSize: 20, items: [] }));
    const storage = memoryStorage();
    const session = new AuthSession({ storage, storageKey: "rt", fetch });

    await session.login({ email: user.email, password: "x" });
    await session.http.request("GET", "/api/admin/branches");

    expect(storage.getItem("rt")).toBe("refresh-1");
    const headers = fetch.mock.calls[1][1]?.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer access-1");
  });

  it("refreshes once on 401 and retries the request with the new token", async () => {
    const fetch = vi
      .fn<typeof globalThis.fetch>()
      .mockResolvedValueOnce(json(200, tokens(1)))
      .mockResolvedValueOnce(json(401))
      .mockResolvedValueOnce(json(200, tokens(2)))
      .mockResolvedValueOnce(json(200, { ok: true }));
    const storage = memoryStorage();
    const session = new AuthSession({ storage, storageKey: "rt", fetch });
    await session.login({ email: user.email, password: "x" });

    await expect(session.http.request("GET", "/api/admin/branches")).resolves.toEqual({ ok: true });

    expect(JSON.parse(fetch.mock.calls[2][1]?.body as string)).toEqual({ refreshToken: "refresh-1" });
    expect((fetch.mock.calls[3][1]?.headers as Record<string, string>).Authorization).toBe("Bearer access-2");
    expect(storage.getItem("rt")).toBe("refresh-2");
  });

  it("shares a single refresh call between concurrent callers", async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(json(200, tokens(2)));
    const session = new AuthSession({ storage: memoryStorage({ rt: "refresh-1" }), storageKey: "rt", fetch });

    await Promise.all([session.refresh(), session.refresh()]);

    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("signs out when the refresh token is rejected", async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(json(401));
    const storage = memoryStorage({ rt: "stale" });
    const session = new AuthSession({ storage, storageKey: "rt", fetch });

    await expect(session.refresh()).resolves.toBeNull();
    expect(storage.getItem("rt")).toBeNull();
  });

  it("surfaces validation errors from problem details", async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(
      json(400, { title: "One or more validation errors occurred.", errors: { Name: ["Tên là bắt buộc."] } }),
    );
    const session = new AuthSession({ storage: memoryStorage(), storageKey: "rt", fetch });

    const error = await session.http.request("POST", "/api/admin/branches", {}).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe("Tên là bắt buộc.");
    expect((error as ApiError).fieldErrors).toEqual({ Name: ["Tên là bắt buộc."] });
  });
});
