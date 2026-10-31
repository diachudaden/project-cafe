import { ApiError, createHttp, type Http, type HttpOptions } from "./http";

export type Role = "Admin" | "BranchManager" | "Staff" | "Customer";

/** Mirrors AuthUserResponse in backend/src/ChonRieng.Api/Endpoints/AuthContracts.cs. */
export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  roles: Role[];
  /** Branches assigned to a BranchManager/Staff; empty for Admin and Customer. */
  branchIds: string[];
}

/** Mirrors AuthTokenResponse. */
export interface AuthTokens {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: AuthUser;
}

export interface RegisterInput {
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string | null;
  deviceId?: string;
}

export interface LoginInput {
  email: string;
  password: string;
  deviceId?: string;
}

export function createAuthApi(http: Http) {
  return {
    login: (input: LoginInput) => http.request<AuthTokens>("POST", "/api/auth/login", input),
    register: (input: RegisterInput) => http.request<AuthTokens>("POST", "/api/auth/register", input),
    refresh: (refreshToken: string) => http.request<AuthTokens>("POST", "/api/auth/refresh", { refreshToken }),
    logout: (refreshToken: string) => http.request<void>("POST", "/api/auth/logout", { refreshToken }),
    me: () => http.request<AuthUser>("GET", "/api/auth/me"),
  };
}

export type AuthApi = ReturnType<typeof createAuthApi>;

type TokenStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

export interface AuthSessionOptions extends Pick<HttpOptions, "baseUrl" | "fetch"> {
  /** Where the refresh token survives a page reload. The access token is kept in memory only. */
  storage: TokenStorage;
  storageKey: string;
}

/**
 * Holds the access token in memory and the rotating refresh token in storage.
 * Concurrent refresh requests share one in-flight call, because the backend
 * revokes a refresh token as soon as it is used.
 */
export class AuthSession {
  private accessToken: string | null = null;
  private refreshing: Promise<AuthUser | null> | null = null;
  private readonly listeners = new Set<(user: AuthUser | null) => void>();
  private readonly authApi: AuthApi;
  /** Authenticated client for every other API call. */
  readonly http: Http;

  constructor(private readonly options: AuthSessionOptions) {
    this.authApi = createAuthApi(createHttp({ baseUrl: options.baseUrl, fetch: options.fetch }));
    this.http = createHttp({
      baseUrl: options.baseUrl,
      fetch: options.fetch,
      getAccessToken: () => this.accessToken,
      refreshAccessToken: async () => (await this.refresh()) !== null,
    });
  }

  subscribe(listener: (user: AuthUser | null) => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  async login(input: LoginInput): Promise<AuthUser> {
    const tokens = await this.authApi.login(input);
    this.apply(tokens);
    return tokens.user;
  }

  async register(input: RegisterInput): Promise<AuthUser> {
    const tokens = await this.authApi.register(input);
    this.apply(tokens);
    return tokens.user;
  }

  /** Exchanges the stored refresh token for a new pair; resolves null when signed out. */
  refresh(): Promise<AuthUser | null> {
    this.refreshing ??= this.doRefresh().finally(() => {
      this.refreshing = null;
    });
    return this.refreshing;
  }

  async logout(): Promise<void> {
    const refreshToken = this.options.storage.getItem(this.options.storageKey);
    this.clear();
    if (refreshToken) {
      try {
        await this.authApi.logout(refreshToken);
      } catch {
        // The local session is already gone; an unreachable server must not block sign-out.
      }
    }
  }

  private async doRefresh(): Promise<AuthUser | null> {
    const refreshToken = this.options.storage.getItem(this.options.storageKey);
    if (!refreshToken) {
      return null;
    }

    try {
      const tokens = await this.authApi.refresh(refreshToken);
      this.apply(tokens);
      return tokens.user;
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        this.clear();
        return null;
      }
      throw error;
    }
  }

  private apply(tokens: AuthTokens) {
    this.accessToken = tokens.accessToken;
    this.options.storage.setItem(this.options.storageKey, tokens.refreshToken);
    this.listeners.forEach((listener) => listener(tokens.user));
  }

  private clear() {
    const wasSignedIn = this.accessToken !== null;
    this.accessToken = null;
    this.options.storage.removeItem(this.options.storageKey);
    if (wasSignedIn) {
      this.listeners.forEach((listener) => listener(null));
    }
  }
}
