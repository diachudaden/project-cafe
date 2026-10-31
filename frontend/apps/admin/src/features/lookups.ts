import { useQuery } from "@tanstack/react-query";
import { adminApi } from "../api";

// Select options for foreign keys. The API caps pageSize at 100, which covers a single cafe chain.

export function useBranchOptions() {
  const query = useQuery({ queryKey: ["branches", "options"], queryFn: () => adminApi.branches.list(1, 100) });
  const items = query.data?.items ?? [];
  return {
    options: items.map((b) => ({ value: b.id, label: b.name })),
    nameOf: (id: string | null) => items.find((b) => b.id === id)?.name ?? id ?? "",
  };
}

export function useZoneOptions() {
  const branches = useBranchOptions();
  const query = useQuery({ queryKey: ["zones", "options"], queryFn: () => adminApi.zones.list(1, 100) });
  const items = query.data?.items ?? [];
  const label = (z: (typeof items)[number]) => `${branches.nameOf(z.branchId)} / ${z.name}`;
  return {
    options: items.map((z) => ({ value: z.id, label: label(z) })),
    nameOf: (id: string) => {
      const zone = items.find((z) => z.id === id);
      return zone ? label(zone) : id;
    },
  };
}
