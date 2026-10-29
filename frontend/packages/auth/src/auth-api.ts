import type { AuthClient, AuthSession, AuthUser, LoginCredentials } from "./types";

export class AuthApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly type: string,
    readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = "AuthApiError";
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function isAuthRole(value: unknown): value is AuthUser["role"] {
  return value === "Admin" || value === "BranchManager" || value === "Staff" || value === "Customer";
}

function parseUser(value: unknown): AuthUser {
  if (
    !isRecord(value) ||
    typeof value.id !== "string" ||
    !(typeof value.email === "string" || value.email === null) ||
    !(typeof value.phone === "string" || value.phone === null) ||
    typeof value.fullName !== "string" ||
    !isAuthRole(value.role) ||
    !Array.isArray(value.branchIds) ||
    !value.branchIds.every((id) => typeof id === "string")
  ) {
    throw new AuthApiError("Máy chủ trả về thông tin tài khoản không hợp lệ.", 502, "auth.invalid_response");
  }

  return {
    id: value.id,
    email: value.email,
    phone: value.phone,
    fullName: value.fullName,
    role: value.role,
    branchIds: value.branchIds,
  };
}

function parseSession(value: unknown): AuthSession {
  if (
    !isRecord(value) ||
    typeof value.accessToken !== "string" ||
    typeof value.expiresIn !== "number" ||
    !Number.isFinite(value.expiresIn) ||
    value.expiresIn <= 0
  ) {
    throw new AuthApiError("Máy chủ trả về phiên đăng nhập không hợp lệ.", 502, "auth.invalid_response");
  }

  return {
    accessToken: value.accessToken,
    expiresIn: value.expiresIn,
    user: parseUser(value.user),
  };
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch (error) {
    if (error instanceof SyntaxError) {
      throw new AuthApiError("Máy chủ trả về phản hồi không hợp lệ.", 502, "auth.invalid_response");
    }

    throw error;
  }
}

async function request(
  url: string,
  init: RequestInit,
): Promise<Response> {
  try {
    return await fetch(url, { ...init, credentials: "include" });
  } catch (error) {
    if (error instanceof TypeError) {
      throw new AuthApiError(
        "Không kết nối được máy chủ. Thử lại sau.",
        0,
        "auth.network_error",
      );
    }

    throw error;
  }
}

async function readProblem(response: Response): Promise<AuthApiError> {
  let payload: unknown;

  try {
    payload = await response.json();
  } catch (error) {
    if (!(error instanceof SyntaxError)) {
      throw error;
    }
  }

  const type = isRecord(payload) && typeof payload.type === "string" ? payload.type : "";
  const detail = isRecord(payload) && typeof payload.detail === "string" ? payload.detail : "";
  const fieldErrors: Record<string, string[]> = {};
  if (isRecord(payload) && isRecord(payload.errors)) {
    for (const [field, messages] of Object.entries(payload.errors)) {
      if (typeof messages === "string") {
        fieldErrors[field] = [messages];
      } else if (Array.isArray(messages)) {
        const validMessages = messages.filter((message): message is string => typeof message === "string");
        if (validMessages.length > 0) {
          fieldErrors[field] = validMessages;
        }
      }
    }
  }

  return new AuthApiError(
    detail || response.statusText || "Yêu cầu xác thực không thành công.",
    response.status,
    type,
    fieldErrors,
  );
}

export async function login(
  apiBaseUrl: string,
  client: AuthClient,
  credentials: LoginCredentials,
): Promise<AuthSession> {
  const response = await request(`${apiBaseUrl}/api/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ...credentials, client }),
  });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return parseSession(await readJson(response));
}

export async function refresh(apiBaseUrl: string): Promise<AuthSession> {
  const response = await request(`${apiBaseUrl}/api/v1/auth/refresh`, { method: "POST" });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return parseSession(await readJson(response));
}

export async function getCurrentUser(apiBaseUrl: string, accessToken: string): Promise<AuthUser> {
  const response = await request(`${apiBaseUrl}/api/v1/auth/me`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });

  if (!response.ok) {
    throw await readProblem(response);
  }

  return parseUser(await readJson(response));
}

export async function logout(apiBaseUrl: string): Promise<void> {
  const response = await request(`${apiBaseUrl}/api/v1/auth/logout`, { method: "POST" });

  if (!response.ok) {
    throw await readProblem(response);
  }
}
