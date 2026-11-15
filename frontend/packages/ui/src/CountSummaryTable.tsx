import { Alert, Descriptions, Table, Typography } from "antd";
import type { CountSummary, CountSummaryRow } from "@cafecr/api-client";
import { VarianceChip } from "./InventoryControls";
import { unitsText } from "./inventory";
import { shiftTypeLabels } from "./labels";
import { dateTime, time } from "./format";

const num = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(1).replace(".", ","));

/** Shift summary table (TASK-027 §4.2): Đầu ca · + Chuyển vào · − Huỷ · Lượt × định mức · Dự kiến · Đếm · Lệch · Ngưỡng. */
export function CountSummaryTable({ summary, countColumn = "Đếm" }: { summary: CountSummary; countColumn?: string }) {
  const outer = summary.warehouse === "Outer";
  const over = summary.itemCount - summary.withinCount;
  return (
    <>
      <Descriptions size="small" column={{ xs: 1, sm: 2 }} style={{ marginBottom: 12 }} items={[
        { label: "Chi nhánh", children: summary.branchName.replace("[DEMO DATA] ", "") },
        outer && summary.shiftType
          ? { label: "Ca", children: `${shiftTypeLabels[summary.shiftType]} ${time(summary.shiftStart)}–${time(summary.shiftEnd)}` }
          : { label: "Kho", children: "Kho trong" },
        { label: "Người đếm", children: summary.confirmedByName ?? summary.countedByName },
        outer ? { label: "Lượt bán", children: summary.sessions } : { label: "Mã phiếu", children: summary.code },
        ...(summary.confirmedAt ? [{ label: "Xác nhận lúc", children: dateTime(summary.confirmedAt) }] : []),
      ]} />
      <Alert style={{ marginBottom: 12 }} showIcon type={over === 0 ? "success" : "warning"}
        message={over === 0 ? "Tất cả trong ngưỡng" : `${summary.withinCount}/${summary.itemCount} món trong ngưỡng`} />
      <Table<CountSummaryRow> size="small" rowKey="itemId" pagination={false} dataSource={summary.rows} scroll={{ x: true }}
        rowClassName={(r) => (r.withinTolerance === false ? "count-row-over" : "")}
        columns={[
          { title: "Món", dataIndex: "name", fixed: "left", render: (v: string, r) => <><Typography.Text strong>{v}</Typography.Text><br /><Typography.Text type="secondary">{r.baseUnit}</Typography.Text></> },
          { title: outer ? "Đầu ca" : "Tồn sổ", dataIndex: "opening", align: "right" },
          ...(outer ? [
            { title: "+ Chuyển vào", dataIndex: "transfersIn", align: "right" as const },
            { title: "− Huỷ", dataIndex: "waste", align: "right" as const },
            { title: "Lượt × định mức", align: "right" as const, render: (_: unknown, r: CountSummaryRow) => `${r.sessions} × ${num(r.norm)} = ${num(r.usage)}` },
          ] : []),
          { title: "Dự kiến", dataIndex: "expected", align: "right", render: (v: number) => <b>{v}</b> },
          { title: countColumn, dataIndex: "counted", align: "right", render: (v: number | null, r) => (
            <span title={v !== null && r.units.length ? unitsText(v, r.baseUnit, r.units) : undefined}><b>{v ?? "—"}</b></span>
          ) },
          { title: "Lệch", dataIndex: "variance", align: "right", render: (v: number | null) => <b>{v === null ? "—" : v > 0 ? `+${v}` : v}</b> },
          { title: "Ngưỡng", dataIndex: "tolerance", align: "right", render: (v: number) => `±${v}` },
          { title: "", key: "chip", render: (_, r) => <VarianceChip variance={r.variance} tolerance={r.tolerance} /> },
        ]} />
    </>
  );
}
