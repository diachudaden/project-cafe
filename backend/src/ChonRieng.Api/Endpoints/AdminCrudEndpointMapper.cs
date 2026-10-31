using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Text.Json;
using ChonRieng.Application.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace ChonRieng.Api.Endpoints;

/// <summary>A bounded page of administrator-managed resources.</summary>
public sealed record PageResponse<T>(int Page, int PageSize, IReadOnlyList<T> Items);

public static class AdminCrudEndpointMapper
{
    public static RouteGroupBuilder MapCrud<TEntity, TCreate, TUpdate, TResponse>(
        this RouteGroupBuilder group,
        string route,
        Func<TCreate, TEntity> create,
        Action<TEntity, TUpdate> update,
        Func<TEntity, TResponse> response,
        Func<TEntity, Guid> getId)
        where TEntity : class
        where TCreate : class
        where TUpdate : class
    {
        group.MapGet("/", async Task<Results<Ok<PageResponse<TResponse>>, BadRequest<ProblemDetails>>> (
            [Description("Số trang, bắt đầu từ 1; mặc định là 1.")]
            int? page,
            [Description("Số bản ghi mỗi trang, từ 1 đến 100; mặc định là 50.")]
            int? pageSize,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            if (page is < 1 || pageSize is < 1 or > 100)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Tham số phân trang không hợp lệ",
                    Detail = "page phải lớn hơn 0 và pageSize phải nằm trong khoảng từ 1 đến 100.",
                });
            }

            var currentPage = page ?? 1;
            var currentPageSize = pageSize ?? 50;
            if (currentPage > int.MaxValue / currentPageSize)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Tham số phân trang không hợp lệ",
                    Detail = "Số trang yêu cầu vượt quá giới hạn hỗ trợ.",
                });
            }

            var entities = await service.ListAsync(currentPage, currentPageSize, cancellationToken);
            return TypedResults.Ok(new PageResponse<TResponse>(
                currentPage,
                currentPageSize,
                entities.Select(response).ToArray()));
        })
            .WithName($"List{route}")
            .WithSummary($"Liệt kê {GetVietnameseResourceName(route)}")
            .WithDescription($"Trả về một trang dữ liệu {GetVietnameseResourceName(route)}. Chỉ tài khoản Admin được sử dụng API quản trị tổng quát này.")
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async Task<Results<Ok<TResponse>, NotFound>> (
            Guid id,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            var entity = await service.FindAsync(id, cancellationToken);
            return entity is null ? TypedResults.NotFound() : TypedResults.Ok(response(entity));
        })
            .WithName($"Get{route}")
            .WithSummary($"Lấy {GetVietnameseResourceName(route)} theo mã")
            .WithDescription("Chỉ tài khoản Admin có quyền truy cập toàn hệ thống. Trả về 404 nếu không tìm thấy bản ghi.")
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
            HttpContext httpContext,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            var request = await ReadRequestAsync<TCreate>(httpContext, cancellationToken);
            if (request is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Request"] = ["Nội dung yêu cầu phải là một đối tượng JSON hợp lệ."],
                });
            }

            var errors = ValidateRequest(request);
            if (errors.Count != 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var entity = await service.CreateAsync(create(request), cancellationToken);
            var id = getId(entity);
            return (IResult)TypedResults.Created($"/api/admin/{route.ToLowerInvariant()}/{id}", response(entity));
        })
            .WithName($"Create{route}")
            .WithSummary($"Tạo {GetVietnameseResourceName(route)}")
            .WithDescription("Chỉ tài khoản Admin có quyền truy cập toàn hệ thống. Dữ liệu không hợp lệ trả về 400; vi phạm ràng buộc dữ liệu trả về 409.")
            .Accepts<TCreate>("application/json")
            .Produces<TResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", async (
            HttpContext httpContext,
            Guid id,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            var request = await ReadRequestAsync<TUpdate>(httpContext, cancellationToken);
            if (request is null)
            {
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["Request"] = ["Nội dung yêu cầu phải là một đối tượng JSON hợp lệ."],
                });
            }

            var errors = ValidateRequest(request);
            if (errors.Count != 0)
            {
                return TypedResults.ValidationProblem(errors);
            }

            var entity = await service.UpdateAsync(id, item => update(item, request), cancellationToken);
            return entity is null
                ? (IResult)TypedResults.NotFound()
                : TypedResults.Ok(response(entity));
        })
            .WithName($"Update{route}")
            .WithSummary($"Cập nhật {GetVietnameseResourceName(route)}")
            .WithDescription("Chỉ tài khoản Admin có quyền truy cập toàn hệ thống. Trả về 404 nếu không tìm thấy và 409 nếu dữ liệu xung đột.")
            .Accepts<TUpdate>("application/json")
            .Produces<TResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id,
            IEntityCrudService<TEntity> service,
            CancellationToken cancellationToken) =>
        {
            return await service.DeleteAsync(id, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.NotFound();
        })
            .WithName($"Delete{route}")
            .WithSummary($"Xóa {GetVietnameseResourceName(route)}")
            .WithDescription("Chỉ tài khoản Admin có quyền truy cập toàn hệ thống. Trả về 404 nếu không tìm thấy hoặc 409 nếu bản ghi liên quan không cho phép xóa.")
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<TRequest?> ReadRequestAsync<TRequest>(
        HttpContext httpContext,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        try
        {
            return await httpContext.Request.ReadFromJsonAsync<TRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string[]> ValidateRequest<TRequest>(TRequest request)
        where TRequest : class
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        return validationResults
            .SelectMany(result => result.MemberNames.DefaultIfEmpty("Request")
                .Select(member => new { Member = member, result.ErrorMessage }))
            .GroupBy(item => item.Member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ErrorMessage ?? "The value is invalid.").ToArray());
    }

    private static string GetVietnameseResourceName(string route)
    {
        return route switch
        {
            "Branches" => "chi nhánh",
            "Zones" => "khu vực",
            "Seats" => "chỗ ngồi",
            "PricePlans" => "bảng giá",
            "Employees" => "nhân viên",
            "Customers" => "hồ sơ khách hàng",
            "Shifts" => "ca làm",
            "ShiftAssignments" => "phân công ca",
            "Attendances" => "chấm công",
            "ShiftClosings" => "biên bản kết ca",
            "Sessions" => "lượt sử dụng chỗ",
            "SessionExtensions" => "lần gia hạn",
            "Bills" => "hóa đơn",
            "PointTransactions" => "giao dịch điểm",
            "Expenses" => "khoản chi",
            "Payrolls" => "bảng lương",
            "EmployeeBranches" => "phân công nhân viên theo chi nhánh",
            "Availabilities" => "lịch rảnh",
            "ShiftRequests" => "yêu cầu đổi ca/nghỉ",
            "Notifications" => "thông báo",
            "Users" => "tài khoản",
            _ => route,
        };
    }
}
