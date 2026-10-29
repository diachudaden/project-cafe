# TASK-002: Xác định yêu cầu frontend và xác thực

- Trạng thái: Hoàn thành
- Mục tiêu: Xác nhận các quyết định sản phẩm cần thiết để xây dựng ba ứng dụng frontend dùng dữ liệu nghiệp vụ giả nhưng xác thực người dùng thật.
- Lý do: API hiện tại mới là bộ khung, chưa xác định nhà cung cấp xác thực hoặc endpoint production. Không được tự suy đoán hành vi tài khoản, phiên đăng nhập, phân quyền hay màn hình.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)
- [../docs/payments/reference/project.md](../docs/payments/reference/project.md)

## Tài liệu quy tắc

- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/testing.md](../.rule/testing.md)

## Phụ thuộc

- Không có

## Phạm vi

- Xác nhận các màn hình tối thiểu và luồng chính ban đầu của `admin`, `pos` và `customer`.
- Xác định màn hình nghiệp vụ nào sử dụng dữ liệu giả, đồng thời nêu rõ dữ liệu đó không được lưu trữ và không có giá trị làm nguồn dữ liệu chính thức.
- Xác định thông tin định danh dùng để đăng nhập (ví dụ: email, số điện thoại hoặc mã nhân viên) và ba ứng dụng có dùng chung hệ thống tài khoản hay không.
- Quyết định nhà cung cấp xác thực và cách quản lý phiên, bao gồm cookie hoặc token, gia hạn phiên nếu cần, đăng xuất và dữ liệu người dùng hiện tại.
- Xác định vai trò/quyền và điều hướng sau đăng nhập trong phạm vi cần thiết cho các màn hình ban đầu đã duyệt.
- Xác định các lỗi xác thực dự kiến và cách frontend xử lý từng lỗi.
- Cập nhật [../spec.md](../spec.md) với phạm vi và tiêu chí chấp nhận đã được duyệt.

## Ngoài phạm vi

- Không triển khai ứng dụng frontend, màn hình dữ liệu giả hoặc endpoint xác thực trong task này.
- Không tự giả định định dạng token, nhà cung cấp danh tính, mô hình vai trò hoặc cách lưu phiên.
- Không dùng xác thực giả thay cho yêu cầu đăng nhập thật.

## Tiêu chí chấp nhận

- Phạm vi màn hình/luồng ban đầu của từng ứng dụng được ghi lại.
- Ranh giới dữ liệu giả được nêu rõ, phân biệt dữ liệu nghiệp vụ mẫu với xác thực thật.
- Yêu cầu, phản hồi, phiên đăng nhập, đăng xuất và cách xử lý lỗi được mô tả đủ chi tiết để triển khai.
- Mọi quyết định sản phẩm hoặc bảo mật còn bỏ ngỏ được ghi rõ là điểm chặn, không được tự ý giả định.
- Đặc tả chuẩn đã được cập nhật theo các yêu cầu được duyệt.

## Xác minh

- Đối chiếu đặc tả đã cập nhật với các quyết định của chủ sản phẩm.
- Xác nhận từng yêu cầu cần cho TASK-003 và TASK-005 đã được ghi lại hoặc đánh dấu rõ là đang bị chặn.

## Ghi chú triển khai

Quyết định sản phẩm được lấy từ [docs/payments/reference/project.md](../docs/payments/reference/project.md) (thiết kế Chốn Riêng: ba app, bốn vai trò, Identity + JWT) rồi thu hẹp thành lát cắt đầu tiên trong [spec.md](../spec.md).

Không triển khai frontend hay endpoint. Đã cập nhật `spec.md`, `.rule/business-rules.md` và `.rule/security.md`.

**Khác với bản thiết kế đầy đủ (cố ý, để auth vẫn là auth thật):**

- Lát cắt này dùng email/SĐT + mật khẩu cho cả ba app. OTP SMS giả lập và PIN POS chưa được duyệt vì chưa có nhà cung cấp OTP / hợp đồng PIN.
- Không có đăng ký công khai; tài khoản kiểm thử do backend seed.
- Chỉ bốn endpoint auth: login, refresh, logout, me.

## Bằng chứng xác minh

Đối chiếu thủ công (không có lệnh build cho task tài liệu):

| Nhu cầu TASK-003 | Trạng thái |
| --- | --- |
| Nhà cung cấp, identifier, client app, vai trò | Có trong spec |
| Request/response login, refresh, logout, me | Có trong spec |
| Cookie refresh, JWT access, CORS credentials | Có trong spec |
| Lỗi và cách frontend xử lý | Có trong spec |
| OTP, PIN, register, khóa ký JWT production | Điểm chặn, không giả định |

| Nhu cầu TASK-005 | Trạng thái |
| --- | --- |
| Màn hình/luồng `admin`, `pos`, `customer` | Có trong spec |
| Ranh giới dữ liệu giả vs auth thật | Có trong spec |
| Màn hình ngoài lát cắt | Ngoài phạm vi / điểm chặn |

Kiểm tra working tree: chỉ thay đổi tài liệu, không thêm màn hình hay API.
