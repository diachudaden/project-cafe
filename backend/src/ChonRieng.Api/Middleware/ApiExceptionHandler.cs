using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChonRieng.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException)
        {
            return false;
        }

        logger.LogWarning(
            "Database write conflicted with a constraint or concurrent update for {RequestPath}.",
            httpContext.Request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Dữ liệu bị xung đột",
            Detail = "Thay đổi không phù hợp với trạng thái hiện tại của dữ liệu hoặc bản ghi liên quan.",
            Instance = httpContext.Request.Path,
        }, cancellationToken);
        return true;
    }
}
