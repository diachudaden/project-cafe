import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Alert, Button, Modal, Result, Space, Spin, Tag, Typography } from "antd";
import type { PaymentRequestView } from "@cafecr/api-client";
import { errorText, vnd } from "@cafecr/ui";
import { api } from "../../api";
import { useNotify } from "../useNotify";
import { isPaymentTerminal } from "./paymentStatus";
import { PaymentDoneResult } from "./PaymentDoneModal";

function remainingTime(expiresAt: string, now: number): string {
  const seconds = Math.max(0, Math.ceil((Date.parse(expiresAt) - now) / 1000));
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}

export function PaymentRequestModal({
  branchId,
  request,
  title,
  description,
  what,
  onClose,
}: {
  branchId: string;
  request: PaymentRequestView;
  title: string;
  description: string;
  /** What is being paid for, shown on the success screen: "Check-in", "Gia hạn", "Đồ thêm". */
  what: "Check-in" | "Gia hạn" | "Đồ thêm";
  onClose: () => void;
}) {
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [now, setNow] = useState(Date.now());
  const [qrFailed, setQrFailed] = useState(false);
  const status = useQuery({
    queryKey: ["payment-request-status", request.code],
    queryFn: () => api.payments.status(request.code),
    // Keep polling while pending, and also after a failed call (data still undefined): a single network
    // error on the first poll must not leave the cashier on a QR screen that never updates.
    refetchInterval: (query) => {
      const current = query.state.data?.status;
      if (current === undefined) return query.state.status === "error" ? 5000 : 2500;
      return current === "Pending" ? 2500 : false;
    },
    retry: false,
  });
  const paymentStatus = status.data?.status;
  const billCode = status.data?.billCode;
  const session = useQuery({
    queryKey: ["payment-request-session", request.code, billCode],
    queryFn: () => api.sessions.bill(billCode!),
    enabled: paymentStatus === "Paid" && !!billCode,
    retry: false,
  });
  const cancel = useMutation({
    mutationFn: () => api.payments.cancel(request.code),
    onSuccess: async () => {
      await status.refetch();
    },
    onError: async (error) => {
      const current = await status.refetch();
      if (current.data?.status === "Pending") notify.fail(error);
    },
  });

  useEffect(() => {
    if (paymentStatus !== "Pending") return;
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, [paymentStatus]);

  useEffect(() => {
    if (paymentStatus === "Paid") {
      void queryClient.invalidateQueries({ queryKey: ["floor", branchId] });
      void queryClient.invalidateQueries({ queryKey: ["current-shift"] });
      // Add-on and extension lines are on the seat's bill (SeatActionsSheet: ["seat-bill", billCode]).
      void queryClient.invalidateQueries({ queryKey: ["seat-bill"] });
    }
  }, [branchId, paymentStatus, queryClient]);

  const terminal = isPaymentTerminal(paymentStatus);
  const close = () => {
    if (terminal) onClose();
  };
  const cancelRequest = () => Modal.confirm({
    title: "Hủy yêu cầu thanh toán?",
    content: "Nếu khách đã chuyển tiền, hệ thống sẽ giữ nguyên kết quả thanh toán đã xác nhận.",
    okText: "Hủy yêu cầu",
    cancelText: "Tiếp tục chờ",
    okButtonProps: { danger: true },
    onOk: () => cancel.mutateAsync(),
  });

  let content;
  if (status.isError) {
    content = (
      <Alert
        type="error"
        showIcon
        message="Không kiểm tra được trạng thái thanh toán"
        description={errorText(status.error)}
        action={<Button onClick={() => void status.refetch()}>Thử lại</Button>}
      />
    );
  } else if (paymentStatus === "Paid") {
    content = session.data ? (
      <PaymentDoneResult done={{
        what,
        seatCode: session.data.seatCode,
        amount: request.amount,
        method: "VietQr",
        billCode: session.data.bill.code,
        billTotal: session.data.bill.total,
        endAt: what === "Đồ thêm" ? undefined : session.data.endAt,
        customerName: status.data?.customerName ?? session.data.customer?.fullName,
        pointsAwarded: status.data?.pointsAwarded ?? 0,
      }} />
    ) : session.isError || !billCode ? (
      <Alert
        type="warning"
        showIcon
        message="Đã nhận thanh toán"
        description={session.isError ? errorText(session.error) : "Chưa nhận được mã bill từ máy chủ."}
        action={<Button onClick={() => void session.refetch()}>Tải lại bill</Button>}
      />
    ) : <Spin tip="Đang tải bill đã thanh toán..." />;
  } else if (paymentStatus === "Expired") {
    content = <Result status="warning" title="Yêu cầu đã hết hạn" subTitle="Hãy tạo yêu cầu VietQR mới để tiếp tục." />;
  } else if (paymentStatus === "Cancelled") {
    content = <Result status="info" title="Yêu cầu đã được hủy" subTitle="Chưa ghi nhận thanh toán cho yêu cầu này." />;
  } else if (paymentStatus === "NeedsAttention") {
    content = <Result status="warning" title="Cần kiểm tra giao dịch" subTitle="Đã nhận thông tin giao dịch nhưng chưa thể tự động hoàn tất bill. Vui lòng báo quản lý." />;
  } else {
    content = (
      <Space direction="vertical" size="middle" align="center" style={{ width: "100%" }}>
        {!qrFailed
          ? <img src={request.qrUrl} alt="Mã QR thanh toán VietQR" onError={() => setQrFailed(true)}
            style={{ width: 240, height: 240, objectFit: "contain" }} />
          : <Alert type="warning" showIcon message="Không tải được ảnh QR" description="Khách có thể nhập mã chuyển khoản bên dưới nếu đang dùng tài khoản kiểm thử." />}
        <Typography.Title level={3} style={{ margin: 0 }}>{vnd(request.amount)}</Typography.Title>
        <Typography.Text>{description}</Typography.Text>
        <Typography.Text copyable strong style={{ fontSize: 22, letterSpacing: 3 }}>{request.code}</Typography.Text>
        <Tag color="processing">Đang chờ thanh toán · còn {remainingTime(request.expiresAt, now)}</Tag>
        <Typography.Text type="secondary" style={{ textAlign: "center" }}>
          Giữ nguyên nội dung chuyển khoản để hệ thống tự đối soát. Bill chỉ được tạo sau khi xác nhận thanh toán.
        </Typography.Text>
      </Space>
    );
  }

  return (
    <Modal
      open
      centered
      width={460}
      title={title}
      footer={terminal
        ? <Button type="primary" onClick={close}>Đóng</Button>
        : <Button danger loading={cancel.isPending} onClick={cancelRequest}>Hủy yêu cầu</Button>}
      closable={terminal}
      maskClosable={terminal}
      keyboard={terminal}
      onCancel={close}
    >
      {content}
    </Modal>
  );
}
