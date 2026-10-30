using ChonRieng.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ChonRieng.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", async Task<Results<Created<AuthTokenResponse>, ValidationProblem>> (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(request, cancellationToken);
            return result.Tokens is null
                ? TypedResults.ValidationProblem(result.Errors.ToDictionary())
                : TypedResults.Created("/api/auth/me", result.Tokens);
        })
            .AllowAnonymous()
            .WithName("RegisterCustomer")
            .WithSummary("Register a customer account")
            .WithDescription("Creates a customer account and returns an access token and refresh token.")
            .ProducesValidationProblem();

        group.MapPost("/login", async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult>> (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var tokens = await authService.LoginAsync(request, cancellationToken);
            return tokens is null ? TypedResults.Unauthorized() : TypedResults.Ok(tokens);
        })
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Sign in")
            .WithDescription("Checks credentials and issues an access token and refresh token.")
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async Task<Results<Ok<AuthTokenResponse>, UnauthorizedHttpResult>> (
            RefreshTokenRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var tokens = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
            return tokens is null ? TypedResults.Unauthorized() : TypedResults.Ok(tokens);
        })
            .AllowAnonymous()
            .WithName("RefreshTokens")
            .WithSummary("Rotate refresh token")
            .WithDescription("Consumes a valid refresh token and returns a newly rotated token pair.")
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async Task<NoContent> (
            RevokeTokenRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            await authService.RevokeAsync(request.RefreshToken, cancellationToken);
            return TypedResults.NoContent();
        })
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Revoke a refresh token")
            .WithDescription("Revokes the submitted refresh token. Repeated requests are safe.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", async Task<Results<Ok<AuthUserResponse>, NotFound>> (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var user = await authService.GetCurrentUserAsync(httpContext.User, cancellationToken);
            return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
        })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the current account")
            .WithDescription("Returns safe profile information for the authenticated account.");
    }
}
