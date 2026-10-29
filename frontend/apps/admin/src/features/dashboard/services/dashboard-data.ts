import { mockAdminBranches, mockDashboardByBranch } from "../data/dashboard.mock";
import type { AdminBranch, DashboardSummary } from "../types";

export async function getMockAdminBranches(): Promise<AdminBranch[]> {
  return mockAdminBranches.map((branch) => ({ ...branch }));
}

export async function getMockDashboardSummary(branchId: string): Promise<DashboardSummary> {
  const summary = mockDashboardByBranch[branchId];
  if (!summary) {
    throw new Error("Không tìm thấy dữ liệu mẫu cho chi nhánh đã chọn.");
  }

  return {
    ...summary,
    alerts: summary.alerts.map((alert) => ({ ...alert })),
  };
}
