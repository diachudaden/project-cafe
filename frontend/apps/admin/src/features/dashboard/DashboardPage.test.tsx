import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "@chonrieng/auth";
import { DashboardPage } from "./DashboardPage";

const branchIds = [
  "11111111-1111-7111-8111-111111111111",
  "22222222-2222-7222-8222-222222222222",
];

function renderDashboard(role: "Admin" | "BranchManager", assignedBranchIds = branchIds) {
  const user = {
    id: "00000000-0000-0000-0000-000000000001",
    email: "manager@example.test",
    phone: null,
    fullName: "Nguyen Van A",
    role,
    branchIds: assignedBranchIds,
  };
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const body = String(input).endsWith("/auth/refresh")
      ? { accessToken: "test-token", expiresIn: 900, user }
      : user;
    return new Response(JSON.stringify(body), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  }));
  return render(
    <AuthProvider apiBaseUrl="http://api.test" client="admin">
      <DashboardPage />
    </AuthProvider>,
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("DashboardPage", () => {
  it("filters sample dashboard values by selected branch", async () => {
    const user = userEvent.setup();
    renderDashboard("Admin");

    expect(await screen.findByText("17.490.000 đ")).toBeInTheDocument();

    await user.selectOptions(screen.getByRole("combobox", { name: "Lọc theo chi nhánh" }), "11111111-1111-7111-8111-111111111111");

    await waitFor(() => expect(screen.getByText("8.450.000 đ")).toBeInTheDocument());
    expect(screen.getByText("Đang xem:")).toHaveTextContent("Chốn Riêng - Nguyễn Trãi");
  });

  it("limits a branch manager to their assigned sample branches", async () => {
    renderDashboard("BranchManager", [branchIds[0]]);

    expect(await screen.findByText("8.450.000 đ")).toBeInTheDocument();
    const options = screen.getAllByRole("option");
    expect(options.map((option) => (option as HTMLOptionElement).value)).toEqual([branchIds[0]]);
    expect(screen.queryByRole("option", { name: "Tất cả chi nhánh" })).not.toBeInTheDocument();
    expect(screen.queryByRole("option", { name: "Chốn Riêng - Thảo Điền" })).not.toBeInTheDocument();
  });
});
