using ChonRieng.Api.Authorization;
using System.Security.Cryptography;
using ChonRieng.Api.Features.Sales;
using ChonRieng.Api.Services;
using ChonRieng.Domain.Entities;
using ChonRieng.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ChonRieng.Api.Endpoints;

public static class AdminCrudEndpoints
{
    public static void MapAdminCrudEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin")
            .WithTags("Quản trị")
            .RequireAuthorization(policy => policy.RequireRole(BusinessRoles.Admin))
            .WithDescription(
                "Các API trong nhóm này hiện chỉ dành cho tài khoản có vai trò Admin và có phạm vi toàn hệ thống. CRUD tổng quát chưa được mở cho BranchManager, Staff hoặc Customer.")
            .WithMetadata(new AuthorizationDescriptionMetadata(
                "Chỉ vai trò Admin được sử dụng các API quản trị tổng quát; quyền áp dụng trên toàn hệ thống."));

        admin.MapGroup("/branches").MapCrud<Branch, CreateBranchRequest, UpdateBranchRequest, BranchResponse>(
            "Branches",
            request => new Branch
            {
                Name = request.Name,
                Address = request.Address,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Is24h = request.Is24h,
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
                Phone = request.Phone,
                IsActive = request.IsActive,
            },
            (entity, request) =>
            {
                entity.Name = request.Name;
                entity.Address = request.Address;
                entity.Latitude = request.Latitude;
                entity.Longitude = request.Longitude;
                entity.Is24h = request.Is24h;
                entity.OpenTime = request.OpenTime;
                entity.CloseTime = request.CloseTime;
                entity.Phone = request.Phone;
                entity.IsActive = request.IsActive;
            },
            entity => new BranchResponse(entity.Id, entity.Name, entity.Address, entity.Latitude, entity.Longitude, entity.Is24h, entity.OpenTime, entity.CloseTime, entity.Phone, entity.IsActive),
            entity => entity.Id);

        admin.MapGroup("/zones").MapCrud<Zone, CreateZoneRequest, UpdateZoneRequest, ZoneResponse>(
            "Zones",
            request => new Zone { BranchId = request.BranchId, Name = request.Name },
            (entity, request) => { entity.BranchId = request.BranchId; entity.Name = request.Name; },
            entity => new ZoneResponse(entity.Id, entity.BranchId, entity.Name),
            entity => entity.Id);

        admin.MapGroup("/seats").MapCrud<Seat, CreateSeatRequest, UpdateSeatRequest, SeatResponse>(
            "Seats",
            request => new Seat { ZoneId = request.ZoneId, Code = request.Code, Type = request.Type, Status = request.Status },
            (entity, request) => { entity.ZoneId = request.ZoneId; entity.Code = request.Code; entity.Type = request.Type; entity.Status = request.Status; },
            entity => new SeatResponse(entity.Id, entity.ZoneId, entity.Code, entity.Type, entity.Status),
            entity => entity.Id);

        admin.MapGroup("/price-plans").MapCrud<PricePlan, CreatePricePlanRequest, UpdatePricePlanRequest, PricePlanResponse>(
            "PricePlans",
            request => new PricePlan { BranchId = request.BranchId, Name = request.Name, DurationMinutes = request.DurationMinutes, Price = request.Price, ExtendPrice = request.ExtendPrice, ValidFrom = request.ValidFrom, ValidTo = request.ValidTo },
            (entity, request) => { entity.BranchId = request.BranchId; entity.Name = request.Name; entity.DurationMinutes = request.DurationMinutes; entity.Price = request.Price; entity.ExtendPrice = request.ExtendPrice; entity.ValidFrom = request.ValidFrom; entity.ValidTo = request.ValidTo; },
            entity => new PricePlanResponse(entity.Id, entity.BranchId, entity.Name, entity.DurationMinutes, entity.Price, entity.ExtendPrice, entity.ValidFrom, entity.ValidTo),
            entity => entity.Id);

        var pinHasher = new PasswordHasher<Employee>();
        admin.MapGroup("/employees").MapCrud<Employee, CreateEmployeeRequest, UpdateEmployeeRequest, EmployeeResponse>(
            "Employees",
            request =>
            {
                var employee = new Employee { UserId = request.UserId, HourlyRate = request.HourlyRate, NightAllowance = request.NightAllowance };
                employee.PinHash = pinHasher.HashPassword(employee, request.InitialPin);
                return employee;
            },
            (entity, request) =>
            {
                entity.HourlyRate = request.HourlyRate;
                entity.NightAllowance = request.NightAllowance;
                if (request.NewPin is not null)
                {
                    entity.PinHash = pinHasher.HashPassword(entity, request.NewPin);
                }
            },
            entity => new EmployeeResponse(entity.Id, entity.UserId, entity.HourlyRate, entity.NightAllowance),
            entity => entity.Id);

        admin.MapGroup("/customers").MapCrud<Customer, CreateCustomerRequest, UpdateCustomerRequest, CustomerResponse>(
            "Customers",
            request => new Customer { UserId = request.UserId, Points = request.Points, Tier = request.Tier, TotalSpent = request.TotalSpent },
            (entity, request) => { entity.UserId = request.UserId; entity.Points = request.Points; entity.Tier = request.Tier; entity.TotalSpent = request.TotalSpent; },
            entity => new CustomerResponse(entity.Id, entity.UserId, entity.Points, entity.Tier, entity.TotalSpent),
            entity => entity.Id);

        admin.MapGroup("/shifts").MapCrud<Shift, CreateShiftRequest, UpdateShiftRequest, ShiftResponse>(
            "Shifts",
            request => new Shift { BranchId = request.BranchId, Date = request.Date, Type = request.Type, StartAt = request.StartAt, EndAt = request.EndAt, RequiredStaff = request.RequiredStaff, IsOpen = request.IsOpen, Status = request.Status },
            (entity, request) => { entity.BranchId = request.BranchId; entity.Date = request.Date; entity.Type = request.Type; entity.StartAt = request.StartAt; entity.EndAt = request.EndAt; entity.RequiredStaff = request.RequiredStaff; entity.IsOpen = request.IsOpen; entity.Status = request.Status; },
            entity => new ShiftResponse(entity.Id, entity.BranchId, entity.Date, entity.Type, entity.StartAt, entity.EndAt, entity.RequiredStaff, entity.IsOpen, entity.Status),
            entity => entity.Id);

        admin.MapGroup("/shift-assignments").MapCrud<ShiftAssignment, CreateShiftAssignmentRequest, UpdateShiftAssignmentRequest, ShiftAssignmentResponse>(
            "ShiftAssignments",
            request => new ShiftAssignment { ShiftId = request.ShiftId, EmployeeId = request.EmployeeId, Status = request.Status, RespondedAt = request.RespondedAt },
            (entity, request) => { entity.ShiftId = request.ShiftId; entity.EmployeeId = request.EmployeeId; entity.Status = request.Status; entity.RespondedAt = request.RespondedAt; },
            entity => new ShiftAssignmentResponse(entity.Id, entity.ShiftId, entity.EmployeeId, entity.Status, entity.RespondedAt),
            entity => entity.Id);

        admin.MapGroup("/attendances").MapCrud<Attendance, CreateAttendanceRequest, UpdateAttendanceRequest, AttendanceResponse>(
            "Attendances",
            request => new Attendance { EmployeeId = request.EmployeeId, ShiftId = request.ShiftId, CheckInAt = request.CheckInAt, CheckOutAt = request.CheckOutAt, Method = request.Method, Approved = request.Approved },
            (entity, request) => { entity.EmployeeId = request.EmployeeId; entity.ShiftId = request.ShiftId; entity.CheckInAt = request.CheckInAt; entity.CheckOutAt = request.CheckOutAt; entity.Method = request.Method; entity.Approved = request.Approved; },
            entity => new AttendanceResponse(entity.Id, entity.EmployeeId, entity.ShiftId, entity.CheckInAt, entity.CheckOutAt, entity.Method, entity.Approved),
            entity => entity.Id);

        admin.MapGroup("/shift-closings").MapReadOnlyCrud<ShiftClosing, ShiftClosingResponse>(
            "ShiftClosings",
            entity => new ShiftClosingResponse(entity.Id, entity.ShiftId, entity.ClosedByEmployeeId, entity.ExpectedCash, entity.CountedCash, entity.Difference, entity.Note, entity.Denominations));

        admin.MapGroup("/sessions").MapReadOnlyCrud<Session, SessionResponse>(
            "Sessions",
            entity => new SessionResponse(entity.Id, entity.SeatId, entity.CustomerId, entity.PricePlanId, entity.StartAt, entity.EndAt, entity.Status));

        admin.MapGroup("/session-extensions").MapReadOnlyCrud<SessionExtension, SessionExtensionResponse>(
            "SessionExtensions",
            entity => new SessionExtensionResponse(entity.Id, entity.SessionId, entity.AddedMinutes, entity.Price, entity.CreatedAt));

        admin.MapGroup("/bills").MapReadOnlyCrud<Bill, BillResponse>(
            "Bills",
            entity => new BillResponse(entity.Id, entity.Code, entity.BranchId, entity.ShiftId, entity.SessionId, entity.Total, entity.PaymentMethod, entity.ClaimedByCustomerId, entity.ClaimedAt));

        admin.MapGroup("/point-transactions").MapReadOnlyCrud<PointTransaction, PointTransactionResponse>(
            "PointTransactions",
            entity => new PointTransactionResponse(entity.Id, entity.CustomerId, entity.BillId, entity.Points, entity.Reason));

        admin.MapGroup("/expenses").MapReadOnlyCrud<Expense, ExpenseResponse>(
            "Expenses",
            entity => new ExpenseResponse(entity.Id, entity.BranchId, entity.Category, entity.Amount, entity.Date, entity.Note, entity.CreatedByEmployeeId));

        admin.MapGroup("/payrolls").MapReadOnlyCrud<Payroll, PayrollResponse>(
            "Payrolls",
            entity => new PayrollResponse(entity.Id, entity.EmployeeId, entity.Month, entity.TotalHours, entity.BaseAmount, entity.Allowance, entity.Bonus, entity.Deduction, entity.NetAmount, entity.Status));

        admin.MapGroup("/employee-branches").MapCrud<EmployeeBranch, CreateEmployeeBranchRequest, UpdateEmployeeBranchRequest, EmployeeBranchResponse>(
            "EmployeeBranches",
            request => new EmployeeBranch { EmployeeId = request.EmployeeId, BranchId = request.BranchId, IsHome = request.IsHome },
            (entity, request) => { entity.EmployeeId = request.EmployeeId; entity.BranchId = request.BranchId; entity.IsHome = request.IsHome; },
            entity => new EmployeeBranchResponse(entity.Id, entity.EmployeeId, entity.BranchId, entity.IsHome),
            entity => entity.Id);

        admin.MapGroup("/availabilities").MapCrud<Availability, CreateAvailabilityRequest, UpdateAvailabilityRequest, AvailabilityResponse>(
            "Availabilities",
            request => new Availability { EmployeeId = request.EmployeeId, IsRecurring = request.IsRecurring, DayOfWeek = request.DayOfWeek, SpecificDate = request.SpecificDate, From = request.From, To = request.To },
            (entity, request) => { entity.EmployeeId = request.EmployeeId; entity.IsRecurring = request.IsRecurring; entity.DayOfWeek = request.DayOfWeek; entity.SpecificDate = request.SpecificDate; entity.From = request.From; entity.To = request.To; },
            entity => new AvailabilityResponse(entity.Id, entity.EmployeeId, entity.IsRecurring, entity.DayOfWeek, entity.SpecificDate, entity.From, entity.To),
            entity => entity.Id);

        admin.MapGroup("/shift-requests").MapCrud<ShiftRequest, CreateShiftRequestRequest, UpdateShiftRequestRequest, ShiftRequestResponse>(
            "ShiftRequests",
            request => new ShiftRequest { Type = request.Type, ShiftId = request.ShiftId, RequesterEmployeeId = request.RequesterEmployeeId, TargetEmployeeId = request.TargetEmployeeId, Reason = request.Reason, Status = request.Status, ReviewedByEmployeeId = request.ReviewedByEmployeeId, ReviewedAt = request.ReviewedAt },
            (entity, request) => { entity.Type = request.Type; entity.ShiftId = request.ShiftId; entity.RequesterEmployeeId = request.RequesterEmployeeId; entity.TargetEmployeeId = request.TargetEmployeeId; entity.Reason = request.Reason; entity.Status = request.Status; entity.ReviewedByEmployeeId = request.ReviewedByEmployeeId; entity.ReviewedAt = request.ReviewedAt; },
            entity => new ShiftRequestResponse(entity.Id, entity.Type, entity.ShiftId, entity.RequesterEmployeeId, entity.TargetEmployeeId, entity.Reason, entity.Status, entity.ReviewedByEmployeeId, entity.ReviewedAt),
            entity => entity.Id);

        admin.MapGroup("/notifications").MapCrud<Notification, CreateNotificationRequest, UpdateNotificationRequest, NotificationResponse>(
            "Notifications",
            request => new Notification { UserId = request.UserId, Type = request.Type, Title = request.Title, Body = request.Body, Link = request.Link, ReadAt = request.ReadAt },
            (entity, request) => { entity.UserId = request.UserId; entity.Type = request.Type; entity.Title = request.Title; entity.Body = request.Body; entity.Link = request.Link; entity.ReadAt = request.ReadAt; },
            entity => new NotificationResponse(entity.Id, entity.UserId, entity.Type, entity.Title, entity.Body, entity.Link, entity.ReadAt),
            entity => entity.Id);

        MapAdminUserEndpoints(admin);
    }

    private static void MapAdminUserEndpoints(RouteGroupBuilder admin)
    {
        var users = admin.MapGroup("/users")
            .WithTags("Quản lý tài khoản");

        users.MapGet("/", async (
            int? page,
            int? pageSize,
            string? search,
            string? role,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var currentPage = page ?? 1;
            var currentPageSize = pageSize ?? 50;
            if (currentPage < 1 || currentPageSize is < 1 or > 100)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Tham số phân trang không hợp lệ",
                    detail: "page phải lớn hơn 0 và pageSize phải nằm trong khoảng từ 1 đến 100.");
            }

            if (currentPage > int.MaxValue / currentPageSize)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Tham số phân trang không hợp lệ",
                    detail: "Số trang yêu cầu vượt quá giới hạn hỗ trợ.");
            }

            var query = dbContext.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Case-insensitive match on name, email or phone; phone also matches without spaces/dots.
                var term = search.Trim().ToLower();
                var digits = new string(term.Where(char.IsDigit).ToArray());
                query = query.Where(user => user.FullName.ToLower().Contains(term)
                    || (user.Email != null && user.Email.ToLower().Contains(term))
                    || (user.PhoneNumber != null && (user.PhoneNumber.Contains(term)
                        || (digits.Length > 0 && user.PhoneNumber.Replace(" ", "").Replace(".", "").Replace("-", "").Contains(digits)))));
            }

            var roleNames = (role ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (roleNames.Length > 0)
            {
                var roleIds = dbContext.Roles.Where(r => roleNames.Contains(r.Name!)).Select(r => r.Id);
                query = query.Where(user => dbContext.UserRoles.Any(ur => ur.UserId == user.Id && roleIds.Contains(ur.RoleId)));
            }

            var rows = await query
                .OrderBy(user => user.FullName).ThenBy(user => user.Id)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .Select(user => new
                {
                    user.Id,
                    Email = user.Email ?? "",
                    user.FullName,
                    user.PhoneNumber,
                    user.LockoutEnd,
                    Roles = dbContext.UserRoles.Where(ur => ur.UserId == user.Id)
                        .Join(dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!).ToList(),
                })
                .ToArrayAsync(cancellationToken);
            var pageUsers = rows
                .Select(user => new AdminUserResponse(user.Id, user.Email, user.FullName, user.PhoneNumber, user.LockoutEnd, user.Roles))
                .ToArray();
            return Results.Ok(new PageResponse<AdminUserResponse>(currentPage, currentPageSize, pageUsers));
        })
            .WithName("ListUsers")
            .WithSummary("Liệt kê tài khoản")
            .WithDescription("Trả về danh sách tài khoản có phân trang kèm vai trò; lọc theo search (họ tên, email, số điện thoại) và role (một hoặc nhiều vai trò, phân tách bằng dấu phẩy). Không bao gồm mật khẩu, mã băm hoặc trường bảo mật của Identity.")
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        users.MapGet("/{id:guid}", async Task<Results<Ok<AdminUserDetailResponse>, NotFound>> (
            Guid id,
            UserManager<User> userManager,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            var roles = (await userManager.GetRolesAsync(user)).ToArray();
            return TypedResults.Ok(new AdminUserDetailResponse(user.Id, user.Email ?? "", user.FullName, user.PhoneNumber, user.LockoutEnd, roles));
        })
            .WithName("GetUser")
            .WithSummary("Lấy tài khoản theo mã")
            .WithDescription("Trả về hồ sơ an toàn và danh sách vai trò. Chỉ Admin được truy cập.")
            .Produces(StatusCodes.Status404NotFound);

        users.MapPost("/", async Task<Results<Created<AdminUserDetailResponse>, ValidationProblem>> (
            CreateAdminUserRequest request,
            UserManager<User> userManager,
            AppDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (request.Roles.Any(role => !BusinessRoles.All.Contains(role)))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Roles"] = ["Vai trò không hợp lệ. Các vai trò được hỗ trợ: Admin, BranchManager, Staff, Customer."],
                });
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
            };
            var createResult = await userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return TypedResults.ValidationProblem(IdentityErrorFormatter.ToValidationErrors(createResult).ToDictionary());
            }

            var roleResult = await userManager.AddToRolesAsync(user, request.Roles.Distinct(StringComparer.Ordinal));
            if (!roleResult.Succeeded)
            {
                return TypedResults.ValidationProblem(IdentityErrorFormatter.ToValidationErrors(roleResult).ToDictionary());
            }

            await EnsureRoleProfilesAsync(dbContext, userManager, user, request.Roles, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            var detail = new AdminUserDetailResponse(user.Id, user.Email ?? "", user.FullName, user.PhoneNumber, user.LockoutEnd, request.Roles.ToArray());
            return TypedResults.Created($"/api/admin/users/{user.Id}", detail);
        })
            .WithName("CreateUser")
            .WithSummary("Tạo tài khoản")
            .WithDescription("Tạo tài khoản và gán một hoặc nhiều vai trò được hỗ trợ: Admin, BranchManager, Staff, Customer. Chỉ Admin được thao tác.")
            .ProducesValidationProblem();

        users.MapPut("/{id:guid}", async Task<Results<Ok<AdminUserDetailResponse>, NotFound, ValidationProblem, Conflict<ProblemDetails>>> (
            Guid id,
            UpdateAdminUserRequest request,
            UserManager<User> userManager,
            AppDbContext dbContext,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (request.Roles.Any(role => !BusinessRoles.All.Contains(role)))
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Roles"] = ["Vai trò không hợp lệ. Các vai trò được hỗ trợ: Admin, BranchManager, Staff, Customer."],
                });
            }

            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            var newRoles = request.Roles.Distinct(StringComparer.Ordinal).ToArray();
            var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var currentRoles = await userManager.GetRolesAsync(user);
            if (Guid.TryParse(actorId, out var currentUserId)
                && currentUserId == id
                && currentRoles.Contains(BusinessRoles.Admin, StringComparer.Ordinal)
                && !newRoles.Contains(BusinessRoles.Admin, StringComparer.Ordinal))
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Không thể tự gỡ vai trò quản trị",
                    Detail = "Hãy dùng một tài khoản Admin khác để thay đổi vai trò của tài khoản này.",
                });
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            user.Email = request.Email;
            user.UserName = request.Email;
            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return TypedResults.ValidationProblem(IdentityErrorFormatter.ToValidationErrors(updateResult).ToDictionary());
            }

            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return TypedResults.ValidationProblem(IdentityErrorFormatter.ToValidationErrors(removeResult).ToDictionary());
            }

            var addResult = await userManager.AddToRolesAsync(user, newRoles);
            if (!addResult.Succeeded)
            {
                return TypedResults.ValidationProblem(IdentityErrorFormatter.ToValidationErrors(addResult).ToDictionary());
            }

            await EnsureRoleProfilesAsync(dbContext, userManager, user, newRoles, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return TypedResults.Ok(new AdminUserDetailResponse(user.Id, user.Email ?? "", user.FullName, user.PhoneNumber, user.LockoutEnd, newRoles));
        })
            .WithName("UpdateUser")
            .WithSummary("Cập nhật tài khoản")
            .WithDescription("Cập nhật hồ sơ và toàn bộ danh sách vai trò. Tài khoản có nhiều vai trò phải được cấp quyền theo chính sách cụ thể của từng endpoint.")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        users.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>>> (
            Guid id,
            UserManager<User> userManager,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (Guid.TryParse(actorId, out var currentUserId) && currentUserId == id)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Không thể xóa tài khoản đang đăng nhập",
                    Detail = "Hãy đăng nhập bằng một tài khoản Admin khác để xóa tài khoản này.",
                });
            }

            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null)
            {
                return TypedResults.NotFound();
            }

            var result = await userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                return TypedResults.NoContent();
            }

            return TypedResults.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Không thể xóa tài khoản",
                Detail = "Tài khoản đang liên kết với dữ liệu khác nên không thể xóa.",
            });
        })
            .WithName("DeleteUser")
            .WithSummary("Xóa tài khoản")
            .WithDescription("Chỉ Admin được thao tác. Không thể tự xóa tài khoản đang đăng nhập; lỗi liên kết dữ liệu trả về 409.")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>Customer accounts need a Customer profile; Staff/BranchManager accounts need an Employee record.</summary>
    private static async Task EnsureRoleProfilesAsync(
        AppDbContext dbContext,
        UserManager<User> userManager,
        User user,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Contains(BusinessRoles.Customer, StringComparer.Ordinal)
            && !await dbContext.Customers.AnyAsync(customer => customer.UserId == user.Id, cancellationToken))
        {
            dbContext.Customers.Add(new Customer
            {
                UserId = user.Id,
                Tier = Loyalty.Bronze,
                MemberCode = Loyalty.NewMemberCode(),
            });
        }

        if ((roles.Contains(BusinessRoles.Staff, StringComparer.Ordinal) || roles.Contains(BusinessRoles.BranchManager, StringComparer.Ordinal))
            && !await dbContext.Employees.AnyAsync(employee => employee.UserId == user.Id, cancellationToken))
        {
            // The PIN is random and discarded; PIN login is a later phase (decision D2).
            dbContext.Employees.Add(new Employee
            {
                UserId = user.Id,
                PinHash = userManager.PasswordHasher.HashPassword(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
