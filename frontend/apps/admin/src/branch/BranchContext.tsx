import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import type { PublicBranch } from "@cafecr/api-client";
import { hasRole } from "@cafecr/ui";
import { api, useAuth } from "../api";

interface BranchContextValue {
  /** null = whole chain (Admin only). */
  branchId: string | null;
  setBranchId(id: string | null): void;
  branches: PublicBranch[];
  branchName: (id: string | null | undefined) => string;
  canSeeAll: boolean;
}

const BranchContext = createContext<BranchContextValue | null>(null);
const STORAGE_KEY = "cafecr.admin.branch";

/** Top-bar branch switcher state: every screen filters by the selected branch (reference: "Chuyển chi nhánh nhanh"). */
export function BranchProvider({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const user = state.user;
  const canSeeAll = hasRole(user, "Admin");
  const query = useQuery({ queryKey: ["branches", "public"], queryFn: () => api.catalog.branches(), enabled: !!user });
  const branches = useMemo(
    () => (query.data ?? []).filter((b) => canSeeAll || user?.branchIds.includes(b.id)),
    [query.data, canSeeAll, user],
  );
  const [branchId, setBranchIdState] = useState<string | null>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) || null;
    } catch {
      return null;
    }
  });

  // Managers always work in one of their branches; Admin may pick "Tất cả".
  useEffect(() => {
    if (!branches.length) return;
    if (branchId && branches.some((b) => b.id === branchId)) return;
    setBranchIdState(canSeeAll ? null : branches[0].id);
  }, [branches, branchId, canSeeAll]);

  const value = useMemo<BranchContextValue>(() => ({
    branchId,
    setBranchId: (id) => {
      setBranchIdState(id);
      try {
        if (id) localStorage.setItem(STORAGE_KEY, id);
        else localStorage.removeItem(STORAGE_KEY);
      } catch {
        // Storage may be blocked; the selection still works for this page view.
      }
    },
    branches,
    branchName: (id) => branches.find((b) => b.id === id)?.name ?? (id ? "Chi nhánh khác" : "Tất cả chi nhánh"),
    canSeeAll,
  }), [branchId, branches, canSeeAll]);

  return <BranchContext.Provider value={value}>{children}</BranchContext.Provider>;
}

export function useBranch() {
  const value = useContext(BranchContext);
  if (!value) throw new Error("useBranch must be used inside BranchProvider");
  return value;
}
