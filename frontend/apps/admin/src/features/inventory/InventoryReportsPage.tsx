import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card, Col, DatePicker, Progress, Row, Segmented, Space, Statistic, Table, Tabs, Tag, Typography } from "antd";
import dayjs from "dayjs";
import type { BranchUsageRow, UsageGroupBy, UsageReportRow } from "@cafecr/api-client";
import { addDays, dayMonth, issueKindLabels, shiftTypeLabels, todayVn, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";

const ratio = (actual: number | null, norm: number) => (actual === null || norm <= 0 ? null : Math.round((actual / norm) * 100));

/** Norm per session (actual vs configured) by day/shift, reconciliation results and a branch comparison. */
export function InventoryReportsPage() {
  const [range, setRange] = useState<[string, string]>(() => [addDays(todayVn(), -13), todayVn()]);
  const picker = (
    <DatePicker.RangePicker value={[dayjs(range[0]), dayjs(range[1])]} format="DD/MM/YYYY" allowClear={false}
      onChange={(v) => v?.[0] && v[1] && setRange([v[0].format("YYYY-MM-DD"), v[1].format("YYYY-MM-DD")])} />
  );
  return (
    <Tabs tabBarExtraContent={picker} items={[
      { key: "usage", label: "Định mức thực tế", children: <RequireBranch title="Định mức thực tế">{(b) => <Usage branchId={b} range={range} />}</RequireBranch> },
      { key: "issues", label: "Đối soát 2 kho", children: <RequireBranch title="Đối soát 2 kho">{(b) => <Issues branchId={b} range={range} />}</RequireBranch> },
      { key: "branches", label: "So sánh chi nhánh", children: <Branches range={range} /> },
    ]} />
  );
}

function Usage({ branchId, range }: { branchId: string; range: [string, string] }) {
  const [groupBy, setGroupBy] = useState<UsageGroupBy>("Day");
  const report = useQuery({ queryKey: ["inventory-usage", branchId, range, groupBy], queryFn: () => api.inventory.usageReport(branchId, range[0], range[1], groupBy) });
  const rows = report.data?.rows ?? [];
  const totals = useMemo(() => Object.values(rows.reduce<Record<string, { name: string; unit: string; sessions: number; actual: number; norm: number }>>((acc, r) => {
    const t = (acc[r.itemId] ??= { name: r.itemName, unit: r.baseUnit, sessions: 0, actual: 0, norm: r.norm });
    t.sessions += r.sessions; t.actual += r.actualUsage; t.norm = r.norm;
    return acc;
  }, {})), [rows]);

  return (
    <Space direction="vertical" style={{ width: "100%" }} size="large">
      <Row gutter={[16, 16]}>
        {totals.map((t) => {
          const perSession = t.sessions ? t.actual / t.sessions : null;
          const pct = ratio(perSession, t.norm);
          return (
            <Col key={t.name} xs={24} sm={12} lg={6}>
              <Card size="small">
                <Statistic title={`${t.name} / lượt`} value={perSession === null ? "—" : perSession.toFixed(2)} suffix={t.unit} />
                <Typography.Text type="secondary">Định mức {t.norm} · {t.sessions} lượt</Typography.Text>
                {pct !== null && <Progress percent={Math.min(pct, 200) / 2} format={() => `${pct}% định mức`} status={pct > 115 ? "exception" : "normal"} />}
              </Card>
            </Col>
          );
        })}
      </Row>
      <Card title="Chi tiết" extra={<Segmented value={groupBy} onChange={(v) => setGroupBy(v as UsageGroupBy)} options={[{ value: "Day", label: "Theo ngày" }, { value: "Shift", label: "Theo ca" }]} />}>
        <Table<UsageReportRow> rowKey={(r) => `${r.period}-${r.itemId}`} size="small" loading={report.isLoading} dataSource={rows} scroll={{ x: "max-content" }} pagination={{ pageSize: 50 }}
          columns={[
            { title: groupBy === "Day" ? "Ngày" : "Ca", render: (_, r) => (groupBy === "Day" ? dayMonth(r.date) : `${dayMonth(r.date)} ${r.shiftType ? shiftTypeLabels[r.shiftType] : ""}`) },
            { title: "Mặt hàng", dataIndex: "itemName" },
            { title: "Lượt", dataIndex: "sessions", align: "right" },
            { title: "Tiêu hao thực tế", dataIndex: "actualUsage", align: "right" },
            { title: "Theo định mức", dataIndex: "normUsage", align: "right", render: (v: number) => v.toFixed(1) },
            { title: "Thực tế / lượt", dataIndex: "actualPerSession", align: "right", render: (v: number | null, r) => {
              const pct = ratio(v, r.norm);
              return v === null ? "—" : <Space>{v.toFixed(2)}{pct !== null && <Tag color={pct > 115 ? "red" : pct < 85 ? "blue" : "green"}>{pct}%</Tag>}</Space>;
            } },
            { title: "Định mức", dataIndex: "norm", align: "right" },
            { title: "Lệch đếm", dataIndex: "variance", align: "right" },
          ]} />
      </Card>
    </Space>
  );
}

function Issues({ branchId, range }: { branchId: string; range: [string, string] }) {
  const report = useQuery({ queryKey: ["inventory-issues-report", branchId, range], queryFn: () => api.inventory.issuesReport(branchId, range[0], range[1]) });
  const kinds = report.data?.kinds ?? [];
  const units = (k: string) => kinds.find((x) => x.kind === k)?.units ?? 0;
  const lossValue = (report.data?.items ?? []).reduce((s, i) => s + (i.lossValue ?? 0), 0);
  return (
    <Space direction="vertical" style={{ width: "100%" }} size="large">
      <Row gutter={16}>
        <Col xs={24} sm={8}><Card size="small"><Statistic title="Ghi phiếu sai (đơn vị)" value={units("TransferMisrecorded")} /></Card></Col>
        <Col xs={24} sm={8}><Card size="small"><Statistic title="Hao hụt thật (đơn vị)" value={units("RealLoss")} valueStyle={{ color: "#cf1322" }} />
          <Typography.Text type="secondary">≈ {vnd(lossValue)} theo giá nhập gần nhất</Typography.Text></Card></Col>
        <Col xs={24} sm={8}><Card size="small"><Statistic title="Dư không rõ (đơn vị)" value={units("UnexplainedSurplus")} /></Card></Col>
      </Row>
      <Card title="Theo mặt hàng">
        <Table rowKey="itemId" size="small" loading={report.isLoading} dataSource={report.data?.items} pagination={false}
          columns={[
            { title: "Mặt hàng", dataIndex: "itemName" },
            { title: "Ghi phiếu sai", dataIndex: "misrecorded", align: "right" },
            { title: "Hao hụt thật", dataIndex: "realLoss", align: "right", render: (v: number) => <Typography.Text type={v > 0 ? "danger" : undefined}>{v}</Typography.Text> },
            { title: "Giá trị hao hụt", dataIndex: "lossValue", align: "right", render: (v: number | null) => (v === null ? "—" : vnd(v)) },
            { title: "Dư không rõ", dataIndex: "surplus", align: "right" },
          ]} />
      </Card>
      <Card title="Các kỳ đối soát">
        <Table rowKey="id" size="small" dataSource={report.data?.issues} pagination={{ pageSize: 20 }}
          columns={[
            { title: "Kỳ", render: (_, i) => `${dayMonth(i.periodFrom)} → ${dayMonth(i.periodTo)}` },
            { title: "Mặt hàng", dataIndex: "itemName" },
            { title: "Loại", dataIndex: "kind", render: (k: keyof typeof issueKindLabels) => <Tag color={issueKindLabels[k].color}>{issueKindLabels[k].label}</Tag> },
            { title: "Kho trong", dataIndex: "innerVar", align: "right" },
            { title: "Kho ngoài", dataIndex: "outerVar", align: "right" },
            { title: "Tổng", dataIndex: "total", align: "right" },
            { title: "Trạng thái", dataIndex: "status", render: (s: string) => (s === "Open" ? <Tag color="gold">Chưa xử lý</Tag> : <Tag>Đã xử lý</Tag>) },
          ]} />
      </Card>
    </Space>
  );
}

function Branches({ range }: { range: [string, string] }) {
  const report = useQuery({ queryKey: ["inventory-branches", range], queryFn: () => api.inventory.branchUsage(range[0], range[1]) });
  return (
    <Card title="Định mức thực tế / lượt theo chi nhánh">
      <Table<BranchUsageRow> rowKey={(r) => `${r.branchId}-${r.itemId}`} size="small" loading={report.isLoading} dataSource={report.data} pagination={false} scroll={{ x: "max-content" }}
        columns={[
          { title: "Mặt hàng", dataIndex: "itemName" },
          { title: "Chi nhánh", dataIndex: "branchName", render: (v: string) => v.replace("[DEMO DATA] ", "") },
          { title: "Lượt", dataIndex: "sessions", align: "right" },
          { title: "Thực tế / lượt", dataIndex: "actualPerSession", align: "right", render: (v: number | null) => (v === null ? "—" : v.toFixed(2)) },
          { title: "Định mức", dataIndex: "norm", align: "right" },
          { title: "", render: (_, r) => {
            const pct = ratio(r.actualPerSession, r.norm);
            return pct === null ? null : <Progress style={{ width: 160 }} size="small" percent={Math.min(pct, 200) / 2} format={() => `${pct}%`} status={pct > 115 ? "exception" : "normal"} />;
          } },
        ]} />
    </Card>
  );
}
