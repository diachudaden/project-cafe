export type SeatStatus = "empty" | "occupied" | "expiring" | "maintenance";

export interface PosSeat {
  id: string;
  zone: string;
  status: SeatStatus;
  guestLabel?: string;
  timeLabel?: string;
}

export type PaymentChoice = "cash" | "vietqr";
