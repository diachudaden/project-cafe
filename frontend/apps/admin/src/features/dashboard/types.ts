export interface AdminBranch {
  id: string;
  name: string;
  address: string;
  hours: string;
  active: boolean;
}

export interface DashboardAlert {
  id: string;
  title: string;
  detail: string;
  tone: "warning" | "neutral";
}

export interface DashboardSummary {
  revenue: number;
  occupiedSeats: number;
  totalSeats: number;
  alerts: DashboardAlert[];
}
