import { Form, Input, Select, Tag } from "antd";
import { seatStatuses, seatTypes, type Seat, type SeatInput, type SeatStatus, type SeatType } from "@cafecr/api-client";
import { adminApi } from "../../api";
import { CrudPage } from "../../components/CrudPage";
import { useZoneOptions } from "../lookups";

const typeLabels: Record<SeatType, string> = { Single: "Đơn", Group: "Nhóm", Nap: "Ngủ trưa" };
const statusLabels: Record<SeatStatus, { label: string; color: string }> = {
  Available: { label: "Trống", color: "green" },
  Occupied: { label: "Đang dùng", color: "orange" },
  Maintenance: { label: "Bảo trì", color: "default" },
};

export function SeatsPage() {
  const zones = useZoneOptions();

  return (
    <CrudPage<Seat, SeatInput, SeatInput, SeatInput, Seat>
      title="Chỗ ngồi"
      queryKey="seats"
      resource={adminApi.seats}
      columns={[
        { title: "Mã", dataIndex: "code" },
        { title: "Khu vực", dataIndex: "zoneId", render: (id: string) => zones.nameOf(id) },
        { title: "Loại", dataIndex: "type", render: (t: SeatType) => typeLabels[t] },
        {
          title: "Trạng thái",
          dataIndex: "status",
          render: (s: SeatStatus) => <Tag color={statusLabels[s].color}>{statusLabels[s].label}</Tag>,
        },
      ]}
      initialValues={{ type: "Single", status: "Available" }}
      toFormValues={({ id: _id, ...s }) => s}
      toCreate={(v) => v}
      toUpdate={(v) => v}
      renderFields={() => (
        <>
          <Form.Item label="Khu vực" name="zoneId" rules={[{ required: true }]}>
            <Select options={zones.options} showSearch optionFilterProp="label" />
          </Form.Item>
          <Form.Item label="Mã chỗ" name="code" rules={[{ required: true, max: 32 }]}>
            <Input />
          </Form.Item>
          <Form.Item label="Loại" name="type" rules={[{ required: true }]}>
            <Select options={seatTypes.map((t) => ({ value: t, label: typeLabels[t] }))} />
          </Form.Item>
          <Form.Item label="Trạng thái" name="status" rules={[{ required: true }]}>
            <Select options={seatStatuses.map((s) => ({ value: s, label: statusLabels[s].label }))} />
          </Form.Item>
        </>
      )}
    />
  );
}
