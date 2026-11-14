import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Alert, Button, Drawer, Empty, Input, List, Popconfirm, Select, Space, Switch, Tag, Typography } from "antd";
import { ApiError, type RuleViolation, type ShiftView } from "@cafecr/api-client";
import { assignmentLabels, dayMonth, phaseLabels, shiftTypeLabels, time, vn } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../../components/useNotify";
import { cleanName } from "./slots";

export function ShiftDrawer({ branchId, shift, onClose, onChanged }: { branchId: string; shift: ShiftView | null; onClose: () => void; onChanged: () => void }) {
  const notify = useNotify();
  const [employeeId, setEmployeeId] = useState<string>();
  const [violations, setViolations] = useState<RuleViolation[]>([]);
  const [needsReason, setNeedsReason] = useState(false);
  const [reason, setReason] = useState("");
  const employees = useQuery({ queryKey: ["employees", branchId], queryFn: () => api.workforce.employees(branchId), enabled: !!shift });

  const reset = () => { setEmployeeId(undefined); setViolations([]); setNeedsReason(false); setReason(""); };
  const assign = useMutation({
    mutationFn: () => api.shifts.assign(shift!.id, employeeId!, needsReason ? reason : undefined),
    onSuccess: () => { notify.ok("Đã giao ca."); reset(); onChanged(); },
    onError: (e) => {
      if (e instanceof ApiError && e.violations.length) {
        setViolations(e.violations);
        setNeedsReason(e.violations.filter((v) => v.isBlocking).every((v) => v.canOverride));
      } else notify.fail(e);
    },
  });
  const unassign = useMutation({ mutationFn: api.shifts.unassign, onSuccess: () => { notify.ok("Đã gỡ."); onChanged(); }, onError: notify.fail });
  // "Tắt kích hoạt" = delete the shift; the API refuses shifts that already have attendance or bills.
  const deactivate = useMutation({
    mutationFn: () => api.shifts.remove(shift!.id),
    onSuccess: () => { notify.ok("Đã tắt ca."); reset(); onClose(); onChanged(); },
    onError: notify.fail,
  });

  // Dry-run the rules as soon as someone is picked, like Teams warning about a clash.
  const check = async (id: string) => {
    setEmployeeId(id);
    try {
      const r = await api.shifts.validate(shift!.id, id);
      setViolations(r.violations);
      setNeedsReason(r.needsOverrideReason);
    } catch (e) {
      notify.fail(e);
    }
  };

  const active = shift?.assignments.filter((a) => a.status !== "Declined") ?? [];
  const assigned = new Set(active.map((a) => a.employeeId));
  const blocked = violations.some((v) => v.isBlocking) && !(needsReason && reason.trim());
  const notified = shift?.status === "Published" && active.length > 0;

  return (
    <Drawer open={!!shift} onClose={() => { reset(); onClose(); }} width={420}
      title={shift && `${shiftTypeLabels[shift.type]} ${dayMonth(shift.date)} · ${time(shift.startAt)}–${time(shift.endAt)}`}
      extra={shift && (
        <Popconfirm
          title="Tắt ca này?"
          description={notified
            ? <>Ca sẽ bị xóa và báo hủy cho: {active.map((a) => cleanName(a.employeeName)).join(", ")}.</>
            : "Ca sẽ bị xóa khỏi lịch."}
          okText="Tắt ca" okButtonProps={{ danger: true }} cancelText="Giữ"
          onConfirm={() => deactivate.mutate()}
        >
          <Space size={6} style={{ cursor: "pointer" }}>
            <Typography.Text type="secondary">Kích hoạt</Typography.Text>
            <Switch checked loading={deactivate.isPending} />
          </Space>
        </Popconfirm>
      )}>
      {shift && (
        <Space direction="vertical" style={{ width: "100%" }}>
          <div>
            <Tag color={phaseLabels[shift.phase].color}>{phaseLabels[shift.phase].label}</Tag>
            {shift.isOpen && <Tag color="orange">Ca trống</Tag>}
            Đã nhận {shift.acceptedCount}/{shift.requiredStaff}
          </div>
          <List
            header="Phân công"
            dataSource={shift.assignments}
            locale={{ emptyText: <Empty description="Chưa giao cho ai." /> }}
            renderItem={(a) => (
              <List.Item actions={[<Button key="x" size="small" type="link" danger onClick={() => unassign.mutate(a.id)}>Gỡ</Button>]}>
                <List.Item.Meta
                  title={<>{cleanName(a.employeeName)} <Tag color={assignmentLabels[a.status].color}>{assignmentLabels[a.status].label}</Tag></>}
                  description={[a.declineReason && `Lý do từ chối: ${a.declineReason}`, a.overrideReason && `Bỏ qua quy tắc: ${a.overrideReason}`].filter(Boolean).join(" · ") || undefined}
                />
              </List.Item>
            )}
          />
          <Typography.Text strong>Giao thêm</Typography.Text>
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn nhân viên" value={employeeId} onChange={(v) => void check(v)} style={{ width: "100%" }}
            options={(employees.data ?? []).filter((e) => !assigned.has(e.employeeId)).map((e) => ({ value: e.employeeId, label: cleanName(e.fullName) }))}
          />
          {violations.map((v, i) => (
            <Alert key={i} showIcon type={v.isBlocking ? (v.canOverride ? "warning" : "error") : "info"} message={v.message} />
          ))}
          {needsReason && <Input.TextArea placeholder="Lý do bỏ qua quy tắc (bắt buộc)" value={reason} onChange={(e) => setReason(e.target.value)} />}
          <Button type="primary" block disabled={!employeeId || blocked} loading={assign.isPending} onClick={() => assign.mutate()}>Giao ca</Button>
          {shift.status === "Draft" && <Typography.Text type="secondary">Ca nháp: nhân viên chỉ nhận thông báo khi bạn bấm "Công bố tuần".</Typography.Text>}
          <Typography.Text type="secondary">Giờ hiển thị theo giờ Việt Nam ({vn(shift.startAt).format("DD/MM/YYYY")}).</Typography.Text>
        </Space>
      )}
    </Drawer>
  );
}
