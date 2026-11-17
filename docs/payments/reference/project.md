# Thiết kế hệ thống quản lý chuỗi cà phê tự phục vụ

Oct 18, 2026 · @Gnuh

## Tổng quan

Hệ thống quản lý chuỗi cà phê tự phục vụ theo mô hình Chốn Riêng: khách mua **lượt ngồi 4 tiếng** kèm một ly tự pha, gia hạn mỗi 4 tiếng, nhiều chi nhánh mở 24/7. Đây là đồ án học tập, ưu tiên rõ ràng, dễ trình bày và chạy được trên máy cá nhân bằng một lệnh `docker compose up`.

**Ba ứng dụng trên một hệ thống:**

- **Web Quản lý** – chia lý chi nhánh và chỗ ngồi.ca, duyệt chấm công, báo cáo tuần/tháng/quý/năm, thu chi, quản lý chi nhánh và chỗ ngồi.
- **POS Nhân viên** (tablet tại quầy) – check-in khách, tính tiền, gia hạn, điểm danh, kết ca, xem lịch và lương.
- **Web Khách** (mobile-first, PWA) – tìm chi nhánh còn chỗ, đặt chỗ, nhập bill, tích điểm, đổi quà.

**Ngoài phạm vi (đã thay thế bởi TASK-013):** SePay Test mode + máy in giả trên web; phần cổng thanh toán thật và máy in hóa đơn vật lý đã được chuyển sang task riêng.

## Vai trò & phân quyền

Bốn vai trò, phân quyền theo chi nhánh: quản lý và nhân viên chỉ thấy dữ liệu chi nhánh mình được gán; Admin thấy toàn chuỗi.

| Vai trò | Ứng dụng | Phạm vi dữ liệu | Quyền chính |
| --- | --- | --- | --- |
| Admin (chủ chuỗi) | Web Quản lý | Toàn chuỗi | Tạo chi nhánh, gán quản lý, cấu hình giá, xem báo cáo tổng |
| Quản lý chi nhánh | Web Quản lý | Chi nhánh được gán | Chia ca, duyệt chấm công, nhập thu chi, xem báo cáo chi nhánh |
| Nhân viên | POS Nhân viên | Ca đang làm | Check-in/out khách, thu tiền, điểm danh, kết ca, xem lương cá nhân |
| Khách | Web Khách | Dữ liệu của mình | Đặt chỗ, nhập bill, tích/đổi điểm, đánh giá |

Triển khai bằng ASP.NET Core Identity + JWT, claim `role` và `branchIds`; mỗi API kiểm tra cả vai trò lẫn chi nhánh (policy-based authorization).

## Tính năng chi tiết

Cột MVP đánh dấu những gì cần có để demo bảo vệ đồ án; phần còn lại là mở rộng nếu còn thời gian. Dòng *(gợi ý)* là tính năng mình đề xuất thêm ngoài yêu cầu ban đầu.

### Quản lý

| Nhóm | Tính năng | Mô tả | MVP |
| --- | --- | --- | --- |
| Chuỗi cửa hàng | Chuyển chi nhánh nhanh | Ô chọn chi nhánh trên thanh trên cùng (hoặc "Tất cả"); mọi màn hình lọc theo lựa chọn này | Có |
| Chuỗi cửa hàng | Hồ sơ chi nhánh | Địa chỉ, toạ độ bản đồ, giờ mở cửa / 24h, số điện thoại, trạng thái hoạt động | Có |
| Chuỗi cửa hàng | Sơ đồ & chỗ ngồi | Khu vực, loại chỗ (bàn đơn, bàn nhóm, góc ngủ), bật/tắt chỗ đang sửa | Có |
| Chuỗi cửa hàng | Bảng giá theo chi nhánh | Giá lượt 4 tiếng, giá gia hạn, giá theo khung giờ (ngày/đêm/cuối tuần) | Có |
| Chuỗi cửa hàng | So sánh chi nhánh | Doanh thu, lượt khách, tỷ lệ lấp đầy, chi phí lương trên doanh thu, xếp hạng | Có |
| Chia ca | Lịch ca kiểu Teams | Lịch tuần/tháng, mỗi ca là một thẻ như cuộc họp, màu theo chi nhánh; kéo thả để tạo hoặc dời ca | Có |
| Chia ca | Gửi ca như lời mời họp | Ca ở trạng thái Nháp → Công bố; nhân viên nhận thông báo và bấm Chấp nhận / Từ chối | Có |
| Chia ca | Kiểm tra xung đột tự động | Chặn trùng giờ (kể cả khác chi nhánh), thiếu giờ nghỉ, vượt giờ tuần; hiện lý do ngay khi kéo thả | Có |
| Chia ca | Trợ lý xếp ca | Lưới rảnh/bận của nhân viên (giống Scheduling Assistant của Teams), gợi ý người phù hợp | Có |
| Chia ca | Ca trống | Đăng ca chưa có người để nhân viên đủ điều kiện tự nhận, ai nhận trước được trước | Có |
| Chia ca | Sao chép tuần | Copy lịch tuần trước sang tuần này, chỉ báo những ca bị xung đột | Có |
| Chia ca | Duyệt đổi ca / nghỉ | Hàng chờ đơn đổi ca, xin nghỉ; duyệt xong lịch tự cập nhật và báo cho các bên | Có |
| Nhân sự | Hồ sơ nhân viên | Thông tin, lương giờ, phụ cấp ca đêm, chi nhánh được làm (một người nhiều chi nhánh), khoá tài khoản | Có |
| Nhân sự | Duyệt chấm công | Giờ vào/ra thực tế so với ca, đi trễ, về sớm, tăng ca | Có |
| Nhân sự | Kiểm tra kết ca | Chênh lệch tiền mặt từng ca, biên bản bàn giao, ghi chú lý do | Có |
| Nhân sự | Chốt lương tháng | Xem bảng lương tạm tính, điều chỉnh thưởng/phạt, chốt và gửi cho nhân viên | Có |
| Thu chi | Sổ thu chi | Thu tự ghi từ hoá đơn (tiền mặt, VietQR); chi nhập tay theo danh mục, đính kèm ảnh hoá đơn; sổ chỉ thêm, sai thì ghi bút toán đảo | Có |
| Thu chi | Đối soát | So tiền mặt kết ca và giao dịch VietQR với sổ thu, liệt kê khoản lệch | Không |
| Báo cáo | Báo cáo theo kỳ | Tuần/tháng/quý/năm: doanh thu, chi phí, lãi lỗ, lượt khách; so với kỳ trước | Có |
| Báo cáo | Phân tích vận hành | Bản đồ nhiệt giờ cao điểm, tỷ lệ lấp đầy theo loại chỗ, giờ công theo chi nhánh | Không |
| Báo cáo | Xuất file | Excel/PDF cho mọi báo cáo | Có |
| Vận hành | Dashboard realtime | Chỗ trống từng chi nhánh, doanh thu hôm nay, ai đang trong ca (trạng thái hiện diện) | Có |
| Vận hành | Kênh chi nhánh *(gợi ý)* | Thông báo cho toàn chi nhánh như một channel của Teams; nhân viên xác nhận đã đọc | Không |
| Vận hành | Tồn kho nguyên liệu *(gợi ý)* | Cà phê gói, trà, sữa, ly; cảnh báo sắp hết | Không |
| Vận hành | Khuyến mãi & voucher *(gợi ý)* | Mã giảm giá, giờ vàng, ưu đãi sinh viên | Không |

### Nhân viên

| Nhóm | Tính năng | Mô tả | MVP |
| --- | --- | --- | --- |
| Tài khoản | Đăng nhập / đăng xuất | Web: SĐT hoặc email + mật khẩu. Tablet POS dùng chung ở quầy: chọn tên + nhập PIN 6 số, khoá màn hình sau 5 phút không thao tác | Có |
| Tài khoản | Một phiên POS mỗi người | Đăng nhập máy khác thì phiên cũ bị đăng xuất; đăng xuất tự động khi kết ca | Có |
| Tài khoản | Hồ sơ cá nhân | Đổi mật khẩu, đổi PIN, thông tin ngân hàng nhận lương | Có |
| Lịch làm | Xem lịch kiểu Teams | Tuần/tháng; mỗi ca hiện giờ, chi nhánh, địa chỉ, chỉ đường và đồng nghiệp cùng ca | Có |
| Lịch làm | Nhận ca | Chấp nhận / Từ chối ca được giao (kèm lý do); tự nhận ca trống. Hệ thống chặn nếu trùng giờ hoặc không đủ giờ nghỉ | Có |
| Lịch làm | Đăng ký giờ rảnh | Đánh dấu khung giờ có thể làm, theo tuần hoặc lặp lại | Có |
| Lịch làm | Đổi ca / xin nghỉ | Chọn ca, chọn người nhận thay (chỉ hiện người không bị trùng), chờ quản lý duyệt | Có |
| Lịch làm | Nhắc ca | Thông báo 1 giờ trước ca; báo khi lịch bị đổi | Có |
| Trong ca | Điểm danh (Tham gia ca) | Nút "Tham gia" trên thẻ ca đang tới, chỉ mở từ 15 phút trước giờ bắt đầu; phải đúng chi nhánh (quét QR động tại quầy) | Có |
| Trong ca | Tính tiền / check-in khách | Chọn chỗ trên sơ đồ, quét QR thành viên, thu tiền mặt hoặc VietQR, in/gửi bill có mã | Có |
| Trong ca | Gia hạn & check-out | POS nhắc khi khách còn 15 phút; một chạm để gia hạn hoặc trả chỗ | Có |
| Trong ca | Báo cáo sự cố *(gợi ý)* | Hết nguyên liệu, máy hư; gửi kèm ảnh cho quản lý | Không |
| Kết ca | Kết ca (Rời ca) | Wizard 3 bước: đếm tiền theo mệnh giá → đối chiếu với tiền dự kiến → bàn giao cho ca sau; xong thì tự đăng xuất | Có |
| Lương | Xem lương | Giờ công từng ca, phụ cấp ca đêm, thưởng/phạt, tạm tính và lương đã chốt | Có |

### Khách

| Tính năng | Mô tả | MVP |
| --- | --- | --- |
| Tìm chi nhánh còn chỗ | Bản đồ + danh sách, sắp theo khoảng cách, hiện số chỗ trống theo loại, giờ mở cửa | Có |
| Nhập bill tích điểm | Nhập mã bill hoặc quét QR trên bill để cộng điểm (mỗi bill chỉ dùng một lần) | Có |
| Tích điểm & hạng thành viên | 1.000đ = 1 điểm; hạng Đồng/Bạc/Vàng theo tổng chi tiêu | Có |
| Đổi điểm | Đổi lấy giờ ngồi miễn phí, voucher | Có |
| Đăng ký/đăng nhập | Số điện thoại + OTP giả lập, hoặc email | Có |
| Đặt giữ chỗ *(gợi ý)* | Giữ chỗ trong 30 phút, quá hạn tự hủy | Không |
| Đồng hồ đếm ngược *(gợi ý)* | Thời gian còn lại của lượt ngồi, nhắc gia hạn | Không |
| Lịch sử ghé quán *(gợi ý)* | Danh sách bill, tổng giờ đã ngồi trong tháng | Không |
| Đánh giá chi nhánh *(gợi ý)* | Chấm điểm độ yên tĩnh, wifi, vệ sinh | Không |
| Hướng dẫn tự pha *(gợi ý)* | Công thức có ảnh từng bước, đúng đặc trưng mô hình tự phục vụ | Không |

## Luồng nghiệp vụ chính

Luồng quan trọng nhất là vòng đời một lượt ngồi: mọi doanh thu, điểm thưởng và số chỗ trống đều sinh ra từ đây.

&#91;embedded content: luồng một lượt ngồi · 6 bước, 1 quyết định\]

Mỗi lần gia hạn tạo một dòng SessionExtensions và cộng vào cùng một bill, nên khách chỉ cần nhập một mã để tích điểm cho cả lượt.

**Luồng ca làm của nhân viên**

1. Quản lý xếp lịch tuần (hoặc sao chép tuần trước); nhân viên nhận thông báo.
2. Đầu ca: nhân viên điểm danh bằng PIN hoặc QR động tại quầy; trễ quá 10 phút bị đánh dấu.
3. Trong ca: mọi bill tự gắn với ca đang mở.
4. Cuối ca: wizard kết ca — đếm tiền theo mệnh giá, hệ thống so với tiền mặt dự kiến, ghi chênh lệch, bàn giao ca sau.
5. Quản lý duyệt chấm công và biên bản kết ca.
6. Cuối tháng: hệ thống tính lương = giờ công × đơn giá + phụ cấp ca đêm + thưởng − phạt; nhân viên xem trước khi chốt.

**Trạng thái một ca**

Nháp → Công bố → Đã gửi cho nhân viên → Đã nhận / Bị từ chối (quay về quản lý hoặc thành ca trống) → Đang diễn ra (sau khi điểm danh) → Đã kết ca → Đã duyệt.

**Quy tắc chống xung đột ca**

Mọi quy tắc chạy ở server mỗi khi gán, nhận, đổi hoặc sao chép ca. Giao diện gọi trước API kiểm tra để hiện cảnh báo ngay khi kéo thả, giống Teams báo trùng lịch họp.

| Quy tắc | Mặc định | Khi vi phạm |
| --- | --- | --- |
| Không trùng giờ, kể cả khác chi nhánh | Bắt buộc | Chặn |
| Nghỉ tối thiểu giữa 2 ca | 8 tiếng | Chặn (quản lý được bỏ qua kèm lý do) |
| Thời gian di chuyển khi 2 ca liền nhau khác chi nhánh | 60 phút | Chặn |
| Tổng giờ làm trong tuần | 48 giờ (giới hạn giờ làm việc bình thường theo Bộ luật Lao động) | Cảnh báo |
| Chỉ xếp vào chi nhánh nhân viên được gán | Bắt buộc | Chặn |
| Ca trùng ngày đã được duyệt nghỉ | Bắt buộc | Chặn |
| Ca chưa đủ số người tối thiểu | Theo ca | Cảnh báo trên lịch |

**Xử lý tranh chấp đồng thời:** hai quản lý cùng xếp một người, hoặc hai nhân viên cùng bấm nhận một ca trống. Server kiểm tra lại quy tắc trong cùng transaction với lệnh ghi, bảng `Shifts` có cột `RowVersion` (optimistic concurrency). Người ghi sau nhận lỗi "Ca vừa được người khác nhận" và lịch tự làm mới.

**Thiết kế code:** mỗi quy tắc là một class `IShiftRule` (pattern Specification / Chain of Responsibility). `ShiftConflictChecker` chạy lần lượt các rule và gom kết quả thành danh sách vi phạm, nên thêm quy tắc mới không phải sửa code cũ.

**Quy tắc nhập bill tích điểm**

- Mã bill 8 ký tự in trên hóa đơn kèm QR; mỗi bill chỉ tích điểm một lần.
- Hạn nhập bill: 7 ngày kể từ lúc thanh toán.
- Nếu nhân viên đã quét QR thành viên lúc check-in thì điểm được cộng tự động, không cần nhập.

## Thiết kế UX

Mỗi vai trò làm việc chính của mình trong tối đa 3 chạm, vì nhân viên đứng quầy lúc đông khách và khách thường mở app trên điện thoại khi đang di chuyển.

**Nguyên tắc chung**

- **Một màn hình, một việc chính:** nút hành động chính to, đặt ở vị trí cố định (góc dưới phải trên tablet, đáy màn hình trên mobile).
- **Màu mang nghĩa nhất quán:** xanh = chỗ trống, cam = sắp hết giờ, đỏ = quá giờ, xám = đang bảo trì.
- **Tiếng Việt đời thường:** "Gia hạn thêm 4 tiếng" thay vì "Extend session"; tiền luôn hiển thị dạng 45.000đ.
- **Không mất dữ liệu:** mọi thao tác tiền có bước xác nhận; kết ca có thể lưu nháp.
- **Trạng thái rỗng có hướng dẫn:** "Chưa có ca nào tuần này — bấm Tạo ca" thay vì màn hình trắng.
- **Responsive & dark mode:** POS hay dùng ban đêm ở chi nhánh 24/7.

**Màn hình chính từng vai trò**

| Vai trò | Màn hình đầu tiên | Thao tác nổi bật |
| --- | --- | --- |
| Nhân viên (tablet) | Sơ đồ chỗ ngồi realtime, tô màu theo trạng thái | Chạm chỗ trống → Check-in → Thu tiền (3 chạm) |
| Nhân viên | Thanh trên cùng: ca hiện tại, tiền mặt đang giữ | Nút "Kết ca" mở wizard 3 bước: đếm tiền → đối chiếu → bàn giao |
| Quản lý | Dashboard: doanh thu hôm nay, lấp đầy %, cảnh báo | Bộ lọc kỳ (Tuần/Tháng/Quý/Năm) dùng chung cho mọi biểu đồ |
| Quản lý | Lịch chia ca dạng lưới kéo thả | Sao chép lịch tuần trước sang tuần này một chạm |
| Khách (mobile) | Danh sách chi nhánh gần nhất + số chỗ trống | Thanh dưới cùng 4 tab: Tìm quán · Quét bill · Điểm · Tôi |

**Phong cách Microsoft Teams cho Web Quản lý và POS**

Nghiệp vụ ca làm khớp tự nhiên với cách Teams quản lý cuộc họp: ca có giờ, địa điểm, người tham gia, lời mời cần phản hồi và nút Tham gia. Người dùng đã quen Teams sẽ hiểu ngay mà không cần hướng dẫn.

&#91;embedded content: phác thảo màn hình Lịch ca theo phong cách Teams\]

Thẻ nét liền là ca đã nhận, nét đứt là ca còn chờ phản hồi hoặc ca trống; bấm một thẻ để mở khung chi tiết bên phải.

| Teams | Trong hệ thống |
| --- | --- |
| Thanh ứng dụng dọc bên trái | Hoạt động · Lịch ca · Chi nhánh · Thu chi · Báo cáo (nhân viên: Hoạt động · Lịch của tôi · Quầy · Lương) |
| Lịch cuộc họp (tuần/ngày/tháng) | Lịch ca, màu theo chi nhánh |
| Lời mời họp: Chấp nhận / Từ chối | Nhận ca / Từ chối ca kèm lý do |
| Scheduling Assistant | Trợ lý xếp ca: lưới rảnh/bận, cảnh báo trùng ca |
| Nút Tham gia trên cuộc họp sắp bắt đầu | Nút Tham gia ca = điểm danh |
| Rời cuộc họp | Rời ca = mở wizard kết ca |
| Feed Hoạt động | Được giao ca, đơn đổi ca, cảnh báo thiếu người, chênh lệch kết ca |
| Trạng thái hiện diện (chấm màu trên avatar) | Đang trong ca / Nghỉ / Ngoại tuyến |
| Channel của team | Kênh thông báo từng chi nhánh |

Web Khách giữ giao diện mobile riêng (4 tab dưới cùng), vì khách không dùng lịch hay ca.

**Thư viện gợi ý:** Fluent UI React v9 (design system mà Teams đang dùng) cho Web Quản lý và POS để có đúng cảm giác Teams; FullCalendar hoặc react-big-calendar cho lịch tuần kéo thả; Tailwind + shadcn/ui cho Web Khách; Recharts cho biểu đồ; Leaflet + OpenStreetMap cho bản đồ (miễn phí, không cần API key).

## Kiến trúc kỹ thuật

Một backend .NET 10 (LTS) phục vụ cả ba app React; local chạy toàn bộ bằng Docker Compose, lên AWS thì frontend nằm trên S3 + CloudFront, API chạy ECS Fargate, CSDL trên RDS.

&#91;embedded content: kiến trúc triển khai trên AWS\]

Người dùng tải giao diện tĩnh từ CloudFront, còn mọi dữ liệu và cập nhật realtime đi qua Load Balancer tới API; chỉ API được chạm vào CSDL.

**Stack**

| Tầng | Công nghệ |
| --- | --- |
| Backend | .NET 10, ASP.NET Core Web API, EF Core 10, FluentValidation, SignalR, Hangfire (job đêm), Serilog |
| Frontend | React 19 + TypeScript + Vite, React Router, TanStack Query, React Hook Form + Zod, Recharts |
| CSDL | SQL Server 2025 (image `mcr.microsoft.com/mssql/server:2025-latest`) |
| Container | Docker, Docker Compose |
| Cloud | AWS: ECR, ECS Fargate, ALB, RDS, S3, CloudFront, Secrets Manager, CloudWatch |
| Kiểm thử | xUnit, Testcontainers, Playwright (E2E luồng check-in và kết ca) |
| CI/CD | GitHub Actions: build → test → push ECR → deploy ECS |

**Docker Compose (local)** — 6 service, mở `http://localhost:8080`:

1. `sqlserver` – SQL Server 2025, volume lưu dữ liệu, healthcheck bằng `sqlcmd`.
2. `api` – .NET API, `depends_on` sqlserver healthy, tự chạy migration + seed dữ liệu mẫu (3 chi nhánh, 40 chỗ, 10 nhân viên).
3. `admin`, `pos`, `customer` – mỗi app build bằng Vite và phục vụ bằng nginx.
4. `gateway` – nginx định tuyến `/admin`, `/pos`, `/`, `/api`, `/hubs` để cả hệ thống chỉ dùng một cổng.

**Lưu ý AWS cho đồ án:** kiểm tra RDS đã hỗ trợ SQL Server 2025 ở thời điểm triển khai; nếu chưa, chạy container SQL Server 2025 trên một EC2 nhỏ. Dùng bản Express để giảm chi phí và tắt tài nguyên khi không demo.

## Thiết kế CSDL

22 bảng chia 5 nhóm (17 bảng gốc + 5 bảng cho nghiệp vụ ca bên dưới), dùng EF Core Code-First migrations trên SQL Server 2025. Khóa chính `uniqueidentifier` (Guid v7 sinh bằng Guid.CreateVersion7() để giữ thứ tự), tiền lưu `decimal(12,0)` theo VND, thời gian lưu `datetimeoffset`.

| Nhóm | Bảng | Cột chính | Quan hệ |
| --- | --- | --- | --- |
| Chi nhánh | Branches | Name, Address, Lat, Lng, Is24h, OpenTime, CloseTime | 1–n Zones, Shifts, Sessions |
| Chi nhánh | Zones | BranchId, Name (Khu yên tĩnh, Khu nhóm…) | 1–n Seats |
| Chi nhánh | Seats | ZoneId, Code, Type (Single/Group/Nap), Status | 1–n Sessions |
| Chi nhánh | PricePlans | BranchId?, Name, DurationMinutes (240), Price, ExtendPrice, ValidFrom/To | dùng bởi Sessions |
| Người dùng | Users | Email, Phone, PasswordHash, FullName, Role | 1–1 Employees hoặc Customers |
| Người dùng | Employees | UserId, HourlyRate, NightAllowance, PinHash | 1–n ShiftAssignments, Attendances |
| Người dùng | Customers | UserId, Points, Tier, TotalSpent | 1–n Bills, PointTransactions |
| Ca làm | Shifts | BranchId, Date, Type (Sáng/Chiều/Tối/Đêm), StartAt, EndAt, RequiredStaff, IsOpen, Status (Draft/Published), RowVersion | 1–n ShiftAssignments |
| Ca làm | ShiftAssignments | ShiftId, EmployeeId, Status (Offered/Accepted/Declined), RespondedAt | n–n Shift–Employee |
| Ca làm | Attendances | EmployeeId, ShiftId, CheckInAt, CheckOutAt, Method, Approved |  |
| Ca làm | ShiftClosings | ShiftId, ClosedBy, ExpectedCash, CountedCash, Difference, Note, Denominations (JSON) | 1–1 Shift |
| Bán hàng | Sessions | SeatId, CustomerId?, PricePlanId, StartAt, EndAt, Status (Active/Expired/Closed) | 1–n SessionExtensions; 1–1 Bill |
| Bán hàng | SessionExtensions | SessionId, AddedMinutes, Price, CreatedAt |  |
| Bán hàng | Bills | Code (in trên bill), BranchId, ShiftId, SessionId, Total, PaymentMethod, ClaimedByCustomerId?, ClaimedAt |  |
| Bán hàng | PointTransactions | CustomerId, BillId?, Points (+/-), Reason |  |
| Tài chính | Expenses | BranchId, Category, Amount, Date, Note, CreatedBy |  |
| Tài chính | Payrolls | EmployeeId, Month, TotalHours, BaseAmount, Allowance, Bonus, Deduction, NetAmount, Status |  |

**Bảng bổ sung cho nghiệp vụ ca và đăng nhập**

| Bảng | Cột chính | Dùng cho |
| --- | --- | --- |
| EmployeeBranches | EmployeeId, BranchId, IsHome | Một nhân viên làm nhiều chi nhánh; quy tắc "chỉ xếp vào chi nhánh được gán" |
| Availabilities | EmployeeId, DayOfWeek hoặc Date, From, To, IsRecurring | Giờ rảnh, dùng cho trợ lý xếp ca |
| ShiftRequests | Type (Swap/Leave/Drop), ShiftId, RequesterId, TargetEmployeeId?, Reason, Status, ReviewedBy, ReviewedAt | Đổi ca, xin nghỉ, trả ca |
| Notifications | UserId, Type, Title, Body, Link, ReadAt | Feed Hoạt động kiểu Teams |
| UserSessions | UserId, DeviceId, IsPos, BranchId?, RefreshTokenHash, CreatedAt, RevokedAt | Đăng nhập/đăng xuất, giới hạn một phiên POS mỗi người |

Index quan trọng: `ShiftAssignments(EmployeeId)` kèm `Shifts(StartAt, EndAt)` để tìm ca chồng giờ nhanh; unique index có filter trên `UserSessions(UserId) WHERE RevokedAt IS NULL AND IsPos = 1` để chặn hai phiên POS.

**Điểm đáng chú ý cho đồ án**

- `Bills.Code` có unique index; nhập bill tích điểm kiểm tra `ClaimedByCustomerId IS NULL` trong một transaction để chống tích hai lần.
- Số chỗ trống = Seats active trừ Sessions đang `Active`; tạo **view** `vw_BranchAvailability` và cache 10 giây.
- Báo cáo kỳ dùng bảng tổng hợp `DailyRevenue` (job chạy mỗi đêm) để báo cáo năm không phải quét toàn bộ Bills.
- SQL Server 2025 có kiểu `json` native và hàm `REGEXP_*`: dùng `json` cho cột `Denominations` là một điểm cộng khi trình bày.

## Thiết kế API

REST, tiền tố `/api/v1`, tài liệu tự sinh bằng OpenAPI + Scalar. Cập nhật realtime (chỗ trống, khách sắp hết giờ) đẩy qua SignalR hub `/hubs/branch`.

| Nhóm | Endpoint | Vai trò |
| --- | --- | --- |
| Xác thực | `POST /auth/register` · `POST /auth/login` · `POST /auth/refresh` | Tất cả |
| Chi nhánh | `GET /branches?lat=&lng=` · `GET /branches/{id}/availability` | Công khai |
| Chi nhánh | `POST/PUT /branches` · `CRUD /branches/{id}/zones` · `CRUD /seats` | Admin |
| Bảng giá | `GET/POST /price-plans` | Quản lý |
| Lượt ngồi | `POST /sessions` (check-in) · `POST /sessions/{id}/extend` · `POST /sessions/{id}/close` · `GET /branches/{id}/sessions/active` | Nhân viên |
| Hóa đơn | `GET /bills/{code}` · `POST /bills/claim` (khách nhập bill) | Nhân viên, Khách |
| Điểm | `GET /me/points` · `POST /me/redeem` · `GET /rewards` | Khách |
| Ca làm | `GET/POST /shifts?branchId=&week=` · `POST /shifts/{id}/assign` · `POST /shifts/copy-week` | Quản lý |
| Ca làm | `GET /me/shifts` · `POST /attendances/check-in` · `POST /attendances/check-out` | Nhân viên |
| Kết ca | `GET /shifts/{id}/closing-preview` · `POST /shifts/{id}/close` | Nhân viên |
| Lương | `GET /me/payroll?month=` · `POST /payrolls/generate?month=` | Nhân viên, Quản lý |
| Thu chi | `CRUD /expenses` | Quản lý |
| Báo cáo | `GET /reports/revenue?branchId=&period=week\|month\|quarter\|year&date=` · `GET /reports/profit-loss` · `GET /reports/occupancy` · `GET /reports/export?format=xlsx` | Quản lý, Admin |

**API bổ sung cho ca làm, đăng nhập và thông báo**

| Nhóm | Endpoint | Vai trò |
| --- | --- | --- |
| Đăng nhập | `POST /auth/pin-login` (tablet POS) · `POST /auth/logout` · `GET /auth/sessions` | Nhân viên |
| Ca làm | `POST /shifts/validate` (kiểm tra xung đột, không lưu) · `POST /shifts/publish?branchId=&week=` | Quản lý |
| Ca làm | `GET /shifts/open` · `POST /shifts/{id}/claim` (nhận ca trống) | Nhân viên |
| Ca làm | `POST /assignments/{id}/accept` · `POST /assignments/{id}/decline` | Nhân viên |
| Ca làm | `GET /scheduling/assistant?branchId=&from=&to=` (lưới rảnh/bận) | Quản lý |
| Giờ rảnh | `GET/PUT /me/availability` | Nhân viên |
| Đơn | `POST /shift-requests` · `POST /shift-requests/{id}/approve` · `POST /shift-requests/{id}/reject` | Nhân viên, Quản lý |
| Chi nhánh | `PUT /employees/{id}/branches` | Quản lý, Admin |
| Thông báo | `GET /me/notifications` · `POST /me/notifications/read` | Tất cả |

Lỗi xung đột trả `409 Conflict` kèm danh sách vi phạm (`rule`, `message`, `canOverride`) để giao diện hiện đúng lý do.

Quy ước: lỗi trả về dạng `ProblemDetails` (RFC 9457), danh sách có phân trang `?page=&pageSize=`, validation bằng FluentValidation, mọi thao tác tiền ghi audit log.

## Cấu trúc thư mục project

Backend theo Clean Architecture 4 tầng, dùng Repository + Unit of Work + Service Layer thay cho CQRS/MediatR: ít lớp trừu tượng hơn, đọc code là hiểu luồng (MediatR cũng đã chuyển sang license thương mại từ 2025). Frontend là một workspace pnpm chứa 3 app.

```
chon-rieng/
├── docker-compose.yml
├── .env.example
├── backend/
├── frontend/
├── infra/terraform/
└── .github/workflows/
```

**Backend**

```
backend/
├── ChonRieng.sln
├── src/
│   ├── ChonRieng.Domain/                 # LÕI – không tham chiếu project nào
│   │   ├── Common/        BaseEntity.cs, IAuditable.cs
│   │   ├── Entities/      Branch, Seat, Session, Bill, Shift, Employee, Customer...
│   │   ├── Enums/         SeatStatus, ShiftType, PaymentMethod
│   │   └── Exceptions/    DomainException.cs
│   │
│   ├── ChonRieng.Application/            # NGHIỆP VỤ – chỉ tham chiếu Domain
│   │   ├── Common/
│   │   │   ├── Interfaces/  IRepository<T>, IUnitOfWork, ICurrentUser, IDateTime
│   │   │   └── Result.cs
│   │   ├── Pricing/        IPricingStrategy, DayPricing, NightPricing, PricingStrategyFactory
│   │   ├── Features/
│   │   │   ├── Sessions/    ISessionService, SessionService, Dtos/, Validators/
│   │   │   ├── Shifts/      ShiftService, Rules/ (IShiftRule, ShiftConflictChecker)
│   │   │   ├── Attendance/
│   │   │   ├── Bills/
│   │   │   ├── Loyalty/
│   │   │   ├── Payroll/
│   │   │   └── Reports/
│   │   └── DependencyInjection.cs
│   │
│   ├── ChonRieng.Infrastructure/         # KỸ THUẬT – EF Core, S3, JWT
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/   SeatConfiguration.cs... (Fluent API)
│   │   │   ├── Repositories/     Repository<T>.cs, SessionRepository.cs
│   │   │   ├── UnitOfWork.cs
│   │   │   ├── Migrations/
│   │   │   └── Seed/DataSeeder.cs
│   │   ├── Services/      JwtTokenService, S3FileStorage, SystemDateTime
│   │   └── DependencyInjection.cs
│   │
│   └── ChonRieng.Api/                    # CỬA VÀO – HTTP, SignalR
│       ├── Controllers/   SessionsController, ShiftsController...
│       ├── Hubs/          BranchHub.cs
│       ├── Middleware/    ExceptionMiddleware.cs
│       ├── Program.cs
│       └── appsettings.json
└── tests/
    ├── ChonRieng.UnitTests/          # test Service bằng UoW giả (Moq/NSubstitute)
    └── ChonRieng.IntegrationTests/   # Testcontainers + SQL Server thật
```

Quy tắc phụ thuộc chỉ đi một chiều: Api → Infrastructure → Application → Domain. Application khai báo interface (`IUnitOfWork`), Infrastructure hiện thực nó, Api nối hai bên bằng DI trong `Program.cs`.

**Design pattern dùng trong project**

| Pattern | Đặt ở đâu | Giải quyết việc gì |
| --- | --- | --- |
| Repository | `IRepository<T>` + repo riêng khi cần query đặc thù | Nghiệp vụ không biết EF Core; dễ mock khi test |
| Unit of Work | `IUnitOfWork` gom các repo + `SaveChangesAsync` | Check-in lưu Session, Bill, Seat trong 1 transaction; nhập bill tích điểm không bị cộng 2 lần |
| Service Layer | `SessionService`, `ShiftService`... | Mỗi use case là một method; controller chỉ nhận request và trả response |
| Strategy + Factory | `IPricingStrategy`, `PricingStrategyFactory` | Giá ban ngày, ca đêm, cuối tuần mà không if-else chồng chất |
| Result | `Result<T>` | Trả lỗi nghiệp vụ ("Chỗ đã có người") không cần ném exception |
| Rich Domain Model | Method trong Entity: `Session.Extend()`, `Bill.Claim()` | Quy tắc nghiệp vụ nằm một chỗ, không rải khắp service |
| Options | `JwtOptions`, `AwsOptions` | Đọc cấu hình kiểu strongly-typed |

**Ví dụ một use case: check-in khách**

```csharp
// Application/Common/Interfaces/IUnitOfWork.cs
public interface IUnitOfWork
{
    IRepository<Seat> Seats { get; }
    IRepository<PricePlan> PricePlans { get; }
    ISessionRepository Sessions { get; }
    IRepository<Bill> Bills { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// Application/Features/Sessions/SessionService.cs
public class SessionService(IUnitOfWork uow, PricingStrategyFactory pricing, IDateTime clock)
    : ISessionService
{
    public async Task<Result<SessionDto>> CheckInAsync(CheckInRequest req, CancellationToken ct)
    {
        var seat = await uow.Seats.GetByIdAsync(req.SeatId, ct);
        if (seat is null || seat.Status != SeatStatus.Available)
            return Result.Fail<SessionDto>("Chỗ này không còn trống");

        var plan  = await uow.PricePlans.GetByIdAsync(req.PricePlanId, ct);
        var price = pricing.Get(clock.Now).Calculate(plan!);      // Strategy

        var session = Session.Start(seat, req.CustomerId, plan!, clock.Now); // logic trong Entity
        var bill    = Bill.Create(session, price, req.PaymentMethod);

        await uow.Sessions.AddAsync(session, ct);
        await uow.Bills.AddAsync(bill, ct);
        await uow.SaveChangesAsync(ct);                            // 1 transaction

        return Result.Ok(session.ToDto());
    }
}

// Api/Controllers/SessionsController.cs
[HttpPost]
public async Task<IActionResult> CheckIn(CheckInRequest req, CancellationToken ct)
    => (await sessions.CheckInAsync(req, ct)).ToActionResult();
```

**Frontend**

```
frontend/
├── packages/
│   ├── ui/           # component dùng chung, theme, màu trạng thái
│   └── api-client/   # client TS sinh từ OpenAPI (orval) + TanStack Query hooks
└── apps/
    ├── admin/        # Vite + React TS
    ├── pos/
    └── customer/     # PWA
```

Bên trong mỗi app, code chia theo tính năng: `src/features/sessions/` chứa page, component và hook riêng (`useActiveSessions`, `useCheckIn`); state server dùng TanStack Query, form dùng React Hook Form + Zod, state UI nhỏ dùng `useState`.

## Lộ trình triển khai

6 sprint, mỗi sprint 2 tuần, làm theo thứ tự phụ thuộc: không có chỗ ngồi thì không bán được, không có bill thì không có điểm hay báo cáo.

&#91;embedded content: lộ trình 12 tuần · 6 sprint, 2 mốc\]

Nếu thiếu thời gian, giữ nguyên Sprint 1–3 và các dòng MVP; các tính năng *(gợi ý)* là phần cắt được.

**Câu hỏi cần chốt trước khi code**

- [ ] Nhóm mấy người và chia việc theo app hay theo tính năng?
- [ ] Giá lượt 4 tiếng và giá gia hạn dùng số mẫu nào?
- [ ] Có cần deploy thật lên AWS khi bảo vệ, hay chạy local là đủ?
