import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, DatePicker, Space, Switch, Table, Tag } from "antd";
import dayjs from "dayjs";
import { addDays, dayMonth, shiftTypeLabels, time, todayVn, weekStart } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function AttendancePage() {
  return <RequireBranch title="Chấm công">{(branchId) => <Attendance branchId={branchId} />}</RequireBranch>;
}

function Attendance({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [week, setWeek] = useState(() => weekStart(todayVn()));
  const rows = useQuery({ queryKey: ["attendance", branchId, week], queryFn: () => api.attendance.list(branchId, week, addDays(week, 6)) });
  const approve = useMutation({
    mutationFn: ({ id, approved }: { id: string; approved: boolean }) => api.attendance.approve(id, approved),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["attendance", branchId] }),
    onError: notify.fail,
  });
  const pending = (rows.data ?? []).filter((r) => r.attendanceId && r.checkOutAt && !r.approved);

  return (
    <Card title="Duyệt chấm công" extra={
      <Space>
        <DatePicker picker="week" value={dayjs(week)} onChange={(d) => d && setWeek(weekStart(d.format("YYYY-MM-DD")))} />
        <Button disabled={!pending.length} onClick={() => pending.forEach((r) => approve.mutate({ id: r.attendanceId!, approved: true }))}>
          Duyệt tất cả ({pending.length})
        </Button>
      </Space>
    }>
      <Table
        rowKey={(r) => `${r.shiftId}-${r.employeeId}`} size="middle" loading={rows.isLoading} dataSource={rows.data} scroll={{ x: "max-content" }} pagination={{ pageSize: 30 }}
        columns={[
          { title: "Ngày", render: (_, r) => `${dayMonth(r.date)} ${shiftTypeLabels[r.shiftType]}` },
          { title: "Nhân viên", dataIndex: "employeeName" },
          { title: "Giờ ca", render: (_, r) => `${time(r.shiftStart)}–${time(r.shiftEnd)}` },
          { title: "Giờ thực tế", render: (_, r) => r.missing ? <Tag color="red">Vắng</Tag> : `${time(r.checkInAt)}–${time(r.checkOutAt)}` },
          {
            title: "Ghi chú", render: (_, r) => (
              <Space size={4} wrap>
                {r.lateMinutes > 10 && <Tag color="orange">Trễ {r.lateMinutes}′</Tag>}
                {r.earlyLeaveMinutes > 0 && <Tag color="gold">Về sớm {r.earlyLeaveMinutes}′</Tag>}
                {r.overtimeMinutes > 15 && <Tag color="blue">Tăng ca {r.overtimeMinutes}′</Tag>}
              </Space>
            ),
          },
          {
            title: "Duyệt", render: (_, r) => r.attendanceId && (
              <Switch checked={r.approved} disabled={!r.checkOutAt} onChange={(approved) => approve.mutate({ id: r.attendanceId!, approved })} />
            ),
          },
        ]}
      />
    </Card>
  );
}
