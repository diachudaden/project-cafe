# TASK-004: Tạo frontend workspace

- Trạng thái: Chờ xử lý
- Mục tiêu: Thiết lập workspace Vite, React và TypeScript có thể duy trì, gồm các ứng dụng `admin`, `pos` và `customer`.
- Lý do: Thư mục `frontend` hiện chưa có workspace hoặc ứng dụng nào có thể chạy.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

## Tài liệu quy tắc

- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/coding-style.md](../.rule/coding-style.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.github/instructions/frontend.instructions.md](../.github/instructions/frontend.instructions.md)

## Phụ thuộc

- Không có. Có thể dựng bộ khung này song song trong lúc xác nhận yêu cầu của TASK-002.

## Phạm vi

- Cấu hình package manager và workspace bên trong `frontend`.
- Tạo các ứng dụng Vite + React + TypeScript tại `frontend/apps/admin`, `frontend/apps/pos` và `frontend/apps/customer`.
- Mỗi ứng dụng có entry point, thiết lập route ban đầu và lệnh phát triển/build riêng.
- Bổ sung các quy ước TypeScript, lint, định dạng và build dùng chung khi giúp giảm lặp lại.
- Thêm cấu hình URL API theo môi trường, không commit secrets dành riêng cho môi trường.
- Thêm README tối thiểu cho package hoặc cập nhật README dự án với hướng dẫn cài đặt, chạy và build.

## Ngoài phạm vi

- Không thêm màn hình nghiệp vụ hoặc dữ liệu nghiệp vụ giả trong task này.
- Không triển khai xác thực hoặc giả lập đăng nhập thành công.
- Không thêm Orval, API client được sinh tự động hoặc TanStack Query hooks được sinh cho API nghiệp vụ.

## Tiêu chí chấp nhận

- Cả ba ứng dụng có entry point Vite/React/TypeScript hợp lệ.
- Có thể khởi động và build từng ứng dụng bằng các lệnh được tài liệu hóa.
- Cấu hình workspace dùng chung nhất quán và an toàn kiểu dữ liệu.
- Cấu hình API lấy theo môi trường và không chứa secrets đã commit.

## Xác minh

- Cài dependency bằng package manager đã chọn.
- Chạy kiểm tra kiểu dữ liệu và production build cho cả ba ứng dụng.
- Ghi lại chính xác lệnh và kết quả trong task này và README.

## Ghi chú triển khai

Giữ workspace trong thư mục `frontend` hiện có và sử dụng cấu trúc `apps/`, `packages/`. Chọn công cụ workspace đơn giản nhất đáp ứng được các ứng dụng và package dùng chung cần thiết.
