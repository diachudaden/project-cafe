import { mockSeats } from "../data/seats.mock";
import type { PosSeat } from "../types";

export async function getMockSeats(): Promise<PosSeat[]> {
  return mockSeats.map((seat) => ({ ...seat }));
}
