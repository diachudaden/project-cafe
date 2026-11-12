import { useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Badge, Button, Layout, Segmented, Space, Typography } from "antd";
import { ThemeToggle, shiftTypeLabels, time, vnd } from "@cafecr/ui";
import { api, useAuth } from "./api";
import { ClosingWizard } from "./features/counter/ClosingWizard";

export function useCurrentShift() {
  return useQuery({ queryKey: ["current-shift"], queryFn: api.attendance.current, refetchInterval: 30_000 });
}

/** Top bar: current shift, cash held and the "Kết ca" button are always visible (reference UX). */
export function PosLayout() {
  const { state, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const current = useCurrentShift();
  const feed = useQuery({ queryKey: ["notifications"], queryFn: api.notifications.feed, refetchInterval: 30_000 });
  const [closing, setClosing] = useState(false);
  const shift = current.data;

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Layout.Header style={{ background: "#2b1d12", display: "flex", alignItems: "center", gap: 16, paddingInline: 16, height: "auto", minHeight: 64, flexWrap: "wrap" }}>
        <Typography.Text strong style={{ color: "#fff", fontSize: 18 }}>Chốn Riêng POS</Typography.Text>
        {shift ? (
          <Typography.Text style={{ color: "#f5e6d3" }}>
            {shift.branchName.replace("[DEMO DATA] ", "")} · {shiftTypeLabels[shift.type]} {time(shift.startAt)}–{time(shift.endAt)} · Tiền mặt đang giữ: <b>{vnd(shift.cashHeld)}</b>
          </Typography.Text>
        ) : (
          <Typography.Text style={{ color: "#f5e6d3" }}>Chưa tham gia ca</Typography.Text>
        )}
        <div style={{ flex: 1 }} />
        {shift && <Button danger type="primary" onClick={() => setClosing(true)}>Kết ca</Button>}
        <Typography.Text style={{ color: "#fff" }}>{state.user?.fullName.replace("[DEMO DATA] ", "")}</Typography.Text>
        <ThemeToggle ghost />
        <Button ghost onClick={() => void logout()}>Đăng xuất</Button>
      </Layout.Header>
      <div style={{ padding: "12px 16px 0" }}>
        <Segmented
          block
          value={location.pathname}
          onChange={(v) => navigate(v as string)}
          options={[
            { value: "/", label: "Quầy" },
            { value: "/schedule", label: "Lịch của tôi" },
            { value: "/payroll", label: "Lương" },
            { value: "/activity", label: <Space size={4}>Hoạt động <Badge count={feed.data?.unread ?? 0} size="small" /></Space> },
          ]}
        />
      </div>
      <Layout.Content style={{ padding: 16 }}>
        <Outlet />
      </Layout.Content>
      {shift && closing && (
        <ClosingWizard shiftId={shift.shiftId} onClose={() => setClosing(false)} onDone={() => { setClosing(false); void logout(); }} />
      )}
    </Layout>
  );
}
