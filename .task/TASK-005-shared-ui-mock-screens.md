# TASK-005: Xây dựng UI dùng chung và màn hình với dữ liệu giả

- Trạng thái: Chờ xử lý
- Mục tiêu: Triển khai các màn hình ban đầu đã duyệt bằng UI có thể tái sử dụng và dữ liệu nghiệp vụ giả được cô lập rõ ràng.
- Lý do: Có thể phát triển màn hình nghiệp vụ trước khi API tương ứng tồn tại, đồng thời giữ khả năng thay thế dữ liệu mà không phải viết lại toàn bộ UI.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

## Tài liệu quy tắc

- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.github/instructions/frontend.instructions.md](../.github/instructions/frontend.instructions.md)

## Phụ thuộc

- [TASK-002](./TASK-002-frontend-auth-requirements.md)
- [TASK-004](./TASK-004-frontend-workspace.md)

## Phạm vi

- Tạo `frontend/packages/ui` để chứa các component trình bày dùng chung giữa các ứng dụng.
- Chỉ triển khai các màn hình ban đầu và luồng chính đã được duyệt trong TASK-002.
- Dùng model TypeScript và dữ liệu giả cho màn hình nghiệp vụ; đặt tên hoặc cấu trúc module để nhận biết rõ dữ liệu giả.
- Để component phụ thuộc vào ranh giới service/data theo feature, không import trực tiếp mảng dữ liệu giả vào component trình bày.
- Mặc định đặt dữ liệu giả trong từng app/feature; chỉ tạo package dữ liệu giả dùng chung khi đã có nhu cầu tái sử dụng thực tế.
- Bổ sung trạng thái loading, rỗng và lỗi phù hợp với các màn hình đã duyệt.
- Thêm test UI/component tập trung cho các tương tác và hành vi hiển thị quan trọng.

## Ngoài phạm vi

- Không triển khai xác thực giả hoặc coi dữ liệu nghiệp vụ giả là dữ liệu đã lưu/có thẩm quyền.
- Không tự thêm quy tắc nghiệp vụ hay màn hình chưa được duyệt trong TASK-002.
- Không thay ranh giới service dữ liệu giả bằng client Orval hoặc hooks API nghiệp vụ được sinh tự động.

## Tiêu chí chấp nhận

- Các màn hình ban đầu đã duyệt có mặt trong đúng ứng dụng.
- Các ứng dụng sử dụng UI tái sử dụng từ `packages/ui`, không sao chép component.
- Dữ liệu nghiệp vụ được nhận diện rõ là dữ liệu giả và cô lập sau các hàm truy cập theo feature.
- Hành vi UI có test tập trung cho tương tác chính đã thống nhất và các trạng thái rỗng/lỗi liên quan.
- Có thể thay lớp dữ liệu/service giả bằng triển khai thật sau này mà không phải viết lại component trình bày.

## Xác minh

- Chạy test frontend tập trung cho màn hình và UI dùng chung đã thay đổi.
- Chạy kiểm tra kiểu dữ liệu và production build cho tất cả ứng dụng.
- Ghi lại lệnh và kết quả thực tế trong task này.

## Ghi chú triển khai

Task này chỉ xử lý dữ liệu giả cho màn hình nghiệp vụ. Xác thực vẫn phải gọi backend thật và được xử lý trong TASK-003, TASK-006.
