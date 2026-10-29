# TASK-007: Xác minh và tài liệu hóa frontend

- Trạng thái: Hoàn thành
- Mục tiêu: Chứng minh cả ba ứng dụng chạy được và đăng nhập gọi API thật, sau đó ghi lại cách thiết lập cùng giới hạn của dữ liệu nghiệp vụ giả.
- Lý do: Chỉ có thể xem dự án hoàn thành khi các ứng dụng và luồng đăng nhập thật đã được xác minh bằng kết quả thực tế mới nhất.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

## Tài liệu quy tắc

- [../.rule/testing.md](../.rule/testing.md)
- [../.rule/security.md](../.rule/security.md)

## Phụ thuộc

- [TASK-006](./TASK-006-real-sign-in-integration.md)

## Phạm vi

- Chạy các lệnh cài đặt, kiểm tra kiểu dữ liệu, lint, build và test tập trung đã được tài liệu hóa cho frontend.
- Xác minh `admin`, `pos` và `customer` đều có thể khởi động và build.
- Kiểm tra đăng nhập thật thành công, thông tin đăng nhập sai và backend không khả dụng; kiểm tra tải lại phiên/đăng xuất nếu hợp đồng đã duyệt có yêu cầu.
- Kiểm tra dữ liệu nghiệp vụ giả được đánh dấu/tài liệu hóa và không bị trình bày như dữ liệu backend đã lưu.
- Cập nhật README với hướng dẫn thiết lập, cấu hình môi trường, lệnh chạy/build từng app, yêu cầu để backend auth hoạt động và giới hạn dữ liệu giả.
- Ghi kết quả lệnh và các điểm chặn còn lại vào task và backlog.

## Ngoài phạm vi

- Không thêm tính năng sản phẩm hoặc endpoint backend mới trong task xác minh.
- Không tuyên bố đã tích hợp API thật chỉ dựa trên test giả lập hoặc frontend build thành công.

## Tiêu chí chấp nhận

- Cả ba ứng dụng vượt qua các bước kiểm tra kiểu dữ liệu và production build được tài liệu hóa.
- Test tập trung đạt yêu cầu và luồng đăng nhập thật đã được xác minh với backend.
- Ranh giới dữ liệu nghiệp vụ giả và các giới hạn đã biết được nêu rõ trong tài liệu dự án.
- Kết quả xác minh được ghi lại kèm lệnh và kết quả thực tế.
- Mọi bước kiểm tra thất bại hoặc vấn đề chưa giải quyết đều được nêu rõ; không tuyên bố hoàn tất khi chưa có bằng chứng.

## Xác minh

- Thực thi các bước kiểm tra frontend và xác minh đăng nhập end-to-end được mô tả ở trên.
- Rà soát hướng dẫn README theo góc nhìn thiết lập mới từ đầu.

## Ghi chú triển khai

### Bằng chứng xác minh

- Cài sạch trong bản sao tạm độc lập với `node_modules`: `npm ci` thành công (129 packages, 0 vulnerabilities). Không chạy lệnh cài sạch trên worktree chính vì Vite của Customer đang chạy và giữ file native trong `node_modules`.
- `npm run typecheck`: đạt cho các workspace frontend.
- `npm test`: đạt, 20/20 test.
- `npm run build`: đạt cho Admin, POS và Customer.
- `npm audit`: 0 vulnerabilities.
- Lint chưa chạy vì frontend workspace chưa cấu hình script lint.
- E2E qua trình duyệt với backend/API tạm biệt lập: Admin đăng nhập thành công; `BranchManager` chỉ thấy chi nhánh được gán; tài khoản sai ứng dụng bị từ chối; POS đăng nhập, tải lại phiên và đăng xuất thành công; Customer đăng nhập, trang hồ sơ hiển thị dữ liệu từ `/me`, tải lại phiên và đăng xuất thành công. Các lần đăng xuất trả HTTP 204.
- Admin ở viewport 390px vẫn hiển thị nút đăng xuất sau khi sửa CSS responsive; luồng logout trả HTTP 204.
- Khi API không khả dụng, Customer hiển thị lỗi kết nối, vẫn ở `/login` và không lưu thông tin phiên vào browser storage.
- Cả ba app hiển thị thông báo dữ liệu mẫu; dashboard Admin của `BranchManager` chỉ liệt kê chi nhánh thuộc `branchIds`.
- Node trong môi trường xác minh là `23.6.0`; npm 10.9.2. npm/Vitest có cảnh báo Node 23 chưa được hỗ trợ; README nêu các phiên bản Node được hỗ trợ.
- Backend kiểm thử, dữ liệu SQLite và mật khẩu seed dùng riêng cho lượt xác minh; không ghi credential vào tài liệu.

Không thể chạy `npm ci` trực tiếp trong worktree chính do file native bị tiến trình Vite của người dùng khóa; xác minh cài sạch và các lệnh frontend được chạy trong bản sao tạm. Không dừng tiến trình Customer do người dùng sở hữu trên cổng 5175.
