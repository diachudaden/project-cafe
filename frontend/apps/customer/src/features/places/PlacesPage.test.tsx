import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PlacesPage } from "./PlacesPage";

describe("PlacesPage", () => {
  it("shows nearby sample branches and their sample availability", async () => {
    render(<PlacesPage />);

    expect(await screen.findByText("Chốn Riêng - Nguyễn Trãi")).toBeInTheDocument();
    expect(screen.getByText("Khoảng 0,8 km")).toBeInTheDocument();
    expect(screen.getByText("12 chỗ trống · mẫu")).toBeInTheDocument();
    expect(screen.getByText("Chốn Riêng - Thảo Điền")).toBeInTheDocument();
  });
});
