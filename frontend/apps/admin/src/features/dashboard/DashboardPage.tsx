import { useEffect, useState } from "react";
import { Badge, FieldLabel, MetricCard, PageContent, PageHeading, StatePanel } from "@chonrieng/ui";
import { useAuth } from "@chonrieng/auth";
import { getMockAdminBranches, getMockDashboardSummary } from "./services/dashboard-data";
import type { AdminBranch, DashboardSummary } from "./types";

type LoadState<T> =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; data: T };

const initialState: LoadState<DashboardSummary> = { status: "loading" };

export function DashboardPage() {
  const { user } = useAuth();
  const [branches, setBranches] = useState<LoadState<AdminBranch[]>>({ status: "loading" });
  const [summary, setSummary] = useState<LoadState<DashboardSummary>>(initialState);
  const [selectedBranchId, setSelectedBranchId] = useState(
    user?.role === "Admin" ? "all" : user?.branchIds[0] ?? "",
  );
  const [reloadKey, setReloadKey] = useState(0);
  const allowedBranchIds = new Set(user?.branchIds.map((id) => id.toLowerCase()) ?? []);
  const visibleBranches = branches.status === "ready"
    ? branches.data.filter((branch) => user?.role === "Admin" || allowedBranchIds.has(branch.id.toLowerCase()))
    : [];
  const visibleBranchIds = visibleBranches.map((branch) => branch.id).join(",");

  useEffect(() => {
    let active = true;
    getMockAdminBranches()
      .then((data) => {
        if (active) setBranches({ status: "ready", data });
      })
      .catch((error: unknown) => {
        if (active) {
          setBranches({
            status: "error",
            message: error instanceof Error ? error.message : "Không tải được danh sách chi nhánh mẫu.",
          });
        }
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    if (!user || branches.status !== "ready") {
      return;
    }

    if (user.role === "Admin") {
      if (!selectedBranchId) {
        setSelectedBranchId("all");
      }
      return;
    }

    if (!visibleBranches.some((branch) => branch.id === selectedBranchId)) {
      setSelectedBranchId(visibleBranches[0]?.id ?? "");
    }
  }, [branches, selectedBranchId, user, visibleBranchIds]);

  useEffect(() => {
    let active = true;
    setSummary({ status: "loading" });

    if (!user || branches.status !== "ready") {
      return () => {
        active = false;
      };
    }

    if (
      !selectedBranchId ||
      (user.role !== "Admin" && !allowedBranchIds.has(selectedBranchId.toLowerCase()))
    ) {
      setSummary({
        status: "ready",
        data: { revenue: 0, occupiedSeats: 0, totalSeats: 0, alerts: [] },
      });
      return () => {
        active = false;
      };
    }

    getMockDashboardSummary(selectedBranchId)
      .then((data) => {
        if (active) setSummary({ status: "ready", data });
      })
      .catch((error: unknown) => {
        if (active) {
          setSummary({
            status: "error",
            message: error instanceof Error ? error.message : "Không tải được tổng quan mẫu.",
          });
        }
      });

    return () => {
      active = false;
    };
  }, [branches.status, selectedBranchId, reloadKey, user, visibleBranchIds]);

  const reload = () => setReloadKey((key) => key + 1);
  const selectedBranchName =
    selectedBranchId === "all"
      ? "Tất cả chi nhánh"
      : branches.status === "ready"
        ? branches.data.find((branch) => branch.id === selectedBranchId)?.name
        : "Chi nhánh";

  return (
    <PageContent>
      <PageHeading
        eyebrow="Tổng quan vận hành"
        title="Hoạt động"
        description="Số liệu trong ngày được minh họa bằng dữ liệu mẫu."
        action={
          <div className="admin-branch-filter">
            <FieldLabel htmlFor="branch-filter">Chi nhánh</FieldLabel>
            <select
              className="cr-select"
              id="branch-filter"
              aria-label="Lọc theo chi nhánh"
              value={selectedBranchId}
              onChange={(event) => setSelectedBranchId(event.target.value)}
              disabled={branches.status !== "ready"}
            >
              {user?.role === "Admin" && <option value="all">Tất cả chi nhánh</option>}
              {visibleBranches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
          </div>
        }
      />

      <div className="admin-current-filter" aria-live="polite">
        Đang xem: <strong>{selectedBranchName}</strong>
      </div>

      {summary.status === "loading" && (
        <StatePanel state="loading" title="Đang tải tổng quan mẫu..." />
      )}
      {summary.status === "error" && (
        <StatePanel state="error" title="Không tải được tổng quan" description={summary.message} onRetry={reload} />
      )}
      {summary.status === "ready" && (
        <>
          {summary.data.totalSeats === 0 ? (
            <StatePanel state="empty" title="Chưa có dữ liệu chỗ ngồi mẫu" />
          ) : (
            <section className="cr-metric-grid" aria-label="Chỉ số hoạt động mẫu">
              <MetricCard
                label="Doanh thu hôm nay"
                value={`${summary.data.revenue.toLocaleString("vi-VN")} đ`}
                detail="Số liệu minh họa"
                tone="positive"
              />
              <MetricCard
                label="Tỷ lệ sử dụng chỗ"
                value={`${Math.round((summary.data.occupiedSeats / summary.data.totalSeats) * 100)}%`}
                detail={`${summary.data.occupiedSeats}/${summary.data.totalSeats} chỗ mẫu`}
              />
              <MetricCard
                label="Cảnh báo cần chú ý"
                value={String(summary.data.alerts.length)}
                detail="Trong phạm vi đang chọn"
                tone={summary.data.alerts.length > 0 ? "warning" : "default"}
              />
            </section>
          )}

          <section className="cr-card admin-alerts">
            <div className="cr-card__heading">
              <h2>Cảnh báo</h2>
              <Badge tone={summary.data.alerts.length > 0 ? "warning" : "success"}>
                {summary.data.alerts.length > 0 ? `${summary.data.alerts.length} mục` : "Không có"}
              </Badge>
            </div>
            {summary.data.alerts.length === 0 ? (
              <div className="cr-card__body">
                <p className="admin-muted">Hiện chưa có cảnh báo trong dữ liệu mẫu.</p>
              </div>
            ) : (
              <ul className="admin-alert-list">
                {summary.data.alerts.map((alert) => (
                  <li className="admin-alert" key={alert.id}>
                    <span className={`admin-alert__dot admin-alert__dot--${alert.tone}`} aria-hidden="true" />
                    <div>
                      <strong>{alert.title}</strong>
                      <p>{alert.detail}</p>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </PageContent>
  );
}
