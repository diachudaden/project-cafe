using System.Security.Claims;
using ChonRieng.Api.Features.Common;

namespace ChonRieng.Api.Features.Scheduling;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this WebApplication app)
    {
        var shifts = app.MapGroup("/api/shifts").WithTags("Ca làm").RequireWorkforce();

        shifts.MapGet("/", async (Guid branchId, DateOnly from, DateOnly to, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.ListAsync(CurrentUser.From(user), branchId, from, to, ct)).ToHttp())
            .WithName("ListBranchShifts")
            .WithSummary("Lịch ca của chi nhánh")
            .WithDescription("Ca trong khoảng ngày (tối đa 62 ngày) kèm phân công, số người đã nhận và cảnh báo thiếu người.");

        shifts.MapGet("/{shiftId:guid}", async (Guid shiftId, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.GetAsync(CurrentUser.From(user), shiftId, ct)).ToHttp())
            .WithName("GetShift").WithSummary("Chi tiết ca");

        shifts.MapPost("/", async (CreateShiftRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.CreateAsync(CurrentUser.From(user), request, ct)).ToHttp(s => TypedResults.Created($"/api/shifts/{s.Id}", s)))
            .RequireManagement()
            .WithName("CreateShift").WithSummary("Tạo ca (Nháp)");

        shifts.MapPut("/{shiftId:guid}", async (Guid shiftId, UpdateShiftRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.UpdateAsync(CurrentUser.From(user), shiftId, request, ct)).ToHttp())
            .RequireManagement()
            .WithName("UpdateShift")
            .WithSummary("Sửa / dời ca")
            .WithDescription("Gửi kèm rowVersion; trả 409 nếu ca vừa được người khác cập nhật.");

        shifts.MapDelete("/{shiftId:guid}", async (Guid shiftId, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.DeleteAsync(CurrentUser.From(user), shiftId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .RequireManagement()
            .WithName("DeleteShift").WithSummary("Xóa ca chưa diễn ra");

        shifts.MapPost("/validate", async (ValidateAssignmentRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.ValidateAsync(CurrentUser.From(user), request.ShiftId, request.EmployeeId, ct)).ToHttp())
            .RequireManagement()
            .WithName("ValidateShiftAssignment")
            .WithSummary("Kiểm tra xung đột trước khi xếp ca (không lưu)")
            .WithDescription("Trả danh sách vi phạm: overlap, branch, leave, rest (bỏ qua được kèm lý do), travel, weekly-hours (cảnh báo).");

        shifts.MapPost("/{shiftId:guid}/assignments", async (Guid shiftId, AssignRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.AssignAsync(CurrentUser.From(user), shiftId, request, ct)).ToHttp())
            .RequireManagement()
            .WithName("AssignShift")
            .WithSummary("Giao ca cho nhân viên")
            .WithDescription("Chạy lại toàn bộ quy tắc ở máy chủ. 409 kèm violations nếu bị chặn.");

        shifts.MapPost("/publish", async (PublishRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.PublishAsync(CurrentUser.From(user), request, ct)).ToHttp())
            .RequireManagement()
            .WithName("PublishWeek").WithSummary("Công bố lịch tuần (Nháp → Công bố) và báo cho nhân viên");

        shifts.MapPost("/copy-week", async (CopyWeekRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.CopyWeekAsync(CurrentUser.From(user), request, ct)).ToHttp())
            .RequireManagement()
            .WithName("CopyWeek").WithSummary("Sao chép lịch tuần, chỉ báo các ca bị xung đột");

        shifts.MapGet("/open", (ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                service.OpenShiftsAsync(CurrentUser.From(user), ct))
            .WithName("ListOpenShifts").WithSummary("Ca trống tôi có thể nhận");

        shifts.MapPost("/{shiftId:guid}/claim", async (Guid shiftId, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.ClaimAsync(CurrentUser.From(user), shiftId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("ClaimOpenShift").WithSummary("Nhận ca trống (ai nhận trước được trước)");

        var assignments = app.MapGroup("/api/assignments").WithTags("Ca làm").RequireWorkforce();

        assignments.MapPost("/{assignmentId:guid}/accept", async (Guid assignmentId, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.AcceptAsync(CurrentUser.From(user), assignmentId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("AcceptAssignment").WithSummary("Nhận ca được giao");

        assignments.MapPost("/{assignmentId:guid}/decline", async (Guid assignmentId, DeclineRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.DeclineAsync(CurrentUser.From(user), assignmentId, request.Reason, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("DeclineAssignment").WithSummary("Từ chối ca (bắt buộc lý do)");

        assignments.MapDelete("/{assignmentId:guid}", async (Guid assignmentId, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                (await service.UnassignAsync(CurrentUser.From(user), assignmentId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .RequireManagement()
            .WithName("RemoveAssignment").WithSummary("Gỡ nhân viên khỏi ca");

        var attendance = app.MapGroup("/api/attendances").WithTags("Chấm công").RequireWorkforce();

        attendance.MapPost("/check-in", async (CheckInRequest request, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
                (await service.CheckInAsync(CurrentUser.From(user), request.ShiftId, ct)).ToHttp())
            .WithName("AttendanceCheckIn")
            .WithSummary("Tham gia ca (điểm danh)")
            .WithDescription("Mở từ 15 phút trước giờ bắt đầu; chỉ cho ca đã nhận. Đến trễ hơn 10 phút được đánh dấu trễ.");

        attendance.MapPost("/check-out", async (CheckInRequest request, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
                (await service.CheckOutAsync(CurrentUser.From(user), request.ShiftId, ct)).ToHttp())
            .WithName("AttendanceCheckOut").WithSummary("Rời ca không qua wizard kết ca");

        attendance.MapGet("/", async (Guid branchId, DateOnly from, DateOnly to, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
                (await service.ListAsync(CurrentUser.From(user), branchId, from, to, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListAttendance").WithSummary("Duyệt chấm công: giờ ca so với giờ thực tế, trễ, về sớm, tăng ca, vắng");

        attendance.MapPost("/{attendanceId:guid}/approve", async (Guid attendanceId, ApproveAttendanceRequest request, ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
                (await service.ApproveAsync(CurrentUser.From(user), attendanceId, request.Approved, ct)).ToHttp())
            .RequireManagement()
            .WithName("ApproveAttendance").WithSummary("Duyệt / bỏ duyệt chấm công");

        var me = app.MapGroup("/api/me").WithTags("Của tôi").RequireWorkforce();

        me.MapGet("/shifts", (DateOnly from, DateOnly to, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
                service.MyShiftsAsync(CurrentUser.From(user), from, to, ct))
            .WithName("MyShifts").WithSummary("Lịch ca của tôi kèm đồng nghiệp cùng ca");

        me.MapGet("/current-shift", async (ClaimsPrincipal user, AttendanceService service, CancellationToken ct) =>
                await service.CurrentAsync(CurrentUser.From(user), ct) is { } current ? Results.Ok(current) : Results.NoContent())
            .WithName("MyCurrentShift").WithSummary("Ca tôi đang làm (204 nếu chưa tham gia ca nào)");

        me.MapGet("/shift-requests", (ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                service.MyRequestsAsync(CurrentUser.From(user), ct))
            .WithName("MyShiftRequests").WithSummary("Đơn của tôi (và đơn nhờ tôi nhận thay)");

        me.MapGet("/colleagues", (ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                service.ColleaguesAsync(CurrentUser.From(user), ct))
            .WithName("MyColleagues").WithSummary("Đồng nghiệp cùng chi nhánh (để chọn người nhận đổi ca)");

        me.MapGet("/availability", (ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                service.MyAvailabilityAsync(CurrentUser.From(user), ct))
            .WithName("MyAvailability").WithSummary("Giờ rảnh của tôi");

        me.MapPut("/availability", async (IReadOnlyList<AvailabilitySlot> slots, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.ReplaceAvailabilityAsync(CurrentUser.From(user), slots, ct)).ToHttp())
            .WithName("ReplaceMyAvailability").WithSummary("Thay toàn bộ giờ rảnh");

        var requests = app.MapGroup("/api/shift-requests").WithTags("Đơn đổi ca / nghỉ").RequireWorkforce();

        requests.MapPost("/", async (CreateShiftRequestRequest request, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.CreateRequestAsync(CurrentUser.From(user), request, ct)).ToHttp(r => TypedResults.Created($"/api/shift-requests/{r.Id}", r)))
            .WithName("CreateShiftRequest")
            .WithSummary("Gửi đơn đổi ca / xin nghỉ / trả ca")
            .WithDescription("Swap cần targetEmployeeId; người nhận thay được kiểm tra trùng lịch.");

        requests.MapGet("/", async (Guid branchId, string? status, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.ListRequestsAsync(CurrentUser.From(user), branchId, status, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListBranchShiftRequests").WithSummary("Hàng chờ đơn của chi nhánh");

        requests.MapPost("/{requestId:guid}/approve", async (Guid requestId, ReviewRequest request, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.ReviewAsync(CurrentUser.From(user), requestId, true, request.Note, ct)).ToHttp())
            .RequireManagement()
            .WithName("ApproveShiftRequest").WithSummary("Duyệt đơn; lịch tự cập nhật và báo cho các bên");

        requests.MapPost("/{requestId:guid}/reject", async (Guid requestId, ReviewRequest request, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.ReviewAsync(CurrentUser.From(user), requestId, false, request.Note, ct)).ToHttp())
            .RequireManagement()
            .WithName("RejectShiftRequest").WithSummary("Từ chối đơn");

        requests.MapPost("/{requestId:guid}/cancel", async (Guid requestId, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.CancelRequestAsync(CurrentUser.From(user), requestId, ct)).ToHttp(_ => TypedResults.NoContent()))
            .WithName("CancelShiftRequest").WithSummary("Hủy đơn đang chờ");

        var employees = app.MapGroup("/api").WithTags("Nhân viên").RequireManagement();

        employees.MapGet("/branches/{branchId:guid}/employees", async (Guid branchId, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.ListEmployeesAsync(CurrentUser.From(user), branchId, ct)).ToHttp())
            .WithName("ListBranchEmployees").WithSummary("Nhân viên của chi nhánh");

        employees.MapGet("/employees", async (ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ListAllEmployeesAsync(CurrentUser.From(user), ct)))
            .WithName("ListAllEmployees")
            .WithSummary("Tất cả nhân viên trong phạm vi quản lý")
            .WithDescription("Admin thấy toàn chuỗi; BranchManager thấy nhân viên thuộc các chi nhánh mình quản lý.");

        employees.MapPut("/employees/{employeeId:guid}", async (Guid employeeId, UpdateEmployeeRequest request, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.UpdateEmployeeAsync(CurrentUser.From(user), employeeId, request, ct)).ToHttp())
            .WithName("UpdateEmployee")
            .WithSummary("Sửa lương giờ / phụ cấp ca đêm")
            .WithDescription("BranchManager chỉ sửa được nhân viên vai trò Staff thuộc chi nhánh mình.");

        employees.MapPut("/employees/{employeeId:guid}/branches", async (Guid employeeId, UpdateEmployeeBranchesRequest request, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.UpdateBranchesAsync(CurrentUser.From(user), employeeId, request, ct)).ToHttp())
            .WithName("SetEmployeeWorkBranches")
            .WithSummary("Gán chi nhánh làm việc")
            .WithDescription("BranchManager chỉ thêm/bớt chi nhánh mình quản lý cho nhân viên Staff.");

        employees.MapGet("/scheduling/assistant", async (Guid branchId, DateOnly from, DateOnly to, ClaimsPrincipal user, WorkforceService service, CancellationToken ct) =>
                (await service.AssistantAsync(CurrentUser.From(user), branchId, from, to, ct)).ToHttp())
            .WithName("SchedulingAssistant").WithSummary("Trợ lý xếp ca: giờ rảnh và ca đã có của từng nhân viên");
    }
}
