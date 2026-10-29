# TASK-001: Thiết lập quy trình làm việc GitHub Copilot cho Cafe CR

- Trạng thái: Hoàn thành
- Mục tiêu: Xây dựng quy trình làm việc GitHub Copilot có thể duy trì và phù hợp với quy ước của repository.
- Lý do: Repository cần có hướng dẫn Copilot, backlog công việc và quy tắc dự án rõ ràng để các công việc triển khai sau này nhất quán, dễ xem xét.

## Tài liệu đặc tả

- [../spec.md](../spec.md)
- [../README.md](../README.md)

## Tài liệu quy tắc

- [../.rule/architecture.md](../.rule/architecture.md)
- [../.rule/coding-style.md](../.rule/coding-style.md)
- [../.rule/business-rules.md](../.rule/business-rules.md)
- [../.rule/security.md](../.rule/security.md)
- [../.rule/testing.md](../.rule/testing.md)

## Phụ thuộc

- Không có

## Phạm vi

- Thêm hướng dẫn Copilot áp dụng trên toàn repository.
- Thêm các tệp hướng dẫn theo đường dẫn cho backend, frontend và tests.
- Thêm các tệp prompt có thể tái sử dụng cho việc lập kế hoạch, triển khai, xem xét và xác minh.
- Thêm backlog công việc tối thiểu và bộ quy tắc phù hợp với dự án hiện tại.

## Ngoài phạm vi

- Không thay thế tài liệu dự án hiện có.
- Không thêm tự động hóa GitHub không cần thiết hoặc task giả định, không gắn với repository.
- Không sao chép toàn bộ đặc tả nghiệp vụ vào nhiều tệp hướng dẫn.

## Tiêu chí chấp nhận

- Repository có tệp hướng dẫn Copilot ở cấp repository rõ ràng.
- Các hướng dẫn và prompt liên quan được tổ chức dưới [.github/](../.github).
- Quy trình task được lưu trong [.task/](../.task) và tham chiếu các yêu cầu thực tế của dự án.
- Bộ quy tắc trong [.rule/](../.rule) tồn tại và mô tả đúng kiến trúc cùng các ràng buộc hiện hành.
- Quy trình tham chiếu cấu trúc repository và các tệp đặc tả thực tế, không dùng nội dung giữ chỗ.

## Xác minh

- Kiểm tra working tree và cấu trúc tệp.
- Kiểm tra repository không bị trùng lặp tài liệu và không thiếu liên kết tham chiếu.
- Xác nhận các tệp nằm đúng vị trí và tham chiếu nội dung dự án hiện có.

## Ghi chú triển khai

Thiết lập này sử dụng tài liệu và cấu trúc thực tế của repository thay vì tạo ra quy trình mới không được hỗ trợ. Repository hiện không có GitHub Actions hoặc thư mục agent đang hoạt động, vì vậy giải pháp phù hợp là sử dụng mẫu instruction/prompt được hỗ trợ của Copilot cùng các tham chiếu task và quy tắc rõ ràng.
