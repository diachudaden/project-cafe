import type { PosSeat } from "../types";

export const mockShiftSummary = {
  label: "Ca chiều · 14:00 - 22:00",
  cashOnHand: 1_250_000,
};

export const mockSeats: PosSeat[] = [
  { id: "A01", zone: "Tầng trệt", status: "empty" },
  { id: "A02", zone: "Tầng trệt", status: "occupied", guestLabel: "Khách tại quầy", timeLabel: "Còn 1 giờ 20 phút" },
  { id: "A03", zone: "Tầng trệt", status: "expiring", guestLabel: "Khách tại quầy", timeLabel: "Còn 8 phút" },
  { id: "A04", zone: "Tầng trệt", status: "maintenance" },
  { id: "A05", zone: "Tầng trệt", status: "empty" },
  { id: "A06", zone: "Tầng trệt", status: "occupied", guestLabel: "Khách tại quầy", timeLabel: "Còn 45 phút" },
  { id: "B01", zone: "Tầng lửng", status: "empty" },
  { id: "B02", zone: "Tầng lửng", status: "expiring", guestLabel: "Khách tại quầy", timeLabel: "Còn 12 phút" },
  { id: "B03", zone: "Tầng lửng", status: "occupied", guestLabel: "Khách tại quầy", timeLabel: "Còn 2 giờ" },
  { id: "B04", zone: "Tầng lửng", status: "empty" },
  { id: "B05", zone: "Tầng lửng", status: "maintenance" },
  { id: "B06", zone: "Tầng lửng", status: "empty" },
];
