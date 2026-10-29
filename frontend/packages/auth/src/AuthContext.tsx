import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  AuthApiError,
  getCurrentUser,
  login as requestLogin,
  logout as requestLogout,
  refresh as requestRefresh,
} from "./auth-api";
import { getAuthErrorMessage, isInvalidAccessToken } from "./error-message";
import type { AuthClient, AuthSession, AuthUser, LoginCredentials } from "./types";

type AuthStatus = "checking" | "anonymous" | "authenticated";

type AuthContextValue = {
  accessToken: string | null;
  error: string | null;
  fieldErrors: Partial<Record<"identifier" | "password", string>>;
  status: AuthStatus;
  user: AuthUser | null;
  signIn: (credentials: LoginCredentials) => Promise<boolean>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);
let restoreInFlight: Promise<AuthSession> | null = null;

const rolesByClient: Record<AuthClient, readonly AuthUser["role"][]> = {
  admin: ["Admin", "BranchManager"],
  pos: ["Staff"],
  customer: ["Customer"],
};

function getLoginFieldErrors(error: unknown): AuthContextValue["fieldErrors"] {
  if (!(error instanceof AuthApiError)) {
    return {};
  }

  const errors: AuthContextValue["fieldErrors"] = {};
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const key = field.toLowerCase();
    if ((key === "identifier" || key === "password") && messages[0]) {
      errors[key] = messages[0];
    }
  }
  return errors;
}

function assertClientAccess(user: AuthUser, client: AuthClient): void {
  if (!rolesByClient[client].includes(user.role)) {
    throw new AuthApiError(
      "Tài khoản này không dùng được trên ứng dụng này.",
      403,
      "auth.forbidden_client",
    );
  }
}

async function currentUserWithRefresh(
  apiBaseUrl: string,
  session: AuthSession,
): Promise<{ session: AuthSession; user: AuthUser }> {
  try {
    return { session, user: await getCurrentUser(apiBaseUrl, session.accessToken) };
  } catch (error) {
    if (!isInvalidAccessToken(error)) {
      throw error;
    }

    const renewedSession = await requestRefresh(apiBaseUrl);
    return {
      session: renewedSession,
      user: await getCurrentUser(apiBaseUrl, renewedSession.accessToken),
    };
  }
}

function restoreSession(apiBaseUrl: string): Promise<AuthSession> {
  if (!restoreInFlight) {
    restoreInFlight = requestRefresh(apiBaseUrl).finally(() => {
      restoreInFlight = null;
    });
  }

  return restoreInFlight;
}

export function AuthProvider({
  apiBaseUrl,
  client,
  children,
}: {
  apiBaseUrl: string;
  client: AuthClient;
  children: ReactNode;
}) {
  const [status, setStatus] = useState<AuthStatus>("checking");
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [user, setUser] = useState<AuthUser | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<AuthContextValue["fieldErrors"]>({});

  const clearSession = useCallback(() => {
    setAccessToken(null);
    setUser(null);
    setStatus("anonymous");
  }, []);

  useEffect(() => {
    let active = true;
    setStatus("checking");
    void restoreSession(apiBaseUrl)
      .then(async (session) => {
        assertClientAccess(session.user, client);
        const current = await currentUserWithRefresh(apiBaseUrl, session);
        assertClientAccess(current.user, client);
        if (active) {
          setAccessToken(current.session.accessToken);
          setUser(current.user);
          setError(null);
          setStatus("authenticated");
        }
      })
      .catch((restoreError: unknown) => {
        if (active) {
          clearSession();
          setError(getAuthErrorMessage(restoreError));
        }
      });
    return () => {
      active = false;
    };
  }, [apiBaseUrl, clearSession, client]);

  const signIn = useCallback(async (credentials: LoginCredentials) => {
    setError(null);
    setFieldErrors({});
    try {
      const session = await requestLogin(apiBaseUrl, client, credentials);
      assertClientAccess(session.user, client);
      const current = await currentUserWithRefresh(apiBaseUrl, session);
      assertClientAccess(current.user, client);
      setAccessToken(current.session.accessToken);
      setUser(current.user);
      setStatus("authenticated");
      return true;
    } catch (signInError) {
      clearSession();
      setError(getAuthErrorMessage(signInError));
      setFieldErrors(getLoginFieldErrors(signInError));
      return false;
    }
  }, [apiBaseUrl, clearSession, client]);

  const signOut = useCallback(async () => {
    const pendingLogout = requestLogout(apiBaseUrl);
    clearSession();
    try {
      await pendingLogout;
      setError(null);
    } catch (logoutError) {
      setError(getAuthErrorMessage(logoutError));
    }
  }, [apiBaseUrl, clearSession]);

  const value = useMemo<AuthContextValue>(() => ({
    accessToken,
    error,
    fieldErrors,
    status,
    user,
    signIn,
    signOut,
  }), [accessToken, error, fieldErrors, signIn, signOut, status, user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return value;
}
