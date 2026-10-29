import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "./AuthContext";
import { RequireAuth } from "./RequireAuth";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("RequireAuth", () => {
  it("redirects to login when refresh cannot restore a session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      type: "auth.session_revoked",
      detail: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
    }), {
      status: 401,
      headers: { "Content-Type": "application/problem+json" },
    })));

    render(
      <AuthProvider apiBaseUrl="http://api.test" client="admin">
        <MemoryRouter initialEntries={["/dashboard"]}>
          <Routes>
            <Route path="/login" element={<p>Trang đăng nhập</p>} />
            <Route element={<RequireAuth />}>
              <Route path="/dashboard" element={<p>Nội dung được bảo vệ</p>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(await screen.findByText("Trang đăng nhập")).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText("Nội dung được bảo vệ")).not.toBeInTheDocument());
  });
});
