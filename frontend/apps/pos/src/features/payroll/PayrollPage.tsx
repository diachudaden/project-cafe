import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card, Col, DatePicker, Row, Statistic, Table, Tag } from "antd";
import dayjs from "dayjs";
import { dayMonth, monthStart, shiftTypeLabels, time, todayVn, vnd } from "@cafecr/ui";
import { api } from "../../api";

export function PayrollPage() {
  const [month, setMonth] = useState(() => monthStart(todayVn()));
  const payroll = useQuery({ queryKey: ["my-payroll", month], queryFn: () => api.finance.myPayroll(month) });
  const p = payroll.data;

  return (
    <Card title="Lương của tôi" extra={<DatePicker picker="month" value={dayjs(month)} onChange={(d) => d && setMonth(d.startOf("month").format("YYYY-MM-DD"))} />}>
      <Row gutter={16} style={{ marginBottom: 16 }}>
        <Col xs={12} md={6}><Statistic title="Lương giờ" value={p ? vnd(p.hourlyRate) : "—"} /></Col>
        <Col xs={12} md={6}><Statistic title="Phụ cấp đêm / giờ" value={p ? vnd(p.nightAllowance) : "—"} /></Col>
        <Col xs={12} md={6}><Statistic title="Tạm tính (đã duyệt)" value={p ? vnd(p.provisionalNet) : "—"} /></Col>
        <Col xs={12} md={6}>
          <Statistic title={p?.payroll?.status === "Finalized" ? "Thực nhận (đã chốt)" : "Bảng lương"} value={p?.payroll ? vnd(p.payroll.netAmount) : "Chưa có"} />
        </Col>
      </Row>
      <Table size="small" rowKey={(l) => l.checkInAt} loading={payroll.isLoading} dataSource={p?.lines} pagination={false} scroll={{ x: "max-content" }}
        columns={[
          { title: "Ngày", render: (_, l) => `${dayMonth(l.date)} ${shiftTypeLabels[l.shiftType]}` },
          { title: "Chi nhánh", dataIndex: "branchName" },
          { title: "Vào – ra", render: (_, l) => `${time(l.checkInAt)}–${time(l.checkOutAt)}` },
          { title: "Giờ công", dataIndex: "hours" },
          { title: "Giờ đêm", dataIndex: "nightHours" },
          { title: "Tiền ca", render: (_, l) => (l.approved ? vnd(l.amount) : <Tag>Chờ duyệt</Tag>) },
        ]} />
      {p?.payroll && (
        <Card size="small" style={{ marginTop: 16 }} title="Chi tiết bảng lương">
          Lương giờ {vnd(p.payroll.baseAmount)} + phụ cấp {vnd(p.payroll.allowance)} + thưởng {vnd(p.payroll.bonus)} − phạt {vnd(p.payroll.deduction)} = <b>{vnd(p.payroll.netAmount)}</b>
        </Card>
      )}
    </Card>
  );
}
