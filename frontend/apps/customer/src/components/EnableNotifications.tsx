import { useState } from "react";
import { Alert, Button } from "antd";

/** Asks for browser notification permission only when the customer taps the button (never on load). */
export function EnableNotifications() {
  const supported = typeof window !== "undefined" && "Notification" in window;
  const [permission, setPermission] = useState<NotificationPermission | "unsupported">(supported ? Notification.permission : "unsupported");

  if (permission === "unsupported") return null;
  if (permission === "granted") {
    return <Alert type="success" showIcon message="Đã bật thông báo trên trình duyệt này." style={{ marginTop: 12 }} />;
  }
  if (permission === "denied") {
    return <Alert type="info" showIcon message="Trình duyệt đang chặn thông báo. Bạn vẫn nhận thông báo trong ứng dụng; có thể bật lại trong cài đặt trang." style={{ marginTop: 12 }} />;
  }
  return (
    <Button block style={{ marginTop: 12 }} onClick={() => void Notification.requestPermission().then(setPermission)}>
      🔔 Bật thông báo
    </Button>
  );
}
