import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import { useQueryClient } from "@tanstack/react-query";
import type { AuthUser, LoginInput } from "@cafecr/api-client";
import { session } from "../api";

type AuthState =
  | { status: "loading"; user: null }
  | { status: "signedOut"; user: null }
  | { status: "signedIn"; user: AuthUser };

interface AuthContextValue {
  state: AuthState;
  login(input: LoginInput): Promise<AuthUser>;
  logout(): Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
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

  const logout = useCallback(async () => {
    await session.logout();
    setState({ status: "signedOut", user: null });
    queryClient.clear();
  }, [queryClient]);

  return <AuthContext.Provider value={{ state, login, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used inside AuthProvider");
  return value;
}

export const isAdmin = (user: AuthUser) => user.roles.includes("Admin");
