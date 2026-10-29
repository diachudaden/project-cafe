import { useEffect, useState } from "react";
import { Badge, PageContent, PageHeading, StatePanel } from "@chonrieng/ui";
import { getMockAdminBranches } from "../dashboard/services/dashboard-data";
import type { AdminBranch } from "../dashboard/types";

type BranchesState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; data: AdminBranch[] };

export function BranchesPage() {
  const [state, setState] = useState<BranchesState>({ status: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    getMockAdminBranches()
      .then((data) => {
        if (active) setState({ status: "ready", data });
      })
      .catch((error: unknown) => {
        if (active) {
          setState({
            status: "error",
            message: error instanceof Error ? error.message : "Không tải được danh sách chi nhánh mẫu.",
          });
        }
      });
    return () => {
      active = false;
    };
  }, [reloadKey]);

  return (
    <PageContent>
      <PageHeading
        eyebrow="Mạng lưới"
        title="Chi nhánh"
        description="Danh sách chỉ đọc từ dữ liệu minh họa."
      />
      {state.status === "loading" && <StatePanel state="loading" title="Đang tải chi nhánh mẫu..." />}
      {state.status === "error" && (
        <StatePanel
          state="error"
          title="Không tải được danh sách"
          description={state.message}
          onRetry={() => setReloadKey((key) => key + 1)}
        />
      )}
      {state.status === "ready" && state.data.length === 0 && (
        <StatePanel state="empty" title="Chưa có chi nhánh mẫu" />
      )}
      {state.status === "ready" && state.data.length > 0 && (
        <div className="admin-branch-grid">
          {state.data.map((branch) => (
            <article className="cr-card admin-branch-card" key={branch.id}>
              <div className="cr-card__heading">
                <h2>{branch.name}</h2>
                <Badge tone={branch.active ? "success" : "neutral"}>
                  {branch.active ? "Đang hoạt động" : "Tạm ngưng"}
                </Badge>
              </div>
              <div className="cr-card__body">
                <p className="admin-branch-address">{branch.address}</p>
                <p className="admin-muted">Giờ mở cửa: {branch.hours}</p>
              </div>
            </article>
          ))}
        </div>
      )}
    </PageContent>
  );
}
