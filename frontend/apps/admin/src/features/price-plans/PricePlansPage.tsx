import { DatePicker, Form, Input, InputNumber, Select } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import type { PricePlan, PricePlanInput } from "@cafecr/api-client";
import { adminApi } from "../../api";
import { CrudPage } from "../../components/CrudPage";
import { useBranchOptions } from "../lookups";

const ALL_BRANCHES = "";
const vnd = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" });

type PricePlanForm = Omit<PricePlanInput, "branchId" | "validFrom" | "validTo"> & {
  branchId: string;
  validFrom: Dayjs | null;
  validTo: Dayjs | null;
};

const toInput = ({ branchId, validFrom, validTo, ...rest }: PricePlanForm): PricePlanInput => ({
  ...rest,
  branchId: branchId === ALL_BRANCHES ? null : branchId,
  validFrom: validFrom?.toISOString() ?? null,
  validTo: validTo?.toISOString() ?? null,
});

const formatDate = (value: string | null) => (value ? dayjs(value).format("DD/MM/YYYY HH:mm") : "—");

export function PricePlansPage() {
  const branches = useBranchOptions();

  return (
    <CrudPage<PricePlan, PricePlanForm, PricePlanInput, PricePlanInput, PricePlan>
      title="Bảng giá"
      queryKey="price-plans"
      resource={adminApi.pricePlans}
      columns={[
        { title: "Tên", dataIndex: "name" },
        {
          title: "Chi nhánh",
          dataIndex: "branchId",
          render: (id: string | null) => (id ? branches.nameOf(id) : "Tất cả"),
        },
        { title: "Thời lượng (phút)", dataIndex: "durationMinutes" },
        { title: "Giá", dataIndex: "price", render: (v: number) => vnd.format(v) },
        { title: "Giá gia hạn", dataIndex: "extendPrice", render: (v: number) => vnd.format(v) },
        { title: "Hiệu lực từ", dataIndex: "validFrom", render: formatDate },
        { title: "Đến", dataIndex: "validTo", render: formatDate },
      ]}
      initialValues={{ branchId: ALL_BRANCHES, durationMinutes: 240, price: 0, extendPrice: 0 }}
      toFormValues={({ id: _id, branchId, validFrom, validTo, ...p }) => ({
        ...p,
        branchId: branchId ?? ALL_BRANCHES,
        validFrom: validFrom ? dayjs(validFrom) : null,
        validTo: validTo ? dayjs(validTo) : null,
      })}
      toCreate={toInput}
      toUpdate={toInput}
      renderFields={() => (
        <>
          <Form.Item label="Tên" name="name" rules={[{ required: true, max: 150 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Chi nhánh" name="branchId">
            <Select options={[{ value: ALL_BRANCHES, label: "Tất cả chi nhánh" }, ...branches.options]} />
          </Form.Item>
          <Form.Item label="Thời lượng (phút)" name="durationMinutes" rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} min={1} />
          </Form.Item>
          <Form.Item label="Giá (VND)" name="price" rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} min={0} step={1000} />
          </Form.Item>
          <Form.Item label="Giá gia hạn (VND)" name="extendPrice" rules={[{ required: true }]}>
            <InputNumber style={{ width: "100%" }} min={0} step={1000} />
          </Form.Item>
          <Form.Item label="Hiệu lực từ" name="validFrom">
            <DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: "100%" }} />
          </Form.Item>
          <Form.Item label="Hiệu lực đến" name="validTo">
            <DatePicker showTime format="DD/MM/YYYY HH:mm" style={{ width: "100%" }} />
          </Form.Item>
        </>
      )}
    />
  );
}
