import type { Role } from "./auth";
import type { Http } from "./http";

// Types mirror backend/src/ChonRieng.Api/Endpoints/AdminResourceContracts.cs and AdminUserContracts.cs.
// Enums are serialized as strings; TimeOnly as "HH:mm:ss"; DateTimeOffset as ISO 8601.

export interface Page<T> {
  page: number;
  pageSize: number;
  items: T[];
}

export interface Branch {
  id: string;
  name: string;
  address: string;
  latitude: number | null;
  longitude: number | null;
  is24h: boolean;
  openTime: string;
  closeTime: string;
  phone: string | null;
  isActive: boolean;
}
export type BranchInput = Omit<Branch, "id">;

export interface Zone {
  id: string;
  branchId: string;
  name: string;
}
export type ZoneInput = Omit<Zone, "id">;

export const seatTypes = ["Single", "Group", "Nap"] as const;
export type SeatType = (typeof seatTypes)[number];
export const seatStatuses = ["Available", "Occupied", "Maintenance"] as const;
export type SeatStatus = (typeof seatStatuses)[number];

export interface Seat {
  id: string;
  zoneId: string;
  code: string;
  type: SeatType;
  status: SeatStatus;
}
export type SeatInput = Omit<Seat, "id">;

export interface PricePlan {
  id: string;
  /** null applies the plan to every branch. */
  branchId: string | null;
  name: string;
  durationMinutes: number;
  price: number;
  extendPrice: number;
  validFrom: string | null;
  validTo: string | null;
}
export type PricePlanInput = Omit<PricePlan, "id">;

export const roles: Role[] = ["Admin", "BranchManager", "Staff", "Customer"];

export interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  lockoutEnd: string | null;
}
export interface AdminUserDetail extends AdminUser {
  roles: Role[];
}
export interface CreateUserInput {
  email: string;
  password: string;
  fullName: string;
  phoneNumber: string | null;
  roles: Role[];
}
export type UpdateUserInput = Omit<CreateUserInput, "password">;

export interface CrudResource<T, TCreate, TUpdate = TCreate, TDetail = T> {
  list(page: number, pageSize: number): Promise<Page<T>>;
  get(id: string): Promise<TDetail>;
  create(input: TCreate): Promise<TDetail>;
  update(id: string, input: TUpdate): Promise<TDetail>;
  remove(id: string): Promise<void>;
}

function crud<T, TCreate, TUpdate = TCreate, TDetail = T>(
  http: Http,
  path: string,
): CrudResource<T, TCreate, TUpdate, TDetail> {
  return {
    list: (page, pageSize) => http.request("GET", `${path}?page=${page}&pageSize=${pageSize}`),
    get: (id) => http.request("GET", `${path}/${id}`),
    create: (input) => http.request("POST", path, input),
    update: (id, input) => http.request("PUT", `${path}/${id}`, input),
    remove: (id) => http.request("DELETE", `${path}/${id}`),
  };
}

export function createAdminApi(http: Http) {
  return {
    branches: crud<Branch, BranchInput>(http, "/api/admin/branches"),
    zones: crud<Zone, ZoneInput>(http, "/api/admin/zones"),
    seats: crud<Seat, SeatInput>(http, "/api/admin/seats"),
    pricePlans: crud<PricePlan, PricePlanInput>(http, "/api/admin/price-plans"),
    users: crud<AdminUser, CreateUserInput, UpdateUserInput, AdminUserDetail>(http, "/api/admin/users"),
  };
}

export type AdminApi = ReturnType<typeof createAdminApi>;
