# TASK-006: Tích hợp đăng nhập thật vào các ứng dụng

- Trạng thái: Chờ xử lý
- Mục tiêu: Kết nối trải nghiệm đăng nhập dùng chung trong cả ba ứng dụng với API xác thực backend thật.
- Lý do: Tiêu chí hoàn thành của dự án yêu cầu đăng nhập gọi API thật, kể cả khi màn hình nghiệp vụ vẫn dùng dữ liệu giả.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

## Tài liệu quy tắc

- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/testing.md](../.rule/testing.md)
- [../.github/instructions/frontend.instructions.md](../.github/instructions/frontend.instructions.md)

## Phụ thuộc

- [TASK-003](./TASK-003-real-authentication-api.md)
- [TASK-004](./TASK-004-frontend-workspace.md)
- [TASK-005](./TASK-005-shared-ui-mock-screens.md)

## Phạm vi

- Thêm component trình bày đăng nhập dùng chung trong `packages/ui` và sử dụng trong cả ba ứng dụng.
- Thêm route cho từng ứng dụng bằng React Router; kiểm tra dữ liệu đăng nhập bằng React Hook Form + Zod theo hợp đồng đã duyệt.
- Triển khai service xác thực gọi endpoint backend thật.
- Xử lý trạng thái đang gửi, lỗi đã được tài liệu hóa, thiết lập phiên khi đăng nhập thành công, điều hướng sau đăng nhập đã duyệt và hành vi phiên/đăng xuất.
- Tuân thủ mô hình cookie/token đã duyệt; không lưu thông tin xác thực nhạy cảm trong browser storage trừ khi thiết kế được duyệt yêu cầu rõ ràng.
- Tách logic service xác thực khỏi các module dữ liệu nghiệp vụ giả.

## Ngoài phạm vi

- Không trả về thành công giả, dùng thông tin đăng nhập hard-code hoặc chuyển sang xác thực giả khi API lỗi.
- Không tự suy ra vai trò, điều hướng, gia hạn phiên hoặc cách lưu token ngoài hợp đồng đã duyệt.
- Không thêm client Orval hoặc hooks được sinh tự động, trừ khi có yêu cầu riêng đã được duyệt.

## Tiêu chí chấp nhận

- Màn hình đăng nhập được dùng chung bởi `admin`, `pos` và `customer`.
- Gửi form trong mỗi ứng dụng tạo request thật đến backend được cấu hình.
- Hành vi thành công và thất bại khớp hợp đồng backend và có test tập trung.
- Lỗi API không khả dụng và thông tin đăng nhập sai được hiển thị cho người dùng; không trường hợp nào bị coi là thành công.
- Hành vi phiên và đăng xuất tuân theo các quyết định được ghi trong TASK-002.

## Xác minh

- Chạy test frontend tập trung cho validation, trạng thái đang gửi/lỗi/thành công và hành vi auth service.
- Xác minh từng ứng dụng với backend đang chạy bằng tài khoản/môi trường kiểm thử đã duyệt.
- Chạy kiểm tra kiểu dữ liệu và production build cho cả ba ứng dụng.
- Ghi lại lệnh thực tế, yêu cầu môi trường và kết quả trong task này.

## Ghi chú triển khai

Sử dụng UI dùng chung cho phần trình bày, nhưng đặt các lệnh gọi API và điều hướng riêng của ứng dụng ở đúng layer auth/application.
