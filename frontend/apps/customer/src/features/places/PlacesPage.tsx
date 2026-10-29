import { useEffect, useState } from "react";
import { Badge, PageContent, PageHeading, StatePanel } from "@chonrieng/ui";
import { getMockCustomerBranches } from "./services/branch-data";
import type { CustomerBranch } from "./types";

type PlacesState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; data: CustomerBranch[] };

export function PlacesPage() {
  const [state, setState] = useState<PlacesState>({ status: "loading" });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    getMockCustomerBranches()
      .then((data) => {
        if (active) setState({ status: "ready", data });
      })
      .catch((error: unknown) => {
        if (active) {
          setState({
            status: "error",
            message: error instanceof Error ? error.message : "Không tải được danh sách quán mẫu.",
          });
        }
      });
    return () => {
      active = false;
    };
  }, [reloadKey]);

  return (
    <PageContent className="customer-page-content">
      <PageHeading
        eyebrow="Dành cho bạn"
        title="Tìm quán"
        description="Danh sách và số chỗ trống dưới đây chỉ là thông tin minh họa."
      />
      {state.status === "loading" && <StatePanel state="loading" title="Đang tải danh sách quán mẫu..." />}
      {state.status === "error" && (
        <StatePanel
          state="error"
          title="Không tải được danh sách quán"
          description={state.message}
          onRetry={() => setReloadKey((key) => key + 1)}
        />
      )}
      {state.status === "ready" && state.data.length === 0 && (
        <StatePanel state="empty" title="Chưa có quán mẫu gần bạn" />
      )}
      {state.status === "ready" && state.data.length > 0 && (
        <div className="customer-place-list">
          {state.data.map((branch, index) => (
            <article className="cr-card customer-place-card" key={branch.id}>
              <div className="customer-place-art" aria-hidden="true">
                <span>CR</span>
                <small>{String(index + 1).padStart(2, "0")}</small>
              </div>
              <div className="customer-place-info">
                <div className="customer-place-heading">
                  <div>
                    <h2>{branch.name}</h2>
                    <p>{branch.address}</p>
                  </div>
                  <Badge tone={branch.openSeats > 0 ? "success" : "neutral"}>{branch.statusLabel}</Badge>
                </div>
                <div className="customer-place-meta">
                  <span><i aria-hidden="true">⌖</i> Khoảng {branch.distanceLabel}</span>
                  <span><i aria-hidden="true">⌂</i> {branch.openSeats} chỗ trống · mẫu</span>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </PageContent>
  );
}
