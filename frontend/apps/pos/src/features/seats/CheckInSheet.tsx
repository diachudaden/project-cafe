import { useState } from "react";
import { Button } from "@chonrieng/ui";
import type { PaymentChoice } from "./types";

export function CheckInSheet({
  seatId,
  onClose,
  onConfirm,
}: {
  seatId: string;
  onClose: () => void;
  onConfirm: (choice: PaymentChoice) => void;
}) {
  const [choice, setChoice] = useState<PaymentChoice>("cash");

  return (
    <div className="pos-sheet-backdrop" onMouseDown={(event) => {
      if (event.target === event.currentTarget) onClose();
    }}>
      <section
        aria-labelledby="checkin-title"
        aria-modal="true"
        className="pos-sheet"
        role="dialog"
      >
        <div className="pos-sheet__handle" aria-hidden="true" />
        <div className="pos-sheet__heading">
          <div>
            <p className="pos-eyebrow">Bắt đầu lượt mẫu</p>
            <h2 id="checkin-title">Check-in ghế {seatId}</h2>
          </div>
          <button aria-label="Đóng" className="pos-close-button" onClick={onClose} type="button">×</button>
        </div>
        <div className="pos-price-row">
          <span>Giá trải nghiệm mẫu</span>
          <strong>35.000 đ / giờ</strong>
        </div>
        <fieldset className="pos-payment-options">
          <legend>Phương thức thanh toán mẫu</legend>
          <label className={choice === "cash" ? "is-selected" : ""}>
            <input
              checked={choice === "cash"}
              name="payment"
              onChange={() => setChoice("cash")}
              type="radio"
              value="cash"
            />
            <span aria-hidden="true">₫</span>
            <span><strong>Tiền mặt</strong><small>Chọn để minh họa</small></span>
          </label>
          <label className={choice === "vietqr" ? "is-selected" : ""}>
            <input
              checked={choice === "vietqr"}
              name="payment"
              onChange={() => setChoice("vietqr")}
              type="radio"
              value="vietqr"
            />
            <span aria-hidden="true">▦</span>
            <span><strong>VietQR</strong><small>Không tạo giao dịch thật</small></span>
          </label>
        </fieldset>
        <div className="pos-sheet__actions">
          <Button className="cr-button--secondary" onClick={onClose} type="button">Hủy</Button>
          <Button onClick={() => onConfirm(choice)} type="button">Xác nhận mẫu</Button>
        </div>
        <p className="pos-sheet__note">Thao tác chỉ cập nhật trạng thái mẫu trên màn hình hiện tại.</p>
      </section>
    </div>
  );
}
