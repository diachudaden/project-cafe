import dayjs from "dayjs";
import utc from "dayjs/plugin/utc";

dayjs.extend(utc);

/** Vietnam has no daylight saving: business times are always shown at UTC+7. */
const VN_OFFSET_MINUTES = 7 * 60;

export const vn = (value?: string | number | Date | dayjs.Dayjs) => dayjs(value).utcOffset(VN_OFFSET_MINUTES);

const vndFormat = new Intl.NumberFormat("vi-VN");

/** 45000 → "45.000đ" (reference UX rule). */
export const vnd = (amount: number) => `${vndFormat.format(Math.round(amount))}đ`;

export const time = (iso: string | null | undefined) => (iso ? vn(iso).format("HH:mm") : "—");
export const dateTime = (iso: string | null | undefined) => (iso ? vn(iso).format("DD/MM HH:mm") : "—");
export const dayMonth = (date: string) => dayjs(date).format("DD/MM");

/** Today's business date in Vietnam as "YYYY-MM-DD". */
export const todayVn = () => vn().format("YYYY-MM-DD");

/** Monday of the week containing <date> ("YYYY-MM-DD"). */
export const weekStart = (date: string) => {
  const d = dayjs(date);
  return d.subtract((d.day() + 6) % 7, "day").format("YYYY-MM-DD");
};

export const addDays = (date: string, days: number) => dayjs(date).add(days, "day").format("YYYY-MM-DD");

export const monthStart = (date: string) => dayjs(date).startOf("month").format("YYYY-MM-DD");

/** "2026-11-02" + "08:00" → ISO with +07:00 offset. */
export const vnIso = (date: string, hhmm: string) => `${date}T${hhmm.length === 5 ? hhmm + ":00" : hhmm}+07:00`;

export const minutesLeftText = (minutes: number) =>
  minutes < 0 ? `Quá giờ ${Math.abs(minutes)} phút` : minutes < 60 ? `Còn ${minutes} phút` : `Còn ${Math.floor(minutes / 60)} giờ ${minutes % 60} phút`;
