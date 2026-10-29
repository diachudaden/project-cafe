export type AuthClient = "admin" | "pos" | "customer";

export type AuthUser = {
  id: string;
  email: string | null;
  phone: string | null;
  fullName: string;
  role: "Admin" | "BranchManager" | "Staff" | "Customer";
  branchIds: string[];
};

export type AuthSession = {
  accessToken: string;
  expiresIn: number;
  user: AuthUser;
};

export type LoginCredentials = {
  identifier: string;
  password: string;
};

export type AuthProviderProps = {
  apiBaseUrl: string;
  client: AuthClient;
  children: React.ReactNode;
};
