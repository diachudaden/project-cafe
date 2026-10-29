import { EmptyFeature, PageContent, PageHeading } from "@chonrieng/ui";

export function ScanPage() {
  return (
    <PageContent className="customer-page-content">
      <PageHeading
        eyebrow="Hóa đơn"
        title="Quét bill"
        description="Tính năng quét hóa đơn chưa được bật trong bản xem trước."
      />
      <EmptyFeature title="Quét bill" />
    </PageContent>
  );
}
