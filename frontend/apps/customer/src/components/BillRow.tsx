import { useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { List, theme } from "antd";
import { billLink } from "@cafecr/ui";

/**
 * A list row that opens its bill when it has one (chevron, hover and pressed state, keyboard Enter/Space);
 * rows without a bill code render as plain, non-interactive rows.
 */
export function BillRow({ billCode, extra, children }: { billCode: string | null | undefined; extra: ReactNode; children: ReactNode }) {
  const navigate = useNavigate();
  const { token } = theme.useToken();
  const [hover, setHover] = useState(false);
  const [pressed, setPressed] = useState(false);
  const link = billLink(billCode);

  if (!link) {
    return <List.Item extra={extra}>{children}</List.Item>;
  }

  const background = pressed ? token.colorFillSecondary : hover ? token.colorFillQuaternary : undefined;
  return (
    <List.Item
      role="link" tabIndex={0} aria-label={`Xem hóa đơn ${billCode}`}
      onClick={() => navigate(link)}
      onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); navigate(link); } }}
      onPointerEnter={() => setHover(true)} onPointerLeave={() => { setHover(false); setPressed(false); }}
      onPointerDown={() => setPressed(true)} onPointerUp={() => setPressed(false)}
      style={{ cursor: "pointer", background, borderRadius: token.borderRadius, paddingInline: 8, marginInline: -8, transition: "background .15s" }}
      extra={<span style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>{extra}<span aria-hidden style={{ color: token.colorTextTertiary, fontSize: 20, lineHeight: 1 }}>›</span></span>}
    >
      {children}
    </List.Item>
  );
}
