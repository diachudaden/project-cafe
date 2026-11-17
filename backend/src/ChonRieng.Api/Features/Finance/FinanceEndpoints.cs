using System.Security.Claims;
using ChonRieng.Api.Features.Common;
using ChonRieng.Domain.Entities;

namespace ChonRieng.Api.Features.Finance;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app)
    {
        var closing = app.MapGroup("/api").WithTags("Kết ca").RequireWorkforce();

        closing.MapGet("/shifts/{shiftId:guid}/closing-preview", async (Guid shiftId, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.PreviewAsync(CurrentUser.From(user), shiftId, ct)).ToHttp())
            .WithName("ClosingPreview")
            .WithSummary("Số liệu đối chiếu kết ca")
            .WithDescription("Tiền mặt dự kiến = tổng bill tiền mặt của ca. Nhân viên đã tham gia ca hoặc quản lý chi nhánh được xem.");

        closing.MapPost("/shifts/{shiftId:guid}/closing", async (Guid shiftId, SubmitClosingRequest request, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.SubmitClosingAsync(CurrentUser.From(user), shiftId, request, ct)).ToHttp())
            .WithName("SubmitClosing")
            .WithSummary("Lưu nháp / nộp biên bản kết ca")
            .WithDescription("Wizard 3 bước: đếm tiền theo mệnh giá → đối chiếu → bàn giao. Nộp (submit = true) sẽ tự rời ca; có chênh lệch thì bắt buộc ghi chú.");

        closing.MapGet("/shift-closings", async (Guid branchId, DateOnly from, DateOnly to, ClosingStatus? status, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.ListClosingsAsync(CurrentUser.From(user), branchId, from, to, status, ct)).ToHttp())
            .RequireManagement()
            .WithName("ListClosings").WithSummary("Biên bản kết ca của chi nhánh");

        closing.MapPost("/shift-closings/{closingId:guid}/review", async (Guid closingId, ReviewClosingRequest request, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.ReviewClosingAsync(CurrentUser.From(user), closingId, request, ct)).ToHttp())
            .RequireManagement()
            .WithName("ReviewClosing").WithSummary("Duyệt / từ chối biên bản kết ca (từ chối cần lý do)");

        var finance = app.MapGroup("/api").WithTags("Thu chi & lương").RequireManagement();

        finance.MapGet("/expenses", async (Guid branchId, DateOnly from, DateOnly to, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.ListExpensesAsync(CurrentUser.From(user), branchId, from, to, ct)).ToHttp())
            .WithName("ListBranchExpenses").WithSummary("Sổ chi của chi nhánh");

        finance.MapPost("/expenses", async (CreateExpenseRequest request, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.CreateExpenseAsync(CurrentUser.From(user), request, ct)).ToHttp(e => TypedResults.Created($"/api/expenses/{e.Id}", e)))
            .WithName("CreateExpense").WithSummary("Ghi khoản chi");

        finance.MapPost("/expenses/{expenseId:guid}/reverse", async (Guid expenseId, ReverseExpenseRequest request, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.ReverseExpenseAsync(CurrentUser.From(user), expenseId, request.Reason, ct)).ToHttp())
            .WithName("ReverseExpense")
            .WithSummary("Ghi bút toán đảo")
            .WithDescription("Sổ chỉ thêm: không sửa/xóa, sai thì ghi một bút toán âm tham chiếu bút toán gốc.");

        finance.MapGet("/payrolls", async (Guid branchId, DateOnly month, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.ListPayrollAsync(CurrentUser.From(user), branchId, month, ct)).ToHttp())
            .WithName("ListPayroll").WithSummary("Bảng lương tháng của nhân viên có chi nhánh chính là branchId");

        finance.MapPost("/payrolls/generate", async (GeneratePayrollRequest request, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.GeneratePayrollAsync(CurrentUser.From(user), request, ct)).ToHttp())
            .WithName("GeneratePayroll")
            .WithSummary("Tính lương tạm (Draft) từ chấm công đã duyệt")
            .WithDescription("Lương = giờ công × lương giờ + giờ đêm (22:00–06:00) × phụ cấp + thưởng − phạt. Bảng lương đã chốt không bị tính lại.");

        finance.MapPut("/payrolls/{payrollId:guid}", async (Guid payrollId, AdjustPayrollRequest request, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.AdjustPayrollAsync(CurrentUser.From(user), payrollId, request, ct)).ToHttp())
            .WithName("AdjustPayroll").WithSummary("Điều chỉnh thưởng / phạt (chỉ khi Draft)");

        finance.MapPost("/payrolls/{payrollId:guid}/finalize", async (Guid payrollId, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.FinalizePayrollAsync(CurrentUser.From(user), payrollId, ct)).ToHttp())
            .WithName("FinalizePayroll").WithSummary("Chốt lương (chỉ Admin) và gửi cho nhân viên");

        app.MapGet("/api/me/payroll", async (DateOnly month, ClaimsPrincipal user, FinanceService service, CancellationToken ct) =>
                (await service.MyPayrollAsync(CurrentUser.From(user), month, ct)).ToHttp())
            .WithTags("Của tôi")
            .RequireWorkforce()
            .WithName("MyPayroll").WithSummary("Lương của tôi: giờ công từng ca, phụ cấp đêm, tạm tính và đã chốt");
    }
}
