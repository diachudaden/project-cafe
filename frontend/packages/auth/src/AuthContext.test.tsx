import { StrictMode } from "react";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider, useAuth } from "./AuthContext";

const user = {
  id: "00000000-0000-0000-0000-000000000001",
  email: "member@example.test",
  phone: null,
  fullName: "Nguyen Van A",
  role: "Customer",
  branchIds: [],
};
const session = { accessToken: "memory-only-token", expiresIn: 900, user };
const fetchMock = vi.fn<typeof fetch>();

function Probe() {
  const auth = useAuth();
  return (
    <>
      <output data-testid="status">{auth.status}</output>
      <output data-testid="name">{auth.user?.fullName ?? ""}</output>
      <output data-testid="error">{auth.error ?? ""}</output>
    </>
  );
}

function SignInProbe() {
  const auth = useAuth();
  return (
    <>
      <output data-testid="status">{auth.status}</output>
      <output data-testid="name">{auth.user?.fullName ?? ""}</output>
      <output data-testid="error">{auth.error ?? ""}</output>
      <button
        type="button"
        onClick={() => void auth.signIn({ identifier: "member@example.test", password: "example" })}
      >
        Sign in
      </button>
      <button type="button" onClick={() => void auth.signOut()}>
        Sign out
      </button>
    </>
  );
}

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("AuthProvider", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", fetchMock);
    fetchMock.mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("restores the cookie session without rotating the refresh cookie twice under StrictMode", async () => {
    fetchMock.mockImplementation(async (input) => (
      String(input).endsWith("/auth/refresh") ? jsonResponse(session) : jsonResponse(user)
    ));
    render(
      <StrictMode>
        <AuthProvider apiBaseUrl="http://api.test" client="customer">
          <Probe />
        </AuthProvider>
      </StrictMode>,
    );

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("name")).toHaveTextContent("Nguyen Van A");
    expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith("/auth/refresh"))).toHaveLength(1);
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/auth/me"))).toBe(true);
  });

  it("does not allow a refreshed account with another app role", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({
      ...session,
      user: { ...user, role: "Staff" },
    }));
    render(
      <AuthProvider apiBaseUrl="http://api.test" client="customer">
        <Probe />
      </AuthProvider>,
    );

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(screen.getByTestId("error")).toHaveTextContent("Tài khoản này không dùng được trên ứng dụng này.");
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("authenticates through the configured client and verifies /me", async () => {
    const signedInUser = { ...user, role: "Customer" };
    fetchMock.mockImplementation(async (input) => {
      if (String(input).endsWith("/auth/login")) {
        return jsonResponse({ ...session, user: signedInUser });
      }

      if (String(input).endsWith("/auth/me")) {
        return jsonResponse(signedInUser);
      }

      if (String(input).endsWith("/auth/logout")) {
        return new Response(null, { status: 204 });
      }

      return jsonResponse({
        type: "auth.session_revoked",
        detail: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
      }, 401);
    });
    const userEventInstance = userEvent.setup();
    render(
      <AuthProvider apiBaseUrl="http://api.test" client="customer">
        <SignInProbe />
      </AuthProvider>,
    );
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));

    await userEventInstance.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("name")).toHaveTextContent("Nguyen Van A");
    const loginCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/auth/login"));
    expect(JSON.parse(String(loginCall?.[1]?.body))).toMatchObject({
      identifier: "member@example.test",
      client: "customer",
    });

    await userEventInstance.click(screen.getByRole("button", { name: "Sign out" }));
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(screen.getByTestId("name")).toBeEmptyDOMElement();
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/auth/logout"))).toBe(true);
  });
});
