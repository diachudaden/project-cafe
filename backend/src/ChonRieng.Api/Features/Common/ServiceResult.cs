using Microsoft.AspNetCore.Mvc;

namespace ChonRieng.Api.Features.Common;

/// <summary>A business-rule violation returned with 409 so the UI can show the exact reason.</summary>
public sealed record RuleViolation(string Rule, string Message, bool CanOverride = false, bool IsBlocking = true);

public sealed record ServiceError(
    int Status,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    IReadOnlyList<RuleViolation>? Violations = null)
{
    // Out-of-scope records also return 404 so other branches' data is not revealed.
    public static ServiceError NotFound(string detail = "Không tìm thấy dữ liệu hoặc bạn không có quyền truy cập.") =>
        new(StatusCodes.Status404NotFound, "Không tìm thấy", detail);

    public static ServiceError Forbidden(string detail) =>
        new(StatusCodes.Status403Forbidden, "Không có quyền", detail);

    public static ServiceError Invalid(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ", message, new Dictionary<string, string[]> { [field] = [message] });

    public static ServiceError Conflict(string title, string detail, IReadOnlyList<RuleViolation>? violations = null) =>
        new(StatusCodes.Status409Conflict, title, detail, Violations: violations);
}

public readonly struct Result<T>
{
    private Result(T? value, ServiceError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    // Interface-typed values (e.g. IReadOnlyList) cannot use the implicit conversion, so use From.
    public static Result<T> From(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => new(value, null);
    public static implicit operator Result<T>(ServiceError error) => new(default, error);
}

public static class ResultHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? TypedResults.Ok(result.Value) : onSuccess(result.Value!);
        }

        return result.Error!.ToHttp();
    }

    public static IResult ToHttp(this ServiceError error)
    {
        if (error.Status == StatusCodes.Status400BadRequest && error.Errors is not null)
        {
            return TypedResults.ValidationProblem(error.Errors.ToDictionary(), detail: error.Detail, title: error.Title);
        }

        var problem = new ProblemDetails { Status = error.Status, Title = error.Title, Detail = error.Detail };
        if (error.Violations is { Count: > 0 })
        {
            problem.Extensions["violations"] = error.Violations;
        }

        return TypedResults.Problem(problem);
    }
}

/// <summary>Marker value for operations that return no body.</summary>
public readonly record struct Done;
