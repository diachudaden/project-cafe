import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Card, Col, Drawer, Empty, Form, Input, InputNumber, List, Modal, Popconfirm, Row, Select, Space, Switch, Tag, TimePicker, Typography } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import { ApiError, type RuleViolation, type ShiftType, type ShiftView } from "@cafecr/api-client";
import { addDays, assignmentLabels, dayMonth, phaseLabels, shiftTypeDefaults, shiftTypeLabels, time, todayVn, vn, vnIso, weekStart } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

const weekdays = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật"];

export function SchedulePage() {
  return <RequireBranch title="Lịch ca">{(branchId) => <Schedule branchId={branchId} />}</RequireBranch>;
}

function Schedule({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [week, setWeek] = useState(() => weekStart(todayVn()));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [creating, setCreating] = useState<string | null>(null);
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

  const days = Array.from({ length: 7 }, (_, i) => addDays(week, i));
  const drafts = (shifts.data ?? []).filter((s) => s.status === "Draft").length;
  const selected = shifts.data?.find((s) => s.id === selectedId) ?? null;

  return (
    <Card
      title={`Lịch ca · tuần ${dayMonth(week)} – ${dayMonth(to)}`}
      extra={
        <Space wrap>
          <Button onClick={() => setWeek(addDays(week, -7))}>Tuần trước</Button>
          <Button onClick={() => setWeek(weekStart(todayVn()))}>Tuần này</Button>
          <Button onClick={() => setWeek(addDays(week, 7))}>Tuần sau</Button>
          <Popconfirm title="Sao chép lịch tuần trước sang tuần này?" onConfirm={() => copy.mutate()}>
            <Button loading={copy.isPending}>Sao chép tuần trước</Button>
          </Popconfirm>
          <Button type="primary" disabled={drafts === 0} loading={publish.isPending} onClick={() => publish.mutate()}>
            Công bố tuần ({drafts} nháp)
          </Button>
        </Space>
      }
    >
      {shifts.isError && <Alert type="error" message={shifts.error.message} style={{ marginBottom: 12 }} />}
      <div style={{ overflowX: "auto" }}>
        <Row gutter={8} wrap={false} style={{ minWidth: 1100 }}>
          {days.map((date, i) => (
            <Col key={date} flex="1 1 0">
              <Space style={{ width: "100%", justifyContent: "space-between", marginBottom: 8 }}>
                <Typography.Text strong={date === todayVn()}>{weekdays[i]} {dayMonth(date)}</Typography.Text>
                <Button size="small" onClick={() => setCreating(date)}>+</Button>
              </Space>
              <Space direction="vertical" style={{ width: "100%" }}>
                {(shifts.data ?? []).filter((s) => s.date === date).map((s) => (
                  <ShiftCard key={s.id} shift={s} onClick={() => setSelectedId(s.id)} />
                ))}
              </Space>
            </Col>
          ))}
        </Row>
      </div>
      {creating && <CreateShiftModal branchId={branchId} date={creating} onClose={() => setCreating(null)} onCreated={() => void refresh()} />}
      <ShiftDrawer branchId={branchId} shift={selected} onClose={() => setSelectedId(null)} onChanged={() => void refresh()} />
    </Card>
  );
}

function ShiftCard({ shift, onClick }: { shift: ShiftView; onClick: () => void }) {
  const phase = phaseLabels[shift.phase];
  // Solid border = everyone accepted; dashed = waiting for answers or an open spot (Teams-style).
  const settled = !shift.understaffed && shift.assignments.every((a) => a.status !== "Offered");
  return (
    <Card
      size="small"
      hoverable
      onClick={onClick}
      style={{ border: `1px ${settled ? "solid" : "dashed"} ${shift.understaffed ? "#fa8c16" : "#d9d9d9"}` }}
      styles={{ body: { padding: 8 } }}
    >
      <Typography.Text strong>{shiftTypeLabels[shift.type]}</Typography.Text> <Typography.Text type="secondary">{time(shift.startAt)}–{time(shift.endAt)}</Typography.Text>
      <div><Tag color={phase.color}>{phase.label}</Tag>{shift.isOpen && <Tag color="orange">Ca trống</Tag>}</div>
      {shift.assignments.map((a) => (
        <div key={a.id} style={{ fontSize: 12 }}>
          <Tag color={assignmentLabels[a.status].color} style={{ marginRight: 4 }}>{assignmentLabels[a.status].label}</Tag>{a.employeeName.replace("[DEMO DATA] ", "")}
        </div>
      ))}
      {shift.understaffed && <Typography.Text type="warning" style={{ fontSize: 12 }}>Thiếu người: {shift.acceptedCount}/{shift.requiredStaff}</Typography.Text>}
    </Card>
  );
}

interface ShiftForm { type: ShiftType; start: Dayjs; end: Dayjs; requiredStaff: number; isOpen: boolean }

function toTimes(date: string, f: ShiftForm) {
  const startAt = vnIso(date, f.start.format("HH:mm"));
  // An end time at or before the start means the shift ends the next day (night shift).
  const endDate = f.end.format("HH:mm") <= f.start.format("HH:mm") ? addDays(date, 1) : date;
  return { startAt, endAt: vnIso(endDate, f.end.format("HH:mm")) };
}

function CreateShiftModal({ branchId, date, onClose, onCreated }: { branchId: string; date: string; onClose: () => void; onCreated: () => void }) {
  const notify = useNotify();
  const [form] = Form.useForm<ShiftForm>();
  const create = useMutation({
    mutationFn: (f: ShiftForm) => api.shifts.create({ branchId, date, type: f.type, ...toTimes(date, f), requiredStaff: f.requiredStaff, isOpen: f.isOpen }),
    onSuccess: () => { notify.ok("Đã tạo ca nháp."); onCreated(); onClose(); },
    onError: notify.fail,
  });
  const setType = (type: ShiftType) => {
    const [s, e] = shiftTypeDefaults[type];
    form.setFieldsValue({ start: dayjs(s, "HH:mm"), end: dayjs(e, "HH:mm") });
  };

  return (
    <Modal open title={`Tạo ca ngày ${dayMonth(date)}`} okText="Tạo" cancelText="Hủy" onCancel={onClose} onOk={() => form.submit()} confirmLoading={create.isPending}>
      <Form form={form} layout="vertical" onFinish={(f) => create.mutate(f)}
        initialValues={{ type: "Morning", start: dayjs("06:00", "HH:mm"), end: dayjs("14:00", "HH:mm"), requiredStaff: 1, isOpen: false }}>
        <Form.Item label="Loại ca" name="type">
          <Select onChange={setType} options={(Object.keys(shiftTypeLabels) as ShiftType[]).map((t) => ({ value: t, label: shiftTypeLabels[t] }))} />
        </Form.Item>
        <Space>
          <Form.Item label="Bắt đầu" name="start" rules={[{ required: true }]}><TimePicker format="HH:mm" minuteStep={15} /></Form.Item>
          <Form.Item label="Kết thúc" name="end" rules={[{ required: true }]}><TimePicker format="HH:mm" minuteStep={15} /></Form.Item>
        </Space>
        <Form.Item label="Số người tối thiểu" name="requiredStaff"><InputNumber min={1} max={20} /></Form.Item>
        <Form.Item label="Ca trống (nhân viên tự nhận)" name="isOpen" valuePropName="checked"><Switch /></Form.Item>
      </Form>
    </Modal>
  );
}

function ShiftDrawer({ branchId, shift, onClose, onChanged }: { branchId: string; shift: ShiftView | null; onClose: () => void; onChanged: () => void }) {
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
  const remove = useMutation({ mutationFn: () => api.shifts.remove(shift!.id), onSuccess: () => { notify.ok("Đã xóa ca."); onClose(); onChanged(); }, onError: notify.fail });

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

  const assigned = new Set(shift?.assignments.filter((a) => a.status !== "Declined").map((a) => a.employeeId));
  const blocked = violations.some((v) => v.isBlocking) && !(needsReason && reason.trim());

  return (
    <Drawer open={!!shift} onClose={() => { reset(); onClose(); }} width={420}
      title={shift && `${shiftTypeLabels[shift.type]} ${dayMonth(shift.date)} · ${time(shift.startAt)}–${time(shift.endAt)}`}
      extra={shift && <Popconfirm title="Xóa ca này?" onConfirm={() => remove.mutate()}><Button danger size="small">Xóa ca</Button></Popconfirm>}>
      {shift && (
        <Space direction="vertical" style={{ width: "100%" }}>
          <div><Tag color={phaseLabels[shift.phase].color}>{phaseLabels[shift.phase].label}</Tag> Đã nhận {shift.acceptedCount}/{shift.requiredStaff}</div>
          <List
            header="Phân công"
            dataSource={shift.assignments}
            locale={{ emptyText: <Empty description="Chưa giao cho ai." /> }}
            renderItem={(a) => (
              <List.Item actions={[<Button key="x" size="small" type="link" danger onClick={() => unassign.mutate(a.id)}>Gỡ</Button>]}>
                <List.Item.Meta
                  title={<>{a.employeeName} <Tag color={assignmentLabels[a.status].color}>{assignmentLabels[a.status].label}</Tag></>}
                  description={[a.declineReason && `Lý do từ chối: ${a.declineReason}`, a.overrideReason && `Bỏ qua quy tắc: ${a.overrideReason}`].filter(Boolean).join(" · ") || undefined}
                />
              </List.Item>
            )}
          />
          <Typography.Text strong>Giao thêm</Typography.Text>
          <Select
            showSearch optionFilterProp="label" placeholder="Chọn nhân viên" value={employeeId} onChange={(v) => void check(v)} style={{ width: "100%" }}
            options={(employees.data ?? []).filter((e) => !assigned.has(e.employeeId)).map((e) => ({ value: e.employeeId, label: e.fullName }))}
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
