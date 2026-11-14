import { forwardRef, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Empty, Input, List, Segmented, Skeleton, Space, Typography, type InputRef } from "antd";
import { MinusOutlined, PlusOutlined, SearchOutlined } from "@ant-design/icons";
import type { ProductView } from "@cafecr/api-client";
import { displayName, hasRole, vnd } from "@cafecr/ui";
import { api, useAuth } from "../../api";
import { ALL_CATEGORIES, changeCart, menuState, type Cart, type CartLine } from "./addOnMenu";

/** The branch's sellable menu (shared cache with every add-on screen). */
export function useMenu(branchId: string) {
  return useQuery({ queryKey: ["products", branchId], queryFn: () => api.catalog.products(branchId) });
}

/** − qty + buttons for one product. */
export function Stepper({ cart, productId, onCartChange, size = "large" }: { cart: Cart; productId: string; onCartChange: (cart: Cart) => void; size?: "large" | "small" }) {
  const q = cart[productId] ?? 0;
  return (
    <Space size="small">
      <Button shape="circle" size={size} icon={<MinusOutlined />} disabled={q === 0} onClick={() => onCartChange(changeCart(cart, productId, -1))} aria-label="Bớt" />
      <Typography.Text strong style={{ minWidth: 24, display: "inline-block", textAlign: "center", fontSize: size === "large" ? 18 : 14 }}>{q}</Typography.Text>
      <Button shape="circle" size={size} type={q > 0 ? "primary" : "default"} icon={<PlusOutlined />} onClick={() => onCartChange(changeCart(cart, productId, 1))} aria-label="Thêm" />
    </Space>
  );
}

/** "Đã chọn" chips with small steppers, so a choice can be changed without scrolling back to it. */
export function CartChips({ lines, cart, onCartChange }: { lines: CartLine[]; cart: Cart; onCartChange: (cart: Cart) => void }) {
  if (lines.length === 0) return null;
  return (
    <div>
      <Typography.Text type="secondary">Đã chọn</Typography.Text>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, marginTop: 4, maxHeight: 120, overflowY: "auto" }}>
        {lines.map((l) => (
          <Space key={l.productId} size={4} style={{ border: "1px solid rgba(128,128,128,.3)", borderRadius: 20, padding: "2px 4px 2px 10px" }}>
            <Typography.Text>{displayName(l.name)}</Typography.Text>
            <Stepper cart={cart} productId={l.productId} onCartChange={onCartChange} size="small" />
          </Space>
        ))}
      </div>
    </div>
  );
}

/**
 * Searchable add-on menu with quantity steppers (used by "Thêm đồ" and by the check-in screen). Shows a real error
 * (status + traceId), an empty menu and "no result" as different states.
 */
export const AddOnGrid = forwardRef<InputRef, { branchId: string; cart: Cart; onCartChange: (cart: Cart) => void }>(
  function AddOnGrid({ branchId, cart, onCartChange }, searchRef) {
    const { state: auth } = useAuth();
    const products = useMenu(branchId);
    const [category, setCategory] = useState(ALL_CATEGORIES);
    const [search, setSearch] = useState("");
    const categories = useMemo(() => [...new Set((products.data ?? []).map((p) => p.category))], [products.data]);
    const view = menuState({ isLoading: products.isLoading, error: products.error, products: products.data, term: search, category });
    const canManageMenu = hasRole(auth.user, "Admin", "BranchManager");

    let body;
    switch (view.kind) {
      case "loading":
        body = <Skeleton active paragraph={{ rows: 6 }} />;
        break;
      case "error":
        body = (
          <Alert type="error" showIcon message="Không tải được thực đơn"
            description={<>{view.message}{view.diagnostics && <div><Typography.Text type="secondary" copyable style={{ fontSize: 12 }}>{view.diagnostics}</Typography.Text></div>}</>}
            action={<Button onClick={() => void products.refetch()} loading={products.isFetching}>Thử lại</Button>} />
        );
        break;
      case "empty":
        body = (
          <Empty description="Chi nhánh chưa có món nào đang bán.">
            {canManageMenu
              ? <Button href="/admin/pricing" target="_blank" rel="noopener">Thêm món ở Bảng giá</Button>
              : <Typography.Text type="secondary">Nhờ quản lý thêm món.</Typography.Text>}
          </Empty>
        );
        break;
      case "noResult":
        body = (
          <Empty description={`Không tìm thấy “${view.term}”`}>
            <Button onClick={() => setSearch("")}>Xóa tìm kiếm</Button>
          </Empty>
        );
        break;
      case "list":
        body = (
          <>
            {view.searching && <Typography.Text type="secondary">{view.items.length} món</Typography.Text>}
            <List
              dataSource={view.items}
              renderItem={(p: ProductView) => (
                <List.Item actions={[<Stepper key="q" cart={cart} productId={p.id} onCartChange={onCartChange} />]}>
                  <List.Item.Meta title={displayName(p.name)} description={`${vnd(p.price)} · ${p.category}`} />
                </List.Item>
              )}
            />
          </>
        );
        break;
    }

    return (
      <Space direction="vertical" style={{ width: "100%" }} size="middle">
        <Input ref={searchRef} allowClear prefix={<SearchOutlined />} placeholder="Tìm món (vd: mi, trung op, uong)" value={search}
          onChange={(e) => setSearch(e.target.value)} disabled={view.kind === "loading" || view.kind === "error"} />
        {categories.length > 1 && (
          <Segmented block value={search.trim() ? ALL_CATEGORIES : category} disabled={!!search.trim()} onChange={(v) => setCategory(v as string)}
            options={[{ value: ALL_CATEGORIES, label: "Tất cả" }, ...categories.map((c) => ({ value: c, label: c }))]} />
        )}
        {body}
      </Space>
    );
  },
);
