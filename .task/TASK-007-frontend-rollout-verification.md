# TASK-007: Xác minh và tài liệu hóa frontend

- Trạng thái: Chờ xử lý
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

Chạy các bước kiểm tra nhỏ nhất nhưng vẫn cung cấp bằng chứng cho từng tiêu chí chấp nhận; ghi rõ bước nào không thể chạy và lý do.
