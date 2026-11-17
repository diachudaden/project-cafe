import { describe, expect, it } from "vitest";
import { newPasswordErrors, passwordProblems } from "./password";

describe("passwordProblems", () => {
  it("accepts a password meeting every backend rule", () => {
    expect(passwordProblems("New-Password-456!")).toEqual([]);
    expect(passwordProblems("Mậtkhẩu mới 2026")).toEqual([]);
  });

  it("follows Identity's ASCII-only character classes", () => {
    expect(passwordProblems("matkhauvietnam2026ậ")).toEqual(["Mật khẩu phải có ít nhất một chữ hoa."]); // ậ = special
    expect(passwordProblems("ĐỔI-MẬT-KHẨU-2026")).toEqual(["Mật khẩu phải có ít nhất một chữ thường."]); // Ổ is not A-Z
  });

  it.each([
    ["Short-1a!", "ít nhất 12 ký tự"],
    ["new-password-456!", "chữ hoa"],
    ["NEW-PASSWORD-456!", "chữ thường"],
    ["New-Password-abc!", "chữ số"],
    ["NewPassword4567", "ký tự đặc biệt"],
  ])("rejects %s (%s)", (password, rule) => {
    const problems = passwordProblems(password);
    expect(problems).toHaveLength(1);
    expect(problems[0]).toContain(rule);
  });

  it("lists every broken rule for an empty password", () => {
    expect(passwordProblems("")).toHaveLength(5);
  });
});

describe("newPasswordErrors", () => {
  it("requires the confirmation to match", () => {
    expect(newPasswordErrors("New-Password-456!", "New-Password-456!")).toEqual({ newPassword: [], confirmation: [] });
    expect(newPasswordErrors("New-Password-456!", "New-Password-457!").confirmation).toEqual(["Mật khẩu nhập lại không khớp."]);
  });
});
