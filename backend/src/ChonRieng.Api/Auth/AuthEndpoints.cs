using System.Security.Claims;
using ChonRieng.Application.Features.Auth;
using ChonRieng.Application.Features.Auth.Dtos;

namespace ChonRieng.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", Login)
            .AllowAnonymous()
            .WithName("Login")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/refresh", Refresh)
            .AllowAnonymous()
            .WithName("Refresh")
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
            .AllowAnonymous()
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", Me)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .Produces<AuthUserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> Login(
        LoginRequest? request,
        IAuthService auth,
        AuthCookieWriter cookies,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return AuthProblems.Validation();
        }

        var outcome = await auth.LoginAsync(request, cancellationToken);
        return ToHttp(outcome, cookies, http);
    }

    private static async Task<IResult> Refresh(
        IAuthService auth,
        AuthCookieWriter cookies,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var outcome = await auth.RefreshAsync(cookies.ReadRefreshToken(http.Request), cancellationToken);
        return ToHttp(outcome, cookies, http);
    }

    private static async Task<IResult> Logout(
        IAuthService auth,
        AuthCookieWriter cookies,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(cookies.ReadRefreshToken(http.Request), cancellationToken);
        cookies.ClearRefreshToken(http.Response);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(
        ClaimsPrincipal principal,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return AuthProblems.InvalidToken();
        }

        var user = await auth.GetCurrentUserAsync(userId, cancellationToken);
        return user is null ? AuthProblems.InvalidToken() : Results.Ok(user);
    }

    private static IResult ToHttp(AuthOutcome outcome, AuthCookieWriter cookies, HttpContext http)
    {
        if (!outcome.Succeeded)
        {
            return AuthProblems.From(outcome);
        }

        if (!string.IsNullOrWhiteSpace(outcome.RefreshToken))
        {
            cookies.SetRefreshToken(http.Response, outcome.RefreshToken);
        }

        return Results.Ok(outcome.Body);
    }
}

public static class AuthProblems
{
    public static IResult From(AuthOutcome outcome) =>
        Results.Problem(
            type: outcome.ErrorType,
            title: outcome.Detail,
            detail: outcome.Detail,
            statusCode: outcome.StatusCode);

    public static IResult Validation() =>
        Results.Problem(
            type: AuthErrorTypes.Validation,
            title: AuthErrorDetails.Validation,
            detail: AuthErrorDetails.Validation,
            statusCode: StatusCodes.Status400BadRequest);

    public static IResult InvalidToken() =>
        Results.Problem(
            type: AuthErrorTypes.InvalidToken,
            title: "Unauthorized",
            detail: AuthErrorDetails.SessionRevoked,
            statusCode: StatusCodes.Status401Unauthorized);
}
