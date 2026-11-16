/**
 * Mirrors the Identity password options in backend Program.cs (RequiredLength 12, digit, lower, upper,
 * non-alphanumeric). Identity's PasswordValidator is ASCII-only: "ậ" is not a lowercase letter there, it counts as a
 * special character, so the same ranges are used here.
 */
export const PASSWORD_MIN_LENGTH = 12;

export const passwordPolicyText = "Tối thiểu 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";

/** Vietnamese messages for every rule the new password breaks; empty when it is acceptable. */
export function passwordProblems(password: string): string[] {
  const problems: string[] = [];
  if (password.length < PASSWORD_MIN_LENGTH) problems.push(`Mật khẩu phải có ít nhất ${PASSWORD_MIN_LENGTH} ký tự.`);
  if (!/[A-Z]/.test(password)) problems.push("Mật khẩu phải có ít nhất một chữ hoa.");
  if (!/[a-z]/.test(password)) problems.push("Mật khẩu phải có ít nhất một chữ thường.");
  if (!/[0-9]/.test(password)) problems.push("Mật khẩu phải có ít nhất một chữ số.");
  if (!/[^A-Za-z0-9]/.test(password)) problems.push("Mật khẩu phải có ít nhất một ký tự đặc biệt.");
  return problems;
}

/** Problems for a new password + confirmation pair, as shown under the form fields. */
export function newPasswordErrors(newPassword: string, confirmation: string): { newPassword: string[]; confirmation: string[] } {
  return {
    newPassword: passwordProblems(newPassword),
    confirmation: confirmation === newPassword ? [] : ["Mật khẩu nhập lại không khớp."],
  };
}
