import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthApiError, getCurrentUser, login, logout, refresh } from "./auth-api";
import { getAuthErrorMessage } from "./error-message";

const session = {
  accessToken: "access-token",
  expiresIn: 900,
  user: {
    id: "00000000-0000-0000-0000-000000000001",
    email: "member@example.test",
    phone: null,
    fullName: "Nguyen Van A",
    role: "Customer",
    branchIds: [],
  },
};

const fetchMock = vi.fn<typeof fetch>();

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("auth API client", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("posts real credentials, client discriminator, and cookie credentials", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(session));

    await expect(login("http://api.test", "customer", {
      identifier: "0901234567",
      password: "not-a-real-password",
    })).resolves.toEqual(session);

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("http://api.test/api/v1/auth/login");
    expect(init?.credentials).toBe("include");
    expect(JSON.parse(String(init?.body))).toEqual({
      identifier: "0901234567",
      password: "not-a-real-password",
      client: "customer",
    });
  });

  it("restores with refresh and reads current user with a bearer token", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(session))
      .mockResolvedValueOnce(jsonResponse(session.user));

    await expect(refresh("http://api.test")).resolves.toEqual(session);
    await expect(getCurrentUser("http://api.test", session.accessToken)).resolves.toEqual(session.user);

    expect(fetchMock.mock.calls[0][1]?.credentials).toBe("include");
    expect(fetchMock.mock.calls[1][1]?.headers).toEqual({ Authorization: "Bearer access-token" });
    expect(fetchMock.mock.calls[1][1]?.credentials).toBe("include");
  });

  it("calls logout with credentials and accepts the 204 response", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(logout("http://api.test")).resolves.toBeUndefined();
    expect(fetchMock.mock.calls[0][0]).toBe("http://api.test/api/v1/auth/logout");
    expect(fetchMock.mock.calls[0][1]?.credentials).toBe("include");
  });

  it("preserves documented problem type and maps credential errors to approved copy", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({
      type: "auth.invalid_credentials",
      detail: "Email/số điện thoại hoặc mật khẩu không đúng.",
    }, 401));

    const result = login("http://api.test", "admin", { identifier: "x", password: "x" });
    await expect(result).rejects.toMatchObject({
      status: 401,
      type: "auth.invalid_credentials",
    });
    await expect(result).rejects.toBeInstanceOf(AuthApiError);
    await result.catch((error: unknown) => {
      expect(getAuthErrorMessage(error)).toBe("Email/số điện thoại hoặc mật khẩu không đúng.");
    });
  });

  it("extracts field validation errors when ProblemDetails includes them", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({
      type: "validation",
      detail: "Kiểm tra lại thông tin đăng nhập.",
      errors: { Identifier: ["Nhập email hoặc số điện thoại."] },
    }, 400));

    await expect(login("http://api.test", "admin", { identifier: "", password: "" }))
      .rejects.toMatchObject({
        status: 400,
        type: "validation",
        fieldErrors: { Identifier: ["Nhập email hoặc số điện thoại."] },
      });
  });

  it("surfaces API-unreachable errors instead of a success-shaped fallback", async () => {
    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch"));

    const result = login("http://api.test", "pos", { identifier: "x", password: "x" });
    await expect(result).rejects.toMatchObject({ status: 0, type: "auth.network_error" });
    await expect(result).rejects.toThrow("Không kết nối được máy chủ. Thử lại sau.");
  });
});
