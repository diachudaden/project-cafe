import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { SignInForm } from "./index";

describe("SignInForm", () => {
  it("validates required fields before submitting", async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn<() => Promise<void>>().mockResolvedValue();
    render(<SignInForm appName="Khách hàng" error={null} onSubmit={onSubmit} />);

    await user.click(screen.getByRole("button", { name: "Đăng nhập" }));

    expect(await screen.findByText("Nhập email hoặc số điện thoại.")).toBeInTheDocument();
    expect(screen.getByText("Nhập mật khẩu.")).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("submits normalized credentials and disables while pending", async () => {
    const user = userEvent.setup();
    let finishSubmit!: () => void;
    const onSubmit = vi.fn(() => new Promise<void>((resolve) => {
      finishSubmit = resolve;
    }));
    render(<SignInForm appName="Quản trị" error={null} onSubmit={onSubmit} />);

    await user.type(screen.getByLabelText("Email hoặc số điện thoại"), " user@example.com ");
    await user.type(screen.getByLabelText("Mật khẩu"), "secret");
    await user.click(screen.getByRole("button", { name: "Đăng nhập" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith({
      identifier: "user@example.com",
      password: "secret",
    }));
    expect(screen.getByRole("button", { name: "Đang đăng nhập..." })).toBeDisabled();
    finishSubmit();
    await waitFor(() => expect(screen.getByRole("button", { name: "Đăng nhập" })).toBeEnabled());
  });

  it("announces API errors", () => {
    render(
      <SignInForm
        appName="Điểm phục vụ"
        error="Email/số điện thoại hoặc mật khẩu không đúng."
        fieldErrors={{ identifier: "Định danh chưa hợp lệ." }}
        onSubmit={vi.fn<() => Promise<void>>().mockResolvedValue()}
      />,
    );

    expect(screen.getByRole("alert")).toHaveTextContent("Email/số điện thoại hoặc mật khẩu không đúng.");
    expect(screen.getByText("Định danh chưa hợp lệ.")).toBeInTheDocument();
  });
});
