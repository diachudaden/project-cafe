# Bảng dữ liệu – Cafe CR (Chốn Riêng)

- **Ngày tạo:** 06/11/2026
- **Nguồn schema:** `backend/src/ChonRieng.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`, migration mới nhất `20261031080334_RoleWorkflows`
- **Tổng số bảng:** 28, gồm 24 bảng vẽ trong ERD và 4 bảng hệ thống. Không tính `__EFMigrationsHistory`.
- **Sơ đồ đi kèm:** [ERD.md](ERD.md) (mã Mermaid), [ERD.png](ERD.png), [ERD.svg](ERD.svg), [ERD.drawio](ERD.drawio). Trong ERD, tên bảng viết IN_HOA_GACH_DUOI, ví dụ `PricePlans` là `PRICE_PLANS`.

**Cách đọc:** `/admin` là Web Quản lý, `/pos` là ứng dụng quầy, `/` là Web Khách. Tên người, tên chỗ và số tiền trong cột "Use case ví dụ" là minh họa. Cột trạng thái lưu số thì ghi kèm số, ví dụ `Status = 1 (Active)`.

**Job nền:** code hiện không có job nền nào. Trừ việc tạo vai trò và Admin đầu tiên lúc khởi động, dữ liệu chỉ thay đổi khi người dùng thao tác. Ví dụ, một lượt ngồi quá giờ vẫn ở `Active` cho tới khi nhân viên bấm trả chỗ.

## 1. Chi nhánh & chỗ ngồi

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `Branches` | Mỗi dòng là một chi nhánh: `Name`, `Address`, tọa độ `Lat`/`Lng`, `OpenTime`/`CloseTime`, `Is24h`, `IsActive`. | Admin quản lý ở `/admin/branches`. Web Khách hiển thị để tìm chi nhánh. Hầu hết dữ liệu vận hành đều gắn về một chi nhánh. | Admin tạo chi nhánh "Chốn Riêng Quận 3" trên `/admin/branches` → thêm 1 dòng. Khách Bình mở Web Khách → đọc bảng này để xem danh sách chi nhánh. |
| `Zones` | Mỗi dòng là một khu vực trong chi nhánh: `BranchId`, `Name`. Tên không trùng trong cùng chi nhánh. | Quản lý sơ đồ chỗ ở `/admin/floor`, nhóm chỗ ngồi trên màn hình quầy `/pos`. | Quản lý Chi tạo khu "Tầng 2 yên tĩnh" trên `/admin/floor` → thêm 1 dòng. |
| `Seats` | Mỗi dòng là một chỗ ngồi: `ZoneId`, `Code`, `Type` (Single, Group, Nap), `Status`. | Sơ đồ chỗ ở `/admin/floor` và `/pos`. Web Khách đếm số chỗ trống theo loại. Báo cáo lấp đầy. | Quản lý Chi bật bảo trì chỗ A03 → sửa dòng thành `Status = 3 (Maintenance)`. Sau đó An check-in vào A03 trên `/pos` thì bị từ chối. |
| `PricePlans` | Mỗi dòng là một gói giá: `Name`, `DurationMinutes`, `Price`, `ExtendPrice`, `ValidFrom`/`ValidTo`. `BranchId` rỗng nghĩa là áp dụng toàn chuỗi. | Thiết lập ở `/admin/pricing`. `/pos` đọc khi check-in. Web Khách xem bảng giá. Gói của chi nhánh thay thế gói toàn chuỗi cùng thời lượng. | Admin tạo "Gói 4 tiếng" 240 phút, giá 60.000đ, gia hạn 50.000đ, `BranchId` rỗng → thêm 1 dòng. An check-in khách trên `/pos` → đọc dòng này để tính tiền. |

## 2. Người dùng & phân quyền

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `Users` | Mỗi dòng là một tài khoản đăng nhập của mọi vai trò: `FullName`, `Email`, `PhoneNumber`, `PasswordHash`, `LockoutEnd`. | Đăng nhập cả 3 ứng dụng. Admin quản lý ở `/admin/users`. `/pos` tra khách theo email hoặc số điện thoại. | Khách Bình đăng ký trên Web Khách → thêm 1 dòng `Users`, đồng thời thêm 1 dòng `Customers`. |
| `AspNetRoles` | Mỗi dòng là một vai trò: `Name` (`Admin`, `BranchManager`, `Staff`, `Customer`). | API tự tạo các vai trò này khi khởi động. Dùng để phân quyền. | Khi API khởi động lần đầu → thêm 4 dòng. Khi Admin mở `/admin/users` → đọc để hiện vai trò của tài khoản. |
| `AspNetUserRoles` | Mỗi dòng cho biết một tài khoản có một vai trò: `UserId`, `RoleId`. Một tài khoản có thể có nhiều vai trò. | Gán vai trò ở `/admin/users`. Được đọc khi đăng nhập để đưa vai trò vào token. | Admin gán vai trò `BranchManager` cho Chi → thêm 1 dòng. Chi đăng nhập `/admin` → đọc dòng này để mở quyền quản lý. |
| `UserSessions` | Mỗi dòng là một phiên đăng nhập: `UserId`, `DeviceId`, `RefreshTokenHash`, `ExpiresAt`, `RevokedAt`. | Giữ đăng nhập bằng refresh token ở cả 3 ứng dụng. Không hiện trên màn hình nào. | An đăng nhập `/pos` → thêm 1 dòng. Token được làm mới → đổi `RefreshTokenHash` và `ExpiresAt`. An đăng xuất → ghi `RevokedAt`. |
| `Employees` | Mỗi dòng là hồ sơ nhân viên, 1–1 với `Users`: `HourlyRate`, `NightAllowance`, `PinHash`. | Quản lý ở `/admin/employees`. Mức lương được đọc khi tính lương. | Quản lý Chi đặt lương của An là 30.000đ/giờ, phụ cấp đêm 5.000đ/giờ trên `/admin/employees` → sửa dòng của An. |
| `EmployeeBranches` | Mỗi dòng cho biết nhân viên được gán vào chi nhánh nào: `EmployeeId`, `BranchId`, `IsHome` (chi nhánh chính). | Xác định phạm vi quyền theo chi nhánh. Kiểm tra khi xếp ca. Bảng lương tính theo chi nhánh chính. | Quản lý Chi gán An vào Quận 3 (chi nhánh chính) và Quận 1 trên `/admin/employees` → thêm 2 dòng, dòng Quận 3 có `IsHome = 1`. |
| `Customers` | Mỗi dòng là hồ sơ thành viên, 1–1 với `Users`: `MemberCode`, `Points`, `Tier`, `TotalSpent`. | Trang điểm của khách ở `/points`. `/pos` tra thành viên khi check-in. | Bình đăng ký → thêm 1 dòng `Tier = Bronze`, `Points = 0`. An nhập số điện thoại của Bình trên `/pos` → đọc dòng này để gắn khách vào lượt ngồi. |

## 3. Ca làm & chấm công

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `Shifts` | Mỗi dòng là một ca làm của chi nhánh: `Date`, `Type`, `StartAt`/`EndAt`, `RequiredStaff`, `IsOpen` (cho nhân viên tự nhận), `Status`, `RowVersion`. | Lập lịch ở `/admin/schedule`. Nhân viên xem ở `/pos/schedule`. `RowVersion` chặn hai người cùng sửa, hoặc cùng nhận suất cuối của ca. | Quản lý Chi tạo ca sáng 07:00–12:00 ngày 10/11 → thêm 1 dòng, `Status = 1 (Draft)`. Chi công bố tuần → sửa thành `Status = 2 (Published)`. |
| `ShiftAssignments` | Mỗi dòng là một suất phân công nhân viên vào ca: `ShiftId`, `EmployeeId`, `Status`, `DeclineReason`, `OverrideReason`. | Phân công ở `/admin/schedule`. Nhân viên nhận hoặc từ chối ở `/pos/schedule`. | Chi giao ca sáng cho An → thêm 1 dòng, `Status = 1 (Offered)`. An bấm nhận trên `/pos/schedule` → sửa thành `Status = 2 (Accepted)` và ghi `RespondedAt`. |
| `ShiftRequests` | Mỗi dòng là một đơn đổi ca, nghỉ hoặc trả ca: `Type`, `ShiftId`, `RequesterEmployeeId`, `TargetEmployeeId`, `Reason`, `Status`. | Nhân viên gửi và hủy đơn ở `/pos/schedule`. Quản lý duyệt ở `/admin/requests`. | An xin đổi ca sáng cho Dương → thêm 1 dòng, `Type = 1 (Swap)`, `Status = Pending`. Chi duyệt trên `/admin/requests` → sửa thành `Approved`, ca chuyển sang Dương. |
| `Availabilities` | Mỗi dòng là một khung giờ rảnh của nhân viên: lặp hằng tuần (`DayOfWeek`) hoặc một ngày cụ thể (`SpecificDate`), kèm `From`/`To`. | Nhân viên khai báo ở `/pos/schedule`. Quản lý tham khảo khi xếp lịch. | An khai báo rảnh thứ Ba 18:00–22:00 hằng tuần → xóa các dòng cũ của An rồi thêm lại, trong đó có 1 dòng `IsRecurring = 1`, `DayOfWeek = 2`. |
| `Attendances` | Mỗi dòng là chấm công của một nhân viên cho một ca: `CheckInAt`, `CheckOutAt`, `Method`, `Approved`, `ApprovedByUserId`. | Nút "Tham gia ca" trên `/pos`. Duyệt ở `/admin/attendance`. Là đầu vào tính lương. Muốn bán hàng, nhân viên phải có dòng đang mở ở đây. | An bấm Tham gia ca lúc 06:50 trên `/pos` → thêm 1 dòng, ghi `CheckInAt`. An nộp kết ca → ghi `CheckOutAt`. Chi duyệt trên `/admin/attendance` → `Approved = 1`. |

## 4. Bán hàng

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `Sessions` | Mỗi dòng là một lượt ngồi của khách: `SeatId`, `CustomerId` (rỗng là khách vãng lai), `PricePlanId`, `StartAt`/`EndAt`, `Status`, `ClosedAt`. | Màn hình quầy `/pos`. Web Khách xem lượt đang ngồi. Báo cáo lấp đầy. Mỗi chỗ chỉ có một lượt `Active`, nên hai máy POS không bán trùng chỗ. | An check-in khách Bình vào chỗ A03 trên `/pos` → thêm 1 dòng, `Status = 1 (Active)`. Bình trả chỗ đúng giờ → sửa thành `Status = 3 (Closed)`. Nếu trả chỗ quá giờ → `Status = 2 (Expired)`. |
| `SessionExtensions` | Mỗi dòng là một lần gia hạn: `SessionId`, `AddedMinutes`, `Price`. | Nút gia hạn trên `/pos`. Lưu lịch sử gia hạn và đếm số lần gia hạn của lượt ngồi. | An gia hạn cho Bình trên `/pos` → thêm 1 dòng 240 phút, 50.000đ. `Sessions.EndAt` cộng thêm 240 phút, `Bills.Total` cộng thêm 50.000đ. |
| `Bills` | Mỗi dòng là hóa đơn của một lượt ngồi: `Code` (mã 8 ký tự), `Total`, `PaymentMethod`, `ShiftId`, `BranchId`, `ClaimedByCustomerId`, `ClaimedAt`. | Tạo khi check-in vì khách trả tiền trước. Là nguồn doanh thu của báo cáo `/admin/reports`, tiền dự kiến khi kết ca và mã để khách tích điểm. | An check-in Bình như khách vãng lai, thu 60.000đ tiền mặt → thêm 1 dòng `Code = K7M2QX9A`, `PaymentMethod = 1 (Cash)`, `ClaimedByCustomerId` còn rỗng. Cuối ca, wizard kết ca cộng các bill tiền mặt của ca để ra tiền dự kiến. |

## 5. Tích điểm

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `PointTransactions` | Mỗi dòng là một lần cộng điểm: `CustomerId`, `BillId`, `Points`, `Reason`. Quy tắc: 1.000đ = 1 điểm. | Lịch sử điểm ở `/points`. Ghi khi check-in hoặc gia hạn có gắn thành viên, và khi khách nhập mã bill ở `/claim`. | Bill `K7M2QX9A` 60.000đ chưa gắn thành viên, tối đa 7 ngày sau Bình nhập mã trên `/claim` → thêm 1 dòng `Points = 60`. `Customers.Points` tăng 60, `Bills.ClaimedByCustomerId` ghi là Bình. |

## 6. Tài chính & lương

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `ShiftClosings` | Mỗi dòng là biên bản kết ca, một ca chỉ có một biên bản: `ExpectedCash`, `CountedCash`, `Difference`, `Denominations` (JSON số tờ theo mệnh giá), `Note`, `Status`, thông tin duyệt. | Wizard kết ca trên `/pos`. Quản lý duyệt ở `/admin/closings`. | An đếm được 1.950.000đ, dự kiến 2.000.000đ → thêm 1 dòng `Difference = -50.000`, bắt buộc có `Note`, `Status = 2 (Submitted)`. Chi duyệt → sửa thành `Status = 3 (Approved)`. |
| `Expenses` | Mỗi dòng là một khoản chi của chi nhánh: `Category`, `Amount`, `Date`, `Note`. Chỉ thêm, không sửa: muốn sửa sai thì ghi bút toán đảo qua `ReversesExpenseId`. | Quản lý ở `/admin/expenses`. Báo cáo lãi lỗ ở `/admin/reports`. | Chi ghi "Điện nước" 1.200.000đ → thêm 1 dòng. Phát hiện nhập nhầm, Chi bấm đảo → thêm 1 dòng `Amount = -1.200.000`, `ReversesExpenseId` trỏ về dòng cũ. |
| `Payrolls` | Mỗi dòng là bảng lương của một nhân viên trong một tháng: `Month`, `TotalHours`, `BaseAmount`, `Allowance`, `Bonus`, `Deduction`, `NetAmount`, `Status`. | Tính và chốt ở `/admin/payroll`. Nhân viên xem ở `/pos/payroll`. Chi phí lương trong báo cáo lãi lỗ. | Chi tính lương tháng 10 cho An, 100 giờ đã duyệt × 30.000đ → thêm 1 dòng `BaseAmount = 3.000.000`, `Status = Draft`. Admin chốt → sửa thành `Finalized`, không sửa được nữa. |

## 7. Thông báo

| Bảng | Lưu cái gì | Để làm gì | Use case ví dụ |
| --- | --- | --- | --- |
| `Notifications` | Mỗi dòng là một thông báo gửi cho một tài khoản: `Type`, `Title`, `Body`, `Link`, `ReadAt`. | Feed Hoạt động, ví dụ `/pos/activity`. Được ghi khi có ca mới, đơn mới, biên bản kết ca, lương đã chốt. | Chi duyệt đơn của An → thêm 1 dòng cho An, `Type = request.reviewed`. An mở feed và đánh dấu đã đọc → ghi `ReadAt`. |

## Bảng hệ thống

Đây là các bảng phụ của ASP.NET Identity. Chúng không được vẽ trong ERD, và code nghiệp vụ không dùng trực tiếp.

| Bảng | Cột | Khóa ngoại | Vai trò |
| --- | --- | --- | --- |
| `AspNetUserClaims` | `Id`, `UserId`, `ClaimType`, `ClaimValue` | `UserId` → `Users` | Claim riêng của tài khoản |
| `AspNetUserLogins` | `LoginProvider`, `ProviderKey`, `ProviderDisplayName`, `UserId` | `UserId` → `Users` | Đăng nhập qua nhà cung cấp ngoài |
| `AspNetUserTokens` | `UserId`, `LoginProvider`, `Name`, `Value` | `UserId` → `Users` | Token của Identity |
| `AspNetRoleClaims` | `Id`, `RoleId`, `ClaimType`, `ClaimValue` | `RoleId` → `AspNetRoles` | Claim gắn với vai trò |

`AspNetRoles` và `AspNetUserRoles` cũng là bảng Identity, nhưng được đưa vào ERD chính vì phân quyền theo vai trò là nghiệp vụ cốt lõi.

## Ghi chú về schema

**Cột tham chiếu nhưng không có khóa ngoại.** Các cột dưới đây lưu Id của bảng khác, nhưng database không ràng buộc, nên ERD không có đường quan hệ:

| Cột | Trỏ tới về mặt logic |
| --- | --- |
| `Attendances.ApprovedByUserId` | `Users.Id` |
| `Expenses.CreatedByUserId` | `Users.Id` |
| `Expenses.ReversesExpenseId` | `Expenses.Id` |
| `ShiftClosings.ReviewedByUserId` | `Users.Id` |

**Unique nhưng vẫn là quan hệ 1–nhiều.** ERD chỉ vẽ 1–1 khi unique index nằm riêng trên cột FK và không có filter. Có 4 quan hệ 1–1: `Users`–`Employees`, `Users`–`Customers`, `Sessions`–`Bills`, `Shifts`–`ShiftClosings`. Các index có filter dưới đây chỉ giới hạn một phần dữ liệu:

| Index | Ý nghĩa |
| --- | --- |
| `Sessions.SeatId`, lọc `Status = 1` | Mỗi chỗ chỉ có một lượt đang hoạt động, nhưng có nhiều lượt theo thời gian |
| `EmployeeBranches.EmployeeId`, lọc `IsHome = 1` | Mỗi nhân viên chỉ có một chi nhánh chính, nhưng có thể thuộc nhiều chi nhánh |
| `UserSessions(UserId, IsPos)`, lọc `RevokedAt IS NULL AND IsPos = 1` | Mỗi tài khoản chỉ có một phiên POS đang hoạt động |

## Những chỗ cần xác nhận

1. `Seats.Status = 2 (Occupied)` **(cần xác nhận)**: giá trị có trong enum, nhưng không có code nào ghi. Trạng thái có khách được suy ra từ `Sessions`.
2. `UserSessions.IsPos` và `UserSessions.BranchId` **(cần xác nhận)**: code đăng nhập luôn ghi `IsPos = 0` và không ghi `BranchId`. Có vẻ để dành cho đăng nhập POS bằng PIN.
3. `Employees.PinHash` **(cần xác nhận)**: có lưu, nhưng chưa có luồng đăng nhập bằng PIN. Comment trong code ghi đây là phase sau.
4. `Attendances.Method` **(cần xác nhận)**: code nghiệp vụ chỉ ghi `App`. Chưa rõ có dự định thêm phương thức khác không.
5. `Customers.Tier` **(cần xác nhận)**: giá trị chuẩn là `Bronze`, `Silver`, `Gold`, nhưng code vẫn xử lý giá trị cũ `DEMO` và tự sửa khi khách mở trang điểm. Chưa rõ database thật còn dữ liệu này không.
6. 4 cột tham chiếu không có khóa ngoại ở mục trên **(cần xác nhận)**: chưa rõ đây là thiết kế có chủ đích hay còn thiếu ràng buộc.
