import { Badge, PageContent, PageHeading } from "@chonrieng/ui";

export function PointsPage() {
  return (
    <PageContent className="customer-page-content">
      <PageHeading
        eyebrow="Chương trình thành viên"
        title="Điểm của bạn"
        description="Thông tin điểm trong bản xem trước là dữ liệu mẫu, chưa phải số dư tài khoản."
      />
      <section className="customer-loyalty-card">
        <div>
          <span className="customer-loyalty-card__label">Điểm minh họa</span>
          <strong>1.280</strong>
          <span className="customer-loyalty-card__unit">điểm mẫu</span>
        </div>
        <span className="customer-loyalty-card__stamp" aria-hidden="true">✦</span>
        <Badge tone="warning">Chỉ dùng để xem trước</Badge>
      </section>
      <section className="cr-card customer-points-empty">
        <span aria-hidden="true">✧</span>
        <h2>Lịch sử điểm mẫu</h2>
        <p>Chưa có giao dịch điểm nào trong dữ liệu minh họa.</p>
      </section>
    </PageContent>
  );
}
