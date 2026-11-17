import { Fragment, useState, type CSSProperties, type ReactNode } from "react";
import { useMutation } from "@tanstack/react-query";
import { Avatar, Badge, Button, Divider, InputNumber, Popover, Segmented, Space, Switch, Tag, TimePicker, Tooltip, Typography, theme } from "antd";
import { ClockCircleOutlined, CloudOutlined, EditOutlined, MoonOutlined, PlusOutlined, StarOutlined, SunOutlined, UserAddOutlined } from "@ant-design/icons";
import dayjs from "dayjs";
import type { AssignmentStatus, PublicBranch, ShiftType, ShiftView } from "@cafecr/api-client";
import { assignmentLabels, dayMonth, phaseLabels, shiftTypeLabels, time, todayVn } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../../components/useNotify";
import { cleanName, dayColors, givenName, matches, shiftTimes, slotHours, type Highlight } from "./slots";

export const weekdays = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật"];
const weekdaysShort = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

export const shiftTypeIcons: Record<ShiftType, ReactNode> = {
  Morning: <SunOutlined />,
  Afternoon: <CloudOutlined />,
  Evening: <MoonOutlined />,
  Night: <StarOutlined />,
};

export interface GridProps {
  branchId: string;
  branch: PublicBranch | undefined;
  days: string[];
  types: ShiftType[];
  shifts: ShiftView[];
  highlight: Highlight | null;
  onOpenShift(id: string): void;
  onCreated(): void;
}

const css = `
.cr-slot-empty { transition: border-color .15s, color .15s, background .15s; }
.cr-slot-empty:hover { border-color: var(--cr-accent) !important; color: var(--cr-accent) !important; }
.cr-shift-tile { transition: transform .15s, box-shadow .15s; }
.cr-shift-tile:hover { transform: translateY(-1px); }
`;

/** Desktop: shift types as rows, days as columns, so the same slot is always in the same place. */
export function ScheduleGrid(props: GridProps) {
  const { days, types, shifts, branch } = props;
  const today = todayVn();

  return (
    <div style={{ overflowX: "auto" }}>
      <style>{css}</style>
      <div style={{ display: "grid", gridTemplateColumns: "136px repeat(7, minmax(118px, 1fr))", gap: 6, minWidth: 1000 }}>
        <div />
        {days.map((date, i) => (
          <DayHeader key={date} date={date} index={i} today={today} count={shifts.filter((s) => s.date === date).length} />
        ))}
        {types.map((type, row) => (
          <Fragment key={type}>
            <RowHeader type={type} branch={branch} />
            {days.map((date, i) => (
              <SlotCell key={date} {...props} date={date} dayIndex={i} type={type} last={row === types.length - 1} />
            ))}
          </Fragment>
        ))}
      </div>
      <Legend />
    </div>
  );
}

/** Phone width: one day at a time, its slots stacked. */
export function ScheduleDayView(props: GridProps) {
  const { days, types, branch } = props;
  const today = todayVn();
  const [day, setDay] = useState(() => (days.includes(today) ? today : days[0]));
  const selected = days.includes(day) ? day : days[0];
  const index = days.indexOf(selected);

  return (
    <div>
      <style>{css}</style>
      <Segmented
        block
        value={selected}
        onChange={(v) => setDay(v as string)}
        options={days.map((d, i) => ({
          value: d,
          label: (
            <div style={{ lineHeight: 1.2, padding: "4px 0" }}>
              <div style={{ fontSize: 11 }}>{weekdaysShort[i]}</div>
              <div style={{ fontWeight: d === today ? 700 : 500 }}>{dayjs(d).format("DD")}</div>
            </div>
          ),
        }))}
      />
      <div style={{ marginTop: 12 }}>
        <DayHeader date={selected} index={index} today={today} count={props.shifts.filter((s) => s.date === selected).length} />
      </div>
      <Space direction="vertical" size={8} style={{ width: "100%", marginTop: 8 }}>
        {types.map((type) => (
          <div key={type}>
            <RowHeader type={type} branch={branch} compact />
            <SlotCell {...props} date={selected} dayIndex={index} type={type} last />
          </div>
        ))}
      </Space>
      <Legend />
    </div>
  );
}

function DayHeader({ date, index, today, count }: { date: string; index: number; today: string; count: number }) {
  const { token } = theme.useToken();
  const c = dayColors(token, index);
  const isToday = date === today;
  return (
    <div style={{
      background: isToday ? c.soft : c.tint, borderTop: `4px solid ${c.accent}`, borderRadius: "10px 10px 4px 4px",
      padding: "8px 10px", opacity: date < today ? 0.6 : 1,
    }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <span style={{ color: c.text, fontWeight: 600 }}>{weekdays[index]}</span>
        {isToday && <span style={{ background: c.accent, color: "#fff", borderRadius: 10, padding: "0 8px", fontSize: 11 }}>Hôm nay</span>}
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
        <span style={{ fontSize: 20, fontWeight: 700, color: token.colorText }}>{dayMonth(date)}</span>
        <span style={{ fontSize: 12, color: token.colorTextSecondary }}>{count} ca</span>
      </div>
    </div>
  );
}

function RowHeader({ type, branch, compact }: { type: ShiftType; branch: PublicBranch | undefined; compact?: boolean }) {
  const { token } = theme.useToken();
  const [s, e] = slotHours(type, branch);
  return (
    <div style={{
      display: "flex", alignItems: "center", gap: 10, padding: compact ? "0 0 4px" : "8px 10px",
      background: compact ? undefined : token.colorFillQuaternary, borderRadius: 8,
    }}>
      <span style={{
        width: 32, height: 32, borderRadius: 16, display: "inline-flex", alignItems: "center", justifyContent: "center",
        background: token.colorFillSecondary, color: token.colorText, fontSize: 16, flexShrink: 0,
      }}>{shiftTypeIcons[type]}</span>
      <div style={{ lineHeight: 1.25 }}>
        <div style={{ fontWeight: 600 }}>{shiftTypeLabels[type]}</div>
        <div style={{ fontSize: 12, color: token.colorTextSecondary }}>{s}–{e}</div>
      </div>
    </div>
  );
}

function SlotCell(props: GridProps & { date: string; dayIndex: number; type: ShiftType; last: boolean }) {
  const { token } = theme.useToken();
  const { date, dayIndex, type, shifts, highlight, last } = props;
  const c = dayColors(token, dayIndex);
  const today = todayVn();
  const here = shifts.filter((s) => s.date === date && s.type === type).sort((a, b) => a.startAt.localeCompare(b.startAt));

  return (
    <div style={{
      background: date === today ? c.soft : c.tint, padding: 6, minHeight: 96,
      borderRadius: last ? "4px 4px 10px 10px" : 4, display: "flex", flexDirection: "column", gap: 6,
    }}>
      {here.length > 0
        ? here.map((s) => (
          <ShiftTile key={s.id} shift={s} accent={c.accent}
            dim={!!highlight && !matches(s, highlight)} glow={!!highlight && matches(s, highlight)}
            onClick={() => props.onOpenShift(s.id)} />
        ))
        : <EmptySlot {...props} accent={c.accent} past={date < today} />}
    </div>
  );
}

function EmptySlot({ branchId, branch, date, type, accent, past, onCreated }: GridProps & { date: string; type: ShiftType; accent: string; past: boolean }) {
  const { token } = theme.useToken();
  const [open, setOpen] = useState(false);
  const base: CSSProperties = {
    flex: 1, minHeight: 84, borderRadius: 8, border: `1px dashed ${token.colorBorder}`, display: "flex",
    alignItems: "center", justifyContent: "center", gap: 6, color: token.colorTextTertiary, fontSize: 13,
  };
  if (past) return <div style={{ ...base, opacity: 0.5 }}>—</div>;

  return (
    <Popover
      trigger="click" open={open} onOpenChange={setOpen} destroyTooltipOnHide placement="bottom"
      title={<Space>{shiftTypeIcons[type]}{shiftTypeLabels[type]} · {weekdays[(dayjs(date).day() + 6) % 7]} {dayMonth(date)}</Space>}
      content={<ActivateForm branchId={branchId} branch={branch} date={date} type={type} onDone={() => { setOpen(false); onCreated(); }} />}
    >
      <div className="cr-slot-empty" role="button" tabIndex={0}
        style={{ ...base, cursor: "pointer", ["--cr-accent" as string]: accent } as CSSProperties}
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") setOpen(true); }}>
        <PlusOutlined /> Kích hoạt
      </div>
    </Popover>
  );
}

function ActivateForm({ branchId, branch, date, type, onDone }: { branchId: string; branch: PublicBranch | undefined; date: string; type: ShiftType; onDone: () => void }) {
  const notify = useNotify();
  const { token } = theme.useToken();
  const [defaultStart, defaultEnd] = slotHours(type, branch);
  const [start, setStart] = useState(defaultStart);
  const [end, setEnd] = useState(defaultEnd);
  const [custom, setCustom] = useState(false);
  const [requiredStaff, setRequiredStaff] = useState(1);
  const [isOpen, setIsOpen] = useState(false);

  const create = useMutation({
    mutationFn: () => api.shifts.create({ branchId, date, type, ...shiftTimes(date, start, end), requiredStaff, isOpen }),
    onSuccess: (s) => {
      notify.ok(s.status === "Draft" ? "Đã kích hoạt ca (nháp, chưa công bố)." : "Đã kích hoạt ca và báo cho nhân viên.");
      onDone();
    },
    onError: notify.fail,
  });

  const row: CSSProperties = { display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12 };
  return (
    <div style={{ width: 270 }}>
      <Space direction="vertical" size={10} style={{ width: "100%" }}>
        {custom ? (
          <Space>
            <TimePicker size="small" format="HH:mm" minuteStep={15} allowClear={false} value={dayjs(start, "HH:mm")} onChange={(v) => v && setStart(v.format("HH:mm"))} />
            –
            <TimePicker size="small" format="HH:mm" minuteStep={15} allowClear={false} value={dayjs(end, "HH:mm")} onChange={(v) => v && setEnd(v.format("HH:mm"))} />
          </Space>
        ) : (
          <div style={row}>
            <Space size={6}><ClockCircleOutlined style={{ color: token.colorTextSecondary }} /><b>{start}–{end}</b>{end <= start && <Typography.Text type="secondary">(hôm sau)</Typography.Text>}</Space>
            <Button size="small" type="link" onClick={() => setCustom(true)}>Đổi giờ</Button>
          </div>
        )}
        <div style={row}>
          <span>Số người tối thiểu</span>
          <InputNumber size="small" min={1} max={20} value={requiredStaff} onChange={(v) => setRequiredStaff(v ?? 1)} style={{ width: 64 }} />
        </div>
        <div style={row}>
          <Tooltip title="Nhân viên tự nhận ca sau khi lịch được công bố."><span>Ca trống (nhân viên tự nhận)</span></Tooltip>
          <Switch size="small" checked={isOpen} onChange={setIsOpen} />
        </div>
      </Space>
      <Divider style={{ margin: "12px 0" }} />
      <div style={row}>
        <b>Kích hoạt</b>
        <Switch checked={create.isPending || create.isSuccess} loading={create.isPending} onChange={(on) => on && create.mutate()} />
      </div>
    </div>
  );
}

function statusColor(token: ReturnType<typeof theme.useToken>["token"], status: AssignmentStatus) {
  return status === "Accepted" ? token.colorSuccess : status === "Offered" ? token.colorWarning : token.colorError;
}

function ShiftTile({ shift, accent, dim, glow, onClick }: { shift: ShiftView; accent: string; dim: boolean; glow: boolean; onClick: () => void }) {
  const { token } = theme.useToken();
  const draft = shift.status === "Draft";
  const ratio = Math.min(1, shift.acceptedCount / Math.max(1, shift.requiredStaff));
  const barColor = shift.understaffed ? token.colorWarning : token.colorSuccess;

  return (
    <div className="cr-shift-tile" role="button" tabIndex={0} onClick={onClick} onKeyDown={(e) => { if (e.key === "Enter") onClick(); }}
      style={{
        background: token.colorBgContainer, borderRadius: 8, padding: "6px 8px", cursor: "pointer",
        border: `1px ${draft ? "dashed" : "solid"} ${shift.understaffed ? token.colorWarningBorder : token.colorBorderSecondary}`,
        borderLeft: `4px solid ${accent}`, opacity: dim ? 0.35 : 1,
        boxShadow: glow ? `0 0 0 2px ${token.colorPrimary}` : token.boxShadowTertiary,
      }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 4 }}>
        <span style={{ fontWeight: 600, fontSize: 13, whiteSpace: "nowrap" }}>{time(shift.startAt)}–{time(shift.endAt)}</span>
        <Space size={6} style={{ fontSize: 13 }}>
          {shift.phase === "InProgress" && <Tooltip title="Đang diễn ra"><Badge status="processing" color={token.colorSuccess} /></Tooltip>}
          {shift.isOpen && (
            <Tooltip title={draft ? "Ca trống · nhân viên chỉ thấy sau khi công bố" : "Ca trống · nhân viên tự nhận"}>
              <UserAddOutlined style={{ color: token.colorWarning }} />
            </Tooltip>
          )}
          {draft && <Tooltip title="Nháp · chưa công bố"><EditOutlined style={{ color: token.colorTextTertiary }} /></Tooltip>}
        </Space>
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 4, minHeight: 22 }}>
        {shift.assignments.length === 0 && <Typography.Text type="secondary" style={{ fontSize: 12 }}>Chưa có người</Typography.Text>}
        {shift.assignments.map((a) => (
          <Tooltip key={a.id} title={`${cleanName(a.employeeName)} · ${assignmentLabels[a.status].label}`}>
            <span style={{
              display: "inline-flex", alignItems: "center", gap: 4, padding: "1px 6px 1px 1px", borderRadius: 12,
              background: token.colorFillTertiary, fontSize: 12, maxWidth: "100%",
              textDecoration: a.status === "Declined" ? "line-through" : undefined, opacity: a.status === "Declined" ? 0.6 : 1,
            }}>
              <Avatar size={18} style={{ background: statusColor(token, a.status), fontSize: 10, flexShrink: 0 }}>{givenName(a.employeeName).charAt(0).toUpperCase()}</Avatar>
              <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{givenName(a.employeeName)}</span>
            </span>
          </Tooltip>
        ))}
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 6 }}>
        <div style={{ flex: 1, height: 4, borderRadius: 2, background: token.colorFillSecondary, overflow: "hidden" }}>
          <div style={{ width: `${ratio * 100}%`, height: "100%", background: barColor }} />
        </div>
        <span style={{ fontSize: 11, color: shift.understaffed ? token.colorWarningText : token.colorTextSecondary }}>{shift.acceptedCount}/{shift.requiredStaff}</span>
      </div>
      {(shift.phase === "Closed" || shift.phase === "Approved") && (
        <Tag bordered={false} color={phaseLabels[shift.phase].color} style={{ margin: "6px 0 0", fontSize: 11, lineHeight: "16px", paddingInline: 6 }}>{phaseLabels[shift.phase].label}</Tag>
      )}
    </div>
  );
}

function Legend() {
  const { token } = theme.useToken();
  const dot = (color: string) => <span style={{ display: "inline-block", width: 10, height: 10, borderRadius: 5, background: color, marginRight: 4 }} />;
  const item: CSSProperties = { display: "inline-flex", alignItems: "center", fontSize: 12, color: token.colorTextSecondary };
  return (
    <div style={{ display: "flex", flexWrap: "wrap", gap: "6px 16px", marginTop: 12 }}>
      <span style={item}>{dot(token.colorSuccess)}Đã nhận</span>
      <span style={item}>{dot(token.colorWarning)}Chờ phản hồi</span>
      <span style={item}>{dot(token.colorError)}Từ chối</span>
      <span style={item}><EditOutlined style={{ marginRight: 4 }} />Nháp (viền nét đứt)</span>
      <span style={item}><UserAddOutlined style={{ marginRight: 4, color: token.colorWarning }} />Ca trống</span>
      <span style={item}><span style={{ width: 16, height: 4, background: token.colorWarning, marginRight: 4, borderRadius: 2 }} />Thiếu người</span>
    </div>
  );
}
