import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Card, Input, Result, Typography } from "antd";
import type { ClaimResult } from "@cafecr/api-client";
import { errorText, tierLabels, vnd } from "@cafecr/ui";
import { api } from "../api";

export function ClaimPage() {
  const queryClient = useQueryClient();
  const [code, setCode] = useState("");
  const [result, setResult] = useState<ClaimResult | null>(null);
  const claim = useMutation({
    mutationFn: () => api.loyalty.claim(code),
    onSuccess: (r) => { setResult(r); setCode(""); void queryClient.invalidateQueries({ queryKey: ["points"] }); },
  });

  return (
    <div style={{ padding: 16 }}>
      <Typography.Title level={3}>Nhập bill tích điểm</Typography.Title>
      {result ? (
        <Result status="success" title={`+${result.pointsEarned} điểm`}
          subTitle={`Bill ${result.code} · ${vnd(result.total)}. Số dư: ${result.balance} điểm · Hạng ${tierLabels[result.tier]?.label ?? result.tier}`}
          extra={<Button type="primary" onClick={() => setResult(null)}>Nhập bill khác</Button>} />
      ) : (
        <Card>
          <Typography.Paragraph type="secondary">Nhập mã 8 ký tự in trên hóa đơn. Mỗi bill tích điểm một lần, trong 7 ngày. 1.000đ = 1 điểm.</Typography.Paragraph>
          <Input
            size="large" maxLength={8} value={code} placeholder="VD: K7M2Q9XA" autoCapitalize="characters"
            style={{ letterSpacing: 6, fontSize: 24, textAlign: "center", textTransform: "uppercase" }}
            onChange={(e) => setCode(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ""))}
            onPressEnter={() => code.length === 8 && claim.mutate()}
          />
          {claim.isError && <Alert style={{ marginTop: 12 }} type="error" showIcon message={errorText(claim.error)} />}
          <Button type="primary" block style={{ marginTop: 12 }} disabled={code.length !== 8} loading={claim.isPending} onClick={() => claim.mutate()}>
            Tích điểm
          </Button>
        </Card>
      )}
    </div>
  );
}
