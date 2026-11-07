import type { ReactNode } from "react";
import { Card, Empty } from "antd";
import { useBranch } from "../branch/BranchContext";

/** Screens that work on one branch ask the Admin to pick one instead of "Tất cả". */
export function RequireBranch({ title, children }: { title: string; children: (branchId: string) => ReactNode }) {
  const { branchId } = useBranch();
  if (!branchId) {
    return (
      <Card title={title}>
        <Empty description="Chọn một chi nhánh ở thanh trên cùng để xem màn hình này." />
      </Card>
    );
  }
  return <>{children(branchId)}</>;
}
