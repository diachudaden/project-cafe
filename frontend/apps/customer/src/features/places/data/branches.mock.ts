import type { CustomerBranch } from "../types";

export const mockCustomerBranches: CustomerBranch[] = [
  {
    id: "branch-nearby-1",
    name: "Chốn Riêng - Nguyễn Trãi",
    address: "120 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh",
    distanceLabel: "0,8 km",
    openSeats: 12,
    statusLabel: "Đang mở cửa",
  },
  {
    id: "branch-nearby-2",
    name: "Chốn Riêng - Thảo Điền",
    address: "18 Xuân Thủy, TP. Thủ Đức, TP. Hồ Chí Minh",
    distanceLabel: "3,2 km",
    openSeats: 7,
    statusLabel: "Đang mở cửa",
  },
  {
    id: "branch-nearby-3",
    name: "Chốn Riêng - Bạch Đằng",
    address: "42 Bạch Đằng, Hải Châu, Đà Nẵng",
    distanceLabel: "Xa hơn",
    openSeats: 0,
    statusLabel: "Tạm đóng",
  },
];
