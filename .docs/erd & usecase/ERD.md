---
title: "ERD vật lý Cafe CR (Chốn Riêng), 24 bảng nghiệp vụ"
config:
  layout: elk
  er:
    useMaxWidth: false
---
%% Sinh từ AppDbContextModelSnapshot.cs, migration mới nhất 20261031080334_RoleWorkflows.
%% Ký hiệu crow's foot:
%%   ||  đúng 1            (phía bảng cha: FK NOT NULL)
%%   |o  0 hoặc 1          (phía bảng cha: FK cho phép NULL)
%%   o{  0 hoặc nhiều      (phía bảng con: FK không unique)
%%   o|  0 hoặc 1          (phía bảng con: quan hệ 1-1)
%% Quan hệ 1-1 chỉ khi unique index nằm riêng trên cột FK và không có filter.
%% UK ghép hoặc UK có filter vẫn là 1-nhiều, ví dụ Sessions.SeatId, EmployeeBranches.EmployeeId.
%% Cột "tham chiếu logic" không có FK trong database nên không có đường quan hệ.
%% 4 bảng Identity phụ không vẽ, xem mục Bảng hệ thống trong BANG-DU-LIEU.md.
erDiagram
    direction LR

    %% ==== CHI NHÁNH & CHỖ NGỒI ====
    BRANCHES {
        uniqueidentifier Id PK
        nvarchar(300) Address
        time CloseTime
        bit Is24h
        bit IsActive
        decimal(9,6) Lat "NULL"
        decimal(9,6) Lng "NULL"
        nvarchar(150) Name
        time OpenTime
        nvarchar(32) Phone "NULL"
    }
    ZONES {
        uniqueidentifier Id PK
        uniqueidentifier BranchId FK, UK "UK(BranchId, Name)"
        nvarchar(100) Name UK "UK(BranchId, Name)"
    }
    SEATS {
        uniqueidentifier Id PK
        nvarchar(32) Code UK "UK(ZoneId, Code)"
        tinyint Status "1 Available, 2 Occupied, 3 Maintenance"
        tinyint Type "1 Single, 2 Group, 3 Nap"
        uniqueidentifier ZoneId FK, UK "UK(ZoneId, Code)"
    }
    PRICE_PLANS {
        uniqueidentifier Id PK
        uniqueidentifier BranchId FK "NULL = toàn chuỗi"
        int DurationMinutes
        decimal(12,0) ExtendPrice
        nvarchar(120) Name
        decimal(12,0) Price
        datetimeoffset ValidFrom "NULL"
        datetimeoffset ValidTo "NULL"
    }
    BRANCHES ||--o{ ZONES : "có"
    ZONES ||--o{ SEATS : "gồm"
    BRANCHES |o--o{ PRICE_PLANS : "áp dụng"

    %% ==== NGƯỜI DÙNG & PHÂN QUYỀN ====
    USERS {
        uniqueidentifier Id PK
        int AccessFailedCount
        nvarchar(max) ConcurrencyStamp "NULL"
        nvarchar(256) Email "NULL"
        bit EmailConfirmed
        nvarchar(150) FullName
        bit LockoutEnabled
        datetimeoffset LockoutEnd "NULL"
        nvarchar(256) NormalizedEmail UK "NULL; UK(NormalizedEmail) lọc NormalizedEmail IS NOT NULL"
        nvarchar(256) NormalizedUserName UK "NULL; UK(NormalizedUserName) lọc NormalizedUserName IS NOT NULL"
        nvarchar(max) PasswordHash "NULL"
        nvarchar(32) PhoneNumber UK "NULL; UK(PhoneNumber) lọc PhoneNumber IS NOT NULL"
        bit PhoneNumberConfirmed
        nvarchar(max) SecurityStamp "NULL"
        bit TwoFactorEnabled
        nvarchar(256) UserName "NULL"
    }
    ASP_NET_ROLES {
        uniqueidentifier Id PK
        nvarchar(max) ConcurrencyStamp "NULL"
        nvarchar(256) Name "NULL"
        nvarchar(256) NormalizedName UK "NULL; UK(NormalizedName) lọc NormalizedName IS NOT NULL"
    }
    ASP_NET_USER_ROLES {
        uniqueidentifier UserId PK, FK
        uniqueidentifier RoleId PK, FK
    }
    USER_SESSIONS {
        uniqueidentifier Id PK
        uniqueidentifier BranchId FK "NULL"
        datetimeoffset CreatedAt
        nvarchar(200) DeviceId
        datetimeoffset ExpiresAt "NULL"
        bit IsPos UK "UK(UserId, IsPos) lọc RevokedAt IS NULL AND IsPos = 1"
        nvarchar(256) RefreshTokenHash "concurrency token"
        datetimeoffset RevokedAt "NULL"
        uniqueidentifier UserId FK, UK "UK(UserId, IsPos) lọc RevokedAt IS NULL AND IsPos = 1"
    }
    EMPLOYEES {
        uniqueidentifier Id PK
        decimal(12,0) HourlyRate
        decimal(12,0) NightAllowance
        nvarchar(256) PinHash
        uniqueidentifier UserId FK, UK "UK(UserId)"
    }
    EMPLOYEE_BRANCHES {
        uniqueidentifier Id PK
        uniqueidentifier BranchId FK, UK "UK(EmployeeId, BranchId)"
        uniqueidentifier EmployeeId FK, UK "UK(EmployeeId) lọc IsHome = 1; UK(EmployeeId, BranchId)"
        bit IsHome
    }
    CUSTOMERS {
        uniqueidentifier Id PK
        nvarchar(16) MemberCode UK "NULL; UK(MemberCode) lọc MemberCode IS NOT NULL"
        int Points
        nvarchar(40) Tier "Bronze, Silver, Gold"
        decimal(12,0) TotalSpent
        uniqueidentifier UserId FK, UK "UK(UserId)"
    }
    ASP_NET_ROLES ||--o{ ASP_NET_USER_ROLES : "gán cho"
    USERS ||--o{ ASP_NET_USER_ROLES : "được gán"
    BRANCHES |o--o{ USER_SESSIONS : "gắn phiên"
    USERS ||--o{ USER_SESSIONS : "đăng nhập"
    USERS ||--o| EMPLOYEES : "là nhân viên"
    BRANCHES ||--o{ EMPLOYEE_BRANCHES : "có nhân sự"
    EMPLOYEES ||--o{ EMPLOYEE_BRANCHES : "thuộc"
    USERS ||--o| CUSTOMERS : "là khách"

    %% ==== CA LÀM & CHẤM CÔNG ====
    SHIFTS {
        uniqueidentifier Id PK
        uniqueidentifier BranchId FK
        date Date
        datetimeoffset EndAt "CK EndAt lớn hơn StartAt"
        bit IsOpen
        int RequiredStaff
        rowversion RowVersion "chống ghi đè đồng thời"
        datetimeoffset StartAt
        tinyint Status "1 Draft, 2 Published"
        tinyint Type "1 Morning, 2 Afternoon, 3 Evening, 4 Night"
    }
    SHIFT_ASSIGNMENTS {
        uniqueidentifier Id PK
        nvarchar(500) DeclineReason "NULL"
        uniqueidentifier EmployeeId FK, UK "UK(ShiftId, EmployeeId)"
        nvarchar(500) OverrideReason "NULL"
        datetimeoffset RespondedAt "NULL"
        uniqueidentifier ShiftId FK, UK "UK(ShiftId, EmployeeId)"
        tinyint Status "1 Offered, 2 Accepted, 3 Declined"
    }
    SHIFT_REQUESTS {
        uniqueidentifier Id PK
        datetimeoffset CreatedAt
        nvarchar(1000) Reason
        uniqueidentifier RequesterEmployeeId FK
        datetimeoffset ReviewedAt "NULL"
        uniqueidentifier ReviewedByEmployeeId FK "NULL"
        uniqueidentifier ShiftId FK
        nvarchar(32) Status "Pending, Approved, Rejected, Cancelled"
        uniqueidentifier TargetEmployeeId FK "NULL"
        tinyint Type "1 Swap, 2 Leave, 3 Drop"
    }
    AVAILABILITIES {
        uniqueidentifier Id PK
        tinyint DayOfWeek "NULL; 0 CN, 1 T2, 2 T3, 3 T4, 4 T5, 5 T6, 6 T7"
        uniqueidentifier EmployeeId FK
        time From
        bit IsRecurring "CK 1 thì có DayOfWeek, 0 thì có SpecificDate"
        date SpecificDate "NULL"
        time To "CK From nhỏ hơn To"
    }
    ATTENDANCES {
        uniqueidentifier Id PK
        bit Approved
        datetimeoffset ApprovedAt "NULL"
        uniqueidentifier ApprovedByUserId "NULL; tham chiếu logic Users.Id, không có FK"
        datetimeoffset CheckInAt "NULL"
        datetimeoffset CheckOutAt "NULL"
        uniqueidentifier EmployeeId FK, UK "UK(EmployeeId, ShiftId)"
        nvarchar(32) Method "code hiện chỉ ghi App"
        uniqueidentifier ShiftId FK, UK "UK(EmployeeId, ShiftId)"
    }
    BRANCHES ||--o{ SHIFTS : "xếp ca"
    EMPLOYEES ||--o{ SHIFT_ASSIGNMENTS : "được giao"
    SHIFTS ||--o{ SHIFT_ASSIGNMENTS : "phân công"
    EMPLOYEES ||--o{ SHIFT_REQUESTS : "gửi đơn"
    EMPLOYEES |o--o{ SHIFT_REQUESTS : "duyệt đơn"
    SHIFTS ||--o{ SHIFT_REQUESTS : "có đơn"
    EMPLOYEES |o--o{ SHIFT_REQUESTS : "nhận thay"
    EMPLOYEES ||--o{ AVAILABILITIES : "khai báo"
    EMPLOYEES ||--o{ ATTENDANCES : "chấm công"
    SHIFTS ||--o{ ATTENDANCES : "ghi nhận"

    %% ==== BÁN HÀNG ====
    SESSIONS {
        uniqueidentifier Id PK
        datetimeoffset ClosedAt "NULL"
        uniqueidentifier CustomerId FK "NULL = khách vãng lai"
        datetimeoffset EndAt
        uniqueidentifier PricePlanId FK
        uniqueidentifier SeatId FK, UK "UK(SeatId) lọc Status = 1"
        datetimeoffset StartAt
        tinyint Status "1 Active, 2 Expired, 3 Closed"
    }
    SESSION_EXTENSIONS {
        uniqueidentifier Id PK
        int AddedMinutes
        datetimeoffset CreatedAt
        decimal(12,0) Price
        uniqueidentifier SessionId FK
    }
    BILLS {
        uniqueidentifier Id PK
        uniqueidentifier BranchId FK
        datetimeoffset ClaimedAt "NULL"
        uniqueidentifier ClaimedByCustomerId FK "NULL; concurrency token"
        nvarchar(8) Code UK "UK(Code)"
        datetimeoffset CreatedAt
        tinyint PaymentMethod "1 Cash, 2 VietQr"
        uniqueidentifier SessionId FK, UK "UK(SessionId)"
        uniqueidentifier ShiftId FK
        decimal(12,0) Total
    }
    CUSTOMERS |o--o{ SESSIONS : "sử dụng"
    PRICE_PLANS ||--o{ SESSIONS : "áp giá"
    SEATS ||--o{ SESSIONS : "phục vụ"
    SESSIONS ||--o{ SESSION_EXTENSIONS : "gia hạn"
    BRANCHES ||--o{ BILLS : "thu"
    CUSTOMERS |o--o{ BILLS : "nhận điểm"
    SESSIONS ||--o| BILLS : "xuất"
    SHIFTS ||--o{ BILLS : "ghi doanh thu"

    %% ==== TÍCH ĐIỂM ====
    POINT_TRANSACTIONS {
        uniqueidentifier Id PK
        uniqueidentifier BillId FK "NULL"
        datetimeoffset CreatedAt
        uniqueidentifier CustomerId FK
        int Points
        nvarchar(200) Reason
    }
    BILLS |o--o{ POINT_TRANSACTIONS : "sinh điểm"
    CUSTOMERS ||--o{ POINT_TRANSACTIONS : "tích"

    %% ==== TÀI CHÍNH & LƯƠNG ====
    SHIFT_CLOSINGS {
        uniqueidentifier Id PK
        uniqueidentifier ClosedByEmployeeId FK
        decimal(12,0) CountedCash
        nvarchar(max) Denominations "JSON mệnh giá và số tờ"
        decimal(12,0) Difference
        decimal(12,0) ExpectedCash
        nvarchar(1000) Note "NULL"
        nvarchar(1000) ReviewNote "NULL"
        datetimeoffset ReviewedAt "NULL"
        uniqueidentifier ReviewedByUserId "NULL; tham chiếu logic Users.Id, không có FK"
        uniqueidentifier ShiftId FK, UK "UK(ShiftId)"
        tinyint Status "1 Draft, 2 Submitted, 3 Approved, 4 Rejected"
        datetimeoffset SubmittedAt "NULL"
    }
    EXPENSES {
        uniqueidentifier Id PK
        decimal(12,0) Amount
        uniqueidentifier BranchId FK
        nvarchar(80) Category
        uniqueidentifier CreatedByEmployeeId FK "NULL"
        uniqueidentifier CreatedByUserId "NULL; tham chiếu logic Users.Id, không có FK"
        datetimeoffset Date
        nvarchar(1000) Note "NULL"
        uniqueidentifier ReversesExpenseId UK "NULL; tham chiếu logic Expenses.Id, không có FK; UK(ReversesExpenseId) lọc ReversesExpenseId IS NOT NULL"
    }
    PAYROLLS {
        uniqueidentifier Id PK
        decimal(12,0) Allowance
        decimal(12,0) BaseAmount
        decimal(12,0) Bonus
        decimal(12,0) Deduction
        uniqueidentifier EmployeeId FK, UK "UK(EmployeeId, Month)"
        date Month UK "ngày đầu tháng; UK(EmployeeId, Month)"
        decimal(12,0) NetAmount
        nvarchar(32) Status "Draft, Finalized"
        decimal(8,2) TotalHours
    }
    EMPLOYEES ||--o{ SHIFT_CLOSINGS : "lập"
    SHIFTS ||--o| SHIFT_CLOSINGS : "kết"
    BRANCHES ||--o{ EXPENSES : "chi"
    EMPLOYEES |o--o{ EXPENSES : "ghi"
    EMPLOYEES ||--o{ PAYROLLS : "nhận lương"

    %% ==== THÔNG BÁO ====
    NOTIFICATIONS {
        uniqueidentifier Id PK
        nvarchar(2000) Body
        datetimeoffset CreatedAt
        nvarchar(500) Link "NULL"
        datetimeoffset ReadAt "NULL"
        nvarchar(200) Title
        nvarchar(64) Type "shift.offered, shift.changed, shift.declined, request.created, request.reviewed, closing.submitted, closing.reviewed, payroll.finalized"
        uniqueidentifier UserId FK
    }
    USERS ||--o{ NOTIFICATIONS : "nhận"
