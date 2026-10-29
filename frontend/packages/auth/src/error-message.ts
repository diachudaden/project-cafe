import { AuthApiError } from "./auth-api";

export function getAuthErrorMessage(error: unknown): string {
  if (!(error instanceof AuthApiError)) {
    return error instanceof Error ? error.message : "Đã xảy ra lỗi xác thực. Thử lại sau.";
  }

  if (error.status === 0 || error.status >= 500) {
    return "Không kết nối được máy chủ. Thử lại sau.";
  }

  switch (error.type) {
    case "validation":
      return "Kiểm tra lại thông tin đăng nhập.";
    case "auth.invalid_credentials":
      return "Email/số điện thoại hoặc mật khẩu không đúng.";
    case "auth.account_locked":
      return "Tài khoản đang bị khóa tạm thời. Thử lại sau.";
    case "auth.forbidden_client":
      return "Tài khoản này không dùng được trên ứng dụng này.";
    case "auth.session_revoked":
      return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    case "auth.invalid_token":
      return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    default:
      return error.message || "Yêu cầu xác thực không thành công.";
  }
}

export function isInvalidAccessToken(error: unknown): boolean {
  return error instanceof AuthApiError && error.status === 401 && error.type === "auth.invalid_token";
}

export function isRevokedSession(error: unknown): boolean {
  return error instanceof AuthApiError && error.status === 401 && error.type === "auth.session_revoked";
}
