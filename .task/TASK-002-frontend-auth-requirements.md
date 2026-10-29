# TASK-002: Xác định yêu cầu frontend và xác thực

- Trạng thái: Chờ xử lý
- Mục tiêu: Xác nhận các quyết định sản phẩm cần thiết để xây dựng ba ứng dụng frontend dùng dữ liệu nghiệp vụ giả nhưng xác thực người dùng thật.
- Lý do: API hiện tại mới là bộ khung, chưa xác định nhà cung cấp xác thực hoặc endpoint production. Không được tự suy đoán hành vi tài khoản, phiên đăng nhập, phân quyền hay màn hình.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

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

Repository hiện chưa xác định hành vi nghiệp vụ hoặc nhà cung cấp xác thực. Không bắt đầu triển khai xác thực hay màn hình phụ thuộc trước khi các quyết định cần thiết được duyệt.
