import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { SeatMapPage } from "./SeatMapPage";

describe("SeatMapPage", () => {
  it("only opens check-in for an empty seat and updates the local sample state", async () => {
    const user = userEvent.setup();
    render(<SeatMapPage />);

    const emptySeat = await screen.findByRole("button", { name: "Ghế A01: Còn trống" });
    expect(screen.getByRole("button", { name: "Ghế A02: Đang sử dụng" })).toBeDisabled();

    await user.click(emptySeat);
    expect(screen.getByRole("dialog", { name: "Check-in ghế A01" })).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Xác nhận mẫu" }));
    expect(await screen.findByRole("status")).toHaveTextContent("Đã cập nhật ghế A01 trong trạng thái mẫu");
    expect(screen.getByRole("button", { name: "Ghế A01: Đang sử dụng" })).toBeDisabled();
  });
});
