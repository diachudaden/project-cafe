import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Card, Form, Input, Modal, Popconfirm, Select, Space, Switch, Tag, Typography } from "antd";
import type { FloorSeat, SeatType } from "@cafecr/api-client";
import { seatStateStyles, seatTypeLabels } from "@cafecr/ui";
import { api } from "../../api";
import { RequireBranch } from "../../components/RequireBranch";
import { useNotify } from "../../components/useNotify";

export function FloorPage() {
  return <RequireBranch title="Sơ đồ chỗ ngồi">{(branchId) => <FloorEditor branchId={branchId} />}</RequireBranch>;
}

function FloorEditor({ branchId }: { branchId: string }) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const floor = useQuery({ queryKey: ["floor", branchId], queryFn: () => api.catalog.floor(branchId), refetchInterval: 15_000 });
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
  const [zoneName, setZoneName] = useState("");
  const [seatFor, setSeatFor] = useState<{ zoneId: string; seat?: FloorSeat } | null>(null);
  const [form] = Form.useForm<{ code: string; type: SeatType }>();

  const addZone = useMutation({ mutationFn: () => api.catalog.createZone(branchId, zoneName), onSuccess: () => { setZoneName(""); refresh(); }, onError: notify.fail });
  const deleteZone = useMutation({ mutationFn: api.catalog.deleteZone, onSuccess: refresh, onError: notify.fail });
  const saveSeat = useMutation({
    mutationFn: (v: { code: string; type: SeatType }) => seatFor!.seat ? api.catalog.updateSeat(seatFor!.seat.id, v) : api.catalog.createSeat(seatFor!.zoneId, v),
    onSuccess: () => { setSeatFor(null); refresh(); }, onError: notify.fail,
  });
  const deleteSeat = useMutation({ mutationFn: api.catalog.deleteSeat, onSuccess: () => { setSeatFor(null); refresh(); }, onError: notify.fail });
  const maintenance = useMutation({
    mutationFn: ({ id, on }: { id: string; on: boolean }) => api.catalog.setMaintenance(id, on), onSuccess: refresh, onError: notify.fail,
  });

  return (
    <Card title={`Sơ đồ chỗ ngồi · ${floor.data?.branchName ?? ""}`} extra={
      <Space.Compact>
        <Input placeholder="Tên khu vực mới" value={zoneName} onChange={(e) => setZoneName(e.target.value)} />
        <Button disabled={!zoneName.trim()} onClick={() => addZone.mutate()}>Thêm khu</Button>
      </Space.Compact>
    }>
      <Space wrap style={{ marginBottom: 12 }}>
        {Object.entries(seatStateStyles).map(([k, s]) => <Tag key={k} style={{ background: s.background, color: s.color, borderColor: s.color }}>{s.label}</Tag>)}
      </Space>
      {floor.data?.zones.map((zone) => (
        <Card key={zone.id} type="inner" size="small" style={{ marginBottom: 12 }} title={zone.name} extra={
          <Space>
            <Button size="small" onClick={() => { form.setFieldsValue({ code: "", type: "Single" }); setSeatFor({ zoneId: zone.id }); }}>Thêm chỗ</Button>
            <Popconfirm title="Xóa khu vực (phải không còn chỗ)?" onConfirm={() => deleteZone.mutate(zone.id)}><Button size="small" danger>Xóa khu</Button></Popconfirm>
          </Space>
        }>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(140px, 1fr))", gap: 8 }}>
            {zone.seats.map((seat) => {
              const style = seatStateStyles[seat.state];
              return (
                <div key={seat.id} style={{ background: style.background, border: `1px solid ${style.color}`, borderRadius: 8, padding: 8 }}>
                  <Typography.Text strong style={{ color: style.color, cursor: "pointer" }}
                    onClick={() => { form.setFieldsValue({ code: seat.code, type: seat.type }); setSeatFor({ zoneId: zone.id, seat }); }}>
                    {seat.code}
                  </Typography.Text>
                  <div style={{ fontSize: 12 }}>{seatTypeLabels[seat.type]} · {style.label}</div>
                  <Space size={4} style={{ marginTop: 4 }}>
                    <Switch size="small" checked={seat.state === "Maintenance"} disabled={!!seat.session}
                      onChange={(on) => maintenance.mutate({ id: seat.id, on })} />
                    <span style={{ fontSize: 12 }}>Bảo trì</span>
                  </Space>
                </div>
              );
            })}
          </div>
        </Card>
      ))}
      <Modal open={!!seatFor} title={seatFor?.seat ? `Sửa chỗ ${seatFor.seat.code}` : "Thêm chỗ"} onCancel={() => setSeatFor(null)} onOk={() => form.submit()}
        footer={(_, { OkBtn, CancelBtn }) => (
          <Space>
            {seatFor?.seat && <Popconfirm title="Xóa chỗ (chỉ khi chưa từng có khách)?" onConfirm={() => deleteSeat.mutate(seatFor.seat!.id)}><Button danger>Xóa</Button></Popconfirm>}
            <CancelBtn /><OkBtn />
          </Space>
        )}>
        <Form form={form} layout="vertical" onFinish={(v) => saveSeat.mutate(v)}>
          <Form.Item label="Mã chỗ" name="code" rules={[{ required: true, max: 32 }]}><Input /></Form.Item>
          <Form.Item label="Loại" name="type"><Select options={(Object.keys(seatTypeLabels) as SeatType[]).map((t) => ({ value: t, label: seatTypeLabels[t] }))} /></Form.Item>
        </Form>
      </Modal>
    </Card>
  );
}
