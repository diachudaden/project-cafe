import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Checkbox, Col, Empty, Form, Input, List, Modal, Popconfirm, Row, Select, Space, Tag, TimePicker, Typography } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import type { AvailabilitySlot, MyShift, ShiftRequestType } from "@cafecr/api-client";
import {
  addDays, assignmentLabels, dayMonth, dayOfWeekLabels, phaseLabels, requestStatusLabels, requestTypeLabels, shiftTypeLabels, time, todayVn,
} from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";

export function SchedulePage() {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const today = todayVn();
  const mine = useQuery({ queryKey: ["my-shifts", today], queryFn: () => api.shifts.mine(addDays(today, -1), addDays(today, 13)) });
  const open = useQuery({ queryKey: ["open-shifts"], queryFn: api.shifts.open });
  const requests = useQuery({ queryKey: ["my-requests"], queryFn: api.workforce.myRequests });
  const refresh = () => void queryClient.invalidateQueries();
  const [declining, setDeclining] = useState<MyShift | null>(null);
  const [requesting, setRequesting] = useState<MyShift | null>(null);
  const [reason, setReason] = useState("");

  const accept = useMutation({ mutationFn: api.shifts.accept, onSuccess: () => { notify.ok("Đã nhận ca."); refresh(); }, onError: notify.fail });
  const decline = useMutation({
    mutationFn: () => api.shifts.decline(declining!.assignmentId, reason),
    onSuccess: () => { notify.ok("Đã từ chối ca; quản lý đã được báo."); setDeclining(null); setReason(""); refresh(); },
    onError: notify.fail,
  });
  const claim = useMutation({ mutationFn: api.shifts.claim, onSuccess: () => { notify.ok("Đã nhận ca trống."); refresh(); }, onError: notify.fail });
  const cancel = useMutation({ mutationFn: api.workforce.cancelRequest, onSuccess: refresh, onError: notify.fail });

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={14}>
        <Card title="Ca của tôi (2 tuần)">
          <List
            dataSource={mine.data}
            locale={{ emptyText: <Empty description="Chưa có ca nào được giao." /> }}
            renderItem={(s) => (
              <List.Item actions={[
                s.assignmentStatus === "Offered" && <Button key="a" type="primary" onClick={() => accept.mutate(s.assignmentId)}>Nhận ca</Button>,
                s.assignmentStatus === "Offered" && <Button key="d" onClick={() => setDeclining(s)}>Từ chối</Button>,
                s.assignmentStatus === "Accepted" && !s.checkInAt && s.date > today && <Button key="r" onClick={() => setRequesting(s)}>Đổi / nghỉ</Button>,
              ].filter(Boolean)}>
                <List.Item.Meta
                  title={<>{dayMonth(s.date)} · {shiftTypeLabels[s.type]} {time(s.startAt)}–{time(s.endAt)} <Tag color={assignmentLabels[s.assignmentStatus].color}>{assignmentLabels[s.assignmentStatus].label}</Tag>
                    {s.phase !== "Published" && <Tag color={phaseLabels[s.phase].color}>{phaseLabels[s.phase].label}</Tag>}</>}
                  description={<>{s.branchName} · {s.branchAddress}{s.colleagues.length > 0 && <><br />Cùng ca: {s.colleagues.map((c) => c.name).join(", ")}</>}</>}
                />
              </List.Item>
            )}
          />
        </Card>
      </Col>
      <Col xs={24} lg={10}>
        <Space direction="vertical" style={{ width: "100%" }} size="middle">
          <Card title="Ca trống có thể nhận">
            <List size="small" dataSource={open.data} locale={{ emptyText: "Không có ca trống." }}
              renderItem={(s) => (
                <List.Item actions={[<Button key="c" size="middle" type="primary" loading={claim.isPending} onClick={() => claim.mutate(s.shiftId)}>Nhận</Button>]}>
                  {dayMonth(s.date)} {shiftTypeLabels[s.type]} {time(s.startAt)}–{time(s.endAt)} · {s.branchName.replace("[DEMO DATA] ", "")} ({s.acceptedCount}/{s.requiredStaff})
                </List.Item>
              )} />
          </Card>
          <Card title="Đơn của tôi">
            <List size="small" dataSource={requests.data} locale={{ emptyText: "Chưa có đơn." }}
              renderItem={(r) => (
                <List.Item actions={[r.status === "Pending" && <Popconfirm key="x" title="Hủy đơn?" onConfirm={() => cancel.mutate(r.id)}><Button size="small">Hủy</Button></Popconfirm>].filter(Boolean)}>
                  {requestTypeLabels[r.type]} {dayMonth(r.date)} {shiftTypeLabels[r.shiftType]}{r.targetName ? ` → ${r.targetName}` : ""} <Tag color={requestStatusLabels[r.status].color}>{requestStatusLabels[r.status].label}</Tag>
                </List.Item>
              )} />
          </Card>
          <AvailabilityCard />
        </Space>
      </Col>
      <Modal open={!!declining} title="Từ chối ca" okText="Từ chối" cancelText="Hủy" onCancel={() => setDeclining(null)}
        okButtonProps={{ disabled: !reason.trim(), danger: true }} confirmLoading={decline.isPending} onOk={() => decline.mutate()}>
        <Input.TextArea placeholder="Lý do (bắt buộc)" value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
      {requesting && <RequestModal shift={requesting} onClose={() => setRequesting(null)} onDone={refresh} />}
    </Row>
  );
}

function RequestModal({ shift, onClose, onDone }: { shift: MyShift; onClose: () => void; onDone: () => void }) {
  const notify = useNotify();
  const [form] = Form.useForm<{ type: ShiftRequestType; targetEmployeeId?: string; reason: string }>();
  const type = Form.useWatch("type", form);
  const colleagues = useQuery({ queryKey: ["colleagues"], queryFn: api.shifts.colleagues });
  const create = useMutation({
    mutationFn: (v: { type: ShiftRequestType; targetEmployeeId?: string; reason: string }) => api.workforce.createRequest({ ...v, shiftId: shift.shiftId }),
    onSuccess: () => { notify.ok("Đã gửi đơn, chờ quản lý duyệt."); onDone(); onClose(); },
    onError: notify.fail,
  });

  return (
    <Modal open title={`Đơn cho ca ${dayMonth(shift.date)} ${shiftTypeLabels[shift.type]}`} okText="Gửi đơn" cancelText="Hủy" onCancel={onClose} onOk={() => form.submit()} confirmLoading={create.isPending}>
      <Form form={form} layout="vertical" initialValues={{ type: "Leave" }} onFinish={(v) => create.mutate(v)}>
        <Form.Item label="Loại đơn" name="type">
          <Select options={(Object.keys(requestTypeLabels) as ShiftRequestType[]).map((t) => ({ value: t, label: requestTypeLabels[t] }))} />
        </Form.Item>
        {type === "Swap" && (
          <Form.Item label="Người nhận thay" name="targetEmployeeId" rules={[{ required: true }]} extra="Máy chủ kiểm tra người nhận thay không bị trùng lịch.">
            <Select showSearch optionFilterProp="label" options={(colleagues.data ?? []).map((c) => ({ value: c.employeeId, label: c.name }))} />
          </Form.Item>
        )}
        <Form.Item label="Lý do" name="reason" rules={[{ required: true, max: 1000 }]}><Input.TextArea /></Form.Item>
      </Form>
    </Modal>
  );
}

const DAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

/** Weekly recurring availability (giờ rảnh) used by the manager's scheduling assistant. */
function AvailabilityCard() {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const slots = useQuery({ queryKey: ["availability"], queryFn: api.workforce.myAvailability });
  const [edit, setEdit] = useState<Record<string, [Dayjs, Dayjs] | null> | null>(null);
  const save = useMutation({
    mutationFn: () => api.workforce.setMyAvailability(
      Object.entries(edit!).filter(([, v]) => v).map(([day, v]): AvailabilitySlot => ({
        isRecurring: true, dayOfWeek: day, specificDate: null, from: v![0].format("HH:mm:ss"), to: v![1].format("HH:mm:ss"),
      })),
    ),
    onSuccess: () => { notify.ok("Đã lưu giờ rảnh."); setEdit(null); void queryClient.invalidateQueries({ queryKey: ["availability"] }); },
    onError: notify.fail,
  });
  const start = () => setEdit(Object.fromEntries(DAYS.map((d) => {
    const s = slots.data?.find((x) => x.isRecurring && x.dayOfWeek === d);
    return [d, s ? [dayjs(s.from, "HH:mm:ss"), dayjs(s.to, "HH:mm:ss")] as [Dayjs, Dayjs] : null];
  })));

  return (
    <Card title="Giờ rảnh hằng tuần" extra={!edit && <Button onClick={start}>Sửa</Button>}>
      {!edit && (
        (slots.data ?? []).length === 0 ? <Typography.Text type="secondary">Chưa đăng ký giờ rảnh.</Typography.Text> : (
          <Space direction="vertical">
            {slots.data!.map((s, i) => <div key={i}>{s.dayOfWeek ? dayOfWeekLabels[s.dayOfWeek] : s.specificDate}: {s.from.slice(0, 5)}–{s.to.slice(0, 5)}</div>)}
          </Space>
        )
      )}
      {edit && (
        <Space direction="vertical" style={{ width: "100%" }}>
          {DAYS.map((d) => (
            <Space key={d}>
              <Checkbox style={{ width: 100 }} checked={!!edit[d]}
                onChange={(e) => setEdit({ ...edit, [d]: e.target.checked ? [dayjs("08:00", "HH:mm"), dayjs("22:00", "HH:mm")] : null })}>
                {dayOfWeekLabels[d]}
              </Checkbox>
              {edit[d] && <TimePicker.RangePicker format="HH:mm" minuteStep={30} value={edit[d]} onChange={(v) => v && v[0] && v[1] && setEdit({ ...edit, [d]: [v[0], v[1]] })} />}
            </Space>
          ))}
          <Space><Button onClick={() => setEdit(null)}>Hủy</Button><Button type="primary" loading={save.isPending} onClick={() => save.mutate()}>Lưu</Button></Space>
        </Space>
      )}
    </Card>
  );
}
