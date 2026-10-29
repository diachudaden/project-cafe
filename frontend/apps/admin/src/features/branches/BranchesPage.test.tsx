import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { BranchesPage } from "./BranchesPage";

describe("BranchesPage", () => {
  it("shows read-only sample branch information", async () => {
    render(<BranchesPage />);

    expect(await screen.findByText("Chốn Riêng - Nguyễn Trãi")).toBeInTheDocument();
    expect(screen.getByText("120 Nguyễn Trãi, Quận 1, TP. Hồ Chí Minh")).toBeInTheDocument();
    expect(screen.getAllByText("Đang hoạt động")).toHaveLength(2);
  });
});
