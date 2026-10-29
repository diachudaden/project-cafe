# TASK-006: Tích hợp đăng nhập thật vào các ứng dụng

- Trạng thái: Hoàn thành
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

Đã triển khai:

- Thêm `@chonrieng/ui` form đăng nhập React Hook Form + Zod, có validation, trạng thái đang gửi, lỗi chung và lỗi field từ ProblemDetails.
- Thêm `@chonrieng/auth` dùng chung cho request login/refresh/logout/me, parse hợp đồng phản hồi, ánh xạ lỗi, quản lý access token trong React memory state và bảo vệ route.
- Cả ba app cấu hình `client` riêng, route `/login`, trang chính sau đăng nhập và nút đăng xuất. Khi khởi động/tải lại, app refresh qua cookie rồi xác nhận hồ sơ qua `/me`.
- Customer Profile dùng dữ liệu `/me` thật; dashboard Admin giới hạn `BranchManager` theo `branchIds`. Dữ liệu nghiệp vụ mẫu vẫn nằm trong các service feature hiện có.
- Không lưu token/mật khẩu vào browser storage; không thêm thông tin đăng nhập vào source, không thêm Orval, OTP, PIN hay đăng ký.

**Bằng chứng xác minh**

- `Set-Location .\frontend; npm install` — thành công; Zod được nâng lên bản đã vá, `npm audit` không phát hiện lỗ hổng.
- `Set-Location .\frontend; npm run typecheck` — thành công cho `@chonrieng/auth`, Admin, POS và Customer.
- `Set-Location .\frontend; npm test` — 20 test đạt: shared UI (5), auth (10), Admin (3), POS (1), Customer (1).
- `Set-Location .\frontend; npm run build` — cả ba app production build thành công.
- `Set-Location .\frontend; npm audit` — 0 vulnerabilities.
- Kiểm tra tích hợp HTTP với backend thực chạy bằng SQLite tạm và khóa/mật khẩu seed ngẫu nhiên chỉ trong môi trường tiến trình: cả `admin`, `pos`, `customer` đều login 200, `/me` 200, refresh 200 và logout 204; CORS trả đúng origin và credentials. Dữ liệu tạm được dọn sau kiểm tra.
- `git diff --check` — thành công sau khi cập nhật mã và tài liệu.
