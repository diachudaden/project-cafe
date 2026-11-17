import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import type {
  AuthSession,
  AuthUser,
  ChangePasswordInput,
  ForgotPasswordInput,
  ForgotPasswordResult,
  LoginInput,
  RegisterInput,
  ResetPasswordInput,
  Role,
} from "@cafecr/api-client";

export type AuthState =
  | { status: "loading"; user: null }
  | { status: "signedOut"; user: null }
  | { status: "signedIn"; user: AuthUser };

export interface AuthContextValue {
  state: AuthState;
  login(input: LoginInput): Promise<AuthUser>;
  register(input: RegisterInput): Promise<AuthUser>;
  logout(): Promise<void>;
  forgotPassword(input: ForgotPasswordInput): Promise<ForgotPasswordResult>;
  resetPassword(input: ResetPasswordInput): Promise<void>;
  /** Keeps the current session; other sessions of the account are signed out. */
  changePassword(input: Omit<ChangePasswordInput, "refreshToken">): Promise<void>;
}

export const hasRole = (user: AuthUser | null, ...roles: Role[]) => !!user && roles.some((r) => user.roles.includes(r));

/** One auth context per app; each app owns its AuthSession (separate refresh-token storage key). */
export function createAuth(session: AuthSession, onSignedOut?: () => void) {
  const AuthContext = createContext<AuthContextValue | null>(null);

  function AuthProvider({ children }: { children: ReactNode }) {
    const [state, setState] = useState<AuthState>({ status: "loading", user: null });

    useEffect(() => {
      const unsubscribe = session.subscribe((user) =>
        setState(user ? { status: "signedIn", user } : { status: "signedOut", user: null }),
      );
      // Restore the session after a reload from the refresh token kept in sessionStorage.
      session
        .refresh()
        .then((user) => {
          if (!user) setState({ status: "signedOut", user: null });
        })
        .catch(() => setState({ status: "signedOut", user: null }));
      return unsubscribe;
    }, []);

    const login = useCallback((input: LoginInput) => session.login(input), []);
    const register = useCallback((input: RegisterInput) => session.register(input), []);
    const logout = useCallback(async () => {
      await session.logout();
      setState({ status: "signedOut", user: null });
      onSignedOut?.();
    }, []);

    const forgotPassword = useCallback((input: ForgotPasswordInput) => session.forgotPassword(input), []);
    const resetPassword = useCallback((input: ResetPasswordInput) => session.resetPassword(input), []);
    const changePassword = useCallback((input: Omit<ChangePasswordInput, "refreshToken">) => session.changePassword(input), []);

    return (
      <AuthContext.Provider value={{ state, login, register, logout, forgotPassword, resetPassword, changePassword }}>
        {children}
      </AuthContext.Provider>
    );
  }

  function useAuth(): AuthContextValue {
    const value = useContext(AuthContext);
    if (!value) throw new Error("useAuth must be used inside AuthProvider");
    return value;
  }

  return { AuthProvider, useAuth };
}
