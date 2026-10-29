import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { StatePanel } from "./index";

describe("StatePanel", () => {
  it("announces loading and empty states", () => {
    const { rerender } = render(<StatePanel state="loading" title="Đang tải dữ liệu mẫu" />);
    expect(screen.getByRole("status")).toHaveTextContent("Đang tải dữ liệu mẫu");

    rerender(<StatePanel state="empty" title="Chưa có dữ liệu mẫu" />);
    expect(screen.getByRole("status")).toHaveTextContent("Chưa có dữ liệu mẫu");
  });

  it("exposes errors and invokes retry", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<StatePanel state="error" title="Không tải được" onRetry={onRetry} />);

    expect(screen.getByRole("alert")).toHaveTextContent("Không tải được");
    await user.click(screen.getByRole("button", { name: "Thử lại" }));
    expect(onRetry).toHaveBeenCalledOnce();
  });
});
