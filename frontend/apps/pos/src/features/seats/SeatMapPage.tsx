import { useEffect, useMemo, useState } from "react";
import { PageContent, PageHeading, StatePanel } from "@chonrieng/ui";
import { CheckInSheet } from "./CheckInSheet";
import { getMockSeats } from "./services/seat-data";
import type { PaymentChoice, PosSeat, SeatStatus } from "./types";

type SeatsState =
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; data: PosSeat[] };

const statusLabels: Record<SeatStatus, string> = {
  empty: "Còn trống",
  occupied: "Đang sử dụng",
  expiring: "Sắp hết giờ",
  maintenance: "Bảo trì",
};

export function SeatMapPage() {
  const [state, setState] = useState<SeatsState>({ status: "loading" });
  const [selectedSeat, setSelectedSeat] = useState<string | null>(null);
  const [notice, setNotice] = useState("");
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;
    getMockSeats()
      .then((data) => {
        if (active) setState({ status: "ready", data });
      })
      .catch((error: unknown) => {
        if (active) {
          setState({
            status: "error",
            message: error instanceof Error ? error.message : "Không tải được sơ đồ ghế mẫu.",
          });
        }
      });
    return () => {
      active = false;
    };
  }, [reloadKey]);

  const counts = useMemo(() => {
    if (state.status !== "ready") return null;
    return state.data.reduce(
      (result, seat) => {
        result[seat.status] += 1;
        return result;
      },
      { empty: 0, occupied: 0, expiring: 0, maintenance: 0 },
    );
  }, [state]);

  const confirmCheckIn = (choice: PaymentChoice) => {
    if (state.status !== "ready" || !selectedSeat) return;

    setState({
      status: "ready",
      data: state.data.map((seat) =>
        seat.id === selectedSeat
          ? { ...seat, status: "occupied", guestLabel: "Lượt mẫu vừa tạo", timeLabel: "Vừa check-in" }
          : seat,
      ),
    });
    setNotice(`Đã cập nhật ghế ${selectedSeat} trong trạng thái mẫu · ${choice === "cash" ? "Tiền mặt" : "VietQR"}.`);
    setSelectedSeat(null);
  };

  return (
    <PageContent className="pos-page-content">
      <section className="pos-shift-card" aria-label="Thông tin ca mẫu">
        <div>
          <span className="pos-eyebrow">Ngữ cảnh ca hiện tại · Mẫu</span>
          <strong>Ca chiều · 14:00 - 22:00</strong>
        </div>
        <div>
          <span className="pos-eyebrow">Tiền mặt tại quầy · Mẫu</span>
          <strong>1.250.000 đ</strong>
        </div>
      </section>

      <PageHeading
        eyebrow="Khu vực phục vụ"
        title="Sơ đồ ghế"
        description="Chọn một ghế trống để xem luồng check-in minh họa."
      />

      {notice && <p className="pos-inline-notice" role="status">{notice}</p>}

      {state.status === "loading" && <StatePanel state="loading" title="Đang tải sơ đồ ghế mẫu..." />}
      {state.status === "error" && (
        <StatePanel
          state="error"
          title="Không tải được sơ đồ ghế"
          description={state.message}
          onRetry={() => setReloadKey((key) => key + 1)}
        />
      )}
      {state.status === "ready" && state.data.length === 0 && (
        <StatePanel state="empty" title="Chưa có ghế mẫu" />
      )}
      {state.status === "ready" && counts && state.data.length > 0 && (
        <>
          <div className="pos-seat-legend" aria-label="Chú thích trạng thái ghế">
            <span><i className="is-empty" /> Còn trống <strong>{counts.empty}</strong></span>
            <span><i className="is-occupied" /> Đang sử dụng <strong>{counts.occupied}</strong></span>
            <span><i className="is-expiring" /> Sắp hết giờ <strong>{counts.expiring}</strong></span>
            <span><i className="is-maintenance" /> Bảo trì <strong>{counts.maintenance}</strong></span>
          </div>
          {["Tầng trệt", "Tầng lửng"].map((zone) => {
            const seats = state.data.filter((seat) => seat.zone === zone);
            if (seats.length === 0) return null;
            return (
              <section className="pos-zone" key={zone}>
                <h2>{zone}</h2>
                <div className="pos-seat-grid">
                  {seats.map((seat) => (
                    <button
                      aria-label={`Ghế ${seat.id}: ${statusLabels[seat.status]}`}
                      className={`pos-seat pos-seat--${seat.status}`}
                      disabled={seat.status !== "empty"}
                      key={seat.id}
                      onClick={() => setSelectedSeat(seat.id)}
                      type="button"
                    >
                      <span className="pos-seat__icon" aria-hidden="true">{seat.status === "maintenance" ? "⌁" : "⌂"}</span>
                      <strong>{seat.id}</strong>
                      <span>{statusLabels[seat.status]}</span>
                      {seat.timeLabel && <small>{seat.timeLabel}</small>}
                    </button>
                  ))}
                </div>
              </section>
            );
          })}
        </>
      )}

      {selectedSeat && (
        <CheckInSheet
          onClose={() => setSelectedSeat(null)}
          onConfirm={confirmCheckIn}
          seatId={selectedSeat}
        />
      )}
    </PageContent>
  );
}
