import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Alert, Button, Card, DatePicker, Descriptions, Drawer, Empty, Input, Segmented, Select, Space, Switch, Table, Tag, Timeline, Tooltip, Typography,
} from "antd";
import { ReloadOutlined, SearchOutlined } from "@ant-design/icons";
import type { Dayjs } from "dayjs";
import {
  paymentLogStages, type PaymentLogFilter, type PaymentLogLevel, type PaymentLogStage, type PaymentLogView, type PaymentRequestStatus,
} from "@cafecr/api-client";
import { errorText, vn, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useBranch } from "../../branch/BranchContext";

const stageLabels: Record<PaymentLogStage, string> = {
  RequestCreated: "Tạo mã QR",
  StatusPolled: "POS kiểm tra",
  WebhookReceived: "Nhận webhook",
  WebhookRejected: "Webhook bị từ chối",
  WebhookDuplicate: "Webhook trùng",
  TransactionRecorded: "Lưu giao dịch",
  TransactionIgnored: "Bỏ qua giao dịch",
  TransactionUnmatched: "Không khớp mã",
  CodeMatched: "Khớp mã",
  AmountMismatch: "Sai số tiền",
  LatePayment: "Trả muộn",
  AlreadyPaid: "Trả trùng",
  SettlementFailed: "Không tạo được bill",
  Settled: "Hoàn tất",
  Expired: "Hết hạn",
  Cancelled: "Đã hủy",
  Exception: "Lỗi hệ thống",
};

const levelColors: Record<PaymentLogLevel, string> = { Info: "blue", Warning: "orange", Error: "red" };
const levelLabels: Record<PaymentLogLevel, string> = { Info: "Thông tin", Warning: "Cảnh báo", Error: "Lỗi" };
const stageColor = (stage: PaymentLogStage, level: PaymentLogLevel) =>
  stage === "Settled" ? "green" : level === "Info" ? "default" : levelColors[level];

const requestStatus: Record<PaymentRequestStatus, { label: string; color: string }> = {
  Pending: { label: "Đang chờ", color: "processing" },
  Paid: { label: "Đã thanh toán", color: "success" },
  Expired: { label: "Hết hạn", color: "default" },
  Cancelled: { label: "Đã hủy", color: "default" },
  NeedsAttention: { label: "Cần xử lý", color: "error" },
};

/** Quick views over the most common questions. */
const views: Record<string, Pick<PaymentLogFilter, "stage" | "level">> = {
  all: {},
  attention: { level: ["Warning", "Error"] },
  webhook: { stage: ["WebhookReceived", "WebhookRejected", "WebhookDuplicate", "TransactionRecorded", "TransactionUnmatched", "TransactionIgnored", "Exception"] },
  flow: { stage: ["RequestCreated", "CodeMatched", "Settled", "SettlementFailed", "Expired", "Cancelled"] },
};

const stamp = (iso: string) => vn(iso).format("DD/MM HH:mm:ss");
const elapsed = (ms: number | null) => (ms == null ? "" : ms < 60_000 ? `+${(ms / 1000).toFixed(1)}s` : `+${Math.floor(ms / 60_000)}m${Math.round((ms % 60_000) / 1000)}s`);

function DetailJson({ log }: { log: PaymentLogView }) {
  const pretty = useMemo(() => {
    if (!log.detail) return null;
    try {
      return JSON.stringify(JSON.parse(log.detail), null, 2);
    } catch {
      return log.detail;
    }
  }, [log.detail]);
  return (
    <Space direction="vertical" size={4} style={{ width: "100%" }}>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        {log.providerTxId && <>Mã GD SePay: <Typography.Text code copyable>{log.providerTxId}</Typography.Text> · </>}
        {log.traceId && <>Trace: <Typography.Text code copyable>{log.traceId}</Typography.Text></>}
      </Typography.Text>
      {pretty && <pre style={{ margin: 0, fontSize: 12, whiteSpace: "pre-wrap", wordBreak: "break-all" }}>{pretty}</pre>}
    </Space>
  );
}

export function PaymentLogsPage() {
  const { branchName } = useBranch();
  const [view, setView] = useState("all");
  const [text, setText] = useState("");
  const [search, setSearch] = useState("");
  const [stages, setStages] = useState<PaymentLogStage[]>([]);
  const [levels, setLevels] = useState<PaymentLogLevel[]>([]);
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const [live, setLive] = useState(true);
  const [traceCode, setTraceCode] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setSearch(text.trim()), 300);
    return () => clearTimeout(timer);
  }, [text]);

  const filter: PaymentLogFilter = {
    search: search || undefined,
    stage: stages.length ? stages : views[view].stage,
    level: levels.length ? levels : views[view].level,
    from: range?.[0]?.startOf("day").toISOString(),
    to: range?.[1]?.endOf("day").toISOString(),
    page,
    pageSize,
  };
  const filterKey = JSON.stringify({ ...filter, page: undefined, pageSize: undefined });
  useEffect(() => setPage(1), [filterKey]);

  const logs = useQuery({
    queryKey: ["payment-logs", filter],
    queryFn: () => api.paymentLogs.list(filter),
    refetchInterval: live ? 10_000 : false,
    placeholderData: (previous) => previous,
  });

  return (
    <Card
      title="Nhật ký thanh toán"
      extra={
        <Space>
          <Tooltip title="Tự tải lại mỗi 10 giây"><Switch checked={live} onChange={setLive} checkedChildren="Trực tiếp" unCheckedChildren="Tạm dừng" /></Tooltip>
          <Button icon={<ReloadOutlined />} loading={logs.isFetching} onClick={() => void logs.refetch()} />
        </Space>
      }
    >
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Segmented
          value={view}
          onChange={(v) => setView(v as string)}
          options={[
            { value: "all", label: "Tất cả" },
            { value: "attention", label: "Cần chú ý" },
            { value: "webhook", label: "Webhook SePay" },
            { value: "flow", label: "Các mốc chính" },
          ]}
        />
        <Space wrap>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder="Mã thanh toán, mã bill, mã GD SePay, nội dung"
            value={text}
            onChange={(e) => setText(e.target.value)}
            style={{ width: 320 }}
          />
          <Select
            mode="multiple"
            allowClear
            placeholder="Bước"
            value={stages}
            onChange={setStages}
            options={paymentLogStages.map((s) => ({ value: s, label: stageLabels[s] }))}
            optionFilterProp="label"
            style={{ minWidth: 220 }}
          />
          <Select
            mode="multiple"
            allowClear
            placeholder="Mức"
            value={levels}
            onChange={setLevels}
            options={(["Info", "Warning", "Error"] as const).map((l) => ({ value: l, label: levelLabels[l] }))}
            style={{ minWidth: 160 }}
          />
          <DatePicker.RangePicker value={range} onChange={(v) => setRange(v)} format="DD/MM/YYYY" />
        </Space>
        {logs.isError && <Alert type="error" showIcon message="Không tải được nhật ký" description={errorText(logs.error)} />}
        <Table<PaymentLogView>
          rowKey="id"
          size="small"
          loading={logs.isLoading}
          dataSource={logs.data?.items}
          scroll={{ x: "max-content" }}
          rowClassName={(l) => (l.level === "Error" ? "payment-log-error" : "")}
          expandable={{ expandedRowRender: (l) => <DetailJson log={l} />, rowExpandable: (l) => !!l.detail || !!l.providerTxId }}
          pagination={{
            current: page,
            pageSize,
            total: logs.data?.total ?? 0,
            showSizeChanger: true,
            pageSizeOptions: [20, 50, 100, 200],
            showTotal: (total) => `${total} dòng`,
            onChange: (p, s) => { setPage(p); setPageSize(s); },
          }}
          columns={[
            { title: "Thời gian", dataIndex: "occurredAt", render: (v: string) => stamp(v) },
            { title: "Mức", dataIndex: "level", render: (l: PaymentLogLevel) => <Tag color={levelColors[l]}>{levelLabels[l]}</Tag> },
            { title: "Bước", render: (_, l) => <Tag color={stageColor(l.stage, l.level)}>{stageLabels[l.stage] ?? l.stage}</Tag> },
            {
              title: "Mã TT",
              dataIndex: "paymentCode",
              render: (code: string | null, l) => code && (l.paymentRequestId
                ? <Button type="link" size="small" style={{ padding: 0 }} onClick={() => setTraceCode(code)}>{code}</Button>
                : <Typography.Text type="secondary">{code}</Typography.Text>),
            },
            { title: "Số tiền", dataIndex: "amount", align: "right", render: (v: number | null) => (v == null ? "" : vnd(v)) },
            { title: "Bill", dataIndex: "billCode", render: (v: string | null) => v && <Typography.Text code>{v}</Typography.Text> },
            { title: "Nội dung", dataIndex: "message", render: (m: string) => <span style={{ whiteSpace: "normal", display: "inline-block", maxWidth: 520 }}>{m}</span> },
            { title: "Từ lúc tạo", dataIndex: "elapsedMs", render: (ms: number | null) => <Typography.Text type="secondary">{elapsed(ms)}</Typography.Text> },
            { title: "Chi nhánh", dataIndex: "branchId", render: (id: string | null) => (id ? branchName(id).replace("[DEMO DATA] ", "") : <Typography.Text type="secondary">—</Typography.Text>) },
          ]}
        />
      </Space>
      <style>{`.payment-log-error > td { background: color-mix(in srgb, #ff4d4f 8%, transparent) !important; }`}</style>
      <TraceDrawer code={traceCode} onClose={() => setTraceCode(null)} />
    </Card>
  );
}

function TraceDrawer({ code, onClose }: { code: string | null; onClose: () => void }) {
  const trace = useQuery({
    queryKey: ["payment-trace", code],
    queryFn: () => api.paymentLogs.trace(code!),
    enabled: !!code,
    refetchInterval: (q) => (q.state.data?.request.status === "Pending" ? 5_000 : false),
  });
  const data = trace.data;
  // Show the request's own steps together with uncoded callbacks (rejected webhooks...) from the same time window.
  const timeline = useMemo(() => {
    if (!data) return [];
    return [...data.logs.map((l) => ({ ...l, related: false })), ...data.relatedLogs.map((l) => ({ ...l, related: true }))]
      .sort((a, b) => a.occurredAt.localeCompare(b.occurredAt));
  }, [data]);

  return (
    <Drawer open={!!code} onClose={onClose} width={720} title={`Dòng thời gian · ${code ?? ""}`} destroyOnClose>
      {trace.isLoading && <Typography.Text>Đang tải...</Typography.Text>}
      {trace.isError && <Alert type="error" showIcon message={errorText(trace.error)} />}
      {data && (
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
          <Descriptions size="small" column={2} bordered>
            <Descriptions.Item label="Trạng thái"><Tag color={requestStatus[data.request.status].color}>{requestStatus[data.request.status].label}</Tag></Descriptions.Item>
            <Descriptions.Item label="Loại">{data.request.kind === "CheckIn" ? "Check-in" : "Gia hạn"}</Descriptions.Item>
            <Descriptions.Item label="Số tiền">{vnd(data.request.amount)}</Descriptions.Item>
            <Descriptions.Item label="Chỗ">{data.request.seatCode} · {data.request.branchName.replace("[DEMO DATA] ", "")}</Descriptions.Item>
            <Descriptions.Item label="Tạo lúc">{stamp(data.request.createdAt)}</Descriptions.Item>
            <Descriptions.Item label="Hết hạn">{stamp(data.request.expiresAt)}</Descriptions.Item>
            <Descriptions.Item label="Thanh toán lúc">{data.request.paidAt ? stamp(data.request.paidAt) : "—"}</Descriptions.Item>
            <Descriptions.Item label="Bill">{data.request.billCode ? <Typography.Text code copyable>{data.request.billCode}</Typography.Text> : "—"}</Descriptions.Item>
            <Descriptions.Item label="Người tạo" span={2}>{data.request.createdByName ?? "—"}</Descriptions.Item>
          </Descriptions>

          {data.diagnosis.length > 0 && (
            <Alert
              type={data.request.status === "Paid" && data.diagnosis.length === 1 ? "success" : data.request.status === "Paid" ? "info" : "warning"}
              showIcon
              message="Nhận định"
              description={<ul style={{ margin: 0, paddingLeft: 18 }}>{data.diagnosis.map((d) => <li key={d}>{d}</li>)}</ul>}
            />
          )}

          <div>
            <Typography.Title level={5}>Các bước</Typography.Title>
            {timeline.length === 0 ? <Empty description="Chưa có log cho mã này." /> : (
              <Timeline
                items={timeline.map((l) => ({
                  color: l.stage === "Settled" ? "green" : l.level === "Error" ? "red" : l.level === "Warning" ? "orange" : l.related ? "gray" : "blue",
                  children: (
                    <Space direction="vertical" size={0} style={{ width: "100%" }}>
                      <Space wrap size={4}>
                        <Typography.Text type="secondary">{stamp(l.occurredAt)}</Typography.Text>
                        <Tag color={stageColor(l.stage, l.level)}>{stageLabels[l.stage] ?? l.stage}</Tag>
                        {l.related && <Tag>không gắn mã</Tag>}
                        {!l.related && <Typography.Text type="secondary">{elapsed(l.elapsedMs)}</Typography.Text>}
                      </Space>
                      <Typography.Text>{l.message}</Typography.Text>
                      {l.detail && (
                        <Typography.Paragraph type="secondary" style={{ fontSize: 12, margin: 0 }} ellipsis={{ rows: 2, expandable: true, symbol: "chi tiết" }}>
                          {l.detail}
                        </Typography.Paragraph>
                      )}
                    </Space>
                  ),
                }))}
              />
            )}
          </div>

          <div>
            <Typography.Title level={5}>Giao dịch ngân hàng</Typography.Title>
            <Table
              rowKey="id"
              size="small"
              pagination={false}
              dataSource={data.transactions}
              locale={{ emptyText: "Không có giao dịch nào khớp mã hoặc cùng số tiền trong thời gian này." }}
              scroll={{ x: "max-content" }}
              columns={[
                { title: "Nhận lúc", dataIndex: "receivedAt", render: (v: string) => stamp(v) },
                { title: "Ngân hàng ghi", dataIndex: "occurredAt", render: (v: string) => stamp(v) },
                { title: "Số tiền", dataIndex: "amount", render: (v: number) => vnd(v) },
                { title: "Mã GD", dataIndex: "providerTxId", render: (v: string) => <Typography.Text code copyable>{v}</Typography.Text> },
                { title: "Khớp", render: (_, t) => (t.suspect ? <Tag color="orange">Nghi ngờ</Tag> : <Tag>{t.matchStatus}</Tag>) },
                { title: "Ghi chú", dataIndex: "note" },
                { title: "Nội dung CK", dataIndex: "content", render: (v: string | null) => v ?? <Typography.Text type="secondary">—</Typography.Text> },
              ]}
            />
          </div>
        </Space>
      )}
    </Drawer>
  );
}
