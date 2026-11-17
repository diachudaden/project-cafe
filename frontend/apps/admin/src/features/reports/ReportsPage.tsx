import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card, Col, DatePicker, Progress, Row, Segmented, Space, Statistic, Table, Typography } from "antd";
import dayjs from "dayjs";
import type { ReportPeriod } from "@cafecr/api-client";
import { seatTypeLabels, todayVn, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useBranch } from "../../branch/BranchContext";

const periods: { value: ReportPeriod; label: string }[] = [
  { value: "Week", label: "Tuần" }, { value: "Month", label: "Tháng" }, { value: "Quarter", label: "Quý" }, { value: "Year", label: "Năm" },
];

export function ReportsPage() {
  const { branchId, branchName, canSeeAll } = useBranch();
  const [period, setPeriod] = useState<ReportPeriod>("Month");
  const [date, setDate] = useState(todayVn());
  const b = branchId ?? undefined;
  const revenue = useQuery({ queryKey: ["report", "revenue", b, period, date], queryFn: () => api.reports.revenue(period, date, b) });
  const pl = useQuery({ queryKey: ["report", "pl", b, period, date], queryFn: () => api.reports.profitLoss(period, date, b) });
  const occupancy = useQuery({ queryKey: ["report", "occ", b, period, date], queryFn: () => api.reports.occupancy(period, date, b) });
  const compare = useQuery({ queryKey: ["report", "cmp", period, date], queryFn: () => api.reports.branches(period, date), enabled: canSeeAll });
  const r = revenue.data;
  const max = Math.max(1, ...(r?.buckets.map((x) => x.revenue) ?? [1]));

  return (
    <Space direction="vertical" style={{ width: "100%" }} size="middle">
      <Card>
        <Space wrap>
          <Typography.Text strong>{branchName(branchId)}</Typography.Text>
          <Segmented value={period} onChange={(v) => setPeriod(v as ReportPeriod)} options={periods} />
          <DatePicker value={dayjs(date)} onChange={(d) => d && setDate(d.format("YYYY-MM-DD"))} />
        </Space>
      </Card>
      <Row gutter={[16, 16]}>
        <Col xs={12} md={6}><Card><Statistic title="Doanh thu" value={r ? vnd(r.total) : "—"} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="So với kỳ trước" value={r?.changePercent ?? "—"} suffix={r?.changePercent != null ? "%" : ""} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Chi phí + lương" value={pl.data ? vnd(pl.data.expenses + pl.data.payroll) : "—"} /></Card></Col>
        <Col xs={12} md={6}><Card><Statistic title="Lãi / lỗ" value={pl.data ? vnd(pl.data.profit) : "—"} valueStyle={{ color: (pl.data?.profit ?? 0) < 0 ? "#cf1322" : undefined }} /></Card></Col>
      </Row>
      <Card title="Doanh thu theo kỳ">
        <Table
          size="small" rowKey="start" dataSource={r?.buckets} pagination={false} scroll={{ y: 360 }}
          columns={[
            { title: "Kỳ", dataIndex: "label", width: 100 },
            { title: "Doanh thu", dataIndex: "revenue", width: 140, render: (v: number) => vnd(v) },
            { title: "Hóa đơn", dataIndex: "bills", width: 90 },
            { title: "", dataIndex: "revenue", render: (v: number) => <Progress percent={Math.round((v / max) * 100)} showInfo={false} /> },
          ]}
        />
      </Card>
      <Row gutter={[16, 16]}>
        <Col xs={24} md={12}>
          <Card title="Chi phí theo danh mục">
            <Table size="small" pagination={false} rowKey="0" dataSource={Object.entries(pl.data?.expensesByCategory ?? {})}
              columns={[{ title: "Danh mục", render: (_, x) => x[0] }, { title: "Số tiền", render: (_, x) => vnd(x[1]) }]} />
            <Typography.Paragraph style={{ marginTop: 8 }}>Lương: {pl.data ? vnd(pl.data.payroll) : "—"}</Typography.Paragraph>
          </Card>
        </Col>
        <Col xs={24} md={12}>
          <Card title="Tỷ lệ lấp đầy theo loại chỗ">
            <Table size="small" pagination={false} rowKey={(x) => `${x.branchId}-${x.seatType}`} dataSource={occupancy.data}
              columns={[
                { title: "Chi nhánh", dataIndex: "branchName" },
                { title: "Loại", dataIndex: "seatType", render: (t) => seatTypeLabels[t as keyof typeof seatTypeLabels] },
                { title: "Số chỗ", dataIndex: "seats" },
                { title: "Lấp đầy", dataIndex: "rate", render: (v: number) => `${v}%` },
              ]} />
          </Card>
        </Col>
      </Row>
      {canSeeAll && (
        <Card title="So sánh chi nhánh">
          <Table size="small" pagination={false} rowKey="branchId" dataSource={compare.data}
            columns={[
              { title: "#", dataIndex: "rank", width: 50 },
              { title: "Chi nhánh", dataIndex: "branchName" },
              { title: "Doanh thu", dataIndex: "revenue", render: (v: number) => vnd(v) },
              { title: "Lượt khách", dataIndex: "sessions" },
              { title: "Lấp đầy", dataIndex: "occupancy", render: (v: number) => `${v}%` },
              { title: "Chi phí lương", dataIndex: "payrollCost", render: (v: number) => vnd(v) },
              { title: "Lương / doanh thu", dataIndex: "payrollRatio", render: (v: number | null) => (v == null ? "—" : `${v}%`) },
            ]} />
        </Card>
      )}
    </Space>
  );
}
