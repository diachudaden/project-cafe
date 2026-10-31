import { Form, Input, InputNumber, Switch, Tag, TimePicker } from "antd";
import dayjs, { type Dayjs } from "dayjs";
import type { Branch, BranchInput } from "@cafecr/api-client";
import { adminApi } from "../../api";
import { CrudPage } from "../../components/CrudPage";

const TIME = "HH:mm:ss";

type BranchForm = Omit<BranchInput, "openTime" | "closeTime"> & { openTime: Dayjs; closeTime: Dayjs };

const toInput = ({ openTime, closeTime, ...rest }: BranchForm): BranchInput => ({
  ...rest,
  phone: rest.phone || null,
  openTime: openTime.format(TIME),
  closeTime: closeTime.format(TIME),
});

export function BranchesPage() {
  return (
    <CrudPage<Branch, BranchForm, BranchInput, BranchInput, Branch>
      title="Chi nhánh"
      queryKey="branches"
      resource={adminApi.branches}
      columns={[
        { title: "Tên", dataIndex: "name" },
        { title: "Địa chỉ", dataIndex: "address" },
        { title: "Điện thoại", dataIndex: "phone" },
        {
          title: "Giờ mở cửa",
          render: (_, b) => (b.is24h ? "24/7" : `${b.openTime.slice(0, 5)} – ${b.closeTime.slice(0, 5)}`),
        },
        {
          title: "Trạng thái",
          dataIndex: "isActive",
          render: (active: boolean) => (active ? <Tag color="green">Hoạt động</Tag> : <Tag>Tạm ngưng</Tag>),
        },
      ]}
      initialValues={{ is24h: false, isActive: true, openTime: dayjs("07:00:00", TIME), closeTime: dayjs("22:00:00", TIME) }}
      toFormValues={({ id: _id, ...b }) => ({ ...b, openTime: dayjs(b.openTime, TIME), closeTime: dayjs(b.closeTime, TIME) })}
      toCreate={toInput}
      toUpdate={toInput}
      renderFields={() => (
        <>
          <Form.Item label="Tên" name="name" rules={[{ required: true, max: 150 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Địa chỉ" name="address" rules={[{ required: true, max: 500 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Điện thoại" name="phone" rules={[{ max: 32 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Mở cửa 24/7" name="is24h" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item label="Giờ mở cửa" name="openTime" rules={[{ required: true }]}>
            <TimePicker format="HH:mm" minuteStep={15} />
          </Form.Item>
          <Form.Item label="Giờ đóng cửa" name="closeTime" rules={[{ required: true }]}>
            <TimePicker format="HH:mm" minuteStep={15} />
          </Form.Item>
          <Form.Item label="Vĩ độ" name="latitude">
            <InputNumber style={{ width: "100%" }} min={-90} max={90} />
          </Form.Item>
          <Form.Item label="Kinh độ" name="longitude">
            <InputNumber style={{ width: "100%" }} min={-180} max={180} />
          </Form.Item>
          <Form.Item label="Đang hoạt động" name="isActive" valuePropName="checked">
            <Switch />
          </Form.Item>
        </>
      )}
    />
  );
}
