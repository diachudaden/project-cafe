# Backlog công việc

Backlog này được giữ ở quy mô nhỏ, bám sát yêu cầu. Backlog ghi lại các task đang hoạt động và quan hệ phụ thuộc, không bổ sung công việc giả định ngoài phạm vi hiện tại của dự án.

## Các task hiện tại

| Mã | Trạng thái | Tiêu đề | Ghi chú |
| --- | --- | --- | --- |
| TASK-001 | Hoàn thành | [Thiết lập quy trình làm việc GitHub Copilot cho Cafe CR](./TASK-001-setup.md) | Tạo các quy tắc, prompt và cấu trúc task ở cấp repository. |
| TASK-002 | Chờ xử lý | [Xác định yêu cầu frontend và xác thực](./TASK-002-frontend-auth-requirements.md) | Làm rõ ranh giới dữ liệu giả, phạm vi màn hình ban đầu và hợp đồng đăng nhập thật trước khi triển khai. |
| TASK-003 | Chờ xử lý | [Triển khai API xác thực thật](./TASK-003-real-authentication-api.md) | Bổ sung hành vi xác thực backend và hợp đồng OpenAPI sau khi yêu cầu được duyệt. Phụ thuộc TASK-002. |
| TASK-004 | Chờ xử lý | [Tạo frontend workspace](./TASK-004-frontend-workspace.md) | Thiết lập workspace Vite, React, TypeScript và ba ứng dụng có thể chạy độc lập. |
| TASK-005 | Chờ xử lý | [Xây dựng UI dùng chung và màn hình với dữ liệu giả](./TASK-005-shared-ui-mock-screens.md) | Triển khai các màn hình đã được duyệt bằng UI dùng chung và dữ liệu giả được cô lập rõ ràng. Phụ thuộc TASK-002 và TASK-004. |
| TASK-006 | Chờ xử lý | [Tích hợp đăng nhập thật vào các ứng dụng](./TASK-006-real-sign-in-integration.md) | Kết nối giao diện đăng nhập dùng chung của từng ứng dụng với API xác thực backend. Phụ thuộc TASK-003, TASK-004 và TASK-005. |
| TASK-007 | Chờ xử lý | [Xác minh và tài liệu hóa frontend](./TASK-007-frontend-rollout-verification.md) | Xác minh cả ba ứng dụng và luồng đăng nhập thật, sau đó ghi lại cách thiết lập cùng giới hạn của dữ liệu giả. Phụ thuộc TASK-006. |

## Quy tắc quản lý task

- Liên kết các task với đặc tả chuẩn trong [../spec.md](../spec.md).
- Ưu tiên task có phạm vi hẹp, tiêu chí chấp nhận rõ ràng và lệnh xác minh cụ thể.
- Nếu yêu cầu thiếu hoặc mâu thuẫn, đánh dấu là bị chặn thay vì tự suy đoán.
