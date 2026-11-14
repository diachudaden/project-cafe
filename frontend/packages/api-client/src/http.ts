/** RFC 7807 problem body returned by the API for 400/404/409 responses. */
/** Business-rule violation returned with 409 (e.g. shift conflicts). */
export interface RuleViolation {
  rule: string;
  message: string;
  canOverride: boolean;
  isBlocking: boolean;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  violations?: RuleViolation[];
  /** Added by ASP.NET Core to every ProblemDetails; quote it when reporting a server error. */
  traceId?: string;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors: Record<string, string[]> = {},
    readonly violations: RuleViolation[] = [],
    readonly title?: string,
    readonly traceId?: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

const statusMessages: Record<number, string> = {
  400: "Dữ liệu gửi lên không hợp lệ.",
  401: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
  403: "Tài khoản không có quyền thực hiện thao tác này.",
  404: "Không tìm thấy dữ liệu.",
  409: "Dữ liệu bị xung đột với trạng thái hiện tại.",
  429: "Thao tác quá nhanh. Vui lòng thử lại sau ít phút.",
};

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails = {};
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Empty or non-JSON body (e.g. bare 401/404).
  }

  const fieldErrors = problem.errors ?? {};
  const firstFieldError = Object.values(fieldErrors).flat()[0];
  const message =
    problem.detail ??
    firstFieldError ??
    statusMessages[response.status] ??
    problem.title ??
    "Yêu cầu không thành công.";

  return new ApiError(response.status, message, fieldErrors, problem.violations ?? [], problem.title, problem.traceId);
}

export interface HttpOptions {
  /** Origin of the API; empty string means same origin (dev proxy). */
  baseUrl?: string;
  getAccessToken?: () => string | null;
  /** Called once after a 401; resolves true when a new access token is available. */
  refreshAccessToken?: () => Promise<boolean>;
  fetch?: typeof fetch;
}

export type HttpMethod = "GET" | "POST" | "PUT" | "DELETE";

/** Builds "?a=1&b=2", skipping undefined/null/empty values. */
export function query(params: Record<string, string | number | boolean | null | undefined>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== "");
  return entries.length ? "?" + new URLSearchParams(entries.map(([k, v]) => [k, String(v)])).toString() : "";
}

export interface Http {
  request<T>(method: HttpMethod, path: string, body?: unknown): Promise<T>;
}

export function createHttp(options: HttpOptions = {}): Http {
  const baseUrl = options.baseUrl ?? "";
  const doFetch = options.fetch ?? ((input, init) => fetch(input, init));

  async function request<T>(method: HttpMethod, path: string, body?: unknown, canRetry = true): Promise<T> {
    const headers: Record<string, string> = { Accept: "application/json" };
    if (body !== undefined) {
      headers["Content-Type"] = "application/json";
    }

    const token = options.getAccessToken?.() ?? null;
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    let response: Response;
    try {
      response = await doFetch(`${baseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch {
      throw new ApiError(0, "Không kết nối được máy chủ. Vui lòng thử lại.");
    }

    if (response.status === 401 && token && canRetry && options.refreshAccessToken) {
      if (await options.refreshAccessToken()) {
        return request<T>(method, path, body, false);
      }
    }

    if (!response.ok) {
      throw await toApiError(response);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  return { request: (method, path, body) => request(method, path, body) };
}
