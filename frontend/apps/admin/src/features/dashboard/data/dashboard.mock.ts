import type { AdminBranch, DashboardSummary } from "../types";

export const mockAdminBranches: AdminBranch[] = [
  {
    id: "11111111-1111-7111-8111-111111111111",
    name: "Chốn Riêng - Nguyễn Trãi",
    address: "120 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
    hours: "08:00 - 23:00",
    active: true,
  },
  {
    id: "22222222-2222-7222-8222-222222222222",
    name: "Chốn Riêng - Thảo Điền",
    address: "18 Xuân Thủy, TP. Thủ Đức, TP. Hồ Chí Minh",
    hours: "09:00 - 22:00",
    active: true,
  },
  {
    id: "33333333-3333-7333-8333-333333333333",
    name: "Chốn Riêng - Hải Châu",
    address: "42 Bạch Đằng, Hải Châu, Đà Nẵng",
    hours: "08:00 - 22:00",
    active: false,
  },
];

export const mockDashboardByBranch: Record<string, DashboardSummary> = {
  "11111111-1111-7111-8111-111111111111": {
    revenue: 8_450_000,
    occupiedSeats: 32,
    totalSeats: 48,
    alerts: [
      { id: "seat-a12", title: "Ghế A12 sắp hết giờ", detail: "Còn khoảng 10 phút", tone: "warning" },
      { id: "cash-close", title: "Chờ xác nhận cuối ca", detail: "1 khoản thu chi mẫu", tone: "neutral" },
    ],
  },
  "22222222-2222-7222-8222-222222222222": {
    revenue: 6_280_000,
    occupiedSeats: 21,
    totalSeats: 36,
    alerts: [
      { id: "seat-b04", title: "Ghế B04 sắp hết giờ", detail: "Còn khoảng 15 phút", tone: "warning" },
    ],
  },
  "33333333-3333-7333-8333-333333333333": {
    revenue: 2_760_000,
    occupiedSeats: 9,
    totalSeats: 24,
    alerts: [],
  },
  all: {
    revenue: 17_490_000,
    occupiedSeats: 62,
    totalSeats: 108,
    alerts: [
      { id: "seat-a12", title: "Ghế A12 sắp hết giờ", detail: "Nguyễn Trãi · còn khoảng 10 phút", tone: "warning" },
      { id: "cash-close", title: "Chờ xác nhận cuối ca", detail: "Nguyễn Trãi · 1 khoản thu chi mẫu", tone: "neutral" },
      { id: "seat-b04", title: "Ghế B04 sắp hết giờ", detail: "Thảo Điền · còn khoảng 15 phút", tone: "warning" },
    ],
  },
};
