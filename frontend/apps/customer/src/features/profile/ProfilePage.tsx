import { Badge, PageContent, PageHeading } from "@chonrieng/ui";
import { useAuth } from "@chonrieng/auth";

export function ProfilePage() {
  const { user } = useAuth();

  return (
    <PageContent className="customer-page-content">
      <PageHeading
        eyebrow="Tài khoản"
        title="Tôi"
        description="Thông tin tài khoản được lấy từ phiên đăng nhập thật."
      />
      <section className="cr-card customer-profile-card">
        <div className="customer-profile-avatar" aria-hidden="true">
          {user?.fullName.trim().charAt(0).toUpperCase()}
        </div>
        <div>
          <Badge tone="success">{user?.role}</Badge>
          <h2>{user?.fullName}</h2>
          <p>{user?.phone ?? user?.email}</p>
        </div>
      </section>
      <section className="cr-card customer-loyalty-summary">
        <div>
          <span>Điểm thành viên · dữ liệu mẫu</span>
          <strong>1.280 điểm</strong>
        </div>
        <span aria-hidden="true">✦</span>
      </section>
    </PageContent>
  );
}
