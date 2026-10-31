import { ApiError } from "@cafecr/api-client";

/** User-facing message for any thrown value; rule violations are listed when present. */
export function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.violations.length) {
      return error.violations.map((v) => v.message).join(" ");
    }
    return error.message;
  }
  return error instanceof Error ? error.message : "Đã có lỗi xảy ra.";
}
