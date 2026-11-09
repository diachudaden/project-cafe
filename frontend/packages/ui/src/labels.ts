import type {
  AssignmentStatus, ClosingStatus, PaymentMethod, Role, SeatLiveState, SeatType, ShiftPhase, ShiftRequestType, ShiftType,
} from "@cafecr/api-client";

export const roleLabels: Record<Role, string> = {
  Admin: "Quản trị viên",
  BranchManager: "Quản lý chi nhánh",
  Staff: "Nhân viên",
  Customer: "Khách hàng",
};

export const shiftTypeLabels: Record<ShiftType, string> = { Morning: "Ca sáng", Afternoon: "Ca chiều", Evening: "Ca tối", Night: "Ca đêm" };

/** Default hours per shift type (24/7 branches use 3 × 8 h; others split at 15:00). */
export const shiftTypeDefaults: Record<ShiftType, [string, string]> = {
  Morning: ["06:00", "14:00"],
  Afternoon: ["14:00", "22:00"],
  Evening: ["15:00", "23:00"],
  Night: ["22:00", "06:00"],
};

export const phaseLabels: Record<ShiftPhase, { label: string; color: string }> = {
  Draft: { label: "Nháp", color: "default" },
  Published: { label: "Đã công bố", color: "blue" },
  InProgress: { label: "Đang diễn ra", color: "green" },
  Closed: { label: "Đã kết ca", color: "purple" },
  Approved: { label: "Đã duyệt", color: "cyan" },
};

export const assignmentLabels: Record<AssignmentStatus, { label: string; color: string }> = {
  Offered: { label: "Chờ phản hồi", color: "gold" },
  Accepted: { label: "Đã nhận", color: "green" },
  Declined: { label: "Từ chối", color: "red" },
};

export const seatTypeLabels: Record<SeatType, string> = { Single: "Bàn đơn", Group: "Bàn nhóm", Nap: "Góc ngủ" };

/** Reference UX colours: green = free, orange = ending soon, red = overdue, grey = maintenance. */
export const seatStateStyles: Record<SeatLiveState, { label: string; color: string; background: string }> = {
  Available: { label: "Trống", color: "#237804", background: "#d9f7be" },
  Occupied: { label: "Đang dùng", color: "#0958d9", background: "#d6e4ff" },
  Held: { label: "Chờ thanh toán", color: "#722ed1", background: "#efdbff" },
  EndingSoon: { label: "Sắp hết giờ", color: "#ad4e00", background: "#ffe7ba" },
  Overdue: { label: "Quá giờ", color: "#a8071a", background: "#ffccc7" },
  Maintenance: { label: "Bảo trì", color: "#595959", background: "#f0f0f0" },
};

export const paymentLabels: Record<PaymentMethod, string> = { Cash: "Tiền mặt", VietQr: "VietQR", Mixed: "Hỗn hợp" };

export const closingLabels: Record<ClosingStatus, { label: string; color: string }> = {
  Draft: { label: "Nháp", color: "default" },
  Submitted: { label: "Chờ duyệt", color: "gold" },
  Approved: { label: "Đã duyệt", color: "green" },
  Rejected: { label: "Bị từ chối", color: "red" },
};

export const requestTypeLabels: Record<ShiftRequestType, string> = { Swap: "Đổi ca", Leave: "Xin nghỉ", Drop: "Trả ca" };

export const requestStatusLabels: Record<string, { label: string; color: string }> = {
  Pending: { label: "Chờ duyệt", color: "gold" },
  Approved: { label: "Đã duyệt", color: "green" },
  Rejected: { label: "Từ chối", color: "red" },
  Cancelled: { label: "Đã hủy", color: "default" },
};

export const tierLabels: Record<string, { label: string; color: string }> = {
  Bronze: { label: "Đồng", color: "#ad6800" },
  Silver: { label: "Bạc", color: "#8c8c8c" },
  Gold: { label: "Vàng", color: "#d4b106" },
  DEMO: { label: "Đồng", color: "#ad6800" },
};

export const dayOfWeekLabels: Record<string, string> = {
  Monday: "Thứ 2", Tuesday: "Thứ 3", Wednesday: "Thứ 4", Thursday: "Thứ 5", Friday: "Thứ 6", Saturday: "Thứ 7", Sunday: "Chủ nhật",
};
