import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Card, Empty, Space, Spin, Tag, Typography } from "antd";
import { seatTypeLabels, vnd } from "@cafecr/ui";
import { api } from "../api";

export function FindPage() {
  const [position, setPosition] = useState<{ lat: number; lng: number } | null>(null);
  const [locating, setLocating] = useState(false);
  const branches = useQuery({
    queryKey: ["branches", position],
    queryFn: () => api.catalog.branches(position?.lat, position?.lng),
    refetchInterval: 30_000,
  });
  const [openId, setOpenId] = useState<string | null>(null);
  const plans = useQuery({ queryKey: ["plans", openId], queryFn: () => api.catalog.activePlans(openId!), enabled: !!openId });

  const locate = () => {
    setLocating(true);
    navigator.geolocation?.getCurrentPosition(
      (p) => { setPosition({ lat: p.coords.latitude, lng: p.coords.longitude }); setLocating(false); },
      () => setLocating(false),
      { timeout: 8000 },
    );
  };

  return (
    <div style={{ padding: 16 }}>
      <Space style={{ width: "100%", justifyContent: "space-between", marginBottom: 12 }}>
        <Typography.Title level={3} style={{ margin: 0 }}>Tìm quán còn chỗ</Typography.Title>
        <Button loading={locating} onClick={locate}>Gần tôi</Button>
      </Space>
      {branches.isLoading && <Spin />}
      {branches.data?.length === 0 && <Empty description="Chưa có chi nhánh." />}
      <Space direction="vertical" style={{ width: "100%" }}>
        {branches.data?.map((b) => {
          const free = b.availability.reduce((s, a) => s + a.free, 0);
          return (
            <Card key={b.id} size="small" onClick={() => setOpenId(openId === b.id ? null : b.id)} style={{ cursor: "pointer" }}>
              <Space style={{ width: "100%", justifyContent: "space-between" }} align="start">
                <div>
                  <Typography.Text strong>{b.name.replace("[DEMO DATA] ", "")}</Typography.Text>
                  <div style={{ fontSize: 13, color: "#8c8c8c" }}>{b.address}{b.distanceKm != null && ` · ${b.distanceKm} km`}</div>
                  <div style={{ fontSize: 13 }}>{b.is24h ? "Mở 24/7" : `${b.openTime.slice(0, 5)} – ${b.closeTime.slice(0, 5)}`} · {b.openNow ? <Tag color="green">Đang mở</Tag> : <Tag>Đã đóng</Tag>}</div>
                </div>
                <Tag color={free > 0 ? "green" : "red"} style={{ fontSize: 14 }}>{free} chỗ trống</Tag>
              </Space>
              <Space wrap style={{ marginTop: 8 }}>
                {b.availability.map((a) => <Tag key={a.type}>{seatTypeLabels[a.type]}: {a.free}/{a.total}</Tag>)}
              </Space>
              {openId === b.id && (
                <div style={{ marginTop: 8 }}>
                  {plans.data?.map((p) => (
                    <div key={p.id}>{p.durationMinutes / 60} tiếng kèm 1 ly tự pha: <b>{vnd(p.price)}</b> · gia hạn {vnd(p.extendPrice)}</div>
                  ))}
                  {b.phone && <a href={`tel:${b.phone}`}>Gọi {b.phone}</a>}
                  {b.latitude != null && b.longitude != null && (
                    <> · <a href={`https://www.openstreetmap.org/?mlat=${b.latitude}&mlon=${b.longitude}#map=17/${b.latitude}/${b.longitude}`} target="_blank" rel="noreferrer">Xem bản đồ</a></>
                  )}
                </div>
              )}
            </Card>
          );
        })}
      </Space>
    </div>
  );
}
