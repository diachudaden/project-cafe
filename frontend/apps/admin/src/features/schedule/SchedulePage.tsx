import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Card, Grid, List, Modal, Popconfirm, Skeleton, Space, Switch, Tag, Typography, theme } from "antd";
import { CopyOutlined, LeftOutlined, NotificationOutlined, RightOutlined } from "@ant-design/icons";
import { addDays, dayMonth, shiftTypeLabels, todayVn, weekStart } from "@cafecr/ui";
import { api } from "../../api";
import { useBranch } from "../../branch/BranchContext";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";
import { ScheduleDayView, ScheduleGrid } from "./ScheduleGrid";
import { ShiftDrawer } from "./ShiftDrawer";
import { matches, visibleShiftTypes, type Highlight } from "./slots";

export function SchedulePage() {
  return <RequireBranch title="Lịch ca">{(branchId) => <Schedule branchId={branchId} />}</RequireBranch>;
}

const highlights: { key: Highlight; label: string }[] = [
  { key: "draft", label: "Nháp" },
  { key: "understaffed", label: "Thiếu người" },
  { key: "open", label: "Ca trống" },
  { key: "waiting", label: "Chờ phản hồi" },
];

function Schedule({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const { token } = theme.useToken();
  const screens = Grid.useBreakpoint();
  const queryClient = useQueryClient();
  const { branches } = useBranch();
  const branch = branches.find((b) => b.id === branchId);
  const [week, setWeek] = useState(() => weekStart(todayVn()));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [showAll, setShowAll] = useState(false);
  const [highlight, setHighlight] = useState<Highlight | null>(null);
  const to = addDays(week, 6);
  const shifts = useQuery({ queryKey: ["shifts", branchId, week], queryFn: () => api.shifts.list(branchId, week, to) });
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["shifts", branchId] });

  const publish = useMutation({
    mutationFn: () => api.shifts.publish(branchId, week),
    onSuccess: (r) => { notify.ok(`Đã công bố ${r.published} ca, báo cho ${r.notifiedEmployees} nhân viên.`); void refresh(); },
    onError: notify.fail,
  });
  const copy = useMutation({
    mutationFn: () => api.shifts.copyWeek(branchId, addDays(week, -7), week, true),
    onSuccess: (r) => {
      Modal.info({
        title: `Đã sao chép ${r.created} ca (bỏ qua ${r.skipped})`,
        content: r.conflicts.length === 0 ? "Không có xung đột." : (
          <List size="small" dataSource={r.conflicts} renderItem={(c) => (
            <List.Item>{dayMonth(c.date)} {shiftTypeLabels[c.type]} – {c.employeeName}: {c.violations.map((v) => v.message).join(" ")}</List.Item>
          )} />
        ),
      });
      void refresh();
    },
    onError: notify.fail,
  });

  const list = shifts.data ?? [];
  const days = Array.from({ length: 7 }, (_, i) => addDays(week, i));
  const types = visibleShiftTypes(branch, list, showAll);
  const drafts = list.filter((s) => s.status === "Draft").length;
  const selected = list.find((s) => s.id === selectedId) ?? null;
  const isThisWeek = week === weekStart(todayVn());
  const gridProps = {
    branchId, branch, days, types, shifts: list, highlight,
    onOpenShift: setSelectedId, onCreated: () => void refresh(),
  };

  const mobile = screens.md === false;
  const toolbar = (
        <Space wrap>
          <Space.Compact>
            <Button icon={<LeftOutlined />} onClick={() => setWeek(addDays(week, -7))} aria-label="Tuần trước" />
            <Button type={isThisWeek ? "primary" : "default"} ghost={isThisWeek} onClick={() => setWeek(weekStart(todayVn()))}>
              {dayMonth(week)} – {dayMonth(to)}
            </Button>
            <Button icon={<RightOutlined />} onClick={() => setWeek(addDays(week, 7))} aria-label="Tuần sau" />
          </Space.Compact>
          <Popconfirm title="Sao chép lịch tuần trước sang tuần này?" onConfirm={() => copy.mutate()}>
            <Button icon={<CopyOutlined />} loading={copy.isPending}>Sao chép tuần trước</Button>
          </Popconfirm>
          <Button type="primary" icon={<NotificationOutlined />} disabled={drafts === 0} loading={publish.isPending} onClick={() => publish.mutate()}>
            Công bố tuần ({drafts} nháp)
          </Button>
        </Space>
  );

  return (
    <Card
      title={
        <div style={{ lineHeight: 1.3, padding: "8px 0" }}>
          <div>Lịch ca</div>
          <Typography.Text type="secondary" style={{ fontSize: 13, fontWeight: 400 }}>
            {branch?.name ?? ""}{branch ? (branch.is24h ? " · 24/7" : ` · ${branch.openTime.slice(0, 5)}–${branch.closeTime.slice(0, 5)}`) : ""}
          </Typography.Text>
        </div>
      }
      extra={mobile ? undefined : toolbar}
    >
      {mobile && <div style={{ marginBottom: 12 }}>{toolbar}</div>}
      {shifts.isError && <Alert type="error" message={shifts.error.message} style={{ marginBottom: 12 }} />}

      <div style={{ display: "flex", flexWrap: "wrap", justifyContent: "space-between", alignItems: "center", gap: 8, marginBottom: 12 }}>
        <Space size={[4, 6]} wrap>
          <Typography.Text strong style={{ marginRight: 4 }}>{list.length} ca</Typography.Text>
          {highlights.map((h) => {
            const n = list.filter((s) => matches(s, h.key)).length;
            return (
              <Tag.CheckableTag key={h.key} checked={highlight === h.key}
                onChange={(on) => setHighlight(on ? h.key : null)}
                style={{ border: `1px solid ${highlight === h.key ? token.colorPrimary : token.colorBorderSecondary}`, opacity: n === 0 ? 0.5 : 1 }}>
                {h.label} · {n}
              </Tag.CheckableTag>
            );
          })}
        </Space>
        <Space size={6}>
          <Switch size="small" checked={showAll} onChange={setShowAll} />
          <Typography.Text type="secondary">Hiện tất cả loại ca</Typography.Text>
        </Space>
      </div>

      {shifts.isLoading ? <Skeleton active paragraph={{ rows: 8 }} />
        : mobile ? <ScheduleDayView {...gridProps} /> : <ScheduleGrid {...gridProps} />}

      <ShiftDrawer branchId={branchId} shift={selected} onClose={() => setSelectedId(null)} onChanged={() => void refresh()} />
    </Card>
  );
}
