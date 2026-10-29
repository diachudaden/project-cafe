# Backlog công việc

Backlog này được giữ ở quy mô nhỏ, bám sát yêu cầu. Backlog ghi lại các task đang hoạt động và quan hệ phụ thuộc, không bổ sung công việc giả định ngoài phạm vi hiện tại của dự án.

## Các task hiện tại

| Mã | Trạng thái | Tiêu đề | Ghi chú |
| --- | --- | --- | --- |
| TASK-001 | Hoàn thành | [Thiết lập quy trình làm việc GitHub Copilot cho Cafe CR](./TASK-001-setup.md) | Tạo các quy tắc, prompt và cấu trúc task ở cấp repository. |
| TASK-002 | Hoàn thành | [Xác định yêu cầu frontend và xác thực](./TASK-002-frontend-auth-requirements.md) | Phạm vi màn hình, ranh giới dữ liệu giả và hợp đồng đăng nhập thật đã ghi trong spec.md. OTP/PIN/đăng ký vẫn là điểm chặn. |
| TASK-003 | Hoàn thành | [Triển khai API xác thực thật](./TASK-003-real-authentication-api.md) | Bốn endpoint auth, hợp đồng OpenAPI, CORS/cookie, test tích hợp và xác minh build đã hoàn tất. Phụ thuộc TASK-002. |
| TASK-004 | Hoàn thành | [Tạo frontend workspace](./TASK-004-frontend-workspace.md) | Ba app Vite/React/TypeScript đã type-check, build và được xác minh chạy trên các cổng riêng. |
| TASK-005 | Hoàn thành | [Xây dựng UI dùng chung và màn hình với dữ liệu giả](./TASK-005-shared-ui-mock-screens.md) | Package UI, màn hình nghiệp vụ dữ liệu mẫu cho cả ba app và test tương tác đã hoàn tất. Phụ thuộc TASK-002 và TASK-004. |
| TASK-006 | Hoàn thành | [Tích hợp đăng nhập thật vào các ứng dụng](./TASK-006-real-sign-in-integration.md) | Auth UI/service dùng chung, session refresh, `/me`, logout và bảo vệ route đã hoàn tất; backend integration đã xác minh cho cả ba client. |
| TASK-007 | Hoàn thành | [Xác minh và tài liệu hóa frontend](./TASK-007-frontend-rollout-verification.md) | Ba app đã type-check, test, build và được xác minh đăng nhập/refresh/logout với backend; hướng dẫn thiết lập và ranh giới dữ liệu mẫu đã được cập nhật. Phụ thuộc TASK-006. |

## Quy tắc quản lý task

- Liên kết các task với đặc tả chuẩn trong [../spec.md](../spec.md).
- Ưu tiên task có phạm vi hẹp, tiêu chí chấp nhận rõ ràng và lệnh xác minh cụ thể.
- Nếu yêu cầu thiếu hoặc mâu thuẫn, đánh dấu là bị chặn thay vì tự suy đoán.
