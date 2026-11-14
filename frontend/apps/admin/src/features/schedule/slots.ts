import type { GlobalToken } from "antd";
import type { PublicBranch, ShiftType, ShiftView } from "@cafecr/api-client";
import { addDays, shiftTypeDefaults, vnIso } from "@cafecr/ui";

export const allShiftTypes: ShiftType[] = ["Morning", "Afternoon", "Evening", "Night"];

type BranchHours = Pick<PublicBranch, "is24h" | "openTime" | "closeTime">;

/** Non-24h branches split their day into two shifts at this time. */
const SPLIT = "15:00";
const hhmm = (t: string) => t.slice(0, 5);

/** Rows shown by default: 24/7 branches run Sáng / Chiều / Đêm (3 × 8 h), the others Sáng / Tối. */
export function defaultShiftTypes(branch: BranchHours | undefined): ShiftType[] {
  if (!branch) return allShiftTypes;
  return branch.is24h ? ["Morning", "Afternoon", "Night"] : ["Morning", "Evening"];
}

/** Types to draw: the branch defaults (or all), plus any type that already has a shift this week. */
export function visibleShiftTypes(branch: BranchHours | undefined, shifts: ShiftView[], showAll: boolean): ShiftType[] {
  if (showAll) return allShiftTypes;
  const wanted = new Set<ShiftType>([...defaultShiftTypes(branch), ...shifts.map((s) => s.type)]);
  return allShiftTypes.filter((t) => wanted.has(t));
}

/**
 * Default hours for a new shift of this type. 24/7 branches use the standard 3 × 8 h table; other
 * branches start "Ca sáng" at opening time and end "Ca tối" at closing time (demo-level precision).
 */
export function slotHours(type: ShiftType, branch: BranchHours | undefined): [string, string] {
  const fallback = shiftTypeDefaults[type];
  if (!branch || branch.is24h || type === "Night") return fallback;
  const open = hhmm(branch.openTime);
  const close = hhmm(branch.closeTime);
  const closesAfterMidnight = close <= open;
  let [start, end] = fallback;
  if (type === "Morning") [start, end] = [open < SPLIT ? open : start, SPLIT];
  else if (type === "Evening") [start, end] = [SPLIT, close];
  else {
    if (open > start) start = open;
    if (!closesAfterMidnight && close < end) end = close;
  }
  // Anything odd (e.g. a branch opening after 15:00) falls back to the standard hours.
  return start === end ? fallback : [start, end];
}

/** An end time at or before the start means the shift ends the next day (night shift). */
export function shiftTimes(date: string, start: string, end: string) {
  const endDate = end <= start ? addDays(date, 1) : date;
  return { startAt: vnIso(date, start), endAt: vnIso(endDate, end) };
}

/** One accent per weekday (Mon → Sun), from Ant Design's preset palettes so dark mode adapts. */
const dayPalette = ["blue", "cyan", "green", "gold", "orange", "magenta", "purple"] as const;

export function dayColors(token: GlobalToken, weekdayIndex: number) {
  const name = dayPalette[weekdayIndex % dayPalette.length];
  const t = token as unknown as Record<string, string>;
  return { tint: t[`${name}1`], soft: t[`${name}2`], accent: t[`${name}6`], text: t[`${name}7`] };
}

export const cleanName = (name: string) => name.replace("[DEMO DATA] ", "");
/** Vietnamese names put the given name last: "Nguyễn Văn An" → "An". */
export const givenName = (name: string) => cleanName(name).trim().split(/\s+/).pop() ?? name;

export type Highlight = "draft" | "understaffed" | "open" | "waiting";

export function matches(shift: ShiftView, h: Highlight) {
  switch (h) {
    case "draft": return shift.status === "Draft";
    case "understaffed": return shift.understaffed;
    case "open": return shift.isOpen;
    case "waiting": return shift.assignments.some((a) => a.status === "Offered");
  }
}
